using Fusion;
using Game.Core;
using Game.Core.Onboarding;
using Game.Networking;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>플레이어 1인의 생존 상태(체력·사망·부활)를 단독 소유한다.
    /// <see cref="IDamageable"/> 구현체 — 데미지 소스가 콜라이더에서 <c>GetComponent&lt;IDamageable&gt;</c>로
    /// 찾으므로 반드시 플레이어 콜라이더와 같은 GameObject(아바타 루트)에 둔다.
    ///
    /// 권한: 체력/사망/부활은 게임 상태 → 서버(State Authority)에서만 변경(ADR-001, ADR-003).
    /// 정책 분리: "언제 부활시킬지"는 이 컴포넌트가 모른다. 외부(호스트 측)가
    /// <see cref="GameplayEvents.PlayerReviveRequested"/>를 발행하면 서버에서 이를 소비해 부활만 수행한다.</summary>
    public sealed class PlayerLifeCycle : ServerAuthoritativeBehaviour, IDamageable
    {
        [Networked, OnChangedRender(nameof(OnHealthChanged))] public int CurrentHealth { get; private set; }
        [Networked] public int MaxHealth { get; private set; }
        [Networked, OnChangedRender(nameof(OnDeadChanged))] public NetworkBool IsDead { get; private set; }

        private int _playerId = -1;
        public void Initialize(int playerId) => _playerId = playerId;

        // 막기처럼 "받는 피해"를 바꾸는 동작은 무기가 구간별로 알고 있다. CC 면역과 같은 방식으로
        // 무기에게 물어본다(PlayerCrowdControlController의 CurrentImmunity와 동일한 배선).
        private PlayerStateMachine _machine;

        public override void Spawned()
        {
            _machine = GetComponent<PlayerStateMachine>();

            if (!HasStateAuthority) return;
            MaxHealth = PlayerStatsConstants.BaseMaxHealth;     // Todo: + PlayerProfile StatModifier 합산
            CurrentHealth = MaxHealth;
            IsDead = false;
        }

        private void OnEnable()  => GameplayEvents.PlayerReviveRequested += OnReviveRequested;
        private void OnDisable() => GameplayEvents.PlayerReviveRequested -= OnReviveRequested;

        public void ApplyDamage(in DamageInfo info)
        {
            if (!HasStateAuthority) return;
            if (IsDead) return;
            // 대시 무적 — 막기(감면)와 달리 아예 사건이 없었던 것으로 친다.
            if (_machine != null && _machine.IsInvulnerable) return;

            // 감면이 걸렸다면 방어에 성공한 것이다. 0%면 저스트가드(완전 무효), 그 사이면 일반 막기.
            // 완전 무효는 아래에서 조기 반환하므로 알림은 그 전에 보낸다.
            int percent = IncomingDamagePercent();
            if (percent < 100)
            {
                // 막아낸 사실은 무기도 알아야 한다 — 막기 성공에 자원을 돌려주는 무기가 있다.
                _machine?.Weapon?.OnGuardSucceeded(percent == 0);
                RPC_ShowGuardSuccess(percent == 0);
            }

            int amount = info.Amount * percent / 100;
            if (amount <= 0) return;   // 완전히 막았다 — 체력도 데미지 숫자도 건드리지 않는다

            RPC_ShowDamageNumber(amount, transform.position, DamageType.Normal, bossID: info.SourcePlayerId);
            // 연습 구간이 열려 있으면 체력이 1 밑으로 내려가지 않는다. 이 컴포넌트는 그런 구간이
            // 있다는 것만 알고, 누가 왜 열었는지는 모른다(Game.Core.Onboarding 계약).
            CurrentHealth = Mathf.Max(OnboardingRules.PlayersCannotDie ? 1 : 0, CurrentHealth - amount);
            if (CurrentHealth == 0)
                IsDead = true;
        }

        /// 지금 장착한 무기가 보고하는 피해 비율(%). 무기가 없으면 감면 없음.
        private int IncomingDamagePercent() => _machine?.Weapon?.IncomingDamagePercent ?? 100;

        public void Heal(int amount)
        {
            if (!HasStateAuthority) return;
            if (IsDead) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        private void OnReviveRequested(int playerId)
        {
            if (!HasStateAuthority) return;
            if (playerId != _playerId || !IsDead) return; 

            CurrentHealth = MaxHealth; 
            IsDead = false; 
            // 부활 위치(제자리 vs 스폰포인트)는 미정 — 필요 시 NetworkCharacterController.Teleport(서버).
        }

        private void OnHealthChanged()
            => GameplayEvents.RaisePlayerHealthChanged(_playerId, CurrentHealth, MaxHealth);

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowDamageNumber(int amount, Vector3 worldPos, DamageType type, int bossID)
            => GameplayEvents.RaiseDamageDealt(Object.InputAuthority.PlayerId, amount, worldPos, type, bossID);

        // 막은 당사자 화면에만 뜨는 표시다 — 다른 클라에는 아예 보내지 않는다.
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_ShowGuardSuccess(NetworkBool isJustGuard)
            => GameplayEvents.RaiseGuardSucceeded(Object.InputAuthority.PlayerId, isJustGuard);

        private void OnDeadChanged()
        {
            if(IsDead) GameplayEvents.RaisePlayerDied(_playerId);
            else GameplayEvents.RaisePlayerRevived(_playerId);
        }
    }
}
