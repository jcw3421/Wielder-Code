using System;
using Game.Core.Onboarding;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>"지금부터 이 과정을 가르쳐라"를 러너에게 알리는 쪽. 프레임워크의 확장점이다.
    ///
    /// 무엇을 신호로 삼을지는 구현이 정한다 — 무기 온보딩은 장착을, 던전 온보딩은 구역 진입을,
    /// 맵 온보딩은 위치를 볼 것이다. <see cref="OnboardingRunner"/>는 이 신호만 받으므로
    /// 새 분야를 붙일 때 러너를 건드릴 일이 없다.
    ///
    /// 씬에 여러 개를 놓을 수 있고, 러너가 자식으로 달린 것들을 전부 구독한다.</summary>
    public abstract class OnboardingCourseProvider : MonoBehaviour
    {
        public event Action<IOnboardingCourse> CourseRequested;

        /// 구현이 "이 과정으로 갈아 끼워라"라고 요청할 때 부른다.
        protected void RequestCourse(IOnboardingCourse course)
        {
            if (course != null)
                CourseRequested?.Invoke(course);
        }

        /// 러너가 시작할 때 한 번 부른다. 처음부터 띄울 과정이 있으면 여기서 요청한다.
        public virtual void Begin() { }
    }
}
