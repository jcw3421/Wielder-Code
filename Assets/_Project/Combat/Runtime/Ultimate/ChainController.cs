using Fusion;                       // NetworkBehaviour, [Networked], TickTimer
using Game.Core;                    // ChainConstants, GameplayEvents
using Game.Networking;              // ServerAuthoritativeBehaviour
using System;
using UnityEngine;                  // Debug

namespace Game.Combat.Chain         // 체인 시스템 네임스페이스
{
    /// <summary>
    /// 팀 체인 상태기계의 단일 권한 지점. 인스턴스 1개, 서버 소유.
    /// 임의 플레이어의 궁극기 <b>적중</b> 신호를 받아 체인 단계를 올리고,
    /// 4회 누적 시 FullChainResolver를 통해 풀체인을 발동한다.
    /// 클라이언트 간 합의/경쟁 상태를 만들지 않는다(루트 CLAUDE.md §1).
    /// </summary>
    public sealed class ChainController : ServerAuthoritativeBehaviour
    {
        const int MAXPLAYER = ChainConstants.FullChainRequiredLinks;

        [Networked, OnChangedRender(nameof(OnStageChanged))] private int Stage { get; set; }
        [Networked, Capacity(MAXPLAYER + 1)]
        private NetworkArray<int> ChainedPlayerId => default;
        [Networked] TickTimer ChainConnectTimer { get; set; }

        // 사람이 아닌 쪽이 채운 링크의 주인 자리에 넣는 값. -1은 "빈 슬롯"이라 쓸 수 없고,
        // 0 이상은 실제 PlayerId와 겹친다. 그래서 둘 다 아닌 값을 따로 둔다.
        private const int SeededPlayerId = -2;

        private Action<Vector3> fullChainAction;

        // 풀체인 이펙트·판정이 놓일 월드 지점. 팀 합동기라 시전자에 붙으면 안 된다.
        // Combat은 BossContent를 참조할 수 없고(루트 CLAUDE.md §2) Core에도 보스 위치를 주는
        // 계약이 없으므로, "보스 위치"를 직접 묻는 대신 타격이 실제로 들어간 지점으로 대신한다.
        // 보스전에서는 사실상 보스 자리가 된다. 풀체인 효과를 스타터 무기에서 가져오므로 앵커도 같은 출처에서 —
        // 스타터의 궁이 맞힌 대상으로 잡는다. 좌표가 아니라 대상을 기억해 두고 발동 순간에 현재 위치를 읽는다:
        // 체인 연결에 최대 6초가 걸리는데 좌표를 박아두면 그 사이 움직인 보스를 놓치고 허공에 터진다.
        private Transform anchorTarget;
        private Vector3 anchorFallback;   // 대상이 죽어 사라졌을 때 쓸, 맞힌 순간의 좌표

        // OnStageChanged가 마지막으로 알린 단계. Despawn 시점엔 [Networked] 상태를 믿을 수 없어
        // 화면에 체인 UI가 떠 있는지는 이 로컬 값으로 판단한다.
        private int _renderedStage;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            Stage = 0;
            ChainedPlayerId.Set(0, -1);
        }

        // 씬 전환 등으로 체인이 살아 있는 채 이 오브젝트가 사라질 때.
        // 이 컨트롤러는 씬마다 새로 놓이지만 HUD는 씬을 넘어 살아남는다 —
        // 종료 신호 없이 사라지면 체인 UI가 0초에서 멈춘 채 계속 떠 있게 된다.
        // 만료와 같은 stage 0 신호를 각 피어가 자기 자신에게 알린다(이미 내려가는 중이라 RPC는 못 쓴다).
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (_renderedStage <= 0) return;

            _renderedStage = 0;
            GameplayEvents.RaiseChainProgress(-1, 0, 0);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 씨앗 링크가 걸려 있으면 매 틱 채운다. 채울 때 연결 창도 함께 되감기므로
            // 만료로 풀리지 않는다 — 플레이어가 궁을 언제 쓰든 링크가 기다리고 있다.
            if (SeedLinkTarget > 0) SeedLinks(SeedLinkTarget);

            if (Stage == 0) return;

            if (ChainConnectTimer.Expired(Runner))
            {
                ResetChain();
            }
        }

        /// <summary>사람이 아닌 쪽이 매 틱 채워 둘 링크 수. 0이면 평소대로 동작한다.
        /// 네트워크 상태가 아니라 서버 로컬 설정이다 — 서버에서만 의미가 있다.
        ///
        /// 왜 채우는 일을 이 클래스가 하는가: 체인 상태는 전부 여기 private [Networked]다. 밖에서 매 틱
        /// 밀어 넣게 하면 호출자가 Fusion 틱 루프 안에 있어야 하고, 그러자고 호출자마다 NetworkObject를
        /// 달아야 한다. 켜고 끄기만 밖에서 하면 그 부담이 없어진다.
        ///
        /// 지금 이걸 켜는 곳은 온보딩의 혼자 연습이지만, 이 클래스는 누가 왜 켰는지 모른다.</summary>
        public int SeedLinkTarget { get; set; }

        /// <summary>사람이 아닌 쪽이 체인을 <paramref name="count"/>개까지 끌어올린다(서버 전용).
        /// 마지막 링크는 반드시 사람 몫으로 남기므로 이것만으로는 풀체인이 터지지 않는다.
        ///
        /// 채울 때마다 연결 창을 되감으므로, 매 틱 부르는 쪽이 있으면 만료로 풀리지 않는다.
        /// 효과·앵커는 건드리지 않는다 — 그것들은 진짜 궁을 쓴 스타터에게서만 온다.
        ///
        /// 언제·왜 채울지는 이 클래스가 정하지 않는다. 지금 쓰는 곳은 온보딩의 혼자 연습이다.</summary>
        public void SeedLinks(int count)
        {
            if (!HasStateAuthority) return;   // 체인 상태 변경은 서버 단독(루트 CLAUDE.md §1)

            int target = Mathf.Min(count, ChainConstants.FullChainRequiredLinks - 1);
            if (Stage >= target) return;

            while (Stage < target)
            {
                Stage++;
                ChainedPlayerId.Set(Stage, SeededPlayerId);
            }

            ChainConnectTimer = TickTimer.CreateFromSeconds(Runner, ChainConstants.ChainWindowSeconds);
        }

        /// 체인을 완전히 비운다. 단계만 0으로 되돌리면 다음 체인이 이전 스타터의 효과와
        /// 참여자 기록을 물고 시작한다.
        private void ResetChain()
        {
            if (!HasStateAuthority) return;

            Stage = 0;
            ChainConnectTimer = default;
            fullChainAction = null;
            anchorTarget = null;

            for (int i = 0; i < MAXPLAYER + 1; i++)
                ChainedPlayerId.Set(i, -1);
        }

        /// 서버에서만 호출. 궁이 <b>실제로 적중한</b> 순간 그 대상과 함께 링크를 연결한다.
        /// 허공에 휘두른 궁은 여기까지 오지 않으므로 체인을 잇지 못한다.
        /// 예외로 데미지 판정이 없는 지원형 궁은 시전 즉시 오며, 이때 hitTarget은 null이다.
        public void HookFullChain(Action<Vector3> FullChainAction, int playerID, Transform hitTarget, Vector3 hitPoint)
        {
            if (!HasStateAuthority) return;

            // 효과도 앵커도 스타터 한 곳에서 온다. 뒤에 붙는 참여자는 단계만 올린다.
            // 판단 기준이 Stage가 아니라 효과의 유무인 이유: 씨앗 링크(SeedLinks)는 단계만 올리고
            // 효과를 남기지 않으므로, Stage로 보면 첫 진짜 궁이 "뒤에 붙는 참여자"로 취급돼
            // 풀체인이 효과 없이 터진다. 일반 플레이에선 두 조건이 같은 시점을 가리킨다.
            if (fullChainAction == null)
            {
                fullChainAction = FullChainAction;
                anchorTarget = hitTarget;
                anchorFallback = hitPoint;
            }

            ChainConnectTimer = TickTimer.CreateFromSeconds(Runner, ChainConstants.ChainWindowSeconds);
            Stage++;
            ChainedPlayerId.Set(Stage, playerID);

            if (Stage >= ChainConstants.FullChainRequiredLinks)
                TriggerFullChain();
        }
        private void TriggerFullChain()
        {
            // 초기화 전에 확정값을 지역에 보존한다. RPC가 수신 측에서 Stage를 다시 읽으면
            // 이미 0으로 리셋된 값을 보게 되어 참여 인원이 0으로 전달된다.
            int participants = Stage;
            int lastPlayerId = ChainedPlayerId[Stage];
            var starterEffect = fullChainAction;

            // 대상이 아직 살아 있으면 지금 위치를, 죽어서 사라졌으면 맞힌 순간의 좌표를 쓴다.
            var anchor = anchorTarget != null ? anchorTarget.position : anchorFallback;

            // 체인을 완성시킨 마지막 링크는 OnChangedRender를 타지 못한다 — 같은 틱 안에서
            // Stage가 올랐다가 바로 0으로 되돌아가므로 렌더 시점엔 0만 보인다.
            // 그래서 완성 링크만 여기서 직접 알린다(나머지 단계는 OnStageChanged가 담당).
            RPC_RaiseChainProgress(lastPlayerId, participants);

            ResetChain();

            starterEffect?.Invoke(anchor);
            RPC_RaiseFullChainTriggered(participants);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_RaiseFullChainTriggered(int participants)
            => GameplayEvents.RaiseFullChainTriggered(participants);

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        void RPC_RaiseChainProgress(int playerId, int stage)
            => GameplayEvents.RaiseChainProgress(playerId, stage, ChainConstants.ChainWindowSeconds);
        void OnStageChanged()
        {
            _renderedStage = Stage;
            GameplayEvents.RaiseChainProgress(ChainedPlayerId[Stage], Stage, ChainConstants.ChainWindowSeconds);
        }

    }
}
