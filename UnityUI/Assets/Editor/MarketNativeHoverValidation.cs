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
    public sealed class MarketNativeHoverReport
    {
        public int schemaVersion = 1;
        public string status;
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public string runtimeAssemblySha256;
        public string bundleSha256;
        public string method = "Real screen-coordinate GraphicRaycaster hits; one persistent PointerEventData per viewport; stock BaseInputModule.HandlePointerExitAndEnter; formal item providers.";
        public List<MarketNativeHoverViewportReport> viewports = new List<MarketNativeHoverViewportReport>();
        public List<RuntimePreviewLogEntry> errors = new List<RuntimePreviewLogEntry>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class MarketNativeHoverViewportReport
    {
        public int width;
        public int height;
        public float expectedContentScale;
        public string status;
        public List<MarketNativeHoverRowReport> rows = new List<MarketNativeHoverRowReport>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class MarketNativeHoverCardReport
    {
        public string path;
        public bool expectedHighlight;
        public bool actualHighlight;
    }

    [Serializable]
    public sealed class MarketNativeHoverRowReport
    {
        public string label;
        public string expectedOwner;
        public string expectedItemId;
        public string actualItemId;
        public string layoutMode = "normal-six-card";
        public bool diagnosticLayoutRestored;
        public Vector2 cardScreenTopLeftMin;
        public Vector2 cardScreenTopLeftMax;
        public string pendingImmediately;
        public string ownerImmediately;
        public string ownerAfterDelay;
        public string raycastHit;
        public string previousPointerEnter;
        public string pointerEnter;
        public int pointerDataId;
        public int nativeMoveSerial;
        public bool nativeDispatchInvoked;
        public List<string> stockEventOrder = new List<string>();
        public List<MarketNativeHoverCardReport> cards = new List<MarketNativeHoverCardReport>();
        public bool tipActive;
        public float groupAlpha;
        public string expectedTitle;
        public string expectedBody;
        public string expectedHint;
        public string title;
        public string body;
        public string hint;
        public int bodyLineCount;
        public float generatedLinePitchPhysical;
        public string lastGeneratedLine;
        public bool lastLineReadable;
        public bool hintInsideRoot;
        public string bodyVerticalOverflow;
        public float bodyContentHeight;
        public float bodyViewportHeight;
        public bool overflowScrollPresent;
        public bool scrollReachedEnd;
        public float expectedContentScale;
        public float rootScreenWidth;
        public float rootScreenHeight;
        public float titlePhysicalFontSize;
        public float bodyPhysicalFontSize;
        public float hintPhysicalFontSize;
        public float horizontalPaddingPhysical;
        public float verticalPaddingPhysical;
        public float layoutSpacingPhysical;
        public float bodyLineSpacing;
        public Vector2 shadowOffsetPhysical;
        public float clampMarginPhysical;
        public Vector2 screenTopLeftMin;
        public Vector2 screenTopLeftMax;
        public RuntimePreviewPointerEvidence pointer;
        public string image;
        public VisualImageValidation imageValidation;
        public List<string> failures = new List<string>();
    }

    public static class MarketNativeHoverValidation
    {
        internal const string PendingKey = "XingShang.RuntimePreview.NativeMarketHover.Pending";

        [MenuItem("XingShang/Runtime Preview/Run Native Market Hover Validation")]
        public static void RunMenu()
        {
            RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
            options.OutputPath = Path.Combine(options.ProjectPath, "Build/MarketNativeHover");
            Run(options);
        }

        public static void BatchEntry()
        {
            try
            {
                RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
                options.OutputPath = Path.Combine(options.ProjectPath, "Build/MarketNativeHover");
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

        private static void Run(RuntimePreviewOptions options)
        {
            if (Application.isBatchMode)
                throw new InvalidOperationException("Native market hover validation requires an Editor Game View; do not use -batchmode.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting native market hover validation.");
            options.Cases = new[] { "market-native-hover" };
            if (options.Widths == null || options.Widths.Length == 0)
            {
                options.Widths = new[] { 2560, 1920, 1280, 1366 };
                options.Heights = new[] { 1440, 1080, 720, 768 };
            }
            options.Validate();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close dirty scenes before native market hover validation.");
            }
            SessionState.SetString(RuntimePreview.ConfigurationKey, JsonUtility.ToJson(options, false));
            SessionState.SetBool(RuntimePreview.PendingKey, false);
            SessionState.SetBool(RuntimePreview.InteractionPendingKey, false);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(RuntimePreview.ExitCodeKey, -1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
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
                    case "-runtimePreviewDll": options.PluginAssemblyPath = Path.GetFullPath(value); break;
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
    public static class MarketNativeHoverBootstrap
    {
        static MarketNativeHoverBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !SessionState.GetBool(MarketNativeHoverValidation.PendingKey, false))
                return;
            SessionState.SetBool(MarketNativeHoverValidation.PendingKey, false);
            try
            {
                RuntimePreviewOptions options = JsonUtility.FromJson<RuntimePreviewOptions>(
                    SessionState.GetString(RuntimePreview.ConfigurationKey, string.Empty));
                new GameObject("XingShang.RuntimePreview.NativeMarketHover")
                    .AddComponent<MarketNativeHoverRunner>().Configure(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetInt(RuntimePreview.ExitCodeKey, 1);
                EditorApplication.isPlaying = false;
            }
        }
    }

    public sealed class MarketNativeHoverRunner : MonoBehaviour
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private RuntimePreviewOptions _options;
        private readonly List<RuntimePreviewLogEntry> _errors = new List<RuntimePreviewLogEntry>();
        private MarketNativeHoverViewportReport _viewport;
        private int _pointerDataId;
        private int _lastNativeSerial;
        private string _previousPointerEnter;

        internal void Configure(RuntimePreviewOptions options)
        {
            _options = options;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_options.OutputPath);
            Application.logMessageReceived += OnLogMessage;
            MarketNativeHoverReport report = new MarketNativeHoverReport
            {
                status = "running",
                startedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                runtimeAssemblySha256 = VisualValidation.Sha256File(_options.PluginAssemblyPath),
                bundleSha256 = VisualValidation.Sha256File(_options.BundlePath)
            };
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
            report.errors.AddRange(_errors);
            if (fatal != null)
                report.failures.Add(fatal.ToString());
            int exitCode = report.failures.Count == 0 && report.errors.Count == 0 ? 0 : 1;
            for (int i = 0; i < report.viewports.Count; i++)
            {
                if (report.viewports[i].status != "passed")
                    exitCode = 1;
            }
            report.status = exitCode == 0 ? "passed" : "failed";
            report.finishedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "report.json"), report);
            Debug.Log("XingShang native market hover validation " + report.status + ": " +
                Path.Combine(_options.OutputPath, "report.json"));
            yield return null;
            Finish(exitCode);
        }

        private IEnumerator RunMatrix(MarketNativeHoverReport report)
        {
            for (int index = 0; index < _options.Widths.Length; index++)
            {
                int width = _options.Widths[index];
                int height = _options.Heights[index];
                _viewport = new MarketNativeHoverViewportReport
                {
                    width = width,
                    height = height,
                    expectedContentScale = ExpectedScale(width, height),
                    status = "running"
                };
                report.viewports.Add(_viewport);
                _pointerDataId = 0;
                _lastNativeSerial = 0;
                _previousPointerEnter = null;
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
                    throw new InvalidOperationException("Native hover Game View did not reach " + width + "x" + height + ".");

                RuntimePreviewAdapter adapter = new RuntimePreviewAdapter(_options);
                RuntimePreviewInteractionContext context = new RuntimePreviewInteractionContext
                {
                    Adapter = adapter,
                    Width = width,
                    Height = height,
                    Result = new RuntimePreviewInteractionTestResult { name = "market-native-hover" }
                };
                try
                {
                    adapter.OpenCase("market-empty", false);
                    BaseInputModule[] modules = UnityEngine.Object.FindObjectsOfType<BaseInputModule>();
                    for (int i = 0; i < modules.Length; i++)
                        modules[i].enabled = false;
                    EventSystem.current.enabled = true;
                    EventSystem.current.SetSelectedGameObject(null);
                    Canvas.ForceUpdateCanvases();
                    yield return context.Wait(0.6f);
                    AttachEventRecorders(context);
                    IEnumerator routes = RunNativeRoutes(context);
                    while (true)
                    {
                        object yielded = null;
                        try
                        {
                            if (!routes.MoveNext())
                                break;
                            yielded = routes.Current;
                        }
                        catch (Exception exception)
                        {
                            string failure = Unwind(exception).ToString();
                            _viewport.failures.Add(failure);
                            context.Fail(failure);
                            break;
                        }
                        yield return yielded;
                    }
                }
                finally
                {
                    adapter.Dispose();
                }
                if (context != null)
                    _viewport.failures.AddRange(context.Result.failures);
                _viewport.status = _viewport.failures.Count == 0 ? "passed" : "failed";
                VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "report.json"), report);
                yield return null;
            }
        }

        private IEnumerator RunNativeRoutes(RuntimePreviewInteractionContext context)
        {
            GameObject cardA = context.Adapter.CardRoot(0);
            Button buyA = context.Adapter.CardActionButton(0, "Buy");
            Button buyB = context.Adapter.CardActionButton(1, "Buy");
            Button borrowA = context.Adapter.CardActionButton(0, "Borrow");
            RuntimePreviewItemBinding item01 = RuntimePreviewMarketInteractionProbe.Item(context.Adapter, 0);

            yield return MoveAndProbe(context, "outside", cardA, null, null, null, false);
            yield return MoveAndProbe(context, "outside-direct-buy", buyA.gameObject, buyA.transform, null, null, false);
            yield return MoveAndProbe(context, "buy-to-same-card-title", cardA, cardA.transform,
                RuntimePreviewPointerDriver.Center(RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "NameText").gameObject), item01, true);
            yield return MoveAndProbe(context, "same-card-title-to-outside", cardA, null, null, null, false);
            yield return MoveAndProbe(context, "outside-to-card-title", cardA, cardA.transform,
                RuntimePreviewPointerDriver.Center(RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "NameText").gameObject), item01, false);
            yield return MoveAndProbe(context, "title-to-buy", buyA.gameObject, buyA.transform, null, null, false);
            yield return MoveAndProbe(context, "buy-to-same-card-title-again", cardA, cardA.transform,
                RuntimePreviewPointerDriver.Center(RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "NameText").gameObject), item01, false);
            yield return MoveAndProbe(context, "title-to-borrow", borrowA.gameObject, borrowA.transform, null, null, false);
            yield return MoveAndProbe(context, "borrow-to-outside", cardA, null, null, null, false);
            yield return MoveAndProbe(context, "outside-direct-borrow", borrowA.gameObject, borrowA.transform, null, null, false);
            yield return MoveAndProbe(context, "direct-borrow-to-outside", cardA, null, null, null, false);
            yield return MoveAndProbe(context, "outside-direct-buy-card-a", buyA.gameObject, buyA.transform, null, null, false);
            yield return MoveAndProbe(context, "card-a-buy-to-card-b-buy", buyB.gameObject, buyB.transform, null, null, false);

            int cartBefore = context.Adapter.CartEntryCount;
            RuntimePreviewPointerDriver.Click(context, buyB.gameObject);
            SyncAfterExternalMove(context);
            context.Check(context.Adapter.CartEntryCount == cartBefore + 1,
                "Clicked card-B buy did not enter the cart exactly once.");
            context.Check(string.IsNullOrEmpty(context.Adapter.DetailItemId),
                "Clicked card-B buy bubbled into item detail.");
            yield return MoveAndProbe(context, "clicked-buy-to-outside", buyB.gameObject, null, null, null, false);
            context.Check(EventSystem.current.currentSelectedGameObject == buyB.gameObject,
                "Pointer exit hid the tooltip by clearing keyboard selection instead of preserving focus.");

            Button next = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "NextPageButton");
            context.Check(next != null && next.IsInteractable(), "Formal next-page button is unavailable.");
            RuntimePreviewPointerDriver.Click(context, next.gameObject);
            SyncAfterExternalMove(context);
            yield return context.Wait(0.25f);
            RuntimePreviewItemBinding item07 = RuntimePreviewMarketInteractionProbe.Item(context.Adapter, 0);
            context.Check(item07.ItemId == "07", "Paging did not bind the first reusable card to item 07: " + item07.ItemId);
            yield return MoveAndProbe(context, "page-two-outside-to-card-title", cardA, cardA.transform,
                RuntimePreviewPointerDriver.Center(RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "NameText").gameObject), item07, false);
            yield return MoveAndProbe(context, "page-two-title-to-buy", buyA.gameObject, buyA.transform, null, null, false);
            yield return MoveAndProbe(context, "page-two-buy-to-reused-card-title", cardA, cardA.transform,
                RuntimePreviewPointerDriver.Center(RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "NameText").gameObject), item07, false);

            GameObject lastCard = context.Adapter.CardRoot(5);
            RuntimePreviewItemBinding lastItem = RuntimePreviewMarketInteractionProbe.Item(context.Adapter, 5);
            yield return MoveAndProbe(context, "page-two-item12-natural-bottom-card-supplemental", lastCard, lastCard.transform,
                BottomBodyPoint(lastCard), lastItem, false, "12", "normal-six-card-supplemental");

            Rect item01NaturalBounds = CardBounds(cardA, context.Height);
            Rect item12NaturalBounds = CardBounds(lastCard, context.Height);
            MarketNativeHoverRowReport longText;
            using (new DiagnosticCardLayoutSwap(cardA, lastCard))
            {
                Canvas.ForceUpdateCanvases();
                SyncAfterExternalMove(context);
                yield return MoveAndProbe(context, "page-two-long-text-bottom-card", cardA, cardA.transform,
                    BottomBodyPoint(cardA), item07, false, "07", "diagnostic-layout-only-swap-item07-item12");
                longText = _viewport.rows[_viewport.rows.Count - 1];
                Check(context, longText, RectClose(CardBounds(cardA, context.Height), item12NaturalBounds),
                    "The layout-only diagnostic did not place the real item 07 card at the natural bottom-right slot.");
            }
            Canvas.ForceUpdateCanvases();
            longText.diagnosticLayoutRestored = RectClose(CardBounds(cardA, context.Height), item01NaturalBounds) &&
                RectClose(CardBounds(lastCard, context.Height), item12NaturalBounds);
            Check(context, longText, longText.diagnosticLayoutRestored,
                "The six-card layout was not restored after the item 07 diagnostic placement.");
            SyncAfterExternalMove(context);
            Button rightEdge = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "CartClearButton");
            yield return MoveAndProbe(context, "right-edge-native-tooltip", rightEdge.gameObject, rightEdge.transform, null, null, false);
            yield return MoveAndProbe(context, "right-edge-to-outside", rightEdge.gameObject, null, null, null, false);

            EventSystem.current.SetSelectedGameObject(cardA);
            yield return context.Wait(0.38f);
            MarketNativeHoverRowReport focus = Capture(context, "keyboard-focus-retained", cardA.transform, item07);
            _viewport.rows.Add(focus);
            Check(context, focus, focus.tipActive && focus.ownerAfterDelay == PathOf(cardA.transform),
                "Keyboard focus no longer shows the current item tooltip.");
            EventSystem.current.SetSelectedGameObject(null);
            yield return context.Wait(0.08f);
            Check(context, focus, !context.Adapter.TooltipRoot.gameObject.activeSelf,
                "Keyboard deselect did not hide the focused tooltip.");
        }

        private IEnumerator MoveAndProbe(
            RuntimePreviewInteractionContext context,
            string label,
            GameObject hitTarget,
            Component expectedOwner,
            Vector2? point,
            RuntimePreviewItemBinding expectedItem,
            bool checkReferenceSize,
            string requiredItemId = null,
            string layoutMode = null)
        {
            MarketNativeHoverRowReport row = new MarketNativeHoverRowReport
            {
                label = label,
                expectedOwner = expectedOwner == null ? string.Empty : PathOf(expectedOwner),
                expectedItemId = requiredItemId ?? (expectedItem == null ? string.Empty : expectedItem.ItemId),
                layoutMode = layoutMode ?? "normal-six-card",
                expectedContentScale = _viewport.expectedContentScale
            };
            _viewport.rows.Add(row);
            Vector2 actualPoint = point.HasValue ? point.Value : expectedOwner == null
                ? new Vector2(1f, 1f)
                : RuntimePreviewPointerDriver.Center(hitTarget);
            MarketNativeHoverEventRecorder.Active = row.stockEventOrder;
            try
            {
                row.pointer = RuntimePreviewPointerDriver.MoveAt(
                    context, hitTarget, actualPoint, expectedOwner != null);
            }
            finally
            {
                MarketNativeHoverEventRecorder.Active = null;
            }
            row.raycastHit = row.pointer.raycastHit;
            row.previousPointerEnter = row.pointer.previousPointerEnter;
            row.pointerEnter = row.pointer.pointerEnter;
            row.pointerDataId = row.pointer.pointerDataId;
            row.nativeMoveSerial = row.pointer.nativeMoveSerial;
            row.nativeDispatchInvoked = row.pointer.nativeDispatchInvoked;
            row.pendingImmediately = PathOf(TooltipOwner(context.Adapter, true));
            row.ownerImmediately = PathOf(TooltipOwner(context.Adapter, false));
            CaptureCards(context, row);
            CheckNativeRouteShape(context, row);
            Check(context, row, row.nativeDispatchInvoked, "Stock native enter/exit dispatch was not invoked.");
            if (_pointerDataId == 0)
                _pointerDataId = row.pointerDataId;
            else
                Check(context, row, _pointerDataId == row.pointerDataId, "PointerEventData instance changed between native moves.");
            if (_lastNativeSerial > 0)
                Check(context, row, row.nativeMoveSerial > _lastNativeSerial, "Native dispatch serial did not advance.");
            if (!string.IsNullOrEmpty(_previousPointerEnter))
                Check(context, row, row.previousPointerEnter == _previousPointerEnter,
                    "The previous native hit was cleared or changed between physical moves.");
            _lastNativeSerial = row.nativeMoveSerial;
            _previousPointerEnter = row.pointerEnter;
            Check(context, row, string.IsNullOrEmpty(row.expectedOwner)
                ? string.IsNullOrEmpty(row.pendingImmediately) && string.IsNullOrEmpty(row.ownerImmediately)
                : row.pendingImmediately == row.expectedOwner || row.ownerImmediately == row.expectedOwner,
                "Immediate tooltip owner does not match the deepest raycast trigger.");
            for (int i = 0; i < row.cards.Count; i++)
            {
                Check(context, row, row.cards[i].expectedHighlight == row.cards[i].actualHighlight,
                    "Parent card highlight and native hit disagree: " + row.cards[i].path);
            }

            yield return context.Wait(0.38f);
            Capture(context, row, expectedOwner, expectedItem);
            if (checkReferenceSize && expectedItem != null && row.tipActive)
            {
                float scale = _viewport.expectedContentScale;
                float expectedHeight = scale > 1f ? 274f : 182.5f;
                Check(context, row, Mathf.Abs(row.rootScreenWidth - 300f * scale) <= 1.5f,
                    "Tooltip physical width does not follow market content scale: " + row.rootScreenWidth);
                Check(context, row, Mathf.Abs(row.rootScreenHeight - expectedHeight) <= 30f,
                    "Tooltip wrapped height differs from the same-item reference: " + row.rootScreenHeight);
                Check(context, row, Mathf.Abs(row.titlePhysicalFontSize - 13f * scale) <= 0.75f &&
                    Mathf.Abs(row.bodyPhysicalFontSize - 11f * scale) <= 0.75f &&
                    Mathf.Abs(row.hintPhysicalFontSize - 8f * scale) <= 0.75f,
                    "Tooltip fonts did not scale with the paper and padding.");
                Check(context, row, Mathf.Abs(row.horizontalPaddingPhysical - 15f * scale) <= 0.75f &&
                    Mathf.Abs(row.verticalPaddingPhysical - 13f * scale) <= 0.75f &&
                    Mathf.Abs(row.layoutSpacingPhysical - 5f * scale) <= 0.75f,
                    "Tooltip padding or line-group spacing did not scale.");
                float expectedLinePitch = 11f * 1.75f * scale;
                Check(context, row, Mathf.Abs(row.generatedLinePitchPhysical - expectedLinePitch) <= 1.5f,
                    "Generated body line pitch does not match the reference physical line height: " +
                    row.generatedLinePitchPhysical);
                Check(context, row, Vector2.Distance(row.shadowOffsetPhysical,
                    new Vector2(5f * scale, 6f * scale)) <= 1f,
                    "Tooltip shadow offset did not scale with the paper.");
            }
            if (checkReferenceSize || label == "page-two-long-text-bottom-card" ||
                label == "page-two-item12-natural-bottom-card-supplemental")
                yield return CaptureNativeFrame(context, row, label);
        }

        private IEnumerator CaptureNativeFrame(
            RuntimePreviewInteractionContext context,
            MarketNativeHoverRowReport row,
            string label)
        {
            string path = Path.Combine(_options.OutputPath, label + "-" + context.Width + "x" + context.Height + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            row.image = path;
            row.imageValidation = VisualValidation.ValidatePng(path, context.Width, context.Height);
            Check(context, row, row.imageValidation.dimensionsValid && row.imageValidation.hasVisiblePixels &&
                row.imageValidation.hasNonUniformContent,
                "Native hover PNG is missing, has the wrong dimensions or is blank: " + path);
        }

        private void CheckNativeRouteShape(
            RuntimePreviewInteractionContext context,
            MarketNativeHoverRowReport row)
        {
            switch (row.label)
            {
                case "outside-direct-buy":
                case "outside-direct-buy-card-a":
                    RequireEventOrder(context, row, "enter:BuyButton", "enter:GoodsCard00");
                    break;
                case "outside-direct-borrow":
                    RequireEventOrder(context, row, "enter:BorrowButton", "enter:GoodsCard00");
                    break;
                case "buy-to-same-card-title":
                case "buy-to-same-card-title-again":
                case "page-two-buy-to-reused-card-title":
                    Check(context, row, row.stockEventOrder.Contains("exit:BuyButton") &&
                        !row.stockEventOrder.Contains("exit:GoodsCard00") &&
                        !row.stockEventOrder.Contains("enter:GoodsCard00"),
                        "Returning to the same card must not synthesize a parent exit/enter; the child exit must restore the card owner.");
                    break;
                case "card-a-buy-to-card-b-buy":
                    RequireEventOrder(context, row,
                        "exit:BuyButton", "exit:GoodsCard00", "enter:BuyButton", "enter:GoodsCard01");
                    break;
                case "clicked-buy-to-outside":
                    RequireEventOrder(context, row, "exit:BuyButton", "exit:GoodsCard01");
                    break;
            }
        }

        private void RequireEventOrder(
            RuntimePreviewInteractionContext context,
            MarketNativeHoverRowReport row,
            params string[] expected)
        {
            int cursor = 0;
            for (int i = 0; i < row.stockEventOrder.Count && cursor < expected.Length; i++)
            {
                if (row.stockEventOrder[i] == expected[cursor])
                    cursor++;
            }
            Check(context, row, cursor == expected.Length,
                "Stock event order mismatch; expected sequence " + string.Join(" -> ", expected) +
                "; actual " + string.Join(" -> ", row.stockEventOrder.ToArray()));
        }

        private MarketNativeHoverRowReport Capture(
            RuntimePreviewInteractionContext context,
            string label,
            Component expectedOwner,
            RuntimePreviewItemBinding expectedItem)
        {
            MarketNativeHoverRowReport row = new MarketNativeHoverRowReport
            {
                label = label,
                expectedOwner = expectedOwner == null ? string.Empty : PathOf(expectedOwner),
                expectedItemId = expectedItem == null ? string.Empty : expectedItem.ItemId,
                expectedContentScale = _viewport.expectedContentScale
            };
            Capture(context, row, expectedOwner, expectedItem);
            return row;
        }

        private void Capture(
            RuntimePreviewInteractionContext context,
            MarketNativeHoverRowReport row,
            Component expectedOwner,
            RuntimePreviewItemBinding expectedItem)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform root = context.Adapter.TooltipRoot;
            CanvasGroup group = root == null ? null : root.GetComponent<CanvasGroup>();
            row.ownerAfterDelay = PathOf(TooltipOwner(context.Adapter, false));
            row.tipActive = root != null && root.gameObject.activeSelf;
            row.groupAlpha = group == null ? 0f : group.alpha;
            Text title = RuntimePreviewMarketInteractionProbe.TooltipText(context.Adapter, "TitleText");
            Text body = RuntimePreviewMarketInteractionProbe.TooltipText(context.Adapter, "BodyText");
            Text hint = RuntimePreviewMarketInteractionProbe.TooltipText(context.Adapter, "HintText");
            row.title = title == null ? string.Empty : title.text;
            row.body = body == null ? string.Empty : body.text;
            row.hint = hint == null ? string.Empty : hint.text;
            Component provider = expectedOwner == null ? null : TooltipTrigger(expectedOwner.gameObject);
            row.expectedTitle = ProviderText(provider, "Title");
            row.expectedBody = ProviderText(provider, "Body");
            row.expectedHint = ProviderText(provider, "Hint");
            if (expectedOwner == null)
            {
                Check(context, row, !row.tipActive && string.IsNullOrEmpty(row.ownerAfterDelay),
                    "Tooltip survived the native exit.");
                return;
            }

            Check(context, row, row.tipActive && row.groupAlpha >= 0.999f,
                "Tooltip did not reach its visible terminal after the production delay and fade.");
            Check(context, row, row.ownerAfterDelay == row.expectedOwner,
                "Settled tooltip owner differs from the current native hit.");
            Check(context, row, row.title == row.expectedTitle && row.body == row.expectedBody && row.hint == row.expectedHint,
                "Tooltip text differs from its current owner providers; the final sentence or item marker may be stale.");
            if (expectedItem != null)
            {
                row.actualItemId = expectedItem.ItemId;
                if (string.IsNullOrEmpty(row.expectedItemId))
                    row.expectedItemId = expectedItem.ItemId;
                Rect cardScreen = RuntimePreviewMarketInteractionProbe.ScreenRect(
                    expectedOwner as RectTransform, context.Height, true);
                row.cardScreenTopLeftMin = cardScreen.min;
                row.cardScreenTopLeftMax = cardScreen.max;
                Check(context, row, row.expectedItemId == row.actualItemId,
                    "The native hover case bound a different formal item than its required item ID.");
                Check(context, row, row.title == expectedItem.Name && row.body == expectedItem.Description &&
                    row.hint == "点击查看行情 · " + expectedItem.Key,
                    "Tooltip does not match the formal current ItemDefinition binding.");
                Check(context, row, row.hint.Contains(expectedItem.Key),
                    "Current item identifier is absent from the visible hint.");
            }
            if (root == null || !row.tipActive || title == null || body == null || hint == null)
                return;

            Rect screen = RuntimePreviewMarketInteractionProbe.ScreenRect(root, context.Height, true);
            row.screenTopLeftMin = screen.min;
            row.screenTopLeftMax = screen.max;
            row.rootScreenWidth = screen.width;
            row.rootScreenHeight = screen.height;
            float unitsToScreen = root.rect.width <= 0.001f ? 0f : screen.width / root.rect.width;
            row.titlePhysicalFontSize = title.fontSize * unitsToScreen;
            row.bodyPhysicalFontSize = body.fontSize * unitsToScreen;
            row.hintPhysicalFontSize = hint.fontSize * unitsToScreen;
            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                row.horizontalPaddingPhysical = layout.padding.left * unitsToScreen;
                row.verticalPaddingPhysical = layout.padding.top * unitsToScreen;
                row.layoutSpacingPhysical = layout.spacing * unitsToScreen;
            }
            row.bodyLineSpacing = body.lineSpacing;
            RectTransform shadow = root.Find("Shadow") as RectTransform;
            if (shadow != null)
            {
                Rect shadowScreen = RuntimePreviewMarketInteractionProbe.ScreenRect(shadow, context.Height, true);
                row.shadowOffsetPhysical = new Vector2(shadowScreen.x - screen.x, shadowScreen.y - screen.y);
            }
            float margin = 10f * _viewport.expectedContentScale;
            row.clampMarginPhysical = margin;
            Check(context, row, screen.xMin >= margin - 0.75f && screen.yMin >= margin - 0.75f &&
                screen.xMax <= context.Width - margin + 0.75f && screen.yMax <= context.Height - margin + 0.75f,
                "Tooltip escaped the scaled native screen clamp: " + screen);

            row.bodyLineCount = body.cachedTextGenerator.lineCount;
            row.bodyVerticalOverflow = body.verticalOverflow.ToString();
            IList<UILineInfo> lines = body.cachedTextGenerator.lines;
            if (lines != null && lines.Count > 0)
            {
                if (lines.Count > 1)
                    row.generatedLinePitchPhysical = (lines[0].topY - lines[1].topY) * unitsToScreen;
                int start = Mathf.Clamp(lines[lines.Count - 1].startCharIdx, 0, body.text.Length);
                row.lastGeneratedLine = body.text.Substring(start).Trim();
            }
            Check(context, row, row.bodyLineCount > 0 && row.bodyVerticalOverflow != "Truncate",
                "Body text was truncated instead of retaining its generated final line.");
            if (expectedItem != null)
                Check(context, row, row.bodyLineCount >= 2, "The formal item description did not wrap onto multiple lines.");
            bool hintRequired = hint.gameObject.activeSelf && !string.IsNullOrEmpty(row.expectedHint);
            Rect hintScreen = RuntimePreviewMarketInteractionProbe.ScreenRect(hint.rectTransform, context.Height, true);
            row.hintInsideRoot = !hintRequired || hintScreen.xMin >= screen.xMin - 0.75f &&
                hintScreen.xMax <= screen.xMax + 0.75f && hintScreen.yMin >= screen.yMin - 0.75f &&
                hintScreen.yMax <= screen.yMax + 0.75f;
            Check(context, row, row.hintInsideRoot, "The item identifier hint is outside the visible tooltip paper.");

            ScrollRect scroll = root.GetComponentInChildren<ScrollRect>(true);
            row.overflowScrollPresent = scroll != null && scroll.gameObject.activeSelf;
            if (row.overflowScrollPresent)
            {
                row.bodyContentHeight = scroll.content.rect.height;
                row.bodyViewportHeight = scroll.viewport.rect.height;
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                Rect contentScreen = RuntimePreviewMarketInteractionProbe.ScreenRect(scroll.content, context.Height, true);
                Rect viewportScreen = RuntimePreviewMarketInteractionProbe.ScreenRect(scroll.viewport, context.Height, true);
                row.scrollReachedEnd = scroll.verticalNormalizedPosition <= 0.0001f;
                row.lastLineReadable = row.scrollReachedEnd &&
                    Mathf.Abs(contentScreen.yMin - viewportScreen.yMin) <= 1.5f && row.hintInsideRoot;
            }
            else
            {
                row.lastLineReadable = row.bodyLineCount > 0 && body.preferredHeight <= body.rectTransform.rect.height + 1f &&
                    row.hintInsideRoot;
            }
            Check(context, row, row.lastLineReadable,
                "The generated final body line is not readable within the limited tooltip viewport.");
        }

        private void CaptureCards(RuntimePreviewInteractionContext context, MarketNativeHoverRowReport row)
        {
            IList cards = GetField(GetField(context.Adapter, "_market"), "_cards") as IList;
            GameObject hit = context.PointerEnterHandler;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                GameObject card = context.Adapter.CardRoot(i);
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                bool pointerInside = hit != null && hit.transform.IsChildOf(card.transform);
                bool focused = selected != null && selected.transform.IsChildOf(card.transform);
                bool expected = card.activeInHierarchy && (pointerInside || focused);
                bool actual = Convert.ToBoolean(GetField(cards[i], "_hovered"), CultureInfo.InvariantCulture);
                row.cards.Add(new MarketNativeHoverCardReport
                {
                    path = PathOf(card.transform),
                    expectedHighlight = expected,
                    actualHighlight = actual
                });
            }
        }

        private static Rect CardBounds(GameObject card, int screenHeight)
        {
            return RuntimePreviewMarketInteractionProbe.ScreenRect(card.transform as RectTransform, screenHeight, true);
        }

        private static bool RectClose(Rect actual, Rect expected)
        {
            return Vector2.Distance(actual.min, expected.min) <= 0.75f &&
                Vector2.Distance(actual.max, expected.max) <= 0.75f;
        }

        private sealed class DiagnosticCardLayoutSwap : IDisposable
        {
            private readonly GameObject _item07;
            private readonly GameObject _item12;
            private readonly Transform _item07Parent;
            private readonly Transform _item12Parent;
            private readonly int _item07Sibling;
            private readonly int _item12Sibling;
            private readonly Vector3 _item07Position;
            private readonly Vector3 _item12Position;
            private readonly List<LayoutGroup> _layouts = new List<LayoutGroup>();
            private readonly List<bool> _layoutEnabled = new List<bool>();

            internal DiagnosticCardLayoutSwap(GameObject item07, GameObject item12)
            {
                _item07 = item07;
                _item12 = item12;
                _item07Parent = item07.transform.parent;
                _item12Parent = item12.transform.parent;
                _item07Sibling = item07.transform.GetSiblingIndex();
                _item12Sibling = item12.transform.GetSiblingIndex();
                _item07Position = item07.transform.position;
                _item12Position = item12.transform.position;
                CollectLayouts(item07.transform);
                CollectLayouts(item12.transform);
                for (int i = 0; i < _layouts.Count; i++)
                    _layouts[i].enabled = false;
                _item07.transform.SetParent(_item12Parent, true);
                _item12.transform.SetParent(_item07Parent, true);
                _item07.transform.position = _item12Position;
                _item12.transform.position = _item07Position;
                _item07.transform.SetSiblingIndex(_item12Sibling);
                _item12.transform.SetSiblingIndex(_item07Sibling);
            }

            public void Dispose()
            {
                _item07.transform.SetParent(_item07Parent, true);
                _item12.transform.SetParent(_item12Parent, true);
                _item07.transform.position = _item07Position;
                _item12.transform.position = _item12Position;
                _item07.transform.SetSiblingIndex(_item07Sibling);
                _item12.transform.SetSiblingIndex(_item12Sibling);
                for (int i = 0; i < _layouts.Count; i++)
                    _layouts[i].enabled = _layoutEnabled[i];
            }

            private void CollectLayouts(Transform target)
            {
                LayoutGroup[] layouts = target.GetComponentsInParent<LayoutGroup>();
                for (int i = 0; i < layouts.Length; i++)
                {
                    if (_layouts.Contains(layouts[i]))
                        continue;
                    _layouts.Add(layouts[i]);
                    _layoutEnabled.Add(layouts[i].enabled);
                }
            }
        }

        private static Vector2 BottomBodyPoint(GameObject card)
        {
            RectTransform rect = card.transform as RectTransform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = card.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            float width = topRight.x - topLeft.x;
            float height = topLeft.y - bottomLeft.y;
            return new Vector2(topRight.x - width * 0.04f, bottomLeft.y + height * 0.34f);
        }

        private void AttachEventRecorders(RuntimePreviewInteractionContext context)
        {
            for (int i = 0; i < 6; i++)
            {
                AddRecorder(context.Adapter.CardRoot(i));
                AddRecorder(context.Adapter.CardActionButton(i, "Buy").gameObject);
                AddRecorder(context.Adapter.CardActionButton(i, "Borrow").gameObject);
            }
        }

        private static void AddRecorder(GameObject target)
        {
            if (target.GetComponent<MarketNativeHoverEventRecorder>() == null)
                target.AddComponent<MarketNativeHoverEventRecorder>();
        }

        private void SyncAfterExternalMove(RuntimePreviewInteractionContext context)
        {
            _previousPointerEnter = PathOf(context.PointerEnterHandler == null
                ? null
                : context.PointerEnterHandler.transform);
            _lastNativeSerial = 0;
        }

        private static Component TooltipTrigger(GameObject target)
        {
            MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().FullName ==
                    "XingShangMod.UI.Infrastructure.UiTooltipTrigger")
                    return components[i];
            }
            return null;
        }

        private static Component TooltipOwner(RuntimePreviewAdapter adapter, bool pending)
        {
            object overlays = GetField(adapter, "_overlays");
            object tooltip = GetField(overlays, "_tooltip");
            return GetField(tooltip, pending ? "_pendingOwner" : "_owner") as Component;
        }

        private static string ProviderText(Component provider, string name)
        {
            Func<string> value = GetField(provider, name) as Func<string>;
            return value == null ? string.Empty : value() ?? string.Empty;
        }

        private static float ExpectedScale(int width, int height)
        {
            return width >= 2304 && height >= 1296 ? 1.5f : 1f;
        }

        private void Check(
            RuntimePreviewInteractionContext context,
            MarketNativeHoverRowReport row,
            bool condition,
            string message)
        {
            if (condition)
                return;
            row.failures.Add(message);
            context.Fail(row.label + ": " + message);
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add(new RuntimePreviewLogEntry { type = type.ToString(), condition = condition, stackTrace = stackTrace });
        }

        private void Finish(int exitCode)
        {
            if (_options.ExitEditorWhenDone)
            {
                SessionState.SetInt(RuntimePreview.ExitCodeKey, exitCode);
                EditorApplication.isPlaying = false;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static Exception Unwind(Exception exception)
        {
            while (exception.InnerException != null)
                exception = exception.InnerException;
            return exception;
        }

        private static object GetField(object target, string name)
        {
            if (target == null)
                return null;
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

        private static string PathOf(Component target)
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

    public sealed class MarketNativeHoverEventRecorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal static List<string> Active;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Active != null)
                Active.Add("enter:" + name);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Active != null)
                Active.Add("exit:" + name);
        }
    }
}
