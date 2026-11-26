using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class RuntimePreviewInteractionReport
    {
        public int schemaVersion = 1;
        public string status;
        public string fixtureId;
        public string startedUtc;
        public string finishedUtc;
        public string lastEvent;
        public string lastEventUtc;
        public string unityVersion;
        public string gpu;
        public string gpuVersion;
        public string runtimeAssemblySha256;
        public string bundleSha256;
        public string editorSourcesSha256;
        public List<RuntimePreviewInteractionTestResult> tests = new List<RuntimePreviewInteractionTestResult>();
        public List<RuntimePreviewLogEntry> errors = new List<RuntimePreviewLogEntry>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class RuntimePreviewInteractionTestResult
    {
        public string name;
        public int width;
        public int height;
        public string status;
        public float durationMs;
        public string stateBeforeSha256;
        public string stateAfterSha256;
        public long cashBefore;
        public long cashAfter;
        public long expectedCashAfter;
        public bool expectStateChange;
        public List<RuntimePreviewInteractionSample> samples = new List<RuntimePreviewInteractionSample>();
        public List<RuntimePreviewPointerEvidence> pointerEvents = new List<RuntimePreviewPointerEvidence>();
        public List<RuntimePreviewWindowCallbackRecord> windowCallbacks = new List<RuntimePreviewWindowCallbackRecord>();
        public List<RuntimePreviewTooltipProbe> tooltipProbes = new List<RuntimePreviewTooltipProbe>();
        public List<RuntimePreviewButtonProbe> buttonProbes = new List<RuntimePreviewButtonProbe>();
        public List<RuntimePreviewFlightProbe> flightProbes = new List<RuntimePreviewFlightProbe>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class RuntimePreviewInteractionSample
    {
        public string label;
        public string captureMode;
        public float elapsedMs;
        public string note;
        public string png;
        public string semantics;
        public VisualImageValidation image;
        public List<RuntimePreviewComponentRectEvidence> componentRects = new List<RuntimePreviewComponentRectEvidence>();
        public List<RuntimePreviewPixelProbeEvidence> pixelProbes = new List<RuntimePreviewPixelProbeEvidence>();
        public bool active;
        public int activeObjectCount;
        public float alpha;
        public Vector2 position;
        public Vector3 rotation;
        public Vector3 scale;
        public Vector2 shadow;
        public float chartAlpha;
        public float goldScale;
        public List<RuntimePreviewTweenDiagnostic> tweens = new List<RuntimePreviewTweenDiagnostic>();
    }

    [Serializable]
    public sealed class RuntimePreviewTweenDiagnostic
    {
        public string source;
        public string type;
        public float duration;
        public float fullDuration;
        public float elapsed;
        public float position;
        public float fullPosition;
        public float timeScale;
        public float dotweenTimeScale;
        public float timeTimeScale;
        public float unscaledDeltaTime;
        public bool isPlaying;
        public bool isComplete;
    }

    [Serializable]
    public sealed class RuntimePreviewCompletionEvidence
    {
        public float duration;
        public float elapsed;
        public float timeScale;
        public int completionCount;
        public bool originalOnCompletePresent;
    }

    internal sealed class RuntimePreviewCompletionProbe
    {
        internal RuntimePreviewAdapter Owner;
        internal object Tween;
        internal bool Card;

        public void Complete()
        {
            if (Card)
                Owner.CompleteCardTween(Tween);
            else
                Owner.CompleteInventoryTween(Tween);
        }
    }

    [Serializable]
    public sealed class RuntimePreviewTokenDiagnostic
    {
        public float position;
        public float duration;
        public float alpha;
        public Vector3 scale;
        public Vector2 anchoredPosition;
        public float timeScale;
        public bool isPlaying;
    }

    [Serializable]
    public sealed class RuntimePreviewWindowCallbackRecord
    {
        public string eventName;
        public string classification;
        public float realtime;
        public float duration;
        public float elapsed;
        public float timeScale;
        public float dotweenTimeScale;
        public float timeTimeScale;
        public float unscaledDeltaTime;
        public bool isPlaying;
        public bool isComplete;
        public int generation;
        public int scopeTweenCount;
        public bool scopeRootActive;
        public bool marketRootActive;
        public int uiInstanceId;
        public int marketInstanceId;
        public int sequenceInstanceId;
        public bool uiDisposed;
        public bool marketDisposed;
        public int rootInstanceId;
        public bool rootDestroyed;
        public bool originalOnCompletePresent;
        public bool originalOnKillPresent;
        public bool completeBeforeKill;
        public string stackTrace;
    }

    internal sealed class RuntimePreviewWindowTweenProbe
    {
        internal RuntimePreviewAdapter Owner;
        internal object Tween;
        internal int SequenceInstanceId;
        internal bool Completed;
        internal bool OriginalOnCompletePresent;
        internal bool OriginalOnKillPresent;

        public void OnComplete()
        {
            Completed = true;
            Owner.RecordWindowCallback("OnComplete", Tween, this);
        }

        public void OnKill()
        {
            Owner.RecordWindowCallback("OnKill", Tween, this);
        }
    }

    internal sealed class RuntimePreviewInteractionContext
    {
        internal RuntimePreviewInteractionRunner Runner;
        internal RuntimePreviewAdapter Adapter;
        internal RuntimePreviewInteractionTestResult Result;
        internal int Width;
        internal int Height;
        internal GameObject PointerEnterHandler;
        internal GameObject PointerPress;

        internal float Now { get { return Time.realtimeSinceStartup; } }

        internal void Open(string caseName, bool reducedMotion)
        {
            Adapter.OpenCase(caseName, reducedMotion);
            BaseInputModule[] modules = UnityEngine.Object.FindObjectsOfType<BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
                modules[i].enabled = false;
            if (EventSystem.current != null)
            {
                EventSystem.current.enabled = true;
                EventSystem.current.SetSelectedGameObject(null);
            }
            PointerEnterHandler = null;
            PointerPress = null;
            Runner.PrepareCanvases();
            Canvas.ForceUpdateCanvases();
        }

        internal void Check(bool condition, string message)
        {
            if (!condition)
                Fail(message);
        }

        internal void Fail(string message)
        {
            if (!Result.failures.Contains(message))
                Result.failures.Add(message);
        }

        internal IEnumerator Wait(float seconds)
        {
            float start = Now;
            while (Now - start < seconds)
                yield return new WaitForEndOfFrame();
        }

        internal IEnumerator WaitUntil(float start, float milliseconds)
        {
            while ((Now - start) * 1000f < milliseconds)
                yield return new WaitForEndOfFrame();
        }

        internal float ElapsedMs(float start)
        {
            return (Now - start) * 1000f;
        }

        internal IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return new WaitForEndOfFrame();
        }

        internal RuntimePreviewInteractionSample Record(string label, float start, string note = "")
        {
            RuntimePreviewInteractionSample sample = new RuntimePreviewInteractionSample
            {
                label = label,
                captureMode = "state-only-no-png",
                elapsedMs = ElapsedMs(start),
                note = note
            };
            Result.samples.Add(sample);
            return sample;
        }

        internal RuntimePreviewInteractionSample Capture(
            string label,
            float start,
            string note = "",
            bool terminal = false,
            bool allowBlank = false)
        {
            return Runner.Capture(this, label, start, note, terminal, allowBlank);
        }
    }

    public sealed partial class RuntimePreviewInteractionRunner : MonoBehaviour
    {
        private RuntimePreviewOptions _options;
        private readonly List<RuntimePreviewLogEntry> _errors = new List<RuntimePreviewLogEntry>();
        private Camera _camera;
        private GameObject _cameraObject;
        private RenderTexture _target;
        private bool _fixtureWritten;

        internal static string[] DefaultCases()
        {
            return new[]
            {
                "pointer-market-click", "ticker-pointer", "card-pointer", "card-select",
                "inventory-hover", "inventory-focus", "tooltip-delay-cancel", "window-motion", "toast-stack",
                "trade-feedback-double-confirm", "reduced-motion",
                "ranking-continuous", "capacity-guard", "history-advance",
                "add-to-cart-flight", "market-tooltip-reuse", "trade-button-motion",
                "add-to-cart-flight-probe", "pixel-constant", "masthead-and-ledger",
                "chrome-focus-edges", "chart-controls", "signed-masthead-wallet"
            };
        }

        internal void Configure(RuntimePreviewOptions options)
        {
            _options = options;
        }

        private IEnumerator Start()
        {
            Debug.Log("XingShang Interaction timing before runner: runInBackground=" +
                Application.runInBackground + " focused=" + Application.isFocused +
                " editorPaused=" + EditorApplication.isPaused);
            Application.runInBackground = true;
            Directory.CreateDirectory(_options.OutputPath);
            Application.logMessageReceived += OnLogMessage;
            RuntimePreviewInteractionReport report = new RuntimePreviewInteractionReport
            {
                status = "running",
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                gpu = SystemInfo.graphicsDeviceName,
                gpuVersion = SystemInfo.graphicsDeviceVersion,
                runtimeAssemblySha256 = VisualValidation.Sha256File(_options.PluginAssemblyPath),
                bundleSha256 = VisualValidation.Sha256File(_options.BundlePath),
                editorSourcesSha256 = EditorSourcesHash()
            };
            WritePartialReport(report, "runner-created");
            Exception fatal = null;
            IEnumerator matrix = RunMatrix(report);
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
            Application.logMessageReceived -= OnLogMessage;
            if (_camera != null)
                _camera.targetTexture = null;
            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
            }
            if (_cameraObject != null)
                Destroy(_cameraObject);
            if (fatal != null)
            {
                report.failures.Add(fatal.ToString());
                Debug.LogException(fatal);
            }
            report.errors.AddRange(_errors);
            int exitCode = report.failures.Count == 0 && report.errors.Count == 0 ? 0 : 1;
            for (int i = 0; i < report.tests.Count; i++)
            {
                if (report.tests[i].status != "passed")
                    exitCode = 1;
            }
            report.status = exitCode == 0 ? "passed" : "failed";
            report.finishedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string reportPath = Path.Combine(_options.OutputPath, "report.json");
            WritePartialReport(report, "runner-finished");
            if (exitCode == 0)
                Debug.Log("XingShang Interaction Preview passed: " + report.tests.Count + " cases. " + reportPath);
            else
                Debug.LogError("XingShang Interaction Preview failed. " + reportPath);
            yield return null;
            Finish(exitCode);
        }

        private IEnumerator RunMatrix(RuntimePreviewInteractionReport report)
        {
            for (int viewport = 0; viewport < _options.Widths.Length; viewport++)
            {
                int width = _options.Widths[viewport];
                int height = _options.Heights[viewport];
                WritePartialReport(report, "viewport-begin " + width + "x" + height);
                yield return RuntimePreviewGameView.SetNativeSize(width, height);
                bool ready = false;
                for (int frame = 0; frame < 90; frame++)
                {
                    if (Screen.width == width && Screen.height == height)
                    {
                        ready = true;
                        break;
                    }
                    yield return new WaitForEndOfFrame();
                }
                if (!ready)
                    throw new InvalidOperationException("Interaction Game View did not reach native size " + width + "x" + height + ".");
                EnsureCamera();
                _target = CreateTarget(width, height);
                _camera.targetTexture = _target;
                try
                {
                    for (int i = 0; i < _options.Cases.Length; i++)
                        yield return RunOneTest(report, _options.Cases[i], width, height);
                }
                finally
                {
                    _camera.targetTexture = null;
                    _target.Release();
                    Destroy(_target);
                    _target = null;
                }
                WritePartialReport(report, "viewport-end " + width + "x" + height);
            }
        }

        private IEnumerator RunOneTest(
            RuntimePreviewInteractionReport report,
            string name,
            int width,
            int height)
        {
            RuntimePreviewInteractionTestResult result = new RuntimePreviewInteractionTestResult
            {
                name = name,
                width = width,
                height = height,
                status = "running",
                expectStateChange = name == "trade-feedback-double-confirm" || name == "history-advance"
            };
            report.tests.Add(result);
            WritePartialReport(report, "case-begin " + name + " " + width + "x" + height);
            int errorsBefore = _errors.Count;
            float started = Time.realtimeSinceStartup;
            RuntimePreviewAdapter adapter = null;
            Exception failure = null;
            try
            {
                adapter = new RuntimePreviewAdapter(_options);
                result.stateBeforeSha256 = adapter.CurrentRunStateHash;
                result.cashBefore = adapter.CurrentCash;
                report.fixtureId = adapter.FixtureId;
                if (!_fixtureWritten)
                {
                    VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "fixture.json"), adapter.DescribeFixture());
                    File.WriteAllText(Path.Combine(_options.OutputPath, "fixture.runstate.json"), adapter.RunStateJson);
                    _fixtureWritten = true;
                }
            }
            catch (Exception exception)
            {
                failure = Unwind(exception);
            }
            if (failure == null)
            {
                RuntimePreviewInteractionContext context = new RuntimePreviewInteractionContext
                {
                    Runner = this,
                    Adapter = adapter,
                    Result = result,
                    Width = width,
                    Height = height
                };
                IEnumerator test = Dispatch(context);
                while (true)
                {
                    object yielded = null;
                    try
                    {
                        if (!test.MoveNext())
                            break;
                        yielded = test.Current;
                    }
                    catch (Exception exception)
                    {
                        failure = Unwind(exception);
                        break;
                    }
                    yield return yielded;
                }
            }
            if (adapter != null)
            {
                try
                {
                    result.stateAfterSha256 = adapter.CurrentRunStateHash;
                    result.cashAfter = adapter.CurrentCash;
                    if (!result.expectStateChange)
                        adapter.AssertBusinessStateUnchanged(name);
                }
                catch (Exception exception)
                {
                    failure = failure ?? Unwind(exception);
                }
                try
                {
                    adapter.Dispose();
                }
                catch (Exception exception)
                {
                    failure = failure ?? Unwind(exception);
                }
                if (name == "window-motion")
                    result.windowCallbacks.AddRange(adapter.WindowCallbacks);
            }
            if (failure != null)
            {
                AddFailure(result, failure.ToString());
                Debug.LogException(failure);
            }
            for (int i = errorsBefore; i < _errors.Count; i++)
                AddFailure(result, "Console " + _errors[i].type + ": " + _errors[i].condition);
            result.durationMs = (Time.realtimeSinceStartup - started) * 1000f;
            result.status = result.failures.Count == 0 ? "passed" : "failed";
            WritePartialReport(report, "case-end " + name + " " + result.status + " " +
                result.durationMs.ToString("0.0", CultureInfo.InvariantCulture) + "ms");
            yield return null;
        }

        private IEnumerator Dispatch(RuntimePreviewInteractionContext context)
        {
            switch (context.Result.name)
            {
                case "pointer-market-click": return MarketPointerClick(context);
                case "ticker-pointer": return TickerPointer(context);
                case "market-ink-review": return MarketInkReview(context);
                case "market-ledger-edges": return MarketLedgerEdges(context);
                case "card-pointer": return CardPointer(context);
                case "card-select": return CardSelect(context);
                case "inventory-hover": return InventoryHover(context);
                case "loan-proof": return LoanProof(context);
                case "asset-profit-proof": return AssetProfitProof(context);
                case "asset-sign-ink": return AssetSignInk(context);
                case "inventory-readonly-inspect": return InventoryReadonlyInspect(context);
                case "inventory-focus": return InventoryFocus(context);
                case "tooltip-delay-cancel": return TooltipDelayCancel(context);
                case "window-motion": return WindowMotion(context);
                case "toast-stack": return ToastStack(context);
                case "trade-feedback-double-confirm": return TradeFeedbackDoubleConfirm(context);
                case "reduced-motion": return ReducedMotion(context);
                case "ranking-continuous": return RankingContinuous(context);
                case "capacity-guard": return CapacityGuard(context);
                case "history-advance": return HistoryAdvance(context);
                case "add-to-cart-flight": return AddToCartFlight(context);
                case "market-tooltip-reuse": return MarketTooltipReuse(context);
                case "trade-button-motion": return TradeButtonMotion(context);
                case "add-to-cart-flight-probe": return AddToCartFlightProbe(context);
                case "pixel-constant": return PixelConstant(context);
                case "signed-masthead-wallet": return SignedMastheadWallet(context);
                case "chrome-focus-edges": return ChromeFocusAndEdges(context);
                case "chart-controls": return ChartControls(context);
                case "masthead-and-ledger": return MastheadAndLedger(context);
                default: throw new ArgumentOutOfRangeException("caseName", context.Result.name);
            }
        }

        internal void WriteSemanticSnapshot(
            RuntimePreviewInteractionContext context,
            string label,
            VisualSemanticSnapshot snapshot)
        {
            VisualValidation.WriteJson(
                Path.Combine(_options.OutputPath, context.Result.name + "-" + label + "-" +
                    context.Width + "x" + context.Height + ".json"),
                snapshot);
        }

        internal void LogStep(RuntimePreviewInteractionContext context, string step)
        {
            Debug.Log("XingShang Interaction step " + context.Result.name + " " +
                context.Width + "x" + context.Height + " " + step);
        }

        internal RuntimePreviewInteractionSample Capture(
            RuntimePreviewInteractionContext context,
            string label,
            float start,
            string note,
            bool terminal,
            bool allowBlank)
        {
            RuntimePreviewInteractionSample sample = new RuntimePreviewInteractionSample
            {
                label = label,
                captureMode = "native-png+semantics",
                elapsedMs = context.ElapsedMs(start),
                note = note,
                png = Path.Combine(_options.OutputPath, context.Result.name + "-" + Sanitize(label) + "-" +
                    context.Width + "x" + context.Height + ".png"),
                semantics = Path.Combine(_options.OutputPath, context.Result.name + "-" + Sanitize(label) + "-" +
                    context.Width + "x" + context.Height + ".json")
            };
            context.Result.samples.Add(sample);
            try
            {
                PrepareCanvases();
                Canvas.ForceUpdateCanvases();
                string viewCase = context.Result.name;
                if (viewCase == "inventory-readonly-inspect")
                    viewCase = label == "held-quote" ? "readonly-held-quote" : "inventory-readonly";
                VisualSemanticSnapshot semantic = VisualValidation.CaptureSemantics(
                    context.Adapter.FixtureId,
                    viewCase,
                    context.Width,
                    context.Height,
                    context.Adapter.CurrentRunStateHash);
                sample.componentRects = semantic.keyComponents;
                VisualValidation.WriteJson(sample.semantics, semantic);
                if (terminal && !allowBlank)
                {
                    List<string> failures = VisualValidation.ValidateSemantics(semantic);
                    for (int i = 0; i < failures.Count; i++)
                        context.Fail(failures[i]);
                }
                WriteScreenshot(sample.png, context.Width, context.Height);
                sample.image = VisualValidation.ValidatePng(sample.png, context.Width, context.Height);
                sample.pixelProbes = MarketAlignmentValidation.ReviewPng(sample.png, semantic);
                if (terminal && !allowBlank)
                {
                    List<string> pixelFailures = MarketAlignmentValidation.ValidateMarketPixels(semantic, sample.pixelProbes);
                    for (int i = 0; i < pixelFailures.Count; i++)
                        context.Fail(pixelFailures[i]);
                }
                context.Check(sample.image.dimensionsValid, "Interaction PNG dimensions mismatch: " + sample.png);
                context.Check(sample.image.hasVisiblePixels, "Interaction PNG is fully transparent: " + sample.png);
                if (!allowBlank)
                    context.Check(sample.image.hasNonUniformContent, "Interaction PNG is blank/uniform: " + sample.png);
            }
            catch (Exception exception)
            {
                context.Fail(Unwind(exception).ToString());
            }
            return sample;
        }

        private void WritePartialReport(RuntimePreviewInteractionReport report, string eventName)
        {
            report.lastEvent = eventName;
            report.lastEventUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            report.errors = new List<RuntimePreviewLogEntry>(_errors);
            VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "report.json"), report);
            Debug.Log("XingShang Interaction " + eventName);
        }

        private void EnsureCamera()
        {
            if (_camera != null)
                return;
            _cameraObject = new GameObject("XingShang.RuntimePreview.InteractionCamera", typeof(Camera));
            _camera = _cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.027f, 0.035f, 0.031f, 1f);
            _camera.orthographic = true;
            _camera.cullingMask = -1;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        internal void PrepareCanvases()
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
        }

        private static RenderTexture CreateTarget(int width, int height)
        {
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "InteractionPreview-" + width + "x" + height,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            target.Create();
            return target;
        }

        private void WriteScreenshot(string path, int width, int height)
        {
            if (_target == null || _target.width != width || _target.height != height)
                throw new InvalidOperationException("Interaction capture target does not match the native viewport.");
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = _target;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply(false, false);
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(image));
            }
            finally
            {
                RenderTexture.active = previous;
                Destroy(image);
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

        private static string Sanitize(string value)
        {
            return string.IsNullOrEmpty(value) ? "sample" : value.Replace(' ', '-').Replace('/', '-');
        }

        private static void AddFailure(RuntimePreviewInteractionTestResult result, string message)
        {
            if (!result.failures.Contains(message))
                result.failures.Add(message);
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
            if (_options.ExitEditorWhenDone)
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
}
