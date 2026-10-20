#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PixelArtPackageBuilder
{
    private const string Root="Assets/World/PixelArtV1/";
    [Serializable] private class AssetCheck { public string name; public int width,height,spriteCount; public bool pointFilter,noCompression,noMipmaps; }
    [Serializable] private class Report { public string status; public int textureCount,spriteCount,prefabCount,animationCount; public AssetCheck[] assets; }
    private static Sprite[] Sprites(string name) => AssetDatabase.LoadAllAssetsAtPath(Root+"Sprites/"+name+".png").OfType<Sprite>().ToArray();
    public static void Build()
    {
        Directory.CreateDirectory(Root+"Prefabs"); Directory.CreateDirectory(Root+"Animations"); Directory.CreateDirectory(Root+"Materials"); AssetDatabase.Refresh();
        string materialPath=Root+"Materials/PixelSprite.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material,materialPath);
        }
        string[] sheets={"sapling","tree","forest","mountain","character","ground_elements","water","rain","fire"};
        foreach(string sheet in sheets) foreach(Sprite sprite in Sprites(sheet))
        {
            string path=Root+"Prefabs/"+sprite.name+".prefab";
            if(File.Exists(path)) continue;
            var go=new GameObject(sprite.name); var view=go.AddComponent<SpriteRenderer>(); view.sprite=sprite; view.sharedMaterial=material;
            if(sheet=="rain") go.transform.localScale=Vector3.one*.25f;
            PrefabUtility.SaveAsPrefabAsset(go,path); UnityEngine.Object.DestroyImmediate(go);
        }
        Sprite[] character=Sprites("character");
        foreach(string dir in new[]{"down","left","right","up"})
        {
            Sprite[] poses=Enumerable.Range(0,3).Select(i=>character.Single(s=>s.name=="character_"+dir+"_"+i)).ToArray();
            Clip("character_idle_"+dir,new[]{poses[0]},1,true);
            Clip("character_walk_"+dir,new[]{poses[1],poses[0],poses[2],poses[0]},8,true);
        }
        Clip("fire_loop",Sprites("fire").OrderBy(s=>s.name).ToArray(),8,true);
        Sprite[] rain=Sprites("rain");
        Clip("splash",new[]{rain.Single(s=>s.name=="splash_0"),rain.Single(s=>s.name=="splash_1")},8,false);
        Clip("ripple",new[]{rain.Single(s=>s.name=="ripple_0"),rain.Single(s=>s.name=="ripple_1")},5,false);
        AssetDatabase.SaveAssets();
    }
    private static void Clip(string name,Sprite[] frames,float fps,bool loop)
    {
        string path=Root+"Animations/"+name+".anim"; if(File.Exists(path)) return;
        var clip=new AnimationClip {name=name,frameRate=fps};
        var keys=new ObjectReferenceKeyframe[frames.Length+1];
        for(int i=0;i<frames.Length;i++) keys[i]=new ObjectReferenceKeyframe {time=i/fps,value=frames[i]};
        keys[frames.Length]=new ObjectReferenceKeyframe {time=frames.Length/fps,value=frames[frames.Length-1]};
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
        var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop; AnimationUtility.SetAnimationClipSettings(clip,settings);
        AssetDatabase.CreateAsset(clip,path);
    }
    public static void RunBatch()
    {
        PixelArtImporter.ConfigureAll();
        string[] names={"sapling","tree","forest","mountain","character","ground_elements","water","rain","fire"};
        int[] counts={1,1,1,1,12,6,9,6,4}; var checks=new List<AssetCheck>(); int total=0;
        for(int i=0;i<names.Length;i++)
        {
            string path=Root+"Sprites/"+names[i]+".png"; var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path); Sprite[] sprites=Sprites(names[i]);
            if(sprites.Length!=counts[i]) throw new InvalidOperationException(names[i]+": incorrect sprite count "+sprites.Length);
            if(importer.filterMode!=FilterMode.Point||importer.textureCompression!=TextureImporterCompression.Uncompressed||importer.mipmapEnabled)
                throw new InvalidOperationException(names[i]+": incorrect import settings");
            foreach(Sprite sprite in sprites) if(sprite.rect.width<=0||sprite.rect.height<=0) throw new InvalidOperationException("Empty sprite: "+sprite.name);
            total+=sprites.Length; checks.Add(new AssetCheck {name=names[i],width=texture.width,height=texture.height,spriteCount=sprites.Length,pointFilter=true,noCompression=true,noMipmaps=true});
        }
        var report=new Report {status="passed",textureCount=names.Length,spriteCount=total,prefabCount=Directory.GetFiles(Root+"Prefabs","*.prefab").Length,
            animationCount=Directory.GetFiles(Root+"Animations","*.anim").Length,assets=checks.ToArray()};
        string[] args=Environment.GetCommandLineArgs(); string output="";
        for(int i=0;i<args.Length-1;i++) if(args[i]=="-pixelArtReport") output=args[i+1];
        if(output!="") { Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output,JsonUtility.ToJson(report,true)); }
        Debug.Log("PIXEL ART PASS: "+total+" sprites, "+report.prefabCount+" prefabs, "+report.animationCount+" animations");
    }
}
#endif
