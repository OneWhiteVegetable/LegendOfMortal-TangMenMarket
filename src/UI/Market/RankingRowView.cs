using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI.Market
{
    internal sealed class RankingRowView : IDisposable
    {
        internal const int SparkPointCount = 20;

        private readonly UiFrontendServices _services;
        private readonly WeeklyChartView _chart;
        private readonly RectTransform _motionPivot;
        private readonly RectTransform _chartHost;
        private readonly HorizontalLayoutGroup _rowLayout;
        private readonly HorizontalLayoutGroup _itemLayout;
        private readonly LayoutElement[] _columns = new LayoutElement[6];
        private readonly RectTransform _emptyState;
        private readonly Text _emptyTitle;
        private readonly Text _emptyBody;
        private readonly LayoutElement _rowElement;
        private readonly LayoutElement _itemImageElement;
        private readonly LayoutElement _nameElement;
        private readonly LayoutElement _keyElement;
        private readonly Text _rankText;
        private readonly Text _nameText;
        private readonly Text _keyText;
        private readonly Text _priceText;
        private readonly Text _changeText;
        private readonly Image _itemImage;
        private readonly Image _openIcon;
        private readonly Button _openButton;
        private readonly RankingRowFeedback _feedback;
        private bool _compact;
        private bool _empty;
        private bool _disposed;

        internal RankingRowView(
            UiFrontendServices services,
            UiPrefabInstance view,
            bool compact)
        {
            _services = services;
            View = view;
            _rankText = view.Text("RankText");
            _nameText = view.Text("NameText");
            _keyText = view.Text("KeyText");
            _priceText = view.Text("PriceText");
            _changeText = view.Text("ChangeText");
            _itemImage = view.Image("ItemImage");
            _openButton = view.Button("OpenButton");
            Image[] childImages = View.Root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                if (childImages[i].name == "OpenIcon")
                {
                    _openIcon = childImages[i];
                    break;
                }
            }
            _openButton.transition = Selectable.Transition.None;
            Image hitArea = _openButton.targetGraphic as Image;
            if (hitArea == null)
                hitArea = _openButton.GetComponent<Image>();
            if (hitArea != null)
            {
                hitArea.sprite = null;
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
            }

            _motionPivot = View.Root.Find("MotionPivot") as RectTransform;
            if (_motionPivot == null)
            {
                _motionPivot = RankingLayout.Rect("MotionPivot", View.Root);
                RankingLayout.Stretch(_motionPivot);
                List<Transform> originalChildren = new List<Transform>();
                for (int i = 0; i < View.Root.childCount; i++)
                {
                    Transform child = View.Root.GetChild(i);
                    if (child != _motionPivot)
                        originalChildren.Add(child);
                }
                for (int i = 0; i < originalChildren.Count; i++)
                    originalChildren[i].SetParent(_motionPivot, false);
            }
            RankingLayout.Stretch(_motionPivot);

            Image normal = RankingLayout.Image(
                "Background",
                _motionPivot,
                services.Assets.GetSprite("row_ranking_normal_9s"),
                RankingLayout.PaperLight,
                true);
            normal.color = new Color(1f, 1f, 1f, 0.75f);
            RankingLayout.IgnoreLayout(normal);
            RankingLayout.Stretch(normal.rectTransform);
            Image highlight = RankingLayout.Image(
                "FocusHighlight",
                _motionPivot,
                services.Assets.GetSprite("row_ranking_hover_9s"),
                RankingLayout.PaperLight,
                true);
            RankingLayout.IgnoreLayout(highlight);
            RankingLayout.Stretch(highlight.rectTransform);
            normal.transform.SetSiblingIndex(0);
            highlight.transform.SetSiblingIndex(1);
            Shadow inkShadow = normal.gameObject.AddComponent<Shadow>();
            inkShadow.effectColor = RankingLayout.Alpha(UiFormatting.Ink, 0.9f);
            inkShadow.effectDistance = new Vector2(-4f, 0f);
            inkShadow.useGraphicAlpha = true;
            inkShadow.enabled = false;

            _rowLayout = RankingLayout.Columns(_motionPivot, compact);
            string[] names = { "RankColumn", "ItemColumn", "PriceColumn", "ChangeColumn", "ChartColumn", "OpenColumn" };
            RectTransform[] columns = new RectTransform[6];
            for (int i = 0; i < columns.Length; i++)
            {
                columns[i] = RankingLayout.Rect(names[i], _motionPivot);
                _columns[i] = RankingLayout.Column(columns[i], i, compact);
            }

            _rankText.transform.SetParent(columns[0], false);
            RankingLayout.Stretch(_rankText.rectTransform);
            _rankText.alignment = TextAnchor.MiddleCenter;
            _rankText.fontStyle = FontStyle.Italic;
            _rankText.horizontalOverflow = HorizontalWrapMode.Overflow;

            HorizontalLayoutGroup itemLayout = columns[1].gameObject.AddComponent<HorizontalLayoutGroup>();
            _itemLayout = itemLayout;
            _itemLayout.spacing = 11f;
            _itemLayout.childAlignment = TextAnchor.MiddleLeft;
            _itemLayout.childControlWidth = true;
            _itemLayout.childControlHeight = true;
            _itemLayout.childForceExpandWidth = false;
            _itemLayout.childForceExpandHeight = false;
            _itemImage.transform.SetParent(columns[1], false);
            _itemImage.preserveAspect = true;
            _itemImageElement = RankingLayout.Layout(_itemImage, 48f, 48f);
            RectTransform itemText = RankingLayout.Rect("ItemText", columns[1]);
            RankingLayout.Layout(itemText, -1f, -1f, 1f);
            VerticalLayoutGroup itemTextLayout = itemText.gameObject.AddComponent<VerticalLayoutGroup>();
            itemTextLayout.childAlignment = TextAnchor.MiddleLeft;
            itemTextLayout.childControlWidth = true;
            itemTextLayout.childControlHeight = true;
            itemTextLayout.childForceExpandWidth = true;
            itemTextLayout.childForceExpandHeight = false;
            itemTextLayout.spacing = 1f;
            _nameText.transform.SetParent(itemText, false);
            _nameElement = RankingLayout.Layout(_nameText, -1f, 22f, 1f);
            ConfigureSingleLine(_nameText, 14, 10);
            _keyText.transform.SetParent(itemText, false);
            _keyElement = RankingLayout.Layout(_keyText, -1f, 14f, 1f);
            ConfigureSingleLine(_keyText, 9, 7);

            _priceText.transform.SetParent(columns[2], false);
            RankingLayout.Stretch(_priceText.rectTransform);
            ConfigureSingleLine(_priceText, 16, 11);
            _changeText.transform.SetParent(columns[3], false);
            RankingLayout.Stretch(_changeText.rectTransform);
            ConfigureSingleLine(_changeText, 16, 11);

            _chartHost = view.Rect("ChartHost");
            _chartHost.SetParent(columns[4], false);
            if (_openIcon != null)
            {
                _openIcon.transform.SetParent(columns[5], false);
                _openIcon.preserveAspect = true;
                RankingLayout.Center(_openIcon.rectTransform, 18f, 18f);
            }

            _rowElement = RankingLayout.Layout(View.Root, -1f, 68f);
            _feedback = View.GameObject.AddComponent<RankingRowFeedback>();
            _feedback.Configure(services.Motion, _motionPivot, highlight.gameObject, inkShadow);
            _chart = new WeeklyChartView(
                _chartHost,
                WeeklyChartMode.Spark,
                services.Assets.RegularFont,
                services.Assets.DryBrushTexture);

            _emptyState = RankingLayout.Rect("EmptyState", _motionPivot);
            RankingLayout.IgnoreLayout(_emptyState);
            RankingLayout.Stretch(_emptyState, 32f, 12f, 32f, 12f);
            VerticalLayoutGroup emptyLayout = _emptyState.gameObject.AddComponent<VerticalLayoutGroup>();
            emptyLayout.childAlignment = TextAnchor.MiddleCenter;
            emptyLayout.childControlWidth = true;
            emptyLayout.childControlHeight = true;
            emptyLayout.childForceExpandWidth = true;
            emptyLayout.childForceExpandHeight = false;
            emptyLayout.spacing = 6f;
            Image emptyIcon = RankingLayout.Image(
                "EmptyIcon",
                _emptyState,
                services.Assets.GetSprite("icon_rank"),
                RankingLayout.Gold);
            emptyIcon.color = RankingLayout.Gold;
            emptyIcon.preserveAspect = true;
            RankingLayout.Layout(emptyIcon, 34f, 34f);
            _emptyTitle = RankingLayout.Text(
                "EmptyTitle",
                _emptyState,
                "行市尚未开榜",
                22,
                UiFormatting.Ink,
                services.Assets.BoldFont,
                TextAnchor.MiddleCenter);
            RankingLayout.Layout(_emptyTitle, -1f, 32f, 1f);
            _emptyBody = RankingLayout.Text(
                "EmptyBody",
                _emptyState,
                "本周目还没有已出现商品。\n新货上市后，会按累计涨跌列入此榜。",
                12,
                UiFormatting.Muted,
                services.Assets.RegularFont,
                TextAnchor.MiddleCenter);
            _emptyBody.lineSpacing = 1.35f;
            RankingLayout.Layout(_emptyBody, -1f, 48f, 1f);
            _emptyState.gameObject.SetActive(false);
            SetCompact(compact);
        }

        internal UiPrefabInstance View { get; private set; }
        internal GameObject GameObject { get { return View.GameObject; } }
        internal string ItemId { get; private set; }
        internal UnityAction OpenAction;

        internal void BindEmpty()
        {
            _empty = true;
            ItemId = null;
            for (int i = 0; i < _columns.Length; i++)
                _columns[i].gameObject.SetActive(false);
            _emptyState.gameObject.SetActive(true);
            _rowElement.preferredHeight = _compact ? 150f : 164f;
            _chart.SetPoints(new List<WeeklyPoint>());
            _chart.Chart.color = Color.white;
            _openButton.interactable = false;
            _feedback.SetInteractable(false);
        }

        internal void Bind(RankingEntry entry, int rank)
        {
            _empty = false;
            ItemId = entry.Item.ItemId;
            _emptyState.gameObject.SetActive(false);
            for (int i = 0; i < _columns.Length; i++)
                _columns[i].gameObject.SetActive(true);
            _rowElement.preferredHeight = _compact ? 57f : 68f;
            Color foreground = entry.Delisted ? UiFormatting.Muted : UiFormatting.Ink;
            _rankText.text = rank.ToString("00");
            _rankText.color = rank <= 3 && !entry.Delisted ? UiFormatting.Red : foreground;
            _itemImage.sprite = _services.Assets.GetSprite(MarketQuery.IconKey(entry.Item));
            _itemImage.color = entry.Delisted
                ? new Color(0.45f, 0.47f, 0.45f, 0.65f)
                : Color.white;
            _nameText.text = (entry.Delisted ? "WASTED · " : string.Empty) + entry.Item.Name;
            _nameText.color = foreground;
            _keyText.text = MarketQuery.ItemTotalReturnCaption(entry.History) + " · " + entry.Item.ItemId;
            _keyText.color = UiFormatting.Muted;
            _priceText.text = entry.Current == null
                ? "—"
                : UiFormatting.Money(entry.Current.Price) + " <size=10>文</size>";
            _priceText.color = foreground;
            _changeText.text = entry.Current == null ? "—" : UiFormatting.Percent(entry.TotalReturn);
            _changeText.color = entry.Delisted
                ? UiFormatting.Muted
                : UiFormatting.Trend(entry.TotalReturn);
            List<WeeklyPoint> history = entry.SparkHistory ?? entry.History;
            if (history.Count > SparkPointCount)
                history = history.GetRange(history.Count - SparkPointCount, SparkPointCount);
            _chart.SetPoints(history);
            _chart.Chart.color = entry.Delisted
                ? new Color(0.5f, 0.5f, 0.5f, 0.35f)
                : Color.white;
            _openButton.interactable = true;
            _feedback.SetInteractable(true);
        }

        internal void SetCompact(bool compact)
        {
            if (_disposed)
                return;
            _compact = compact;
            _rowElement.preferredHeight = _empty
                ? (compact ? 150f : 164f)
                : (compact ? 57f : 68f);
            _emptyTitle.fontSize = compact ? 20 : 22;
            _emptyBody.fontSize = compact ? 11 : 12;
            RankingLayout.ConfigureColumns(_rowLayout, compact);
            _rowLayout.padding = new RectOffset(14, 14, compact ? 4 : 6, compact ? 4 : 6);
            for (int i = 0; i < _columns.Length; i++)
                RankingLayout.ConfigureColumn(_columns[i], i, compact);
            _itemLayout.spacing = compact ? 8f : 11f;
            _itemImageElement.preferredWidth = compact ? 40f : 48f;
            _itemImageElement.preferredHeight = compact ? 40f : 48f;
            _rankText.fontSize = compact ? 25 : 29;
            _nameText.fontSize = compact ? 13 : 14;
            _nameText.resizeTextMaxSize = compact ? 13 : 14;
            _nameElement.preferredHeight = compact ? 20f : 22f;
            _keyText.fontSize = compact ? 8 : 9;
            _keyText.resizeTextMaxSize = compact ? 8 : 9;
            _keyElement.preferredHeight = compact ? 12f : 14f;
            _priceText.fontSize = compact ? 14 : 16;
            _priceText.resizeTextMaxSize = compact ? 14 : 16;
            _changeText.fontSize = compact ? 14 : 16;
            _changeText.resizeTextMaxSize = compact ? 14 : 16;
            float chartHeight = compact ? 36f : 42f;
            _chartHost.anchorMin = new Vector2(0f, 0.5f);
            _chartHost.anchorMax = new Vector2(1f, 0.5f);
            _chartHost.pivot = new Vector2(0.5f, 0.5f);
            _chartHost.offsetMin = new Vector2(0f, -chartHeight * 0.5f);
            _chartHost.offsetMax = new Vector2(0f, chartHeight * 0.5f);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _feedback.Dispose();
            _chart.Dispose();
            OpenAction = null;
            ItemId = null;
            View.Dispose();
        }

        private static void ConfigureSingleLine(Text text, int size, int minimum)
        {
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimum;
            text.resizeTextMaxSize = size;
            text.lineSpacing = 1f;
            text.raycastTarget = false;
        }
    }

    internal sealed class RankingEntry
    {
        internal ItemDefinition Item;
        internal List<WeeklyPoint> History;
        internal List<WeeklyPoint> SparkHistory;
        internal WeeklyPoint Current;
        internal double TotalReturn;
        internal bool Delisted;
        internal int AppearanceOrder;
    }
}
