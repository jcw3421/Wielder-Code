Shader "UI/PunkGraphicFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Palette)]
        _InkColor ("Ink (dark)", Color) = (0.06, 0.06, 0.08, 1)
        _PaperColor ("Paper (light)", Color) = (0.95, 0.95, 0.92, 1)
        _AccentColor ("Accent", Color) = (0.78, 1.0, 0.09, 1)
        _ThresholdLow ("Threshold Low", Range(0, 1)) = 0.22
        _ThresholdHigh ("Threshold High", Range(0, 1)) = 0.62
        _ThresholdSoftness ("Threshold Softness", Range(0, 0.3)) = 0.02
        _ToneAmount ("Tone Amount", Range(0, 1)) = 1

        [Header(Halftone)]
        _HalftoneAmount ("Halftone Amount", Range(0, 1)) = 0.8
        _HalftoneScale ("Halftone Cell (px)", Range(2, 40)) = 7
        _HalftoneAngle ("Halftone Angle (deg)", Range(0, 90)) = 30

        [Header(Ink Outline)]
        _OutlineColor ("Outline Color", Color) = (0.03, 0.03, 0.05, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 8)) = 2.5
        _OutlineIntensity ("Outline Intensity", Range(0, 1)) = 0.9

        [Header(Chromatic Aberration)]
        _AberrationAmount ("Aberration (texels)", Range(0, 8)) = 1.5
        _AberrationAngle ("Aberration Angle (deg)", Range(0, 180)) = 0

        [Header(Glitch)]
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0
        _GlitchStrength ("Glitch Strength", Range(0, 0.2)) = 0.03
        _GlitchBands ("Glitch Bands", Range(4, 80)) = 24
        _GlitchSpeed ("Glitch Speed", Range(0, 30)) = 12

        [Header(Print Texture)]
        _ScanlineAmount ("Scanline Amount", Range(0, 1)) = 0.15
        _ScanlineScale ("Scanline Cell (px)", Range(1, 12)) = 3
        _GrainAmount ("Grain Amount", Range(0, 1)) = 0.08

        [Header(Accent Glow)]
        _GlowIntensity ("Accent Glow", Range(0, 4)) = 0
        _GlowThreshold ("Glow Saturation Threshold", Range(0, 1)) = 0.4
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

            fixed4 _InkColor;
            fixed4 _PaperColor;
            fixed4 _AccentColor;
            float _ThresholdLow;
            float _ThresholdHigh;
            float _ThresholdSoftness;
            float _ToneAmount;

            float _HalftoneAmount;
            float _HalftoneScale;
            float _HalftoneAngle;

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineIntensity;

            float _AberrationAmount;
            float _AberrationAngle;

            float _GlitchAmount;
            float _GlitchStrength;
            float _GlitchBands;
            float _GlitchSpeed;

            float _ScanlineAmount;
            float _ScanlineScale;
            float _GrainAmount;

            float _GlowIntensity;
            float _GlowThreshold;
            float _GlowPulse;
            float _GlowPulseSpeed;

            float Luminance01(float3 rgb)
            {
                return dot(rgb, float3(0.299, 0.587, 0.114));
            }

            float Hash11(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float2 Rotate2D(float2 p, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;
                float s = sin(radians);
                float c = cos(radians);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            // 가로 띠 단위로 UV를 순간 밀어 VHS 글리치를 만든다.
            // 띠와 시간을 해시로 섞어 일부 띠만 튀게 한다 — 전부 튀면 그냥 흔들림으로 보인다.
            float2 ApplyGlitch(float2 uv)
            {
                if (_GlitchAmount <= 0.0) return uv;

                float band = floor(uv.y * _GlitchBands);
                float tick = floor(_Time.y * _GlitchSpeed);
                float noise = Hash11(band + tick * 37.0);

                float active = step(1.0 - _GlitchAmount, noise);
                uv.x += (noise - 0.5) * 2.0 * _GlitchStrength * active;

                return uv;
            }

            // 화면 픽셀 기준 망점. UV 기준으로 하면 UI 크기마다 점 크기가 달라진다.
            // 밝을수록 점이 커지는 인쇄 스크린톤 방식.
            float HalftoneDots(float2 screenPosition, float luminance)
            {
                float2 cell = Rotate2D(screenPosition / max(_HalftoneScale, 1e-3), _HalftoneAngle);
                float distanceToCenter = length(frac(cell) - 0.5);

                float radius = saturate(luminance) * 0.72;
                float aa = fwidth(distanceToCenter) + 1e-4;

                return smoothstep(radius + aa, radius - aa, distanceToCenter);
            }

            // 휘도를 먹/액센트/종이 3톤으로 하드 컷. 이게 그래픽 인쇄물 느낌의 뼈대다.
            float3 QuantizeToPalette(float luminance)
            {
                float soft = max(_ThresholdSoftness, 1e-4);

                float toAccent = smoothstep(_ThresholdLow - soft, _ThresholdLow + soft, luminance);
                float toPaper = smoothstep(_ThresholdHigh - soft, _ThresholdHigh + soft, luminance);

                float3 tone = lerp(_InkColor.rgb, _AccentColor.rgb, toAccent);
                return lerp(tone, _PaperColor.rgb, toPaper);
            }

            // 알파 기울기로 실루엣 경계를 뽑아 굵은 먹선을 얹는다.
            float SilhouetteEdge(float2 uv, float baseAlpha)
            {
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;

                float left  = tex2D(_MainTex, uv - float2(offset.x, 0)).a;
                float right = tex2D(_MainTex, uv + float2(offset.x, 0)).a;
                float down  = tex2D(_MainTex, uv - float2(0, offset.y)).a;
                float up    = tex2D(_MainTex, uv + float2(0, offset.y)).a;

                float gradient = abs(right - left) + abs(up - down);

                // baseAlpha를 곱해 실루엣 안쪽에만 남긴다(바깥으로 번지면 UI가 지저분해진다).
                return saturate(gradient) * baseAlpha;
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
                float2 screenPosition = i.vertex.xy;
                float2 uv = ApplyGlitch(i.uv);

                // 색수차 — R/B만 반대로 밀어 인쇄 어긋남처럼 보이게. 알파는 중앙 샘플 기준.
                float2 shift = Rotate2D(float2(_MainTex_TexelSize.x * _AberrationAmount, 0), _AberrationAngle);

                fixed4 center = tex2D(_MainTex, uv);
                float3 source = float3(
                    tex2D(_MainTex, uv + shift).r,
                    center.g,
                    tex2D(_MainTex, uv - shift).b);

                float baseAlpha = center.a;
                float luminance = Luminance01(source);

                // 3톤 하드 컷.
                float3 rgb = lerp(source, QuantizeToPalette(luminance), _ToneAmount);

                // 하프톤은 중간톤 대역에만 얹는다 — 전면에 깔면 그냥 도트 노이즈가 된다.
                float midBand = smoothstep(_ThresholdLow - 0.12, _ThresholdLow + 0.06, luminance)
                              * (1.0 - smoothstep(_ThresholdHigh - 0.06, _ThresholdHigh + 0.12, luminance));
                float dots = HalftoneDots(screenPosition, luminance);
                rgb = lerp(rgb, _InkColor.rgb, (1.0 - dots) * midBand * _HalftoneAmount);

                // 굵은 먹선. 더하지 않고 덮어써야 선이 뭉개지지 않는다.
                float outline = SilhouetteEdge(uv, baseAlpha) * _OutlineIntensity;
                rgb = lerp(rgb, _OutlineColor.rgb, saturate(outline));

                // 스캔라인·그레인 — 인쇄/CRT 질감. 화면 픽셀 기준이라 UI 크기와 무관하다.
                float scanline = sin(screenPosition.y * UNITY_PI / max(_ScanlineScale, 1e-3)) * 0.5 + 0.5;
                rgb *= lerp(1.0, scanline, _ScanlineAmount);

                float grain = Hash21(screenPosition + frac(_Time.y) * 100.0) - 0.5;
                rgb += grain * _GrainAmount;

                // 원본에서 채도가 높던 부분(문양)만 액센트로 발광시킨다.
                float maxChannel = max(source.r, max(source.g, source.b));
                float minChannel = min(source.r, min(source.g, source.b));
                float saturation = (maxChannel - minChannel) / max(maxChannel, 1e-4);

                float pulse = lerp(1.0, 0.5 + 0.5 * sin(_Time.y * _GlowPulseSpeed), _GlowPulse);
                float glow = smoothstep(_GlowThreshold, 1.0, saturation) * maxChannel;

                // 더해지는 항은 알파를 곱한다 — 안 그러면 투명 영역에 빛무리가 남는다.
                rgb += _AccentColor.rgb * (glow * _GlowIntensity * pulse * baseAlpha);

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
