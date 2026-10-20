using System;
public static class EcologyTrace
{
    static double Sum(V1Simulation s,bool gold){double n=0;foreach(var c in s.Cells)n+=gold?c.Gold+c.MudGold:c.Water;return n;}
    public static void Main()
    {
        var s=V1Level.Create();double water=Sum(s,false),gold=Sum(s,true);
        for(int p=0;p<=2250;p++)
        {
            if(p%150==0||p==96)
            {
                int flooded=0,land=0;float maxWater=0,minHeight=float.MaxValue,maxHeight=float.MinValue;
                foreach(var c in s.Cells)if(!c.Blocked){if(!c.River){land++;if(c.Water>4)flooded++;}maxWater=Math.Max(maxWater,c.Water);minHeight=Math.Min(minHeight,c.Height);maxHeight=Math.Max(maxHeight,c.Height);}
                var l=s.Ledger;
                Console.WriteLine($"day={s.Day} phase={p} flooded={flooded}/{land} water={Sum(s,false):F2} maxWater={maxWater:F2} height={minHeight:F2}..{maxHeight:F2} in={l.WaterInput:F2} out={l.WaterOutlet:F2} gold={Sum(s,true):F4} waterError={Sum(s,false)-(water+l.WaterInput-l.WaterGrowth-l.WaterFire-l.WaterEvaporated-l.WaterOutlet):F4} goldError={Sum(s,true)-(gold+l.GoldInput-l.GoldMined-l.GoldBurned-l.GoldOutlet):F4}");
            }
            if(p<2250)s.AdvancePhase();
        }
        foreach(float amount in new[]{1f,4f,8f})
        {
            var f=V1Level.Create();int cell=V1Level.Cell(f,11,11);f.Cells[cell].Tree=true;f.Cells[cell].Wood=8;f.Cells[cell].Water=4;
            f.PlaceElement(cell,V1Element.Fire,amount);
            for(int p=0;p<3;p++)f.AdvancePhase();
            Console.WriteLine($"fireTool={amount}, wetTreeWood={f.Cells[cell].Wood:F3}, tree={f.Cells[cell].Tree}, fire={f.Cells[cell].Fire:F3}, spreads={f.FireSpreads}");
        }
    }
}
