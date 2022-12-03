using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

using static Unity.Mathematics.math;

// The compiler might complain we are trying to invoke menthods of float3x4 and quaternion as math has methods with the same name
// We can use the using keyword to indicate what we mean.
// We are not usiong a method on float3x4 so we can ignore trying to resolve conflicts here
// using float3x4 = Unity.Mathematics.float3x4;
using quaternion = Unity.Mathematics.quaternion;

public class Fractal_Optimized_Burst : MonoBehaviour
{

    // To define a job we have to create a struct tyupe that implements a job interface.
    // This is like extending a class instead of inheriting existing functionalities you incluide specific functionality yourself.
    // We will use IJobFor which is very flexible

    // All the jobs we scheduled did not really improve performance too much. That is because the jobs could be scheduled on the main thread
    // since it has nothing to do while waiting. And the other jobs get scheduled on othe threas which are waiting for the main threads due to dependencies.
    // This is okay in situations where we schedule jobs and do other stuff on the main thread while we wait and can call completeion in a lateUpdate function or even 
    // The next frame. 
    // But this is all because we are not using the burst compiler. We have to explicitly instruct unity to compile our job struct with burst with [BurstCompile]
    // Now the code runs a bit faster due to burst optimizations but there isnt much to gain yet.
    // This is because burst compilation is on demand in the editor, just like shader compilation. When a job is first run it will be compiled by burst while at the same time
    // a regular c# compiled version is used to run the job. Once bnurst compilation is finished the enditor will switch to running the burst version. We can enforce waiting till the
    // burst version is complete by setting CompileSynchronously = true property.
    // We can further improve perforamnce by setting FloatMode.Fast. This causes operations like a + b*c to be reordered to b*c + a as there are faster madd(multiply - add) operations.
    // Its always good to turn it on. 
    // The second one is FloatPrecision.Standard which allows for lower precision calculation of sin and cos in the quarternion.
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    struct UpdateFractalLevelJob : IJobFor {
        public float spinAngleDelta;
		public float scale;


        // If we have different parallel jobs accessing the same part of the memory and both write the one that wrote last will win.
        // if one writes and the other reads it might eitehr get the new or the old one. This all depends on teh exact timing. 
        // We have no control over that. Hence it is better to mark things as read only or write only and avoid Race conditions.
        // The compiler will force you to not write to readonly fields and not read from write only fields.
        [ReadOnly]
		public NativeArray<FractalPart> parents;
		public NativeArray<FractalPart> parts;

        [WriteOnly]
		public NativeArray<float3x4> matrices;

        // The idea is that the execute function will replace the innermost part of our forloop.
        // To do this we need all the variables that a required must be added above.
        public void Execute(int i) {
            // i is the for loop index
            // The rest of this code is the same as the for loop that the Fractal_Optimized_Procedural class had.
            FractalPart parent = parents[i / 5];
			FractalPart part = parts[i];
			part.spinAngle += spinAngleDelta;
			part.worldRotation = mul(
                parent.worldRotation,
                mul(part.rotation, quaternion.RotateY(part.spinAngle))
            );
			part.worldPosition =
				parent.worldPosition +
				mul(parent.worldRotation, (1.5f * scale * part.direction));

			parts[i] = part;

			// matrices[i] = float4x4.TRS(
			// 	part.worldPosition, part.worldRotation, float3(scale)
			// );
            // Since we are optimizing the 4x4 matrix to a 3x3 we need to calculate the TRS ourselves.
            // first get a 3x3 rotaiton matrix the factor it by teh scale
            float3x3 r = float3x3(part.worldRotation) * scale;
            // we thjen form a 3x4 matrix with the 3 columns of the rotaitonscale matrix and putting th eposition in the last column.
			matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }

    }

    // We begin by removing the game objects. This means we no longer have Transforms to store the world rotation and position
    // So we will store it explicitly.
    struct FractalPart {
        public float3 direction, worldPosition;
        public quaternion rotation, worldRotation;
        // because of the issue with quaternions being constatnly updated with multiplicatiosn we will 
        // instead generate new quaternions each frame using a spinAngle
        public float spinAngle;
    }

    // now we creat an array of parts to store all the objects we need to manipulate.
    // we can go one step further and make it 2D instead of 1D to give it some structure.
    // This way we can put all objects in the same laeyer in 1 row.

    // To work with Jobs only simple values and structs are allowed. We can use arrays  but they have 
    // to be native arrays. This is a struct that contains a pointer to native machine memory. This
    // sidesteps the default memory management.
    NativeArray<FractalPart>[] parts;

    // Another optimization we can make for sending data to the GPU is to limit our transformation matrix to a 3x4
    // the bottom row is always going to be a [0, 0, 0, 1]. Since it is the same we can discard it reducing 
    // The amount of data we transfer to the GPU by 25%.
	NativeArray<float3x4>[] matrices;

    [SerializeField, Range(1, 8)]
    int depth = 4;

    [SerializeField]
    Mesh mesh;

    [SerializeField]
    Material material;

    static float3[] directions = {
        up(), right(), left(), forward(), back()
    };


    static quaternion[] rotations = {
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
		quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };


    // In the graph example we had teh GPU fill the buffer for its own use.
    // Here we will have the CPU fill in the buffer. We will use a seperate buffer per level.
    // 4x4 = 16 * 4byes = size of strides
    ComputeBuffer[] matricesBuffers;

    // we will create a reference to the matrix buffer in the shader
    static readonly int matricesID = Shader.PropertyToID("_Matrices");

    static MaterialPropertyBlock propertyBlock;


    // with a compute buffere we should have an onenable and disable function to make sure we cleanup the buffers
    // we can change awake to onEnable
    // void Awake() {
    void OnEnable() {
        // We define parts to have size equal to depth
        parts = new NativeArray<FractalPart>[depth];
		matrices = new NativeArray<float3x4>[depth];
        matricesBuffers = new ComputeBuffer[depth];
        // each layer we define to hae a new array. The first layer only has 1 object so we declare it with an array of size 1.
        // parts[0] = new FractalPart[1];

        // We can instantiate all of it because we know that each layer is about 5x bigger than the previous layer. Since we instantiate 5 objects for each object each time
        // int length = 1;
        // We updated our matrix so the new stride is 12x4 for teh compute buffer
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            // We can create a new native array of size lenght. The second argument indicates how long the native array shoudlexist. We will keep using the same 
            // Array every frame so we will use Allocator.Persistent
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffers[i] = new ComputeBuffer(length, stride);
        }

        parts[0][0] = CreatePart(0);
        for (int li = 1; li < parts.Length; li++) {
            // Createa a reference to the parts array at a given level
            NativeArray<FractalPart> levelParts = parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
                for (int ci = 0; ci < 5; ci++) {
                    levelParts[fpi + ci] = CreatePart(ci);
                }
            }
        }

        // if (propertyBlock == null) {
		// 	propertyBlock = new MaterialPropertyBlock();
		// }

        // We can also assign to something only if it is null using the following
        propertyBlock ??= new MaterialPropertyBlock();
        
    }

    void OnDisable() {
        for (int i = 0; i < matricesBuffers.Length; i++) {
            matricesBuffers[i].Release();
            parts[i].Dispose();
            matrices[i].Dispose();
        }
        // safer to dereference them as well
        parts = null;
		matrices = null;
		matricesBuffers = null;
    }

    void OnValidate () {
        // we can use the onvalidate function to support changing the depth live.
        // we check if the parts array is not null because if it is we are free to change the value.
        // but if we change the value (i.e on validate is called) then we will disable the current arrays
        // and reenable them with the updated depth.
        // However onvalidate is also called when the component is disabled. This means the fractal would be disabled and enabled anyway
        // to stop this we put another condition to check if it is enabled.
        if (parts != null & enabled)
        {
            OnDisable();
            OnEnable();
        }
	}


    // we dont need the rest of the code since there is no transform. We can also use => since it is only doing 1 thing
    FractalPart CreatePart(int childIndex) => new FractalPart{
            direction = directions[childIndex],
            rotation = rotations[childIndex]
        };

    void Update() {
        // to animate we need the delta rotation for each frame.
        // Quaternion deltaRotation = Quaternion.Euler(0f, 22.5f * Time.deltaTime, 0f);
        // float spinAngleDelta = 22.5f * Time.deltaTime;
        // using the mathematics package we conmvert everything to radians
        float spinAngleDelta = 0.125f * PI * Time.deltaTime;

        float scale = 1f;
        // rotate the root part first (it does nto move)
        FractalPart rootPart = parts[0][0];
        // This is problematic V as these delta rotations stack up errors.
        // rootPart.rotation *= deltaRotation;
        rootPart.spinAngle += spinAngleDelta;
        // we also have to set the root parts transform to this new rotation value.
        // This will allow other child objects to inherit that rotation.
        // rootPart.worldRotation = rootPart.rotation;
        rootPart.worldRotation = mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle));

        // Struct is a value type. So changing a local variable of it doesnt change the original. We have to copy the modified struct back
        parts[0][0] = rootPart;
        // matrices[0][0] = float3x4.TRS(
		// 	rootPart.worldPosition, rootPart.worldRotation, float3(scale)
		// );

        float3x3 r = float3x3(rootPart.worldRotation) * scale;
        matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);

        JobHandle jobHandle = default;
        for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            // We create a new instance of the fractal level job. We pass all the relavant variables and references to the readonly and writeonly NativeArrays.
            jobHandle = new UpdateFractalLevelJob{
                spinAngleDelta = spinAngleDelta,
                scale = scale,
                parents = parts[li - 1],
                parts = parts[li],
                matrices = matrices[li]
            // We can further improve our speed by using ScheduleParallel to use multiple CPU cores.
            // This adds an additional parameter to sayu how many batches should we use at a time.
            // }.Schedule(parts[li].Length, jobHandle);
            }.ScheduleParallel(parts[li].Length, 8, jobHandle);
            // One way we can implement the job execution is with a forloop
            // for (int fpi = 0; fpi < parts[li].Length; fpi++) {
			// 	job.Execute(fpi);
			// }
            // But we dont ahve to explicitly invoke the Execute fucntion. We can schedule the joibn and let it perform the loop on its own.
            // It has 2 parameters, the first is how many iterations are there and teh second is a JobHandle struct value, which is used to enforece a sequential dependency  between jobs.
            // default keyword sets it to have no constraints
            // schedule returns a jobHandel value which we can use to track the jobs progress. We can call teh complete functiuon on this handel to stop further exectution untill this job
            // is finished.
            // However this still updates our fractal like before in a sequential manner. We can relax this constraint by delaying completion untill all the jobs have been scheduled
            // job.Schedule(parts[li].Length, default).Complete();
            // jobHandle = job.Schedule(parts[li].Length, jobHandle);
            // infact we dont even have to store the job we can just schedule as we define it above
        }

        // We waitfor completion after all the jobs have been scheduled
        jobHandle.Complete();
        

        // After calculating the TRS for each object we iterate through the objects and set their transformations in the compute buffer
        // It is not always ideal to send data from teh CPU to the GPU but in this case we have no choice and this is the most efficient way to do it

        // if we just set the buffers in the loop below the draw commands are queued up but they all use the incorrect buffer from the last level
        // The solution is to link each buffer to a specfic draw command. We can do this with MaterialPropertyBlock.

        // bounds of our drawing area
        var bounds = new Bounds(Vector3.zero, 3f * Vector3.one);
        for (int i=0; i < matricesBuffers.Length; i++)
        {
            ComputeBuffer buffer = matricesBuffers[i];
            buffer.SetData(matrices[i]);
            propertyBlock.SetBuffer(matricesID, buffer);
            // Instead of attaching the buffer to the material we pass a propertyblock as well to make sure that unity copies the configuration
            // of the block at that time and use it for that specific draw command overruling what was set for the material.
            // material.SetBuffer(matricesID, buffer);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
        }
    }

}
