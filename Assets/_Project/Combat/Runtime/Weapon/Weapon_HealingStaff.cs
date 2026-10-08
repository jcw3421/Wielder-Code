using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    public class Weapon_HealingStaff : WeaponBase
    {
        [Networked] int CurComboIdx { get; set; }
        [Networked] TickTimer ComboPersistantTimer { get; set; }
        readonly float secBtwCombo = 2f;

        const int ComboCount = 3;
        const int SkillIdx = 3;
        const int UltimateIdx = 6;
        const int FullChainIdx = 7;
        bool SkillUsing;

        [Networked] NetworkBool ChargeEffectActive { get; set; }
        EffectHandle _chargeEffect;
        Vector3 _chargeOffset;
        Quaternion _chargeRotation;

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
        public override bool IsSkillFinished() => SkillUsing == false && DurationTimers[SkillIdx + 2].ExpiredOrNotRunning(Runner);
        public override bool IsUltimateFinished() => DurationTimers[UltimateIdx].ExpiredOrNotRunning(Runner);

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
            SkillUsing = true;
            var entry = Entry(SkillIdx);

            CooldownTimers.Set(SkillIdx + 1,
                TickTimer.CreateFromSeconds(Runner, entry.Duration)); // 차징 시작 시점을 차징 시작 애니메이션 지속시간 만큼 미룸. 

            DurationTimers.Set(SkillIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            BeginAttack(SkillIdx);

            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }
        /// 대시·CC로 행동이 끊기면 차징도 버린다. 차징 오라는 Render가 이 값을 보고 그리므로,
        /// 여기서 안 끄면 캔슬하고 빠져나간 자리에 이펙트만 남는다.
        protected override void OnCancel()
        {
            if (!HasStateAuthority) return;

            // 차징 중에 끊겼다면 쿨타임을 여기서 건다. 이 무기는 차징 루프 자체가 힐을 뿌리므로,
            // 릴리즈에서만 쿨을 걸면 "차징 → 힐 → 대시 캔슬 → 즉시 재시전"으로 쿨 없이 무한히 힐할 수 있다.
            if (SkillUsing)
            {
                float cooldown = Entry(SkillIdx).Cooldown;
                CooldownTimers.Set(SkillIdx, TickTimer.CreateFromSeconds(Runner, cooldown));
                // 쿨이 걸렸으면 UI에도 알린다 — 이 통지가 릴리즈 경로에만 있으면
                // 대시로 끊었을 때 실제로는 잠겼는데 아이콘은 계속 켜져 있다.
                RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Skill, cooldown);
            }

            SkillUsing = false;
            ChargeEffectActive = false;
        }

        public override void OnSkillTick()
        {
            if (!HasStateAuthority) return;
            if (!SkillUsing) return;
            if (!CooldownTimers[SkillIdx + 1].Expired(Runner)) return;

            var entry = Entry(SkillIdx + 1);
            CooldownTimers.Set(SkillIdx + 1,
                TickTimer.CreateFromSeconds(Runner, entry.Cooldown));   // 차징 중에, 애니메이션 1개 루프 진행될 때마다 힐 들어감. 
            ChargeEffectActive = true;

            BuffAreaParams param = new BuffAreaParams
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                applyCnt = 30,
                buff = new BuffInfo(BuffType.Heal, (int)GetDamage(entry.Damage), 0, false, Object.InputAuthority.PlayerId)
            };

            ScheduleBuff(SkillIdx + 1, BuffLogicType.RangeArc, param);
            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }
        public override void OnSkillReleased()
        {
            if (!HasStateAuthority) return;
            ChargeEffectActive = false;
            if (SkillUsing == false) return;

            var entry = Entry(SkillIdx + 2);

            CooldownTimers.Set(SkillIdx,   // 차징 종료시점에, 스킬 시작(SkillIdx + 0) 쿨타임을 초기화함. 
                TickTimer.CreateFromSeconds(Runner, Entry(SkillIdx).Cooldown));
            DurationTimers.Set(SkillIdx + 2,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Skill, Entry(SkillIdx).Cooldown);
            BeginAttack(SkillIdx + 2);

            BuffAreaParams param = new BuffAreaParams
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                applyCnt = 30,
                buff = new BuffInfo(BuffType.DamageIncrese, 10, 20, false, Object.InputAuthority.PlayerId)
            };

            ScheduleBuff(SkillIdx + 2, BuffLogicType.RangeArc, param);
            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
            SkillUsing = false;
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

            foreach (var playerRef in Runner.ActivePlayers)
            {
                int playerId = playerRef.PlayerId;
                GameplayEvents.RaisePlayerReviveRequested(playerId);
            }

            Gauge = 0;
            HookFullChain(onCast: true);   // 지원형 궁 — 데미지 판정이 없어 적중을 기다릴 수 없다
            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }
        /// 힐러가 체인 스타터일 때의 풀체인 — 아군 공격력 증가.
        /// 대상이 아군이라 anchor(적 위치)를 쓰지 않는다. 시전자 주변을 기준으로 잡는다.
        public override void FullChainAction(Vector3 anchor)
        {
            var entry = Entry(FullChainIdx);

            var param = new BuffAreaParams
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                applyCnt = 30,
                buff = new BuffInfo(
                    BuffType.DamageIncrese,
                    Mathf.RoundToInt(entry.Damage),                          // 버프 수치는 레코드의 Damage 칸을 쓴다
                    Mathf.RoundToInt(entry.Duration * Runner.TickRate),      // 지속(초) → 틱
                    false,
                    Object.InputAuthority.PlayerId)
            };

            ScheduleBuff(FullChainIdx, BuffLogicType.RangeArc, param);
        }

        public override void Render()
        {
            base.Render();

            if (!ChargeEffectActive)
            {
                _chargeEffect.Stop(clear: true);   // 남은 파티클까지 즉시 지운다 — 차징 오라는 손을 떼면 그 자리에 남으면 안 된다
                _chargeEffect = default;
                return;
            }

            if (!_chargeEffect.IsPlaying) PlayChargeEffect();
            _chargeEffect.SetTransform(
                transform.position + transform.rotation * _chargeOffset,
                transform.rotation * _chargeRotation);
        }

        private void PlayChargeEffect()
        {
            var record = data.Attacks[SkillIdx + 1];
            for (int i = 0; i < record.ParticleCount; i++)
            {
                var particle = record.Particle(i);
                if (!particle.Sustained) continue;
                if (particle.Prefab == null || !particle.Prefab.TryGetComponent(out ParticleGroup group)) continue;

                _chargeOffset = particle.Offset;
                _chargeRotation = Quaternion.Euler(particle.Euler);
                _chargeEffect = EffectPool.Instance.Play(group,
                    transform.position + transform.rotation * _chargeOffset,
                    transform.rotation * _chargeRotation,
                    particle.SafeSpeed);
                return;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Object != null && HasStateAuthority) ChargeEffectActive = false;
            _chargeEffect.Stop(clear: true);   // 남은 파티클까지 즉시 지운다 — 차징 오라는 손을 떼면 그 자리에 남으면 안 된다
            _chargeEffect = default;
        }
    }
}
