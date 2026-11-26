using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class Round4StepperValidationReport
    {
        public int schemaVersion = 1;
        public string status;
        public string unityVersion;
        public string method = "Isolated uGUI controls with Button event dispatch, a pressed-edge GraphicRaycaster probe, immediate press/release samples, 100 ms color transitions and RenderTexture pixels.";
        public string outputPath;
        public List<Round4StepperCaseReport> cases = new List<Round4StepperCaseReport>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    public sealed class Round4StepperCaseReport
    {
        public string name;
        public Vector2 buttonSize;
        public List<string> checks = new List<string>();
        public List<Round4StepperPixelReport> pixels = new List<Round4StepperPixelReport>();
    }

    [Serializable]
    public sealed class Round4StepperPixelReport
    {
        public string state;
        public string image;
        public Color32 faceSample;
        public float cellWidth;
        public float cellHeight;
        public float darkWidth;
        public float darkHeight;
        public int otherCellAndQuantityDifferentPixels;
    }

    public static class Round4StepperValidation
    {
        internal const string PendingKey = "XingShang.Round4Stepper.Pending";
        internal const string ExitCodeKey = "XingShang.Round4Stepper.ExitCode";

        private static readonly Color32 Ink = new Color32(32, 35, 33, 255);
        private static readonly Color32 Paper = new Color32(245, 240, 228, 255);
        private static Type _stateType;
        private static Type _motionType;
        private static MethodInfo _attachStepper;
        private static MethodInfo _apply;
        private static MethodInfo _advance;
        private static MethodInfo _setReducedMotion;
        private static FieldInfo _stepperField;

        [MenuItem("XingShang/Round 4/Independent Stepper Validation")]
        public static void RunMenu()
        {
            Round4StepperValidationReport report = Run();
            Debug.Log("Round4StepperValidation: " + report.status + " -> " + report.outputPath);
        }

        public static void BatchEntry()
        {
            try
            {
                if (Application.isBatchMode)
                    throw new InvalidOperationException("Round4 stepper validation requires an Editor Game View; do not use -batchmode.");
                SessionState.SetBool(PendingKey, true);
                SessionState.SetInt(ExitCodeKey, -1);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        internal static void RunFromPlayMode()
        {
            int exitCode = 0;
            try
            {
                LoadFormalRuntime();
                Round4StepperValidationReport report = Run();
                exitCode = report.status == "PASS" ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            SessionState.SetInt(ExitCodeKey, exitCode);
            EditorApplication.isPlaying = false;
        }

        private static void LoadFormalRuntime()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(candidate => candidate.GetName().Name == "XingShangMod"))
                return;
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string plugin = Path.Combine(repository, "bin/Release/XingShangMod.dll");
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == "-round4StepperDll")
                    plugin = Path.GetFullPath(arguments[i + 1]);
            }
            const string game = "C:/allsoft/steam/steamapps/common/LegendOfMortal";
            string[] directories =
            {
                Path.Combine(game, "Mortal_Data/Managed"),
                Path.Combine(game, "BepInEx/core"),
                Path.GetDirectoryName(plugin)
            };
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
            {
                string simpleName = new AssemblyName(args.Name).Name;
                if (simpleName.StartsWith("Unity", StringComparison.Ordinal))
                    return null;
                for (int i = 0; i < directories.Length; i++)
                {
                    string candidate = Path.Combine(directories[i], simpleName + ".dll");
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }
                return null;
            };
            Assembly.LoadFrom(plugin);
        }

        public static Round4StepperValidationReport Run()
        {
            Round4StepperValidationReport report = new Round4StepperValidationReport
            {
                unityVersion = Application.unityVersion,
                outputPath = ResolveOutputPath()
            };
            Directory.CreateDirectory(report.outputPath);
            GameObject canvasObject = null;
            GameObject cameraObject = null;
            GameObject eventObject = null;
            RenderTexture renderTexture = null;
            try
            {
                InitializeReflection();
                object motion = Activator.CreateInstance(_motionType, true);
                renderTexture = new RenderTexture(400, 300, 24, RenderTextureFormat.ARGB32);
                renderTexture.Create();
                cameraObject = new GameObject("Round4StepperCamera", typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(72, 72, 68, 255);
                camera.orthographic = true;
                camera.orthographicSize = 150f;
                camera.transform.position = new Vector3(0f, 0f, -100f);
                camera.targetTexture = renderTexture;
                canvasObject = new GameObject("Round4StepperCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                eventObject = new GameObject("Round4StepperEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                CaseSpec[] specs =
                {
                    new CaseSpec("goods-card", 28f, 27f, 30f),
                    new CaseSpec("goods-card-narrow-short", 24f, 24f, 26f),
                    new CaseSpec("cart-entry", 23f, 22f, 24f),
                    new CaseSpec("item-detail", 34f, 34f, 44f)
                };
                for (int i = 0; i < specs.Length; i++)
                    RunCase(report, specs[i], canvas, camera, renderTexture, motion);
            }
            catch (Exception exception)
            {
                report.failures.Add(exception.ToString());
            }
            finally
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
                if (canvasObject != null)
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                if (eventObject != null)
                    UnityEngine.Object.DestroyImmediate(eventObject);
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
            report.status = report.failures.Count == 0 && report.cases.Count == 4 ? "PASS" : "FAIL";
            File.WriteAllText(
                Path.Combine(report.outputPath, "report.json"),
                JsonUtility.ToJson(report, true));
            return report;
        }

        private static void RunCase(
            Round4StepperValidationReport report,
            CaseSpec spec,
            Canvas canvas,
            Camera camera,
            RenderTexture renderTexture,
            object motion)
        {
            Round4StepperCaseReport result = new Round4StepperCaseReport
            {
                name = spec.Name,
                buttonSize = new Vector2(spec.ButtonWidth, spec.ButtonHeight)
            };
            report.cases.Add(result);
            Fixture fixture = null;
            List<Frame> frames = new List<Frame>();
            try
            {
                _setReducedMotion.Invoke(motion, new object[] { false });
                fixture = CreateFixture(spec, canvas.transform, motion);
                EventSystem.current.SetSelectedGameObject(null);
                Settle(fixture, 0.1f);
                Frame rest = Capture(report, result, fixture, camera, renderTexture, "rest");
                frames.Add(rest);
                Check(report, result, Luminance(rest.FaceSample) > 180f, "rest face remains paper");

                PointerEventData pointer = Pointer(fixture.Decrement);
                Enter(fixture.Decrement, pointer);
                Settle(fixture, 0.1f);
                Check(report, result, Face(fixture.Decrement).color.a == 0f, "disabled minus has no black face");
                Check(report, result, Mathf.Abs(Icon(fixture.Decrement).color.a - 0.28f) < 0.001f, "disabled minus alpha is 0.28");
                Exit(fixture.Decrement, pointer);

                pointer = Pointer(fixture.Increment);
                Enter(fixture.Increment, pointer);
                Settle(fixture, 0.1f);
                Frame hover = Capture(report, result, fixture, camera, renderTexture, "hover-plus");
                frames.Add(hover);
                Check(report, result, Luminance(hover.FaceSample) < 70f, "available plus has an ink-filled cell");
                Check(report, result, hover.DarkWidth >= hover.CellWidth - 2f && hover.DarkHeight >= hover.CellHeight - 2f,
                    "hover ink covers the whole subcell rather than only the icon");
                int unchangedPixels = DifferentPixels(rest.Texture, hover.Texture, fixture.Decrement.Rect, camera) +
                    DifferentPixels(rest.Texture, hover.Texture, fixture.Quantity.rectTransform, camera);
                hover.Pixels.otherCellAndQuantityDifferentPixels = unchangedPixels;
                Check(report, result, unchangedPixels == 0, "quantity digits and the other subcell are pixel-identical on plus hover");
                Check(report, result, Icon(fixture.Increment).color == (Color)Paper, "plus symbol is paper on ink");

                Vector3[] restCorners = new Vector3[4];
                fixture.Increment.Rect.GetWorldCorners(restCorners);
                Down(fixture.Increment, pointer);
                Check(report, result, fixture.QuantityValue == 1 &&
                    Mathf.Approximately(Face(fixture.Increment).rectTransform.localScale.x, 0.88f) &&
                    Mathf.Approximately(Icon(fixture.Increment).rectTransform.localScale.x, 0.88f),
                    "short clicks show the full pressed face and symbol immediately on pointer down");
                Settle(fixture, 0.1f);
                Frame pressed = Capture(report, result, fixture, camera, renderTexture, "press-plus");
                frames.Add(pressed);
                Check(report, result, Mathf.Abs(Face(fixture.Increment).rectTransform.localScale.x - 0.88f) < 0.001f &&
                    Mathf.Abs(Icon(fixture.Increment).rectTransform.localScale.x - 0.88f) < 0.001f,
                    "pressed face and symbol both scale to 0.88");
                Check(report, result, SameCorners(restCorners, fixture.Increment.Rect) &&
                    fixture.Increment.Rect.localScale == Vector3.one,
                    "pressed hit rect and world corners remain stable");
                Rect hitBounds = fixture.Increment.Rect.rect;
                Vector3 edge = fixture.Increment.Rect.TransformPoint(new Vector3(
                    hitBounds.xMin + 0.5f, hitBounds.center.y, 0f));
                var edgePointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(camera, edge)
                };
                var edgeHits = new List<RaycastResult>();
                canvas.GetComponent<GraphicRaycaster>().Raycast(edgePointer, edgeHits);
                Check(report, result, edgeHits.Count > 0 &&
                    edgeHits[0].gameObject.GetComponentInParent<Button>() == fixture.Increment.Button,
                    "a point just inside the unchanged hit target still hits plus outside its pressed face");
                Check(report, result, Ratio(pressed.DarkWidth, hover.DarkWidth, 0.88f) &&
                    Ratio(pressed.DarkHeight, hover.DarkHeight, 0.88f),
                    "pressed dark pixel bounds shrink to approximately 88 percent");

                ReleaseAndClick(fixture.Increment, pointer);
                Check(report, result, Mathf.Approximately(Face(fixture.Increment).rectTransform.localScale.x, 1f) &&
                    Mathf.Approximately(Icon(fixture.Increment).rectTransform.localScale.x, 1f),
                    "pointer up restores face and symbol size before any color-transition tick");
                Settle(fixture, 0.1f);
                Check(report, result, fixture.QuantityValue == 2 && fixture.Quantity.text == "2",
                    "one plus click changes quantity from 1 to 2");
                Check(report, result, fixture.Decrement.Button.IsInteractable(), "minus enables at quantity 2");
                Check(report, result, Mathf.Abs(Face(fixture.Increment).rectTransform.localScale.x - 1f) < 0.001f &&
                    Face(fixture.Increment).color.a > 0.99f,
                    "release returns to full-size hovered plus");

                Exit(fixture.Increment, pointer);
                Settle(fixture, 0.1f);
                frames.Add(Capture(report, result, fixture, camera, renderTexture, "leave-while-selected"));
                Check(report, result, EventSystem.current.currentSelectedGameObject == fixture.Increment.gameObject,
                    "keyboard focus survives pointer exit");
                Check(report, result, Face(fixture.Increment).color.a == 0f,
                    "selected plus does not remain black after pointer exit");

                for (int i = 0; i < 8; i++)
                {
                    Enter(fixture.Increment, pointer);
                    Settle(fixture, 0.0125f);
                    Exit(fixture.Increment, pointer);
                    Enter(fixture.Decrement, pointer);
                    Settle(fixture, 0.0125f);
                    Exit(fixture.Decrement, pointer);
                }
                Settle(fixture, 0.1f);
                Check(report, result, Face(fixture.Increment).color.a == 0f && Face(fixture.Decrement).color.a == 0f,
                    "rapid alternating hover leaves no sticky black cell");

                pointer = Pointer(fixture.Decrement);
                Enter(fixture.Decrement, pointer);
                Settle(fixture, 0.1f);
                Check(report, result, Face(fixture.Decrement).color.a > 0.99f && Face(fixture.Increment).color.a == 0f,
                    "enabled minus blacks out independently at quantity 2");
                Down(fixture.Decrement, pointer);
                ReleaseAndClick(fixture.Decrement, pointer);
                Settle(fixture, 0.1f);
                frames.Add(Capture(report, result, fixture, camera, renderTexture, "back-to-one-minus-disabled"));
                Check(report, result, fixture.QuantityValue == 1 && !fixture.Decrement.Button.IsInteractable(),
                    "one minus click returns to 1 and disables minus");
                Check(report, result, Face(fixture.Decrement).color.a == 0f &&
                    Mathf.Abs(Icon(fixture.Decrement).color.a - 0.28f) < 0.001f,
                    "newly disabled minus clears its face and returns to alpha 0.28");
                Down(fixture.Decrement, pointer);
                ReleaseAndClick(fixture.Decrement, pointer);
                Settle(fixture, 0.1f);
                Check(report, result, fixture.QuantityValue == 1, "disabled minus click does not change quantity");
                Exit(fixture.Decrement, pointer);

                pointer = Pointer(fixture.Increment);
                _setReducedMotion.Invoke(motion, new object[] { true });
                Enter(fixture.Increment, pointer);
                Check(report, result, Face(fixture.Increment).color.a > 0.99f,
                    "reduced motion applies hover without a transition");
                Down(fixture.Increment, pointer);
                Check(report, result, Mathf.Abs(Face(fixture.Increment).rectTransform.localScale.x - 0.88f) < 0.001f &&
                    Mathf.Abs(Icon(fixture.Increment).rectTransform.localScale.x - 0.88f) < 0.001f,
                    "reduced motion applies the pressed visual immediately");
                UpWithoutClick(fixture.Increment, pointer);
                Exit(fixture.Increment, pointer);
                Settle(fixture, 0.1f);
            }
            catch (Exception exception)
            {
                report.failures.Add(spec.Name + ": " + exception);
            }
            finally
            {
                if (fixture != null)
                    fixture.Dispose();
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].Texture != null)
                        UnityEngine.Object.DestroyImmediate(frames[i].Texture);
                }
            }
        }

        private static Fixture CreateFixture(CaseSpec spec, Transform parent, object motion)
        {
            Fixture fixture = new Fixture { QuantityValue = 1 };
            GameObject root = CreateRect("QuantityStepper_" + spec.Name, parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(spec.ButtonWidth * 2f + spec.ValueWidth, spec.ButtonHeight);
            Image paper = root.AddComponent<Image>();
            paper.color = Paper;
            paper.raycastTarget = true;
            fixture.Root = root;
            fixture.Decrement = CreateButton(root.transform, "DecrementButton", 0f, spec.ButtonWidth, spec.ButtonHeight, false);
            fixture.Quantity = CreateText(root.transform, spec.ButtonWidth, spec.ValueWidth, spec.ButtonHeight);
            fixture.Increment = CreateButton(
                root.transform,
                "IncrementButton",
                spec.ButtonWidth + spec.ValueWidth,
                spec.ButtonWidth,
                spec.ButtonHeight,
                true);
            fixture.Decrement.Button.onClick.AddListener(delegate { fixture.Change(-1); });
            fixture.Increment.Button.onClick.AddListener(delegate { fixture.Change(1); });
            fixture.Sync();
            _attachStepper.Invoke(null, new object[] { fixture.Decrement.Button, motion });
            _attachStepper.Invoke(null, new object[] { fixture.Increment.Button, motion });
            Canvas.ForceUpdateCanvases();
            return fixture;
        }

        private static ButtonParts CreateButton(
            Transform parent,
            string name,
            float left,
            float width,
            float height,
            bool plus)
        {
            GameObject root = CreateRect(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, height);
            Image hit = root.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.ColorTint;
            GameObject iconObject = CreateRect("Icon", root.transform);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(14f, 14f);
            Image icon = iconObject.AddComponent<Image>();
            icon.sprite = SymbolSprite.Create(plus);
            icon.color = Ink;
            icon.raycastTarget = false;
            return new ButtonParts(button, rect, icon);
        }

        private static Text CreateText(Transform parent, float left, float width, float height)
        {
            GameObject root = CreateRect("QuantityText", parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width, height);
            Text text = root.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = "1";
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Ink;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static Frame Capture(
            Round4StepperValidationReport report,
            Round4StepperCaseReport result,
            Fixture fixture,
            Camera camera,
            RenderTexture renderTexture,
            string state)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            string path = Path.Combine(report.outputPath, result.name + "-" + state + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Round4StepperPixelReport pixels = new Round4StepperPixelReport
            {
                state = state,
                image = path,
                faceSample = SampleFace(texture, fixture.Increment.Rect, camera)
            };
            DarkBounds(texture, fixture.Increment.Rect, camera, pixels);
            result.pixels.Add(pixels);
            return new Frame(texture, pixels);
        }

        private static Color32 SampleFace(Texture2D texture, RectTransform rect, Camera camera)
        {
            Rect local = rect.rect;
            Vector3 world = rect.TransformPoint(new Vector3(
                local.center.x,
                local.yMax - local.height * 0.1f,
                0f));
            Vector3 screen = camera.WorldToScreenPoint(world);
            return texture.GetPixel(
                Mathf.Clamp(Mathf.RoundToInt(screen.x), 0, texture.width - 1),
                Mathf.Clamp(Mathf.RoundToInt(screen.y), 0, texture.height - 1));
        }

        private static void DarkBounds(
            Texture2D texture,
            RectTransform rect,
            Camera camera,
            Round4StepperPixelReport result)
        {
            Rect pixel = PixelRect(texture, rect, camera);
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            int scanLeft = Mathf.Max(0, Mathf.FloorToInt(pixel.xMin + 0.5f));
            int scanRight = Mathf.Min(texture.width, Mathf.FloorToInt(pixel.xMax + 0.5f));
            int scanBottom = Mathf.Max(0, Mathf.FloorToInt(pixel.yMin + 0.5f));
            int scanTop = Mathf.Min(texture.height, Mathf.FloorToInt(pixel.yMax + 0.5f));
            for (int y = scanBottom; y < scanTop; y++)
            {
                for (int x = scanLeft; x < scanRight; x++)
                {
                    if (Luminance(texture.GetPixel(x, y)) >= 150f)
                        continue;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
            result.cellWidth = pixel.width;
            result.cellHeight = pixel.height;
            result.darkWidth = maxX < minX ? 0f : maxX - minX + 1;
            result.darkHeight = maxY < minY ? 0f : maxY - minY + 1;
        }

        private static int DifferentPixels(
            Texture2D left,
            Texture2D right,
            RectTransform rect,
            Camera camera)
        {
            Rect pixel = PixelRect(left, rect, camera);
            int different = 0;
            int scanLeft = Mathf.Max(0, Mathf.FloorToInt(pixel.xMin + 0.5f));
            int scanRight = Mathf.Min(left.width, Mathf.FloorToInt(pixel.xMax + 0.5f));
            int scanBottom = Mathf.Max(0, Mathf.FloorToInt(pixel.yMin + 0.5f));
            int scanTop = Mathf.Min(left.height, Mathf.FloorToInt(pixel.yMax + 0.5f));
            for (int y = scanBottom; y < scanTop; y++)
            {
                for (int x = scanLeft; x < scanRight; x++)
                {
                    if (left.GetPixel(x, y) != right.GetPixel(x, y))
                        different++;
                }
            }
            return different;
        }

        private static Rect PixelRect(Texture2D texture, RectTransform rect, Camera camera)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 bottomLeft = camera.WorldToScreenPoint(corners[0]);
            Vector3 topRight = camera.WorldToScreenPoint(corners[2]);
            return new Rect(
                Mathf.Clamp(bottomLeft.x, 0f, texture.width - 1f),
                Mathf.Clamp(bottomLeft.y, 0f, texture.height - 1f),
                Mathf.Clamp(topRight.x - bottomLeft.x, 1f, texture.width - 1f),
                Mathf.Clamp(topRight.y - bottomLeft.y, 1f, texture.height - 1f));
        }

        private static PointerEventData Pointer(ButtonParts button)
        {
            Vector3 screen = RectTransformUtility.WorldToScreenPoint(null, button.Rect.TransformPoint(button.Rect.rect.center));
            return new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = screen,
                pointerCurrentRaycast = new RaycastResult { gameObject = button.gameObject },
                pointerPressRaycast = new RaycastResult { gameObject = button.gameObject }
            };
        }

        private static void Enter(ButtonParts button, PointerEventData pointer)
        {
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        }

        private static void Exit(ButtonParts button, PointerEventData pointer)
        {
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        }

        private static void Down(ButtonParts button, PointerEventData pointer)
        {
            pointer.pointerPress = button.gameObject;
            pointer.rawPointerPress = button.gameObject;
            pointer.eligibleForClick = true;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        }

        private static void ReleaseAndClick(ButtonParts button, PointerEventData pointer)
        {
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            if (pointer.eligibleForClick)
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            pointer.eligibleForClick = false;
            pointer.pointerPress = null;
            pointer.rawPointerPress = null;
        }

        private static void UpWithoutClick(ButtonParts button, PointerEventData pointer)
        {
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            pointer.eligibleForClick = false;
            pointer.pointerPress = null;
            pointer.rawPointerPress = null;
        }

        private static void Settle(Fixture fixture, float duration)
        {
            Advance(fixture.Decrement.Button, duration);
            Advance(fixture.Increment.Button, duration);
        }

        private static void Advance(Button button, float duration)
        {
            Component state = button.GetComponent(_stateType);
            _apply.Invoke(state, null);
            object stepper = _stepperField.GetValue(state);
            _advance.Invoke(stepper, new object[] { duration });
        }

        private static Image Face(ButtonParts button)
        {
            Transform face = button.transform.Find("StepperFace");
            if (face == null)
                throw new InvalidOperationException("Missing StepperFace on " + button.name);
            return face.GetComponent<Image>();
        }

        private static Image Icon(ButtonParts button)
        {
            return button.transform.Find("Icon").GetComponent<Image>();
        }

        private static void InitializeReflection()
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetName().Name == "XingShangMod");
            if (assembly == null)
                throw new InvalidOperationException("XingShangMod runtime assembly is not loaded.");
            _stateType = assembly.GetType("XingShangMod.UI.Market.MarketIconInteractionState", true);
            _motionType = assembly.GetType("XingShangMod.UI.Infrastructure.UiMotion", true);
            Type visualType = assembly.GetType("XingShangMod.UI.Market.MarketStepperCellVisual", true);
            _attachStepper = _stateType.GetMethod("AttachStepper", BindingFlags.Static | BindingFlags.NonPublic);
            _apply = _stateType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic);
            _stepperField = _stateType.GetField("_stepper", BindingFlags.Instance | BindingFlags.NonPublic);
            _advance = visualType.GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            _setReducedMotion = _motionType.GetMethod("SetReducedMotion", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_attachStepper == null || _apply == null || _stepperField == null ||
                _advance == null || _setReducedMotion == null)
                throw new InvalidOperationException("Round 4 stepper reflection contract is incomplete.");
        }

        private static string ResolveOutputPath()
        {
            string configured = Environment.GetEnvironmentVariable("XINGSHANG_ROUND4_STEPPER_OUTPUT");
            if (!string.IsNullOrEmpty(configured))
                return Path.GetFullPath(configured);
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../../../.screenshots/market-review/round-4/stepper"));
        }

        private static bool SameCorners(Vector3[] expected, RectTransform actual)
        {
            Vector3[] corners = new Vector3[4];
            actual.GetWorldCorners(corners);
            for (int i = 0; i < 4; i++)
            {
                if ((expected[i] - corners[i]).sqrMagnitude > 0.0001f)
                    return false;
            }
            return true;
        }

        private static bool Ratio(float actual, float expected, float target)
        {
            return expected > 0f && Mathf.Abs(actual / expected - target) <= 0.06f;
        }

        private static float Luminance(Color32 color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        private static void Check(
            Round4StepperValidationReport report,
            Round4StepperCaseReport result,
            bool passed,
            string label)
        {
            result.checks.Add((passed ? "PASS " : "FAIL ") + label);
            if (!passed)
                report.failures.Add(result.name + ": " + label);
        }

        private sealed class CaseSpec
        {
            internal readonly string Name;
            internal readonly float ButtonWidth;
            internal readonly float ButtonHeight;
            internal readonly float ValueWidth;

            internal CaseSpec(string name, float buttonWidth, float buttonHeight, float valueWidth)
            {
                Name = name;
                ButtonWidth = buttonWidth;
                ButtonHeight = buttonHeight;
                ValueWidth = valueWidth;
            }
        }

        private sealed class ButtonParts
        {
            internal readonly Button Button;
            internal readonly RectTransform Rect;
            internal readonly Image SourceIcon;

            internal ButtonParts(Button button, RectTransform rect, Image sourceIcon)
            {
                Button = button;
                Rect = rect;
                SourceIcon = sourceIcon;
            }

            internal GameObject gameObject { get { return Button.gameObject; } }
            internal Transform transform { get { return Button.transform; } }
            internal string name { get { return Button.name; } }
        }

        private sealed class Fixture : IDisposable
        {
            internal GameObject Root;
            internal ButtonParts Decrement;
            internal ButtonParts Increment;
            internal Text Quantity;
            internal int QuantityValue;

            internal void Change(int delta)
            {
                QuantityValue = Mathf.Clamp(QuantityValue + delta, 1, 999);
                Sync();
            }

            internal void Sync()
            {
                Quantity.text = QuantityValue.ToString();
                Decrement.Button.interactable = QuantityValue > 1;
                Increment.Button.interactable = QuantityValue < 999;
            }

            public void Dispose()
            {
                if (Decrement != null && Decrement.SourceIcon != null && Decrement.SourceIcon.sprite != null)
                    SymbolSprite.Destroy(Decrement.SourceIcon.sprite);
                if (Increment != null && Increment.SourceIcon != null && Increment.SourceIcon.sprite != null)
                    SymbolSprite.Destroy(Increment.SourceIcon.sprite);
                if (Root != null)
                    UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class Frame
        {
            internal readonly Texture2D Texture;
            internal readonly Round4StepperPixelReport Pixels;

            internal Frame(Texture2D texture, Round4StepperPixelReport pixels)
            {
                Texture = texture;
                Pixels = pixels;
            }

            internal Color32 FaceSample { get { return Pixels.faceSample; } }
            internal float DarkWidth { get { return Pixels.darkWidth; } }
            internal float DarkHeight { get { return Pixels.darkHeight; } }
            internal float CellWidth { get { return Pixels.cellWidth; } }
            internal float CellHeight { get { return Pixels.cellHeight; } }
        }

        private static class SymbolSprite
        {
            internal static Sprite Create(bool plus)
            {
                Texture2D texture = new Texture2D(14, 14, TextureFormat.RGBA32, false);
                Color[] pixels = Enumerable.Repeat(Color.clear, 14 * 14).ToArray();
                for (int y = 0; y < 14; y++)
                {
                    for (int x = 0; x < 14; x++)
                    {
                        if ((y >= 6 && y <= 7) || (plus && x >= 6 && x <= 7))
                            pixels[y * 14 + x] = Color.white;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 14f, 14f), new Vector2(0.5f, 0.5f), 1f);
                sprite.name = plus ? "Round4Plus" : "Round4Minus";
                return sprite;
            }

            internal static void Destroy(Sprite sprite)
            {
                Texture2D texture = sprite.texture;
                UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    [InitializeOnLoad]
    public static class Round4StepperValidationBootstrap
    {
        static Round4StepperValidationBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += CompletePendingExit;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !SessionState.GetBool(Round4StepperValidation.PendingKey, false))
                return;
            SessionState.SetBool(Round4StepperValidation.PendingKey, false);
            Round4StepperValidation.RunFromPlayMode();
        }

        private static void CompletePendingExit()
        {
            int exitCode = SessionState.GetInt(Round4StepperValidation.ExitCodeKey, -1);
            if (exitCode < 0 || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            SessionState.SetInt(Round4StepperValidation.ExitCodeKey, -1);
            EditorApplication.Exit(exitCode);
        }
    }
}
