using System;
using System.Collections.Generic;

public sealed partial class V1Simulation
{
    public V1Village VillageById(int id) { foreach (var v in Villages) if (v.active && v.id == id) return v; return null; }
    public int BuildingTier(int population) => population >= 28 ? 3 : population >= 18 ? 2 : 1;
    public bool InBuilding(int center, int cell, int tier)
    {
        if (cell < 0) return false;
        int dx = cell % Width - center % Width, dy = cell / Width - center / Width;
        int left = tier >= 2 ? -1 : 0, right = tier >= 3 ? 2 : 1, top = tier >= 2 ? 2 : 1;
        return dx >= left && dx <= right && dy >= 0 && dy <= top;
    }
    public bool IsBuilding(int cell) { foreach (var v in Villages) if (v.active && InBuilding(v.cell, cell, v.tier)) return true; return false; }
    private float BuildingFuel(int cell) { foreach(var v in Villages) if(v.active&&InBuilding(v.cell,cell,v.tier))return v.timber;return 0; }
    public bool NearRiver(int cell)
    {
        for (int y = -2; y <= 3; y++) for (int x = -2; x <= 3; x++)
        { int i = Index(cell % Width + x, cell / Width + y); if (Valid(i) && Cells[i].River) return true; }
        return false;
    }
    public bool CanSettle(int cell, int excludingId = -1)
    {
        if (!Valid(cell) || cell % Width < 2 || cell % Width >= Width - 3 || cell / Width < 2 || cell / Width >= Height - 3) return false;
        if (!BuildingSpace(cell, 1, excludingId)) return false;
        foreach (var v in Villages) if (v.active && v.id != excludingId && (InRegion(v.cell, cell) || InRegion(cell, v.cell))) return false;
        var r = ReadRegion(cell);
        return r.cells == 36 && r.habitable >= 18 && r.wet >= 2 && r.food >= 8 && r.health >= .5f;
    }
    private bool BuildingSpace(int center, int tier, int excludingId)
    {
        int left = tier >= 2 ? -1 : 0, right = tier >= 3 ? 2 : 1, top = tier >= 2 ? 2 : 1;
        for (int dy = 0; dy <= top; dy++) for (int dx = left; dx <= right; dx++)
        {
            int i = Index(center % Width + dx, center / Width + dy);
            if (!Valid(i) || Cells[i].River || Cells[i].Water > 3 || Cells[i].Fire > .1f || Cells[i].Mine == V1Mine.Active || i == CharacterCell) return false;
            foreach (var v in Villages) if (v.active && v.id != excludingId && InBuilding(v.cell, i, v.tier)) return false;
        }
        return true;
    }
    public V1Village AddVillage(int cell, int population = 8)
    {
        if (!CanSettle(cell) || ActiveVillages == 0 && !NearRiver(cell)) return null;
        var village = new V1Village { id = villageId++, cell = cell, population = population };
        Villages.Add(village); ClearBuildingVegetation(village); village.region = ReadRegion(cell); return village;
    }
    public V1Region ReadRegion(int center)
    {
        var r = new V1Region(); int drink = -1, owner = -1; float drinkDistance = float.MaxValue;
        foreach (var v in Villages) if (v.active && v.cell == center) { owner = v.id; break; }
        for (int dy = -2; dy <= 3; dy++) for (int dx = -2; dx <= 3; dx++)
        {
            int i = Index(center % Width + dx, center / Width + dy); if (i < 0) continue;
            r.cells++; if (!Valid(i)) continue; var c = Cells[i];
            r.grass += c.Grass; r.wood += c.Wood; r.water += c.Water; r.earth += c.Earth; r.pollution += c.Pollution;
            if (c.Water >= .5f) { r.wet++; float d = Math.Abs(dx) + Math.Abs(dy) + (c.River ? 0 : 10); if (d < drinkDistance) { drinkDistance = d; drink = i; } }
            if (c.Water > 4 && !c.River) r.flooded++;
            if (c.Grass > .2f || c.Tree) r.green++;
            if (c.Tree && c.Wood > 0) r.trees++;
            if (c.Mine == V1Mine.Active && c.Gold > 0 && c.Water < 4 && c.Fire < .1f) r.mines++;
            if (c.Fire > .1f) r.burning++;
            if (!c.River && c.Water < 4 && c.Fire < .1f && c.Pollution < 60 && !c.Dead(Settings.deadGold, Settings.mineGold)) r.habitable++;
        }
        r.pollution /= Math.Max(1, r.cells); r.drinkPollution = drink < 0 ? 100 : Cells[drink].Pollution;
        foreach (var a in Animals) if (a.alive && InRegion(center, a.cell))
        { if (a.origin == V1AnimalOrigin.Farmed) { if (a.owner == owner) r.farmed++; } else r.wild++; }
        r.food = Math.Min(r.habitable * 1.2f, 4 + r.grass * .42f + r.trees * .7f + r.farmed * 2.2f);
        r.capacity = Math.Min(r.habitable * 1.1f, r.food);
        float water = Math.Min(1, r.wet / 2f), quality = Math.Max(.05f, 1 - r.drinkPollution / 75);
        float pollution = Math.Max(.1f, 1 - r.pollution / 80), safety = Math.Max(0, 1 - (r.burning * .25f + r.flooded * .08f));
        r.health = Clamp(water * quality * pollution * safety, 0, 1);
        return r;
    }

    private void WorkVillages(float dt)
    {
        foreach (var v in Villages) if (v.active)
        {
            float buildingFire=0;int burningCell=-1;
            for(int dy=0;dy<=(v.tier>=2?2:1);dy++)for(int dx=v.tier>=2?-1:0;dx<=(v.tier>=3?2:1);dx++)
            {int i=Index(v.cell%Width+dx,v.cell/Width+dy);if(Valid(i)&&Cells[i].Fire>buildingFire){buildingFire=Cells[i].Fire;burningCell=i;}}
            if(burningCell>=0&&buildingFire>.01f)
            {
                float burned=Math.Min(v.timber,buildingFire*.12f*dt);v.timber-=burned;Ledger.TimberUsed+=burned;
                v.housing=Math.Max(4,v.housing-buildingFire*.06f*dt);Cells[burningCell].Fire+=burned*.7f;
                Cells[burningCell].Earth+=burned*.5f;Cells[burningCell].Pollution+=burned*.18f;Ledger.PollutionMade+=burned*.18f;
            }
            var r = ReadRegion(v.cell);
            if (r.health > .4f && r.food >= v.population * .5f)
            {
                int tree = -1, mine = -1;
                for (int dy = -2; dy <= 3; dy++) for (int dx = -2; dx <= 3; dx++)
                {
                    int i = Index(v.cell % Width + dx, v.cell / Width + dy); if (!Valid(i)) continue;
                    var c = Cells[i];
                    if (c.Fire > .1f || c.Water > 4 || c.Pollution > 60) continue;
                    if (c.Tree && c.Wood >= Settings.treeWood && !IsBuilding(i) && (tree < 0 || c.Wood > Cells[tree].Wood)) tree = i;
                    if (c.Mine == V1Mine.Active && c.Gold > 0 && (mine < 0 || c.Gold > Cells[mine].Gold)) mine = i;
                }
                if (mine >= 0)
                {
                    float amount = Math.Min(Cells[mine].Gold, .14f * dt * Math.Max(.25f, v.toolHealth));
                    Cells[mine].Gold -= amount; v.metal += amount; Ledger.GoldMined += amount;
                    Cells[mine].Pollution += amount * .6f; Ledger.PollutionMade += amount * .6f;
                    if (Cells[mine].Gold <= 0) { Cells[mine].Gold = 0; Cells[mine].Mine = V1Mine.Exhausted; Event("村庄开采使矿点枯竭，生产开始依赖库存。"); }
                }
                if (tree >= 0 && v.timber < 12)
                {
                    float amount = Math.Min(Math.Max(0, Cells[tree].Wood - 1.5f), .15f * dt * Math.Max(.2f, v.toolHealth));
                    Cells[tree].Wood -= amount; v.timber += amount; Ledger.TimberHarvested += amount;
                }
            }
            float metalNeed = v.population * .003f * dt, paidMetal = Math.Min(v.metal, metalNeed);
            v.metal -= paidMetal;
            v.toolHealth = Clamp(v.toolHealth + (paidMetal + .00001f >= metalNeed ? .018f : -.025f) * dt, 0, 1);
            float timberNeed = v.population * .008f * dt, paidTimber = Math.Min(v.timber, timberNeed);
            v.timber -= paidTimber; Ledger.TimberUsed += paidTimber;
            float smoke = v.population * .0015f * dt;
            Cells[v.cell].Pollution += smoke; Ledger.PollutionMade += smoke;
            if (!v.furnace && (r.pollution > 15 || r.drinkPollution > 25) && v.metal >= 1 && v.timber >= 3)
            { v.metal -= 1; v.timber -= 3; Ledger.TimberUsed += 3; v.furnace = true; Event("村民建设了治理炉，能处理附近的日常污染。"); }
            if (v.furnace)
            {
                if (!v.cleaning && (r.pollution >= 30 || r.drinkPollution >= 25)) v.cleaning = true;
                if (v.cleaning && r.pollution <= 15 && r.drinkPollution <= 15) v.cleaning = false;
                if (v.cleaning && v.timber >= .06f * dt && r.wet >= 2)
                {
                    v.timber -= .06f * dt; Ledger.TimberUsed += .06f * dt;
                    for (int d = -1; d < 4; d++)
                    {
                        int i = d < 0 ? v.cell : neighbors[v.cell * 4 + d]; if (!Valid(i)) continue;
                        float cleaned = Math.Min(Cells[i].Pollution, .14f * dt);
                        Cells[i].Pollution -= cleaned; Ledger.PollutionCleaned += cleaned;
                    }
                    v.furnaceHeat = Math.Max(0, v.furnaceHeat - dt * .08f);
                }
                else if (v.cleaning && v.timber > .1f)
                {
                    v.furnaceHeat += dt * .04f;
                    if (v.furnaceHeat > 1.2f)
                    {
                        int edge = neighbors[v.cell * 4 + Random(4)];
                        if (Valid(edge) && Cells[edge].Water < .5f && Cells[edge].Grass + Cells[edge].Wood > .5f)
                        { Cells[edge].Fire += .5f; v.furnaceHeat = .5f; Event("治理炉缺少安全水源，炉火外泄。"); }
                    }
                }
            }
            if (r.health > .7f && r.burning==0 && r.flooded==0 && v.timber >= 3 && v.metal >= 1 && v.housing < v.population + 4 && v.housing < 36)
            { v.timber -= 3; v.metal -= 1; Ledger.TimberUsed += 3; v.housing += 3; }
        }
    }

    private void VillagesPhase()
    {
        var parents = new List<V1Village>(Villages);
        foreach (var v in parents) if (v.active)
        {
            v.phases++; var r = ReadRegion(v.cell); v.region = r;
            float capacity = Math.Min(v.housing, r.capacity);
            bool severe = r.burning > 0 || r.flooded >= 4 || r.health < .35f || v.toolHealth < .15f;
            bool good = !severe && r.health >= .7f && capacity >= v.population + 1 && v.toolHealth > .4f && v.timber > .05f;
            if (good) { v.population += Math.Min(2, Math.Max(0, (int)capacity - v.population)); v.badPhases = 0; v.condition = "饮水、食物与工具充足，人口增长"; }
            else if (severe || capacity < v.population - 1 || v.timber <= .01f)
            {
                v.population = Math.Max(0, v.population - (severe ? 2 : 1)); v.badPhases++;
                v.condition = r.drinkPollution > 25 ? "取水点受污染，健康下降" : r.burning > 0 ? "火灾伤害村庄" : r.flooded > 0 ? "积水影响居住" : v.toolHealth < .4f ? "金材断供，工具磨损" : "食物、木材或居住容量不足";
            }
            else { v.badPhases = Math.Max(0, v.badPhases - 1); v.condition = r.mines > 0 ? "矿源有效，村庄稳定生产" : "暂无有效矿源，消耗储备"; }
            int tier = BuildingTier(v.population);
            if (tier <= v.tier || BuildingSpace(v.cell, tier, v.id)) { if (tier != v.tier) { v.tier = tier; ClearBuildingVegetation(v); } }
            else v.condition += "；扩建空间不足";
            if (v.population == 0) { Leave(v); continue; }
            if (v.badPhases >= Settings.migrationPhases)
            {
                int target = BestSettlement(v.id, v.cell);
                if (target >= 0)
                { v.cell = target; v.badPhases = 0; v.migrations++; v.tier = 1; ClearBuildingVegetation(v); Event("村庄因环境恶化迁移到更适宜的河岸。"); }
                else { v.population = Math.Max(0, v.population - 2); v.condition = "无可达迁移地，居民逐步搬离"; if (v.population == 0) Leave(v); }
            }
            if (v.population >= 20 && v.timber >= 5 && v.metal >= 3 && AdvanceCount % 4 == 0 && ActiveVillages < 4)
            {
                int candidate = BestSettlement(-1, v.cell);
                if (candidate >= 0)
                {
                    var child = AddVillage(candidate, 4);
                    if (child != null) { v.population -= 4; v.timber -= 5; v.metal -= 3; child.timber = 3; child.metal = 2; NewVillages++; Event("居民带着储备建立新村庄，原村人口与建材已扣除。"); }
                }
            }
        }
        bool ore = false; foreach (var c in Cells) if (c.Mine == V1Mine.Active && c.Gold > 0) { ore = true; break; }
        if (ActiveVillages == 0 && ore && AdvanceCount % 6 == 0)
        {
            int cell = BestSettlement(-1, -1);
            if (cell >= 0 && NearRiver(cell)) { AddVillage(cell, 6); Event("恢复的河岸吸引了外来居民。"); }
        }
    }
    private void Leave(V1Village v)
    {
        v.active = false; v.population = 0;
        foreach (var a in Animals) if (a.alive && a.owner == v.id) { a.origin = V1AnimalOrigin.Wild; a.owner = -1; }
        Event("村庄居民已搬离；改善环境后仍可吸引新居民。");
    }
    private int BestSettlement(int excludingId, int from)
    {
        int best = -1; float score = .75f;
        bool[] reachable = from >= 0 ? Reachability(from) : null;
        for (int y = 2; y < Height - 3; y++) for (int x = 2; x < Width - 3; x++)
        {
            int i = Index(x, y); if (i == from || reachable != null && !reachable[i] || !CanSettle(i, excludingId)) continue;
            var r = ReadRegion(i); float value = r.health + r.food * .006f + r.mines * .04f;
            if (from >= 0) value -= (Math.Abs(x - from % Width) + Math.Abs(y - from / Width)) * .004f;
            if (value > score) { score = value; best = i; }
        }
        return best;
    }
    public bool Reachable(int from, int to)
    {
        if (!Valid(from) || !Valid(to)) return false;
        return Reachability(from)[to];
    }
    private bool[] Reachability(int from)
    {
        var seen = new bool[Cells.Length]; var path = new int[Cells.Length]; int head = 0, tail = 1;
        path[0] = from; seen[from] = true;
        while (head < tail)
        {
            int i = path[head++];
            for (int d = 0; d < 4; d++)
            { int n = neighbors[i * 4 + d]; if (!Valid(n) || seen[n] || Cells[n].River && !Cells[n].Bridge || Cells[n].Water > 4 && !Cells[n].Bridge) continue; seen[n] = true; path[tail++] = n; }
        }
        return seen;
    }
    private void ClearBuildingVegetation(V1Village v)
    {
        for (int dy = 0; dy <= (v.tier >= 2 ? 2 : 1); dy++) for (int dx = v.tier >= 2 ? -1 : 0; dx <= (v.tier >= 3 ? 2 : 1); dx++)
        {
            int i = Index(v.cell % Width + dx, v.cell / Width + dy); if (!Valid(i)) continue;
            if (Cells[i].Tree) { v.timber += Cells[i].Wood; Ledger.TimberHarvested += Cells[i].Wood; }
            Cells[i].Tree = false; Cells[i].Wood = 0; Cells[i].Grass = 0;
        }
    }
    private void ProsperityPhase()
    {
        float health = 0, effectivePopulation = 0; int villages = 0;
        foreach (var v in Villages) if (v.active)
        { var r = ReadRegion(v.cell); float h = r.health * (.5f + v.toolHealth * .5f); effectivePopulation += v.population * h; health += h; villages++; }
        int habitable = 0, green = 0, mature = 0, wild = 0, farmed = 0, species = 0;
        var kinds = new bool[4];
        for (int i = 0; i < Cells.Length; i++) if (Valid(i) && !Cells[i].River)
        { habitable++; if (Cells[i].Grass > .3f && Cells[i].Pollution < 40) green++; if (PlantStage(i) >= 2 && Cells[i].Pollution < 40) mature++; }
        foreach (var a in Animals) if (a.alive && a.hunger == 0)
        { if (a.origin == V1AnimalOrigin.Wild) { wild++; kinds[(int)a.kind] = true; } else if (VillageById(a.owner) != null) farmed++; }
        foreach (bool k in kinds) if (k) species++;
        float ecology = 6f * green / Math.Max(1, habitable) + Math.Min(14, mature * .65f);
        Prosperity = Clamp(Math.Min(40, effectivePopulation / 32 * 40) + (villages > 0 ? 20 * health / villages : 0) + ecology + Math.Min(10, wild * .5f + species * 1.5f) + Math.Min(10, farmed), 0, 100);
        if (villages > 0 && Prosperity >= Settings.prosperityTarget) StablePhases++; else StablePhases = 0;
        if (!Won && StablePhases >= Settings.prosperityHoldPhases) { Won = true; Event("河岸达到持续繁荣！可以继续观察和建设。"); }
    }
}
