using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(100)]
public class RiverbankStartMenu : MonoBehaviour
{
    public string gameplayScene="RiverbankEmergenceV1";
    public Texture2D[] seasonalBackgrounds;
    public Font menuFont;
    public UnityEngine.TextCore.Text.FontAsset menuFontAsset;
    private VisualElement root,background,overlay;
    private Label selection,hint,message;
    private Button start,previous,next,honor,end,close;
    private bool loading;
    public RiverbankSeason Selection=>RiverbankMenuSession.SelectedSeason;
    private static readonly string[] classes={"season-spring","season-summer","season-autumn","season-winter"};
    private static readonly string[] hints={"春水初生，万物等待萌发。","夏日葱茏，河岸生机正盛。","秋色满岸，万物待你照料。","冬雪静落，为河岸留住生机。","进入游戏时随机选择四季；此处预览秋景。"};
    private void OnEnable()
    {
        if(!Application.isPlaying)return;
        root=GetComponent<UIDocument>().rootVisualElement;
        root.style.unityFontDefinition=new FontDefinition{fontAsset=menuFontAsset};
        background=root.Q<VisualElement>("Background");overlay=root.Q<VisualElement>("HonorOverlay");
        selection=root.Q<Label>("Select");hint=root.Q<Label>("SeasonHint");message=root.Q<Label>("MenuMessage");
        start=root.Q<Button>("Start");previous=root.Q<Button>("SeasonPrevious");next=root.Q<Button>("SeasonNext");
        honor=root.Q<Button>("Honor");end=root.Q<Button>("End");close=root.Q<Button>("HonorClose");
        start.clicked+=StartGame;previous.clicked+=PreviousSeason;next.clicked+=NextSeason;
        honor.clicked+=ShowHonors;end.clicked+=QuitGame;close.clicked+=CloseHonors;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        root.RegisterCallback<GeometryChangedEvent>(OnLayoutChanged);
        root.focusable=true;root.schedule.Execute(()=>start.Focus());
        RefreshSeason();
    }
    private void OnDisable()
    {
        if(start!=null)start.clicked-=StartGame;if(previous!=null)previous.clicked-=PreviousSeason;if(next!=null)next.clicked-=NextSeason;
        if(honor!=null)honor.clicked-=ShowHonors;if(end!=null)end.clicked-=QuitGame;if(close!=null)close.clicked-=CloseHonors;
        if(root!=null)root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        if(root!=null)root.UnregisterCallback<GeometryChangedEvent>(OnLayoutChanged);
    }
    public void NextSeason(){if(!loading){RiverbankMenuSession.Select((RiverbankSeason)(((int)Selection+1)%5));RefreshSeason();}}
    public void PreviousSeason(){if(!loading){RiverbankMenuSession.Select((RiverbankSeason)(((int)Selection+4)%5));RefreshSeason();}}
    private void RefreshSeason()
    {
        int index=Selection==RiverbankSeason.Random?2:(int)Selection;
        foreach(string value in classes)background.RemoveFromClassList(value);
        background.AddToClassList(classes[index]);
        if(seasonalBackgrounds!=null&&seasonalBackgrounds.Length==4)background.style.backgroundImage=new StyleBackground(seasonalBackgrounds[index]);
        selection.text="选择季节："+RiverbankMenuSession.SeasonNames[(int)Selection];hint.text=hints[(int)Selection];
    }
    public void StartGame()
    {
        if(loading)return;
        if(!Application.CanStreamedLevelBeLoaded(gameplayScene)){message.text="关卡尚未加入构建设置，请打开初始界面场景菜单完成配置。";return;}
        RiverbankMenuSession.BeginGame();loading=true;
        start.text="正在进入河岸…";start.SetEnabled(false);previous.SetEnabled(false);next.SetEnabled(false);honor.SetEnabled(false);end.SetEnabled(false);
        SceneManager.LoadSceneAsync(gameplayScene);
    }
    public void ShowHonors()
    {
        bool unlocked=PlayerPrefs.GetInt(RiverbankMenuSession.HonorKey,0)>0;
        root.Q<Label>("HonorTitle").text=unlocked?"河岸守护者":"尚未获得称号";
        root.Q<Label>("HonorDescription").text=unlocked?"你已完成河岸繁荣教学，让河岸持续焕发生机。":"完成河岸繁荣教学，解锁「河岸守护者」。";
        overlay.RemoveFromClassList("hidden");close.Focus();
    }
    public void CloseHonors(){overlay.AddToClassList("hidden");honor.Focus();}
    private void OnLayoutChanged(GeometryChangedEvent e)=>root.Q<VisualElement>("MenuRoot").EnableInClassList("compact",e.newRect.width<1100);
    private void OnKeyDown(KeyDownEvent e)
    {
        if(e.keyCode==KeyCode.Escape&&!overlay.ClassListContains("hidden")){CloseHonors();e.StopPropagation();}
        else if((e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow)&&overlay.ClassListContains("hidden"))
        {if(e.keyCode==KeyCode.LeftArrow)PreviousSeason();else NextSeason();e.StopPropagation();}
    }
    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
}
