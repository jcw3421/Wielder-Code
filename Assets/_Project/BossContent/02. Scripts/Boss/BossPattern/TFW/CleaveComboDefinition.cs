using UnityEngine;

namespace Game.BossContent
{
    /// 일도양단 연계(1페이즈 대검). 360도 회전 → 기모으기 → 칼등치기(스턴) → 일도양단.
    /// 타격 시점은 Active 클립 하나의 타임라인 위 초 단위 오프셋이다(클립을 보고 맞춘다).
    [CreateAssetMenu(fileName = "CleaveComboDef", menuName = "BossContent/Boss/TFW/CleaveCombo")]
    public class CleaveComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {

        [Header("연계 타이밍 — Active 시작 기준 초")]
        [Tooltip("Active는 통짜 클립이라 베이커가 길이를 넣어준다. 아래 값들이 그 안에 들어가야 한다.")]
        public float SpinAtSeconds = 0.4f;
        public float BackhandAtSeconds = 2.2f;
        public float CleaveAtSeconds = 3.4f;

        [Header("1타 — 360도 회전베기 (전방위)")]
        public float SpinRadius = 5f;
        public int SpinDamage = 12;
        public float SpinHeight = 1f;

        [Header("2타 — 칼등치기 (이 타격만 스턴)")]
        public float BackhandReach = 3f;
        public float BackhandRadius = 2.5f;
        public int BackhandDamage = 8;
        public float BackhandHeight = 1f;

        [Header("3타 — 일도양단 (전방 직선, 데미지는 공통 Damage 필드)")]
        public float CleaveReach = 4f;      // 박스 중심까지 전방 거리
        public float CleaveLength = 8f;
        public float CleaveWidth = 3f;
        public float CleaveHeight = 3f;

        [Header("전진 이동")]
        [Tooltip("클립 루트모션을 따라 앞뒤로 움직인다. 끄면 제자리에서만 패턴을 친다 — 떨림이 나면 먼저 꺼보고 원인을 가른다.")]
        public bool UseForwardMotion = true;

        [Header("조각 — 베이커가 채운다")]
        [Tooltip("각 Active 조각이 시작하는 시각(초). 조각이 바뀌는 시점을 코드가 이 값으로 안다.")]
        public float[] SegmentStartSeconds;
        [Tooltip("이 번호(0부터)의 조각에서 보스를 180도 돌린다. 후방 타격 모션 없이 앞으로 쓰는 클립을 재활용할 때 쓴다.")]
        public int[] TurnAroundSegments;
        [Tooltip("x = Active 시작 후 초, y = 누적 전진 거리(m). 곡선이 평평하면 제자리, 가파르면 확 튀어나간다. 손으로 다듬어도 된다.")]
        public AnimationCurve ForwardDistanceCurve = new AnimationCurve();

        [Header("연출 — 파티클 프리팹을 꽂는다")]
        [SerializeField] private PatternEffectCue[] _effectCues;
        public PatternEffectCue[] EffectCues => _effectCues;

        bool ITfwComboDefinition.UseForwardMotion => UseForwardMotion;
        float[] ITfwComboDefinition.SegmentStartSeconds => SegmentStartSeconds;
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { SpinAtSeconds, TfwComboTiming.SegmentStart(SegmentStartSeconds, 1), BackhandAtSeconds, CleaveAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        // 가장 멀리 닿는 타격이 사거리를 정한다 — 이보다 멀면 패턴이 후보에 안 오른다.
        protected override float HitRange => Mathf.Max(
            SpinRadius,
            BackhandReach + BackhandRadius,
            CleaveReach + CleaveLength * 0.5f);

        public override BossPatternBase CreatePattern(BossServices services)
            => new CleaveCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;

            Gizmos.DrawWireSphere(boss.position + boss.up * SpinHeight, SpinRadius);
            Gizmos.DrawWireSphere(boss.position + fwd * BackhandReach + boss.up * BackhandHeight, BackhandRadius);

            Gizmos.matrix = Matrix4x4.TRS(
                boss.position + fwd * CleaveReach + boss.up * (CleaveHeight * 0.5f),
                Quaternion.LookRotation(fwd), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(CleaveWidth, CleaveHeight, CleaveLength));
            Gizmos.matrix = Matrix4x4.identity;   // 복원 — 다음 패턴 기즈모가 이 행렬 안 물게
        }

        private void OnValidate()
        {
            if (SpinAtSeconds > BackhandAtSeconds || BackhandAtSeconds > CleaveAtSeconds)
                Debug.LogWarning($"{name}: 연계 타격 시점이 순서대로가 아님 (회전 → 칼등치기 → 일도양단)", this);
            if (!ManualActiveSeconds && CleaveAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 일도양단 시점({CleaveAtSeconds:F2}s)이 Active 길이({ActiveSeconds:F2}s) 밖 — 타격이 유실된다", this);
        }
    }
}
