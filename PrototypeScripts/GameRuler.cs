using System;
using System.Collections.Generic;
using UnityEngine;

public class GameRuler : MonoBehaviour
{
    [Serializable]
    public class ElementPlacement
    {
        public Vector2Int position;
        public Element element;
        [Min(0)] public int amount;

        public ElementPlacement(int x, int y, Element element, int amount)
        {
            position = new Vector2Int(x, y);
            this.element = element;
            this.amount = amount;
        }
    }

    [Header("Grid")]
    [SerializeField, Min(1)] private int width = 7;
    [SerializeField, Min(1)] private int height = 3;
    [SerializeField, Min(1)] private int maxAmountPerElement = 1000;
    [SerializeField] private List<ElementPlacement> initialElements = new List<ElementPlacement>
    {
        new ElementPlacement(3, 1, Element.Wood, 12),
        new ElementPlacement(3, 0, Element.Gold, 2),
        new ElementPlacement(3, 2, Element.Gold, 3),
        new ElementPlacement(2, 1, Element.Water, 8)
    };

    [Header("Simulation")]
    [SerializeField, Min(0.01f)] private float stepSeconds = 0.5f;
    [SerializeField] private bool startPaused = true;
    [SerializeField, Min(0f)] private float reactionRate = 0.4f;
    [SerializeField, Min(0f)] private float woodGrowthRate = 0.05f;
    [SerializeField, Min(0f)] private float metalFatiguePerSecond = 0.3f;
    [SerializeField, Min(1f)] private float maxMetalMultiplier = 4f;

    [Header("Scene preview")]
    [SerializeField, Min(0.1f)] private float cellSize = 1f;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool logEachStep = false;

    private const int ElementCount = 5;
    private BaseElement[,,] cells;
    private float accumulatedTime;

    public bool IsRunning { get; private set; }
    public int StepCount { get; private set; }
    public float SimulationSeconds { get; private set; }
    public int Width => cells == null ? width : cells.GetLength(0);
    public int Height => cells == null ? height : cells.GetLength(1);

    private void Awake()
    {
        ResetSimulation();
        if (!startPaused) ResumeSimulation();
    }

    private void Update()
    {
        if (!IsRunning) return;

        accumulatedTime += Time.deltaTime;
        float duration = Mathf.Max(0.01f, stepSeconds);
        // Limit catch-up work per frame, but retain the unprocessed time.
        int stepsThisFrame = 0;
        while (accumulatedTime >= duration && stepsThisFrame < 8)
        {
            SimulateStep(duration);
            accumulatedTime -= duration;
            stepsThisFrame++;
        }
    }

    [ContextMenu("Simulation/Reset from initial elements")]
    public void ResetSimulation()
    {
        ValidateSettings();
        IsRunning = false;
        accumulatedTime = 0f;
        StepCount = 0;
        SimulationSeconds = 0f;
        cells = new BaseElement[width, height, ElementCount];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        for (int e = 0; e < ElementCount; e++)
            cells[x, y, e] = new BaseElement(0, (Element)e);

        if (initialElements == null) return;
        foreach (ElementPlacement placement in initialElements)
        {
            if (placement == null || !Inside(placement.position.x, placement.position.y)
                || !ValidElement(placement.element)) continue;

            BaseElement target = cells[placement.position.x, placement.position.y, (int)placement.element];
            long sum = (long)target.count + Math.Max(0, placement.amount);
            target.count = (int)Math.Min(maxAmountPerElement, sum);
        }
    }

    [ContextMenu("Simulation/Resume")]
    public void ResumeSimulation()
    {
        EnsureInitialized();
        IsRunning = true;
    }

    [ContextMenu("Simulation/Pause")]
    public void PauseSimulation()
    {
        IsRunning = false;
        accumulatedTime = 0f;
    }

    [ContextMenu("Simulation/Step once")]
    public void StepOnce()
    {
        EnsureInitialized();
        PauseSimulation();
        SimulateStep(Mathf.Max(0.01f, stepSeconds));
    }

    public int GetCount(int x, int y, Element element)
    {
        EnsureInitialized();
        if (!Inside(x, y) || !ValidElement(element)) return 0;
        return cells[x, y, (int)element].count;
    }

    // Return a copy so outside callers cannot change the live grid accidentally.
    public BaseElement GetElement(int x, int y, Element element)
    {
        EnsureInitialized();
        if (!Inside(x, y) || !ValidElement(element)) return null;
        return cells[x, y, (int)element].Clone();
    }

    // Runtime edits are permitted only while paused; reset restores initialElements.
    public bool TrySetCount(int x, int y, Element element, int amount)
    {
        EnsureInitialized();
        if (IsRunning || !Inside(x, y) || !ValidElement(element)) return false;
        cells[x, y, (int)element] = new BaseElement(
            Mathf.Clamp(amount, 0, maxAmountPerElement), element);
        return true;
    }

    // Four orthogonal neighbors; diagonals and the current cell are excluded.
    public EnvironmentSnapshot GetEnvironment(int x, int y)
    {
        EnsureInitialized();
        if (!Inside(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Cell is outside the grid.");
        return ReadCell(x - 1, y).Add(ReadCell(x + 1, y))
            .Add(ReadCell(x, y - 1)).Add(ReadCell(x, y + 1));
    }

    public static Element GetCounterElement(Element target)
    {
        switch (target)
        {
            case Element.Gold: return Element.Fire;
            case Element.Wood: return Element.Gold;
            case Element.Water: return Element.Earth;
            case Element.Fire: return Element.Water;
            case Element.Earth: return Element.Wood;
            default: throw new ArgumentOutOfRangeException(nameof(target));
        }
    }

    private void SimulateStep(float duration)
    {
        var next = new BaseElement[Width, Height, ElementCount];

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            // Co-located elements also react. All reads still use the old grid.
            EnvironmentSnapshot influence = GetEnvironment(x, y).Add(ReadCell(x, y));
            for (int e = 0; e < ElementCount; e++)
                next[x, y, e] = CalculateNext(cells[x, y, e], influence, duration);
        }

        cells = next;
        StepCount++;
        SimulationSeconds += duration;
        if (logEachStep)
            Debug.Log($"Step {StepCount} ({SimulationSeconds:0.0}s)\n{DescribeGrid()}", this);
    }

    private BaseElement CalculateNext(BaseElement current, EnvironmentSnapshot influence, float duration)
    {
        // Zero quantity stays absent. This version has no seeds or spontaneous creation.
        if (current.count <= 0) return new BaseElement(0, current.myElement);

        BaseElement next = current.Clone();
        int counterAmount = influence[GetCounterElement(current.myElement)];
        int damage = 0;

        if (counterAmount > 0)
        {
            float resistance = Mathf.Max(1f, current.count / (float)counterAmount);
            float multiplier = 1f;
            if (current.myElement == Element.Wood)
            {
                // Continuous metal exposure ramps up damage, bounded by a multiplier cap.
                if (metalFatiguePerSecond > 0f)
                {
                    float maxExposure = (maxMetalMultiplier - 1f) / metalFatiguePerSecond;
                    next.metalExposureSeconds = Mathf.Min(
                        current.metalExposureSeconds + duration, maxExposure);
                }
                else next.metalExposureSeconds = 0f;
                multiplier += next.metalExposureSeconds * metalFatiguePerSecond;
            }

            float progress = current.reactionRemainder
                + counterAmount * reactionRate / resistance * multiplier * duration;
            int wholeDamage = Mathf.FloorToInt(progress);
            damage = Mathf.Min(current.count, wholeDamage);
            next.reactionRemainder = progress - wholeDamage;
        }
        else
        {
            // Interrupting exposure resets both fatigue and unfinished damage.
            next.metalExposureSeconds = 0f;
            next.reactionRemainder = 0f;
        }

        int growth = 0;
        if (current.myElement == Element.Wood && influence.Water > 0)
        {
            float progress = current.growthRemainder + influence.Water * woodGrowthRate * duration;
            growth = Mathf.FloorToInt(progress);
            next.growthRemainder = progress - growth;
        }
        else next.growthRemainder = 0f;

        // Growth and damage are simultaneous; growth does not consume water yet.
        next.count = Mathf.Clamp(current.count - damage + growth, 0, maxAmountPerElement);
        if (next.count == 0) return new BaseElement(0, current.myElement);
        if (next.count == maxAmountPerElement) next.growthRemainder = 0f;
        return next;
    }

    public string DescribeGrid()
    {
        EnsureInitialized();
        var result = new System.Text.StringBuilder();
        for (int y = Height - 1; y >= 0; y--)
        for (int x = 0; x < Width; x++)
        {
            EnvironmentSnapshot state = ReadCell(x, y);
            if (state.Gold + state.Wood + state.Water + state.Fire + state.Earth > 0)
                result.AppendLine($"({x},{y}) {state}");
        }
        return result.ToString();
    }

    private EnvironmentSnapshot ReadCell(int x, int y)
    {
        if (!Inside(x, y)) return default(EnvironmentSnapshot);
        return new EnvironmentSnapshot(cells[x, y, 0].count, cells[x, y, 1].count,
            cells[x, y, 2].count, cells[x, y, 3].count, cells[x, y, 4].count);
    }

    private bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    private static bool ValidElement(Element element) => (int)element >= 0 && (int)element < ElementCount;
    private void EnsureInitialized() { if (cells == null) ResetSimulation(); }
    private void OnValidate() { ValidateSettings(); }

    private void ValidateSettings()
    {
        width = Mathf.Clamp(width, 1, 64);
        height = Mathf.Clamp(height, 1, 64);
        maxAmountPerElement = Mathf.Clamp(maxAmountPerElement, 1, 100000);
        stepSeconds = Mathf.Clamp(stepSeconds, 0.01f, 10f);
        reactionRate = Mathf.Clamp(reactionRate, 0f, 10f);
        woodGrowthRate = Mathf.Clamp(woodGrowthRate, 0f, 10f);
        metalFatiguePerSecond = Mathf.Clamp(metalFatiguePerSecond, 0f, 10f);
        maxMetalMultiplier = Mathf.Clamp(maxMetalMultiplier, 1f, 10f);
        cellSize = Mathf.Clamp(cellSize, 0.1f, 10f);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Color[] colors = { Color.yellow, Color.green, Color.cyan, Color.red,
            new Color(0.65f, 0.4f, 0.2f) };
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            Vector3 center = transform.position + new Vector3(x * cellSize, y * cellSize, 0f);
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(center, new Vector3(cellSize, cellSize, 0.01f));
            string label = "";
            for (int e = 0; e < ElementCount; e++)
            {
                int amount = PreviewCount(x, y, (Element)e);
                if (amount <= 0) continue;
                Vector3 offset = new Vector3((e - 2) * cellSize * 0.16f, -cellSize * 0.28f, 0f);
                Gizmos.color = colors[e];
                Gizmos.DrawCube(center + offset, new Vector3(cellSize * 0.12f, cellSize * 0.12f, 0.01f));
                label += $"{(Element)e}:{amount}\n";
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(center + new Vector3(-cellSize * 0.4f, cellSize * 0.35f, 0f), label);
#endif
        }
    }

    private int PreviewCount(int x, int y, Element element)
    {
        if (cells != null) return cells[x, y, (int)element].count;
        long amount = 0;
        if (initialElements != null)
        foreach (ElementPlacement placement in initialElements)
            if (placement != null && placement.position.x == x && placement.position.y == y
                && placement.element == element)
                amount += Math.Max(0, placement.amount);
        return (int)Math.Min(maxAmountPerElement, amount);
    }
}
