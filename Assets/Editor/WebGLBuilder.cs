using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System;

public class WebGLBuilder
{
    // Hardcoded Itch.io configuration
    private const string ITCH_USERNAME = "Frolicks";
    private const string ITCH_GAME = "Dogship";
    private static readonly string BUTLER_PATH = @"C:\Tools\butler\butler.exe";

    [MenuItem("Build/Build WebGL and Upload to Itch.io")]
    public static void BuildAndUpload()
    {
        if (!ValidateButlerSetup())
            return;

        Build();
        ZipBuild();
        UploadToItch();
    }

    private static bool ValidateButlerSetup()
    {
        if (string.IsNullOrEmpty(ITCH_USERNAME) || string.IsNullOrEmpty(ITCH_GAME))
        {
            Debug.LogError("Please set ITCH_USERNAME and ITCH_GAME environment variables");
            return false;
        }

        if (!File.Exists(BUTLER_PATH))
        {
            Debug.LogError($"Butler not found at {BUTLER_PATH}. Please install Butler and set BUTLER_PATH environment variable");
            return false;
        }

        // Check if Butler is authenticated
        var startInfo = new ProcessStartInfo
        {
            FileName = BUTLER_PATH,
            Arguments = "login",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using (var process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"Butler authentication failed: {error}");
                    return false;
                }

                Debug.Log($"Butler authenticated as: {output.Trim()}");
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to check Butler authentication: {e.Message}");
            return false;
        }
    }

    [MenuItem("Build/Build WebGL")]
    public static void Build()
    {
        string path = "Build/WebGL";
        // Clean build directory if it exists
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            Debug.Log("Cleaned build directory: " + path);
        }
        // Get enabled scenes from Build Settings
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        BuildPipeline.BuildPlayer(
            scenes,
            path,
            BuildTarget.WebGL,
            BuildOptions.None
        );
        Debug.Log("WebGL build completed!");
    }

    private static void ZipBuild()
    {
        string buildPath = "Build/WebGL";
        string zipPath = "Build/WebGL.zip";

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(buildPath, zipPath);
        Debug.Log("Build zipped to: " + zipPath);
    }

    private static void UploadToItch()
    {
        string version = DateTime.Now.ToString("yyyy.MM.dd.HH.mm");
        string arguments = $"push \"Build/WebGL\" {ITCH_USERNAME}/{ITCH_GAME}:html --userversion {version}";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = BUTLER_PATH,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                    Debug.Log("Butler: " + output);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError("Butler Error: " + error);

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"Butler failed with exit code: {process.ExitCode}");
                    return;
                }
                Debug.Log("Upload completed successfully!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to upload: {e.Message}");
        }
    }
}
