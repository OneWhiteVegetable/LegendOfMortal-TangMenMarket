using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Inventory
{
    internal sealed class InventoryPage
    {
        private readonly UiFactory _ui;
        private readonly UiTooltip _tooltip;
        private readonly RunState _state;
        private readonly ItemCatalog _items;
        private readonly ShortService _shorts;
        private readonly List<InventorySlotCard> _cards = new List<InventorySlotCard>();
        private readonly List<InventorySlotModel> _slots = new List<InventorySlotModel>();
        private readonly RectTransform _content;
        private readonly Text _goodsValue;
        private readonly Text _floatingValue;
        private readonly Text _lifeValue;
        private readonly Text _slotsValue;
        private readonly Text _carryValue;
        private readonly Text _debtValue;
        private readonly Text _status;
        private AssetService _assets;

        internal InventoryPage(
            UiFactory ui,
            Transform parent,
            RunState state,
            ItemCatalog items,
            ShortService shorts,
            UiTooltip tooltip)
        {
            Root = UiFactory.CreateRect("InventoryPage", parent);
            UiFactory.Stretch(Root);
            _ui = ui;
            _tooltip = tooltip;
            _state = state;
            _items = items;
            _shorts = shorts;

            Image summary = ui.CreatePanel(
                Root,
                "SharedCapacitySummary",
                null,
                null,
                new Color(0.12f, 0.13f, 0.115f, 0.97f));
            UiFactory.SetRect(
                summary.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 122f));

            RectTransform metrics = UiFactory.CreateRect("Metrics", summary.transform);
            UiFactory.SetRect(
                metrics,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -6f),
                new Vector2(-16f, 62f));
            GridLayoutGroup metricGrid = metrics.gameObject.AddComponent<GridLayoutGroup>();
            metricGrid.padding = new RectOffset(2, 2, 2, 2);
            metricGrid.spacing = new Vector2(5f, 5f);
            metricGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            metricGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            metricGrid.childAlignment = TextAnchor.UpperLeft;
            metricGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            InventoryGridLayout metricLayout = metrics.gameObject.AddComponent<InventoryGridLayout>();
            metricLayout.NarrowColumns = 6;
            metricLayout.WideColumns = 6;
            metricLayout.RowHeight = 56f;
            metricLayout.HorizontalSpacing = 5f;

            _goodsValue = CreateSummaryMetric(ui, metrics, "持货市值", out _);
            _floatingValue = CreateSummaryMetric(ui, metrics, "浮动盈亏", out _);
            _lifeValue = CreateSummaryMetric(ui, metrics, "真实体力 / 总格", out _);
            _slotsValue = CreateSummaryMetric(ui, metrics, "已占 / 空余", out _);
            _carryValue = CreateSummaryMetric(ui, metrics, "负重能力", out _);
            Text debtCaption;
            _debtValue = CreateSummaryMetric(ui, metrics, "欠款 / 冻结", out debtCaption);

            _status = ui.CreateText(
                summary.transform,
                "Status",
                string.Empty,
                16,
                InventoryStyle.Paper,
                TextAnchor.MiddleLeft);
            _status.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.SetRect(
                _status.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 8f),
                new Vector2(-28f, 42f));

            ScrollRect scroll = ui.CreateScrollRect(
                Root,
                "SharedInventorySlots",
                Vector2.zero,
                out _content);
            UiFactory.SetRect(
                scroll.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            scroll.GetComponent<RectTransform>().offsetMin = new Vector2(0f, 0f);
            scroll.GetComponent<RectTransform>().offsetMax = new Vector2(0f, -132f);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            GridLayoutGroup grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(5, 5, 5, 8);
            grid.spacing = new Vector2(10f, 10f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            InventoryGridLayout layout = _content.gameObject.AddComponent<InventoryGridLayout>();
            layout.NarrowColumns = 4;
            layout.WideColumns = 6;
            layout.RowHeight = 196f;
            layout.HorizontalSpacing = 10f;

            for (int i = 0; i < 12; i++)
            {
                InventorySlotCard card = new InventorySlotCard(ui, _content, tooltip);
                card.SetActive(false);
                _cards.Add(card);
            }
        }

        internal RectTransform Root { get; private set; }

        internal void BindAssets(AssetService assets)
        {
            _assets = assets;
        }

        internal void SetActive(bool active)
        {
            if (Root.gameObject.activeSelf != active)
                Root.gameObject.SetActive(active);
        }

        internal void Refresh(CapacitySnapshot capacity, AssetSnapshot assets, string notice)
        {
            BuildSlots(capacity);
            EnsureCardCount(_slots.Count);
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i < _slots.Count)
                    _cards[i].Bind(_slots[i]);
                else
                    _cards[i].SetActive(false);
            }

            int frozen = CountFrozen(capacity);
            _goodsValue.text = assets == null ? "--" : InventoryStyle.Amount(assets.LongValue) + " 文";
            _floatingValue.text = assets == null ? "--" : InventoryStyle.SignedAmount(assets.FloatingProfit) + " 文";
            _floatingValue.color = assets == null ? InventoryStyle.Paper : InventoryStyle.Profit(assets.FloatingProfit);
            _lifeValue.text = capacity == null
                ? "--"
                : capacity.Stats.Life + " / " + capacity.TotalSlots + " 格";
            _slotsValue.text = capacity == null
                ? "--"
                : capacity.UsedSlots + " / " + capacity.AvailableSlots;
            _carryValue.text = capacity == null ? "--" : InventoryStyle.Amount(capacity.CarryingCapacity);
            _debtValue.text = InventoryStyle.Amount(_state.Debt) + " 文 / " + frozen + " 项";
            _debtValue.color = _state.Debt > 0 || frozen > 0
                ? new Color(1f, 0.67f, 0.48f, 1f)
                : InventoryStyle.Paper;

            if (!string.IsNullOrEmpty(notice))
                _status.text = notice;
            else if (capacity == null)
                _status.text = "货囊容量暂不可读，未修改任何持仓。";
            else if (capacity.Error != TradeError.None)
                _status.text = "货囊数据异常：" + capacity.Error + " " + capacity.InvalidItemId + "。仅保留只读视图。";
            else if (capacity.IsOverCapacity)
                _status.text = "当前已超容：旧持仓保留，本页不执行加仓或平仓；多空仍共用同一组格位。";
            else if (_state.Debt > 0)
                _status.text = "到期平仓欠款 " + InventoryStyle.Amount(_state.Debt) + " 文；后续银两入账由行商服务优先偿还。";
            else if (frozen > 0)
                _status.text = frozen + " 项退市仓位已冻结，按最后可见价列账，无法平仓。";
            else
                _status.text = "多空共用格位；每格上限 = 负重能力 ÷ 货物重量（向下取整）。";
        }

        private void BuildSlots(CapacitySnapshot capacity)
        {
            _slots.Clear();
            if (capacity == null)
                return;

            if (capacity.Items != null)
            {
                for (int i = 0; i < capacity.Items.Count; i++)
                {
                    ItemCapacity item = capacity.Items[i];
                    if (item != null && item.LongQuantity + item.ShortQuantity > 0)
                        AddItemSlots(item);
                }
            }

            if (capacity.UsedSlots >= 0 && capacity.UsedSlots < capacity.TotalSlots)
            {
                int emptyCount = capacity.TotalSlots - capacity.UsedSlots;
                for (int i = 0; i < emptyCount; i++)
                    _slots.Add(InventorySlotModel.Empty());
            }
        }

        private void AddItemSlots(ItemCapacity item)
        {
            ItemDefinition definition;
            _items.TryGetItem(item.ItemId, out definition);
            Holding holding = _state.FindHolding(item.ItemId);
            List<ShortBatch> batches = _state.GetShortBatches(item.ItemId);
            double cost = holding == null ? 0.0 : holding.TotalCost;
            double proceeds = 0.0;
            for (int i = 0; i < batches.Count; i++)
            {
                if (batches[i].Quantity > 0)
                    proceeds += batches[i].Proceeds;
            }

            long totalQuantity = item.LongQuantity > long.MaxValue - item.ShortQuantity
                ? long.MaxValue
                : item.LongQuantity + item.ShortQuantity;
            double averageCost = totalQuantity > 0 ? (cost + proceeds) / totalQuantity : 0.0;
            double currentPrice = 0.0;
            bool hasPrice = _assets != null && _assets.TryGetVisiblePrice(item.ItemId, out currentPrice);
            if (!hasPrice)
                currentPrice = 0.0;
            double totalFloating = hasPrice
                ? currentPrice * item.LongQuantity - cost + proceeds - currentPrice * item.ShortQuantity
                : 0.0;
            bool delisted = XingShangMod.UI.Market.MarketView.IsDelisted(_state, item.ItemId);
            int weeksRemaining = item.ShortQuantity > 0 ? _shorts.GetWeeksRemaining(item.ItemId) : -1;
            int dueWeek = item.ShortQuantity > 0 ? _shorts.GetNextDueWeek(item.ItemId) : -1;

            bool canSplit = item.StackLimit > 0 && item.UsedSlots > 0 && item.UsedSlots != int.MaxValue;
            int renderedSlots = canSplit ? item.UsedSlots : 1;
            long remaining = totalQuantity;
            string itemName = definition == null ? "未知货物 " + item.ItemId : definition.Name;
            string description = definition == null ? "该货物不在当前商品目录中。" : definition.Description;
            string iconId = definition == null ? string.Empty : Path.GetFileNameWithoutExtension(definition.IconPath);
            string direction = item.LongQuantity > 0 && item.ShortQuantity > 0
                ? "多/空"
                : item.ShortQuantity > 0 ? "做空" : "做多";
            string costCaption = item.ShortQuantity > 0 && item.LongQuantity == 0 ? "实收" : "成本";

            for (int slotIndex = 0; slotIndex < renderedSlots; slotIndex++)
            {
                long stackQuantity = canSplit
                    ? Math.Min(remaining, item.StackLimit)
                    : totalQuantity;
                remaining -= stackQuantity;
                if (remaining < 0)
                    remaining = 0;
                double stackFloating = totalQuantity > 0
                    ? totalFloating * stackQuantity / totalQuantity
                    : 0.0;
                string status;
                if (delisted)
                    status = "冻结 · 已退市 · 无法平仓";
                else if (item.StackLimit <= 0)
                    status = "负重不足 · 当前无法堆叠";
                else if (item.ShortQuantity > 0 && weeksRemaining >= 0)
                    status = "占" + item.UsedSlots + "格 · " +
                        (weeksRemaining == 0 ? "24周借契本周到期" : "24周借契最近余" + weeksRemaining + "周");
                else
                    status = "占" + item.UsedSlots + "格 · 每格上限" + item.StackLimit;

                string tooltip = BuildTooltip(
                    item,
                    itemName,
                    description,
                    direction,
                    cost,
                    proceeds,
                    averageCost,
                    hasPrice,
                    currentPrice,
                    totalFloating,
                    delisted,
                    dueWeek,
                    weeksRemaining);
                _slots.Add(new InventorySlotModel
                {
                    IsEmpty = false,
                    ItemId = item.ItemId,
                    ItemName = itemName,
                    IconId = iconId,
                    Direction = direction,
                    CostCaption = costCaption,
                    StackQuantity = stackQuantity,
                    TotalQuantity = totalQuantity,
                    SlotIndex = slotIndex,
                    RenderedSlots = renderedSlots,
                    AverageCost = averageCost,
                    CurrentPrice = currentPrice,
                    HasPrice = hasPrice,
                    FloatingProfit = stackFloating,
                    Status = status,
                    Delisted = delisted,
                    Tooltip = tooltip
                });
            }
        }

        private static string BuildTooltip(
            ItemCapacity item,
            string name,
            string description,
            string direction,
            double longCost,
            double shortProceeds,
            double averageCost,
            bool hasPrice,
            double currentPrice,
            double floating,
            bool delisted,
            int dueWeek,
            int weeksRemaining)
        {
            StringBuilder text = new StringBuilder();
            text.Append(name).Append('\n').Append(description);
            text.Append("\n\n方向：").Append(direction);
            if (item.LongQuantity > 0)
                text.Append("\n持货：").Append(InventoryStyle.Amount(item.LongQuantity))
                    .Append(" 件；成本 ").Append(InventoryStyle.Amount(longCost)).Append(" 文");
            if (item.ShortQuantity > 0)
                text.Append("\n待还：").Append(InventoryStyle.Amount(item.ShortQuantity))
                    .Append(" 件；批次实收 ").Append(InventoryStyle.Amount(shortProceeds)).Append(" 文");
            text.Append("\n平均每件成本/实收：").Append(InventoryStyle.Amount(averageCost)).Append(" 文");
            text.Append("\n现价：").Append(hasPrice ? InventoryStyle.Amount(currentPrice) + " 文" : "暂无可见价格");
            text.Append("\n物品总浮盈：").Append(hasPrice ? InventoryStyle.SignedAmount(floating) + " 文" : "--");
            text.Append("\n占格：").Append(item.UsedSlots).Append("；每格上限：").Append(item.StackLimit);
            if (item.ShortQuantity > 0 && dueWeek >= 0)
            {
                text.Append("\n最近批次：第 ").Append(dueWeek).Append(" 市场周到期");
                text.Append(weeksRemaining >= 0 ? "，余 " + weeksRemaining + " / 24 周" : string.Empty);
            }
            if (delisted)
                text.Append("\n已退市：仓位冻结，按最后可见价列账，不能买卖、借还或自动平仓。");
            return text.ToString();
        }

        private int CountFrozen(CapacitySnapshot capacity)
        {
            if (capacity == null || capacity.Items == null)
                return 0;
            int count = 0;
            for (int i = 0; i < capacity.Items.Count; i++)
            {
                ItemCapacity item = capacity.Items[i];
                if (item != null && XingShangMod.UI.Market.MarketView.IsDelisted(_state, item.ItemId))
                    count++;
            }
            return count;
        }

        private void EnsureCardCount(int count)
        {
            while (_cards.Count < count)
                _cards.Add(new InventorySlotCard(_ui, _content, _tooltip));
        }

        private static Text CreateSummaryMetric(UiFactory ui, Transform parent, string caption, out Text captionText)
        {
            RectTransform root = UiFactory.CreateRect(caption, parent);
            captionText = ui.CreateText(
                root,
                "Caption",
                caption,
                13,
                new Color(0.69f, 0.71f, 0.65f, 1f),
                TextAnchor.MiddleLeft);
            UiFactory.SetRect(
                captionText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -2f),
                new Vector2(-8f, 22f));
            Text value = ui.CreateText(
                root,
                "Value",
                "--",
                20,
                InventoryStyle.Paper,
                TextAnchor.MiddleLeft);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(
                value.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 2f),
                new Vector2(-8f, 30f));
            return value;
        }
    }

    internal sealed class InventorySlotModel
    {
        internal bool IsEmpty;
        internal string ItemId;
        internal string ItemName;
        internal string IconId;
        internal string Direction;
        internal string CostCaption;
        internal long StackQuantity;
        internal long TotalQuantity;
        internal int SlotIndex;
        internal int RenderedSlots;
        internal double AverageCost;
        internal double CurrentPrice;
        internal bool HasPrice;
        internal double FloatingProfit;
        internal string Status;
        internal bool Delisted;
        internal string Tooltip;

        internal static InventorySlotModel Empty()
        {
            return new InventorySlotModel
            {
                IsEmpty = true,
                ItemId = string.Empty,
                ItemName = "空",
                IconId = string.Empty,
                Tooltip = "空格。多头与空头共用此格位。"
            };
        }
    }

    internal sealed class InventorySlotCard
    {
        private readonly UiFactory _ui;
        private readonly Image _background;
        private readonly Button _button;
        private readonly Image _icon;
        private readonly Text _direction;
        private readonly Text _name;
        private readonly Text _quantity;
        private readonly Text _cost;
        private readonly Text _profit;
        private readonly Text _status;
        private readonly Text _empty;
        private readonly Image _wasted;
        private string _tooltip = string.Empty;

        internal InventorySlotCard(UiFactory ui, Transform parent, UiTooltip tooltip)
        {
            _ui = ui;
            _background = ui.CreatePanel(parent, "InventorySlot", "slot_inventory_empty_9s");
            _button = _background.gameObject.AddComponent<Button>();
            _button.targetGraphic = _background;
            ColorBlock colors = _button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.83f, 1f);
            colors.pressedColor = new Color(0.84f, 0.80f, 0.70f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            _button.colors = colors;

            _icon = ui.CreateImage(
                _background.transform,
                "ItemIcon",
                (Sprite)null,
                new Vector2(96f, 84f),
                true);
            UiFactory.SetTopLeft(_icon.rectTransform, new Vector2(12f, -32f), new Vector2(96f, 84f));

            _direction = ui.CreateText(
                _background.transform,
                "Direction",
                string.Empty,
                15,
                InventoryStyle.Muted,
                TextAnchor.UpperRight,
                new Vector2(150f, 24f));
            UiFactory.SetRect(
                _direction.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -9f),
                new Vector2(160f, 24f));

            _name = ui.CreateText(
                _background.transform,
                "Name",
                string.Empty,
                19,
                InventoryStyle.Ink,
                TextAnchor.MiddleRight,
                new Vector2(160f, 28f));
            UiFactory.SetRect(
                _name.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -34f),
                new Vector2(165f, 28f));

            _quantity = ui.CreateText(
                _background.transform,
                "Quantity",
                string.Empty,
                29,
                InventoryStyle.Ink,
                TextAnchor.MiddleRight,
                new Vector2(160f, 40f));
            UiFactory.SetRect(
                _quantity.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -63f),
                new Vector2(165f, 40f));

            _cost = CreateLine(ui, _background.transform, "Cost", 15, 111f, 24f);
            _profit = CreateLine(ui, _background.transform, "Profit", 17, 136f, 27f);
            _status = CreateLine(ui, _background.transform, "Status", 14, 164f, 23f);

            _empty = ui.CreateText(
                _background.transform,
                "Empty",
                "空",
                30,
                new Color(0.20f, 0.20f, 0.17f, 0.20f),
                TextAnchor.MiddleCenter);
            UiFactory.Stretch(_empty.rectTransform);

            _wasted = ui.CreateImage(
                _background.transform,
                "Wasted",
                "wasted_delist_wordmark",
                new Vector2(140f, 50f),
                true);
            _wasted.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            _wasted.gameObject.SetActive(false);
            tooltip.Attach(_background.gameObject, () => _tooltip);
        }

        internal void SetActive(bool active)
        {
            if (_background.gameObject.activeSelf != active)
                _background.gameObject.SetActive(active);
        }

        internal void Bind(InventorySlotModel model)
        {
            SetActive(true);
            _tooltip = model.Tooltip;
            _background.gameObject.name = model.IsEmpty
                ? "InventorySlot.Empty"
                : "InventorySlot." + model.ItemId + "." + model.SlotIndex;
            _background.sprite = _ui.Sprites.GetSprite(model.IsEmpty || model.Delisted
                ? "slot_inventory_empty_9s"
                : model.Direction == "做空" ? "slot_inventory_short_9s" : "slot_inventory_long_9s");
            _background.color = model.Delisted ? new Color(0.58f, 0.58f, 0.54f, 1f) : Color.white;
            _button.interactable = !model.IsEmpty && !model.Delisted;
            _empty.gameObject.SetActive(model.IsEmpty);
            _icon.gameObject.SetActive(!model.IsEmpty);
            _direction.gameObject.SetActive(!model.IsEmpty);
            _name.gameObject.SetActive(!model.IsEmpty);
            _quantity.gameObject.SetActive(!model.IsEmpty);
            _cost.gameObject.SetActive(!model.IsEmpty);
            _profit.gameObject.SetActive(!model.IsEmpty);
            _status.gameObject.SetActive(!model.IsEmpty);
            _wasted.gameObject.SetActive(!model.IsEmpty && model.Delisted);
            if (model.IsEmpty)
                return;

            _icon.sprite = _ui.Sprites.GetSprite(model.IconId);
            _icon.color = model.Delisted ? new Color(0.40f, 0.42f, 0.39f, 0.78f) : Color.white;
            _direction.text = model.Direction + " · " + (model.SlotIndex + 1) + "/" + model.RenderedSlots + "格";
            _direction.color = model.Delisted
                ? InventoryStyle.Muted
                : model.Direction == "做空" ? InventoryStyle.Green : InventoryStyle.Red;
            _name.text = model.ItemName;
            _name.color = model.Delisted ? InventoryStyle.Muted : InventoryStyle.Ink;
            _quantity.text = "×" + InventoryStyle.Amount(model.StackQuantity);
            _quantity.color = model.Delisted ? InventoryStyle.Muted : InventoryStyle.Ink;
            _cost.text = model.CostCaption + " " + InventoryStyle.Amount(model.AverageCost) +
                " · 现价 " + (model.HasPrice ? InventoryStyle.Amount(model.CurrentPrice) : "--");
            _cost.color = InventoryStyle.Muted;
            _profit.text = "浮盈 " + (model.HasPrice ? InventoryStyle.SignedAmount(model.FloatingProfit) : "--") +
                " · 共" + InventoryStyle.Amount(model.TotalQuantity) + "件";
            _profit.color = model.HasPrice ? InventoryStyle.Profit(model.FloatingProfit) : InventoryStyle.Muted;
            _status.text = model.Status;
            _status.color = model.Delisted ? InventoryStyle.Red : InventoryStyle.Muted;
        }

        private static Text CreateLine(
            UiFactory ui,
            Transform parent,
            string name,
            int fontSize,
            float top,
            float height)
        {
            Text text = ui.CreateText(parent, name, string.Empty, fontSize, InventoryStyle.Ink, TextAnchor.MiddleLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12f, -top - height);
            rect.offsetMax = new Vector2(-12f, -top);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }

    internal sealed class InventoryGridLayout : MonoBehaviour
    {
        internal int NarrowColumns = 4;
        internal int WideColumns = 6;
        internal float RowHeight = 196f;
        internal float HorizontalSpacing = 10f;
        private RectTransform _rect;
        private GridLayoutGroup _grid;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _grid = GetComponent<GridLayoutGroup>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void Apply()
        {
            if (_rect == null || _grid == null || _rect.rect.width <= 0f)
                return;
            int columns = Screen.width > 0 && Screen.width <= 1450 ? NarrowColumns : WideColumns;
            columns = Math.Max(1, columns);
            float width = (_rect.rect.width - _grid.padding.horizontal - HorizontalSpacing * (columns - 1)) / columns;
            width = Mathf.Max(80f, width);
            Vector2 cellSize = new Vector2(width, RowHeight);
            if (_grid.constraintCount != columns || _grid.cellSize != cellSize)
            {
                _grid.constraintCount = columns;
                _grid.cellSize = cellSize;
                LayoutRebuilder.MarkLayoutForRebuild(_rect);
            }
        }
    }
}
