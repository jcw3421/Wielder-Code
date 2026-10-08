using Game.Core;
using UnityEngine;

namespace Game.BossContent
{
    /// 도넛 장판(2페이즈). 보스를 중심으로 고리 모양이 터진다 — 안쪽이 안전지대라
    /// "물러나지 말고 파고들어라"를 강제한다. 원본 패턴들 사이사이에 끼워 쓰는 마무리 공격이다.
    [CreateAssetMenu(fileName = "DonutFieldDef", menuName = "BossContent/Boss/TFW/DonutField")]
    public class DonutFieldDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("폭발 시점 — Active 시작 기준 초")]
        public float BurstAtSeconds = 0.5f;

        [Header("고리 반경")]
        [Tooltip("이 반경 안쪽은 안전하다 — 보스에게 붙으면 피할 수 있다.")]
        public float InnerRadius = 3.5f;
        public float OuterRadius = 14f;
        public float BurstHeight = 1f;

        [Header("전진 이동")]
        public bool UseForwardMotion = false;   // 제자리 패턴

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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { BurstAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        // 바깥 고리까지 닿아야 의미가 있으니 사거리는 넉넉히 잡는다.
        protected override float HitRange => OuterRadius;

        public override BossPatternBase CreatePattern(BossServices services)
            => new DonutField(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Gizmos.color = GizmoColor;
            Vector3 center = boss.position + boss.up * BurstHeight;
            Gizmos.DrawWireSphere(center, InnerRadius);   // 안전지대
            Gizmos.DrawWireSphere(center, OuterRadius);
        }

        private void OnValidate()
        {
            if (InnerRadius >= OuterRadius)
                Debug.LogWarning($"{name}: 안쪽 반경이 바깥보다 크거나 같음 — 아무도 안 맞는다", this);
            if (!ManualActiveSeconds && BurstAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 폭발 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
