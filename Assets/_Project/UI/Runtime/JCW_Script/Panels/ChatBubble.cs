using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>플레이어 머리 위 채팅 말풍선(뷰). 상용 게임 말풍선의 기본 요소를 따른다.
    ///   - 둥근 모서리 + 테두리 (9-slice 로 늘려도 모서리가 찌그러지지 않는다)
    ///   - 말하는 사람을 가리키는 꼬리 — 여러 명이 동시에 떠들 때 누가 말했는지 구분되는 핵심 요소
    ///   - 짧은 말은 작게, 길면 최대 폭에서 줄바꿈 (박스가 항상 같은 크기면 싸구려로 보인다)
    ///   - 튀어나오는 스케일 팝 + 페이드 (DOTween)
    ///
    /// 스프라이트는 에셋 없이 런타임에 한 번 구워서 정적으로 캐시한다(둥근 모서리용 텍스처를
    /// 프로젝트에 새로 추가하지 않으려고).</summary>
    public sealed class ChatBubble : MonoBehaviour
    {
        /// 캔버스는 픽셀 단위로 잡고 스케일로 월드 크기를 맞춘다(1px = 1cm).
        private const float PixelToWorld = 0.01f;

        private const int PanelSize = 48;
        private const int CornerRadius = 14;
        private const int BorderWidth = 3;
        private const int TailWidth = 26;
        private const int TailHeight = 16;

        /// 꼬리가 몸통 밑변을 살짝 파고들게 해서 이음매가 보이지 않게 한다.
        private const float TailOverlap = 2f;

        private static readonly Color BorderColor = new(0.16f, 0.18f, 0.24f, 1f);

        private static Sprite _panelSprite;
        private static Sprite _tailSprite;

        private CanvasGroup _group;
        private RectTransform _pop;
        private TextMeshProUGUI _text;
        private LayoutElement _textLayout;
        private float _maxWidth;
        private Sequence _sequence;

        public static ChatBubble Create(Transform anchor, float height, float maxWidth, float fontSize,
                                        TMP_FontAsset font, Color background, Color textColor)
        {
            var root = new GameObject("ChatBubble", typeof(RectTransform));
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = Vector3.up * height;

            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0f);          // 아래를 기준으로 위로 자란다
            rootRect.localScale = Vector3.one * PixelToWorld;

            var bubble = root.AddComponent<ChatBubble>();
            bubble._maxWidth = maxWidth;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            bubble._group = root.AddComponent<CanvasGroup>();

            // 팝 애니메이션은 이 컨테이너 스케일로 준다(루트 스케일은 월드 크기라 건드리지 않는다).
            var pop = new GameObject("Pop", typeof(RectTransform));
            pop.transform.SetParent(root.transform, false);
            bubble._pop = (RectTransform)pop.transform;
            Anchor(bubble._pop, new Vector2(0.5f, 0f));

            // 몸통 — 내용에 맞춰 가로/세로가 줄어든다.
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(pop.transform, false);
            var bodyRect = (RectTransform)body.transform;
            Anchor(bodyRect, new Vector2(0.5f, 0f));
            bodyRect.anchoredPosition = new Vector2(0f, TailHeight);

            var image = body.AddComponent<Image>();
            image.sprite = PanelSprite();
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = false;

            var layout = body.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 12, 14);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = body.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(body.transform, false);

            bubble._text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null) bubble._text.font = font;
            bubble._text.fontSize = fontSize;
            bubble._text.color = textColor;
            bubble._text.alignment = TextAlignmentOptions.Center;
            bubble._text.raycastTarget = false;
            bubble._textLayout = textObject.AddComponent<LayoutElement>();

            // 꼬리는 몸통 뒤가 아니라 위에 그린다 — 몸통 밑변의 테두리 선이 꼬리를 가로지르지 않게.
            var tail = new GameObject("Tail", typeof(RectTransform));
            tail.transform.SetParent(pop.transform, false);
            var tailRect = (RectTransform)tail.transform;
            Anchor(tailRect, new Vector2(0.5f, 1f));
            tailRect.sizeDelta = new Vector2(TailWidth, TailHeight);
            tailRect.anchoredPosition = new Vector2(0f, TailHeight + TailOverlap);

            var tailImage = tail.AddComponent<Image>();
            tailImage.sprite = TailSprite();
            tailImage.color = background;
            tailImage.raycastTarget = false;

            bubble._group.alpha = 0f;
            return bubble;
        }

        private static void Anchor(RectTransform rect, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = pivot;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        public void Show(string message)
        {
            _text.text = message;

            // 짧은 말은 자연 폭으로 작게, 길면 최대 폭에서 줄바꿈.
            _text.ForceMeshUpdate();
            _textLayout.preferredWidth = Mathf.Min(_text.preferredWidth, _maxWidth);

            gameObject.SetActive(true);

            _sequence?.Kill();
            _pop.localScale = Vector3.one * 0.7f;
            _sequence = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _group.alpha, v => _group.alpha = v, 1f, 0.12f))
                .Join(_pop.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
        }

        public void Hide(float duration)
        {
            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => _group.alpha, v => _group.alpha = v, 0f, duration))
                .Join(_pop.DOScale(0.85f, duration))
                .OnComplete(() => gameObject.SetActive(false));
        }

        public void Reattach(Transform anchor, float height)
        {
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.up * height;
        }

        private void OnDestroy() => _sequence?.Kill();

        /// 쿼터뷰라 카메라 회전이 고정이지만, 카메라를 돌리는 연출이 들어와도 눕지 않게 매 프레임 맞춘다.
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }

        // ------------------------------------------------------------------ 스프라이트 생성

        private static Sprite PanelSprite()
        {
            if (_panelSprite != null) return _panelSprite;

            var pixels = new Color[PanelSize * PanelSize];
            float half = PanelSize * 0.5f;

            for (int y = 0; y < PanelSize; y++)
            for (int x = 0; x < PanelSize; x++)
            {
                float distance = RoundedRectDistance(x + 0.5f - half, y + 0.5f - half, half, CornerRadius);

                float outside = Mathf.Clamp01(0.5f - distance);                 // 바깥 경계 안티에일리어싱
                float inside = Mathf.Clamp01(0.5f - (distance + BorderWidth));  // 테두리 안쪽

                var color = Color.Lerp(BorderColor, Color.white, inside);
                color.a = outside;
                pixels[y * PanelSize + x] = color;
            }

            _panelSprite = Bake(pixels, PanelSize, PanelSize,
                                new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius));
            return _panelSprite;
        }

        private static Sprite TailSprite()
        {
            if (_tailSprite != null) return _tailSprite;

            var pixels = new Color[TailWidth * TailHeight];
            float half = TailWidth * 0.5f;

            for (int y = 0; y < TailHeight; y++)
            for (int x = 0; x < TailWidth; x++)
            {
                // 위(y=TailHeight)가 밑변, 아래(y=0)가 뾰족한 끝.
                float spread = half * ((y + 0.5f) / TailHeight);
                float dx = Mathf.Abs(x + 0.5f - half);

                float outside = Mathf.Clamp01(spread - dx + 0.5f);
                float inside = Mathf.Clamp01(spread - BorderWidth * 1.6f - dx + 0.5f);

                var color = Color.Lerp(BorderColor, Color.white, inside);
                color.a = outside;
                pixels[y * TailWidth + x] = color;
            }

            _tailSprite = Bake(pixels, TailWidth, TailHeight, Vector4.zero);
            return _tailSprite;
        }

        private static Sprite Bake(Color[] pixels, int width, int height, Vector4 border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// 둥근 사각형의 부호 거리(음수 = 안쪽).
        private static float RoundedRectDistance(float px, float py, float half, float radius)
        {
            float qx = Mathf.Abs(px) - (half - radius);
            float qy = Mathf.Abs(py) - (half - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                       Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
