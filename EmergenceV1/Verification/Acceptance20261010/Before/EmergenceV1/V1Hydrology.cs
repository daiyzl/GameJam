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
                float head = snapshot[n].Height + Settings.waterHeadScale * snapshot[n].Water;
                desired += Math.Max(0, level - head - .025f) * Settings.waterFlow * dt;
            }
            float limit = Math.Min(snapshot[i].Water, desired / (1 + snapshot[i].Grass * .08f + snapshot[i].Wood * .025f));
            if (desired <= .0001f) continue;
            for (int d = 0; d < 4; d++)
            {
                int n = neighbors[i * 4 + d]; if (!Valid(n)) continue;
                float head = snapshot[n].Height + Settings.waterHeadScale * snapshot[n].Water;
                float flow = Math.Max(0, level - head - .025f) * Settings.waterFlow * dt * limit / desired;
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
                c.SoilBody -= eroded; c.Height -= eroded * .025f; c.Earth -= nutrients; c.Gold -= gold;
                c.Sediment += eroded; c.MudNutrient += nutrients; c.MudGold += gold;
            }
            if (velocity < .8f && c.Sediment > .0001f)
            {
                float deposited = Math.Min(c.Sediment, .16f * dt * (1 - velocity / .8f));
                float fraction = deposited / c.Sediment;
                c.Earth += c.MudNutrient * fraction; c.Gold += c.MudGold * fraction;
                c.MudNutrient *= 1 - fraction; c.MudGold *= 1 - fraction; c.Sediment -= deposited;
                c.SoilBody += deposited; c.Height += deposited * .12f; c.Deposited += deposited;
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
    private static void Transfer(float[] delta, int from, int to, float amount) { delta[from] -= amount; delta[to] += amount; }
}
