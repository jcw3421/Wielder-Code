namespace Game.Core.Onboarding
{
    /// <summary>온보딩 같은 "연습 구간"이 열려 있는 동안에만 달라지는 게임플레이 규칙.
    ///
    /// 구간을 여는 쪽(Onboarding 모듈)이 구현해 <see cref="ServiceLocator"/>에 등록하고,
    /// 규칙에 영향을 받는 쪽(Combat)은 <see cref="OnboardingRules"/>로 <b>물어보기만</b> 한다.
    /// 이 방향이라 Combat은 온보딩을 전혀 모른 채로 남는다 — 구독도, 분기 플래그도 없다.</summary>
    public interface IOnboardingRules
    {
        /// 연습 중에는 죽지 않는다. 사망 처리가 끼면 부활 흐름·집결 처리까지 딸려온다.
        bool PlayersCannotDie { get; }

        /// 연습 중에 얻은 것은 서버에 저장하지 않는다.
        bool PersistenceSuspended { get; }
    }

    /// <summary>규칙 질의 창구. 등록된 규칙이 없으면 전부 "평소대로"를 답하므로,
    /// 호출부는 온보딩이 있든 없든 같은 코드로 둘 수 있다.</summary>
    public static class OnboardingRules
    {
        public static bool PlayersCannotDie
            => ServiceLocator.TryGet<IOnboardingRules>(out var rules) && rules.PlayersCannotDie;

        public static bool PersistenceSuspended
            => ServiceLocator.TryGet<IOnboardingRules>(out var rules) && rules.PersistenceSuspended;
    }
}
