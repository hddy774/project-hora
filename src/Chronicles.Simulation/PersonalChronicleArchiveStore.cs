using System.Collections;
using System.Runtime.InteropServices;
using Chronicles.Core;

namespace Chronicles.Simulation;

public static class PersonalChronicleRules
{
    public const int YearsPerVolume = 100;
    public const int DetailedRetentionLimit = 100;
    public const int VolumeRetentionLimit = 100;

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

/// <summary>
/// 세부 연대기를 지우기 전에 100년 단위 통계를 누적한다. 완결본은 인물당 100권,
/// 일반 인물 세부 기록은 100건으로 제한하며 관심 인물만 세부 기록을 계속 보존한다.
/// </summary>
internal sealed class PersonalChronicleArchiveStore
{
    private const int InitialCompletedVolumeCapacity = 4;

    private readonly int _population;
    private readonly CompactChronicleStore _details;
    private readonly List<CompactPersonalCenturyEntry>?[] _completed;
    private readonly PersonalCenturyAccumulatorState[] _current;

    public PersonalChronicleArchiveStore(int population, CompactChronicleStore details)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _details = details ?? throw new ArgumentNullException(nameof(details));
        _completed = new List<CompactPersonalCenturyEntry>[population];
        _current = new PersonalCenturyAccumulatorState[population];
    }

    public static bool IsValidCheckpoint(AgentCheckpoint agent, long worldTick)
    {
        if (agent.PersonalCenturyChronicle is not { } volumes
            || volumes.Count > PersonalChronicleRules.VolumeRetentionLimit
            || (!agent.PreserveDetailedChronicle
                && agent.Chronicle.Count > PersonalChronicleRules.DetailedRetentionLimit))
        {
            return false;
        }

        var previousLife = 0;
        var previousCentury = -1L;
        foreach (var volume in volumes)
        {
            if (!IsValidVolume(volume, agent.LifeNumber, worldTick)
                || volume.LifeNumber < previousLife
                || (volume.LifeNumber == previousLife
                    && volume.CenturyIndex <= previousCentury))
            {
                return false;
            }
            previousLife = volume.LifeNumber;
            previousCentury = volume.CenturyIndex;
        }

        var current = agent.PersonalCenturyAccumulator;
        return current is null
            || (current.LifeNumber is >= 1
                && current.LifeNumber <= agent.LifeNumber
                && current.CenturyIndex >= 0
                && current.LifeStartedTick >= 0
                && current.ObservedAgeYears >= 0
                && current.ObservedTick is >= 0
                && current.ObservedTick <= worldTick
                && AreCountsValid(
                    current.EventCount,
                    current.GrowthEventCount,
                    current.CombatEventCount,
                    current.LifeEventCount,
                    current.AcquisitionEventCount,
                    current.MajorRealmBreakthroughCount,
                    current.BreakthroughFailureCount)
                && Enum.IsDefined(current.FinalRealmStage)
                && Enum.IsDefined(current.FinalMinorStage));
    }

    private static bool IsValidVolume(
        PersonalCenturyChronicleEntry volume,
        int maximumLifeNumber,
        long worldTick) =>
        volume.IsComplete
        && volume.LifeNumber is >= 1
        && volume.LifeNumber <= maximumLifeNumber
        && volume.CenturyIndex >= 0
        && volume.CenturyIndex
            <= (long.MaxValue - PersonalChronicleRules.YearsPerVolume + 1)
                / PersonalChronicleRules.YearsPerVolume
        && volume.StartAgeYears
            == volume.CenturyIndex * PersonalChronicleRules.YearsPerVolume
        && volume.EndAgeYears
            == volume.StartAgeYears + PersonalChronicleRules.YearsPerVolume - 1
        && volume.ObservedThroughAgeYears >= volume.StartAgeYears
        && volume.ObservedThroughAgeYears <= volume.EndAgeYears
        && volume.StartedTick.Value >= 0
        && volume.ObservedThroughTick.Value >= volume.StartedTick.Value
        && volume.ObservedThroughTick.Value <= worldTick
        && AreCountsValid(
            volume.EventCount,
            volume.GrowthEventCount,
            volume.CombatEventCount,
            volume.LifeEventCount,
            volume.AcquisitionEventCount,
            volume.MajorRealmBreakthroughCount,
            volume.BreakthroughFailureCount)
        && Enum.IsDefined(volume.FinalRealmStage)
        && Enum.IsDefined(volume.FinalMinorStage);

    private static bool AreCountsValid(
        int events,
        int growth,
        int combat,
        int life,
        int acquisition,
        int majorBreakthrough,
        int breakthroughFailure) =>
        events >= 0
        && growth is >= 0 && growth <= events
        && combat is >= 0 && combat <= events
        && life is >= 0 && life <= events
        && acquisition is >= 0 && acquisition <= events
        && majorBreakthrough is >= 0 && majorBreakthrough <= events
        && breakthroughFailure is >= 0 && breakthroughFailure <= events;

    public void SetProtected(int agentIndex, bool isProtected)
    {
        ValidateAgent(agentIndex);
        _details.SetProtected(agentIndex, isProtected);
        if (isProtected || _completed[agentIndex] is not { Count: > 0 } completed)
        {
            return;
        }

        var last = completed[^1];
        _details.RemoveArchivedThrough(
            agentIndex,
            last.LifeNumber,
            last.ObservedThroughAgeYears);
        _details.PrunePersonNames(
            agentIndex,
            lifeNumber => RetainsLife(agentIndex, lifeNumber));
    }

    public bool IsProtected(int agentIndex) =>
        _details.IsProtected(ValidateAgent(agentIndex));

    public void Record(int agentIndex, LifeChronicleEntry entry)
    {
        ValidateAgent(agentIndex);
        ref var current = ref _current[agentIndex];
        if (!current.Initialized || current.LifeNumber != entry.LifeNumber)
        {
            if (current.Initialized)
            {
                CompleteCurrent(agentIndex, ref current, current.ObservedAgeYears, current.ObservedTick);
            }
            current = PersonalCenturyAccumulatorState.Start(entry);
        }
        else
        {
            AdvanceCompletedCenturies(
                agentIndex,
                ref current,
                entry.AgeYears / PersonalChronicleRules.YearsPerVolume);
        }

        current.Record(entry);
    }

    public void AdvanceTo(
        int agentIndex,
        int lifeNumber,
        long ageYears,
        SimulationTick tick,
        string personName,
        RealmStage realmStage,
        MinorStage minorStage)
    {
        ValidateAgent(agentIndex);
        ref var current = ref _current[agentIndex];
        if (!current.Initialized)
        {
            current = PersonalCenturyAccumulatorState.StartEmpty(
                lifeNumber,
                ageYears,
                tick,
                personName,
                realmStage,
                minorStage);
        }
        if (current.LifeNumber != lifeNumber)
        {
            return;
        }

        AdvanceCompletedCenturies(
            agentIndex,
            ref current,
            ageYears / PersonalChronicleRules.YearsPerVolume);
        current.Observe(ageYears, tick, personName, realmStage, minorStage);
    }

    public IReadOnlyList<PersonalCenturyChronicleEntry> Project(
        int agentIndex,
        int currentLifeNumber,
        long currentAgeYears,
        SimulationTick currentTick,
        string currentName,
        RealmStage currentRealm,
        MinorStage currentMinor,
        bool currentLifeEnded)
    {
        ValidateAgent(agentIndex);
        var completed = _completed[agentIndex];
        var current = _current[agentIndex];
        var projectedCount = current.Initialized && current.LifeNumber == currentLifeNumber
            ? checked((int)Math.Min(
                PersonalChronicleRules.VolumeRetentionLimit,
                (currentAgeYears / PersonalChronicleRules.YearsPerVolume)
                    - current.CenturyIndex + 1))
            : 0;
        var result = new List<PersonalCenturyChronicleEntry>(Math.Min(
            PersonalChronicleRules.VolumeRetentionLimit,
            (completed?.Count ?? 0) + projectedCount));
        if (completed is not null)
        {
            var skip = Math.Max(
                0,
                completed.Count + projectedCount - PersonalChronicleRules.VolumeRetentionLimit);
            for (var index = skip; index < completed.Count; index++)
            {
                result.Add(Expand(agentIndex, completed[index]));
            }
        }

        if (projectedCount > 0)
        {
            var targetCentury = currentAgeYears / PersonalChronicleRules.YearsPerVolume;
            var firstCentury = Math.Max(
                current.CenturyIndex,
                targetCentury - PersonalChronicleRules.VolumeRetentionLimit + 1);
            for (var century = firstCentury; century <= targetCentury; century++)
            {
                result.Add(ProjectCurrent(
                    agentIndex,
                    current,
                    century,
                    targetCentury,
                    currentAgeYears,
                    currentTick,
                    currentName,
                    currentRealm,
                    currentMinor,
                    currentLifeEnded));
            }
        }

        return result.Count <= PersonalChronicleRules.VolumeRetentionLimit
            ? result
            : result.TakeLast(PersonalChronicleRules.VolumeRetentionLimit).ToArray();
    }

    public IReadOnlyList<PersonalCenturyChronicleEntry> CreateCompletedCheckpoint(
        int agentIndex,
        bool materialize)
    {
        ValidateAgent(agentIndex);
        var view = new AgentCompletedArchive(this, agentIndex);
        return materialize ? view.ToArray() : view;
    }

    public PersonalCenturyAccumulatorCheckpoint? CreateAccumulatorCheckpoint(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _current[agentIndex].Initialized
            ? _current[agentIndex].CreateCheckpoint()
            : null;
    }

    public void Restore(
        int agentIndex,
        IReadOnlyList<PersonalCenturyChronicleEntry> completed,
        PersonalCenturyAccumulatorCheckpoint? current)
    {
        ValidateAgent(agentIndex);
        if (completed.Count > PersonalChronicleRules.VolumeRetentionLimit)
        {
            throw new InvalidDataException("개인 100년 종합 연대기가 100권을 초과했습니다.");
        }
        _completed[agentIndex] = null;
        if (completed.Count > 0)
        {
            var list = new List<CompactPersonalCenturyEntry>(
                Math.Min(
                    PersonalChronicleRules.VolumeRetentionLimit,
                    Math.Max(InitialCompletedVolumeCapacity, completed.Count)));
            foreach (var entry in completed)
            {
                _details.RegisterPersonNameForLife(
                    agentIndex,
                    entry.LifeNumber,
                    entry.PersonName);
                list.Add(CompactPersonalCenturyEntry.Create(entry));
            }
            _completed[agentIndex] = list;
        }
        _current[agentIndex] = current is null
            ? default
            : PersonalCenturyAccumulatorState.Restore(current);
    }

    public void Rebuild(int agentIndex, IEnumerable<LifeChronicleEntry> detailed)
    {
        ValidateAgent(agentIndex);
        _completed[agentIndex] = null;
        _current[agentIndex] = default;
        foreach (var entry in detailed)
        {
            Record(agentIndex, entry);
        }
    }

    private void AdvanceCompletedCenturies(
        int agentIndex,
        ref PersonalCenturyAccumulatorState current,
        long targetCentury)
    {
        while (current.CenturyIndex < targetCentury)
        {
            var endAge = checked(
                ((current.CenturyIndex + 1) * PersonalChronicleRules.YearsPerVolume) - 1);
            var endTick = AddYearsClamped(current.LifeStartedTick, endAge, long.MaxValue);
            CompleteCurrent(agentIndex, ref current, endAge, endTick);
            current = current.StartNextCentury();
        }
    }

    private void CompleteCurrent(
        int agentIndex,
        ref PersonalCenturyAccumulatorState current,
        long observedThroughAge,
        long observedThroughTick)
    {
        var completed = _completed[agentIndex]
            ??= new List<CompactPersonalCenturyEntry>(
                InitialCompletedVolumeCapacity);
        if (completed.Count == PersonalChronicleRules.VolumeRetentionLimit)
        {
            var evictedLife = completed[0].LifeNumber;
            completed.RemoveAt(0);
            if (!_details.IsProtected(agentIndex)
                && !RetainsLife(agentIndex, evictedLife))
            {
                _details.PrunePersonNameForLife(agentIndex, evictedLife);
            }
        }
        else if (completed.Count == completed.Capacity)
        {
            completed.Capacity = Math.Min(
                PersonalChronicleRules.VolumeRetentionLimit,
                checked(completed.Capacity * 2));
        }
        completed.Add(current.Complete(observedThroughAge, observedThroughTick));
        _details.RemoveArchivedThrough(
            agentIndex,
            current.LifeNumber,
            observedThroughAge);
    }

    private bool RetainsLife(int agentIndex, int lifeNumber) =>
        (_current[agentIndex].Initialized
            && _current[agentIndex].LifeNumber == lifeNumber)
        || _completed[agentIndex]?.Any(entry => entry.LifeNumber == lifeNumber) == true;

    private PersonalCenturyChronicleEntry Expand(
        int agentIndex,
        CompactPersonalCenturyEntry entry)
    {
        var personId = PersonCausalRules.CreatePersonId(agentIndex, entry.LifeNumber);
        if (!_details.TryGetPersonName(agentIndex, personId, out var personName))
        {
            throw new InvalidOperationException(
                $"100년 종합 연대기의 인물 이름을 복원할 수 없습니다. "
                + $"Agent={agentIndex}, Life={entry.LifeNumber}, Person={personId.Value}");
        }
        return entry.Expand(personName);
    }

    private PersonalCenturyChronicleEntry ProjectCurrent(
        int agentIndex,
        PersonalCenturyAccumulatorState current,
        long century,
        long targetCentury,
        long currentAgeYears,
        SimulationTick currentTick,
        string currentName,
        RealmStage currentRealm,
        MinorStage currentMinor,
        bool currentLifeEnded)
    {
        var startAge = checked(century * PersonalChronicleRules.YearsPerVolume);
        var endAge = checked(startAge + PersonalChronicleRules.YearsPerVolume - 1);
        var isCurrentAccumulator = century == current.CenturyIndex;
        var complete = century < targetCentury || currentLifeEnded;
        var observedAge = complete && currentAgeYears > endAge
            ? endAge
            : Math.Min(endAge, currentAgeYears);
        var personId = PersonCausalRules.CreatePersonId(agentIndex, current.LifeNumber);
        var name = _details.TryGetPersonName(agentIndex, personId, out var storedName)
            ? storedName
            : currentName;
        return new PersonalCenturyChronicleEntry(
            current.LifeNumber,
            century,
            startAge,
            endAge,
            observedAge,
            name,
            complete,
            isCurrentAccumulator ? current.EventCount : 0,
            isCurrentAccumulator ? current.GrowthEventCount : 0,
            isCurrentAccumulator ? current.CombatEventCount : 0,
            isCurrentAccumulator ? current.LifeEventCount : 0,
            isCurrentAccumulator ? current.AcquisitionEventCount : 0,
            isCurrentAccumulator ? current.MajorRealmBreakthroughCount : 0,
            isCurrentAccumulator ? current.BreakthroughFailureCount : 0,
            century == targetCentury ? currentRealm : current.FinalRealmStage,
            century == targetCentury ? currentMinor : current.FinalMinorStage,
            new SimulationTick(AddYearsClamped(current.LifeStartedTick, startAge, currentTick.Value)),
            century == targetCentury
                ? currentTick
                : new SimulationTick(AddYearsClamped(
                    current.LifeStartedTick,
                    observedAge,
                    currentTick.Value)));
    }

    private static long AddYearsClamped(long startTick, long years, long maximumTick)
    {
        var yearsInTicks = years > long.MaxValue / GameCalendar.TicksPerYear
            ? long.MaxValue
            : years * GameCalendar.TicksPerYear;
        var value = startTick > long.MaxValue - yearsInTicks
            ? long.MaxValue
            : startTick + yearsInTicks;
        return Math.Min(value, maximumTick);
    }

    private int ValidateAgent(int agentIndex)
    {
        if ((uint)agentIndex >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
        return agentIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct CompactPersonalCenturyEntry(
        long CenturyIndex,
        long ObservedThroughAgeYears,
        long LifeStartedTick,
        long ObservedThroughTick,
        int LifeNumber,
        int EventCount,
        int GrowthEventCount,
        int CombatEventCount,
        int LifeEventCount,
        int AcquisitionEventCount,
        int MajorRealmBreakthroughCount,
        int BreakthroughFailureCount,
        RealmStage FinalRealmStage,
        MinorStage FinalMinorStage)
    {
        public static CompactPersonalCenturyEntry Create(PersonalCenturyChronicleEntry entry) => new(
            entry.CenturyIndex,
            entry.ObservedThroughAgeYears,
            Math.Max(0, entry.StartedTick.Value
                - checked(entry.StartAgeYears * GameCalendar.TicksPerYear)),
            entry.ObservedThroughTick.Value,
            entry.LifeNumber,
            entry.EventCount,
            entry.GrowthEventCount,
            entry.CombatEventCount,
            entry.LifeEventCount,
            entry.AcquisitionEventCount,
            entry.MajorRealmBreakthroughCount,
            entry.BreakthroughFailureCount,
            entry.FinalRealmStage,
            entry.FinalMinorStage);

        public PersonalCenturyChronicleEntry Expand(string personName)
        {
            var startAge = checked(CenturyIndex * PersonalChronicleRules.YearsPerVolume);
            var endAge = checked(startAge + PersonalChronicleRules.YearsPerVolume - 1);
            return new PersonalCenturyChronicleEntry(
                LifeNumber,
                CenturyIndex,
                startAge,
                endAge,
                ObservedThroughAgeYears,
                personName,
                IsComplete: true,
                EventCount,
                GrowthEventCount,
                CombatEventCount,
                LifeEventCount,
                AcquisitionEventCount,
                MajorRealmBreakthroughCount,
                BreakthroughFailureCount,
                FinalRealmStage,
                FinalMinorStage,
                new SimulationTick(AddYearsClamped(LifeStartedTick, startAge, long.MaxValue)),
                new SimulationTick(ObservedThroughTick));
        }
    }

    private struct PersonalCenturyAccumulatorState
    {
        public bool Initialized;
        public int LifeNumber;
        public long CenturyIndex;
        public long LifeStartedTick;
        public long ObservedAgeYears;
        public long ObservedTick;
        public int EventCount;
        public int GrowthEventCount;
        public int CombatEventCount;
        public int LifeEventCount;
        public int AcquisitionEventCount;
        public int MajorRealmBreakthroughCount;
        public int BreakthroughFailureCount;
        public RealmStage FinalRealmStage;
        public MinorStage FinalMinorStage;

        public static PersonalCenturyAccumulatorState Start(LifeChronicleEntry entry)
        {
            var state = StartEmpty(
                entry.LifeNumber,
                entry.AgeYears,
                entry.Tick,
                entry.PersonName,
                entry.RealmStage,
                entry.MinorStage);
            return state;
        }

        public static PersonalCenturyAccumulatorState StartEmpty(
            int lifeNumber,
            long ageYears,
            SimulationTick tick,
            string personName,
            RealmStage realmStage,
            MinorStage minorStage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(personName);
            return new PersonalCenturyAccumulatorState
            {
                Initialized = true,
                LifeNumber = lifeNumber,
                CenturyIndex = ageYears / PersonalChronicleRules.YearsPerVolume,
                LifeStartedTick = Math.Max(
                    0,
                    tick.Value - checked(ageYears * GameCalendar.TicksPerYear)),
                ObservedAgeYears = ageYears,
                ObservedTick = tick.Value,
                FinalRealmStage = realmStage,
                FinalMinorStage = minorStage,
            };
        }

        public static PersonalCenturyAccumulatorState Restore(
            PersonalCenturyAccumulatorCheckpoint checkpoint) => new()
        {
            Initialized = true,
            LifeNumber = checkpoint.LifeNumber,
            CenturyIndex = checkpoint.CenturyIndex,
            LifeStartedTick = checkpoint.LifeStartedTick,
            ObservedAgeYears = checkpoint.ObservedAgeYears,
            ObservedTick = checkpoint.ObservedTick,
            EventCount = checkpoint.EventCount,
            GrowthEventCount = checkpoint.GrowthEventCount,
            CombatEventCount = checkpoint.CombatEventCount,
            LifeEventCount = checkpoint.LifeEventCount,
            AcquisitionEventCount = checkpoint.AcquisitionEventCount,
            MajorRealmBreakthroughCount = checkpoint.MajorRealmBreakthroughCount,
            BreakthroughFailureCount = checkpoint.BreakthroughFailureCount,
            FinalRealmStage = checkpoint.FinalRealmStage,
            FinalMinorStage = checkpoint.FinalMinorStage,
        };

        public void Record(LifeChronicleEntry entry)
        {
            Observe(
                entry.AgeYears,
                entry.Tick,
                entry.PersonName,
                entry.RealmStage,
                entry.MinorStage);
            EventCount++;
            GrowthEventCount += PersonalChronicleRules.IsGrowth(entry.Kind) ? 1 : 0;
            CombatEventCount += PersonalChronicleRules.IsCombat(entry.Kind) ? 1 : 0;
            LifeEventCount += PersonalChronicleRules.IsLife(entry.Kind) ? 1 : 0;
            AcquisitionEventCount += PersonalChronicleRules.IsAcquisition(entry.Kind) ? 1 : 0;
            MajorRealmBreakthroughCount += entry.Kind == ChronicleEventKind.MajorRealmBreakthrough ? 1 : 0;
            BreakthroughFailureCount += entry.Kind == ChronicleEventKind.BreakthroughFailure ? 1 : 0;
        }

        public void Observe(
            long ageYears,
            SimulationTick tick,
            string personName,
            RealmStage realmStage,
            MinorStage minorStage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(personName);
            ObservedAgeYears = Math.Max(ObservedAgeYears, ageYears);
            ObservedTick = Math.Max(ObservedTick, tick.Value);
            FinalRealmStage = realmStage;
            FinalMinorStage = minorStage;
        }

        public CompactPersonalCenturyEntry Complete(long observedAge, long observedTick) => new(
            CenturyIndex,
            observedAge,
            LifeStartedTick,
            observedTick,
            LifeNumber,
            EventCount,
            GrowthEventCount,
            CombatEventCount,
            LifeEventCount,
            AcquisitionEventCount,
            MajorRealmBreakthroughCount,
            BreakthroughFailureCount,
            FinalRealmStage,
            FinalMinorStage);

        public PersonalCenturyAccumulatorState StartNextCentury() => new()
        {
            Initialized = true,
            LifeNumber = LifeNumber,
            CenturyIndex = checked(CenturyIndex + 1),
            LifeStartedTick = LifeStartedTick,
            ObservedAgeYears = checked(
                (CenturyIndex + 1) * PersonalChronicleRules.YearsPerVolume),
            ObservedTick = AddYearsClamped(
                LifeStartedTick,
                checked((CenturyIndex + 1) * PersonalChronicleRules.YearsPerVolume),
                long.MaxValue),
            FinalRealmStage = FinalRealmStage,
            FinalMinorStage = FinalMinorStage,
        };

        public PersonalCenturyAccumulatorCheckpoint CreateCheckpoint() => new(
            LifeNumber,
            CenturyIndex,
            LifeStartedTick,
            ObservedAgeYears,
            ObservedTick,
            EventCount,
            GrowthEventCount,
            CombatEventCount,
            LifeEventCount,
            AcquisitionEventCount,
            MajorRealmBreakthroughCount,
            BreakthroughFailureCount,
            FinalRealmStage,
            FinalMinorStage);
    }

    private readonly struct AgentCompletedArchive : IReadOnlyList<PersonalCenturyChronicleEntry>
    {
        private readonly PersonalChronicleArchiveStore _owner;
        private readonly int _agentIndex;

        public AgentCompletedArchive(PersonalChronicleArchiveStore owner, int agentIndex)
        {
            _owner = owner;
            _agentIndex = agentIndex;
        }

        public int Count => _owner._completed[_agentIndex]?.Count ?? 0;
        public PersonalCenturyChronicleEntry this[int index] =>
            _owner.Expand(_agentIndex, _owner._completed[_agentIndex]![index]);

        public IEnumerator<PersonalCenturyChronicleEntry> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public sealed partial class World
{
    public IReadOnlyList<PersonalCenturyChronicleEntry> GetAgentPersonalCenturyChronicle(
        AgentId agentId)
    {
        var index = ValidateAgent(agentId);
        return _agents.PersonalCenturyChronicles.Project(
            index,
            _agents.LifeNumbers[index],
            GetAgeYears(index),
            Tick,
            _agents.Names[index],
            _agents.RealmStages[index],
            (MinorStage)_agents.RealmLayers[index],
            _agents.LifeStates[index] != PersonLifeState.Living);
    }

    public void SetDetailedChronicleProtection(AgentId agentId, bool isProtected) =>
        _agents.PersonalCenturyChronicles.SetProtected(ValidateAgent(agentId), isProtected);

    public IReadOnlyList<AgentId> GetDetailedChronicleProtectionList()
    {
        var result = new List<AgentId>();
        for (var index = 0; index < Population; index++)
        {
            if (_agents.PersonalCenturyChronicles.IsProtected(index))
            {
                result.Add(new AgentId(index));
            }
        }
        return result;
    }
}
