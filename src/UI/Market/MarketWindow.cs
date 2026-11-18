using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    public sealed class MarketWindow : IDisposable
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f, 1f);
        private static readonly Color Paper = new Color(0.91f, 0.88f, 0.79f, 1f);

        private readonly UiFactory _ui;
        private readonly RunState _state;
        private readonly ItemCatalog _items;
        private readonly TradingService _trading;
        private readonly Action _onChanged;
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly List<MarketItemCard> _cards = new List<MarketItemCard>(6);

        private RectTransform _root;
        private RectTransform _marketContent;
        private RectTransform _marketView;
        private Text _indexValue;
        private Text _indexChange;
        private Text _newsText;
        private Text _pageText;
        private WeeklyChartGraphic _indexChart;
        private Button _previousButton;
        private Button _nextButton;
        private Button _rankingButton;
        private Button _closeButton;
        private MarketItemDetail _detail;
        private MarketRankingPanel _ranking;
        private MarketCartPanel _cart;
        private UiModal _modal;
        private UiToast _toast;
        private UiTooltip _tooltip;
        private Image _stamp;
        private CanvasGroup _stampGroup;
        private GameObject _previousSelected;
        private int _pageIndex;
        private bool _showRanking;
        private string _detailItemId;
        private float _stampRemaining;
        private bool _disposed;

        public MarketWindow(
            Transform parent,
            UiFactory ui,
            RunState state,
            ItemCatalog items,
            TradingService trading,
            CapacityService capacity,
            Action onChanged)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            if (ui == null)
                throw new ArgumentNullException("ui");
            if (state == null)
                throw new ArgumentNullException("state");
            if (items == null)
                throw new ArgumentNullException("items");
            if (trading == null)
                throw new ArgumentNullException("trading");
            if (capacity == null)
                throw new ArgumentNullException("capacity");

            _ui = ui;
            _state = state;
            _items = items;
            _trading = trading;
            _onChanged = onChanged;
            _definitions = MarketView.BuildDefinitions(items);

            ConfigureCanvas(parent);
            Image window = ui.CreatePanel(
                parent,
                "XingShangMod.MarketWindow",
                "panel_merchant_window_9s",
                null,
                Color.white);
            _root = window.rectTransform;
            UiFactory.Stretch(_root);
            MarketWindowLifecycle lifecycle = window.gameObject.AddComponent<MarketWindowLifecycle>();
            lifecycle.Owner = this;

            BuildHeader();
            BuildBody(capacity);

            _modal = UiModal.Create(ui, _root);
            _toast = UiToast.Create(ui, _root);
            _tooltip = UiTooltip.Create(ui, _root);
            _tooltip.Attach(_rankingButton.gameObject, "风云涨跌榜（只列入已出现商品）");
            _tooltip.Attach(_closeButton.gameObject, "离开唐门行市");
            _tooltip.Attach(_previousButton.gameObject, "上一页");
            _tooltip.Attach(_nextButton.gameObject, "下一页");
            BuildStamp();

            _root.gameObject.SetActive(false);
        }

        public bool IsOpen
        {
            get { return !_disposed && _root != null && _root.gameObject.activeSelf; }
        }

        public void Show()
        {
            if (_disposed || _root == null)
                return;
            if (!IsOpen && EventSystem.current != null)
                _previousSelected = EventSystem.current.currentSelectedGameObject;

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            ShowMarketContent();
            _showRanking = false;
            _marketView.gameObject.SetActive(true);
            _ranking.Root.gameObject.SetActive(false);
            SetButtonLabel(_rankingButton, "涨跌榜");
            Refresh();
        }

        public void Hide()
        {
            if (_disposed || _root == null)
                return;

            _modal.Hide();
            _tooltip.Hide();
            _toast.Clear();
            HideStamp();
            _root.gameObject.SetActive(false);
            RestoreSelection();
        }

        public void Refresh()
        {
            if (_disposed || _root == null)
                return;

            RefreshHeader();
            RefreshMarket();
            _ranking.Refresh();
            if (!string.IsNullOrEmpty(_detailItemId))
            {
                ItemDefinition item;
                if (_state.HasItemAppeared(_detailItemId) && _items.TryGetItem(_detailItemId, out item))
                    _detail.Bind(item);
                else
                    ShowMarketContent();
            }
            _cart.Refresh();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].DetailRequested -= ShowDetail;
                _cards[i].AddRequested -= AddToCart;
                _cards[i].Dispose();
            }
            _cards.Clear();

            if (_detail != null)
            {
                _detail.BackRequested -= ShowMarketContent;
                _detail.AddRequested -= AddToCart;
                _detail.Dispose();
            }
            if (_ranking != null)
            {
                _ranking.BackRequested -= ShowMarket;
                _ranking.DetailRequested -= ShowDetail;
                _ranking.Dispose();
            }
            if (_cart != null)
            {
                _cart.ConfirmRequested -= ConfirmCart;
                _cart.Dispose();
            }

            if (_previousButton != null)
                _previousButton.onClick.RemoveListener(PreviousPage);
            if (_nextButton != null)
                _nextButton.onClick.RemoveListener(NextPage);
            if (_rankingButton != null)
                _rankingButton.onClick.RemoveListener(ToggleRanking);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);

            if (_modal != null)
                _modal.Hide();
            if (_tooltip != null)
                _tooltip.Hide();
            if (_toast != null)
                _toast.Clear();
            if (_root != null)
            {
                SafeTween.Kill(_root.gameObject);
                _root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
            RestoreSelection();
            _detailItemId = null;
        }

        private void ConfigureCanvas(Transform parent)
        {
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildHeader()
        {
            Image masthead = _ui.CreatePanel(
                _root,
                "Masthead",
                "panel_masthead_9s",
                null,
                Color.white);
            UiFactory.SetRect(
                masthead.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(-32f, 84f));

            Image brandBlock = _ui.CreatePanel(
                masthead.transform,
                "Brand",
                "panel_brand_block",
                new Vector2(300f, 72f),
                Color.white);
            UiFactory.SetTopLeft(brandBlock.rectTransform, new Vector2(12f, -6f), new Vector2(300f, 72f));
            Image brandMark = _ui.CreateImage(
                brandBlock.transform,
                "Seal",
                "brand_shang_mark",
                new Vector2(58f, 58f),
                true);
            UiFactory.SetTopLeft(brandMark.rectTransform, new Vector2(10f, -7f), new Vector2(58f, 58f));
            Text brandTitle = _ui.CreateText(
                brandBlock.transform,
                "Title",
                "唐门行市",
                31,
                Paper,
                TextAnchor.MiddleLeft,
                new Vector2(208f, 42f));
            UiFactory.SetTopLeft(brandTitle.rectTransform, new Vector2(78f, -7f), new Vector2(208f, 42f));
            Text brandSubtitle = _ui.CreateText(
                brandBlock.transform,
                "Subtitle",
                "TANGMEN EXCHANGE",
                12,
                new Color(0.75f, 0.71f, 0.63f, 1f),
                TextAnchor.UpperLeft,
                new Vector2(208f, 22f));
            UiFactory.SetTopLeft(brandSubtitle.rectTransform, new Vector2(80f, -47f), new Vector2(208f, 22f));

            Text indexTitle = _ui.CreateText(
                masthead.transform,
                "IndexTitle",
                "唐门指数",
                16,
                Paper,
                TextAnchor.MiddleLeft,
                new Vector2(150f, 24f));
            UiFactory.SetTopLeft(indexTitle.rectTransform, new Vector2(332f, -9f), new Vector2(150f, 24f));
            _indexValue = _ui.CreateText(
                masthead.transform,
                "IndexValue",
                string.Empty,
                30,
                Paper,
                TextAnchor.MiddleLeft,
                new Vector2(174f, 42f));
            UiFactory.SetTopLeft(_indexValue.rectTransform, new Vector2(330f, -32f), new Vector2(174f, 42f));
            _indexChange = _ui.CreateText(
                masthead.transform,
                "IndexChange",
                string.Empty,
                16,
                Paper,
                TextAnchor.MiddleLeft,
                new Vector2(130f, 32f));
            UiFactory.SetTopLeft(_indexChange.rectTransform, new Vector2(488f, -38f), new Vector2(130f, 32f));
            _indexChart = WeeklyChartGraphic.Create(masthead.transform, WeeklyChartMode.Card, _ui.GameFont);
            _indexChart.HorizontalGridLines = 0;
            _indexChart.LineWidth = 2.2f;
            _indexChart.PlotPadding = new Vector4(2f, 4f, 2f, 4f);
            UiFactory.SetTopLeft(_indexChart.rectTransform, new Vector2(620f, -12f), new Vector2(200f, 58f));

            _closeButton = _ui.CreateButton(
                masthead.transform,
                "Close",
                "离开",
                Hide,
                19,
                new Vector2(82f, 58f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            SetTopRight(_closeButton, new Vector2(-16f, -13f), new Vector2(82f, 58f));
            _rankingButton = _ui.CreateButton(
                masthead.transform,
                "Ranking",
                "涨跌榜",
                ToggleRanking,
                19,
                new Vector2(104f, 58f),
                "button_text_action_normal_9s",
                "button_text_action_hover_9s",
                null,
                "button_text_action_disabled_9s");
            SetTopRight(_rankingButton, new Vector2(-108f, -13f), new Vector2(104f, 58f));

            Image ticker = _ui.CreatePanel(
                _root,
                "LatestNews",
                "panel_story_news_9s",
                null,
                Color.white);
            UiFactory.SetRect(
                ticker.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -104f),
                new Vector2(-32f, 38f));
            Text tickerLabel = _ui.CreateText(
                ticker.transform,
                "Label",
                "最新快报",
                17,
                new Color(0.70f, 0.22f, 0.16f, 1f),
                TextAnchor.MiddleCenter,
                new Vector2(112f, 30f));
            UiFactory.SetTopLeft(tickerLabel.rectTransform, new Vector2(8f, -4f), new Vector2(112f, 30f));
            _newsText = _ui.CreateText(
                ticker.transform,
                "Text",
                string.Empty,
                17,
                Ink,
                TextAnchor.MiddleLeft);
            _newsText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Stretch(_newsText.rectTransform, new Vector2(128f, 3f), new Vector2(-16f, -3f));
        }

        private void BuildBody(CapacityService capacity)
        {
            Image body = _ui.CreatePanel(
                _root,
                "Body",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            body.raycastTarget = false;
            UiFactory.Stretch(body.rectTransform, new Vector2(24f, 28f), new Vector2(-24f, -150f));

            Image marketContent = _ui.CreatePanel(
                body.transform,
                "MarketContent",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            marketContent.raycastTarget = false;
            UiFactory.Stretch(marketContent.rectTransform);
            _marketContent = marketContent.rectTransform;

            Image goodsArea = _ui.CreatePanel(
                _marketContent,
                "GoodsArea",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            goodsArea.raycastTarget = false;
            UiFactory.Stretch(goodsArea.rectTransform, Vector2.zero, new Vector2(-366f, 0f));

            Image marketView = _ui.CreatePanel(
                goodsArea.transform,
                "MarketView",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            marketView.raycastTarget = false;
            UiFactory.Stretch(marketView.rectTransform);
            _marketView = marketView.rectTransform;

            Text sectionTitle = _ui.CreateText(
                _marketView,
                "SectionTitle",
                "今日货价",
                26,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(180f, 48f));
            UiFactory.SetTopLeft(sectionTitle.rectTransform, new Vector2(8f, -2f), new Vector2(180f, 48f));
            Text sectionSubtitle = _ui.CreateText(
                _marketView,
                "SectionSubtitle",
                "WEEKLY COMMODITIES",
                12,
                new Color(0.35f, 0.31f, 0.25f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(210f, 36f));
            UiFactory.SetTopLeft(sectionSubtitle.rectTransform, new Vector2(160f, -8f), new Vector2(210f, 36f));

            _nextButton = _ui.CreateButton(
                _marketView,
                "NextPage",
                "›",
                NextPage,
                34,
                new Vector2(48f, 44f),
                "button_brush_arrow_normal",
                "button_brush_arrow_hover",
                "button_brush_arrow_pressed",
                "button_brush_arrow_disabled");
            SetTopRight(_nextButton, new Vector2(-4f, -2f), new Vector2(48f, 44f));
            _previousButton = _ui.CreateButton(
                _marketView,
                "PreviousPage",
                "‹",
                PreviousPage,
                34,
                new Vector2(48f, 44f),
                "button_brush_arrow_normal",
                "button_brush_arrow_hover",
                "button_brush_arrow_pressed",
                "button_brush_arrow_disabled");
            SetTopRight(_previousButton, new Vector2(-178f, -2f), new Vector2(48f, 44f));
            _pageText = _ui.CreateText(
                _marketView,
                "PageCount",
                string.Empty,
                20,
                Ink,
                TextAnchor.MiddleCenter,
                new Vector2(120f, 44f));
            SetTopRight(_pageText, new Vector2(-54f, -2f), new Vector2(120f, 44f));

            Image cardsRoot = _ui.CreatePanel(
                _marketView,
                "Cards",
                null,
                null,
                new Color(0f, 0f, 0f, 0f));
            cardsRoot.raycastTarget = false;
            UiFactory.Stretch(cardsRoot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, -54f));
            for (int i = 0; i < 6; i++)
            {
                MarketItemCard card = new MarketItemCard(cardsRoot.transform, _ui);
                card.Root.name = "GoodsCard." + i.ToString(CultureInfo.InvariantCulture);
                int column = i % 3;
                int row = i / 3;
                float bottom = row == 0 ? 0.5f : 0f;
                float top = row == 0 ? 1f : 0.5f;
                card.Root.anchorMin = new Vector2(column / 3f, bottom);
                card.Root.anchorMax = new Vector2((column + 1) / 3f, top);
                card.Root.offsetMin = new Vector2(5f, 5f);
                card.Root.offsetMax = new Vector2(-5f, -5f);
                card.DetailRequested += ShowDetail;
                card.AddRequested += AddToCart;
                _cards.Add(card);
            }

            _ranking = new MarketRankingPanel(goodsArea.transform, _ui, _state, _items);
            UiFactory.Stretch(_ranking.Root);
            _ranking.BackRequested += ShowMarket;
            _ranking.DetailRequested += ShowDetail;
            _ranking.Root.gameObject.SetActive(false);

            _cart = new MarketCartPanel(
                _marketContent,
                _ui,
                _state,
                _items,
                _trading,
                capacity,
                _definitions);
            UiFactory.SetRect(
                _cart.Root,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(344f, 0f));
            _cart.ConfirmRequested += ConfirmCart;

            _detail = new MarketItemDetail(body.transform, _ui, _state);
            UiFactory.Stretch(_detail.Root);
            _detail.BackRequested += ShowMarketContent;
            _detail.AddRequested += AddToCart;
            _detail.Root.gameObject.SetActive(false);

            Text footer = _ui.CreateText(
                _root,
                "Footer",
                "红涨绿跌 · 买卖借还同匣原子结算 · 仅显示本周目已发生行情",
                14,
                new Color(0.33f, 0.29f, 0.23f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(920f, 22f));
            UiFactory.SetRect(
                footer.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(28f, 4f),
                new Vector2(920f, 22f));
        }

        private void BuildStamp()
        {
            _stamp = _ui.CreateImage(
                _root,
                "DealStamp",
                "stamp_deal_chengjiao",
                new Vector2(400f, 250f),
                true);
            _stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            _stampGroup = _stamp.gameObject.AddComponent<CanvasGroup>();
            _stampGroup.interactable = false;
            _stampGroup.blocksRaycasts = false;
            _stamp.gameObject.SetActive(false);
        }

        private void RefreshHeader()
        {
            List<WeeklyPoint> indexHistory = MarketView.GetVisibleIndexHistory(_state);
            WeeklyPoint current = indexHistory.Count == 0 ? null : indexHistory[indexHistory.Count - 1];
            double value = current == null ? _state.CurrentIndex : current.Price;
            double weeklyReturn = current == null ? 0.0 : current.Return;
            _indexValue.text = MarketView.FormatNumber(value, 2);
            _indexValue.color = MarketView.GetTrendColor(weeklyReturn);
            _indexChange.text = MarketView.FormatReturn(weeklyReturn);
            _indexChange.color = MarketView.GetTrendColor(weeklyReturn);
            if (indexHistory.Count > 24)
                indexHistory = indexHistory.GetRange(indexHistory.Count - 24, 24);
            _indexChart.SetPoints(indexHistory);

            NewsRecord latest = MarketView.GetLatestNews(_state, null);
            if (latest == null)
            {
                _newsText.text = "本周暂无异动快报。";
                return;
            }
            ItemDefinition item;
            string itemName = _items.TryGetItem(latest.ItemId, out item) ? item.Name : latest.ItemId;
            _newsText.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} · {1} {2}　{3}",
                latest.Direction >= 0 ? "利多" : "利空",
                itemName,
                MarketView.FormatReturn(latest.Return),
                latest.Text);
        }

        private void RefreshMarket()
        {
            List<ItemDefinition> appeared = MarketView.GetAppearedItems(_state, _items);
            int pageCount = (appeared.Count + 5) / 6;
            if (pageCount <= 0)
            {
                _pageIndex = 0;
                _pageText.text = "00 / 00";
            }
            else
            {
                _pageIndex = Mathf.Clamp(_pageIndex, 0, pageCount - 1);
                _pageText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00} / {1:00}",
                    _pageIndex + 1,
                    pageCount);
            }
            _previousButton.interactable = pageCount > 0 && _pageIndex > 0;
            _nextButton.interactable = pageCount > 0 && _pageIndex < pageCount - 1;

            for (int i = 0; i < _cards.Count; i++)
            {
                int itemIndex = _pageIndex * 6 + i;
                bool active = itemIndex < appeared.Count;
                _cards[i].Root.gameObject.SetActive(active);
                if (active)
                    _cards[i].Bind(appeared[itemIndex], _state);
            }
        }

        private void PreviousPage()
        {
            if (_pageIndex > 0)
            {
                _pageIndex--;
                RefreshMarket();
            }
        }

        private void NextPage()
        {
            _pageIndex++;
            RefreshMarket();
        }

        private void ToggleRanking()
        {
            ShowMarketContent();
            _showRanking = !_showRanking;
            _marketView.gameObject.SetActive(!_showRanking);
            _ranking.Root.gameObject.SetActive(_showRanking);
            SetButtonLabel(_rankingButton, _showRanking ? "今日货价" : "涨跌榜");
            if (_showRanking)
                _ranking.Refresh();
        }

        private void ShowMarket()
        {
            _showRanking = false;
            ShowMarketContent();
            _marketView.gameObject.SetActive(true);
            _ranking.Root.gameObject.SetActive(false);
            SetButtonLabel(_rankingButton, "涨跌榜");
        }

        private void ShowMarketContent()
        {
            _detailItemId = null;
            if (_detail != null)
                _detail.Root.gameObject.SetActive(false);
            if (_marketContent != null)
                _marketContent.gameObject.SetActive(true);
        }

        private void ShowDetail(string itemId)
        {
            if (_disposed || !_state.HasItemAppeared(itemId))
                return;
            ItemDefinition item;
            if (!_items.TryGetItem(itemId, out item))
                return;

            _showRanking = false;
            _marketView.gameObject.SetActive(true);
            _ranking.Root.gameObject.SetActive(false);
            SetButtonLabel(_rankingButton, "涨跌榜");
            _detailItemId = itemId;
            _marketContent.gameObject.SetActive(false);
            _detail.Root.gameObject.SetActive(true);
            _detail.Bind(item);
        }

        private void AddToCart(string itemId, TradeAction action, int quantity)
        {
            if (MarketView.IsDelisted(_state, itemId))
            {
                _toast.Show("商品已退市，仓位冻结，禁止交易与平仓。");
                return;
            }
            _cart.Add(itemId, action, quantity);
            ItemDefinition item;
            string name = _items.TryGetItem(itemId, out item) ? item.Name : itemId;
            _toast.Show(name + " · " + MarketView.GetActionName(action) + "已纳入契匣。");
        }

        private void ConfirmCart()
        {
            List<TradeOrder> orders = _cart.GetOrders();
            TradeResult preview = _trading.Preview(orders);
            _cart.Refresh();
            if (!preview.Success)
            {
                _toast.Show(MarketView.GetErrorMessage(preview, _items), 3.5f);
                return;
            }

            string message = _cart.BuildPreviewMessage(preview);
            _modal.Show(
                "确认立契",
                message,
                "确认成交",
                delegate { ExecuteOrders(orders); },
                "返回修改");
        }

        private void ExecuteOrders(List<TradeOrder> orders)
        {
            TradeResult result = _trading.Execute(orders);
            if (!result.Success)
            {
                _cart.Refresh();
                _toast.Show(MarketView.GetErrorMessage(result, _items), 4f);
                return;
            }

            _cart.Clear();
            Refresh();
            ShowStamp();
            _toast.Show("契匣已结算，货囊与银两账目已更新。", 2.2f);
            if (_onChanged != null)
                _onChanged();
        }

        private void ShowStamp()
        {
            HideStamp();
            _stampRemaining = 1.8f;
            _stampGroup.alpha = 1f;
            _stamp.gameObject.SetActive(true);
            _stamp.rectTransform.SetAsLastSibling();
        }

        private void HideStamp()
        {
            _stampRemaining = 0f;
            if (_stamp != null)
                _stamp.gameObject.SetActive(false);
        }

        internal void Tick()
        {
            if (_disposed)
                return;
            if (_stampRemaining > 0f)
            {
                _stampRemaining -= Time.unscaledDeltaTime;
                if (_stampGroup != null)
                    _stampGroup.alpha = Mathf.Clamp01(_stampRemaining / 0.25f);
                if (_stampRemaining <= 0f)
                    HideStamp();
            }
            if (!Input.GetKeyDown(KeyCode.Escape) || _modal.IsOpen)
                return;
            if (!string.IsNullOrEmpty(_detailItemId))
                ShowMarketContent();
            else
                Hide();
        }

        private void RestoreSelection()
        {
            if (EventSystem.current != null && _previousSelected != null && _previousSelected.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_previousSelected);
            _previousSelected = null;
        }

        private static void SetTopRight(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                position,
                size);
        }

        private static void SetButtonLabel(Button button, string value)
        {
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = value;
        }
    }

    internal sealed class MarketWindowLifecycle : MonoBehaviour
    {
        internal MarketWindow Owner;

        private void Update()
        {
            if (Owner != null)
                Owner.Tick();
        }

        private void OnDestroy()
        {
            if (Owner != null)
                Owner.Dispose();
            Owner = null;
        }
    }

    internal static class MarketView
    {
        internal static Dictionary<string, ItemDefinition> BuildDefinitions(ItemCatalog catalog)
        {
            Dictionary<string, ItemDefinition> result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                ItemDefinition item = catalog.Items[i];
                if (item != null && !result.ContainsKey(item.ItemId))
                    result.Add(item.ItemId, item);
            }
            return result;
        }

        internal static List<ItemDefinition> GetAppearedItems(RunState state, ItemCatalog catalog)
        {
            List<ItemDefinition> result = new List<ItemDefinition>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (state.AppearedItemIds == null)
                return result;
            for (int i = 0; i < state.AppearedItemIds.Count; i++)
            {
                string itemId = state.AppearedItemIds[i];
                ItemDefinition item;
                if (!string.IsNullOrEmpty(itemId) && seen.Add(itemId) && catalog.TryGetItem(itemId, out item))
                    result.Add(item);
            }
            return result;
        }

        internal static List<WeeklyPoint> GetVisibleHistory(RunState state, string itemId)
        {
            List<WeeklyPoint> result = new List<WeeklyPoint>();
            if (state.PriceHistory == null || !state.HasItemAppeared(itemId))
                return result;
            int currentWeek = GetCurrentWeek(state);
            for (int i = 0; i < state.PriceHistory.Count; i++)
            {
                WeeklyPoint point = state.PriceHistory[i];
                if (point != null && point.MarketWeekIndex <= currentWeek &&
                    string.Equals(point.ItemId, itemId, StringComparison.Ordinal) && IsFinite(point.Price))
                    result.Add(point);
            }
            result.Sort(ComparePoints);
            return result;
        }

        internal static List<WeeklyPoint> GetVisibleIndexHistory(RunState state)
        {
            List<WeeklyPoint> result = new List<WeeklyPoint>();
            if (state.IndexHistory == null)
                return result;
            int currentWeek = GetCurrentWeek(state);
            for (int i = 0; i < state.IndexHistory.Count; i++)
            {
                WeeklyPoint point = state.IndexHistory[i];
                if (point != null && point.MarketWeekIndex <= currentWeek && IsFinite(point.Price))
                    result.Add(point);
            }
            result.Sort(ComparePoints);
            return result;
        }

        internal static WeeklyPoint GetCurrentPoint(RunState state, string itemId)
        {
            List<WeeklyPoint> history = GetVisibleHistory(state, itemId);
            return history.Count == 0 ? null : history[history.Count - 1];
        }

        internal static bool IsDelisted(RunState state, string itemId)
        {
            SecurityBinding binding = state.FindSecurityBinding(itemId);
            if (binding != null && binding.IsDelisted)
                return true;
            WeeklyPoint point = GetCurrentPoint(state, itemId);
            return point != null && point.Delisted;
        }

        internal static long GetShortQuantity(RunState state, string itemId)
        {
            long result = 0;
            if (state.ShortBatches == null)
                return result;
            for (int i = 0; i < state.ShortBatches.Count; i++)
            {
                ShortBatch batch = state.ShortBatches[i];
                if (batch != null && batch.Quantity > 0 &&
                    string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    result += batch.Quantity;
            }
            return result;
        }

        internal static int GetNearestDueWeek(RunState state, string itemId)
        {
            int result = int.MaxValue;
            if (state.ShortBatches == null)
                return -1;
            for (int i = 0; i < state.ShortBatches.Count; i++)
            {
                ShortBatch batch = state.ShortBatches[i];
                if (batch != null && batch.Quantity > 0 && batch.DueWeek < result &&
                    string.Equals(batch.ItemId, itemId, StringComparison.Ordinal))
                    result = batch.DueWeek;
            }
            return result == int.MaxValue ? -1 : result;
        }

        internal static NewsRecord GetLatestNews(RunState state, string itemId)
        {
            NewsRecord latest = null;
            if (state.NewsHistory == null)
                return null;
            int currentWeek = GetCurrentWeek(state);
            for (int i = 0; i < state.NewsHistory.Count; i++)
            {
                NewsRecord record = state.NewsHistory[i];
                if (record == null || record.MarketWeekIndex > currentWeek ||
                    !state.HasItemAppeared(record.ItemId) ||
                    (!string.IsNullOrEmpty(itemId) && !string.Equals(record.ItemId, itemId, StringComparison.Ordinal)))
                    continue;
                if (latest == null || record.MarketWeekIndex >= latest.MarketWeekIndex)
                    latest = record;
            }
            return latest;
        }

        internal static int GetCurrentWeek(RunState state)
        {
            return state.Clock == null ? 0 : state.Clock.MarketWeekIndex;
        }

        internal static string FormatNumber(double value, int decimals = 0)
        {
            if (!IsFinite(value))
                return "—";
            return value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        internal static string FormatReturn(double value)
        {
            if (!IsFinite(value))
                return "—";
            double percent = value * 100.0;
            string arrow = percent > 0.0 ? "▲ " : percent < 0.0 ? "▼ " : "— ";
            string sign = percent > 0.0 ? "+" : string.Empty;
            return arrow + sign + percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        internal static Color GetTrendColor(double value)
        {
            if (IsFinite(value) && value > 0.0)
                return new Color(0.70f, 0.20f, 0.14f, 1f);
            if (IsFinite(value) && value < 0.0)
                return new Color(0.18f, 0.43f, 0.28f, 1f);
            return new Color(0.34f, 0.30f, 0.24f, 1f);
        }

        internal static string GetActionName(TradeAction action)
        {
            switch (action)
            {
                case TradeAction.Buy: return "买货";
                case TradeAction.Sell: return "卖货";
                case TradeAction.Borrow: return "借货";
                case TradeAction.Return: return "还货";
                default: return "交易";
            }
        }

        internal static string GetRecordActionName(string action)
        {
            switch (action)
            {
                case TradeActionNames.Buy: return "买货";
                case TradeActionNames.Sell: return "卖货";
                case TradeActionNames.Borrow: return "借货";
                case TradeActionNames.Return: return "还货";
                case TradeActionNames.Expire: return "到期还货";
                default: return action;
            }
        }

        internal static string GetIconSpriteId(ItemDefinition item)
        {
            string path = item == null ? string.Empty : item.IconPath ?? string.Empty;
            int slash = path.LastIndexOf('/');
            if (slash >= 0)
                path = path.Substring(slash + 1);
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 4);
            return path;
        }

        internal static string GetErrorMessage(TradeResult result, ItemCatalog catalog)
        {
            if (result == null)
                return "交易未能完成，请重新预览。";
            string message = string.IsNullOrEmpty(result.Message) ? "交易未能完成，请重新预览。" : result.Message;
            ItemDefinition item;
            if (!string.IsNullOrEmpty(result.ItemId) && catalog.TryGetItem(result.ItemId, out item))
                message = item.Name + "：" + message;
            if (result.Error == TradeError.InsufficientCash)
            {
                message += string.Format(
                    CultureInfo.InvariantCulture,
                    "\n需付 {0} 文，可用 {1} 文。",
                    result.RequiredAmount,
                    result.AvailableAmount);
            }
            else if (result.Error == TradeError.InsufficientHolding ||
                     result.Error == TradeError.InsufficientShortPosition)
            {
                message += string.Format(
                    CultureInfo.InvariantCulture,
                    "\n指令 {0} 件，可用 {1} 件。",
                    result.RequiredAmount,
                    result.AvailableAmount);
            }
            return message;
        }

        private static int ComparePoints(WeeklyPoint left, WeeklyPoint right)
        {
            return left.MarketWeekIndex.CompareTo(right.MarketWeekIndex);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
