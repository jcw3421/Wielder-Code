using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Networking
{
    /// <summary>플레이어 채팅. 클라이언트는 요청만 하고, 전파는 서버가 한다(루트 CLAUDE.md 1).
    ///
    /// 흐름: 로컬 UI가 <see cref="SendChat"/> → (호스트면 바로, 아니면 RPC로) 서버 →
    /// 서버가 길이 검증 후 <see cref="Rpc_Broadcast"/>로 전원에게 전파 →
    /// 각 피어가 <see cref="GameplayEvents.ChatMessageReceived"/>를 발행 → ChatPanel이 표시.
    ///
    /// 닉네임은 같은 오브젝트의 <see cref="PlayerIdentity"/>에서 서버가 읽는다(클라이언트가 보낸 이름을 믿지 않음).
    /// 플레이어 프리팹에 PlayerIdentity와 함께 붙인다.</summary>
    [RequireComponent(typeof(PlayerIdentity))]
    public sealed class PlayerChat : ServerAuthoritativeBehaviour, IChatService
    {
        /// <summary>한 메시지의 최대 글자 수. NetworkString&lt;_64&gt; 용량과 맞춘다.</summary>
        public const int MaxMessageLength = 64;

        private PlayerIdentity _identity;

        private void Awake()
        {
            _identity = GetComponent<PlayerIdentity>();
        }

        public override void Spawned()
        {
            // 보내기 창구는 내 캐릭터 하나뿐이다(남의 오브젝트로는 못 보낸다).
            if (Object.HasInputAuthority)
                ServiceLocator.Register<IChatService>(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Object.HasInputAuthority)
                ServiceLocator.Unregister<IChatService>();
        }

        public void SendChat(string message)
        {
            if (!Object.HasInputAuthority) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            message = message.Trim();
            if (message.Length > MaxMessageLength)
                message = message.Substring(0, MaxMessageLength);

            // 호스트는 자기 자신이 서버라 RPC 왕복 없이 바로 처리한다(EntryVoteService와 같은 분기).
            if (HasStateAuthority) ServerSend(message);
            else Rpc_RequestSend(message);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_RequestSend(NetworkString<_64> message) => ServerSend(message.Value);

        private void ServerSend(string message)
        {
            if (!RequireServer()) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            if (message.Length > MaxMessageLength)
                message = message.Substring(0, MaxMessageLength);

            Rpc_Broadcast(_identity != null ? _identity.Nickname : default, message);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_Broadcast(NetworkString<_32> nickname, NetworkString<_64> message)
        {
            var name = nickname.Value;
            if (string.IsNullOrEmpty(name))
                name = $"Player {Object.InputAuthority.PlayerId}";

            GameplayEvents.RaiseChatMessageReceived(Object.InputAuthority.PlayerId, name, message.Value);
        }
    }
}
