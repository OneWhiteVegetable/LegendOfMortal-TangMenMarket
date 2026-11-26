using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketRankingPanel : IDisposable
    {
        private const float RankingSpacing = 7f;

        private readonly UiFrontendServices _services;
        private readonly RectTransform _root;
        private readonly RectTransform _viewport;
        private readonly RectTransform _rowContent;
        private readonly ScrollRect _scroll;
        private readonly VerticalLayoutGroup _rowLayout;
        private readonly List<RankingRowView> _rows = new List<RankingRowView>();
        private readonly RankingChrome _chrome;
        private readonly RankingScrollInput _keyboard;
        private bool _ascending;
        private bool _compact;
        private bool _disposed;
        private ModApplication _lastApplication;
        private RunState _lastState;
        private ItemCatalog _lastCatalog;

        internal MarketRankingPanel(UiFrontendServices services, RectTransform content)
        {
            if (content == null || content.parent == null)
                throw new InvalidOperationException("Ranking requires a sibling surface beside GoodsGrid.");
            _services = services;
            _compact = IsCompact(content);
            _root = RankingLayout.Rect("RankingRoot", content.parent);
            RankingLayout.Stretch(_root);
            VerticalLayoutGroup rootLayout = _root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(28, 28, 20, 15);
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.spacing = 0f;

            _chrome = new RankingChrome(services, _root, _compact, ToggleSort, RequestBack);

            RectTransform viewport = RankingLayout.Rect("RankingViewport", _root);
            _viewport = viewport;
            LayoutElement viewportElement = RankingLayout.Layout(viewport, -1f, 0f, 1f, 1f);
            viewportElement.minHeight = 0f;
            Image hitArea = viewport.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.135f;
            _scroll.scrollSensitivity = _compact ? 28f : 36f;
            _scroll.viewport = viewport;

            _rowContent = RankingLayout.Rect("Content", viewport);
            _rowContent.anchorMin = new Vector2(0f, 1f);
            _rowContent.anchorMax = new Vector2(1f, 1f);
            _rowContent.pivot = new Vector2(0.5f, 1f);
            _rowContent.offsetMin = Vector2.zero;
            _rowContent.offsetMax = Vector2.zero;
            _rowLayout = _rowContent.gameObject.AddComponent<VerticalLayoutGroup>();
            _rowLayout.padding = new RectOffset(4, 16, 4, 4);
            _rowLayout.spacing = RankingSpacing;
            _rowLayout.childAlignment = TextAnchor.UpperLeft;
            _rowLayout.childControlWidth = true;
            _rowLayout.childControlHeight = true;
            _rowLayout.childForceExpandWidth = true;
            _rowLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = _rowContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _rowContent;

            Image track = RankingLayout.Image(
                "Scrollbar",
                viewport,
                null,
                Color.clear);
            track.raycastTarget = true;
            RankingLayout.IgnoreLayout(track);
            track.rectTransform.anchorMin = new Vector2(1f, 0f);
            track.rectTransform.anchorMax = new Vector2(1f, 1f);
            track.rectTransform.pivot = new Vector2(1f, 0.5f);
            track.rectTransform.offsetMin = new Vector2(-14f, 4f);
            track.rectTransform.offsetMax = new Vector2(0f, -4f);
            Image rail = RankingLayout.Image("Rail", track.transform, null, RankingLayout.Alpha(UiFormatting.Ink, 0.13f));
            RankingLayout.Stretch(rail.rectTransform, 10f, 0f, 0f, 0f);
            Image handle = RankingLayout.Image("Handle", track.transform, null, Color.clear);
            // Scrollbar drives the anchors, leaving default RectTransform sizeDelta untouched.
            RankingLayout.Stretch(handle.rectTransform);
            Image fill = RankingLayout.Image("Thumb", handle.transform, null, RankingLayout.Gold);
            RankingLayout.Stretch(fill.rectTransform, 10f, 0f, 0f, 0f);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = fill;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            _scroll.verticalScrollbarSpacing = 0f;

            _keyboard = viewport.gameObject.AddComponent<RankingScrollInput>();
            _keyboard.Owner = this;
            _root.gameObject.SetActive(false);
        }

        internal event Action<string> DetailRequested;
        internal event Action BackRequested;
        internal bool Ascending { get { return _ascending; } }

        internal int Refresh(
            ModApplication application,
            RunState state,
            ItemCatalog catalog)
        {
            if (_disposed)
                return 0;
            _lastApplication = application;
            _lastState = state;
            _lastCatalog = catalog;
            UpdateLayoutProfile();
            _root.gameObject.SetActive(true);
            _chrome.Show();
            _chrome.SetAscending(_ascending);

            List<RankingEntry> entries = BuildEntries(application, state, catalog);
            entries.Sort(CompareEntries);
            _chrome.SetSortInteractable(entries.Count > 1);
            _chrome.SetItemCount(entries.Count);
            BindRows(entries);
            return entries.Count;
        }

        internal void ToggleSort()
        {
            if (_disposed)
                return;
            if (_lastApplication == null)
            {
                _ascending = false;
                _chrome.SetAscending(false);
                return;
            }
            _ascending = !_ascending;
            _chrome.SetAscending(_ascending);
            Refresh(_lastApplication, _lastState, _lastCatalog);
            ScrollToTop();
        }

        internal void Hide()
        {
            if (_disposed)
                return;
            _chrome.Hide();
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].GameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        private List<RankingEntry> BuildEntries(
            ModApplication application,
            RunState state,
            ItemCatalog catalog)
        {
            List<ItemDefinition> items = MarketQuery.AppearedItems(state, catalog);
            List<RankingEntry> result = new List<RankingEntry>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                ItemDefinition item = items[i];
                List<WeeklyPoint> history = MarketQuery.ItemHistory(application, item.ItemId);
                List<WeeklyPoint> sparkHistory = history;
                if (history.Count > RankingRowView.SparkPointCount)
                {
                    sparkHistory = history.GetRange(
                        history.Count - RankingRowView.SparkPointCount,
                        RankingRowView.SparkPointCount);
                }
                result.Add(new RankingEntry
                {
                    Item = item,
                    History = history,
                    SparkHistory = sparkHistory,
                    Current = history.Count == 0 ? null : history[history.Count - 1],
                    TotalReturn = MarketQuery.TotalReturn(history),
                    Delisted = MarketQuery.IsDelisted(application, state, item.ItemId),
                    AppearanceOrder = i
                });
            }
            return result;
        }

        private int CompareEntries(RankingEntry left, RankingEntry right)
        {
            return RankingOrdering.Compare(
                left.Current != null,
                left.TotalReturn,
                left.AppearanceOrder,
                right.Current != null,
                right.TotalReturn,
                right.AppearanceOrder,
                _ascending);
        }

        private void BindRows(List<RankingEntry> entries)
        {
            if (entries.Count == 0)
            {
                if (_rows.Count == 0)
                    _rows.Add(CreateRow());
                for (int i = 0; i < _rows.Count; i++)
                {
                    bool active = i == 0;
                    if (active)
                    {
                        _rows[i].BindEmpty();
                        _rows[i].View.Root.SetSiblingIndex(0);
                    }
                    _rows[i].GameObject.SetActive(active);
                }
                return;
            }

            Dictionary<string, RankingRowView> reusable =
                new Dictionary<string, RankingRowView>(StringComparer.Ordinal);
            for (int i = 0; i < _rows.Count; i++)
            {
                RankingRowView row = _rows[i];
                if (!string.IsNullOrEmpty(row.ItemId) && !reusable.ContainsKey(row.ItemId))
                    reusable.Add(row.ItemId, row);
            }

            RankingRowView[] ordered = new RankingRowView[entries.Count];
            HashSet<RankingRowView> used = new HashSet<RankingRowView>();
            for (int i = 0; i < entries.Count; i++)
            {
                string itemId = entries[i].Item.ItemId;
                RankingRowView row;
                if (!string.IsNullOrEmpty(itemId) && reusable.TryGetValue(itemId, out row))
                {
                    ordered[i] = row;
                    used.Add(row);
                }
            }

            int nextUnused = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (ordered[i] != null)
                    continue;
                while (nextUnused < _rows.Count && used.Contains(_rows[nextUnused]))
                    nextUnused++;
                RankingRowView row;
                if (nextUnused < _rows.Count)
                {
                    row = _rows[nextUnused];
                }
                else
                {
                    row = CreateRow();
                    _rows.Add(row);
                }
                ordered[i] = row;
                used.Add(row);
            }

            for (int i = 0; i < ordered.Length; i++)
            {
                RankingRowView row = ordered[i];
                row.Bind(entries[i], i + 1);
                row.GameObject.SetActive(true);
                row.View.Root.SetSiblingIndex(i);
            }
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!used.Contains(_rows[i]))
                    _rows[i].GameObject.SetActive(false);
            }
        }

        private RankingRowView CreateRow()
        {
            UiPrefabInstance view = _services.Assets.Create("RankingRow", _rowContent);
            RankingRowView row;
            try
            {
                row = new RankingRowView(_services, view, _compact);
            }
            catch
            {
                view.Dispose();
                throw;
            }
            RankingRowView captured = row;
            row.OpenAction = delegate { RequestDetail(captured); };
            view.Button("OpenButton").onClick.AddListener(row.OpenAction);
            return row;
        }

        private void RequestBack()
        {
            if (_disposed)
                return;
            Action callback = BackRequested;
            if (callback != null)
                callback();
        }

        private void RequestDetail(RankingRowView row)
        {
            if (_disposed || row == null || string.IsNullOrEmpty(row.ItemId))
                return;
            Action<string> callback = DetailRequested;
            if (callback != null)
                callback(row.ItemId);
        }

        private void UpdateLayoutProfile()
        {
            bool compact = IsCompact(_root);
            if (compact == _compact)
                return;
            _compact = compact;
            _scroll.scrollSensitivity = compact ? 28f : 36f;
            _chrome.SetCompact(compact);
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetCompact(compact);
        }

        private static bool IsCompact(RectTransform content)
        {
            float height = content.rect.height;
            return height > 0f ? height < 620f : Screen.height < 900;
        }

        private void ScrollRows(int direction)
        {
            float rowHeight = _compact ? 57f : 68f;
            ScrollBy(direction * (rowHeight + RankingSpacing));
        }

        private void ScrollPage(int direction)
        {
            ScrollBy(direction * Mathf.Max(1f, _viewport.rect.height - 24f));
        }

        private void ScrollBy(float pixels)
        {
            float range = Mathf.Max(0f, _rowContent.rect.height - _viewport.rect.height);
            if (range <= 0f)
                return;
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(
                _scroll.verticalNormalizedPosition + pixels / range);
        }

        private void ScrollToTop()
        {
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 1f;
        }

        private void ScrollToBottom()
        {
            _scroll.StopMovement();
            _scroll.verticalNormalizedPosition = 0f;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_keyboard != null)
                _keyboard.Owner = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                RankingRowView row = _rows[i];
                if (row.View.IsAlive && row.OpenAction != null)
                    row.View.Button("OpenButton").onClick.RemoveListener(row.OpenAction);
                row.Dispose();
            }
            _rows.Clear();
            _chrome.Dispose();
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            DetailRequested = null;
            BackRequested = null;
            _lastApplication = null;
            _lastState = null;
            _lastCatalog = null;
        }

        private sealed class RankingScrollInput : MonoBehaviour
        {
            internal MarketRankingPanel Owner;

            private void Update()
            {
                MarketRankingPanel owner = Owner;
                if (owner == null || owner._disposed)
                    return;
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    owner.ScrollToTop();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    owner.ScrollToBottom();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.PageUp))
                    owner.ScrollPage(1);
                if (Input.GetKeyDown(KeyCode.PageDown))
                    owner.ScrollPage(-1);
                if (Input.GetKeyDown(KeyCode.UpArrow))
                    owner.ScrollRows(1);
                if (Input.GetKeyDown(KeyCode.DownArrow))
                    owner.ScrollRows(-1);
            }
        }
    }
}
