namespace Chronicles.Game.Politics;

public static partial class PoliticsEngine
{
    public static PoliticalResult PlanAdoptPolicy(PoliticalContext context, PoliticalState state, long actor,
        long group, string category, int choice, int place = -1)
    {
        var d = new PoliticalDraft(context, state); var p = d.Person(actor); var g = d.Group(group);
        if (!d.Leads(p, g) || !PolicyCatalog.IsChoice(category, choice) ||
            d.State.Preparations.Any(x => x.Group == group && x.Category == category && x.Stage is not (PolicyStage.Implemented or PolicyStage.Cancelled)))
            return PoliticalResult.Rejected("Adoption requires the current leader, a defined detailed choice and no duplicate pending category.");
        var target = d.Place(place < 0 ? p!.Place : place);
        if (target is null) return PoliticalResult.Rejected("Policy adoption must name an actual place for later jurisdiction checks.");
        var prep = new PolicyPreparation { Id = d.Id(), Author = actor, Group = group, Category = category,
            Choice = choice, Place = target.Id, Jurisdiction = target.Sovereign, LawRevision = context.LawRevision,
            Adopted = context.Month, Stage = PolicyStage.Adopted };
        d.State.Preparations.Add(prep);
        History(d, prep, "adopted without spending money or applying effects");
        return d.Finish("Adopted a detailed policy; preparation and implementation remain separate transitions.");
    }

    public static PoliticalResult PlanPreparePolicy(PoliticalContext context, PoliticalState state,
        long actor, long preparation)
    {
        var d = new PoliticalDraft(context, state); var prep = d.State.Preparations.FirstOrDefault(x => x.Id == preparation);
        if (prep is null || prep.Stage is PolicyStage.Preparing or PolicyStage.Implemented or PolicyStage.Cancelled ||
            !d.Leads(d.Person(actor), d.Group(prep.Group)) || prep.Author != actor || !PolicyJurisdiction(context, state, d.Group(prep.Group)!, prep.Place, prep.Category))
            return PoliticalResult.Rejected("Preparation requires the adopting current leader and current implementation jurisdiction.");
        var g = d.Group(prep.Group)!;
        if (g.Money < context.Rules.PolicyPreparationCost)
        { prep.Stage = PolicyStage.InsufficientFunds; History(d, prep, "actual treasury is insufficient"); return d.Finish("Policy adopted but preparation lacks actual funds."); }
        var due = checked(context.Month + context.Rules.PreparationMonths);
        var workers = g.Members.Order().Select(d.Person).Where(p => p is not null &&
            d.Place(p.Place)?.Province == d.Place(prep.Place)?.Province &&
            IsLaborAvailable(context, state, p.Id, context.Month, due)).Take(context.Rules.PreparationWorkers).ToArray();
        if (workers.Length < context.Rules.PreparationWorkers)
        { prep.Stage = PolicyStage.InsufficientLabor; History(d, prep, "shared member labor is insufficient"); return d.Finish("Policy adopted but shared labor is unavailable."); }
        prep.Stage = PolicyStage.Preparing; prep.Due = due; prep.Escrow = context.Rules.PolicyPreparationCost;
        prep.RequiredWorkers = workers.Length; prep.Workers = workers.Select(p => p!.Id).ToList();
        prep.Jurisdiction = d.Place(prep.Place)!.Sovereign;
        g.Money -= prep.Escrow;
        d.Reserve(g, workers.Select(p => p!), due, "policy", prep.Id);
        History(d, prep, "actual treasury escrow and worker intervals reserved together");
        return d.Finish("Reserved actual money and shared labor before policy implementation.");
    }

    public static PoliticalResult PlanImplementPolicy(PoliticalContext context, PoliticalState state, long preparation)
    {
        var d = new PoliticalDraft(context, state); var prep = d.State.Preparations.FirstOrDefault(x => x.Id == preparation);
        if (prep is null || prep.Stage != PolicyStage.Preparing || context.Month < prep.Due)
            return PoliticalResult.Rejected("Only a completed preparation can implement a policy.");
        var g = d.Group(prep.Group)!;
        if (!d.Leads(d.Person(prep.Author), g) || d.Place(prep.Place)?.Sovereign != prep.Jurisdiction ||
            !PolicyJurisdiction(context, state, g, prep.Place, prep.Category) || prep.Workers.Any(id =>
                d.Person(id) is not { Status: LifeStatus.Alive } p || !g.Members.Contains(id) ||
                d.Place(p.Place)?.Province != d.Place(prep.Place)?.Province ||
                p.Action != ActionKind.Idle && p.ActionData != $"politics:policy:{prep.Id}"))
        {
            d.Cancel(prep, "current leader, workers or implementation jurisdiction changed");
            return d.Finish("Cancelled stale preparation, refunded its actual escrow and released its labor.");
        }
        var old = g.Policies.GetValueOrDefault(prep.Category, 1);
        g.Policies[prep.Category] = prep.Choice;
        d.State.ConsumedMoney = checked(d.State.ConsumedMoney + prep.Escrow); prep.Escrow = 0;
        prep.Stage = PolicyStage.Implemented; d.ReleaseLabor(prep.Id, "policy");
        d.State.PolicyHistory.Add(new(context.Month, prep.Id, prep.Group, prep.Category, old, prep.Choice,
            prep.Stage, "implemented after current authority and jurisdiction revalidation", prep.LawRevision));
        return d.Finish("Implemented a detailed choice and consumed only its reserved preparation cost; no resources or permanent specialization were created.");
    }

    public static PoliticalResult PlanCancelPolicy(PoliticalContext context, PoliticalState state,
        long actor, long preparation)
    {
        var d = new PoliticalDraft(context, state); var prep = d.State.Preparations.FirstOrDefault(x => x.Id == preparation);
        if (prep is null || prep.Stage is PolicyStage.Implemented or PolicyStage.Cancelled || !d.Leads(d.Person(actor), d.Group(prep.Group)))
            return PoliticalResult.Rejected("Only the current leader can cancel an unfinished policy.");
        d.Cancel(prep, "cancelled by the current leader");
        return d.Finish("Cancelled policy preparation and returned actual reserved resources.");
    }

    public static PoliticalResult PlanCharter(PoliticalContext context, PoliticalState state,
        long sovereignActor, long sect, long sectConsent, int province, bool teaching, bool industry, long until, long fee)
    {
        var d = new PoliticalDraft(context, state); var g = d.Group(sect); var territory = d.Places.Where(p => p.Province == province).ToArray();
        var sovereign = territory.FirstOrDefault()?.Sovereign ?? 0;
        if (g is null || g.Kind != GroupKind.Sect || !d.Leads(d.Person(sectConsent), g) ||
            !d.Leads(d.Person(sovereignActor), d.Group(sovereign)) || territory.Any(p => p.Sovereign != sovereign) ||
            until <= context.Month || fee < 0 || g.Money < fee ||
            !g.Members.Any(id => d.Person(id) is { Status: LifeStatus.Alive } p && d.Place(p.Place)?.Province == province))
            return PoliticalResult.Rejected("A sect charter requires real local members, both current leaders, current territorial law and a funded explicit fee.");
        var id = d.Id(); g.Jurisdiction = sovereign;
        d.State.Charters.Add(new(id, sect, sovereign, province, teaching, industry, context.Month, until));
        d.Transfer(PoliticalAccount.Group(sect), PoliticalAccount.Group(sovereign), fee, "sect charter fee", id);
        return d.Finish("Authorized specified sect activities under current local law and transferred the real charter fee.");
    }

    public static PoliticalResult PlanOffice(PoliticalContext context, PoliticalState state,
        long actor, long group, string office, long holder)
    {
        var d = new PoliticalDraft(context, state); var g = d.Group(group); var p = d.Person(holder);
        if (!d.Leads(d.Person(actor), g) || p is not { Status: LifeStatus.Alive } || !g!.Members.Contains(holder) ||
            context.Month - p.Born < context.Rules.MaturityMonths || string.IsNullOrWhiteSpace(office) || office == "leader")
            return PoliticalResult.Rejected("An office requires current leader authority and an actual adult member.");
        d.State.Offices.RemoveAll(x => x.Group == group && x.Name == office);
        d.State.Offices.Add(new(group, office, holder, context.Month));
        return d.Finish("Assigned an actual organizational office without granting territorial or past-life authority.");
    }

    public static PoliticalResult PlanPolicyActivity(PoliticalContext context, PoliticalState state, PolicyActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var d = new PoliticalDraft(context, state); var g = d.Group(activity.Group); var actor = d.Person(activity.Actor);
        if (!d.Leads(actor, g) || !Catalog.PolicyNames.Contains(activity.Category))
            return PoliticalResult.Rejected("A policy activity requires current leader authority and a defined category.");
        var subject = d.Person(activity.Subject); var other = d.Person(activity.Counterparty);
        var place = d.Place(activity.Place < 0 ? subject?.Place ?? actor!.Place : activity.Place);
        if (place is null || !PolicyJurisdiction(context, state, g!, place.Id, activity.Category))
            return PoliticalResult.Rejected("Current policy activity jurisdiction is insufficient.");
        var choice = g!.Policies.GetValueOrDefault(activity.Category, 1);
        if (!PolicyCatalog.IsChoice(activity.Category, choice)) return PoliticalResult.Rejected("The active policy choice is invalid.");
        var amount = checked(context.Rules.ActivityGrant * (choice + 1));
        var member = subject is { Status: LifeStatus.Alive } && g.Members.Contains(subject.Id);
        switch (activity.Category)
        {
            case "입문·선발":
                if (subject is not { Status: LifeStatus.Alive } || member || subject.Place != actor!.Place ||
                    !actor.KnownPeople.Contains(subject.Id) || context.Month - subject.Born < context.Rules.MaturityMonths ||
                    choice == 1 && !subject.Roots.Any(x => x > 0) || choice == 2 && subject.Realm == Realm.Mortal ||
                    g.Kind == GroupKind.Sect && subject.Sect != 0)
                    return PoliticalResult.Rejected("The actual candidate fails the active admission criteria or mutual contact requirements.");
                if (g.Kind == GroupKind.Sect) subject.Sect = g.Id;
                else { d.LeaveHousehold(subject); subject.Household = g.Id; }
                g.Members.Add(subject.Id); EnsureService(d, g.Id, subject.Id);
                break;
            case "교육·전승":
                if (!member || other is not { Status: LifeStatus.Alive } || other.Id == subject!.Id ||
                    !g.Members.Contains(other.Id) || other.Place != subject.Place ||
                    string.IsNullOrWhiteSpace(activity.Knowledge) || other.Understanding.GetValueOrDefault(activity.Knowledge) <= 0)
                    return PoliticalResult.Rejected("Teaching requires an actual knowledgeable member and a distinct colocated pupil.");
                if (!ReserveActivity(d, g, [other, subject], "teaching", out var teachingId))
                    return PoliticalResult.Rejected("Teaching lacks shared teacher or pupil labor.");
                if (g.Money < amount) return PoliticalResult.Rejected("Teaching lacks an actual funded budget.");
                d.Transfer(PoliticalAccount.Group(g.Id), PoliticalAccount.Person(other.Id), amount, "teaching budget", teachingId);
                d.State.Effects.Add(new(teachingId, PoliticalEffectKind.Teaching, g.Id, subject.Id, other.Id, place.Id,
                    amount, activity.Knowledge, context.Month, context.Month + 1));
                break;
            case "수행·자원":
            case "기예·산업":
                if (!member || activity.Category == "수행·자원" && !subject!.Roots.Any(x => x > 0) ||
                    activity.Category == "기예·산업" && (string.IsNullOrWhiteSpace(activity.Knowledge) ||
                        subject!.Skills.GetValueOrDefault(activity.Knowledge) < choice))
                    return PoliticalResult.Rejected("The current member lacks the actual cultivation or industry input required by this choice.");
                if (!ReserveActivity(d, g, [subject!], "funded activity", out var grantId) || g.Money < amount)
                    return PoliticalResult.Rejected("The activity lacks shared labor or unreserved budget.");
                d.Transfer(PoliticalAccount.Group(g.Id), PoliticalAccount.Person(subject!.Id), amount, activity.Category, grantId);
                d.State.Effects.Add(new(grantId, activity.Category == "수행·자원" ? PoliticalEffectKind.CultivationGrant : PoliticalEffectKind.IndustryGrant,
                    g.Id, subject.Id, 0, place.Id, amount, activity.Knowledge, context.Month, context.Month + 1));
                break;
            case "경제·분배": return PlanPayroll(context, state, activity.Actor, activity.Group);
            case "규율·상벌":
                if (!member || subject!.Place != place.Id) return PoliticalResult.Rejected("Internal discipline requires an actual current member in the current jurisdiction.");
                var fine = Math.Min(subject.Money, amount); var fineId = d.Id();
                d.Transfer(PoliticalAccount.Person(subject.Id), PoliticalAccount.Group(g.Id), fine, "disciplinary fine", fineId);
                d.State.Effects.Add(new(fineId, PoliticalEffectKind.Fine, g.Id, subject.Id, 0, place.Id, fine, "internal discipline", context.Month, context.Month));
                AdjustService(d, g.Id, subject.Id, 0, -choice - 1);
                break;
            case "대외·외교":
                var counterpart = d.Group(activity.Subject);
                if (counterpart is null || counterpart.Id == g.Id || !d.Leads(other, counterpart))
                    return PoliticalResult.Rejected("Diplomacy requires an actual distinct organization and its current consenting leader.");
                d.State.Accords.RemoveAll(x => x.FirstGroup == g.Id && x.SecondGroup == counterpart.Id || x.SecondGroup == g.Id && x.FirstGroup == counterpart.Id);
                d.State.Accords.Add(new(d.Id(), Math.Min(g.Id, counterpart.Id), Math.Max(g.Id, counterpart.Id), choice, context.Month));
                break;
            case "영토·방위":
                var defenders = g.Members.Order().Select(d.Person).Where(p => p is { Status: LifeStatus.Alive } && p.Place == place.Id &&
                    IsLaborAvailable(context, state, p.Id, context.Month, context.Month + 1)).Take(choice + 1).Select(p => p!).ToArray();
                if (defenders.Length != choice + 1 || g.Money < amount) return PoliticalResult.Rejected("The defense choice lacks actual local defenders or treasury.");
                if (!ReserveActivity(d, g, defenders, "defense", out var defenseId)) return PoliticalResult.Rejected("Defense labor is already reserved.");
                foreach (var defender in defenders)
                {
                    var pay = amount / defenders.Length + (defender.Id == defenders[0].Id ? amount % defenders.Length : 0);
                    d.Transfer(PoliticalAccount.Group(g.Id), PoliticalAccount.Person(defender.Id), pay, "defense pay", defenseId);
                    d.State.Effects.Add(new(d.Id(), PoliticalEffectKind.Defense, g.Id, defender.Id, 0, place.Id, pay,
                        "local guard", context.Month, context.Month + 1));
                }
                break;
            case "민생·구휼":
                if (subject is not { Status: LifeStatus.Alive } || subject.Place != place.Id ||
                    subject.Money > checked(context.Rules.ReliefThreshold * (choice + 1)) || g.Money < amount)
                    return PoliticalResult.Rejected("Relief requires an actual eligible poor resident and an unreserved treasury grant.");
                var reliefId = d.Id();
                d.Transfer(PoliticalAccount.Group(g.Id), PoliticalAccount.Person(subject.Id), amount, "relief", reliefId);
                d.State.Effects.Add(new(reliefId, PoliticalEffectKind.Relief, g.Id, subject.Id, 0, place.Id, amount,
                    "actual resident relief", context.Month, context.Month));
                break;
            case "승계·인사":
                if (!member || context.Month - subject!.Born < context.Rules.MaturityMonths)
                    return PoliticalResult.Rejected("Succession policy requires a living adult current member.");
                var eligible = g.Members.Select(d.Person).Where(p => p is { Status: LifeStatus.Alive } && context.Month - p.Born >= context.Rules.MaturityMonths).Select(p => p!);
                var first = choice == 0 ? eligible.OrderBy(p => p.Born).ThenBy(p => p.Id).First() :
                    eligible.OrderByDescending(p => d.State.Service.FirstOrDefault(s => s.Group == g.Id && s.Person == p.Id)?.Contribution ?? 0).ThenBy(p => p.Id).First();
                if (choice != 2 && first.Id != subject.Id) return PoliticalResult.Rejected("The proposed current person does not meet the active succession priority.");
                g.Leader = subject.Id; d.State.Offices.RemoveAll(x => x.Group == g.Id && x.Name == "leader");
                d.State.Offices.Add(new(g.Id, "leader", subject.Id, context.Month));
                break;
        }
        return d.Finish("Applied the active detailed policy to actual inputs, current jurisdiction and shared resources.");
    }

    public static PoliticalResult PlanReleaseCompletedLabor(PoliticalContext context, PoliticalState state)
    {
        var d = new PoliticalDraft(context, state);
        foreach (var r in d.State.Labor.Where(x => x.Until <= context.Month && x.Purpose != "policy").ToArray())
        {
            if (r.Purpose is "teaching" or "funded activity" or "defense") AdjustService(d, r.Group, r.Person, 1, 1);
            d.ReleaseLabor(r.Reference, r.Purpose);
        }
        return d.Finish("Released completed political work intervals and recorded actual completed service.");
    }

    public static bool HasDefense(PoliticalContext context, PoliticalState state, int place) =>
        state.Effects.Any(e => e.Kind == PoliticalEffectKind.Defense && e.Place == place && e.From <= context.Month && e.Until > context.Month &&
            context.People.Any(p => p.Id == e.Person && p.Status == LifeStatus.Alive && p.Place == place) &&
            context.Places.Any(p => p.Id == place && (p.Sovereign == e.Group || p.Delegate == e.Group)));

    private static bool PolicyJurisdiction(PoliticalContext context, PoliticalState state, Group group, int placeId, string category)
    {
        var place = context.Places.FirstOrDefault(p => p.Id == placeId);
        if (place is null || group.Dissolved) return false;
        var localMember = group.Members.Any(id => context.People.Any(p => p.Id == id && p.Status == LifeStatus.Alive &&
            context.Places.Any(at => at.Id == p.Place && at.Province == place.Province)));
        if (!localMember) return false;
        if (category == "영토·방위" || category == "민생·구휼" && group.Kind == GroupKind.Dynasty)
            return place.Sovereign == group.Id || place.Delegate == group.Id;
        if (group.Kind == GroupKind.Sect && place.Sovereign != 0 && category is "교육·전승" or "기예·산업")
            return group.Jurisdiction == place.Sovereign && state.Charters.Any(c => c.Sect == group.Id && c.Sovereign == place.Sovereign &&
                c.Province == place.Province && c.Until > context.Month && (category == "교육·전승" ? c.Teaching : c.Industry));
        return group.Kind != GroupKind.Sect || group.Jurisdiction == place.Sovereign;
    }

    private static void History(PoliticalDraft d, PolicyPreparation prep, string reason) =>
        d.State.PolicyHistory.Add(new(d.Context.Month, prep.Id, prep.Group, prep.Category,
            d.Group(prep.Group)!.Policies.GetValueOrDefault(prep.Category, 1), prep.Choice, prep.Stage, reason, prep.LawRevision));

    private static bool ReserveActivity(PoliticalDraft d, Group g, Person[] workers, string purpose, out long id)
    {
        id = d.Id();
        if (workers.DistinctBy(p => p.Id).Count() != workers.Length || workers.Any(p =>
            !IsLaborAvailable(d.Context, d.State, p.Id, d.Context.Month, d.Context.Month + 1))) return false;
        d.Reserve(g, workers, d.Context.Month + 1, purpose, id); return true;
    }

    private static void EnsureService(PoliticalDraft d, long group, long person)
    {
        if (!d.State.Service.Any(x => x.Group == group && x.Person == person)) d.State.Service.Add(new(group, person, 0, 50));
    }

    private static void AdjustService(PoliticalDraft d, long group, long person, long contribution, int loyalty)
    {
        EnsureService(d, group, person);
        var index = d.State.Service.FindIndex(x => x.Group == group && x.Person == person); var old = d.State.Service[index];
        d.State.Service[index] = old with { Contribution = checked(old.Contribution + contribution), Loyalty = Math.Clamp(old.Loyalty + loyalty, 0, 100) };
    }
}
