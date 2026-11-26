using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner
    {
        private static IEnumerator ChartControls(RuntimePreviewInteractionContext context)
        {
            foreach (string page in new[] { "detail", "index", "assets" })
            {
                context.Open(page, true);
                yield return context.WaitFrames(4);
                Component chart = page == "assets" ? context.Adapter.AssetsChart : context.Adapter.DetailChart;
                Button reset = page == "assets" ? context.Adapter.AssetsChartReset : context.Adapter.DetailButton("ResetChart");
                GameObject target = chart.gameObject;
                Vector2 at = RuntimePreviewPointerDriver.Center(target);
                PointerEventData pointer = new PointerEventData(EventSystem.current) { position = at, pointerId = -1 };
                List<RaycastResult> hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject) != target)
                {
                    context.Fail(page + " chart missed the physical center raycast");
                    continue;
                }
                pointer.pointerPressRaycast = pointer.pointerCurrentRaycast = hits[0];
                int initial = (int)chart.GetType().GetProperty("VisibleRange").GetValue(chart, null);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                int selected = (int)chart.GetType().GetProperty("SelectedIndex").GetValue(chart, null);
                context.Check(selected >= 0 && selected <= context.Adapter.DetailCurrentWeek, page + " hover selected no historical observation");
                pointer.scrollDelta = new Vector2(0f, 2f);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.scrollHandler);
                int zoomed = (int)chart.GetType().GetProperty("VisibleRange").GetValue(chart, null);
                context.Check(zoomed < initial, page + " wheel did not zoom in");
                float before = (float)chart.GetType().GetProperty("VisibleStart").GetValue(chart, null);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
                pointer.position = at + Vector2.right * 95f;
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
                float after = (float)chart.GetType().GetProperty("VisibleStart").GetValue(chart, null);
                context.Check(after < before, page + " drag did not move toward earlier dates");
                context.Capture(page + "-pan", context.Now, "Wheel zoom and horizontal drag from a raycast hit", true);
                Click(context, reset.gameObject);
                yield return context.WaitFrames(2);
                context.Check((int)chart.GetType().GetProperty("VisibleRange").GetValue(chart, null) == initial,
                    page + " reset failed to restore the initial range");
            }
        }

        private static IEnumerator MastheadAndLedger(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            Button index = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "IndexButton");
            RectTransform underline = (RectTransform)index.transform.Find("IndexNameText/FocusUnderline");
            foreach (string part in new[] { "IndexNameText", "IndexPriceText", "IndexChangeText", "IndexChartHost" })
            {
                Vector2 point = RuntimePreviewPointerDriver.Center(index.transform.Find(part).gameObject);
                RuntimePreviewPointerDriver.MoveAt(context, index.gameObject, point, true);
                yield return context.Wait(0.25f);
                context.Check(underline.rect.width > 20f, "Index part has no hover underline: " + part);
            }
            context.Check(context.Adapter.TooltipRoot.gameObject.activeSelf, "Index hover has no tooltip.");
            context.Capture("index-hover", context.Now, "Index chart, quote and name share one action", true);
            Pointer(context, index.gameObject, false);
            yield return context.Wait(0.25f);
            context.Check(underline.rect.width < 0.01f, "Index underline stayed visible after exit.");
            EventSystem.current.SetSelectedGameObject(index.gameObject);
            yield return context.Wait(0.25f);
            context.Check(underline.rect.width > 20f, "Keyboard focus has no underline.");
            EventSystem.current.SetSelectedGameObject(null);

            Button checkout = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "CheckoutButton");
            EventSystem.current.SetSelectedGameObject(checkout.gameObject);
            Click(context, checkout.gameObject);
            context.Runner.PrepareCanvases();
            yield return context.Wait(0.45f);
            Button confirm = context.Adapter.ModalConfirmButton;
            Button cancel = confirm.transform.parent.Find("CancelButton").GetComponent<Button>();
            RectTransform dialog = (RectTransform)confirm.transform.parent.parent;
            float density = context.Width >= 2304 && context.Height >= 1296 ? 1.5f : 1f;
            context.Check(Mathf.Abs(dialog.rect.width - 600f) < 1f &&
                Mathf.Abs(dialog.GetComponentInParent<Canvas>().rootCanvas.scaleFactor - density) < .01f,
                "Ledger did not match the prototype width at the current screen density.");
            context.Check(Mathf.Abs(confirm.targetGraphic.rectTransform.rect.height -
                cancel.targetGraphic.rectTransform.rect.height) < .1f &&
                confirm.targetGraphic.rectTransform.rect.height >= 37f, "The two painted faces must be full height.");
            context.Capture("ledger", context.Now, "Two full-height faces and a signed cash total", true);
            Button close = dialog.Find("Header/CloseButton").GetComponent<Button>();
            Pointer(context, close.gameObject, true);
            yield return context.Wait(.2f);
            Color glyph = close.transform.Find("Icon").GetComponent<Image>().color;
            context.Check(glyph.r > .94f && glyph.g > .94f && glyph.b > .94f,
                "Confirmation close glyph did not turn white on hover.");
            context.Capture("ledger-close", context.Now, "Close control keeps its white cross", true);
            Click(context, cancel.gameObject);
            yield return context.WaitFrames(2);
            context.Check(EventSystem.current.currentSelectedGameObject == checkout.gameObject,
                "Cancel did not restore focus to the invoking control.");

            long originalCash = context.Adapter.CurrentCash;
            int originalTrades = context.Adapter.TradeHistoryCount();
            context.Adapter.ClickCheckout();
            context.Adapter.AdjustFixtureWallet(10);
            context.Adapter.ClickModalConfirmTwice();
            context.Check(context.Adapter.CurrentCash == originalCash + 10 && context.Adapter.TradeHistoryCount() == originalTrades,
                "A changed quote was executed without a new confirmation.");
            confirm = context.Adapter.ModalConfirmButton;
            context.Check(confirm != null, "Updated quote was not offered for review.");
            confirm.transform.parent.Find("CancelButton").GetComponent<Button>().onClick.Invoke();
            context.Adapter.AdjustFixtureWallet(-10);

            context.Adapter.OpenLongLedger();
            context.Runner.PrepareCanvases();
            yield return context.Wait(.45f);
            confirm = context.Adapter.ModalConfirmButton;
            dialog = (RectTransform)confirm.transform.parent.parent;
            ScrollRect scroll = dialog.GetComponentInChildren<ScrollRect>();
            context.Check(scroll.content.rect.height > scroll.viewport.rect.height + 30f,
                "Long order did not get a scrollable body.");
            scroll.verticalNormalizedPosition = 0f;
            yield return context.WaitFrames(2);
            context.Check(scroll.verticalNormalizedPosition < .015f, "Last ledger line is unreachable.");
            context.Capture("long-ledger-end", context.Now, "Last order and fixed footer", true);
            Click(context, dialog.Find("Header/CloseButton").gameObject);
            yield return context.WaitFrames(2);
        }
    }
}
