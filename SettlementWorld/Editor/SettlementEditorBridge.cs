#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SettlementEditorBridge
{
    private const string Key="FiveElementsSettlementVerification.";
    private static string RequestPath=>Path.Combine(Application.dataPath,"../SettlementVerification/request.json");
    [Serializable] private class Request{public string outputDirectory="";}
    [Serializable] private class Report
    {
        public string status,scenePath,checks,error,previewPath,inventoryPath;
        public int cellCount,regionCells,initialPopulation,finalPopulation,villageTier,animals,farmBirths,wildMoves,newVillages,runtimeErrors;
        public float prosperity,movementDistance,riverCollisionX,bridgeCrossingX;
        public bool prosperityWin,hoverPopulation,inventoryPauses;
    }
    static SettlementEditorBridge()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(message,stack,type)=>{if(SessionState.GetString(Key+"phase","")!=""&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);};
    }
    private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    private static void Phase(string phase){SessionState.SetString(Key+"phase",phase);SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+180);}
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void Start()
    {
        SettlementSceneBuilder.Open();var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(type);game.Show();game.maximized=true;game.Focus();Phase("waitingPlay");EditorApplication.isPlaying=true;
    }
    private static void TimeStart(){SessionState.SetFloat(Key+"time",Time.time);}
    private static float Elapsed()=>Time.time-SessionState.GetFloat(Key+"time",0);
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath))return;var request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));Directory.CreateDirectory(request.outputDirectory);SessionState.SetString(Key+"output",request.outputDirectory);File.Delete(RequestPath);SessionState.SetInt(Key+"errors",0);
                if(EditorApplication.isPlaying){Phase("exitPlay");EditorApplication.isPlaying=false;return;}Start();return;
            }
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))throw new TimeoutException("Settlement verification timed out: "+phase);
            if(phase=="exitPlay"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(!EditorApplication.isPlaying)return;var world=UnityEngine.Object.FindObjectOfType<SettlementWorld>();if(world==null||world.Simulation==null)return;var sim=world.Simulation;
            if(phase=="waitingPlay")
            {
                Require(sim.Cells.Length==1120&&world.Origin==new Vector3Int(-4,-2,0),"Expected 40x28 Tilemap with negative origin");var village=sim.Villages[0];Require(sim.NearRiver(village.cell)&&sim.ReadRegion(village.cell).cells==36,"Initial village must be by river and read exactly 36 cells");
                Require(world.GetComponentsInChildren<ElementTile>().Length==0,"Per-cell scripts must not be used");Require(world.FindHoveredVillage(world.Position(village.cell)+new Vector3(.5f,.6f))==village&&world.hud.VillageText(village).Contains("8 人"),"Village hover/population readout failed");
                Require(sim.AddVillage(sim.Index(village.cell%sim.Width-1,village.cell/sim.Width))==null,"Village exclusion failed");world.Paused=true;SessionState.SetFloat(Key+"startX",world.player.Position.x);world.player.SetVerificationMovement(Vector2.right);TimeStart();Phase("moving");return;
            }
            if(phase=="moving"&&Elapsed()>.35f)
            {
                float movement=world.player.Position.x-SessionState.GetFloat(Key+"startX",0);Require(movement>.5f,"Real character movement failed");SessionState.SetFloat(Key+"movement",movement);
                world.player.Teleport(new Vector2(8.4f,13.5f));world.player.SetVerificationMovement(Vector2.right);TimeStart();Phase("riverCollision");return;
            }
            if(phase=="riverCollision"&&Elapsed()>.5f)
            {
                float x=world.player.Position.x;Require(x>8.6f&&x<8.86f,"River boundary collision failed: "+x);SessionState.SetFloat(Key+"collision",x);
                world.player.Teleport(new Vector2(8.5f,4.5f));world.player.SetVerificationMovement(Vector2.right);TimeStart();Phase("bridge");return;
            }
            if(phase=="bridge"&&Elapsed()>.75f)
            {
                float x=world.player.Position.x;Require(x>11,"Bridge traversal failed: "+x);SessionState.SetFloat(Key+"bridge",x);world.player.ReleaseVerificationMovement();
                world.Paused=false;world.hud.inventoryButton.onClick.Invoke();Require(world.InventoryOpen&&world.hud.inventoryPage.activeSelf,"Inventory button/page failed");SessionState.SetInt(Key+"step",sim.StepCount);TimeStart();Phase("inventoryPaused");return;
            }
            if(phase=="inventoryPaused"&&Elapsed()>1)
            {
                Require(sim.StepCount==SessionState.GetInt(Key+"step",0),"Inventory must pause simulation");world.hud.elementButtons[0].onClick.Invoke();Require(!world.InventoryOpen&&!world.AnimalMode&&world.SelectedElement==Element.Gold,"Element selection did not close resource page");world.Paused=true;
                var gold=new Vector3Int(5,7,0);world.player.Teleport(world.ground.GetCellCenterWorld(gold)+Vector3.down*.7f);
                for(int k=0;k<5;k++)Require(world.Place(gold),"Gold placement failed");int stock=sim.Inventory[0];Require(!world.Place(gold)&&sim.Inventory[0]==stock,"Saturated placement must preserve inventory");var other=new Vector3Int(8,7,0);world.player.Teleport(world.ground.GetCellCenterWorld(other)+Vector3.down*.7f);Require(world.Place(other),"Secondary construction reserve failed");
                for(int a=0;a<4;a++)
                {
                    world.ToggleInventory();world.hud.animalButtons[a].onClick.Invoke();var cell=new Vector3Int(a%2==0?4:8,a<2?7:11,0);world.player.Teleport(world.ground.GetCellCenterWorld(cell)+Vector3.down*.7f);Require(world.AnimalMode&&!world.InventoryOpen&&world.Place(cell),"Farm animal placement failed: "+a);
                }
                world.SelectElement(Element.Gold);world.Paused=false;int before=sim.StepCount;SessionState.SetInt(Key+"step",before);Phase("scheduledGrowth");return;
            }
            if(phase=="scheduledGrowth"&&sim.StepCount>=SessionState.GetInt(Key+"step",0)+2)
            {
                Require(sim.Villages[0].population>8,"Real scheduled village growth failed");world.Paused=true;
                for(int k=0;k<180&&!sim.Won;k++)world.Tick();Require(sim.Won,"Authored map did not reach prosperity target with available resources: "+sim.Prosperity+", pop="+sim.TotalPopulation);
                Require(sim.Villages[0].tier>=3&&sim.FarmBirths>0&&sim.WildlifeMoves>0,"Growth, farm breeding or wild migration missing");
                SessionState.SetFloat(Key+"prosperity",sim.Prosperity);SessionState.SetInt(Key+"population",sim.TotalPopulation);SessionState.SetInt(Key+"tier",sim.Villages[0].tier);SessionState.SetInt(Key+"births",sim.FarmBirths);SessionState.SetInt(Key+"wildMoves",sim.WildlifeMoves);SessionState.SetInt(Key+"animals",sim.Animals.Count);SessionState.SetInt(Key+"newVillages",sim.NewVillages);
                world.player.Teleport(world.spawn+Vector2.left*3);TimeStart();Phase("prosperousPreview");return;
            }
            if(phase=="prosperousPreview"&&Elapsed()>.65f){ScreenCapture.CaptureScreenshot(Output("prosperous-preview.png"));Phase("prosperousCapture");return;}
            if(phase=="prosperousCapture")
            {
                if(!File.Exists(Output("prosperous-preview.png")))return;world.hud.restartButton.onClick.Invoke();Require(sim!=world.Simulation&&world.Simulation.Villages[0].population==8,"Restart failed");world.Paused=true;world.ToggleInventory();TimeStart();Phase("inventoryPreview");return;
            }
            if(phase=="inventoryPreview"&&Elapsed()>.25f){ScreenCapture.CaptureScreenshot(Output("inventory-preview.png"));Phase("inventoryCapture");return;}
            if(phase=="inventoryCapture")
            {
                if(!File.Exists(Output("inventory-preview.png")))return;world.ToggleInventory();TimeStart();Phase("startPreview");return;
            }
            if(phase=="startPreview"&&Elapsed()>.6f){ScreenCapture.CaptureScreenshot(Output("settlement-preview.png"));Phase("finish");return;}
            if(phase=="finish")
            {
                if(!File.Exists(Output("settlement-preview.png")))return;Require(SessionState.GetInt(Key+"errors",0)==0,"Runtime errors detected");
                var report=new Report{status="passed",scenePath=SettlementSceneBuilder.ScenePath,checks="Actual Unity Play: Tilemap/negative origin, riverside spawn, 36 cells, village exclusion, hover population, character movement, river collision, bridge traversal, editable Canvas resource page, inventory pause, resource placement with budget, four farm species, real scheduled population growth, prosperity win, farm breeding, wildlife migration, restart",cellCount=1120,regionCells=36,initialPopulation=8,finalPopulation=SessionState.GetInt(Key+"population",0),villageTier=SessionState.GetInt(Key+"tier",0),animals=SessionState.GetInt(Key+"animals",0),farmBirths=SessionState.GetInt(Key+"births",0),wildMoves=SessionState.GetInt(Key+"wildMoves",0),prosperity=SessionState.GetFloat(Key+"prosperity",0),movementDistance=SessionState.GetFloat(Key+"movement",0),riverCollisionX=SessionState.GetFloat(Key+"collision",0),bridgeCrossingX=SessionState.GetFloat(Key+"bridge",0),runtimeErrors=0,prosperityWin=true,hoverPopulation=true,inventoryPauses=true,previewPath=Output("settlement-preview.png"),inventoryPath=Output("inventory-preview.png")};
                report.newVillages=SessionState.GetInt(Key+"newVillages",0);File.WriteAllText(Output("report.json"),JsonUtility.ToJson(report,true));SessionState.SetString(Key+"phase","");Debug.Log("河岸繁荣：场景、人口、动物、资源页面与繁荣度通关验证通过。已恢复起点并暂停，按空格开始建设。");
            }
        }
        catch(Exception e){string output=SessionState.GetString(Key+"output","");if(output!="")File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{status="failed",error=e.ToString()},true));SessionState.SetString(Key+"phase","");Debug.LogException(e);}
    }
}
#endif
