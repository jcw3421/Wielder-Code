using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 좌/우클릭·슬롯 스킬 하나의 쿨타임을 표현하는 뷰.
    /// 스펙 방침대로 매 이벤트를 받지 않고, <see cref="TriggerCooldown"/>로 시작한 뒤
    /// 자체 타이머로 Update에서 fillAmount를 직접 줄여 시계방향 연출을 만든다.
    ///
    /// 프리팹 세팅: <see cref="_cooldownOverlay"/>는 Image Type = Filled(Radial360) 권장.
    /// </summary>
    public sealed class SkillSlot : MonoBehaviour
    {
        [Header("필수")]
        [Tooltip("쿨타임 동안 fillAmount가 1→0으로 줄어드는 오버레이. Filled(Radial360) 권장.")]
        [SerializeField] private Image _cooldownOverlay;

        [Header("선택")]
        [SerializeField] private TMP_Text _cooldownText;   // 남은 초 표기. 없으면 무시.

        private float _duration;
        private float _remaining;

        public bool IsOnCooldown => _remaining > 0f;

        private void Awake() => SetReadyInternal();

        /// <summary>쿨타임 시작. 이미 돌고 있으면 새 값으로 덮어쓴다.</summary>
        public void TriggerCooldown(float seconds)
        {
            if (seconds <= 0f) { SetReady(); return; }

            _duration = seconds;
            _remaining = seconds;
            _cooldownOverlay.fillAmount = 1f;
            _cooldownOverlay.enabled = true;
            if (_cooldownText != null) _cooldownText.enabled = true;
        }

        /// <summary>쿨타임 강제 종료(사용 가능 상태).</summary>
        public void SetReady()
        {
            _remaining = 0f;
            SetReadyInternal();
            PlayReadyFlash();
        }

    

        private void Update()
        {
            if (_remaining <= 0f) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                SetReady();
                return;
            }

            _cooldownOverlay.fillAmount = _remaining / _duration;
            if (_cooldownText != null)
                _cooldownText.text = Mathf.CeilToInt(_remaining).ToString();
        }

        private void SetReadyInternal()
        {
            _cooldownOverlay.fillAmount = 0f;
            _cooldownOverlay.enabled = false;
            if (_cooldownText != null) _cooldownText.enabled = false;
        }

        /// <summary>쿨타임이 끝나 사용 가능해질 때 살짝 튀는 연출.</summary>
        private void PlayReadyFlash()
        {
            if (!isActiveAndEnabled) return;
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6, 0.6f);
        }

        private void OnDestroy() => transform.DOKill();
    }
}
