using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// 광전사. 기본공격이 <b>적중할 때마다</b> "분노 스택"이 쌓이고, 스택은 <b>스킬에만</b> 공격속도로 붙는다.
    /// 스택은 10중첩까지 쌓이며, 마지막으로 쌓인 시점부터 10초가 지나면 한꺼번에 0으로 꺼진다
    /// (한 개씩 빠지지 않는다).
    public class Weapon_Berserker : WeaponBase
    {
        // WeaponRecord_5004의 Attack 배치 (각 AttackRecord의 DamageType 기준).
        // 0:Attack9 평타 · 1:Attack10 평타 · 2:Attack11 스킬 · 3:Attack12 궁극기 · 4:풀체인
        const int ComboCount = 2;
        const int SkillIdx = 2;
        const int UltimateIdx = 3;
        const int FullChainIdx = 4;

        // 분노 스택 튜닝값
        const int MaxRageStack = 10;
        const float RageDuration = 10f;
        const float SpeedPerStack = 0.1f;   // 10중첩 = 스킬 공격속도 2배

        [Networked] int CurComboIdx { get; set; }
        [Networked] TickTimer ComboPersistantTimer { get; set; }
        readonly float secBtwCombo = 2f;

        [Networked] public int RageStack { get; private set; }
        [Networked] TickTimer RageTimer { get; set; }

        /// 이미 분노를 준 공격의 시작 틱. 다단히트 공격이 한 번에 여러 스택을 주지 않게 막는다
        /// (스윙 1회 = 최대 1스택).
        [Networked] int RagedAttackTick { get; set; }

        /// 스킬 시전 구간 동안만 무기 공격속도를 올려 둔 상태인지.
        /// 시전이 끝나면 1로 되돌려서 평타·궁극기에 배율이 새지 않게 한다.
        [Networked] NetworkBool SkillBoosted { get; set; }

        public override bool CanAttack
        {
            get => CooldownTimers[CurComboIdx].Expired(Runner);
            protected set { }
        }
        public override bool CanSkill
        {
            get => CooldownTimers[SkillIdx].Expired(Runner);
            protected set { }
        }
        public override bool CanUltimate
        {
            get => Gauge == MaxGauge
                && CooldownTimers[UltimateIdx].Expired(Runner);
            protected set { }
        }

        public override bool IsAttackFinished() => DurationTimers[CurComboIdx].ExpiredOrNotRunning(Runner);
        public override bool IsSkillFinished() => DurationTimers[SkillIdx].ExpiredOrNotRunning(Runner);
        public override bool IsUltimateFinished() => DurationTimers[UltimateIdx].ExpiredOrNotRunning(Runner);

        float RageMultiplier => 1f + RageStack * SpeedPerStack;

        public override void Spawned()
        {
            base.Spawned();

            if (!HasStateAuthority) return;

            CooldownTimers.Set(CurComboIdx,
                TickTimer.CreateFromSeconds(Runner, Entry(CurComboIdx).Cooldown));
            CooldownTimers.Set(SkillIdx,
                TickTimer.CreateFromSeconds(Runner, Entry(SkillIdx).Cooldown));
            CooldownTimers.Set(UltimateIdx,
                TickTimer.CreateFromSeconds(Runner, Entry(UltimateIdx).Cooldown));
            ComboPersistantTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);

            RageStack = 0;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            if (!HasStateAuthority) return;

            // 유지시간이 끝나면 쌓인 만큼이 한 번에 사라진다.
            if (RageStack > 0 && RageTimer.Expired(Runner))
            {
                RageStack = 0;
                RageTimer = default;
            }

            EndSkillBoostIfDone();
        }

        /// 스킬이 끝났으면 공격속도를 1로 되돌린다. 타임라인(타격·이펙트)이 아직 남아 있으면
        /// 도중에 느려져 뒷 타가 밀리므로, 타임라인까지 다 끝난 뒤에 푼다.
        void EndSkillBoostIfDone()
        {
            if (!HasStateAuthority) return;
            if (!SkillBoosted) return;
            if (AttackStartTick[SkillIdx] > 0) return;
            if (!DurationTimers[SkillIdx].ExpiredOrNotRunning(Runner)) return;

            SetAttackSpeed(1f);
            SkillBoosted = false;
        }

        public override void OnAttackTick()
        {
            if (!HasStateAuthority) return;
            if (!CooldownTimers[CurComboIdx].Expired(Runner)) return;

            CurComboIdx = ComboPersistantTimer.Expired(Runner) ? 0 : (CurComboIdx + 1) % ComboCount;
            var entry = Entry(CurComboIdx);

            CooldownTimers.Set(CurComboIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Cooldown));
            DurationTimers.Set(CurComboIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Normal, entry.Cooldown);
            BeginAttack(CurComboIdx);
            ComboPersistantTimer = TickTimer.CreateFromSeconds(Runner, secBtwCombo);

            var attackParams = new DealAreaParams()
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                damage = GetDamage(entry.Damage),
                applyCnt = 30
            };

            ScheduleAttack(CurComboIdx, DealLogicType.RangeWidth, attackParams, DamageType.Normal, true);

            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }

        /// 분노는 <b>적중했을 때만</b> 쌓인다 — 허공에 휘두르면 아무 일도 없다.
        /// 기본공격에만 반응한다(스킬·궁극기 적중으로는 안 쌓임).
        protected override void OnHitLanded(int idx, int targetCount)
        {
            if (!HasStateAuthority) return;   // 분노는 서버 권한 (ADR-001) — 호출부도 서버 분기지만 여기서도 막는다
            if (idx >= ComboCount) return;

            // 다단히트 공격이라도 이 스윙에서 이미 받았으면 넘어간다(스윙 1회 = 최대 1스택).
            int startTick = AttackStartTick[idx];
            if (startTick == RagedAttackTick) return;
            RagedAttackTick = startTick;

            // 최대치에서도 유지시간은 갱신된다(계속 맞히면 계속 유지).
            RageStack = Mathf.Min(MaxRageStack, RageStack + 1);
            RageTimer = TickTimer.CreateFromSeconds(Runner, RageDuration);
        }

        public override void OnSkillPressed()
        {
            if (!HasStateAuthority) return;
            if (!CooldownTimers[SkillIdx].Expired(Runner)) return;

            // 배율을 먼저 걸어야 Entry()가 계산하는 쿨타임·지속·타격 시점에 반영된다.
            // 스택이 시전 도중 꺼져도 이 값은 그대로라, 진행 중인 타임라인이 안 밀린다.
            SetAttackSpeed(RageMultiplier);
            SkillBoosted = true;

            var entry = Entry(SkillIdx);

            CooldownTimers.Set(SkillIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Cooldown));
            DurationTimers.Set(SkillIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Skill, entry.Cooldown);
            BeginAttack(SkillIdx);

            var skillParams = new DealAreaParams()
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                damage = GetDamage(entry.Damage),
                applyCnt = 30
            };

            ScheduleAttack(SkillIdx, DealLogicType.RangeWidth, skillParams, DamageType.Normal, true);

            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }

        public override void OnUltimatePressed()
        {
            if (!HasStateAuthority) return;
            if (!CooldownTimers[UltimateIdx].Expired(Runner)) return;

            var entry = Entry(UltimateIdx);

            CooldownTimers.Set(UltimateIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Cooldown));
            DurationTimers.Set(UltimateIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Ultimate, entry.Cooldown);
            BeginAttack(UltimateIdx);

            var ultParams = new DealAreaParams()
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                damage = GetDamage(entry.Damage),
                applyCnt = 30
            };

            ScheduleAttack(UltimateIdx, DealLogicType.RangeArc, ultParams, DamageType.Ultimate, false);

            Gauge = 0;
            HookFullChain();
            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }

        public override void FullChainAction(Vector3 anchor)
        {
            var entry = Entry(FullChainIdx);
            SetFullChainAnchor(FullChainIdx, anchor);

            // BeginAttack을 쓰지 않는다 — 애니 배속·전진 요청까지 같이 나가면 풀체인이 터지는
            // 순간 진행 중인 동작이 흐트러진다. 타격·이펙트 타임라인만 연다.
            AttackStartTick.Set(FullChainIdx, Runner.Tick);
            HitsFired.Set(FullChainIdx, 0);
            ParticlesFired.Set(FullChainIdx, 0);

            var fullchainParams = new DealAreaParams()
            {
                origin = anchor,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                damage = GetDamage(entry.Damage),
                applyCnt = 30
            };

            ScheduleAttack(FullChainIdx, DealLogicType.RangeArc, fullchainParams, DamageType.FullChain, false);
        }
    }
}
