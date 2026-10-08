using UnityEngine;

namespace Game.BossContent
{
    /// 연속 6회 + 찌르기(2페이즈). 전방을 빠르게 여섯 번 긁고 마지막에 깊게 찌른다.
    /// 타격이 촘촘해 한 번 물리면 빠져나오기 어렵다 — 애초에 정면에 서지 않는 게 답인 패턴이다.
    [CreateAssetMenu(fileName = "RushSixStrikeDef", menuName = "BossContent/Boss/TFW/RushSixStrike")]
    public class RushSixStrikeDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("연속 타격 (6회)")]
        [Tooltip("첫 타격 시점(초). 이후 Interval 간격으로 6번 나간다.")]
        public float FirstStrikeAtSeconds = 0.3f;
        public float StrikeIntervalSeconds = 0.35f;
        public int StrikeCount = 6;

        [Header("연속 타격 기하 (전방 부채꼴)")]
        public float StrikeRadius = 4.5f;
        public float StrikeHalfAngle = 45f;
        public int StrikeDamage = 8;

        [Header("마무리 찌르기 (데미지는 공통 Damage)")]
        public float ThrustAtSeconds = 2.8f;
        public float ThrustReach = 4.5f;
        public float ThrustLength = 9f;
        public float ThrustWidth = 2f;
        public float StrikeHeight = 2.5f;

        [Header("전진 이동")]
        public bool UseForwardMotion = true;

        [Header("조각 — 베이커가 채운다")]
        public float[] SegmentStartSeconds;
        [Tooltip("이 번호(0부터)의 조각에서 보스를 180도 돌린다. 후방 타격 모션 없이 앞으로 쓰는 클립을 재활용할 때 쓴다.")]
        public int[] TurnAroundSegments;
        public AnimationCurve ForwardDistanceCurve = new AnimationCurve();

        [Header("연출 — 파티클 프리팹을 꽂는다")]
        [SerializeField] private PatternEffectCue[] _effectCues;
        public PatternEffectCue[] EffectCues => _effectCues;

        bool ITfwComboDefinition.UseForwardMotion => UseForwardMotion;
        float[] ITfwComboDefinition.SegmentStartSeconds => SegmentStartSeconds;
        float[] ITfwComboDefinition.SegmentShakeSeconds => BuildShakeSeconds();
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        /// 이 패턴만 조각 수와 흔들림 횟수가 다르다.
        ///
        /// 연속 타격은 조각 하나(클립 하나) 안에서 Interval 간격으로 여러 번 나가기 때문에,
        /// 조각 경계로만 흔들면 6번 맞는 동안 화면이 한 번만 움직인다.
        /// 그래서 타격마다 한 칸씩 펼친 배열을 내놓는다 — 마지막 칸이 마무리 찌르기다.
        ///
        /// SegmentShakes도 이 길이에 맞춰야 한다(연속 타격 수 + 1). 짧으면 마지막 값이 이어지므로
        /// 앞쪽을 약하게 채우고 마지막만 세게 두면 된다.
        private float[] BuildShakeSeconds()
        {
            int strikes = Mathf.Max(1, StrikeCount);
            var times = new float[strikes + 1];

            for (int i = 0; i < strikes; i++)
                times[i] = FirstStrikeAtSeconds + StrikeIntervalSeconds * i;

            times[strikes] = ThrustAtSeconds;
            return times;
        }

        protected override float HitRange => Mathf.Max(StrikeRadius, ThrustReach + ThrustLength * 0.5f);

        public override BossPatternBase CreatePattern(BossServices services)
            => new RushSixStrike(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;

            Gizmos.DrawWireSphere(boss.position + boss.up, StrikeRadius);
            Gizmos.matrix = Matrix4x4.TRS(
                boss.position + fwd * ThrustReach + boss.up * (StrikeHeight * 0.5f),
                Quaternion.LookRotation(fwd), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(ThrustWidth, StrikeHeight, ThrustLength));
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnValidate()
        {
            float lastStrike = FirstStrikeAtSeconds + StrikeIntervalSeconds * (StrikeCount - 1);
            if (lastStrike > ThrustAtSeconds)
                Debug.LogWarning($"{name}: 6타({lastStrike:F2}s)가 찌르기({ThrustAtSeconds:F2}s)보다 늦음", this);
            if (!ManualActiveSeconds && ThrustAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 찌르기 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
