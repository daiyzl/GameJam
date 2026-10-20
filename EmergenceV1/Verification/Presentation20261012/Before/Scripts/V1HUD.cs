using UnityEngine;
using UnityEngine.UI;

public class V1HUD : MonoBehaviour
{
    public V1World world;
    public Text title, status, clockLabel, feedback, readout, tutorial, villageDetails, amountLabel;
    public Image timeImage;
    public GameObject toolsPage, villagePanel;
    public Button advanceButton, toolsButton, closeToolsButton, restartButton, rainButton, raiseButton, lowerButton;
    public Button[] elementButtons, amountButtons;
    private Font font;
    private readonly string[] tasks={
        "1 / 6 · 观察时间\n点击“渡过时段”：早晨 → 中午 → 傍晚 → 次日早晨。移动与投放不会推动时间。",
        "2 / 6 · 播种新树\n到村庄西南教学标记处（7,6），选择木播种，补水、土，再渡过时段。3×3 已有树格必须少于 4。",
        "3 / 6 · 让树苗长大\n继续为自己的树补水和养分，再渡过时段。木量达到 3 变为树，达到 8 变为树林。",
        "4 / 6 · 观察材料来源\n村庄只能从树／树林采木、从矿点采金。查看村庄库存；西北低地会使分散金逐步富集。",
        "5 / 6 · 维持动物生境\n保留草地、水源和林缘。野生动物迁移，养殖动物在食物与水足够时繁殖。",
        "6 / 6 · 帮助河岸繁荣\n村庄范围：小型 6×6、中型 7×7、大型 8×8。保留树与草，留意金材、水质和淤积。繁荣≥65 并保持 5 个时段。",
        "教学完成 · 河岸已繁荣\n可以继续渡过时间，观察资源枯竭、迁移、水土变化，或重开尝试另一种布局。"
    };
    private void Awake()
    {
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
        foreach(var t in GetComponentsInChildren<Text>(true))t.font=font;
        advanceButton.onClick.AddListener(world.AdvancePhase);toolsButton.onClick.AddListener(world.ToggleTools);closeToolsButton.onClick.AddListener(world.ToggleTools);restartButton.onClick.AddListener(world.ResetWorld);
        rainButton.onClick.AddListener(()=>{world.ScheduleRain();if(world.ToolsOpen)world.ToggleTools();});
        raiseButton.onClick.AddListener(()=>{world.SelectTerrain(1);world.ToggleTools();});lowerButton.onClick.AddListener(()=>{world.SelectTerrain(-1);world.ToggleTools();});
        for(int e=0;e<elementButtons.Length;e++){int n=e;elementButtons[e].onClick.AddListener(()=>{world.Select((V1Element)n);world.ToggleTools();});}
        float[] amounts={1,4,8};for(int e=0;e<amountButtons.Length;e++){int n=e;amountButtons[e].onClick.AddListener(()=>world.SetAmount(amounts[n]));}
        SetTools(false);
    }
    public void SetTools(bool show){if(toolsPage!=null)toolsPage.SetActive(show);}
    private void Update()
    {
        var s=world.Simulation;if(s==null)return;
        title.text=world.LevelWon?"河岸繁荣 V1 · 教学通关":"河岸繁荣 V1 · 让世界自己生长";
        status.text=$"繁荣 {s.Prosperity:0.0} / {s.Settings.prosperityTarget:0}    稳定 {s.StablePhases} / {s.Settings.prosperityHoldPhases} 时段    人口 {s.TotalPopulation}    村庄 {s.ActiveVillages}";
        clockLabel.text=$"第 {s.Day} 天 · {s.PhaseName}\n时间等待你的选择";
        advanceButton.GetComponentInChildren<Text>().text="渡过时段 → "+s.NextPhaseName;advanceButton.interactable=!world.ToolsOpen&&!world.Busy;
        timeImage.sprite=world.timeBackgrounds[s.Phase];
        feedback.text=world.Message+"\nWASD 移动 · Tab 五行页 · E / 近处点击投放 · Q 安排降雨 · R 重开 · 当前 "+(world.TerrainMode==0?V1World.ElementName(world.Selected):world.TerrainMode>0?"垫高":"挖低");
        tutorial.text=tasks[Mathf.Clamp(s.TutorialStage,0,6)];
        var c=world.ReadCell(world.HoveredCell);int i=world.Index(world.HoveredCell);
        string type=i>=0&&s.IsWet(i)?s.BridgeUsable(i)?"桥面":"河流／积水":c.Mine==V1Mine.Active?"固定矿点":c.Mine==V1Mine.Exhausted?"枯竭矿遗址":c.Dead(s.Settings.deadGold,s.Settings.mineGold)?"死地（停止富集，污染四邻）":c.Tree?s.PlantStage(i)==3?"树林":s.PlantStage(i)==2?"树":"树苗":c.Grass>.2f?"草地":"裸地";
        readout.text=$"({world.HoveredCell.x},{world.HoveredCell.y}) {type} · {V1Terrain.Name(V1Terrain.Stage(c.Height,s.Settings))}   金 {c.Gold:0.0} 悬浮金 {c.MudGold:0.0} 木 {c.Wood:0.0} 水 {c.Water:0.0} 火 {c.Fire:0.0} 土 {c.Earth:0.0}\n草 {c.Grass:0.0} 种子 {c.Seeds} 污染 {c.Pollution:0.0} 高度 {c.Height:0.00} 淤积 {c.Deposited:0.0}   3×3 有树格 {(i>=0?s.TreeCells3x3(i):0)} / 4（少于4才萌发）";
        villagePanel.SetActive(world.HoveredVillage!=null&&!world.ToolsOpen);if(world.HoveredVillage!=null)villageDetails.text=VillageText(world.HoveredVillage);
        amountLabel.text=$"无限投放 · 当前每次 {world.PlacementAmount:0} 份（播种始终 1 粒）";
    }
    public string VillageText(V1Village v)
    {
        var r=world.Simulation.ReadRegion(v.cell);
        return $"村庄 {v.id} · {new[]{"","小型","中型","大型"}[v.tier]}\n当前人口：{v.population} 人 · 迁移 {v.migrations} 次\n住房 {v.housing:0} / 环境承载 {r.capacity:0}\n采材／统计 {r.cells} 格 · 树格 {r.trees} · 取水点 {r.wet}\n木材库存 {v.timber:0.0} · 金材库存 {v.metal:0.0}\n有效矿点 {r.mines} · 工具状况 {v.toolHealth*100:0}%\n本村养殖 {r.farmed} · 野生 {r.wild}\n平均污染 {r.pollution:0.0} · 饮水污染 {r.drinkPollution:0.0}\n健康 {r.health*100:0}% · 火灾 {r.burning} · 积水 {r.flooded}\n治理炉：{(v.furnace?v.cleaning?"治理中":"待命":"未建设")}\n{v.condition}";
    }
    private void OnDestroy(){if(font!=null)Destroy(font);}
}
