using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal struct MarketDraft
    {
        internal bool Buy;
        internal bool Borrow;
        internal bool Sell;
        internal bool Return;
        internal bool CanIncrement;
        internal bool Any { get { return Buy || Borrow || Sell || Return; } }
    }

    internal sealed class MarketCartPanel : IDisposable
    {
        private const int PageSize = 24;
        private readonly RectTransform _pendingPager;
        private float _listTop;
        private long _pendingPage;
        private long _pendingRowCount;
        private readonly List<PendingRow> _pendingRows = new List<PendingRow>(PageSize);
        private readonly UiFrontendServices _services;
        private readonly ModApplication _application;
        private readonly RunState _state;
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly UiPrefabInstance _window;
        private readonly ScrollRect _scroll;
        private readonly MarketCartEmptyView _emptyView;
        private readonly MarketCheckoutInk _checkoutInk;
        private readonly Font _whiteAmountFont;
        private readonly Image _checkoutIcon;
        private readonly List<CartEntryModel> _entries = new List<CartEntryModel>();
        private readonly List<CartEntryView> _views = new List<CartEntryView>();
        private readonly HashSet<CartEntryModel> _entriesAwaitingAnimation = new HashSet<CartEntryModel>();
        private List<TradeRecord> _settled = new List<TradeRecord>();
        private bool _pending = true;
        private bool _visible;
        private int _settledPage;
        private float _pendingScroll = 1f;
        private float _settledScroll = 1f;

        internal MarketCartPanel(
            UiFrontendServices services,
            ModApplication application,
            UiPrefabInstance window)
        {
            _services = services;
            _application = application;
            _state = application.State;
            _definitions = MarketQuery.BuildDefinitions(application.Items);
            _window = window;
            Image checkoutImage = window.Button("CheckoutButton").targetGraphic as Image;
            if (checkoutImage != null)
                checkoutImage.type = Image.Type.Simple;
            _scroll = window.Scroll("CartListScroll");
            VerticalLayoutGroup contentLayout = window.Get<VerticalLayoutGroup>("CartListContent");
            contentLayout.padding = new RectOffset(
                contentLayout.padding.left,
                contentLayout.padding.right,
                1,
                10);
            RectTransform content = contentLayout.transform as RectTransform;
            Button checkoutButton = window.Button("CheckoutButton");
            _checkoutIcon = CreateCheckoutLabel(checkoutButton);
            _checkoutInk = MarketCheckoutInk.Attach(checkoutButton, _checkoutIcon, services);
            _whiteAmountFont = FindAmountFont();
            window.Text("BuyTotalText").gameObject.AddComponent<LedgerNumeralWeight>();
            window.Text("SellTotalText").gameObject.AddComponent<LedgerNumeralWeight>();
            window.Text("CashDeltaText").font = services.Assets.BoldFont;
            MarketLetterSpacing.Attach(window.Rect("CartRail").Find("Title").GetComponent<Text>(), .15f);
            MarketLetterSpacing.Attach(window.Rect("CartRail").Find("Subtitle").GetComponent<Text>(), .16f);
            MarketLetterSpacing.Attach(window.Button("PendingTabButton").GetComponentInChildren<Text>(true), .08f);
            MarketLetterSpacing.Attach(window.Button("SettledTabButton").GetComponentInChildren<Text>(true), .08f);
            for (int i = 0; i < content.childCount; i++)
            {
                GameObject child = content.GetChild(i).gameObject;
                _views.Add(CreateView(services.Assets.Adopt("CartEntry", child)));
            }
            _pendingPager = new GameObject("PendingPages", typeof(RectTransform)).GetComponent<RectTransform>();
            _pendingPager.SetParent(window.Rect("CartRail"), false);
            _pendingPager.anchorMin = new Vector2(0f, 1f);
            _pendingPager.anchorMax = Vector2.one;
            _pendingPager.pivot = new Vector2(0.5f, 1f);
            _pendingPager.gameObject.SetActive(false);
            _emptyView = new MarketCartEmptyView(services, _scroll, content);
            MarketVisualStyle.Apply(window);
            Image divider = window.Rect("CartRail").Find("CartFooter/TotalDivider").GetComponent<Image>();
            divider.gameObject.AddComponent<LedgerDashedRule>();
            ConfigureCartTab(window.Button("PendingTabButton"));
            ConfigureCartTab(window.Button("SettledTabButton"));
            MarketCartClearMotion.Attach(window.Button("CartClearButton"), services.Motion);
            window.Text("FooterNoteText").color = new Color32(156, 164, 152, 255);
            window.Button("PendingTabButton").onClick.AddListener(SelectPending);
            window.Button("SettledTabButton").onClick.AddListener(SelectSettled);
            window.Button("CartClearButton").onClick.AddListener(RequestClear);
            window.Button("CheckoutButton").onClick.AddListener(Checkout);
            window.Button("SettledPrevButton").onClick.AddListener(PreviousSettledPage);
            window.Button("SettledNextButton").onClick.AddListener(NextSettledPage);
            services.Overlays.AttachTooltip(
                window.Button("CartClearButton").gameObject,
                "清空契匣",
                "只移除待结算货签，不成交、不影响货囊。",
                "操作前会再次确认");
        }

        internal event Action<List<TradeOrder>> CheckoutRequested;
        internal event Action OrdersChanged;

        private static Font FindAmountFont()
        {
            foreach (string name in Font.GetOSInstalledFontNames())
            {
                if (string.Equals(name, "Microsoft YaHei", StringComparison.OrdinalIgnoreCase) || name == "微软雅黑")
                    return Font.CreateDynamicFontFromOSFont(name, 16);
            }
            return null;
        }

        private void ConfigureAmount(Text text)
        {
            bool systemDigits = _pending && _whiteAmountFont != null;
            text.font = systemDigits ? _whiteAmountFont : _services.Assets.BoldFont;
            text.fontStyle = systemDigits ? FontStyle.Bold : FontStyle.Normal;
            text.supportRichText = true;
            // Unity's bitmap bearing at 15 UI units is one screen pixel shorter than the
            // 15 CSS px YaHei Bold numerals. Match their measured visible height.
            text.fontSize = text.resizeTextMaxSize = _pending ? 16 : 15;
        }

        private static string LedgerAmount(long? amount, bool expense)
        {
            if (!amount.HasValue) return "金额待核";
            string sign = amount.Value == 0 ? string.Empty : expense ? "− " : "+ ";
            return sign + UiFormatting.Money(amount.Value) + " <color=#C6C5B9><size=10>文</size></color>";
        }

        private Image CreateCheckoutLabel(Button button)
        {
            Text label = button.GetComponentInChildren<Text>(true);
            MarketLetterSpacing.Attach(label, .25f);
            label.font = _services.Assets.BoldFont;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = new Vector2(16f, 0);
            label.rectTransform.sizeDelta = new Vector2(84f, 30f);
            Image icon = new GameObject("CheckoutIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(button.transform, false);
            icon.gameObject.layer = button.gameObject.layer;
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition = new Vector2(-42f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(21f, 21f);
            return icon;
        }

        private static void ConfigureCartTab(Button tab)
        {
            tab.transition = Selectable.Transition.None;
            tab.targetGraphic.color = Color.clear;
            tab.targetGraphic.canvasRenderer.SetColor(Color.white);
            Image line = tab.transform.Find("ActiveLine").GetComponent<Image>();
            line.gameObject.AddComponent<MarketPaperEdge>().Slant = Mathf.Tan(25f * Mathf.Deg2Rad);
            Image face = tab.targetGraphic as Image;
            if (face != null)
            {
                face.overrideSprite = null;
                face.sprite = null;
            }
        }

        internal MarketDraft Draft(string itemId, ref int quantity, bool includeClosing)
        {
            if (string.IsNullOrEmpty(itemId))
                return default(MarketDraft);
            MarketDraft current = DraftAt(itemId, quantity, includeClosing);
            if (quantity > 1 && !current.Any)
            {
                int low = 1;
                int high = quantity - 1;
                while (low < high)
                {
                    int middle = low + (int)(((long)high - low + 1L) / 2L);
                    if (DraftAt(itemId, middle, includeClosing).Any)
                        low = middle;
                    else
                        high = middle - 1;
                }
                quantity = low;
                current = DraftAt(itemId, quantity, includeClosing);
            }
            current.CanIncrement = quantity < int.MaxValue && DraftAt(itemId, quantity + 1, includeClosing).Any;
            return current;
        }

        internal string BorrowHint()
        {
            return _application.Trading != null && _application.Trading.NewBorrowClosed
                ? ShortService.ClosedMessage
                : "立下借货契 · 做空";
        }

        private MarketDraft DraftAt(string itemId, int quantity, bool includeClosing)
        {
            Holding holding = includeClosing ? _state.FindHolding(itemId) : null;
            long unsold = holding == null ? 0 : holding.Quantity - PendingQuantity(itemId, TradeAction.Sell);
            long unreturned = includeClosing ? MarketQuery.ShortQuantity(_state, itemId) - PendingQuantity(itemId, TradeAction.Return) : 0;
            return new MarketDraft
            {
                Buy = CanAdd(itemId, TradeAction.Buy, quantity),
                Borrow = CanAdd(itemId, TradeAction.Borrow, quantity),
                Sell = quantity <= unsold && CanAdd(itemId, TradeAction.Sell, quantity),
                Return = quantity <= unreturned && CanAdd(itemId, TradeAction.Return, quantity)
            };
        }

        private bool CanAdd(string itemId, TradeAction action, int quantity)
        {
            int index = FindEntry(itemId, action);
            List<TradeOrder> candidate = Candidate(itemId, action, quantity, index);
            TradeResult result = candidate == null ? null : ProjectCandidate(candidate);
            return result != null && result.Success;
        }

        private int FindEntry(string itemId, TradeAction action)
        {
            return _entries.FindIndex(entry => entry.Action == action && entry.ItemId == itemId);
        }

        private int PendingQuantity(string itemId, TradeAction action)
        {
            int index = FindEntry(itemId, action);
            return index < 0 ? 0 : _entries[index].Quantity;
        }

        private List<TradeOrder> Candidate(string itemId, TradeAction action, int quantity, int index)
        {
            long merged = index < 0 ? quantity : (long)_entries[index].Quantity + quantity;
            if (merged <= 0 || merged > int.MaxValue)
                return null;
            List<TradeOrder> candidate = GetOrders();
            TradeOrder order = new TradeOrder(action, itemId, (int)merged);
            if (index < 0)
                candidate.Add(order);
            else
                candidate[index] = order;
            return candidate;
        }

        internal void SetVisible(bool visible)
        {
            _visible = visible;
        }

        internal void SetLayoutProfile(bool narrow, bool wide)
        {
            RectTransform scrollRect = _scroll.GetComponent<RectTransform>();
            float horizontal = narrow ? 9f : wide ? 15f : 12f;
            scrollRect.offsetMin = new Vector2(horizontal + 5f, scrollRect.offsetMin.y);
            scrollRect.offsetMax = new Vector2(-horizontal, scrollRect.offsetMax.y);
            VerticalLayoutGroup content = _window.Get<VerticalLayoutGroup>("CartListContent");
            content.spacing = narrow ? 8f : 10f;
            for (int i = 0; i < _views.Count; i++)
                _views[i].ApplyLayoutMetrics();
            _emptyView.RefreshLayout();
        }

        internal void SetListTop(float top)
        {
            _listTop = top;
            RectTransform list = _scroll.GetComponent<RectTransform>();
            float pagerHeight = _pendingPager.gameObject.activeSelf ? 36f : 0f;
            _pendingPager.offsetMin = new Vector2(list.offsetMin.x, -top - pagerHeight);
            _pendingPager.offsetMax = new Vector2(list.offsetMax.x, -top);
            list.offsetMax = new Vector2(list.offsetMax.x, -top - pagerHeight);
        }

        internal bool Add(string itemId, TradeAction action, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0 || MarketQuery.IsDelisted(_application, _state, itemId))
            {
                _services.Overlays.ShowToast("商品已退市，仓位冻结，禁止交易与平仓。", 2.8f, UiToastKind.Error);
                return false;
            }

            int existingIndex = FindEntry(itemId, action);
            List<TradeOrder> candidate = Candidate(itemId, action, quantity, existingIndex);
            if (candidate == null)
            {
                _services.Overlays.ShowToast("货契数量超限。", 4.8f, UiToastKind.Error);
                return false;
            }
            if (!ValidateCandidate(candidate, itemId, quantity))
                return false;

            if (existingIndex < 0)
            {
                CartEntryModel added = new CartEntryModel
                {
                    ItemId = itemId,
                    Action = action,
                    Quantity = quantity
                };
                _entries.Add(added);
                _entriesAwaitingAnimation.Add(added);
            }
            else
            {
                _entries[existingIndex].Quantity += quantity;
            }
            if (_pending && _visible)
                SaveScroll();
            _pending = true;
            NotifyChanged();
            return true;
        }

        internal void Refresh()
        {
            if (!_visible)
                return;
            int settledStart = 0;
            int count;
            if (_pending)
            {
                ProjectRows();
                count = _pendingRows.Count;
            }
            else
            {
                _settled = GetSettledRecords();
                int pageCount = 1 + Math.Max(0, _settled.Count - 1) / PageSize;
                _settledPage = Mathf.Clamp(_settledPage, 0, pageCount - 1);
                settledStart = _settledPage * PageSize;
                count = Math.Min(PageSize, _settled.Count - settledStart);
            }
            EnsureViews(count);
            _emptyView.Bind(count == 0, _pending);
            for (int i = 0; i < _views.Count; i++)
            {
                bool active = i < count;
                _views[i].GameObject.SetActive(active);
                if (!active)
                {
                    _views[i].StopAddedAnimation();
                    continue;
                }
                if (_pending)
                {
                    PendingRow row = _pendingRows[i];
                    CartEntryModel model = row.Entry;
                    _views[i].BindPending(_application, row, _state);
                    if (_entriesAwaitingAnimation.Remove(model))
                        _views[i].PlayAdded();
                }
                else
                {
                    _views[i].BindSettled(_settled[settledStart + i]);
                }
            }
            _window.Text("CartCountText").text = (_pending ? _pendingRowCount : _settled.Count).ToString();
            UpdateTabs();
            RefreshTotals();
            RestoreScroll();
        }

        internal List<TradeOrder> GetOrders()
        {
            List<TradeOrder> result = new List<TradeOrder>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
                result.Add(new TradeOrder(_entries[i].Action, _entries[i].ItemId, _entries[i].Quantity));
            return result;
        }

        internal void Clear()
        {
            _entries.Clear();
            _entriesAwaitingAnimation.Clear();
            _pendingPage = 0;
            _pendingScroll = 1f;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (_visible)
                Refresh();
            if (OrdersChanged != null)
                OrdersChanged();
        }

        private void ProjectRows()
        {
            _pendingRows.Clear();
            _pendingRowCount = 0;
            if (_entries.Count == 0)
                return;
            TradeResult projection = ProjectCandidate(GetOrders());
            CapacitySnapshot before = projection == null ? null : projection.CapacityBefore;
            if (before == null && _application.Capacity != null)
            {
                try { before = _application.Capacity.Evaluate(_state, _definitions); }
                catch (InvalidOperationException) { }
            }
            foreach (CartEntryModel entry in _entries)
            {
                bool opening = entry.Action == TradeAction.Buy || entry.Action == TradeAction.Borrow;
                bool longSide = entry.Action == TradeAction.Buy || entry.Action == TradeAction.Sell;
                ItemCapacity position = before == null ? null : before.FindItem(entry.ItemId);
                long held = position == null ? 0 : longSide ? position.LongQuantity : position.ShortQuantity;
                if (opening)
                {
                    int closing = FindEntry(entry.ItemId, longSide ? TradeAction.Sell : TradeAction.Return);
                    if (closing >= 0)
                        held = Math.Max(0, held - _entries[closing].Quantity);
                }
                ItemDefinition item;
                _definitions.TryGetValue(entry.ItemId, out item);
                int limit = before == null ? 0 : CapacityService.GetStackLimit(item, before.CarryingCapacity);
                entry.Stacks = new CartStackLayout(entry.Quantity, limit, held, opening);
                _pendingRowCount += entry.Stacks.Count;
            }
            _pendingPage = Math.Min(_pendingPage, (_pendingRowCount - 1L) / PageSize);
            long start = _pendingPage * PageSize;
            foreach (CartEntryModel entry in _entries)
            {
                int index = (int)Math.Min(start, entry.Stacks.Count);
                start -= index;
                bool allowed = false;
                bool checkedIncrease = false;
                for (; index < entry.Stacks.Count && _pendingRows.Count < PageSize; index++)
                {
                    PendingRow row = new PendingRow { Entry = entry };
                    entry.Stacks.GetRow(index, out row.Offset, out row.Quantity, out row.Limit);
                    if (row.Quantity < row.Limit && !checkedIncrease)
                    {
                        allowed = CanAdd(entry.ItemId, entry.Action, 1);
                        checkedIncrease = true;
                    }
                    row.CanIncrement = row.Quantity < row.Limit && allowed;
                    _pendingRows.Add(row);
                }
                if (_pendingRows.Count == PageSize)
                    break;
            }
        }

        private void EnsureViews(int count)
        {
            while (_views.Count < count)
                _views.Add(CreateView(_services.Assets.Create(
                    "CartEntry",
                    _window.Get<VerticalLayoutGroup>("CartListContent").transform)));
        }

        private CartEntryView CreateView(UiPrefabInstance view)
        {
            CartEntryView result = new CartEntryView(_services, view, _definitions);
            result.QuantityChanged += OnQuantityChanged;
            result.RemoveRequested += OnRemoveRequested;
            result.ApplyLayoutMetrics();
            return result;
        }

        private bool ValidateCandidate(List<TradeOrder> candidate, string itemId, int quantity)
        {
            TradeResult projection = ProjectCandidate(candidate);
            if (projection != null && projection.Success)
                return true;

            ItemDefinition item;
            string name = _definitions.TryGetValue(itemId, out item) && item != null ? item.Name : itemId;
            _services.Overlays.ShowToast(
                CartCapacityFeedback.Rejection(projection, itemId, name, quantity),
                4.8f,
                UiToastKind.Error);
            return false;
        }

        private TradeResult ProjectCandidate(List<TradeOrder> candidate)
        {
            try
            {
                return _application.Trading == null ? null : _application.Trading.PreviewCapacity(candidate);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void OnQuantityChanged(CartEntryView view, int delta)
        {
            if (!_pending || view.Model == null || !_entries.Contains(view.Model))
                return;
            if (delta > 0)
            {
                if (view.RowQuantity >= view.RowLimit ||
                    MarketQuery.IsDelisted(_application, _state, view.Model.ItemId))
                    return;
                Add(view.Model.ItemId, view.Model.Action, 1);
                return;
            }
            Reduce(view.Model, 1);
        }

        private void OnRemoveRequested(CartEntryView view)
        {
            if (_pending && view.Model != null && _entries.Contains(view.Model))
                Reduce(view.Model, view.RowQuantity);
        }

        private void Reduce(CartEntryModel model, int quantity)
        {
            SaveScroll();
            model.Quantity -= quantity;
            if (model.Quantity <= 0)
            {
                _entriesAwaitingAnimation.Remove(model);
                _entries.Remove(model);
            }
            NotifyChanged();
        }

        private void RefreshTotals()
        {
            Button checkout = _window.Button("CheckoutButton");
            Button clear = _window.Button("CartClearButton");
            Text buyCaption = _window.Text("BuyCaptionText");
            Text sellCaption = _window.Text("SellCaptionText");
            Text cashCaption = _window.Text("CashCaptionText");
            Text footerNote = _window.Text("FooterNoteText");
            ConfigureAmount(_window.Text("BuyTotalText"));
            ConfigureAmount(_window.Text("SellTotalText"));
            if (!_pending)
            {
                buyCaption.text = "逐笔账目";
                sellCaption.text = "显示口径";
                _window.Text("BuyTotalText").text = "真实成交";
                _window.Text("SellTotalText").text = "成交额 / 银两变化";
                cashCaption.text = "账簿已记";
                _window.Text("CashDeltaText").text = _settled.Count + " 笔";
                footerNote.text = "银两按原版钱包的实际收支记录。";
                UiView.SetLabel(checkout, "再写新契");
                _checkoutIcon.sprite = _services.Assets.GetSprite("icon_plus");
                checkout.interactable = true;
                _checkoutInk.Paint(false);
                clear.gameObject.SetActive(false);
                return;
            }

            buyCaption.text = "买入 / 还货";
            sellCaption.text = "卖出 / 借货";
            long? outgoing;
            long? incoming;
            GetGrossTotals(out outgoing, out incoming);
            _window.Text("BuyTotalText").text = LedgerAmount(outgoing, true);
            _window.Text("SellTotalText").text = LedgerAmount(incoming, false);
            TradeResult preview = null;
            if (_entries.Count > 0 && _application.Trading != null)
            {
                try
                {
                    preview = _application.Trading.Preview(GetOrders());
                }
                catch (Exception)
                {
                    preview = null;
                }
            }

            if (preview != null && preview.Success)
            {
                long cash = preview.CashDelta;
                cashCaption.text = cash > 0 ? "预计银两净收" : cash < 0 ? "预计银两应付" : "预计银两不变";
                _window.Text("CashDeltaText").text = SignedMoney(cash) + " 文";
                footerNote.text = UiFormatting.SettlementBalance(preview);
            }
            else
            {
                cashCaption.text = _entries.Count == 0 ? "预计银两不变" :
                    preview != null && preview.Error == TradeError.BorrowWindowClosed ? "请移除待结算借契" : "银两待校验";
                _window.Text("CashDeltaText").text = _entries.Count == 0 ? "0 文" : "—";
                footerNote.text = _entries.Count == 0
                    ? BorrowHint()
                    : UiFormatting.TradeErrorMessage(preview, _application.Items);
            }
            UiView.SetLabel(checkout, "立契结算");
            _checkoutIcon.sprite = _services.Assets.GetSprite("icon_check");
            bool hasDelisted = false;
            for (int i = 0; i < _entries.Count; i++)
                hasDelisted |= MarketQuery.IsDelisted(_application, _state, _entries[i].ItemId);
            checkout.interactable = _entries.Count > 0 && !hasDelisted &&
                (preview == null || preview.Error != TradeError.BorrowWindowClosed);
            _checkoutInk.Paint(false);
            clear.gameObject.SetActive(true);
            clear.interactable = _entries.Count > 0;
        }

        private void UpdateTabs()
        {
            Button pending = _window.Button("PendingTabButton");
            Button settled = _window.Button("SettledTabButton");
            TintTab(pending, _pending);
            TintTab(settled, !_pending);
            Transform pendingLine = pending.transform.Find("ActiveLine");
            if (pendingLine != null)
                pendingLine.gameObject.SetActive(_pending);
            Transform settledLine = settled.transform.Find("ActiveLine");
            if (settledLine != null)
                settledLine.gameObject.SetActive(!_pending);
            Button previous = _window.Button("SettledPrevButton");
            Button next = _window.Button("SettledNextButton");
            long pageCount = _pending
                ? 1L + Math.Max(0L, _pendingRowCount - 1L) / PageSize
                : 1L + Math.Max(0, _settled.Count - 1) / PageSize;
            long page = _pending ? _pendingPage : _settledPage;
            bool pendingPager = _pending && pageCount > 1;
            _pendingPager.gameObject.SetActive(pendingPager);
            SetListTop(_listTop);
            Transform parent = pendingPager ? _pendingPager : _window.Rect("CartRail");
            Text counter = _window.Text("SettledPageText");
            if (counter.transform.parent != parent)
            {
                previous.transform.SetParent(parent, false);
                next.transform.SetParent(parent, false);
                counter.transform.SetParent(parent, false);
                SetPagerTop(previous.GetComponent<RectTransform>(), pendingPager ? 4f : 20f);
                SetPagerTop(next.GetComponent<RectTransform>(), pendingPager ? 4f : 20f);
                SetPagerTop(counter.rectTransform, pendingPager ? 3f : 19f);
            }
            bool show = !_pending || pendingPager;
            previous.gameObject.SetActive(show);
            next.gameObject.SetActive(show);
            counter.gameObject.SetActive(show);
            counter.text = (page + 1) + "/" + pageCount;
            previous.interactable = page > 0;
            next.interactable = page + 1 < pageCount;
        }

        private static void SetPagerTop(RectTransform rect, float top)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
        }

        private static void TintTab(Button tab, bool active)
        {
            // Keep the conventional selected/hover/focus feedback on the lettering only.
            Text label = tab.GetComponentInChildren<Text>(true);
            tab.targetGraphic = label;
            tab.transition = Selectable.Transition.ColorTint;
            label.color = Color.white;
            ColorBlock colors = tab.colors;
            colors.normalColor = active ? UiFormatting.Paper : UiFormatting.DarkMuted;
            colors.highlightedColor = UiFormatting.Gold;
            colors.selectedColor = UiFormatting.Gold;
            colors.pressedColor = UiFormatting.Paper;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            tab.colors = colors;
        }

        private void PreviousSettledPage() { TurnPage(-1); }
        private void NextSettledPage() { TurnPage(1); }

        private void TurnPage(int delta)
        {
            if (_pending)
            {
                _pendingPage = Math.Max(0, _pendingPage + delta);
                _pendingScroll = 1f;
            }
            else
            {
                _settledPage += delta;
                _settledScroll = 1f;
            }
            Refresh();
        }

        private void SelectPending()
        {
            if (_pending)
                return;
            SaveScroll();
            _pending = true;
            Refresh();
        }

        private void SelectSettled()
        {
            if (!_pending)
                return;
            SaveScroll();
            _pending = false;
            Refresh();
        }

        private void SaveScroll()
        {
            float position = _scroll.content.rect.height <= _scroll.viewport.rect.height + 0.5f
                ? 1f : _scroll.verticalNormalizedPosition;
            if (_pending)
                _pendingScroll = position;
            else
                _settledScroll = position;
        }

        private void RestoreScroll()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scroll.content);
            float value = _pending ? _pendingScroll : _settledScroll;
            if (!float.IsNaN(value) && !float.IsInfinity(value))
                _scroll.verticalNormalizedPosition = Mathf.Clamp01(value);
        }

        private void RequestClear()
        {
            if (!_pending || _entries.Count == 0)
                return;
            UiModalContent content = new UiModalContent
            {
                Title = "清空全部待结算？",
                Subtitle = "仅整理契匣，不写入行市账目",
                Explanation = "全部待结算货签都会被移除；不会成交，也不会改变货囊、银两或持仓。",
                TotalCaption = "待移除货签",
                Total = _pendingRowCount + " 张",
                ConfirmLabel = "全部移除",
                CancelLabel = "保留契匣",
                Confirmed = Clear
            };
            _services.Overlays.ShowModal(content);
        }

        private void Checkout()
        {
            if (!_pending)
            {
                SaveScroll();
                _pending = true;
                Refresh();
                return;
            }
            if (_entries.Count > 0 && CheckoutRequested != null)
                CheckoutRequested(GetOrders());
        }

        private List<TradeRecord> GetSettledRecords()
        {
            List<TradeRecord> result = new List<TradeRecord>();
            if (_state.TradeHistory == null)
                return result;
            int currentWeek = MarketQuery.CurrentWeek(_state);
            for (int i = _state.TradeHistory.Count - 1; i >= 0; i--)
            {
                TradeRecord record = _state.TradeHistory[i];
                if (record != null && record.MarketWeekIndex >= 0 && record.MarketWeekIndex <= currentWeek &&
                    _state.HasItemAppeared(record.ItemId))
                    result.Add(record);
            }
            return result;
        }

        private void GetGrossTotals(out long? outgoing, out long? incoming)
        {
            outgoing = 0;
            incoming = 0;
            foreach (CartEntryModel entry in _entries)
            {
                WeeklyPoint point = MarketQuery.CurrentPoint(_application, entry.ItemId);
                bool expense = entry.Action == TradeAction.Buy || entry.Action == TradeAction.Return;
                long? total = expense ? outgoing : incoming;
                long amount = 0;
                bool quoted = point != null && !MarketQuery.IsDelisted(_application, _state, entry.ItemId) &&
                    CartStackLayout.TryAmount(point.Price, entry.Quantity, entry.Action == TradeAction.Borrow, out amount);
                total = quoted && total.HasValue && amount <= long.MaxValue - total.Value
                    ? total.Value + amount : (long?)null;
                if (expense)
                    outgoing = total;
                else
                    incoming = total;
            }
        }

        private static string SignedMoney(long value)
        {
            return value.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);
        }

        private static string SignedGross(TradeRecord record)
        {
            decimal value = (decimal)record.CashDelta - record.DebtDelta;
            return value.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            if (_window.IsAlive)
            {
                _window.Button("PendingTabButton").onClick.RemoveListener(SelectPending);
                _window.Button("SettledTabButton").onClick.RemoveListener(SelectSettled);
                _window.Button("CartClearButton").onClick.RemoveListener(RequestClear);
                _window.Button("CheckoutButton").onClick.RemoveListener(Checkout);
                _window.Button("SettledPrevButton").onClick.RemoveListener(PreviousSettledPage);
                _window.Button("SettledNextButton").onClick.RemoveListener(NextSettledPage);
            }
            _emptyView.Dispose();
            if (_whiteAmountFont != null) UnityEngine.Object.Destroy(_whiteAmountFont);
            for (int i = 0; i < _views.Count; i++)
                _views[i].Dispose();
            _views.Clear();
            _entries.Clear();
            _entriesAwaitingAnimation.Clear();
            OrdersChanged = null;
            CheckoutRequested = null;
        }

        private sealed class LedgerNumeralWeight : BaseMeshEffect
        {
            // Browser DirectWrite and Unity rasterize the same Bold face differently. Extend
            // only the numeral ink horizontally; the smaller, muted wen unit keeps its own weight.
            private readonly List<UIVertex> _vertices = new List<UIVertex>();

            public override void ModifyMesh(VertexHelper mesh)
            {
                Text text = graphic as Text;
                if (!IsActive() || text == null || !text.text.EndsWith("</color>", StringComparison.Ordinal)) return;
                _vertices.Clear();
                mesh.GetUIVertexStream(_vertices);
                int originalCount = _vertices.Count;
                for (int i = 0; i < originalCount; i++)
                {
                    UIVertex vertex = _vertices[i];
                    if (vertex.color.r < 216) continue;
                    vertex.position.x += .4f;
                    Color32 tint = vertex.color;
                    tint.a = (byte)Mathf.RoundToInt(tint.a * .4f);
                    vertex.color = tint;
                    _vertices.Add(vertex);
                }
                mesh.Clear();
                mesh.AddUIVertexTriangleStream(_vertices);
            }
        }

        private sealed class LedgerDashedRule : BaseMeshEffect
        {
            public override void ModifyMesh(VertexHelper mesh)
            {
                if (!IsActive()) return;
                mesh.Clear();
                Rect rect = graphic.rectTransform.rect;
                for (float x = rect.xMin; x < rect.xMax; x += 5f)
                {
                    float right = Mathf.Min(x + 3f, rect.xMax);
                    int start = mesh.currentVertCount;
                    mesh.AddVert(new Vector2(x, rect.yMin), graphic.color, Vector2.zero);
                    mesh.AddVert(new Vector2(x, rect.yMax), graphic.color, Vector2.zero);
                    mesh.AddVert(new Vector2(right, rect.yMax), graphic.color, Vector2.zero);
                    mesh.AddVert(new Vector2(right, rect.yMin), graphic.color, Vector2.zero);
                    mesh.AddTriangle(start, start + 1, start + 2);
                    mesh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }

        private sealed class CartEntryModel
        {
            internal string ItemId;
            internal TradeAction Action;
            internal int Quantity;
            internal CartStackLayout Stacks;
        }

        private struct PendingRow
        {
            internal CartEntryModel Entry;
            internal int Offset;
            internal int Quantity;
            internal int Limit;
            internal bool CanIncrement;
        }

        private sealed class CartEntryView : IDisposable
        {
            private readonly UiFrontendServices _services;
            private readonly UiPrefabInstance _view;
            private readonly Dictionary<string, ItemDefinition> _definitions;
            private readonly CanvasGroup _canvasGroup;
            private Tween _addedTween;
            private bool _settledLayout;

            internal CartEntryView(
                UiFrontendServices services,
                UiPrefabInstance view,
                Dictionary<string, ItemDefinition> definitions)
            {
                _services = services;
                _view = view;
                _definitions = definitions;
                _canvasGroup = _view.Root.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                    _canvasGroup = _view.Root.gameObject.AddComponent<CanvasGroup>();
                _view.Button("QuantityDecrementButton").onClick.AddListener(Decrease);
                _view.Button("QuantityIncrementButton").onClick.AddListener(Increment);
                _view.Button("RemoveButton").onClick.AddListener(Remove);
                MarketStepperFrame.Attach(_view.Button("QuantityDecrementButton").transform.parent as RectTransform, true);
                MarketIconInteractionState.AttachStepper(
                    _view.Button("QuantityDecrementButton"), services.Motion);
                MarketIconInteractionState.AttachStepper(
                    _view.Button("QuantityIncrementButton"), services.Motion);
                _services.Overlays.AttachTooltip(
                    _view.Button("QuantityIncrementButton").gameObject,
                    "本格增加一件",
                    "本格装满后，可从商品卡另开一格。数量为 1 时按减号移除本张货契。");
            }

            internal event Action<CartEntryView, int> QuantityChanged;
            internal event Action<CartEntryView> RemoveRequested;
            internal CartEntryModel Model { get; private set; }
            internal int RowQuantity { get; private set; }
            internal int RowLimit { get; private set; }
            internal GameObject GameObject { get { return _view.GameObject; } }

            internal void ApplyLayoutMetrics()
            {
                Text name = _view.Text("NameText");
                name.fontSize = 13;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 9;
                name.resizeTextMaxSize = name.fontSize;
                SetNameRect(name.rectTransform, 8f, 23f);
                Text type = _view.Text("TypeText");
                type.fontSize = 10;
                SetNameRect(type.rectTransform, 29f, 17f);
                SetTopLeft(_view.Image("ItemImage").rectTransform, 12f, 9f, 39f, 39f);
                SetTopRight(_view.Button("RemoveButton").GetComponent<RectTransform>(), 9f, 9f, 23f, 23f);
                Transform removeIcon = _view.Button("RemoveButton").transform.Find("Icon");
                if (removeIcon != null)
                    Center(removeIcon as RectTransform, 13f, 13f);
                Transform stepper = _view.Button("QuantityDecrementButton").transform.parent;
                stepper.localScale = Vector3.one;
                SetTopLeft(stepper as RectTransform, 12f, 56f, 72f, 24f);
                SetTopLeft(_view.Button("QuantityDecrementButton").GetComponent<RectTransform>(), 1f, 1f, 23f, 22f);
                SetTopLeft(_view.Text("QuantityText").rectTransform, 24f, 1f, 24f, 22f);
                SetTopLeft(_view.Button("QuantityIncrementButton").GetComponent<RectTransform>(), 48f, 1f, 23f, 22f);
                Center(_view.Button("QuantityDecrementButton").transform.Find("Icon") as RectTransform, 14f, 14f);
                Center(_view.Button("QuantityIncrementButton").transform.Find("Icon") as RectTransform, 14f, 14f);
                _view.Text("QuantityText").fontSize = 11;
                _view.Text("QuantityText").fontStyle = FontStyle.Normal;
                Text time = _view.Text("SettledText");
                time.fontSize = 9;
                SetTopLeft(time.rectTransform, 12f, 55f, 116f, 24f);
                SetAmountLayout(_settledLayout);
            }

            internal void PlayAdded()
            {
                StopAddedAnimation();
                if (!_view.IsAlive || _services.Motion.ReducedMotion)
                    return;
                _canvasGroup.alpha = 0f;
                _view.Root.localScale = Vector3.one * 0.94f;
                try
                {
                    _addedTween = DOVirtual.Float(0f, 1f, 0.22f, delegate(float value)
                    {
                        if (!_view.IsAlive)
                            return;
                        _canvasGroup.alpha = value;
                        _view.Root.localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, value);
                    }).SetUpdate(true).SetEase(UiMotionEasing.PaperOut).OnComplete(delegate
                    {
                        _addedTween = null;
                        ResetAddedVisual();
                    });
                }
                catch (Exception)
                {
                    StopAddedAnimation();
                }
            }

            internal void StopAddedAnimation()
            {
                SafeTween.Kill(_addedTween);
                _addedTween = null;
                ResetAddedVisual();
            }

            private void ResetAddedVisual()
            {
                if (!_view.IsAlive)
                    return;
                _canvasGroup.alpha = 1f;
                _view.Root.localScale = Vector3.one;
            }

            internal void BindPending(ModApplication application, PendingRow row, RunState state)
            {
                CartEntryModel model = row.Entry;
                RowQuantity = row.Quantity;
                RowLimit = row.Limit;
                if (!ReferenceEquals(Model, model))
                    StopAddedAnimation();
                Model = model;
                SetAmountLayout(false);
                ItemDefinition item;
                _definitions.TryGetValue(model.ItemId, out item);
                string name = item == null ? model.ItemId : item.Name;
                bool delisted = MarketQuery.IsDelisted(application, state, model.ItemId);
                bool longSide = model.Action == TradeAction.Buy || model.Action == TradeAction.Sell;
                _view.Image("Background").sprite = _services.Assets.GetSprite(
                    longSide ? "card_cart_entry_long_9s" : "card_cart_entry_short_9s");
                _view.Image("ItemImage").sprite = _services.Assets.GetSprite(MarketQuery.IconKey(item));
                _view.Image("ItemImage").color = Color.white;
                _view.Text("NameText").text = name;
                _view.Text("NameText").color = delisted ? UiFormatting.Muted : UiFormatting.Ink;
                _view.Text("TypeText").text = delisted
                    ? "WASTED · 已退市"
                    : UiFormatting.ActionName(model.Action) + (longSide ? " · 货契" : " · 借契") +
                        " · 本格 " + row.Quantity + "/" + row.Limit;
                _view.Text("TypeText").color = longSide ? UiFormatting.RedDark : UiFormatting.GreenDark;
                _view.Text("QuantityText").text = row.Quantity.ToString();
                _view.Text("SettledText").gameObject.SetActive(false);
                SetStepperActive(true);
                _view.Button("RemoveButton").gameObject.SetActive(true);
                _view.Button("RemoveButton").interactable = true;
                _view.Button("QuantityDecrementButton").interactable = true;
                _view.Button("QuantityIncrementButton").interactable = !delisted && row.CanIncrement;
                SetStepperIconState(_view.Button("QuantityDecrementButton"));
                SetStepperIconState(_view.Button("QuantityIncrementButton"));
                WeeklyPoint point = MarketQuery.CurrentPoint(application, model.ItemId);
                long amount = 0;
                bool known = point != null && CartStackLayout.TryRowAmount(
                    point.Price, row.Offset, row.Quantity, model.Action == TradeAction.Borrow, out amount);
                bool expense = model.Action == TradeAction.Buy || model.Action == TradeAction.Return;
                _view.Text("SubtotalText").text = delisted || point == null
                    ? "—"
                    : !known ? "金额超限" : SignedMoney(expense ? -amount : amount) + " <size=10>文</size>";
            }

            internal void BindSettled(TradeRecord record)
            {
                StopAddedAnimation();
                Model = null;
                SetAmountLayout(true);
                ItemDefinition item;
                _definitions.TryGetValue(record.ItemId, out item);
                string name = item == null ? record.ItemId : item.Name;
                bool longSide = record.Action == TradeActionNames.Buy || record.Action == TradeActionNames.Sell;
                _view.Image("Background").sprite = _services.Assets.GetSprite(
                    longSide ? "card_cart_entry_long_9s" : "card_cart_entry_short_9s");
                _view.Image("ItemImage").sprite = _services.Assets.GetSprite(MarketQuery.IconKey(item));
                _view.Image("ItemImage").color = Color.white;
                _view.Text("NameText").text = name;
                _view.Text("NameText").color = UiFormatting.Ink;
                _view.Text("TypeText").text = UiFormatting.RecordActionName(record.Action) +
                    (longSide ? " · 货契" : " · 借契");
                _view.Text("TypeText").color = longSide ? UiFormatting.RedDark : UiFormatting.GreenDark;
                _view.Text("SettledText").text = string.Format(
                    "{0}年{1}月{2}周 · ×{3}",
                    record.GameYear,
                    record.GameMonth,
                    record.GameRound,
                    record.Quantity);
                _view.Text("SettledText").gameObject.SetActive(true);
                SetStepperActive(false);
                _view.Button("RemoveButton").gameObject.SetActive(false);
                _view.Text("SubtotalText").text =
                    "成交 " + MarketCartPanel.SignedGross(record) + " 文\n银两 " +
                    MarketCartPanel.SignedMoney(record.CashDelta) + " 文";
                if (record.DebtDelta != 0)
                    _view.Text("SubtotalText").text += "\n另记欠款 " + MarketCartPanel.SignedMoney(record.DebtDelta) + " 文";
            }

            private void SetAmountLayout(bool settled)
            {
                _settledLayout = settled;
                Text amount = _view.Text("SubtotalText");
                amount.fontSize = settled ? 9 : 14;
                amount.resizeTextForBestFit = true;
                amount.resizeTextMinSize = settled ? 9 : 8;
                amount.resizeTextMaxSize = amount.fontSize;
                amount.lineSpacing = 1f;
                SetTopRight(amount.rectTransform, 12f, settled ? 43f : 52f,
                    145f, settled ? 41f : 27f);
            }

            private static void SetNameRect(RectTransform rect, float top, float height)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(60f, -top - height);
                rect.offsetMax = new Vector2(-42f, -top);
            }

            private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
            {
                if (rect == null)
                    return;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(left, -top);
                rect.sizeDelta = new Vector2(width, height);
            }

            private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
            {
                if (rect == null)
                    return;
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-right, -top);
                rect.sizeDelta = new Vector2(width, height);
            }

            private static void Center(RectTransform rect, float width, float height)
            {
                if (rect == null)
                    return;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(width, height);
            }

            private static void SetStepperIconState(Button button)
            {
                MarketIconInteractionState.Refresh(button);
            }

            private void SetStepperActive(bool active)
            {
                Transform stepper = _view.Button("QuantityDecrementButton").transform.parent;
                stepper.gameObject.SetActive(active);
            }

            private void Decrease() { Change(-1); }
            private void Increment() { Change(1); }

            private void Change(int quantity)
            {
                if (Model != null && QuantityChanged != null)
                    QuantityChanged(this, quantity);
            }

            private void Remove()
            {
                if (RemoveRequested != null)
                    RemoveRequested(this);
            }

            public void Dispose()
            {
                StopAddedAnimation();
                if (_view.IsAlive)
                {
                    _view.Button("QuantityDecrementButton").onClick.RemoveListener(Decrease);
                    _view.Button("QuantityIncrementButton").onClick.RemoveListener(Increment);
                    _view.Button("RemoveButton").onClick.RemoveListener(Remove);
                }
                Model = null;
                QuantityChanged = null;
                RemoveRequested = null;
                _view.Dispose();
            }
        }
    }
}
