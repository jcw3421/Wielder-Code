using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 플레이어/파티원 스탯 바 (HP, 얼티밋, 경험치 등). (current/max) 비율만 표시한다.
    /// <see cref="_fillImage"/>는 Image Type = Filled(Horizontal)여야 한다.
    /// </summary>
    public sealed class PlayerStatusBar : MonoBehaviour
    {
        public enum TextFormat { ValueOverMax, Percent }   // "50 / 100" | "50%"

        [Header("필수 Bar 이미지")]
        [SerializeField] private Image _fillImage;

        [Header("선택 TexT")]
        [SerializeField] private TMP_Text _valueText;
        [SerializeField] private TextFormat _textFormat = TextFormat.ValueOverMax;

        [Header("연출 트윈")]
        [SerializeField] private float _tweenDuration = 0.25f;
        [SerializeField] private Ease _ease = Ease.OutQuad;

        private Tween _fillTween;

        /// <summary>바 갱신. animate=false면 트윈 없이 즉시 반영.</summary>
        public void SetValue(int current, int max, bool animate = true)
        {
            current = Mathf.Max(0, current);
            max = Mathf.Max(1, max);                              // 0 나눗셈 방지
            float ratio = Mathf.Clamp01((float)current / max);

            _fillTween?.Kill();
            if (animate && isActiveAndEnabled && _tweenDuration > 0f)
                _fillTween = FillTo(_fillImage, ratio, _tweenDuration).SetEase(_ease);
            else
                _fillImage.fillAmount = ratio;

            SetText(current, max, ratio);
        }

        private void SetText(int current, int max, float ratio)
        {
            if (_valueText == null) return;
            _valueText.text = _textFormat == TextFormat.Percent
                ? $"{Mathf.RoundToInt(ratio * 100f)}%"
                : $"{current.ToString("N0", CultureInfo.InvariantCulture)} / {max.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        // Image.DOFillAmount는 DOTween UI 모듈(asmdef 밖)이라 코어 DOTween.To로 트윈한다.
        private static Tweener FillTo(Image image, float targetRatio, float duration)
            => DOTween.To(() => image.fillAmount, value => image.fillAmount = value, targetRatio, duration);

        private void OnDestroy() => _fillTween?.Kill();
    }
}
