using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class RankingChrome : IDisposable
    {
        private readonly RectTransform _header;
        private readonly RectTransform _tableHead;
        private readonly LayoutElement _headerElement;
        private readonly LayoutElement _tableHeadElement;
        private readonly LayoutElement _markElement;
        private readonly LayoutElement _titleLineElement;
        private readonly LayoutElement _tagElement;
        private readonly LayoutElement _titleElement;
        private readonly LayoutElement _periodElement;
        private readonly LayoutElement _backElement;
        private readonly LayoutElement _sortElement;
        private readonly LayoutElement[] _headColumns = new LayoutElement[6];
        private readonly Text[] _headLabels = new Text[6];
        private readonly Text _title;
        private readonly Text _period;
        private readonly Image _periodPaper;
        private readonly RectTransform _periodRail;
        private readonly Image _titleRule;
        private readonly Text _sortLabel;
        private readonly Button _backButton;
        private readonly Button _sortButton;
        private readonly Image _markIcon;
        private readonly HorizontalLayoutGroup _tableLayout;
        private readonly UnityAction _backAction;
        private readonly UnityAction _sortAction;
        private bool _disposed;

        internal RankingChrome(
            UiFrontendServices services,
            RectTransform content,
            bool compact,
            UnityAction sortAction,
            UnityAction backAction)
        {
            _sortAction = sortAction;
            _backAction = backAction;
            _header = RankingLayout.Rect("RankingHeader", content);
            _headerElement = RankingLayout.Layout(_header, -1f, 82f);
            HorizontalLayoutGroup headerLayout = _header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(4, 4, 0, 16);
            headerLayout.spacing = 20f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;

            Image mark = RankingLayout.Image("RankingTitleMark", _header, null, UiFormatting.Ink);
            _markElement = RankingLayout.Layout(mark, 54f, 48f);
            mark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            Shadow markShadow = mark.gameObject.AddComponent<Shadow>();
            markShadow.effectColor = UiFormatting.Red;
            markShadow.effectDistance = new Vector2(4f, -4f);
            UiPaperShape.Attach(mark);
            _markIcon = RankingLayout.Image(
                "Icon",
                mark.transform,
                services.Assets.GetSprite("icon_rank"),
                RankingLayout.GoldLight);
            _markIcon.color = RankingLayout.GoldLight;
            _markIcon.preserveAspect = true;
            RankingLayout.Center(_markIcon.rectTransform, 30f, 30f);

            RectTransform lockup = RankingLayout.Rect("RankingLockup", _header);
            RankingLayout.Layout(lockup, -1f, -1f, 1f);
            VerticalLayoutGroup lockupLayout = lockup.gameObject.AddComponent<VerticalLayoutGroup>();
            lockupLayout.childAlignment = TextAnchor.UpperLeft;
            lockupLayout.childControlWidth = true;
            lockupLayout.childControlHeight = true;
            lockupLayout.childForceExpandWidth = true;
            lockupLayout.childForceExpandHeight = false;
            lockupLayout.spacing = 0f;

            RectTransform titleLine = RankingLayout.Rect("TitleLine", lockup);
            _titleLineElement = RankingLayout.Layout(titleLine, -1f, 50f, 1f);
            HorizontalLayoutGroup titleLayout = titleLine.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleLayout.spacing = 11f;
            titleLayout.childAlignment = TextAnchor.MiddleLeft;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = false;

            Image tag = RankingLayout.Image("RankingTitleTag", titleLine, null, UiFormatting.Ink);
            _tagElement = RankingLayout.Layout(tag, 66f, 38f);
            tag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            UiPaperShape.Attach(tag, 5f);
            Text tagText = RankingLayout.Text(
                "Label",
                tag.transform,
                "货物",
                22,
                RankingLayout.PaperLight,
                services.Assets.BoldFont,
                TextAnchor.MiddleCenter);
            RankingLayout.Stretch(tagText.rectTransform, 4f, 2f, 4f, 2f);
            tagText.verticalOverflow = VerticalWrapMode.Overflow;
            MarketLetterSpacing.Attach(tagText, .08f, Mathf.Tan(8f * Mathf.Deg2Rad));

            _title = RankingLayout.Text(
                "RankingTitleText",
                titleLine,
                "涨跌榜",
                43,
                UiFormatting.Ink,
                services.Assets.BoldFont);
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;
            _title.verticalOverflow = VerticalWrapMode.Overflow;
            MarketLetterSpacing.Attach(_title, -.04f);
            _titleElement = RankingLayout.Layout(_title, 176f, 50f);
            _titleRule = RankingLayout.Image("TitleRule", _title.transform, null, UiFormatting.Red);
            _titleRule.rectTransform.anchorMin = _titleRule.rectTransform.anchorMax = Vector2.zero;
            _titleRule.rectTransform.pivot = Vector2.zero;
            _titleRule.rectTransform.anchoredPosition = new Vector2(2f, -4f);
            UiPaperShape.Attach(_titleRule, 2.65f);
            for (int i = 0; i < 3; i++)
            {
                float bottom = 1f + i * 3f;
                float height = 5f + i;
                AddCandlePart("Wick" + i, i * 9f + 3f, bottom - 2f, 2f, height + 5f);
                AddCandlePart("Body" + i, i * 9f, bottom, 8f, height);
            }

            AddCandlePart("Wick3", 30f, 9f, 2f, 31f);
            AddCandlePart("Body3", 27f, 12f, 8f, 23f);

            _periodRail = RankingLayout.Rect("RankingPeriod", content);
            _periodElement = RankingLayout.Layout(_periodRail, -1f, 10f, 1f);
            _periodPaper = RankingLayout.Image("CaptionPaper", _periodRail, null, RankingLayout.Paper);
            _periodPaper.rectTransform.anchorMin = _periodPaper.rectTransform.anchorMax =
                _periodPaper.rectTransform.pivot = new Vector2(0f, 1f);
            _periodPaper.rectTransform.anchoredPosition = new Vector2(5f, 5f);
            _period = RankingLayout.Text(
                "RankingPeriodText",
                _periodPaper.transform,
                MarketQuery.RankingPeriodCaption,
                11,
                UiFormatting.Muted,
                services.Assets.RegularFont);
            _period.horizontalOverflow = HorizontalWrapMode.Overflow;
            _period.resizeTextForBestFit = true;
            _period.resizeTextMinSize = 9;
            _period.resizeTextMaxSize = 11;
            _period.alignment = TextAnchor.MiddleLeft;
            RankingLayout.Stretch(_period.rectTransform, 8f, 0f, 8f, 0f);
            MarketLetterSpacing.Attach(_period, .08f);

            Image backImage = RankingLayout.Image(
                "RankingBackButton",
                _header,
                services.Assets.GetSprite("button_brush_arrow_normal"),
                RankingLayout.Paper,
                true);
            backImage.raycastTarget = true;
            _backButton = backImage.gameObject.AddComponent<Button>();
            UiFocusOrigin.Attach(_backButton);
            ConfigureButton(
                _backButton,
                backImage,
                services.Assets.GetSprite("button_brush_arrow_hover"),
                services.Assets.GetSprite("button_brush_arrow_pressed"));
            _backElement = RankingLayout.Layout(_backButton, 46f, 44f);
            Image backIcon = RankingLayout.Image(
                "Icon",
                _backButton.transform,
                services.Assets.GetSprite("icon_left"),
                UiFormatting.Ink);
            backIcon.color = UiFormatting.Ink;
            backIcon.preserveAspect = true;
            RankingLayout.Center(backIcon.rectTransform, 30f, 30f);
            MarketBrushArrow.Attach(_backButton, services.Motion);
            if (_backAction != null)
                _backButton.onClick.AddListener(_backAction);
            services.Overlays.AttachTooltip(
                _backButton.gameObject,
                "返回货价",
                "离开涨跌榜并恢复今日货价与契匣。",
                "Esc 也可返回");

            Image sortImage = RankingLayout.Image(
                "RankingSortButton",
                _header,
                services.Assets.GetSprite("button_sort_normal_9s"),
                UiFormatting.Ink,
                true);
            sortImage.raycastTarget = true;
            _sortButton = sortImage.gameObject.AddComponent<Button>();
            UiFocusOrigin.Attach(_sortButton);
            ConfigureButton(
                _sortButton,
                sortImage,
                services.Assets.GetSprite("button_sort_hover_9s"),
                services.Assets.GetSprite("button_sort_pressed_9s"));
            _sortElement = RankingLayout.Layout(_sortButton, 196f, 44f);
            Image sortIcon = RankingLayout.Image(
                "Icon",
                _sortButton.transform,
                services.Assets.GetSprite("icon_sort"),
                RankingLayout.PaperLight);
            sortIcon.color = RankingLayout.PaperLight;
            sortIcon.preserveAspect = true;
            sortIcon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            sortIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            sortIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            sortIcon.rectTransform.anchoredPosition = new Vector2(14f, 0f);
            sortIcon.rectTransform.sizeDelta = new Vector2(18f, 18f);
            _sortLabel = RankingLayout.Text(
                "Label",
                _sortButton.transform,
                "涨幅优先 · 降序",
                11,
                RankingLayout.PaperLight,
                services.Assets.BoldFont,
                TextAnchor.MiddleCenter);
            _sortLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            RankingLayout.Stretch(_sortLabel.rectTransform, 40f, 2f, 8f, 5f);
            if (_sortAction != null)
                _sortButton.onClick.AddListener(_sortAction);
            services.Overlays.AttachTooltip(
                _sortButton.gameObject,
                "累计涨跌排序",
                "点击切换涨幅降序与跌幅升序；同收益保持出现顺序，缺价商品始终排尾。",
                "全部已出现商品位于同一滚动榜单");

            Image headerRule = RankingLayout.Image("HeaderRule", _header, null, UiFormatting.Ink);
            headerRule.color = UiFormatting.Ink;
            RankingLayout.IgnoreLayout(headerRule);
            headerRule.rectTransform.anchorMin = new Vector2(0f, 0f);
            headerRule.rectTransform.anchorMax = new Vector2(1f, 0f);
            headerRule.rectTransform.pivot = new Vector2(0.5f, 0f);
            headerRule.rectTransform.offsetMin = new Vector2(4f, 0f);
            headerRule.rectTransform.offsetMax = new Vector2(-4f, 3f);

            _tableHead = RankingLayout.Rect("RankingTableHead", content);
            Image tableBackground = _tableHead.gameObject.AddComponent<Image>();
            tableBackground.color = RankingLayout.Paper;
            tableBackground.raycastTarget = false;
            _tableHeadElement = RankingLayout.Layout(_tableHead, -1f, 32f);
            _tableLayout = RankingLayout.Columns(_tableHead, compact);
            string[] labels = { "名次", "货物 / ITEM", "现价", "累计涨跌", "近20周留痕", string.Empty };
            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform column = RankingLayout.Rect("Column" + i.ToString(), _tableHead);
                _headColumns[i] = RankingLayout.Column(column, i, compact);
                Text label = RankingLayout.Text(
                    "Label",
                    column,
                    labels[i],
                    9,
                    UiFormatting.Muted,
                    services.Assets.BoldFont,
                    i == 0 ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                RankingLayout.Stretch(label.rectTransform);
                _headLabels[i] = label;
            }
            Image tableRule = RankingLayout.Image("TableRule", _tableHead, null, UiFormatting.Ink);
            tableRule.color = RankingLayout.Alpha(UiFormatting.Ink, 0.28f);
            RankingLayout.IgnoreLayout(tableRule);
            tableRule.rectTransform.anchorMin = new Vector2(0f, 0f);
            tableRule.rectTransform.anchorMax = new Vector2(1f, 0f);
            tableRule.rectTransform.pivot = new Vector2(0.5f, 0f);
            tableRule.rectTransform.offsetMin = new Vector2(4f, 0f);
            tableRule.rectTransform.offsetMax = new Vector2(-4f, 1f);
            Image tableAccent = RankingLayout.Image("TableAccent", _tableHead, null, RankingLayout.Gold);
            tableAccent.color = RankingLayout.Gold;
            RankingLayout.IgnoreLayout(tableAccent);
            tableAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
            tableAccent.rectTransform.anchorMax = new Vector2(0f, 0f);
            tableAccent.rectTransform.pivot = new Vector2(0f, 0f);
            tableAccent.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            tableAccent.rectTransform.sizeDelta = new Vector2(68f, 3f);

            SetCompact(compact);
            Hide();
        }

        private void AddCandlePart(string name, float left, float bottom, float width, float height)
        {
            Image image = RankingLayout.Image(name, _titleRule.transform, null, UiFormatting.Red);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(left - 2f, bottom);
            rect.sizeDelta = new Vector2(width, height);
            UiPaperShape.Attach(image);
        }

        internal void Show()
        {
            if (_disposed)
                return;
            _header.gameObject.SetActive(true);
            _tableHead.gameObject.SetActive(true);
            _periodRail.gameObject.SetActive(true);
        }

        internal void Hide()
        {
            if (_header != null)
                _header.gameObject.SetActive(false);
            if (_tableHead != null)
                _tableHead.gameObject.SetActive(false);
            if (_periodRail != null)
                _periodRail.gameObject.SetActive(false);
        }

        internal void SetAscending(bool ascending)
        {
            if (!_disposed && _sortLabel != null)
                _sortLabel.text = ascending ? "跌幅优先 · 升序" : "涨幅优先 · 降序";
        }

        internal void SetSortInteractable(bool interactable)
        {
            if (!_disposed && _sortButton != null)
                _sortButton.interactable = interactable;
        }

        internal void SetItemCount(int count)
        {
            if (!_disposed)
            {
                _period.text = Math.Max(0, count).ToString("00") +
                    " 件已出现 · " + MarketQuery.RankingPeriodCaption;
                _periodPaper.rectTransform.sizeDelta = new Vector2(
                    Mathf.Ceil(_period.preferredWidth + _period.text.Length * _period.fontSize * .08f) + 16f, 15f);
            }
        }

        internal void SetCompact(bool compact)
        {
            if (_disposed)
                return;
            _headerElement.preferredHeight = 66f;
            _tableHeadElement.preferredHeight = compact ? 28f : 32f;
            _markElement.preferredWidth = 54f;
            _markElement.preferredHeight = 48f;
            RankingLayout.Center(_markIcon.rectTransform, 30f, 30f);
            _titleLineElement.preferredHeight = 48f;
            _tagElement.preferredWidth = 65f;
            _tagElement.preferredHeight = 31f;
            _title.fontSize = 43;
            _titleElement.preferredWidth = compact ? 150f : 176f;
            _titleElement.preferredHeight = 45f;
            _titleRule.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(_title.preferredWidth - .08f * _title.fontSize) + 4f, 5f);
            _periodElement.preferredHeight = 10f;
            _period.resizeTextMaxSize = 10;
            _period.fontSize = 10;
            _backElement.preferredWidth = compact ? 40f : 46f;
            _backElement.preferredHeight = compact ? 38f : 44f;
            _sortElement.preferredWidth = compact ? 134f : 142f;
            _sortElement.preferredHeight = 35f;
            _sortLabel.fontSize = 11;
            RankingLayout.ConfigureColumns(_tableLayout, compact);
            for (int i = 0; i < _headColumns.Length; i++)
            {
                RankingLayout.ConfigureColumn(_headColumns[i], i, compact);
                _headLabels[i].fontSize = 9;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_backButton != null && _backAction != null)
                _backButton.onClick.RemoveListener(_backAction);
            if (_sortButton != null && _sortAction != null)
                _sortButton.onClick.RemoveListener(_sortAction);
            Destroy(_periodRail);
            Destroy(_header);
            Destroy(_tableHead);
        }

        private static void ConfigureButton(
            Button button,
            Image image,
            Sprite highlighted,
            Sprite pressed)
        {
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState sprites = button.spriteState;
            sprites.highlightedSprite = highlighted;
            sprites.pressedSprite = pressed;
            button.spriteState = sprites;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;
        }

        private static void Destroy(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(rect.gameObject);
        }
    }
}
