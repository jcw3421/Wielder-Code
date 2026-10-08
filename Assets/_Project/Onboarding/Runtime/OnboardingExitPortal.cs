using Game.Core.Onboarding;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>온보딩 과정을 하나 끝내면 열리는 나가는 문. 열린 뒤로는 닫지 않는다 —
    /// 나가는 타이밍은 플레이어가 정한다. 다른 무기도 만져보고 싶을 수 있어서 강제로 내보내지 않는다.
    ///
    /// 도입부 과정(Key 0)으로는 열리지 않는다. 도입부는 줍기·장착까지만 가르치므로
    /// 그것만 하고 나가면 정작 배워야 할 것을 못 배운 채로 나가게 된다.
    ///
    /// 포탈 자신이 아니라 별도 오브젝트에 붙인다 — 포탈을 꺼 두면 그 위의 컴포넌트도 같이 멈춰서
    /// 완료 신호를 영영 못 받는다.</summary>
    public sealed class OnboardingExitPortal : MonoBehaviour
    {
        [Tooltip("과정을 끝내면 켜지는 포탈. 처음엔 꺼진다.")]
        [SerializeField] private GameObject _portal;

        private void Awake()
        {
            if (_portal == null)
            {
                Debug.LogError("[OnboardingExitPortal] 포탈이 배선되지 않았습니다.");
                enabled = false;
                return;
            }

            _portal.SetActive(false);
        }

        private void OnEnable() => OnboardingEvents.CourseCompleted += OnCourseCompleted;
        private void OnDisable() => OnboardingEvents.CourseCompleted -= OnCourseCompleted;

        private void OnCourseCompleted(IOnboardingCourse course)
        {
            if (course == null || course.Key <= 0) return;   // 도입부·열쇠 미설정 과정으로는 열지 않는다
            if (_portal == null || _portal.activeSelf) return;

            _portal.SetActive(true);
            Debug.Log("[OnboardingExitPortal] 온보딩 과정 완료 — 로비로 가는 포탈이 열렸습니다.");
        }
    }
}
