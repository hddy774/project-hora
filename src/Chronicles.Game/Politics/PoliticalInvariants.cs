namespace Chronicles.Game.Politics;

public static class PoliticalInvariants
{
    public static IReadOnlyList<string> Validate(PoliticalContext context, PoliticalState state)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var errors = new List<string>();
        if (state.Schema != 1) errors.Add("Unsupported political state schema.");
        if (context.Rules.PlacesPerProvince <= 0 || context.Rules.MaturityMonths < 0 || context.Rules.SectLimit < 0 ||
            context.Rules.FoundingTreasury < 0 || context.Rules.PolicyPreparationCost < 0 || context.Rules.PreparationMonths <= 0 ||
            context.Rules.PreparationWorkers <= 0 || context.Rules.TaxBasisPoints is < 0 or > 10000 ||
            context.Rules.TributeBasisPoints is < 0 or > 10000 || context.Rules.BaseSalary < 0 ||
            context.Rules.ActivityGrant < 0 || context.Rules.ReliefThreshold < 0)
            errors.Add("Political rule values are outside their supported units or ranges.");
        if (context.People.Select(p => p.Id).Distinct().Count() != context.People.Count ||
            context.Groups.Select(g => g.Id).Distinct().Count() != context.Groups.Count ||
            context.People.Any(p => context.Groups.Any(g => g.Id == p.Id)) ||
            context.Places.Select(p => p.Id).Distinct().Count() != context.Places.Count)
            errors.Add("Political inputs contain duplicate stable entity IDs.");
        if (context.People.Any(p => p.Money < 0) || context.Groups.Any(g => g.Money < 0 || g.PolicyEscrow < 0) ||
            state.ConsumedMoney < 0 || state.Preparations.Any(p => p.Escrow < 0)) errors.Add("Political money cannot be negative.");
        var allIds = state.Claims.Select(x => x.Id).Concat(state.Obligations.Select(x => x.Id))
            .Concat(state.Passage.Select(x => x.Id)).Concat(state.Accords.Select(x => x.Id)).Concat(state.Charters.Select(x => x.Id))
            .Concat(state.Labor.Select(x => x.Id)).Concat(state.Effects.Select(x => x.Id)).Concat(state.Preparations.Select(x => x.Id)).ToArray();
        if (allIds.Any(x => x <= 0 || x >= state.NextId) || allIds.Distinct().Count() != allIds.Length || state.NextId <= 0)
            errors.Add("Political facts require unique monotonic IDs.");
        foreach (var g in context.Groups.Where(g => !g.Dissolved))
        {
            if (PoliticsEngine.TopOverlord(context, g.Id) == 0) errors.Add("Vassalage must be a DAG of existing living organizations.");
            if (g.Overlord != 0 && (g.Kind == GroupKind.Sect || !context.Groups.Any(top => top.Id == g.Overlord && !top.Dissolved && top.Kind == GroupKind.Dynasty)))
                errors.Add("Only non-sect organizations may owe political allegiance to a current dynasty.");
            if (g.Members.Distinct().Count() != g.Members.Count || g.Members.Any(id => !context.People.Any(p => p.Id == id)))
                errors.Add("Group membership must reference distinct real people.");
            if (g.Leader != 0 && !g.Members.Contains(g.Leader)) errors.Add("The current leader must be a current member.");
            foreach (var province in g.Provinces)
            {
                var places = context.Places.Where(p => p.Province == province).ToArray();
                if (g.Kind != GroupKind.Dynasty || places.Length != context.Rules.PlacesPerProvince || places.Any(p => p.Sovereign != g.Id))
                    errors.Add("A dynasty province must correspond to its complete actual sovereignty.");
            }
        }
        foreach (var p in context.Places)
        {
            if (p.Sovereign != 0 && !context.Groups.Any(g => !g.Dissolved && g.Kind == GroupKind.Dynasty && g.Id == p.Sovereign && g.Provinces.Contains(p.Province)))
                errors.Add("Sovereign places require an actual dynasty and controlled province.");
            if (p.Delegate != 0 && (p.Sovereign == 0 || !context.Groups.Any(g => !g.Dissolved && g.Id == p.Delegate)))
                errors.Add("Delegation must have a current sovereign and real administrator.");
        }
        if (state.Claims.Any(c => !context.Groups.Any(g => g.Id == c.Group) || !context.Places.Any(p => p.Province == c.Province)))
            errors.Add("Territorial claims reference historical groups and actual provinces.");
        if (state.Claims.Where(c => c.Active).GroupBy(c => (c.Group, c.Province)).Any(g => g.Count() > 1))
            errors.Add("The same active territorial claim cannot be duplicated.");
        if (state.Obligations.Any(o => o.Outstanding < 0 || o.Outstanding > 0 && o.Creditor == o.Debtor ||
            !KnownAccount(context, o.Creditor) || !KnownAccount(context, o.Debtor) ||
            o.Enforcer != 0 && !context.Groups.Any(g => g.Id == o.Enforcer)))
            errors.Add("Obligations require separate real creditor and debtor accounts and explicit enforcement ownership.");
        if (state.Payments.Any(p => p.Amount < 0 || !KnownAccount(context, p.From) || !KnownAccount(context, p.To)))
            errors.Add("Payment history requires actual accounts and nonnegative transfers.");
        if (state.Passage.Any(p => !KnownAccount(context, p.Holder) || !context.Groups.Any(g => g.Id == p.Grantor) || !context.Places.Any(at => at.Id == p.Place)) ||
            state.Charters.Any(c => !context.Groups.Any(g => g.Id == c.Sect && g.Kind == GroupKind.Sect) ||
                !context.Groups.Any(g => g.Id == c.Sovereign) || !context.Places.Any(p => p.Province == c.Province) || c.Until <= c.Granted) ||
            state.Accords.Any(a => a.FirstGroup >= a.SecondGroup || a.Level is < 0 or > 2 ||
                !context.Groups.Any(g => g.Id == a.FirstGroup) || !context.Groups.Any(g => g.Id == a.SecondGroup)))
            errors.Add("Charters, passage and accords must reference real political actors, places and valid terms.");
        if (state.Effects.Any(e => e.Amount < 0 || e.Until < e.From || !context.People.Any(p => p.Id == e.Person) ||
            !context.Groups.Any(g => g.Id == e.Group) || !context.Places.Any(p => p.Id == e.Place) ||
            !Enum.IsDefined(e.Kind) ||
            e.Kind == PoliticalEffectKind.Teaching && !context.People.Any(p => p.Id == e.Other)))
            errors.Add("Policy effects require actual inputs and nonnegative funded amounts.");
        if (state.Labor.Any(r => r.From >= r.Until || !context.People.Any(p => p.Id == r.Person) ||
            !context.Groups.Any(g => g.Id == r.Group && !g.Dissolved))) errors.Add("Labor reservations require actual people, organizations and nonempty integer intervals.");
        foreach (var r in state.Labor)
            if (state.Labor.Any(other => other.Id != r.Id && other.Person == r.Person && other.From < r.Until && r.From < other.Until))
            { errors.Add("A person's labor cannot be reserved twice across political categories or groups."); break; }
        if (state.TaxPeriods.Distinct().Count() != state.TaxPeriods.Count || state.TaxPeriods.Any(p => p <= 0 || p % 12 != 0))
            errors.Add("A completed integer tax year may settle only once.");
        if (state.Offices.GroupBy(x => (x.Group, x.Name)).Any(g => g.Count() > 1) ||
            state.Offices.Any(o => !context.Groups.Any(g => !g.Dissolved && g.Id == o.Group && g.Members.Contains(o.Holder))))
            errors.Add("Offices reference one actual current member per office.");
        if (state.Service.GroupBy(x => (x.Group, x.Person)).Any(g => g.Count() > 1) ||
            state.Service.Any(s => s.Contribution < 0 || s.Loyalty is < 0 or > 100 || !context.People.Any(p => p.Id == s.Person) || !context.Groups.Any(g => g.Id == s.Group)))
            errors.Add("Contribution and loyalty records must reference real people and bounded values.");
        if (state.Preparations.Any(p => !context.Groups.Any(g => g.Id == p.Group) || !PolicyCatalog.IsChoice(p.Category, p.Choice) ||
            !Enum.IsDefined(p.Stage) || !context.People.Any(person => person.Id == p.Author) || !context.Places.Any(at => at.Id == p.Place) ||
            p.Workers.Distinct().Count() != p.Workers.Count ||
            p.Stage == PolicyStage.Preparing && (p.Due <= p.Adopted || p.RequiredWorkers <= 0 || p.Workers.Count != p.RequiredWorkers ||
                p.Workers.Any(id => !state.Labor.Any(r => r.Person == id && r.Reference == p.Id && r.Purpose == "policy"))) ||
            p.Stage != PolicyStage.Preparing && p.Escrow != 0))
            errors.Add("Policy preparation stage, escrow and labor evidence must agree.");
        return errors;
    }

    private static bool KnownAccount(PoliticalContext context, PoliticalAccount account) => Enum.IsDefined(account.Kind) && account.Id > 0 &&
        (account.Kind == PoliticalAccountKind.Person ? context.People.Any(p => p.Id == account.Id) : context.Groups.Any(g => g.Id == account.Id));
}
