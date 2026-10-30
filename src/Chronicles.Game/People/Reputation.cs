namespace Chronicles.Game.People;

public enum ReputationTopic { AbsoluteAptitude, Genius, GeniusCandidate, CombatStrength, Reliability, Threat, Talent, Species }
public enum ClaimOrigin { DirectObservation, Conversation, Document }
public enum SocialReaction { SeekInformation, ConsiderRecruitment, Caution, InvestigateConflict, ContinueNormally }
public sealed record ClaimHop(string EvidenceId, ClaimOrigin Origin, long SourcePersonId, long RecipientPersonId, long ProducedMonth, long ReceivedMonth);
public sealed record ReputationClaim(string Id, long ObserverId, long SubjectId, ReputationTopic Topic, bool Affirmed, string Statement,
    long? MinimumEstimate, long? MaximumEstimate, int Confidence, long ObservedMonth, long ReceivedMonth, long? ExpiresMonth,
    string EvaluationScopeId, int? TalentIndex, ClaimHop[] Provenance);
public sealed record ReputationPolicy(int MaximumClaimsPerObserver = 64, string PolicyId = "PC-TEMP-V021-REPUTATION-01", long Revision = 1, bool RequiresReview = true);
public sealed record ReputationState(ReputationClaim[] Claims)
{
    public static ReputationState Empty() => new([]);
}
public sealed record ReputationChange(bool Accepted, string Code, ReputationState State, string[] ForgottenClaimIds);
public sealed record ReputationQuery(long ObserverId, long SubjectId, long Month);
public sealed record SocialJudgmentContext(long ObserverId, long SubjectId, long Month, int RequiredConfidence = 7500);
public sealed record SocialJudgment(SocialReaction Reaction, string[] ClaimIds, string Reason);

/// <summary>This API deliberately has no actual aptitude, evaluation, soul or world input.</summary>
public static class Reputation
{
    public static ReputationChange Receive(ReputationState state, ReputationClaim claim, ReputationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(claim); ArgumentNullException.ThrowIfNull(policy);
        Validate(claim);
        if (policy.MaximumClaimsPerObserver < 1 || policy.Revision < 1) throw new InvalidDataException("Invalid reputation retention policy.");
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.PolicyId);
        var existing = state.Claims.FirstOrDefault(c => c.ObserverId == claim.ObserverId && c.Id == claim.Id);
        if (existing is not null) return new(Same(existing, claim), "claim identity already received", state, []);
        if (state.Claims.Any(c => c.ObserverId == claim.ObserverId && c.ReceivedMonth > claim.ReceivedMonth))
            return new(false, "cannot backdate a received claim", state, []);
        var other = state.Claims.Where(c => c.ObserverId != claim.ObserverId).ToArray();
        var current = state.Claims.Where(c => c.ObserverId == claim.ObserverId).Append(claim)
            .Where(c => c.ExpiresMonth is null || c.ExpiresMonth > claim.ReceivedMonth)
            .OrderByDescending(c => c.Confidence).ThenByDescending(c => c.ReceivedMonth).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Take(policy.MaximumClaimsPerObserver).Select(c => c with { Provenance = c.Provenance.ToArray() }).ToArray();
        var forgotten = state.Claims.Where(c => c.ObserverId == claim.ObserverId).Append(claim)
            .Where(c => !current.Any(kept => kept.Id == c.Id)).Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();
        return new(true, "observed claim received; truth remains independent", new(other.Concat(current).OrderBy(c => c.ObserverId)
            .ThenBy(c => c.Id, StringComparer.Ordinal).ToArray()), forgotten);
    }

    public static ReputationClaim[] KnownClaims(ReputationState state, ReputationQuery query)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(query);
        return state.Claims.Where(c => c.ObserverId == query.ObserverId && c.SubjectId == query.SubjectId &&
                c.ReceivedMonth <= query.Month && c.ObservedMonth <= query.Month && (c.ExpiresMonth is null || c.ExpiresMonth > query.Month))
            .OrderBy(c => c.Topic).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c with { Provenance = c.Provenance.ToArray() }).ToArray();
    }

    public static SocialJudgment Judge(ReputationState state, SocialJudgmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context); PeopleChecks.Score(context.RequiredConfidence, nameof(context.RequiredConfidence));
        var claims = KnownClaims(state, new(context.ObserverId, context.SubjectId, context.Month)).Where(c => c.Confidence >= context.RequiredConfidence).ToArray();
        var ids = claims.Select(c => c.Id).ToArray();
        if (claims.Length == 0) return new(SocialReaction.SeekInformation, [], "no sufficiently confident known claim");
        if (claims.GroupBy(c => (c.Topic, c.EvaluationScopeId, c.TalentIndex)).Any(g => g.Select(c => c.Affirmed).Distinct().Count() > 1))
            return new(SocialReaction.InvestigateConflict, ids, "known sources disagree; no hidden truth used to choose a source");
        if (claims.Any(c => c.Topic == ReputationTopic.Threat && c.Affirmed)) return new(SocialReaction.Caution, ids, "observed or relayed threat claim");
        if (claims.Any(c => c.Topic is ReputationTopic.Genius or ReputationTopic.GeniusCandidate && c.Affirmed))
            return new(SocialReaction.ConsiderRecruitment, ids, "known favorable aptitude title claim");
        return new(SocialReaction.ContinueNormally, ids, "current known claims do not justify a special response");
    }

    public static void Validate(ReputationClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim); ArgumentException.ThrowIfNullOrWhiteSpace(claim.Id); ArgumentException.ThrowIfNullOrWhiteSpace(claim.Statement);
        PeopleChecks.Score(claim.Confidence, nameof(claim.Confidence));
        if (claim.ObserverId <= 0 || claim.SubjectId <= 0 || !Enum.IsDefined(claim.Topic) || claim.ObservedMonth < 0 ||
            claim.ReceivedMonth < claim.ObservedMonth || claim.ExpiresMonth <= claim.ReceivedMonth ||
            claim.MinimumEstimate < 0 || claim.MaximumEstimate < claim.MinimumEstimate ||
            (claim.MinimumEstimate is null) != (claim.MaximumEstimate is null) || claim.Provenance.Length == 0 ||
            (claim.Topic == ReputationTopic.Talent ? claim.TalentIndex is null or < 0 or > 23 : claim.TalentIndex is not null))
            throw new InvalidDataException("Invalid observed reputation claim.");
        ClaimHop? previous = null;
        foreach (var hop in claim.Provenance)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(hop.EvidenceId);
            if (!Enum.IsDefined(hop.Origin) || hop.SourcePersonId <= 0 || hop.RecipientPersonId <= 0 || hop.ProducedMonth < claim.ObservedMonth ||
                hop.ReceivedMonth < hop.ProducedMonth || hop.ReceivedMonth > claim.ReceivedMonth ||
                (previous is not null && (previous.RecipientPersonId != hop.SourcePersonId || hop.ProducedMonth < previous.ReceivedMonth)))
                throw new InvalidDataException("Claim provenance must follow actual communication in simulation time.");
            previous = hop;
        }
        if (previous!.RecipientPersonId != claim.ObserverId || previous.ReceivedMonth != claim.ReceivedMonth)
            throw new InvalidDataException("Claim recipient and final provenance hop disagree.");
    }

    private static bool Same(ReputationClaim a, ReputationClaim b) =>
        a == b with { Provenance = a.Provenance } && a.Provenance.SequenceEqual(b.Provenance);
}
