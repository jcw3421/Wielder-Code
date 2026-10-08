using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬 하나를 로드하는 얇은 헬퍼. UI 버튼 OnClick에 붙이거나 다른 스크립트에서 호출.
/// 네트워크는 여기서 다루지 않는다 — 다음 씬(로비/게임 씬)의 GameLauncher가 시작한다.
/// 자세한 흐름은 CLAUDE.md 및 Networking 모듈 참고.</summary>
public sealed class SceneLoader : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Build Settings에 등록된 다음 씬 이름.")]
    private string _sceneName;

    [SerializeField]
    [Tooltip("전환 전 대기 시간(초). 페이드/사운드 여유가 필요하면 늘린다.")]
    [Min(0f)]
    private float _delaySeconds = 0f;

    [SerializeField]
    [Tooltip("true면 백그라운드로 비동기 로드(로딩 화면 등에 유용). false면 동기 로드.")]
    private bool _loadAsync = true;

    private bool _loading;

    /// <summary>UI 버튼 OnClick이나 다른 스크립트에서 호출. 중복 호출은 무시된다.</summary>
    public void Load()
    {
        if (_loading) return;
        if (string.IsNullOrEmpty(_sceneName))
        {
            Debug.LogError($"[{nameof(SceneLoader)}] _sceneName이 비어있습니다. 인스펙터에서 지정하세요.");
            return;
        }
        _loading = true;

        if (_delaySeconds > 0f)
            Invoke(nameof(BeginLoad), _delaySeconds);
        else
            BeginLoad();
    }

    private void BeginLoad()
    {
        if (_loadAsync) SceneManager.LoadSceneAsync(_sceneName);
        else SceneManager.LoadScene(_sceneName);
    }
}
