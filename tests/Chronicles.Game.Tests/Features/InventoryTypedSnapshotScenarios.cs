using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class TypedSnapshotFixture
{
    private static readonly Type Owner = typeof(InventoryEngine.OwnedTransaction);
    private static readonly JsonSerializerOptions Projection = (JsonSerializerOptions)(Owner.GetField("ProjectOptions", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new InvalidOperationException("JSON projection reference options are absent."));
    private static readonly JsonSerializerOptions Populate = (JsonSerializerOptions)(Owner.GetField("CopyOptions", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new InvalidOperationException("JSON copy reference options are absent."));
    internal static T Call<T>(string name, params object[] arguments)
    {
        var method = Owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Private copy helper missing: " + name);
        try { return (T)(method.Invoke(null, arguments) ?? throw new InvalidOperationException("Copy helper returned null.")); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    internal static InventoryState Reference(GameState world) => Call<InventoryState>("CreateProjectedSnapshot", world);
    internal static InventoryState Typed(GameState world, string fingerprint) => Call<InventoryState>("CreateTypedProjectedSnapshot", world, fingerprint);
    internal static Person ReferencePerson(Person person)
    {
        var copy = JsonSerializer.Deserialize<Person>(JsonSerializer.SerializeToUtf8Bytes(person, Projection), Populate)
            ?? throw new InvalidOperationException("Reference Person copy was empty.");
        copy.Skills = new(copy.Skills, StringComparer.Ordinal); return copy;
    }
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value);
    internal static void Equal<T>(T left, T right, string message) => InventoryFixture.Check(Json(left) == Json(right), message);

    // Explicit allowlist, not derived from the runtime schema: adding a field requires a deliberate copy/default decision.
    internal static IReadOnlyDictionary<Type, string> Shapes { get; } = new Dictionary<Type, string>
    {
        [typeof(InventoryState)] = "BaseFingerprint Month NextId Revision Items People Materials Ledger",
        [typeof(InventoryLedger)] = "NextEventSequence Initialized BaseCarryCapacity ProductionWork ProfessionalWork ExtractionWorkPerUnit Metadata Reservations Labor Jobs Contracts Sources Effects Appraisals Observations Arrays Bindings Events Receipts",
        [typeof(Item)] = "Id Definition Owner Place Quantity Quality Damage Defects Container Reserved BoundSoul Source",
        [typeof(MaterialFlow)] = "Opening Extraction Production Consumption Loss",
        [typeof(ItemMetadata)] = "Item Custodian Carrier UseRights Provenance Capacity AllowedContents AllowNestedContainers",
        [typeof(AssetReservation)] = "Id Actor Purpose Items Active",
        [typeof(LaborReservation)] = "Id Person Purpose Start End Units Facility FacilityCapacity Active",
        [typeof(ProductionJob)] = "Id Actor Recipe Status Place Due Reservation Labor KnowledgeActivity Target Source OutputDefinition OutputQuantity Inputs Consumed Quality Damage Defects BoundSoul Provenance",
        [typeof(NaturalStock)] = "Id Place Definition Activity Remaining Capacity EcologyWeight Regeneration LastMonth Origin",
        [typeof(InventoryContract)] = "Id Kind Status AdvertisedDefinition AdvertisedName AdvertisedQuantity Seller Buyer Carrier Item Reservation Labor PaidAmount PaidQuantity PaidQuality EndedMonth Price Escrow Fee Due Origin Destination",
        [typeof(ArrayInstallation)] = "Id Item Owner Place Range Condition Active PaidUntil",
        [typeof(ConsumedMaterial)] = "Definition Quantity",
        [typeof(InventoryAppraisal)] = "Appraiser Item Definition EstimatedQuality Damage DetectedDefects Month",
        [typeof(SourceObservation)] = "Observer Source Place Remaining Month Evidence Definition Activity",
        [typeof(InventoryEffect)] = "Cause Target Kind Magnitude Start End ConsumptionSequence",
        [typeof(BindingContract)] = "Id Item Soul Established Evidence",
        [typeof(InventoryEvent)] = "Month Kind Actor Subject Detail Sequence Definition Quantity Place Owner ItemSource IsLoss Quality Damage Defects OwnerObserved",
        [typeof(InventoryReceipt)] = "Signature Subject Message",
        [typeof(Person)] = "Slot SlotGeneration Id BodyId SoulId Name Species Gestates Status Born Ended Lifespan ParentOne ParentTwo Partner Household Sect Place Realm Layer GreatPerfection Work Martial MartialWork Health Money NextBirth Goal Reason Mood LastCare Habits Decisions DecisionCandidates Plan LessonRequests Journal PlanExpansions LastPeopleRevision ObservationCursor ObservationRemaining Action Due ActionTarget ActionData ActionRevision Processes Components Irreversible PendingLayer StructureDamage BondStability SealedUntil Sealed SoulPactUntil SoulPactSource Roots Aptitudes Personality Formations Traits Skills Understanding Beliefs ObservedPeople ObservedPlaces KnownPlaces KnownPeople Equipment Guardian",
        [typeof(Formation)] = "Realm Path Quality Defect Month Source",
        [typeof(Belief)] = "Key Value Confidence Observed Expires Source",
        [typeof(TraitState)] = "Id Source Acquired Expires Active",
        [typeof(PlanStep)] = "Action Target Data",
        [typeof(LessonRequest)] = "Pupil Knowledge Requested Expires",
        [typeof(GameEvent)] = "Id Month Kind Actor Target Text Source TargetKind",
        [typeof(DecisionCandidate)] = "Goal Score Evidence",
        [typeof(DecisionRecord)] = "Month Goal Reason Action Expansions Candidates",
        [typeof(PersonObservation)] = "Id Name Realm Layer Place Month ApparentGestates AppearsAdult",
        [typeof(PlaceObservation)] = "Place Source Qi Facilities Month Sovereign Owner Operator Delegate BlockedUntil"
    };

    internal static Type[] ReachableSchema()
    {
        var types = new HashSet<Type>();
        void Visit(Type type)
        {
            if (type.IsArray) { Visit(type.GetElementType()!); return; }
            if (type.IsGenericType && type.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
            { foreach (var argument in type.GetGenericArguments()) Visit(argument); return; }
            if (type.IsEnum || type.IsPrimitive || type == typeof(string) || type.Namespace?.StartsWith("Chronicles.Game", StringComparison.Ordinal) != true || !types.Add(type)) return;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) Visit(property.PropertyType);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Visit(field.FieldType);
        }
        Visit(typeof(InventoryState)); return types.OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
    }

    // Tests create ordinary public-ctor objects only. No production reflection, uninitialized objects or private writes.
    internal static object Filled(Type type, ref int seed)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying) return Filled(underlying, ref seed);
        if (type == typeof(string)) return "value-" + seed++;
        if (type == typeof(bool)) return true;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(Enum.GetValues(type).Length - 1)!;
        if (type.IsPrimitive) return Convert.ChangeType(seed++, type, System.Globalization.CultureInfo.InvariantCulture);
        if (type.IsArray)
        {
            var element = type.GetElementType()!; var array = Array.CreateInstance(element, 2);
            for (var n = 0; n < array.Length; n++) array.SetValue(Filled(element, ref seed), n);
            return array;
        }
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments(); var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IReadOnlyList<>))
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(args))!;
                list.Add(Filled(args[0], ref seed)); list.Add(Filled(args[0], ref seed)); return list;
            }
            if (definition == typeof(SortedDictionary<,>))
            {
                var dictionary = (IDictionary)(args[0] == typeof(string)
                    ? Activator.CreateInstance(type, StringComparer.Ordinal)! : Activator.CreateInstance(type)!);
                for (var n = 0; n < 2; n++) dictionary.Add(Filled(args[0], ref seed), Filled(args[1], ref seed));
                return dictionary;
            }
        }
        var constructor = type.GetConstructors().OrderBy(c => c.GetParameters().Length).First();
        var parameters = new List<object>();
        foreach (var parameter in constructor.GetParameters()) parameters.Add(Filled(parameter.ParameterType, ref seed));
        var instance = constructor.Invoke(parameters.ToArray());
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
            property.SetValue(instance, Filled(property.PropertyType, ref seed));
        return instance;
    }

    internal static HashSet<object> References(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Visit(object? value)
        {
            if (value is null || value is string) return;
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) return;
            if (!type.IsValueType && !seen.Add(value)) return;
            if (value is IDictionary dictionary)
            { foreach (DictionaryEntry pair in dictionary) { Visit(pair.Key); Visit(pair.Value); } return; }
            if (value is IEnumerable sequence) { foreach (var item in sequence) Visit(item); return; }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                Visit(property.GetValue(value));
        }
        Visit(root); return seen;
    }
    internal static void Isolated(object source, object copy, string message) =>
        InventoryFixture.Check(!References(source).Overlaps(References(copy)), message);
}

public sealed class InventoryTypedSchemaScenario : IFeatureScenario
{
    public string Name => "inventory typed copy schema: 도달한 모든 필드와 명시 Person15 footprint 누락 거부";
    public void Run()
    {
        var reachable = TypedSnapshotFixture.ReachableSchema();
        InventoryFixture.Check(reachable.ToHashSet().SetEquals(TypedSnapshotFixture.Shapes.Keys), "Copy schema contains a new/unclassified reachable type.");
        foreach (var type in reachable)
        {
            var actual = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name)
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name));
            InventoryFixture.Check(actual.ToHashSet(StringComparer.Ordinal).SetEquals(TypedSnapshotFixture.Shapes[type].Split(' ')),
                "Copy/default decision missing for schema " + type.FullName + ": " + string.Join(',', actual));
        }
        string[] projected = ["Slot", "SlotGeneration", "Id", "BodyId", "SoulId", "Status", "Place", "Money", "Health", "Work", "Skills", "Equipment", "Action", "Due", "ActionTarget"];
        InventoryFixture.Check(typeof(Person).GetProperties().Where(p => InventoryEngine.OwnedTransaction.IncludesProjectedPersonProperty(p.Name))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(projected), "The current JSON Person projection changed; review typed copying deliberately.");
    }
}

public sealed class InventoryTypedAllFieldsScenario : IFeatureScenario
{
    public string Name => "inventory typed copy fields: 전 원장 sentinel·현재 JSON Person defaults·모든 중첩 참조 격리";
    public void Run()
    {
        var seed = 10; var ledger = (InventoryLedger)TypedSnapshotFixture.Filled(typeof(InventoryLedger), ref seed);
        var original = TypedSnapshotFixture.Json(ledger); var copy = TypedSnapshotFixture.Call<InventoryLedger>("CopyOwnedLedger", ledger);
        TypedSnapshotFixture.Equal(ledger, copy, "A complete ledger field, nullable value, record or ordering was omitted.");
        TypedSnapshotFixture.Isolated(ledger, copy, "Typed ledger shares a nested list/dict/POCO/record with its source.");
        copy.Metadata[0].UseRights.Add(987); copy.Jobs[0].Inputs.Add(986); copy.Jobs[0].Consumed.Add(new("new", 1));
        copy.Contracts[0].PaidQuality = null; copy.Receipts.Clear(); copy.Sources[0].Remaining++;
        InventoryFixture.Check(original == TypedSnapshotFixture.Json(ledger), "Nested typed writes leaked into the source ledger.");

        var person = (Person)TypedSnapshotFixture.Filled(typeof(Person), ref seed);
        person.Skills = new(StringComparer.Ordinal) { ["I"] = 1, ["i"] = 2, ["İ"] = 3, ["ı"] = 4 };
        person.Equipment = new(Comparer<int>.Create((a, b) => b.CompareTo(a))) { [0] = 11, [3] = 12 };
        var expected = TypedSnapshotFixture.ReferencePerson(person); var typed = TypedSnapshotFixture.Call<Person>("CopyOwnedPerson", person);
        TypedSnapshotFixture.Equal(expected, typed, "Projected 15 values or normal defaults of the other Person fields differ from JSON.");
        TypedSnapshotFixture.Isolated(person, typed, "Typed Person shares mutable projected fields or default arrays with the original.");
        var untouched = TypedSnapshotFixture.Json(person); typed.Skills["new"] = 7; typed.Equipment.Clear(); typed.Roots[0] = 99;
        typed.Decisions.Add(new(1, "x", "y", "z", 1, []));
        InventoryFixture.Check(untouched == TypedSnapshotFixture.Json(person) && typed.Skills.Comparer.Equals(StringComparer.Ordinal), "Person mutation or comparer parity failed.");
        foreach (var contract in ledger.Contracts) { contract.PaidAmount = null; contract.PaidQuantity = null; contract.PaidQuality = null; contract.EndedMonth = null; }
        foreach (var metadata in ledger.Metadata) metadata.Capacity = null;
        ledger.Events = ledger.Events.Select(e => e with { Quality = null, Damage = null }).ToList();
        TypedSnapshotFixture.Equal(ledger, TypedSnapshotFixture.Call<InventoryLedger>("CopyOwnedLedger", ledger), "Null optional values were rewritten.");
    }
}

public sealed class InventoryTypedSnapshotReferenceScenario : IFeatureScenario
{
    public string Name => "inventory typed copy reference: 실제 후보 JSON 형태·ordinal·검증·원본 격리 일치";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var source = OwnedInventoryFixture.Snapshot(world);
        var reference = TypedSnapshotFixture.Reference(world); var typed = TypedSnapshotFixture.Typed(world, reference.BaseFingerprint);
        TypedSnapshotFixture.Equal(reference, typed, "Private typed snapshot differs from the current JSON reference shape.");
        TypedSnapshotFixture.Isolated(world, typed, "A source object escaped into the typed candidate.");
        InventoryFixture.Check(typed.BaseFingerprint == reference.BaseFingerprint && typed.Materials.Comparer.Equals(StringComparer.Ordinal) &&
            typed.Ledger.Receipts.Comparer.Equals(StringComparer.Ordinal) && typed.People.All(p => p.Skills.Comparer.Equals(StringComparer.Ordinal)),
            "Reference fingerprint or normalized ordinal comparers changed.");
        typed.Items[0].Quantity++; typed.Materials.Values.First().Opening++; typed.People[0].Money++;
        typed.Ledger.Metadata[0].Provenance.Add("typed-only");
        InventoryFixture.Check(source == OwnedInventoryFixture.Snapshot(world), "The full typed candidate changed world authority before any commit.");
        foreach (Action<GameState> corrupt in new Action<GameState>[] { w => w.Items[0].Quantity = 0, w => w.NextId = 1, w => w.People[0].Money = -1 })
        {
            var bad = OwnedInventoryFixture.Create(); corrupt(bad);
            string Reject(Action action)
            {
                try { action(); } catch (InvalidDataException error) { return error.Message; }
                throw new InvalidOperationException("Invalid snapshot was accepted without normal validation.");
            }
            var expected = Reject(() => TypedSnapshotFixture.Reference(bad));
            var actual = Reject(() => TypedSnapshotFixture.Typed(bad, reference.BaseFingerprint));
            InventoryFixture.Check(actual == expected, "Typed copy skipped or changed the normal reference validation.");
        }
    }
}

public sealed class InventoryTypedEngineTranscriptScenario : IFeatureScenario
{
    public string Name => "inventory typed copy transcript: 현재 JSON 후보와 실제 예약·생산·소비·장비·거부·재전송 연결";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var untouched = OwnedInventoryFixture.Snapshot(world);
        var reference = TypedSnapshotFixture.Reference(world); var typed = TypedSnapshotFixture.Typed(world, reference.BaseFingerprint);
        long Apply(InventoryCommand command, bool accepted = true)
        {
            var left = InventoryEngine.Apply(reference, command); var right = InventoryEngine.Apply(typed, command);
            InventoryFixture.Check(left.Accepted == accepted && right.Accepted == left.Accepted && left.Message == right.Message &&
                left.Subject == right.Subject && left.Replayed == right.Replayed, "Real inventory result differs: " + command.Id);
            TypedSnapshotFixture.Equal(left.State, right.State, "Real paid command state differs: " + command.Id);
            reference = left.State; typed = right.State; return left.Subject;
        }
        var herb = OwnedInventoryFixture.Item(world, "herb");
        var split = Apply(new SplitInventoryItem("typed-split", 1, herb, 2));
        Apply(new MergeStacks("typed-merge", 1, herb, split));
        var tool = OwnedInventoryFixture.Item(world, "tool"); Apply(new EquipItem("typed-equip", 1, tool)); Apply(new UnequipItem("typed-unequip", 1, tool));
        var reservation = Apply(new ReserveInventoryMaterials("typed-reserve", 1, "actual paid input", new Dictionary<string, int> { ["herb"] = 1 }));
        Apply(new ConsumeInventoryAssets("typed-consume", 1, reservation));
        var job = Apply(new StartProduction("typed-job", 1, "pill", 2));
        Apply(new BeginProduction("typed-process", 1, job)); Apply(new BeginProduction("typed-process", 1, job));
        reference.Month = 2; typed.Month = 2; Apply(new FinishProduction("typed-finish", 1, job));
        Apply(new OfferItem("typed-bad-offer", 1, herb, 0, 4), false);
        InventoryEngine.Validate(typed); InventoryEngine.Validate(reference);
        InventoryFixture.Check(untouched == OwnedInventoryFixture.Snapshot(world), "Transcript committed a prototype candidate to the source world.");
        // The existing fixed world transcript remains an additional producer/save boundary reference; no typed factory hook is installed.
        new InventoryOwnedWorldReferenceScenario().Run();
    }
}
