using UnityEngine;

namespace Game.BossContent
{
    /// 검·방패 연계(2페이즈). 검 → 방패 → 검 → 뒤돌기 → 찌르기.
    /// 4타(뒤돌기)가 등 뒤를 치므로 "뒤로 돌아가면 안전하다"는 습관을 깬다.
    [CreateAssetMenu(fileName = "SwordShieldComboDef", menuName = "BossContent/Boss/TFW/SwordShieldCombo")]
    public class SwordShieldComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("연계 타이밍 — Active 시작 기준 초")]
        public float Slash1AtSeconds = 0.4f;
        public float ShieldAtSeconds = 1.2f;
        public float Slash2AtSeconds = 2.0f;
        public float BackTurnAtSeconds = 2.8f;
        public float ThrustAtSeconds = 3.6f;

        [Header("검 베기 (1·3타, 부채꼴)")]
        public float SlashRadius = 4f;
        public float SlashHalfAngle = 60f;
        public int SlashDamage = 12;

        [Header("방패 후려치기 (2타, 전방 원형)")]
        public float ShieldReach = 2.5f;
        public float ShieldRadius = 2.5f;
        public int ShieldDamage = 10;

        [Header("뒤돌기 (4타, 후방 부채꼴)")]
        public float BackTurnRadius = 4.5f;
        public float BackTurnHalfAngle = 70f;
        public int BackTurnDamage = 14;

        [Header("찌르기 (5타, 좁고 긴 박스, 데미지는 공통 Damage)")]
        public float ThrustReach = 4f;
        public float ThrustLength = 8f;
        public float ThrustWidth = 1.5f;
        public float StrikeHeight = 2.5f;

        [Header("전진 이동")]
        [Tooltip("클립 루트모션을 따라 앞뒤로 움직인다. 끄면 제자리에서만 패턴을 친다.")]
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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { Slash1AtSeconds, ShieldAtSeconds, Slash2AtSeconds, BackTurnAtSeconds, ThrustAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        protected override float HitRange => Mathf.Max(
            SlashRadius, ShieldReach + ShieldRadius, BackTurnRadius,
            ThrustReach + ThrustLength * 0.5f);

        public override BossPatternBase CreatePattern(BossServices services)
            => new SwordShieldCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;

            Gizmos.DrawWireSphere(boss.position + boss.up, SlashRadius);
            Gizmos.DrawWireSphere(boss.position + fwd * ShieldReach + boss.up, ShieldRadius);

            Gizmos.matrix = Matrix4x4.TRS(
                boss.position + fwd * ThrustReach + boss.up * (StrikeHeight * 0.5f),
                Quaternion.LookRotation(fwd), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(ThrustWidth, StrikeHeight, ThrustLength));
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnValidate()
        {
            if (Slash1AtSeconds > ShieldAtSeconds || ShieldAtSeconds > Slash2AtSeconds
                || Slash2AtSeconds > BackTurnAtSeconds || BackTurnAtSeconds > ThrustAtSeconds)
                Debug.LogWarning($"{name}: 연계 타격 시점이 순서대로가 아님", this);
            if (!ManualActiveSeconds && ThrustAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 찌르기 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
