using DG.Tweening;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 로컬 플레이어(나) HUD 총괄. HP·얼티밋 게이지·직업·스킬 슬롯·버프/디버프를 관리한다.
    /// 값은 <see cref="HudOrchestrator"/>가 밀어준다.
    ///
    /// 스킬 슬롯은 쿨타임 이벤트가 아직 Core에 없어, 입력/전투 쪽에서 <see cref="TriggerSkillCooldown"/>을
    /// 호출해 구동한다. (추후 Core 쿨타임 이벤트가 생기면 Orchestrator가 이 메서드로 연결)
    /// </summary>
    public sealed class PlayerUiPanel : MonoBehaviour
    {
        /// <summary>스킬 슬롯 종류. 배열 인덱스가 아니라 의미로 참조하기 위한 키.</summary>
        public enum SkillSlotId
        {
            LeftClick = 0,   // 좌클릭(기본 공격)
            RightClick = 1,  // 우클릭
            Slot1 = 2,
            Slot2 = 3,
            Slot3 = 4,
        }

        [Header("스탯 바")]
        [SerializeField] private PlayerStatusBar _hpBar;

        [Header("스테미너 (대시 자원)")]
        [Tooltip("대시에 쓰는 자원. 최대치·소모량은 장착 무기가 정한다.")]
        [SerializeField] private PlayerStatusBar _staminaBar;

        [Header("얼티밋 게이지 (직업별 바 + 이펙트)")]
        [SerializeField] private UltimateGaugeView _ultimateGauge;

        [Header("정보")]
        [SerializeField] private ClassIconSelector _classIcon;
        [SerializeField] private TMP_Text _nameText;

        [Header("스킬 슬롯 (좌클릭, 우클릭, 슬롯1~3 순서)")]
        [SerializeField] private SkillSlot[] _skillSlots;

        [Header("버프/디버프")]
        [SerializeField] private StatusEffectDisplay _statusEffects;

        [Header("경험치 / 레벨")]
        [Tooltip("경험치 바. 채움 = 현재 경험치 / 다음 레벨까지 필요량.")]
        [SerializeField] private PlayerStatusBar _expBar;
        [Tooltip("레벨 표시. 예: \"Lv.5\".")]
        [SerializeField] private TMP_Text _levelText;

        public void Bind(string displayName)
        {
            _nameText.text = displayName;
            _statusEffects?.Clear();   // 상태이상 표시는 아직 미배치일 수 있어 방어
        }

        /// <summary>무기 장착·교체 시 그 직업의 아이콘·얼티밋 세트만 켠다.</summary>
        public void SetClass(ClassType classType)
        {
            _classIcon.Show(classType);
            _ultimateGauge.SetClass(classType);
        }

        public void SetHp(int current, int max) => _hpBar.SetValue(current, max);

        public void SetUltimate(int current, int max) => _ultimateGauge.SetValue(current, max);

        /// 스테미너 바는 아직 미배치일 수 있어 방어한다(HP·얼티밋과 달리 나중에 붙은 요소).
        public void SetStamina(int current, int max) => _staminaBar?.SetValue(current, max);

        public void ApplyBuff(BuffInfo info) => _statusEffects?.ApplyBuff(info);
        public void RemoveBuff(BuffType type) => _statusEffects?.RemoveBuff(type);
        public void ApplyDebuff(DebuffInfo info) => _statusEffects?.ApplyDebuff(info);
        public void RemoveDebuff(DebuffType type) => _statusEffects?.RemoveDebuff(type);

        /// <summary>
        /// 경험치 갱신. 바는 (현재 경험치 / 다음 레벨까지)로 채우고, 레벨 텍스트도 함께 맞춘다.
        /// PlayerExpChanged 이벤트 하나로 바·레벨을 모두 그린다.
        /// </summary>
        public void SetExp(int currentExp, int expToNext, int level)
        {
            _expBar.SetValue(currentExp, expToNext);
            SetLevelText(level);
        }

        /// <summary>레벨업 순간. 레벨 텍스트를 갱신하고 살짝 튀는 축하 연출을 준다.</summary>
        public void PlayLevelUp(int newLevel)
        {
            SetLevelText(newLevel);
            PlayLevelUpFlash();
        }

        private void SetLevelText(int level) => _levelText.text = $"Lv.{level}";

        private void PlayLevelUpFlash()
        {
            if (!isActiveAndEnabled) return;
            _levelText.transform.DOKill();
            _levelText.transform.localScale = Vector3.one;
            _levelText.transform.DOPunchScale(Vector3.one * 0.3f, 0.4f, vibrato: 6, elasticity: 0.6f);
        }

        /// <summary>지정 슬롯 쿨타임 시작.</summary>
        public void TriggerSkillCooldown(SkillSlotId slot, float seconds)
        {
            int index = (int)slot;
            if (index < 0 || index >= _skillSlots.Length) return;
            _skillSlots[index].TriggerCooldown(seconds);
        }
    }
}
