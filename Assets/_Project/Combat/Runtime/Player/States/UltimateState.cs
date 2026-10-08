using Game.Core;
using Game.Networking;

namespace Game.Combat.Player
{
    /// <summary>Attack/Skill과 완전히 대칭인 얇은 relay. 지금은 홀드형 궁극기를 쓰는 무기가
    /// 없지만, 나중에 생겨도 이 State는 바뀔 필요가 없다.</summary>
    public sealed class UltimateState : PlayerStateBase
    {
        private bool _lastHeld;

        public override PlayerState StateId => PlayerState.Ultimate;

        public override void Enter(PlayerStateMachine machine, NetworkInputData input)
        {
            _lastHeld = true;
            FaceAimPoint(machine, input);
            machine.Weapon?.OnUltimatePressed();
        }

        public override PlayerState Execute(PlayerStateMachine machine, NetworkInputData input)
        {
            var weapon = machine.Weapon;
            if (weapon == null) return PlayerState.Idle;

            bool held = input.Has(InputIntent.Ultimate);
            if (held && !_lastHeld) weapon.OnUltimatePressed();
            else if (!held && _lastHeld) weapon.OnUltimateReleased();
            if (held) weapon.OnUltimateTick();
            _lastHeld = held;

            return weapon.IsUltimateFinished() ? PlayerState.Idle : PlayerState.Ultimate;
        }

        public override void Exit(PlayerStateMachine machine, NetworkInputData input)
        {
            if (_lastHeld) machine.Weapon?.OnUltimateReleased();
        }
    }
}
