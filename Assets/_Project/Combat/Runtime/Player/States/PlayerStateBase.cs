using Game.Core;
using Game.Networking;
using UnityEngine;

namespace Game.Combat.Player
{
    public abstract class PlayerStateBase
    {
        private ushort _prevButtons;

        public abstract PlayerState StateId { get; }
        public virtual void Enter(PlayerStateMachine machine, NetworkInputData input)
        {
            _prevButtons = input.Buttons;
        }
        public abstract PlayerState Execute(PlayerStateMachine machine, NetworkInputData input);
        public virtual void Exit(PlayerStateMachine machine, NetworkInputData input) { }

        /// <summary>이번 틱에 새로 눌렸는가(누른 순간 1회). 판정을 여기 한 곳에만 두어야
        /// 대시와 나머지 액션의 입력 규칙이 갈리지 않는다.</summary>
        protected bool IsRising(NetworkInputData input, InputIntent intent)
            => input.Has(intent) && (_prevButtons & (ushort)intent) == 0;

        /// 눌림 판정을 끝냈으면 이번 틱 버튼을 기록한다. 안 부르면 다음 틱에도 "새로 눌림"으로 보인다.
        protected void CommitButtons(NetworkInputData input) => _prevButtons = input.Buttons;

        protected PlayerState? TryCombatTransition(PlayerStateMachine machine, NetworkInputData input)
        {
            var weapon = machine.Weapon;
            if (weapon == null) return null;

            PlayerState? result = null;
            // 대시가 최우선 — 회피는 다른 행동에 밀리면 안 된다.
            if (IsRising(input, InputIntent.Dash) && weapon.CanDash) result = PlayerState.Dash;
            else if (IsRising(input, InputIntent.Ultimate) && weapon.CanUltimate) result = PlayerState.Ultimate;
            else if (IsRising(input, InputIntent.Skill) && weapon.CanSkill) result = PlayerState.Skill;
            else if (IsRising(input, InputIntent.Attack) && weapon.CanAttack) result = PlayerState.Attacking;

            CommitButtons(input);
            return result;
        }

        /// <summary>조준점을 향해 즉시 회전. transform.rotation 직접 대입은 KCC가 덮어써서 무효.</summary>
        protected static void FaceAimPoint(PlayerStateMachine machine, NetworkInputData input)
        {
            machine.Movement.Face(input.AimPoint - machine.transform.position);
        }

        protected PlayerState Move(PlayerStateMachine machine, NetworkInputData input)
        {
            // 클라 예측(PredictMovement)과 동일한 적용부를 공유해야 결정론이 유지된다.
            bool isMoving = machine.ApplyMovement(input);
            return isMoving ? PlayerState.Moving : PlayerState.Idle;
        }
    }
}
