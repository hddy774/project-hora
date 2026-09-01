using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void AdvanceGeneratedWorldContent(long currentMonth)
    {
        var expansion = _generation.AdvanceContentGeneration(currentMonth);
        if (expansion.IsEmpty)
        {
            return;
        }

        foreach (var generated in expansion.Items)
        {
            if (!ContentDefinitionRegistry.TryGet(
                    new ContentDefinitionId(generated.BaseDefinitionId),
                    out var definition))
            {
                throw new InvalidOperationException(
                    $"지속 생성 아이템의 기본 Definition이 없습니다: {generated.GeneratedId:X16}");
            }
            var source = CreateGeneratedSource(
                "cmip.content-growth.item-instance",
                generated.GeneratedId);
            var created = _content.TryCreate(new CreateContentInstanceCommand(
                definition.Id,
                ContentOwnerRef.World,
                new ContentHolderRef(ContentHolderKind.WorldLocation, 0),
                checked((ushort)(generated.GeneratedId
                    % (ulong)WorldAtlasRules.ProductionLocationCount)),
                Quantity: 1,
                ContentQualityAvailability.Specified,
                generated.QualityHundredths,
                ConditionHundredths: QualityValue.MaximumHundredths,
                source,
                Tick.Value,
                generated.GeneratedId));
            EnsureGeneratedContentApplied(created.Applied, "아이템 실물", created.Status);
        }

        foreach (var generated in expansion.Books)
        {
            var contentId = new KnowledgeContentId(generated.ContentId);
            var contentSource = CreateGeneratedSource(
                "cmip.content-growth.book-content",
                generated.ContentId);
            var content = _knowledge.TryRegisterGeneratedContent(
                contentId,
                generated.Kind,
                generated.CatalogOrdinal,
                contentSource,
                Tick.Value);
            EnsureGeneratedContentApplied(content.Applied, "서적 내용", content.Status);

            var editionSource = CreateGeneratedSource(
                "cmip.content-growth.book-edition",
                generated.ContentId);
            var edition = _knowledge.TryCreateEdition(new CreateKnowledgeEditionCommand(
                contentId,
                ParentEditionId: null,
                KnowledgeEditionRelation.Original,
                ContributorAgent: -1,
                KnowledgeValueAvailability.DataUnavailable,
                ConfidenceHundredths: 0,
                editionSource,
                Tick.Value));
            EnsureGeneratedContentApplied(edition.Applied, "서적 판본", edition.Status);

            var recordDefinition = ContentDefinitionRegistry.Get(
                generated.Kind == KnowledgeContentKind.Technique
                    ? "book.technique"
                    : "book.spell");
            var recordSource = CreateGeneratedSource(
                "cmip.content-growth.book-record",
                generated.ContentId);
            var record = _content.TryCreate(new CreateContentInstanceCommand(
                recordDefinition.Id,
                ContentOwnerRef.World,
                new ContentHolderRef(ContentHolderKind.WorldLocation, 0),
                checked((ushort)(generated.ContentId
                    % (ulong)WorldAtlasRules.ProductionLocationCount)),
                Quantity: 1,
                ContentQualityAvailability.Specified,
                EconomyRules.InitialSkillMaximumHundredths,
                ConditionHundredths: QualityValue.MaximumHundredths,
                recordSource,
                Tick.Value));
            EnsureGeneratedContentApplied(record.Applied, "서적 실물", record.Status);

            var bindingSource = CreateGeneratedSource(
                "cmip.content-growth.book-binding",
                generated.ContentId);
            var binding = _knowledge.TryBindPhysicalRecord(
                new BindKnowledgeRecordCommand(
                    record.InstanceId!.Value,
                    edition.EditionId!.Value,
                    (PhysicalRecordFormat)(generated.CatalogOrdinal % 3),
                    bindingSource,
                    Tick.Value),
                _content);
            EnsureGeneratedContentApplied(binding.Applied, "서적 실물 binding", binding.Status);
        }
    }

    private static ContentSourceResultId CreateGeneratedSource(
        string sourceNamespace,
        ulong stableValue) => new(
            AuthorityRegistry.CreateStableId(
                sourceNamespace,
                stableValue.ToString("X16", System.Globalization.CultureInfo.InvariantCulture))
            .Value);

    private static void EnsureGeneratedContentApplied<TStatus>(
        bool applied,
        string stage,
        TStatus status)
        where TStatus : struct, Enum
    {
        if (!applied)
        {
            throw new InvalidOperationException(
                $"지속 세계 콘텐츠 생성 실패: {stage} · {status}");
        }
    }
}
