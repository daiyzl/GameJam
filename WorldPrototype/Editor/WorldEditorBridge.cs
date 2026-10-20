#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WorldEditorBridge
{
    private const string Key="FiveElementsWorldCheck.";
    private static string RequestPath => Path.Combine(Application.dataPath,"../WorldVerification/request.json");
    [Serializable] private class Request { public string outputDirectory=""; }
    [Serializable] private class Report
    {
        public string status, scenePath, checks, screenshotPath, error;
        public int tileCount, stepCount, runtimeErrors;
        public float movementDistance, collisionX, waterLedgerError, earthLedgerError, woodLedgerError;
    }
    static WorldEditorBridge()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(message,stack,type)=> {
            if(SessionState.GetString(Key+"phase","")!="" && (type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
                SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);
        };
    }
    private static void Require(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
    private static void Phase(string phase,float seconds=90)
    { SessionState.SetString(Key+"phase",phase); SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+seconds); }
    private static void ShowGame()
    {
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var window=EditorWindow.GetWindow(type); window.Show(); window.maximized=true; window.Focus();
    }
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath)) return;
                var req=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                Directory.CreateDirectory(req.outputDirectory); SessionState.SetString(Key+"output",req.outputDirectory); File.Delete(RequestPath);
                SessionState.SetInt(Key+"errors",0);
                if(EditorApplication.isPlaying) { Phase("exitPlay"); EditorApplication.isPlaying=false; return; }
                Start(); return;
            }
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0)) throw new TimeoutException("World verification timed out: "+phase);
            if(phase=="exitPlay") { if(!EditorApplication.isPlayingOrWillChangePlaymode) Start(); return; }
            if(!EditorApplication.isPlaying) return;
            var world=UnityEngine.Object.FindObjectOfType<ElementWorld>();
            if(world==null || world.Simulation==null) return;
            var player=world.player;
            if(phase=="waitingPlay")
            {
                Require(world.GetComponentsInChildren<ElementTile>().Length==432,"Expected 432 authored terrain cells");
                Require(player!=null&&player.GetComponent<Rigidbody2D>()!=null,"Missing physical character");
                Require(world.Simulation.Level.GoalIndex<0,"World must have no puzzle goal");
                world.simulateAutomatically=true;
                SessionState.SetFloat(Key+"startTime",Time.time); SessionState.SetInt(Key+"startStep",world.Simulation.StepCount);
                SessionState.SetFloat(Key+"startX",player.Position.x); player.SetVerificationMovement(Vector2.right); Phase("moving"); return;
            }
            if(phase=="moving" && Time.time-SessionState.GetFloat(Key+"startTime",0)>.4f)
            {
                float distance=player.Position.x-SessionState.GetFloat(Key+"startX",0); Require(distance>.3f,"FixedUpdate movement did not move character");
                SessionState.SetFloat(Key+"distance",distance); player.Teleport(new Vector2(1,4)); player.SetVerificationMovement(Vector2.left);
                SessionState.SetFloat(Key+"startTime",Time.time); Phase("collision"); return;
            }
            if(phase=="collision" && Time.time-SessionState.GetFloat(Key+"startTime",0)>.55f)
            {
                Require(player.Position.x>.5f&&player.Position.x<.9f,"Rock boundary collision failed: "+player.Position.x);
                SessionState.SetFloat(Key+"collisionX",player.Position.x); player.ReleaseVerificationMovement(); player.Teleport(world.SpawnPosition);
                Phase("simulation"); return;
            }
            if(phase=="simulation" && world.Simulation.StepCount>=SessionState.GetInt(Key+"startStep",0)+2)
            {
                world.simulateAutomatically=false;
                int i=world.Simulation.Level.Index(12,7); float previous=world.Simulation.GetCell(i).Water;
                Require(world.Simulation.AddResource(i,Element.Water,2),"Resource injection failed");
                Require(Math.Abs(world.Simulation.GetCell(i).Water-previous-2)<.001f,"Resource injection amount wrong");
                world.StepOnce();
                Require(Math.Abs(world.Simulation.WaterLedgerError)<.01f&&Math.Abs(world.Simulation.EarthLedgerError)<.01f&&Math.Abs(world.Simulation.WoodLedgerError)<.01f,"World resource ledger failed");
                Require(SessionState.GetInt(Key+"errors",0)==0,"Runtime errors detected");
                SessionState.SetInt(Key+"verifiedSteps",world.Simulation.StepCount);
                world.ResetWorld(); world.simulateAutomatically=false;
                SessionState.SetFloat(Key+"captureTime",Time.time); Phase("capture"); return;
            }
            if(phase=="capture" && Time.time-SessionState.GetFloat(Key+"captureTime",0)>.5f)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(SessionState.GetString(Key+"output",""),"world-preview.png")); Phase("finish"); return;
            }
            if(phase=="finish")
            {
                string output=SessionState.GetString(Key+"output",""); string screenshot=Path.Combine(output,"world-preview.png");
                if(!File.Exists(screenshot)) return;
                var report=new Report {status="passed",scenePath=WorldSceneBuilder.ScenePath,checks=SessionState.GetString(Key+"checks","")+"; real FixedUpdate movement; physical rock collision; automatic world ticks; injection and ledgers; reset",screenshotPath=screenshot,
                    tileCount=432,stepCount=SessionState.GetInt(Key+"verifiedSteps",0),runtimeErrors=SessionState.GetInt(Key+"errors",0),movementDistance=SessionState.GetFloat(Key+"distance",0),collisionX=SessionState.GetFloat(Key+"collisionX",0),
                    waterLedgerError=world.Simulation.WaterLedgerError,earthLedgerError=world.Simulation.EarthLedgerError,woodLedgerError=world.Simulation.WoodLedgerError};
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                world.simulateAutomatically=true; SessionState.SetString(Key+"phase",""); Debug.Log("五行小世界：场景、移动、碰撞和环境验证通过。世界已在 Play 模式运行。");
            }
        }
        catch(Exception e)
        {
            string output=SessionState.GetString(Key+"output","");
            if(output!="") File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report {status="failed",error=e.ToString()},true));
            SessionState.SetString(Key+"phase",""); Debug.LogException(e);
        }
    }
    private static void Start()
    {
        SessionState.SetString(Key+"checks",SimulationVerification.RunAll());
        WorldSceneBuilder.Open(); ShowGame(); Phase("waitingPlay"); EditorApplication.isPlaying=true;
    }
}
#endif
