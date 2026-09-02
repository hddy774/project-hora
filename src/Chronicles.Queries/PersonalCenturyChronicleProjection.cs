using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Queries;

/// <summary>
/// Legacy/schema-migration projection. Runtime observations use the compact archive
/// maintained by the simulation and never rebuild all volumes from detailed rows.
/// </summary>
public static class PersonalCenturyChronicleProjection
{
    public const int YearsPerVolume = PersonalChronicleRules.YearsPerVolume;

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

        return results.Count <= PersonalChronicleRules.VolumeRetentionLimit
            ? results
            : results.TakeLast(PersonalChronicleRules.VolumeRetentionLimit).ToArray();
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
            entries.Count(entry => PersonalChronicleRules.IsGrowth(entry.Kind)),
            entries.Count(entry => PersonalChronicleRules.IsCombat(entry.Kind)),
            entries.Count(entry => PersonalChronicleRules.IsLife(entry.Kind)),
            entries.Count(entry => PersonalChronicleRules.IsAcquisition(entry.Kind)),
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

}
