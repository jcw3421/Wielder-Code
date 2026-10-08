using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>장착한 무기의 모델링만 켠다. 무기 로직(WeaponBase)과 보이는 메시가 분리돼 있어,
    /// 로직 교체와 별개로 이 컴포넌트가 <see cref="GameplayEvents.WeaponEquipped"/>를 받아 메시를 토글한다.
    ///
    /// 모델은 프리팹에 미리 배치해 두고 켜고 끄기만 한다(무기마다 부착 위치·스케일이 달라 런타임 생성이 아님).
    /// 아바타마다 붙으므로 playerId로 내 것만 반영한다 — 파티원 아바타도 각자 자기 이벤트로 바뀐다.</summary>
    public sealed class WeaponModelSelector : NetworkBehaviour
    {
        [System.Serializable]
        private struct WeaponModel
        {
            [Tooltip("WeaponRecord의 Id. 인벤토리·IWeaponDB가 쓰는 값과 같아야 한다.")]
            public int WeaponId;
            [Tooltip("이 무기를 들었을 때 켤 모델링 오브젝트.")]
            public GameObject Model;
        }

        [SerializeField] private WeaponModel[] _models;

        private int _playerId;

        public override void Spawned()
        {
            _playerId = Object.InputAuthority.PlayerId;
            GameplayEvents.WeaponEquipped += HandleWeaponEquipped;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
            => GameplayEvents.WeaponEquipped -= HandleWeaponEquipped;

        private void HandleWeaponEquipped(int weaponId, int playerId)
        {
            if (playerId != _playerId) return;

            foreach (WeaponModel entry in _models)
                entry.Model.SetActive(entry.WeaponId == weaponId);
        }
    }
}
