using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.BossContent.Vfx
{
    /// <summary>
    /// 칼등치기 타격 순간에 보스의 허리 높이에서 퍼지는 로컬 전기 스윕 연출.
    /// 전투 판정과 네트워크 상태는 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackhandElectricSweepVfx : MonoBehaviour
    {
        private const int RingPointCount = 44;
        private const int SweepPointCount = 18;
        private const int BranchCount = 7;
        private const int BranchPointCount = 6;

        [Header("Rendering")]
        [SerializeField] private Material _haloMaterial;
        [SerializeField] private Material _coreMaterial;
        [ColorUsage(true, true)]
        [SerializeField] private Color _haloColor = new(0.025f, 0.28f, 4.8f, 0.92f);
        [ColorUsage(true, true)]
        [SerializeField] private Color _coreColor = new(0.65f, 0.94f, 5.2f, 1f);

        [Header("Shape")]
        [Min(0.1f)]
        [SerializeField] private float _radius = 7f;
        [Range(270f, 360f)]
        [SerializeField] private float _arcDegrees = 330f;
        [Min(0f)]
        [SerializeField] private float _height = 1.65f;
        [Tooltip("프리팹 로컬 전방을 기준으로 칼등 이동 방향을 맞추는 각도")]
        [SerializeField] private float _directionYaw;
        [Min(0.01f)]
        [SerializeField] private float _ringHaloWidth = 0.48f;
        [Min(0.005f)]
        [SerializeField] private float _ringCoreWidth = 0.085f;
        [Min(0.01f)]
        [SerializeField] private float _sweepHaloWidth = 1.05f;
        [Min(0.005f)]
        [SerializeField] private float _sweepCoreWidth = 0.16f;
        [Min(0.01f)]
        [SerializeField] private float _branchHaloWidth = 0.24f;
        [Min(0.005f)]
        [SerializeField] private float _branchCoreWidth = 0.045f;

        [Header("Timing")]
        [Range(0.6f, 0.8f)]
        [SerializeField] private float _lifetime = 0.72f;
        [Min(0.01f)]
        [SerializeField] private float _flickerInterval = 0.035f;
        [SerializeField] private int _shapeSeed = 9341;

        private readonly List<LinePair> _branches = new(BranchCount);
        private LinePair _ring;
        private LinePair _sweep;
        private float _age;
        private float _nextFlicker;
        private int _shapeFrame;
        private bool _finished;

        private sealed class LinePair
        {
            public LineRenderer Halo;
            public LineRenderer Core;
            public Vector3[] Points;
            public float HaloWidth;
            public float CoreWidth;
        }

        private void Awake()
        {
            BuildRenderers();
        }

        private void OnEnable()
        {
            if (_ring == null)
                BuildRenderers();

            _age = 0f;
            _nextFlicker = 0f;
            _shapeFrame = 0;
            _finished = false;
            SetEnabled(true);
            RebuildShape(0.04f);
            ApplyIntensity(0f);
        }

        private void Update()
        {
            if (_finished)
                return;

            _age += Time.deltaTime;   // guardrail:allow RNG — 로컬 연출 타이밍. 판정은 서버 틱이 잰다
            if (_age >= _lifetime)
            {
                SetEnabled(false);
                _finished = true;
                return;
            }

            float normalizedAge = Mathf.Clamp01(_age / _lifetime);
            float expansion = Mathf.SmoothStep(0.04f, 1f, Mathf.Clamp01(normalizedAge / 0.22f));
            if (_age >= _nextFlicker)
            {
                RebuildShape(expansion);
                _nextFlicker = _age + _flickerInterval;
                _shapeFrame++;
            }

            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedAge / 0.045f));
            float decay = 1f - Mathf.SmoothStep(0.18f, 1f, normalizedAge);
            float flicker = 0.78f + Mathf.Sin(_age * 83f) * 0.22f;
            ApplyIntensity(attack * decay * flicker);
        }

        private void OnDisable()
        {
            SetEnabled(false);
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(0.1f, _radius);
            _height = Mathf.Max(0f, _height);
            _ringHaloWidth = Mathf.Max(0.01f, _ringHaloWidth);
            _ringCoreWidth = Mathf.Clamp(_ringCoreWidth, 0.005f, _ringHaloWidth);
            _sweepHaloWidth = Mathf.Max(0.01f, _sweepHaloWidth);
            _sweepCoreWidth = Mathf.Clamp(_sweepCoreWidth, 0.005f, _sweepHaloWidth);
            _branchHaloWidth = Mathf.Max(0.01f, _branchHaloWidth);
            _branchCoreWidth = Mathf.Clamp(_branchCoreWidth, 0.005f, _branchHaloWidth);
            _lifetime = Mathf.Clamp(_lifetime, 0.6f, 0.8f);
            _flickerInterval = Mathf.Max(0.01f, _flickerInterval);
        }

        private void BuildRenderers()
        {
            if (_ring != null)
                return;

            _ring = CreateLinePair("ElectricCrescent", RingPointCount, _ringHaloWidth, _ringCoreWidth, 0);
            _sweep = CreateLinePair("BackhandSweepRibbon", SweepPointCount, _sweepHaloWidth, _sweepCoreWidth, 2);

            for (int i = 0; i < BranchCount; i++)
            {
                _branches.Add(CreateLinePair($"BranchArc_{i:00}", BranchPointCount,
                    _branchHaloWidth, _branchCoreWidth, 4));
            }
        }

        private LinePair CreateLinePair(string objectName, int pointCount, float haloWidth, float coreWidth,
            int sortingOrder)
        {
            var holder = new GameObject(objectName);
            holder.transform.SetParent(transform, false);

            return new LinePair
            {
                Halo = CreateLine(holder, "Halo", _haloMaterial, haloWidth, pointCount, sortingOrder),
                Core = CreateLine(holder, "Core", _coreMaterial != null ? _coreMaterial : _haloMaterial,
                    coreWidth, pointCount, sortingOrder + 1),
                Points = new Vector3[pointCount],
                HaloWidth = haloWidth,
                CoreWidth = coreWidth
            };
        }

        private static LineRenderer CreateLine(GameObject parent, string name, Material material, float width,
            int pointCount, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = pointCount;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.05f),
                new Keyframe(0.06f, 0.85f),
                new Keyframe(0.36f, 1f),
                new Keyframe(0.84f, 0.72f),
                new Keyframe(1f, 0.02f));
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private void RebuildShape(float expansion)
        {
            float yaw = _directionYaw * Mathf.Deg2Rad;
            Vector3 forward = new(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 right = new(forward.z, 0f, -forward.x);
            BuildRing(expansion, forward, right);
            BuildSweep(expansion, forward, right);
            BuildBranches(expansion, forward, right);
        }

        private void BuildRing(float expansion, Vector3 forward, Vector3 right)
        {
            float startDegrees = -_arcDegrees * 0.5f;
            for (int i = 0; i < _ring.Points.Length; i++)
            {
                float t = i / (_ring.Points.Length - 1f);
                float angle = (startDegrees + _arcDegrees * t) * Mathf.Deg2Rad;
                float jag = SignedHash(i, _shapeFrame, 10) * _radius * 0.035f * Mathf.Sin(t * Mathf.PI);
                float radius = (_radius + jag) * expansion;
                float lift = Mathf.Sin(t * Mathf.PI * 5f) * 0.1f
                    + SignedHash(i, 41, _shapeFrame) * 0.12f;
                _ring.Points[i] = (forward * Mathf.Cos(angle) + right * Mathf.Sin(angle)) * radius
                    + Vector3.up * (_height + lift);
            }

            SetPositions(_ring);
        }

        private void BuildSweep(float expansion, Vector3 forward, Vector3 right)
        {
            for (int i = 0; i < _sweep.Points.Length; i++)
            {
                float t = i / (_sweep.Points.Length - 1f);
                float angle = Mathf.Lerp(-126f, 112f, t) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(_radius * 0.42f, _radius, Mathf.Sin(t * Mathf.PI * 0.5f));
                float bladeBulge = Mathf.Sin(t * Mathf.PI) * _radius * 0.16f;
                float jag = SignedHash(i, _shapeFrame, 70) * _radius * 0.025f;
                Vector3 radial = forward * Mathf.Cos(angle) + right * Mathf.Sin(angle);
                _sweep.Points[i] = radial * ((radius + bladeBulge + jag) * expansion)
                    + Vector3.up * (_height + 0.22f + Mathf.Sin(t * Mathf.PI) * 0.48f);
            }

            SetPositions(_sweep);
        }

        private void BuildBranches(float expansion, Vector3 forward, Vector3 right)
        {
            for (int branchIndex = 0; branchIndex < _branches.Count; branchIndex++)
            {
                LinePair branch = _branches[branchIndex];
                float anchorT = Mathf.Lerp(0.12f, 0.9f, Hash01(branchIndex, 17));
                float anchorAngle = Mathf.Lerp(-145f, 145f, anchorT) * Mathf.Deg2Rad;
                Vector3 radial = forward * Mathf.Cos(anchorAngle) + right * Mathf.Sin(anchorAngle);
                Vector3 tangent = -forward * Mathf.Sin(anchorAngle) + right * Mathf.Cos(anchorAngle);
                Vector3 anchor = radial * (_radius * expansion * Mathf.Lerp(0.68f, 0.98f,
                    Hash01(branchIndex, 29))) + Vector3.up * (_height + 0.1f);
                float branchLength = _radius * Mathf.Lerp(0.13f, 0.27f, Hash01(branchIndex, 33));
                float tangentSign = Hash01(branchIndex, 51) > 0.5f ? 1f : -1f;

                for (int i = 0; i < branch.Points.Length; i++)
                {
                    float t = i / (branch.Points.Length - 1f);
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    float sideJag = SignedHash(branchIndex, i, _shapeFrame) * branchLength * 0.28f * envelope;
                    branch.Points[i] = anchor
                        + (radial * t + tangent * (tangentSign * t * 0.58f)) * branchLength * expansion
                        + tangent * sideJag
                        + Vector3.up * (SignedHash(i, branchIndex, _shapeFrame + 90) * 0.2f * envelope);
                }

                SetPositions(branch);
            }
        }

        private static void SetPositions(LinePair pair)
        {
            pair.Halo.SetPositions(pair.Points);
            pair.Core.SetPositions(pair.Points);
        }

        private void ApplyIntensity(float intensity)
        {
            ApplyIntensity(_ring, intensity, 1f);
            ApplyIntensity(_sweep, intensity, 1.18f);
            for (int i = 0; i < _branches.Count; i++)
            {
                float branchPulse = 0.72f + Hash01(i, _shapeFrame + 120) * 0.28f;
                ApplyIntensity(_branches[i], intensity * branchPulse, 1f);
            }
        }

        private void ApplyIntensity(LinePair pair, float intensity, float widthScale)
        {
            Color halo = _haloColor;
            Color core = _coreColor;
            halo.a *= Mathf.Clamp01(intensity);
            core.a *= Mathf.Clamp01(intensity * 1.35f);
            pair.Halo.startColor = halo;
            pair.Halo.endColor = new Color(halo.r, halo.g, halo.b, 0f);
            pair.Core.startColor = core;
            pair.Core.endColor = new Color(core.r, core.g, core.b, 0f);
            pair.Halo.widthMultiplier = pair.HaloWidth * intensity * widthScale;
            pair.Core.widthMultiplier = pair.CoreWidth * intensity * widthScale;
        }

        private void SetEnabled(bool value)
        {
            if (_ring == null)
                return;

            _ring.Halo.enabled = value;
            _ring.Core.enabled = value;
            _sweep.Halo.enabled = value;
            _sweep.Core.enabled = value;
            for (int i = 0; i < _branches.Count; i++)
            {
                _branches[i].Halo.enabled = value;
                _branches[i].Core.enabled = value;
            }
        }

        private float Hash01(int a, int b)
        {
            uint value = (uint)(_shapeSeed + a * 374761393 + b * 668265263);
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value & 0x00ffffffu) / 16777215f;
        }

        private float SignedHash(int a, int b, int c)
        {
            uint value = (uint)(_shapeSeed + a * 374761393 + b * 668265263 + c * 2147483647);
            value = (value ^ (value >> 13)) * 1274126177u;
            return ((value & 0x00ffffffu) / 8388607.5f) - 1f;
        }
    }
}
