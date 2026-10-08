using UnityEngine;

namespace Game.BossContent
{
    /// 공용 BossAoeField가 실제 폭발 신호로 켜는 TFW 이펙트에만 붙이는 표시 연결.
    [DisallowMultipleComponent]
    public sealed class TfwIaidoExplosionVisual : MonoBehaviour
    {
        private void OnEnable()
        {
            var field = GetComponentInParent<TfwIaidoFieldLink>();
            if (field != null) field.ExplosionEnabled(this);
        }

        internal void PrimeParticles()
        {
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            {
                particles.Simulate(0.001f, false, true, false);
                particles.Play(false);
            }
        }
    }
}
