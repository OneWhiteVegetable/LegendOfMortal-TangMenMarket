using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;

namespace XingShangMod.UI.Charting
{
    public sealed partial class WeeklyChartGraphic
    {
        private sealed class LabelCandidate
        {
            internal string Text;
            internal Color32 Color;
            internal Vector2 Position;
            internal bool Below;
            internal bool Latest;
        }

        private sealed class LabelGroup
        {
            internal int Index;
            internal double Value;
            internal Vector2 Position;
            internal readonly List<string> Kinds = new List<string>();
        }

        private struct PlacedLabel
        {
            internal float X;
            internal float Y;
            internal float Width;
            internal float Height;
        }

        private void RefreshLabels()
        {
            if (rectTransform == null)
                return;
            RenderContext context = CreateRenderContext();
            RefreshAxisLabels(context);
            for (int i = 0; i < _dataTags.Count; i++)
                _dataTags[i].Root.gameObject.SetActive(false);
            if ((_mode == WeeklyChartMode.Spark || _mode == WeeklyChartMode.Card || _timeline.Count == 0) ||
                (!_showRangeLabels && !_showLatestLabel))
            {
                RefreshHoverLabel();
                return;
            }

            var candidates = new List<LabelCandidate>();
            for (int seriesIndex = 0; seriesIndex < _series.Count; seriesIndex++)
            {
                ChartSeriesData item = _series[seriesIndex];
                var entries = new List<KeyValuePair<int, Vector2>>();
                double minimum = double.MaxValue;
                double maximum = double.MinValue;
                for (int i = context.First; i <= context.Last; i++)
                {
                    if (i < 0 || i >= item.AlignedPoints.Length || item.AlignedPoints[i] == null)
                        continue;
                    double value = item.AlignedPoints[i].Price;
                    minimum = Math.Min(minimum, value);
                    maximum = Math.Max(maximum, value);
                    entries.Add(new KeyValuePair<int, Vector2>(i, GetDataPosition(context, seriesIndex, i)));
                }
                if (entries.Count == 0)
                    continue;
                var groups = new Dictionary<int, LabelGroup>();
                if (_showRangeLabels)
                {
                    AddLabelGroup(groups, FindEntry(entries, item, minimum), item, "低");
                    AddLabelGroup(groups, FindEntry(entries, item, maximum), item, "高");
                }
                if (_showLatestLabel)
                    AddLabelGroup(groups, entries[entries.Count - 1].Key, item, "最新");

                foreach (LabelGroup group in groups.Values)
                {
                    var ordered = new List<string>();
                    if (group.Kinds.Contains("高"))
                        ordered.Add("高");
                    if (group.Kinds.Contains("低"))
                        ordered.Add("低");
                    if (group.Kinds.Contains("最新"))
                        ordered.Add(_mode == WeeklyChartMode.Assets
                            ? (group.Index < _timeline.Count - 1 ? "区间末" : "末次记录")
                            : "最新");
                    string prefix = _series.Count > 1
                        ? (string.IsNullOrEmpty(item.Label) ? item.Id : item.Label) + " · "
                        : string.Empty;
                    candidates.Add(new LabelCandidate
                    {
                        Text = prefix + string.Join("·", ordered.ToArray()) + " " + SeriesValue(seriesIndex, group.Value),
                        Color = GetPointColor(seriesIndex, group.Index),
                        Position = group.Position,
                        Below = ordered.Count == 1 && ordered[0] == "低",
                        Latest = group.Kinds.Contains("最新")
                    });
                }
            }

            var placed = new List<PlacedLabel>();
            int tagIndex = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                LabelCandidate candidate = candidates[i];
                ChartTag tag = EnsureDataTag(tagIndex++);
                tag.Text.text = candidate.Text;
                tag.Text.font = ResolveLabelFont();
                tag.Text.fontSize = _labelFontSize;
                tag.Text.color = Multiply(candidate.Color);
                float width = Math.Max(1f, tag.Text.preferredWidth) + 14f;
                float height = Mathf.Ceil(_labelFontSize * 1.35f) + 6f;
                if (width > context.Plot.width - 8f || height > context.Plot.height - 8f)
                {
                    tag.Root.gameObject.SetActive(false);
                    continue;
                }
                tag.Root.sizeDelta = new Vector2(width, height);
                tag.Paper.PaperColor = Paint(new Color32(246, 238, 216, 255), candidate.Latest ? 0.95f : 0.88f);
                tag.Paper.BorderColor = Paint(new Color32(108, 88, 57, 255), 0.25f);
                tag.Paper.AccentColor = Multiply(candidate.Color);
                tag.Paper.AccentWidth = 2f;
                tag.Paper.ShadowColor = new Color32(0, 0, 0, 0);
                tag.Paper.RefreshStyle();

                float desiredX = candidate.Position.x + (candidate.Latest ? 12f : 0f);
                float desiredY = candidate.Position.y + (candidate.Below ? -20f : 20f);
                float minX = context.Plot.xMin + width * 0.5f + 4f;
                float maxX = context.Plot.xMax - width * 0.5f - 4f;
                float minY = context.Plot.yMin + height * 0.5f + 4f;
                float maxY = context.Plot.yMax - height * 0.5f - 4f;
                float bestX = Mathf.Clamp(desiredX, minX, maxX);
                float bestY = Mathf.Clamp(desiredY, minY, maxY);
                float bestScore = float.MaxValue;
                bool foundBest = false;
                for (int step = 0; step < 10; step++)
                {
                    int direction = step == 0 ? 0 : step % 2 == 1 ? 1 : -1;
                    float distance = Mathf.Ceil(step / 2f) * (height + 6f);
                    float x = Mathf.Clamp(desiredX, minX, maxX);
                    float y = Mathf.Clamp(desiredY - direction * distance, minY, maxY);
                    float score = Math.Abs(y - desiredY) + Math.Abs(x - desiredX);
                    if (!foundBest || score < bestScore)
                    {
                        bestX = x;
                        bestY = y;
                        bestScore = score;
                        foundBest = true;
                    }
                    if (!LabelCollision(x, y, width, height, placed))
                    {
                        bestX = x;
                        bestY = y;
                        break;
                    }
                }
                PlaceTagCenter(tag, new Vector2(bestX, bestY));
                tag.Root.gameObject.SetActive(true);
                placed.Add(new PlacedLabel { X = bestX, Y = bestY, Width = width, Height = height });
            }
            RefreshHoverLabel();
        }

        private void RefreshAxisLabels(RenderContext context)
        {
            for (int i = 0; i < _axisLabels.Count; i++)
                _axisLabels[i].gameObject.SetActive(false);
            if (_mode == WeeklyChartMode.Spark || context.Reference == null)
                return;
            int labelIndex = 0;
            Rect parentRect = rectTransform.rect;
            Color32 timeColor = Paint(_gridColor, _mode == WeeklyChartMode.Card ? 0.54f : 0.88f);
            List<int> ticks = GetTimeTicks(context.Plot);
            for (int i = 0; i < ticks.Count; i++)
            {
                WeeklyPoint point = FindPointAt(ticks[i]);
                if (point == null)
                    continue;
                float x = TimeTickPosition(ticks[i], context.Plot);
                if (x < context.Plot.xMin - 1f || x > context.Plot.xMax + 1f)
                    continue;
                Text label = EnsureAxisLabel(labelIndex++);
                label.text = FormatAxisDate(point);
                label.font = ResolveAxisLabelFont();
                label.fontSize = _mode == WeeklyChartMode.Card ? 10 : 11;
                label.color = timeColor;
                float width = Math.Max(1f, label.preferredWidth) + 2f;
                float height = label.fontSize + 5f;
                float estimatedWidth = label.text.Length * (_mode == WeeklyChartMode.Card ? 5.8f : 6.2f);
                float pivotX = 0.5f;
                if (x - estimatedWidth * 0.5f < context.Plot.xMin)
                    pivotX = 0f;
                if (x + estimatedWidth * 0.5f > context.Plot.xMax)
                    pivotX = 1f;
                PlaceText(
                    label,
                    new Vector2(x, context.Plot.yMin - (_mode == WeeklyChartMode.Card ? 12f : 15f)),
                    new Vector2(pivotX, 0.5f),
                    new Vector2(width, height));
            }

            Color32 priceColor = Paint(_gridColor, _mode == WeeklyChartMode.Card ? 0.5f : 0.88f);
            double[] prices = GetPriceTicks(context.ReferenceMinimum, context.ReferenceMaximum, context.Plot);
            for (int i = 0; i < prices.Length; i++)
            {
                Text label = EnsureAxisLabel(labelIndex++);
                label.text = FormatValue(prices[i]);
                label.font = ResolveAxisLabelFont();
                label.fontSize = _mode == WeeklyChartMode.Card ? 10 : 11;
                label.color = priceColor;
                float width = Math.Max(1f, label.preferredWidth) + 2f;
                float height = label.fontSize + 5f;
                float y = PriceTickPosition(prices[i], context.ReferenceMinimum, context.ReferenceMaximum, context.Plot);
                bool card = _mode == WeeklyChartMode.Card;
                PlaceText(
                    label,
                    new Vector2(card ? context.Plot.xMin + 7f : context.Plot.xMin - 8f, y + (card ? 4f : 0f)),
                    new Vector2(card ? 0f : 1f, 0.5f),
                    new Vector2(width, height));
            }

            bool singleRecord = _timeline.Count == 1;
            if (_mode == WeeklyChartMode.Card && !singleRecord)
                return;
            Text note = EnsureAxisLabel(labelIndex++);
            note.font = ResolveAxisLabelFont();
            note.fontSize = _mode == WeeklyChartMode.Assets ? 11 : 10;
            note.alignment = TextAnchor.MiddleLeft;
            if (singleRecord)
            {
                note.text = _mode == WeeklyChartMode.Assets
                    ? "仅有一周记录 · 各线独立缩放"
                    : _mode == WeeklyChartMode.Card ? "仅有一周记录" : "仅有一周记录 · 不补绘历史";
                note.color = _mode == WeeklyChartMode.Assets
                    ? Paint(GetAssetColor(context.ReferenceIndex), 0.9f)
                    : Paint(_gridColor, 0.78f);
            }
            else if (_mode == WeeklyChartMode.Assets)
            {
                string name = string.IsNullOrEmpty(context.Reference.Label) ? context.Reference.Id : context.Reference.Label;
                if (name.Length > 12)
                    name = name.Substring(0, 12) + "…";
                note.text = "参考刻度：" + name + " · 各线独立缩放";
                note.color = Paint(GetAssetColor(context.ReferenceIndex), 0.9f);
            }
            else
            {
                note.text = "价格";
                note.color = Paint(_gridColor, 0.7f);
            }
            PlaceText(
                note,
                new Vector2(parentRect.xMin + 5f, parentRect.yMax - 8f),
                new Vector2(0f, 1f),
                new Vector2(Math.Max(1f, note.preferredWidth) + 2f, note.fontSize + 5f));
        }

        private void RefreshHoverLabel()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _timeline.Count || !_interactionEnabled)
            {
                HideHoverTag();
                return;
            }
            WeeklyPoint headingPoint = FindPointAt(_selectedIndex);
            if (headingPoint == null)
            {
                HideHoverTag();
                return;
            }
            if (_hoverTag == null)
                _hoverTag = CreateTag("HoverPaper");
            ChartTag tag = _hoverTag;
            tag.Text.text = FormatDate(headingPoint);
            tag.Text.font = ResolveLabelFont();
            tag.Text.fontSize = 12;
            tag.Text.alignment = TextAnchor.MiddleLeft;
            tag.Text.color = Multiply(_labelColor);
            float contentWidth = Math.Max(1f, tag.Text.preferredWidth);
            const float rowHeight = 18f;
            for (int i = 0; i < _series.Count; i++)
            {
                WeeklyPoint point = _series[i].AlignedPoints[_selectedIndex];
                string name = string.IsNullOrEmpty(_series[i].Label) ? string.Empty : _series[i].Label + "  ";
                Text row = EnsureHoverRow(i);
                row.text = "━━  " + name + (point == null ? "—" : SeriesValue(i, point.Price));
                row.font = ResolveLabelFont();
                row.fontSize = 12;
                row.alignment = TextAnchor.MiddleLeft;
                row.color = Multiply(GetPointColor(i, _selectedIndex));
                row.gameObject.SetActive(true);
                contentWidth = Math.Max(contentWidth, row.preferredWidth);
            }
            for (int i = _series.Count; i < _hoverRows.Count; i++)
                _hoverRows[i].gameObject.SetActive(false);

            tag.Paper.PaperColor = Paint(new Color32(246, 238, 216, 255), 0.96f);
            tag.Paper.BorderColor = Paint(new Color32(108, 88, 57, 255), 0.32f);
            tag.Paper.AccentWidth = 0f;
            tag.Paper.ShadowColor = Paint(new Color32(66, 49, 29, 255), 0.12f);
            tag.Paper.ShadowOffset = new Vector2(2f, -2f);
            tag.Paper.RefreshStyle();
            float width = Mathf.Clamp(contentWidth + 22f, 116f, 260f);
            float height = Math.Max(70f, 40f + _series.Count * rowHeight);
            tag.Root.sizeDelta = new Vector2(width, height);
            PlaceHoverLine(tag.Text, new Vector2(10f, -7f), new Vector2(width - 20f, 22f));
            for (int i = 0; i < _series.Count; i++)
            {
                PlaceHoverLine(
                    _hoverRows[i],
                    new Vector2(10f, -32f - i * rowHeight),
                    new Vector2(width - 20f, rowHeight));
            }

            Rect bounds = rectTransform.rect;
            Rect plot = GetPlotRect();
            float x = TimeTickPosition(_selectedIndex, plot);
            float left = x + 13f;
            if (left + width > bounds.xMax - 5f)
                left = x - width - 13f;
            left = Mathf.Clamp(left, bounds.xMin + 5f, Math.Max(bounds.xMin + 5f, bounds.xMax - width - 5f));
            Vector2 pointer = _hasPointerLocal ? _lastPointerLocal : new Vector2(x, plot.center.y);
            float bottom = Mathf.Clamp(
                pointer.y + 12f,
                bounds.yMin + 5f,
                Math.Max(bounds.yMin + 5f, bounds.yMax - height - 5f));
            PlaceTagBottomLeft(tag, new Vector2(left, bottom));
            tag.Root.SetAsLastSibling();
            tag.Root.gameObject.SetActive(true);
        }

        private Text EnsureHoverRow(int index)
        {
            while (_hoverRows.Count <= index)
            {
                Text row = CreateText("HoverSeries");
                row.rectTransform.SetParent(_hoverTag.Root, false);
                _hoverRows.Add(row);
            }
            return _hoverRows[index];
        }

        private static void PlaceHoverLine(Text text, Vector2 position, Vector2 size)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void HideHoverTag()
        {
            if (!_isDestroying && _hoverTag != null && _hoverTag.Root != null)
                _hoverTag.Root.gameObject.SetActive(false);
        }

        private static int FindEntry(List<KeyValuePair<int, Vector2>> entries, ChartSeriesData item, double value)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (item.AlignedPoints[entries[i].Key].Price == value)
                    return entries[i].Key;
            }
            return entries[0].Key;
        }

        private void AddLabelGroup(
            Dictionary<int, LabelGroup> groups,
            int index,
            ChartSeriesData item,
            string kind)
        {
            LabelGroup group;
            if (!groups.TryGetValue(index, out group))
            {
                RenderContext context = CreateRenderContext();
                int seriesIndex = _series.IndexOf(item);
                group = new LabelGroup
                {
                    Index = index,
                    Value = item.AlignedPoints[index].Price,
                    Position = GetDataPosition(context, seriesIndex, index)
                };
                groups.Add(index, group);
            }
            group.Kinds.Add(kind);
        }

        private static bool LabelCollision(float x, float y, float width, float height, List<PlacedLabel> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedLabel item = placed[i];
                if (Math.Abs(x - item.X) < (width + item.Width) * 0.5f + 4f &&
                    Math.Abs(y - item.Y) < (height + item.Height) * 0.5f + 3f)
                    return true;
            }
            return false;
        }

        private Text EnsureAxisLabel(int index)
        {
            while (_axisLabels.Count <= index)
            {
                Text text = CreateText("AxisLabel");
                _axisLabels.Add(text);
            }
            Text label = _axisLabels[index];
            label.gameObject.SetActive(true);
            return label;
        }

        private ChartTag EnsureDataTag(int index)
        {
            while (_dataTags.Count <= index)
                _dataTags.Add(CreateTag("DataPaper"));
            return _dataTags[index];
        }

        private Text CreateText(string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = this.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.localScale = Vector3.one;
            EnsureCanvasRenderer(gameObject);
            Text text = gameObject.AddComponent<Text>();
            text.font = ResolveLabelFont();
            text.fontSize = _labelFontSize;
            text.color = _labelColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private ChartTag CreateTag(string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = this.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            EnsureCanvasRenderer(gameObject);
            var paper = gameObject.AddComponent<WeeklyChartPaperGraphic>();
            var tag = new ChartTag { Root = rect, Paper = paper, Text = CreateText(name + "Text") };
            tag.Text.rectTransform.SetParent(rect, false);
            StretchRect(tag.Text.rectTransform, new Vector2(0.5f, 0.5f));
            tag.Text.rectTransform.offsetMin = new Vector2(7f, 3f);
            tag.Text.rectTransform.offsetMax = new Vector2(-7f, -3f);
            return tag;
        }

        private void PlaceText(Text label, Vector2 localPosition, Vector2 pivot, Vector2 size)
        {
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = localPosition - rectTransform.rect.min;
            label.gameObject.SetActive(true);
        }

        private void PlaceTagCenter(ChartTag tag, Vector2 localPosition)
        {
            tag.Root.anchorMin = Vector2.zero;
            tag.Root.anchorMax = Vector2.zero;
            tag.Root.pivot = new Vector2(0.5f, 0.5f);
            tag.Root.anchoredPosition = localPosition - rectTransform.rect.min;
        }

        private void PlaceTagBottomLeft(ChartTag tag, Vector2 localPosition)
        {
            tag.Root.anchorMin = Vector2.zero;
            tag.Root.anchorMax = Vector2.zero;
            tag.Root.pivot = Vector2.zero;
            tag.Root.anchoredPosition = localPosition - rectTransform.rect.min;
        }

        private void ApplyLabelFont()
        {
            Font font = ResolveLabelFont();
            Font axisFont = ResolveAxisLabelFont();
            for (int i = 0; i < _axisLabels.Count; i++)
                _axisLabels[i].font = axisFont;
            for (int i = 0; i < _dataTags.Count; i++)
                _dataTags[i].Text.font = font;
            if (_hoverTag != null)
                _hoverTag.Text.font = font;
            for (int i = 0; i < _hoverRows.Count; i++)
                _hoverRows[i].font = font;
        }

        private void ApplyLabelFontSize()
        {
            for (int i = 0; i < _dataTags.Count; i++)
                _dataTags[i].Text.fontSize = _labelFontSize;
        }
    }
}
