#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public static class V1SceneBuilder
{
    public const string ScenePath="Assets/Scenes/RiverbankEmergenceV1.unity";
    private const string Root="Assets/World/EmergenceV1/";
    private static Material material;
    private static Sprite Sprite(string sheet,string name=null)
    {
        string path="Assets/World/PixelArtV1/Sprites/"+sheet+".png";
        if(sheet=="rock"||sheet=="village")path="Assets/World/Tutorial/Art/"+sheet+".png";
        if(new[]{"deer","rabbit","cow","chicken"}.Contains(sheet))path="Assets/World/Settlement/Art/"+sheet+".png";
        if(new[]{"gold_mine_mountain","gold_ore_exposed","gold_mine_abandoned","bridge_wood","bridge_stone","ground_dead","pollution_fog","water_polluted","soil_slope","river_sediment_dam","village_medium","village_large"}.Contains(sheet))path="Assets/World/WorldArtExpansionV1/Sprites/"+sheet+".png";
        if(sheet.StartsWith("background_"))path="Assets/World/WorldArtExpansionV1/Backgrounds/"+sheet+".png";
        var all=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();var s=name==null?all.FirstOrDefault():all.FirstOrDefault(v=>v.name==name);
        if(s==null)throw new InvalidOperationException("Missing art "+path+" / "+name);return s;
    }
    private static TileBase Tile(string name,Sprite sprite,bool solid=false)
    {
        string path=Root+"Tiles/"+name+".asset";var t=AssetDatabase.LoadAssetAtPath<Tile>(path);if(t!=null)return t;
        t=ScriptableObject.CreateInstance<Tile>();t.name=name;t.sprite=sprite;t.colliderType=solid?UnityEngine.Tilemaps.Tile.ColliderType.Grid:UnityEngine.Tilemaps.Tile.ColliderType.None;t.flags=TileFlags.None;
        if(sprite!=null)t.transform=Matrix4x4.Scale(Vector3.one/sprite.bounds.size.x);
        AssetDatabase.CreateAsset(t,path);return t;
    }
    private static V1GroundTile Ground(string name,Sprite sprite,V1Cell c)
    {
        string path=Root+"Tiles/"+name+".asset";var t=AssetDatabase.LoadAssetAtPath<V1GroundTile>(path);if(t!=null)return t;
        t=ScriptableObject.CreateInstance<V1GroundTile>();t.sprite=sprite;t.earth=c.Earth;t.grass=c.Grass;t.water=c.Water;t.height=c.River?.2f:c.Height;t.soilBody=c.SoilBody;t.river=c.River;t.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None;t.transform=Matrix4x4.Scale(Vector3.one/sprite.bounds.size.x);AssetDatabase.CreateAsset(t,path);return t;
    }
    private static GameObject Prefab(string name,Sprite sprite,float width)
    {
        string path=Root+"Prefabs/"+name+".prefab";var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset!=null)return asset;
        var go=new GameObject(name);var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=material;go.transform.localScale=Vector3.one*(width/sprite.bounds.size.x);asset=PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);return asset;
    }
    private static Tilemap Map(Transform parent,string name,int order,bool render=true)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var m=go.AddComponent<Tilemap>();if(render){var r=go.AddComponent<TilemapRenderer>();r.sharedMaterial=material;r.sortingOrder=order;}return m;
    }
    [MenuItem("五行世界/V1/打开河岸涌现关卡")]
    public static void Open()
    {
        if(EditorApplication.isPlaying)return;
        Directory.CreateDirectory(Root+"Tiles");Directory.CreateDirectory(Root+"Prefabs");Directory.CreateDirectory("Assets/Scenes");AssetDatabase.Refresh();
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/World/PixelArtV1/Materials/PixelSprite.mat");if(material==null)throw new InvalidOperationException("Sprite material missing");
        var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)scene=File.Exists(ScenePath)?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive):Build();
        SceneManager.SetActiveScene(scene);
        for(int i=SceneManager.sceneCount-1;i>=0;i--){var other=SceneManager.GetSceneAt(i);if(other!=scene&&other.isDirty)throw new InvalidOperationException("Another scene has unsaved edits: "+other.name+". Save it before verification.");if(other!=scene)EditorSceneManager.CloseScene(other,true);}
        var world=UnityEngine.Object.FindObjectOfType<V1World>();UpgradeAcceptanceAssets(world);world.player.bodyView.transform.localScale=Vector3.one*(.8f/world.player.frames[0].bounds.size.x);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();Selection.activeGameObject=world.gameObject;
    }
    private static void UpgradeAcceptanceAssets(V1World world)
    {
        if(world.settings.ecologyRevision<1)
        {
            var current=new V1Settings();var s=world.settings;
            s.fireSpreadThreshold=current.fireSpreadThreshold;s.fireSpreadDelay=current.fireSpreadDelay;s.fireHeatTransfer=current.fireHeatTransfer;
            s.fireIgnitionHeat=current.fireIgnitionHeat;s.fireHeatDecay=current.fireHeatDecay;s.fireBurnRate=current.fireBurnRate;
            s.fireFuelGain=current.fireFuelGain;s.fireDecay=current.fireDecay;s.fireWaterQuench=current.fireWaterQuench;
            s.riverCurrent=current.riverCurrent;s.bankResistance=current.bankResistance;s.sedimentHeightScale=current.sedimentHeightScale;
            s.generateTerrain=true;s.terrainSeed=current.terrainSeed;s.terrainNoiseScale=current.terrainNoiseScale;s.terrainAmplitude=current.terrainAmplitude;
            s.slopeHeight=current.slopeHeight;s.mountainHeight=current.mountainHeight;s.ecologyRevision=1;
        }
        var dead=world.deadTile as Tile;
        if(dead!=null){dead.sprite=Sprite("ground_dead");dead.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None;EditorUtility.SetDirty(dead);}
        // The supplied soil_slope image represents an embankment. Use it only on river sediment.
        world.damPrefab=Prefab("RiverEmbankment",Sprite("soil_slope"),1.2f);world.slopePrefab=null;
        for(int stage=2;stage<=3;stage++)
        {
            string path=AssetDatabase.GetAssetPath(world.plantPrefabs[stage-1]);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var scale=root.transform.localScale;var box=root.GetComponent<BoxCollider2D>();if(box==null)box=root.AddComponent<BoxCollider2D>();
                box.size=new Vector2((stage==3?.48f:.32f)/Mathf.Abs(scale.x),.28f/Mathf.Abs(scale.y));
                box.offset=new Vector2(0,-(stage==3?.55f:.52f)/scale.y);box.isTrigger=false;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        if(world.bridges.Count==0)foreach(var root in world.gameObject.scene.GetRootGameObjects())
            foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if(renderer.sprite==null||!renderer.sprite.name.StartsWith("bridge_"))continue;
                var cells=new System.Collections.Generic.List<Vector3Int>();var bounds=renderer.bounds;
                foreach(var cell in world.bridgeMarkers.cellBounds.allPositionsWithin)
                {var p=world.ground.GetCellCenterWorld(cell);if(world.bridgeMarkers.HasTile(cell)&&p.x>=bounds.min.x&&p.x<=bounds.max.x&&p.y>=bounds.min.y&&p.y<=bounds.max.y)cells.Add(cell);}
                if(cells.Count>0)world.bridges.Add(new V1BridgeSpan{view=renderer.gameObject,cells=cells.ToArray()});
            }
        foreach(var span in world.bridges)if(span.view!=null&&span.cells!=null&&span.cells.Length>0)
        {
            var renderer=span.view.GetComponent<SpriteRenderer>();
            bool stone=renderer.sprite!=null&&(renderer.sprite.name.Contains("stone")||span.view.name.Contains("石"));
            var minimum=span.cells[0];var maximum=minimum;
            foreach(var cell in span.cells){minimum=Vector3Int.Min(minimum,cell);maximum=Vector3Int.Max(maximum,cell);}
            renderer.sprite=OrthographicBridge(stone);renderer.sharedMaterial=material;renderer.sortingOrder=5;
            var a=world.ground.CellToWorld(minimum);var b=world.ground.CellToWorld(maximum+new Vector3Int(1,1,0));
            span.view.transform.position=(a+b)*.5f;span.view.transform.rotation=Quaternion.identity;
            span.view.transform.localScale=new Vector3((b.x-a.x)/renderer.sprite.bounds.size.x,(b.y-a.y)/renderer.sprite.bounds.size.y,1);
        }
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"Art/V1Water.shader");
        if(shader==null)throw new InvalidOperationException("V1Water shader must be installed before scene upgrade");
        string materialPath=Root+"Art/FlowingWater.mat";var waterMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(waterMaterial==null){waterMaterial=new Material(shader);AssetDatabase.CreateAsset(waterMaterial,materialPath);}
        world.water.GetComponent<TilemapRenderer>().sharedMaterial=waterMaterial;
        world.terrainPrefabs=new[]{Prefab("TerrainPlain",TerrainSprite("TerrainPlainV1",256),1.01f),
            Prefab("TerrainSlope",TerrainSprite("TerrainSlopeV1",256),1.2f),Prefab("TerrainMountain",Sprite("mountain"),1.25f)};
        world.ground.GetComponent<TilemapRenderer>().sortingOrder=-2;
    }
    private static Sprite TerrainSprite(string name,int maxSize)
    {
        string path=Root+"Art/"+name+".png";
        if(!File.Exists(path))throw new InvalidOperationException("Install terrain sprite first: "+path);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null){AssetDatabase.ImportAsset(path);importer=(TextureImporter)AssetImporter.GetAtPath(path);}
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=128;importer.maxTextureSize=maxSize;importer.filterMode=FilterMode.Point;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;importer.spritePivot=new Vector2(.5f,.5f);importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Sprite OrthographicBridge(bool stone)
    {
        Directory.CreateDirectory(Root+"Art");string path=Root+"Art/"+(stone?"BridgeStoneTopV2":"BridgeWoodTopV2")+".png";
        if(!File.Exists(path))
        {
            // A native pixel deck whose outer rectangle is exactly the bridge marker span.
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                int noise=((x*73+y*37+x*y*11)%13)-6;
                bool rail=y<4||y>=60,edge=x<1||x>=63;
                Color32 color;
                if(stone)
                {
                    bool mortar=y%12==0||(x+(y/12%2)*8)%16==0;
                    int shade=rail?73:mortar?68:132+noise;
                    color=new Color32((byte)shade,(byte)(shade+2),(byte)Math.Max(0,shade-8),255);
                    if(y==4||y==59)color=new Color32(181,174,149,255);
                }
                else
                {
                    bool seam=x%8==0;int shade=rail?61:seam?68:125+noise+(x%8==1?16:0);
                    color=new Color32((byte)(shade+25),(byte)shade,(byte)Math.Max(0,shade-50),255);
                    if(y==4||y==59)color=new Color32(186,141,73,255);
                    if(!rail&&!seam&&(x*11+y*7)%53==0)color=new Color32(88,59,28,255);
                }
                if(edge)color=new Color32(47,40,29,255);
                if(rail&&x%16<4)color=stone?new Color32(168,164,140,255):new Color32(169,120,62,255);
                texture.SetPixel(x,y,color);
            }
            texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.spritePivot=new Vector2(.5f,.5f);importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static Scene Build()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var root=new GameObject("河岸繁荣 V1 · 手动时间与五行生态");var w=root.AddComponent<V1World>();w.spriteMaterial=material;
        var grid=new GameObject("Grid · 每格1单位，集中数据");grid.transform.SetParent(root.transform,false);grid.AddComponent<Grid>();
        w.ground=Map(grid.transform,"Ground · 可用Tilemap绘制",0);w.grass=Map(grid.transform,"Vegetation ground",1);w.water=Map(grid.transform,"Visible water",2);w.fire=Map(grid.transform,"Fire",22);
        w.riverMarkers=Map(grid.transform,"River bed markers",0,false);w.sourceMarkers=Map(grid.transform,"Upstream sources",0,false);w.outletMarkers=Map(grid.transform,"Downstream outlets",0,false);w.bridgeMarkers=Map(grid.transform,"Bridge markers",0,false);w.obstacles=Map(grid.transform,"Rock barriers",10);w.walking=Map(grid.transform,"Walking collision · derived",0,false);
        var body=w.walking.gameObject.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Static;var tc=w.walking.gameObject.AddComponent<TilemapCollider2D>();w.walking.gameObject.AddComponent<CompositeCollider2D>();tc.usedByComposite=true;
        var soil=Sprite("ground_elements","ground_earth");var grass=Sprite("ground_elements","ground_grass");
        var land=Ground("Land",soil,V1Level.Default(0,0));var path=Ground("Path",soil,V1Level.Default(5,4));var river=Ground("Riverbed",soil,V1Level.Default(12,0));
        w.soilTile=land;w.grassTile=Tile("Grass",grass);w.dryTile=Tile("Dry",soil);w.deadTile=Tile("Deathland",Sprite("ground_dead"));w.pollutedWaterTile=Tile("PollutedWater",Sprite("water_polluted"));w.walkingTile=Tile("Collision",null,true);
        string[] waterNames={"water_nw","water_n","water_ne","water_w","water_center","water_e","water_sw","water_s","water_se"};w.waterTiles=waterNames.Select(n=>Tile(n,Sprite("water",n))).ToArray();w.fireTiles=Enumerable.Range(0,4).Select(i=>Tile("Fire"+i,Sprite("fire","fire_"+i))).ToArray();
        w.plantPrefabs=new[]{Prefab("Sapling",Sprite("sapling"),.4f),Prefab("Tree",Sprite("tree"),1.05f),Prefab("Forest",Sprite("forest"),1.6f)};
        w.mineralPrefabs=new[]{Prefab("Mine",Sprite("gold_mine_mountain"),1.4f),Prefab("ExposedOre",Sprite("gold_ore_exposed"),.8f),Prefab("AbandonedMine",Sprite("gold_mine_abandoned"),1.4f)};
        w.animalPrefabs=new[]{Prefab("WildOrFarmDeer",Sprite("deer"),.65f),Prefab("WildOrFarmRabbit",Sprite("rabbit"),.4f),Prefab("WildOrFarmCow",Sprite("cow"),.8f),Prefab("WildOrFarmChicken",Sprite("chicken"),.35f)};
        w.villagePrefabs=new[]{Prefab("VillageSmall",Sprite("village"),2.2f),Prefab("VillageMedium",Sprite("village_medium"),3.2f),Prefab("VillageLarge",Sprite("village_large"),4.2f)};
        w.pollutionPrefab=Prefab("Pollution",Sprite("pollution_fog"),1.5f);w.slopePrefab=Prefab("SoilSlope",Sprite("soil_slope"),1.2f);w.damPrefab=Prefab("SedimentDam",Sprite("river_sediment_dam"),2.1f);
        w.timeBackgrounds=new[]{Sprite("background_morning"),Sprite("background_noon"),Sprite("background_evening")};
        var rock=Tile("Rock",Sprite("rock"));var layout=new V1Simulation(V1Level.Width,V1Level.Height);
        for(int y=0;y<V1Level.Height;y++)for(int x=0;x<V1Level.Width;x++){int wx=x+V1Level.OriginX,wy=y+V1Level.OriginY;layout.Cells[layout.Index(x,y)]=V1Level.Default(wx,wy);}
        V1Level.ApplyFeatures(layout);
        for(int y=0;y<V1Level.Height;y++)for(int x=0;x<V1Level.Width;x++)
        {
            var cell=new Vector3Int(x+V1Level.OriginX,y+V1Level.OriginY,0);var c=layout.Cells[layout.Index(x,y)];var d=V1Level.Default(cell.x,cell.y);
            w.ground.SetTile(cell,c.River?river:d.Grass<1?path:land);
            if(c.River)w.riverMarkers.SetTile(cell,w.walkingTile);if(c.Source)w.sourceMarkers.SetTile(cell,w.walkingTile);if(c.Outlet)w.outletMarkers.SetTile(cell,w.walkingTile);if(c.Bridge)w.bridgeMarkers.SetTile(cell,w.walkingTile);if(c.Blocked)w.obstacles.SetTile(cell,rock);
            if(!c.Equals(d))w.cellSeeds.Add(new V1CellSeed{cell=cell,state=c});
        }
        var populated=V1Level.Create();foreach(var a in populated.Animals)w.animalSeeds.Add(new V1AnimalSeed{cell=new Vector3Int(a.cell%populated.Width+V1Level.OriginX,a.cell/populated.Width+V1Level.OriginY,0),kind=a.kind,origin=a.origin});
        Decor("木桥",Prefab("WoodBridge",Sprite("bridge_wood"),3),new Vector3(13,5),5);Decor("石桥",Prefab("StoneBridge",Sprite("bridge_stone"),3),new Vector3(13,18),5);
        var plot=Outline("教学播种地 (7,6)",new Color(.96f,.81f,.3f));plot.enabled=true;plot.positionCount=5;plot.SetPositions(new[]{new Vector3(7,6),new Vector3(7,7),new Vector3(8,7),new Vector3(8,6),new Vector3(7,6)});
        var go=new GameObject("人物 · 河岸守护者");go.transform.position=w.spawn;var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;go.AddComponent<CircleCollider2D>().radius=.18f;
        w.player=go.AddComponent<TutorialCharacter>();var avatar=new GameObject("Avatar").AddComponent<SpriteRenderer>();avatar.transform.SetParent(go.transform,false);avatar.transform.localPosition=new Vector3(0,.38f);avatar.sprite=Sprite("character","character_down_0");avatar.sharedMaterial=material;avatar.sortingOrder=19;avatar.transform.localScale=Vector3.one*(.52f/avatar.sprite.bounds.size.x);w.player.bodyView=avatar;w.player.frames=new[]{"down","left","right","up"}.SelectMany(d=>Enumerable.Range(0,3).Select(i=>Sprite("character","character_"+d+"_"+i))).ToArray();
        var cam=new GameObject("Main Camera");cam.tag="MainCamera";w.worldCamera=cam.AddComponent<Camera>();w.worldCamera.orthographic=true;w.worldCamera.orthographicSize=7;w.worldCamera.clearFlags=CameraClearFlags.SolidColor;cam.transform.position=new Vector3(9.5f,7.1f,-10);w.worldCamera.transparencySortMode=TransparencySortMode.CustomAxis;w.worldCamera.transparencySortAxis=Vector3.up;var follow=cam.AddComponent<V1Camera>();follow.world=w;follow.target=go.transform;
        w.regionOutline=Outline("村庄采材与统计范围",new Color(.6f,1,.6f));w.cellOutline=Outline("目标格",new Color(1,.85f,.4f));w.densityOutline=Outline("播种3×3范围",new Color(.6f,1,.45f));
        var weather=new GameObject("Weather · visual only");w.weather=weather.AddComponent<TutorialWeather>();w.weather.streak=Sprite("rain","rain_streak");w.weather.splash0=Sprite("rain","splash_0");w.weather.splash1=Sprite("rain","splash_1");w.weather.ripple0=Sprite("rain","ripple_0");w.weather.ripple1=Sprite("rain","ripple_1");
        w.ground.CompressBounds();BuildUI(w);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();return scene;
    }
    private static void Decor(string name,GameObject prefab,Vector3 position,int order){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name=name;go.transform.position=position;go.GetComponent<SpriteRenderer>().sortingOrder=order;}
    private static LineRenderer Outline(string name,Color color){var l=new GameObject(name).AddComponent<LineRenderer>();l.sharedMaterial=material;l.startColor=l.endColor=color;l.startWidth=l.endWidth=.04f;l.sortingOrder=27;l.enabled=false;return l;}
    private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return r;}
    private static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,Color? color=null){var r=Rect(parent,name,anchor,pos,size);var i=r.gameObject.AddComponent<Image>();i.color=color??new Color(.035f,.075f,.055f,.92f);i.raycastTarget=false;return r;}
    private static Text Text(Transform parent,string name,Vector2 pos,Vector2 size,int font,string text=""){var r=Rect(parent,name,new Vector2(0,1),pos,size);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.color=new Color(.97f,.96f,.86f);t.raycastTarget=false;t.text=text;return t;}
    private static Button Button(Transform parent,string name,Vector2 pos,Vector2 size,string text){var r=Rect(parent,name,new Vector2(0,1),pos,size);r.gameObject.AddComponent<Image>().color=new Color(.22f,.38f,.28f);var b=r.gameObject.AddComponent<Button>();var t=Text(r,"Label",Vector2.zero,size,23,text);t.alignment=TextAnchor.MiddleCenter;return b;}
    private static void BuildUI(V1World w)
    {
        var go=new GameObject("V1 Canvas · 界面可在Inspector编辑",typeof(RectTransform));go.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var cs=go.AddComponent<CanvasScaler>();cs.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;cs.referenceResolution=new Vector2(1920,1080);cs.matchWidthOrHeight=.5f;go.AddComponent<GraphicRaycaster>();var h=go.AddComponent<V1HUD>();h.world=w;w.hud=h;
        var head=Panel(go.transform,"目标与时间",new Vector2(0,1),new Vector2(24,-20),new Vector2(1872,144));h.title=Text(head,"Title",new Vector2(18,-12),new Vector2(1030,45),32);h.status=Text(head,"Status",new Vector2(18,-64),new Vector2(1070,40),25);h.toolsButton=Button(head,"Tools",new Vector2(18,-106),new Vector2(180,30),"五行页 · Tab");h.restartButton=Button(head,"Restart",new Vector2(215,-106),new Vector2(112,30),"重开 · R");
        var img=Rect(head,"时段背景",new Vector2(0,1),new Vector2(1110,-12),new Vector2(210,118));h.timeImage=img.gameObject.AddComponent<Image>();h.timeImage.preserveAspect=true;h.timeImage.raycastTarget=false;h.clockLabel=Text(head,"Clock",new Vector2(1340,-14),new Vector2(490,62),25);h.advanceButton=Button(head,"Advance",new Vector2(1340,-83),new Vector2(505,46),"渡过时段 → 中午");h.advanceButton.GetComponent<Image>().color=new Color(.52f,.4f,.16f);
        var task=Panel(go.transform,"教学目标",new Vector2(0,1),new Vector2(24,-184),new Vector2(450,175));h.tutorial=Text(task,"Tutorial",new Vector2(16,-12),new Vector2(418,153),24);
        var footer=Panel(go.transform,"操作说明",new Vector2(.5f,0),new Vector2(0,18),new Vector2(1872,104));h.feedback=Text(footer,"Feedback",new Vector2(18,-10),new Vector2(1836,88),24);
        var read=Panel(go.transform,"地块数据",new Vector2(0,0),new Vector2(24,138),new Vector2(1210,78));h.readout=Text(read,"Readout",new Vector2(14,-7),new Vector2(1180,66),22);
        var tooltip=Panel(go.transform,"村庄悬停详情",new Vector2(1,1),new Vector2(-24,-184),new Vector2(460,415));h.villagePanel=tooltip.gameObject;h.villageDetails=Text(tooltip,"Details",new Vector2(18,-14),new Vector2(424,390),24);tooltip.gameObject.SetActive(false);
        var page=Panel(go.transform,"五行独立页面",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1720,820),new Color(.035f,.065f,.047f,.99f));page.GetComponent<Image>().raycastTarget=true;h.toolsPage=page.gameObject;Text(page,"Heading",new Vector2(36,-22),new Vector2(1430,58),36,"五行工具 · 无限投放，自由建设");h.closeToolsButton=Button(page,"Close",new Vector2(1520,-26),new Vector2(160,50),"返回 · Tab");
        Text(page,"Hint",new Vector2(36,-94),new Vector2(1635,64),24,"选择后在人物附近 3 格内点击地面或按 E 投放。打开页面与走动均不会推进环境。\n投放改变存量；点击“渡过时段”才结算生长、水流、污染与村庄发展。");
        string[] desc={"同格金多于木会压制木\n未成矿的金向低处富集\n金≥20成固定矿，开采可枯竭","木工具只播种，不发木材\n3×3有树格<4才萌发\n每增1木消耗1土、2水","灌溉、饮水、灭火\n水头差推动邻格流动\n搬运污染与泥沙，可能洪水","燃烧植物，灰烬补土\n处理部分污染，也产生烟\n火>金才损金；失控伤村庄","只补充土地养分\n无种子只长草，不长树\n改变高度需另外使用地形工具"};
        h.elementButtons=new Button[5];string[] icons={"ground_gold","ground_wood","ground_water","ground_fire","ground_earth"};
        for(int e=0;e<5;e++){var card=Panel(page,"Element "+e,new Vector2(0,1),new Vector2(36+e*330,-184),new Vector2(314,300),new Color(.12f,.22f,.16f));var icon=Rect(card,"Icon",new Vector2(0,1),new Vector2(16,-16),new Vector2(60,60)).gameObject.AddComponent<Image>();icon.sprite=Sprite("ground_elements",icons[e]);icon.raycastTarget=false;Text(card,"Name",new Vector2(88,-26),new Vector2(216,46),26,V1World.ElementName((V1Element)e));Text(card,"Rules",new Vector2(16,-96),new Vector2(282,144),23,desc[e]);h.elementButtons[e]=Button(card,"Select",new Vector2(16,-246),new Vector2(282,42),"选择 · "+(e+1));}
        h.amountLabel=Text(page,"Amount",new Vector2(36,-510),new Vector2(1090,44),25);h.amountButtons=new Button[3];for(int i=0;i<3;i++)h.amountButtons[i]=Button(page,"Amount"+i,new Vector2(1140+i*180,-510),new Vector2(164,42),"每次 "+new[]{1,4,8}[i]+" 份");
        Text(page,"TerrainHelp",new Vector2(36,-578),new Vector2(1635,76),24,"地形决定水路与金的富集低点。垫高／挖低每次改变高度 0.2，不直接增加养分。\nQ 或下方按钮安排人物附近 5×5 格降雨；下一次渡过时段才补水。");h.raiseButton=Button(page,"Raise",new Vector2(36,-684),new Vector2(310,54),"地形 · 垫高");h.lowerButton=Button(page,"Lower",new Vector2(366,-684),new Vector2(310,54),"地形 · 挖低");h.rainButton=Button(page,"Rain",new Vector2(696,-684),new Vector2(390,54),"安排下一时段降雨");Text(page,"Animals",new Vector2(1110,-677),new Vector2(570,110),22,"动物由生境与繁殖自然出现。\n初始河岸有野生与养殖动物；\n饲养群归本村，野生群会迁移。");page.gameObject.SetActive(false);
        var events=new GameObject("EventSystem");events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();
    }
}
#endif
