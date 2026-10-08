using UnityEngine;

namespace Game.BossContent
{
    /// 방패찍기 연계(2페이즈). 검 → 방패 → X검 → 방패찍기(스턴) → 뒷발차기.
    /// 4타 방패찍기에 맞으면 기절한다 — 연계 후반을 그대로 얻어맞게 만드는 처벌 구간이다.
    [CreateAssetMenu(fileName = "ShieldBashComboDef", menuName = "BossContent/Boss/TFW/ShieldBashCombo")]
    public class ShieldBashComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("연계 타이밍 — Active 시작 기준 초")]
        public float SlashAtSeconds = 0.4f;
        public float ShieldAtSeconds = 1.2f;
        public float CrossSlashAtSeconds = 2.0f;
        public float BashAtSeconds = 2.8f;
        public float KickAtSeconds = 3.6f;

        [Header("검 베기 (1타, 부채꼴)")]
        public float SlashRadius = 4f;
        public float SlashHalfAngle = 60f;
        public int SlashDamage = 12;

        [Header("방패 후려치기 (2타)")]
        public float ShieldReach = 2.5f;
        public float ShieldRadius = 2.5f;
        public int ShieldDamage = 10;

        [Header("X 베기 (3타, 넓은 부채꼴)")]
        public float CrossRadius = 5f;
        public float CrossHalfAngle = 80f;
        public int CrossDamage = 15;

        [Header("방패찍기 (4타 — 이 타격만 스턴)")]
        public float BashReach = 3f;
        public float BashRadius = 3f;
        public int BashDamage = 8;

        [Header("뒷발차기 (5타, 데미지는 공통 Damage)")]
        public float KickReach = 3f;
        public float KickRadius = 3.5f;

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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { SlashAtSeconds, ShieldAtSeconds, CrossSlashAtSeconds, BashAtSeconds, KickAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        protected override float HitRange => Mathf.Max(
            SlashRadius, CrossRadius, ShieldReach + ShieldRadius,
            BashReach + BashRadius, KickReach + KickRadius);

        public override BossPatternBase CreatePattern(BossServices services)
            => new ShieldBashCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;

            Gizmos.DrawWireSphere(boss.position + boss.up, CrossRadius);
            Gizmos.DrawWireSphere(boss.position + fwd * BashReach + boss.up, BashRadius);
        }

        private void OnValidate()
        {
            if (SlashAtSeconds > ShieldAtSeconds || ShieldAtSeconds > CrossSlashAtSeconds
                || CrossSlashAtSeconds > BashAtSeconds || BashAtSeconds > KickAtSeconds)
                Debug.LogWarning($"{name}: 연계 타격 시점이 순서대로가 아님", this);
            if (!ManualActiveSeconds && KickAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 뒷발차기 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
