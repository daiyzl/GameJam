using System;

// Gradient Perlin noise shared by the Unity map and standalone rule checks.
public static class V1Terrain
{
    private static float Fade(float t)=>t*t*t*(t*(t*6-15)+10);
    private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
    private static float Gradient(int x,int y,int seed,float dx,float dy)
    {
        uint h=unchecked((uint)(x*374761393+y*668265263+seed*1442695041));
        h=(h^(h>>13))*1274126177u;h^=h>>16;
        switch(h&7)
        {
            case 0:return dx;case 1:return -dx;case 2:return dy;case 3:return -dy;
            case 4:return (dx+dy)*.7071068f;case 5:return (dx-dy)*.7071068f;
            case 6:return (-dx+dy)*.7071068f;default:return (-dx-dy)*.7071068f;
        }
    }
    public static float Noise(float x,float y,int seed)
    {
        int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);float dx=x-ix,dy=y-iy,u=Fade(dx),v=Fade(dy);
        return V1Simulation.Clamp(.5f+Lerp(Lerp(Gradient(ix,iy,seed,dx,dy),Gradient(ix+1,iy,seed,dx-1,dy),u),
            Lerp(Gradient(ix,iy+1,seed,dx,dy-1),Gradient(ix+1,iy+1,seed,dx-1,dy-1),u),v),0,1);
    }
    public static void Generate(V1Cell[] cells,int width,int height,int originX,int originY,V1Settings settings,int villageX=9,int villageY=8)
    {
        if(!settings.generateTerrain)return;
        float scale=Math.Max(.005f,settings.terrainNoiseScale);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int i=x+y*width;var c=cells[i];
            // Authored river beds, depressions, banks and custom Tile heights take precedence.
            if(c.Blocked||c.River||Math.Abs(c.Height-2.8f)>.001f)continue;
            int wx=x+originX,wy=y+originY;
            float n=Noise((wx+47.3f)*scale,(wy+91.7f)*scale,settings.terrainSeed)*.75f+
                Noise((wx+47.3f)*scale*2,(wy+91.7f)*scale*2,settings.terrainSeed+37)*.25f;
            float generated=V1Simulation.Clamp(2.8f+(n-.5f)*settings.terrainAmplitude,1.4f,6);
            // Blend the tutorial settlement and the existing access road into a usable plain.
            float distance=Math.Max(Math.Abs(wx-villageX),Math.Abs(wy-villageY));
            float blend=V1Simulation.Clamp((distance-4)/4,0,1);
            if(wy>=4&&wy<=5&&wx>=3&&wx<=28)blend=0;
            c.Height=Lerp(2.8f,generated,Fade(blend));cells[i]=c;
        }
    }
    public static int Stage(float height,V1Settings settings)=>height>=settings.mountainHeight?2:height>=settings.slopeHeight?1:0;
    public static string Name(int stage)=>stage==2?"山地":stage==1?"坡地":"平原";
}
