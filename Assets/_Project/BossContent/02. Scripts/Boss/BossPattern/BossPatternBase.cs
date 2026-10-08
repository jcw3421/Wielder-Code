using Game.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Game.BossContent
{
    /// 모든 패턴의 공통 골격: Telegraph(예고) → Active(타격) → Recovery(후딜) 3단계 카운트다운.
    /// 자식은 OnActiveHit()에서 "무엇을 어떻게 때리나"만 구현한다.
    public abstract class BossPatternBase    
    {
        protected BossServices Services { get; }
        private readonly PatternDefinition _def;
        public int UnlockPhase { get; }
        public int LockPhase { get; }
        public int AnimatorPatternId { get; }
        public float AttackRange { get; }
        public int Weight { get; }
        public float CooldownSeconds { get; }
        /// 이 패턴이 대상을 고르는 방식. BossPatternState가 패턴 시작·종료에 맞춰 센서에 걸고 푼다.
        public TargetMode Targeting { get; }


        private float _speedMultiplier;
        /// 현재 페이즈 배속. 단계 시간은 ToTicks가 알아서 나누지만, 이동 속도처럼
        /// 시간에 비례하는 값을 자식이 직접 쓸 때는 여기에 곱해야 거리가 배속과 무관해진다.
        protected float SpeedMultiplier => _speedMultiplier;

        // 값은 .controller의 PatternStep 조건과 맺은 계약 — 순서/숫자를 바꾸면 애니 전이가 깨진다.
        private enum PatternStep { Telegraph = 0, Active = 1, Recovery = 2, Done = 3 }
        private PatternStep _patternStep;
        private int _remaining;

        private static readonly Collider[] HitBuffer = new Collider[16];


        protected BossPatternBase(PatternDefinition def, BossServices services)
        {
            _def = def;
            UnlockPhase = def.UnlockPhase;
            LockPhase = def.LockPhase;
            AnimatorPatternId = def.AnimatorPatternId;
            AttackRange = def.AttackRange;
            Weight = def.Weight;
            CooldownSeconds = def.CooldownSeconds;
            Targeting = def.Targeting;
            Services = services;
        }

        public bool IsFinished => _patternStep == PatternStep.Done;

        public void Begin()
        {
            Services.Motor.Stop();
            Services.Net.AnimMoveSpeed = 0f;
            _speedMultiplier = Services.Stats.PatternSpeedMultiplierFor(Services.Health.CurrentPhase);
            Services.Net.AnimPatternSpeedMultiplier = _speedMultiplier;
            Services.Net.CurrentAnimPatternId = AnimatorPatternId;
            Services.Net.PatternStartSeq++;
            OnBegin();                                              // ← 신규: 자식 실행상태 리셋 지점
            EnterStage(PatternStep.Telegraph, ToTicks(_def.TelegraphSeconds));
        }

        public void Tick()
        {
            if (_patternStep == PatternStep.Telegraph) OnTelegraphTick();   // ← 진행 중 매 틱 훅
            if(_patternStep == PatternStep.Active)    OnActiveTick();
            if (--_remaining > 0) return;
            switch (_patternStep)
            {
                case PatternStep.Telegraph:
                    EnterStage(PatternStep.Active, ToTicks(_def.ActiveSeconds));
                    break;
                case PatternStep.Active:
                    OnActiveHit();          // ← 자식이 채우는 유일한 지점
                    EnterStage(PatternStep.Recovery, ToTicks(_def.RecoverySeconds));
                    break;
                case PatternStep.Recovery:
                    _patternStep = PatternStep.Done;
                    break;
            }
        }
        protected virtual void OnBegin() { }
        protected virtual void OnActiveTick() { }
        protected virtual void OnTelegraphTick() { }
        /// Active 순간의 타격 판정. 자식이 구현.
        protected abstract void OnActiveHit();
        protected int ToTicks(float seconds)
            => Mathf.RoundToInt(seconds * Services.Stats.TickRate / _speedMultiplier);

        /// 이 패턴의 정의. 다단히트 설정처럼 공통 데이터를 자식이 읽을 때 쓴다.
        protected PatternDefinition Definition => _def;
        public void Cancel() => _patternStep = PatternStep.Done;

        private void EnterStage(PatternStep stage, int durationTicks)
        {
            _patternStep = stage;
            _remaining = durationTicks;
            Services.Net.CurrentPatternStep = (int)stage;   // 애니메이터가 이 값으로 단계를 따라온다
        }

        

        /// 적중한 대상에게 이 패턴의 CC(기본 경직, SO에서 오버라이드)를 건다. 데미지와 같은 대상에만 걸리도록
        /// 타격 헬퍼의 적중 분기에서 데미지 직후 함께 호출한다. 우선순위·슈퍼아머·면역 판정은 대상 컨트롤러가 한다.
        private void ApplyHitCC(Rigidbody rb)
        {
            if (_def.HitCC == CCType.None) return;
            if (rb.TryGetComponent<ICrowdControllable>(out var cc))
                cc.ApplyCC(new CCInfo(_def.HitCC));
        }

        /// 공용 판정 헬퍼 — 원형 범위 내 플레이어에게 데미지 1회.
        protected void HitPlayersInSphere(Vector3 center, float radius, int damage)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                var rb = HitBuffer[i].attachedRigidbody;
                if (rb != null && rb.TryGetComponent<IDamageable>(out var target))
                {
                    target.ApplyDamage(new DamageInfo(damage, DamageType.Normal));
                    ApplyHitCC(rb);
                }
            }
        }
        /// 여러 틱에 걸쳐 때리는 패턴용(돌진 등) — alreadyHit에 없는 대상만 타격해 대상당 1회를 보장한다.
        protected void HitPlayersInSphereOnce(Vector3 center, float radius, int damage, HashSet<IDamageable> alreadyHit)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, HitBuffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                var rb = HitBuffer[i].attachedRigidbody;
                if (rb != null && rb.TryGetComponent<IDamageable>(out var target) && alreadyHit.Add(target))
                {
                    target.ApplyDamage(new DamageInfo(damage, DamageType.Normal));
                    ApplyHitCC(rb);   // alreadyHit로 대상당 1회 보장 — 다틱 패턴에서 매 틱 재경직되지 않는다
                }
            }
        }
        /// 부채꼴(수평 스윕) 판정 — 반경 내 + 정면 기준 halfAngleDeg 안의 플레이어에게 데미지 1회.
        protected void HitPlayersInFan(Vector3 origin, Vector3 forward, float radius, float halfAngleDeg, int damage)
        {
            forward.y = 0f;
            forward.Normalize();
            int count = Physics.OverlapSphereNonAlloc(origin, radius, HitBuffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                Vector3 to = HitBuffer[i].transform.position - origin;
                to.y = 0f;
                // 정면 기준 각도 밖이면 제외 (원점에 겹친 경우는 통과)
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(forward, to) > halfAngleDeg)
                    continue;
                var rb = HitBuffer[i].attachedRigidbody;
                if (rb != null && rb.TryGetComponent<IDamageable>(out var target))
                {
                    target.ApplyDamage(new DamageInfo(damage, DamageType.Normal));
                    ApplyHitCC(rb);
                }
            }
        }
        /// 박스 판정 — center 기준 halfExtents 박스(rotation) 안 플레이어에게 데미지 1회.
        protected void HitPlayersInBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, int damage)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, HitBuffer, rotation, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                var rb = HitBuffer[i].attachedRigidbody;
                if (rb != null && rb.TryGetComponent<IDamageable>(out var target))
                {
                    target.ApplyDamage(new DamageInfo(damage, DamageType.Normal));
                    ApplyHitCC(rb);
                }
            }
                
        }

    }
}
