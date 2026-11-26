using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    internal static partial class UiBuilder
    {
        private const string PrefabRoot = "Assets/Prefabs";
        private const string BindingPath = "Assets/Binding/ui_map.json";
        private const string BundleOutput = "Build";
        private const string BundleName = "xingshang_ui.bundle";

        private static readonly string[] PrefabOrder =
        {
            "GoodsCard", "CartEntry", "InventorySlot", "RankingRow", "ChartHost", "NewsTicker",
            "Toast", "Tooltip", "Modal", "ItemDetail", "AssetsPage", "InventoryWindow", "MarketWindow",
            "FlyToken", "DealStamp"
        };

        private static readonly string[] ExplicitShaderPaths =
        {
            "Assets/Shaders/UI_PaperMultiply.shader"
        };

        private static UiMapBuilder currentMap;
        private static RectTransform currentRoot;

        [MenuItem("XingShang/Generate UI and Bundle")]
        public static void GenerateAll()
        {
            ConfigureProject();
            ValidateExplicitShaders();
            UiAssets.ConfigureAndValidate();
            GeneratePrefabs();
            ValidatePrefabs();
            ConfigurePhysicalPixelPrefabs();
            UiFonts.ValidatePrefabs(PrefabOrder.Select(name => PrefabRoot + "/" + name + ".prefab"));
            BuildBundle();
            Debug.Log("XingShang UI: generation complete. Bundle: " + Path.GetFullPath(Path.Combine(BundleOutput, BundleName)));
        }

        public static void BatchGenerate()
        {
            try
            {
                GenerateAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        internal static void Bind(string key, RectTransform target, string type)
        {
            if (currentMap == null || currentRoot == null)
            {
                throw new InvalidOperationException("Bindings can only be added while generating a prefab.");
            }

            currentMap.Add(key, currentRoot, target, type);
        }

        private static void ConfigureProject()
        {
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.companyName = "XingShang";
            PlayerSettings.productName = "Tangmen Exchange UI";
            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateExplicitShaders()
        {
            foreach (string path in ExplicitShaderPaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                    throw new InvalidOperationException("Explicit UI shader could not be imported: " + path);
                if (ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Explicit UI shader has compilation errors: " + path);
            }
        }

        private static void GeneratePrefabs()
        {
            Directory.CreateDirectory(PrefabRoot);
            Directory.CreateDirectory("Assets/Binding");
            List<UiPrefabMap> maps = new List<UiPrefabMap>();

            foreach (string name in PrefabOrder)
            {
                currentMap = new UiMapBuilder(name);
                currentRoot = null;
                RectTransform root = null;
                try
                {
                    root = CreatePrefab(name);
                    currentRoot = root;
                    currentMap.Add("Root", root, root, "RectTransform");
                    string path = PrefabRoot + "/" + name + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
                    maps.Add(currentMap.Build());
                    Debug.Log("XingShang UI: generated " + path);
                }
                finally
                {
                    if (root != null)
                    {
                        UnityEngine.Object.DestroyImmediate(root.gameObject);
                    }
                }
            }

            UiMapData map = new UiMapData
            {
                prefabs = maps.ToArray(),
                sprites = UiAssets.Entries.Select(entry => new UiAssetMap(entry.id, entry.id)).ToArray(),
                fonts = UiFonts.All.Select(definition => new UiAssetMap(definition.Key, definition.AssetName)).ToArray()
            };
            File.WriteAllText(BindingPath, JsonUtility.ToJson(map, true));
            AssetDatabase.ImportAsset(BindingPath, ImportAssetOptions.ForceSynchronousImport);
            currentMap = null;
            currentRoot = null;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static RectTransform CreatePrefab(string name)
        {
            switch (name)
            {
                case "GoodsCard": return BuildGoodsCard();
                case "CartEntry": return BuildCartEntry();
                case "InventorySlot": return BuildInventorySlot();
                case "RankingRow": return BuildRankingRow();
                case "ChartHost": return BuildChartHost();
                case "NewsTicker": return BuildNewsTicker();
                case "Toast": return BuildToast();
                case "Tooltip": return BuildTooltip();
                case "Modal": return BuildModal();
                case "ItemDetail": return BuildItemDetail();
                case "AssetsPage": return BuildAssetsPage();
                case "InventoryWindow": return BuildInventoryWindow();
                case "MarketWindow": return BuildMarketWindow();
                case "FlyToken": return BuildFlyToken();
                case "DealStamp": return BuildDealStamp();
                default: throw new ArgumentOutOfRangeException("name", name);
            }
        }

        private static void ConfigurePhysicalPixelPrefabs()
        {
            foreach (string name in PrefabOrder)
            {
                string path = PrefabRoot + "/" + name + ".prefab";
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CanvasScaler[] scalers = prefab.GetComponentsInChildren<CanvasScaler>(true);
                    foreach (CanvasScaler scaler in scalers)
                    {
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                        scaler.scaleFactor = 1f;
                    }
                    if (scalers.Length != 0)
                        PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static RectTransform CanvasRoot(string name, int sortingOrder = 0)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            RectTransform root = gameObject.GetComponent<RectTransform>();
            currentRoot = root;
            Canvas canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            return root;
        }

        private static RectTransform SimpleRoot(string name, float width, float height)
        {
            RectTransform root = UiElements.Rect(name, null);
            currentRoot = root;
            UiElements.Center(root, width, height);
            return root;
        }

        private static GameObject Instantiate(string prefabName, Transform parent)
        {
            string path = PrefabRoot + "/" + prefabName + ".prefab";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Prefab dependency missing: " + path);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Cannot instantiate " + path);
            }

            instance.transform.localScale = Vector3.one;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        private static void BuildBundle()
        {
            Directory.CreateDirectory(BundleOutput);
            List<string> assets = PrefabOrder.Select(name => PrefabRoot + "/" + name + ".prefab").ToList();
            assets.Add(BindingPath);
            assets.AddRange(UiFonts.All.Select(definition => definition.Path));
            assets.AddRange(UiFonts.All.Select(definition => definition.LicensePath).Distinct(StringComparer.Ordinal));
            assets.AddRange(UiAssets.Entries.Select(entry => UiAssets.SpritePath(entry.id)));

            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = assets.Distinct(StringComparer.Ordinal).ToArray()
            };
            BuildAssetBundleOptions options = BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression;
            AssetBundleManifest result = BuildPipeline.BuildAssetBundles(BundleOutput, new[] { build }, options, BuildTarget.StandaloneWindows64);
            if (result == null || !File.Exists(Path.Combine(BundleOutput, BundleName)))
            {
                throw new InvalidOperationException("AssetBundle build failed: " + BundleName);
            }

            ValidateBundle(Path.Combine(BundleOutput, BundleName));
        }

        private static void ValidateBundle(string path)
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                throw new InvalidOperationException("Cannot load generated AssetBundle: " + path);
            }

            try
            {
                TextAsset mapAsset = bundle.LoadAsset<TextAsset>("ui_map");
                UiMapData map = mapAsset == null ? null : JsonUtility.FromJson<UiMapData>(mapAsset.text);
                if (map == null || map.prefabs == null || map.prefabs.Length != PrefabOrder.Length)
                {
                    throw new InvalidOperationException("Bundle binding map is missing or incomplete.");
                }

                foreach (string name in PrefabOrder)
                {
                    GameObject prefab = bundle.LoadAsset<GameObject>(name);
                    if (prefab == null || prefab.name != name)
                    {
                        throw new InvalidOperationException("Bundle prefab missing: " + name);
                    }

                    CanvasScaler[] scalers = prefab.GetComponentsInChildren<CanvasScaler>(true);
                    foreach (CanvasScaler scaler in scalers)
                    {
                        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize ||
                            !Mathf.Approximately(scaler.scaleFactor, 1f))
                            throw new InvalidOperationException("Physical pixel CanvasScaler mismatch in " + name + ".");
                    }

                    UiPrefabMap prefabMap = map.prefabs.Single(entry => entry.name == name);
                    foreach (UiNodeMap node in prefabMap.nodes)
                    {
                        Transform target = string.IsNullOrEmpty(node.path) ? prefab.transform : prefab.transform.Find(node.path);
                        if (target == null)
                        {
                            throw new InvalidOperationException("Bundle binding path missing: " + name + ":" + node.key);
                        }
                    }

                    if (name == "InventoryWindow" && prefab.GetComponentsInChildren<Slider>(true).Length != 0)
                    {
                        throw new InvalidOperationException("InventoryWindow in bundle still contains a debug Slider.");
                    }
                }

                foreach (UiAssetMap sprite in map.sprites)
                {
                    if (bundle.LoadAsset<Sprite>(sprite.asset) == null)
                    {
                        throw new InvalidOperationException("Bundle sprite missing: " + sprite.asset);
                    }
                }

                UiFonts.ValidateBundle(bundle, map);

                Debug.Log(string.Format(
                    "XingShang UI: bundle reload passed for {0} prefabs, all binding paths, {1} sprites, ui_map, {2} fonts and their OFL licenses; no debug Slider present.",
                    PrefabOrder.Length, map.sprites.Length, UiFonts.All.Length));
            }
            finally
            {
                bundle.Unload(true);
            }
        }

        public static void BatchPreview()
        {
            try
            {
                CapturePreviews();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CapturePreviews()
        {
            Directory.CreateDirectory("Build/Previews");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.Ink;
            camera.orthographic = true;
            camera.cullingMask = -1;

            AssetBundle bundle = AssetBundle.LoadFromFile("Build/xingshang_ui.bundle");
            if (bundle == null)
            {
                throw new InvalidOperationException("Preview requires Build/xingshang_ui.bundle.");
            }

            try
            {
                CapturePrefab(bundle, "MarketWindow", camera, 1920, 1080, "Build/Previews/MarketWindow-1920x1080.png");
                CapturePrefab(bundle, "InventoryWindow", camera, 1366, 768, "Build/Previews/InventoryWindow-1366x768.png");
                CapturePrefab(bundle, "ItemDetail", camera, 1920, 1080, "Build/Previews/ItemDetail-1920x1080.png");
                CapturePrefab(bundle, "MarketWindow", camera, 2560, 1080, "Build/Previews/MarketWindow-2560x1080.png");
                CapturePrefab(bundle, "Modal", camera, 2560, 1080, "Build/Previews/Modal-2560x1080.png");
            }
            finally
            {
                bundle.Unload(true);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log("XingShang UI: preview capture from AssetBundle complete.");
        }

        private static void CapturePrefab(AssetBundle bundle, string name, Camera camera, int width, int height, string output)
        {
            GameObject asset = bundle.LoadAsset<GameObject>(name);
            GameObject instance = UnityEngine.Object.Instantiate(asset);
            instance.name = name;
            Canvas canvas = instance.GetComponent<Canvas>();
            if (canvas == null)
            {
                Canvas childCanvas = instance.AddComponent<Canvas>();
                childCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                childCanvas.worldCamera = camera;
                childCanvas.planeDistance = 1f;
                CanvasScaler scaler = instance.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = renderTexture;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(output, ImageConversion.EncodeToPNG(image));
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
