using System.Text.Json;

namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    /// <summary>For serial GameWorld candidate execution only. The complete initial snapshot is always isolated.</summary>
    internal static OwnedTransaction Own(GameState world) => new(world);
    internal static OwnedTransaction OwnProjected(GameState world) => new(world, true);
    internal static OwnedTransaction OwnTypedProjectedForExperiment(GameState world) => new(world, true, true);

    internal readonly record struct OwnedResult(bool Accepted, string Message, long Subject = 0, bool Replayed = false);
    internal readonly record struct OwnedJob(long Id, string Recipe, long Due);

    /// <summary>
    /// Owns its snapshot exclusively: no mutable model is returned, even before commit. Every rejection or exception
    /// discards the whole transaction. Commit consumes ownership before validation and transfers it at most once.
    /// </summary>
    internal sealed partial class OwnedTransaction : IDisposable
    {
        private InventoryState? candidate;
        private GameState? source;
        private readonly bool projectedPerson;

        internal OwnedTransaction(GameState world) : this(world, false) { }
        internal OwnedTransaction(GameState world, bool projectedPerson) : this(world, projectedPerson, false) { }
        internal OwnedTransaction(GameState world, bool projectedPerson, bool typedCopy)
        {
            ArgumentNullException.ThrowIfNull(world); this.projectedPerson = projectedPerson;
            if (typedCopy && !projectedPerson) throw new ArgumentException("Typed copying requires the private Person projection.", nameof(typedCopy));
            candidate = !projectedPerson ? InventoryState.FromWorld(world, world.Inventory) : typedCopy
                ? CreateTypedProjectedSnapshot(world, ProjectedFingerprint(world)) : CreateProjectedSnapshot(world);
            source = world;
        }

        private InventoryState Open() => candidate ?? throw new InvalidOperationException("Inventory transaction is already consumed or discarded.");
        public void Dispose() { candidate = null; source = null; }

        internal OwnedResult Apply(InventoryCommand command)
        {
            var current = Open();
            try
            {
                ArgumentNullException.ThrowIfNull(command);
                var signature = command.GetType().Name + ":" + JsonSerializer.Serialize(command, command.GetType());
                if (string.IsNullOrWhiteSpace(command.Id)) return Reject("Command ID is required.");
                // FromWorld.Copy always restores an Ordinal comparer; the public custom-comparer path stays separate.
                current.Ledger.Receipts.TryGetValue(command.Id, out var receipt);
                if (receipt is not null)
                    return receipt.Signature == signature ? new(true, receipt.Message, receipt.Subject, true)
                        : Reject("Command ID already belongs to a different request.");
                Validate(current);
                long money = 0; string? moneyOverflow = null;
                try { money = TotalMoney(current); } catch (OverflowException error) { moneyOverflow = error.Message; }
                var subject = ExecuteMutation(current, command);
                current.Revision = checked(current.Revision + 1);
                current.Ledger.Receipts.Add(command.Id, new(signature, subject, "Accepted."));
                Validate(current);
                // The public path checks the unchanged input sum here. Preserve its error precedence as well as value.
                if (moneyOverflow is not null) throw new OverflowException(moneyOverflow);
                Require(money == TotalMoney(current), "Inventory transition changed total money without a real counterparty.");
                return new(true, "Accepted.", subject);
            }
            catch (Exception error) when (error is RuleViolation or InvalidDataException or OverflowException)
            { return Reject(error.Message); }
            catch { Dispose(); throw; }
        }

        private OwnedResult Reject(string message) { Dispose(); return new(false, message); }

        internal int ItemQuantity(long item)
        {
            var current = Open();
            try { return current.Items.Single(i => i.Id == item).Quantity; }
            catch { Dispose(); throw; }
        }

        internal OwnedJob Job(long id)
        {
            var current = Open();
            try { var job = current.Ledger.Jobs.Single(j => j.Id == id); return new(job.Id, job.Recipe, job.Due); }
            catch { Dispose(); throw; }
        }

        internal void AttachKnowledgeActivity(long job, long activity)
        {
            var current = Open();
            try { current.Ledger.Jobs.Single(j => j.Id == job).KnowledgeActivity = activity; }
            catch { Dispose(); throw; }
        }

        internal void TuneIfNeeded(TuneInventoryProfile command)
        {
            var current = Open();
            try
            {
                if (current.Ledger.BaseCarryCapacity == command.CarryCapacity && current.Ledger.ProductionWork == command.ProductionWork &&
                    current.Ledger.ProfessionalWork == command.ProfessionalWork && current.Ledger.ExtractionWorkPerUnit == command.ExtractionWork) return;
                var result = Apply(command);
                if (!result.Accepted) throw new InvalidDataException(result.Message);
            }
            catch { Dispose(); throw; }
        }

        internal void SynchronizeWorldActions()
        {
            var current = Open();
            try
            {
                foreach (var labor in current.Ledger.Labor.Where(l => l.Purpose == "world-action")) labor.Active = false;
                // Fresh for this call: subsequent commands may cancel jobs or finish cargo on the same owned snapshot.
                var activeJobs = current.Ledger.Jobs.Where(j => j.Status is "reserved" or "processing").Select(j => (j.Actor, j.Id)).ToHashSet();
                var activeCarriers = current.Ledger.Contracts.Where(c => c.Status == "open").Select(c => (c.Carrier, c.Id)).ToHashSet();
                var lastLabor = new Dictionary<(long Person, long End), LaborReservation>();
                foreach (var labor in current.Ledger.Labor.Where(l => l.Purpose == "world-action"))
                    lastLabor[(labor.Person, labor.End)] = labor;
                foreach (var p in current.People.Where(p => p.Status == LifeStatus.Alive && p.Action != ActionKind.Idle && p.Due > current.Month))
                {
                    if (activeJobs.Contains((p.Id, p.ActionTarget)) || activeCarriers.Contains((p.Id, p.ActionTarget))) continue;
                    if (!lastLabor.TryGetValue((p.Id, p.Due), out var row))
                    {
                        row = new() { Id = current.NextId++, Person = p.Id, Purpose = "world-action", Start = current.Month, End = p.Due, Units = 100 };
                        current.Ledger.Labor.Add(row); lastLabor[(p.Id, p.Due)] = row;
                    }
                    else row.Active = true;
                }
            }
            catch { Dispose(); throw; }
        }

        internal void CommitTo(GameState world)
        {
            var owned = Open(); var original = source; Dispose();
            ArgumentNullException.ThrowIfNull(world);
            if (!ReferenceEquals(world, original)) throw new InvalidOperationException("Inventory transaction belongs to a different world.");
            Validate(owned);
            var currentFingerprint = projectedPerson ? ProjectedFingerprint(world) : WorldFingerprint(world);
            if (owned.BaseFingerprint.Length == 0 || owned.BaseFingerprint != currentFingerprint)
                throw new InvalidOperationException("Inventory candidate is stale; create it from the current world.");
            if (!world.People.Select(p => p.Id).SequenceEqual(owned.People.Select(p => p.Id)))
                throw new InvalidOperationException("Inventory candidates cannot create, reorder, or remove people.");
            foreach (var pair in world.People.Zip(owned.People))
            {
                pair.First.Money = pair.Second.Money; pair.First.Health = pair.Second.Health; pair.First.Work = pair.Second.Work;
                pair.First.Skills = pair.Second.Skills; pair.First.Equipment = pair.Second.Equipment;
            }
            world.Items = owned.Items; world.Materials = owned.Materials; world.Inventory = owned.Ledger;
            world.NextId = owned.NextId; world.Revision = owned.Revision;
        }
    }
}
