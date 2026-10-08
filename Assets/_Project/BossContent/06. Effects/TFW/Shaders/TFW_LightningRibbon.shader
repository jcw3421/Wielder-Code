Shader "Game/BossContent/TFW/LightningRibbon"
{
    Properties
    {
        [HDR] _BaseColor("Color", Color) = (0.1, 1.5, 6, 1)
        _Intensity("Intensity", Range(0, 8)) = 2
        _Fade("Fade", Range(0, 1)) = 1
        _PulseSpeed("Pulse Speed", Range(0, 40)) = 18
        _CorePower("Core Sharpness", Range(0.5, 12)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "LightningRibbon"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float4 _BaseColor;
                float _Intensity;
                float _Fade;
                float _PulseSpeed;
                float _CorePower;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = saturate(1.0 - abs(input.uv.y * 2.0 - 1.0));
                float core = pow(across, max(0.5, _CorePower));
                float endFade = smoothstep(0.0, 0.025, input.uv.x)
                              * smoothstep(0.0, 0.06, 1.0 - input.uv.x);
                float pulse = 0.78 + 0.22 * sin(_Time.y * _PulseSpeed + input.uv.x * 29.0);
                float energy = core * endFade * pulse * _Fade * input.color.a;
                return half4(_BaseColor.rgb * input.color.rgb * (_Intensity * energy), energy);
            }
            ENDHLSL
        }
    }
}
