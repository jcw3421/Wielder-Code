using Game.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Game.BossContent
{
    /// TFW 패턴들이 공유하는 판정 헬퍼.
    ///
    /// BossPatternBase의 공용 헬퍼는 CC를 SO의 HitCC 하나로 고정하고(ApplyHitCC가 private),
    /// 도넛(가운데가 빈 원) 모양도 없다. 연계 안에서 타격마다 다른 CC를 걸거나
    /// "보스 몸 주변만 안전한" 장판을 만들려면 여기 것들을 쓴다.
    ///
    /// 전부 서버에서만 호출된다(패턴 로직은 StateAuthority 전용).
    public static class TfwHits
    {
        private static readonly Collider[] Buffer = new Collider[32];

        /// 원형 판정 — CC를 직접 지정한다. CCType.None이면 CC를 걸지 않는다.
        public static void Sphere(Vector3 center, float radius, int damage, CCType cc)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
                Apply(Buffer[i], damage, cc);
        }

        /// 도넛 판정 — 바깥 반경 안이면서 안쪽 반경 밖인 대상만 때린다.
        /// "보스에게 바짝 붙으면 안전한" 장판을 만들 때 쓴다(안쪽 반경이 안전지대).
        public static void Donut(Vector3 center, float innerRadius, float outerRadius, int damage, CCType cc)
        {
            float innerSqr = innerRadius * innerRadius;
            int count = Physics.OverlapSphereNonAlloc(center, outerRadius, Buffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                Vector3 to = Buffer[i].transform.position - center;
                to.y = 0f;
                if (to.sqrMagnitude < innerSqr) continue;   // 안전지대
                Apply(Buffer[i], damage, cc);
            }
        }

        /// 박스 판정 — 레이저처럼 좁고 긴 직선 공격에 쓴다.
        public static void Box(Vector3 center, Vector3 halfExtents, Quaternion rotation, int damage, CCType cc)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, Buffer, rotation, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
                Apply(Buffer[i], damage, cc);
        }

        /// 부채꼴 판정 — 반경 안 + 정면 기준 halfAngleDeg 안.
        public static void Fan(Vector3 origin, Vector3 forward, float radius, float halfAngleDeg,
                               int damage, CCType cc)
        {
            forward.y = 0f;
            forward.Normalize();

            int count = Physics.OverlapSphereNonAlloc(origin, radius, Buffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                Vector3 to = Buffer[i].transform.position - origin;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Angle(forward, to) > halfAngleDeg) continue;
                Apply(Buffer[i], damage, cc);
            }
        }

        /// 여러 틱에 걸쳐 때리는 패턴용 — alreadyHit에 없는 대상만 때려 대상당 1회를 보장한다.
        public static void SphereOnce(Vector3 center, float radius, int damage, CCType cc,
                                      HashSet<IDamageable> alreadyHit)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, BossLayers.PlayerMask);
            for (int i = 0; i < count; i++)
            {
                var rb = Buffer[i].attachedRigidbody;
                if (rb == null || !rb.TryGetComponent<IDamageable>(out var target)) continue;
                if (!alreadyHit.Add(target)) continue;

                target.ApplyDamage(new DamageInfo(damage, DamageType.Normal));
                ApplyCC(rb, cc);
            }
        }

        private static readonly Vector3[] PlayerBuffer = new Vector3[16];

        /// 살아있는 플레이어 중 하나를 결정론적으로 고른다(어그로 무관).
        /// salt는 같은 틱에 여러 번 뽑을 때 서로 다른 결과가 나오도록 섞는 값이다.
        /// 서버에서만 도는 로직이지만 시드를 고정해 리플레이·재시뮬레이션에서도 같은 대상이 나온다.
        ///
        /// 사망자를 반드시 걸러야 한다 — 시체를 지목하면 그 패턴이 통째로 허공을 친다.
        /// (GetAllPlayerPositions는 사망자를 포함한다. 이름이 비슷하니 주의.)
        public static bool TryPickRandomPlayer(BossServices services, int salt, out Vector3 position)
        {
            int count = services.Sensor.GetAlivePlayerPositions(PlayerBuffer);
            if (count == 0)
            {
                position = default;
                return false;
            }

            int seed = Game.Networking.DeterministicRng.Combine(services.Stats.BossID, salt,
                                                               services.Net.PatternStartSeq);
            var rng = Game.Networking.SessionContext.Current.CreateRng(seed);
            position = PlayerBuffer[rng.Range(0, count)];
            return true;
        }

        private static void Apply(Collider collider, int damage, CCType cc)
        {
            var rb = collider.attachedRigidbody;
            if (rb == null || !rb.TryGetComponent<IDamageable>(out var target)) return;

            target.ApplyDamage(new DamageInfo(damage, DamageType.Normal));
            ApplyCC(rb, cc);
        }

        private static void ApplyCC(Rigidbody rb, CCType cc)
        {
            if (cc == CCType.None) return;
            if (rb.TryGetComponent<ICrowdControllable>(out var controllable))
                controllable.ApplyCC(new CCInfo(cc));   // 지속은 Core의 StatusEffectConstants 기본값
        }
    }
}
