using UnityEditor;
using UnityEngine;

public class WebGLBuilder
{
    [MenuItem("Build/Build WebGL")]
    public static void Build()
    {
        string path = "Build/WebGL";
        BuildPipeline.BuildPlayer(
            new[] { "Assets/Scenes/MainScene.unity" },
            path,
            BuildTarget.WebGL,
            BuildOptions.None
        );
        Debug.Log("WebGL build completed!");
    }
}
