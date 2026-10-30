using System.Text.Json;
using System.Text.Json.Serialization;
using Chronicles.Game.Politics;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private static readonly JsonSerializerOptions PoliticsCommandJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<PoliticalAccountKind>() }
    };
    private static readonly HashSet<string> PoliticsCommandKinds = new(StringComparer.Ordinal)
    {
        "found", "join", "policy", "adoptPolicy", "preparePolicy", "implementPolicy", "cancelPolicy", "govern", "releaseProvince", "claimProvince",
        "vassal", "dissolve", "delegate", "transferOwnership", "setOperator", "grantPassage", "transferProvince", "loan", "repayDebt", "enforceDebt",
        "assignReceivable", "novateDebt", "enforcementOwner", "charterSect", "revokeCharter", "appointOffice", "policyActivity", "payroll", "collectTax", "succession", "cancel"
    };

    private sealed record PoliticsCommandOptions
    {
        public string Category { get; init; } = "";
        public string Knowledge { get; init; } = "";
        public string Basis { get; init; } = "명시 계약";
        public int Place { get; init; } = -1;
        public int Province { get; init; } = -1;
        public long Counterparty { get; init; }
        public long ToGroup { get; init; }
        public long Until { get; init; }
        public long Fee { get; init; }
        public long Enforcer { get; init; }
        public bool Teaching { get; init; } = true;
        public bool Industry { get; init; } = true;
        public PoliticalAccount Creditor { get; init; }
        public PoliticalAccount Debtor { get; init; }
        public PoliticalAccount Holder { get; init; }
    }

    private PoliticalContext PoliticsContext(bool includeMaturity = true) => new(Month, Revision, state.LawRevision,
        state.People, state.Groups, state.Places, PoliticsRules(),
        includeMaturity ? new SortedDictionary<long, long>(state.People.ToDictionary(p => p.Id, PeopleMaturityMonths)) : null);

    private PoliticalRules PoliticsRules()
    {
        var defaults = new PoliticalRules();
        long Value(string key, long fallback) => state.Laws.GetValueOrDefault(key, fallback);
        return defaults with
        {
            MaturityMonths = Law("birth.maturity"), SectLimit = IntLaw("politics.sectLimit"),
            FoundingTreasury = Law("politics.foundCost"), PolicyPreparationCost = Law("politics.policyCost"),
            FamilyFounderMinimumRealm = IntLaw("politics.founderRealm"),
            BaseSalary = Law("politics.salary"), PreparationMonths = Value("politics.preparationMonths", defaults.PreparationMonths),
            PreparationWorkers = checked((int)Value("politics.preparationWorkers", defaults.PreparationWorkers)),
            TaxBasisPoints = checked((int)Value("politics.taxBasisPoints", defaults.TaxBasisPoints)),
            TributeBasisPoints = checked((int)Value("politics.tributeBasisPoints", defaults.TributeBasisPoints)),
            ActivityGrant = Value("politics.activityGrant", defaults.ActivityGrant), ReliefThreshold = Value("politics.reliefThreshold", defaults.ReliefThreshold)
        };
    }

    // Call after the shared actor/control check and before legacy political handlers.
    private bool TryPoliticsCommand(GameCommand c, Person p, out string? error)
    {
        error = null;
        if (!PoliticsCommandKinds.Contains(c.Kind)) return false;
        var context = PoliticsContext(); var domain = state.Politics;
        var options = new PoliticsCommandOptions();
        if (c.Data.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            try { options = JsonSerializer.Deserialize<PoliticsCommandOptions>(c.Data, PoliticsCommandJson) ?? options; }
            catch (JsonException) { error = "정치 명령의 입력 형식을 확인하세요."; return true; }
        }
        PoliticalResult? plan;
        switch (c.Kind)
        {
            case "found":
                if (c.Data is not ("sect" or "family")) { error = "가문 또는 종문을 선택하세요."; return true; }
                plan = PoliticsEngine.PlanFound(context, domain, p.Id, state.NextId, $"{p.Name}{(c.Data == "sect" ? "종" : "가")}",
                    c.Data == "sect" ? GroupKind.Sect : GroupKind.Family);
                break;
            case "join":
                if (!groups.TryGetValue(c.Target, out var joining) || joining.Kind != GroupKind.Sect ||
                    !people.TryGetValue(joining.Leader, out var leader) || !PoliticsAgreement(p, leader))
                { error = "실제 종문 수장과의 대면·입문 동의가 필요합니다."; return true; }
                plan = PoliticsEngine.PlanPolicyActivity(context, domain, new(leader.Id, joining.Id, "입문·선발", p.Id));
                break;
            case "policy":
            case "adoptPolicy":
                if (c.Value is < 0 or > 2) { error = "정책의 세부 선택 범위를 확인하세요."; return true; }
                var category = options.Category.Length == 0 ? c.Data : options.Category;
                plan = c.Kind == "policy"
                    ? PoliticsEngine.PlanPolicyChange(context, domain, p.Id, c.Target, category, (int)c.Value, options.Place)
                    : PoliticsEngine.PlanAdoptPolicy(context, domain, p.Id, c.Target, category, (int)c.Value, options.Place);
                break;
            case "preparePolicy": plan = PoliticsEngine.PlanPreparePolicy(context, domain, p.Id, c.Target); break;
            case "implementPolicy":
                if (!domain.Preparations.Any(x => x.Id == c.Target && groups.TryGetValue(x.Group, out var g) && g.Leader == p.Id))
                { error = "현재 정책 시행 권한이 없습니다."; return true; }
                plan = PoliticsEngine.PlanImplementPolicy(context, domain, c.Target); break;
            case "cancelPolicy": plan = PoliticsEngine.PlanCancelPolicy(context, domain, p.Id, c.Target); break;
            case "govern":
            case "releaseProvince":
            case "claimProvince":
                if (c.Value is < 0 or >= 10) { error = "실제 주를 선택하세요."; return true; }
                plan = c.Kind == "govern" ? PoliticsEngine.PlanSovereignty(context, domain, p.Id, c.Target, (int)c.Value) :
                    c.Kind == "releaseProvince" ? PoliticsEngine.PlanReleaseProvince(context, domain, p.Id, c.Target, (int)c.Value) :
                    PoliticsEngine.PlanClaim(context, domain, p.Id, c.Target, (int)c.Value, options.Basis == "명시 계약" ? c.Data : options.Basis);
                break;
            case "vassal":
                var overlordConsent = c.Value == 0 ? 0 : PoliticsGroupConsent(p, c.Value);
                plan = PoliticsEngine.PlanVassalage(context, domain, p.Id, c.Target, c.Value, overlordConsent); break;
            case "dissolve": plan = PoliticsEngine.PlanDissolve(context, domain, p.Id, c.Target); break;
            case "delegate":
            case "transferOwnership":
            case "setOperator":
            case "grantPassage":
                if (c.Target is < 0 or >= 100 || !p.KnownPlaces.Contains((int)c.Target))
                { error = "개인이 아는 실제 지점을 선택하세요."; return true; }
                var holder = options.Holder.Id == 0 ? PoliticsAccount(c.Value) : options.Holder;
                if (c.Kind == "delegate" ? c.Value != 0 && !PoliticsKnowsAccount(p, PoliticalAccount.Group(c.Value)) :
                    !(c.Kind == "setOperator" && holder.Id == 0) && !PoliticsKnowsAccount(p, holder))
                { error = "개인이 아는 실제 권리 수령자를 선택하세요."; return true; }
                plan = c.Kind == "delegate" ? PoliticsEngine.PlanDelegate(context, domain, p.Id, (int)c.Target, c.Value) :
                    c.Kind == "grantPassage" ? PoliticsEngine.PlanPassage(context, domain, p.Id, (int)c.Target, holder, options.Until) :
                    PoliticsEngine.PlanProperty(context, domain, p.Id, (int)c.Target,
                        c.Kind == "transferOwnership" ? PoliticalRight.Ownership : PoliticalRight.Operation, holder);
                break;
            case "transferProvince":
                if (c.Value is < 0 or >= 10) { error = "실제 주를 선택하세요."; return true; }
                plan = PoliticsEngine.PlanTransferSovereignty(context, domain, p.Id, c.Target, options.ToGroup,
                    PoliticsGroupConsent(p, options.ToGroup), (int)c.Value); break;
            case "loan":
                var creditor = options.Creditor.Id == 0 ? PoliticalAccount.Person(p.Id) : options.Creditor;
                var debtor = options.Debtor.Id == 0 ? PoliticsAccount(c.Target) : options.Debtor;
                plan = PoliticsEngine.PlanLoan(context, domain, p.Id, creditor, debtor, PoliticsAccountConsent(p, debtor),
                    c.Value, options.Enforcer, options.Province, options.Basis); break;
            case "repayDebt": plan = PoliticsEngine.PlanRepayment(context, domain, p.Id, c.Target, c.Value); break;
            case "enforceDebt": plan = PoliticsEngine.PlanEnforceDebt(context, domain, p.Id, c.Target, c.Value); break;
            case "assignReceivable":
            case "novateDebt":
                var nextAccount = options.Holder.Id == 0 ? PoliticsAccount(c.Value) : options.Holder;
                plan = c.Kind == "assignReceivable" ? PoliticsEngine.PlanAssignReceivable(context, domain, p.Id, c.Target,
                    nextAccount, PoliticsAccountConsent(p, nextAccount)) : PoliticsEngine.PlanNovateDebt(context, domain, p.Id, c.Target,
                    nextAccount, PoliticsAccountConsent(p, nextAccount)); break;
            case "enforcementOwner":
                plan = PoliticsEngine.PlanEnforcementOwner(context, domain, p.Id, c.Target, c.Value,
                    c.Value == 0 ? 0 : PoliticsGroupConsent(p, c.Value)); break;
            case "charterSect":
                if (c.Value is < 0 or >= 10) { error = "실제 주를 선택하세요."; return true; }
                plan = PoliticsEngine.PlanCharter(context, domain, p.Id, c.Target, PoliticsGroupConsent(p, c.Target),
                    (int)c.Value, options.Teaching, options.Industry, options.Until, options.Fee); break;
            case "revokeCharter": plan = PoliticsEngine.PlanRevokeCharter(context, domain, p.Id, c.Target); break;
            case "appointOffice":
                if (!PoliticsKnowsAccount(p, PoliticalAccount.Person(c.Value))) { error = "개인이 아는 현재 구성원을 선택하세요."; return true; }
                plan = PoliticsEngine.PlanOffice(context, domain, p.Id, c.Target, c.Data, c.Value); break;
            case "policyActivity":
                if (options.Category == "대외·외교")
                {
                    var consenting = PoliticsGroupConsent(p, c.Value);
                    if (consenting == 0) { error = "상대 조직 현 수장의 실제 동의가 필요합니다."; return true; }
                    options = options with { Counterparty = consenting };
                }
                else if (options.Category is not ("경제·분배" or "영토·방위") && !PoliticsKnowsAccount(p, PoliticalAccount.Person(c.Value)) ||
                    options.Category == "교육·전승" && !PoliticsKnowsAccount(p, PoliticalAccount.Person(options.Counterparty)))
                { error = "개인이 아는 실제 정책 참여자를 선택하세요."; return true; }
                plan = PoliticsEngine.PlanPolicyActivity(context, domain, new(p.Id, c.Target, options.Category,
                    c.Value, options.Counterparty, options.Knowledge, options.Place)); break;
            case "payroll": plan = PoliticsEngine.PlanPayroll(context, domain, p.Id, c.Target); break;
            case "collectTax":
                if (!groups.TryGetValue(c.Target, out var taxing) || taxing.Leader != p.Id || taxing.Dissolved || taxing.Provinces.Count == 0)
                { error = "실제 영토의 현재 조세 권한이 필요합니다."; return true; }
                plan = PoliticsEngine.PlanCollectTax(context, domain); break;
            case "succession":
                if (!groups.TryGetValue(c.Target, out var succeeding) || !succeeding.Members.Contains(p.Id) || PoliticsSuccessor(succeeding) != c.Value)
                { error = "현재 구성원과 실제 승계 우선순위를 확인하세요."; return true; }
                plan = PoliticsEngine.PlanSuccession(context, domain, c.Target, c.Value); break;
            case "cancel": return TryCancelPoliticsWork(p, out error);
            default: return false;
        }
        error = CommitPolitics(plan, p.Id, c.Target, "정치 명령", c.Id);
        return true;
    }

    private string? CommitPolitics(PoliticalResult result, long actor = 0, long target = 0, string kind = "", string source = "실제 정치 전이")
    {
        if (!result.Accepted || result.Transition is not { } transition) return result.Message;
        if (!PoliticsEngine.ValidateForApplication(PoliticsContext(), state.Politics, transition, out var error)) return error;
        var addedGroups = transition.Groups.Where(g => !groups.ContainsKey(g.Id)).OrderBy(g => g.Id).ToArray();
        for (var i = 0; i < addedGroups.Length; i++)
            if (addedGroups[i].Id != state.NextId + i) return "새 조직의 안정 신원이 바뀌었습니다. 다시 확인하세요.";
        // AI invokes this adapter directly, so allocate global IDs only after all pure checks accept.
        state.NextId = checked(state.NextId + addedGroups.Length);
        foreach (var update in transition.People)
        {
            var current = people[update.Id];
            // Preserve the identity held by Step's living[] and by the current action/command.
            current.Money = update.Money; current.Household = update.Household; current.Sect = update.Sect;
            current.Action = update.Action; current.Due = update.Due; current.ActionTarget = update.ActionTarget; current.ActionData = update.ActionData;
        }
        foreach (var update in transition.Groups)
        {
            var index = state.Groups.FindIndex(g => g.Id == update.Id);
            if (index < 0) state.Groups.Add(update); else state.Groups[index] = update;
        }
        foreach (var update in transition.Places) state.Places[update.Id] = update;
        state.Politics = transition.NextState;
        state.ConsumedMoney = checked(state.ConsumedMoney + transition.ConsumedMoney);
        Reindex();
        if (kind.Length != 0) Event(kind, actor, target, transition.Reason, source);
        return null;
    }

    private bool PoliticsLaborAvailable(Person person, long from, long until) =>
        PoliticsEngine.IsLaborAvailable(PoliticsContext(), state.Politics, person.Id, from, until);

    private bool TryCancelPoliticsWork(Person person, out string? error)
    {
        error = null;
        if (!state.Politics.Labor.Any(r => r.Person == person.Id && r.From <= Month && r.Until > Month)) return false;
        error = CommitPolitics(PoliticsEngine.PlanCancelWork(PoliticsContext(), state.Politics, person.Id), person.Id, person.Household, "정치 작업 취소");
        return true;
    }

    private PoliticalAccount PoliticsAccount(long id) => groups.ContainsKey(id) ? PoliticalAccount.Group(id) : PoliticalAccount.Person(id);
    private bool PoliticsKnowsAccount(Person actor, PoliticalAccount account) => account.Kind == PoliticalAccountKind.Person
        ? account.Id == actor.Id || actor.KnownPeople.Contains(account.Id)
        : account.Id == actor.Household || account.Id == actor.Sect || groups.TryGetValue(account.Id, out var group) && actor.KnownPeople.Contains(group.Leader);
    private long PoliticsAccountConsent(Person proposer, PoliticalAccount account) => account.Kind == PoliticalAccountKind.Group
        ? PoliticsGroupConsent(proposer, account.Id) : people.TryGetValue(account.Id, out var respondent) && PoliticsAgreement(proposer, respondent) ? respondent.Id : 0;
    private long PoliticsGroupConsent(Person proposer, long group) => groups.TryGetValue(group, out var g) &&
        people.TryGetValue(g.Leader, out var leader) && PoliticsAgreement(proposer, leader) ? leader.Id : 0;
    private bool PoliticsAgreement(Person proposer, Person respondent) => respondent.Status == LifeStatus.Alive &&
        Month - respondent.Born >= PeopleMaturityMonths(respondent) && (proposer.Id == respondent.Id ||
            ShareExplorationSpace(respondent, proposer) && proposer.KnownPeople.Contains(respondent.Id) && respondent.KnownPeople.Contains(proposer.Id) &&
            respondent.Personality[4] + proposer.Personality[4] >= 50);

    private long PoliticsSuccessor(Group group)
    {
        var candidates = group.Members.Where(people.ContainsKey).Select(id => people[id]).Where(p =>
            p.Status == LifeStatus.Alive && Month - p.Born >= PeopleMaturityMonths(p));
        return group.Policies.GetValueOrDefault("승계·인사", 1) switch
        {
            0 => candidates.OrderBy(p => p.Born).ThenBy(p => p.Id).Select(p => p.Id).FirstOrDefault(),
            1 => candidates.OrderByDescending(p => state.Politics.Service.FirstOrDefault(s => s.Group == group.Id && s.Person == p.Id)?.Contribution ?? 0)
                .ThenBy(p => p.Id).Select(p => p.Id).FirstOrDefault(),
            _ => candidates.OrderBy(p => p.Id).Select(p => p.Id).FirstOrDefault()
        };
    }

    // Replace legacy SettleGroups, before Step captures living[]. Legacy tax/payroll must not also run.
    private void SettlePolitics()
    {
        foreach (var prep in state.Politics.Preparations.Where(x => x.Stage == PolicyStage.Preparing && x.Due <= Month).OrderBy(x => x.Id).ToArray())
            RequirePoliticalSettlement(PoliticsEngine.PlanImplementPolicy(PoliticsContext(), state.Politics, prep.Id), prep.Author, prep.Group, "정책 결산");
        if (state.Politics.Labor.Any(r => r.Until <= Month && r.Purpose != "policy"))
            RequirePoliticalSettlement(PoliticsEngine.PlanReleaseCompletedLabor(PoliticsContext(), state.Politics));
        if (state.Politics.Offices.Any(o => !people.TryGetValue(o.Holder, out var holder) || holder.Status != LifeStatus.Alive))
            RequirePoliticalSettlement(PoliticsEngine.PlanVacateDepartedOffices(PoliticsContext(), state.Politics));
        foreach (var group in state.Groups.Where(g => !g.Dissolved && (g.Leader == 0 || !people.TryGetValue(g.Leader, out var p) || p.Status != LifeStatus.Alive)).ToArray())
        {
            var successor = PoliticsSuccessor(group);
            if (successor == 0 && (group.Provinces.Count == 0 || group.Members.Any(id => people[id].Status == LifeStatus.Alive))) continue;
            RequirePoliticalSettlement(PoliticsEngine.PlanSuccession(PoliticsContext(), state.Politics, group.Id, successor), successor, group.Id, "정치 승계");
        }
        foreach (var group in state.Groups.Where(g => g.Kind == GroupKind.Sect && !g.Dissolved &&
            people.TryGetValue(g.Leader, out var p) && p.Status == LifeStatus.Alive).OrderBy(g => g.Id).ToArray())
            if (!state.Politics.Payments.Any(p => p.Kind == $"payroll:{group.Id}:{Month}"))
                RequirePoliticalSettlement(PoliticsEngine.PlanPayroll(PoliticsContext(), state.Politics, group.Leader, group.Id));
        if (Month > 0 && Month % 12 == 0 && !state.Politics.TaxPeriods.Contains(Month))
            RequirePoliticalSettlement(PoliticsEngine.PlanCollectTax(PoliticsContext(), state.Politics));
        RoutePoliticalTeaching();
    }

    private void RequirePoliticalSettlement(PoliticalResult result, long actor = 0, long target = 0, string kind = "")
    {
        if (CommitPolitics(result, actor, target, kind) is { } error) throw new InvalidOperationException(error);
    }

    // Call after actual physical inheritance and after changing the person's life status; replace old automatic office loops.
    private void PoliticsOnLifeEnded(Person person, Person? actualHeir)
    {
        if (state.Places.Any(place => place.Operator == person.Id || person.Status == LifeStatus.Dead && actualHeir is not null && place.Owner == person.Id))
            RequirePoliticalSettlement(PoliticsEngine.PlanDepartedPersonalRights(PoliticsContext(), state.Politics, person.Id,
                person.Status == LifeStatus.Dead ? actualHeir?.Id ?? 0 : 0));
        if (person.Status == LifeStatus.Dead && actualHeir is not null && state.Politics.Obligations.Any(o =>
            o.Creditor == PoliticalAccount.Person(person.Id) || o.Debtor == PoliticalAccount.Person(person.Id)))
            RequirePoliticalSettlement(PoliticsEngine.PlanEstateObligations(PoliticsContext(), state.Politics, person.Id, actualHeir.Id, actualHeir.Id));
        foreach (var group in state.Groups.Where(g => !g.Dissolved && g.Leader == person.Id).ToArray())
            RequirePoliticalSettlement(PoliticsEngine.PlanSuccession(PoliticsContext(), state.Politics, group.Id, PoliticsSuccessor(group)), 0, group.Id, "정치 승계");
        if (state.Politics.Offices.Any(o => o.Holder == person.Id))
            RequirePoliticalSettlement(PoliticsEngine.PlanVacateDepartedOffices(PoliticsContext(), state.Politics));
    }

    private void RoutePoliticalTeaching()
    {
        foreach (var effect in state.Politics.Effects.Where(e => e.Kind == PoliticalEffectKind.Teaching && e.Until <= Month &&
            !state.Politics.RoutedEffects.Contains(e.Id)).OrderBy(e => e.Id).ToArray())
        {
            if (!people.TryGetValue(effect.Person, out var pupil) || !people.TryGetValue(effect.Other, out var teacher) ||
                pupil.Status != LifeStatus.Alive || teacher.Status != LifeStatus.Alive || pupil.Place != teacher.Place ||
                !pupil.KnownPeople.Contains(teacher.Id) || !teacher.KnownPeople.Contains(pupil.Id) ||
                !PoliticsEngine.CanImplementPolicy(PoliticsContext(), state.Politics, effect.Group, effect.Place, "교육·전승") ||
                teacher.Understanding.GetValueOrDefault(effect.Subject) <= pupil.Understanding.GetValueOrDefault(effect.Subject) ||
                Month > effect.Until + 12)
            { state.Politics.RoutedEffects.Add(effect.Id); continue; }
            if (teacher.LessonRequests.Count >= Law("ai.planBudget")) continue;
            if (!teacher.LessonRequests.Any(r => r.Pupil == pupil.Id && r.Knowledge == effect.Subject && r.Expires > Month))
            {
                teacher.LessonRequests.Add(new(pupil.Id, effect.Subject, Month, Month + 12));
                Event("배움 요청", pupil.Id, teacher.Id, $"{pupil.Name} 정책 예산으로 {teacher.Name}에게 전수 요청", $"실제 정책 활동 {effect.Id}");
            }
            state.Politics.RoutedEffects.Add(effect.Id);
        }
    }
}
