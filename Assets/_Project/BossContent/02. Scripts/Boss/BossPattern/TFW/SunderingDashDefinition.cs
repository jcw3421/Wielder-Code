using UnityEngine;

namespace Game.BossContent
{
    /// 섬단(1페이즈 대검). 멀어진 대상을 향해 일직선으로 빠르게 파고들며 경로를 베고,
    /// 돌진이 끝나는 지점에서 전방 참격으로 마무리한다.
    [CreateAssetMenu(fileName = "SunderingDashDef", menuName = "BossContent/Boss/TFW/SunderingDash")]
    public class SunderingDashDefinition : TfwPatternDefinition, IPatternEffectSource
    {
        [Header("조준")]
        [Tooltip("예고 시간 중 어느 비율까지 타겟을 따라가고 방향을 확정할지. 락 이후 남은 시간이 회피 리드타임이 된다.")]
        [Range(0f, 1f)] public float LockAtTelegraphRatio = 0.6f;
        public float TelegraphRotationSpeed = 12f;

        [Header("돌진")]
        [Tooltip("체크하면 아래 곡선(클립 루트모션)을 따라 움직인다. 해제하면 DashSpeed로 등속 돌진한다(기본).")]
        public bool UseClipDistance = true;
        [Tooltip("x = Active 시작 후 초, y = 누적 전진 거리(m). 베이커가 클립에서 뽑는다.")]
        public AnimationCurve ForwardDistanceCurve = new AnimationCurve();
        [Tooltip("UseClipDistance를 끌 때 쓰는 등속 돌진 속도.")]
        public float DashSpeed = 18f;
        public float PathRadius = 2f;        // 경로 타격 반경
        public int PathDamage = 10;          // 경로에 스친 대상
        public float HitHeight = 1f;
        public float HitFrontOffset = 1.5f;  // 판정구를 몸 앞에 둬 블로커보다 먼저 닿게

        [Header("마무리 참격 (데미지는 공통 Damage 필드)")]
        public float FinishReach = 3.5f;
        public float FinishRadius = 4f;

        [Header("연출 — 파티클 프리팹을 꽂는다")]
        [SerializeField] private PatternEffectCue[] _effectCues;
        public PatternEffectCue[] EffectCues => _effectCues;

        // 돌진 거리는 사거리 판단에서 뺀다 — 멀리서 시작하는 게 이 패턴의 목적이라
        // AttackRange를 돌진 거리까지 키우면 사실상 상시 발동이 된다.
        protected override float HitRange => Mathf.Max(PathRadius, FinishReach + FinishRadius);

        public override BossPatternBase CreatePattern(BossServices services)
            => new SunderingDash(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;

            // 경로 판정구 — 실제로는 매 틱 전진하며 찍히므로 이건 시작 지점 한 컷이다.
            Gizmos.DrawWireSphere(boss.position + boss.up * HitHeight + fwd * HitFrontOffset, PathRadius);
            Gizmos.DrawWireSphere(boss.position + fwd * FinishReach + boss.up * HitHeight, FinishRadius);
        }
    }
}
