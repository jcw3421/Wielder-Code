using UnityEngine;

namespace Game.BossContent
{
    /// 올려베기 연계(1페이즈 대검). 오른쪽 올려베기 → 왼쪽 올려베기 → 백스텝 → 일도양단.
    /// 백스텝으로 거리를 벌린 뒤 다시 파고들며 베는 리듬이라, 붙어 있던 플레이어가 한 번 속는다.
    /// 백스텝은 별도 코드 없이 ForwardDistanceCurve가 내려가는 구간으로 표현된다.
    [CreateAssetMenu(fileName = "RisingComboDef", menuName = "BossContent/Boss/TFW/RisingCombo")]
    public class RisingComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {

        [Header("연계 타이밍 — Active 시작 기준 초")]
        public float RightRiseAtSeconds = 0.5f;
        public float LeftRiseAtSeconds = 1.3f;
        public float CleaveAtSeconds = 3.6f;

        [Header("1·2타 — 좌우 올려베기 (부채꼴)")]
        public float RiseRadius = 4.5f;
        public float RiseHalfAngle = 55f;
        [Tooltip("정면에서 좌우로 기울이는 각도. 오른쪽 베기는 +, 왼쪽 베기는 -로 적용된다.")]
        public float RiseYawOffset = 30f;
        public int RiseDamage = 11;

        [Header("3타 — 일도양단 (전방 직선, 데미지는 공통 Damage)")]
        public float CleaveReach = 5f;
        public float CleaveLength = 9f;
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
        [Tooltip("x = Active 시작 후 초, y = 누적 전진 거리(m). 백스텝 구간은 곡선이 내려간다. 손으로 다듬어도 된다.")]
        public AnimationCurve ForwardDistanceCurve = new AnimationCurve();

        [Header("연출 — 파티클 프리팹을 꽂는다")]
        [SerializeField] private PatternEffectCue[] _effectCues;
        public PatternEffectCue[] EffectCues => _effectCues;

        bool ITfwComboDefinition.UseForwardMotion => UseForwardMotion;
        float[] ITfwComboDefinition.SegmentStartSeconds => SegmentStartSeconds;
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { RightRiseAtSeconds, LeftRiseAtSeconds, TfwComboTiming.SegmentStart(SegmentStartSeconds, 2), CleaveAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        // 백스텝으로 물러난 만큼 일도양단이 멀리 나가지만, 사거리 판단은 시작 위치 기준이라 넣지 않는다.
        protected override float HitRange => Mathf.Max(RiseRadius, CleaveReach + CleaveLength * 0.5f);

        public override BossPatternBase CreatePattern(BossServices services)
            => new RisingCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;

            Gizmos.DrawWireSphere(boss.position + boss.up, RiseRadius);   // 부채꼴 반경(각도는 생략)
            Gizmos.matrix = Matrix4x4.TRS(
                boss.position + fwd * CleaveReach + boss.up * (CleaveHeight * 0.5f),
                Quaternion.LookRotation(fwd), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(CleaveWidth, CleaveHeight, CleaveLength));
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnValidate()
        {
            if (RightRiseAtSeconds > LeftRiseAtSeconds || LeftRiseAtSeconds > CleaveAtSeconds)
                Debug.LogWarning($"{name}: 연계 타격 시점이 순서대로가 아님 (우 → 좌 → 일도양단)", this);
            if (!ManualActiveSeconds && CleaveAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 일도양단 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
