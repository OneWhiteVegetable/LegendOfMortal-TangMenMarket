using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class Round4TypographyRect
    {
        public float left;
        public float top;
        public float right;
        public float bottom;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class Round4TypographyTextState
    {
        public string role;
        public string text;
        public string font;
        public int fontSize;
        public float canvasScaleFactor;
        public float textPixelsPerUnit;
        public float glyphDensity;
        public float rasterFontSize;
        public int physicalVertexCount;
        public int meshVertexCount;
        public string generationFailure;
        public float physicalPixelsPerUnit;
        public float preferredWidth;
        public float preferredHeight;
        public float rectWidth;
        public float rectHeight;
        public int lineCount;
        public int characterCountVisible;
        public int expectedCharacterCount;
        public Round4TypographyRect screenRect;
    }

    [Serializable]
    public sealed class Round4TypographyMarketState
    {
        public string label;
        public string png;
        public Round4TypographyTextState title;
        public Round4TypographyTextState category;
    }

    [Serializable]
    public sealed class Round4TypographyTooltipState
    {
        public string label;
        public string png;
        public float rootCanvasScaleFactor;
        public float rootLocalScale;
        public float rootRectWidth;
        public float rootRectHeight;
        public Round4TypographyRect rootScreenRect;
        public Round4TypographyTextState title;
        public Round4TypographyTextState body;
        public Round4TypographyTextState hint;
    }

    [Serializable]
    public sealed class Round4TypographyReport
    {
        public int schemaVersion = 1;
        public string status = "running";
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public string runtimeAssembly;
        public string runtimeAssemblySha256;
        public string bundle;
        public string bundleSha256;
        public int screenWidth;
        public int screenHeight;
        public string coordinateSpace = "lossless physical PNG pixels, origin top-left; half-open crop rectangles are produced by the companion script";
        public string evidenceBoundary = "Unity Editor formal runtime View with synthetic keyboard focus; low-density controls toggle only MarketTextInkEffect glyph density in the same build. Not game footage or an OS mouse test.";
        public Round4TypographyMarketState marketLow;
        public Round4TypographyMarketState marketHigh;
        public Round4TypographyTooltipState item01Low;
        public Round4TypographyTooltipState item01High;
        public Round4TypographyTooltipState item07Low;
        public Round4TypographyTooltipState item07High;
        public List<string> failures = new List<string>();
    }

    public static class Round4TypographyValidation
    {
        internal const string PendingKey = "XingShang.Round4Typography.Pending";
        internal const string ConfigurationKey = "XingShang.Round4Typography.Configuration";
        internal const string ExitCodeKey = "XingShang.Round4Typography.ExitCode";

        public static void BatchEntry()
        {
            try
            {
                if (Application.isBatchMode)
                    throw new InvalidOperationException("Round4 typography validation requires an Editor Game View; do not use -batchmode.");
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before starting Round4 typography validation.");
                RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
                options.OutputPath = Path.Combine(options.ProjectPath, "Build/Round4Typography");
                ApplyCommandLine(options);
                options.BepInExPluginPath = Path.Combine(options.OutputPath, "plugin-root");
                options.Validate();
                PrepareIsolatedBundle(options);
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save or close dirty scenes before Round4 typography validation.");
                }
                SessionState.SetString(ConfigurationKey, JsonUtility.ToJson(options, false));
                SessionState.SetBool(PendingKey, true);
                SessionState.SetInt(ExitCodeKey, -1);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void PrepareIsolatedBundle(RuntimePreviewOptions options)
        {
            string directory = Path.Combine(options.BepInExPluginPath,
                "XingShangMod", "Runtime", "bundles");
            Directory.CreateDirectory(directory);
            File.Copy(options.BundlePath, Path.Combine(directory, "xingshang_ui.bundle"), true);
        }

        private static void ApplyCommandLine(RuntimePreviewOptions options)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-round4TypographyOutput": options.OutputPath = Path.GetFullPath(args[i + 1]); break;
                    case "-runtimePreviewDll": options.PluginAssemblyPath = Path.GetFullPath(args[i + 1]); break;
                    case "-runtimePreviewFixture": options.FixturePath = Path.GetFullPath(args[i + 1]); break;
                    case "-round4TypographyItems": options.ItemsPath = Path.GetFullPath(args[i + 1]); break;
                    case "-round4TypographyNews": options.NewsPath = Path.GetFullPath(args[i + 1]); break;
                    case "-round4TypographyBundle": options.BundlePath = Path.GetFullPath(args[i + 1]); break;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class Round4TypographyValidationBootstrap
    {
        static Round4TypographyValidationBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += CompletePendingExit;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !SessionState.GetBool(Round4TypographyValidation.PendingKey, false))
                return;
            SessionState.SetBool(Round4TypographyValidation.PendingKey, false);
            try
            {
                RuntimePreviewOptions options = JsonUtility.FromJson<RuntimePreviewOptions>(
                    SessionState.GetString(Round4TypographyValidation.ConfigurationKey, string.Empty));
                GameObject host = new GameObject("XingShang.Round4Typography.Runner");
                host.AddComponent<Round4TypographyValidationRunner>().Configure(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetInt(Round4TypographyValidation.ExitCodeKey, 1);
                EditorApplication.isPlaying = false;
            }
        }

        private static void CompletePendingExit()
        {
            int exitCode = SessionState.GetInt(Round4TypographyValidation.ExitCodeKey, -1);
            if (exitCode < 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            SessionState.SetInt(Round4TypographyValidation.ExitCodeKey, -1);
            Debug.Log("Round4 typography validation exiting Editor with code " + exitCode + ".");
            EditorApplication.Exit(exitCode);
        }
    }

    [DefaultExecutionOrder(2000)]
    public sealed class Round4TypographyValidationRunner : MonoBehaviour
    {
        private RuntimePreviewOptions _options;
        private float? _densityOverride;
        private RuntimePreviewAdapter _adapter;
        private Round4TypographyReport _report;
        private Camera _camera;
        private GameObject _cameraObject;
        private RenderTexture _target;

        internal void Configure(RuntimePreviewOptions options)
        {
            _options = options;
        }

        private IEnumerator Start()
        {
            Exception fatal = null;
            _report = new Round4TypographyReport
            {
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                runtimeAssembly = _options.PluginAssemblyPath,
                runtimeAssemblySha256 = VisualValidation.Sha256File(_options.PluginAssemblyPath),
                bundle = _options.BundlePath,
                bundleSha256 = VisualValidation.Sha256File(_options.BundlePath)
            };
            try
            {
                Directory.CreateDirectory(_options.OutputPath);
                UiFonts.ValidateImportedFonts();
                CreateCamera();
                _adapter = new RuntimePreviewAdapter(_options);
            }
            catch (Exception exception)
            {
                fatal = exception;
            }

            if (fatal == null)
            {
                IEnumerator validation = RunValidation();
                while (true)
                {
                    object yielded = null;
                    try
                    {
                        if (!validation.MoveNext())
                            break;
                        yielded = validation.Current;
                    }
                    catch (Exception exception)
                    {
                        fatal = exception;
                        break;
                    }
                    yield return yielded;
                }
            }

            int exitCode = 0;
            if (fatal != null)
            {
                _report.failures.Add(fatal.ToString());
                Debug.LogException(fatal);
                exitCode = 1;
            }
            if (_report.failures.Count > 0)
                exitCode = 1;
            _report.status = exitCode == 0 ? "passed" : "failed";
            _report.finishedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            try
            {
                VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "report.json"), _report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            if (_adapter != null)
                _adapter.Dispose();
            if (_camera != null)
                _camera.targetTexture = null;
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
            }
            if (_cameraObject != null)
                Destroy(_cameraObject);
            Debug.Log("Round4 typography report: " + Path.Combine(_options.OutputPath, "report.json"));
            yield return null;
            SessionState.SetInt(Round4TypographyValidation.ExitCodeKey, exitCode);
            EditorApplication.isPlaying = false;
        }

        private IEnumerator RunValidation()
        {
            yield return RuntimePreviewGameView.SetNativeSize(2560, 1440);
            bool viewportReady = false;
            for (int frame = 0; frame < 90; frame++)
            {
                if (Screen.width == 2560 && Screen.height == 1440)
                {
                    viewportReady = true;
                    break;
                }
                yield return new WaitForEndOfFrame();
            }
            if (!viewportReady)
                throw new InvalidOperationException("Game View did not reach 2560x1440; actual=" + Screen.width + "x" + Screen.height + ".");
            _report.screenWidth = Screen.width;
            _report.screenHeight = Screen.height;
            _target = new RenderTexture(2560, 1440, 24, RenderTextureFormat.ARGB32)
            {
                name = "Round4Typography-2560x1440",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            _target.Create();
            _camera.targetTexture = _target;
            EnsureEventSystem();
            _adapter.OpenCase("market-empty", true);
            PrepareCanvases();
            yield return WaitFrames(15);

            Text title = FindUniqueText("仿南宫尖肩甲");
            Transform card = FindAncestor(title.transform, "GoodsCard");
            Text category = FindChildText(card, "CategoryText");
            Check(card != null && category != null, "The 仿南宫尖肩甲 card or its CategoryText is missing.");
            Check(DensityEffect(title) != null && DensityEffect(category) != null,
                "High-density goods text does not own MarketTextInkEffect.");
            float titleDensity = GlyphDensity(title);
            float categoryDensity = GlyphDensity(category);

            SetTextDensity(title, 1f);
            SetTextDensity(category, 1f);
            yield return WaitFrames(4);
            _report.marketLow = CaptureMarket("low-density-control", title, category,
                "market-low-density-control-2560x1440.png");

            SetTextDensity(title, titleDensity);
            SetTextDensity(category, categoryDensity);
            yield return WaitFrames(4);
            _report.marketHigh = CaptureMarket("physical-density", title, category,
                "market-physical-density-2560x1440.png");
            CompareText(_report.marketLow.title, _report.marketHigh.title, "goods title");
            CompareText(_report.marketLow.category, _report.marketHigh.category, "goods category");
            Check(_report.marketHigh.title.text == "仿南宫尖肩甲", "The goods title changed during density validation.");
            Check(_report.marketHigh.category.text.StartsWith("行货", StringComparison.Ordinal),
                "The goods category text changed during density validation.");
            CheckDensity(_report.marketLow.title, _report.marketHigh.title, "goods title", 1.174f);
            CheckDensity(_report.marketLow.category, _report.marketHigh.category, "goods category", 1f);

            yield return SelectCardAndWait(0);
            RectTransform tooltip = _adapter.TooltipRoot;
            Check(tooltip != null && tooltip.gameObject.activeSelf, "Item 01 tooltip did not open from formal keyboard focus.");
            Canvas tooltipCanvas = tooltip.GetComponent<Canvas>();
            Check(tooltipCanvas != null, "Tooltip root Canvas is missing.");
            float tooltipDensity = GlyphDensity(FindChildText(tooltip, "BodyText"));
            yield return CaptureTooltipPair(tooltip, tooltipDensity, "item01", 6, "mod:01");

            EventSystem.current.SetSelectedGameObject(null);
            yield return WaitFrames(4);
            Button next = RuntimePreviewMarketInteractionProbe.MarketButton(_adapter, "NextPageButton");
            Check(next != null && next.IsInteractable(), "NextPageButton is unavailable for item 07 typography validation.");
            ExecuteEvents.Execute(next.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return WaitFrames(12);
            Text pageTwoTitle = RuntimePreviewMarketInteractionProbe.CardText(_adapter, 0, "NameText");
            Check(pageTwoTitle != null && pageTwoTitle.text == "快报印花",
                "Paging did not bind item 07 before the long-tooltip validation.");
            yield return SelectCardAndWait(0);
            tooltip = _adapter.TooltipRoot;
            tooltipDensity = GlyphDensity(FindChildText(tooltip, "BodyText"));
            yield return CaptureTooltipPair(tooltip, tooltipDensity, "item07", 8, "mod:07");
            EventSystem.current.SetSelectedGameObject(null);
            yield return WaitFrames(2);
        }

        private IEnumerator CaptureTooltipPair(
            RectTransform tooltip,
            float restoredDensity,
            string itemKey,
            int expectedBodyLines,
            string expectedHint)
        {
            SetTooltipDensity(tooltip, 1f);
            yield return WaitFrames(4);
            Round4TypographyTooltipState low = CaptureTooltip(itemKey + "-low-density-control", tooltip,
                "tooltip-" + itemKey + "-low-density-control-2560x1440.png");
            SetTooltipDensity(tooltip, restoredDensity);
            yield return WaitFrames(4);
            Round4TypographyTooltipState high = CaptureTooltip(itemKey + "-physical-density", tooltip,
                "tooltip-" + itemKey + "-physical-density-2560x1440.png");
            if (itemKey == "item01")
            {
                _report.item01Low = low;
                _report.item01High = high;
            }
            else
            {
                _report.item07Low = low;
                _report.item07High = high;
            }

            CompareText(low.title, high.title, itemKey + " tooltip title");
            CompareText(low.body, high.body, itemKey + " tooltip body");
            CompareText(low.hint, high.hint, itemKey + " tooltip hint");
            Check(Nearly(low.rootRectWidth, high.rootRectWidth, 0.01f) &&
                Nearly(low.rootRectHeight, high.rootRectHeight, 0.01f),
                itemKey + " tooltip logical size changed with font density.");
            CompareRect(low.rootScreenRect, high.rootScreenRect, itemKey + " tooltip root", 0.05f);
            Check(Nearly(high.rootCanvasScaleFactor, 1f, 0.001f) &&
                Nearly(high.rootLocalScale, 1.5f, 0.001f),
                itemKey + " tooltip root scale or inherited Canvas scale changed.");
            Check(high.body.lineCount == expectedBodyLines,
                itemKey + " tooltip body line count changed: expected=" + expectedBodyLines + " actual=" + high.body.lineCount + ".");
            Check(high.hint.text.IndexOf(expectedHint, StringComparison.Ordinal) >= 0,
                itemKey + " tooltip item identifier is incomplete: " + high.hint.text);
            Check(high.body.characterCountVisible == high.body.expectedCharacterCount &&
                high.title.characterCountVisible == high.title.expectedCharacterCount &&
                high.hint.characterCountVisible == high.hint.expectedCharacterCount,
                itemKey + " tooltip title, body or hint was truncated after density resampling.");
            CheckDensity(low.body, high.body, itemKey + " tooltip body", 1f);
        }

        private Round4TypographyMarketState CaptureMarket(
            string label,
            Text title,
            Text category,
            string fileName)
        {
            Canvas.ForceUpdateCanvases();
            string png = Path.Combine(_options.OutputPath, fileName);
            WriteScreenshot(png);
            return new Round4TypographyMarketState
            {
                label = label,
                png = png,
                title = CaptureText("goods-title", title),
                category = CaptureText("goods-category", category)
            };
        }

        private Round4TypographyTooltipState CaptureTooltip(
            string label,
            RectTransform root,
            string fileName)
        {
            Canvas.ForceUpdateCanvases();
            string png = Path.Combine(_options.OutputPath, fileName);
            WriteScreenshot(png);
            Canvas canvas = root.GetComponent<Canvas>();
            return new Round4TypographyTooltipState
            {
                label = label,
                png = png,
                rootCanvasScaleFactor = canvas == null ? 0f : canvas.scaleFactor,
                rootLocalScale = root.localScale.x,
                rootRectWidth = root.rect.width,
                rootRectHeight = root.rect.height,
                rootScreenRect = ScreenRect(root),
                title = CaptureText("tooltip-title", FindChildText(root, "TitleText")),
                body = CaptureText("tooltip-body", FindChildText(root, "BodyText")),
                hint = CaptureText("tooltip-hint", FindChildText(root, "HintText"))
            };
        }

        private Round4TypographyTextState CaptureText(string role, Text text)
        {
            if (text == null)
                throw new InvalidOperationException("Cannot capture missing text role " + role + ".");
            Canvas.ForceUpdateCanvases();
            TextGenerator generator = text.cachedTextGenerator;
            return new Round4TypographyTextState
            {
                role = role,
                text = text.text ?? string.Empty,
                font = text.font == null ? string.Empty : text.font.name,
                fontSize = text.fontSize,
                canvasScaleFactor = text.canvas == null ? 0f : text.canvas.scaleFactor,
                textPixelsPerUnit = text.pixelsPerUnit,
                glyphDensity = GlyphDensity(text),
                rasterFontSize = EffectInt(text, "_rasterGlyphFontSize", text.fontSize),
                physicalVertexCount = EffectInt(text, "_physicalVertexCount", 0),
                meshVertexCount = EffectInt(text, "_meshVertexCount", 0),
                generationFailure = EffectString(text, "_glyphGenerationFailure"),
                physicalPixelsPerUnit = PhysicalPixelsPerUnit(text),
                preferredWidth = text.preferredWidth,
                preferredHeight = text.preferredHeight,
                rectWidth = text.rectTransform.rect.width,
                rectHeight = text.rectTransform.rect.height,
                lineCount = generator == null ? 0 : generator.lineCount,
                characterCountVisible = generator == null ? 0 : generator.characterCountVisible,
                expectedCharacterCount = (text.text ?? string.Empty).Length,
                screenRect = ScreenRect(text.rectTransform)
            };
        }

        private void CompareText(
            Round4TypographyTextState low,
            Round4TypographyTextState high,
            string role)
        {
            Check(low.text == high.text && low.font == high.font && low.fontSize == high.fontSize,
                role + " text, font or layout fontSize changed with density.");
            Check(Nearly(low.preferredWidth, high.preferredWidth, 0.05f) &&
                Nearly(low.preferredHeight, high.preferredHeight, 0.05f),
                role + " preferred layout metrics changed with density: low=" +
                low.preferredWidth + "x" + low.preferredHeight + " high=" +
                high.preferredWidth + "x" + high.preferredHeight + ".");
            Check(Nearly(low.rectWidth, high.rectWidth, 0.05f) &&
                Nearly(low.rectHeight, high.rectHeight, 0.05f),
                role + " RectTransform size changed with density.");
            Check(low.lineCount == high.lineCount,
                role + " wrapping changed with density: low=" + low.lineCount + " high=" + high.lineCount + ".");
            Check(high.characterCountVisible == high.expectedCharacterCount,
                role + " has invisible generated characters: " + high.characterCountVisible + "/" + high.expectedCharacterCount + ".");
            CompareRect(low.screenRect, high.screenRect, role, 0.05f);
        }

        private void CheckDensity(
            Round4TypographyTextState low,
            Round4TypographyTextState high,
            string role,
            float expectedVisualMeshScale)
        {
            Check(high.glyphDensity >= low.glyphDensity * 1.25f,
                role + " did not increase dynamic-font sampling density: low=" +
                low.glyphDensity + " high=" + high.glyphDensity + ".");
            float expectedDensity = high.physicalPixelsPerUnit * expectedVisualMeshScale;
            Check(Nearly(high.glyphDensity, expectedDensity, 0.025f),
                role + " glyph density does not match final physical scale: density=" +
                high.glyphDensity + " expected=" + expectedDensity + ".");
            int expectedRasterSize = Mathf.Max(high.fontSize, Mathf.CeilToInt(high.fontSize * expectedDensity));
            Check(Nearly(high.rasterFontSize, expectedRasterSize, 0.01f),
                role + " raster font size does not match its glyph generation settings: actual=" +
                high.rasterFontSize + " expected=" + expectedRasterSize + ".");
            Check(high.meshVertexCount > 0 && high.physicalVertexCount == high.meshVertexCount,
                role + " did not generate every glyph vertex at physical density: " + high.physicalVertexCount + "/" + high.meshVertexCount +
                "; " + high.generationFailure + ".");
        }

        private void CompareRect(
            Round4TypographyRect low,
            Round4TypographyRect high,
            string role,
            float tolerance)
        {
            Check(Nearly(low.left, high.left, tolerance) && Nearly(low.top, high.top, tolerance) &&
                Nearly(low.width, high.width, tolerance) && Nearly(low.height, high.height, tolerance),
                role + " screen geometry changed with density: low=" + Format(low) + " high=" + Format(high) + ".");
        }

        private static string Format(Round4TypographyRect rect)
        {
            return rect.left + "," + rect.top + " " + rect.width + "x" + rect.height;
        }

        private void LateUpdate()
        {
            if (!_densityOverride.HasValue)
                return;
            Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                BaseMeshEffect effect = DensityEffect(texts[i]);
                if (effect == null)
                    continue;
                FieldInfo field = effect.GetType().GetField("_glyphDensity",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                    continue;
                float current = Convert.ToSingle(field.GetValue(effect), CultureInfo.InvariantCulture);
                if (Nearly(current, _densityOverride.Value, 0.00001f))
                    continue;
                field.SetValue(effect, _densityOverride.Value);
                SetPhysicalGeneratorDensity(texts[i], _densityOverride.Value);
                texts[i].SetVerticesDirty();
            }
        }

        private void SetTooltipDensity(RectTransform root, float density)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                SetTextDensity(texts[i], density);
            Canvas.ForceUpdateCanvases();
        }

        private void SetTextDensity(Text text, float density)
        {
            _densityOverride = density <= 1.0001f ? (float?)density : null;
            BaseMeshEffect effect = DensityEffect(text);
            if (effect == null)
                throw new InvalidOperationException("Missing glyph density effect on " + text.name + ".");
            FieldInfo field = effect.GetType().GetField("_glyphDensity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(effect.GetType().FullName, "_glyphDensity");
            field.SetValue(effect, density);
            SetPhysicalGeneratorDensity(text, density);
            text.SetVerticesDirty();
            Canvas.ForceUpdateCanvases();
        }

        private static void SetPhysicalGeneratorDensity(Text text, float density)
        {
            Component[] components = text.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].GetType().FullName != "XingShangMod.UI.Market.MarketPhysicalText")
                    continue;
                MethodInfo method = components[i].GetType().GetMethod("SetDensity",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                    throw new MissingMethodException(components[i].GetType().FullName, "SetDensity");
                method.Invoke(components[i], new object[] { density });
                return;
            }
            throw new InvalidOperationException("Missing MarketPhysicalText on " + text.name + ".");
        }

        private static BaseMeshEffect DensityEffect(Text text)
        {
            if (text == null)
                return null;
            BaseMeshEffect[] effects = text.GetComponents<BaseMeshEffect>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i].GetType().FullName == "XingShangMod.UI.Market.MarketTextInkEffect")
                    return effects[i];
            }
            return null;
        }

        private static float GlyphDensity(Text text)
        {
            BaseMeshEffect effect = DensityEffect(text);
            if (effect == null)
                return 1f;
            FieldInfo field = effect.GetType().GetField("_glyphDensity",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? 1f : Convert.ToSingle(field.GetValue(effect), CultureInfo.InvariantCulture);
        }

        private static int EffectInt(Text text, string fieldName, int fallback)
        {
            BaseMeshEffect effect = DensityEffect(text);
            FieldInfo field = effect == null ? null : effect.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? fallback : Convert.ToInt32(field.GetValue(effect), CultureInfo.InvariantCulture);
        }

        private static string EffectString(Text text, string fieldName)
        {
            BaseMeshEffect effect = DensityEffect(text);
            FieldInfo field = effect == null ? null : effect.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? string.Empty : Convert.ToString(field.GetValue(effect), CultureInfo.InvariantCulture);
        }

        private IEnumerator SelectCardAndWait(int index)
        {
            EventSystem.current.SetSelectedGameObject(null);
            yield return WaitFrames(2);
            EventSystem.current.SetSelectedGameObject(_adapter.CardRoot(index));
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 2f)
            {
                RectTransform tooltip = _adapter.TooltipRoot;
                CanvasGroup group = tooltip == null ? null : tooltip.GetComponent<CanvasGroup>();
                if (tooltip != null && tooltip.gameObject.activeSelf && group != null && group.alpha >= 0.999f)
                    yield break;
                yield return new WaitForEndOfFrame();
            }
            throw new InvalidOperationException("Tooltip did not settle after formal keyboard focus on card " + index + ".");
        }

        private Text FindUniqueText(string value)
        {
            Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>();
            Text result = null;
            for (int i = 0; i < texts.Length; i++)
            {
                if (!texts[i].gameObject.activeInHierarchy || texts[i].text != value ||
                    texts[i].name != "NameText" || FindAncestor(texts[i].transform, "GoodsCard") == null)
                    continue;
                if (result != null)
                    throw new InvalidOperationException("Multiple active text nodes contain " + value + ".");
                result = texts[i];
            }
            if (result == null)
                throw new InvalidOperationException("No active text node contains " + value + ".");
            return result;
        }

        private static Text FindChildText(Transform root, string childName)
        {
            if (root == null)
                return null;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name == childName)
                    return texts[i];
            }
            return null;
        }

        private static Transform FindAncestor(Transform current, string prefix)
        {
            while (current != null)
            {
                if (current.name.StartsWith(prefix, StringComparison.Ordinal))
                    return current;
                current = current.parent;
            }
            return null;
        }

        private float PhysicalPixelsPerUnit(Text text)
        {
            Rect rect = text.rectTransform.rect;
            if (rect.width <= 0.001f || rect.height <= 0.001f)
                return 0f;
            Vector3[] corners = new Vector3[4];
            text.rectTransform.GetWorldCorners(corners);
            Camera camera = ResolveCamera(text.rectTransform);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);
            float horizontal = Vector2.Distance(bottomLeft, bottomRight) / rect.width;
            float vertical = Vector2.Distance(bottomLeft, topLeft) / rect.height;
            return Mathf.Max(horizontal, vertical);
        }

        private Round4TypographyRect ScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera camera = ResolveCamera(rect);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            float left = Mathf.Min(bottomLeft.x, topRight.x);
            float right = Mathf.Max(bottomLeft.x, topRight.x);
            float bottom = Mathf.Min(bottomLeft.y, topRight.y);
            float top = Mathf.Max(bottomLeft.y, topRight.y);
            return new Round4TypographyRect
            {
                left = left,
                top = Screen.height - top,
                right = right,
                bottom = Screen.height - bottom,
                width = right - left,
                height = top - bottom
            };
        }

        private Camera ResolveCamera(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas != null)
                canvas = canvas.rootCanvas;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;
            return canvas.worldCamera == null ? _camera : canvas.worldCamera;
        }

        private void CreateCamera()
        {
            _cameraObject = new GameObject("XingShang.Round4Typography.Camera", typeof(Camera));
            _camera = _cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.027f, 0.035f, 0.031f, 1f);
            _camera.orthographic = true;
            _camera.cullingMask = -1;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void PrepareCanvases()
        {
            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.gameObject.scene.IsValid() || canvas.transform.parent != null)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _camera;
                canvas.planeDistance = 1f;
                canvas.overrideSorting = true;
            }
            _camera.orthographicSize = Screen.height / 200f;
            Canvas.ForceUpdateCanvases();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            new GameObject("XingShang.Round4Typography.EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void WriteScreenshot(string path)
        {
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(2560, 1440, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = _target;
                image.ReadPixels(new Rect(0f, 0f, 2560f, 1440f), 0, 0);
                image.Apply(false, false);
                byte[] png = ImageConversion.EncodeToPNG(image);
                if (png == null || png.Length == 0)
                    throw new InvalidDataException("PNG encoding returned no data.");
                File.WriteAllBytes(path, png);
            }
            finally
            {
                RenderTexture.active = previous;
                Destroy(image);
            }
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return new WaitForEndOfFrame();
        }

        private void Check(bool condition, string message)
        {
            if (!condition && !_report.failures.Contains(message))
                _report.failures.Add(message);
        }

        private static bool Nearly(float left, float right, float tolerance)
        {
            return Mathf.Abs(left - right) <= tolerance;
        }
    }
}
