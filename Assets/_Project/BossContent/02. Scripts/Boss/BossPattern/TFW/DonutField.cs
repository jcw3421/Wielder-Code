namespace Game.BossContent
{
    /// 도넛 장판: 보스 주위로 고리가 터진다. 안쪽 반경은 안전지대라
    /// 다른 장판 패턴(밖으로 도망)과 반대 방향의 반응을 요구한다.
    public class DonutField : TfwComboPattern
    {
        private readonly DonutFieldDefinition _def;
        private bool _burstDone;

        public DonutField(DonutFieldDefinition def, BossServices services)
            : base(def, def, services)
        {
            _def = def;
        }

        protected override void OnComboBegin() => _burstDone = false;

        protected override void OnComboActiveTick()
        {
            if (_burstDone || ElapsedTicks < ToTicks(_def.BurstAtSeconds)) return;

            _burstDone = true;
            TfwHits.Donut(Services.Motor.Position + Services.Motor.Up * _def.BurstHeight,
                          _def.InnerRadius, _def.OuterRadius, _def.Damage, Definition.HitCC);
        }
    }
}
