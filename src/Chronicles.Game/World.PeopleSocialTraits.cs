using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{

    /// <summary>Only this person's active source-backed preferences enter their goal score.</summary>
    internal int PeoplePreference(Person person, string goal)
    {
        var preferences = Traits.ActivePreferences(state.PeopleDomain.Traits, person.Id, Month);
        int Weight(GoalPreference axis) => goal switch
        {
            "production" => axis switch { GoalPreference.Patience or GoalPreference.Discipline => 3, GoalPreference.Curiosity or GoalPreference.Autonomy => 1, _ => 0 },
            "information" => axis switch { GoalPreference.Curiosity => 4, GoalPreference.Autonomy => 2, GoalPreference.Risk or GoalPreference.SeekingDao => 1, _ => 0 },
            "trade" => axis switch { GoalPreference.Cooperation or GoalPreference.Trust => 2, GoalPreference.Ambition or GoalPreference.Honour => 1, _ => 0 },
            "learning" => axis switch { GoalPreference.SeekingDao or GoalPreference.Curiosity => 3, GoalPreference.Patience or GoalPreference.Discipline => 2, _ => 0 },
            "cooperation" => axis switch { GoalPreference.Family => 4, GoalPreference.Cooperation or GoalPreference.Empathy => 3, GoalPreference.Trust => 2, GoalPreference.Honour => 1, _ => 0 },
            "retreat" => axis switch { GoalPreference.Survival => 4, GoalPreference.Patience => 1, GoalPreference.Risk => -2, GoalPreference.Competition => -1, _ => 0 },
            "politics" => axis switch { GoalPreference.Ambition => 4, GoalPreference.Honour or GoalPreference.Discipline => 2, GoalPreference.Competition or GoalPreference.Family => 1, _ => 0 },
            "cultivation" => axis switch { GoalPreference.SeekingDao => 4, GoalPreference.Discipline or GoalPreference.Patience => 3, GoalPreference.Survival => 1, _ => 0 },
            "martial" => axis switch { GoalPreference.Competition or GoalPreference.Discipline => 3, GoalPreference.SeekingDao => 2, GoalPreference.Risk => 1, _ => 0 },
            _ => 0
        };
        return Math.Clamp(preferences.Sum(p => p.Amount * Weight(p.Preference)) / 100, -500, 500);
    }

    internal int PeopleFortuneModifier(Person person, FortuneEventKind eventKind) => Math.Clamp(
        Traits.ActiveFortune(state.PeopleDomain.Traits, person.Id, Month, eventKind).Sum(e => e.ModifierBasisPoints), -10000, 10000);

    // Call immediately after the shared completed-action counter. Unlike its capped 0..100 preference counter,
    // this dated record continues to observe real practice and therefore cannot lapse while practice continues.
    internal void RecordPeopleCompletedAction(Person person, ActionKind kind)
    {
        if (kind == ActionKind.Idle) return;
        EnsurePeopleSocialBaselines();
        var key = kind.ToString(); var habits = state.PeopleDomain.Social.Habits.ToList();
        var index = habits.FindIndex(h => h.Person == person.Id && h.Habit == key);
        if (index < 0) habits.Add(new(person.Id, person.BodyId, key, 1, Month, Month, person.Habits.GetValueOrDefault(key)));
        else habits[index] = habits[index] with { Count = checked(habits[index].Count + 1), LastCompleted = Month,
            LastObservedCounter = person.Habits.GetValueOrDefault(key) };
        SetPeopleSocial(state.PeopleDomain.Social with { Habits = habits.OrderBy(h => h.Person).ThenBy(h => h.Habit, StringComparer.Ordinal).ToArray() });
    }

    private void RecordPeoplePoliticalTitles(GameEvent entry, Person actor)
    {
        if (entry.Kind != "정치 명령") return;
        var before = state.PeopleDomain.Social.Groups.ToDictionary(g => g.Group);
        if (entry.Text.StartsWith("Founded an actual organization", StringComparison.Ordinal))
            foreach (var group in state.Groups.Where(g => g.Id == actor.Sect && g.Kind == GroupKind.Sect && g.Leader == actor.Id && g.Founded == Month))
                RecordPeopleTrait(actor, "status.sect-founder", PeopleEventSource(entry, actor), [TraitFact.ActualSectFounded], true, group.Id);
        foreach (var group in state.Groups.Where(g => g.Kind == GroupKind.Dynasty && g.Leader == actor.Id && g.Provinces.Count > 0 &&
            before.TryGetValue(g.Id, out var prior) && prior.Kind != GroupKind.Dynasty))
            RecordPeopleTrait(actor, "status.dynasty-founder", PeopleEventSource(entry, actor), [TraitFact.ActualDynastyFounded], true, group.Id);
    }

    private void ReconcilePeopleAuthority(GameEvent? evidence = null)
    {
        foreach (var group in state.Groups)
        {
            var holder = !group.Dissolved && group.Provinces.Count > 0 && people.TryGetValue(group.Leader, out var current) && current.Status == LifeStatus.Alive ? current : null;
            foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.DefinitionId == "status.landlord" && t.RelatedEntityId == group.Id &&
                t.Status is TraitStatus.Active or TraitStatus.Latent && t.PersonId != holder?.Id).ToArray())
                ReleasePeopleTraits(people[trait.PersonId], "status.landlord", group.Id, evidence is null ? $"authority:{group.Id}:changed:{Month}" : $"event:{evidence.Id}",
                    evidence is not null && people[trait.PersonId].Journal.Contains(evidence));
            if (holder is not null && !state.PeopleDomain.Traits.Traits.Any(t => t.PersonId == holder.Id && t.DefinitionId == "status.landlord" &&
                t.RelatedEntityId == group.Id && t.Status == TraitStatus.Active))
            {
                var source = evidence is null ? new PeopleSource($"authority:{group.Id}:{holder.Id}", PeopleSourceKind.Development,
                    "실제 현재 주권/직책 참조·호칭은 세금 권한을 발급하지 않음", Month, state.LawRevision, holder.Id, holder.BodyId, holder.SoulId) : PeopleEventSource(evidence, holder);
                RecordPeopleTrait(holder, "status.landlord", source, [TraitFact.ActualLandAuthority], true, group.Id,
                    observeSelf: evidence is not null && holder.Journal.Contains(evidence));
            }
            if (!group.Dissolved && group.Kind == GroupKind.Dynasty && group.Provinces.Count == 0 && evidence?.Actor == group.Leader &&
                people.TryGetValue(group.Leader, out var exile) && exile.Status == LifeStatus.Alive &&
                state.PeopleDomain.Social.Groups.Any(g => g.Group == group.Id && g.Territory))
                RecordPeopleTrait(exile, "status.exile", PeopleEventSource(evidence, exile), [TraitFact.ActualExile], true, group.Id);
        }
    }

    private void ReleasePeopleTraits(Person person, string definition, long? related, string evidence, bool personallyKnown = true)
    {
        foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.PersonId == person.Id && t.DefinitionId == definition &&
            (related is null || t.RelatedEntityId == related) && t.Status is TraitStatus.Active or TraitStatus.Latent or TraitStatus.Suppressed).ToArray())
        {
            var source = new PeopleSource(evidence, PeopleSourceKind.Development, "실제 조건/권한/관계의 종료", Month, state.LawRevision,
                person.Id, person.BodyId, person.SoulId);
            var released = Traits.Transition(state.PeopleDomain.Traits, new(trait.Id, $"{evidence}:trait:{trait.Id}", source,
                [TraitCatalog.Get(definition).Release]), TraitChangeKind.Released);
            if (released.Accepted)
            {
                state.PeopleDomain = state.PeopleDomain with { Traits = released.State };
                if (personallyKnown) state.PeopleDomain = state.PeopleDomain with
                {
                    TraitObservations = state.PeopleDomain.TraitObservations.Where(o => o.ObserverId != person.Id || o.SubjectId != person.Id || o.DefinitionId != definition)
                        .Append(new(person.Id, person.Id, definition, TraitStatus.Released, Month, Month, trait.ExpiresMonth, evidence)).ToArray()
                };
            }
        }
        ProjectPeopleTraits(person);
    }

    private void FormPeopleExperiencedTraits(Person person)
    {
        var policy = state.PeopleDomain.Social.Policy;
        var habits = state.PeopleDomain.Social.Habits.Where(h => h.Person == person.Id && h.Habit != "baseline").ToArray();
        int Count(params string[] kinds) => habits.Where(h => kinds.Contains(h.Habit, StringComparer.Ordinal) && Month - h.LastCompleted <= policy.HabitLapseMonths).Sum(h => h.Count);
        void Form(string id, TraitFact fact, bool condition, bool lapse = false)
        {
            var active = state.PeopleDomain.Traits.Traits.FirstOrDefault(t => t.PersonId == person.Id && t.DefinitionId == id && t.Status is TraitStatus.Active or TraitStatus.Latent or TraitStatus.Suppressed);
            if (condition && active is null)
                RecordPeopleTrait(person, id, new($"experience:{person.BodyId}:{id}:{Month}", PeopleSourceKind.Development,
                    $"실제 완료 습성/개인 경험·{string.Join(',', habits.Select(h => $"{h.Habit}:{h.Count}@{h.LastCompleted}"))}", Month,
                    state.LawRevision, person.Id, person.BodyId, person.SoulId), [fact], true);
            else if (!condition && lapse && active is not null) ReleasePeopleTraits(person, id, null, $"actual-habit-lapse:{person.BodyId}:{id}:{Month}");
        }
        var practice = Count("Cultivate", "Martial", "Study");
        Form("habit.routine", TraitFact.RoutineExperience, practice >= policy.HabitThreshold, lapse: true);
        Form("habit.planning", TraitFact.PlanningExperience, Count("Craft", "Farm", "Teach") >= policy.HabitThreshold, lapse: true);
        Form("habit.adaptable", TraitFact.AdaptationExperience, habits.Count(h => h.Count > 0 && Month - h.LastCompleted <= policy.HabitLapseMonths) >= 4, lapse: true);
        Form("habit.storage", TraitFact.StorageExperience, Count("Gather", "Farm") >= policy.HabitThreshold, lapse: true);
        Form("temperament.curious", TraitFact.CuriosityExperience, Count("Explore", "Study") >= policy.HabitThreshold && person.Personality[3] >= 50);
        Form("temperament.competitive", TraitFact.CompetitionExperience, Count("Martial") >= policy.HabitThreshold && person.Personality[13] >= 50);
        Form("temperament.calm", TraitFact.CalmExperience, Count("Heal", "Guard") >= policy.HabitThreshold && Math.Abs(person.Mood) <= 10);
        Form("temperament.resolve", TraitFact.ResolveExperience, practice >= policy.HabitThreshold && person.Journal.Any(e => e.Kind.Contains("실패", StringComparison.Ordinal)));
        Form("dao.seeking", TraitFact.DaoValue, Count("Cultivate", "Study") >= policy.HabitThreshold && person.Personality[1] >= 60);
        Form("dao.longevity", TraitFact.SurvivalValue, Count("Heal", "Gather", "Care") >= policy.HabitThreshold && person.Personality[0] >= 60);
        Form("dao.protection", TraitFact.ProtectionValue, Count("Care") >= policy.HabitThreshold);
    }

    private void AwakenPeoplePrototypes(Person person)
    {
        var profile = PeopleProfile(person);
        var latent = state.PeopleDomain.Traits.Traits.Where(t => t.PersonId == person.Id && t.Status == TraitStatus.Latent &&
            TraitCatalog.Get(t.DefinitionId).Layer is TraitLayer.Body or TraitLayer.Bloodline).ToArray();
        foreach (var trait in latent)
        {
            var facts = trait.DefinitionId switch
            {
                "body.meridian" when profile.Learning.Any(l => l.TalentIndex == 7) => new[] { TraitFact.BodyPrototype, TraitFact.MeridianFit },
                "body.earth" when state.Places[person.Place].Element == 4 && profile.Learning.Any(l => l.TalentIndex is 4 or 7 or 18) => [TraitFact.BodyPrototype, TraitFact.EarthAffinity],
                "bloodline.latent" when profile.Learning.Count(l => l.TalentIndex is 7 or 10) >= 3 => [TraitFact.BloodlinePrototype, TraitFact.BloodlineAwakened],
                "bloodline.resonance" when person.Journal.Any(e => e.Kind == "전수" && e.Month == Month &&
                    people.TryGetValue(e.Actor, out var teacher) && Related(person, teacher)) => [TraitFact.BloodlinePrototype, TraitFact.BloodlineResonating],
                _ => Array.Empty<TraitFact>()
            };
            if (facts.Length > 0)
                RecordPeopleTrait(person, trait.DefinitionId, trait.Source with { Month = Month, Reference = "실제 학습/협력에서 확인한 기존 육신·혈맥 원형의 발현" }, facts, true,
                    trait.RelatedEntityId, trait.ExpiresMonth);
        }
    }
}
