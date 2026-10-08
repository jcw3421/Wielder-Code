using Fusion;
using Game.Core;
using Game.Networking;

namespace Game.Combat.Player
{
    /// <summary>플레이어의 공격 스탯 소유자. PlayerLifeCycle이 방어(체력)를 갖는 것과 같은 위계로
    /// 공격 쪽을 담당한다. 임시 스탯은 Core의 TemporaryStatRequested로 들어와 여기서 적용된다.
    /// <b>수명은 요청을 보낸 쪽이 갖는다</b> — 만료되면 값 0으로 다시 요청이 오고 그때 걷는다.
    /// 여기서 따로 시간을 세면 같은 버프의 만료 시점이 두 군데서 관리된다.
    /// 상태 변경은 서버에서만 일어난다(루트 CLAUDE.md §1).</summary>
    public sealed class PlayerCombatStats : ServerAuthoritativeBehaviour
    {
        /// 공격력 임시 증가분. 무기가 기본 공격력에 더해 쓴다.
        /// 네트워크 상태로 둬서 중간 입장한 클라이언트도 현재 버프 상태를 그대로 받는다
        /// (RPC로 알리면 입장 전에 지나간 통지를 놓친다).
        [Networked, OnChangedRender(nameof(OnAttackPowerBonusChanged))]
        public int AttackPowerBonus { get; private set; }

        private PlayerLifeCycle _lifeCycle;
        private bool _wasDead;

        private int PlayerId => Object.InputAuthority.PlayerId;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            _lifeCycle = GetComponent<PlayerLifeCycle>();
            GameplayEvents.TemporaryStatRequested += OnTemporaryStatRequested;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
            => GameplayEvents.TemporaryStatRequested -= OnTemporaryStatRequested;

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 죽으면 임시 스탯도 같이 걷힌다 — PlayerBuffable이 사망 시 버프를 걷는 것과 맞춘다.
            // 안 그러면 부활한 뒤에도 죽기 전 버프가 남는다.
            bool dead = _lifeCycle != null && _lifeCycle.IsDead;
            if (!dead)
            {
                _wasDead = false;
                return;
            }

            if (_wasDead) return;
            _wasDead = true;
            AttackPowerBonus = 0;
        }

        /// 다른 모듈·시스템이 보낸 임시 스탯 요청. 대상이 이 플레이어일 때만 받는다.
        /// durationTicks가 0 이하면 "걷으라"는 뜻이다(요청한 쪽이 만료를 알려온 것).
        private void OnTemporaryStatRequested(int playerId, StatModifier modifier, int durationTicks)
        {
            if (!HasStateAuthority) return;
            if (playerId != PlayerId) return;

            // 이 컴포넌트가 소유하는 임시 스탯은 공격력뿐이다. MaxHealth는 PlayerLifeCycle 소유.
            if (modifier.Type != StatType.AttackPower) return;

            AttackPowerBonus = durationTicks > 0 ? modifier.Value : 0;
        }

        private void OnAttackPowerBonusChanged()
            => GameplayEvents.RaisePlayerStatChanged(
                PlayerId, StatType.AttackPower, StatLayer.Temporary, AttackPowerBonus);
    }
}
