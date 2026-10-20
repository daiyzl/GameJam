using System;

public sealed partial class V1Simulation
{
    private void FireAndGrowth(float dt)
    {
        Array.Copy(Cells, snapshot, Cells.Length); 
        Array.Clear(fireDelta, 0, fireDelta.Length);
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            var c = Cells[i];
            // Moist land dampens fire gradually; standing water extinguishes rapidly.
            // Both cooling and quenching use this cell's water, with a time-scaled rate.
            c.IgnitionHeat = Math.Max(0, c.IgnitionHeat - (Settings.fireHeatDecay + c.Water * .005f) * dt);
            float quench = IsWet(i) ? .8f : Settings.fireWaterQuench;
            float extinguished = Math.Min(c.Fire, Math.Min(c.Water * .5f, c.Water * quench * dt));
            c.Fire -= extinguished;
            c.Water -= extinguished * 2; Ledger.WaterFire += extinguished * 2;
            if (c.Fire > .0001f)
            {
                // Heat treats pollution even on bare polluted soil. Cleanup consumes
                // a finite amount of fire, independently of vegetation burned.
                float treated = Math.Min(c.Pollution, Math.Min(c.Fire / Math.Max(.001f, Settings.firePollutionCost),
                    Math.Min(6, c.Fire) * Settings.firePollutionCleanRate * dt));
                c.Pollution -= treated; Ledger.PollutionCleaned += treated;
                c.Fire = Math.Max(0, c.Fire - treated * Settings.firePollutionCost);
                float fuel = c.Wood + c.Grass;
                float burned = Math.Min(fuel, (.05f + c.Fire * Settings.fireBurnRate) * dt / (1 + c.Water * .18f));
                float woodBurn = fuel > 0 ? burned * c.Wood / fuel : 0;
                c.Wood = Math.Max(0, c.Wood - woodBurn); 
                c.Grass = Math.Max(0, c.Grass - (burned - woodBurn));
                c.Earth += burned * .5f;
                float cleaned = Math.Min(c.Pollution, burned * .85f);
                c.Pollution -= cleaned; 
                Ledger.PollutionCleaned += cleaned;
                c.Pollution += burned * .18f; 
                Ledger.PollutionMade += burned * .18f;
                c.FireAge += dt;
                c.Fire = Math.Max(0, c.Fire + burned * Settings.fireFuelGain - dt * (.04f + c.Fire * Settings.fireDecay + (fuel <= .01f ? .8f : 0)));
                if (c.Fire > c.Gold && c.Gold > 0)
                {
                    float lost = Math.Min(c.Gold, (c.Fire - c.Gold) * .03f * dt);
                    c.Gold -= lost; 
                    Ledger.GoldBurned += lost;
                }
            }
            else c.FireAge = 0;
            if (c.Gold > c.Wood && c.Wood > 0)
                c.Wood = Math.Max(0, c.Wood - Settings.goldPressure * (c.Gold - c.Wood) * dt);
            if (c.Pollution > 60)
            {
                float damage = (c.Pollution - 60) * .0008f * dt;
                c.Wood = Math.Max(0, c.Wood - damage); 
                c.Grass = Math.Max(0, c.Grass - damage);
            }
            if (c.Wood <= .0001f)
            {
                c.Wood = 0; c.Tree = false; c.TreeAge = 0;
                c.PlayerTree = false;
                if (snapshot[i].Fire > .01f) c.Seeds = 0;
            }
            Cells[i] = c;
            UpdateDeadLand(i);
        }
        // Reserve births in a stable order; each later candidate sees earlier reserved trees.
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && !IsBuilding(i))
        {
            var c = Cells[i];
            bool grow = snapshot[i].Fire <= .0001f && c.Fire <= .0001f && c.Pollution < 60 && !IsWet(i) && c.Mine != V1Mine.Active && !c.Dead(Settings.deadGold, Settings.mineGold);
            int before = PlantStage(i);
            if (grow && !c.Tree && c.Seeds > 0 && c.Gold < Settings.deadGold && c.Earth >= .1f && c.Water >= .2f && TreeCells3x3(i) < 4)
            {
                c.Tree = true; c.Wood = .1f; c.TreeAge = 0; c.Seeds--;
                c.Earth -= .1f; c.Water -= .2f; Ledger.WaterGrowth += .2f;
                c.PlayerTree = c.PlayerSeed; c.PlayerSeed = false;
                TreeBirths++; if (c.PlayerTree) { PlayerTreeBirths++; Event("种子建立树苗：本格 3×3 内已有树格少于 4。"); }
                Cells[i] = c;
            }
            if (grow)
            {
                float quality = Math.Max(0, 1 - c.Pollution / 60) / (1 + c.Snow * 3);
                if (c.Tree)
                {
                    float amount = Math.Min(14 - c.Wood, Math.Min(Settings.treeGrowth * quality * dt, Math.Min(c.Earth, c.Water * .5f)));
                    amount = Math.Max(0, amount);
                    c.Wood += amount; 
                    c.Earth -= amount; 
                    c.Water -= 2 * amount; 
                    Ledger.WaterGrowth += 2 * amount;
                    float ageBefore = c.TreeAge; 
                    c.TreeAge += dt;
                    if (c.Wood >= Settings.treeWood && (int)(ageBefore / 35) != (int)(c.TreeAge / 35))
                    {
                        int n = neighbors[i * 4 + Random(4)];
                        if (Valid(n) && !IsBuilding(n) && !Cells[n].Tree) AddSeeds(n, 1);
                    }
                }
                // Grass is separate from wood; only real biomass is usable as animal feed.
                float grass = Math.Min(2.5f - c.Grass, Math.Min(Settings.grassGrowth * quality * dt, Math.Min(c.Earth / .4f, c.Water / .8f)));
                grass = Math.Max(0, grass);
                c.Grass += grass; c.Earth -= grass * .4f; c.Water -= grass * .8f; Ledger.WaterGrowth += grass * .8f;
            }
            if (c.Tree && c.Grass > .1f) { float litter = Math.Min(c.Wood, c.Wood * .0004f * dt); c.Wood -= litter; c.Earth += litter * .5f; }
            Cells[i] = c;
            int after = PlantStage(i);
            if (before < 2 && after >= 2) { MatureTransitions++; Event("树苗木量达到 " + Settings.treeWood + "，长成单棵树。"); }
            if (before < 3 && after >= 3) { ForestTransitions++; Event("树木持续积累木量，达到树林阶段。"); }
        }
        // Heat is a finite budget deducted from the source. Small fires stay local;
        // neighbors must preheat before ignition and new fires cannot spread immediately.
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && snapshot[i].Fire >= Settings.fireSpreadThreshold && (snapshot[i].FireAge >= Settings.fireSpreadDelay || snapshot[i].Fire >= Settings.fireSpreadThreshold * 2) &&
            snapshot[i].Wood + snapshot[i].Grass + BuildingFuel(i) > .3f && !IsWet(i))
        {
            float budget = Math.Min(Cells[i].Fire, snapshot[i].Fire * Settings.fireHeatTransfer * dt);
            float sent = 0;
            for (int d = 0; d < 4; d++)
            {
                int n = neighbors[i * 4 + d]; if (!Valid(n)) continue;
                var c = Cells[n];
                if (!IsWet(n) && c.Fire <= .01f && c.Wood + c.Grass + BuildingFuel(n) > .3f)
                {
                    fireDelta[n] += budget * .25f; sent += budget * .25f;
                }
            }
            Cells[i].Fire = Math.Max(0, Cells[i].Fire - sent);
        }
        for (int i = 0; i < Cells.Length; i++)
        {
            if (fireDelta[i] > 0)
            {
                Cells[i].IgnitionHeat += fireDelta[i];
                if (Cells[i].IgnitionHeat >= Settings.fireIgnitionHeat)
                {
                    Cells[i].Fire = Math.Min(Settings.fireSpreadThreshold * .7f, Math.Max(1.4f, Cells[i].IgnitionHeat * 2));
                    Cells[i].IgnitionHeat = 0; Cells[i].FireAge = 0; FireSpreads++;
                }
            }
            if (Cells[i].Mine == V1Mine.Active && Cells[i].Gold <= 0) { Cells[i].Mine = V1Mine.Exhausted; Event("矿点已耗尽，留下废弃矿洞。"); }
        }
    }

    private void EnrichGold(float dt)
    {
        Array.Clear(goldDelta, 0, goldDelta.Length);
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && Cells[i].Mine == V1Mine.None && !Cells[i].DeadLand && Cells[i].Gold >= Settings.mineGold)
        {
            Cells[i].Mine = V1Mine.Active; GoldMinesFormed++;
            Event("金达到成矿阈值，矿点固定至枯竭。");
        }
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && Cells[i].Mine == V1Mine.None && !Cells[i].Dead(Settings.deadGold, Settings.mineGold) && Cells[i].Gold > .001f)
        {
            int targetStep = EnrichmentStep(i);
            if (targetStep < 0 || targetStep == i) continue;
            float amount = Cells[i].Gold * Math.Min(.5f, Settings.goldTransfer * dt);
            goldDelta[i] -= amount; goldDelta[targetStep] += amount;
        }
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            Cells[i].Gold = Math.Max(0, Cells[i].Gold + goldDelta[i]);
            if (Cells[i].Mine == V1Mine.None && !Cells[i].DeadLand && Cells[i].Gold >= Settings.mineGold)
            {
                Cells[i].Mine = V1Mine.Active; GoldMinesFormed++; Event("低处金富集形成新矿点。");
            }
            UpdateDeadLand(i);
        }
    }

    public int EnrichmentStep(int center)
    {
        if (!Valid(center) || Cells[center].Dead(Settings.deadGold, Settings.mineGold)) return center;
        for (int j = 0; j < 25; j++) { visited[j] = 0; parent[j] = -1; }
        int left = center % Width - 2, bottom = center / Width - 2;
        int head = 0, tail = 1, best = 12; queue[0] = 12; visited[12] = 1;
        float minimum = Cells[center].Height; int distance = 0;
        while (head < tail)
        {
            int local = queue[head++], lx = local % 5, ly = local / 5;
            int world = Index(left + lx, bottom + ly);
            int dist = Math.Abs(lx - 2) + Math.Abs(ly - 2);
            float h = Cells[world].Height;
            if (h < minimum - .0001f || Math.Abs(h - minimum) < .0001f && dist < distance)
            { minimum = h; best = local; distance = dist; }
            for (int d = 0; d < 4; d++)
            {
                int nx = lx + (d == 0 ? 1 : d == 2 ? -1 : 0), ny = ly + (d == 1 ? 1 : d == 3 ? -1 : 0);
                if (nx < 0 || nx > 4 || ny < 0 || ny > 4) continue;
                int next = nx + ny * 5, cell = Index(left + nx, bottom + ny);
                if (visited[next] != 0 || !Valid(cell) || IsBuilding(cell) || Cells[cell].Dead(Settings.deadGold, Settings.mineGold)) continue;
                visited[next] = 1; parent[next] = local; queue[tail++] = next;
            }
        }
        if (best == 12) return center;
        while (parent[best] != 12 && parent[best] >= 0) best = parent[best];
        return Index(left + best % 5, bottom + best / 5);
    }

    private void Pollution(float dt)
    {
        Array.Clear(pollutionDelta, 0, pollutionDelta.Length);
        // Reserve emissions before cleanup so a grass tile cannot cancel the entire source.
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && Cells[i].Dead(Settings.deadGold, Settings.mineGold))
        {
            float made = Settings.deadPollutionRate * dt; int count = 0;
            for (int d = 0; d < 4; d++) if (Valid(neighbors[i * 4 + d])) count++;
            pollutionDelta[i] += count == 0 ? made : made * .6f;
            if (count > 0) for (int d = 0; d < 4; d++)
            { int n = neighbors[i * 4 + d]; if (Valid(n)) pollutionDelta[n] += made * .4f / count; }
            Ledger.PollutionMade += made;
        }
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
        {
            var c = Cells[i];
            c.Pollution += pollutionDelta[i];
            float cleaned = Math.Min(c.Pollution, (c.Grass + c.Wood * .2f) * .006f * dt);
            c.Pollution -= cleaned; Ledger.PollutionCleaned += cleaned;
            if (c.Pollution > 50) c.Earth = Math.Max(0, c.Earth - (c.Pollution - 50) * .0004f * dt);
            // Low exposure already reduces soil quality; severe pollution also kills plants.
            c.Earth = Math.Max(0, c.Earth - Math.Min(60, c.Pollution) * .002f * dt);
            Cells[i] = c;
        }
        Array.Clear(pollutionDelta, 0, pollutionDelta.Length);
        Array.Copy(Cells, snapshot, Cells.Length);
        for (int i = 0; i < Cells.Length; i++) if (Valid(i))
            for (int d = 0; d < 2; d++)
            {
                int n = neighbors[i * 4 + d]; if (!Valid(n)) continue;
                float diff = (snapshot[i].Pollution - snapshot[n].Pollution) * Math.Min(.1f, .008f * dt);
                pollutionDelta[i] -= diff; pollutionDelta[n] += diff;
            }
        for (int i = 0; i < Cells.Length; i++) if (Valid(i)) Cells[i].Pollution = Math.Max(0, Cells[i].Pollution + pollutionDelta[i]);
    }
}
