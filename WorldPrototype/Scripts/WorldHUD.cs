using UnityEngine;

public class WorldHUD : MonoBehaviour
{
    public ElementWorld world;
    private Font font;
    private GUIStyle title, text, small, button;
    private void Awake() { font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16); }
    private void OnDestroy() { if (font != null) Destroy(font); }
    private GUIStyle Make(int size, bool bold = false)
    {
        var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
        style.normal.textColor = new Color(0.95f, 0.94f, 0.86f); return style;
    }
    private void OnGUI()
    {
        if (world == null || world.Simulation == null) return;
        if (title == null)
        {
            title = Make(25, true); text = Make(16); small = Make(13);
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15 };
        }
        float scale = Mathf.Max(0.7f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        Matrix4x4 matrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = Screen.height / scale;
        Color previous = GUI.color; GUI.color = new Color(0.06f, 0.12f, 0.10f, 0.90f);
        GUI.DrawTexture(new Rect(18, 18, 348, 150), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(w - 302, 18, 284, 236), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(18, h - 72, w - 36, 54), Texture2D.whiteTexture); GUI.color = previous;
        GUI.Label(new Rect(32, 26, 314, 38), "五行小世界", title);
        GUI.Label(new Rect(32, 68, 314, 28), "自由行走 · 环境模拟", text);
        GUI.Label(new Rect(32, 101, 314, 50), $"世界第 {world.Simulation.StepCount} 步  /  {(world.simulateAutomatically ? "运转中" : "已暂停")}\n随身元素：{ElementWorld.ElementName(world.SelectedElement)}", text);
        if (GUI.Button(new Rect(32, 178, 108, 32), world.simulateAutomatically ? "暂停世界" : "继续世界", button)) world.simulateAutomatically = !world.simulateAutomatically;
        if (GUI.Button(new Rect(146, 178, 84, 32), "单步", button)) world.StepOnce();
        if (GUI.Button(new Rect(236, 178, 112, 32), "重置世界", button)) world.ResetWorld();
        string info = "走近地块查看环境";
        if (world.SelectedTile != null)
        {
            var tile = world.SelectedTile; var c = tile.Current;
            info = $"面前地块  ({tile.gridPosition.x}, {tile.gridPosition.y})\n金  {c.Gold:0.0}       木  {c.Wood:0.0}\n水  {c.Water:0.0}       火  {c.Fire:0.0}\n土  {c.Earth:0.0}\n\n{(tile.blocksSimulation ? "岩石边界" : "按 E 加入随身元素")}";
        }
        GUI.Label(new Rect(w - 286, 30, 250, 192), info, text);
        GUI.Label(new Rect(w - 286, 219, 250, 28), $"水账误差 {world.Simulation.WaterLedgerError:0.000}", small);
        GUI.Label(new Rect(32, h - 63, w - 64, 24), "WASD / 方向键移动    1–5 选择金木水火土    E 投放    空格暂停    . 单步    R 重置", text);
        GUI.Label(new Rect(32, h - 38, w - 64, 22), world.Message, small);
        GUI.matrix = matrix;
    }
}
