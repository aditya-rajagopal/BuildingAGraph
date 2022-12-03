using UnityEngine;

public class Fractal_Optimized_Procedural : MonoBehaviour
{

    // We begin by removing the game objects. This means we no longer have Transforms to store the world rotation and position
    // So we will store it explicitly.
    struct FractalPart {
        public Vector3 direction, worldPosition;
        public Quaternion rotation, worldRotation;
        // because of the issue with quaternions being constatnly updated with multiplicatiosn we will 
        // instead generate new quaternions each frame using a spinAngle
        public float spinAngle;
    }

    // now we creat an array of parts to store all the objects we need to manipulate.
    // we can go one step further and make it 2D instead of 1D to give it some structure.
    // This way we can put all objects in the same laeyer in 1 row.
    FractalPart[][] parts;

    [SerializeField, Range(1, 8)]
    int depth = 4;

    [SerializeField]
    Mesh mesh;

    [SerializeField]
    Material material;

    static Vector3[] directions = {
        Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
    };


    static Quaternion[] rotations = {
        Quaternion.identity,
        Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(0f, 0f, 90f),
		Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f)
    };

    // We need to store the 4x4 transformation matrices. TRS matrix
    Matrix4x4[][] matrices;

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
        parts = new FractalPart[depth][];
        matrices = new Matrix4x4[depth][];
        matricesBuffers = new ComputeBuffer[depth];
        // each layer we define to hae a new array. The first layer only has 1 object so we declare it with an array of size 1.
        // parts[0] = new FractalPart[1];

        // We can instantiate all of it because we know that each layer is about 5x bigger than the previous layer. Since we instantiate 5 objects for each object each time
        // int length = 1;
        int stride = 16 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new FractalPart[length];
            matrices[i] = new Matrix4x4[length];
            matricesBuffers[i] = new ComputeBuffer(length, stride);
        }

        parts[0][0] = CreatePart(0);
        for (int li = 1; li < parts.Length; li++) {
            // Createa a reference to the parts array at a given level
            FractalPart[] levelParts = parts[li];
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
        float spinAngleDelta = 22.5f * Time.deltaTime;

        float scale = 1f;
        // rotate the root part first (it does nto move)
        FractalPart rootPart = parts[0][0];
        // This is problematic V as these delta rotations stack up errors.
        // rootPart.rotation *= deltaRotation;
        rootPart.spinAngle += spinAngleDelta;
        // we also have to set the root parts transform to this new rotation value.
        // This will allow other child objects to inherit that rotation.
        // rootPart.worldRotation = rootPart.rotation;
        rootPart.worldRotation = rootPart.rotation * Quaternion.Euler(0f, rootPart.spinAngle, 0f);

        // Struct is a value type. So changing a local variable of it doesnt change the original. We have to copy the modified struct back
        parts[0][0] = rootPart;
        matrices[0][0] = Matrix4x4.TRS(
			rootPart.worldPosition, rootPart.worldRotation, Vector3.one
		);

        for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            // we need the parent transform to know what to rotate our objects relative to
            // we start from lvl 1 so this should be possible to do.
            FractalPart[] parentParts = parts[li - 1];
            FractalPart[] levelParts = parts[li];
            Matrix4x4[] levelMatrices = matrices[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                // Transform parentTransform = parentParts[fpi / 5].transform;
                FractalPart parent = parentParts[fpi / 5];
                FractalPart part = levelParts[fpi];
                // rotate the part's rotation to the old rotation rotated by the deltaRotation quaternion.
                // part.rotation *= deltaRotation;
                part.spinAngle += spinAngleDelta;
                // Quaternion rotation works a bit differently the last in the multiplication is applied first
                // The childs rotation should be applied first then the paretnts.
                // we first rotate the part rotation by this new quatrnion we generate with teh updated angle and then apply that rotation followed 
                // by the parent's rotation
                part.worldRotation = parent.worldRotation * ( part.rotation * Quaternion.Euler(0f, part.spinAngle, 0f) );
                // we have to scale the offset by 150% as we are scaling the distance by the parts own scale. 
                // We also need to rotate the transform offset by the parent's rotation since we are doing it from that frame of reference
                part.worldPosition = parent.worldPosition + parent.worldRotation * (1.5f * scale * part.direction);

                // copy the modified part back.
                levelParts[fpi] = part;
                // using quaternion eventually produces errors due to accumulation of floating point inacuracies. These are due to adding very small angles in the
                // quaternionDelta untill it is no longer recognized as a valid rotation.
                // Instead of updating the 
                levelMatrices[fpi] = Matrix4x4.TRS(
					part.worldPosition, part.worldRotation, scale * Vector3.one
				);
            }
        }

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
