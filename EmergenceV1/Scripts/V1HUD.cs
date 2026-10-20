using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class V1HUD : MonoBehaviour
{
    public int uiRevision;
    public V1World world;
    public Text status,clockLabel,feedback,readout,tutorial,villageDetails,amountLabel,statusDetails,statusMessage;
    public Image timeImage;
    public GameObject toolsPage,villagePanel,tutorialPanel,resultPage,statusPage,feedbackPanel;
    public RectTransform cellDataPanel;
    public Button closeStatusButton;
    public bool StatusOpen => statusPage!=null&&statusPage.activeSelf;
    public Button advanceButton,toolsButton,closeToolsButton,restartButton,rainButton,raiseButton,lowerButton,endButton;
    public Button coordinateButton,resultRestartButton,resultMenuButton,resultQuitButton;
    public Button[] elementButtons,amountButtons,honorButtons;
    public InputField coordinateX,coordinateY;
    public Text coordinateFeedback,resultTime,resultSummary,honorHint,resultEmpty;
    private readonly string[] tasks={
        "观察河岸\n点击「渡过时段」，让环境完成一次演化。",
        "播下一粒种子\n使用木工具播种，补充水和土；周围3×3的有树格须少于4。",
        "让它长成一棵树\n继续照料亲手播下的树苗，木量达到3后完成教学。"
    };
    private void Awake()
    {
        advanceButton.onClick.AddListener(world.AdvancePhase);toolsButton.onClick.AddListener(world.ToggleTools);
        closeToolsButton.onClick.AddListener(world.ToggleTools);restartButton.onClick.AddListener(world.ResetWorld);
        endButton.onClick.AddListener(world.EndGame);
        closeStatusButton.onClick.AddListener(world.ToggleStatus);
        rainButton.onClick.AddListener(world.ScheduleRain);
        raiseButton.onClick.AddListener(()=>world.SelectTerrain(1));lowerButton.onClick.AddListener(()=>world.SelectTerrain(-1));
        for(int e=0;e<elementButtons.Length;e++){int n=e;elementButtons[e].onClick.AddListener(()=>world.Select((V1Element)n));}
        float[] amounts={1,4,8};for(int i=0;i<amountButtons.Length;i++){int n=i;amountButtons[i].onClick.AddListener(()=>world.SetAmount(amounts[n]));}
        coordinateButton.onClick.AddListener(()=>{world.PlaceAtCoordinates(coordinateX.text,coordinateY.text);coordinateFeedback.text=world.Message;});
        resultRestartButton.onClick.AddListener(world.ResetWorld);
        resultMenuButton.onClick.AddListener(()=>SceneManager.LoadSceneAsync("StartMenu"));
        resultQuitButton.onClick.AddListener(Quit);
        SetTools(false);SetStatus(false);HideResults();
    }
    public void SetTools(bool show){if(toolsPage!=null)toolsPage.SetActive(show);}
    public void SetStatus(bool show){if(statusPage!=null)statusPage.SetActive(show);}
    public void HideResults(){if(resultPage!=null)resultPage.SetActive(false);}
    public void ShowResults(List<V1Honor> honors)
    {
        SetTools(false);SetStatus(false);villagePanel.SetActive(false);resultPage.SetActive(true);resultPage.transform.SetAsLastSibling();
        int phases=world.Simulation.AdvanceCount;
        resultTime.text=$"{phases/3} 天  ·  {phases%3} 个时段";
        resultSummary.text=$"累计渡过 {phases} 个时段    ·    {(world.Simulation.IsProsperous?"河岸持续繁荣":"河岸仍在生长")}    ·    本局获得 {honors.Count} 枚称号";
        resultEmpty.gameObject.SetActive(honors.Count==0);honorHint.text="将鼠标停在称号上，查看达成条件。";
        for(int i=0;i<honorButtons.Length;i++)
        {
            var button=honorButtons[i];button.gameObject.SetActive(i<honors.Count);
            if(i>=honors.Count)continue;
            button.GetComponentInChildren<Text>().text=honors[i].Name;
            var hover=button.GetComponent<V1HonorHover>();if(hover==null)hover=button.gameObject.AddComponent<V1HonorHover>();
            hover.Configure(this,honors[i].Name+"\n"+honors[i].Condition);
        }
    }
    public void HonorTooltip(string text){honorHint.text=text;}
    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    private void Update()
    {
        var s=world.Simulation;if(s==null)return;
        string season=new[]{"春","夏","秋","冬"}[(int)s.Season]+"季";
        string phaseEvent=new[]{"平静","雨","风","露水","晴日","雷雨","大旱","雪"}[(int)s.CurrentEvent];
        status.text=$"繁荣 {s.Prosperity:0.0} / {s.Settings.prosperityTarget:0}       稳定 {s.StablePhases} / {s.Settings.prosperityHoldPhases} 个时段\n第 {s.Day} 天 · {s.PhaseName} / {season}       本时段事件：{phaseEvent}";
        statusDetails.text=$"人口与村庄     {s.TotalPopulation} 人 / {s.ActiveVillages} 座\n成熟林地     {s.MatureCoverage*100:0.0}%     林地得分 {s.ProsperityForest:0.0}/30\n草木覆盖     {s.ProsperityGreen:0.0}/20\n水土余量     {s.ProsperityResources:0.0}/20\n野生生灵     {s.ProsperityWildlife:0.0}/15\n持续供养     {s.ProsperityLivelihood:0.0}/15\n林地平衡     ×{s.EcosystemBalance:0.00}       灾损扣分 −{s.ProsperityDamage:0.0}";
        statusMessage.text=$"{world.Message}\n地图种子 {world.WorldSeed}   ·   M / Esc 关闭状态";
        clockLabel.text=$"第 {s.Day} 天 · {s.PhaseName} / {season}   <size=18>M 状态</size>";
        advanceButton.GetComponentInChildren<Text>().text="渡过时段  →  "+s.NextPhaseName;
        advanceButton.interactable=!world.MenuOpen&&!world.Busy&&!world.Ended;
        feedback.text=world.Message+"\nWASD 移动 · Tab 五行工具 · M 状态 · E / 近处点击投放 · Q 安排降雨   /   当前："+(world.TerrainMode==0?V1World.ElementName(world.Selected):world.TerrainMode>0?"垫高":"挖低");
        bool showTutorial=!s.TutorialComplete&&!world.Ended&&!world.MenuOpen;
        tutorialPanel.SetActive(showTutorial);feedbackPanel.SetActive(showTutorial);
        var dataPosition=new Vector2(24,showTutorial?150:18);
        if(cellDataPanel.anchoredPosition!=dataPosition)cellDataPanel.anchoredPosition=dataPosition;
        cellDataPanel.gameObject.SetActive(!world.MenuOpen&&!world.Ended);
        if(!s.TutorialComplete)tutorial.text=tasks[Mathf.Clamp(s.TutorialStage,0,2)];
        var c=world.ReadCell(world.HoveredCell);int cell=world.Index(world.HoveredCell);
        string type=cell>=0&&s.IsWet(cell)?s.BridgeUsable(cell)?"桥面":"河流／积水":c.Mine==V1Mine.Active?"矿点":c.Dead(s.Settings.deadGold,s.Settings.mineGold)?"死地":c.Tree?s.PlantStage(cell)==3?"树林":s.PlantStage(cell)==2?"树":"树苗":c.Grass>.2f?"草地":"裸地";
        readout.text=$"({world.HoveredCell.x}, {world.HoveredCell.y})  {type} · {V1Terrain.Name(V1Terrain.Stage(c.Height,s.Settings))}    金 {c.Gold:0.0}   木 {c.Wood:0.0}   水 {c.Water:0.0}   火 {c.Fire:0.0}   土 {c.Earth:0.0}\n种子 {c.Seeds}/2    污染 {c.Pollution:0.0}    积雪 {c.Snow:0.0}    高度 {c.Height:0.00}    淤积 {c.Deposited:0.0}";
        readout.text+=$"    地图种子 {world.WorldSeed}";
        villagePanel.SetActive(world.HoveredVillage!=null&&!world.MenuOpen&&!world.Ended);
        if(world.HoveredVillage!=null)villageDetails.text=VillageText(world.HoveredVillage);
        amountLabel.text=$"当前：{(world.TerrainMode==0?V1World.ElementName(world.Selected):world.TerrainMode>0?"垫高":"挖低")}    ·    每次 {world.PlacementAmount:0} 份（播种始终1粒）";
        for(int i=0;i<elementButtons.Length;i++)elementButtons[i].GetComponentInChildren<Text>().text=world.TerrainMode==0&&(int)world.Selected==i?"已选中":"选择";
    }
    public string VillageText(V1Village v)
    {
        var r=world.Simulation.ReadRegion(v.cell);
        return $"村庄 {v.id} · {new[]{"","小型","中型","大型"}[v.tier]}\n当前人口：{v.population} 人 · 迁移 {v.migrations} 次\n住房 {v.housing:0} / 环境承载 {r.capacity:0}\n采材／统计 {r.cells} 格 · 树格 {r.trees} · 取水点 {r.wet}\n木材库存 {v.timber:0.0} · 金材库存 {v.metal:0.0}\n有效矿点 {r.mines} · 工具状况 {v.toolHealth*100:0}%\n本村养殖 {r.farmed} · 野生 {r.wild}\n平均污染 {r.pollution:0.0} · 饮水污染 {r.drinkPollution:0.0}\n健康 {r.health*100:0}% · 火灾 {r.burning} · 积水 {r.flooded}\n{v.condition}";
    }
}
