using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 버프/디버프 하나를 표현하는 아이콘. <see cref="StatusEffectTracker"/>가 풀링해서 켜고 끈다.
    /// 남은 시간은 표시만 갱신한다(판정은 서버 몫 — 만료 신호는 이벤트로 온다).
    ///
    /// 프리팹 세팅: <see cref="_durationOverlay"/>는 Image Type = Filled(Radial360) 권장.
    /// </summary>
    public sealed class StatusEffectIcon : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _durationOverlay;   // 남은 시간 비율(1→0). 없으면 무시.

        private float _duration;
        private float _remaining;

        /// <summary>durationSeconds &lt;= 0 이면 남은 시간 연출을 끈다(무한/미표시).</summary>
        public void Bind(Sprite icon, float durationSeconds)
        {
            _icon.sprite = icon;
            _duration = durationSeconds;
            _remaining = durationSeconds;

            if (_durationOverlay == null) return;
            _durationOverlay.enabled = durationSeconds > 0f;
            _durationOverlay.fillAmount = 1f;
        }

        private void Update()
        {
            if (_remaining <= 0f || _durationOverlay == null) return;

            _remaining -= Time.deltaTime;
            _durationOverlay.fillAmount = Mathf.Clamp01(_remaining / _duration);
        }
    }
}
