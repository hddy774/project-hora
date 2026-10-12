namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    double Next()
    {
        uint x = State.RandomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State.RandomState = x;
        return x / 4294967296.0;
    }
    public void AdvanceHour()
    {
        long target = State.CompletedHours + 1;
        while (State.CompletedHours < target) AdvanceMinute();
    }
    void BeginHour()
    {
        ExpireOrders();
        foreach (var s in State.Stocks)
        {
            s.PreviousPrice = s.Price; s.Volume = 0;
            if (State.Hour == 0) { s.DayOpenPrice = s.Price; s.DayVolume = s.DayTurnover = 0; }
        }
        PrepareMarketSignals();
        int count = Math.Min(State.Retail.Count, 300 + (int)(Next() * 501)); State.ActiveRetailLastHour = count;
        var actors = actorBuffer.AsSpan(0, AiCount + count);
        for (int i = 0; i < AiCount; i++) actors[i] = i + 1;
        for (int i = 0; i < count; i++)
        { State.RetailCursor = (State.RetailCursor + 7919) % State.Retail.Count; actors[AiCount + i] = AiCount + State.RetailCursor + 1; }
        for (int i = actors.Length - 1; i > 0; i--)
        { int j = (int)(Next() * (i + 1)); (actors[i], actors[j]) = (actors[j], actors[i]); }
        State.MinuteActors.Clear(); foreach (int id in actors) State.MinuteActors.Add(id);
    }
    public void AdvanceMinute()
    {
        if (State.Minute == 0) BeginHour(); else ExpireOrders();
        PrepareMarketSignals();
        if (State.CompletedMinutes >= State.Environment.NextEventMinute) PublishNews();
        int minute = State.Minute, count = State.MinuteActors.Count;
        int first = count * minute / 60, end = count * (minute + 1) / 60;
        for (int i = first; i < end; i++)
        { var t = Owner(State.MinuteActors[i]); if (t.IsRetail) DecideRetail(t); else DecideInstitution(t); }
        for (int id = minute % 5 + 1; id <= AiCount; id += 5)
        {
            var t = Owner(id); var d = t.Development!;
            if (d.NextExecutionMinute <= State.CompletedMinutes) DecideInstitution(t);
        }
        State.CompletedMinutes++;
        if (State.Minute == 0) EndHour();
    }
    void EndHour()
    {
        State.Orders.RemoveAll(o => o.Remaining == 0);
        State.CompletedHours++;
        State.MinuteActors.Clear();
        if (State.Hour == 0)
            foreach (var s in State.Stocks) { s.DayOpenPrice = s.Price; s.DayVolume = s.DayTurnover = 0; }
        foreach (var s in State.Stocks)
        {
            s.Sentiment *= .94; Append(s.History, (double)s.MarkPrice, HistoryLimit);
        }
        foreach (var t in State.Bots) Append(t.EquityHistory, t.Equity(State.Stocks), HistoryLimit);
        foreach (var stock in State.Stocks.Where(s => s.Active)) { stock.ShareHourSum = checked(stock.ShareHourSum + stock.OutstandingShares); stock.ShareHours++; }
        if (State.Hour == 0) ServiceFinance();
        bool monthEnd = State.CompletedHours % (SeasonLength * 24) == 0;
        if (monthEnd)
        {
            CancelAllOrders(); closingBooks = true;
            try { CloseBusinesses(); } finally { closingBooks = false; }
        }
        ProcessBankruptcies();
        UpdatePerformance();
        if (State.Hour == 0) Append(State.DailyHistory, CaptureSnapshot(), 121);
        if (monthEnd) { FinishSeason(State.Season - 1); }
        RecordHour();
    }
}
