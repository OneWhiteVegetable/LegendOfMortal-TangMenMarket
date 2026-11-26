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
        private static IEnumerator MarketLedgerEdges(RuntimePreviewInteractionContext context)
        {
            context.Open("market", false);
            yield return context.Wait(.6f);
            Button button = context.Adapter.CheckoutButton;
            long beforeCash = context.Adapter.CurrentCash;
            string beforeState = context.Adapter.CurrentRunStateHash;
            RuntimePreviewTradeExpectation quote = context.Adapter.PreviewTrade();
            context.Check(quote.RecordCount == 2 && context.Adapter.CartEntryCount == 2,
                "The amount reference must use two real, executable market orders.");
            RectTransform[] frames = LedgerFrameValidation.Frames(context);
            var rest = LedgerFrameValidation.Read(context, button,
                context.Capture("rest", context.Now, "Two executable orders; all six commodity frames and both ledger frames", true), frames);
            Vector2[] positions = Array.ConvertAll(frames, frame => frame.anchoredPosition);
            try
            {
                for (int phase = 1; phase <= 3; phase++)
                {
                    for (int i = 0; i < frames.Length; i++)
                        frames[i].anchoredPosition = positions[i] + LedgerFrameValidation.SubpixelOffset(frames[i], phase);
                    yield return context.WaitFrames(3);
                    LedgerFrameValidation.Read(context, button, context.Capture("fraction-" + phase, context.Now,
                        "Offset the layout by fractions of a physical pixel and rescan every frame edge"), frames);
                }
            }
            finally
            {
                for (int i = 0; i < frames.Length; i++) frames[i].anchoredPosition = positions[i];
            }
            Pointer(context, button.gameObject, true);
            yield return context.Wait(.2f);
            var hover = LedgerFrameValidation.Read(context, button,
                context.Capture("hover", context.Now, "Checkout pointer hover at full size", true));
            LedgerFrameValidation.CheckPose(context, button, rest, hover, -1f, 2f, true, "hover");
            RuntimePreviewMarketInteractionProbe.Down(context, button.gameObject);
            yield return context.Wait(.2f);
            var pressed = LedgerFrameValidation.Read(context, button,
                context.Capture("pressed", context.Now, "Checkout held at full size", true));
            LedgerFrameValidation.CheckPose(context, button, rest, pressed, 2f, -2f, true, "pressed");
            RuntimePreviewMarketInteractionProbe.UpAndClick(context, button.gameObject);
            LedgerFrameValidation.UseFixturePointer();
            yield return context.Wait(.3f);
            Button confirm = context.Adapter.ModalConfirmButton;
            RectTransform dialog = confirm.transform.parent.parent as RectTransform;
            Click(context, dialog.Find("Header/CloseButton").gameObject);
            yield return context.Wait(.18f);
            Pointer(context, button.gameObject, false);
            yield return context.Wait(.18f);
            var returned = LedgerFrameValidation.Read(context, button,
                context.Capture("returned-rest", context.Now, "Dialog X closed; pointer outside checkout", true));
            LedgerFrameValidation.CheckPose(context, button, rest, returned, 0f, 0f, false, "restored");

            Pointer(context, button.gameObject, true);
            yield return context.Wait(.2f);
            var reentered = LedgerFrameValidation.Read(context, button,
                context.Capture("reentered", context.Now, "Hovering checkout again after closing the first contract", true));
            LedgerFrameValidation.CheckPose(context, button, rest, reentered, -1f, 2f, true, "hover after X");
            Click(context, button.gameObject);
            LedgerFrameValidation.UseFixturePointer();
            yield return context.Wait(.26f);
            confirm = context.Adapter.ModalConfirmButton;
            Click(context, confirm.transform.parent.Find("CancelButton").gameObject);
            yield return context.Wait(.18f);
            Pointer(context, button.gameObject, false);
            yield return context.Wait(.15f);
            Pointer(context, button.gameObject, true);
            yield return context.Wait(.18f);
            var afterCancel = LedgerFrameValidation.Read(context, button,
                context.Capture("reentered-after-cancel", context.Now, "Second hover after cancelling a new contract", true));
            LedgerFrameValidation.CheckPose(context, button, rest, afterCancel, -1f, 2f, true, "hover after cancel");
            context.Check(context.Adapter.CurrentRunStateHash == beforeState && context.Adapter.CurrentCash == beforeCash,
                "Closing either confirmation must leave both the wallet and economic state intact.");
        }

        private static IEnumerator MarketInkReview(RuntimePreviewInteractionContext context)
        {
            context.Open("market-empty", true);
            yield return context.Wait(0.3f);
            Button borrow = context.Adapter.CardActionButton(0, "Borrow");
            Click(context, borrow.gameObject);
            yield return context.Wait(0.1f);
            context.Check(context.Adapter.CartEntryCount == 1, "Borrow did not enter the pending ledger.");
            Pointer(context, borrow.gameObject, false);
            yield return context.Wait(0.25f);
            context.Check(context.Adapter.CardGoldLine(0).localScale.y < 0.01f,
                "The left gold line remained after borrowing and moving the pointer off the card.");
            context.Capture("borrow-pointer-left", context.Now, "Pointer has exited the card after a successful borrow", true);
            Button pending = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "PendingTabButton");
            Button settled = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "SettledTabButton");
            Click(context, settled.gameObject);
            yield return context.Wait(0.18f);
            context.Capture("settled-ledger", context.Now, "Historical ledger on the native cart rail", true);
            Click(context, pending.gameObject);
            Click(context, context.Adapter.CardActionButton(0, "Open").gameObject);
            yield return context.Wait(0.21f);
            Click(context, context.Adapter.DetailButton("QuantityIncrement").gameObject);
            Click(context, context.Adapter.DetailButton("Back").gameObject);
            yield return context.Wait(0.25f);
            Pointer(context, context.Adapter.CardRoot(0), false);
            yield return context.Wait(0.20f);
            context.Check(context.Adapter.CardGoldLine(0).localScale.y < 0.01f,
                "Returning from a detail quantity change retained mouse-selected card lighting.");
            context.Capture("returned-market", context.Now, "First page after adjusting quantity in detail and returning", true);
            EventSystem.current.SetSelectedGameObject(context.Adapter.CardRoot(0), new AxisEventData(EventSystem.current));
            yield return context.WaitFrames(3);
            context.Check(context.Adapter.CardGoldLine(0).localScale.y > .99f,
                "Keyboard-origin selection must still show the card focus line.");
            EventSystem.current.SetSelectedGameObject(null);
            yield return context.WaitFrames(3);
            context.Check(context.Adapter.CardGoldLine(0).localScale.y < .01f,
                "The card remained highlighted after relinquishing keyboard focus.");
            context.Adapter.ShowRanking();
            yield return context.Wait(0.25f);
            context.Check(context.Adapter.RankingRowCount == 54, "Ranking header did not accompany the full list.");
            context.Capture("ranking-heading", context.Now, "Ranking title and rows with their own paper geometry");
        }

        private static IEnumerator MarketTooltipReuse(RuntimePreviewInteractionContext context)
        {
            context.Open("market-empty", false);
            yield return context.Wait(0.6f);
            RectTransform tooltip = context.Adapter.TooltipRoot;
            context.Check(tooltip != null, "Market tooltip was not created by the formal card bindings.");
            EventSystem.current.SetSelectedGameObject(null);

            float start = context.Now;
            Pointer(context, context.Adapter.CardRoot(0), true);
            yield return ShowMarketTooltip(context, "card-item-01", start,
                RuntimePreviewMarketInteractionProbe.Item(context.Adapter, 0), null, true);

            start = context.Now;
            Pointer(context, context.Adapter.CardActionButton(0, "Buy").gameObject, true);
            yield return ShowMarketTooltip(context, "buy-operation", start, null, "加入购物车 · 做多", true);

            start = context.Now;
            Pointer(context, context.Adapter.CardActionButton(0, "Borrow").gameObject, true);
            yield return ShowMarketTooltip(context, "borrow-operation", start, null, "立下借货契 · 做空", true);

            start = context.Now;
            Pointer(context, context.Adapter.CardRoot(1), true);
            yield return ShowMarketTooltip(context, "other-card-item-02", start,
                RuntimePreviewMarketInteractionProbe.Item(context.Adapter, 1), null, true);

            Pointer(context, context.Adapter.CardRoot(1), false);
            yield return context.Wait(0.28f);
            context.Check(!tooltip.gameObject.activeSelf,
                "Tooltip remained visible after the card-buy-borrow-other-card-exit sequence.");

            start = context.Now;
            Pointer(context, context.Adapter.CardRoot(0), true);
            yield return context.WaitUntil(start, 25f);
            Pointer(context, context.Adapter.CardActionButton(0, "Buy").gameObject, true);
            yield return context.WaitUntil(start, 50f);
            Pointer(context, context.Adapter.CardActionButton(0, "Borrow").gameObject, true);
            yield return context.WaitUntil(start, 75f);
            Pointer(context, context.Adapter.CardRoot(1), true);
            yield return context.WaitUntil(start, 100f);
            Pointer(context, context.Adapter.CardRoot(1), false);
            yield return context.Wait(0.3f);
            context.Check(!tooltip.gameObject.activeSelf,
                "A delayed tooltip survived a rapid card/button/card sweep and exit.");

            Button next = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "NextPageButton");
            context.Check(next != null && next.IsInteractable(), "The real next-page button is unavailable for card reuse validation.");
            Click(context, next.gameObject);
            yield return context.Wait(0.25f);
            RuntimePreviewItemBinding pageTwo = RuntimePreviewMarketInteractionProbe.Item(context.Adapter, 0);
            context.Check(pageTwo.ItemId == "07",
                "Paging did not rebind the first reusable card to item 07; actual=" + pageTwo.ItemId + ".");
            Text pageTwoName = RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "NameText");
            context.Check(pageTwoName != null && pageTwoName.text == pageTwo.Name,
                "The reusable card NameText and direct tooltip binding refer to different items after paging.");
            start = context.Now;
            Pointer(context, context.Adapter.CardRoot(0), true);
            yield return ShowMarketTooltip(context, "page-two-item-07", start, pageTwo, null, true);

            Button rightEdge = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "CartClearButton");
            if (rightEdge != null)
            {
                start = context.Now;
                Pointer(context, rightEdge.gameObject, true);
                yield return ShowMarketTooltip(context, "right-edge-clamp", start, null, null, false);
            }
            Pointer(context, rightEdge == null ? context.Adapter.CardRoot(0) : rightEdge.gameObject, false);
            yield return context.Wait(0.25f);
            context.Check(!tooltip.gameObject.activeSelf, "Tooltip remained after final pointer exit.");
        }

        private static IEnumerator ShowMarketTooltip(
            RuntimePreviewInteractionContext context,
            string label,
            float start,
            RuntimePreviewItemBinding expectedItem,
            string expectedOperation,
            bool capturePng)
        {
            RectTransform root = context.Adapter.TooltipRoot;
            CanvasGroup group = root == null ? null : root.GetComponent<CanvasGroup>();
            while ((root == null || !root.gameObject.activeSelf || group == null || group.alpha < 0.999f) &&
                context.ElapsedMs(start) < 1200f)
                yield return new WaitForEndOfFrame();
            context.Check(root != null && root.gameObject.activeSelf && group != null && group.alpha >= 0.999f,
                label + " did not complete its tooltip fade-in before capture.");
            if (root == null)
                yield break;
            Canvas.ForceUpdateCanvases();
            RuntimePreviewTooltipProbe probe = RuntimePreviewMarketInteractionProbe.CaptureTooltip(
                context, label, start, expectedItem);
            if (expectedItem != null)
            {
                context.Check(probe.title == expectedItem.Name,
                    label + " title does not match the bound item: expected=" + expectedItem.Name + " actual=" + probe.title);
                context.Check(probe.body == expectedItem.Description,
                    label + " does not contain the complete bound item description.");
                context.Check(probe.hint == "点击查看行情 · " + expectedItem.Key,
                    label + " hint does not contain the current item identifier: " + probe.hint);
                context.Check(probe.bodyLineCount >= 2 && probe.horizontalOverflow == "Wrap",
                    label + " long description did not wrap onto multiple lines; lines=" + probe.bodyLineCount +
                    " overflow=" + probe.horizontalOverflow + ".");
                context.Check(probe.effectiveTitleAlpha >= 0.999f && probe.effectiveBodyAlpha >= 0.999f &&
                    probe.effectiveHintAlpha >= 0.999f,
                    label + " text alpha was sampled before all item text reached its terminal opacity.");
            }
            else if (!string.IsNullOrEmpty(expectedOperation))
            {
                context.Check(string.IsNullOrEmpty(probe.title) && probe.body == expectedOperation &&
                    string.IsNullOrEmpty(probe.hint),
                    label + " does not match its prototype operation hint: " + probe.body);
                context.Check(probe.effectiveBodyAlpha >= 0.999f,
                    label + " operation text alpha did not reach one before capture.");
            }
            Rect screen = Rect.MinMaxRect(
                probe.screenTopLeftMin.x, probe.screenTopLeftMin.y,
                probe.screenTopLeftMax.x, probe.screenTopLeftMax.y);
            context.Check(screen.xMin >= 9.5f && screen.yMin >= 9.5f &&
                screen.xMax <= context.Width - 9.5f && screen.yMax <= context.Height - 9.5f,
                label + " escaped the ten-pixel screen clamp: " + screen + " viewport=" +
                context.Width + "x" + context.Height + ".");
            if (!capturePng)
                yield break;
            RuntimePreviewInteractionSample sample = context.Capture(label, start,
                "native PNG after group alpha and effective text alpha reached one; actual elapsedMs is recorded", false);
            RecordTooltip(context, sample, root);
            probe.png = sample.png;
        }

        private static IEnumerator TradeButtonMotion(RuntimePreviewInteractionContext context)
        {
            context.Open("market-empty", false);
            yield return context.Wait(0.6f);
            string[] actions = { "Buy", "Borrow" };
            for (int i = 0; i < actions.Length; i++)
            {
                string action = actions[i];
                // Release clicks enter the cart. Use another commodity for borrowing since a long
                // and short in the same item would correctly disable its second direction.
                Button button = context.Adapter.CardActionButton(i, action);
                Pointer(context, button.gameObject, false);
                yield return context.Wait(0.2f);
                float start = context.Now;
                RuntimePreviewButtonProbe rest = RuntimePreviewMarketInteractionProbe.CaptureButton(
                    context, button, action, "rest", start);
                RuntimePreviewInteractionSample restPng = context.Capture(
                    action.ToLowerInvariant() + "-whole-button-rest", start,
                    "rest reference for native face-pixel comparison", false);
                rest.png = restPng.png;
                RuntimePreviewMarketInteractionProbe.CaptureFacePixel(restPng.png, button, context.Height, rest);

                Pointer(context, button.gameObject, true);
                yield return context.Wait(0.18f);
                RuntimePreviewButtonProbe hover = RuntimePreviewMarketInteractionProbe.CaptureButton(
                    context, button, action, "hover", start);
                RuntimePreviewInteractionSample hoverPng = context.Capture(action.ToLowerInvariant() + "-whole-button-hover",
                    start, "whole trade button translate/rotate/brightness, not icon-only", false);
                hover.png = hoverPng.png;
                RuntimePreviewMarketInteractionProbe.CaptureFacePixel(hoverPng.png, button, context.Height, hover);
                CheckButtonHover(context, button, rest, hover, action + " hover");

                int cartBeforePress = context.Adapter.CartEntryCount;
                RuntimePreviewMarketInteractionProbe.Down(context, button.gameObject);
                yield return context.Wait(0.18f);
                RuntimePreviewButtonProbe pressed = RuntimePreviewMarketInteractionProbe.CaptureButton(
                    context, button, action, "pressed", start);
                context.Check(context.Adapter.CartEntryCount == cartBeforePress && !pressed.detailOpened,
                    action + " pointerDown changed the cart or opened detail before pointerUp/click.");
                RuntimePreviewInteractionSample pressedPng = context.Capture(action.ToLowerInvariant() + "-whole-button-pressed",
                    start, "whole trade button down pose (1,-1) scale .93 in Unity local coordinates", false);
                pressed.png = pressedPng.png;
                RuntimePreviewMarketInteractionProbe.CaptureFacePixel(pressedPng.png, button, context.Height, pressed);
                CheckButtonPressed(context, rest, hover, pressed, action + " pressed");

                RuntimePreviewMarketInteractionProbe.UpAndClick(context, button.gameObject);
                yield return context.Wait(0.18f);
                RuntimePreviewButtonProbe released = RuntimePreviewMarketInteractionProbe.CaptureButton(
                    context, button, action, "released-hover", start);
                context.Check(!released.detailOpened,
                    action + " real button click bubbled into item detail.");
                RuntimePreviewInteractionSample releasedPng = context.Capture(action.ToLowerInvariant() + "-whole-button-released-hover",
                    start, "released hover returns to the bright hover face", false);
                released.png = releasedPng.png;
                RuntimePreviewMarketInteractionProbe.CaptureFacePixel(releasedPng.png, button, context.Height, released);
                CheckButtonHover(context, button, rest, released, action + " release rebound");

                Pointer(context, button.gameObject, false);
                yield return context.Wait(0.5f);
                RuntimePreviewButtonProbe exited = RuntimePreviewMarketInteractionProbe.CaptureButton(
                    context, button, action, "exit-rest", start);
                RuntimePreviewInteractionSample exitPng = context.Capture(action.ToLowerInvariant() + "-whole-button-exit-rest",
                    start, "exit restores the original whole-button pose and face", false);
                exited.png = exitPng.png;
                RuntimePreviewMarketInteractionProbe.CaptureFacePixel(exitPng.png, button, context.Height, exited);
                CheckButtonRest(context, rest, exited, action + " exit");
                context.Check(exited.facePixel.a >= 0.99f && NearFacePixel(exited.facePixel, rest.facePixel),
                    action + " exit native face did not return to its rest color: rest=" + rest.facePixel +
                    " actual=" + exited.facePixel + ".");
            }
        }

        private static void CheckButtonHover(
            RuntimePreviewInteractionContext context,
            Button button,
            RuntimePreviewButtonProbe rest,
            RuntimePreviewButtonProbe hover,
            string label)
        {
            Vector2 expectedLocal = rest.localPosition + new Vector2(0f, 3f);
            context.Check(Vector2.Distance(hover.localPosition, expectedLocal) <= 0.08f,
                label + " whole-button local position mismatch: expected=" + expectedLocal + " actual=" + hover.localPosition);
            context.Check(Mathf.Abs(Mathf.DeltaAngle(hover.rotationZ, rest.rotationZ + 3f)) <= 0.03f,
                label + " rotation must be +3 degrees in Unity (CSS -3 degrees): " + hover.rotationZ);
            context.Check(Vector3.Distance(hover.localScale, rest.localScale) <= 0.001f,
                label + " hover unexpectedly scaled the button: " + hover.localScale);
            Vector2 parentOffset = hover.parentCenterBottomLeft - rest.parentCenterBottomLeft;
            Vector2 expectedScreen = rest.centerBottomLeft + RuntimePreviewMarketInteractionProbe.ScreenDelta(
                button.GetComponent<RectTransform>(), new Vector2(0f, 3f)) + parentOffset;
            context.Check(Vector2.Distance(hover.centerBottomLeft, expectedScreen) <= 0.15f,
                label + " screen-space center did not move upward by the scaled three CSS pixels: expected=" +
                expectedScreen + " actual=" + hover.centerBottomLeft);
            CheckBrightness(context, rest, hover, 1.12f, label);
            context.Check(hover.facePixel.a >= 0.99f &&
                NearFacePixel(hover.facePixel, Multiply(rest.facePixel, 1.12f)),
                label + " native face pixel does not approach CSS brightness(1.12): rest=" +
                rest.facePixel + " actual=" + hover.facePixel + " sample=" + hover.faceSampleTopLeft);
        }

        private static void CheckButtonPressed(
            RuntimePreviewInteractionContext context,
            RuntimePreviewButtonProbe rest,
            RuntimePreviewButtonProbe hover,
            RuntimePreviewButtonProbe pressed,
            string label)
        {
            Vector2 expectedLocal = rest.localPosition + new Vector2(1f, -1f);
            context.Check(Vector2.Distance(pressed.localPosition, expectedLocal) <= 0.08f,
                label + " whole-button down position mismatch: expected=" + expectedLocal + " actual=" + pressed.localPosition);
            context.Check(Mathf.Abs(Mathf.DeltaAngle(pressed.rotationZ, rest.rotationZ)) <= 0.03f,
                label + " active pose must remove hover rotation: " + pressed.rotationZ);
            context.Check(Vector3.Distance(pressed.localScale, rest.localScale * 0.93f) <= 0.001f,
                label + " active pose must scale the whole button to .93: " + pressed.localScale);
            context.Check(NearColor(pressed.backgroundTint, hover.backgroundTint) && NearColor(pressed.iconTint, hover.iconTint),
                label + " must retain the CSS hover brightness while :active transforms the button.");
            context.Check(NearFacePixel(pressed.facePixel, hover.facePixel),
                label + " native pressed face differs from the hover-brightened normal face: hover=" +
                hover.facePixel + " pressed=" + pressed.facePixel + ".");
        }

        private static void CheckButtonRest(
            RuntimePreviewInteractionContext context,
            RuntimePreviewButtonProbe rest,
            RuntimePreviewButtonProbe exited,
            string label)
        {
            context.Check(Vector2.Distance(exited.localPosition, rest.localPosition) <= 0.08f &&
                Mathf.Abs(Mathf.DeltaAngle(exited.rotationZ, rest.rotationZ)) <= 0.03f &&
                Vector3.Distance(exited.localScale, rest.localScale) <= 0.001f,
                label + " did not return its whole button to the original pose.");
            CheckBrightness(context, rest, exited, 1f, label);
        }

        private static void CheckBrightness(
            RuntimePreviewInteractionContext context,
            RuntimePreviewButtonProbe rest,
            RuntimePreviewButtonProbe actual,
            float multiplier,
            string label)
        {
            context.Check(NearColor(actual.backgroundTint, Multiply(rest.backgroundTint, multiplier)) &&
                NearColor(actual.iconTint, Multiply(rest.iconTint, multiplier)),
                label + " background/icon brightness mismatch: multiplier=" + multiplier.ToString("0.00", CultureInfo.InvariantCulture) +
                " background=" + actual.backgroundTint + " icon=" + actual.iconTint);
        }

        private static bool NearColor(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= 0.005f && Mathf.Abs(left.g - right.g) <= 0.005f &&
                Mathf.Abs(left.b - right.b) <= 0.005f && Mathf.Abs(left.a - right.a) <= 0.005f;
        }

        private static bool NearFacePixel(Color actual, Color expected)
        {
            return Mathf.Abs(actual.r - expected.r) <= 0.035f &&
                Mathf.Abs(actual.g - expected.g) <= 0.035f &&
                Mathf.Abs(actual.b - expected.b) <= 0.035f;
        }

        private static Color Multiply(Color color, float multiplier)
        {
            color.r *= multiplier;
            color.g *= multiplier;
            color.b *= multiplier;
            return color;
        }

        private static IEnumerator AddToCartFlightProbe(RuntimePreviewInteractionContext context)
        {
            context.Adapter.ConfigureFlightCapacityFixture();
            context.Open("capacity-empty", false);
            yield return context.Wait(0.6f);
            yield return RunFlightProbe(context, 0, "Buy", false);
            yield return RunFlightProbe(context, 1, "Borrow", false);
            context.Adapter.SetReducedMotion(true);
            yield return RunFlightProbe(context, 2, "Buy", true);
            yield return RunFlightProbe(context, 4, "Borrow", true);
            context.Check(context.Adapter.CartEntryCount == 4,
                "Buy, borrow and both reduced-motion actions must each enter the cart exactly once; entries=" +
                context.Adapter.CartEntryCount + ".");
            context.Check(string.IsNullOrEmpty(context.Adapter.DetailItemId),
                "A trade-button click opened detail during the flight probe.");
        }

        private static IEnumerator RunFlightProbe(
            RuntimePreviewInteractionContext context,
            int cardIndex,
            string action,
            bool reducedMotion)
        {
            Button button = context.Adapter.CardActionButton(cardIndex, action);
            RuntimePreviewItemBinding item = RuntimePreviewMarketInteractionProbe.Item(context.Adapter, cardIndex);
            Vector2 buttonBefore = RuntimePreviewMarketInteractionProbe.Center(button.gameObject);
            float start = context.Now;
            RuntimePreviewFlightProbe probe = new RuntimePreviewFlightProbe
            {
                action = action,
                itemId = item.ItemId,
                reducedMotion = reducedMotion,
                buttonInteractable = button.IsInteractable(),
                itemDelisted = item.Delisted,
                samplingMethod = reducedMotion
                    ? "Real click with reduced motion; no FlyToken is expected."
                    : "Real clicked production tween paused after spawn; actual tween Goto samples at 180ms and 359.99ms while wall time advances; resumed for natural completion and cleanup.",
                cartBefore = context.Adapter.CartEntryCount,
                buttonCenterBeforeBottomLeft = buttonBefore,
                buttonCenterBeforeTopLeft = RuntimePreviewMarketInteractionProbe.TopLeft(context, buttonBefore),
                expectedTargetLeftTop = RuntimePreviewMarketInteractionProbe.ExpectedFlightTargetLeftTop(
                    RuntimePreviewMarketInteractionProbe.CartRail(context.Adapter), context.Height),
                expectedUntransformedSizeTopLeft = RuntimePreviewMarketInteractionProbe.ExpectedFlightTokenSize(
                    context.Width, context.Height),
                expectedSprite = RuntimePreviewMarketInteractionProbe.CardItemImage(context.Adapter, cardIndex).sprite.name
            };
            context.Result.flightProbes.Add(probe);
            Click(context, button.gameObject);
            probe.toastMessagesAfterClick.AddRange(context.Adapter.ToastMessages());
            probe.spawnElapsedMs = context.ElapsedMs(start);
            probe.cartAfter = context.Adapter.CartEntryCount;
            probe.cartDelta = probe.cartAfter - probe.cartBefore;
            Vector2 buttonAtSpawn = RuntimePreviewMarketInteractionProbe.Center(button.gameObject);
            probe.buttonCenterAtSpawnBottomLeft = buttonAtSpawn;
            probe.buttonCenterAtSpawnTopLeft = RuntimePreviewMarketInteractionProbe.TopLeft(context, buttonAtSpawn);
            RectTransform token = RuntimePreviewMarketInteractionProbe.ActiveToken(context.Adapter);
            bool created;
            int activeTokens;
            bool stamp;
            context.Adapter.FeedbackState(out created, out activeTokens, out stamp);
            probe.stampActive = stamp;
            context.Check(probe.cartDelta == 1,
                action + " did not increment the cart exactly once: before=" + probe.cartBefore + " after=" + probe.cartAfter);
            context.Check(!stamp, action + " add-to-cart unexpectedly produced a settlement stamp.");
            if (reducedMotion)
            {
                context.Check(token == null && activeTokens == 0,
                    "Reduced-motion " + action + " left a FlyToken at generation time.");
                yield return context.Wait(0.45f);
                context.Adapter.FeedbackState(out created, out activeTokens, out stamp);
                probe.cleanupElapsedMs = context.ElapsedMs(start);
                probe.activeTokensAfterCleanup = activeTokens;
                probe.pooledTokensAfterCleanup = RuntimePreviewMarketInteractionProbe.Tokens(context.Adapter, false).Count;
                probe.tokenTweensAfterCleanup = RuntimePreviewMarketInteractionProbe.TokenTweenCount(context.Adapter);
                probe.cleaned = activeTokens == 0 && probe.tokenTweensAfterCleanup == 0;
                context.Check(probe.cleaned && !stamp && context.Adapter.CartEntryCount == probe.cartAfter,
                    "Reduced-motion " + action + " was not residual-free or inserted the cart more than once.");
                context.Record(action.ToLowerInvariant() + "-reduced-motion-clean", start,
                    "cartDelta=" + probe.cartDelta + "; activeTokens=" + activeTokens + "; tokenTweens=" +
                    probe.tokenTweensAfterCleanup + "; actual elapsedMs=" +
                    probe.cleanupElapsedMs.ToString("0.0", CultureInfo.InvariantCulture));
                yield break;
            }

            context.Check(token != null && activeTokens == 1,
                action + " did not create exactly one FlyToken from the clicked button.");
            if (token == null)
                yield break;
            Vector2 spawn = RuntimePreviewMarketInteractionProbe.Center(token.gameObject);
            probe.tokenSpawnCenterBottomLeft = spawn;
            probe.tokenSpawnCenterTopLeft = RuntimePreviewMarketInteractionProbe.TopLeft(context, spawn);
            probe.expectedLandingCenterTopLeft = RuntimePreviewMarketInteractionProbe.ExpectedFlightLanding(
                RuntimePreviewMarketInteractionProbe.CartRail(context.Adapter), token, context.Height);
            RuntimePreviewFlightFrameProbe spawnFrame = RuntimePreviewMarketInteractionProbe.CaptureFlightFrame(
                context, token, "spawn", start);
            probe.frames.Add(spawnFrame);
            context.Check(Vector2.Distance(
                spawnFrame.untransformedSizeTopLeft, probe.expectedUntransformedSizeTopLeft) <= 0.75f,
                action + " untransformed FlyToken size differs from the 55 CSS px viewport conversion: expected=" +
                probe.expectedUntransformedSizeTopLeft + " actual=" + spawnFrame.untransformedSizeTopLeft + ".");
            context.Check(Vector2.Distance(
                spawnFrame.itemImageSizeTopLeft, probe.expectedUntransformedSizeTopLeft * (45f / 55f)) <= 0.75f,
                action + " item art did not retain its 45/55 proportion inside the resized FlyToken: expected=" +
                probe.expectedUntransformedSizeTopLeft * (45f / 55f) + " actual=" + spawnFrame.itemImageSizeTopLeft + ".");
            Image tokenImage = RuntimePreviewMarketInteractionProbe.TokenImage(token);
            probe.tokenSprite = tokenImage == null || tokenImage.sprite == null ? string.Empty : tokenImage.sprite.name;
            context.Check(Vector2.Distance(spawn, buttonAtSpawn) <= 0.75f,
                action + " FlyToken did not spawn at the actual clicked button center: button=" + buttonAtSpawn +
                " token=" + spawn + ".");
            context.Check(tokenImage != null && tokenImage.sprite == RuntimePreviewMarketInteractionProbe.CardItemImage(
                context.Adapter, cardIndex).sprite,
                action + " FlyToken did not preserve the current item sprite.");

            context.Adapter.PauseActiveTokenTweenForSampling();
            yield return context.WaitUntil(start, 180f);
            context.Adapter.SeekActiveTokenTweenForSampling(0.18f);
            probe.midpointElapsedMs = context.ElapsedMs(start);
            Vector2 midpoint = RuntimePreviewMarketInteractionProbe.Center(token.gameObject);
            probe.midpointCenterTopLeft = RuntimePreviewMarketInteractionProbe.TopLeft(context, midpoint);
            probe.lastActiveCenterTopLeft = probe.midpointCenterTopLeft;
            RuntimePreviewFlightFrameProbe midpointFrame = RuntimePreviewMarketInteractionProbe.CaptureFlightFrame(
                context, token, "midpoint", start);
            probe.frames.Add(midpointFrame);
            probe.tweenPosition = midpointFrame.tweenPosition;
            probe.tweenDuration = midpointFrame.tweenDuration;
            probe.midpointAlpha = midpointFrame.alpha;
            context.Check(token.gameObject.activeSelf &&
                probe.tweenPosition > 0f && probe.tweenPosition < probe.tweenDuration,
                action + " has no real in-flight tween at the approximately 180ms midpoint.");
            context.Check(Mathf.Abs(probe.tweenPosition - 0.18f) <= 0.0001f,
                action + " production tween was not evaluated at the intended 180ms midpoint: " + probe.tweenPosition);
            context.Check(Vector2.Distance(spawn, midpoint) > 1f,
                action + " FlyToken did not move away from the clicked button by its midpoint.");
            CheckFlightBounds(context, action + " midpoint", spawnFrame, midpointFrame);
            float midpointWidthRatio = midpointFrame.transformedSizeTopLeft.x /
                Mathf.Max(0.001f, midpointFrame.untransformedSizeTopLeft.x);
            context.Check(midpointWidthRatio >= 0.85f && midpointWidthRatio <= 0.98f,
                action + " midpoint rotated/scaled bounds do not match the visible CSS flight stage: ratio=" +
                midpointWidthRatio.ToString("0.0000", CultureInfo.InvariantCulture) + " actual=" +
                midpointFrame.transformedSizeTopLeft + ".");

            RuntimePreviewDeferredFlightCapture midpointCapture = context.Runner.CaptureFlightFrame(
                context, action.ToLowerInvariant() + "-button-flight-midpoint", start,
                "actual clicked-button origin; native texture retained before deferred PNG encoding");
            try
            {
                RuntimePreviewInteractionSample mid = midpointCapture.Sample;
                midpointFrame.png = mid.png;
                mid.position = midpoint;
                mid.alpha = probe.midpointAlpha;
                mid.active = token.gameObject.activeSelf;
                mid.activeObjectCount = 1;
                mid.scale = midpointFrame.localScale;
                mid.rotation = token.localEulerAngles;

                yield return context.WaitUntil(start, 359.99f);
                context.Adapter.SeekActiveTokenTweenForSampling(probe.tweenDuration - 0.00001f);
                RuntimePreviewFlightFrameProbe nearEnd = RuntimePreviewMarketInteractionProbe.CaptureFlightFrame(
                    context, token, "near-end", start);
                probe.lastActiveCenterTopLeft = nearEnd.centerTopLeft;
                probe.frames.Add(nearEnd);
                context.Check(nearEnd.tweenDuration > 0f &&
                    Mathf.Abs(nearEnd.tweenPosition - (nearEnd.tweenDuration - 0.00001f)) <= 0.0001f &&
                    nearEnd.alpha <= 0.001f,
                    action + " near-end sample is not the intended almost-transparent 359.99ms endpoint: position=" +
                    nearEnd.tweenPosition + "/" + nearEnd.tweenDuration + " alpha=" + nearEnd.alpha + ".");
                context.Adapter.ResumeActiveTokenTweenAfterSampling();

                while (token.gameObject.activeSelf && context.ElapsedMs(start) < 4000f)
                {
                    Vector2 lastActive = RuntimePreviewMarketInteractionProbe.Center(token.gameObject);
                    probe.lastActiveCenterTopLeft = RuntimePreviewMarketInteractionProbe.TopLeft(context, lastActive);
                    yield return new WaitForEndOfFrame();
                }
                probe.cleanupElapsedMs = context.ElapsedMs(start);
                Vector2 landing = RuntimePreviewMarketInteractionProbe.Center(token.gameObject);
                probe.landingCenterTopLeft = RuntimePreviewMarketInteractionProbe.TopLeft(context, landing);
                context.Adapter.FeedbackState(out created, out activeTokens, out stamp);
                probe.activeTokensAfterCleanup = activeTokens;
                probe.pooledTokensAfterCleanup = RuntimePreviewMarketInteractionProbe.Tokens(context.Adapter, false).Count;
                probe.tokenTweensAfterCleanup = RuntimePreviewMarketInteractionProbe.TokenTweenCount(context.Adapter);
                probe.cleaned = !token.gameObject.activeSelf && activeTokens == 0 &&
                    probe.tokenTweensAfterCleanup == 0;
                probe.stampActive = stamp;
                context.Check(probe.cleaned && !stamp,
                    action + " FlyToken/tween did not clean up after the real 360ms tween; wall elapsedMs=" +
                    probe.cleanupElapsedMs.ToString("0.0", CultureInfo.InvariantCulture) + ".");
                context.Check(Vector2.Distance(probe.landingCenterTopLeft, probe.expectedLandingCenterTopLeft) <= 1f,
                    action + " FlyToken center differs from the untransformed CSS left/top plus half token size: expected=" +
                    probe.expectedLandingCenterTopLeft + " actual=" + probe.landingCenterTopLeft + ".");
                context.Check(context.Adapter.CartEntryCount == probe.cartAfter,
                    action + " changed the cart again during flight cleanup.");
                context.Record(action.ToLowerInvariant() + "-button-flight-cleaned", start,
                    "cartDelta=" + probe.cartDelta + "; activeTokens=" + activeTokens + "; tokenTweens=" +
                    probe.tokenTweensAfterCleanup + "; lastActive=" + probe.lastActiveCenterTopLeft +
                    "; landing=" + probe.landingCenterTopLeft + "; actual elapsedMs=" +
                    probe.cleanupElapsedMs.ToString("0.0", CultureInfo.InvariantCulture));
            }
            finally
            {
                context.Runner.CompleteFlightCapture(context, midpointCapture);
            }
        }

        private static void CheckFlightBounds(
            RuntimePreviewInteractionContext context,
            string label,
            RuntimePreviewFlightFrameProbe untransformed,
            RuntimePreviewFlightFrameProbe transformed)
        {
            float radians = transformed.rotationZ * Mathf.Deg2Rad;
            float cosine = Mathf.Abs(Mathf.Cos(radians));
            float sine = Mathf.Abs(Mathf.Sin(radians));
            Vector2 expected = new Vector2(
                untransformed.untransformedSizeTopLeft.x * Mathf.Abs(transformed.localScale.x) * cosine +
                untransformed.untransformedSizeTopLeft.y * Mathf.Abs(transformed.localScale.y) * sine,
                untransformed.untransformedSizeTopLeft.x * Mathf.Abs(transformed.localScale.x) * sine +
                untransformed.untransformedSizeTopLeft.y * Mathf.Abs(transformed.localScale.y) * cosine);
            context.Check(Vector2.Distance(expected, transformed.transformedSizeTopLeft) <= 0.75f,
                label + " actual world-corner bounds do not match its rotation and scale: expected=" + expected +
                " actual=" + transformed.transformedSizeTopLeft + ".");
        }
    }
}
