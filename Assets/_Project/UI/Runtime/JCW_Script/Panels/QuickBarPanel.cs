using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 퀵바(인벤토리·캐릭터정보·옵션·기타). 게임플레이 이벤트가 아니라 유저 입력에 반응하므로
    /// Orchestrator를 거치지 않고 독립적으로 동작한다.
    ///
    /// 각 버튼은 대상 패널 GameObject를 켜고 끈다(토글). 버튼→패널 짝은 인스펙터에서 지정한다.
    /// </summary>
    public sealed class QuickBarPanel : MonoBehaviour
    {
        [Serializable]
        private struct MenuButton
        {
            public Button Button;      // 퀵바의 버튼
            public GameObject Panel;    // 이 버튼이 여닫는 패널
        }

        [SerializeField] private MenuButton[] _buttons;

        private void Awake()
        {
            if (_buttons == null) return;
            foreach (MenuButton entry in _buttons)
            {
                if (entry.Button == null || entry.Panel == null) continue;
                GameObject panel = entry.Panel; // 클로저 캡처용 로컬
                entry.Button.onClick.AddListener(() => Toggle(panel));
            }
        }

        /// <summary>패널을 열려있으면 닫고, 닫혀있으면 연다.</summary>
        public void Toggle(GameObject panel)
        {
            if (panel != null)
                panel.SetActive(!panel.activeSelf);
        }
    }
}
