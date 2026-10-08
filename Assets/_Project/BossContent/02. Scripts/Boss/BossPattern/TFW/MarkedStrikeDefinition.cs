using UnityEngine;

namespace Game.BossContent
{
    /// 지목 공격(2페이즈). 무작위 플레이어 한 명의 발밑을 표시하고, 잠시 뒤 그 자리를 내리찍는다.
    /// 어그로와 무관하게 대상이 정해지므로 탱커 뒤에 숨어도 소용이 없다 —
    /// 지목당한 사람이 스스로 빠지고 주변이 흩어져야 하는 패턴이다.
    [CreateAssetMenu(fileName = "MarkedStrikeDef", menuName = "BossContent/Boss/TFW/MarkedStrike")]
    public class MarkedStrikeDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("타이밍 — Active 시작 기준 초")]
        [Tooltip("대상을 정하고 표시(데칼)를 띄우는 시점.")]
        public float MarkAtSeconds = 0.2f;
        [Tooltip("표시한 자리를 실제로 내리찍는 시점. 사이 간격이 곧 회피 시간이다.")]
        public float ImpactAtSeconds = 1.6f;

        [Header("타격")]
        public float ImpactRadius = 4f;

        [Header("표시 — 대상 발밑에 띄울 경고 데칼")]
        [Tooltip("네트워크 오브젝트여야 모든 플레이어에게 보인다(03. Prefabs/Effect의 경고 데칼 등).\n수명은 프리팹이 스스로 정리해야 한다 — 패턴은 스폰만 한다.")]
        public Fusion.NetworkObject MarkerPrefab;

        [Header("전진 이동")]
        public bool UseForwardMotion = false;   // 제자리에서 내리찍는다

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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { ImpactAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        // 대상이 어디 있든 찍으므로 사거리 제한이 사실상 없어야 한다.
        protected override float HitRange => 100f;

        public override BossPatternBase CreatePattern(BossServices services)
            => new MarkedStrike(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(boss.position, ImpactRadius);   // 실제 위치는 대상 발밑이다
        }

        private void OnValidate()
        {
            if (MarkAtSeconds >= ImpactAtSeconds)
                Debug.LogWarning($"{name}: 표시가 타격보다 늦거나 같음 — 피할 시간이 없다", this);
            if (MarkerPrefab == null)
                Debug.LogWarning($"{name}: MarkerPrefab이 비어 있음 — 예고 없이 맞는 패턴이 된다", this);
            if (!ManualActiveSeconds && ImpactAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 타격 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
