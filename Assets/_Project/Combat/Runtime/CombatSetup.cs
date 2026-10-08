using Game.Combat.Chain;
using Game.Combat.Player;
using Game.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>
    /// 플레이어 스폰 이벤트를 구독하여 Combat 컴포넌트 간 참조를 와이어링한다.
    /// 씬에 하나 배치하고, ChainController/FullChainResolver를 인스펙터에서 연결한다.
    /// Networking → Combat 참조 불가 문제를 Core 이벤트로 우회한다.
    /// </summary>
    public sealed class CombatSetup : MonoBehaviour
    {
        [SerializeField] private ChainController _chainController;

        private void OnEnable()
        {
            GameplayEvents.PlayerSpawned += OnPlayerSpawned;
            WireAlreadySpawnedAvatars();
        }

        /// <summary>이미 살아 있는 아바타의 무기를 이 씬의 ChainController에 다시 물린다.
        ///
        /// 아바타는 DontDestroyOnLoad라 씬을 옮겨도 다시 스폰되지 않는다. 그래서 스폰 뒤에
        /// 로드된 씬의 CombatSetup은 <see cref="GameplayEvents.PlayerSpawned"/>를 영영 못 받고,
        /// 그 씬의 ChainController는 아무 무기와도 연결되지 않은 채 놀게 된다(체인이 안 이어진다).
        /// 씬마다 컨트롤러가 따로 있으므로, 켜질 때 현재 아바타들을 이쪽으로 다시 물려야 한다.</summary>
        private void WireAlreadySpawnedAvatars()
        {
            if (_chainController == null) return;

            var containers = FindObjectsByType<WeaponContainer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var weapons in containers)
            {
                if (weapons == null || weapons.Data == null) continue;

                // 아직 Spawned()가 안 불린 무기는 건너뛴다. 클라이언트로 접속하면 이 컴포넌트가
                // 아바타보다 먼저 인스턴스화될 수 있는데, Initialize가 [Networked] Gauge를 건드려
                // "Networked properties can only be accessed when Spawned() has been called"로 터진다.
                // 그 무기들은 나중에 PlayerSpawned를 타고 OnPlayerSpawned에서 배선된다.
                foreach (var w in weapons.Data.Values)
                    if (w != null && w.Object != null && w.Object.IsValid)
                        w.Initialize(_chainController);
            }
        }

        private void OnDisable()
        {
            GameplayEvents.PlayerSpawned -= OnPlayerSpawned;
        }

        private void OnPlayerSpawned(int playerId, GameObject avatar)
        {
            var lifeCycle = avatar.GetComponentInChildren<PlayerLifeCycle>();
            if (lifeCycle != null)
                lifeCycle.Initialize(playerId);

            var weapons = avatar.GetComponent<WeaponContainer>();
            if (weapons != null)
                foreach (var w in weapons.Data.Values)
                {
                    w.Initialize(_chainController);
                    w.gameObject.SetActive(false);
                }

            var stateMachine = avatar.GetComponentInChildren<PlayerStateMachine>();
            if (stateMachine != null)
            {
                var movement = avatar.GetComponentInChildren<PlayerMovement>();
                stateMachine.Initialize(movement);
            }
        }
    }
}
