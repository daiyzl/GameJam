using UnityEngine;
using UnityEngine.UI;

public class TutorialHUD : MonoBehaviour
{
    public TutorialWorld world;
    public Text title,instruction,values,progress,feedback;
    public Button pauseButton,retryButton,restartButton;
    private Font font;
    private void Awake()
    {
        font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
        foreach(Text text in GetComponentsInChildren<Text>(true))text.font=font;
        pauseButton.onClick.AddListener(()=>world.Paused=!world.Paused);
        retryButton.onClick.AddListener(()=>world.RetryStation());
        restartButton.onClick.AddListener(()=>world.ResetLevel());
    }
    private void Update()
    {
        if(world.Simulation==null)return;
        title.text=world.LessonTitle;instruction.text=world.Instruction;
        Vector3Int cell=world.CellAtWorld(world.player.Position);CellState c=world.ReadCell(world.TargetCell);
        values.text=$"目标地块 {world.TargetCell.x},{world.TargetCell.y}\n金 {c.Gold:0.0}   木 {c.Wood:0.0}\n水 {c.Water:0.0}   火 {c.Fire:0.0}   土 {c.Earth:0.0}\n\n脚下格子 {cell.x},{cell.y}   已选：{ElementWorld.ElementName(world.SelectedElement)}";
        string[] lessons={"滋养树苗","金伐木","水灭火","火燃木生土","雨中再生"};string text="村口试炼\n";
        for(int i=0;i<lessons.Length;i++)text+=(world.Stage>i?"✓ ":world.Stage==i?"→ ":"· ")+lessons[i]+"\n";
        progress.text=text;
        feedback.text=$"{world.Message}\nWASD 移动   1–5 金木水火土   E 投放   Q 唤雨   T 重试本站   R 重开   空格暂停";
        pauseButton.GetComponentInChildren<Text>().text=world.Paused?"继续模拟":"暂停模拟";
    }
    private void OnDestroy() {if(font!=null)Destroy(font);}
}
