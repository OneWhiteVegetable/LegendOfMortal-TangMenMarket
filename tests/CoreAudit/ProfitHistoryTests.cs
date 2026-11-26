using System.Collections.Generic;
using System.Linq;
using XingShangMod.Data;
using XingShangMod.Domain;
using XingShangMod.Persistence;
using XingShangMod.Trading;
using static AuditAssert;

internal sealed class ProfitHistoryTests
{
    private static AssetService Service(RunState state, FakeWallet wallet)
    {
        return new AssetService(state, wallet, new Dictionary<string,ItemDefinition>());
    }

    internal void Register(TestRunner runner)
    {
        runner.Run("asset-profit-signed-cash-no-external-income", Captures);
        runner.Run("asset-profit-old-week-replay-duplicates-gaps-future", OldWeeks);
        runner.Run("asset-profit-incomplete-old-ledger-not-zero", Incomplete);
        runner.Run("asset-profit-legacy-and-native-expiry-same-return", Expiry);
    }

    private static RunState State(int week)
    {
        var state = new RunState();
        state.Clock.MarketWeekIndex = week;
        return state;
    }
    private static WeeklyPoint Price(int week,double value,string id="A")
    {
        return new WeeklyPoint { ItemId=id, MarketWeekIndex=week, Price=value,Observed=true };
    }
    private static TradeRecord Trade(int week,string action,int qty,long delta,string id="A",long debt=0)
    {
        return new TradeRecord { MarketWeekIndex=week, ItemId=id, Action=action, Quantity=qty,
            CashDelta=delta, DebtDelta=debt, BatchId=id=="B"?"batch":"" };
    }
    private static AssetPoint Old(int week,long cash,double longValue,double shortValue,long debt=0)
    {
        return new AssetPoint {MarketWeekIndex=week,Cash=cash,LongValue=longValue,ShortLiability=shortValue,
            Debt=debt,NetAssets=cash+longValue-shortValue-debt};
    }
    private static RunState Trades(bool oldDebt)
    {
        RunState s=State(4);
        s.PriceHistory.AddRange(new[]{Price(0,100),Price(1,90),Price(2,120),Price(3,110),Price(3,100,"B"),Price(4,130,"B")});
        s.TradeHistory.AddRange(new[]{Trade(0,"Buy",10,-1000),Trade(2,"Sell",4,480),Trade(3,"Sell",6,660),
            Trade(3,"Borrow",2,194,"B"),Trade(4,"Expire",2,oldDebt?-5:-260,"B",oldDebt?255:0)});
        return s;
    }
    private static void Captures()
    {
        var s=State(1);
        s.TradeHistory.Add(Trade(0,"Buy",10,-1000));s.Holdings.Add(new Holding{ItemId="A",Quantity=10,TotalCost=1000});
        s.PriceHistory.Add(Price(1,90));
        var wallet=new FakeWallet(-120);
        var service=Service(s,wallet);
        var saved=service.CaptureWeeklySnapshot();
        True(saved.HasProfitMetrics,"captured opening cost and floating loss");
        Equal(1,saved.ProfitRecordVersion,"new point provenance");
        Near(-100,saved.TotalProfit,"trading loss");Near(-0.1,saved.TotalProfit/saved.CapitalBasis,"signed return");
        wallet.Set(4880);
        var next=service.CaptureTradeSnapshot();
        Near(saved.TotalProfit,next.TotalProfit,"external income did not move profit");
        Near(saved.NetAssets+5000,next.NetAssets,"external income moved net assets");
        var restored=RunStateNormalizer.Normalize(JsonCodec.Deserialize<RunState>(JsonCodec.Serialize(s,false)));
        Equal(-120L,restored.AssetHistory[0].Cash,"signed cash survived sidecar serialization");
        Near(-100,restored.AssetHistory[0].TotalProfit,"signed profit survived sidecar serialization");
    }
    private static void OldWeeks()
    {
        var s=Trades(false);
        s.AssetHistory.AddRange(new[]{Old(0,1000,1000,0),Old(1,3000,1111,0),Old(1,7300,900,0),
            Old(2,-200,666,0),Old(3,8000,0,200),Old(4,-900,0,0),Old(5,98765,888,0)});
        s.TradeHistory.Add(Trade(5,"Buy",99,-999));
        s.PriceHistory.Add(Price(5,999999));
        var service=Service(s,new FakeWallet(-900));
        var result=service.ProfitHistory(s.AssetHistory,service.GetSnapshot());
        True(result.Select(p=>p.MarketWeekIndex).SequenceEqual(new[]{0,1,3,4}),"one point per reconciled visible week");
        Near(-100,result[1].TotalProfit,"historical losses are independent of historical wallet income");
        Near(1000,result[1].CapitalBasis,"original active capital at that week");
        Near(134,result[2].TotalProfit,"realized gains plus short floating loss");
        Near(1194,result[2].CapitalBasis,"short net proceeds enter basis once");
        Near(74,result[3].TotalProfit,"expiry loss recorded exactly once");
        True(!s.AssetHistory[2].HasProfitMetrics,"opening a readonly graph did not write into a legacy save");
    }
    private static void Incomplete()
    {
        var s=State(2);
        s.AssetHistory.Add(Old(1,9000,100,0));s.Holdings.Add(new Holding{ItemId="A",Quantity=1,TotalCost=100});
        var service=Service(s,new FakeWallet(1000));
        True(!service.CaptureWeeklySnapshot().HasProfitMetrics,"missing opening trade/price cannot produce 0%");
        Equal(0,service.ProfitHistory(s.AssetHistory,service.GetSnapshot()).Count,"old partial history remains absent");
        var empty=State(0);
        empty.AssetHistory.Add(Old(12,50,99999,0));
        var first=Service(empty,new FakeWallet(123)).CaptureWeeklySnapshot();
        True(first.HasProfitMetrics && first.TotalProfit==0 && first.CapitalBasis==0,"known zero profit, no rate denominator");
        var partial=State(4);
        partial.AssetHistory.Add(Old(0,200,900,0));
        partial.TradeHistory.Add(Trade(2,"Buy",1,-50));
        partial.TradeHistory.Add(Trade(3,"Sell",1,75));
        True(!Service(partial,new FakeWallet(37)).GetSnapshot().IsRealizedProfitComplete,
            "prior holdings before journal began reveal even a fully closed missing position");
    }
    private static void Expiry()
    {
        var native=Service(Trades(false),new FakeWallet(-255)).GetSnapshot();
        var oldState=Trades(true);oldState.Debt=255;
        var old=Service(oldState,new FakeWallet(0)).GetSnapshot();
        Near(native.TotalProfit,old.TotalProfit,"old debt and signed wallet have equal trading P&L");
        Near(native.ReturnRate,old.ReturnRate,"return denominator unchanged on wallet migration");
        Near(native.NetAssets,old.NetAssets,"legacy liability subtracted once");
    }
}
