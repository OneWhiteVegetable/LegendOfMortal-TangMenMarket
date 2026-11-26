using System;
using System.Collections.Generic;
using UnityEngine;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Trading;
using XingShangMod.UI.Infrastructure;

namespace XingShangMod.UI.Shared
{
    internal sealed class TradeConfirmation
    {
        private readonly ModApplication _application;
        private readonly UiFrontendServices _services;
        private string _previewFailure;

        internal TradeConfirmation(ModApplication application, UiFrontendServices services)
        {
            _application = application;
            _services = services;
            _services.Overlays.RegisterItems(application.Items);
        }

        internal void Present(IList<TradeOrder> orders, string title, Action<TradeResult> succeeded)
        {
            List<TradeOrder> copy = CopyOrders(orders);
            if (copy.Count == 0)
            {
                _services.Overlays.ShowToast("契匣为空，尚无需要结算的货契。", 2.8f, UiToastKind.Error);
                return;
            }
            TradeResult preview = Preview(copy);
            if (preview == null || !preview.Success)
            {
                ShowPreviewError(preview);
                return;
            }

            ShowQuote(title, copy, preview, succeeded, false);
        }

        private void ShowQuote(string title, List<TradeOrder> orders, TradeResult preview,
            Action<TradeResult> succeeded, bool changed)
        {
            UiModalContent content = BuildContent(title, orders, preview);
            if (changed)
                content.Subtitle = "行情或账目已变，请核对更新后的货契再落印";
            content.Confirmed = delegate { RevalidateAndExecute(title, orders, preview, succeeded); };
            _services.Overlays.ShowModal(content);
        }

        private void RevalidateAndExecute(string title, List<TradeOrder> orders, TradeResult accepted, Action<TradeResult> succeeded)
        {
            TradeResult preview = Preview(orders);
            if (preview == null || !preview.Success)
            {
                ShowPreviewError(preview);
                return;
            }
            if (!SameQuote(accepted, preview))
            {
                ShowQuote(title, orders, preview, succeeded, true);
                return;
            }
            TradeResult result = _application.ExecuteTrade(orders);
            if (result == null || !result.Success)
            {
                _services.Overlays.ShowToast(
                    UiFormatting.TradeErrorMessage(result, _application.Items), 4f, UiToastKind.Error);
                return;
            }
            _services.Overlays.ShowToast("契约已落印，货囊与银两账目已更新。", 2.4f, UiToastKind.Success);
            PlaySuccessfulFeedback(result);
            if (succeeded != null)
                succeeded(result);
        }

        private static bool SameQuote(TradeResult previous, TradeResult current)
        {
            if (previous.CashAfter != current.CashAfter || previous.DebtAfter != current.DebtAfter ||
                previous.CashDelta != current.CashDelta || previous.DebtDelta != current.DebtDelta ||
                previous.CapacityAfter.UsedSlots != current.CapacityAfter.UsedSlots ||
                previous.CapacityAfter.TotalSlots != current.CapacityAfter.TotalSlots ||
                previous.CapacityAfter.CarryingCapacity != current.CapacityAfter.CarryingCapacity ||
                previous.Records.Count != current.Records.Count)
                return false;
            for (int i = 0; i < previous.Records.Count; i++)
            {
                TradeRecord old = previous.Records[i];
                TradeRecord next = current.Records[i];
                if (old.ItemId != next.ItemId || old.Action != next.Action || old.Quantity != next.Quantity ||
                    old.UnitPrice != next.UnitPrice || old.CashDelta != next.CashDelta ||
                    old.DebtDelta != next.DebtDelta || old.MarketWeekIndex != next.MarketWeekIndex)
                    return false;
            }
            return true;
        }

        private TradeResult Preview(IList<TradeOrder> orders)
        {
            _previewFailure = null;
            try
            {
                return _application.Trading == null ? null : _application.Trading.Preview(orders);
            }
            catch (Exception exception)
            {
                _previewFailure = "预览失败：" + exception.Message;
                return null;
            }
        }

        private void ShowPreviewError(TradeResult preview)
        {
            string message = preview == null && !string.IsNullOrEmpty(_previewFailure)
                ? _previewFailure
                : UiFormatting.TradeErrorMessage(preview, _application.Items);
            _services.Overlays.ShowToast(message, 4f, UiToastKind.Error);
        }

        private void PlaySuccessfulFeedback(TradeResult result)
        {
            if (result.Records == null || result.Records.Count == 0)
                return;
            bool closing = true;
            bool hasRecord = false;
            List<Sprite> icons = new List<Sprite>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < result.Records.Count; i++)
            {
                TradeRecord record = result.Records[i];
                if (record == null)
                    continue;
                hasRecord = true;
                if (record.Action != TradeActionNames.Sell && record.Action != TradeActionNames.Return &&
                    record.Action != TradeActionNames.Expire)
                    closing = false;
                ItemDefinition item;
                if (seen.Add(record.ItemId) && _application.Items.TryGetItem(record.ItemId, out item))
                    icons.Add(_services.Assets.GetSprite(MarketQuery.IconKey(item)));
            }
            if (hasRecord)
                _services.Overlays.ShowTradeFeedback(icons, closing);
        }

        private UiModalContent BuildContent(string title, IList<TradeOrder> orders, TradeResult preview)
        {
            UiModalContent content = new UiModalContent
            {
                Title = title,
                Subtitle = "核对货契，确认时重新校验，整份同成同败",
                TotalCaption = preview.CashDelta == 0 ? "本次银两收支相抵" : preview.CashDelta > 0 ? "本次预计净收" : "本次预计净付",
                Total = UiFormatting.SignedMoney(preview.CashDelta) + " 文",
                ConfirmLabel = "确认落印",
                CancelLabel = "再核一遍",
                Explanation = BuildExplanation(preview)
            };
            int count = preview.Records == null ? 0 : preview.Records.Count;
            for (int i = 0; i < count; i++)
            {
                TradeRecord record = preview.Records[i];
                ItemDefinition item;
                string name = _application.Items.TryGetItem(record.ItemId, out item) ? item.Name : record.ItemId;
                bool outgoing = record.Action == TradeActionNames.Buy || record.Action == TradeActionNames.Return ||
                    record.Action == TradeActionNames.Expire;
                content.Lines.Add(new UiModalLine
                {
                    Label = UiFormatting.RecordActionName(record.Action) + " · " + name,
                    Quantity = "× " + record.Quantity,
                    Amount = ((decimal)record.CashDelta - record.DebtDelta).ToString("+#,0;-#,0;0", System.Globalization.CultureInfo.InvariantCulture) + " 文",
                    Accent = outgoing ? UiFormatting.RedDark : UiFormatting.GreenDark
                });
            }
            return content;
        }

        private string BuildExplanation(TradeResult preview)
        {
            string text = UiFormatting.SettlementBalance(preview) + "。";
            if (preview.DebtDelta < 0)
                text += " 本次抵还欠款 " + (-(decimal)preview.DebtDelta).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " 文。";
            if (preview.CapacityBefore != null && preview.CapacityAfter != null)
            {
                text += " 货囊 " + UiFormatting.CapacitySlots(preview.CapacityBefore) +
                    " → " + UiFormatting.CapacitySlots(preview.CapacityAfter) + "，每格负重 " +
                    preview.CapacityAfter.CarryingCapacity + "。";
            }
            return text + (_application.Trading.NewBorrowClosed
                ? " 已停开新借契。旧契可提前归还，到期按市价买回。"
                : " 借货按现价 97% 实收，24 个市场周到期。") + "退市仓位禁止交易与平仓。";
        }

        private static List<TradeOrder> CopyOrders(IList<TradeOrder> orders)
        {
            List<TradeOrder> result = new List<TradeOrder>();
            if (orders == null)
                return result;
            for (int i = 0; i < orders.Count; i++)
            {
                TradeOrder order = orders[i];
                if (order != null && order.Quantity > 0 && !string.IsNullOrEmpty(order.ItemId))
                    result.Add(new TradeOrder(order.Action, order.ItemId, order.Quantity));
            }
            return result;
        }
    }
}
