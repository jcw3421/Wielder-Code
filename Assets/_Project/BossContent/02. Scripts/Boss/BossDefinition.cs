using Game.Core;
using UnityEngine;

namespace Game.BossContent
{
    /// 보스 한 종의 모든 튜닝 값. 디자이너가 인스펙터에서 만지는 유일한 입구.
    /// 런타임에는 Spawned()에서 BossStats로 한 번 복사되고, 그 뒤로는 읽히지 않는다.
    [CreateAssetMenu(fileName = "BossDefinition", menuName = "BossContent/Boss/BossDefinition")]
    public class BossDefinition : ScriptableObject
    {
        [Header("베이스 데이터")]
        [SerializeField] BossRecord data;
        public int MaxHealth => data.BaseHealth; 
        public int BossID => data.Id;
        public string BossName => data.Name;
        //보스의 공격력- 패턴 공격력이 배율

        [Header("페이즈")]
        public int[] PhaseThresholdPercents = { 50 };            // 이 HP%를 지날 때마다 페이즈 +1
        public float[] PhasePatternSpeedMultipliers = { 1f, 1.5f }; // 인덱스 = 페이즈-1. 넘치면 마지막 값 유지

        [Header("페이즈전환 타이밍 (BossTimingBaker가 _PT 클립에서 자동 기입)")]
        [Tooltip("체크 시 베이커가 PhaseTransitionActiveSeconds를 건드리지 않는다 — Active 클립을 Loop로 두고 지속시간을 직접 넣을 때(웨이브 여러 번 등). Telegraph/Recovery는 계속 자동 기입.")]
        public bool ManualPhaseTransitionActive;
        public float PhaseTransitionTelegraphSeconds;   // 진입 → Active(장판 발동)
        public float PhaseTransitionActiveSeconds;
        public float PhaseTransitionRecoverySeconds;

        [Header("무력화 — 다운에서 일어나는 데 걸리는 시간 (BossTimingBaker가 Staggered_GetUp에서 자동 기입)")]
        [Tooltip("무력화 시간의 마지막 이만큼을 기상 동작에 쓴다 — 다 일어나는 순간과 추격 복귀가 맞는다.\n" +
                 "무력화 전체 길이는 StaggerBossOutcome이 정하고, 이 값은 그 안에서 잘라 쓴다(더하지 않는다).\n" +
                 "0이면 기상 동작 없이 다운 자세에서 바로 추격으로 튄다.")]
        [Min(0f)] public float StaggerGetUpSeconds = 1f;

        [Header("이동 · 감지")]
        public float MoveSpeedMultiplier = 0.25f;
        public float RotationSpeed = 5f;
        public float DetectRange = 10f;
        public float StopDistance = 2f;

        [Header("공격")]
        [Tooltip("패턴과 패턴 사이의 최소 간격(초). 전투 내내 적용된다.")]
        public float PatternIntervalSeconds = 0.5f;

        [Tooltip("플레이어를 처음 발견한 뒤 첫 패턴까지의 추가 대기(초). 그 동안 추격은 한다. " +
                 "0이면 예전 그대로 — 위 간격만큼만 기다리고 바로 때린다. " +
                 "보스룸에 들어서자마자 맞는 걸 막고 싶을 때 쓴다. 첫 조우에만 적용되고 " +
                 "이후 패턴 템포에는 영향이 없다.")]
        public float FirstEncounterDelaySeconds = 0f;
        public PatternDefinition[] Patterns;

        [Header("연대 기믹")]
        [Tooltip("이 보스가 쓰는 기믹들. 각 기믹이 자기 발동 HP%를 갖고 있어 여기 순서는 상관없다.\n" +
                 "쓰려면 보스 프리팹에 BossGimmickNetState 컴포넌트가 붙어 있어야 한다.")]
        public BossGimmickDefinition[] Gimmicks;

        [Header("사망")]
        public float DespawnDelaySeconds = 10f;

        [Header("페이즈전환 장판")]
        public AoeBurstConfig PhaseTransitionBurst = new();

        private void OnValidate()
        {
            if (Patterns == null || Patterns.Length == 0)
                Debug.LogWarning($"{name}: Patterns가 비어 있음 — 패턴 상태 진입 시 예외 발생", this);
            else if (!System.Array.Exists(Patterns, p => p != null && p.UnlockPhase <= 1))
                Debug.LogWarning($"{name}: 1페이즈에서 쓸 수 있는 패턴(UnlockPhase≤1)이 없음", this);

            if (PhasePatternSpeedMultipliers == null || PhasePatternSpeedMultipliers.Length == 0)
                Debug.LogWarning($"{name}: PhasePatternSpeedMultipliers가 비어 있음 — 인덱스 -1 예외", this);

            for (int i = 1; i < PhaseThresholdPercents.Length; i++)
                if (PhaseThresholdPercents[i] >= PhaseThresholdPercents[i - 1])
                    Debug.LogWarning($"{name}: PhaseThresholdPercents는 내림차순이어야 함 (예: 75, 50, 25)", this);
            bool hasNoCooldownPattern = false;
            foreach (var p in Patterns)
            {
                if (p == null) continue;
                if (p.TelegraphSeconds < 0.8f)
                    Debug.LogWarning($"{name}: {p.name} 텔레그래프 {p.TelegraphSeconds}s — 지연 여유상 0.8s 이상 권장", this);
                if (p.AttackRange < StopDistance)
                    Debug.LogWarning($"{name}: {p.name} AttackRange({p.AttackRange:F1}) < StopDistance({StopDistance:F1}) — 사거리 안에 도달 불가", this);
                if (p.Weight <= 0)
                    Debug.LogWarning($"{name}: {p.name} Weight가 {p.Weight} — 1 이상이어야 선택됨", this);
                if (p.CooldownSeconds <= 0f)
                    hasNoCooldownPattern = true;
            }
            if (!hasNoCooldownPattern && Patterns.Length > 0)
                Debug.LogWarning($"{name}: 모든 패턴에 쿨타임이 있음 — 전원 쿨다운 시 보스가 공격 불가 상태에 빠질 수 있음", this);

            if (Gimmicks == null) return;
            for (int i = 0; i < Gimmicks.Length; i++)
            {
                if (Gimmicks[i] == null)
                {
                    Debug.LogWarning($"{name}: Gimmicks[{i}]가 비어 있음 — 그 기믹은 열리지 않는다", this);
                    continue;
                }
                for (int j = i + 1; j < Gimmicks.Length; j++)
                    if (Gimmicks[j] != null && Gimmicks[j].GimmickId == Gimmicks[i].GimmickId)
                        Debug.LogWarning($"{name}: Gimmicks[{i}]와 [{j}]의 GimmickId가 {Gimmicks[i].GimmickId}로 같음 — UI가 구분하지 못한다", this);
            }
        }
    }
}
