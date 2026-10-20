using UnityEngine;

public class GameRuler : MonoBehaviour
{
    [SerializeField, Range(0, 2)] private int initialLevel = 0;
    [SerializeField] private SimulationSettings settings = new SimulationSettings();
    private FiveElementSimulation simulation;
    private CellState[] plan;
    private float accumulator;
    public int LevelIndex { get; private set; }
    public FiveElementSimulation Simulation { get { EnsureInitialized(); return simulation; } }
    public bool IsRunning { get; private set; }
    public bool IsPlacement { get; private set; }
    public float Speed { get; private set; } = 1;
    public string LastMessage { get; private set; } = "";
    public int Width => Simulation.Level.Width;
    public int Height => Simulation.Level.Height;

    private void Awake() { Application.runInBackground = true; LoadLevel(initialLevel); }
    private void OnValidate() { if (settings != null) settings.Validate(); }
    private void Update()
    {
        if (!IsRunning) return;
        accumulator += Time.deltaTime * Speed;
        int count = 0;
        while (IsRunning && accumulator >= settings.StepSeconds && count++ < 16)
        {
            accumulator -= settings.StepSeconds;
            simulation.Step();
            if (simulation.Result != SimulationResult.Active) IsRunning = false;
        }
    }
    private void EnsureInitialized() { if (simulation == null) LoadLevel(initialLevel); }

    public void LoadLevel(int index)
    {
        LevelIndex = Mathf.Clamp(index, 0, PrototypeLevels.Count - 1);
        if (settings == null) settings = new SimulationSettings();
        simulation = new FiveElementSimulation(PrototypeLevels.Create(LevelIndex), settings);
        plan = new CellState[simulation.Level.Initial.Length];
        IsPlacement = true; IsRunning = false; accumulator = 0; LastMessage = "选择元素后点击可用格投放";
    }

    public float Remaining(Element element)
    {
        EnsureInitialized();
        float used = 0;
        for (int i = 0; i < plan.Length; i++) used += plan[i].Get(element);
        return simulation.Level.Budget[(int)element] - used;
    }
    public float Planned(int index, Element element) => plan != null && index >= 0 && index < plan.Length ? plan[index].Get(element) : 0;

    public bool TryAdjustPlan(int index, Element element, int change)
    {
        EnsureInitialized();
        if ((int)element < 0 || (int)element > 4 || change == 0) return false;
        if (!IsPlacement) { LastMessage = "运行后不能修改布置，请先重新布置"; return false; }
        if (index < 0 || index >= plan.Length || !simulation.Level.Placeable[index])
        { LastMessage = "该格不能投放"; return false; }
        float present = plan[index].Get(element);
        if (change > 0 && Remaining(element) + 0.0001f < change)
        { LastMessage = "该元素的投放预算不足"; return false; }
        if (change < 0 && present < -change)
        { LastMessage = "只能撤回自己投放的元素"; return false; }
        CellState c = plan[index]; c.Set(element, present + change); plan[index] = c;
        simulation.Reset(plan);
        LastMessage = change > 0 ? "已投放，可启动观察结果" : "已撤回投放";
        return true;
    }
    public void ClearPlan()
    {
        if (!IsPlacement) return;
        System.Array.Clear(plan, 0, plan.Length); ResetSimulation();
    }
    public void LoadDemoPlan()
    {
        ResetSimulation(); ClearPlan();
        int index = simulation.Level.Index(3, 1);
        for (int i = 0; i < 6; i++) TryAdjustPlan(index, Element.Gold, 1);
        LastMessage = "已加载可验证的金方案；点击启动，或逐步观察";
    }
    [ContextMenu("Simulation/Resume")]
    public void ResumeSimulation()
    {
        EnsureInitialized();
        if (simulation.Result != SimulationResult.Active) return;
        IsPlacement = false; IsRunning = true;
    }
    [ContextMenu("Simulation/Pause")]
    public void PauseSimulation() { IsRunning = false; accumulator = 0; }
    [ContextMenu("Simulation/Step once")]
    public void StepOnce()
    {
        EnsureInitialized(); PauseSimulation();
        if (simulation.Result != SimulationResult.Active) return;
        IsPlacement = false; simulation.Step();
    }
    [ContextMenu("Simulation/Reset (keep plan)")]
    public void ResetSimulation()
    {
        EnsureInitialized(); IsRunning = false; IsPlacement = true; accumulator = 0;
        simulation.Reset(plan); LastMessage = "已恢复初始世界，保留上次投放";
    }
    public void ToggleSpeed() { Speed = Speed < 2 ? 4 : 1; }
    public float GetCount(int x, int y, Element element) => Simulation.GetCell(x, y).Get(element);
    public EnvironmentSnapshot GetEnvironment(int x, int y) => Simulation.GetEnvironment(Simulation.Level.Index(x, y));
    public BaseElement GetElement(int x, int y, Element element)
    {
        CellState c = Simulation.GetCell(x, y);
        return new BaseElement(c.Get(element), element) { metalExposureSeconds = c.CutExposure };
    }
}
