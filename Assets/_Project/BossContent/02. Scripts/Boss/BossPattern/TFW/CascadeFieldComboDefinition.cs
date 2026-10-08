using UnityEngine;

namespace Game.BossContent
{
    /// 연쇄 장판 연계(2페이즈). 방패 → 검 → X검으로 몰아붙인 뒤 장판을 4회 연달아 깐다.
    /// 근접 연계에 묶여 있다가 장판이 시작되므로, 붙어 있던 사람이 그대로 물리기 쉽다.
    [CreateAssetMenu(fileName = "CascadeFieldComboDef", menuName = "BossContent/Boss/TFW/CascadeFieldCombo")]
    public class CascadeFieldComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("근접 연계 타이밍 — Active 시작 기준 초")]
        public float ShieldAtSeconds = 0.4f;
        public float SlashAtSeconds = 1.2f;
        public float CrossSlashAtSeconds = 2.0f;

        [Header("방패 후려치기 (1타)")]
        public float ShieldReach = 2.5f;
        public float ShieldRadius = 2.5f;
        public int ShieldDamage = 10;

        [Header("검 베기 (2타, 부채꼴)")]
        public float SlashRadius = 4f;
        public float SlashHalfAngle = 60f;
        public int SlashDamage = 12;

        [Header("X 베기 (3타, 넓은 부채꼴, 데미지는 공통 Damage)")]
        public float CrossRadius = 5f;
        public float CrossHalfAngle = 80f;

        [Header("연쇄 장판 (4회)")]
        [Tooltip("첫 장판이 깔리는 시점(초). 이후 간격마다 반복된다.")]
        public float FirstFieldAtSeconds = 2.8f;
        public float FieldIntervalSeconds = 0.7f;
        public int FieldWaveCount = 4;
        [Tooltip("장판 자체의 파라미터. FieldPrefab을 꽂아야 실제로 깔린다.")]
        public AoeBurstConfig Burst = new();

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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { ShieldAtSeconds, SlashAtSeconds, CrossSlashAtSeconds, FirstFieldAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        protected override float HitRange => Mathf.Max(CrossRadius, ShieldReach + ShieldRadius);

        public override BossPatternBase CreatePattern(BossServices services)
            => new CascadeFieldCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(boss.position + boss.up, CrossRadius);
        }

        private void OnValidate()
        {
            if (ShieldAtSeconds > SlashAtSeconds || SlashAtSeconds > CrossSlashAtSeconds)
                Debug.LogWarning($"{name}: 근접 연계 타격 시점이 순서대로가 아님", this);
            if (Burst != null && Burst.FieldPrefab == null)
                Debug.LogWarning($"{name}: Burst의 FieldPrefab이 비어 있음 — 장판이 안 깔린다", this);

            float lastField = FirstFieldAtSeconds + FieldIntervalSeconds * (FieldWaveCount - 1);
            if (!ManualActiveSeconds && lastField > ActiveSeconds)
                Debug.LogWarning($"{name}: 마지막 장판({lastField:F2}s)이 Active 길이 밖 — 유실된다", this);
        }
    }
}
