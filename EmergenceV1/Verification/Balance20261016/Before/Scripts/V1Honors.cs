using System.Collections.Generic;

public sealed class V1Honor
{
    public readonly string Id, Name, Condition;
    public V1Honor(string id, string name, string condition) { Id=id;Name=name;Condition=condition; }
}

public static class V1Honors
{
    public static readonly V1Honor[] All = {
        new V1Honor("strategist","运筹帷幄","本局玩家从未移动，并在结束时达成持续繁荣。"),
        new V1Honor("guardian","自然守护者","结束时达成持续繁荣，且全图没有污染和死地。"),
        new V1Honor("unassisted","无为而治","本局未投放元素、调整地形或手动安排降雨，并在结束时达成持续繁荣。"),
        new V1Honor("wilderness","山野自荣","结束时没有存活村庄，生态仍达成持续繁荣。"),
        new V1Honor("planter","植树成荫","本局通过玩家播种成功培育至少12棵树苗。"),
        new V1Honor("wildlife","生灵繁盛","结束时至少有12只存活野生动物，且包含至少3种。"),
        new V1Honor("settlements","四方烟火","结束时至少有3座存活村庄，总人口至少48人。"),
        new V1Honor("century","岁月长流","本局累计渡过至少100天，即300个时段。")
    };
    public static List<V1Honor> Evaluate(V1Simulation s, bool hasPlaced, bool hasMoved)
    {
        var result=new List<V1Honor>();
        bool prosperous=s.IsProsperous, clean=true;
        for(int i=0;i<s.Cells.Length;i++)if(s.Valid(i))
        {var c=s.Cells[i];if(c.Pollution>.001f||c.Dead(s.Settings.deadGold,s.Settings.mineGold)){clean=false;break;}}
        int wild=0,species=0;var kinds=new bool[4];
        foreach(var a in s.Animals)if(a.alive&&a.origin==V1AnimalOrigin.Wild){wild++;kinds[(int)a.kind]=true;}
        foreach(bool kind in kinds)if(kind)species++;
        bool[] earned={prosperous&&!hasMoved,prosperous&&clean,prosperous&&!hasPlaced,
            prosperous&&s.ActiveVillages==0,s.PlayerTreeBirths>=12,wild>=12&&species>=3,
            s.ActiveVillages>=3&&s.TotalPopulation>=48,s.AdvanceCount>=300};
        for(int i=0;i<All.Length;i++)if(earned[i])result.Add(All[i]);
        return result;
    }
}
