using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Mortal.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XingShangMod.App;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.UI.Infrastructure;
using XingShangMod.UI.Inventory;
using XingShangMod.UI.Market;
using XingShangMod.UI.Shared;

namespace XingShangMod.UI
{
    public sealed class ModUserInterface : IModUserInterface, IDisposable
    {
        private enum NavigationScreen
        {
            Market,
            Detail,
            Inventory
        }

        private struct NavigationFrame
        {
            internal NavigationScreen Screen;
            internal bool ShowRanking;
            internal string ItemId;
            internal bool IndexMode;

            internal static NavigationFrame Market(bool showRanking)
            {
                return new NavigationFrame
                {
                    Screen = NavigationScreen.Market,
                    ShowRanking = showRanking
                };
            }

            internal static NavigationFrame Detail(string itemId, bool indexMode)
            {
                return new NavigationFrame
                {
                    Screen = NavigationScreen.Detail,
                    ItemId = itemId,
                    IndexMode = indexMode
                };
            }

            internal static NavigationFrame Inventory()
            {
                return new NavigationFrame { Screen = NavigationScreen.Inventory };
            }
        }

        private enum UiCloseReason
        {
            HostRequest,
            UserRequest,
            ApplicationChanged,
            ApplicationUnavailable,
            StateReplaced,
            SceneChanged,
            RefreshFailed,
            OpenFailed,
            Disposed
        }

        private static global::XingShangMod.Plugin _loggerOwner;
        private static ManualLogSource _pluginLogger;
        private readonly HashSet<int> _menuFailures = new HashSet<int>();
        private readonly List<NavigationFrame> _navigation = new List<NavigationFrame>();
        private ModApplication _application;
        private GameObject _runtimeObject;
        private ModUserInterfaceRuntime _runtime;
        private UiAssetBundle _assets;
        private UiInputStack _input;
        private UiMotion _motion;
        private UiFrontendServices _services;
        private UiOverlayService _overlays;
        private TradeConfirmation _confirmation;
        private MarketWindow _market;
        private MarketItemDetail _detail;
        private InventoryWindow _inventory;
        private MarketEngine _subscribedMarket;
        private RunState _boundState;
        private Action _marketClosed;
        private bool _marketSessionActive;
        private bool _userClosePending;
        private int _closeGeneration;
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

        public bool IsAlive { get { return !_disposed && _runtimeObject != null; } }
        public bool IsMarketOpen { get { return _market != null && _market.IsOpen; } }
        public bool IsOpen
        {
            get
            {
                return IsMarketOpen || (_detail != null && _detail.IsOpen) ||
                    (_inventory != null && _inventory.IsOpen);
            }
        }
        public Action<string> LogWarning { get; set; }

        public void Attach(ModApplication application)
        {
            ThrowIfDisposed();
            if (application == null)
                throw new ArgumentNullException("application");
            if (!ReferenceEquals(_application, application))
            {
                CloseAll(UiCloseReason.ApplicationChanged);
                DisposeFrontend();
                _application = application;
            }
            EnsureRuntime();
            AttachSceneHook();
            Refresh();
        }

        public bool TryOpenMarket(Action onClosed)
        {
            if (_disposed || _application == null || !_application.IsEnabled || _application.State == null ||
                _marketSessionActive || _userClosePending || IsOpen)
                return false;
            try
            {
                if (!EnsureReady())
                    return false;
                _marketClosed = onClosed;
                _marketSessionActive = true;
                _navigation.Clear();
                _market.ShowMarket();
                if (!_market.IsOpen)
                    throw new InvalidOperationException("The prefab market window did not open.");
                return true;
            }
            catch (Exception exception)
            {
                ReportWarning("Market open failed; the original merchant flow remains available: " + exception);
                CloseAll(UiCloseReason.OpenFailed);
                DisposeFrontend();
                return false;
            }
        }

        public void OpenReadonlyInventory()
        {
            if (_disposed || _application == null || !_application.IsEnabled || _application.State == null ||
                _marketSessionActive || _userClosePending || IsMarketOpen || (_detail != null && _detail.IsOpen))
                return;
            try
            {
                if (EnsureReady())
                {
                    _navigation.Clear();
                    _inventory.Show(false);
                }
            }
            catch (Exception exception)
            {
                ReportWarning("Readonly inventory open failed: " + exception);
                CloseAll(UiCloseReason.OpenFailed);
            }
        }

        public void CloseAll()
        {
            CloseAll(UiCloseReason.HostRequest);
        }

        private void CloseAll(UiCloseReason reason)
        {
            if (reason == UiCloseReason.UserRequest && TryBeginUserClose())
                return;
            ForceClose(reason);
        }

        private bool TryBeginUserClose()
        {
            if (_userClosePending)
                return true;
            UiScreen active = GetActiveWindow();
            if (active == null)
                return false;

            _userClosePending = true;
            int generation = ++_closeGeneration;
            _navigation.Clear();
            if (_overlays != null)
                _overlays.CloseAll();
            if (_input != null)
                _input.CloseAll();
            try
            {
                active.Hide(delegate
                {
                    if (!_userClosePending || generation != _closeGeneration)
                        return;
                    FinishClose(UiCloseReason.UserRequest);
                });
            }
            catch (Exception exception)
            {
                ReportWarning("Animated market close failed; forcing cleanup: " + exception);
                ForceClose(UiCloseReason.UserRequest);
            }
            return true;
        }

        private void ForceClose(UiCloseReason reason)
        {
            _closeGeneration++;
            _userClosePending = false;
            _navigation.Clear();
            if (_overlays != null)
                _overlays.CloseAll();
            if (_input != null)
                _input.CloseAll();
            DisposeWindows();
            FinishClose(reason);
        }

        private void FinishClose(UiCloseReason reason)
        {
            _closeGeneration++;
            _userClosePending = false;
            _navigation.Clear();
            bool notify = _marketSessionActive && reason != UiCloseReason.OpenFailed;
            _marketSessionActive = false;
            Action callback = _marketClosed;
            _marketClosed = null;
            if (notify && callback != null)
            {
                try
                {
                    callback();
                }
                catch (Exception exception)
                {
                    ReportWarning("Market close callback failed (" + reason + "): " + exception);
                }
            }
        }

        private UiScreen GetActiveWindow()
        {
            if (_inventory != null && _inventory.IsOpen)
                return _inventory;
            if (_detail != null && _detail.IsOpen)
                return _detail;
            if (_market != null && _market.IsOpen)
                return _market;
            return null;
        }

        private void HideWindows()
        {
            if (_inventory != null)
                _inventory.Hide();
            if (_detail != null)
                _detail.Hide();
            if (_market != null)
                _market.Hide();
        }

        public void Refresh()
        {
            if (_disposed)
                return;
            if (_application == null || !_application.IsEnabled || _application.State == null)
            {
                CloseAll(UiCloseReason.ApplicationUnavailable);
                return;
            }
            if (_boundState == null && _market == null && _inventory == null && _detail == null)
                return;
            try
            {
                if (!ReferenceEquals(_boundState, _application.State) || !WindowsAreAlive())
                {
                    if (!EnsureReady())
                        return;
                }
                if (_market != null && _market.IsOpen)
                    _market.Refresh();
                if (_detail != null && _detail.IsOpen)
                    _detail.Refresh();
                if (_inventory != null && _inventory.IsOpen)
                    _inventory.Refresh();
            }
            catch (Exception exception)
            {
                ReportWarning("UI refresh failed: " + exception);
                CloseAll(UiCloseReason.RefreshFailed);
            }
        }

        public void AttachSystemMenu(MenuPanel panel)
        {
            AttachSystemMenuCore(panel);
        }

        private void AttachSystemMenuCore(MenuPanel panel)
        {
            if (_disposed || panel == null)
                return;
            EnsureRuntime();
            Button entry = SystemMenuEntry.EnsureCreated(
                panel,
                OpenReadonlyInventory,
                CloseAll,
                null);
            if (entry != null)
                SystemMenuEntry.ScheduleReposition(panel);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_sceneHookAttached)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                _sceneHookAttached = false;
            }
            CloseAll(UiCloseReason.Disposed);
            DisposeFrontend();
            if (_runtime != null)
                _runtime.Owner = null;
            if (_runtimeObject != null)
                UnityEngine.Object.Destroy(_runtimeObject);
            _runtime = null;
            _runtimeObject = null;
            _application = null;
            _boundState = null;
            _menuFailures.Clear();
        }

        private bool EnsureReady()
        {
            if (_disposed || _application == null || !_application.IsEnabled || _application.State == null)
                return false;
            EnsureRuntime();
            if (_assets == null)
            {
                _assets = new UiAssetBundle();
                _input = new UiInputStack();
                _motion = new UiMotion();
                _services = new UiFrontendServices(_assets, _input, _motion);
                _overlays = new UiOverlayService(_services);
                _confirmation = new TradeConfirmation(_application, _services);
            }
            if (!ReferenceEquals(_boundState, _application.State) || !WindowsAreAlive())
                RebuildWindows();
            return WindowsAreAlive();
        }

        private void RebuildWindows()
        {
            CloseAll(UiCloseReason.StateReplaced);
            DisposeWindows();
            _market = new MarketWindow(_services, _application, _confirmation);
            _market.DetailRequested += OpenDetail;
            _market.IndexRequested += OpenIndex;
            _market.InventoryRequested += OpenMarketInventory;
            _market.CloseRequested += CloseFromUser;
            _detail = new MarketItemDetail(_services, _application, _market.Cart, () => _marketSessionActive);
            _detail.BackRequested += ReturnToMarket;
            _detail.CloseRequested += CloseDetail;
            _detail.InventoryRequested += OpenDetailInventory;
            _detail.RankingRequested += ReturnToRanking;
            _detail.AddRequested += AddFromDetail;
            _detail.ConstituentRequested += OpenConstituent;
            _inventory = new InventoryWindow(_services, _application, _confirmation);
            _inventory.CloseRequested += CloseInventory;
            _inventory.DetailRequested += OpenInventoryDetail;
            _inventory.IndexRequested += OpenInventoryIndex;
            _inventory.RankingRequested += OpenInventoryRanking;
            _boundState = _application.State;
            _subscribedMarket = _application.Market;
            if (_subscribedMarket != null)
                _subscribedMarket.WeekAdvanced += OnMarketWeekAdvanced;
        }

        private void CloseFromUser()
        {
            CloseAll(UiCloseReason.UserRequest);
        }

        private void OpenDetail(string itemId)
        {
            if (!_marketSessionActive || _market == null || !_market.IsOpen)
                return;
            _navigation.Add(NavigationFrame.Market(_market.IsRanking));
            _market.Hide();
            _detail.Show(itemId);
        }

        private void OpenIndex()
        {
            if (!_marketSessionActive || _market == null || !_market.IsOpen)
                return;
            _navigation.Add(NavigationFrame.Market(_market.IsRanking));
            _market.Hide();
            _detail.ShowIndex();
        }

        private void OpenConstituent(string itemId)
        {
            if (!_marketSessionActive || _detail == null || !_detail.IsOpen || string.IsNullOrEmpty(itemId))
                return;
            NavigationFrame frame = CaptureDetail();
            if (!frame.IndexMode)
                return;
            _navigation.Add(frame);
            _detail.Show(itemId);
        }

        private void ReturnToMarket()
        {
            GoBack();
        }

        private void CloseDetail()
        {
            if (_marketSessionActive)
                CloseFromUser();
            else
                GoBack();
        }

        private void ReturnToRanking()
        {
            ShowRootRanking();
        }

        private void OpenMarketInventory()
        {
            if (!_marketSessionActive || _market == null || !_market.IsOpen)
                return;
            _navigation.Add(NavigationFrame.Market(_market.IsRanking));
            _market.Hide();
            _inventory.Show(true);
        }

        private void OpenDetailInventory()
        {
            if (!_marketSessionActive || _detail == null || !_detail.IsOpen)
                return;
            _navigation.Add(CaptureDetail());
            _detail.Hide();
            _inventory.Show(true);
        }

        private void CloseInventory()
        {
            if (_inventory == null)
                return;
            if (!_marketSessionActive)
            {
                CloseAll(UiCloseReason.UserRequest);
                return;
            }
            GoBack();
        }

        private void OpenInventoryDetail(string itemId)
        {
            if (_inventory == null || !_inventory.IsOpen || _userClosePending ||
                _inventory.IsInteractive != _marketSessionActive || !_detail.HasPosition(itemId))
                return;
            if (!_marketSessionActive)
            {
                _inventory.Hide();
                _detail.ShowHolding(itemId);
                return;
            }
            _navigation.Add(NavigationFrame.Inventory());
            _inventory.Hide();
            _detail.Show(itemId);
        }

        private void OpenInventoryIndex()
        {
            if (!_marketSessionActive || _inventory == null || !_inventory.IsOpen || !_inventory.IsInteractive)
                return;
            _navigation.Add(NavigationFrame.Inventory());
            _inventory.Hide();
            _detail.ShowIndex();
        }

        private void OpenInventoryRanking()
        {
            if (!_marketSessionActive || _inventory == null || !_inventory.IsOpen || !_inventory.IsInteractive)
                return;
            ShowRootRanking();
        }

        private void GoBack()
        {
            if (!_marketSessionActive)
            {
                if (_detail != null && _detail.IsOpen && !_userClosePending)
                {
                    _detail.Hide();
                    _inventory.Show(false);
                }
                return;
            }
            NavigationFrame frame = NavigationFrame.Market(false);
            if (_navigation.Count > 0)
            {
                int index = _navigation.Count - 1;
                frame = _navigation[index];
                _navigation.RemoveAt(index);
            }
            HideWindows();
            Restore(frame);
        }

        private NavigationFrame CaptureDetail()
        {
            return NavigationFrame.Detail(_detail.ItemId, _detail.IndexMode);
        }

        private void ShowRootRanking()
        {
            if (!_marketSessionActive)
                return;
            _navigation.Clear();
            HideWindows();
            _market.ShowRanking();
        }

        private void Restore(NavigationFrame frame)
        {
            switch (frame.Screen)
            {
                case NavigationScreen.Detail:
                    if (frame.IndexMode)
                        _detail.ShowIndex();
                    else if (!string.IsNullOrEmpty(frame.ItemId))
                        _detail.Show(frame.ItemId);
                    else
                        _detail.Resume();
                    break;
                case NavigationScreen.Inventory:
                    _inventory.Show(true);
                    break;
                default:
                    if (frame.ShowRanking)
                        _market.ShowRanking();
                    else
                        _market.ShowMarket();
                    break;
            }
        }

        private void AddFromDetail(string itemId, Trading.TradeAction action, int quantity, RectTransform sourceRect)
        {
            if (!_marketSessionActive || _market == null || !_detail.IsOpen || _detail.IndexMode ||
                !string.Equals(itemId, _detail.ItemId, StringComparison.Ordinal))
                return;
            _market.AddFromDetail(itemId, action, quantity, sourceRect);
        }

        private void EnsureRuntime()
        {
            if (_runtimeObject != null)
                return;
            _runtimeObject = new GameObject("XingShangMod.UI.Host");
            UnityEngine.Object.DontDestroyOnLoad(_runtimeObject);
            _runtime = _runtimeObject.AddComponent<ModUserInterfaceRuntime>();
            _runtime.Owner = this;
        }

        private bool WindowsAreAlive()
        {
            return _market != null && _market.Root != null && _detail != null && _detail.Root != null &&
                _inventory != null && _inventory.Root != null;
        }

        private void DisposeWindows()
        {
            UnsubscribeMarket();
            if (_market != null)
            {
                _market.DetailRequested -= OpenDetail;
                _market.IndexRequested -= OpenIndex;
                _market.InventoryRequested -= OpenMarketInventory;
                _market.CloseRequested -= CloseFromUser;
                _market.Dispose();
            }
            if (_detail != null)
            {
                _detail.BackRequested -= ReturnToMarket;
                _detail.CloseRequested -= CloseDetail;
                _detail.InventoryRequested -= OpenDetailInventory;
                _detail.RankingRequested -= ReturnToRanking;
                _detail.AddRequested -= AddFromDetail;
                _detail.ConstituentRequested -= OpenConstituent;
                _detail.Dispose();
            }
            if (_inventory != null)
            {
                _inventory.CloseRequested -= CloseInventory;
                _inventory.DetailRequested -= OpenInventoryDetail;
                _inventory.IndexRequested -= OpenInventoryIndex;
                _inventory.RankingRequested -= OpenInventoryRanking;
                _inventory.Dispose();
            }
            _market = null;
            _detail = null;
            _inventory = null;
            _boundState = null;
        }

        private void DisposeFrontend()
        {
            DisposeWindows();
            if (_overlays != null)
                _overlays.Dispose();
            if (_input != null)
                _input.Dispose();
            if (_assets != null)
                _assets.Dispose();
            _overlays = null;
            _input = null;
            _motion = null;
            _services = null;
            _assets = null;
            _confirmation = null;
        }

        private void UnsubscribeMarket()
        {
            if (_subscribedMarket != null)
                _subscribedMarket.WeekAdvanced -= OnMarketWeekAdvanced;
            _subscribedMarket = null;
        }

        private void OnMarketWeekAdvanced(MarketWeekChange change)
        {
            if (!IsOpen || _overlays == null || change == null || change.NewlyDelistedItemIds == null ||
                change.NewlyDelistedItemIds.Count == 0 || _application.State == null)
                return;
            RunState state = _application.State;
            List<string> notices = new List<string>();
            for (int i = 0; i < change.NewlyDelistedItemIds.Count; i++)
            {
                string itemId = change.NewlyDelistedItemIds[i];
                ItemDefinition item;
                string name = _application.Items.TryGetItem(itemId, out item) ? item.Name : itemId;
                DelistingNotice notice = NewsService.BuildDelistingNotice(state, itemId, change.MarketWeekIndex);
                List<string> changes = new List<string>();
                if ((notice.Kind & DelistingNoticeKind.LongFrozen) != 0)
                    changes.Add("持货 " + notice.FrozenLongQuantity + " 件冻结");
                if ((notice.Kind & DelistingNoticeKind.ShortSettled) != 0)
                {
                    string settlement = "空头 " + notice.SettledShortQuantity + " 件已自动结算";
                    if ((notice.Kind & DelistingNoticeKind.ShortOutstanding) != 0)
                        settlement += "，剩余借货 " + notice.OutstandingShortQuantity + " 件冻结";
                    if (notice.SettlementCashDelta != 0)
                        settlement += "，现金 " + UiFormatting.SignedMoney(notice.SettlementCashDelta) + " 文";
                    if (notice.SettlementDebtDelta != 0)
                        settlement += "，债务 " + UiFormatting.SignedMoney(notice.SettlementDebtDelta) + " 文";
                    changes.Add(settlement);
                }
                else if ((notice.Kind & DelistingNoticeKind.ShortOutstanding) != 0)
                {
                    changes.Add("借货 " + notice.OutstandingShortQuantity + " 件仍冻结");
                }
                string text = name + " 已退市";
                if (changes.Count > 0)
                    text += "（" + string.Join("；", changes.ToArray()) + "）";
                notices.Add(text);
            }
            _overlays.ShowToast(string.Join("；", notices.ToArray()) + "。", 4f);
        }

        internal void Tick()
        {
            if (_disposed)
                return;
            _menuScanRemaining -= Time.unscaledDeltaTime;
            if (_menuScanRemaining <= 0f)
            {
                _menuScanRemaining = 0.6f;
                EnsureSystemMenuEntries();
            }
            if (_inventory != null && _inventory.IsOpen)
                _inventory.Tick();
            if (_input != null && (IsOpen || (_overlays != null && _overlays.HasModal)))
                _input.Tick();
        }

        private void EnsureSystemMenuEntries()
        {
            if (_application == null || !_application.IsEnabled)
                return;
            MenuPanel[] panels = Resources.FindObjectsOfTypeAll<MenuPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                MenuPanel panel = panels[i];
                if (panel == null || !panel.gameObject.scene.IsValid())
                    continue;
                int id = panel.GetInstanceID();
                if (_menuFailures.Contains(id))
                    continue;
                try
                {
                    AttachSystemMenuCore(panel);
                }
                catch (Exception exception)
                {
                    _menuFailures.Add(id);
                    ReportWarning("System menu entry failed: " + exception);
                }
            }
        }

        private void AttachSceneHook()
        {
            if (_sceneHookAttached)
                return;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHookAttached = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CloseAll(UiCloseReason.SceneChanged);
            DisposeFrontend();
            _menuFailures.Clear();
            _menuScanRemaining = 0f;
        }

        private void ReportWarning(string message)
        {
            string formatted = "[XingShangMod.UI] " + message;
            if (TryLogBepInExWarning(formatted))
                return;
            Action<string> logger = LogWarning;
            if (logger != null)
                logger(formatted);
            else
                Debug.LogWarning(formatted);
        }

        private static bool TryLogBepInExWarning(string message)
        {
            try
            {
                global::XingShangMod.Plugin plugin = global::XingShangMod.Plugin.Current;
                if (plugin == null)
                    return false;
                if (!ReferenceEquals(_loggerOwner, plugin))
                {
                    PropertyInfo property = plugin.GetType().GetProperty(
                        "Logger",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    _pluginLogger = property == null ? null : property.GetValue(plugin, null) as ManualLogSource;
                    _loggerOwner = plugin;
                }
                if (_pluginLogger != null)
                {
                    _pluginLogger.LogWarning(message);
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }

    internal sealed class ModUserInterfaceRuntime : MonoBehaviour
    {
        internal ModUserInterface Owner;

        private void Update()
        {
            if (Owner != null)
                Owner.Tick();
        }

        private void OnDestroy()
        {
            ModUserInterface owner = Owner;
            Owner = null;
            if (owner != null)
                owner.Dispose();
        }
    }
}
