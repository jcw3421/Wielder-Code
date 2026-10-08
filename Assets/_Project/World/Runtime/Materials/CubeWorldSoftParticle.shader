// Textureless particle shader: the round falloff is derived from the particle UV so
// the ambience VFX pull in no texture assets of their own.
Shader "Game/CubeWorld/SoftParticle"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Softness("Edge Softness", Range(0, 8)) = 2.0
        _Intensity("Intensity", Range(0, 8)) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10
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

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Softness;
                half _Intensity;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float  fogCoord   : TEXCOORD1;
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
                output.fogCoord = ComputeFogFactor(vpi.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = saturate(length(input.uv - 0.5) * 2.0);

                // _Softness 0 means "flat, no falloff" (used by the blocky smoke cubes).
                // It must not reach pow(), because at the corners the base is 0 too and
                // pow(0, 0) is undefined — some drivers return NaN, which shows up as
                // black/white garbage instead of a particle.
                half falloff = _Softness > 0.0 ? pow(saturate(1.0 - d), _Softness) : 1.0;

                half4 color = _BaseColor * input.color;
                color.a *= falloff;
                color.rgb *= _Intensity;

                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
