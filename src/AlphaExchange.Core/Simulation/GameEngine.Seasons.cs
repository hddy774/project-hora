namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void FinishSeason(long season)
    {
        var result = new SeasonResult
        {
            Season = season,
            Matches = State.TotalMatches - State.SeasonStartMatches,
            RetailEquity = State.Retail.Sum(t => t.Equity(State.Stocks)),
            Start = State.SeasonSnapshot,
            End = CaptureSnapshot(),
            Days = State.DailyHistory.Where(d => d.Hour > (season - 1) * 720 && d.Hour <= season * 720).ToList(),
            Policy = State.Government.Policy,
            CompanyReports = State.Stocks.Select(s => s.Active ? CopyReport(s.Report) : CopyReport(s.Reports.LastOrDefault() ?? s.Report)).ToList()
        };
        foreach (var (t, index) in Ranking().Select((t, i) => (t, i)))
        {
            var row = new SeasonStanding(t.Id, index + 1, t.SeasonOpeningEquity, t.Equity(State.Stocks), t.Return(State.Stocks), t.Generation, t.Name);
            result.Standings.Add(row);
            if (AwardSeasonGrowth(t, season, row.Rank) is { } reward) result.GrowthRewards.Add(reward);
            Append(t.SeasonRanks, new RankHistory(season, row.Rank, row.Return, row.Equity, t.Generation), 12);
        }
        State.PendingSeasons.Add(result);
        foreach (var t in Participants)
        { t.SeasonOpeningEquity = t.Equity(State.Stocks); if (t.IsRetail) t.SeasonReturnBasis = t.ReturnIndex; else t.SeasonSnapshot = CaptureTrader(t); }
        State.SeasonSnapshot = result.End;
        State.SeasonStartMatches = State.TotalMatches;
    }
    static void Append<T>(List<T> values, T value, int maximum)
    { values.Add(value); if (values.Count > maximum) values.RemoveRange(0, values.Count - maximum); }
}
