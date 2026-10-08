using UnityEngine;

namespace Game.BossContent
{
    /// 4타 연계: 찌르기 → 전방 일도양단 → 후방 일도양단 → 전방 부채꼴 참격.
    /// 3타는 보스 뒤쪽을 때린다 — 등 뒤로 파고든 플레이어를 노린다.
    public class FourStrikeCombo : TfwComboPattern
    {
        private readonly FourStrikeComboDefinition _def;

        private int _thrustHits, _frontHits, _backHits, _finishHits;

        public FourStrikeCombo(FourStrikeComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
            => _thrustHits = _frontHits = _backHits = _finishHits = 0;

        protected override void OnComboActiveTick()
        {
            Vector3 fwd = Services.Motor.Forward;

            while (TryHit(ref _thrustHits, nameof(_def.ThrustAtSeconds),
                          _def.ThrustAtSeconds, _def.ThrustDamage, out int thrustDamage))
                HitBox(fwd, _def.ThrustReach, _def.ThrustWidth, _def.ThrustLength, thrustDamage);

            while (TryHit(ref _frontHits, nameof(_def.FrontCleaveAtSeconds),
                          _def.FrontCleaveAtSeconds, _def.CleaveDamage, out int frontDamage))
                HitBox(fwd, _def.CleaveReach, _def.CleaveWidth, _def.CleaveLength, frontDamage);

            // 뒤돌아 벤다. 판정은 Forward 기준 그대로다 —
            // 이 조각을 TurnAroundSegments에 넣어두면 진입할 때 보스가 이미 180도 돌아 있다.
            while (TryHit(ref _backHits, nameof(_def.BackCleaveAtSeconds),
                          _def.BackCleaveAtSeconds, _def.CleaveDamage, out int backDamage))
                HitBox(fwd, _def.CleaveReach, _def.CleaveWidth, _def.CleaveLength, backDamage);

            while (TryHit(ref _finishHits, nameof(_def.FinishAtSeconds),
                          _def.FinishAtSeconds, _def.Damage, out int finishDamage))
                TfwHits.Fan(Services.Motor.Position, fwd, _def.FinishRadius,
                            _def.FinishHalfAngle, finishDamage, Definition.HitCC);
        }

        private void HitBox(Vector3 direction, float reach, float width, float length, int damage)
        {
            Vector3 center = Services.Motor.Position + direction * reach
                             + Services.Motor.Up * (_def.StrikeHeight * 0.5f);
            TfwHits.Box(center,
                        new Vector3(width, _def.StrikeHeight, length) * 0.5f,
                        Quaternion.LookRotation(direction),
                        damage, Definition.HitCC);
        }
    }
}
