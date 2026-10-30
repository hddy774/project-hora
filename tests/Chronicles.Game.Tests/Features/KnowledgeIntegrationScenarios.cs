using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

public sealed class KnowledgeGenesisTransmissionContractScenario : IFeatureScenario
{
    public string Name => "INV-024 knowledge bridge contract: explicit genesis transmission licence and existing comprehension baseline";
    public void Run()
    {
        var ordinary = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(ordinary.SeedInitialKnowledge(2, 1, [1], 100, "ordinary-initial"));
        KnowledgeFixture.Rejected(ordinary, KnowledgeStatus.Forbidden, () => ordinary.StartTeaching(new("unlicensed",
            KnowledgeFixture.Two, KnowledgeFixture.One, KnowledgeFixture.Work, [1], KnowledgeTransmission.Parent, 0, 2)));
        var licensed = KnowledgeFixture.Fresh();
        KnowledgeFixture.Ok(licensed.SeedInitialKnowledge(2, 1, [1], 100, "explicit-initial", canTransmit: true));
        var lesson = KnowledgeFixture.Ok(licensed.StartTeaching(new("licensed", KnowledgeFixture.Two, KnowledgeFixture.One,
            KnowledgeFixture.Work, [1], KnowledgeTransmission.Parent, 0, 2)));
        licensed = KnowledgeFixture.RoundTrip(licensed);
        KnowledgeFixture.Ok(licensed.Complete(new("licensed.done", lesson, 2, [KnowledgeFixture.Two, KnowledgeFixture.One])));
        KnowledgeFixture.Check(licensed.ViewPersonal(1).Rows.Single().Mastery == 0, "Genesis licence granted practical skill.");

        var baseline = new KnowledgeAuthority();
        KnowledgeFixture.Ok(baseline.DefineWork(new("entry", "입문", "entry", KnowledgeKind.Cultivation,
            [new(1, "원리", 100, 100)], 0, "entry-definition")));
        var edition = KnowledgeFixture.Ok(baseline.CreateEdition("entry", 0, EditionRelation.Original, 1, [new(1, "입문 순서")], 0, "entry-edition"));
        KnowledgeFixture.Ok(baseline.BindMedium(10, edition, 1, 0, KnowledgeMediumKind.Book, 0, KnowledgeRights.Read, [], false, 0, "entry-medium"));
        KnowledgeFixture.Ok(baseline.SeedInitialKnowledge(1, edition, [1], 30, "entry-genesis"));
        var study = KnowledgeFixture.Ok(baseline.StartStudy(new("entry-study", KnowledgeFixture.One, 10, [1], 0, 1)));
        KnowledgeFixture.Ok(baseline.Complete(new("entry-study.done", study, 1, [KnowledgeFixture.One])));
        KnowledgeFixture.Check(baseline.FindKnowledge(1, "entry", 1)?.Comprehension == 31 && baseline.Checkpoint().Mastery.Count == 0,
            "Actual new study failed to advance above an explicit initial comprehension baseline.");
        KnowledgeFixture.RoundTrip(baseline);
    }
}

public sealed class KnowledgeSubstrateAndRevocationContractScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge bridge contract: a real reserved paper substrate may be the paid blank and reading rights revoke";
    public void Run()
    {
        var a = KnowledgeFixture.Fresh();
        var request = KnowledgeFixture.Copy() with { Chapters = [1], Payment = new("physical-substrate", 500, 500, 1, 202, 1) };
        var copy = KnowledgeFixture.Ok(a.StartTranscription(request));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Ok(a.Complete(new("physical-copy.done", copy, 1, [KnowledgeFixture.One])));
        KnowledgeFixture.Check(a.Checkpoint().Media.Any(m => m.ItemId == 500), "A reserved paper substrate could not become the bound output.");
        KnowledgeFixture.Ok(a.GrantAccess(101, 1, 2, KnowledgeRights.Read, 10, 1, "reading-right"));
        KnowledgeFixture.Check(a.ViewAccessible(2, 0, 1).Total == 1, "Explicit reading grant was ignored.");
        var study = KnowledgeFixture.Ok(a.StartStudy(new("borrowed-study", KnowledgeFixture.Two, 101, [1], 1, 1)));
        KnowledgeFixture.Ok(a.RevokeAccess(101, 1, 2, 1, "reading-revoked"));
        a = KnowledgeFixture.RoundTrip(a);
        KnowledgeFixture.Check(a.ViewAccessible(2, 0, 1).Total == 0, "A revoked right remained visible.");
        KnowledgeFixture.Rejected(a, KnowledgeStatus.Unavailable, () => a.Complete(new("revoked-completion", study, 2, [KnowledgeFixture.Two])));
        KnowledgeFixture.Check(a.ViewPersonal(2).Total == 0, "Revocation completed an inaccessible learning action.");
        KnowledgeFixture.RoundTrip(a);
    }
}
