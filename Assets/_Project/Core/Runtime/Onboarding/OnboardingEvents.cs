using System;

namespace Game.Core.Onboarding
{
    /// <summary>온보딩 진행 상황 알림 채널.
    ///
    /// 표시 담당(UI)은 이 채널만 구독한다 — UI는 Onboarding 모듈을 참조할 수 없다(루트 CLAUDE.md §2).
    /// 발행은 진행 담당(Onboarding 모듈의 러너)이 단독으로 한다.</summary>
    public static class OnboardingEvents
    {
        /// 표시할 과정이 바뀌었다. 이전 과정의 줄과 진행도는 버린다.
        public static event Action<IOnboardingCourse> CourseStarted;
        public static void RaiseCourseStarted(IOnboardingCourse course) => CourseStarted?.Invoke(course);

        /// 인자는 현재 과정의 스텝 인덱스.
        public static event Action<int> StepCompleted;
        public static void RaiseStepCompleted(int stepIndex) => StepCompleted?.Invoke(stepIndex);

        /// 현재 과정의 모든 스텝이 완료됐다.
        public static event Action<IOnboardingCourse> CourseCompleted;
        public static void RaiseCourseCompleted(IOnboardingCourse course) => CourseCompleted?.Invoke(course);
    }
}
