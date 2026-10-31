using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game.Execution;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private readonly PopulationColumns executionColumns = new();
    private readonly StableEventQueue executionQueue = new();
    private readonly Dictionary<long, (TalentInputStamp Stamp, InnateTalentSnapshot Values)> executionTalents = [];
    private Person[]? executionReferencePeople;
    private bool scalarExecutionVerification;
    private ITalentBatchBackend executionTalentBackend = new ScalarTalentBackend();
    private long executionFrames, executionDueChecks, executionTalentCommits;

    // Verification-only choice; neither command dispatch nor save state exposes another authority.
    internal void UseScalarExecutionReferenceForVerification() => scalarExecutionVerification = true;
    internal void UseTalentBackendForVerification(ITalentBatchBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend); executionTalentBackend = backend; executionTalents.Clear();
    }
    internal (long Frames, long DueChecks, long TalentCommits, long QueueRebuilds) ExecutionDiagnostics =>
        (executionFrames, executionDueChecks, executionTalentCommits, executionQueue.Rebuilds);

    private ScopedLease<PopulationRow> BeginExecutionMonth()
    {
        if (scalarExecutionVerification)
        {
            executionReferencePeople = state.People.Where(p => p.Status == LifeStatus.Alive).ToArray();
            return new(executionReferencePeople.Length, i =>
            {
                var p = executionReferencePeople[i];
                return new(ExecutionIdentity.Of(p), i, 0, 0, p.Place, p.Health, p.Action, p.Due);
            }, () => { if (executionReferencePeople is null) throw new InvalidOperationException("The reference snapshot was returned."); },
                () => executionReferencePeople = null);
        }
        executionColumns.Refresh(state.People, state.ActiveSlots);
        foreach (var p in state.People) executionQueue.Reconcile(ActionSchedule.Of(p));
        executionQueue.BoundInvalidEntries(); executionQueue.Promote(Month); executionFrames++;
        return executionColumns.Borrow();
    }

    private bool TryExecutionPerson(PopulationRow row, out Person person)
    {
        // Do not skip a same-month death, movement or action change: the old Person[] held this exact object.
        person = scalarExecutionVerification ? executionReferencePeople![row.Ordinal] : executionColumns.Original(row);
        return true;
    }

    private bool ExecutionActionDue(Person person)
    {
        if (scalarExecutionVerification) return person.Action != ActionKind.Idle && person.Due <= Month;
        executionDueChecks++;
        return executionQueue.TakeDue(ActionSchedule.Of(person), Month);
    }

    private void ForgetExecutionPerson(Person person)
    {
        executionQueue.Cancel(person.Id); executionTalents.Remove(person.Id);
    }

    private TalentInputStamp ExecutionTalentStamp(Person person)
    {
        var profile = PeopleProfile(person);
        var birth = state.PeopleDomain.Inheritance.Births.FirstOrDefault(b => b.PersonId == person.Id);
        var conception = birth is null ? null : state.PeopleDomain.Inheritance.Conceptions.FirstOrDefault(c => c.Id == birth.ConceptionId);
        var source = JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile.Innate,
            Genome = state.PeopleDomain.Genomes.FirstOrDefault(g => g.PersonId == person.Id),
            Birth = birth, FrozenGeneticModel = conception?.Model
        });
        var slot = person.Slot >= 0 && person.Slot < state.ActiveSlots.Count ? state.ActiveSlots[person.Slot] : null;
        return new(ExecutionIdentity.Of(person), state.Revision, state.LawRevision, Convert.ToHexStringLower(SHA256.HashData(source)),
            person.Status, slot?.Person ?? 0, slot?.Generation ?? 0);
    }

    private int ExecutionInnateTalent(Person person, int index)
    {
        if ((uint)index >= 24) throw new ArgumentOutOfRangeException(nameof(index));
        if (scalarExecutionVerification) return PeopleAptitude(person).Talents[index].Innate;
        return ExecutionInnateTalents(person)!.Read(PeopleProfile(person).Innate)[index];
    }

    private InnateTalentSnapshot? ExecutionInnateTalents(Person person)
    {
        if (scalarExecutionVerification) return null;
        var stamp = ExecutionTalentStamp(person);
        if (executionTalents.TryGetValue(person.Id, out var cached) && cached.Stamp == stamp) return cached.Values;
        var innate = PeopleProfile(person).Innate;
        var input = new TalentBatchInput(stamp, innate.Foundations, innate.ElementRoots);
        // Scalar remains the product backend. SIMD exists solely as a comparison candidate until measured.
        var pending = InnateTalentBatch.Calculate(input, executionTalentBackend);
        var status = InnateTalentBatch.Commit(pending, ExecutionTalentStamp(person), out var values);
        if (status != TalentBatchStatus.Applied) throw new InvalidOperationException("Synchronous talent input changed before publication.");
        var complete = new InnateTalentSnapshot(innate, values);
        executionTalents[person.Id] = (stamp, complete); executionTalentCommits++; return complete;
    }
}
