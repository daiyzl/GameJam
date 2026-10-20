using System;

// Shared authored layout used by the scene builder and standalone verification.
public static class V1Level
{
    public const int Width = 40, Height = 28, OriginX = -4, OriginY = -2;
    public static int Cell(V1Simulation sim, int worldX, int worldY) => sim.Index(worldX - OriginX, worldY - OriginY);
    public static V1Cell Default(int worldX, int worldY)
    {
        bool river = (worldX == 12 || worldX == 13) && worldY > -2 && worldY < 25 || worldY >= 17 && worldY <= 18 && worldX >= 13 && worldX <= 30;
        bool boundary = worldX == -4 || worldX == 35 || worldY == -2 || worldY == 25;
        bool path = worldY >= 4 && worldY <= 5 && worldX >= 3 && worldX <= 28;
        return new V1Cell {
            Earth = river ? 4 : 12, Grass = river ? 0 : path ? .3f : 1.5f,
            Water = river ? 8 : 1.2f, Height = river ? .2f + (worldY + 1) * .04f : 2.8f,
            SoilBody = river ? 25 : 30, Blocked = boundary, River = river,
            Source = (worldX == 12 || worldX == 13) && worldY == 24,
            Outlet = (worldX == 12 || worldX == 13) && worldY == -1,
            Bridge = (worldX == 12 || worldX == 13) && (worldY == 4 || worldY == 5 || worldY == 17 || worldY == 18)
        };
    }
    public static V1Simulation Create(V1Settings settings = null)
    {
        var sim = new V1Simulation(Width, Height, settings);
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) sim.Cells[sim.Index(x, y)] = Default(x + OriginX, y + OriginY);
        ApplyFeatures(sim); sim.Initialize(); PopulateResidents(sim); return sim;
    }
    public static void ApplyFeatures(V1Simulation sim)
    {
        int[,] trees = { {6,6},{7,10},{10,6},{11,11},{6,12},{9,12},{2,15},{3,19},{7,21},{17,7},{18,11},{21,13},{24,20},{27,16},{29,10},{31,21},{22,7},{24,7},{22,9},{24,9} };
        for (int j = 0; j < trees.GetLength(0); j++)
        { int i = Cell(sim, trees[j, 0], trees[j, 1]); sim.Cells[i].Tree = true; sim.Cells[i].Wood = j < 6 ? 7 : 5; sim.Cells[i].Water = 4; sim.Cells[i].Earth = 16; }
        foreach (int i in new[] { Cell(sim,7,11), Cell(sim,8,6), Cell(sim,18,8) }) sim.Cells[i].Gold = 30;
        for (int y = 14; y <= 18; y++) for (int x = 3; x <= 7; x++)
        { int i = Cell(sim,x,y); sim.Cells[i].Gold = 3; sim.Cells[i].Height = 3.1f; }
        int basin = Cell(sim,5,16); sim.Cells[basin].Height = 1.6f; sim.Cells[Cell(sim,4,17)].Gold = 12;
        int mud = Cell(sim,12,16); sim.Cells[mud].Sediment = 9; sim.Cells[mud].MudNutrient = 2; sim.Cells[mud].Deposited = 2.2f; sim.Cells[mud].Height += .6f;
        int bank = Cell(sim,11,20); sim.Cells[bank].Height = 3.3f; sim.Cells[bank].Grass = .2f;
        // Remote polluted water is a visible, local source problem rather than a village timer.
        sim.Cells[Cell(sim,13,20)].Pollution = 35; sim.Cells[Cell(sim,14,20)].Pollution = 45;
        foreach (int i in new[] { Cell(sim,2,2), Cell(sim,1,20), Cell(sim,30,22), Cell(sim,28,7) }) sim.Cells[i].Blocked = true;
    }
    public static void PopulateResidents(V1Simulation sim)
    {
        var v = sim.AddVillage(Cell(sim,9,8),8); if (v == null) throw new InvalidOperationException("Initial riverside village cannot settle");
        sim.AddAnimal(Cell(sim,8,7),V1AnimalKind.Cow,V1AnimalOrigin.Farmed,v.id);
        sim.AddAnimal(Cell(sim,7,7),V1AnimalKind.Cow,V1AnimalOrigin.Farmed,v.id);
        sim.AddAnimal(Cell(sim,11,7),V1AnimalKind.Chicken,V1AnimalOrigin.Farmed,v.id);
        sim.AddAnimal(Cell(sim,11,6),V1AnimalKind.Chicken,V1AnimalOrigin.Farmed,v.id);
        for (int kind = 0; kind < 4; kind++)
        {
            int x = kind < 2 ? 5 : 20, y = kind < 2 ? 11 + kind * 2 : 12 + kind * 2;
            sim.AddAnimal(Cell(sim,x,y),(V1AnimalKind)kind,V1AnimalOrigin.Wild);
            sim.AddAnimal(Cell(sim,x+1,y),(V1AnimalKind)kind,V1AnimalOrigin.Wild);
        }
    }
}
