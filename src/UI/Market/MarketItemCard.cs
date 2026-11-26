using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
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
    internal sealed class MarketItemCard : IDisposable
    {
        private const float DesignWidth = 468f;
        private const float DesignHeight = 384f;
        private readonly UiFrontendServices _services;
        private readonly UiPrefabInstance _view;
        private readonly WeeklyChartView _chart;
        private readonly string _motionId;
        private RectTransform _motionPivot;
        private RectTransform _visualContent;
        private RectTransform _hardShadow;
        private RectTransform _nameBack;
        private RectTransform _nameRedLine;
        private RectTransform _quoteBack;
        private RectTransform _changeBack;
        private RectTransform _actionBand;
        private RectTransform _itemRect;
        private RectTransform _itemVisualRect;
        private RectTransform _goldLine;
        private RectTransform _categorySkew;
        private bool _categorySkewIsBound;
        private CardSkewEffect[] _skewEffects;
        private GoodsCardMotionDriver _motionDriver;
        private MarketTradeButtonMotion _buyButtonMotion;
        private MarketTradeButtonMotion _borrowButtonMotion;
        private Sequence _motionSequence;
        private Vector2 _itemRestPosition;
        private Vector2 _shadowRestPosition;
        private Vector3 _itemRestScale;
        private Quaternion _itemRestRotation;
        private Vector2 _lastRootSize;
        private Color _chartRestColor = new Color(1f, 1f, 1f, 0.54f);
        private float _designScale = 1f;
        private int _priceMaximum = 37;
        private float _skew;
        private bool _narrowLayout;
        private bool _wideLayout;
        private bool _shortLayout;
        private bool _hovered;
        private bool _disposed;
        private string _itemId;
        private ItemDefinition _tooltipItem;
        private int _quantity = 1;
        private bool _delisted;
        private readonly MarketCartPanel _cart;

        internal MarketItemCard(UiFrontendServices services, GameObject instance, MarketCartPanel cart)
        {
            _services = services;
            _cart = cart;
            _motionId = "XingShang.GoodsCard." + GetHashCode();
            _view = services.Assets.Adopt("GoodsCard", instance);
            BuildMotionHierarchy();
            ConfigureStepperFrame();
            ConfigureTypography();
            _chart = new WeeklyChartView(
                _view.Rect("ChartHost"),
                WeeklyChartMode.Card,
                services.Assets.RegularFont,
                services.Assets.DryBrushTexture);
            _motionDriver = instance.GetComponent<GoodsCardMotionDriver>();
            if (_motionDriver == null)
                _motionDriver = instance.AddComponent<GoodsCardMotionDriver>();
            _motionDriver.Bind(this);
            _view.Button("BuyButton").onClick.AddListener(AddBuy);
            _view.Button("BorrowButton").onClick.AddListener(AddBorrow);
            _view.Button("QuantityDecrementButton").onClick.AddListener(Decrease);
            _view.Button("QuantityIncrementButton").onClick.AddListener(Increment);
            MarketIconInteractionState.AttachStepper(
                _view.Button("QuantityDecrementButton"), services.Motion);
            MarketIconInteractionState.AttachStepper(
                _view.Button("QuantityIncrementButton"), services.Motion);
            _buyButtonMotion = MarketTradeButtonMotion.Attach(_view.Button("BuyButton"), services.Motion, UiFormatting.Red);
            _borrowButtonMotion = MarketTradeButtonMotion.Attach(_view.Button("BorrowButton"), services.Motion, UiFormatting.Green);
            RegisterSkewEffects();
            MarketTextInkEffect.Attach(_view, "NameText", MarketTextInkKind.GoodsTitle);
            MarketTextInkEffect.Attach(_view, "CategoryText", MarketTextInkKind.GoodsCategory);
            services.Overlays.AttachTooltip(
                instance,
                TooltipTitle,
                TooltipBody,
                TooltipHint,
                false);
            services.Overlays.AttachTooltip(
                _view.Button("BuyButton").gameObject,
                string.Empty,
                "加入购物车 · 做多",
                string.Empty,
                false);
            services.Overlays.AttachTooltip(
                _view.Button("BorrowButton").gameObject,
                () => string.Empty,
                _cart.BorrowHint,
                () => string.Empty,
                false);
            services.Motion.ReducedMotionChanged += OnReducedMotionChanged;
        }

        internal event Action<string> DetailRequested;
        internal event Action<string, TradeAction, int, RectTransform> AddRequested;
        internal GameObject GameObject { get { return _view.GameObject; } }

        private string TooltipTitle()
        {
            return _tooltipItem == null ? string.Empty : _tooltipItem.Name;
        }

        private string TooltipBody()
        {
            return _tooltipItem == null ? string.Empty : _tooltipItem.Description ?? string.Empty;
        }

        private string TooltipHint()
        {
            return _tooltipItem == null
                ? string.Empty
                : "点击查看行情 · mod:" + _tooltipItem.ItemId;
        }

        private void BindTooltipItem(ItemDefinition item)
        {
            _tooltipItem = item;
            UiTooltipTrigger trigger = _view.GameObject.GetComponent<UiTooltipTrigger>();
            if (trigger != null)
                trigger.InvalidateBinding();
        }

        internal void SetLayoutProfile(bool narrow, bool wide, bool compactHeight)
        {
            _narrowLayout = narrow;
            _wideLayout = wide;
            _shortLayout = compactHeight;
            ApplyResponsiveMetrics();
        }

        internal void Bind(
            ModApplication application,
            ItemDefinition item,
            RunState state,
            int visibleNumber)
        {
            ApplyDesignContentScale();
            BindTooltipItem(item);
            ResetMotion();
            if (item == null)
            {
                GameObject.SetActive(false);
                return;
            }
            if (!string.Equals(_itemId, item.ItemId, StringComparison.Ordinal))
            {
                _itemId = item.ItemId;
                _quantity = 1;
            }
            GameObject.SetActive(true);
            List<WeeklyPoint> history = MarketQuery.ItemHistory(application, item.ItemId);
            WeeklyPoint current = history.Count == 0 ? null : history[history.Count - 1];
            double weeklyReturn = MarketQuery.WeeklyReturn(history);
            _delisted = MarketQuery.IsDelisted(application, state, item.ItemId);
            Color trend = _delisted ? UiFormatting.Muted : UiFormatting.Trend(weeklyReturn);
            string number = visibleNumber.ToString("00");

            _view.Text("IndexText").text = number;
            _view.Text("NameText").text = item.Name;
            UpdateNamePaper();
            _view.Text("CategoryText").text = "行货 · " + number;
            UpdateCategoryPaper();
            _view.Image("ItemImage").sprite = _services.Assets.GetSprite(MarketQuery.IconKey(item));
            _view.Image("ItemImage").color = _delisted
                ? new Color(0.45f, 0.47f, 0.45f, 0.72f)
                : Color.white;
            BindPrice(current, trend);
            _view.Text("DeltaText").text = current == null || history.Count < 2
                ? "—"
                : UiFormatting.Delta(current.Price - history[history.Count - 2].Price, "文");
            _view.Text("DeltaText").color = trend;
            _view.Text("ChangeText").text = current == null ? "—" : UiFormatting.Percent(weeklyReturn);
            _view.Text("ChangeText").color = trend;
            UpdateChangePaper();
            if (history.Count > 26)
                history = history.GetRange(history.Count - 26, 26);
            _chart.SetPoints(history);
            _chartRestColor = _delisted
                ? new Color(0.45f, 0.46f, 0.44f, 0.54f)
                : new Color(1f, 1f, 1f, 0.54f);
            _chart.Chart.color = _chartRestColor;

            _view.Image("DelistedMark").gameObject.SetActive(_delisted);
            UiView.SetButtonSprites(
                _view.Button("OpenButton"),
                _services.Assets,
                _delisted ? "card_goods_delisted" : "card_goods_normal",
                _delisted ? "card_goods_delisted" : "card_goods_normal",
                _delisted ? "card_goods_delisted" : "card_goods_normal",
                _delisted ? "card_goods_delisted" : "card_goods_normal");
            UpdateQuantity();
            _view.Button("OpenButton").transition = Selectable.Transition.SpriteSwap;
            _view.Button("OpenButton").interactable = true;
        }

        internal void BindEmpty()
        {
            ApplyDesignContentScale();
            BindTooltipItem(null);
            ResetMotion();
            _itemId = null;
            _quantity = 1;
            _delisted = true;
            GameObject.SetActive(true);
            _view.Text("IndexText").text = "00";
            _view.Text("NameText").text = "今日无货";
            UpdateNamePaper();
            _view.Text("CategoryText").text = "EMPTY";
            UpdateCategoryPaper();
            _view.Image("ItemImage").sprite = _services.Assets.GetSprite("icon_cart");
            _view.Image("ItemImage").color = UiFormatting.Muted;
            BindPrice(null, UiFormatting.Muted);
            _view.Text("DeltaText").text = "尚未出现可交易货物";
            _view.Text("DeltaText").color = UiFormatting.Muted;
            _view.Text("ChangeText").text = string.Empty;
            UpdateChangePaper();
            _chart.SetPoints(new List<WeeklyPoint>());
            _chartRestColor = new Color(1f, 1f, 1f, 0.54f);
            _chart.Chart.color = _chartRestColor;
            _view.Image("DelistedMark").gameObject.SetActive(false);
            UiView.SetButtonSprites(
                _view.Button("OpenButton"),
                _services.Assets,
                "card_goods_normal",
                "card_goods_normal",
                "card_goods_normal",
                "card_goods_normal");
            _view.Button("OpenButton").transition = Selectable.Transition.None;
            _view.Button("OpenButton").interactable = false;
            UpdateQuantity();
        }

        private void ConfigureStepperFrame()
        {
            MarketStepperFrame.Attach(_view.Button("QuantityDecrementButton").transform.parent as RectTransform, false);
        }

        private void BuildMotionHierarchy()
        {
            RectTransform root = _view.Root;
            Transform motion = root.Find("MotionPivot");
            if (motion == null)
                motion = CreateRect("MotionPivot", root);
            _motionPivot = motion as RectTransform;
            Stretch(_motionPivot);
            Transform content = motion.Find("VisualContent");
            if (content == null)
                content = CreateRect("VisualContent", motion);
            _visualContent = content as RectTransform;
            _visualContent.anchorMin = new Vector2(0.5f, 0.5f);
            _visualContent.anchorMax = new Vector2(0.5f, 0.5f);
            _visualContent.pivot = new Vector2(0.5f, 0.5f);
            _visualContent.anchoredPosition = Vector2.zero;
            _visualContent.sizeDelta = new Vector2(DesignWidth, DesignHeight);
            _visualContent.localScale = Vector3.one;

            int index = 0;
            while (index < root.childCount)
            {
                Transform child = root.GetChild(index);
                if (child == motion)
                {
                    index++;
                    continue;
                }
                child.SetParent(_visualContent, false);
            }
            ConfigureCategoryHierarchy();
            _nameBack = _visualContent.Find("NameBack") as RectTransform;
            if (_nameBack != null)
            {
                Shadow nameShadow = _nameBack.GetComponent<Shadow>();
                if (nameShadow != null)
                    nameShadow.enabled = false;
                Transform redLine = _nameBack.Find("NameRedLine");
                if (redLine == null)
                    redLine = CreateImage("NameRedLine", _nameBack, null).transform;
                _nameRedLine = redLine as RectTransform;
                Image redLineImage = _nameRedLine.GetComponent<Image>();
                redLineImage.color = UiFormatting.Red;
                redLineImage.raycastTarget = false;
                _nameRedLine.anchorMin = new Vector2(1f, 0f);
                _nameRedLine.anchorMax = new Vector2(1f, 1f);
                _nameRedLine.pivot = new Vector2(0f, 0.5f);
            }
            Transform legacySlash = _visualContent.Find("TitleSlash");
            if (legacySlash != null)
                legacySlash.gameObject.SetActive(false);
            Transform quoteGoldEdge = _visualContent.Find("QuoteGoldEdge");
            if (quoteGoldEdge != null)
                quoteGoldEdge.gameObject.SetActive(false);
            Transform quoteBack = _visualContent.Find("QuoteBack");
            if (quoteBack == null)
                quoteBack = CreateImage("QuoteBack", _visualContent, null).transform;
            _quoteBack = quoteBack as RectTransform;
            Image quoteImage = _quoteBack.GetComponent<Image>();
            quoteImage.color = new Color(245f / 255f, 240f / 255f, 228f / 255f, 0.86f);
            quoteImage.raycastTarget = false;
            if (_quoteBack.GetComponent<QuotePaperEffect>() == null)
                _quoteBack.gameObject.AddComponent<QuotePaperEffect>();
            if (_quoteBack.GetSiblingIndex() > _view.Text("PriceText").transform.GetSiblingIndex())
                _quoteBack.SetSiblingIndex(_view.Text("PriceText").transform.GetSiblingIndex());
            Transform changeBack = _visualContent.Find("ChangeBack");
            if (changeBack == null)
                changeBack = CreateImage("ChangeBack", _visualContent, null).transform;
            _changeBack = changeBack as RectTransform;
            Image changeImage = _changeBack.GetComponent<Image>();
            changeImage.color = new Color(32f / 255f, 35f / 255f, 33f / 255f, 0.045f);
            changeImage.raycastTarget = false;
            if (_changeBack.GetSiblingIndex() > _view.Text("ChangeText").transform.GetSiblingIndex())
                _changeBack.SetSiblingIndex(_view.Text("ChangeText").transform.GetSiblingIndex());
            Transform actionBand = _visualContent.Find("ActionBand");
            if (actionBand == null)
                actionBand = CreateImage("ActionBand", _visualContent, null).transform;
            _actionBand = actionBand as RectTransform;
            Image actionImage = _actionBand.GetComponent<Image>();
            actionImage.color = new Color(245f / 255f, 240f / 255f, 228f / 255f, 0.96f);
            actionImage.raycastTarget = false;
            if (_actionBand.GetSiblingIndex() > _view.Button("QuantityDecrementButton").transform.parent.GetSiblingIndex())
                _actionBand.SetSiblingIndex(_view.Button("QuantityDecrementButton").transform.parent.GetSiblingIndex());
            Transform actionTop = _actionBand.Find("ActionTopLine");
            if (actionTop == null)
                actionTop = CreateImage("ActionTopLine", _actionBand, null).transform;
            Image actionTopImage = actionTop.GetComponent<Image>();
            actionTopImage.color = new Color(32f / 255f, 35f / 255f, 33f / 255f, 0.14f);
            actionTopImage.raycastTarget = false;

            Image source = _view.Image("Background");
            Sprite backgroundSprite = source.sprite;
            Image.Type backgroundType = source.type;
            Color backgroundColor = source.color;
            Shadow sourceShadow = root.GetComponent<Shadow>();
            _shadowRestPosition = sourceShadow == null
                ? new Vector2(4f, -5f)
                : sourceShadow.effectDistance;
            if (sourceShadow != null)
                sourceShadow.enabled = false;

            Image shadow = CreateImage("HardShadow", _visualContent, backgroundSprite);
            _hardShadow = shadow.rectTransform;
            Stretch(_hardShadow);
            _hardShadow.anchoredPosition = _shadowRestPosition;
            shadow.type = backgroundType;
            shadow.color = Color.clear;
            shadow.enabled = false;

            Image background = CreateImage("VisualBackground", _visualContent, backgroundSprite);
            Stretch(background.rectTransform);
            background.type = backgroundType;
            background.color = backgroundColor;
            source.color = Color.clear;
            source.raycastTarget = true;
            Button openButton = _view.Button("OpenButton");
            openButton.targetGraphic = background;

            Transform line = _visualContent.Find("HoverGoldLine");
            if (line == null)
                line = CreateImage("HoverGoldLine", _visualContent, null).transform;
            _goldLine = line as RectTransform;
            ConfigureGoldLine(_goldLine);
            _hardShadow.SetAsFirstSibling();
            background.rectTransform.SetSiblingIndex(1);
            _goldLine.SetAsLastSibling();
            _motionPivot.anchoredPosition = Vector2.zero;
            _motionPivot.localRotation = Quaternion.identity;
            _motionPivot.localScale = Vector3.one;

            _itemVisualRect = _view.Image("ItemImage").rectTransform;
            Transform itemParent = _itemVisualRect.parent;
            int itemSiblingIndex = _itemVisualRect.GetSiblingIndex();
            _itemRect = CreateRect("ItemArtLayout", itemParent);
            _itemRect.SetSiblingIndex(itemSiblingIndex);
            _itemRect.anchorMin = _itemVisualRect.anchorMin;
            _itemRect.anchorMax = _itemVisualRect.anchorMax;
            _itemRect.pivot = _itemVisualRect.pivot;
            _itemRect.anchoredPosition = _itemVisualRect.anchoredPosition;
            _itemRect.sizeDelta = _itemVisualRect.sizeDelta;
            _itemRect.localScale = _itemVisualRect.localScale;
            _itemRect.localRotation = _itemVisualRect.localRotation;
            _itemVisualRect.SetParent(_itemRect, false);
            Stretch(_itemVisualRect);
            _itemVisualRect.anchoredPosition = Vector2.zero;
            _itemVisualRect.localRotation = Quaternion.identity;
            _itemRestPosition = _itemRect.anchoredPosition;
            _itemRestScale = _itemVisualRect.localScale;
            _itemRestRotation = _itemVisualRect.localRotation;
            ApplyDesignContentScale();
        }

        private void ConfigureCategoryHierarchy()
        {
            Transform existing = _visualContent.Find("CategorySkew");
            if (existing != null)
            {
                _categorySkew = _view.Rect("CategorySkew");
                _categorySkewIsBound = true;
            }
            else
            {
                RectTransform legacyBack = _visualContent.Find("CategoryBack") as RectTransform;
                RectTransform legacyText = _view.Text("CategoryText").rectTransform;
                if (legacyBack == null || legacyText.parent != _visualContent)
                    return;
                _categorySkew = CreateRect("CategorySkew", _visualContent);
                _categorySkew.SetSiblingIndex(legacyBack.GetSiblingIndex());
                _categorySkew.anchorMin = legacyBack.anchorMin;
                _categorySkew.anchorMax = legacyBack.anchorMax;
                _categorySkew.pivot = legacyBack.pivot;
                _categorySkew.anchoredPosition = legacyBack.anchoredPosition;
                _categorySkew.sizeDelta = legacyBack.sizeDelta;
                _categorySkew.localScale = legacyBack.localScale;
                _categorySkew.localRotation = legacyBack.localRotation;
                legacyBack.SetParent(_categorySkew, false);
                legacyText.SetParent(_categorySkew, false);
            }

            RectTransform categoryBack = _categorySkew.Find("CategoryBack") as RectTransform;
            if (categoryBack != null)
            {
                Stretch(categoryBack);
                UiPaperShape.Attach(categoryBack.GetComponent<Image>());
            }
            Stretch(_view.Text("CategoryText").rectTransform);
        }

        private void ApplyDesignContentScale()
        {
            if (_visualContent == null || _view.Root == null)
                return;
            Rect rootRect = _view.Root.rect;
            if (rootRect.width <= 0f || rootRect.height <= 0f)
                return;
            if (Mathf.Abs(_lastRootSize.x - rootRect.width) < 0.01f &&
                Mathf.Abs(_lastRootSize.y - rootRect.height) < 0.01f)
                return;

            _lastRootSize = new Vector2(rootRect.width, rootRect.height);
            _designScale = rootRect.width / DesignWidth;
            _visualContent.sizeDelta = new Vector2(DesignWidth, rootRect.height / _designScale);
            _visualContent.localScale = new Vector3(_designScale, _designScale, 1f);
            ApplyResponsiveMetrics();
        }

        private void ApplyResponsiveMetrics()
        {
            if (_visualContent == null || _itemRect == null || _designScale <= 0f)
                return;
            float scale = _designScale;
            float inverse = 1f / scale;
            float physicalWidth = Screen.width > 0 ? Screen.width : 1920f;
            float physicalHeight = Screen.height > 0 ? Screen.height : 1080f;
            float contentScale = MarketLayoutMetrics.ContentScale(physicalWidth, physicalHeight);
            float viewportWidth = MarketLayoutMetrics.EffectiveSize(physicalWidth, contentScale);
            float viewportHeight = MarketLayoutMetrics.EffectiveSize(physicalHeight, contentScale);

            Text name = _view.Text("NameText");
            float namePixels = Mathf.Clamp(viewportWidth * 0.0102f, 14f, 19f);
            name.fontSize = Mathf.Max(1, Mathf.RoundToInt(namePixels * inverse));
            name.resizeTextMaxSize = name.fontSize;
            name.resizeTextMinSize = Mathf.Min(14, name.fontSize);
            Text price = _view.Text("PriceText");
            float pricePixels = Mathf.Clamp(viewportWidth * 0.02f, 24f, 37f);
            _priceMaximum = Mathf.Max(1, Mathf.RoundToInt(pricePixels * inverse));
            price.fontSize = _priceMaximum;
            Text delta = _view.Text("DeltaText");
            delta.fontSize = Mathf.Max(1, Mathf.RoundToInt((_narrowLayout ? 10f : 11f) * inverse));
            Text change = _view.Text("ChangeText");
            change.fontSize = Mathf.Max(1, Mathf.CeilToInt(
                Mathf.Clamp(viewportWidth * 0.009f, 11f, 15f) * inverse));
            _view.Text("PriceUnitText").fontSize = Mathf.Max(1, Mathf.RoundToInt(10f * inverse));
            _view.Text("CategoryText").fontSize = Mathf.Max(1, Mathf.RoundToInt(8f * inverse));
            _view.Text("IndexText").fontSize = Mathf.Max(1, Mathf.RoundToInt((_narrowLayout ? 72f : 91f) * inverse));
            _view.Text("QuantityText").fontSize = Mathf.Max(1, Mathf.RoundToInt(12f * inverse));

            RectTransform ghost = _view.Text("IndexText").rectTransform;
            SetTopRight(ghost, 12f * inverse, 30f * inverse, 184f * inverse, 100f * inverse);
            Shadow nameShadow = _nameBack.GetComponent<Shadow>();
            if (nameShadow != null)
            {
                nameShadow.effectColor = UiFormatting.Red;
                nameShadow.effectDistance = new Vector2(3f * inverse, 0f);
                nameShadow.useGraphicAlpha = false;
            }
            Shadow nameTextShadow = _view.Text("NameText").GetComponent<Shadow>();
            if (nameTextShadow != null)
                nameTextShadow.effectDistance = new Vector2(1f * inverse, -1f * inverse);
            UpdateCategoryPaper();

            float padding = _wideLayout ? 15f : _narrowLayout ? 9f : 12f;
            float topPadding = _wideLayout ? 14f : _narrowLayout ? 9f : 12f;
            float bottomPadding = _wideLayout ? 12f : _narrowLayout ? 8f : 10f;
            float stepperHeight = _shortLayout ? 26f : 29f;
            float tradeHeight = _shortLayout ? 27f : _narrowLayout ? 29f : 31f;
            float actionHeight = (_shortLayout ? 4f : 7f) + Mathf.Max(stepperHeight, tradeHeight) + 1f;
            float headerHeight = Mathf.Round(namePixels + 10f);
            float cardHeight = _visualContent.rect.height * scale;
            float contentWidth = _visualContent.rect.width * scale - 2f - padding * 2f;
            float bodyHeight = MarketLayoutMetrics.CardBodyHeight(
                cardHeight, topPadding, bottomPadding, headerHeight, actionHeight);
            float bodyTop = 1f + topPadding + headerHeight;
            float itemHeight = _shortLayout
                ? Mathf.Clamp(viewportHeight * 0.123f, 68f, 96f)
                : _wideLayout
                    ? Mathf.Clamp(viewportHeight * 0.165f, 125f, 186f)
                    : Mathf.Clamp(viewportHeight * 0.135f, 82f, 148f);
            float itemWidth = contentWidth * 0.44f;
            float itemTop = MarketLayoutMetrics.CenteredTop(bodyTop, bodyHeight, itemHeight);
            SetTopLeft(
                _itemRect,
                (1f + padding) * inverse,
                itemTop * inverse,
                itemWidth * inverse,
                itemHeight * inverse);
            _itemRestPosition = _itemRect.anchoredPosition;

            RectTransform halftone = _visualContent.Find("Halftone") as RectTransform;
            if (halftone != null)
            {
                float dotSize = itemWidth * 0.82f;
                SetTopLeft(
                    halftone,
                    (1f + padding + (itemWidth - dotSize) * 0.5f) * inverse,
                    MarketLayoutMetrics.CenteredTop(bodyTop, bodyHeight, dotSize) * inverse,
                    dotSize * inverse,
                    dotSize * inverse);
            }

            float changeHeight = _shortLayout ? MarketLayoutMetrics.CompactQuoteChangeHeight(viewportWidth) : 26f;
            float quoteHeight = _shortLayout ? MarketLayoutMetrics.CompactQuoteHeight(pricePixels, changeHeight) : pricePixels + 67f;
            float quoteLeft = 1f + padding + itemWidth + 7f;
            float quoteWidth = contentWidth - itemWidth - 7f;
            float quoteTop = MarketLayoutMetrics.CenteredTop(bodyTop, bodyHeight, quoteHeight) - (_shortLayout ? 1f : 0f);
            float textLeft = quoteLeft + (_shortLayout ? 8f : 12f);
            float textWidth = quoteWidth - (_shortLayout ? 15f : 19f);
            float priceTop = quoteTop + (_shortLayout ? 6f : 9f);
            SetTopLeft(_quoteBack, quoteLeft * inverse, quoteTop * inverse,
                quoteWidth * inverse, quoteHeight * inverse);
            SetTopLeft(price.rectTransform,
                textLeft * inverse, priceTop * inverse, (textWidth - 19f) * inverse, pricePixels * inverse);
            FitPrice(price);
            SetTopLeft(_view.Text("PriceUnitText").rectTransform,
                (quoteLeft + quoteWidth - 22f) * inverse, priceTop * inverse,
                15f * inverse, pricePixels * inverse);
            SetTopLeft(delta.rectTransform,
                textLeft * inverse, (quoteTop + pricePixels + (_shortLayout ? 9f : 14f)) * inverse,
                textWidth * inverse, (_shortLayout ? 14f : 15f) * inverse);
            SetTopLeft(change.rectTransform,
                textLeft * inverse, (quoteTop + pricePixels + (_shortLayout ? 26f : 32f)) * inverse,
                textWidth * inverse, changeHeight * inverse);

            float horizontal = 1f + padding + 5f;
            float bottom = bottomPadding + 2f;
            float hairline = inverse;
            float stepperButtonWidth = _narrowLayout ? 24f : 28f;
            float stepperValueWidth = _narrowLayout ? 24f : 28f;
            RectTransform stepper = _view.Button("QuantityDecrementButton").transform.parent as RectTransform;
            stepper.anchoredPosition = new Vector2(horizontal * inverse, bottom * inverse);
            stepper.sizeDelta = new Vector2(
                2f * hairline + (stepperButtonWidth * 2f + stepperValueWidth) * inverse,
                stepperHeight * inverse);
            RectTransform decrement = _view.Button("QuantityDecrementButton").GetComponent<RectTransform>();
            RectTransform increment = _view.Button("QuantityIncrementButton").GetComponent<RectTransform>();
            RectTransform quantity = _view.Text("QuantityText").rectTransform;
            SetTopLeft(decrement, hairline, hairline, stepperButtonWidth * inverse, stepperHeight * inverse - 2f * hairline);
            SetTopLeft(quantity, hairline + stepperButtonWidth * inverse, 0f,
                stepperValueWidth * inverse, stepperHeight * inverse);
            SetTopLeft(increment, hairline + (stepperButtonWidth + stepperValueWidth) * inverse, hairline,
                stepperButtonWidth * inverse, stepperHeight * inverse - 2f * hairline);
            Center(decrement.Find("Icon") as RectTransform, 14f * inverse, 14f * inverse);
            Center(increment.Find("Icon") as RectTransform, 14f * inverse, 14f * inverse);

            float tradeWidth = _narrowLayout ? 31f : 34f;
            float tradeGap = _narrowLayout ? 5f : 7f;
            SetBottomRight(_view.Button("BuyButton").GetComponent<RectTransform>(),
                (horizontal + tradeWidth + tradeGap) * inverse, bottom * inverse,
                tradeWidth * inverse, tradeHeight * inverse);
            SetBottomRight(_view.Button("BorrowButton").GetComponent<RectTransform>(),
                horizontal * inverse, bottom * inverse,
                tradeWidth * inverse, tradeHeight * inverse);
            Center(_view.Button("BuyButton").transform.Find("Icon") as RectTransform, 20f * inverse, 20f * inverse);
            Center(_view.Button("BorrowButton").transform.Find("Icon") as RectTransform, 20f * inverse, 20f * inverse);
            if (_buyButtonMotion != null)
                _buyButtonMotion.SetRestPosition(_view.Button("BuyButton").GetComponent<RectTransform>().anchoredPosition, inverse);
            if (_borrowButtonMotion != null)
                _borrowButtonMotion.SetRestPosition(_view.Button("BorrowButton").GetComponent<RectTransform>().anchoredPosition, inverse);

            float actionHorizontal = 1f + padding;
            float actionBottom = bottomPadding + 1f;
            _actionBand.offsetMin = new Vector2(actionHorizontal * inverse, actionBottom * inverse);
            _actionBand.offsetMax = new Vector2(-actionHorizontal * inverse, (actionBottom + actionHeight) * inverse);
            RectTransform actionTop = _actionBand.Find("ActionTopLine") as RectTransform;
            if (actionTop != null)
            {
                actionTop.anchorMin = new Vector2(0f, 1f);
                actionTop.anchorMax = new Vector2(1f, 1f);
                actionTop.offsetMin = new Vector2(0f, -1f * inverse);
                actionTop.offsetMax = Vector2.zero;
            }

            RectTransform chart = _view.Rect("ChartHost");
            chart.offsetMin = new Vector2(1f * inverse, ((_shortLayout ? 35f : 42f) + 1f) * inverse);
            chart.offsetMax = new Vector2(-1f * inverse, -1f * inverse);

            _shadowRestPosition = new Vector2(4f * inverse, -5f * inverse);
            if (!_hovered)
                _hardShadow.anchoredPosition = _shadowRestPosition;
            _goldLine.offsetMin = new Vector2(-1f * inverse, 18f * inverse);
            _goldLine.offsetMax = new Vector2(3f * inverse, -16f * inverse);
            UpdateNamePaper();
            UpdateChangePaper();
        }

        private void ConfigureTypography()
        {
            Text index = _view.Text("IndexText");
            index.font = _services.Assets.DisplayFont;
            index.fontSize = 91;
            index.fontStyle = FontStyle.Italic;
            Color ghostColor = UiFormatting.Ink;
            ghostColor.a = 0.045f;
            index.color = ghostColor;
            Text name = _view.Text("NameText");
            name.fontSize = 19;
            ConfigureSingleLine(name, 14, 19);
            _view.Text("CategoryText").fontSize = 8;
            Text price = _view.Text("PriceText");
            price.font = _services.Assets.BoldFont;
            price.fontSize = 37;
            price.fontStyle = FontStyle.Italic;
            price.horizontalOverflow = HorizontalWrapMode.Overflow;
            price.verticalOverflow = VerticalWrapMode.Overflow;
            price.resizeTextForBestFit = false;
            Text priceUnit = _view.Text("PriceUnitText");
            priceUnit.font = _services.Assets.RegularFont;
            priceUnit.fontSize = 10;
            priceUnit.color = UiFormatting.Muted;
            _view.Text("DeltaText").fontSize = 11;
            _view.Text("ChangeText").fontSize = 15;
            Text quantity = _view.Text("QuantityText");
            quantity.fontSize = 12;
            quantity.font = _services.Assets.BoldFont;
            quantity.fontStyle = FontStyle.Normal;
        }

        private void UpdateNamePaper()
        {
            Text name = _view.Text("NameText");
            float inverse = 1f / Mathf.Max(0.01f, _designScale);
            float horizontal = (1f + (_wideLayout ? 15f : _narrowLayout ? 9f : 12f)) * inverse;
            float top = (1f + (_wideLayout ? 14f : _narrowLayout ? 9f : 12f)) * inverse;
            float physicalWidth = Screen.width > 0 ? Screen.width : 1920f;
            float physicalHeight = Screen.height > 0 ? Screen.height : 1080f;
            float viewportWidth = MarketLayoutMetrics.EffectiveSize(
                physicalWidth, MarketLayoutMetrics.ContentScale(physicalWidth, physicalHeight));
            float namePixels = Mathf.Clamp(viewportWidth * 0.0102f, 14f, 19f);
            float height = Mathf.Round(namePixels + 10f) * inverse;
            float letterSpacing = name.text.Length * namePixels * 0.05f * inverse;
            float textWidth = Mathf.Clamp(Mathf.Ceil(name.preferredWidth + letterSpacing), 1f, 316f);
            RectTransform nameRect = name.rectTransform;
            SetTopLeft(nameRect, horizontal + 6f * inverse, top + 2f * inverse,
                textWidth, height - 4f * inverse);
            if (_nameBack != null)
            {
                SetTopLeft(
                    _nameBack,
                    horizontal,
                    top,
                    Mathf.Min(328f, textWidth + 12f * inverse),
                    height);
                if (_nameRedLine != null)
                {
                    _nameRedLine.offsetMin = Vector2.zero;
                    _nameRedLine.offsetMax = new Vector2(3f * inverse, 0f);
                }
            }
        }

        private void UpdateCategoryPaper()
        {
            Text category = _view.Text("CategoryText");
            RectTransform categoryContainer = _categorySkewIsBound
                ? _view.Rect("CategorySkew")
                : _categorySkew;
            if (categoryContainer == null)
                return;
            float inverse = 1f / Mathf.Max(0.01f, _designScale);
            float right = (1f + (_wideLayout ? 15f : _narrowLayout ? 9f : 12f)) * inverse;
            float top = (1f + (_wideLayout ? 14f : _narrowLayout ? 9f : 12f)) * inverse;
            float width = Mathf.Ceil(category.preferredWidth) + 12f * inverse;
            SetTopRight(categoryContainer, right, top, width, 16f * inverse);
        }

        private void UpdateChangePaper()
        {
            if (_changeBack == null)
                return;
            Text change = _view.Text("ChangeText");
            _changeBack.gameObject.SetActive(!string.IsNullOrEmpty(change.text));
            float inverse = 1f / Mathf.Max(0.01f, _designScale);
            float padding = _wideLayout ? 15f : _narrowLayout ? 9f : 12f;
            float width = Mathf.Ceil(change.preferredWidth + 12f * inverse);
            SetTopRight(
                _changeBack,
                (1f + padding + 7f) * inverse,
                -change.rectTransform.anchoredPosition.y,
                width,
                change.rectTransform.rect.height);
        }

        private void BindPrice(WeeklyPoint current, Color trend)
        {
            string value = current == null ? "—" : UiFormatting.Money(current.Price);
            bool hasPrice = value != "—";
            Text price = _view.Text("PriceText");
            price.font = hasPrice ? _services.Assets.BoldFont : _services.Assets.RegularFont;
            price.fontStyle = hasPrice ? FontStyle.Italic : FontStyle.Normal;
            price.text = value;
            price.color = trend;
            FitPrice(price);

            Text unit = _view.Text("PriceUnitText");
            unit.text = hasPrice ? "文" : string.Empty;
            unit.color = UiFormatting.Muted;
            unit.gameObject.SetActive(hasPrice);
        }

        private void FitPrice(Text price)
        {
            const int minimum = 1;
            int maximum = Math.Max(minimum, _priceMaximum);
            float availableWidth = Mathf.Max(1f, price.rectTransform.rect.width - 2f);
            price.fontSize = maximum;
            if (price.preferredWidth <= availableWidth)
                return;
            int size = Mathf.Clamp(
                Mathf.FloorToInt(maximum * availableWidth / price.preferredWidth),
                minimum,
                maximum);
            price.fontSize = size;
            while (size > minimum && price.preferredWidth > availableWidth)
            {
                size--;
                price.fontSize = size;
            }
        }

        private static void ConfigureSingleLine(Text text, int minimum, int maximum)
        {
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = maximum;
        }

        private void RegisterSkewEffects()
        {
            Graphic[] graphics = _visualContent.GetComponentsInChildren<Graphic>(true);
            List<CardSkewEffect> dynamicEffects = new List<CardSkewEffect>(graphics.Length);
            float categorySkew = Mathf.Tan(7f * Mathf.Deg2Rad);
            for (int i = 0; i < graphics.Length; i++)
            {
                CardSkewEffect effect = graphics[i].GetComponent<CardSkewEffect>();
                if (effect == null)
                    effect = graphics[i].gameObject.AddComponent<CardSkewEffect>();
                bool fixedCategory = _categorySkew != null && graphics[i].transform.parent == _categorySkew &&
                    (graphics[i].name == "CategoryBack" || graphics[i].name == "CategoryText");
                if (fixedCategory)
                    effect.SetSkew(categorySkew);
                else
                    dynamicEffects.Add(effect);
            }
            _skewEffects = dynamicEffects.ToArray();
        }

        private void SetInteraction(bool active, bool force)
        {
            if (_disposed || !_view.IsAlive)
                return;
            active = active && _view.Button("OpenButton").interactable;
            if (!force && active == _hovered)
                return;
            _hovered = active;
            AnimateHover(active);
        }

        private void AnimateHover(bool hover)
        {
            KillMotion();
            if (!_view.IsAlive)
                return;
            Vector2 pivotFrom = _motionPivot.anchoredPosition;
            Vector2 shadowFrom = _hardShadow.anchoredPosition;
            float skewFrom = _skew;
            float chartFrom = _chart.Chart.color.a;
            Vector2 itemFrom = _itemVisualRect.anchoredPosition;
            Vector3 itemScaleFrom = _itemVisualRect.localScale;
            Quaternion itemRotationFrom = _itemVisualRect.localRotation;
            float goldFrom = _goldLine.localScale.y;
            float chartTarget = hover ? 0.72f : _chartRestColor.a;

            if (_services.Motion.ReducedMotion)
            {
                ApplyMainFrame(1f, hover, pivotFrom, shadowFrom, skewFrom, chartFrom, chartTarget);
                ApplyItemFrame(1f, hover, itemFrom, itemScaleFrom, itemRotationFrom);
                SetGoldLine(hover ? 1f : 0f);
                return;
            }

            try
            {
                Sequence sequence = DOTween.Sequence();
                if (sequence == null)
                {
                    ApplyMainFrame(1f, hover, pivotFrom, shadowFrom, skewFrom, chartFrom, chartTarget);
                    ApplyItemFrame(1f, hover, itemFrom, itemScaleFrom, itemRotationFrom);
                    SetGoldLine(hover ? 1f : 0f);
                    return;
                }
                _motionSequence = sequence;
                sequence.SetUpdate(true).SetId(_motionId);
                sequence.Insert(0f, DOVirtual.Float(0f, 1f, 0.15f, delegate(float value)
                {
                    ApplyMainFrame(value, hover, pivotFrom, shadowFrom, skewFrom, chartFrom, chartTarget);
                }).SetEase(PaperOut));
                sequence.Insert(0f, DOVirtual.Float(0f, 1f, 0.17f, delegate(float value)
                {
                    ApplyItemFrame(value, hover, itemFrom, itemScaleFrom, itemRotationFrom);
                }).SetEase(PaperOut));
                sequence.Insert(0f, DOVirtual.Float(goldFrom, hover ? 1f : 0f, 0.18f, SetGoldLine).SetEase(PaperOut));
                sequence.OnComplete(delegate
                {
                    if (_motionSequence == sequence)
                        _motionSequence = null;
                });
            }
            catch (Exception)
            {
                KillMotion();
                ApplyMainFrame(1f, hover, pivotFrom, shadowFrom, skewFrom, chartFrom, chartTarget);
                ApplyItemFrame(1f, hover, itemFrom, itemScaleFrom, itemRotationFrom);
                SetGoldLine(hover ? 1f : 0f);
            }
        }

        private void ApplyMainFrame(
            float value,
            bool hover,
            Vector2 pivotFrom,
            Vector2 shadowFrom,
            float skewFrom,
            float chartFrom,
            float chartTarget)
        {
            if (!_view.IsAlive)
                return;
            float inverse = 1f / Mathf.Max(0.01f, _designScale);
            _motionPivot.anchoredPosition = Vector2.LerpUnclamped(
                pivotFrom,
                hover ? new Vector2(-3f, 5f) : Vector2.zero,
                value);
            _hardShadow.anchoredPosition = Vector2.LerpUnclamped(
                shadowFrom,
                hover ? new Vector2(8f * inverse, -10f * inverse) : _shadowRestPosition,
                value);
            _skew = Mathf.LerpUnclamped(skewFrom, hover ? Mathf.Tan(-0.3f * Mathf.Deg2Rad) : 0f, value);
            ApplySkew(_skew);
            Color chartColor = _chartRestColor;
            chartColor.a = Mathf.LerpUnclamped(chartFrom, chartTarget, value);
            _chart.Chart.color = chartColor;
        }

        private void ApplyItemFrame(
            float value,
            bool hover,
            Vector2 positionFrom,
            Vector3 scaleFrom,
            Quaternion rotationFrom)
        {
            if (!_view.IsAlive)
                return;
            _itemVisualRect.anchoredPosition = Vector2.LerpUnclamped(
                positionFrom,
                hover ? new Vector2(0f, 5f / Mathf.Max(0.01f, _designScale)) : Vector2.zero,
                value);
            _itemVisualRect.localScale = Vector3.LerpUnclamped(
                scaleFrom,
                hover ? _itemRestScale * 1.045f : _itemRestScale,
                value);
            _itemVisualRect.localRotation = Quaternion.SlerpUnclamped(
                rotationFrom,
                hover ? _itemRestRotation * Quaternion.Euler(0f, 0f, 1f) : _itemRestRotation,
                value);
        }

        private void ApplySkew(float value)
        {
            if (_skewEffects == null)
                return;
            for (int i = 0; i < _skewEffects.Length; i++)
            {
                if (_skewEffects[i] != null)
                    _skewEffects[i].SetSkew(value);
            }
        }

        private void SetGoldLine(float value)
        {
            if (_goldLine != null)
                _goldLine.localScale = new Vector3(1f, value, 1f);
        }

        private void ResetMotion()
        {
            _hovered = false;
            KillMotion();
            if (_buyButtonMotion != null)
                _buyButtonMotion.ResetToRest();
            if (_borrowButtonMotion != null)
                _borrowButtonMotion.ResetToRest();
            if (!_view.IsAlive)
                return;
            _motionPivot.anchoredPosition = Vector2.zero;
            _hardShadow.anchoredPosition = _shadowRestPosition;
            _itemRect.anchoredPosition = _itemRestPosition;
            _itemVisualRect.anchoredPosition = Vector2.zero;
            _itemVisualRect.localScale = _itemRestScale;
            _itemVisualRect.localRotation = _itemRestRotation;
            _skew = 0f;
            ApplySkew(0f);
            SetGoldLine(0f);
            if (_chart.Chart != null)
                _chart.Chart.color = _chartRestColor;
        }

        private void KillMotion()
        {
            SafeTween.Kill(_motionSequence);
            _motionSequence = null;
            SafeTween.Kill(_motionId);
        }

        private static float PaperOut(float time, float duration, float overshootOrAmplitude, float period)
        {
            if (duration <= 0f)
                return 1f;
            float target = Mathf.Clamp01(time / duration);
            float parameter = target;
            for (int i = 0; i < 6; i++)
            {
                float inverse = 1f - parameter;
                float x = 3f * inverse * inverse * parameter * 0.16f +
                    3f * inverse * parameter * parameter * 0.3f +
                    parameter * parameter * parameter;
                float derivative = 3f * inverse * inverse * 0.16f +
                    6f * inverse * parameter * 0.14f +
                    3f * parameter * parameter * 0.7f;
                if (Mathf.Abs(derivative) < 0.000001f)
                    break;
                float next = Mathf.Clamp01(parameter - (x - target) / derivative);
                if (Mathf.Abs(next - parameter) < 0.000001f)
                {
                    parameter = next;
                    break;
                }
                parameter = next;
            }
            return 1f - Mathf.Pow(1f - parameter, 3f);
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

        private static void SetBottomRight(RectTransform rect, float right, float bottom, float width, float height)
        {
            if (rect == null)
                return;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-right - width * 0.5f, bottom + height * 0.5f);
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

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void ConfigureGoldLine(RectTransform rect)
        {
            Image image = rect.GetComponent<Image>();
            if (image == null)
                image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.7059f, 0.5765f, 0.3451f, 1f);
            image.raycastTarget = false;
            UiPaperShape.Attach(image);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(-1f, 18f);
            rect.offsetMax = new Vector2(3f, -16f);
            rect.localScale = new Vector3(1f, 0f, 1f);
        }

        private bool IsNestedControlHit(GameObject hit)
        {
            if (hit == null)
                return false;
            Transform hitTransform = hit.transform;
            return IsControlHit(hitTransform, "QuantityDecrementButton") ||
                IsControlHit(hitTransform, "QuantityIncrementButton") ||
                IsControlHit(hitTransform, "BuyButton") ||
                IsControlHit(hitTransform, "BorrowButton");
        }

        private bool IsControlHit(Transform hit, string buttonName)
        {
            Transform control = _view.Button(buttonName).transform;
            return hit == control || hit.IsChildOf(control);
        }

        private void OpenDetail()
        {
            if (!string.IsNullOrEmpty(_itemId) && DetailRequested != null)
                DetailRequested(_itemId);
        }

        private void AddBuy() { Add(TradeAction.Buy); }
        private void AddBorrow() { Add(TradeAction.Borrow); }

        private void Add(TradeAction action)
        {
            if (_delisted || string.IsNullOrEmpty(_itemId) || AddRequested == null)
                return;
            Button source = action == TradeAction.Buy
                ? _view.Button("BuyButton")
                : _view.Button("BorrowButton");
            AddRequested(_itemId, action, _quantity, source.GetComponent<RectTransform>());
        }

        private void Decrease() { SetQuantity(_quantity - 1); }
        private void Increment() { if (_quantity < int.MaxValue) SetQuantity(_quantity + 1); }

        private void SetQuantity(int value)
        {
            if (_delisted)
                return;
            _quantity = Math.Max(1, value);
            UpdateQuantity();
        }

        private void UpdateTradeButton(Button button, string family, bool allowed)
        {
            string prefix = "button_trade_" + family + "_";
            Sprite normal = _services.Assets.GetSprite(prefix + "normal");
            SpriteState state = button.spriteState;
            state.disabledSprite = _services.Assets.GetSprite(prefix + "disabled");
            button.spriteState = state;
            button.interactable = allowed;
            Image image = button.targetGraphic as Image;
            if (image == null)
                image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.white;
                image.sprite = allowed ? normal : state.disabledSprite;
            }
        }

        internal void UpdateQuantity()
        {
            MarketDraft draft = _delisted ? default(MarketDraft) : _cart.Draft(_itemId, ref _quantity, false);
            UpdateTradeButton(_view.Button("BuyButton"), "red", draft.Buy);
            UpdateTradeButton(_view.Button("BorrowButton"), "green", draft.Borrow);
            _view.Text("QuantityText").text = _quantity.ToString();
            Button decrement = _view.Button("QuantityDecrementButton");
            Button increment = _view.Button("QuantityIncrementButton");
            decrement.interactable = !_delisted && _quantity > 1;
            increment.interactable = draft.CanIncrement;
            SetStepperIconState(decrement);
            SetStepperIconState(increment);
        }

        private static void SetStepperIconState(Button button)
        {
            MarketIconInteractionState.Refresh(button);
        }

        private void OnReducedMotionChanged(bool reduced)
        {
            if (!reduced || _disposed)
                return;
            SetInteraction(_hovered, true);
            if (_buyButtonMotion != null)
                _buyButtonMotion.Settle();
            if (_borrowButtonMotion != null)
                _borrowButtonMotion.Settle();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _services.Motion.ReducedMotionChanged -= OnReducedMotionChanged;
            if (_motionDriver != null)
            {
                _motionDriver.Clear();
                UnityEngine.Object.Destroy(_motionDriver);
                _motionDriver = null;
            }
            KillMotion();
            if (_buyButtonMotion != null)
                _buyButtonMotion.ResetToRest();
            if (_borrowButtonMotion != null)
                _borrowButtonMotion.ResetToRest();
            if (_view.IsAlive)
            {
                _view.Button("BuyButton").onClick.RemoveListener(AddBuy);
                _view.Button("BorrowButton").onClick.RemoveListener(AddBorrow);
                _view.Button("QuantityDecrementButton").onClick.RemoveListener(Decrease);
                _view.Button("QuantityIncrementButton").onClick.RemoveListener(Increment);
            }
            _chart.Dispose();
            _skewEffects = null;
            DetailRequested = null;
            AddRequested = null;
            _itemId = null;
            _tooltipItem = null;
        }

        private sealed class GoodsCardMotionDriver : MonoBehaviour,
            IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
        {
            private MarketItemCard _owner;
            private bool _pointerInside;

            internal void Bind(MarketItemCard owner)
            {
                _owner = owner;
            }

            internal void Clear()
            {
                _owner = null;
                _pointerInside = false;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _pointerInside = true;
                SyncInteraction();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _pointerInside = false;
                SyncInteraction();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (_owner == null || eventData == null ||
                    eventData.button != PointerEventData.InputButton.Left ||
                    _owner.IsNestedControlHit(eventData.pointerCurrentRaycast.gameObject))
                    return;
                _owner.OpenDetail();
            }

            public void OnSubmit(BaseEventData eventData)
            {
                if (_owner != null)
                    _owner.OpenDetail();
            }

            private void Update()
            {
                SyncInteraction();
            }

            private void OnDisable()
            {
                _pointerInside = false;
                if (_owner != null)
                    _owner.ResetMotion();
            }

            private void OnDestroy()
            {
                Clear();
            }

            private void SyncInteraction()
            {
                if (_owner == null || !_owner._view.IsAlive)
                    return;
                _owner.ApplyDesignContentScale();
                bool focused = false;
                EventSystem eventSystem = EventSystem.current;
                GameObject selected = eventSystem == null ? null : eventSystem.currentSelectedGameObject;
                if (selected != null && UiFocusOrigin.KeyboardActive)
                {
                    Transform transform = selected.transform;
                    focused = transform == _owner._view.Root || transform.IsChildOf(_owner._view.Root);
                }
                _owner.SetInteraction(_pointerInside || focused, false);
            }
        }

        private sealed class QuotePaperEffect : BaseMeshEffect
        {
            public override void ModifyMesh(VertexHelper vertexHelper)
            {
                if (vertexHelper == null || graphic == null)
                    return;
                Rect rect = graphic.rectTransform.rect;
                if (rect.width <= 0f || rect.height <= 0f)
                    return;
                UIVertex vertex = default(UIVertex);
                for (int i = 0; i < vertexHelper.currentVertCount; i++)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    Vector3 position = vertex.position;
                    float x = Mathf.Clamp01((position.x - rect.xMin) / rect.width);
                    float y = Mathf.Clamp01((position.y - rect.yMin) / rect.height);
                    float bottomX = rect.xMin + rect.width * 0.96f * x;
                    float bottomY = rect.yMin + rect.height * 0.07f * (1f - x);
                    float topX = rect.xMin + rect.width * (0.08f + 0.92f * x);
                    float topY = rect.yMin + rect.height * (1f - 0.04f * x);
                    position.x = Mathf.LerpUnclamped(bottomX, topX, y);
                    position.y = Mathf.LerpUnclamped(bottomY, topY, y);
                    vertex.position = position;
                    vertexHelper.SetUIVertex(vertex, i);
                }
            }
        }

        private sealed class CardSkewEffect : BaseMeshEffect
        {
            private float _skew;

            internal void SetSkew(float value)
            {
                if (Mathf.Abs(_skew - value) < 0.000001f)
                    return;
                _skew = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }

            public override void ModifyMesh(VertexHelper vertexHelper)
            {
                if (vertexHelper == null || Mathf.Abs(_skew) < 0.000001f || graphic == null)
                    return;
                float centerY = graphic.rectTransform.rect.center.y;
                UIVertex vertex = default(UIVertex);
                for (int i = 0; i < vertexHelper.currentVertCount; i++)
                {
                    vertexHelper.PopulateUIVertex(ref vertex, i);
                    Vector3 position = vertex.position;
                    position.x += (position.y - centerY) * _skew;
                    vertex.position = position;
                    vertexHelper.SetUIVertex(vertex, i);
                }
            }
        }
    }
}
