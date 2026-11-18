using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Market
{
    internal sealed class MarketCartPanel : IDisposable
    {
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f, 1f);
        private static readonly Color Muted = new Color(0.40f, 0.37f, 0.32f, 1f);
        private static readonly Color Error = new Color(0.68f, 0.19f, 0.14f, 1f);

        private readonly UiFactory _ui;
        private readonly RunState _state;
        private readonly ItemCatalog _items;
        private readonly TradingService _trading;
        private readonly CapacityService _capacity;
        private readonly IReadOnlyDictionary<string, ItemDefinition> _definitions;
        private readonly List<CartEntry> _entries = new List<CartEntry>();
        private readonly List<CartEntryView> _views = new List<CartEntryView>();
        private readonly RectTransform _content;
        private readonly Text _countText;
        private readonly Text _summaryText;
        private readonly Text _capacityText;
        private readonly Text _statusText;
        private readonly Button _clearButton;
        private readonly Button _confirmButton;
        private TradeResult _preview;

        internal event Action ConfirmRequested;

        internal MarketCartPanel(
            Transform parent,
            UiFactory ui,
            RunState state,
            ItemCatalog items,
            TradingService trading,
            CapacityService capacity,
            IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            _ui = ui;
            _state = state;
            _items = items;
            _trading = trading;
            _capacity = capacity;
            _definitions = definitions;

            Image root = ui.CreatePanel(
                parent,
                "ContractLedger",
                "panel_cart_rail_9s",
                null,
                Color.white);
            Root = root.rectTransform;
            Image cartIcon = ui.CreateImage(
                Root,
                "CartIcon",
                "icon_cart",
                new Vector2(38f, 38f),
                true);
            UiFactory.SetTopLeft(cartIcon.rectTransform, new Vector2(16f, -14f), new Vector2(38f, 38f));
            Text title = ui.CreateText(
                Root,
                "Title",
                "契匣",
                27,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(140f, 40f));
            UiFactory.SetTopLeft(title.rectTransform, new Vector2(62f, -10f), new Vector2(140f, 40f));
            Text subtitle = ui.CreateText(
                Root,
                "Subtitle",
                "CONTRACT LEDGER",
                11,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(160f, 24f));
            UiFactory.SetTopLeft(subtitle.rectTransform, new Vector2(64f, -45f), new Vector2(160f, 24f));
            _countText = ui.CreateText(
                Root,
                "Count",
                "0",
                19,
                Ink,
                TextAnchor.MiddleCenter,
                new Vector2(36f, 36f));
            SetTopRight(_countText, new Vector2(-18f, -18f), new Vector2(36f, 36f));
            _clearButton = ui.CreateButton(
                Root,
                "Clear",
                "清空",
                Clear,
                16,
                new Vector2(62f, 36f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetTopRight(_clearButton, new Vector2(-62f, -18f), new Vector2(62f, 36f));
            Text pending = ui.CreateText(
                Root,
                "Pending",
                "待结算 · 整匣同成同败",
                16,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(280f, 32f));
            UiFactory.SetTopLeft(pending.rectTransform, new Vector2(20f, -72f), new Vector2(280f, 32f));

            ScrollRect scroll = ui.CreateScrollRect(Root, "EntryScroll", Vector2.zero, out _content);
            UiFactory.Stretch(scroll.GetComponent<RectTransform>(), new Vector2(10f, 286f), new Vector2(-10f, -110f));

            _summaryText = ui.CreateText(
                Root,
                "Summary",
                string.Empty,
                18,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(304f, 48f));
            SetBottomLeft(_summaryText, new Vector2(20f, 226f), new Vector2(304f, 48f));
            _capacityText = ui.CreateText(
                Root,
                "Capacity",
                string.Empty,
                16,
                Muted,
                TextAnchor.UpperLeft,
                new Vector2(304f, 64f));
            SetBottomLeft(_capacityText, new Vector2(20f, 158f), new Vector2(304f, 64f));
            _statusText = ui.CreateText(
                Root,
                "Status",
                string.Empty,
                16,
                Muted,
                TextAnchor.UpperLeft,
                new Vector2(304f, 70f));
            SetBottomLeft(_statusText, new Vector2(20f, 86f), new Vector2(304f, 70f));
            _confirmButton = ui.CreateButton(
                Root,
                "Confirm",
                "预览并立契",
                Confirm,
                22,
                new Vector2(304f, 60f),
                "button_checkout_red_normal_9s",
                "button_checkout_red_hover_9s",
                "button_checkout_red_pressed_9s",
                "button_checkout_red_disabled_9s");
            SetBottomLeft(_confirmButton, new Vector2(20f, 18f), new Vector2(304f, 60f));
            Refresh();
        }

        internal RectTransform Root { get; private set; }

        internal void Add(string itemId, TradeAction action, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0 || MarketView.IsDelisted(_state, itemId))
                return;
            CartEntry existing = null;
            for (int i = 0; i < _entries.Count; i++)
            {
                CartEntry entry = _entries[i];
                if (entry.Action == action && string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
                {
                    existing = entry;
                    break;
                }
            }
            if (existing == null)
            {
                _entries.Add(new CartEntry
                {
                    ItemId = itemId,
                    Action = action,
                    Quantity = quantity
                });
            }
            else
            {
                long combined = (long)existing.Quantity + quantity;
                existing.Quantity = combined >= int.MaxValue ? int.MaxValue : (int)combined;
            }
            Refresh();
        }

        internal List<TradeOrder> GetOrders()
        {
            List<TradeOrder> result = new List<TradeOrder>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
            {
                CartEntry entry = _entries[i];
                result.Add(new TradeOrder(entry.Action, entry.ItemId, entry.Quantity));
            }
            return result;
        }

        internal void Refresh()
        {
            RebuildEntries();
            RefreshPreview();
        }

        internal void RefreshPreview()
        {
            if (_entries.Count == 0)
            {
                _preview = null;
                ApplyPreview(null);
                return;
            }
            ApplyPreview(_trading.Preview(GetOrders()));
        }

        internal void ApplyPreview(TradeResult preview)
        {
            _preview = preview;
            CapacitySnapshot current = GetCurrentCapacity();
            CapacitySnapshot before = preview != null && preview.CapacityBefore != null
                ? preview.CapacityBefore
                : current;
            CapacitySnapshot after = preview != null && preview.CapacityAfter != null
                ? preview.CapacityAfter
                : before;

            if (_entries.Count == 0)
            {
                _summaryText.text = "货签尚空 · 尚无待结算契约";
                _statusText.text = "从商品卡或详情加入买、卖、借、还。";
                _statusText.color = Muted;
            }
            else if (preview == null)
            {
                _summaryText.text = "待结算 " + _entries.Count.ToString(CultureInfo.InvariantCulture) + " 笔";
                _statusText.text = "尚未取得结算预览。";
                _statusText.color = Error;
            }
            else if (!preview.Success)
            {
                _summaryText.text = "整匣未结算 · 请修改货签";
                _statusText.text = MarketView.GetErrorMessage(preview, _items);
                _statusText.color = Error;
            }
            else
            {
                _summaryText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "银两变动 {0:+#,0;-#,0;0} 文\n结算后 {1:N0} 文",
                    preview.CashDelta,
                    preview.CashAfter);
                string debt = preview.DebtDelta == 0
                    ? "预览有效，确认时将重新校验整份契匣。"
                    : "欠款变动 " + preview.DebtDelta.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture) + " 文。";
                _statusText.text = debt;
                _statusText.color = Muted;
            }

            if (before == null && after == null)
            {
                _capacityText.text = "格位与负重：无法读取原版属性。";
            }
            else
            {
                CapacitySnapshot first = before ?? after;
                CapacitySnapshot second = after ?? before;
                _capacityText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "格位 {0}/{1} → {2}/{3}\n负重能力 {4} → {5}",
                    first.UsedSlots,
                    first.TotalSlots,
                    second.UsedSlots,
                    second.TotalSlots,
                    first.CarryingCapacity,
                    second.CarryingCapacity);
            }
            _confirmButton.interactable = _entries.Count > 0 && preview != null && preview.Success;
            _clearButton.interactable = _entries.Count > 0;
        }

        internal string BuildPreviewMessage(TradeResult preview)
        {
            StringBuilder builder = new StringBuilder();
            int recordCount = preview.Records == null ? 0 : preview.Records.Count;
            int shown = Math.Min(recordCount, 5);
            for (int i = 0; i < shown; i++)
            {
                TradeRecord record = preview.Records[i];
                ItemDefinition item;
                string name = _items.TryGetItem(record.ItemId, out item) ? item.Name : record.ItemId;
                builder.Append(MarketView.GetRecordActionName(record.Action));
                builder.Append(' ');
                builder.Append(name);
                builder.Append(" ×");
                builder.Append(record.Quantity.ToString(CultureInfo.InvariantCulture));
                builder.Append(" @ ");
                builder.Append(MarketView.FormatNumber(record.UnitPrice));
                builder.AppendLine();
            }
            if (recordCount > shown)
                builder.AppendLine("另有 " + (recordCount - shown).ToString(CultureInfo.InvariantCulture) + " 条结算记录。");
            builder.Append("银两变动：");
            builder.Append(preview.CashDelta.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture));
            builder.Append(" 文；结算后 ");
            builder.Append(preview.CashAfter.ToString("N0", CultureInfo.InvariantCulture));
            builder.AppendLine(" 文。");
            if (preview.DebtDelta != 0 || preview.DebtAfter != 0)
            {
                builder.Append("欠款：");
                builder.Append(preview.DebtAfter.ToString("N0", CultureInfo.InvariantCulture));
                builder.AppendLine(" 文。");
            }
            if (preview.CapacityBefore != null && preview.CapacityAfter != null)
            {
                builder.Append("格位：");
                builder.Append(preview.CapacityBefore.UsedSlots.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(preview.CapacityBefore.TotalSlots.ToString(CultureInfo.InvariantCulture));
                builder.Append(" → ");
                builder.Append(preview.CapacityAfter.UsedSlots.ToString(CultureInfo.InvariantCulture));
                builder.Append('/');
                builder.Append(preview.CapacityAfter.TotalSlots.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine();
                builder.Append("负重能力：");
                builder.Append(preview.CapacityBefore.CarryingCapacity.ToString(CultureInfo.InvariantCulture));
                builder.Append(" → ");
                builder.Append(preview.CapacityAfter.CarryingCapacity.ToString(CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        internal void Clear()
        {
            _entries.Clear();
            Refresh();
        }

        public void Dispose()
        {
            _clearButton.onClick.RemoveListener(Clear);
            _confirmButton.onClick.RemoveListener(Confirm);
            ClearViews();
            _entries.Clear();
            ConfirmRequested = null;
            _preview = null;
        }

        private void RebuildEntries()
        {
            ClearViews();
            _countText.text = _entries.Count.ToString(CultureInfo.InvariantCulture);
            _content.sizeDelta = new Vector2(0f, Math.Max(80f, _entries.Count * 138f + 6f));
            _content.anchoredPosition = Vector2.zero;
            for (int i = 0; i < _entries.Count; i++)
                CreateEntryView(_entries[i], i);
        }

        private void CreateEntryView(CartEntry entry, int index)
        {
            ItemDefinition item;
            if (!_items.TryGetItem(entry.ItemId, out item))
                return;
            bool isLong = entry.Action == TradeAction.Buy || entry.Action == TradeAction.Sell;
            bool delisted = MarketView.IsDelisted(_state, entry.ItemId);
            Image background = _ui.CreatePanel(
                _content,
                "Entry." + entry.ItemId + "." + entry.Action,
                isLong ? "card_cart_entry_long_9s" : "card_cart_entry_short_9s",
                null,
                Color.white);
            UiFactory.SetRect(
                background.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -index * 138f - 3f),
                new Vector2(0f, 130f));

            Image icon = _ui.CreateImage(
                background.transform,
                "Icon",
                MarketView.GetIconSpriteId(item),
                new Vector2(58f, 58f),
                true);
            UiFactory.SetTopLeft(icon.rectTransform, new Vector2(10f, -10f), new Vector2(58f, 58f));
            Text name = _ui.CreateText(
                background.transform,
                "Name",
                item.Name,
                19,
                Ink,
                TextAnchor.MiddleLeft,
                new Vector2(196f, 32f));
            UiFactory.SetTopLeft(name.rectTransform, new Vector2(76f, -8f), new Vector2(196f, 32f));
            Text action = _ui.CreateText(
                background.transform,
                "Action",
                MarketView.GetActionName(entry.Action) + (isLong ? " · 货契" : " · 借契"),
                14,
                Muted,
                TextAnchor.MiddleLeft,
                new Vector2(190f, 28f));
            UiFactory.SetTopLeft(action.rectTransform, new Vector2(76f, -39f), new Vector2(190f, 28f));

            Button remove = _ui.CreateButton(
                background.transform,
                "Remove",
                "×",
                null,
                22,
                new Vector2(34f, 34f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetTopRight(remove, new Vector2(-8f, -8f), new Vector2(34f, 34f));
            Button minus = _ui.CreateButton(
                background.transform,
                "Minus",
                "−",
                null,
                21,
                new Vector2(34f, 36f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetBottomLeft(minus, new Vector2(12f, 12f), new Vector2(34f, 36f));
            Text quantity = _ui.CreateText(
                background.transform,
                "Quantity",
                entry.Quantity.ToString(CultureInfo.InvariantCulture),
                18,
                Ink,
                TextAnchor.MiddleCenter,
                new Vector2(54f, 36f));
            SetBottomLeft(quantity, new Vector2(48f, 12f), new Vector2(54f, 36f));
            Button plus = _ui.CreateButton(
                background.transform,
                "Plus",
                "+",
                null,
                21,
                new Vector2(34f, 36f),
                "button_paper_icon_normal",
                "button_paper_icon_hover",
                "button_paper_icon_pressed",
                "button_paper_icon_disabled");
            SetBottomLeft(plus, new Vector2(104f, 12f), new Vector2(34f, 36f));
            Text subtotal = _ui.CreateText(
                background.transform,
                "Subtotal",
                GetSubtotal(entry),
                18,
                Ink,
                TextAnchor.MiddleRight,
                new Vector2(156f, 38f));
            SetBottomRight(subtotal, new Vector2(-12f, 12f), new Vector2(156f, 38f));

            Image wasted = _ui.CreateImage(
                background.transform,
                "DelistedWordmark",
                "wasted_delist_wordmark",
                new Vector2(150f, 76f),
                true);
            wasted.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            wasted.gameObject.SetActive(delisted);
            if (delisted)
            {
                background.color = new Color(0.63f, 0.65f, 0.64f, 1f);
                icon.color = new Color(0.45f, 0.47f, 0.45f, 0.65f);
                name.color = Muted;
                subtotal.color = Muted;
                remove.interactable = false;
                minus.interactable = false;
                plus.interactable = false;
            }
            else
            {
                minus.interactable = entry.Quantity > 1;
            }

            CartEntryView view = new CartEntryView
            {
                Root = background,
                Remove = remove,
                Minus = minus,
                Plus = plus
            };
            view.RemoveAction = delegate { RemoveEntry(entry); };
            view.MinusAction = delegate { ChangeQuantity(entry, -1); };
            view.PlusAction = delegate { ChangeQuantity(entry, 1); };
            remove.onClick.AddListener(view.RemoveAction);
            minus.onClick.AddListener(view.MinusAction);
            plus.onClick.AddListener(view.PlusAction);
            _views.Add(view);
        }

        private string GetSubtotal(CartEntry entry)
        {
            WeeklyPoint current = MarketView.GetCurrentPoint(_state, entry.ItemId);
            if (current == null)
                return "暂无报价";
            try
            {
                decimal rate = entry.Action == TradeAction.Borrow ? 0.97m : 1m;
                decimal amount = decimal.Floor((decimal)current.Price * entry.Quantity * rate);
                string sign = entry.Action == TradeAction.Buy || entry.Action == TradeAction.Return ? "− " : "+ ";
                return sign + amount.ToString("N0", CultureInfo.InvariantCulture) + " 文";
            }
            catch (OverflowException)
            {
                return "金额超限";
            }
        }

        private void ChangeQuantity(CartEntry entry, int delta)
        {
            if (!_entries.Contains(entry) || MarketView.IsDelisted(_state, entry.ItemId))
                return;
            long value = (long)entry.Quantity + delta;
            entry.Quantity = value <= 1 ? 1 : value >= int.MaxValue ? int.MaxValue : (int)value;
            Refresh();
        }

        private void RemoveEntry(CartEntry entry)
        {
            if (MarketView.IsDelisted(_state, entry.ItemId))
                return;
            _entries.Remove(entry);
            Refresh();
        }

        private CapacitySnapshot GetCurrentCapacity()
        {
            try
            {
                return _capacity.Evaluate(_state, _definitions);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private void Confirm()
        {
            if (_entries.Count > 0 && ConfirmRequested != null)
                ConfirmRequested();
        }

        private void ClearViews()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                CartEntryView view = _views[i];
                if (view.Remove != null)
                    view.Remove.onClick.RemoveListener(view.RemoveAction);
                if (view.Minus != null)
                    view.Minus.onClick.RemoveListener(view.MinusAction);
                if (view.Plus != null)
                    view.Plus.onClick.RemoveListener(view.PlusAction);
                if (view.Root != null)
                {
                    view.Root.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(view.Root.gameObject);
                }
            }
            _views.Clear();
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

        private static void SetBottomLeft(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                position,
                size);
        }

        private static void SetBottomRight(Component component, Vector2 position, Vector2 size)
        {
            UiFactory.SetRect(
                (RectTransform)component.transform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                position,
                size);
        }

        private sealed class CartEntry
        {
            internal string ItemId;
            internal TradeAction Action;
            internal int Quantity;
        }

        private sealed class CartEntryView
        {
            internal Image Root;
            internal Button Remove;
            internal Button Minus;
            internal Button Plus;
            internal UnityAction RemoveAction;
            internal UnityAction MinusAction;
            internal UnityAction PlusAction;
        }
    }
}
