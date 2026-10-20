using UnityEngine;

// Small ground-level dust, with a capped particle count. It carries no simulation state.
public sealed class V1DeadLandEffect : MonoBehaviour
{
    private ParticleSystem dust;
    private Material material;
    public void Configure(float pollution)
    {
        if(dust==null)
        {
            dust=gameObject.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=dust.main;main.loop=true;main.duration=4;main.startLifetime=new ParticleSystem.MinMaxCurve(1.8f,3.2f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.015f,.07f);main.startSize=new ParticleSystem.MinMaxCurve(.04f,.10f);
            main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var shape=dust.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.82f,.7f,0);
            var velocity=dust.velocityOverLifetime;velocity.enabled=true;
            velocity.x=new ParticleSystem.MinMaxCurve(-.025f,.025f);
            velocity.y=new ParticleSystem.MinMaxCurve(.025f,.08f);
            velocity.z=new ParticleSystem.MinMaxCurve(0,0);
            var color=dust.colorOverLifetime;color.enabled=true;
            var fade=new Gradient();
            fade.SetKeys(new[]{new GradientColorKey(new Color(.35f,.38f,.26f),0),new GradientColorKey(new Color(.48f,.43f,.33f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.28f,.2f),new GradientAlphaKey(0,1)});color.color=fade;
            var renderer=dust.GetComponent<ParticleSystemRenderer>();
            material=new Material(Shader.Find("Sprites/Default"));
            renderer.sharedMaterial=material;renderer.sortingOrder=7;
            dust.Play();
        }
        var emission=dust.emission;
        emission.rateOverTime=Mathf.Lerp(2,5,Mathf.Clamp01(pollution/60));
    }
    private void OnDestroy(){if(material!=null)Destroy(material);}
}
