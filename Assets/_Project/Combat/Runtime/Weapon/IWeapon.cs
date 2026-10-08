using System;
using Game.Combat.Chain;
using Game.Core;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>전투 판정/입력 라이프사이클. 상태(State)는 이 인터페이스만 보고 진행 여부를 판단하며,
    /// 콤보·차징·막기 등 세부 타이밍은 전혀 모른다 — 전부 무기(WeaponBase 이하)가 캡슐화한다.
    /// Attack/Skill/Ultimate 세 액션은 완전히 대칭(예외 없음).</summary>
    public interface IWeaponActions
    {
        int ID { get; }
        bool CanAttack { get; }
        bool CanSkill { get; }
        bool CanUltimate { get; }

        void OnAttackPressed();
        void OnAttackReleased();
        void OnAttackTick();       // held 상태인 틱마다 호출
        bool IsAttackFinished();   // 상태가 매 틱 묻는 "나가도 되나?" 질의

        void OnSkillPressed();
        void OnSkillReleased();
        void OnSkillTick();
        bool IsSkillFinished();

        void OnUltimatePressed();
        void OnUltimateReleased();
        void OnUltimateTick();
        bool IsUltimateFinished();

        /// 대시는 전 무기 공통 동작이라 WeaponBase가 구현을 갖는다(무기별 수치만 다르다).
        /// 홀드가 없어 Released/Tick이 없는 것만 나머지 세 액션과 다르다 —
        /// 대시는 눌린 순간에 전부 결정되고, 끝나는 시점은 IsDashFinished가 답한다.
        bool CanDash { get; }

        /// <param name="backward">이동 입력 없이 눌렀는가 — 그때는 제자리에서 뒤로 빠지는 백대시가 나간다.</param>
        void OnDashPressed(bool backward);
        bool IsDashFinished();

        /// <param name="anchor">풀체인 이펙트·판정이 놓일 월드 지점. 팀 합동기라 시전자에 붙지 않는다.</param>
        void FullChainAction(Vector3 anchor);

        /// <summary>CC로 행동이 끊길 때 호출. 진행 중인 콤보·차징·홀드를 버리고 즉시 대기 상태로 되돌린다.</summary>
        void Cancel();

        /// <summary>지금 이 무기가 부여하는 슈퍼아머 등급. 이 등급 <b>이하</b>의 CC는 무시된다
        /// (<see cref="CCType.None"/>=면역 없음, <see cref="CCType.Flinch"/>=경직만 면역).
        /// 궁극기 중 전 CC 면역은 게임 전역 규칙이라 무기가 아니라 상태로 판정한다.</summary>
        CCType CurrentImmunityUpTo { get; }

        /// <summary>지금 이 무기가 받는 피해에 적용할 비율(%). 100 = 그대로, 50 = 절반, 0 = 완전 무효.
        /// 막기·패링처럼 "받는 쪽"을 바꾸는 동작이 여기로 보고한다. 정수라서 틱 결정론이 유지된다.</summary>
        int IncomingDamagePercent { get; }

        /// <summary>감면이 실제로 걸려 피해를 막아낸 순간(서버). 막기 성공에 보상을 주는 무기가 쓴다.</summary>
        /// <param name="justGuard">완전 무효(저스트가드)였는가. false면 일반 막기.</param>
        void OnGuardSucceeded(bool justGuard);

        /// <summary>스킬을 유지한 채로 평타 입력이 들어온 순간(홀드형 스킬 전용).
        /// 상태는 "평타가 눌렸다"만 알려주고, 그걸로 무엇을 할지는 무기가 정한다
        /// — 방패는 막기 성공 뒤 열린 반격 창에서만 반격 찌르기를 낸다.</summary>
        void OnAttackDuringSkill();

        /// <summary>지금 평타를 끊고 스킬로 넘어가도 되는가. "언제 끊어도 되는지"의 판단은 무기 몫이다 —
        /// 방패만 열어두고, 그 안에서 후딜 구간인지까지 스스로 본다.
        /// 대시는 이 값과 무관하게 항상 캔슬할 수 있다(회피는 늦으면 의미가 없다).</summary>
        bool CanCancelIntoSkill { get; }

        void Initialize(ChainController chainController);
    }

    /// <summary>애니메이션 배선 전용. 무기가 소유하며 상태 코드는 이 데이터만 읽는다 →
    /// 새 무기를 추가해도 상태 코드는 바뀌지 않는다(OCP).</summary>
    public interface IWeaponAnimation
    {
        // 이 무기(직업)의 클립을 담은 Override Controller. 무기 교체 시 Animator에 꽂는다.
        RuntimeAnimatorController AnimatorController { get; }

        event Action<int, float> OnAnimPlayRequested;

        /// 전진기 요청 (거리 m, 시작 지연 s, 지속 s, 진행 곡선). 이동 수행은 상태머신 책임 — 무기는 값만 알린다.
        event Action<float, float, float, AnimationCurve> OnLungeRequested;

        /// 애니메이션 배속 요청. 공격 발동 시점에 무기가 알린다.
        event Action<float> OnAnimSpeedRequested;

        /// 공격이 실제로 발동한 순간. 콤보가 한 상태 안에서 이어져도 매 타격마다 발생한다.
        event Action OnAttackBegan;
    }

    public interface IWeapon : IWeaponActions, IWeaponAnimation
    {
        void ResetGauge();
        ReinforcementSetup ReinforcementSetup { get; }
        Reinforcement ReinforceCur { get; }
        Reinforcement ReinforceNext { get; }
        int CurrentReinforcement { get; set; }
    }
}
