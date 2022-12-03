using UnityEngine;
using static UnityEngine.Mathf;
public static class FunctionLibrary
{
    // Delegates allow us to do python like functional paramaters
    public delegate Vector3 Function(float u, float v, float t);

    public enum FunctionName { Wave, MultiWave, Ripple, Sphere, Torus};

    static Function[] functions = {Wave, MultiWave, Ripple, Sphere, Torus};

    public static Function GetFunction (FunctionName name)
    {
        return functions[(int)name];
    }

    public static FunctionName GetRandomFunctionNameOtherThan (FunctionName name) {
		var choice = (FunctionName)Random.Range(1, functions.Length);
		return choice == name ? 0 : choice;
	}

    public static FunctionName GetNextFunctionName (FunctionName name)
    {
        return  ((int)name < functions.Length - 1)? name + 1: 0;
    }

    public static Vector3 Wave(float u, float v, float t)
    {
        float y = Sin(PI * (u + v + t));
        return new Vector3(u, y, v);
    }

    public static Vector3 MultiWave(float u, float v, float t)
    {
        float y = Abs(Sin(PI * (1f / 4f) * (u - v + t)));
        y += Cos(PI * (u + v + t));
        return new Vector3(u, y, v);
    }

    public static Vector3 Ripple (float u, float v, float t)
    {
        float d = Sqrt(u*u + v*v);
        float y = Sin( PI * ( 4f * d  - t)) / (1f + 10f * d);
        return new Vector3(u, y, v);
    }

    public static Vector3 Sphere (float u, float v, float t)
    {
        // float r = 0.5f + 0.5f * Sin(PI * t);
        float r = 0.9f + 0.1f * Sin(PI * (6f * u + 4f * v + t));
        float s = Cos(PI * 0.5f * v);
        float q = r * s;
        Vector3 p;
        p.x = q * Sin(PI  * u);
        p.y = r * Sin(PI * 0.5f * v);
        p.z = q * Cos(PI * u);
        return p;
    }

    public static Vector3 Torus (float u, float v, float t)
    {
        // float r = 0.9f + 0.1f * Sin(PI * (6f * u + 4f * v + t));
        // float r1 = 0.75f;
        // float r2 = 0.25f;
        float r1 = 0.7f + 0.1f * Sin(PI * (6f * u + 0.5f * t));
        float r2 = 0.15f + 0.05f * Sin(PI * (8f * u + 4f * v + 2f * t));
        float s = r1 + r2 * Cos(PI  * v);
        Vector3 p;
        p.x = s * Sin(PI  * u);
        p.y = r2 * Sin(PI * v);
        p.z = s * Cos(PI * u);
        return p;
    }

    public static Vector3 Morph (
        float u, float v, float t, Function from, Function to, float progress
    )
    {
        return Vector3.LerpUnclamped(from(u, v, t), to(u, v, t), SmoothStep(0f, 1f, progress));
    }
}
