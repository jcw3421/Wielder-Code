using System;
using DG.Tweening;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 얼티밋 게이지 뷰. 직업(ClassType)별로 바·가득참 이펙트를 미리 배치해 두고,
    /// 현재 직업 세트만 켜서 쓴다. 게이지가 가득 차면 그 직업의 이펙트를 페이드로 켠다.
    /// 유저 패널·파티원 카드가 공용으로 쓴다.
    /// </summary>
    public sealed class UltimateGaugeView : MonoBehaviour
    {
        [Serializable]
        private struct ClassGauge
        {
            public ClassType ClassType;
            public GameObject Root;          // 이 직업 세트(바+이펙트)의 부모. 직업 전환 시 켜고 끈다.
            public PlayerStatusBar Bar;      // 직업별 얼티밋 바
            public CanvasGroup ReadyEffect;  // 직업별 가득참 발광 (평소 alpha 0)
        }

        [SerializeField] private ClassGauge[] _gauges;
        [SerializeField] private float _readyFade = 0.25f;

        private ClassGauge _active;
        private bool _hasActive;
        private bool _ready;
        private Tween _readyTween;

        private void OnDestroy() => _readyTween?.Kill();

        /// <summary>직업 세트를 전환한다. 그 직업 것만 켜고 나머지는 끈다.</summary>
        public void SetClass(ClassType classType)
        {
            _readyTween?.Kill();
            _ready = false;
            _hasActive = false;

            for (int gaugeIndex = 0; gaugeIndex < _gauges.Length; gaugeIndex++)
            {
                ClassGauge gauge = _gauges[gaugeIndex];
                bool isActiveClass = gauge.ClassType == classType;

                gauge.Root.SetActive(isActiveClass);
                gauge.ReadyEffect.alpha = 0f;

                if (isActiveClass)
                {
                    _active = gauge;
                    _hasActive = true;
                }
            }
        }

        /// <summary>게이지 값 갱신 + 가득 차면 현재 직업 이펙트를 켠다.</summary>
        public void SetValue(int current, int max)
        {
            if (!_hasActive) return;

            _active.Bar.SetValue(current, max);
            SetReady(current >= max);
        }

        // 가득 참 발광을 페이드로 켜고 끈다. 상태가 그대로면 아무것도 안 한다.
        private void SetReady(bool ready)
        {
            if (ready == _ready) return;
            _ready = ready;

            CanvasGroup effect = _active.ReadyEffect;
            _readyTween?.Kill();
            _readyTween = DOTween.To(() => effect.alpha, alpha => effect.alpha = alpha, ready ? 1f : 0f, _readyFade);
        }
    }
}
