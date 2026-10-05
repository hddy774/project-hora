namespace AlphaExchange.Core;

public readonly record struct InvestmentSignal(double Score, double TargetExposure, double StockLimit, double SectorLimit,
    bool Defensive, double Volatility, InvestmentHorizon Horizon=InvestmentHorizon.Short);

public sealed partial class GameEngine
{
    double[] trends=[],fastTrends=[],longTrends=[],volatilities=[],dividendYields=[];
    InvestmentSignal[] institutionSignals=[];
    int[] signalSectors=[];
    double[] portfolioSectors=[];
    string[] sectorNames=[];
    readonly Dictionary<string,int> sectorIds=new(StringComparer.Ordinal);
    double marketTrend;
    internal void PrepareMarketSignals()
    {
        int count=State.Stocks.Count;
        if(trends.Length!=count)
        {
            trends=new double[count]; fastTrends=new double[count]; longTrends=new double[count]; volatilities=new double[count];
            dividendYields=new double[count]; institutionSignals=new InvestmentSignal[count]; signalSectors=new int[count];
            portfolioSectors=new double[count];
        }
        bool changed=sectorNames.Length!=count;
        if(!changed) for(int i=0;i<count;i++) if(sectorNames[i]!=State.Stocks[i].Sector) { changed=true; break; }
        if(changed)
        {
            sectorNames=new string[count]; sectorIds.Clear();
            for(int i=0;i<count;i++)
            {
                string name=sectorNames[i]=State.Stocks[i].Sector;
                if(!sectorIds.TryGetValue(name,out int sector)) sectorIds.Add(name,sector=sectorIds.Count);
                signalSectors[i]=sector;
            }
        }
        var tenDays=State.DailyHistory.LastOrDefault(p=>p.Hour<=State.CompletedHours-Rules.Horizons[2].MinimumHours);
        marketTrend=0; int active=0;
        for(int i=0;i<count;i++)
        {
            var s=State.Stocks[i]; int n=s.History.Count; dividendYields[i]=s.DividendYield;
            trends[i]=(double)s.PreviousPrice/s.History[Math.Max(0,n-24)]-1;
            fastTrends[i]=(double)s.PreviousPrice/s.History[Math.Max(0,n-3)]-1;
            longTrends[i]=trends[i];
            if(tenDays is not null && i<tenDays.SecurityIds.Length && i<tenDays.SplitFactors.Length && tenDays.SplitFactors[i]>0 && tenDays.SecurityIds[i]==s.SecurityId)
                longTrends[i]=(double)s.PreviousPrice/(tenDays.StockPrices[i]*tenDays.SplitFactors[i]/s.SplitFactor)-1;
            double sum=0; int samples=0;
            for(int j=Math.Max(1,n-12);j<n;j++) { sum+=Math.Abs(s.History[j]/s.History[j-1]-1); samples++; }
            volatilities[i]=s.Volatility=samples==0 ? 0 : sum/samples;
            if(s.Active) { marketTrend+=trends[i]; active++; }
        }
        marketTrend/=Math.Max(1,active);
    }
    public InvestmentSignal Analyze(Trader t,int index)
    {
        if(t.IsRetail || t.Id==0) throw new ArgumentException("기관 전용 판단입니다.",nameof(t));
        if(index<0 || index>=State.Stocks.Count) throw new ArgumentOutOfRangeException(nameof(index));
        PrepareMarketSignals(); return Analyze(t,index,t.Equity(State.Stocks),Capabilities(t).Effective);
    }
    InvestmentSignal Analyze(Trader t,int index,long equity,Abilities a)
    {
        var s=State.Stocks[index]; var r=Rules.Investment;
        double risk=a.RiskManagement/100.0,macro=a.Macro/100.0;
        bool defensive=marketTrend<r.FallingMarket || trends[index]<r.FallingStock || volatilities[index]>r.VolatilityRisk ||
            equity<t.SeasonOpeningEquity*(1-r.DrawdownRisk);
        double cash=r.BaseCash+r.DispositionCash[(int)t.Disposition]-t.Risk*r.RiskCashEffect+(defensive ? r.DefensiveCash*risk : 0);
        cash=Math.Clamp(cash,r.MinimumCash,r.MaximumCash);
        double value=Math.Clamp(s.FairValue/s.PreviousPrice-1,-1,2);
        InvestmentHorizon horizon=value>r.LongDiscount && s.Report.NetIncome>0 && t.Disposition!=Disposition.Aggressive
            ? InvestmentHorizon.Long : Math.Abs(fastTrends[index])>=r.FastMove &&
                (a.Scalping>a.SwingTrading || t.Disposition is Disposition.Opportunistic or Disposition.Aggressive)
                ? InvestmentHorizon.UltraShort : InvestmentHorizon.Short;
        var d=t.Development!;
        if((t.Shares[index]>0 || t.ShortShares[index]>0) && d.NextReviewHours[index]>State.CompletedHours &&
            Math.Abs(s.Sentiment)<r.MaterialNews && !defensive && value>=r.ThesisBreakValue)
            horizon=d.PositionHorizons[index];
        var method=Rules.Horizons[(int)horizon];
        double Skill(int skill)=>r.SkillFloor+(1-r.SkillFloor)*skill/100.0;
        double trend=horizon==InvestmentHorizon.Long ? longTrends[index] : trends[index];
        double score=value*method.ValueWeight*Skill(a.Valuation)+trend*method.TrendWeight*Skill(a.Technical)+
            fastTrends[index]*method.FastWeight*Skill(method.FastWeight<0 ? a.SwingTrading : a.Scalping)+
            s.Sentiment*method.NewsWeight*Skill(a.News)+dividendYields[index]*method.DividendWeight*Skill(a.Valuation);
        score-=volatilities[index]*risk+(s.Report.DebtRatio*State.Government.Policy.BaseRate)*macro;
        double spread=bids[index].Count>0 && asks[index].Count>0 ?
            Math.Max(0,(asks[index][0].Price-bids[index][0].Price)/(double)s.PreviousPrice) : r.BaseSpread*2;
        double roundTrip=State.Government.Policy.FeeBasisPoints/10000.0*2+spread;
        // A short holding must clear more trading friction per game day; long
        // positions amortize it, and dividends remain part of their thesis.
        double cost=roundTrip*24/Math.Max(24,method.ExpectedHours);
        score-=Math.Sign(score)*Math.Min(Math.Abs(score),cost*Skill(100-a.Execution));
        return new(score,1-cash,r.StockLimit-a.Diversification/100.0*r.DiversificationStockEffect,
            r.SectorLimit-a.Diversification/100.0*r.DiversificationSectorEffect,defensive,volatilities[index],horizon);
    }
    void DecideInstitution(Trader t)
    {
        if(t.WaitingForCapital) return;
        var d=t.Development!; var r=Rules.Investment; var stocks=State.Stocks;
        long gross=t.Cash,shorts=0; Array.Clear(portfolioSectors);
        for(int i=0;i<stocks.Count;i++)
        {
            long value=stocks[i].Value(t.Shares[i]); gross=checked(gross+value);
            portfolioSectors[signalSectors[i]]+=value; shorts=checked(shorts+stocks[i].Value(t.ShortShares[i]));
        }
        long equity=gross-shorts-t.LoanDebt-t.FineDebt-t.TaxDebt-t.ShortDividendDebt;
        double ratio=(double)t.Cash/Math.Max(1,gross);
        bool margin=(long)Math.Ceiling(shorts*1.5)+t.ReservedCash>t.Cash;
        bool cashBoundary=ratio<r.MinimumCash || ratio>r.MaximumCash+r.CashTolerance;
        bool marketEmergency=marketTrend<r.FallingMarket && State.CompletedHours-d.LastDecisionHour>=r.EmergencyReviewHours;
        bool newsEmergency=false;
        for(int i=0;i<stocks.Count;i++) if((t.Shares[i]>0 || t.ShortShares[i]>0) &&
            (Math.Abs(stocks[i].Sentiment-d.LastNewsSignals[i])>=r.MaterialNews || stocks[i].HourlyChange<r.FallingStock)) { newsEmergency=true; break; }
        if(State.CompletedHours<d.NextDecisionHour && !margin && !cashBoundary && !marketEmergency && !newsEmergency) return;
        CancelOrders(t.Id); d.PortfolioReviews++; d.LastDecisionHour=State.CompletedHours;
        var a=Capabilities(t).Effective; bool defensive=false; double bestScore=double.MinValue;
        var signals=institutionSignals; double cashTarget=r.MinimumCash;
        for(int i=0;i<stocks.Count;i++)
        {
            signals[i]=Analyze(t,i,equity,a);
            d.LastNewsSignals[i]=stocks[i].Sentiment;
            if(stocks[i].Active) { defensive|=signals[i].Defensive; bestScore=Math.Max(bestScore,signals[i].Score); cashTarget=Math.Max(cashTarget,1-signals[i].TargetExposure); }
        }
        d.TargetCashRatio=Math.Clamp(cashTarget,r.MinimumCash,r.MaximumCash);
        long holdings=gross-t.Cash;
        var loan=LoanTerms(t);
        long free=Math.Max(0,t.Cash-t.ReservedCash-(long)Math.Ceiling(shorts*1.5));
        if(t.LoanDebt>0 && (t.LoanDebt>loan.Limit || defensive && free>gross*d.TargetCashRatio))
            RepayLoan(t.Id,Math.Max(t.LoanDebt-loan.Limit,Math.Max(0,free-(long)(gross*d.TargetCashRatio))));
        else if(!defensive && ratio<=r.MaximumCash && t.CreditScore>=r.CreditBorrowMinimum && bestScore>r.BorrowScore && t.LoanDebt<equity*r.MaximumLoanRatio)
            BorrowCash(t.Id,Math.Min(loan.Available,Math.Max(0,(long)(equity*r.MaximumLoanRatio)-t.LoanDebt)));
        if(margin) { t.MarginCalls++; t.Decision="담보 부족: 상환·보유 주식 청산"; }
        else t.Decision=defensive ? $"하락 방어 · 현금 목표 {d.TargetCashRatio:P0} · 분산·부채/헤지 검토" : $"현금 목표 {d.TargetCashRatio:P0} · 가치/뉴스/비용·보유 기간 재평가";
        // Rank the bounded order batch once. Recheck cash and exposure at execution;
        // only the selected security can change this trader's portfolio valuation.
        gross=Math.Max(1,t.Cash+holdings); ratio=(double)t.Cash/gross;
        bool initiallySelling=margin || ratio<d.TargetCashRatio-r.CashTolerance || t.LoanDebt>loan.Limit;
        Span<int> candidates=stackalloc int[r.OrderAttempts];
        Span<double> priorities=stackalloc double[r.OrderAttempts];
        int candidateCount=0,first=(int)(Next()*stocks.Count);
        for(int j=0;j<stocks.Count;j++)
        {
            int i=(first+j)%stocks.Count; if(!stocks[i].Active) continue;
            bool due=d.NextReviewHours[i]<=State.CompletedHours || margin || cashBoundary || marketEmergency || newsEmergency;
            if(!due || initiallySelling && t.Shares[i]==0 && t.ShortShares[i]==0) continue;
            var signal=signals[i]; double weight=(double)stocks[i].Value(t.Shares[i])/gross;
            double priority=initiallySelling && t.Shares[i]>t.ReservedShares[i] ? weight+1 :
                t.ShortShares[i]>0 && (margin || signal.Score>r.BuyScore || !State.Government.Policy.ShortSellingAllowed) ? 2 :
                signal.Score-weight*r.DiversificationStockEffect;
            int place=candidateCount;
            while(place>0 && priority>priorities[place-1]) place--;
            if(place>=candidates.Length) continue;
            int last=Math.Min(candidateCount,candidates.Length-1);
            for(int p=last;p>place;p--) { candidates[p]=candidates[p-1]; priorities[p]=priorities[p-1]; }
            candidates[place]=i; priorities[place]=priority;
            candidateCount=Math.Min(candidateCount+1,candidates.Length);
        }
        for(int attempt=0;attempt<candidateCount;attempt++)
        {
            gross=Math.Max(1,t.Cash+holdings); ratio=(double)t.Cash/gross;
            bool sellForCash=margin || ratio<d.TargetCashRatio-r.CashTolerance || t.LoanDebt>loan.Limit;
            bool deploying=ratio>r.MaximumCash+r.CashTolerance;
            int index=candidates[attempt];
            var stock=stocks[index]; var selected=signals[index];
            long beforeHolding=stock.Value(t.Shares[index]),beforeShort=stock.Value(t.ShortShares[index]);
            try
            {
            var method=Rules.Horizons[(int)selected.Horizon];
            d.PositionHorizons[index]=selected.Horizon; d.NextReviewHours[index]=State.CompletedHours+method.ReviewHours;
            int desired=Math.Max(1,(int)Math.Min(1_000_000,gross*(deploying ? r.DeployOrderRatio : r.OrderRatio+a.Execution/100.0*r.OrderSkillRatio)/stock.Price));
            double spread=r.BaseSpread+(1-a.Execution/100.0)*r.SkillSpread;
            double offset=Math.Clamp(selected.Score*spread+(Next()-.5)*r.QuoteNoise*(1-a.Technical/100.0),-spread*2,spread*2);
            bool exitShort=t.ShortShares[index]>0 && (margin || !State.Government.Policy.ShortSellingAllowed || selected.Score>r.BuyScore ||
                Math.Abs((double)stock.Price/Math.Max(1,t.ShortAveragePrice[index])-1)>r.ShortExitReturn);
            if(exitShort) { CoverPosition(t,index,desired,margin); continue; }
            double weight=(double)beforeHolding/gross;
            double sectorWeight=portfolioSectors[signalSectors[index]]/gross;
            bool overweight=weight>=selected.StockLimit || sectorWeight>=selected.SectorLimit;
            bool buy=!margin && !sellForCash && !overweight && ratio>d.TargetCashRatio &&
                (selected.Score>r.BuyScore || deploying && stock.Report.Equity>0);
            if(buy)
            {
                double budget=Math.Min(selected.StockLimit-weight,Math.Min(selected.SectorLimit-sectorWeight,ratio-d.TargetCashRatio));
                bool take=selected.Score>=r.TakeLiquidityScore || deploying;
                int price=Quote(stock.Price*(1+offset+(take ? spread : -spread)),true);
                int q=Math.Min(desired,Math.Min(AiBuyCapacity(t,price,gross,shorts),Math.Max(0,(int)(gross*budget/price))));
                if(q>0) SubmitOrder(t.Id,index,true,price,q,method.OrderHours);
            }
            else if(t.Shares[index]>t.ReservedShares[index])
            {
                bool broken=stock.FairValue/stock.Price-1<r.ThesisBreakValue || stock.Report.Equity<=0;
                bool thesisExit=selected.Score<method.ExitScore;
                if(sellForCash || overweight || broken || thesisExit)
                {
                    int price=sellForCash && bids[index].Count>0 ? bids[index][0].Price : Quote(stock.Price*(1+offset+(broken ? -spread : spread)),false);
                    int q=(int)Math.Min(t.Shares[index]-t.ReservedShares[index],sellForCash ? Math.Max(desired,(int)Math.Min(1_000_000,Math.Ceiling(Math.Max(0,d.TargetCashRatio*gross-t.Cash)/price))) : desired);
                    if(q>0) SubmitOrder(t.Id,index,false,price,q,method.OrderHours);
                }
            }
            else if(selected.Score<r.ShortScore && trends[index]<0 && fastTrends[index]<=0 &&
                a.RiskManagement>=Rules.Representative.InitialAbility*Rules.Representative.Weight &&
                shorts<Math.Max(0,equity)*r.MaximumShortRatio)
            {
                int price=Quote(stock.Price*(1-spread),false);
                int q=Math.Min(desired,(int)Math.Max(0,(t.Cash-t.ReservedCash-Math.Ceiling(shorts*1.5)-gross*r.MinimumCash)/(price/2.0+ExchangeFee(price))));
                if(q>0) SubmitOrder(t.Id,index,false,price,q,method.OrderHours,shortSale:true);
            }
            }
            finally
            {
                long change=stock.Value(t.Shares[index])-beforeHolding;
                holdings=checked(holdings+change); portfolioSectors[signalSectors[index]]+=change;
                shorts=checked(shorts+stock.Value(t.ShortShares[index])-beforeShort);
            }
        }
        if(margin)
            for(int i=0;i<stocks.Count;i++) if(t.ShortShares[i]>0 && t.ReservedCovers[i]==0) CoverPosition(t,i,1_000_000,true);
        d.NextDecisionHour=State.CompletedHours+Rules.Horizons[(int)InvestmentHorizon.Long].ReviewHours;
        Span<int> counts=stackalloc int[3]; bool anyPosition=false;
        for(int i=0;i<stocks.Count;i++) if(t.Shares[i]>0 || t.ShortShares[i]>0)
        { anyPosition=true; counts[(int)d.PositionHorizons[i]]++; d.NextDecisionHour=Math.Min(d.NextDecisionHour,Math.Max(State.CompletedHours+1,d.NextReviewHours[i])); }
        if(!anyPosition) d.NextDecisionHour=State.CompletedHours+Rules.Horizons[(int)InvestmentHorizon.Short].ReviewHours;
        if(marketEmergency || newsEmergency) d.NextDecisionHour=Math.Min(d.NextDecisionHour,State.CompletedHours+r.EmergencyReviewHours);
        t.ActiveMethods=$"초단기 {counts[0]} · 단기 {counts[1]} · 장기 {counts[2]}종목";
        var operation=State.Operations.FirstOrDefault(o=>o.Status==OperationStatus.Active && (o.LeaderId==t.Id || o.PartnerId==t.Id));
        if(operation is not null && !margin && CashRatio(t)>d.TargetCashRatio)
        {
            int i=operation.StockIndex; var signal=signals[i]; long value=stocks[i].Value(t.Shares[i]); gross=Math.Max(1,t.GrossAssets(stocks));
            if(value<gross*signal.StockLimit)
            {
                int price=Quote(stocks[i].Price*(1+r.BaseSpread),true);
                int q=Math.Min(AiBuyCapacity(t,price),Math.Min(2,(int)((gross*signal.StockLimit-value)/price)));
                if(q>0) SubmitOrder(t.Id,i,true,price,q,1);
            }
            t.Decision="대표 공동 작전 참여 · 적발/벌금 위험";
        }
    }
    int AiBuyCapacity(Trader t,int price)
        => AiBuyCapacity(t,price,t.GrossAssets(State.Stocks),t.ShortLiability(State.Stocks));
    int AiBuyCapacity(Trader t,int price,long gross,long shorts)
    {
        long budget=Math.Max(0,t.Cash-t.ReservedCash-(long)Math.Ceiling(shorts*1.5)-(long)Math.Ceiling(gross*Rules.Investment.MinimumCash));
        return (int)Math.Min(1_000_000,budget/(price+ExchangeFee(price)));
    }
    void CoverPosition(Trader t,int index,int desired,bool margin)
    {
        int price=asks[index].Count>0 ? asks[index][0].Price : Quote(State.Stocks[index].Price*(1+Rules.Investment.BaseSpread*2),true);
        int q=(int)Math.Min(t.ShortShares[index]-t.ReservedCovers[index],margin ? 1_000_000 : desired);
        while(q>0 && SubmitOrder(t.Id,index,true,price,q,1,cover:true) is not null) q/=2;
    }
    static int Quote(double price,bool buy)
    { int tick=price<1000 ? 1 : 10; return Math.Clamp((int)(buy ? Math.Ceiling(price/tick) : Math.Floor(price/tick))*tick,1,10_000_000); }
}
