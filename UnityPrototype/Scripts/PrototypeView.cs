using UnityEngine;

[RequireComponent(typeof(GameRuler))]
public class PrototypeView : MonoBehaviour
{
    private GameRuler ruler;
    private Font font;
    private GUIStyle title, heading, text, small, button, cellText;
    private int selectedCell = -1;
    private Element selectedElement;
    private bool inspectOnly = true, removing;
    private static readonly string[] Names = { "金", "木", "水", "火", "土" };
    private static readonly Color[] Colors = {
        new Color(0.94f, 0.77f, 0.36f), new Color(0.43f, 0.78f, 0.52f),
        new Color(0.38f, 0.71f, 0.95f), new Color(1f, 0.45f, 0.32f), new Color(0.72f, 0.55f, 0.39f) };
    private static readonly Color Background = new Color(0.055f, 0.075f, 0.095f);
    private static readonly Color Panel = new Color(0.105f, 0.135f, 0.16f);
    private static readonly Color Ink = new Color(0.88f, 0.90f, 0.88f);

    private void Awake()
    {
        ruler = GetComponent<GameRuler>();
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);
    }
    private void OnDestroy() { if (font != null) Destroy(font); }
    private GUIStyle Style(int size, bool bold = false)
    {
        var s = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
        s.normal.textColor = Ink; return s;
    }
    private void InitializeStyles()
    {
        if (title != null) return;
        title = Style(32, true); heading = Style(20, true); text = Style(16); small = Style(13);
        cellText = Style(13); cellText.alignment = TextAnchor.UpperLeft;
        button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16, padding = new RectOffset(8, 8, 5, 5) };
    }
    private static void Fill(Rect rect, Color color)
    {
        Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
    }
    private void Label(float x, float y, float w, float h, string value, GUIStyle style) => GUI.Label(new Rect(x, y, w, h), value, style);
    private bool Button(float x, float y, float w, string value) => GUI.Button(new Rect(x, y, w, 36), value, button);

    private void OnGUI()
    {
        if (ruler == null) return;
        InitializeStyles();
        Fill(new Rect(0, 0, Screen.width, Screen.height), Background);
        Matrix4x4 previous = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 820f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2,
            (Screen.height - 820 * scale) / 2, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        FiveElementSimulation sim = ruler.Simulation;
        LevelDefinition level = sim.Level;
        Label(24, 20, 620, 44, "五行机关园", title);
        Label(24, 66, 830, 30, "涌现 / 改变一个条件，观察整个系统如何回应", text);
        for (int l = 0; l < PrototypeLevels.Count; l++)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = ruler.LevelIndex == l ? Colors[0] : Color.gray;
            if (Button(24 + l * 280, 108, 264, PrototypeLevels.Names[l]))
            { ruler.LoadLevel(l); selectedCell = -1; inspectOnly = true; }
            GUI.backgroundColor = previousColor;
        }
        DrawBoard(sim);
        DrawDetails(sim);
        DrawTools();
        GUI.enabled = sim.Result == SimulationResult.Active;
        if (Button(24, 740, 120, ruler.IsRunning ? "继续运行中" : ruler.IsPlacement ? "启动" : "继续")) ruler.ResumeSimulation();
        if (Button(154, 740, 100, "暂停")) ruler.PauseSimulation();
        if (Button(264, 740, 100, "单步")) ruler.StepOnce();
        GUI.enabled = true;
        if (Button(374, 740, 110, ruler.Speed > 1 ? "速度 ×4" : "速度 ×1")) ruler.ToggleSpeed();
        if (Button(494, 740, 140, "重新布置")) ruler.ResetSimulation();
        if (Button(644, 740, 150, "加载金方案")) { ruler.LoadDemoPlan(); inspectOnly = true; }
        Label(24, 788, 1180, 24, ruler.LastMessage, small);
        GUI.matrix = previous;
    }
    private void DrawBoard(FiveElementSimulation sim)
    {
        LevelDefinition level = sim.Level;
        float size = Mathf.Min(90, Mathf.Min(850f / level.Width, 450f / level.Height));
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            int i = level.Index(x, y);
            Rect rect = new Rect(24 + x * size, 162 + (level.Height - 1 - y) * size, size - 4, size - 4);
            CellState c = sim.GetCell(i);
            Color color = level.Blocked[i] ? new Color(0.08f, 0.10f, 0.12f) : Panel;
            if (c.Water > 0.1f) color = Color.Lerp(color, new Color(0.12f, 0.30f, 0.44f), Mathf.Min(0.8f, c.Water / 12));
            if (c.Wood > 0.1f) color = Color.Lerp(color, new Color(0.20f, 0.37f, 0.22f), Mathf.Min(0.6f, c.Wood / 20));
            if (c.Fire > 0.01f) color = Color.Lerp(color, new Color(0.55f, 0.16f, 0.10f), 0.7f);
            if (i == selectedCell) Fill(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Colors[0]);
            Fill(rect, color);
            if (level.Blocked[i]) { Label(rect.x + 18, rect.y + 28, size - 30, 28, "岩壁", small); continue; }
            if (GUI.Button(rect, "", GUIStyle.none))
            {
                selectedCell = i;
                if (!inspectOnly) ruler.TryAdjustPlan(i, selectedElement, removing ? -1 : 1);
            }
            string marker = i == level.GoalIndex ? "水槽" : i == level.SourceIndex ? "水源" : i == level.ProtectedIndex ? "守护树" : $"{x},{y}";
            Label(rect.x + 6, rect.y + 3, size - 12, 22, marker, small);
            string values = "";
            for (int e = 0; e < 5; e++) if (c.Get((Element)e) > 0.001f) values += $"{Names[e]} {c.Get((Element)e):0.0}  ";
            if (i == level.GoalIndex) values = $"收水\n{sim.CollectedWater:0.0}/{level.TargetWater:0}";
            Label(rect.x + 6, rect.y + 25, size - 12, size - 29, values, cellText);
            if (c.Gold > 0) Fill(new Rect(rect.x + 5, rect.yMax - 5, rect.width - 10, 3), Colors[0]);
        }
        Label(24, 626, 860, 36, level.Hint, small);
    }
    private void DrawTools()
    {
        GUI.enabled = ruler.IsPlacement;
        for (int e = 0; e < 5; e++)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = !inspectOnly && (int)selectedElement == e ? Colors[e] : Color.gray;
            if (Button(24 + e * 158, 674, 148, $"{Names[e]} · 剩余 {ruler.Remaining((Element)e):0}"))
            { selectedElement = (Element)e; inspectOnly = false; removing = false; }
            GUI.backgroundColor = old;
        }
        GUI.enabled = true;
        if (Button(824, 674, 104, "只查看")) inspectOnly = true;
        GUI.enabled = ruler.IsPlacement;
        if (Button(938, 674, 126, removing ? "撤回已开启" : "撤回投放")) { removing = !removing; inspectOnly = false; }
        if (Button(1074, 674, 176, "清空自己的投放")) ruler.ClearPlan();
        GUI.enabled = true;
        Label(24, 713, 1220, 22, inspectOnly ? "查看模式：点击格子查看自身数量和四邻格影响。" : removing
            ? $"撤回模式：点击格子撤回自己投入的 1 份{Names[(int)selectedElement]}。"
            : $"投放模式：点击格子投入 1 份{Names[(int)selectedElement]}；启动后锁定布局。", small);
    }
    private void DrawDetails(FiveElementSimulation sim)
    {
        Fill(new Rect(944, 162, 306, 486), Panel);
        string state = sim.Result == SimulationResult.Won ? "已完成" : sim.Result == SimulationResult.Lost ? "未完成"
            : ruler.IsPlacement ? "布置阶段" : ruler.IsRunning ? "运行中" : "已暂停";
        Label(962, 178, 270, 30, state, heading);
        Label(962, 218, 270, 52, $"步数  {sim.StepCount} / {sim.Level.StepLimit}\n收水  {sim.CollectedWater:0.0} / {sim.Level.TargetWater:0}", text);
        if (sim.Result != SimulationResult.Active)
            Label(962, 282, 270, 72, sim.ResultReason + "\n点击重新布置可保留方案重试。", text);
        else if (selectedCell < 0)
            Label(962, 284, 270, 170, "选择一个格子查看数量。\n\n无火时，1 土 + 2 水 → 1 木。\n弱火能烧木；火势超过金的耐火阈值，才会损伤金。", text);
        else
        {
            CellState c = sim.GetCell(selectedCell);
            EnvironmentSnapshot env = sim.GetEnvironment(selectedCell);
            Label(962, 282, 270, 32, $"格子 {selectedCell % sim.Level.Width},{selectedCell / sim.Level.Width}", heading);
            string quantities = "";
            for (int e = 0; e < 5; e++) quantities += $"{Names[e]}  {c.Get((Element)e):0.00}     邻格 {env[(Element)e]:0.00}\n";
            Label(962, 316, 270, 132, quantities, text);
            string growth = c.Fire > 0.00001f ? "有火，停止生长" : c.Wood <= 0.00001f ? "没有木，不自动生长"
                : c.Earth < 1 ? "土不足 1，停止生长" : c.Water < 2 ? "水不足 2，停止生长" : "可消耗 1 土、2 水生长";
            float multiplier = 1 + c.CutExposure * sim.Settings.FatigueRate;
            Label(962, 454, 270, 88, $"{growth}\n切割倍率  ×{multiplier:0.00}\n金耐火阈值  {c.Gold * sim.Settings.GoldResistance:0.00}", small);
        }
        Label(962, 559, 270, 74, $"资源核对\n水误差  {sim.WaterLedgerError:0.0000}\n土误差  {sim.EarthLedgerError:0.0000}", small);
    }
}
