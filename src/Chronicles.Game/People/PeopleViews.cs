namespace Chronicles.Game.People;

public sealed record TalentExpressionView(int Index, int Learned, int Understanding, int CurrentExpression);
public sealed record CognitionExpressionView(CognitionCapability Capability, int Practice, int CurrentExpression);
public sealed record PeopleDetailView(long PersonId, long Month, bool Observer, InnateProfile? Innate,
    TalentExpressionView[] CurrentTalents, CognitionExpressionView[] CurrentCognition, Embodiment? ActualBody,
    ConceptionRecord? GeneticOrigin, BirthProfile? Birth, ParentGenome? Genome, PersonTrait[]? TraitHistory,
    KnownTrait[] KnownTraits, SoulTraceProfile? ActualSoul, SoulTraceHint[] KnownTraces, AptitudeEvaluation[]? ActualEvaluations,
    ReputationClaim[] KnownReputation, PeopleLearningEvent[]? LearningHistory, string PolicyId, long PolicyRevision);
