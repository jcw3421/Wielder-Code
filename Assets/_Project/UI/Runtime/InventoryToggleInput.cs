using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public class InventoryToggleInput : MonoBehaviour, IToggleUI
    {
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _inventoryTooltipPanel;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private InputActionReference _toggleInventoryAction;

        [Tooltip("인벤토리를 열 수 있는 씬 이름들. 여기에 없는 씬에서는 토글 키가 먹지 않고, " +
                 "열린 채로 그런 씬에 들어가면 닫힌다.")]
        [SerializeField] private string[] _allowedScenes = { "Lobby", "Onboarding" };

        private bool _allowedHere;

        public bool IsOpen => _inventoryPanel != null && _inventoryPanel.activeSelf;

        private void Start()
        {
            _inventoryPanel.SetActive(false);
            _inventoryTooltipPanel.SetActive(false);
        }

        private void OnEnable()
        {
            _toggleInventoryAction.action.performed += OnToggleKey;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _allowedHere = IsAllowed(SceneManager.GetActiveScene().name);
        }

        private void OnDisable()
        {
            _toggleInventoryAction.action.performed -= OnToggleKey;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _allowedHere = IsAllowed(scene.name);
            if (!_allowedHere && IsOpen)
            {
                if (ServiceLocator.TryGet<IToggleUIManager>(out var mgr))
                    mgr.Close(this);
            }
        }

        private void OnToggleKey(InputAction.CallbackContext context)
        {
            if (InputGuard.SuppressKeyboard) return;   // 채팅 등 InputField 입력 중
            if (!_allowedHere) return;
            if (!ServiceLocator.TryGet<IToggleUIManager>(out var mgr)) return;

            if (IsOpen)
                mgr.Close(this);
            else
                mgr.Open(this);
        }

        private bool IsAllowed(string sceneName)
        {
            if (_allowedScenes == null) return false;

            foreach (var allowed in _allowedScenes)
                if (allowed == sceneName)
                    return true;

            return false;
        }

        public void OnManagedOpen(int sortingOrder)
        {
            _inventoryTooltipPanel.SetActive(false);
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = sortingOrder;
            }
            _inventoryPanel.SetActive(true);

            GameplayEvents.RaiseInventoryOpened();
        }

        public void OnManagedClose()
        {
            _inventoryTooltipPanel.SetActive(false);
            _inventoryPanel.SetActive(false);
        }
    }
}
