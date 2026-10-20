#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class TutorialEditorBridge
{
    private const string Key="FiveElementsTutorialVerification.";
    private static string RequestPath => Path.Combine(Application.dataPath,"../TutorialVerification/request.json");
    [Serializable] private class Request {public string outputDirectory="";}
    [Serializable] private class Report
    {
        public string status,scenePath,checks,error,previewPath,rainPath,mapPath;
        public int runtimeErrors,cellCount,lessonsCompleted,rainParticles,rainPulses;
        public float movementDistance,collisionX,waterLedgerError,earthLedgerError,woodLedgerError;
    }
    static TutorialEditorBridge()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(message,stack,type)=>{
            if(SessionState.GetString(Key+"phase","")!=""&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);
        };
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Phase(string name){SessionState.SetString(Key+"phase",name);SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+180);}
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void ShowGame()
    {var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);window.Show();window.maximized=true;window.Focus();}
    private static void Start()
    {SessionState.SetString(Key+"rules",SimulationVerification.RunAll());TutorialSceneBuilder.Open();ShowGame();Phase("waitingPlay");EditorApplication.isPlaying=true;}
    private static void Approach(TutorialWorld world)
    {world.player.Teleport((Vector2)world.TargetPosition+Vector2.down*1.1f);}
    private static void Ledgers(TutorialWorld world)
    {Require(Mathf.Abs(world.Simulation.WaterLedgerError)<.05f&&Mathf.Abs(world.Simulation.EarthLedgerError)<.05f&&Mathf.Abs(world.Simulation.WoodLedgerError)<.05f,"Resource ledgers failed");}
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath))return;var req=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                Directory.CreateDirectory(req.outputDirectory);SessionState.SetString(Key+"output",req.outputDirectory);File.Delete(RequestPath);SessionState.SetInt(Key+"errors",0);
                if(EditorApplication.isPlaying){Phase("exitPlay");EditorApplication.isPlaying=false;return;}Start();return;
            }
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))throw new TimeoutException("Tutorial verification timed out: "+phase);
            if(phase=="exitPlay"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(!EditorApplication.isPlaying)return;
            var world=UnityEngine.Object.FindObjectOfType<TutorialWorld>();if(world==null||world.Simulation==null)return;
            var hud=UnityEngine.Object.FindObjectOfType<TutorialHUD>();
            if(phase=="waitingPlay")
            {
                Require(world.Simulation.Level.Initial.Length==748,"Expected 34x22 Tilemap cells");Require(world.Origin.x==-4&&world.Origin.y==-2,"Negative map origin missing");
                var negative=new Vector3Int(-3,-1,0);Require(world.CellAtWorld(world.ground.GetCellCenterWorld(negative))==negative,"WorldToCell negative coordinate conversion failed");
                Require(world.GetComponentsInChildren<ElementTile>().Length==0,"Tutorial must read Tilemap rather than per-cell components");Require(hud!=null&&hud.retryButton!=null,"Missing editable Canvas UI");
                SessionState.SetFloat(Key+"startX",world.player.Position.x);SessionState.SetFloat(Key+"time",Time.time);world.player.SetVerificationMovement(Vector2.right);Phase("moving");return;
            }
            if(phase=="moving"&&Time.time-SessionState.GetFloat(Key+"time",0)>.35f)
            {
                float distance=world.player.Position.x-SessionState.GetFloat(Key+"startX",0);Require(distance>.4f,"Character did not move through FixedUpdate");SessionState.SetFloat(Key+"distance",distance);
                world.player.Teleport(new Vector2(-2.4f,6.5f));world.player.SetVerificationMovement(Vector2.left);SessionState.SetFloat(Key+"time",Time.time);Phase("collision");return;
            }
            if(phase=="collision"&&Time.time-SessionState.GetFloat(Key+"time",0)>.5f)
            {
                float x=world.player.Position.x;Require(x> -2.85f&&x< -2.6f,"Tilemap collision failed: "+x);SessionState.SetFloat(Key+"collision",x);world.player.ReleaseVerificationMovement();
                Approach(world);world.SelectedElement=Element.Gold;Require(!world.Interact(),"Wrong teaching element was accepted");world.SelectedElement=Element.Water;Require(world.Interact(),"Growth interaction failed");
                Phase("growthAuto");return;
            }
            if(phase=="growthAuto"&&world.Stage==1)
            {
                var grown=world.ReadCell(world.stations[0]);Require(Mathf.Abs(grown.Wood-3)<.001f&&Mathf.Abs(grown.Earth-1)<.001f,"Growth 1:1:2 lesson failed");Ledgers(world);
                Approach(world);hud.retryButton.onClick.Invoke();Require(world.Stage==1&&world.ReadCell(world.stations[0]).Wood==3,"Retry discarded completed station");
                world.SelectedElement=Element.Gold;Require(world.Interact(),"Gold interaction failed");world.Paused=true;
                for(int i=0;i<80&&world.Stage==1;i++)world.Tick();Require(world.Stage==2,"Gold lesson did not advance");Require(!world.walking.HasTile(world.stations[1]),"Cutting did not remove dense wood collision");
                Approach(world);world.SelectedElement=Element.Water;Require(world.Interact(),"Extinguish interaction failed");world.Paused=true;world.Tick();Require(world.Stage==3&&world.ReadCell(world.stations[2]).Fire<.001f,"Extinguish lesson failed");
                Approach(world);world.SelectedElement=Element.Fire;Require(world.Interact(),"Burn interaction failed");world.Paused=true;
                for(int i=0;i<12&&world.Stage==3;i++)world.Tick();Require(world.Stage==4&&world.ReadCell(world.stations[3]).Earth>=1,"Burn to earth lesson failed");
                Require(world.CallRain(),"Rain interaction failed");world.Paused=true;SessionState.SetFloat(Key+"time",Time.time);Phase("rainParticles");return;
            }
            if(phase=="rainParticles"&&Time.time-SessionState.GetFloat(Key+"time",0)>.8f)
            {
                Require(world.weather.IsRaining&&world.weather.ParticleCount>0,"Rain particles did not emit");SessionState.SetInt(Key+"particles",world.weather.ParticleCount);
                ScreenCapture.CaptureScreenshot(Output("rain-preview.png"));Phase("rainCapture");return;
            }
            if(phase=="rainCapture")
            {
                if(!File.Exists(Output("rain-preview.png")))return;
                for(int i=0;i<10&&world.Stage==4;i++)world.Tick();Require(world.Stage==5,"Rain did not cause extinguishing followed by growth");Ledgers(world);SessionState.SetInt(Key+"rainPulses",world.RainPulses);
                world.player.Teleport(world.TargetPosition);Phase("returnVillage");return;
            }
            if(phase=="returnVillage")
            {
                if(!world.Completed)return;Require(SessionState.GetInt(Key+"errors",0)==0,"Runtime errors detected");SessionState.SetInt(Key+"completed",5);
                hud.restartButton.onClick.Invoke();Require(world.Stage==0&&!world.Completed,"Restart button failed");SessionState.SetFloat(Key+"time",Time.time);Phase("startPreview");return;
            }
            if(phase=="startPreview"&&Time.time-SessionState.GetFloat(Key+"time",0)>.6f)
            {ScreenCapture.CaptureScreenshot(Output("tutorial-preview.png"));Phase("startCapture");return;}
            if(phase=="startCapture")
            {
                if(!File.Exists(Output("tutorial-preview.png")))return;
                var follow=UnityEngine.Object.FindObjectOfType<TutorialFollowCamera>();follow.enabled=false;var camera=follow.GetComponent<Camera>();camera.orthographicSize=11.7f;camera.transform.position=new Vector3(13,9,-10);
                SessionState.SetFloat(Key+"time",Time.time);Phase("mapPreview");return;
            }
            if(phase=="mapPreview"&&Time.time-SessionState.GetFloat(Key+"time",0)>.15f)
            {ScreenCapture.CaptureScreenshot(Output("tutorial-map.png"));Phase("finish");return;}
            if(phase=="finish")
            {
                if(!File.Exists(Output("tutorial-map.png")))return;
                var follow=UnityEngine.Object.FindObjectOfType<TutorialFollowCamera>();follow.GetComponent<Camera>().orthographicSize=6.7f;follow.enabled=true;
                var report=new Report {status="passed",scenePath=TutorialSceneBuilder.ScenePath,checks=SessionState.GetString(Key+"rules","")+"; native Tilemap and negative cell coordinates; real movement; Tilemap physics; real scheduled growth; teaching interactions; retry preserving progress; gold clearing collider; extinguish; burn/ash; rain particles and regrowth; return-to-village win; Canvas restart",
                    cellCount=748,lessonsCompleted=SessionState.GetInt(Key+"completed",0),rainParticles=SessionState.GetInt(Key+"particles",0),rainPulses=SessionState.GetInt(Key+"rainPulses",0),runtimeErrors=SessionState.GetInt(Key+"errors",0),movementDistance=SessionState.GetFloat(Key+"distance",0),collisionX=SessionState.GetFloat(Key+"collision",0),
                    waterLedgerError=world.Simulation.WaterLedgerError,earthLedgerError=world.Simulation.EarthLedgerError,woodLedgerError=world.Simulation.WoodLedgerError,previewPath=Output("tutorial-preview.png"),rainPath=Output("rain-preview.png"),mapPath=Output("tutorial-map.png")};
                File.WriteAllText(Output("report.json"),JsonUtility.ToJson(report,true));SessionState.SetString(Key+"phase","");Debug.Log("村口试炼：五项教学、Tilemap 坐标、碰撞与天气验证通过，已恢复到可玩的关卡起点。");
            }
        }
        catch(Exception e)
        {
            string output=SessionState.GetString(Key+"output","");if(output!="")File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report {status="failed",error=e.ToString()},true));
            SessionState.SetString(Key+"phase","");Debug.LogException(e);
        }
    }
}
#endif
