using UnityEngine;

namespace Game.BossContent
{
    /// 몸 오라 셰이더(TFW_BodyAura)에 보스의 월드 속도를 넘긴다.
    ///
    /// 셰이더는 이 값으로 껍질을 진행 반대쪽으로 끌고, 연기 무늬를 진행축으로 늘인다.
    /// 즉 서 있으면 몸에 붙은 먼지, 움직이면 뒤로 끌리는 꼬리가 된다.
    ///
    /// 순수 연출이다 — 상태를 바꾸지 않으므로 서버/클라 구분 없이 각자 로컬에서 돌린다.
    /// 값은 MaterialPropertyBlock으로 넣는다. 머티리얼 에셋을 건드리면 이 보스가 여럿일 때
    /// 서로의 속도를 덮어쓴다.
    [DisallowMultipleComponent]
    public class BossAuraVelocity : MonoBehaviour
    {
        private static readonly int VelocityId = Shader.PropertyToID("_TFW_Velocity");
        private static readonly int AuraColorId = Shader.PropertyToID("_AuraColor");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int FringeColorId = Shader.PropertyToID("_FringeColor");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int RiseSpeedId = Shader.PropertyToID("_RiseSpeed");
        private static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");
        private static readonly int EmberRateId = Shader.PropertyToID("_EmberRate");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int ShellHeightId = Shader.PropertyToID("_ShellHeight");
        private static readonly int TrailLengthId = Shader.PropertyToID("_TrailLength");
        private static readonly int TrailStretchId = Shader.PropertyToID("_TrailStretch");
        private static readonly int RedColorId = Shader.PropertyToID("_RedColor");
        private static readonly int BlackColorId = Shader.PropertyToID("_BlackColor");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int ErosionId = Shader.PropertyToID("_Erosion");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int RedAmountId = Shader.PropertyToID("_RedAmount");

        private const int SwordShieldPhase = 4;

        /// 세팅 툴이 만드는 오라 렌더러의 이름. 인스펙터가 비었을 때 이 이름으로 찾는다.
        public const string AuraObjectName = "FX_TFW_BodyAura";

        [Tooltip("오라 머티리얼을 쓰는 렌더러들. 비워두면 자식에서 이름으로 찾는다.")]
        [SerializeField] private Renderer[] _auraRenderers;

        [SerializeField] private BossNetState _netState;
        [SerializeField, Min(0.01f)] private float _phaseBlendSeconds = 0.65f;

        [Tooltip("속도 응답 시간(초). 낮을수록 꼬리가 즉각 반응하고, 높을수록 늘어진다.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _smoothing = 0.12f;

        private MaterialPropertyBlock _block;
        private Vector3 _lastPosition;
        private Vector3 _velocity;
        private float _phaseBlend;
        private Renderer[] _outerAuraRenderers = System.Array.Empty<Renderer>();
        private ParticleBaseline[] _outerParticles = System.Array.Empty<ParticleBaseline>();

        private readonly struct ParticleBaseline
        {
            public readonly ParticleSystem System;
            public readonly float Emission;
            public readonly float Lifetime;
            public readonly float Size;
            public readonly float Noise;

            public ParticleBaseline(ParticleSystem system)
            {
                System = system;
                Emission = system.emission.rateOverTimeMultiplier;
                Lifetime = system.main.startLifetimeMultiplier;
                Size = system.main.startSizeMultiplier;
                Noise = system.noise.strengthMultiplier;
            }
        }

        private void Awake()
        {
            if (_netState == null) _netState = GetComponent<BossNetState>();
            // 프리팹의 직렬화 목록보다 실제 자식 구조를 기준으로 삼는다. 세팅 도구를
            // 반복 실행한 구버전 프리팹에는 같은 렌더러가 중복 저장될 수 있다.
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.name == AuraObjectName) found.Add(r);
            _auraRenderers = found.ToArray();

            Transform outerRoot = FindChild(transform, "FX_TFW_OuterAura");
            if (outerRoot != null)
            {
                _outerAuraRenderers = outerRoot.GetComponentsInChildren<Renderer>(true);
                ParticleSystem[] particles = outerRoot.GetComponentsInChildren<ParticleSystem>(true);
                _outerParticles = new ParticleBaseline[particles.Length];
                for (int i = 0; i < particles.Length; i++)
                    _outerParticles[i] = new ParticleBaseline(particles[i]);
            }

            _block = new MaterialPropertyBlock();
            _lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            // guardrail:allow RNG — 로컬 오라 보간 전용. 네트워크 상태·판정에는 사용하지 않는다.
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 position = transform.position;
            Vector3 raw = (position - _lastPosition) / dt;
            _lastPosition = position;

            // 텔레포트·리스폰으로 한 프레임에 확 튀는 값은 꼬리를 화면 밖까지 늘린다.
            if (raw.sqrMagnitude > 400f) raw = Vector3.zero;

            _velocity = Vector3.Lerp(_velocity, raw, 1f - Mathf.Exp(-dt / _smoothing));

            float targetBlend = _netState != null && _netState.CurrentPhase >= SwordShieldPhase ? 1f : 0f;
            _phaseBlend = Mathf.MoveTowards(_phaseBlend, targetBlend, dt / Mathf.Max(0.01f, _phaseBlendSeconds));
            float phaseWeight = _phaseBlend * _phaseBlend * (3f - 2f * _phaseBlend);

            var value = new Vector4(_velocity.x, _velocity.y, _velocity.z, 0f);
            for (int i = 0; i < _auraRenderers.Length; i++)
            {
                var r = _auraRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetVector(VelocityId, value);
                if (phaseWeight > 0f) ApplyBodyPhase(r.sharedMaterial, phaseWeight);
                r.SetPropertyBlock(_block);
            }

            if (phaseWeight > 0f)
            {
                ApplyOuterAuraPhase(phaseWeight);
                ApplyOuterParticlePhase(phaseWeight);
            }
        }

        private void ApplyBodyPhase(Material material, float weight)
        {
            if (material == null) return;

            SetColorFromMaterial(material, AuraColorId, new Color(0.008f, 0.00005f, 0.00005f, 1f), weight);
            SetColorFromMaterial(material, DeepColorId, new Color(0.00003f, 0f, 0f, 1f), weight);
            SetColorFromMaterial(material, CoreColorId, new Color(1.35f, 0.002f, 0.001f, 1f), weight);
            SetColorFromMaterial(material, FringeColorId, new Color(0.015f, 0.0001f, 0.00005f, 1f), weight);
            SetFloatFromMaterial(material, GlowId, 0.62f, weight);
            SetFloatFromMaterial(material, RimWidthId, 0.42f, weight);
            SetFloatFromMaterial(material, NoiseScaleId, 4.6f, weight);
            SetFloatFromMaterial(material, RiseSpeedId, 0.75f, weight);
            SetFloatFromMaterial(material, TurbulenceId, 1.55f, weight);
            SetFloatFromMaterial(material, EmberRateId, 0.35f, weight);
            SetFloatFromMaterial(material, PulseId, 0.12f, weight);
            SetFloatFromMaterial(material, ShellHeightId, 0.05f, weight);
            SetFloatFromMaterial(material, TrailLengthId, 0.42f, weight);
            SetFloatFromMaterial(material, TrailStretchId, 4.2f, weight);
        }

        private void ApplyOuterAuraPhase(float weight)
        {
            for (int i = 0; i < _outerAuraRenderers.Length; i++)
            {
                Renderer renderer = _outerAuraRenderers[i];
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || !material.HasProperty(DensityId)) continue;

                renderer.GetPropertyBlock(_block);
                SetColorFromMaterial(material, BlackColorId, new Color(0.00004f, 0f, 0f, 1f), weight);
                SetColorFromMaterial(material, RedColorId, new Color(0.85f, 0.0015f, 0.0005f, 1f), weight);
                SetFloatFromMaterial(material, DensityId, 1.25f, weight);
                SetFloatFromMaterial(material, ErosionId, 0.3f, weight);
                SetFloatFromMaterial(material, NoiseScaleId, 5.4f, weight);
                SetFloatFromMaterial(material, FlowSpeedId, 0.82f, weight);
                SetFloatFromMaterial(material, RedAmountId, 0.12f, weight);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void ApplyOuterParticlePhase(float weight)
        {
            for (int i = 0; i < _outerParticles.Length; i++)
            {
                ParticleBaseline baseline = _outerParticles[i];
                if (baseline.System == null) continue;

                var emission = baseline.System.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(baseline.Emission, baseline.Emission * 1.25f, weight);

                var main = baseline.System.main;
                main.startLifetimeMultiplier = Mathf.Lerp(baseline.Lifetime, baseline.Lifetime * 1.1f, weight);
                main.startSizeMultiplier = Mathf.Lerp(baseline.Size, baseline.Size * 1.12f, weight);

                var noise = baseline.System.noise;
                if (noise.enabled)
                    noise.strengthMultiplier = Mathf.Lerp(baseline.Noise, baseline.Noise * 1.25f, weight);
            }
        }

        private void SetColorFromMaterial(Material material, int propertyId, Color target, float weight)
        {
            if (material.HasProperty(propertyId))
                _block.SetColor(propertyId, Color.Lerp(material.GetColor(propertyId), target, weight));
        }

        private void SetFloatFromMaterial(Material material, int propertyId, float target, float weight)
        {
            if (material.HasProperty(propertyId))
                _block.SetFloat(propertyId, Mathf.Lerp(material.GetFloat(propertyId), target, weight));
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName) return child;
                Transform nested = FindChild(child, childName);
                if (nested != null) return nested;
            }

            return null;
        }
    }
}
