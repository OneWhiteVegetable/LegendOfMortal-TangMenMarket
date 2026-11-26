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
    public sealed class Round4CardArtReport
    {
        public int schemaVersion = 1;
        public string status;
        public string evidenceType = "Isolated Unity Editor, formal runtime DLL/bundle, native uGUI pointer dispatch; not game footage";
        public string startedUtc;
        public string finishedUtc;
        public string unityVersion;
        public string runtimeAssemblySha256;
        public string bundleSha256;
        public List<Round4CardArtViewportReport> viewports = new List<Round4CardArtViewportReport>();
        public List<RuntimePreviewLogEntry> errors = new List<RuntimePreviewLogEntry>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class Round4CardArtViewportReport
    {
        public int width;
        public int height;
        public float expectedContentScale;
        public string status;
        public Round4CardArtQuantityReport quantity = new Round4CardArtQuantityReport();
        public List<Round4CardArtPoseReport> poses = new List<Round4CardArtPoseReport>();
        public List<RuntimePreviewPointerEvidence> pointerEvents = new List<RuntimePreviewPointerEvidence>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class Round4CardArtQuantityReport
    {
        public bool interactionStatePresent;
        public bool stepperFacePresent;
        public bool legacyPressDriverPresent;
        public bool decrementDisabledAtOne;
        public bool decrementEnabledAtTwo;
        public bool returnedToOne;
        public string valueAfterIncrement;
        public string valueAfterDecrement;
        public Vector2 incrementCenterScreen;
        public Vector2 decrementCenterScreen;
        public Rect incrementScreenRect;
        public Rect decrementScreenRect;
        public string incrementTargetGraphic;
        public string decrementTargetGraphic;
        public bool incrementTargetGraphicRaycast;
        public bool decrementTargetGraphicRaycast;
        public Vector3 incrementIconScaleDuringPress;
        public Vector3 incrementFaceScaleDuringPress;
        public Vector3 incrementButtonScaleDuringPress;
        public Vector3 decrementIconScaleDuringPress;
        public Vector3 decrementFaceScaleDuringPress;
        public Vector3 decrementButtonScaleDuringPress;
    }

    [Serializable]
    public sealed class Round4CardArtPoseReport
    {
        public string label;
        public string itemId;
        public string png;
        public string layoutPath;
        public Vector2 visualPivot;
        public Vector2 visualAnchorMin;
        public Vector2 visualAnchorMax;
        public Vector2 visualPosition;
        public Vector3 visualScale;
        public Vector3 visualEuler;
        public Vector2 visualRectSize;
        public Vector2 layoutPosition;
        public Vector3 layoutScale;
        public Vector3 layoutEuler;
        public Vector2 layoutRectSize;
        public float designScale;
        public Vector2 visualCenterScreen;
        public Vector2 layoutCenterScreen;
        public Vector2 extraCenterDeltaScreen;
        public Vector2 expectedExtraCenterDeltaScreen;
        public Vector2 buyPosition;
        public Vector2 buyRectSize;
        public Vector2 borrowPosition;
        public Vector2 borrowRectSize;
    }

    public static class Round4CardArtValidation
    {
        internal const string PendingKey = "XingShang.RuntimePreview.Round4CardArt.Pending";

        [MenuItem("XingShang/Runtime Preview/Run Round 4 Card Art Validation")]
        public static void RunMenu()
        {
            RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
            options.OutputPath = Path.Combine(options.ProjectPath, "Build/Round4CardArt");
            Run(options);
        }

        public static void BatchEntry()
        {
            try
            {
                RuntimePreviewOptions options = RuntimePreviewOptions.CreateDefault();
                options.OutputPath = Path.Combine(options.ProjectPath, "Build/Round4CardArt");
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
                throw new InvalidOperationException("Round 4 card art validation requires an Editor Game View; do not use -batchmode.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting Round 4 card art validation.");
            options.Cases = new[] { "round-4-card-art" };
            if (options.Widths == null || options.Widths.Length == 0)
            {
                options.Widths = new[] { 2560, 1920, 1280, 1366 };
                options.Heights = new[] { 1440, 1080, 720, 768 };
            }
            options.Validate();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close dirty scenes before Round 4 card art validation.");
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
                    case "-runtimePreviewFixture": options.FixturePath = Path.GetFullPath(value); break;
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
    public static class Round4CardArtBootstrap
    {
        static Round4CardArtBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !SessionState.GetBool(Round4CardArtValidation.PendingKey, false))
                return;
            SessionState.SetBool(Round4CardArtValidation.PendingKey, false);
            try
            {
                RuntimePreviewOptions options = JsonUtility.FromJson<RuntimePreviewOptions>(
                    SessionState.GetString(RuntimePreview.ConfigurationKey, string.Empty));
                new GameObject("XingShang.RuntimePreview.Round4CardArt")
                    .AddComponent<Round4CardArtRunner>().Configure(options);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SessionState.SetInt(RuntimePreview.ExitCodeKey, 1);
                EditorApplication.isPlaying = false;
            }
        }
    }

    public sealed class Round4CardArtRunner : MonoBehaviour
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private RuntimePreviewOptions _options;
        private readonly List<RuntimePreviewLogEntry> _errors = new List<RuntimePreviewLogEntry>();

        internal void Configure(RuntimePreviewOptions options)
        {
            _options = options;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_options.OutputPath);
            Application.logMessageReceived += OnLogMessage;
            Round4CardArtReport report = new Round4CardArtReport
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
            Debug.Log("XingShang Round 4 card art validation " + report.status + ": " +
                Path.Combine(_options.OutputPath, "report.json"));
            yield return null;
            Finish(exitCode);
        }

        private IEnumerator RunMatrix(Round4CardArtReport report)
        {
            for (int index = 0; index < _options.Widths.Length; index++)
            {
                int width = _options.Widths[index];
                int height = _options.Heights[index];
                Round4CardArtViewportReport viewport = new Round4CardArtViewportReport
                {
                    width = width,
                    height = height,
                    expectedContentScale = ExpectedScale(width, height),
                    status = "running"
                };
                report.viewports.Add(viewport);
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
                    throw new InvalidOperationException("Round 4 Game View did not reach " + width + "x" + height + ".");

                RuntimePreviewAdapter adapter = new RuntimePreviewAdapter(_options);
                RuntimePreviewInteractionContext context = new RuntimePreviewInteractionContext
                {
                    Adapter = adapter,
                    Width = width,
                    Height = height,
                    Result = new RuntimePreviewInteractionTestResult { name = "round-4-card-art" }
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
                    yield return RunViewport(context, viewport);
                }
                finally
                {
                    adapter.Dispose();
                }
                viewport.pointerEvents.AddRange(context.Result.pointerEvents);
                viewport.failures.AddRange(context.Result.failures);
                viewport.status = viewport.failures.Count == 0 ? "passed" : "failed";
                VisualValidation.WriteJson(Path.Combine(_options.OutputPath, "report.json"), report);
                yield return null;
            }
        }

        private IEnumerator RunViewport(
            RuntimePreviewInteractionContext context,
            Round4CardArtViewportReport viewport)
        {
            GameObject card = context.Adapter.CardRoot(0);
            RuntimePreviewPointerDriver.Move(context, card, false);
            yield return context.Wait(0.25f);
            yield return ValidateQuantity(context, viewport.quantity);
            EventSystem.current.SetSelectedGameObject(null);
            RuntimePreviewPointerDriver.Move(context, card, false);
            yield return context.Wait(0.3f);

            Round4CardArtPoseReport rest = null;
            yield return CapturePose(context, viewport, "rest", true, delegate(Round4CardArtPoseReport pose) { rest = pose; });
            CheckRest(context, rest, "01");

            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.Wait(0.28f);
            Round4CardArtPoseReport hover = null;
            yield return CapturePose(context, viewport, "hover", true, delegate(Round4CardArtPoseReport pose) { hover = pose; });
            CheckHover(context, rest, hover, viewport.expectedContentScale);

            Button buy = context.Adapter.CardActionButton(0, "Buy");
            RuntimePreviewPointerDriver.Move(context, buy.gameObject, true);
            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.Wait(0.2f);

            float rapidStart = context.Now;
            RuntimePreviewPointerDriver.Move(context, card, false);
            yield return context.WaitUntil(rapidStart, 35f);
            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.WaitUntil(rapidStart, 70f);
            RuntimePreviewPointerDriver.Move(context, card, false);
            yield return context.WaitUntil(rapidStart, 105f);
            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.Wait(0.28f);
            Round4CardArtPoseReport rapidHover = null;
            yield return CapturePose(context, viewport, "rapid-hover", true, delegate(Round4CardArtPoseReport pose) { rapidHover = pose; });
            CheckHover(context, rest, rapidHover, viewport.expectedContentScale);
            RuntimePreviewPointerDriver.Move(context, card, false);
            RuntimePreviewPointerDriver.Move(context, card, true);
            RuntimePreviewPointerDriver.Move(context, card, false);
            EventSystem.current.SetSelectedGameObject(null);
            yield return context.Wait(0.3f);
            Round4CardArtPoseReport rapidRest = null;
            yield return CapturePose(context, viewport, "rapid-rest", false, delegate(Round4CardArtPoseReport pose) { rapidRest = pose; });
            CheckRest(context, rapidRest, "01");

            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.Wait(0.04f);
            context.Adapter.SetReducedMotion(true);
            yield return context.WaitFrames(1);
            Round4CardArtPoseReport reducedHover = null;
            yield return CapturePose(context, viewport, "reduced-hover", true, delegate(Round4CardArtPoseReport pose) { reducedHover = pose; });
            Check(context, context.Adapter.IsReducedMotion, "ReducedMotion did not become active.");
            CheckHover(context, rest, reducedHover, viewport.expectedContentScale);
            RuntimePreviewPointerDriver.Move(context, card, false);
            EventSystem.current.SetSelectedGameObject(null);
            yield return context.WaitFrames(1);
            Round4CardArtPoseReport reducedRest = null;
            yield return CapturePose(context, viewport, "reduced-rest", false, delegate(Round4CardArtPoseReport pose) { reducedRest = pose; });
            CheckRest(context, reducedRest, "01");
            context.Adapter.SetReducedMotion(false);

            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.Wait(0.04f);
            Button next = RuntimePreviewMarketInteractionProbe.MarketButton(context.Adapter, "NextPageButton");
            Check(context, next != null && next.IsInteractable(), "Next-page button is unavailable for card reuse validation.");
            RuntimePreviewPointerDriver.Click(context, next.gameObject);
            EventSystem.current.SetSelectedGameObject(null);
            yield return context.Wait(0.3f);
            Round4CardArtPoseReport reusedRest = null;
            yield return CapturePose(context, viewport, "page-two-rest", true, delegate(Round4CardArtPoseReport pose) { reusedRest = pose; });
            CheckRest(context, reusedRest, "07");
            RuntimePreviewPointerDriver.Move(context, card, true);
            yield return context.Wait(0.28f);
            Round4CardArtPoseReport reusedHover = null;
            yield return CapturePose(context, viewport, "page-two-hover", true, delegate(Round4CardArtPoseReport pose) { reusedHover = pose; });
            CheckHover(context, reusedRest, reusedHover, viewport.expectedContentScale);
            RuntimePreviewPointerDriver.Move(context, card, false);
            yield return context.Wait(0.25f);
        }

        private IEnumerator ValidateQuantity(
            RuntimePreviewInteractionContext context,
            Round4CardArtQuantityReport report)
        {
            Button decrement = CardButton(context.Adapter, 0, "QuantityDecrementButton");
            Button increment = CardButton(context.Adapter, 0, "QuantityIncrementButton");
            Text value = RuntimePreviewMarketInteractionProbe.CardText(context.Adapter, 0, "QuantityText");
            report.interactionStatePresent = HasComponent(increment, "MarketIconInteractionState") &&
                HasComponent(decrement, "MarketIconInteractionState");
            report.stepperFacePresent = increment.transform.Find("StepperFace") != null &&
                decrement.transform.Find("StepperFace") != null;
            report.legacyPressDriverPresent = HasComponent(increment, "GoodsCardControlPressDriver") ||
                HasComponent(decrement, "GoodsCardControlPressDriver");
            report.decrementDisabledAtOne = !decrement.IsInteractable() && value.text == "1";
            report.incrementCenterScreen = RuntimePreviewPointerDriver.Center(increment.gameObject);
            report.decrementCenterScreen = RuntimePreviewPointerDriver.Center(decrement.gameObject);
            report.incrementScreenRect = RuntimePreviewMarketInteractionProbe.ScreenRect(
                increment.GetComponent<RectTransform>(), context.Height, true);
            report.decrementScreenRect = RuntimePreviewMarketInteractionProbe.ScreenRect(
                decrement.GetComponent<RectTransform>(), context.Height, true);
            report.incrementTargetGraphic = PathOf(increment.targetGraphic);
            report.decrementTargetGraphic = PathOf(decrement.targetGraphic);
            report.incrementTargetGraphicRaycast = increment.targetGraphic != null && increment.targetGraphic.raycastTarget;
            report.decrementTargetGraphicRaycast = decrement.targetGraphic != null && decrement.targetGraphic.raycastTarget;
            Check(context, report.interactionStatePresent, "Quantity buttons no longer share MarketIconInteractionState.");
            Check(context, report.stepperFacePresent, "Quantity buttons were not attached through the stepper visual interface.");
            Check(context, !report.legacyPressDriverPresent, "Legacy GoodsCardControlPressDriver is still attached.");
            Check(context, report.decrementDisabledAtOne, "Draft decrement is not disabled at quantity one.");

            Image incrementIcon = increment.transform.Find("Icon").GetComponent<Image>();
            RectTransform incrementFace = increment.transform.Find("StepperFace") as RectTransform;
            RuntimePreviewPointerDriver.Move(context, increment.gameObject, true);
            RuntimePreviewMarketInteractionProbe.Down(context, increment.gameObject);
            yield return context.Wait(0.14f);
            report.incrementIconScaleDuringPress = incrementIcon.rectTransform.localScale;
            report.incrementFaceScaleDuringPress = incrementFace == null ? Vector3.zero : incrementFace.localScale;
            report.incrementButtonScaleDuringPress = increment.GetComponent<RectTransform>().localScale;
            CheckPressedCell(context, report.incrementIconScaleDuringPress,
                report.incrementFaceScaleDuringPress, report.incrementButtonScaleDuringPress, "Increment");
            RuntimePreviewMarketInteractionProbe.UpAndClick(context, increment.gameObject);
            report.valueAfterIncrement = value.text;
            report.decrementEnabledAtTwo = decrement.IsInteractable() && value.text == "2";
            Check(context, report.decrementEnabledAtTwo, "Verified increment click did not produce quantity two and enable decrement.");

            Image decrementIcon = decrement.transform.Find("Icon").GetComponent<Image>();
            RectTransform decrementFace = decrement.transform.Find("StepperFace") as RectTransform;
            RuntimePreviewPointerDriver.Move(context, decrement.gameObject, true);
            RuntimePreviewMarketInteractionProbe.Down(context, decrement.gameObject);
            yield return context.Wait(0.14f);
            report.decrementIconScaleDuringPress = decrementIcon.rectTransform.localScale;
            report.decrementFaceScaleDuringPress = decrementFace == null ? Vector3.zero : decrementFace.localScale;
            report.decrementButtonScaleDuringPress = decrement.GetComponent<RectTransform>().localScale;
            CheckPressedCell(context, report.decrementIconScaleDuringPress,
                report.decrementFaceScaleDuringPress, report.decrementButtonScaleDuringPress, "Decrement");
            RuntimePreviewMarketInteractionProbe.UpAndClick(context, decrement.gameObject);
            report.valueAfterDecrement = value.text;
            report.returnedToOne = !decrement.IsInteractable() && value.text == "1";
            Check(context, report.returnedToOne, "Verified decrement click did not return to disabled quantity one.");
            RuntimePreviewPointerDriver.Move(context, decrement.gameObject, false);
            yield return context.Wait(0.2f);
        }

        private static void CheckPressedCell(
            RuntimePreviewInteractionContext context,
            Vector3 iconScale,
            Vector3 faceScale,
            Vector3 buttonScale,
            string label)
        {
            Vector3 expected = new Vector3(0.88f, 0.88f, 1f);
            bool iconOkay = Nearly(iconScale, expected);
            bool faceOkay = Nearly(faceScale, expected);
            bool buttonOkay = Nearly(buttonScale, Vector3.one);
            Check(context, iconOkay && faceOkay && buttonOkay,
                label + ": Press visuals are not one 0.88 face/icon transform with a stable hit rect: icon=" +
                iconScale.ToString("F8") + " face=" + faceScale.ToString("F8") + " button=" +
                buttonScale.ToString("F8") + " expected=" + expected.ToString("F8") + " checks=" +
                iconOkay + "/" + faceOkay + "/" + buttonOkay + " distances=" +
                Vector3.Distance(iconScale, expected).ToString("R", CultureInfo.InvariantCulture) + "/" +
                Vector3.Distance(faceScale, expected).ToString("R", CultureInfo.InvariantCulture) + "/" +
                Vector3.Distance(buttonScale, Vector3.one).ToString("R", CultureInfo.InvariantCulture));
        }

        private IEnumerator CapturePose(
            RuntimePreviewInteractionContext context,
            Round4CardArtViewportReport viewport,
            string label,
            bool png,
            Action<Round4CardArtPoseReport> assign)
        {
            Canvas.ForceUpdateCanvases();
            Image image = RuntimePreviewMarketInteractionProbe.CardItemImage(context.Adapter, 0);
            RectTransform visual = image.rectTransform;
            RectTransform layout = visual.parent as RectTransform;
            object card = Card(context.Adapter, 0);
            Button buy = context.Adapter.CardActionButton(0, "Buy");
            Button borrow = context.Adapter.CardActionButton(0, "Borrow");
            float designScale = Convert.ToSingle(GetField(card, "_designScale"), CultureInfo.InvariantCulture);
            Round4CardArtPoseReport pose = new Round4CardArtPoseReport
            {
                label = label,
                itemId = RuntimePreviewMarketInteractionProbe.CardItemId(context.Adapter, 0),
                layoutPath = PathOf(layout),
                visualPivot = visual.pivot,
                visualAnchorMin = visual.anchorMin,
                visualAnchorMax = visual.anchorMax,
                visualPosition = visual.anchoredPosition,
                visualScale = visual.localScale,
                visualEuler = visual.localRotation.eulerAngles,
                visualRectSize = visual.rect.size,
                layoutPosition = layout.anchoredPosition,
                layoutScale = layout.localScale,
                layoutEuler = layout.localRotation.eulerAngles,
                layoutRectSize = layout.rect.size,
                designScale = designScale,
                visualCenterScreen = RuntimePreviewPointerDriver.Center(visual.gameObject),
                layoutCenterScreen = RuntimePreviewPointerDriver.Center(layout.gameObject),
                buyPosition = buy.GetComponent<RectTransform>().anchoredPosition,
                buyRectSize = buy.GetComponent<RectTransform>().rect.size,
                borrowPosition = borrow.GetComponent<RectTransform>().anchoredPosition,
                borrowRectSize = borrow.GetComponent<RectTransform>().rect.size
            };
            pose.extraCenterDeltaScreen = pose.visualCenterScreen - pose.layoutCenterScreen;
            pose.expectedExtraCenterDeltaScreen = RuntimePreviewMarketInteractionProbe.ScreenDelta(
                visual, new Vector2(0f, 5f / Mathf.Max(0.01f, designScale)));
            viewport.poses.Add(pose);
            assign(pose);
            Check(context, layout.name == "ItemArtLayout" && visual.parent == layout &&
                layout.parent != null && layout.parent.name == "VisualContent",
                "ItemArtLayout does not preserve the original layout parent.");
            Check(context, Nearly(visual.pivot, new Vector2(0.5f, 0.5f)) &&
                Nearly(visual.anchorMin, Vector2.zero) && Nearly(visual.anchorMax, Vector2.one),
                label + ": ItemImage is not a centered, layout-filling visual node.");
            Check(context, Nearly(visual.rect.size, layout.rect.size),
                label + ": Visual rect and layout rect differ before local visual transforms.");
            if (!png)
                yield break;

            string directory = Path.Combine(_options.OutputPath, viewport.width + "x" + viewport.height);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, label + ".png");
            pose.png = path;
            if (File.Exists(path))
                File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path, 1);
            bool written = false;
            for (int frame = 0; frame < 90; frame++)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    written = true;
                    break;
                }
                yield return new WaitForEndOfFrame();
            }
            yield return context.WaitFrames(2);
            Check(context, written, label + ": Native PNG was not written.");
            if (!written)
                yield break;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                bool loaded = ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false);
                Check(context, loaded && texture.width == viewport.width && texture.height == viewport.height,
                    label + ": PNG dimensions differ from the requested native viewport.");
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static void CheckRest(
            RuntimePreviewInteractionContext context,
            Round4CardArtPoseReport pose,
            string expectedItemId)
        {
            Check(context, pose != null, "Rest pose was not captured.");
            if (pose == null)
                return;
            Check(context, pose.itemId == expectedItemId,
                pose.label + ": Reused card item is " + pose.itemId + ", expected " + expectedItemId + ".");
            Check(context, Nearly(pose.visualPosition, Vector2.zero) && Nearly(pose.visualScale, Vector3.one) &&
                Quaternion.Angle(Quaternion.identity, Quaternion.Euler(pose.visualEuler)) <= 0.01f,
                pose.label + ": Item art did not reset its local position, scale, and rotation.");
            Check(context, pose.extraCenterDeltaScreen.magnitude <= 0.02f,
                pose.label + ": Rest visual center differs from the layout center: " + pose.extraCenterDeltaScreen);
        }

        private static void CheckHover(
            RuntimePreviewInteractionContext context,
            Round4CardArtPoseReport rest,
            Round4CardArtPoseReport hover,
            float expectedContentScale)
        {
            Check(context, rest != null && hover != null, "Hover endpoints were not captured.");
            if (rest == null || hover == null)
                return;
            float expectedY = 5f / Mathf.Max(0.01f, hover.designScale);
            Check(context, Mathf.Abs(hover.visualPosition.y - expectedY) <= 0.002f &&
                Mathf.Abs(hover.visualPosition.x) <= 0.002f,
                hover.label + ": Extra art lift is not 5 CSS px in the current design scale: " + hover.visualPosition);
            Check(context, Nearly(hover.visualScale, rest.visualScale * 1.045f),
                hover.label + ": Item art scale is not 1.045: " + hover.visualScale);
            Quaternion targetRotation = Quaternion.Euler(rest.visualEuler) * Quaternion.Euler(0f, 0f, 1f);
            Check(context, Quaternion.Angle(targetRotation, Quaternion.Euler(hover.visualEuler)) <= 0.01f,
                hover.label + ": Item art does not use Unity +1 degree for screen-counterclockwise rotation.");
            Check(context, Vector2.Distance(hover.extraCenterDeltaScreen, hover.expectedExtraCenterDeltaScreen) <= 0.08f &&
                Mathf.Abs(hover.expectedExtraCenterDeltaScreen.y - 5f * expectedContentScale) <= 0.08f,
                hover.label + ": Art did not move about the centered layout frame by the expected physical delta: actual=" +
                hover.extraCenterDeltaScreen + " expected=" + hover.expectedExtraCenterDeltaScreen);
            Check(context, Nearly(hover.layoutPosition, rest.layoutPosition) &&
                Nearly(hover.layoutScale, rest.layoutScale) && Nearly(hover.layoutEuler, rest.layoutEuler) &&
                Nearly(hover.layoutRectSize, rest.layoutRectSize),
                hover.label + ": ItemArtLayout changed during visual-only hover.");
            Check(context, Nearly(hover.buyPosition, rest.buyPosition) && Nearly(hover.buyRectSize, rest.buyRectSize) &&
                Nearly(hover.borrowPosition, rest.borrowPosition) && Nearly(hover.borrowRectSize, rest.borrowRectSize),
                hover.label + ": Trade-button local hit geometry changed during item art hover.");
        }

        private static object Card(RuntimePreviewAdapter adapter, int index)
        {
            object market = GetField(adapter, "_market");
            IList cards = GetField(market, "_cards") as IList;
            if (cards == null || index < 0 || index >= cards.Count)
                throw new ArgumentOutOfRangeException("index");
            return cards[index];
        }

        private static Button CardButton(RuntimePreviewAdapter adapter, int index, string key)
        {
            object view = GetField(Card(adapter, index), "_view");
            return Invoke(view, "Button", key) as Button;
        }

        private static bool HasComponent(Component target, string typeName)
        {
            MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == typeName)
                    return true;
            }
            return false;
        }

        private static bool Nearly(Vector2 actual, Vector2 expected)
        {
            return Vector2.Distance(actual, expected) <= 0.002f;
        }

        private static bool Nearly(Vector3 actual, Vector3 expected)
        {
            return Vector3.Distance(actual, expected) <= 0.002f;
        }

        private static float ExpectedScale(int width, int height)
        {
            return width >= 2304 && height >= 1296 ? 1.5f : 1f;
        }

        private static void Check(RuntimePreviewInteractionContext context, bool condition, string message)
        {
            context.Check(condition, message);
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

        private static object Invoke(object target, string name, params object[] arguments)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            Type type = target.GetType();
            MethodInfo method = type.GetMethod(name, All);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method.Invoke(target, arguments);
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
}
