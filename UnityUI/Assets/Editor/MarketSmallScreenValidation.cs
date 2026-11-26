using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class MarketSmallScreenLayoutEvidence
    {
        public bool compactHeight;
        public float contentScale;
        public float tolerance;
        public bool footerNoteActive;
        public List<MarketSmallScreenRectEvidence> rects = new List<MarketSmallScreenRectEvidence>();
        public List<MarketSmallScreenCheckEvidence> checks = new List<MarketSmallScreenCheckEvidence>();
        public List<MarketSmallScreenTextEvidence> texts = new List<MarketSmallScreenTextEvidence>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class MarketSmallScreenRectEvidence
    {
        public string key;
        public string path;
        public string source;
        public VisualRectSemantic screenTopLeft;
    }

    [Serializable]
    public sealed class MarketSmallScreenCheckEvidence
    {
        public string key;
        public string metric;
        public string status;
        public float expected;
        public float actual;
        public float tolerance;
    }

    [Serializable]
    public sealed class MarketSmallScreenTextEvidence
    {
        public string path;
        public bool active;
        public int fontSize;
        public float rectHeight;
        public float preferredHeight;
        public float preferredWidth;
        public int cachedLineCount;
        public int characterCountVisible;
    }

    public static class MarketSmallScreenValidation
    {
        public static MarketSmallScreenLayoutEvidence Capture(VisualSemanticSnapshot snapshot)
        {
            MarketSmallScreenLayoutEvidence evidence = new MarketSmallScreenLayoutEvidence();
            float scale = snapshot.requestedWidth >= 2304 && snapshot.requestedHeight >= 1296 ? 1.5f : 1f;
            float viewportWidth = snapshot.requestedWidth / scale;
            evidence.contentScale = scale;
            evidence.compactHeight = snapshot.requestedHeight / scale <= 790f;
            evidence.tolerance = 0.75f * scale;
            bool compact = evidence.compactHeight;

            Node footer = Require(evidence, snapshot, "cart-footer", "images", string.Empty, "/CartFooter#");
            Node checkout = Require(evidence, snapshot, "checkout", "buttons", "/CartFooter#", "/CheckoutButton#");
            Node rail = Require(evidence, snapshot, "cart-rail", "images", string.Empty, "/CartRail#");
            Node listContent = Require(evidence, snapshot, "cart-list-content", "layouts", "/CartListScroll#", "/Content#");
            Node cartTitle = Require(evidence, snapshot, "cart-title", "texts", "/CartRail#", "/Title#");
            Node cartSubtitle = Require(evidence, snapshot, "cart-subtitle", "texts", "/CartRail#", "/Subtitle#");
            Node pendingTab = Require(evidence, snapshot, "pending-tab", "buttons", "/CartTabs#", "/PendingTabButton#");
            Node quote = Require(evidence, snapshot, "quote", "images", "GoodsCard00", "/QuoteBack#");
            Node changeBack = Require(evidence, snapshot, "change-back", "images", "GoodsCard00", "/ChangeBack#");
            Node price = Require(evidence, snapshot, "price", "texts", "GoodsCard00", "/PriceText#");
            Node unit = Require(evidence, snapshot, "price-unit", "texts", "GoodsCard00", "/PriceUnitText#");
            Node delta = Require(evidence, snapshot, "delta", "texts", "GoodsCard00", "/DeltaText#");
            Node change = Require(evidence, snapshot, "change", "texts", "GoodsCard00", "/ChangeText#");

            float footerHeight = (compact ? 153f : 193f) * scale;
            TopInset(evidence, listContent, rail, (compact ? 85f : 110f) * scale);
            TopInset(evidence, listContent, pendingTab, (compact ? 36f : 45f) * scale);
            TopInset(evidence, pendingTab, rail, (compact ? 49f : 65f) * scale);
            Height(evidence, pendingTab, (compact ? 29f : 35f) * scale);
            TopInset(evidence, cartTitle, rail, (compact ? 8f : 10f) * scale);
            Height(evidence, cartTitle, (compact ? 22f : 32f) * scale);
            TopInset(evidence, cartSubtitle, rail, (compact ? 32f : 41f) * scale);
            Height(evidence, cartSubtitle, (compact ? 10f : 18f) * scale);
            Height(evidence, footer, footerHeight);
            Height(evidence, checkout, (compact ? 36f : 43f) * scale);
            LeftInset(evidence, checkout, footer, (compact ? 12f : 15f) * scale);
            RightInset(evidence, checkout, footer, (compact ? 12f : 15f) * scale);
            BottomInset(evidence, checkout, footer, (compact ? 12f : 33f) * scale);

            if (price != null)
            {
                float changeHeight = (compact ? CompactChangeHeight(viewportWidth) : 26f) * scale;
                Height(evidence, quote, price.rect.height + (compact ? 32f * scale + changeHeight : 67f * scale));
                Height(evidence, change, changeHeight);
                Height(evidence, changeBack, changeHeight);
                TopInset(evidence, price, quote, (compact ? 6f : 9f) * scale);
                TopInset(evidence, unit, quote, (compact ? 6f : 9f) * scale);
                TopInset(evidence, delta, quote, price.rect.height + (compact ? 9f : 14f) * scale);
                TopInset(evidence, change, quote, price.rect.height + (compact ? 26f : 32f) * scale);
                TopInset(evidence, changeBack, quote, price.rect.height + (compact ? 26f : 32f) * scale);
                Height(evidence, delta, (compact ? 14f : 15f) * scale);
            }
            if (quote != null)
            {
                LeftInset(evidence, price, quote, (compact ? 8f : 12f) * scale);
                LeftInset(evidence, delta, quote, (compact ? 8f : 12f) * scale);
                LeftInset(evidence, change, quote, (compact ? 8f : 12f) * scale);
                RightInset(evidence, unit, quote, 7f * scale);
                Width(evidence, price, quote.rect.width - (compact ? 34f : 38f) * scale);
                Width(evidence, delta, quote.rect.width - (compact ? 15f : 19f) * scale);
                Width(evidence, change, quote.rect.width - (compact ? 15f : 19f) * scale);
            }

            CaptureFooterLine(evidence, snapshot, footer, "buy-caption", "/BuyCaptionText#",
                compact ? 9f : 8f, compact ? 20f : 26f, compact ? 12f : 15f, scale);
            CaptureFooterLine(evidence, snapshot, footer, "buy-total", "/BuyTotalText#",
                compact ? 9f : 8f, compact ? 20f : 26f, compact ? 12f : 15f, scale);
            CaptureFooterLine(evidence, snapshot, footer, "sell-caption", "/SellCaptionText#",
                compact ? 34f : 35f, compact ? 20f : 26f, compact ? 12f : 15f, scale);
            CaptureFooterLine(evidence, snapshot, footer, "sell-total", "/SellTotalText#",
                compact ? 34f : 35f, compact ? 20f : 26f, compact ? 12f : 15f, scale);
            CaptureFooterLine(evidence, snapshot, footer, "cash-caption", "/CashCaptionText#",
                compact ? 67f : 70f, compact ? 30f : 36f, compact ? 12f : 15f, scale);
            CaptureFooterLine(evidence, snapshot, footer, "cash-delta", "/CashDeltaText#",
                compact ? 67f : 66f, compact ? 30f : 42f, compact ? 12f : 15f, scale);
            Node divider = Require(evidence, snapshot, "total-divider", "images", "/CartFooter#", "/TotalDivider#");
            TopInset(evidence, divider, footer, (compact ? 60f : 65f) * scale);
            LeftInset(evidence, divider, footer, (compact ? 12f : 15f) * scale);
            RightInset(evidence, divider, footer, (compact ? 12f : 15f) * scale);

            evidence.footerNoteActive = Contains(snapshot.texts, string.Empty, "/FooterNoteText#");
            Expect(evidence, "footer-note", "active", compact ? 0f : 1f,
                evidence.footerNoteActive ? 1f : 0f);
            if (snapshot.caseName == "market-empty")
                CaptureEmptyAlignment(evidence, snapshot, rail, footerHeight, scale);
            evidence.texts = CaptureTexts();
            return evidence;
        }

        private static void CaptureFooterLine(
            MarketSmallScreenLayoutEvidence evidence,
            VisualSemanticSnapshot snapshot,
            Node footer,
            string key,
            string leaf,
            float top,
            float height,
            float horizontal,
            float scale)
        {
            Node node = Require(evidence, snapshot, key, "texts", "/CartFooter#", leaf);
            TopInset(evidence, node, footer, top * scale);
            Height(evidence, node, height * scale);
            if (node != null && footer != null)
            {
                float actual = node.path.IndexOf("TotalText#", StringComparison.Ordinal) >= 0 ||
                    node.path.IndexOf("CashDeltaText#", StringComparison.Ordinal) >= 0
                    ? Right(node, footer)
                    : Left(node, footer);
                Expect(evidence, key, "horizontal-inset", horizontal * scale, actual);
            }
        }

        private static void CaptureEmptyAlignment(
            MarketSmallScreenLayoutEvidence evidence,
            VisualSemanticSnapshot snapshot,
            Node rail,
            float footerHeight,
            float scale)
        {
            Node title = Require(evidence, snapshot, "empty-title", "texts", "/CartEmpty#", "/TitleText#");
            Node icon = Require(evidence, snapshot, "empty-icon", "images", "/CartEmpty#", "/EmptyIcon#");
            if (rail == null)
                return;
            float listTop = (evidence.compactHeight ? 85f : 110f) * scale;
            float viewportHeight = rail.rect.height - listTop - footerHeight;
            float emptyHeight = Math.Max(180f * scale, viewportHeight - 11f * scale);
            float rootCenter = rail.rect.y + listTop + scale + emptyHeight * 0.5f;
            CenterY(evidence, title, rootCenter + 8.5f * scale);
            CenterY(evidence, icon, rootCenter - 46f * scale);
        }

        private static float CompactChangeHeight(float viewportWidth)
        {
            float fontSize = Math.Max(11f, Math.Min(15f, viewportWidth * 0.009f));
            return (float)Math.Ceiling(fontSize * 1.3f) + 4f;
        }

        private static Node Require(
            MarketSmallScreenLayoutEvidence evidence,
            VisualSemanticSnapshot snapshot,
            string key,
            string source,
            string ancestor,
            string leaf)
        {
            string path;
            VisualRectSemantic rect = Find(snapshot, source, ancestor, leaf, out path);
            if (rect == null)
            {
                evidence.failures.Add("Required market layout rect is missing: " + key + " (" + leaf + ").");
                return null;
            }
            Node node = new Node { key = key, path = path, rect = TopLeft(rect, snapshot.requestedHeight) };
            evidence.rects.Add(new MarketSmallScreenRectEvidence
            {
                key = key,
                path = path,
                source = source,
                screenTopLeft = node.rect
            });
            return node;
        }

        private static VisualRectSemantic Find(
            VisualSemanticSnapshot snapshot,
            string source,
            string ancestor,
            string leaf,
            out string path)
        {
            if (source == "texts")
            {
                for (int i = 0; i < snapshot.texts.Count; i++)
                {
                    VisualTextSemantic node = snapshot.texts[i];
                    if (Matches(node.path, ancestor, leaf))
                    {
                        path = node.path;
                        return node.rect;
                    }
                }
            }
            else if (source == "buttons")
            {
                for (int i = 0; i < snapshot.buttons.Count; i++)
                {
                    VisualButtonSemantic node = snapshot.buttons[i];
                    if (Matches(node.path, ancestor, leaf))
                    {
                        path = node.path;
                        return node.rect;
                    }
                }
            }
            else if (source == "layouts")
            {
                for (int i = 0; i < snapshot.layouts.Count; i++)
                {
                    VisualLayoutSemantic node = snapshot.layouts[i];
                    if (Matches(node.path, ancestor, leaf))
                    {
                        path = node.path;
                        return node.rect;
                    }
                }
            }
            else
            {
                for (int i = 0; i < snapshot.images.Count; i++)
                {
                    VisualImageSemantic node = snapshot.images[i];
                    if (Matches(node.path, ancestor, leaf))
                    {
                        path = node.path;
                        return node.rect;
                    }
                }
            }
            path = string.Empty;
            return null;
        }

        private static bool Matches(string path, string ancestor, string leaf)
        {
            return (string.IsNullOrEmpty(ancestor) || path.IndexOf(ancestor, StringComparison.Ordinal) >= 0) &&
                path.IndexOf(leaf, StringComparison.Ordinal) >= 0;
        }

        private static bool Contains(List<VisualTextSemantic> texts, string ancestor, string leaf)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                if (Matches(texts[i].path, ancestor, leaf))
                    return true;
            }
            return false;
        }

        private static void Height(MarketSmallScreenLayoutEvidence evidence, Node node, float expected)
        {
            if (node != null)
                Expect(evidence, node.key, "height", expected, node.rect.height);
        }

        private static void Width(MarketSmallScreenLayoutEvidence evidence, Node node, float expected)
        {
            if (node != null)
                Expect(evidence, node.key, "width", expected, node.rect.width);
        }

        private static void TopInset(MarketSmallScreenLayoutEvidence evidence, Node node, Node parent, float expected)
        {
            if (node != null && parent != null)
                Expect(evidence, node.key, "top-inset", expected, node.rect.y - parent.rect.y);
        }

        private static void LeftInset(MarketSmallScreenLayoutEvidence evidence, Node node, Node parent, float expected)
        {
            if (node != null && parent != null)
                Expect(evidence, node.key, "left-inset", expected, Left(node, parent));
        }

        private static void RightInset(MarketSmallScreenLayoutEvidence evidence, Node node, Node parent, float expected)
        {
            if (node != null && parent != null)
                Expect(evidence, node.key, "right-inset", expected, Right(node, parent));
        }

        private static void BottomInset(MarketSmallScreenLayoutEvidence evidence, Node node, Node parent, float expected)
        {
            if (node != null && parent != null)
            {
                float actual = parent.rect.y + parent.rect.height - node.rect.y - node.rect.height;
                Expect(evidence, node.key, "bottom-inset", expected, actual);
            }
        }

        private static void CenterY(MarketSmallScreenLayoutEvidence evidence, Node node, float expected)
        {
            if (node != null)
                Expect(evidence, node.key, "center-y", expected, node.rect.y + node.rect.height * 0.5f);
        }

        private static float Left(Node node, Node parent)
        {
            return node.rect.x - parent.rect.x;
        }

        private static float Right(Node node, Node parent)
        {
            return parent.rect.x + parent.rect.width - node.rect.x - node.rect.width;
        }

        private static void Expect(
            MarketSmallScreenLayoutEvidence evidence,
            string key,
            string metric,
            float expected,
            float actual)
        {
            bool passed = Math.Abs(expected - actual) <= evidence.tolerance;
            evidence.checks.Add(new MarketSmallScreenCheckEvidence
            {
                key = key,
                metric = metric,
                status = passed ? "PASS" : "FAIL",
                expected = expected,
                actual = actual,
                tolerance = evidence.tolerance
            });
            if (!passed)
            {
                evidence.failures.Add(key + " " + metric + ": expected " +
                    expected.ToString("0.###", CultureInfo.InvariantCulture) + ", actual " +
                    actual.ToString("0.###", CultureInfo.InvariantCulture) + ", tolerance " +
                    evidence.tolerance.ToString("0.###", CultureInfo.InvariantCulture) + ".");
            }
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

        private static List<MarketSmallScreenTextEvidence> CaptureTexts()
        {
            List<MarketSmallScreenTextEvidence> result = new List<MarketSmallScreenTextEvidence>();
            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (!canvas.isRootCanvas)
                    continue;
                Text[] texts = canvas.GetComponentsInChildren<Text>(true);
                for (int j = 0; j < texts.Length; j++)
                {
                    Text text = texts[j];
                    string path = PathOf(text.transform);
                    if (path.IndexOf("XingShangMod.MarketWindow", StringComparison.Ordinal) < 0 ||
                        (path.IndexOf("GoodsCard00", StringComparison.Ordinal) < 0 &&
                        path.IndexOf("/CartFooter#", StringComparison.Ordinal) < 0))
                        continue;
                    float scale = text.rectTransform.lossyScale.y;
                    TextGenerator generator = text.cachedTextGenerator;
                    result.Add(new MarketSmallScreenTextEvidence
                    {
                        path = path,
                        active = text.gameObject.activeInHierarchy,
                        fontSize = text.fontSize,
                        rectHeight = text.rectTransform.rect.height * scale,
                        preferredHeight = text.preferredHeight * scale,
                        preferredWidth = text.preferredWidth * scale,
                        cachedLineCount = generator == null ? 0 : generator.lineCount,
                        characterCountVisible = generator == null ? 0 : generator.characterCountVisible
                    });
                }
            }
            result.Sort(delegate(MarketSmallScreenTextEvidence left, MarketSmallScreenTextEvidence right)
            {
                return string.CompareOrdinal(left.path, right.path);
            });
            return result;
        }

        private static string PathOf(Transform transform)
        {
            if (transform == null)
                return string.Empty;
            string result = transform.name + "#" + transform.GetSiblingIndex().ToString(CultureInfo.InvariantCulture);
            Transform current = transform.parent;
            while (current != null)
            {
                result = current.name + "#" + current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + "/" + result;
                current = current.parent;
            }
            return result;
        }

        private sealed class Node
        {
            internal string key;
            internal string path;
            internal VisualRectSemantic rect;
        }
    }
}
