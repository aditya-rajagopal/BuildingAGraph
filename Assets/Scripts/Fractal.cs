using UnityEngine;

public class Fractal : MonoBehaviour
{
    [SerializeField, Range(1, 8)]
    int depth = 4;


    // Start is called before the first frame update
    void Start()
    {
        name = "Fractal " + depth;
        if (depth <= 1)
        {
            // since we are recursively generating instances of fractal and are setting the depth of the child 
            // 1 less than the parent. We can limit the generation of fractals to only depth number.
            return;
        }
        // we have to spawn clones. To pass a reference of Fractal instance (this object) we can pass 
        // the keyword this

        // if we do this in awake every time we instantiate a new Fractal sphere it would invoke the awake method of that
        // and endlessly generate spheres.
        // Start is only run right before the first update method not when the object is created.
        // New instances created now will get their update in teh next frame. And they will then spawn objects.
        // So objects would only spawn once per frame. 

        Fractal childA = CreateChild(Vector3.right, Quaternion.Euler(0f, 0f, -90f));
        Fractal childB = CreateChild(Vector3.up, Quaternion.Euler(0f, 0f, 0f));
        Fractal childC = CreateChild(Vector3.left, Quaternion.Euler(0f, 0f, 90f));
        Fractal childD = CreateChild(Vector3.forward, Quaternion.Euler(90f, 0f, 0f));
		Fractal childE = CreateChild(Vector3.back, Quaternion.Euler(-90f, 0f, 0f));

        childA.transform.SetParent(transform, false);
        childB.transform.SetParent(transform, false);
        childC.transform.SetParent(transform, false);
        childD.transform.SetParent(transform, false);
		childE.transform.SetParent(transform, false);

    }

    Fractal CreateChild(Vector3 direction, Quaternion rotation) {
        Fractal child = Instantiate(this);
        child.depth = depth - 1;
        child.transform.localPosition = 0.75f * direction;
        child.transform.localRotation = rotation;
        child.transform.localScale = 0.5f * Vector3.one;
        return child;
    }


    // Update is called once per frame
    void Update()
    {
        // This method is highly inefficient raching around 8fps with a depth of 8. This because unity has a very ahrd time with 
        // highly nested objects. To combat this we are going to replace our approach to Fractal_Optimized.
        transform.Rotate(0f, 22.5f * Time.deltaTime, 0f);
    }

}
