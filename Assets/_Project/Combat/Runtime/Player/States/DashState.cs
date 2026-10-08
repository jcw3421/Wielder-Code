using Game.Networking;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>대시 상태. 수치(스테미너 비용·지속·이동 거리)와 진행 판정은 전부 무기가 갖는다 —
    /// 이 상태는 방향만 잡아주고 "끝났는지"만 묻는다.
    ///
    /// 다른 액션과 달리 홀드 처리가 없다. 대시는 눌린 순간에 전부 결정되므로 Tick·Release가 할 일이 없다.
    /// 이 상태에 있는 동안은 무적이다(<see cref="PlayerStateMachine.IsInvulnerable"/>).
    /// 별도 쿨타임도 없다 — 대시 지속이 곧 다음 대시까지의 간격이고, 그 이상은 스테미너가 제한한다.</summary>
    public sealed class DashState : PlayerStateBase
    {
        public override PlayerState StateId => PlayerState.Dash;

        public override void Enter(PlayerStateMachine machine, NetworkInputData input)
        {
            base.Enter(machine, input);

            // 평타·스킬을 끊고 들어온 경우가 있다 — 남은 타격·이펙트와 무기 고유 진행 상태를 여기서 버린다.
            machine.Weapon?.Cancel();

            // 이동 입력이 없으면 방향을 틀지 않고 그 자리에서 뒤로 빠진다(백대시).
            bool backward = !FaceMoveDirection(machine, input);
            machine.Weapon?.OnDashPressed(backward);
        }

        public override PlayerState Execute(PlayerStateMachine machine, NetworkInputData input)
        {
            var weapon = machine.Weapon;
            if (weapon == null) return PlayerState.Idle;

            return weapon.IsDashFinished() ? PlayerState.Idle : PlayerState.Dash;
        }

        /// <summary>대시는 조준점이 아니라 이동 입력 방향으로 구른다.
        /// (실제 이동은 무기가 요청한 전진을 상태머신이 틱마다 적용한다 — transform.forward 기준.
        /// 백대시는 그 거리가 음수라 몸을 안 돌려도 뒤로 빠진다.)</summary>
        /// <returns>이동 입력이 있어 방향을 잡았으면 true. false면 제자리 = 백대시.</returns>
        private static bool FaceMoveDirection(PlayerStateMachine machine, NetworkInputData input)
        {
            Vector3 dir = new Vector3(input.Move.x, 0f, input.Move.y);
            if (dir.sqrMagnitude < 0.01f) return false;

            machine.Movement.Face(dir);
            return true;
        }
    }
}
