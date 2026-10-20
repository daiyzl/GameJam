using System;
using System.Linq;
using System.Diagnostics;
public static class Program
{
    static int assertions;
    static void Assert(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    static SettlementSimulation Map()
    {
        var s=new SettlementSimulation(30,24,new SettlementSettings());
        for(int y=0;y<s.Height;y++)for(int x=0;x<s.Width;x++)
        {int i=s.Index(x,y);s.Blocked[i]=x==0||y==0||x==s.Width-1||y==s.Height-1;s.River[i]=x==9;s.Elevation[i]=x==9?1:2.4f;s.Cells[i]=new CellState{Earth=8,Water=x==9?8:1,Wood=x==9?0:3};}
        return s;
    }
    static void HoldHealthy(SettlementSimulation s)
    {for(int i=0;i<s.Cells.Length;i++)if(s.Valid(i)){var c=s.Cells[i];c.Water=s.River[i]?8:1;c.Earth=8;c.Wood=s.River[i]?0:3;c.Fire=0;s.Cells[i]=c;}}
    static void Main()
    {
        var s=Map();int center=s.Index(6,6);var v=s.AddVillage(center);Assert(v!=null,"village needs riverside spawn");Assert(s.ReadRegion(center).cells==36,"exact 6x6 region");
        Assert(s.AddVillage(s.Index(5,7))==null,"exclude village inside 36 cells");
        for(int y=4;y<=9;y++)for(int x=18;x<=23;x++)s.Cells[s.Index(x,y)].Water=0;
        Assert(s.AddVillage(s.Index(20,6))==null,"dry area must reject village");
        s.CharacterCell=s.Index(7,15);Assert(!s.CanSettle(s.Index(6,14)),"new village must avoid player's current footprint");s.CharacterCell=-1;
        var notRiver=Map();Array.Clear(notRiver.River,0,notRiver.River.Length);Assert(notRiver.AddVillage(center)==null,"initial village must be near actual river");
        int gold=s.Index(5,5);s.Cells[gold].Gold=20;
        for(int k=0;k<18;k++){HoldHealthy(s);s.Step();}
        Assert(v.population>=18&&v.tier>=3&&v.housing>12,"healthy village needs resources to expand");
        int oldPopulation=v.population;
        for(int dy=-2;dy<=3;dy++)for(int dx=-2;dx<=3;dx++){int i=s.Index(6+dx,6+dy);s.River[i]=false;s.Cells[i]=new CellState{Earth=1,Fire=6,Wood=1};}
        s.Step();s.Step();Assert(v.population<oldPopulation,"fire must reduce population");
        for(int k=0;k<12&&v.migrations==0&&v.active;k++)s.Step();Assert(v.migrations>0&&v.cell!=center,"village must migrate to healthier river bank");
        var gone=Map();var departed=gone.AddVillage(center);Array.Clear(gone.River,0,gone.River.Length);for(int i=0;i<gone.Cells.Length;i++)gone.Cells[i]=new CellState{Fire=3};
        for(int k=0;k<12;k++)gone.Step();Assert(!departed.active&&gone.TotalPopulation==0,"no viable region: villagers leave map");
        var birth=Map();var parent=birth.AddVillage(center,26);parent.housing=40;parent.region=birth.ReadRegion(center);
        for(int y=10;y<=18;y++)birth.Cells[birth.Index(5,y)].Gold=8;
        Assert(birth.TryNewVillage()&&birth.NewVillages==1,"healthy new region attracts village");
        var second=birth.Villages[1];Assert(!birth.InRegion(parent.cell,second.cell)&&!birth.InRegion(second.cell,parent.cell),"new village exclusion symmetric");
        Assert(birth.Villages[1].population==6&&parent.population==22,"settler transfer from parent");
        var wild=Map();int start=wild.Index(16,6);wild.Cells[start]=new CellState{Earth=1};int better=wild.Index(17,6);wild.Cells[better]=new CellState{Wood=6,Water=2,Earth=8};var deer=wild.AddAnimal(start,AnimalKind.Deer,AnimalOrigin.Wild);wild.Step();Assert(deer.cell!=start&&wild.WildlifeMoves>0,"wild animals migrate towards habitat");
        var farm=Map();var owner=farm.AddVillage(center);owner.housing=48;farm.Cells[gold].Gold=30;var cow=farm.AddAnimal(farm.Index(5,5),AnimalKind.Cow,AnimalOrigin.Farmed,owner.id);
        for(int k=0;k<40;k++){HoldHealthy(farm);farm.Step();foreach(var a in farm.Animals)if(a.origin==AnimalOrigin.Farmed)Assert(farm.InRegion(owner.cell,a.cell),"farms stay in owner's 36 cells");}
        Assert(farm.FarmBirths>0,"healthy farm animals breed");Assert(farm.AddAnimal(farm.Index(20,6),AnimalKind.Chicken,AnimalOrigin.Farmed,owner.id)==null,"farm animal outside village rejected");
        var e=Map();int at=e.Index(20,6);e.Cells[at]=new CellState{Wood=2,Earth=2,Water=4};e.Step();Assert(e.Cells[at].Wood>2,"wood grows with water and earth");
        e.Cells[at]=new CellState{Wood=4,Fire=.1f};float before=e.Cells[at].Wood;e.Step();Assert(e.Cells[at].Wood<before&&e.Cells[at].Earth>0,"small fire burns wood into soil");
        e.Cells[at]=new CellState{Wood=4,Fire=2,Water=4};e.Step();Assert(e.Cells[at].Fire==0,"water prioritizes extinguishing");
        e.Cells[at]=new CellState{Wood=4,Gold=2};e.Step();Assert(e.Cells[at].Wood==4,"construction gold no longer destroys healthy forest");
        int stock=e.Inventory[0];Assert(e.AddElement(at,Element.Gold)&&e.Inventory[0]==stock-4,"inventory charge on placement");e.Inventory[0]=0;Assert(!e.AddElement(at,Element.Gold),"no negative inventory");Assert(!e.AddElement(-1,Element.Water),"out-of-range placement rejected");
        var win=Map();var town=win.AddVillage(center,40);town.housing=48;for(int a=0;a<12;a++){win.AddAnimal(win.Index(4+a%3,4+a/3),(AnimalKind)(a%4),AnimalOrigin.Farmed,town.id);win.AddAnimal(win.Index(15+a%4,4+a/4),(AnimalKind)(a%4),AnimalOrigin.Wild);}
        for(int k=0;k<8;k++){HoldHealthy(win);win.Step();}Assert(win.Won&&win.Prosperity>=win.Settings.prosperityTarget,"prosperity-based win persists required steps");
        var replay1=Map();var replay2=Map();replay1.AddVillage(center);replay2.AddVillage(center);replay1.AddAnimal(start,AnimalKind.Deer,AnimalOrigin.Wild);replay2.AddAnimal(start,AnimalKind.Deer,AnimalOrigin.Wild);
        var timer=Stopwatch.StartNew();for(int k=0;k<240;k++){replay1.Step();replay2.Step();for(int i=0;i<replay1.Cells.Length;i++){var a=replay1.Cells[i];var b=replay2.Cells[i];Assert(a.Water==b.Water&&a.Wood==b.Wood&&a.Fire==b.Fire,"deterministic buffered ecology");Assert(a.Water>=0&&a.Wood>=0&&a.Earth>=0&&a.Fire>=0&&a.Gold>=0,"nonnegative quantities");}}
        timer.Stop();Console.WriteLine($"PASS: {assertions} assertions; growth, decline, migration, departure, settlement exclusion, animal movement/breeding, ecology, stocks, prosperity win, deterministic replay. 480 simulated steps + assertions: {timer.ElapsedMilliseconds}ms.");
    }
}
