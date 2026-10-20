#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class RiverbankStartMenuVerification
{
    private const string Key="Riverbank.StartMenu.";
    private static string RequestPath=>Path.Combine(Application.dataPath,"../StartMenuVerification/request.json");
    [Serializable] private class Request{public string outputDirectory="";}
    [Serializable] private class Report{public string status,error;public bool defaultAutumn,seasonCycle,backgrounds,honors,startGame,randomResolved;public int runtimeErrors;}
    static RiverbankStartMenuVerification()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetString(Key+"phase","")!=""&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);};
    }
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void Phase(string phase){SessionState.SetString(Key+"phase",phase);SessionState.SetFloat(Key+"time",(float)EditorApplication.timeSinceStartup);}
    private static double Elapsed()=>EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"time",0);
    private static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
    private static void Click(Button button){button.Focus();using(var e=NavigationSubmitEvent.GetPooled()){e.target=button;button.SendEvent(e);}}
    private static void Start()
    {
        RiverbankStartMenuBuilder.Open();EditorApplication.isPaused=false;Phase("ready");EditorApplication.isPlaying=true;
        var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Show();
    }
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath))return;
                var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));Directory.CreateDirectory(request.outputDirectory);
                SessionState.SetString(Key+"output",request.outputDirectory);File.Delete(RequestPath);SessionState.SetInt(Key+"errors",0);
                if(EditorApplication.isPlaying){Phase("exit");EditorApplication.isPlaying=false;return;}Start();return;
            }
            if(Elapsed()>90)throw new TimeoutException("Menu verification: "+phase);
            if(phase=="exit"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(phase=="return"){if(!EditorApplication.isPlayingOrWillChangePlaymode){RiverbankStartMenuBuilder.Open();Phase("finalMenu");EditorApplication.isPlaying=true;}return;}
            if(phase=="game")
            {
                var world=UnityEngine.Object.FindObjectOfType<V1World>();if(world==null||world.Simulation==null||Elapsed()<.5)return;
                Require(SceneManager.GetActiveScene().name=="RiverbankEmergenceV1"&&RiverbankMenuSession.StartedFromMenu,"Start loads the actual riverbank scene");
                Require((int)RiverbankMenuSession.InitialSeason>=0&&(int)RiverbankMenuSession.InitialSeason<4,"Random resolves to an actual season");
                Require(world.hud.clockLabel.text.Contains(RiverbankMenuSession.SeasonNames[(int)RiverbankMenuSession.InitialSeason]+"季"),"Chosen season is passed to the game readout");
                ScreenCapture.CaptureScreenshot(Output("game-start.png"));Phase("return");EditorApplication.isPlaying=false;return;
            }
            if(!EditorApplication.isPlaying)return;var menu=UnityEngine.Object.FindObjectOfType<RiverbankStartMenu>();if(menu==null)return;
            var root=menu.GetComponent<UIDocument>().rootVisualElement;
            if(phase=="ready"&&Elapsed()>.8)
            {
                Require(menu.Selection==RiverbankSeason.Autumn&&root.Q<Label>("Select").text=="选择季节：秋","Default season is autumn and is a label");
                Require(root.Q<Button>("Select")==null,"Season selector is not a button");
                Require(menu.menuFontAsset!=null&&root.Q<Label>("Select").resolvedStyle.unityFontDefinition.fontAsset==menu.menuFontAsset&&menu.menuFontAsset.atlasTextures[0]!=null,"Menu uses a persistent Chinese font asset and atlas");
                SessionState.SetInt(Key+"step",0);Phase("capture");return;
            }
            if(phase=="capture"&&Elapsed()>.45)
            {
                int step=SessionState.GetInt(Key+"step",0);int season=(step+2)%5;
                Require((int)menu.Selection==season,"Right arrow cycles autumn/winter/random/spring/summer");
                int background=season==4?2:season;
                Require(root.Q<VisualElement>("Background").style.backgroundImage.value.texture==menu.seasonalBackgrounds[background],"Season uses the supplied matching background; random uses autumn");
                var card=root.Q<VisualElement>("MenuCard").worldBound;
                Require(card.width>200&&card.height>200&&card.xMin>=0&&card.yMin>=0&&card.xMax<=root.worldBound.width+1&&card.yMax<=root.worldBound.height+1,"Menu stays inside the game viewport");
                ScreenCapture.CaptureScreenshot(Output("menu-"+menu.Selection.ToString().ToLowerInvariant()+".png"));Phase("captured");return;
            }
            if(phase=="captured"&&Elapsed()>.25)
            {
                int step=SessionState.GetInt(Key+"step",0);
                if(step<4){Click(root.Q<Button>("SeasonNext"));SessionState.SetInt(Key+"step",step+1);Phase("capture");return;}
                Click(root.Q<Button>("SeasonPrevious"));Require(menu.Selection==RiverbankSeason.Spring,"Left arrow goes to previous season");
                Click(root.Q<Button>("SeasonPrevious"));Require(menu.Selection==RiverbankSeason.Random,"Left arrow wraps from spring to random");
                Click(root.Q<Button>("Honor"));Require(!root.Q<VisualElement>("HonorOverlay").ClassListContains("hidden"),"Honor button opens the title panel");Phase("honor");return;
            }
            if(phase=="honor"&&Elapsed()>.45){ScreenCapture.CaptureScreenshot(Output("honors.png"));Phase("honorCaptured");return;}
            if(phase=="honorCaptured"&&Elapsed()>.25){Click(root.Q<Button>("HonorClose"));Require(root.Q<VisualElement>("HonorOverlay").ClassListContains("hidden"),"Title panel returns to the menu");Click(root.Q<Button>("Start"));Phase("game");return;}
            if(phase=="finalMenu"&&Elapsed()>.8)
            {
                Require(menu.Selection==RiverbankSeason.Autumn,"Re-entering Play restores autumn default");
                Require(SessionState.GetInt(Key+"errors",0)==0,"No menu runtime errors");
                ScreenCapture.CaptureScreenshot(Output("menu-final.png"));File.WriteAllText(Output("report.json"),JsonUtility.ToJson(new Report{status="passed",defaultAutumn=true,seasonCycle=true,backgrounds=true,honors=true,startGame=true,randomResolved=true,runtimeErrors=0},true));
                SessionState.SetString(Key+"phase","");var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Focus();Debug.Log("初始界面验收通过：季节选择、背景切换、称号、开始游戏；已恢复默认秋景。");
            }
        }
        catch(Exception e){string output=SessionState.GetString(Key+"output","");if(output!="")File.WriteAllText(Output("report.json"),JsonUtility.ToJson(new Report{status="failed",error=e.ToString(),runtimeErrors=SessionState.GetInt(Key+"errors",0)},true));SessionState.SetString(Key+"phase","");Debug.LogException(e);}
    }
}
#endif
