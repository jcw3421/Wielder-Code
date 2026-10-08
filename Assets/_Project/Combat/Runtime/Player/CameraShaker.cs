using Game.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Combat.Player
{
    /// <summary>로컬 카메라 흔들림. GameplayEvents.CameraShakeRequested를 받아
    /// "충격 한 방"을 쌓고, 파이프라인 마지막에서 그 합을 카메라 보정 채널에 얹는다.
    ///
    /// 흔들림 한 건은 감쇠 진동이다 — 임의 방향으로 한 번 세게 치고 나서
    /// 튕겨 돌아오며 지수적으로 잦아든다. 진폭만 서서히 줄이는 노이즈는
    /// 화면이 뿌옇게 떨리기만 하고 때린 느낌이 안 난다. 첫 프레임에 최대로
    /// 밀리고 반 주기 만에 반대로 넘어가는 게 타격감을 만든다.
    ///
    /// 여러 건이 겹치면 더한다 — 연계 타격이 촘촘히 들어올 때 하나가 다른 하나를
    /// 끊지 않고 쌓여야 연타로 읽힌다.
    ///
    /// 파이프라인 마지막(Finalize)에서 CameraState의 보정 채널만 건드린다 —
    /// Follow 타깃이나 카메라 transform을 직접 밀면 Cinemachine의 댐핑과 싸워서
    /// 흔들림이 멎은 뒤에도 카메라가 제자리로 천천히 기어 돌아온다.
    ///
    /// 순수 로컬 연출이라 네트워크 상태를 바꾸지 않는다. PlayerCameraController가
    /// 로컬 플레이어의 vcam에만 붙이므로 세션당 하나만 산다.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraShaker : CinemachineExtension
    {
        [Header("약한 세기")]
        [Tooltip("첫 충격의 크기(m).")]
        [SerializeField] private float _lightAmplitude = 0.045f;
        [Tooltip("초당 진동 횟수. 높을수록 짧고 날카롭다.")]
        [SerializeField] private float _lightFrequency = 18f;
        [Tooltip("클수록 빨리 잦아든다.")]
        [SerializeField] private float _lightDamping = 18f;

        [Header("큰 세기")]
        [SerializeField] private float _heavyAmplitude = 0.18f;
        [SerializeField] private float _heavyFrequency = 15f;
        [SerializeField] private float _heavyDamping = 14f;

        [Header("공통")]
        [Tooltip("진폭 1m당 함께 꺾이는 각도(도). 위치만 흔들면 평면적이라 약간 비틀어 준다.")]
        [SerializeField] private float _tiltPerMeter = 5f;
        [Tooltip("진폭 1m당 시야각이 좁아지는 정도(도). 순간적으로 확 당겨졌다 풀리는 맛.")]
        [SerializeField] private float _fovKickPerMeter = 6f;
        [Tooltip("동시에 살아 있을 수 있는 충격 수. 넘치면 가장 약해진 것부터 밀어낸다.")]
        [SerializeField] private int _maxConcurrent = 4;
        [Tooltip("전체 세기 배율. 0이면 흔들지 않는다.")]
        [SerializeField, Range(0f, 2f)] private float _masterScale = 1f;

        /// 충격 한 건. 카메라 로컬 축(우/상) 기준 방향으로 감쇠 진동한다.
        private struct Impulse
        {
            public Vector2 Direction;   // 화면 기준 방향(정규화)
            public float Amplitude;
            public float Frequency;
            public float Damping;
            public float Elapsed;

            /// 지금 이 순간의 변위 계수. 첫 프레임이 1(최대)이고, 반 주기마다 부호가 뒤집히며 잦아든다.
            public float Evaluate()
                => Mathf.Exp(-Damping * Elapsed) * Mathf.Cos(Elapsed * Frequency * 2f * Mathf.PI);

            /// 눈에 안 보일 만큼 잦아들었나.
            public bool IsDone => Amplitude * Mathf.Exp(-Damping * Elapsed) < 0.002f;
        }

        private readonly System.Collections.Generic.List<Impulse> _impulses = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            GameplayEvents.CameraShakeRequested += OnShakeRequested;
        }

        private void OnDisable() => GameplayEvents.CameraShakeRequested -= OnShakeRequested;

        protected override void OnDestroy()
        {
            GameplayEvents.CameraShakeRequested -= OnShakeRequested;
            base.OnDestroy();
        }

        private void OnShakeRequested(CameraShakeStrength strength)
        {
            switch (strength)
            {
                case CameraShakeStrength.Light:
                    Shake(_lightAmplitude, _lightFrequency, _lightDamping);
                    break;
                case CameraShakeStrength.Heavy:
                    Shake(_heavyAmplitude, _heavyFrequency, _heavyDamping);
                    break;
            }
        }

        /// 충격 한 건을 쌓는다. 방향은 매번 다르게 잡아 같은 자리로만 밀리지 않게 한다.
        public void Shake(float amplitude, float frequency, float damping)
        {
            if (amplitude <= 0f || _masterScale <= 0f) return;

            if (_impulses.Count >= _maxConcurrent) DropWeakest();

            // 세로보다 가로가 넓게 흔들려야 쿼터뷰에서 자연스럽다.
            // guardrail:allow RNG — 로컬 카메라 연출 전용. 피어마다 방향이 달라도 게임 상태와 무관하다
            float angle = Random.Range(0f, Mathf.PI * 2f);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f).normalized;

            _impulses.Add(new Impulse
            {
                Direction = dir,
                Amplitude = amplitude * _masterScale,
                Frequency = frequency,
                Damping = damping,
                Elapsed = 0f,
            });
        }

        private void DropWeakest()
        {
            int weakest = 0;
            float min = float.MaxValue;
            for (int i = 0; i < _impulses.Count; i++)
            {
                float remaining = _impulses[i].Amplitude * Mathf.Exp(-_impulses[i].Damping * _impulses[i].Elapsed);
                if (remaining >= min) continue;
                min = remaining;
                weakest = i;
            }
            _impulses.RemoveAt(weakest);
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || _impulses.Count == 0) return;

            // deltaTime이 음수면 Cinemachine이 상태를 리셋하는 중이다(순간이동·컷 전환). 그때는 흔들지 않는다.
            if (deltaTime < 0f)
            {
                _impulses.Clear();
                return;
            }

            Vector2 screenOffset = Vector2.zero;

            for (int i = _impulses.Count - 1; i >= 0; i--)
            {
                var imp = _impulses[i];
                imp.Elapsed += deltaTime;

                if (imp.IsDone) { _impulses.RemoveAt(i); continue; }

                screenOffset += imp.Direction * (imp.Amplitude * imp.Evaluate());
                _impulses[i] = imp;
            }

            if (screenOffset.sqrMagnitude < 0.0000001f) return;

            // 화면 기준 변위를 카메라가 보는 방향으로 옮긴다 — 위아래로 흔들리는 게 화면에서도 위아래여야 한다.
            Quaternion look = state.RawOrientation;
            state.PositionCorrection += look * new Vector3(screenOffset.x, screenOffset.y, 0f);

            if (_tiltPerMeter > 0.0001f)
                state.OrientationCorrection *= Quaternion.Euler(
                    -screenOffset.y * _tiltPerMeter, 0f, screenOffset.x * _tiltPerMeter);

            if (_fovKickPerMeter > 0.0001f)
            {
                var lens = state.Lens;
                lens.FieldOfView = Mathf.Max(1f, lens.FieldOfView - screenOffset.magnitude * _fovKickPerMeter);
                state.Lens = lens;
            }
        }
    }
}
