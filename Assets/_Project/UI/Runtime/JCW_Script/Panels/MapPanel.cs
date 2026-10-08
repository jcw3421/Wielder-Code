using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 맵(백그라운드 기반) 패널. CanvasGroup.alpha 한 줄로 자식 전체 투명도를 조절한다.
    /// 게임플레이 이벤트와 무관한 순수 표시 UI라 이벤트를 구독하지 않는다.
    /// </summary>
    public sealed class MapPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        [Range(0f, 1f)]
        [SerializeField] private float _defaultAlpha = 1f;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            SetAlpha(_defaultAlpha);
        }

        /// <summary>맵 전체 투명도 설정(0=완전 투명, 1=불투명). 슬라이더 등에 연결.</summary>
        public void SetAlpha(float alpha)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}
