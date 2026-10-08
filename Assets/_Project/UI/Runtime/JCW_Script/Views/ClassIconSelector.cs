using System;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 직업별 아이콘을 미리 배치해 두고, 현재 직업에 맞는 하나만 켠다.
    /// (아이콘마다 스타일·프레임이 달라 sprite 교체가 아니라 오브젝트 토글로 처리한다.)
    /// </summary>
    public sealed class ClassIconSelector : MonoBehaviour
    {
        [Serializable]
        private struct ClassIcon
        {
            public ClassType ClassType;
            public GameObject Icon;
        }

        [SerializeField] private ClassIcon[] _icons;

        /// <summary>해당 직업 아이콘만 켜고 나머지는 끈다.</summary>
        public void Show(ClassType classType)
        {
            for (int iconIndex = 0; iconIndex < _icons.Length; iconIndex++)
            {
                ClassIcon entry = _icons[iconIndex];
                entry.Icon.SetActive(entry.ClassType == classType);
            }
        }
    }
}
