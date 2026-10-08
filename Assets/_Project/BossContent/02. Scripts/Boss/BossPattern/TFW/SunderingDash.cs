using Game.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Game.BossContent
{
    /// 섬단: 예고 전반에 타겟을 빠르게 조준 → 락 시점에 방향 확정(추적 중단) →
    /// 확정된 직선을 따라 고속 돌진하며 경로의 플레이어를 1회씩 타격 → 종료 지점에서 전방 참격.
    /// 락 이후 남은 예고 시간이 "확정된 라인을 보고 옆으로 빠질" 리드타임 역할을 한다.
    public class SunderingDash : BossPatternBase
    {
        private readonly SunderingDashDefinition _def;
        private readonly HashSet<IDamageable> _pathHit = new();   // 한 번의 돌진에서 대상당 1회

        private TfwTelegraphPlan _telegraph;
        private int _warningElapsed;
        private Vector3 _direction;
        private bool _hasDirection;
        private int _telegraphTicksElapsed;
        private int _lockAtTick;
        private int _activeTicks;

        /// 클립 거리(4.4m)를 타겟까지의 거리에 맞춰 늘리거나 줄이는 배수. Active 첫 틱에 한 번 확정한다.
        private float _distanceScale = 1f;

        /// 스케일 상하한. 아래로는 클립 거리의 절반, 위로는 2.5배까지만 — 그 이상 늘리면 발이 심하게 미끄러진다.
        private const float MinDashScale = 0.5f;
        private const float MaxDashScale = 2.5f;

        public SunderingDash(SunderingDashDefinition def, BossServices services)
            : base(def, services)
        {
            _def = def;
        }

        protected override void OnBegin()
        {
            _telegraph = new TfwTelegraphPlan(Definition, Services);
            _warningElapsed = -ToTicks(Definition.TelegraphSeconds);
            _telegraph.Tick(_warningElapsed, SpeedMultiplier);
            _pathHit.Clear();
            _hasDirection = false;
            _telegraphTicksElapsed = 0;
            _lockAtTick = Mathf.RoundToInt(ToTicks(_def.TelegraphSeconds) * _def.LockAtTelegraphRatio);
            _activeTicks = 0;
            _distanceScale = 1f;
        }

        protected override void OnTelegraphTick()
        {
            _telegraph.Tick(++_warningElapsed, SpeedMultiplier);
            if (_hasDirection) return;   // 락 이후엔 방향 고정 — 확정 라인이 리드타임 동안 유지된다

            if (_telegraphTicksElapsed >= _lockAtTick)
            {
                _direction = Services.Motor.Forward;
                _hasDirection = true;
                return;
            }

            if (Services.Sensor.TryGetTargetPosition(out var target))
                Services.Motor.FaceToward(target, _def.TelegraphRotationSpeed);
            _telegraphTicksElapsed++;
        }

        protected override void OnActiveTick()
        {
            if (!_hasDirection)
            {
                _direction = Services.Motor.Forward;   // Ratio=1이라 락이 안 걸렸으면 여기서 확정
                _hasDirection = true;
            }

            if (_activeTicks == 0) LockDashDistance();   // 방향이 확정된 뒤라야 투영이 의미를 갖는다
            _activeTicks++;
            _telegraph.Tick(_activeTicks, SpeedMultiplier);
            Move();

            // 판정구를 몸 앞에 둔다 — 중심에 두고 앞까지 닿게 키우면 옆구리까지 부푼다.
            // 매 틱 전진하며 찍히므로 누적 커버리지가 돌진 경로를 덮는다.
            TfwHits.SphereOnce(
                Services.Motor.Position + Services.Motor.Up * _def.HitHeight + _direction * _def.HitFrontOffset,
                _def.PathRadius, _def.PathDamage, Definition.HitCC, _pathHit);
        }

        /// 클립 루트모션의 누적 거리 곡선을 따라 이동한다 — 애니에서 나아가는 거리와 실제 돌진 거리를 맞춘다.
        /// 곡선이 없거나 UseClipDistance를 끄면 DashSpeed로 등속 돌진한다.
        private void Move()
        {
            var curve = _def.ForwardDistanceCurve;
            if (!_def.UseClipDistance || curve == null || curve.length < 2)
            {
                // 배속이 걸리면 Active 틱 수가 줄어든다(ToTicks). 속도에도 같이 곱해야 돌진 거리가 유지된다.
                Services.Motor.MoveInDirection(_direction, _def.DashSpeed * SpeedMultiplier);
                return;
            }

            // 곡선이 이번 틱에 나아간 만큼만 옮긴다. 속도가 아니라 변위를 넘기므로
            // NCC의 가속 램프·maxSpeed 클램프를 타지 않고, 클립 거리를 그대로 재현한다.
            // 곡선은 배속 1 기준이라 진행도(0~1)로 환산해 훑는다.
            int totalTicks = Mathf.Max(1, ToTicks(_def.ActiveSeconds));
            float now  = _def.ActiveSeconds * Mathf.Clamp01((float)_activeTicks / totalTicks);
            float prev = _def.ActiveSeconds * Mathf.Clamp01((float)(_activeTicks - 1) / totalTicks);

            float delta = (curve.Evaluate(now) - curve.Evaluate(prev)) * _distanceScale;
            delta = Mathf.Clamp(delta, -0.5f, 0.5f);
            if (Mathf.Abs(delta) < 0.0001f) return;

            Services.Motor.MoveByDelta(_direction * delta);
        }

        /// 돌진 거리를 타겟에 맞춘다.
        ///
        /// 클립이 나아가는 거리는 4.4m로 고정인데 이 패턴의 사거리(AttackRange)는 8m다.
        /// 그대로 두면 멀리서 시작할수록 못 닿는데, 이 패턴의 존재 이유가 "멀어진 대상에게 파고들기"라
        /// 거리에 맞춰 곡선을 늘리고 줄인다. 곡선의 모양(초반 폭발 → 감속)은 그대로라 연출은 유지된다.
        ///
        /// 확정된 돌진선에 투영해서 재므로, 락 이후 타겟이 옆으로 빠졌으면 그만큼 덜 간다 —
        /// 회피 리드타임이 그대로 의미를 갖는다.
        private void LockDashDistance()
        {
            _distanceScale = 1f;

            var curve = _def.ForwardDistanceCurve;
            if (!_def.UseClipDistance || curve == null || curve.length < 2) return;

            float clipDistance = curve.Evaluate(_def.ActiveSeconds);
            if (clipDistance < 0.01f) return;

            if (!Services.Sensor.TryGetTargetPosition(out var target)) return;

            Vector3 toTarget = target - Services.Motor.Position;
            toTarget.y = 0f;

            // 마무리 참격이 닿는 거리만큼은 남겨둔다 — 대상을 지나쳐 등 뒤에서 베지 않게.
            float wanted = Vector3.Dot(toTarget, _direction) - _def.FinishReach;

            _distanceScale = Mathf.Clamp(wanted / clipDistance, MinDashScale, MaxDashScale);
        }

        /// 돌진 종료 지점의 마무리 참격. 경로 타격과 별개라 이미 스친 대상도 다시 맞는다.
        protected override void OnActiveHit()
        {
            Services.Motor.Stop();
            TfwHits.Sphere(
                Services.Motor.Position + _direction * _def.FinishReach + Services.Motor.Up * _def.HitHeight,
                _def.FinishRadius, _def.Damage, Definition.HitCC);
        }
    }
}
