using UnityEngine;
using UnityEngine.UI;

public class SettlementHUD : MonoBehaviour
{
    public SettlementWorld world;
    public Text title,status,feedback,cellValues,villageDetails;
    public GameObject inventoryPage,villagePanel;
    public Button inventoryButton,pauseButton,restartButton,closeInventory;
    public Button[] elementButtons,animalButtons;
    public Text[] elementStocks,animalStocks;
    private Font font;
    private static readonly int[] ElementCost={4,2,4,1,3};
    private void Awake()
    {
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
        foreach(var text in GetComponentsInChildren<Text>(true))text.font=font;
        inventoryButton.onClick.AddListener(world.ToggleInventory);closeInventory.onClick.AddListener(world.ToggleInventory);
        pauseButton.onClick.AddListener(()=>world.Paused=!world.Paused);restartButton.onClick.AddListener(world.ResetWorld);
        for(int e=0;e<5;e++){int index=e;elementButtons[e].onClick.AddListener(()=>{world.SelectElement((Element)index);world.ToggleInventory();});}
        for(int a=0;a<4;a++){int index=a;animalButtons[a].onClick.AddListener(()=>{world.SelectAnimal((AnimalKind)index);world.ToggleInventory();});}
        SetInventory(false);
    }
    public void SetInventory(bool show){inventoryPage.SetActive(show);}
    private void Update()
    {
        var sim=world.Simulation;if(sim==null)return;
        int active=0,wild=0,farmed=0;foreach(var v in sim.Villages)if(v.active)active++;foreach(var a in sim.Animals)if(a.alive){if(a.origin==AnimalOrigin.Wild)wild++;else farmed++;}
        title.text=sim.Won?"河岸繁荣 · 建设成功":"河岸繁荣 · 帮助村庄成长";
        status.text=$"繁荣度 {sim.Prosperity:0.0} / {sim.Settings.prosperityTarget:0}   稳定 {sim.StableSteps} / {sim.Settings.prosperityHoldSteps} 步   人口 {sim.TotalPopulation}   村庄 {active}   野生 {wild} / 养殖 {farmed}";
        string selected=world.AnimalMode?"饲养"+SettlementSimulation.AnimalName(world.SelectedAnimal):SettlementSimulation.ElementName(world.SelectedElement);
        feedback.text=$"{world.Message}\nWASD 移动   Tab 资源页   1–5 五行   E / 点击投放   Q 唤雨（12水）   空格暂停   R 重开   当前：{selected}";
        var cell=world.HoveredCell;var c=world.ReadCell(cell);cellValues.text=$"地块 {cell.x},{cell.y}    金 {c.Gold:0.0}   木 {c.Wood:0.0}   水 {c.Water:0.0}   火 {c.Fire:0.0}   土 {c.Earth:0.0}";
        var village=world.HoveredVillage;villagePanel.SetActive(village!=null&&!world.InventoryOpen);
        if(village!=null)villageDetails.text=VillageText(village);
        pauseButton.GetComponentInChildren<Text>().text=world.Paused?"继续":"暂停";
        for(int e=0;e<5;e++){int stock=sim.Inventory[e];elementStocks[e].text=$"库存 {stock} · 每次 {ElementCost[e]}";elementButtons[e].interactable=stock>=ElementCost[e];}
        for(int a=0;a<4;a++){int stock=sim.FarmInventory[a];animalStocks[a].text=$"可饲养 {stock} 只 · 在村庄区域投放";animalButtons[a].interactable=stock>0;}
    }
    public string VillageText(SettlementVillage village)
    {
        var r=world.Simulation.ReadRegion(village.cell);string scale=new[]{"","聚落","小村","村庄","大村庄"}[village.tier];
        return $"村庄 {village.id} · {scale}\n当前人口：{village.population} 人\n住房 {village.housing:0} / 环境容量 {r.capacity:0}\n周围 {r.cells} 格 · 饮水点 {r.wet} · 植被 {r.green}\n野生 {r.wild} / 养殖 {r.farmed} · 食物 {r.food:0.0}\n环境健康 {r.health*100:0}% · 火灾 {r.burning} 格\n{village.condition}\n迁移倒计时：{Mathf.Max(0,world.settings.migrationDelay-village.badSteps)} 步（环境恶化时）";
    }
    private void OnDestroy(){if(font!=null)Destroy(font);}
}
