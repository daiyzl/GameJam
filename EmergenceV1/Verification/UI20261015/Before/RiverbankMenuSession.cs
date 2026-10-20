using UnityEngine;

public enum RiverbankSeason { Spring, Summer, Autumn, Winter, Random }

public static class RiverbankMenuSession
{
    public static RiverbankSeason SelectedSeason { get; private set; } = RiverbankSeason.Autumn;
    public static RiverbankSeason InitialSeason { get; private set; } = RiverbankSeason.Autumn;
    public static bool StartedFromMenu { get; private set; }
    public const string HonorKey="Riverbank.Honor.Guardian";
    public static readonly string[] SeasonNames={"春","夏","秋","冬","随机"};
    public static void Select(RiverbankSeason season)=>SelectedSeason=season;
    public static void BeginGame()
    {
        InitialSeason=SelectedSeason==RiverbankSeason.Random?(RiverbankSeason)Random.Range(0,4):SelectedSeason;
        StartedFromMenu=true;
    }
    public static void RecordTeachingWin()
    {
        if(PlayerPrefs.GetInt(HonorKey,0)>0)return;
        PlayerPrefs.SetInt(HonorKey,1);PlayerPrefs.Save();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession(){SelectedSeason=InitialSeason=RiverbankSeason.Autumn;StartedFromMenu=false;}
}
