using System.Globalization;
using Chronicles.Game;

internal static class ReincarnationScenarios
{
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run(int route)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.disasterChance"] = 0,
            ["soul.crossing"] = 0, ["founder.ascensionProtection"] = 0, ["hazard.sealChance"] = 10000,
            ["hazard.sealDuration"] = 1, ["hazard.portalChance"] = 10000 });
        var state = world.Checkpoint(); var person = state.People[0];
        person.Realm = route switch { 1 or 2 or 3 => Realm.Spirit, 4 or 5 => Realm.Void,
            6 or 7 => Realm.Integration, 8 or 9 => Realm.Tribulation, 10 or 11 => Realm.Mahayana, _ => Realm.HalfImmortal };
        person.GreatPerfection = true;
        switch (route)
        {
            case 1: person.Lifespan = 1201; break;
            case 2:
                state.People[1].Realm = Realm.HalfImmortal;
                person.Health = 1; break;
            case 3:
                state.Materials["food"].Loss += state.Items.Where(i => i.Owner == person.Id && i.Definition == "food").Sum(i => i.Quantity);
                state.Items.RemoveAll(i => i.Owner == person.Id && i.Definition == "food");
                person.Health = 1; break;
            case 4: person.Place = 8; person.KnownPlaces.Add(8); break;
            case 5: person.StructureDamage = 99; state.Places[0].Turbidity = 100; break;
            case 6: person.BondStability = 1; person.Health = 30; state.Places[0].Turbidity = 0; break;
            case 7: state.Laws["world.capacityRealm"] = 6; break;
            case 8:
                person.Layer = 1; person.GreatPerfection = false; person.PendingLayer = 2;
                person.Action = ActionKind.Trial; person.Due = 1; state.Laws["cultivation.breakthroughRisk"] = 10000; break;
            case 9:
                person.Work = 1000000000; person.PendingLayer = 9; person.Action = ActionKind.Trial; person.Due = 1;
                person.Health = 1; state.Laws["cultivation.breakthroughRisk"] = 0; state.Laws["hazard.trialDamage"] = 100; break;
            case 10: person.Place = 89; person.KnownPlaces.Add(89); person.KnownPlaces.Add(99); break;
            case 11:
                person.Health = 1; person.StructureDamage = 99; state.Laws["soul.pactDuration"] = 1;
                state.Items.Add(new Item { Id = state.NextId++, Definition = "soulWard", Owner = person.Id,
                    Place = -1, Quantity = 1, Quality = 5000, Source = "원인 생산자 시험용 실제 실물 fixture" }); break;
            case 12: state.Laws["world.capacityRealm"] = 9; break;
            case 13: person.Place = 99; person.KnownPlaces.Add(99); break;
        }
        if (route == 11) state.Materials["soulWard"] = new MaterialFlow { Opening = 1 };
        world = GameWorld.Restore(state);
        void Send(string kind, long target = 0, long actor = 0)
        {
            var result = world.Execute(new(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), actor == 0 ? person.Id : actor, kind, target));
            Assert(result.Accepted, result.Message);
        }
        if (route != 2) Send("possess");
        switch (route)
        {
            case 2: Send("attack", person.Id, state.People[1].Id); break;
            case 4: Send("explore"); world.AdvanceTo(1); Assert(world.Checkpoint().People[0].Sealed, "실제 금제 발동"); break;
            case 5 or 6: Send("cultivate"); break;
            case 10: Send("travel", 99); break;
            case 11: Send("use", state.Items.Single(i => i.Definition == "soulWard").Id); break;
            case 13: Send("ascend"); break;
        }
        if (route is not 2 and not 13) world.AdvanceTo(route == 4 ? 2 : 1);
        state = world.Checkpoint();
        var soul = state.Souls.Single(s => route == 13 ? s.Parent == person.SoulId : s.Id == person.SoulId);
        Assert(soul.Route == route && soul.Status == "waiting", $"RC-{route:00} 실제 원인→대기: {soul.Route}/{soul.Status}");
        if (route is 7 or 12 or 13) Assert(state.DeathCount == 0, "세계 이탈/비승은 사망 아님");
        Assert(GameCodec.Decode(GameCodec.Encode(world)).StateHash() == world.StateHash(), "원인/여정 왕복");
    }
}
