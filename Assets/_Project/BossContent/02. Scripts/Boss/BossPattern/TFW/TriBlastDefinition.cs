using UnityEngine;

namespace Game.BossContent
{
    /// 3연속 폭발(1페이즈 대검). 근거리 → 중거리 → 원거리 순으로 충격파가 퍼져나간다.
    /// 안에서 밖으로 번지므로 각 단계는 꽉 찬 원이어도 성립한다 —
    /// 1타에 맞고 밖으로 뛴 플레이어를 2타가 쫓아가는 리듬이 된다.
    [CreateAssetMenu(fileName = "TriBlastDef", menuName = "BossContent/Boss/TFW/TriBlast")]
    public class TriBlastDefinition : TfwPatternDefinition, IPatternEffectSource
    {
        [Header("폭발 타이밍 — Active 시작 기준 초")]
        public float NearAtSeconds = 0.3f;
        public float MidAtSeconds = 1.1f;
        public float FarAtSeconds = 1.9f;

        [Header("반경 (안 → 밖 순서여야 한다)")]
        public float NearRadius = 4f;
        public float MidRadius = 8f;
        public float FarRadius = 13f;

        [Header("데미지 — 멀수록 약하게 두면 도망이 보상받는다")]
        public int NearDamage = 25;
        public int MidDamage = 18;
        public int FarDamage = 12;

        public float BlastHeight = 1f;

        [Header("연출 — 파티클 프리팹을 꽂는다")]
        [SerializeField] private PatternEffectCue[] _effectCues;
        public PatternEffectCue[] EffectCues => _effectCues;

        // 가장 바깥 폭발이 사거리를 정한다.
        protected override float HitRange => FarRadius;

        public override BossPatternBase CreatePattern(BossServices services)
            => new TriBlast(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Gizmos.color = GizmoColor;
            Vector3 center = boss.position + boss.up * BlastHeight;
            Gizmos.DrawWireSphere(center, NearRadius);
            Gizmos.DrawWireSphere(center, MidRadius);
            Gizmos.DrawWireSphere(center, FarRadius);
        }

        private void OnValidate()
        {
            if (NearRadius >= MidRadius || MidRadius >= FarRadius)
                Debug.LogWarning($"{name}: 반경이 안→밖 순서가 아님 (근 < 중 < 원)", this);
            if (NearAtSeconds > MidAtSeconds || MidAtSeconds > FarAtSeconds)
                Debug.LogWarning($"{name}: 폭발 시점이 순서대로가 아님", this);
            if (!ManualActiveSeconds && FarAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 원거리 폭발 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
