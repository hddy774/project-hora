using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private const string KnowledgeActionPrefix = "knowledge:";
    private static readonly Dictionary<string, string> KnowledgeCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["study"] = "study", ["teach"] = "teach", ["requestTeaching"] = "requestTeaching",
        ["transcribe"] = "transcribe", ["experiment"] = "experiment", ["correctKnowledge"] = "correctKnowledge"
    };
    private KnowledgeAuthority? knowledgeAuthority;
    private KnowledgeState? knowledgeSnapshot;
    private sealed record KnowledgeWorldAction(long Activity, long Body, long PartnerBody, string Mode,
        string Work, long SourceItem = 0, long TargetItem = 0, long Reservation = 0, string Recipe = "", string Substrate = "paper");

    private KnowledgeAuthority KnowledgeRuntime()
    {
        if (knowledgeAuthority is null || !ReferenceEquals(knowledgeSnapshot, state.Knowledge))
        {
            knowledgeAuthority = KnowledgeAuthority.Restore(state.Knowledge);
            knowledgeSnapshot = state.Knowledge;
        }
        return knowledgeAuthority;
    }

    private void SaveKnowledge()
    {
        state.Knowledge = KnowledgeRuntime().Checkpoint();
        knowledgeSnapshot = state.Knowledge;
    }

    private void InitializeKnowledge()
    {
        if (state.Knowledge.Works.Count > 0) return;
        if (Month != 0 || state.People.Count != 2) throw new InvalidDataException("지식 창세는 두 시조의 실제 초기 상태에서만 실행합니다.");
        state.Knowledge = new() { Rules = CurrentKnowledgeRules() };
        knowledgeAuthority = null;
        var authority = KnowledgeRuntime();
        var founders = state.People.OrderBy(p => p.Id).ToArray();
        var definitions = new List<(string Id, string Title, string Skill, KnowledgeKind Kind)> { ("entry", "입문 원리", "entry", KnowledgeKind.Cultivation) };
        definitions.AddRange(Catalog.Paths.Select(p => (p.Id, p.Book, p.Id, KnowledgeKind.Cultivation)));
        definitions.AddRange(InventoryDefinitions.Recipes.Values.OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => ("recipe:" + r.Id, InventoryDefinitions.Definition(r.Output).Item.Name + " 공정", r.Skill, KnowledgeKind.Craft)));
        foreach (var definition in definitions)
        {
            CheckKnowledge(authority.DefineWork(new(definition.Id, definition.Title, definition.Skill, definition.Kind,
                [new(1, "핵심 원리", 100, 100), new(2, "세부 운용", 100, 100), new(3, "주해", 100, 100)], 0,
                "genesis:work:" + definition.Id)));
            var edition = CheckKnowledge(authority.CreateEdition(definition.Id, 0, EditionRelation.Original, founders[0].Id,
                [new(1, CanonicalKnowledgeClaim(definition.Id)), new(2, definition.Title + "의 세부 운용 기록"), new(3, definition.Title + "의 주해")],
                0, "genesis:edition:" + definition.Id));
            foreach (var founder in founders)
            {
                var comprehension = definition.Id == "entry" ? 100 : definition.Kind == KnowledgeKind.Craft ? 15 : 30;
                CheckKnowledge(authority.SeedInitialKnowledge(founder.Id, edition, [1], comprehension,
                    $"genesis:person:{founder.Id}:{definition.Id}", canTransmit: true));
            }
            foreach (var book in state.Items.Where(i => i.Definition == definition.Id && i.Source == "창세 초기 지식 매체").OrderBy(i => i.Id))
                CheckKnowledge(authority.BindMedium(book.Id, edition, book.Owner, KnowledgeItemPlace(book), KnowledgeMediumKind.Book,
                    KnowledgeQuality(book.Quality), KnowledgeRights.Read | KnowledgeRights.Copy | KnowledgeRights.Teach,
                    book.Defects.HasFlag(ItemDefect.MissingPages) ? [3] : [], KnowledgeUnreadable(book), 0, "genesis:medium:" + book.Id));
        }
        SaveKnowledge();
        foreach (var founder in founders) MirrorKnowledge(founder);
    }

    private KnowledgeRules CurrentKnowledgeRules() => new()
    {
        Revision = state.LawRevision,
        TranscriptionMonthsPerChapter = (int)state.Laws.GetValueOrDefault("knowledge.copyMonths", 1),
        PaperPerChapter = (int)state.Laws.GetValueOrDefault("knowledge.copyPaper", 1),
        InkPerChapter = (int)state.Laws.GetValueOrDefault("knowledge.copyInk", 1),
        TeachingMinimumComprehension = (int)state.Laws.GetValueOrDefault("knowledge.teachMinimum", 1),
        PracticeMinimumComprehension = (int)state.Laws.GetValueOrDefault("knowledge.practiceMinimum", 1),
        MaximumActionMonths = (int)state.Laws.GetValueOrDefault("knowledge.maximumActionMonths", 120)
    };

    private bool TryKnowledgeCommand(GameCommand command, Person person, out string? error)
    {
        error = null;
        if (!KnowledgeCommands.TryGetValue(command.Kind, out var kind)) return false;
        command = command with { Kind = kind };
        if (state.Knowledge.Works.Count == 0) { error = "지식 권위가 초기화되지 않았습니다."; return true; }
        SettleKnowledge();
        if (command.Kind == "requestTeaching")
        {
            var work = ResolveKnowledgeWork(command.Data, person);
            if (person.Action != ActionKind.Idle || !people.TryGetValue(command.Target, out var teacher) || teacher.Id == person.Id ||
                !person.KnownPeople.Contains(teacher.Id) || teacher.Status != LifeStatus.Alive || teacher.Place != person.Place || work is null)
                error = "실제 대화 가능한 스승과 학습 대상을 확인하세요.";
            else if (teacher.LessonRequests.Count >= Law("ai.planBudget") || teacher.LessonRequests.Any(r => r.Pupil == person.Id && r.Knowledge == work && r.Expires > Month))
                error = "이미 요청을 기다리거나 상대의 요청 용량이 가득 찼습니다.";
            else
            {
                teacher.LessonRequests.Add(new(person.Id, work, Month, Month + 12));
                person.Action = ActionKind.Wait; person.Due = Month + 1;
                Event("배움 요청", person.Id, teacher.Id, $"{person.Name} {teacher.Name}에게 실제 배움을 요청했다.", command.Id);
            }
            return true;
        }
        if (person.Action != ActionKind.Idle || person.Health < 10 || person.Sealed)
        { error = "기존 노동·건강·봉인 상태를 확인하세요."; return true; }
        var months = command.Value == 0 ? 1 : command.Value;
        if (months < 1 || months > KnowledgeRuntime().Rules.MaximumActionMonths) { error = "현재 법칙의 학습 기간 범위를 확인하세요."; return true; }
        switch (command.Kind)
        {
            case "study":
            case "correctKnowledge":
                error = StartWorldStudy(command, person, (int)months); break;
            case "teach": error = StartWorldTeaching(command, person, (int)months); break;
            case "transcribe": error = StartWorldTranscription(command, person); break;
            case "experiment": error = StartWorldExperiment(command, person, (int)months); break;
        }
        return true;
    }

    private bool TryStartKnowledge(Person person, string kind, long target, string data, out string? error)
    {
        if (!KnowledgeCommands.ContainsKey(kind)) { error = null; return false; }
        var source = KnowledgeActionSource(person, "ai-" + kind);
        return TryKnowledgeCommand(new(source, person.Id, kind, target, Data: data), person, out error);
    }

    private string? StartWorldStudy(GameCommand command, Person person, int months)
    {
        if (!CanReadKnowledgeItem(person, command.Target)) return "실제로 접근 가능한 읽을 수 있는 서적이 필요합니다.";
        var medium = state.Knowledge.Media.Find(m => m.ItemId == command.Target && !m.Destroyed);
        if (medium is null) return "이 실물의 판본 출처가 없습니다.";
        var work = state.Knowledge.Editions.Single(e => e.Id == medium.EditionId).WorkId;
        if (command.Kind == "correctKnowledge" && KnowledgeFor(person, work)?.Belief != KnowledgeBelief.Contradicted)
            return "실제 검증으로 반박된 개인 지식과 정정 매체를 선택하세요.";
        var result = KnowledgeRuntime().StartStudy(new("command:" + command.Id, KnowledgeParticipantFor(person), command.Target, [1], Month, months));
        if (!result.Applied) return result.Message;
        var action = new KnowledgeWorldAction(result.Id, person.BodyId, 0, command.Kind, work, command.Target);
        SetKnowledgeAction(person, ActionKind.Study, action);
        SaveKnowledge(); return null;
    }

    private string? StartWorldTeaching(GameCommand command, Person teacher, int months)
    {
        var work = ResolveKnowledgeWork(command.Data, teacher);
        if (work is null || !people.TryGetValue(command.Target, out var pupil) || pupil.Status != LifeStatus.Alive ||
            !teacher.KnownPeople.Contains(pupil.Id) || !ShareExplorationSpace(pupil, teacher) || pupil.Health < 10 ||
            pupil.Action != ActionKind.Idle && (pupil.Action != ActionKind.Wait || pupil.ActionData.StartsWith(KnowledgeActionPrefix, StringComparison.Ordinal)))
            return "실제 지식·같은 장소·상대의 노동 용량을 확인하세요.";
        var transmission = pupil.ParentOne == teacher.Id || pupil.ParentTwo == teacher.Id || pupil.Guardian == teacher.Id ? KnowledgeTransmission.Parent :
            teacher.Sect != 0 && teacher.Sect == pupil.Sect ? KnowledgeTransmission.Apprenticeship : KnowledgeTransmission.Mentor;
        var result = KnowledgeRuntime().StartTeaching(new("command:" + command.Id, KnowledgeParticipantFor(teacher),
            KnowledgeParticipantFor(pupil), work, [1], transmission, Month, months));
        if (!result.Applied) return result.Message;
        SetKnowledgeAction(teacher, ActionKind.Teach, new(result.Id, teacher.BodyId, pupil.BodyId, "teach", work));
        SetKnowledgeAction(pupil, ActionKind.Wait, new(result.Id, pupil.BodyId, teacher.BodyId, "learn", work));
        teacher.LessonRequests.RemoveAll(r => r.Pupil == pupil.Id && (r.Knowledge == work || r.Knowledge == command.Data));
        SaveKnowledge(); return null;
    }

    private string? StartWorldTranscription(GameCommand command, Person person)
    {
        if (!CanReadKnowledgeItem(person, command.Target)) return "필사할 실제 서적을 선택하세요.";
        var medium = state.Knowledge.Media.Find(m => m.ItemId == command.Target && !m.Destroyed);
        if (medium is null) return "원본 판본을 확인할 수 없습니다.";
        var edition = state.Knowledge.Editions.Single(e => e.Id == medium.EditionId);
        var cost = KnowledgeRuntime().QuoteTranscription(KnowledgeMediumKind.Book, 1);
        if (Inventory(person.Id, "paper") < cost.Paper || Inventory(person.Id, "ink") < cost.Ink) return "실제 종이와 먹이 부족합니다.";
        var paperSource = Owned(person.Id).Where(i => i.Definition == "paper" && i.Reserved == 0 &&
            (i.Place == -1 || i.Place == person.Place)).OrderBy(i => i.Id).First();
        Item? blankSource;
        if (command.Data.Length > 0)
        {
            if (!long.TryParse(command.Data, NumberStyles.None, CultureInfo.InvariantCulture, out var specified) ||
                !items.TryGetValue(specified, out blankSource) || blankSource.Owner != person.Id || blankSource.Definition != "blankBook" ||
                blankSource.Reserved != 0 || blankSource.Quantity != 1 || KnowledgeItemPlace(blankSource) != person.Place)
                return "실제로 소유하고 접근할 수 있는 빈 기록책을 선택하세요.";
        }
        else blankSource = Owned(person.Id).Where(i => i.Definition == "blankBook" && i.Quantity == 1 && i.Reserved == 0 &&
            KnowledgeItemPlace(i) == person.Place).OrderBy(i => i.Id).FirstOrDefault() ?? paperSource;
        var inkSource = Owned(person.Id).Where(i => i.Definition == "ink" && i.Reserved == 0 &&
            (i.Place == -1 || i.Place == person.Place)).OrderBy(i => i.Id).First();
        var request = new TranscribeKnowledgeRequest("command:" + command.Id, KnowledgeParticipantFor(person), command.Target, [1], state.NextId,
            KnowledgeMediumKind.Book, KnowledgeQuality(blankSource.Quality),
            new("writing:" + command.Id, state.NextId, paperSource.Id, cost.Paper, inkSource.Id, cost.Ink), Month);
        var check = KnowledgeRuntime().PreflightTranscription(request);
        if (!check.Applied) return check.Message;
        var error = ReserveInventoryPhysical(person, blankSource.Id, 1, "knowledge-blank", out var reservation, out var blankId);
        if (error is not null) return error;
        var writing = new Dictionary<string, int>(StringComparer.Ordinal) { ["ink"] = cost.Ink };
        var additionalPaper = cost.Paper - (blankSource.Definition == "paper" ? 1 : 0);
        if (additionalPaper > 0) writing.Add("paper", additionalPaper);
        error = ReserveInventoryMaterials(person, writing, "knowledge-writing-inputs", out var writingReservation);
        if (error is not null) { ReleaseInventoryReservation(person, reservation); return error; }
        var binding = state.Items.FirstOrDefault(i => i.Reserved == writingReservation && i.BoundSoul > 0)?.BoundSoul ?? 0;
        if (binding > 0) items[blankId].BoundSoul = binding;
        error = ConsumeInventoryReservation(person, writingReservation);
        if (error is not null) throw new InvalidOperationException(error);
        request = request with { TargetItemId = blankId, Payment = request.Payment with { BlankMediumItemId = blankId } };
        var result = KnowledgeRuntime().StartTranscription(request);
        if (!result.Applied) throw new InvalidOperationException(result.Message);
        SetKnowledgeAction(person, ActionKind.Craft, new(result.Id, person.BodyId, 0, "transcribe", edition.WorkId,
            command.Target, blankId, reservation, Substrate: blankSource.Definition));
        SaveKnowledge(); return null;
    }

    private string? StartWorldExperiment(GameCommand command, Person person, int months)
    {
        var work = command.Target > 0 ? state.Knowledge.Media.Find(m => m.ItemId == command.Target && !m.Destroyed) is { } medium ?
            state.Knowledge.Editions.Single(e => e.Id == medium.EditionId).WorkId : null : ResolveKnowledgeWork(command.Data, person);
        if (work is null || command.Target > 0 && !CanReadKnowledgeItem(person, command.Target)) return "실제로 아는 원리와 실험 대상을 선택하세요.";
        var recipeId = work.StartsWith("recipe:", StringComparison.Ordinal) ? work[7..] : "";
        IReadOnlyDictionary<string, int> inputs = recipeId.Length > 0 ? InventoryDefinitions.Recipes[recipeId].Inputs :
            new Dictionary<string, int>(StringComparer.Ordinal) { ["paper"] = 1, ["ink"] = 1 };
        if (inputs.Any(input => Inventory(person.Id, input.Key) < input.Value)) return "실험할 실제 재료가 부족합니다.";
        var error = BeginKnowledgePractice(person, work, months, "command:" + command.Id, out var activity);
        if (error is not null) return error;
        error = ReserveInventoryMaterials(person, inputs, "knowledge-experiment", out var reservation);
        if (error is not null) { CancelKnowledgePractice(person, activity, "reservation-failed:" + command.Id); return error; }
        SetKnowledgeAction(person, ActionKind.Craft, new(activity, person.BodyId, 0, "experiment", work,
            command.Target, Reservation: reservation, Recipe: recipeId));
        SaveKnowledge(); return null;
    }

    private bool CompleteKnowledge(Person person)
    {
        if (!TryReadKnowledgeAction(person, out var context)) return false;
        SettleKnowledge();
        var activity = KnowledgeRuntime().FindActivity(context.Activity);
        if (activity is null || activity.Status != KnowledgeActivityStatus.Active) { ClearKnowledgeParticipants(context.Activity); return true; }
        if (Month < activity.DueMonth) return true;
        var participants = activity.TeacherId > 0 ? new[] { activity.ActorId, activity.TeacherId } : [activity.ActorId];
        if (participants.Any(id => !people.TryGetValue(id, out var participant) || participant.Status != LifeStatus.Alive ||
            participant.Health < 10 || participant.Place != activity.Place || !TryReadKnowledgeAction(participant, out var saved) ||
            saved.Activity != activity.Id || saved.Body != participant.BodyId || saved.PartnerBody > 0 &&
            !participants.Any(other => other != id && people[other].BodyId == saved.PartnerBody)))
        { CancelKnowledge(person); return true; }
        if (context.SourceItem > 0 && !CanReadKnowledgeItem(people[activity.ActorId], context.SourceItem))
        { CancelKnowledge(person); return true; }
        var actor = people[activity.ActorId];
        var beforeLearning = KnowledgeFor(actor, activity.WorkId);
        KnowledgePracticeOutcome? outcome = null;
        if (context.Mode == "experiment")
        {
            var actual = state.Items.Where(i => i.Reserved == context.Reservation && i.Owner == actor.Id).ToArray();
            if (actual.Length == 0) { CancelKnowledge(person); return true; }
            var quality = (int)(actual.Sum(i => (long)i.Quality * i.Quantity) / actual.Sum(i => (long)i.Quantity));
            var hasEnergy = context.Recipe.Length > 0 || state.Places[actor.Place].Qi > 0;
            var matches = activity.Adopted[0].Claim == CanonicalKnowledgeClaim(context.Work);
            var error = ConsumeInventoryReservation(actor, context.Reservation);
            if (error is not null) throw new InvalidOperationException(error);
            if (context.Recipe.Length == 0 && hasEnergy) state.Places[actor.Place].Qi--;
            if (context.Recipe.Length > 0 && matches)
            {
                var recipe = InventoryDefinitions.Recipes[context.Recipe];
                var proficiency = activity.Adopted[0].Mastery * 100;
                var resultQuality = Math.Clamp((quality + proficiency + Catalog.Talent(actor, recipe.Skill == "alchemy" ? 14 : 15)) / 3 + Roll(401) - 200, 0, 10000);
                var product = AddItem(actor.Id, recipe.Output, recipe.Quantity, resultQuality, $"실제 지식 실험 {activity.Id}·육신 {actor.BodyId}");
                if (product is not null)
                {
                    product.BoundSoul = actual.FirstOrDefault(i => i.BoundSoul > 0)?.BoundSoul ?? 0;
                    var metadata = state.Inventory.Metadata.Find(m => m.Item == product.Id);
                    if (metadata is null) state.Inventory.Metadata.Add(metadata = new() { Item = product.Id, Custodian = actor.Id });
                    metadata.Provenance.AddRange(actual.Select(i => i.Source)); metadata.Provenance.Add(product.Source);
                }
            }
            var test = !hasEnergy ? KnowledgeTestResult.None : matches ? KnowledgeTestResult.SupportsClaim : KnowledgeTestResult.RefutesClaim;
            outcome = new($"process:{actor.BodyId}:{activity.Id}", actor.Id, activity.Adopted[0].PersonalRevision,
                true, hasEnergy && matches, test, !hasEnergy ? "실제 원천 부족으로 검증 불가" : matches ? "채택한 순서로 실제 공정이 성립함" : "채택한 순서에서 실제 공정이 성립하지 않음");
        }
        if (context.Mode == "transcribe" && (!items.TryGetValue(context.TargetItem, out var blank) || blank.Owner != actor.Id ||
            context.Substrate is not ("paper" or "blankBook") || blank.Definition != context.Substrate || blank.Quantity != 1 || blank.Reserved != context.Reservation))
        { CancelKnowledge(person); return true; }
        KnowledgeInterpretation[]? interpretations = null;
        if (activity.Kind == KnowledgeActivityKind.Study && KnowledgeFor(actor, activity.WorkId) is null &&
            Roll(10000) < state.Laws.GetValueOrDefault("knowledge.misreadChance", 100))
            interpretations = [new(1, activity.Claims[0].Claim + " 순서를 거꾸로 적용한다.", 60)];
        var result = KnowledgeRuntime().Complete(new($"complete:{actor.BodyId}:{activity.Id}", activity.Id, Month,
            participants.Select(id => KnowledgeParticipantFor(people[id])).ToArray(), outcome, interpretations));
        if (!result.Applied) { CancelKnowledge(person); return true; }
        if (context.Mode == "transcribe")
        {
            var error = ReleaseInventoryReservation(actor, context.Reservation);
            if (error is not null) throw new InvalidOperationException(error);
            var target = items[context.TargetItem];
            Material(context.Substrate).Consumption++; Material(context.Work).Production++;
            target.Definition = context.Work; target.Source = $"실제 필사 {activity.Id}·원본 {context.SourceItem}·육신 {actor.BodyId}";
            state.Inventory.Metadata.Find(m => m.Item == target.Id)?.Provenance.Add(target.Source);
            Event("제작", actor.Id, target.Id, $"{Catalog.Items[target.Definition].Name} 필사 완료", target.Source);
        }
        SaveKnowledge();
        foreach (var id in participants) MirrorKnowledge(people[id]);
        var afterLearning = KnowledgeFor(actor, activity.WorkId);
        if (afterLearning is not null)
        {
            var skill = state.Knowledge.Works.Single(w => w.Id == activity.WorkId).SkillId;
            RecordPeopleLearning(actor, skill, (afterLearning.Mastery - (beforeLearning?.Mastery ?? 0)) * 100,
                (afterLearning.Comprehension - (beforeLearning?.Comprehension ?? 0)) * 100,
                $"knowledge-learning:{actor.BodyId}:{activity.Id}", checked((int)(activity.DueMonth - activity.StartedMonth)),
                activity.Kind == KnowledgeActivityKind.Teaching ? PeopleLearningKind.Teaching :
                    activity.Kind == KnowledgeActivityKind.Practice ? PeopleLearningKind.Experiment : PeopleLearningKind.Reflection,
                afterLearning.Belief != KnowledgeBelief.Supported);
        }
        Remember(actor, "knowledge:last", context.Mode, $"실제 지식 행동 {activity.Id}·개인 revision 보존", 100);
        if (activity.TeacherId > 0)
        {
            var teacher = people[activity.TeacherId];
            var progress = KnowledgeFor(actor, activity.WorkId)?.Comprehension ?? 0;
            Remember(teacher, $"pupil:{actor.Id}:{activity.WorkId}", progress.ToString(CultureInfo.InvariantCulture), "실제 수업에서 확인한 진척", 100);
            Remember(actor, $"teaching:{teacher.Id}:{activity.WorkId}", progress.ToString(CultureInfo.InvariantCulture), "실제 수업·개인 판본 출처", 80);
        }
        ClearKnowledgeParticipants(activity.Id);
        return true;
    }

    private bool CancelKnowledge(Person person)
    {
        if (!TryReadKnowledgeAction(person, out var context)) return false;
        var activity = KnowledgeRuntime().FindActivity(context.Activity);
        if (activity?.Status == KnowledgeActivityStatus.Active)
            CheckKnowledge(KnowledgeRuntime().Cancel(activity.Id, Month, $"cancel:{person.BodyId}:{activity.Id}"));
        if (context.Reservation > 0 && state.Inventory.Reservations.Any(r => r.Id == context.Reservation && r.Active))
            ReleaseInventoryReservation(person, context.Reservation);
        ClearKnowledgeParticipants(context.Activity); SaveKnowledge(); return true;
    }

    private void SettleKnowledge()
    {
        if (state.Knowledge.Works.Count == 0) return;
        var authority = KnowledgeRuntime();
        var before = authority.Revision;
        if (authority.Rules.Revision < state.LawRevision)
            CheckKnowledge(authority.ChangeRules(CurrentKnowledgeRules(), Month, "knowledge-laws:" + state.LawRevision));
        foreach (var medium in authority.Checkpoint().Media)
        {
            var exists = items.TryGetValue(medium.ItemId, out var actual);
            if (medium.Destroyed) continue;
            var owner = exists ? actual!.Owner : medium.OwnerId;
            var place = exists ? KnowledgeItemPlace(actual!) : medium.Place;
            var quality = exists ? KnowledgeQuality(actual!.Quality) : medium.Quality;
            var unreadable = !exists || KnowledgeUnreadable(actual!);
            var lastChapter = state.Knowledge.Editions.Single(e => e.Id == medium.EditionId).Chapters.Max(c => c.Number);
            var missing = exists && actual!.Defects.HasFlag(ItemDefect.MissingPages) ? medium.MissingChapters.Length > 0 ? medium.MissingChapters : [lastChapter] : Array.Empty<int>();
            if (owner == medium.OwnerId && place == medium.Place && quality == medium.Quality && unreadable == medium.Unreadable &&
                exists && missing.SequenceEqual(medium.MissingChapters)) continue;
            CheckKnowledge(authority.SynchronizeMedium(medium.ItemId, owner, place, quality, missing, unreadable, !exists, Month,
                $"physical:{medium.ItemId}:{Month}:{medium.Revision}"));
        }
        var snapshot = authority.Checkpoint();
        foreach (var medium in snapshot.Media.Where(m => !m.Destroyed && items.ContainsKey(m.ItemId)))
        {
            var actual = items[medium.ItemId];
            var users = state.Inventory.Metadata.Find(m => m.Item == actual.Id)?.UseRights ?? [];
            foreach (var user in users.Where(id => id != actual.Owner))
                if (!snapshot.Access.Any(a => a.ItemId == actual.Id && a.PersonId == user && !a.Revoked && a.ExpiresMonth > Month))
                    CheckKnowledge(authority.GrantAccess(actual.Id, actual.Owner, user, KnowledgeRights.Read, long.MaxValue, Month,
                        $"inventory-read:{actual.Id}:{user}:{Month}:{authority.Revision}"));
            foreach (var user in snapshot.Access.Where(a => a.ItemId == actual.Id && !a.Revoked && a.ExpiresMonth > Month &&
                a.PersonId != actual.Owner && !users.Contains(a.PersonId)).Select(a => a.PersonId).Distinct())
                CheckKnowledge(authority.RevokeAccess(actual.Id, actual.Owner, user, Month,
                    $"inventory-revoke:{actual.Id}:{user}:{Month}:{authority.Revision}"));
        }
        if (authority.Revision != before) SaveKnowledge();
        foreach (var person in state.People.Where(p => p.ActionData.StartsWith(KnowledgeActionPrefix, StringComparison.Ordinal)).ToArray())
        {
            if (!TryReadKnowledgeAction(person, out var context)) continue;
            var activity = authority.FindActivity(context.Activity);
            if (activity?.Status == KnowledgeActivityStatus.Active && (person.Status != LifeStatus.Alive || person.BodyId != context.Body ||
                person.Place != activity.Place || person.Health < 10)) CancelKnowledge(person);
        }
    }

    private string? BeginKnowledgePractice(Person person, string workId, int months, string sourceId, out long activityId)
    {
        activityId = 0;
        var adoption = KnowledgeFor(person, workId);
        if (adoption is null) return "실제로 습득한 공정 지식이 없습니다.";
        var result = KnowledgeRuntime().StartPractice(new(sourceId, KnowledgeParticipantFor(person), workId, 1,
            adoption.PersonalRevision, Month, months));
        if (!result.Applied) return result.Message;
        activityId = result.Id; SaveKnowledge(); return null;
    }

    private string? FinishKnowledgePractice(Person person, long activityId, bool successful, string processReceipt, string observation)
    {
        var activity = KnowledgeRuntime().FindActivity(activityId);
        if (activity is null || activity.Kind != KnowledgeActivityKind.Practice || activity.ActorId != person.Id) return "채택한 지식 실습이 없습니다.";
        var result = KnowledgeRuntime().Complete(new("finish:" + processReceipt, activityId, Month, [KnowledgeParticipantFor(person)],
            new(processReceipt, person.Id, activity.Adopted[0].PersonalRevision, true, successful, KnowledgeTestResult.None, observation)));
        if (!result.Applied) return result.Message;
        SaveKnowledge(); MirrorKnowledge(person); return null;
    }

    private void CancelKnowledgePractice(Person person, long activityId, string sourceId)
    {
        if (activityId <= 0) return;
        var activity = KnowledgeRuntime().FindActivity(activityId);
        if (activity?.Status == KnowledgeActivityStatus.Active && activity.ActorId == person.Id)
        { CheckKnowledge(KnowledgeRuntime().Cancel(activityId, Month, sourceId)); SaveKnowledge(); }
    }

    private long BeginCultivationKnowledgePractice(Person person, string work)
    {
        var error = BeginKnowledgePractice(person, work, 1, KnowledgeActionSource(person, "cultivation"), out var activity);
        if (error is not null) person.Reason = error;
        return error is null ? activity : 0;
    }

    private void FinishCultivationKnowledgePractice(Person person, long activity, bool successful, string receipt, string observation)
    {
        if (activity <= 0) return;
        var error = FinishKnowledgePractice(person, activity, successful, receipt, observation);
        if (error is not null) { person.Reason = error; CancelKnowledgePractice(person, activity, "failed:" + receipt); }
    }

    partial void BeginInventoryPractice(Person p, string workId, int months, string sourceId, ref long activityId, ref string? error) =>
        error = BeginKnowledgePractice(p, workId, months, sourceId, out activityId);
    partial void FinishInventoryPractice(Person p, long activityId, bool successful, string receipt, string observation, ref string? error) =>
        error = FinishKnowledgePractice(p, activityId, successful, receipt, observation);
    partial void CancelInventoryPractice(Person p, long activityId, string sourceId) => CancelKnowledgePractice(p, activityId, sourceId);

    private KnowledgeAdoption? KnowledgeFor(Person person, string workId) =>
        KnowledgeRuntime().Adopt(person.Id, workId, 1);

    private string KnowledgeSource(Person person, string workId) =>
        KnowledgeRuntime().FindKnowledge(person.Id, workId, 1)?.SourceId ?? "";

    private bool CanConsultKnowledge(Person person, string workId, long editionId) => state.Knowledge.Media.Any(m =>
        m.EditionId == editionId && !m.Destroyed && !m.Unreadable && !m.MissingChapters.Contains(1) &&
        state.Knowledge.Editions.Any(e => e.Id == editionId && e.WorkId == workId) && CanReadKnowledgeItem(person, m.ItemId) &&
        (m.OwnerId == person.Id && m.OwnerRights.HasFlag(KnowledgeRights.Read) || state.Knowledge.Access.Any(a =>
            a.ItemId == m.ItemId && a.PersonId == person.Id && a.Rights.HasFlag(KnowledgeRights.Read) &&
            a.GrantedMonth <= Month && a.ExpiresMonth > Month && (!a.Revoked || a.RevokedMonth > Month))));

    private bool CanReadKnowledgeItem(Person person, long itemId) => items.TryGetValue(itemId, out var item) &&
        item.Quantity == 1 && item.Reserved == 0 && !KnowledgeUnreadable(item) &&
        InventoryDefinitions.Definition(item.Definition).Item.Kind == ItemKind.Book && KnowledgeItemPlace(item) == person.Place &&
        (item.Owner == person.Id || state.Inventory.Metadata.Any(m => m.Item == item.Id && m.UseRights.Contains(person.Id))) &&
        (item.BoundSoul == 0 || item.BoundSoul == person.SoulId);

    private void MirrorKnowledge(Person person)
    {
        foreach (var work in state.Knowledge.Works)
        {
            var learned = KnowledgeFor(person, work.Id);
            if (learned is null) person.Understanding.Remove(work.Id); else person.Understanding[work.Id] = learned.Comprehension * 100;
        }
        foreach (var skill in state.Knowledge.Works.Select(w => w.SkillId).Distinct(StringComparer.Ordinal))
        {
            var owned = state.Knowledge.Works.Where(w => w.SkillId == skill).Select(w => KnowledgeFor(person, w.Id)).Where(k => k is not null).ToArray();
            if (owned.Length == 0) person.Understanding.Remove(skill); else person.Understanding[skill] = owned.Max(k => k!.Comprehension) * 100;
            var mastery = owned.Select(k => k!.Mastery).DefaultIfEmpty().Max() * 100;
            if (mastery == 0) person.Skills.Remove(skill); else person.Skills[skill] = mastery;
        }
    }

    private string? ResolveKnowledgeWork(string value, Person person)
    {
        if (value.Length == 0) value = person.Realm == Realm.Mortal ? "entry" : Catalog.Paths.First(p => p.Realm == person.Realm).Id;
        return state.Knowledge.Works.FirstOrDefault(w => w.Id == value)?.Id ??
            state.Knowledge.Works.Where(w => w.SkillId == value).OrderBy(w => w.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;
    }

    private void SetKnowledgeAction(Person person, ActionKind kind, KnowledgeWorldAction context)
    {
        person.Action = kind; person.Due = KnowledgeRuntime().FindActivity(context.Activity)!.DueMonth;
        person.ActionTarget = context.Reservation > 0 ? context.Reservation : context.Activity;
        person.ActionData = KnowledgeActionPrefix + JsonSerializer.Serialize(context); person.ActionRevision = state.LawRevision;
    }

    private static bool TryReadKnowledgeAction(Person person, out KnowledgeWorldAction context)
    {
        context = null!;
        if (!person.ActionData.StartsWith(KnowledgeActionPrefix, StringComparison.Ordinal)) return false;
        try { context = JsonSerializer.Deserialize<KnowledgeWorldAction>(person.ActionData.AsSpan(KnowledgeActionPrefix.Length))!; }
        catch (JsonException exception) { throw new InvalidDataException("지식 행동 문맥이 손상되었습니다.", exception); }
        if (context is null || context.Activity <= 0 || context.Body <= 0) throw new InvalidDataException("지식 행동 신원 문맥이 없습니다.");
        return true;
    }

    private void ClearKnowledgeParticipants(long activity)
    {
        foreach (var person in state.People)
            if (TryReadKnowledgeAction(person, out var context) && context.Activity == activity)
            { person.Action = ActionKind.Idle; person.Due = 0; person.ActionTarget = 0; person.ActionData = ""; }
    }

    private static long CheckKnowledge(KnowledgeResult result) => result.Applied ? result.Id : throw new InvalidOperationException(result.Message);
    private static KnowledgeParticipant KnowledgeParticipantFor(Person person) => new(person.Id, person.Place,
        person.Status == LifeStatus.Alive && person.Health >= 10 && !person.Sealed);
    private int KnowledgeItemPlace(Item item) => item.Place >= 0 ? item.Place : people[item.Owner].Place;
    private static bool KnowledgeUnreadable(Item item) => item.Damage >= 100 || item.Defects.HasFlag(ItemDefect.LostCore);
    private static int KnowledgeQuality(int quality) => quality switch { < 1000 => 0, < 3000 => 1, < 4000 => 2, < 6000 => 3, < 8000 => 4, < 10000 => 5, _ => 6 };
    private string KnowledgeActionSource(Person person, string kind) => $"{kind}:{person.BodyId}:{Month}:{state.Knowledge.Activities.Count(a => a.ActorId == person.Id || a.TeacherId == person.Id)}";
    private static string CanonicalKnowledgeClaim(string workId)
    {
        if (workId == "entry") return "호흡을 고르고 감응한 영기를 분리하여 운기한다.";
        if (workId.StartsWith("recipe:", StringComparison.Ordinal) && InventoryDefinitions.Recipes.TryGetValue(workId[7..], out var recipe))
            return string.Join("·", recipe.Inputs.OrderBy(i => i.Key, StringComparer.Ordinal).Select(i =>
                InventoryDefinitions.Definition(i.Key).Item.Name + " " + i.Value.ToString(CultureInfo.InvariantCulture))) + "을 정해진 순서로 가공한다.";
        var path = Catalog.Paths.FirstOrDefault(p => p.Id == workId);
        return path is null ? throw new InvalidDataException("공정의 실제 원리를 찾을 수 없습니다.") : path.Name + ": 기록된 순서대로 기운을 순환한다.";
    }
}
