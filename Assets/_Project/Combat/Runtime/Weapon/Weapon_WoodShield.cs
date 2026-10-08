using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    public class Weapon_WoodShield : WeaponBase
    {
        [Networked] int CurComboIdx { get; set; }
        [Networked] TickTimer ComboPersistantTimer { get; set; }
        readonly float secBtwCombo = 4f;

        const int ComboCount = 4;
        const int SkillIdx = 4;
        const int UltimateIdx = 7;
        const int FullChainIdx = 8;
        bool SkillUsing;

        // 막기: 홀드하는 동안 받는 피해를 절반으로 줄인다.
        // 저스트가드: 막기 시작 구간(막기시작 애니메이션 길이) 안에 맞으면 완전 무효.
        const int BlockDamagePercent = 50;
        const int JustGuardDamagePercent = 0;

        [Networked] NetworkBool Blocking { get; set; }
        [Networked] TickTimer JustGuardWindow { get; set; }

        [Header("막기 스테미너")]
        [SerializeField, Tooltip("막기를 시작할 때 한 번 깎이는 양(포인트).")]
        private int blockStartCost = 15;
        [SerializeField, Tooltip("막고 있는 동안 초당 깎이는 양(포인트/초). 0이면 홀드 자체는 공짜.")]
        private float blockDrainPerSecond = 12f;
        [SerializeField, Tooltip("일반 막기 성공 시 돌려받는 양(포인트).")]
        private int guardRefund = 10;
        [SerializeField, Tooltip("저스트가드(완전 무효) 성공 시 돌려받는 양(포인트).")]
        private int justGuardRefund = 25;

        [Header("반격 찌르기")]
        [SerializeField, Tooltip("시트의 반격 행 이름. 이 행이 반격의 애니·이펙트·전진·데미지를 갖는다.")]
        private string counterAttackName = "반격찌르기";
        [SerializeField, Tooltip("막기 성공 후 반격을 넣을 수 있는 시간(초). 일반·저스트가드 공통.")]
        private float counterWindowSeconds = 1.5f;
        [SerializeField, Tooltip("반격이 나갔을 때 돌려받는 양(포인트).")]
        private int counterRefund = 15;

        // 막기 성공으로 열린 반격 창. 만료되면 그냥 사라진다(별도 보상 없음).
        // 막고 있는 동안에는 계속 갱신되고, 방패를 내린 시점부터 counterWindowSeconds 동안 살아 있다.
        [Networked] TickTimer CounterWindow { get; set; }

        // 이번 막기에서 한 번이라도 막아냈는가. 홀드 중 창을 살려두는 근거.
        [Networked] NetworkBool CounterArmed { get; set; }

        /// 시트에서 찾은 반격 행. -1이면 반격이 꺼진다.
        private int _counterIdx = -1;

        /// 반격 성공 시 돌려받는 스테미너. UI 표시용(프로필 패널) — 방패에만 있는 항목이다.
        public int CounterRefund => counterRefund;

        [Networked] NetworkBool ChargeEffectActive { get; set; }
        EffectHandle _chargeEffect;
        Vector3 _chargeOffset;
        Quaternion _chargeRotation;

        /// 반격 창이 열려 있으면 콤보 쿨타임과 무관하게 평타 입력을 받는다 — 그 입력이 곧 반격이다.
        public override bool CanAttack
        {
            get => CounterReady || CooldownTimers[CurComboIdx].Expired(Runner);
            protected set { }
        }
        /// <summary>막기는 시작 비용을 낼 수 있어야 들어간다 — 스테미너가 바닥이면 방패를 들지 못한다.
        /// 반격의 타격이 아직 남아 있으면 올리지 못한다 — 평타를 끊을 때와 같은 규칙이다
        /// (모션만 덮이고 데미지는 들어가는 상황을 막는다).</summary>
        public override bool CanSkill
        {
            get => CooldownTimers[SkillIdx].Expired(Runner)
                && Stamina != null && Stamina.CanSpend(blockStartCost)
                && (_counterIdx < 0 || HitsDone(_counterIdx));
            protected set { }
        }
        public override bool CanUltimate
        {
            get => Gauge == MaxGauge
                && CooldownTimers[UltimateIdx].Expired(Runner);
            protected set { }
        }
        /// 막는 동안만 감면된다. 저스트가드 창 안이면 완전 무효, 그 뒤 홀드는 절반.
        public override int IncomingDamagePercent
        {
            get
            {
                if (!Blocking) return 100;

                return JustGuardWindow.ExpiredOrNotRunning(Runner)
                    ? BlockDamagePercent
                    : JustGuardDamagePercent;
            }
        }

        /// 막는 동안은 경직·기절에 끊기지 않는다.
        public override CCType CurrentImmunityUpTo => Blocking ? CCType.Stun : CCType.None;

        /// <summary>방패는 평타를 끊고 즉시 막기로 넘어갈 수 있다 — 방어가 한 박자 늦으면 탱커가 성립하지 않는다.
        /// 단 <b>타격이 다 나간 뒤</b>(후딜)에만 연다. 그래야 평타 데미지는 그대로 들어가고
        /// 마무리 모션만 잘려서, 평타를 헛치게 만들지 않으면서 연계가 매끄러워진다.</summary>
        public override bool CanCancelIntoSkill
        {
            get
            {
                for (int i = 0; i < ComboCount; i++)
                    if (!HitsDone(i)) return false;

                // 반격도 평타 상태에서 나가므로 같이 본다 — 빼먹으면 반격 선딜에 방패가 올라가
                // 데미지도 안 들어간 채로 모션만 끊긴다(반격 인덱스는 콤보 범위 밖이라 위 루프에 안 걸린다).
                return _counterIdx < 0 || HitsDone(_counterIdx);
            }
        }

        /// 그 공격이 진행 중이 아니거나, 진행 중이어도 타격이 전부 나갔는가.
        private bool HitsDone(int idx)
            => AttackStartTick[idx] <= 0 || HitsFired[idx] >= data.Attacks[idx].HitCount;

        /// <summary>막아낸 순간 스테미너를 일부 돌려주고, 반격 창을 연다 — 잘 막을수록 오래 버티고 되받아친다.
        /// 저스트가드(완전 무효)는 스테미너를 더 많이 돌려받는다(반격 창 길이는 같다).</summary>
        public override void OnGuardSucceeded(bool justGuard)
        {
            Stamina.Restore(justGuard ? justGuardRefund : guardRefund);

            // 아래 두 줄: 호출부 PlayerLifeCycle.ApplyDamage()가 HasStateAuthority로 막는다.
            CounterArmed = true;   // guardrail:allow AUTH
            CounterWindow = TickTimer.CreateFromSeconds(Runner, counterWindowSeconds);   // guardrail:allow AUTH
        }

        /// <summary>막기를 유지한 채 좌클릭이 들어온 순간. 반격 창이 열려 있을 때만 반격 찌르기가 나간다.
        /// 창이 닫혀 있으면 아무 일도 없다 — 막는 중에는 평타가 나가지 않는다.
        ///
        /// 반격 뒤에도 <see cref="Blocking"/>을 유지한다(테라 창기사와 같은 결) —
        /// 상태는 계속 Skill이고, 반격 모션이 끝나면 OnSkillTick이 '막는중' 루프로 되돌린다.</summary>
        public override void OnAttackDuringSkill()
        {
            if (!HasStateAuthority) return;
            if (!SkillUsing) return;

            PerformCounter();
        }

        /// 반격을 쓸 수 있는 상태인가. 막기를 놓은 뒤에도 창이 남아 있는 동안은 유효하다.
        private bool CounterReady =>
            _counterIdx >= 0 && !CounterWindow.ExpiredOrNotRunning(Runner);

        // 반격은 서버에서만 판정한다. 각 피어의 UI가 알아야 하므로 다른 무기 이벤트와 같이 RPC로 알린다.
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RaiseCounterAttacked(int playerId)
            => GameplayEvents.RaiseCounterAttacked(playerId);

        /// <summary>반격 찌르기 발동(서버). 막기를 유지한 채든, 이미 놓은 뒤든 같은 경로를 탄다.</summary>
        private void PerformCounter()
        {
            if (!CounterReady) return;

            // 아래 두 줄: 호출부(OnAttackDuringSkill/OnAttackTick)가 HasStateAuthority로 막는다.
            CounterWindow = default;   // 한 번 열린 창은 한 번만 쓴다  guardrail:allow AUTH
            CounterArmed = false;   // guardrail:allow AUTH

            var entry = Entry(_counterIdx);
            DurationTimers.Set(_counterIdx, TickTimer.CreateFromSeconds(Runner, entry.Duration));

            // 막기를 유지한 채 반격했다면, 모션이 끝날 때까지 '막는중' 루프를 미뤄 둔다
            // — 안 그러면 다음 틱에 덮어써진다.
            if (SkillUsing)
                CooldownTimers.Set(SkillIdx + 1, TickTimer.CreateFromSeconds(Runner, entry.Duration));

            RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Skill, 0f);
            RPC_RaiseCounterAttacked(Object.InputAuthority.PlayerId);
            BeginAttack(_counterIdx);

            var attackParams = new DealAreaParams()
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                damage = GetDamage(entry.Damage),
                applyCnt = 30
            };

            ScheduleAttack(_counterIdx, DealLogicType.RangeWidth, attackParams, DamageType.Skill, true);
            Stamina.Restore(counterRefund);

            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.05f);
        }

        /// CC 등으로 행동이 끊기면 막기도 풀린다 — 안 풀면 감면이 계속 남는다.
        protected override void OnCancel()
        {
            if (!HasStateAuthority) return;

            Blocking = false;
            JustGuardWindow = default;
            SkillUsing = false;
            CounterWindow = default;   // 막기가 끊기면 반격 기회도 사라진다
            CounterArmed = false;
            // 차징 오라도 같이 끈다 — 이 값이 남으면 Render가 계속 이펙트를 살려둬서,
            // 캔슬하고 빠져나간 자리에 이펙트만 덩그러니 남는다.
            ChargeEffectActive = false;
        }

        /// <summary>반격도 평타 상태에서 나갈 수 있으므로 그 지속까지 같이 본다.
        /// 콤보 타이머만 보면 반격이 시작된 틱에 곧바로 "공격 끝"이 되어, 상태가 Idle로 빠지면서
        /// 상태머신이 Locomotion을 덮어씌운다 — 데미지는 들어가는데 모션만 잘리는 증상이 난다.</summary>
        public override bool IsAttackFinished()
        {
            if (!DurationTimers[CurComboIdx].ExpiredOrNotRunning(Runner)) return false;
            if (_counterIdx >= 0 && !DurationTimers[_counterIdx].ExpiredOrNotRunning(Runner)) return false;

            return true;
        }
        /// <summary>막기가 끝났는지. 반격 지속도 함께 본다 — 반격 도중에 방패를 내리면 상태가 Idle로 빠지면서
        /// 상태머신이 Locomotion을 덮어씌워, 데미지는 들어가는데 모션만 사라진다.</summary>
        public override bool IsSkillFinished()
        {
            if (SkillUsing) return false;
            if (!DurationTimers[SkillIdx + 2].ExpiredOrNotRunning(Runner)) return false;
            if (_counterIdx >= 0 && !DurationTimers[_counterIdx].ExpiredOrNotRunning(Runner)) return false;

            return true;
        }
        public override bool IsUltimateFinished() => DurationTimers[UltimateIdx].ExpiredOrNotRunning(Runner);

        public override void Spawned()
        {
            base.Spawned();

            // 반격 행은 대시와 같은 방식으로 이름으로 찾는다. 없으면 반격만 꺼지고 나머지는 그대로 돈다.
            _counterIdx = FindAttackIndex(data.Attacks, counterAttackName);

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

            // 막기 성공으로 창이 열려 있으면 평타 대신 반격이 나간다(막기를 이미 놓았어도 된다).
            if (CounterReady)
            {
                PerformCounter();
                return;
            }

            // 반격이 재생되는 동안은 평타를 막는다. 클릭 한 번이 여러 틱에 걸쳐 있어서,
            // 이걸 안 막으면 반격이 나간 다음 틱에 평타가 겹쳐 나가고 모션까지 덮어쓴다.
            if (_counterIdx >= 0 && !DurationTimers[_counterIdx].ExpiredOrNotRunning(Runner)) return;

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

            // 반격 타격이 남아 있으면 방패를 못 올린다. 상태 전이(CanSkill)를 거치지 않고
            // 스킬 상태 안에서 바로 눌린 경우가 있어, 같은 규칙을 여기서도 막는다.
            if (_counterIdx >= 0 && !HitsDone(_counterIdx)) return;

            if (!Stamina.TrySpend(blockStartCost)) return;   // 방패를 드는 것 자체가 스테미너를 쓴다

            SkillUsing = true;
            var entry = Entry(SkillIdx);

            // 막기 시작. 저스트가드 창은 "막기시작" 애니메이션 길이와 같다 —
            // 창 길이를 바꾸려면 시트의 막기시작 Duration을 조절한다.
            Blocking = true;
            JustGuardWindow = TickTimer.CreateFromSeconds(Runner, entry.Duration);

            CooldownTimers.Set(SkillIdx + 1,
                TickTimer.CreateFromSeconds(Runner, entry.Duration)); // 차징 시작 시점을 차징 시작 애니메이션 지속시간 만큼 미룸. 

            DurationTimers.Set(SkillIdx,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            BeginAttack(SkillIdx);

            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }
        public override void OnSkillTick()
        {
            if (!HasStateAuthority) return;
            if (!SkillUsing) return;

            // 막고 있는 동안에는 반격 창이 닳지 않는다 — 카운트다운은 방패를 내린 순간부터 시작된다.
            if (CounterArmed)
                CounterWindow = TickTimer.CreateFromSeconds(Runner, counterWindowSeconds);

            // 들고 있는 동안 계속 깎인다. 바닥나면 버튼을 떼지 않아도 막기가 풀린다 —
            // 종료 경로는 손을 뗀 것과 같다(막기끝 모션·쿨타임 그대로).
            if (!Stamina.TryDrain(blockDrainPerSecond))
            {
                OnSkillReleased();
                return;
            }

            if (!CooldownTimers[SkillIdx + 1].Expired(Runner)) return;

            var entry = Entry(SkillIdx + 1);
            CooldownTimers.Set(SkillIdx + 1,
                TickTimer.CreateFromSeconds(Runner, entry.Cooldown));   // 막는중 애니메이션을 루프로 다시 재생
            ChargeEffectActive = true;

            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }
        public override void OnSkillReleased()
        {
            if (!HasStateAuthority) return;
            ChargeEffectActive = false;
            if (SkillUsing == false) return;

            // 막기 종료 — 손을 떼는 순간 감면이 끝난다(막기끝 모션 중에는 이미 무방비).
            Blocking = false;
            JustGuardWindow = default;
            // 반격 창은 남겨 둔다 — 막기를 놓은 뒤에도 창이 살아 있는 동안은 좌클릭으로 반격이 나간다.

            CooldownTimers.Set(SkillIdx,   // 차징 종료시점에, 스킬 시작(SkillIdx + 0) 쿨타임을 초기화함.
                TickTimer.CreateFromSeconds(Runner, Entry(SkillIdx).Cooldown));

            // 반격이 재생 중이면 막기끝 모션을 내지 않는다 — 내면 시작된 반격 클립을 그대로 덮어써서
            // 데미지만 들어가고 모션은 안 보인다. 방패는 이미 내려갔고(Blocking=false) 반격만 마저 나간다.
            if (_counterIdx >= 0 && !DurationTimers[_counterIdx].ExpiredOrNotRunning(Runner))
            {
                SkillUsing = false;
                return;
            }

            var entry = Entry(SkillIdx + 2);
            DurationTimers.Set(SkillIdx + 2,
                TickTimer.CreateFromSeconds(Runner, entry.Duration));
            RPC_RaiseWeaponUsed(Object.InputAuthority.PlayerId, ID, (int)DamageType.Skill, Entry(SkillIdx).Cooldown);
            BeginAttack(SkillIdx + 2);

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
            var ultBuffParams = new BuffAreaParams()
            {
                origin = transform.position,
                direction = transform.forward,
                range_distance = entry.Range,
                range_angle = entry.Angle,
                applyCnt = 4,
                buff = new BuffInfo(BuffType.Heal, 1, 0, false, Object.InputAuthority.PlayerId)
            };

            BuffApplier.ApplyBuff(_buffables, BuffLogicType.RangeArc, ultBuffParams);
            _buffables.Clear();

            ScheduleAttack(UltimateIdx, DealLogicType.RangeWidth, ultParams, DamageType.Ultimate, false);

            Gauge = 0;
            HookFullChain();
            RaiseAnimationPlay(Animator.StringToHash(entry.AnimName), 0.1f);
        }
        /// 방패가 체인 스타터일 때의 풀체인 — 아군 공격력 증가.
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
                StopChargeEffect();
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

        /// 남은 파티클까지 즉시 지운다 — 차징 오라는 손을 떼면 그 자리에 남으면 안 된다.
        private void StopChargeEffect()
        {
            _chargeEffect.Stop(clear: true);
            _chargeEffect = default;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Object != null && HasStateAuthority) ChargeEffectActive = false;
            StopChargeEffect();
        }
    }
}
