using Fusion;
using UnityEngine;

namespace Game.BossContent
{
    /// 발밑 분진(먼지·가루).
    ///
    /// 클립마다 발 딛는 시점을 베이킹하지 않는다. 클립이 24개인 데다 페이즈 배속까지 걸려서
    /// 베이킹한 값은 금방 어긋난다. 대신 **실제 발 본을 매 프레임 읽어 접지를 감지**한다 —
    /// 발이 바닥 가까이 내려오면서 아래로 향하던 속도가 꺾이는 순간이 곧 착지다.
    /// 이러면 클립을 교체하거나 배속이 바뀌어도 저절로 맞는다.
    ///
    /// 세 가지를 낸다:
    ///   1) 착지 분진 — 발이 닿는 순간 터지는 먼지. 무거운 보스라는 인상을 만든다.
    ///   2) 이동 분진 — 전진 속도에 비례해 발밑에 깔리는 가루. 돌진할 때 확 인다.
    ///   3) 행동 분진 — 패턴을 새로 시작할 때 발밑에서 한 번 확 인다. 보스가 자세를 잡으며
    ///      바닥을 딛는 순간이라, 큰 동작이 시작된다는 신호를 몸으로 읽게 해준다.
    ///   4) 충격 분진 — 패턴이 바닥을 찍을 때 BossPatternEffectPlayer가 따로 낸다(이 컴포넌트 밖).
    ///
    /// 순수 연출이라 모든 피어에서 로컬로 돈다.
    public class BossGroundDust : NetworkBehaviour
    {
        [SerializeField] private BossNetState _netState;
        [SerializeField] private ParticleSystem _dust;

        [Tooltip("발 본. 보통 Character1_LeftFoot / Character1_RightFoot.")]
        [SerializeField] private Transform[] _feet;

        [Header("착지 감지")]
        [Tooltip("보스 발밑 기준 이 높이 안에 들어와야 접지로 본다.")]
        [SerializeField] private float _contactHeight = 0.45f;

        [Tooltip("이보다 빠르게 내려오던 발만 분진을 낸다 — 가만히 선 발이 계속 터지는 걸 막는다.")]
        [SerializeField] private float _contactSpeed = 1.2f;

        [Tooltip("착지 한 번에 내는 입자 수.")]
        [SerializeField] private int _stepParticles = 9;

        [Tooltip("같은 발이 이 시간 안에 다시 터지지 않게 한다.")]
        [SerializeField] private float _stepCooldown = 0.15f;

        [Header("이동 분진")]
        [Tooltip("이 속도(m/s)를 넘겨야 이동 분진이 깔린다.")]
        [SerializeField] private float _moveThreshold = 1.5f;

        [Tooltip("속도 1m/s당 초당 입자 수.")]
        [SerializeField] private float _moveParticlesPerSpeed = 2.5f;

        [Header("행동 분진")]
        [Tooltip("패턴을 새로 시작할 때 발밑에서 한 번 내는 입자 수. 0이면 끈다.")]
        [SerializeField] private int _actionParticles = 16;

        [Tooltip("행동 분진이 퍼지는 반경 배수. 발밑보다 넓게 깔린다.")]
        [SerializeField] private float _actionSpread = 1.6f;

        private float[] _prevFootY;
        private float[] _nextStepAt;
        private Vector3 _prevRoot;
        private float _moveCarry;
        private int _lastPatternSeq;

        private void Reset()
        {
            _netState = GetComponent<BossNetState>();
            _dust = GetComponentInChildren<ParticleSystem>();
        }

        public override void Spawned()
        {
            int n = _feet != null ? _feet.Length : 0;
            _prevFootY = new float[n];
            _nextStepAt = new float[n];

            for (int i = 0; i < n; i++)
                if (_feet[i] != null) _prevFootY[i] = _feet[i].position.y;

            _prevRoot = transform.position;
            _lastPatternSeq = _netState != null ? _netState.PatternStartSeq : 0;
        }

        /// Render에서 도는 이유는 애니메이션이 적용된 뒤의 본 위치를 읽어야 하기 때문이다.
        public override void Render()
        {
            if (_dust == null) return;

            float dt = Time.deltaTime;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다
            if (dt <= 0.0001f) return;

            EmitAction();
            EmitFootsteps(dt);
            EmitMovement(dt);

            _prevRoot = transform.position;
        }

        /// 패턴이 새로 시작될 때마다 한 번. PatternStartSeq는 서버가 패턴을 낼 때 올려주는
        /// 일련번호라, 같은 패턴을 연달아 써도 값이 바뀌어 빠짐없이 잡힌다.
        private void EmitAction()
        {
            if (_netState == null || _actionParticles <= 0) return;

            int seq = _netState.PatternStartSeq;
            if (seq == _lastPatternSeq) return;
            _lastPatternSeq = seq;

            Burst(transform.position, _actionParticles, 0.8f, _actionSpread);
        }

        private void EmitFootsteps(float dt)
        {
            if (_feet == null) return;

            float groundY = transform.position.y;

            for (int i = 0; i < _feet.Length; i++)
            {
                var foot = _feet[i];
                if (foot == null) continue;

                float y = foot.position.y;
                float fallSpeed = (_prevFootY[i] - y) / dt;   // 양수 = 내려오는 중
                _prevFootY[i] = y;

                if (Time.time < _nextStepAt[i]) continue;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다
                if (y - groundY > _contactHeight) continue;
                if (fallSpeed < _contactSpeed) continue;

                _nextStepAt[i] = Time.time + _stepCooldown;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다

                // 밟는 세기에 비례해 양을 늘린다 — 살금살금과 쿵 내려찍기가 달라 보인다.
                int count = Mathf.RoundToInt(_stepParticles * Mathf.Clamp(fallSpeed / _contactSpeed, 1f, 3f));
                Burst(new Vector3(foot.position.x, groundY, foot.position.z), count, fallSpeed * 0.25f);
            }
        }

        private void EmitMovement(float dt)
        {
            Vector3 delta = transform.position - _prevRoot;
            delta.y = 0f;

            float speed = delta.magnitude / dt;
            if (speed < _moveThreshold) { _moveCarry = 0f; return; }

            // 소수점 이하를 다음 프레임으로 넘겨야 저속에서도 끊기지 않고 깔린다.
            _moveCarry += speed * _moveParticlesPerSpeed * dt;
            int count = Mathf.FloorToInt(_moveCarry);
            if (count <= 0) return;
            _moveCarry -= count;

            Vector3 behind = transform.position - delta.normalized * 0.6f;
            behind.y = transform.position.y;
            Burst(behind, count, speed * 0.15f);
        }

        /// spread가 1보다 크면 파티클마다 위치를 흩어 넓게 깐다.
        /// (EmitParams는 셰이프 크기를 못 바꾸므로 위치를 직접 흩는다)
        private void Burst(Vector3 position, int count, float upSpeed, float spread = 1f)
        {
            float up = Mathf.Clamp(upSpeed, 0.2f, 2.5f);

            if (spread <= 1.001f)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = position,
                    applyShapeToPosition = true,
                    velocity = Vector3.up * up,
                };
                _dust.Emit(p, count);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 disc = UnityEngine.Random.insideUnitCircle * spread;   // guardrail:allow RNG — 파티클 산포용. 게임 상태에 안 들어간다

                // 바깥으로 밀려나가게 — 발밑에서 고리처럼 퍼진다
                Vector3 outward = new Vector3(disc.x, 0f, disc.y);

                var p = new ParticleSystem.EmitParams
                {
                    position = position + outward,
                    applyShapeToPosition = true,
                    velocity = Vector3.up * up + outward.normalized * (up * 0.6f),
                };
                _dust.Emit(p, 1);
            }
        }
    }
}
