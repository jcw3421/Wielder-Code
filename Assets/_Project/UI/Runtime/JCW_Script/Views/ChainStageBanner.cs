using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 체인 카드 목록 위에 붙는 단계 표시 이미지. 체인 1·2·3 그리고 풀체인 완성까지
    /// 단계가 오를 때마다 그림이 바뀌며 한 번 튄다.
    ///
    /// 단계별 그림을 미리 배치해 두고 해당 단계 것만 켠다 — 크기·자리를 인스펙터에서 각각 맞춰둘 수 있다
    /// (<see cref="WeaponCutInSelector"/>·<see cref="GuardResultBanner"/>와 같은 방식).
    ///
    /// 언제 어느 단계를 띄울지는 <see cref="PartyCutInStack"/>이 정한다.
    /// </summary>
    public sealed class ChainStageBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;

        [Tooltip("단계별 연출 오브젝트. 0번 = 체인 1단계, 마지막 = 풀체인 완성. 카드 슬롯 수와 같게 넣는다. " +
                 "미리 배치해 두면 해당 단계 것만 켜진다.")]
        [SerializeField] private GameObject[] _stageVisuals;

        [Header("등장 연출")]
        [Tooltip("튀어나오는 대상. 보통 단계 그림들을 담은 부모를 넣는다. 비우면 이 오브젝트 전체가 튄다.")]
        [SerializeField] private RectTransform _popTarget;

        [SerializeField] private float _fadeDuration = 0.15f;

        [Tooltip("단계가 오를 때 이 배율에서 1로 줄어들며 튄다.")]
        [SerializeField] private float _popScale = 1.35f;

        [SerializeField] private float _popDuration = 0.25f;

        [Header("연결 남은 시간 (선택)")]
        [Tooltip("다음 링크까지 남은 시간을 초 단위로 센다. 링크가 이어질 때마다 되감긴다.")]
        [SerializeField] private TMP_Text _windowText;

        [Tooltip("같은 남은 시간을 채움량으로도 보여준다. Image Type = Filled.")]
        [SerializeField] private Image _windowGauge;

        [Tooltip("링크가 이어져 게이지가 되감길 때 차오르는 시간. 0이면 즉시 찬다.")]
        [SerializeField] private float _gaugeRefillDuration = 0.3f;

        [Header("게이지 경고 (남은 시간이 얼마 없을 때)")]
        [Tooltip("남은 시간이 이 값 아래로 내려가면 게이지 색이 깜빡인다.")]
        [SerializeField] private float _warnThreshold = 2f;

        [SerializeField] private Color _warnColor = new(1f, 0.25f, 0.2f, 1f);

        [Tooltip("깜빡임 한 번에 걸리는 시간(초). 짧을수록 다급해진다.")]
        [SerializeField] private float _warnPulseDuration = 0.25f;

        private RectTransform _rect;
        private Sequence _sequence;
        private bool _prepared;

        private float _windowSecondsLeft;
        private float _windowSecondsTotal;

        private Tween _gaugeRefill;      // 되감기는 동안엔 남은 시간이 채움량을 덮어쓰지 않게 한다
        private Tween _gaugeWarn;
        private Color _gaugeBaseColor;

        /// <summary>꺼진 채 대기하므로 자기 Awake를 믿을 수 없다 — 스택이 대신 불러준다.</summary>
        public void Prepare()
        {
            if (_prepared) return;
            if (_group == null) return;

            _rect = _popTarget != null ? _popTarget : (RectTransform)_group.transform;
            if (_windowGauge != null) _gaugeBaseColor = _windowGauge.color;
            _prepared = true;

            HideImmediate();
        }

        /// <summary>
        /// 해당 단계 이미지로 바꾸고 튀어나오게 한다(stage 1부터).
        /// windowSeconds는 다음 링크까지 허용되는 시간 — 링크가 이어질 때마다 되감긴다.
        /// </summary>
        public void Show(int stage, int windowSeconds)
        {
            Prepare();
            if (!_prepared) return;

            int index = stage - 1;
            if (index < 0 || index >= _stageVisuals.Length) return;

            _windowSecondsTotal = windowSeconds;
            _windowSecondsLeft = _windowSecondsTotal;
            StopWarnPulse();
            UpdateWindowDisplay();
            RefillGauge();

            ShowStageVisual(index);

            _sequence?.Kill();

            gameObject.SetActive(true);
            _group.alpha = 0f;
            _rect.localScale = Vector3.one * _popScale;

            _sequence = DOTween.Sequence()
                .Insert(0f, FadeGroup(1f))
                .Insert(0f, ScaleTo(Vector3.one, _popDuration, Ease.OutBack));
        }

        /// 해당 단계 것만 켜고 나머지는 끈다.
        private void ShowStageVisual(int index)
        {
            for (int i = 0; i < _stageVisuals.Length; i++)
            {
                if (_stageVisuals[i] != null) _stageVisuals[i].SetActive(i == index);
            }
        }

        /// <summary>
        /// 카운트다운만 멈춘다. 풀체인이 완성돼 더 이을 게 없는데도 숫자가 계속 줄면 어색하다
        /// — 배너 그림은 그대로 두고 남은 시간 표시만 지운다.
        /// </summary>
        public void StopWindow()
        {
            _windowSecondsLeft = 0f;

            _gaugeRefill?.Kill();
            _gaugeRefill = null;
            StopWarnPulse();

            if (_windowText != null) _windowText.text = string.Empty;
            if (_windowGauge != null) _windowGauge.fillAmount = 0f;
        }

        /// <summary>체인 종료 — 사라진다.</summary>
        public void Hide()
        {
            StopWindow();

            if (!_prepared) return;
            if (!gameObject.activeSelf) return;

            _sequence?.Kill();

            _sequence = DOTween.Sequence()
                .Insert(0f, FadeGroup(0f))
                .OnComplete(HideImmediate);
        }

        /// <summary>트윈 없이 즉시 숨긴다(새 체인 시작 등).</summary>
        public void HideNow()
        {
            StopWindow();

            if (!_prepared) return;

            _sequence?.Kill();
            HideImmediate();
        }

        // 남은 시간은 표시용이다 — 실제로 언제 끊기는지는 Combat이 stage 0으로 알려준다.
        // UI 시계로 판단하면 서버 실제 타이밍과 어긋나므로 여기선 숫자만 굴린다.
        private void Update()
        {
            if (_windowSecondsLeft <= 0f) return;

            _windowSecondsLeft = Mathf.Max(0f, _windowSecondsLeft - Time.unscaledDeltaTime);
            UpdateWindowDisplay();

            if (_windowSecondsLeft <= _warnThreshold) StartWarnPulse();
        }

        private void UpdateWindowDisplay()
        {
            if (_windowText != null) _windowText.text = _windowSecondsLeft.ToString("0.0");

            if (_windowGauge == null) return;

            // 되감기 트윈이 도는 중엔 그쪽에 채움량을 맡긴다 — 여기서 덮어쓰면 트윈이 씹힌다.
            if (_gaugeRefill != null && _gaugeRefill.IsActive() && _gaugeRefill.IsPlaying()) return;

            _windowGauge.fillAmount = _windowSecondsLeft / _windowSecondsTotal;
        }

        // 링크가 이어지면 게이지가 현재 위치에서 꽉 찬 데까지 차오른다.
        // 남은 시간이 되감겼다는 걸 눈으로 잡아주는 부분이라 즉시 채우지 않는다.
        private void RefillGauge()
        {
            if (_windowGauge == null) return;

            _gaugeRefill?.Kill();

            if (_gaugeRefillDuration <= 0f)
            {
                _windowGauge.fillAmount = 1f;
                return;
            }

            _gaugeRefill = DOTween
                .To(
                    () => _windowGauge.fillAmount,
                    value => _windowGauge.fillAmount = value,
                    1f,
                    _gaugeRefillDuration)
                .SetEase(Ease.OutCubic);
        }

        // 남은 시간이 얼마 없을 때 경고색으로 깜빡인다. 이미 돌고 있으면 그대로 둔다.
        private void StartWarnPulse()
        {
            if (_windowGauge == null) return;
            if (_gaugeWarn != null && _gaugeWarn.IsActive()) return;

            _gaugeWarn = DOTween
                .To(
                    () => _windowGauge.color,
                    value => _windowGauge.color = value,
                    _warnColor,
                    _warnPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopWarnPulse()
        {
            _gaugeWarn?.Kill();
            _gaugeWarn = null;

            if (_windowGauge != null) _windowGauge.color = _gaugeBaseColor;
        }

        private void OnDisable()
        {
            _sequence?.Kill();
            _sequence = null;

            _gaugeRefill?.Kill();
            _gaugeRefill = null;
            StopWarnPulse();
        }

        private void HideImmediate()
        {
            // 아래 SetActive(false)가 OnDisable을 불러 시퀀스를 다시 Kill하지 않도록 먼저 놓는다
            // (이 메서드는 시퀀스의 OnComplete에서도 불린다).
            _sequence = null;

            _group.alpha = 0f;
            _rect.localScale = Vector3.one;
            gameObject.SetActive(false);
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
