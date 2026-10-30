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
