using System;
public static class BalanceProbe
{
 public static void Main()
 {
  int wins=0,early=0;
  for(uint seed=1;seed<=24;seed++)
  {
   var settings=new V1Settings{initialSeason=(V1Season)((seed-1)%4)};
   var s=V1Level.Create(settings,seed,true); float peak=0;int first=-1;
   for(int p=0;p<300;p++){s.AdvancePhase();peak=Math.Max(peak,s.Prosperity);if(s.Won&&first<0)first=p+1;}
   if(first>0)wins++;if(first>0&&first<=12)early++;
   Console.WriteLine($"seed={seed},season={s.Season},peak={peak:F1},firstProsperityPhase={first}");
  }
  Console.WriteLine($"Winners={wins}/24 Early={early}");
 }
}
