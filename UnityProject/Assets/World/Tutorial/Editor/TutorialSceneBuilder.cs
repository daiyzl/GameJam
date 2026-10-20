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

public static class TutorialSceneBuilder
{
    public const string ScenePath="Assets/Scenes/FiveElementsTutorial.unity";
    private const string Root="Assets/World/Tutorial/";
    private const string Old="Assets/World/PixelArtV1/Sprites/";
    private static Material material;
    private static Sprite Sprite(string sheet,string name=null)
    {
        string path=(sheet=="rock"||sheet=="village"?Root+"Art/":Old)+sheet+".png";
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        var result=name==null?sprites.FirstOrDefault():sprites.FirstOrDefault(s=>s.name==name);
        if(result==null)throw new InvalidOperationException("Missing sprite: "+path+" / "+name);return result;
    }
    private static TutorialTile Tile(string name,Sprite sprite,float wood=0,float water=0,float earth=0,float elevation=3,bool solid=false,float offset=0,float scale=1)
    {
        string path=Root+"Tiles/"+name+".asset";var tile=AssetDatabase.LoadAssetAtPath<TutorialTile>(path);if(tile!=null)return tile;
        tile=ScriptableObject.CreateInstance<TutorialTile>();tile.name=name;tile.sprite=sprite;tile.wood=wood;tile.water=water;tile.earth=earth;tile.elevation=elevation;tile.blocksSimulation=solid;
        tile.colliderType=solid?UnityEngine.Tilemaps.Tile.ColliderType.Grid:UnityEngine.Tilemaps.Tile.ColliderType.None;tile.flags=TileFlags.None;
        tile.transform=Matrix4x4.TRS(new Vector3(0,offset,0),Quaternion.identity,Vector3.one*scale);AssetDatabase.CreateAsset(tile,path);return tile;
    }
    private static Tilemap Map(Transform parent,string name,int order,bool render=true)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var map=go.AddComponent<Tilemap>();
        if(render){var renderer=go.AddComponent<TilemapRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;renderer.mode=order>=10?TilemapRenderer.Mode.Individual:TilemapRenderer.Mode.Chunk;}
        return map;
    }
    private static SpriteRenderer View(string name,Sprite sprite,Vector3 position,float scale=1)
    {
        var go=new GameObject(name);go.transform.position=position;go.transform.localScale=Vector3.one*scale;
        var view=go.AddComponent<SpriteRenderer>();view.sprite=sprite;view.sharedMaterial=material;view.sortingOrder=10;view.spriteSortPoint=SpriteSortPoint.Pivot;return view;
    }
    private static void ImportNewArt()
    {
        foreach(string name in new[]{"rock","village"})
        {
            string path=Root+"Art/"+name+".png";AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);importer.spritePixelsPerUnit=width/(name=="rock"?1.1f:6f);
            importer.SaveAndReimport();
        }
    }
    [MenuItem("五行世界/教学关卡/打开村口试炼")]
    public static void Open()
    {
        if(EditorApplication.isPlaying)return;
        ImportNewArt();Directory.CreateDirectory(Root+"Tiles");Directory.CreateDirectory(Root+"Prefabs");AssetDatabase.Refresh();
        string materialPath=Root+"TutorialSprite.mat";material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,materialPath);}
        foreach(string name in new[]{"rock","village"})
        {
            string path=Root+"Prefabs/"+name+".prefab";
            if(!File.Exists(path)){var view=View(name,Sprite(name),Vector3.zero);PrefabUtility.SaveAsPrefabAsset(view.gameObject,path);UnityEngine.Object.DestroyImmediate(view.gameObject);}
        }
        Scene scene=SceneManager.GetSceneByPath(ScenePath);
        if(!scene.IsValid()||!scene.isLoaded)scene=File.Exists(ScenePath)?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive):Build();
        SceneManager.SetActiveScene(scene);
        for(int i=SceneManager.sceneCount-1;i>=0;i--){Scene other=SceneManager.GetSceneAt(i);if(other!=scene&&!other.isDirty&&!string.IsNullOrEmpty(other.path))EditorSceneManager.CloseScene(other,true);}
        AssetDatabase.SaveAssets();Selection.activeGameObject=UnityEngine.Object.FindObjectOfType<TutorialWorld>().gameObject;
    }
    private static Scene Build()
    {
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var root=new GameObject("村口试炼 · Tilemap 世界与教学");var world=root.AddComponent<TutorialWorld>();
        var gridObject=new GameObject("Grid（Tile Palette 的 Active Target 选择各 Tilemap）");gridObject.AddComponent<Grid>();gridObject.transform.SetParent(root.transform);
        world.ground=Map(gridObject.transform,"Ground · 初始地面",0);world.water=Map(gridObject.transform,"Water · 水域与岸线",1);
        world.plants=Map(gridObject.transform,"Plants · 木的初始分布",10);world.metal=Map(gridObject.transform,"Metal · 运行金",9);
        world.flames=Map(gridObject.transform,"Fire · 运行火",20);world.obstacles=Map(gridObject.transform,"Obstacles · 岩石",10);
        world.walking=Map(gridObject.transform,"Collision · 自动更新的行走阻挡",0,false);
        var body=world.walking.gameObject.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Static;
        var tileCollider=world.walking.gameObject.AddComponent<TilemapCollider2D>();world.walking.gameObject.AddComponent<CompositeCollider2D>();tileCollider.usedByComposite=true;
        var grass=Tile("Grass",Sprite("ground_elements","ground_grass"),earth:2);
        var soil=Tile("Earth",Sprite("ground_elements","ground_earth"),earth:3);
        var woodGround=Tile("WoodGround",Sprite("ground_elements","ground_wood"),earth:3);
        var scorch=Tile("ScorchedGround",Sprite("ground_elements","ground_fire"),earth:0);
        var rock=Tile("Rock",Sprite("rock"),solid:true);var hiddenBlock=Tile("HiddenBlock",null,solid:true);
        world.saplingTile=Tile("Sapling",Sprite("sapling"),wood:1,offset:.28f);
        world.treeTile=Tile("Tree",Sprite("tree"),wood:5,offset:1.05f);
        world.forestTile=Tile("Forest",Sprite("forest"),wood:12,offset:1);
        world.goldTile=Tile("GoldPatch",Sprite("ground_elements","ground_gold"),scale:.45f);
        world.walkingTile=hiddenBlock;
        string[] waterNames={"water_nw","water_n","water_ne","water_w","water_center","water_e","water_sw","water_s","water_se"};
        world.waterTiles=waterNames.Select(n=>(TileBase)Tile(n,Sprite("water",n),water:6,elevation:0)).ToArray();
        world.fireTiles=Enumerable.Range(0,4).Select(i=>(TileBase)Tile("Fire_"+i,Sprite("fire","fire_"+i),offset:.38f)).ToArray();
        for(int y=-2;y<20;y++)for(int x=-4;x<30;x++)
        {
            var cell=new Vector3Int(x,y,0);bool path=(y>=4&&y<=5&&x>=2&&x<=26)||(x>=2&&x<=3&&y>=3&&y<=8)||world.stations.Any(s=>Mathf.Abs(x-s.x)<=1&&y>=5&&y<=8);
            world.ground.SetTile(cell,path?soil:grass);
            if(x==-4||x==29||y==-2||y==19)world.obstacles.SetTile(cell,rock);
        }
        foreach(var cell in new[]{new Vector3Int(6,13,0),new Vector3Int(12,16,0),new Vector3Int(26,15,0)})world.plants.SetTile(cell,world.forestTile);
        foreach(var cell in new[]{new Vector3Int(2,12,0),new Vector3Int(8,14,0),new Vector3Int(10,12,0),new Vector3Int(15,16,0),new Vector3Int(25,13,0)})world.plants.SetTile(cell,world.treeTile);
        for(int y=12;y<=16;y++)for(int x=18;x<=21;x++)world.water.SetTile(new Vector3Int(x,y,0),world.waterTiles[4]);
        for(int y=10;y<12;y++)world.water.SetTile(new Vector3Int(20,y,0),world.waterTiles[4]);
        for(int i=0;i<4;i++)
        {
            var cell=world.stations[i];world.ground.SetTile(cell,i==3?scorch:woodGround);world.plants.SetTile(cell,i==0?world.saplingTile:i==1?world.forestTile:world.treeTile);
            world.cellSeeds.Add(new TutorialCellSeed {cell=cell,wood=i==0?1:i==1?8:6,earth=i==0?3:0,elevation=0});
        }
        // Distant mountains are scenery; their base is blocked on the data grid.
        View("山体 · 远山",Sprite("mountain"),new Vector3(-.5f,15),1.35f);
        for(int y=13;y<=14;y++)for(int x=-2;x<=1;x++)world.obstacles.SetTile(new Vector3Int(x,y,0),hiddenBlock);
        foreach(var cell in new[]{new Vector3Int(7,3,0),new Vector3Int(14,11,0),new Vector3Int(25,2,0),new Vector3Int(26,11,0)})world.obstacles.SetTile(cell,rock);
        var village=View("村庄 · 起点与终点",Sprite("village"),new Vector3(-.5f,1));var buildingCollider=village.gameObject.AddComponent<BoxCollider2D>();buildingCollider.size=new Vector2(.7f,.25f)*6;buildingCollider.offset=new Vector2(0,.5f);
        var playerObject=new GameObject("玩家 · 四方向像素人物");playerObject.transform.position=world.spawn;
        var rigid=playerObject.AddComponent<Rigidbody2D>();rigid.gravityScale=0;rigid.constraints=RigidbodyConstraints2D.FreezeRotation;rigid.interpolation=RigidbodyInterpolation2D.Interpolate;rigid.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        playerObject.AddComponent<CircleCollider2D>().radius=.18f;world.player=playerObject.AddComponent<TutorialCharacter>();
        var avatar=View("人物 Sprite",Sprite("character","character_up_0"),Vector3.zero,1.4f);avatar.transform.SetParent(playerObject.transform,false);avatar.transform.localPosition=new Vector3(0,.42f);
        world.player.bodyView=avatar;world.player.frames=new[]{"down","left","right","up"}.SelectMany(d=>Enumerable.Range(0,3).Select(i=>Sprite("character","character_"+d+"_"+i))).ToArray();
        var cameraObject=new GameObject("教学相机");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6.7f;camera.backgroundColor=new Color(.24f,.32f,.22f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.transparencySortMode=TransparencySortMode.CustomAxis;camera.transparencySortAxis=Vector3.up;cameraObject.transform.position=new Vector3(7,5,-10);
        var follow=cameraObject.AddComponent<TutorialFollowCamera>();follow.world=world;follow.target=playerObject.transform;
        var marker=new GameObject("当前教学目标 · 黄色框");world.targetOutline=marker.AddComponent<LineRenderer>();world.targetOutline.sharedMaterial=material;world.targetOutline.startWidth=.045f;world.targetOutline.endWidth=.045f;world.targetOutline.startColor=world.targetOutline.endColor=new Color(1,.87f,.35f);world.targetOutline.sortingOrder=25;world.targetOutline.useWorldSpace=true;
        var weatherObject=new GameObject("天气 · 雨线水花涟漪");world.weather=weatherObject.AddComponent<TutorialWeather>();world.weather.streak=Sprite("rain","rain_streak");world.weather.splash0=Sprite("rain","splash_0");world.weather.splash1=Sprite("rain","splash_1");world.weather.ripple0=Sprite("rain","ripple_0");world.weather.ripple1=Sprite("rain","ripple_1");
        world.ground.CompressBounds();BuildUI(world);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();return scene;
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;
        rt.anchorMin=rt.anchorMax=anchor;rt.pivot=anchor;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
    }
    private static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {var rt=Rect(parent,name,anchor,position,size);rt.gameObject.AddComponent<Image>().color=new Color(.04f,.09f,.065f,.88f);return rt;}
    private static Text Text(Transform parent,string name,Vector2 position,Vector2 size,int fontSize,string content="")
    {
        var rt=Rect(parent,name,new Vector2(0,1),position,size);var text=rt.gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=fontSize;text.color=new Color(.97f,.96f,.86f);text.text=content;text.raycastTarget=false;return text;
    }
    private static Button Button(Transform parent,string name,Vector2 position,string content)
    {
        var rt=Rect(parent,name,new Vector2(0,1),position,new Vector2(160,44));rt.gameObject.AddComponent<Image>().color=new Color(.20f,.32f,.23f);var button=rt.gameObject.AddComponent<Button>();
        var label=Text(rt,"Label",new Vector2(0,0),new Vector2(160,44),23,content);label.alignment=TextAnchor.MiddleCenter;return button;
    }
    private static void BuildUI(TutorialWorld world)
    {
        var go=new GameObject("教学 UI · Canvas（可在 Inspector 调整）",typeof(RectTransform));var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;go.AddComponent<GraphicRaycaster>();
        var hud=go.AddComponent<TutorialHUD>();hud.world=world;
        var lesson=Panel(go.transform,"教学说明",new Vector2(0,1),new Vector2(24,-24),new Vector2(590,226));
        hud.title=Text(lesson,"Title",new Vector2(18,-14),new Vector2(554,44),30);hud.instruction=Text(lesson,"Instruction",new Vector2(18,-68),new Vector2(550,138),25);
        hud.pauseButton=Button(go.transform,"Pause",new Vector2(24,-264),"暂停模拟");hud.retryButton=Button(go.transform,"Retry",new Vector2(196,-264),"重试本站");hud.restartButton=Button(go.transform,"Restart",new Vector2(368,-264),"重开关卡");
        var readout=Panel(go.transform,"每格五行数量",new Vector2(1,1),new Vector2(-24,-24),new Vector2(378,226));hud.values=Text(readout,"Cell Values",new Vector2(18,-14),new Vector2(346,200),25);
        var progress=Panel(go.transform,"教学进度",new Vector2(1,1),new Vector2(-24,-264),new Vector2(378,240));hud.progress=Text(progress,"Progress",new Vector2(18,-14),new Vector2(346,214),24);
        var footer=Panel(go.transform,"操作提示",new Vector2(.5f,0),new Vector2(0,20),new Vector2(1800,100));hud.feedback=Text(footer,"Feedback",new Vector2(18,-12),new Vector2(1764,82),25);
        var events=new GameObject("EventSystem");events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();
    }
}
#endif
