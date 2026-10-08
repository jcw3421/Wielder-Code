using UnityEngine;

namespace Game.BossContent
{
    /// 4타 연계(1페이즈 대검). 찌르기 → 일도양단 → 후방 일도양단 → 전방 참격.
    /// 3타가 뒤를 치므로 "뒤로 돌아가면 안전하다"는 습관을 깨는 패턴이다.
    [CreateAssetMenu(fileName = "FourStrikeComboDef", menuName = "BossContent/Boss/TFW/FourStrikeCombo")]
    public class FourStrikeComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {

        [Header("연계 타이밍 — Active 시작 기준 초")]
        public float ThrustAtSeconds = 0.4f;
        public float FrontCleaveAtSeconds = 1.4f;
        public float BackCleaveAtSeconds = 2.4f;
        public float FinishAtSeconds = 3.4f;

        [Header("1타 — 찌르기 (좁고 긴 박스)")]
        public float ThrustReach = 4f;
        public float ThrustLength = 8f;
        public float ThrustWidth = 1.5f;
        public int ThrustDamage = 14;

        [Header("2·3타 — 전방·후방 일도양단 (같은 기하, 방향만 반대)")]
        public float CleaveReach = 4f;
        public float CleaveLength = 8f;
        public float CleaveWidth = 3.5f;
        public int CleaveDamage = 16;

        [Header("4타 — 마무리 전방 참격 (부채꼴, 데미지는 공통 Damage)")]
        public float FinishRadius = 6f;
        public float FinishHalfAngle = 75f;

        public float StrikeHeight = 2.5f;   // 박스 높이 겸 판정 중심 높이

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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { ThrustAtSeconds, FrontCleaveAtSeconds, BackCleaveAtSeconds, FinishAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        protected override float HitRange => Mathf.Max(
            ThrustReach + ThrustLength * 0.5f,
            CleaveReach + CleaveLength * 0.5f,
            FinishRadius);

        public override BossPatternBase CreatePattern(BossServices services)
            => new FourStrikeCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;
            Vector3 up = boss.up * (StrikeHeight * 0.5f);

            DrawBox(boss.position + fwd * ThrustReach + up, fwd,
                    new Vector3(ThrustWidth, StrikeHeight, ThrustLength));
            DrawBox(boss.position + fwd * CleaveReach + up, fwd,
                    new Vector3(CleaveWidth, StrikeHeight, CleaveLength));
            DrawBox(boss.position - fwd * CleaveReach + up, -fwd,
                    new Vector3(CleaveWidth, StrikeHeight, CleaveLength));
            Gizmos.DrawWireSphere(boss.position + boss.up, FinishRadius);
        }

        private static void DrawBox(Vector3 center, Vector3 forward, Vector3 size)
        {
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(forward), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnValidate()
        {
            if (ThrustAtSeconds > FrontCleaveAtSeconds || FrontCleaveAtSeconds > BackCleaveAtSeconds
                || BackCleaveAtSeconds > FinishAtSeconds)
                Debug.LogWarning($"{name}: 연계 타격 시점이 순서대로가 아님", this);
            if (!ManualActiveSeconds && FinishAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 마무리 참격 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
