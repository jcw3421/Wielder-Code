using Game.Core;
using Game.Networking;
using UnityEngine;

namespace Game.UI.Onboarding
{
    /// <summary>로비에 두는 컴포넌트. 이 기기에서 온보딩을 아직 안 봤으면 온보딩 씬으로 보낸다.
    ///
    /// 온보딩이 로비 뒤에 오는 이유: 세션을 여는 _NetworkSetups와 데이터시트가 각각
    /// 로비·부트스트랩에서 DontDestroyOnLoad로 올라온다. 온보딩을 그 앞에 두면 둘 다 없는
    /// 상태로 시작하게 된다. 그래서 "로비까지 정상적으로 부팅한 뒤 씬만 온보딩으로 옮기는" 모양이다.
    ///
    /// 씬 전환을 <see cref="NetworkSceneLoader"/>로 하는 이유: 아바타가 이미 스폰돼 있고
    /// 세션이 돌고 있으므로, 일반 SceneManager로 옮기면 네트워크 씬과 로컬 씬이 어긋난다.
    ///
    /// 싱글 세션에서만 보낸다. 온보딩 씬 전환은 네트워크 씬 로드라 세션 전원이 함께 끌려가는데,
    /// 온보딩은 혼자 도는 곳이다(러너·체인 프라이머 모두 1인 전제). 파티원을 끌고 가지 않게 하는
    /// 확실한 방법은 "혼자일 때만 들어갈 수 있게" 하는 것이다 — 싱글이면 끌려갈 사람이 없다.</summary>
    public sealed class OnboardingEntry : MonoBehaviour
    {
        private const string SeenKey = "Onboarding.Seen";

        [SerializeField] private string _onboardingSceneName = "Onboarding";

        [Tooltip("체크하면 이미 본 기기에서도 매번 온보딩으로 보낸다. 온보딩 자체를 손볼 때 쓴다.")]
        [SerializeField] private bool _alwaysShow;

        /// 포탈을 타고 로비로 돌아온 뒤 다시 온보딩으로 튕기지 않도록, 한 번 보냈으면 기록한다.
        public static bool Seen
        {
            get => PlayerPrefs.GetInt(SeenKey, 0) != 0;
            set { PlayerPrefs.SetInt(SeenKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void OnEnable()
        {
            LobbyEvents.SceneLoadDone += OnSceneLoadDone;
            LobbyEvents.SessionChanged += OnSessionChanged;
        }

        private void OnDisable()
        {
            LobbyEvents.SceneLoadDone -= OnSceneLoadDone;
            LobbyEvents.SessionChanged -= OnSessionChanged;
        }

        private void Start() => TrySend();

        // 로비 진입이 씬 로드 완료 뒤에 확정되는 경로도 있어서 양쪽에서 시도한다. 중복은 Seen이 막는다.
        private void OnSceneLoadDone() => TrySend();

        // 씬 로드 완료가 세션 상태 확정보다 먼저 오는 경로가 있다(StartGame 안에서 씬이 먼저 로드되고
        // 상태는 await 뒤에 Single로 바뀐다). 그 순서로는 아래 싱글 가드에 걸려 돌아가므로,
        // 상태가 확정되는 시점에도 한 번 더 시도한다.
        private void OnSessionChanged(SessionTransition transition) => TrySend();

        private void TrySend()
        {
            if (!_alwaysShow && Seen) return;
            if (CurrentState() != LauncherState.Single) return;   // 멀티에서는 보내지 않는다(Seen도 남기지 않는다)

            var loader = NetworkSceneLoader.Current;
            if (loader == null) return;   // 아직 세션이 안 올라왔다. 다음 신호에 다시 시도한다.

            Seen = true;
            loader.LoadGameScene(_onboardingSceneName);
        }

        private static LauncherState CurrentState()
            => ServiceLocator.TryGet<ILobbyService>(out var lobby) ? lobby.CurrentState : LauncherState.Idle;
    }
}
