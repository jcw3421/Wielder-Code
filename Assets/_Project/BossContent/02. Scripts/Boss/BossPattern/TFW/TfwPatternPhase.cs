namespace Game.BossContent
{
    /// 연출 컴포넌트들이 "지금 때리는 중인가"를 판단하는 단일 창구.
    ///
    /// 값 1(Active)만 보면 안 되는 이유가 있다. BossPatternBase.Cancel()은 자기 내부 단계만
    /// Done으로 바꾸고 BossNetState.CurrentPatternStep은 그대로 둔다(공용 파일이라 여기서 못 고친다).
    /// 그래서 보스가 Active 도중에 죽거나 무력화되면 네트워크 값이 Active에 멈춘 채로 남고,
    /// 그 값만 보는 연출은 영영 꺼지지 않는다 — 시체가 디스폰까지 칼을 번쩍이던 증상이 이것이다.
    ///
    /// 보스가 실제로 패턴을 치는 중인지는 FSM이 매 전이마다 갱신하는 CurrentAnimStateId가 알고 있다.
    /// 두 값을 함께 보면 인터럽트가 그대로 잡힌다.
    public static class TfwPatternPhase
    {
        /// BossPatternBase의 private enum PatternStep.Active와 맺은 계약.
        /// (BossPhaseTransitionState의 Step.Active도 같은 값을 쓴다.)
        public const int ActiveStep = 1;

        /// 지금 타격 구간인가. 패턴과 페이즈 전환(거합) 둘 다 포함한다 —
        /// 거합도 칼을 휘두르는 동작이라 궤적·검기가 붙어야 한다.
        /// 사망·무력화·기믹으로 끊긴 경우는 여기서 false가 된다.
        public static bool IsAttackActive(IBossAnimSource net)
        {
            if (net == null || net.CurrentPatternStep != ActiveStep) return false;

            var state = (BossAnimState)net.CurrentAnimStateId;
            return state == BossAnimState.Pattern || state == BossAnimState.PhaseTransition;
        }
    }
}
