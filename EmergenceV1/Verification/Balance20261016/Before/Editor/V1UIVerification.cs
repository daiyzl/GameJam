#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class V1UIVerification
{
    private const string Key="Riverbank.UI3.";
    private static string RequestPath=>Path.Combine(Application.dataPath,"../GameUIVerification/request.json");
    [Serializable]private class Request{public string outputDirectory="";}
    [Serializable]private class Report{public string status,error;public bool coordinatePlacement,latchedFlags,shortTutorial,multipleHonors,hoverConditions,earnedOnlyMenu,endTime,customUI;public int runtimeErrors;}
    static V1UIVerification()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetString(Key+"phase","")!=""&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);};
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void Phase(string phase){SessionState.SetString(Key+"phase",phase);SessionState.SetFloat(Key+"time",(float)EditorApplication.timeSinceStartup);}
    private static double Elapsed()=>EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"time",0);
    private static string Owned()=>string.Join(",",V1Honors.All.Where(RiverbankMenuSession.HasHonor).Select(h=>h.Id));
    private static void Start()
    {
        RiverbankStartMenuBuilder.Open();V1SceneBuilder.Open();
        var game=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));game.Show();game.Focus();
        EditorApplication.isPaused=false;Phase("ready");EditorApplication.isPlaying=true;
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
            if(Elapsed()>120)throw new TimeoutException("UI verification: "+phase);
            if(phase=="exit"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(!EditorApplication.isPlaying)return;
            if(phase=="menu"||phase=="menuCapture")
            {
                var menu=UnityEngine.Object.FindObjectOfType<RiverbankStartMenu>();if(menu==null||Elapsed()<.8)return;
                var root=menu.GetComponent<UIDocument>().rootVisualElement;
                if(phase=="menu")
                {
                    menu.ShowHonors();Require(root.Q<ScrollView>("HonorList").contentContainer.childCount==V1Honors.All.Count(RiverbankMenuSession.HasHonor),"Menu displays earned titles only");
                    Require(Owned()==SessionState.GetString(Key+"owned",""),"Verification did not create fake title unlocks");
                    ScreenCapture.CaptureScreenshot(Output("menu-earned-honors.png"));Phase("menuCapture");return;
                }
                menu.CloseHonors();ScreenCapture.CaptureScreenshot(Output("menu-ready.png"));
                File.WriteAllText(Output("report.json"),JsonUtility.ToJson(new Report{status="passed",coordinatePlacement=true,latchedFlags=true,shortTutorial=true,multipleHonors=true,hoverConditions=true,earnedOnlyMenu=true,endTime=true,customUI=true,runtimeErrors=SessionState.GetInt(Key+"errors",0)},true));
                Require(SessionState.GetInt(Key+"errors",0)==0,"UI runtime errors");SessionState.SetString(Key+"phase","");return;
            }
            var w=UnityEngine.Object.FindObjectOfType<V1World>();if(w==null||w.Simulation==null)return;var h=w.hud;
            if(phase=="ready"&&Elapsed()>.7)
            {
                SessionState.SetString(Key+"owned",Owned());w.settings.seasonalEvents=false;w.settings.dryIgnition=false;w.player.SetVerificationMovement(Vector2.zero);
                Require(h.timeImage==null&&h.endButton.transform.localPosition.x<h.restartButton.transform.localPosition.x,"End button is before restart and no phase image exists");
                Require(!w.HasMoved&&!w.HasPlaced&&h.uiRevision==4,"New run starts with clear flags and custom UI");
                Require(!w.PlaceAtCoordinates("invalid","5")&&!w.HasPlaced,"Invalid coordinates do not count as intervention");
                h.toolsButton.onClick.Invoke();w.Select(V1Element.Earth);int i=w.Index(new Vector3Int(-3,5,0));float earth=w.Simulation.Cells[i].Earth;
                h.coordinateX.text="-3";h.coordinateY.text="5";h.coordinateButton.onClick.Invoke();
                Require(w.HasPlaced&&!w.HasMoved&&w.Simulation.AdvanceCount==0&&w.Simulation.Cells[i].Earth==earth+w.PlacementAmount,"Coordinate form supports negative remote cells without moving or advancing time");
                Phase("tools");return;
            }
            if(phase=="tools"&&Elapsed()>.6)
            {
                Require(w.HasPlaced&&!w.HasMoved,"Placement flag remains latched while idle");ScreenCapture.CaptureScreenshot(Output("coordinate-tools.png"));
                Phase("toolsCaptured");return;
            }
            if(phase=="toolsCaptured"&&Elapsed()>.3){w.ToggleTools();w.player.SetVerificationMovement(Vector2.right);Phase("moving");return;}
            if(phase=="moving"&&Elapsed()>.3){Require(w.HasMoved,"Actual walking latches movement flag");w.player.SetVerificationMovement(Vector2.zero);Phase("stopped");return;}
            if(phase=="stopped"&&Elapsed()>.4)
            {
                Require(w.HasMoved&&w.HasPlaced,"Stopping movement and placement never resets either flag");w.ResetWorld();w.player.SetVerificationMovement(Vector2.zero);
                Require(!w.HasMoved&&!w.HasPlaced,"Only restart clears the two flags");
                w.Select(V1Element.Wood);Require(w.PlaceAtCoordinates("7","6"),"Player seed placement");
                for(int p=0;p<6&&!w.Simulation.TutorialComplete;p++)
                {w.Select(V1Element.Water);w.SetAmount(1);w.PlaceAtCoordinates("7","6");w.Select(V1Element.Earth);w.SetAmount(4);w.PlaceAtCoordinates("7","6");w.AdvancePhase();}
                Require(w.Simulation.TutorialComplete&&w.Simulation.PlantStage(w.Index(new Vector3Int(7,6,0)))>=2,"A player-grown tree completes the short tutorial");Phase("tutorial");return;
            }
            if(phase=="tutorial"&&Elapsed()>.5)
            {
                Require(!h.tutorialPanel.activeSelf,"Completed tutorial panel is hidden");ScreenCapture.CaptureScreenshot(Output("tutorial-complete.png"));
                Phase("tutorialCaptured");return;
            }
            if(phase=="tutorialCaptured"&&Elapsed()>.3)
            {
                w.ResetWorld();w.AdvancePhase();Require(V1Honors.Evaluate(w.Simulation,w.HasPlaced,w.HasMoved).Count==0,"Actual early ending has no artificial title awards");
                h.endButton.onClick.Invoke();Require(w.Ended&&h.resultPage.activeSelf&&h.resultTime.text.Contains("1 个时段"),"End button opens the elapsed-time settlement");
                int step=w.Simulation.AdvanceCount;w.AdvancePhase();Require(w.Simulation.AdvanceCount==step&&!w.PlaceAtCoordinates("7","6"),"Ending blocks further input");Phase("ended");return;
            }
            if(phase=="ended"&&Elapsed()>.5)
            {
                ScreenCapture.CaptureScreenshot(Output("end-empty.png"));
                Phase("endCaptured");return;
            }
            if(phase=="endCaptured"&&Elapsed()>.3)
            {
                h.ShowResults(new List<V1Honor>(V1Honors.All));h.resultSummary.text="布局验证预览 · 不写入称号记录";
                var hover=new PointerEventData(EventSystem.current);ExecuteEvents.Execute(h.honorButtons[0].gameObject,hover,ExecuteEvents.pointerEnterHandler);
                Require(h.honorHint.text.Contains(V1Honors.All[0].Condition)&&h.honorButtons.All(b=>b.gameObject.activeSelf),"Multiple title cards and custom hover conditions are visible");Phase("honorPreview");return;
            }
            if(phase=="honorPreview"&&Elapsed()>.6)
            {
                ScreenCapture.CaptureScreenshot(Output("layout-preview-eight-titles.png"));Phase("previewCaptured");return;
            }
            if(phase=="previewCaptured"&&Elapsed()>.3)
            {
                Require(Owned()==SessionState.GetString(Key+"owned",""),"Preview does not persist unearned titles");
                h.ShowResults(V1Honors.Evaluate(w.Simulation,w.HasPlaced,w.HasMoved));h.resultMenuButton.onClick.Invoke();Phase("menu");return;
            }
        }
        catch(Exception e)
        {
            File.WriteAllText(Output("report.json"),JsonUtility.ToJson(new Report{status="failed",error=e.ToString(),runtimeErrors=SessionState.GetInt(Key+"errors",0)},true));SessionState.SetString(Key+"phase","");Debug.LogException(e);
        }
    }
}
#endif
