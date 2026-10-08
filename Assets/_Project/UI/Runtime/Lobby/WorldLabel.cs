using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>월드에 떠 있는 한 줄짜리 텍스트. 항상 카메라를 향해 돌아간다.
    ///
    /// 캔버스와 텍스트를 씬에 손으로 만들어 두지 않고 런타임에 조립하는 이유:
    /// 표지판·NPC 라벨은 문자열만 다르고 구조가 전부 같다. 씬에 십수 개를 박아 두면
    /// 문구 하나 고칠 때마다 프리팹/씬을 다시 만져야 한다.
    ///
    /// 채팅 말풍선은 배경·꼬리·팝 연출이 필요해 <see cref="ChatBubble"/>로 따로 만든다.</summary>
    public sealed class WorldLabel : MonoBehaviour
    {
        /// 캔버스는 픽셀 단위로 잡고 스케일로 월드 크기를 맞춘다(1px = 1cm).
        private const float PixelToWorld = 0.01f;

        private Canvas _canvas;
        private TextMeshProUGUI _text;

        public static WorldLabel Create(Transform parent, Vector3 localPosition, float width,
                                        float fontSize, TMP_FontAsset font, string objectName)
        {
            var root = new GameObject(objectName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(width, width * 0.5f);
            rootRect.localScale = Vector3.one * PixelToWorld;

            var label = root.AddComponent<WorldLabel>();
            label._canvas = root.AddComponent<Canvas>();
            label._canvas.renderMode = RenderMode.WorldSpace;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(root.transform, false);

            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            label._text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label._text.font = font;
            label._text.fontSize = fontSize;
            label._text.alignment = TextAlignmentOptions.Center;
            label._text.raycastTarget = false;
            return label;
        }

        public bool Visible
        {
            get => _canvas != null && _canvas.enabled;
            set { if (_canvas != null) _canvas.enabled = value; }
        }

        public void SetText(string value)
        {
            if (_text != null) _text.text = value;
        }

        public void SetColor(Color value)
        {
            if (_text != null) _text.color = value;
        }

        /// 쿼터뷰라 카메라 회전이 고정이지만, 카메라를 돌리는 연출이 들어와도 글자가 눕지 않게 매 프레임 맞춘다.
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
