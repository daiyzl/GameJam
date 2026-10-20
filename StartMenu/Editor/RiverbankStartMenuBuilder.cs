#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class RiverbankStartMenuBuilder
{
    public const string ScenePath="Assets/Scenes/StartMenu.unity";
    private const string PanelPath="Assets/World/StartMenu/StartMenuPanel.asset";
    [MenuItem("五行世界/初始界面/打开初始界面")]
    public static void Open()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before opening the menu scene.");
        foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if(scene.isDirty)throw new InvalidOperationException("Save the edited scene before setting up the menu: "+scene.name);
        Directory.CreateDirectory("Assets/World/StartMenu");Directory.CreateDirectory("Assets/Scenes");AssetDatabase.Refresh();
        var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/World/StartEnum.uxml");
        if(tree==null)throw new InvalidOperationException("Provided StartEnum.uxml could not be imported.");
        var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if(panel==null){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,PanelPath);}
        panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1920,1080);
        panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;panel.match=.5f;EditorUtility.SetDirty(panel);
        panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/World/StartMenu/StartMenuTheme.tss");
        var target=File.Exists(ScenePath)?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var menu=UnityEngine.Object.FindObjectOfType<RiverbankStartMenu>();
        if(menu==null){var root=new GameObject("河岸繁荣 · 初始界面");root.AddComponent<UIDocument>();menu=root.AddComponent<RiverbankStartMenu>();}
        var document=menu.GetComponent<UIDocument>();document.panelSettings=panel;document.visualTreeAsset=tree;
        menu.seasonalBackgrounds=new[]{"spring","summer","autumn","winter"}.Select(s=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/World/WorldArtExpansionV1/Backgrounds/season_"+s+".png")).ToArray();
        if(menu.seasonalBackgrounds.Any(t=>t==null))throw new InvalidOperationException("The four supplied seasonal backgrounds are required.");
        menu.gameplayScene="RiverbankEmergenceV1";
        menu.menuFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/World/StartMenu/Fonts/NotoSansCJKsc-Regular.otf");
        if(menu.menuFont==null)throw new InvalidOperationException("Import the bundled Chinese font before opening the menu.");
        menu.menuFontAsset=CreateMenuFont(menu.menuFont);
        AssetDatabase.ImportAsset("Assets/World/StartMenu.uss",ImportAssetOptions.ForceUpdate);
        string textSettingsPath="Assets/World/StartMenu/StartMenuTextSettings.asset";
        var textSettings=AssetDatabase.LoadAssetAtPath<PanelTextSettings>(textSettingsPath);
        if(textSettings==null){textSettings=ScriptableObject.CreateInstance<PanelTextSettings>();AssetDatabase.CreateAsset(textSettings,textSettingsPath);}
        textSettings.defaultFontAsset=menu.menuFontAsset;panel.textSettings=textSettings;EditorUtility.SetDirty(panel);EditorUtility.SetDirty(textSettings);
        if(UnityEngine.Object.FindObjectOfType<Camera>()==null)
        {
            var camera=new GameObject("Main Camera");camera.tag="MainCamera";var view=camera.AddComponent<Camera>();
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.07f,.1f,.07f);view.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
            camera.AddComponent<AudioListener>();
        }
        EditorSceneManager.SaveScene(target,ScenePath);AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/RiverbankEmergenceV1.unity",true)};
        Selection.activeGameObject=menu.gameObject;
    }
    private static UnityEngine.TextCore.Text.FontAsset CreateMenuFont(Font source)
    {
        const string path="Assets/World/StartMenu/Fonts/StartMenuFont.asset";
        var asset=AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(path);
        string sourceText=File.ReadAllText("Assets/World/StartEnum.uxml")+File.ReadAllText("Assets/Scripts/StartMenu/RiverbankStartMenu.cs")+File.ReadAllText("Assets/Scripts/EmergenceV1/V1Honors.cs");
        string characters=new string(sourceText.Where(c=>!char.IsControl(c)).Distinct().ToArray());
        if(asset!=null&&characters.All(c=>asset.HasCharacter(c))){asset.atlasPopulationMode=UnityEngine.TextCore.Text.AtlasPopulationMode.Static;return asset;}
        bool creating=asset==null;
        if(creating)asset=UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(source,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic,true);
        asset.atlasPopulationMode=UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic;asset.isMultiAtlasTexturesEnabled=true;
        var serializedFont=new SerializedObject(asset);serializedFont.FindProperty("m_SourceFontFile").objectReferenceValue=source;serializedFont.ApplyModifiedPropertiesWithoutUndo();
        if(!asset.TryAddCharacters(characters,out string missing)||!string.IsNullOrWhiteSpace(missing))throw new InvalidOperationException("Menu font glyphs unavailable: "+missing);
        asset.atlasPopulationMode=UnityEngine.TextCore.Text.AtlasPopulationMode.Static;asset.name="StartMenuFont";
        if(creating)AssetDatabase.CreateAsset(asset,path);
        if(!AssetDatabase.Contains(asset.material))AssetDatabase.AddObjectToAsset(asset.material,asset);
        foreach(var texture in asset.atlasTextures){texture.name="StartMenuFont Atlas";if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,asset);EditorUtility.SetDirty(texture);}
        EditorUtility.SetDirty(asset);return asset;
    }
}
#endif
