using UnityEngine;

public class Fractal_Optimized : MonoBehaviour
{
    // Rather than have each part update itself we'll instead control the entire fractal from the single root object that has the Fractal component.
    // Unity has a much  better time managing a single updating game object. But we at a minimum need to keep track of the direction and rotation of each 
    // object. We will use a struct for this.
    struct FractalPart {
        // Tagging it as public here will make it avaiable to be accessed anywhere inside fractal but the struct is itself private to fractal.
        public Vector3 direction;
        public Quaternion rotation;
        // we aslo need its transform to correctly scale rotate and postition it.
        public Transform transform;
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


    void Awake() {
        // We define parts to have size equal to depth
        parts = new FractalPart[depth][];
        // each layer we define to hae a new array. The first layer only has 1 object so we declare it with an array of size 1.
        // parts[0] = new FractalPart[1];

        // We can instantiate all of it because we know that each layer is about 5x bigger than the previous layer. Since we instantiate 5 objects for each object each time
        // int length = 1;
        
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new FractalPart[length];
        }

        float scale = 1.0f;
        parts[0][0] = CreatePart(0, 0, scale);
        for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            // Createa a reference to the parts array at a given level
            FractalPart[] levelParts = parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
                for (int ci = 0; ci < 5; ci++) {
                    levelParts[fpi + ci] = CreatePart(li, ci, scale);
                }
            }
        }
        
    }


    FractalPart CreatePart(int levelIndex, int childIndex, float scale) {
        // we need the child index to figure out what the position and rotation needs to be.
        var go = new GameObject("Fractal Part " + levelIndex + " C" + childIndex);
        // since the scale at each levelIndx does nto change we can fix it
        go.transform.localScale = scale * Vector3.one;
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;

        // if the constructor method invocation has no parameters we can skip the empty parameter list
        return new FractalPart{
            direction = directions[childIndex],
            rotation = rotations[childIndex],
            transform = go.transform
        };
    }

    void Update() {
        // to animate we need the delta rotation for each frame.
        Quaternion deltaRotation = Quaternion.Euler(0f, 22.5f * Time.deltaTime, 0f);

        // rotate the root part first (it does nto move)
        FractalPart rootPart = parts[0][0];
        rootPart.rotation *= deltaRotation;
        // we also have to set the root parts transform to this new rotation value.
        // This will allow other child objects to inherit that rotation.
        rootPart.transform.localRotation = rootPart.rotation;

        // Struct is a value type. So changing a local variable of it doesnt change the original. We have to copy the modified struct back
        parts[0][0] = rootPart;

        for (int li = 1; li < parts.Length; li++) {
            // we need the parent transform to know what to rotate our objects relative to
            // we start from lvl 1 so this should be possible to do.
            FractalPart[] parentParts = parts[li - 1];
            FractalPart[] levelParts = parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                Transform parentTransform = parentParts[fpi / 5].transform;
                FractalPart part = levelParts[fpi];
                // rotate the part's rotation to the old rotation rotated by the deltaRotation quaternion.
                part.rotation *= deltaRotation;
                // Quaternion rotation works a bit differently the last in the multiplication is applied first
                // The childs rotation should be applied first then the paretnts.
                part.transform.localRotation = parentTransform.localRotation * part.rotation;
                // we have to scale the offset by 150% as we are scaling the distance by the parts own scale. 
                // We also need to rotate the transform offset by the parent's rotation since we are doing it from that frame of reference
                part.transform.localPosition = parentTransform.localPosition + parentTransform.localRotation * (1.5f * part.transform.localScale.x * part.direction);

                // copy the modified part back.
                levelParts[fpi] = part;
            }
        }
    }

}
