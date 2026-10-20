#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Art-only setup; never changes scenes, simulation settings or existing assets.
[InitializeOnLoad]
public sealed class WorldArtExpansionImporter : AssetPostprocessor
{
    private const string Root = "Assets/World/WorldArtExpansionV1/";
    private static bool queued, building;
    private static readonly string[] Sprites = {
        "gold_mine_mountain", "gold_ore_exposed", "gold_mine_abandoned",
        "bridge_wood", "bridge_stone", "ground_dead", "pollution_fog",
        "village_medium", "village_large", "water_polluted", "soil_slope", "river_sediment_dam"
    };
    private static readonly string[] Backgrounds = {
        "season_spring", "season_summer", "season_autumn", "season_winter",
        "background_morning", "background_noon", "background_evening"
    };

    static WorldArtExpansionImporter() { QueueBuild(); }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, StringComparison.Ordinal) ||
            !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
        var importer = (TextureImporter)assetImporter;
        if (importer.userData == "WorldArtExpansionV1.configured") return;
        Configure(importer);
    }

    private static void Configure(TextureImporter importer)
    {
        string name = Path.GetFileNameWithoutExtension(importer.assetPath);
        byte[] bytes = File.ReadAllBytes(importer.assetPath);
        int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        float units = name == "ground_dead" || name == "water_polluted" ? 1f : name == "pollution_fog" ? 1.5f :
            name == "soil_slope" ? 2f : name == "river_sediment_dam" ? 3f :
            name.StartsWith("bridge_", StringComparison.Ordinal) ? 3f :
            name == "village_medium" ? 4.5f : name == "village_large" ? 6f :
            name == "gold_ore_exposed" ? 1.5f :
            name.StartsWith("gold_", StringComparison.Ordinal) ? 3f : 16f;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = width / units;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.wrapMode = name == "ground_dead" || name == "water_polluted" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(.5f, .5f);
        importer.SetTextureSettings(settings);
        importer.userData = "WorldArtExpansionV1.configured";
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        if (!building && imported.Any(p => p.StartsWith(Root, StringComparison.Ordinal)
            && p.EndsWith(".png", StringComparison.OrdinalIgnoreCase))) QueueBuild();
    }

    private static void QueueBuild()
    {
        if (queued || building) return;
        queued = true;
        EditorApplication.delayCall += () => { queued = false; BuildMissing(); };
    }

    [MenuItem("五行世界/扩展美术/配置导入并创建缺失预制体")]
    public static void BuildMissing()
    {
        if (building || !Directory.Exists(Root + "Sprites")) return;
        building = true;
        try
        {
            Directory.CreateDirectory(Root + "Prefabs");
            Directory.CreateDirectory(Root + "Tiles");
            Directory.CreateDirectory(Root + "Animations");
            Directory.CreateDirectory(Root + "Materials");
            string materialPath = Root + "Materials/PixelSprite.mat";
            if (!File.Exists(materialPath))
            {
                string existing = "Assets/World/PixelArtV1/Materials/PixelSprite.mat";
                if (File.Exists(existing)) AssetDatabase.CopyAsset(existing, materialPath);
                else
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                    if (shader != null) AssetDatabase.CreateAsset(new Material(shader), materialPath);
                }
            }
            var report = new ArtReport();
            foreach (string name in Sprites.Concat(Backgrounds))
            {
                bool background = Backgrounds.Contains(name);
                string path = Root + (background ? "Backgrounds/" : "Sprites/") + name + ".png";
                if (!File.Exists(path)) { report.missing++; continue; }
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { AssetDatabase.ImportAsset(path); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
                if (importer == null) { report.missing++; continue; }
                if (importer.userData != "WorldArtExpansionV1.configured") { Configure(importer); importer.SaveAndReimport(); }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) { report.missing++; continue; }
                report.spriteCount++;
                if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed) report.importErrors++;

                string prefabPath = Root + "Prefabs/" + name + ".prefab";
                if (!File.Exists(prefabPath))
                {
                    var go = new GameObject(name);
                    try
                    {
                        var renderer = go.AddComponent<SpriteRenderer>();
                        renderer.sprite = sprite;
                        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (material != null) renderer.sharedMaterial = material;
                        renderer.sortingOrder = background ? -100 : name == "ground_dead" ? -5 : name == "water_polluted" ? -3 : 10;
                        if (name == "pollution_fog")
                        {
                            renderer.sortingOrder = 30;
                            renderer.color = new Color(.18f, .18f, .2f, .55f);
                            AddFogLoop(go);
                        }
                        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null && prefab.GetComponent<SpriteRenderer>() != null &&
                    prefab.GetComponent<SpriteRenderer>().sprite != null) report.prefabCount++;
            }

            foreach (string name in new[] { "ground_dead", "water_polluted" })
            {
                string tilePath = Root + "Tiles/" + name + ".asset";
                if (!File.Exists(tilePath))
                {
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Sprites/" + name + ".png");
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
            }
            report.tileReady = AssetDatabase.LoadAssetAtPath<Tile>(Root + "Tiles/ground_dead.asset") != null;
            report.waterTileReady = AssetDatabase.LoadAssetAtPath<Tile>(Root + "Tiles/water_polluted.asset") != null;
            int expected = Sprites.Length + Backgrounds.Length;
            report.passed = report.spriteCount == expected && report.prefabCount == expected && report.tileReady && report.waterTileReady && report.importErrors == 0 && report.missing == 0;
            AssetDatabase.SaveAssets();
            File.WriteAllText(Root + "import-report.json", JsonUtility.ToJson(report, true));
            AssetDatabase.ImportAsset(Root + "import-report.json");
            Debug.Log("扩展美术配置完成：" + report.spriteCount + " Sprites, " + report.prefabCount + " Prefabs, passed=" + report.passed);
        }
        finally { building = false; }
    }

    private static void AddFogLoop(GameObject go)
    {
        string path = Root + "Animations/pollution_fog_loop.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = "pollution_fog_loop", legacy = true, wrapMode = WrapMode.Loop, frameRate = 12 };
            var alpha = new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1.5f, .6f), new Keyframe(3, .4f));
            clip.SetCurve("", typeof(SpriteRenderer), "m_Color.a", alpha);
            var scale = new AnimationCurve(new Keyframe(0, .98f), new Keyframe(1.5f, 1.02f), new Keyframe(3, .98f));
            clip.SetCurve("", typeof(Transform), "localScale.x", scale);
            clip.SetCurve("", typeof(Transform), "localScale.y", scale);
            AssetDatabase.CreateAsset(clip, path);
        }
        var animation = go.AddComponent<Animation>();
        animation.AddClip(clip, clip.name);
        animation.clip = clip;
        animation.playAutomatically = true;
    }

    // Explicit command-line export; no dialogs, no scene loading or Play changes.
    public static void BuildAndExport()
    {
        BuildMissing();
        string[] args = Environment.GetCommandLineArgs();
        int option = Array.IndexOf(args, "--worldArtExport");
        if (option >= 0 && option + 1 < args.Length)
            AssetDatabase.ExportPackage(Root.TrimEnd('/'), args[option + 1], ExportPackageOptions.Recurse);
    }

    [Serializable] private class ArtReport
    {
        public int spriteCount, prefabCount, missing, importErrors;
        public bool tileReady, waterTileReady, passed;
    }
}
#endif
