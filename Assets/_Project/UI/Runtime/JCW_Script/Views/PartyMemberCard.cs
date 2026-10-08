using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 파티원 한 명의 카드. HP·얼티밋 게이지·직업·이름·버프/디버프를 묶는다.
    /// 이벤트 구독은 하지 않는다 — <see cref="PartyUiPanel"/>이 playerId로 찾아 값을 밀어준다.
    /// 인스펙터 슬롯은 모두 연결돼 있다고 가정한다.
    /// </summary>
    public sealed class PartyMemberCard : MonoBehaviour
    {
        [Header("스탯 바")]
        [SerializeField] private PlayerStatusBar _hpBar;

        [Header("얼티밋 게이지 (직업별 바 + 이펙트)")]
        [SerializeField] private UltimateGaugeView _ultimateGauge;

        [Header("직업 아이콘 / 닉네임")]
        [SerializeField] private ClassIconSelector _classIcon;
        [SerializeField] private TMP_Text _nameText;

        [Header("버프/디버프")]
        [SerializeField] private StatusEffectDisplay _statusEffects;

        public int PlayerId { get; private set; }

        /// <summary>카드를 특정 플레이어에 바인딩하고 초기 상태로 리셋한다.</summary>
        public void Bind(int playerId, string displayName)
        {
            PlayerId = playerId;
            _nameText.text = displayName;
            _statusEffects?.Clear();
        }

        /// <summary>무기 장착·교체 시 그 직업의 아이콘·얼티밋 세트만 켠다.</summary>
        public void SetClass(ClassType classType)
        {
            _classIcon.Show(classType);
            _ultimateGauge.SetClass(classType);
        }

        public void UpdateNickname(string nickname) => _nameText.text = nickname;

        public void SetHp(int current, int max) => _hpBar.SetValue(current, max);
        public void SetUltimate(int current, int max) => _ultimateGauge.SetValue(current, max);

        public void ApplyBuff(BuffInfo info) => _statusEffects?.ApplyBuff(info);
        public void RemoveBuff(BuffType type) => _statusEffects?.RemoveBuff(type);
        public void ApplyDebuff(DebuffInfo info) => _statusEffects?.ApplyDebuff(info);
        public void RemoveDebuff(DebuffType type) => _statusEffects?.RemoveDebuff(type);
    }
}
