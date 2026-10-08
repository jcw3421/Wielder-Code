// Volumetric-looking chimney smoke without a texture asset.
//
// Three things do the work that a flipbook would normally do:
//   - fBm value noise erodes the billboard disc into wisps, and the erosion grows with
//     particle age so a puff dissolves instead of fading out as a circle;
//   - a spherical billboard normal is lit by the main light, so puffs have a bright side
//     and a shadowed side instead of reading as flat decals;
//   - a depth soft-fade stops the quads from slicing visibly into roofs.
//
// Per-particle age and seed arrive through custom vertex streams packed into TEXCOORD0.zw
// (see LobbyAmbience.CreateSmoke), which is what keeps every puff from eroding identically.
Shader "Game/CubeWorld/Smoke"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.72, 0.72, 0.70, 1)
        _ShadowColor("Shadow Color", Color) = (0.30, 0.32, 0.36, 1)
        _LightInfluence("Light Influence", Range(0, 2)) = 0.9
        _AmbientInfluence("Ambient Influence", Range(0, 2)) = 1.0

        _NoiseScale("Noise Scale", Range(0.5, 12)) = 3.2
        _NoiseScroll("Noise Scroll", Range(0, 3)) = 0.55
        _ErosionMin("Erosion At Birth", Range(0, 2)) = 0.35
        _ErosionMax("Erosion At Death", Range(0, 2)) = 1.15
        _Contrast("Edge Contrast", Range(0, 3)) = 0.65
        _EdgeSoftness("Edge Softness", Range(0.01, 1)) = 0.42

        _SoftFadeDistance("Soft Fade Distance", Range(0.01, 4)) = 0.9
        [Toggle(_SOFTFADE_ON)] _SoftFade("Depth Soft Fade", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ _SOFTFADE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half _LightInfluence;
                half _AmbientInfluence;
                half _NoiseScale;
                half _NoiseScroll;
                half _ErosionMin;
                half _ErosionMax;
                half _Contrast;
                half _EdgeSoftness;
                half _SoftFadeDistance;
                float _SoftFade;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * ValueNoise(p);
                    p *= 2.03;
                    amplitude *= 0.5;
                }
                return value;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                // xy = quad uv, z = age percent, w = stable random (custom vertex streams)
                float4 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float4 screenPos  : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vpi.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                output.screenPos = ComputeScreenPos(vpi.positionCS);
                output.fogCoord = ComputeFogFactor(vpi.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centred = input.uv.xy * 2.0 - 1.0;
                float agePercent = saturate(input.uv.z);
                float seed = input.uv.w;

                float radius = length(centred);
                float disc = saturate(1.0 - radius);

                // Scroll the noise upward over the particle's life so the wisps crawl.
                float2 noiseUV = input.uv.xy * _NoiseScale + seed * 41.7;
                noiseUV += float2(_NoiseScroll * agePercent * 0.6, -_NoiseScroll * agePercent);
                float noise = Fbm(noiseUV);

                float erosion = lerp(_ErosionMin, _ErosionMax, agePercent);
                float mask = disc * (1.0 + _Contrast) - noise * erosion;
                mask = smoothstep(0.0, _EdgeSoftness, mask);

                if (mask <= 0.001) discard;

                // Treat the billboard as a sphere so the puff catches light on one side.
                float3 normalVS = float3(centred, sqrt(saturate(1.0 - saturate(radius * radius))));
                float3 normalWS = normalize(mul((float3x3)UNITY_MATRIX_I_V, normalVS));

                Light mainLight = GetMainLight();
                half wrapped = saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5);

                half3 ambient = SampleSH(normalWS) * _AmbientInfluence;
                half3 body = lerp(_ShadowColor.rgb, _BaseColor.rgb, wrapped);
                half3 lit = body * (ambient + mainLight.color * wrapped * _LightInfluence);

                half alpha = mask * input.color.a * _BaseColor.a;

                #if defined(_SOFTFADE_ON)
                    float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float particleEye = input.screenPos.w;
                    alpha *= saturate((sceneEye - particleEye) / _SoftFadeDistance);
                #endif

                half3 color = lit * input.color.rgb;
                color = MixFog(color, input.fogCoord);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
