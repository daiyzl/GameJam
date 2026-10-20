using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-100)]
public class SettlementWorld : MonoBehaviour
{
    public Tilemap ground,riverBed,bridges,plants,water,flames,metal,obstacles,walking;
    public TutorialCharacter player;
    public TutorialWeather weather;
    public SettlementHUD hud;
    public Camera worldCamera;
    public LineRenderer hoverRegion,targetOutline;
    public TileBase saplingTile,treeTile,forestTile,goldTile,walkingTile;
    public TileBase[] waterTiles,fireTiles;
    public Sprite villageSprite;
    public Sprite[] animalSprites;
    public Material spriteMaterial;
    public List<TutorialCellSeed> cellSeeds=new List<TutorialCellSeed>();
    public Vector3Int initialVillageCell=new Vector3Int(6,8,0);
    public Vector2 spawn=new Vector2(6.5f,5.5f);
    public SettlementSettings settings=new SettlementSettings();
    public SettlementSimulation Simulation {get;private set;}
    public Vector3Int Origin {get;private set;}
    public Element SelectedElement {get;private set;}=Element.Gold;
    public AnimalKind SelectedAnimal {get;private set;}=AnimalKind.Chicken;
    public bool AnimalMode {get;private set;}
    public bool InventoryOpen {get;private set;}
    public bool Paused {get;set;}
    public string Message {get;private set;}="帮助河边村庄：按 Tab 查看可用资源，先投放金扩建住房，再饲养动物。";
    public SettlementVillage HoveredVillage {get;private set;}
    public Vector3Int HoveredCell {get;private set;}
    private readonly Dictionary<int,SettlementVillageView> villageViews=new Dictionary<int,SettlementVillageView>();
    private readonly Dictionary<int,SpriteRenderer> animalViews=new Dictionary<int,SpriteRenderer>();
    private readonly List<int> removeIds=new List<int>();
    private CellState[] initialCells;
    private bool[] initialBlocked,initialRiver;
    private float[] initialElevation;
    private float accumulator;
    private int rainSteps,lastFireFrame=-1;
    private Vector3Int rainCenter;
    private Transform residents;
    private static readonly Vector3[] CellCorners={new Vector3(-.5f,-.5f),new Vector3(-.5f,.5f),new Vector3(.5f,.5f),new Vector3(.5f,-.5f),new Vector3(-.5f,-.5f)};
    private void Awake()
    {
        ground.CompressBounds();var bounds=ground.cellBounds;Origin=bounds.min;
        Simulation=new SettlementSimulation(bounds.size.x,bounds.size.y,settings);
        for(int y=0;y<bounds.size.y;y++)for(int x=0;x<bounds.size.x;x++)
        {
            int i=Simulation.Index(x,y);var cell=Origin+new Vector3Int(x,y,0);var tile=ground.GetTile<TutorialTile>(cell);var p=plants.GetTile<TutorialTile>(cell);
            Simulation.Blocked[i]=!ground.HasTile(cell)||obstacles.HasTile(cell);Simulation.River[i]=riverBed.HasTile(cell);
            Simulation.Cells[i]=new CellState{Earth=tile!=null?tile.earth:6,Wood=p!=null?p.wood:0,Water=Simulation.River[i]?8:0,Gold=tile!=null?tile.gold:0};
            Simulation.Elevation[i]=Simulation.River[i]?1:2.4f;
        }
        foreach(var seed in cellSeeds){int i=Index(seed.cell);if(i>=0){Simulation.Cells[i]=seed.State;Simulation.Elevation[i]=seed.elevation;}}
        initialCells=(CellState[])Simulation.Cells.Clone();initialBlocked=(bool[])Simulation.Blocked.Clone();initialRiver=(bool[])Simulation.River.Clone();initialElevation=(float[])Simulation.Elevation.Clone();
        var go=new GameObject("运行村庄与动物");go.transform.SetParent(transform,false);residents=go.transform;ResetWorld();
    }
    private void Update()
    {
        if(Simulation==null)return;
        if(Input.GetKeyDown(KeyCode.Tab)||InventoryOpen&&Input.GetKeyDown(KeyCode.Escape))ToggleInventory();
        UpdateHover();
        if(!InventoryOpen)
        {
            for(int e=0;e<5;e++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+e)))SelectElement((Element)e);
            if(Input.GetKeyDown(KeyCode.Space))Paused=!Paused;
            if(Input.GetKeyDown(KeyCode.R))ResetWorld();
            if(Input.GetKeyDown(KeyCode.E))Place(ground.WorldToCell(player.Position+player.Facing));
            if(Input.GetKeyDown(KeyCode.Q))CallRain(ground.WorldToCell(player.Position));
            if(Input.GetMouseButtonDown(0)&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))Place(HoveredCell);
        }
        if(!Paused&&!InventoryOpen)
        {
            accumulator+=Time.deltaTime;int loops=0;
            while(accumulator>=settings.stepSeconds&&loops++<4){accumulator-=settings.stepSeconds;Tick();}
        }
        int frame=Mathf.FloorToInt(Time.time*8)%4;if(frame!=lastFireFrame){lastFireFrame=frame;RefreshFire(frame);}
        AnimateAnimals();
    }
    public int Index(Vector3Int cell)=>Simulation.Index(cell.x-Origin.x,cell.y-Origin.y);
    public Vector3Int Cell(int index)=>Origin+new Vector3Int(index%Simulation.Width,index/Simulation.Width,0);
    public Vector3 Position(int index)=>ground.GetCellCenterWorld(Cell(index));
    public CellState ReadCell(Vector3Int cell){int i=Index(cell);return i>=0?Simulation.Cells[i]:default(CellState);}
    public void SelectElement(Element element){SelectedElement=element;AnimalMode=false;Message="已选择"+SettlementSimulation.ElementName(element)+"，靠近地块后按 E 或点击投放。";}
    public void SelectAnimal(AnimalKind kind){SelectedAnimal=kind;AnimalMode=true;Message="已选择饲养"+SettlementSimulation.AnimalName(kind)+"，在村庄36格内的空地投放。";}
    public void ToggleInventory()
    {
        InventoryOpen=!InventoryOpen;
        if(InventoryOpen)player.SetVerificationMovement(Vector2.zero);else player.ReleaseVerificationMovement();
        if(hud!=null)hud.SetInventory(InventoryOpen);
    }
    public bool Place(Vector3Int cell)
    {
        if(InventoryOpen)return false;
        if(Vector2.Distance(player.Position,ground.GetCellCenterWorld(cell))>3.2f){Message="走近地块再投放（范围 3 格）。";return false;}
        int i=Index(cell);bool ok;
        if(AnimalMode)ok=Simulation.AddAnimal(i,SelectedAnimal,AnimalOrigin.Farmed,spend:true)!=null;
        else ok=Simulation.AddElement(i,SelectedElement);
        Message=ok?(AnimalMode?"动物加入养殖群；食物充足时会繁殖，为村庄提供食物。":Simulation.LastEvent):"此处不能投放：检查库存、建筑、水域或村庄范围。";
        if(ok){Simulation.CalculateProsperity();RefreshViews();}return ok;
    }
    public bool CallRain(Vector3Int center)
    {
        if(InventoryOpen||rainSteps>0)return false;
        if(Simulation.Inventory[(int)Element.Water]<12){Message="唤雨需要库存中的 12 水。";return false;}
        Simulation.Inventory[(int)Element.Water]-=12;rainCenter=center;rainSteps=4;
        weather.BeginRain(ground.GetCellCenterWorld(center));Message="局部雨水先扑灭火，再滋养植物。";return true;
    }
    public void Tick()
    {
        Simulation.CharacterCell=Index(ground.WorldToCell(player.Position));
        if(rainSteps>0)
        {
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            {int i=Index(rainCenter+new Vector3Int(dx,dy,0));if(Simulation.Valid(i)){var c=Simulation.Cells[i];c.Water=Mathf.Min(12,c.Water+1.5f);Simulation.Cells[i]=c;}}
            if(--rainSteps==0)weather.StopRain();
        }
        Simulation.Step();RefreshViews();
        if(Simulation.Won)Message="村庄已繁荣：地图繁荣度达到目标并持续稳定。你可以继续建设。";
        else if(Simulation.LastEvent.Contains("村庄")&&Simulation.StepCount%2==0)Message=Simulation.LastEvent;
    }
    public void ResetWorld()
    {
        if(InventoryOpen){InventoryOpen=false;player.ReleaseVerificationMovement();if(hud!=null)hud.SetInventory(false);}
        int width=Simulation.Width,height=Simulation.Height;Simulation=new SettlementSimulation(width,height,settings);
        Array.Copy(initialCells,Simulation.Cells,initialCells.Length);Array.Copy(initialBlocked,Simulation.Blocked,initialBlocked.Length);Array.Copy(initialRiver,Simulation.River,initialRiver.Length);Array.Copy(initialElevation,Simulation.Elevation,initialElevation.Length);
        var village=Simulation.AddVillage(Index(initialVillageCell));if(village==null)throw new InvalidOperationException("初始村庄必须靠近水源，36格区域完整且建筑地块可用。");
        foreach(var c in new[]{new Vector3Int(5,6,0),new Vector3Int(8,10,0)})Simulation.AddAnimal(Index(c),c.x==5?AnimalKind.Chicken:AnimalKind.Cow,AnimalOrigin.Farmed,village.id);
        int wildKind=0;
        foreach(var entry in new[]{new Vector3Int(4,14,0),new Vector3Int(15,13,0),new Vector3Int(23,21,0),new Vector3Int(26,15,0)})Simulation.AddAnimal(Index(entry),(AnimalKind)wildKind++,AnimalOrigin.Wild);
        player.Teleport(spawn);weather.StopRain();rainSteps=0;Paused=false;accumulator=0;SelectedElement=Element.Gold;AnimalMode=false;HoveredVillage=null;Simulation.CalculateProsperity();RefreshViews();
        Message="河边村庄需要住房和食物。Tab 查看资源，先在村庄附近投放金。";
    }
    private void Set(Tilemap map,Vector3Int cell,TileBase tile){if(map.GetTile(cell)!=tile)map.SetTile(cell,tile);}
    public void RefreshViews()
    {
        for(int i=0;i<Simulation.Cells.Length;i++)
        {
            var c=Simulation.Cells[i];var cell=Cell(i);bool blocked=!Simulation.Valid(i),building=Simulation.IsBuilding(i);
            Set(plants,cell,!blocked&&!building&&!Simulation.River[i]&&c.Wood>.3f?(c.Wood<3?saplingTile:c.Wood>=8?forestTile:treeTile):null);
            Set(metal,cell,!blocked&&!building&&c.Gold>.1f?goldTile:null);
            Set(water,cell,!blocked&&!bridges.HasTile(cell)&&c.Water>.35f?WaterTile(i):null);
            if(water.HasTile(cell))water.SetColor(cell,new Color(1,1,1,Simulation.River[i]?1:Mathf.Clamp(.35f+c.Water*.1f,.4f,.85f)));
            Set(walking,cell,obstacles.HasTile(cell)||building||!Simulation.River[i]&&c.Wood>11||Simulation.River[i]&&!bridges.HasTile(cell)?walkingTile:null);
        }
        RefreshFire(Mathf.FloorToInt(Time.time*8)%4);RefreshResidents();
    }
    private TileBase WaterTile(int index)
    {
        int x=index%Simulation.Width,y=index/Simulation.Width;bool n=Wet(Simulation.Index(x,y+1)),s=Wet(Simulation.Index(x,y-1)),w=Wet(Simulation.Index(x-1,y)),e=Wet(Simulation.Index(x+1,y));
        int frame=!n&&!w?0:!n&&!e?2:!s&&!w?6:!s&&!e?8:!n?1:!s?7:!w?3:!e?5:4;return waterTiles[frame];
    }
    private bool Wet(int i)=>i>=0&&Simulation.Cells[i].Water>.35f;
    private void RefreshFire(int frame)
    {if(Simulation==null)return;for(int i=0;i<Simulation.Cells.Length;i++)Set(flames,Cell(i),Simulation.Cells[i].Fire>.04f?fireTiles[frame]:null);}
    private SpriteRenderer SpriteObject(string name,Sprite sprite)
    {var go=new GameObject(name);go.transform.SetParent(residents,false);var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=spriteMaterial;renderer.sortingOrder=12;renderer.spriteSortPoint=SpriteSortPoint.Pivot;return renderer;}
    private void RefreshResidents()
    {
        foreach(var v in Simulation.Villages)if(v.active)
        {
            if(!villageViews.TryGetValue(v.id,out var view))
            {var renderer=SpriteObject("村庄 "+v.id,villageSprite);view=renderer.gameObject.AddComponent<SettlementVillageView>();view.world=this;view.villageId=v.id;var hit=renderer.gameObject.AddComponent<BoxCollider2D>();hit.isTrigger=true;hit.size=new Vector2(2.8f,2.1f);villageViews[v.id]=view;}
            view.gameObject.SetActive(true);view.transform.position=Position(v.cell)+new Vector3(.5f,.6f);view.transform.localScale=Vector3.one*(.65f+.12f*v.tier);
            view.gameObject.name=$"村庄 {v.id} · {v.population} 人 · 规模 {v.tier}";
        }
        foreach(var pair in villageViews){var v=Simulation.ById(pair.Key);if(v==null)pair.Value.gameObject.SetActive(false);}
        foreach(var a in Simulation.Animals)if(a.alive)
        {
            if(!animalViews.TryGetValue(a.id,out var view))
            {view=SpriteObject(SettlementSimulation.AnimalName(a.kind),animalSprites[(int)a.kind]);animalViews[a.id]=view;view.transform.position=Position(a.cell)+new Vector3(0,.12f);}
            view.gameObject.SetActive(true);view.color=a.origin==AnimalOrigin.Farmed?new Color(1,1,.88f):Color.white;
            view.gameObject.name=$"{(a.origin==AnimalOrigin.Farmed?"养殖":"野生")}{SettlementSimulation.AnimalName(a.kind)} {a.id}";
        }
        removeIds.Clear();foreach(var pair in animalViews){bool found=false;foreach(var a in Simulation.Animals)if(a.alive&&a.id==pair.Key){found=true;break;}if(!found){Destroy(pair.Value.gameObject);removeIds.Add(pair.Key);}}
        foreach(int id in removeIds)animalViews.Remove(id);
    }
    private void AnimateAnimals()
    {foreach(var a in Simulation.Animals)if(a.alive&&animalViews.TryGetValue(a.id,out var view)){float sway=Mathf.Sin(Time.time*3+a.id)*.035f;Vector3 destination=Position(a.cell)+new Vector3(sway,.12f+Mathf.Abs(sway)*.4f);if(Mathf.Abs(destination.x-view.transform.position.x)>.1f)view.flipX=destination.x<view.transform.position.x;view.transform.position=Vector3.Lerp(view.transform.position,destination,1-Mathf.Exp(-7*Time.deltaTime));}}
    private void UpdateHover()
    {
        if(worldCamera==null)return;var point=worldCamera.ScreenToWorldPoint(Input.mousePosition);point.z=0;HoveredCell=ground.WorldToCell(point);HoveredVillage=FindHoveredVillage(point);
        hoverRegion.enabled=HoveredVillage!=null&&!InventoryOpen;
        if(hoverRegion.enabled)
        {
            var start=ground.CellToWorld(Cell(HoveredVillage.cell)+new Vector3Int(-2,-2,0));
            hoverRegion.positionCount=5;hoverRegion.SetPosition(0,start);hoverRegion.SetPosition(1,start+Vector3.up*6);hoverRegion.SetPosition(2,start+new Vector3(6,6));hoverRegion.SetPosition(3,start+Vector3.right*6);hoverRegion.SetPosition(4,start);
        }
        bool valid=Simulation.Valid(Index(HoveredCell))&&Vector2.Distance(player.Position,ground.GetCellCenterWorld(HoveredCell))<3.2f;
        targetOutline.enabled=valid&&!InventoryOpen&&HoveredVillage==null;
        if(targetOutline.enabled){var center=ground.GetCellCenterWorld(HoveredCell);targetOutline.positionCount=5;for(int i=0;i<5;i++)targetOutline.SetPosition(i,center+CellCorners[i]);}
    }
    public SettlementVillage FindHoveredVillage(Vector3 position)
    {foreach(var pair in villageViews)if(pair.Value.gameObject.activeSelf&&pair.Value.GetComponent<SpriteRenderer>().bounds.Contains(position))return Simulation.ById(pair.Key);return null;}
}
public class SettlementVillageView : MonoBehaviour
{
    public SettlementWorld world;
    public int villageId;
}
