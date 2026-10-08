using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// <see cref="DebuffType"/> → 아이콘 매핑 에셋. 유저·파티원·보스가 공용으로 참조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "DebuffIconCatalog", menuName = "Game/UI/Debuff Icon Catalog")]
    public sealed class DebuffIconCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public DebuffType Type;
            public Sprite Icon;
        }

        [SerializeField] private Entry[] _entries;

        /// <summary>해당 디버프 아이콘을 찾는다. 없으면 null(아이콘 미지정 허용).</summary>
        public Sprite GetIcon(DebuffType type)
        {
            if (_entries == null) return null;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Type == type)
                    return _entries[i].Icon;
            return null;
        }
    }
}
