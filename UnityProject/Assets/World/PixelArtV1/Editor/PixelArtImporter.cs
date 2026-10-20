#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// Import-only helper. Does not alter gameplay scripts or replace scene artwork.
public class PixelArtImporter : AssetPostprocessor
{
    private const string Prefix = "Assets/World/PixelArtV1/";
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Prefix, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
        var importer = (TextureImporter)assetImporter;
        if (importer.userData == "PixelArtV1.configured") return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = 512;
    }

    [MenuItem("五行世界/像素素材/配置导入与切片")]
    public static void ConfigureAll()
    {
        string[] names = { "sapling", "tree", "forest", "mountain", "character", "ground_elements", "water", "rain", "fire" };
        foreach (string name in names)
        {
            string path = Prefix + "Sprites/" + name + ".png";
            if (!File.Exists(path)) continue;
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;
            int cols = name == "fire" ? 2 : name == "character" || name == "ground_elements" || name == "water" || name == "rain" ? 3 : 1;
            int rows = name == "character" ? 4 : name == "ground_elements" || name == "rain" || name == "fire" ? 2 : name == "water" ? 3 : 1;
            float units = name == "forest" ? 5 : name == "mountain" ? 4 : name == "tree" ? 2 : name == "sapling" ? .7f : 1;
            importer.spritePixelsPerUnit = texture.width / (float)cols / units;
            if (cols * rows > 1)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var previous = provider.GetSpriteRects();
                var frames = new SpriteRect[cols * rows];
                string[] groundNames = { "ground_gold", "ground_wood", "ground_water", "ground_fire", "ground_earth", "ground_grass" };
                string[] waterNames = { "water_nw", "water_n", "water_ne", "water_w", "water_center", "water_e", "water_sw", "water_s", "water_se" };
                string[] directions = { "down", "left", "right", "up" };
                string[] rainNames = { "rain_streak", "rain_drop", "splash_0", "splash_1", "ripple_0", "ripple_1" };
                for (int row = 0; row < rows; row++) for (int col = 0; col < cols; col++)
                {
                    int index = row * cols + col;
                    int left = Mathf.RoundToInt(texture.width * col / (float)cols), right = Mathf.RoundToInt(texture.width * (col + 1) / (float)cols);
                    int bottom = Mathf.RoundToInt(texture.height * (rows-row-1) / (float)rows), top = Mathf.RoundToInt(texture.height * (rows-row) / (float)rows);
                    string frameName = name == "ground_elements" ? groundNames[index] : name == "water" ? waterNames[index] : name == "character" ? "character_" + directions[row] + "_" + col : name == "rain" ? rainNames[index] : name + "_" + index;
                    var old = previous.FirstOrDefault(r => r.name == frameName);
                    frames[index] = new SpriteRect { name = frameName, rect = new Rect(left,bottom,right-left,top-bottom), alignment = SpriteAlignment.Center, pivot = new Vector2(.5f,.5f), spriteID = old != null ? old.spriteID : GUID.Generate() };
                }
                provider.SetSpriteRects(frames);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(frames.Select(f => new SpriteNameFileIdPair(f.name,f.spriteID)));
                provider.Apply();
            }
            else importer.spriteImportMode = SpriteImportMode.Single;
            importer.userData = "PixelArtV1.configured";
            importer.SaveAndReimport();
        }
        AssetDatabase.SaveAssets(); Debug.Log("像素素材已配置为 Sprite、Point、无压缩，并完成图集切片。");
        PixelArtPackageBuilder.Build();
    }
}
#endif
