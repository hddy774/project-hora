using System.Globalization;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class InventoryWorldFixture
{
    internal static GameWorld Create(ulong seed = 42, IReadOnlyDictionary<string, long>? extraLaws = null)
    {
        var laws = new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.spoilage"] = 0,
            ["ecology.disasterChance"] = 0, ["ecology.regrowth"] = 0 };
        if (extraLaws is not null) foreach (var (key, value) in extraLaws) laws[key] = value;
        return GameWorld.Create(seed, laws);
    }
    internal static GameCommand Command(string id, long actor, string kind, long target = 0, long value = 0, string data = "") => new(id, actor, kind, target, value, data);
    internal static void Accept(GameWorld world, GameCommand command)
    { var result = world.Execute(command); InventoryFixture.Check(result.Accepted, command.Kind + ": " + result.Message); }
    internal static void Reject(GameWorld world, GameCommand command)
    {
        var before = world.StateHash(); var result = world.Execute(command);
        InventoryFixture.Check(!result.Accepted && before == world.StateHash(), "Rejected world inventory command changed authority.");
    }
    internal static void Add(GameState state, long actor, string definition, int quantity)
    {
        state.Items.Add(new() { Id = state.NextId++, Owner = actor, Definition = definition, Quantity = quantity, Quality = 5000,
            Place = -1, Source = "explicit functional fixture materials" });
        if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new()); flow.Opening += quantity;
    }
}

public sealed class InventoryPublicContinuationScenario : IFeatureScenario
{
    public string Name => "inventory public: INV-017 continuation keeps every physical reservation backed by one ledger";
    public void Run()
    {
        var world = GameWorld.Create(997);
        for (var month = 1; month <= 240; month++)
        {
            try { world.AdvanceTo(month); }
            catch (InvalidDataException error)
            {
                var state = world.Checkpoint();
                var orphaned = state.Items.Where(i => i.Reserved != 0 && !state.Inventory.Reservations.Any(r => r.Id == i.Reserved && r.Active && r.Items.Contains(i.Id)))
                    .Select(i => string.Create(CultureInfo.InvariantCulture, $"item={i.Id}/{i.Definition}/reserved={i.Reserved}/owner={i.Owner}/source={i.Source}"));
                throw new InvalidOperationException($"Inventory continuation month {month}: {string.Join("; ", orphaned)}", error);
            }
        }
        Invariants.Validate(world.Checkpoint());
    }
}

public sealed class InventoryPublicGatherScenario : IFeatureScenario
{
    public string Name => "inventory public: finite gathering uses scheduled time and survives exact GameCodec continuation";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var before = world.Checkpoint(); var actor = before.People[0].Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("gather-control", actor, "possess"));
        var request = InventoryWorldFixture.Command("real-gather", actor, "gather", data: "food");
        InventoryWorldFixture.Accept(world, request); var active = world.Checkpoint();
        var job = active.Inventory.Jobs.Single(j => j.Actor == actor && j.Status == "processing");
        var source = active.Inventory.Sources.Single(s => s.Id == job.Source);
        InventoryFixture.Check(job.OutputDefinition == "food" && job.OutputQuantity > 0, "Scheduled extraction omitted its own frozen output description.");
        InventoryFixture.Check(active.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity) ==
            before.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity), "Gathering paid inventory before the actual work completed.");
        var hash = world.StateHash(); InventoryWorldFixture.Accept(world, request);
        InventoryFixture.Check(hash == world.StateHash(), "Replayed gather restarted its action or reservation.");
        var restored = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(job.Due); restored.AdvanceTo(job.Due);
        var finished = world.Checkpoint();
        InventoryFixture.Check(world.StateHash() == restored.StateHash(), "Gathering save continuation diverged.");
        InventoryFixture.Check(finished.Inventory.Sources.Single(s => s.Id == source.Id).Remaining == source.Remaining - job.OutputQuantity &&
            finished.Materials["food"].Extraction == job.OutputQuantity && finished.Inventory.Jobs.Single(j => j.Id == job.Id).Status == "completed",
            "Scheduled gather did not debit its finite source and credit one physical output.");
        Invariants.Validate(finished);
    }
}

public sealed class InventoryPublicCraftScenario : IFeatureScenario
{
    public string Name => "inventory public: canonical recipe practice consumes real inputs and cancellation never refunds the process";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        InventoryWorldFixture.Add(state, actor, "herb", 3); InventoryWorldFixture.Add(state, actor, "wood", 1); world = GameWorld.Restore(state);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("craft-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("craft-cancelled", actor, "craft", data: "pill"));
        state = world.Checkpoint(); var job = state.Inventory.Jobs.Single(j => j.Actor == actor && j.Status == "processing");
        InventoryFixture.Check(job.KnowledgeActivity > 0 && state.Materials["herb"].Consumption == 3 && state.Items.All(i => i.Definition != "herb"),
            "Craft did not adopt actual recipe knowledge and consume its real ingredients at the boundary.");
        InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("cannot-work-twice", actor, "appraise", state.Items.First(i => i.Owner == actor && i.Definition == "tool").Id));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("cancel-process", actor, "cancel"));
        state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Jobs.Single(j => j.Id == job.Id).Status == "cancelled" && state.Materials["herb"].Consumption == 3 &&
            state.Items.All(i => i.Definition != "pill" && i.Definition != "herb"), "Cancellation recreated consumed materials or produced a pill.");
        InventoryWorldFixture.Add(state, actor, "herb", 3); InventoryWorldFixture.Add(state, actor, "wood", 1); world = GameWorld.Restore(state);
        var request = InventoryWorldFixture.Command("craft-settled", actor, "craft", data: "pill"); InventoryWorldFixture.Accept(world, request);
        job = world.Checkpoint().Inventory.Jobs.Last(j => j.Actor == actor); var restored = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(job.Due); restored.AdvanceTo(job.Due); state = world.Checkpoint();
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && state.Materials["herb"].Consumption == 6 &&
            state.Inventory.Jobs.Single(j => j.Id == job.Id).Status is "completed" or "failed", "Craft completion/save did not settle one actual process.");
        var hash = world.StateHash(); InventoryWorldFixture.Accept(world, request); InventoryFixture.Check(hash == world.StateHash(), "Craft replay duplicated output or mastery.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicSourceLossScenario : IFeatureScenario
{
    public string Name => "inventory public: actual source loss cancels extraction and capacity growth grants no stock";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("loss-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("loss-gather", actor, "gather", data: "food"));
        state = world.Checkpoint(); var job = state.Inventory.Jobs.Single(j => j.Actor == actor); state.Places[0].Source = 0; world = GameWorld.Restore(state);
        world.AdvanceTo(1); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Jobs.Single(j => j.Id == job.Id).Status == "cancelled" &&
            state.Inventory.Sources.Where(s => s.Place == 0).All(s => s.Remaining == 0) && state.People.Single(p => p.Id == actor).Action == ActionKind.Idle,
            "Destroyed source remained reserved or manufactured a harvest.");
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("loss-observe", 0, "observe"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("larger-source-cap", 0, "law", value: 2000, data: "ecology.sourceCapacity"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("loss-control-again", actor, "possess")); world.AdvanceTo(2); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Sources.Where(s => s.Place == 0).Sum(s => s.Remaining) == 0 &&
            state.Inventory.Sources.Where(s => s.Place == 0 && s.EcologyWeight > 0).Sum(s => s.Capacity) == 2000,
            "Increasing a source capacity minted fresh resources with regeneration disabled.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicAuctionScenario : IFeatureScenario
{
    public string Name => "inventory public: real auction escrow and advertised snapshots settle once through the world scheduler";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var seller = state.People[0].Id; var buyer = state.People[1].Id;
        var item = state.Items.First(i => i.Owner == seller && i.Definition == "tool").Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("auction-seller", seller, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("auction-offer", seller, "offer", item, 5, "auction"));
        state = world.Checkpoint(); var contract = state.Inventory.Contracts.Single(c => c.Item == item);
        InventoryFixture.Check(contract.AdvertisedDefinition == "tool" && contract.AdvertisedQuantity == 1 && contract.AdvertisedName.Length > 0,
            "Listing lacked its own immutable public item description.");
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("auction-buyer", buyer, "possess"));
        var request = InventoryWorldFixture.Command("auction-funded-bid", buyer, "bid", contract.Id, 7); InventoryWorldFixture.Accept(world, request);
        state = world.Checkpoint(); InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("auction-unfunded", buyer, "bid", contract.Id,
            state.People.Single(p => p.Id == buyer).Money + 8));
        var hash = world.StateHash(); InventoryWorldFixture.Accept(world, request); InventoryFixture.Check(hash == world.StateHash(), "Auction replay re-debited its deposit.");
        world.AdvanceTo(contract.Due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Contracts.Single(c => c.Id == contract.Id).Status == "completed" &&
            state.Inventory.Contracts.Single(c => c.Id == contract.Id).Escrow == 0 && state.Items.Single(i => i.Id == item).Owner == buyer &&
            state.Contracts.Single(c => c.Id == contract.Id).Status == "completed", "Auction did not settle physical ownership, escrow and its derived public projection.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicTransportScenario : IFeatureScenario
{
    public string Name => "inventory public: completed route delivers physical cargo and blocked routes keep it at actual origin";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("cargo-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("learn-route", actor, "explore")); world.AdvanceTo(1);
        state = world.Checkpoint(); var item = state.Items.First(i => i.Owner == actor && i.Definition == "tool").Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("start-cargo", actor, "transport", item, 1)); state = world.Checkpoint();
        var cargo = state.Inventory.Contracts.Single(c => c.Item == item && c.Status == "open");
        InventoryFixture.Check(state.Items.Single(i => i.Id == item).Owner == actor && state.People.Single(p => p.Id == actor).Place == 0,
            "Cargo changed ownership or arrived before travel time.");
        world.AdvanceTo(cargo.Due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Contracts.Single(c => c.Id == cargo.Id).Status == "completed" &&
            state.People.Single(p => p.Id == actor).Place == 1 && state.Items.Single(i => i.Id == item).Place == 1,
            "Due cargo was cancelled before its carrier could complete the actual route.");
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("return-cargo", actor, "transport", item, 0)); state = world.Checkpoint();
        cargo = state.Inventory.Contracts.Last(c => c.Item == item); state.Places[0].BlockedUntil = cargo.Due + 1; world = GameWorld.Restore(state);
        world.AdvanceTo(cargo.Due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Contracts.Single(c => c.Id == cargo.Id).Status == "failed" &&
            state.People.Single(p => p.Id == actor).Place == 1 && state.Items.Single(i => i.Id == item).Place == 1 &&
            state.Items.Single(i => i.Id == item).Reserved == 0, "Blocked route teleported the cargo or left it committed forever.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicProfessionalScenario : IFeatureScenario
{
    public string Name => "inventory public: medicine appraisal and source exploration produce real effects and personal evidence";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        state.People[0].Health = 30; InventoryWorldFixture.Add(state, actor, "herb", 2); world = GameWorld.Restore(state);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("professional-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("actual-medicine", actor, "treat")); var due = world.Checkpoint().People[0].Due;
        world.AdvanceTo(due); state = world.Checkpoint();
        InventoryFixture.Check(state.People[0].Health > 30 && state.Materials["herb"].Consumption == 2, "Medical treatment did not consume its real remedy or heal its actual patient.");
        var item = state.Items.First(i => i.Owner == actor && i.Definition == "tool").Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("actual-appraisal", actor, "appraise", item)); due = world.Checkpoint().People[0].Due;
        world.AdvanceTo(due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Appraisals.Any(a => a.Appraiser == actor && a.Item == item), "Appraisal did not create the appraiser's own evidence.");
        var foodConsumed = state.Materials["food"].Consumption;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("actual-exploration", actor, "exploreSource")); due = world.Checkpoint().People[0].Due;
        world.AdvanceTo(due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Observations.Any(o => o.Observer == actor && o.Evidence.StartsWith("actual exploration", StringComparison.Ordinal)) &&
            state.Materials["food"].Consumption == foodConsumed + 1, "Exploration gained source knowledge without spending actual provisions and work.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicArrayScenario : IFeatureScenario
{
    public string Name => "inventory public: actual array installation and upkeep never create natural energy or survive destruction";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        InventoryWorldFixture.Add(state, actor, "array", 1); InventoryWorldFixture.Add(state, actor, "resource.02.0", 1); world = GameWorld.Restore(state);
        var item = state.Items.Single(i => i.Owner == actor && i.Definition == "array").Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("array-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("install-real-array", actor, "use", item)); state = world.Checkpoint();
        var array = state.Inventory.Arrays.Single(a => a.Item == item);
        InventoryFixture.Check(!array.Active, "An unfunded installed array produced an operational effect.");
        var qi = state.Places[0].Qi;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("pay-array-upkeep", actor, "upkeepArray", array.Id));
        world.AdvanceTo(world.Checkpoint().People[0].Due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Arrays.Single(a => a.Item == item).Active && state.Materials["resource.02.0"].Consumption == 1 &&
            state.Places[0].Qi <= qi, "Paid array failed to consume physical fuel or manufactured natural source energy.");
        state.Items.Single(i => i.Id == item).Damage = 100; world = GameWorld.Restore(state); world.AdvanceTo(world.Month + 1); state = world.Checkpoint();
        InventoryFixture.Check(!state.Inventory.Arrays.Single(a => a.Item == item).Active && state.Inventory.Effects.All(e =>
            !e.Cause.StartsWith("array:" + array.Id.ToString(CultureInfo.InvariantCulture) + ":", StringComparison.Ordinal)), "Broken frame kept its paid effects.");
        InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("cannot-fuel-broken-array", actor, "upkeepArray", array.Id));
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicGrowthPulseScenario : IFeatureScenario
{
    public string Name => "inventory public: paid growth dose improves a bottlenecked canonical pulse with real Qi debit and exact save continuation";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(71); var state = world.Checkpoint(); var actor = state.People[0].Id;
        var person = state.People[0]; WorldScenarioFixtures.SetCultivation(state, person, Realm.Qi); person.Health = 50;
        state.Places[person.Place].Turbidity = 0; InventoryWorldFixture.Add(state, actor, "growthPill", 1);
        world = GameWorld.Restore(state); InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("growth-control", actor, "possess"));
        var baseline = GameCodec.Decode(GameCodec.Encode(world));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("paid-growth-use", actor, "use", world.Checkpoint().Items.Single(i => i.Owner == actor && i.Definition == "growthPill").Id));
        state = world.Checkpoint(); var body = state.People.Single(p => p.Id == actor).BodyId;
        InventoryFixture.Check(state.Cultivation[body].RootGrowth.Total == 0 && state.People.Single(p => p.Id == actor).Work == 0,
            "Taking the pill granted free structure work before a real cultivation process.");
        var before = state.Places[0].Qi * (5000 / state.Laws["cultivation.sourceCost"]) + state.CultivationSupply.GetValueOrDefault(0).Total;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("growth-practice", actor, "cultivate", data: "path.1.0"));
        InventoryWorldFixture.Accept(baseline, InventoryWorldFixture.Command("growth-practice", actor, "cultivate", data: "path.1.0"));
        var restored = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(1); restored.AdvanceTo(1); baseline.AdvanceTo(1);
        state = world.Checkpoint(); var normal = baseline.Checkpoint(); person = state.People.Single(p => p.Id == actor);
        var after = state.Places[0].Qi * (5000 / state.Laws["cultivation.sourceCost"]) + state.CultivationSupply.GetValueOrDefault(0).Total;
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && state.Cultivation[body].RootGrowth.Total > normal.Cultivation[body].RootGrowth.Total &&
            before - after == person.Processes[0] && state.Cultivation[body].RootGrowth.Total == person.Processes[3] && person.Roots.SequenceEqual(normal.People.Single(p => p.Id == actor).Roots),
            "Paid growth dose did not help the actual process, duplicated innate roots, or bypassed real source debit.");
        var progress = state.Cultivation[body].RootGrowth.Total; world.AdvanceTo(2); state = world.Checkpoint();
        InventoryFixture.Check(state.Cultivation[body].RootGrowth.Total == progress && InventoryEngine.ActiveEffects(InventoryState.FromWorld(state, state.Inventory), actor, "growth").Count == 0,
            "Expired medicine granted passive or permanent structural work.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicArtifactCycleScenario : IFeatureScenario
{
    public string Name => "inventory public: real armor craft equipment soul binding combat damage and repair preserve one artifact through save";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(73, new Dictionary<string, long> { ["combat.hitMinimum"] = 10000, ["combat.hitMaximum"] = 10000 });
        var state = world.Checkpoint(); var actor = state.People[0].Id; var opponent = state.People[1].Id;
        InventoryWorldFixture.Add(state, actor, "ore", 5); InventoryWorldFixture.Add(state, actor, "cloth", 2); world = GameWorld.Restore(state);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-craft", actor, "craft", data: "armor"));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due); state = world.Checkpoint();
        var job = state.Inventory.Jobs.Single(j => j.Actor == actor && j.Recipe == "armor");
        InventoryFixture.Check(job.Status == "completed", "Deterministic artifact fixture did not complete its physical craft.");
        var item = state.Items.Single(i => i.Owner == actor && i.Definition == "armor").Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-equip", actor, "equip", item));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-bind", actor, "bind", item));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-opponent", opponent, "possess")); state = world.Checkpoint();
        if (state.People.Single(p => p.Id == opponent).Action != ActionKind.Idle)
            InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-opponent-ready", opponent, "cancel"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-real-hit", opponent, "attack", actor)); state = world.Checkpoint();
        var damaged = state.Items.Single(i => i.Id == item).Damage;
        InventoryFixture.Check(damaged > 0 && state.Items.Single(i => i.Id == item).BoundSoul == state.People.Single(p => p.Id == actor).SoulId,
            "Actual combat did not damage the equipped bound physical artifact.");
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-owner", actor, "possess"));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("artifact-repair", actor, "repair", item));
        var restored = GameCodec.Decode(GameCodec.Encode(world)); var due = world.Checkpoint().People.Single(p => p.Id == actor).Due;
        world.AdvanceTo(due); restored.AdvanceTo(due); state = world.Checkpoint();
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && state.Items.Single(i => i.Id == item).Damage < damaged &&
            state.Inventory.Bindings.Single(b => b.Item == item).Soul == state.Items.Single(i => i.Id == item).BoundSoul &&
            state.People.Single(p => p.Id == actor).Equipment[3] == item && state.Materials["ore"].Consumption == 5,
            "Repair/save duplicated equipment, lost its binding or failed to spend real ore.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicTalismanCycleScenario : IFeatureScenario
{
    public string Name => "inventory public: crafted sold talisman pays actual parties and protects one real hit with no use replay";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(79, new Dictionary<string, long> { ["combat.hitMinimum"] = 10000, ["combat.hitMaximum"] = 10000 });
        var state = world.Checkpoint(); var seller = state.People[0].Id; var buyer = state.People[1].Id;
        InventoryWorldFixture.Add(state, seller, "paper", 2); InventoryWorldFixture.Add(state, seller, "ink", 1); world = GameWorld.Restore(state);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("talisman-seller", seller, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("talisman-craft", seller, "craft", data: "talisman"));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == seller).Due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Jobs.Single(j => j.Actor == seller && j.Recipe == "talisman").Status == "completed",
            "Deterministic talisman fixture did not complete its physical craft.");
        var item = state.Items.Single(i => i.Owner == seller && i.Definition == "talisman").Id;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("talisman-offer", seller, "offer", item, 5));
        var contract = world.Checkpoint().Inventory.Contracts.Single(c => c.Item == item);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("talisman-buyer", buyer, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("talisman-trade", buyer, "trade", contract.Id));
        var unwarded = GameCodec.Decode(GameCodec.Encode(world)); var use = InventoryWorldFixture.Command("talisman-use", buyer, "use", item);
        InventoryWorldFixture.Accept(world, use); var hash = world.StateHash(); InventoryWorldFixture.Accept(world, use);
        InventoryFixture.Check(hash == world.StateHash(), "Replayed talisman use recreated its physical defense.");
        foreach (var instance in new[] { world, unwarded })
        {
            InventoryWorldFixture.Accept(instance, InventoryWorldFixture.Command("talisman-attacker", seller, "possess"));
            InventoryWorldFixture.Accept(instance, InventoryWorldFixture.Command("talisman-hit", seller, "attack", buyer));
        }
        state = world.Checkpoint();
        var actual = state.Combat.Exchanges.Last().Strikes.Single(s => s.Attacker == seller && s.Defender == buyer);
        var without = unwarded.Checkpoint().Combat.Exchanges.Last().Strikes.Single(s => s.Attacker == seller && s.Defender == buyer);
        InventoryFixture.Check(actual.Hit && actual.Damage == Math.Max(1, without.Damage / 2) && state.Items.All(i => i.Id != item) &&
            state.People.Single(p => p.Id == buyer).Traits.All(t => t.Id != "호신부") && state.Materials["talisman"].Consumption == 1 &&
            state.Inventory.Contracts.Single(c => c.Id == contract.Id).PaidAmount == 5,
            "Talisman was not consumed once, did not protect its actual buyer, or fabricated a market payment.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryPublicFarmPatientCycleScenario : IFeatureScenario
{
    public string Name => "inventory public: successful real seed water farming and agreed treatment heal an actual other patient";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(83); var state = world.Checkpoint(); var farmer = state.People[0].Id; var patient = state.People[1].Id;
        state.People[1].Health = 40; state.People[1].Personality[4] = 100;
        InventoryWorldFixture.Add(state, farmer, "resource.12.0", 1); InventoryWorldFixture.Add(state, farmer, "resource.06.0", 1);
        InventoryWorldFixture.Add(state, farmer, "herb", 2); world = GameWorld.Restore(state);
        var food = state.Items.Where(i => i.Owner == farmer && i.Definition == "food").Sum(i => i.Quantity);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("farm-control", farmer, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("actual-farm", farmer, "farm"));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == farmer).Due); state = world.Checkpoint();
        InventoryFixture.Check(state.Inventory.Jobs.Single(j => j.Actor == farmer && j.Recipe == "farm.food").Status == "completed" &&
            state.Items.Where(i => i.Owner == farmer && i.Definition == "food").Sum(i => i.Quantity) == food + InventoryDefinitions.Recipes["farm.food"].Quantity &&
            state.Materials["resource.12.0"].Consumption == 1 && state.Materials["resource.06.0"].Consumption == 1 && state.Places[0].Facilities.Contains("경작지"),
            "Farm did not consume real seed/water and deliver its actual food and prepared plot.");
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("patient-control", patient, "possess"));
        if (world.Checkpoint().People.Single(p => p.Id == patient).Action != ActionKind.Idle)
            InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("patient-ready", patient, "cancel"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("doctor-control", farmer, "possess"));
        var before = world.Checkpoint().People.Single(p => p.Id == patient).Health;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("actual-other-patient", farmer, "treat", patient));
        var restored = GameCodec.Decode(GameCodec.Encode(world)); var due = world.Checkpoint().People.Single(p => p.Id == farmer).Due;
        world.AdvanceTo(due); restored.AdvanceTo(due); state = world.Checkpoint();
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && state.People.Single(p => p.Id == patient).Health > before + state.Laws["cultivation.recovery"] &&
            state.Materials["herb"].Consumption == 2, "Agreed other-patient medicine had no effect beyond natural recovery or did not consume real herbs.");
        Invariants.Validate(state);
    }
}
