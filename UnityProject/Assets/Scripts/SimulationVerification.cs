using System;
using System.Text;

// Runs the same pure simulation used by GameRuler, both in Unity and offline.
public static class SimulationVerification
{
    private static int assertions;
    private static readonly StringBuilder report = new StringBuilder();
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
    }
    private static void Near(float actual, float expected, string message, float tolerance = 0.001f)
        => Check(Math.Abs(actual - expected) < tolerance, message + $" ({actual} != {expected})");
    private static LevelDefinition Empty(int width = 3, int height = 1)
    {
        var l = new LevelDefinition(width, height) { TargetWater = 99999, StepLimit = 1000 };
        return l;
    }
    private static SimulationSettings NoFlow() => new SimulationSettings { FlowRate = 0, CutRate = 0, GrowthPerStep = 0 };
    private static void Put(LevelDefinition l, int i, Element e, float amount)
    { var c = l.Initial[i]; c.Set(e, amount); l.Initial[i] = c; }
    private static void Ledger(FiveElementSimulation s)
    {
        Near(s.WaterLedgerError, 0, "Water ledger"); Near(s.EarthLedgerError, 0, "Earth ledger");
        Near(s.WoodLedgerError, 0, "Wood ledger");
        for (int i = 0; i < s.Level.Initial.Length; i++)
        for (int e = 0; e < 5; e++)
            Check(s.GetCell(i).Get((Element)e) >= -0.00001f, "Nonnegative quantities");
    }
    public static string RunAll()
    {
        assertions = 0; report.Clear();
        Growth(); Fire(); Gold(); Flow(); Levels(); Determinism();
        report.AppendLine($"PASS: {assertions} assertions");
        return report.ToString();
    }
    private static void Growth()
    {
        var l = Empty(1); Put(l, 0, Element.Wood, 2); Put(l, 0, Element.Earth, 2); Put(l, 0, Element.Water, 6);
        var settings = NoFlow(); settings.GrowthPerStep = 1;
        var s = new FiveElementSimulation(l, settings); s.Step();
        Near(s.GetCell(0).Wood, 3, "Growth gains one wood"); Near(s.GetCell(0).Earth, 1, "Growth spends one earth");
        Near(s.GetCell(0).Water, 4, "Growth spends two water");
        s.Step(); s.Step(); Near(s.GetCell(0).Wood, 4, "No growth after earth exhausted"); Ledger(s);
        Put(l, 0, Element.Fire, 1);
        s = new FiveElementSimulation(l, settings); s.Step();
        Near(s.GrowthEarthSpent, 0, "A cell burning at step start cannot grow");
        Near(s.GetCell(0).Wood, 2, "Water extinguishes before burning");
        s.Step(); Near(s.GetCell(0).Wood, 3, "Growth resumes next step"); Ledger(s);
        Put(l, 0, Element.Fire, 0); Put(l, 0, Element.Water, 1);
        s = new FiveElementSimulation(l, settings); s.Step(); Near(s.GetCell(0).Wood, 2, "Water below two prevents growth");
        Put(l, 0, Element.Wood, 0); Put(l, 0, Element.Water, 6);
        s = new FiveElementSimulation(l, settings); s.Step(); Near(s.GetCell(0).Wood, 0, "No spontaneous wood");
        report.AppendLine("PASS growth: 1:1:2, local resources, fire gate, exhausted resources");
    }
    private static void Fire()
    {
        var l = Empty(1); Put(l, 0, Element.Wood, 10); Put(l, 0, Element.Fire, 0.01f);
        var s = new FiveElementSimulation(l, NoFlow()); s.Step();
        Check(s.GetCell(0).Wood < 10, "Weak fire damages wood"); Check(s.GetCell(0).Fire > 0.01f, "Fuel grows fire");
        Near(s.GetCell(0).Earth, s.BurnedWood, "Only actual burned wood returns earth"); Ledger(s);
        Put(l, 0, Element.Fire, 2); Put(l, 0, Element.Water, 0.5f);
        s = new FiveElementSimulation(l, NoFlow()); s.Step();
        Near(s.ExtinguishWaterSpent, 0.5f, "Insufficient water still spent on fire"); Check(s.BurnedWood > 0, "Unquenched fire burns"); Ledger(s);
        Put(l, 0, Element.Water, 3);
        s = new FiveElementSimulation(l, NoFlow()); s.Step(); Near(s.BurnedWood, 0, "Enough water prevents burning");
        l = Empty(3); for (int i = 0; i < 3; i++) Put(l, i, Element.Wood, 5);
        Put(l, 0, Element.Fire, 1);
        s = new FiveElementSimulation(l, NoFlow()); s.Step();
        Check(s.GetCell(1).Fire > 0, "Adjacent fuel ignites"); Near(s.GetCell(1).Wood, 5, "New fire does not burn same tick");
        Near(s.GetCell(2).Fire, 0, "Fire cannot chain across two edges in one tick");
        s.Step(); Check(s.GetCell(2).Fire > 0, "Fire propagates next tick"); Ledger(s);
        l.Blocked[1] = true; s = new FiveElementSimulation(l, NoFlow()); s.Step(); s.Step();
        Near(s.GetCell(2).Fire, 0, "Walls isolate fire influence");
        report.AppendLine("PASS fire: weak flame, fuel growth, water priority, ash, delayed spread, walls");
    }
    private static void Gold()
    {
        var l = Empty(1); Put(l, 0, Element.Gold, 5); Put(l, 0, Element.Fire, 5);
        var settings = NoFlow(); settings.FireDecay = 0;
        var s = new FiveElementSimulation(l, settings);
        for (int i = 0; i < 50; i++) s.Step(); Near(s.GetCell(0).Gold, 5, "Under threshold never damages gold");
        Put(l, 0, Element.Fire, 6); s = new FiveElementSimulation(l, settings); s.Step();
        Check(s.GetCell(0).Gold < 5, "Above threshold melts gold");
        l = Empty(1); Put(l, 0, Element.Gold, 2); Put(l, 0, Element.Wood, 100);
        settings = NoFlow(); settings.CutRate = 0.4f;
        s = new FiveElementSimulation(l, settings);
        float first;
        s.Step(); first = 100 - s.GetCell(0).Wood;
        for (int i = 0; i < 30; i++) s.Step();
        float before = s.GetCell(0).Wood; s.Step();
        Check(before - s.GetCell(0).Wood > first, "Continuous cutting accelerates"); Ledger(s);
        l = Empty(3, 3); Put(l, 1, Element.Gold, 2); Put(l, 7, Element.Gold, 3); Put(l, 4, Element.Wood, 12);
        s = new FiveElementSimulation(l, NoFlow()); Near(s.GetEnvironment(4).Gold, 5, "Four-neighbor quantities aggregate");
        Put(l, 0, Element.Gold, 99); s = new FiveElementSimulation(l, NoFlow()); Near(s.GetEnvironment(4).Gold, 5, "Diagonals excluded");
        report.AppendLine("PASS gold: threshold, no hidden damage, fatigue, local aggregation");
    }
    private static void Flow()
    {
        var l = Empty(3, 3); Put(l, 4, Element.Water, 4);
        var settings = NoFlow(); settings.FlowRate = 100;
        var s = new FiveElementSimulation(l, settings); s.Step();
        Near(s.Total(Element.Water), 4, "Several exits do not duplicate water");
        Near(s.GetCell(0).Water, 0, "Incoming water waits before crossing another edge"); Ledger(s);
        var thick = Empty(2); Put(thick, 0, Element.Water, 20); Put(thick, 0, Element.Earth, 10);
        settings.FlowRate = 2;
        var retained = new FiveElementSimulation(thick, settings); retained.Step();
        Put(thick, 0, Element.Earth, 0);
        var loose = new FiveElementSimulation(thick, settings); loose.Step();
        Check(retained.GetCell(1).Water < loose.GetCell(1).Water, "Earth slows outgoing water");
        Put(thick, 1, Element.Wood, 8); var blocked = new FiveElementSimulation(thick, settings); blocked.Step();
        Near(blocked.GetCell(1).Water, 0, "Dense wood blocks flow");
        thick.GoalIndex = 1; Put(thick, 1, Element.Wood, 0);
        var goal = new FiveElementSimulation(thick, settings); goal.Step();
        Check(goal.CollectedWater > 0, "Goal collects transferred water"); Ledger(goal);
        report.AppendLine("PASS water: shared outflow budget, delay, soil retention, wood barrier, collection");
    }
    private static void Levels()
    {
        for (int levelIndex = 0; levelIndex < 2; levelIndex++)
        for (int side = 1; side <= 3; side += 2)
        {
            var level = PrototypeLevels.Create(levelIndex);
            var plan = new CellState[level.Initial.Length];
            plan[level.Index(3, side)].Gold = 6;
            var s = new FiveElementSimulation(level, new SimulationSettings()); s.Reset(plan);
            while (s.Result == SimulationResult.Active) { s.Step(); Ledger(s); }
            Check(s.Result == SimulationResult.Won, $"Level {levelIndex + 1}, gold layout y={side} must be solvable; collected={s.CollectedWater}");
            report.AppendLine($"PASS level {levelIndex + 1}: gold y={side}, won in {s.StepCount} steps, water={s.CollectedWater:0.00}");
        }
        var empty = new FiveElementSimulation(PrototypeLevels.Create(0), new SimulationSettings());
        while (empty.Result == SimulationResult.Active) empty.Step();
        Check(empty.Result == SimulationResult.Lost, "Unopened wood wall causes timeout");
        empty.Reset(null); Check(empty.StepCount == 0 && empty.Result == SimulationResult.Active, "Reset clears result and time");
        var l = Empty(1); l.GoalIndex = 0; l.ProtectedIndex = 0; l.TargetWater = 1; Put(l, 0, Element.Water, 2);
        var priority = new FiveElementSimulation(l, NoFlow()); priority.Step();
        Check(priority.Result == SimulationResult.Lost, "Destroyed protected tree takes priority over goal");
    }
    private static void Determinism()
    {
        var a = new FiveElementSimulation(PrototypeLevels.Create(2), new SimulationSettings());
        var b = new FiveElementSimulation(PrototypeLevels.Create(2), new SimulationSettings());
        for (int step = 0; step < 30; step++)
        {
            a.Step(); b.Step(); Ledger(a);
            for (int i = 0; i < a.Level.Initial.Length; i++)
            for (int e = 0; e < 5; e++) Near(a.GetCell(i).Get((Element)e), b.GetCell(i).Get((Element)e), "Repeatable simulation");
        }
        report.AppendLine("PASS deterministic replay and laboratory resource ledgers");
    }
}
