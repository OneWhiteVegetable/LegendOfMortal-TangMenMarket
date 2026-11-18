using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Mortal.Core;
using Mortal.Free;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;

namespace XingShangMod.App
{
    public sealed class ModApplication
    {
        private static readonly WeeklyPoint[] EmptyHistory = new WeeklyPoint[0];

        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;
        private readonly Dictionary<string, ItemDefinition> _itemDefinitions =
            new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private IModUserInterface _ui;
        private bool _initialized;
        private bool _progressionRefreshPending;
        private bool _pendingMarket;
        private bool _freeSceneStable;
        private int _walletMutationDepth;
        private int _clockMutationDepth;

        public static ModApplication Instance { get; private set; }
        public bool IsEnabled { get; private set; }
        public RunState State { get; private set; }
        public ItemCatalog Items { get; private set; }
        public NewsCatalog NewsCatalog { get; private set; }
        public WeeklyDataRepository WeeklyData { get; private set; }
        public RosterService Roster { get; private set; }
        public MarketClock Clock { get; private set; }
        public MarketEngine Market { get; private set; }
        public NewsService News { get; private set; }
        public MortalWallet Wallet { get; private set; }
        public TradingService Trading { get; private set; }
        public ShortService Shorts { get; private set; }
        public AssetService Assets { get; private set; }
        public CapacityService Capacity { get; private set; }
        public SaveCoordinator Saves { get; private set; }
        public IModUserInterface UI { get { return _ui; } }
        public string LastError { get; private set; }

        internal bool SuppressWalletHook { get { return _walletMutationDepth > 0; } }

        public event Action Changed;

        public ModApplication(
            Action<string> logInfo,
            Action<string> logWarning,
            Action<string> logError)
        {
            _logInfo = logInfo ?? delegate { };
            _logWarning = logWarning ?? delegate { };
            _logError = logError ?? delegate { };
            Instance = this;
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            Items = ItemCatalog.Load();
            NewsCatalog = NewsCatalog.Load();
            WeeklyData = new WeeklyDataRepository();
            for (int i = 0; i < Items.Items.Count; i++)
            {
                ItemDefinition item = Items.Items[i];
                _itemDefinitions.Add(item.ItemId, item);
            }

            News = new NewsService(NewsCatalog);
            Saves = new SaveCoordinator(
                new SidecarStore(),
                delegate { return State; },
                CreateAndActivateNewRun,
                ApplyLoadedState,
                MigrateMissingSidecar);
            _initialized = true;
            IsEnabled = true;
            LastError = null;
            EnsureState();
        }

        public void AttachUI(IModUserInterface ui)
        {
            if (!Plugin.IsMainThread)
            {
                Plugin.RunOnMainThread(delegate { AttachUI(ui); });
                return;
            }
            if (!IsEnabled)
                return;

            if (!ReferenceEquals(_ui, ui) && _ui != null)
                RunUi(_ui.CloseAll, "CloseAll");
            _ui = ui;
            if (_ui != null)
            {
                RunUi(_ui.Refresh, "Refresh");
                TryOpenPendingMarket();
            }
        }

        public IReadOnlyList<WeeklyPoint> GetIndexHistory()
        {
            if (State == null || State.IndexHistory == null)
                return EmptyHistory;
            return new ReadOnlyCollection<WeeklyPoint>(new List<WeeklyPoint>(State.IndexHistory));
        }

        public IReadOnlyList<WeeklyPoint> GetItemHistory(string itemId)
        {
            if (State == null || string.IsNullOrEmpty(itemId))
                return EmptyHistory;
            return new ReadOnlyCollection<WeeklyPoint>(State.GetPriceHistory(itemId));
        }

        public TradeResult ExecuteTrade(TradeOrder order)
        {
            return ExecuteTrade(order == null ? null : new[] { order });
        }

        public TradeResult ExecuteTrade(IEnumerable<TradeOrder> orders)
        {
            if (!IsEnabled || !EnsureState())
                return IntegrationFailure();
            if (orders == null)
                return Trading.Execute((IEnumerable<TradeOrder>)null);

            try
            {
                TradeResult result = null;
                RunWithWalletSuppressed(delegate { result = Trading.Execute(orders); });
                if (result != null && result.Success)
                {
                    Assets.CaptureTradeSnapshot();
                    NotifyChanged();
                }
                return result;
            }
            catch (Exception exception)
            {
                HandleIntegrationError("ExecuteTrade", exception);
                return IntegrationFailure();
            }
        }

        public void RefreshProgression()
        {
            if (!IsEnabled || !EnsureState())
                return;
            bool changed;
            RefreshProgressionCore(out changed);
            if (changed)
                NotifyChanged();
        }

        internal bool EnsureState()
        {
            if (!IsEnabled || !_initialized || State != null)
                return IsEnabled && State != null;

            MarketTime time;
            if (!TryReadGameTime(out time))
                return false;
            CreateAndActivateNewRun(time);
            return true;
        }

        internal void OnActionPointsSpent(int actualPoints)
        {
            if (actualPoints <= 0 || !IsEnabled || !EnsureState())
                return;
            MarketTime time;
            if (!TryReadGameTime(out time))
                return;
            RunClockUpdate(delegate { Clock.ApplyActionCost(actualPoints, time); });
        }

        internal void OnNaturalTimeUpdated()
        {
            if (!IsEnabled || !EnsureState())
                return;
            MarketTime time;
            if (!TryReadGameTime(out time))
                return;
            RunClockUpdate(delegate { Clock.SynchronizeNaturalTime(time); });
        }

        internal void OnStoryTimeUpdated()
        {
            if (!IsEnabled || !EnsureState())
                return;
            MarketTime time;
            if (!TryReadGameTime(out time))
                return;
            RunClockUpdate(delegate { Clock.AdvanceStory(time); });
        }

        private void RunClockUpdate(Action update)
        {
            if (_clockMutationDepth > 0)
                return;
            _clockMutationDepth++;
            try
            {
                update();
                ProcessClockAdvancement();
            }
            finally
            {
                _clockMutationDepth--;
            }
        }

        internal void OnNonTradeMoneyIncome()
        {
            if (!IsEnabled || SuppressWalletHook || State == null || State.Debt <= 0)
                return;

            long repaid = 0;
            bool success = false;
            RunWithWalletSuppressed(delegate { success = Shorts.TryRepayDebtFromCash(out repaid); });
            if (!success)
                throw new InvalidOperationException("The original silver stat rejected a debt repayment.");
            if (repaid > 0)
            {
                _logInfo("Repaid " + repaid + " silver of market debt.");
                NotifyChanged();
            }
        }

        internal void OnNewGamePending(bool newAct)
        {
            if (!IsEnabled)
                return;
            _pendingMarket = false;
            _freeSceneStable = false;
            if (newAct)
                Saves.MarkNewActPending();
            else
                Saves.MarkNewGamePending();
        }

        internal void OnCreateSaveData(GameSave gameSave)
        {
            if (!IsEnabled || gameSave == null)
                return;
            if (!Saves.IsNewRunPending && !EnsureState())
                return;
            if (!Saves.CreateSaveData(gameSave))
                _logWarning("Memory snapshot was not created: " + Saves.LastError);
        }

        internal void OnExecuteSaveData(string saveFile, object gameSave)
        {
            if (!IsEnabled)
                return;
            if (!Saves.ExecuteSaveData(saveFile, gameSave))
                _logWarning("Sidecar was not written; the original save is unchanged: " + Saves.LastError);
        }

        internal void OnExecuteLoadGameData(GameSave gameSave)
        {
            if (!IsEnabled || gameSave == null)
                return;
            if (Saves.ExecuteLoadGameData(gameSave))
                return;

            _logWarning("Sidecar restore failed safely; initializing from the loaded original save: " + Saves.LastError);
            MarketTime time;
            if (!TryReadGameSaveTime(gameSave, out time) && !TryReadGameTime(out time))
                throw new InvalidOperationException("The loaded original save has no valid game date.");
            CreateAndActivateNewRun(time);
        }

        internal void OnFreeAwake()
        {
            if (!IsEnabled)
                return;
            _freeSceneStable = false;
            _progressionRefreshPending = true;
        }

        internal void OnFreeFrame(FreeManager manager)
        {
            if (!IsEnabled || manager == null || !manager.isActiveAndEnabled)
                return;
            if (!EnsureState())
                return;

            if (_progressionRefreshPending)
            {
                _progressionRefreshPending = false;
                RefreshProgression();
            }

            _freeSceneStable = manager.EnableHotKey && !manager.IsProcessing;
            if (_freeSceneStable)
                TryOpenPendingMarket();
        }

        internal void MarkDoorWorkCompleted()
        {
            if (!IsEnabled)
                return;
            _pendingMarket = true;
            _freeSceneStable = false;
        }

        internal void HandleIntegrationError(string source, Exception exception)
        {
            Disable(source + ": " + exception);
        }

        internal void Disable(string reason)
        {
            LastError = string.IsNullOrWhiteSpace(reason) ? "The mod was disabled." : reason;
            IsEnabled = false;
            _logError(LastError);
            if (_ui != null)
                RunUi(_ui.CloseAll, "CloseAll");
        }

        internal void Shutdown()
        {
            if (_ui != null)
                RunUi(_ui.CloseAll, "CloseAll");
            _ui = null;
            _pendingMarket = false;
            _freeSceneStable = false;
            IsEnabled = false;
            if (ReferenceEquals(Instance, this))
                Instance = null;
        }

        internal static bool TryReadGameTime(out MarketTime time)
        {
            time = default(MarketTime);
            PlayerStatManagerData manager = PlayerStatManagerData.Instance;
            if (manager == null || ReferenceEquals(manager.GameTime, null))
                return false;
            GameTime gameTime = manager.GameTime;
            if (gameTime.Year < 1 || gameTime.Month < 1 || gameTime.Month > 12 ||
                gameTime.Stage < MonthStageType.上旬 || gameTime.Stage > MonthStageType.下旬)
                return false;
            time = new MarketTime(gameTime.Year, gameTime.Month, (int)gameTime.Stage);
            return true;
        }

        private RunState CreateAndActivateNewRun()
        {
            MarketTime time;
            if (!TryReadGameTime(out time))
                throw new InvalidOperationException("The original game date is unavailable for a new run.");
            return CreateAndActivateNewRun(time);
        }

        private RunState CreateAndActivateNewRun(MarketTime time)
        {
            var state = new RunState
            {
                RunId = Guid.NewGuid().ToString("N"),
                RunSeed = CreateRunSeed(),
                CurrentIndex = 1000.0,
                CurrentGameYear = time.Year
            };
            new MarketClock(state.Clock).Initialize(time);
            RebuildServices(state, true);
            _pendingMarket = false;
            _freeSceneStable = false;
            bool changed;
            RefreshProgressionCore(out changed);
            if (State.AssetHistory.Count == 0)
                Assets.CaptureWeeklySnapshot();
            _logInfo("Initialized market run " + State.RunId + " at " +
                time.Year + "-" + time.Month + "-" + time.Round + ".");
            NotifyChanged();
            return State;
        }

        private void ApplyLoadedState(RunState state)
        {
            RebuildServices(RunStateNormalizer.Normalize(state), false);
            _pendingMarket = false;
            _freeSceneStable = false;
            bool changed;
            RefreshProgressionCore(out changed);
            if (State.AssetHistory.Count == 0)
                Assets.CaptureWeeklySnapshot();
            _logInfo("Restored market run " + State.RunId + " without redrawing bindings.");
            NotifyChanged();
        }

        private void MigrateMissingSidecar(SaveMigrationRequest request)
        {
            MarketTime time;
            if (request == null || !TryReadGameSaveTime(request.GameSave, out time))
            {
                if (!TryReadGameTime(out time))
                    throw new InvalidOperationException("Cannot migrate a save without a valid game date.");
            }
            _logWarning("No usable sidecar exists for TimeTick " +
                (request == null ? 0L : request.TimeTick) + "; initialized a new market run from the original date.");
            CreateAndActivateNewRun(time);
        }

        private void RebuildServices(RunState state, bool initializeRoster)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            if (_ui != null)
                RunUi(_ui.CloseAll, "CloseAll");

            State = state;
            Clock = new MarketClock(state.Clock);
            Wallet = new MortalWallet();
            Capacity = new CapacityService();
            Roster = new RosterService(Items);
            News = new NewsService(NewsCatalog);
            if (initializeRoster)
                Roster.Initialize(state, state.Clock.MarketWeekIndex);
            Market = new MarketEngine(state, WeeklyData, Items.Items);
            Market.WeekAdvanced += OnMarketWeekAdvanced;
            Trading = new TradingService(state, Wallet, _itemDefinitions, Capacity);
            Shorts = new ShortService(Trading);
            Assets = new AssetService(state, Wallet, _itemDefinitions);
        }

        private void ProcessClockAdvancement()
        {
            MarketClockAdvancement advancement = Clock.LastAdvancement;
            if (advancement.WasBackward)
            {
                _logWarning("Game time moved backwards; only the market clock baseline was synchronized.");
                NotifyChanged();
                return;
            }
            if (advancement.WeekCount == 0)
                return;

            Market.AdvanceWeeks(advancement);
            NotifyChanged();
        }

        private void OnMarketWeekAdvanced(MarketWeekChange change)
        {
            int finalWeek = State.Clock.MarketWeekIndex;
            try
            {
                State.Clock.MarketWeekIndex = change.MarketWeekIndex;
                RunWithWalletSuppressed(delegate { Shorts.SettleDueBatches(); });
                for (int i = 0; i < change.Items.Count; i++)
                    News.Generate(State, change.Items[i]);
                AssetPoint point = Assets.CaptureWeeklySnapshot();
                point.GameYear = change.Time.Year;
                point.GameMonth = change.Time.Month;
                point.GameRound = change.Time.Round;
            }
            finally
            {
                State.Clock.MarketWeekIndex = finalWeek;
            }
        }

        private void RefreshProgressionCore(out bool changed)
        {
            changed = false;
            PlayerStatManagerData manager = PlayerStatManagerData.Instance;
            if (manager == null || manager.Stats == null)
                return;

            int homeLevel = manager.Stats.Get(GameStatType.門派規模).FinalValue;
            if (homeLevel >= 2)
                changed |= UnlockPage(RosterService.HomeLevel2SourceId);
            if (homeLevel >= 3)
                changed |= UnlockPage(RosterService.HomeLevel3SourceId);

            MissionManagerData missions = MissionManagerData.Instance;
            if (missions != null)
            {
                for (int i = 1; i <= 5; i++)
                {
                    string source = "Facility_" + i.ToString("000");
                    FlagData flag = missions.GetFlag(source);
                    if (flag != null && flag.State > 0)
                        changed |= UnlockPage(source);
                }
            }

            if (!State.Item09Unlocked && manager.StoryKeyList != null &&
                manager.StoryKeyList.Contains("Ch_2_2_5_1_001"))
            {
                State.Item09Unlocked = true;
                changed = true;
            }

            for (int i = 0; i < State.AppearedItemIds.Count; i++)
            {
                string itemId = State.AppearedItemIds[i];
                if (State.FindSecurityBinding(itemId) == null)
                {
                    Market.RegisterAppearedItem(Items.GetItem(itemId));
                    changed = true;
                }
            }
        }

        private bool UnlockPage(string sourceId)
        {
            PageUnlockState page = Roster.Unlock(State, sourceId, State.Clock.MarketWeekIndex);
            return page != null;
        }

        private void TryOpenPendingMarket()
        {
            if (!_pendingMarket || !_freeSceneStable || _ui == null || !IsEnabled)
                return;
            _pendingMarket = false;
            RunUi(_ui.OpenMarket, "OpenMarket");
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    _logError("Changed subscriber failed: " + exception);
                }
            }
            if (_ui != null)
                RunUi(_ui.Refresh, "Refresh");
        }

        private void RunUi(Action action, string operation)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                _logError("UI operation " + operation + " failed: " + exception);
            }
        }

        private void RunWithWalletSuppressed(Action action)
        {
            _walletMutationDepth++;
            try
            {
                action();
            }
            finally
            {
                _walletMutationDepth--;
            }
        }

        private TradeResult IntegrationFailure()
        {
            TradeResult result = TradeResult.Fail(
                TradeError.InvalidState,
                "行商服务尚未就绪或已停用。",
                string.Empty);
            result.CashAfter = Wallet == null ? 0L : Wallet.Balance;
            result.DebtAfter = State == null ? 0L : State.Debt;
            return result;
        }

        private static bool TryReadGameSaveTime(GameSave save, out MarketTime time)
        {
            time = default(MarketTime);
            if (save == null || save.CurrentYear < 1 || save.CurrentMonth < 1 || save.CurrentMonth > 12 ||
                save.CurrentStage < MonthStageType.上旬 || save.CurrentStage > MonthStageType.下旬)
                return false;
            time = new MarketTime(save.CurrentYear, save.CurrentMonth, (int)save.CurrentStage);
            return true;
        }

        private static int CreateRunSeed()
        {
            byte[] bytes = new byte[4];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
        }
    }
}
