using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Chronicles.Core;

public readonly record struct RegistryStableId(ulong Value)
{
    public bool IsValid => Value != 0;

    public override string ToString() => IsValid ? $"R{Value:X16}" : "Undefined";
}

public enum RegistryCatalogKind : byte
{
    Domain,
    Target,
    SemanticState,
    Unit,
    Operation,
    Owner,
    Schema,
    Mapping,
    Formula,
}

public enum RegistryEntryState : byte
{
    Active,
    Deprecated,
    Retired,
}

public enum RegistryChangeKind : byte
{
    Added,
    Deprecated,
    Retired,
    Replaced,
    Migrated,
}

public readonly record struct RegistryEntry(
    RegistryStableId Id,
    string RegistryRef,
    string Key,
    RegistryCatalogKind Kind,
    int IntroducedRevision,
    RegistryEntryState State = RegistryEntryState.Active,
    int RetiredRevision = 0,
    RegistryStableId ReplacementId = default);

public readonly record struct AuthorityOwnerBinding(
    RegistryStableId DomainId,
    RegistryStableId OwnerId,
    RegistryStableId SchemaId,
    RegistryStableId MappingId,
    RegistryStableId MutationOperationId);

public readonly record struct RegistryChangeSet(
    long Sequence,
    int Revision,
    RegistryChangeKind Kind,
    RegistryStableId EntryId,
    RegistryStableId PreviousEntryId = default);

public enum RegistryValidationError : byte
{
    None,
    InvalidRegistryRevision,
    MissingActiveSchema,
    MissingActiveMapping,
    EmptyRegistryRef,
    EmptyKey,
    InvalidStableId,
    DuplicateStableId,
    DuplicateKey,
    InvalidIntroducedRevision,
    InvalidRetiredRevision,
    ActiveEntryHasRetirement,
    MissingReplacement,
    ReplacementIsSelf,
    ReplacementNotFound,
    ReplacementRegistryMismatch,
    ReplacementIsRetired,
    OrphanOwnerBinding,
    DuplicateDomainOwner,
    InvalidChangeSequence,
    OrphanChangeEntry,
}

public readonly record struct RegistryValidationResult(
    RegistryValidationError Error,
    int EntryIndex = -1,
    int RelatedIndex = -1)
{
    public bool IsValid => Error == RegistryValidationError.None;

    public static RegistryValidationResult Valid => new(RegistryValidationError.None);
}

public enum RegistryResolveStatus : byte
{
    Ready,
    Deprecated,
    Replaced,
    Undefined,
    Blocked,
}

public readonly record struct RegistryResolveResult(
    RegistryResolveStatus Status,
    RegistryStableId Requested,
    RegistryStableId Resolved,
    RegistryEntry? Entry)
{
    public bool IsUsable => Status is RegistryResolveStatus.Ready
        or RegistryResolveStatus.Deprecated
        or RegistryResolveStatus.Replaced;
}

public sealed class AuthorityRegistrySnapshot
{
    private readonly ReadOnlyCollection<RegistryEntry> _entries;
    private readonly ReadOnlyCollection<AuthorityOwnerBinding> _owners;
    private readonly ReadOnlyCollection<RegistryChangeSet> _changes;
    private readonly Dictionary<ulong, int> _entryIndexById;

    public AuthorityRegistrySnapshot(
        int registryRevision,
        RegistryStableId activeSchemaId,
        RegistryStableId activeMappingId,
        IEnumerable<RegistryEntry> entries,
        IEnumerable<AuthorityOwnerBinding> owners,
        IEnumerable<RegistryChangeSet> changes)
    {
        RegistryRevision = registryRevision;
        ActiveSchemaId = activeSchemaId;
        ActiveMappingId = activeMappingId;
        _entries = Array.AsReadOnly(entries.OrderBy(entry => entry.Id.Value).ToArray());
        _owners = Array.AsReadOnly(owners.OrderBy(owner => owner.DomainId.Value).ToArray());
        _changes = Array.AsReadOnly(changes.OrderBy(change => change.Sequence).ToArray());
        Validation = AuthorityRegistry.Validate(
            RegistryRevision,
            ActiveSchemaId,
            ActiveMappingId,
            _entries,
            _owners,
            _changes);
        _entryIndexById = new Dictionary<ulong, int>(_entries.Count);
        if (Validation.IsValid)
        {
            for (var index = 0; index < _entries.Count; index++)
            {
                _entryIndexById.Add(_entries[index].Id.Value, index);
            }
        }
    }

    public int RegistryRevision { get; }

    public RegistryStableId ActiveSchemaId { get; }

    public RegistryStableId ActiveMappingId { get; }

    public IReadOnlyList<RegistryEntry> Entries => _entries;

    public IReadOnlyList<AuthorityOwnerBinding> OwnerBindings => _owners;

    public IReadOnlyList<RegistryChangeSet> ChangeSets => _changes;

    public RegistryValidationResult Validation { get; }

    public RegistryResolveResult Resolve(RegistryStableId requested)
    {
        if (!Validation.IsValid)
        {
            return new RegistryResolveResult(
                RegistryResolveStatus.Blocked,
                requested,
                default,
                null);
        }

        if (!requested.IsValid || !_entryIndexById.TryGetValue(requested.Value, out var index))
        {
            return new RegistryResolveResult(
                RegistryResolveStatus.Undefined,
                requested,
                default,
                null);
        }

        var entry = _entries[index];
        return entry.State switch
        {
            RegistryEntryState.Active => new RegistryResolveResult(
                RegistryResolveStatus.Ready,
                requested,
                entry.Id,
                entry),
            RegistryEntryState.Deprecated => new RegistryResolveResult(
                RegistryResolveStatus.Deprecated,
                requested,
                entry.Id,
                entry),
            RegistryEntryState.Retired => ResolveReplacement(requested, entry),
            _ => new RegistryResolveResult(
                RegistryResolveStatus.Blocked,
                requested,
                default,
                null),
        };
    }

    public bool TryGetOwner(RegistryStableId domainId, out AuthorityOwnerBinding binding)
    {
        var low = 0;
        var high = _owners.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = _owners[middle];
            var comparison = candidate.DomainId.Value.CompareTo(domainId.Value);
            if (comparison == 0)
            {
                binding = candidate;
                return true;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        binding = default;
        return false;
    }

    private RegistryResolveResult ResolveReplacement(
        RegistryStableId requested,
        RegistryEntry retired)
    {
        if (!_entryIndexById.TryGetValue(retired.ReplacementId.Value, out var replacementIndex))
        {
            return new RegistryResolveResult(
                RegistryResolveStatus.Blocked,
                requested,
                default,
                null);
        }

        var replacement = _entries[replacementIndex];
        return new RegistryResolveResult(
            RegistryResolveStatus.Replaced,
            requested,
            replacement.Id,
            replacement);
    }
}

public static class AuthorityRegistry
{
    public const int CurrentRevision = 1;
    public const string RegistryContract = "CMIP_AUTHORITY_REGISTRY_V1";

    public static AuthorityRegistrySnapshot Current { get; } = CreateCurrent();

    public static RegistryStableId CreateStableId(string registryRef, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return CreateStableId(registryRef, key.AsSpan());
    }

    /// <summary>
    /// 16자리 대문자 16진수 key의 기존 문자열 계약을 할당 없이 계산한다.
    /// 문자열 overload의 <c>value.ToString("X16", InvariantCulture)</c>와 같은 ID다.
    /// </summary>
    public static RegistryStableId CreateStableId(string registryRef, ulong numericKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryRef);
        Span<char> key = stackalloc char[16];
        if (!numericKey.TryFormat(
                key,
                out var written,
                "X16",
                CultureInfo.InvariantCulture)
            || written != key.Length)
        {
            throw new InvalidOperationException("numeric stable key 형식화에 실패했습니다.");
        }
        return CreateStableId(registryRef, key);
    }

    private static RegistryStableId CreateStableId(
        string registryRef,
        ReadOnlySpan<char> key)
    {
        const ulong offset = 14_695_981_039_346_656_037UL;
        const ulong prime = 1_099_511_628_211UL;
        var hash = offset;
        HashUtf8("xianxia-alpha110-registry-v1", ref hash, prime);
        HashUtf8(registryRef, ref hash, prime);
        hash ^= 0xff;
        hash *= prime;
        HashUtf8(key, ref hash, prime);
        return new RegistryStableId(hash == 0 ? 1UL : hash);
    }

    public static RegistryValidationResult Validate(
        int registryRevision,
        RegistryStableId activeSchemaId,
        RegistryStableId activeMappingId,
        IReadOnlyList<RegistryEntry> entries,
        IReadOnlyList<AuthorityOwnerBinding> owners,
        IReadOnlyList<RegistryChangeSet> changes)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(changes);
        if (registryRevision <= 0)
        {
            return new RegistryValidationResult(RegistryValidationError.InvalidRegistryRevision);
        }

        var idIndex = new Dictionary<ulong, int>(entries.Count);
        var keyIndex = new Dictionary<(string RegistryRef, string Key), int>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.RegistryRef))
            {
                return new RegistryValidationResult(RegistryValidationError.EmptyRegistryRef, index);
            }

            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                return new RegistryValidationResult(RegistryValidationError.EmptyKey, index);
            }

            if (!entry.Id.IsValid || entry.Id != CreateStableId(entry.RegistryRef, entry.Key))
            {
                return new RegistryValidationResult(RegistryValidationError.InvalidStableId, index);
            }

            if (!idIndex.TryAdd(entry.Id.Value, index))
            {
                return new RegistryValidationResult(
                    RegistryValidationError.DuplicateStableId,
                    index,
                    idIndex[entry.Id.Value]);
            }

            if (!keyIndex.TryAdd((entry.RegistryRef, entry.Key), index))
            {
                return new RegistryValidationResult(
                    RegistryValidationError.DuplicateKey,
                    index,
                    keyIndex[(entry.RegistryRef, entry.Key)]);
            }

            if (entry.IntroducedRevision <= 0 || entry.IntroducedRevision > registryRevision)
            {
                return new RegistryValidationResult(
                    RegistryValidationError.InvalidIntroducedRevision,
                    index);
            }

            if (entry.State is RegistryEntryState.Active or RegistryEntryState.Deprecated)
            {
                if (entry.RetiredRevision != 0 || entry.ReplacementId.IsValid)
                {
                    return new RegistryValidationResult(
                        RegistryValidationError.ActiveEntryHasRetirement,
                        index);
                }
            }
            else if (entry.RetiredRevision <= entry.IntroducedRevision
                     || entry.RetiredRevision > registryRevision)
            {
                return new RegistryValidationResult(
                    RegistryValidationError.InvalidRetiredRevision,
                    index);
            }
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.State != RegistryEntryState.Retired)
            {
                continue;
            }

            if (!entry.ReplacementId.IsValid)
            {
                return new RegistryValidationResult(RegistryValidationError.MissingReplacement, index);
            }

            if (entry.ReplacementId == entry.Id)
            {
                return new RegistryValidationResult(RegistryValidationError.ReplacementIsSelf, index);
            }

            if (!idIndex.TryGetValue(entry.ReplacementId.Value, out var related))
            {
                return new RegistryValidationResult(RegistryValidationError.ReplacementNotFound, index);
            }

            var replacement = entries[related];
            if (!string.Equals(replacement.RegistryRef, entry.RegistryRef, StringComparison.Ordinal))
            {
                return new RegistryValidationResult(
                    RegistryValidationError.ReplacementRegistryMismatch,
                    index,
                    related);
            }

            if (replacement.State != RegistryEntryState.Active)
            {
                return new RegistryValidationResult(
                    RegistryValidationError.ReplacementIsRetired,
                    index,
                    related);
            }
        }

        if (!TryValidateActiveRef(activeSchemaId, RegistryCatalogKind.Schema, entries, idIndex))
        {
            return new RegistryValidationResult(RegistryValidationError.MissingActiveSchema);
        }

        if (!TryValidateActiveRef(activeMappingId, RegistryCatalogKind.Mapping, entries, idIndex))
        {
            return new RegistryValidationResult(RegistryValidationError.MissingActiveMapping);
        }

        var boundDomains = new Dictionary<ulong, int>(owners.Count);
        for (var index = 0; index < owners.Count; index++)
        {
            var owner = owners[index];
            if (!boundDomains.TryAdd(owner.DomainId.Value, index))
            {
                return new RegistryValidationResult(
                    RegistryValidationError.DuplicateDomainOwner,
                    index,
                    boundDomains[owner.DomainId.Value]);
            }

            if (!HasKind(owner.DomainId, RegistryCatalogKind.Domain, entries, idIndex)
                || !HasKind(owner.OwnerId, RegistryCatalogKind.Owner, entries, idIndex)
                || !HasKind(owner.SchemaId, RegistryCatalogKind.Schema, entries, idIndex)
                || !HasKind(owner.MappingId, RegistryCatalogKind.Mapping, entries, idIndex)
                || !HasKind(owner.MutationOperationId, RegistryCatalogKind.Operation, entries, idIndex))
            {
                return new RegistryValidationResult(RegistryValidationError.OrphanOwnerBinding, index);
            }
        }

        long previousSequence = 0;
        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            if (change.Sequence <= previousSequence
                || change.Revision <= 0
                || change.Revision > registryRevision)
            {
                return new RegistryValidationResult(
                    RegistryValidationError.InvalidChangeSequence,
                    index);
            }

            if (!idIndex.ContainsKey(change.EntryId.Value)
                || (change.PreviousEntryId.IsValid
                    && !idIndex.ContainsKey(change.PreviousEntryId.Value)))
            {
                return new RegistryValidationResult(
                    RegistryValidationError.OrphanChangeEntry,
                    index);
            }

            previousSequence = change.Sequence;
        }

        return RegistryValidationResult.Valid;
    }

    private static AuthorityRegistrySnapshot CreateCurrent()
    {
        var entries = new List<RegistryEntry>();
        static RegistryEntry Add(
            List<RegistryEntry> target,
            string registryRef,
            string key,
            RegistryCatalogKind kind)
        {
            var value = new RegistryEntry(
                CreateStableId(registryRef, key),
                registryRef,
                key,
                kind,
                CurrentRevision);
            target.Add(value);
            return value;
        }

        var schema = Add(entries, "cmip.schema", "world.schema25", RegistryCatalogKind.Schema);
        var mapping = Add(entries, "cmip.mapping", "world.schema25.authority", RegistryCatalogKind.Mapping);
        var mutation = Add(entries, "cmip.operation", "authority.mutate", RegistryCatalogKind.Operation);
        Add(entries, "cmip.semantic", "ready", RegistryCatalogKind.SemanticState);
        Add(entries, "cmip.semantic", "review", RegistryCatalogKind.SemanticState);
        Add(entries, "cmip.semantic", "blocked", RegistryCatalogKind.SemanticState);
        Add(entries, "cmip.semantic", "undefined", RegistryCatalogKind.SemanticState);
        Add(entries, "cmip.unit", "basis-point", RegistryCatalogKind.Unit);
        Add(entries, "cmip.unit", "world-month", RegistryCatalogKind.Unit);
        Add(entries, "cmip.target", "authority-world", RegistryCatalogKind.Target);

        var owners = new List<AuthorityOwnerBinding>(14);
        var changes = new List<RegistryChangeSet>();
        long sequence = 0;
        foreach (var master in Enumerable.Range(1, 14))
        {
            var code = $"m{master:D2}";
            var domain = Add(entries, "cmip.domain", code, RegistryCatalogKind.Domain);
            var owner = Add(entries, "cmip.owner", $"simulation.{code}", RegistryCatalogKind.Owner);
            owners.Add(new AuthorityOwnerBinding(
                domain.Id,
                owner.Id,
                schema.Id,
                mapping.Id,
                mutation.Id));
        }

        foreach (var entry in entries.OrderBy(entry => entry.Id.Value))
        {
            changes.Add(new RegistryChangeSet(
                ++sequence,
                CurrentRevision,
                RegistryChangeKind.Added,
                entry.Id));
        }

        return new AuthorityRegistrySnapshot(
            CurrentRevision,
            schema.Id,
            mapping.Id,
            entries,
            owners,
            changes);
    }

    private static void HashUtf8(ReadOnlySpan<char> value, ref ulong hash, ulong prime)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        Span<byte> buffer = byteCount <= 256
            ? stackalloc byte[byteCount]
            : new byte[byteCount];
        Encoding.UTF8.GetBytes(value, buffer);
        foreach (var valueByte in buffer)
        {
            hash ^= valueByte;
            hash *= prime;
        }
    }

    private static bool TryValidateActiveRef(
        RegistryStableId id,
        RegistryCatalogKind kind,
        IReadOnlyList<RegistryEntry> entries,
        Dictionary<ulong, int> index) =>
        HasKind(id, kind, entries, index)
        && entries[index[id.Value]].State == RegistryEntryState.Active;

    private static bool HasKind(
        RegistryStableId id,
        RegistryCatalogKind kind,
        IReadOnlyList<RegistryEntry> entries,
        Dictionary<ulong, int> index) =>
        id.IsValid
        && index.TryGetValue(id.Value, out var entryIndex)
        && entries[entryIndex].Kind == kind;
}
