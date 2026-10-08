using Game.Core;
using UnityEngine;

namespace Game.UI.Onboarding
{
    /// <summary>로비에 두는 "온보딩으로 가는 포탈"의 개폐 담당. 포탈 오브젝트에 붙인다.
    ///
    /// 싱글 세션에서만 연다. 온보딩 씬 전환은 네트워크 씬 로드라 세션 전원이 함께 끌려가는데,
    /// 온보딩은 혼자 하는 곳이다. 파티원을 끌고 가지 않게 하는 확실한 방법은
    /// "혼자일 때만 들어갈 수 있게" 하는 것이다 — 싱글이면 끌려갈 사람이 없다.
    ///
    /// 멀티(Host/Client)로 전환되면 포탈은 다시 닫힌다.</summary>
    public sealed class OnboardingPortalGate : MonoBehaviour
    {
        [Tooltip("싱글 세션일 때만 켜질 오브젝트. 비우면 이 오브젝트 자신을 켜고 끈다.")]
        [SerializeField] private GameObject _portal;

        private GameObject Target => _portal != null ? _portal : gameObject;

        private void OnEnable()
        {
            LobbyEvents.SessionChanged += OnSessionChanged;
            Apply(CurrentState());
        }

        private void OnDisable() => LobbyEvents.SessionChanged -= OnSessionChanged;

        private void OnSessionChanged(SessionTransition transition) => Apply(transition.State);

        private static LauncherState CurrentState()
            => ServiceLocator.TryGet<ILobbyService>(out var lobby) ? lobby.CurrentState : LauncherState.Idle;

        private void Apply(LauncherState state)
        {
            bool open = state == LauncherState.Single;

            var target = Target;
            if (target.activeSelf != open)
                target.SetActive(open);
        }
    }
}
