Shader "UI/OrnateGoldFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Tone)]
        _BlackLift ("Black Lift", Range(0, 0.4)) = 0.12
        _Gamma ("Gamma", Range(0.4, 2.5)) = 0.85
        _Saturation ("Saturation", Range(0, 2)) = 0.9

        [Header(Gold Ramp)]
        _RampAmount ("Ramp Amount", Range(0, 1)) = 0.85
        _RampShadow ("Shadow", Color) = (0.16, 0.10, 0.07, 1)
        _RampMid ("Mid (bronze)", Color) = (0.52, 0.36, 0.18, 1)
        _RampHigh ("High (gold)", Color) = (0.90, 0.74, 0.38, 1)
        _RampSpecular ("Specular (cream)", Color) = (1.0, 0.97, 0.86, 1)
        _RampMidPoint ("Ramp Mid Point", Range(0.1, 0.9)) = 0.4
        _RampHighPoint ("Ramp High Point", Range(0.1, 0.99)) = 0.75

        [Header(Directional Light)]
        _LightAngle ("Light Angle (deg)", Range(0, 360)) = 90
        _GradientStrength ("Surface Gradient", Range(0, 0.6)) = 0.18

        [Header(Bevel Edge)]
        _BevelWidth ("Bevel Width (texels)", Range(0, 6)) = 2
        _BevelLight ("Bevel Light", Range(0, 3)) = 1.1
        _BevelShadow ("Bevel Shadow", Range(0, 1)) = 0.45
        _BevelLightColor ("Bevel Light Color", Color) = (1.0, 0.95, 0.78, 1)

        [Header(Silk Sweep)]
        _SweepColor ("Sweep Color", Color) = (1, 0.98, 0.88, 1)
        _SweepIntensity ("Sweep Intensity", Range(0, 2)) = 0.35
        _SweepWidth ("Sweep Width", Range(0.02, 0.6)) = 0.28
        _SweepSpeed ("Sweep Speed", Range(-1, 1)) = 0.12
        _SweepAngle ("Sweep Angle (deg)", Range(0, 180)) = 65
        _SweepMaskMin ("Sweep Luminance Mask", Range(0, 1)) = 0.45

        [Header(Soft Bloom)]
        _BloomColor ("Bloom Color", Color) = (1.0, 0.92, 0.70, 1)
        _BloomIntensity ("Bloom Intensity", Range(0, 2)) = 0.35
        _BloomThreshold ("Bloom Threshold", Range(0, 1)) = 0.7

        [Header(Element Glow)]
        _ElementColor ("Element Color", Color) = (0.42, 0.72, 1.0, 1)
        _ElementIntensity ("Element Intensity", Range(0, 4)) = 0
        _ElementThreshold ("Element Saturation Threshold", Range(0, 1)) = 0.4
        _ElementPulse ("Element Pulse Amount", Range(0, 1)) = 0
        _ElementPulseSpeed ("Element Pulse Speed", Range(0, 10)) = 2

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            float _BlackLift;
            float _Gamma;
            float _Saturation;

            float _RampAmount;
            fixed4 _RampShadow;
            fixed4 _RampMid;
            fixed4 _RampHigh;
            fixed4 _RampSpecular;
            float _RampMidPoint;
            float _RampHighPoint;

            float _LightAngle;
            float _GradientStrength;

            float _BevelWidth;
            float _BevelLight;
            float _BevelShadow;
            fixed4 _BevelLightColor;

            fixed4 _SweepColor;
            float _SweepIntensity;
            float _SweepWidth;
            float _SweepSpeed;
            float _SweepAngle;
            float _SweepMaskMin;

            fixed4 _BloomColor;
            float _BloomIntensity;
            float _BloomThreshold;

            fixed4 _ElementColor;
            float _ElementIntensity;
            float _ElementThreshold;
            float _ElementPulse;
            float _ElementPulseSpeed;

            float Luminance01(float3 rgb)
            {
                return dot(rgb, float3(0.299, 0.587, 0.114));
            }

            float2 LightDirection()
            {
                float radians = _LightAngle * UNITY_PI / 180.0;
                return float2(cos(radians), sin(radians));
            }

            // 4단 금색 램프. 그림자→청동→금→크림으로 이어지는 매끄러운 보간이라
            // 계단이 지지 않는다 — 원신 UI 금테의 핵심은 하드 컷이 아니라 이 부드러운 전이다.
            float3 GoldRamp(float luminance)
            {
                float mid = _RampMidPoint;
                float high = max(_RampHighPoint, mid + 1e-3);

                if (luminance < mid)
                    return lerp(_RampShadow.rgb, _RampMid.rgb, luminance / mid);

                if (luminance < high)
                    return lerp(_RampMid.rgb, _RampHigh.rgb, (luminance - mid) / (high - mid));

                return lerp(_RampHigh.rgb, _RampSpecular.rgb, (luminance - high) / max(1.0 - high, 1e-3));
            }

            // 알파 기울기의 방향을 광원 방향과 비교해 양각을 만든다.
            // 광원 쪽 경계는 밝게, 반대쪽 경계는 어둡게 — 금속을 깎아낸 듯한 입체감이 나온다.
            // 사방을 균일하게 밝히면 크롬 실루엣처럼 보이므로 방향을 반드시 준다.
            float BevelFactor(float2 uv, float baseAlpha)
            {
                float2 offset = _MainTex_TexelSize.xy * _BevelWidth;

                float left  = tex2D(_MainTex, uv - float2(offset.x, 0)).a;
                float right = tex2D(_MainTex, uv + float2(offset.x, 0)).a;
                float down  = tex2D(_MainTex, uv - float2(0, offset.y)).a;
                float up    = tex2D(_MainTex, uv + float2(0, offset.y)).a;

                float2 gradient = float2(left - right, down - up);
                float strength = saturate(length(gradient));

                // 기울기가 거의 없는 평평한 안쪽은 양각 계산에서 제외한다.
                if (strength < 1e-4) return 0.0;

                float facing = dot(gradient / strength, LightDirection());

                // baseAlpha를 곱해 실루엣 안쪽에만 남긴다.
                return facing * strength * baseAlpha;
            }

            // 넓고 느린 광택 띠. 젠존제식 날카로운 섬광과 달리 부드럽게 지나간다.
            float SilkSweep(float2 uv, float luminance)
            {
                float radians = _SweepAngle * UNITY_PI / 180.0;
                float projected = uv.x * cos(radians) + uv.y * sin(radians);

                float travel = projected - _Time.y * _SweepSpeed;
                float distanceToBand = abs(frac(travel) - 0.5);

                // smoothstep을 제곱해 가장자리를 더 부드럽게 떨군다.
                float band = smoothstep(_SweepWidth, 0.0, distanceToBand);
                band *= band;

                return band * smoothstep(_SweepMaskMin, 1.0, luminance);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
                float baseAlpha = source.a;

                // 검정을 들어올린다 — 원신 UI에는 순수 검정이 거의 없다.
                // 어두운 아트를 램프에 그대로 넣으면 전부 그림자색으로 뭉치므로 여기서 펴준다.
                float3 rgb = lerp(_BlackLift.xxx, 1.0.xxx, source.rgb);
                rgb = pow(max(rgb, 1e-4), _Gamma);
                rgb = lerp(Luminance01(rgb).xxx, rgb, _Saturation);

                float luminance = saturate(Luminance01(rgb));
                rgb = lerp(rgb, GoldRamp(luminance), _RampAmount);

                // 면 전체에 광원 방향 그라데이션 — 위쪽이 밝은 금속판 느낌.
                float2 lightDirection = LightDirection();
                float projected = dot(i.uv - 0.5, lightDirection) + 0.5;
                rgb *= lerp(1.0 - _GradientStrength, 1.0 + _GradientStrength, saturate(projected));

                // 양각. 밝은 쪽은 크림색을 더하고 어두운 쪽은 곱해서 눌러야 선이 탁해지지 않는다.
                float bevel = BevelFactor(i.uv, baseAlpha);
                rgb *= lerp(1.0, 1.0 - _BevelShadow, saturate(-bevel));
                rgb += _BevelLightColor.rgb * (saturate(bevel) * _BevelLight);

                float litLuminance = Luminance01(rgb);

                // 더해지는 항은 모두 알파를 곱한다 — 안 그러면 투명 영역에 빛무리가 남는다.
                float sweep = SilkSweep(i.uv, litLuminance) * _SweepIntensity * baseAlpha;
                rgb += _SweepColor.rgb * sweep;

                float bloom = smoothstep(_BloomThreshold, 1.0, litLuminance);
                rgb += _BloomColor.rgb * (bloom * _BloomIntensity * baseAlpha);

                // 원본에서 채도가 높던 부분(문양)만 원소색으로 발광시킨다. 금테는 반응하지 않는다.
                float maxChannel = max(source.r, max(source.g, source.b));
                float minChannel = min(source.r, min(source.g, source.b));
                float saturation = (maxChannel - minChannel) / max(maxChannel, 1e-4);

                float pulse = lerp(1.0, 0.5 + 0.5 * sin(_Time.y * _ElementPulseSpeed), _ElementPulse);
                float element = smoothstep(_ElementThreshold, 1.0, saturation) * maxChannel;
                rgb += _ElementColor.rgb * (element * _ElementIntensity * pulse * baseAlpha);

                fixed4 color = fixed4(saturate(rgb), baseAlpha) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
