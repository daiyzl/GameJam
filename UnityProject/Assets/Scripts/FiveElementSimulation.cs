using System;

[Serializable]
public class SimulationSettings
{
    public float StepSeconds = 0.5f;
    public int GrowthPerStep = 1;
    public float CutRate = 0.4f;
    public float FatigueRate = 0.3f;
    public float MaxCutMultiplier = 4f;
    public float BurnRate = 0.12f;
    public float MaxBurnPerStep = 3f;
    public float FireYield = 1f;
    public float MaxFire = 30f;
    public float FireDecay = 1f;
    public float IgnitionFire = 0.5f;
    public float GoldResistance = 1f;
    public float MeltRate = 0.5f;
    public float WaterPerFire = 1f;
    public float FlowRate = 12f;
    public float EarthRetention = 0.2f;
    public float WoodBlockThreshold = 8f;
    public float WaterDepth = 0.2f;
    public float MaxWood = 100f;

    public void Validate()
    {
        StepSeconds = Clamp(StepSeconds, 0.01f, 5f);
        GrowthPerStep = Math.Max(0, Math.Min(10, GrowthPerStep));
        CutRate = Clamp(CutRate, 0, 10); FatigueRate = Clamp(FatigueRate, 0, 10);
        MaxCutMultiplier = Clamp(MaxCutMultiplier, 1, 10);
        BurnRate = Clamp(BurnRate, 0, 10); MaxBurnPerStep = Clamp(MaxBurnPerStep, 0, 100);
        FireYield = Clamp(FireYield, 0, 5); MaxFire = Clamp(MaxFire, 1, 1000);
        FireDecay = Clamp(FireDecay, 0, 100); IgnitionFire = Clamp(IgnitionFire, 0, 10);
        GoldResistance = Clamp(GoldResistance, 0.01f, 100); MeltRate = Clamp(MeltRate, 0, 10);
        WaterPerFire = Clamp(WaterPerFire, 0.01f, 10); FlowRate = Clamp(FlowRate, 0, 1000);
        EarthRetention = Clamp(EarthRetention, 0, 100); WoodBlockThreshold = Clamp(WoodBlockThreshold, 0.1f, 1000);
        WaterDepth = Clamp(WaterDepth, 0.01f, 10); MaxWood = Clamp(MaxWood, 1, 1000);
    }
    private static float Clamp(float value, float min, float max)
        => float.IsNaN(value) || float.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
}

public struct CellState
{
    public float Gold, Wood, Water, Fire, Earth, CutExposure;
    public float Get(Element e)
    {
        switch (e)
        {
            case Element.Gold: return Gold;
            case Element.Wood: return Wood;
            case Element.Water: return Water;
            case Element.Fire: return Fire;
            case Element.Earth: return Earth;
            default: throw new ArgumentOutOfRangeException(nameof(e));
        }
    }
    public void Set(Element e, float value)
    {
        value = Math.Max(0, value);
        switch (e)
        {
            case Element.Gold: Gold = value; break;
            case Element.Wood: Wood = value; break;
            case Element.Water: Water = value; break;
            case Element.Fire: Fire = value; break;
            case Element.Earth: Earth = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(e));
        }
    }
    public EnvironmentSnapshot Snapshot() => new EnvironmentSnapshot(Gold, Wood, Water, Fire, Earth);
}

public enum SimulationResult { Active, Won, Lost }

public class LevelDefinition
{
    public readonly int Width, Height;
    public readonly CellState[] Initial;
    public readonly bool[] Blocked, Placeable;
    public readonly float[] Elevation;
    public readonly float[] Budget = new float[5];
    public string Name, Hint;
    public int GoalIndex = -1, ProtectedIndex = -1, SourceIndex = -1;
    public float TargetWater = 10f;
    public int StepLimit = 40;
    public LevelDefinition(int width, int height)
    {
        if (width < 1 || height < 1 || width > 64 || height > 64) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height;
        Initial = new CellState[width * height]; Blocked = new bool[width * height];
        Placeable = new bool[width * height]; Elevation = new float[width * height];
    }
    public int Index(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? x + y * Width : -1;
}

// Pure deterministic simulation. Buffers are allocated once, not once per tick.
public sealed class FiveElementSimulation
{
    public const float Epsilon = 0.00001f;
    public readonly LevelDefinition Level;
    public readonly SimulationSettings Settings;
    private readonly CellState[] current, grown, cooled, reacted;
    private readonly float[] waterDelta;
    private readonly int[] neighbors;
    private readonly float[] desires = new float[4];
    public int StepCount { get; private set; }
    public float CollectedWater { get; private set; }
    public float GrowthWaterSpent { get; private set; }
    public float ExtinguishWaterSpent { get; private set; }
    public float GrowthEarthSpent { get; private set; }
    public float BurnedWood { get; private set; }
    public float CutWood { get; private set; }
    public float InitialWater { get; private set; }
    public float InitialEarth { get; private set; }
    public float InitialWood { get; private set; }
    public SimulationResult Result { get; private set; }
    public string ResultReason { get; private set; } = "";

    public FiveElementSimulation(LevelDefinition level, SimulationSettings settings)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Settings.Validate();
        int n = level.Initial.Length;
        current = new CellState[n]; grown = new CellState[n]; cooled = new CellState[n];
        reacted = new CellState[n]; waterDelta = new float[n]; neighbors = new int[n * 4];
        for (int i = 0; i < n; i++)
        {
            int x = i % level.Width, y = i / level.Width;
            neighbors[i * 4] = level.Index(x + 1, y);
            neighbors[i * 4 + 1] = level.Index(x, y + 1);
            neighbors[i * 4 + 2] = level.Index(x - 1, y);
            neighbors[i * 4 + 3] = level.Index(x, y - 1);
        }
        Reset(null);
    }

    public CellState GetCell(int index) => index >= 0 && index < current.Length ? current[index] : default(CellState);

    // Explicit external resource input for a free-roaming sandbox; keep ledgers valid.
    public bool AddResource(int index, Element element, float amount)
    {
        if (index < 0 || index >= current.Length || Level.Blocked[index] || (int)element < 0 || (int)element > 4
            || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
        CellState cell = current[index]; cell.Set(element, cell.Get(element) + amount); current[index] = cell;
        if (element == Element.Water) InitialWater += amount;
        if (element == Element.Earth) InitialEarth += amount;
        if (element == Element.Wood) InitialWood += amount;
        return true;
    }
    public CellState GetCell(int x, int y) => GetCell(Level.Index(x, y));
    public EnvironmentSnapshot GetEnvironment(int index) => Neighborhood(current, index, false);

    private EnvironmentSnapshot Neighborhood(CellState[] buffer, int index, bool includeSelf)
    {
        if (index < 0 || index >= buffer.Length || Level.Blocked[index]) return default(EnvironmentSnapshot);
        EnvironmentSnapshot result = includeSelf ? buffer[index].Snapshot() : default(EnvironmentSnapshot);
        for (int d = 0; d < 4; d++)
        {
            int j = neighbors[index * 4 + d];
            if (j >= 0 && !Level.Blocked[j]) result = result.Add(buffer[j].Snapshot());
        }
        return result;
    }

    public void Reset(CellState[] plan)
    {
        if (plan != null && plan.Length != current.Length) throw new ArgumentException("Plan size mismatch.");
        StepCount = 0; CollectedWater = 0; GrowthWaterSpent = 0; ExtinguishWaterSpent = 0;
        GrowthEarthSpent = 0; BurnedWood = 0; CutWood = 0;
        InitialWater = 0; InitialEarth = 0; InitialWood = 0;
        Result = SimulationResult.Active; ResultReason = "";
        for (int i = 0; i < current.Length; i++)
        {
            CellState cell = Level.Blocked[i] ? default(CellState) : Level.Initial[i];
            if (plan != null && !Level.Blocked[i])
                for (int e = 0; e < 5; e++) cell.Set((Element)e, cell.Get((Element)e) + plan[i].Get((Element)e));
            cell.CutExposure = 0;
            current[i] = cell;
            InitialWater += cell.Water; InitialEarth += cell.Earth; InitialWood += cell.Wood;
        }
    }

    public void Step()
    {
        if (Result != SimulationResult.Active) return;
        float dt = Settings.StepSeconds;
        // Phase 1: local growth. A cell that begins this step on fire cannot grow.
        for (int i = 0; i < current.Length; i++)
        {
            CellState c = current[i];
            if (!Level.Blocked[i] && c.Fire <= Epsilon && c.Wood > Epsilon)
            {
                int gain = Math.Min(Settings.GrowthPerStep, Math.Min((int)Math.Floor(c.Earth + Epsilon),
                    Math.Min((int)Math.Floor(c.Water / 2f + Epsilon), (int)Math.Floor(Settings.MaxWood - c.Wood + Epsilon))));
                gain = Math.Max(0, gain);
                c.Wood += gain; c.Earth -= gain; c.Water -= 2 * gain;
                GrowthEarthSpent += gain; GrowthWaterSpent += 2 * gain;
            }
            grown[i] = c;
        }
        // Phase 2a: extinguish everywhere before any cell reads fire influence.
        for (int i = 0; i < current.Length; i++)
        {
            CellState c = grown[i];
            float spent = Math.Min(c.Water, c.Fire * Settings.WaterPerFire);
            c.Water = Math.Max(0, c.Water - spent);
            c.Fire = Math.Max(0, c.Fire - spent / Settings.WaterPerFire);
            ExtinguishWaterSpent += spent;
            cooled[i] = c;
        }
        // Phase 2b: all influence reads use the same cooled snapshot.
        for (int i = 0; i < current.Length; i++)
        {
            CellState c = cooled[i], next = c;
            if (Level.Blocked[i]) { reacted[i] = c; continue; }
            EnvironmentSnapshot env = Neighborhood(cooled, i, true);
            if (c.Wood > Epsilon && env.Gold > Epsilon)
            {
                float exposureCap = Settings.FatigueRate > Epsilon
                    ? (Settings.MaxCutMultiplier - 1) / Settings.FatigueRate : 0;
                next.CutExposure = Math.Min(c.CutExposure + dt, exposureCap);
                float resistance = Math.Max(1, c.Wood / env.Gold);
                float cut = Math.Min(c.Wood, env.Gold * Settings.CutRate / resistance
                    * (1 + next.CutExposure * Settings.FatigueRate) * dt);
                next.Wood -= cut; CutWood += cut;
            }
            else next.CutExposure = 0;

            if (c.Fire > Epsilon)
            {
                float burn = Math.Min(next.Wood, Math.Min(Settings.MaxBurnPerStep,
                    Settings.BurnRate * c.Fire * next.Wood * dt));
                next.Wood -= burn; next.Earth += burn; BurnedWood += burn;
                next.Fire = Math.Min(Settings.MaxFire, c.Fire + burn * Settings.FireYield);
                if (next.Wood <= Epsilon) next.Fire = Math.Max(0, next.Fire - Settings.FireDecay * dt);
            }
            else if (next.Wood > Epsilon && c.Water <= Epsilon)
            {
                float adjacentFire = Neighborhood(cooled, i, false).Fire;
                if (adjacentFire > Epsilon) next.Fire = Math.Min(Settings.IgnitionFire, adjacentFire);
            }

            // Under-threshold fire never accumulates hidden metal damage.
            float excessFire = env.Fire - c.Gold * Settings.GoldResistance;
            if (c.Gold > Epsilon && excessFire > Epsilon)
                next.Gold = Math.Max(0, c.Gold - excessFire * Settings.MeltRate * dt);
            reacted[i] = next;
        }
        // Phase 3: simultaneous local water transfers, each emitter has one budget.
        Array.Clear(waterDelta, 0, waterDelta.Length);
        for (int i = 0; i < current.Length; i++)
        {
            CellState source = reacted[i];
            if (Level.Blocked[i] || i == Level.GoalIndex || source.Water <= Epsilon) continue;
            float budget = Math.Min(source.Water, Settings.FlowRate * dt / (1 + source.Earth * Settings.EarthRetention));
            float sourceHead = Level.Elevation[i] + source.Water * Settings.WaterDepth;
            float total = 0;
            for (int d = 0; d < 4; d++)
            {
                desires[d] = 0;
                int j = neighbors[i * 4 + d];
                if (j < 0 || Level.Blocked[j]) continue;
                float permeability = Math.Max(0, 1 - Math.Max(source.Wood, reacted[j].Wood) / Settings.WoodBlockThreshold);
                float targetHead = Level.Elevation[j] + reacted[j].Water * Settings.WaterDepth;
                float difference = sourceHead - targetHead;
                if (difference <= Epsilon || permeability <= 0) continue;
                desires[d] = Math.Min(budget, difference / (2 * Settings.WaterDepth)) * permeability;
                total += desires[d];
            }
            float scale = total > budget ? budget / total : 1;
            for (int d = 0; d < 4; d++)
            {
                float amount = desires[d] * scale;
                if (amount <= 0) continue;
                waterDelta[i] -= amount;
                waterDelta[neighbors[i * 4 + d]] += amount;
            }
        }
        for (int i = 0; i < current.Length; i++)
        {
            CellState c = reacted[i];
            c.Water = Math.Max(0, c.Water + waterDelta[i]);
            current[i] = c;
        }
        if (Level.GoalIndex >= 0)
        {
            CellState goal = current[Level.GoalIndex];
            CollectedWater += goal.Water; goal.Water = 0; current[Level.GoalIndex] = goal;
        }
        StepCount++;
        if (Level.ProtectedIndex >= 0 && current[Level.ProtectedIndex].Wood <= Epsilon)
        { Result = SimulationResult.Lost; ResultReason = "守护树被摧毁"; }
        else if (CollectedWater + 0.001f >= Level.TargetWater)
        { Result = SimulationResult.Won; ResultReason = "水槽已装满，机关恢复运转"; }
        else if (StepCount >= Level.StepLimit)
        { Result = SimulationResult.Lost; ResultReason = "达到步数上限，水槽尚未装满"; }
    }

    public float Total(Element element)
    {
        float sum = 0;
        for (int i = 0; i < current.Length; i++) sum += current[i].Get(element);
        return sum;
    }
    public float WaterLedgerError => InitialWater - Total(Element.Water) - CollectedWater - GrowthWaterSpent - ExtinguishWaterSpent;
    public float EarthLedgerError => InitialEarth + BurnedWood - Total(Element.Earth) - GrowthEarthSpent;
    public float WoodLedgerError => InitialWood + GrowthEarthSpent - Total(Element.Wood) - CutWood - BurnedWood;
}
