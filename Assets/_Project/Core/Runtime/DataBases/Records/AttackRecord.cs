using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(menuName = "Game/DataBases/Records/Attack")]
    public class AttackRecord : DataRecord
    {
        [Header("Sheet Data")]
        [SerializeField] private string _name;
        [SerializeField] private int _damageType;
        [SerializeField] private string _animName;
        [SerializeField] private float _particleOccurDelay;
        [SerializeField] private float _attackOccurDelay;
        [SerializeField] private float _cooldown;
        [SerializeField] private float _angle;
        [SerializeField] private float _range;
        [SerializeField] private float _duration;
        [SerializeField] private float _damage;
        [SerializeField] private float _aggroAmount;

        [Header("Manual")]
        [Tooltip("애니메이션 재생 배속. 1 = 원본. 게임플레이 타이밍(Duration 등)은 바뀌지 않으니 주의.")]
        [SerializeField] private float _animSpeed = 1f;

        [Tooltip("타격 시점(초) 목록. 비어 있으면 AttackOccurDelay에 1타.\n" +
                 "총 데미지는 시트값 그대로이고, 타격들이 정확히 나눠 갖는다.")]
        [SerializeField] private float[] _hitTimes;

        [Tooltip("재생할 이펙트 목록. 시점·위치·배속이 각각 따로라 한 공격에 서로 다른 이펙트를 여러 개 둘 수 있다.")]
        [SerializeField] private AttackParticle[] _particles;

        [Header("Manual - 전진기")]
        [Tooltip("공격 중 전방으로 밀려나는 거리(m). 0이면 전진 없음.")]
        [SerializeField] private float _lungeDistance;
        [Tooltip("공격 시작 후 전진이 시작되기까지의 시간(초).")]
        [SerializeField] private float _lungeStartDelay;
        [Tooltip("전진에 걸리는 시간(초). 0이면 전진 없음.")]
        [SerializeField] private float _lungeDuration;
        [Tooltip("전진 진행 곡선. X = 시간 진행률(0~1), Y = 이동 진행률(0~1). 클립 루트모션을 구워 넣는다. 비어 있으면 등속.")]
        [SerializeField] private AnimationCurve _lungeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public string Name => _name;
        public int DamageType => _damageType;
        public string AnimName => _animName;
        public float ParticleOccurDelay => _particleOccurDelay;
        public float AttackOccurDelay => _attackOccurDelay;
        public float Cooldown => _cooldown;
        public float Angle => _angle;
        public float Range => _range;
        public float Duration => _duration;
        public float Damage => _damage;
        public float AggroAmount => _aggroAmount;
        // 아래는 시트에서 오지 않는 수동 값 — ApplySheetData가 건드리지 않으므로 Sync/플레이 양쪽에서 보존된다.

        /// 0 이하 값은 애니메이터를 멈춰버리므로 1로 막는다.
        public float AnimSpeed => _animSpeed > 0f ? _animSpeed : 1f;

        /// 이펙트 개수. 비어 있으면 이펙트 없음.
        public int ParticleCount => _particles?.Length ?? 0;

        /// i번째 이펙트.
        public AttackParticle Particle(int i) => _particles[i];

        /// 다단 타수. 비어 있으면 1타(= AttackOccurDelay).
        public int HitCount => _hitTimes != null && _hitTimes.Length > 0 ? _hitTimes.Length : 1;

        /// i번째 타격 시점(초).
        public float HitTime(int i)
            => _hitTimes != null && _hitTimes.Length > 0 ? _hitTimes[i] : _attackOccurDelay;

        /// total을 count등분한 i번째 몫. 누적값의 차라서 전체 합이 정확히 total이다
        /// (10을 3등분 → 3, 4, 3). 데미지·게이지처럼 정수로 쪼개되 총량이 새면 안 되는 값에 쓴다.
        public static int Split(float total, int i, int count)
            => Mathf.RoundToInt(total * (i + 1) / count) - Mathf.RoundToInt(total * i / count);

        public float LungeDistance => _lungeDistance;
        public float LungeStartDelay => _lungeStartDelay;
        public float LungeDuration => _lungeDuration;
        public AnimationCurve LungeCurve => _lungeCurve;
        public bool HasLunge => _lungeDistance != 0f && _lungeDuration > 0f;

        public void ApplySheetData(int id, string name, int damageType, string animName,
            float particleOccurDelay, float attackOccurDelay, float cooldown, float angle, float range,
            float duration, float damage, float aggroAmount)
        {
            SetId(id);
            _name = name;
            _damageType = damageType;
            _animName = animName;
            _particleOccurDelay = particleOccurDelay;
            _attackOccurDelay = attackOccurDelay;
            _cooldown = cooldown;
            _angle = angle;
            _range = range;
            _duration = duration;
            _damage = damage;
            _aggroAmount = aggroAmount;
        }
    }
}
