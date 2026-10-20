using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandglass.EditorTools
{
    /// <summary>
    /// Builds the browser version to Builds/WebGL for GitHub Pages. Pages
    /// cannot set Content-Encoding, so the Brotli files carry Unity's
    /// decompression fallback and the loader unpacks them in the browser.
    /// Also callable from the command line:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Sandglass.EditorTools.BuildWebGL.Build
    /// </summary>
    public static class BuildWebGL
    {
        public const string OutputDir = "Builds/WebGL";

        [MenuItem("Sandglass/Build WebGL")]
        public static void Build()
        {
            PlayerSettings.WebGL.template = "PROJECT:Sandglass";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Arena.unity" },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[BuildWebGL] {s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalSize / (1024f * 1024f):0.0} MB, {s.totalErrors} errors");
            if (s.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            // Pages would otherwise run the files through Jekyll.
            File.WriteAllText(Path.Combine(OutputDir, ".nojekyll"), "");
            File.WriteAllText(Path.Combine(OutputDir, "build-done.txt"), System.DateTime.UtcNow.ToString("o"));
        }
    }
}
