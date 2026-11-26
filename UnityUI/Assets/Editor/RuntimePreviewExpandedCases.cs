using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    public sealed partial class RuntimePreviewInteractionRunner : MonoBehaviour
    {
        private static IEnumerator RankingContinuous(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            context.Adapter.ShowRanking();
            yield return context.Wait(0.25f);
            ScrollRect scroll = context.Adapter.RankingScroll;
            context.Check(scroll != null && scroll.content == context.Adapter.RankingContent,
                "Ranking is not backed by the single formal ScrollRect/content.");
            context.Check(context.Adapter.RankingRowCount == 54,
                "Ranking must bind all 54 appeared items in one continuous list, not one page.");
            List<string> ids = context.Adapter.RankingItemIds();
            context.Check(ids.Count == 54 && new HashSet<string>(ids).Count == 54,
                "Ranking rows do not contain 54 unique appeared item IDs.");
            context.Check(!context.Adapter.CartRailActive, "Full-width ranking still shows CartRail.");
            context.Check(!context.Adapter.RankingPagerActive, "Full-width ranking still exposes pagination controls.");
            context.Check(context.Adapter.GoodsArea.rect.width > 0f &&
                context.Adapter.RankingRoot.rect.width >= context.Adapter.GoodsArea.rect.width * 0.95f,
                "Ranking root does not occupy the full GoodsArea width.");
            RequireSorted(context, context.Adapter.RankingReturns(), false, "default cumulative return");
            context.Capture("ranking-top", context.Now, "all 54 items in one full-width ScrollRect", true);

            context.Adapter.ClickRankingSort();
            yield return context.Wait(0.15f);
            context.Check(context.Adapter.RankingAscending, "Ranking sort did not enter ascending mode.");
            RequireSorted(context, context.Adapter.RankingReturns(), true, "ascending cumulative return");
            float before = scroll.verticalNormalizedPosition;
            PointerEventData wheel = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -40f) };
            ExecuteEvents.Execute<IScrollHandler>(scroll.gameObject, wheel, ExecuteEvents.scrollHandler);
            yield return context.Wait(0.2f);
            context.Check(scroll.verticalNormalizedPosition < before,
                "Continuous ranking did not scroll within the same ScrollRect.");
            context.Capture("ranking-scrolled", context.Now, "same ScrollRect after sorting and continuous scrolling", true);

            scroll.StopMovement();
            float scrolled = scroll.verticalNormalizedPosition;
            context.Adapter.ClickRankingRow("01");
            context.Runner.PrepareCanvases();
            yield return context.Wait(0.6f);
            context.Check(context.Adapter.DetailItemId == "01",
                "Ranking row did not navigate to the corresponding real item detail.");
            context.Capture("ranking-detail", context.Now, "ranking to real detail navigation", true);
            GameObject back = context.Adapter.DetailBackButton.gameObject;
            Transform paper = back.transform.Find("BrushPaper");
            context.Check(paper != null && paper.localScale.x < 0.01f, "Detail return arrow must start with its red paper folded.");
            Pointer(context, back, true);
            float began = context.Now;
            bool unfolded = false;
            while (paper != null && context.Now - began < 0.6f && paper.localScale.x < 0.999f)
            {
                yield return new WaitForEndOfFrame();
                if (!unfolded && paper.localScale.x > 0.001f && paper.localScale.x < 0.999f)
                {
                    unfolded = true;
                    context.Capture("detail-back-unfolding", began, "red paper scaleX=" + paper.localScale.x, true);
                }
            }
            float deadline = context.Now + 2f;
            while (paper != null && paper.localScale.x < 0.999f && context.Now < deadline)
                yield return new WaitForEndOfFrame();
            context.Check(unfolded && paper != null && paper.localScale.x >= 0.999f,
                "Return arrow expansion ended at " + (paper == null ? -1f : paper.localScale.x) + ", intermediate observed=" + unfolded);
            context.Capture("detail-back-open", began, "red paper scaleX=" + (paper == null ? -1f : paper.localScale.x), true);
            Pointer(context, back, false);
            yield return context.Wait(0.25f);
            yield return context.WaitFrames(6);
            context.Check(paper != null && paper.localScale.x <= 0.001f, "Return arrow failed to retract on pointer exit.");
            Click(context, back);
            context.Runner.PrepareCanvases();
            yield return context.Wait(0.6f);
            context.Check(context.Adapter.RankingRoot.gameObject.activeInHierarchy && context.Adapter.RankingAscending,
                "Returning from detail lost ranking mode or sorting.");
            context.Check(Mathf.Abs(scroll.verticalNormalizedPosition - scrolled) < 0.003f,
                "Returning from detail lost scroll position: " + scrolled + " -> " + scroll.verticalNormalizedPosition);
            context.Capture("ranking-restored", context.Now, "returned to the same sorted and scrolled ranking", true);
        }

        private static IEnumerator CapacityGuard(RuntimePreviewInteractionContext context)
        {
            context.Adapter.SetItemStackOfFour("01");
            context.Open("market-empty", true);
            yield return context.Wait(0.2f);
            Button plus = context.Adapter.CardActionButton(0, "QuantityIncrement");
            for (int i = 0; i < 4; i++)
                Click(context, plus.gameObject);
            context.Check(context.Adapter.FirstCardQuantity == 5, "Two free slots should allow a five-piece draft.");
            Click(context, context.Adapter.CardActionButton(0, "Buy").gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.CartEntryCount == 1 && context.Adapter.FirstCartQuantity == 5,
                "Two displayed contracts must retain a single economic order of five.");
            context.Check(context.Adapter.CartRowQuantity(0) == 4 && context.Adapter.CartRowQuantity(1) == 1,
                "Stack limit four must display five pieces as 4 + 1.");
            context.Check(!context.Adapter.CartIncrementAvailable, "The four-piece row must stop at its own stack limit.");
            context.Check(context.Adapter.CartRowButton(1, "QuantityIncrement").IsInteractable(),
                "A one-piece partial row should accept another piece.");
            context.Check(context.Adapter.FirstCardQuantity == 3 && !plus.IsInteractable(),
                "After five enter the cart, only three more fit; the card draft and + must refresh.");
            context.Check(!context.Adapter.CardActionButton(0, "Borrow").IsInteractable(),
                "A pending buy must block simultaneous borrowing of the same item.");
            RuntimePreviewTradeExpectation expected = context.Adapter.PreviewTrade();
            context.Check(expected.CapacityAfterUsed == 4 && expected.CapacityAfterTotal == 4,
                "Two stacked contracts should fill the last two real slots.");
            context.Capture("split-four-plus-one", context.Now, "four and one on two paper contracts; one settlement order", true);

            Click(context, context.Adapter.CartRowButton(1, "QuantityDecrement").gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.FirstCartQuantity == 4 && plus.IsInteractable(),
                "Decrement at one must remove that row, freeing one slot immediately.");
            Click(context, context.Adapter.CardActionButton(1, "Borrow").gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.CartEntryCount == 2 && !plus.IsInteractable() &&
                !context.Adapter.CardActionButton(0, "Buy").IsInteractable(),
                "Borrowing a different item must consume the recovered slot and disable further opening buys.");
            context.Check(context.Adapter.CheckoutInteractable, "An exactly-full mixed cart must remain executable.");
            context.Capture("fully-allocated", context.Now, "cash and mixed directions retained; no spare shared slot", true);

            context.Adapter.ClearToasts();
            context.Adapter.AddFromDetail("01", "Buy", 1, null);
            context.Check(context.Adapter.FirstCartQuantity == 4 && context.Adapter.ToastMessages().Count == 1,
                "Direct addition must also reject an overflowing order without mutating the cart.");
            Click(context, context.Adapter.CardActionButton(0, "Open").gameObject);
            yield return context.Wait(0.25f);
            context.Check(context.Adapter.DetailItemId == "01" && !context.Adapter.DetailButton("QuantityIncrement").IsInteractable() &&
                !context.Adapter.DetailButton("Buy").IsInteractable() && !context.Adapter.DetailButton("Borrow").IsInteractable(),
                "Detail controls must observe the same full cart and existing positions as the market card.");
            context.Capture("detail-at-capacity", context.Now, "the detail footer shares the market card limit", true);

            context.Adapter.SetItemStackOfFour("04");
            context.Open("market-empty", true);
            context.Adapter.AddFromDetail("04", "Buy", 5, null);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.CartEntryCount == 1 && context.Adapter.CartRowQuantity(0) == 2 &&
                context.Adapter.CartRowQuantity(1) == 3,
                "Six pieces already held at four per slot must accept two in the open stack and three in a new slot.");
            Click(context, context.Adapter.CartRowButton(0, "Remove").gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.FirstCartQuantity == 3, "Remove must discard just the two displayed pieces.");
            context.Capture("existing-partial-stack", context.Now, "only the selected contract was removed", true);

            context.Adapter.PrepareLargeBackpack();
            context.Open("market-empty", true);
            context.Adapter.AddFromDetail("01", "Borrow", 25, null);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.CartEntryCount == 1 && context.Adapter.VisibleCartRows == 24,
                "A large backpack must create a bounded first page without splitting the borrowing order.");
            Button nextPage = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "SettledNextButton");
            Click(context, nextPage.gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.VisibleCartRows == 1 && context.Adapter.CartRowQuantity(0) == 1,
                "Final pending page must contain only the 25th contract.");
            Click(context, context.Adapter.CartDecrementButton.gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.FirstCartQuantity == 24 && context.Adapter.VisibleCartRows == 24 &&
                !nextPage.gameObject.activeInHierarchy,
                "Removing the only last-page row must return to the first page and hide its pager.");
            IEnumerator hostInput = context.Adapter.CheckHostModuleReplacement(context.Check);
            while (hostInput.MoveNext())
                yield return hostInput.Current;
        }

        private static IEnumerator HistoryAdvance(RuntimePreviewInteractionContext context)
        {
            context.Adapter.ConfigureAdvancementFixture();
            context.Open("detail", false);
            yield return context.Wait(0.6f);
            Component chartComponent = context.Adapter.DetailChart;
            context.Check(chartComponent != null, "Detail chart is not the formal WeeklyChartGraphic.");
            context.Check(context.Adapter.DetailHistoryCount == 78, "Advancement fixture must begin with 78 visible prices.");
            float initialStart = ChartFloat(chartComponent, "VisibleStart");
            context.Check(Math.Abs(initialStart - 52f) <= 0.01f, "Detail chart did not begin at visible start 52.");
            PanChart(context, chartComponent);
            float userStart = ChartFloat(chartComponent, "VisibleStart");
            float userRange = ChartFloat(chartComponent, "VisibleRange");
            float userZoom = ChartFloat(chartComponent, "YZoomFactor");
            context.Check(userStart < initialStart && userStart >= 0f,
                "Real chart drag did not move the user viewport away from the latest week.");
            context.Capture("before-advance", context.Now, "user-panned viewport before real MarketEngine week advance", true);

            long cashBefore = context.Adapter.CurrentCash;
            context.Result.expectedCashAfter = cashBefore;
            string stateBefore = context.Adapter.CurrentRunStateHash;
            context.Adapter.AdvanceRealWeekAndRefresh();
            context.Check(context.Adapter.DetailCurrentWeek == 78, "Market clock did not advance to week 78.");
            context.Check(context.Adapter.DetailHistoryCount == 79,
                "Real MarketEngine advance must append a new price, leaving 79 visible points.");
            context.Check(ChartFloat(chartComponent, "TotalPointCount") == 79f,
                "Chart did not ingest the real advanced history.");
            context.Check(ChartFloat(chartComponent, "VisiblePointCount") >= 2f,
                "Advanced chart contains fewer than two visible points.");
            context.Check(Math.Abs(ChartFloat(chartComponent, "VisibleStart") - userStart) <= 0.01f,
                "Real week advance reset or shifted the user's visible start.");
            context.Check(Math.Abs(ChartFloat(chartComponent, "VisibleRange") - userRange) <= 0.01f,
                "Real week advance changed the user's visible range.");
            context.Check(Math.Abs(ChartFloat(chartComponent, "YZoomFactor") - userZoom) <= 0.0001f,
                "Real week advance changed the user's price zoom.");
            context.Check(context.Adapter.CurrentCash == cashBefore, "Real week advance changed cash without a trade.");
            context.Check(context.Adapter.CurrentRunStateHash != stateBefore, "Real week advance did not update RunState.");
            context.Capture("after-advance", context.Now, "79 points; user start/range/zoom preserved", true);
        }

        private static IEnumerator AddToCartFlight(RuntimePreviewInteractionContext context)
        {
            context.Open("capacity-empty", false);
            yield return context.Wait(0.6f);
            float start = context.Now;
            int startFrame = Time.frameCount;
            Click(context, context.Adapter.CardActionButton(0, "Buy").gameObject);
            context.Adapter.FeedbackState(out bool created, out int tokens, out bool stamp);
            context.Check(created && tokens > 0 && !stamp,
                "Source add button did not create the real FlyToken without a settlement stamp.");
            List<RuntimePreviewTokenDiagnostic> diagnostics = context.Adapter.TokenDiagnostics();
            context.Check(diagnostics.Count > 0, "FlyToken Tween diagnostics are missing after source add.");
            Vector2 initial = diagnostics.Count == 0 ? Vector2.zero : diagnostics[0].anchoredPosition;
            while (context.ElapsedMs(start) < 250f)
            {
                diagnostics = context.Adapter.TokenDiagnostics();
                if (diagnostics.Count > 0 && diagnostics[0].position > 0f &&
                    diagnostics[0].position < diagnostics[0].duration &&
                    Vector2.Distance(initial, diagnostics[0].anchoredPosition) > 0.001f)
                    break;
                yield return null;
            }
            diagnostics = context.Adapter.TokenDiagnostics();
            context.Check(diagnostics.Count > 0 && diagnostics[0].position > 0f &&
                diagnostics[0].position < diagnostics[0].duration,
                "No real in-flight Tween progress was available for the mid-frame PNG.");
            RuntimePreviewInteractionSample mid = context.Capture("source-to-cart-midframe", start,
                "actual source-button to CartRail flight PNG captured while 0 < position < duration");
            if (diagnostics.Count > 0)
            {
                mid.activeObjectCount = diagnostics.Count;
                mid.position = diagnostics[0].anchoredPosition;
                mid.scale = diagnostics[0].scale;
                mid.alpha = diagnostics[0].alpha;
                mid.note += "; tweenPosition=" + diagnostics[0].position.ToString("0.000000", CultureInfo.InvariantCulture) +
                    "/" + diagnostics[0].duration.ToString("0.000000", CultureInfo.InvariantCulture);
            }
            // Capturing and encoding the mid-flight frame synchronously pauses the Editor's update loop.
            float cleanupStart = context.Now;
            while (context.ElapsedMs(cleanupStart) < 4000f)
            {
                context.Adapter.FeedbackState(out created, out tokens, out stamp);
                if (tokens == 0)
                    break;
                yield return null;
            }
            context.Adapter.FeedbackState(out created, out tokens, out stamp);
            diagnostics = context.Adapter.TokenDiagnostics();
            string cleanupNote = "activeTokens=" + tokens + "; diagnostics=" + diagnostics.Count +
                "; runInBackground=" + Application.runInBackground +
                "; focused=" + Application.isFocused +
                "; editorPaused=" + EditorApplication.isPaused +
                "; unityTimeScale=" + Time.timeScale +
                "; framesAdvanced=" + (Time.frameCount - startFrame);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                RuntimePreviewTokenDiagnostic diagnostic = diagnostics[i];
                cleanupNote += "; tween=" + diagnostic.position.ToString("0.000000", CultureInfo.InvariantCulture) +
                    "/" + diagnostic.duration.ToString("0.000000", CultureInfo.InvariantCulture) +
                    " timeScale=" + diagnostic.timeScale.ToString("0.###", CultureInfo.InvariantCulture) +
                    " playing=" + diagnostic.isPlaying + " position=" + diagnostic.anchoredPosition;
            }
            context.Record("cleanup-observation", start, cleanupNote);
            context.Capture("cleanup-observation", start, cleanupNote, true);
            context.Check(tokens == 0 && !stamp, "FlyToken did not clean up after its real animation.");
            context.Check(context.Adapter.CartEntryCount == 1, "Source add did not create exactly one cart entry.");
            List<string> messages = context.Adapter.ToastMessages();
            bool success = false;
            for (int i = 0; i < messages.Count; i++)
                success |= messages[i].IndexOf("已纳入契匣", StringComparison.Ordinal) >= 0;
            context.Check(success, "Successful source add did not produce its success Toast.");

            Click(context, context.Adapter.CardActionButton(0, "Open").gameObject);
            yield return context.Wait(0.45f);
            Button detailBuy = context.Adapter.DetailButton("Buy");
            context.Check(detailBuy.IsInteractable(), "The partially filled position should allow another detail purchase.");
            Click(context, detailBuy.gameObject);
            RectTransform token = RuntimePreviewMarketInteractionProbe.ActiveToken(context.Adapter);
            context.Check(token != null, "A detail add must fly towards its visible header when the market cart is hidden.");
            if (token != null)
                context.Check(Vector2.Distance(RuntimePreviewMarketInteractionProbe.Center(token.gameObject),
                    RuntimePreviewMarketInteractionProbe.Center(detailBuy.gameObject)) < 2f,
                    "The detail token did not originate at the clicked button.");
            context.Capture("detail-add-flight", context.Now, "detail purchase starts at its button and uses the active detail header", true);
        }

        private static IEnumerator PixelConstant(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            CanvasScaler[] scalers = context.Adapter.PreviewCanvasScalers;
            int active = 0;
            for (int i = 0; i < scalers.Length; i++)
            {
                if (!scalers[i].gameObject.activeInHierarchy)
                    continue;
                active++;
                context.Check(scalers[i].uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize,
                    "CanvasScaler is not ConstantPixelSize: " + scalers[i].name + " mode=" + scalers[i].uiScaleMode);
            }
            context.Check(active > 0, "No active CanvasScaler was found for ConstantPixelSize validation.");
            RuntimePreviewInteractionSample sample = context.Capture("constant-pixel-native", context.Now,
                "ConstantPixelSize native physical viewport", true);
            VisualSemanticSnapshot semantic = VisualValidation.CaptureSemantics(
                context.Adapter.FixtureId, context.Result.name, context.Width, context.Height,
                context.Adapter.CurrentRunStateHash);
            for (int i = 0; i < semantic.captureDiagnostics.Count; i++)
            {
                VisualCaptureDiagnostic diagnostic = semantic.captureDiagnostics[i];
                context.Check(diagnostic.canvasScalerMode == "ConstantPixelSize",
                    "Semantic CanvasScaler mode mismatch: " + diagnostic.canvasPath + " mode=" + diagnostic.canvasScalerMode);
                context.Check(diagnostic.targetWidth == context.Width && diagnostic.targetHeight == context.Height,
                    "targetTexture dimensions differ from the requested physical viewport: " + diagnostic.canvasPath);
                context.Check(Math.Abs(diagnostic.rootTargetRect.x) <= 0.01f && Math.Abs(diagnostic.rootTargetRect.y) <= 0.01f &&
                    Math.Abs(diagnostic.rootTargetRect.width - context.Width) <= 0.01f &&
                    Math.Abs(diagnostic.rootTargetRect.height - context.Height) <= 0.01f,
                    "Root Canvas physical rect is not the native viewport: " + diagnostic.canvasPath +
                    " rect=" + diagnostic.rootTargetRect.x + "," + diagnostic.rootTargetRect.y + "," +
                    diagnostic.rootTargetRect.width + "," + diagnostic.rootTargetRect.height);
            }
            sample.note += "; scalers=" + active;
        }

        private static IEnumerator MarketPointerClick(RuntimePreviewInteractionContext context)
        {
            context.Open("market-empty", false);
            yield return context.Wait(0.6f);
            context.Check(context.Adapter.CartEntryCount == 0,
                "pointer-market-click must begin from the dedicated empty market, not the two-order default.");
            RuntimePreviewInteractionSample empty = context.Capture("empty-before-input", context.Now,
                "dedicated market-empty before real pointer input", true);
            empty.note += "; default market with two orders is a separate following static case";

            Button increment = context.Adapter.CardActionButton(0, "QuantityIncrement");
            Button decrement = context.Adapter.CardActionButton(0, "QuantityDecrement");
            context.Check(increment != null && decrement != null && !decrement.IsInteractable(),
                "First-card quantity controls did not begin at the disabled-one boundary.");
            Click(context, increment.gameObject);
            yield return context.Wait(0.12f);
            context.Check(context.Adapter.FirstCardQuantity == 2 && decrement.IsInteractable(),
                "Screen-coordinate first-card + did not change 1 to 2 and enable −.");
            RuntimePreviewInteractionSample draftTwo = context.Capture("draft-two", context.Now,
                "actual GraphicRaycaster first-card + click", true);
            draftTwo.note += "; pointer evidence is stored in test.pointerEvents";

            Click(context, decrement.gameObject);
            yield return context.Wait(0.12f);
            context.Check(context.Adapter.FirstCardQuantity == 1 && !decrement.IsInteractable(),
                "Screen-coordinate first-card − did not restore 1 and disable −.");
            context.Capture("draft-one", context.Now, "actual first-card − click at the lower boundary", true);

            Click(context, context.Adapter.CardActionButton(0, "Buy").gameObject);
            yield return context.Wait(0.55f);
            context.Check(context.Adapter.CartEntryCount == 1 && context.Adapter.FirstCartQuantity == 1,
                "Actual buy-button click did not create one quantity-one pending entry.");
            Click(context, context.Adapter.CartIncrementButton.gameObject);
            yield return context.Wait(0.12f);
            context.Check(context.Adapter.FirstCartQuantity == 2,
                "Actual cart + click did not change quantity to two.");
            context.Capture("cart-quantity-two", context.Now, "actual cart-entry + click", true);

            Click(context, context.Adapter.CartDecrementButton.gameObject);
            yield return context.Wait(0.12f);
            context.Check(context.Adapter.FirstCartQuantity == 1,
                "Actual cart-entry − click did not restore quantity one.");
            Click(context, context.Adapter.CardActionButton(0, "Open").gameObject);
            float detailStart = context.Now;
            while ((context.Adapter.WindowRootActive || context.Adapter.DetailWindowAnimationActive) &&
                context.ElapsedMs(detailStart) < 1600f)
                yield return new WaitForEndOfFrame();
            context.Check(context.Adapter.DetailItemId == "01",
                "Actual card OpenButton click did not navigate to item 01 detail.");
            context.Check(!context.Adapter.WindowRootActive && !context.Adapter.DetailWindowAnimationActive,
                "Market close and detail open did not reach their formal terminal before semantic capture.");
            yield return context.WaitFrames(2);
            context.Capture("card-click-detail", context.Now,
                "screen-coordinate click on the card OpenButton, not a nested trade control", true);
        }

        private static IEnumerator TickerPointer(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(0.6f);
            RectTransform ticker = context.Adapter.NewsTickerRoot;
            RectTransform track = context.Adapter.NewsTickerTrack;
            context.Check(ticker != null && track != null && ticker.gameObject.activeInHierarchy,
                "Formal NewsTicker root/track is unavailable for pointer validation.");
            Vector2 runningBefore = track.anchoredPosition;
            yield return context.Wait(0.22f);
            Vector2 runningAfter = track.anchoredPosition;
            context.Check(Vector2.Distance(runningBefore, runningAfter) > 0.01f,
                "NewsTicker was not moving before the pointer entered.");

            Pointer(context, ticker.gameObject, true);
            yield return context.WaitFrames(2);
            Vector2 pausedBefore = track.anchoredPosition;
            yield return context.Wait(0.28f);
            Vector2 pausedAfter = track.anchoredPosition;
            context.Check(Vector2.Distance(pausedBefore, pausedAfter) <= 0.01f,
                "NewsTicker moved while the actual raycast-driven pointer was inside.");
            RuntimePreviewInteractionSample paused = context.Capture("ticker-paused", context.Now,
                "real EventSystem/GraphicRaycaster pointer enter pauses the ticker", true);
            paused.note += "; pausedOffset=" + pausedAfter;

            Pointer(context, ticker.gameObject, false);
            yield return context.Wait(0.28f);
            Vector2 resumed = track.anchoredPosition;
            context.Check(Vector2.Distance(pausedAfter, resumed) > 0.01f,
                "NewsTicker did not resume after actual pointer exit.");
            context.Capture("ticker-resumed", context.Now,
                "pointer exit resumes the formal NewsTicker timeline", true);
        }

        private static void RequireSorted(RuntimePreviewInteractionContext context, List<double> values, bool ascending, string label)
        {
            context.Check(values.Count == 54, label + " does not include all 54 returns.");
            for (int i = 1; i < values.Count; i++)
            {
                bool ordered = ascending ? values[i - 1] <= values[i] + 0.0000001 : values[i - 1] >= values[i] - 0.0000001;
                context.Check(ordered, label + " is not monotonic at index " + i + ": " + values[i - 1] + " -> " + values[i]);
            }
        }

        private static bool Near(Color actual, Color32 expected)
        {
            return Mathf.Abs(actual.r - expected.r / 255f) <= 0.01f &&
                Mathf.Abs(actual.g - expected.g / 255f) <= 0.01f &&
                Mathf.Abs(actual.b - expected.b / 255f) <= 0.01f &&
                Mathf.Abs(actual.a - expected.a / 255f) <= 0.01f;
        }

        private static float ChartFloat(Component chart, string property)
        {
            return Convert.ToSingle(
                chart.GetType().GetProperty(property).GetValue(chart, null),
                CultureInfo.InvariantCulture);
        }

        private static void PanChart(RuntimePreviewInteractionContext context, Component chart)
        {
            RectTransform rect = chart.GetComponent<RectTransform>();
            Canvas canvas = chart.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(
                camera,
                rect.TransformPoint(rect.rect.center));
            RuntimePreviewPointerDriver.Drag(
                context,
                chart.gameObject,
                center,
                center + new Vector2(400f, 0f));
        }
    }
}
