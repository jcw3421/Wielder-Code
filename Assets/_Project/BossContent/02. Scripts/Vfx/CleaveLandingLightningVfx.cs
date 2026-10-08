using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.BossContent.Vfx
{
    /// <summary>
    /// Cleave 착지 순간에만 재생되는 로컬 표현 컴포넌트.
    /// 카메라를 향하는 LineRenderer 리본 메시를 겹쳐 지면 균열과 수직 번개를 만든다.
    /// 전투 판정이나 네트워크 상태는 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CleaveLandingLightningVfx : MonoBehaviour
    {
        private const int GroundArcCount = 11;
        private const int SkyArcCount = 5;
        private const int GroundPointCount = 10;
        private const int SkyPointCount = 12;

        [Header("Rendering")]
        [SerializeField] private Material _glowMaterial;
        [SerializeField] private Material _coreMaterial;
        [SerializeField] private Material _decalMaterial;
        [ColorUsage(true, true)]
        [SerializeField] private Color _glowColor = new(0.02f, 0.35f, 3.8f, 0.9f);
        [ColorUsage(true, true)]
        [SerializeField] private Color _coreColor = new(0.55f, 0.92f, 4.5f, 1f);

        [Header("Shape")]
        [Min(0.1f)]
        [SerializeField] private float _radius = 9f;
        [Min(0.1f)]
        [SerializeField] private float _skyHeight = 10.5f;
        [Min(0f)]
        [SerializeField] private float _groundOffset = 0.09f;
        [SerializeField] private LayerMask _groundLayers = (1 << 9) | (1 << 12);
        [Min(0.1f)]
        [SerializeField] private float _projectionStartHeight = 1.5f;
        [Min(0.1f)]
        [SerializeField] private float _projectionDepth = 4f;
        [Min(0.1f)]
        [SerializeField] private float _decalSize = 18f;
        [Range(2, 33)]
        [SerializeField] private int _decalGridResolution = 17;
        [Min(0.01f)]
        [SerializeField] private float _glowWidth = 0.51f;
        [Min(0.005f)]
        [SerializeField] private float _coreWidth = 0.1125f;

        [Header("Timing")]
        [Min(0.05f)]
        [SerializeField] private float _lifetime = 0.82f;
        [Min(0.01f)]
        [SerializeField] private float _rebuildInterval = 0.035f;
        [SerializeField] private int _shapeSeed = 7127;

        private readonly List<Arc> _arcs = new(GroundArcCount + SkyArcCount);
        private float _age;
        private float _nextRebuildTime;
        private int _shapeFrame;
        private bool _finished;
        private Mesh _decalMesh;
        private MeshRenderer _decalRenderer;
        private MaterialPropertyBlock _decalProperties;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");

        private sealed class Arc
        {
            public LineRenderer Glow;
            public LineRenderer Core;
            public Vector3[] Points;
            public float Angle;
            public float Reach;
            public float Bend;
            public bool IsSky;
        }

        private void Awake()
        {
            BuildRenderers();
            BuildGroundDecal();
        }

        private void OnEnable()
        {
            if (_arcs.Count == 0)
                BuildRenderers();
            if (_decalMesh == null)
                BuildGroundDecal();

            _age = 0f;
            _shapeFrame = 0;
            _nextRebuildTime = 0f;
            _finished = false;
            SetRenderersEnabled(true);
            if (_decalRenderer != null)
                _decalRenderer.enabled = _decalMaterial != null;
            ApplyDecalFade(1f);
            RebuildArcs();
        }

        private void Update()
        {
            if (_finished)
                return;

            _age += Time.deltaTime;   // guardrail:allow RNG — 로컬 연출 타이밍. 판정은 서버 틱이 잰다
            if (_age >= _lifetime)
            {
                SetRenderersEnabled(false);
                ApplyDecalFade(0f);
                if (_decalRenderer != null)
                    _decalRenderer.enabled = false;
                _finished = true;
                return;
            }

            if (_age >= _nextRebuildTime)
            {
                RebuildArcs();
                _nextRebuildTime = _age + _rebuildInterval;
            }

            float normalizedAge = Mathf.Clamp01(_age / _lifetime);
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedAge / 0.055f));
            float decay = 1f - Mathf.SmoothStep(0.22f, 1f, normalizedAge);
            float pulse = 0.72f + Mathf.Sin(_age * 71f) * 0.28f;
            ApplyIntensity(attack * decay * pulse);
            ApplyDecalFade(1f - Mathf.SmoothStep(0.18f, 1f, normalizedAge));
        }

        private void OnDisable()
        {
            SetRenderersEnabled(false);
            if (_decalRenderer != null)
                _decalRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_decalMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(_decalMesh);
            else
                DestroyImmediate(_decalMesh);
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(0.1f, _radius);
            _skyHeight = Mathf.Max(0.1f, _skyHeight);
            _groundOffset = Mathf.Max(0f, _groundOffset);
            _projectionStartHeight = Mathf.Max(0.1f, _projectionStartHeight);
            _projectionDepth = Mathf.Max(0.1f, _projectionDepth);
            _decalSize = Mathf.Max(0.1f, _decalSize);
            _decalGridResolution = Mathf.Clamp(_decalGridResolution, 2, 33);
            _glowWidth = Mathf.Max(0.01f, _glowWidth);
            _coreWidth = Mathf.Clamp(_coreWidth, 0.005f, _glowWidth);
            _lifetime = Mathf.Max(0.05f, _lifetime);
            _rebuildInterval = Mathf.Max(0.01f, _rebuildInterval);
        }

        private void BuildRenderers()
        {
            if (_arcs.Count > 0)
                return;

            for (int i = 0; i < GroundArcCount; i++)
            {
                float angle = (360f / GroundArcCount) * i + Hash01(i, 1) * 17f;
                float reach = Mathf.Lerp(0.74f, 0.98f, Hash01(i, 2));
                _arcs.Add(CreateArc($"GroundBolt_{i:00}", GroundPointCount, angle, reach, false, i));
            }

            for (int i = 0; i < SkyArcCount; i++)
            {
                float angle = (360f / SkyArcCount) * i + Hash01(i, 11) * 32f;
                float reach = Mathf.Lerp(0.34f, 0.7f, Hash01(i, 12));
                _arcs.Add(CreateArc($"SkyRibbon_{i:00}", SkyPointCount, angle, reach, true, i + 100));
            }
        }

        private Arc CreateArc(string objectName, int pointCount, float angle, float reach, bool isSky, int salt)
        {
            var holder = new GameObject(objectName);
            holder.transform.SetParent(transform, false);

            var glow = CreateLine(holder, "Glow", _glowMaterial, _glowWidth, _glowColor, pointCount, 0);
            var core = CreateLine(holder, "Core", _coreMaterial != null ? _coreMaterial : _glowMaterial,
                _coreWidth, _coreColor, pointCount, 1);

            return new Arc
            {
                Glow = glow,
                Core = core,
                Points = new Vector3[pointCount],
                Angle = angle,
                Reach = reach,
                Bend = Mathf.Lerp(-1f, 1f, Hash01(salt, 21)),
                IsSky = isSky
            };
        }

        private void BuildGroundDecal()
        {
            if (_decalMesh != null)
                return;

            var decalObject = new GameObject("GroundConformingDecal");
            decalObject.transform.SetParent(transform, false);
            var filter = decalObject.AddComponent<MeshFilter>();
            _decalRenderer = decalObject.AddComponent<MeshRenderer>();
            _decalRenderer.sharedMaterial = _decalMaterial;
            _decalRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _decalRenderer.receiveShadows = false;
            _decalRenderer.lightProbeUsage = LightProbeUsage.Off;
            _decalRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _decalRenderer.enabled = _decalMaterial != null;

            int resolution = _decalGridResolution;
            var vertices = new Vector3[resolution * resolution];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
            float halfSize = _decalSize * 0.5f;

            for (int z = 0; z < resolution; z++)
            {
                float v = z / (resolution - 1f);
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (resolution - 1f);
                    int vertexIndex = z * resolution + x;
                    Vector3 localPoint = new(Mathf.Lerp(-halfSize, halfSize, u), 0f,
                        Mathf.Lerp(-halfSize, halfSize, v));
                    ProjectDecalVertex(localPoint, out vertices[vertexIndex], out normals[vertexIndex]);
                    uv[vertexIndex] = new Vector2(u, v);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < resolution - 1; z++)
            {
                for (int x = 0; x < resolution - 1; x++)
                {
                    int a = z * resolution + x;
                    int b = a + 1;
                    int c = a + resolution;
                    int d = c + 1;
                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            _decalMesh = new Mesh { name = "TFW_CleaveLanding_GroundDecal_Runtime" };
            _decalMesh.MarkDynamic();
            _decalMesh.vertices = vertices;
            _decalMesh.normals = normals;
            _decalMesh.uv = uv;
            _decalMesh.triangles = triangles;
            _decalMesh.RecalculateBounds();
            filter.sharedMesh = _decalMesh;
            _decalProperties = new MaterialPropertyBlock();
        }

        private void ProjectDecalVertex(Vector3 localPoint, out Vector3 projectedPoint, out Vector3 projectedNormal)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 rayOrigin = worldPoint + Vector3.up * _projectionStartHeight;
            float rayDistance = _projectionStartHeight + _projectionDepth;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance,
                    _groundLayers, QueryTriggerInteraction.Ignore))
            {
                projectedPoint = transform.InverseTransformPoint(hit.point + hit.normal * _groundOffset);
                projectedNormal = transform.InverseTransformDirection(hit.normal).normalized;
                return;
            }

            projectedPoint = localPoint + Vector3.up * _groundOffset;
            projectedNormal = Vector3.up;
        }

        private void ApplyDecalFade(float fade)
        {
            if (_decalRenderer == null || _decalProperties == null)
                return;

            _decalRenderer.GetPropertyBlock(_decalProperties);
            _decalProperties.SetFloat(FadeId, Mathf.Clamp01(fade));
            _decalProperties.SetFloat(ProgressId, 1f);
            _decalRenderer.SetPropertyBlock(_decalProperties);
        }

        private static LineRenderer CreateLine(GameObject parent, string name, Material material, float width,
            Color color, int pointCount, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = pointCount;
            line.sharedMaterial = material;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.12f),
                new Keyframe(0.08f, 1f),
                new Keyframe(0.72f, 0.7f),
                new Keyframe(1f, 0f));
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private void RebuildArcs()
        {
            for (int arcIndex = 0; arcIndex < _arcs.Count; arcIndex++)
            {
                Arc arc = _arcs[arcIndex];
                if (arc.IsSky)
                    BuildSkyPoints(arc, arcIndex);
                else
                    BuildGroundPoints(arc, arcIndex);

                arc.Glow.SetPositions(arc.Points);
                arc.Core.SetPositions(arc.Points);
            }

            _shapeFrame++;
        }

        private void BuildGroundPoints(Arc arc, int arcIndex)
        {
            float radians = arc.Angle * Mathf.Deg2Rad;
            Vector3 forward = new(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 side = new(forward.z, 0f, -forward.x);
            float length = _radius * arc.Reach;

            for (int i = 0; i < arc.Points.Length; i++)
            {
                float t = i / (arc.Points.Length - 1f);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float jag = SignedHash(arcIndex, i, _shapeFrame) * _radius * 0.095f * envelope;
                float slowBend = arc.Bend * _radius * 0.12f * t * t;
                float lift = _groundOffset + Mathf.Abs(SignedHash(i, arcIndex, 7)) * 0.025f;
                Vector3 point = forward * Mathf.Lerp(_radius * 0.06f, length, t)
                    + side * (jag + slowBend);
                arc.Points[i] = ProjectToGround(point, lift);
            }
        }

        private Vector3 ProjectToGround(Vector3 localPoint, float offset)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 rayOrigin = worldPoint + Vector3.up * _projectionStartHeight;
            float rayDistance = _projectionStartHeight + _projectionDepth;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance,
                    _groundLayers, QueryTriggerInteraction.Ignore))
                return localPoint + Vector3.up * offset;

            return transform.InverseTransformPoint(hit.point + hit.normal * offset);
        }

        private void BuildSkyPoints(Arc arc, int arcIndex)
        {
            float radians = arc.Angle * Mathf.Deg2Rad;
            Vector3 outward = new(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
            Vector3 side = new(outward.z, 0f, -outward.x);
            Vector3 top = outward * (_radius * arc.Reach) + Vector3.up * _skyHeight;

            for (int i = 0; i < arc.Points.Length; i++)
            {
                float t = i / (arc.Points.Length - 1f);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float jagSide = SignedHash(arcIndex, i, _shapeFrame + 31) * _radius * 0.14f * envelope;
                float jagOut = SignedHash(i, arcIndex, _shapeFrame + 79) * _radius * 0.075f * envelope;
                Vector3 basePoint = Vector3.Lerp(Vector3.up * 0.18f, top, t);
                arc.Points[i] = basePoint + side * jagSide + outward * jagOut;
            }
        }

        private void ApplyIntensity(float intensity)
        {
            Color glow = _glowColor;
            Color core = _coreColor;
            glow.a *= intensity;
            core.a *= Mathf.Clamp01(intensity * 1.45f);

            for (int i = 0; i < _arcs.Count; i++)
            {
                Arc arc = _arcs[i];
                float skyScale = arc.IsSky ? 1.15f : 1f;
                arc.Glow.startColor = glow;
                arc.Glow.endColor = new Color(glow.r, glow.g, glow.b, 0f);
                arc.Glow.widthMultiplier = _glowWidth * intensity * skyScale;
                arc.Core.startColor = core;
                arc.Core.endColor = new Color(core.r, core.g, core.b, 0f);
                arc.Core.widthMultiplier = _coreWidth * intensity * skyScale;
            }
        }

        private void SetRenderersEnabled(bool value)
        {
            for (int i = 0; i < _arcs.Count; i++)
            {
                _arcs[i].Glow.enabled = value;
                _arcs[i].Core.enabled = value;
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
