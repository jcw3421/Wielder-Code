Shader "Game/BossContent/TFW Outer Aura Particle"
{
    Properties
    {
        [HDR] _RedColor("Red Core", Color) = (3.5, 0.015, 0.008, 1)
        _BlackColor("Black Smoke", Color) = (0.002, 0, 0.001, 1)
        _Density("Density", Range(0, 2)) = 1
        _Erosion("Edge Erosion", Range(0, 1)) = 0.42
        _NoiseScale("Noise Scale", Range(0.5, 12)) = 4.2
        _FlowSpeed("Flow Speed", Range(0, 4)) = 0.75
        _RedAmount("Red Amount", Range(0, 1)) = 0.32
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+20"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "OuterAura"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RedColor;
                half4 _BlackColor;
                half _Density;
                half _Erosion;
                half _NoiseScale;
                half _FlowSpeed;
                half _RedAmount;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.55;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amp;
                    p = mul(float2x2(1.52, -1.18, 1.18, 1.52), p) + 7.1;
                    amp *= 0.5;
                }
                return sum;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                centered.x *= 0.82;
                float radial = saturate(1.0 - dot(centered, centered));

                float time = _Time.y * _FlowSpeed;
                float2 flowUv = input.uv * _NoiseScale + float2(sin(time * 0.37), -time);
                float warp = Fbm(flowUv * 0.62 + 11.7);
                float smokeNoise = Fbm(flowUv + (warp - 0.5) * 2.4);
                float tornEdge = smoothstep(_Erosion - 0.18, _Erosion + 0.2,
                                            radial * 0.78 + smokeNoise * 0.52);

                float wisps = smoothstep(0.48, 0.82, smokeNoise + radial * 0.22);
                float redCore = smoothstep(1.0 - _RedAmount, 1.0, wisps * radial);
                redCore *= smoothstep(0.18, 0.58, radial);

                half3 color = lerp(_BlackColor.rgb, _RedColor.rgb, redCore);
                color += _RedColor.rgb * pow(saturate(redCore), 3.0) * 0.35;

                half alpha = tornEdge * radial * _Density;
                alpha *= lerp(0.48, 0.82, wisps) * input.color.a;
                alpha *= smoothstep(0.0, 0.16, alpha);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
