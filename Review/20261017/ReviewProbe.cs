using System;

public static class ReviewProbe
{
    public static int Main()
    {
        var defaults = new V1Settings();
        var changed = new V1Settings { terrainNoiseScale = .31f, terrainAmplitude = 1f, generateTerrain = false };
        var a = V1WorldGenerator.Generate(40, 28, defaults, 317).simulation;
        var b = V1WorldGenerator.Generate(40, 28, changed, 317).simulation;
        int differentHeights = 0;
        for (int i = 0; i < a.Cells.Length; i++)
            if (a.Cells[i].Height != b.Cells[i].Height) differentHeights++;
        Console.WriteLine("Procedural generation: changed noise scale, amplitude and generateTerrain; different heights=" + differentHeights + "/" + a.Cells.Length);
        Console.WriteLine("This is an observed configuration limitation, not a passing feature assertion.");
        return 0;
    }
}
