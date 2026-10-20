using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-100)]
public class TutorialWorld : MonoBehaviour
{
    public Tilemap ground,plants,water,flames,metal,obstacles,walking;
    public TutorialCharacter player;
    public TutorialWeather weather;
    public LineRenderer targetOutline;
    public TileBase saplingTile,treeTile,forestTile,goldTile,walkingTile;
    public TileBase[] waterTiles,fireTiles;
    public List<TutorialCellSeed> cellSeeds=new List<TutorialCellSeed>();
    public Vector3Int[] stations={new Vector3Int(3,8,0),new Vector3Int(10,8,0),new Vector3Int(17,8,0),new Vector3Int(24,8,0)};
    public Vector3Int villageCell=new Vector3Int(2,3,0);
    public Vector2 spawn=new Vector2(2.5f,3.5f);
    public SimulationSettings rules=new SimulationSettings {StepSeconds=.75f,FlowRate=2f};
    public FiveElementSimulation Simulation {get;private set;}
    public Vector3Int Origin {get;private set;}
    public int Stage {get;private set;}
    public bool Completed {get;private set;}
    public bool Paused {get;set;}
    public bool LessonRunning {get;private set;}
    public Element SelectedElement {get;set;}=Element.Water;
    public string Message {get;private set;}="村口的小径通向四个试验点，先去黄色框标记的树苗旁。";
    public int RainPulses {get;private set;}
    public bool RainActive => rainRemaining>0;
    public Vector3Int TargetCell => Stage<4?stations[Stage]:Stage==4?stations[3]:villageCell;
    public Vector3 TargetPosition => ground.GetCellCenterWorld(TargetCell);
    public string LessonTitle => Completed?"教学完成 · 世界会自己变化":new[]{"1 / 5   滋养树苗","2 / 5   金伐木","3 / 5   水灭火","4 / 5   火燃木，留下土","5 / 5   一场雨，引发连锁变化","返回村口"}[Stage];
    public string Instruction => Completed?"你已经掌握五行的基础互动。按 R 可以重新体验。":new[]{
        "走到树苗旁，选择水（3），按 E 投放。观察木从 1 增至 3；每增加 1 木，消耗 1 土和 2 水。",
        "走到密林旁，选择金（1），按 E 投放。等待木减少到 4。金少时作用慢，持续作用会越来越强。",
        "走到燃烧的树旁，选择水（3），按 E 投放。让火熄灭，并保住树木。",
        "走到试验树旁，选择火（4），按 E 投放。观察木减少、土增加，土达到 1 即可。",
        "留在刚燃烧过的地块旁，按 Q 唤雨。雨会灭火，幸存的木利用水和灰烬再生。",
        "沿小径回到村口的黄色框，完成这次练习。"}[Stage];
    private LevelDefinition initial;
    private float accumulator;
    private bool armed,used;
    private int rainRemaining;
    private float growthBaseline,cutBaseline,waterSpentBaseline,burnBaseline,rainWood;
    private CellState rainBaseline;
    private int lastFireFrame=-1;
    private static readonly Vector2[] OutlineCorners={new Vector2(-.5f,-.5f),new Vector2(-.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,-.5f),new Vector2(-.5f,-.5f)};

    private void Awake()
    {
        Application.runInBackground=true;
        BoundsInt bounds=ground.cellBounds; Origin=bounds.min;
        if(bounds.size.x<1||bounds.size.y<1||bounds.size.x>64||bounds.size.y>64) throw new InvalidOperationException("教学地图必须在 1..64 格以内");
        initial=new LevelDefinition(bounds.size.x,bounds.size.y) {Name="村口试炼",TargetWater=float.MaxValue,StepLimit=int.MaxValue};
        for(int y=0;y<initial.Height;y++) for(int x=0;x<initial.Width;x++)
        {
            int i=initial.Index(x,y); var cell=Origin+new Vector3Int(x,y,0); initial.Blocked[i]=!ground.HasTile(cell)||obstacles.HasTile(cell);
            var g=ground.GetTile<TutorialTile>(cell); var p=plants.GetTile<TutorialTile>(cell); var w=water.GetTile<TutorialTile>(cell);
            initial.Initial[i]=new CellState {Earth=g!=null?g.earth:2,Gold=g!=null?g.gold:0,
                Wood=p!=null?p.wood:plants.HasTile(cell)?1:0,Water=w!=null?w.water:water.HasTile(cell)?6:0};
            initial.Elevation[i]=w!=null?w.elevation:g!=null?g.elevation:3;
        }
        foreach(var seed in cellSeeds)
        {
            int i=initial.Index(seed.cell.x-Origin.x,seed.cell.y-Origin.y);
            if(i<0) {Debug.LogError("教学初始配置超出地面范围："+seed.cell);continue;}
            initial.Initial[i]=seed.State;initial.Elevation[i]=seed.elevation;
        }
        ResetLevel();
    }
    private void Update()
    {
        if(Simulation==null) return;
        for(int e=0;e<5;e++) if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+e))) SelectedElement=(Element)e;
        if(Input.GetKeyDown(KeyCode.E)) Interact();
        if(Input.GetKeyDown(KeyCode.Q)) CallRain();
        if(Input.GetKeyDown(KeyCode.Space)) Paused=!Paused;
        if(Input.GetKeyDown(KeyCode.T)) RetryStation();
        if(Input.GetKeyDown(KeyCode.R)) ResetLevel();
        ArmStation();
        if(Stage==5 && Vector2.Distance(player.Position,TargetPosition)<1.6f) {Completed=true;Message="村口试炼完成：局部规则组合，产生了灭火、灰烬与再生。";}
        if(LessonRunning&&!Paused)
        {
            accumulator+=Time.deltaTime;int count=0;
            while(accumulator>=rules.StepSeconds && count++<4 && LessonRunning) {accumulator-=rules.StepSeconds;Tick();}
        }
        int frame=Mathf.FloorToInt(Time.time*8)%4;
        if(frame!=lastFireFrame) {lastFireFrame=frame;RefreshFire(frame);}
        UpdateOutline();
    }
    public int Index(Vector3Int cell) => Simulation.Level.Index(cell.x-Origin.x,cell.y-Origin.y);
    public CellState ReadCell(Vector3Int cell) => Simulation.GetCell(Index(cell));
    public Vector3Int CellAtWorld(Vector3 position) => ground.WorldToCell(position);
    private bool NearTarget(float distance=1.8f) => Vector2.Distance(player.Position,TargetPosition)<distance;
    private void ArmStation()
    {
        if(armed||Stage>3||!NearTarget(2.5f)) return;
        if(Stage==2) Simulation.AddResource(Index(TargetCell),Element.Fire,2);
        armed=true;RefreshViews();
    }
    public bool Interact()
    {
        if(Completed||Stage>=4) {Message=Stage==4?"这一站请按 Q 唤雨。":"沿小径返回村口。";return false;}
        if(!NearTarget()) {Message="请先走到黄色框的地块旁，再按 E。";return false;}
        Element required=Stage==1?Element.Gold:Stage==3?Element.Fire:Element.Water;
        if(SelectedElement!=required) {Message="这一站需要"+ElementWorld.ElementName(required)+"，先按对应数字键选择。";return false;}
        ArmStation();float amount=required==Element.Water?4:required==Element.Gold?2:1;
        if(!Simulation.AddResource(Index(TargetCell),required,amount)) return false;
        used=true;LessonRunning=true;Paused=false;Message="已投放 "+amount+" 份"+ElementWorld.ElementName(required)+"，观察环境变化。";RefreshViews();return true;
    }
    public bool CallRain()
    {
        if(Stage!=4||Completed) {Message="先完成四个试验，最后一站可以按 Q 唤雨。";return false;}
        if(!NearTarget(3)) {Message="走到刚燃烧过的试验地旁再唤雨。";return false;}
        if(rainRemaining>0) return false;
        rainRemaining=4;RainPulses=0;used=true;LessonRunning=true;Paused=false;
        if(weather!=null) weather.BeginRain(TargetPosition);
        Message="雨正在落下：同一份水会先灭火，再参与木的再生。";return true;
    }
    public void Tick()
    {
        if(rainRemaining>0)
        {
            Vector3Int center=stations[3];
            for(int dy=-2;dy<=2;dy++) for(int dx=-2;dx<=2;dx++) Simulation.AddResource(Index(center+new Vector3Int(dx,dy,0)),Element.Water,3);
            rainRemaining--;RainPulses++;
        }
        Simulation.Step();RefreshViews();EvaluateLesson();
    }
    private void EvaluateLesson()
    {
        if(!used||Stage>4) return;
        CellState c=ReadCell(TargetCell);bool success=false;
        if(Stage==0) success=c.Wood>=3 && Simulation.GrowthEarthSpent-growthBaseline>=2;
        if(Stage==1) success=c.Wood<=4 && Simulation.CutWood-cutBaseline>=3.9f;
        if(Stage==2) success=c.Fire<=.001f && c.Wood>=5 && Simulation.ExtinguishWaterSpent-waterSpentBaseline>=1.9f;
        if(Stage==3) success=c.Earth>=1 && Simulation.BurnedWood-burnBaseline>=1;
        if(Stage==4) success=c.Fire<=.001f && c.Wood>=rainWood+.9f && RainPulses>=2;
        if(!success) return;
        Stage++;LessonRunning=false;armed=false;used=false;accumulator=0;
        if(Stage==4) {rainBaseline=ReadCell(stations[3]);rainWood=rainBaseline.Wood;}
        if(Stage==5) {rainRemaining=0;if(weather!=null)weather.StopRain();}
        CaptureBaselines();Message=Stage==5?"雨让火熄灭，灰烬和水让木再生。现在返回村口。":"这一站完成，继续前往下一个黄色框。";UpdateOutline();
    }
    private void CaptureBaselines()
    {growthBaseline=Simulation.GrowthEarthSpent;cutBaseline=Simulation.CutWood;waterSpentBaseline=Simulation.ExtinguishWaterSpent;burnBaseline=Simulation.BurnedWood;}
    public void RetryStation()
    {
        if(Stage>=5) return;
        var level=new LevelDefinition(initial.Width,initial.Height) {Name=initial.Name,TargetWater=float.MaxValue,StepLimit=int.MaxValue};
        Array.Copy(initial.Blocked,level.Blocked,initial.Blocked.Length);Array.Copy(initial.Elevation,level.Elevation,initial.Elevation.Length);
        for(int i=0;i<level.Initial.Length;i++) level.Initial[i]=Simulation.GetCell(i);
        int target=Index(TargetCell);level.Initial[target]=Stage==4?rainBaseline:initial.Initial[target];
        Simulation=new FiveElementSimulation(level,rules);LessonRunning=false;Paused=false;armed=false;used=false;rainRemaining=0;accumulator=0;
        if(weather!=null)weather.StopRain();CaptureBaselines();RefreshViews();Message="本站已恢复，可以重新尝试；之前的教学进度保留。";
    }
    public void ResetLevel()
    {
        Simulation=new FiveElementSimulation(initial,rules);Stage=0;Completed=false;LessonRunning=false;Paused=false;armed=false;used=false;rainRemaining=0;RainPulses=0;accumulator=0;
        SelectedElement=Element.Water;if(player!=null)player.Teleport(spawn);if(weather!=null)weather.StopRain();CaptureBaselines();RefreshViews();UpdateOutline();
        Message="从村口出发，沿小径前往黄色框。";
    }
    private void Set(Tilemap map,Vector3Int cell,TileBase tile) {if(map.GetTile(cell)!=tile) map.SetTile(cell,tile);}
    private void RefreshViews()
    {
        for(int y=0;y<Simulation.Level.Height;y++) for(int x=0;x<Simulation.Level.Width;x++)
        {
            int i=Simulation.Level.Index(x,y);var cell=Origin+new Vector3Int(x,y,0);var c=Simulation.GetCell(i);bool blocked=Simulation.Level.Blocked[i];
            Set(plants,cell,!blocked&&c.Wood>.05f?(c.Wood<3?saplingTile:c.Wood>=8?forestTile:treeTile):null);
            Set(metal,cell,!blocked&&c.Gold>.05f?goldTile:null);
            Set(water,cell,!blocked&&c.Water>.1f?WaterView(cell):null);
            if(water.HasTile(cell)) water.SetColor(cell,new Color(1,1,1,Mathf.Clamp(.5f+c.Water*.06f,.5f,1)));
            Set(walking,cell,obstacles.HasTile(cell)||!blocked&&c.Wood>=rules.WoodBlockThreshold?walkingTile:null);
        }
        RefreshFire(Mathf.FloorToInt(Time.time*8)%4);
    }
    private TileBase WaterView(Vector3Int cell)
    {
        bool n=ReadCell(cell+Vector3Int.up).Water>.1f,s=ReadCell(cell+Vector3Int.down).Water>.1f,w=ReadCell(cell+Vector3Int.left).Water>.1f,e=ReadCell(cell+Vector3Int.right).Water>.1f;
        int index=4;if(n||s||w||e) index=!n&&!w?0:!n&&!e?2:!s&&!w?6:!s&&!e?8:!n?1:!s?7:!w?3:!e?5:4;
        return waterTiles[index];
    }
    private void RefreshFire(int frame)
    {
        if(Simulation==null||fireTiles==null||fireTiles.Length!=4)return;
        for(int y=0;y<Simulation.Level.Height;y++) for(int x=0;x<Simulation.Level.Width;x++)
        {int i=Simulation.Level.Index(x,y);Set(flames,Origin+new Vector3Int(x,y,0),Simulation.GetCell(i).Fire>.005f?fireTiles[frame]:null);}
    }
    private void UpdateOutline()
    {
        if(targetOutline==null)return;var center=TargetPosition;
        targetOutline.enabled=!Completed;targetOutline.positionCount=5;
        for(int i=0;i<5;i++)targetOutline.SetPosition(i,center+(Vector3)OutlineCorners[i]);
    }
}
