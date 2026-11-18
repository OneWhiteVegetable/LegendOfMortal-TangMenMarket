using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Mortal.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.UI.Charting;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Inventory;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI
{
    public sealed class ModUserInterface : IModUserInterface, IDisposable
    {
        private readonly Dictionary<string, ItemDefinition> _definitions =
            new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly HashSet<int> _menuFailures = new HashSet<int>();

        private ModApplication _application;
        private SpriteAssetLibrary _sprites;
        private UiFactory _factory;
        private Canvas _canvas;
        private ModUserInterfaceRuntime _runtime;
        private UiToast _toast;
        private MarketWindow _market;
        private InventoryWindow _inventory;
        private MarketEngine _subscribedMarket;
        private RunState _boundState;
        private NewsTicker _marketTicker;
        private AssetBar _inventoryAssetBar;
        private NewsTicker _inventoryTicker;
        private ButtonNavigation _inventoryNavigation;
        private NavigationTab _inventoryNavigationTab;
        private Button _nativeInventoryTab;
        private Button _nativeAssetsTab;
        private float _menuScanRemaining;
        private bool _sceneHookAttached;
        private bool _disposed;

        public ModUserInterface()
        {
        }

        public ModUserInterface(ModApplication application)
        {
            Attach(application);
        }

        public Canvas Canvas { get { return _canvas; } }
        public bool IsAlive { get { return !_disposed && _canvas != null; } }
        public bool IsOpen
        {
            get
            {
                return (_market != null && _market.IsOpen) ||
                    (_inventory != null && _inventory.IsOpen);
            }
        }
        public Action<string> LogWarning { get; set; }

        public void Attach(ModApplication application)
        {
            ThrowIfDisposed();
            if (application == null)
            {
                throw new ArgumentNullException("application");
            }
            if (ReferenceEquals(_application, application))
            {
                EnsureCanvas();
                return;
            }

            CloseAll();
            UnsubscribeMarket();
            _application = application;
            _definitions.Clear();
            if (application.Items != null)
            {
                for (int i = 0; i < application.Items.Items.Count; i++)
                {
                    ItemDefinition item = application.Items.Items[i];
                    if (item != null && !_definitions.ContainsKey(item.ItemId))
                    {
                        _definitions.Add(item.ItemId, item);
                    }
                }
            }
            EnsureCanvas();
            AttachSceneHook();
            Refresh();
        }

        public void OpenMarket()
        {
            if (!EnsureReady())
            {
                return;
            }
            EnsureEventSystem();
            if (_inventory != null)
            {
                _inventory.Hide();
            }
            RefreshShared();
            _market.Show();
        }

        public void OpenReadonlyInventory()
        {
            if (!EnsureReady())
            {
                return;
            }
            EnsureEventSystem();
            if (_market != null)
            {
                _market.Hide();
            }
            RefreshShared();
            _inventoryAssetBar.SetIndexInteractive(false);
            _inventoryNavigation.SetCurrent(_inventoryNavigationTab);
            _inventory.Show();
            SelectInventoryNavigation();
        }

        public void CloseAll()
        {
            HideWindows();
            if (_toast != null)
            {
                _toast.Clear();
            }
        }

        public void Refresh()
        {
            if (_disposed)
            {
                return;
            }
            EnsureCanvas();
            if (_application == null || !_application.IsEnabled)
            {
                HideWindows();
                return;
            }
            if (_application.State == null)
            {
                return;
            }
            if (!ReferenceEquals(_boundState, _application.State) || !WindowsAreAlive())
            {
                RebuildWindows();
            }
            if (_market != null)
            {
                _market.Refresh();
            }
            if (_inventory != null)
            {
                _inventory.Refresh();
            }
            RefreshShared();
        }

        public void AttachSystemMenu(MenuPanel panel)
        {
            if (_disposed || panel == null)
            {
                return;
            }
            EnsureCanvas();
            Button entry = SystemMenuEntry.EnsureCreated(
                panel,
                OpenReadonlyInventory,
                CloseAll,
                SetFont);
            if (entry != null)
            {
                SystemMenuEntry.ScheduleReposition(panel);
            }
        }

        public void SetFont(Font font)
        {
            if (font == null || _canvas == null || (_factory != null && _factory.GameFont == font))
            {
                return;
            }
            if (_factory != null)
            {
                _factory.GameFont = font;
            }
            Text[] texts = _canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
            WeeklyChartGraphic[] charts = _canvas.GetComponentsInChildren<WeeklyChartGraphic>(true);
            for (int i = 0; i < charts.Length; i++)
            {
                charts[i].LabelFont = font;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            UnsubscribeMarket();
            if (_sceneHookAttached)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _sceneHookAttached = false;
            }
            HideWindows();
            if (_market != null)
            {
                _market.Dispose();
                _market = null;
            }
            if (_inventory != null && _inventory.Root != null)
            {
                UnityEngine.Object.Destroy(_inventory.Root.gameObject);
            }
            _inventory = null;
            if (_runtime != null)
            {
                _runtime.Owner = null;
            }
            if (_canvas != null)
            {
                SafeTween.Kill(_canvas.gameObject);
                UnityEngine.Object.Destroy(_canvas.gameObject);
            }
            _canvas = null;
            if (_sprites != null)
            {
                _sprites.Dispose();
                _sprites = null;
            }
            _application = null;
            _boundState = null;
        }

        private bool EnsureReady()
        {
            if (_disposed || _application == null || !_application.IsEnabled || _application.State == null)
            {
                return false;
            }
            EnsureCanvas();
            EnsureEventSystem();
            if (!ReferenceEquals(_boundState, _application.State) || !WindowsAreAlive())
            {
                RebuildWindows();
            }
            return _market != null && _inventory != null;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                return;
            }
            if (_sprites == null)
            {
                string pluginRoot = Path.Combine(Paths.PluginPath, "XingShangMod");
                _sprites = new SpriteAssetLibrary(pluginRoot);
                _sprites.LogWarning = ReportWarning;
            }
            _factory = new UiFactory(_sprites, FindGameFont());
            _canvas = _factory.CreateCanvas();
            _runtime = _canvas.gameObject.AddComponent<ModUserInterfaceRuntime>();
            _runtime.Owner = this;
            _toast = UiToast.Create(_factory, _canvas.transform);
            _market = null;
            _inventory = null;
            _boundState = null;
            _marketTicker = null;
            _inventoryAssetBar = null;
            _inventoryTicker = null;
            _inventoryNavigation = null;
        }

        private void RebuildWindows()
        {
            HideWindows();
            UnsubscribeMarket();
            if (_market != null)
            {
                _market.Dispose();
            }
            if (_inventory != null && _inventory.Root != null)
            {
                UnityEngine.Object.Destroy(_inventory.Root.gameObject);
            }
            _market = null;
            _inventory = null;
            _marketTicker = null;
            _inventoryAssetBar = null;
            _inventoryTicker = null;
            _inventoryNavigation = null;
            _nativeInventoryTab = null;
            _nativeAssetsTab = null;

            _boundState = _application.State;
            _market = new MarketWindow(
                _canvas.transform,
                _factory,
                _boundState,
                _application.Items,
                _application.Trading,
                _application.Capacity,
                OnMarketChanged);
            BuildMarketTicker();

            _inventory = new InventoryWindow(
                _canvas.transform,
                _factory,
                _boundState,
                _application.Items,
                _application.Shorts,
                _application.Assets,
                _application.Capacity);
            BuildInventoryChrome();

            _subscribedMarket = _application.Market;
            if (_subscribedMarket != null)
            {
                _subscribedMarket.WeekAdvanced += OnMarketWeekAdvanced;
            }
            _inventoryNavigationTab = NavigationTab.Inventory;
            RefreshShared();
        }

        private void BuildMarketTicker()
        {
            Transform marketRoot = null;
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _canvas.transform.GetChild(i);
                if (child.name == "XingShangMod.MarketWindow")
                {
                    marketRoot = child;
                    break;
                }
            }
            if (marketRoot == null)
            {
                throw new InvalidOperationException("The market window root was not created.");
            }
            Transform oldTicker = marketRoot.Find("LatestNews");
            int siblingIndex = oldTicker == null ? 1 : oldTicker.GetSiblingIndex();
            _marketTicker = NewsTicker.Create(_factory, marketRoot);
            _marketTicker.Root.SetSiblingIndex(siblingIndex);
            if (oldTicker != null)
            {
                oldTicker.gameObject.SetActive(false);
            }
        }

        private void BuildInventoryChrome()
        {
            Transform paper = _inventory.Root.Find("PaperWindow");
            if (paper == null)
            {
                throw new InvalidOperationException("The inventory paper window was not created.");
            }
            Transform oldHeader = paper.Find("Header");
            if (oldHeader == null)
            {
                throw new InvalidOperationException("The inventory header was not created.");
            }
            _nativeInventoryTab = FindButton(oldHeader, "InventoryTab");
            _nativeAssetsTab = FindButton(oldHeader, "AssetsTab");

            _inventoryAssetBar = AssetBar.Create(_factory, paper);
            _inventoryAssetBar.Root.SetSiblingIndex(oldHeader.GetSiblingIndex());
            _inventoryAssetBar.SetIndexInteractive(false);
            _inventoryNavigation = ButtonNavigation.CreateReadonly(
                _factory,
                _inventoryAssetBar.Root,
                ShowReadonlyInventoryTab,
                ShowReadonlyAssetsTab,
                CloseAll);
            _inventoryAssetBar.DockNavigation(_inventoryNavigation);
            _inventoryNavigation.SetCurrent(NavigationTab.Inventory);

            _inventoryTicker = NewsTicker.Create(_factory, paper);
            _inventoryTicker.Root.SetSiblingIndex(oldHeader.GetSiblingIndex() + 1);
            RectTransform content = paper.Find("Content") as RectTransform;
            if (content != null)
            {
                content.offsetMax = new Vector2(content.offsetMax.x, -150f);
            }
            oldHeader.gameObject.SetActive(false);
        }

        private void ShowReadonlyInventoryTab()
        {
            _inventoryNavigationTab = NavigationTab.Inventory;
            if (_inventoryNavigation != null)
            {
                _inventoryNavigation.SetCurrent(NavigationTab.Inventory);
            }
            if (_nativeInventoryTab != null)
            {
                _nativeInventoryTab.onClick.Invoke();
            }
            SelectInventoryNavigation();
        }

        private void ShowReadonlyAssetsTab()
        {
            _inventoryNavigationTab = NavigationTab.Assets;
            if (_inventoryNavigation != null)
            {
                _inventoryNavigation.SetCurrent(NavigationTab.Assets);
            }
            if (_nativeAssetsTab != null)
            {
                _nativeAssetsTab.onClick.Invoke();
            }
            SelectInventoryNavigation();
        }

        private void SelectInventoryNavigation()
        {
            if (_inventoryNavigation == null || EventSystem.current == null)
            {
                return;
            }
            Button selected = _inventoryNavigationTab == NavigationTab.Assets
                ? _inventoryNavigation.AssetsButton
                : _inventoryNavigation.InventoryButton;
            if (selected != null)
            {
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }
        }

        private void RefreshShared()
        {
            if (_application == null || _application.State == null)
            {
                return;
            }
            RunState state = _application.State;
            if (_marketTicker != null)
            {
                _marketTicker.SetNews(state, _definitions);
            }
            if (_inventoryTicker != null)
            {
                _inventoryTicker.SetNews(state, _definitions);
            }
            if (_inventoryAssetBar != null)
            {
                if (_application.Assets != null)
                {
                    _inventoryAssetBar.SetAssets(_application.Assets.GetSnapshot());
                }
                WeeklyPoint current = GetCurrentIndexPoint(state);
                _inventoryAssetBar.SetIndex(
                    current == null ? state.CurrentIndex : current.Price,
                    current == null ? 0.0 : current.Return);
                _inventoryAssetBar.SetIndexInteractive(false);
            }
        }

        private void OnMarketChanged()
        {
            if (_application != null && _application.Assets != null)
            {
                _application.Assets.CaptureTradeSnapshot();
            }
            Refresh();
        }

        private void OnMarketWeekAdvanced(MarketWeekChange change)
        {
            if (change == null || change.NewlyDelistedItemIds == null || change.NewlyDelistedItemIds.Count == 0)
            {
                return;
            }
            List<string> names = new List<string>();
            for (int i = 0; i < change.NewlyDelistedItemIds.Count; i++)
            {
                string itemId = change.NewlyDelistedItemIds[i];
                ItemDefinition item;
                names.Add(_definitions.TryGetValue(itemId, out item) ? item.Name : itemId);
            }
            if (_toast != null)
            {
                _toast.Show(string.Join("、", names.ToArray()) + " 已退市，持倉與借貨批次凍結。", 4f);
            }
        }

        internal void Tick()
        {
            if (_disposed)
            {
                return;
            }
            _menuScanRemaining -= Time.unscaledDeltaTime;
            if (_menuScanRemaining <= 0f)
            {
                _menuScanRemaining = 0.6f;
                EnsureSystemMenuEntries();
            }
            if (!IsOpen)
            {
                return;
            }
            EnsureEventSystem();
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            bool cancelButton = Input.GetButtonDown("Cancel");
            if (!escape && !cancelButton)
            {
                return;
            }
            if (HasOpenModal())
            {
                if (cancelButton && !escape)
                {
                    CancelTopModal();
                }
                return;
            }
            if (!WindowHandlesCancel(escape))
            {
                CloseAll();
            }
        }

        private bool WindowHandlesCancel(bool escape)
        {
            if (escape && _market != null && _market.IsOpen && _marketTicker != null)
            {
                Transform root = _marketTicker.Root.parent;
                MarketWindowLifecycle input = root == null ? null : root.GetComponent<MarketWindowLifecycle>();
                if (input != null && input.isActiveAndEnabled)
                {
                    return true;
                }
            }
            if (_inventory != null && _inventory.IsOpen)
            {
                InventoryWindowInput input = _inventory.Root.GetComponent<InventoryWindowInput>();
                if (input != null && input.isActiveAndEnabled)
                {
                    return true;
                }
            }
            return false;
        }

        private void CancelTopModal()
        {
            UiModal[] modals = _canvas.GetComponentsInChildren<UiModal>(false);
            for (int i = modals.Length - 1; i >= 0; i--)
            {
                if (modals[i].IsOpen)
                {
                    modals[i].Cancel();
                    return;
                }
            }
        }

        private void EnsureSystemMenuEntries()
        {
            if (_application == null || !_application.IsEnabled)
            {
                return;
            }
            MenuPanel[] panels = Resources.FindObjectsOfTypeAll<MenuPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                MenuPanel panel = panels[i];
                if (panel == null || !panel.gameObject.scene.IsValid())
                {
                    continue;
                }
                int id = panel.GetInstanceID();
                if (_menuFailures.Contains(id))
                {
                    continue;
                }
                try
                {
                    AttachSystemMenu(panel);
                }
                catch (Exception exception)
                {
                    _menuFailures.Add(id);
                    ReportWarning("System menu entry failed: " + exception.Message);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>();
            }
            if (eventSystem == null)
            {
                GameObject root = new GameObject("XingShangMod.EventSystem", typeof(EventSystem));
                eventSystem = root.GetComponent<EventSystem>();
            }
            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
        }

        private bool HasOpenModal()
        {
            if (_canvas == null)
            {
                return false;
            }
            UiModal[] modals = _canvas.GetComponentsInChildren<UiModal>(false);
            for (int i = 0; i < modals.Length; i++)
            {
                if (modals[i].IsOpen)
                {
                    return true;
                }
            }
            return false;
        }

        private bool WindowsAreAlive()
        {
            return _market != null && _marketTicker != null && _inventory != null && _inventory.Root != null &&
                _inventoryAssetBar != null && _inventoryTicker != null && _inventoryNavigation != null;
        }

        private void HideWindows()
        {
            if (_market != null)
            {
                _market.Hide();
            }
            if (_inventory != null)
            {
                _inventory.Hide();
            }
        }

        private void UnsubscribeMarket()
        {
            if (_subscribedMarket != null)
            {
                _subscribedMarket.WeekAdvanced -= OnMarketWeekAdvanced;
                _subscribedMarket = null;
            }
        }

        private void AttachSceneHook()
        {
            if (_sceneHookAttached)
            {
                return;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHookAttached = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CloseAll();
            _menuFailures.Clear();
            _menuScanRemaining = 0f;
        }

        private Font FindGameFont()
        {
            Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null && text.font != null && (_canvas == null || text.GetComponentInParent<Canvas>() != _canvas))
                {
                    return text.font;
                }
            }
            return null;
        }

        private static WeeklyPoint GetCurrentIndexPoint(RunState state)
        {
            WeeklyPoint current = null;
            if (state.IndexHistory == null)
            {
                return null;
            }
            int currentWeek = state.Clock == null ? 0 : state.Clock.MarketWeekIndex;
            for (int i = 0; i < state.IndexHistory.Count; i++)
            {
                WeeklyPoint point = state.IndexHistory[i];
                if (point != null && point.MarketWeekIndex <= currentWeek &&
                    (current == null || point.MarketWeekIndex >= current.MarketWeekIndex))
                {
                    current = point;
                }
            }
            return current;
        }

        private static Button FindButton(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child == null ? null : child.GetComponent<Button>();
        }

        private void ReportWarning(string message)
        {
            Action<string> logger = LogWarning;
            if (logger != null)
            {
                logger("[XingShangMod.UI] " + message);
            }
            else
            {
                Debug.LogWarning("[XingShangMod.UI] " + message);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }

    internal sealed class ModUserInterfaceRuntime : MonoBehaviour
    {
        internal ModUserInterface Owner;

        private void Update()
        {
            if (Owner != null)
            {
                Owner.Tick();
            }
        }

        private void OnDestroy()
        {
            ModUserInterface owner = Owner;
            Owner = null;
            if (owner != null)
            {
                owner.Dispose();
            }
        }
    }
}
