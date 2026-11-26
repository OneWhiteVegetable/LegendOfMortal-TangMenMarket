using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XingShang.UI.Authoring
{
    [Serializable]
    public sealed class RuntimePreviewHtmlDocument
    {
        public int schemaVersion;
        public long startingCash;
        public RuntimePreviewHtmlItem[] items;
        public RuntimePreviewHtmlIndex index;
    }

    [Serializable]
    public sealed class RuntimePreviewHtmlItem
    {
        public string key;
        public string itemId;
        public string name;
        public string icon;
        public double currentPrice;
        public RuntimePreviewHtmlPoint[] history;
    }

    [Serializable]
    public sealed class RuntimePreviewHtmlIndex
    {
        public string name;
        public RuntimePreviewHtmlPoint[] history;
    }

    [Serializable]
    public sealed class RuntimePreviewHtmlPoint
    {
        public string t;
        public double close;
    }

    [Serializable]
    public sealed class RuntimePreviewFixtureDescriptor
    {
        public int schemaVersion = 1;
        public string fixtureId;
        public string source;
        public string sourceSha256;
        public int runSeed;
        public int currentWeek;
        public int itemCount;
        public int historyPointsPerItem;
        public int indexPoints;
        public int futurePoisonPointsPerSeries;
        public long cash;
        public int life;
        public int lightness;
        public int internalStrength;
        public string longItemId;
        public int longQuantity;
        public string shortItemId;
        public int shortQuantity;
        public int shortDueWeek;
        public string runStateSha256;
        public string notes;
    }

    internal sealed class RuntimePreviewTradeExpectation
    {
        internal long CashBefore;
        internal long CashAfter;
        internal long DebtAfter;
        internal int RecordCount;
        internal int ExpectedTradeHistoryCount;
        internal int CapacityAfterUsed;
        internal int CapacityAfterTotal;
    }

    internal sealed class RuntimePreviewAdapter : IDisposable
    {
        private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly RuntimePreviewOptions _options;
        private readonly List<ScriptableObject> _scriptableObjects = new List<ScriptableObject>();
        private readonly RuntimePreviewHtmlDocument _html;
        private readonly string _sourceHash;
        private Assembly _assembly;
        private ResolveEventHandler _assemblyResolver;
        private object _application;
        private object _state;
        private object _items;
        private object _news;
        private ScriptableObject _fixtureMoney;

        internal void SetDisplayedCash(int value)
        {
            // Drive the regular screen refresh from the actual GameStat read by the preview wallet.
            SetField(_fixtureMoney, "_value", value);
            Invoke(_market, "Refresh");
        }

        private object _wallet;
        private object _capacity;
        private object _ui;
        private object _market;
        private object _detail;
        private object _inventory;
        private object _overlays;
        private object _confirmation;
        private string _initialStateHash;
        private long _initialCash;
        private bool _disposed;
        private readonly List<RuntimePreviewWindowCallbackRecord> _windowCallbacks =
            new List<RuntimePreviewWindowCallbackRecord>();
        private readonly List<RuntimePreviewWindowTweenProbe> _windowProbes =
            new List<RuntimePreviewWindowTweenProbe>();
        private readonly HashSet<int> _instrumentedSequences = new HashSet<int>();
        private readonly List<RuntimePreviewCompletionProbe> _completionProbes =
            new List<RuntimePreviewCompletionProbe>();
        private bool _cardCompleted;
        private float _cardCompletionElapsed;
        private float _cardCompletionDuration;
        private float _cardCompletionTimeScale;
        private bool _cardOriginalOnCompletePresent;
        private int _inventoryExpectedCompletions;
        private int _inventoryCompletedCompletions;
        private float _inventoryLongestDuration;
        private float _inventoryLatestElapsed;
        private float _inventoryCompletionTimeScale;

        internal RuntimePreviewAdapter(RuntimePreviewOptions options)
        {
            _options = options;
            try
            {
                InstallAssemblyResolution();
                _assembly = Assembly.LoadFrom(_options.PluginAssemblyPath);
                ConfigurePluginPath();
                string json = File.ReadAllText(_options.FixturePath);
                _sourceHash = VisualValidation.Sha256Text(json);
                _html = JsonUtility.FromJson<RuntimePreviewHtmlDocument>(json);
                ValidateHtmlFixture(_html);
                _items = InvokeStatic(RequireRuntimeType("XingShangMod.Progression.ItemCatalog"), "Load", _options.ItemsPath);
                _news = InvokeStatic(RequireRuntimeType("XingShangMod.Progression.NewsCatalog"), "Load", _options.NewsPath);
                _state = BuildRunState();
                if (_options.Cases.Length == 1 && (_options.Cases[0] == "asset-profit-proof" || _options.Cases[0] == "asset-sign-ink"))
                    ConfigureProfitFixture();
                BuildApplication();
                _initialStateHash = StateHash();
                _initialCash = WalletBalance();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal string FixtureId { get { return "html-v4-shared-78w-current-77"; } }
        internal string RunStateJson { get { return SerializeState(true); } }
        internal string InitialStateHash { get { return _initialStateHash; } }

        internal RuntimePreviewFixtureDescriptor DescribeFixture()
        {
            return new RuntimePreviewFixtureDescriptor
            {
                fixtureId = FixtureId,
                source = _options.FixturePath,
                sourceSha256 = _sourceHash,
                runSeed = 4908,
                currentWeek = _html.items[0].history.Length - 1,
                itemCount = _html.items.Length,
                historyPointsPerItem = _html.items[0].history.Length,
                indexPoints = _html.index.history.Length,
                futurePoisonPointsPerSeries = 2,
                cash = _initialCash,
                life = 18,
                lightness = 20,
                internalStrength = 20,
                longItemId = "04",
                longQuantity = 6,
                shortItemId = "19",
                shortQuantity = 2,
                shortDueWeek = _html.items[0].history.Length - 1 + 20,
                runStateSha256 = _initialStateHash,
                notes = "HTML item/index histories are shared. Demo ticker and stamina controls are not imported. News is generated only by the formal NewsService. Short proceeds use the formal 97% rule; future poison points must remain invisible."
            };
        }

        internal List<string> ValidateFixtureSemantics(VisualSemanticSnapshot snapshot)
        {
            List<string> failures = new List<string>();
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                if (snapshot.texts[i].text.IndexOf("模拟快讯", StringComparison.Ordinal) >= 0)
                    failures.Add("HTML simulated news leaked into the formal runtime view: " + snapshot.texts[i].path);
            }

            if (snapshot.caseName.IndexOf("market", StringComparison.Ordinal) == 0)
            {
                List<VisualTextSemantic> names = new List<VisualTextSemantic>();
                List<VisualTextSemantic> prices = new List<VisualTextSemantic>();
                List<VisualTextSemantic> priceUnits = new List<VisualTextSemantic>();
                List<VisualImageSemantic> images = new List<VisualImageSemantic>();
                for (int i = 0; i < snapshot.texts.Count; i++)
                {
                    VisualTextSemantic text = snapshot.texts[i];
                    if (text.path.IndexOf("GoodsRow", StringComparison.Ordinal) < 0)
                        continue;
                    if (text.path.IndexOf("/NameText#", StringComparison.Ordinal) >= 0)
                        names.Add(text);
                    if (text.path.IndexOf("/PriceText#", StringComparison.Ordinal) >= 0)
                        prices.Add(text);
                    if (text.path.IndexOf("/PriceUnitText#", StringComparison.Ordinal) >= 0)
                        priceUnits.Add(text);
                }
                for (int i = 0; i < snapshot.images.Count; i++)
                {
                    VisualImageSemantic image = snapshot.images[i];
                    if (image.path.IndexOf("GoodsRow", StringComparison.Ordinal) >= 0 &&
                        image.path.IndexOf("/ItemImage#", StringComparison.Ordinal) >= 0)
                        images.Add(image);
                }
                if (names.Count != 6 || prices.Count != 6 || priceUnits.Count != 6 || images.Count != 6)
                {
                    failures.Add("Market fixture projection must contain six names, prices, price units and item sprites; actual=" +
                        names.Count + "/" + prices.Count + "/" + priceUnits.Count + "/" + images.Count + ".");
                }
                int count = Math.Min(6, Math.Min(names.Count, Math.Min(prices.Count, Math.Min(priceUnits.Count, images.Count))));
                for (int i = 0; i < count; i++)
                {
                    RuntimePreviewHtmlItem item = _html.items[i];
                    RequireEqual(failures, names[i].path, item.name, names[i].text);
                    double close = item.history[item.history.Length - 1].close;
                    RequireEqual(failures, prices[i].path, close.ToString("N0", CultureInfo.InvariantCulture), prices[i].text);
                    RequireEqual(failures, priceUnits[i].path, "文", priceUnits[i].text);
                    string expectedIcon = Path.GetFileNameWithoutExtension(item.icon ?? string.Empty);
                    RequireEqual(failures, images[i].path, expectedIcon, images[i].sprite);
                }
            }
            if (snapshot.caseName.IndexOf("detail", StringComparison.Ordinal) == 0)
            {
                RuntimePreviewHtmlItem item = _html.items[0];
                RequireText(failures, snapshot, "/DetailPage#", "/NameText#", item.name);
                RequireText(failures, snapshot, "/DetailPage#", "/PriceText#",
                    item.history[item.history.Length - 1].close.ToString("N0", CultureInfo.InvariantCulture) + " 文");
            }
            if (snapshot.caseName.IndexOf("inventory", StringComparison.Ordinal) == 0)
                RequireText(failures, snapshot, "InventoryWindow", "/CapacityValueText#", "2/4 格");
            if (snapshot.caseName == "inventory-maturities")
            {
                Graphic badge = LoanCountdown;
                if (!badge.isActiveAndEnabled || badge.mainTexture.name != "loan_maturity_atlas" ||
                    (int)GetProperty(badge, "RemainingWeeks") != 2 || badge.GetComponentsInChildren<Text>().Length != 0)
                    failures.Add("Mixed maturity card must bind the atlas, display only 2 and expose no Text on the ticket.");
                if (!HasBothMaturities(LoanTooltipBody)) failures.Add("Loan description lost a due date, countdown or quantity.");
            }
            if (snapshot.caseName == "assets")
            {
                RequireText(failures, snapshot, "AssetsPage", "/CashText#",
                    _initialCash.ToString("N0", CultureInfo.InvariantCulture) + " 文");
                RequireText(failures, snapshot, "AssetsPage", "/GoodsText#",
                    (FindItem("04").currentPrice * 6).ToString("N0", CultureInfo.InvariantCulture) + " 文");
            }
            if (snapshot.caseName == "market-empty")
            {
                RequireText(failures, snapshot, "/CartEmpty#", "/TitleText#", "货签尚空");
                RequireText(failures, snapshot, "/CartRail#", "/CartCountText#", "0");
            }
            if (snapshot.caseName == "market")
                RequireText(failures, snapshot, "/CartRail#", "/CartCountText#", "2");
            if (snapshot.caseName == "market-modal")
                RequireText(failures, snapshot, "XingShangMod.Modal", "/TitleText#", "立契结算");
            if (snapshot.caseName == "market-toast")
                RequireText(failures, snapshot, "XingShangMod.Toast", "/MessageText#", "飞天门辟邪玉 · 买入已纳入契匣。");
            if (snapshot.caseName == "index")
                RequireText(failures, snapshot, "/DetailPage#", "/NameText#", "唐门指数");
            if (snapshot.caseName == "ranking" && RankingRowCount != _html.items.Length)
                failures.Add("Ranking has " + RankingRowCount + " rows for " + _html.items.Length + " visible fixture items.");
            if (snapshot.caseName == "detail-tooltip")
                RequireText(failures, snapshot, "XingShangMod.Tooltip", "/TitleText#", "复位图表");
            return failures;
        }

        internal void OpenCase(string caseName)
        {
            OpenCase(caseName, true);
        }

        internal void OpenCase(string caseName, bool reducedMotion)
        {
            ThrowIfDisposed();
            DisposeUserInterface();
            _ui = Construct(RequireRuntimeType("XingShangMod.UI.ModUserInterface"), _application);
            SetProperty(_ui, "LogWarning", new Action<string>(Debug.LogWarning));
            bool ready = Convert.ToBoolean(Invoke(_ui, "EnsureReady"), CultureInfo.InvariantCulture);
            if (!ready)
                throw new InvalidOperationException("Formal ModUserInterface.EnsureReady returned false.");
            object motion = GetField(_ui, "_motion");
            Invoke(motion, "SetReducedMotion", reducedMotion);
            _market = GetField(_ui, "_market");
            _detail = GetField(_ui, "_detail");
            _inventory = GetField(_ui, "_inventory");
            _overlays = GetField(_ui, "_overlays");
            _confirmation = GetField(_ui, "_confirmation");

            switch (caseName)
            {
                case "market-empty":
                    OpenMarketEmpty();
                    break;
                case "market":
                    OpenMarketWithCart();
                    break;
                case "market-modal":
                    OpenMarketWithCart();
                    PresentRealCartPreview();
                    break;
                case "market-toast":
                    OpenMarketWithCart();
                    Invoke(_overlays, "ShowToast", "飞天门辟邪玉 · 买入已纳入契匣。", 60f);
                    break;
                case "detail":
                    OpenMarketWithCart();
                    OpenDetail();
                    break;
                case "index":
                    OpenMarketEmpty();
                    Invoke(_ui, "OpenIndex");
                    break;
                case "ranking":
                    OpenMarketEmpty();
                    ShowRanking();
                    break;
                case "detail-tooltip":
                    OpenMarketWithCart();
                    OpenDetail();
                    ShowDetailTooltip();
                    break;
                case "inventory":
                case "inventory-maturities":
                    OpenMarketWithCart();
                    Invoke(_ui, "OpenMarketInventory");
                    break;
                case "inventory-readonly":
                    Invoke(_ui, "OpenReadonlyInventory");
                    break;
                case "assets":
                    Invoke(_ui, "OpenReadonlyInventory");
                    Invoke(_inventory, "ShowAssets");
                    break;
                case "capacity-empty":
                    OpenMarketWithCart();
                    Invoke(GetField(_market, "_cart"), "Clear");
                    Invoke(_market, "Refresh");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("caseName", caseName, "Unknown Runtime Preview case.");
            }
        }

        internal string CurrentRunStateHash { get { return StateHash(); } }
        internal long CurrentCash { get { return WalletBalance(); } }
        internal long CurrentDebt { get { return Convert.ToInt64(GetField(_state, "Debt"), CultureInfo.InvariantCulture); } }

        internal bool IsReducedMotion
        {
            get { return Convert.ToBoolean(GetProperty(GetField(_ui, "_motion"), "ReducedMotion"), CultureInfo.InvariantCulture); }
        }

        internal void SetReducedMotion(bool value)
        {
            Invoke(GetField(_ui, "_motion"), "SetReducedMotion", value);
        }

        internal GameObject CardRoot(int index)
        {
            return GetProperty(CardAt(index), "GameObject") as GameObject;
        }

        internal RectTransform CardMotionPivot(int index)
        {
            return GetField(CardAt(index), "_motionPivot") as RectTransform;
        }

        internal RectTransform CardHardShadow(int index)
        {
            return GetField(CardAt(index), "_hardShadow") as RectTransform;
        }

        internal RectTransform CardGoldLine(int index)
        {
            return GetField(CardAt(index), "_goldLine") as RectTransform;
        }

        internal Graphic CardChart(int index)
        {
            object view = GetField(CardAt(index), "_chart");
            return GetProperty(view, "Chart") as Graphic;
        }

        internal GameObject InventorySlotRoot(int index)
        {
            return GetProperty(InventorySlotAt(index), "GameObject") as GameObject;
        }

        internal RectTransform InventorySlotPivot(int index)
        {
            object motion = GetField(InventorySlotAt(index), "_motion");
            return GetField(motion, "_pivot") as RectTransform;
        }

        internal RectTransform InventoryActions(int index)
        {
            return GetField(GetField(InventorySlotAt(index), "_motion"), "_actions") as RectTransform;
        }

        internal CanvasGroup InventoryActionsGroup(int index)
        {
            return GetField(GetField(InventorySlotAt(index), "_motion"), "_actionsGroup") as CanvasGroup;
        }

        internal GameObject InventoryFocusFrame(int index)
        {
            return GetField(GetField(InventorySlotAt(index), "_motion"), "_focusFrame") as GameObject;
        }

        internal RectTransform WindowRoot
        {
            get { return GetProperty(_market, "Root") as RectTransform; }
        }

        internal RectTransform WindowPivot
        {
            get { return GetField(GetField(_market, "_windowMotion"), "_pivot") as RectTransform; }
        }

        internal CanvasGroup WindowMotionGroup
        {
            get { return GetField(GetField(_market, "_windowMotion"), "_group") as CanvasGroup; }
        }

        internal bool WindowRootActive { get { return WindowRoot != null && WindowRoot.gameObject.activeSelf; } }

        internal bool DetailWindowRootActive
        {
            get
            {
                RectTransform root = GetProperty(_detail, "Root") as RectTransform;
                return root != null && root.gameObject.activeSelf;
            }
        }

        internal bool DetailWindowAnimationActive
        {
            get
            {
                IList tweens = GetField(GetField(GetField(_detail, "_windowMotion"), "_scope"), "_tweens") as IList;
                bool playing;
                bool complete;
                ReadTweenState(tweens, out playing, out complete);
                return playing;
            }
        }

        internal bool UserClosePending
        {
            get { return Convert.ToBoolean(GetField(_ui, "_userClosePending"), CultureInfo.InvariantCulture); }
        }

        internal bool CardAnimationActive
        {
            get { return GetField(CardAt(0), "_motionSequence") != null; }
        }

        internal bool CardTweenPlaying
        {
            get
            {
                object sequence = GetField(CardAt(0), "_motionSequence");
                return sequence != null && Convert.ToBoolean(
                    InvokeStatic(
                        ResolveType("DG.Tweening.TweenExtensions, DOTween"),
                        "IsPlaying",
                        sequence),
                    CultureInfo.InvariantCulture);
            }
        }

        internal void BeginCardCompletionProbe()
        {
            _cardCompleted = false;
            _cardCompletionElapsed = 0f;
            _cardCompletionDuration = 0f;
            _cardCompletionTimeScale = 0f;
            object sequence = GetField(CardAt(0), "_motionSequence");
            if (sequence == null)
                throw new InvalidOperationException("Card completion probe requires a live Sequence.");
            Delegate original = GetField(sequence, "onComplete") as Delegate;
            if (original == null)
                throw new InvalidOperationException("Card Sequence lost its original OnComplete delegate.");
            _cardOriginalOnCompletePresent = true;
            RuntimePreviewCompletionProbe probe = new RuntimePreviewCompletionProbe
            {
                Owner = this,
                Tween = sequence,
                Card = true
            };
            Type callbackType = ResolveType("DG.Tweening.TweenCallback, DOTween");
            Delegate callback = Delegate.CreateDelegate(callbackType, probe, probe.GetType().GetMethod("Complete"));
            SetField(sequence, "onComplete", Delegate.Combine(original, callback));
            _completionProbes.Add(probe);
        }

        internal void BeginInventoryCompletionProbe()
        {
            _inventoryExpectedCompletions = 0;
            _inventoryCompletedCompletions = 0;
            _inventoryLongestDuration = 0f;
            _inventoryLatestElapsed = 0f;
            _inventoryCompletionTimeScale = 0f;
            IList tweens = InventoryTweens();
            if (tweens == null || tweens.Count == 0)
                throw new InvalidOperationException("Inventory completion probe requires live Tweens.");
            Type callbackType = ResolveType("DG.Tweening.TweenCallback, DOTween");
            _inventoryExpectedCompletions = tweens.Count;
            for (int i = 0; i < tweens.Count; i++)
            {
                object tween = tweens[i];
                RuntimePreviewCompletionProbe probe = new RuntimePreviewCompletionProbe
                {
                    Owner = this,
                    Tween = tween,
                    Card = false
                };
                Delegate original = GetField(tween, "onComplete") as Delegate;
                Delegate callback = Delegate.CreateDelegate(callbackType, probe, probe.GetType().GetMethod("Complete"));
                if (original != null)
                    callback = Delegate.Combine(original, callback);
                SetField(tween, "onComplete", callback);
                _completionProbes.Add(probe);
            }
        }

        internal void CompleteCardTween(object tween)
        {
            Type extensions = ResolveType("DG.Tweening.TweenExtensions, DOTween");
            _cardCompleted = true;
            _cardCompletionElapsed = Convert.ToSingle(
                InvokeStatic(extensions, "Elapsed", tween, false),
                CultureInfo.InvariantCulture);
            _cardCompletionDuration = Convert.ToSingle(GetField(tween, "duration"), CultureInfo.InvariantCulture);
            _cardCompletionTimeScale = Convert.ToSingle(GetField(tween, "timeScale"), CultureInfo.InvariantCulture);
        }

        internal void CompleteInventoryTween(object tween)
        {
            Type extensions = ResolveType("DG.Tweening.TweenExtensions, DOTween");
            _inventoryCompletedCompletions++;
            _inventoryLongestDuration = Math.Max(
                _inventoryLongestDuration,
                Convert.ToSingle(GetField(tween, "duration"), CultureInfo.InvariantCulture));
            _inventoryLatestElapsed = Math.Max(
                _inventoryLatestElapsed,
                Convert.ToSingle(InvokeStatic(extensions, "Elapsed", tween, false), CultureInfo.InvariantCulture));
            _inventoryCompletionTimeScale = Convert.ToSingle(GetField(tween, "timeScale"), CultureInfo.InvariantCulture);
        }

        internal RuntimePreviewCompletionEvidence CardCompletionEvidence
        {
            get
            {
                return !_cardCompleted ? null : new RuntimePreviewCompletionEvidence
                {
                    duration = _cardCompletionDuration,
                    elapsed = _cardCompletionElapsed,
                    timeScale = _cardCompletionTimeScale,
                    completionCount = 1,
                    originalOnCompletePresent = _cardOriginalOnCompletePresent
                };
            }
        }

        internal RuntimePreviewCompletionEvidence InventoryCompletionEvidence
        {
            get
            {
                return _inventoryExpectedCompletions <= 0 ||
                    _inventoryCompletedCompletions != _inventoryExpectedCompletions
                    ? null
                    : new RuntimePreviewCompletionEvidence
                    {
                        duration = _inventoryLongestDuration,
                        elapsed = _inventoryLatestElapsed,
                        timeScale = _inventoryCompletionTimeScale,
                        completionCount = _inventoryCompletedCompletions,
                        originalOnCompletePresent = true
                    };
            }
        }

        internal bool WindowTweenExists
        {
            get
            {
                IList tweens = WindowTweens();
                return tweens != null && tweens.Count > 0;
            }
        }

        internal bool WindowAnimationActive
        {
            get
            {
                bool playing;
                bool complete;
                ReadTweenState(WindowTweens(), out playing, out complete);
                return playing;
            }
        }

        internal bool WindowAnimationComplete
        {
            get
            {
                bool playing;
                bool complete;
                ReadTweenState(WindowTweens(), out playing, out complete);
                return complete;
            }
        }

        internal bool InventoryTweenExists
        {
            get
            {
                IList tweens = InventoryTweens();
                return tweens != null && tweens.Count > 0;
            }
        }

        internal bool InventoryAnimationActive
        {
            get
            {
                bool playing;
                bool complete;
                ReadTweenState(InventoryTweens(), out playing, out complete);
                return playing;
            }
        }

        internal bool InventoryAnimationComplete
        {
            get
            {
                bool playing;
                bool complete;
                ReadTweenState(InventoryTweens(), out playing, out complete);
                return complete;
            }
        }

        internal int WindowTweenCount
        {
            get
            {
                IList tweens = WindowTweens();
                return tweens == null ? 0 : tweens.Count;
            }
        }

        internal List<RuntimePreviewTweenDiagnostic> WindowTweenDiagnostics()
        {
            List<RuntimePreviewTweenDiagnostic> result = new List<RuntimePreviewTweenDiagnostic>();
            IList tweens = WindowTweens();
            Type extensions = ResolveType("DG.Tweening.TweenExtensions, DOTween");
            float dotweenTimeScale = Convert.ToSingle(
                ResolveType("DG.Tweening.DOTween, DOTween").GetField("timeScale", AllStatic).GetValue(null),
                CultureInfo.InvariantCulture);
            for (int i = 0; tweens != null && i < tweens.Count; i++)
            {
                object tween = tweens[i];
                if (tween == null)
                    continue;
                result.Add(new RuntimePreviewTweenDiagnostic
                {
                    source = "Window/_scope/_tweens[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                    type = tween.GetType().FullName,
                    duration = Convert.ToSingle(GetField(tween, "duration"), CultureInfo.InvariantCulture),
                    fullDuration = Convert.ToSingle(GetField(tween, "fullDuration"), CultureInfo.InvariantCulture),
                    elapsed = Convert.ToSingle(
                        InvokeStatic(extensions, "Elapsed", tween, false),
                        CultureInfo.InvariantCulture),
                    position = Convert.ToSingle(GetProperty(tween, "position"), CultureInfo.InvariantCulture),
                    fullPosition = Convert.ToSingle(GetProperty(tween, "fullPosition"), CultureInfo.InvariantCulture),
                    timeScale = Convert.ToSingle(GetField(tween, "timeScale"), CultureInfo.InvariantCulture),
                    dotweenTimeScale = dotweenTimeScale,
                    timeTimeScale = Time.timeScale,
                    unscaledDeltaTime = Time.unscaledDeltaTime,
                    isPlaying = Convert.ToBoolean(
                        InvokeStatic(extensions, "IsPlaying", tween),
                        CultureInfo.InvariantCulture),
                    isComplete = Convert.ToBoolean(
                        InvokeStatic(extensions, "IsComplete", tween),
                        CultureInfo.InvariantCulture)
                });
            }
            return result;
        }

        internal int UiInstanceId { get { return _ui == null ? 0 : _ui.GetHashCode(); } }
        internal int MarketInstanceId { get { return _market == null ? 0 : _market.GetHashCode(); } }

        internal List<RuntimePreviewWindowCallbackRecord> WindowCallbacks
        {
            get { return _windowCallbacks; }
        }

        internal void InstrumentWindowOpenTween()
        {
            IList tweens = WindowTweens();
            if (tweens == null || tweens.Count == 0)
            {
                RecordWindowCallback("ProbeMissingTween", null, null);
                return;
            }
            Type callbackType = ResolveType("DG.Tweening.TweenCallback, DOTween");
            for (int i = 0; i < tweens.Count; i++)
            {
                object tween = tweens[i];
                if (tween == null || tween.GetType().FullName != "DG.Tweening.Sequence")
                    continue;
                int sequenceId = tween.GetHashCode();
                if (!_instrumentedSequences.Add(sequenceId))
                    continue;
                Delegate originalComplete = GetField(tween, "onComplete") as Delegate;
                Delegate originalKill = GetField(tween, "onKill") as Delegate;
                if (originalComplete == null || originalKill == null)
                    throw new InvalidOperationException("Window Sequence lost its original OnComplete/OnKill delegates before instrumentation.");
                RuntimePreviewWindowTweenProbe probe = new RuntimePreviewWindowTweenProbe
                {
                    Owner = this,
                    Tween = tween,
                    SequenceInstanceId = sequenceId,
                    OriginalOnCompletePresent = originalComplete != null,
                    OriginalOnKillPresent = originalKill != null
                };
                Delegate completeProbe = Delegate.CreateDelegate(
                    callbackType,
                    probe,
                    probe.GetType().GetMethod("OnComplete"));
                Delegate killProbe = Delegate.CreateDelegate(
                    callbackType,
                    probe,
                    probe.GetType().GetMethod("OnKill"));
                SetField(tween, "onComplete", Delegate.Combine(originalComplete, completeProbe));
                SetField(tween, "onKill", Delegate.Combine(originalKill, killProbe));
                _windowProbes.Add(probe);
                RecordWindowCallback("ProbeAttached", tween, probe);
            }
        }

        internal void RecordWindowCallback(
            string eventName,
            object tween,
            RuntimePreviewWindowTweenProbe probe)
        {
            try
            {
                object windowMotion = GetField(_market, "_windowMotion");
                object scope = GetField(windowMotion, "_scope");
                GameObject scopeRoot = GetField(scope, "_root") as GameObject;
                RectTransform pivot = GetField(windowMotion, "_pivot") as RectTransform;
                IList scopeTweens = GetField(scope, "_tweens") as IList;
                Type extensions = ResolveType("DG.Tweening.TweenExtensions, DOTween");
                bool completeBeforeKill = probe != null && probe.Completed;
                _windowCallbacks.Add(new RuntimePreviewWindowCallbackRecord
                {
                    eventName = eventName,
                    classification = eventName == "OnKill"
                        ? (completeBeforeKill ? "complete-then-kill" : "kill-without-complete")
                        : eventName == "OnComplete" ? "natural-complete-candidate" : "instrumentation",
                    realtime = Time.realtimeSinceStartup,
                    duration = tween == null ? 0f : Convert.ToSingle(GetField(tween, "duration"), CultureInfo.InvariantCulture),
                    elapsed = tween == null ? 0f : Convert.ToSingle(
                        InvokeStatic(extensions, "Elapsed", tween, false),
                        CultureInfo.InvariantCulture),
                    timeScale = tween == null ? 0f : Convert.ToSingle(GetField(tween, "timeScale"), CultureInfo.InvariantCulture),
                    dotweenTimeScale = Convert.ToSingle(
                        ResolveType("DG.Tweening.DOTween, DOTween").GetField("timeScale", AllStatic).GetValue(null),
                        CultureInfo.InvariantCulture),
                    timeTimeScale = Time.timeScale,
                    unscaledDeltaTime = Time.unscaledDeltaTime,
                    isPlaying = tween != null && Convert.ToBoolean(
                        InvokeStatic(extensions, "IsPlaying", tween),
                        CultureInfo.InvariantCulture),
                    isComplete = tween != null && Convert.ToBoolean(
                        InvokeStatic(extensions, "IsComplete", tween),
                        CultureInfo.InvariantCulture),
                    generation = Convert.ToInt32(GetProperty(scope, "Generation"), CultureInfo.InvariantCulture),
                    scopeTweenCount = scopeTweens == null ? 0 : scopeTweens.Count,
                    scopeRootActive = scopeRoot != null && scopeRoot.activeSelf,
                    marketRootActive = WindowRootActive,
                    uiInstanceId = _ui == null ? 0 : _ui.GetHashCode(),
                    marketInstanceId = _market == null ? 0 : _market.GetHashCode(),
                    sequenceInstanceId = probe == null ? 0 : probe.SequenceInstanceId,
                    uiDisposed = _ui == null || Convert.ToBoolean(GetField(_ui, "_disposed"), CultureInfo.InvariantCulture),
                    marketDisposed = _market == null || Convert.ToBoolean(GetField(_market, "_disposed"), CultureInfo.InvariantCulture),
                    rootInstanceId = pivot == null ? 0 : pivot.gameObject.GetInstanceID(),
                    rootDestroyed = pivot == null,
                    originalOnCompletePresent = probe != null && probe.OriginalOnCompletePresent,
                    originalOnKillPresent = probe != null && probe.OriginalOnKillPresent,
                    completeBeforeKill = completeBeforeKill,
                    stackTrace = Environment.StackTrace
                });
            }
            catch (Exception exception)
            {
                _windowCallbacks.Add(new RuntimePreviewWindowCallbackRecord
                {
                    eventName = eventName,
                    classification = "ProbeError",
                    realtime = Time.realtimeSinceStartup,
                    stackTrace = Environment.StackTrace + "\n" + exception
                });
            }
        }

        internal void CloseFormalUi()
        {
            Invoke(_ui, "CloseFromUser");
        }

        internal void ReopenMarket() { OpenMarketEmpty(); }

        internal Button DetailTooltipTarget
        {
            get
            {
                object view = GetProperty(_detail, "View");
                return Invoke(view, "Button", "ResetChartButton") as Button;
            }
        }

        internal Button DetailBackButton
        {
            get { return Invoke(GetProperty(_detail, "View"), "Button", "BackButton") as Button; }
        }

        internal RectTransform TooltipRoot
        {
            get
            {
                object tooltip = GetField(_overlays, "_tooltip");
                if (tooltip == null)
                    return null;
                object view = GetField(tooltip, "_view");
                return GetProperty(view, "Root") as RectTransform;
            }
        }

        internal void ShowToast(string message, float duration, string kind)
        {
            Type toastKind = RequireRuntimeType("XingShangMod.UI.Infrastructure.UiToastKind");
            Invoke(_overlays, "ShowToast", message, duration, Enum.Parse(toastKind, kind));
        }

        internal List<RectTransform> ToastRoots()
        {
            List<RectTransform> result = new List<RectTransform>();
            object toast = GetField(_overlays, "_toast");
            if (toast == null)
                return result;
            IList entries = GetField(toast, "_entries") as IList;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                object view = GetField(entries[i], "_view");
                RectTransform root = GetProperty(view, "Root") as RectTransform;
                if (root != null && root.gameObject.activeSelf)
                    result.Add(root);
            }
            return result;
        }

        internal CanvasGroup ToastGroup(RectTransform root)
        {
            object toast = GetField(_overlays, "_toast");
            IList entries = GetField(toast, "_entries") as IList;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                object view = GetField(entries[i], "_view");
                if (GetProperty(view, "Root") as RectTransform == root)
                    return GetField(entries[i], "_group") as CanvasGroup;
            }
            return null;
        }

        internal bool ToastStackActive
        {
            get
            {
                object toast = GetField(_overlays, "_toast");
                RectTransform stack = toast == null ? null : GetField(toast, "_stack") as RectTransform;
                return stack != null && stack.gameObject.activeSelf;
            }
        }

        internal RuntimePreviewTradeExpectation PreviewTrade()
        {
            object cart = GetField(_market, "_cart");
            object orders = Invoke(cart, "GetOrders");
            object trading = GetProperty(_application, "Trading");
            object preview = Invoke(trading, "Preview", orders);
            if (!Convert.ToBoolean(GetProperty(preview, "Success"), CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Formal trade Preview failed: " + GetProperty(preview, "Message"));
            object capacity = GetProperty(preview, "CapacityAfter");
            return new RuntimePreviewTradeExpectation
            {
                CashBefore = WalletBalance(),
                CashAfter = Convert.ToInt64(GetProperty(preview, "CashAfter"), CultureInfo.InvariantCulture),
                DebtAfter = Convert.ToInt64(GetProperty(preview, "DebtAfter"), CultureInfo.InvariantCulture),
                RecordCount = ((IList)GetProperty(preview, "Records")).Count,
                ExpectedTradeHistoryCount = TradeHistoryCount() + ((IList)GetProperty(preview, "Records")).Count,
                CapacityAfterUsed = capacity == null ? -1 : Convert.ToInt32(GetProperty(capacity, "UsedSlots"), CultureInfo.InvariantCulture),
                CapacityAfterTotal = capacity == null ? -1 : Convert.ToInt32(GetProperty(capacity, "TotalSlots"), CultureInfo.InvariantCulture)
            };
        }

        internal void ClickCheckout()
        {
            object view = GetProperty(_market, "View");
            Button checkout = Invoke(view, "Button", "CheckoutButton") as Button;
            if (checkout == null || !checkout.IsInteractable())
                throw new InvalidOperationException("Formal checkout button is unavailable.");
            checkout.onClick.Invoke();
            if (!Convert.ToBoolean(GetProperty(_overlays, "HasModal"), CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Checkout did not open the formal confirmation Modal.");
        }

        internal void ClickModalConfirmTwice()
        {
            object modal = GetField(_overlays, "_modal");
            object view = GetField(modal, "_view");
            Button confirm = Invoke(view, "Button", "ConfirmButton") as Button;
            if (confirm == null)
                throw new InvalidOperationException("Formal Modal confirm button is unavailable.");
            confirm.onClick.Invoke();
            confirm.onClick.Invoke();
        }

        internal int TradeHistoryCount()
        {
            return ((IList)GetField(_state, "TradeHistory")).Count;
        }

        internal void EvaluateCapacity(out int used, out int total)
        {
            object snapshot = Invoke(_capacity, "Evaluate", _state, BuildDefinitions());
            used = Convert.ToInt32(GetProperty(snapshot, "UsedSlots"), CultureInfo.InvariantCulture);
            total = Convert.ToInt32(GetProperty(snapshot, "TotalSlots"), CultureInfo.InvariantCulture);
        }

        internal void FeedbackState(out bool created, out int activeTokens, out bool stampActive)
        {
            object feedback = GetField(_overlays, "_feedback");
            created = feedback != null;
            activeTokens = 0;
            stampActive = false;
            if (feedback == null)
                return;
            IList tokens = GetField(feedback, "_tokens") as IList;
            for (int i = 0; tokens != null && i < tokens.Count; i++)
            {
                GameObject token = GetProperty(tokens[i], "GameObject") as GameObject;
                if (token != null && token.activeSelf)
                    activeTokens++;
            }
            object stamp = GetField(feedback, "_stamp");
            GameObject stampObject = stamp == null ? null : GetProperty(stamp, "GameObject") as GameObject;
            stampActive = stampObject != null && stampObject.activeSelf;
        }

        private IList WindowTweens()
        {
            object motion = GetField(_market, "_windowMotion");
            object scope = GetField(motion, "_scope");
            return GetField(scope, "_tweens") as IList;
        }

        private IList InventoryTweens()
        {
            GameObject slot = InventorySlotRoot(0);
            object result = InvokeStatic(
                ResolveType("DG.Tweening.DOTween, DOTween"),
                "TweensById",
                slot,
                false,
                null);
            return result as IList;
        }

        private static void ReadTweenState(IList tweens, out bool playing, out bool complete)
        {
            playing = false;
            complete = tweens != null && tweens.Count > 0;
            for (int i = 0; tweens != null && i < tweens.Count; i++)
            {
                object tween = tweens[i];
                if (tween == null)
                    continue;
                bool isPlaying = Convert.ToBoolean(
                    InvokeStatic(ResolveType("DG.Tweening.TweenExtensions, DOTween"), "IsPlaying", tween),
                    CultureInfo.InvariantCulture);
                bool isComplete = Convert.ToBoolean(
                    InvokeStatic(ResolveType("DG.Tweening.TweenExtensions, DOTween"), "IsComplete", tween),
                    CultureInfo.InvariantCulture);
                playing |= isPlaying && !isComplete;
                complete &= isComplete;
            }
        }

        private object CardAt(int index)
        {
            IList cards = GetField(_market, "_cards") as IList;
            if (cards == null || index < 0 || index >= cards.Count)
                throw new ArgumentOutOfRangeException("index");
            return cards[index];
        }

        private object InventorySlotAt(int index)
        {
            object section = GetField(_inventory, "_longSection");
            IList slots = GetField(section, "_slots") as IList;
            if (slots == null || index < 0 || index >= slots.Count)
                throw new ArgumentOutOfRangeException("index");
            return slots[index];
        }

        private object FirstShortSlot { get { return ((IList)GetField(GetField(_inventory, "_shortSection"), "_slots"))[0]; } }
        internal GameObject ShortSlotRoot { get { return (GameObject)GetProperty(FirstShortSlot, "GameObject"); } }
        internal Graphic LoanCountdown { get { return ShortSlotRoot.transform.Find("MotionPivot/LoanMaturity").GetComponent<Graphic>(); } }
        internal string LoanTooltipBody { get { return (string)Invoke(FirstShortSlot, "TooltipBody"); } }
        internal int LoanWeek { get { return _html.items[0].history.Length - 1; } }
        internal bool HasBothMaturities(string body)
        {
            return body != null && body.Contains("市周 " + (LoanWeek + 2) + " 到期（余 2 周） · 1 件") &&
                body.Contains("市周 " + (LoanWeek + 20) + " 到期（余 20 周） · 1 件");
        }
        internal RectTransform InventoryMetric(string key)
        {
            return (RectTransform)Invoke(GetProperty(_inventory,"View"),"Rect",key);
        }
        internal int[] GetLoanDueWeeks()
        {
            IList batches = (IList)GetField(_state,"ShortBatches");
            int[] weeks = new int[batches.Count];
            for (int i = 0; i < weeks.Length; i++) weeks[i] = (int)GetField(batches[i],"DueWeek");
            return weeks;
        }
        internal void SetLoanDueWeeks(int[] weeks)
        {
            IList batches = (IList)GetField(_state,"ShortBatches");
            for (int i = 0; i < weeks.Length; i++) SetField(batches[i],"DueWeek",weeks[i]);
            Invoke(_inventory,"Refresh");
        }
        internal void SetInventoryAttributes(int life, int light, int strength)
        {
            SetProperty(_application,"Capacity",CreateCapacityService(life,light,strength));
            Invoke(_inventory,"Refresh");
        }
        internal void RestoreInventoryAttributes()
        {
            SetProperty(_application,"Capacity",_capacity);
            Invoke(_inventory,"Refresh");
        }
        internal Action TemporarilyEmptyInventory()
        {
            IList longs = (IList)GetField(_state,"Holdings"), shorts = (IList)GetField(_state,"ShortBatches");
            var savedLong = new ArrayList(longs);
            var savedShort = new ArrayList(shorts);
            longs.Clear();
            shorts.Clear();
            return delegate
            {
                foreach (object lot in savedLong) longs.Add(lot);
                foreach (object lot in savedShort) shorts.Add(lot);
                Invoke(_inventory,"Refresh");
            };
        }

        internal int CartEntryCount
        {
            get { return ((IList)GetField(GetField(_market, "_cart"), "_entries")).Count; }
        }

        internal int FirstCartQuantity
        {
            get
            {
                IList entries = GetField(GetField(_market, "_cart"), "_entries") as IList;
                return entries == null || entries.Count == 0
                    ? 0
                    : Convert.ToInt32(GetField(entries[0], "Quantity"), CultureInfo.InvariantCulture);
            }
        }

        internal int CartRowQuantity(int index)
        {
            IList rows = (IList)GetField(GetField(_market, "_cart"), "_pendingRows");
            return Convert.ToInt32(GetField(rows[index], "Quantity"), CultureInfo.InvariantCulture);
        }

        internal Button CartRowButton(int index, string key)
        {
            IList views = (IList)GetField(GetField(_market, "_cart"), "_views");
            return (Button)Invoke(GetField(views[index], "_view"), "Button", key + "Button");
        }

        internal Button DetailButton(string key)
        {
            return (Button)Invoke(GetProperty(_detail, "View"), "Button", key + "Button");
        }

        internal IEnumerator CheckHostModuleReplacement(Action<bool, string> check)
        {
            EventSystem previous = EventSystem.current;
            if (previous != null)
                previous.enabled = false;
            GameObject host = new GameObject("FixtureHost.EventSystem", typeof(EventSystem));
            Type type = RequireRuntimeType("XingShangMod.UI.Infrastructure.UiInputStack");
            object first = Construct(type);
            object second = null;
            try
            {
                Invoke(first, "EnsureEventSystem");
                BaseInputModule original = host.GetComponent<BaseInputModule>();
                Invoke(first, "Dispose");
                first = null;
                second = Construct(type);
                Invoke(second, "EnsureEventSystem");
                BaseInputModule current = (BaseInputModule)GetField(second, "_hostInputModule");
                check(current != null && current != original && current.gameObject == host,
                    "A host EventSystem without an input module must receive a live replacement on same-frame reopen.");
                yield return null;
                check(current != null && current.enabled && host.activeSelf && EventSystem.current == host.GetComponent<EventSystem>(),
                    "The replacement host input module must still work after deferred destruction.");
            }
            finally
            {
                if (first != null)
                    Invoke(first, "Dispose");
                if (second != null)
                    Invoke(second, "Dispose");
                host.SetActive(false);
                UnityEngine.Object.Destroy(host);
                if (previous != null)
                    previous.enabled = true;
            }
        }

        internal void SetItemStackOfFour(string itemId)
        {
            // The fixture carries 200 per slot and already owns two occupied slots out of four.
            SetField(Invoke(_items, "GetItem", itemId), "Weight", 50.0);
        }

        internal void PrepareLargeBackpack()
        {
            _capacity = CreateCapacityService(400);
            SetField(Invoke(_items, "GetItem", "01"), "Weight", 200.0);
            SetProperty(_application, "Capacity", _capacity);
            SetProperty(_application, "Trading", Construct(
                RequireRuntimeType("XingShangMod.Trading.TradingService"), _state, _wallet, BuildDefinitions(), _capacity));
        }

        internal int VisibleCartRows
        {
            get { return ((IList)GetField(GetField(_market, "_cart"), "_pendingRows")).Count; }
        }

        internal Button CartIncrementButton
        {
            get
            {
                IList views = GetField(GetField(_market, "_cart"), "_views") as IList;
                if (views == null || views.Count == 0)
                    return null;
                object view = GetField(views[0], "_view");
                return Invoke(view, "Button", "QuantityIncrementButton") as Button;
            }
        }

        internal Button CartDecrementButton
        {
            get
            {
                IList views = GetField(GetField(_market, "_cart"), "_views") as IList;
                if (views == null || views.Count == 0)
                    return null;
                object view = GetField(views[0], "_view");
                return Invoke(view, "Button", "QuantityDecrementButton") as Button;
            }
        }

        internal bool CartIncrementAvailable
        {
            get
            {
                Button increment = CartIncrementButton;
                return increment != null && increment.IsInteractable();
            }
        }

        internal void ClickCartIncrement()
        {
            Button button = CartIncrementButton;
            if (button == null || !button.IsInteractable())
                throw new InvalidOperationException("Cart increment is not interactable at the capacity boundary.");
            ExecuteEvents.Execute(
                button.gameObject,
                new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerClickHandler);
        }

        internal bool CheckoutInteractable
        {
            get
            {
                Button button = Invoke(GetProperty(_market, "View"), "Button", "CheckoutButton") as Button;
                return button != null && button.IsInteractable();
            }
        }

        internal Button CardActionButton(int index, string action)
        {
            object view = GetField(CardAt(index), "_view");
            return Invoke(view, "Button", action + "Button") as Button;
        }

        internal int FirstCardQuantity
        {
            get { return Convert.ToInt32(GetField(CardAt(0), "_quantity"), CultureInfo.InvariantCulture); }
        }

        internal Button CheckoutButton
        {
            get { return Invoke(GetProperty(_market, "View"), "Button", "CheckoutButton") as Button; }
        }

        internal void AdjustFixtureWallet(int delta)
        {
            bool applied = (bool)Invoke(_wallet, delta > 0 ? "TryDeposit" : "TrySpend", (long)Math.Abs(delta));
            if (!applied)
                throw new InvalidOperationException("Test wallet could not apply fixture adjustment.");
        }

        internal void OpenLongLedger()
        {
            object content = Construct(RequireRuntimeType("XingShangMod.UI.Infrastructure.UiModalContent"));
            SetField(content, "Title", "立契结算");
            IList lines = (IList)GetField(content, "Lines");
            Type lineType = RequireRuntimeType("XingShangMod.UI.Infrastructure.UiModalLine");
            for (int i = 1; i <= 30; i++)
            {
                object line = Construct(lineType);
                SetField(line, "Label", "借货 · 第" + i + "笔货契");
                SetField(line, "Quantity", "× 1");
                SetField(line, "Amount", "+97 文");
                lines.Add(line);
            }
            Invoke(_overlays, "ShowModal", content);
        }

        internal Button ModalConfirmButton
        {
            get
            {
                object modal = GetField(_overlays, "_modal");
                object view = GetField(modal, "_view");
                return Invoke(view, "Button", "ConfirmButton") as Button;
            }
        }

        internal void ClickCardAction(int index, string action)
        {
            Button button = CardActionButton(index, action);
            if (button == null || !button.IsInteractable())
                throw new InvalidOperationException("Card action is not interactable: " + index + "/" + action);
            button.onClick.Invoke();
        }

        internal void AddFromDetail(string itemId, string action, int quantity, RectTransform source)
        {
            Type tradeAction = RequireRuntimeType("XingShangMod.Trading.TradeAction");
            Invoke(_market, "AddFromDetail", itemId, Enum.Parse(tradeAction, action), quantity, source);
        }

        internal void ClearToasts()
        {
            object toast = GetField(_overlays, "_toast");
            if (toast != null)
                Invoke(toast, "Clear");
        }

        internal List<string> ToastMessages()
        {
            List<string> result = new List<string>();
            object toast = GetField(_overlays, "_toast");
            IList entries = toast == null ? null : GetField(toast, "_entries") as IList;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                object view = GetField(entries[i], "_view");
                result.Add(Invoke(view, "Text", "MessageText") is Text
                    ? ((Text)Invoke(view, "Text", "MessageText")).text
                    : string.Empty);
            }
            return result;
        }

        internal List<Color> ToastAccentColors()
        {
            List<Color> result = new List<Color>();
            object toast = GetField(_overlays, "_toast");
            IList entries = toast == null ? null : GetField(toast, "_entries") as IList;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                object view = GetField(entries[i], "_view");
                Image icon = Invoke(view, "Image", "StatusIcon") as Image;
                result.Add(icon == null ? Color.clear : icon.color);
            }
            return result;
        }

        internal void PauseActiveTokenTweenForSampling()
        {
            InvokeStatic(
                ResolveType("DG.Tweening.TweenExtensions, DOTween"),
                "TogglePause",
                ActiveTokenTween());
        }

        internal void SeekActiveTokenTweenForSampling(float seconds)
        {
            InvokeStatic(
                ResolveType("DG.Tweening.TweenExtensions, DOTween"),
                "Goto",
                ActiveTokenTween(),
                seconds,
                false);
        }

        internal void ResumeActiveTokenTweenAfterSampling()
        {
            InvokeStatic(
                ResolveType("DG.Tweening.TweenExtensions, DOTween"),
                "TogglePause",
                ActiveTokenTween());
        }

        private object ActiveTokenTween()
        {
            object feedback = GetField(_overlays, "_feedback");
            IDictionary tweens = feedback == null ? null : GetField(feedback, "_tokenTweens") as IDictionary;
            foreach (DictionaryEntry pair in tweens)
            {
                RectTransform root = GetProperty(pair.Key, "Root") as RectTransform;
                if (root != null && root.gameObject.activeSelf)
                    return pair.Value;
            }
            throw new InvalidOperationException("The flight sampling probe requires one active token tween.");
        }

        internal List<RuntimePreviewTokenDiagnostic> TokenDiagnostics()
        {
            List<RuntimePreviewTokenDiagnostic> result = new List<RuntimePreviewTokenDiagnostic>();
            object feedback = GetField(_overlays, "_feedback");
            IDictionary tweens = feedback == null ? null : GetField(feedback, "_tokenTweens") as IDictionary;
            if (tweens == null)
                return result;
            Type extensions = ResolveType("DG.Tweening.TweenExtensions, DOTween");
            foreach (DictionaryEntry pair in tweens)
            {
                object view = pair.Key;
                object tween = pair.Value;
                RectTransform root = GetProperty(view, "Root") as RectTransform;
                if (root == null || !root.gameObject.activeSelf)
                    continue;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                result.Add(new RuntimePreviewTokenDiagnostic
                {
                    position = Convert.ToSingle(GetProperty(tween, "position"), CultureInfo.InvariantCulture),
                    duration = Convert.ToSingle(GetField(tween, "duration"), CultureInfo.InvariantCulture),
                    alpha = group == null ? 0f : group.alpha,
                    scale = root.localScale,
                    anchoredPosition = root.anchoredPosition,
                    timeScale = Convert.ToSingle(GetField(tween, "timeScale"), CultureInfo.InvariantCulture),
                    isPlaying = Convert.ToBoolean(
                        InvokeStatic(extensions, "IsPlaying", tween),
                        CultureInfo.InvariantCulture)
                });
            }
            return result;
        }

        internal void ShowRanking()
        {
            Invoke(_market, "ShowRanking");
        }

        internal bool RankingAscending
        {
            get { return Convert.ToBoolean(GetProperty(GetField(_market, "_ranking"), "Ascending"), CultureInfo.InvariantCulture); }
        }

        internal int RankingRowCount
        {
            get
            {
                IList rows = GetField(GetField(_market, "_ranking"), "_rows") as IList;
                int count = 0;
                for (int i = 0; rows != null && i < rows.Count; i++)
                {
                    GameObject root = GetProperty(rows[i], "GameObject") as GameObject;
                    if (root != null && root.activeSelf)
                        count++;
                }
                return count;
            }
        }

        internal List<string> RankingItemIds()
        {
            List<Tuple<int, string>> ordered = new List<Tuple<int, string>>();
            IList rows = GetField(GetField(_market, "_ranking"), "_rows") as IList;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                object row = rows[i];
                GameObject gameObject = GetProperty(row, "GameObject") as GameObject;
                object view = GetProperty(row, "View");
                RectTransform root = GetProperty(view, "Root") as RectTransform;
                if (gameObject != null && gameObject.activeSelf)
                    ordered.Add(Tuple.Create(root.GetSiblingIndex(), Convert.ToString(GetProperty(row, "ItemId"), CultureInfo.InvariantCulture)));
            }
            ordered.Sort(delegate (Tuple<int, string> left, Tuple<int, string> right) { return left.Item1.CompareTo(right.Item1); });
            List<string> result = new List<string>();
            for (int i = 0; i < ordered.Count; i++)
                result.Add(ordered[i].Item2);
            return result;
        }

        internal List<double> RankingReturns()
        {
            List<double> result = new List<double>();
            List<string> ids = RankingItemIds();
            for (int i = 0; i < ids.Count; i++)
            {
                IEnumerable history = Invoke(_application, "GetItemHistory", ids[i]) as IEnumerable;
                double first = 0.0;
                double last = 0.0;
                bool found = false;
                foreach (object point in history)
                {
                    double price = Convert.ToDouble(GetField(point, "Price"), CultureInfo.InvariantCulture);
                    if (!found)
                    {
                        first = price;
                        found = true;
                    }
                    last = price;
                }
                result.Add(found && first != 0.0 ? last / first - 1.0 : 0.0);
            }
            return result;
        }

        internal ScrollRect RankingScroll
        {
            get { return GetField(GetField(_market, "_ranking"), "_scroll") as ScrollRect; }
        }

        internal RectTransform RankingContent
        {
            get { return GetField(GetField(_market, "_ranking"), "_rowContent") as RectTransform; }
        }

        internal RectTransform RankingRoot
        {
            get { return GetField(GetField(_market, "_ranking"), "_root") as RectTransform; }
        }

        internal RectTransform GoodsArea
        {
            get { return Invoke(GetProperty(_market, "View"), "Rect", "GoodsArea") as RectTransform; }
        }

        internal RectTransform NewsTickerRoot
        {
            get { return Invoke(GetProperty(_market, "View"), "Rect", "NewsTicker") as RectTransform; }
        }

        internal RectTransform NewsTickerTrack
        {
            get
            {
                RectTransform root = NewsTickerRoot;
                return root == null ? null : root.Find("Viewport/Track") as RectTransform;
            }
        }

        internal bool CartRailActive
        {
            get
            {
                RectTransform rail = Invoke(GetProperty(_market, "View"), "Rect", "CartRail") as RectTransform;
                return rail != null && rail.gameObject.activeSelf;
            }
        }

        internal bool RankingPagerActive
        {
            get
            {
                RectTransform pager = GoodsArea.Find("SectionHeader/Pager") as RectTransform;
                return pager != null && pager.gameObject.activeSelf;
            }
        }

        internal void ClickRankingSort()
        {
            object ranking = GetField(_market, "_ranking");
            object chrome = GetField(ranking, "_chrome");
            Button sort = GetField(chrome, "_sortButton") as Button;
            if (sort == null || !sort.IsInteractable())
                throw new InvalidOperationException("Ranking Chrome sort button is unavailable.");
            sort.onClick.Invoke();
        }

        internal void ClickRankingRow(string itemId)
        {
            IList rows = GetField(GetField(_market, "_ranking"), "_rows") as IList;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                object row = rows[i];
                GameObject gameObject = GetProperty(row, "GameObject") as GameObject;
                if (gameObject == null || !gameObject.activeSelf ||
                    !string.Equals(Convert.ToString(GetProperty(row, "ItemId"), CultureInfo.InvariantCulture), itemId, StringComparison.Ordinal))
                    continue;
                object view = GetProperty(row, "View");
                Button open = Invoke(view, "Button", "OpenButton") as Button;
                if (open == null || !open.IsInteractable())
                    throw new InvalidOperationException("Ranking row is not interactable: " + itemId);
                open.onClick.Invoke();
                return;
            }
            throw new InvalidOperationException("Ranking row not found: " + itemId);
        }

        internal string DetailItemId
        {
            get { return GetField(_detail, "_itemId") as string; }
        }

        internal Component DetailChart
        {
            get { return GetProperty(GetField(_detail, "_chart"), "Chart") as Component; }
        }

        internal Component AssetsChart
        {
            get { return (Component)GetProperty(GetField(GetField(_inventory, "_assetsPage"), "_chart"), "Chart"); }
        }

        internal Button AssetsChartReset
        {
            get { return (Button)Invoke(GetField(GetField(_inventory, "_assetsPage"), "_view"), "Button", "ResetChartButton"); }
        }

        internal int DetailHistoryCount
        {
            get { return ((ICollection)Invoke(_application, "GetItemHistory", "01")).Count; }
        }

        internal int DetailCurrentWeek
        {
            get { return Convert.ToInt32(GetField(GetField(_state, "Clock"), "MarketWeekIndex"), CultureInfo.InvariantCulture); }
        }

        internal void ConfigureFlightCapacityFixture()
        {
            _capacity = CreateCapacityService(31);
            SetProperty(_application, "Capacity", _capacity);
            SetProperty(_application, "Trading", Construct(
                RequireRuntimeType("XingShangMod.Trading.TradingService"), _state, _wallet, BuildDefinitions(), _capacity));
        }

        internal void ConfigureAdvancementFixture()
        {
            object clone = InvokeStatic(RequireRuntimeType("XingShangMod.Persistence.RunStateCloner"), "Clone", _state);
            const int currentWeek = 77;
            IList appeared = GetField(clone, "AppearedItemIds") as IList;
            for (int i = appeared.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(Convert.ToString(appeared[i]), "01", StringComparison.Ordinal))
                    appeared.RemoveAt(i);
            }
            FilterHistory(clone, "PriceHistory", currentWeek, true);
            FilterHistory(clone, "IndexHistory", currentWeek, false);
            ((IList)GetField(clone, "SecurityBindings")).Clear();
            ((IList)GetField(clone, "AnnualIndexBindings")).Clear();
            object repository = Construct(RequireRuntimeType("XingShangMod.Data.WeeklyDataRepository"));
            object selector = Construct(
                RequireRuntimeType("XingShangMod.Market.MarketSelector"),
                repository,
                Convert.ToInt32(GetField(clone, "RunSeed"), CultureInfo.InvariantCulture));
            object item = Invoke(_items, "GetItem", "01");
            object binding = Invoke(selector, "CreateSecurityBinding", item, currentWeek);
            SetField(binding, "LastPrice", _html.items[0].history[currentWeek].close);
            ((IList)GetField(clone, "SecurityBindings")).Add(binding);
            object engine = Construct(
                RequireRuntimeType("XingShangMod.Market.MarketEngine"),
                clone,
                repository,
                GetProperty(_items, "Items"));
            SetProperty(_application, "State", clone);
            SetProperty(_application, "WeeklyData", repository);
            SetProperty(_application, "Market", engine);
            SetProperty(_application, "Clock", Construct(
                RequireRuntimeType("XingShangMod.Market.MarketClock"),
                GetField(clone, "Clock")));
            object definitions = BuildDefinitions();
            SetProperty(_application, "Assets", Construct(
                RequireRuntimeType("XingShangMod.Trading.AssetService"), clone, _wallet, definitions));
            SetProperty(_application, "Trading", Construct(
                RequireRuntimeType("XingShangMod.Trading.TradingService"), clone, _wallet, definitions, _capacity));
            _state = clone;
        }

        internal void AdvanceRealWeekAndRefresh()
        {
            object clock = GetField(_state, "Clock");
            SetField(clock, "MarketWeekIndex", 78);
            object time = Construct(RequireRuntimeType("XingShangMod.Market.MarketTime"), 2, 7, 2);
            Invoke(GetProperty(_application, "Market"), "AdvanceWeeks", 1, time);
            Invoke(_ui, "Refresh");
        }

        internal CanvasScaler[] PreviewCanvasScalers
        {
            get { return UnityEngine.Object.FindObjectsOfType<CanvasScaler>(); }
        }

        private static void FilterHistory(object state, string fieldName, int currentWeek, bool onlyFirstItem)
        {
            IList history = GetField(state, fieldName) as IList;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                object point = history[i];
                string itemId = Convert.ToString(GetField(point, "ItemId"), CultureInfo.InvariantCulture);
                int week = Convert.ToInt32(GetField(point, "MarketWeekIndex"), CultureInfo.InvariantCulture);
                if (week > currentWeek || (onlyFirstItem && itemId != "01"))
                    history.RemoveAt(i);
            }
        }

        internal void AssertBusinessStateUnchanged(string caseName)
        {
            string currentHash = StateHash();
            if (!string.Equals(currentHash, _initialStateHash, StringComparison.Ordinal))
                throw new InvalidOperationException(caseName + " mutated RunState while only browsing/previewing UI.");
            long balance = WalletBalance();
            if (balance != _initialCash)
                throw new InvalidOperationException(caseName + " mutated the fixture wallet: " + _initialCash + " -> " + balance + ".");
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            DisposeUserInterface();
            for (int i = 0; i < _scriptableObjects.Count; i++)
            {
                if (_scriptableObjects[i] != null)
                    UnityEngine.Object.Destroy(_scriptableObjects[i]);
            }
            _scriptableObjects.Clear();
            if (_assemblyResolver != null)
                AppDomain.CurrentDomain.AssemblyResolve -= _assemblyResolver;
            _assemblyResolver = null;
            _application = null;
            _state = null;
            _disposed = true;
        }

        private void OpenMarketEmpty()
        {
            bool opened = Convert.ToBoolean(Invoke(_ui, "TryOpenMarket", new object[] { null }), CultureInfo.InvariantCulture);
            if (!opened)
                throw new InvalidOperationException("Formal ModUserInterface.TryOpenMarket returned false for market-empty.");
        }

        private void OpenMarketWithCart()
        {
            OpenMarketEmpty();
            object cart = GetField(_market, "_cart");
            Type tradeAction = RequireRuntimeType("XingShangMod.Trading.TradeAction");
            Invoke(cart, "Add", "01", Enum.Parse(tradeAction, "Buy"), 2);
            Invoke(cart, "Add", "07", Enum.Parse(tradeAction, "Borrow"), 1);
            Invoke(_market, "Refresh");
        }

        private void PresentRealCartPreview()
        {
            object cart = GetField(_market, "_cart");
            object orders = Invoke(cart, "GetOrders");
            Invoke(_confirmation, "Present", orders, "立契结算", null);
            bool hasModal = Convert.ToBoolean(GetProperty(_overlays, "HasModal"), CultureInfo.InvariantCulture);
            if (!hasModal)
                throw new InvalidOperationException("Formal TradeConfirmation did not open a Modal; inspect the real Preview failure.");
        }

        private void OpenDetail()
        {
            Invoke(_ui, "OpenDetail", "01");
            bool isOpen = Convert.ToBoolean(GetProperty(_detail, "IsOpen"), CultureInfo.InvariantCulture);
            if (!isOpen)
                throw new InvalidOperationException("Formal MarketItemDetail did not open fixture item 01.");
        }

        private void ShowDetailTooltip()
        {
            object view = GetProperty(_detail, "View");
            Button reset = Invoke(view, "Button", "ResetChartButton") as Button;
            if (reset == null)
                throw new InvalidOperationException("Formal detail ResetChartButton was not bound.");
            if (EventSystem.current == null)
                throw new InvalidOperationException("Tooltip preview requires the formal EventSystem.");
            EventSystem.current.SetSelectedGameObject(reset.gameObject);
        }

        private object BuildRunState()
        {
            object state = Construct(RequireRuntimeType("XingShangMod.Persistence.RunState"));
            int currentWeek = _html.items[0].history.Length - 1;
            SetField(state, "RunId", "visual-fixture-78w");
            SetField(state, "RunSeed", 4908);
            SetField(state, "Item09Unlocked", true);
            object lastIndex = CreateWeeklyPoint("Index", _html.index.history[currentWeek], currentWeek, 0.0);
            SetField(state, "CurrentIndex", Convert.ToDouble(GetField(lastIndex, "Price"), CultureInfo.InvariantCulture));
            RuntimePreviewHtmlPoint last = _html.items[0].history[currentWeek];
            int year;
            int month;
            int round;
            ParsePointTime(last.t, currentWeek, out year, out month, out round);
            SetField(state, "CurrentGameYear", year);
            object clock = GetField(state, "Clock");
            SetField(clock, "MarketWeekIndex", currentWeek);
            SetField(clock, "CurrentGameMonth", month);
            SetField(clock, "LastGameRound", round);
            SetField(clock, "WeeksThisGameMonth", ((currentWeek % 4) + 1));

            double previousIndex = 0.0;
            for (int i = 0; i < _html.index.history.Length; i++)
            {
                double close = _html.index.history[i].close;
                double weeklyReturn = i == 0 || previousIndex == 0.0 ? 0.0 : close / previousIndex - 1.0;
                AddToList(state, "IndexHistory", CreateWeeklyPoint("Index", _html.index.history[i], i, weeklyReturn));
                previousIndex = close;
            }
            AddToList(state, "IndexHistory", CreatePoisonPoint("Index", currentWeek + 1, currentWeek));
            AddToList(state, "IndexHistory", CreatePoisonPoint("Index", currentWeek + 10, currentWeek));

            for (int itemIndex = 0; itemIndex < _html.items.Length; itemIndex++)
            {
                RuntimePreviewHtmlItem item = _html.items[itemIndex];
                string itemId = NormalizeItemId(item.itemId, item.key);
                AddToList(state, "AppearedItemIds", itemId);
                double previous = 0.0;
                for (int i = 0; i < item.history.Length; i++)
                {
                    double close = item.history[i].close;
                    double weeklyReturn = i == 0 || previous == 0.0 ? 0.0 : close / previous - 1.0;
                    AddToList(state, "PriceHistory", CreateWeeklyPoint(itemId, item.history[i], i, weeklyReturn));
                    previous = close;
                }
                AddToList(state, "PriceHistory", CreatePoisonPoint(itemId, currentWeek + 1, currentWeek));
                AddToList(state, "PriceHistory", CreatePoisonPoint(itemId, currentWeek + 10, currentWeek));
                object binding = Construct(RequireRuntimeType("XingShangMod.Domain.SecurityBinding"));
                SetField(binding, "ItemId", itemId);
                SetField(binding, "SecurityId", "fixture-" + itemId);
                SetField(binding, "Code", "HTML-V4");
                SetField(binding, "AppearedWeek", 0);
                SetField(binding, "NextRecordIndex", item.history.Length);
                SetField(binding, "LastPrice", item.history[item.history.Length - 1].close);
                AddToList(state, "SecurityBindings", binding);
            }
            BuildPositionsAndAssets(state, currentWeek);
            GenerateFormalNews(state);
            return state;
        }

        private void BuildPositionsAndAssets(object state, int currentWeek)
        {
            RuntimePreviewHtmlItem longItem = FindItem("04");
            RuntimePreviewHtmlItem shortItem = FindItem("19");
            int longQuantity = 6;
            int shortQuantity = 2;
            long longUnitCost = Math.Max(1L, (long)Math.Round(longItem.currentPrice * 0.94, MidpointRounding.AwayFromZero));
            long shortUnitPrice = Math.Max(1L, (long)Math.Round(shortItem.currentPrice * 1.06, MidpointRounding.AwayFromZero));
            long longCost = checked(longUnitCost * longQuantity);
            long shortProceeds = (long)decimal.Floor((decimal)shortUnitPrice * shortQuantity * 0.97m);
            long cash = checked(_html.startingCash - longCost + shortProceeds);

            object holding = Construct(RequireRuntimeType("XingShangMod.Domain.Holding"));
            SetField(holding, "ItemId", "04");
            SetField(holding, "Quantity", longQuantity);
            SetField(holding, "TotalCost", longCost);
            AddToList(state, "Holdings", holding);

            object batch = Construct(RequireRuntimeType("XingShangMod.Domain.ShortBatch"));
            SetField(batch, "BatchId", "fixture-short-19");
            SetField(batch, "ItemId", "19");
            SetField(batch, "Quantity", shortQuantity);
            SetField(batch, "OpenWeek", currentWeek - 4);
            SetField(batch, "Proceeds", shortProceeds);
            SetField(batch, "DueWeek", currentWeek + 20);
            AddToList(state, "ShortBatches", batch);

            AddToList(state, "TradeHistory", CreateTradeRecord(
                "fixture-buy-04", "04", "Buy", string.Empty, longQuantity, longUnitCost, -longCost, 0));
            if (_options.Cases.Length == 1 && (_options.Cases[0] == "inventory-maturities" || _options.Cases[0] == "loan-proof"))
            {
                long soonProceeds = shortProceeds / 2;
                object soon = Construct(RequireRuntimeType("XingShangMod.Domain.ShortBatch"));
                SetField(soon, "BatchId", "fixture-soon-19");
                SetField(soon, "ItemId", "19");
                SetField(soon, "Quantity", 1);
                SetField(soon, "OpenWeek", currentWeek - 22);
                SetField(soon, "DueWeek", currentWeek + 2);
                SetField(soon, "Proceeds", soonProceeds);
                AddToList(state, "ShortBatches", soon);
                AddToList(state, "TradeHistory", CreateTradeRecord(
                    "fixture-borrow-soon", "19", "Borrow", "fixture-soon-19", 1, shortUnitPrice, soonProceeds, currentWeek - 22));
                SetField(batch, "Quantity", 1);
                SetField(batch, "Proceeds", shortProceeds - soonProceeds);
                AddToList(state, "TradeHistory", CreateTradeRecord(
                    "fixture-borrow-later", "19", "Borrow", "fixture-short-19", 1, shortUnitPrice, shortProceeds - soonProceeds, currentWeek - 4));
            }
            else
            {
                AddToList(state, "TradeHistory", CreateTradeRecord(
                    "fixture-borrow-19", "19", "Borrow", "fixture-short-19", shortQuantity, shortUnitPrice, shortProceeds, currentWeek - 4));
            }

            for (int week = 0; week <= currentWeek; week++)
            {
                double longValue = longItem.history[week].close * longQuantity;
                bool hasBorrow = week >= currentWeek - 4;
                double shortLiability = hasBorrow ? shortItem.history[week].close * shortQuantity : 0;
                long snapshotCash = hasBorrow ? cash : cash - shortProceeds;
                object point = Construct(RequireRuntimeType("XingShangMod.Domain.AssetPoint"));
                SetField(point, "MarketWeekIndex", week);
                int year;
                int month;
                int round;
                ParsePointTime(longItem.history[week].t, week, out year, out month, out round);
                SetField(point, "GameYear", year);
                SetField(point, "GameMonth", month);
                SetField(point, "GameRound", round);
                SetField(point, "Cash", snapshotCash);
                SetField(point, "LongValue", longValue);
                SetField(point, "ShortLiability", shortLiability);
                SetField(point, "NetAssets", snapshotCash + longValue - shortLiability);
                AddToList(state, "AssetHistory", point);
            }
            _initialCash = cash;
        }

        private void GenerateFormalNews(object state)
        {
            object service = Construct(RequireRuntimeType("XingShangMod.Progression.NewsService"), _news);
            IList prices = GetField(state, "PriceHistory") as IList;
            int currentWeek = _html.items[0].history.Length - 1;
            List<object> currentPoints = new List<object>();
            for (int i = 0; i < prices.Count; i++)
            {
                object point = prices[i];
                if (Convert.ToInt32(GetField(point, "MarketWeekIndex"), CultureInfo.InvariantCulture) == currentWeek)
                    currentPoints.Add(point);
            }
            for (int i = 0; i < currentPoints.Count; i++)
                Invoke(service, "Generate", state, currentPoints[i]);
        }

        private void BuildApplication()
        {
            _application = Construct(
                RequireRuntimeType("XingShangMod.App.ModApplication"),
                new Action<string>(Debug.Log),
                new Action<string>(Debug.LogWarning),
                new Action<string>(Debug.LogError));
            _wallet = CreateWallet(_initialCash);
            object definitions = BuildDefinitions();
            _capacity = CreateCapacityService();
            object assets = Construct(
                RequireRuntimeType("XingShangMod.Trading.AssetService"), _state, _wallet, definitions);
            object trading = Construct(
                RequireRuntimeType("XingShangMod.Trading.TradingService"), _state, _wallet, definitions, _capacity);
            SetProperty(_application, "IsEnabled", true);
            SetProperty(_application, "State", _state);
            SetProperty(_application, "Items", _items);
            SetProperty(_application, "NewsCatalog", _news);
            SetProperty(_application, "News", Construct(RequireRuntimeType("XingShangMod.Progression.NewsService"), _news));
            SetProperty(_application, "Wallet", _wallet);
            SetProperty(_application, "Capacity", _capacity);
            SetProperty(_application, "Assets", assets);
            SetProperty(_application, "Trading", trading);
            IDictionary applicationDefinitions = GetField(_application, "_itemDefinitions") as IDictionary;
            foreach (DictionaryEntry pair in (IDictionary)definitions)
                applicationDefinitions.Add(pair.Key, pair.Value);
        }

        private object CreateWallet(long cash)
        {
            Type gameStatType = ResolveType("Mortal.Core.GameStat, Mortal.Core");
            Type playerStatsType = ResolveType("Mortal.Core.PlayerStatData, Mortal.Core");
            ScriptableObject money = ScriptableObject.CreateInstance(gameStatType);
            _fixtureMoney = money;
            ScriptableObject stats = ScriptableObject.CreateInstance(playerStatsType);
            _scriptableObjects.Add(money);
            _scriptableObjects.Add(stats);
            SetField(money, "_type", Enum.Parse(gameStatType.GetNestedType("GameStatType") ?? ResolveType("Mortal.Core.GameStatType, Mortal.Core"), "銀兩"));
            SetField(money, "_value", checked((int)cash));
            SetField(money, "_min", 0);
            SetField(money, "_max", int.MaxValue);
            Type listType = typeof(List<>).MakeGenericType(gameStatType);
            object list = Activator.CreateInstance(listType);
            ((IList)list).Add(money);
            SetField(stats, "_list", list);
            return Construct(RequireRuntimeType("XingShangMod.Trading.MortalWallet"), stats);
        }

        private void ConfigureProfitFixture()
        {
            int end = _html.items[0].history.Length - 1;
            var lots = (IList)GetField(_state,"Holdings");
            SetField(lots[0],"Quantity",1);
            SetField(lots[0],"TotalCost",1000L);
            ((IList)GetField(_state,"ShortBatches")).Clear();
            var trades = (IList)GetField(_state,"TradeHistory");
            trades.Clear();
            trades.Add(CreateTradeRecord("asset-proof-buy","04","Buy","",1,1000,-1000,0));
            var values = new double[end+1];
            foreach (object point in (IList)GetField(_state,"PriceHistory"))
            {
                int week = (int)GetField(point,"MarketWeekIndex");
                if ((string)GetField(point,"ItemId") != "04" || week < 0 || week > end) continue;
                double mark = week == end ? 900 : Math.Round(1000 + 165*Math.Sin(week*.24));
                if (_options.Cases[0] == "asset-sign-ink" && week >= end - 25)
                    mark = 1000 + 10 * ReturnInkPercents[week - (end - 25)];
                values[week] = mark;
                SetField(point,"Price",mark);
            }
            foreach (object point in (IList)GetField(_state,"AssetHistory"))
            {
                int week = (int)GetField(point,"MarketWeekIndex");
                long cash = week < end-11 ? 5400 : -125;
                double value = values[week];
                SetField(point,"Cash",cash);
                SetField(point,"LongValue",value);
                SetField(point,"ShortLiability",0d);
                SetField(point,"NetAssets",cash+value);
            }
            _initialCash = -125;
        }

        internal static readonly int[] ReturnInkPercents =
            { 12,9,6,4,8,13,10,7,3,0,0,-4,-9,-6,-3,5,9,6,-8,-11,-7,-2,0,0,6,12 };

        private object AssetsObject { get { return GetField(_inventory,"_assetsPage"); } }
        internal Button AssetsSwitch { get { return (Button)GetField(AssetsObject,"_switchButton"); } }
        internal Text AssetsTitle { get { return (Text)GetField(AssetsObject,"_chartTitle"); } }
        internal Text AssetsEmptyMessage { get { return (Text)GetField(AssetsObject,"_emptyChart"); } }
        internal Component AssetPlot { get { return (Component)GetProperty(GetField(AssetsObject,"_chart"),"Chart"); } }
        internal void RefreshInventoryData() { Invoke(_inventory,"Refresh"); }
        internal Action OmitProfitHistory(bool omitTrades, bool uninvested = false)
        {
            IList history=(IList)GetField(_state,"AssetHistory"), trades=(IList)GetField(_state,"TradeHistory");
            var savedHistory = new ArrayList(history);
            var savedTrades = new ArrayList(trades);
            IList holdings = (IList)GetField(_state,"Holdings");
            var savedHoldings = new ArrayList(holdings);
            history.Clear();
            if (omitTrades) trades.Clear();
            if (uninvested) holdings.Clear();
            RefreshInventoryData();
            return delegate
            {
                foreach(object point in savedHistory) history.Add(point);
                if (omitTrades) foreach(object trade in savedTrades) trades.Add(trade);
                if (uninvested) foreach(object lot in savedHoldings) holdings.Add(lot);
                RefreshInventoryData();
            };
        }

        private object CreateCapacityService(int life = 18, int light = 20, int strength = 20)
        {
            Type statsType = RequireRuntimeType("XingShangMod.Trading.CapacityStats");
            Type delegateType = typeof(Func<>).MakeGenericType(statsType);
            NewExpression body = Expression.New(
                statsType.GetConstructor(new[] { typeof(int), typeof(int), typeof(int) }),
                Expression.Constant(life),
                Expression.Constant(light),
                Expression.Constant(strength));
            Delegate reader = Expression.Lambda(delegateType, body).Compile();
            return Construct(RequireRuntimeType("XingShangMod.Trading.CapacityService"), reader);
        }

        private object BuildDefinitions()
        {
            Type itemType = RequireRuntimeType("XingShangMod.Domain.ItemDefinition");
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), itemType);
            object result = Activator.CreateInstance(dictionaryType);
            IEnumerable items = GetProperty(_items, "Items") as IEnumerable;
            foreach (object item in items)
                ((IDictionary)result).Add((string)GetField(item, "ItemId"), item);
            return result;
        }

        private object CreateWeeklyPoint(string itemId, RuntimePreviewHtmlPoint source, int week, double weeklyReturn)
        {
            object point = Construct(RequireRuntimeType("XingShangMod.Domain.WeeklyPoint"));
            int year;
            int month;
            int round;
            ParsePointTime(source.t, week, out year, out month, out round);
            SetField(point, "ItemId", itemId);
            SetField(point, "MarketWeekIndex", week);
            SetField(point, "GameYear", year);
            SetField(point, "GameMonth", month);
            SetField(point, "GameRound", round);
            SetField(point, "SourceRecordIndex", week);
            SetField(point, "Price", source.close);
            SetField(point, "BaseReturn", weeklyReturn);
            SetField(point, "Return", weeklyReturn);
            SetField(point, "Observed", true);
            return point;
        }

        private object CreatePoisonPoint(string itemId, int week, int currentWeek)
        {
            RuntimePreviewHtmlPoint source = new RuntimePreviewHtmlPoint
            {
                t = _html.items[0].history[currentWeek].t,
                close = 1000000000.0 + week * 1000000.0
            };
            object point = CreateWeeklyPoint(itemId, source, week, 999.0);
            SetField(point, "Observed", false);
            return point;
        }

        private object CreateTradeRecord(
            string id,
            string itemId,
            string action,
            string batchId,
            int quantity,
            double unitPrice,
            long cashDelta,
            int week)
        {
            object record = Construct(RequireRuntimeType("XingShangMod.Domain.TradeRecord"));
            SetField(record, "TradeId", id);
            SetField(record, "ItemId", itemId);
            SetField(record, "Action", action);
            SetField(record, "BatchId", batchId);
            SetField(record, "Quantity", quantity);
            SetField(record, "UnitPrice", unitPrice);
            SetField(record, "CashDelta", cashDelta);
            SetField(record, "MarketWeekIndex", week);
            SetField(record, "GameYear", 2);
            SetField(record, "GameMonth", 7);
            SetField(record, "GameRound", 2);
            return record;
        }

        private static void RequireText(
            List<string> failures,
            VisualSemanticSnapshot snapshot,
            string pathFragment,
            string leaf,
            string expected)
        {
            for (int i = 0; i < snapshot.texts.Count; i++)
            {
                VisualTextSemantic text = snapshot.texts[i];
                if (text.path.IndexOf(pathFragment, StringComparison.Ordinal) >= 0 &&
                    text.path.IndexOf(leaf, StringComparison.Ordinal) >= 0)
                {
                    RequireEqual(failures, text.path, expected, text.text);
                    return;
                }
            }
            failures.Add("Required fixture text is absent: " + pathFragment + " ... " + leaf + "; expected=" + expected);
        }

        private static void RequireEqual(List<string> failures, string path, string expected, string actual)
        {
            string visible = VisualValidation.StripRichText(actual ?? string.Empty, true);
            if (!string.Equals(expected, visible, StringComparison.Ordinal))
                failures.Add("Fixture semantic mismatch at " + path + ": expected='" + expected + "', actual='" + visible + "'.");
        }

        private RuntimePreviewHtmlItem FindItem(string itemId)
        {
            for (int i = 0; i < _html.items.Length; i++)
            {
                if (NormalizeItemId(_html.items[i].itemId, _html.items[i].key) == itemId)
                    return _html.items[i];
            }
            throw new InvalidDataException("Shared fixture is missing item " + itemId + ".");
        }

        private void ValidateHtmlFixture(RuntimePreviewHtmlDocument document)
        {
            if (document == null || document.items == null || document.items.Length != 54 ||
                document.index == null || document.index.history == null)
                throw new InvalidDataException("Expected the V4 shared fixture with 54 items and an index history.");
            for (int i = 0; i < document.items.Length; i++)
            {
                RuntimePreviewHtmlItem item = document.items[i];
                if (item == null || item.history == null || item.history.Length != 78 || document.index.history.Length != 78)
                    throw new InvalidDataException("Every Runtime Preview series must contain exactly 78 visible weeks.");
                for (int j = 0; j < item.history.Length; j++)
                {
                    if (item.history[j] == null || double.IsNaN(item.history[j].close) ||
                        double.IsInfinity(item.history[j].close) || item.history[j].close <= 0.0)
                        throw new InvalidDataException("Fixture contains an invalid close at item " + i + ", week " + j + ".");
                }
            }
        }

        private static void ParsePointTime(string text, int week, out int year, out int month, out int round)
        {
            year = 1 + week / 48;
            month = 1 + (week % 48) / 4;
            round = 1 + week % 4;
            if (string.IsNullOrEmpty(text))
                return;
            List<int> values = new List<int>(3);
            int current = 0;
            bool hasDigit = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    current = current * 10 + (text[i] - '0');
                    hasDigit = true;
                }
                else if (hasDigit)
                {
                    values.Add(current);
                    current = 0;
                    hasDigit = false;
                }
            }
            if (hasDigit)
                values.Add(current);
            if (values.Count >= 3)
            {
                year = values[0];
                month = values[1];
                round = values[2];
            }
        }

        private static string NormalizeItemId(string itemId, string key)
        {
            string value = itemId;
            if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(key))
            {
                int colon = key.LastIndexOf(':');
                value = colon >= 0 ? key.Substring(colon + 1) : key;
            }
            int parsed;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || parsed < 1 || parsed > 54)
                throw new InvalidDataException("Invalid fixture item id: " + value + ".");
            return parsed.ToString("00", CultureInfo.InvariantCulture);
        }

        private string SerializeState(bool pretty)
        {
            return Convert.ToString(
                InvokeStatic(
                    RequireRuntimeType("XingShangMod.Data.JsonCodec"),
                    "Serialize",
                    _state,
                    _state.GetType(),
                    pretty),
                CultureInfo.InvariantCulture);
        }

        private string StateHash()
        {
            return VisualValidation.Sha256Text(SerializeState(false));
        }

        private long WalletBalance()
        {
            return Convert.ToInt64(GetProperty(_wallet, "Balance"), CultureInfo.InvariantCulture);
        }

        private void DisposeUserInterface()
        {
            if (_ui != null)
            {
                Invoke(_ui, "Dispose");
                _ui = null;
            }
            _market = null;
            _detail = null;
            _inventory = null;
            _overlays = null;
            _confirmation = null;
        }

        private void ConfigurePluginPath()
        {
            Type paths = ResolveType("BepInEx.Paths, BepInEx.Core");
            SetStaticProperty(paths, "PluginPath", _options.BepInExPluginPath);
        }

        private void InstallAssemblyResolution()
        {
            _assemblyResolver = delegate (object sender, ResolveEventArgs args)
            {
                string simpleName = new AssemblyName(args.Name).Name;
                if (simpleName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                    simpleName.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                    simpleName.StartsWith("Unity.", StringComparison.Ordinal))
                    return null;
                string[] directories = { _options.ManagedPath, _options.BepInExCorePath, Path.GetDirectoryName(_options.PluginAssemblyPath) };
                for (int i = 0; i < directories.Length; i++)
                {
                    if (string.IsNullOrEmpty(directories[i]))
                        continue;
                    string candidate = Path.Combine(directories[i], simpleName + ".dll");
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }
                return null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += _assemblyResolver;
        }

        private Type RequireRuntimeType(string fullName)
        {
            Type type = _assembly.GetType(fullName, false);
            if (type == null)
                throw new TypeLoadException("Runtime Preview required type is absent from " + _assembly.Location + ": " + fullName);
            return type;
        }

        private static Type ResolveType(string assemblyQualifiedName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            if (type != null)
                return type;
            string fullName = assemblyQualifiedName.Split(',')[0];
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }
            throw new TypeLoadException("Cannot resolve preview integration type: " + assemblyQualifiedName);
        }

        private static object Construct(Type type, params object[] arguments)
        {
            try
            {
                return Activator.CreateInstance(
                    type,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    arguments,
                    CultureInfo.InvariantCulture);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw();
                throw;
            }
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot invoke " + methodName + " on null.");
            MethodInfo method = FindMethod(target.GetType(), methodName, arguments, false);
            return InvokeMethod(method, target, arguments);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(type, methodName, arguments, true);
            return InvokeMethod(method, null, arguments);
        }

        private static object InvokeMethod(MethodInfo method, object target, object[] arguments)
        {
            try
            {
                ParameterInfo[] parameters = method.GetParameters();
                int supplied = arguments == null ? 0 : arguments.Length;
                object[] completed = arguments;
                if (parameters.Length != supplied)
                {
                    completed = new object[parameters.Length];
                    for (int i = 0; i < supplied; i++)
                        completed[i] = arguments[i];
                    for (int i = supplied; i < parameters.Length; i++)
                        completed[i] = parameters[i].DefaultValue;
                }
                return method.Invoke(target, completed);
            }
            catch (TargetInvocationException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException ?? exception).Throw();
                throw;
            }
        }

        private static MethodInfo FindMethod(Type type, string name, object[] arguments, bool isStatic)
        {
            int count = arguments == null ? 0 : arguments.Length;
            Type current = type;
            while (current != null)
            {
                BindingFlags flags = (isStatic ? AllStatic : AllInstance) | BindingFlags.DeclaredOnly;
                MethodInfo[] methods = current.GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo candidate = methods[i];
                    if (candidate.Name != name)
                        continue;
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (parameters.Length < count)
                        continue;
                    bool compatible = true;
                    for (int j = count; j < parameters.Length; j++)
                    {
                        if (parameters[j].IsOptional)
                            continue;
                        compatible = false;
                        break;
                    }
                    if (!compatible)
                        continue;
                    for (int j = 0; j < count; j++)
                    {
                        if (arguments[j] == null || parameters[j].ParameterType.IsInstanceOfType(arguments[j]))
                            continue;
                        compatible = false;
                        break;
                    }
                    if (compatible)
                        return candidate;
                }
                current = current.BaseType;
            }
            throw new MissingMethodException(type.FullName, name + "/" + count);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = FindField(target.GetType(), name);
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = FindField(target.GetType(), name);
            field.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, AllInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
                current = current.BaseType;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        private static object GetProperty(object target, string name)
        {
            return FindProperty(target.GetType(), name).GetValue(target, null);
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = FindProperty(target.GetType(), name);
            property.SetValue(target, value, null);
        }

        private static void SetStaticProperty(Type type, string name, object value)
        {
            PropertyInfo property = type.GetProperty(name, AllStatic);
            if (property == null)
                throw new MissingMemberException(type.FullName, name);
            property.SetValue(null, value, null);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                PropertyInfo property = current.GetProperty(name, AllInstance | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property;
                current = current.BaseType;
            }
            throw new MissingMemberException(type.FullName, name);
        }

        private static void AddToList(object target, string fieldName, object value)
        {
            IList list = GetField(target, fieldName) as IList;
            if (list == null)
                throw new InvalidOperationException(target.GetType().FullName + "." + fieldName + " is not a mutable list.");
            list.Add(value);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
