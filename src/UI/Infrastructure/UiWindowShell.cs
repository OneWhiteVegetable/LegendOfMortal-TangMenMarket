using UnityEngine;
using UnityEngine.UI;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Infrastructure
{
    internal sealed class UiWindowShell : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _window;
        private RectTransform _motionPivot;
        private RectTransform _paper;
        private CanvasGroup _motionGroup;
        private CanvasGroup _windowGroup;
        private Vector2 _lastRootSize;
        private UiWindowLayoutProfile _layout;
        private bool _configured;

        internal RectTransform MotionPivot { get { return _motionPivot; } }
        internal RectTransform MotionTarget { get { return _window; } }
        internal CanvasGroup MotionGroup { get { return _motionGroup; } }
        internal CanvasGroup WindowGroup { get { return _windowGroup; } }

        internal void Configure(RectTransform root, RectTransform window)
        {
            _root = root;
            _window = window;
            _motionPivot = Find(root, "WindowMotion");
            if (_motionPivot == null)
            {
                GameObject pivotObject = new GameObject("WindowMotion", typeof(RectTransform), typeof(CanvasGroup));
                _motionPivot = pivotObject.GetComponent<RectTransform>();
                _motionPivot.SetParent(root, false);
            }
            _motionPivot.SetSiblingIndex(window.GetSiblingIndex() + 1);
            Stretch(_motionPivot, 0f, 0f, 0f, 0f);

            _motionGroup = _motionPivot.GetComponent<CanvasGroup>();
            if (_motionGroup == null)
                _motionGroup = _motionPivot.gameObject.AddComponent<CanvasGroup>();
            _motionGroup.interactable = false;
            _motionGroup.blocksRaycasts = false;
            _motionGroup.ignoreParentGroups = false;
            _windowGroup = _window.GetComponent<CanvasGroup>();
            if (_windowGroup == null)
                _windowGroup = _window.gameObject.AddComponent<CanvasGroup>();
            _windowGroup.interactable = false;
            _windowGroup.blocksRaycasts = false;
            _windowGroup.ignoreParentGroups = false;

            _paper = Find(_motionPivot, "PaperGrain");
            if (_paper == null)
                _paper = Find(_window, "PaperGrain");
            if (_paper != null)
            {
                if (_paper.parent != _motionPivot)
                    _paper.SetParent(_motionPivot, false);
                _paper.SetAsLastSibling();
            }

            RectTransform header = Find(_window, "Masthead");
            Image brand = Find(header, "BrandBlock").GetComponent<Image>();
            brand.color = UiFormatting.Paper;
            UiPaperShape.Attach(brand, 24f).StraightLeft();
            UiPaperShape.Attach(Find(brand.transform, "BrandSealBack").GetComponent<Image>());
            RectTransform sealFace = Find(brand.transform, "BrandSealBack/SealFace");
            if (sealFace != null) UiPaperShape.Attach(sealFace.GetComponent<Image>());
            UiPaperShape.Attach(Find(brand.transform, "BrandRed").GetComponent<Image>());
            UiPaperShape.Attach(Find(header, "MastheadRedStroke").GetComponent<Image>(), 2.5f).StraightLeft();
            UiLedgerWatermark.Create(_window);
            _configured = true;
            ApplyNow();
        }

        internal void ApplyNow()
        {
            if (!_configured || _root == null || _window == null)
                return;

            Vector2 size = Screen.width > 0 && Screen.height > 0
                ? new Vector2(Screen.width, Screen.height)
                : _root.rect.size;
            _lastRootSize = size;

            float scale = UiCanvasDensity.Apply(_root);
            UiWindowLayoutProfile layout = UiWindowLayoutProfile.For(size.x / scale, size.y / scale);
            _layout = layout;

            Stretch(_motionPivot, 0f, 0f, 0f, 0f);
            Stretch(_window, layout.WindowInset, layout.WindowInset, layout.WindowInset, layout.WindowInset);
            if (_paper != null)
            {
                Stretch(_paper, layout.WindowInset, layout.WindowInset, layout.WindowInset, layout.WindowInset);
                _paper.SetAsLastSibling();
                RectTransform grain = Find(_paper, "Grain");
                if (grain != null)
                    Stretch(grain, 0f, 0f, 0f, 0f);
            }

            RectTransform header = Find(_window, "Masthead");
            if (header != null)
            {
                Top(header, 0f, layout.MastheadHeight);
                header.SetAsLastSibling();
                ApplyBrand(header, layout);
                ApplyIndex(header, layout);
                ApplyMoney(header, layout);
                ApplyNav(header, layout);
            }

            RectTransform status = Find(_window, "Statusbar");
            if (status != null)
                ApplyStatus(status, layout);

            RectTransform ticker = Find(_window, "NewsTicker");
            if (ticker != null)
                ApplyTicker(ticker, layout);

            RectTransform page = Find(_window, "PageHost");
            if (page == null)
                page = Find(_window, "DetailPage");
            if (page != null)
                Stretch(page, 0f, layout.MastheadHeight +
                    (ticker != null && ticker.gameObject.activeSelf ? UiWindowLayoutProfile.TickerHeight : 0f),
                    0f, layout.StatusHeight);

            RectTransform profile = Find(_window, "ProfilePage");
            if (profile != null)
                Stretch(profile, 26f, layout.MastheadHeight + 18f, 26f, layout.StatusHeight + 12f);
        }

        private void OnEnable()
        {
            ApplyNow();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyNow();
        }

        private void Update()
        {
            if (!_configured || _root == null)
                return;
            Vector2 size = Screen.width > 0 && Screen.height > 0
                ? new Vector2(Screen.width, Screen.height)
                : _root.rect.size;
            if (Mathf.Abs(size.x - _lastRootSize.x) > 0.01f ||
                Mathf.Abs(size.y - _lastRootSize.y) > 0.01f)
                ApplyNow();
        }

        private static void ApplyBrand(RectTransform header, UiWindowLayoutProfile layout)
        {
            float brandWidth = layout.BrandWidth;
            float masthead = layout.MastheadHeight;
            bool compact = layout.Compact;
            RectTransform brand = Find(header, "BrandBlock");
            if (brand == null)
                return;
            Left(brand, 0f, brandWidth);
            brand.offsetMin = new Vector2(brand.offsetMin.x, 5f);

            float sealWidth = layout.BrandSealWidth;
            float sealHeight = layout.BrandSealHeight;
            float sealX = compact ? 17f : 22f;
            RectTransform seal = Find(brand, "BrandSealBack");
            if (seal != null)
            {
                PlaceAtCenter(seal, sealX, (masthead - 5f - sealHeight) * 0.5f, sealWidth, sealHeight);
                seal.localRotation = Quaternion.Euler(0f, 0f, 5f);
            }

            float titleX = sealX + sealWidth + (compact ? 9f : 13f);
            RectTransform tangmen = Find(brand, "BrandTangmen");
            if (tangmen != null)
            {
                Place(tangmen, titleX, compact ? 5f : 9f, compact ? 64f : 68f, compact ? 34f : 38f);
                tangmen.localRotation = Quaternion.Euler(0f, 0f, 2f);
                SetSingleLineText(tangmen, compact ? 23 : 29);
            }
            RectTransform red = Find(brand, "BrandRed");
            if (red != null)
            {
                PlaceAtCenter(red, titleX + (compact ? 58f : 60f), compact ? 10f : 12f, compact ? 60f : 68f, compact ? 32f : 38f);
                red.localRotation = Quaternion.Euler(0f, 0f, 4f);
            }
            RectTransform market = Find(brand, "BrandRed/BrandMarket");
            if (market != null)
            {
                Stretch(market, 0f, 0f, 0f, 0f);
                SetSingleLineText(market, compact ? 22 : 29);
            }
            RectTransform legacyTitle = Find(brand, "BrandTitle");
            if (legacyTitle != null)
            {
                Place(legacyTitle, titleX, compact ? 6f : 10f, brandWidth - titleX - 22f, compact ? 34f : 38f);
                SetFontSize(legacyTitle, compact ? 23 : 29);
            }
            RectTransform subtitle = Find(brand, "BrandSubtitle");
            if (subtitle != null)
            {
                Place(subtitle, titleX, compact ? 45f : 53f, compact ? 150f : 160f, compact ? 12f : 14f);
                SetFontSize(subtitle, compact ? 7 : 8);
            }
        }

        private static void ApplyIndex(RectTransform header, UiWindowLayoutProfile layout)
        {
            float brandWidth = layout.BrandWidth;
            float indexWidth = layout.IndexWidth;
            float masthead = layout.MastheadHeight;
            bool compact = layout.Compact;
            RectTransform index = Find(header, "IndexButton");
            if (index == null)
                return;
            Place(index, brandWidth, 0f, indexWidth, masthead);

            RectTransform name = Find(index, "IndexNameText");
            if (name != null)
            {
                Place(name, compact ? 14f : 18f, compact ? 7f : 8f, compact ? 96f : 108f, compact ? 19f : 22f);
                SetFontSize(name, compact ? 12 : 13);
            }
            RectTransform price = Find(index, "IndexPriceText");
            if (price != null)
            {
                Place(price, compact ? 14f : 18f, compact ? 25f : 29f, compact ? 96f : 112f, compact ? 32f : 35f);
                SetFontSize(price, compact ? 19 : 23);
            }

            float chartWidth = layout.IndexChartWidth;
            float chartHeight = layout.IndexChartHeight;
            float chartX = indexWidth - chartWidth - (compact ? 12f : 16f);
            float changeX = compact ? 112f : 124f;
            RectTransform change = Find(index, "IndexChangeText");
            if (change != null)
            {
                Place(change, changeX, compact ? 22f : 26f, chartX - changeX - 6f, compact ? 26f : 30f);
                SetFontSize(change, compact ? 11 : 12);
            }
            RectTransform chart = Find(index, "IndexChartHost");
            if (chart != null)
                Place(chart, chartX, (masthead - chartHeight) * 0.5f, chartWidth, chartHeight);
        }

        internal void RefreshMoneyLayout()
        {
            RectTransform header = Find(_window, "Masthead");
            if (header != null && _layout != null)
                ApplyMoney(header, _layout);
        }

        private static void ApplyMoney(RectTransform header, UiWindowLayoutProfile layout)
        {
            float masthead = layout.MastheadHeight;
            float navWidth = layout.NavWidth;
            float moneyWidth = layout.MoneyWidth;
            bool compact = layout.Compact;
            RectTransform money = Find(header, "MoneyBlock");
            if (money != null)
            {
                float iconSize = compact ? 32f : 38f;
                float iconX = compact ? 10f : 20f;
                float textX = compact ? 50f : 68f;
                Text amount = Find(money, "MoneyText").GetComponent<Text>();
                amount.fontSize = compact ? 18 : 22;
                // Expand to the left into the spacer; the navigation stays anchored at the right.
                float room = Mathf.Max(moneyWidth, header.rect.width - layout.BrandWidth - layout.IndexWidth - navWidth - 8f);
                moneyWidth = Mathf.Clamp(Mathf.Ceil(amount.preferredWidth) + textX + 9f, moneyWidth, room);
                Right(money, navWidth, moneyWidth);
                RectTransform icon = Find(money, "MoneyCoin");
                if (icon != null)
                    Place(icon, iconX, (masthead - iconSize) * 0.5f, iconSize, iconSize);
                RectTransform caption = Find(money, "MoneyCaption");
                if (caption != null)
                {
                    Place(caption, textX, compact ? 7f : 11f, moneyWidth - textX - 8f, compact ? 18f : 20f);
                    SetFontSize(caption, compact ? 10 : 11);
                }
                Place(amount.rectTransform, textX, compact ? 25f : 30f,
                    moneyWidth - textX - 8f, compact ? 34f : 37f);
            }
        }

        private static void ApplyNav(RectTransform header, UiWindowLayoutProfile layout)
        {
            float navWidth = layout.NavWidth;
            float masthead = layout.MastheadHeight;
            bool compact = layout.Compact;
            RectTransform nav = Find(header, "HeaderNav");
            if (nav == null)
                return;
            Right(nav, 0f, navWidth);
            string[] names = { "RankingButton", "InventoryButton", "CloseButton" };
            float buttonWidth = navWidth / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform button = Find(nav, names[i]);
                if (button == null)
                    continue;
                Place(button, i * buttonWidth, 0f, buttonWidth, masthead - 5f);
                RectTransform icon = Find(button, "Icon");
                if (icon != null)
                    Place(icon, (buttonWidth - 28f) * 0.5f, compact ? 6f : 10f, 28f, 28f);
                RectTransform label = Find(button, "Label");
                if (label != null)
                {
                    Place(label, 3f, compact ? 36f : 44f, buttonWidth - 6f, compact ? 24f : 26f);
                    SetFontSize(label, compact ? 11 : 12);
                }
            }
        }

        private static void ApplyStatus(RectTransform status, UiWindowLayoutProfile layout)
        {
            float height = layout.StatusHeight;
            bool compact = layout.Compact;
            Bottom(status, 0f, height);
            RectTransform brand = Find(status, "Brand");
            if (brand != null)
            {
                Place(brand, 18f, 0f, compact ? 180f : 210f, height);
                SetFontSize(brand, compact ? 9 : 10);
            }
            RectTransform legend = Find(status, "Legend");
            if (legend != null)
            {
                Place(legend, compact ? 210f : 250f, 0f, compact ? 280f : 360f, height);
                SetFontSize(legend, compact ? 9 : 10);
            }
            RectTransform hint = Find(status, "StatusText");
            if (hint != null)
            {
                Stretch(hint, compact ? 490f : 610f, 0f, compact ? 174f : 220f, 0f);
                SetFontSize(hint, compact ? 9 : 10);
            }
            RectTransform motion = Find(status, "MotionButton");
            if (motion != null)
            {
                Right(motion, 12f, compact ? 150f : 176f);
                RectTransform icon = Find(motion, "Icon");
                if (icon != null)
                    Place(icon, 8f, (height - 16f) * 0.5f, 16f, 16f);
                RectTransform label = Find(motion, "Label");
                if (label != null)
                {
                    Place(label, 30f, 0f, compact ? 112f : 136f, height);
                    SetFontSize(label, compact ? 9 : 10);
                }
            }
        }

        private static void ApplyTicker(RectTransform ticker, UiWindowLayoutProfile layout)
        {
            float masthead = layout.MastheadHeight;
            Top(ticker, masthead, UiWindowLayoutProfile.TickerHeight);
            LayoutElement layoutElement = ticker.GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.preferredHeight = UiWindowLayoutProfile.TickerHeight;
            const float labelWidth = 120.28f;
            RectTransform label = Find(ticker, "LabelBackground");
            if (label != null)
            {
                Place(label, 0f, 0f, labelWidth, UiWindowLayoutProfile.TickerHeight);
                Image paper = label.GetComponent<Image>();
                paper.color = UiFormatting.Red;
                UiPaperShape.Attach(paper, 14f).StraightLeft();
                RectTransform icon = Find(label, "InfoIcon");
                if (icon != null)
                    Place(icon, 19f, 5f, 24f, 24f);
                RectTransform text = Find(label, "LabelText");
                if (text != null)
                {
                    Place(text, 50f, 0f, labelWidth - 54f, UiWindowLayoutProfile.TickerHeight);
                    SetFontSize(text, 11);
                }
            }
            RectTransform viewport = Find(ticker, "Viewport");
            if (viewport != null)
                Stretch(viewport, labelWidth, 0f, 0f, 0f);
        }

        private static RectTransform Find(Transform parent, string path)
        {
            if (parent == null)
                return null;
            Transform child = parent.Find(path);
            return child as RectTransform;
        }

        private static void SetFontSize(Component target, int size)
        {
            Text text = target == null ? null : target.GetComponent<Text>();
            if (text != null)
                text.fontSize = size;
        }

        private static void SetSingleLineText(Component target, int size)
        {
            Text text = target == null ? null : target.GetComponent<Text>();
            if (text == null)
                return;
            text.fontSize = size;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
        }

        private static void PlaceAtCenter(RectTransform rect, float x, float y, float width, float height)
        {
            Place(rect, x, y, width, height);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x + width * 0.5f, -y - height * 0.5f);
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Top(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -top - height);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private static void Bottom(RectTransform rect, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = new Vector2(0f, bottom + height);
        }

        private static void Left(RectTransform rect, float left, float width)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(left + width, 0f);
        }

        private static void Right(RectTransform rect, float right, float width)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-right - width, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
        }
    }
}
