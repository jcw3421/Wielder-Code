using UnityEngine;

namespace Game.BossContent
{
    /// 레이저 연계(2페이즈). 방패 후려치기 → 방사형 레이저 1차 → 각도를 틀어 2차 → 지목 강타.
    ///
    /// 보스를 중심으로 LaserCount(기본 6)개의 줄기가 방사형으로 뻗는다. 줄기 사이의
    /// 쐐기꼴 빈 공간이 안전지대다. 2차는 절반 칸만큼 돌려 쏘므로, 1차에서 안전했던 자리가
    /// 2차에서는 줄기 위가 된다 — 가만히 서 있으면 안 되고 반드시 움직여야 한다.
    [CreateAssetMenu(fileName = "LaserComboDef", menuName = "BossContent/Boss/TFW/LaserCombo")]
    public class LaserComboDefinition : TfwPatternDefinition, IPatternEffectSource, ITfwComboDefinition
    {
        [Header("연계 타이밍 — Active 시작 기준 초")]
        public float ShieldAtSeconds = 0.4f;
        public float FrontLaserAtSeconds = 1.4f;
        public float BackLaserAtSeconds = 2.4f;
        public float MarkedAtSeconds = 3.6f;

        [Header("방사형 레이저")]
        [Tooltip("보스를 중심으로 몇 갈래로 뻗을지. 6이면 60도 간격이다.")]
        [Range(2, 12)] public int LaserCount = 6;

        [Tooltip("몇 번 발사할지. 1이면 FrontLaserAtSeconds에 한 번만 쏘고 " +
                 "BackLaserAtSeconds는 무시된다(연출 큐도 안 생긴다). " +
                 "2면 SecondVolleyOffset만큼 돌려 한 번 더 쏜다.")]
        [Range(1, 2)] public int LaserVolleys = 1;

        [Tooltip("2차 발사를 1차에서 몇 칸만큼 돌릴지(0~1). 0.5면 정확히 줄기 사이로 온다 — " +
                 "1차 안전지대에 그대로 서 있으면 2차에 맞는다.")]
        [Range(0f, 1f)] public float SecondVolleyOffset = 0.5f;

        [Header("방패 후려치기 (1타)")]
        public float ShieldReach = 2.5f;
        public float ShieldRadius = 2.5f;
        public int ShieldDamage = 10;

        [Header("레이저 (2·3타 — 보스에서 뻗어나가는 좁고 긴 박스)")]
        public float LaserLength = 20f;
        public float LaserWidth = 2.5f;
        public float LaserHeight = 3f;
        public int LaserDamage = 20;

        [Header("지목 강타 (4타, 데미지는 공통 Damage)")]
        [Tooltip("무작위 플레이어 위치를 내리찍는다. 어그로와 무관하다.")]
        public float MarkedRadius = 4f;

        [Header("전진 이동")]
        public bool UseForwardMotion = false;   // 제자리에서 훑는다

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
        float[] ITfwComboDefinition.SegmentShakeSeconds => new[] { ShieldAtSeconds, FrontLaserAtSeconds, BackLaserAtSeconds, MarkedAtSeconds };
        int[] ITfwComboDefinition.TurnAroundSegments => TurnAroundSegments;
        AnimationCurve ITfwComboDefinition.ForwardDistanceCurve => ForwardDistanceCurve;

        // 레이저가 멀리 뻗지만, 패턴 개시는 근접에서 하도록 방패 기준으로 잡는다.
        protected override float HitRange => ShieldReach + ShieldRadius;

        public override BossPatternBase CreatePattern(BossServices services)
            => new LaserCombo(this, services);

        public override void DrawRangeGizmo(Transform boss)
        {
            Vector3 fwd = boss.forward; fwd.y = 0f; fwd.Normalize();
            Gizmos.color = GizmoColor;
            Vector3 up = boss.up * (LaserHeight * 0.5f);

            // 1차(실선 색)와 2차(반투명)를 같이 그려 안전지대가 어떻게 뒤집히는지 보이게 한다.
            for (int i = 0; i < Mathf.Max(2, LaserCount); i++)
            {
                DrawLaser(boss.position + up, LaserDirection(fwd, i, 0f));
            }

            var c = GizmoColor; c.a *= 0.35f;
            Gizmos.color = c;
            for (int i = 0; i < Mathf.Max(2, LaserCount); i++)
            {
                DrawLaser(boss.position + up, LaserDirection(fwd, i, SecondVolleyOffset));
            }

            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(boss.position + fwd * ShieldReach + boss.up, ShieldRadius);
        }

        /// i번째 줄기의 방향. volleyOffset은 "칸 하나의 몇 배만큼 돌릴지"다.
        /// 판정(LaserCombo)과 기즈모가 같은 식을 써야 그려진 대로 맞는다.
        public Vector3 LaserDirection(Vector3 forward, int index, float volleyOffset)
        {
            int n = Mathf.Max(2, LaserCount);
            float step = 360f / n;
            return Quaternion.AngleAxis((index + volleyOffset) * step, Vector3.up) * forward;
        }

        private void DrawLaser(Vector3 origin, Vector3 forward)
        {
            Vector3 center = origin + forward * (LaserLength * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(forward), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(LaserWidth, LaserHeight, LaserLength));
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnValidate()
        {
            if (ShieldAtSeconds > FrontLaserAtSeconds || FrontLaserAtSeconds > BackLaserAtSeconds
                || BackLaserAtSeconds > MarkedAtSeconds)
                Debug.LogWarning($"{name}: 연계 타격 시점이 순서대로가 아님", this);
            if (!ManualActiveSeconds && MarkedAtSeconds > ActiveSeconds)
                Debug.LogWarning($"{name}: 지목 강타 시점이 Active 길이 밖 — 타격이 유실된다", this);
        }
    }
}
