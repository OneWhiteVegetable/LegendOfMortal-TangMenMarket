using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Shared
{
    internal sealed class NewsTickerView
    {
        private const float ItemPadding = 32f;
        private const float HeadlineBodyGap = 9f;
        private const float ManualScrollStep = 56f;
        private const float FallbackRowHeight = 36f;

        private readonly UiFrontendServices _services;
        private readonly UiPrefabInstance _view;
        private readonly ModApplication _application;
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly List<NewsRecord> _news = new List<NewsRecord>();
        private readonly List<float> _entryOffsets = new List<float>();
        private readonly List<float> _entryWidths = new List<float>();
        private RectTransform _viewport;
        private RectTransform _track;
        private RectTransform _groupA;
        private RectTransform _groupB;
        private RectTransform _template;
        private float _rowHeight;
        private float _groupWidth;
        private float _offset;
        private int _manualIndex;
        private bool _reducedMotion;
        private bool _bound;

        internal NewsTickerView(UiFrontendServices services, RectTransform host, ModApplication application)
        {
            _services = services;
            _view = services.Assets.Adopt("NewsTicker", host.gameObject);
            _application = application;
            _definitions = MarketQuery.BuildDefinitions(application.Items);
            _reducedMotion = services.Motion.ReducedMotion;
            PrepareVisualTree(host);
            EnsureFocusTarget(host);
            NewsTickerViewDriver driver = host.GetComponent<NewsTickerViewDriver>();
            if (driver == null)
                driver = host.gameObject.AddComponent<NewsTickerViewDriver>();
            driver.Owner = this;
            _view.SetActive(false);
        }

        internal void Bind(RunState state, string itemId = null)
        {
            if (!_view.IsAlive)
                return;
            List<NewsRecord> next = MarketQuery.CurrentWeekNews(_application, state, itemId);
            if (_bound && SameNews(next))
            {
                _view.SetActive(next.Count > 0);
                SyncMotionMode();
                return;
            }

            _bound = true;
            _news.Clear();
            _news.AddRange(next);
            if (_news.Count == 0)
            {
                ClearItems();
                _view.SetActive(false);
                return;
            }

            _view.SetActive(true);
            RebuildItems();
            SyncMotionMode();
        }

        internal void Next()
        {
            if (!_view.IsAlive)
                return;
            SyncMotionMode();
            if (_news.Count == 0 || _groupWidth <= 0f)
                return;
            _manualIndex = FindEntryIndex(_offset);
            if (_reducedMotion && TryReadNextPage())
                return;
            _manualIndex = (_manualIndex + 1) % _news.Count;
            _offset = _entryOffsets[_manualIndex];
            ApplyTrackPosition();
        }

        internal bool Scroll(float scrollDeltaY, bool interactionPaused)
        {
            if (_view.IsAlive)
                SyncMotionMode();
            if (!_view.IsAlive || _news.Count == 0 || _groupWidth <= 0f ||
                (!_reducedMotion && !interactionPaused) || scrollDeltaY == 0f ||
                float.IsNaN(scrollDeltaY) || float.IsInfinity(scrollDeltaY))
                return false;
            _offset = NewsTickerMotion.Wrap(_offset - scrollDeltaY * ManualScrollStep, _groupWidth);
            _manualIndex = FindEntryIndex(_offset);
            ApplyTrackPosition();
            return true;
        }

        internal void Tick(float deltaTime, bool interactionPaused)
        {
            if (!_view.IsAlive || _news.Count == 0 || _groupWidth <= 0f ||
                !_view.Root.gameObject.activeInHierarchy)
                return;
            SyncMotionMode();
            if (_reducedMotion || interactionPaused)
                return;
            float next = NewsTickerMotion.Advance(_offset, deltaTime, _groupWidth);
            if (next == _offset)
                return;
            _offset = next;
            ApplyTrackPosition();
        }

        private void PrepareVisualTree(RectTransform host)
        {
            _viewport = RequireRect(_view.Find("Viewport"), "Viewport");
            _track = RequireRect(_view.Find("Viewport/Track"), "Viewport/Track");
            DisableAutomaticLayout(_track);
            Graphic viewportGraphic = _viewport.GetComponent<Graphic>();
            if (viewportGraphic == null)
            {
                Image image = _viewport.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                viewportGraphic = image;
            }
            viewportGraphic.raycastTarget = true;
            if (_viewport.GetComponent<RectMask2D>() == null && _viewport.GetComponent<Mask>() == null)
                _viewport.gameObject.AddComponent<RectMask2D>();

            PrepareTemplate();
            _groupA = EnsureGroup("GroupA");
            _groupB = EnsureGroup("GroupB");
            _rowHeight = _viewport.rect.height;
            if (_rowHeight <= 0f)
                _rowHeight = host.rect.height;
            if (_rowHeight <= 0f)
                _rowHeight = FallbackRowHeight;
        }

        private void PrepareTemplate()
        {
            _template = AsRect(_view.Find("Viewport/Track/NewsTickerItem"));
            if (_template == null)
                _template = AsRect(_view.Find("Viewport/Track/GroupA/TickerItem"));
            if (_template == null)
                _template = AsRect(_view.Find("Viewport/Track/GroupA/NewsTickerItem"));
            if (_template == null)
                _template = AsRect(_view.Find("Viewport/Track/GroupA/Item"));

            Text headline = FindText(_template, "HeadlineText");
            Text body = FindText(_template, "BodyText");
            if (headline == null)
                headline = FindText("Viewport/Track/HeadlineText", "Viewport/Track/GroupA/HeadlineText");
            if (body == null)
                body = FindText("Viewport/Track/BodyText", "Viewport/Track/GroupA/BodyText");
            if (_template == null)
                _template = CreateRect("NewsTickerItemTemplate", _track);
            _template.SetParent(_track, false);
            _template.name = "NewsTickerItemTemplate";
            DisableAutomaticLayout(_template);

            if (headline == null)
                headline = CreateText("HeadlineText", _template, _services.Assets.BoldFont, 15,
                    UiFormatting.RiseOnDark);
            if (body == null)
                body = CreateText("BodyText", _template, _services.Assets.RegularFont, 15,
                    UiFormatting.Paper);
            headline.rectTransform.SetParent(_template, false);
            headline.name = "HeadlineText";
            body.rectTransform.SetParent(_template, false);
            body.name = "BodyText";
            ConfigureText(headline);
            ConfigureText(body);
            _template.gameObject.SetActive(false);
        }

        private RectTransform EnsureGroup(string name)
        {
            RectTransform group = AsRect(_view.Find("Viewport/Track/" + name));
            if (group == null)
                group = CreateRect(name, _track);
            DisableAutomaticLayout(group);
            return group;
        }

        private static void EnsureFocusTarget(RectTransform host)
        {
            Image background = host.Find("Background") == null
                ? null
                : host.Find("Background").GetComponent<Image>();
            if (background != null)
                background.raycastTarget = true;
            if (host.GetComponent<Selectable>() != null)
                return;
            Button button = host.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;
        }

        private void RebuildItems()
        {
            ClearItems();
            _reducedMotion = _services.Motion.ReducedMotion;
            PopulateGroup(_groupA, true);
            _groupWidth = 0f;
            for (int i = 0; i < _entryWidths.Count; i++)
                _groupWidth += _entryWidths[i];
            PopulateGroup(_groupB, false);
            _groupA.gameObject.SetActive(true);
            _groupB.gameObject.SetActive(true);
            PlaceLeft(_groupA, 0f, _groupWidth, _rowHeight);
            PlaceLeft(_groupB, _groupWidth, _groupWidth, _rowHeight);
            PlaceLeft(_track, 0f, _groupWidth * 2f, _rowHeight);
            _offset = 0f;
            _manualIndex = 0;
            ApplyTrackPosition();
        }

        private void PopulateGroup(RectTransform group, bool measure)
        {
            float x = 0f;
            for (int i = 0; i < _news.Count; i++)
            {
                NewsTickerItemVisual item = CreateItem(group, i);
                BindItem(item, _news[i]);
                ConfigureText(item.Headline);
                ConfigureText(item.Body);
                float headlineWidth = PreferredWidth(item.Headline);
                float bodyWidth = PreferredWidth(item.Body);
                float itemWidth = ItemPadding * 2f + headlineWidth + HeadlineBodyGap + bodyWidth;
                if (!measure && i < _entryWidths.Count)
                    itemWidth = _entryWidths[i];
                PlaceLeft(item.Root, x, itemWidth, _rowHeight);
                PlaceLeft(item.Headline.rectTransform, ItemPadding, headlineWidth, _rowHeight);
                PlaceLeft(item.Body.rectTransform, ItemPadding + headlineWidth + HeadlineBodyGap,
                    bodyWidth, _rowHeight);
                if (measure)
                {
                    _entryOffsets.Add(x);
                    _entryWidths.Add(itemWidth);
                }
                x += itemWidth;
            }
        }

        private NewsTickerItemVisual CreateItem(RectTransform group, int index)
        {
            GameObject instance = UnityEngine.Object.Instantiate(_template.gameObject, group, false);
            instance.name = "NewsTickerItem_" + index;
            instance.SetActive(true);
            RectTransform root = instance.GetComponent<RectTransform>();
            DisableAutomaticLayout(root);
            Text headline = FindText(root, "HeadlineText");
            Text body = FindText(root, "BodyText");
            if (headline == null || body == null)
                throw new InvalidOperationException("NewsTickerItem requires HeadlineText and BodyText.");
            return new NewsTickerItemVisual(root, headline, body);
        }

        private void BindItem(NewsTickerItemVisual item, NewsRecord news)
        {
            ItemDefinition definition;
            string name = _definitions.TryGetValue(news.ItemId, out definition)
                ? definition.Name
                : news.ItemId;
            item.Headline.text = name + " " + UiFormatting.Percent(news.Return);
            item.Headline.color = UiFormatting.Trend(news.Return, true);
            item.Body.text = news.Text ?? string.Empty;
            item.Body.color = UiFormatting.Paper;
        }

        private void ClearItems()
        {
            ClearGroup(_groupA);
            ClearGroup(_groupB);
            _entryOffsets.Clear();
            _entryWidths.Clear();
            _groupWidth = 0f;
            _offset = 0f;
            _manualIndex = 0;
            if (_track != null)
                PlaceLeft(_track, 0f, 0f, _rowHeight);
        }

        private static void ClearGroup(RectTransform group)
        {
            if (group == null)
                return;
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                GameObject child = group.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        private bool TryReadNextPage()
        {
            float viewportWidth = _viewport.rect.width;
            if (viewportWidth <= 0f)
                return false;
            float itemStart = _entryOffsets[_manualIndex];
            float unreadWidth = itemStart + _entryWidths[_manualIndex] - (_offset + viewportWidth);
            if (unreadWidth <= 1f)
                return false;
            float next = _offset + Math.Max(1f, viewportWidth * 0.8f);
            float lastPage = Math.Max(itemStart, itemStart + _entryWidths[_manualIndex] - viewportWidth);
            _offset = Math.Min(next, lastPage);
            ApplyTrackPosition();
            return true;
        }

        private int FindEntryIndex(float offset)
        {
            int index = 0;
            for (int i = 1; i < _entryOffsets.Count; i++)
            {
                if (_entryOffsets[i] > offset + 0.001f)
                    break;
                index = i;
            }
            return index;
        }

        private void SyncMotionMode()
        {
            bool reduced = _services.Motion.ReducedMotion;
            if (_reducedMotion == reduced)
                return;
            _reducedMotion = reduced;
            if (_news.Count == 0 || _groupWidth <= 0f)
                return;
            if (reduced)
            {
                _manualIndex = FindEntryIndex(_offset);
                _offset = _entryOffsets[_manualIndex];
            }
            else
            {
                _offset = NewsTickerMotion.Wrap(_offset, _groupWidth);
            }
            ApplyTrackPosition();
        }

        private void ApplyTrackPosition()
        {
            if (_track != null)
                _track.anchoredPosition = new Vector2(-_offset, 0f);
        }

        private bool SameNews(List<NewsRecord> next)
        {
            if (_news.Count != next.Count)
                return false;
            for (int i = 0; i < _news.Count; i++)
            {
                NewsRecord current = _news[i];
                NewsRecord candidate = next[i];
                if (!string.Equals(current.NewsId, candidate.NewsId, StringComparison.Ordinal) ||
                    !string.Equals(current.ItemId, candidate.ItemId, StringComparison.Ordinal) ||
                    current.Direction != candidate.Direction || current.Return != candidate.Return ||
                    !string.Equals(current.Text, candidate.Text, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private Text FindText(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                Transform transform = _view.Find(paths[i]);
                if (transform != null)
                {
                    Text text = transform.GetComponent<Text>();
                    if (text != null)
                        return text;
                }
            }
            return null;
        }

        private static Text FindText(RectTransform parent, string name)
        {
            if (parent == null)
                return null;
            Transform child = parent.Find(name);
            return child == null ? null : child.GetComponent<Text>();
        }

        private static RectTransform RequireRect(Transform transform, string path)
        {
            RectTransform rect = AsRect(transform);
            if (rect == null)
                throw new InvalidOperationException("NewsTicker requires " + path + ".");
            return rect;
        }

        private static RectTransform AsRect(Transform transform)
        {
            return transform as RectTransform;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.lineSpacing = 1.12f;
            ConfigureText(text);
            return text;
        }

        private static void ConfigureText(Text text)
        {
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
        }

        private static float PreferredWidth(Text text)
        {
            return Mathf.Ceil(Mathf.Max(1f, text.preferredWidth)) + 1f;
        }

        private static void DisableAutomaticLayout(RectTransform rect)
        {
            HorizontalLayoutGroup horizontal = rect.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
                horizontal.enabled = false;
            VerticalLayoutGroup vertical = rect.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                vertical.enabled = false;
            ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;
        }

        private static void PlaceLeft(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(Math.Max(0f, width), Math.Max(0f, height));
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private sealed class NewsTickerItemVisual
        {
            internal NewsTickerItemVisual(RectTransform root, Text headline, Text body)
            {
                Root = root;
                Headline = headline;
                Body = body;
            }

            internal RectTransform Root { get; private set; }
            internal Text Headline { get; private set; }
            internal Text Body { get; private set; }
        }
    }

    internal sealed class NewsTickerViewDriver : MonoBehaviour, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IScrollHandler,
        ISubmitHandler
    {
        private bool _pointerInside;
        private bool _focused;
        internal NewsTickerView Owner;

        private void Update()
        {
            if (Owner != null)
                Owner.Tick(Time.unscaledDeltaTime, _pointerInside || _focused);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focused = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focused = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Owner != null && eventData != null &&
                eventData.button == PointerEventData.InputButton.Left)
                Owner.Next();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Owner != null && eventData != null &&
                Owner.Scroll(eventData.scrollDelta.y, _pointerInside || _focused))
                eventData.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (Owner != null)
                Owner.Next();
        }

        private void OnEnable()
        {
            _focused = EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _focused = false;
        }

        private void OnDestroy()
        {
            Owner = null;
        }
    }
}
