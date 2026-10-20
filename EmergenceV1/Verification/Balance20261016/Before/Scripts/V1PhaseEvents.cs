using System;
using System.Collections.Generic;

public sealed partial class V1Simulation
{
    public V1PhaseEvent CurrentEvent { get; private set; }
    public readonly List<int> LightningCells = new List<int>();
    public int NaturalFires { get; private set; }
    private V1PhaseEvent? pendingEvent;
    private int droughtRemaining;
    // Separate from animal randomness, so event draws do not prescribe animal routes.
    private uint eventRandom = 731;
    private int EventRandom(int n) { eventRandom ^= eventRandom << 13; eventRandom ^= eventRandom >> 17; eventRandom ^= eventRandom << 5; return (int)(eventRandom % (uint)n); }
    public bool EventAllowed(V1PhaseEvent value) => value != V1PhaseEvent.Thunderstorm && value != V1PhaseEvent.Drought && value != V1PhaseEvent.Snow ||
        value == V1PhaseEvent.Thunderstorm && Season == V1Season.Spring || value == V1PhaseEvent.Drought && Season == V1Season.Summer || value == V1PhaseEvent.Snow && Season == V1Season.Winter;
    public bool QueueEvent(V1PhaseEvent value)
    {
        if (!EventAllowed(value)) return false;
        pendingEvent = value; return true;
    }
    private void BeginPhaseEvent()
    {
        LightningCells.Clear();
        var chosen = V1PhaseEvent.None;
        if (pendingEvent.HasValue) { chosen = pendingEvent.Value; pendingEvent = null; droughtRemaining = 0; }
        else if (Settings.seasonalEvents)
        {
            if (droughtRemaining > 0 && Season == V1Season.Summer) { chosen = V1PhaseEvent.Drought; droughtRemaining--; }
            else
            {
                int roll = EventRandom(10000);
                if (roll < 350 && Season == V1Season.Spring) chosen = V1PhaseEvent.Thunderstorm;
                else if (roll < 500 && Season == V1Season.Summer) { chosen = V1PhaseEvent.Drought; droughtRemaining = 2; }
                else if (roll < 500 && Season == V1Season.Winter) chosen = V1PhaseEvent.Snow;
                else if (roll < 1900) chosen = V1PhaseEvent.Rain;
                else if (roll < 3100) chosen = V1PhaseEvent.Wind;
                else if (roll < 4100) chosen = V1PhaseEvent.Dew;
                else if (roll < 6100) chosen = V1PhaseEvent.Sunshine;
            }
        }
        CurrentEvent = EventAllowed(chosen) ? chosen : V1PhaseEvent.None;
        PendingRainCell = -1;
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            float input = CurrentEvent == V1PhaseEvent.Rain ? Settings.rainWater :
                CurrentEvent == V1PhaseEvent.Thunderstorm ? Settings.stormWater :
                CurrentEvent == V1PhaseEvent.Dew && Cells[i].Water < .8f && !Cells[i].River ? .025f : 0;
            Cells[i].Water += input; Ledger.WaterInput += input;
            if (CurrentEvent == V1PhaseEvent.Snow)
            { Cells[i].Snow += Settings.snowfallWater; Ledger.WaterInput += Settings.snowfallWater; }
        }
    }
    private void MeltSnow(float dt)
    {
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && Cells[i].Snow > 0)
        {
            float rate = Season == V1Season.Winter ? .006f : .025f;
            if (CurrentEvent == V1PhaseEvent.Rain || CurrentEvent == V1PhaseEvent.Thunderstorm) rate += .02f;
            float melt = Math.Min(Cells[i].Snow, (rate + Cells[i].Fire * .05f) * dt);
            Cells[i].Snow -= melt; Cells[i].Water += melt; // Already booked when snow fell.
        }
    }
    public float DryFireRisk(int i)
    {
        if (!Valid(i) || IsBuilding(i) || IsWet(i)) return 0;
        var c = Cells[i];
        if (c.River || c.Bridge || c.Snow > .01f || c.Water >= .15f || c.Fire > .01f || c.Wood + c.Grass < 1 || c.DryPhases < 6) return 0;
        return Math.Min(.06f, Math.Min(.03f, (c.DryPhases - 5) * .003f) * (CurrentEvent == V1PhaseEvent.Drought ? 2 : 1));
    }
    private void FinishPhaseEvent()
    {
        if (CurrentEvent == V1PhaseEvent.Thunderstorm)
        {
            for (int strike = 0; strike < 2; strike++)
            {
                int target = -1, count = 0;
                for (int i = 0; i < Cells.Length; i++) if (Valid(i) && !IsBuilding(i) && !Cells[i].River && !Cells[i].Bridge && !IsWet(i) && !LightningCells.Contains(i))
                    if (EventRandom(++count) == 0) target = i;
                if (target < 0) break;
                LightningCells.Add(target);
                if (Cells[target].Snow <= .01f && Cells[target].Water < .55f && Cells[target].Wood + Cells[target].Grass >= 1 && EventRandom(10000) < 3000)
                    Cells[target].Fire = Math.Max(Cells[target].Fire, 2.4f);
            }
        }
        int dry = -1, candidates = 0;
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            if (Cells[i].Water < .15f && Cells[i].Snow <= .01f && !Cells[i].River) Cells[i].DryPhases = Math.Min(60, Cells[i].DryPhases + 1);
            else Cells[i].DryPhases = 0;
            if (Settings.dryIgnition && DryFireRisk(i) > 0 && EventRandom(++candidates) == 0) dry = i;
        }
        // At most one weak spontaneous ignition per map and phase.
        if (dry >= 0 && EventRandom(10000) < DryFireRisk(dry) * 10000)
        { Cells[dry].Fire = 1.6f; Cells[dry].FireAge = 0; NaturalFires++; }
    }
}
