using System.Globalization;
using Chronicles.Game.Cultivation;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;
using Chronicles.Game.People;
using Chronicles.Game.Politics;
using KnowledgeEvidence = Chronicles.Game.Cultivation.KnowledgeEvidence;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // Hooks: initialize after people/knowledge/inventory initialization; call TryCultivationCommand before
    // legacy dispatch (also from AI Start); call CompleteCultivation BEFORE Complete clears Action;
    // SettleCultivation follows source regeneration and precedes actor completions. Death cleanup precedes inheritance.
    private const long NeutralCultivationFlow = 5000;
    private const int NaturalCultivationSeats = 4;

    private void InitializeCultivation()
    {
        foreach (var p in state.People)
            if (!state.Cultivation.ContainsKey(p.BodyId))
                InitializeCultivationPerson(p, Month == 0 && p.Realm == Realm.HalfImmortal && p.ParentOne == 0 && p.ParentTwo == 0);
    }

    private void InitializeCultivationPerson(Person p, bool founder = false)
    {
        if (state.Cultivation.ContainsKey(p.BodyId)) return;
        if (!founder && p.Realm != Realm.Mortal)
            throw new InvalidDataException("A nonmortal body must retain its actual cultivation metadata.");
        var metadata = founder ? CultivationProfiles.ForFounder(p.BodyId, p.SoulId, Month, CultivationPolicyForWorld()) :
            CultivationProfiles.ForNewBody(p.BodyId, p.SoulId, p.ParentOne == 0 ? "명시 편입의 현재 영근 입력" : $"출생 유전 입력·부모 {p.ParentOne}/{p.ParentTwo}");
        state.Cultivation.Add(p.BodyId, metadata);
        if (founder) { p.Layer = 5; p.GreatPerfection = false; }
        p.Work = 0; p.MartialWork = 0; p.Irreversible = false; p.PendingLayer = 0;
        p.Processes = new int[4]; p.Components = new int[7];
        p.Formations.Clear(); // Old generic formation copies are not a second source of realm authority.
        MirrorCultivation(p, metadata);
    }

    private bool TryCultivationCommand(GameCommand c, Person p, out string? error)
    {
        error = null;
        var kind = c.Kind.ToLowerInvariant();
        if (kind == "cancel")
        {
            if (!state.Cultivation.TryGetValue(p.BodyId, out var current)) return false;
            if (current.PendingTrial is not null) { error = "발동한 실제 겁난은 취소할 수 없습니다."; return true; }
            if (CultivationEngine.ActiveAttempt(current))
            {
                error = ApplyCultivation(p, new CancelCultivation(c.Id, current.Revision));
                if (error is null && p.Status == LifeStatus.Alive) ClearCultivationAction(p);
                return true;
            }
            if (p.Action is not (ActionKind.Cultivate or ActionKind.Martial)) return false;
            if (p.Action == ActionKind.Cultivate && p.ActionTarget != 0)
                CancelKnowledgePractice(p, p.ActionTarget, $"cultivation.cancel.{p.BodyId}.{Month}.{c.Id}");
            ClearCultivationAction(p); return true;
        }
        if (kind is not ("cultivate" or "martial" or "breakthrough" or "trial" or "cultivationprepare" or
            "cultivationstart" or "cultivationfracture" or "cultivationresolve" or "cultivationstabilize" or
            "cultivationrecover" or "cultivationconvert" or "cultivationauxiliary")) return false;
        if (p.Status != LifeStatus.Alive) { error = "현재 살아 있는 육신이 필요합니다."; return true; }
        if (!state.Cultivation.TryGetValue(p.BodyId, out var metadata)) { error = "육신의 수행 초기 프로필이 없습니다."; return true; }
        if (kind == "cultivationstart") { error = ApplyCultivation(p, new StartCultivation(c.Id, metadata.Revision)); ScheduleCultivation(p); return true; }
        if (kind == "cultivationfracture") { error = ApplyCultivation(p, new FractureGoldenCore(c.Id, metadata.Revision)); ScheduleCultivation(p); return true; }
        if (kind == "cultivationresolve") { error = ApplyCultivation(p, new ResolveCultivation(c.Id, metadata.Revision)); ScheduleCultivation(p); return true; }
        if (kind == "cultivationstabilize") { error = ApplyCultivation(p, new StabilizeCultivation(c.Id, metadata.Revision)); ScheduleCultivation(p); return true; }
        if (p.Action != ActionKind.Idle) { error = "기존 행동이 진행 중입니다."; return true; }
        if (p.Health < 10 && kind != "cultivationrecover") { error = "먼저 육신 회복이 필요합니다."; return true; }
        if (kind != "cultivationauxiliary" && state.Inventory.Labor.Any(l => l.Active && l.Purpose != "world-action" &&
            l.Person == p.Id && l.Start < Month + 1 && l.End > Month))
        { error = "같은 월에 예약된 생업/운송 노동이 있습니다."; return true; }
        if (kind == "cultivationauxiliary")
        {
            error = ApplyCultivation(p, new ConfigureAuxiliaryCultivation(c.Id, metadata.Revision, c.Data)); return true;
        }
        if (kind is "breakthrough" or "cultivationprepare" or "cultivationconvert")
        {
            error = PrepareWorldCultivation(p, c, kind == "cultivationconvert");
            if (error is null) ScheduleCultivation(p);
            return true;
        }
        if (kind == "trial")
        {
            if (metadata.PendingTrial is null) { error = "아직 발동한 소경지 겁난이 없습니다."; return true; }
            p.Action = ActionKind.Trial; p.Due = Math.Max(Month + 1, metadata.PendingTrial.Due); return true;
        }
        if (CultivationEngine.ActiveAttempt(metadata) || metadata.PendingTrial is not null)
        { error = "현재 형성·안착·실제 겁난을 먼저 종결해야 합니다."; return true; }
        if (kind == "cultivationrecover")
        {
            p.Action = ActionKind.Heal; p.ActionData = c.Value != 0 ? "cultivation.medical" : "cultivation.recovery";
            p.Due = Month + 1; p.ActionRevision = state.LawRevision; return true;
        }
        if (kind == "martial")
        {
            if (p.Martial >= 10) { error = "무인 10경지가 완성되었습니다."; return true; }
            p.Action = ActionKind.Martial; p.Due = Month + 1; p.ActionRevision = state.LawRevision; return true;
        }
        var path = CurrentCultivationPath(p, c.Data);
        if (path is null) { error = "현재 경지의 배운 공법 또는 입문 지식이 필요합니다."; return true; }
        var context = CultivationContextFor(p);
        if (!context.Site.HasUseRight || (!context.Site.HasReservedSeat && context.Site.OccupiedSeats >= context.Site.SeatCapacity))
        { error = "실제 수행 사용권/자리가 부족합니다."; return true; }
        p.Action = ActionKind.Cultivate; p.ActionData = path; p.ActionRevision = state.LawRevision; p.Due = Month + 1;
        p.ActionTarget = BeginCultivationKnowledgePractice(p, p.Realm == Realm.Mortal ? "entry" : path);
        return true;
    }

    private bool CompleteCultivation(Person p)
    {
        var action = p.Action;
        var recovery = action == ActionKind.Heal && p.ActionData.StartsWith("cultivation.", StringComparison.Ordinal);
        if (action is not (ActionKind.Cultivate or ActionKind.Breakthrough or ActionKind.Trial or ActionKind.Martial) && !recovery) return false;
        var data = p.ActionData; var knowledgeActivity = action == ActionKind.Cultivate ? p.ActionTarget : 0;
        ClearCultivationAction(p);
        if (!state.Cultivation.TryGetValue(p.BodyId, out var metadata)) { p.Reason = "수행 프로필 없음"; return true; }
        string? error;
        if (recovery)
            error = ApplyCultivation(p, new RecoverCultivation(CultivationActionId(p, "recover"), metadata.Revision, data == "cultivation.medical"));
        else if (action == ActionKind.Cultivate)
        {
            if (ApplyCultivationEnvironmentalDamage(p))
            {
                if (knowledgeActivity != 0) FinishCultivationKnowledgePractice(p, knowledgeActivity, false,
                    CultivationActionId(p, "environment"), "실제 수행 환경의 구조/결속 붕괴");
                return true;
            }
            metadata = state.Cultivation[p.BodyId];
            var before = metadata.EntryWork;
            var command = new PracticeCultivation(CultivationActionId(p, "practice"), metadata.Revision, data, Elements.Uniform(1));
            error = ApplyCultivation(p, command, out var result);
            var performed = result?.Accepted == true && (result.Flow?.Assimilated.Total > 0 || result.State.EntryWork > before);
            if (knowledgeActivity != 0) FinishCultivationKnowledgePractice(p, knowledgeActivity, performed,
                command.Id, performed ? "실제 토납/정제/순환/체화 완료" : "실제 공급/공정 부족·무성과");
            if (performed)
            {
                Gain(p, p.Realm == Realm.Mortal ? "entry.practice" : "circulation", 1);
                RecordPeopleLearning(p, p.Realm == Realm.Mortal ? "entry" : "circulation", 1, 0, command.Id);
            }
            // A completed ordinary month returns to the shared decision boundary. Readiness is not an
            // automatic new attempt: food, recovery, care and the next goal must be considered first.
        }
        else if (action == ActionKind.Martial)
        {
            error = ApplyCultivation(p, new TrainMartial(CultivationActionId(p, "martial"), metadata.Revision));
            if (error is null)
            {
                RecordPeopleLearning(p, "martial", 1, 0, CultivationActionId(p, "martial-practice"));
                metadata = state.Cultivation[p.BodyId];
                var required = CultivationPolicyForWorld().MartialMonthsPerStage * (p.Martial + 1L) * NeutralCultivationFlow;
                if (metadata.Martial.Work >= required && p.Health >= 50 && metadata.Martial.Injury <= CultivationPolicyForWorld().FailureDamage)
                    error = ApplyCultivation(p, new BreakthroughMartial(CultivationActionId(p, "martial-breakthrough"), metadata.Revision));
            }
        }
        else if (metadata.PendingTrial is not null)
            error = ApplyCultivation(p, new ResolveMinorTribulation(CultivationActionId(p, "trial"), metadata.Revision));
        else
        {
            var attempt = metadata.Attempt;
            CultivationCommand? command = attempt?.Phase switch
            {
                AttemptPhase.Prepared => new StartCultivation(CultivationActionId(p, "start"), metadata.Revision),
                AttemptPhase.Started when attempt.TargetRealm == Realm.NascentSoul && attempt.Kind == AttemptKind.RealmBreakthrough =>
                    new FractureGoldenCore(CultivationActionId(p, "fracture"), metadata.Revision),
                AttemptPhase.Started or AttemptPhase.Fractured => new ResolveCultivation(CultivationActionId(p, "resolve"), metadata.Revision),
                AttemptPhase.Stabilizing or AttemptPhase.Recovering => new StabilizeCultivation(CultivationActionId(p, "stabilize"), metadata.Revision),
                _ => null
            };
            error = command is null ? "진행할 수행 공정이 없습니다." : ApplyCultivation(p, command);
        }
        if (error is not null) p.Reason = error;
        ScheduleCultivation(p); return true;
    }

    private string? PrepareWorldCultivation(Person p, GameCommand command, bool conversion)
    {
        var metadata = state.Cultivation[p.BodyId];
        var path = command.Data.Length == 0 ? NextKnownCultivationPath(p) : command.Data;
        if (path is null) return "배운 목표 경지 공법이 없습니다.";
        var context = CultivationContextFor(p);
        var itemsToUse = command.Target == 0 ? Array.Empty<long>() : [command.Target];
        if (command.Target != 0 && !context.Supports.Any(i => i.ItemId == command.Target && i.Kind == "pill" && i.ReservedBy.Length == 0))
            return "현재 접근 가능한 미예약 단약 실물이 필요합니다.";
        // Preview has no RNG or mutations; a failed AI preparation must not leave a physical split/reservation.
        var preview = CultivationEngine.Execute(metadata, CultivationActorFor(p),
            new PrepareCultivation(command.Id, metadata.Revision, path, itemsToUse, conversion), context, 9999);
        if (!preview.Accepted) return CultivationFailure(preview);
        long reservation; long physical = 0;
        if (command.Target != 0)
        {
            var error = ReserveInventoryPhysical(p, command.Target, 1, "cultivation-support", out reservation, out physical);
            if (error is not null) return error;
        }
        else reservation = Id();
        context = CultivationContextFor(p);
        if (physical != 0)
        {
            var selected = items[physical];
            context = context with { Supports = [.. context.Supports.Where(s => s.ItemId != physical),
                new(physical, "pill", selected.Quantity, selected.Quality)] };
        }
        var prepared = CultivationEngine.Execute(metadata, CultivationActorFor(p),
            new PrepareCultivation(reservation.ToString(CultureInfo.InvariantCulture), metadata.Revision, path,
                physical == 0 ? [] : [physical], conversion), context, 9999);
        if (!prepared.Accepted)
        {
            if (physical != 0) ReleaseInventoryReservation(p, reservation);
            return CultivationFailure(prepared);
        }
        CommitCultivation(p, prepared); return null;
    }

    private string? ApplyCultivation(Person p, CultivationCommand command) => ApplyCultivation(p, command, out _);
    private string? ApplyCultivation(Person p, CultivationCommand command, out CultivationResult? accepted)
    {
        accepted = null;
        if (!state.Cultivation.TryGetValue(p.BodyId, out var metadata)) return "수행 프로필 없음";
        var context = CultivationContextFor(p); var actor = CultivationActorFor(p);
        // Eligibility preview never consumes the world's random stream. Only a valid actual resolution draws once.
        var preview = CultivationEngine.Execute(metadata, actor, command, context, 9999);
        if (!preview.Accepted) return CultivationFailure(preview);
        var resolves = command is ResolveCultivation or ResolveMinorTribulation or BreakthroughMartial;
        var result = resolves ? CultivationEngine.Execute(metadata, actor, command, context, Roll(10000)) : preview;
        if (!result.Accepted) throw new InvalidOperationException("A valid cultivation resolution changed its prerequisites.");
        CommitCultivation(p, result); accepted = result; return null;
    }

    private void CommitCultivation(Person p, CultivationResult result)
    {
        foreach (var group in result.Supports.GroupBy(s => (s.Operation, s.Reservation)))
        {
            if (group.Key.Operation == SupportOperation.Reserve) continue; // Prepared atomically by shared physical reservation helper.
            if (group.Key.Reservation.Length > 0)
            {
                var id = long.Parse(group.Key.Reservation, CultureInfo.InvariantCulture);
                var error = group.Key.Operation == SupportOperation.Release ? ReleaseInventoryReservation(p, id) : ConsumeInventoryReservation(p, id);
                if (error is not null) throw new InvalidOperationException(error);
            }
            else
                foreach (var operation in group)
                {
                    var error = ReserveInventoryPhysical(p, operation.ItemId, operation.Quantity, "cultivation-recovery", out var reservation, out _);
                    if (error is not null) throw new InvalidOperationException(error);
                    error = ConsumeInventoryReservation(p, reservation);
                    if (error is not null) throw new InvalidOperationException(error);
                }
        }
        DebitCultivationSupply(state.Places[p.Place], result.SupplyDebit);
        state.Cultivation[p.BodyId] = result.State;
        var oldRealm = p.Realm; var oldLayer = p.Layer;
        p.Realm = result.Actor.Realm ?? p.Realm; p.Layer = result.Actor.Layer ?? p.Layer;
        p.GreatPerfection = result.Actor.GreatPerfection ?? p.GreatPerfection;
        p.Martial = result.Actor.MartialStage ?? p.Martial;
        p.Health = Math.Clamp(p.Health + result.Actor.HealthChange, 0, 100);
        p.Lifespan = Math.Max(p.Lifespan, result.Actor.LifespanFloorMonths);
        MirrorCultivation(p, result.State);
        if (oldRealm != p.Realm || oldLayer != p.Layer) state.Places[p.Place].PeopleRevision++;
        if (result.Flow is not null) p.Processes = [BoundFlow(result.Flow.Absorbed.Total), BoundFlow(result.Flow.Refined.Total),
            BoundFlow(result.Flow.Circulated.Total), BoundFlow(result.Flow.Assimilated.Total)];
        p.Reason = result.Blockers.Count == 0 ? string.Join("; ", result.Notices.Select(n => n.Detail)) : CultivationFailure(result);
        foreach (var notice in result.Notices.Where(n => n.Kind is not ("공정 실습" or "감응 준비" or "무예 단련")))
            Event(notice.Kind, p.Id, 0, $"{p.Name} {notice.Detail}", notice.Source);
        if (result.Actor.Death is { } death)
        {
            ClearCultivationAction(p);
            var route = death switch { CultivationDeathCause.CoreFracture => 3, CultivationDeathCause.Tribulation => 8,
                CultivationDeathCause.PostTribulationCollapse => 9, CultivationDeathCause.StructureCollapse => 5, CultivationDeathCause.BondCollapse => 6,
                _ => throw new InvalidOperationException("Unknown cultivation death cause.") };
            EndLife(p, route, string.Join("; ", result.Notices.Select(n => n.Detail)));
        }
    }

    private void ScheduleCultivation(Person p)
    {
        // Only already-committed mandatory phases continue; ordinary monthly work is never repeated here.
        if (p.Status != LifeStatus.Alive || !state.Cultivation.TryGetValue(p.BodyId, out var metadata)) return;
        if (metadata.PendingTrial is not null)
        { p.Action = ActionKind.Trial; p.Due = Math.Max(Month + 1, metadata.PendingTrial.Due); p.PendingLayer = metadata.PendingTrial.TargetLayer; return; }
        if (!CultivationEngine.ActiveAttempt(metadata)) return;
        p.Action = ActionKind.Breakthrough; p.ActionData = metadata.Attempt!.Path;
        p.ActionTarget = long.Parse(metadata.Attempt.Id, CultureInfo.InvariantCulture);
        p.Due = Math.Max(Month + 1, metadata.Attempt.Due); p.ActionRevision = metadata.Attempt.LawRevision;
    }

    private void SettleCultivation()
    {
        foreach (var place in state.Places)
        {
            var quantum = CultivationSourceQuantum();
            var opened = state.CultivationSupply.GetValueOrDefault(place.Id);
            var cap = Law("ecology.sourceCapacity") * quantum;
            // Regeneration replenishes unopened parcels only up to the combined real source capacity.
            place.Qi = (int)Math.Min(place.Qi, Math.Max(0, (cap - opened.Total) / quantum));
        }
        foreach (var p in state.People)
        {
            if (p.Status != LifeStatus.Alive) { ReleaseCultivationForLifeEnd(p); continue; }
            if (p.Action == ActionKind.Idle && state.Cultivation.TryGetValue(p.BodyId, out var metadata) &&
                (metadata.PendingTrial is not null || CultivationEngine.ActiveAttempt(metadata))) ScheduleCultivation(p);
        }
    }

    private void ReleaseCultivationForLifeEnd(Person p)
    {
        if (!state.Cultivation.TryGetValue(p.BodyId, out var metadata)) return;
        var attempt = metadata.Attempt;
        if (attempt is not null && long.TryParse(attempt.Id, CultureInfo.InvariantCulture, out var reservation))
        {
            foreach (var item in state.Items.Where(i => i.Owner == p.Id && i.Reserved == reservation)) item.Reserved = 0;
            var row = state.Inventory.Reservations.FirstOrDefault(r => r.Id == reservation && r.Actor == p.Id);
            if (row is not null) row.Active = false;
        }
        if (CultivationEngine.ActiveAttempt(metadata) || metadata.PendingTrial is not null) state.Cultivation[p.BodyId] = metadata with
        { Revision = metadata.Revision + 1, Attempt = !CultivationEngine.ActiveAttempt(metadata) ? attempt :
            attempt! with { Phase = attempt!.Phase == AttemptPhase.Fractured ? AttemptPhase.Failed : AttemptPhase.Cancelled }, PendingTrial = null };
    }

    private bool ApplyCultivationEnvironmentalDamage(Person p)
    {
        var place = state.Places[p.Place]; var metadata = state.Cultivation[p.BodyId];
        if (p.Realm >= Realm.Void && place.Turbidity >= Law("hazard.turbidity") && Law("hazard.structureDamage") > 0)
            ApplyCultivation(p, new DamageCultivation(CultivationActionId(p, "turbidity"), metadata.Revision,
                CultivationDamageCause.Turbidity, IntLaw("hazard.structureDamage") * 100));
        if (p.Status != LifeStatus.Alive) return true;
        metadata = state.Cultivation[p.BodyId];
        if (p.Realm >= Realm.Integration && (p.Health < 40 || p.Roots.Max() - p.Roots.Min() > 8000) && Law("hazard.bondDamage") > 0)
            ApplyCultivation(p, new DamageCultivation(CultivationActionId(p, "bond"), metadata.Revision,
                CultivationDamageCause.BondConflict, IntLaw("hazard.bondDamage") * 100));
        return p.Status != LifeStatus.Alive;
    }

    private CultivationPolicy CultivationPolicyForWorld() => CultivationPolicyForLaws(state.Laws, state.LawRevision);

    // Duration edits apply to future adopted structures/attempts. This explicit law-edit check never changes
    // earned work, layers, completion, formation history, reservations or RNG and never repairs corrupt saves.
    private void ReconcileCultivationLaws(IReadOnlyDictionary<string, long> beforeLaws)
    {
        var before = CultivationPolicyForLaws(beforeLaws, Math.Max(1, state.LawRevision - 1));
        foreach (var person in state.People)
            CultivationInvariants.Validate(state.Cultivation[person.BodyId], CultivationActorFor(person), before);
    }

    private static CultivationPolicy CultivationPolicyForLaws(IReadOnlyDictionary<string, long> laws, long revision) => new()
    {
        Revision = revision, EntryMonths = (int)laws["cultivation.entry"], QiMonths = (int)laws["cultivation.qi"],
        FoundationMonths = (int)laws["cultivation.foundation"], UpperDurationPercent = (int)laws["cultivation.upper"],
        BaseRisk = (int)laws["cultivation.breakthroughRisk"], TrialRisk = (int)laws["cultivation.breakthroughRisk"],
        MartialMonthsPerStage = (int)laws["martial.period"], TrialHealthCost = (int)laws["hazard.trialDamage"],
        QiLifespanMonths = laws["life.qi"], FoundationLifespanMonths = laws["life.foundation"], GoldenCoreLifespanMonths = laws["life.core"],
        HalfImmortalLifespanMonths = laws["founder.lifespan"], UpperLifespanPercent = (int)laws["life.upperMultiplier"]
    };

    private CultivationActor CultivationActorFor(Person p)
    {
        var profile = state.PeopleDomain.Profiles.FirstOrDefault(x => x.PersonId == p.Id);
        var assessment = profile is null ? null : Aptitudes.Assess(PeopleProfiles.Context(profile, state.PeopleDomain.Traits, Month,
            10000, 10000, [], state.PeopleDomain.Tuning));
        int Capability(int index)
        {
            if (assessment is null) return Catalog.Talent(p, index);
            var talent = assessment.Talents[index];
            // Preserve the neutral 50.00 throughput reference; learned/understood and actual active effects add once.
            // Health is applied once inside the process authority, so this assessment uses unconditioned capabilities.
            return Math.Clamp(talent.CurrentExpression + talent.Innate * (10000 - state.PeopleDomain.Tuning.TalentInnateWeight) / 10000, 0, 10000);
        }
        var formation = CultivationFormationAt(state.Places[p.Place], p);
        var assistance = 10000L + (long)formation.QiBonus * 100 + InventoryGrowthBonus(p);
        int Assisted(int index) => (int)Math.Clamp((long)Capability(index) * assistance / 10000, 0, 10000);
        return new()
        {
            PersonId = p.Id, BodyId = p.BodyId, SoulId = p.SoulId, Realm = p.Realm, Layer = p.Layer, GreatPerfection = p.GreatPerfection,
            Health = p.Health, Status = p.Status, MartialStage = p.Martial, InnateRoots = Elements.From(p.Roots),
            Absorption = Assisted(6), Refinement = Assisted(8), Circulation = Assisted(7), Assimilation = Assisted(8),
            Body = Capability(9), Mind = Capability(10)
        };
    }

    private CultivationContext CultivationContextFor(Person p)
    {
        var place = state.Places[p.Place];
        var formation = CultivationFormationAt(place, p);
        var political = new PoliticalContext(Month, Revision, state.LawRevision, state.People, state.Groups, state.Places,
            new() { MaturityMonths = Law("birth.maturity"), SectLimit = IntLaw("politics.sectLimit") });
        var knowledge = new List<KnowledgeEvidence>();
        foreach (var work in Catalog.Paths.Select(x => x.Id).Prepend("entry"))
        {
            var adopted = KnowledgeFor(p, work);
            if (adopted is null) continue;
            var memory = state.Knowledge.Memories.FindLast(m => m.PersonId == p.Id && m.WorkId == work && m.Chapter == 1);
            knowledge.Add(new(work, adopted.EditionId.ToString(CultureInfo.InvariantCulture), adopted.PersonalRevision,
                adopted.Comprehension * 100, adopted.Mastery * 100,
                adopted.Claim == CanonicalKnowledgeClaim(work) ? 0 : 8000,
                memory is { Decoding: > 0, Comprehension: > 0 }, CanConsultKnowledge(p, work, adopted.EditionId), KnowledgeSource(p, work)));
        }
        var quantum = CultivationSourceQuantum(); var yield = CultivationParcel(place, quantum);
        var opened = state.CultivationSupply.GetValueOrDefault(place.Id);
        var claimants = Math.Max(1, state.People.Count(q => q.Status == LifeStatus.Alive && q.Place == p.Place &&
            (q.Id == p.Id || (q.Due <= Month && q.Action is ActionKind.Cultivate or ActionKind.Breakthrough or ActionKind.Trial))));
        var occupied = state.People.Count(q => q.Id != p.Id && q.Status == LifeStatus.Alive && q.Place == p.Place &&
            q.Action is ActionKind.Cultivate or ActionKind.Breakthrough or ActionKind.Trial);
        var policy = CultivationPolicyForWorld();
        policy = policy with { BaseRisk = Math.Max(0, policy.BaseRisk - InventoryBreakthroughBonus(p)) };
        return new()
        {
            Month = Month, LawRevision = state.LawRevision, Policy = policy, Knowledge = knowledge,
            Site = new()
            {
                PlaceId = p.Place, Available = Law("cultivation.sourceCost") == 0 ? default : Elements.Map(yield, n => n * place.Qi) + opened,
                Claimants = claimants, HasUseRight = PoliticsEngine.CanUseCultivationSite(political, state.Politics, p.Id, p.Place),
                SeatCapacity = NaturalCultivationSeats, OccupiedSeats = occupied,
                HasReservedSeat = p.Action is ActionKind.Cultivate or ActionKind.Breakthrough or ActionKind.Trial,
                Turbidity = place.Turbidity * 100, FormationOperational = formation.Operational, FormationQuality = formation.Quality,
                CelestialWindow = Month % 12 == 0 && place.Turbidity < 50,
                ObservedRealmConditions = p.ObservedPlaces.ContainsKey(p.Place),
                RealPassageAccessible = place.Facilities.Contains("계역 통로") && place.BlockedUntil <= Month
            },
            Supports = CultivationSupportsFor(p)
        };
    }

    private CultivationSupport[] CultivationSupportsFor(Person p)
    {
        var attempt = state.Cultivation.GetValueOrDefault(p.BodyId)?.Attempt;
        var reserved = attempt is null || !long.TryParse(attempt.Id, CultureInfo.InvariantCulture, out var id) ? 0 : id;
        return Owned(p.Id).Where(i => Catalog.Items[i.Definition].Kind == ItemKind.Pill && i.Damage < 100 &&
            (i.Defects & (ItemDefect.Contamination | ItemDefect.LostCore)) == 0 &&
            (i.Place == -1 || i.Place == p.Place) && (i.Reserved == 0 || i.Reserved == reserved))
            .Select(i => new CultivationSupport(i.Id, "pill", i.Quantity, i.Quality, i.Reserved == 0 ? "" : attempt!.Id)).ToArray();
    }

    private string? CurrentCultivationPath(Person p, string requested)
    {
        var realm = p.Realm == Realm.Mortal ? Realm.Qi : p.Realm;
        if (requested.Length > 0)
        {
            var path = CultivationProfiles.Find(requested);
            if (path?.Definition.Realm != realm || (p.Realm != Realm.Mortal && KnowledgeFor(p, requested) is null)) return null;
            return p.Realm == Realm.Mortal && KnowledgeFor(p, "entry") is null && KnowledgeFor(p, requested) is null ? null : requested;
        }
        var active = state.Cultivation[p.BodyId].ActivePath;
        if (active.Length > 0 && KnowledgeFor(p, active) is not null) return active;
        if (p.Realm == Realm.Mortal && KnowledgeFor(p, "entry") is not null) return "path.1.0";
        return Catalog.Paths.FirstOrDefault(path => path.Realm == realm && KnowledgeFor(p, path.Id) is not null)?.Id;
    }
    private string? NextKnownCultivationPath(Person p) => p.Realm == Realm.HalfImmortal ? null :
        Catalog.Paths.FirstOrDefault(path => path.Realm == p.Realm + 1 && KnowledgeFor(p, path.Id) is not null)?.Id;

    private long CultivationSourceQuantum() => NeutralCultivationFlow / Math.Max(1, Law("cultivation.sourceCost"));
    private static Elements CultivationParcel(Place place, long quantum)
    {
        var weights = Enumerable.Repeat(1, 5).ToArray(); weights[place.Element] = 2;
        return Elements.Allocate(quantum, Elements.From(weights));
    }
    private void DebitCultivationSupply(Place place, Elements debit)
    {
        if (debit.Total == 0) return;
        var opened = state.CultivationSupply.GetValueOrDefault(place.Id); var parcel = CultivationParcel(place, CultivationSourceQuantum());
        long parcels = 0;
        for (var element = 0; element < 5; element++)
        {
            var needed = Math.Max(0, debit.At(element) - opened.At(element));
            if (needed > 0) parcels = Math.Max(parcels, (needed + parcel.At(element) - 1) / parcel.At(element));
        }
        if (parcels > place.Qi) throw new InvalidOperationException("Validated cultivation debit exceeds the real source.");
        place.Qi -= checked((int)parcels);
        opened += Elements.Map(parcel, amount => amount * parcels); opened -= debit;
        if (!opened.Nonnegative) throw new InvalidOperationException("Negative elemental cultivation source.");
        state.CultivationSupply[place.Id] = opened;
    }

    private CultivationInspection CultivationInspectionFor(Person p) =>
        CultivationQueries.Inspect(state.Cultivation[p.BodyId], CultivationActorFor(p), CultivationPolicyForWorld());
    private CultivationPotential CultivationPotentialFor(Person p) =>
        CultivationQueries.Potential(state.Cultivation[p.BodyId], CultivationActorFor(p), CultivationContextFor(p));

    private CultivationPotential CultivationPotentialFor(Person p, bool observer)
    {
        if (observer) return CultivationPotentialFor(p);
        var knowledge = new List<KnowledgeEvidence>();
        foreach (var work in Catalog.Paths.Select(x => x.Id).Prepend("entry"))
        {
            var adopted = KnowledgeFor(p, work);
            var memory = state.Knowledge.Memories.FindLast(m => m.PersonId == p.Id && m.WorkId == work && m.Chapter == 1);
            if (adopted is null || memory is not { Decoding: > 0, Comprehension: > 0 }) continue;
            knowledge.Add(new(work, adopted.EditionId.ToString(CultureInfo.InvariantCulture), adopted.PersonalRevision,
                adopted.Comprehension * 100, adopted.Mastery * 100, adopted.Belief == KnowledgeBelief.Contradicted ? 10000 : 0,
                true, false, memory.SourceId));
        }
        // These are explicit neutral assumptions, not a fresh read of hidden personal aptitudes or the site.
        var neutralActor = new CultivationActor { PersonId = p.Id, BodyId = p.BodyId, SoulId = p.SoulId,
            Realm = p.Realm, Layer = p.Layer, GreatPerfection = p.GreatPerfection, Health = p.Health,
            Status = p.Status, MartialStage = p.Martial };
        var context = new CultivationContext { Month = Month, Knowledge = knowledge };
        var potential = CultivationQueries.Potential(state.Cultivation[p.BodyId], neutralActor, context);
        string[] externalChecks = ["right", "seat", "formation-site", "window", "self-control"];
        return potential with
        {
            HasActualRecoverySupply = false,
            KnownNextRoutes = potential.KnownNextRoutes.Select(route => route with
            {
                Missing = [.. route.Missing.Where(b => !externalChecks.Contains(b.Code, StringComparer.Ordinal)),
                    new("unknown-site", "현재 공급·자리·사용권·형성 환경은 확인 전 미상", "실제 현장 관측·권리 확인·준비 검사")],
                Condition = route.Condition + "; 기본 법칙/중립 수용 가정에 따른 조건부 계획"
            }).ToArray(),
            InformationBasis = "실제 자기 기억과 완료한 구조만 사용; 현재 환경·권한·숨은 자질은 미상, 중립 기본법칙을 가정"
        };
    }

    private CombatProjection CultivationCombatFor(Person p) => CultivationCombatFor(p, true);

    private CombatProjection CultivationCombatFor(Person p, bool observer)
    {
        var metadata = state.Cultivation[p.BodyId]; var effects = new List<CombatSourceEffect>();
        void Skill(string id, int value, params int[] axes)
        {
            if (value <= 0) return;
            var multipliers = Enumerable.Repeat(10000L, 8).ToArray(); foreach (var axis in axes) multipliers[axis] += value / 4;
            effects.Add(new(id, metadata.Revision, default, CombatAxes.From(multipliers)));
        }
        var practice = metadata.ActivePath.Length == 0 ? null : KnowledgeFor(p, metadata.ActivePath);
        if (practice is not null) Skill($"practice:{p.BodyId}:{metadata.ActivePath}", practice.Mastery * 100, 0, 2);
        Skill($"combat-experience:{p.BodyId}", p.Skills.GetValueOrDefault("combat"), 2, 4, 6);
        Skill($"spell-practice:{p.BodyId}", p.Skills.GetValueOrDefault("spell"), 0, 2, 5);
        foreach (var itemId in p.Equipment.Values.Distinct().Order())
        {
            if (!items.TryGetValue(itemId, out var item) || item.Owner != p.Id || item.Reserved != 0 || item.Damage >= 100 ||
                (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) != 0 || (item.BoundSoul != 0 && item.BoundSoul != p.SoulId)) continue;
            var axes = p.Equipment.Where(e => e.Value == itemId).Select(e => e.Key switch
            { 0 or 1 => 0, 2 or 3 or 7 => 3, 4 => 5, 5 => 4, 6 => 2, _ => -1 }).Where(axis => axis >= 0).Distinct().ToArray();
            var multipliers = Enumerable.Repeat(10000L, 8).ToArray(); foreach (var axis in axes) multipliers[axis] += item.Quality * (100 - item.Damage) / 400;
            effects.Add(new($"item:{item.Id}", metadata.Revision, default, CombatAxes.From(multipliers)));
        }
        var traitEffects = observer ? PeopleEffects.Resolve(Traits.ActiveEffects(state.PeopleDomain.Traits, p.Id, Month)
            .Where(e => e.Target.Domain == EffectDomain.Combat)).Applied : [];
        foreach (var cause in traitEffects.GroupBy(e => e.Source.CauseId, StringComparer.Ordinal))
        {
            if (effects.Any(e => e.SourceId == cause.Key)) continue;
            var add = new long[8]; var multiply = Enumerable.Repeat(10000L, 8).ToArray();
            foreach (var effect in cause)
                if (effect.Operation == EffectOperation.Add) add[effect.Target.Index] += effect.Amount;
                else multiply[effect.Target.Index] = multiply[effect.Target.Index] * (10000 + effect.Amount) / 10000;
            effects.Add(new(cause.Key, cause.Max(e => e.Source.Revision), CombatAxes.From(add), CombatAxes.From(multiply)));
        }
        if (!observer)
            return CultivationQueries.Combat(metadata,
                new() { PersonId = p.Id, BodyId = p.BodyId, SoulId = p.SoulId, Realm = p.Realm, Layer = p.Layer,
                    GreatPerfection = p.GreatPerfection, Health = p.Health, Status = p.Status, MartialStage = p.Martial },
                new(), new() { Name = "자기 기억·현재 구조/장비·중립 기준전·기본법칙 가정" }, effects);
        var formation = CultivationFormationAt(state.Places[p.Place], p);
        var activeFrames = formation.Causes.ToHashSet(StringComparer.Ordinal);
        var defenses = state.Inventory.Effects.Where(e => e.Kind == "defense.place" && e.Start <= Month && Month < e.End)
            .GroupBy(e => e.Cause, StringComparer.Ordinal).Select(g => g.OrderByDescending(e => e.Start).First());
        foreach (var defense in defenses)
        {
            var array = state.Inventory.Arrays.FirstOrDefault(a => defense.Cause.StartsWith($"array:{a.Id}:", StringComparison.Ordinal));
            if (array is null || !activeFrames.Contains($"item:{array.Item}") || !items.TryGetValue(array.Item, out var frame) ||
                frame.Damage >= 100 || (frame.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) != 0) continue;
            var cause = $"item:{frame.Id}";
            if (effects.Any(e => e.SourceId == cause)) continue;
            effects.Add(new(cause, defense.Start, default,
                new(10000, 10000, 10000, 10000 + defense.Magnitude, 10000, 10000, 10000, 10000)));
        }
        var site = state.Places[p.Place];
        return CultivationQueries.Combat(metadata, CultivationActorFor(p), CultivationPolicyForWorld(), new()
        {
            Name = "현재 장소·단독·중거리·1개월", Moving = p.Action == ActionKind.Travel, PreparedFormation = formation.Operational,
            LowSupply = site.Qi == 0 && state.CultivationSupply.GetValueOrDefault(p.Place).Total == 0,
            CelestialWindow = Month % 12 == 0 && site.Turbidity < 50,
            ObservedRealmConditions = p.ObservedPlaces.ContainsKey(p.Place), AccessiblePassage = site.Facilities.Contains("계역 통로") && site.BlockedUntil <= Month
        }, effects);
    }

    private static void MirrorCultivation(Person p, CultivationState metadata)
    {
        p.Work = 0; p.MartialWork = 0;
        p.Irreversible = metadata.Attempt?.Phase == AttemptPhase.Fractured;
        p.PendingLayer = metadata.PendingTrial?.TargetLayer ?? 0;
        p.StructureDamage = metadata.Damage / 100; p.BondStability = 100 - metadata.Damage / 100;
        if (metadata.Structure is GoldenCoreStructure && metadata.Formations.Count > 0)
            p.Components = metadata.Formations[^1].Factors.Select(f => f.Value).ToArray();
        else p.Components = new int[7];
    }
    private static void ClearCultivationAction(Person p)
    { p.Action = ActionKind.Idle; p.Due = 0; p.ActionTarget = 0; p.ActionData = ""; }
    private string CultivationActionId(Person p, string phase) => $"cultivation:{p.BodyId}:{Month}:{state.Cultivation[p.BodyId].Revision}:{phase}";
    private static string CultivationFailure(CultivationResult result) => string.Join("; ", result.Blockers.Select(b => b.Detail + "·" + b.Remedy));
    private static int BoundFlow(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
}
