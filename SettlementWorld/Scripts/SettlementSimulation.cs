using System;
using System.Collections.Generic;

[Serializable]
public class SettlementSettings
{
    public float stepSeconds=.8f, prosperityTarget=65;
    public int prosperityHoldSteps=5, migrationDelay=10, newVillageInterval=8;
    public float forestGrowth=.18f, burnRate=.16f, flowRate=.45f;
}
public enum AnimalKind { Deer, Rabbit, Cow, Chicken }
public enum AnimalOrigin { Wild, Farmed }
[Serializable]
public class SettlementAnimal
{
    public int id,cell,owner=-1,age,starvation;
    public AnimalKind kind;
    public AnimalOrigin origin;
    public bool alive=true;
}
[Serializable]
public class SettlementVillage
{
    public int id,cell,population=8,tier=1,badSteps,migrations;
    public float housing=12;
    public bool active=true;
    public SettlementRegion region;
    public string condition="等待环境评估";
}
public struct SettlementRegion
{
    public int cells,habitable,wet,green,wild,farmed,burning;
    public float water,wood,earth,gold,fire,food,capacity,health;
}

// Deterministic local ecology and settlement rules. No scene, sprites or frame timing here.
public sealed class SettlementSimulation
{
    public readonly int Width,Height;
    public readonly CellState[] Cells;
    public readonly bool[] Blocked,River;
    public readonly float[] Elevation;
    public readonly SettlementSettings Settings;
    public readonly List<SettlementVillage> Villages=new List<SettlementVillage>();
    public readonly List<SettlementAnimal> Animals=new List<SettlementAnimal>();
    public readonly int[] Inventory={24,24,72,8,36};
    public readonly int[] FarmInventory={3,4,3,6};
    public int StepCount {get;private set;}
    public float Prosperity {get;private set;}
    public int StableSteps {get;private set;}
    public bool Won {get;private set;}
    public string LastEvent {get;private set;}="河边的小村庄等待你的帮助。";
    public int WildlifeMoves {get;private set;}
    public int FarmBirths {get;private set;}
    public int NewVillages {get;private set;}
    public int CharacterCell=-1;
    public int TotalPopulation {get {int n=0;foreach(var v in Villages)if(v.active)n+=v.population;return n;}}
    private readonly CellState[] next;
    private readonly float[] waterDelta;
    private readonly int[] neighbors;
    private readonly float[] desire=new float[4];
    private uint random=317;
    private int nextVillageId=1,nextAnimalId=1;
    public SettlementSimulation(int width,int height,SettlementSettings settings)
    {
        if(width<6||height<6||width>64||height>64)throw new ArgumentOutOfRangeException("map");
        Width=width;Height=height;Settings=settings??new SettlementSettings();
        Cells=new CellState[width*height];next=new CellState[Cells.Length];Blocked=new bool[Cells.Length];River=new bool[Cells.Length];Elevation=new float[Cells.Length];waterDelta=new float[Cells.Length];neighbors=new int[Cells.Length*4];
        for(int i=0;i<Cells.Length;i++){int x=i%width,y=i/width;neighbors[i*4]=Index(x+1,y);neighbors[i*4+1]=Index(x,y+1);neighbors[i*4+2]=Index(x-1,y);neighbors[i*4+3]=Index(x,y-1);}
    }
    public int Index(int x,int y)=>x>=0&&y>=0&&x<Width&&y<Height?x+y*Width:-1;
    public bool Valid(int i)=>i>=0&&i<Cells.Length&&!Blocked[i];
    private static float Min(float a,float b)=>Math.Min(a,b);
    private static float Clamp(float x,float lo,float hi)=>Math.Max(lo,Math.Min(hi,x));
    private int Random(int count){random^=random<<13;random^=random>>17;random^=random<<5;return (int)(random%(uint)count);}
    public bool InRegion(int center,int cell)
    {int dx=cell%Width-center%Width,dy=cell/Width-center/Width;return cell>=0&&dx>=-2&&dx<=3&&dy>=-2&&dy<=3;}
    public bool InMapRegion(int center)=>center%Width>=2&&center%Width<Width-3&&center/Width>=2&&center/Width<Height-3;
    public SettlementVillage VillageAt(int cell)
    {foreach(var v in Villages)if(v.active&&InRegion(v.cell,cell))return v;return null;}
    public SettlementVillage ById(int id){foreach(var v in Villages)if(v.active&&v.id==id)return v;return null;}
    public bool IsBuilding(int cell)
    {foreach(var v in Villages)if(v.active){int dx=cell%Width-v.cell%Width,dy=cell/Width-v.cell/Width;if(dx>=0&&dx<=1&&dy>=0&&dy<=1)return true;}return false;}
    public bool CanSettle(int cell,int excludingId=-1)
    {
        if(!Valid(cell)||!InMapRegion(cell)||River[cell]||Cells[cell].Water>3||Cells[cell].Fire>.05f)return false;
        if(!Valid(Index(cell%Width+1,cell/Width))||!Valid(Index(cell%Width,cell/Width+1))||!Valid(Index(cell%Width+1,cell/Width+1)))return false;
        for(int dy=0;dy<=1;dy++)for(int dx=0;dx<=1;dx++){int i=Index(cell%Width+dx,cell/Width+dy);if(River[i]||Cells[i].Fire>.05f||Cells[i].Water>3)return false;}
        if(CharacterCell>=0)
        {int dx=CharacterCell%Width-cell%Width,dy=CharacterCell/Width-cell/Width;if(dx>=0&&dx<=1&&dy>=0&&dy<=1)return false;}
        foreach(var v in Villages)if(v.active&&v.id!=excludingId&&(InRegion(v.cell,cell)||InRegion(cell,v.cell)))return false;
        var r=ReadRegion(cell);return r.cells==36&&r.wet>=2&&r.habitable>=18;
    }
    public SettlementVillage AddVillage(int cell,int population=8)
    {
        if(!CanSettle(cell)||Villages.Count==0&&!NearRiver(cell))return null;var v=new SettlementVillage{id=nextVillageId++,cell=cell,population=population};Villages.Add(v);v.region=ReadRegion(cell);return v;
    }
    public bool NearRiver(int center)
    {for(int dy=-2;dy<=3;dy++)for(int dx=-2;dx<=3;dx++){int i=Index(center%Width+dx,center/Width+dy);if(i>=0&&River[i])return true;}return false;}
    public bool AddElement(int cell,Element element,bool spend=true)
    {
        int e=(int)element;if(!Valid(cell)||e<0||e>4||IsBuilding(cell))return false;
        int cost=element==Element.Water?4:element==Element.Earth?3:element==Element.Wood?2:element==Element.Gold?4:1;
        if(spend&&Inventory[e]<cost)return false;
        // Never waste a charge on an already saturated cell.
        float cap=element==Element.Water?12:element==Element.Earth?12:element==Element.Wood?12:element==Element.Gold?20:10;
        if(Cells[cell].Get(element)>=cap-.01f)return false;
        if(spend)Inventory[e]-=cost;
        var c=Cells[cell];c.Set(element,Min(cap,c.Get(element)+cost));Cells[cell]=c;
        LastEvent=ElementName(element)+"已投放，周围环境将继续自行变化。";return true;
    }
    public static string ElementName(Element element)=>new[]{"金","木","水","火","土"}[(int)element];
    public static string AnimalName(AnimalKind kind)=>new[]{"鹿","兔","牛","鸡"}[(int)kind];
    public SettlementAnimal AddAnimal(int cell,AnimalKind kind,AnimalOrigin origin,int owner=-1,bool spend=false)
    {
        if(!Valid(cell)||River[cell]||IsBuilding(cell)||Cells[cell].Fire>.05f||Cells[cell].Water>3||Animals.Count>=96)return null;
        int count=0;foreach(var a in Animals)if(a.alive&&a.cell==cell)count++;if(count>=2)return null;
        if(origin==AnimalOrigin.Farmed){var village=owner<0?VillageAt(cell):ById(owner);if(village==null||!InRegion(village.cell,cell))return null;owner=village.id;}
        if(spend&&FarmInventory[(int)kind]<=0)return null;
        if(spend)FarmInventory[(int)kind]--;
        var animal=new SettlementAnimal{id=nextAnimalId++,cell=cell,kind=kind,origin=origin,owner=owner};Animals.Add(animal);return animal;
    }
    public SettlementRegion ReadRegion(int center)
    {
        var r=new SettlementRegion();int cx=center%Width,cy=center/Width;
        for(int y=cy-2;y<=cy+3;y++)for(int x=cx-2;x<=cx+3;x++)
        {
            int i=Index(x,y);if(i<0)continue;r.cells++;if(Blocked[i])continue;var c=Cells[i];
            r.water+=c.Water;r.wood+=c.Wood;r.earth+=c.Earth;r.gold+=c.Gold;r.fire+=c.Fire;
            if(River[i]||c.Water>.5f)r.wet++;if(c.Wood>.5f&&c.Fire<.1f)r.green++;if(c.Fire>.1f)r.burning++;
            if(!River[i]&&c.Water<3&&c.Fire<.1f)r.habitable++;
        }
        foreach(var a in Animals)if(a.alive&&InRegion(center,a.cell)){if(a.origin==AnimalOrigin.Farmed)r.farmed++;else r.wild++;}
        // Forest and healthy grazers produce food; excessively dense forest does not add unlimited capacity.
        r.food=Min(24,r.green*.9f+r.wood*.14f)+r.farmed*3+r.wild*1.2f;
        float waterSupport=Min(40,r.wet*4+r.water*.35f);
        r.capacity=Min(r.habitable*1.3f,Min(waterSupport,r.food*1.8f+4));
        r.health=Clamp(Min(1,r.wet/3f)*.3f+Min(1,r.food/12f)*.4f+Min(1,r.habitable/24f)*.3f-r.burning*.13f-r.fire*.045f,0,1);
        return r;
    }
    public float Habitat(int cell,AnimalKind kind)
    {
        if(!Valid(cell)||River[cell]||IsBuilding(cell)||Cells[cell].Water>3)return -100;
        var c=Cells[cell];float wood=c.Wood,water=c.Water;float fire=c.Fire;
        for(int d=0;d<4;d++){int j=neighbors[cell*4+d];if(Valid(j)){wood+=Cells[j].Wood*.3f;water+=Cells[j].Water*.3f;fire+=Cells[j].Fire*.8f;}}
        float food=kind==AnimalKind.Deer?Min(wood,7):kind==AnimalKind.Cow?Min(wood,3)*1.7f:kind==AnimalKind.Rabbit?Min(wood,4)*1.4f:Min(wood,3)+Min(c.Earth,5)*.35f;
        return food+Min(water,3)-fire*9-(c.Wood>10?3:0);
    }
    public void Step()
    {
        StepCount++;Ecology();UpdateAnimals();UpdateVillages();
        if(StepCount%Settings.newVillageInterval==0)TryNewVillage();
        CalculateProsperity();
        if(Prosperity>=Settings.prosperityTarget&&TotalPopulation>0)StableSteps++;else StableSteps=0;
        if(StableSteps>=Settings.prosperityHoldSteps)Won=true;
        if(StepCount%6==0){Inventory[(int)Element.Water]=Math.Min(96,Inventory[(int)Element.Water]+4);Inventory[(int)Element.Wood]=Math.Min(32,Inventory[(int)Element.Wood]+2);Inventory[(int)Element.Earth]=Math.Min(48,Inventory[(int)Element.Earth]+3);}
        if(StepCount%12==0)Inventory[(int)Element.Gold]=Math.Min(32,Inventory[(int)Element.Gold]+2);
    }
    private void Ecology()
    {
        // Buffered phases: cooling, fuel/growth, then simultaneous water flow.
        for(int i=0;i<Cells.Length;i++)
        {
            var c=Cells[i];if(!Valid(i)){next[i]=default(CellState);continue;}
            if(River[i])c.Water=Math.Max(c.Water,8);
            float quench=Min(c.Fire,c.Water/1.2f);c.Fire-=quench;c.Water-=quench*1.2f;next[i]=c;
        }
        Array.Copy(next,Cells,Cells.Length);
        for(int i=0;i<Cells.Length;i++)
        {
            var c=Cells[i];if(!Valid(i))continue;
            if(c.Fire>.001f)
            {
                float burned=Min(c.Wood,Min(1.5f,c.Fire*c.Wood*Settings.burnRate));c.Wood-=burned;c.Earth=Min(12,c.Earth+burned*.8f);c.Fire=Clamp(c.Fire+burned*.6f-.18f,0,12);
                if(c.Fire>c.Gold*1.5f)c.Gold=Math.Max(0,c.Gold-(c.Fire-c.Gold*1.5f)*.08f);
            }
            else if(c.Wood>.001f&&!IsBuilding(i)&&!River[i])
            {
                float gain=Min(Settings.forestGrowth,Min(c.Earth,Min(c.Water/2,12-c.Wood)));
                c.Wood+=gain;c.Earth-=gain;c.Water-=gain*2;
                // Metals now mean tools/building reserves: only very dense vegetation is pruned.
                if(c.Gold>0&&c.Wood>8)c.Wood=Math.Max(8,c.Wood-c.Gold*.04f);
                c.Earth=Min(12,c.Earth+.015f);
            }
            if(c.Fire<.001f&&c.Wood>.1f&&c.Water<.25f)
            {for(int d=0;d<4;d++){int j=neighbors[i*4+d];if(Valid(j)&&Cells[j].Fire>.5f){c.Fire=.3f;break;}}}
            // Seeds reach a neighbor after previous ticks grew a sufficiently large tree.
            if(StepCount%5==0&&c.Wood<.05f&&c.Water>.25f&&c.Earth>.5f&&!River[i]&&!IsBuilding(i)&&c.Fire<.001f)
            {for(int d=0;d<4;d++){int j=neighbors[i*4+d];if(Valid(j)&&Cells[j].Wood>5&&Cells[j].Fire<.01f){c.Wood=.2f;c.Earth-=.2f;break;}}}
            next[i]=c;
        }
        Array.Clear(waterDelta,0,waterDelta.Length);
        for(int i=0;i<Cells.Length;i++)
        {
            if(!Valid(i)||next[i].Water<=.001f)continue;float total=0;
            float budget=Min(next[i].Water,Settings.flowRate/(1+next[i].Earth*.06f));
            float head=Elevation[i]+next[i].Earth*.035f+next[i].Water*.3f;
            for(int d=0;d<4;d++)
            {
                int j=neighbors[i*4+d];desire[d]=0;if(!Valid(j)||IsBuilding(j))continue;
                float diff=head-(Elevation[j]+next[j].Earth*.035f+next[j].Water*.3f);
                if(diff>0)desire[d]=Min(budget,diff*.4f)/(1+next[j].Wood*.04f);total+=desire[d];
            }
            float scale=total>budget?budget/total:1;
            for(int d=0;d<4;d++)if(desire[d]>0){float amount=desire[d]*scale;waterDelta[i]-=amount;waterDelta[neighbors[i*4+d]]+=amount;}
        }
        for(int i=0;i<Cells.Length;i++){var c=next[i];if(Valid(i))c.Water=Clamp(c.Water+waterDelta[i]-(River[i]?0:.01f),0,20);Cells[i]=c;}
    }
    private void UpdateAnimals()
    {
        int oldCount=Animals.Count;
        for(int k=0;k<oldCount;k++)
        {
            var a=Animals[k];if(!a.alive)continue;a.age++;
            var owner=a.origin==AnimalOrigin.Farmed?ById(a.owner):null;
            if(a.origin==AnimalOrigin.Farmed&&owner==null){a.origin=AnimalOrigin.Wild;a.owner=-1;LastEvent="失去村庄照料的动物回到了野外。";}
            if(a.origin==AnimalOrigin.Farmed&&owner!=null&&!InRegion(owner.cell,a.cell))
            {
                int destination=BestAnimalCell(owner.cell,a.kind,owner);if(destination>=0)a.cell=destination;else {a.origin=AnimalOrigin.Wild;a.owner=-1;}
            }
            float score=Habitat(a.cell,a.kind);int chosen=a.cell;float best=score;
            int start=Random(4);
            for(int d=0;d<4;d++)
            {
                int j=neighbors[a.cell*4+(d+start)%4];if(!Valid(j)||a.origin==AnimalOrigin.Farmed&&(owner==null||!InRegion(owner.cell,j)))continue;
                float s=Habitat(j,a.kind);if(s>best+.04f){chosen=j;best=s;}
            }
            if(chosen==a.cell&&a.origin==AnimalOrigin.Wild&&StepCount%3==0)
            {
                int j=neighbors[a.cell*4+Random(4)];float nearby=Habitat(j,a.kind);
                if(nearby>=Math.Max(.6f,score-.75f)){chosen=j;best=nearby;}
            }
            if(chosen!=a.cell){a.cell=chosen;if(a.origin==AnimalOrigin.Wild)WildlifeMoves++;}
            if(best<.6f||Cells[a.cell].Fire>.2f)a.starvation++;else a.starvation=Math.Max(0,a.starvation-2);
            if(a.starvation>=8){a.alive=false;continue;}
            // Farming needs actual forage; nutrients return through manure.
            if(a.origin==AnimalOrigin.Farmed&&best>.6f)
            {
                var c=Cells[a.cell];float feed=Min(c.Wood,a.kind==AnimalKind.Cow?.035f:.012f);c.Wood-=feed;c.Earth=Min(12,c.Earth+feed*.7f);Cells[a.cell]=c;
                if(a.age%18==0&&a.starvation==0&&AliveCount(a.origin,a.owner)<12)
                {int target=BestAnimalCell(a.cell,a.kind,owner);if(target>=0&&AddAnimal(target,a.kind,AnimalOrigin.Farmed,a.owner)!=null)FarmBirths++;}
            }
        }
        if(StepCount%12==0&&AliveCount(AnimalOrigin.Wild,-1)<22)
        {
            for(int attempt=0;attempt<18;attempt++)
            {int i=Random(Cells.Length);AnimalKind kind=(AnimalKind)Random(4);if(Habitat(i,kind)>4&&AddAnimal(i,kind,AnimalOrigin.Wild)!=null)break;}
        }
        // Recycle departed/dead entries so long sessions never hit an allocation cap.
        Animals.RemoveAll(a=>!a.alive);
    }
    private int AliveCount(AnimalOrigin origin,int owner)
    {int n=0;foreach(var a in Animals)if(a.alive&&a.origin==origin&&(owner<0||a.owner==owner))n++;return n;}
    private int BestAnimalCell(int center,AnimalKind kind,SettlementVillage owner)
    {
        int result=-1;float best=.6f;
        for(int dy=-2;dy<=3;dy++)for(int dx=-2;dx<=3;dx++)
        {int i=Index(center%Width+dx,center/Width+dy);if(i<0||owner!=null&&!InRegion(owner.cell,i))continue;int occupied=0;foreach(var a in Animals)if(a.alive&&a.cell==i)occupied++;if(occupied>=2)continue;float s=Habitat(i,kind);if(s>best){best=s;result=i;}}
        return result;
    }
    private float Consume(int center,Element element,float amount)
    {
        float left=amount;int cx=center%Width,cy=center/Width;
        for(int y=cy-2;y<=cy+3&&left>.001f;y++)for(int x=cx-2;x<=cx+3&&left>.001f;x++)
        {int i=Index(x,y);if(!Valid(i))continue;var c=Cells[i];float reserve=element==Element.Wood?.6f:0;float take=Min(left,Math.Max(0,c.Get(element)-reserve));c.Set(element,c.Get(element)-take);Cells[i]=c;left-=take;}
        return amount-left;
    }
    private void UpdateVillages()
    {
        foreach(var v in Villages)
        {
            if(!v.active)continue;var r=ReadRegion(v.cell);v.region=r;
            bool safe=r.burning==0&&r.fire<.1f;
            if(safe&&r.gold>=1&&r.wood>=4&&r.capacity>v.housing)
            {Consume(v.cell,Element.Gold,1);Consume(v.cell,Element.Wood,1);v.housing=Min(48,v.housing+2);}
            float capacity=Min(v.housing,r.capacity);
            if(safe&&r.wet>=2&&r.health>=.72f&&capacity>=v.population+1)
            {v.population++;v.badSteps=0;v.condition="人口增长：饮水、食物与住房充足";}
            else if(!safe||r.wet<2||capacity<v.population-1||r.health<.5f)
            {
                v.badSteps++;if(v.badSteps%2==0)v.population=Math.Max(1,v.population-(!safe?2:1));
                v.condition=!safe?"火灾威胁，居民正在离开":r.wet<2?"缺水，人口减少":"食物或住房不足，规模缩小";
            }
            else {v.badSteps=Math.Max(0,v.badSteps-1);v.condition=capacity<=v.population+1&&v.housing<r.capacity?"缺少建材：在周围投放金与木":"环境稳定，等待更多食物与可居住土地";}
            v.tier=v.population>=28?4:v.population>=18?3:v.population>=10?2:1;
            if(v.badSteps>=Settings.migrationDelay)
            {
                int destination=FindSettlement(v.id);if(destination>=0)
                {v.cell=destination;v.migrations++;v.badSteps=0;v.population=Math.Max(4,v.population);v.housing=12;v.region=ReadRegion(destination);v.condition="已迁往条件更好的河岸";LastEvent="村庄迁移了：原区域条件无法维持生活。";}
                else {v.active=false;v.population=0;LastEvent="村民搬离了地图。改善河岸环境后，仍有机会迎来新的村庄。";}
            }
        }
    }
    private int FindSettlement(int excludingId=-1,bool requiresMaterials=false)
    {
        int best=-1;float score=12;
        for(int y=2;y<Height-3;y++)for(int x=2;x<Width-3;x++)
        {int i=Index(x,y);if(!CanSettle(i,excludingId))continue;var r=ReadRegion(i);if(requiresMaterials&&r.gold<4)continue;float s=r.capacity*r.health;if(r.health>=.72f&&r.fire<.1f&&r.food>=6&&s>score){score=s;best=i;}}
        return best;
    }
    public bool TryNewVillage()
    {
        int active=0;SettlementVillage parent=null;foreach(var v in Villages)if(v.active){active++;if(v.population>=20&&v.region.health>=.75f&&(parent==null||v.population>parent.population))parent=v;}
        if(active>=5||active>0&&parent==null)return false;
        int candidate=FindSettlement(-1,true);if(candidate<0)return false;
        var born=AddVillage(candidate,6);if(born==null)return false;Consume(candidate,Element.Gold,4);if(parent!=null)parent.population-=4;
        NewVillages++;LastEvent="新村庄在宜居的河岸出现了，现有村庄周围36格内不会重复生成。";return true;
    }
    public void CalculateProsperity()
    {
        float ecology=0;int land=0;
        for(int i=0;i<Cells.Length;i++)if(Valid(i)&&!River[i]){land++;ecology+=Min(Cells[i].Wood/4,1)*(Cells[i].Fire>.1f?0:1);}
        float pop=Min(40,TotalPopulation*.9f),quality=0;int villages=0,wild=0,farmed=0;
        foreach(var v in Villages)if(v.active){quality+=ReadRegion(v.cell).health;villages++;}
        foreach(var a in Animals)if(a.alive){if(a.origin==AnimalOrigin.Wild)wild++;else farmed++;}
        Prosperity=Clamp(pop+(villages>0?quality/villages*20:0)+(land>0?ecology/land*20:0)+Min(10,wild*.7f)+Min(10,farmed*1.5f),0,100);
    }
}
