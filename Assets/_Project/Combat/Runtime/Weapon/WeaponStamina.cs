using Fusion;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>대시 자원(스테미너)의 소유자. 무기와 같은 GameObject에 붙는다.
    ///
    /// 자원이 무기에 딸린 이유는 최대치·회복 속도·소모량이 무기마다 다르기 때문이고,
    /// 그걸 <see cref="WeaponBase"/> 안에 두면 이미 아홉 가지 일을 하는 클래스가 하나 더 늘어난다.
    /// 여기서는 "얼마 남았나"만 관리하고, 언제 쓰는지는 무기가 정한다.
    ///
    /// 내부 단위는 포인트의 1/1000이다. 초당 회복량을 틱마다 정수로 쪼개 더하기 위한 것으로,
    /// float 누산을 쓰지 않으므로 같은 틱 시퀀스면 항상 같은 값이 나온다(결정론, 루트 CLAUDE.md §3).
    /// 상태 변경은 서버에서만 일어난다(§1).</summary>
    public sealed class WeaponStamina : NetworkBehaviour
    {
        private const int UnitsPerPoint = 1000;

        [SerializeField, Tooltip("스테미너 최대치(포인트).")]
        private int maxStamina = 100;
        [SerializeField, Tooltip("초당 회복량(포인트/초).")]
        private float regenPerSecond = 20f;
        [SerializeField, Tooltip("대시 1회 소모량(포인트). 0이면 제한 없이 대시할 수 있다.")]
        private int dashCost = 30;
        [SerializeField, Tooltip("공격 1회가 적중했을 때 차는 양(포인트). 평타·스킬·궁 공통. " +
                                 "다단히트는 타수로 나눠 가지므로 총량은 타수와 무관하다.")]
        private int gainPerAttack = 5;

        [Networked, OnChangedRender(nameof(OnStaminaChanged))]
        private int Units { get; set; }

        // 마지막으로 UI에 알린 포인트 값. 회복 중엔 매 틱 Units가 바뀌므로 포인트 단위로 줄인다.
        private int _raised = -1;

        // 틱당 회복량. 두 값 모두 Spawned 이후 고정이라 매 틱 다시 계산할 이유가 없다.
        private int _regenUnitsPerTick;

        // 마지막으로 소모가 일어난 틱. 그 틱에는 회복을 건너뛴다.
        private int _lastSpendTick = -1;

        public int Max => maxStamina;
        public int Current => Units / UnitsPerPoint;

        private int MaxUnits => maxStamina * UnitsPerPoint;

        public int GainPerAttack => gainPerAttack;

        /// 대시 1회 소모량. UI 표시용(프로필 패널).
        public int DashCost => dashCost;

        /// 초당 회복량. UI 표시용(프로필 패널).
        public float RegenPerSecond => regenPerSecond;

        /// 대시 한 번 낼 수 있는지. 무기(WeaponBase.CanDash)가 이 값을 그대로 쓴다.
        public bool CanSpendDash => CanSpend(dashCost);

        /// <summary>대시 1회분을 깎는다. 모자라면 아무것도 하지 않고 false.</summary>
        public bool TrySpendDash() => TrySpend(dashCost);

        public bool CanSpend(int cost) => Units >= cost * UnitsPerPoint;

        /// <summary>비용만큼 깎는다. 모자라면 아무것도 하지 않고 false.
        /// 대시 말고 다른 소모처(막기 시작 등)가 이걸 쓴다.</summary>
        public bool TrySpend(int cost)
        {
            if (!HasStateAuthority || !CanSpend(cost)) return false;

            Spend(cost * UnitsPerPoint);
            return true;
        }

        /// <summary>이번 틱에 "초당 N포인트" 속도로 깎는다(막기 홀드처럼 계속 소모되는 동작용).
        /// 남은 양이 모자라면 남은 것을 전부 쓰고 false를 준다 — 호출부가 그걸 보고 동작을 끝낸다.</summary>
        public bool TryDrain(float pointsPerSecond)
        {
            if (!HasStateAuthority) return false;

            int units = Mathf.RoundToInt(pointsPerSecond * UnitsPerPoint * Runner.DeltaTime);
            if (units <= 0) return true;   // 소모 0이면 계속할 수 있다

            if (Units < units)
            {
                Spend(Units);
                return false;
            }

            Spend(units);
            return true;
        }

        /// <summary>가드 성공 보상처럼 즉시 돌려주는 회복. 최대치를 넘지 않는다.</summary>
        public void Restore(int points)
        {
            if (!HasStateAuthority || points <= 0) return;

            Units = Mathf.Min(MaxUnits, Units + points * UnitsPerPoint);
        }

        // 소모한 틱에는 회복하지 않는다 — 안 그러면 "초당 10 소모 + 초당 20 회복"이 오히려 차오른다.
        private void Spend(int units)
        {
            Units -= units;
            _lastSpendTick = Runner.Tick;
        }

        public override void Spawned()
        {
            _regenUnitsPerTick = Mathf.RoundToInt(regenPerSecond * UnitsPerPoint * Runner.DeltaTime);

            if (HasStateAuthority) Units = MaxUnits;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            int max = MaxUnits;
            if (max <= 0 || Units >= max) return;
            if (Runner.Tick <= _lastSpendTick) return;   // 소모한 틱에는 회복하지 않는다

            Units = Mathf.Min(max, Units + _regenUnitsPerTick);
        }

        private void OnStaminaChanged()
        {
            int current = Current;
            if (current == _raised) return;

            _raised = current;
            GameplayEvents.RaisePlayerStaminaChanged(Object.InputAuthority.PlayerId, current, maxStamina);
        }
    }
}
