using UnityEngine;
using static FunctionLibrary;

public class GPUGraph : MonoBehaviour
{
    [SerializeField]
    ComputeShader computeShader;

    [SerializeField]
	Material material;

	[SerializeField]
	Mesh mesh;

    const int maxResolution = 1000;
    [SerializeField, Range(1, maxResolution)]
    int resolution = 10;
    [SerializeField]
    FunctionName function;
    [SerializeField, Min(0f)]
    float functionDuration = 1f, transitionDuration = 1f;

    float duration;

	bool transitioning;

	FunctionLibrary.FunctionName transitionFunction;

    public enum TransitionMode { Cycle, Random }

	[SerializeField]
	TransitionMode transitionMode;

    ComputeBuffer positionsBuffer;


    // We need identifiers for properties of the compute shader. These are claimed on demand and dont change while
    // teh app is running\
    static readonly int
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionID = Shader.PropertyToID("_Resolution"),
        stepId = Shader.PropertyToID("_Step"),
        timeId = Shader.PropertyToID("_Time"),
        transitionProgressId = Shader.PropertyToID("_TransitionProgress");

    

    // Onenable is called each time after Awake and also after every hot reload
    void OnEnable()
    {
        // we enable the compute buffer with the number of slots we want
        // The second parameter is the size of each slot (stide)
        // Here we wish to pass a 3D vector which is 3 floats hence 3 * 4
        // We also instantiate teh full buffer we possibly can to avoid issues when changing resolution while runnign.
        positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4);
    }

    // OnDisable is called each time the component is destroyed and right before a hot reload
    void OnDisable()
    {
        // If the component is disabled/destroyed or a hot reload is abouyt to occur release our compute buffer
        positionsBuffer.Release();
        // to be safe also set it explictly to null. This allows it to be garbage collected.
        positionsBuffer = null;

        // Eventually though if teh compute buffer is not released and nothing holds a reference to it
        // It will be garbage collected but not at a specific time? better to manually release and dereference it.
    }


    void Update() {
        duration += Time.deltaTime;
        if (transitioning) {
            if (duration >= transitionDuration) {
				duration -= transitionDuration;
				transitioning = false;
			}
        }
		else if (duration >= functionDuration) {
			duration -= functionDuration;
            transitioning = true;
			transitionFunction = function;
            function = FunctionLibrary.GetNextFunctionName(function);
		}

        UpdateFunctionOnGPU();
    }

    void PickNextFunction () {
		function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
	}

    void UpdateFunctionOnGPU (){
        // Here we set the 3 variables in the compute shader
        float step = 2f / resolution;
        computeShader.SetInt(resolutionID, resolution);
        computeShader.SetFloat(stepId, step);
        computeShader.SetFloat(timeId, Time.time);
        if (transitioning) {
			computeShader.SetFloat(
				transitionProgressId,
				Mathf.SmoothStep(0f, 1f, duration / transitionDuration)
			);
		}

        // The position buffer is a bit different as it is not somethiing we are writing to only
        // but instead we link to the buffer.
        // It has an extra parameter which is the Kernel index. We can use the findKernel function
        // but in our case only have 1 kernel which is always 0.
        // int kernelID = computeShader.FindKernel("FunctionKernel");
        var kernelID =
			(int)function + (int)(transitioning ? transitionFunction : function) * 5;
        computeShader.SetBuffer(kernelID, positionsId, positionsBuffer);

        // once we have our buffer set we can invoke Dispatch on the compute shader with 4 parameters
        // First one is the kernel ID and the other 3 are the amount of groups to run in each dimension.
        // Since we have 8 threads in each direction, we need resolution/8 groups in each x and y direction(rounded up).
        int groups = Mathf.CeilToInt(resolution / 8f);
        // we then dispatch the command to compute the positions with z = 1 (we dont need this)
        // also make sure to dispatch to the correct kernel ID.
        computeShader.Dispatch(kernelID, groups, groups, 1);

        // We already have all the locations we want to draw our cubes but isntead of taking the points back to the CPU
        // we can just send a mesh and material to the GPU and ahve it draw the cubes procedurally
        // We can do this with Graphics.DrawMeshInstancedProcedural

        // Because this does not use game objects unity does not know where in teh scene this stuff is happening
        // So we need to provide a bounding box. This box is the bounds of the spatial box in which we are drawing.
        // This is used to determine if this drawing can be skipped as it can be outside of view i.e frusturm culling.

        // cube with size 2 centered on the origin. But since our cubes can poke out a bit if placed at the absolute edge of
        // our bounds we should add the step to this
        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + step));

        // Now that we are using the GPU surface shader we must set the position buffer and step data into the relavant ids.
        material.SetBuffer(positionsId, positionsBuffer);
		material.SetFloat(stepId, step);
        // finally we also provide how many instances should be drawn. This is resolution * resolution
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution * resolution);

    }
}
