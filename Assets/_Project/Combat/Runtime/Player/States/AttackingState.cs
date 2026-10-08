using Game.Core;
using Game.Networking;

namespace Game.Combat.Player
{
    /// <summary>무기의 콤보/타이밍은 전혀 모른다 — 입력 이벤트를 무기에 전달하고
    /// "끝났는지"만 물어보는 얇은 relay. 새 무기를 추가해도 이 코드는 바뀌지 않는다(OCP).</summary>
    public sealed class AttackingState : PlayerStateBase
    {
        private bool _lastHeld;

        public override PlayerState StateId => PlayerState.Attacking;

        public override void Enter(PlayerStateMachine machine, NetworkInputData input)
        {
            _lastHeld = true;

            FaceAimPoint(machine, input);

            machine.Weapon?.OnAttackPressed();
        }

        public override PlayerState Execute(PlayerStateMachine machine, NetworkInputData input)
        {
            var weapon = machine.Weapon;
            if (weapon == null) return PlayerState.Idle;

            // 평타를 끊고 나갈 수 있는 두 입력. 어느 쪽이든 홀드를 "뗀 것"으로 넘기지 않는다
            // (_lastHeld = false) — 콤보 마무리가 발동하면 안 된다.
            bool dashRising = IsRising(input, InputIntent.Dash);
            CommitButtons(input);

            // 대시는 누른 순간에만, 대신 언제든 나간다(회피가 늦으면 회피가 아니다).
            if (dashRising && weapon.CanDash)
            {
                _lastHeld = false;
                return PlayerState.Dash;
            }

            // 스킬은 반대로 "누르고 있으면 열리는 순간 나간다"(선입력). 타이밍을 정확히 맞춰 누를 필요 없이
            // 방패를 미리 들고 있으면 타격이 끝나는 즉시 올라간다 — 캔슬을 열어준 무기(방패)만 해당된다.
            if (input.Has(InputIntent.Skill) && weapon.CanCancelIntoSkill && weapon.CanSkill)
            {
                _lastHeld = false;
                return PlayerState.Skill;
            }

            bool held = input.Has(InputIntent.Attack);
            if (held && !_lastHeld) weapon.OnAttackPressed();
            else if (!held && _lastHeld) weapon.OnAttackReleased();
            if (held) weapon.OnAttackTick();
            _lastHeld = held;

            return weapon.IsAttackFinished() ? PlayerState.Idle : PlayerState.Attacking;
        }

        public override void Exit(PlayerStateMachine machine, NetworkInputData input)
        {
            if (_lastHeld) machine.Weapon?.OnAttackReleased();
        }
    }
}
