Shader "RiverbankV1/ConnectedTerrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PlainTex ("Plain surface", 2D) = "white" {}
        _SlopeTex ("Slope surface", 2D) = "white" {}
        _HighTex ("Highland surface", 2D) = "white" {}
        _HeightMap ("Height and vegetation", 2D) = "white" {}
        _MapRect ("Map origin and dimensions", Vector) = (0,0,40,28)
        _Thresholds ("Slope, highland and blend widths", Vector) = (3.2,4.25,.45,.55)
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct output { float4 vertex:SV_POSITION; float2 world:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _PlainTex,_SlopeTex,_HighTex,_HeightMap;
            float4 _MapRect,_Thresholds;
            fixed4 _Color;
            output vert(input v)
            {
                output o;o.vertex=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color*_Color;return o;
            }
            fixed4 frag(output i):SV_Target
            {
                float2 uv=(i.world-_MapRect.xy)/_MapRect.zw;
                float2 cell=1/_MapRect.zw;
                float2 data=tex2D(_HeightMap,uv).rg;
                float slope=smoothstep(_Thresholds.x-_Thresholds.z,_Thresholds.x+_Thresholds.z,data.r);
                float high=smoothstep(_Thresholds.y-_Thresholds.w,_Thresholds.y+_Thresholds.w,data.r);
                // World coordinates give adjacent sprites exactly the same texture phase.
                float2 surface=floor(i.world*64)/256;
                fixed3 plain=tex2D(_PlainTex,surface).rgb;
                fixed3 hill=tex2D(_SlopeTex,surface).rgb;
                fixed3 rock=tex2D(_HighTex,surface).rgb;
                fixed3 color=lerp(lerp(plain,hill,slope),rock,high);
                float vegetation=saturate(data.g/1.5);
                fixed3 bare=dot(plain,fixed3(.3,.5,.2))*fixed3(1.12,.86,.55);
                color=lerp(bare,color,lerp(vegetation,1,saturate(slope*.7+high)));
                float dx=tex2D(_HeightMap,uv-cell*float2(.5,0)).r-tex2D(_HeightMap,uv+cell*float2(.5,0)).r;
                float dy=tex2D(_HeightMap,uv-cell*float2(0,.5)).r-tex2D(_HeightMap,uv+cell*float2(0,.5)).r;
                color*=clamp(1+(dx-dy)*.13,.78,1.13);
                return fixed4(color*i.color.rgb*i.color.a,i.color.a);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
