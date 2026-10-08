using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public readonly struct EffectHandle
    {
        private readonly ParticleGroup _instance;
        private readonly uint _playVersion;

        internal EffectHandle(ParticleGroup instance)
        {
            _instance = instance;
            _playVersion = instance.PlayVersion;
        }

        public bool IsPlaying => IsCurrent && _instance.IsPlaying;
        private bool IsCurrent => _instance != null && _instance.PlayVersion == _playVersion;

        /// <param name="clear">남아 있는 파티클까지 즉시 지울지. 캔슬이면 true, 자연 종료면 false.</param>
        public void Stop(bool clear)
        {
            if (IsCurrent) _instance.Stop(clear);
        }

        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            if (IsCurrent) _instance.transform.SetPositionAndRotation(position, rotation);
        }
    }

    public class EffectPool : MonoBehaviour
    {
        public static EffectPool Instance { get; private set; }

        private readonly Dictionary<ParticleGroup, ParticleGroup[]> _pools = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public EffectHandle Play(ParticleGroup prefab, Vector3 position, Quaternion rotation,
            float speed = 1f, bool withChildren = true)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ParticleGroup[prefab.Capacity];
                _pools.Add(prefab, pool);
            }

            var instance = GetAvailable(pool, prefab);
            if (instance == null) return default;

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.Play(withChildren, speed);
            return new EffectHandle(instance);
        }

        private ParticleGroup GetAvailable(ParticleGroup[] pool, ParticleGroup prefab)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null)
                {
                    pool[i] = Instantiate(prefab, transform);
                    return pool[i];
                }

                if (!pool[i].IsPlaying) return pool[i];
            }

            return null;
        }
    }
}
