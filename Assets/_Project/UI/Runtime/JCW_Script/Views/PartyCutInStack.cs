using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 체인 컷인 카드 목록. 궁극기를 쓴 순서대로 화면 우측에 카드가 위에서부터 차곡차곡 쌓인다.
    /// 로컬 플레이어도 이 목록에 포함된다 — 내 궁극기는 여기에 더해 <see cref="SkillCutIn"/>
    /// 중앙 연출까지 함께 터진다.
    ///
    /// 쌓이는 자리는 Combat이 보내는 체인 stage(1부터)가 그대로 결정한다 — UI가 따로 세지 않는다.
    /// 자리 배치는 VerticalLayoutGroup에 맡기고, 여기선 슬롯을 켜고 끄기만 한다
    /// (꺼진 슬롯은 레이아웃에서 빠지므로 위에서부터 채워진다).
    ///
    /// 값은 <see cref="HudOrchestrator"/>가 밀어준다(직접 이벤트 구독 안 함).
    /// </summary>
    public sealed class PartyCutInStack : MonoBehaviour
    {
        [Header("카드 슬롯 (위 → 아래 순서. 체인 최대 인원수만큼)")]
        [SerializeField] private ChainCutInSlot[] _slots;

        [Tooltip("목록 위에 붙는 체인 단계 표시(선택). 카드가 쌓일 때마다 다음 이미지로 바뀐다.")]
        [SerializeField] private ChainStageBanner _banner;

        [Header("닉네임 직업 색")]
        [Tooltip("체인 비네트와 같은 직업색 규칙을 쓴다 — 화면 전체가 같은 색으로 읽히도록.")]
        [SerializeField] private ClassColor[] _classColors;

        [SerializeField] private Color _fallbackNameColor = Color.white;

        [Header("풀체인")]
        [Tooltip("마지막 카드까지 쌓인 뒤 이 시간 동안은 거두라는 신호를 무시하고 전원을 보여준다. " +
                 "풀체인이 발동하면 Combat이 곧바로 체인을 비우기 때문에, 이게 없으면 마지막 카드가 뜨자마자 사라진다.")]
        [SerializeField] private float _fullChainHoldSeconds = 1.2f;

        // 0보다 크면 홀드 중. 이 시각이 지나면 스스로 거둔다.
        private float _holdUntil;

        [Serializable]
        private struct ClassColor
        {
            public ClassType ClassType;
            public Color Color;
        }

        private void Awake()
        {
            // 슬롯은 꺼진 채 대기하므로 자기 Awake가 돌지 않는다. 배치 좌표를 여기서 기억시킨다.
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Prepare();

            _banner?.Prepare();
        }

        private void OnDisable()
        {
            _holdUntil = 0f;
            ClearAllImmediate();
        }

        private void Update()
        {
            if (_holdUntil <= 0f) return;
            if (Time.unscaledTime < _holdUntil) return;

            _holdUntil = 0f;
            HideAllSlots();
        }

        /// <summary>
        /// 체인 stage번째 기여자를 그 자리에 세운다(stage 1 = 맨 위).
        /// stage 1은 새 체인의 시작이므로 이전 체인이 남아 있으면 먼저 비운다.
        /// 마지막 자리까지 찼다면 그 순간이 곧 풀체인 완성이므로 홀드를 건다 —
        /// 그래야 뒤따라오는 종료 신호(윈도우 만료·풀체인 소비)에 카드가 바로 지워지지 않는다.
        /// </summary>
        public void ShowLink(int stage, ClassType classType, int weaponId, string nickname, int windowSeconds)
        {
            int index = stage - 1;
            if (index < 0 || index >= _slots.Length) return;

            if (stage == 1)
            {
                _holdUntil = 0f;
                ClearAllImmediate();
            }

            _slots[index]?.Show(classType, weaponId, nickname, GetNameColor(classType));
            _banner?.Show(stage, windowSeconds);

            if (stage != _slots.Length) return;

            // 풀체인 완성 — 더 이을 링크가 없으니 카운트다운을 멈추고 잠시 붙잡는다.
            _banner?.StopWindow();
            _holdUntil = Time.unscaledTime + _fullChainHoldSeconds;
        }

        /// <summary>
        /// 체인 종료(윈도우 만료·풀체인 소비) — 쌓인 카드를 전부 거둔다.
        /// 풀체인 홀드 중이면 무시한다. 종료 신호가 여러 갈래로 오는데다 도착 순서가
        /// 보장되지 않으므로, 홀드가 스스로 끝나며 거두게 두는 편이 안전하다.
        /// </summary>
        public void ClearAll()
        {
            if (_holdUntil > 0f) return;

            HideAllSlots();
        }

        private void HideAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.Hide();

            _banner?.Hide();
        }

        private void ClearAllImmediate()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.HideNow();

            _banner?.HideNow();
        }

        private Color GetNameColor(ClassType classType)
        {
            for (int i = 0; i < _classColors.Length; i++)
            {
                if (_classColors[i].ClassType == classType) return _classColors[i].Color;
            }

            return _fallbackNameColor;
        }
    }
}
