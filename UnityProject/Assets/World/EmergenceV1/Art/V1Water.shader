Shader "RiverbankV1/FlowingWater"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _Color;
            output vert(input v)
            {
                output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy; o.color=v.color*_Color; return o;
            }
            fixed4 frag(output i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                float2 p=floor(i.world*48)/48;
                float wave=sin(p.y*24+sin(p.x*7)*.7-_Time.y*1.5);
                float broken=step(.35,sin(p.x*13+_Time.y*.25));
                float ripple=smoothstep(.92,.99,wave)*broken;
                c.rgb=lerp(c.rgb,c.rgb*.65+fixed3(.14,.20,.18),ripple*.36);
                c.rgb*=c.a; return c;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
