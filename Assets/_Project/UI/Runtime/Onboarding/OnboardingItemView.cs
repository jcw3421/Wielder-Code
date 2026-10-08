using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Game.UI.Onboarding
{
    /// <summary>체크리스트 한 줄의 표시. 마름모 아이콘 하나와 문장 하나.
    /// 완료되면 아이콘이 채워지고 문장이 흐려지면서 한 번 튄다 —
    /// 사라지지는 않는다. 방금 뭘 해냈는지 눈으로 확인할 시간을 준다.</summary>
    public sealed class OnboardingItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        [Tooltip("미완료 표시. 완료되면 꺼진다.")]
        [SerializeField] private GameObject _pendingIcon;

        [Tooltip("완료 표시. 처음엔 꺼져 있다.")]
        [SerializeField] private GameObject _completedIcon;

        [SerializeField] private CanvasGroup _group;

        [Header("완료 연출")]
        [Tooltip("완료된 줄의 불투명도. 목록에 남되 눈에 덜 걸리게 한다.")]
        [SerializeField] private float _completedAlpha = 0.45f;

        [Tooltip("완료 순간 이 배율에서 1로 줄어들며 튄다.")]
        [SerializeField] private float _popScale = 1.2f;

        [SerializeField] private float _popDuration = 0.25f;

        private RectTransform _rect;

        private void Awake() => _rect = (RectTransform)transform;

        public void Bind(string text)
        {
            if (_label != null) _label.text = text;
            ApplyCompleted(false);
        }

        public void MarkCompleted()
        {
            ApplyCompleted(true);

            if (_rect == null) return;
            _rect.DOKill();
            _rect.localScale = Vector3.one * _popScale;
            _rect.DOScale(1f, _popDuration).SetEase(Ease.OutBack);
        }

        private void ApplyCompleted(bool completed)
        {
            if (_pendingIcon != null) _pendingIcon.SetActive(!completed);
            if (_completedIcon != null) _completedIcon.SetActive(completed);
            if (_group != null) _group.alpha = completed ? _completedAlpha : 1f;
        }

        private void OnDestroy()
        {
            if (_rect != null) _rect.DOKill();
        }
    }
}
