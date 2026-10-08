using System.Collections.Generic;
using UnityEngine;

namespace Game.BossContent
{
    /// TFW 등장의 시그니처 — 착지 충격에 무기들이 공중으로 떠오르고, 포효에서 보스에게 모인다.
    ///
    /// 이 보스의 이름이 곧 연출의 근거다. "The First Wielder"는 최초로 무기를 든 자고,
    /// 프리팹에 실제 무기(대검·검·방패)가 페이즈별로 올려져 있다. 갑옷 보스는 투구 때문에
    /// 얼굴 클로즈업이 안 먹히므로, 정체성을 보여줄 물건이 얼굴 대신 무기여야 한다.
    ///
    /// 전부 로컬 연출이다 — 네트워크 상태를 만들지 않고, 서버가 복제하는 등장 박자를
    /// TfwBossEntrance가 읽어서 Rise/Converge를 부른다. 모든 피어가 같은 박자에 같은 그림을 만든다.
    ///
    /// 무기 원본을 통째로 복제하지 않고 메시와 머티리얼만 떠서 새 오브젝트에 얹는 이유:
    /// 프리팹의 무기에는 궤적 리본·검기 쿼드 같은 자식과 스크립트가 붙어 있는데,
    /// 그것까지 복제하면 사본이 보스를 참조한 채로 살아 움직인다.
    public class TfwEntranceWeaponRite : MonoBehaviour
    {
        /// 다른 FBX에서 가져온 무기 한 종. 모델 프리팹의 자식은 씬 오브젝트로 참조할 수 없어서
        /// (FBX 서브에셋이다) 메시와 머티리얼을 직접 받는다.
        [System.Serializable]
        public struct WeaponEntry
        {
            [Tooltip("인스펙터 식별용 — 로직에는 쓰이지 않는다.")]
            public string Label;
            public Mesh Mesh;
            [Tooltip("서브메시 순서대로. 비면 분홍색으로 나온다.")]
            public Material[] Materials;
        }

        [Header("원본 — 보스 프리팹에 이미 올려져 있는 무기들")]
        [Tooltip("복제해서 띄울 무기 오브젝트. 페이즈 전환으로 꺼져 있어도 메시는 읽히므로 " +
                 "대검·검·방패를 모두 꽂아 두면 종류가 섞여 더 풍성하다.")]
        [SerializeField] private GameObject[] _weaponSources;

        [Tooltip("바깥 FBX에서 빌려 온 무기들. 보스 무기가 3종뿐이라 이것만으로는 고리가 단조롭다 — " +
                 "에디터 툴이 P09_Weapon.fbx의 검·방패·지팡이·활 18종을 여기 채운다.")]
        [SerializeField] private WeaponEntry[] _meshWeapons;

        [Header("크기 — 출처가 달라도 한 화면에서 같은 무게로 읽히게 맞춘다")]
        [Tooltip("모든 무기의 최장축을 이 길이(m)로 맞춘다. 보스 무기와 바깥 무기는 원본 스케일이 " +
                 "제각각이라(대검 3.5배, FBX 원본 1배) 그대로 띄우면 크기가 뒤죽박죽이 된다. " +
                 "0이면 정규화하지 않고 원본 크기를 쓴다.")]
        [Min(0f)] [SerializeField] private float _targetLength = 2.7f;

        [Tooltip("자루마다 길이를 이만큼 엇갈리게 준다(m). 전부 같은 길이면 공산품처럼 보인다.")]
        [Min(0f)] [SerializeField] private float _lengthVariance = 0.55f;

        [Header("배치 — 실측 기준(보스 키 4.9m, 폭 5.4m)")]
        [Tooltip("띄울 자루 수. 무기 종류가 이보다 적으면 돌려 쓴다.")]
        [Min(1)] [SerializeField] private int _count = 18;

        [Tooltip("착지 지점에서 무기까지의 거리(m). 보스 폭이 5.4m라 그보다 넉넉히 밖에 둬야 " +
                 "몸에 겹치지 않는다.")]
        [Min(0.5f)] [SerializeField] private float _radius = 7.5f;

        [Tooltip("무기가 떠 있는 높이(m). 실측 가슴 높이가 3.6m다 — 그 언저리에 둬야 " +
                 "보스와 같은 프레임에 담긴다.")]
        [SerializeField] private float _height = 3.8f;

        [Tooltip("자루마다 높이를 이만큼 엇갈리게 준다(m). 0이면 전부 같은 높이라 기계적으로 보인다.")]
        [Min(0f)] [SerializeField] private float _heightVariance = 1.1f;

        [Tooltip("칼날을 세운 뒤 추가로 돌리는 각(도). 보통 0으로 둔다 — 무기마다 메시의 최장축을 " +
                 "찾아 자동으로 세우기 때문이다. 출처가 다른 FBX를 섞으면 칼날 축이 제각각이라 " +
                 "고정 각도로는 어떤 무기는 서고 어떤 무기는 눕는다.")]
        [SerializeField] private Vector3 _bladeEuler = Vector3.zero;

        [Tooltip("자루마다 기울기를 이만큼 엇갈리게 준다(도). 완전히 나란하면 울타리처럼 보인다.")]
        [Min(0f)] [SerializeField] private float _tiltVariance = 14f;

        [Header("등장 — 하나씩 올라와야 '소환'으로 읽힌다")]
        [Tooltip("자루 사이의 등장 간격(초). 전부 한 번에 뜨면 한 덩어리로 보이고 개수가 안 읽힌다. " +
                 "인덱스가 곧 고리 위의 각도라 이 간격이 고리를 한 바퀴 도는 물결을 만든다. " +
                 "간격 × 자루 수 + 등장 시간이 착지 구간(1.05초)보다 길면 뒤쪽 자루는 못 뜨고 넘어간다.")]
        [Min(0f)] [SerializeField] private float _riseInterval = 0.034f;

        [Tooltip("자루 하나가 다 올라오는 데 걸리는 시간(초).")]
        [Min(0.05f)] [SerializeField] private float _riseSeconds = 0.42f;

        [Tooltip("올라오기 시작하는 높이(m). 음수면 바닥을 뚫고 솟는다.")]
        [SerializeField] private float _riseFromHeight = -1.2f;

        [Header("떠 있는 동안")]
        [Tooltip("고리 전체가 보스를 중심으로 도는 속도(도/초). 느릴수록 무겁다.")]
        [SerializeField] private float _orbitDegreesPerSecond = 16f;

        [Tooltip("자루가 제자리에서 도는 속도(도/초).")]
        [SerializeField] private float _spinDegreesPerSecond = 34f;

        [Tooltip("위아래로 흔들리는 폭(m).")]
        [Min(0f)] [SerializeField] private float _bobAmplitude = 0.22f;

        [Tooltip("위아래 한 번 왕복에 걸리는 시간(초).")]
        [Min(0.1f)] [SerializeField] private float _bobSeconds = 2.4f;

        [Header("수렴 — 포효에서 보스에게 돌아간다")]
        [Tooltip("무기가 빨려 들어갈 목표. 비우면 보스의 가슴 높이로 모인다.")]
        [SerializeField] private Transform _convergeTarget;

        [Tooltip("빨려 들어가는 데 걸리는 시간(초).")]
        [Min(0.05f)] [SerializeField] private float _convergeSeconds = 0.5f;

        [Tooltip("수렴 목표를 비웠을 때 쓰는 높이(m). 실측 가슴 3.6m.")]
        [SerializeField] private float _convergeHeight = 3.6f;

        /// 떠 있는 자루 한 개.
        private sealed class Blade
        {
            public Transform Transform;
            public Vector3 BasePosition;     // 고리 위의 제자리(착지 지점 기준 월드)
            public Quaternion BaseRotation;
            public Vector3 FullScale;
            /// 메시 무게중심이 원점에서 얼마나 벗어나 있는지(스케일 반영). 자루 끝에 피벗이 박힌
            /// 무기가 흔해서, 이걸 빼 주지 않으면 고리 위에서 제각각 삐뚤게 떠 있는다.
            public Vector3 CenterOffset;
            public float RiseAt;             // 등장 시작 시각(연출 시작 기준 초)
            public float BobPhase;
            /// 지난 프레임에 놓인 무게중심. 수렴이 여기서부터 출발한다 —
            /// 트랜스폼 위치를 쓰면 피벗 보정만큼 어긋난 자리에서 빨려 들어간다.
            public Vector3 Center;
            public Vector3 ConvergeFrom;     // 수렴이 시작된 순간의 자리
        }

        /// 띄울 수 있는 무기 한 종으로 정규화한 것. 보스 무기든 바깥 FBX든 여기로 모인다.
        private readonly struct Piece
        {
            public readonly Mesh Mesh;
            public readonly Material[] Materials;
            public readonly int Layer;

            public Piece(Mesh mesh, Material[] materials, int layer)
            {
                Mesh = mesh; Materials = materials; Layer = layer;
            }
        }

        private readonly List<Blade> _blades = new();
        private readonly List<Piece> _pool = new();
        private Vector3 _origin;
        private Quaternion _facing;
        private float _elapsed;
        private float _convergeElapsed = -1f;   // 음수 = 아직 수렴 전
        private bool _running;

        /// 착지 충격에 호출된다. 착지 지점을 중심으로 고리를 짜고 하나씩 솟게 한다.
        public void Rise(Vector3 groundPosition, Quaternion facing)
        {
            Clear();
            BuildPool();
            if (_pool.Count == 0) return;

            _origin = groundPosition;
            _facing = facing;
            _elapsed = 0f;
            _convergeElapsed = -1f;
            _running = true;

            for (int i = 0; i < _count; i++)
            {
                var blade = Build(i);
                if (blade != null) _blades.Add(blade);
            }
        }

        /// 두 갈래의 출처를 하나의 목록으로 모은다.
        ///
        /// 보스 무기는 프리팹 안의 오브젝트라 렌더러에서 메시를 꺼내면 되고,
        /// 바깥 무기는 FBX 서브에셋이라 메시·머티리얼을 직접 받는다. 어느 쪽이든
        /// 여기서부터는 "메시 + 머티리얼"일 뿐이라 뒤 코드가 출처를 몰라도 된다.
        private void BuildPool()
        {
            _pool.Clear();

            if (_weaponSources != null)
            {
                foreach (var source in _weaponSources)
                {
                    if (source == null) continue;

                    // 스킨드도 받는다 — 바인드 포즈 그대로 그려지는데, 공중에 멈춰 있는
                    // 무기에는 그게 정확히 원하는 그림이다.
                    var renderer = source.GetComponentInChildren<Renderer>(includeInactive: true);
                    Mesh mesh = MeshOf(renderer);
                    if (mesh == null) continue;

                    _pool.Add(new Piece(mesh, renderer.sharedMaterials, source.layer));
                }
            }

            if (_meshWeapons != null)
            {
                foreach (var entry in _meshWeapons)
                {
                    if (entry.Mesh == null) continue;
                    _pool.Add(new Piece(entry.Mesh, entry.Materials, gameObject.layer));
                }
            }
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer == null) return null;
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            return renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
        }

        /// 포효에 호출된다. 떠 있던 자루가 전부 보스에게 빨려 들어간다.
        public void Converge()
        {
            if (!_running || _convergeElapsed >= 0f) return;

            _convergeElapsed = 0f;
            foreach (var blade in _blades)
                blade.ConvergeFrom = blade.Center;
        }

        /// 사본을 모두 지운다. 등장이 끝나거나 보스가 사라질 때 반드시 지나야 한다 —
        /// 사본은 보스 밑에 달려 있지 않아서 보스가 사라져도 저 혼자 남는다.
        public void Clear()
        {
            foreach (var blade in _blades)
                if (blade.Transform != null) Destroy(blade.Transform.gameObject);

            _blades.Clear();
            _running = false;
            _convergeElapsed = -1f;
        }

        private void OnDestroy() => Clear();

        /// 고리 위 i번째 자루를 만든다. 변주는 전부 인덱스에서 뽑는다 —
        /// 난수를 쓰면 피어마다 그림이 달라지고, 같은 보스를 두 번 봤을 때 재현되지 않는다.
        private Blade Build(int index)
        {
            Piece piece = _pool[index % _pool.Count];

            var go = new GameObject("TFW_RiteBlade");
            go.layer = piece.Layer;
            go.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = piece.Materials;

            // 인덱스를 홀짝·3·4주기로 흩어 길이·높이·기울기에 변주를 준다.
            float lengthStep = _lengthVariance * ((index % 3) - 1f) * 0.5f;
            float heightStep = _heightVariance * ((index % 3) - 1f) * 0.5f;
            float tilt = _tiltVariance * ((index % 2 == 0) ? 1f : -1f) * ((index % 4) * 0.25f + 0.5f);

            float scale = ScaleFor(piece.Mesh, _targetLength + lengthStep);

            // 고리 위의 자리. 착지 지점 기준이라 보스가 어디에 소환돼도 같은 그림이 나온다.
            float turn = 360f / _count * index;
            Quaternion yaw = _facing * Quaternion.Euler(0f, turn, 0f);
            Vector3 outward = yaw * Vector3.forward;

            var blade = new Blade
            {
                Transform = go.transform,
                BasePosition = _origin + outward * _radius + Vector3.up * (_height + heightStep),
                // 안쪽부터: 최장축을 세우고 → 손보정 → 옆으로 기울이고 → 고리 방향으로 돌린다.
                BaseRotation = yaw * Quaternion.Euler(0f, 0f, tilt) *
                               Quaternion.Euler(_bladeEuler) * UprightFor(piece.Mesh),
                FullScale = Vector3.one * scale,
                CenterOffset = piece.Mesh.bounds.center * scale,
                RiseAt = _riseInterval * index,
                BobPhase = index * 0.7f,
            };

            // 첫 프레임에 제 크기로 튀어나오지 않도록 0에서 시작한다.
            // Place로 놓는 이유: 아직 안 솟은 자루도 Center가 채워져 있어야, 그 전에 수렴이
            // 걸렸을 때 월드 원점에서 날아오지 않는다.
            Place(blade, blade.BasePosition + Vector3.up * (_riseFromHeight - _height),
                  blade.BaseRotation, 0f);

            return blade;
        }

        /// 메시의 최장축이 위를 향하도록 세우는 회전.
        ///
        /// 무기는 길쭉하다 — 바운즈에서 가장 긴 축이 곧 칼날/자루의 방향이다. 이걸로 세우면
        /// 어느 FBX에서 가져온 무기든, 원본이 X로 눕혀져 있든 Z로 뻗어 있든 똑같이 선다.
        /// 방패처럼 길이 차이가 작은 물건도 어느 축이든 세로로 서기만 하면 그림이 성립한다.
        private static Quaternion UprightFor(Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;

            Vector3 longest = size.x >= size.y && size.x >= size.z ? Vector3.right
                            : size.y >= size.z ? Vector3.up
                            : Vector3.forward;

            return Quaternion.FromToRotation(longest, Vector3.up);
        }

        /// 최장축이 원하는 길이가 되도록 균일 배율을 낸다.
        ///
        /// 출처마다 원본 크기가 딴판이다 — 보스 대검은 프리팹에서 3.5배로 부풀려 쥐어져 있고,
        /// 빌려 온 FBX 무기는 사람 손 기준 1배다. 정규화하지 않고 한 고리에 섞으면
        /// 이쑤시개와 통나무가 나란히 뜬다.
        private float ScaleFor(Mesh mesh, float length)
        {
            if (_targetLength <= 0f) return 1f;   // 0이면 정규화하지 않는다는 뜻

            Vector3 size = mesh.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            return longest > 0.0001f ? Mathf.Max(0.01f, length) / longest : 1f;
        }

        private void Update()
        {
            if (!_running) return;

            // guardrail:allow RNG — 등장 연출 전용(Update). 서버 판정은 이 값을 쓰지 않는다.
            float dt = Time.deltaTime;
            _elapsed += dt;

            if (_convergeElapsed >= 0f)
            {
                TickConverge(dt);
                return;
            }

            // 고리 전체가 보스를 축으로 천천히 돈다.
            Quaternion orbit = Quaternion.Euler(0f, _orbitDegreesPerSecond * _elapsed, 0f);

            foreach (var blade in _blades)
            {
                float local = _elapsed - blade.RiseAt;
                if (local <= 0f) continue;

                // 솟아오르는 동안은 위치·크기를 함께 채운다. 다 차면 1로 붙어 있는다.
                float rise = Mathf.Clamp01(local / _riseSeconds);
                rise = 1f - (1f - rise) * (1f - rise);   // 끝에서 감속 — 등속이면 튀어나온 뒤 뚝 멈춘다

                Vector3 anchored = _origin + orbit * (blade.BasePosition - _origin);
                float from = _riseFromHeight - _height;
                float bob = _bobAmplitude *
                            Mathf.Sin((_elapsed / _bobSeconds + blade.BobPhase) * 2f * Mathf.PI);

                Vector3 center = anchored + Vector3.up * (Mathf.Lerp(from, 0f, rise) + bob * rise);
                Quaternion rotation = orbit * blade.BaseRotation *
                                      Quaternion.Euler(0f, _spinDegreesPerSecond * local, 0f);

                Place(blade, center, rotation, rise);
            }
        }

        /// 자루를 "메시의 무게중심이 center에 오도록" 놓는다.
        ///
        /// 트랜스폼 위치를 그대로 쓰면 안 되는 이유: 무기 피벗은 대개 자루 끝에 박혀 있어서
        /// 그 점을 고리 위에 맞추면 칼이 통째로 바깥으로 밀려 나간다. 출처가 여럿이라
        /// 피벗 관례도 제각각이니, 눈에 보이는 덩어리의 중심을 기준으로 놓는 게 유일하게 일관된다.
        private static void Place(Blade blade, Vector3 center, Quaternion rotation, float factor)
        {
            blade.Center = center;
            blade.Transform.localScale = blade.FullScale * factor;
            blade.Transform.SetPositionAndRotation(
                center - rotation * (blade.CenterOffset * factor), rotation);
        }

        /// 전부 보스 쪽으로 빨려 들며 줄어든다. 다 모이면 스스로 치운다.
        private void TickConverge(float dt)
        {
            _convergeElapsed += dt;

            float t = Mathf.Clamp01(_convergeElapsed / _convergeSeconds);
            float eased = t * t;   // 처음엔 머뭇거리다 끝에서 확 빨린다

            Vector3 target = _convergeTarget != null
                ? _convergeTarget.position
                : _origin + Vector3.up * _convergeHeight;

            foreach (var blade in _blades)
            {
                if (blade.Transform == null) continue;

                Quaternion rotation = blade.Transform.rotation *
                                      Quaternion.Euler(0f, _spinDegreesPerSecond * 4f * dt, 0f);

                Place(blade, Vector3.Lerp(blade.ConvergeFrom, target, eased), rotation, 1f - eased);
            }

            if (t >= 1f) Clear();
        }
    }
}
