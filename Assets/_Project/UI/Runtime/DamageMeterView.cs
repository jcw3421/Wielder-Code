using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// 간단 표의 한 줄. 계산·서식은 Presenter가 끝내고 문자열로 넘긴다 — View는 배치만 한다.
    public struct DamageMeterRow
    {
        public string Rank;
        public string Name;
        public string Percent;
        public string Dps;
        public string Damage;
        public float Share;   // 0~1. 행 배경 막대 길이
    }

    /// 상세 표의 한 줄. IsHeader면 플레이어 구분 헤더, 아니면 그 플레이어의 피해 종류별 집계.
    public struct DamageMeterDetailRow
    {
        public bool IsHeader;
        public int ColorIndex;   // 헤더가 쓰는 플레이어 색. 데이터 줄은 -1.
        public string Label;
        public string Amount;
        public string Share;
        public string Average;
        public string Max;
        public string Hits;
        public float Fill;       // 0~1. 그 플레이어 안에서의 비중 막대
    }

    public sealed class DamageMeterView : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private const float HeaderHeight = 26f;
        private const float ColumnHeaderHeight = 18f;
        private const float RowHeight = 22f;
        private const float DetailRowHeight = 18f;
        private const float BodyPadding = 6f;
        private const float HandleSize = 20f;
        private const float RowFillAlpha = 0.30f;

        // # / 닉네임 / 기여 / DPS / 피해
        private static readonly float[] SimpleColumns = { 0f, 0.08f, 0.42f, 0.56f, 0.78f, 1f };
        // 구분 / 딜량 / 지분 / 평균 / 최대 / 타수
        private static readonly float[] DetailColumns = { 0f, 0.30f, 0.48f, 0.60f, 0.75f, 0.88f, 1f };

        private static readonly Color[] Palette =
        {
            new Color(0.36f, 0.75f, 0.98f),
            new Color(0.98f, 0.62f, 0.30f),
            new Color(0.55f, 0.85f, 0.45f),
            new Color(0.86f, 0.51f, 0.90f),
            new Color(0.98f, 0.85f, 0.35f),
            new Color(0.95f, 0.45f, 0.48f),
        };
        private static readonly Vector2 MinSize = new Vector2(240f, 100f);

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset _font;

        private sealed class Surface
        {
            public Image Image;
            public float BaseAlpha;
        }

        private sealed class RowWidgets
        {
            public RectTransform Root;
            public RectTransform FillRect;
            public Image Fill;
            public Image Accent;
            public TMP_Text Rank, Name, Percent, Dps, Damage;
        }

        private sealed class DetailWidgets
        {
            public RectTransform Root;
            public RectTransform FillRect;
            public Image Fill;
            public Image Divider;
            public TMP_Text Label, Amount, Share, Average, Max, Hits;
        }

        private RectTransform _panel;
        private RectTransform _body;
        private RectTransform _rowsArea;
        private RectTransform _detailArea;
        private RectTransform _detailRowsArea;
        private RectTransform _resizeHandle;
        private TMP_Text _title;
        private TMP_Text _emptyLabel;
        private TMP_Text _collapseLabel, _detailLabel, _opacityLabel;
        private Button _collapseButton, _detailButton, _resetButton, _opacityButton;

        private readonly List<Surface> _surfaces = new List<Surface>(16);
        private readonly List<RowWidgets> _rows = new List<RowWidgets>(4);
        private readonly List<DetailWidgets> _detailRows = new List<DetailWidgets>(24);

        private Vector2 _dragStartPointer;
        private Vector2 _dragStartPosition;
        private Vector2 _resizeStartPointer;
        private Vector2 _resizeStartSize;
        private Vector2 _resizeStartPosition;

        // 모드별로 따로 기억한다 — 상세에서 키운 크기가 간단히로 따라오면 안 된다.
        private Vector2 _simpleSize = new Vector2(400f, 168f);
        private Vector2 _detailSize = new Vector2(620f, 460f);
        private bool _showDetails;
        private float _surfaceAlpha = 0.75f;

        public event Action CollapseRequested;
        public event Action DetailRequested;
        public event Action ResetRequested;
        public bool IsCollapsed { get; private set; }

        private Vector2 CurrentSize
        {
            get => _showDetails ? _detailSize : _simpleSize;
            set { if (_showDetails) _detailSize = value; else _simpleSize = value; }
        }

        private void Awake()
        {
            BuildLayout();
            _collapseButton.onClick.AddListener(() => CollapseRequested?.Invoke());
            _detailButton.onClick.AddListener(() => DetailRequested?.Invoke());
            _resetButton.onClick.AddListener(() => ResetRequested?.Invoke());
            _opacityButton.onClick.AddListener(CycleOpacity);
            ApplySurfaceAlpha();
            Resize();
        }

        public void SetContent(string title, List<DamageMeterRow> rows, List<DamageMeterDetailRow> details,
            bool showDetails)
        {
            _title.text = title;

            if (showDetails != _showDetails)
            {
                _showDetails = showDetails;
                _detailLabel.text = showDetails ? "간단히" : "상세";
                _detailArea.gameObject.SetActive(showDetails);
            }

            BindRows(rows);
            if (showDetails) BindDetails(details);
            Resize();   // 모드마다 따로 기억해 둔 크기를 다시 적용한다
        }

        public void SetCollapsed(bool collapsed)
        {
            IsCollapsed = collapsed;
            _body.gameObject.SetActive(!collapsed);
            _resizeHandle.gameObject.SetActive(!collapsed);
            _collapseLabel.text = collapsed ? "+" : "−";
            Resize();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStartPointer = eventData.position;
            _dragStartPosition = _panel.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _panel.anchoredPosition = _dragStartPosition + eventData.position - _dragStartPointer;
            ClampToCanvas();
        }

        private void BindRows(List<DamageMeterRow> rows)
        {
            int count = rows.Count;
            while (_rows.Count < count) _rows.Add(CreateRow(_rows.Count));

            for (int i = 0; i < _rows.Count; i++)
            {
                RowWidgets widgets = _rows[i];
                bool used = i < count;
                if (widgets.Root.gameObject.activeSelf != used) widgets.Root.gameObject.SetActive(used);
                if (!used) continue;

                DamageMeterRow row = rows[i];
                widgets.Rank.text = row.Rank;
                widgets.Name.text = row.Name;
                widgets.Percent.text = row.Percent;
                widgets.Dps.text = row.Dps;
                widgets.Damage.text = row.Damage;

                Color color = Palette[i % Palette.Length];
                widgets.Accent.color = color;
                widgets.Name.color = color;   // 이름 색 = 막대 색. 어느 막대가 누구 것인지 바로 붙는다
                widgets.Fill.color = new Color(color.r, color.g, color.b, RowFillAlpha * _surfaceAlpha);
                widgets.FillRect.anchorMax = new Vector2(Mathf.Clamp01(row.Share), 1f);
            }

            _emptyLabel.gameObject.SetActive(count == 0);
            _rowsArea.sizeDelta = new Vector2(0f, count * RowHeight);
            _detailArea.offsetMax = new Vector2(0f, -(ColumnHeaderHeight + count * RowHeight + 6f));
        }

        private void BindDetails(List<DamageMeterDetailRow> details)
        {
            int count = details.Count;
            while (_detailRows.Count < count) _detailRows.Add(CreateDetailRow(_detailRows.Count));

            for (int i = 0; i < _detailRows.Count; i++)
            {
                DetailWidgets widgets = _detailRows[i];
                bool used = i < count;
                if (widgets.Root.gameObject.activeSelf != used) widgets.Root.gameObject.SetActive(used);
                if (!used) continue;

                DamageMeterDetailRow row = details[i];
                widgets.Label.text = row.Label;
                widgets.Amount.text = row.Amount;
                widgets.Share.text = row.Share;
                widgets.Average.text = row.Average;
                widgets.Max.text = row.Max;
                widgets.Hits.text = row.Hits;

                Color color = row.IsHeader ? Palette[Mathf.Abs(row.ColorIndex) % Palette.Length]
                            : new Color(0.78f, 0.86f, 0.9f);
                widgets.Label.color = color;
                widgets.Label.fontStyle = row.IsHeader ? FontStyles.Bold : FontStyles.Normal;
                widgets.Divider.gameObject.SetActive(row.IsHeader);
                widgets.Fill.color = new Color(color.r, color.g, color.b, RowFillAlpha * 0.7f * _surfaceAlpha);
                widgets.FillRect.anchorMax = new Vector2(Mathf.Clamp01(row.Fill), 1f);
            }

            _detailRowsArea.sizeDelta = new Vector2(0f, count * DetailRowHeight);
        }

        private void Resize()
        {
            var canvasRect = (RectTransform)_panel.parent;
            Vector2 size = IsCollapsed ? new Vector2(CurrentSize.x, HeaderHeight) : CurrentSize;
            size.x = Mathf.Clamp(size.x, MinSize.x, Mathf.Max(MinSize.x, canvasRect.rect.width));
            size.y = Mathf.Clamp(size.y, IsCollapsed ? HeaderHeight : MinSize.y,
                Mathf.Max(HeaderHeight, canvasRect.rect.height));
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            ClampToCanvas();
        }

        private void BeginResize(PointerEventData eventData)
        {
            if (IsCollapsed) return;
            _resizeStartPointer = eventData.position;
            _resizeStartSize = _panel.rect.size;
            _resizeStartPosition = _panel.anchoredPosition;
        }

        // 패널 피벗이 우상단이라 폭을 키우면 왼쪽으로 자란다. 오른쪽 아래 손잡이를 잡은 만큼
        // 위치를 같이 밀어, 잡은 모서리가 포인터를 따라오게 한다.
        private void ResizeDrag(PointerEventData eventData)
        {
            if (IsCollapsed) return;
            var canvasRect = (RectTransform)_panel.parent;
            Vector2 delta = eventData.position - _resizeStartPointer;
            Vector2 size;
            size.x = Mathf.Clamp(_resizeStartSize.x + delta.x, MinSize.x, Mathf.Max(MinSize.x, canvasRect.rect.width));
            size.y = Mathf.Clamp(_resizeStartSize.y - delta.y, MinSize.y, Mathf.Max(MinSize.y, canvasRect.rect.height));
            CurrentSize = size;
            _panel.anchoredPosition = _resizeStartPosition + new Vector2(size.x - _resizeStartSize.x, 0f);
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            ClampToCanvas();
        }

        private void CycleOpacity()
        {
            _surfaceAlpha += 0.25f;
            if (_surfaceAlpha > 1f) _surfaceAlpha = 0.25f;
            ApplySurfaceAlpha();
        }

        private void ApplySurfaceAlpha()
        {
            for (int i = 0; i < _surfaces.Count; i++)
            {
                Surface surface = _surfaces[i];
                Color color = surface.Image.color;
                color.a = surface.BaseAlpha * _surfaceAlpha;
                surface.Image.color = color;
            }
            if (_opacityLabel != null)
                _opacityLabel.text = $"{Mathf.RoundToInt(_surfaceAlpha * 100f)}%";
        }

        private void ClampToCanvas()
        {
            var canvasRect = (RectTransform)_panel.parent;
            Vector2 pos = _panel.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -canvasRect.rect.width + _panel.rect.width, 0f);
            pos.y = Mathf.Clamp(pos.y, -canvasRect.rect.height + _panel.rect.height, 0f);
            _panel.anchoredPosition = pos;
        }

        private void BuildLayout()
        {
            _panel = CreateRect("Panel", transform, _simpleSize);
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = Vector2.one;
            _panel.anchoredPosition = new Vector2(-24f, -24f);
            AddSurface(_panel, new Color(0.025f, 0.04f, 0.06f), 1f);
            var outline = _panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.56f, 0.66f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            BuildHeader();

            _body = CreateRect("Body", _panel, Vector2.zero);
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(BodyPadding, BodyPadding);
            _body.offsetMax = new Vector2(-BodyPadding, -(HeaderHeight + 2f));
            // 패널을 작게 줄이면 줄이 넘친다. 밖으로 새지 않게 여기서 자른다.
            _body.gameObject.AddComponent<RectMask2D>();

            BuildSimpleTable();
            BuildDetailTable();
            BuildResizeHandle();
        }

        private void BuildHeader()
        {
            RectTransform header = CreateRect("Header", _panel, Vector2.zero);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(0f, -HeaderHeight);
            header.offsetMax = Vector2.zero;
            AddSurface(header, new Color(0.06f, 0.18f, 0.24f), 1f);

            RectTransform underline = CreateRect("Underline", header, Vector2.zero);
            underline.anchorMin = Vector2.zero;
            underline.anchorMax = new Vector2(1f, 0f);
            underline.pivot = new Vector2(0.5f, 0f);
            underline.offsetMin = Vector2.zero;
            underline.offsetMax = new Vector2(0f, 2f);
            AddSurface(underline, new Color(0.18f, 0.62f, 0.72f), 0.9f).raycastTarget = false;

            var dragTrigger = header.gameObject.AddComponent<EventTrigger>();
            AddDragEntry(dragTrigger, EventTriggerType.BeginDrag, data => OnBeginDrag((PointerEventData)data));
            AddDragEntry(dragTrigger, EventTriggerType.Drag, data => OnDrag((PointerEventData)data));

            float right = -4f;
            _collapseButton = CreateHeaderButton("Collapse", header, 22f, ref right, "−", out _collapseLabel);
            _detailButton = CreateHeaderButton("Detail", header, 44f, ref right, "상세", out _detailLabel);
            _resetButton = CreateHeaderButton("Reset", header, 46f, ref right, "초기화", out _);
            _opacityButton = CreateHeaderButton("Opacity", header, 36f, ref right, "75%", out _opacityLabel);

            _title = CreateText("Title", header, 13f, TextAlignmentOptions.MidlineLeft);
            _title.rectTransform.offsetMin = new Vector2(10f, 0f);
            _title.rectTransform.offsetMax = new Vector2(right - 4f, 0f);
        }

        private void BuildSimpleTable()
        {
            RectTransform columnHeader = CreateRect("ColumnHeader", _body, Vector2.zero);
            columnHeader.anchorMin = new Vector2(0f, 1f);
            columnHeader.anchorMax = new Vector2(1f, 1f);
            columnHeader.pivot = new Vector2(0.5f, 1f);
            columnHeader.sizeDelta = new Vector2(0f, ColumnHeaderHeight);
            AddSurface(columnHeader, new Color(0.05f, 0.12f, 0.16f), 1f).raycastTarget = false;

            string[] labels = { "#", "닉네임", "기여", "DPS", "피해" };
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text cell = CreateCell(columnHeader, SimpleColumns[i], SimpleColumns[i + 1], 11f, Alignment(i));
                cell.text = labels[i];
                cell.color = new Color(0.55f, 0.7f, 0.78f);
            }

            _rowsArea = CreateRect("Rows", _body, Vector2.zero);
            _rowsArea.anchorMin = new Vector2(0f, 1f);
            _rowsArea.anchorMax = new Vector2(1f, 1f);
            _rowsArea.pivot = new Vector2(0.5f, 1f);
            _rowsArea.anchoredPosition = new Vector2(0f, -ColumnHeaderHeight);
            _rowsArea.sizeDelta = Vector2.zero;

            _emptyLabel = CreateText("Empty", _body, 12f, TextAlignmentOptions.Top);
            _emptyLabel.rectTransform.offsetMin = new Vector2(0f, 0f);
            _emptyLabel.rectTransform.offsetMax = new Vector2(0f, -(ColumnHeaderHeight + 10f));
            _emptyLabel.color = new Color(0.45f, 0.55f, 0.62f);
            _emptyLabel.text = "전투 피해 기록 없음";
        }

        private void BuildDetailTable()
        {
            _detailArea = CreateRect("Details", _body, Vector2.zero);
            _detailArea.anchorMin = Vector2.zero;
            _detailArea.anchorMax = Vector2.one;
            _detailArea.offsetMin = Vector2.zero;
            _detailArea.offsetMax = new Vector2(0f, -(ColumnHeaderHeight + 6f));

            RectTransform columnHeader = CreateRect("DetailColumnHeader", _detailArea, Vector2.zero);
            columnHeader.anchorMin = new Vector2(0f, 1f);
            columnHeader.anchorMax = new Vector2(1f, 1f);
            columnHeader.pivot = new Vector2(0.5f, 1f);
            columnHeader.sizeDelta = new Vector2(0f, DetailRowHeight);
            AddSurface(columnHeader, new Color(0.05f, 0.12f, 0.16f), 1f).raycastTarget = false;

            string[] labels = { "구분", "딜량", "지분", "평균", "최대", "타수" };
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text cell = CreateCell(columnHeader, DetailColumns[i], DetailColumns[i + 1], 10f,
                    i == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight);
                cell.text = labels[i];
                cell.color = new Color(0.55f, 0.7f, 0.78f);
            }

            _detailRowsArea = CreateRect("DetailRows", _detailArea, Vector2.zero);
            _detailRowsArea.anchorMin = new Vector2(0f, 1f);
            _detailRowsArea.anchorMax = new Vector2(1f, 1f);
            _detailRowsArea.pivot = new Vector2(0.5f, 1f);
            _detailRowsArea.anchoredPosition = new Vector2(0f, -DetailRowHeight);
            _detailRowsArea.sizeDelta = Vector2.zero;

            _detailArea.gameObject.SetActive(false);
        }

        // 오른쪽 아래 모서리에 딱 붙는 손잡이. 빗금은 끄는 방향(↘)에 수직인 ↗ 방향이라
        // 모서리에서 안쪽으로 갈수록 길어지는 삼각형이 된다.
        private void BuildResizeHandle()
        {
            _resizeHandle = CreateRect("ResizeHandle", _panel, new Vector2(HandleSize, HandleSize));
            _resizeHandle.anchorMin = _resizeHandle.anchorMax = _resizeHandle.pivot = new Vector2(1f, 0f);
            _resizeHandle.anchoredPosition = Vector2.zero;
            Image hitArea = _resizeHandle.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0.01f);
            hitArea.raycastTarget = true;

            var resizeTrigger = _resizeHandle.gameObject.AddComponent<EventTrigger>();
            AddDragEntry(resizeTrigger, EventTriggerType.BeginDrag, data => BeginResize((PointerEventData)data));
            AddDragEntry(resizeTrigger, EventTriggerType.Drag, data => ResizeDrag((PointerEventData)data));

            const float diagonal = 0.70710678f;   // cos 45°
            for (int i = 0; i < 3; i++)
            {
                float distance = 4f + i * 4f;                  // 모서리에서 빗금까지의 수직 거리
                float length = 2f * distance - 1f;             // 직각이등변 삼각형의 현 길이
                RectTransform line = CreateRect($"GripLine{i + 1}", _resizeHandle, new Vector2(length, 2f));
                line.anchorMin = line.anchorMax = new Vector2(1f, 0f);
                line.pivot = new Vector2(0.5f, 0.5f);
                line.anchoredPosition = new Vector2(-distance * diagonal, distance * diagonal);
                line.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image lineImage = line.gameObject.AddComponent<Image>();
                lineImage.color = new Color(0.3f, 0.78f, 0.86f, 0.95f);
                lineImage.raycastTarget = false;
            }
        }

        private RowWidgets CreateRow(int index)
        {
            var widgets = new RowWidgets();
            widgets.Root = CreateRect($"Row{index}", _rowsArea, Vector2.zero);
            widgets.Root.anchorMin = new Vector2(0f, 1f);
            widgets.Root.anchorMax = new Vector2(1f, 1f);
            widgets.Root.pivot = new Vector2(0.5f, 1f);
            widgets.Root.anchoredPosition = new Vector2(0f, -index * RowHeight);
            widgets.Root.sizeDelta = new Vector2(0f, RowHeight);
            AddSurface(widgets.Root, new Color(0.035f, 0.09f, 0.12f), 1f).raycastTarget = false;

            // 기여도 막대. anchorMax.x만 움직이면 폭을 다시 계산하지 않아도 패널 크기를 따라간다.
            widgets.FillRect = CreateRect("Fill", widgets.Root, Vector2.zero);
            widgets.FillRect.anchorMin = Vector2.zero;
            widgets.FillRect.anchorMax = new Vector2(0f, 1f);
            widgets.FillRect.pivot = new Vector2(0f, 0.5f);
            widgets.FillRect.offsetMin = widgets.FillRect.offsetMax = Vector2.zero;
            widgets.Fill = widgets.FillRect.gameObject.AddComponent<Image>();
            widgets.Fill.raycastTarget = false;

            RectTransform accent = CreateRect("Accent", widgets.Root, Vector2.zero);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.offsetMin = new Vector2(0f, 2f);
            accent.offsetMax = new Vector2(3f, -2f);
            widgets.Accent = accent.gameObject.AddComponent<Image>();
            widgets.Accent.raycastTarget = false;

            // 플레이어끼리의 구분선.
            RectTransform divider = CreateRect("Divider", widgets.Root, Vector2.zero);
            divider.anchorMin = Vector2.zero;
            divider.anchorMax = new Vector2(1f, 0f);
            divider.pivot = new Vector2(0.5f, 0f);
            divider.offsetMin = Vector2.zero;
            divider.offsetMax = new Vector2(0f, 1f);
            AddSurface(divider, new Color(0.20f, 0.42f, 0.50f), 0.85f).raycastTarget = false;

            widgets.Rank = CreateCell(widgets.Root, SimpleColumns[0], SimpleColumns[1], 12f, Alignment(0));
            widgets.Name = CreateCell(widgets.Root, SimpleColumns[1], SimpleColumns[2], 13f, Alignment(1));
            widgets.Percent = CreateCell(widgets.Root, SimpleColumns[2], SimpleColumns[3], 13f, Alignment(2));
            widgets.Dps = CreateCell(widgets.Root, SimpleColumns[3], SimpleColumns[4], 13f, Alignment(3));
            widgets.Damage = CreateCell(widgets.Root, SimpleColumns[4], SimpleColumns[5], 13f, Alignment(4));
            return widgets;
        }

        private DetailWidgets CreateDetailRow(int index)
        {
            var widgets = new DetailWidgets();
            widgets.Root = CreateRect($"DetailRow{index}", _detailRowsArea, Vector2.zero);
            widgets.Root.anchorMin = new Vector2(0f, 1f);
            widgets.Root.anchorMax = new Vector2(1f, 1f);
            widgets.Root.pivot = new Vector2(0.5f, 1f);
            widgets.Root.anchoredPosition = new Vector2(0f, -index * DetailRowHeight);
            widgets.Root.sizeDelta = new Vector2(0f, DetailRowHeight);

            widgets.FillRect = CreateRect("Fill", widgets.Root, Vector2.zero);
            widgets.FillRect.anchorMin = Vector2.zero;
            widgets.FillRect.anchorMax = new Vector2(0f, 1f);
            widgets.FillRect.pivot = new Vector2(0f, 0.5f);
            widgets.FillRect.offsetMin = widgets.FillRect.offsetMax = Vector2.zero;
            widgets.Fill = widgets.FillRect.gameObject.AddComponent<Image>();
            widgets.Fill.raycastTarget = false;

            // 플레이어 구분 헤더 위에만 켠다 — 상세 표에서도 사람 단위 경계가 보여야 한다.
            RectTransform divider = CreateRect("Divider", widgets.Root, Vector2.zero);
            divider.anchorMin = new Vector2(0f, 1f);
            divider.anchorMax = new Vector2(1f, 1f);
            divider.pivot = new Vector2(0.5f, 1f);
            divider.offsetMin = Vector2.zero;
            divider.offsetMax = new Vector2(0f, 1f);
            widgets.Divider = AddSurface(divider, new Color(0.20f, 0.42f, 0.50f), 0.85f);
            widgets.Divider.raycastTarget = false;

            widgets.Label = CreateCell(widgets.Root, DetailColumns[0], DetailColumns[1], 11f, TextAlignmentOptions.MidlineLeft);
            widgets.Amount = CreateCell(widgets.Root, DetailColumns[1], DetailColumns[2], 11f, TextAlignmentOptions.MidlineRight);
            widgets.Share = CreateCell(widgets.Root, DetailColumns[2], DetailColumns[3], 11f, TextAlignmentOptions.MidlineRight);
            widgets.Average = CreateCell(widgets.Root, DetailColumns[3], DetailColumns[4], 11f, TextAlignmentOptions.MidlineRight);
            widgets.Max = CreateCell(widgets.Root, DetailColumns[4], DetailColumns[5], 11f, TextAlignmentOptions.MidlineRight);
            widgets.Hits = CreateCell(widgets.Root, DetailColumns[5], DetailColumns[6], 11f, TextAlignmentOptions.MidlineRight);
            return widgets;
        }

        private static TextAlignmentOptions Alignment(int column)
            => column == 0 ? TextAlignmentOptions.Midline
             : column == 1 ? TextAlignmentOptions.MidlineLeft
             : TextAlignmentOptions.MidlineRight;

        private TMP_Text CreateCell(Transform parent, float minX, float maxX, float size, TextAlignmentOptions alignment)
        {
            TMP_Text text = CreateText("Cell", parent, size, alignment);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.offsetMin = new Vector2(4f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            return text;
        }

        private Button CreateHeaderButton(string name, Transform parent, float width, ref float right, string label,
            out TMP_Text labelText)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(width, 20f));
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(right, -3f);
            AddSurface(rect, new Color(0.1f, 0.35f, 0.44f), 1f);
            Button button = rect.gameObject.AddComponent<Button>();
            labelText = CreateText("Label", rect, 11f, TextAlignmentOptions.Center);
            labelText.text = label;
            right -= width + 3f;
            return button;
        }

        private TMP_Text CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private Image AddSurface(RectTransform rect, Color color, float baseAlpha)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = new Color(color.r, color.g, color.b, baseAlpha * _surfaceAlpha);
            _surfaces.Add(new Surface { Image = image, BaseAlpha = baseAlpha });
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            return rect;
        }

        private static void AddDragEntry(EventTrigger trigger, EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }
    }
}
