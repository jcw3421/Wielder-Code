using UnityEngine;

namespace Game.BossContent
{
    /// 레이저 연계: 방패 → 방사형 레이저 1차 → 각도를 튼 2차 → 지목 강타.
    /// 줄기 사이의 쐐기꼴이 안전지대이고, 2차는 그 안전지대 위로 온다.
    public class LaserCombo : TfwComboPattern
    {
        private readonly LaserComboDefinition _def;
        private int _shield, _marked;
        private bool _frontLaser, _backLaser;
        private bool _targetSelected, _hasTarget;
        private Vector3 _target;

        public LaserCombo(LaserComboDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin()
        {
            _shield = _marked = 0;
            _frontLaser = _backLaser = false;
            _targetSelected = _hasTarget = false;
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

            if (!_frontLaser && ElapsedTicks >= ToTicks(_def.FrontLaserAtSeconds))
            {
                _frontLaser = true;
                FireVolley(fwd, 0f);
            }

            // 2차는 칸을 반쯤 돌려 쏜다 — 1차 안전지대에 그대로 서 있으면 여기서 맞는다.
            // LaserVolleys가 1이면 통째로 건너뛴다.
            if (_def.LaserVolleys >= 2 && !_backLaser && ElapsedTicks >= ToTicks(_def.BackLaserAtSeconds))
            {
                _backLaser = true;
                FireVolley(fwd, _def.SecondVolleyOffset);
            }

            int lead=Mathf.RoundToInt(TfwTelegraphPlan.LeadSeconds*Services.Stats.TickRate);
            if(!_targetSelected && ElapsedTicks>=ToTicks(_def.MarkedAtSeconds)-lead)
            {
                _targetSelected=true;
                _hasTarget=TfwHits.TryPickRandomPlayer(Services,salt:29,out _target);
                if(_hasTarget)Services.Net.GetComponent<BossPatternEffectPlayer>()?.SetTargetWarning(_target,_def.MarkedRadius,ToTicks(_def.MarkedAtSeconds)-ElapsedTicks);
            }
            while (TryHit(ref _marked, nameof(_def.MarkedAtSeconds),
                          _def.MarkedAtSeconds, _def.Damage, out int markedDamage))
            {
                if(_hasTarget)
                {
                    TfwHits.Sphere(_target+Vector3.up,_def.MarkedRadius,markedDamage,Definition.HitCC);
                    if(_marked<Repeats.EffectiveHitCount(nameof(_def.MarkedAtSeconds),_def.MarkedAtSeconds))
                        Services.Net.GetComponent<BossPatternEffectPlayer>()?.SetTargetWarning(_target,_def.MarkedRadius,
                            ToTicks(Repeats.RepeatTimeAt(nameof(_def.MarkedAtSeconds),_def.MarkedAtSeconds,_marked))-ElapsedTicks);
                }
            }

        }

        /// 한 번의 발사 = 보스 중심에서 사방으로 뻗는 LaserCount개의 줄기.
        ///
        /// 대상당 1회로 묶지 않는다 — 줄기들이 보스 근처에서 겹치므로, 겹친 자리에 있으면
        /// 여러 번 맞는 게 맞다. 중심에 붙어 있을수록 위험하다는 규칙이 그림과 일치한다.
        private void FireVolley(Vector3 forward, float volleyOffset)
        {
            int n = Mathf.Max(2, _def.LaserCount);
            for (int i = 0; i < n; i++)
                FireLaser(_def.LaserDirection(forward, i, volleyOffset));
        }

        /// 보스 몸에서 시작하는 한 줄. 박스 중심을 절반 길이만큼 밀어 몸에서 뻗어나가게 한다.
        private void FireLaser(Vector3 direction)
        {
            Vector3 center = Services.Motor.Position
                             + direction * (_def.LaserLength * 0.5f)
                             + Services.Motor.Up * (_def.LaserHeight * 0.5f);

            TfwHits.Box(center,
                        new Vector3(_def.LaserWidth, _def.LaserHeight, _def.LaserLength) * 0.5f,
                        Quaternion.LookRotation(direction), _def.LaserDamage, Definition.HitCC);
        }
    }
}
