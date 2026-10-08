Shader "Game/BossContent/TFW/LightningDecal"
{
    Properties
    {
        _BaseMap("Fracture Mask", 2D) = "white" {}
        [HDR] _BaseColor("Color", Color) = (0.08, 1.2, 5, 1)
        _Intensity("Intensity", Range(0, 8)) = 2
        _Fade("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GroundConformingDecal"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Intensity;
                float _Fade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float radial = saturate(1.0 - length(input.uv - 0.5) * 1.72);
                float alpha = max(mask.a, max(mask.r, max(mask.g, mask.b))) * radial;
                alpha *= _Fade * input.color.a;
                return half4(_BaseColor.rgb * input.color.rgb * (_Intensity * alpha), alpha);
            }
            ENDHLSL
        }
    }
}
