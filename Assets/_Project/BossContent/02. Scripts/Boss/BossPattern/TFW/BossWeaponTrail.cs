using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Game.BossContent
{
    /// 무기 궤적 리본.
    ///
    /// TrailRenderer는 **점 하나**의 궤적만 그린다. 칼끝에 달면 칼끝이 지나간 실 한 가닥이 나올 뿐이라
    /// "칼끝에서만 빛나는" 그림이 된다. 검이 쓸고 간 **면**을 그리려면 칼자루와 칼끝 두 점을
    /// 매 프레임 기록해 사각형을 이어붙여야 한다 — 그게 이 컴포넌트다.
    ///
    /// UV는 u = 나이(0=방금, 1=사라지기 직전), v = 칼날 방향(0=자루, 1=칼끝).
    /// TFW/SlashTrail 셰이더가 u로 꼬리를 죽이고 v로 두께를 잡는다.
    ///
    /// 순수 연출이라 모든 피어에서 로컬로 돈다. 상태는 읽기만 한다.
    public class BossWeaponTrail : NetworkBehaviour
    {
        [System.Serializable]
        public class Blade
        {
            [Tooltip("칼자루 쪽 기준점 — 보통 무기 트랜스폼 자신")]
            public Transform Root;
            [Tooltip("칼끝 기준점")]
            public Transform Tip;
        }

        [SerializeField] private BossNetState _netState;

        [Tooltip("무기별 칼자루·칼끝 쌍. 꺼져 있는 무기는 자동으로 건너뛴다.")]
        [SerializeField] private Blade[] _blades;

        [SerializeField] private Material _material;

        [Tooltip("리본이 남아 있는 시간. 길면 잔상이 지저분해진다.")]
        [SerializeField] private float _seconds = 0.38f;

        [Tooltip("리본 폭 배수. 칼자루~칼끝 그대로 그리면 무기 두께만큼만 나와 얇다. " +
                 "양 끝을 바깥으로 늘려 획을 두툼하게 만든다.")]
        [SerializeField] private float _widen = 1.45f;

        [Tooltip("칼끝 쪽으로 치우쳐 늘린다(0.5=양쪽 균등, 1=칼끝만). 획의 무게중심이 칼끝에 실린다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _tipBias = 0.75f;

        [Tooltip("이만큼 움직였을 때만 마디를 추가한다 — 제자리에서 마디가 쌓여 지저분해지는 걸 막는다.")]
        [SerializeField] private float _minStep = 0.05f;

        [Tooltip("Active가 끝난 뒤에도 이만큼 더 그린다 — 마지막 획이 뚝 끊기지 않게.")]
        [SerializeField] private float _tailSeconds = 0.12f;

        private const int MaxSegments = 96;

        private readonly List<Ribbon> _ribbons = new();
        private float _emitUntil;

        private class Ribbon
        {
            public Blade Blade;
            public Mesh Mesh;
            public MeshFilter Filter;
            public MeshRenderer Renderer;

            public readonly List<Vector3> RootPath = new();
            public readonly List<Vector3> TipPath = new();
            public readonly List<float> Born = new();
        }

        private void Reset() => _netState = GetComponent<BossNetState>();

        public override void Spawned()
        {
            if (_blades == null) return;

            foreach (var blade in _blades)
            {
                if (blade?.Root == null || blade.Tip == null) continue;

                // 리본은 월드 공간에 그린다. 보스가 회전해도 이미 그려진 획이 따라 돌면 안 되기 때문이다.
                var go = new GameObject($"{blade.Root.name}_Ribbon");
                go.transform.SetParent(transform, false);

                var mesh = new Mesh { name = go.name };
                mesh.MarkDynamic();

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                _ribbons.Add(new Ribbon
                {
                    Blade = blade, Mesh = mesh, Filter = filter, Renderer = renderer
                });
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            foreach (var r in _ribbons)
                if (r.Mesh != null) Destroy(r.Mesh);
            _ribbons.Clear();
        }

        /// Render에서 도는 이유는 애니메이션이 적용된 뒤의 본 위치를 읽어야 하기 때문이다.
        /// FixedUpdateNetwork에서 읽으면 틱 사이 보간이 빠져 궤적이 각져 보인다.
        public override void Render()
        {
            if (_netState == null) return;

            // 단계 값만 보면 안 된다 — 사망·무력화로 패턴이 끊겨도 그 값은 Active에 멈춰 있어
            // 시체가 디스폰까지 궤적을 남긴다(TfwPatternPhase 주석 참고).
            if (TfwPatternPhase.IsAttackActive(_netState))
                _emitUntil = Time.time + _tailSeconds;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다

            bool emitting = Time.time < _emitUntil;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다

            foreach (var ribbon in _ribbons)
                UpdateRibbon(ribbon, emitting);
        }

        private void UpdateRibbon(Ribbon r, bool emitting)
        {
            var blade = r.Blade;

            // 꺼진 무기(페이즈가 다른 쪽)는 궤적을 남기지 않는다.
            bool active = blade.Root != null && blade.Root.gameObject.activeInHierarchy;

            if (emitting && active)
            {
                Vector3 root = blade.Root.position;
                Vector3 tip = blade.Tip.position;

                bool far = r.TipPath.Count == 0 ||
                           (tip - r.TipPath[^1]).sqrMagnitude >= _minStep * _minStep;

                if (far)
                {
                    // 칼자루~칼끝 선분을 양 끝으로 늘려 획을 두툼하게 만든다.
                    // _tipBias로 늘어난 몫을 칼끝 쪽에 더 준다 — 원심력이 실린 것처럼 보인다.
                    Vector3 axis = tip - root;
                    float extra = (_widen - 1f);
                    if (extra > 0.0001f)
                    {
                        root -= axis * (extra * (1f - _tipBias));
                        tip  += axis * (extra * _tipBias);
                    }

                    r.RootPath.Add(root);
                    r.TipPath.Add(tip);
                    r.Born.Add(Time.time);   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다

                    if (r.TipPath.Count > MaxSegments)
                    {
                        r.RootPath.RemoveAt(0);
                        r.TipPath.RemoveAt(0);
                        r.Born.RemoveAt(0);
                    }
                }
            }

            // 수명이 지난 마디부터 앞에서 걷어낸다.
            while (r.Born.Count > 0 && Time.time - r.Born[0] > _seconds)   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다
            {
                r.RootPath.RemoveAt(0);
                r.TipPath.RemoveAt(0);
                r.Born.RemoveAt(0);
            }

            Rebuild(r);
        }

        private void Rebuild(Ribbon r)
        {
            int n = r.TipPath.Count;
            if (n < 2)
            {
                r.Renderer.enabled = false;
                r.Mesh.Clear();
                return;
            }

            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var colors = new Color[n * 2];
            var tris = new int[(n - 1) * 6];

            for (int i = 0; i < n; i++)
            {
                // 월드 좌표를 이 오브젝트 로컬로 되돌린다 — 보스가 움직여도 획은 제자리에 남는다.
                verts[i * 2] = transform.InverseTransformPoint(r.RootPath[i]);
                verts[i * 2 + 1] = transform.InverseTransformPoint(r.TipPath[i]);

                float age = Mathf.Clamp01((Time.time - r.Born[i]) / Mathf.Max(0.001f, _seconds));   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다
                uvs[i * 2] = new Vector2(age, 0f);
                uvs[i * 2 + 1] = new Vector2(age, 1f);

                var c = new Color(1f, 1f, 1f, 1f - age);
                colors[i * 2] = c;
                colors[i * 2 + 1] = c;
            }

            for (int i = 0; i < n - 1; i++)
            {
                int v = i * 2, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v + 2; tris[t + 4] = v + 1; tris[t + 5] = v + 3;
            }

            r.Mesh.Clear();
            r.Mesh.vertices = verts;
            r.Mesh.uv = uvs;
            r.Mesh.colors = colors;
            r.Mesh.triangles = tris;
            r.Mesh.RecalculateBounds();

            r.Renderer.enabled = true;
        }
    }
}
