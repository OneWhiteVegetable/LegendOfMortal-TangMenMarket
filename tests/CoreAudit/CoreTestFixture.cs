using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Market;
using XingShangMod.Persistence;
using XingShangMod.Trading;

internal sealed class CoreTestFixture : IDisposable
{
    public string Root { get; private set; }
    public WeeklyDataRepository Repository { get; private set; }
    public ItemDefinition Item { get; private set; }

    public CoreTestFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "xingshang-core-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        CreateMarketData();
    }

    public RunState CreateTradeState(double price)
    {
        RunState state = new RunState { RunId = "trade-run", RunSeed = 5 };
        new MarketClock(state.Clock).Initialize(new MarketTime(1, 4, 1));
        state.AppearedItemIds.Add(Item.ItemId);
        state.SecurityBindings.Add(new SecurityBinding
        {
            ItemId = Item.ItemId,
            SecurityId = "stock-flat",
            LastPrice = price,
            DelistedWeek = -1
        });
        SetPrice(state, 0, price);
        return state;
    }

    public TradingService CreateTrading(RunState state, IPlayerWallet wallet)
    {
        return CreateTrading(state, wallet, new CapacityStats(30, 10, 10), Item.Weight);
    }

    public TradingService CreateTrading(
        RunState state,
        IPlayerWallet wallet,
        CapacityStats stats,
        double weight)
    {
        var definition = new ItemDefinition
        {
            ItemId = Item.ItemId,
            ReferencePrice = Item.ReferencePrice,
            Weight = weight
        };
        var definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        definitions.Add(definition.ItemId, definition);
        return new TradingService(state, wallet, definitions, new CapacityService(delegate
        {
            return stats;
        }));
    }

    public void SetPrice(RunState state, int week, double price)
    {
        state.Clock.MarketWeekIndex = week;
        state.PriceHistory.Add(new WeeklyPoint
        {
            ItemId = Item.ItemId,
            MarketWeekIndex = week,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 1,
            Price = price,
            Observed = true
        });
    }

    public RunState CreateMarketState(string indexId, string stockId, bool canDelist)
    {
        RunState state = new RunState
        {
            RunId = "market-run",
            RunSeed = 17,
            CurrentIndex = 1000,
            CurrentGameYear = 1
        };
        new MarketClock(state.Clock).Initialize(new MarketTime(1, 4, 1));
        state.AnnualIndexBindings.Add(new AnnualIndexBinding
        {
            Year = 1,
            SecurityId = indexId,
            Code = indexId,
            DataPath = indexId + ".wkg",
            StartRecordIndex = 0,
            NextRecordIndex = 1,
            StartIndexValue = 1000
        });
        state.AppearedItemIds.Add(Item.ItemId);
        state.SecurityBindings.Add(new SecurityBinding
        {
            ItemId = Item.ItemId,
            SecurityId = stockId,
            Code = stockId,
            DataPath = stockId + ".wkg",
            StartRecordIndex = 0,
            NextRecordIndex = 1,
            AppearedWeek = 0,
            LastPrice = 100,
            CanDelist = canDelist,
            DelistedWeek = -1
        });
        state.PriceHistory.Add(new WeeklyPoint
        {
            ItemId = Item.ItemId,
            MarketWeekIndex = 0,
            GameYear = 1,
            GameMonth = 4,
            GameRound = 1,
            SourceRecordIndex = 0,
            Price = 100,
            Observed = true
        });
        return state;
    }

    public void Advance(MarketEngine engine, RunState state, MarketTime time)
    {
        int next = state.Clock.MarketWeekIndex + 1;
        state.Clock.MarketWeekIndex = next;
        engine.AdvanceWeeks(new MarketClockAdvancement(new[] { time }, next, next, false));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
        catch
        {
        }
    }

    private void CreateMarketData()
    {
        string runtime = Path.Combine(Root, "Runtime");
        string data = Path.Combine(runtime, "data");
        Directory.CreateDirectory(data);
        Item = new ItemDefinition { ItemId = "01_item", ReferencePrice = 100, Weight = 1 };
        WriteSecurity(data, "index-up-a", 200, 10, false, false);
        WriteSecurity(data, "index-up-b", 200, 10, false, false);
        WriteSecurity(data, "index-flat", 200, 0, false, false);
        WriteSecurity(data, "index-short", 3, 10, false, false);
        WriteSecurity(data, "stock-flat", 200, 0, false, false);
        WriteSecurity(data, "stock-short", 3, 0, false, false);
        WriteSecurity(data, "stock-delist", 3, 0, true, false);
        WriteSecurity(data, "stock-no-trade", 4, 10, false, true);
        var catalog = new SelectionCatalogDto
        {
            schema_version = 1,
            securities = new[]
            {
                Entry("stock-flat"), Entry("stock-short"), Entry("stock-delist"), Entry("stock-no-trade")
            },
            indices = new[]
            {
                Entry("index-up-a"), Entry("index-up-b"), Entry("index-flat"), Entry("index-short")
            }
        };
        string catalogJson = "{\"schema_version\":1,\"securities\":" +
            JsonCodec.Serialize(catalog.securities, false) + ",\"indices\":" +
            JsonCodec.Serialize(catalog.indices, false) + "}";
        File.WriteAllText(Path.Combine(runtime, "selection_catalog.json"), catalogJson, new UTF8Encoding(false));
        Repository = new WeeklyDataRepository(data);
    }

    private static SelectionEntryDto Entry(string id)
    {
        bool delist = id == "stock-delist";
        bool noTrade = id == "stock-no-trade";
        int rows = id.EndsWith("short", StringComparison.Ordinal) ? 3 : noTrade ? 4 : 200;
        return new SelectionEntryDto
        {
            security_id = id,
            name = id,
            path = id + ".wkg",
            status = "success",
            row_count = rows,
            first_week = 4,
            last_week = 4 + (rows - 1) * 7,
            observed_rows = noTrade ? rows - 1 : rows,
            no_trade_rows = noTrade ? 1 : 0,
            source_missing_rows = 0
        };
    }

    private static void WriteSecurity(
        string directory,
        string id,
        int count,
        int percent,
        bool delist,
        bool noTrade)
    {
        var header = new HeaderDto
        {
            schemaVersion = 1,
            recordSize = 28,
            securityId = id,
            code = id,
            currency = "CNY",
            rawScale = 100,
            calendarId = "audit",
            rowCount = count,
            firstWeek = 4,
            lastWeek = 4 + (count - 1) * 7,
            source = "audit",
            adjustmentAlgorithm = "audit"
        };
        byte[] headerBytes = new UTF8Encoding(false).GetBytes(JsonCodec.Serialize(header, false));
        string path = Path.Combine(directory, id + ".wkg");
        using (FileStream file = File.Create(path))
        using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
        using (BinaryWriter writer = new BinaryWriter(gzip, new UTF8Encoding(false, true)))
        {
            writer.Write(new byte[] { (byte)'S', (byte)'W', (byte)'K', (byte)'1' });
            writer.Write((uint)headerBytes.Length);
            writer.Write(headerBytes);
            for (int i = 0; i < count; i++)
            {
                bool empty = noTrade && i == 2;
                writer.Write(4 + i * 7);
                writer.Write(empty ? -1 : 4 + i * 7);
                writer.Write(empty ? 0L : (long)(10000 + i * percent * 100));
                writer.Write(empty ? 0.0 : 1.0);
                writer.Write((ushort)(delist && i == count - 1 ? 2 : 0));
                writer.Write((byte)(empty ? 1 : 0));
                writer.Write((byte)0);
            }
        }
    }

    private sealed class HeaderDto
    {
        public int schemaVersion;
        public int recordSize;
        public string securityId;
        public string code;
        public string currency;
        public int rawScale;
        public string calendarId;
        public int rowCount;
        public int firstWeek;
        public int lastWeek;
        public string source;
        public string adjustmentAlgorithm;
    }
}
