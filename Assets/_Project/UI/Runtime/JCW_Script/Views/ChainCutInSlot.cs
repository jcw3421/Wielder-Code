using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 체인 컷인 카드 한 장. 궁극기를 쓴 플레이어 한 명 = 카드 한 장이다.
    /// 직업별 카드 배경 + 무기별 일러스트 + 닉네임을 세팅하고 우측에서 밀려 들어온다.
    ///
    /// 언제 어떤 값으로 켤지는 <see cref="PartyCutInStack"/>이 정한다 — 이 컴포넌트는
    /// 자기 카드 한 장의 표시와 슬라이드만 책임진다.
    ///
    /// ※ 이 오브젝트 자체는 VerticalLayoutGroup이 좌표를 관리하므로 움직이면 안 된다.
    ///   슬라이드는 한 단계 안쪽인 <see cref="_card"/>가 담당한다.
    /// </summary>
    public sealed class ChainCutInSlot : MonoBehaviour
    {
        [Header("카드")]
        [Tooltip("실제로 슬라이드하는 대상. 이 오브젝트(슬롯) 자신이 아니라 자식이어야 한다.")]
        [SerializeField] private RectTransform _card;
        [SerializeField] private CanvasGroup _cardGroup;

        [Tooltip("직업별 카드 배경을 토글한다. 파티 카드에도 직업 아이콘과 같은 방식을 쓴다.")]
        [SerializeField] private ClassIconSelector _classCard;

        [Header("내용")]
        [SerializeField] private WeaponCutInSelector _illustrations;
        [SerializeField] private TMP_Text _nameText;

        [Header("타이밍")]
        [Tooltip("카드가 숨겨질 때의 위치 오프셋. 우측에서 들어오려면 X를 양수로.")]
        [SerializeField] private Vector2 _slideOffset = new(600f, 0f);

        [SerializeField] private float _slideDuration = 0.18f;

        private Vector2 _shownPosition;
        private Sequence _sequence;
        private bool _prepared;

        /// <summary>
        /// 인스펙터에 배치된 위치를 "보이는 위치"로 기억하고 카드를 접어둔다.
        /// 슬롯은 꺼진 채 대기하므로 자신의 Awake를 믿을 수 없다 — 스택이 대신 불러준다.
        /// </summary>
        public void Prepare()
        {
            if (_prepared) return;
            if (_card == null || _cardGroup == null) return;

            _shownPosition = _card.anchoredPosition;
            _prepared = true;

            HideImmediate();
        }

        /// <summary>이 자리에 체인 기여자 한 명을 세운다. 카드가 우측에서 밀려 들어온다.</summary>
        public void Show(ClassType classType, int weaponId, string nickname, Color nameColor)
        {
            Prepare();
            if (!_prepared) return;

            _classCard?.Show(classType);
            _illustrations?.Show(weaponId);

            if (_nameText != null)
            {
                _nameText.text = nickname;
                _nameText.color = nameColor;
            }

            _sequence?.Kill();

            gameObject.SetActive(true);
            _card.anchoredPosition = _shownPosition + _slideOffset;
            _cardGroup.alpha = 0f;

            _sequence = DOTween.Sequence()
                .Insert(0f, MoveCard(_shownPosition, Ease.OutCubic))
                .Insert(0f, FadeCard(1f));
        }

        /// <summary>체인 종료 — 카드가 우측으로 빠지며 자리를 비운다.</summary>
        public void Hide()
        {
            if (!_prepared) return;
            if (!gameObject.activeSelf) return;

            _sequence?.Kill();

            _sequence = DOTween.Sequence()
                .Insert(0f, MoveCard(_shownPosition + _slideOffset, Ease.InCubic))
                .Insert(0f, FadeCard(0f))
                .OnComplete(HideImmediate);
        }

        /// <summary>트윈 없이 즉시 자리를 비운다(새 체인 시작 등 이전 카드를 남기면 안 될 때).</summary>
        public void HideNow()
        {
            if (!_prepared) return;

            _sequence?.Kill();
            HideImmediate();
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

            _card.anchoredPosition = _shownPosition;
            _cardGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private Tween MoveCard(Vector2 target, Ease ease)
        {
            return DOTween
                .To(
                    () => _card.anchoredPosition,
                    value => _card.anchoredPosition = value,
                    target,
                    _slideDuration)
                .SetEase(ease);
        }

        private Tween FadeCard(float target)
        {
            return DOTween.To(
                () => _cardGroup.alpha,
                value => _cardGroup.alpha = value,
                target,
                _slideDuration);
        }
    }
}
