using System.Collections.ObjectModel;
using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Application;

public sealed record AuthorityOwnerDiagnostic(
    string DomainRef,
    string OwnerRef,
    string SchemaRef,
    string MappingRef,
    string MutationRef);

public sealed record AuthorityRegistryDiagnosticsSnapshot(
    string Contract,
    int Revision,
    string ValidationStatus,
    string ActiveSchemaRef,
    string ActiveMappingRef,
    int EntryCount,
    int OwnerBindingCount,
    int ChangeSetCount,
    IReadOnlyList<AuthorityOwnerDiagnostic> OwnerBindings,
    IReadOnlyList<string> FormulaStableRefs);

internal static class AuthorityRegistryDiagnosticsProjection
{
    public static AuthorityRegistryDiagnosticsSnapshot Current { get; } = Create();

    private static AuthorityRegistryDiagnosticsSnapshot Create()
    {
        var registry = AuthorityRegistry.Current;
        var ownerBindings = registry.OwnerBindings
            .Select(binding => new AuthorityOwnerDiagnostic(
                ResolveRef(registry, binding.DomainId),
                ResolveRef(registry, binding.OwnerId),
                ResolveRef(registry, binding.SchemaId),
                ResolveRef(registry, binding.MappingId),
                ResolveRef(registry, binding.MutationOperationId)))
            .ToArray();
        var formulas = FormulaRegistry.All
            .Select(formula => $"{formula.StableId} r{formula.Revision}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new AuthorityRegistryDiagnosticsSnapshot(
            AuthorityRegistry.RegistryContract,
            registry.RegistryRevision,
            registry.Validation.IsValid
                ? "READY"
                : $"BLOCKED · {registry.Validation.Error}",
            ResolveRef(registry, registry.ActiveSchemaId),
            ResolveRef(registry, registry.ActiveMappingId),
            registry.Entries.Count,
            registry.OwnerBindings.Count,
            registry.ChangeSets.Count,
            new ReadOnlyCollection<AuthorityOwnerDiagnostic>(ownerBindings),
            new ReadOnlyCollection<string>(formulas));
    }

    private static string ResolveRef(
        AuthorityRegistrySnapshot registry,
        RegistryStableId id)
    {
        var resolved = registry.Resolve(id);
        return resolved.Entry is { } entry
            ? $"{entry.RegistryRef}:{entry.Key} · {entry.Id}"
            : $"{resolved.Status} · {id}";
    }
}
