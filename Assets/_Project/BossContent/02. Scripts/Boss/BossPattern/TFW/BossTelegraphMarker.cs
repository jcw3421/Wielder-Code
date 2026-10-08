using Game.Networking;
using UnityEngine;

namespace Game.BossContent
{
    /// 예고 표시 하나의 수명만 책임지는 네트워크 오브젝트. 판정은 하지 않는다 —
    /// 지목 공격의 타격은 MarkedStrike가 서버 틱에서 직접 굴린다.
    ///
    /// BossAoeField를 쓰지 않는 이유: 저쪽은 Init()으로 도화선·판정을 받아야 도는데,
    /// MarkedStrike는 SpawnServerObject만 하고 Init을 부르지 않는다. 그대로 얹으면
    /// _initialized가 false라 FixedUpdateNetwork가 통째로 빠져나가고, 표시가 영영
    /// Despawn되지 않는다.
    ///
    /// 차오름·페이드는 자식 쿼드의 PatternFieldDecal이 로컬로 돌린다. 여기서는
    /// 서버가 정해진 시간 뒤에 지우기만 한다 — 지우는 건 상태 변경이라 서버 몫이다.
    public class BossTelegraphMarker : ServerAuthoritativeBehaviour
    {
        [Tooltip("스폰 후 이 시간이 지나면 서버가 지운다. 예고→타격 간격보다 넉넉해야 " +
                 "맞는 순간까지 표시가 남는다.")]
        [SerializeField] private float _lifeSeconds = 2.5f;

        private int _ticksLeft;

        public override void Spawned()
        {
            if (!HasStateAuthority) return;

            // 틱으로 환산해 둔다 — 프로젝트 규칙상 시간 비교는 네트워크 틱 기준이다.
            _ticksLeft = Mathf.Max(1, Mathf.RoundToInt(_lifeSeconds / Runner.DeltaTime));
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _ticksLeft <= 0) return;
            if (--_ticksLeft > 0) return;

            Runner.Despawn(Object);
        }
    }
}
