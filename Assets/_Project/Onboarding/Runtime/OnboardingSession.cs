using System;
using Game.Core;
using Game.Core.Onboarding;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>온보딩 구간의 수명과 규칙을 한 곳에서 쥔다.
    ///
    /// 씬에 놓인 이 오브젝트의 수명을 그대로 구간으로 쓴다 — 씬이 열리면 열리고, 씬이 내려가면
    /// (포탈로 나가든 다른 경로로 나가든) 파괴되면서 닫힌다. 나가는 경로마다 따로 신호를 보내는 것보다
    /// 빠뜨릴 구석이 없다.
    ///
    /// 규칙은 <see cref="IOnboardingRules"/>로 등록만 해 둔다. Combat은 이걸 물어보기만 하므로
    /// 온보딩을 참조하지도, 온보딩 전용 분기를 들고 있지도 않는다.</summary>
    public sealed class OnboardingSession : MonoBehaviour, IOnboardingRules
    {
        [Tooltip("연습 중에는 죽지 않는다. 사망 처리가 끼면 부활 흐름·집결 처리까지 딸려온다.")]
        [SerializeField] private bool _playersCannotDie = true;

        [Tooltip("연습 중에 얻은 것은 서버에 저장하지 않는다.")]
        [SerializeField] private bool _suspendPersistence = true;

        /// 구간이 열리고(true) 닫혔다(false). 같은 모듈 안의 부수 처리(인벤토리 격리 등)가 구독한다.
        /// 닫힘 신호는 규칙을 내리기 <b>전에</b> 나가므로, 되돌리는 쪽이 아직 규칙 아래에서 동작한다.
        public static event Action<bool> ActiveChanged;

        public bool PlayersCannotDie => _playersCannotDie;
        public bool PersistenceSuspended => _suspendPersistence;

        private bool _open;

        private void OnEnable()
        {
            if (_open) return;
            _open = true;

            ServiceLocator.Register<IOnboardingRules>(this);
            ActiveChanged?.Invoke(true);
        }

        private void OnDestroy()
        {
            if (!_open) return;
            _open = false;

            ActiveChanged?.Invoke(false);
            ServiceLocator.Unregister<IOnboardingRules>();
        }
    }
}
