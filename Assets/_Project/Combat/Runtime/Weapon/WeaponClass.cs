// Game.Combat — WeaponClass.cs
// 무기의 최상위 구분인 직업(Class). 무기에 귀속되며, 체인 스타터의 직업이 풀체인 종류를 결정한다.
// MVP: Dealer만 실제로 사용. Tanker/Healer는 다음 단계 대비 정의만.

namespace Game.Combat
{
    public enum WeaponClass : byte
    {
        Dealer  = 0,
        Tanker  = 1,
        Healer  = 2,
    }
}
