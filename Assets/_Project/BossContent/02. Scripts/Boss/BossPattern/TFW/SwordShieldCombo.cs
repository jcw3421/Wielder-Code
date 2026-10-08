using UnityEngine;

namespace Game.BossContent
{
    /// 검·방패 연계: 검 → 방패 → 검 → 뒤돌기 → 찌르기.
    /// 뒤돌기는 보스를 실제로 돌리지 않고 판정 방향만 뒤집는다 — 클립에서 상체가 도는 연출을 맡는다.
    public class SwordShieldCombo : TfwComboPattern
    {
        private readonly SwordShieldComboDefinition _def;
        private int _slash1, _shield, _slash2, _backTurn, _thrust;

        public SwordShieldCombo(SwordShieldComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
            => _slash1 = _shield = _slash2 = _backTurn = _thrust = 0;

        protected override void OnComboActiveTick()
        {
            Vector3 fwd = Services.Motor.Forward;
            Vector3 origin = Services.Motor.Position;

            while (TryHit(ref _slash1, nameof(_def.Slash1AtSeconds),
                          _def.Slash1AtSeconds, _def.SlashDamage, out int slash1Damage))
            {
                TfwHits.Fan(origin, fwd, _def.SlashRadius, _def.SlashHalfAngle, slash1Damage, Definition.HitCC);
            }

            while (TryHit(ref _shield, nameof(_def.ShieldAtSeconds),
                          _def.ShieldAtSeconds, _def.ShieldDamage, out int shieldDamage))
            {
                TfwHits.Sphere(origin + fwd * _def.ShieldReach + Services.Motor.Up,
                               _def.ShieldRadius, shieldDamage, Definition.HitCC);
            }

            while (TryHit(ref _slash2, nameof(_def.Slash2AtSeconds),
                          _def.Slash2AtSeconds, _def.SlashDamage, out int slash2Damage))
            {
                TfwHits.Fan(origin, fwd, _def.SlashRadius, _def.SlashHalfAngle, slash2Damage, Definition.HitCC);
            }

            // 뒤돌아 벤다. 판정은 Forward 기준 그대로다 —
            // 이 조각을 TurnAroundSegments에 넣어두면 진입할 때 보스가 이미 180도 돌아 있다.
            while (TryHit(ref _backTurn, nameof(_def.BackTurnAtSeconds),
                          _def.BackTurnAtSeconds, _def.BackTurnDamage, out int backTurnDamage))
            {
                TfwHits.Fan(origin, fwd, _def.BackTurnRadius, _def.BackTurnHalfAngle,
                            backTurnDamage, Definition.HitCC);
            }

            while (TryHit(ref _thrust, nameof(_def.ThrustAtSeconds),
                          _def.ThrustAtSeconds, _def.Damage, out int thrustDamage))
            {
                Vector3 center = origin + fwd * _def.ThrustReach
                                 + Services.Motor.Up * (_def.StrikeHeight * 0.5f);
                TfwHits.Box(center,
                            new Vector3(_def.ThrustWidth, _def.StrikeHeight, _def.ThrustLength) * 0.5f,
                            Quaternion.LookRotation(fwd), thrustDamage, Definition.HitCC);
            }
        }
    }
}
