using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 무기별 컷인 일러스트를 미리 배치해 두고, 해당 무기 것만 켠다.
    /// (일러스트마다 크기·구도가 달라 sprite 교체 대신 오브젝트 토글로 처리한다 —
    ///  각 이미지를 인스펙터에서 미리 맞춰둘 수 있다. <see cref="ClassIconSelector"/>와 같은 방식.)
    /// </summary>
    public sealed class WeaponCutInSelector : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            public WeaponRecord Weapon;
            public GameObject Illustration;
        }

        [SerializeField] private Entry[] _entries;

        /// <summary>해당 무기 일러스트만 켜고 나머지는 끈다. 등록된 게 없으면 전부 끄고 false.</summary>
        public bool Show(int weaponId)
        {
            bool found = false;

            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (entry.Illustration == null) continue;

                bool match = entry.Weapon != null && entry.Weapon.Id == weaponId;
                entry.Illustration.SetActive(match);
                found |= match;
            }

            return found;
        }
    }
}
