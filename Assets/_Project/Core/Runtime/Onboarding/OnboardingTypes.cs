using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Onboarding
{
    // ⚠ 아래 enum의 값은 온보딩 과정 에셋에 정수로 저장된다.
    //   - 값을 명시적으로 못 박는다. 중간 삽입·순서 변경은 이미 저장된 에셋이
    //     다른 항목을 가리키게 만들고, 컴파일도 되고 로그도 안 남아서 찾기 어렵다.
    //   - 항목을 없앨 때도 값은 재사용하지 않는다(번호를 비워 둔다).

    /// 스텝이 완료로 바뀌는 조건. 전부 <see cref="GameplayEvents"/>로만 판정한다 —
    /// 어느 모듈에서 온 행동이든 Core 이벤트로만 들어온다(루트 CLAUDE.md §2).
    public enum OnboardingTrigger
    {
        None = 0,
        WeaponEquipped = 1,      // Param 미사용. 아무 무기나 장착하면 완료.
        EnemyKilled = 2,         // Param = 처치 수. 그만큼 잡으면 완료.
        UltimateGaugeFull = 3,   // Param 미사용. 궁극기 게이지가 가득 차면 완료.
        ChainStageReached = 4,   // Param = 체인 단계. 그 단계에 도달하면 완료.
        FullChain = 5,           // Param 미사용. 풀체인이 발동하면 완료.
        ItemPickedUp = 6,        // Param = 아이템 ID. 0이면 아무거나. 그 아이템을 주우면 완료.
        InventoryOpened = 7,     // Param 미사용. 인벤토리 창을 열면 완료.
        WeaponActionUsed = 8,    // Param = DamageType(0 평타 / 1 스킬 / 2 궁극기). 그 동작을 쓰면 완료.
        GuardSucceeded = 9,      // Param = 0이면 아무 막기, 1이면 저스트가드만 인정.
        CounterAttacked = 10,    // Param 미사용. 막기 후 반격을 넣으면 완료.
    }

    /// 온보딩 한 줄.
    [Serializable]
    public struct OnboardingItem
    {
        [Tooltip("화면에 그대로 표시되는 문장. 플레이어에게 시킬 행동 하나를 쓴다.")]
        [TextArea(1, 2)]
        public string Text;

        public OnboardingTrigger Trigger;

        [Tooltip("Trigger에 따라 의미가 달라진다(처치 수 / 체인 단계 / 미사용).")]
        public int Param;
    }

    /// 이 과정이 무엇을 가르치는가. 값은 에셋에 정수로 저장되므로 재배치하지 않는다.
    public enum OnboardingCourseKind
    {
        Weapon = 0,
        Dungeon = 1,
        Map = 2,
    }

    /// <summary>온보딩 한 과정. 한 화면에 함께 떠 있는 스텝 묶음이고, 순서는 표시 순서일 뿐
    /// 어느 것을 먼저 해도 되고 진행을 막지도 않는다.
    ///
    /// 인터페이스로 두는 이유: 과정을 어디서 어떻게 정의하든(ScriptableObject·시트·코드)
    /// 진행 담당과 표시 담당은 같은 계약만 보면 된다. 던전·맵 온보딩은 이걸 구현해서 붙인다.</summary>
    public interface IOnboardingCourse
    {
        OnboardingCourseKind Kind { get; }

        /// 같은 Kind 안에서 이 과정을 고르는 열쇠(무기 온보딩이면 무기 ID).
        /// 0이면 "종류 전체에 공통인 도입부"라는 뜻이다.
        int Key { get; }

        IReadOnlyList<OnboardingItem> Steps { get; }
    }
}
