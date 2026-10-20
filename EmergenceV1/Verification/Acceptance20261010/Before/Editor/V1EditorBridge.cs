#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Verification is opt-in: an installer places a one-shot request outside Assets.
[InitializeOnLoad]
public static class V1EditorBridge
{
    private const string Key="RiverbankV1.Check.";
    private static string RequestPath=>Path.Combine(Application.dataPath,"../EmergenceV1Verification/request.json");
    [Serializable] private class Request{public string outputDirectory="";}
    [Serializable] private class Report
    {
        public string status,scenePath,checks,error;
        public int cellCount,regionCells,phases,day,finalPopulation,villageTier,farmBirths,wildMoves,treeBirths,forestTransitions,runtimeErrors;
        public float prosperity,movementDistance,riverCollisionX,bridgeCrossingX;
        public bool manualTime,threePhaseCycle,densityRule,playerTreeMature,teachingWin,hoverPopulation;
        public double maximumEnvironmentMilliseconds,profilerMaximumMilliseconds;
        public bool scheduledRain;
        public string profilerPath;
    }
    static V1EditorBridge()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetString(Key+"phase","")!=""&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);};
    }
    private static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
    private static void Phase(string phase){SessionState.SetString(Key+"phase",phase);SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+180);SessionState.SetFloat(Key+"time",Time.time);}
    private static float Elapsed()=>Time.time-SessionState.GetFloat(Key+"time",0);
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void Start()
    {
        SessionState.SetBool(Key+"profilerWasEnabled",UnityEditorInternal.ProfilerDriver.enabled);SessionState.SetBool(Key+"profileEditorWasEnabled",UnityEditorInternal.ProfilerDriver.profileEditor);SessionState.SetBool(Key+"cpuWasEnabled",UnityEditorInternal.ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU));
        UnityEditorInternal.ProfilerDriver.profileEditor=true;UnityEditorInternal.ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU,true);UnityEditorInternal.ProfilerDriver.enabled=true;
        V1SceneBuilder.Open();EditorApplication.isPaused=false;var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(type);game.Show();game.maximized=false;game.Focus();Phase("waitingPlay");EditorApplication.isPlaying=true;
    }
    private static void Click(V1World world)
    {
        var recorder=UnityEngine.Profiling.Recorder.Get("V1.Environment.AdvancePhase");
        if(recorder.isValid){SessionState.SetFloat(Key+"profilerMs",Mathf.Max(SessionState.GetFloat(Key+"profilerMs",0),recorder.elapsedNanoseconds/1000000f));recorder.enabled=true;}
        int before=world.Simulation.AdvanceCount;world.hud.advanceButton.onClick.Invoke();Require(world.Simulation.AdvanceCount==before+1,"One button click must advance exactly one phase");
        SessionState.SetFloat(Key+"phaseMs",Mathf.Max(SessionState.GetFloat(Key+"phaseMs",0),(float)world.LastEnvironmentMilliseconds));
    }
    private static void Put(V1World w,Vector3Int cell,V1Element element,float amount)
    {
        w.player.Teleport(w.ground.GetCellCenterWorld(cell)+Vector3.down*.7f);w.Select(element);w.SetAmount(amount);Require(w.Place(cell),"Legal near-character placement failed: "+cell+" / "+element);
    }
    private static void Support(V1World w)
    {
        var s=w.Simulation;
        var plot=new Vector3Int(7,6,0);var p=w.ReadCell(plot);
        if(p.Water<2)Put(w,plot,V1Element.Water,1);if(p.Earth<6)Put(w,plot,V1Element.Earth,4);
        var towns=new System.Collections.Generic.List<V1Village>(s.Villages);
        foreach(var v in towns)if(v.active)for(int dy=-2;dy<=3;dy++)for(int dx=-2;dx<=3;dx++)
        {
            int i=s.Index(v.cell%s.Width+dx,v.cell/s.Width+dy);if(!s.Valid(i)||s.IsBuilding(i)||s.Cells[i].River||s.Cells[i].Mine==V1Mine.Active)continue;
            var cell=w.Coordinate(i);if(s.Cells[i].Earth<6)Put(w,cell,V1Element.Earth,4);if(s.Cells[i].Water<1)Put(w,cell,V1Element.Water,1);
            if(!s.Cells[i].Tree&&s.TreeCells3x3(i)<4)Put(w,cell,V1Element.Wood,1);
        }
        w.player.Teleport(w.spawn);w.player.SetVerificationMovement(Vector2.zero);
    }
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath))return;var req=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));Directory.CreateDirectory(req.outputDirectory);SessionState.SetString(Key+"output",req.outputDirectory);File.Delete(RequestPath);SessionState.SetInt(Key+"errors",0);SessionState.SetFloat(Key+"phaseMs",0);SessionState.SetFloat(Key+"profilerMs",0);
                if(EditorApplication.isPlaying){Phase("exitPlay");EditorApplication.isPlaying=false;return;}Start();return;
            }
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))throw new TimeoutException("Verification timeout: "+phase);
            if(phase=="exitPlay"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(!EditorApplication.isPlaying)return;var w=UnityEngine.Object.FindObjectOfType<V1World>();if(w==null||w.Simulation==null)return;var s=w.Simulation;
            if(phase=="waitingPlay")
            {
                Time.timeScale=1;EditorApplication.isPaused=false;UnityEditorInternal.ProfilerDriver.profileEditor=true;UnityEditorInternal.ProfilerDriver.enabled=true;
                Require(s.Cells.Length==1120&&w.Origin==new Vector3Int(-4,-2,0),"40x28 authored Tilemap including negative origin");Require(s.ReadRegion(s.Villages[0].cell).cells==36&&s.NearRiver(s.Villages[0].cell),"Riverside village must read 36 cells");
                Require(w.FindHoveredVillage(w.Position(s.Villages[0].cell)+Vector3.up*.6f)==s.Villages[0]&&w.hud.VillageText(s.Villages[0]).Contains("8 人"),"Hover population readout");
                SessionState.SetFloat(Key+"water",s.Cells[0].Water);w.player.SetVerificationMovement(Vector2.zero);Phase("frozen");return;
            }
            if(phase=="frozen"&&Elapsed()>2)
            {
                Require(s.AdvanceCount==0&&s.Cells[0].Water==SessionState.GetFloat(Key+"water",0)&&s.Villages[0].population==8,"Wall-clock must not advance environment");
                ScreenCapture.CaptureScreenshot(Output("morning.png"));SessionState.SetFloat(Key+"startX",w.player.Position.x);w.player.SetVerificationMovement(Vector2.right);Phase("moving");return;
            }
            if(phase=="moving"&&Elapsed()>.35f)
            {
                float distance=w.player.Position.x-SessionState.GetFloat(Key+"startX",0);Require(distance>.5f&&s.AdvanceCount==0,"Character moves without advancing time");SessionState.SetFloat(Key+"movement",distance);
                w.player.Teleport(new Vector2(11.4f,13.5f));w.player.SetVerificationMovement(Vector2.right);Phase("river");return;
            }
            if(phase=="river"&&Elapsed()>.5f)
            {
                float x=w.player.Position.x;Require(x>11.6f&&x<11.87f,"River collision: "+x);SessionState.SetFloat(Key+"collision",x);w.player.Teleport(new Vector2(11.5f,4.5f));w.player.SetVerificationMovement(Vector2.right);Phase("bridge");return;
            }
            if(phase=="bridge"&&Elapsed()>1)
            {
                float x=w.player.Position.x;Require(x>14.2f,"Bridge crossing: "+x);SessionState.SetFloat(Key+"bridge",x);w.player.Teleport(w.spawn);w.player.SetVerificationMovement(Vector2.zero);Click(w);Require(s.Phase==1&&s.Day==1,"Morning to noon");Phase("noon");return;
            }
            if(phase=="noon"&&Elapsed()>.4f){Require(w.hud.timeImage.sprite==w.timeBackgrounds[1],"Noon background must switch");ScreenCapture.CaptureScreenshot(Output("noon.png"));Phase("noonCaptured");return;}
            if(phase=="noonCaptured"&&Elapsed()>.2f){Click(w);Require(s.Phase==2,"Noon to evening");Phase("evening");return;}
            if(phase=="evening"&&Elapsed()>.4f){Require(w.hud.timeImage.sprite==w.timeBackgrounds[2],"Evening background must switch");ScreenCapture.CaptureScreenshot(Output("evening.png"));Phase("eveningCaptured");return;}
            if(phase=="eveningCaptured"&&Elapsed()>.2f){Click(w);Require(s.Day==2&&s.Phase==0,"Evening to next morning");w.hud.toolsButton.onClick.Invoke();Require(w.ToolsOpen&&w.hud.toolsPage.activeSelf,"Independent tools page");SessionState.SetInt(Key+"step",s.AdvanceCount);Phase("tools");return;}
            if(phase=="tools"&&Elapsed()>.5f)
            {
                Require(!w.hud.advanceButton.interactable,"Advance unavailable while tools page is open");w.hud.advanceButton.onClick.Invoke();Require(s.AdvanceCount==SessionState.GetInt(Key+"step",0),"Tools page cannot accidentally advance");ScreenCapture.CaptureScreenshot(Output("tools.png"));Phase("toolsCaptured");return;
            }
            if(phase=="toolsCaptured"&&Elapsed()>.2f)
            {
                w.hud.elementButtons[1].onClick.Invoke();Require(!w.ToolsOpen&&w.Selected==V1Element.Wood,"Selecting seed tool closes page");w.ResetWorld();w.player.SetVerificationMovement(Vector2.zero);
                var demo=new Vector3Int(23,8,0);Require(w.Simulation.TreeCells3x3(w.Index(demo))==4,"Density demo has four tree cells");Put(w,demo,V1Element.Wood,1);Click(w);Require(!w.ReadCell(demo).Tree&&w.ReadCell(demo).Seeds==1,"Four tree cells prevent establishment");
                var plot=new Vector3Int(7,6,0);Put(w,plot,V1Element.Wood,1);Put(w,plot,V1Element.Water,1);Put(w,plot,V1Element.Earth,4);Click(w);Require(w.ReadCell(plot).Tree&&w.Simulation.PlayerTreeBirths>0,"Supported player seed must sprout");SessionState.SetInt(Key+"loops",0);Phase("grow");return;
            }
            if(phase=="grow")
            {
                Support(w);Click(w);int loops=SessionState.GetInt(Key+"loops",0)+1;SessionState.SetInt(Key+"loops",loops);
                if(!w.LevelWon||s.Villages[0].tier<3){Require(loops<40,"Full tutorial did not reach prosperity/large village: stage "+s.TutorialStage+", prosperity "+s.Prosperity);return;}
                Require(s.ForestTransitions>0&&s.FarmBirths>0&&s.WildlifeMoves>0&&s.Ledger.GoldMined>1&&s.Ledger.TimberHarvested>1,"Tree stages, animal ecology and real material production");
                var report=new Report{status="passed",scenePath=V1SceneBuilder.ScenePath,checks="Actual Unity Play: stationary manual time, real character movement and river/bridge collision, three backgrounds and next day, clickable Canvas tools and phase button, density rejects four occupied cells, legal player seed grows, actual mining/harvesting, farm breeding and wild migration, full six-stage teaching prosperity win, restart",cellCount=1120,regionCells=36,phases=s.AdvanceCount,day=s.Day,finalPopulation=s.TotalPopulation,villageTier=s.Villages[0].tier,farmBirths=s.FarmBirths,wildMoves=s.WildlifeMoves,treeBirths=s.TreeBirths,forestTransitions=s.ForestTransitions,prosperity=s.Prosperity,movementDistance=SessionState.GetFloat(Key+"movement",0),riverCollisionX=SessionState.GetFloat(Key+"collision",0),bridgeCrossingX=SessionState.GetFloat(Key+"bridge",0),manualTime=true,threePhaseCycle=true,densityRule=true,playerTreeMature=true,teachingWin=true,hoverPopulation=true};
                report.runtimeErrors=SessionState.GetInt(Key+"errors",0);report.maximumEnvironmentMilliseconds=SessionState.GetFloat(Key+"phaseMs",0);report.profilerMaximumMilliseconds=SessionState.GetFloat(Key+"profilerMs",0);
                SessionState.SetString(Key+"report",JsonUtility.ToJson(report,true));w.player.Teleport(w.spawn);w.Select(V1Element.Wood);w.SetAmount(1);Phase("won");return;
            }
            if(phase=="won"&&Elapsed()>.8f){ScreenCapture.CaptureScreenshot(Output("prosperous.png"));Phase("reset");return;}
            if(phase=="reset"&&File.Exists(Output("prosperous.png")))
            {
                w.hud.restartButton.onClick.Invoke();Require(w.Simulation.AdvanceCount==0&&w.Simulation.TotalPopulation==8,"Restart restores initial state");w.player.SetVerificationMovement(Vector2.zero);Phase("finish");return;
            }
            if(phase=="finish"&&Elapsed()>.7f)
            {
                w.ScheduleRain();Require(w.Simulation.AdvanceCount==0&&w.Simulation.Ledger.WaterInput==0,"Queuing rain must not advance time or add water");Click(w);Require(w.weather.IsRaining&&Math.Abs(w.Simulation.Ledger.WaterInput-115)<.01,"Scheduled 25-cell rain plus source input");Phase("rain");return;
            }
            if(phase=="rain"&&Elapsed()>.5f)
            {
                Require(w.weather.ParticleCount>0,"Scheduled rain must show actual particles");w.ResetWorld();Require(!w.weather.IsRaining&&w.Simulation.AdvanceCount==0,"Restart clears rain and pending environment time");w.player.SetVerificationMovement(Vector2.zero);Phase("finalCapture");return;
            }
            if(phase=="finalCapture"&&Elapsed()>.7f)
            {
                Require(SessionState.GetInt(Key+"errors",0)==0,"Runtime errors logged");ScreenCapture.CaptureScreenshot(Output("level-start.png"));var report=JsonUtility.FromJson<Report>(SessionState.GetString(Key+"report",""));report.scheduledRain=true;
                int last=UnityEditorInternal.ProfilerDriver.lastFrameIndex,first=Math.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex,last-250);
                for(int f=first;f<=last;f++)using(var frame=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0))if(frame.valid)
                    for(int n=0;n<frame.sampleCount;n++)if(frame.GetSampleName(n)=="V1.Environment.AdvancePhase")report.profilerMaximumMilliseconds=Math.Max(report.profilerMaximumMilliseconds,frame.GetSampleTimeMs(n));
                report.profilerPath=last>=0?Output("v1-profiler.data"):"";if(last>=0)UnityEditorInternal.ProfilerDriver.SaveProfile(report.profilerPath);File.WriteAllText(Output("report.json"),JsonUtility.ToJson(report,true));Phase("restoreEditor");return;
            }
            if(phase=="restoreEditor"&&Elapsed()>.3f){RestoreProfiler();var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(type);game.maximized=false;game.Show();game.Focus();SessionState.SetString(Key+"phase","");w.player.ReleaseVerificationMovement();Debug.Log("河岸繁荣 V1：真实Play验证通过，已恢复第1天早晨，等待点击渡过时段。");}
        }
        catch(Exception e){RestoreProfiler();string output=SessionState.GetString(Key+"output","");if(output!="")File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{status="failed",error=e.ToString()},true));SessionState.SetString(Key+"phase","");Debug.LogException(e);}
    }
    private static void RestoreProfiler(){UnityEditorInternal.ProfilerDriver.enabled=SessionState.GetBool(Key+"profilerWasEnabled",false);UnityEditorInternal.ProfilerDriver.profileEditor=SessionState.GetBool(Key+"profileEditorWasEnabled",false);UnityEditorInternal.ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU,SessionState.GetBool(Key+"cpuWasEnabled",true));}
}
#endif
