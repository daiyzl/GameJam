using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

[Serializable] public class V1CellSeed { public Vector3Int cell; public V1Cell state; }
[Serializable] public class V1AnimalSeed { public Vector3Int cell; public V1AnimalKind kind; public V1AnimalOrigin origin; }
[Serializable] public class V1BridgeSpan { public GameObject view; public Vector3Int[] cells; }

[DefaultExecutionOrder(-100)]
public class V1World : MonoBehaviour
{
    public Tilemap ground, riverMarkers, sourceMarkers, outletMarkers, bridgeMarkers, obstacles, grass, water, fire, walking;
    public TutorialCharacter player;
    public TutorialWeather weather;
    public V1HUD hud;
    public Camera worldCamera;
    public V1Settings settings = new V1Settings();
    public Vector3Int initialVillageCell = new Vector3Int(9,8,0);
    public Vector2 spawn = new Vector2(9.5f,4.5f);
    public List<V1CellSeed> cellSeeds = new List<V1CellSeed>();
    public List<V1AnimalSeed> animalSeeds = new List<V1AnimalSeed>();
    public List<V1BridgeSpan> bridges = new List<V1BridgeSpan>();
    public GameObject[] plantPrefabs, mineralPrefabs, animalPrefabs, villagePrefabs;
    public GameObject pollutionPrefab, slopePrefab, damPrefab;
    public TileBase soilTile, grassTile, deadTile, dryTile, pollutedWaterTile, walkingTile;
    public TileBase[] waterTiles, fireTiles;
    public Sprite[] timeBackgrounds;
    public Material spriteMaterial;
    public LineRenderer regionOutline, cellOutline, densityOutline;
    public V1Simulation Simulation { get; private set; }
    public Vector3Int Origin { get; private set; }
    public V1Element Selected { get; private set; } = V1Element.Wood;
    public bool ToolsOpen { get; private set; }
    public bool Busy { get; private set; }
    public int TerrainMode { get; private set; }
    public float PlacementAmount { get; private set; } = 4;
    public Vector3Int HoveredCell { get; private set; }
    public V1Village HoveredVillage { get; private set; }
    public string Message { get; private set; } = "时间由你决定：先观察河岸，再点击右上角“渡过时段”。";
    public bool LevelWon => Simulation != null && Simulation.Won && Simulation.TutorialStage == 6;
    public double LastEnvironmentMilliseconds { get; private set; }
    private V1Cell[] initial;
    private Transform viewsRoot;
    private readonly Dictionary<int,GameObject> plants = new Dictionary<int,GameObject>(), minerals = new Dictionary<int,GameObject>(), clouds = new Dictionary<int,GameObject>(), terrain = new Dictionary<int,GameObject>(), villages = new Dictionary<int,GameObject>(), animals = new Dictionary<int,GameObject>();
    private readonly Dictionary<int,int> plantStages = new Dictionary<int,int>(), mineralStages = new Dictionary<int,int>(), villageStages = new Dictionary<int,int>(), terrainStages = new Dictionary<int,int>();
    private readonly List<int> stale = new List<int>();
    private readonly Dictionary<int,GameObject> deadEffects = new Dictionary<int,GameObject>();
    private int animalVisualSeed;
    private void Awake()
    {
        Application.runInBackground=true;
        ground.CompressBounds(); var bounds = ground.cellBounds; Origin = bounds.min;
        initial = new V1Cell[bounds.size.x*bounds.size.y];
        for (int y=0;y<bounds.size.y;y++) for(int x=0;x<bounds.size.x;x++)
        {
            int i=x+y*bounds.size.x; var coordinate=Origin+new Vector3Int(x,y,0); var tile=ground.GetTile<V1GroundTile>(coordinate);
            bool river=riverMarkers.HasTile(coordinate);
            initial[i]=new V1Cell {
                Earth=tile!=null?tile.earth:12, Grass=tile!=null?tile.grass:1.5f, Water=tile!=null?tile.water:1.2f,
                Height=(tile!=null?tile.height:2.8f)+(river?(coordinate.y+1)*.04f:0), SoilBody=tile!=null?tile.soilBody:30,
                Blocked=!ground.HasTile(coordinate)||obstacles.HasTile(coordinate), River=river,
                Source=sourceMarkers.HasTile(coordinate), Outlet=outletMarkers.HasTile(coordinate), Bridge=bridgeMarkers.HasTile(coordinate)
            };
        }
        foreach(var seed in cellSeeds) { int i=(seed.cell.x-Origin.x)+(seed.cell.y-Origin.y)*bounds.size.x; if(i>=0&&i<initial.Length)initial[i]=seed.state; }
        var root=new GameObject("环境对象 · 数据条件生成");root.transform.SetParent(transform,false);viewsRoot=root.transform;
        ResetWorld();
    }
    private void Update()
    {
        if(Simulation==null)return;
        Simulation.CharacterCell=Index(ground.WorldToCell(player.Position));
        if (!Simulation.Walkable(Simulation.CharacterCell)) EnsurePlayerSafe();
        if(Input.GetKeyDown(KeyCode.Tab)||ToolsOpen&&Input.GetKeyDown(KeyCode.Escape))ToggleTools();
        UpdateHover();
        if(ToolsOpen||Busy)return;
        for(int e=0;e<5;e++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+e)))Select((V1Element)e);
        if(Input.GetKeyDown(KeyCode.R))ResetWorld();
        if(Input.GetKeyDown(KeyCode.Q))ScheduleRain();
        if(Input.GetKeyDown(KeyCode.E))Place(ground.WorldToCell(player.Position+player.Facing*.9f));
        if(Input.GetMouseButtonDown(0)&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))Place(HoveredCell);
        // Deliberately no accumulator, timer or automatic simulation call here.
    }
    public void ResetWorld()
    {
        animalVisualSeed=Guid.NewGuid().GetHashCode();
        var bounds=ground.cellBounds;Simulation=new V1Simulation(bounds.size.x,bounds.size.y,settings,unchecked((uint)animalVisualSeed));Array.Copy(initial,Simulation.Cells,initial.Length);Simulation.Initialize();
        var village=Simulation.AddVillage(Index(initialVillageCell));if(village==null)throw new InvalidOperationException("Initial village needs safe riverside land");
        foreach(var seed in animalSeeds)Simulation.AddAnimal(Index(seed.cell),seed.kind,seed.origin,seed.origin==V1AnimalOrigin.Farmed?village.id:-1);
        ClearViews(plants);ClearViews(minerals);ClearViews(clouds);ClearViews(terrain);ClearViews(villages);ClearViews(animals);ClearViews(deadEffects);
        plantStages.Clear();mineralStages.Clear();villageStages.Clear();terrainStages.Clear();
        player.Teleport(spawn);player.ReleaseVerificationMovement();ToolsOpen=false;Busy=false;TerrainMode=0;
        if(weather!=null)weather.StopRain();
        if(hud!=null)hud.SetTools(false);
        Message="第 1 天早晨：环境不会自行推进。点击“渡过时段”观察第一轮变化。";
        RefreshDisplay();
    }
    private static void ClearViews(Dictionary<int,GameObject> dict){foreach(var v in dict.Values)if(v!=null){v.SetActive(false);Destroy(v);}dict.Clear();}
    public void ToggleTools()
    {
        ToolsOpen=!ToolsOpen;hud.SetTools(ToolsOpen);
        if(ToolsOpen)player.SetVerificationMovement(Vector2.zero);else player.ReleaseVerificationMovement();
    }
    public void Select(V1Element element){Selected=element;TerrainMode=0;Message=ElementHelp(element);}
    public void SelectTerrain(int mode){TerrainMode=mode;Selected=V1Element.Earth;Message=mode>0?"垫高会改变水路和金富集低点；地形操作不直接增加养分。":"挖低会改变水路和金富集低点。";}
    public void SetAmount(float amount){PlacementAmount=Mathf.Clamp(amount,1,12);}
    public bool Place(Vector3Int cell)
    {
        if(ToolsOpen||Busy)return false;int i=Index(cell);
        if(i<0||Vector2.Distance(player.Position,ground.GetCellCenterWorld(cell))>3.2f){Message="靠近目标地块后再投放（3 格以内）。";return false;}
        bool success=TerrainMode!=0?Simulation.AdjustTerrain(i,TerrainMode*.2f):Simulation.PlaceElement(i,Selected,PlacementAmount);
        if(success)
        {
            Message=TerrainMode!=0?"地形已调整，下一时段水流会重新结算。":Selected==V1Element.Wood?"种子已投放；下一时段满足水土与 3×3 密度条件后萌发。":"元素已投放；点击“渡过时段”结算影响。";
            RefreshDisplay();
        }
        else Message="此处是障碍、村庄占地或不可调整的矿点。";
        return success;
    }
    public void ScheduleRain()
    {
        int i=Index(ground.WorldToCell(player.Position));Simulation.QueueRain(i);Message="已安排人物附近 5×5 格降雨；点击渡过时段后落雨。";
    }
    public void AdvancePhase()
    {
        if(Busy||ToolsOpen)return;Busy=true;
        try
        {
            bool rain=Simulation.PendingRainCell>=0;int rainCell=Simulation.PendingRainCell;
            Simulation.CharacterCell=Index(ground.WorldToCell(player.Position));var watch=System.Diagnostics.Stopwatch.StartNew();
            UnityEngine.Profiling.Profiler.BeginSample("V1.Environment.AdvancePhase");
            try { Simulation.AdvancePhase(); } finally { UnityEngine.Profiling.Profiler.EndSample();watch.Stop();LastEnvironmentMilliseconds=watch.Elapsed.TotalMilliseconds; }
            if(weather!=null){if(rain)weather.BeginRain(Position(rainCell));else weather.StopRain();}
            Message=Simulation.LastEvent;RefreshDisplay();
        }
        finally{Busy=false;}
    }
    public int Index(Vector3Int cell)=>Simulation==null?IndexInitial(cell):Simulation.Index(cell.x-Origin.x,cell.y-Origin.y);
    private int IndexInitial(Vector3Int cell){var b=ground.cellBounds;int x=cell.x-Origin.x,y=cell.y-Origin.y;return x>=0&&y>=0&&x<b.size.x&&y<b.size.y?x+y*b.size.x:-1;}
    public Vector3Int Coordinate(int i)=>Origin+new Vector3Int(i%Simulation.Width,i/Simulation.Width,0);
    public Vector3 Position(int i)=>ground.GetCellCenterWorld(Coordinate(i));
    public V1Cell ReadCell(Vector3Int cell){int i=Index(cell);return i>=0?Simulation.Cells[i]:default;}
    public static string ElementName(V1Element e)=>new[]{"金","木 · 播种","水","火","土 · 养分"}[(int)e];
    public static string ElementHelp(V1Element e)=>new[]{"金会压制同格木；低于成矿阈值时向低处富集，成矿后可开采。","木工具投放种子；土水充足、无火且 3×3 有树格少于 4 才能建立新树。","水用于灌溉与灭火；流动也会搬运泥沙和污染，过量积水会伤害村庄。","火烧植物、回补灰烬并处理部分污染；强火损金，干燥燃料会助长火灾。","土提供养分；垫高与挖低使用另外的按钮，养分消耗不改变地形高度。"}[(int)e];
    private void UpdateHover()
    {
        if(worldCamera==null)return;var mouse=Input.mousePosition;bool onScreen=mouse.x>=0&&mouse.y>=0&&mouse.x<Screen.width&&mouse.y<Screen.height;
        var point=onScreen?worldCamera.ScreenToWorldPoint(mouse):(Vector3)player.Position;point.z=0;HoveredCell=ground.WorldToCell(point);if(Index(HoveredCell)<0){HoveredCell=ground.WorldToCell(player.Position);point=player.Position;}HoveredVillage=onScreen?FindHoveredVillage(point):null;
        int i=Index(HoveredCell);cellOutline.enabled=i>=0&&!ToolsOpen;
        if(cellOutline.enabled)DrawRect(cellOutline,ground.GetCellCenterWorld(HoveredCell),1,1);
        densityOutline.enabled=i>=0&&!ToolsOpen&&Selected==V1Element.Wood;
        if(densityOutline.enabled){DrawRect(densityOutline,Position(i),3,3);densityOutline.startColor=densityOutline.endColor=Simulation.TreeCells3x3(i)<4?new Color(.6f,1,.45f):new Color(1,.5f,.25f);}
        regionOutline.enabled=HoveredVillage!=null&&!ToolsOpen;
        if(regionOutline.enabled)DrawRect(regionOutline,Position(HoveredVillage.cell)+new Vector3(.5f,.5f),6,6);
    }
    public V1Village FindHoveredVillage(Vector3 point)
    {
        foreach(var v in Simulation.Villages)if(v.active&&villages.TryGetValue(v.id,out var go))
        {var b=go.GetComponentInChildren<SpriteRenderer>().bounds;if(point.x>=b.min.x&&point.x<=b.max.x&&point.y>=b.min.y&&point.y<=b.max.y)return v;}
        return null;
    }
    private static void DrawRect(LineRenderer line,Vector3 center,float width,float height)
    {
        float x=width*.5f,y=height*.5f;line.positionCount=5;line.SetPositions(new[]{center+new Vector3(-x,-y),center+new Vector3(-x,y),center+new Vector3(x,y),center+new Vector3(x,-y),center+new Vector3(-x,-y)});
    }
    private GameObject Spawn(GameObject prefab,Vector3 position,int order)
    {
        if(prefab==null)return null;var go=Instantiate(prefab,position,Quaternion.identity,viewsRoot);
        foreach(var r in go.GetComponentsInChildren<SpriteRenderer>()){r.sortingOrder=order;r.spriteSortPoint=SpriteSortPoint.Pivot;}return go;
    }
    private void StageView(Dictionary<int,GameObject> dict,Dictionary<int,int> stages,int key,int stage,GameObject prefab,Vector3 position,int order)
    {
        if(stages.TryGetValue(key,out int old)&&old==stage&&dict.TryGetValue(key,out var same)){same.transform.position=position;return;}
        if(dict.TryGetValue(key,out var existing)){existing.SetActive(false);Destroy(existing);dict.Remove(key);}stages[key]=stage;
        if(stage>=0&&prefab!=null)dict[key]=Spawn(prefab,position,order);
    }
    public void RefreshDisplay()
    {
        if(Simulation==null)return;
        RefreshBridges();
        for(int i=0;i<Simulation.Cells.Length;i++)
        {
            var c=Simulation.Cells[i];var cell=Coordinate(i);if(c.Blocked){RefreshCollision(i);continue;}
            bool dead=c.Dead(settings.deadGold,settings.mineGold);
            bool wet=Simulation.IsWet(i);
            grass.SetTile(cell,wet?null:dead?deadTile:c.Grass>.3f?grassTile:c.Earth<1&&c.Water<.3f?dryTile:null);
            water.SetTile(cell,wet?waterTiles[WaterTileIndex(i)]:null);
            water.SetTileFlags(cell,TileFlags.None);
            water.SetColor(cell,wet?Color.Lerp(Color.white,new Color(.42f,.66f,.32f),Mathf.Clamp01(c.Pollution/70)):Color.white);
            if(dead&&!wet)
            {
                if(!deadEffects.TryGetValue(i,out var effect))
                {effect=new GameObject("死地尘雾 "+cell);effect.transform.SetParent(viewsRoot,false);effect.transform.position=Position(i);effect.AddComponent<V1DeadLandEffect>();deadEffects[i]=effect;}
                effect.GetComponent<V1DeadLandEffect>().Configure(c.Pollution);
            }
            else if(deadEffects.TryGetValue(i,out var oldEffect)){oldEffect.SetActive(false);Destroy(oldEffect);deadEffects.Remove(i);}
            fire.SetTile(cell,c.Fire>.01f?fireTiles[Mathf.Clamp((int)c.Fire,0,fireTiles.Length-1)]:null);
            int p=wet?0:Simulation.PlantStage(i);StageView(plants,plantStages,i,p>0?p:-1,p>0?plantPrefabs[p-1]:null,Position(i)+Vector3.up*(p==1?.22f:p==2?.52f:.55f),12);
            if(p>=2&&plants.TryGetValue(i,out var plant)) ConfigureTreeCollider(plant,i,p);
            int m=wet||dead?-1:c.Mine==V1Mine.Active?0:c.Mine==V1Mine.Exhausted?2:c.Gold>=settings.deadGold?1:-1;
            StageView(minerals,mineralStages,i,m,m>=0?mineralPrefabs[m]:null,Position(i)+Vector3.up*.3f,13);
            if(c.Pollution>10&&!wet&&!dead)
            {
                if(!clouds.TryGetValue(i,out var cloud)){cloud=Spawn(pollutionPrefab,Position(i),30);clouds[i]=cloud;var animation=cloud.GetComponent<Animation>();if(animation!=null)animation.enabled=false;}
                foreach(var r in cloud.GetComponentsInChildren<SpriteRenderer>())r.color=new Color(.12f,.12f,.14f,Mathf.Clamp(c.Pollution/100,.15f,.65f));
            }
            else if(clouds.TryGetValue(i,out var oldCloud)){Destroy(oldCloud);clouds.Remove(i);}
            // soil_slope is river embankment art; never generate it on raised land.
            int t=c.River&&c.Deposited>=2?1:-1;
            StageView(terrain,terrainStages,i,t,t==1?damPrefab:null,Position(i),8);
            RefreshCollision(i);
        }
        stale.Clear();foreach(var entry in villages)if(Simulation.VillageById(entry.Key)==null)stale.Add(entry.Key);
        foreach(int id in stale){Destroy(villages[id]);villages.Remove(id);villageStages.Remove(id);}
        foreach(var v in Simulation.Villages)if(v.active)StageView(villages,villageStages,v.id,v.tier,villagePrefabs[v.tier-1],Position(v.cell)+new Vector3(v.tier==2?0:.5f,.6f),15);
        stale.Clear();foreach(var entry in animals){bool alive=false;foreach(var a in Simulation.Animals)if(a.id==entry.Key&&a.alive){alive=true;break;}if(!alive)stale.Add(entry.Key);}
        foreach(int id in stale){Destroy(animals[id]);animals.Remove(id);}
        foreach(var a in Simulation.Animals)if(a.alive&&!animals.ContainsKey(a.id))
        {var view=Spawn(animalPrefabs[(int)a.kind],Position(a.cell)+Vector3.up*.18f,18);animals[a.id]=view;view.AddComponent<V1AnimalView>().Configure(this,a,unchecked(animalVisualSeed+a.id*7919));}
        var tint=Simulation.Phase==0?new Color(1,.97f,.9f):Simulation.Phase==1?Color.white:new Color(.87f,.77f,.81f);
        ground.color=grass.color=water.color=tint;
        worldCamera.backgroundColor=Simulation.Phase==2?new Color(.20f,.22f,.26f):new Color(.25f,.35f,.24f);
        RefreshBoundary();walking.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();Physics2D.SyncTransforms();EnsurePlayerSafe();
    }
    private void RefreshCollision(int i)
    {
        if(i<0)return;var c=Simulation.Cells[i];
        bool block=!Simulation.Walkable(i);
        walking.SetTile(Coordinate(i),block?walkingTile:null);
    }
    private int WaterTileIndex(int i)
    {
        // Pools use open water; a single-cell pool must not become a square bank tile.
        if(!Simulation.Cells[i].River)return 4;
        var cell=Coordinate(i);bool left=Wet(cell+Vector3Int.left),right=Wet(cell+Vector3Int.right),up=Wet(cell+Vector3Int.up),down=Wet(cell+Vector3Int.down);
        int col=!left?0:!right?2:1,row=!up?0:!down?2:1;return row*3+col;
    }
    private bool Wet(Vector3Int cell)=>Simulation.IsWet(Index(cell));
    private void ConfigureTreeCollider(GameObject plant,int i,int stage)
    {
        var box=plant.GetComponent<BoxCollider2D>();if(box==null)box=plant.AddComponent<BoxCollider2D>();
        var scale=plant.transform.lossyScale;
        box.isTrigger=false;box.size=new Vector2((stage==3?.48f:.32f)/Mathf.Abs(scale.x),.28f/Mathf.Abs(scale.y));
        box.offset=new Vector2(0,(Position(i).y-plant.transform.position.y)/scale.y);
    }
    private void RefreshBoundary()
    {
        for(int x=-1;x<=Simulation.Width;x++)
        {walking.SetTile(Origin+new Vector3Int(x,-1,0),walkingTile);walking.SetTile(Origin+new Vector3Int(x,Simulation.Height,0),walkingTile);}
        for(int y=0;y<Simulation.Height;y++)
        {walking.SetTile(Origin+new Vector3Int(-1,y,0),walkingTile);walking.SetTile(Origin+new Vector3Int(Simulation.Width,y,0),walkingTile);}
    }
    private void RefreshBridges()
    {
        foreach(var span in bridges)
        {
            bool available=span.cells!=null&&span.cells.Length>0;
            if(span.cells!=null)foreach(var coordinate in span.cells)
            {int i=Index(coordinate);if(!Simulation.Valid(i)||Simulation.Cells[i].Water>settings.bridgeFloodWater)available=false;}
            if(span.cells!=null)foreach(var coordinate in span.cells){int i=Index(coordinate);if(i>=0)Simulation.Cells[i].Bridge=available;}
            if(span.view!=null)span.view.SetActive(available);
        }
    }
    private void EnsurePlayerSafe()
    {
        int i=Index(ground.WorldToCell(player.Position));bool unsafePosition=!Simulation.Walkable(i);
        if(!unsafePosition&&plants.TryGetValue(i,out var plant))
        {
            var box=plant.GetComponent<BoxCollider2D>();
            if(box!=null){var bounds=box.bounds;bounds.Expand(new Vector3(.36f,.36f,0));unsafePosition=bounds.Contains(new Vector3(player.Position.x,player.Position.y,bounds.center.z));}
        }
        if(!unsafePosition){Simulation.CharacterCell=i;return;}
        Vector3 local=ground.transform.InverseTransformPoint(player.Position);int safe=Simulation.NearestSafeLand(local.x-Origin.x,local.y-Origin.y);
        if(safe<0){Message="附近没有可落脚的地面；排水后恢复通行。";return;}
        player.Teleport(Position(safe));Simulation.CharacterCell=safe;Physics2D.SyncTransforms();
        Message="脚下地形已不可通行，已移到最近的安全地面。";
    }
}
