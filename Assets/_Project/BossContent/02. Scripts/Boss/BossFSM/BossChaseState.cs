using UnityEngine;


namespace Game.BossContent
{
    public class BossChaseState : BossStateBase    
    {
        public override BossAnimState AnimatorStateId => BossAnimState.Chase;
        private int _ticksUntilPattern;

        // 첫 조우인가. 스폰 후 처음 추격에 들어갈 때만 추가 대기를 붙인다 —
        // 매번 붙이면 그건 패턴 간격(PatternIntervalTicks)과 같은 값이 되어버린다.
        private bool _firstEncounter = true;

        public BossChaseState(BossFSM fsm, BossServices services): base(fsm, services) { }

        public override void Enter()
        {
            _services.Sensor.PickTarget();

            _ticksUntilPattern = _services.Stats.PatternIntervalTicks;
            if (_firstEncounter)
            {
                _ticksUntilPattern += _services.Stats.FirstEncounterDelayTicks;
                _firstEncounter = false;
            }
        }

        public override void Tick()
        {
            // 커밋 타겟이 유효하지 않으면(사망·이탈) 재선택, 그래도 없으면 대기로 복귀
            if (!_services.Sensor.TryGetTargetPosition(out var targetPos))
            {
                if (!_services.Sensor.PickTarget() || !_services.Sensor.TryGetTargetPosition(out targetPos))
                {
                    _fsm.ChangeState(_fsm.IdleState);
                    return;
                }
            }
            bool moved = _services.Motor.MoveToward(targetPos);
            _services.Net.AnimMoveSpeed = moved ? 1f : 0f;
            if (_ticksUntilPattern > 0)
            {
                _ticksUntilPattern--;
                return;                    // 추격은 계속, 공격만 잠금
            }
            if (_fsm.PatternState.HasUsablePattern(targetPos))
            {
                _fsm.ChangeState(_fsm.PatternState);
            }
        }
    }
}
