using System.Collections.Generic;
using Fusion;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat.Player
{
    /// <summary>로컬 플레이어 전용 쿼터뷰 카메라 리그. 순수 로컬 연출이라 네트워크 상태를
    /// 바꾸지 않는다(권한 규칙과 무관). 입력 권한을 가진 내 아바타에서만 활성화되므로
    /// 4인 세션에서 각 피어는 자기 카메라 하나만 만든다.
    ///
    /// 카메라가 따라가는 타깃을 플레이어 위치가 아니라
    /// "플레이어 → 마우스 방향으로 일정 거리 끌어낸 지점(에임 앵커)"으로 두고,
    /// Cinemachine 의 PositionDamping 으로 쫀득하게 트레일시킨다.
    ///
    /// 사용법: 플레이어 프리팹에 이 컴포넌트를 붙인다. 씬의 MainCamera 는 런타임에
    /// CinemachineBrain 이 없으면 자동으로 추가한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCameraController : NetworkBehaviour
    {
        [Header("쿼터뷰 배치")]
        [Tooltip("타깃 기준 카메라의 월드 오프셋. (Y=높이, Z=뒤로 거리)")]
        [SerializeField] private Vector3 _followOffset = new Vector3(0f, 17f, -14f);
        [Tooltip("카메라가 내려다보는 각도(도). 오프셋과 일관되게 두면 타깃을 정조준한다.")]
        [SerializeField] private float _lookDownAngle = 50f;
        [Tooltip("직교 투영(쿼터뷰 룩) 사용 여부. 끄면 원근 투영.")]
        [SerializeField] private bool _orthographic = false;
        [Tooltip("원근 투영일 때 시야각(도). 낮을수록 망원(납작), 높을수록 광각.")]
        [SerializeField] private float _fieldOfView = 40f;

        [Header("휠 줌")]
        [Tooltip("마우스 휠 한 칸당 줌 배율 변화량.")]
        [SerializeField] private float _zoomStep = 0.08f;
        [Tooltip("최대 확대(가까이) 배율. 팔로우 오프셋 배율의 하한.")]
        [SerializeField] private float _minZoom = 0.5f;
        [Tooltip("최대 축소(멀리) 배율. 팔로우 오프셋 배율의 상한.")]
        [SerializeField] private float _maxZoom = 1.5f;
        [Tooltip("줌 보간 속도. 클수록 빠르게 붙는다.")]
        [SerializeField] private float _zoomLerpSpeed = 10f;

        [Header("에임 리드")]
        [Tooltip("마우스 방향으로 카메라가 끌려가는 최대 거리.")]
        [SerializeField] private float _aimLeadDistance = 0.5f;
        [Tooltip("클수록 천천히 따라온다(초 단위 근사). 0 이면 즉시.")]
        [SerializeField] private float _damping = 3f;

        private CinemachineCamera _vcam;
        private CinemachineFollow _follow;
        private Transform _aimAnchor;
        private Camera _brainCam;
        private bool _ownsCamera;
        private bool _warnedNoCamera;
        private float _zoom = 1f;        // 휠로 조절한 목표 줌 배율
        private float _currentZoom = 1f; // 실제 적용 중인 배율(보간)

        // 관전(로컬 전용): 내가 사망하면 살아있는 팀원을 따라간다. Tab으로 순환.
        private PlayerLifeCycle _selfLife;
        private PlayerLifeCycle _spectateLife;
        private readonly List<PlayerLifeCycle> _spectateCandidates = new();

        public override void Spawned()
        {
            // 내 입력 권한이 있는 아바타만 카메라를 가진다. (원격 플레이어 아바타는 무시)
            // 실제 생성은 MainCamera 가 준비된 뒤 LateUpdate 에서 한다 — 아바타가 카메라보다
            // 먼저 스폰되는 경쟁 상황(느린 씬 로딩)에서도 포기하지 않고 준비되면 만들기 위해서다.
            _ownsCamera = Object.HasInputAuthority;
            _selfLife = GetComponentInParent<PlayerLifeCycle>();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => TearDown();
        private void OnDestroy() => TearDown();

        private bool TryBuildRig()
        {
            _brainCam = Camera.main;
            if (_brainCam == null)
            {
                // 아직 메인 카메라가 없을 뿐 — 다음 프레임에 다시 시도한다(영구 실패 아님).
                if (!_warnedNoCamera)
                {
                    Debug.LogWarning("[PlayerCameraController] MainCamera 를 아직 못 찾음 — 준비되면 자동으로 카메라를 만든다. (MainCamera 태그 카메라가 씬에 있는지 확인)");
                    _warnedNoCamera = true;
                }
                return false;
            }
            if (_brainCam.GetComponent<CinemachineBrain>() == null)
                _brainCam.gameObject.AddComponent<CinemachineBrain>();
            _brainCam.orthographic = _orthographic;

            // 마우스 방향으로 끌리는 추적 앵커(월드 공간, 플레이어의 회전과 분리).
            _aimAnchor = new GameObject($"AimAnchor_{Object.InputAuthority}").transform;
            _aimAnchor.position = transform.position;

            // 가상 카메라.
            var go = new GameObject($"PlayerVCam_{Object.InputAuthority}");
            _vcam = go.AddComponent<CinemachineCamera>();
            _vcam.Priority = 10;
            _vcam.Follow = _aimAnchor;
            _vcam.transform.rotation = Quaternion.Euler(_lookDownAngle, 0f, 0f); // 고정 쿼터뷰 각

            // Cinemachine 렌즈 기본 FOV가 40이라 명시적으로 넣어야 인스펙터 값이 먹는다.
            var lens = _vcam.Lens;
            lens.FieldOfView = _fieldOfView;
            _vcam.Lens = lens;

            // 바디: 고정 오프셋 + 축별 댐핑. WorldSpace 바인딩으로 타깃 회전과 분리해 각도를 고정한다.
            _follow = go.AddComponent<CinemachineFollow>();
            _follow.FollowOffset = _followOffset;
            var tracker = _follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(_damping, _damping, _damping);
            _follow.TrackerSettings = tracker;

            // 보스 패턴 등이 요청하는 화면 흔들림. vcam에 붙어야 파이프라인 마지막에서 보정이 먹는다.
            go.AddComponent<CameraShaker>();
            return true;
        }

        private void TearDown()
        {
            if (_vcam != null) Destroy(_vcam.gameObject);
            if (_aimAnchor != null) Destroy(_aimAnchor.gameObject);
        }

        private void LateUpdate()
        {
            if (!_ownsCamera) return;
            // 리그가 아직 없으면 MainCamera 가 준비될 때까지 매 프레임 재시도. 준비되면 한 번만 만든다.
            if (_vcam == null && !TryBuildRig()) return;

            UpdateZoom();

            // 사망 중이면 살아있는 팀원을 관전한다(순수 로컬 연출 — 네트워크 권한 무관).
            if (_selfLife != null && _selfLife.IsDead)
            {
                UpdateSpectate();
            }
            else
            {
                _spectateLife = null;
                UpdateAimLead();
            }
        }
        // 마우스 휠로 카메라 거리(팔로우 오프셋 배율)를 조절한다. 순수 로컬 연출.
        private void UpdateZoom()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                    _zoom = Mathf.Clamp(_zoom - Mathf.Sign(scroll) * _zoomStep, _minZoom, _maxZoom);
            }

            // guardrail:allow RNG — 카메라 줌 보간(로컬 뷰 전용). 네트워크 상태를 만들지 않는다.
            _currentZoom = Mathf.Lerp(_currentZoom, _zoom, 1f - Mathf.Exp(-_zoomLerpSpeed * Time.deltaTime));
            _follow.FollowOffset = _followOffset * _currentZoom;
        }

        private void UpdateAimLead()
        {
            Vector3 playerPos = transform.position;
            Vector3 target = playerPos;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // 플레이어 높이의 수평면에 마우스 광선을 투영해 조준 지점을 구한다.
                var ground = new Plane(Vector3.up, playerPos);

                Ray ray = _brainCam.ScreenPointToRay(mouse.position.ReadValue());
                if (ground.Raycast(ray, out float enter))
                {
                    Vector3 lead = ray.GetPoint(enter) - playerPos;
                    lead.y = 0f;
                    target = playerPos + Vector3.ClampMagnitude(lead, _aimLeadDistance);
                }
            }

            _aimAnchor.position = target;
        }
        // 관전 대상 갱신: 대상이 없거나 무효(파괴/사망)면 새로 고르고, Tab 입력 시 다음 팀원으로 순환한다.
        private void UpdateSpectate()
        {
            // 관전자 사망 또는 퇴장
            if (_spectateLife == null || _spectateLife.IsDead)
                _spectateLife = PickLivingTeammate(null);

            // 관전자 변경 요청시
            else if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                _spectateLife = PickLivingTeammate(_spectateLife);

            // 관전할 팀원이 없으면 내 시체 위치를 비춘다.
            _aimAnchor.position = (_spectateLife != null ? _spectateLife.transform : transform).position;
        }

        // 살아있는 팀원 중 하나를 고른다. after 가 주어지면 그 다음(순환), null 이면 첫 번째.
        // 관전은 로컬 연출이라 씬을 직접 조회해도 무방(권한·동기화와 무관). 사망 전환/순환 시에만 호출.
        private PlayerLifeCycle PickLivingTeammate(PlayerLifeCycle after)
        {
            _spectateCandidates.Clear();
            foreach (var lc in FindObjectsByType<PlayerLifeCycle>())
            {
                if (lc == _selfLife) continue;   // 나 자신 제외
                if (lc.IsDead) continue;         // 죽은 팀원 제외
                _spectateCandidates.Add(lc);
            }
            if (_spectateCandidates.Count == 0) return null;
            if (after == null) return _spectateCandidates[0];
            int idx = _spectateCandidates.IndexOf(after);
            return _spectateCandidates[(idx + 1) % _spectateCandidates.Count];
        }
    }
}
