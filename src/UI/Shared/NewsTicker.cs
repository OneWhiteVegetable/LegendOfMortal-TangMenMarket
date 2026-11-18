using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Shared
{
    public sealed class NewsTicker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float LabelWidth = 176f;
        private const float ItemSpacing = 54f;
        private readonly List<Text> _items = new List<Text>();
        private RectTransform _track;
        private RectTransform _viewport;
        private float _groupWidth;
        private float _speed = 76f;
        private bool _paused;

        public RectTransform Root { get; private set; }
        public bool IsPaused { get { return _paused; } }
        public float Speed
        {
            get { return _speed; }
            set { _speed = Mathf.Max(0f, value); }
        }

        public static NewsTicker Create(UiFactory factory, Transform parent)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }
            if (parent == null)
            {
                throw new ArgumentNullException("parent");
            }

            Image root = factory.CreatePanel(
                parent,
                "SharedNewsTicker",
                null,
                null,
                new Color(0.12f, 0.13f, 0.12f, 0.98f));
            root.raycastTarget = true;
            NewsTicker ticker = root.gameObject.AddComponent<NewsTicker>();
            ticker.Root = root.rectTransform;
            UiFactory.SetRect(
                ticker.Root,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -104f),
                new Vector2(0f, 42f));

            Image label = factory.CreateImage(
                root.transform,
                "Label",
                "panel_ticker_label",
                new Vector2(LabelWidth, 42f));
            UiFactory.SetRect(
                label.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(LabelWidth, 0f));
            Text labelText = factory.CreateText(
                label.transform,
                "Text",
                "江湖快報",
                20,
                UiFormatting.Paper,
                TextAnchor.MiddleCenter);
            UiFactory.Stretch(labelText.rectTransform, new Vector2(18f, 2f), new Vector2(-12f, -2f));

            RectTransform viewport = UiFactory.CreateRect("Viewport", root.transform);
            UiFactory.Stretch(viewport, new Vector2(LabelWidth, 0f), Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            ticker._viewport = viewport;
            ticker._track = UiFactory.CreateRect("Track", viewport);
            ticker._track.anchorMin = new Vector2(0f, 0.5f);
            ticker._track.anchorMax = new Vector2(0f, 0.5f);
            ticker._track.pivot = new Vector2(0f, 0.5f);
            ticker._track.anchoredPosition = Vector2.zero;
            root.gameObject.SetActive(false);
            return ticker;
        }

        public void SetNews(RunState state, IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            if (state == null)
            {
                Clear();
                return;
            }
            int currentWeek = state.Clock != null ? state.Clock.MarketWeekIndex : -1;
            List<NewsRecord> appeared = new List<NewsRecord>();
            if (state.NewsHistory != null)
            {
                for (int i = 0; i < state.NewsHistory.Count; i++)
                {
                    NewsRecord record = state.NewsHistory[i];
                    if (record != null && state.HasItemAppeared(record.ItemId))
                    {
                        appeared.Add(record);
                    }
                }
            }
            SetNews(
                appeared,
                currentWeek,
                itemId =>
                {
                    ItemDefinition definition;
                    return definitions != null && itemId != null && definitions.TryGetValue(itemId, out definition) && definition != null
                        ? definition.Name
                        : itemId;
                });
        }

        public void SetNews(
            IEnumerable<NewsRecord> records,
            int currentWeek,
            Func<string, string> itemNameResolver)
        {
            ClearItems();
            if (records == null)
            {
                Root.gameObject.SetActive(false);
                return;
            }

            List<NewsRecord> visible = new List<NewsRecord>();
            foreach (NewsRecord record in records)
            {
                if (record != null && record.MarketWeekIndex <= currentWeek && !string.IsNullOrWhiteSpace(record.Text))
                {
                    visible.Add(record);
                }
            }
            visible.Sort(CompareNews);
            if (visible.Count > 32)
            {
                visible.RemoveRange(0, visible.Count - 32);
            }
            if (visible.Count == 0)
            {
                Root.gameObject.SetActive(false);
                return;
            }

            Root.gameObject.SetActive(true);
            _groupWidth = BuildGroup(visible, itemNameResolver, 0f);
            BuildGroup(visible, itemNameResolver, _groupWidth);
            _track.sizeDelta = new Vector2(_groupWidth * 2f, 42f);
            _track.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        public void Clear()
        {
            ClearItems();
            if (Root != null)
            {
                Root.gameObject.SetActive(false);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _paused = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _paused = false;
        }

        private void Update()
        {
            if (_paused || _groupWidth <= 0f || _track == null)
            {
                return;
            }
            Vector2 position = _track.anchoredPosition;
            position.x -= _speed * Time.unscaledDeltaTime;
            if (position.x <= -_groupWidth)
            {
                position.x += _groupWidth;
            }
            _track.anchoredPosition = position;
        }

        private float BuildGroup(
            IList<NewsRecord> records,
            Func<string, string> itemNameResolver,
            float offset)
        {
            float x = offset;
            for (int i = 0; i < records.Count; i++)
            {
                NewsRecord record = records[i];
                string itemName = itemNameResolver == null ? record.ItemId : itemNameResolver(record.ItemId);
                string direction = record.Direction >= 0 ? "利多" : "利空";
                string content = direction + " · " + itemName + " " + UiFormatting.Percent(record.Return) + "　" + record.Text;
                Text text = CreateTickerText(content, record.Direction >= 0 ? UiFormatting.Rise : UiFormatting.Fall);
                float width = Mathf.Ceil(text.preferredWidth) + ItemSpacing;
                UiFactory.SetRect(
                    text.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(x, 0f),
                    new Vector2(width, 42f));
                x += width;
            }
            return x - offset;
        }

        private Text CreateTickerText(string content, Color color)
        {
            GameObject gameObject = new GameObject("News", typeof(RectTransform));
            int layer = LayerMask.NameToLayer("UI");
            if (layer >= 0)
            {
                gameObject.layer = layer;
            }
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(_track, false);
            Text text = gameObject.AddComponent<Text>();
            Text labelText = Root.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                text.font = labelText.font;
            }
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.text = content;
            text.color = color;
            _items.Add(text);
            return text;
        }

        private void ClearItems()
        {
            _paused = false;
            _groupWidth = 0f;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                {
                    _items[i].gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(_items[i].gameObject);
                }
            }
            _items.Clear();
            if (_track != null)
            {
                _track.anchoredPosition = Vector2.zero;
                _track.sizeDelta = new Vector2(0f, 42f);
            }
        }

        private static int CompareNews(NewsRecord left, NewsRecord right)
        {
            int week = left.MarketWeekIndex.CompareTo(right.MarketWeekIndex);
            if (week != 0)
            {
                return week;
            }
            return string.Compare(left.NewsId, right.NewsId, StringComparison.Ordinal);
        }
    }
}
