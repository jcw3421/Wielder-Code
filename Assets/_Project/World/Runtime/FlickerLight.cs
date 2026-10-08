using UnityEngine;

namespace Game.World
{
    /// <summary>횃불·화로 조명이 살아 있는 것처럼 밝기를 흔든다.
    ///
    /// 표시 전용이라 복제하지 않는다 — 각 피어가 로컬로 돌려도 게임 상태에 영향이 없고,
    /// 맵에 수십 개가 깔리므로 네트워크로 보낼 값이 아니다.
    ///
    /// 위상 시드를 <b>월드 좌표에서 뽑는다</b>. 난수를 쓰면 피어마다·재생마다 달라지고
    /// 무엇보다 같은 프레임에 모든 횃불이 같은 밝기가 되는 사고를 막으려면 시드가 필요한데,
    /// 좌표 기반이면 그 두 가지를 한 번에 만족한다(결정론 규칙도 지킨다).</summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [Tooltip("기준 밝기 대비 흔들림 폭(0.35 = ±35%).")]
        [SerializeField, Range(0f, 1f)] private float _amplitude = 0.35f;

        [Tooltip("초당 흔들림 속도. 높을수록 급하게 떨린다.")]
        [SerializeField] private float _speed = 6f;

        [Tooltip("같이 흔들 불꽃 메시(선택). 세로로만 늘었다 줄었다 한다.")]
        [SerializeField] private Transform _flame;

        [SerializeField, Range(0f, 1f)] private float _flameAmplitude = 0.12f;

        private Light _light;
        private float _baseIntensity;
        private Vector3 _baseFlameScale;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;

            if (_flame != null) _baseFlameScale = _flame.localScale;

            Vector3 p = transform.position;
            _seed = Mathf.Repeat(p.x * 12.9898f + p.z * 78.233f + p.y * 37.719f, 100f);
        }

        private void Update()
        {
            // Perlin은 0..1을 돌려주므로 -1..1로 옮겨야 기준 밝기를 중심으로 흔들린다.
            // guardrail:allow RNG — 불빛 깜빡임 연출(뷰 전용). 피어별로 위상이 달라도 무방하다.
            float n = Mathf.PerlinNoise(Time.time * _speed + _seed, 0f) * 2f - 1f;

            _light.intensity = _baseIntensity * (1f + n * _amplitude);

            if (_flame != null)
            {
                _flame.localScale = new Vector3(
                    _baseFlameScale.x,
                    _baseFlameScale.y * (1f + n * _flameAmplitude),
                    _baseFlameScale.z);
            }
        }
    }
}
