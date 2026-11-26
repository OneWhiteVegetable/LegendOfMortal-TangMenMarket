using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner : MonoBehaviour
    {
        private static IEnumerator CardPointer(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            RectTransform pivot = context.Adapter.CardMotionPivot(0);
            RectTransform shadow = context.Adapter.CardHardShadow(0);
            context.Check(pivot != null && shadow != null, "Formal card MotionPivot/HardShadow is missing.");
            RuntimePreviewInteractionSample rest = context.Capture("rest", context.Now, "pointer rest", true);
            RecordCard(context, rest);
            CheckCard(context, false, "pointer rest");
            yield return context.WaitFrames(2);

            float start = context.Now;
            Pointer(context, context.Adapter.CardRoot(0), true);
            context.Check(context.Adapter.CardAnimationActive && context.Adapter.CardTweenPlaying,
                "Card PointerEnter did not create and start its formal Tween.");
            context.Adapter.BeginCardCompletionProbe();
            yield return context.WaitUntil(start, 75f);
            bool midAnimationActive = context.Adapter.CardAnimationActive;
            bool midMoved = Vector2.Distance(pivot.anchoredPosition, Vector2.zero) > 0.05f;
            RuntimePreviewInteractionSample entering = context.Record("enter-75ms", start,
                "card PointerEnter mid; state-only, no PNG blocking");
            RecordCard(context, entering);
            context.Check(midAnimationActive && midMoved,
                "Card midpoint has no active formal transition or shows no visible movement.");
            while (context.Adapter.CardAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample hover = context.Capture("hover-final", start, "PointerEnter terminal; expected 180ms", true);
            RecordCard(context, hover);
            CheckCardCompletionEvidence(context);
            CheckCard(context, true, "PointerEnter terminal");
            yield return context.WaitFrames(2);

            start = context.Now;
            Pointer(context, context.Adapter.CardRoot(0), false);
            context.Check(context.Adapter.CardAnimationActive && context.Adapter.CardTweenPlaying,
                "Card PointerExit did not create and start its formal Tween.");
            context.Adapter.BeginCardCompletionProbe();
            while (context.Adapter.CardAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample exit = context.Capture("exit-final", start, "PointerExit rest terminal", true);
            RecordCard(context, exit);
            CheckCardCompletionEvidence(context);
            CheckCard(context, false, "PointerExit terminal");
        }

        private static IEnumerator CardSelect(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            GameObject card = context.Adapter.CardRoot(0);
            EventSystem.current.SetSelectedGameObject(null);
            float start = context.Now;
            EventSystem.current.SetSelectedGameObject(card);
            yield return new WaitForEndOfFrame();
            context.Check(context.Adapter.CardAnimationActive && context.Adapter.CardTweenPlaying,
                "Card Select did not create and start its formal Tween.");
            context.Adapter.BeginCardCompletionProbe();
            while (context.Adapter.CardAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample selected = context.Capture("selected", start, "EventSystem select/focus terminal", true);
            RecordCard(context, selected);
            CheckCardCompletionEvidence(context);
            CheckCard(context, true, "Select terminal");
            yield return context.WaitFrames(2);

            start = context.Now;
            EventSystem.current.SetSelectedGameObject(null);
            yield return new WaitForEndOfFrame();
            context.Check(context.Adapter.CardAnimationActive && context.Adapter.CardTweenPlaying,
                "Card Deselect did not create and start its formal Tween.");
            context.Adapter.BeginCardCompletionProbe();
            while (context.Adapter.CardAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample deselected = context.Capture("deselected", start, "Deselect rest terminal", true);
            RecordCard(context, deselected);
            CheckCardCompletionEvidence(context);
            CheckCard(context, false, "Deselect terminal");
        }

        private static IEnumerator InventoryHover(RuntimePreviewInteractionContext context)
        {
            context.Open("inventory", false);
            yield return context.Wait(0.6f);
            GameObject slot = context.Adapter.InventorySlotRoot(0);
            var buttons = slot.GetComponentInParent<Canvas>().rootCanvas.GetComponentsInChildren<Button>();
            foreach (Button button in buttons)
            {
                if (button.name != "CloseAllLongButton" && button.name != "CloseAllShortButton") continue;
                context.Check(button.interactable && button.GetComponentInChildren<Text>().color.a >= 0.99f,
                    button.name + " retained disabled text after the entering window became interactive.");
            }
            CanvasGroup actions = context.Adapter.InventoryActionsGroup(0);
            context.Check(slot != null && actions != null, "Formal inventory slot/actions are missing.");
            RuntimePreviewInteractionSample rest = context.Capture("rest", context.Now, "inventory operation strip hidden", true);
            RecordInventory(context, rest);
            CheckInventory(context, false, false, "inventory rest");
            yield return context.WaitFrames(2);

            float start = context.Now;
            Pointer(context, slot, true);
            context.Check(context.Adapter.InventoryTweenExists && context.Adapter.InventoryAnimationActive,
                "Inventory PointerEnter did not create and start its formal Tweens.");
            context.Adapter.BeginInventoryCompletionProbe();
            while (context.Adapter.InventoryAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample hover = context.Capture("hover", start, "operation strip PointerEnter terminal; expected 160ms", true);
            RecordInventory(context, hover);
            CheckInventoryCompletionEvidence(context);
            CheckInventory(context, true, false, "inventory hover terminal");
            yield return context.WaitFrames(2);

            Button close = context.Adapter.InventoryActions(0).Find("ClosePositionButton").GetComponent<Button>();
            Pointer(context, close.gameObject, true);
            yield return context.Wait(0.2f);
            context.Capture("close-hover", context.Now, "long-position close action under the pointer", true);
            EventSystem.current.SetSelectedGameObject(close.gameObject);
            Pointer(context, close.gameObject, false);
            yield return context.Wait(0.22f);
            Color tint = close.targetGraphic.canvasRenderer.GetColor();
            context.Check(close.IsInteractable() && close.image.sprite == null && tint.r > tint.g * 1.7f,
                "Keyboard-focused long close action has no solid red face: " + tint);
            context.Capture("close-keyboard", context.Now, "keyboard focus remains on the full close action", true);
            EventSystem.current.SetSelectedGameObject(null);
            Pointer(context, slot, true);
            yield return context.Wait(0.2f);

            start = context.Now;
            Pointer(context, slot, false);
            context.Check(context.Adapter.InventoryTweenExists && context.Adapter.InventoryAnimationActive,
                "Inventory PointerExit did not create and start its formal Tweens.");
            context.Adapter.BeginInventoryCompletionProbe();
            while (context.Adapter.InventoryAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample exit = context.Capture("exit", start, "PointerExit hides operation strip", true);
            RecordInventory(context, exit);
            CheckInventoryCompletionEvidence(context);
            CheckInventory(context, false, false, "inventory exit terminal");
        }

        private static IEnumerator InventoryReadonlyInspect(RuntimePreviewInteractionContext context)
        {
            context.Open("inventory-readonly", true);
            yield return context.WaitFrames(3);
            GameObject slot = context.Adapter.InventorySlotRoot(0);
            Pointer(context, slot, true);
            yield return context.WaitFrames(2);
            RectTransform actions = context.Adapter.InventoryActions(0);
            Button inspect = actions.Find("ViewButton").GetComponent<Button>();
            Button close = actions.Find("ClosePositionButton").GetComponent<Button>();
            context.Check(inspect.IsInteractable() && !close.gameObject.activeInHierarchy,
                "Readonly slot must offer only its own quote action.");
            context.Capture("quote-only", context.Now, "single enabled quote button with no close action", true);
            Click(context, inspect.gameObject);
            context.Runner.PrepareCanvases();
            yield return context.WaitFrames(3);
            context.Check(context.Adapter.DetailWindowRootActive && context.Adapter.DetailItemId == "04",
                "Readonly quote did not open the selected holding.");
            Button back = context.Adapter.DetailBackButton;
            Button[] buttons = back.GetComponentInParent<Canvas>().rootCanvas.GetComponentsInChildren<Button>();
            foreach (Button button in buttons)
            {
                if (button.name == "BuyButton" || button.name == "SellButton" || button.name == "ReturnButton" ||
                    button.name == "BorrowButton" || button.name == "InventoryButton" || button.name == "RankingButton" ||
                    button.name == "IndexButton")
                    context.Check(!button.IsInteractable(), "Readonly quote exposed " + button.name);
            }
            context.Capture("held-quote", context.Now, "held-item chart with restricted navigation", true);
            Click(context, back.gameObject);
            context.Runner.PrepareCanvases();
            yield return context.WaitFrames(3);
            context.Check(slot.activeInHierarchy && !close.gameObject.activeInHierarchy,
                "Returning from a quote did not preserve readonly inventory.");
            context.Capture("returned", context.Now, "returned to the system inventory", true);
        }

        private static IEnumerator InventoryFocus(RuntimePreviewInteractionContext context)
        {
            context.Open("inventory", false);
            yield return context.Wait(0.6f);
            GameObject slot = context.Adapter.InventorySlotRoot(0);
            EventSystem.current.SetSelectedGameObject(null);
            float start = context.Now;
            EventSystem.current.SetSelectedGameObject(slot);
            yield return new WaitForEndOfFrame();
            context.Check(context.Adapter.InventoryTweenExists && context.Adapter.InventoryAnimationActive,
                "Inventory Select did not create and start its formal Tweens.");
            context.Adapter.BeginInventoryCompletionProbe();
            while (context.Adapter.InventoryAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample focus = context.Capture("focus", start, "focus frame and operation strip", true);
            RecordInventory(context, focus);
            CheckInventoryCompletionEvidence(context);
            CheckInventory(context, true, true, "inventory focus terminal");
            yield return context.WaitFrames(2);

            start = context.Now;
            EventSystem.current.SetSelectedGameObject(null);
            yield return new WaitForEndOfFrame();
            context.Check(context.Adapter.InventoryTweenExists && context.Adapter.InventoryAnimationActive,
                "Inventory Deselect did not create and start its formal Tweens.");
            context.Adapter.BeginInventoryCompletionProbe();
            while (context.Adapter.InventoryAnimationActive && context.ElapsedMs(start) < 900f)
                yield return new WaitForEndOfFrame();
            RuntimePreviewInteractionSample deselect = context.Capture("deselect", start, "focus exit terminal", true);
            RecordInventory(context, deselect);
            CheckInventoryCompletionEvidence(context);
            CheckInventory(context, false, false, "inventory Deselect terminal");
        }

        private static IEnumerator TooltipDelayCancel(RuntimePreviewInteractionContext context)
        {
            context.Open("detail", false);
            yield return context.Wait(0.6f);
            Button target = context.Adapter.DetailTooltipTarget;
            RectTransform tooltip = context.Adapter.TooltipRoot;
            context.Check(target != null && tooltip != null, "Formal Tooltip target/root is missing.");
            EventSystem.current.SetSelectedGameObject(null);
            Pointer(context, target.gameObject, false);
            yield return context.Wait(0.2f);
            context.Check(!tooltip.gameObject.activeSelf,
                "Tooltip was already active after physical input isolation and explicit cancellation.");

            float start = context.Now;
            Pointer(context, target.gameObject, true);
            yield return context.WaitUntil(start, 60f);
            Pointer(context, target.gameObject, false);
            yield return context.Wait(0.22f);
            context.Check(!tooltip.gameObject.activeSelf, "Tooltip appeared after its delayed PointerEnter was cancelled.");
            RuntimePreviewInteractionSample cancelled = context.Capture("cancelled", start, "150ms delay cancelled before display", true);
            RecordTooltip(context, cancelled, tooltip);
            yield return context.WaitFrames(2);

            start = context.Now;
            Pointer(context, target.gameObject, true);
            yield return context.WaitUntil(start, 70f);
            context.Check(!tooltip.gameObject.activeSelf, "Tooltip appeared before its 150ms delay.");
            while (!tooltip.gameObject.activeSelf && context.ElapsedMs(start) < 800f)
                yield return new WaitForEndOfFrame();
            float delay = context.ElapsedMs(start);
            context.Check(tooltip.gameObject.activeSelf, "Tooltip did not appear after PointerEnter.");
            CheckDuration(context, delay, 150f, "Tooltip delay");
            RuntimePreviewInteractionSample appearing = context.Record("appearing", start,
                "real delayed Tooltip; state-only so entrance timing is not blocked by PNG encoding");
            RecordTooltip(context, appearing, tooltip);
            yield return context.Wait(0.18f);
            CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
            context.Check(tooltip.gameObject.activeSelf && group != null && group.alpha >= 0.99f,
                "Tooltip did not reach its visible alpha terminal after the 120ms entrance.");
            RuntimePreviewInteractionSample visible = context.Capture("visible", start, "Tooltip visible terminal", true);
            RecordTooltip(context, visible, tooltip);
            Pointer(context, target.gameObject, false);
            yield return new WaitForEndOfFrame();
            context.Check(!tooltip.gameObject.activeSelf, "Tooltip did not hide on PointerExit.");
        }

        private static IEnumerator WindowMotion(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            context.Adapter.InstrumentWindowOpenTween();
            float start = context.Now;
            context.Check(context.Adapter.WindowTweenExists && context.Adapter.WindowAnimationActive,
                "Window open did not create and start its formal 460ms Tween.");
            RuntimePreviewInteractionSample openStart = context.Record("open-start", start, "window open start; state-only");
            RecordWindow(context, openStart);
            yield return context.WaitUntil(start, 230f);
            RuntimePreviewInteractionSample openMid = context.Record("open-mid", start, "460ms open midpoint; state-only");
            RecordWindow(context, openMid);
            CanvasGroup group = context.Adapter.WindowMotionGroup;
            context.Check(group.alpha > 0.001f, "Window open is invisible at its midpoint.");
            if (!context.Adapter.WindowAnimationActive)
                CheckWindowOpenCompletion(context);
            while (context.Adapter.WindowAnimationActive && context.ElapsedMs(start) < 1400f)
                yield return new WaitForEndOfFrame();
            float openElapsed = context.ElapsedMs(start);
            RuntimePreviewInteractionSample openFinal = context.Capture("open-final", start, "460ms open terminal", true);
            openFinal.note = "460ms Tween terminal; wall elapsedMs=" + openElapsed.ToString("0.0", CultureInfo.InvariantCulture);
            RecordWindow(context, openFinal);
            CheckWindowOpenCompletion(context);
            context.Check(context.Adapter.WindowRootActive && WindowOpenMatches(context), "Window open final pose/alpha is incorrect.");
            context.Check(!context.Adapter.WindowTweenExists || context.Adapter.WindowAnimationComplete,
                "Window open Tween exists but is paused/incomplete at the reported terminal.");
            yield return context.WaitFrames(2);

            start = context.Now;
            context.Adapter.CloseFormalUi();
            context.Check(context.Adapter.UserClosePending, "Formal CloseFromUser did not enter its animated close callback path.");
            RuntimePreviewInteractionSample closeStart = context.Record("close-start", start, "240ms close start; state-only");
            RecordWindow(context, closeStart);
            yield return context.WaitUntil(start, 120f);
            RuntimePreviewInteractionSample closeMid = context.Record("close-mid", start, "240ms close midpoint; state-only");
            RecordWindow(context, closeMid);
            context.Check(context.Adapter.WindowRootActive && context.Adapter.UserClosePending &&
                group.alpha > 0.001f && group.alpha < 0.999f,
                "Window close did not retain an intermediate visible state with a pending completion callback.");
            while (context.Adapter.UserClosePending && context.ElapsedMs(start) < 1200f)
                yield return new WaitForEndOfFrame();
            float closeElapsed = context.ElapsedMs(start);
            RuntimePreviewInteractionSample closeFinal = context.Capture("close-final", start, "240ms close terminal; intentional uniform frame allowed", true, true);
            RecordWindow(context, closeFinal);
            CheckDuration(context, closeElapsed, 240f, "window close");
            context.Check(!context.Adapter.WindowRootActive, "Window root remained active after close completion.");
            context.Check(!context.Adapter.UserClosePending, "Formal 240ms close completion callback did not finish.");
        }

        private static IEnumerator ToastStack(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            float start = context.Now;
            context.Adapter.ShowToast("第一条真实堆叠消息", 0.8f, "Neutral");
            context.Adapter.ShowToast("第二条成功消息", 0.8f, "Success");
            context.Adapter.ShowToast("第三条错误消息", 0.8f, "Error");
            List<RectTransform> roots = context.Adapter.ToastRoots();
            context.Check(roots.Count == 3, "Three consecutive Toast messages did not create three active entries.");
            yield return context.WaitUntil(start, 100f);
            RuntimePreviewInteractionSample entering = context.Record("stack-entering", start,
                "three Toast entries entering; state-only");
            RecordToast(context, entering, roots);
            yield return context.WaitUntil(start, 300f);
            roots = context.Adapter.ToastRoots();
            context.Check(roots.Count == 3, "Toast entries were replaced instead of stacked.");
            bool positionsDistinct = true;
            for (int i = 0; i < roots.Count; i++)
            {
                CanvasGroup group = context.Adapter.ToastGroup(roots[i]);
                context.Check(group != null && group.alpha >= 0.99f, "Toast " + i + " did not reach alpha=1 after 220ms.");
                for (int j = i + 1; j < roots.Count; j++)
                    positionsDistinct &= Mathf.Abs(roots[i].anchoredPosition.y - roots[j].anchoredPosition.y) >= 20f;
            }
            context.Check(positionsDistinct, "Toast stack entries overlap vertically.");
            RuntimePreviewInteractionSample stacked = context.Record("stacked", start,
                "220ms entrance terminal; three non-overlapping messages; state-only to protect cleanup timing");
            RecordToast(context, stacked, roots);
            while ((context.Adapter.ToastRoots().Count != 0 || context.Adapter.ToastStackActive) &&
                context.ElapsedMs(start) < 1800f)
                yield return new WaitForEndOfFrame();
            float cleanup = context.ElapsedMs(start);
            RuntimePreviewInteractionSample cleared = context.Capture("cleared", start, "duration plus 200ms exit cleanup", true);
            RecordToast(context, cleared, context.Adapter.ToastRoots());
            CheckDuration(context, cleanup, 1000f, "Toast cleanup");
            context.Check(context.Adapter.ToastRoots().Count == 0 && !context.Adapter.ToastStackActive,
                "Toast entries/stack were not cleaned after exit animations.");
        }

        private static IEnumerator TradeFeedbackDoubleConfirm(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            string beforeHash = context.Adapter.CurrentRunStateHash;
            RuntimePreviewTradeExpectation expected = context.Adapter.PreviewTrade();
            context.Result.expectedCashAfter = expected.CashAfter;
            context.Adapter.AssertBusinessStateUnchanged("trade Preview");
            bool created;
            int tokens;
            bool stamp;
            context.Adapter.FeedbackState(out created, out tokens, out stamp);
            context.Check(!created && tokens == 0 && !stamp, "Trade feedback existed before Preview/Execute succeeded.");
            RuntimePreviewInteractionSample preview = context.Capture("preview", context.Now, "real Preview; no cash/state mutation", true);
            preview.activeObjectCount = 0;
            yield return context.WaitFrames(2);
            Click(context, context.Adapter.CheckoutButton.gameObject);
            yield return context.Wait(0.32f);
            context.Adapter.AssertBusinessStateUnchanged("trade confirmation Modal");
            context.Adapter.FeedbackState(out created, out tokens, out stamp);
            context.Check(tokens == 0 && !stamp, "FlyToken/DealStamp appeared before Execute success.");
            context.Capture("modal", context.Now, "formal confirmation Modal; still no Execute", true);
            yield return context.WaitFrames(2);

            float start = context.Now;
            Button confirm = context.Adapter.ModalConfirmButton;
            Vector2 confirmPoint = RuntimePreviewPointerDriver.Center(confirm.gameObject);
            Click(context, confirm.gameObject);
            RuntimePreviewPointerDriver.ClickAt(context, null, confirmPoint, false);
            context.Adapter.FeedbackState(out created, out tokens, out stamp);
            context.Check(created && tokens == 0 && stamp,
                "Successful Execute did not create exactly the stamp-only formal feedback.");
            context.Check(context.Adapter.CurrentCash == expected.CashAfter,
                "Cash after double confirm differs from Preview: expected=" + expected.CashAfter + " actual=" + context.Adapter.CurrentCash);
            context.Check(context.Adapter.CurrentDebt == expected.DebtAfter,
                "Debt after double confirm differs from Preview.");
            context.Check(context.Adapter.TradeHistoryCount() == expected.ExpectedTradeHistoryCount,
                "Double confirm did not commit exactly one Preview batch: expected history=" + expected.ExpectedTradeHistoryCount +
                " actual=" + context.Adapter.TradeHistoryCount());
            context.Check(context.Adapter.CurrentRunStateHash != beforeHash,
                "Successful Execute did not change RunState.");
            int used;
            int total;
            context.Adapter.EvaluateCapacity(out used, out total);
            context.Check(used == expected.CapacityAfterUsed && total == expected.CapacityAfterTotal,
                "Committed capacity differs from Preview: expected=" + expected.CapacityAfterUsed + "/" +
                expected.CapacityAfterTotal + " actual=" + used + "/" + total);
            RuntimePreviewInteractionSample committed = context.Record("committed", start,
                "Execute succeeded; feedback created only now; state-only");
            RecordFeedback(context, committed, created, tokens, stamp);

            yield return context.WaitUntil(start, 180f);
            context.Adapter.FeedbackState(out created, out tokens, out stamp);
            context.Check(tokens == 0 && stamp, "Stamp-only feedback was missing or unexpectedly included FlyToken at mid-animation.");
            RuntimePreviewInteractionSample feedback = context.Record("feedback-180ms", start,
                "real stamp-only feedback in flight; state-only, no PNG delay");
            RecordFeedback(context, feedback, created, tokens, stamp);
            while (true)
            {
                context.Adapter.FeedbackState(out created, out tokens, out stamp);
                if (!stamp || context.ElapsedMs(start) >= 1400f)
                    break;
                yield return new WaitForEndOfFrame();
            }
            float stampCleanup = context.ElapsedMs(start);
            context.Check(!stamp, "DealStamp remained active after 650ms animation.");
            CheckDuration(context, stampCleanup, 650f, "DealStamp cleanup");
            context.Adapter.FeedbackState(out created, out tokens, out stamp);
            RuntimePreviewInteractionSample cleaned = context.Capture("feedback-cleaned", start, "pooled feedback objects inactive after both timelines", true);
            RecordFeedback(context, cleaned, created, tokens, stamp);
            context.Check(tokens == 0 && !stamp, "Feedback cleanup terminal is not inactive.");
        }

        private static IEnumerator ReducedMotion(RuntimePreviewInteractionContext context)
        {
            context.Runner.LogStep(context, "before-open");
            context.Open("market", false);
            context.Runner.LogStep(context, "after-open");
            yield return context.Wait(0.6f);
            context.Runner.LogStep(context, "before-pointer");
            Pointer(context, context.Adapter.CardRoot(0), true);
            context.Runner.LogStep(context, "before-toast");
            context.Adapter.ShowToast("减少动效终态验收", 30f, "Success");
            yield return context.Wait(0.06f);
            context.Runner.LogStep(context, "before-set-reduced");
            context.Adapter.SetReducedMotion(true);
            context.Runner.LogStep(context, "after-set-reduced");
            yield return new WaitForEndOfFrame();
            context.Check(context.Adapter.IsReducedMotion, "ReducedMotion did not become active.");
            CheckCard(context, true, "ReducedMotion card terminal");
            List<RectTransform> roots = context.Adapter.ToastRoots();
            context.Check(roots.Count == 1, "ReducedMotion Toast was not retained.");
            if (roots.Count == 1)
            {
                CanvasGroup group = context.Adapter.ToastGroup(roots[0]);
                context.Check(group != null && group.alpha >= 0.99f, "ReducedMotion Toast did not settle to alpha=1.");
            }
            context.Runner.LogStep(context, "after-terminal-checks");
            yield return context.Wait(0.35f);
            context.Runner.PrepareCanvases();
            Canvas.ForceUpdateCanvases();
            context.Runner.LogStep(context, "before-first-semantics");
            VisualSemanticSnapshot first = VisualValidation.CaptureSemantics(
                context.Adapter.FixtureId, context.Result.name, context.Width, context.Height,
                context.Adapter.CurrentRunStateHash);
            context.Runner.LogStep(context, "after-first-semantics");
            context.Runner.WriteSemanticSnapshot(context, "reduced-first", first);
            RuntimePreviewInteractionSample startSample = context.Record("reduced-start", context.Now,
                "ReducedMotion terminal before two-second stability window; state-only");
            RecordCard(context, startSample);
            yield return context.Wait(2.0f);
            context.Runner.LogStep(context, "before-second-semantics");
            VisualSemanticSnapshot second = VisualValidation.CaptureSemantics(
                context.Adapter.FixtureId, context.Result.name, context.Width, context.Height,
                context.Adapter.CurrentRunStateHash);
            context.Runner.LogStep(context, "after-second-semantics");
            context.Runner.WriteSemanticSnapshot(context, "reduced-second", second);
            context.Check(VisualValidation.StabilityFingerprint(first) == VisualValidation.StabilityFingerprint(second),
                "ReducedMotion visual state continued moving during the two-second stability window.");
            RuntimePreviewInteractionSample endSample = context.Capture("reduced-end", context.Now,
                "ReducedMotion stable terminal", true);
            RecordCard(context, endSample);
            CheckCard(context, true, "ReducedMotion two-second terminal");
            context.Runner.LogStep(context, "case-complete");
        }

        private static RuntimePreviewPointerEvidence Pointer(
            RuntimePreviewInteractionContext context,
            GameObject target,
            bool enter)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            return RuntimePreviewPointerDriver.Move(context, target, enter);
        }

        private static RuntimePreviewPointerEvidence Click(
            RuntimePreviewInteractionContext context,
            GameObject target,
            bool requireClick = true)
        {
            if (target == null && requireClick)
                throw new ArgumentNullException("target");
            return RuntimePreviewPointerDriver.Click(context, target, requireClick);
        }

        private static bool CardMatches(RuntimePreviewInteractionContext context, bool hover)
        {
            RectTransform pivot = context.Adapter.CardMotionPivot(0);
            RectTransform shadow = context.Adapter.CardHardShadow(0);
            RectTransform gold = context.Adapter.CardGoldLine(0);
            Graphic chart = context.Adapter.CardChart(0);
            if (pivot == null || shadow == null || gold == null || chart == null)
                return false;
            Vector2 expectedPosition = hover ? new Vector2(-3f, 5f) : Vector2.zero;
            Vector2 expectedShadow = hover ? new Vector2(8f, -10f) : new Vector2(4f, -5f);
            float expectedAlpha = hover ? 0.72f : 0.54f;
            float expectedGold = hover ? 1f : 0f;
            Vector2 actualShadow = shadow.parent.parent.InverseTransformVector(
                shadow.parent.TransformVector(shadow.anchoredPosition));
            return Vector2.Distance(pivot.anchoredPosition, expectedPosition) <= 0.08f &&
                Vector2.Distance(actualShadow, expectedShadow) <= 0.08f &&
                Mathf.Abs(chart.color.a - expectedAlpha) <= 0.01f &&
                Mathf.Abs(gold.localScale.y - expectedGold) <= 0.01f;
        }

        private static void CheckCard(RuntimePreviewInteractionContext context, bool hover, string label)
        {
            RectTransform pivot = context.Adapter.CardMotionPivot(0);
            RectTransform shadow = context.Adapter.CardHardShadow(0);
            string actual = pivot == null || shadow == null
                ? "missing pivot/shadow"
                : "actual pivot=" + pivot.anchoredPosition +
                    ", actualShadow=" + shadow.parent.parent.InverseTransformVector(
                        shadow.parent.TransformVector(shadow.anchoredPosition)) +
                    ", expectedShadow=" + (hover ? new Vector2(8f, -10f) : new Vector2(4f, -5f));
            context.Check(CardMatches(context, hover), label + " mismatch: expected pivot=" +
                (hover ? "(-3,5)" : "(0,0)") + ", shadow=" + (hover ? "(8,-10)" : "(4,-5)") +
                ", chartAlpha=" + (hover ? "0.72" : "0.54") + ", goldScale=" + (hover ? "1" : "0") + "; " + actual);
        }

        private static bool InventoryMatches(
            RuntimePreviewInteractionContext context,
            bool highlighted,
            bool focused)
        {
            RectTransform pivot = context.Adapter.InventorySlotPivot(0);
            RectTransform actions = context.Adapter.InventoryActions(0);
            CanvasGroup group = context.Adapter.InventoryActionsGroup(0);
            GameObject focus = context.Adapter.InventoryFocusFrame(0);
            if (pivot == null || actions == null || group == null || focus == null)
                return false;
            return Vector2.Distance(pivot.anchoredPosition, highlighted ? new Vector2(0f, 3f) : Vector2.zero) <= 0.08f &&
                Mathf.Abs(Mathf.DeltaAngle(pivot.localEulerAngles.z, highlighted ? 0.5f : 0f)) <= 0.03f &&
                Vector2.Distance(actions.anchoredPosition, highlighted ? Vector2.zero : new Vector2(0f, -40f)) <= 0.08f &&
                Mathf.Abs(group.alpha - (highlighted ? 1f : 0f)) <= 0.01f &&
                group.interactable == highlighted && group.blocksRaycasts == highlighted && focus.activeSelf == focused;
        }

        private static void CheckInventory(
            RuntimePreviewInteractionContext context,
            bool highlighted,
            bool focused,
            string label)
        {
            context.Check(InventoryMatches(context, highlighted, focused), label +
                " mismatch: expected pivot/actions/alpha/interactable/focus terminal were not reached.");
        }

        private static bool WindowOpenMatches(RuntimePreviewInteractionContext context)
        {
            RectTransform pivot = context.Adapter.WindowPivot;
            CanvasGroup group = context.Adapter.WindowMotionGroup;
            return pivot != null && group != null && group.alpha >= 0.999f &&
                Quaternion.Angle(pivot.localRotation, Quaternion.identity) <= 0.03f &&
                Vector3.Distance(pivot.localScale, Vector3.one) <= 0.001f;
        }

        private static void CheckWindowOpenCompletion(RuntimePreviewInteractionContext context)
        {
            RuntimePreviewWindowCallbackRecord complete = null;
            RuntimePreviewWindowCallbackRecord kill = null;
            for (int i = 0; i < context.Adapter.WindowCallbacks.Count; i++)
            {
                RuntimePreviewWindowCallbackRecord record = context.Adapter.WindowCallbacks[i];
                if (record.eventName == "OnComplete")
                    complete = record;
                if (record.eventName == "OnKill")
                    kill = record;
            }
            context.Check(complete != null && kill != null,
                "Window open did not produce the instrumented OnComplete/OnKill evidence.");
            if (complete == null || kill == null)
                return;
            context.Check(complete.isComplete && complete.classification == "natural-complete-candidate" &&
                complete.stackTrace.IndexOf("DG.Tweening.Tween.DoGoto", StringComparison.Ordinal) >= 0,
                "Window OnComplete was not a natural DOTween completion: " + complete.stackTrace);
            context.Check(Math.Abs(complete.elapsed - 0.46f) <= 0.005f,
                "Window OnComplete elapsed is not the formal 460ms: actual=" +
                complete.elapsed.ToString("0.000000", CultureInfo.InvariantCulture) + "s.");
            context.Check(complete.uiInstanceId == context.Adapter.UiInstanceId &&
                complete.marketInstanceId == context.Adapter.MarketInstanceId &&
                complete.uiInstanceId == kill.uiInstanceId && complete.marketInstanceId == kill.marketInstanceId,
                "Window completion crossed _ui/_market instances or used a stale callback.");
            context.Check(complete.sequenceInstanceId == kill.sequenceInstanceId &&
                complete.generation == kill.generation && complete.rootInstanceId == kill.rootInstanceId,
                "Window completion/kill changed Sequence, scope generation or root identity.");
            context.Check(!complete.uiDisposed && !complete.marketDisposed && !complete.rootDestroyed &&
                complete.scopeRootActive && complete.marketRootActive,
                "Window completed through a disposed or inactive UI/scope.");
            context.Check(kill.completeBeforeKill && kill.scopeTweenCount == 0 &&
                kill.classification == "complete-then-kill",
                "Window was killed/reset without a preceding natural OnComplete.");
            context.Check(complete.originalOnCompletePresent && complete.originalOnKillPresent &&
                kill.originalOnCompletePresent && kill.originalOnKillPresent,
                "Window original completion delegates were not preserved.");
            context.Check(Mathf.Abs(complete.timeScale - 1f) <= 0.0001f &&
                Mathf.Abs(complete.dotweenTimeScale - 1f) <= 0.0001f &&
                Mathf.Abs(complete.timeTimeScale - 1f) <= 0.0001f,
                "Window completion used a non-unit timeScale.");
        }

        private static void CheckCardCompletionEvidence(RuntimePreviewInteractionContext context)
        {
            RuntimePreviewCompletionEvidence evidence = context.Adapter.CardCompletionEvidence;
            context.Check(evidence != null, "Card did not raise its natural OnComplete callback.");
            if (evidence == null)
                return;
            context.Check(evidence.completionCount == 1 && evidence.originalOnCompletePresent,
                "Card original OnComplete delegate was not preserved.");
            context.Check(Math.Abs(evidence.duration - 0.18f) <= 0.005f &&
                Math.Abs(evidence.elapsed - 0.18f) <= 0.005f,
                "Card completion duration/elapsed mismatch: duration=" + evidence.duration +
                " elapsed=" + evidence.elapsed + ".");
            context.Check(Mathf.Abs(evidence.timeScale - 1f) <= 0.0001f,
                "Card completion used a non-unit timeScale.");
        }

        private static void CheckInventoryCompletionEvidence(RuntimePreviewInteractionContext context)
        {
            RuntimePreviewCompletionEvidence evidence = context.Adapter.InventoryCompletionEvidence;
            context.Check(evidence != null, "Inventory did not complete all four formal Tweens.");
            if (evidence == null)
                return;
            context.Check(evidence.completionCount == 4,
                "Inventory completion count mismatch: expected 4, actual=" + evidence.completionCount + ".");
            context.Check(Math.Abs(evidence.duration - 0.16f) <= 0.005f &&
                Math.Abs(evidence.elapsed - 0.16f) <= 0.005f,
                "Inventory longest duration/elapsed mismatch: duration=" + evidence.duration +
                " elapsed=" + evidence.elapsed + ".");
            context.Check(Mathf.Abs(evidence.timeScale - 1f) <= 0.0001f,
                "Inventory completion used a non-unit timeScale.");
        }

        private static void CheckDuration(
            RuntimePreviewInteractionContext context,
            float actualMs,
            float expectedMs,
            string label)
        {
            float tolerance = Math.Max(90f, expectedMs * 0.2f);
            context.Check(Math.Abs(actualMs - expectedMs) <= tolerance,
                label + " real-frame duration mismatch: expected=" + expectedMs.ToString(CultureInfo.InvariantCulture) +
                "ms actual=" + actualMs.ToString("0.0", CultureInfo.InvariantCulture) +
                "ms tolerance=" + tolerance.ToString("0.0", CultureInfo.InvariantCulture) + "ms.");
        }

        private static void RecordCard(RuntimePreviewInteractionContext context, RuntimePreviewInteractionSample sample)
        {
            RectTransform pivot = context.Adapter.CardMotionPivot(0);
            RectTransform shadow = context.Adapter.CardHardShadow(0);
            RectTransform gold = context.Adapter.CardGoldLine(0);
            Graphic chart = context.Adapter.CardChart(0);
            sample.active = context.Adapter.CardRoot(0).activeSelf;
            sample.position = pivot.anchoredPosition;
            sample.rotation = pivot.localEulerAngles;
            sample.scale = pivot.localScale;
            sample.shadow = shadow.parent.parent.InverseTransformVector(
                shadow.parent.TransformVector(shadow.anchoredPosition));
            sample.chartAlpha = chart.color.a;
            sample.goldScale = gold.localScale.y;
        }

        private static void RecordInventory(RuntimePreviewInteractionContext context, RuntimePreviewInteractionSample sample)
        {
            RectTransform pivot = context.Adapter.InventorySlotPivot(0);
            RectTransform actions = context.Adapter.InventoryActions(0);
            CanvasGroup group = context.Adapter.InventoryActionsGroup(0);
            sample.active = context.Adapter.InventorySlotRoot(0).activeSelf;
            sample.position = pivot.anchoredPosition;
            sample.rotation = pivot.localEulerAngles;
            sample.scale = pivot.localScale;
            sample.shadow = actions.anchoredPosition;
            sample.alpha = group.alpha;
        }

        private static void RecordTooltip(
            RuntimePreviewInteractionContext context,
            RuntimePreviewInteractionSample sample,
            RectTransform tooltip)
        {
            sample.active = tooltip != null && tooltip.gameObject.activeSelf;
            if (tooltip == null)
                return;
            CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
            sample.alpha = group == null ? 0f : group.alpha;
            sample.position = tooltip.anchoredPosition;
            sample.scale = tooltip.localScale;
        }

        private static void RecordWindow(RuntimePreviewInteractionContext context, RuntimePreviewInteractionSample sample)
        {
            RectTransform pivot = context.Adapter.WindowPivot;
            CanvasGroup group = context.Adapter.WindowMotionGroup;
            sample.active = context.Adapter.WindowRootActive;
            if (pivot != null)
            {
                sample.position = pivot.anchoredPosition;
                sample.rotation = pivot.localEulerAngles;
                sample.scale = pivot.localScale;
            }
            sample.alpha = group == null ? 0f : group.alpha;
            sample.activeObjectCount = context.Adapter.WindowTweenCount;
            sample.tweens = context.Adapter.WindowTweenDiagnostics();
        }

        private static void RecordToast(
            RuntimePreviewInteractionContext context,
            RuntimePreviewInteractionSample sample,
            List<RectTransform> roots)
        {
            sample.activeObjectCount = roots.Count;
            sample.active = context.Adapter.ToastStackActive;
            if (roots.Count > 0)
            {
                sample.position = roots[0].anchoredPosition;
                CanvasGroup group = context.Adapter.ToastGroup(roots[0]);
                sample.alpha = group == null ? 0f : group.alpha;
            }
        }

        private static void RecordFeedback(
            RuntimePreviewInteractionContext context,
            RuntimePreviewInteractionSample sample,
            bool created,
            int tokens,
            bool stamp)
        {
            sample.active = created;
            sample.activeObjectCount = tokens + (stamp ? 1 : 0);
        }
    }
}
