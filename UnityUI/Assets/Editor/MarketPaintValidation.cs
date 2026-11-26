using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class NativePaintRect
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class MarketTitlePaintProbe
    {
        public string font;
        public int fontSize;
        public string fontStyle;
        public float pixelsPerUnit;
        public float nativeFontSize;
        public NativePaintRect crop;
        public int neutralInkPixels;
        public int neutralInkWidth;
        public int neutralInkHeight;
        public float titleToRedLineGap;
    }

    [Serializable]
    public sealed class MarketChartPaintProbe
    {
        public int totalPointCount;
        public int visiblePointCount;
        public int visibleRange;
        public int segmentCount;
        public float lineWidth;
        public float pixelsPerUnit;
        public float nativeLineWidth;
        public float graphicAlpha;
        public float mainStrokeOpacity;
        public float underStrokeOpacity;
        public float effectiveMainStrokeAlpha;
        public float effectiveUnderStrokeAlpha;
        public float underStrokeAdditionalWidth;
        public string risingColor;
        public string fallingColor;
        public string dryBrushTexture;
        public string plotShader;
        public int detailContrastOver20;
        public NativePaintRect chartCrop;
        public NativePaintRect detailCrop;
    }

    [Serializable]
    public sealed class MarketEmptyTextPaintProbe
    {
        public string font;
        public int fontSize;
        public string fontStyle;
        public float pixelsPerUnit;
        public float nativeFontSize;
        public int lightInkPixels;
        public int lightInkWidth;
        public int lightInkHeight;
        public NativePaintRect crop;
    }

    [Serializable]
    public sealed class MarketCartCountPaintProbe
    {
        public string text;
        public string font;
        public int fontSize;
        public string fontStyle;
        public float nativeFontSize;
        public int visibleCharacterCount;
        public int expectedCharacterCount;
        public int lineCount;
        public float textVisibleAreaRatio;
        public string imageType;
        public string sprite;
        public float localRotationZ;
        public float localWidth;
        public float localHeight;
        public float pixelRotationZ;
        public float goldCenterDeltaX;
        public float goldCenterDeltaY;
        public int goldPaintPixels;
        public int goldMeanR;
        public int goldMeanG;
        public int goldMeanB;
        public NativePaintRect goldBounds;
        public bool goldTouchingProbeEdge;
        public int digitInkPixels;
        public NativePaintRect digitBounds;
        public bool digitTouchingGoldEdge;
        public NativePaintRect probeRect;
        public NativePaintRect crop;
        public NativePaintRect digitCrop;
    }

    [Serializable]
    public sealed class MarketCartPaintProbe
    {
        public int leftLightStripeColumns;
        public int rightLightStripeColumns;
        public float tabsBaselineFromRailTop;
        public float cartListFromRailTop;
        public float baselineToListGap;
        public float activeLineMinusBaselineY;
        public MarketEmptyTextPaintProbe emptyTitle;
        public MarketEmptyTextPaintProbe emptyBody;
        public MarketCartCountPaintProbe countBadge;
        public NativePaintRect railCrop;
        public NativePaintRect tabsCrop;
    }

    [Serializable]
    public sealed class MarketPaintProbeReport
    {
        public int schemaVersion = 2;
        public string coordinateSpace = "nativePixelsTopLeft";
        public string countBadgeMeasurement = "Thresholded framebuffer pixels; exclusive-right/bottom bounds. Rotation is estimated from rows spanning at least 90% of the maximum gold row, never from RectTransform size.";
        public string sourcePng;
        public int width;
        public int height;
        public float nativePixelsPerCssPixel;
        public MarketTitlePaintProbe firstTitle;
        public MarketChartPaintProbe firstChart;
        public MarketCartPaintProbe cart;
        public List<string> crops = new List<string>();
    }

    public static class MarketPaintValidation
    {
        public static void Capture(string pngPath, Texture2D image, Color32[] pixels)
        {
            if (image == null || pixels == null || !Path.GetFileName(pngPath).StartsWith("market", StringComparison.Ordinal))
                return;
            string semanticsPath = Path.ChangeExtension(pngPath, ".json");
            if (!File.Exists(semanticsPath))
                return;
            VisualSemanticSnapshot snapshot = JsonUtility.FromJson<VisualSemanticSnapshot>(
                File.ReadAllText(semanticsPath));
            if (snapshot == null || string.IsNullOrEmpty(snapshot.caseName) ||
                !snapshot.caseName.StartsWith("market", StringComparison.Ordinal))
                return;
            VisualImageSemantic nameBack = FindImage(snapshot, "/GoodsCard00#", "/NameBack#");
            if (nameBack == null)
                return;

            string directory = Path.Combine(
                Path.GetDirectoryName(pngPath),
                Path.GetFileNameWithoutExtension(pngPath) + "-paint");
            Directory.CreateDirectory(directory);
            MarketPaintProbeReport report = new MarketPaintProbeReport
            {
                sourcePng = pngPath,
                width = image.width,
                height = image.height,
                nativePixelsPerCssPixel = image.width >= 2304 && image.height >= 1296 ? 1.5f : 1f
            };
            CaptureTitle(snapshot, pixels, image, directory, report);
            CaptureChart(snapshot, pixels, image, directory, report);
            CaptureCart(snapshot, pixels, image, directory, report);
            VisualValidation.WriteJson(Path.Combine(directory, "paint-probes.json"), report);
        }

        private static void CaptureTitle(
            VisualSemanticSnapshot snapshot,
            Color32[] pixels,
            Texture2D image,
            string directory,
            MarketPaintProbeReport report)
        {
            VisualImageSemantic nameBack = FindImage(snapshot, "/GoodsCard00#", "/NameBack#");
            VisualImageSemantic redLine = FindImage(snapshot, "/GoodsCard00#", "/NameRedLine#");
            VisualTextSemantic text = FindText(snapshot, "/GoodsCard00#", "/NameText#");
            NativePaintRect titleRect = NativeRect(nameBack.rect, image);
            InkMeasurement ink = MeasureInk(pixels, image, titleRect, true);
            report.firstTitle = new MarketTitlePaintProbe
            {
                font = text == null ? string.Empty : text.font,
                fontSize = text == null ? 0 : text.fontSize,
                fontStyle = text == null ? string.Empty : text.fontStyle,
                pixelsPerUnit = text == null ? 0f : text.pixelsPerUnit,
                nativeFontSize = text == null ? 0f : text.nativeFontSize,
                neutralInkPixels = ink.Count,
                neutralInkWidth = ink.Width,
                neutralInkHeight = ink.Height,
                titleToRedLineGap = redLine == null || ink.Count == 0 ? -1f : NativeRect(redLine.rect, image).x - ink.Right
            };
            int margin = Mathf.RoundToInt(8f * report.nativePixelsPerCssPixel);
            report.firstTitle.crop = SaveCrop(
                pixels,
                image,
                Expand(titleRect, margin, image),
                Path.Combine(directory, "first-title.png"),
                report.crops);
        }

        private static void CaptureChart(
            VisualSemanticSnapshot snapshot,
            Color32[] pixels,
            Texture2D image,
            string directory,
            MarketPaintProbeReport report)
        {
            VisualChartSemantic chart = null;
            for (int i = 0; i < snapshot.charts.Count; i++)
            {
                if (snapshot.charts[i].mode == "Card" &&
                    snapshot.charts[i].path.IndexOf("/GoodsCard00#", StringComparison.Ordinal) >= 0)
                {
                    chart = snapshot.charts[i];
                    break;
                }
            }
            if (chart == null)
                return;
            NativePaintRect chartRect = NativeRect(chart.rect, image);
            NativePaintRect detailRect = image.width == 1920 && image.height == 1080
                ? new NativePaintRect { x = 68, y = 474, width = 134, height = 61 }
                : Clamp(new NativePaintRect
                {
                    x = chartRect.x + Mathf.RoundToInt(chartRect.width * 0.0442f),
                    y = chartRect.y + Mathf.RoundToInt(chartRect.height * 0.7807f),
                    width = Mathf.Max(1, Mathf.RoundToInt(chartRect.width * 0.2821f)),
                    height = Mathf.Max(1, Mathf.RoundToInt(chartRect.height * 0.1683f))
                }, image);
            report.firstChart = new MarketChartPaintProbe
            {
                totalPointCount = chart.totalPointCount,
                visiblePointCount = chart.visiblePointCount,
                visibleRange = chart.visibleRange,
                segmentCount = chart.segmentCount,
                lineWidth = chart.lineWidth,
                pixelsPerUnit = chart.pixelsPerUnit,
                nativeLineWidth = chart.nativeLineWidth,
                graphicAlpha = chart.graphicAlpha,
                mainStrokeOpacity = chart.mainStrokeOpacity,
                underStrokeOpacity = chart.underStrokeOpacity,
                effectiveMainStrokeAlpha = chart.effectiveMainStrokeAlpha,
                effectiveUnderStrokeAlpha = chart.effectiveUnderStrokeAlpha,
                underStrokeAdditionalWidth = chart.underStrokeAdditionalWidth,
                risingColor = chart.risingColor,
                fallingColor = chart.fallingColor,
                dryBrushTexture = chart.dryBrushTexture,
                plotShader = chart.plotShader,
                detailContrastOver20 = DetailContrast(pixels, image, detailRect)
            };
            report.firstChart.chartCrop = SaveCrop(
                pixels, image, chartRect, Path.Combine(directory, "first-chart.png"), report.crops);
            report.firstChart.detailCrop = SaveCrop(
                pixels, image, detailRect, Path.Combine(directory, "first-chart-lower-left.png"), report.crops);
        }

        private static void CaptureCart(
            VisualSemanticSnapshot snapshot,
            Color32[] pixels,
            Texture2D image,
            string directory,
            MarketPaintProbeReport report)
        {
            VisualImageSemantic rail = FindImage(snapshot, "/PageHost#", "/CartRail#");
            if (rail == null)
                return;
            NativePaintRect railRect = NativeRect(rail.rect, image);
            VisualImageSemantic baseline = FindImage(snapshot, "/CartTabs#", "/Baseline#");
            VisualImageSemantic activeLine = FindImage(snapshot, "/PendingTabButton#", "/ActiveLine#");
            VisualScrollSemantic scroll = null;
            for (int i = 0; i < snapshot.scrolls.Count; i++)
            {
                if (snapshot.scrolls[i].path.IndexOf("/CartListScroll#", StringComparison.Ordinal) >= 0)
                {
                    scroll = snapshot.scrolls[i];
                    break;
                }
            }
            report.cart = new MarketCartPaintProbe
            {
                leftLightStripeColumns = CountLightStripeColumns(pixels, image, railRect, true),
                rightLightStripeColumns = CountLightStripeColumns(pixels, image, railRect, false)
            };
            if (baseline != null)
            {
                NativePaintRect baselineRect = NativeRect(baseline.rect, image);
                float baselineY = baselineRect.y + baselineRect.height * 0.5f;
                report.cart.tabsBaselineFromRailTop = baselineY - railRect.y;
                if (activeLine != null)
                {
                    NativePaintRect activeRect = NativeRect(activeLine.rect, image);
                    report.cart.activeLineMinusBaselineY = activeRect.y + activeRect.height * 0.5f - baselineY;
                }
                if (scroll != null)
                {
                    NativePaintRect scrollRect = NativeRect(scroll.rect, image);
                    report.cart.cartListFromRailTop = scrollRect.y - railRect.y;
                    report.cart.baselineToListGap = scrollRect.y - baselineY;
                }
                int verticalMargin = Mathf.RoundToInt(30f * report.nativePixelsPerCssPixel);
                report.cart.tabsCrop = SaveCrop(
                    pixels,
                    image,
                    Expand(baselineRect, 0, verticalMargin, image),
                    Path.Combine(directory, "cart-tabs.png"),
                    report.crops);
            }
            report.cart.railCrop = SaveCrop(
                pixels, image, railRect, Path.Combine(directory, "cart-rail.png"), report.crops);
            report.cart.emptyTitle = CaptureEmptyText(
                snapshot, pixels, image, directory, report, "/TitleText#", "cart-empty-title.png");
            report.cart.emptyBody = CaptureEmptyText(
                snapshot, pixels, image, directory, report, "/BodyText#", "cart-empty-body.png");
            report.cart.countBadge = CaptureCountBadge(snapshot, pixels, image, directory, report);
        }

        private static MarketCartCountPaintProbe CaptureCountBadge(
            VisualSemanticSnapshot snapshot,
            Color32[] pixels,
            Texture2D image,
            string directory,
            MarketPaintProbeReport report)
        {
            VisualImageSemantic badge = FindImage(snapshot, "/CartRail#", "/CountBack#");
            VisualTextSemantic text = FindText(snapshot, "/CountBack#", "/CartCountText#");
            if (badge == null)
                return null;
            int margin = Mathf.RoundToInt(8f * report.nativePixelsPerCssPixel);
            NativePaintRect probeRect = Expand(NativeRect(badge.rect, image), margin, image);
            InkMeasurement gold = MeasureCountGold(pixels, image, probeRect);
            NativePaintRect goldBounds = NativeRect(gold);
            InkMeasurement digit = gold.Count == 0
                ? new InkMeasurement()
                : MeasureCountDigit(pixels, image, goldBounds);
            NativePaintRect digitBounds = NativeRect(digit);
            float scale = report.nativePixelsPerCssPixel;
            bool compact = image.width / scale <= 1450f || image.height / scale <= 790f;
            float expectedX = image.width - (compact ? 14f : 20f) * scale -
                (compact ? 28f : 32f) * scale;
            float expectedY = (compact ? 118f + 27f : 136f + 34f) * scale;
            float centerX = gold.Count == 0 ? expectedX : goldBounds.x + goldBounds.width * 0.5f;
            float centerY = gold.Count == 0 ? expectedY : goldBounds.y + goldBounds.height * 0.5f;
            MarketCartCountPaintProbe result = new MarketCartCountPaintProbe
            {
                text = text == null ? string.Empty : text.text,
                font = text == null ? string.Empty : text.font,
                fontSize = text == null ? 0 : text.fontSize,
                fontStyle = text == null ? string.Empty : text.fontStyle,
                nativeFontSize = text == null ? 0f : text.nativeFontSize,
                visibleCharacterCount = text == null ? 0 : text.characterCountVisible,
                expectedCharacterCount = text == null ? 0 : text.expectedCharacterCount,
                lineCount = text == null ? 0 : text.cachedLineCount,
                textVisibleAreaRatio = text == null ? 0f : text.visibleAreaRatio,
                imageType = badge.imageType,
                sprite = badge.sprite,
                localRotationZ = badge.localRotationZ,
                localWidth = badge.localWidth,
                localHeight = badge.localHeight,
                pixelRotationZ = gold.Count == 0 ? 0f : CountGoldRotation(pixels, image, goldBounds),
                goldCenterDeltaX = centerX - expectedX,
                goldCenterDeltaY = centerY - expectedY,
                goldPaintPixels = gold.Count,
                goldMeanR = gold.MeanR,
                goldMeanG = gold.MeanG,
                goldMeanB = gold.MeanB,
                goldBounds = goldBounds,
                goldTouchingProbeEdge = gold.Count > 0 && Touches(gold, probeRect),
                digitInkPixels = digit.Count,
                digitBounds = digitBounds,
                digitTouchingGoldEdge = digit.Count > 0 && gold.Count > 0 && Touches(digit, goldBounds),
                probeRect = probeRect
            };
            result.crop = SaveCrop(
                pixels, image, probeRect, Path.Combine(directory, "cart-count.png"), report.crops);
            if (digit.Count > 0)
            {
                int digitMargin = Mathf.Max(2, Mathf.RoundToInt(3f * report.nativePixelsPerCssPixel));
                result.digitCrop = SaveCrop(
                    pixels,
                    image,
                    Expand(digitBounds, digitMargin, image),
                    Path.Combine(directory, "cart-count-digit.png"),
                    report.crops);
            }
            return result;
        }

        private static MarketEmptyTextPaintProbe CaptureEmptyText(
            VisualSemanticSnapshot snapshot,
            Color32[] pixels,
            Texture2D image,
            string directory,
            MarketPaintProbeReport report,
            string leaf,
            string filename)
        {
            VisualTextSemantic text = FindText(snapshot, "/CartEmpty#", leaf);
            if (text == null)
                return null;
            NativePaintRect rect = NativeRect(text.rect, image);
            InkMeasurement ink = MeasureInk(pixels, image, rect, false);
            MarketEmptyTextPaintProbe result = new MarketEmptyTextPaintProbe
            {
                font = text.font,
                fontSize = text.fontSize,
                fontStyle = text.fontStyle,
                pixelsPerUnit = text.pixelsPerUnit,
                nativeFontSize = text.nativeFontSize,
                lightInkPixels = ink.Count,
                lightInkWidth = ink.Width,
                lightInkHeight = ink.Height
            };
            int margin = Mathf.RoundToInt(4f * report.nativePixelsPerCssPixel);
            result.crop = SaveCrop(
                pixels, image, Expand(rect, margin, image), Path.Combine(directory, filename), report.crops);
            return result;
        }

        private static int CountLightStripeColumns(
            Color32[] pixels,
            Texture2D image,
            NativePaintRect rail,
            bool left)
        {
            int sampleWidth = Mathf.Max(12, Mathf.RoundToInt(rail.width * 0.045f));
            int startX = left ? rail.x : rail.x + rail.width - sampleWidth;
            int endX = left ? rail.x + sampleWidth : rail.x + rail.width;
            int startY = rail.y + Mathf.RoundToInt(rail.height * 0.08f);
            int endY = rail.y + rail.height - Mathf.RoundToInt(rail.height * 0.08f);
            int columns = 0;
            for (int x = startX; x < endX; x++)
            {
                int light = 0;
                int total = 0;
                for (int y = startY; y < endY; y += 2)
                {
                    Color32 pixel = Pixel(pixels, image, x, y);
                    if (pixel.r > 130 && pixel.g > 120 && pixel.b > 100)
                        light++;
                    total++;
                }
                if (total > 0 && light * 2 >= total)
                    columns++;
            }
            return columns;
        }

        private static int DetailContrast(Color32[] pixels, Texture2D image, NativePaintRect rect)
        {
            int count = 0;
            int[] window = new int[81];
            for (int y = rect.y; y < rect.y + rect.height; y++)
            {
                for (int x = rect.x; x < rect.x + rect.width; x++)
                {
                    int center = Gray(Pixel(pixels, image, x, y));
                    int length = 0;
                    for (int offsetY = -4; offsetY <= 4; offsetY++)
                    {
                        int sampleY = Mathf.Clamp(y + offsetY, rect.y, rect.y + rect.height - 1);
                        for (int offsetX = -4; offsetX <= 4; offsetX++)
                        {
                            int sampleX = Mathf.Clamp(x + offsetX, rect.x, rect.x + rect.width - 1);
                            window[length++] = Gray(Pixel(pixels, image, sampleX, sampleY));
                        }
                    }
                    Array.Sort(window);
                    if (window[40] - center > 20)
                        count++;
                }
            }
            return count;
        }

        private static InkMeasurement MeasureCountGold(
            Color32[] pixels,
            Texture2D image,
            NativePaintRect rect)
        {
            InkMeasurement result = NewInkMeasurement();
            for (int y = rect.y; y < rect.y + rect.height; y++)
            {
                for (int x = rect.x; x < rect.x + rect.width; x++)
                {
                    Color32 pixel = Pixel(pixels, image, x, y);
                    if (!IsCountGold(pixel))
                        continue;
                    AddInk(result, x, y);
                    result.RedSum += pixel.r;
                    result.GreenSum += pixel.g;
                    result.BlueSum += pixel.b;
                }
            }
            return result;
        }

        private static InkMeasurement MeasureCountDigit(
            Color32[] pixels,
            Texture2D image,
            NativePaintRect gold)
        {
            InkMeasurement result = NewInkMeasurement();
            int radius = Math.Max(gold.width, gold.height);
            for (int y = gold.y; y < gold.y + gold.height; y++)
            {
                for (int x = gold.x; x < gold.x + gold.width; x++)
                {
                    Color32 pixel = Pixel(pixels, image, x, y);
                    if (Gray(pixel) > 130 ||
                        !HasCountGold(pixels, image, x, y, radius, -1, 0) ||
                        !HasCountGold(pixels, image, x, y, radius, 1, 0) ||
                        !HasCountGold(pixels, image, x, y, radius, 0, -1) ||
                        !HasCountGold(pixels, image, x, y, radius, 0, 1))
                        continue;
                    AddInk(result, x, y);
                }
            }
            return result;
        }

        private static float CountGoldRotation(
            Color32[] pixels,
            Texture2D image,
            NativePaintRect gold)
        {
            int[] leftEdges = new int[gold.height];
            int[] rightEdges = new int[gold.height];
            int maximumWidth = 0;
            for (int y = gold.y; y < gold.y + gold.height; y++)
            {
                int left = -1;
                int right = -1;
                for (int x = gold.x; x < gold.x + gold.width; x++)
                {
                    if (!IsCountGold(Pixel(pixels, image, x, y)))
                        continue;
                    if (left < 0)
                        left = x;
                    right = x;
                }
                leftEdges[y - gold.y] = left;
                rightEdges[y - gold.y] = right;
                maximumWidth = Math.Max(maximumWidth, right - left + 1);
            }
            int minimumWidth = Mathf.CeilToInt(maximumWidth * 0.9f);
            int count = 0;
            float sumY = 0f;
            float sumLeft = 0f;
            float sumRight = 0f;
            for (int y = gold.y; y < gold.y + gold.height; y++)
            {
                int left = leftEdges[y - gold.y];
                int right = rightEdges[y - gold.y];
                if (left < 0 || right - left + 1 < minimumWidth)
                    continue;
                count++;
                sumY += y;
                sumLeft += left;
                sumRight += right;
            }
            if (count < 2)
                return 0f;
            float meanY = sumY / count;
            float meanLeft = sumLeft / count;
            float meanRight = sumRight / count;
            float denominator = 0f;
            float leftNumerator = 0f;
            float rightNumerator = 0f;
            for (int y = gold.y; y < gold.y + gold.height; y++)
            {
                int left = leftEdges[y - gold.y];
                int right = rightEdges[y - gold.y];
                if (left < 0 || right - left + 1 < minimumWidth)
                    continue;
                float offsetY = y - meanY;
                denominator += offsetY * offsetY;
                leftNumerator += offsetY * (left - meanLeft);
                rightNumerator += offsetY * (right - meanRight);
            }
            if (denominator <= 0f)
                return 0f;
            float leftAngle = Mathf.Atan(leftNumerator / denominator) * Mathf.Rad2Deg;
            float rightAngle = Mathf.Atan(rightNumerator / denominator) * Mathf.Rad2Deg;
            return (leftAngle + rightAngle) * 0.5f;
        }

        private static bool HasCountGold(
            Color32[] pixels,
            Texture2D image,
            int x,
            int y,
            int radius,
            int directionX,
            int directionY)
        {
            for (int distance = 1; distance <= radius; distance++)
            {
                if (IsCountGold(Pixel(
                    pixels,
                    image,
                    x + directionX * distance,
                    y + directionY * distance)))
                    return true;
            }
            return false;
        }

        private static bool IsCountGold(Color32 pixel)
        {
            return pixel.r >= 140 && pixel.g >= 105 && pixel.b <= 180 &&
                pixel.r >= pixel.g && pixel.r - pixel.b >= 45 && pixel.g - pixel.b >= 30;
        }

        private static InkMeasurement NewInkMeasurement()
        {
            return new InkMeasurement
            {
                Left = int.MaxValue,
                Top = int.MaxValue,
                Right = int.MinValue,
                Bottom = int.MinValue
            };
        }

        private static void AddInk(InkMeasurement measurement, int x, int y)
        {
            measurement.Count++;
            measurement.Left = Math.Min(measurement.Left, x);
            measurement.Top = Math.Min(measurement.Top, y);
            measurement.Right = Math.Max(measurement.Right, x + 1);
            measurement.Bottom = Math.Max(measurement.Bottom, y + 1);
        }

        private static NativePaintRect NativeRect(InkMeasurement measurement)
        {
            if (measurement.Count == 0)
                return new NativePaintRect();
            measurement.Width = measurement.Right - measurement.Left;
            measurement.Height = measurement.Bottom - measurement.Top;
            return new NativePaintRect
            {
                x = measurement.Left,
                y = measurement.Top,
                width = measurement.Width,
                height = measurement.Height
            };
        }

        private static bool Touches(InkMeasurement measurement, NativePaintRect rect)
        {
            return measurement.Left <= rect.x || measurement.Top <= rect.y ||
                measurement.Right >= rect.x + rect.width ||
                measurement.Bottom >= rect.y + rect.height;
        }

        private static InkMeasurement MeasureInk(
            Color32[] pixels,
            Texture2D image,
            NativePaintRect rect,
            bool dark)
        {
            InkMeasurement result = new InkMeasurement
            {
                Left = int.MaxValue,
                Top = int.MaxValue,
                Right = int.MinValue,
                Bottom = int.MinValue
            };
            for (int y = rect.y; y < rect.y + rect.height; y++)
            {
                for (int x = rect.x; x < rect.x + rect.width; x++)
                {
                    Color32 pixel = Pixel(pixels, image, x, y);
                    int maximum = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                    int minimum = Math.Min(pixel.r, Math.Min(pixel.g, pixel.b));
                    bool match = dark
                        ? maximum < 150 && maximum - minimum < 45
                        : minimum > 90 && maximum - minimum < 75;
                    if (!match)
                        continue;
                    result.Count++;
                    result.Left = Math.Min(result.Left, x);
                    result.Top = Math.Min(result.Top, y);
                    result.Right = Math.Max(result.Right, x + 1);
                    result.Bottom = Math.Max(result.Bottom, y + 1);
                }
            }
            if (result.Count == 0)
            {
                result.Left = result.Top = result.Right = result.Bottom = 0;
                return result;
            }
            result.Width = result.Right - result.Left;
            result.Height = result.Bottom - result.Top;
            return result;
        }

        private static NativePaintRect SaveCrop(
            Color32[] pixels,
            Texture2D image,
            NativePaintRect rect,
            string path,
            List<string> crops)
        {
            rect = Clamp(rect, image);
            if (rect.width <= 0 || rect.height <= 0)
                return rect;
            Color32[] output = new Color32[rect.width * rect.height];
            for (int y = 0; y < rect.height; y++)
            {
                int sourceY = rect.y + rect.height - 1 - y;
                for (int x = 0; x < rect.width; x++)
                    output[y * rect.width + x] = Pixel(pixels, image, rect.x + x, sourceY);
            }
            Texture2D crop = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false, false);
            try
            {
                crop.SetPixels32(output);
                crop.Apply(false, false);
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(crop));
                crops.Add(Path.GetFileName(path));
            }
            finally
            {
                UnityEngine.Object.Destroy(crop);
            }
            return rect;
        }

        private static VisualImageSemantic FindImage(
            VisualSemanticSnapshot snapshot,
            string parent,
            string leaf)
        {
            for (int i = 0; i < snapshot.images.Count; i++)
            {
                VisualImageSemantic image = snapshot.images[i];
                if (image.path.IndexOf(parent, StringComparison.Ordinal) >= 0 &&
                    image.path.IndexOf(leaf, StringComparison.Ordinal) >= 0)
                    return image;
            }
            return null;
        }

        private static VisualTextSemantic FindText(
            VisualSemanticSnapshot snapshot,
            string parent,
            string leaf)
        {
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                VisualTextSemantic text = snapshot.texts[i];
                if (text.path.IndexOf(parent, StringComparison.Ordinal) >= 0 &&
                    text.path.IndexOf(leaf, StringComparison.Ordinal) >= 0)
                    return text;
            }
            return null;
        }

        private static NativePaintRect NativeRect(VisualRectSemantic rect, Texture2D image)
        {
            if (rect == null)
                return new NativePaintRect();
            return Clamp(new NativePaintRect
            {
                x = Mathf.FloorToInt(rect.x),
                y = Mathf.FloorToInt(image.height - rect.y - rect.height),
                width = Mathf.CeilToInt(rect.x + rect.width) - Mathf.FloorToInt(rect.x),
                height = Mathf.CeilToInt(image.height - rect.y) - Mathf.FloorToInt(image.height - rect.y - rect.height)
            }, image);
        }

        private static NativePaintRect Expand(NativePaintRect rect, int margin, Texture2D image)
        {
            return Expand(rect, margin, margin, image);
        }

        private static NativePaintRect Expand(
            NativePaintRect rect,
            int horizontal,
            int vertical,
            Texture2D image)
        {
            return Clamp(new NativePaintRect
            {
                x = rect.x - horizontal,
                y = rect.y - vertical,
                width = rect.width + horizontal * 2,
                height = rect.height + vertical * 2
            }, image);
        }

        private static NativePaintRect Clamp(NativePaintRect rect, Texture2D image)
        {
            int right = Mathf.Clamp(rect.x + rect.width, 0, image.width);
            int bottom = Mathf.Clamp(rect.y + rect.height, 0, image.height);
            rect.x = Mathf.Clamp(rect.x, 0, image.width);
            rect.y = Mathf.Clamp(rect.y, 0, image.height);
            rect.width = Math.Max(0, right - rect.x);
            rect.height = Math.Max(0, bottom - rect.y);
            return rect;
        }

        private static Color32 Pixel(Color32[] pixels, Texture2D image, int x, int y)
        {
            x = Mathf.Clamp(x, 0, image.width - 1);
            y = Mathf.Clamp(y, 0, image.height - 1);
            return pixels[(image.height - 1 - y) * image.width + x];
        }

        private static int Gray(Color32 pixel)
        {
            return (pixel.r * 77 + pixel.g * 150 + pixel.b * 29) >> 8;
        }

        private sealed class InkMeasurement
        {
            internal int Count;
            internal int Width;
            internal int Height;
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
            internal long RedSum;
            internal long GreenSum;
            internal long BlueSum;
            internal int MeanR { get { return Count == 0 ? 0 : (int)((RedSum + Count / 2) / Count); } }
            internal int MeanG { get { return Count == 0 ? 0 : (int)((GreenSum + Count / 2) / Count); } }
            internal int MeanB { get { return Count == 0 ? 0 : (int)((BlueSum + Count / 2) / Count); } }
        }
    }
}
