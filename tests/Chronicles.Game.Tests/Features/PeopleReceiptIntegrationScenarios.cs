using System.Text.Json;

namespace Chronicles.Game.Tests.Features;

public sealed class PeoplePublicOathReceiptPrivacyScenario : IFeatureScenario
{
    public string Name => "INV-008/009/015 people receipts public: unseen remote oath end cannot update personal agreement knowledge";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture();
        var request = f.Request("requestOath", f.First, f.Second, data: "실제 대면한 연구 협력 약속");
        f.Consent(f.Second, request); f.World.AdvanceTo(1);
        f.Cancel(f.Second); f.Send("explore", f.Second); f.World.AdvanceTo(2);
        f.Cancel(f.First); f.Control(f.Second); f.Send("travel", f.Second, 1); f.Control(f.First); f.World.AdvanceTo(3);
        var state = f.World.Checkpoint();
        PeopleFixture.Check(state.People.Single(p => p.Id == f.First).Place == 0 && state.People.Single(p => p.Id == f.Second).Place == 1,
            "Remote evidence test requires actual completed movement, not an observer teleport.");
        var received = f.World.PeopleKnownOaths(f.First).Single(); var before = JsonSerializer.Serialize(received);
        PeopleFixture.Check(received.KnownActive && received.Person == f.First && received.Other == f.Second &&
            received.ObservedMonth == 1 && f.World.PeopleKnownOaths(f.Second).Length == 0 && f.World.PeopleKnownOaths(0).Length == 0,
            "A controlled person receives only their actual signed agreement receipt.");
        f.World = PeoplePublicFixture.Configure(f.World, s => s.People.Single(p => p.Id == f.Second).Lifespan = 4 - s.People.Single(p => p.Id == f.Second).Born);
        f.World.AdvanceTo(4); state = f.World.Checkpoint();
        PeopleFixture.Check(state.People.Single(p => p.Id == f.Second).Status == LifeStatus.Dead && !state.PeopleDomain.Social.Oaths.Single().Active &&
            state.People.Single(p => p.Id == f.First).Journal.All(e => e.Kind != "사망" || e.Actor != f.Second) &&
            JsonSerializer.Serialize(f.World.PeopleKnownOaths(f.First).Single()) == before,
            "Actual remote authority ends while the unwitnessed personal oath receipt remains unchanged.");
        var hash = f.World.StateHash(); _ = f.World.PeopleKnownOaths(f.First); _ = f.World.PeopleReceivedClaims(f.First); _ = f.World.PeopleRequests(f.First);
        PeopleFixture.Check(f.World.StateHash() == hash, "Receipt queries cannot settle, reveal, mutate or consume RNG.");
        f.Send("explore", f.First); f.World.AdvanceTo(5); f.Send("travel", f.First, 1); f.World.AdvanceTo(6);
        PeopleFixture.Check(JsonSerializer.Serialize(f.World.PeopleKnownOaths(f.First).Single()) == before,
            "Visiting an old hidden end location later cannot manufacture a witnessed death or refresh an agreement receipt.");
        f.Send("observe");
        PeopleFixture.Check(f.World.PeopleKnownOaths(0).Length == 2 && f.World.PeopleKnownOaths(0).Select(o => o.Person).Distinct().Count() == 2,
            "Observer receipt queries preserve explicit receipt owners rather than exposing raw hidden end reasons.");
        var restored = GameCodec.Decode(GameCodec.Encode(f.World));
        PeopleFixture.Check(restored.StateHash() == f.World.StateHash() && JsonSerializer.Serialize(restored.PeopleKnownOaths(f.First).Single()) == before,
            "Actual authority and separately received knowledge survive the production save independently.");
    }
}

public sealed class PeoplePublicRequestReceiptPrivacyScenario : IFeatureScenario
{
    public string Name => "INV-008/009/015 people receipts public: remote rejection and hidden authority cannot change an undelivered request belief";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture(); var request = f.Request("requestAssessment", f.First, f.Second, 21);
        f.Control(f.Second); f.Send("explore", f.Second); f.World.AdvanceTo(1); f.Send("travel", f.Second, 1);
        f.Cancel(f.First); f.World.AdvanceTo(2);
        var received = JsonSerializer.Serialize(f.World.PeopleRequests(f.First));
        f.Control(f.Second); f.Reject("rejectPeopleRequest", f.Second, request); f.Control(f.First);
        PeopleFixture.Check(JsonSerializer.Serialize(f.World.PeopleRequests(f.First)) == received,
            "A remote attempt cannot deliver a response or change the proposer's received pending request.");
        var hidden = PeoplePublicFixture.Configure(f.World, state => state.PeopleDomain = state.PeopleDomain with
        {
            Social = state.PeopleDomain.Social with { Sessions = state.PeopleDomain.Social.Sessions.Select(s => s.Id == request ? s with
                { Status = Chronicles.Game.People.PeopleSessionStatus.Cancelled, Ended = state.Month, CompletionSource = "unreceived authority-only alternative" } : s).ToArray() }
        });
        PeopleFixture.Check(JsonSerializer.Serialize(hidden.PeopleRequests(f.First)) == received,
            "A coherent alternate hidden authority status cannot act as an undelivered personal receipt.");
        f.Cancel(f.Second); f.Send("travel", f.Second, 0); f.World.AdvanceTo(3); f.Send("rejectPeopleRequest", f.Second, request);
        f.Control(f.First);
        PeopleFixture.Check(f.World.PeopleRequests(f.First).Length == 0 && f.World.Checkpoint().People.Single(p => p.Id == f.First).Journal
            .Any(e => e.Source == $"people:declined:{request}"), "An actual face-to-face response reaches both journals and updates the recipient's request knowledge.");
    }
}

public sealed class PeoplePublicDetachedDetailScenario : IFeatureScenario
{
    public string Name => "INV-004/009 people queries public: mutating nested observer aptitude, traits, soul, evaluations and claim arrays cannot alter authority";
    public void Run()
    {
        var f = new PeopleSocialPublicFixture(); var request = f.Request("requestOath", f.First, f.Second, data: "조회 불변성의 실제 약속");
        f.Consent(f.Second, request); f.World.AdvanceTo(1); f.Cancel(f.First);
        f.Send("breakOath", f.First, f.World.Checkpoint().PeopleDomain.Social.Oaths.Single().Id); f.Send("observe");
        var hash = f.World.StateHash(); var detail = f.World.PeopleDetail(f.First)!;
        detail.Innate!.ElementRoots[0] = 0; detail.Innate.Foundations[0] = 0;
        detail.CurrentTalents[0] = detail.CurrentTalents[0] with { Learned = 9999 };
        detail.CurrentCognition[0] = detail.CurrentCognition[0] with { Practice = 9999 };
        detail.Genome!.Genes[0] = detail.Genome.Genes[0] with { FirstAllele = 0 };
        detail.TraitHistory![0].History[0] = detail.TraitHistory[0].History[0] with { EventId = "mutated-external-trait-array" };
        detail.ActualSoul!.Values[0] = detail.ActualSoul.Values[0] with { DefinitionIds = ["mutated-external-soul-array"] };
        detail.ActualEvaluations![0].Population[0] = detail.ActualEvaluations[0].Population[0] with { AbsoluteIndex = 0 };
        detail.KnownReputation[0].Provenance[0] = detail.KnownReputation[0].Provenance[0] with { EvidenceId = "mutated-external-claim-array" };
        var received = f.World.PeopleReceivedClaims(f.First); received[0].Provenance[0] = received[0].Provenance[0] with { EvidenceId = "mutated-other-query" };
        PeopleFixture.Check(f.World.StateHash() == hash && f.World.PeopleDetail(f.First)!.Innate!.Foundations[0] != 0,
            "Every nested observer array is a detached value projection, not a mutable alias of people authority.");
        Invariants.Validate(f.World.Checkpoint());
    }
}
