public static class PrototypeLevels
{
    public const int Count = 3;
    public static readonly string[] Names = { "01 · 切开水路", "02 · 火与金的取舍", "03 · 五行实验场" };
    public static LevelDefinition Create(int index)
    {
        if (index == 2) return Laboratory();
        LevelDefinition level = Corridor();
        level.Name = Names[index == 0 ? 0 : 1];
        level.Hint = index == 0
            ? "在木墙旁投入金。木量低于 8 后水可以通过；土和水会使残存木重新生长。"
            : "金只切木；火会蔓延、回土并消耗灭火水。保护右上方的守护树。";
        level.Budget[(int)Element.Gold] = 6;
        if (index == 1)
        {
            Set(level, 3, 2, Element.Wood, 18);
            Set(level, 4, 2, Element.Wood, 6);
            Open(level, 4, 3); Set(level, 4, 3, Element.Wood, 7); Set(level, 4, 3, Element.Earth, 3);
            Open(level, 6, 3); Set(level, 6, 3, Element.Wood, 5); Set(level, 6, 3, Element.Earth, 2);
            level.ProtectedIndex = level.Index(6, 3); level.Placeable[level.ProtectedIndex] = false;
            level.Budget[(int)Element.Fire] = 2; level.Budget[(int)Element.Earth] = 4;
            level.Budget[(int)Element.Water] = 4;
            Set(level, 0, 2, Element.Water, 48);
            level.StepLimit = 60;
        }
        return level;
    }
    private static LevelDefinition Corridor()
    {
        var level = new LevelDefinition(9, 5);
        for (int i = 0; i < level.Blocked.Length; i++) level.Blocked[i] = true;
        for (int x = 0; x < 9; x++) Open(level, x, 2);
        Open(level, 3, 1); Open(level, 3, 3); Open(level, 4, 1);
        level.SourceIndex = level.Index(0, 2); level.GoalIndex = level.Index(8, 2);
        level.Placeable[level.SourceIndex] = false; level.Placeable[level.GoalIndex] = false;
        Set(level, 0, 2, Element.Water, 36);
        Set(level, 3, 2, Element.Wood, 12); Set(level, 3, 2, Element.Earth, 3);
        level.TargetWater = 10; level.StepLimit = 40;
        return level;
    }
    private static LevelDefinition Laboratory()
    {
        var level = new LevelDefinition(9, 6) { Name = "03 · 五行实验场", Hint = "自由投放五行，单步观察。火先烧木变强，跨过阈值后才能损伤金。", StepLimit = 100, TargetWater = 12 };
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++) Open(level, x, y);
        level.SourceIndex = level.Index(0, 2); level.GoalIndex = level.Index(8, 2);
        level.Placeable[level.SourceIndex] = false; level.Placeable[level.GoalIndex] = false;
        Set(level, 0, 2, Element.Water, 40);
        Set(level, 3, 2, Element.Wood, 12); Set(level, 3, 2, Element.Earth, 4);
        Set(level, 5, 4, Element.Wood, 12); Set(level, 5, 4, Element.Fire, 1);
        Set(level, 6, 4, Element.Gold, 5); Set(level, 6, 4, Element.Wood, 4);
        Set(level, 3, 4, Element.Wood, 7); Set(level, 3, 4, Element.Earth, 4); Set(level, 3, 4, Element.Water, 8);
        level.Budget[0] = 8; level.Budget[1] = 6; level.Budget[2] = 12; level.Budget[3] = 4; level.Budget[4] = 8;
        return level;
    }
    private static void Open(LevelDefinition level, int x, int y)
    {
        int i = level.Index(x, y);
        level.Blocked[i] = false; level.Placeable[i] = true;
        // Fixed slope carries finite water from left to right. Side slots sit slightly higher.
        level.Elevation[i] = (level.Width - 1 - x) * 8 + (y == 2 ? 0 : 2);
    }
    private static void Set(LevelDefinition level, int x, int y, Element element, float amount)
    {
        int i = level.Index(x, y);
        CellState c = level.Initial[i]; c.Set(element, amount); level.Initial[i] = c;
    }
}
