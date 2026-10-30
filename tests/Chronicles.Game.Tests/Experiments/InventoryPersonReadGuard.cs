using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Experiments;

internal static class InventoryPersonReadGuard
{
    internal static readonly string[] Projected = ["Slot", "SlotGeneration", "Id", "BodyId", "SoulId", "Status", "Place", "Money", "Health", "Work", "Skills", "Equipment", "Action", "Due", "ActionTarget"];
    internal static readonly string[] Unused = ["Name", "Species", "Gestates", "Born", "Ended", "Lifespan", "ParentOne", "ParentTwo", "Partner", "Household", "Sect", "Realm", "Layer", "GreatPerfection", "Martial", "MartialWork", "NextBirth", "Goal", "Reason", "Mood", "LastCare", "Habits", "Decisions", "DecisionCandidates", "Plan", "LessonRequests", "Journal", "PlanExpansions", "LastPeopleRevision", "ObservationCursor", "ObservationRemaining", "ActionData", "ActionRevision", "Processes", "Components", "Irreversible", "PendingLayer", "StructureDamage", "BondStability", "SealedUntil", "Sealed", "SoulPactUntil", "SoulPactSource", "Roots", "Aptitudes", "Personality", "Formations", "Traits", "Understanding", "Beliefs", "ObservedPeople", "ObservedPlaces", "KnownPlaces", "KnownPeople", "Guardian"];
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!).GroupBy(op => unchecked((ushort)op.Value))
        .ToDictionary(g => g.Key, g => g.First());

    internal static IReadOnlyList<string> ForbiddenAccesses(IEnumerable<MethodBase> methods)
    {
        var errors = new List<string>();
        foreach (var method in methods)
        {
            var code = method.GetMethodBody()?.GetILAsByteArray(); if (code is null) continue;
            for (var offset = 0; offset < code.Length;)
            {
                ushort value = code[offset++]; if (value == 0xfe) value = (ushort)(0xfe00 | code[offset++]);
                var op = Opcodes[value];
                if (op.OperandType == OperandType.InlineMethod)
                {
                    var token = BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset));
                    var target = method.Module.ResolveMethod(token, method.DeclaringType?.GetGenericArguments(), (method as MethodInfo)?.GetGenericArguments());
                    if (target?.DeclaringType == typeof(Person) && (target.Name.StartsWith("get_", StringComparison.Ordinal) || target.Name.StartsWith("set_", StringComparison.Ordinal)))
                    {
                        var field = target.Name[4..];
                        var copyNormalizer = method.DeclaringType == typeof(InventoryState) && method.Name == "Copy" && field is "Understanding" or "Beliefs";
                        var passThrough = field == "Work" && ((method.DeclaringType == typeof(InventoryState) && method.Name == "ApplyTo") ||
                            (method.DeclaringType == typeof(InventoryEngine.OwnedTransaction) && method.Name == "CommitTo"));
                        var typedCopyTransport = method.DeclaringType == typeof(InventoryEngine.OwnedTransaction) && method.Name == "CopyOwnedPerson" &&
                            Projected.Contains(field, StringComparer.Ordinal);
                        var allowed = target.Name.StartsWith("get_", StringComparison.Ordinal) ? Projected.Contains(field, StringComparer.Ordinal)
                            : field is "Money" or "Health" or "Skills" or "Equipment" || passThrough || typedCopyTransport;
                        if (!allowed && !copyNormalizer) errors.Add($"{method.DeclaringType?.FullName}.{method.Name}: {target.Name}");
                    }
                    else if (target?.DeclaringType?.Assembly == typeof(Person).Assembly &&
                        target.DeclaringType.Namespace?.StartsWith("Chronicles.Game.Inventory", StringComparison.Ordinal) != true &&
                        (target.GetParameters().Any(p => p.ParameterType == typeof(Person)) || (target as MethodInfo)?.ReturnType == typeof(Person)))
                        errors.Add($"{method.DeclaringType?.FullName}.{method.Name}: unclassified Person helper {target}");
                }
                offset += op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(offset)),
                    _ => 4
                };
            }
        }
        return errors;
    }

    internal static IEnumerable<MethodBase> InventoryMethods() => typeof(InventoryEngine).Assembly.GetTypes()
        .Where(t => t.Namespace?.StartsWith("Chronicles.Game.Inventory", StringComparison.Ordinal) == true)
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Cast<MethodBase>()
            .Concat(t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)));
}
