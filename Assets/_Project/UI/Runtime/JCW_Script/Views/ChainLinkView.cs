using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 체인 링크 하나. 링크가 이어지면 "이은 플레이어의 직업" 아이콘을 켜고, 끊기면 끈다.
    /// 아이콘은 <see cref="ClassIconSelector"/> 재사용 — 직업별 오브젝트를 미리 배치해 토글한다.
    /// </summary>
    public sealed class ChainLinkView : MonoBehaviour
    {
        [SerializeField] private ClassIconSelector _classIcon;

        public void Fill(ClassType classType)
        {
            _classIcon.gameObject.SetActive(true);
            _classIcon.Show(classType);
        }

        public void Clear() => _classIcon.gameObject.SetActive(false);
    }
}
