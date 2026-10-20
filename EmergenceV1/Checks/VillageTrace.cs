using System;
public static class VillageTrace
{
    public static void Main()
    {
        var s=V1Level.Create();
        for(int p=0;p<=20;p++)
        {
            foreach(var v in s.Villages)
            {
                var r=s.ReadRegion(v.cell);
                Console.WriteLine($"phase={p} day={s.Day} id={v.id} at=({v.cell%s.Width+V1Level.OriginX},{v.cell/s.Width+V1Level.OriginY}) active={v.active} pop={v.population} tier={v.tier} moves={v.migrations} bad={v.badPhases} wood={v.timber:F2} metal={v.metal:F2} food={r.food:F2} health={r.health:F2} pollution={r.pollution:F2} drink={r.drinkPollution:F2} condition={v.condition}");
            }
            s.AdvancePhase();
        }
    }
}
