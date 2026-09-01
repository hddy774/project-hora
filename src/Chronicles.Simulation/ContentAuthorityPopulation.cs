using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 새 세계의 승인 콘텐츠 모집단과 PC 임시 배치만 담당한다. 생명주기 전이는
/// <see cref="ContentAuthorityStore"/>에 남겨 초기화와 런타임 변경 책임을 분리한다.
/// </summary>
internal static class ContentAuthorityPopulation
{
    private const ulong RealmSourcePrefix = 0xC011_0000_0000_0000UL;
    private const ulong MaterialSourcePrefix = 0xC014_0000_0000_0000UL;
    private const ulong ItemSourcePrefix = 0xC015_0000_0000_0000UL;
    private const ulong InheritanceSourcePrefix = 0xC016_0000_0000_0000UL;

    private static readonly string[] MaterialDefinitions =
    [
        "material.artifact",
        "material.pill",
    ];

    private static readonly string[] ItemDefinitions =
    [
        "item.artifact.offense",
        "item.artifact.defense",
        "item.pill.growth",
        "item.pill.breakthrough",
    ];

    public static ContentAuthorityStore Create(ulong worldSeed)
    {
        var store = new ContentAuthorityStore();
        var locationOffset = checked((int)(worldSeed
            % (ulong)WorldAtlasRules.ProductionLocationCount));
        AddSecretRealms(store, locationOffset);
        AddWorldInstances(
            store,
            MaterialDefinitions,
            ContentPopulationRules.ApprovedMaterialStackCount,
            MaterialSourcePrefix,
            locationOffset,
            locationStride: 17);
        AddWorldInstances(
            store,
            ItemDefinitions,
            ContentPopulationRules.ApprovedItemInstanceCount,
            ItemSourcePrefix,
            locationOffset,
            locationStride: 31);
        AddWorldInstances(
            store,
            ["created-work.independent-inheritance"],
            ContentPopulationRules.ApprovedIndependentInheritanceCount,
            InheritanceSourcePrefix,
            locationOffset,
            locationStride: 47);
        return store;
    }

    private static void AddSecretRealms(ContentAuthorityStore store, int locationOffset)
    {
        for (var index = 0; index < ContentPopulationRules.InitialSecretRealmCount; index++)
        {
            var natural = index < ContentPopulationRules.InitialNaturalSecretRealmCount;
            AddWorldInstance(
                store,
                natural ? "secret-realm.natural" : "secret-realm.artificial",
                RealmSourcePrefix,
                index,
                checked((ushort)((locationOffset + index)
                    % WorldAtlasRules.ProductionLocationCount)));
        }
    }

    private static void AddWorldInstances(
        ContentAuthorityStore store,
        string[] definitions,
        int count,
        ulong sourcePrefix,
        int locationOffset,
        int locationStride)
    {
        for (var index = 0; index < count; index++)
        {
            AddWorldInstance(
                store,
                definitions[index % definitions.Length],
                sourcePrefix,
                index,
                checked((ushort)((locationOffset + (index * locationStride))
                    % WorldAtlasRules.ProductionLocationCount)));
        }
    }

    private static void AddWorldInstance(
        ContentAuthorityStore store,
        string definitionRef,
        ulong sourcePrefix,
        int index,
        ushort locationId)
    {
        var definition = ContentDefinitionRegistry.Get(definitionRef);
        var result = store.TryCreate(new CreateContentInstanceCommand(
            definition.Id,
            ContentOwnerRef.World,
            new ContentHolderRef(ContentHolderKind.WorldLocation, 0),
            locationId,
            Quantity: 1,
            ContentQualityAvailability.DataUnavailable,
            QualityHundredths: 0,
            ConditionHundredths: QualityValue.MaximumHundredths,
            new ContentSourceResultId(sourcePrefix + checked((ulong)index + 1UL)),
            Tick: 0));
        if (!result.Applied)
        {
            throw new InvalidOperationException(
                $"초기 콘텐츠 생성에 실패했습니다: {definitionRef} #{index + 1} · {result.Status}");
        }
    }
}
