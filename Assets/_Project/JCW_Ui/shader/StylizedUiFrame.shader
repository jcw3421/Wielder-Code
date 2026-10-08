Shader "UI/StylizedFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Tone)]
        _Brightness ("Brightness", Range(-0.5, 0.5)) = 0
        _Contrast ("Contrast", Range(0.5, 2.5)) = 1.1
        _ContrastPivot ("Contrast Pivot", Range(0, 1)) = 0.35
        _Saturation ("Saturation", Range(0, 3)) = 1.2
        _HueShift ("Hue Shift", Range(0, 1)) = 0

        [Header(Posterize)]
        _PosterizeSteps ("Posterize Steps", Range(2, 24)) = 6
        _PosterizeAmount ("Posterize Amount", Range(0, 1)) = 0.7

        [Header(Gradient Map)]
        _RampAmount ("Ramp Amount", Range(0, 1)) = 0
        _RampShadow ("Ramp Shadow", Color) = (0.09, 0.11, 0.20, 1)
        _RampMid ("Ramp Mid", Color) = (0.55, 0.42, 0.28, 1)
        _RampHigh ("Ramp High", Color) = (1.0, 0.94, 0.75, 1)

        [Header(Edge Light)]
        _EdgeColor ("Edge Color", Color) = (1, 0.92, 0.7, 1)
        _EdgeWidth ("Edge Width (texels)", Range(0, 6)) = 1.5
        _EdgeIntensity ("Edge Intensity", Range(0, 4)) = 1.2

        [Header(Metal Sweep)]
        _SweepColor ("Sweep Color", Color) = (1,1,1,1)
        _SweepIntensity ("Sweep Intensity", Range(0, 3)) = 0
        _SweepWidth ("Sweep Width", Range(0.01, 0.5)) = 0.12
        _SweepSpeed ("Sweep Speed", Range(-2, 2)) = 0.3
        _SweepAngle ("Sweep Angle (deg)", Range(0, 180)) = 60
        _SweepMaskMin ("Sweep Luminance Mask", Range(0, 1)) = 0.35

        [Header(Emblem Glow)]
        _GlowColor ("Glow Color", Color) = (1, 0.2, 0.15, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 0
        _GlowThreshold ("Glow Saturation Threshold", Range(0, 1)) = 0.45
        _GlowPulse ("Glow Pulse Amount", Range(0, 1)) = 0
        _GlowPulseSpeed ("Glow Pulse Speed", Range(0, 12)) = 3

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

            float _Brightness;
            float _Contrast;
            float _ContrastPivot;
            float _Saturation;
            float _HueShift;

            float _PosterizeSteps;
            float _PosterizeAmount;

            float _RampAmount;
            fixed4 _RampShadow;
            fixed4 _RampMid;
            fixed4 _RampHigh;

            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeIntensity;

            fixed4 _SweepColor;
            float _SweepIntensity;
            float _SweepWidth;
            float _SweepSpeed;
            float _SweepAngle;
            float _SweepMaskMin;

            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowThreshold;
            float _GlowPulse;
            float _GlowPulseSpeed;

            float Luminance01(float3 rgb)
            {
                return dot(rgb, float3(0.299, 0.587, 0.114));
            }

            float3 RgbToHsv(float3 rgb)
            {
                float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(rgb.bg, k.wz), float4(rgb.gb, k.xy), step(rgb.b, rgb.g));
                float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-5)), d / (q.x + 1e-5), q.x);
            }

            float3 HsvToRgb(float3 hsv)
            {
                float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(hsv.xxx + k.xyz) * 6.0 - k.www);
                return hsv.z * lerp(k.xxx, saturate(p - k.xxx), hsv.y);
            }

            // 채도를 회색(휘도)과 원색 사이 보간으로 조절. 1 = 원본.
            float3 ApplySaturation(float3 rgb, float amount)
            {
                return lerp(Luminance01(rgb).xxx, rgb, amount);
            }

            // 밝기만 계단으로 끊고 색상비는 유지한다.
            // RGB를 채널별로 끊으면 색이 틀어지고 어두운 부분이 전부 검정으로 뭉친다.
            float3 ApplyPosterize(float3 rgb, float steps, float amount)
            {
                float levels = max(1.0, steps - 1.0);
                float luminance = Luminance01(rgb);
                float stepped = round(luminance * levels) / levels;

                // 원래 밝기 대비 계단 밝기의 비율만 곱해 색상은 그대로 둔다.
                float3 recolored = rgb * (stepped / max(luminance, 1e-4));
                return lerp(rgb, recolored, amount);
            }

            // 휘도를 3색 램프(그림자→중간→하이라이트)로 다시 칠한다.
            // 스타일라이즈 금속의 핵심 — 사진 같은 질감이 도색된 느낌으로 바뀐다.
            float3 ApplyGradientMap(float3 rgb, float amount)
            {
                float luminance = saturate(Luminance01(rgb));

                float3 ramp = luminance < 0.5
                    ? lerp(_RampShadow.rgb, _RampMid.rgb, luminance * 2.0)
                    : lerp(_RampMid.rgb, _RampHigh.rgb, (luminance - 0.5) * 2.0);

                return lerp(rgb, ramp, amount);
            }

            // 알파 기울기로 실루엣 경계를 뽑는다 — 손그림 외곽선 느낌.
            float SilhouetteEdge(float2 uv, float baseAlpha)
            {
                float2 offset = _MainTex_TexelSize.xy * _EdgeWidth;

                float left  = tex2D(_MainTex, uv - float2(offset.x, 0)).a;
                float right = tex2D(_MainTex, uv + float2(offset.x, 0)).a;
                float down  = tex2D(_MainTex, uv - float2(0, offset.y)).a;
                float up    = tex2D(_MainTex, uv + float2(0, offset.y)).a;

                float gradient = abs(right - left) + abs(up - down);

                // baseAlpha를 곱해 실루엣 안쪽에만 남긴다(바깥으로 번지면 UI가 지저분해진다).
                return saturate(gradient) * baseAlpha;
            }

            // 밝은 금속 위로 흐르는 광택 띠. 투명·어두운 부분은 마스크로 제외한다.
            float MetalSweep(float2 uv, float luminance)
            {
                float radians = _SweepAngle * UNITY_PI / 180.0;
                float projected = uv.x * cos(radians) + uv.y * sin(radians);

                float travel = projected - _Time.y * _SweepSpeed;
                float distanceToBand = abs(frac(travel) - 0.5);

                float band = smoothstep(_SweepWidth, 0.0, distanceToBand);
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

                float3 rgb = source.rgb;

                // 대비 피벗을 원본 밝기대에 맞춰야 어두운 아트가 검정으로 눌리지 않는다.
                rgb = (rgb - _ContrastPivot) * _Contrast + _ContrastPivot + _Brightness;
                rgb = saturate(rgb);

                if (_HueShift > 0.0)
                {
                    float3 hsv = RgbToHsv(rgb);
                    hsv.x = frac(hsv.x + _HueShift);
                    rgb = HsvToRgb(hsv);
                }

                rgb = saturate(ApplySaturation(rgb, _Saturation));
                rgb = ApplyPosterize(rgb, _PosterizeSteps, _PosterizeAmount);
                rgb = ApplyGradientMap(rgb, _RampAmount);

                float luminance = Luminance01(rgb);

                // 더해지는 항은 모두 알파를 곱한다 — 안 그러면 투명 영역에 빛무리가 남는다.
                float edge = SilhouetteEdge(i.uv, baseAlpha) * _EdgeIntensity;
                rgb += _EdgeColor.rgb * edge;

                float sweep = MetalSweep(i.uv, luminance) * _SweepIntensity * baseAlpha;
                rgb += _SweepColor.rgb * sweep;

                // 채도가 높은 픽셀(문양의 붉은 부분)만 발광시킨다. 회색 금속은 반응하지 않는다.
                float maxChannel = max(rgb.r, max(rgb.g, rgb.b));
                float minChannel = min(rgb.r, min(rgb.g, rgb.b));
                float saturation = (maxChannel - minChannel) / max(maxChannel, 1e-4);

                float pulse = lerp(1.0, 0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed), _GlowPulse);
                float glow = smoothstep(_GlowThreshold, 1.0, saturation) * maxChannel;
                rgb += _GlowColor.rgb * (glow * _GlowIntensity * pulse * baseAlpha);

                fixed4 color = fixed4(rgb, baseAlpha) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
