namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public MarketRules MarketRules => State.Environment.Rules;
    public SimulationRules Rules => State.Rules ??= SimulationRules.Default();
    void InitializeDevelopment(Trader t)
    {
        if (t.IsRetail || t.Id == 0) return;
        int count = State.Stocks.Count;
        t.Development = new InstitutionDevelopment
        {
            TargetCashRatio = Rules.Investment.BaseCash,
            PositionOpenedHours = Enumerable.Repeat(-1L, count).ToArray(),
            ShortOpenedHours = Enumerable.Repeat(-1L, count).ToArray(),
            NextReviewHours = new long[count],
            PositionHorizons = new InvestmentHorizon[count],
            LastNewsSignals = new double[count]
        };
    }
    public InstitutionSkills Capabilities(Trader t)
    {
        if (t.IsRetail || t.Id is < 1 or > AiCount) throw new ArgumentException("기관 성장 정보가 없습니다.", nameof(t));
        if (t.Development is null) InitializeDevelopment(t);
        return new(t.Abilities, t.Abilities);
    }
    public InvestmentHorizon HoldingHorizon(long hours)
    {
        if (hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        for (int i = 0; i < Rules.Horizons.Length; i++)
            if (Rules.Horizons[i].MaximumHours is not { } maximum || hours < maximum) return (InvestmentHorizon)i;
        return InvestmentHorizon.Long;
    }
    public PositionTiming PositionTiming(Trader t, int index)
    {
        if (t.Development is not { } d || index < 0 || index >= State.Stocks.Count) throw new ArgumentException("기관 보유 기간이 없습니다.");
        long opened = t.Shares[index] > 0 ? d.PositionOpenedHours[index] : t.ShortShares[index] > 0 ? d.ShortOpenedHours[index] : -1;
        long? age = opened >= 0 ? State.CompletedHours - opened : null;
        return new(age, d.PositionHorizons[index], age.HasValue ? HoldingHorizon(age.Value) : null, d.NextReviewHours[index]);
    }
    public double CashRatio(Trader t) => (double)t.Cash / Math.Max(1, t.GrossAssets(State.Stocks));
    internal SeasonGrowthReward? AwardSeasonGrowth(Trader t, long season, int rank)
    {
        if (season < 1 || season >= State.Season || rank is < 1 or > AiCount) throw new ArgumentOutOfRangeException(nameof(season));
        var d = t.Development!;
        if (season <= d.LastRewardSeason) return null;
        d.LastRewardSeason = season; d.LastRewardRank = rank;
        var reward = !t.WaitingForCapital && t.BirthHour <= (season - 1) * SeasonLength * 24 ? Rules.Rewards.FirstOrDefault(r => rank <= r.MaximumRank) : null;
        int points = reward?.AbilityPoints ?? 0, credit = reward?.CreditBonus ?? 0;
        d.LastRewardPoints = points; d.PointsEarned = checked(d.PointsEarned + points); d.AvailablePoints = checked(d.AvailablePoints + points);
        d.CreditBonus = Math.Min(Rules.Representative.MaximumCreditBonus, (int)Math.Round(d.CreditBonus * Rules.Representative.CreditRetention) + credit);
        t.CreditScore = Math.Min(99, t.CreditScore + credit);
        var allocation = new int[10];
        while (d.AvailablePoints > 0)
        {
            int best = -1; double value = double.MinValue;
            for (int i = 0; i < 10; i++)
            {
                int current = t.Abilities.Get((AbilityKind)i); if (current >= Rules.Representative.AbilityMaximum) continue;
                double priority = Rules.Representative.PointPriorities[i] * (Rules.Representative.AbilityMaximum - current);
                if (d.TargetCashRatio >= Rules.Investment.MaximumCash - Rules.Investment.CashTolerance && i == (int)AbilityKind.RiskManagement) priority *= 1.5;
                if (priority > value) { best = i; value = priority; }
            }
            if (best < 0) break;
            t.Abilities.Set((AbilityKind)best, t.Abilities.Get((AbilityKind)best) + 1);
            d.AllocatedPoints[best]++; allocation[best]++; d.PointsSpent++; d.AvailablePoints--;
        }
        d.NextDecisionHour = State.CompletedHours;
        return new(t.Id, t.Generation, rank, points, credit, allocation);
    }
    void RecordPositionOpen(Trader t, int index, bool shortPosition, bool wasEmpty)
    {
        if (!wasEmpty || t.Development is not { } d) return;
        (shortPosition ? d.ShortOpenedHours : d.PositionOpenedHours)[index] = State.CompletedHours;
        d.NextReviewHours[index] = State.CompletedHours + Rules.Horizons[(int)d.PositionHorizons[index]].ReviewHours;
    }
    void RefreshPositionTiming(Trader t, int index)
    {
        if (t.Development is not { } d) return;
        if (t.Shares[index] == 0) d.PositionOpenedHours[index] = -1;
        if (t.ShortShares[index] == 0) d.ShortOpenedHours[index] = -1;
        d.NextReviewHours[index] = State.CompletedHours;
        d.NextDecisionHour = State.CompletedHours;
    }
}
