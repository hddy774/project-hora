namespace Chronicles.Game.Knowledge;

public sealed partial class KnowledgeAuthority
{
    /// <summary>Only existing personal records are projected. Missing people/chapters allocate no authority state.</summary>
    public KnowledgePage<PersonalKnowledgeView> ViewPersonal(long personId, int offset = 0, int limit = 32, long changedSinceRevision = 0)
    {
        ValidateRange(offset, limit, changedSinceRevision);
        if (!personMemories.TryGetValue(personId, out var index)) return new(state.Revision, offset, 0, Array.Empty<PersonalKnowledgeView>());
        long Changed(PersonalKnowledge m) => Math.Max(m.Revision, mastery.GetValueOrDefault((m.PersonId, m.WorkId, m.Chapter))?.Revision ?? 0);
        var selected = index.Values.Where(m => Changed(m) > changedSinceRevision);
        var rows = selected.Skip(offset).Take(limit).Select(m =>
        {
            var work = works[m.WorkId];
            return new PersonalKnowledgeView(m.WorkId, work.Title, work.SkillId, m.Chapter,
                work.Chapters.Single(c => c.Number == m.Chapter).Title, m.EditionId, m.Claim,
                m.Decoding, m.Comprehension, m.Confidence, m.Belief,
                mastery.GetValueOrDefault((m.PersonId, m.WorkId, m.Chapter))?.Mastery ?? 0,
                m.Revision, Changed(m), m.SourceId);
        }).ToArray();
        return new(state.Revision, offset, selected.Count(), Array.AsReadOnly(rows));
    }

    /// <summary>Access reveals a physical catalogue entry, never unread chapter claims or another person's progress.</summary>
    public KnowledgePage<AccessibleKnowledgeView> ViewAccessible(long personId, int place, long month,
        int offset = 0, int limit = 32, long changedSinceRevision = 0)
    {
        ValidateRange(offset, limit, changedSinceRevision);
        ArgumentOutOfRangeException.ThrowIfNegative(month);
        var selected = media.Values.Where(m => !m.Destroyed && m.Place == place &&
            (RightsFor(personId, m, month) & KnowledgeRights.Read) != 0 &&
            Math.Max(m.Revision, AccessRevision(personId, m.ItemId)) > changedSinceRevision).OrderBy(m => m.ItemId);
        var rows = selected.Skip(offset).Take(limit).Select(m =>
        {
            var edition = editions[m.EditionId];
            return new AccessibleKnowledgeView(m.ItemId, m.EditionId, edition.WorkId, works[edition.WorkId].Title,
                m.Quality, RightsFor(personId, m, month), m.Unreadable ? 0 : edition.Chapters.Count(c => !m.MissingChapters.Contains(c.Number)),
                m.Unreadable, Math.Max(m.Revision, AccessRevision(personId, m.ItemId)));
        }).ToArray();
        return new(state.Revision, offset, selected.Count(), Array.AsReadOnly(rows));
    }

    public KnowledgePage<PersonalKnowledge> ViewPersonalHistory(long personId, string workId, int chapter,
        int offset = 0, int limit = 32)
    {
        ValidateRange(offset, limit, 0);
        var selected = state.Memories.Where(m => m.PersonId == personId && m.WorkId == workId && m.Chapter == chapter);
        return new(state.Revision, offset, selected.Count(), Array.AsReadOnly(selected.Skip(offset).Take(limit).ToArray()));
    }

    private long AccessRevision(long personId, long itemId) => state.Access.Where(a => a.PersonId == personId && a.ItemId == itemId)
        .Select(a => a.Revision).DefaultIfEmpty().Max();

    private static void ValidateRange(int offset, int limit, long changedSinceRevision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(limit));
        ArgumentOutOfRangeException.ThrowIfNegative(changedSinceRevision);
    }
}
