using UnityEngine;

namespace Game.BossContent
{
    /// 연속 6회 + 찌르기. 타격 시점을 하나씩 필드로 두지 않고 "첫 시점 + 간격"으로 굴린다 —
    /// 여섯 개를 인스펙터에 늘어놓아 봐야 조정만 번거로워진다.
    public class RushSixStrike : TfwComboPattern
    {
        private readonly RushSixStrikeDefinition _def;

        private int _strikesDone;
        private int _thrustHits;

        public RushSixStrike(RushSixStrikeDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
        {
            _strikesDone = 0;
            _thrustHits = 0;
        }

        protected override void OnComboActiveTick()
        {
            Vector3 fwd = Services.Motor.Forward;
            Vector3 origin = Services.Motor.Position;

            // 밀린 타격이 있으면 한 틱에 여러 번이라도 따라잡는다(배속이 높으면 간격이 한 틱보다 짧아진다).
            while (_strikesDone < _def.StrikeCount)
            {
                float at = _def.FirstStrikeAtSeconds + _def.StrikeIntervalSeconds * _strikesDone;
                if (ElapsedTicks < ToTicks(at)) break;

                TfwHits.Fan(origin, fwd, _def.StrikeRadius, _def.StrikeHalfAngle,
                            _def.StrikeDamage, Definition.HitCC);
                _strikesDone++;
            }

            while (TryHit(ref _thrustHits, nameof(_def.ThrustAtSeconds),
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
