using System;
public static class GeneratedBalanceProbe
{
 public static void Main(){int wins=0;for(int seed=1;seed<=16;seed++){var g=V1WorldGenerator.Generate(40,28,new V1Settings{initialSeason=(V1Season)((seed-1)%4)},seed);int first=-1;float peak=0;for(int p=0;p<300;p++){g.simulation.AdvancePhase();peak=Math.Max(peak,g.simulation.Prosperity);if(first<0&&g.simulation.Won)first=p+1;}if(first>=0)wins++;Console.WriteLine($"seed={seed},first={first},peak={peak:F1}");}Console.WriteLine($"Natural prosperity within 100 days: {wins}/16");}
}
