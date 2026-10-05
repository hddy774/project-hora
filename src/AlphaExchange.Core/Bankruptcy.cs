namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public event Action<BankruptcyRecord>? BankruptcyRecorded;
    void RecordBankruptcy(BankruptcyRecord record)
    {
        var totals=State.BankruptcyTotals;
        if(record.Kind==BankruptcyKind.Company) { totals.Companies++; totals.CompanyCapital+=record.ReplacementCapital; }
        else if(record.Kind==BankruptcyKind.Institution)
        { totals.Institutions++; totals.InstitutionCapital+=record.ReplacementCapital; totals.ClosedInstitutionTradingIncome+=record.TradingIncome; }
        else { totals.Retail++; totals.RetailCapital+=record.ReplacementCapital; totals.ClosedRetailTradingIncome+=record.TradingIncome; }
        Append(State.Bankruptcies,record,64); BankruptcyRecorded?.Invoke(CheckpointCopy.Bankruptcy(record)); cachedStats=null;
    }
    internal void ProcessBankruptcies()
    {
        if(State.Hour!=0) return;
        for(int i=0;i<State.Stocks.Count;i++)
        {
            var s=State.Stocks[i];
            if(s.WaitingForCapital)
            {
                long capital=(long)s.Price*1000;
                if(Math.Max(0,capital-State.Bank.Cash)<=State.RealEconomy.Cash/2)
                    State.BankruptcyTotals.CompanyCapital+=FundListing(s,i,capital,State.Stocks.Sum(x=>x.MarketCap));
            }
            else if(s.Active && (s.Report.Equity<=0 || s.InsolventMonths>=3)) FailCompany(i);
        }
        // One daily portfolio check, rather than another 10,000 valuations each hour.
        for(int i=0;i<State.Bots.Count;i++) CheckTrader(State.Bots[i]);
        for(int i=0;i<State.Retail.Count;i++) CheckTrader(State.Retail[i]);
    }
    void CheckTrader(Trader t)
    {
        if(t.WaitingForCapital)
        {
            long funded=FundReplacement(t);
            if(t.IsRetail) State.BankruptcyTotals.RetailCapital+=funded; else State.BankruptcyTotals.InstitutionCapital+=funded;
            return;
        }
        if(State.CompletedHours-t.BirthHour<24) return;
        long equity=t.Equity(State.Stocks);
        bool exhausted=t.IsRetail && equity<=1000 && t.Shares.All(q=>q==0);
        if(equity<=0 || exhausted) FailTrader(t,exhausted ? "생활·거래 자금 소진" : "부채 초과·지급 불능");
    }
    long FundReplacement(Trader t)
    {
        long wanted=t.IsRetail ? RetailInitialCapital : InitialCash;
        if(State.RealEconomy.Cash<wanted) { t.WaitingForCapital=true; return 0; }
        TransferCash(State.RealEconomy,t,wanted,"replacement-capital");
        t.OpeningCash+=wanted; t.OpeningEquity+=wanted; t.SeasonOpeningEquity=t.Equity(State.Stocks);
        t.LastReturnEquity=t.SeasonOpeningEquity; t.LastNetContribution=t.NetContribution;
        t.WaitingForCapital=false;
        if(!t.IsRetail) { t.OpeningSnapshot=CaptureTrader(t); t.OpeningSnapshot.NetIncome=0; t.SeasonSnapshot=CaptureTrader(t); }
        return wanted;
    }
    internal void FailTrader(Trader t,string reason)
    {
        CancelOrders(t.Id);
        var f=t.Financials(State.Stocks);
        var record=new BankruptcyRecord { Id=State.NextBankruptcyId++,Hour=State.CompletedHours,
            Kind=t.IsRetail ? BankruptcyKind.Retail : BankruptcyKind.Institution,
            EntityId=$"{t.Id}:g{t.Generation}",Name=t.IsRetail ? $"개인 {t.Id-AiCount}" : t.Name,
            Generation=t.Generation,SuccessorId=$"{t.Id}:g{t.Generation+1}",Reason=reason,
            Assets=f.Assets,Liabilities=f.Liabilities,Equity=f.Equity,
            TradingIncome=f.NetIncome-t.NetContribution };
        long inKind=0;
        for(int i=0;i<t.Shares.Length;i++)
        {
            long quantity=t.Shares[i]; if(quantity==0) continue;
            var bank=BankAccount(); long old=bank.Shares[i];
            bank.AverageCost[i]=(old*bank.AverageCost[i]+(double)State.Stocks[i].Value(quantity))/(old+quantity);
            bank.Shares[i]+=quantity; t.Shares[i]=0;
            inKind=checked(inKind+State.Stocks[i].Value(quantity));
        }
        long secured=Math.Min(t.LoanDebt,inKind); t.LoanDebt-=secured; inKind-=secured;
        long loanPaid=Math.Min(t.Cash,t.LoanDebt); TransferCash(t,State.Bank,loanPaid,"bankruptcy-loan-recovery"); t.LoanDebt-=loanPaid;
        record.LoanWriteOff=t.LoanDebt; State.Bank.LoanWriteOffs+=t.LoanDebt;
        long shortClaim=checked(t.ShortLiability(State.Stocks)+t.ShortDividendDebt);
        long shortRecovered=Math.Min(shortClaim,inKind); shortClaim-=shortRecovered;
        long shortPaid=Math.Min(t.Cash,shortClaim); TransferCash(t,State.Bank,shortPaid,"bankruptcy-short-recovery"); shortClaim-=shortPaid;
        record.ShortWriteOff=shortClaim; State.Bank.ShortWriteOffs+=shortClaim;
        // A defaulted stock loan is a lost bank claim, never newly minted inventory.
        long finePaid=Math.Min(t.Cash,t.FineDebt); TransferCash(t,State.Government,finePaid,"bankruptcy-fine-recovery");
        record.FineWriteOff=t.FineDebt-finePaid; State.Government.FineWriteOffs+=record.FineWriteOff;
        long taxPaid=Math.Min(t.Cash,t.TaxDebt); TransferCash(t,State.Government,taxPaid,"bankruptcy-tax-recovery");
        record.TaxWriteOff=t.TaxDebt-taxPaid; State.Government.TaxWriteOffs+=record.TaxWriteOff;
        TransferCash(t,State.RealEconomy,t.Cash,"bankruptcy-estate");
        foreach(var op in State.Operations.Where(o=>o.Status==OperationStatus.Active && (o.LeaderId==t.Id || o.PartnerId==t.Id)))
        { op.Status=OperationStatus.Failed; op.EndsHour=State.CompletedHours; op.Detail="참여 기관 파산으로 종료"; }
        var next=new Trader { Id=t.Id,IsRetail=t.IsRetail,Generation=t.Generation+1,BirthHour=State.CompletedHours,
            Name=t.IsRetail ? "" : $"{Names[(t.Id+ t.Generation*3-1)%Names.Length]} {t.Id:000} · {t.Generation+1}기",
            Strategy=t.Strategy,Risk=.15+Next()*.7,Patience=.2+Next()*.6,WaitingForCapital=true };
        if(!next.IsRetail) InitializeRepresentative(next);
        Endow(next,0);
        if(next.IsRetail) State.Retail[next.Id-AiCount-1]=next; else State.Bots[next.Id-1]=next;
        record.ReplacementCapital=FundReplacement(next);
        RebuildBooks(); RecordBankruptcy(record);
    }
    internal void FailCompany(int index)
    {
        var stock=State.Stocks[index]; if(!stock.Active) return;
        CancelSecurity(index); CancelOrders(0);
        foreach(var vote in State.CompanyVotes.Where(v=>v.SecurityId==stock.SecurityId && v.Status==VoteStatus.Open))
        { vote.Status=VoteStatus.Cancelled; vote.CloseHour=State.CompletedHours; RecordVote(vote); }
        var report=CopyReport(stock.Report); stock.Report=CopyReport(stock.Report);
        var estate=stock.Report;
        var record=new BankruptcyRecord { Id=State.NextBankruptcyId++,Hour=State.CompletedHours,Kind=BankruptcyKind.Company,
            EntityId=stock.SecurityId,Name=stock.Name,Generation=stock.Generation,Reason=estate.Equity<=0 ? "자본 잠식" : "3개월 운영 자금 부족",
            Assets=estate.Assets,Liabilities=estate.Liabilities,Equity=estate.Equity,Report=report };
        CompanyReportRecorded?.Invoke(report);
        long recovery=Math.Min(State.RealEconomy.Cash/10,checked(estate.Inventory/2+estate.Receivables/2+estate.FixedAssets/3));
        TransferCash(State.RealEconomy,estate,recovery,"company-liquidation-assets");
        long loanPaid=Math.Min(estate.Cash,estate.Debt); TransferCash(estate,State.Bank,loanPaid,"company-liquidation-loan");
        record.LoanWriteOff=estate.Debt-loanPaid; State.Bank.LoanWriteOffs+=record.LoanWriteOff;
        long tradePaid=Math.Min(estate.Cash,estate.TradePayables); TransferCash(estate,State.RealEconomy,tradePaid,"company-liquidation-costs");
        record.TradeWriteOff=estate.TradePayables-tradePaid;
        long perShare=estate.Cash/Math.Max(1,stock.OutstandingShares);
        foreach(var t in Participants)
        {
            long cash=checked(t.Shares[index]*perShare); double profit=cash-t.Shares[index]*t.AverageCost[index];
            TransferCash(estate,t,cash,"company-liquidation-share"); t.SellCashFlow+=cash; t.RealizedProfit+=profit;
            t.Shares[index]=0; t.AverageCost[index]=0;
            long shortCompensation=checked(t.ShortShares[index]*perShare);
            long paid=Math.Min(t.Cash-t.ReservedCash,shortCompensation);
            TransferCash(t,State.Bank,paid,"company-liquidation-short");
            t.ShortDividendExpense+=shortCompensation; t.ShortDividendPaid+=paid; t.ShortDividendDebt+=shortCompensation-paid;
            profit+=t.ShortShares[index]*t.ShortAveragePrice[index]; t.RealizedProfit+=t.ShortShares[index]*t.ShortAveragePrice[index];
            t.ShortShares[index]=0; t.ShortAveragePrice[index]=0; PayTradeTax(t,profit);
        }
        TransferCash(estate,State.Bank,checked(State.Bank.ShareInventory[index]*perShare),"company-liquidation-bank-share");
        TransferCash(estate,State.RealEconomy,estate.Cash,"company-liquidation-estate");
        stock.TotalShares=stock.TreasuryShares=stock.FounderShares=0; stock.Active=false;
        State.Bank.ShareInventory[index]=0; State.Bank.ReservedLending[index]=0;
        var definition=CompanyCatalog.Companies.First(c=>c.Symbol==stock.Symbol);
        record.ReplacementCapital=ListCompany(definition,index,stock.Generation+1);
        record.SuccessorId=State.Stocks[index].SecurityId;
        RebuildBooks(); RecordBankruptcy(record);
    }
}
