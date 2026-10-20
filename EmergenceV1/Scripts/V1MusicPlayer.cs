using UnityEngine;

// One persistent, non-spatial music player also covers the menu and scene transitions.
public sealed class V1MusicPlayer : MonoBehaviour
{
    public AudioClip[] tracks;
    [Range(0,1)] public float volume = .38f;
    [Range(.2f,8)] public float fadeSeconds = 2.5f;
    public static V1MusicPlayer Instance { get; private set; }
    public int CurrentTrack { get; private set; } = -1;
    public AudioClip CurrentClip => CurrentTrack >= 0 && tracks != null && CurrentTrack < tracks.Length ? tracks[CurrentTrack] : null;
    private readonly AudioSource[] sources = new AudioSource[2];
    private readonly float[] levels = new float[2];
    private int active;
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        for (int i=0;i<2;i++)
        {
            sources[i]=gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake=false;sources[i].loop=true;sources[i].spatialBlend=0;
            sources[i].volume=0;sources[i].priority=64;
        }
    }
    public static int TrackFor(V1Season season,V1PhaseEvent phaseEvent)
    {
        if(phaseEvent==V1PhaseEvent.Thunderstorm)return 4;
        if(phaseEvent==V1PhaseEvent.Snow)return 5;
        if(phaseEvent==V1PhaseEvent.Wind)return 6;
        return Mathf.Clamp((int)season,0,3);
    }
    public static void Play(V1Season season,V1PhaseEvent phaseEvent=V1PhaseEvent.None)
    {
        // Resources fallback lets an existing authored scene work without rebuilding it.
        if(Instance==null)
        {
            var player=new GameObject("四季与事件音乐").AddComponent<V1MusicPlayer>();
            string[] names={"spring","summer","autumn","winter","thunderstorm","snow","wind"};
            player.tracks=new AudioClip[names.Length];
            for(int i=0;i<names.Length;i++)player.tracks[i]=Resources.Load<AudioClip>("RiverbankMusic/"+names[i]);
        }
        Instance.Select(TrackFor(season,phaseEvent));
    }
    private void Select(int track)
    {
        if(track==CurrentTrack){pending=-1;return;}
        if(tracks==null||track<0||track>=tracks.Length||tracks[track]==null)return;
        // Reuse an outgoing voice on rapid changes, avoiding clicks and repeated restarts.
        int next=1-active;
        if(sources[next].clip!=tracks[track])
        {
            if(levels[next]>.001f) { pending=track; return; }
            sources[next].Stop();sources[next].clip=tracks[track];sources[next].Play();
        }
        else if(!sources[next].isPlaying)sources[next].Play();
        active=next;CurrentTrack=track;pending=-1;
    }
    private int pending=-1;
    private void Update()
    {
        for(int i=0;i<2;i++)
        {
            levels[i]=Mathf.MoveTowards(levels[i],i==active?1:0,Time.unscaledDeltaTime/Mathf.Max(.2f,fadeSeconds));
            sources[i].volume=levels[i]*volume;
            if(i!=active&&levels[i]<=0&&sources[i].isPlaying)sources[i].Stop();
        }
        if(pending>=0&&levels[1-active]<=.001f)Select(pending);
    }
    private void OnDestroy(){if(Instance==this)Instance=null;}
}
