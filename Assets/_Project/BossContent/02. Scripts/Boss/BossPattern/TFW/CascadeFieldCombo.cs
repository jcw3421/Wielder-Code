using Game.Networking;
using UnityEngine;

namespace Game.BossContent
{
    /// 연쇄 장판 연계: 방패 → 검 → X검 → 장판 4회.
    /// 장판은 Eruption과 같은 리듬으로 깐다 — 웨이브마다 시드를 섞어 위치가 매번 달라지되 결정론은 지킨다.
    public class CascadeFieldCombo : TfwComboPattern
    {
        private readonly CascadeFieldComboDefinition _def;

        private int _shield, _slash, _cross;
        private int _wavesSpawned;

        public CascadeFieldCombo(CascadeFieldComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
        {
            _shield = _slash = _cross = 0;
            _wavesSpawned = 0;
        }

        protected override void OnComboActiveTick()
        {
            Vector3 fwd = Services.Motor.Forward;
            Vector3 origin = Services.Motor.Position;

            while (TryHit(ref _shield, nameof(_def.ShieldAtSeconds),
                          _def.ShieldAtSeconds, _def.ShieldDamage, out int shieldDamage))
            {
                TfwHits.Sphere(origin + fwd * _def.ShieldReach + Services.Motor.Up,
                               _def.ShieldRadius, shieldDamage, Definition.HitCC);
            }

            while (TryHit(ref _slash, nameof(_def.SlashAtSeconds),
                          _def.SlashAtSeconds, _def.SlashDamage, out int slashDamage))
            {
                TfwHits.Fan(origin, fwd, _def.SlashRadius, _def.SlashHalfAngle,
                            slashDamage, Definition.HitCC);
            }

            while (TryHit(ref _cross, nameof(_def.CrossSlashAtSeconds),
                          _def.CrossSlashAtSeconds, _def.Damage, out int crossDamage))
            {
                TfwHits.Fan(origin, fwd, _def.CrossRadius, _def.CrossHalfAngle,
                            crossDamage, Definition.HitCC);
            }

            TickFieldWaves();
        }

        /// 첫 장판 시점부터 간격마다 한 웨이브씩. 배속이 높으면 간격이 한 틱보다 짧아질 수 있어
        /// while로 밀린 웨이브를 따라잡는다.
        private void TickFieldWaves()
        {
            while (_wavesSpawned < _def.FieldWaveCount)
            {
                float at = _def.FirstFieldAtSeconds + _def.FieldIntervalSeconds * _wavesSpawned;
                if (ElapsedTicks < ToTicks(at)) break;

                // 웨이브마다 시드에 인덱스를 섞어 위치가 매번 다르게(결정론 유지).
                int seed = DeterministicRng.Combine(Services.Stats.BossID, AnimatorPatternId,
                                                    Services.Net.PatternStartSeq * 31 + _wavesSpawned);
                BossAoeBurst.Spawn(Services, _def.Burst, seed);
                _wavesSpawned++;
            }
        }
    }
}
