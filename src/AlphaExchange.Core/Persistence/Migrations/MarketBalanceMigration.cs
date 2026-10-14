namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void UpgradeMarketBalance()
    {
        // v9 has the same securities and ledger. Preserve every balance/history
        // and start the new daily band at the observed price, without a fake trade.
        CancelAllOrders();
        State.Version = 10;
        State.Environment.Rules.Version = "market-1.11";
        if (State.Environment.Rules.InvestorCapital == 1_000_000_000)
            State.Environment.Rules.InvestorCapital = InitialCash;
        for (int i = 0; i < StockCount; i++)
        {
            var stock = State.Stocks[i];
            stock.LimitReferencePrice = stock.MarkPrice;
            stock.NextCompanyNewsMinute = State.CompletedMinutes + 20 + i * 25;
        }
        foreach (var trader in State.Bots)
        {
            var d = trader.Development!;
            d.OutcomesSinceMinute = State.CompletedMinutes;
            d.NextExecutionMinute = State.CompletedMinutes;
            d.TargetCashRatio = DesiredCashRatio(trader);
            d.Plans.Clear();
        }
    }
}
