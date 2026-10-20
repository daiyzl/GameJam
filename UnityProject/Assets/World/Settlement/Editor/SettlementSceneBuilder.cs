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

public static class SettlementSceneBuilder
{
    public const string ScenePath="Assets/Scenes/FiveElementsSettlement.unity";
    private const string Root="Assets/World/Settlement/";
    private static Material material;
    private static Sprite Sprite(string sheet,string name=null)
    {
        string path=Root+"Art/"+sheet+".png";
        if(sheet=="rock"||sheet=="village")path="Assets/World/Tutorial/Art/"+sheet+".png";
        else if(sheet!="deer"&&sheet!="rabbit"&&sheet!="cow"&&sheet!="chicken")path="Assets/World/PixelArtV1/Sprites/"+sheet+".png";
        var all=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();var sprite=name==null?all.FirstOrDefault():all.FirstOrDefault(s=>s.name==name);
        if(sprite==null)throw new InvalidOperationException("Missing sprite: "+path+" / "+name);return sprite;
    }
    private static TutorialTile Tile(string name,Sprite sprite,float wood=0,float earth=0,bool solid=false,float offset=0,float scale=1)
    {
        string path=Root+"Tiles/"+name+".asset";var tile=AssetDatabase.LoadAssetAtPath<TutorialTile>(path);if(tile!=null)return tile;
        tile=ScriptableObject.CreateInstance<TutorialTile>();tile.name=name;tile.sprite=sprite;tile.wood=wood;tile.earth=earth;tile.colliderType=solid?UnityEngine.Tilemaps.Tile.ColliderType.Grid:UnityEngine.Tilemaps.Tile.ColliderType.None;tile.flags=TileFlags.None;tile.blocksSimulation=solid;
        tile.transform=Matrix4x4.TRS(new Vector3(0,offset),Quaternion.identity,Vector3.one*scale);AssetDatabase.CreateAsset(tile,path);return tile;
    }
    private static Tilemap Map(Transform parent,string name,int order,bool render=true)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var map=go.AddComponent<Tilemap>();
        if(render){var r=go.AddComponent<TilemapRenderer>();r.sharedMaterial=material;r.sortingOrder=order;r.mode=order>=10?TilemapRenderer.Mode.Individual:TilemapRenderer.Mode.Chunk;}return map;
    }
    private static SpriteRenderer View(string name,Sprite sprite,Vector3 position,float scale=1)
    {
        var go=new GameObject(name);go.transform.position=position;go.transform.localScale=Vector3.one*scale;var view=go.AddComponent<SpriteRenderer>();view.sprite=sprite;view.sharedMaterial=material;view.sortingOrder=12;view.spriteSortPoint=SpriteSortPoint.Pivot;return view;
    }
    private static void ImportArt()
    {
        foreach(string name in new[]{"deer","rabbit","cow","chicken"})
        {
            string path=Root+"Art/"+name+".png";AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;imp.filterMode=FilterMode.Point;imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=4096;imp.npotScale=TextureImporterNPOTScale.None;
            imp.GetSourceTextureWidthAndHeight(out int width,out int height);imp.spritePixelsPerUnit=width/(name=="cow"?1.25f:name=="deer"?1.05f:.7f);imp.SaveAndReimport();
        }
    }
    [MenuItem("五行世界/村庄经营/打开河岸繁荣")]
    public static void Open()
    {
        if(EditorApplication.isPlaying)return;ImportArt();Directory.CreateDirectory(Root+"Tiles");Directory.CreateDirectory(Root+"Prefabs");AssetDatabase.Refresh();
        string path=Root+"SettlementSprite.mat";material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default"));AssetDatabase.CreateAsset(material,path);}
        foreach(string name in new[]{"deer","rabbit","cow","chicken"})
        {string prefab=Root+"Prefabs/"+name+".prefab";if(!File.Exists(prefab)){var v=View(name,Sprite(name),Vector3.zero);PrefabUtility.SaveAsPrefabAsset(v.gameObject,prefab);UnityEngine.Object.DestroyImmediate(v.gameObject);}}
        var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)scene=File.Exists(ScenePath)?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive):Build();
        SceneManager.SetActiveScene(scene);for(int i=SceneManager.sceneCount-1;i>=0;i--){var other=SceneManager.GetSceneAt(i);if(other!=scene&&!other.isDirty&&!string.IsNullOrEmpty(other.path))EditorSceneManager.CloseScene(other,true);}
        AssetDatabase.SaveAssets();Selection.activeGameObject=UnityEngine.Object.FindObjectOfType<SettlementWorld>().gameObject;
    }
    private static Scene Build()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var root=new GameObject("河岸繁荣 · 村庄生态世界");var world=root.AddComponent<SettlementWorld>();world.spriteMaterial=material;world.villageSprite=Sprite("village");world.animalSprites=new[]{Sprite("deer"),Sprite("rabbit"),Sprite("cow"),Sprite("chicken")};
        var grid=new GameObject("Grid · 地形和五行");grid.AddComponent<Grid>();grid.transform.SetParent(root.transform);
        world.ground=Map(grid.transform,"Ground · 土地养分",0);world.riverBed=Map(grid.transform,"River Sources · 河流源格",0,false);world.bridges=Map(grid.transform,"Bridges · 过河小径",2);
        world.water=Map(grid.transform,"Water · 动态水量",1);world.plants=Map(grid.transform,"Plants · 动态植被",10);world.flames=Map(grid.transform,"Fire · 动态火势",20);world.metal=Map(grid.transform,"Metal · 建材工具",9);world.obstacles=Map(grid.transform,"Obstacles · 岩壁",10);world.walking=Map(grid.transform,"Collision · 河流建筑密林",0,false);
        var body=world.walking.gameObject.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Static;var collider=world.walking.gameObject.AddComponent<TilemapCollider2D>();world.walking.gameObject.AddComponent<CompositeCollider2D>();collider.usedByComposite=true;
        var grass=Tile("Grass",Sprite("ground_elements","ground_grass"),earth:6);
        var soil=Tile("Earth",Sprite("ground_elements","ground_earth"),earth:7);
        var rock=Tile("Rock",Sprite("rock"),solid:true);world.walkingTile=Tile("Block",null,solid:true);
        world.saplingTile=Tile("Sapling",Sprite("sapling"),wood:1,offset:.28f);world.treeTile=Tile("Tree",Sprite("tree"),wood:5,offset:1.05f);world.forestTile=Tile("Forest",Sprite("forest"),wood:9,offset:1);
        world.goldTile=Tile("Gold",Sprite("ground_elements","ground_gold"),scale:.45f);
        string[] waterNames={"water_nw","water_n","water_ne","water_w","water_center","water_e","water_sw","water_s","water_se"};world.waterTiles=waterNames.Select(n=>(TileBase)Tile(n,Sprite("water",n))).ToArray();world.fireTiles=Enumerable.Range(0,4).Select(i=>(TileBase)Tile("Fire_"+i,Sprite("fire","fire_"+i),offset:.38f)).ToArray();
        for(int y=-2;y<26;y++)for(int x=-4;x<36;x++)
        {
            var cell=new Vector3Int(x,y,0);bool path=(y>=4&&y<=5&&x>=2&&x<=29)||(x>=6&&x<=7&&y>=5&&y<=10);
            world.ground.SetTile(cell,path?soil:grass);
            if(x==-4||x==35||y==-2||y==25)world.obstacles.SetTile(cell,rock);
            if(x>=9&&x<=10&&y>-2&&y<25||y>=17&&y<=18&&x>=10&&x<=30){world.riverBed.SetTile(cell,world.walkingTile);world.water.SetTile(cell,world.waterTiles[4]);}
            if(x>=9&&x<=10&&y>=4&&y<=5)world.bridges.SetTile(cell,soil);
        }
        foreach(var cell in new[]{new Vector3Int(1,15,0),new Vector3Int(4,18,0),new Vector3Int(13,13,0),new Vector3Int(17,21,0),new Vector3Int(23,22,0),new Vector3Int(29,13,0)})world.plants.SetTile(cell,world.forestTile);
        foreach(var cell in new[]{new Vector3Int(4,14,0),new Vector3Int(15,13,0),new Vector3Int(23,21,0),new Vector3Int(26,15,0),new Vector3Int(5,14,0),new Vector3Int(14,14,0),new Vector3Int(7,16,0),new Vector3Int(28,21,0)})world.plants.SetTile(cell,world.treeTile);
        // The first village is on the west bank: one column of its 36 cells contains the river.
        foreach(var cell in new[]{new Vector3Int(4,6,0),new Vector3Int(4,8,0),new Vector3Int(4,10,0),new Vector3Int(5,7,0),new Vector3Int(5,9,0),new Vector3Int(5,11,0),new Vector3Int(8,6,0),new Vector3Int(8,8,0),new Vector3Int(8,10,0),new Vector3Int(7,6,0),new Vector3Int(7,11,0),new Vector3Int(6,11,0)})
        {world.plants.SetTile(cell,world.saplingTile);world.cellSeeds.Add(new TutorialCellSeed{cell=cell,wood=2.8f,water=1,earth=7,elevation=2.4f});}
        foreach(var cell in new[]{new Vector3Int(5,6,0),new Vector3Int(8,10,0)})world.cellSeeds.Add(new TutorialCellSeed{cell=cell,wood=3,water=1,earth=7,elevation=2.4f});
        foreach(var cell in new[]{new Vector3Int(2,12,0),new Vector3Int(18,10,0),new Vector3Int(28,7,0),new Vector3Int(31,22,0),new Vector3Int(2,2,0)})world.obstacles.SetTile(cell,rock);
        View("北岸远山",Sprite("mountain"),new Vector3(1,21),1.1f);for(int y=20;y<=21;y++)for(int x=0;x<=2;x++)world.obstacles.SetTile(new Vector3Int(x,y,0),world.walkingTile);
        var go=new GameObject("玩家 · 河岸守护者");go.transform.position=world.spawn;var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.interpolation=RigidbodyInterpolation2D.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;go.AddComponent<CircleCollider2D>().radius=.18f;
        world.player=go.AddComponent<TutorialCharacter>();var avatar=View("人物 Sprite",Sprite("character","character_up_0"),Vector3.zero,1.4f);avatar.transform.SetParent(go.transform,false);avatar.transform.localPosition=new Vector3(0,.42f);world.player.bodyView=avatar;world.player.frames=new[]{"down","left","right","up"}.SelectMany(d=>Enumerable.Range(0,3).Select(i=>Sprite("character","character_"+d+"_"+i))).ToArray();
        var cam=new GameObject("河岸相机");cam.tag="MainCamera";var camera=cam.AddComponent<Camera>();world.worldCamera=camera;camera.orthographic=true;camera.orthographicSize=7;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.32f,.22f);camera.transparencySortMode=TransparencySortMode.CustomAxis;camera.transparencySortAxis=Vector3.up;cam.transform.position=new Vector3(10,7.5f,-10);
        var follow=cam.AddComponent<SettlementCamera>();follow.world=world;follow.target=go.transform;
        world.hoverRegion=Outline("村庄影响范围 · 6×6",new Color(.55f,.92f,.7f));world.targetOutline=Outline("可投放地块",new Color(1,.88f,.35f));
        var weather=new GameObject("局部降雨");world.weather=weather.AddComponent<TutorialWeather>();world.weather.streak=Sprite("rain","rain_streak");world.weather.splash0=Sprite("rain","splash_0");world.weather.splash1=Sprite("rain","splash_1");world.weather.ripple0=Sprite("rain","ripple_0");world.weather.ripple1=Sprite("rain","ripple_1");
        world.ground.CompressBounds();BuildUI(world);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();return scene;
    }
    private static LineRenderer Outline(string name,Color color)
    {var go=new GameObject(name);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startColor=line.endColor=color;line.startWidth=line.endWidth=.04f;line.sortingOrder=25;line.enabled=false;return line;}
    private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=anchor;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
    private static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color? color=null)
    {var rt=Rect(parent,name,anchor,position,size);var image=rt.gameObject.AddComponent<Image>();image.color=color??new Color(.035f,.075f,.055f,.92f);image.raycastTarget=false;return rt;}
    private static Text Text(Transform parent,string name,Vector2 position,Vector2 size,int fontSize,string content="")
    {var rt=Rect(parent,name,new Vector2(0,1),position,size);var text=rt.gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=fontSize;text.color=new Color(.96f,.95f,.84f);text.text=content;text.raycastTarget=false;return text;}
    private static Button Button(Transform parent,string name,Vector2 position,Vector2 size,string caption)
    {var rt=Rect(parent,name,new Vector2(0,1),position,size);rt.gameObject.AddComponent<Image>().color=new Color(.22f,.38f,.28f);var b=rt.gameObject.AddComponent<Button>();var t=Text(rt,"Label",Vector2.zero,size,23,caption);t.alignment=TextAnchor.MiddleCenter;return b;}
    private static void Icon(Transform parent,Sprite sprite,Vector2 position,Vector2 size)
    {var rt=Rect(parent,"Icon",new Vector2(0,1),position,size);var img=rt.gameObject.AddComponent<Image>();img.sprite=sprite;img.preserveAspect=true;img.raycastTarget=false;}
    private static void BuildUI(SettlementWorld world)
    {
        var go=new GameObject("村庄 UI · Canvas（可编辑）",typeof(RectTransform));var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scale=go.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;go.AddComponent<GraphicRaycaster>();var hud=go.AddComponent<SettlementHUD>();hud.world=world;world.hud=hud;
        var head=Panel(go.transform,"繁荣目标",new Vector2(0,1),new Vector2(24,-20),new Vector2(1872,122));hud.title=Text(head,"Title",new Vector2(18,-10),new Vector2(1280,45),30);hud.status=Text(head,"Status",new Vector2(18,-66),new Vector2(1390,40),25);
        hud.inventoryButton=Button(head,"Inventory",new Vector2(1460,-26),new Vector2(150,54),"资源 · Tab");hud.pauseButton=Button(head,"Pause",new Vector2(1622,-26),new Vector2(100,54),"暂停");hud.restartButton=Button(head,"Restart",new Vector2(1734,-26),new Vector2(116,54),"重开");
        var footer=Panel(go.transform,"消息与操作",new Vector2(.5f,0),new Vector2(0,18),new Vector2(1872,112));hud.feedback=Text(footer,"Feedback",new Vector2(18,-12),new Vector2(1836,90),24);
        var readout=Panel(go.transform,"格子读数",new Vector2(0,0),new Vector2(24,140),new Vector2(820,48));hud.cellValues=Text(readout,"Values",new Vector2(14,-5),new Vector2(800,40),22);
        var tooltip=Panel(go.transform,"鼠标悬停村庄 · 人口与36格环境",new Vector2(1,1),new Vector2(-24,-156),new Vector2(470,340));hud.villagePanel=tooltip.gameObject;hud.villageDetails=Text(tooltip,"Village Details",new Vector2(18,-14),new Vector2(434,312),24);tooltip.gameObject.SetActive(false);
        var page=Panel(go.transform,"资源独立页面 · Tab",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1720,890),new Color(.035f,.065f,.047f,.99f));hud.inventoryPage=page.gameObject;
        Text(page,"Page Title",new Vector2(40,-25),new Vector2(1490,60),36,"五行资源与养殖");hud.closeInventory=Button(page,"Close",new Vector2(1520,-30),new Vector2(155,52),"返回 · Tab");
        Text(page,"Page Help",new Vector2(40,-96),new Vector2(1610,66),24,"选择一种资源，然后在人物附近投放。打开页面时世界暂停。库存会缓慢补充；火与动物数量有限。\n村庄周围 6×6 格决定人口与规模，鼠标移到村庄可查看人数和环境。");
        string[] descriptions={"建造工具与建材\n与木一起扩建住房\n仅修剪过密树林","植被、食物与栖息地\n有水与养分时生长\n每增1木消耗1土2水","饮水、灌溉与灭火\n沿地势流动\n过量积水挤占居住地","少量火也会烧木\n灰烬回土，火势会传播\n威胁居民与动物","土地养分与蓄水\n支持植物生长\n大量堆土改变水路"};
        string[] names={"金 · 建造","木 · 生态","水 · 生存","火 · 扰动","土 · 土地"};string[] sprites={"ground_gold","ground_wood","ground_water","ground_fire","ground_earth"};hud.elementButtons=new Button[5];hud.elementStocks=new Text[5];
        for(int e=0;e<5;e++)
        {var card=Panel(page,"五行 "+e,new Vector2(0,1),new Vector2(40+e*328,-190),new Vector2(310,310),new Color(.12f,.22f,.16f));Icon(card,Sprite("ground_elements",sprites[e]),new Vector2(18,-18),new Vector2(62,62));Text(card,"Name",new Vector2(92,-26),new Vector2(210,46),26,names[e]);hud.elementStocks[e]=Text(card,"Stock",new Vector2(18,-92),new Vector2(276,38),22);Text(card,"Rules",new Vector2(18,-140),new Vector2(275,106),22,descriptions[e]);hud.elementButtons[e]=Button(card,"Select",new Vector2(18,-256),new Vector2(274,42),"选择投放");}
        Text(page,"Animal Heading",new Vector2(40,-520),new Vector2(1610,42),27,"养殖动物 · 提供食物并繁殖；野生动物会自行寻找水源、迁移和进入地图");hud.animalButtons=new Button[4];hud.animalStocks=new Text[4];
        for(int a=0;a<4;a++)
        {var card=Panel(page,"养殖 "+a,new Vector2(0,1),new Vector2(40+a*410,-585),new Vector2(392,265),new Color(.12f,.22f,.16f));Icon(card,world.animalSprites[a],new Vector2(18,-18),new Vector2(88,88));Text(card,"Name",new Vector2(128,-24),new Vector2(235,48),28,SettlementSimulation.AnimalName((AnimalKind)a));hud.animalStocks[a]=Text(card,"Stock",new Vector2(18,-122),new Vector2(360,62),21);Text(card,"Habitat",new Vector2(128,-74),new Vector2(244,44),21,a==0?"偏爱树林与水源":a==1?"偏爱草地和灌木":a==2?"需要充足草料": "土地、种子与灌木");hud.animalButtons[a]=Button(card,"Select",new Vector2(18,-208),new Vector2(356,42),"选择饲养");}
        page.gameObject.SetActive(false);var events=new GameObject("EventSystem");events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();
    }
}
#endif
