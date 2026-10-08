using Fusion;
using Game.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Game.BossContent
{
    /// TFW 등장 연출 — 하늘에서 내려와 내리꽂히는 최종보스 등장.
    ///
    /// 공용 보스 코드(BossEntity·BossFSM·BossNetState·BossDefinition)를 한 줄도 건드리지 않는다.
    /// 자기 [Networked] 상태를 직접 들고, 보스 프리팹에 얹히기만 하면 동작한다.
    ///
    /// FSM에 훅을 걸지 않고 보스를 붙잡아 두는 방법:
    ///   BossIdleState는 DetectRange(기본 10m) 안에 플레이어가 있어야 교전으로 넘어간다.
    ///   등장의 긴 구간(예고·강하)에서 보스를 그보다 높은 곳에 두면 FSM은 스스로 대기에 머문다 —
    ///   막을 것도 되돌릴 것도 없다. 그래서 "길게 끄는" 연출은 전부 공중에서 한다.
    ///   착지 뒤 포효 구간은 Animator.Play로 스테이트를 물려 두어 애니가 바뀌지 않게 한다.
    ///
    /// 서버는 박자와 위치만 정하고, 이펙트·카메라·타임라인은 모든 피어가 각자 로컬로 돌린다.
    /// 컷신을 RPC로 뿌리지 않는 이유: RPC는 그 순간 접속해 있던 사람에게만 간다.
    /// 단계 값으로 두면 늦게 들어온 클라도 값만 읽고 그 박자부터 이어 붙는다.
    public class TfwBossEntrance : NetworkBehaviour
    {
        /// 값은 아래 큐 배열·카메라 키와 맺은 계약 — 순서/숫자를 바꾸면 연출이 어긋난다.
        private const int BeatNone = 0, BeatShadow = 1, BeatHover = 2,
                          BeatDive = 3, BeatImpact = 4, BeatRoar = 5;

        /// 카메라 무빙 한 지점. 착지 지점 기준 로컬값이라 소환 위치가 바뀌어도 그림이 유지된다.
        [System.Serializable]
        public struct CameraKey
        {
            [Tooltip("등장 시작 기준 초.")]
            public float AtSeconds;
            [Tooltip("보스 정면 기준 위치. z 음수가 보스 앞(카메라가 얼굴을 본다), y가 높이.")]
            public Vector3 Position;
            [Tooltip("체크하면 각도를 무시하고 보스를 계속 따라 본다. 강하처럼 보스 높이가 " +
                     "매 프레임 바뀌는 구간은 이걸 켜야 한다 — 고정 각도로는 프레임 밖으로 나간다.")]
            public bool LookAtBoss;
            [Tooltip("LookAtBoss가 꺼져 있을 때 쓰는 내려보는 각(도). 양수면 내려보고 음수면 올려본다.")]
            public float PitchDegrees;

            [Tooltip("체크하면 앞 키에서 이 키로 보간하지 않고 이 시점에 순간 전환한다(하드컷). " +
                     "한 대의 카메라가 처음부터 끝까지 미끄러지는 그림이 연출을 밋밋하게 만드는 " +
                     "가장 큰 원인이다 — 컷이 리듬을 만든다. 앞 키의 자세는 이 시점까지 유지된다.")]
            public bool Cut;

            [Tooltip("이 키의 시야각(도). 0이면 카메라에 설정된 값을 그대로 쓴다. " +
                     "낮을수록(35 안팎) 압축돼 웅장하고, 높을수록(70 안팎) 왜곡돼 가깝고 위압적이다.")]
            public float FieldOfView;

            [Tooltip("LookAtBoss가 켜졌을 때 이 키가 조준할 높이(m). 0이면 공용 LookAtHeight를 쓴다. " +
                     "실측: 발바닥 0 / 가슴 3.6 / 머리 4.1 / 투구 중심 4.4 / 투구 꼭대기 4.9.")]
            public float LookAtHeight;
        }

        [Header("타이밍 — 합이 곧 컷신 길이이자 플레이어가 묶이는 시간")]
        [Tooltip("정적(초). 보스는 아직 화면 밖 상공에 있다. 아레나 바닥이 먼저 보스의 것이 되는 구간 " +
                 "— ShadowCues가 여기서 터진다. 0이면 등장 연출을 끈다.")]
        [Min(0f)] [SerializeField] private float _shadowSeconds = 1.2f;

        [Tooltip("강하(초). 보스가 DiveHeight에서 HoverHeight까지 천천히 내려온다 — 여기가 '보여주는' 구간이다. " +
                 "길게 줘도 안전하다: 보스가 아직 공중이라 FSM이 깨어나지 않는다.")]
        [Min(0f)] [SerializeField] private float _hoverSeconds = 1.5f;

        [Tooltip("내리꽂기(초). 짧을수록 무겁게 떨어진다.")]
        [Min(0.05f)] [SerializeField] private float _diveSeconds = 0.45f;

        [Tooltip("착지 충격 유지(초). 무기가 하나씩 떠오르는 구간이라 WeaponRite의 등장 간격 × 자루보다 길어야 한다.")]
        [Min(0f)] [SerializeField] private float _impactHoldSeconds = 1.05f;

        [Tooltip("포효·발도(초). 이 구간은 보스가 이미 지상이라 FSM이 깨어 있다 — " +
                 "BossDefinition의 FirstEncounterDelaySeconds(TFW는 3.2)보다 짧게 잡아야 도중에 패턴이 끼어들지 않는다.")]
        [Min(0f)] [SerializeField] private float _roarSeconds = 1.2f;

        [Header("높이")]
        [Tooltip("등장을 시작하는 높이(m). 화면 밖이어야 하므로 넉넉히 준다.")]
        [Min(20f)] [SerializeField] private float _diveHeight = 28f;

        [Tooltip("강하가 끝나고 내리꽂기 직전에 머무는 높이(m). " +
                 "DetectRange(기본 10m)보다 높아야 이 구간에서 보스가 전투를 시작하지 않는다.")]
        [Min(12f)] [SerializeField] private float _hoverHeight = 13f;

        [Header("박자별 이펙트 — 그 박자에 들어가는 순간 한꺼번에 생성된다")]
        [Tooltip("정적. 보스가 오기 전에 바닥이 먼저 반응하는 구간 — 확장하는 링·역중력 파편. " +
                 "AttachToAnchor를 꺼야 착지 지점에 남는다(보스는 아직 28m 상공이다).")]
        [SerializeField] private PatternEffectCue[] _shadowCues;
        [Tooltip("강하 중. 보스에 붙는 기운. AttachToAnchor를 켠다.")]
        [SerializeField] private PatternEffectCue[] _hoverCues;
        [Tooltip("내리꽂기. 낙하 궤적. AttachToAnchor를 켠다.")]
        [SerializeField] private PatternEffectCue[] _diveCues;
        [Tooltip("착지 충격. 크레이터·균열·충격파. AttachToAnchor를 꺼야 바닥에 남는다.")]
        [SerializeField] private PatternEffectCue[] _impactCues;
        [Tooltip("포효. 보스에서 터지는 기운.")]
        [SerializeField] private PatternEffectCue[] _roarCues;

        [Header("시네마틱")]
        [Tooltip("등장 시작에 재생할 타임라인. 레터박스 같은 로컬 연출만 담는다 — " +
                 "보스 애니메이터를 건드리면 네트워크로 온 파라미터와 싸운다. 비워도 나머지는 동작한다.")]
        [SerializeField] private PlayableDirector _director;

        [Tooltip("등장 동안만 켜지는 전용 카메라. 플레이어 카메라보다 Depth를 높게 둬야 위에 그려진다.")]
        [SerializeField] private Camera _cinematicCamera;

        [Tooltip("등장 동안만 켜지는 컷신 전용 그레이딩(블룸·비네트 등). 게임플레이와 같은 룩으로 " +
                 "컷신을 돌리면 '연출'로 읽히지 않는다. 전역 볼륨이지만 이 구간엔 시네마틱 카메라가 " +
                 "화면을 덮고 있어 다른 카메라에 영향이 보이지 않는다. 비워도 나머지는 동작한다.")]
        [SerializeField] private Volume _cinematicVolume;

        [Tooltip("카메라 무빙. 리그가 보스 정면을 향해 착지 지점에 놓이므로, " +
                 "z 음수 = 보스 앞(얼굴이 보인다), z 양수 = 보스 뒤다.")]
        [SerializeField] private CameraKey[] _cameraKeys;

        [Tooltip("LookAtHeight가 0인 키가 조준할 기본 높이(m). 실측 기준 전신을 담기 좋은 높이는 " +
                 "가슴(3.6)보다 조금 아래다 — 프레임 위쪽에 여백이 남아야 큰 보스가 커 보인다.")]
        [Min(0f)] [SerializeField] private float _lookAtHeight = 3.2f;

        [Tooltip("마지막 이 시간 동안 플레이어 카메라 자리로 미끄러져 들어간다(초). " +
                 "이게 없으면 컷신이 끝나는 순간 화면이 툭 끊긴다 — 시네마틱 카메라는 보스 기준이고 " +
                 "플레이어 카메라는 플레이어 기준이라 위치가 전혀 다르기 때문이다. 0이면 즉시 전환.")]
        [Min(0f)] [SerializeField] private float _blendOutSeconds = 0.55f;

        [Header("착지 임팩트 — 셋이 같은 프레임에 터져야 '무겁다'가 된다")]
        [Tooltip("착지 순간 플레이어 카메라에 요청할 흔들림 세기. 컷신 동안엔 시네마틱 카메라가 " +
                 "위에 그려져 이 흔들림이 보이지 않으므로, 화면에 실제로 나타나는 건 아래 ImpactShakeAmplitude다. " +
                 "그래도 같이 쏘는 이유는 블렌드아웃으로 돌아간 뒤의 여진을 남기기 위해서다.")]
        [SerializeField] private CameraShakeStrength _impactShake = CameraShakeStrength.Heavy;

        [Tooltip("시네마틱 카메라 자체의 흔들림 진폭(m). 첫 프레임에 최대로 밀리고 반 주기마다 " +
                 "부호가 뒤집히며 잦아든다 — 진폭만 줄어드는 노이즈는 때린 느낌이 안 난다. " +
                 "0이면 시네마틱 카메라는 흔들리지 않는다.")]
        [Min(0f)] [SerializeField] private float _impactShakeAmplitude = 0.42f;

        [Tooltip("초당 진동 횟수. 높을수록 짧고 날카롭다.")]
        [Min(0.1f)] [SerializeField] private float _impactShakeFrequency = 13f;

        [Tooltip("클수록 빨리 잦아든다.")]
        [Min(0.1f)] [SerializeField] private float _impactShakeDamping = 9f;

        [Tooltip("착지 순간 카메라를 이만큼 멈춘다(초). Time.timeScale은 절대 쓰지 않는다 — " +
                 "네트워크 게임에서 시간을 늦추면 서버 틱과 어긋난다. 카메라만 멈춰도 눈에는 똑같이 보이고 " +
                 "판정에는 닿지 않는다. 0이면 히트스톱 없음.")]
        [Min(0f)] [SerializeField] private float _impactHitStopSeconds = 0.09f;

        [Tooltip("착지 순간 시야각이 순간적으로 넓어지는 정도(도). 확 밀려났다 돌아오는 맛을 만든다.")]
        [Min(0f)] [SerializeField] private float _impactFovKick = 13f;

        [Tooltip("넓어진 시야각이 제자리로 돌아오는 시간(초).")]
        [Min(0.01f)] [SerializeField] private float _impactFovRecoverSeconds = 0.38f;

        [Tooltip("착지 순간 화면을 덮는 흰 판. 시네마틱 카메라 앞에 붙은 쿼드를 꽂는다. " +
                 "비워도 나머지는 동작한다.")]
        [SerializeField] private Renderer _impactFlash;

        [Tooltip("흰 판이 사라지는 시간(초).")]
        [Min(0.01f)] [SerializeField] private float _impactFlashSeconds = 0.22f;

        [Header("무기 의식 — 이 보스의 시그니처")]
        [Tooltip("착지 직후 무기가 공중에 떠올랐다가 포효에서 손으로 모이는 연출. " +
                 "비워도 나머지는 동작한다.")]
        [SerializeField] private TfwEntranceWeaponRite _weaponRite;

        [Header("등장 애니메이션 — 컨트롤러의 스테이트를 이름으로 직접 재생한다")]
        [Tooltip("강하·내리꽂기에 재생할 스테이트. 공용 BossAnimState에 값을 추가하지 않으려고 " +
                 "파라미터 조건 대신 Animator.Play로 직접 건다. 비우면 애니는 그대로 둔다.")]
        [SerializeField] private string _fallStateName = "Entrance_Fall";

        [Tooltip("착지·포효에 재생할 스테이트. 여기에 꽂은 클립 길이가 포효 시간을 눈으로 채운다. " +
                 "이 스테이트에서 Locomotion으로 가는 전이(ExitTime)가 걸려 있어야 전투 애니로 복귀한다.")]
        [SerializeField] private string _landStateName = "Entrance_Land";

        // ── 네트워크 상태 (BossNetState를 건드리지 않으려고 여기서 직접 들고 있다) ──
        [Networked] private int Beat { get; set; }
        [Networked] private Vector3 GroundPosition { get; set; }
        /// 착지 시 보스가 바라볼 방향. 클라의 카메라 리그도 이 값으로 정면을 잡는다.
        [Networked] private Quaternion FacingRotation { get; set; }

        // ── 서버 전용 ──
        private int _remainingTicks;

        // ── 클라 전용 ──
        private const int NoBeatSeen = -1;
        private int _lastBeat = NoBeatSeen;
        private Transform _detachedRig;
        private Animator _animator;
        private int _fallStateHash, _landStateHash;
        /// 등장이 시작된 뒤 흐른 시간(초). 카메라 무빙만 쓰는 값이라 로컬 시간으로 잰다 —
        /// 서버가 재는 박자와 몇 프레임 어긋나도 그림이 달라질 뿐 판정에는 닿지 않는다.
        /// 히트스톱 동안에는 멈춘다.
        private float _cameraElapsed;
        /// 히트스톱과 무관하게 계속 흐르는 시간. 블렌드아웃이 이걸 쓴다 —
        /// 카메라 시간으로 재면 멈춘 만큼 늦어져서 컷신이 끝나는 순간 남은 블렌드가 툭 끊긴다.
        private float _realElapsed;
        private bool _cameraRunning;
        /// 컷신이 끝날 때 되돌아갈 목적지. PlayerCameraController가 Cinemachine 브레인을 얹는
        /// MainCamera가 곧 이것이다(시네마틱 카메라는 태그가 없어 여기 잡히지 않는다).
        private Camera _playerCamera;

        // ── 착지 임팩트 (클라 전용) ──
        /// 카메라 설계 시야각. 키가 FieldOfView를 비워 두면 이 값으로 돌아온다.
        private float _baseFov;
        private float _shakeElapsed = -1f;   // 음수 = 흔들림 없음
        private Vector2 _shakeDirection;
        private float _hitStopRemaining;
        private float _fovKickRemaining;
        private float _flashRemaining;
        private MaterialPropertyBlock _flashBlock;
        private static readonly int FlashColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlashColorLegacyId = Shader.PropertyToID("_Color");

        private bool HasEntrance => _shadowSeconds > 0f;

        private float TotalSeconds
            => _shadowSeconds + _hoverSeconds + _diveSeconds + _impactHoldSeconds + _roarSeconds;

        public override void Spawned()
        {
            _animator = GetComponent<Animator>();
            _fallStateHash = string.IsNullOrEmpty(_fallStateName) ? 0 : Animator.StringToHash(_fallStateName);
            _landStateHash = string.IsNullOrEmpty(_landStateName) ? 0 : Animator.StringToHash(_landStateName);

            if (_cinematicCamera != null)
            {
                _cinematicCamera.enabled = false;
                _baseFov = _cinematicCamera.fieldOfView;
            }
            if (_cinematicVolume != null) _cinematicVolume.enabled = false;
            SetFlashAlpha(0f);
            DetachRig();

            if (!HasStateAuthority || !HasEntrance) return;

            GroundPosition = transform.position;
            FacingRotation = ResolveFacing();
            transform.rotation = FacingRotation;   // 등을 보인 채 떨어지지 않게 시작부터 돌려 둔다

            LockPlayers();
            EnterBeat(BeatShadow, _shadowSeconds);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // 등장 도중 보스가 사라져도(호스트 마이그레이션·강제 종료) 카메라가 남으면
            // 플레이어가 영영 컷신 화면에 갇힌다. 반드시 여기를 지난다.
            if (_cinematicCamera != null) _cinematicCamera.enabled = false;
            if (_cinematicVolume != null) _cinematicVolume.enabled = false;
            if (_director != null) _director.Stop();
            // 리그를 지우면 그 자식인 흰 판도 함께 사라지지만, 무기 사본은 리그 밖에 살아서 남는다.
            if (_weaponRite != null) _weaponRite.Clear();
            if (_detachedRig != null) Destroy(_detachedRig.gameObject);
            _detachedRig = null;
        }

        /// 플레이어들 한가운데를 바라보는 회전. 씬의 소환 지점 회전은 대개 기본값이라
        /// 그대로 두면 보스가 아무 데나 보고 떨어진다 — 등을 보이고 등장하던 원인이다.
        private Quaternion ResolveFacing()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var obj) || obj == null) continue;
                sum += obj.transform.position;
                count++;
            }

            if (count == 0) return transform.rotation;   // 아무도 없으면 소환 회전 그대로

            Vector3 toPlayers = sum / count - transform.position;
            toPlayers.y = 0f;

            return toPlayers.sqrMagnitude < 0.01f ? transform.rotation
                                                  : Quaternion.LookRotation(toPlayers.normalized);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Beat == BeatNone) return;

            switch (Beat)
            {
                case BeatShadow: SetHeight(_diveHeight); break;
                case BeatHover:  TickHover();  break;
                case BeatDive:   TickDive();   break;
                // 착지·포효 구간은 위치를 고정해 FSM이 걸어 나가지 못하게 한다.
                case BeatImpact:
                case BeatRoar:   SetHeight(0f); break;
            }

            if (--_remainingTicks > 0) return;

            switch (Beat)
            {
                case BeatShadow: EnterBeat(BeatHover, _hoverSeconds); break;
                case BeatHover:  EnterBeat(BeatDive, _diveSeconds); break;
                case BeatDive:
                    SetHeight(0f);
                    EnterBeat(BeatImpact, _impactHoldSeconds);
                    break;
                case BeatImpact: EnterBeat(BeatRoar, _roarSeconds); break;
                case BeatRoar:   Beat = BeatNone; break;   // 여기서부터 FSM이 알아서 교전을 시작한다
            }
        }

        /// 등속으로 천천히 내려온다 — 여기가 보스를 '보여주는' 구간이라 급하면 안 된다.
        private void TickHover()
        {
            float remaining = Mathf.Clamp01((float)_remainingTicks / Mathf.Max(1, ToTicks(_hoverSeconds)));
            SetHeight(Mathf.Lerp(_hoverHeight, _diveHeight, remaining));
        }

        /// 남은 시간 비율의 제곱으로 내린다 — 등속이면 엘리베이터처럼 보인다.
        private void TickDive()
        {
            float remaining = Mathf.Clamp01((float)_remainingTicks / Mathf.Max(1, ToTicks(_diveSeconds)));
            SetHeight(_hoverHeight * remaining * remaining);
        }

        /// 착지 지점 기준 높이를 직접 놓는다. NCC.Move를 타지 않으므로 중력·접지 처리와 싸우지 않고,
        /// NetworkTransform이 그대로 복제한다. 등장이 끝나면 FSM의 첫 Move부터 정상 동작으로 돌아온다.
        private void SetHeight(float height)
        {
            Vector3 ground = GroundPosition;
            transform.position = new Vector3(ground.x, ground.y + height, ground.z);
        }

        private void EnterBeat(int beat, float seconds)
        {
            // 호출부(BeginEntrance/TickBeat)가 이미 막고 있지만, [Networked] 쓰기라 여기서도 잠근다.
            if (!HasStateAuthority) return;

            Beat = beat;
            _remainingTicks = Mathf.Max(1, ToTicks(seconds));
        }

        private int ToTicks(float seconds) => Mathf.RoundToInt(seconds * Runner.TickRate);

        /// 등장 내내 플레이어를 묶어 둔다.
        ///
        /// Combat 모듈을 직접 건드리지 않는다 — Core의 ICrowdControllable이 이미 열려 있는 통로고,
        /// 지속시간을 틱으로 명시할 수 있어 서버가 재는 등장 길이와 정확히 같게 걸린다.
        private void LockPlayers()
        {
            int ticks = ToTicks(TotalSeconds);
            if (ticks <= 0) return;

            foreach (var player in Runner.ActivePlayers)
            {
                if (!Runner.TryGetPlayerObject(player, out var obj) || obj == null) continue;
                if (obj.TryGetComponent<ICrowdControllable>(out var controllable))
                    controllable.ApplyCC(new CCInfo(CCType.Stun, ticks));
            }
        }

        // ── 연출 (모든 피어) ────────────────────────────────────────────
        public override void Render()
        {
            int beat = Beat;

            if (beat != _lastBeat)
            {
                bool firstLook = _lastBeat == NoBeatSeen;
                _lastBeat = beat;

                // 첫 Render에서 0이면 등장을 안 쓰거나 이미 끝난 보스다 — 걸 연출이 없다.
                if (!(firstLook && beat == BeatNone))
                {
                    ApplyCinematic(beat);
                    PlayEntranceAnimation(beat);
                    if (beat == BeatImpact) StartImpact();
                    PlayWeaponRite(beat);
                    PlayCues(CuesFor(beat));
                }
            }

            TickCamera();
        }

        /// 착지 한 프레임. 흔들림·히트스톱·시야각 킥·섬광이 여기서 동시에 걸린다 —
        /// 하나씩 흩어 놓으면 각각은 보여도 "맞았다"로 읽히지 않는다.
        private void StartImpact()
        {
            GameplayEvents.RaiseCameraShakeRequested(_impactShake);

            if (_impactShakeAmplitude > 0f)
            {
                _shakeElapsed = 0f;
                // 세로보다 가로가 넓게 흔들려야 쿼터뷰에서 자연스럽다.
                // guardrail:allow RNG — 로컬 카메라 연출 전용. 피어마다 방향이 달라도 게임 상태와 무관하다
                float angle = Random.Range(0f, Mathf.PI * 2f);
                _shakeDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f).normalized;
            }

            _hitStopRemaining = _impactHitStopSeconds;
            _fovKickRemaining = _impactFovKick > 0f ? _impactFovRecoverSeconds : 0f;
            _flashRemaining = _impactFlash != null ? _impactFlashSeconds : 0f;
        }

        /// 무기 의식은 착지에 떠오르고 포효에 모인다. 등장이 끝나거나 중간에 잘리면 치운다.
        private void PlayWeaponRite(int beat)
        {
            if (_weaponRite == null) return;

            switch (beat)
            {
                case BeatImpact: _weaponRite.Rise(GroundPosition, FacingRotation); break;
                case BeatRoar:   _weaponRite.Converge(); break;
                case BeatNone:   _weaponRite.Clear(); break;
            }
        }

        private void ApplyCinematic(int beat)
        {
            bool running = beat != BeatNone;
            if (_cinematicCamera != null) _cinematicCamera.enabled = running;
            if (_cinematicVolume != null) _cinematicVolume.enabled = running;

            if (beat == BeatShadow)
            {
                // 리그를 착지 지점에, 보스 정면을 마주 보도록 놓는다.
                // 180도 돌리는 이유: 이래야 카메라 키의 z 음수가 "보스 앞"이 되어 얼굴이 잡힌다.
                if (_detachedRig != null)
                    _detachedRig.SetPositionAndRotation(
                        GroundPosition, FacingRotation * Quaternion.Euler(0f, 180f, 0f));

                // 플레이어 카메라는 이 시점엔 확실히 존재한다(스폰 직후엔 아직 없을 수 있다).
                _playerCamera = Camera.main;
                _cameraElapsed = 0f;
                _realElapsed = 0f;
                _cameraRunning = true;
            }
            else if (!running)
            {
                _cameraRunning = false;
                // 임팩트 잔재가 남으면 다음에 이 보스가 다시 등장할 때 시야각이 어긋난 채 시작한다.
                _shakeElapsed = -1f;
                _hitStopRemaining = 0f;
                _fovKickRemaining = 0f;
                _flashRemaining = 0f;
                SetFlashAlpha(0f);
                if (_cinematicCamera != null && _baseFov > 0f) _cinematicCamera.fieldOfView = _baseFov;
            }

            if (_director == null) return;
            if (beat == BeatShadow) _director.Play();
            else if (!running) _director.Stop();
        }

        /// 등장 애니를 스테이트 이름으로 직접 건다.
        ///
        /// 파라미터 조건을 안 쓰는 이유: 그러려면 공용 BossAnimState에 값을 추가하고
        /// BossView가 그걸 흘려보내야 하는데, 그건 다른 보스까지 건드리는 변경이다.
        /// Play는 컨트롤러에 스테이트만 있으면 되고, 없으면 조용히 무시된다.
        ///
        /// 착지 스테이트를 물려 두면 포효 구간에 FSM이 깨어나 추격에 들어가도
        /// 애니는 바뀌지 않는다 — 걷는 모션이 튀어나오는 걸 이걸로 막는다.
        private void PlayEntranceAnimation(int beat)
        {
            if (_animator == null) return;

            if ((beat == BeatHover || beat == BeatDive) && _fallStateHash != 0)
                _animator.Play(_fallStateHash, 0, 0f);
            else if (beat == BeatImpact && _landStateHash != 0)
                _animator.Play(_landStateHash, 0, 0f);
        }

        /// 키프레임 사이를 훑어 카메라를 놓는다.
        ///
        /// 리그가 착지 지점에 보스 정면을 향해 놓여 있으므로, 키의 값은 그대로 로컬 좌표가 된다.
        /// 어느 맵 어느 자리에서 소환되든 같은 그림이 나오는 이유다.
        private void TickCamera()
        {
            if (!_cameraRunning || _cinematicCamera == null || _cameraKeys == null || _cameraKeys.Length == 0)
                return;

            float dt = Time.deltaTime;   // guardrail:allow RNG — 카메라 연출 전용(Render). 판정은 서버 틱이 잰다
            _realElapsed += dt;
            TickFlash(dt);

            // 히트스톱: 카메라 시간만 멈춘다. 서버 박자는 계속 흐르므로 등장 길이는 변하지 않는다.
            if (_hitStopRemaining > 0f) _hitStopRemaining -= dt;
            else _cameraElapsed += dt;

            var cam = _cinematicCamera.transform;
            int last = _cameraKeys.Length - 1;

            // 구간을 찾는다. 키가 몇 개뿐이라 선형 탐색으로 충분하다.
            int i = 0;
            while (i < last && _cameraElapsed >= _cameraKeys[i + 1].AtSeconds) i++;

            Vector3 localPosition;
            Quaternion localRotation;
            float fov;

            if (i >= last)
            {
                localPosition = _cameraKeys[last].Position;
                localRotation = RotationFor(_cameraKeys[last], localPosition);
                fov = FovFor(_cameraKeys[last]);
            }
            else
            {
                CameraKey a = _cameraKeys[i], b = _cameraKeys[i + 1];

                // 다음 키가 하드컷이면 그 시점까지 앞 키의 자세를 그대로 물고 있는다.
                // 시간이 b에 닿는 순간 위의 구간 탐색이 b로 넘어가면서 그림이 툭 갈린다 — 그게 컷이다.
                float span = b.AtSeconds - a.AtSeconds;
                float t = b.Cut ? 0f
                        : span > 0.0001f ? Mathf.Clamp01((_cameraElapsed - a.AtSeconds) / span)
                        : 1f;

                // SmoothStep으로 구간 경계에서 속도를 죽인다 — 선형이면 키마다 각지게 꺾인다.
                t = t * t * (3f - 2f * t);

                localPosition = Vector3.Lerp(a.Position, b.Position, t);

                // 양 끝의 회전을 각각 구해 섞는다 — 한쪽만 보스를 따라보는 구간도 자연히 이어진다.
                localRotation = Quaternion.Slerp(RotationFor(a, localPosition),
                                                 RotationFor(b, localPosition), t);
                fov = Mathf.Lerp(FovFor(a), FovFor(b), t);
            }

            cam.localPosition = localPosition;
            cam.localRotation = localRotation;

            ApplyImpactShake(cam, dt);
            _cinematicCamera.fieldOfView = fov + FovKick(dt);

            BlendOutToPlayerCamera(cam);
        }

        /// 이 키의 시야각. 0이면 카메라에 설정된 설계값으로 돌아간다.
        private float FovFor(CameraKey key) => key.FieldOfView > 0f ? key.FieldOfView : _baseFov;

        /// 감쇠 진동 한 건을 카메라 로컬 자세 위에 얹는다.
        ///
        /// 왜 여기서 직접 흔드는가: 프로젝트의 CameraShaker는 CinemachineExtension이라
        /// 플레이어 vcam에만 붙는다. 컷신 동안엔 이 시네마틱 카메라가 Depth로 그 위를 덮으므로
        /// 그쪽 흔들림은 화면에 한 픽셀도 나타나지 않는다 — 착지가 가벼웠던 진짜 원인이다.
        private void ApplyImpactShake(Transform cam, float dt)
        {
            if (_shakeElapsed < 0f) return;

            _shakeElapsed += dt;

            float envelope = Mathf.Exp(-_impactShakeDamping * _shakeElapsed);
            if (_impactShakeAmplitude * envelope < 0.002f) { _shakeElapsed = -1f; return; }

            // 첫 프레임이 최대(1)이고 반 주기마다 부호가 뒤집힌다. 진폭만 줄이는 노이즈는
            // 화면이 뿌옇게 떨리기만 하고 때린 느낌이 안 난다.
            float wave = envelope * Mathf.Cos(_shakeElapsed * _impactShakeFrequency * 2f * Mathf.PI);
            Vector2 offset = _shakeDirection * (_impactShakeAmplitude * wave);

            // 화면 기준 변위를 카메라가 보는 방향으로 옮긴다 — 위아래로 흔들리면 화면에서도 위아래여야 한다.
            cam.localPosition += cam.localRotation * new Vector3(offset.x, offset.y, 0f);

            // 위치만 흔들면 평면적이라 약간 비틀어 준다.
            cam.localRotation *= Quaternion.Euler(-offset.y * 5f, 0f, offset.x * 5f);
        }

        /// 착지 순간 넓어졌다 제자리로 돌아오는 시야각 증분(도). 제곱으로 줄여 초반을 급하게 뺀다.
        private float FovKick(float dt)
        {
            if (_fovKickRemaining <= 0f) return 0f;

            _fovKickRemaining -= dt;
            if (_fovKickRemaining <= 0f) return 0f;

            float k = _fovKickRemaining / _impactFovRecoverSeconds;
            return _impactFovKick * k * k;
        }

        private void TickFlash(float dt)
        {
            if (_flashRemaining <= 0f) return;

            _flashRemaining -= dt;
            float k = Mathf.Max(0f, _flashRemaining) / _impactFlashSeconds;
            SetFlashAlpha(k * k);
        }

        /// 흰 판의 알파만 건드린다. 머티리얼을 복제하지 않으려고 프로퍼티 블록을 쓴다 —
        /// 이 프리팹은 여러 피어에서 같은 에셋을 공유한다.
        private void SetFlashAlpha(float alpha)
        {
            if (_impactFlash == null) return;

            _flashBlock ??= new MaterialPropertyBlock();
            _impactFlash.GetPropertyBlock(_flashBlock);

            var color = new Color(1f, 1f, 1f, alpha);
            _flashBlock.SetColor(FlashColorId, color);
            _flashBlock.SetColor(FlashColorLegacyId, color);   // URP/빌트인 어느 셰이더가 꽂혀도 먹게

            _impactFlash.SetPropertyBlock(_flashBlock);

            // 다 사라진 판을 계속 그리면 화면 전체를 덮는 투명 쿼드가 매 프레임 블렌딩된다.
            bool visible = alpha > 0.001f;
            if (_impactFlash.enabled != visible) _impactFlash.enabled = visible;
        }

        /// 이 키가 원하는 회전. LookAtBoss면 매 프레임 보스를 겨눈다.
        ///
        /// 고정 각도로는 강하 구간을 담을 수 없다 — 보스 높이가 28m에서 13m까지 바뀌는 동안
        /// 앙각이 44도에서 2도까지 움직이기 때문에, 한 각도로 고정하면 대부분의 시간 프레임 밖이다.
        private Quaternion RotationFor(CameraKey key, Vector3 cameraLocalPosition)
        {
            if (!key.LookAtBoss || _detachedRig == null)
                return Quaternion.Euler(key.PitchDegrees, 0f, 0f);

            // 카메라가 리그의 자식이므로 조준점도 리그 로컬로 옮겨서 계산한다.
            float height = key.LookAtHeight > 0f ? key.LookAtHeight : _lookAtHeight;
            Vector3 targetLocal = _detachedRig.InverseTransformPoint(
                transform.position + Vector3.up * height);
            Vector3 toTarget = targetLocal - cameraLocalPosition;

            return toTarget.sqrMagnitude < 0.0001f
                ? Quaternion.Euler(key.PitchDegrees, 0f, 0f)
                : Quaternion.LookRotation(toTarget);
        }

        /// 끝나기 직전 플레이어 카메라 자리로 미끄러져 들어간다.
        ///
        /// 마지막 키를 게임플레이 카메라와 같은 오프셋으로 잡아도 소용이 없다 —
        /// 그 오프셋은 보스 착지 지점 기준이고 플레이어 카메라는 플레이어 기준이라 실제 자리가 다르다.
        /// 그래서 값이 아니라 실제 카메라의 월드 자세를 향해 섞는다. 다 섞이면 꺼도 티가 안 난다.
        private void BlendOutToPlayerCamera(Transform cam)
        {
            if (_blendOutSeconds <= 0f || _playerCamera == null) return;

            // 히트스톱으로 멈추지 않는 시계로 잰다 — 카메라 시간으로 재면 멈춘 만큼 늦게 시작해
            // 컷신이 끝나는 순간 아직 안 섞인 나머지가 그대로 튄다.
            float remaining = TotalSeconds - _realElapsed;
            if (remaining > _blendOutSeconds) return;

            float t = 1f - Mathf.Clamp01(remaining / _blendOutSeconds);
            t = t * t * (3f - 2f * t);

            var target = _playerCamera.transform;
            cam.SetPositionAndRotation(
                Vector3.Lerp(cam.position, target.position, t),
                Quaternion.Slerp(cam.rotation, target.rotation, t));
        }

        private void PlayCues(PatternEffectCue[] cues)
        {
            if (cues == null) return;

            foreach (var cue in cues)
            {
                if (cue == null) continue;

                // 기준점은 큐가 보스에 붙는지로 가른다.
                //  · 붙는 큐 — 보스를 따라와야 하므로 보스 위치. 강하 기운·낙하 궤적이 그렇다.
                //  · 안 붙는 큐 — 바닥에 남아야 하므로 착지 지점. 크레이터·균열이 그렇다.
                // 강하 중에는 보스가 상공에 있어서, 이 구분이 없으면 바닥 연출이 하늘에 뜬다.
                Vector3 origin = cue.AttachToAnchor ? transform.position : GroundPosition;
                cue.Spawn(origin + transform.rotation * cue.LocalOffset,
                          transform.rotation,
                          cue.AttachToAnchor ? transform : null);
            }
        }

        private PatternEffectCue[] CuesFor(int beat)
        {
            switch (beat)
            {
                case BeatShadow: return _shadowCues;
                case BeatHover:  return _hoverCues;
                case BeatDive:   return _diveCues;
                case BeatImpact: return _impactCues;
                case BeatRoar:   return _roarCues;
                default:         return null;
            }
        }

        /// 시네마틱 리그는 프리팹에서 보스 밑에 달려 있지만 런타임에는 떼어낸다.
        /// 붙어 있으면 카메라가 보스와 함께 45m를 낙하해서 착지 지점이 아니라 보스만 따라다닌다.
        private void DetachRig()
        {
            // 카메라가 아니라 그 부모(리그)를 뗀다 — 카메라만 떼면 키프레임의 기준 공간이 사라진다.
            Transform rig = _director != null ? _director.transform
                          : _cinematicCamera != null ? _cinematicCamera.transform.parent
                          : null;

            if (rig == null || rig == transform) return;

            rig.SetParent(null, worldPositionStays: true);
            _detachedRig = rig;
        }
    }
}
