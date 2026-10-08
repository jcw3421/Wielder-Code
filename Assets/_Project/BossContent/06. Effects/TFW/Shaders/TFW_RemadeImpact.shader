Shader "Game/BossContent/TFW/RemadeImpact"
{
    Properties
    {
        [HDR] _Tint("Crimson", Color) = (3.5,0.12,0.025,1)
        _SlashTex("Authored slash mask", 2D) = "black" {}
        _Age("Age", Float) = 0
        _Layer("Layer", Float) = 0
        _Box("Box", Float) = 0
        _Fan("Fan", Float) = 0
        _Donut("Donut", Float) = 0
        _Progress("Warning countdown", Range(0,1)) = 0
        _Size("Hit dimensions", Vector) = (1,0,1,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            TEXTURE2D(_SlashTex); SAMPLER(sampler_SlashTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint,_Size;
            float _Age,_Layer,_Box,_Fan,_Donut,_Progress;
            CBUFFER_END
            V Vert(A a) { V o; o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o; }
            float Line(float distance,float width) {return 1-smoothstep(width,width+max(fwidth(distance),.001),distance);}
            half4 Frag(V i):SV_Target
            {
                float u=i.uv.x, v=i.uv.y;
                float span=_Donut>.5?_Size.x-_Size.y:_Size.x;
                float edge=(1-v)*span;
                if(_Donut>.5)edge=min(edge,v*span);
                if(_Fan>.5)edge=min(edge,sin(min(u,1-u)*2*_Size.z*0.0174533)*v*_Size.x);
                if(_Box>.5)edge=min(min(u,1-u)*2*_Size.x,min(v,1-v)*2*_Size.z);
                float rim=Line(edge,.075);
                if(_Layer>2.5)
                {
                    // Stable full boundary + advancing fill, never a misleading growing hit radius.
                    float fill=1-smoothstep(_Progress-.015,_Progress+.015,v);
                    float front=Line(abs(v-_Progress),.006);
                    float ticks=step(.8,frac((_Box>.5?v:u)*24))*Line(edge,.17);
                    float urgent=smoothstep(.72,1,_Progress);
                    float3 color=lerp(float3(1.5,.48,.035),float3(2.5,.14,.035),urgent);
                    color=lerp(color,float3(3.2,1.8,.7),rim*.6+front*.25);
                    float alpha=.10+fill*.11+rim*.66+ticks*.22+front*.22;
                    return half4(color,saturate(alpha));
                }
                float life=1-smoothstep(.10,.42,_Age);
                float flash=1-smoothstep(.025,.075,_Age);
                float3 ink=float3(.024,.005,.012);
                float3 red=float3(1.7,.075,.035);
                float3 hot=float3(4,2.3,1.5);
                float2 maskUv;
                float coverage;
                if(_Box>.5)
                {
                    maskUv=float2(v,.32+u*.34);
                    coverage=1;
                }
                else
                {
                    // The texture is a straight hand-shaped stroke, wrapped only over a partial arc.
                    maskUv=float2(_Fan>.5?u:u*1.5-.12+_Age*.55,lerp(.19,.72,saturate((v-.32)/.68)));
                    coverage=step(0,maskUv.x)*step(maskUv.x,1)*smoothstep(.3,.38,v);
                }
                float mask=SAMPLE_TEXTURE2D(_SlashTex,sampler_SlashTex,saturate(maskUv)).r*coverage;
                float erosion=smoothstep(.07,.39,_Age)*.60;
                float body=smoothstep(.035+erosion,.12+erosion,mask);
                float core=smoothstep(.72,.96,mask);
                float3 color=lerp(ink,red,smoothstep(.10,.54,mask));
                color=lerp(color,hot,core*(.5+flash*.5));
                color=lerp(color,float3(1,.09,.02),rim*(1-body));
                float footprint=flash*.13+rim*.12*life;
                return half4(color,saturate(body*life+footprint));
            }
            ENDHLSL
        }
    }
}
