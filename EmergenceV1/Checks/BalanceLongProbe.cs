using System;
public static class BalanceLongProbe
{
 public static int Main()
 {
  foreach(uint seed in new uint[]{2,17})
  {
   var s=V1Level.Create(new V1Settings{initialSeason=seed==17?V1Season.Spring:V1Season.Summer},seed,true);
   double initial=0;int land=0,max=0;foreach(var c in s.Cells){initial+=c.Water+c.Snow;if(!c.Blocked&&!c.River)land++;}
   for(int p=0;p<2250;p++){s.AdvancePhase();max=Math.Max(max,s.FloodCells);}
   double water=0;foreach(var c in s.Cells){if(float.IsNaN(c.Water)||c.Water<0)throw new Exception("Invalid resources");water+=c.Water+c.Snow;}
   var l=s.Ledger;double err=water-(initial+l.WaterInput-l.WaterGrowth-l.WaterFire-l.WaterEvaporated-l.WaterOutlet);
   Console.WriteLine($"seed={seed},season={s.Season},days=750,maxFlood={max}/{land},finalFlood={s.FloodCells},waterError={err:F4},everProsperous={s.Won}");
   if(max>=land*.25||s.FloodCells>=land*.15||Math.Abs(err)>2)throw new Exception("Random map long-run regression");
  }
  Console.WriteLine("PASS: fortunate and ordinary random maps retain dry land and conserve water over 750 days");return 0;
 }
}
