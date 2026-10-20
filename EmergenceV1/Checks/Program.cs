using System;
using System.Diagnostics;

public static class Program
{
    static int assertions;
    static void Check(bool condition,string message) { assertions++; if(!condition) throw new Exception(message); }
    static V1Simulation Blank(V1Settings settings=null)
    {
        settings=settings??new V1Settings();settings.seasonalEvents=false;settings.dryIgnition=false;settings.evaporationRate=.003f;
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
    static V1Simulation Level(V1Settings settings=null)
    {settings=settings??new V1Settings();settings.seasonalEvents=false;settings.dryIgnition=false;settings.evaporationRate=.003f;return V1Level.Create(settings);}
    static double Water(V1Simulation s){double n=0;foreach(var c in s.Cells)n+=c.Water+c.Snow;return n;}
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
        var a=Level();var b=Level();Check(a.Villages.Count==1&&a.NearRiver(a.Villages[0].cell),"Authored initial village is riverside");
        Check(a.ReadRegion(a.Villages[0].cell).cells==36,"Village region is exactly 36 cells");
        Check(a.AddVillage(a.Villages[0].cell+1)==null,"Village center cannot enter existing region");
        var overlap=Level();var child=overlap.AddVillage(V1Level.Cell(overlap,9,13),4);Check(child!=null,"Ranges may overlap while centers remain outside both regions");
        overlap.AddAnimal(V1Level.Cell(overlap,10,11),V1AnimalKind.Chicken,V1AnimalOrigin.Farmed,overlap.Villages[0].id);
        Check(overlap.ReadRegion(child.cell).farmed==0,"Another village cannot reuse farm animal food supply");
        double initialWater=Water(a),initialGold=Gold(a),initialPollution=Pollution(a);
        var watch=Stopwatch.StartNew();
        for(int phase=0;phase<30;phase++)
        {
            foreach(var s in new[]{a,b})
            {
                int plant=V1Level.Cell(s,7,6);
                // Prosperity now needs restoration beyond a single village's resource area.
                for(int y=1;y<s.Height-1;y+=2)for(int x=1;x<s.Width-1;x+=2)
                {
                    int forest=s.Index(x,y);var c=s.Cells[forest];
                    if(!s.Valid(forest)||c.River||s.IsBuilding(forest)||c.Gold>=s.Settings.deadGold||s.IsWet(forest))continue;
                    if(c.Earth<6)s.PlaceElement(forest,V1Element.Earth);
                    if(c.Water<2)s.PlaceElement(forest,V1Element.Water,1);
                    if(!c.Tree&&s.TreeCells3x3(forest)<4)s.PlaceElement(forest,V1Element.Wood);
                }
                if(phase==0)s.PlaceElement(plant,V1Element.Wood);
                if(phase%2==0){s.PlaceElement(plant,V1Element.Water);s.PlaceElement(plant,V1Element.Earth);}
                foreach(var v in s.Villages)if(v.active)
                    for(int dy=V1Simulation.RegionMin(v.tier);dy<=V1Simulation.RegionMax(v.tier);dy++)for(int dx=V1Simulation.RegionMin(v.tier);dx<=V1Simulation.RegionMax(v.tier);dx++)
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
        Check(a.TutorialComplete&&a.TutorialStage==3,"Tutorial completes once the player's sapling becomes a tree");
    }
    static void Hydrology()
    {
        var s=Blank(new V1Settings{phaseSeconds=.5f});int a=s.Index(5,5),b=s.Index(6,5);s.Cells[a].Water=5;s.Cells[b].Water=0;s.Cells[a].Pollution=10;s.Cells[a].Sediment=2;
        s.AdvancePhase();Check(s.Cells[b].Water>0&&s.Cells[b].Pollution>0&&s.Cells[b].Sediment>0,"Water carries pollution and sediment to lower head");
        var d=Blank(new V1Settings{phaseSeconds=10});int c=d.Index(5,5);d.Cells[c].Sediment=6;d.Cells[c].MudNutrient=2;float h=d.Cells[c].Height;
        d.AdvancePhase();Check(d.Cells[c].Height>h&&d.Cells[c].Deposited>0,"Slow flow deposits sediment and raises terrain");
        var noOre=Level();for(int i=0;i<noOre.Cells.Length;i++){noOre.Cells[i].Gold=0;noOre.Cells[i].MudGold=0;noOre.Cells[i].Mine=V1Mine.None;}
        for(int p=0;p<60;p++)noOre.AdvancePhase();Check(noOre.Villages[0].toolHealth<.2f||!noOre.Villages[0].active,"No ore supply exhausts reserves and damages village production");
        var open=Channel(false);var clogged=Channel(true);int bank=open.Index(5,4),bed=open.Index(6,5);float bedStart=clogged.Cells[bed].Height;
        for(int p=0;p<300;p++){open.AdvancePhase();clogged.AdvancePhase();}
        Console.WriteLine($"No-rain channel: open bank={open.Cells[bank].Water:F2}, clogged bank={clogged.Cells[bank].Water:F2}, deposited={clogged.Cells[bed].Deposited:F2}");
        Check(clogged.Cells[bed].Height>bedStart&&clogged.Cells[bed].Deposited>0,"Sediment raises downstream channel bed without rain");
        Check(open.Cells[bank].Water<4&&clogged.Cells[bank].Water>4,"Continuous upstream input plus deposited bed causes genuine no-rain flooding");
        var fireTown=Level(new V1Settings{phaseSeconds=.5f});var town=fireTown.Villages[0];int burn=town.cell;fireTown.Cells[burn].Water=0;fireTown.Cells[burn].Fire=2;float timber=town.timber,housing=town.housing;
        fireTown.AdvancePhase();Check(town.timber<timber&&town.housing<housing,"Fire consumes village timber and damages housing");
    }
    static V1Simulation Channel(bool sediment)
    {
        var s=Blank(new V1Settings{sourceRate=1.05f});for(int i=0;i<s.Cells.Length;i++)s.Cells[i]=new V1Cell{Blocked=true};
        s.Cells[s.Index(5,5)]=new V1Cell{Source=true,River=true,Height=0,SoilBody=30};
        s.Cells[s.Index(6,5)]=new V1Cell{Outlet=true,River=true,Height=0,SoilBody=30,Water=20,Sediment=sediment?240:0};
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
    static V1Simulation VillageSandbox()
    {
        var s=new V1Simulation(24,24,new V1Settings{phaseSeconds=.5f});
        for(int i=0;i<s.Cells.Length;i++)s.Cells[i]=new V1Cell{Earth=30,Grass=2,Water=1,Height=2,SoilBody=20};
        for(int y=0;y<s.Height;y++)s.Cells[s.Index(12,y)].River=true;
        s.Initialize();return s;
    }
    static void VillageRegionsAndContinuity()
    {
        for(int tier=1;tier<=3;tier++)
        {
            var s=VillageSandbox();var v=s.AddVillage(s.Index(9,10),tier==1?8:tier==2?18:28);
            Check(v!=null&&v.tier==tier,"New village starts at the tier corresponding to its population");
            int size=tier+5,min=-(size-1)/2,max=min+size-1,count=0;
            for(int i=0;i<s.Cells.Length;i++)if(s.InRegion(v.cell,i))count++;
            Check(count==size*size&&s.ReadRegion(v.cell).cells==count,"Tier "+tier+" reads exactly "+size*size+" cells");
            int included=s.Index(9+min,10),excluded=s.Index(9+max+1,10);
            s.Cells[included].Pollution=10;s.Cells[excluded].Pollution=50;
            Tree(s,9+min,10,10);Tree(s,9+max+1,10,20);
            var r=s.ReadRegion(v.cell);
            Check(Math.Abs(r.pollution-10f/(size*size))<.00001&&r.wood==10,"Pollution denominator and wood total use only tier bounds");
            Check(s.AddVillage(v.cell)==null&&s.AddVillage(included)==null,"New villages cannot overwrite the existing center or tier region");
            v.housing=36;v.timber=1;v.metal=10;s.Cells[included].Pollution=0;s.Cells[included].Earth=0;s.Cells[excluded].Earth=0;
            s.Cells[included].Grass=0;s.Cells[excluded].Grass=0;
            s.AdvancePhase();
            Check(s.Ledger.TimberHarvested>0&&s.Cells[included].Wood<10&&s.Cells[excluded].Wood==20,"Harvesting finds the outer tier boundary and excludes the next cell: tier="+tier+", harvested="+s.Ledger.TimberHarvested+", included="+s.Cells[included].Wood+", excluded="+s.Cells[excluded].Wood);
            Check(v.region.cells==s.ReadRegion(v.cell).cells,"Cached region refreshes after tier changes");
        }
        var untouched=Level();var original=untouched.Villages[0];int originalId=original.id;
        for(int p=0;p<18;p++)
        {
            untouched.AdvancePhase();
            Check(untouched.VillageById(originalId)==original,"Initial village identity survives through day six without intervention");
            foreach(var a in untouched.Villages)foreach(var b in untouched.Villages)
                if(a.active&&b.active&&a!=b)Check(!untouched.InRegion(a.cell,b.cell)&&!untouched.InRegion(b.cell,a.cell),"Active village centers cannot overlap any tier region");
        }
        Console.WriteLine($"Day-six continuity: original village #{original.id}, population={original.population}, tier={original.tier}, migrations={original.migrations}; {original.condition}");
        // Force a material shortage in a medium village with a safe destination.
        var migration=VillageSandbox();var migrating=migration.AddVillage(migration.Index(9,10),22);int before=migrating.cell;
        migrating.housing=36;migrating.timber=0;migrating.metal=10;migrating.badPhases=3;
        migration.AdvancePhase();
        Check(migrating.cell!=before&&migrating.migrations==1&&migration.Villages.Count==1&&migration.VillageById(migrating.id)==migrating,"Migration moves the same village instead of generating a replacement");
        Check(migrating.tier==2&&migrating.region.cells==49&&migrating.condition.Contains("已迁移"),"Migration preserves a medium village and refreshes its 49-cell readout");
        // Split a large village at the tier threshold; parent population and tier must agree immediately.
        var split=VillageSandbox();var parent=split.AddVillage(split.Index(9,10),28);parent.housing=36;parent.timber=1;parent.metal=10;
        for(int p=0;p<3;p++)split.AdvancePhase();
        parent.population=28;parent.timber=10;split.AdvancePhase();
        Check(split.NewVillages>0&&split.VillageById(parent.id)==parent,"Splitting creates a new id and retains the parent village");
        Check(parent.tier==split.BuildingTier(parent.population)&&parent.region.cells==49,"Split parent updates its tier and region in the same phase");
        Console.WriteLine("PASS: village 36/49/64-cell regions, harvesting boundaries, day-six identity, migration tier and split continuity");
    }
    static void TerrainAndEcology()
    {
        var a=Level();var b=Level();var different=Level(new V1Settings{terrainSeed=911});
        int[] stages=new int[3];int varied=0;
        for(int i=0;i<a.Cells.Length;i++)if(a.Valid(i)&&!a.Cells[i].River)
        {
            stages[V1Terrain.Stage(a.Cells[i].Height,a.Settings)]++;
            Check(a.Cells[i].Height==b.Cells[i].Height,"Same seed generates identical Perlin terrain");
            if(a.Cells[i].Height!=different.Cells[i].Height)varied++;
        }
        Check(stages[0]>0&&stages[1]>0&&stages[2]>0&&varied>100,"Perlin terrain produces plains, slopes and mountains; changing seed changes elevations: "+string.Join(",",stages));
        Check(a.Cells[V1Level.Cell(a,9,8)].Height==2.8f&&a.Cells[V1Level.Cell(a,5,16)].Height==1.6f,"Noise keeps tutorial settlement and authored basin intact");
        Console.WriteLine("Perlin terrain cells: plain="+stages[0]+", slope="+stages[1]+", mountain="+stages[2]);
        foreach(float amount in new[]{4f,8f})
        {
            var fire=Blank();int cell=fire.Index(5,5);
            for(int i=0;i<fire.Cells.Length;i++)fire.Cells[i].Earth=0;
            Tree(fire,5,5,8);fire.Cells[cell].Water=4;
            fire.PlaceElement(cell,V1Element.Fire,amount);
            for(int p=0;p<3;p++)fire.AdvancePhase();
            Check(!fire.Cells[cell].Tree&&fire.Cells[cell].Wood<.01f,"A normal fire tool deposit burns a moist forest in three phases: amount="+amount+", wood="+fire.Cells[cell].Wood);
        }
        var spread=Blank();for(int i=0;i<spread.Cells.Length;i++)spread.Cells[i].Earth=0;
        for(int x=4;x<=6;x++)Tree(spread,x,5,8);
        spread.PlaceElement(spread.Index(5,5),V1Element.Fire,8);spread.AdvancePhase();
        Check(spread.FireSpreads>=2&&spread.Cells[spread.Index(4,5)].Wood<8&&spread.Cells[spread.Index(6,5)].Wood<8,"Ordinary tool fire can accumulate ignition heat on moist adjacent trees");
        var ore=Blank();int mine=ore.Index(5,5);ore.Cells[mine].Gold=30;ore.Initialize();ore.Cells[mine].Water=30;
        for(int p=0;p<3;p++)ore.AdvancePhase();
        Check(ore.Cells[mine].Gold==30&&ore.Cells[mine].Mine==V1Mine.Active,"Flooding preserves fixed mine gold and status");
        // Compare the same river bank, with and without rooted forest cover.
        var bare=Blank(new V1Settings{phaseSeconds=.5f});var rooted=Blank(new V1Settings{phaseSeconds=.5f});
        foreach(var s in new[]{bare,rooted})
        {
            for(int i=0;i<s.Cells.Length;i++)s.Cells[i]=new V1Cell{Blocked=true};
            s.Cells[s.Index(5,5)]=new V1Cell{River=true,Height=1,Water=12};
            s.Cells[s.Index(6,5)]=new V1Cell{Height=2,Water=0,Earth=0};
        }
        Tree(rooted,6,5,10);bare.AdvancePhase();rooted.AdvancePhase();
        Check(rooted.Cells[rooted.Index(6,5)].Water<bare.Cells[bare.Index(6,5)].Water,"Rooted river bank resists overbank flow");
        double initialWater=Water(a),initialGold=Gold(a);int maxFlood=0,land=stages[0]+stages[1]+stages[2];
        for(int p=0;p<2250;p++)
        {
            a.AdvancePhase();maxFlood=Math.Max(maxFlood,a.FloodCells);
            if(p%150==0)foreach(var c in a.Cells)Check(!float.IsNaN(c.Height)&&!float.IsInfinity(c.Water)&&c.Water>=0&&c.Gold>=0,"Long-run terrain and resources remain finite");
        }
        var ledger=a.Ledger;
        double waterError=Water(a)-(initialWater+ledger.WaterInput-ledger.WaterGrowth-ledger.WaterFire-ledger.WaterEvaporated-ledger.WaterOutlet);
        double goldError=Gold(a)-(initialGold+ledger.GoldInput-ledger.GoldMined-ledger.GoldBurned-ledger.GoldOutlet);
        Check(maxFlood<land*.15f&&a.FloodCells<land*.1f,"750 days of untouched river flow retain dry land; max flooded="+maxFlood+", final="+a.FloodCells);
        Check(Math.Abs(waterError)<1&&Math.Abs(goldError)<.01,"750-day water and gold budgets close: water="+waterError+", gold="+goldError);
        Console.WriteLine($"750-day run: max flooded={maxFlood}/{land}, final={a.FloodCells}, water={Water(a):F2}, gold={Gold(a):F4}, waterError={waterError:F4}, goldError={goldError:F5}");
        Console.WriteLine("PASS: Perlin terrain, moist forest destruction/ignition, rooted banks, flooded mine and 750-day water/gold budgets");
    }
    static void SeedsAndSpringBirds()
    {
        var s=Blank(new V1Settings{phaseSeconds=.5f}); int cell=s.Index(5,5);
        float earth=s.Cells[cell].Earth;
        for(int i=0;i<7;i++)s.PlaceElement(cell,V1Element.Wood);
        Check(s.Cells[cell].Seeds==2&&s.Cells[cell].Earth==earth+2.5f&&s.SeedsComposted==5,"Repeated player sowing retains two seeds and composts each excess seed into 0.5 earth immediately");
        Check(s.Cells[cell].PlayerSeed,"Retained player seeds keep tutorial ownership");
        s.Cells[cell].Seeds=7;earth=s.Cells[cell].Earth;s.Initialize();
        Check(s.Cells[cell].Seeds==2&&s.Cells[cell].Earth==earth+2.5f,"Initial/imported stocks use the same cap and conversion");
        int capped=s.SeedsComposted;s.Initialize();Check(s.SeedsComposted==capped,"Repeated normalization does not compost the same seeds twice");
        s.Cells[cell].Seeds=9;s.Cells[cell].Water=0;s.Cells[cell].Earth=0;s.AdvancePhase();
        Check(s.Cells[cell].Seeds<=2&&s.SeedsComposted==capped+7,"Phase start normalizes externally edited stocks before growth");
        var natural=Blank(new V1Settings{phaseSeconds=.5f,treeGrowth=0,grassGrowth=0});
        for(int i=0;i<natural.Cells.Length;i++){natural.Cells[i].Seeds=2;natural.Cells[i].Earth=0;}
        Tree(natural,5,5,5);natural.Cells[cell].TreeAge=34.75f;natural.AdvancePhase();
        Check(natural.SeedsComposted==1,"Mature tree dispersal uses the shared two-seed limit");
        var birds=Blank(new V1Settings{phaseSeconds=.01f,treeGrowth=0,grassGrowth=0,springBirdMigrationChance=1,initialSeason=V1Season.Spring});
        for(int i=0;i<birds.Cells.Length;i++){birds.Cells[i].Seeds=2;birds.Cells[i].Earth=0;birds.Cells[i].Water=0;}
        for(int phase=0;phase<1000;phase++)
        {
            birds.AdvancePhase();Check(birds.BirdLandings.Count>=4&&birds.BirdLandings.Count<=7,"A spring event has four to seven independent landings");
            foreach(var landing in birds.BirdLandings)Check(birds.BirdCanLand(landing.cell),"Bird seed outcome belongs to its safe landing cell");
        }
        float totalEarth=0;foreach(var c in birds.Cells){totalEarth+=c.Earth;Check(c.Seeds==2,"Bird seeds never overflow cell capacity");}
        Check(birds.BirdSeeds==birds.SeedsComposted&&totalEarth==birds.BirdSeeds*.5f,"Seeds delivered to full cells convert into precisely 0.5 earth each");
        float deliveryRate=birds.BirdSeeds/5500f;Check(deliveryRate>.26f&&deliveryRate<.34f,"Independent per-bird delivery rate is approximately 0.3: "+deliveryRate);
        foreach(var season in new[]{V1Season.Summer,V1Season.Autumn,V1Season.Winter})
        {birds.Season=season;int before=birds.BirdMigrations;birds.AdvancePhase();Check(birds.BirdLandings.Count==0&&birds.BirdMigrations==before,"Bird migration is spring-only: "+season);}
        birds.Season=V1Season.Spring;birds.Settings.springBirdMigrationChance=0;birds.AdvancePhase();Check(birds.BirdLandings.Count==0,"Zero migration chance disables events");
        birds.Settings.springBirdMigrationChance=1;for(int i=0;i<birds.Cells.Length;i++)birds.Cells[i].Blocked=true;
        birds.AdvancePhase();Check(birds.BirdLandings.Count==0,"No safe ground means no invalid landing or seed input");
        var probability=Blank(new V1Settings{phaseSeconds=.01f,treeGrowth=0,grassGrowth=0,initialSeason=V1Season.Spring});
        for(int p=0;p<1000;p++)probability.AdvancePhase();
        Check(probability.BirdMigrations>75&&probability.BirdMigrations<170,"Default migration is probabilistic, about 12 percent per phase: "+probability.BirdMigrations);
        Console.WriteLine("PASS: two-seed capacity, 0.5-earth compost, player/natural/bird inputs, spring gating and 30% per-bird delivery");
    }
    static void PhaseEventsAndCleanup()
    {
        var rain=Blank(new V1Settings{phaseSeconds=.5f,waterFlow=0,treeGrowth=0,grassGrowth=0});
        rain.Cells[0].Blocked=true;rain.QueueRain(rain.Index(5,5));Check(rain.AdvanceCount==0&&rain.Ledger.WaterInput==0,"Scheduling whole-map rain does not advance time or add water");
        rain.AdvancePhase();
        for(int i=1;i<rain.Cells.Length;i++)Check(rain.Cells[i].Water>2,"Rain reaches every unblocked cell, including distant corners");
        Check(Math.Abs(rain.Ledger.WaterInput-143*rain.Settings.rainWater)<.0001&&rain.Cells[0].Water==2,"Whole-map rain books exact input and skips blocked cells");
        var snow=Blank(new V1Settings{phaseSeconds=.5f,waterFlow=0,treeGrowth=0,grassGrowth=0});snow.Season=V1Season.Winter;
        double initial=Water(snow);Check(snow.QueueEvent(V1PhaseEvent.Snow),"Winter accepts a snow event");snow.AdvancePhase();
        Check(snow.Cells[50].Snow>0&&snow.Cells[50].Water<2.01,"Snow is stored and melts gradually instead of becoming an instant flood");
        double frozen=snow.Cells[50].Snow;snow.Season=V1Season.Spring;snow.QueueEvent(V1PhaseEvent.None);snow.AdvancePhase();
        Check(snow.Cells[50].Snow<frozen,"Snow melts when the season warms");
        Check(Math.Abs(Water(snow)-(initial+snow.Ledger.WaterInput-snow.Ledger.WaterEvaporated))<.001,"Snowfall and melting preserve the water ledger");
        foreach(var season in new[]{V1Season.Spring,V1Season.Summer,V1Season.Autumn,V1Season.Winter})
        {
            var events=Blank(new V1Settings{phaseSeconds=.01f});events.Season=season;events.Settings.seasonalEvents=true;
            Check(events.QueueEvent(V1PhaseEvent.Thunderstorm)==(season==V1Season.Spring),"Spring-only thunderstorm request");
            Check(events.QueueEvent(V1PhaseEvent.Drought)==(season==V1Season.Summer),"Summer-only drought request");
            Check(events.QueueEvent(V1PhaseEvent.Snow)==(season==V1Season.Winter),"Winter-only snow request");
            for(int p=0;p<240;p++){events.AdvancePhase();Check(events.EventAllowed(events.CurrentEvent),"Random events obey season restrictions");}
        }
        var dry=Blank(new V1Settings{phaseSeconds=.01f});dry.Season=V1Season.Summer;int cell=dry.Index(5,5);
        dry.Cells[cell].Wood=6;dry.Cells[cell].Tree=true;dry.Cells[cell].Water=0;dry.Cells[cell].DryPhases=20;
        float ordinary=dry.DryFireRisk(cell);dry.QueueEvent(V1PhaseEvent.Drought);dry.AdvancePhase();
        Check(dry.DryFireRisk(cell)>ordinary,"Drought raises ignition risk on persistently dry fuel");
        dry.Cells[cell].Water=1;Check(dry.DryFireRisk(cell)==0,"Moist fuel has no drought ignition risk");
        dry.Cells[cell].Water=0;dry.Cells[cell].Snow=.2f;Check(dry.DryFireRisk(cell)==0,"Snow suppresses dry ignition");
        dry.Cells[cell].Snow=0;dry.Cells[cell].DryPhases=0;Check(dry.DryFireRisk(cell)==0,"A newly dry cell does not immediately ignite");
        var ignition=Blank(new V1Settings{phaseSeconds=.01f,treeGrowth=0,grassGrowth=0});ignition.Settings.dryIgnition=true;
        for(int i=0;i<ignition.Cells.Length;i++){ignition.Cells[i].Water=0;ignition.Cells[i].Earth=0;ignition.Cells[i].Wood=6;ignition.Cells[i].Tree=true;ignition.Cells[i].DryPhases=40;}
        for(int p=0;p<500;p++){int before=ignition.NaturalFires;ignition.AdvancePhase();Check(ignition.NaturalFires-before<=1,"Natural fires are limited to one new ignition per map and phase");}
        Check(ignition.NaturalFires>0,"Prolonged dry fuel can actually ignite");
        var bare=Blank();for(int i=0;i<bare.Cells.Length;i++)bare.Cells[i]=new V1Cell{Blocked=true};
        bare.Cells[cell]=new V1Cell{Pollution=60,Height=2,SoilBody=20};bare.PlaceElement(cell,V1Element.Fire,4);bare.AdvancePhase();
        Check(bare.Cells[cell].Pollution<40&&bare.Ledger.PollutionCleaned>20,"Fire directly cleans bare polluted ground without grass or wood: remaining="+bare.Cells[cell].Pollution);
        Check(Math.Abs(Pollution(bare)-(60+bare.Ledger.PollutionMade-bare.Ledger.PollutionCleaned-bare.Ledger.PollutionOutlet))<.001,"Direct fire cleanup closes the pollution ledger");
        bare.Cells[cell].Pollution=60;bare.Cells[cell].Gold=12;bare.Initialize();bare.PlaceElement(cell,V1Element.Fire,8);bare.AdvancePhase();
        Check(bare.Cells[cell].Pollution<40&&bare.Ledger.PollutionMade>0,"Fire reduces deathland pollution while its gold source continues emitting");
        Console.WriteLine("PASS: whole-map rain, seasonal events, snow/melt budgets, drought ignition limits and bare-ground fire cleanup");
    }
    static void SeasonalLongRuns()
    {
        foreach(var season in new[]{V1Season.Spring,V1Season.Summer,V1Season.Autumn,V1Season.Winter})
        {
            var s=V1Level.Create(new V1Settings{initialSeason=season});double initial=Water(s),gold=Gold(s),pollution=Pollution(s);int max=0,land=0;float peakProsperity=0;
            foreach(var c in s.Cells)if(!c.Blocked&&!c.River)land++;
            for(int p=0;p<2250;p++){s.AdvancePhase();max=Math.Max(max,s.FloodCells);peakProsperity=Math.Max(peakProsperity,s.Prosperity);if(p<12)Check(!s.Won,"Ordinary starting map is not a trivial early idle win");if(p%150==0)Integrity(s);}
            var l=s.Ledger;double waterError=Water(s)-(initial+l.WaterInput-l.WaterGrowth-l.WaterFire-l.WaterEvaporated-l.WaterOutlet);
            double goldError=Gold(s)-(gold+l.GoldInput-l.GoldMined-l.GoldBurned-l.GoldOutlet);
            double pollutionError=Pollution(s)-(pollution+l.PollutionMade-l.PollutionCleaned-l.PollutionOutlet);
            Console.WriteLine($"{season} 750 days with events: maxFlood={max}/{land}, final={s.FloodCells}, waterError={waterError:F4}, goldError={goldError:F5}, pollutionError={pollutionError:F4}, naturalFires={s.NaturalFires}");
            Check(max<land*.2f&&s.FloodCells<land*.12f,"Seasonal precipitation retains habitable land after 750 days");
            Console.WriteLine($"Idle prosperity: peak={peakProsperity:F1}, final={s.Prosperity:F1}, won={s.Won}");
            // Lucky natural prosperity is valid (the unassisted honor); it must not be an early default win.
            Check(Math.Abs(waterError)<2&&Math.Abs(goldError)<.02&&Math.Abs(pollutionError)<Math.Max(1,l.PollutionMade*.0001),"Seasonal long-run resource ledgers close within float accumulation tolerance");
        }
    }
    static bool Has(System.Collections.Generic.List<V1Honor> honors,string id)=>honors.Exists(h=>h.Id==id);
    static void HonorsAndTutorial()
    {
        var s=Blank(new V1Settings{phaseSeconds=.01f,treeGrowth=0,grassGrowth=0});
        for(int i=0;i<s.Cells.Length;i++){s.Cells[i].Grass=2.5f;s.Cells[i].Earth=100;}
        for(int i=0;i<36;i++){s.Cells[i].Wood=4;s.Cells[i].Tree=true;}
        for(int i=0;i<12;i++)Check(s.AddAnimal(s.Index(2+i%6,4+i/6),(V1AnimalKind)(i%3),V1AnimalOrigin.Wild)!=null,"Title fixture has living wild animals");
        for(int p=0;p<s.Settings.prosperityHoldPhases;p++)s.AdvancePhase();
        Check(s.ActiveVillages==0&&s.IsProsperous,"Wilderness can sustain prosperity without a village");
        var honors=V1Honors.Evaluate(s,false,false);
        Check(Has(honors,"strategist")&&Has(honors,"guardian")&&Has(honors,"unassisted")&&Has(honors,"wilderness")&&Has(honors,"wildlife"),"One run can earn multiple titles with exact conditions");
        Check(!Has(V1Honors.Evaluate(s,true,true),"strategist")&&!Has(V1Honors.Evaluate(s,true,true),"unassisted"),"Movement and intervention flags independently disqualify their titles");
        Check(Has(V1Honors.Evaluate(s,true,false),"strategist")&&!Has(V1Honors.Evaluate(s,true,false),"unassisted"),"Stationary coordinate intervention is distinct from unassisted play");
        s.Cells[50].Pollution=1;Check(!Has(V1Honors.Evaluate(s,false,false),"guardian"),"Pollution blocks guardian title");s.Cells[50].Pollution=0;
        s.Cells[50].Gold=12;Check(!Has(V1Honors.Evaluate(s,false,false),"guardian"),"Deathland blocks guardian title");s.Cells[50].Gold=0;
        s.PlayerTreeBirths=12;Check(Has(V1Honors.Evaluate(s,false,false),"planter"),"Player-grown saplings earn the planter title");
        while(s.AdvanceCount<300)s.AdvancePhase();Check(Has(V1Honors.Evaluate(s,true,true),"century"),"300 manual phases equal 100 elapsed days");
        var towns=new V1Simulation(24,24);for(int i=0;i<towns.Cells.Length;i++)towns.Cells[i]=new V1Cell{Grass=2,Water=2,Earth=20,Height=2};
        towns.Cells[towns.Index(3,5)].River=true;
        foreach(var p in new[]{towns.Index(5,5),towns.Index(17,5),towns.Index(5,17)})Check(towns.AddVillage(p,16)!=null,"Independent settlements can meet title requirements");
        Check(Has(V1Honors.Evaluate(towns,true,true),"settlements"),"Three settlements and population 48 earn settlement title");
        var tutorial=Blank();int plot=tutorial.Index(5,5);tutorial.PlaceElement(plot,V1Element.Wood);tutorial.AdvancePhase();
        Check(!tutorial.TutorialComplete,"Sapling alone does not complete the grow-tree tutorial");
        for(int p=0;p<4&&!tutorial.TutorialComplete;p++){tutorial.Cells[plot].Water=4;tutorial.Cells[plot].Earth=30;tutorial.AdvancePhase();}
        Check(tutorial.TutorialComplete&&tutorial.PlantStage(plot)>=2&&!tutorial.Won,"Tutorial ends at a player's grown tree independently of prosperity");
        tutorial.Cells[plot].Wood=0;tutorial.Cells[plot].Tree=false;tutorial.AdvancePhase();Check(tutorial.TutorialComplete,"Completed tutorial does not reappear when that tree disappears");
        Check(V1Honors.All.Length==8,"There are exactly eight title definitions");
        Console.WriteLine("PASS: eight titles, multiple awards, move/place flags, natural prosperity, clean end state and short tutorial");
    }
    static void PollutionFireRisk()
    {
        var s=Blank(new V1Settings{phaseSeconds=.5f,waterFlow=0,grassGrowth=0,treeGrowth=0});
        for(int i=0;i<s.Cells.Length;i++)s.Cells[i]=new V1Cell{Height=2,SoilBody=20};
        for(int y=3;y<=8;y++)for(int x=3;x<=8;x++)s.Cells[s.Index(x,y)].Pollution=80;
        int start=s.Index(5,5),forest=s.Index(9,5);s.Cells[start].Fire=1;
        s.Cells[forest].Tree=true;s.Cells[forest].Wood=6;
        double initial=Pollution(s);float peak=1;int affected=0;
        var seen=new bool[s.Cells.Length];
        for(int p=0;p<100;p++)
        {
            s.AdvancePhase();
            for(int i=0;i<s.Cells.Length;i++)if(s.Cells[i].Fire>.1f){seen[i]=true;peak=Math.Max(peak,s.Cells[i].Fire);}
        }
        foreach(bool b in seen)if(b)affected++;
        Console.WriteLine($"Pollution fire: peak={peak:F2}, spread={s.FireSpreads}, affected={affected}, adjacentWood={s.Cells[forest].Wood:F2}, cleaned={s.Ledger.PollutionCleaned:F1}");
        Check(peak>3&&s.FireSpreads>8&&affected>12,"Pollution fuels stronger fire and multi-generation spread on bare ground");
        Check(s.Cells[forest].Wood<5,"Pollution fire threatens adjacent clean trees");
        Check(Math.Abs(Pollution(s)-(initial+s.Ledger.PollutionMade-s.Ledger.PollutionCleaned-s.Ledger.PollutionOutlet))<.02,"Combustible pollution retains the resource ledger");
        for(int i=0;i<s.Cells.Length;i++){s.Cells[i].Pollution=0;s.Cells[i].Wood=0;s.Cells[i].Grass=0;s.Cells[i].Earth=0;s.Cells[i].Gold=0;}
        for(int p=0;p<50;p++)s.AdvancePhase();
        foreach(var c in s.Cells)Check(c.Fire<.001f,"Fuel exhaustion extinguishes fire");
        var barrier=Blank(new V1Settings{phaseSeconds=.5f,waterFlow=0,grassGrowth=0,treeGrowth=0});
        for(int i=0;i<barrier.Cells.Length;i++)barrier.Cells[i]=new V1Cell{Height=2,Pollution=80};
        for(int y=0;y<12;y++)barrier.Cells[barrier.Index(6,y)]=new V1Cell{River=true,Water=30,Height=2};
        barrier.Cells[barrier.Index(4,5)].Fire=4;
        for(int p=0;p<80;p++)barrier.AdvancePhase();
        for(int y=0;y<12;y++)for(int x=7;x<12;x++)Check(barrier.Cells[barrier.Index(x,y)].Fire==0,"River blocks pollution fire from crossing");
    }
    static void RandomWorldProsperity()
    {
        var lucky=V1Level.Create(new V1Settings{initialSeason=V1Season.Spring},17,true);
        var repeat=V1Level.Create(new V1Settings{initialSeason=V1Season.Spring},17,true);
        var ordinary=V1Level.Create(new V1Settings{initialSeason=V1Season.Summer},2,true);
        Check(lucky.AdvanceCount==0&&lucky.StablePhases==0&&lucky.Prosperity>0&&lucky.MatureCoverage>0,"Initial scores describe the real map without advancing time or stability");
        int different=0;
        for(int i=0;i<lucky.Cells.Length;i++)
        {
            Check(lucky.Cells[i].Height==repeat.Cells[i].Height&&lucky.Cells[i].Earth==repeat.Cells[i].Earth&&lucky.Cells[i].Wood==repeat.Cells[i].Wood,"A saved seed reproduces physical starting conditions");
            if(lucky.Cells[i].Height!=ordinary.Cells[i].Height)different++;
        }
        Check(different>100,"Different seeds produce materially different terrain");
        bool naturalTitle=false;int first=-1;
        for(int p=0;p<120;p++)
        {
            lucky.AdvancePhase();ordinary.AdvancePhase();
            if(p<12)Check(!lucky.Won&&!ordinary.Won,"Even a fortunate seed is not a trivial early win");
            if(lucky.IsProsperous){naturalTitle|=Has(V1Honors.Evaluate(lucky,false,false),"unassisted");if(first<0)first=p+1;}
        }
        Check(naturalTitle&&lucky.Won,"A fortunate map and event sequence can earn unassisted prosperity without intervention");
        Check(!ordinary.Won,"An ordinary seed does not reach prosperity simply by repeating advance");
        float before=lucky.Prosperity;
        for(int i=0;i<lucky.Cells.Length;i++){lucky.Cells[i].Wood=0;lucky.Cells[i].Tree=false;lucky.Cells[i].Seeds=0;lucky.Cells[i].Pollution=100;}
        lucky.AdvancePhase();
        Check(lucky.Prosperity<before&&!lucky.IsProsperous&&lucky.StablePhases==0,"Degradation resets current stability instead of preserving a historical win");
        Console.WriteLine($"PASS: random physical maps, ordinary idle failure, lucky unassisted title at phase {first}, current prosperity lost after damage");
    }
    public static int Main(string[] args)
    {
        try {
            RandomWorldProsperity();PollutionFireRisk();HonorsAndTutorial();
            if(args.Length>0&&args[0]=="--balance"){Console.WriteLine("PASS: focused balance checks, "+assertions+" assertions");return 0;}
            PhaseEventsAndCleanup();SeedsAndSpringBirds();Trees();GoldAndFire();AcceptanceRegressions();Hydrology();VillageRegionsAndContinuity();MapAndBudgets();TerrainAndEcology();SeasonalLongRuns();Console.WriteLine("PASS: "+assertions+" assertions");return 0;
        }
        catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
    }
}

