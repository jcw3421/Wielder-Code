using UnityEngine;

namespace Game.BossContent
{
    /// 근 → 중 → 원 3연속 폭발. 보스를 중심으로 동심원이 차례로 터진다.
    /// 앞 단계에 맞은 대상도 다음 단계에 다시 맞는다(단계마다 독립 판정) — 제자리에 서 있으면 3번 맞는다.
    public class TriBlast : BossPatternBase
    {
        private readonly TriBlastDefinition _def;

        private TfwTelegraphPlan _telegraph;
        private int _telegraphElapsed;
        private int _elapsedTicks;
        private bool _nearDone, _midDone, _farDone;

        public TriBlast(TriBlastDefinition def, BossServices services)
            : base(def, services)
        {
            _def = def;
        }

        protected override void OnBegin()
        {
            _telegraph = new TfwTelegraphPlan(Definition, Services);
            _telegraphElapsed = -ToTicks(Definition.TelegraphSeconds);
            _telegraph.Tick(_telegraphElapsed, SpeedMultiplier);
            _elapsedTicks = 0;
            _nearDone = _midDone = _farDone = false;
        }

        protected override void OnTelegraphTick() => _telegraph.Tick(++_telegraphElapsed, SpeedMultiplier);

        protected override void OnActiveTick()
        {
            _elapsedTicks++;
            _telegraph.Tick(_elapsedTicks, SpeedMultiplier);
            Vector3 center = Services.Motor.Position + Services.Motor.Up * _def.BlastHeight;

            if (!_nearDone && _elapsedTicks >= ToTicks(_def.NearAtSeconds))
            {
                _nearDone = true;
                TfwHits.Sphere(center, _def.NearRadius, _def.NearDamage, Definition.HitCC);
            }

            if (!_midDone && _elapsedTicks >= ToTicks(_def.MidAtSeconds))
            {
                _midDone = true;
                TfwHits.Sphere(center, _def.MidRadius, _def.MidDamage, Definition.HitCC);
            }

            if (!_farDone && _elapsedTicks >= ToTicks(_def.FarAtSeconds))
            {
                _farDone = true;
                TfwHits.Sphere(center, _def.FarRadius, _def.FarDamage, Definition.HitCC);
            }
        }

        protected override void OnActiveHit() { }   // 타격은 전부 OnActiveTick 담당
    }
}
