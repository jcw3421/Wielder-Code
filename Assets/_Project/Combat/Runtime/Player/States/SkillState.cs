using Game.Core;
using Game.Networking;

namespace Game.Combat.Player
{
    /// <summary>무기의 스킬 동작(고정 길이/차징/막기 홀드)을 전혀 모른다 — 입력 이벤트를
    /// 무기에 전달하고 "끝났는지"만 물어보는 얇은 relay. AttackingState와 동일한 모양.</summary>
    public sealed class SkillState : PlayerStateBase
    {
        private bool _lastHeld;

        public override PlayerState StateId => PlayerState.Skill;

        public override void Enter(PlayerStateMachine machine, NetworkInputData input)
        {
            _lastHeld = true;

            // 평타 후딜을 끊고 들어온 경우가 있다 — 남은 이펙트와 콤보 진행 상태를 버린다(대시와 같은 처리).
            // 평소 진입(Idle/Moving)에서는 진행 중인 게 없어 아무 일도 하지 않는다.
            machine.Weapon?.Cancel();

            FaceAimPoint(machine, input);
            machine.Weapon?.OnSkillPressed();
        }

        public override PlayerState Execute(PlayerStateMachine machine, NetworkInputData input)
        {
            var weapon = machine.Weapon;
            if (weapon == null) return PlayerState.Idle;

            // 두 입력의 눌림 판정을 한 번에 보고 버튼을 기록한다 —
            // 따로 두 번 보면 먼저 본 쪽이 버튼을 기록해 뒤엣것이 항상 false가 된다.
            bool dashRising = IsRising(input, InputIntent.Dash);
            bool attackRising = IsRising(input, InputIntent.Attack);
            CommitButtons(input);

            // 대시는 스킬(차징·막기 포함)을 끊고 즉시 나간다. 궁극기만 캔슬 불가.
            // 홀드를 "뗀 것"으로 넘기면 안 된다 — 차징 스킬은 Release가 곧 발동이라
            // 대시로 끊었는데 스킬이 나가고 쿨타임까지 먹는다. 버리는 건 DashState.Enter의 Cancel()이 한다.
            if (dashRising && weapon.CanDash)
            {
                _lastHeld = false;
                return PlayerState.Dash;
            }

            // 스킬을 유지한 채 들어온 평타 입력. 무엇으로 쓸지는 무기가 정한다(방패=반격 찌르기).
            if (attackRising) weapon.OnAttackDuringSkill();

            bool held = input.Has(InputIntent.Skill);
            if (held && !_lastHeld) weapon.OnSkillPressed();
            else if (!held && _lastHeld) weapon.OnSkillReleased();
            if (held) weapon.OnSkillTick();
            _lastHeld = held;

            return weapon.IsSkillFinished() ? PlayerState.Idle : PlayerState.Skill;
        }

        public override void Exit(PlayerStateMachine machine, NetworkInputData input)
        {
            if (_lastHeld) machine.Weapon?.OnSkillReleased();
        }
    }
}
