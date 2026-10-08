using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.BossContent
{
    /// 서버가 보낸 판정 형상 안에서만 그리는 TFW 참격/폭발. 게임 판정에는 관여하지 않는다.
    public sealed class TfwImpactVfx : MonoBehaviour
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Renderer[] _renderers;
        private MaterialPropertyBlock _properties;
        private float _age;
        private bool _persistent;
        private int _shape;
        private Vector3 _dimensions;
        private static readonly int Age = Shader.PropertyToID("_Age");
        private static readonly int Layer = Shader.PropertyToID("_Layer");


        public static TfwImpactVfx CreateWarning(int shape, Vector3 center, Vector3 dimensions, Quaternion rotation, Material material)
        {
            var root=new GameObject("TFW_AttackWarning");root.transform.SetPositionAndRotation(center,rotation);
            var effect=root.AddComponent<TfwImpactVfx>();effect._persistent=true;effect._shape=shape;effect._dimensions=dimensions;
            effect.AddMesh(BuildFootprint(shape,dimensions),material,3);
            effect._renderers=root.GetComponentsInChildren<Renderer>();effect._properties=new MaterialPropertyBlock();
            effect.ApplyAge();effect.SetProgress(0);return effect;
        }
        public void SetProgress(float progress)
        {
            foreach(var renderer in _renderers)
            {
                renderer.GetPropertyBlock(_properties);_properties.SetFloat("_Progress",progress);renderer.SetPropertyBlock(_properties);
            }
        }

        private void AddMesh(Mesh mesh, Material material, int layer)
        {
            _meshes.Add(mesh);
            var child = new GameObject(layer == 0 ? "HitBoundaryAndAsh" : "WhiteCoreBlades");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock(); block.SetFloat(Layer, layer); renderer.SetPropertyBlock(block);
        }

        private void Update()
        {
            // guardrail:allow RNG — VFX 수명 계산(연출 전용). 판정은 서버 틱이 따로 잰다.
            _age += Time.deltaTime;
            if (!_persistent && _age >= .55f) { Destroy(gameObject); return; }
            ApplyAge();
        }

        private void ApplyAge()
        {
            if (_renderers == null) return;
            foreach (var renderer in _renderers)
            {
                renderer.GetPropertyBlock(_properties);
                _properties.SetFloat(Age, _persistent ? .08f : _age);
                _properties.SetFloat("_Box", _shape == 2 ? 1 : 0);
                _properties.SetFloat("_Fan", _shape == 3 ? 1 : 0);
                _properties.SetFloat("_Donut", _shape == 1 ? 1 : 0);
                _properties.SetVector("_Size", _dimensions);
                renderer.SetPropertyBlock(_properties);
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes) if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
        }

        // 0 sphere's equatorial section, 1 donut, 2 oriented box, 3 fan.
        // 모든 정점은 서버 판정 체적 안에 있다. 도넛의 안전지대는 메시 자체가 비어 있다.
        public static Mesh BuildFootprint(int shape, Vector3 d)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            if (shape == 2)
            {
                vertices.AddRange(new[] { new Vector3(-d.x, 0, -d.z), new Vector3(d.x, 0, -d.z), new Vector3(d.x, 0, d.z), new Vector3(-d.x, 0, d.z) });
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            }
            else
            {
                int segments = 96;
                float halfAngle = shape == 3 ? d.z : 180;
                float inner = shape == 1 ? d.y / Mathf.Cos(Mathf.PI / 96) : 0;
                for (int i = 0; i <= segments; i++)
                {
                    float fraction = (float)i / segments;
                    float angle = Mathf.Lerp(-halfAngle, halfAngle, fraction) * Mathf.Deg2Rad;
                    var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    vertices.Add(direction * inner); vertices.Add(direction * d.x);
                    uv.Add(new Vector2(fraction, 0)); uv.Add(new Vector2(fraction, 1));
                    if (i == segments) continue;
                    int n = i * 2; triangles.AddRange(new[] { n, n + 1, n + 3, n, n + 3, n + 2 });
                }
            }
            var mesh = new Mesh { name = "TFW_ExactHitSection" }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }

    }
}
