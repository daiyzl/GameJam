#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Verification is opt-in: an installer places a one-shot request outside Assets.
[InitializeOnLoad]
public static class V1EditorBridge
{
    private const string Key="RiverbankV1.Presentation.";
    private static string RequestPath=>Path.Combine(Application.dataPath,"../EmergenceV1Verification/request.json");
    [Serializable] private class Request{public string outputDirectory="";public bool eventsOnly=false;}
    [Serializable] private class Report
    {
        public string status,scenePath,checks,error;
        public int cellCount,regionCells,phases,day,finalPopulation,villageTier,farmBirths,wildMoves,treeBirths,forestTransitions,runtimeErrors;
        public float prosperity,movementDistance,riverCollisionX,bridgeCrossingX;
        public bool manualTime,threePhaseCycle,densityRule,playerTreeMature,teachingWin,hoverPopulation;
        public double maximumEnvironmentMilliseconds,profilerMaximumMilliseconds;
        public bool scheduledRain;
        public bool acceptanceRegressions;
        public bool waterBridgeFirePolish;
        public bool villageRegionsAndContinuity;
        public bool terrainFireFloodGold;
        public bool connectedTerrainAndOre;
        public bool springBirdMigration, seedCapacity;
        public bool birdSprites, fullMapRain, seasonalEventViews, droughtFire, barePollutionCleanup;
        public int longRunMaxFlooded,longRunFinalFlooded,longRunDay;
        public string profilerPath;
    }
    static V1EditorBridge()
    {
        EditorApplication.update+=Update;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetString(Key+"phase","")!=""&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))SessionState.SetInt(Key+"errors",SessionState.GetInt(Key+"errors",0)+1);};
    }
    private static void Require(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
    private static void Phase(string phase){SessionState.SetString(Key+"phase",phase);SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+180);SessionState.SetFloat(Key+"time",Time.time);}
    private static float Elapsed()=>Time.time-SessionState.GetFloat(Key+"time",0);
    private static string Output(string name)=>Path.Combine(SessionState.GetString(Key+"output",""),name);
    private static void Start()
    {
        SessionState.SetBool(Key+"profilerWasEnabled",UnityEditorInternal.ProfilerDriver.enabled);SessionState.SetBool(Key+"profileEditorWasEnabled",UnityEditorInternal.ProfilerDriver.profileEditor);SessionState.SetBool(Key+"cpuWasEnabled",UnityEditorInternal.ProfilerDriver.IsAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU));
        UnityEditorInternal.ProfilerDriver.profileEditor=true;UnityEditorInternal.ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU,true);UnityEditorInternal.ProfilerDriver.enabled=true;
        V1SceneBuilder.Open();EditorApplication.isPaused=false;var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(type);game.Show();game.maximized=false;game.Focus();Phase("waitingPlay");EditorApplication.isPlaying=true;
    }
    private static void Click(V1World world)
    {
        var recorder=UnityEngine.Profiling.Recorder.Get("V1.Environment.AdvancePhase");
        if(recorder.isValid){SessionState.SetFloat(Key+"profilerMs",Mathf.Max(SessionState.GetFloat(Key+"profilerMs",0),recorder.elapsedNanoseconds/1000000f));recorder.enabled=true;}
        int before=world.Simulation.AdvanceCount;world.hud.advanceButton.onClick.Invoke();Require(world.Simulation.AdvanceCount==before+1,"One button click must advance exactly one phase");
        SessionState.SetFloat(Key+"phaseMs",Mathf.Max(SessionState.GetFloat(Key+"phaseMs",0),(float)world.LastEnvironmentMilliseconds));
    }
    private static void Put(V1World w,Vector3Int cell,V1Element element,float amount)
    {
        w.player.Teleport(w.ground.GetCellCenterWorld(cell)+Vector3.down*.7f);w.Select(element);w.SetAmount(amount);Require(w.Place(cell),"Legal near-character placement failed: "+cell+" / "+element);
    }
    private static void Support(V1World w)
    {
        var s=w.Simulation;
        var plot=new Vector3Int(7,6,0);var p=w.ReadCell(plot);
        if(p.Water<2)Put(w,plot,V1Element.Water,1);if(p.Earth<6)Put(w,plot,V1Element.Earth,4);
        var towns=new System.Collections.Generic.List<V1Village>(s.Villages);
        foreach(var v in towns)if(v.active)for(int dy=V1Simulation.RegionMin(v.tier);dy<=V1Simulation.RegionMax(v.tier);dy++)for(int dx=V1Simulation.RegionMin(v.tier);dx<=V1Simulation.RegionMax(v.tier);dx++)
        {
            int i=s.Index(v.cell%s.Width+dx,v.cell/s.Width+dy);if(!s.Valid(i)||s.IsBuilding(i)||s.Cells[i].River||s.Cells[i].Mine==V1Mine.Active)continue;
            var cell=w.Coordinate(i);if(s.Cells[i].Earth<6)Put(w,cell,V1Element.Earth,4);if(s.Cells[i].Water<1)Put(w,cell,V1Element.Water,1);
            if(!s.Cells[i].Tree&&s.TreeCells3x3(i)<4)Put(w,cell,V1Element.Wood,1);
        }
        w.player.Teleport(w.spawn);w.player.SetVerificationMovement(Vector2.zero);
    }
    private static BoxCollider2D Trunk(V1World w,int cell)
    {
        foreach(var box in w.GetComponentsInChildren<BoxCollider2D>())
            if(Vector2.Distance((Vector2)box.bounds.center,(Vector2)w.Position(cell))<.05f)return box;
        return null;
    }
    private static void Acceptance(V1World w)
    {
        w.ResetWorld();var s=w.Simulation;var dead=new Vector3Int(4,17,0);int d=w.Index(dead);
        Require(w.grass.GetTile(dead)==w.deadTile,"Deathland uses the deathland ground tile");
        foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())
            Require(Vector2.Distance(renderer.transform.position,w.Position(d)+Vector3.up*.3f)>.05f||!renderer.gameObject.name.StartsWith("ExposedOre"),"Deathland must not display mining/ore objects");
        Require(w.slopePrefab==null&&w.damPrefab==null,"Separate river embankment art has been removed");
        foreach(var cell in new[]{new Vector3Int(11,20,0),new Vector3Int(12,16,0)})
        {
            bool found=false;foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())
                if(renderer.gameObject.name.StartsWith("RiverEmbankment")&&Vector2.Distance(renderer.transform.position,w.ground.GetCellCenterWorld(cell))<.05f)found=true;
            Require(!found,"No river embankment sprite is generated on land or river sediment");
        }
        int tree=w.Index(new Vector3Int(6,6,0));var treeBox=Trunk(w,tree);
        Require(treeBox!=null&&treeBox.bounds.size.x<.5f&&treeBox.bounds.size.y<.3f,"Tree has a compact trunk BoxCollider2D");
        for(int x=20;x<=22;x++)
        {int i=w.Index(new Vector3Int(x,12,0));s.Cells[i].Tree=true;s.Cells[i].Wood=10;s.Cells[i].Water=1.2f;s.Cells[i].Earth=0;}
        w.RefreshDisplay();int forest=w.Index(new Vector3Int(20,12,0));var forestBox=Trunk(w,forest);
        Require(forestBox!=null&&forestBox.bounds.size.x<.5f&&!w.walking.HasTile(w.Coordinate(forest)),"Forest uses a partial trunk collider, not a full tile blocker");
        var playerCollider=w.player.GetComponent<Collider2D>();playerCollider.enabled=false;
        try
        {
            Physics2D.SyncTransforms();
            Require(Physics2D.CircleCast(new Vector2(6.5f,5.7f),.18f,Vector2.up,1).collider==treeBox,"Real physics blocks a tree trunk");
            Require(Physics2D.CircleCast(new Vector2(20.96f,11.7f),.18f,Vector2.up,1.6f).collider==null,"Player-sized gap beside forest remains open");
            Require(Physics2D.CircleCast(new Vector2(-2.5f,4.5f),.18f,Vector2.left,3).collider!=null,"Left rock boundary blocks movement");
            Require(Physics2D.CircleCast(new Vector2(33.5f,4.5f),.18f,Vector2.right,3).collider!=null,"Right rock boundary blocks movement");
            Require(Physics2D.CircleCast(new Vector2(.5f,-.5f),.18f,Vector2.down,3).collider!=null,"Bottom rock boundary blocks movement");
            Require(Physics2D.CircleCast(new Vector2(.5f,23.5f),.18f,Vector2.up,3).collider!=null,"Top rock boundary blocks movement");
        }
        finally{playerCollider.enabled=true;}
        int middle=w.Index(new Vector3Int(21,12,0));s.Cells[middle].Fire=50;w.AdvancePhase();
        Require(s.FireSpreads>=2&&s.Cells[forest].Wood<9.9f&&s.Cells[w.Index(new Vector3Int(22,12,0))].Wood<9.9f,"Actual (20,12)-(22,12) strong forest fire spreads both ways");
        w.ResetWorld();s=w.Simulation;int flooded=w.Index(new Vector3Int(6,6,0));
        s.Cells[flooded].Water=30;w.player.Teleport(w.Position(flooded));
        int expected=s.NearestSafeLand(6.5f-w.Origin.x,6.5f-w.Origin.y);w.RefreshDisplay();
        Require(w.Index(w.ground.WorldToCell(w.player.Position))==expected&&s.SafeLanding(expected),"Flooded player teleports to nearest safe land");
        Require(w.water.HasTile(w.Coordinate(flooded))&&Trunk(w,flooded)==null,"Submerged tree sprite and collider are removed");
        Require(w.bridges.Count==2,"Existing scene bridges have registered marker spans");var span=w.bridges[0];
        foreach(var coordinate in span.cells)s.Cells[w.Index(coordinate)].Water=30;w.RefreshDisplay();
        Require(!span.view.activeSelf,"Overtopped bridge visual hides");
        foreach(var coordinate in span.cells)Require(w.walking.HasTile(coordinate)&&!s.BridgeUsable(w.Index(coordinate)),"Hidden bridge cannot remain walkable");
        foreach(var coordinate in span.cells)s.Cells[w.Index(coordinate)].Water=8;w.RefreshDisplay();Require(span.view.activeSelf,"Bridge returns when water recedes");
        foreach(var coordinate in span.cells)Require(!w.walking.HasTile(coordinate),"Restored bridge is passable");
        w.ResetWorld();w.player.SetVerificationMovement(Vector2.zero);SessionState.SetBool(Key+"acceptance",true);
    }
    private static void Polish(V1World w)
    {
        w.ResetWorld();Click(w);var pool=new Vector3Int(5,16,0);
        Require(w.Simulation.IsWet(w.Index(pool))&&w.water.GetTile(pool)==w.waterTiles[4],"First noon pool (5,16) uses water_center");
        var polluted=new Vector3Int(13,20,0);int p=w.Index(polluted);w.Simulation.Cells[p].Pollution=45;w.RefreshDisplay();
        Require(w.water.GetTile(polluted)!=w.pollutedWaterTile&&w.water.GetColor(polluted).r<.9f,"Pollution tints real water instead of replacing it with a land sprite");
        var shader=w.water.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sharedMaterial.shader;
        Require(shader.name=="RiverbankV1/FlowingWater"&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Water ripple shader: "+shader.name+", supported="+shader.isSupported+", error="+ShaderUtil.ShaderHasError(shader));
        w.ResetWorld();foreach(var span in w.bridges)
        {
            var renderer=span.view.GetComponent<SpriteRenderer>();var bounds=renderer.bounds;
            Require(renderer.sprite.name.StartsWith("Bridge")&&span.view.transform.eulerAngles.z<.01f,"Bridge deck has orthographic, horizontal artwork");
            foreach(var cell in span.cells)
            {var cellCenter=w.ground.GetCellCenterWorld(cell);Require(bounds.Contains(cellCenter)&&w.Simulation.BridgeUsable(w.Index(cell)),"Displayed deck covers every passable bridge marker");}
            Require(Mathf.Abs(bounds.size.x-2)<.01f&&Mathf.Abs(bounds.size.y-2)<.01f,"Deck rectangle exactly matches 2x2 passable cells");
        }
        var dead=new Vector3Int(4,17,0);bool found=false;
        foreach(var effect in w.GetComponentsInChildren<V1DeadLandEffect>())if(Vector2.Distance(effect.transform.position,w.ground.GetCellCenterWorld(dead))<.05f)
        {found=true;Require(effect.GetComponent<ParticleSystem>().main.maxParticles<=24,"Deathland particles have bounded count");}
        Require(found,"Initial deathland has a dust particle effect");
        w.Simulation.Cells[w.Index(dead)].Gold=0;w.RefreshDisplay();
        foreach(var effect in w.GetComponentsInChildren<V1DeadLandEffect>())Require(Vector2.Distance(effect.transform.position,w.ground.GetCellCenterWorld(dead))>.05f,"Recovered deathland removes particle emitter immediately");
        w.ResetWorld();var s=w.Simulation;int center=w.Index(new Vector3Int(21,12,0));
        for(int y=10;y<=14;y++)for(int x=19;x<=23;x++)
        {int i=w.Index(new Vector3Int(x,y,0));s.Cells[i]=new V1Cell{Tree=true,Wood=6,Height=2.8f,SoilBody=30};}
        s.Cells[center].Fire=1;for(int phase=0;phase<5;phase++)Click(w);
        int damaged=0;for(int y=10;y<=14;y++)for(int x=19;x<=23;x++)if(s.Cells[w.Index(new Vector3Int(x,y,0))].Wood<5.9f)damaged++;
        Require(damaged==1&&s.FireSpreads==0&&s.Cells[center].Fire==0,"Actual Unity: fire 1 on wood 6 stays local and dies after 5 phases in connected dry trees");
        w.ResetWorld();w.player.SetVerificationMovement(Vector2.zero);SessionState.SetBool(Key+"polish",true);
    }
    private static void VillageAcceptance(V1World w)
    {
        w.ResetWorld();var s=w.Simulation;var original=s.Villages[0];
        for(int p=0;p<18;p++)
        {
            Click(w);
            Require(s.VillageById(original.id)==original,"Day-six village keeps its identity");
            foreach(var a in s.Villages)foreach(var b in s.Villages)
                if(a.active&&b.active&&a!=b)Require(!s.InRegion(a.cell,b.cell)&&!s.InRegion(b.cell,a.cell),"Village generation must not enter an existing tier region");
        }
        File.WriteAllText(Output("day-six-village.txt"),"Initial village #"+original.id+", active="+original.active+", population="+original.population+", tier="+original.tier+", migrations="+original.migrations+", at="+w.Coordinate(original.cell)+", cause="+original.condition);
        w.ResetWorld();s=w.Simulation;original=s.Villages[0];
        for(int tier=1;tier<=3;tier++)
        {
            original.tier=tier;w.RefreshDisplay();int cells=(tier+5)*(tier+5);
            Require(s.ReadRegion(original.cell).cells==cells&&w.hud.VillageText(original).Contains("采材／统计 "+cells+" 格"),"Actual village HUD shows tier region "+cells);
            Require(w.FindHoveredVillage(w.Position(original.cell)+Vector3.up*.6f)==original,"Rendered village remains selectable at each tier");
        }
        w.ResetWorld();s=w.Simulation;original=s.Villages[0];int from=original.cell;
        original.population=22;original.tier=2;original.housing=36;original.timber=0;original.badPhases=3;
        Click(w);
        Require(original.migrations==1&&original.cell!=from&&original.tier==2&&original.region.cells==49&&s.Villages.Count==1,"Material-shortage migration preserves medium village identity and size");
        Require(w.FindHoveredVillage(w.Position(original.cell)+Vector3.up*.6f)==original&&w.hud.VillageText(original).Contains("已迁移"),"Migrated village renderer and readout follow its new location");
        w.ResetWorld();SessionState.SetBool(Key+"villages",true);
    }
    private static void TerrainAcceptance(V1World w)
    {
        w.ResetWorld();var s=w.Simulation;int[] counts=new int[3];
        Require(w.terrainPrefabs!=null&&w.terrainPrefabs.Length==3,"Three terrain SpriteRenderer prefabs are configured");
        foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())
            for(int stage=0;stage<3;stage++)if(renderer.name.StartsWith(w.terrainPrefabs[stage].name))counts[stage]++;
        Require(counts[0]>0&&counts[1]>0&&counts[2]>0,"Actual Perlin map displays plains, slopes and mountains");
        File.WriteAllText(Output("terrain-counts.txt"),"plain="+counts[0]+", slope="+counts[1]+", mountain="+counts[2]);
        for(int x=20;x<=22;x++)
        {int i=w.Index(new Vector3Int(x,12,0));s.Cells[i].Tree=true;s.Cells[i].Wood=8;s.Cells[i].Water=4;s.Cells[i].Earth=0;s.Cells[i].Grass=0;}
        w.RefreshDisplay();Put(w,new Vector3Int(21,12,0),V1Element.Fire,8);
        for(int p=0;p<3;p++)Click(w);
        Require(!s.Cells[w.Index(new Vector3Int(21,12,0))].Tree&&s.FireSpreads>=2,"Ordinary tool fire burns a moist forest and ignites both adjacent forests");
        w.ResetWorld();s=w.Simulation;int ore=w.Index(new Vector3Int(18,8,0));float gold=s.Cells[ore].Gold;
        s.Cells[ore].Water=30;w.RefreshDisplay();bool shown=false;
        foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())
            if((renderer.name.StartsWith("PlainGoldVein")||renderer.name.StartsWith("ExposedOre"))&&Vector2.Distance(renderer.transform.position,w.Position(ore)+Vector3.up*.3f)<.05f)
                shown=renderer.enabled&&renderer.color.a>0&&renderer.color.a<1;
        Require(shown&&s.Cells[ore].Gold==gold&&s.Cells[ore].Mine==V1Mine.Active,"Submerged mine remains visible and keeps gold");
        w.ResetWorld();SessionState.SetBool(Key+"terrain",true);
    }
    private static SpriteRenderer At(V1World w,int cell,string prefix)
    {
        foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())
            if(renderer.name.StartsWith(prefix)&&Vector2.Distance(renderer.transform.position,w.Position(cell)+Vector3.up*.3f)<.05f)return renderer;
        return null;
    }
    private static void PresentationAcceptance(V1World w)
    {
        w.ResetWorld();var s=w.Simulation;var shader=w.terrainMaterial.shader;
        Require(shader.name=="RiverbankV1/ConnectedTerrain"&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Connected terrain shader compiles on the actual renderer");
        foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())if(renderer.name.StartsWith("Connected"))
        {
            Require(renderer.sharedMaterial.shader==shader&&Math.Abs(renderer.bounds.size.x-1)<.01f&&Math.Abs(renderer.bounds.size.y-1)<.01f,"Ground sprites join as full square surfaces");
            var heights=renderer.sharedMaterial.GetTexture("_HeightMap") as Texture2D;
            Require(heights!=null&&heights.filterMode==FilterMode.Bilinear&&heights.width==s.Width,"Ground material receives the shared bilinear height field");
        }
        foreach(var span in w.bridges)foreach(var coordinate in span.cells)s.Cells[w.Index(coordinate)].Deposited=20;
        w.RefreshDisplay();foreach(var renderer in w.GetComponentsInChildren<SpriteRenderer>())
            Require(!renderer.name.StartsWith("RiverEmbankment"),"Even bridge sediment cannot create an embankment view");
        int high=w.Index(new Vector3Int(22,11,0)),plain=w.Index(new Vector3Int(23,11,0));
        s.Cells[high].Height=5;s.Cells[high].Gold=30;s.Cells[high].Mine=V1Mine.Active;s.Cells[high].Water=1;
        s.Cells[plain].Height=2.8f;s.Cells[plain].Gold=30;s.Cells[plain].Mine=V1Mine.Active;s.Cells[plain].Water=1;
        w.RefreshDisplay();
        Require(At(w,high,"ExposedOre")?.sprite.name=="gold_ore_exposed","Highland mine uses exposed ore instead of a second mountain");
        Require(At(w,plain,"PlainGoldVein")!=null,"Plain mine uses the new flat gold vein");
        s.Cells[high].Gold=0;s.Cells[high].Mine=V1Mine.Exhausted;w.RefreshDisplay();
        Require(At(w,high,"ExposedOre")==null&&At(w,high,"AbandonedMine")==null,"Exhausted highland clears mineral overlays and restores ground");
        s.Cells[plain].Height=4.8f;w.RefreshDisplay();
        Require(At(w,plain,"PlainGoldVein")==null&&At(w,plain,"ExposedOre")!=null,"Changing terrain height switches the mine's visual type");
        w.ResetWorld();SessionState.SetBool(Key+"presentation",true);
    }
    private static void Update()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        try
        {
            string phase=SessionState.GetString(Key+"phase","");
            if(phase=="")
            {
                if(!File.Exists(RequestPath))return;var req=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));Directory.CreateDirectory(req.outputDirectory);SessionState.SetString(Key+"output",req.outputDirectory);SessionState.SetBool(Key+"eventsOnly",req.eventsOnly);File.Delete(RequestPath);SessionState.SetInt(Key+"errors",0);SessionState.SetFloat(Key+"phaseMs",0);SessionState.SetFloat(Key+"profilerMs",0);
                if(EditorApplication.isPlaying){Phase("exitPlay");EditorApplication.isPlaying=false;return;}Start();return;
            }
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))throw new TimeoutException("Verification timeout: "+phase);
            if(phase=="exitPlay"){if(!EditorApplication.isPlayingOrWillChangePlaymode)Start();return;}
            if(!EditorApplication.isPlaying)return;var w=UnityEngine.Object.FindObjectOfType<V1World>();if(w==null||w.Simulation==null)return;var s=w.Simulation;
            if(phase=="waitingPlay")
            {
                SessionState.SetBool(Key+"seasonalWasEnabled",w.settings.seasonalEvents);SessionState.SetBool(Key+"dryWasEnabled",w.settings.dryIgnition);
                w.settings.seasonalEvents=false;w.settings.dryIgnition=false;
                w.ResetWorld();s=w.Simulation;
                Require(w.birdSprites!=null&&w.birdSprites.Length==4,"Four actual swallow Sprite assets are assigned");
                int plot=w.Index(new Vector3Int(7,6,0));float earth=s.Cells[plot].Earth;
                for(int n=0;n<5;n++)Require(s.PlaceElement(plot,V1Element.Wood),"Seed placement in actual world");
                Require(s.Cells[plot].Seeds==2&&s.Cells[plot].Earth==earth+1.5f,"Actual world seed cap and exact excess conversion");
                s.Season=V1Season.Spring;float chance=s.Settings.springBirdMigrationChance;
                try{s.Settings.springBirdMigrationChance=1;Click(w);}finally{s.Settings.springBirdMigrationChance=chance;}
                var flock=w.GetComponentInChildren<V1BirdFlockView>();
                Require(s.BirdMigrations==1&&s.BirdLandings.Count>=4&&flock!=null&&flock.VisibleBirdCount==s.BirdLandings.Count,"Spring migration creates the actual visible flock");
                foreach(var landing in s.BirdLandings)Require(s.BirdCanLand(landing.cell)&&s.Cells[landing.cell].Seeds<=2,"Birds land on eligible cells with bounded seeds");
                SessionState.SetInt(Key+"birdStep",s.AdvanceCount);SessionState.SetInt(Key+"birdSeeds",s.BirdSeeds);
                SessionState.SetFloat(Key+"birdCameraSize",w.worldCamera.orthographicSize);
                w.worldCamera.GetComponent<V1Camera>().enabled=false;w.worldCamera.orthographicSize=5;
                w.worldCamera.transform.position=w.Position(s.BirdLandings[0].cell)+Vector3.back*10;
                Phase("birdView");return;
            }
            if(phase=="birdView"&&Elapsed()>2.9f)
            {
                Require(s.AdvanceCount==SessionState.GetInt(Key+"birdStep",0)&&s.BirdSeeds==SessionState.GetInt(Key+"birdSeeds",0),"Bird animation does not advance time or deliver seeds again");
                ScreenCapture.CaptureScreenshot(Output("spring-birds.png"));Phase("birdCaptured");return;
            }
            if(phase=="birdCaptured"&&Elapsed()>.3f)
            {
                w.worldCamera.orthographicSize=SessionState.GetFloat(Key+"birdCameraSize",7);w.worldCamera.GetComponent<V1Camera>().enabled=true;
                w.ResetWorld();Require(w.GetComponentInChildren<V1BirdFlockView>().VisibleBirdCount==0,"Reset clears flock views");
                SessionState.SetBool(Key+"birds",true);
                w.worldCamera.GetComponent<V1Camera>().enabled=false;w.worldCamera.orthographicSize=15;w.worldCamera.transform.position=new Vector3(15.5f,11.5f,-10);
                w.ScheduleRain();Click(w);Require(w.weather.IsRaining&&Math.Abs(w.Simulation.Ledger.WaterInput-RainInput(w))<.01,"Rain input covers all unblocked map cells");
                var view=w.GetComponentInChildren<V1EventView>();Require(view.RainArea.x>=s.Width&&view.RainArea.y>=s.Height,"Rain emitter covers the full map");
                Phase("eventRain");return;
            }
            if(phase=="eventRain"&&Elapsed()>.7f)
            {
                Require(w.weather.ParticleCount>0,"Whole-map rain renders particles");ScreenCapture.CaptureScreenshot(Output("full-map-rain.png"));Phase("eventRainCaptured");return;
            }
            if(phase=="eventRainCaptured"&&Elapsed()>.2f)
            {
                w.ResetWorld();s=w.Simulation;s.Season=V1Season.Spring;Require(s.QueueEvent(V1PhaseEvent.Thunderstorm),"Spring thunderstorm can be queued");Click(w);
                Require(s.LightningCells.Count==2&&w.GetComponentInChildren<V1EventView>().LightningCount==2&&w.weather.IsRaining,"Thunderstorm shows actual strike positions and heavy rain");
                w.worldCamera.orthographicSize=6;w.worldCamera.transform.position=w.Position(s.LightningCells[0])+new Vector3(0,2,-10);Phase("eventThunder");return;
            }
            if(phase=="eventThunder"&&Elapsed()>.8f&&w.GetComponentInChildren<V1EventView>().LightningVisible)
            {
                var view=w.GetComponentInChildren<V1EventView>();view.enabled=false;
                foreach(var line in view.GetComponentsInChildren<LineRenderer>())if(line.name=="落雷")line.enabled=true;
                ScreenCapture.CaptureScreenshot(Output("spring-thunderstorm.png"));Phase("eventThunderCaptured");return;
            }
            if(phase=="eventThunderCaptured"&&Elapsed()>.2f)
            {
                w.GetComponentInChildren<V1EventView>().enabled=true;
                w.worldCamera.orthographicSize=15;w.worldCamera.transform.position=new Vector3(15.5f,11.5f,-10);
                w.ResetWorld();s=w.Simulation;s.Season=V1Season.Winter;Require(s.QueueEvent(V1PhaseEvent.Snow),"Winter snow can be queued");Click(w);
                Require(s.Cells[w.Index(new Vector3Int(7,6,0))].Snow>0&&w.GetComponentInChildren<V1EventView>().IsSnowing&&!w.weather.IsRaining,"Snow accumulates and uses distinct snow particles");Phase("eventSnow");return;
            }
            if(phase=="eventSnow"&&Elapsed()>1)
            {Require(w.GetComponentInChildren<V1EventView>().SnowParticleCount>0,"Visible snow particles exist");ScreenCapture.CaptureScreenshot(Output("winter-snow.png"));Phase("eventSnowCaptured");return;}
            if(phase=="eventSnowCaptured"&&Elapsed()>.2f)
            {
                w.worldCamera.orthographicSize=SessionState.GetFloat(Key+"birdCameraSize",7);w.worldCamera.GetComponent<V1Camera>().enabled=true;
                w.ResetWorld();s=w.Simulation;int polluted=w.Index(new Vector3Int(7,6,0));
                s.Cells[polluted].Water=0;s.Cells[polluted].Wood=0;s.Cells[polluted].Tree=false;s.Cells[polluted].Grass=0;s.Cells[polluted].Gold=0;s.Cells[polluted].Pollution=60;
                Require(s.PlaceElement(polluted,V1Element.Fire,4),"Fire placement on bare pollution");Click(w);
                Require(s.Ledger.PollutionCleaned>15&&s.Cells[polluted].Pollution<40,"Bare polluted ground is cleaned without burning vegetation");
                w.ResetWorld();s=w.Simulation;s.Season=V1Season.Summer;int dry=w.Index(new Vector3Int(20,12,0));
                s.Cells[dry].Water=0;s.Cells[dry].Height=6;s.Cells[dry].Wood=6;s.Cells[dry].Tree=true;s.Cells[dry].DryPhases=20;
                Require(s.QueueEvent(V1PhaseEvent.Drought),"Summer drought can be queued");Click(w);Require(s.DryFireRisk(dry)>0,"Drought raises actual land fire risk");
                SessionState.SetBool(Key+"phaseEvents",true);w.ResetWorld();
                if(SessionState.GetBool(Key+"eventsOnly",false))
                {
                    Require(SessionState.GetInt(Key+"errors",0)==0,"No runtime errors in event playback");
                    var report=new Report{status="passed",scenePath=V1SceneBuilder.ScenePath,checks="Actual Unity Play: bird Sprite animation, full-map rain/input, spring lightning, winter snowfall/cover, summer dry fire risk, bare-ground pollution cleanup, no animation-driven ecological steps",runtimeErrors=0,birdSprites=true,fullMapRain=true,seasonalEventViews=true,droughtFire=true,barePollutionCleanup=true,springBirdMigration=true,seedCapacity=true,manualTime=true};
                    File.WriteAllText(Output("report.json"),JsonUtility.ToJson(report,true));Phase("restoreEditor");return;
                }
                Phase("legacyChecks");return;
            }
            if(phase=="legacyChecks")
            {
                Time.timeScale=1;EditorApplication.isPaused=false;UnityEditorInternal.ProfilerDriver.profileEditor=true;UnityEditorInternal.ProfilerDriver.enabled=true;
                Acceptance(w);Polish(w);VillageAcceptance(w);TerrainAcceptance(w);PresentationAcceptance(w);s=w.Simulation;
                Require(s.Cells.Length==1120&&w.Origin==new Vector3Int(-4,-2,0),"40x28 authored Tilemap including negative origin");Require(s.ReadRegion(s.Villages[0].cell).cells==36&&s.NearRiver(s.Villages[0].cell),"Riverside village must read 36 cells");
                Require(w.FindHoveredVillage(w.Position(s.Villages[0].cell)+Vector3.up*.6f)==s.Villages[0]&&w.hud.VillageText(s.Villages[0]).Contains("8 人"),"Hover population readout");
                SessionState.SetFloat(Key+"water",s.Cells[0].Water);w.player.SetVerificationMovement(Vector2.zero);
                Click(w);w.player.Teleport(new Vector2(7.5f,13.5f));SessionState.SetFloat(Key+"cameraSize",w.worldCamera.orthographicSize);
                w.worldCamera.GetComponent<V1Camera>().enabled=false;w.worldCamera.orthographicSize=5.5f;w.worldCamera.transform.position=new Vector3(8.5f,17,-10);Phase("polishView");return;
            }
            if(phase=="polishView"&&Elapsed()>1.5f)
            {Require(w.GetComponentsInChildren<ParticleSystem>().Length>0,"Deathland particle systems exist in Play");ScreenCapture.CaptureScreenshot(Output("water-deathland-bridge.png"));Phase("polishCaptured");return;}
            if(phase=="polishCaptured"&&Elapsed()>.3f)
            {w.worldCamera.orthographicSize=SessionState.GetFloat(Key+"cameraSize",7);w.worldCamera.GetComponent<V1Camera>().enabled=true;w.ResetWorld();w.player.SetVerificationMovement(Vector2.zero);Phase("frozen");return;}
            if(phase=="frozen"&&Elapsed()>2)
            {
                Require(s.AdvanceCount==0&&s.Cells[0].Water==SessionState.GetFloat(Key+"water",0)&&s.Villages[0].population==8,"Wall-clock must not advance environment");
                ScreenCapture.CaptureScreenshot(Output("morning.png"));SessionState.SetFloat(Key+"startX",w.player.Position.x);w.player.SetVerificationMovement(Vector2.right);Phase("moving");return;
            }
            if(phase=="moving"&&Elapsed()>.35f)
            {
                float distance=w.player.Position.x-SessionState.GetFloat(Key+"startX",0);Require(distance>.5f&&s.AdvanceCount==0,"Character moves without advancing time");SessionState.SetFloat(Key+"movement",distance);
                w.player.Teleport(new Vector2(11.4f,13.5f));w.player.SetVerificationMovement(Vector2.right);Phase("river");return;
            }
            if(phase=="river"&&Elapsed()>.5f)
            {
                float x=w.player.Position.x;Require(x>11.6f&&x<11.87f,"River collision: "+x);SessionState.SetFloat(Key+"collision",x);w.player.Teleport(new Vector2(11.5f,4.5f));w.player.SetVerificationMovement(Vector2.right);Phase("bridge");return;
            }
            if(phase=="bridge"&&Elapsed()>1)
            {
                float x=w.player.Position.x;Require(x>14.2f,"Bridge crossing: "+x);SessionState.SetFloat(Key+"bridge",x);w.player.Teleport(w.spawn);w.player.SetVerificationMovement(Vector2.zero);Click(w);Require(s.Phase==1&&s.Day==1,"Morning to noon");Phase("noon");return;
            }
            if(phase=="noon"&&Elapsed()>.4f){Require(w.hud.timeImage.sprite==w.timeBackgrounds[1],"Noon background must switch");ScreenCapture.CaptureScreenshot(Output("noon.png"));Phase("noonCaptured");return;}
            if(phase=="noonCaptured"&&Elapsed()>.2f){Click(w);Require(s.Phase==2,"Noon to evening");Phase("evening");return;}
            if(phase=="evening"&&Elapsed()>.4f){Require(w.hud.timeImage.sprite==w.timeBackgrounds[2],"Evening background must switch");ScreenCapture.CaptureScreenshot(Output("evening.png"));Phase("eveningCaptured");return;}
            if(phase=="eveningCaptured"&&Elapsed()>.2f){Click(w);Require(s.Day==2&&s.Phase==0,"Evening to next morning");w.hud.toolsButton.onClick.Invoke();Require(w.ToolsOpen&&w.hud.toolsPage.activeSelf,"Independent tools page");SessionState.SetInt(Key+"step",s.AdvanceCount);Phase("tools");return;}
            if(phase=="tools"&&Elapsed()>.5f)
            {
                Require(!w.hud.advanceButton.interactable,"Advance unavailable while tools page is open");w.hud.advanceButton.onClick.Invoke();Require(s.AdvanceCount==SessionState.GetInt(Key+"step",0),"Tools page cannot accidentally advance");ScreenCapture.CaptureScreenshot(Output("tools.png"));Phase("toolsCaptured");return;
            }
            if(phase=="toolsCaptured"&&Elapsed()>.2f)
            {
                w.hud.elementButtons[1].onClick.Invoke();Require(!w.ToolsOpen&&w.Selected==V1Element.Wood,"Selecting seed tool closes page");w.ResetWorld();w.player.SetVerificationMovement(Vector2.zero);
                var demo=new Vector3Int(23,8,0);Require(w.Simulation.TreeCells3x3(w.Index(demo))==4,"Density demo has four tree cells");Put(w,demo,V1Element.Wood,1);Click(w);Require(!w.ReadCell(demo).Tree&&w.ReadCell(demo).Seeds==1,"Four tree cells prevent establishment");
                var plot=new Vector3Int(7,6,0);Put(w,plot,V1Element.Wood,1);Put(w,plot,V1Element.Water,1);Put(w,plot,V1Element.Earth,4);Click(w);Require(w.ReadCell(plot).Tree&&w.Simulation.PlayerTreeBirths>0,"Supported player seed must sprout");SessionState.SetInt(Key+"loops",0);Phase("grow");return;
            }
            if(phase=="grow")
            {
                Support(w);Click(w);int loops=SessionState.GetInt(Key+"loops",0)+1;SessionState.SetInt(Key+"loops",loops);
                if(!w.LevelWon||s.Villages[0].tier<3){Require(loops<40,"Full tutorial did not reach prosperity/large village: stage "+s.TutorialStage+", prosperity "+s.Prosperity);return;}
                Require(s.ForestTransitions>0&&s.FarmBirths>0&&s.WildlifeMoves>0&&s.Ledger.GoldMined>1&&s.Ledger.TimberHarvested>1,"Tree stages, animal ecology and real material production");
                var report=new Report{status="passed",scenePath=V1SceneBuilder.ScenePath,checks="Actual Unity Play: stationary manual time, real character movement and river/bridge collision, three backgrounds and next day, clickable Canvas tools and phase button, density rejects four occupied cells, legal player seed grows, actual mining/harvesting, farm breeding and wild migration, full six-stage teaching prosperity win, restart",cellCount=1120,regionCells=36,phases=s.AdvanceCount,day=s.Day,finalPopulation=s.TotalPopulation,villageTier=s.Villages[0].tier,farmBirths=s.FarmBirths,wildMoves=s.WildlifeMoves,treeBirths=s.TreeBirths,forestTransitions=s.ForestTransitions,prosperity=s.Prosperity,movementDistance=SessionState.GetFloat(Key+"movement",0),riverCollisionX=SessionState.GetFloat(Key+"collision",0),bridgeCrossingX=SessionState.GetFloat(Key+"bridge",0),manualTime=true,threePhaseCycle=true,densityRule=true,playerTreeMature=true,teachingWin=true,hoverPopulation=true};
                report.runtimeErrors=SessionState.GetInt(Key+"errors",0);report.maximumEnvironmentMilliseconds=SessionState.GetFloat(Key+"phaseMs",0);report.profilerMaximumMilliseconds=SessionState.GetFloat(Key+"profilerMs",0);
                SessionState.SetString(Key+"report",JsonUtility.ToJson(report,true));w.player.Teleport(w.spawn);w.Select(V1Element.Wood);w.SetAmount(1);Phase("won");return;
            }
            if(phase=="won"&&Elapsed()>.8f){ScreenCapture.CaptureScreenshot(Output("prosperous.png"));Phase("reset");return;}
            if(phase=="reset"&&File.Exists(Output("prosperous.png")))
            {
                w.hud.restartButton.onClick.Invoke();Require(w.Simulation.AdvanceCount==0&&w.Simulation.TotalPopulation==8,"Restart restores initial state");w.player.SetVerificationMovement(Vector2.zero);Phase("finish");return;
            }
            if(phase=="finish"&&Elapsed()>.7f)
            {
                w.worldCamera.GetComponent<V1Camera>().enabled=false;SessionState.SetFloat(Key+"cameraSize",w.worldCamera.orthographicSize);
                w.worldCamera.orthographicSize=15;w.worldCamera.transform.position=new Vector3(15.5f,11.5f,-10);Phase("terrainView");return;
            }
            if(phase=="terrainView"&&Elapsed()>.7f)
            {ScreenCapture.CaptureScreenshot(Output("perlin-terrain.png"));Phase("terrainCaptured");return;}
            if(phase=="terrainCaptured"&&Elapsed()>.3f)
            {
                UnityEditorInternal.ProfilerDriver.enabled=false;SessionState.SetInt(Key+"longMaxFlood",0);Phase("longRiver");return;
            }
            if(phase=="longRiver")
            {
                for(int p=0;p<25&&s.AdvanceCount<2250;p++)
                {Click(w);SessionState.SetInt(Key+"longMaxFlood",Math.Max(SessionState.GetInt(Key+"longMaxFlood",0),s.FloodCells));}
                if(s.AdvanceCount<2250)return;
                int land=0;foreach(var c in s.Cells)if(!c.Blocked&&!c.River)land++;
                Require(s.FloodCells<land*.1f&&SessionState.GetInt(Key+"longMaxFlood",0)<land*.15f,"Actual 750-day river run preserves dry terrain");
                var report=JsonUtility.FromJson<Report>(SessionState.GetString(Key+"report",""));report.longRunDay=s.Day;report.longRunMaxFlooded=SessionState.GetInt(Key+"longMaxFlood",0);report.longRunFinalFlooded=s.FloodCells;
                SessionState.SetString(Key+"report",JsonUtility.ToJson(report,true));File.WriteAllText(Output("long-river.txt"),"day="+s.Day+", max flooded="+report.longRunMaxFlooded+"/"+land+", final="+s.FloodCells);Phase("longRiverView");return;
            }
            if(phase=="longRiverView"&&Elapsed()>.7f)
            {ScreenCapture.CaptureScreenshot(Output("day-751-terrain.png"));Phase("longRiverCaptured");return;}
            if(phase=="longRiverCaptured"&&Elapsed()>.3f)
            {
                UnityEditorInternal.ProfilerDriver.enabled=true;w.worldCamera.orthographicSize=SessionState.GetFloat(Key+"cameraSize",7);w.worldCamera.GetComponent<V1Camera>().enabled=true;
                w.ResetWorld();w.ScheduleRain();Require(w.Simulation.AdvanceCount==0&&w.Simulation.Ledger.WaterInput==0,"Queuing rain must not advance time or add water");Click(w);Require(w.weather.IsRaining&&Math.Abs(w.Simulation.Ledger.WaterInput-RainInput(w))<.01,"Scheduled whole-map rain plus source input");Phase("rain");return;
            }
            if(phase=="rain"&&Elapsed()>.5f)
            {
                Require(w.weather.ParticleCount>0,"Scheduled rain must show actual particles");w.ResetWorld();Require(!w.weather.IsRaining&&w.Simulation.AdvanceCount==0,"Restart clears rain and pending environment time");w.player.SetVerificationMovement(Vector2.zero);Phase("finalCapture");return;
            }
            if(phase=="finalCapture"&&Elapsed()>.7f)
            {
                Require(SessionState.GetInt(Key+"errors",0)==0,"Runtime errors logged");ScreenCapture.CaptureScreenshot(Output("level-start.png"));var report=JsonUtility.FromJson<Report>(SessionState.GetString(Key+"report",""));report.scheduledRain=true;report.acceptanceRegressions=SessionState.GetBool(Key+"acceptance",false);report.waterBridgeFirePolish=SessionState.GetBool(Key+"polish",false);report.villageRegionsAndContinuity=SessionState.GetBool(Key+"villages",false);report.terrainFireFloodGold=SessionState.GetBool(Key+"terrain",false);report.connectedTerrainAndOre=SessionState.GetBool(Key+"presentation",false);
                report.springBirdMigration=SessionState.GetBool(Key+"birds",false);report.seedCapacity=report.springBirdMigration;
                report.birdSprites=report.springBirdMigration;report.fullMapRain=report.seasonalEventViews=report.droughtFire=report.barePollutionCleanup=SessionState.GetBool(Key+"phaseEvents",false);
                int last=UnityEditorInternal.ProfilerDriver.lastFrameIndex,first=Math.Max(UnityEditorInternal.ProfilerDriver.firstFrameIndex,last-250);
                for(int f=first;f<=last;f++)using(var frame=UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f,0))if(frame.valid)
                    for(int n=0;n<frame.sampleCount;n++)if(frame.GetSampleName(n)=="V1.Environment.AdvancePhase")report.profilerMaximumMilliseconds=Math.Max(report.profilerMaximumMilliseconds,frame.GetSampleTimeMs(n));
                report.profilerPath=last>=0?Output("v1-profiler.data"):"";if(last>=0)UnityEditorInternal.ProfilerDriver.SaveProfile(report.profilerPath);File.WriteAllText(Output("report.json"),JsonUtility.ToJson(report,true));Phase("restoreEditor");return;
            }
            if(phase=="restoreEditor"&&Elapsed()>.3f){RestoreProfiler();RestoreEventSettings(w);w.ResetWorld();var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var game=EditorWindow.GetWindow(type);game.maximized=false;game.Show();game.Focus();SessionState.SetString(Key+"phase","");w.player.ReleaseVerificationMovement();Debug.Log("河岸繁荣 V1：真实Play验证通过，已恢复第1天早晨，等待点击渡过时段。");}
        }
        catch(Exception e){RestoreProfiler();string output=SessionState.GetString(Key+"output","");if(output!="")File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(new Report{status="failed",error=e.ToString()},true));SessionState.SetString(Key+"phase","");Debug.LogException(e);}
    }
    private static void RestoreProfiler(){UnityEditorInternal.ProfilerDriver.enabled=SessionState.GetBool(Key+"profilerWasEnabled",false);UnityEditorInternal.ProfilerDriver.profileEditor=SessionState.GetBool(Key+"profileEditorWasEnabled",false);UnityEditorInternal.ProfilerDriver.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU,SessionState.GetBool(Key+"cpuWasEnabled",true));}
    private static void RestoreEventSettings(V1World w){w.settings.seasonalEvents=SessionState.GetBool(Key+"seasonalWasEnabled",true);w.settings.dryIgnition=SessionState.GetBool(Key+"dryWasEnabled",true);}
    private static double RainInput(V1World w){double input=0;for(int i=0;i<w.Simulation.Cells.Length;i++)if(w.Simulation.Valid(i)){input+=w.settings.rainWater;if(w.Simulation.Cells[i].Source)input+=w.settings.sourceRate*w.settings.phaseSeconds;}return input;}
}
#endif
