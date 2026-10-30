using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Queries;

/// <summary>
/// Builds a read-only, per-life 100-year history book from permanent detailed
/// chronicle events. The detailed chronicle remains the canonical stored source.
/// </summary>
public static class PersonalCenturyChronicleProjection
{
    public const int YearsPerVolume = 100;

    public static IReadOnlyList<PersonalCenturyChronicleEntry> Create(
        IReadOnlyList<LifeChronicleEntry> detailedChronicle,
        AgentSnapshot currentAgent,
        SimulationTick currentTick)
    {
        ArgumentNullException.ThrowIfNull(detailedChronicle);
        ArgumentNullException.ThrowIfNull(currentAgent);

        var eventsByLife = detailedChronicle
            .GroupBy(entry => entry.LifeNumber)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(entry => entry.Sequence).ToArray());
        if (!eventsByLife.ContainsKey(currentAgent.LifeNumber))
        {
            eventsByLife.Add(currentAgent.LifeNumber, []);
        }

        var results = new List<PersonalCenturyChronicleEntry>();
        foreach (var (lifeNumber, lifeEvents) in eventsByLife.OrderBy(pair => pair.Key))
        {
            var isCurrentLife = lifeNumber == currentAgent.LifeNumber;
            var lastObservedAge = isCurrentLife
                ? currentAgent.AgeYears
                : lifeEvents.Length == 0
                    ? 0
                    : lifeEvents.Max(entry => entry.AgeYears);
            var lastCentury = lastObservedAge / YearsPerVolume;
            var lifeStartedTick = lifeEvents.Length == 0
                ? currentTick
                : lifeEvents[0].Tick;

            for (var century = 0L; century <= lastCentury; century++)
            {
                results.Add(CreateEntry(
                    lifeNumber,
                    century,
                    lifeEvents,
                    lastObservedAge,
                    lifeStartedTick,
                    currentAgent,
                    currentTick));
            }
        }

        return results;
    }

    private static PersonalCenturyChronicleEntry CreateEntry(
        int lifeNumber,
        long century,
        IReadOnlyList<LifeChronicleEntry> lifeEvents,
        long lastObservedAge,
        SimulationTick lifeStartedTick,
        AgentSnapshot currentAgent,
        SimulationTick currentTick)
    {
        var startAge = checked(century * YearsPerVolume);
        var endAge = checked(startAge + YearsPerVolume - 1);
        var isCurrentLife = lifeNumber == currentAgent.LifeNumber;
        var currentLifeEnded = isCurrentLife
            && currentAgent.Identity.LifeState != PersonLifeState.Living;
        var isComplete = !isCurrentLife || currentLifeEnded || lastObservedAge > endAge;
        var observedThrough = isComplete && lastObservedAge > endAge
            ? endAge
            : Math.Min(endAge, lastObservedAge);
        var entries = lifeEvents
            .Where(entry => entry.AgeYears >= startAge && entry.AgeYears <= endAge)
            .ToArray();
        var final = lifeEvents.LastOrDefault(entry => entry.AgeYears <= observedThrough);
        var finalRealm = final == default
            ? currentAgent.RealmStage
            : final.RealmStage;
        var finalMinor = final == default
            ? MinorStage.Floor1
            : final.MinorStage;
        var personName = final == default
            ? currentAgent.Name
            : final.PersonName;
        var startedTick = AddYearsClamped(lifeStartedTick, startAge, currentTick);
        var observedThroughTick = AddYearsClamped(
            lifeStartedTick,
            observedThrough,
            currentTick);
        return new PersonalCenturyChronicleEntry(
            lifeNumber,
            century,
            startAge,
            endAge,
            observedThrough,
            personName,
            isComplete,
            entries.Length,
            entries.Count(entry => IsGrowth(entry.Kind)),
            entries.Count(entry => IsCombat(entry.Kind)),
            entries.Count(entry => IsLife(entry.Kind)),
            entries.Count(entry => IsAcquisition(entry.Kind)),
            entries.Count(entry => entry.Kind == ChronicleEventKind.MajorRealmBreakthrough),
            entries.Count(entry => entry.Kind == ChronicleEventKind.BreakthroughFailure),
            finalRealm,
            finalMinor,
            startedTick,
            observedThroughTick);
    }

    private static SimulationTick AddYearsClamped(
        SimulationTick start,
        long years,
        SimulationTick maximum)
    {
        var value = checked(start.Value + checked(years * GameCalendar.TicksPerYear));
        return new SimulationTick(Math.Min(value, maximum.Value));
    }

    public static bool IsGrowth(ChronicleEventKind kind) => kind is
        ChronicleEventKind.CultivationEntered
        or ChronicleEventKind.MinorStageBreakthrough
        or ChronicleEventKind.FloorNineCompletion
        or ChronicleEventKind.BottleneckBreakthrough
        or ChronicleEventKind.MajorRealmBreakthrough
        or ChronicleEventKind.GreatPerfectionAchieved
        or ChronicleEventKind.BreakthroughFailure;

    public static bool IsCombat(ChronicleEventKind kind) =>
        kind == ChronicleEventKind.CombatDeath;

    public static bool IsLife(ChronicleEventKind kind) => kind is
        ChronicleEventKind.Birth
        or ChronicleEventKind.Death
        or ChronicleEventKind.CombatDeath
        or ChronicleEventKind.ReincarnationEntered
        or ChronicleEventKind.ReincarnationCrossingCompleted
        or ChronicleEventKind.Rebirth;

    public static bool IsAcquisition(ChronicleEventKind kind) => kind is
        ChronicleEventKind.FortuitousEncounter
        or ChronicleEventKind.TechniqueAcquired
        or ChronicleEventKind.SpellAcquired;
}
