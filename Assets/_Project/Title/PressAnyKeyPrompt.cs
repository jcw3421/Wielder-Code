using Game.SDK.Stove;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>타이틀의 "PRESS ANY KEY" 안내. 문구를 점멸시키고, 아무 키·클릭이 들어오면
/// <see cref="SceneLoader.Load"/>를 호출해 다음 씬으로 넘긴다.
///
/// 버튼으로는 키보드 입력을 받을 수 없어(클릭·Submit만 반응) 별도 컴포넌트로 둔다.
/// 씬 전환 자체는 SceneLoader가 담당한다 — 여기선 입력 감지와 점멸 연출만 한다.</summary>
public sealed class PressAnyKeyPrompt : MonoBehaviour
{
    [SerializeField]
    [Tooltip("입력이 들어오면 Load()를 호출할 SceneLoader.")]
    private SceneLoader _sceneLoader;

    [SerializeField]
    [Tooltip("점멸시킬 문구의 CanvasGroup. 비워두면 점멸 없이 입력만 받는다.")]
    private CanvasGroup _promptGroup;

    [SerializeField]
    [Tooltip("점멸 1회에 걸리는 시간(초).")]
    [Min(0.1f)]
    private float _blinkCycleSeconds = 1.2f;

    [SerializeField]
    [Tooltip("점멸 시 가장 흐려지는 알파.")]
    [Range(0f, 1f)]
    private float _minAlpha = 0.2f;

    private TextMeshProUGUI _promptText;
    private bool _triggered;
    private bool textChanged;
    private bool _failShown;

    private void Start()
    {
        _promptText = _promptGroup.gameObject.GetComponent<TextMeshProUGUI>();
        _promptText.text = "초기화 중...";
    }
    private void Update()
    {
        if (_triggered) return;

        // 초기화 실패 → 시작 자체를 막는다. 빌드에서는 StoveBase가 이미 종료를 부른 뒤다.
        if (StoveBase.HasFailed && !StoveBase.CanStart)
        {
            if (!_failShown)
            {
                _promptText.text = "스토브 초기화 실패";
                _failShown = true;
            }
            if (_promptGroup != null) _promptGroup.alpha = 1f;
            return;
        }

        // 초기화가 끝나기 전에는 입력을 받지 않는다.
        if (!StoveBase.CanStart)
        {
            return;
        }

        if (textChanged == false)
        {
            _promptText.text = "아무 키나 눌러 시작";
            textChanged = true;
            return;
        }

        Blink();
        if (!AnyInput()) return;
        _promptText.text = "로딩 중";

        _triggered = true;
        if (_promptGroup != null) _promptGroup.alpha = 1f;   // 넘어가는 순간엔 또렷하게
        _sceneLoader.Load();
    }

    // 사인 곡선으로 부드럽게 밝아졌다 어두워진다.
    private void Blink()
    {
        if (_promptGroup == null || _triggered) return;

        float phase = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _blinkCycleSeconds);
        _promptGroup.alpha = Mathf.Lerp(_minAlpha, 1f, (phase + 1f) * 0.5f);
    }

    // 키보드 아무 키, 마우스 좌/우클릭, 게임패드 아무 버튼.
    private static bool AnyInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }
}
