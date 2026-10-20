using System;
using System.Reflection;

internal static class RulesChecks
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static void Set(GameRuler ruler, string field, object value)
        => typeof(GameRuler).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ruler, value);
    private static GameRuler Empty()
    {
        var ruler = new GameRuler();
        ruler.ResetSimulation();
        for (int x = 0; x < ruler.Width; x++)
        for (int y = 0; y < ruler.Height; y++)
        for (int e = 0; e < 5; e++) ruler.TrySetCount(x, y, (Element)e, 0);
        Set(ruler, "woodGrowthRate", 0f);
        return ruler;
    }
    public static void Main()
    {
        var demo = new GameRuler();
        demo.ResetSimulation();
        var env = demo.GetEnvironment(3, 1);
        Check(env.Gold == 5 && env.Water == 8 && env.Wood == 0, "Neighbor aggregation / self exclusion");
        demo.TrySetCount(2, 0, Element.Gold, 99);
        Check(demo.GetEnvironment(3, 1).Gold == 5, "Diagonal exclusion");
        Check(demo.GetEnvironment(0, 0).Gold == 0, "Grid boundary");
        var copy = demo.GetElement(3, 1, Element.Wood);
        copy.count = 100;
        Check(demo.GetCount(3, 1, Element.Wood) == 12, "Defensive copies");
        Check(new BaseElement(-10, Element.Fire).count == 0, "Negative quantity guard");

        Element[] counters = { Element.Fire, Element.Gold, Element.Earth, Element.Water, Element.Wood };
        for (int e = 0; e < 5; e++)
            Check(GameRuler.GetCounterElement((Element)e) == counters[e], "Five element cycle");

        var simultaneous = Empty();
        Set(simultaneous, "reactionRate", 2f);
        Set(simultaneous, "metalFatiguePerSecond", 0f);
        simultaneous.TrySetCount(0, 1, Element.Gold, 2);
        simultaneous.TrySetCount(1, 1, Element.Wood, 2);
        simultaneous.TrySetCount(2, 1, Element.Earth, 2);
        simultaneous.StepOnce();
        Check(simultaneous.GetCount(1, 1, Element.Wood) == 0, "Metal cuts wood");
        Check(simultaneous.GetCount(2, 1, Element.Earth) == 0, "All reactions read previous state");

        var weak = Empty();
        Set(weak, "reactionRate", 0.1f);
        Set(weak, "metalFatiguePerSecond", 0f);
        weak.TrySetCount(1, 1, Element.Gold, 1);
        weak.TrySetCount(2, 1, Element.Wood, 1);
        weak.StepOnce();
        Check(weak.GetCount(2, 1, Element.Wood) == 1, "Sub-unit damage waits");
        for (int i = 0; i < 25; i++) weak.StepOnce();
        Check(weak.GetCount(2, 1, Element.Wood) == 0, "Sub-unit damage accumulates");

        var fatigue = Empty();
        var constant = Empty();
        Set(constant, "metalFatiguePerSecond", 0f);
        foreach (var ruler in new[] { fatigue, constant })
        {
            ruler.TrySetCount(1, 1, Element.Gold, 2);
            ruler.TrySetCount(2, 1, Element.Wood, 100);
            for (int i = 0; i < 100; i++) ruler.StepOnce();
        }
        Check(fatigue.GetCount(2, 1, Element.Wood) < constant.GetCount(2, 1, Element.Wood), "Continuous exposure accelerates cutting");
        fatigue.TrySetCount(1, 1, Element.Gold, 0);
        fatigue.StepOnce();
        Check(fatigue.GetElement(2, 1, Element.Wood).metalExposureSeconds == 0f, "Interrupted exposure resets fatigue");

        var growth = Empty();
        Set(growth, "reactionRate", 0f);
        Set(growth, "woodGrowthRate", 1f);
        growth.TrySetCount(2, 1, Element.Wood, 2);
        growth.TrySetCount(2, 1, Element.Water, 2);
        growth.StepOnce();
        Check(growth.GetCount(2, 1, Element.Wood) == 3, "Co-located water promotes growth");
        Check(growth.GetCount(2, 1, Element.Water) == 2, "Influence model does not consume water");
        Check(growth.GetCount(1, 1, Element.Wood) == 0, "Absent wood is not spontaneously created");
        growth.TrySetCount(2, 1, Element.Wood, 1000);
        growth.StepOnce();
        Check(growth.GetCount(2, 1, Element.Wood) == 1000, "Growth cap");
        growth.ResumeSimulation();
        Check(!growth.TrySetCount(2, 1, Element.Wood, 1), "Running grid cannot be edited");
        growth.PauseSimulation();
        Check(growth.TrySetCount(2, 1, Element.Wood, -9) && growth.GetCount(2, 1, Element.Wood) == 0, "Paused editing clamps quantities");
        growth.ResetSimulation();
        Check(growth.StepCount == 0 && growth.SimulationSeconds == 0f && !growth.IsRunning, "Reset clears time and pauses");
        Check(growth.GetCount(3, 1, Element.Wood) == 12, "Reset restores initial placements");

        var coexist = Empty();
        Set(coexist, "reactionRate", 2f);
        Set(coexist, "metalFatiguePerSecond", 0f);
        coexist.TrySetCount(1, 1, Element.Gold, 2);
        coexist.TrySetCount(1, 1, Element.Wood, 2);
        coexist.StepOnce();
        Check(coexist.GetCount(1, 1, Element.Wood) == 0, "Co-located counter elements react");
        Console.WriteLine($"PASS: {assertions} assertions");
    }
}
