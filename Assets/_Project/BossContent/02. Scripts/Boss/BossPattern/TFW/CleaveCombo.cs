using Game.Core;
using UnityEngine;

namespace Game.BossContent
{
    /// 일도양단 연계: 360도 회전베기 → 기모으기(타격 없음) → 칼등치기(스턴) → 일도양단.
    /// 타격이 Active 끝 1회가 아니라 클립 타임라인 위 여러 지점이라 판정을 OnComboActiveTick에서 한다.
    /// 조준 락과 조각별 전진은 TfwComboPattern이 담당한다.
    public class CleaveCombo : TfwComboPattern
    {
        private readonly CleaveComboDefinition _def;

        private int _spinHits, _backhandHits, _cleaveHits;

        public CleaveCombo(CleaveComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
            => _spinHits = _backhandHits = _cleaveHits = 0;

        protected override void OnComboActiveTick()
        {
            // ToTicks가 배속으로 나누므로 Active 길이와 내부 시점이 같은 비율로 줄어든다.
            while (TryHit(ref _spinHits, nameof(_def.SpinAtSeconds),
                          _def.SpinAtSeconds, _def.SpinDamage, out int spinDamage))
            {
                TfwHits.Sphere(
                    Services.Motor.Position + Services.Motor.Up * _def.SpinHeight,
                    _def.SpinRadius, spinDamage, Definition.HitCC);
            }

            // 이 타격만 스턴 — 맞으면 이어지는 일도양단을 그대로 얻어맞는다.
            while (TryHit(ref _backhandHits, nameof(_def.BackhandAtSeconds),
                          _def.BackhandAtSeconds, _def.BackhandDamage, out int backhandDamage))
            {
                TfwHits.Sphere(
                    Services.Motor.Position + Services.Motor.Forward * _def.BackhandReach
                        + Services.Motor.Up * _def.BackhandHeight,
                    _def.BackhandRadius, backhandDamage, CCType.Stun);
            }

            while (TryHit(ref _cleaveHits, nameof(_def.CleaveAtSeconds),
                          _def.CleaveAtSeconds, _def.Damage, out int cleaveDamage))
            {
                Vector3 fwd = Services.Motor.Forward;
                TfwHits.Box(
                    Services.Motor.Position + fwd * _def.CleaveReach
                        + Services.Motor.Up * (_def.CleaveHeight * 0.5f),
                    new Vector3(_def.CleaveWidth, _def.CleaveHeight, _def.CleaveLength) * 0.5f,
                    Quaternion.LookRotation(fwd),
                    cleaveDamage, Definition.HitCC);
            }
        }
    }
}
