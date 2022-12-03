// required for all shaders will make the name given here availble for selection
Shader "Graph/Point Surface GPU" {

    Properties {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5 
    }

    SubShader {
        // Subshaders need to be in CGPROGRAM and ENDCG tags?
        CGPROGRAM
            // Compiler directive to make a "surface" shader with "Standard" lighting and full support for shadows
            // By default the ConfigureProcedural function will only get invoked for the regular draw pass.
            // To apply it when rendering shadows we ahve to indicate that we need a custom shadow pass with addshadow.
            #pragma surface ConfigureSurface Standard fullforwardshadows addshadow
            
            //This indicates that the surface shader needs to invoke a ConfigureProcedural function per vertex. 
            // It's a void function without any parameters.
            // assume uniform scaling lets us not worry about the normals needing recalculation due to non-uniform transformations
            // which we do not have in our case. 
            #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
            // Since we rely on a strucutured buffer filled by a compute shader we need to increase teh shader target level to 4.5
            #pragma target 4.5

            #include "PointGPU.hlsl"

            struct Input {
                float3 worldPos;
            };

            void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
                surface.Smoothness = 0.5;
                surface.Albedo = saturate(input.worldPos * 0.5 + 0.5);
            }
        ENDCG
    }
    
    // Fallback to the default diffuse shader in case of issues?
    FallBack "Diffuse"
}