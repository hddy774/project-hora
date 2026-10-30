using System.Security.Cryptography;
using System.Text.Json;

namespace Chronicles.Game.Politics;

public static partial class PoliticsEngine
{
    public static string Fingerprint(PoliticalContext context, PoliticalState state) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            context.Month, context.Revision, context.LawRevision, context.Rules,
            People = context.People.OrderBy(x => x.Id), Groups = context.Groups.OrderBy(x => x.Id),
            Places = context.Places.OrderBy(x => x.Id), State = state
        })));

    public static bool ValidateForApplication(PoliticalContext context, PoliticalState state,
        PoliticalTransition transition, out string reason)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (!string.Equals(Fingerprint(context, state), transition.Precondition, StringComparison.Ordinal))
        { reason = "Political inputs changed; plan again before application."; return false; }
        var projected = context with
        {
            People = Merge(context.People, transition.People, x => x.Id),
            Groups = Merge(context.Groups, transition.Groups, x => x.Id),
            Places = Merge(context.Places, transition.Places, x => x.Id)
        };
        var errors = PoliticalInvariants.Validate(projected, transition.NextState);
        if (errors.Count != 0) { reason = errors[0]; return false; }
        if (transition.ConsumedMoney < 0 || transition.NextState.ConsumedMoney - state.ConsumedMoney != transition.ConsumedMoney ||
            Balance(context, state) != Balance(projected, transition.NextState) + transition.ConsumedMoney)
        { reason = "Political transition does not conserve actual money."; return false; }
        reason = ""; return true;
    }

    public static bool IsLaborAvailable(PoliticalContext context, PoliticalState state, long person,
        long from, long until) => from >= context.Month && until > from &&
        context.People.Any(p => p.Id == person && p.Status == LifeStatus.Alive && p.Action == ActionKind.Idle) &&
        !state.Labor.Any(r => r.Person == person && r.From < until && from < r.Until);

    public static long TopOverlord(PoliticalContext context, long group)
    {
        var seen = new HashSet<long>();
        while (group != 0)
        {
            if (!seen.Add(group)) return 0;
            var current = context.Groups.FirstOrDefault(g => g.Id == group && !g.Dissolved);
            if (current is null) return 0;
            if (current.Overlord == 0) return current.Id;
            group = current.Overlord;
        }
        return 0;
    }

    public static PoliticalPlaceRights? DescribeRights(PoliticalContext context, PoliticalState state, int placeId)
    {
        var place = context.Places.FirstOrDefault(p => p.Id == placeId);
        return place is null ? null : new(place.Id, place.Sovereign, TopOverlord(context, place.Sovereign),
            place.Delegate, place.Owner, place.Operator,
            Array.AsReadOnly(state.Claims.Where(c => c.Active && c.Province == place.Province).OrderBy(c => c.Id).ToArray()));
    }

    public static bool HasRight(PoliticalContext context, PoliticalState state, PoliticalAccount holder,
        int placeId, PoliticalRight right)
    {
        var place = context.Places.FirstOrDefault(p => p.Id == placeId);
        if (place is null || !Exists(context, holder)) return false;
        return right switch
        {
            PoliticalRight.Ownership => place.Owner == holder.Id,
            PoliticalRight.Operation => place.Owner == holder.Id || place.Operator == holder.Id,
            PoliticalRight.Administration => holder.Kind == PoliticalAccountKind.Group &&
                (place.Sovereign == holder.Id || place.Delegate == holder.Id),
            PoliticalRight.Sovereignty => holder.Kind == PoliticalAccountKind.Group && place.Sovereign == holder.Id,
            PoliticalRight.Passage => place.BlockedUntil <= context.Month &&
                (place.Sovereign == 0 || (holder.Kind == PoliticalAccountKind.Group && place.Sovereign == holder.Id) ||
                 state.Passage.Any(p => p.Holder == holder && p.Place == placeId && p.Grantor == place.Sovereign && p.Until > context.Month)),
            _ => false
        };
    }

    public static PoliticalResult PlanFound(PoliticalContext context, PoliticalState state, long actor,
        long groupId, string name, GroupKind kind)
    {
        var d = new PoliticalDraft(context, state);
        var p = d.Person(actor);
        if (p is null || p.Status != LifeStatus.Alive || p.Action != ActionKind.Idle ||
            context.Month - p.Born < context.Rules.MaturityMonths || p.Money < context.Rules.FoundingTreasury ||
            groupId <= 0 || d.Groups.Any(g => g.Id == groupId) || d.People.Any(x => x.Id == groupId) ||
            string.IsNullOrWhiteSpace(name) || kind is not (GroupKind.Family or GroupKind.Sect) ||
            !IsLaborAvailable(context, state, actor, context.Month, context.Month + 1))
            return PoliticalResult.Rejected("Founding requires a living adult, actual treasury, a unique group ID and available labor.");
        if (kind == GroupKind.Sect && (p.Sect != 0 || d.Groups.Count(g => g.Kind == GroupKind.Sect && !g.Dissolved) >= context.Rules.SectLimit))
            return PoliticalResult.Rejected("Concurrent sect limit or existing membership prevents founding.");
        var g = new Group { Id = groupId, Name = name.Trim(), Kind = kind, Leader = actor,
            Founded = context.Month, Money = context.Rules.FoundingTreasury, Members = [actor],
            Policies = new(Catalog.PolicyNames.ToDictionary(x => x, _ => 1, StringComparer.Ordinal), StringComparer.Ordinal) };
        p.Money -= g.Money;
        if (kind == GroupKind.Sect) { p.Sect = groupId; g.Jurisdiction = d.Place(p.Place)?.Sovereign ?? 0; }
        else
        {
            d.LeaveHousehold(p);
            p.Household = groupId;
        }
        d.Groups.Add(g);
        d.State.Service.Add(new(groupId, actor, 0, 50));
        d.Reserve(g, [p], context.Month + 1, "founding", groupId);
        return d.Finish("Founded an actual organization by transferring the founder's money; no realm qualification was inferred from fertility.");
    }

    public static PoliticalResult PlanSovereignty(PoliticalContext context, PoliticalState state, long actor,
        long group, int province)
    {
        var d = new PoliticalDraft(context, state);
        var g = d.Group(group); var p = d.Person(actor);
        var places = d.Places.Where(x => x.Province == province).ToArray();
        if (!d.Leads(p, g) || g!.Kind == GroupKind.Sect || g.Overlord != 0 || province < 0 ||
            places.Length != context.Rules.PlacesPerProvince || places.Any(x => x.Sovereign != 0 || !p!.KnownPlaces.Contains(x.Id)) ||
            !g.Members.Any(id => d.Person(id) is { Status: LifeStatus.Alive } resident &&
                resident.Household == group && places.Any(x => x.Id == resident.Place)) ||
            !IsLaborAvailable(context, state, actor, context.Month, context.Month + 1))
            return PoliticalResult.Rejected("Independent sovereignty requires a completely observed province, a resident household and real administration labor.");
        foreach (var place in places) { place.Sovereign = group; place.Delegate = 0; }
        g.Kind = GroupKind.Dynasty; g.Provinces.Add(province); g.Provinces.Sort();
        d.Claim(group, province, "actual independent sovereignty");
        d.Reserve(g, [p!], context.Month + 1, "administration", d.Id());
        return d.Finish("One complete province establishes a dynasty; ownership and delegated administration are unchanged rights.");
    }

    public static PoliticalResult PlanClaim(PoliticalContext context, PoliticalState state, long actor,
        long group, int province, string basis)
    {
        var d = new PoliticalDraft(context, state);
        if (!d.Leads(d.Person(actor), d.Group(group)) || !d.Places.Any(p => p.Province == province) || string.IsNullOrWhiteSpace(basis))
            return PoliticalResult.Rejected("A claim requires a current leader, an existing province and an explicit basis.");
        d.Claim(group, province, basis);
        return d.Finish("Recorded a territorial claim without creating sovereignty, property or tax authority.");
    }

    public static PoliticalResult PlanVassalage(PoliticalContext context, PoliticalState state, long actor,
        long group, long overlord, long consentingOverlordLeader = 0)
    {
        var d = new PoliticalDraft(context, state);
        var g = d.Group(group); var top = d.Group(overlord);
        if (!d.Leads(d.Person(actor), g) || g!.Kind == GroupKind.Sect || group == overlord ||
            (overlord != 0 && (top is null || top.Kind != GroupKind.Dynasty || !d.Leads(d.Person(consentingOverlordLeader), top))))
            return PoliticalResult.Rejected("Vassalage requires current authority and acceptance by the overlord's living leader.");
        var cursor = overlord; var seen = new HashSet<long> { group };
        while (cursor != 0)
        {
            if (!seen.Add(cursor)) return PoliticalResult.Rejected("A vassal cycle is forbidden.");
            var ancestor = d.Group(cursor);
            if (ancestor is null || ancestor.Dissolved) return PoliticalResult.Rejected("The proposed overlord chain is invalid.");
            cursor = ancestor.Overlord;
        }
        g.Overlord = overlord;
        return d.Finish("Changed political allegiance; creditor, debtor, property and sovereign identities remain explicit.");
    }

    public static PoliticalResult PlanDelegate(PoliticalContext context, PoliticalState state, long actor,
        int placeId, long administrator)
    {
        var d = new PoliticalDraft(context, state);
        var place = d.Place(placeId); var recipient = d.Group(administrator);
        if (place is null || !d.Leads(d.Person(actor), d.Group(place.Sovereign)) ||
            (administrator != 0 && (recipient is null || recipient.Dissolved || recipient.Kind == GroupKind.Sect ||
                !recipient.Members.Any(id => d.Person(id) is { Status: LifeStatus.Alive } p &&
                    d.Place(p.Place)?.Province == place.Province))))
            return PoliticalResult.Rejected("Only the current sovereign can delegate to a real resident administration.");
        place.Delegate = administrator;
        return d.Finish("Delegated administration without transferring sovereignty, ownership or tax entitlement.");
    }

    public static PoliticalResult PlanProperty(PoliticalContext context, PoliticalState state, long actor,
        int placeId, PoliticalRight right, PoliticalAccount recipient)
    {
        var d = new PoliticalDraft(context, state); var place = d.Place(placeId);
        if (place is null || !Exists(context, recipient) ||
            !(place.Owner == actor && d.Person(actor)?.Status == LifeStatus.Alive ||
              d.Leads(d.Person(actor), d.Group(place.Owner))) ||
            right is not (PoliticalRight.Ownership or PoliticalRight.Operation))
            return PoliticalResult.Rejected("Only a living owner or its current leader can transfer property or appoint its operator.");
        if (right == PoliticalRight.Ownership) { place.Owner = recipient.Id; place.Operator = 0; }
        else place.Operator = recipient.Id;
        return d.Finish("Transferred the specified property right without changing political authority.");
    }

    public static PoliticalResult PlanPassage(PoliticalContext context, PoliticalState state, long actor,
        int placeId, PoliticalAccount holder, long until)
    {
        var d = new PoliticalDraft(context, state); var place = d.Place(placeId);
        if (place is null || !d.Leads(d.Person(actor), d.Group(place.Sovereign)) || !Exists(context, holder) || until <= context.Month)
            return PoliticalResult.Rejected("Passage requires current sovereign permission, a real holder and a future expiry.");
        d.State.Passage.Add(new(d.Id(), place.Sovereign, holder, placeId, until));
        return d.Finish("Granted time bounded passage under the current sovereign.");
    }

    public static PoliticalResult PlanReleaseProvince(PoliticalContext context, PoliticalState state,
        long actor, long group, int province)
    {
        var d = new PoliticalDraft(context, state); var g = d.Group(group);
        if (!d.Leads(d.Person(actor), g) || !g!.Provinces.Contains(province))
            return PoliticalResult.Rejected("Only the current sovereign leader can release a controlled province.");
        d.ReleaseProvince(g, province);
        return d.Finish("Released territorial authority; the dynasty, its claims, actual assets and obligations persist with zero territorial tax.");
    }

    public static PoliticalResult PlanTransferSovereignty(PoliticalContext context, PoliticalState state,
        long sovereignActor, long fromGroup, long toGroup, long acceptingLeader, int province)
    {
        var d = new PoliticalDraft(context, state); var from = d.Group(fromGroup); var to = d.Group(toGroup);
        var recipient = d.Person(acceptingLeader); var places = d.Places.Where(p => p.Province == province).ToArray();
        if (fromGroup == toGroup || !d.Leads(d.Person(sovereignActor), from) || !from!.Provinces.Contains(province) ||
            !d.Leads(recipient, to) || to!.Kind == GroupKind.Sect || to.Kind == GroupKind.Family && to.Overlord != 0 ||
            places.Length != context.Rules.PlacesPerProvince || places.Any(p => p.Sovereign != fromGroup || !recipient!.KnownPlaces.Contains(p.Id)) ||
            !to.Members.Any(id => d.Person(id) is { Status: LifeStatus.Alive } p && p.Household == toGroup && places.Any(at => at.Id == p.Place)) ||
            !IsLaborAvailable(context, state, acceptingLeader, context.Month, context.Month + 1))
            return PoliticalResult.Rejected("Territorial transfer requires the current sovereign, the accepting current leader, a fully observed resident household and available administration.");
        d.ReleaseProvince(from, province);
        foreach (var place in places) { place.Sovereign = toGroup; place.Delegate = 0; }
        to.Kind = GroupKind.Dynasty; to.Provinces.Add(province); to.Provinces.Sort();
        d.Claim(toGroup, province, "accepted transfer of actual sovereignty");
        d.Reserve(to, [recipient!], context.Month + 1, "administration", d.Id());
        return d.Finish("Transferred a complete province atomically while preserving both dynasties' claims, personal property and all obligation owners.");
    }

    public static PoliticalResult PlanSuccession(PoliticalContext context, PoliticalState state,
        long group, long successor)
    {
        var d = new PoliticalDraft(context, state); var g = d.Group(group); var p = d.Person(successor);
        if (g is null || g.Dissolved || (g.Leader != 0 && d.Person(g.Leader)?.Status == LifeStatus.Alive) ||
            (successor != 0 && (p is null || p.Status != LifeStatus.Alive || !g.Members.Contains(successor) ||
                context.Month - p.Born < context.Rules.MaturityMonths)))
            return PoliticalResult.Rejected("Succession requires a vacancy and an eligible current person; soul history never grants membership or office.");
        g.Leader = successor;
        d.State.Offices.RemoveAll(x => x.Group == group && (x.Name == "leader" || d.Person(x.Holder)?.Status != LifeStatus.Alive));
        if (successor != 0) d.State.Offices.Add(new(group, "leader", successor, context.Month));
        if (!g.Members.Any(id => d.Person(id)?.Status == LifeStatus.Alive))
            foreach (var province in g.Provinces.ToArray()) d.ReleaseProvince(g, province);
        return d.Finish("Filled or preserved an actual vacancy; public and personal assets and all obligation owners were preserved.");
    }

    public static PoliticalResult PlanDissolve(PoliticalContext context, PoliticalState state, long actor, long group)
    {
        var d = new PoliticalDraft(context, state); var g = d.Group(group); var p = d.Person(actor);
        var account = PoliticalAccount.Group(group);
        if (!d.Leads(p, g) || g!.Debt != 0 || d.State.Obligations.Any(o => o.Outstanding > 0 && (o.Creditor == account || o.Debtor == account)))
            return PoliticalResult.Rejected("Dissolution requires current authority and an explicit resolution of every owned receivable and debt.");
        foreach (var prep in d.State.Preparations.Where(x => x.Group == group && x.Stage is not (PolicyStage.Cancelled or PolicyStage.Implemented)))
            d.Cancel(prep, "organization dissolved");
        foreach (var province in g.Provinces.ToArray()) d.ReleaseProvince(g, province);
        foreach (var member in d.People)
        {
            if (member.Sect == group) member.Sect = 0;
            if (member.Household == group) member.Household = 0;
        }
        foreach (var v in d.Groups.Where(x => x.Overlord == group)) v.Overlord = 0;
        foreach (var place in d.Places)
        {
            if (place.Delegate == group) place.Delegate = 0;
            if (place.Operator == group) place.Operator = 0;
            if (place.Owner == group) { place.Owner = actor; place.Operator = 0; }
        }
        d.Transfer(account, PoliticalAccount.Person(actor), g.Money, "dissolution", group);
        g.Leader = 0; g.Dissolved = true;
        d.State.Offices.RemoveAll(x => x.Group == group);
        foreach (var reservation in d.State.Labor.Where(x => x.Group == group).ToArray())
            d.ReleaseLabor(reservation.Reference, reservation.Purpose);
        return d.Finish("Dissolved the real organization, refunded preparation escrow and transferred its actual remaining money.");
    }

    internal static bool Exists(PoliticalContext context, PoliticalAccount account) => Enum.IsDefined(account.Kind) && account.Id > 0 &&
        (account.Kind == PoliticalAccountKind.Person ? context.People.Any(p => p.Id == account.Id) :
            context.Groups.Any(g => g.Id == account.Id && !g.Dissolved));

    internal static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(value))!;
    internal static List<T> Merge<T>(IReadOnlyList<T> current, IReadOnlyList<T> changes, Func<T, long> key) =>
        current.Where(x => !changes.Any(y => key(y) == key(x))).Concat(changes).OrderBy(key).ToList();
    internal static long Balance(PoliticalContext context, PoliticalState state) => checked(
        context.People.Sum(p => p.Money) + context.Groups.Sum(g => checked(g.Money + g.PolicyEscrow)) + state.Preparations.Sum(p => p.Escrow));
}

internal sealed class PoliticalDraft
{
    private readonly PoliticalContext original;
    private readonly PoliticalState before;
    public PoliticalContext Context { get; }
    public PoliticalState State { get; }
    public List<Person> People { get; }
    public List<Group> Groups { get; }
    public List<Place> Places { get; }

    public PoliticalDraft(PoliticalContext context, PoliticalState state)
    {
        original = context; before = state; State = PoliticsEngine.Clone(state);
        People = PoliticsEngine.Clone(context.People.ToList()); Groups = PoliticsEngine.Clone(context.Groups.ToList());
        Places = PoliticsEngine.Clone(context.Places.ToList());
        foreach (var person in People)
        {
            person.Skills = new(person.Skills, StringComparer.Ordinal);
            person.Understanding = new(person.Understanding, StringComparer.Ordinal);
            person.Beliefs = new(person.Beliefs, StringComparer.Ordinal);
        }
        foreach (var group in Groups)
        {
            group.Policies = new(group.Policies, StringComparer.Ordinal);
            group.PendingPolicies = new(group.PendingPolicies, StringComparer.Ordinal);
        }
        Context = context with { People = People, Groups = Groups, Places = Places };
    }
    public long Id() => State.NextId++;
    public Person? Person(long id) => People.FirstOrDefault(p => p.Id == id);
    public Group? Group(long id) => Groups.FirstOrDefault(g => g.Id == id);
    public Place? Place(int id) => Places.FirstOrDefault(p => p.Id == id);
    public bool Leads(Person? p, Group? g) => p is { Status: LifeStatus.Alive } && g is { Dissolved: false } &&
        Group(g.Id) == g && g.Leader == p.Id && g.Members.Contains(p.Id);
    public void LeaveHousehold(Person person)
    {
        if (Group(person.Household) is not { } old) return;
        old.Members.Remove(person.Id);
        if (old.Leader == person.Id) old.Leader = 0;
        State.Offices.RemoveAll(o => o.Group == old.Id && o.Holder == person.Id);
    }
    public void Claim(long group, int province, string basis)
    {
        if (!State.Claims.Any(x => x.Group == group && x.Province == province && x.Active))
            State.Claims.Add(new(Id(), group, province, Context.Month, basis));
    }
    public void ReleaseProvince(Group group, int province)
    {
        Claim(group.Id, province, "former sovereignty retained after territory loss");
        group.Provinces.Remove(province);
        foreach (var place in Places.Where(p => p.Province == province && p.Sovereign == group.Id))
        { place.Sovereign = 0; place.Delegate = 0; }
        foreach (var sect in Groups.Where(g => g.Kind == GroupKind.Sect && g.Jurisdiction == group.Id))
            if (!sect.Members.Any(id => Person(id) is { Status: LifeStatus.Alive } p && Place(p.Place)?.Sovereign == group.Id))
                sect.Jurisdiction = 0;
    }
    public long Money(PoliticalAccount account) => account.Kind == PoliticalAccountKind.Person ? Person(account.Id)!.Money : Group(account.Id)!.Money;
    public void Transfer(PoliticalAccount from, PoliticalAccount to, long amount, string kind, long reference)
    {
        if (amount < 0 || Money(from) < amount) throw new InvalidOperationException("Transfer exceeds actual available money.");
        if (amount == 0 || from == to) return;
        SetMoney(from, Money(from) - amount); SetMoney(to, checked(Money(to) + amount));
        State.Payments.Add(new(Context.Month, from, to, amount, kind, reference));
    }
    private void SetMoney(PoliticalAccount account, long value)
    { if (account.Kind == PoliticalAccountKind.Person) Person(account.Id)!.Money = value; else Group(account.Id)!.Money = value; }
    public void Reserve(Group group, IEnumerable<Person> workers, long until, string purpose, long reference)
    {
        foreach (var p in workers)
        {
            State.Labor.Add(new(Id(), group.Id, p.Id, Context.Month, until, purpose, reference));
            p.Action = ActionKind.Guard; p.Due = until; p.ActionTarget = group.Id;
            p.ActionData = $"politics:{purpose}:{reference}";
        }
    }
    public void ReleaseLabor(long reference, string purpose)
    {
        foreach (var r in State.Labor.Where(x => x.Reference == reference && x.Purpose == purpose))
            if (Person(r.Person) is { } p && p.ActionData == $"politics:{purpose}:{reference}")
            { p.Action = ActionKind.Idle; p.Due = 0; p.ActionTarget = 0; p.ActionData = ""; }
        State.Labor.RemoveAll(x => x.Reference == reference && x.Purpose == purpose);
    }
    public void Cancel(PolicyPreparation preparation, string reason)
    {
        Group(preparation.Group)!.Money += preparation.Escrow; preparation.Escrow = 0;
        preparation.Stage = PolicyStage.Cancelled; ReleaseLabor(preparation.Id, "policy");
        State.PolicyHistory.Add(new(Context.Month, preparation.Id, preparation.Group, preparation.Category,
            Group(preparation.Group)!.Policies.GetValueOrDefault(preparation.Category, 1), preparation.Choice,
            preparation.Stage, reason, Context.LawRevision));
    }
    public PoliticalResult Finish(string reason)
    {
        var errors = PoliticalInvariants.Validate(Context, State);
        if (errors.Count != 0) return PoliticalResult.Rejected(errors[0]);
        var consumed = State.ConsumedMoney - before.ConsumedMoney;
        var transition = new PoliticalTransition(PoliticsEngine.Fingerprint(original, before), State,
            Changed(original.People, People, p => p.Id), Changed(original.Groups, Groups, g => g.Id),
            Changed(original.Places, Places, p => p.Id), consumed, reason);
        return PoliticsEngine.ValidateForApplication(original, before, transition, out var error)
            ? new(true, reason, transition) : PoliticalResult.Rejected(error);
    }
    private static List<T> Changed<T>(IReadOnlyList<T> prior, IEnumerable<T> current, Func<T, long> key) =>
        current.Where(x => !prior.Any(y => key(y) == key(x) && JsonSerializer.Serialize(y) == JsonSerializer.Serialize(x))).OrderBy(key).ToList();
}
