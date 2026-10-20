#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class V1CanvasBuilder
{
    private static Font font;
    private static readonly Color Ink=new Color(.92f,.9f,.79f),Gold=new Color(.86f,.71f,.4f),Muted=new Color(.62f,.7f,.61f);
    private const string Root="Assets/World/EmergenceV1/Art/";
    private static Sprite Frame(string name,Color fill,Color edge)
    {
        string path=Root+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(saved!=null)return saved;
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name=name+" Texture",filterMode=FilterMode.Point};
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            int d=Mathf.Min(x,y,31-x,31-y);bool corner=(x<3||x>28)&&(y<3||y>28);
            Color color=corner?Color.clear:d<2?edge:d==2?Color.Lerp(edge,fill,.65f):Color.Lerp(fill*.9f,fill*1.1f,y/31f);
            if(!corner&&d>=2)color.a=fill.a;
            texture.SetPixel(x,y,color);
        }
        texture.Apply();var sprite=Sprite.Create(texture,new Rect(0,0,32,32),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(8,8,8,8));sprite.name=name;
        Directory.CreateDirectory(Root);AssetDatabase.CreateAsset(sprite,path);AssetDatabase.AddObjectToAsset(texture,sprite);return sprite;
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;
        r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    private static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,bool field=false)
    {
        var r=Rect(parent,name,anchor,pos,size);var image=r.gameObject.AddComponent<Image>();image.raycastTarget=false;image.type=Image.Type.Sliced;
        image.sprite=Frame(field?"UIInset":"UIPanel",field?new Color(.055f,.1f,.082f,.99f):new Color(.075f,.13f,.105f,.96f),field?new Color(.3f,.39f,.3f):new Color(.52f,.47f,.3f));return r;
    }
    private static Text Text(Transform parent,string name,float x,float y,float width,float height,int size,string text="",Color? color=null)
    {
        var r=Rect(parent,name,new Vector2(0,1),new Vector2(x,y),new Vector2(width,height));var t=r.gameObject.AddComponent<Text>();
        t.font=font;t.fontSize=size;t.color=color??Ink;t.text=text;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;
    }
    private static Button Button(Transform parent,string name,float x,float y,float width,float height,string text,bool primary=false)
    {
        var r=Rect(parent,name,new Vector2(0,1),new Vector2(x,y),new Vector2(width,height));var image=r.gameObject.AddComponent<Image>();
        image.sprite=Frame(primary?"UIPrimary":"UIButton",primary?new Color(.62f,.47f,.22f):new Color(.14f,.24f,.18f),primary?new Color(.95f,.8f,.48f):new Color(.39f,.5f,.35f));image.type=Image.Type.Sliced;
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.24f,1.18f,.95f);colors.selectedColor=new Color(1.13f,1.10f,.9f);colors.pressedColor=new Color(.75f,.8f,.72f);colors.disabledColor=new Color(.55f,.6f,.55f,.65f);colors.fadeDuration=.12f;button.colors=colors;
        var label=Text(r,"Label",8,-3,width-16,height-6,24,text,primary?new Color(1,.95f,.77f):Ink);label.alignment=TextAnchor.MiddleCenter;return button;
    }
    private static RectTransform Overlay(Transform parent,string name)
    {
        var r=Rect(parent,name,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        var image=r.gameObject.AddComponent<Image>();image.color=new Color(.015f,.035f,.027f,.84f);image.raycastTarget=true;return r;
    }
    private static InputField Input(Transform parent,string name,float x,float y,string value)
    {
        var r=Panel(parent,name,new Vector2(0,1),new Vector2(x,y),new Vector2(260,56),true);var image=r.GetComponent<Image>();image.raycastTarget=true;
        var input=r.gameObject.AddComponent<InputField>();input.targetGraphic=image;input.contentType=InputField.ContentType.IntegerNumber;input.characterLimit=5;
        input.textComponent=Text(r,"Value",16,-8,228,40,28);input.textComponent.supportRichText=false;
        input.caretColor=Gold;input.customCaretColor=true;input.selectionColor=new Color(.47f,.56f,.31f,.6f);
        var colors=input.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.12f,1.12f,.97f);colors.selectedColor=new Color(1.2f,1.18f,1);input.colors=colors;
        input.text=value;return input;
    }
    public static void Rebuild(V1World w)
    {
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/World/StartMenu/Fonts/NotoSansCJKsc-Regular.otf");
        if(font==null)throw new System.InvalidOperationException("Bundled Chinese font is required for game UI.");
        if(w.hud!=null)Object.DestroyImmediate(w.hud.gameObject);
        var go=new GameObject("V1 Canvas · 河岸界面",typeof(RectTransform));go.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        go.AddComponent<GraphicRaycaster>();var h=go.AddComponent<V1HUD>();h.world=w;h.uiRevision=5;w.hud=h;
        var head=Panel(go.transform,"目标与时间",new Vector2(0,1),new Vector2(24,-20),new Vector2(1872,148));
        h.title=Text(head,"Title",24,-12,330,50,34,"河岸繁荣",Gold);h.status=Text(head,"Status",365,-9,930,76,21);
        h.toolsButton=Button(head,"Tools",24,-87,200,44,"五行工具 · Tab");h.endButton=Button(head,"EndGame",242,-87,160,44,"结束游戏");h.restartButton=Button(head,"Restart",420,-87,150,44,"重开 · R");
        h.clockLabel=Text(head,"Clock",1310,-18,535,42,25,"",Muted);h.advanceButton=Button(head,"Advance",1310,-77,535,54,"渡过时段",true);h.timeImage=null;
        var tutorial=Panel(go.transform,"教学目标",new Vector2(0,1),new Vector2(24,-188),new Vector2(460,200));h.tutorialPanel=tutorial.gameObject;
        Text(tutorial,"Eyebrow",20,-12,420,28,18,"河 岸 初 课",Gold);h.tutorial=Text(tutorial,"Tutorial",20,-51,420,130,24);
        var footer=Panel(go.transform,"操作说明",new Vector2(.5f,0),new Vector2(0,18),new Vector2(1872,112));h.feedback=Text(footer,"Feedback",20,-14,1832,86,23);
        var read=Panel(go.transform,"地块数据",new Vector2(0,0),new Vector2(24,150),new Vector2(1270,96));h.readout=Text(read,"Readout",18,-12,1234,76,22);
        var village=Panel(go.transform,"村庄详情",new Vector2(1,1),new Vector2(-24,-188),new Vector2(470,430));h.villagePanel=village.gameObject;h.villageDetails=Text(village,"Details",22,-20,426,395,24);village.gameObject.SetActive(false);
        var tools=Overlay(go.transform,"五行投放");h.toolsPage=tools.gameObject;
        var page=Panel(tools,"ToolCard",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1720,930));
        Text(page,"Heading",36,-22,1430,56,36,"五行工具",Gold);h.closeToolsButton=Button(page,"Close",1520,-26,160,50,"返回 · Tab");
        Text(page,"Hint",36,-88,1630,66,23,"选择元素后，可在下方输入地图坐标直接投放。\n返回河岸后，也可以在人物附近点击地面或按 E 投放。",Muted);
        string[] names={"金","木 · 播种","水","火","土 · 养分"};
        string[] rules={"富集成矿，供村庄开采\n过量的金会压制草木","每次播种1粒\n土水充足、密度合适才萌发","补充水分，灌溉与灭火\n过量会形成积水","污染助燃，火势沿污染蔓延\n树林与村庄也会受伤","补充养分，帮助植物生长\n垫高和挖低另用地形工具"};
        h.elementButtons=new Button[5];for(int i=0;i<5;i++)
        {
            var card=Panel(page,"Element"+i,new Vector2(0,1),new Vector2(36+i*330,-174),new Vector2(314,256),true);
            Text(card,"Number",20,-14,270,28,18,"0"+(i+1),Muted);Text(card,"Name",20,-52,274,44,28,names[i],Gold);
            Text(card,"Rules",20,-106,274,88,22,rules[i]);h.elementButtons[i]=Button(card,"Select",20,-198,274,42,"选择");
        }
        h.amountLabel=Text(page,"Amount",36,-462,1010,48,24);h.amountButtons=new Button[3];for(int i=0;i<3;i++)h.amountButtons[i]=Button(page,"Amount"+i,1080+i*202,-460,186,46,new[]{"1 份","4 份","8 份"}[i]);
        var coords=Panel(page,"Coordinates",new Vector2(0,1),new Vector2(36,-532),new Vector2(1648,174),true);
        Text(coords,"Heading",24,-12,370,40,26,"坐标投放",Gold);Text(coords,"Hint",400,-20,1190,30,20,"使用地图显示的 (X, Y) 坐标，支持负数。",Muted);
        Text(coords,"XLabel",24,-78,35,50,25,"X");h.coordinateX=Input(coords,"CoordinateX",68,-72,"7");
        Text(coords,"YLabel",370,-78,35,50,25,"Y");h.coordinateY=Input(coords,"CoordinateY",414,-72,"6");
        h.coordinateButton=Button(coords,"PlaceCoordinates",724,-72,270,56,"投放到坐标",true);h.coordinateFeedback=Text(coords,"CoordinateFeedback",1020,-72,600,78,21,"投放不推进时间。",Muted);
        h.raiseButton=Button(page,"Raise",36,-742,300,54,"地形 · 垫高");h.lowerButton=Button(page,"Lower",354,-742,300,54,"地形 · 挖低");h.rainButton=Button(page,"Rain",672,-742,420,54,"安排下一时段全图降雨");
        Text(page,"TerrainHelp",36,-826,1630,56,21,"垫高／挖低每次改变高度0.2，不直接增加养分。投放和安排降雨后，点击「渡过时段」结算环境变化。",Muted);
        tools.gameObject.SetActive(false);
        var result=Overlay(go.transform,"结束结算");h.resultPage=result.gameObject;
        var end=Panel(result,"ResultCard",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1480,860));
        Text(end,"Eyebrow",60,-30,1360,36,21,"河 岸 纪 事",Gold).alignment=TextAnchor.MiddleCenter;
        Text(end,"Heading",60,-88,1360,76,50,"这一程，万物留下回响").alignment=TextAnchor.MiddleCenter;
        Text(end,"TimeLabel",60,-190,1360,30,21,"已 渡 过 的 总 时 间",Muted).alignment=TextAnchor.MiddleCenter;
        h.resultTime=Text(end,"Elapsed",60,-232,1360,68,46,"",Gold);h.resultTime.alignment=TextAnchor.MiddleCenter;
        h.resultSummary=Text(end,"Summary",60,-312,1360,36,22);h.resultSummary.alignment=TextAnchor.MiddleCenter;
        h.honorButtons=new Button[8];for(int i=0;i<8;i++)h.honorButtons[i]=Button(end,"Honor"+i,60+(i%4)*340,-378-(i/4)*118,320,94,"",true);
        h.resultEmpty=Text(end,"NoHonors",60,-418,1360,120,27,"本局尚未获得称号。\n每一次尝试，都会留下新的河岸故事。",Muted);h.resultEmpty.alignment=TextAnchor.MiddleCenter;
        var hint=Panel(end,"HonorCondition",new Vector2(0,1),new Vector2(60,-634),new Vector2(1360,102),true);h.honorHint=Text(hint,"Condition",22,-15,1316,76,23);
        h.resultMenuButton=Button(end,"ReturnMenu",60,-772,420,56,"返回初始界面",true);h.resultRestartButton=Button(end,"RestartRun",530,-772,420,56,"再开一局");h.resultQuitButton=Button(end,"Quit",1000,-772,420,56,"退出游戏");result.gameObject.SetActive(false);
        if(Object.FindObjectOfType<EventSystem>()==null){var events=new GameObject("EventSystem");events.AddComponent<EventSystem>();events.AddComponent<StandaloneInputModule>();}
        AssetDatabase.SaveAssets();
    }
}
#endif
