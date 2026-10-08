using UnityEngine;

namespace Game.BossContent
{
    /// 바닥 장판 쿼드. TFW/GroundField 셰이더의 _Progress를 수명 동안 0→1로 밀어
    /// "차오르다 터진다"를 보여준다 — 플레이어가 빠져나갈 시간을 눈으로 읽는 장치다.
    ///
    /// 순수 연출이라 서버 판정과 무관하다. 판정은 패턴 코드가 자기 시점에 따로 낸다.
    [RequireComponent(typeof(MeshRenderer))]
    public class PatternFieldDecal : MonoBehaviour
    {
        [Tooltip("이 시간에 걸쳐 _Progress가 0에서 1까지 간다. 보통 예고~타격 간격과 맞춘다.")]
        public float FillSeconds = 1.5f;

        [Tooltip("다 찬 뒤 남는 시간 동안 서서히 사라진다. 0이면 그대로 남는다(큐 Lifetime이 지운다).")]
        public float FadeSeconds = 0.6f;

        [Tooltip("가운데 안전지대 비율(0~1). 도넛 장판만 0보다 크게 둔다.")]
        [Range(0f, 1f)] public float InnerRadius;

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int InnerId    = Shader.PropertyToID("_InnerRadius");
        private static readonly int ColorId    = Shader.PropertyToID("_BaseColor");
        private static readonly int FadeId     = Shader.PropertyToID("_Fade");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _fillColor;
        private float _elapsed;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();

            // 머티리얼을 복제하지 않고 프로퍼티 블록만 쓴다 — 장판이 여러 장 겹쳐도 배칭이 살아 있다.
            _fillColor = _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(ColorId)
                ? _renderer.sharedMaterial.GetColor(ColorId)
                : Color.white;

            Apply(0f, 1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;   // guardrail:allow RNG — 데칼 페이드(뷰 전용 MonoBehaviour)

            float progress = FillSeconds > 0.001f ? Mathf.Clamp01(_elapsed / FillSeconds) : 1f;

            // 다 찬 뒤부터 밝기를 내린다. 큐의 Lifetime이 오브젝트를 지우기 전에 자연스럽게 빠지게.
            float fade = 1f;
            if (FadeSeconds > 0.001f && _elapsed > FillSeconds)
                fade = Mathf.Clamp01(1f - (_elapsed - FillSeconds) / FadeSeconds);

            Apply(progress, fade);
        }

        private void Apply(float progress, float fade)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(ProgressId, progress);
            _block.SetFloat(InnerId, InnerRadius);
            _block.SetColor(ColorId, _fillColor * fade);

            // 색만 낮추면 그을음·자국의 알파가 그대로 남아, 큐 수명이 끝나 오브젝트가
            // 지워지는 순간 팍 꺼진다. 알파도 같이 빼야 서서히 사라진다.
            _block.SetFloat(FadeId, fade);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
