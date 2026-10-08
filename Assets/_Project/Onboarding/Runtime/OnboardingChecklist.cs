using System.Collections.Generic;
using Game.Core.Onboarding;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>에셋으로 정의하는 온보딩 과정. <see cref="IOnboardingCourse"/>의 기본 구현이다.
    ///
    /// 파일 이름과 직렬화 필드 이름은 이미 만들어 둔 에셋이 그대로 물고 있으므로 바꾸지 않는다
    /// (바꾸면 컴파일은 되고 값만 조용히 비워진다).</summary>
    [CreateAssetMenu(fileName = "OnboardingChecklist", menuName = "Game/Onboarding/Course")]
    public sealed class OnboardingChecklist : ScriptableObject, IOnboardingCourse
    {
        [Tooltip("이 과정이 무엇을 가르치는가. 기존 에셋은 전부 무기 과정(0)이다.")]
        [SerializeField] private OnboardingCourseKind _kind = OnboardingCourseKind.Weapon;

        [Tooltip("같은 종류 안에서 이 과정을 고르는 열쇠. 무기 과정이면 무기 ID다. " +
                 "0이면 도입부 과정(줍기·인벤토리·장착), 음수면 열쇠를 아직 안 채운 상태라 아무 것에도 붙지 않는다.")]
        [SerializeField] private int _weaponId;

        [SerializeField] private OnboardingItem[] _items;

        public OnboardingCourseKind Kind => _kind;
        public int Key => _weaponId;
        public IReadOnlyList<OnboardingItem> Steps => _items;

#if UNITY_EDITOR
        /// 에디터 생성 도구가 기본 항목을 채워 넣는 통로. 런타임에서는 쓰지 않는다.
        public void SetItems(int weaponId, OnboardingItem[] items)
        {
            _weaponId = weaponId;
            _items = items;
        }
#endif
    }
}
