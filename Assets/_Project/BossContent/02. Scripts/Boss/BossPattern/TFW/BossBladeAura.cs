using Fusion;
using UnityEngine;

namespace Game.BossContent
{
    /// 검기(劍氣) — 칼날 외곽에 서리는 기운.
    ///
    /// 궤적 리본(BossWeaponTrail)이 "칼이 지나간 자리"를 월드에 남긴다면, 이쪽은
    /// "지금 칼에 실린 기"라 칼에 붙어 같이 움직인다. 둘은 겹쳐 보여야 한다 —
    /// 리본만 있으면 칼 자체는 평범한 쇳덩이로 보인다.
    ///
    /// 세기는 휘두르는 속도가 정한다. 칼을 들고만 있으면 꺼지고, 후려칠 때 확 붙는다.
    /// TFW/BladeAura 셰이더의 _Strength를 프로퍼티 블록으로 민다.
    ///
    /// 순수 연출이라 모든 피어에서 로컬로 돈다. 상태는 읽기만 한다.
    public class BossBladeAura : NetworkBehaviour
    {
        [SerializeField] private BossNetState _netState;

        [Tooltip("무기마다 칼날 평면에 세워 둔 쿼드. 꺼져 있는 무기는 자동으로 건너뛴다.")]
        [SerializeField] private Renderer[] _auras;

        [Tooltip("이 속도(m/s)로 휘두르면 검기가 최대로 붙는다. 낮추면 살짝만 움직여도 켜진다.")]
        [SerializeField] private float _fullSpeed = 8f;

        [Tooltip("붙는 속도(1/초). 크면 획이 시작되자마자 확 켜진다.")]
        [SerializeField] private float _attack = 14f;

        [Tooltip("빠지는 속도(1/초). 작으면 획이 끝난 뒤 여운이 남는다.")]
        [SerializeField] private float _release = 4f;

        [Tooltip("Active가 끝난 뒤에도 이만큼 더 켜 둔다 — 마지막 획이 뚝 끊기지 않게.")]
        [SerializeField] private float _tailSeconds = 0.12f;

        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private MaterialPropertyBlock _block;
        private Vector3[] _lastPos;
        private float[] _strength;
        private float _emitUntil;

        private void Reset() => _netState = GetComponent<BossNetState>();

        public override void Spawned()
        {
            if (_auras == null || _auras.Length == 0) return;

            _block = new MaterialPropertyBlock();
            _lastPos = new Vector3[_auras.Length];
            _strength = new float[_auras.Length];

            for (int i = 0; i < _auras.Length; i++)
            {
                if (_auras[i] == null) continue;
                _lastPos[i] = _auras[i].transform.position;
                _auras[i].enabled = false;
            }
        }

        /// Render에서 도는 이유는 애니메이션이 적용된 뒤의 본 위치를 읽어야 하기 때문이다
        /// (BossWeaponTrail과 같다). FixedUpdateNetwork면 틱 사이 보간이 빠져 속도가 튄다.
        public override void Render()
        {
            if (_netState == null || _auras == null || _lastPos == null) return;

            // 단계 값만 보면 안 된다 — 사망·무력화로 패턴이 끊겨도 그 값은 Active에 멈춰 있어
            // 시체의 칼이 계속 빛난다(TfwPatternPhase 주석 참고).
            if (TfwPatternPhase.IsAttackActive(_netState))
                _emitUntil = Time.time + _tailSeconds;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다

            bool emitting = Time.time < _emitUntil;   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다
            float dt = Mathf.Max(0.0001f, Time.deltaTime);   // guardrail:allow RNG — 연출 전용 Render. 틱 시간으로 그리면 보간이 빠져 궤적이 각진다

            for (int i = 0; i < _auras.Length; i++)
            {
                var aura = _auras[i];
                if (aura == null) continue;

                Vector3 pos = aura.transform.position;
                float speed = (pos - _lastPos[i]).magnitude / dt;
                _lastPos[i] = pos;

                // 꺼진 무기(페이즈가 다른 쪽)는 위치만 갱신하고 넘어간다 — 무기가 켜지는
                // 순간 한 프레임치 이동이 통째로 속도로 잡혀 검기가 번쩍이는 걸 막는다.
                if (!aura.gameObject.activeInHierarchy) { _strength[i] = 0f; continue; }

                float target = emitting
                    ? Mathf.Clamp01(speed / Mathf.Max(0.01f, _fullSpeed))
                    : 0f;

                float rate = target > _strength[i] ? _attack : _release;
                _strength[i] = Mathf.MoveTowards(_strength[i], target, rate * dt);

                bool visible = _strength[i] > 0.005f;
                aura.enabled = visible;
                if (!visible) continue;

                aura.GetPropertyBlock(_block);
                _block.SetFloat(StrengthId, _strength[i]);
                aura.SetPropertyBlock(_block);
            }
        }
    }
}
