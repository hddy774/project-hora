using System.Text.Json;

namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    /// <summary>For serial GameWorld candidate execution only. The complete initial snapshot is always isolated.</summary>
    internal static OwnedTransaction Own(GameState world) => new(world);

    internal readonly record struct OwnedResult(bool Accepted, string Message, long Subject = 0, bool Replayed = false);
    internal readonly record struct OwnedJob(long Id, string Recipe, long Due);

    /// <summary>
    /// Owns its snapshot exclusively: no mutable model is returned, even before commit. Every rejection or exception
    /// discards the whole transaction. Commit consumes ownership before validation and transfers it at most once.
    /// </summary>
    internal sealed class OwnedTransaction : IDisposable
    {
        private InventoryState? candidate;
        private GameState? source;

        internal OwnedTransaction(GameState world)
        { ArgumentNullException.ThrowIfNull(world); candidate = InventoryState.FromWorld(world, world.Inventory); source = world; }

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
                var receipt = current.Ledger.Receipts.FirstOrDefault(entry => string.Equals(entry.Key, command.Id, StringComparison.Ordinal)).Value;
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
                foreach (var p in current.People.Where(p => p.Status == LifeStatus.Alive && p.Action != ActionKind.Idle && p.Due > current.Month))
                {
                    if (current.Ledger.Jobs.Any(j => j.Actor == p.Id && j.Id == p.ActionTarget && j.Status is "reserved" or "processing") ||
                        current.Ledger.Contracts.Any(c => c.Carrier == p.Id && c.Id == p.ActionTarget && c.Status == "open")) continue;
                    var row = current.Ledger.Labor.LastOrDefault(l => l.Person == p.Id && l.Purpose == "world-action" && l.End == p.Due);
                    if (row is null) current.Ledger.Labor.Add(new() { Id = current.NextId++, Person = p.Id, Purpose = "world-action", Start = current.Month, End = p.Due, Units = 100 });
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
            if (owned.BaseFingerprint.Length == 0 || owned.BaseFingerprint != WorldFingerprint(world))
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
