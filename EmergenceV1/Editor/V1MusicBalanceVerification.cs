#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in integration verification. Does not award titles or save simulated changes.
[InitializeOnLoad]
public static class V1MusicBalanceVerification
{
    private const string Key="Riverbank.MusicBalance.";
    private static string RequestPath=>Path.Combine(Application.dataPath,"../MusicBalanceVerification/request.json");
    [Serializable] private class Request { public string outputDirectory=""; }
    [Serializable] private class Report
    {
        public string status,error;
        public int tracks,runtimeErrors;
        public bool gameplaySelection,eventReturn,menuSelection,randomAutumn,singlePlayer,playback,manualTime,rapidSelection,randomWorld;
    }
    private static readonly V1Season[] Seasons={V1Season.Spring,V1Season.Summer,V1Season.Autumn,V1Season.Winter,V1Season.Spring,V1Season.Winter,V1Season.Autumn,V1Season.Autumn};
    private static readonly V1PhaseEvent[] Events={V1PhaseEvent.None,V1PhaseEvent.None,V1PhaseEvent.None,V1PhaseEvent.None,V1PhaseEvent.Thunderstorm,V1PhaseEvent.Snow,V1PhaseEvent.Wind,V1PhaseEvent.None};
    static V1MusicBalanceVerification()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetString(Key+"phase","")!=""&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);};
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void Phase(string value){SessionState.SetString(Key+"phase",value);SessionState.SetFloat(Key+"time",(float)EditorApplication.timeSinceStartup);}
    private static double Elapsed()=>EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"time",0);
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void Start()
    {
        RiverbankStartMenuBuilder.Open();V1SceneBuilder.Open();
        var game=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));game.Show();game.Focus();
        SessionState.SetInt(Key+"step",0);Phase("ready");EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
    }
    private static void Next(V1World w,int step)
    {
        w.Simulation.Season=Seasons[step];Require(w.Simulation.QueueEvent(Events[step]),"Event allowed");w.AdvancePhase();
        SessionState.SetInt(Key+"count",w.Simulation.AdvanceCount);Phase("track");
    }
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath))return;var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));File.Delete(RequestPath);
                Directory.CreateDirectory(request.outputDirectory);SessionState.SetString(Key+"output",request.outputDirectory);SessionState.SetInt(Key+"errors",0);
                if(EditorApplication.isPlaying){Phase("exit");EditorApplication.isPlaying=false;return;}Start();return;
            }
            if(Elapsed()>120)throw new TimeoutException(phase);
            if(phase=="exit"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(!EditorApplication.isPlaying)return;
            if(phase=="menu"||phase=="menuTrack"||phase=="rapid")
            {
                var menu=UnityEngine.Object.FindObjectOfType<RiverbankStartMenu>();if(menu==null||Elapsed()<3.2)return;
                var player=V1MusicPlayer.Instance;
                Require(player!=null&&player.GetInstanceID()==SessionState.GetInt(Key+"player",0),"Scene transition retains one player");
                int expected=menu.Selection==RiverbankSeason.Random?2:(int)menu.Selection;
                Require(player.CurrentTrack==expected,"Menu season and random autumn music");
                if(phase=="rapid")
                {
                    Require(player.CurrentTrack==2&&UnityEngine.Object.FindObjectsOfType<AudioListener>().Length==1,"Rapid changes end on latest selection with one listener");
                    File.WriteAllText(Output("report.json"),JsonUtility.ToJson(new Report{status="passed",tracks=7,runtimeErrors=SessionState.GetInt(Key+"errors",0),gameplaySelection=true,eventReturn=true,menuSelection=true,randomAutumn=true,singlePlayer=true,playback=true,manualTime=true,rapidSelection=true,randomWorld=true},true));
                    Require(SessionState.GetInt(Key+"errors",0)==0,"Runtime errors");SessionState.SetString(Key+"phase","");return;
                }
                int step=SessionState.GetInt(Key+"menuStep",0);
                if(step<5){menu.NextSeason();SessionState.SetInt(Key+"menuStep",step+1);Phase("menuTrack");return;}
                for(int i=0;i<8;i++)menu.NextSeason();
                while(menu.Selection!=RiverbankSeason.Autumn)menu.NextSeason();
                Phase("rapid");return;
            }
            var w=UnityEngine.Object.FindObjectOfType<V1World>();if(w==null||w.Simulation==null||Elapsed()<1)return;
            if(phase=="ready")
            {
                w.settings.seasonalEvents=false;w.settings.dryIgnition=false;w.player.SetVerificationMovement(Vector2.zero);
                Require(w.settings.randomizeWorld&&w.settings.prosperityTarget==75&&w.settings.prosperityHoldPhases==9,"New scene balance and random map defaults");
                int seed=w.WorldSeed;var previous=(V1Cell[])w.Simulation.Cells.Clone();w.ResetWorld();
                Require(seed!=w.WorldSeed&&previous.Where((c,i)=>Math.Abs(c.Height-w.Simulation.Cells[i].Height)>.001f).Count()>100,"New runs produce a new terrain seed and physical map");
                w.settings.randomizeWorld=false;w.settings.terrainSeed=317;w.ResetWorld();previous=(V1Cell[])w.Simulation.Cells.Clone();w.ResetWorld();
                Require(previous.Where((c,i)=>c.Height!=w.Simulation.Cells[i].Height||c.Earth!=w.Simulation.Cells[i].Earth||c.Water!=w.Simulation.Cells[i].Water).Count()==0,"Fixed seed reproduces starting conditions");
                w.settings.randomizeWorld=true;w.ResetWorld();
                var music=V1MusicPlayer.Instance;Require(music!=null&&music.tracks.Length==7,"Seven music clips loaded");
                foreach(var clip in music.tracks)Require(clip!=null&&clip.length>30&&clip.channels==2,"Real stereo instrumental loop");
                Require(music.GetComponents<AudioSource>().Length==2&&UnityEngine.Object.FindObjectsOfType<AudioListener>().Length==1,"Two crossfade sources and one listener");
                SessionState.SetInt(Key+"player",music.GetInstanceID());Next(w,0);return;
            }
            if(phase=="track"&&Elapsed()>3.2)
            {
                int step=SessionState.GetInt(Key+"step",0);var music=V1MusicPlayer.Instance;
                int expected=V1MusicPlayer.TrackFor(Seasons[step],Events[step]);
                Require(music.CurrentTrack==expected,"Gameplay season/event selects track "+step);
                var source=music.GetComponents<AudioSource>().FirstOrDefault(s=>s.clip==music.CurrentClip&&s.isPlaying);
                Require(source!=null&&source.timeSamples>0&&source.volume>0,"Audio playback advances: "+expected);
                Require(w.Simulation.AdvanceCount==SessionState.GetInt(Key+"count",0),"Music never advances the ecology clock");
                if(step<Seasons.Length-1){SessionState.SetInt(Key+"step",step+1);Next(w,step+1);return;}
                w.ResetWorld();ScreenCapture.CaptureScreenshot(Output("game-music-prosperity.png"));Phase("captured");return;
            }
            if(phase=="captured"&&Elapsed()>1)
            {SceneManager.LoadSceneAsync("StartMenu");SessionState.SetInt(Key+"menuStep",0);Phase("menu");}
        }
        catch(Exception e)
        {
            string path=SessionState.GetString(Key+"output","");if(!string.IsNullOrEmpty(path))File.WriteAllText(Path.Combine(path,"report.json"),JsonUtility.ToJson(new Report{status="failed",error=e.ToString(),runtimeErrors=SessionState.GetInt(Key+"errors",0)},true));
            SessionState.SetString(Key+"phase","");Debug.LogException(e);
        }
    }
}
#endif
