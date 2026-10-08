using System.Collections.Generic;
using Game.Core;
using Game.Core.Onboarding;
using UnityEngine;

namespace Game.Onboarding
{
    /// <summary>무기 온보딩의 과정 공급자. 흐름은 두 덩이다:
    ///   1) 도입부 — 무기를 줍고, 인벤토리를 열고, 장착한다. 무기와 무관하므로 과정 하나로 끝난다.
    ///   2) 무기별 — 장착한 무기의 과정으로 갈아 끼운다. 무기를 바꿔 끼면 그 무기 과정으로 다시 바뀐다.
    ///
    /// 던전·맵 온보딩은 이 클래스를 흉내 내어 각자의 신호(구역 진입·위치 등)로 과정을 요청하면 된다.
    /// 러너는 어느 쪽이 요청했는지 구분하지 않는다.</summary>
    public sealed class WeaponOnboardingProvider : OnboardingCourseProvider
    {
        [Tooltip("무기를 잡기 전에 띄우는 과정. 줍기·인벤토리·장착.")]
        [SerializeField] private OnboardingChecklist _introCourse;

        [Tooltip("무기별 과정. 각 에셋의 Weapon Id로 장착한 무기와 짝지어진다.")]
        [SerializeField] private List<OnboardingChecklist> _weaponCourses = new();

        private int _currentWeaponId = -1;

        private void OnEnable() => GameplayEvents.WeaponEquipped += OnWeaponEquipped;
        private void OnDisable() => GameplayEvents.WeaponEquipped -= OnWeaponEquipped;

        public override void Begin()
        {
            if (_introCourse == null)
            {
                Debug.LogError("[WeaponOnboardingProvider] 도입부 과정이 배선되지 않았습니다.");
                return;
            }

            RequestCourse(_introCourse);
        }

        private void OnWeaponEquipped(int weaponId, int sourcePlayerId)
        {
            if (weaponId <= 0) return;                  // 0 = 맨손. 장착 해제로는 과정을 바꾸지 않는다
            if (weaponId == _currentWeaponId) return;   // 같은 무기 재장착은 진행도를 날리지 않는다

            var course = FindCourse(weaponId);
            if (course == null)
            {
                Debug.LogWarning($"[WeaponOnboardingProvider] 무기 {weaponId}의 온보딩 과정이 없습니다. 현재 과정을 유지합니다.");
                return;
            }

            _currentWeaponId = weaponId;
            RequestCourse(course);
        }

        private IOnboardingCourse FindCourse(int weaponId)
        {
            foreach (var course in _weaponCourses)
                if (course != null && course.Key > 0 && course.Key == weaponId)
                    return course;

            return null;
        }
    }
}
