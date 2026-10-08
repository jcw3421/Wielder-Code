using Fusion;
using Game.Combat.Chain;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    public class TestSwordWeapon : WeaponBase
    {
        // 콤보는 이 무기 고유 로직
        [Networked] int CurComboIdx { get; set; }
        [Networked] TickTimer ComboPersistantTimer { get; set; }
        readonly float secBtwCombo = 2f;

        const int ComboCount = 4;
        const int SkillIdx = 4;
        const int UltimateIdx = 5;
        const int FullChainIdx = 6;

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
        public override void OnSkillPressed()
        {
            if (!HasStateAuthority) return;
            if (!CooldownTimers[SkillIdx].Expired(Runner)) return;

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

            ScheduleAttack(UltimateIdx, DealLogicType.RangeWidth, ultParams, DamageType.Ultimate, false);
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
        public override bool IsAttackFinished() => DurationTimers[CurComboIdx].ExpiredOrNotRunning(Runner);
        public override bool IsSkillFinished() => DurationTimers[SkillIdx].ExpiredOrNotRunning(Runner);
        public override bool IsUltimateFinished() => DurationTimers[UltimateIdx].ExpiredOrNotRunning(Runner);
    }
}
