using UnityEngine;

namespace Game.BossContent
{
    /// 타격 하나를 여러 번으로 쪼개는 설정. 제작소가 TimingKey로 짝을 찾는다.
    ///
    /// 시각을 정하는 방법이 두 가지다.
    ///   · Times를 채우면 — 그 목록이 곧 타격 시각이다(Active 기준 초). 1타 1.0, 2타 2.5,
    ///     3타 2.7처럼 간격이 제각각인 연타를 그대로 적을 수 있다.
    ///   · Times가 비면 — 예전 방식대로 기준 시점에서 IntervalSeconds씩 Count번 등간격으로 찍는다.
    ///
    /// 등간격만 있던 시절에는 "3타를 2.5초와 2.7초에"처럼 실제 애니메이션의 스윙 간격을
    /// 따라갈 수가 없었다. 클립의 타격은 원래 고르지 않으므로 Times 쪽이 기본 선택지다.
    [System.Serializable]
    public struct HitRepeat
    {
        [Tooltip("반복할 타격의 시점 필드 이름(예: SpinAtSeconds).")]
        public string TimingKey;
        [Tooltip("총 타격 횟수. 1 이하면 다단히트가 아니다. Times를 채우면 무시된다.")]
        public int Count;
        [Tooltip("타격 사이 간격(초, 배속 1 기준). Times를 채우면 무시된다.")]
        public float IntervalSeconds;

        [Tooltip("타격 시각 목록(Active 시작 기준 초). 채우면 Count·Interval 대신 이 값이 그대로 쓰인다. " +
                 "오름차순으로 넣는다 — 순서가 뒤집혀도 멈추지는 않지만 그 타가 앞당겨 터진다.")]
        public float[] Times;
    }

    /// 다단히트를 가진 TFW 패턴 정의의 공통 기반.
    ///
    /// 왜 공용 PatternDefinition이 아니라 여기인가: 다단히트를 쓰는 SO도, 그 값을 읽는 코드도
    /// (TfwComboPattern·TfwTelegraphPlan·TFW 제작소) 전부 TFW뿐이다. SkeletonKing·ThunderDragon의
    /// 정의에는 _hitRepeats 항목이 하나도 없다. 공용 파일에 두면 다른 보스 작업자가 쓰지도 않는
    /// 필드를 인스펙터에서 계속 보게 되고, 이 기능을 손볼 때마다 공용 파일 diff가 생긴다.
    ///
    /// SO 에셋은 필드 이름으로 저장되고 구체 클래스(CleaveComboDefinition 등)는 그대로라,
    /// 선언이 상속 사슬의 어디로 옮겨가든 기존 _hitRepeats 값은 그대로 다시 붙는다.
    public abstract class TfwPatternDefinition : PatternDefinition
    {
        [Header("공통 — 다단히트")]
        [Tooltip("한 타격을 여러 번 나눠 때린다. 제작소(패턴 제작소)에서 편집한다.\n" +
                 "총 피해는 보존되고 타격 수만큼 나뉜다 — 다단히트로 바꿔도 패턴의 총 위력은 그대로다.")]
        [SerializeField] private HitRepeat[] _hitRepeats;

        /// 이 타격이 시각을 직접 적어둔 목록. 안 쓰면 null.
        public float[] RepeatTimesFor(string timingKey)
        {
            int index = IndexOfRepeat(timingKey);
            if (index < 0) return null;
            var times = _hitRepeats[index].Times;
            return times != null && times.Length > 0 ? times : null;
        }

        /// 이 타격의 총 타격 횟수. 다단히트 설정이 없으면 1.
        public int RepeatCountFor(string timingKey)
        {
            var times = RepeatTimesFor(timingKey);
            if (times != null) return times.Length;

            int index = IndexOfRepeat(timingKey);
            return index < 0 ? 1 : Mathf.Max(1, _hitRepeats[index].Count);
        }

        /// 타격 사이 간격(초, 배속 1 기준). 설정이 없거나 Times를 쓰면 0.
        public float RepeatIntervalFor(string timingKey)
        {
            if (RepeatTimesFor(timingKey) != null) return 0f;

            int index = IndexOfRepeat(timingKey);
            return index < 0 ? 0f : Mathf.Max(0f, _hitRepeats[index].IntervalSeconds);
        }

        /// hitIndex번째 타격이 터지는 시각(Active 기준 초).
        /// Times를 쓰면 그 값을 그대로, 아니면 기준 시점에서 간격을 곱해 낸다.
        /// 두 방식의 분기를 여기 한 곳에만 두어야 서버·제작소·기즈모가 같은 시각을 본다.
        public float RepeatTimeAt(string timingKey, float baseSeconds, int hitIndex)
        {
            var times = RepeatTimesFor(timingKey);
            if (times == null) return baseSeconds + RepeatIntervalFor(timingKey) * hitIndex;

            return times[Mathf.Clamp(hitIndex, 0, times.Length - 1)];
        }

        /// Active 안에서 실제로 터지는 타격 수.
        ///
        /// 요청한 횟수를 다 못 넣을 수도 있다 — 마지막 타격이 Active 밖으로 밀리면 그 타격은
        /// 영영 실행되지 않는다. 피해를 "요청 횟수"로 나누면 그만큼이 조용히 증발하므로,
        /// 실제로 터지는 횟수로 나눠 총 피해를 보존한다.
        public int EffectiveHitCount(string timingKey, float baseSeconds)
        {
            var times = RepeatTimesFor(timingKey);
            if (times == null)
                return EffectiveHitCount(ActiveSeconds, baseSeconds,
                                         RepeatCountFor(timingKey), RepeatIntervalFor(timingKey));

            // 적어둔 시각 중 Active 안에 드는 것만 실제로 터진다. 앞에서부터 세는 이유는
            // TryHit이 순서대로 쏘기 때문 — 중간에 창 밖 값이 끼면 그 뒤는 못 나간다.
            int fits = 0;
            while (fits < times.Length && times[fits] <= ActiveSeconds) fits++;
            return fits;
        }

        /// 위와 같은 계산의 순수 함수 형태. 제작소는 아직 저장 전인 값(SerializedProperty)으로
        /// 미리보기를 그려야 해서 필드가 아니라 인자로 받는다 — 계산 자체는 하나만 둔다.
        public static int EffectiveHitCount(float activeSeconds, float baseSeconds,
                                            int requestedCount, float intervalSeconds)
        {
            if (baseSeconds > activeSeconds) return 0;
            if (requestedCount <= 1 || intervalSeconds <= 0f) return requestedCount;

            int fits = Mathf.FloorToInt(Mathf.Max(0f, activeSeconds - baseSeconds) / intervalSeconds) + 1;
            return Mathf.Clamp(fits, 1, requestedCount);
        }

        /// 총 피해를 타격 수로 나눈다. 나머지는 가운데 타격부터 바깥으로 한 대씩 얹는다 —
        /// 앞뒤로 몰면 첫 타나 막타만 유독 아파서 다단히트가 고르게 느껴지지 않는다.
        ///
        /// 제작소가 화면에 표시하는 값과 서버가 실제로 주는 값이 반드시 같아야 하므로
        /// 양쪽이 이 함수 하나를 공유한다.
        public static int DistributeDamage(int totalDamage, int hitCount, int hitIndex)
        {
            if (totalDamage <= 0 || hitCount <= 0 || hitIndex < 0 || hitIndex >= hitCount) return 0;

            int damage = totalDamage / hitCount;
            int remainder = totalDamage % hitCount;
            int center = hitCount / 2;

            for (int rank = 0; rank < remainder; rank++)
            {
                int index;
                if ((hitCount & 1) == 1)
                    index = rank == 0 ? center : ((rank & 1) == 1 ? center - (rank + 1) / 2 : center + rank / 2);
                else
                    index = (rank & 1) == 0 ? center - 1 - rank / 2 : center + rank / 2;

                if (index == hitIndex) return damage + 1;
            }
            return damage;
        }

        private int IndexOfRepeat(string timingKey)
        {
            if (_hitRepeats == null || string.IsNullOrEmpty(timingKey)) return -1;
            for (int i = 0; i < _hitRepeats.Length; i++)
                if (_hitRepeats[i].TimingKey == timingKey) return i;
            return -1;
        }
    }
}
