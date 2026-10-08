using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 체인 진행을 화면 가장자리 풀스크린 효과 레이어로 표현한다.
    /// 레이어 하나 = FullScreen Pass Render Feature 머티리얼 하나(바깥→안쪽 순서로 배열에 넣는다).
    /// 여기선 각 머티리얼의 색(_Color)과 표시량(_Alpha)만 민다 — Mask_Size 같은 룩 파라미터는
    /// 머티리얼 에셋에서 직접 잡는다(레이어마다 다르게 해서 안쪽으로 좁혀지는 그림을 만든다).
    ///
    /// 값은 <see cref="HudOrchestrator"/>가 밀어준다(직접 이벤트 구독 안 함).
    ///
    /// ※ Render Feature는 머티리얼 "에셋"을 참조하므로 런타임 변경이 에셋에 직접 쓰인다.
    ///   에디터에서 플레이하면 .mat 값이 바뀐 채 남아서, OnDisable에서 숨김 상태로 되돌린다.
    /// </summary>
    public sealed class ChainVignette : MonoBehaviour
    {
        [Header("레이어 머티리얼 (0 = 스타터/최외곽 → 안쪽 순서)")]
        [Tooltip("FS_Speed 복제본들. Mask_Size를 레이어마다 다르게 잡아 안쪽으로 좁혀지게 만든다.")]
        [SerializeField] private Material[] _layerMaterials;

        [Header("직업 색 (HDR — 세기를 올려야 선명하다)")]
        [SerializeField] private ClassColor[] _classColors;

        [Header("표시")]
        [Tooltip("이미 이어진(바깥) 레이어가 정착하는 알파.")]
        [SerializeField] private float _settledAlpha = 0.5f;
        [Tooltip("알파 변화 속도(초당).")]
        [SerializeField] private float _fadeSpeed = 4f;

        [Header("펄스 (가장 안쪽 = 지금 이어야 할 레이어)")]
        [SerializeField] private float _pulseSpeed = 3f;
        [SerializeField] private float _pulseMin = 0.35f;
        [SerializeField] private float _pulseMax = 1f;

        [Header("풀체인 / 실패 색")]
        [Tooltip("풀체인 순간 모든 레이어가 이 색으로 통일되며 터진다.")]
        [SerializeField, ColorUsage(true, true)] private Color _burstColor = Color.white;
        [Tooltip("체인이 끊겼을 때 식어가는 색.")]
        [SerializeField, ColorUsage(true, true)] private Color _failColor = Color.gray;

        [Serializable]
        private struct ClassColor
        {
            public ClassType ClassType;
            [ColorUsage(true, true)] public Color Color;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        private float[] _alpha;      // 레이어별 현재 알파(부드러운 전환용)
        private int _activeCount;    // 켜진 레이어 수 = 현재 체인 단계
        private float _burst;        // 풀체인 버스트 잔량(0~1)

        private void Awake() => _alpha = new float[_layerMaterials.Length];

        private void OnDisable() => HideAll();

        /// <summary>stage번째 레이어(1부터)를 기여자 직업색으로 켠다. 가장 안쪽이 현재 레이어가 된다.</summary>
        public void SetLayer(int stage, ClassType classType)
        {
            if (stage < 1 || stage > _layerMaterials.Length) return;

            _layerMaterials[stage - 1].SetColor(ColorId, GetColor(classType));
            _activeCount = stage;
            _burst = 0f;
        }

        /// <summary>체인 끊김. 쌓인 레이어가 회색으로 식으며 사라진다.</summary>
        public void Fail()
        {
            SetAllColors(_failColor);
            _activeCount = 0;
            _burst = 0f;
        }

        /// <summary>풀체인 완성. 모든 레이어가 버스트 색으로 통일되며 터진 뒤 사라진다.</summary>
        public void Burst()
        {
            SetAllColors(_burstColor);
            _activeCount = 0;
            _burst = 1f;
        }

        private void Update()
        {
            if (_alpha == null) return;

            if (_burst > 0f)
                _burst = Mathf.MoveTowards(_burst, 0f, _fadeSpeed * Time.deltaTime);

            // 가장 안쪽(현재) 레이어만 울렁여 "지금 이어라"를 알린다.
            float pulse = Mathf.Lerp(_pulseMin, _pulseMax, (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f);

            for (int i = 0; i < _layerMaterials.Length; i++)
            {
                float target;
                if (_burst > 0f) target = _burst;                 // 버스트: 전 레이어 동시 발광
                else if (i >= _activeCount) target = 0f;           // 아직 안 이어졌거나 취소됨
                else if (i == _activeCount - 1) target = pulse;    // 현재 레이어
                else target = _settledAlpha;                        // 이미 이어진 레이어

                _alpha[i] = Mathf.MoveTowards(_alpha[i], target, _fadeSpeed * Time.deltaTime);
                _layerMaterials[i].SetFloat(AlphaId, _alpha[i]);
            }
        }

        private void HideAll()
        {
            if (_alpha == null) return;

            _activeCount = 0;
            _burst = 0f;
            for (int i = 0; i < _layerMaterials.Length; i++)
            {
                _alpha[i] = 0f;
                _layerMaterials[i].SetFloat(AlphaId, 0f);
            }
        }

        private void SetAllColors(Color color)
        {
            for (int i = 0; i < _layerMaterials.Length; i++)
                _layerMaterials[i].SetColor(ColorId, color);
        }

        private Color GetColor(ClassType type)
        {
            if (_classColors != null)
                for (int i = 0; i < _classColors.Length; i++)
                    if (_classColors[i].ClassType == type)
                        return _classColors[i].Color;
            return Color.white;
        }
    }
}
