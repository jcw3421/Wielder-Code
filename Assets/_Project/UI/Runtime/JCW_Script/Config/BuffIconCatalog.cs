using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// <see cref="BuffType"/> → 아이콘 매핑 에셋. 유저·파티원·보스가 공용으로 참조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "BuffIconCatalog", menuName = "Game/UI/Buff Icon Catalog")]
    public sealed class BuffIconCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public BuffType Type;
            public Sprite Icon;
        }

        [SerializeField] private Entry[] _entries;

        /// <summary>해당 버프 아이콘. 등록 안 됐으면 null.</summary>
        public Sprite GetIcon(BuffType type)
        {
            if (_entries == null) return null;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Type == type)
                    return _entries[i].Icon;
            return null;
        }
    }
}
