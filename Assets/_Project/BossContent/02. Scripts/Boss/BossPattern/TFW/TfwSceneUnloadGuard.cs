using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Game.BossContent
{
    /// <summary>씬이 바뀌기 직전에 보스를 스스로 먼저 퇴장시킨다. TFW 보스 프리팹에만 붙인다.
    ///
    /// 왜 필요한가: 파티가 전멸하면 로비로 돌아가는데, 그때 보스는 아직 살아 있다.
    /// Fusion은 씬을 내리기 직전 자기 로딩 코루틴 안에서 현재 씬의 런타임 스폰 오브젝트를
    /// 한꺼번에 디스폰한다(NetworkSceneManagerDefault.DestroyAllRuntimeSpawnedObjectsInScene).
    /// 그 일괄 디스폰에 살아 있는 TFW 보스가 걸리면 에디터가 응답 없이 멈춘다 —
    /// 전 스레드가 Wait에 걸린 데드락이라 강제 종료 외에는 방법이 없다.
    ///
    /// 같은 보스라도 평소 사망 후 디스폰(BossEntity.TickDespawn)은 멀쩡하고, 보스를 이미 잡은 뒤
    /// 포탈로 로비에 가는 것도 멀쩡하다. 즉 문제는 "무엇을 지우는가"가 아니라 "어떻게 지우는가"다.
    /// 그래서 그 일괄 루프가 돌기 전에 보스가 자기 발로 나간다.
    ///
    /// <see cref="INetworkRunnerCallbacks.OnSceneLoadStart"/>는 로딩 코루틴의 첫 줄이라
    /// 그 루프보다 반드시 앞선다. 여기서 디스폰해 두면 루프가 보스를 만나지 않는다.
    ///
    /// 이 컴포넌트는 연출·판정에 관여하지 않는다. 보스가 사라지는 시점만 앞당길 뿐이라
    /// 게임 로직(BossDefeatedCondition 등)은 평소와 똑같이 "보스가 러너에서 사라졌다"로 읽는다.</summary>
    [DisallowMultipleComponent]
    public sealed class TfwSceneUnloadGuard : NetworkBehaviour, INetworkRunnerCallbacks
    {
        private bool _registered;

        // 씬 밖으로 빼 둔 보스를 실제로 디스폰할 차례인지.
        private bool _despawnQueued;

        public override void Spawned()
        {
            // 디스폰은 서버(State Authority)만 할 수 있으므로 콜백도 서버에서만 받는다.
            if (!HasStateAuthority) return;

            Runner.AddCallbacks(this);
            _registered = true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!_despawnQueued) return;
            _despawnQueued = false;
            DespawnNow();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (!_registered) return;

            _registered = false;
            if (runner != null) runner.RemoveCallbacks(this);
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
            if (!_registered) return;
            if (Object == null || !Object.IsValid) return;   // 이미 퇴장 처리된 뒤

            // 여기서 곧바로 Despawn하면 여전히 Fusion 로딩 코루틴 안이다. 대신 보스를 현재 씬 밖으로
            // 빼 둔다 — 일괄 디스폰 루프는 "현재 씬 소속"만 고르므로 아예 후보에서 빠진다.
            // 실제 퇴장은 씬 로딩이 끝난 뒤 평범한 문맥에서 한다.
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            _despawnQueued = true;
        }

        /// 씬 로딩이 끝났으면 이제 평범하게 퇴장시킨다. FixedUpdateNetwork가 먼저 돌면 그쪽이 처리한다.
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (!_despawnQueued) return;
            _despawnQueued = false;
            DespawnNow();
        }

        private void DespawnNow()
        {
            if (Object == null || !Object.IsValid) return;

            _registered = false;
            Runner.RemoveCallbacks(this);
            Runner.Despawn(Object);
        }

        // ── 나머지 콜백은 쓰지 않는다 ──────────────────────────────
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
