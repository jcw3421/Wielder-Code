using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 한 대상(플레이어·파티원·보스)의 버프·디버프 아이콘을 관리한다.
    /// 버프·디버프를 각각 별도 트래커로 보여주고, 타입→아이콘은 카탈로그로,
    /// 지속 틱→초는 틱레이트로 변환해 넘긴다. 세 대상이 이 컴포넌트를 하나씩 재사용한다.
    /// </summary>
    public sealed class StatusEffectDisplay : MonoBehaviour
    {
        [Header("버프")]
        [SerializeField] private StatusEffectTracker _buffTracker;
        [SerializeField] private BuffIconCatalog _buffCatalog;

        [Header("디버프")]
        [SerializeField] private StatusEffectTracker _debuffTracker;
        [SerializeField] private DebuffIconCatalog _debuffCatalog;

        [Tooltip("지속 틱을 초로 환산할 때 쓰는 틱레이트. Fusion 시뮬레이션 TickRate와 맞춘다.")]
        [SerializeField] private float _ticksPerSecond = 60f;

        public void ApplyBuff(BuffInfo info)
            => _buffTracker.Show((int)info.Type, _buffCatalog.GetIcon(info.Type), ToSeconds(info.DurationTicks));

        public void RemoveBuff(BuffType type) => _buffTracker.Hide((int)type);

        public void ApplyDebuff(DebuffInfo info)
            => _debuffTracker.Show((int)info.Type, _debuffCatalog.GetIcon(info.Type), ToSeconds(info.DurationTicks));

        public void RemoveDebuff(DebuffType type) => _debuffTracker.Hide((int)type);

        public void Clear()
        {
            _buffTracker.Clear();
            _debuffTracker.Clear();
        }

        private float ToSeconds(int ticks) => _ticksPerSecond > 0f ? ticks / _ticksPerSecond : 0f;
    }
}
