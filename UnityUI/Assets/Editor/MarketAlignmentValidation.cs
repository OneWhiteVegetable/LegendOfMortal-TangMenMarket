using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class RuntimePreviewComponentRectEvidence
    {
        public string key;
        public string path;
        public string source;
        public VisualRectSemantic screenBottomLeft;
        public VisualRectSemantic screenTopLeft;
        public string pixelProbe;
    }

    [Serializable]
    public sealed class RuntimePreviewPixelProbeEvidence
    {
        public string key;
        public string path;
        public string predicate;
        public bool supported;
        public string detail;
        public int observedStart = -1;
        public int observedEnd = -1;
        public int observedSecondaryStart = -1;
        public int observedSecondaryEnd = -1;
        public int matchingPixels;
    }

    [Serializable]
    public sealed class RuntimePreviewRaycastHitEvidence
    {
        public string path;
        public string module;
        public int depth;
        public int sortingOrder;
    }

    [Serializable]
    public sealed class RuntimePreviewPointerEvidence
    {
        public string eventType;
        public string method = "EventSystem.RaycastAll + direct GraphicRaycaster.Raycast verification + ExecuteEvents pointer pipeline";
        public Vector2 screenPositionBottomLeft;
        public Vector2 screenPositionTopLeft;
        public string expectedTarget;
        public string raycastHit;
        public string raycaster;
        public string previousPointerEnter;
        public string pointerEnter;
        public string nativeDispatch;
        public int pointerDataId;
        public int nativeMoveSerial;
        public bool nativeDispatchInvoked;
        public string pointerPress;
        public string pointerClick;
        public bool directGraphicRaycastVerified;
        public bool clickDispatched;
        public bool fallbackUsed;
        public string fallbackPolicy = "Direct event injection is diagnostic only and fails the physical-hit requirement.";
        public List<RuntimePreviewRaycastHitEvidence> hits = new List<RuntimePreviewRaycastHitEvidence>();
    }

    public static class MarketAlignmentValidation
    {
        public static List<RuntimePreviewComponentRectEvidence> CaptureComponents(VisualSemanticSnapshot snapshot)
        {
            List<RuntimePreviewComponentRectEvidence> result = new List<RuntimePreviewComponentRectEvidence>();
            AddImage(result, snapshot, "window", "/Window#", string.Empty, string.Empty);
            AddLayoutOrImage(result, snapshot, "masthead", "/Masthead#", string.Empty);
            AddImage(result, snapshot, "ticker", "/NewsTicker#", "/Background#", string.Empty);
            AddImage(result, snapshot, "ticker-label", "/NewsTicker#", "/LabelBackground#", "ticker-label-red");
            AddLayout(result, snapshot, "goods-grid", "/GoodsGrid#");
            AddImage(result, snapshot, "first-card", "/GoodsRow0#", "/GoodsCard00#", "/VisualBackground#", "first-card-top");
            AddImage(result, snapshot, "second-row-card", "/GoodsRow1#", "/GoodsCard03#", "/VisualBackground#", "second-row-card-top");
            AddImage(result, snapshot, "first-item-image", "/GoodsRow0#", "/GoodsCard00#", "/ItemImage#", string.Empty);
            AddText(result, snapshot, "first-price", "/GoodsRow0#", "/PriceText#", string.Empty);
            AddText(result, snapshot, "first-price-unit", "/GoodsRow0#", "/PriceUnitText#", "price-unit-ink");
            AddText(result, snapshot, "first-delta", "/GoodsRow0#", "/DeltaText#", string.Empty);
            AddText(result, snapshot, "first-change", "/GoodsRow0#", "/ChangeText#", string.Empty);
            AddImage(result, snapshot, "first-quote", "/GoodsRow0#", "/GoodsCard00#", "/QuoteBack#", string.Empty);
            AddImage(result, snapshot, "first-change-back", "/GoodsRow0#", "/GoodsCard00#", "/ChangeBack#", string.Empty);
            AddImage(result, snapshot, "first-halftone", "/GoodsRow0#", "/GoodsCard00#", "/Halftone#", "halftone-ink");
            AddImage(result, snapshot, "first-quantity-plus", "/GoodsRow0#", "/IncrementButton#", "/Icon#", "quantity-icon-ink");
            AddLayoutOrImage(result, snapshot, "quantity-stepper", "/GoodsRow0#", "/QuantityStepper#");
            AddButton(result, snapshot, "buy-button", "/GoodsRow0#", "/BuyButton#");
            AddButton(result, snapshot, "borrow-button", "/GoodsRow0#", "/BorrowButton#");
            AddImage(result, snapshot, "cart-rail", "/CartRail#", string.Empty, string.Empty, string.Empty);
            AddImage(result, snapshot, "cart-divider", "/CartRail#", "/GoldEdge#", "cart-divider-gold");
            AddImage(result, snapshot, "cart-footer", "/CartFooter#", string.Empty, string.Empty, string.Empty);
            AddImage(result, snapshot, "checkout", "/CheckoutButton#", string.Empty, "checkout-red");
            AddImage(result, snapshot, "empty-icon", "/CartEmpty#", "/EmptyIcon#", "empty-icon-ink");
            AddText(result, snapshot, "empty-title", "/CartEmpty#", "/TitleText#", "empty-title-ink");
            return result;
        }

        public static List<RuntimePreviewPixelProbeEvidence> ReviewPng(
            string path,
            VisualSemanticSnapshot snapshot)
        {
            List<RuntimePreviewPixelProbeEvidence> result = new List<RuntimePreviewPixelProbeEvidence>();
            Texture2D image = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(path), false))
                    throw new InvalidDataException("Cannot decode native PNG for market pixel probes: " + path);
                if (image.width != snapshot.requestedWidth || image.height != snapshot.requestedHeight)
                    throw new InvalidDataException("Pixel probe PNG dimensions differ from the semantic viewport: " + path);
                for (int i = 0; i < snapshot.keyComponents.Count; i++)
                {
                    RuntimePreviewComponentRectEvidence component = snapshot.keyComponents[i];
                    if (string.IsNullOrEmpty(component.pixelProbe))
                        continue;
                    if (component.pixelProbe == "first-card-top" || component.pixelProbe == "second-row-card-top")
                        result.Add(CardTop(image, component));
                    else if (component.pixelProbe == "ticker-label-red")
                        result.Add(TickerLabel(image, component));
                    else if (component.pixelProbe == "checkout-red")
                        result.Add(Checkout(
                            image,
                            component,
                            snapshot.caseName == "market-empty",
                            snapshot.requestedWidth == 1920 && snapshot.requestedHeight == 1080 ? 1590 : -1));
                    else if (component.pixelProbe == "cart-divider-gold")
                        result.Add(CartDivider(image, component));
                    else if (component.pixelProbe == "empty-icon-ink")
                        result.Add(EmptyInk(image, component, true));
                    else if (component.pixelProbe == "empty-title-ink")
                        result.Add(EmptyInk(image, component, false));
                    else if (component.pixelProbe == "price-unit-ink" ||
                        component.pixelProbe == "quantity-icon-ink" ||
                        component.pixelProbe == "halftone-ink")
                        result.Add(SubtleInk(image, component));
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(image);
            }
            return result;
        }

        public static List<string> ValidateMarketPixels(
            VisualSemanticSnapshot snapshot,
            List<RuntimePreviewPixelProbeEvidence> probes)
        {
            List<string> failures = new List<string>();
            if (snapshot.caseName != "market" && snapshot.caseName != "market-empty")
                return failures;
            RequireProbe(failures, probes, "first-card-top");
            RequireProbe(failures, probes, "second-row-card-top");
            RequireProbe(failures, probes, "ticker-label-red");
            RequireProbe(failures, probes, "checkout-red");
            RequireProbe(failures, probes, "cart-divider-gold");
            RequireProbe(failures, probes, "price-unit-ink");
            RequireProbe(failures, probes, "quantity-icon-ink");
            RequireProbe(failures, probes, "halftone-ink");
            ValidateCardDateLabels(snapshot, failures);
            if (snapshot.caseName == "market-empty")
            {
                RequireProbe(failures, probes, "empty-icon-ink");
                RequireProbe(failures, probes, "empty-title-ink");
            }
            return failures;
        }

        private static void ValidateCardDateLabels(
            VisualSemanticSnapshot snapshot,
            List<string> failures)
        {
            Dictionary<string, List<VisualTextSemantic>> cards = new Dictionary<string, List<VisualTextSemantic>>();
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                VisualTextSemantic text = snapshot.texts[i];
                int chart = text.path.IndexOf("/ChartHost#", StringComparison.Ordinal);
                if (chart < 0 || text.path.IndexOf("/AxisLabel#", StringComparison.Ordinal) < 0 ||
                    text.text.IndexOf("年", StringComparison.Ordinal) < 0 ||
                    text.text.IndexOf("周", StringComparison.Ordinal) < 0)
                    continue;
                if (text.text.IndexOf("市周", StringComparison.Ordinal) >= 0)
                    failures.Add("Card axis uses the long detail date instead of the short HTML label: " + text.path);
                string card = text.path.Substring(0, chart);
                List<VisualTextSemantic> labels;
                if (!cards.TryGetValue(card, out labels))
                {
                    labels = new List<VisualTextSemantic>();
                    cards.Add(card, labels);
                }
                labels.Add(text);
            }
            foreach (KeyValuePair<string, List<VisualTextSemantic>> pair in cards)
            {
                List<VisualTextSemantic> labels = pair.Value;
                if (labels.Count > 4)
                    failures.Add("Card has more than four date labels: " + pair.Key);
                for (int i = 0; i < labels.Count; i++)
                {
                    for (int j = i + 1; j < labels.Count; j++)
                    {
                        VisualRectSemantic a = labels[i].rect;
                        VisualRectSemantic b = labels[j].rect;
                        bool overlap = a.x < b.x + b.width - 0.5f && a.x + a.width > b.x + 0.5f &&
                            a.y < b.y + b.height - 0.5f && a.y + a.height > b.y + 0.5f;
                        if (overlap)
                            failures.Add("Card date labels overlap: " + labels[i].path + " <-> " + labels[j].path);
                    }
                }
            }
        }

        private static void RequireProbe(
            List<string> failures,
            List<RuntimePreviewPixelProbeEvidence> probes,
            string key)
        {
            RuntimePreviewPixelProbeEvidence found = null;
            for (int i = 0; i < probes.Count; i++)
            {
                if (probes[i].key == key)
                {
                    found = probes[i];
                    break;
                }
            }
            if (found == null)
                failures.Add("Required visible-pixel probe is absent: " + key);
            else if (!found.supported)
                failures.Add("Visible-pixel probe failed: " + key + "; " + found.detail);
        }

        private static RuntimePreviewPixelProbeEvidence CardTop(
            Texture2D image,
            RuntimePreviewComponentRectEvidence component)
        {
            VisualRectSemantic rect = component.screenBottomLeft;
            int x0 = Clamp(Mathf.RoundToInt(rect.x) + 3, 0, image.width - 1);
            int x1 = Clamp(Mathf.RoundToInt(rect.x + rect.width) - 20, x0 + 1, image.width);
            int expectedTop = Mathf.RoundToInt(rect.y + rect.height);
            int y0 = Clamp(expectedTop - 9, 0, image.height - 1);
            int y1 = Clamp(expectedTop + 9, y0 + 1, image.height);
            int bestY = -1;
            Run best = null;
            for (int y = y0; y < y1; y++)
            {
                Run run = LongestRun(image, x0, x1, y, true, Dark);
                if (run == null)
                    continue;
                run.Start += x0;
                run.End += x0;
                if (best == null || run.Length > best.Length)
                {
                    best = run;
                    bestY = y;
                }
            }
            bool supported = best != null && best.Length >= (x1 - x0) * 0.55f;
            return new RuntimePreviewPixelProbeEvidence
            {
                key = component.pixelProbe,
                path = component.path,
                predicate = "long dark horizontal border in expected top ±9 native px",
                supported = supported,
                observedStart = best == null ? bestY : best.Start,
                observedEnd = best == null ? -1 : best.End,
                observedSecondaryStart = bestY,
                detail = "expectedTop=" + expectedTop.ToString(CultureInfo.InvariantCulture) +
                    "; observedY=" + bestY.ToString(CultureInfo.InvariantCulture) +
                    "; run=" + (best == null ? "none" : best.Length.ToString(CultureInfo.InvariantCulture))
            };
        }

        private static RuntimePreviewPixelProbeEvidence TickerLabel(
            Texture2D image,
            RuntimePreviewComponentRectEvidence component)
        {
            VisualRectSemantic rect = component.screenBottomLeft;
            int y = Clamp(Mathf.RoundToInt(rect.y + rect.height * 0.5f), 0, image.height - 1);
            int x0 = Clamp(Mathf.RoundToInt(rect.x) - 2, 0, image.width - 1);
            int x1 = Clamp(Mathf.RoundToInt(rect.x + rect.width + 24f), x0 + 1, image.width);
            Run envelope = ColorEnvelope(image, x0, x1, y, Red);
            if (envelope != null)
            {
                envelope.Start += x0;
                envelope.End += x0;
            }
            bool supported = envelope != null && envelope.MatchingPixels >= rect.width * 0.35f;
            return new RuntimePreviewPixelProbeEvidence
            {
                key = component.pixelProbe,
                path = component.path,
                predicate = "R>120 AND R>1.8G; outer red runs enclose text cutouts",
                supported = supported,
                observedStart = envelope == null ? -1 : envelope.Start,
                observedEnd = envelope == null ? -1 : envelope.End,
                matchingPixels = envelope == null ? 0 : envelope.MatchingPixels,
                detail = "scanY=" + y.ToString(CultureInfo.InvariantCulture) +
                    "; observedRight=" + (envelope == null ? -1 : envelope.End).ToString(CultureInfo.InvariantCulture)
            };
        }

        private static RuntimePreviewPixelProbeEvidence Checkout(
            Texture2D image,
            RuntimePreviewComponentRectEvidence component,
            bool disabled,
            int referenceX)
        {
            VisualRectSemantic rect = component.screenBottomLeft;
            Func<Color32, bool> predicate = disabled ? (Func<Color32, bool>)DisabledRed : Red;
            int x = referenceX >= 0
                ? Clamp(referenceX, 0, image.width - 1)
                : Clamp(Mathf.RoundToInt(rect.x + Math.Min(32f, rect.width * 0.1f)), 0, image.width - 1);
            int y0 = Clamp(Mathf.RoundToInt(rect.y - 8f), 0, image.height - 1);
            int y1 = Clamp(Mathf.RoundToInt(rect.y + rect.height + 8f), y0 + 1, image.height);
            Run vertical = LongestVerticalRun(image, x, y0, y1, predicate);
            if (vertical != null)
            {
                vertical.Start += y0;
                vertical.End += y0;
            }
            int y = Clamp(Mathf.RoundToInt(rect.y + rect.height * 0.5f), 0, image.height - 1);
            int x0 = Clamp(Mathf.RoundToInt(rect.x - 8f), 0, image.width - 1);
            int x1 = Clamp(Mathf.RoundToInt(rect.x + rect.width + 8f), x0 + 1, image.width);
            Run horizontal = ColorEnvelope(image, x0, x1, y, predicate);
            if (horizontal != null)
            {
                horizontal.Start += x0;
                horizontal.End += x0;
            }
            bool supported = vertical != null && horizontal != null &&
                vertical.Length >= rect.height * 0.55f && horizontal.Length >= rect.width * 0.9f;
            return new RuntimePreviewPixelProbeEvidence
            {
                key = component.pixelProbe,
                path = component.path,
                predicate = disabled
                    ? "disabled checkout dark-red face; vertical longest run plus horizontal color envelope"
                    : "enabled checkout red face; vertical longest run plus horizontal red envelope",
                supported = supported,
                observedStart = vertical == null ? -1 : vertical.Start,
                observedEnd = vertical == null ? -1 : vertical.End,
                observedSecondaryStart = horizontal == null ? -1 : horizontal.Start,
                observedSecondaryEnd = horizontal == null ? -1 : horizontal.End,
                matchingPixels = horizontal == null ? 0 : horizontal.MatchingPixels,
                detail = "verticalX=" + x.ToString(CultureInfo.InvariantCulture) +
                    "; horizontalY=" + y.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static RuntimePreviewPixelProbeEvidence CartDivider(
            Texture2D image,
            RuntimePreviewComponentRectEvidence component)
        {
            VisualRectSemantic rect = component.screenBottomLeft;
            int y = Clamp(Mathf.RoundToInt(rect.y + rect.height * 0.5f), 0, image.height - 1);
            int x0 = Clamp(Mathf.RoundToInt(rect.x - 3f), 0, image.width - 1);
            int x1 = Clamp(Mathf.RoundToInt(rect.x + rect.width + 3f), x0 + 1, image.width);
            Run run = LongestRun(image, x0, x1, y, true, Gold);
            if (run != null)
            {
                run.Start += x0;
                run.End += x0;
            }
            bool supported = run != null && run.Length >= Math.Max(2f, rect.width * 0.6f);
            return new RuntimePreviewPixelProbeEvidence
            {
                key = component.pixelProbe,
                path = component.path,
                predicate = "gold chroma inside the CartRail GoldEdge native rectangle",
                supported = supported,
                observedStart = run == null ? -1 : run.Start,
                observedEnd = run == null ? -1 : run.End,
                detail = "scanY=" + y.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static RuntimePreviewPixelProbeEvidence EmptyInk(
            Texture2D image,
            RuntimePreviewComponentRectEvidence component,
            bool icon)
        {
            VisualRectSemantic rect = component.screenBottomLeft;
            int x0 = Clamp(Mathf.RoundToInt(rect.x), 0, image.width - 1);
            int x1 = Clamp(Mathf.RoundToInt(rect.x + rect.width), x0 + 1, image.width);
            int y0 = Clamp(Mathf.RoundToInt(rect.y), 0, image.height - 1);
            int y1 = Clamp(Mathf.RoundToInt(rect.y + rect.height), y0 + 1, image.height);
            int count = 0;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = -1;
            int maxY = -1;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    Color32 pixel = image.GetPixel(x, y);
                    bool match = icon
                        ? pixel.r > 65 && pixel.g > 48 && pixel.r > pixel.b * 1.25f && pixel.g > pixel.b * 1.05f
                        : pixel.r > 95 && pixel.g > 85 && pixel.b > 65;
                    if (!match)
                        continue;
                    count++;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
            bool supported = count > (icon ? 20 : 8);
            return new RuntimePreviewPixelProbeEvidence
            {
                key = component.pixelProbe,
                path = component.path,
                predicate = icon ? "actual gold empty-icon pixels" : "actual light empty-title pixels",
                supported = supported,
                matchingPixels = count,
                observedStart = count == 0 ? -1 : minX,
                observedEnd = count == 0 ? -1 : maxX,
                observedSecondaryStart = count == 0 ? -1 : minY,
                observedSecondaryEnd = count == 0 ? -1 : maxY,
                detail = "matchingPixels=" + count.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static RuntimePreviewPixelProbeEvidence SubtleInk(
            Texture2D image,
            RuntimePreviewComponentRectEvidence component)
        {
            VisualRectSemantic rect = component.screenBottomLeft;
            int x0 = Clamp(Mathf.RoundToInt(rect.x), 0, image.width - 1);
            int x1 = Clamp(Mathf.RoundToInt(rect.x + rect.width), x0 + 1, image.width);
            int y0 = Clamp(Mathf.RoundToInt(rect.y), 0, image.height - 1);
            int y1 = Clamp(Mathf.RoundToInt(rect.y + rect.height), y0 + 1, image.height);
            int count = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    Color32 pixel = image.GetPixel(x, y);
                    bool match = component.pixelProbe == "halftone-ink"
                        ? pixel.r < 242 && pixel.g < 242 && pixel.b < 242 && pixel.g >= pixel.r && pixel.g > pixel.b + 2
                        : pixel.r < 190 && pixel.g < 190 && pixel.b < 190;
                    if (match)
                        count++;
                }
            }
            int required = component.pixelProbe == "halftone-ink" ? 10 : 2;
            return new RuntimePreviewPixelProbeEvidence
            {
                key = component.pixelProbe,
                path = component.path,
                predicate = component.pixelProbe == "halftone-ink"
                    ? "actual low-contrast celadon dot pixels behind the item"
                    : "actual dark glyph/icon pixels inside the target rectangle",
                supported = count >= required,
                matchingPixels = count,
                detail = "matchingPixels=" + count.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static bool Dark(Color32 pixel)
        {
            int max = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
            int min = Math.Min(pixel.r, Math.Min(pixel.g, pixel.b));
            return pixel.r < 190 && pixel.g < 190 && pixel.b < 190 && max - min < 65;
        }

        private static bool Red(Color32 pixel)
        {
            return pixel.r > 120 && pixel.r > 1.8f * pixel.g;
        }

        private static bool DisabledRed(Color32 pixel)
        {
            return pixel.r > 35 && pixel.r > pixel.g * 1.3f && pixel.r > pixel.b * 1.1f;
        }

        private static bool Gold(Color32 pixel)
        {
            return pixel.r > 105 && pixel.g > 75 && pixel.b < 145 &&
                pixel.r > pixel.g * 1.05f && pixel.g > pixel.b * 1.12f;
        }

        private static Run LongestRun(
            Texture2D image,
            int x0,
            int x1,
            int y,
            bool horizontal,
            Func<Color32, bool> predicate)
        {
            Run best = null;
            Run current = null;
            for (int x = x0; x < x1; x++)
            {
                bool match = predicate(image.GetPixel(x, y));
                if (match && current == null)
                    current = new Run { Start = x, End = x, MatchingPixels = 0 };
                if (match)
                {
                    current.End = x;
                    current.MatchingPixels++;
                }
                if (current != null && (!match || x == x1 - 1))
                {
                    if (best == null || current.Length > best.Length)
                        best = current;
                    current = null;
                }
            }
            return best;
        }

        private static Run LongestVerticalRun(
            Texture2D image,
            int x,
            int y0,
            int y1,
            Func<Color32, bool> predicate)
        {
            Run best = null;
            Run current = null;
            for (int y = y0; y < y1; y++)
            {
                bool match = predicate(image.GetPixel(x, y));
                if (match && current == null)
                    current = new Run { Start = y, End = y, MatchingPixels = 0 };
                if (match)
                {
                    current.End = y;
                    current.MatchingPixels++;
                }
                if (current != null && (!match || y == y1 - 1))
                {
                    if (best == null || current.Length > best.Length)
                        best = current;
                    current = null;
                }
            }
            return best;
        }

        private static Run ColorEnvelope(
            Texture2D image,
            int x0,
            int x1,
            int y,
            Func<Color32, bool> predicate)
        {
            Run envelope = null;
            int matchingPixels = 0;
            int runCount = 0;
            for (int x = x0; x < x1; x++)
            {
                if (!predicate(image.GetPixel(x, y)))
                    continue;
                matchingPixels++;
                if (envelope == null)
                    envelope = new Run { Start = x, End = x };
                envelope.End = x;
            }
            if (envelope == null)
                return null;
            for (int x = x0; x < x1; x++)
            {
                if (predicate(image.GetPixel(x, y)) && (x == x0 || !predicate(image.GetPixel(x - 1, y))))
                    runCount++;
            }
            envelope.MatchingPixels = matchingPixels;
            envelope.RunCount = runCount;
            return envelope;
        }

        private static int Clamp(int value, int low, int high)
        {
            return Math.Max(low, Math.Min(high, value));
        }

        private sealed class Run
        {
            public int Start;
            public int End;
            public int MatchingPixels;
            public int RunCount;
            public int Length { get { return End - Start + 1; } }
        }

        private static VisualRectSemantic TopLeft(VisualRectSemantic rect, int height)
        {
            return new VisualRectSemantic
            {
                x = rect.x,
                y = height - rect.y - rect.height,
                width = rect.width,
                height = rect.height
            };
        }

        private static void AddImage(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string first,
            string second,
            string probe)
        {
            AddImage(result, snapshot, key, first, string.Empty, second, probe);
        }

        private static void AddImage(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string first,
            string second,
            string third,
            string probe)
        {
            for (int i = 0; i < snapshot.images.Count; i++)
            {
                VisualImageSemantic image = snapshot.images[i];
                if (!Contains(image.path, first, second, third))
                    continue;
                Add(result, snapshot, key, image.path, "Image", image.rect, probe);
                return;
            }
        }

        private static void AddText(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string first,
            string second,
            string probe)
        {
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                VisualTextSemantic text = snapshot.texts[i];
                if (!Contains(text.path, first, second, string.Empty))
                    continue;
                Add(result, snapshot, key, text.path, "Text", text.rect, probe);
                return;
            }
        }

        private static void AddButton(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string first,
            string second)
        {
            for (int i = 0; i < snapshot.buttons.Count; i++)
            {
                VisualButtonSemantic button = snapshot.buttons[i];
                if (!Contains(button.path, first, second, string.Empty))
                    continue;
                Add(result, snapshot, key, button.path, "Button", button.rect, string.Empty);
                return;
            }
        }

        private static void AddLayout(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string path)
        {
            for (int i = 0; i < snapshot.layouts.Count; i++)
            {
                VisualLayoutSemantic layout = snapshot.layouts[i];
                if (layout.path.IndexOf(path, StringComparison.Ordinal) < 0)
                    continue;
                Add(result, snapshot, key, layout.path, "Layout", layout.rect, string.Empty);
                return;
            }
        }

        private static void AddLayoutOrImage(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string first,
            string second)
        {
            for (int i = 0; i < snapshot.layouts.Count; i++)
            {
                VisualLayoutSemantic layout = snapshot.layouts[i];
                if (!Contains(layout.path, first, second, string.Empty))
                    continue;
                Add(result, snapshot, key, layout.path, "Layout", layout.rect, string.Empty);
                return;
            }
            AddImage(result, snapshot, key, first, second, string.Empty);
        }

        private static bool Contains(string path, string first, string second, string third)
        {
            return (string.IsNullOrEmpty(first) || path.IndexOf(first, StringComparison.Ordinal) >= 0) &&
                (string.IsNullOrEmpty(second) || path.IndexOf(second, StringComparison.Ordinal) >= 0) &&
                (string.IsNullOrEmpty(third) || path.IndexOf(third, StringComparison.Ordinal) >= 0);
        }

        private static void Add(
            List<RuntimePreviewComponentRectEvidence> result,
            VisualSemanticSnapshot snapshot,
            string key,
            string path,
            string source,
            VisualRectSemantic rect,
            string probe)
        {
            result.Add(new RuntimePreviewComponentRectEvidence
            {
                key = key,
                path = path,
                source = source,
                screenBottomLeft = rect,
                screenTopLeft = TopLeft(rect, snapshot.requestedHeight),
                pixelProbe = probe
            });
        }
    }

    internal static class RuntimePreviewPointerDriver
    {
        private static readonly ConditionalWeakTable<RuntimePreviewInteractionContext, NativePointerState> NativePointers =
            new ConditionalWeakTable<RuntimePreviewInteractionContext, NativePointerState>();
        private static readonly MethodInfo NativeExitAndEnter = typeof(BaseInputModule).GetMethod(
            "HandlePointerExitAndEnter", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class NativePointerState
        {
            internal PointerEventData Data;
            internal int Serial;
        }

        internal static RuntimePreviewPointerEvidence Move(
            RuntimePreviewInteractionContext context,
            GameObject target,
            bool inside)
        {
            return MoveAt(context, target, inside ? Center(target) : new Vector2(1f, 1f), inside);
        }

        internal static RuntimePreviewPointerEvidence MoveAt(
            RuntimePreviewInteractionContext context,
            GameObject target,
            Vector2 point,
            bool inside)
        {
            NativePointerState native = NativePointers.GetOrCreateValue(context);
            if (native.Data == null)
                native.Data = new PointerEventData(EventSystem.current);
            PointerEventData data;
            RaycastResult hit;
            List<RaycastResult> hits = Raycast(context, point, out data, out hit, native);
            bool hitTarget = inside && IsTargetOrChild(hit.gameObject, target);
            RuntimePreviewPointerEvidence evidence = Evidence(context, "pointer-" + (inside ? "enter" : "exit"), target, point, data, hits);
            evidence.method = "EventSystem.RaycastAll + direct GraphicRaycaster.Raycast verification + stock BaseInputModule.HandlePointerExitAndEnter";
            evidence.nativeDispatch = "BaseInputModule.HandlePointerExitAndEnter";
            evidence.pointerDataId = data.GetHashCode();
            evidence.nativeMoveSerial = native.Serial + 1;
            evidence.previousPointerEnter = PathOf(data.pointerEnter);
            evidence.fallbackPolicy = "A missed physical hit fails without direct event injection; one PointerEventData is retained for every native move.";
            if (inside && hitTarget && !evidence.directGraphicRaycastVerified)
                context.Fail("Winning pointer hit was not verified by a direct GraphicRaycaster.Raycast: " + PathOf(target));
            if (inside && !hitTarget)
            {
                context.Result.pointerEvents.Add(evidence);
                context.Fail("Screen-coordinate pointer hit missed its target; no event was injected: " + PathOf(target));
                return evidence;
            }
            if (NativeExitAndEnter == null)
                throw new MissingMethodException(typeof(BaseInputModule).FullName, "HandlePointerExitAndEnter");
            BaseInputModule module = EventSystem.current.GetComponent<BaseInputModule>();
            if (module == null)
                module = UnityEngine.Object.FindObjectOfType<BaseInputModule>();
            if (module == null)
                throw new InvalidOperationException("Native pointer validation requires the formal BaseInputModule.");

            NativeExitAndEnter.Invoke(module, new object[] { data, hit.gameObject });
            native.Serial++;
            context.PointerEnterHandler = data.pointerEnter;
            evidence.pointerEnter = PathOf(data.pointerEnter);
            evidence.nativeDispatchInvoked = true;
            context.Result.pointerEvents.Add(evidence);
            return evidence;
        }

        internal static RuntimePreviewPointerEvidence Click(
            RuntimePreviewInteractionContext context,
            GameObject target,
            bool requireClick = true)
        {
            if (target != null)
                Move(context, target, true);
            return ClickAt(context, target, Center(target), requireClick);
        }

        internal static RuntimePreviewPointerEvidence ClickAt(
            RuntimePreviewInteractionContext context,
            GameObject target,
            Vector2 point,
            bool requireClick = true)
        {
            PointerEventData data;
            RaycastResult hit;
            List<RaycastResult> hits = Raycast(context, point, out data, out hit);
            bool hitTarget = IsTargetOrChild(hit.gameObject, target);
            RuntimePreviewPointerEvidence evidence = Evidence(context, "pointer-click", target, point, data, hits);
            if (hitTarget && !evidence.directGraphicRaycastVerified)
                context.Fail("Winning click hit was not verified by a direct GraphicRaycaster.Raycast: " + PathOf(target));
            if (!hitTarget)
            {
                if (requireClick)
                {
                    evidence.fallbackUsed = true;
                    ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
                    context.Fail("Screen-coordinate click missed its target; direct pointerClick fallback is not a mouse pass: " + PathOf(target));
                }
                context.Result.pointerEvents.Add(evidence);
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
            ExecuteEvents.Execute(press, data, ExecuteEvents.pointerUpHandler);
            GameObject click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            evidence.pointerClick = PathOf(click);
            if (press != null && press == click)
            {
                ExecuteEvents.Execute(press, data, ExecuteEvents.pointerClickHandler);
                evidence.clickDispatched = true;
            }
            context.PointerPress = null;
            context.Result.pointerEvents.Add(evidence);
            if (requireClick && !evidence.clickDispatched)
                context.Fail("Raycast hit did not produce the same pointerDown/pointerClick handler: " + PathOf(target));
            return evidence;
        }

        internal static RuntimePreviewPointerEvidence Drag(
            RuntimePreviewInteractionContext context,
            GameObject target,
            Vector2 start,
            Vector2 end)
        {
            PointerEventData data;
            RaycastResult hit;
            List<RaycastResult> hits = Raycast(context, start, out data, out hit);
            bool hitTarget = IsTargetOrChild(hit.gameObject, target);
            RuntimePreviewPointerEvidence evidence = Evidence(context, "pointer-drag", target, start, data, hits);
            if (hitTarget && !evidence.directGraphicRaycastVerified)
                context.Fail("Winning drag hit was not verified by a direct GraphicRaycaster.Raycast: " + PathOf(target));
            if (!hitTarget)
            {
                evidence.fallbackUsed = true;
                context.Result.pointerEvents.Add(evidence);
                context.Fail("Screen-coordinate drag missed its target; no real-mouse pass is claimed: " + PathOf(target));
                return evidence;
            }

            data.button = PointerEventData.InputButton.Left;
            data.pressPosition = start;
            data.pointerPressRaycast = hit;
            GameObject press = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
            context.PointerPress = press;
            evidence.pointerPress = PathOf(press);
            data.dragging = true;
            ExecuteEvents.Execute(press, data, ExecuteEvents.beginDragHandler);
            data.position = end;
            data.delta = end - start;
            PointerEventData endData;
            RaycastResult endHit;
            Raycast(context, end, out endData, out endHit);
            data.pointerCurrentRaycast = endHit;
            ExecuteEvents.Execute(press, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(press, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(press, data, ExecuteEvents.endDragHandler);
            context.PointerPress = null;
            context.Result.pointerEvents.Add(evidence);
            return evidence;
        }

        internal static Vector2 Center(GameObject target)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            RectTransform rect = target.transform as RectTransform;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (rect == null || canvas == null)
                throw new InvalidOperationException("Pointer target has no RectTransform/Canvas: " + PathOf(target));
            canvas = canvas.rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(
                canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
        }

        private static List<RaycastResult> Raycast(
            RuntimePreviewInteractionContext context,
            Vector2 point,
            out PointerEventData data,
            out RaycastResult hit,
            NativePointerState native = null)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                throw new InvalidOperationException("Screen-coordinate pointer validation requires EventSystem.current.");
            eventSystem.enabled = true;
            if (native == null)
            {
                data = new PointerEventData(eventSystem) { position = point };
            }
            else
            {
                data = native.Data;
                data.delta = point - data.position;
                data.position = point;
            }
            List<RaycastResult> hits = new List<RaycastResult>();
            eventSystem.RaycastAll(data, hits);
            hit = hits.Count == 0 ? default(RaycastResult) : hits[0];
            data.pointerCurrentRaycast = hit;
            if (hit.gameObject != null)
            {
                GraphicRaycaster raycaster = hit.module as GraphicRaycaster;
                if (raycaster != null)
                {
                    List<RaycastResult> direct = new List<RaycastResult>();
                    raycaster.Raycast(data, direct);
                    for (int i = 0; i < direct.Count; i++)
                    {
                        if (direct[i].gameObject == hit.gameObject)
                        {
                            data.pointerCurrentRaycast = direct[i];
                            break;
                        }
                    }
                }
            }
            return hits;
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
                screenPositionTopLeft = new Vector2(point.x, context.Height - point.y),
                expectedTarget = PathOf(target),
                raycastHit = PathOf(data.pointerCurrentRaycast.gameObject),
                raycaster = data.pointerCurrentRaycast.module == null
                    ? string.Empty
                    : data.pointerCurrentRaycast.module.GetType().FullName
            };
            GraphicRaycaster raycaster = data.pointerCurrentRaycast.module as GraphicRaycaster;
            if (raycaster != null && data.pointerCurrentRaycast.gameObject != null)
            {
                List<RaycastResult> direct = new List<RaycastResult>();
                raycaster.Raycast(data, direct);
                for (int i = 0; i < direct.Count; i++)
                    evidence.directGraphicRaycastVerified |= direct[i].gameObject == data.pointerCurrentRaycast.gameObject;
            }
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
    }
}
