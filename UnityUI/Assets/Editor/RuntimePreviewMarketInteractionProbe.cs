using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class RuntimePreviewTooltipProbe
    {
        public string label;
        public string itemId;
        public float elapsedMs;
        public string expectedTitle;
        public string expectedBody;
        public string expectedHint;
        public string title;
        public string body;
        public string hint;
        public string horizontalOverflow;
        public int bodyLineCount;
        public float groupAlpha;
        public float titleColorAlpha;
        public float bodyColorAlpha;
        public float hintColorAlpha;
        public float effectiveTitleAlpha;
        public float effectiveBodyAlpha;
        public float effectiveHintAlpha;
        public Vector2 screenTopLeftMin;
        public Vector2 screenTopLeftMax;
        public string png;
    }

    [Serializable]
    public sealed class RuntimePreviewButtonProbe
    {
        public string action;
        public string phase;
        public float elapsedMs;
        public Vector2 centerBottomLeft;
        public Vector2 centerTopLeft;
        public Vector2 parentCenterBottomLeft;
        public Vector2 localPosition;
        public Vector3 localScale;
        public float rotationZ;
        public Color backgroundTint;
        public Color iconTint;
        public Color facePixel;
        public Vector2 faceSampleTopLeft;
        public bool detailOpened;
        public string png;
    }

    [Serializable]
    public sealed class RuntimePreviewFlightFrameProbe
    {
        public string phase;
        public float elapsedMs;
        public float tweenPosition;
        public float tweenDuration;
        public Vector2 localSize;
        public Vector2 untransformedSizeTopLeft;
        public Vector2 itemImageSizeTopLeft;
        public Vector2 transformedBoundsMinTopLeft;
        public Vector2 transformedBoundsMaxTopLeft;
        public Vector2 transformedSizeTopLeft;
        public Vector2 centerTopLeft;
        public Vector3 localScale;
        public float rotationZ;
        public float alpha;
        public string png;
    }

    [Serializable]
    public sealed class RuntimePreviewFlightProbe
    {
        public string action;
        public string itemId;
        public bool reducedMotion;
        public string samplingMethod;
        public bool buttonInteractable;
        public bool itemDelisted;
        public List<string> toastMessagesAfterClick = new List<string>();
        public int cartBefore;
        public int cartAfter;
        public int cartDelta;
        public float spawnElapsedMs;
        public float midpointElapsedMs;
        public float cleanupElapsedMs;
        public float tweenPosition;
        public float tweenDuration;
        public float midpointAlpha;
        public Vector2 buttonCenterBeforeBottomLeft;
        public Vector2 buttonCenterBeforeTopLeft;
        public Vector2 buttonCenterAtSpawnBottomLeft;
        public Vector2 buttonCenterAtSpawnTopLeft;
        public Vector2 tokenSpawnCenterBottomLeft;
        public Vector2 tokenSpawnCenterTopLeft;
        public Vector2 midpointCenterTopLeft;
        public Vector2 lastActiveCenterTopLeft;
        public Vector2 landingCenterTopLeft;
        public Vector2 expectedTargetLeftTop;
        public Vector2 expectedLandingCenterTopLeft;
        public Vector2 expectedUntransformedSizeTopLeft;
        public string tokenSprite;
        public string expectedSprite;
        public bool cleaned;
        public bool stampActive;
        public int activeTokensAfterCleanup;
        public int pooledTokensAfterCleanup;
        public int tokenTweensAfterCleanup;
        public List<RuntimePreviewFlightFrameProbe> frames = new List<RuntimePreviewFlightFrameProbe>();
    }

    internal sealed class RuntimePreviewItemBinding
    {
        internal string ItemId;
        internal string Name;
        internal string Description;
        internal string Key;
        internal bool Delisted;
    }

    internal static class RuntimePreviewMarketInteractionProbe
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static RuntimePreviewItemBinding Item(RuntimePreviewAdapter adapter, int cardIndex)
        {
            object card = Card(adapter, cardIndex);
            string itemId = Convert.ToString(GetField(card, "_itemId"), CultureInfo.InvariantCulture);
            object catalog = GetField(adapter, "_items");
            object item = Invoke(catalog, "GetItem", itemId);
            return new RuntimePreviewItemBinding
            {
                ItemId = itemId,
                Name = Convert.ToString(GetField(item, "Name"), CultureInfo.InvariantCulture),
                Description = Convert.ToString(GetField(item, "Description"), CultureInfo.InvariantCulture),
                Key = "mod:" + itemId,
                Delisted = Convert.ToBoolean(GetField(card, "_delisted"), CultureInfo.InvariantCulture)
            };
        }

        internal static string CardItemId(RuntimePreviewAdapter adapter, int cardIndex)
        {
            return Convert.ToString(GetField(Card(adapter, cardIndex), "_itemId"), CultureInfo.InvariantCulture);
        }

        internal static Text CardText(RuntimePreviewAdapter adapter, int cardIndex, string key)
        {
            object view = GetField(Card(adapter, cardIndex), "_view");
            return Invoke(view, "Text", key) as Text;
        }

        internal static Image CardItemImage(RuntimePreviewAdapter adapter, int cardIndex)
        {
            object view = GetField(Card(adapter, cardIndex), "_view");
            return Invoke(view, "Image", "ItemImage") as Image;
        }

        internal static Button MarketButton(RuntimePreviewAdapter adapter, string key)
        {
            object market = GetField(adapter, "_market");
            object view = GetProperty(market, "View");
            return Invoke(view, "Button", key) as Button;
        }

        internal static RectTransform CartRail(RuntimePreviewAdapter adapter)
        {
            object market = GetField(adapter, "_market");
            object view = GetProperty(market, "View");
            return Invoke(view, "Rect", "CartRail") as RectTransform;
        }

        internal static Text TooltipText(RuntimePreviewAdapter adapter, string key)
        {
            RectTransform root = adapter.TooltipRoot;
            Transform child = root == null ? null : root.Find(key);
            return child == null ? null : child.GetComponent<Text>();
        }

        internal static RuntimePreviewTooltipProbe CaptureTooltip(
            RuntimePreviewInteractionContext context,
            string label,
            float start,
            RuntimePreviewItemBinding expected)
        {
            RectTransform root = context.Adapter.TooltipRoot;
            CanvasGroup group = root == null ? null : root.GetComponent<CanvasGroup>();
            Text title = TooltipText(context.Adapter, "TitleText");
            Text body = TooltipText(context.Adapter, "BodyText");
            Text hint = TooltipText(context.Adapter, "HintText");
            Rect topLeft = ScreenRect(root, context.Height, true);
            RuntimePreviewTooltipProbe probe = new RuntimePreviewTooltipProbe
            {
                label = label,
                itemId = expected == null ? string.Empty : expected.ItemId,
                elapsedMs = context.ElapsedMs(start),
                expectedTitle = expected == null ? string.Empty : expected.Name,
                expectedBody = expected == null ? string.Empty : expected.Description,
                expectedHint = expected == null ? string.Empty : "点击查看行情 · " + expected.Key,
                title = title == null ? string.Empty : title.text,
                body = body == null ? string.Empty : body.text,
                hint = hint == null ? string.Empty : hint.text,
                horizontalOverflow = body == null ? string.Empty : body.horizontalOverflow.ToString(),
                bodyLineCount = body == null || !body.gameObject.activeSelf ? 0 : body.cachedTextGenerator.lineCount,
                groupAlpha = group == null ? 0f : group.alpha,
                titleColorAlpha = title == null ? 0f : title.color.a,
                bodyColorAlpha = body == null ? 0f : body.color.a,
                hintColorAlpha = hint == null ? 0f : hint.color.a,
                screenTopLeftMin = topLeft.min,
                screenTopLeftMax = topLeft.max
            };
            probe.effectiveTitleAlpha = probe.groupAlpha * probe.titleColorAlpha;
            probe.effectiveBodyAlpha = probe.groupAlpha * probe.bodyColorAlpha;
            probe.effectiveHintAlpha = probe.groupAlpha * probe.hintColorAlpha;
            context.Result.tooltipProbes.Add(probe);
            return probe;
        }

        internal static RuntimePreviewButtonProbe CaptureButton(
            RuntimePreviewInteractionContext context,
            Button button,
            string action,
            string phase,
            float start)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            Graphic background = button.targetGraphic == null
                ? button.GetComponent<Graphic>()
                : button.targetGraphic;
            Transform iconTransform = button.transform.Find("Icon");
            Graphic icon = iconTransform == null ? null : iconTransform.GetComponent<Graphic>();
            Vector2 center = Center(button.gameObject);
            Transform card = rect.transform;
            while (card != null && card.name != "MotionPivot")
                card = card.parent;
            RuntimePreviewButtonProbe probe = new RuntimePreviewButtonProbe
            {
                action = action,
                phase = phase,
                elapsedMs = context.ElapsedMs(start),
                centerBottomLeft = center,
                centerTopLeft = TopLeft(context, center),
                parentCenterBottomLeft = card == null ? Vector2.zero : Center(card.gameObject),
                localPosition = rect.anchoredPosition,
                localScale = rect.localScale,
                rotationZ = rect.localEulerAngles.z,
                backgroundTint = background == null ? Color.clear : background.color,
                iconTint = icon == null ? Color.clear : icon.color,
                detailOpened = !string.IsNullOrEmpty(context.Adapter.DetailItemId),
                png = string.Empty
            };
            context.Result.buttonProbes.Add(probe);
            return probe;
        }

        internal static void CaptureFacePixel(
            string png,
            Button button,
            int screenHeight,
            RuntimePreviewButtonProbe probe)
        {
            if (string.IsNullOrEmpty(png) || !File.Exists(png))
                return;
            Texture2D image = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(png), false))
                    return;
                RectTransform rect = button.GetComponent<RectTransform>();
                Rect local = rect.rect;
                Vector3 world = rect.TransformPoint(new Vector3(
                    local.xMin + local.width * 0.18f,
                    local.center.y,
                    0f));
                Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, world);
                int x = Mathf.Clamp(Mathf.RoundToInt(screen.x), 0, image.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(screen.y), 0, image.height - 1);
                probe.facePixel = image.GetPixel(x, y);
                probe.faceSampleTopLeft = new Vector2(screen.x, screenHeight - screen.y);
            }
            finally
            {
                UnityEngine.Object.Destroy(image);
            }
        }

        internal static RectTransform ActiveToken(RuntimePreviewAdapter adapter)
        {
            List<RectTransform> tokens = Tokens(adapter, true);
            return tokens.Count == 0 ? null : tokens[0];
        }

        internal static List<RectTransform> Tokens(RuntimePreviewAdapter adapter, bool activeOnly)
        {
            List<RectTransform> result = new List<RectTransform>();
            object overlays = GetField(adapter, "_overlays");
            object feedback = GetField(overlays, "_feedback");
            IList tokens = feedback == null ? null : GetField(feedback, "_tokens") as IList;
            for (int i = 0; tokens != null && i < tokens.Count; i++)
            {
                RectTransform root = GetProperty(tokens[i], "Root") as RectTransform;
                if (root != null && (!activeOnly || root.gameObject.activeSelf))
                    result.Add(root);
            }
            return result;
        }

        internal static Image TokenImage(RectTransform token)
        {
            Transform image = token == null ? null : token.Find("TokenImage");
            return image == null ? null : image.GetComponent<Image>();
        }

        internal static int TokenTweenCount(RuntimePreviewAdapter adapter)
        {
            object overlays = GetField(adapter, "_overlays");
            object feedback = GetField(overlays, "_feedback");
            IDictionary tweens = feedback == null ? null : GetField(feedback, "_tokenTweens") as IDictionary;
            return tweens == null ? 0 : tweens.Count;
        }

        internal static Vector2 ExpectedFlightTargetLeftTop(RectTransform cartRail, int screenHeight)
        {
            Rect rect = cartRail.rect;
            Vector3 world = cartRail.TransformPoint(new Vector3(
                rect.xMin + rect.width * 0.55f,
                rect.yMax - Mathf.Min(rect.height * 0.45f, 260f),
                0f));
            Canvas canvas = cartRail.GetComponentInParent<Canvas>().rootCanvas;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, world);
            return new Vector2(screen.x, screenHeight - screen.y);
        }

        internal static Vector2 ExpectedFlightLanding(
            RectTransform cartRail,
            RectTransform token,
            int screenHeight)
        {
            return ExpectedFlightTargetLeftTop(cartRail, screenHeight) +
                UntransformedTokenSize(token) * 0.5f;
        }

        internal static Vector2 ExpectedFlightTokenSize(int width, int height)
        {
            float size = width >= 2304 && height >= 1296 ? 82.5f : 55f;
            return new Vector2(size, size);
        }

        internal static Vector2 UntransformedTokenSize(RectTransform token)
        {
            if (token == null)
                return Vector2.zero;
            Vector2 delta = ScreenDelta(token, token.rect.size);
            return new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        }

        internal static RuntimePreviewFlightFrameProbe CaptureFlightFrame(
            RuntimePreviewInteractionContext context,
            RectTransform token,
            string phase,
            float start)
        {
            Rect bounds = ScreenRect(token, context.Height, true);
            CanvasGroup group = token.GetComponent<CanvasGroup>();
            Image itemImage = TokenImage(token);
            RuntimePreviewFlightFrameProbe frame = new RuntimePreviewFlightFrameProbe
            {
                phase = phase,
                elapsedMs = context.ElapsedMs(start),
                localSize = token.rect.size,
                untransformedSizeTopLeft = UntransformedTokenSize(token),
                itemImageSizeTopLeft = itemImage == null ? Vector2.zero : UntransformedTokenSize(itemImage.rectTransform),
                transformedBoundsMinTopLeft = bounds.min,
                transformedBoundsMaxTopLeft = bounds.max,
                transformedSizeTopLeft = bounds.size,
                centerTopLeft = TopLeft(context, Center(token.gameObject)),
                localScale = token.localScale,
                rotationZ = Mathf.DeltaAngle(0f, token.localEulerAngles.z),
                alpha = group == null ? 0f : group.alpha,
                png = string.Empty
            };
            List<RuntimePreviewTokenDiagnostic> diagnostics = context.Adapter.TokenDiagnostics();
            if (diagnostics.Count > 0)
            {
                frame.tweenPosition = diagnostics[0].position;
                frame.tweenDuration = diagnostics[0].duration;
            }
            return frame;
        }

        internal static Vector2 Center(GameObject target)
        {
            return RuntimePreviewPointerDriver.Center(target);
        }

        internal static Vector2 TopLeft(RuntimePreviewInteractionContext context, Vector2 bottomLeft)
        {
            return new Vector2(bottomLeft.x, context.Height - bottomLeft.y);
        }

        internal static Vector2 ScreenDelta(RectTransform rect, Vector2 local)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Vector3 origin = rect.parent.TransformPoint(Vector3.zero);
            Vector3 world = rect.parent.TransformPoint(new Vector3(local.x, local.y, 0f));
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return new Vector2(world.x - origin.x, world.y - origin.y);
            Camera camera = canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
            return camera.WorldToScreenPoint(world) - camera.WorldToScreenPoint(origin);
        }

        internal static Rect ScreenRect(RectTransform rect, int screenHeight, bool topLeft)
        {
            if (rect == null)
                return new Rect();
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera == null ? Camera.main : canvas.worldCamera;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                if (topLeft)
                    screen.y = screenHeight - screen.y;
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            return new Rect(min, max - min);
        }

        internal static RuntimePreviewPointerEvidence Down(
            RuntimePreviewInteractionContext context,
            GameObject target)
        {
            PointerEventData data;
            RaycastResult hit;
            List<RaycastResult> hits;
            Vector2 point = Center(target);
            Raycast(context, point, out data, out hit, out hits);
            RuntimePreviewPointerEvidence evidence = Evidence(context, "pointer-down-hold", target, point, data, hits);
            if (!IsTargetOrChild(hit.gameObject, target))
            {
                context.Result.pointerEvents.Add(evidence);
                context.Fail("Held pointerDown missed its raycast target; no fallback was used: " + PathOf(target));
                return evidence;
            }
            data.button = PointerEventData.InputButton.Left;
            data.pressPosition = point;
            data.pointerPressRaycast = hit;
            data.eligibleForClick = true;
            data.clickCount = 1;
            GameObject press = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
            context.PointerPress = press;
            evidence.pointerPress = PathOf(press);
            evidence.directGraphicRaycastVerified = DirectHitVerified(data, hit);
            context.Result.pointerEvents.Add(evidence);
            context.Check(evidence.directGraphicRaycastVerified,
                "Held pointerDown was not verified by direct GraphicRaycaster.Raycast: " + PathOf(target));
            return evidence;
        }

        internal static RuntimePreviewPointerEvidence UpAndClick(
            RuntimePreviewInteractionContext context,
            GameObject target)
        {
            PointerEventData data;
            RaycastResult hit;
            List<RaycastResult> hits;
            Vector2 point = Center(target);
            Raycast(context, point, out data, out hit, out hits);
            RuntimePreviewPointerEvidence evidence = Evidence(context, "pointer-up-click-after-hold", target, point, data, hits);
            GameObject press = context.PointerPress;
            evidence.pointerPress = PathOf(press);
            if (press == null || !IsTargetOrChild(hit.gameObject, target))
            {
                context.Result.pointerEvents.Add(evidence);
                context.Fail("Held pointer release lost its press or raycast target; no fallback was used: " + PathOf(target));
                context.PointerPress = null;
                return evidence;
            }
            data.button = PointerEventData.InputButton.Left;
            data.pressPosition = point;
            data.pointerPressRaycast = hit;
            data.eligibleForClick = true;
            data.clickCount = 1;
            ExecuteEvents.Execute(press, data, ExecuteEvents.pointerUpHandler);
            GameObject click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            evidence.pointerClick = PathOf(click);
            evidence.directGraphicRaycastVerified = DirectHitVerified(data, hit);
            if (press == click)
            {
                ExecuteEvents.Execute(press, data, ExecuteEvents.pointerClickHandler);
                evidence.clickDispatched = true;
            }
            context.PointerPress = null;
            context.Result.pointerEvents.Add(evidence);
            context.Check(evidence.directGraphicRaycastVerified && evidence.clickDispatched,
                "Held pointer release did not complete the verified GraphicRaycaster pointer pipeline: " + PathOf(target));
            return evidence;
        }

        private static object Card(RuntimePreviewAdapter adapter, int index)
        {
            object market = GetField(adapter, "_market");
            IList cards = GetField(market, "_cards") as IList;
            if (cards == null || index < 0 || index >= cards.Count)
                throw new ArgumentOutOfRangeException("index");
            return cards[index];
        }

        private static void Raycast(
            RuntimePreviewInteractionContext context,
            Vector2 point,
            out PointerEventData data,
            out RaycastResult hit,
            out List<RaycastResult> hits)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                throw new InvalidOperationException("The market probe requires EventSystem.current.");
            eventSystem.enabled = true;
            data = new PointerEventData(eventSystem) { position = point };
            hits = new List<RaycastResult>();
            eventSystem.RaycastAll(data, hits);
            hit = hits.Count == 0 ? default(RaycastResult) : hits[0];
            data.pointerCurrentRaycast = hit;
        }

        private static bool DirectHitVerified(PointerEventData data, RaycastResult hit)
        {
            GraphicRaycaster raycaster = hit.module as GraphicRaycaster;
            if (raycaster == null || hit.gameObject == null)
                return false;
            List<RaycastResult> direct = new List<RaycastResult>();
            raycaster.Raycast(data, direct);
            for (int i = 0; i < direct.Count; i++)
            {
                if (direct[i].gameObject == hit.gameObject)
                    return true;
            }
            return false;
        }

        private static RuntimePreviewPointerEvidence Evidence(
            RuntimePreviewInteractionContext context,
            string eventType,
            GameObject target,
            Vector2 point,
            PointerEventData data,
            List<RaycastResult> hits)
        {
            RuntimePreviewPointerEvidence evidence = new RuntimePreviewPointerEvidence
            {
                eventType = eventType,
                screenPositionBottomLeft = point,
                screenPositionTopLeft = TopLeft(context, point),
                expectedTarget = PathOf(target),
                raycastHit = PathOf(data.pointerCurrentRaycast.gameObject),
                raycaster = data.pointerCurrentRaycast.module == null
                    ? string.Empty
                    : data.pointerCurrentRaycast.module.GetType().FullName,
                directGraphicRaycastVerified = DirectHitVerified(data, data.pointerCurrentRaycast)
            };
            for (int i = 0; i < hits.Count && i < 8; i++)
            {
                evidence.hits.Add(new RuntimePreviewRaycastHitEvidence
                {
                    path = PathOf(hits[i].gameObject),
                    module = hits[i].module == null ? string.Empty : hits[i].module.GetType().FullName,
                    depth = hits[i].depth,
                    sortingOrder = hits[i].sortingOrder
                });
            }
            return evidence;
        }

        private static bool IsTargetOrChild(GameObject hit, GameObject target)
        {
            return hit != null && target != null && (hit == target || hit.transform.IsChildOf(target.transform));
        }

        private static string PathOf(GameObject target)
        {
            if (target == null)
                return string.Empty;
            string result = target.name;
            Transform current = target.transform.parent;
            while (current != null)
            {
                result = current.name + "/" + result;
                current = current.parent;
            }
            return result;
        }

        private static object GetField(object target, string name)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot read field " + name + " on null.");
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, All | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
                type = type.BaseType;
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }

        private static object GetProperty(object target, string name)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot read property " + name + " on null.");
            Type type = target.GetType();
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(name, All | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property.GetValue(target, null);
                type = type.BaseType;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot invoke " + name + " on null.");
            Type type = target.GetType();
            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(All | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    ParameterInfo[] parameters = methods[i].GetParameters();
                    if (methods[i].Name != name || parameters.Length != arguments.Length)
                        continue;
                    bool compatible = true;
                    for (int p = 0; p < parameters.Length; p++)
                    {
                        if (arguments[p] == null || parameters[p].ParameterType.IsInstanceOfType(arguments[p]))
                            continue;
                        compatible = false;
                        break;
                    }
                    if (compatible)
                        return methods[i].Invoke(target, arguments);
                }
                type = type.BaseType;
            }
            throw new MissingMethodException(target.GetType().FullName, name);
        }
    }
}
