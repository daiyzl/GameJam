using System;
using System.Collections.Generic;
using System.Diagnostics;
public static class GeneratorChecks
{
 static int checks;
 static void Check(bool b,string m){checks++;if(!b)throw new Exception(m);}
 static string Signature(V1Simulation s){var b=new System.Text.StringBuilder();foreach(var c in s.Cells)b.Append(c.River?'R':c.Blocked?'#':c.Tree?'T':'.');return b.ToString();}
 static double Water(V1Simulation s){double n=0;foreach(var c in s.Cells)n+=c.Water+c.Snow;return n;}
 public static int Main(string[] args)
 {
  try{
   var signatures=new HashSet<string>();var starts=new HashSet<int>();var villages=new HashSet<int>();var outlets=new HashSet<int>();var watch=Stopwatch.StartNew();
   int lakeLayouts=0,branchLayouts=0,maxRivers=0,earlyWins=0;
   for(int seed=1;seed<=64;seed++)
   {
    var g=V1WorldGenerator.Generate(40,28,new V1Settings(),seed);var s=g.simulation;
    Check(Signature(s)==Signature(V1WorldGenerator.Generate(40,28,new V1Settings(),seed).simulation),"Seed reproduces all physical layout: "+seed);
    Check(s.ActiveVillages>=1&&s.SafeLanding(g.spawn)&&s.NearRiver(g.village),"Safe riverside start: "+seed);
    Check(s.SafeLanding(g.tutorial)&&!s.Cells[g.tutorial].Tree&&s.TreeCells3x3(g.tutorial)<4,"Usable random tutorial plot: "+seed);
    int rivers=0,sources=0,drains=0;
    for(int i=0;i<s.Cells.Length;i++)
    {
     var c=s.Cells[i];int x=i%s.Width,y=i/s.Width;
     if(x==0||x==39||y==0||y==27)Check(c.Blocked&&!c.River&&!c.Bridge,"Fixed solid outer boundary");
     if(c.River)rivers++;if(c.Source)sources++;if(c.Outlet){drains++;outlets.Add(i);}
    }
    Check(sources>=1&&drains>=sources,"Outlet capacity covers river sources: "+seed+", sources="+sources+", outlets="+drains);
    Check(rivers>25&&rivers<400&&g.bridges.Count>0,"Variable bounded rivers and crossings: "+seed+", rivers="+rivers+", bridges="+g.bridges.Count);
    var seen=new bool[s.Cells.Length];var q=new Queue<int>();for(int i=0;i<seen.Length;i++)if(s.Cells[i].Outlet){seen[i]=true;q.Enqueue(i);}
    while(q.Count>0){int i=q.Dequeue();foreach(int n in new[]{s.Index(i%40+1,i/40),s.Index(i%40-1,i/40),s.Index(i%40,i/40+1),s.Index(i%40,i/40-1)})if(n>=0&&!seen[n]&&s.Cells[n].River){seen[n]=true;q.Enqueue(n);}}
    foreach(var b in g.bridges)foreach(int i in b.cells)Check(s.Cells[i].River&&s.Cells[i].Bridge,"Every visible bridge cell is river and traversable");
    var reachable=new HashSet<int>();var walk=new Queue<int>();walk.Enqueue(g.spawn);reachable.Add(g.spawn);int walkable=0;
    for(int i=0;i<s.Cells.Length;i++)if(s.Walkable(i))walkable++;
    while(walk.Count>0){int i=walk.Dequeue();foreach(int n in new[]{s.Index(i%40+1,i/40),s.Index(i%40-1,i/40),s.Index(i%40,i/40+1),s.Index(i%40,i/40-1)})if(n>=0&&s.Walkable(n)&&reachable.Add(n))walk.Enqueue(n);}
    Check(reachable.Count>=walkable*.85f,"Spawn can explore the map through generated crossings: "+seed+", reached="+reachable.Count+"/"+walkable);
    for(int i=0;i<seen.Length;i++){if(s.Cells[i].Source)Check(seen[i],"Every spring reaches a drain: "+seed);if(s.Cells[i].River&&!seen[i]){lakeLayouts++;break;}}
    if(sources>1)branchLayouts++;maxRivers=Math.Max(maxRivers,rivers);signatures.Add(Signature(s));starts.Add(g.spawn);villages.Add(g.village);
    for(int p=0;p<12;p++)s.AdvancePhase();if(s.Won)earlyWins++;
   }
   Check(signatures.Count==64&&starts.Count>40&&villages.Count>40&&outlets.Count>25,"Map topology, spawn, village and river exits vary across seeds");
   Check(branchLayouts>8&&lakeLayouts>8,"Branching rivers and disconnected lakes both occur");
   Check(earlyWins<=3,"Natural early prosperity is uncommon: "+earlyWins);
   Console.WriteLine($"64 maps: layouts={signatures.Count}, spawns={starts.Count}, villages={villages.Count}, outletPositions={outlets.Count}, branched={branchLayouts}, lakes={lakeLayouts}, maxRiverCells={maxRivers}, earlyWins={earlyWins}, elapsedMs={watch.ElapsedMilliseconds}");
   if(args.Length>0&&args[0]=="--long")foreach(int seed in new[]{1,7,17,31})
   {
    var g=V1WorldGenerator.Generate(40,28,new V1Settings{initialSeason=(V1Season)(seed%4)},seed);var s=g.simulation;double initial=Water(s);int peak=0,land=0;foreach(var c in s.Cells)if(!c.Blocked&&!c.River)land++;
    for(int p=0;p<2250;p++){s.AdvancePhase();peak=Math.Max(peak,s.FloodCells);}
    var l=s.Ledger;double error=Water(s)-(initial+l.WaterInput-l.WaterGrowth-l.WaterFire-l.WaterEvaporated-l.WaterOutlet);
    Console.WriteLine($"750 days seed={seed}: maxFlood={peak}/{land}, finalFlood={s.FloodCells}, waterError={error:F3}, everProsperous={s.Won}");
    Check(peak<land*.35f&&s.FloodCells<land*.2f&&Math.Abs(error)<3,"Long-run generated river stability: "+seed);
   }
   Console.WriteLine("PASS: "+checks+" procedural-world assertions");return 0;
  }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
 }
}
