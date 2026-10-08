using Game.Core;
using UnityEngine;

namespace Game.BossContent
{
    /// 방패찍기 연계: 검 → 방패 → X검 → 방패찍기(스턴) → 뒷발차기.
    /// 4타만 스턴이라 그 순간을 피하는 게 이 패턴의 핵심이다.
    public class ShieldBashCombo : TfwComboPattern
    {
        private readonly ShieldBashComboDefinition _def;
        private int _slash, _shield, _cross, _bash, _kick;

        public ShieldBashCombo(ShieldBashComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
            => _slash = _shield = _cross = _bash = _kick = 0;

        protected override void OnComboActiveTick()
        {
            Vector3 fwd = Services.Motor.Forward;
            Vector3 origin = Services.Motor.Position;
            Vector3 up = Services.Motor.Up;

            while (TryHit(ref _slash, nameof(_def.SlashAtSeconds),
                          _def.SlashAtSeconds, _def.SlashDamage, out int slashDamage))
            {
                TfwHits.Fan(origin, fwd, _def.SlashRadius, _def.SlashHalfAngle, slashDamage, Definition.HitCC);
            }

            while (TryHit(ref _shield, nameof(_def.ShieldAtSeconds),
                          _def.ShieldAtSeconds, _def.ShieldDamage, out int shieldDamage))
            {
                TfwHits.Sphere(origin + fwd * _def.ShieldReach + up,
                               _def.ShieldRadius, shieldDamage, Definition.HitCC);
            }

            while (TryHit(ref _cross, nameof(_def.CrossSlashAtSeconds),
                          _def.CrossSlashAtSeconds, _def.CrossDamage, out int crossDamage))
            {
                TfwHits.Fan(origin, fwd, _def.CrossRadius, _def.CrossHalfAngle, crossDamage, Definition.HitCC);
            }

            // 이 타격만 기절 — 맞으면 남은 연계를 그대로 얻어맞는다.
            while (TryHit(ref _bash, nameof(_def.BashAtSeconds),
                          _def.BashAtSeconds, _def.BashDamage, out int bashDamage))
            {
                TfwHits.Sphere(origin + fwd * _def.BashReach + up,
                               _def.BashRadius, bashDamage, CCType.Stun);
            }

            while (TryHit(ref _kick, nameof(_def.KickAtSeconds),
                          _def.KickAtSeconds, _def.Damage, out int kickDamage))
            {
                TfwHits.Sphere(origin + fwd * _def.KickReach + up,
                               _def.KickRadius, kickDamage, Definition.HitCC);
            }
        }
    }
}
