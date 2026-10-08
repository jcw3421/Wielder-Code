using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 방어에 성공했을 때 잠깐 떴다 사라지는 표시. 저스트가드(완전 무효)와 일반 막기를
    /// 각각 다른 연출로 보여준다.
    ///
    /// 두 연출을 미리 배치해 두고 해당하는 쪽만 켠다 — 그림·글자·크기·자리를 인스펙터에서
    /// 각각 맞춰둘 수 있다(<see cref="WeaponCutInSelector"/>·<see cref="ClassIconSelector"/>와 같은 방식).
    /// 등장 연출(팝·페이드·흔들림)은 둘을 감싸는 이 오브젝트가 통째로 받는다.
    ///
    /// 어느 쪽인지는 Combat이 <see cref="Game.Core.GameplayEvents.GuardSucceeded"/>로 알려주고
    /// <see cref="HudOrchestrator"/>가 내 것만 걸러 넘긴다 — UI가 스스로 판정하지 않는다.
    /// </summary>
    public sealed class GuardResultBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;

        [Header("연출 오브젝트 (미리 배치해 두고 해당하는 쪽만 켜진다)")]
        [Tooltip("저스트가드 — 그림·글자를 자식으로 담은 오브젝트.")]
        [SerializeField] private GameObject _justGuardVisual;

        [Tooltip("일반 막기 — 그림·글자를 자식으로 담은 오브젝트.")]
        [SerializeField] private GameObject _guardVisual;

        [Header("글자색 (선택 — 비워두면 씬에서 잡은 색 그대로)")]
        [Tooltip("저스트가드 글자. 여기 넣으면 아래 색을 이 텍스트에만 입힌다 " +
                 "— 머티리얼이 아니라 Vertex Color라서 같은 폰트를 쓰는 다른 글자엔 영향이 없다.")]
        [SerializeField] private TMP_Text _justGuardLabel;
        [SerializeField] private Color _justGuardColor = new(1f, 0.85f, 0.3f, 1f);

        [Tooltip("일반 막기 글자. 비워두면 색을 건드리지 않는다.")]
        [SerializeField] private TMP_Text _guardLabel;
        [SerializeField] private Color _guardColor = Color.white;

        [Header("등장")]
        [SerializeField] private float _fadeDuration = 0.12f;

        [Tooltip("나타날 때 이 배율에서 1로 줄어들며 튄다.")]
        [SerializeField] private float _popScale = 1.4f;

        [SerializeField] private float _popDuration = 0.22f;

        [Tooltip("다 뜬 뒤 머무는 시간. 지나면 스스로 사라진다.")]
        [SerializeField] private float _holdDuration = 0.5f;

        [Header("흔들림")]
        [Tooltip("저스트가드가 흔들리는 폭(픽셀). 세게 흔들어 일반 막기와 구분한다.")]
        [SerializeField] private float _justGuardShakeStrength = 24f;

        [Tooltip("일반 막기가 흔들리는 폭(픽셀). 0이면 흔들지 않는다.")]
        [SerializeField] private float _guardShakeStrength = 9f;

        [Tooltip("흔들림이 잦아드는 데 걸리는 시간.")]
        [SerializeField] private float _shakeDuration = 0.35f;

        [Tooltip("흔들림이 잦아드는 동안 방향이 바뀌는 횟수. 높을수록 잘게 떤다.")]
        [SerializeField] private int _shakeVibrato = 20;

        private RectTransform _rect;
        private Vector2 _shownPosition;   // 흔들기 전 제자리. 흔들다 잘려도 여기로 되돌린다
        private Sequence _sequence;
        private bool _initialized;

        private void Awake()
        {
            if (_group == null)
            {
                enabled = false;
                return;
            }

            _rect = (RectTransform)_group.transform;
            _shownPosition = _rect.anchoredPosition;
            _initialized = true;

            HideImmediate();
        }

        /// <summary>방어 성공 — 연속으로 막으면 다시 호출되어 처음부터 튄다.</summary>
        public void Play(bool isJustGuard)
        {
            if (!_initialized) return;

            if (_justGuardVisual != null) _justGuardVisual.SetActive(isJustGuard);
            if (_guardVisual != null) _guardVisual.SetActive(!isJustGuard);

            // 이 텍스트 하나에만 색이 입혀진다(TMP의 Vertex Color) — 폰트 머티리얼은 건드리지 않는다.
            if (isJustGuard)
            {
                if (_justGuardLabel != null) _justGuardLabel.color = _justGuardColor;
            }
            else
            {
                if (_guardLabel != null) _guardLabel.color = _guardColor;
            }

            _sequence?.Kill();

            gameObject.SetActive(true);
            _group.alpha = 0f;
            _rect.localScale = Vector3.one * _popScale;
            _rect.anchoredPosition = _shownPosition;

            // 등장 → 홀드 → 퇴장까지 한 시퀀스로 예약한다.
            float exitAt = _popDuration + _holdDuration;

            _sequence = DOTween.Sequence()
                .Insert(0f, FadeGroup(1f))
                .Insert(0f, ScaleTo(Vector3.one, _popDuration, Ease.OutBack))
                .Insert(exitAt, FadeGroup(0f))
                .OnComplete(HideImmediate);

            // 위치 흔들기 — 저스트가드는 세게, 일반 막기는 약하게. 팝과 같이 시작해 잦아든다.
            float strength = isJustGuard ? _justGuardShakeStrength : _guardShakeStrength;
            if (strength > 0f) _sequence.Insert(0f, ShakeRect(strength));
        }

        private void OnDisable()
        {
            _sequence?.Kill();
            _sequence = null;
        }

        private void HideImmediate()
        {
            // 아래 SetActive(false)가 OnDisable을 불러 시퀀스를 다시 Kill하지 않도록 먼저 놓는다
            // (이 메서드는 시퀀스의 OnComplete에서도 불린다).
            _sequence = null;

            _group.alpha = 0f;
            _rect.localScale = Vector3.one;
            _rect.anchoredPosition = _shownPosition;   // 흔들리다 중간에 잘려도 밀린 채 남지 않게
            gameObject.SetActive(false);
        }

        // RectTransform 확장(DOShakeAnchorPos)은 DOTween 모듈 .cs에 있어 asmdef에서 안 보인다
        // — 코어 DLL의 DOTween.Shake를 그 구현 그대로 직접 부른다.
        private Tween ShakeRect(float strength)
        {
            return DOTween.Shake(
                () => _rect.anchoredPosition,
                value => _rect.anchoredPosition = value,
                _shakeDuration,
                strength,
                _shakeVibrato,
                90f,            // randomness — 흔들리는 방향이 퍼지는 각도
                true,           // ignoreZAxis — UI라 평면으로만 흔든다
                true,           // fadeOut — 갈수록 잦아든다
                ShakeRandomnessMode.Full);
        }

        private Tween FadeGroup(float target)
        {
            return DOTween.To(
                () => _group.alpha,
                value => _group.alpha = value,
                target,
                _fadeDuration);
        }

        private Tween ScaleTo(Vector3 target, float duration, Ease ease)
        {
            return DOTween
                .To(
                    () => _rect.localScale,
                    value => _rect.localScale = value,
                    target,
                    duration)
                .SetEase(ease);
        }
    }
}
