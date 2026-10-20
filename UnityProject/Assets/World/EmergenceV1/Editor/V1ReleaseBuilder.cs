#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Release-only scene processing keeps authored project objects intact.
public sealed class V1ReleaseSceneCleanup : IProcessSceneWithReport
{
    public int callbackOrder => 100;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if(report==null)return;
        foreach(var root in scene.GetRootGameObjects())foreach(var world in root.GetComponentsInChildren<V1World>(true))
        {
            world.timeBackgrounds=Array.Empty<Sprite>();
            world.pollutedWaterTile=null;world.soilTile=null;
            world.slopePrefab=null;world.damPrefab=null;
        }
    }
}

public static class V1ReleaseBuilder
{
    public static readonly string[] Scenes={"Assets/Scenes/StartMenu.unity","Assets/Scenes/RiverbankEmergenceV1.unity"};
    [Serializable] private sealed class ReleaseReport
    {
        public string result,unityVersion,executable;
        public string[] scenes,packedAssets,files;
        public ulong totalBytes;
        public int errors,warnings;
        public double seconds;
    }
    public static void Build()
    {
        string output=GetArgument("-riverbankOutput");
        string audit=GetArgument("-riverbankAudit");
        if(string.IsNullOrEmpty(output)||string.IsNullOrEmpty(audit))throw new ArgumentException("Explicit release output and audit directories are required.");
        output=Path.GetFullPath(output);audit=Path.GetFullPath(audit);
        if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Release directory must be empty; preserve previous exports: "+output);
        Directory.CreateDirectory(output);Directory.CreateDirectory(audit);
        foreach(string scene in Scenes)if(!File.Exists(scene))throw new FileNotFoundException(scene);
        AssetDatabase.Refresh();
        EditorBuildSettings.scenes=Scenes.Select(s=>new EditorBuildSettingsScene(s,true)).ToArray();
        EditorUserBuildSettings.development=false;EditorUserBuildSettings.allowDebugging=false;
        EditorUserBuildSettings.connectProfiler=false;EditorUserBuildSettings.buildWithDeepProfilingSupport=false;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone,ManagedStrippingLevel.Low);
        PlayerSettings.stripEngineCode=true;
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        // Runtime fonts and textures remain referenced; editor data and test assets do not ship.
        string executable=Path.Combine(output,"Riverbank.exe");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=Scenes,locationPathName=executable,target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.CompressWithLz4HC|BuildOptions.DetailedBuildReport
        });
        var assets=report.packedAssets.SelectMany(p=>p.contents).Select(c=>c.sourceAssetPath).Where(p=>!string.IsNullOrEmpty(p)).Distinct().OrderBy(p=>p).ToArray();
        var summary=report.summary;
        File.WriteAllText(Path.Combine(audit,"build-report.json"),JsonUtility.ToJson(new ReleaseReport{
            result=summary.result.ToString(),unityVersion=Application.unityVersion,executable=executable,
            scenes=Scenes,packedAssets=assets,files=report.GetFiles().Select(f=>f.path).ToArray(),
            totalBytes=summary.totalSize,errors=(int)summary.totalErrors,warnings=(int)summary.totalWarnings,seconds=summary.totalTime.TotalSeconds
        },true));
        if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Release build failed: "+summary.result);
        if(assets.Any(p=>p.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)&&!Scenes.Contains(p)))throw new InvalidOperationException("An unexpected scene entered the release.");
        Debug.Log("Riverbank release succeeded: "+executable);
    }
    private static string GetArgument(string name)
    {
        string[] args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)if(args[i]==name)return args[i+1];return null;
    }
}
#endif
