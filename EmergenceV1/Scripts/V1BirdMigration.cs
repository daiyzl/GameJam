using System;

// Results of one manually advanced phase. Views only play these results back.
public struct V1BirdLanding
{
    public int cell, entryEdge;
    public bool broughtSeed;
}

public sealed partial class V1Simulation
{
    // All seed sources share the same storage and compost rule.
    public int AddSeeds(int cell, int amount, bool player = false)
    {
        if (!Valid(cell) || IsBuilding(cell) || amount <= 0) return 0;
        NormalizeSeedStock(cell);
        int retained = Math.Min(amount, SeedCapacity - Cells[cell].Seeds);
        Cells[cell].Seeds += retained;
        int excess = amount - retained;
        Cells[cell].Earth += excess * ExcessSeedEarth;
        SeedsComposted += excess;
        if (player && retained > 0) Cells[cell].PlayerSeed = true;
        return retained;
    }

    private void NormalizeSeedStock(int cell)
    {
        int excess = Math.Max(0, Cells[cell].Seeds - SeedCapacity);
        Cells[cell].Seeds = Math.Max(0, Math.Min(SeedCapacity, Cells[cell].Seeds));
        Cells[cell].Earth += excess * ExcessSeedEarth;
        SeedsComposted += excess;
        if (Cells[cell].Seeds == 0) Cells[cell].PlayerSeed = false;
    }

    private void NormalizeSeedStocks()
    {
        for (int i = 0; i < Cells.Length; i++) NormalizeSeedStock(i);
    }

    public bool BirdCanLand(int cell)
    {
        if (!Valid(cell) || IsBuilding(cell) || IsWet(cell)) return false;
        var c = Cells[cell];
        return !c.Bridge && !c.River && c.Fire <= .0001f && c.Pollution < 60 &&
            c.Mine == V1Mine.None && !c.Dead(Settings.deadGold, Settings.mineGold);
    }

    private void BirdMigrationPhase()
    {
        BirdLandings.Clear();
        if (Season != V1Season.Spring || Settings.springBirdMigrationChance <= 0) return;
        if (Random(10000) >= Clamp(Settings.springBirdMigrationChance, 0, 1) * 10000) return;

        // Choose a shared resting area, then nearby cells for individual birds.
        // Reservoir sampling also handles maps with very few safe landing cells.
        int center = -1, candidates = 0;
        for (int i = 0; i < Cells.Length; i++)
            if (BirdCanLand(i) && Random(++candidates) == 0) center = i;
        if (center < 0) return;
        int edge = Random(4), count = 4 + Random(4);
        for (int b = 0; b < count; b++)
        {
            int landing = center, seen = 0;
            for (int y = -3; y <= 3; y++) for (int x = -3; x <= 3; x++)
            {
                int i = Index(center % Width + x, center / Width + y);
                if (BirdCanLand(i) && Random(++seen) == 0) landing = i;
            }
            bool seed = Random(10000) < 3000; // Independent 0.3 chance for each bird.
            BirdLandings.Add(new V1BirdLanding { cell = landing, entryEdge = edge, broughtSeed = seed });
            if (seed) { AddSeeds(landing, 1); BirdSeeds++; }
        }
        BirdMigrations++;
        // No forecast, toast or event-log prompt: the flock and seeds are the feedback.
    }
}
