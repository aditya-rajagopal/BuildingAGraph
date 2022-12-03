// required for all shaders will make the name given here availble for selection
Shader "Graph/Point Surface" {

    // Instead of declaring it in the subshader we can put it in the properties here
    // This will make it show up in the materials
    // Range(0, 1) i assume provides us with a slider and 0.5 is the default.
    Properties {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5 
    }
    // you can have as many sub-shaders as you want. Dont know yet what having multiple does
    SubShader {
        // Subshaders need to be in CGPROGRAM and ENDCG tags?
        CGPROGRAM
            // Compiler directive to make a "surface" shader with "Standard" lighting and full support for shadows
            #pragma surface ConfigureSurface Standard fullforwardshadows
            // When the number of objects becomes really large we can run into issues where unity does asynchronus shading
            // while waiting for the complilation and puts some default texture. This does not work with prcedural drawing
            // Hence we can set it here to sync to compilation or in proejct settings.
            #pragma editor_sync_compilation
            // sets miniumum for the shader's target level of quality
            #pragma target 3.0

            // Define a structure for our function. Goal is to colour the vertices of the cube with the world position.
            // So here we accept a vector of float3 (x, y, z) as the position.
            // Note that here you will recieve the worldPos of vertices so what we are colouring here are the vertices
            // This means we will have a gradient between teh vertices.
            struct Input {
                float3 worldPos;
            };

            // float _Smoothness; // We can declare a configurable smoothness variable here but this will not show up in the editor
            // Next we define the function we declared in the pragma directive.
            // here we accept the input structure as an input
            // the second paramter is an input as well as our output hence we must provide the inout keyword.
            void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
                // we can make the material more reflective by increasing smoothness.
                // It was perfectlty matte black before so i am assuming it was 0 by default?
                // Probably the colour(albedo) is also (0, 0, 0) by default.
                // Indeed 1 does make it perfectly reflecting.
                surface.Smoothness = 0.5;

                // Now lets set the colour of the points as the worldPos
                // we can direcly put a float3 into Albedo.
                // As expected the bottom left quadrant cubes are all black (negative x, y. z for these cubes are already very small)
                // I wonder how it treats numbers larger than say 255?
                // top left quadrant is green because negative x (presumably clamped to 0) and positive y (R G B)
                // surface.Albedo = input.worldPos; 
                // However the above approach of just setting world pos gives weird colours where each quadrant has its own colour and one of them is black. Additionally x is ranged (-1, 1) which is absurd for a colour
                // hence we will normalize it to 0, 1
                // surface.Albedo = input.worldPos * 0.5 + 0.5;
                // This makes the colours a lot more bright and removes the black in the bottom right quadrant.
                // The second apporach has 1 issue. Since the z values are very small at high resolutions it will almost alwasy be 0.5
                // making all the colours too blue
                // To solve this we can just access the xy of a flaot3 and apply it to the rg of the Albedo
                // surface.Albedo.rg = input.worldPos.xy * 0.5 + 0.5;
                // The last issue with the above methodology is that the green channel could exceed the bounds of [0, 1] due to functions exceeding 1
                // Therefore we use teh saturate function to clamp the colours to be between [0, 1]
                // surface.Albedo.rg = saturate(input.worldPos.xy * 0.5 + 0.5);
                // Now we want control colours in 3D so we will go back to using all 3 dimensions
                surface.Albedo = saturate(input.worldPos * 0.5 + 0.5);
            }
        ENDCG
    }
    
    // Fallback to the default diffuse shader in case of issues?
    FallBack "Diffuse"
}