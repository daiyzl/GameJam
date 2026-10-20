#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/FiveElementsWorld.unity";
    private const string ArtPath = "Assets/World/Art/";
    private static Material material;
    [MenuItem("五行世界/生成或打开简易世界")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            else scene = Build();
        }
        SceneManager.SetActiveScene(scene);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene other = SceneManager.GetSceneAt(i);
            if (other != scene && !other.isDirty && !string.IsNullOrEmpty(other.path)) EditorSceneManager.CloseScene(other, true);
        }
        Selection.activeGameObject = UnityEngine.Object.FindObjectOfType<ElementWorld>().gameObject;
    }
    private static Sprite Art(string name, int width, int height, Func<int, int, Color> pixel)
    {
        string path = ArtPath + name + ".png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static bool Circle(int x, int y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
    private static SpriteRenderer Render(Transform parent, string name, Sprite sprite, Vector3 position, int order, Color color)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material; sr.sortingOrder = order; sr.color = color; return sr;
    }
    private static Scene Build()
    {
        Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        string materialPath = ArtPath + "WorldSprite.mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        Sprite ground = Art("Ground", 32, 32, (x,y) => Color.white);
        Sprite water = Art("Water", 40, 40, (x,y) => Circle(x,y,20,20,20) ? Color.white : Color.clear);
        Sprite grass = Art("Grass", 16, 12, (x,y) => (x == 3 && y < 6 || x == 7 && y < 10 || x == 11 && y < 7) ? new Color(.20f,.39f,.25f) : Color.clear);
        Sprite tree = Art("Tree", 48, 64, (x,y) => {
            if (Circle(x,y,24,12,17)) { if (y < 12) return new Color(.12f,.22f,.16f,.35f); }
            if (x >= 21 && x <= 26 && y >= 10 && y < 36) return new Color(.39f,.25f,.16f);
            if (Circle(x,y,24,39,20) || Circle(x,y,16,37,13) || Circle(x,y,32,37,13))
                return y > 42 && x < 27 ? new Color(.35f,.61f,.35f) : (y < 31 ? new Color(.15f,.34f,.23f) : new Color(.23f,.47f,.29f));
            return Color.clear;
        });
        Sprite rock = Art("Rock", 32, 32, (x,y) => {
            if (!Circle(x,y,16,15,14)) return Color.clear;
            return y > 17 ? new Color(.56f,.60f,.56f) : new Color(.35f,.42f,.39f);
        });
        Sprite gold = Art("Gold", 20, 16, (x,y) => Circle(x,y,7,6,6) || Circle(x,y,13,8,5) ? (y > 7 ? new Color(1,.84f,.37f) : new Color(.70f,.48f,.18f)) : Color.clear);
        Sprite flame = Art("Flame", 24, 36, (x,y) => {
            if (y < 5 || Math.Abs(x-12) > (32-y)*.32f || y > 32) return Color.clear;
            return y < 17 && Math.Abs(x-12) < 4 ? new Color(1,.90f,.38f) : new Color(1,.38f,.12f);
        });
        Sprite mark = Art("Selection", 32, 32, (x,y) => (x<2||x>29||y<2||y>29) && ((x<7||x>24)||(y<7||y>24)) ? new Color(1,.91f,.53f,.7f) : Color.clear);
        Sprite person = Art("Character", 24, 32, (x,y) => {
            if (y<4 && (x>=6 && x<=9 || x>=14 && x<=17)) return new Color(.19f,.24f,.27f);
            if (x>=5 && x<=18 && y>=4 && y<=17) return y>14 ? new Color(.94f,.66f,.34f) : new Color(.36f,.64f,.70f);
            if (Circle(x,y,12,23,7)) return y>25 ? new Color(.28f,.23f,.19f) : new Color(1,.83f,.62f);
            return Color.clear;
        });
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var root = new GameObject("五行世界 · 地块与环境"); var world = root.AddComponent<ElementWorld>();
        var terrain = new GameObject("地形（选择任意地块编辑初始五行）"); terrain.transform.SetParent(root.transform);
        for (int y=0; y<world.height; y++) for (int x=0; x<world.width; x++)
        {
            var go = new GameObject($"地块 {x:00},{y:00}"); go.transform.SetParent(terrain.transform, false); go.transform.localPosition = new Vector3(x,y);
            var tile = go.AddComponent<ElementTile>(); tile.gridPosition = new Vector2Int(x,y); tile.earth = 3;
            bool border = x==0 || y==0 || x==world.width-1 || y==world.height-1;
            bool stones = x==9 && y>=11 && y<=14 || x==20 && y==4;
            bool pond = ((x-17)*(x-17)/12f + (y-10)*(y-10)/8f) < 1;
            bool river = y>=2 && y<=8 && x == 17 + (y<=4 ? -1 : 0);
            bool path = Mathf.Abs(y - (5+Mathf.Sin(x*.3f)*.8f))<.8f && x>1 && x<16;
            float tint = Mathf.PerlinNoise(x*.17f,y*.17f)*.07f;
            Render(go.transform,"草地",ground,Vector3.zero,0, new Color(.43f+tint,.57f+tint,.36f+tint));
            tile.elevation=4;
            if(path) Render(go.transform,"小径",ground,Vector3.zero,1,new Color(.64f,.61f,.43f));
            if(border||stones)
            {
                tile.blocksSimulation=true; tile.alwaysBlocksWalking=true; tile.earth=12;
                Render(go.transform,"岩石",rock,new Vector3(0,.08f),1000-y*10,Color.white);
            }
            else
            {
                if(pond||river) { tile.water=pond?12:5; tile.earth=5; tile.elevation=pond?0:1; }
                bool woods = (x>=3&&x<=7&&y>=10&&y<=15 || x>=12&&x<=15&&y>=12&&y<=15 || x>=3&&x<=5&&y>=2&&y<=3) && Mathf.PerlinNoise(x*.8f,y*.8f)>.35f;
                if(woods) { tile.wood=10+Mathf.PerlinNoise(x,y)*6; tile.earth=1; }
                if(x==11&&y==7) { tile.wood=3; tile.water=8; tile.earth=4; tile.elevation=1; }
                if(x==13&&y==7) { tile.wood=6; tile.gold=2; tile.earth=1; }
                if(x==7&&y==7) { tile.wood=5; tile.fire=1; tile.earth=2; }
                if(x==10&&y==4 || x==19&&y==14) tile.gold=4;
                if(!path && !pond && !river && (x*17+y*23)%5==0) Render(go.transform,"草簇",grass,new Vector3(.15f,-.1f),3,Color.white);
            }
            tile.waterView=Render(go.transform,"水",water,Vector3.zero,2,Color.white);
            tile.treeView=Render(go.transform,"木",tree,new Vector3(0,.45f),1000-y*10,Color.white);
            tile.goldView=Render(go.transform,"金",gold,new Vector3(.20f,-.10f),1001-y*10,Color.white);
            tile.fireView=Render(go.transform,"火",flame,new Vector3(0,.3f),1002-y*10,Color.white);
            tile.selectionView=Render(go.transform,"交互选择框",mark,Vector3.zero,4,Color.white); tile.selectionView.enabled=false;
            var collider=go.AddComponent<CircleCollider2D>(); collider.radius=border||stones?.45f:.34f; tile.walkingCollider=collider;
            tile.RefreshVisuals(tile.Initial, world.rules.WoodBlockThreshold);
        }
        var playerObject=new GameObject("玩家（WASD 自由移动）"); playerObject.transform.position=world.SpawnPosition;
        var body=playerObject.AddComponent<Rigidbody2D>(); body.gravityScale=0; body.constraints=RigidbodyConstraints2D.FreezeRotation;
        body.interpolation=RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        playerObject.AddComponent<CircleCollider2D>().radius=.19f;
        var player=playerObject.AddComponent<WorldCharacter>(); player.bodyView=Render(playerObject.transform,"人物",person,new Vector3(0,.22f),950,Color.white); world.player=player;
        var cameraObject=new GameObject("世界相机"); cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=6.3f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.17f,.25f,.21f);
        cameraObject.transform.position=new Vector3(12,6,-10);
        var follow=cameraObject.AddComponent<WorldCamera>(); follow.target=player.transform; follow.world=world;
        root.AddComponent<WorldHUD>().world=world;
        EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets(); return scene;
    }
}
#endif
