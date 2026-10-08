using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>공격 중 재생할 이펙트 하나. 시점·위치·배속을 각자 갖기 때문에
    /// 한 공격에 서로 다른 이펙트를 여러 개 다른 타이밍으로 둘 수 있다.
    ///
    /// 위치·회전은 시전자(무기 오브젝트) 기준 로컬값이고, 재생 시점의 위치에 스냅샷으로 놓인다
    /// (플레이어를 따라다니지 않는다).</summary>
    [Serializable]
    public struct AttackParticle
    {
        [Tooltip("ParticleGroup이 붙은 이펙트 프리팹.")]
        public GameObject Prefab;

        [Tooltip("공격 시작 후 재생 시점(초).")]
        public float Time;

        [Tooltip("시전자 기준 로컬 위치.")]
        public Vector3 Offset;

        [Tooltip("시전자 기준 로컬 회전(오일러).")]
        public Vector3 Euler;

        [Tooltip("재생 배속. 1 = 원본.")]
        public float Speed;

        [Tooltip("켜면 재생 내내 시전자를 따라다닌다. 차징·오라처럼 지속되는 이펙트용.\n" +
                 "끄면 발생 위치에 남는다 — 베기 궤적·폭발처럼 순간적인 이펙트는 이쪽이 맞다.")]
        public bool FollowCaster;

        [Tooltip("차징 중 유지하고, 차징 종료 시 해당 재생 인스턴스만 멈춥니다.")]
        public bool Sustained;

        /// 0 이하면 파티클이 멈춰버리므로 1로 막는다.
        public float SafeSpeed => Speed > 0f ? Speed : 1f;
    }
}
