using UnityEngine;

public class TutorialWeather : MonoBehaviour
{
    public Sprite streak,splash0,splash1,ripple0,ripple1;
    private ParticleSystem falling,splashes,ripples;
    private Material material;
    public bool IsRaining {get;private set;}
    public int ParticleCount => (falling!=null?falling.particleCount:0)+(splashes!=null?splashes.particleCount:0)+(ripples!=null?ripples.particleCount:0);
    private void Awake()
    {
        // Reuse the sprite shader already included by the 2D scene. Dynamically
        // looked-up 3D particle shaders can be stripped from a release player.
        material=new Material(Shader.Find("Sprites/Default")){mainTexture=streak.texture};
        material.renderQueue=3000;
        falling=Make("像素雨线",new[]{streak},150,.85f,.7f,new Vector2(-1,-10));
        splashes=Make("落地水花",new[]{splash0,splash1},25,.3f,.5f,Vector2.zero);
        ripples=Make("水面涟漪",new[]{ripple0,ripple1},8,.65f,.7f,Vector2.zero);
        StopRain();
    }
    private ParticleSystem Make(string name,Sprite[] sprites,float rate,float lifetime,float size,Vector2 velocity)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.loop=true;main.playOnAwake=false;main.startLifetime=lifetime;main.startSpeed=0;main.startSize=size;main.maxParticles=400;main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=rate;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(5,5,.01f);
        var motion=ps.velocityOverLifetime;motion.enabled=true;motion.space=ParticleSystemSimulationSpace.World;motion.x=velocity.x;motion.y=velocity.y;
        var sheet=ps.textureSheetAnimation;sheet.enabled=true;sheet.mode=ParticleSystemAnimationMode.Sprites;
        foreach(Sprite sprite in sprites)sheet.AddSprite(sprite);sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));sheet.cycleCount=1;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=30;renderer.renderMode=ParticleSystemRenderMode.Billboard;
        return ps;
    }
    public void BeginRain(Vector3 center)
    {
        if(falling==null)return;transform.position=center;falling.transform.localPosition=Vector3.up*4;IsRaining=true;falling.Play();splashes.Play();ripples.Play();
    }
    public void StopRain()
    {IsRaining=false;foreach(var ps in new[]{falling,splashes,ripples})if(ps!=null)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    private void OnDestroy() {if(material!=null)Destroy(material);}
}
