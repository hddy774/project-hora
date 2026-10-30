namespace Chronicles.Game.Politics;

public static partial class PoliticsEngine
{
    public static PoliticalResult PlanPolicyChange(PoliticalContext context, PoliticalState state, long actor,
        long group, string category, int choice, int place = -1)
    {
        var adoption = PlanAdoptPolicy(context, state, actor, group, category, choice, place);
        if (adoption.Transition is not { } first) return adoption;
        var projected = context with { People = Merge(context.People, first.People, p => p.Id),
            Groups = Merge(context.Groups, first.Groups, g => g.Id), Places = Merge(context.Places, first.Places, p => p.Id) };
        var preparation = PlanPreparePolicy(projected, first.NextState, actor, first.NextState.Preparations[^1].Id);
        if (preparation.Transition is not { } second) return preparation;
        var combined = new PoliticalTransition(first.Precondition, second.NextState, Merge(first.People, second.People, p => p.Id),
            Merge(first.Groups, second.Groups, g => g.Id), Merge(first.Places, second.Places, p => p.Id),
            checked(first.ConsumedMoney + second.ConsumedMoney), second.Reason);
        return ValidateForApplication(context, state, combined, out var reason)
            ? new(true, combined.Reason, combined) : PoliticalResult.Rejected(reason);
    }

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
        foreach (var worker in prep.Workers) AdjustService(d, g.Id, worker, 1, 1);
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
        for (var i = 0; i < d.State.Charters.Count; i++)
            if (d.State.Charters[i] is { Active: true } prior && prior.Sect == sect && prior.Province == province)
                d.State.Charters[i] = prior with { Active = false };
        d.State.Charters.Add(new(id, sect, sovereign, province, teaching, industry, context.Month, until));
        d.Transfer(PoliticalAccount.Group(sect), PoliticalAccount.Group(sovereign), fee, "sect charter fee", id);
        return d.Finish("Authorized specified sect activities under current local law and transferred the real charter fee.");
    }

    public static PoliticalResult PlanRevokeCharter(PoliticalContext context, PoliticalState state,
        long sovereignActor, long charter)
    {
        var d = new PoliticalDraft(context, state); var index = d.State.Charters.FindIndex(c => c.Id == charter && c.Active);
        if (index < 0) return PoliticalResult.Rejected("Unknown active sect charter.");
        var prior = d.State.Charters[index];
        if (!d.Leads(d.Person(sovereignActor), d.Group(prior.Sovereign)) ||
            !d.Places.Any(p => p.Province == prior.Province && p.Sovereign == prior.Sovereign))
            return PoliticalResult.Rejected("Only the actual current local sovereign can revoke its charter.");
        d.State.Charters[index] = prior with { Active = false };
        return d.Finish("Revoked current sect permission while preserving the historical charter and its actual fee transfer.");
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
            if (r.Purpose is "teaching" or "funded activity" or "defense" &&
                d.Person(r.Person) is { Status: LifeStatus.Alive } p && d.Group(r.Group)?.Members.Contains(p.Id) == true &&
                (p.Action == ActionKind.Idle || p.ActionData == $"politics:{r.Purpose}:{r.Reference}"))
                AdjustService(d, r.Group, r.Person, 1, 1);
            d.ReleaseLabor(r.Reference, r.Purpose);
        }
        return d.Finish("Released completed political work intervals and recorded actual completed service.");
    }

    public static PoliticalResult PlanCancelWork(PoliticalContext context, PoliticalState state, long person)
    {
        var d = new PoliticalDraft(context, state);
        if (d.Person(person) is not { Status: LifeStatus.Alive } || !d.State.Labor.Any(r => r.Person == person && r.From <= context.Month && r.Until > context.Month))
            return PoliticalResult.Rejected("There is no current political work for this person to cancel.");
        foreach (var reservation in d.State.Labor.Where(r => r.Person == person && r.From <= context.Month && r.Until > context.Month).ToArray())
        {
            if (reservation.Purpose == "policy")
            {
                var prep = d.State.Preparations.First(p => p.Id == reservation.Reference);
                d.Cancel(prep, "an actual reserved worker withdrew");
            }
            else
            {
                var participants = d.State.Labor.Where(r => r.Reference == reservation.Reference && r.Purpose == reservation.Purpose).Select(r => r.Person).ToArray();
                for (var i = 0; i < d.State.Effects.Count; i++)
                {
                    var effect = d.State.Effects[i];
                    if (effect.Id != reservation.Reference && !(effect.Kind == PoliticalEffectKind.Defense && effect.Group == reservation.Group &&
                        participants.Contains(effect.Person) && effect.From == reservation.From && effect.Until == reservation.Until)) continue;
                    d.State.Effects[i] = effect with { Until = context.Month };
                    if (!d.State.RoutedEffects.Contains(effect.Id)) d.State.RoutedEffects.Add(effect.Id);
                }
                d.ReleaseLabor(reservation.Reference, reservation.Purpose);
            }
        }
        return d.Finish("Cancelled actual political work, refunded unfinished policy escrow and ended cancelled effects without crediting unperformed service.");
    }

    public static IReadOnlyList<long> DefendersAt(PoliticalContext context, PoliticalState state, int place) =>
        Array.AsReadOnly(state.Effects.Where(e => e.Kind == PoliticalEffectKind.Defense && e.Place == place && e.From <= context.Month && e.Until > context.Month &&
            context.People.Any(p => p.Id == e.Person && p.Status == LifeStatus.Alive && p.Place == place) &&
            context.Groups.Any(g => g.Id == e.Group && !g.Dissolved && g.Members.Contains(e.Person)) &&
            context.Places.Any(p => p.Id == place && (p.Sovereign == e.Group || p.Delegate == e.Group)))
            .Select(e => e.Person).Distinct().Order().ToArray());

    public static bool HasDefense(PoliticalContext context, PoliticalState state, int place) => DefendersAt(context, state, place).Count != 0;

    public static bool CanImplementPolicy(PoliticalContext context, PoliticalState state, long group, int place, string category) =>
        context.Groups.FirstOrDefault(g => g.Id == group) is { } g && PolicyCatalog.IsChoice(category, g.Policies.GetValueOrDefault(category, 1)) &&
        PolicyJurisdiction(context, state, g, place, category);

    public static PoliticalResult PlanVacateDepartedOffices(PoliticalContext context, PoliticalState state)
    {
        var d = new PoliticalDraft(context, state);
        d.State.Offices.RemoveAll(o => d.Person(o.Holder)?.Status != LifeStatus.Alive);
        return d.Finish("Vacated actual departed office holders; no soul history or prior-life office was restored.");
    }

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
            return group.Jurisdiction == place.Sovereign && state.Charters.Any(c => c.Active && c.Sect == group.Id && c.Sovereign == place.Sovereign &&
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
