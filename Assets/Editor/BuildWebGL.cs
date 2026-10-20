using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandglass.EditorTools
{
    /// <summary>
    /// Builds the browser version to Builds/WebGL with Brotli compression and
    /// writes the vercel.json that serves the compressed files correctly.
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
            PlayerSettings.WebGL.decompressionFallback = false;
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
            File.WriteAllText(Path.Combine(OutputDir, "vercel.json"), VercelConfig);
            File.WriteAllText(Path.Combine(OutputDir, "build-done.txt"), System.DateTime.UtcNow.ToString("o"));
        }

        // Unity's Brotli files must go out with Content-Encoding: br and their
        // real type, or the loader refuses them.
        const string VercelConfig = @"{
  ""headers"": [
    { ""source"": ""/Build/(.*)\\.js\\.br"", ""headers"": [
      { ""key"": ""Content-Encoding"", ""value"": ""br"" },
      { ""key"": ""Content-Type"", ""value"": ""application/javascript"" } ] },
    { ""source"": ""/Build/(.*)\\.wasm\\.br"", ""headers"": [
      { ""key"": ""Content-Encoding"", ""value"": ""br"" },
      { ""key"": ""Content-Type"", ""value"": ""application/wasm"" } ] },
    { ""source"": ""/Build/(.*)\\.data\\.br"", ""headers"": [
      { ""key"": ""Content-Encoding"", ""value"": ""br"" },
      { ""key"": ""Content-Type"", ""value"": ""application/octet-stream"" } ] },
    { ""source"": ""/Build/(.*)"", ""headers"": [
      { ""key"": ""Cache-Control"", ""value"": ""public, max-age=3600"" } ] }
  ]
}
";
    }
}
