namespace Chronicles.Game.People;

public enum PeopleSessionKind { Assessment, ClaimRelay, Oath }
public enum PeopleSessionStatus { Proposed, Active, Completed, Cancelled, Expired }
public sealed record PeopleSession(long Id, PeopleSessionKind Kind, long Proposer, long Recipient, long ProposerBody, long RecipientBody,
    int Place, long Requested, long Expires, long Started, long Due, PeopleSessionStatus Status, int TalentIndex = 21,
    ReputationClaim? SharedClaim = null, string Terms = "", long Ended = 0, string CompletionSource = "");
public sealed record PeopleOath(long Id, long First, long Second, long Began, long Until, bool Active, string Terms, long Evidence,
    long Ended = 0, string EndSource = "");
public sealed record PeopleHabitSnapshot(long Person, long Body, string Habit, int Count, long LastCompleted, long FirstTracked,
    int LastObservedCounter = 0);
public sealed record PeopleEcologyObservation(long Person, long Body, long Month, int Place, HabitatKind Habitat,
    bool HabitatSuitable, bool ActualAccommodation, bool GenericMaintenanceAbstraction, int AmbientQiConsumed, int HealthCost,
    PeopleSource Source);
public sealed record PeopleGroupSnapshot(long Group, GroupKind Kind, long Leader, bool Territory, long Observed);
public sealed record PeopleRequestView(long Id, PeopleSessionKind Kind, long Proposer, long Recipient, long Requested, long Expires,
    PeopleSessionStatus Status, int TalentIndex, string Terms);
public sealed record PeopleOathObservation(long Person, long Oath, bool Active, long ObservedMonth, long ReceivedMonth, string EvidenceId);
public sealed record PeopleRequestObservation(long Person, long Session, PeopleSessionStatus Status, long ObservedMonth,
    long ReceivedMonth, string EvidenceId);
public sealed record PeopleOathView(long Id, long Person, long Other, long Began, long Until, bool KnownActive, string Terms,
    long ObservedMonth, string EvidenceId);
public sealed record PeopleSocialPolicy(string PolicyId = "PC-TEMP-V021-PEOPLE-SOCIAL-01", long Revision = 1,
    int RequestLifetimeMonths = 12, int MaximumPendingRequests = 8, int SessionMonths = 1, int ClaimLifetimeMonths = 120,
    int ClaimBudget = 12, int HabitThreshold = 3, int HabitLapseMonths = 24, int OathMonths = 120, bool RequiresReview = true)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PolicyId);
        if (Revision < 1 || RequestLifetimeMonths < 1 || MaximumPendingRequests is < 1 or > 64 || SessionMonths is < 1 or > 120 ||
            ClaimLifetimeMonths < 1 || ClaimBudget is < 1 or > 128 || HabitThreshold is < 1 or > 100 || HabitLapseMonths < 1 || OathMonths < 1)
            throw new InvalidDataException("Invalid provisional people-social policy.");
    }
}
public sealed record PeopleSocialState(PeopleSocialPolicy Policy, long LastEvent, long SettledThrough,
    PeopleSession[] Sessions, PeopleOath[] Oaths, PeopleHabitSnapshot[] Habits, PeopleGroupSnapshot[] Groups)
{
    public PeopleEcologyObservation[] Ecology { get; init; } = [];
    public PeopleOathObservation[] OathObservations { get; init; } = [];
    public PeopleRequestObservation[] RequestObservations { get; init; } = [];
    public static PeopleSocialState Empty() => new(new(), 0, -1, [], [], [], []);
}
