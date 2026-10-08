using System.Collections.Generic;
using DG.Tweening;
using Game.Core.Onboarding;
using UnityEngine;

namespace Game.UI.Onboarding
{
    /// <summary>화면 구석에 떠 있는 온보딩 체크리스트의 표시 담당.
    ///
    /// 판정은 하지 않는다 — <see cref="OnboardingEvents"/>가 알려주는 대로 줄을 만들고 체크할 뿐이다.
    /// 어떤 과정을 왜 띄우는지도 모른다. 그래서 무기·던전·맵 어느 온보딩이 돌든 이 패널은 그대로다.
    ///
    /// UI는 Onboarding 모듈을 참조할 수 없으므로(루트 CLAUDE.md §2) 이 경로 말고는 없다.</summary>
    public sealed class OnboardingChecklistPanel : MonoBehaviour
    {
        [Tooltip("줄이 쌓이는 부모. Vertical Layout Group을 붙여 둔다.")]
        [SerializeField] private RectTransform _itemRoot;

        [Tooltip("줄 하나의 원본. _itemRoot 아래에 이 프리팹이 스텝 수만큼 복제된다.")]
        [SerializeField] private OnboardingItemView _itemPrefab;

        [SerializeField] private CanvasGroup _group;

        [Header("전부 완료된 뒤")]
        [Tooltip("마지막 줄이 체크되고 이 시간이 지나면 목록이 사라진다.")]
        [SerializeField] private float _dismissDelay = 2.5f;

        [SerializeField] private float _dismissDuration = 0.6f;

        private readonly List<OnboardingItemView> _views = new();
        private Tween _dismissTween;

        private void OnEnable()
        {
            OnboardingEvents.CourseStarted += OnCourseStarted;
            OnboardingEvents.StepCompleted += OnStepCompleted;
            OnboardingEvents.CourseCompleted += OnCourseCompleted;
        }

        private void OnDisable()
        {
            OnboardingEvents.CourseStarted -= OnCourseStarted;
            OnboardingEvents.StepCompleted -= OnStepCompleted;
            OnboardingEvents.CourseCompleted -= OnCourseCompleted;
        }

        private void OnCourseStarted(IOnboardingCourse course)
        {
            if (course?.Steps == null) return;
            if (_itemRoot == null || _itemPrefab == null)
            {
                Debug.LogError("[OnboardingChecklistPanel] 줄 원본/부모가 배선되지 않았습니다.");
                return;
            }

            _dismissTween?.Kill();
            _dismissTween = null;

            foreach (var view in _views)
                Destroy(view.gameObject);
            _views.Clear();

            gameObject.SetActive(true);
            if (_group != null) _group.alpha = 1f;

            var steps = course.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                var view = Instantiate(_itemPrefab, _itemRoot);
                view.gameObject.SetActive(true);
                view.Bind(steps[i].Text);
                _views.Add(view);
            }
        }

        private void OnStepCompleted(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _views.Count) return;
            _views[stepIndex].MarkCompleted();
        }

        private void OnCourseCompleted(IOnboardingCourse course)
        {
            if (_group == null) return;

            // CanvasGroup.DOFade는 DOTween UI 모듈(asmdef 밖)이라 코어 DOTween.To로 트윈한다(BossUiPanel과 동일).
            _dismissTween?.Kill();
            _dismissTween = DOTween.To(() => _group.alpha, v => _group.alpha = v, 0f, _dismissDuration)
                                   .SetDelay(_dismissDelay);
        }

        private void OnDestroy() => _dismissTween?.Kill();
    }
}
