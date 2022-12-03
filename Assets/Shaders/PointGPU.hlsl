float _Step;

// We shoudl only do this for shadr variants compiled for prcedural drawing.
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    // Now we can add a buffer similar to the compute shader but this time it is readonly since we dont need to write to it.
    StructuredBuffer<float3> _Positions; 
#endif

void ConfigureProcedural() {
    // we do the same here
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
        // we can globally access the id of the instance that is currently being drawn
        float3 position = _Positions[unity_InstanceID];

        // Usually we have a 4x4 transformation matrix where the last column is the x, y, z, 1 position
        // and the diagonal for the 3x3 sub matrix is the scale. We can access this globally with unity_ObjectToWorld.
        // but since we are drawing procedurally this is unity. and we ahve to set this
        unity_ObjectToWorld = 0.0; // set all elements to 0
        unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0); // We can access columns this way and set it to pos
        // our step defines how much we scale our cubes so lets request that as well
        unity_ObjectToWorld._m00_m11_m22 = _Step; // we are scaling it all uniformly

        // we also have the unity_WorldToObject matrix which is the inverse used for transforming normal vectors.
    #endif
}

// We can include this file in the shader graph but we dont actually have to run the procedural function
// So we can have a dummy function that just takes teh inputs and gives it back as an output
void ShaderGraphFunction_float (float3 In, out float3 Out) {
	Out = In;
}

void ShaderGraphFunction_half (half3 In, out half3 Out) {
	Out = In;
}