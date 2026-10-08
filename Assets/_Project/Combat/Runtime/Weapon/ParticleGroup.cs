using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Combat
{
    public class ParticleGroup : MonoBehaviour
    {
        public int Capacity => Mathf.Max(1, _capacity);

        [FormerlySerializedAs("maxCnt")]
        [SerializeField] private int _capacity = 1;

        [FormerlySerializedAs("particle")]
        [SerializeField] private ParticleSystem _particle;

        // 배속은 자식 시스템에도 걸어야 하나가 통째로 빨라진다. 풀링 객체라 매번 찾지 않고 캐시한다.
        private ParticleSystem[] _all;
        private float _appliedSpeed = 1f;

        private void Awake() => _all = GetComponentsInChildren<ParticleSystem>(true);

        /// 재생될 때마다 올라간다. 풀이 이 인스턴스를 다른 공격에 넘겨준 것을
        /// 바깥에서 알아채기 위한 값 — 남의 이펙트를 붙잡고 옮기는 사고를 막는다.
        public uint PlayVersion { get; private set; }
        public bool IsPlaying
        {
            get
            {
                if (_all == null) return _particle != null && _particle.isPlaying;

                foreach (var particleSystem in _all)
                    if (particleSystem.isPlaying) return true;

                return false;
            }
        }

        public void Play(bool withChild = true, float speed = 1f)
        {
            PlayVersion++;
            SetSpeed(speed);
            _particle.Play(withChild);
        }

        /// <param name="clear">true = 이미 방출된 파티클까지 즉시 지운다(캔슬처럼 "없던 일"이 되어야 할 때).
        /// false = 방출만 멈춰서 남은 파티클이 제 수명까지 그 자리에 떠 있는다(자연스러운 종료).</param>
        public void Stop(bool clear) => _particle.Stop(true,
            clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);

        private void SetSpeed(float speed)
        {
            if (_all == null) return;
            if (Mathf.Approximately(_appliedSpeed, speed)) return; // 같은 배속으로 재사용되는 경우가 대부분

            foreach (var ps in _all)
            {
                var main = ps.main;
                main.simulationSpeed = speed;
            }
            _appliedSpeed = speed;
        }
    }
}
