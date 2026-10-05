namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public static readonly double[] CreditLimits = [1, .9, .8, .7, .6, .5, .4, .35, .3];
    public static readonly double[] CreditSpreads = [.005, .01, .018, .028, .045, .065, .09, .125, .17];
    void InitializeRepresentative(Trader t)
    {
        t.Abilities = Abilities.Uniform(Rules.Representative.InitialAbility);
        InitializeDevelopment(t);
        t.Disposition = (Disposition)((t.Id - 1) % 5); t.Integrity = .35 + Next() * .65;
        t.CreditScore = 70;
    }
    void InitializeEconomy()
    {
        CapitalizeMarket();
        for (int i = 0; i < State.Stocks.Count; i++)
        {
            InitializeCompany(State.Stocks[i], i);
        }
        State.Government.History.Add(State.Government.Policy);
    }
    void InitializeCompany(Stock s, int index) => InitializeBusiness(s);
    public LoanOffer LoanTerms(Trader t)
    {
        int grade = (int)t.CreditRating;
        double ratio = Math.Clamp(CreditLimits[grade] * State.Government.Policy.LoanLimitMultiplier, .3, 1);
        // Borrow against owned capital. Borrowing again cannot enlarge its own limit.
        long limit = (long)(Math.Max(0, t.Equity(State.Stocks)) * ratio);
        return new LoanOffer(t.CreditRating, ratio, State.Government.Policy.BaseRate + CreditSpreads[grade], limit,
            Math.Min(Math.Max(0, limit - t.LoanDebt), Math.Max(0,State.Bank.Cash-State.Bank.MarketAccount.ReservedCash)));
    }
    public string? BorrowCash(int ownerId, long amount)
    {
        if ((ownerId < 1 || ownerId > AiCount + State.Retail.Count) || amount <= 0) return "대출 금액이 올바르지 않습니다.";
        var t = Owner(ownerId); var offer = LoanTerms(t);
        if (amount > offer.Available) return "신용 한도 또는 은행 유동성이 부족합니다.";
        TransferCash(State.Bank, t, amount, "loan"); t.LoanDebt += amount; t.BorrowedCash += amount; cachedStats = null;
        return null;
    }
    public long RepayLoan(int ownerId, long amount)
    {
        if ((ownerId < 1 || ownerId > AiCount + State.Retail.Count) || amount <= 0) return 0;
        var t = Owner(ownerId); long paid = Math.Min(amount, Math.Min(t.LoanDebt, AvailableCash(t)));
        TransferCash(t, State.Bank, paid, "repayment"); t.LoanDebt -= paid; t.RepaidCash += paid; cachedStats = null; return paid;
    }
    public long AvailableCash(Trader t) => Math.Max(0, t.Cash - t.ReservedCash - (t.IsRetail || t.Id==0 ? 0 : t.ShortCollateral(State.Stocks)));
    public void ApplyNewsImpact(int stockIndex, double impact)
    {
        if (stockIndex < 0 || stockIndex >= State.Stocks.Count || !double.IsFinite(impact)) throw new ArgumentOutOfRangeException(nameof(stockIndex));
        var s = State.Stocks[stockIndex]; s.SeasonNewsImpact = Math.Clamp(s.SeasonNewsImpact + impact, -1, 1); s.SeasonNewsCount++;
    }
    void RefreshEconomy()
    {
        if(State.World is not null) { VoteEconomicPolicy(); return; }
        // Every resting order is cancelled before a fee/regulation change.
        CancelAllOrders();
        var p = State.Government.Policy;
        int mode = (int)(Next() * 3);
        var next = new GovernmentPolicy { Season = State.Season, Name = mode == 0 ? "성장 지원" : mode == 1 ? "균형 성장" : "안정·감독 강화",
            TaxRate = .08 + Next() * .10, FeeBasisPoints = 10 + (int)(Next() * 16), BaseRate = .01 + Next() * .05,
            ShortSellingAllowed = mode != 2 || Next() > .35, ShortExposureLimit = mode == 2 ? .08 : .15,
            LoanLimitMultiplier = mode == 2 ? .8 : 1, SubsidyRate = mode == 0 ? .00008 : mode == 1 ? .00003 : 0,
            SpendingRatio = mode == 0 ? .85 : mode == 1 ? .65 : .50, DividendTaxRate = .06 + Next() * .06,
            Enforcement = mode == 2 ? .85 : .35 + Next() * .25 };
        State.Government.Policy = next; Append(State.Government.History, next, 12); cachedStats = null;
    }
    void ServiceFinance()
    {
        foreach (var t in Participants.Where(t => !t.IsRetail || t.LoanDebt > 0 || t.FineDebt > 0 || t.ShortDividendDebt > 0 || t.TaxDebt>0))
        {
            CancelOrders(t.Id);
            long interest = t.LoanDebt == 0 ? 0 : (long)Math.Ceiling(t.LoanDebt * LoanTerms(t).AnnualRate / 360);
            long paid = Math.Min(interest, AvailableCash(t));
            bool overdueInterest = paid < interest;
            TransferCash(t, State.Bank, paid, "interest"); t.InterestExpense += interest; t.InterestPaid += paid; t.LoanDebt += interest - paid;
            State.Bank.InterestIncome += interest;
            long borrowFee = t.ShortLiability(State.Stocks) == 0 ? 0 : (long)Math.Ceiling(t.ShortLiability(State.Stocks) * .01 / 360);
            paid = Math.Min(borrowFee, AvailableCash(t));
            TransferCash(t, State.Bank, paid, "borrow-fee"); t.BorrowFees += borrowFee; t.LoanDebt += borrowFee - paid;
            // BorrowFees is cash expense; unpaid part is financed and tracked as borrowing.
            t.BorrowedCash += borrowFee - paid; State.Bank.BorrowFeeIncome += borrowFee;
            paid = Math.Min(t.FineDebt, AvailableCash(t)); TransferCash(t, State.Government, paid, "fine-payment"); t.FineDebt -= paid; t.FinesPaid += paid;
            paid = Math.Min(t.ShortDividendDebt, AvailableCash(t)); TransferCash(t, State.Bank, paid, "short-dividend-arrears");
            t.ShortDividendDebt -= paid; t.ShortDividendPaid += paid; State.Bank.DividendIncome += paid;
            paid=Math.Min(t.TaxDebt,AvailableCash(t)); TransferCash(t,State.Government,paid,"tax-arrears"); t.TaxDebt-=paid; t.TaxesPaid+=paid; State.Government.TaxEscrow+=paid;
            long grant = Math.Min(Math.Max(0, State.Government.Cash - State.Government.TaxEscrow), (long)((t.GrossAssets(State.Stocks) - t.Cash) * State.Government.Policy.SubsidyRate));
            if (grant > 0) { TransferCash(State.Government, t, grant, "subsidy"); t.Subsidies += grant; State.Government.Subsidies += grant; }
            double solvency = (double)t.Equity(State.Stocks) / Math.Max(1, t.OpeningEquity);
            int target = (int)Math.Clamp(70 + (solvency - 1) * 35 - (double)t.LoanDebt / Math.Max(1, t.GrossAssets(State.Stocks)) * 35 +
                (t.IsRetail ? 0 : Capabilities(t).Effective.RiskManagement * .12 + (t.Development?.CreditBonus ?? 0)) - t.MarginCalls * 2, 0, 99);
            if (overdueInterest) target -= 4;
            t.CreditScore = Math.Clamp((int)Math.Round(t.CreditScore * .9 + target * .1), 0, 99);
        }
        cachedStats = null;
    }
    public string? ProposeOperation(int leaderId, int partnerId, int stockIndex)
    {
        if (leaderId is < 1 or > AiCount || partnerId is < 1 or > AiCount || leaderId == partnerId || stockIndex < 0 || stockIndex >= State.Stocks.Count)
            return "작전 참여자가 올바르지 않습니다.";
        if (State.Operations.Any(o => o.Status == OperationStatus.Active && (o.LeaderId == leaderId || o.PartnerId == leaderId || o.LeaderId == partnerId || o.PartnerId == partnerId)))
            return "이미 공동 작전에 참여하고 있습니다.";
        State.Operations.Add(new InstitutionOperation { Id = State.NextOperationId++, LeaderId = leaderId, PartnerId = partnerId,
            StockIndex = stockIndex, SecurityId = State.Stocks[stockIndex].SecurityId, StartedHour = State.CompletedHours, EndsHour = State.CompletedHours + 12 + (int)(Next() * 13),
            OpeningPrice = State.Stocks[stockIndex].Price,LeaderName=Representatives.Name(Owner(leaderId).PortraitId),PartnerName=Representatives.Name(Owner(partnerId).PortraitId),
            Detail = $"{Representatives.Name(Owner(leaderId).PortraitId)} ↔ {Representatives.Name(Owner(partnerId).PortraitId)} · 공동 매집 합의" });
        if (State.Operations.Count > 80) State.Operations.RemoveAll(o => o.Status != OperationStatus.Active && o.EndsHour < State.CompletedHours - 720);
        while (State.Operations.Count > 80)
        { int i = State.Operations.FindIndex(o => o.Status != OperationStatus.Active); if (i < 0) break; State.Operations.RemoveAt(i); }
        return null;
    }
    public void AssessFine(Trader t, long amount)
    {
        if (amount <= 0) return;
        CancelOrders(t.Id); long paid = Math.Min(amount, AvailableCash(t));
        TransferCash(t, State.Government, paid, "fine"); t.Fines += amount; t.FinesPaid += paid; t.FineDebt += amount - paid;
        State.Government.Fines += amount; t.CreditScore = Math.Max(0, t.CreditScore - 12); cachedStats = null;
    }
    void UpdateOperations()
    {
        foreach (var o in State.Operations.Where(o => o.Status == OperationStatus.Active && o.EndsHour <= State.CompletedHours))
        {
            bool detected = Next() < State.Government.Policy.Enforcement;
            double gain = (double)State.Stocks[o.StockIndex].Price / o.OpeningPrice - 1;
            o.Status = detected || gain < o.TargetReturn ? OperationStatus.Failed : OperationStatus.Completed;
            if (o.Status == OperationStatus.Failed)
            {
                foreach (int id in new[] { o.LeaderId, o.PartnerId })
                { var t = Owner(id); long fine = Math.Max(1000, (long)(Math.Max(0, t.Equity(State.Stocks)) * .015)); AssessFine(t, fine); o.Fine += fine; }
                o.Detail = detected ? "감독기관 적발 · 참여 대표에게 벌금" : "목표 수익 미달 · 참여 대표에게 벌금";
            }
            else o.Detail = "공동 매집 종료 · 감독 위험은 다음 작전에도 적용";
        }
        if (State.CompletedHours % 12 != 0) return;
        var candidates = State.Bots.Where(t => t.Integrity < (t.Disposition == Disposition.Sociable ? .78 : .65) &&
            Capabilities(t).Effective.Negotiation > (t.Disposition == Disposition.Sociable ? 40 : 55) && t.Equity(State.Stocks) > 100_000 && !State.Operations.Any(o => (o.LeaderId == t.Id || o.PartnerId == t.Id) && o.EndsHour > State.CompletedHours - 240)).ToArray();
        if (candidates.Length > 1 && Next() < .3)
        { var a = candidates[(int)(Next() * candidates.Length)]; var b = candidates[(int)(Next() * candidates.Length)]; ProposeOperation(a.Id, b.Id, (int)(Next() * State.Stocks.Count)); }
    }
}
