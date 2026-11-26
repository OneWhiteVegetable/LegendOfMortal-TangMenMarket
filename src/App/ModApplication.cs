using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Patches;
using XingShangMod.Persistence;
using XingShangMod.Progression;
using XingShangMod.Trading;
using XingShangMod.UI;

namespace XingShangMod.App
{
    public sealed class ModApplication
    {
        private static readonly WeeklyPoint[] EmptyHistory = new WeeklyPoint[0];
        private static readonly NewsRecord[] EmptyNews = new NewsRecord[0];
        private static readonly AssetPoint[] EmptyAssets = new AssetPoint[0];

        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;
        private readonly Dictionary<string, ItemDefinition> _itemDefinitions =
            new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private IModUserInterface _ui;
        private bool _initialized;
        private bool _progressionRefreshPending;
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
            {
                RunUi(_ui.CloseAll, "CloseAll");
                DoorWorkSession.CloseActive();
            }
            _ui = ui;
            if (_ui != null)
                RunUi(_ui.Refresh, "Refresh");
        }

        public IReadOnlyList<WeeklyPoint> GetIndexHistory()
        {
            if (State == null || State.Clock == null)
                return EmptyHistory;
            return CreateVisibleSnapshot(
                State.IndexHistory,
                State.Clock.MarketWeekIndex,
                point => point.MarketWeekIndex,
                ClonePoint);
        }

        public IReadOnlyList<WeeklyPoint> GetItemHistory(string itemId)
        {
            if (State == null || State.Clock == null ||
                !State.IsItemVisibleAt(itemId, State.Clock.MarketWeekIndex))
                return EmptyHistory;

            List<WeeklyPoint> history = new List<WeeklyPoint>();
            if (State.PriceHistory != null)
            {
                for (int i = 0; i < State.PriceHistory.Count; i++)
                {
                    WeeklyPoint point = State.PriceHistory[i];
                    if (point != null && string.Equals(point.ItemId, itemId, StringComparison.Ordinal))
                        history.Add(point);
                }
            }
            return CreateVisibleSnapshot(
                history,
                State.Clock.MarketWeekIndex,
                point => point.MarketWeekIndex,
                ClonePoint);
        }

        public IReadOnlyList<NewsRecord> GetNewsHistory()
        {
            if (State == null || State.Clock == null)
                return EmptyNews;

            List<NewsRecord> visible = new List<NewsRecord>();
            if (State.NewsHistory != null)
            {
                int currentWeek = State.Clock.MarketWeekIndex;
                for (int i = 0; i < State.NewsHistory.Count; i++)
                {
                    NewsRecord record = State.NewsHistory[i];
                    if (record != null && record.MarketWeekIndex <= currentWeek &&
                        State.IsItemVisibleAt(record.ItemId, record.MarketWeekIndex))
                    {
                        visible.Add(record);
                    }
                }
            }
            return CreateVisibleSnapshot(
                visible,
                State.Clock.MarketWeekIndex,
                record => record.MarketWeekIndex,
                CloneNews);
        }

        public IReadOnlyList<AssetPoint> GetAssetHistory()
        {
            if (State == null || State.Clock == null)
                return EmptyAssets;
            return CreateVisibleSnapshot(
                State.AssetHistory,
                State.Clock.MarketWeekIndex,
                point => point.MarketWeekIndex,
                CloneAsset);
        }

        private static IReadOnlyList<T> CreateVisibleSnapshot<T>(
            IList<T> source,
            int currentWeek,
            Func<T, int> weekSelector,
            Func<T, T> clone) where T : class
        {
            List<T> result = new List<T>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    T value = source[i];
                    if (value != null && weekSelector(value) <= currentWeek)
                        result.Add(clone(value));
                }
            }

            for (int i = 1; i < result.Count; i++)
            {
                T value = result[i];
                int week = weekSelector(value);
                int insertAt = i;
                while (insertAt > 0 && weekSelector(result[insertAt - 1]) > week)
                {
                    result[insertAt] = result[insertAt - 1];
                    insertAt--;
                }
                result[insertAt] = value;
            }
            return new ReadOnlyCollection<T>(result);
        }

        private static WeeklyPoint ClonePoint(WeeklyPoint source)
        {
            return new WeeklyPoint
            {
                ItemId = source.ItemId,
                MarketWeekIndex = source.MarketWeekIndex,
                GameYear = source.GameYear,
                GameMonth = source.GameMonth,
                GameRound = source.GameRound,
                SourceRecordIndex = source.SourceRecordIndex,
                Price = source.Price,
                BaseReturn = source.BaseReturn,
                Return = source.Return,
                Observed = source.Observed,
                DataExhausted = source.DataExhausted,
                Delisted = source.Delisted
            };
        }

        private static NewsRecord CloneNews(NewsRecord source)
        {
            return new NewsRecord
            {
                NewsId = source.NewsId,
                ItemId = source.ItemId,
                MarketWeekIndex = source.MarketWeekIndex,
                GameYear = source.GameYear,
                GameMonth = source.GameMonth,
                GameRound = source.GameRound,
                Direction = source.Direction,
                Return = source.Return,
                PoolId = source.PoolId,
                SelectionIndex = source.SelectionIndex,
                Text = source.Text
            };
        }

        private static AssetPoint CloneAsset(AssetPoint source)
        {
            return source.Copy();
        }

        public TradeResult ExecuteTrade(TradeOrder order)
        {
            return ExecuteTrade(order == null ? null : new[] { order });
        }

        public TradeResult ExecuteTrade(IEnumerable<TradeOrder> orders)
        {
            if (!IsEnabled || !EnsureState())
                return IntegrationFailure();

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
                int previousMonth = Clock.State.CurrentGameMonth;
                update();
                ProcessClockAdvancement(previousMonth != Clock.State.CurrentGameMonth);
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
            DoorWorkSession.Cancel();
            if (!IsEnabled)
                return;
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

        internal void OnExecuteSaveDataPending(string saveFile, object gameSave)
        {
            if (!IsEnabled)
                return;
            if (!Saves.PrepareSaveData(saveFile, gameSave))
                _logWarning("Sidecar journal was not prepared: " + Saves.LastError);
        }

        internal void OnExecuteSaveData(string saveFile, object gameSave)
        {
            if (!IsEnabled)
                return;
            if (!Saves.ExecuteSaveData(saveFile, gameSave))
                _logWarning("Sidecar was not committed; the original save is unchanged: " + Saves.LastError);
        }

        internal void OnExecuteLoadGameData(GameSave gameSave, string saveFile, bool isRestore)
        {
            DoorWorkSession.Cancel();
            if (!IsEnabled || gameSave == null)
                return;
            if (string.IsNullOrWhiteSpace(saveFile))
            {
                Disable("Sidecar restore failed safely; the original save context is unavailable.");
                return;
            }
            if (Saves.ExecuteLoadGameData(saveFile, gameSave, isRestore))
                return;

            Disable("Sidecar restore failed safely; no new market run was initialized: " + Saves.LastError);
        }

        internal void OnFreeAwake()
        {
            DoorWorkSession.Cancel();
            if (!IsEnabled)
                return;
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
        }

        internal bool TryOpenMarketFromDoorWork(
            LuaManager manager,
            string name,
            string key,
            string nextScene)
        {
            if (!IsEnabled || manager == null || !EnsureState() || State == null ||
                string.IsNullOrWhiteSpace(State.RunId))
                return false;

            ModUserInterface unityUi = _ui as ModUserInterface;
            if (_ui == null || (unityUi != null && !unityUi.IsAlive))
                AttachUI(new ModUserInterface(this));
            if (_ui == null)
                return false;

            RefreshProgression();
            if (!IsEnabled || _ui == null || State == null)
                return false;

            var session = new DoorWorkSession(manager, name, key, nextScene, State.RunId);
            bool opened = _ui.TryOpenMarket(session.OnClosed);
            if (!opened || !_ui.IsMarketOpen)
            {
                RunUi(_ui.CloseAll, "CloseAll");
                return false;
            }
            if (session.Activate())
                return true;

            RunUi(_ui.CloseAll, "CloseAll");
            return false;
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
            DoorWorkSession.CloseActive();
        }

        internal void Shutdown()
        {
            if (_ui != null)
                RunUi(_ui.CloseAll, "CloseAll");
            DoorWorkSession.CloseActive();
            _ui = null;
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
            return CreateAndActivateNewRun(
                time,
                Guid.NewGuid().ToString("N"),
                CreateRunSeed());
        }

        private RunState CreateAndActivateNewRun(MarketTime time, string runId, int runSeed)
        {
            var state = new RunState
            {
                RunId = runId,
                RunSeed = runSeed,
                CurrentIndex = 1000.0,
                CurrentGameYear = time.Year
            };
            new MarketClock(state.Clock).Initialize(time);
            RebuildServices(state, true);
            bool changed;
            RefreshProgressionCore(out changed);
            if (State.AssetHistory.Count == 0)
                Assets.CaptureWeeklySnapshot();
            _logInfo("Initialized market run " + State.RunId + " at " +
                time.Year + "-" + time.Month + "-" + time.Round + ".");
            NotifyChanged();
            return State;
        }

        private void ApplyLoadedState(RunState state, GameSave savedVersion)
        {
            RebuildServices(RunStateNormalizer.Normalize(state), false);
            if (state.Debt > 0)
            {
                long savedCash = Wallet.Balance;
                if (savedVersion.Stats != null)
                {
                    foreach (StatSave stat in savedVersion.Stats)
                    {
                        if (stat != null && string.Equals(stat.Key, "money", StringComparison.OrdinalIgnoreCase))
                            savedCash = stat.Value;
                    }
                }
                RunWithWalletSuppressed(delegate { LegacySilverDebt.Restore(state, Wallet, savedCash); });
            }
            bool changed;
            RefreshProgressionCore(out changed);
            if (State.AssetHistory.Count == 0)
                Assets.CaptureWeeklySnapshot();
            _logInfo("Restored market run " + State.RunId + " without redrawing bindings.");
            NotifyChanged();
        }

        private void MigrateMissingSidecar(SaveMigrationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException("request");
            MarketTime time;
            if (!TryReadGameSaveTime(request.GameSave, out time))
            {
                if (!TryReadGameTime(out time))
                    throw new InvalidOperationException("Cannot migrate a save without a valid game date.");
            }

            string runId;
            int runSeed;
            CreateMigrationRunIdentity(request, out runId, out runSeed);
            _logWarning("No usable sidecar exists for TimeTick " +
                (request == null ? 0L : request.TimeTick) +
                "; initialized a deterministic market run from the original save identity.");
            CreateAndActivateNewRun(time, runId, runSeed);
        }

        private static void CreateMigrationRunIdentity(
            SaveMigrationRequest request,
            out string runId,
            out int runSeed)
        {
            string identity = (request.IsRestore ? "RestoreData\n" : "GameSave\n") +
                (request.OriginalSaveId ?? string.Empty) + "\n" +
                request.TimeTick.ToString(CultureInfo.InvariantCulture) + "\n" +
                (request.OriginalSaveFingerprint ?? string.Empty);
            string hash = GameSaveFingerprint.ComputeText(identity);
            runId = hash.Substring(0, 32);
            runSeed = (int)(uint.Parse(
                hash.Substring(32, 8),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture) & 0x7fffffffU);
        }

        private void RebuildServices(RunState state, bool initializeRoster)
        {
            if (state == null)
                throw new ArgumentNullException("state");
            DoorWorkSession.Cancel();
            if (_ui != null)
                RunUi(_ui.CloseAll, "CloseAll");

            State = state;
            Clock = new MarketClock(state.Clock);
            MarketTime loadedTime;
            if (!Clock.IsInitialized && TryReadGameTime(out loadedTime))
                Clock.Initialize(loadedTime);
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

        private void ProcessClockAdvancement(bool monthChanged)
        {
            MarketClockAdvancement advancement = Clock.LastAdvancement;
            if (advancement.WasBackward)
            {
                _logWarning("Game time moved backwards; only the market clock baseline was synchronized.");
                NotifyChanged();
                return;
            }
            if (advancement.WeekCount == 0)
            {
                if (monthChanged)
                    NotifyChanged();
                return;
            }

            Market.AdvanceWeeks(advancement);
            NotifyChanged();
        }

        private void OnMarketWeekAdvanced(MarketWeekChange change)
        {
            int finalWeek = State.Clock.MarketWeekIndex;
            try
            {
                State.Clock.MarketWeekIndex = change.MarketWeekIndex;
                HashSet<string> newlyDelistedItemIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < change.NewlyDelistedItemIds.Count; i++)
                {
                    string itemId = change.NewlyDelistedItemIds[i];
                    if (!newlyDelistedItemIds.Add(itemId))
                        continue;
                    IReadOnlyList<TradeResult> results = null;
                    RunWithWalletSuppressed(delegate { results = Shorts.SettleDelistedBatches(itemId); });
                    RecordAutomaticSettlements("Delisted short settlement", itemId, results);
                }

                List<string> retryItemIds = GetOutstandingDelistedShortItemIds(newlyDelistedItemIds);
                for (int i = 0; i < retryItemIds.Count; i++)
                {
                    string itemId = retryItemIds[i];
                    IReadOnlyList<TradeResult> results = null;
                    RunWithWalletSuppressed(delegate { results = Shorts.SettleDelistedBatches(itemId); });
                    RecordAutomaticSettlements("Delisted short settlement retry", itemId, results);
                }

                IReadOnlyList<TradeResult> dueResults = null;
                RunWithWalletSuppressed(delegate { dueResults = Shorts.SettleDueBatches(); });
                RecordAutomaticSettlements("Due short settlement", string.Empty, dueResults);
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

        private List<string> GetOutstandingDelistedShortItemIds(HashSet<string> excludedItemIds)
        {
            List<string> itemIds = new List<string>();
            HashSet<string> seenItemIds = new HashSet<string>(StringComparer.Ordinal);
            if (State.ShortBatches == null)
                return itemIds;

            for (int i = 0; i < State.ShortBatches.Count; i++)
            {
                ShortBatch batch = State.ShortBatches[i];
                if (batch == null || batch.Quantity <= 0 ||
                    excludedItemIds.Contains(batch.ItemId) || !seenItemIds.Add(batch.ItemId))
                {
                    continue;
                }

                SecurityBinding binding = State.FindSecurityBinding(batch.ItemId);
                if (binding != null && binding.IsDelisted)
                    itemIds.Add(batch.ItemId);
            }
            return itemIds;
        }

        private void RecordAutomaticSettlements(
            string operation,
            string fallbackItemId,
            IReadOnlyList<TradeResult> results)
        {
            if (results == null)
            {
                _logWarning(operation + " returned no result for '" + (fallbackItemId ?? string.Empty) + "'.");
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                TradeResult result = results[i];
                if (result == null)
                {
                    _logWarning(operation + " returned a null result for '" +
                        (fallbackItemId ?? string.Empty) + "'.");
                    continue;
                }

                string itemId = string.IsNullOrEmpty(result.ItemId) ? fallbackItemId : result.ItemId;
                if (string.IsNullOrEmpty(itemId) && result.Records != null && result.Records.Count > 0 &&
                    result.Records[0] != null)
                {
                    itemId = result.Records[0].ItemId;
                }
                string details = " item='" + (itemId ?? string.Empty) +
                    "', cashDelta=" + result.CashDelta.ToString(CultureInfo.InvariantCulture) +
                    ", debtDelta=" + result.DebtDelta.ToString(CultureInfo.InvariantCulture) +
                    ", debtAfter=" + result.DebtAfter.ToString(CultureInfo.InvariantCulture) + ".";
                if (result.Success)
                    _logInfo(operation + " succeeded;" + details);
                else
                    _logWarning(operation + " failed (" + result.Error + "): " + result.Message + ";" + details);
            }
        }

        private void RefreshProgressionCore(out bool changed)
        {
            changed = false;
            PlayerStatManagerData manager = PlayerStatManagerData.Instance;
            if (manager == null || manager.Stats == null)
                return;

            if (!State.Item09Unlocked && manager.StoryKeyList != null &&
                manager.StoryKeyList.Contains("Ch_2_2_5_1_001"))
            {
                State.Item09Unlocked = true;
                changed = true;
            }

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

            changed |= RegisterRevealedItems();
        }

        private bool RegisterRevealedItems()
        {
            bool changed = false;
            for (int i = 0; i < State.AppearedItemIds.Count; i++)
            {
                string itemId = State.AppearedItemIds[i];
                if (State.FindSecurityBinding(itemId) == null)
                {
                    Market.RegisterAppearedItem(Items.GetItem(itemId));
                    WeeklyPoint current = State.FindWeeklyPoint(itemId, State.Clock.MarketWeekIndex);
                    if (current != null)
                        News.Generate(State, current);
                    changed = true;
                }
            }
            return changed;
        }

        private bool UnlockPage(string sourceId)
        {
            PageUnlockState page = Roster.Unlock(State, sourceId, State.Clock.MarketWeekIndex);
            return page != null;
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
