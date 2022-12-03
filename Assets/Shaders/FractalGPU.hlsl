// We shoudl only do this for shadr variants compiled for prcedural drawing.
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    // We generate a buffer for the GPU to recieve the 4x4 TRS matrices from the CPU
    StructuredBuffer<float3x4> _Matrices; 
#endif

// we want to provide arbitrary colour

float4 _ColorA, _ColorB;
float4 _SequenceNumbers;

float4 GetFractalColour() {
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        float4 colour;
        colour.rgb = lerp(
			_ColorA.rgb, _ColorB.rgb,
			frac(unity_InstanceID * _SequenceNumbers.x + _SequenceNumbers.y)
		);
        colour.a = lerp(
			_ColorA.a, _ColorB.a,
			frac(unity_InstanceID * _SequenceNumbers.z + _SequenceNumbers.w)
		);
        return colour;
    #else
        return _ColorA;
    #endif
}

void ConfigureProcedural() {
    // we do the same here to make sure this is only run when procedural drawing is enabled
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        // as opposed to the graph shader we dont have to explicitly calculate the TRS matrix we already 
        // recieve it.
        // We optimized the script to send a 3x4 matrix so we need to reconstruct the full matrix.
        float3x4 m = _Matrices[unity_InstanceID];
        unity_ObjectToWorld._m00_m01_m02_m03 = m._m00_m01_m02_m03;
		unity_ObjectToWorld._m10_m11_m12_m13 = m._m10_m11_m12_m13;
		unity_ObjectToWorld._m20_m21_m22_m23 = m._m20_m21_m22_m23;
		unity_ObjectToWorld._m30_m31_m32_m33 = float4(0.0, 0.0, 0.0, 1.0);
    #endif
}

// We can include this file in the shader graph but we dont actually have to run the procedural function
// So we can have a dummy function that just takes teh inputs and gives it back as an output
void ShaderGraphFunction_float (float3 In, out float3 Out, out float4 FractalColour) {
	Out = In;
    FractalColour = GetFractalColour();
}

void ShaderGraphFunction_half (half3 In, out half3 Out, out half4 FractalColour) {
	Out = In;
    FractalColour = GetFractalColour();
}