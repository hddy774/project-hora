namespace Chronicles.Game.Knowledge;

/// <summary>
/// Deterministic, serializable knowledge transitions. The caller coordinates inventory, actor lifecycle and world time
/// in the same transaction; this authority never reads hidden world facts, consumes RNG, or awards cultivation work.
/// </summary>
public sealed partial class KnowledgeAuthority
{
    private readonly KnowledgeState state;
    private readonly Dictionary<string, KnowledgeWork> works = new(StringComparer.Ordinal);
    private readonly Dictionary<long, KnowledgeEdition> editions = [];
    private readonly Dictionary<long, KnowledgeMedium> media = [];
    private readonly Dictionary<long, KnowledgeActivity> activities = [];
    private readonly Dictionary<(long Person, string Work, int Chapter), PersonalKnowledge> memories = [];
    private readonly Dictionary<(long Person, string Work, int Chapter), PracticalKnowledge> mastery = [];
    private readonly Dictionary<long, SortedDictionary<(string Work, int Chapter), PersonalKnowledge>> personMemories = [];
    private readonly HashSet<string> sources = new(StringComparer.Ordinal);

    public KnowledgeAuthority() : this(new KnowledgeState()) { }

    private KnowledgeAuthority(KnowledgeState snapshot)
    {
        state = Clone(snapshot);
        foreach (var work in state.Works) works.Add(work.Id, work);
        foreach (var edition in state.Editions) editions.Add(edition.Id, edition);
        foreach (var medium in state.Media) media.Add(medium.ItemId, medium);
        foreach (var activity in state.Activities) activities.Add(activity.Id, activity);
        foreach (var memory in state.Memories) IndexMemory(memory);
        foreach (var skill in state.Mastery) mastery[(skill.PersonId, skill.WorkId, skill.Chapter)] = skill;
        foreach (var evidence in state.Evidence) sources.Add(evidence.SourceId);
    }

    public long Revision => state.Revision;
    public KnowledgeRules Rules => state.Rules;
    public KnowledgeState Checkpoint() => Clone(state);

    public static KnowledgeAuthority Restore(KnowledgeState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Validate(snapshot);
        return new(snapshot);
    }

    public KnowledgeResult DefineWork(KnowledgeWork work)
    {
        if (work is null || !ValidWork(work)) return Reject(KnowledgeStatus.Invalid, "Invalid work definition.");
        var error = SourceError(work.SourceId, work.CreatedMonth);
        if (error is not null) return error;
        if (works.ContainsKey(work.Id)) return Reject(KnowledgeStatus.Invalid, "Work identity already exists.");
        var stored = work with { Chapters = work.Chapters.Select(c => c with { }).ToArray() };
        state.Works.Add(stored); works.Add(stored.Id, stored);
        Commit(work.SourceId, "work", 0, 0, work.Id, 0, 0, [], work.CreatedMonth, work.CreatedMonth);
        return Applied();
    }

    public KnowledgeResult CreateEdition(string workId, long parentEditionId, EditionRelation relation,
        long contributorId, EditionChapter[] chapters, long month, string sourceId)
    {
        var error = SourceError(sourceId, month);
        if (error is not null) return error;
        if (!works.TryGetValue(workId, out var work)) return Reject(KnowledgeStatus.NotFound, "Unknown work.");
        if (!Enum.IsDefined(relation) || contributorId <= 0 || month < work.CreatedMonth || !ValidEditionChapters(work, chapters))
            return Reject(KnowledgeStatus.Invalid, "Invalid edition chapters or contributor.");
        if (relation == EditionRelation.Original ? parentEditionId != 0 :
            !editions.TryGetValue(parentEditionId, out var parent) || parent.WorkId != workId || parent.CreatedMonth > month)
            return Reject(KnowledgeStatus.Invalid, "Edition lineage must remain within one work.");
        if (relation == EditionRelation.Copy && chapters.Any(c =>
            !editions[parentEditionId].Chapters.Any(p => p.Number == c.Number && p.Claim == c.Claim)))
            return Reject(KnowledgeStatus.Invalid, "A copy cannot silently change its source claims.");
        var id = state.NextId++;
        var edition = new KnowledgeEdition(id, workId, parentEditionId, relation, contributorId,
            chapters.OrderBy(c => c.Number).Select(c => c with { }).ToArray(), month, state.Revision + 1, sourceId);
        state.Editions.Add(edition); editions.Add(id, edition);
        Commit(sourceId, "edition", contributorId, 0, workId, id, 0, chapters.Select(c => c.Number).ToArray(), month, month);
        return Applied(id);
    }

    public KnowledgeResult BindMedium(long itemId, long editionId, long ownerId, int place, KnowledgeMediumKind kind,
        int quality, KnowledgeRights ownerRights, int[] missingChapters, bool unreadable, long month, string sourceId)
    {
        var error = SourceError(sourceId, month);
        if (error is not null) return error;
        if (!editions.TryGetValue(editionId, out var edition)) return Reject(KnowledgeStatus.NotFound, "Unknown edition.");
        if (itemId <= 0 || ownerId < 0 || place < 0 || !Enum.IsDefined(kind) || quality is < 0 or > 6 ||
            !ValidRights(ownerRights) || !ValidMissing(edition, missingChapters) || month < edition.CreatedMonth)
            return Reject(KnowledgeStatus.Invalid, "Invalid physical medium.");
        if (media.ContainsKey(itemId) || activities.Values.Any(a => a.Status == KnowledgeActivityStatus.Active && a.TargetItemId == itemId))
            return Reject(KnowledgeStatus.Invalid, "A physical item can bind to only one edition.");
        var medium = new KnowledgeMedium(itemId, editionId, ownerId, place, kind, quality, ownerRights,
            missingChapters.Order().ToArray(), unreadable, false, state.Revision + 1, sourceId);
        state.Media.Add(medium); media.Add(itemId, medium);
        Commit(sourceId, "medium", ownerId, 0, edition.WorkId, editionId, itemId, [], month, month);
        return Applied(itemId);
    }

    /// <summary>Call alongside the corresponding inventory move, transfer, damage or destruction.</summary>
    public KnowledgeResult SynchronizeMedium(long itemId, long ownerId, int place, int quality, int[] missingChapters,
        bool unreadable, bool destroyed, long month, string sourceId)
    {
        var error = SourceError(sourceId, month);
        if (error is not null) return error;
        if (!media.TryGetValue(itemId, out var medium)) return Reject(KnowledgeStatus.NotFound, "Unknown medium.");
        var edition = editions[medium.EditionId];
        if (ownerId < 0 || place < 0 || quality is < 0 or > 6 || !ValidMissing(edition, missingChapters) ||
            month < edition.CreatedMonth || medium.Destroyed && !destroyed)
            return Reject(KnowledgeStatus.Invalid, "Invalid physical transition; destroyed media cannot be resurrected.");
        ReplaceMedium(medium with { OwnerId = ownerId, Place = place, Quality = quality,
            MissingChapters = missingChapters.Order().ToArray(), Unreadable = unreadable,
            Destroyed = destroyed, Revision = state.Revision + 1, SourceId = sourceId });
        Commit(sourceId, destroyed ? "medium-destroyed" : "medium-changed", ownerId, 0,
            edition.WorkId, edition.Id, itemId, [], month, month);
        return Applied(itemId);
    }

    public KnowledgeResult GrantAccess(long itemId, long grantorId, long personId, KnowledgeRights rights,
        long expiresMonth, long month, string sourceId)
    {
        var error = SourceError(sourceId, month);
        if (error is not null) return error;
        if (!media.TryGetValue(itemId, out var medium)) return Reject(KnowledgeStatus.NotFound, "Unknown medium.");
        if (grantorId <= 0 || personId <= 0 || medium.OwnerId != grantorId || medium.Destroyed ||
            !ValidRights(rights) || rights == KnowledgeRights.None || (rights & medium.OwnerRights) != rights || expiresMonth <= month)
            return Reject(KnowledgeStatus.Forbidden, "Only the owner may grant the rights held by this medium.");
        state.Access.Add(new(personId, itemId, rights, expiresMonth, grantorId, month, state.Revision + 1, sourceId));
        Commit(sourceId, "access", grantorId, personId, editions[medium.EditionId].WorkId, medium.EditionId,
            itemId, [], month, month);
        return Applied(itemId);
    }

    /// <summary>Explicit genesis evidence only; never grants practical mastery or copies a parent's knowledge.</summary>
    public KnowledgeResult SeedInitialKnowledge(long personId, long editionId, int[] chapters, int comprehension,
        string sourceId)
    {
        var error = SourceError(sourceId, 0);
        if (error is not null) return error;
        if (!editions.TryGetValue(editionId, out var edition)) return Reject(KnowledgeStatus.NotFound, "Unknown edition.");
        if (personId <= 0 || edition.CreatedMonth != 0 || !ValidSelection(edition, chapters) || comprehension is < 0 or > 100 ||
            chapters.Any(c => memories.ContainsKey((personId, edition.WorkId, c))))
            return Reject(KnowledgeStatus.Invalid, "Initial knowledge requires explicit, previously unlearned genesis chapters.");
        foreach (var chapter in chapters.Order())
        {
            var claim = edition.Chapters.Single(c => c.Number == chapter).Claim;
            AppendMemory(new(personId, edition.WorkId, chapter, editionId, claim, 0, comprehension > 0 ? 100 : 0,
                comprehension, 50, KnowledgeBelief.Unverified, 0, state.Revision + 1, 0, sourceId));
        }
        Commit(sourceId, "genesis-knowledge", personId, 0, edition.WorkId, editionId, 0, chapters, 0, 0);
        return Applied();
    }

    public KnowledgeResult ChangeRules(KnowledgeRules rules, long month, string sourceId)
    {
        var error = SourceError(sourceId, month);
        if (error is not null) return error;
        if (rules is null || !ValidRules(rules) || rules.Revision <= state.Rules.Revision)
            return Reject(KnowledgeStatus.Invalid, "Rules need valid units and a newer revision.");
        state.Rules = rules with { };
        Commit(sourceId, "rules", 0, 0, "", 0, 0, [], month, month);
        return Applied();
    }

    public PersonalKnowledge? FindKnowledge(long personId, string workId, int chapter) =>
        memories.GetValueOrDefault((personId, workId, chapter));

    public KnowledgeAdoption? Adopt(long personId, string workId, int chapter, long expectedRevision = 0)
    {
        var memory = FindKnowledge(personId, workId, chapter);
        if (memory is null || expectedRevision != 0 && expectedRevision != memory.Revision) return null;
        return new(personId, workId, chapter, memory.EditionId, memory.Revision, memory.Claim, memory.Comprehension,
            mastery.GetValueOrDefault((personId, workId, chapter))?.Mastery ?? 0, memory.Belief);
    }

    public KnowledgeAdoption? FindAdoptedRevision(long personId, string workId, int chapter, long revision)
    {
        var memory = state.Memories.Find(m => m.PersonId == personId && m.WorkId == workId &&
            m.Chapter == chapter && m.Revision == revision);
        return memory is null ? null : new(personId, workId, chapter, memory.EditionId, memory.Revision,
            memory.Claim, memory.Comprehension, 0, memory.Belief);
    }

    private KnowledgeResult? SourceError(string sourceId, long month)
    {
        if (!ValidText(sourceId) || month < state.Month || state.Revision == long.MaxValue || state.NextId == long.MaxValue)
            return Reject(KnowledgeStatus.Invalid, "A unique source and a nonnegative integer month are required.");
        return sources.Contains(sourceId) ? Reject(KnowledgeStatus.DuplicateSource, "Source already used.") : null;
    }

    private void Commit(string sourceId, string kind, long actor, long other, string work, long edition,
        long item, int[] chapters, long started, long ended, long personalRevision = 0, string detail = "")
    {
        state.Revision++;
        state.Month = Math.Max(state.Month, ended);
        sources.Add(sourceId);
        state.Evidence.Add(new(sourceId, kind, actor, other, work, edition, item, chapters.Order().ToArray(),
            started, ended, personalRevision, state.Revision, detail));
    }

    private KnowledgeResult Applied(long id = 0) => new(KnowledgeStatus.Applied, "Applied.", id, state.Revision);
    private KnowledgeResult Reject(KnowledgeStatus status, string message) => new(status, message, Revision: state.Revision);

    private void ReplaceMedium(KnowledgeMedium medium)
    {
        state.Media[state.Media.FindIndex(m => m.ItemId == medium.ItemId)] = medium;
        media[medium.ItemId] = medium;
    }

    private void ReplaceActivity(KnowledgeActivity activity)
    {
        state.Activities[state.Activities.FindIndex(a => a.Id == activity.Id)] = activity;
        activities[activity.Id] = activity;
    }

    private void AppendMemory(PersonalKnowledge memory)
    {
        state.Memories.Add(memory);
        IndexMemory(memory);
    }

    private void IndexMemory(PersonalKnowledge memory)
    {
        memories[(memory.PersonId, memory.WorkId, memory.Chapter)] = memory;
        if (!personMemories.TryGetValue(memory.PersonId, out var index))
            personMemories.Add(memory.PersonId, index = new(MemoryKeyComparer.Instance));
        index[(memory.WorkId, memory.Chapter)] = memory;
    }

    private sealed class MemoryKeyComparer : IComparer<(string Work, int Chapter)>
    {
        public static MemoryKeyComparer Instance { get; } = new();
        public int Compare((string Work, int Chapter) x, (string Work, int Chapter) y)
        {
            var work = StringComparer.Ordinal.Compare(x.Work, y.Work);
            return work != 0 ? work : x.Chapter.CompareTo(y.Chapter);
        }
    }
}
