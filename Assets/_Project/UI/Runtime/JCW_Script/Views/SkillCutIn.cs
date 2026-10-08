using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 궁극기 컷인 연출. 궁극기를 쓰면 슬라이드바가 나타나고 그 위로 컷인이 우측에서 들어온다.
    /// 짧게 머문 뒤 슬라이드바와 컷인이 같이 빠지며, 체인 연결 윈도우가 그보다 먼저 끝나면
    /// (또는 풀체인으로 소비되면) 그 시점에 바로 거둔다.
    ///
    /// 언제 끝나는지는 Combat이 <see cref="NotifyChainEnded"/>로 알려주므로 UI 시계로 판단하지 않는다
    /// — UI가 스스로 지우면 Combat 실제 타이밍과 어긋난다.
    ///
    /// 재생 시점과 무기 ID는 <see cref="HudOrchestrator"/>가 전달한다.
    /// 내 궁극기 전용 연출이다 — 기여자 전원은 <see cref="PartyCutInStack"/>에 카드로 쌓이고,
    /// 남은 연결 시간 표시는 <see cref="ChainStageBanner"/>가 전담한다.
    /// </summary>
    public sealed class SkillCutIn : MonoBehaviour
    {
        [Header("슬라이드바")]
        [SerializeField] private CanvasGroup _slideBar;

        [Header("컷인")]
        [SerializeField] private RectTransform _cutIn;
        [SerializeField] private WeaponCutInSelector _cutInIllustrations;

        [Tooltip("컷인이 숨겨질 때의 위치 오프셋. 우측에서 들어오려면 X를 양수로.")]
        [SerializeField] private Vector2 _slideOffset = new(900f, 0f);

        [Header("화면 딤")]
        [SerializeField] private CanvasGroup _dim;
        [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.5f;

        [Header("타이밍")]
        [SerializeField] private float _slideDuration = 0.15f;

        [Tooltip("컷인이 다 들어온 뒤 머무는 시간. 이 시간이 지나면 연결 윈도우가 남아 있어도 스스로 빠진다.")]
        [SerializeField] private float _holdDuration = 0.6f;

        private Vector2 _shownPosition;
        private Sequence _sequence;
        private bool _initialized;

        private void Awake()
        {
            if (_cutIn == null || _cutInIllustrations == null)
            {
                enabled = false;
                return;
            }

            _shownPosition = _cutIn.anchoredPosition;
            _initialized = true;

            HideImmediate();
        }

        private void OnDisable()
        {
            _sequence?.Kill();
            _sequence = null;

            if (_initialized) HideImmediate();
        }

        /// <summary>
        /// 궁극기 사용 — 슬라이드바를 띄우고 해당 무기 컷인을 우측에서 밀어 넣는다.
        /// 체인이 이어지면 다시 호출되어 컷인이 교체된다.
        /// </summary>
        public void Play(int weaponId)
        {
            if (!_initialized) return;
            if (!_cutInIllustrations.Show(weaponId)) return;

            _sequence?.Kill();

            _cutIn.gameObject.SetActive(true);
            _cutIn.anchoredPosition = _shownPosition + _slideOffset;

            if (_slideBar != null)
            {
                _slideBar.alpha = 0f;
                _slideBar.gameObject.SetActive(true);
            }

            // 등장 → 홀드 → 퇴장까지 한 시퀀스로 예약한다. 윈도우가 먼저 끝나면
            // NotifyChainEnded가 이 시퀀스를 잘라내고 즉시 퇴장시킨다.
            float exitAt = _slideDuration + _holdDuration;

            _sequence = DOTween.Sequence()
                .Insert(0f, MoveCutIn(_shownPosition, Ease.OutCubic))
                .Insert(exitAt, MoveCutIn(_shownPosition + _slideOffset, Ease.InCubic));

            if (_slideBar != null)
            {
                _sequence.Insert(0f, FadeGroup(_slideBar, 1f));
                _sequence.Insert(exitAt, FadeGroup(_slideBar, 0f));
            }

            if (_dim != null)
            {
                _sequence.Insert(0f, FadeGroup(_dim, _dimAlpha));
                _sequence.Insert(exitAt, FadeGroup(_dim, 0f));
            }

            _sequence.OnComplete(HideImmediate);
        }

        /// <summary>연결 윈도우 만료 또는 풀체인 소비 — 슬라이드바와 컷인을 같이 거둔다.</summary>
        public void NotifyChainEnded()
        {
            if (!_initialized) return;
            if (!_cutIn.gameObject.activeSelf) return;

            _sequence?.Kill();

            _sequence = DOTween.Sequence()
                .Join(MoveCutIn(_shownPosition + _slideOffset, Ease.InCubic));

            if (_slideBar != null) _sequence.Join(FadeGroup(_slideBar, 0f));
            if (_dim != null) _sequence.Join(FadeGroup(_dim, 0f));

            _sequence.OnComplete(HideImmediate);
        }

        private void HideImmediate()
        {
            _cutIn.anchoredPosition = _shownPosition;
            _cutIn.gameObject.SetActive(false);

            if (_slideBar != null)
            {
                _slideBar.alpha = 0f;
                _slideBar.gameObject.SetActive(false);
            }

            if (_dim != null) _dim.alpha = 0f;
        }

        private Tween MoveCutIn(Vector2 target, Ease ease)
        {
            return DOTween
                .To(
                    () => _cutIn.anchoredPosition,
                    value => _cutIn.anchoredPosition = value,
                    target,
                    _slideDuration)
                .SetEase(ease);
        }

        private Tween FadeGroup(CanvasGroup group, float target)
        {
            return DOTween.To(
                () => group.alpha,
                value => group.alpha = value,
                target,
                _slideDuration);
        }
    }
}
