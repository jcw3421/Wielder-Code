using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 보스 HP 바. (current/max) 비율 표시 + 데미지 트레일 + 피격 플래시 연출.
    /// <see cref="_fillImage"/>는 Image Type = Filled(Horizontal)여야 한다.
    /// </summary>
    public sealed class BossStatusBar : MonoBehaviour
    {
        [Header("필수 Bar 이미지")]
        [SerializeField] private Image _fillImage;

        [Header("선택 TexT")]
        [SerializeField] private TMP_Text _valueText;

        [Header("연출 트윈")]
        [SerializeField] private float _tweenDuration = 0.25f;   // 메인 바 이동 시간
        [SerializeField] private Ease _ease = Ease.OutQuad;

        [Header("데미지 트레일")]
        [SerializeField] private Image _trailFill;               // 늦게 따라오는 흰 잔상 바
        [SerializeField] private float _trailDelay = 0.3f;       // 따라오기 전 멈추는 시간
        [SerializeField] private float _trailDuration = 0.6f;    // 따라잡는 시간

        [Header("피격 플래시")]
        [SerializeField] private CanvasGroup _hitFlash;          // 바 끝에서 번쩍 (왼쪽 앵커로 배치, 평소 alpha 0)
        [SerializeField] private float _flashDuration = 0.25f;

        private Tween _fillTween, _trailTween, _flashTween;
        private float _ratio = -1f;   // 직전 비율. -1이면 첫 세팅 전(피해로 치지 않음).

        /// <summary>바 갱신. animate=false면 트윈 없이 즉시 반영.</summary>
        public void SetValue(int current, int max, bool animate = true)
        {
            current = Mathf.Max(0, current);
            max = Mathf.Max(1, max);                              // 0 나눗셈 방지
            float ratio = Mathf.Clamp01((float)current / max);
            bool damaged = _ratio >= 0f && ratio < _ratio;
            bool tween = animate && isActiveAndEnabled;

            // 메인 바 — 새 값으로 이동
            _fillTween?.Kill();
            if (tween && _tweenDuration > 0f) _fillTween = FillTo(_fillImage, ratio, _tweenDuration).SetEase(_ease);
            else _fillImage.fillAmount = ratio;

            // 트레일 바 — 피해 때만 옛 값에 머물다 천천히 따라옴, 그 외엔 즉시
            if (_trailFill != null)
            {
                _trailTween?.Kill();
                if (damaged && tween) _trailTween = FillTo(_trailFill, ratio, _trailDuration).SetDelay(_trailDelay).SetEase(Ease.OutQuad);
                else _trailFill.fillAmount = ratio;
            }

            if (damaged) PlayHitFlash(ratio);

            _ratio = ratio;
            SetText(current, max);
        }

        // 바 끝(채워진 오른쪽 끝)으로 옮겨 alpha 1→0으로 번쩍인다.
        private void PlayHitFlash(float ratio)
        {
            if (_hitFlash == null || !isActiveAndEnabled) return;

            var rect = (RectTransform)_hitFlash.transform;
            rect.anchoredPosition = new Vector2(_fillImage.rectTransform.rect.width * ratio, rect.anchoredPosition.y);

            _flashTween?.Kill();
            _hitFlash.alpha = 1f;
            _flashTween = DOTween.To(() => _hitFlash.alpha, a => _hitFlash.alpha = a, 0f, _flashDuration);
        }

        private void SetText(int current, int max)
        {
            if (_valueText != null) _valueText.text = $"{current.ToString("N0", CultureInfo.InvariantCulture)} / {max.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        // Image.DOFillAmount는 DOTween UI 모듈(asmdef 밖)이라 코어 DOTween.To로 트윈한다.
        private static Tweener FillTo(Image image, float targetRatio, float duration)
            => DOTween.To(() => image.fillAmount, value => image.fillAmount = value, targetRatio, duration);

        private void OnDestroy()
        {
            _fillTween?.Kill();
            _trailTween?.Kill();
            _flashTween?.Kill();
        }
    }
}
