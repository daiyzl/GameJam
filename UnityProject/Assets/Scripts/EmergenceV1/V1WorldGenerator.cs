using System;
using System.Collections.Generic;

public sealed class V1GeneratedBridge
{
    public int[] cells;
    public bool horizontal;
}
public sealed class V1GeneratedWorld
{
    public V1Simulation simulation;
    public int spawn, tutorial, village;
    public readonly List<V1GeneratedBridge> bridges=new List<V1GeneratedBridge>();
}

// Reproducible physical world generation. The outer rock boundary is the only fixed layout.
public sealed class V1WorldGenerator
{
    private readonly int width,height,seed;
    private uint random;
    private readonly V1Simulation sim;
    private readonly V1GeneratedWorld result=new V1GeneratedWorld();
    private readonly float fertility;
    private V1WorldGenerator(int width,int height,V1Settings settings,int seed)
    {
        this.width=width;this.height=height;this.seed=seed;
        random=unchecked((uint)seed)^0xA3C59AC3u;if(random==0)random=317;
        sim=new V1Simulation(width,height,settings,unchecked((uint)seed));result.simulation=sim;
        float draw=Unit();fertility=draw*draw*draw;
    }
    public static V1GeneratedWorld Generate(int width,int height,V1Settings settings,int seed)
    {
        if(width<16||height<16)throw new ArgumentException("Procedural worlds require at least 16 × 16 cells.");
        var g=new V1WorldGenerator(width,height,settings,seed);g.Land();g.Rivers();g.Lakes();g.Resources();g.Bridges();g.Residents();return g.result;
    }
    private int Next(int n){random^=random<<13;random^=random>>17;random^=random<<5;return (int)(random%(uint)n);}
    private float Unit()=>Next(1000000)/1000000f;
    private int Cell(int x,int y)=>sim.Index(x,y);
    private bool Interior(int i)=>i>=0&&i<sim.Cells.Length&&i%width>0&&i%width<width-1&&i/width>0&&i/width<height-1;
    private int Adjacent(int i,int d)
    {int x=i%width+(d==0?1:d==2?-1:0),y=i/width+(d==1?1:d==3?-1:0);return Cell(x,y);}
    private float Noise(int x,int y,int offset,float scale)=>V1Terrain.Noise((x+27.31f)*scale,(y+61.77f)*scale,unchecked(seed+offset));
    private void Land()
    {
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            bool boundary=x==0||y==0||x==width-1||y==height-1;
            float n=Noise(x,y,0,.085f)*.75f+Noise(x,y,37,.17f)*.25f,soil=Noise(x,y,91,.13f);
            sim.Cells[Cell(x,y)]=new V1Cell{Blocked=boundary,Height=V1Simulation.Clamp(2.8f+(n-.5f)*7,1.8f,6),SoilBody=30,
                Earth=6+fertility*13+soil*6,Water=.5f+fertility*1.4f+soil*.6f,Grass=.9f+soil*.9f};
        }
    }
    private int Edge(int side)
    {int x=3+Next(width-6),y=3+Next(height-6);return side==0?Cell(x,height-2):side==1?Cell(width-2,y):side==2?Cell(x,1):Cell(1,y);}
    private List<int> Route(int start,int goal,bool landOnly=false)
    {
        int length=sim.Cells.Length;var cost=new float[length];var from=new int[length];var closed=new bool[length];
        for(int i=0;i<length;i++){cost[i]=float.MaxValue;from[i]=-1;}cost[start]=0;
        for(int step=0;step<length;step++)
        {
            int best=-1;float bestScore=float.MaxValue;
            for(int i=0;i<length;i++)if(!closed[i]&&cost[i]<float.MaxValue)
            {float score=cost[i]+.45f*(Math.Abs(i%width-goal%width)+Math.Abs(i/width-goal/width));if(score<bestScore){best=i;bestScore=score;}}
            if(best<0)break;if(best==goal)
            {var path=new List<int>();for(int i=goal;i>=0;i=from[i])path.Add(i);path.Reverse();return path;}
            closed[best]=true;
            for(int d=0;d<4;d++)
            {
                int n=Adjacent(best,d);if(!Interior(n)||closed[n]||sim.Cells[n].Blocked)continue;
                if(landOnly&&sim.Cells[n].River&&!sim.Cells[n].Bridge)continue;
                float move=landOnly?1+sim.Cells[n].Wood*.1f+Math.Abs(sim.Cells[n].Height-sim.Cells[best].Height):
                    .6f+Noise(n%width,n/width,147,.22f)*3+sim.Cells[n].Height*.18f;
                if((n%width==1||n%width==width-2||n/width==1||n/width==height-2)&&n!=start&&n!=goal)move+=12;
                float next=cost[best]+move;if(next<cost[n]){cost[n]=next;from[n]=best;}
            }
        }
        return new List<int>();
    }
    private void Channel(List<int> path)
    {
        foreach(int i in path)
        {
            sim.Cells[i].River=true;
            // Two-cell width, with connected corners; no diagonal water gaps.
            int d=i/width>1&&i/width<height-2?1:0;
            if(path.Count>1){int index=path.IndexOf(i);int next=path[Math.Min(index+1,path.Count-1)];d=next/width!=i/width?0:1;}
            int n=Adjacent(i,d);if(Interior(n))sim.Cells[n].River=true;
        }
    }
    private void Rivers()
    {
        int side=Next(4),endSide=(side+(Next(4)==0?1:2))%4,start=Edge(side),end=Edge(endSide);
        for(int attempt=0;attempt<20&&Math.Abs(start%width-end%width)+Math.Abs(start/width-end/width)<Math.Min(width,height)*.7f;attempt++)end=Edge(endSide);
        int guide=Cell(width/4+Next(width/2),height/4+Next(height/2));
        var main=Route(start,guide);var tail=Route(guide,end);if(tail.Count>0)main.AddRange(tail.GetRange(1,tail.Count-1));Channel(main);
        sim.Cells[start].Source=true;sim.Cells[end].Outlet=true;
        for(int d=0;d<4;d++){int n=Adjacent(end,d);if(Interior(n)&&sim.Cells[n].River)sim.Cells[n].Outlet=true;}
        if(Next(100)<55&&main.Count>12)
        {
            int branch=Edge((side+1+Next(2))%4),join=main[main.Count/3+Next(Math.Max(1,main.Count/3))];
            var path=Route(branch,join);if(path.Count>6){Channel(path);sim.Cells[branch].Source=true;}
        }
        var distances=new int[sim.Cells.Length];for(int i=0;i<distances.Length;i++)distances[i]=-1;
        var queue=new Queue<int>();for(int i=0;i<sim.Cells.Length;i++)if(sim.Cells[i].Outlet){distances[i]=0;queue.Enqueue(i);}
        while(queue.Count>0){int i=queue.Dequeue();for(int d=0;d<4;d++){int n=Adjacent(i,d);if(Interior(n)&&sim.Cells[n].River&&distances[n]<0){distances[n]=distances[i]+1;queue.Enqueue(n);}}}
        for(int i=0;i<sim.Cells.Length;i++)if(sim.Cells[i].River)
        {
            sim.Cells[i].Height=.12f+Math.Max(0,distances[i])*.016f;sim.Cells[i].Water=5;sim.Cells[i].Grass=0;
            for(int d=0;d<4;d++){int n=Adjacent(i,d);if(Interior(n)&&!sim.Cells[n].River)sim.Cells[n].Height=Math.Max(sim.Cells[n].Height,sim.Cells[i].Height+1.65f);}
        }
    }
    private void Lakes()
    {
        int count=Next(3);
        for(int p=0;p<count;p++)
        {
            int cx=4+Next(width-8),cy=4+Next(height-8);float rx=1.4f+Unit()*1.3f,ry=1.1f+Unit()*1.2f;
            for(int y=cy-3;y<=cy+3;y++)for(int x=cx-3;x<=cx+3;x++)
            {
                int i=Cell(x,y);if(!Interior(i)||sim.Cells[i].River)continue;
                if((x-cx)*(x-cx)/(rx*rx)+(y-cy)*(y-cy)/(ry*ry)>1)continue;
                sim.Cells[i].River=true;sim.Cells[i].Height=.2f;sim.Cells[i].Water=4.5f;sim.Cells[i].Grass=0;
            }
        }
    }
    private int PickLand()
    {for(int n=0;n<sim.Cells.Length*2;n++){int i=Next(sim.Cells.Length);if(Interior(i)&&!sim.Cells[i].River&&!sim.Cells[i].Blocked&&!sim.IsBuilding(i))return i;}return -1;}
    private void Resources()
    {
        for(int i=0;i<sim.Cells.Length;i++)if(Interior(i)&&!sim.Cells[i].River)
        {
            float patch=Noise(i%width,i/width,219,.16f);
            if(patch>.49f&&Unit()<.08f+fertility*.22f&&sim.TreeCells3x3(i)<4)
            {sim.Cells[i].Tree=true;sim.Cells[i].Wood=.5f+Unit()*8;sim.Cells[i].Water=Math.Max(sim.Cells[i].Water,2.3f);}
            if(Unit()<.018f)sim.Cells[i].Seeds=1;
        }
        for(int n=2+Next(4);n>0;n--){int i=PickLand();if(i<0)break;sim.Cells[i].Gold=23+Unit()*22;sim.Cells[i].Tree=false;sim.Cells[i].Wood=0;}
        for(int n=1+Next(4);n>0;n--)
        {int i=PickLand();if(i<0)break;sim.Cells[i].Gold=9+Unit()*7;sim.Cells[i].Tree=false;sim.Cells[i].Wood=0;}
        for(int n=Next(4);n>0;n--){int i=PickLand();if(i>=0)sim.Cells[i].Pollution=20+Unit()*45;}
        for(int n=4+Next(9);n>0;n--){int i=PickLand();if(i>=0&&sim.Cells[i].Gold==0){sim.Cells[i].Blocked=true;sim.Cells[i].Tree=false;sim.Cells[i].Wood=0;}}
    }
    private int Root(int[] groups,int i){while(groups[i]!=i){groups[i]=groups[groups[i]];i=groups[i];}return i;}
    private void Bridges()
    {
        var groups=new int[sim.Cells.Length];for(int i=0;i<groups.Length;i++)groups[i]=i;
        for(int i=0;i<groups.Length;i++)if(Interior(i)&&!sim.Cells[i].River&&!sim.Cells[i].Blocked)
            for(int d=0;d<2;d++){int n=Adjacent(i,d);if(Interior(n)&&!sim.Cells[n].River&&!sim.Cells[n].Blocked)groups[Root(groups,n)]=Root(groups,i);}
        var candidates=new List<V1GeneratedBridge>();
        for(int i=0;i<groups.Length;i++)if(Interior(i)&&!sim.Cells[i].River&&!sim.Cells[i].Blocked)
            for(int d=0;d<2;d++)
            {
                int n=Adjacent(i,d);var cells=new List<int>();
                while(Interior(n)&&sim.Cells[n].River&&cells.Count<7){cells.Add(n);n=Adjacent(n,d);}
                if(cells.Count>0&&cells.Count<7&&Interior(n)&&!sim.Cells[n].River&&!sim.Cells[n].Blocked)
                    candidates.Add(new V1GeneratedBridge{cells=cells.ToArray(),horizontal=d==0});
            }
        while(candidates.Count>0&&result.bridges.Count<6)
        {
            int choice=Next(candidates.Count);var b=candidates[choice];candidates.RemoveAt(choice);
            int d=b.horizontal?0:1,a=Adjacent(b.cells[0],d+2),z=Adjacent(b.cells[b.cells.Length-1],d);
            if(Root(groups,a)==Root(groups,z))continue;
            groups[Root(groups,a)]=Root(groups,z);result.bridges.Add(b);
            foreach(int i in b.cells)sim.Cells[i].Bridge=true;
        }
    }
    private void Residents()
    {
        sim.Initialize();var candidates=new List<int>();
        for(int y=3;y<height-4;y++)for(int x=3;x<width-4;x++){int i=Cell(x,y);if(sim.NearRiver(i)&&sim.CanSettle(i))candidates.Add(i);}
        if(candidates.Count==0)throw new InvalidOperationException("Seed has no safe riverside village site: "+seed);
        int center=candidates[Next(candidates.Count)];var village=sim.AddVillage(center,8);result.village=center;
        var safe=new List<int>();
        for(int y=-2;y<=3;y++)for(int x=-2;x<=3;x++){int i=Cell(center%width+x,center/width+y);if(sim.SafeLanding(i)&&sim.Cells[i].Gold<sim.Settings.deadGold)safe.Add(i);}
        if(safe.Count==0)throw new InvalidOperationException("Seed has no safe spawn: "+seed);
        result.spawn=safe[Next(safe.Count)];
        result.tutorial=result.spawn;
        foreach(int i in safe)if(i!=result.spawn&&Math.Abs(i%width-result.spawn%width)+Math.Abs(i/width-result.spawn/width)<=2)
        {result.tutorial=i;break;}
        // Reserve one random teaching plot, never an authored coordinate.
        foreach(int i in new[]{result.spawn,result.tutorial}){sim.Cells[i].Tree=false;sim.Cells[i].Wood=0;sim.Cells[i].Seeds=0;sim.Cells[i].Gold=0;sim.Cells[i].Pollution=0;}
        for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
        {int i=Cell(result.tutorial%width+x,result.tutorial/width+y);if(Interior(i)&&!sim.IsBuilding(i)&&!sim.Cells[i].River&&sim.Cells[i].Tree){sim.Cells[i].Tree=false;sim.Cells[i].Wood=0;}}
        Road(Route(result.spawn,center,true));
        foreach(var b in result.bridges)
        {int d=b.horizontal?0:1;int a=Adjacent(b.cells[0],d+2);Road(Route(result.spawn,a,true));}
        for(int n=0;n<4;n++)
        {int i=safe[Next(safe.Count)];sim.AddAnimal(i,n<2?V1AnimalKind.Cow:V1AnimalKind.Chicken,V1AnimalOrigin.Farmed,village.id);}
        for(int kind=0;kind<4;kind++)
        {
            int home=PickLand();if(home<0)continue;
            sim.AddAnimal(home,(V1AnimalKind)kind,V1AnimalOrigin.Wild);
            int partner=home;for(int d=0;d<4;d++){int i=Adjacent(home,d);if(sim.Walkable(i)){partner=i;break;}}
            sim.AddAnimal(partner,(V1AnimalKind)kind,V1AnimalOrigin.Wild);
        }
        sim.CharacterCell=result.spawn;sim.EvaluateProsperity();
    }
    private void Road(List<int> path)
    {foreach(int i in path)if(!sim.Cells[i].River&&!sim.IsBuilding(i)&&sim.Cells[i].Gold<sim.Settings.deadGold){sim.Cells[i].Grass=.12f;sim.Cells[i].Tree=false;sim.Cells[i].Wood=0;sim.Cells[i].Seeds=0;}}
}
