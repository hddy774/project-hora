using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    // Internal experiment only. No projected mutable candidate is returned by any accessible API.
    internal sealed partial class OwnedTransaction
    {
        private static readonly HashSet<string> PersonFields = new(StringComparer.Ordinal)
        {
            nameof(Person.Slot), nameof(Person.SlotGeneration), nameof(Person.Id), nameof(Person.BodyId), nameof(Person.SoulId),
            nameof(Person.Status), nameof(Person.Place), nameof(Person.Money), nameof(Person.Health), nameof(Person.Work),
            nameof(Person.Skills), nameof(Person.Equipment), nameof(Person.Action), nameof(Person.Due), nameof(Person.ActionTarget)
        };
        private static readonly JsonSerializerOptions ProjectOptions = new()
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { FilterPerson } }
        };
        private static readonly JsonSerializerOptions CopyOptions = new() { PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate };

        internal static bool IncludesProjectedPersonProperty(string name) => PersonFields.Contains(name);

        private static void FilterPerson(JsonTypeInfo info)
        {
            if (info.Type != typeof(Person)) return;
            for (var index = info.Properties.Count - 1; index >= 0; index--)
                if (!PersonFields.Contains(info.Properties[index].Name)) info.Properties.RemoveAt(index);
        }

        private static string ProjectedFingerprint(GameState world) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { world.Month, world.NextId, world.Revision, world.Items, world.People, world.Materials }, ProjectOptions)));

        private static InventoryState CreateProjectedSnapshot(GameState world)
        {
            ArgumentNullException.ThrowIfNull(world);
            var input = new InventoryState { Month = world.Month, NextId = world.NextId, Revision = world.Revision,
                People = world.People, Items = world.Items, Materials = world.Materials, Ledger = world.Inventory, BaseFingerprint = ProjectedFingerprint(world) };
            // Only Person input is filtered. Items, materials, ledger and all their mutable children are fully isolated.
            var copy = JsonSerializer.Deserialize<InventoryState>(JsonSerializer.SerializeToUtf8Bytes(input, ProjectOptions), CopyOptions)
                ?? throw new InvalidDataException("Empty projected inventory state.");
            copy.Materials = new(copy.Materials, StringComparer.Ordinal); copy.Ledger.Receipts = new(copy.Ledger.Receipts, StringComparer.Ordinal);
            foreach (var person in copy.People) person.Skills = new(person.Skills, StringComparer.Ordinal);
            InventoryEngine.Validate(copy); return copy;
        }
    }
}
