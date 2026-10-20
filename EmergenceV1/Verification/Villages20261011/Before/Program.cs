using System;
using System.Diagnostics;

public static class Program
{
    static int assertions;
    static void Check(bool condition,string message) { assertions++; if(!condition) throw new Exception(message); }
    static V1Simulation Blank(V1Settings settings=null)
    {
        var s=new V1Simulation(12,12,settings);
        for(int i=0;i<s.Cells.Length;i++)s.Cells[i]=new V1Cell{Earth=30,Water=2,Height=2,SoilBody=20};
        return s;
    }
    static void Tree(V1Simulation s,int x,int y,float wood=1) { int i=s.Index(x,y);s.Cells[i].Tree=true;s.Cells[i].Wood=wood; }
    static void Trees()
    {
        var settings=new V1Settings{phaseSeconds=.5f};var s=Blank(settings);int center=s.Index(5,5);
        Tree(s,4,4);Tree(s,6,4);Tree(s,4,6);Tree(s,6,6,10);
        s.PlaceElement(center,V1Element.Wood);s.AdvancePhase();
        Check(!s.Cells[center].Tree&&s.Cells[center].Seeds==1,"Four occupied tree cells must block birth; forest counts as one occupied cell");
        s.Cells[s.Index(6,6)].Tree=false;s.Cells[s.Index(6,6)].Wood=0;s.AdvancePhase();
        Check(s.Cells[center].Tree&&s.Cells[center].Seeds==0&&s.PlayerTreeBirths==1,"Three occupied tree cells allow exactly one new tree");
        var grass=Blank(settings);for(int i=0;i<grass.Cells.Length;i++)grass.Cells[i].Grass=2;
        grass.PlaceElement(center,V1Element.Wood);grass.AdvancePhase();Check(grass.Cells[center].Tree,"Grass cells must not count towards density");
        var noSeeds=Blank();noSeeds.AdvancePhase();Check(noSeeds.TreeBirths==0&&noSeeds.Cells[center].Grass>0,"Water and soil alone grow only grass");
        var stages=Blank();Tree(stages,5,5,2.95f);Tree(stages,7,7,7.95f);stages.Cells[center].Water=4;stages.Cells[stages.Index(7,7)].Water=4;
        stages.AdvancePhase();Check(stages.PlantStage(center)>=2&&stages.PlantStage(stages.Index(7,7))==3,"Wood biomass drives sapling/tree/forest stages");
        float w=stages.Cells[center].Wood;stages.Cells[center].Water=0;stages.Cells[center].Earth=0;stages.AdvancePhase();Check(stages.Cells[center].Wood<=w,"No water/earth must stop growth");
        var simultaneous=Blank(new V1Settings{phaseSeconds=.5f});Tree(simultaneous,4,5);Tree(simultaneous,6,5);Tree(simultaneous,4,6);
        simultaneous.PlaceElement(center,V1Element.Wood);simultaneous.PlaceElement(simultaneous.Index(5,6),V1Element.Wood);simultaneous.AdvancePhase();
        Check(simultaneous.TreeBirths==1,"Simultaneous adjacent seeds reserve density and cannot both bypass limit");
    }
    static void GoldAndFire()
    {
        var s=Blank(new V1Settings{phaseSeconds=.5f});int center=s.Index(5,5);s.Cells[center].Gold=12;s.Cells[center].Wood=0;
        s.Cells[center].Height=1;Check(s.Cells[center].Dead(8,20),"Deathland needs zero wood and a<=G<b");
        s.Cells[center].Wood=1;s.Cells[center].Tree=true;Check(!s.Cells[center].Dead(8,20),"Living tree is not deathland");
        s.Cells[center].Gold=20;s.Initialize();s.Cells[center].Gold=7;int neighbor=s.Index(6,5);s.Cells[neighbor].Height=0;
        s.AdvancePhase();Check(s.Cells[center].Mine==V1Mine.Active&&s.Cells[center].Gold==7,"Mine remains fixed below b/a and does not enrich outward");
        var e=Blank(new V1Settings{phaseSeconds=10});int low=e.Index(5,5);e.Cells[low].Height=0;
        Tree(e,5,5,14); // A living recipient can enrich; established deathland cannot.
        for(int y=3;y<=7;y++)for(int x=3;x<=7;x++)if(e.Index(x,y)!=low)e.Cells[e.Index(x,y)].Gold=3;
        double total=Gold(e);for(int p=0;p<10;p++)e.AdvancePhase();Check(Math.Abs(Gold(e)-total)<.001,"Gold enrichment conserves all gold");Check(e.Cells[low].Mine==V1Mine.Active,"Finite scattered gold enriches into a mine");
        var f=Blank(new V1Settings{phaseSeconds=.5f});f.Cells[center].Water=0;f.Cells[center].Earth=0;Tree(f,5,5,2);f.Cells[center].Fire=.01f;f.Cells[center].Gold=2;
        f.AdvancePhase();Check(f.Cells[center].Wood<2&&Math.Abs(f.Cells[center].Gold-2)<.0001,"Weak fire burns wood without damaging gold below threshold");
        var b=Blank();b.Cells[center].Gold=5;b.Cells[center].Height=1;
        b.Cells[b.Index(6,5)].Blocked=true;b.Cells[b.Index(6,4)].Blocked=true;b.Cells[b.Index(6,6)].Blocked=true;b.Cells[b.Index(7,5)].Height=0;
        int next=b.EnrichmentStep(center);Check(next!=b.Index(6,5),"Gold must not cross blocked cells");
    }
    static double Water(V1Simulation s){double n=0;foreach(var c in s.Cells)n+=c.Water;return n;}
    static double Gold(V1Simulation s){double n=0;foreach(var c in s.Cells)n+=c.Gold+c.MudGold;return n;}
    static double Pollution(V1Simulation s){double n=0;foreach(var c in s.Cells)n+=c.Pollution;return n;}
    static void Integrity(V1Simulation s)
    {
        foreach(var c in s.Cells)
        {
            foreach(float n in new[]{c.Gold,c.Wood,c.Water,c.Fire,c.Earth,c.Grass,c.Sediment,c.MudNutrient,c.MudGold,c.Pollution,c.SoilBody})Check(n>=0&&!float.IsNaN(n)&&!float.IsInfinity(n),"Finite nonnegative resources");
            Check(!c.Tree||c.Wood>0,"Tree occupancy and wood agree");
        }
    }
    static void MapAndBudgets()
    {
        var a=V1Level.Create();var b=V1Level.Create();Check(a.Villages.Count==1&&a.NearRiver(a.Villages[0].cell),"Authored initial village is riverside");
        Check(a.ReadRegion(a.Villages[0].cell).cells==36,"Village region is exactly 36 cells");
        Check(a.AddVillage(a.Villages[0].cell+1)==null,"Village center cannot enter existing region");
        var overlap=V1Level.Create();var child=overlap.AddVillage(V1Level.Cell(overlap,9,13),4);Check(child!=null,"Ranges may overlap while centers remain outside both regions");
        overlap.AddAnimal(V1Level.Cell(overlap,10,11),V1AnimalKind.Chicken,V1AnimalOrigin.Farmed,overlap.Villages[0].id);
        Check(overlap.ReadRegion(child.cell).farmed==0,"Another village cannot reuse farm animal food supply");
        double initialWater=Water(a),initialGold=Gold(a),initialPollution=Pollution(a);
        var watch=Stopwatch.StartNew();
        for(int phase=0;phase<30;phase++)
        {
            foreach(var s in new[]{a,b})
            {
                int plant=V1Level.Cell(s,7,6);
                if(phase==0)s.PlaceElement(plant,V1Element.Wood);
                if(phase%2==0){s.PlaceElement(plant,V1Element.Water);s.PlaceElement(plant,V1Element.Earth);}
                foreach(var v in s.Villages)if(v.active)
                    for(int dy=-2;dy<=3;dy++)for(int dx=-2;dx<=3;dx++)
                    {
                        int i=s.Index(v.cell%s.Width+dx,v.cell/s.Width+dy);if(!s.Valid(i)||s.IsBuilding(i)||s.Cells[i].River||s.Cells[i].Mine==V1Mine.Active)continue;
                        if(s.Cells[i].Earth<6)s.PlaceElement(i,V1Element.Earth);
                        if(s.Cells[i].Water<1)s.PlaceElement(i,V1Element.Water,1);
                        if(!s.Cells[i].Tree&&s.TreeCells3x3(i)<4)s.PlaceElement(i,V1Element.Wood);
                    }
                s.AdvancePhase();
            }
            Integrity(a);
            Check(a.Day==1+a.AdvanceCount/3&&a.Phase==a.AdvanceCount%3,"Manual morning/noon/evening cycle");
            for(int i=0;i<a.Cells.Length;i++)Check(a.Cells[i].Water==b.Cells[i].Water&&a.Cells[i].Gold==b.Cells[i].Gold&&a.Cells[i].Wood==b.Cells[i].Wood,"Deterministic command sequence");
            var l=a.Ledger;
            double waterExpected=initialWater+l.WaterInput-l.WaterGrowth-l.WaterFire-l.WaterEvaporated-l.WaterOutlet;
            Check(Math.Abs(Water(a)-waterExpected)<.08,"Water source/consumption/outlet ledger: "+(Water(a)-waterExpected));
            Check(Math.Abs(Gold(a)-(initialGold+l.GoldInput-l.GoldMined-l.GoldBurned-l.GoldOutlet))<.02,"Gold mining/erosion/enrichment ledger");
            Check(Math.Abs(Pollution(a)-(initialPollution+l.PollutionMade-l.PollutionCleaned-l.PollutionOutlet))<.02,"Pollution transport/cleanup ledger");
            if(phase%5==0)Console.WriteLine($"phase {a.AdvanceCount}: pop={a.TotalPopulation}, prosperity={a.Prosperity:F1}, trees={a.TreeBirths}, farms={a.FarmBirths}, wildMoves={a.WildlifeMoves}, won={a.Won}");
        }
        watch.Stop();Console.WriteLine("30 supported phases: "+watch.ElapsedMilliseconds+"ms");
        Check(a.PlayerTreeBirths>0&&a.MatureTransitions>0&&a.ForestTransitions>0,"Player planted trees mature and form forests");
        Check(a.Ledger.GoldMined>0&&a.Ledger.TimberHarvested>0,"Village obtains materials by mining and tree harvesting");
        Check(a.WildlifeMoves>0&&a.FarmBirths>0,"Wild migration and farm breeding");
        Check(a.Won,"Authored map reaches prosperity through finite, legal player interventions");
        Check(a.TutorialStage==6,"All tutorial result milestones complete");
    }
    static void Hydrology()
    {
        var s=Blank(new V1Settings{phaseSeconds=.5f});int a=s.Index(5,5),b=s.Index(6,5);s.Cells[a].Water=5;s.Cells[b].Water=0;s.Cells[a].Pollution=10;s.Cells[a].Sediment=2;
        s.AdvancePhase();Check(s.Cells[b].Water>0&&s.Cells[b].Pollution>0&&s.Cells[b].Sediment>0,"Water carries pollution and sediment to lower head");
        var d=Blank(new V1Settings{phaseSeconds=10});int c=d.Index(5,5);d.Cells[c].Sediment=6;d.Cells[c].MudNutrient=2;float h=d.Cells[c].Height;
        d.AdvancePhase();Check(d.Cells[c].Height>h&&d.Cells[c].Deposited>0,"Slow flow deposits sediment and raises terrain");
        var noOre=V1Level.Create();for(int i=0;i<noOre.Cells.Length;i++){noOre.Cells[i].Gold=0;noOre.Cells[i].MudGold=0;noOre.Cells[i].Mine=V1Mine.None;}
        for(int p=0;p<60;p++)noOre.AdvancePhase();Check(noOre.Villages[0].toolHealth<.2f||!noOre.Villages[0].active,"No ore supply exhausts reserves and damages village production");
        var open=Channel(false);var clogged=Channel(true);int bank=open.Index(5,4),bed=open.Index(6,5);float bedStart=clogged.Cells[bed].Height;
        for(int p=0;p<30;p++){open.AdvancePhase();clogged.AdvancePhase();}
        Console.WriteLine($"No-rain channel: open bank={open.Cells[bank].Water:F2}, clogged bank={clogged.Cells[bank].Water:F2}, deposited={clogged.Cells[bed].Deposited:F2}");
        Check(clogged.Cells[bed].Height>bedStart&&clogged.Cells[bed].Deposited>0,"Sediment raises downstream channel bed without rain");
        Check(open.Cells[bank].Water<4&&clogged.Cells[bank].Water>4,"Continuous upstream input plus deposited bed causes genuine no-rain flooding");
        var fireTown=V1Level.Create(new V1Settings{phaseSeconds=.5f});var town=fireTown.Villages[0];int burn=town.cell;fireTown.Cells[burn].Water=0;fireTown.Cells[burn].Fire=2;float timber=town.timber,housing=town.housing;
        fireTown.AdvancePhase();Check(town.timber<timber&&town.housing<housing,"Fire consumes village timber and damages housing");
    }
    static V1Simulation Channel(bool sediment)
    {
        var s=Blank(new V1Settings{sourceRate=1.05f});for(int i=0;i<s.Cells.Length;i++)s.Cells[i]=new V1Cell{Blocked=true};
        s.Cells[s.Index(5,5)]=new V1Cell{Source=true,River=true,Height=0,SoilBody=30};
        s.Cells[s.Index(6,5)]=new V1Cell{Outlet=true,River=true,Height=0,SoilBody=30,Water=20,Sediment=sediment?24:0};
        s.Cells[s.Index(5,4)]=new V1Cell{Height=1.2f,SoilBody=30};return s;
    }
    static void AcceptanceRegressions()
    {
        var s=Blank();int dead=s.Index(5,5),near=s.Index(6,5),low=s.Index(5,4);
        s.Cells[dead].Gold=12;s.Cells[dead].Grass=1.5f;s.Cells[dead].Height=0;
        s.Cells[low].Height=-1;s.Cells[near].Gold=7;s.Initialize();double total=Gold(s);
        Check(s.EnrichmentStep(dead)==dead&&s.EnrichmentStep(near)!=dead,"Deathland neither exports gold nor receives enrichment paths");
        float nutrients=s.Cells[near].Earth;s.AdvancePhase();
        Check(s.Cells[dead].Mine==V1Mine.None&&s.Cells[dead].Dead(8,20),"Deathland stays deathland during automatic enrichment: gold="+s.Cells[dead].Gold+", mine="+s.Cells[dead].Mine+", locked="+s.Cells[dead].DeadLand);
        Check(s.Cells[near].Pollution>.1f&&s.Cells[near].Earth<nutrients,"Deathland affects adjacent pollution and nutrients despite grass cleanup");
        Check(Math.Abs(Gold(s)-total)<.001,"Deathland exclusion preserves gold budget");
        s.Cells[dead].Gold=25;s.AdvancePhase();Check(s.Cells[dead].Dead(8,20)&&s.Cells[dead].Mine==V1Mine.None,"Existing deathland cannot form a mine from transported gold");
        s.PlaceElement(dead,V1Element.Gold,1);Check(s.Cells[dead].Mine==V1Mine.Active,"Explicit gold placement may deliberately create a mine");
        var f=Blank();for(int i=0;i<f.Cells.Length;i++){f.Cells[i].Water=0;f.Cells[i].Earth=0;}
        for(int x=4;x<=6;x++)Tree(f,x,5,10);f.Cells[f.Index(5,5)].Fire=50;
        f.AdvancePhase();Check(f.Cells[f.Index(4,5)].Wood<9&&f.Cells[f.Index(6,5)].Wood<9&&f.FireSpreads>=2,"Fire 50 in middle forest spreads to both forests after burning out its own fuel");
        var humid=Blank();for(int i=0;i<humid.Cells.Length;i++)humid.Cells[i].Earth=0;for(int x=4;x<=6;x++)Tree(humid,x,5,10);humid.Cells[humid.Index(5,5)].Fire=50;
        humid.AdvancePhase();Check(humid.Cells[humid.Index(4,5)].Wood<9.9f&&humid.Cells[humid.Index(6,5)].Wood<9.9f,"Strong heat ignites normal moist land, rather than demanding completely dry neighbors: wood="+humid.Cells[humid.Index(4,5)].Wood+", spreads="+humid.FireSpreads);
        var barrier=Blank();for(int i=0;i<barrier.Cells.Length;i++){barrier.Cells[i].Water=0;barrier.Cells[i].Earth=0;}
        Tree(barrier,4,5,10);Tree(barrier,6,5,10);barrier.Cells[barrier.Index(4,5)].Fire=50;barrier.Cells[barrier.Index(5,5)].River=true;barrier.Cells[barrier.Index(5,5)].Water=8;
        barrier.AdvancePhase();Check(barrier.Cells[barrier.Index(6,5)].Fire==0&&barrier.Cells[barrier.Index(6,5)].Wood>=9,"Water and fuel gaps stop fire; fire cannot jump a river");
        var landing=Blank();int cell=landing.Index(5,5);landing.Cells[cell].Water=30;
        landing.Cells[landing.Index(4,5)].Blocked=true;Tree(landing,6,5,10);landing.Cells[landing.Index(5,4)].Fire=2;
        Check(landing.NearestSafeLand(5.5f,5.5f)==landing.Index(5,6),"Nearest safe landing excludes water, rocks, trunks and fire");
        landing.Cells[cell].Bridge=true;Check(!landing.BridgeUsable(cell)&&!landing.Walkable(cell),"Overtopped bridge is impassable");
        landing.Cells[cell].Water=8;Check(landing.BridgeUsable(cell)&&landing.Walkable(cell),"Ordinary bridge remains walkable");
        landing.Cells[cell].Bridge=false;landing.Cells[cell].River=true;landing.Cells[cell].Water=1;Check(!landing.Walkable(cell),"Shallow visible river is also blocked");
        string[] routes=new string[3];uint[] seeds={101u,202u,303u};
        for(int run=0;run<3;run++)
        {
            var animals=new V1Simulation(12,12,new V1Settings{phaseSeconds=.5f},seeds[run]);
            for(int i=0;i<animals.Cells.Length;i++)animals.Cells[i]=new V1Cell{Earth=30,Grass=2,Water=1,Height=2};
            var a=animals.AddAnimal(animals.Index(5,5),V1AnimalKind.Cow,V1AnimalOrigin.Wild);
            for(int phase=0;phase<12;phase++){int before=a.cell;animals.AdvancePhase();Check(animals.Walkable(a.cell)&&Math.Abs(before%12-a.cell%12)+Math.Abs(before/12-a.cell/12)<=1,"Animal takes safe neighboring steps");routes[run]+=a.cell+",";}
        }
        Check(routes[0]!=routes[1]&&routes[1]!=routes[2],"Different session seeds vary animal routes");
        var weak=Blank();for(int i=0;i<weak.Cells.Length;i++)weak.Cells[i]=new V1Cell{Wood=6,Tree=true,Height=2};
        int burn=weak.Index(5,5);weak.Cells[burn].Fire=1;
        for(int phase=0;phase<5;phase++)weak.AdvancePhase();
        int damaged=0;for(int i=0;i<weak.Cells.Length;i++)if(weak.Cells[i].Wood<5.9f)damaged++;
        Check(damaged==1&&weak.FireSpreads==0&&weak.Cells[burn].Fire==0,"Fire 1 in wood 6 burns locally and dies, even in a dry continuous forest");
        var strong=Blank();for(int i=0;i<strong.Cells.Length;i++)strong.Cells[i]=new V1Cell{Wood=10,Tree=true,Height=2};strong.Cells[burn].Fire=50;
        for(int phase=0;phase<5;phase++)strong.AdvancePhase();
        damaged=0;int radius=0;for(int i=0;i<strong.Cells.Length;i++)if(strong.Cells[i].Wood<9.9f){damaged++;radius=Math.Max(radius,Math.Abs(i%12-5)+Math.Abs(i/12-5));}
        Check(damaged>1&&damaged<=5&&radius==1,"A strong single impulse spreads to adjacent trees but loses intensity instead of burning half a dense map: "+damaged+" cells, radius="+radius);
        Console.WriteLine("Fire balance: wood 6 / fire 1 = 1 affected cell; wood 10 / fire 50 = "+damaged+" cells, radius "+radius);
        Console.WriteLine("PASS: acceptance regressions (deathland, heat spread/barriers, flooding/landing, animal routes)");
    }
    public static int Main()
    {
        try { Trees();GoldAndFire();AcceptanceRegressions();Hydrology();MapAndBudgets();Console.WriteLine("PASS: "+assertions+" assertions");return 0; }
        catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
    }
}
