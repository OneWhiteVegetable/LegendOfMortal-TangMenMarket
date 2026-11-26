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
    public sealed class RuntimePreviewOptions
    {
        public string ProjectPath;
        public string OutputPath;
        public string PluginAssemblyPath;
        public string FixturePath;
        public string ItemsPath;
        public string NewsPath;
        public string BundlePath;
        public string ManagedPath;
        public string BepInExCorePath;
        public string BepInExPluginPath;
        public string[] Cases;
        public int[] Widths;
        public int[] Heights;
        public bool ExitEditorWhenDone;

        public static RuntimePreviewOptions CreateDefault()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string repository = Path.GetFullPath(Path.Combine(project, ".."));
            string workspace = Path.GetFullPath(Path.Combine(repository, ".."));
            const string gameRoot = "C:/allsoft/steam/steamapps/common/LegendOfMortal";
            string runtime = Path.Combine(repository, "Runtime");
            string fixture = Path.Combine(repository, "tests/fixtures/demo-market.json");
            return new RuntimePreviewOptions
            {
                ProjectPath = project,
                OutputPath = Path.Combine(project, "Build/RuntimePreview"),
                PluginAssemblyPath = Path.Combine(repository, "bin/Release/XingShangMod.dll"),
                FixturePath = File.Exists(fixture) ? fixture : Path.Combine(workspace, "UI/design_UI/data/demo-market.json"),
                ItemsPath = Path.Combine(runtime, "items.json"),
                NewsPath = Path.Combine(runtime, "news.json"),
                BundlePath = Path.Combine(runtime, "bundles/xingshang_ui.bundle"),
                ManagedPath = Path.Combine(gameRoot, "Mortal_Data/Managed"),
                BepInExCorePath = Path.Combine(gameRoot, "BepInEx/core"),
                BepInExPluginPath = workspace,
                Cases = RuntimePreview.DefaultCases(),
                Widths = new[] { 2560, 1920, 1280, 1366 },
                Heights = new[] { 1440, 1080, 720, 768 },
                ExitEditorWhenDone = false
            };
        }

        public void Validate()
        {
            RequireFile(PluginAssemblyPath, "formal runtime DLL");
            RequireFile(FixturePath, "shared V4 fixture");
            RequireFile(ItemsPath, "formal item catalog");
            RequireFile(NewsPath, "formal news catalog");
            RequireFile(BundlePath, "UI AssetBundle");
            if (!Directory.Exists(ManagedPath))
                throw new DirectoryNotFoundException("Game Managed directory is unavailable: " + ManagedPath);
            if (!Directory.Exists(BepInExCorePath))
                throw new DirectoryNotFoundException("BepInEx core directory is unavailable: " + BepInExCorePath);
            if (Cases == null || Cases.Length == 0 || Widths == null || Heights == null ||
                Widths.Length != Heights.Length || Widths.Length == 0)
                throw new InvalidOperationException("Runtime Preview cases and native viewports must be configured.");
            for (int i = 0; i < Widths.Length; i++)
            {
                if (Widths[i] <= 0 || Heights[i] <= 0)
                    throw new InvalidOperationException("Runtime Preview viewport dimensions must be positive.");
            }
        }

        private static void RequireFile(string path, string description)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("Runtime Preview requires " + description + ".", path);
        }
    }

    [Serializable]
    public sealed class RuntimePreviewRunReport
    {
        public int schemaVersion = 1;
        public string status;
        public string fixtureId;
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public string operatingSystem;
        public string gpu;
        public string gpuVersion;
        public string runtimeAssembly;
        public string runtimeAssemblySha256;
        public string bundle;
        public string bundleSha256;
        public string editorSourcesSha256;
        public List<RuntimePreviewCaseReport> cases = new List<RuntimePreviewCaseReport>();
        public List<RuntimePreviewLogEntry> errors = new List<RuntimePreviewLogEntry>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class RuntimePreviewCaseReport
    {
        public string caseName;
        public int width;
        public int height;
        public string status;
        public string png;
        public string semantics;
        public int stableFrames;
        public string runStateSha256;
        public VisualImageValidation image;
        public List<RuntimePreviewComponentRectEvidence> componentRects = new List<RuntimePreviewComponentRectEvidence>();
        public List<RuntimePreviewPixelProbeEvidence> pixelProbes = new List<RuntimePreviewPixelProbeEvidence>();
        public MarketSmallScreenLayoutEvidence smallScreenLayout;
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class RuntimePreviewLogEntry
    {
        public string type;
        public string condition;
        public string stackTrace;
    }

    public static class RuntimePreview
    {
        internal const string ConfigurationKey = "XingShang.RuntimePreview.Configuration";
        internal const string PendingKey = "XingShang.RuntimePreview.Pending";
        internal const string InteractionPendingKey = "XingShang.RuntimePreview.Interactions.Pending";
        internal const string ExitCodeKey = "XingShang.RuntimePreview.ExitCode";

        [MenuItem("XingShang/Runtime Preview/Run Full Visual Validation")]
        public static void Run()
        {
            Run(RuntimePreviewOptions.CreateDefault());
        }

        public static void Run(RuntimePreviewOptions options)
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Runtime Preview requires an Editor Game View; run with -executeMethod but without -batchmode.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting Runtime Preview.");
            if (options == null)
                throw new ArgumentNullException("options");
            options.Validate();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close dirty scenes before Runtime Preview; no scene changes are discarded.");
            }
            SessionState.SetString(ConfigurationKey, JsonUtility.ToJson(options, false));
            SessionState.SetBool(InteractionPendingKey, false);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(ExitCodeKey, -1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public static void BatchEntry()
        {
            try
            {
                RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
                options.ExitEditorWhenDone = true;
                ApplyCommandLine(options);
                Run(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BatchInteractions()
        {
            try
            {
                RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
                options.OutputPath = Path.Combine(options.ProjectPath, "Build/RuntimePreviewInteractions");
                options.Widths = new[] { 2560, 1920, 1280, 1366 };
                options.Heights = new[] { 1440, 1080, 720, 768 };
                options.Cases = RuntimePreviewInteractionRunner.DefaultCases();
                options.ExitEditorWhenDone = true;
                ApplyCommandLine(options);
                RunInteractions(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RunInteractions(RuntimePreviewOptions options)
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Interaction Preview requires an Editor Game View; do not use -batchmode.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting Interaction Preview.");
            if (options == null)
                throw new ArgumentNullException("options");
            options.Validate();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close dirty scenes before Interaction Preview; no scene changes are discarded.");
            }
            SessionState.SetString(ConfigurationKey, JsonUtility.ToJson(options, false));
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(InteractionPendingKey, true);
            SessionState.SetInt(ExitCodeKey, -1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        public static string[] DefaultCases()
        {
            return new[]
            {
                "market-empty", "market", "detail", "index", "ranking", "inventory", "inventory-readonly", "assets",
                "market-modal", "market-toast", "detail-tooltip"
            };
        }

        private static void ApplyCommandLine(RuntimePreviewOptions options)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                string value = args[i + 1];
                switch (args[i])
                {
                    case "-runtimePreviewOutput": options.OutputPath = Path.GetFullPath(value); break;
                    case "-runtimePreviewGameRoot":
                        string gameRoot = Path.GetFullPath(value);
                        options.ManagedPath = Path.Combine(gameRoot, "Mortal_Data/Managed");
                        options.BepInExCorePath = Path.Combine(gameRoot, "BepInEx/core");
                        break;
                    case "-runtimePreviewDll": options.PluginAssemblyPath = Path.GetFullPath(value); break;
                    case "-runtimePreviewFixture": options.FixturePath = Path.GetFullPath(value); break;
                    case "-runtimePreviewCase": options.Cases = new[] { value }; break;
                    case "-runtimePreviewCases": options.Cases = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); break;
                    case "-runtimePreviewViewport":
                        string[] parts = value.Split('x', 'X');
                        if (parts.Length != 2)
                            throw new ArgumentException("Viewport must be WIDTHxHEIGHT: " + value);
                        options.Widths = new[] { int.Parse(parts[0], CultureInfo.InvariantCulture) };
                        options.Heights = new[] { int.Parse(parts[1], CultureInfo.InvariantCulture) };
                        break;
                }
            }
        }
    }

    [InitializeOnLoad]
    public static class RuntimePreviewBootstrap
    {
        static RuntimePreviewBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += CompletePendingExit;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;
            bool interactions = SessionState.GetBool(RuntimePreview.InteractionPendingKey, false);
            bool staticPreview = SessionState.GetBool(RuntimePreview.PendingKey, false);
            if (!interactions && !staticPreview)
                return;
            SessionState.SetBool(RuntimePreview.PendingKey, false);
            SessionState.SetBool(RuntimePreview.InteractionPendingKey, false);
            try
            {
                RuntimePreviewOptions options = JsonUtility.FromJson<RuntimePreviewOptions>(
                    SessionState.GetString(RuntimePreview.ConfigurationKey, string.Empty));
                GameObject host = new GameObject(interactions
                    ? "XingShang.RuntimePreview.InteractionRunner"
                    : "XingShang.RuntimePreview.Runner");
                if (interactions)
                    host.AddComponent<RuntimePreviewInteractionRunner>().Configure(options);
                else
                    host.AddComponent<RuntimePreviewRunner>().Configure(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetInt(RuntimePreview.ExitCodeKey, 1);
                EditorApplication.isPlaying = false;
            }
        }

        private static void CompletePendingExit()
        {
            int exitCode = SessionState.GetInt(RuntimePreview.ExitCodeKey, -1);
            if (exitCode < 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            SessionState.SetInt(RuntimePreview.ExitCodeKey, -1);
            Debug.Log("XingShang Runtime Preview exiting Editor with code " + exitCode + ".");
            EditorApplication.Exit(exitCode);
        }
    }

    public sealed class RuntimePreviewRunner : MonoBehaviour
    {
        private RuntimePreviewOptions _options;
        private readonly List<RuntimePreviewLogEntry> _errors = new List<RuntimePreviewLogEntry>();
        private Camera _camera;
        private GameObject _cameraObject;

        internal void Configure(RuntimePreviewOptions options)
        {
            _options = options;
        }

        private IEnumerator Start()
        {
            if (_options == null)
            {
                Finish(1);
                yield break;
            }
            Directory.CreateDirectory(_options.OutputPath);
            Application.logMessageReceived += OnLogMessage;
            RuntimePreviewRunReport report = new RuntimePreviewRunReport
            {
                status = "running",
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                gpu = SystemInfo.graphicsDeviceName,
                gpuVersion = SystemInfo.graphicsDeviceVersion,
                runtimeAssembly = _options.PluginAssemblyPath,
                runtimeAssemblySha256 = VisualValidation.Sha256File(_options.PluginAssemblyPath),
                bundle = _options.BundlePath,
                bundleSha256 = VisualValidation.Sha256File(_options.BundlePath),
                editorSourcesSha256 = EditorSourcesHash()
            };
            RuntimePreviewAdapter adapter = null;
            int exitCode = 0;
            Exception fatal = null;
            try
            {
                CreateCamera();
                adapter = new RuntimePreviewAdapter(_options);
                report.fixtureId = adapter.FixtureId;
                VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "fixture.json"), adapter.DescribeFixture());
                File.WriteAllText(Path.Combine(_options.OutputPath, "fixture.runstate.json"), adapter.RunStateJson);
            }
            catch (Exception exception)
            {
                fatal = Unwind(exception);
            }

            if (fatal == null)
            {
                IEnumerator matrix = CaptureMatrix(adapter, report);
                while (true)
                {
                    object yielded = null;
                    try
                    {
                        if (!matrix.MoveNext())
                            break;
                        yielded = matrix.Current;
                    }
                    catch (Exception exception)
                    {
                        fatal = Unwind(exception);
                        break;
                    }
                    yield return yielded;
                }
            }
            try
            {
                if (adapter != null)
                    adapter.Dispose();
            }
            catch (Exception exception)
            {
                fatal = fatal ?? Unwind(exception);
            }
            Application.logMessageReceived -= OnLogMessage;
            if (_camera != null)
                _camera.targetTexture = null;
            if (_cameraObject != null)
                Destroy(_cameraObject);
            if (fatal != null)
            {
                Debug.LogException(fatal);
                report.failures.Add(fatal.ToString());
                exitCode = 1;
            }

            report.errors.AddRange(_errors);
            for (int i = 0; i < report.cases.Count; i++)
            {
                if (report.cases[i].status != "passed")
                    exitCode = 1;
            }
            if (report.failures.Count > 0 || report.errors.Count > 0)
                exitCode = 1;
            report.status = exitCode == 0 ? "passed" : "failed";
            report.finishedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string reportPath = Path.Combine(_options.OutputPath, "report.json");
            VisualValidation.WriteJson(reportPath, report);
            if (exitCode == 0)
                Debug.Log("XingShang Runtime Preview passed: " + report.cases.Count + " native captures. " + reportPath);
            else
                Debug.LogError("XingShang Runtime Preview failed. " + reportPath);
            yield return null;
            Finish(exitCode);
        }

        private IEnumerator CaptureMatrix(RuntimePreviewAdapter adapter, RuntimePreviewRunReport report)
        {
            for (int viewport = 0; viewport < _options.Widths.Length; viewport++)
            {
                int width = _options.Widths[viewport];
                int height = _options.Heights[viewport];
                bool viewportReady = false;
                yield return RuntimePreviewGameView.SetNativeSize(width, height);
                for (int frame = 0; frame < 90; frame++)
                {
                    if (Screen.width == width && Screen.height == height)
                    {
                        viewportReady = true;
                        break;
                    }
                    yield return new WaitForEndOfFrame();
                }
                if (!viewportReady)
                {
                    string message = "Game View did not reach native size " + width + "x" + height +
                        "; actual=" + Screen.width + "x" + Screen.height + ".";
                    report.failures.Add(message);
                    for (int i = 0; i < _options.Cases.Length; i++)
                    {
                        RuntimePreviewCaseReport skipped = CreateCaseResult(adapter, _options.Cases[i], width, height);
                        skipped.status = "failed";
                        skipped.failures.Add(message);
                        report.cases.Add(skipped);
                    }
                    continue;
                }

                RenderTexture target = CreateRenderTexture(width, height);
                _camera.targetTexture = target;
                try
                {
                    for (int i = 0; i < _options.Cases.Length; i++)
                    {
                        RuntimePreviewCaseReport result = CreateCaseResult(adapter, _options.Cases[i], width, height);
                        yield return CaptureCase(adapter, result, width, height);
                        report.cases.Add(result);
                        yield return null;
                    }
                }
                finally
                {
                    _camera.targetTexture = null;
                    target.Release();
                    Destroy(target);
                }
            }
        }

        private RuntimePreviewCaseReport CreateCaseResult(
            RuntimePreviewAdapter adapter,
            string caseName,
            int width,
            int height)
        {
            return new RuntimePreviewCaseReport
            {
                caseName = caseName,
                width = width,
                height = height,
                status = "running",
                runStateSha256 = adapter.InitialStateHash,
                png = Path.Combine(_options.OutputPath, caseName + "-" + width + "x" + height + ".png"),
                semantics = Path.Combine(_options.OutputPath, caseName + "-" + width + "x" + height + ".json")
            };
        }

        private IEnumerator CaptureCase(
            RuntimePreviewAdapter adapter,
            RuntimePreviewCaseReport result,
            int width,
            int height)
        {
            int errorsBefore = _errors.Count;
            Exception failure = null;
            VisualSemanticSnapshot snapshot = null;
            string previous = null;
            int stableFrames = 0;
            bool reuse = result.caseName == "glyph-atlas-reuse";
            bool allPages = result.caseName == "glyph-atlas-all";
            bool checkAtlas = allPages || reuse || result.caseName == "glyph-atlas";
            if (reuse)
                yield return RuntimePreviewGameView.SetNativeSize(1920, 1080);
            try
            {
                adapter.OpenCase(checkAtlas ? "market-empty" : result.caseName);
                if (EventSystem.current != null)
                    EventSystem.current.enabled = false;
                PrepareCameraCanvases();
                PrewarmFonts();
            }
            catch (Exception exception)
            {
                failure = Unwind(exception);
            }

            if (reuse && failure == null)
            {
                yield return new WaitForEndOfFrame();
                try
                {
                    if (Screen.width != 1920 || adapter.WindowRoot.GetComponentInParent<Canvas>().rootCanvas.scaleFactor != 1f)
                        throw new InvalidOperationException("Card reuse case did not begin at ordinary display density.");
                    adapter.CloseFormalUi();
                    adapter.ReopenMarket();
                    RuntimePreviewMarketInteractionProbe.MarketButton(adapter, "NextPageButton").onClick.Invoke();
                }
                catch (Exception exception) { failure = Unwind(exception); }
                yield return RuntimePreviewGameView.SetNativeSize(width, height);
                PrepareCameraCanvases();
                yield return new WaitForEndOfFrame();
            }
            for (int frame = 0; failure == null && frame < 120; frame++)
            {
                bool waitingForTooltip = false;
                try
                {
                    PrewarmFonts();
                    Canvas.ForceUpdateCanvases();
                    if (result.caseName == "detail-tooltip" && !DetailTooltipReady(adapter))
                    {
                        waitingForTooltip = true;
                        stableFrames = 0;
                        previous = null;
                    }
                    else
                    {
                        snapshot = VisualValidation.CaptureSemantics(
                            adapter.FixtureId, result.caseName, width, height, adapter.InitialStateHash);
                        string current = VisualValidation.StabilityFingerprint(snapshot);
                        stableFrames = current == previous ? stableFrames + 1 : 1;
                        previous = current;
                    }
                }
                catch (Exception exception)
                {
                    failure = Unwind(exception);
                }
                if (waitingForTooltip)
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }
                if (failure != null || stableFrames >= 2)
                    break;
                yield return new WaitForEndOfFrame();
            }

            if (allPages && failure == null)
            {
                var visited = new HashSet<string>();
                for (int page = 0; page < 9 && failure == null; ++page)
                {
                    try
                    {
                        Canvas.ForceUpdateCanvases();
                        RuntimePreviewGlyphAtlas.Check(adapter, _camera, result, 1 + 6 * page,
                            path => WriteScreenshot(path, width, height), visited);
                        if (page < 8)
                        {
                            Button next = RuntimePreviewMarketInteractionProbe.MarketButton(adapter, "NextPageButton");
                            if (!next.IsInteractable()) throw new InvalidOperationException("Page " + (page + 2) + " was unreachable.");
                            next.onClick.Invoke();
                        }
                    }
                    catch (Exception exception) { failure = Unwind(exception); }
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                }
                if (visited.Count != 54) result.failures.Add("Glyph stress visited only " + visited.Count + " different items.");
            }
            if (failure == null)
            {
                try
                {
                    if (allPages)
                        snapshot = VisualValidation.CaptureSemantics(adapter.FixtureId, result.caseName, width, height, adapter.InitialStateHash);
                    result.stableFrames = stableFrames;
                    VisualValidation.WriteJson(result.semantics, snapshot);
                    if (stableFrames < 2)
                        result.failures.Add("Layout/semantics did not remain stable for two consecutive frames.");
                    result.failures.AddRange(VisualValidation.ValidateSemantics(snapshot));
                    result.failures.AddRange(adapter.ValidateFixtureSemantics(snapshot));
                    result.componentRects = snapshot.keyComponents;
                    if (checkAtlas && !allPages)
                        RuntimePreviewGlyphAtlas.Check(adapter, _camera, result, reuse ? 7 : 1, path => WriteScreenshot(path, width, height));
                    WriteScreenshot(result.png, width, height);
                    result.image = VisualValidation.ValidatePng(result.png, width, height);
                    result.pixelProbes = MarketAlignmentValidation.ReviewPng(result.png, snapshot);
                    result.failures.AddRange(MarketAlignmentValidation.ValidateMarketPixels(snapshot, result.pixelProbes));
                    if (result.caseName == "market" || result.caseName == "market-empty")
                    {
                        result.smallScreenLayout = MarketSmallScreenValidation.Capture(snapshot);
                        result.failures.AddRange(result.smallScreenLayout.failures);
                    }
                    if (!result.image.dimensionsValid)
                        result.failures.Add("PNG dimensions differ from the requested native viewport.");
                    if (!result.image.hasVisiblePixels)
                        result.failures.Add("Screenshot is fully transparent.");
                    if (!result.image.hasNonUniformContent)
                        result.failures.Add("Screenshot contains only one sampled color and is treated as blank.");
                    adapter.AssertBusinessStateUnchanged(result.caseName);
                }
                catch (Exception exception)
                {
                    failure = Unwind(exception);
                }
            }
            if (failure != null)
            {
                result.failures.Add(failure.ToString());
                Debug.LogException(failure);
            }
            for (int i = errorsBefore; i < _errors.Count; i++)
                result.failures.Add("Console " + _errors[i].type + ": " + _errors[i].condition);
            result.status = result.failures.Count == 0 ? "passed" : "failed";
        }

        private void CreateCamera()
        {
            _cameraObject = new GameObject("XingShang.RuntimePreview.Camera", typeof(Camera));
            _camera = _cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.027f, 0.035f, 0.031f, 1f);
            _camera.orthographic = true;
            _camera.cullingMask = -1;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void PrepareCameraCanvases()
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

        private static RenderTexture CreateRenderTexture(int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "RuntimePreview-" + width + "x" + height,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            target.Create();
            return target;
        }

        private void WriteScreenshot(string path, int width, int height)
        {
            if (_camera.targetTexture == null || _camera.targetTexture.width != width ||
                _camera.targetTexture.height != height)
                throw new InvalidOperationException("Capture RenderTexture does not match the native viewport.");
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = _camera.targetTexture;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply(false, false);
                byte[] png = ImageConversion.EncodeToPNG(image);
                if (png == null || png.Length == 0)
                    throw new InvalidDataException("PNG encoding returned no data.");
                File.WriteAllBytes(path, png);
                MarketPaintValidation.Capture(path, image, image.GetPixels32());
            }
            finally
            {
                RenderTexture.active = previous;
                Destroy(image);
            }
        }

        private static bool DetailTooltipReady(RuntimePreviewAdapter adapter)
        {
            RectTransform root = adapter.TooltipRoot;
            if (root == null || !root.gameObject.activeInHierarchy)
                return false;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            Transform titleTransform = root.Find("TitleText");
            Text title = titleTransform == null ? null : titleTransform.GetComponent<Text>();
            return group != null && group.alpha >= 0.999f &&
                title != null && title.gameObject.activeInHierarchy && title.text == "复位图表";
        }

        private static void PrewarmFonts()
        {
            Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text.font != null && !string.IsNullOrEmpty(text.text))
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            }
        }

        private string EditorSourcesHash()
        {
            string editor = Path.Combine(_options.ProjectPath, "Assets/Editor");
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(editor, "RuntimePreview*.cs", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(editor, "VisualValidation*.cs", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(editor, "*Market*Validation*.cs", SearchOption.TopDirectoryOnly));
            files.Sort(StringComparer.Ordinal);
            string combined = string.Empty;
            for (int i = 0; i < files.Count; i++)
                combined += Path.GetFileName(files[i]) + ":" + VisualValidation.Sha256File(files[i]) + "\n";
            return VisualValidation.Sha256Text(combined);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors.Add(new RuntimePreviewLogEntry
                {
                    type = type.ToString(), condition = condition, stackTrace = stackTrace
                });
            }
        }

        private void Finish(int exitCode)
        {
            if (_options != null && _options.ExitEditorWhenDone)
                SessionState.SetInt(RuntimePreview.ExitCodeKey, exitCode);
            EditorApplication.isPlaying = false;
        }

        private static Exception Unwind(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
                exception = exception.InnerException;
            return exception;
        }
    }

    public static class RuntimePreviewGameView
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static IEnumerator SetNativeSize(int width, int height)
        {
            SetSize(width, height);
            yield return null;
        }

        private static void SetSize(int width, int height)
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", All).GetValue(null, null);
            Type groupType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType", true);
            object standalone = Enum.Parse(groupType, "Standalone");
            object group = sizesType.GetMethod("GetGroup", All).Invoke(sizes, new[] { standalone });
            Type groupClass = group.GetType();
            int total = Convert.ToInt32(groupClass.GetMethod("GetTotalCount", All).Invoke(group, null), CultureInfo.InvariantCulture);
            MethodInfo getSize = groupClass.GetMethod("GetGameViewSize", All);
            int selected = -1;
            for (int i = 0; i < total; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                if (ReadInt(size, "width") == width && ReadInt(size, "height") == height)
                {
                    selected = i;
                    break;
                }
            }
            if (selected < 0)
            {
                Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize", true);
                Type sizeKind = editorAssembly.GetType("UnityEditor.GameViewSizeType", true);
                object fixedResolution = Enum.Parse(sizeKind, "FixedResolution");
                ConstructorInfo constructor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                if (constructor == null)
                    throw new MissingMethodException(sizeType.FullName, ".ctor(GameViewSizeType,int,int,string)");
                object size = constructor.Invoke(new[] { fixedResolution, width, height, "Runtime Preview " + width + "x" + height });
                groupClass.GetMethod("AddCustomSize", All).Invoke(group, new[] { size });
                selected = Convert.ToInt32(groupClass.GetMethod("GetTotalCount", All).Invoke(group, null), CultureInfo.InvariantCulture) - 1;
            }
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView", true);
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selectedProperty = gameViewType.GetProperty("selectedSizeIndex", All);
            if (selectedProperty == null)
                throw new MissingMemberException(gameViewType.FullName, "selectedSizeIndex");
            selectedProperty.SetValue(gameView, selected, null);
            gameView.Focus();
            gameView.Repaint();
        }

        private static int ReadInt(object target, string name)
        {
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, All);
            if (field != null)
                return Convert.ToInt32(field.GetValue(target), CultureInfo.InvariantCulture);
            PropertyInfo property = type.GetProperty(name, All);
            if (property != null)
                return Convert.ToInt32(property.GetValue(target, null), CultureInfo.InvariantCulture);
            throw new MissingMemberException(type.FullName, name);
        }
    }
}
