using System;

public sealed partial class V1Simulation
{
    private void FlowWater(float dt)
    {
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            if (Cells[i].Source) { float input = Settings.sourceRate * dt; Cells[i].Water += input; Ledger.WaterInput += input; }
            float loss = Math.Min(Cells[i].Water, .003f * dt / (1 + Cells[i].Grass * .2f + Cells[i].Wood * .1f));
            Cells[i].Water -= loss; Ledger.WaterEvaporated += loss;
        }
        Array.Copy(Cells, snapshot, Cells.Length);
        Array.Clear(waterDelta, 0, waterDelta.Length); Array.Clear(pollutionDelta, 0, pollutionDelta.Length);
        Array.Clear(sedimentDelta, 0, sedimentDelta.Length); Array.Clear(nutrientDelta, 0, nutrientDelta.Length); Array.Clear(mineralDelta, 0, mineralDelta.Length);
        Array.Clear(outgoing, 0, outgoing.Length);
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && snapshot[i].Water > .0001f)
        {
            float desired = 0, level = snapshot[i].Height + Settings.waterHeadScale * snapshot[i].Water;
            for (int d = 0; d < 4; d++)
            {
                int n = neighbors[i * 4 + d]; if (!Valid(n)) continue;
                desired += DesiredFlow(i, n, level, dt);
            }
            float limit = Math.Min(snapshot[i].Water, desired / (1 + snapshot[i].Grass * .08f + snapshot[i].Wood * .025f));
            if (desired <= .0001f) continue;
            for (int d = 0; d < 4; d++)
            {
                int n = neighbors[i * 4 + d]; if (!Valid(n)) continue;
                float flow = DesiredFlow(i, n, level, dt) * limit / desired;
                float fraction = flow / snapshot[i].Water;
                Transfer(waterDelta, i, n, flow);
                Transfer(pollutionDelta, i, n, snapshot[i].Pollution * fraction * .6f);
                Transfer(sedimentDelta, i, n, snapshot[i].Sediment * fraction);
                Transfer(nutrientDelta, i, n, snapshot[i].MudNutrient * fraction);
                Transfer(mineralDelta, i, n, snapshot[i].MudGold * fraction);
                outgoing[i] += flow;
            }
        }
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            var c = Cells[i];
            c.Water = Math.Max(0, c.Water + waterDelta[i]); c.Pollution = Math.Max(0, c.Pollution + pollutionDelta[i]);
            c.Sediment = Math.Max(0, c.Sediment + sedimentDelta[i]); c.MudNutrient = Math.Max(0, c.MudNutrient + nutrientDelta[i]); c.MudGold = Math.Max(0, c.MudGold + mineralDelta[i]);
            float velocity = outgoing[i] / dt;
            if (velocity > .65f && c.SoilBody > 0 && !c.Bridge)
            {
                float eroded = Math.Min(c.SoilBody, (velocity - .65f) * .05f * dt / (1 + c.Grass + c.Wood * .4f));
                float nutrients = Math.Min(c.Earth, eroded * .3f);
                float gold = c.Mine == V1Mine.Active ? 0 : Math.Min(c.Gold, eroded * c.Gold / Math.Max(1, c.SoilBody));
                c.SoilBody -= eroded; c.Height -= eroded * Settings.sedimentHeightScale; c.Earth -= nutrients; c.Gold -= gold;
                c.Sediment += eroded; c.MudNutrient += nutrients; c.MudGold += gold;
            }
            if (velocity < .8f && c.Sediment > .0001f)
            {
                float deposited = Math.Min(c.Sediment, .16f * dt * (1 - velocity / .8f));
                float fraction = deposited / c.Sediment;
                c.Earth += c.MudNutrient * fraction; c.Gold += c.MudGold * fraction;
                c.MudNutrient *= 1 - fraction; c.MudGold *= 1 - fraction; c.Sediment -= deposited;
                // Re-depositing eroded soil must not create 4.8 times its original height.
                c.SoilBody += deposited; c.Height += deposited * Settings.sedimentHeightScale; c.Deposited += deposited;
            }
            if (c.Outlet && c.Water > 0)
            {
                float drained = Math.Min(c.Water, Settings.outletRate * dt), fraction = drained / c.Water;
                Ledger.WaterOutlet += drained; Ledger.PollutionOutlet += c.Pollution * fraction; Ledger.GoldOutlet += c.MudGold * fraction;
                c.Pollution *= 1 - fraction; c.Sediment *= 1 - fraction; c.MudNutrient *= 1 - fraction; c.MudGold *= 1 - fraction; c.Water -= drained;
            }
            if (c.Mine == V1Mine.Exhausted && c.Gold > .001f) c.Mine = V1Mine.None;
            Cells[i] = c;
        }
    }
    private float DesiredFlow(int from, int to, float level, float dt)
    {
        var a=snapshot[from];var b=snapshot[to];
        float head=b.Height+Settings.waterHeadScale*b.Water;
        float bank=a.River&&!b.River?Settings.bankResistance+Math.Min(.65f,b.Grass*.04f+b.Wood*.045f):0;
        float difference=level-head-.025f-bank;
        if(difference<=0)return 0;
        float flow=difference*Settings.waterFlow*dt;
        // River channels transport downstream; a raised bed still blocks the current
        // until upstream water overtops it. Roots slow overbank flow without making a wall.
        if(a.River&&b.River&&a.Height>b.Height+.001f)
            flow+=a.Water*Settings.riverCurrent*dt*Math.Min(1,(a.Height-b.Height)/.04f);
        return flow;
    }
    private static void Transfer(float[] delta, int from, int to, float amount) { delta[from] -= amount; delta[to] += amount; }
}
