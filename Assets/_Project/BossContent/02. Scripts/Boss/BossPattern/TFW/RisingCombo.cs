using UnityEngine;

namespace Game.BossContent
{
    /// 올려베기 연계: 우 → 좌 부채꼴 두 번 → 뒤로 물러났다가 → 전방 직선 일도양단.
    /// 백스텝은 별도 코드 없이 조각 전진 속도를 음수로 두면 된다(TfwComboPattern이 처리).
    public class RisingCombo : TfwComboPattern
    {
        private readonly RisingComboDefinition _def;

        private int _rightHits, _leftHits, _cleaveHits;

        public RisingCombo(RisingComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
            => _rightHits = _leftHits = _cleaveHits = 0;

        protected override void OnComboActiveTick()
        {
            while (TryHit(ref _rightHits, nameof(_def.RightRiseAtSeconds),
                          _def.RightRiseAtSeconds, _def.RiseDamage, out int rightDamage))
                HitRise(+_def.RiseYawOffset, rightDamage);

            while (TryHit(ref _leftHits, nameof(_def.LeftRiseAtSeconds),
                          _def.LeftRiseAtSeconds, _def.RiseDamage, out int leftDamage))
                HitRise(-_def.RiseYawOffset, leftDamage);

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

        /// 정면에서 yaw만큼 튼 방향으로 부채꼴 판정. 오른쪽 베기는 +, 왼쪽 베기는 -.
        private void HitRise(float yawDegrees, int damage)
        {
            Vector3 dir = Quaternion.Euler(0f, yawDegrees, 0f) * Services.Motor.Forward;
            TfwHits.Fan(Services.Motor.Position, dir, _def.RiseRadius, _def.RiseHalfAngle,
                        damage, Definition.HitCC);
        }
    }
}
