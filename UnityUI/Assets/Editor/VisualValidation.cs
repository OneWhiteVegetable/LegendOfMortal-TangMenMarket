using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class VisualSemanticSnapshot
    {
        public int schemaVersion = 6;
        public string rectSpace = "screenPixelsBottomLeft";
        public string fixtureId;
        public string caseName;
        public int requestedWidth;
        public int requestedHeight;
        public int screenWidth;
        public int screenHeight;
        public string runStateSha256;
        public string focusedObject;
        public List<VisualTextSemantic> texts = new List<VisualTextSemantic>();
        public List<VisualButtonSemantic> buttons = new List<VisualButtonSemantic>();
        public List<VisualImageSemantic> images = new List<VisualImageSemantic>();
        public List<VisualChartSemantic> charts = new List<VisualChartSemantic>();
        public List<VisualLayoutSemantic> layouts = new List<VisualLayoutSemantic>();
        public List<VisualScrollSemantic> scrolls = new List<VisualScrollSemantic>();
        public List<string> overlays = new List<string>();
        public List<string> missingSprites = new List<string>();
        public List<string> missingGlyphs = new List<string>();
        public List<VisualCaptureDiagnostic> captureDiagnostics = new List<VisualCaptureDiagnostic>();
        public List<RuntimePreviewComponentRectEvidence> keyComponents = new List<RuntimePreviewComponentRectEvidence>();
        public List<string> intentionalOverscan = new List<string>();
    }

    [Serializable]
    public sealed class VisualTextSemantic
    {
        public string path;
        public string text;
        public string font;
        public int fontSize;
        public string fontStyle;
        public float pixelsPerUnit;
        public float nativeFontSize;
        public float alpha;
        public VisualRectSemantic rect;
        public bool clipped;
        public int cachedVertexCount;
        public int cachedLineCount;
        public int characterCount;
        public int characterCountVisible;
        public int expectedCharacterCount;
        public VisualRectSemantic visibleRect;
        public float visibleAreaRatio;
        public string geometryDiagnosis;
        public string clippingDiagnosis;
        public string occlusionDiagnosis;
        public List<VisualOccluderSemantic> occluders = new List<VisualOccluderSemantic>();
        public List<VisualOccluderSemantic> intentionalOverlays = new List<VisualOccluderSemantic>();
    }

    [Serializable]
    public sealed class VisualOccluderSemantic
    {
        public string path;
        public float alpha;
        public float overlapRatio;
        public int drawOrderDelta;
        public string shader;
        public float strength;
        public bool raycastTarget;
        public bool intentionalTextureOverlay;
    }

    [Serializable]
    public sealed class VisualButtonSemantic
    {
        public string path;
        public string label;
        public bool interactable;
        public bool selected;
        public VisualRectSemantic rect;
        public bool clipped;
    }

    [Serializable]
    public sealed class VisualImageSemantic
    {
        public string path;
        public string sprite;
        public string texture;
        public int spriteWidth;
        public int spriteHeight;
        public float alpha;
        public bool raycastTarget;
        public string imageType;
        public float localRotationZ;
        public float localWidth;
        public float localHeight;
        public VisualRectSemantic rect;
        public bool clipped;
    }

    [Serializable]
    public sealed class VisualChartSemantic
    {
        public string path;
        public string componentType;
        public string mode;
        public int totalPointCount;
        public int visiblePointCount;
        public int visibleRange;
        public float visibleStart;
        public float yZoomFactor;
        public int selectedIndex;
        public int segmentCount;
        public double visibleMinimum;
        public double visibleMaximum;
        public VisualRectSemantic rect;
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
        public List<VisualChartSeriesSemantic> series = new List<VisualChartSeriesSemantic>();
    }

    [Serializable]
    public sealed class VisualChartSeriesSemantic
    {
        public string id;
        public string label;
        public int totalPointCount;
        public int visiblePointCount;
    }

    [Serializable]
    public sealed class VisualLayoutSemantic
    {
        public string path;
        public string layoutType;
        public VisualRectSemantic rect;
        public bool clipped;
        public float pixelsPerUnit;
        public float expectedChildWidth;
        public int columnCount;
        public List<VisualLayoutChildSemantic> children = new List<VisualLayoutChildSemantic>();
    }

    [Serializable]
    public sealed class VisualLayoutChildSemantic
    {
        public string path;
        public VisualRectSemantic rect;
    }

    [Serializable]
    public sealed class VisualScrollSemantic
    {
        public string path;
        public bool horizontal;
        public bool vertical;
        public float horizontalNormalizedPosition;
        public float verticalNormalizedPosition;
        public VisualRectSemantic rect;
    }

    [Serializable]
    public sealed class VisualRectSemantic
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class VisualCaptureDiagnostic
    {
        public string canvasPath;
        public string renderMode;
        public string cameraPath;
        public string targetTexture;
        public VisualRectSemantic cameraPixelRect;
        public VisualRectSemantic canvasPixelRect;
        public VisualRectSemantic canvasLocalRect;
        public VisualRectSemantic rootTargetRect;
        public int targetWidth;
        public int targetHeight;
        public float canvasScaleFactor;
        public string canvasScalerMode;
        public float cameraAspect;
        public string conversion;
    }

    [Serializable]
    public sealed class VisualImageValidation
    {
        public int width;
        public int height;
        public long fileBytes;
        public int nonTransparentPixels;
        public int sampledColors;
        public bool pngSignatureValid;
        public bool dimensionsValid;
        public bool hasVisiblePixels;
        public bool hasNonUniformContent;
    }

    public static class VisualValidation
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static VisualSemanticSnapshot CaptureSemantics(
            string fixtureId,
            string caseName,
            int width,
            int height,
            string runStateSha256)
        {
            VisualSemanticSnapshot result = new VisualSemanticSnapshot
            {
                fixtureId = fixtureId,
                caseName = caseName,
                requestedWidth = width,
                requestedHeight = height,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                runStateSha256 = runStateSha256,
                focusedObject = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                    ? PathOf(EventSystem.current.currentSelectedGameObject.transform)
                    : string.Empty
            };

            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            Array.Sort(canvases, delegate (Canvas left, Canvas right)
            {
                int order = left.sortingOrder.CompareTo(right.sortingOrder);
                return order != 0 ? order : string.CompareOrdinal(PathOf(left.transform), PathOf(right.transform));
            });
            HashSet<string> overlays = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.gameObject.scene.IsValid() || !canvas.isActiveAndEnabled ||
                    canvas.rootCanvas != canvas)
                    continue;
                CaptureDiagnostic(canvas, result);
                Transform[] transforms = canvas.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    Transform item = transforms[j];
                    if (!item.gameObject.activeInHierarchy)
                        continue;
                    if (item.name == "XingShangMod.Modal" || item.name == "XingShangMod.Toast" ||
                        item.name == "XingShangMod.Tooltip")
                        overlays.Add(item.name.Substring("XingShangMod.".Length));
                }
                CaptureTexts(canvas, result);
                CaptureButtons(canvas, result);
                CaptureImages(canvas, result);
                CaptureScrolls(canvas, result);
                CaptureLayouts(canvas, result);
                CaptureCharts(canvas, result);
            }
            result.overlays.AddRange(overlays);
            result.overlays.Sort(StringComparer.Ordinal);
            result.keyComponents = MarketAlignmentValidation.CaptureComponents(result);
            return result;
        }

        public static string StabilityFingerprint(VisualSemanticSnapshot snapshot)
        {
            VisualSemanticSnapshot clone = JsonUtility.FromJson<VisualSemanticSnapshot>(
                JsonUtility.ToJson(snapshot, false));
            for (int i = 0; i < clone.texts.Count; i++)
            {
                clone.texts[i].rect = Quantize(clone.texts[i].rect);
                clone.texts[i].visibleRect = Quantize(clone.texts[i].visibleRect);
                clone.texts[i].pixelsPerUnit = QuantizeRatio(clone.texts[i].pixelsPerUnit);
                clone.texts[i].nativeFontSize = QuantizeRatio(clone.texts[i].nativeFontSize);
                clone.texts[i].alpha = QuantizeRatio(clone.texts[i].alpha);
                clone.texts[i].visibleAreaRatio = QuantizeRatio(clone.texts[i].visibleAreaRatio);
                QuantizeOccluders(clone.texts[i].occluders);
                QuantizeOccluders(clone.texts[i].intentionalOverlays);
            }
            for (int i = 0; i < clone.buttons.Count; i++)
                clone.buttons[i].rect = Quantize(clone.buttons[i].rect);
            for (int i = 0; i < clone.images.Count; i++)
            {
                clone.images[i].rect = Quantize(clone.images[i].rect);
                clone.images[i].alpha = QuantizeRatio(clone.images[i].alpha);
                clone.images[i].localRotationZ = QuantizeRatio(clone.images[i].localRotationZ);
                clone.images[i].localWidth = QuantizeRatio(clone.images[i].localWidth);
                clone.images[i].localHeight = QuantizeRatio(clone.images[i].localHeight);
            }
            for (int i = 0; i < clone.layouts.Count; i++)
            {
                clone.layouts[i].rect = Quantize(clone.layouts[i].rect);
                clone.layouts[i].expectedChildWidth = QuantizeRatio(clone.layouts[i].expectedChildWidth);
                clone.layouts[i].pixelsPerUnit = QuantizeRatio(clone.layouts[i].pixelsPerUnit);
                for (int j = 0; j < clone.layouts[i].children.Count; j++)
                    clone.layouts[i].children[j].rect = Quantize(clone.layouts[i].children[j].rect);
            }
            for (int i = 0; i < clone.scrolls.Count; i++)
            {
                clone.scrolls[i].horizontalNormalizedPosition = clone.scrolls[i].horizontal
                    ? QuantizeRatio(clone.scrolls[i].horizontalNormalizedPosition)
                    : 0f;
                clone.scrolls[i].verticalNormalizedPosition = clone.scrolls[i].vertical
                    ? QuantizeRatio(clone.scrolls[i].verticalNormalizedPosition)
                    : 0f;
                clone.scrolls[i].rect = Quantize(clone.scrolls[i].rect);
            }
            for (int i = 0; i < clone.charts.Count; i++)
            {
                clone.charts[i].visibleStart = QuantizeRatio(clone.charts[i].visibleStart);
                clone.charts[i].yZoomFactor = QuantizeRatio(clone.charts[i].yZoomFactor);
                clone.charts[i].rect = Quantize(clone.charts[i].rect);
                clone.charts[i].lineWidth = QuantizeRatio(clone.charts[i].lineWidth);
                clone.charts[i].pixelsPerUnit = QuantizeRatio(clone.charts[i].pixelsPerUnit);
                clone.charts[i].nativeLineWidth = QuantizeRatio(clone.charts[i].nativeLineWidth);
                clone.charts[i].graphicAlpha = QuantizeRatio(clone.charts[i].graphicAlpha);
                clone.charts[i].mainStrokeOpacity = QuantizeRatio(clone.charts[i].mainStrokeOpacity);
                clone.charts[i].underStrokeOpacity = QuantizeRatio(clone.charts[i].underStrokeOpacity);
                clone.charts[i].effectiveMainStrokeAlpha = QuantizeRatio(clone.charts[i].effectiveMainStrokeAlpha);
                clone.charts[i].effectiveUnderStrokeAlpha = QuantizeRatio(clone.charts[i].effectiveUnderStrokeAlpha);
                clone.charts[i].underStrokeAdditionalWidth = QuantizeRatio(clone.charts[i].underStrokeAdditionalWidth);
            }
            for (int i = 0; i < clone.keyComponents.Count; i++)
            {
                clone.keyComponents[i].screenBottomLeft = Quantize(clone.keyComponents[i].screenBottomLeft);
                clone.keyComponents[i].screenTopLeft = Quantize(clone.keyComponents[i].screenTopLeft);
            }
            for (int i = 0; i < clone.captureDiagnostics.Count; i++)
            {
                VisualCaptureDiagnostic diagnostic = clone.captureDiagnostics[i];
                diagnostic.cameraPixelRect = Quantize(diagnostic.cameraPixelRect);
                diagnostic.canvasPixelRect = Quantize(diagnostic.canvasPixelRect);
                diagnostic.canvasLocalRect = Quantize(diagnostic.canvasLocalRect);
                diagnostic.rootTargetRect = Quantize(diagnostic.rootTargetRect);
                diagnostic.canvasScaleFactor = QuantizeRatio(diagnostic.canvasScaleFactor);
                diagnostic.cameraAspect = QuantizeRatio(diagnostic.cameraAspect);
            }
            return JsonUtility.ToJson(clone, false);
        }

        private static VisualRectSemantic Quantize(VisualRectSemantic rect)
        {
            if (rect == null)
                return null;
            const float step = 0.01f;
            return new VisualRectSemantic
            {
                x = Mathf.Round(rect.x / step) * step,
                y = Mathf.Round(rect.y / step) * step,
                width = Mathf.Round(rect.width / step) * step,
                height = Mathf.Round(rect.height / step) * step
            };
        }

        private static float QuantizeRatio(float value)
        {
            const float step = 0.001f;
            return Mathf.Round(value / step) * step;
        }

        private static void QuantizeOccluders(List<VisualOccluderSemantic> occluders)
        {
            for (int i = 0; i < occluders.Count; i++)
            {
                occluders[i].alpha = QuantizeRatio(occluders[i].alpha);
                occluders[i].overlapRatio = QuantizeRatio(occluders[i].overlapRatio);
                occluders[i].strength = QuantizeRatio(occluders[i].strength);
            }
        }

        public static List<string> ValidateSemantics(VisualSemanticSnapshot snapshot)
        {
            List<string> failures = new List<string>();
            if (snapshot.screenWidth != snapshot.requestedWidth || snapshot.screenHeight != snapshot.requestedHeight)
            {
                failures.Add("Native Game View size mismatch: requested " + snapshot.requestedWidth + "x" +
                    snapshot.requestedHeight + ", actual " + snapshot.screenWidth + "x" + snapshot.screenHeight + ".");
            }
            for (int i = 0; i < snapshot.missingSprites.Count; i++)
                failures.Add("Missing required Sprite: " + snapshot.missingSprites[i]);
            for (int i = 0; i < snapshot.missingGlyphs.Count; i++)
                failures.Add("Missing font or glyph: " + snapshot.missingGlyphs[i]);
            ValidateCriticalTextVisibility(failures, snapshot);

            bool market = snapshot.caseName.IndexOf("market", StringComparison.Ordinal) == 0;
            bool detail = snapshot.caseName.IndexOf("detail", StringComparison.Ordinal) == 0;
            bool assets = snapshot.caseName == "assets";
            if (market)
            {
                RequireChart(failures, snapshot, "Spark", 14, 0f, 1f, 14);
                for (int i = 0; i < 6; i++)
                    RequireChart(failures, snapshot, "Card", 26, 0f, 1f, 26, i + 1);
            }
            if (detail)
            {
                RequireChart(failures, snapshot, "Spark", 14, 0f, 1f, 14);
                RequireChart(failures, snapshot, "Detail", 26, 52, 1f, 78);
            }
            if (assets)
            {
                RequireChart(failures, snapshot, "Assets", 26, 52, 1f, 78, 1, "/AssetChartHost");
                RequireAssetSeries(failures, snapshot);
            }
            if (snapshot.caseName == "inventory")
                RequireChart(failures, snapshot, "Spark", 14, 0f, 1f, 14);
            if (snapshot.caseName == "readonly-held-quote")
                RequireChart(failures, snapshot, "Detail", 26, 52, 1f, 78);

            RequireOverlay(failures, snapshot, "market-modal", "Modal");
            RequireOverlay(failures, snapshot, "market-toast", "Toast");
            RequireOverlay(failures, snapshot, "detail-tooltip", "Tooltip");
            if (snapshot.caseName == "inventory-readonly")
            {
                for (int i = 0; i < snapshot.buttons.Count; i++)
                {
                    VisualButtonSemantic button = snapshot.buttons[i];
                    if (button.interactable && (button.path.IndexOf("ClosePositionButton", StringComparison.Ordinal) >= 0 ||
                        button.path.IndexOf("CloseAllLongButton", StringComparison.Ordinal) >= 0 ||
                        button.path.IndexOf("CloseAllShortButton", StringComparison.Ordinal) >= 0 ||
                        button.path.IndexOf("IndexButton", StringComparison.Ordinal) >= 0 ||
                        button.path.IndexOf("RankingButton", StringComparison.Ordinal) >= 0 ||
                        button.path.IndexOf("InventoryButton", StringComparison.Ordinal) >= 0))
                        failures.Add("Readonly inventory exposes an interactive navigation/closing control: " + button.path);
                }
                RequireReadonlyIndexHidden(failures, snapshot);
            }
            ValidateContentBounds(failures, snapshot);
            ValidateLayoutWidths(failures, snapshot);
            if (market)
                ValidateMarketLayout(failures, snapshot);
            if (snapshot.caseName.IndexOf("inventory", StringComparison.Ordinal) == 0)
                ValidateInventoryLayout(failures, snapshot);
            return failures;
        }

        private static void ValidateCriticalTextVisibility(
            List<string> failures,
            VisualSemanticSnapshot snapshot)
        {
            bool market = snapshot.caseName.IndexOf("market", StringComparison.Ordinal) == 0;
            bool detail = snapshot.caseName.IndexOf("detail", StringComparison.Ordinal) == 0;
            if (market)
            {
                RequireVisibleText(failures, snapshot, "GoodsRow", "/PriceText#", 6);
                RequireVisibleText(failures, snapshot, "GoodsRow", "/PriceUnitText#", 6);
                RequireVisibleText(failures, snapshot, "/CountBack#", "/CartCountText#", 1);
            }
            if (detail)
            {
                RequireVisibleText(failures, snapshot, "/DetailPage#", "/NameText#", 1);
                RequireVisibleText(failures, snapshot, "/DetailPage#", "/PriceText#", 1);
            }
            if (HasText(snapshot, "/RankingRoot#", "/RankingTitleText#"))
            {
                RequireVisibleText(failures, snapshot, "/RankingHeader#", "/RankingTitleText#", 1);
                RequireVisibleText(failures, snapshot, "/RankingTitleTag#", "/Label#", 1);
            }
            bool splitBrand = HasText(snapshot, "/BrandBlock#", "/BrandTangmen#") &&
                HasText(snapshot, "/BrandBlock#", "/BrandMarket#");
            if (splitBrand)
            {
                RequireVisibleText(failures, snapshot, "/BrandBlock#", "/BrandTangmen#", 1);
                RequireVisibleText(failures, snapshot, "/BrandBlock#", "/BrandMarket#", 1);
            }
            else
            {
                RequireVisibleText(failures, snapshot, "/BrandBlock#", "/BrandTitle#", 1);
            }
            RequireVisibleText(failures, snapshot, "/BrandBlock#", "/BrandSubtitle#", 1);
        }

        private static bool HasText(VisualSemanticSnapshot snapshot, string pathFragment, string leaf)
        {
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                if (snapshot.texts[i].path.IndexOf(pathFragment, StringComparison.Ordinal) >= 0 &&
                    snapshot.texts[i].path.IndexOf(leaf, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static void RequireVisibleText(
            List<string> failures,
            VisualSemanticSnapshot snapshot,
            string pathFragment,
            string leaf,
            int expectedCount)
        {
            int found = 0;
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                VisualTextSemantic text = snapshot.texts[i];
                if (text.path.IndexOf(pathFragment, StringComparison.Ordinal) < 0 ||
                    text.path.IndexOf(leaf, StringComparison.Ordinal) < 0)
                    continue;
                found++;
                if (text.cachedVertexCount <= 0 || text.cachedLineCount < 1)
                    failures.Add("Text geometry is empty: " + text.path + " vertex=" + text.cachedVertexCount +
                        " lines=" + text.cachedLineCount + " diagnosis=" + text.geometryDiagnosis);
                if (text.cachedLineCount != 1)
                    failures.Add("Text wraps unexpectedly: " + text.path + " lines=" + text.cachedLineCount);
                if (text.characterCountVisible < text.expectedCharacterCount)
                    failures.Add("Text generator truncates content: " + text.path + " visibleChars=" +
                        text.characterCountVisible + " expected=" + text.expectedCharacterCount +
                        " diagnosis=" + text.geometryDiagnosis);
                if (text.visibleAreaRatio < 0.98f)
                    failures.Add("Text is clipped by mask/viewport: " + text.path + " visibleRatio=" +
                        text.visibleAreaRatio.ToString("0.###", CultureInfo.InvariantCulture) +
                        " diagnosis=" + text.clippingDiagnosis + " fullRect=" + FormatRect(text.rect) +
                        " visibleRect=" + FormatRect(text.visibleRect));
                for (int j = 0; j < text.occluders.Count; j++)
                {
                    VisualOccluderSemantic occluder = text.occluders[j];
                    if (!occluder.intentionalTextureOverlay &&
                        occluder.alpha >= 0.5f && occluder.overlapRatio >= 0.5f)
                    {
                        failures.Add("Text is occluded by a later Graphic: " + text.path + " occluder=" +
                            occluder.path + " alpha=" + occluder.alpha.ToString("0.###", CultureInfo.InvariantCulture) +
                            " overlap=" + occluder.overlapRatio.ToString("0.###", CultureInfo.InvariantCulture));
                    }
                }
            }
            if (found != expectedCount)
                failures.Add("Critical visible text count mismatch: " + pathFragment + leaf +
                    " expected=" + expectedCount + " actual=" + found + ".");
        }

        public static VisualImageValidation ValidatePng(string path, int expectedWidth, int expectedHeight)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Runtime Preview PNG was not written.", path);
            byte[] bytes = File.ReadAllBytes(path);
            VisualImageValidation result = new VisualImageValidation { fileBytes = bytes.LongLength };
            if (bytes.Length < 24)
                throw new InvalidDataException("PNG is truncated: " + path);
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            result.pngSignatureValid = true;
            for (int i = 0; i < signature.Length; i++)
                result.pngSignatureValid &= bytes[i] == signature[i];
            if (!result.pngSignatureValid)
                throw new InvalidDataException("Screenshot is not a PNG: " + path);
            result.width = ReadBigEndianInt32(bytes, 16);
            result.height = ReadBigEndianInt32(bytes, 20);
            result.dimensionsValid = result.width == expectedWidth && result.height == expectedHeight;

            Texture2D image = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!ImageConversion.LoadImage(image, bytes, false))
                    throw new InvalidDataException("Unity could not decode screenshot: " + path);
                if (image.width != expectedWidth || image.height != expectedHeight)
                    result.dimensionsValid = false;
                Color32[] pixels = image.GetPixels32();
                HashSet<int> colors = new HashSet<int>();
                int stride = Math.Max(1, pixels.Length / 4096);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.a != 0)
                        result.nonTransparentPixels++;
                    if (i % stride == 0)
                        colors.Add((pixel.r << 24) | (pixel.g << 16) | (pixel.b << 8) | pixel.a);
                }
                result.sampledColors = colors.Count;
                result.hasVisiblePixels = result.nonTransparentPixels > 0;
                result.hasNonUniformContent = result.sampledColors > 1;
                MarketPaintValidation.Capture(path, image, pixels);
            }
            finally
            {
                UnityEngine.Object.Destroy(image);
            }
            return result;
        }

        public static void WriteJson(string path, object value)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        }

        public static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return Hex(sha.ComputeHash(stream));
        }

        public static string Sha256Text(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static void CaptureDiagnostic(Canvas canvas, VisualSemanticSnapshot result)
        {
            Camera camera = canvas.worldCamera;
            RenderTexture target = camera == null ? null : camera.targetTexture;
            RectTransform root = canvas.transform as RectTransform;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            VisualCaptureDiagnostic diagnostic = new VisualCaptureDiagnostic
            {
                canvasPath = PathOf(canvas.transform),
                renderMode = canvas.renderMode.ToString(),
                cameraPath = camera == null ? string.Empty : PathOf(camera.transform),
                targetTexture = target == null ? string.Empty : target.name,
                cameraPixelRect = camera == null ? new VisualRectSemantic() : FromRect(camera.pixelRect),
                canvasPixelRect = FromRect(canvas.pixelRect),
                canvasLocalRect = root == null ? new VisualRectSemantic() : FromRect(root.rect),
                rootTargetRect = ScreenRect(root, canvas),
                targetWidth = target == null ? Screen.width : target.width,
                targetHeight = target == null ? Screen.height : target.height,
                canvasScaleFactor = canvas.scaleFactor,
                canvasScalerMode = scaler == null ? string.Empty : scaler.uiScaleMode.ToString(),
                cameraAspect = camera == null ? 0f : camera.aspect,
                conversion = "rootCanvas.InverseTransformPoint, normalize by root rect xMin/yMin/width/height, then targetTexture scale"
            };
            result.captureDiagnostics.Add(diagnostic);
        }

        private static VisualRectSemantic FromRect(Rect rect)
        {
            return new VisualRectSemantic { x = rect.x, y = rect.y, width = rect.width, height = rect.height };
        }

        private static float NativeFontSize(Text text, float pixelsPerUnit)
        {
            float result = text.fontSize * pixelsPerUnit;
            BaseMeshEffect[] effects = text.GetComponents<BaseMeshEffect>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].GetType().FullName != "XingShangMod.UI.Market.MarketTextInkEffect")
                    continue;
                object kind = ReadField(effects[i], "_kind");
                if (kind == null || kind.ToString() != "CartCount")
                    continue;
                float source = Convert.ToSingle(ReadField(effects[i], "_sourceFontSize"), CultureInfo.InvariantCulture);
                float raster = Convert.ToSingle(ReadField(effects[i], "_rasterFontSize"), CultureInfo.InvariantCulture);
                if (source > 0f && raster > 0f)
                    result *= source / raster;
            }
            return result;
        }

        private static void CaptureTexts(Canvas canvas, VisualSemanticSnapshot result)
        {
            Graphic[] allGraphics = canvas.GetComponentsInChildren<Graphic>(true);
            Text[] texts = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (!IsVisible(text))
                    continue;
                string path = PathOf(text.transform);
                TextGenerator generator = text.cachedTextGenerator;
                string visibleText = StripRichText(text.text ?? string.Empty, text.supportRichText);
                VisualRectSemantic visibleRect = VisibleRect(text.rectTransform, canvas);
                VisualRectSemantic screenRect = ScreenRect(text.rectTransform, canvas);
                float textPixelsPerUnit = text.rectTransform.rect.width > 0.0001f
                    ? screenRect.width / text.rectTransform.rect.width
                    : 1f;
                VisualTextSemantic semantic = new VisualTextSemantic
                {
                    path = path,
                    text = text.text ?? string.Empty,
                    font = text.font == null ? string.Empty : text.font.name,
                    fontSize = text.fontSize,
                    fontStyle = text.fontStyle.ToString(),
                    pixelsPerUnit = textPixelsPerUnit,
                    nativeFontSize = NativeFontSize(text, textPixelsPerUnit),
                    alpha = EffectiveAlpha(text),
                    rect = screenRect,
                    clipped = IsClipped(text.transform),
                    cachedVertexCount = generator == null ? 0 : generator.vertexCount,
                    cachedLineCount = generator == null ? 0 : generator.lineCount,
                    characterCount = generator == null ? 0 : generator.characterCount,
                    characterCountVisible = generator == null ? 0 : generator.characterCountVisible,
                    expectedCharacterCount = visibleText.Length,
                    visibleRect = visibleRect,
                    visibleAreaRatio = AreaRatio(visibleRect, screenRect),
                    geometryDiagnosis = generator == null
                        ? "generator-null"
                        : generator.vertexCount == 0
                            ? "geometry-empty"
                            : generator.characterCountVisible < visibleText.Length
                                ? "generator-truncated"
                                : "generated",
                    clippingDiagnosis = AreaRatio(visibleRect, ScreenRect(text.rectTransform, canvas)) < 0.999f
                        ? "viewport-or-mask-clipped"
                        : "unclipped"
                };
                List<VisualOccluderSemantic> intentionalOverlays;
                semantic.occluders = CaptureOccluders(
                    text,
                    i,
                    allGraphics,
                    visibleRect,
                    canvas,
                    out intentionalOverlays);
                semantic.intentionalOverlays = intentionalOverlays;
                semantic.occlusionDiagnosis = semantic.occluders.Count == 0
                    ? (intentionalOverlays.Count > 0 ? "intentional texture overlay" : "no-overlap-above")
                    : "overlap-above";
                result.texts.Add(semantic);
                if (string.IsNullOrEmpty(text.text))
                    continue;
                if (text.font == null)
                {
                    result.missingGlyphs.Add(path + " (no font)");
                    continue;
                }
                for (int j = 0; j < visibleText.Length; j++)
                {
                    char character = visibleText[j];
                    if (char.IsWhiteSpace(character) || char.IsControl(character))
                        continue;
                    if (!text.font.HasCharacter(character))
                    {
                        result.missingGlyphs.Add(path + " U+" + ((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        break;
                    }
                }
            }
        }

        private static VisualRectSemantic VisibleRect(RectTransform target, Canvas canvas)
        {
            VisualRectSemantic result = ScreenRect(target, canvas);
            Camera camera = canvas.worldCamera;
            int width = camera != null && camera.targetTexture != null ? camera.targetTexture.width : Screen.width;
            int height = camera != null && camera.targetTexture != null ? camera.targetTexture.height : Screen.height;
            result = Intersect(result, new VisualRectSemantic { x = 0f, y = 0f, width = width, height = height });
            Transform current = target == null ? null : target.parent;
            while (current != null)
            {
                RectMask2D rectMask = current.GetComponent<RectMask2D>();
                if (rectMask != null && rectMask.enabled)
                {
                    VisualRectSemantic maskRect = ScreenRect(rectMask.rectTransform, canvas);
                    float scaleX = rectMask.rectTransform.rect.width <= 0.0001f
                        ? 1f
                        : maskRect.width / rectMask.rectTransform.rect.width;
                    float scaleY = rectMask.rectTransform.rect.height <= 0.0001f
                        ? 1f
                        : maskRect.height / rectMask.rectTransform.rect.height;
                    maskRect.x += rectMask.padding.x * scaleX;
                    maskRect.y += rectMask.padding.y * scaleY;
                    maskRect.width -= (rectMask.padding.x + rectMask.padding.z) * scaleX;
                    maskRect.height -= (rectMask.padding.y + rectMask.padding.w) * scaleY;
                    result = Intersect(result, maskRect);
                }
                Mask mask = current.GetComponent<Mask>();
                if (mask != null && mask.enabled)
                    result = Intersect(result, ScreenRect(mask.rectTransform, canvas));
                ScrollRect scroll = current.GetComponent<ScrollRect>();
                if (scroll != null && scroll.enabled && scroll.viewport != null)
                    result = Intersect(result, ScreenRect(scroll.viewport, canvas));
                current = current.parent;
            }
            return result;
        }

        private static float AreaRatio(VisualRectSemantic visible, VisualRectSemantic full)
        {
            float fullArea = Area(full);
            return fullArea <= 0f ? 0f : Area(visible) / fullArea;
        }

        private static float Area(VisualRectSemantic rect)
        {
            return rect == null ? 0f : Math.Max(0f, rect.width) * Math.Max(0f, rect.height);
        }

        private static VisualRectSemantic Intersect(VisualRectSemantic left, VisualRectSemantic right)
        {
            if (left == null || right == null)
                return new VisualRectSemantic();
            float x = Math.Max(left.x, right.x);
            float y = Math.Max(left.y, right.y);
            float rightEdge = Math.Min(left.x + left.width, right.x + right.width);
            float topEdge = Math.Min(left.y + left.height, right.y + right.height);
            return new VisualRectSemantic
            {
                x = x,
                y = y,
                width = Math.Max(0f, rightEdge - x),
                height = Math.Max(0f, topEdge - y)
            };
        }

        private static List<VisualOccluderSemantic> CaptureOccluders(
            Text text,
            int textIndex,
            Graphic[] allGraphics,
            VisualRectSemantic visibleRect,
            Canvas canvas,
            out List<VisualOccluderSemantic> intentionalOverlays)
        {
            List<VisualOccluderSemantic> result = new List<VisualOccluderSemantic>();
            intentionalOverlays = new List<VisualOccluderSemantic>();
            textIndex = Array.IndexOf(allGraphics, text);
            for (int i = 0; i < allGraphics.Length; i++)
            {
                Graphic other = allGraphics[i];
                if (other == null || other == text || other is Text || other.transform.IsChildOf(text.transform) ||
                    !IsVisible(other))
                    continue;
                int otherIndex = Array.IndexOf(allGraphics, other);
                if (otherIndex <= textIndex)
                    continue;
                VisualRectSemantic otherRect = VisibleRect(other.rectTransform, canvas);
                float overlap = AreaRatio(Intersect(visibleRect, otherRect), visibleRect);
                if (overlap <= 0.05f)
                    continue;
                bool intentional = IsIntentionalPaperMultiply(other);
                Material material = other.material;
                VisualOccluderSemantic occluder = new VisualOccluderSemantic
                {
                    path = PathOf(other.transform),
                    alpha = EffectiveAlpha(other),
                    overlapRatio = overlap,
                    drawOrderDelta = otherIndex - textIndex,
                    shader = material == null || material.shader == null ? string.Empty : material.shader.name,
                    strength = material != null && material.HasProperty("_Strength")
                        ? material.GetFloat("_Strength")
                        : 0f,
                    raycastTarget = other.raycastTarget,
                    intentionalTextureOverlay = intentional
                };
                if (intentional)
                    intentionalOverlays.Add(occluder);
                else
                    result.Add(occluder);
            }
            result.Sort(delegate (VisualOccluderSemantic left, VisualOccluderSemantic right)
            {
                return right.overlapRatio.CompareTo(left.overlapRatio);
            });
            if (result.Count > 3)
                result.RemoveRange(3, result.Count - 3);
            if (intentionalOverlays.Count > 3)
                intentionalOverlays.RemoveRange(3, intentionalOverlays.Count - 3);
            return result;
        }

        private static bool IsIntentionalPaperMultiply(Graphic graphic)
        {
            if (graphic.raycastTarget)
                return false;
            Material material = graphic.material;
            if (material == null || material.shader == null ||
                material.shader.name != "XingShang/UI/PaperMultiply" ||
                !material.HasProperty("_Strength"))
                return false;
            return Mathf.Abs(material.GetFloat("_Strength") - 0.075f) <= 0.0001f;
        }

        private static void CaptureButtons(Canvas canvas, VisualSemanticSnapshot result)
        {
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (!button.gameObject.activeInHierarchy || !button.enabled)
                    continue;
                Text label = button.GetComponentInChildren<Text>(true);
                result.buttons.Add(new VisualButtonSemantic
                {
                    path = PathOf(button.transform),
                    label = label == null ? string.Empty : label.text,
                    interactable = button.IsInteractable(),
                    selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject,
                    rect = ScreenRect(button.transform as RectTransform, canvas),
                    clipped = IsClipped(button.transform)
                });
            }
        }

        private static void CaptureImages(Canvas canvas, VisualSemanticSnapshot result)
        {
            Image[] images = canvas.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (!IsVisible(image))
                    continue;
                string path = PathOf(image.transform);
                Sprite sprite = image.sprite;
                result.images.Add(new VisualImageSemantic
                {
                    path = path,
                    sprite = sprite == null ? string.Empty : sprite.name,
                    texture = sprite == null || sprite.texture == null ? string.Empty : sprite.texture.name,
                    spriteWidth = sprite == null ? 0 : Mathf.RoundToInt(sprite.rect.width),
                    spriteHeight = sprite == null ? 0 : Mathf.RoundToInt(sprite.rect.height),
                    alpha = EffectiveAlpha(image),
                    raycastTarget = image.raycastTarget,
                    imageType = image.type.ToString(),
                    localRotationZ = Mathf.DeltaAngle(0f, image.rectTransform.localEulerAngles.z),
                    localWidth = image.rectTransform.rect.width,
                    localHeight = image.rectTransform.rect.height,
                    rect = ScreenRect(image.rectTransform, canvas),
                    clipped = IsClipped(image.transform)
                });
                if (sprite == null && (image.name == "ItemImage" || image.name == "TrendIcon" ||
                    image.name == "Icon" || image.name.EndsWith("ItemIcon", StringComparison.Ordinal) ||
                    image.name.EndsWith("IconImage", StringComparison.Ordinal)))
                    result.missingSprites.Add(path);
            }
        }

        private static void CaptureScrolls(Canvas canvas, VisualSemanticSnapshot result)
        {
            ScrollRect[] scrolls = canvas.GetComponentsInChildren<ScrollRect>(true);
            for (int i = 0; i < scrolls.Length; i++)
            {
                ScrollRect scroll = scrolls[i];
                if (!scroll.gameObject.activeInHierarchy)
                    continue;
                result.scrolls.Add(new VisualScrollSemantic
                {
                    path = PathOf(scroll.transform),
                    horizontal = scroll.horizontal,
                    vertical = scroll.vertical,
                    horizontalNormalizedPosition = scroll.horizontalNormalizedPosition,
                    verticalNormalizedPosition = scroll.verticalNormalizedPosition,
                    rect = ScreenRect(scroll.transform as RectTransform, canvas)
                });
            }
        }

        private static void CaptureLayouts(Canvas canvas, VisualSemanticSnapshot result)
        {
            LayoutGroup[] groups = canvas.GetComponentsInChildren<LayoutGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                LayoutGroup group = groups[i];
                if (!group.gameObject.activeInHierarchy)
                    continue;
                RectTransform root = group.GetComponent<RectTransform>();
                VisualLayoutSemantic semantic = new VisualLayoutSemantic
                {
                    path = PathOf(group.transform),
                    layoutType = group.GetType().FullName,
                    rect = ScreenRect(root, canvas),
                    clipped = IsClipped(group.transform)
                };
                for (int childIndex = 0; childIndex < group.transform.childCount; childIndex++)
                {
                    Transform child = group.transform.GetChild(childIndex);
                    if (!child.gameObject.activeInHierarchy)
                        continue;
                    semantic.children.Add(new VisualLayoutChildSemantic
                    {
                        path = PathOf(child),
                        rect = ScreenRect(child as RectTransform, canvas)
                    });
                }
                float unitWidth = root == null ? 0f : root.rect.width;
                semantic.pixelsPerUnit = unitWidth > 0.0001f ? semantic.rect.width / unitWidth : 1f;
                HorizontalLayoutGroup horizontal = group as HorizontalLayoutGroup;
                GridLayoutGroup grid = group as GridLayoutGroup;
                if (horizontal != null && semantic.children.Count > 0)
                {
                    float chrome = group.padding.horizontal + horizontal.spacing * (semantic.children.Count - 1);
                    semantic.expectedChildWidth = (semantic.rect.width - chrome * semantic.pixelsPerUnit) / semantic.children.Count;
                }
                if (grid != null)
                {
                    semantic.columnCount = grid.constraintCount;
                    ScrollRect scroll = group.GetComponentInParent<ScrollRect>();
                    RectTransform viewport = scroll == null ? null : scroll.viewport;
                    if (viewport != null && viewport.rect.width > 0.0001f && semantic.columnCount > 0)
                    {
                        VisualRectSemantic viewportRect = ScreenRect(viewport, canvas);
                        float viewportPixelsPerUnit = viewportRect.width / viewport.rect.width;
                        float chrome = group.padding.horizontal + grid.spacing.x * (semantic.columnCount - 1);
                        semantic.expectedChildWidth =
                            (viewportRect.width - chrome * viewportPixelsPerUnit) / semantic.columnCount;
                    }
                    else
                    {
                        semantic.expectedChildWidth = grid.cellSize.x * semantic.pixelsPerUnit;
                    }
                }
                result.layouts.Add(semantic);
            }
            result.layouts.Sort(delegate (VisualLayoutSemantic left, VisualLayoutSemantic right)
            {
                return string.CompareOrdinal(left.path, right.path);
            });
        }

        private static void CaptureCharts(Canvas canvas, VisualSemanticSnapshot result)
        {
            Component[] components = canvas.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component chart = components[i];
                if (chart == null || chart.GetType().FullName != "XingShangMod.UI.Charting.WeeklyChartGraphic" ||
                    !chart.gameObject.activeInHierarchy)
                    continue;
                object mode = Read(chart, "Mode");
                object segments = Read(chart, "Segments");
                RectTransform chartRect = chart.transform as RectTransform;
                VisualRectSemantic screenRect = ScreenRect(chartRect, canvas);
                float lineWidth = Convert.ToSingle(Read(chart, "LineWidth"), CultureInfo.InvariantCulture);
                float pixelsPerUnit = chartRect != null && chartRect.rect.width > 0.0001f
                    ? screenRect.width / chartRect.rect.width
                    : 1f;
                float graphicAlpha = EffectiveAlpha((Graphic)chart);
                float mainStrokeOpacity = Convert.ToSingle(Read(chart, "MainStrokeOpacity"), CultureInfo.InvariantCulture);
                float underStrokeOpacity = Convert.ToSingle(Read(chart, "UnderStrokeOpacity"), CultureInfo.InvariantCulture);
                Color32 rising = (Color32)Read(chart, "RisingColor");
                Color32 falling = (Color32)Read(chart, "FallingColor");
                Transform plotTransform = chart.transform.Find("PlotClip/DryBrushPlot");
                Graphic plot = plotTransform == null ? null : plotTransform.GetComponent<Graphic>();
                Texture plotTexture = plot == null ? null : plot.mainTexture;
                Material plotMaterial = plot == null ? null : plot.material;
                result.charts.Add(new VisualChartSemantic
                {
                    path = PathOf(chart.transform),
                    componentType = chart.GetType().FullName,
                    mode = mode == null ? string.Empty : mode.ToString(),
                    totalPointCount = Convert.ToInt32(Read(chart, "TotalPointCount"), CultureInfo.InvariantCulture),
                    visiblePointCount = Convert.ToInt32(Read(chart, "VisiblePointCount"), CultureInfo.InvariantCulture),
                    visibleRange = Convert.ToInt32(Read(chart, "VisibleRange"), CultureInfo.InvariantCulture),
                    visibleStart = Convert.ToSingle(Read(chart, "VisibleStart"), CultureInfo.InvariantCulture),
                    yZoomFactor = Convert.ToSingle(Read(chart, "YZoomFactor"), CultureInfo.InvariantCulture),
                    selectedIndex = Convert.ToInt32(Read(chart, "SelectedIndex"), CultureInfo.InvariantCulture),
                    segmentCount = Count(segments),
                    visibleMinimum = Convert.ToDouble(Read(chart, "VisibleMinimum"), CultureInfo.InvariantCulture),
                    visibleMaximum = Convert.ToDouble(Read(chart, "VisibleMaximum"), CultureInfo.InvariantCulture),
                    rect = screenRect,
                    lineWidth = lineWidth,
                    pixelsPerUnit = pixelsPerUnit,
                    nativeLineWidth = lineWidth * pixelsPerUnit,
                    graphicAlpha = graphicAlpha,
                    mainStrokeOpacity = mainStrokeOpacity,
                    underStrokeOpacity = underStrokeOpacity,
                    effectiveMainStrokeAlpha = graphicAlpha * mainStrokeOpacity,
                    effectiveUnderStrokeAlpha = graphicAlpha * underStrokeOpacity,
                    underStrokeAdditionalWidth = Convert.ToSingle(Read(chart, "UnderStrokeAdditionalWidth"), CultureInfo.InvariantCulture),
                    risingColor = "#" + ColorUtility.ToHtmlStringRGB(rising),
                    fallingColor = "#" + ColorUtility.ToHtmlStringRGB(falling),
                    dryBrushTexture = plotTexture == null ? string.Empty : plotTexture.name,
                    plotShader = plotMaterial == null || plotMaterial.shader == null ? string.Empty : plotMaterial.shader.name,
                    series = CaptureChartSeries(chart)
                });
            }
            result.charts.Sort(delegate (VisualChartSemantic left, VisualChartSemantic right)
            {
                return string.CompareOrdinal(left.path, right.path);
            });
        }

        private static List<VisualChartSeriesSemantic> CaptureChartSeries(Component chart)
        {
            List<VisualChartSeriesSemantic> result = new List<VisualChartSeriesSemantic>();
            IEnumerable source = ReadField(chart, "_series") as IEnumerable;
            if (source == null)
                return result;
            float start = Convert.ToSingle(Read(chart, "VisibleStart"), CultureInfo.InvariantCulture);
            int range = Convert.ToInt32(Read(chart, "VisibleRange"), CultureInfo.InvariantCulture);
            int total = Convert.ToInt32(Read(chart, "TotalPointCount"), CultureInfo.InvariantCulture);
            int first = total == 0 ? -1 : Mathf.Clamp(Mathf.CeilToInt(start - 0.0000001f), 0, total - 1);
            int last = total == 0 ? -1 : Mathf.Clamp(Mathf.FloorToInt(start + range - 1f + 0.0000001f), 0, total - 1);
            foreach (object item in source)
            {
                Array aligned = ReadField(item, "AlignedPoints") as Array;
                int visible = 0;
                if (aligned != null)
                {
                    for (int i = Math.Max(0, first); i <= last && i < aligned.Length; i++)
                    {
                        if (aligned.GetValue(i) != null)
                            visible++;
                    }
                }
                result.Add(new VisualChartSeriesSemantic
                {
                    id = Convert.ToString(ReadField(item, "Id"), CultureInfo.InvariantCulture),
                    label = Convert.ToString(ReadField(item, "Label"), CultureInfo.InvariantCulture),
                    totalPointCount = Count(ReadField(item, "SourcePoints")),
                    visiblePointCount = visible
                });
            }
            return result;
        }

        private static void RequireChart(
            List<string> failures,
            VisualSemanticSnapshot snapshot,
            string mode,
            int visibleCount,
            float visibleStart,
            float yZoom,
            int totalCount,
            int occurrence = 1,
            string requiredPath = null)
        {
            int seen = 0;
            VisualChartSemantic found = null;
            for (int i = 0; i < snapshot.charts.Count; i++)
            {
                if (snapshot.charts[i].mode != mode ||
                    (!string.IsNullOrEmpty(requiredPath) && snapshot.charts[i].path.IndexOf(requiredPath, StringComparison.Ordinal) < 0))
                    continue;
                seen++;
                if (seen == occurrence)
                {
                    found = snapshot.charts[i];
                    break;
                }
            }
            if (found == null)
            {
                failures.Add("Required runtime chart is absent: " + mode + " occurrence " + occurrence + ".");
                return;
            }
            if (found.componentType != "XingShangMod.UI.Charting.WeeklyChartGraphic")
                failures.Add("Chart is not the formal WeeklyChartGraphic component: " + found.path + " type=" + found.componentType);
            if (found.totalPointCount != totalCount || found.visiblePointCount != visibleCount ||
                Math.Abs(found.visibleStart - visibleStart) > 0.001f || Math.Abs(found.yZoomFactor - yZoom) > 0.001f)
            {
                failures.Add("Chart semantic mismatch at " + found.path + ": expected total=" + totalCount +
                    ", visible=" + visibleCount + ", start=" + visibleStart.ToString(CultureInfo.InvariantCulture) +
                    ", zoom=" + yZoom.ToString(CultureInfo.InvariantCulture) + "; actual total=" + found.totalPointCount +
                    ", visible=" + found.visiblePointCount + ", start=" + found.visibleStart.ToString(CultureInfo.InvariantCulture) +
                    ", zoom=" + found.yZoomFactor.ToString(CultureInfo.InvariantCulture) + ".");
            }
            if (visibleCount > 1 && found.segmentCount <= 0)
                failures.Add("Runtime chart has no real line segments: " + found.path);
        }

        private static void RequireOverlay(
            List<string> failures,
            VisualSemanticSnapshot snapshot,
            string caseName,
            string overlay)
        {
            if (snapshot.caseName == caseName && !snapshot.overlays.Contains(overlay))
                failures.Add("Overlay was not captured over its real base page: " + overlay);
        }

        private static void RequireAssetSeries(List<string> failures, VisualSemanticSnapshot snapshot)
        {
            VisualChartSemantic chart = null;
            for (int i = 0; i < snapshot.charts.Count; i++)
            {
                if (snapshot.charts[i].mode == "Assets" &&
                    snapshot.charts[i].path.IndexOf("/AssetChartHost", StringComparison.Ordinal) >= 0)
                {
                    chart = snapshot.charts[i];
                    break;
                }
            }
            if (chart == null)
                return;
            if (chart.series.Count != 2)
            {
                failures.Add("Assets chart must contain both active series; actual count=" + chart.series.Count + ".");
                return;
            }
            bool profit = snapshot.caseName != "assets-ledger";
            string first = profit ? "profit" : "cash", second = profit ? "returnRate" : "goods";
            bool[] matched = new bool[2];
            for (int i = 0; i < chart.series.Count; i++)
            {
                VisualChartSeriesSemantic series = chart.series[i];
                int expectedIndex = series.id == first ? 0 : series.id == second ? 1 : -1;
                if (expectedIndex < 0 || matched[expectedIndex])
                {
                    failures.Add("Unexpected or duplicate asset series: " + series.id);
                    continue;
                }
                matched[expectedIndex] = true;
                if (series.totalPointCount != 78 || series.visiblePointCount != 26)
                {
                    failures.Add("Asset series " + series.id + " expected total=78 visible=26; actual total=" +
                        series.totalPointCount + " visible=" + series.visiblePointCount + ".");
                }
            }
            if (!matched[0] || !matched[1])
                failures.Add("Assets chart is missing " + first + " or " + second + ".");
        }

        private static void RequireReadonlyIndexHidden(List<string> failures, VisualSemanticSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                if (snapshot.texts[i].path.IndexOf("/IndexButton#", StringComparison.Ordinal) >= 0)
                    failures.Add("Readonly inventory still shows index quote content: " + snapshot.texts[i].path);
            }
            for (int i = 0; i < snapshot.buttons.Count; i++)
            {
                if (snapshot.buttons[i].path.IndexOf("/IndexButton#", StringComparison.Ordinal) >= 0)
                    failures.Add("Readonly inventory still shows the index button: " + snapshot.buttons[i].path);
            }
            for (int i = 0; i < snapshot.charts.Count; i++)
            {
                if (snapshot.charts[i].path.IndexOf("/IndexButton#", StringComparison.Ordinal) >= 0)
                    failures.Add("Readonly inventory unexpectedly captures an index Spark: " + snapshot.charts[i].path);
            }
        }

        private static void ValidateContentBounds(List<string> failures, VisualSemanticSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                if (!snapshot.texts[i].clipped && !string.IsNullOrEmpty(snapshot.texts[i].text))
                    ValidateRect(failures, snapshot, snapshot.texts[i].path, snapshot.texts[i].rect);
            }
            for (int i = 0; i < snapshot.buttons.Count; i++)
            {
                if (!snapshot.buttons[i].clipped)
                    ValidateRect(failures, snapshot, snapshot.buttons[i].path, snapshot.buttons[i].rect);
            }
            for (int i = 0; i < snapshot.images.Count; i++)
            {
                if (!snapshot.images[i].clipped)
                    ValidateRect(failures, snapshot, snapshot.images[i].path, snapshot.images[i].rect);
            }
            for (int i = 0; i < snapshot.layouts.Count; i++)
            {
                if (!snapshot.layouts[i].clipped)
                    ValidateRect(failures, snapshot, snapshot.layouts[i].path, snapshot.layouts[i].rect);
            }
        }

        private static void ValidateRect(
            List<string> failures,
            VisualSemanticSnapshot snapshot,
            string path,
            VisualRectSemantic rect)
        {
            const float tolerance = 2f;
            if (rect == null || float.IsNaN(rect.x) || float.IsNaN(rect.y) ||
                float.IsNaN(rect.width) || float.IsNaN(rect.height) || rect.width < 0f || rect.height < 0f)
            {
                failures.Add("Invalid screen rectangle: " + path);
                return;
            }
            if (rect.x < -tolerance || rect.y < -tolerance ||
                rect.x + rect.width > snapshot.requestedWidth + tolerance ||
                rect.y + rect.height > snapshot.requestedHeight + tolerance)
            {
                if (IsIntentionalFullScreenOverscan(snapshot, path, rect))
                {
                    string diagnostic = "Intentional full-screen backdrop overscan: " + path + " rect=" + FormatRect(rect) +
                        " viewport=" + snapshot.requestedWidth + "x" + snapshot.requestedHeight + ".";
                    if (!snapshot.intentionalOverscan.Contains(diagnostic))
                        snapshot.intentionalOverscan.Add(diagnostic);
                    return;
                }
                failures.Add("Visible content exceeds native viewport: " + path + " rect=" + FormatRect(rect) +
                    " viewport=" + snapshot.requestedWidth + "x" + snapshot.requestedHeight + ".");
            }
        }

        private static bool IsIntentionalFullScreenOverscan(
            VisualSemanticSnapshot snapshot,
            string path,
            VisualRectSemantic rect)
        {
            bool backdrop = false;
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                int suffix = segments[i].IndexOf('#');
                string name = suffix < 0 ? segments[i] : segments[i].Substring(0, suffix);
                if (name == "SceneBackground" || name == "Blocker" || name == "PaperGrain")
                {
                    backdrop = true;
                    break;
                }
            }
            if (!backdrop)
                return false;
            const float tolerance = 2f;
            return rect.x <= tolerance && rect.y <= tolerance &&
                rect.x + rect.width >= snapshot.requestedWidth - tolerance &&
                rect.y + rect.height >= snapshot.requestedHeight - tolerance;
        }

        private static void ValidateMarketLayout(List<string> failures, VisualSemanticSnapshot snapshot)
        {
            List<VisualLayoutSemantic> rows = new List<VisualLayoutSemantic>();
            VisualImageSemantic checkout = null;
            for (int i = 0; i < snapshot.layouts.Count; i++)
            {
                VisualLayoutSemantic layout = snapshot.layouts[i];
                if (layout.layoutType == "UnityEngine.UI.HorizontalLayoutGroup" &&
                    layout.path.IndexOf("/GoodsRow", StringComparison.Ordinal) >= 0)
                    rows.Add(layout);
            }
            if (rows.Count != 2)
                failures.Add("Market must contain exactly two goods rows; actual=" + rows.Count + ".");
            if (rows.Count == 2 && Math.Abs(rows[0].rect.height - rows[1].rect.height) > 4f)
                failures.Add("Market goods rows do not share the available height equally.");

            for (int i = 0; i < snapshot.images.Count; i++)
            {
                if (snapshot.images[i].path.IndexOf("/CheckoutButton#", StringComparison.Ordinal) >= 0)
                {
                    checkout = snapshot.images[i];
                    break;
                }
            }
            if (checkout == null)
            {
                failures.Add("Market checkout image was not captured.");
                return;
            }
            if (checkout.imageType != "Simple")
                failures.Add("Checkout uses a sliced transparent-border sprite at its compact target height; expected Simple, actual=" + checkout.imageType + ".");
            float expectedCheckoutHeight = snapshot.requestedWidth == 2560 && snapshot.requestedHeight == 1440
                ? 64.5f
                : snapshot.requestedHeight <= 790 ? 36f : 43f;
            if (Math.Abs(checkout.rect.height - expectedCheckoutHeight) > 2f)
                failures.Add("Checkout height differs from the measured HTML contract: expected=" +
                    expectedCheckoutHeight.ToString("0.###", CultureInfo.InvariantCulture) + ", actual=" +
                    checkout.rect.height.ToString("0.###", CultureInfo.InvariantCulture) + ".");
        }

        private static void ValidateInventoryLayout(List<string> failures, VisualSemanticSnapshot snapshot)
        {
            VisualLayoutSemantic sections = null;
            for (int i = 0; i < snapshot.layouts.Count; i++)
            {
                VisualLayoutSemantic layout = snapshot.layouts[i];
                if (layout.layoutType == "UnityEngine.UI.VerticalLayoutGroup" &&
                    layout.path.IndexOf("/ProfileContent#", StringComparison.Ordinal) >= 0 &&
                    layout.path.IndexOf("/Sections#", StringComparison.Ordinal) >= 0)
                {
                    sections = layout;
                    break;
                }
            }
            if (sections == null)
            {
                failures.Add("Inventory Sections layout was not captured.");
            }
            else if (sections.children.Count != 2)
            {
                failures.Add("Inventory must contain long and short sections; actual=" + sections.children.Count + ".");
            }
            else
            {
                VisualLayoutChildSemantic longSection = null;
                VisualLayoutChildSemantic shortSection = null;
                for (int i = 0; i < sections.children.Count; i++)
                {
                    if (sections.children[i].path.IndexOf("/LongSection#", StringComparison.Ordinal) >= 0)
                        longSection = sections.children[i];
                    if (sections.children[i].path.IndexOf("/ShortSection#", StringComparison.Ordinal) >= 0)
                        shortSection = sections.children[i];
                }
                if (longSection == null || shortSection == null)
                {
                    failures.Add("Inventory section identities could not be resolved.");
                }
                else
                {
                    float density = snapshot.requestedWidth >= 2304 && snapshot.requestedHeight >= 1296 ? 1.5f : 1f;
                    float gap = (snapshot.requestedWidth <= 1366 ? 7f : 12f) * density;
                    if (Math.Abs(longSection.rect.height - shortSection.rect.height) > 2f)
                        failures.Add("Inventory long/short sections do not share the remaining height equally.");
                    float occupiedHeight = longSection.rect.height + shortSection.rect.height + gap;
                    if (Math.Abs(sections.rect.height - occupiedHeight) > 2f)
                    {
                        failures.Add("Inventory leaves unused height below its sections: parent=" +
                            sections.rect.height.ToString("0.###", CultureInfo.InvariantCulture) +
                            ", children+gap=" + occupiedHeight.ToString("0.###", CultureInfo.InvariantCulture) + ".");
                    }
                    float parentTop = sections.rect.y + sections.rect.height;
                    float longTop = longSection.rect.y + longSection.rect.height;
                    if (Math.Abs(parentTop - longTop) > 2f || Math.Abs(sections.rect.y - shortSection.rect.y) > 2f)
                        failures.Add("Inventory sections do not touch the top and bottom of their available parent area.");
                }
            }

            List<VisualTextSemantic> metrics = new List<VisualTextSemantic>();
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                VisualTextSemantic text = snapshot.texts[i];
                if (text.path.IndexOf("/Summary#", StringComparison.Ordinal) >= 0 ||
                    text.path.IndexOf("/CapacityCell#", StringComparison.Ordinal) >= 0 ||
                    text.path.IndexOf("/WeightCell#", StringComparison.Ordinal) >= 0)
                    metrics.Add(text);
            }
            if (metrics.Count != 12)
                failures.Add("Inventory Summary must expose six captions and six values; actual text count=" + metrics.Count + ".");
            for (int i = 0; i < metrics.Count; i++)
            {
                if (metrics[i].visibleAreaRatio < 0.98f || metrics[i].geometryDiagnosis != "generated")
                    failures.Add("Inventory Summary text is clipped or not fully generated: " + metrics[i].path);
                for (int j = i + 1; j < metrics.Count; j++)
                {
                    if (OverlapArea(metrics[i].rect, metrics[j].rect) > 0.5f)
                        failures.Add("Inventory Summary text rectangles overlap: " + metrics[i].path + " <-> " + metrics[j].path);
                }
            }
        }

        private static float OverlapArea(VisualRectSemantic left, VisualRectSemantic right)
        {
            if (left == null || right == null)
                return 0f;
            float width = Math.Min(left.x + left.width, right.x + right.width) - Math.Max(left.x, right.x);
            float height = Math.Min(left.y + left.height, right.y + right.height) - Math.Max(left.y, right.y);
            return width <= 0f || height <= 0f ? 0f : width * height;
        }

        private static void ValidateLayoutWidths(List<string> failures, VisualSemanticSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.layouts.Count; i++)
            {
                VisualLayoutSemantic layout = snapshot.layouts[i];
                bool goods = layout.path.IndexOf("/GoodsRow", StringComparison.Ordinal) >= 0;
                bool assetCards = layout.path.IndexOf("/AssetCardsLayout", StringComparison.Ordinal) >= 0;
                bool inventoryGrid = layout.layoutType == "UnityEngine.UI.GridLayoutGroup" &&
                    (layout.path.IndexOf("/LongSection/", StringComparison.Ordinal) >= 0 ||
                     layout.path.IndexOf("/ShortSection/", StringComparison.Ordinal) >= 0);
                if (!goods && !assetCards && !inventoryGrid)
                    continue;
                int expectedChildren = goods ? 3 : assetCards ? 5 : -1;
                if (expectedChildren > 0 && layout.children.Count != expectedChildren)
                {
                    failures.Add("Layout child count mismatch at " + layout.path + ": expected=" + expectedChildren +
                        " actual=" + layout.children.Count + ".");
                    continue;
                }
                if (layout.expectedChildWidth <= 1f)
                {
                    failures.Add("Layout has no valid normal column width: " + layout.path + " expected=" +
                        layout.expectedChildWidth.ToString("0.###", CultureInfo.InvariantCulture) + "px.");
                    continue;
                }
                if (goods && layout.expectedChildWidth < snapshot.requestedWidth * 0.12f)
                {
                    failures.Add("Goods columns are abnormally narrow at " + layout.path + ": expected column=" +
                        layout.expectedChildWidth.ToString("0.###", CultureInfo.InvariantCulture) + "px for viewport " +
                        snapshot.requestedWidth + ".");
                }
                if (inventoryGrid)
                {
                    int expectedColumns = snapshot.requestedWidth <= 1366 ? 4 : 6;
                    if (layout.columnCount != expectedColumns)
                        failures.Add("Inventory grid column mismatch at " + layout.path + ": expected=" +
                            expectedColumns + " actual=" + layout.columnCount + ".");
                }
                float tolerance = Math.Max(2f, layout.expectedChildWidth * 0.04f);
                for (int child = 0; child < layout.children.Count; child++)
                {
                    float actual = layout.children[child].rect.width;
                    if (actual <= 1f || Math.Abs(actual - layout.expectedChildWidth) > tolerance)
                    {
                        failures.Add("Card/cell width mismatch at " + layout.children[child].path + ": actual=" +
                            actual.ToString("0.###", CultureInfo.InvariantCulture) + "px, normal=" +
                            layout.expectedChildWidth.ToString("0.###", CultureInfo.InvariantCulture) +
                            "px, tolerance=" + tolerance.ToString("0.###", CultureInfo.InvariantCulture) + "px.");
                    }
                }
            }
        }

        private static string FormatRect(VisualRectSemantic rect)
        {
            return "(" + rect.x.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                rect.y.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                rect.width.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                rect.height.ToString("0.###", CultureInfo.InvariantCulture) + ")";
        }

        private static object ReadField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, AllInstance);
            if (field == null)
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            return field.GetValue(target);
        }

        private static object Read(object target, string property)
        {
            PropertyInfo info = target.GetType().GetProperty(property, AllInstance);
            if (info == null)
                throw new MissingMemberException(target.GetType().FullName, property);
            return info.GetValue(target, null);
        }

        private static int Count(object value)
        {
            ICollection collection = value as ICollection;
            if (collection != null)
                return collection.Count;
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
                return 0;
            int count = 0;
            IEnumerator iterator = enumerable.GetEnumerator();
            while (iterator.MoveNext())
                count++;
            return count;
        }

        private static bool IsVisible(Graphic graphic)
        {
            if (graphic == null || !graphic.isActiveAndEnabled || graphic.color.a <= 0.001f)
                return false;
            if (IsMaskGraphicHidden(graphic.GetComponent<Mask>()))
                return false;
            Transform current = graphic.transform;
            while (current != null)
            {
                CanvasGroup group = current.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= 0.001f)
                    return false;
                current = current.parent;
            }
            return true;
        }

        private static bool IsMaskGraphicHidden(Mask mask)
        {
            return mask != null && IsMaskGraphicHidden(mask.showMaskGraphic);
        }

        private static bool IsMaskGraphicHidden(bool showMaskGraphic)
        {
            return !showMaskGraphic;
        }

        private static float EffectiveAlpha(Graphic graphic)
        {
            float alpha = graphic.color.a;
            Transform current = graphic.transform;
            while (current != null)
            {
                CanvasGroup group = current.GetComponent<CanvasGroup>();
                if (group != null)
                    alpha *= group.alpha;
                current = current.parent;
            }
            return alpha;
        }

        private static VisualRectSemantic ScreenRect(RectTransform rect, Canvas canvas)
        {
            if (rect == null)
                return new VisualRectSemantic();
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera camera = canvas == null ? null : canvas.worldCamera;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            int targetWidth = camera != null && camera.targetTexture != null ? camera.targetTexture.width : Screen.width;
            int targetHeight = camera != null && camera.targetTexture != null ? camera.targetTexture.height : Screen.height;
            RectTransform root = canvas.rootCanvas.transform as RectTransform;
            Rect rootRect = root.rect;
            if (rootRect.width <= 0.0001f || rootRect.height <= 0.0001f)
                throw new InvalidOperationException("Cannot normalize UI coordinates with a zero-sized root Canvas.");
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local = root.InverseTransformPoint(corners[i]);
                Vector2 point = new Vector2(
                    (local.x - rootRect.xMin) * targetWidth / rootRect.width,
                    (local.y - rootRect.yMin) * targetHeight / rootRect.height);
                minX = Math.Min(minX, point.x);
                minY = Math.Min(minY, point.y);
                maxX = Math.Max(maxX, point.x);
                maxY = Math.Max(maxY, point.y);
            }
            return new VisualRectSemantic
            {
                x = minX,
                y = minY,
                width = maxX - minX,
                height = maxY - minY
            };
        }

        private static bool IsClipped(Transform transform)
        {
            Transform current = transform == null ? null : transform.parent;
            while (current != null)
            {
                RectMask2D rectMask = current.GetComponent<RectMask2D>();
                Mask mask = current.GetComponent<Mask>();
                ScrollRect scroll = current.GetComponent<ScrollRect>();
                if ((rectMask != null && rectMask.enabled) || (mask != null && mask.enabled) ||
                    (scroll != null && scroll.enabled))
                    return true;
                current = current.parent;
            }
            return false;
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

        public static string StripRichText(string value, bool richText)
        {
            if (!richText || string.IsNullOrEmpty(value) || value.IndexOf('<') < 0)
                return value;
            StringBuilder result = new StringBuilder(value.Length);
            bool inside = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '<')
                {
                    inside = true;
                    continue;
                }
                if (inside && value[i] == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                    result.Append(value[i]);
            }
            return result.ToString();
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private static string Hex(byte[] bytes)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }
}
