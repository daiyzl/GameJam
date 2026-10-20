using System;
using System.Collections.Generic;

public sealed partial class V1Simulation
{
    public V1Animal AddAnimal(int cell, V1AnimalKind kind, V1AnimalOrigin origin, int owner = -1)
    {
        if (!Walkable(cell) || Cells[cell].Fire > .1f || Cells[cell].Dead(Settings.deadGold, Settings.mineGold)) return null;
        if (origin == V1AnimalOrigin.Farmed && (VillageById(owner) == null || !InRegion(VillageById(owner).cell, cell))) return null;
        var animal = new V1Animal { id = animalId++, cell = cell, kind = kind, origin = origin, owner = owner };
        Animals.Add(animal); return animal;
    }
    public float AnimalHabitat(int cell, V1AnimalKind kind)
    {
        if (!Walkable(cell)) return -100;
        var c = Cells[cell]; if (c.Fire > .1f || c.Pollution > 60 || c.Dead(Settings.deadGold, Settings.mineGold)) return -100;
        float water = Math.Min(1, c.Water);
        for (int d = 0; d < 4; d++) { int n = neighbors[cell * 4 + d]; if (Valid(n)) water = Math.Max(water, Math.Min(1, Cells[n].Water)); }
        float cover = c.Wood;
        for (int d = 0; d < 4; d++) { int n = neighbors[cell * 4 + d]; if (Valid(n)) cover = Math.Max(cover, Cells[n].Wood); }
        return c.Grass * (kind == V1AnimalKind.Cow ? 1.4f : .9f) + (kind == V1AnimalKind.Deer ? Math.Min(2, cover * .3f) : .1f) + water - c.Pollution * .03f;
    }
    private void AnimalsPhase()
    {
        var before = new List<V1Animal>(Animals); var born = new List<V1Animal>();
        foreach (var a in before) if (a.alive)
        {
            a.age++; var owner = VillageById(a.owner);
            if (a.origin == V1AnimalOrigin.Farmed && owner == null) { a.origin = V1AnimalOrigin.Wild; a.owner = -1; }
            int best = a.cell; float score = AnimalHabitat(best, a.kind);
            // Choose among safe habitat candidates by weight, rather than tracing a fixed
            // best-score loop. Previous cells are less attractive, but remain escape routes.
            var candidates = new int[5]; var weights = new float[5]; int choices = 0;
            for (int d = 0; d < 4; d++)
            {
                int n = neighbors[a.cell * 4 + d]; if (!Valid(n)) continue;
                if (a.origin == V1AnimalOrigin.Farmed && !InRegion(owner.cell, n)) continue;
                float value = AnimalHabitat(n, a.kind);
                if (value <= -100 || value < score - 1.5f) continue;
                candidates[choices] = n; weights[choices++] = Math.Max(.1f, value + 1) * (n == a.previousCell ? .25f : 1);
            }
            if (score > -100) { candidates[choices] = a.cell; weights[choices++] = Math.Max(.1f, score + 1) * .3f; }
            float total = 0; for (int j = 0; j < choices; j++) total += weights[j];
            float roll = Random(10000) / 10000f * total;
            for (int j = 0; j < choices; j++) { roll -= weights[j]; if (roll < 0) { best = candidates[j]; break; } }
            if (best != a.cell) { a.previousCell = a.cell; a.cell = best; if (a.origin == V1AnimalOrigin.Wild) WildlifeMoves++; }
            score = AnimalHabitat(a.cell, a.kind);
            float demand = a.kind == V1AnimalKind.Cow ? .15f : .08f;
            if (score >= .8f && Cells[a.cell].Grass >= demand)
            { Cells[a.cell].Grass -= demand; Cells[a.cell].Earth += demand * .2f; a.hunger = Math.Max(0, a.hunger - 1); }
            else a.hunger++;
            if (a.hunger >= 5 || Cells[a.cell].Fire > 1) { a.alive = false; continue; }
            if (a.age % 4 != 0 || a.hunger != 0 || score < 1.2f) continue;
            int count = 0; bool mate = false;
            foreach (var b in before) if (b.alive && b.origin == a.origin && (a.origin == V1AnimalOrigin.Wild || b.owner == a.owner))
            { count++; if (b.id != a.id && b.kind == a.kind && Math.Abs(b.cell % Width - a.cell % Width) + Math.Abs(b.cell / Width - a.cell / Width) <= 4) mate = true; }
            int pending = 0; foreach (var b in born) if (b.owner == a.owner && b.origin == a.origin) pending++;
            int limit = a.origin == V1AnimalOrigin.Farmed ? Settings.animalCapacity : 24;
            if (mate && count + pending < limit && a.id % 2 == 0)
                born.Add(new V1Animal { id = animalId++, cell = a.cell, kind = a.kind, origin = a.origin, owner = a.owner });
        }
        foreach (var animal in born) { Animals.Add(animal); if (animal.origin == V1AnimalOrigin.Farmed) { FarmBirths++; Event("草料与水源支持养殖繁殖。"); } }
        if (AdvanceCount % 6 == 0)
        {
            int alive = 0; foreach (var a in Animals) if (a.alive && a.origin == V1AnimalOrigin.Wild) alive++;
            int edge = Index(1, 2 + Random(Height - 4)); var kind = (V1AnimalKind)Random(4);
            if (alive < 24 && AnimalHabitat(edge, kind) > 1.5f) AddAnimal(edge, kind, V1AnimalOrigin.Wild);
        }
    }
}
