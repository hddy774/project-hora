using System.Collections.ObjectModel;

namespace Chronicles.Game.Inventory;

public sealed record InventoryDefinition(ItemDefinition Item, IReadOnlyList<int> OccupiedSlots, string Effect = "", int Magnitude = 0);
public sealed record ProductionRecipe(string Id, string Skill, string Output, int Quantity,
    IReadOnlyDictionary<string, int> Inputs);
public sealed record ResourceRoute(string Definition, string Family, string Producer, string Consumer);

/// <summary>Shared definitions are immutable and are never created by opening a query.</summary>
public static class InventoryDefinitions
{
    private static readonly ReadOnlyCollection<int> TwoHands = Array.AsReadOnly(new[] { 0, 1 });
    public static IReadOnlyDictionary<string, InventoryDefinition> Items { get; } = BuildItems();
    public static IReadOnlyDictionary<string, ProductionRecipe> Recipes { get; } = BuildRecipes();
    public static IReadOnlyList<ResourceRoute> Resources { get; } = BuildRoutes();

    public static InventoryDefinition Definition(string id) => Items.TryGetValue(id, out var definition)
        ? definition : throw new InvalidDataException($"Unknown inventory definition: {id}");

    private static ReadOnlyDictionary<string, InventoryDefinition> BuildItems()
    {
        var items = new Dictionary<string, InventoryDefinition>(StringComparer.Ordinal);
        foreach (var definition in Catalog.Items.Values)
        {
            var effect = definition.Kind switch
            {
                ItemKind.Pill => "healing", ItemKind.Food => "nutrition", ItemKind.Talisman => "defense", _ => ""
            };
            items.Add(definition.Id, new(definition,
                Array.AsReadOnly(definition.EquipmentSlot < 0 ? [] : new[] { definition.EquipmentSlot }), effect,
                effect == "healing" ? 20 : 10));
        }
        items.Add("growthPill", new(new("growthPill", "배원단", ItemKind.Pill, 1, 0, -1, "수행 보조"), Array.Empty<int>(), "growth", 100));
        items.Add("breakthroughPill", new(new("breakthroughPill", "파경단", ItemKind.Pill, 1, 0, -1, "돌파 보조"), Array.Empty<int>(), "breakthrough", 10));
        items.Add("greatWeapon", new(new("greatWeapon", "양수 중검", ItemKind.Weapon, 3, 0, 0, "양손 점유"), TwoHands));
        foreach (var resource in ResourceCatalog.All)
        {
            var family = int.Parse(resource.Id.AsSpan(9, 2), System.Globalization.CultureInfo.InvariantCulture);
            var kind = family switch
            {
                0 => ItemKind.Ore, 1 or 2 or 10 or 15 => ItemKind.Metal, 3 or 12 => ItemKind.Herb,
                4 or 8 or 14 => ItemKind.Wood, 9 => ItemKind.Paper, 11 => ItemKind.Food,
                5 or 13 => ItemKind.Cloth, _ => ItemKind.Herb
            };
            items.Add(resource.Id, new(new(resource.Id, resource.Name, kind, family is 0 or 1 ? 2 : 1, 0, -1, resource.Cycle),
                Array.Empty<int>(), family == 11 ? "nutrition" : "", family == 11 ? 10 : 0));
        }
        return new(items);
    }

    private static ReadOnlyDictionary<string, ProductionRecipe> BuildRecipes()
    {
        var recipes = new Dictionary<string, ProductionRecipe>(StringComparer.Ordinal);
        void Add(string id, string skill, string output, int count, params (string Definition, int Count)[] inputs) =>
            recipes.Add(id, new(id, skill, output, count, new ReadOnlyDictionary<string, int>(
                inputs.ToDictionary(x => x.Definition, x => x.Count, StringComparer.Ordinal))));
        foreach (var recipe in Catalog.Recipes)
            recipes.Add(recipe.Id, new(recipe.Id, recipe.Skill, recipe.Output, recipe.Quantity,
                new ReadOnlyDictionary<string, int>(recipe.Inputs.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal))));
        Add("growthPill", "alchemy", "growthPill", 1, ("resource.03.1", 2), ("resource.06.0", 1), ("resource.07.0", 1));
        Add("breakthroughPill", "alchemy", "breakthroughPill", 1, ("resource.03.3", 3), ("resource.02.1", 1), ("resource.07.1", 1));
        Add("greatWeapon", "smith", "greatWeapon", 1, ("resource.01.3", 3), ("resource.04.0", 1), ("resource.07.0", 1));
        for (var variant = 0; variant < 4; variant++)
        {
            string R(int family) => $"resource.{family:00}.{variant}";
            Add($"produce.{R(1)}", "smith", R(1), 1, (R(0), 2), (R(7), 1));
            Add($"produce.{R(9)}", "talisman", R(9), 2, (R(4), 1), (R(6), 1));
            Add($"produce.{R(10)}", "array", R(10), 1, (R(1), 1), (R(2), 1));
            Add($"produce.{R(13)}", "farm", R(13), 2, (R(5), 1), (R(6), 1));
            Add($"produce.{R(14)}", "scholar", R(14), 1, (R(4), 1), (R(13), 1));
            Add($"consume.{R(0)}", "smith", "tool", 1, (R(0), 2), (R(4), 1));
            Add($"consume.{R(1)}", "smith", "weapon", 1, (R(1), 2), (R(4), 1));
            Add($"consume.{R(2)}", "array", "array", 1, (R(2), 1), (R(10), 1));
            Add($"consume.{R(3)}", "alchemy", variant < 2 ? "pill" : "growthPill", 1, (R(3), 2), (R(6), 1));
            Add($"consume.{R(4)}", "farm", "paper", 2, (R(4), 1));
            Add($"consume.{R(5)}", "smith", "armor", 1, (R(5), 2), (R(1), 1));
            Add($"consume.{R(6)}", "alchemy", "pill", 1, (R(6), 1), (R(3), 2));
            Add($"consume.{R(7)}", "smith", R(1), 1, (R(7), 1), (R(0), 2));
            Add($"consume.{R(8)}", "talisman", "soulWard", 1, (R(8), 1), (R(9), 1));
            Add($"consume.{R(9)}", "talisman", "talisman", 1, (R(9), 2));
            Add($"consume.{R(10)}", "array", "array", 1, (R(10), 2));
            Add($"consume.{R(11)}", "farm", "food", 2, (R(11), 2), (R(6), 1));
            Add($"consume.{R(12)}", "farm", R(11), 3, (R(12), 1), (R(6), 1));
            Add($"consume.{R(13)}", "smith", "cloth", 2, (R(13), 2));
            Add($"consume.{R(14)}", "scholar", "path.1.0", 1, (R(14), 2), (R(9), 1));
            Add($"consume.{R(15)}", "smith", "bag", 1, (R(15), 1), (R(13), 2));
        }
        return new(recipes);
    }

    private static ReadOnlyCollection<ResourceRoute> BuildRoutes() => ResourceCatalog.All.Select(resource =>
    {
        var family = int.Parse(resource.Id.AsSpan(9, 2), System.Globalization.CultureInfo.InvariantCulture);
        var producer = family switch
        {
            1 or 9 or 10 or 13 or 14 => $"produce.{resource.Id}",
            0 or 2 or 7 or 15 => "source.mining", 5 => "source.hunting", 6 => "source.collecting",
            11 or 12 => "source.farming", _ => "source.gathering"
        };
        return new ResourceRoute(resource.Id, resource.Family, producer, $"consume.{resource.Id}");
    }).ToList().AsReadOnly();
}
