using System;
using System.Collections.Generic;

public enum V1Element { Gold, Wood, Water, Fire, Earth }
public enum V1Mine { None, Active, Exhausted }
public enum V1AnimalKind { Deer, Rabbit, Cow, Chicken }
public enum V1AnimalOrigin { Wild, Farmed }
public enum V1Season { Spring, Summer, Autumn, Winter }
public enum V1PhaseEvent { None, Rain, Wind, Dew, Sunshine, Thunderstorm, Drought, Snow }

[Serializable]
public class V1Settings
{
    public V1Season initialSeason = V1Season.Autumn;
    public float springBirdMigrationChance = .12f;
    public bool seasonalEvents = true, dryIgnition = true;
    public float rainWater = .2f, stormWater = .45f, snowfallWater = .4f;
    public float evaporationRate = .006f;
    public float soilWaterRetention = .6f;
    public float firePollutionCleanRate = 5, firePollutionCost = .08f;
    public float phaseSeconds = 10, substepSeconds = .5f;
    public float deadGold = 8, mineGold = 20, treeWood = 3, forestWood = 8;
    public float treeGrowth = .20f, grassGrowth = .035f, goldPressure = .02f, goldTransfer = .05f;
    public float waterHeadScale = .18f, waterFlow = .8f, sourceRate = 2, outletRate = 3;
    public float deadPollutionRate = 1.2f, bridgeFloodWater = 12;
    public float fireSpreadThreshold = 3, fireSpreadDelay = 1, fireHeatTransfer = .18f;
    public float fireIgnitionHeat = .35f, fireHeatDecay = .015f, fireBurnRate = .5f;
    public float fireFuelGain = .06f, fireDecay = .06f, fireWaterQuench = .035f;
    public float riverCurrent = 1.1f, bankResistance = .18f, sedimentHeightScale = .025f;
    public bool generateTerrain = true;
    public int ecologyRevision;
    public int terrainSeed = 317;
    public float terrainNoiseScale = .085f, terrainAmplitude = 7, slopeHeight = 3.2f, mountainHeight = 4.25f;
    public float prosperityTarget = 65;
    public int prosperityHoldPhases = 5, migrationPhases = 4, animalCapacity = 12;
}

[Serializable]
public struct V1Cell
{
    public float Gold, Wood, Water, Fire, Earth, Grass, Height, SoilBody;
    public float Sediment, MudNutrient, MudGold, Deposited, Pollution;
    public int Seeds, DryPhases;
    public float Snow;
    public float TreeAge;
    public float FireAge, IgnitionHeat;
    public bool Tree, PlayerTree, PlayerSeed, Blocked, River, Source, Outlet, Bridge, DeadLand;
    public V1Mine Mine;
    public bool Dead(float a, float b) => Mine == V1Mine.None && Gold >= a && (DeadLand || Gold < b) && Wood <= .0001f;
}

[Serializable]
public sealed class V1Village
{
    public int id, cell, population = 8, tier = 1, badPhases, migrations, phases;
    public float housing = 12, timber = 6, metal = 3, toolHealth = 1, furnaceHeat;
    public bool active = true, furnace, cleaning;
    public string condition = "等待下个时段的环境结算";
    public V1Region region;
}

public struct V1Region
{
    public int cells, habitable, wet, green, trees, mines, wild, farmed, burning, flooded;
    public float grass, wood, water, earth, pollution, drinkPollution, food, capacity, health;
}

[Serializable]
public sealed class V1Animal
{
    public int id, cell, previousCell = -1, owner = -1, age, hunger;
    public V1AnimalKind kind;
    public V1AnimalOrigin origin;
    public bool alive = true;
}

// Diagnostic ledger. Sources and sinks are explicit; transport itself must conserve.
public sealed class V1Ledger
{
    public double WaterInput, WaterGrowth, WaterFire, WaterEvaporated, WaterOutlet;
    public double GoldInput, GoldMined, GoldBurned, GoldOutlet;
    public double PollutionMade, PollutionCleaned, PollutionOutlet;
    public double TimberHarvested, TimberUsed;
}

// All environment time enters through AdvancePhase; there is no wall-clock scheduler.
public sealed partial class V1Simulation
{
    public const int SeedCapacity = 2;
    public const float ExcessSeedEarth = .5f;
    public V1Season Season { get; set; }
    public readonly List<V1BirdLanding> BirdLandings = new List<V1BirdLanding>();
    public int BirdMigrations, BirdSeeds, SeedsComposted;
    public readonly int Width, Height;
    public readonly V1Settings Settings;
    public readonly V1Cell[] Cells;
    public readonly List<V1Village> Villages = new List<V1Village>();
    public readonly List<V1Animal> Animals = new List<V1Animal>();
    public readonly V1Ledger Ledger = new V1Ledger();
    public readonly List<string> Events = new List<string>();
    public int AdvanceCount { get; private set; }
    public int Day => 1 + AdvanceCount / 3;
    public int Phase => AdvanceCount % 3;
    public string PhaseName => new[] { "早晨", "中午", "傍晚" }[Phase];
    public string NextPhaseName => new[] { "中午", "傍晚", "次日早晨" }[Phase];
    public int TreeBirths, PlayerTreeBirths, MatureTransitions, ForestTransitions;
    public int GoldMinesFormed, WildlifeMoves, FarmBirths, NewVillages, FireSpreads, FloodCells;
    public int StablePhases { get; private set; }
    public bool Won { get; private set; }
    public float Prosperity { get; private set; }
    public int CharacterCell = -1, PendingRainCell = -1;
    public int TotalPopulation { get { int n = 0; foreach (var v in Villages) if (v.active) n += v.population; return n; } }
    public int ActiveVillages { get { int n = 0; foreach (var v in Villages) if (v.active) n++; return n; } }
    public int TutorialStage { get; private set; }
    public bool TutorialComplete => TutorialStage >= 3;
    public bool IsProsperous => Prosperity >= Settings.prosperityTarget && StablePhases >= Settings.prosperityHoldPhases;
    public string LastEvent => Events.Count > 0 ? Events[Events.Count - 1] : "河岸村庄等待你的帮助。";
    private readonly V1Cell[] snapshot;
    private readonly float[] waterDelta, pollutionDelta, sedimentDelta, nutrientDelta, mineralDelta, goldDelta, fireDelta;
    private readonly int[] neighbors, queue = new int[25], parent = new int[25], visited = new int[25];
    private readonly float[] outgoing;
    private uint random = 317;
    private int villageId = 1, animalId = 1;

    public V1Simulation(int width, int height, V1Settings settings = null, uint randomSeed = 317)
    {
        if (width < 8 || height < 8 || width > 64 || height > 64) throw new ArgumentOutOfRangeException("map");
        Width = width; Height = height; Settings = settings ?? new V1Settings();
        Season = Settings.initialSeason;
        random = randomSeed == 0 ? 317u : randomSeed;
        eventRandom = random ^ 0x9E3779B9u;
        if (eventRandom == 0) eventRandom = 731;
        if (!(Settings.deadGold > 0 && Settings.mineGold > Settings.deadGold) || Settings.phaseSeconds <= 0 || Settings.substepSeconds <= 0)
            throw new ArgumentException("Invalid V1 settings");
        Cells = new V1Cell[width * height]; snapshot = new V1Cell[Cells.Length];
        waterDelta = new float[Cells.Length]; pollutionDelta = new float[Cells.Length]; sedimentDelta = new float[Cells.Length];
        nutrientDelta = new float[Cells.Length]; mineralDelta = new float[Cells.Length]; goldDelta = new float[Cells.Length]; fireDelta = new float[Cells.Length];
        outgoing = new float[Cells.Length]; neighbors = new int[Cells.Length * 4];
        for (int i = 0; i < Cells.Length; i++) { int x = i % width, y = i / width; neighbors[i * 4] = Index(x + 1, y); neighbors[i * 4 + 1] = Index(x, y + 1); neighbors[i * 4 + 2] = Index(x - 1, y); neighbors[i * 4 + 3] = Index(x, y - 1); }
    }
    public int Index(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height ? x + y * Width : -1;
    public bool Valid(int i) => i >= 0 && i < Cells.Length && !Cells[i].Blocked;
    public static float Clamp(float n, float a, float b) => Math.Max(a, Math.Min(b, n));
    private int Random(int n) { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (int)(random % (uint)n); }
    private void Event(string message) { if (Events.Count >= 8) Events.RemoveAt(0); Events.Add(message); }
    public static int RegionSize(int tier) => Math.Max(1, Math.Min(3, tier)) + 5;
    public static int RegionMin(int tier) => -(RegionSize(tier) - 1) / 2;
    public static int RegionMax(int tier) => RegionMin(tier) + RegionSize(tier) - 1;
    public int RegionTier(int center)
    {
        foreach (var v in Villages) if (v.active && v.cell == center) return v.tier;
        return 1;
    }
    public bool InRegion(int center, int cell, int tier = 0)
    {
        if (cell < 0 || center < 0) return false;
        int dx = cell % Width - center % Width, dy = cell / Width - center / Width;
        if (tier == 0) tier = RegionTier(center);
        return dx >= RegionMin(tier) && dx <= RegionMax(tier) && dy >= RegionMin(tier) && dy <= RegionMax(tier);
    }
    public int TreeCells3x3(int center)
    {
        int n = 0;
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
        { int i = Index(center % Width + x, center / Width + y); if (i >= 0 && Cells[i].Tree && Cells[i].Wood > .0001f) n++; }
        return n;
    }
    public int PlantStage(int i) => !Cells[i].Tree || Cells[i].Wood <= .0001f ? 0 : Cells[i].Wood < Settings.treeWood ? 1 : Cells[i].Wood < Settings.forestWood ? 2 : 3;
    public bool IsWet(int i) => Valid(i) && (Cells[i].Water > 4 || Cells[i].River && Cells[i].Water > .5f);
    public bool BridgeUsable(int i) => Valid(i) && Cells[i].Bridge && Cells[i].Water <= Settings.bridgeFloodWater;
    public bool Walkable(int i) => Valid(i) && !IsBuilding(i) && (!IsWet(i) || BridgeUsable(i));
    public bool SafeLanding(int i) => Walkable(i) && Cells[i].Fire <= .1f && PlantStage(i) < 2;
    public int NearestSafeLand(float x, float y)
    {
        int best = -1; float distance = float.MaxValue;
        for (int i = 0; i < Cells.Length; i++) if (SafeLanding(i) && !IsWet(i))
        {
            float dx = i % Width + .5f - x, dy = i / Width + .5f - y, squared = dx * dx + dy * dy;
            if (squared < distance) { distance = squared; best = i; }
        }
        return best;
    }
    private void UpdateDeadLand(int i)
    {
        if (Cells[i].Mine != V1Mine.None || Cells[i].Gold < Settings.deadGold || Cells[i].Wood > .0001f) Cells[i].DeadLand = false;
        else if (Cells[i].Gold < Settings.mineGold) Cells[i].DeadLand = true;
    }
    public void Initialize()
    {
        NormalizeSeedStocks();
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            if (Cells[i].Gold >= Settings.mineGold) { Cells[i].Mine = V1Mine.Active; GoldMinesFormed++; }
            if (Cells[i].Wood > 0) Cells[i].Tree = true;
            UpdateDeadLand(i);
        }
    }
    public bool PlaceElement(int cell, V1Element element, float amount = 4)
    {
        if (!Valid(cell) || IsBuilding(cell) || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
        switch (element)
        {
            case V1Element.Gold:
                if (Cells[cell].Mine == V1Mine.Exhausted) Cells[cell].Mine = V1Mine.None;
                Cells[cell].Gold += amount; Ledger.GoldInput += amount; break;
            case V1Element.Wood: AddSeeds(cell, 1, true); break;
            case V1Element.Water: Cells[cell].Water += amount * 2; Ledger.WaterInput += amount * 2; break;
            case V1Element.Fire: Cells[cell].Fire += amount; break;
            case V1Element.Earth: Cells[cell].Earth += amount; break;
            default: return false;
        }
        // Only an explicit player deposit can turn established deathland into a mine.
        if (element == V1Element.Gold && Cells[cell].Gold >= Settings.mineGold && Cells[cell].Mine == V1Mine.None)
        { Cells[cell].Mine = V1Mine.Active; Cells[cell].DeadLand = false; GoldMinesFormed++; }
        UpdateDeadLand(cell);
        return true;
    }
    public bool AdjustTerrain(int cell, float delta)
    {
        if (!Valid(cell) || IsBuilding(cell) || Cells[cell].Mine == V1Mine.Active) return false;
        Cells[cell].Height = Clamp(Cells[cell].Height + delta, -.5f, 6);
        Cells[cell].SoilBody = Math.Max(0, Cells[cell].SoilBody + delta * 10);
        return true;
    }
    public void QueueRain(int cell) { if (Valid(cell)) { PendingRainCell = cell; QueueEvent(V1PhaseEvent.Rain); } }
    public void AdvancePhase()
    {
        Events.Clear();
        NormalizeSeedStocks();
        BeginPhaseEvent();
        int steps = Math.Max(1, (int)Math.Ceiling(Settings.phaseSeconds / Settings.substepSeconds));
        float dt = Settings.phaseSeconds / steps;
        for (int s = 0; s < steps; s++)
        {
            MeltSnow(dt);
            FireAndGrowth(dt);
            EnrichGold(dt);
            WorkVillages(dt);
            FlowWater(dt);
            Pollution(dt);
        }
        AdvanceCount++;
        FinishPhaseEvent();
        AnimalsPhase(); VillagesPhase(); BirdMigrationPhase(); ProsperityPhase(); TutorialPhase();
        FloodCells = 0; for (int i = 0; i < Cells.Length; i++) if (Valid(i) && !Cells[i].River && Cells[i].Water > 4) FloodCells++;
        if (FloodCells > 0) Event("有 " + FloodCells + " 格土地积水，检查河床淤积与排水方向。");
        if (Events.Count == 0) Event("环境完成一个时段的演化；可以移动、观察，再决定何时渡过下一时段。");
    }
    private void TutorialPhase()
    {
        if(TutorialComplete)return;
        for(int i=0;i<Cells.Length;i++)if(Cells[i].PlayerTree&&PlantStage(i)>=2){TutorialStage=3;return;}
        TutorialStage=PlayerTreeBirths>0?2:AdvanceCount>0?1:0;
    }
}
