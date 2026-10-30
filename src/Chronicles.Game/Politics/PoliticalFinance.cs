namespace Chronicles.Game.Politics;

public static partial class PoliticsEngine
{
    public static PoliticalAccountBalance? DescribeAccount(PoliticalContext context, PoliticalState state, PoliticalAccount account)
    {
        var person = account.Kind == PoliticalAccountKind.Person ? context.People.FirstOrDefault(p => p.Id == account.Id) : null;
        var group = account.Kind == PoliticalAccountKind.Group ? context.Groups.FirstOrDefault(g => g.Id == account.Id) : null;
        if (person is null && group is null) return null;
        var reserved = group is null ? 0 : checked(group.PolicyEscrow + state.Preparations.Where(p => p.Group == account.Id).Sum(p => p.Escrow));
        return new(account, person?.Money ?? group!.Money, reserved,
            state.Obligations.Where(o => o.Creditor == account).Sum(o => o.Outstanding),
            state.Obligations.Where(o => o.Debtor == account).Sum(o => o.Outstanding));
    }

    public static bool CanEnforce(PoliticalContext context, PoliticalObligation obligation) =>
        obligation.Outstanding > 0 && obligation.Enforcer != 0 && context.Groups.Any(g =>
            g.Id == obligation.Enforcer && !g.Dissolved && g.Leader != 0 &&
            context.People.Any(p => p.Id == g.Leader && p.Status == LifeStatus.Alive)) &&
        context.Places.Any(p => p.Province == obligation.Province && p.Sovereign == obligation.Enforcer) &&
        DebtorWithinJurisdiction(context, obligation.Debtor, obligation.Province);

    public static PoliticalResult PlanLoan(PoliticalContext context, PoliticalState state, long actor,
        PoliticalAccount creditor, PoliticalAccount debtor, long debtorConsent, long amount,
        long enforcer, int province, string basis)
    {
        var d = new PoliticalDraft(context, state);
        if (!Authorized(context, actor, creditor) || !Authorized(context, debtorConsent, debtor) || creditor == debtor ||
            !Exists(context, creditor) || !Exists(context, debtor) || amount <= 0 || d.Money(creditor) < amount ||
            string.IsNullOrWhiteSpace(basis) ||
            enforcer != 0 && !context.Groups.Any(g => !g.Dissolved && g.Id == enforcer && g.Provinces.Contains(province)))
            return PoliticalResult.Rejected("A loan requires real funds, separate consenting accounts and an explicit current enforcement owner.");
        var id = d.Id();
        d.Transfer(creditor, debtor, amount, "loan principal", id);
        d.State.Obligations.Add(new(id, creditor, debtor, enforcer, province, amount, basis, context.Month));
        return d.Finish("Transferred actual loan principal and created exactly one receivable with separate creditor, debtor and enforcement ownership.");
    }

    public static PoliticalResult PlanRepayment(PoliticalContext context, PoliticalState state,
        long actor, long obligation, long amount)
    {
        var d = new PoliticalDraft(context, state); var index = d.State.Obligations.FindIndex(x => x.Id == obligation);
        if (index < 0) return PoliticalResult.Rejected("Unknown obligation.");
        var loan = d.State.Obligations[index];
        if (!Authorized(context, actor, loan.Debtor) || !Exists(context, loan.Creditor) ||
            amount <= 0 || amount > loan.Outstanding || d.Money(loan.Debtor) < amount)
            return PoliticalResult.Rejected("Repayment requires current debtor authority and actual unreserved funds within the outstanding principal.");
        d.Transfer(loan.Debtor, loan.Creditor, amount, "debt repayment", obligation);
        d.State.Obligations[index] = loan with { Outstanding = loan.Outstanding - amount };
        return d.Finish("Paid the actual creditor and reduced the one existing obligation.");
    }

    public static PoliticalResult PlanAssignReceivable(PoliticalContext context, PoliticalState state,
        long actor, long obligation, PoliticalAccount newCreditor, long consentingNewCreditor)
    {
        var d = new PoliticalDraft(context, state); var index = d.State.Obligations.FindIndex(x => x.Id == obligation);
        if (index < 0) return PoliticalResult.Rejected("Unknown obligation.");
        var loan = d.State.Obligations[index];
        if (!Authorized(context, actor, loan.Creditor) || !Authorized(context, consentingNewCreditor, newCreditor) ||
            newCreditor == loan.Debtor || !Exists(context, newCreditor))
            return PoliticalResult.Rejected("Receivable assignment requires current owner authority and an actual accepting creditor.");
        d.State.Obligations[index] = loan with { Creditor = newCreditor };
        return d.Finish("Assigned the existing receivable without duplicating principal, debtor ownership or enforcement ownership.");
    }

    public static PoliticalResult PlanEnforcementOwner(PoliticalContext context, PoliticalState state,
        long creditorActor, long obligation, long newEnforcer, long consentingEnforcer)
    {
        var d = new PoliticalDraft(context, state); var index = d.State.Obligations.FindIndex(x => x.Id == obligation);
        if (index < 0) return PoliticalResult.Rejected("Unknown obligation.");
        var loan = d.State.Obligations[index];
        if (!Authorized(context, creditorActor, loan.Creditor) ||
            newEnforcer != 0 && (!Authorized(context, consentingEnforcer, PoliticalAccount.Group(newEnforcer)) ||
                !context.Groups.Any(g => g.Id == newEnforcer && g.Provinces.Contains(loan.Province))))
            return PoliticalResult.Rejected("Enforcement assignment requires creditor consent and current jurisdiction acceptance.");
        d.State.Obligations[index] = loan with { Enforcer = newEnforcer };
        return d.Finish("Changed only enforcement ownership; the creditor and debtor are unchanged.");
    }

    public static PoliticalResult PlanEnforceDebt(PoliticalContext context, PoliticalState state,
        long enforcerActor, long obligation, long amount)
    {
        var d = new PoliticalDraft(context, state); var index = d.State.Obligations.FindIndex(o => o.Id == obligation);
        if (index < 0) return PoliticalResult.Rejected("Unknown obligation.");
        var loan = d.State.Obligations[index];
        if (!Authorized(context, enforcerActor, PoliticalAccount.Group(loan.Enforcer)) || !CanEnforce(context, loan) ||
            !Exists(context, loan.Creditor) || amount <= 0 || amount > loan.Outstanding || d.Money(loan.Debtor) < amount)
            return PoliticalResult.Rejected("Actual debt enforcement requires current jurisdiction over the debtor or its local property, a current enforcer and available principal.");
        d.Transfer(loan.Debtor, loan.Creditor, amount, "enforced repayment", obligation);
        d.State.Obligations[index] = loan with { Outstanding = loan.Outstanding - amount };
        return d.Finish("Enforced a funded payment to the actual creditor without transferring its receivable to the enforcer.");
    }

    public static PoliticalResult PlanNovateDebt(PoliticalContext context, PoliticalState state,
        long creditorActor, long obligation, PoliticalAccount newDebtor, long consentingNewDebtor)
    {
        var d = new PoliticalDraft(context, state); var index = d.State.Obligations.FindIndex(x => x.Id == obligation);
        if (index < 0) return PoliticalResult.Rejected("Unknown obligation.");
        var loan = d.State.Obligations[index];
        if (!Authorized(context, creditorActor, loan.Creditor) || !Authorized(context, consentingNewDebtor, newDebtor) ||
            !Exists(context, newDebtor) || newDebtor == loan.Creditor)
            return PoliticalResult.Rejected("Debt assumption requires the current creditor and a real consenting new debtor.");
        d.State.Obligations[index] = loan with { Debtor = newDebtor };
        return d.Finish("Changed the debtor on one existing obligation; principal, creditor and enforcement owner were preserved.");
    }

    public static PoliticalResult PlanEstateObligations(PoliticalContext context, PoliticalState state,
        long deceased, long heir, long consentingHeir)
    {
        var d = new PoliticalDraft(context, state);
        if (d.Person(deceased) is not { Status: LifeStatus.Dead } || deceased == heir ||
            !Authorized(context, consentingHeir, PoliticalAccount.Person(heir)))
            return PoliticalResult.Rejected("Estate obligation transfer requires an actual deceased person and a real accepting current heir.");
        var prior = PoliticalAccount.Person(deceased); var next = PoliticalAccount.Person(heir);
        for (var i = 0; i < d.State.Obligations.Count; i++)
        {
            var loan = d.State.Obligations[i];
            var creditor = loan.Creditor == prior ? next : loan.Creditor;
            var debtor = loan.Debtor == prior ? next : loan.Debtor;
            d.State.Obligations[i] = loan with { Creditor = creditor, Debtor = debtor,
                Outstanding = creditor == debtor ? 0 : loan.Outstanding };
        }
        return d.Finish("Transferred only actual estate receivables and liabilities to an explicit current heir, preserving obligation IDs and public accounts; self-debt extinguishes.");
    }

    public static PoliticalResult PlanCollectTax(PoliticalContext context, PoliticalState state)
    {
        if (context.Month <= 0 || context.Month % 12 != 0 || state.TaxPeriods.Contains(context.Month))
            return PoliticalResult.Rejected("Tax settles once per completed integer year.");
        var d = new PoliticalDraft(context, state);
        var direct = new SortedDictionary<long, long>();
        foreach (var person in d.People.Where(p => p.Status == LifeStatus.Alive).OrderBy(p => p.Id))
        {
            var place = d.Place(person.Place); var g = d.Group(place?.Sovereign ?? 0);
            if (place is null || g is null || !HasTaxAdministration(context, g, place.Province)) continue;
            var amount = Rate(person.Money, context.Rules.TaxBasisPoints);
            d.Transfer(PoliticalAccount.Person(person.Id), PoliticalAccount.Group(g.Id), amount, "territorial tax", context.Month);
            direct[g.Id] = checked(direct.GetValueOrDefault(g.Id) + amount);
        }
        // Tribute uses only this year's direct tax. Existing treasury and received tribute never become a second tax base.
        foreach (var (id, collected) in direct)
        {
            var payer = d.Group(id)!; var top = d.Group(payer.Overlord);
            if (top is null || !top.Provinces.Any(p => HasTaxAdministration(context, top, p))) continue;
            d.Transfer(PoliticalAccount.Group(id), PoliticalAccount.Group(top.Id), Rate(collected, context.Rules.TributeBasisPoints),
                "vassal tribute", context.Month);
        }
        d.State.TaxPeriods.Add(context.Month);
        return d.Finish("Collected once from actual living residents and remitted one direct-tax share per vassal, without treasury retaxation or territoryless revenue.");
    }

    public static PoliticalResult PlanPayroll(PoliticalContext context, PoliticalState state, long actor, long group)
    {
        var d = new PoliticalDraft(context, state); var g = d.Group(group);
        if (!d.Leads(d.Person(actor), g)) return PoliticalResult.Rejected("Payroll requires current treasury authority.");
        var marker = $"payroll:{group}:{context.Month}";
        if (d.State.Payments.Any(p => p.Kind == marker)) return PoliticalResult.Rejected("This payroll period already settled.");
        var requested = checked(context.Rules.BaseSalary * (g!.Policies.GetValueOrDefault("경제·분배", 1) + 1));
        foreach (var member in g.Members.Order().Select(d.Person).Where(p => p is { Status: LifeStatus.Alive }))
        {
            var pay = Math.Min(g.Money, requested);
            d.Transfer(PoliticalAccount.Group(group), PoliticalAccount.Person(member!.Id), pay, marker, group);
            if (pay < requested)
                d.State.Obligations.Add(new(d.Id(), PoliticalAccount.Person(member.Id), PoliticalAccount.Group(group),
                    g.Jurisdiction, d.Place(member.Place)?.Province ?? -1, requested - pay, marker, context.Month));
            var index = d.State.Service.FindIndex(x => x.Group == group && x.Person == member.Id);
            var old = index < 0 ? new PoliticalService(group, member.Id, 0, 50) : d.State.Service[index];
            var updated = old with { Loyalty = Math.Clamp(old.Loyalty + (pay == requested ? 1 : -5), 0, 100) };
            if (index < 0) d.State.Service.Add(updated); else d.State.Service[index] = updated;
        }
        // A zero-funded payroll still has a period marker, so repeated attempts cannot duplicate arrears.
        if (!d.State.Payments.Any(p => p.Kind == marker))
            d.State.Payments.Add(new(context.Month, PoliticalAccount.Group(group), PoliticalAccount.Group(group), 0, marker, group));
        return d.Finish("Paid salaries from actual treasury and recorded each unpaid amount once as a real employee receivable.");
    }

    internal static bool Authorized(PoliticalContext context, long actor, PoliticalAccount account) =>
        context.People.Any(p => p.Id == actor && p.Status == LifeStatus.Alive) &&
        (account.Kind == PoliticalAccountKind.Person ? actor == account.Id :
            context.Groups.Any(g => g.Id == account.Id && !g.Dissolved && g.Leader == actor));

    private static bool HasTaxAdministration(PoliticalContext context, Group group, int province) =>
        !group.Dissolved && group.Kind == GroupKind.Dynasty && group.Provinces.Contains(province) &&
        context.People.Any(p => p.Id == group.Leader && p.Status == LifeStatus.Alive) &&
        context.Places.Count(p => p.Province == province && p.Sovereign == group.Id) == context.Rules.PlacesPerProvince &&
        group.Members.Any(id => context.People.Any(p => p.Id == id && p.Status == LifeStatus.Alive &&
            context.Places.Any(place => place.Id == p.Place && place.Province == province)));

    private static bool DebtorWithinJurisdiction(PoliticalContext context, PoliticalAccount debtor, int province)
    {
        var person = debtor.Kind == PoliticalAccountKind.Person ? context.People.FirstOrDefault(p => p.Id == debtor.Id) :
            context.People.FirstOrDefault(p => p.Id == context.Groups.FirstOrDefault(g => g.Id == debtor.Id)?.Leader);
        return person is { Status: LifeStatus.Alive } && context.Places.Any(p => p.Id == person.Place && p.Province == province) ||
            context.Places.Any(p => p.Province == province && p.Owner == debtor.Id);
    }

    private static long Rate(long amount, int basisPoints) => checked(amount / 10000 * basisPoints + amount % 10000 * basisPoints / 10000);
}
