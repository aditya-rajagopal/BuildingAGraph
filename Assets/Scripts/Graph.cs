using UnityEngine;
using static FunctionLibrary;

public class Graph : MonoBehaviour
{
    [SerializeField]
    Transform pointPrefab;
    [SerializeField]
    int resolution = 10;
    [SerializeField]
    FunctionName function;
    [SerializeField, Min(0f)]
    float functionDuration = 1f, transitionDuration = 1f;

    Transform[] points;

    float duration;

	bool transitioning;

	FunctionLibrary.FunctionName transitionFunction;

    public enum TransitionMode { Cycle, Random }

	[SerializeField]
	TransitionMode transitionMode;

    void Awake()
    {
        duration = 0f;
        points = new Transform[resolution * resolution];
        float step = 2.0f / resolution;
        Vector3 scale = Vector3.one * step;
        for (int i = 0; i < points.Length; i++)
        {
            Transform point = Instantiate(pointPrefab);
            // We will set this in the update instead
            // position.y =  position.x * position.x * position.x;
            point.localScale = scale;
            point.SetParent(transform, false);
            points[i] = point; 
        }
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
            PickNextFunction();
		}

        if (transitioning) {
			UpdateFunctionTransition();
		}
		else {
			UpdateFunction();
		}  
    }

    void PickNextFunction () {
		function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
	}

    // Update is called once per frame
    void UpdateFunction()
    {   
        Function f = GetFunction(function);
        float time = Time.time;
        float step = 2.0f / resolution;
        float u, v = 0.5f * step - 1f;
        Vector3 scale = Vector3.one * step;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
        {
            if ( x == resolution)
            {
                x = 0;
                z += 1;
                v = (z + 0.5f) * step - 1f;
            }
            u = (x + 0.5f) * step - 1f;
            points[i].localPosition = f(u, v, time);
        }
    }

    void UpdateFunctionTransition()
    {   
        FunctionLibrary.Function
			from = FunctionLibrary.GetFunction(transitionFunction),
			to = FunctionLibrary.GetFunction(function);
		float progress = duration / transitionDuration;
        float time = Time.time;
        float step = 2.0f / resolution;
        float u, v = 0.5f * step - 1f;
        Vector3 scale = Vector3.one * step;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
        {
            if ( x == resolution)
            {
                x = 0;
                z += 1;
                v = (z + 0.5f) * step - 1f;
            }
            u = (x + 0.5f) * step - 1f;
            points[i].localPosition = FunctionLibrary.Morph(
				u, v, time, from, to, progress
			);
        }
    }
}
