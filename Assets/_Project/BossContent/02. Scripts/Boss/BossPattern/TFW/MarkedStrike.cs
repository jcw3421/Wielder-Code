using UnityEngine;

namespace Game.BossContent
{
    /// 지목 공격: 무작위 대상의 발밑을 표시 → 잠시 뒤 그 자리를 내리찍는다.
    ///
    /// 표시한 "자리"를 치지 대상을 따라가지 않는다 — 그래야 지목당한 사람이 빠져나갈 수 있다.
    /// 대상 선정은 결정론적 시드를 쓰므로 서버 재시뮬레이션에서도 같은 사람이 뽑힌다.
    public class MarkedStrike : TfwComboPattern
    {
        private readonly MarkedStrikeDefinition _def;

        private Vector3 _markedPosition;
        private bool _hasTarget, _attemptedMark;
        private int _impactHits;

        public MarkedStrike(MarkedStrikeDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
        {
            _impactHits = 0;
            _hasTarget = _attemptedMark = false;
            TryMark(-ToTicks(Definition.TelegraphSeconds));
        }

        protected override void OnComboTelegraphTick(int elapsedTicks) => TryMark(elapsedTicks);

        private void TryMark(int elapsedTicks)
        {
            int lead=Mathf.RoundToInt(TfwTelegraphPlan.LeadSeconds*Services.Stats.TickRate);
            int markAt=Mathf.Min(ToTicks(_def.MarkAtSeconds),ToTicks(_def.ImpactAtSeconds)-lead);
            if(_attemptedMark || elapsedTicks<markAt)return;
            _attemptedMark=true;
            _hasTarget=TfwHits.TryPickRandomPlayer(Services,salt:17,out _markedPosition);
            if(_hasTarget)Services.Net.GetComponent<BossPatternEffectPlayer>()?.SetTargetWarning(_markedPosition,_def.ImpactRadius,
                ToTicks(_def.ImpactAtSeconds)-elapsedTicks);
        }

        protected override void OnComboActiveTick()
        {
            TryMark(ElapsedTicks);
            while (_hasTarget && TryHit(ref _impactHits, nameof(_def.ImpactAtSeconds),
                                      _def.ImpactAtSeconds, _def.Damage, out int impactDamage))
            {
                TfwHits.Sphere(_markedPosition + Vector3.up, _def.ImpactRadius, impactDamage, Definition.HitCC);
                if(_impactHits<Repeats.EffectiveHitCount(nameof(_def.ImpactAtSeconds),_def.ImpactAtSeconds))
                    Services.Net.GetComponent<BossPatternEffectPlayer>()?.SetTargetWarning(_markedPosition,_def.ImpactRadius,
                        ToTicks(Repeats.RepeatTimeAt(nameof(_def.ImpactAtSeconds),_def.ImpactAtSeconds,_impactHits))-ElapsedTicks);
            }
        }

    }
}
