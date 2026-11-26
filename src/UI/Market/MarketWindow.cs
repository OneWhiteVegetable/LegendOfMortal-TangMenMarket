using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketWindow : UiScreen
    {
        private readonly ModApplication _application;
        private readonly RunState _state;
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly List<MarketItemCard> _cards = new List<MarketItemCard>(6);
        private readonly RectTransform[] _goodsRows = new RectTransform[2];
        private readonly MarketRankingPanel _ranking;
        private readonly MarketCartPanel _cart;
        private readonly NewsTickerView _ticker;
        private readonly WeeklyChartView _indexChart;
        private readonly TradeConfirmation _confirmation;
        private readonly MarketLayoutDriver _layoutDriver;
        private bool _showRanking;
        private bool _narrowLayout;
        private bool _wideLayout;
        private bool _shortLayout;
        private int _marketPage;
        private Font _pageNumberFont;

        internal MarketWindow(
            UiFrontendServices services,
            ModApplication application,
            TradeConfirmation confirmation)
            : base(services, "MarketWindow", UiSorting.Market)
        {
            _application = application;
            _state = application.State;
            _definitions = MarketQuery.BuildDefinitions(application.Items);
            _confirmation = confirmation;
            _cart = new MarketCartPanel(services, application, View);
            _cart.OrdersChanged += RefreshDrafts;
            for (int row = 0; row < 2; row++)
            {
                _goodsRows[row] = View.Get<HorizontalLayoutGroup>("GoodsRow" + row)
                    .GetComponent<RectTransform>();
                Transform rowRoot = _goodsRows[row];
                for (int column = 0; column < rowRoot.childCount; column++)
                {
                    MarketItemCard card = new MarketItemCard(services, rowRoot.GetChild(column).gameObject, _cart);
                    card.DetailRequested += OnDetailRequested;
                    card.AddRequested += AddToCart;
                    _cards.Add(card);
                }
            }
            _ranking = new MarketRankingPanel(
                services,
                View.Get<VerticalLayoutGroup>("GoodsGrid").GetComponent<RectTransform>());
            _ranking.DetailRequested += OnDetailRequested;
            _ranking.BackRequested += ShowMarket;
            ConfigureMarketHeading();
            _cart.CheckoutRequested += ConfirmCart;
            _ticker = new NewsTickerView(services, View.Rect("NewsTicker"), _application);
            _indexChart = new WeeklyChartView(
                View.Rect("IndexChartHost"),
                WeeklyChartMode.Spark,
                services.Assets.RegularFont,
                services.Assets.DryBrushTexture);

            View.Button("PreviousPageButton").onClick.AddListener(PreviousPage);
            View.Button("NextPageButton").onClick.AddListener(NextPage);
            MarketBrushArrow.Attach(View.Button("PreviousPageButton"), services.Motion);
            MarketBrushArrow.Attach(View.Button("NextPageButton"), services.Motion, true);
            View.Button("RankingButton").onClick.AddListener(ToggleRanking);
            View.Button("InventoryButton").onClick.AddListener(RequestInventory);
            View.Button("CloseButton").onClick.AddListener(Close);
            View.Button("IndexButton").onClick.AddListener(RequestIndex);
            View.Button("MotionButton").onClick.AddListener(ToggleMotion);
            services.Overlays.AttachTooltip(
                View.Button("RankingButton").gameObject,
                "风云涨跌榜",
                "按各货自首条行情至今的累计涨跌排列，只列本周目已出现商品。",
                "再次点击返回今日货价");
            services.Overlays.AttachTooltip(
                View.Button("InventoryButton").gameObject,
                "行商货囊",
                "查看持货和借货；可平仓、全部平仓或跳转货物详情。",
                "所有平仓仍需预览并确认");
            _layoutDriver = View.Root.gameObject.AddComponent<MarketLayoutDriver>();
            _layoutDriver.Owner = this;
            ApplyLayoutProfile();
        }

        internal event Action<string> DetailRequested;
        internal event Action IndexRequested;
        internal event Action InventoryRequested;
        internal event Action CloseRequested;
        internal MarketCartPanel Cart { get { return _cart; } }

        private void RefreshDrafts()
        {
            if (!IsOpen || _showRanking)
                return;
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].GameObject.activeSelf)
                    _cards[i].UpdateQuantity();
        }

        internal bool IsRanking { get { return _showRanking; } }

        internal void Show()
        {
            Open();
        }

        internal void ShowMarket()
        {
            _showRanking = false;
            Open();
        }

        internal void ShowRanking()
        {
            _showRanking = true;
            Open();
        }

        protected override void OnOpened()
        {
            ApplyLayoutProfile();
            _cart.SetVisible(!_showRanking);
            UiView.SetLabel(
                View.Button("MotionButton"),
                Services.Motion.ReducedMotion ? "动效已减弱" : "减少动效");
            Refresh();
        }

        internal override void Hide()
        {
            _cart.SetVisible(false);
            base.Hide();
        }

        internal void Refresh()
        {
            if (!IsOpen)
                return;
            RefreshMasthead();
            RectTransform ticker = View.Rect("NewsTicker");
            bool hadTicker = ticker.gameObject.activeSelf;
            if (_showRanking)
                ticker.gameObject.SetActive(false);
            else
                _ticker.Bind(_state);
            if (ticker.gameObject.activeSelf != hadTicker)
                View.Root.GetComponent<UiWindowShell>().ApplyNow();
            RefreshContent();
            _cart.Refresh();
            View.Text("StatusText").text = _showRanking
                ? "涨跌榜 · 累计涨跌 · 点击货签查看详情"
                : "真实周目行情 / 契匣整份预览与结算 / 红涨绿跌";
        }

        public override bool CancelFromInput()
        {
            if (_showRanking)
            {
                ShowMarket();
            }
            else
            {
                Close();
            }
            return true;
        }

        private void RefreshMasthead()
        {
            List<WeeklyPoint> history = MarketQuery.IndexHistory(_application);
            WeeklyPoint current = history.Count == 0 ? null : history[history.Count - 1];
            double weekly = MarketQuery.WeeklyReturn(history);
            SetIndexQuote(current == null ? (double?)null : current.Price, weekly);
            if (history.Count > 14)
                history = history.GetRange(history.Count - 14, 14);
            _indexChart.SetPoints(history);
            SetWalletBalance(_application.Wallet == null ? (long?)null : _application.Wallet.Balance);
        }

        private void RefreshContent()
        {
            ApplyPageModeLayout();
            Transform sectionHeader = View.Rect("GoodsArea").Find("SectionHeader");
            if (sectionHeader != null)
                sectionHeader.gameObject.SetActive(!_showRanking);
            View.Rect("CartRail").gameObject.SetActive(!_showRanking);
            _cart.SetVisible(!_showRanking);
            for (int i = 0; i < _goodsRows.Length; i++)
                _goodsRows[i].gameObject.SetActive(!_showRanking);
            VerticalLayoutGroup grid = View.Get<VerticalLayoutGroup>("GoodsGrid");
            grid.gameObject.SetActive(!_showRanking);
            grid.childForceExpandHeight = true;
            int itemCount;
            int currentPage = 0;
            if (_showRanking)
            {
                itemCount = _ranking.Refresh(_application, _state, _application.Items);
            }
            else
            {
                _ranking.Hide();
                List<ItemDefinition> items = MarketQuery.AppearedItems(_state, _application.Items);
                itemCount = items.Count;
                int pageCount = Math.Max(1, (items.Count + 5) / 6);
                _marketPage = Mathf.Clamp(_marketPage, 0, pageCount - 1);
                currentPage = _marketPage;
                for (int i = 0; i < _cards.Count; i++)
                {
                    int itemIndex = _marketPage * 6 + i;
                    if (items.Count == 0 && i == 0)
                        _cards[i].BindEmpty();
                    else if (items.Count > 0 && itemIndex < items.Count)
                        _cards[i].Bind(_application, items[itemIndex], _state, itemIndex + 1);
                    else
                        _cards[i].GameObject.SetActive(false);
                }
            }
            int pages = _showRanking ? 1 : Math.Max(1, (itemCount + 5) / 6);
            string current = (currentPage + 1).ToString("00");
            string total = pages.ToString("00");
            View.Text("PageText").text = current + "  /  " + total;
            View.Text("CurrentPageText").text = current;
            View.Text("TotalPageText").text = "/ " + total;
            View.Button("PreviousPageButton").interactable =
                !_showRanking && itemCount > 0 && currentPage > 0;
            View.Button("NextPageButton").interactable =
                !_showRanking && itemCount > 0 && currentPage < pages - 1;
            Transform pager = View.Rect("GoodsArea").Find("SectionHeader/Pager");
            if (pager != null)
                pager.gameObject.SetActive(!_showRanking);
            if (!_showRanking)
                SetSectionHeading("今日货价", "WEEKLY COMMODITIES");
        }

        private void ConfigureMarketHeading()
        {
            RectTransform section = View.Rect("GoodsArea").Find("SectionHeader") as RectTransform;
            MarketLetterSpacing.Attach(section.Find("Title").GetComponent<Text>(), .1f);
            MarketLetterSpacing.Attach(section.Find("Subtitle").GetComponent<Text>(), .21f);
            Image slash = section.Find("Slash").GetComponent<Image>();
            slash.rectTransform.localRotation = Quaternion.identity;
            UiPaperShape.Attach(slash, 4f);
            RectTransform pager = section.Find("Pager") as RectTransform;
            pager.sizeDelta = new Vector2(198f, pager.sizeDelta.y);
            PlaceHeading(View.Button("NextPageButton").GetComponent<RectTransform>(), 150f, 0f, 48f, 44f);
            Text current = View.Text("CurrentPageText");
            foreach (string installed in Font.GetOSInstalledFontNames())
            {
                if (!string.Equals(installed, "Impact", StringComparison.OrdinalIgnoreCase))
                    continue;
                _pageNumberFont = Font.CreateDynamicFontFromOSFont(installed, 28);
                if (_pageNumberFont != null)
                    current.font = _pageNumberFont;
                break;
            }
            current.fontStyle = _pageNumberFont == null ? FontStyle.Normal : FontStyle.Italic;
            current.horizontalOverflow = HorizontalWrapMode.Overflow;
            current.verticalOverflow = VerticalWrapMode.Overflow;
            MarketLetterSpacing.Attach(current, 0f, Mathf.Tan(7f * Mathf.Deg2Rad));
            PlaceHeading(current.rectTransform, 56f, 2f, 38f, 32f);
            Image line = pager.Find("PageUnderline").GetComponent<Image>();
            PlaceHeading(line.rectTransform, 55f, 35f, 39f, 4f);
            UiPaperShape.Attach(line, .5f);
            Image tag = pager.Find("TotalPageBack").GetComponent<Image>();
            RectTransform back = tag.rectTransform;
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(.5f, .5f);
            back.sizeDelta = new Vector2(32f, 20f);
            back.anchoredPosition = new Vector2(117f, -26f);
            back.localRotation = Quaternion.Euler(0f, 0f, -3f);
            UiPaperShape.Attach(tag);
            Text total = View.Text("TotalPageText");
            total.font = Services.Assets.BoldFont;
            total.fontSize = 12;
            total.resizeTextForBestFit = false;
            total.horizontalOverflow = HorizontalWrapMode.Overflow;
            total.verticalOverflow = VerticalWrapMode.Overflow;
            total.rectTransform.anchorMin = Vector2.zero;
            total.rectTransform.anchorMax = Vector2.one;
            total.rectTransform.offsetMin = total.rectTransform.offsetMax = Vector2.zero;
        }

        private static void PlaceHeading(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void SetSectionHeading(string title, string subtitle)
        {
            Transform goodsArea = View.Rect("GoodsArea");
            Transform titleTransform = goodsArea.Find("SectionHeader/Title");
            Transform subtitleTransform = goodsArea.Find("SectionHeader/Subtitle");
            Text titleText = titleTransform == null ? null : titleTransform.GetComponent<Text>();
            Text subtitleText = subtitleTransform == null ? null : subtitleTransform.GetComponent<Text>();
            if (titleText != null)
                titleText.text = title;
            if (subtitleText != null)
                subtitleText.text = subtitle;
        }

        private void PreviousPage()
        {
            if (_showRanking)
                return;
            _marketPage = Math.Max(0, _marketPage - 1);
            RefreshContent();
        }

        private void NextPage()
        {
            if (_showRanking)
                return;
            _marketPage++;
            RefreshContent();
        }

        private void ToggleRanking()
        {
            if (_showRanking)
                ShowMarket();
            else
                ShowRanking();
        }

        private void ApplyLayoutProfile()
        {
            float physicalWidth = Screen.width > 0 ? Screen.width : View.Root.rect.width;
            float physicalHeight = Screen.height > 0 ? Screen.height : View.Root.rect.height;
            if (physicalWidth <= 0f)
                physicalWidth = 1920f;
            if (physicalHeight <= 0f)
                physicalHeight = 1080f;
            float contentScale = MarketLayoutMetrics.ContentScale(physicalWidth, physicalHeight);
            float viewportWidth = MarketLayoutMetrics.EffectiveSize(physicalWidth, contentScale);
            float viewportHeight = MarketLayoutMetrics.EffectiveSize(physicalHeight, contentScale);
            bool narrow = viewportWidth <= 1450f;
            bool wide = viewportWidth >= 1700f;
            bool compactHeight = viewportHeight <= 790f;
            _narrowLayout = narrow;
            _wideLayout = wide;
            _shortLayout = compactHeight;


            float railWidth = narrow ? 294f : wide ? 360f : 330f;
            RectTransform rail = View.Rect("CartRail");
            rail.offsetMin = new Vector2(-railWidth, rail.offsetMin.y);
            rail.offsetMax = new Vector2(0f, rail.offsetMax.y);

            ApplyPageModeLayout();

            float headerHeight = narrow ? 31f : 34f;
            float headingBox = headerHeight - (compactHeight ? 7f : 10f);
            RectTransform section = View.Rect("GoodsArea").Find("SectionHeader") as RectTransform;
            if (section != null)
            {
                section.offsetMin = new Vector2(section.offsetMin.x, -headingBox);
                section.offsetMax = new Vector2(section.offsetMax.x, 0f);
                RectTransform title = section.Find("Title") as RectTransform;
                if (title != null)
                {
                    title.anchoredPosition = new Vector2(31f, 0f);
                    title.sizeDelta = new Vector2(88f, headingBox);
                }
                RectTransform subtitle = section.Find("Subtitle") as RectTransform;
                if (subtitle != null)
                {
                    subtitle.anchoredPosition = new Vector2(127f, -(headingBox - 20f) * .5f - 3.5f);
                    subtitle.sizeDelta = new Vector2(180f, 20f);
                }
                RectTransform slash = section.Find("Slash") as RectTransform;
                if (slash != null)
                    PlaceHeading(slash, -2f, headingBox * .5f - .7f, 26f, 9f);
                RectTransform pager = section.Find("Pager") as RectTransform;
                if (pager != null)
                {
                    float overflow = (44f - headingBox) * 0.5f;
                    pager.offsetMin = new Vector2(pager.offsetMin.x, -overflow);
                    pager.offsetMax = new Vector2(pager.offsetMax.x, overflow);
                }
            }

            VerticalLayoutGroup grid = View.Get<VerticalLayoutGroup>("GoodsGrid");
            grid.spacing = narrow ? 11f : wide ? 18f : 15f;
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            gridRect.offsetMax = new Vector2(gridRect.offsetMax.x, -headerHeight);
            for (int i = 0; i < _goodsRows.Length; i++)
            {
                HorizontalLayoutGroup row = _goodsRows[i].GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                    row.spacing = narrow ? 12f : wide ? 20f : 16f;
            }

            Transform cartTitleTransform = View.Rect("CartRail").Find("Title");
            Text cartTitle = cartTitleTransform == null ? null : cartTitleTransform.GetComponent<Text>();
            if (cartTitle != null)
                cartTitle.fontSize = compactHeight ? 17 : 20;
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].SetLayoutProfile(narrow, wide, compactHeight);
            _cart.SetLayoutProfile(narrow, wide);
            ApplyCartLayoutProfile(compactHeight);
        }

        private void ApplyCartLayoutProfile(bool compactHeight)
        {
            float footerHeight = MarketLayoutMetrics.CartFooterHeight(compactHeight);
            float listTop = MarketLayoutMetrics.CartListTop(compactHeight);
            float horizontal = compactHeight ? 12f : 15f;
            RectTransform footer = View.Rect("CartRail").Find("CartFooter") as RectTransform;
            RectTransform scroll = View.Scroll("CartListScroll").GetComponent<RectTransform>();
            footer.offsetMin = new Vector2(footer.offsetMin.x, 0f);
            footer.offsetMax = new Vector2(footer.offsetMax.x, footerHeight);
            scroll.offsetMin = new Vector2(scroll.offsetMin.x, footerHeight);
            _cart.SetListTop(listTop);

            RectTransform rail = View.Rect("CartRail");
            RectTransform tabs = rail.Find("CartTabs") as RectTransform;
            float tabsTop = compactHeight ? 49f : 72f;
            float tabsHeight = compactHeight ? 29f : 40f;
            tabs.offsetMin = new Vector2(tabs.offsetMin.x, -tabsTop - tabsHeight);
            tabs.offsetMax = new Vector2(tabs.offsetMax.x, -tabsTop);
            SetTopAndHeight(rail.Find("Title") as RectTransform,
                compactHeight ? 8f : 10f, compactHeight ? 22f : 32f);
            SetTopAndHeight(rail.Find("Subtitle") as RectTransform,
                compactHeight ? 32f : 41f, compactHeight ? 10f : 18f);

            SetFooterLine(View.Text("BuyCaptionText").rectTransform, horizontal,
                compactHeight ? 9f : 8f, compactHeight ? 20f : 26f);
            SetFooterLine(View.Text("BuyTotalText").rectTransform, horizontal,
                compactHeight ? 9f : 8f, compactHeight ? 20f : 26f);
            SetFooterLine(View.Text("SellCaptionText").rectTransform, horizontal,
                compactHeight ? 34f : 35f, compactHeight ? 20f : 26f);
            SetFooterLine(View.Text("SellTotalText").rectTransform, horizontal,
                compactHeight ? 34f : 35f, compactHeight ? 20f : 26f);
            SetFooterLine(View.Text("CashCaptionText").rectTransform, horizontal,
                compactHeight ? 67f : 70f, compactHeight ? 30f : 36f);
            SetFooterLine(View.Text("CashDeltaText").rectTransform, horizontal,
                compactHeight ? 67f : 66f, compactHeight ? 30f : 42f);

            RectTransform divider = footer.Find("TotalDivider") as RectTransform;
            float dividerTop = compactHeight ? 60f : 65f;
            divider.offsetMin = new Vector2(horizontal, -dividerTop - 1f);
            divider.offsetMax = new Vector2(-horizontal, -dividerTop);
            Text note = View.Text("FooterNoteText");
            note.gameObject.SetActive(!compactHeight);
            note.rectTransform.offsetMin = new Vector2(horizontal, -190f);
            note.rectTransform.offsetMax = new Vector2(-horizontal, -166f);
            note.resizeTextForBestFit = false;
            note.fontSize = 8;

            RectTransform checkout = View.Button("CheckoutButton").GetComponent<RectTransform>();
            float checkoutBottom = compactHeight ? 12f : 33f;
            float checkoutHeight = compactHeight ? 36f : 43f;
            checkout.offsetMin = new Vector2(horizontal, checkoutBottom);
            checkout.offsetMax = new Vector2(-horizontal, checkoutBottom + checkoutHeight);
        }

        private static void SetFooterLine(RectTransform rect, float horizontal, float top, float height)
        {
            float x = rect.anchorMin.x >= 1f ? -horizontal : horizontal;
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static void SetTopAndHeight(RectTransform rect, float top, float height)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private void ApplyPageModeLayout()
        {
            RectTransform goodsArea = View.Rect("GoodsArea");
            if (_showRanking)
            {
                goodsArea.offsetMin = Vector2.zero;
                goodsArea.offsetMax = Vector2.zero;
                return;
            }
            float railWidth = _narrowLayout ? 294f : _wideLayout ? 360f : 330f;
            float rightPadding = _narrowLayout ? 13f : _wideLayout ? 23f : 18f;
            goodsArea.offsetMin = new Vector2(
                _narrowLayout ? 15f : _wideLayout ? 26f : 21f,
                _narrowLayout ? 10f : _wideLayout ? 15f : 12f);
            goodsArea.offsetMax = new Vector2(
                -(railWidth + rightPadding),
                -(_shortLayout ? 9f : _wideLayout ? 20f : 17f));
        }

        internal void AddFromDetail(string itemId, TradeAction action, int quantity)
        {
            AddToCart(itemId, action, quantity, null);
        }

        internal void AddFromDetail(string itemId, TradeAction action, int quantity, RectTransform sourceRect)
        {
            AddToCart(itemId, action, quantity, sourceRect);
        }

        private void AddToCart(string itemId, TradeAction action, int quantity, RectTransform sourceRect)
        {
            if (MarketQuery.IsDelisted(_application, _state, itemId))
            {
                Services.Overlays.ShowToast("商品已退市，仓位冻结，禁止交易与平仓。", 2.8f, UiToastKind.Error);
                return;
            }
            if (!_cart.Add(itemId, action, quantity))
                return;

            ItemDefinition item;
            string name = _definitions.TryGetValue(itemId, out item) && item != null ? item.Name : itemId;
            Services.Overlays.ShowToast(
                name + " · " + UiFormatting.ActionName(action) + "已纳入契匣。",
                2.8f,
                UiToastKind.Success);
            if (sourceRect != null)
            {
                Services.Overlays.PlayAddToCart(
                    sourceRect,
                    IsOpen ? View.Rect("CartRail") : null,
                    Services.Assets.GetSprite(MarketQuery.IconKey(item)));
            }
        }

        private void ConfirmCart(List<TradeOrder> orders)
        {
            _confirmation.Present(orders, "立契结算", delegate
            {
                _cart.Clear();
                if (IsOpen)
                    Refresh();
            });
        }

        private void OnDetailRequested(string itemId)
        {
            if (DetailRequested != null)
                DetailRequested(itemId);
        }

        private void RequestIndex()
        {
            if (IndexRequested != null)
                IndexRequested();
        }

        private void RequestInventory()
        {
            if (InventoryRequested != null)
                InventoryRequested();
        }

        private void Close()
        {
            if (CloseRequested != null)
                CloseRequested();
        }

        private void ToggleMotion()
        {
            Services.Motion.Toggle();
            UiView.SetLabel(
                View.Button("MotionButton"),
                Services.Motion.ReducedMotion ? "动效已减弱" : "减少动效");
        }

        public override void Dispose()
        {
            if (View.IsAlive)
            {
                View.Button("PreviousPageButton").onClick.RemoveListener(PreviousPage);
                View.Button("NextPageButton").onClick.RemoveListener(NextPage);
                View.Button("RankingButton").onClick.RemoveListener(ToggleRanking);
                View.Button("InventoryButton").onClick.RemoveListener(RequestInventory);
                View.Button("CloseButton").onClick.RemoveListener(Close);
                View.Button("IndexButton").onClick.RemoveListener(RequestIndex);
                View.Button("MotionButton").onClick.RemoveListener(ToggleMotion);
            }
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].DetailRequested -= OnDetailRequested;
                _cards[i].AddRequested -= AddToCart;
                _cards[i].Dispose();
            }
            _cards.Clear();
            _ranking.DetailRequested -= OnDetailRequested;
            _ranking.BackRequested -= ShowMarket;
            _ranking.Dispose();
            _cart.OrdersChanged -= RefreshDrafts;
            _cart.CheckoutRequested -= ConfirmCart;
            _cart.Dispose();
            if (_pageNumberFont != null)
                UnityEngine.Object.Destroy(_pageNumberFont);
            _indexChart.Dispose();
            DetailRequested = null;
            IndexRequested = null;
            InventoryRequested = null;
            CloseRequested = null;
            if (_layoutDriver != null)
            {
                _layoutDriver.Owner = null;
                _layoutDriver.enabled = false;
                UnityEngine.Object.Destroy(_layoutDriver);
            }
            base.Dispose();
        }

        private sealed class MarketLayoutDriver : MonoBehaviour
        {
            internal MarketWindow Owner;
            private int _width = -1;
            private int _height = -1;

            private void LateUpdate()
            {
                if (Owner == null)
                {
                    enabled = false;
                    return;
                }
                if (Screen.width == _width && Screen.height == _height)
                    return;
                _width = Screen.width;
                _height = Screen.height;
                Owner.ApplyLayoutProfile();
            }
        }
    }
}
