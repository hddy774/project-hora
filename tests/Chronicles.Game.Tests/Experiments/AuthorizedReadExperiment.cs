using System.Collections.ObjectModel;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Experiments;

internal sealed record KnownSharedItem(long Item, string Definition, long Owner, long ObservedMonth);
internal sealed record MarketReadInput(long Actor, long Money, IReadOnlyList<AdvertisedInventoryOffer> Offers,
    IReadOnlyList<KnownSharedItem> SharedItems, string Fingerprint);
internal sealed record MarketReadRequest(string Kind, string Definition);
internal sealed record MarketReadResult(string Kind, string Definition, long? Contract, long? Price, IReadOnlyList<long> SharedTools);
internal sealed record InventoryReadRow(long Id, string Definition, string Name, int Quantity, string Quality,
    long Container, int? EquippedSlot, bool? IsCarried);
internal sealed record KnowledgeReadRow(string WorkId, int Chapter, string Title, int Comprehension, int Mastery, string SourceId);
internal sealed record PersonalReadInput(long Actor, long Generation, long Context, IReadOnlyList<InventoryReadRow> Items,
    IReadOnlyList<KnowledgeReadRow> Knowledge, string Fingerprint);
internal sealed record PersonalReadRequest(bool Open, string Kind, string Key, int Offset = 0, int Limit = 16);
internal sealed record PersonalReadResponse(string Kind, string Key, int Total, IReadOnlyList<string> Rows);

/// <summary>Actual public offers, finite founding stock and one accepted public domain GrantUse; no injected people or goods.</summary>
internal sealed class PublicReadFixture
{
    internal GameWorld World { get; }
    internal long Seller { get; }
    internal long Viewer { get; }
    internal int Listings { get; }
    internal string Hash { get; }
    internal string CommandHash { get; }
    internal byte[] Envelope { get; }
    internal MarketReadInput Market { get; }
    internal PersonalReadInput Personal { get; }

    private PublicReadFixture(GameWorld world, long seller, long viewer, int listings, IReadOnlyList<string> commands)
    {
        World = world; Seller = seller; Viewer = viewer; Listings = listings;
        Hash = world.StateHash(); CommandHash = ExecutionExperimentScenarios.Hash(commands);
        Envelope = GameCodec.Encode(world, 1);
        Market = CaptureMarket(world, viewer); Personal = CapturePersonal(world, viewer);
        ExecutionExperimentScenarios.Require(world.StateHash() == Hash, "준비 조회가 세계를 수정했습니다.");
    }

    internal static PublicReadFixture Create(int listings)
    {
        if (listings is < 2 or > 10000) throw new ArgumentOutOfRangeException(nameof(listings));
        var world = GameWorld.Create(42, new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["founder.initialFood"] = checked(listings * 4L + 64)
        });
        var opening = world.Checkpoint(); var seller = opening.People[0].Id; var viewer = opening.People[1].Id;
        var food = opening.Items.Single(item => item.Owner == seller && item.Definition == "food").Id;
        var commands = new List<string>();
        for (var index = 0; index < listings; index++)
        {
            Send(new("ex:split:" + index, seller, "split", food, 1));
            var portion = world.Checkpoint().Items.Single(item => item.Owner == seller && item.Definition == "food" &&
                item.Quantity == 1 && item.Reserved == 0).Id;
            Send(new("ex:offer:" + index, seller, "offer", portion, 1 + index % 7, index % 4 == 0 ? "auction" : ""));
        }
        var state = world.Checkpoint(); var tool = state.Items.Single(item => item.Owner == seller && item.Definition == "tool").Id;
        var grant = new GrantUse("ex:shared-real-tool", seller, tool, viewer, true);
        var candidate = InventoryEngine.Apply(InventoryState.FromWorld(state, state.Inventory), grant);
        ExecutionExperimentScenarios.Require(candidate.Accepted, candidate.Message);
        state.Inventory = candidate.State.ApplyTo(state); world = GameWorld.Restore(state);
        commands.Add("public-domain:GrantUse:" + ExecutionExperimentScenarios.Hash(grant));
        Send(new("ex:viewer", viewer, "possess"));
        return new(world, seller, viewer, listings, commands);

        void Send(GameCommand command)
        {
            var result = world.Execute(command);
            ExecutionExperimentScenarios.Require(result.Accepted, command.Kind + ": " + result.Message);
            commands.Add("GameWorld.Execute:" + ExecutionExperimentScenarios.Hash(command));
        }
    }

    internal static MarketReadInput CaptureMarket(GameWorld world, long viewer)
    {
        var state = world.Checkpoint(); var inventory = InventoryState.FromWorld(state, state.Inventory);
        var money = state.People.Single(person => person.Id == viewer).Money;
        var offers = Array.AsReadOnly(InventoryKnowledgeQueries.LocalOffers(inventory, viewer).ToArray());
        // Authorization precedes candidate indexing; no quality/wealth/knowledge of a hidden person is copied into this input.
        var shared = Array.AsReadOnly(state.Items.Where(item => item.Definition == "tool" &&
                (item.Owner == viewer || state.Inventory.Metadata.Any(row => row.Item == item.Id && row.UseRights.Contains(viewer))) &&
                InventoryEngine.IsAccessible(inventory, viewer, item.Id, requireOwnership: false))
            .OrderBy(item => item.Id).Select(item => new KnownSharedItem(item.Id, item.Definition, item.Owner, state.Month)).ToArray());
        return new(viewer, money, offers, shared, ExecutionExperimentScenarios.Hash(new { viewer, money, offers, shared }));
    }

    internal static PersonalReadInput CapturePersonal(GameWorld world, long viewer)
    {
        var view = world.View(viewer);
        var items = Array.AsReadOnly(view.Items.OrderBy(item => item.Id).Select(item => new InventoryReadRow(item.Id,
            item.Definition, item.Name, item.Quantity, item.Quality, item.Container, item.EquippedSlot, item.IsCarried)).ToArray());
        var knowledge = Array.AsReadOnly((view.Detail?.Knowledge?.Chapters ?? []).OrderBy(row => row.WorkId, StringComparer.Ordinal)
            .ThenBy(row => row.Chapter).Select(row => new KnowledgeReadRow(row.WorkId, row.Chapter, row.WorkTitle,
                row.Comprehension, row.Mastery, row.SourceId)).ToArray());
        return new(viewer, view.Generation, view.Revision, items, knowledge,
            ExecutionExperimentScenarios.Hash(new { viewer, view.Generation, view.Revision, items, knowledge }));
    }
}

internal static class AuthorizedReadExperiment
{
    private static readonly MarketReadRequest[] MarketRequests = [new("sale", "food"), new("auction", "food"), new("sale", "not-observed")];
    private static readonly PersonalReadRequest[] PersonalRequests =
    [new(true, "inventory", "food"), new(true, "inventory", "tool"), new(true, "knowledge", "entry"),
        new(true, "knowledge", "not-observed"), new(true, "equipment", ""), new(true, "container", "0"), new(false, "inventory", "food")];

    internal static ReadStorageComparison MeasureMarket(PublicReadFixture warm, PublicReadFixture measured, ReadStorageExperimentOptions options)
    {
        var correctness = VerifyMarket(measured);
        var expected = ExecutionExperimentScenarios.Hash(ScanMarket(measured.Market));
        var indexed = new MarketReadIndex(measured.Market);
        var warmIndexed = new MarketReadIndex(warm.Market);
        var series = new[]
        {
            ExecutionExperimentScenarios.Sample("EX-03", "authorized-full-scan", "동일 공개 광고/사용권 DTO의 전 후보·명시 규칙·결과 직렬화", "세계 DTO 캡처는 별도 준비 비용", warm, measured, options,
                fixture => Output(ScanMarket(fixture.Market)), expected),
            ExecutionExperimentScenarios.Sample("EX-03", "authorized-index-build-and-select", "같은 허가 입력의 명시 ID/종류 색인 구축·모든 후보 규칙·결과 직렬화", "세계 DTO 캡처는 별도 준비 비용", warm, measured, options,
                fixture => Output(new MarketReadIndex(fixture.Market).Read(fixture.Market)), expected),
            ExecutionExperimentScenarios.Sample("EX-03", "authorized-index-reuse", "같은 불변 입력 지문 확인·이미 구축한 ID 색인 읽기·결과 직렬화", "색인 구축은 별도 cold variant/준비 비용", warm, measured, options,
                fixture => Output((ReferenceEquals(fixture, warm) ? warmIndexed : indexed).Read(fixture.Market)), expected),
            ExecutionExperimentScenarios.Sample("EX-03", "world-trade-scan-end-to-end", "GameCodec 복원·후보 scan·실제 공개 sale 정산/재전송 검증·최종hash", "fixture 창세/출품은 별도 준비 비용", warm, measured, options,
                fixture => new(Settle(fixture, ScanMarket(fixture.Market)[0])), correctness.SettlementHash),
            ExecutionExperimentScenarios.Sample("EX-03", "world-trade-index-end-to-end", "GameCodec 복원·허가 입력 index 구축·실제 공개 sale 정산/재전송 검증·최종hash", "fixture 창세/출품은 별도 준비 비용", warm, measured, options,
                fixture => new(Settle(fixture, new MarketReadIndex(fixture.Market).Read(fixture.Market)[0])), correctness.SettlementHash)
        };
        return new("EX-03", "고정 후보 규칙의 완전일치·선택/누락·실제 사용권과 sale 정산. 제품 AI의 전체 목표 정책/장기 행동 분포를 대체하지 않음.",
            measured.Hash, measured.CommandHash, correctness, series, "revisit", "원시 비용과 실제 동일 결과를 비교한 뒤 채택 판단. 제품 후보 경로 변경 없음.");
    }

    internal sealed record MarketCorrectness(bool SameChoices, bool NoHiddenInput, bool RevokedGrantRemoved,
        bool ConservedSettlement, string SettlementHash, IReadOnlyDictionary<string, int> FixedRequestDecisionDistribution);

    internal static MarketCorrectness VerifyMarket(PublicReadFixture fixture)
    {
        var scan = ScanMarket(fixture.Market); var index = new MarketReadIndex(fixture.Market);
        ExecutionExperimentScenarios.Require(ExecutionExperimentScenarios.Hash(scan) == ExecutionExperimentScenarios.Hash(index.Read(fixture.Market)), "실제 후보 누락/선택 차이");
        var hidden = PrivateSplit(fixture);
        ExecutionExperimentScenarios.Require(PublicReadFixture.CaptureMarket(hidden, fixture.Viewer).Fingerprint == fixture.Market.Fingerprint,
            "타인 비공개 split이 후보 입력으로 들어왔습니다.");
        var revoked = GameCodec.Decode(fixture.Envelope); var state = revoked.Checkpoint();
        var shared = fixture.Market.SharedItems.Single(item => item.Owner == fixture.Seller);
        var command = new GrantUse("ex:revoke-real-tool", fixture.Seller, shared.Item, fixture.Viewer, false);
        var candidate = InventoryEngine.Apply(InventoryState.FromWorld(state, state.Inventory), command);
        ExecutionExperimentScenarios.Require(candidate.Accepted, candidate.Message);
        state.Inventory = candidate.State.ApplyTo(state); revoked = GameWorld.Restore(state);
        var input = PublicReadFixture.CaptureMarket(revoked, fixture.Viewer);
        ExecutionExperimentScenarios.Require(input.SharedItems.All(item => item.Item != shared.Item) &&
            input.SharedItems.Count == fixture.Market.SharedItems.Count - 1 && !index.Accepts(input), "철회된 실제 사용권의 낡은 색인을 재사용했습니다.");
        ExecutionExperimentScenarios.Require(ExecutionExperimentScenarios.Hash(ScanMarket(input)) ==
            ExecutionExperimentScenarios.Hash(new MarketReadIndex(input).Read(input)), "실제 사용권 철회 뒤 재구축한 후보가 다릅니다.");
        var settlement = Settle(fixture, scan[0]);
        ExecutionExperimentScenarios.Require(settlement == Settle(fixture, index.Read(fixture.Market)[0]), "실제 정산 결과 차이");
        return new(true, true, true, true, settlement, new ReadOnlyDictionary<string, int>(scan.GroupBy(row => row.Contract is null ? "선택 없음" : row.Kind)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)));
    }

    private static MarketReadResult[] ScanMarket(MarketReadInput input) => MarketRequests.Select(request => Select(input,
        request, input.Offers.Where(offer => offer.Kind == request.Kind && offer.Definition == request.Definition), input.SharedItems)).ToArray();

    private static MarketReadResult Select(MarketReadInput input, MarketReadRequest request, IEnumerable<AdvertisedInventoryOffer> offers,
        IEnumerable<KnownSharedItem> tools)
    {
        var chosen = offers.Where(offer => offer.Seller != input.Actor && (offer.Kind == "sale" ? offer.Price : offer.MinimumBid) is { } price && price <= input.Money)
            .OrderBy(offer => offer.Kind == "sale" ? offer.Price : offer.MinimumBid).ThenBy(offer => offer.Due).ThenBy(offer => offer.Contract).FirstOrDefault();
        return new(request.Kind, request.Definition, chosen?.Contract, chosen is null ? null : chosen.Kind == "sale" ? chosen.Price : chosen.MinimumBid,
            Array.AsReadOnly(tools.Where(item => item.Definition == "tool").Select(item => item.Item).Order().ToArray()));
    }

    private sealed class MarketReadIndex
    {
        private readonly string fingerprint;
        private readonly Dictionary<long, AdvertisedInventoryOffer> offersById;
        private readonly Dictionary<(string Kind, string Definition), long[]> candidateIds;
        private readonly KnownSharedItem[] tools;
        internal MarketReadIndex(MarketReadInput input)
        {
            fingerprint = input.Fingerprint; offersById = input.Offers.ToDictionary(offer => offer.Contract);
            candidateIds = input.Offers.GroupBy(offer => (offer.Kind, offer.Definition)).ToDictionary(group => group.Key, group => group.Select(offer => offer.Contract).ToArray());
            tools = input.SharedItems.Where(item => item.Definition == "tool").OrderBy(item => item.Item).ToArray();
        }
        internal bool Accepts(MarketReadInput input) => input.Fingerprint == fingerprint;
        internal MarketReadResult[] Read(MarketReadInput input)
        {
            ExecutionExperimentScenarios.Require(Accepts(input), "실제 권한/입력 revision이 다른 후보 색인");
            return MarketRequests.Select(request => Select(input, request,
                (candidateIds.GetValueOrDefault((request.Kind, request.Definition)) ?? []).Select(id => offersById[id]), tools)).ToArray();
        }
    }

    private static string Settle(PublicReadFixture fixture, MarketReadResult choice)
    {
        if (choice.Kind != "sale" || choice.Contract is not { } contractId) throw new InvalidOperationException("실제 sale 선택이 필요합니다.");
        var world = GameCodec.Decode(fixture.Envelope); var initial = world.Checkpoint();
        var command = new GameCommand("ex:actual-settlement", fixture.Viewer, "trade", contractId);
        var result = world.Execute(command); ExecutionExperimentScenarios.Require(result.Accepted, result.Message);
        var state = world.Checkpoint(); var contract = state.Inventory.Contracts.Single(row => row.Id == choice.Contract);
        ExecutionExperimentScenarios.Require(contract.Status == "completed" && state.Items.Single(item => item.Id == contract.Item).Owner == fixture.Viewer &&
            state.People.Sum(person => person.Money) == initial.People.Sum(person => person.Money) &&
            state.Items.Sum(item => (long)item.Quantity) == initial.Items.Sum(item => (long)item.Quantity), "실제 정산이 돈/물품을 만들었습니다.");
        var hash = world.StateHash(); ExecutionExperimentScenarios.Require(world.Execute(command).Accepted && world.StateHash() == hash, "정산 재전송이 자산을 복제했습니다.");
        Invariants.Validate(state); return hash;
    }

    internal static ReadStorageComparison MeasureQueries(PublicReadFixture warm, PublicReadFixture measured, ReadStorageExperimentOptions options)
    {
        var correctness = VerifyQueries(measured); var expected = ExecutionExperimentScenarios.Hash(ScanPersonal(measured.Personal));
        var cache = new PersonalReadCache(measured.Personal); var warmCache = new PersonalReadCache(warm.Personal);
        var series = new[]
        {
            ExecutionExperimentScenarios.Sample("EX-06", "authorized-full-scan", "같은 권한 선필터 DTO의 검색·정렬·집계·page·장비/용기·응답 직렬화", "권한 DTO 준비는 별도", warm, measured, options,
                fixture => Output(ScanPersonal(fixture.Personal)), expected),
            ExecutionExperimentScenarios.Sample("EX-06", "read-index-build-and-search", "허가된 snapshot의 read index 구축·동일 요청/정렬/집계/페이지·응답 직렬화", "권한 DTO 준비는 별도", warm, measured, options,
                fixture => Output(new PersonalReadIndex(fixture.Personal).Read(fixture.Personal)), expected),
            ExecutionExperimentScenarios.Sample("EX-06", "immutable-cache-build-and-read", "허가 snapshot의 index·행 materialization·불변 cache 구축/읽기·응답 직렬화", "권한 DTO 준비는 별도", warm, measured, options,
                fixture => Output(new PersonalReadCache(fixture.Personal).Read(fixture.Personal)), expected),
            ExecutionExperimentScenarios.Sample("EX-06", "immutable-cache-reopen", "Actor/세대/개인 입력 지문 검사·불변 캐시 재열기·응답 직렬화", "cache 최초 구축은 cold variant, native 할당은 미포함", warm, measured, options,
                fixture => Output((ReferenceEquals(fixture, warm) ? warmCache : cache).Read(fixture.Personal)), expected),
            ExecutionExperimentScenarios.Sample("EX-06", "world-view-scan-end-to-end", "실제 World.View 권한필터·개인 입력 복사/지문·검색/집계/직렬화 전체", "fixture 생성은 별도", warm, measured, options,
                fixture => Output(ScanPersonal(PublicReadFixture.CapturePersonal(fixture.World, fixture.Viewer))), expected),
            ExecutionExperimentScenarios.Sample("EX-06", "world-view-index-end-to-end", "실제 World.View 권한필터·개인 입력 복사/지문·index 구축/검색/직렬화 전체", "fixture 생성은 별도", warm, measured, options,
                fixture => { var input = PublicReadFixture.CapturePersonal(fixture.World, fixture.Viewer); return Output(new PersonalReadIndex(input).Read(input)); }, expected),
            ExecutionExperimentScenarios.Sample("EX-06", "closed-window-no-view", "닫힌 요청 gate·권위조회 호출0/행 생성0·빈 응답 직렬화", "실제 브라우저/렌더/호스트 요청 비용은 별도", warm, measured, options,
                fixture => Output(ReadWindow(false, () => PublicReadFixture.CapturePersonal(fixture.World, fixture.Viewer))),
                ExecutionExperimentScenarios.Hash(Array.Empty<PersonalReadResponse>()))
        };
        return new("EX-06", "현재 실제 개인 DTO의 첫 지식 페이지/실물·장비·용기와 고정 열기/닫기 요청. 전체 도감/가상 UI 렌더·Android 입력 비용이 아님.",
            measured.Hash, measured.CommandHash, correctness, series, "revisit", "모든 경로는 권한 필터 뒤 같은 snapshot을 사용. 캐시/index cold 비용과 전체 DTO 비용을 함께 비교해야 함.");
    }

    internal sealed record QueryCorrectness(bool EqualRowsCountsPages, bool HiddenStateNoninterference,
        bool OwnEquipmentInvalidatesCache, bool ClosedFramesHaveNoRows, bool Pure, int InventoryRows, int KnowledgeRows);

    internal static QueryCorrectness VerifyQueries(PublicReadFixture fixture)
    {
        var expected = ExecutionExperimentScenarios.Hash(ScanPersonal(fixture.Personal));
        var index = new PersonalReadIndex(fixture.Personal); var cache = new PersonalReadCache(fixture.Personal);
        ExecutionExperimentScenarios.Require(ExecutionExperimentScenarios.Hash(index.Read(fixture.Personal)) == expected &&
            ExecutionExperimentScenarios.Hash(cache.Read(fixture.Personal)) == expected, "개인 검색/집계/page 의미가 다릅니다.");
        var hidden = PrivateSplit(fixture); var same = PublicReadFixture.CapturePersonal(hidden, fixture.Viewer);
        ExecutionExperimentScenarios.Require(same.Fingerprint == fixture.Personal.Fingerprint && ExecutionExperimentScenarios.Hash(cache.Read(same)) == expected,
            "타인 실제 비공개 split으로 개인 캐시/검색이 달라졌습니다.");
        var changed = GameCodec.Decode(fixture.Envelope); var tool = changed.Checkpoint().Items.Single(item => item.Owner == fixture.Viewer && item.Definition == "tool");
        var result = changed.Execute(new("ex:actual-own-equip", fixture.Viewer, "equip", tool.Id));
        ExecutionExperimentScenarios.Require(result.Accepted, result.Message);
        var after = PublicReadFixture.CapturePersonal(changed, fixture.Viewer);
        ExecutionExperimentScenarios.Require(after.Fingerprint != fixture.Personal.Fingerprint && !cache.Accepts(after), "실제 자기 장비 변경에 낡은 캐시를 허용했습니다.");
        ExecutionExperimentScenarios.Require(ExecutionExperimentScenarios.Hash(ScanPersonal(after)) == ExecutionExperimentScenarios.Hash(new PersonalReadIndex(after).Read(after)),
            "실제 자기 장비 변경 뒤 index를 갱신한 결과가 다릅니다.");
        var captures = 0; var closed = ReadWindow(false, () => { captures++; return PublicReadFixture.CapturePersonal(fixture.World, fixture.Viewer); });
        ExecutionExperimentScenarios.Require(captures == 0 && closed.Length == 0 && !PersonalRequests[^1].Open && cache.Read(fixture.Personal)[^1].Rows.Count == 0 &&
            cache.Read(fixture.Personal)[^1].Total == 0 && fixture.World.StateHash() == fixture.Hash,
            "닫힌 요청이 행을 만들거나 조회가 권위를 바꾸었습니다.");
        return new(true, true, true, true, true, fixture.Personal.Items.Count, fixture.Personal.Knowledge.Count);
    }

    private static GameWorld PrivateSplit(PublicReadFixture fixture)
    {
        var state = fixture.World.Checkpoint(); var food = state.Items.Single(item => item.Owner == fixture.Seller && item.Definition == "food" && item.Reserved == 0);
        var result = InventoryEngine.Apply(InventoryState.FromWorld(state, state.Inventory), new SplitInventoryItem("ex:private-real-split", fixture.Seller, food.Id, 1));
        ExecutionExperimentScenarios.Require(result.Accepted, result.Message); state.Inventory = result.State.ApplyTo(state);
        return GameWorld.Restore(state);
    }

    private static PersonalReadResponse[] ScanPersonal(PersonalReadInput input) => PersonalRequests.Select(request => Respond(input, request,
        input.Items.Where(item => request.Kind switch
        {
            "inventory" => item.Definition == request.Key, "equipment" => item.EquippedSlot is not null,
            "container" => item.Container.ToString(System.Globalization.CultureInfo.InvariantCulture) == request.Key, _ => false
        }), input.Knowledge.Where(row => row.WorkId == request.Key))).ToArray();

    private static PersonalReadResponse[] ReadWindow(bool open, Func<PersonalReadInput> capture) => open ? ScanPersonal(capture()) : [];

    private static PersonalReadResponse Respond(PersonalReadInput input, PersonalReadRequest request, IEnumerable<InventoryReadRow> items, IEnumerable<KnowledgeReadRow> knowledge)
    {
        _ = input;
        if (!request.Open) return new(request.Kind, request.Key, 0, Array.Empty<string>());
        var rows = request.Kind == "knowledge" ? knowledge.OrderBy(row => row.Chapter).Select(row => ExecutionExperimentScenarios.Hash(row)).ToArray()
            : items.OrderBy(row => row.Id).Select(row => ExecutionExperimentScenarios.Hash(row)).ToArray();
        return new(request.Kind, request.Key, rows.Length, Array.AsReadOnly(rows.Skip(request.Offset).Take(request.Limit).ToArray()));
    }

    private sealed class PersonalReadIndex
    {
        private readonly string fingerprint;
        private readonly Dictionary<string, InventoryReadRow[]> definitions;
        private readonly Dictionary<long, InventoryReadRow[]> containers;
        private readonly InventoryReadRow[] equipment;
        private readonly Dictionary<string, KnowledgeReadRow[]> knowledge;
        internal PersonalReadIndex(PersonalReadInput input)
        {
            fingerprint = input.Fingerprint;
            definitions = input.Items.GroupBy(item => item.Definition).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            containers = input.Items.GroupBy(item => item.Container).ToDictionary(group => group.Key, group => group.ToArray());
            equipment = input.Items.Where(item => item.EquippedSlot is not null).ToArray();
            knowledge = input.Knowledge.GroupBy(row => row.WorkId).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        }
        internal PersonalReadResponse[] Read(PersonalReadInput input)
        {
            ExecutionExperimentScenarios.Require(input.Fingerprint == fingerprint, "다른 개인 입력의 read index");
            return PersonalRequests.Select(request => Respond(input, request, request.Kind switch
            {
                "inventory" => definitions.GetValueOrDefault(request.Key) ?? [], "equipment" => equipment,
                "container" => containers.GetValueOrDefault(long.Parse(request.Key, System.Globalization.CultureInfo.InvariantCulture)) ?? [], _ => []
            }, knowledge.GetValueOrDefault(request.Key) ?? [])).ToArray();
        }
    }

    private sealed class PersonalReadCache
    {
        private readonly string fingerprint;
        private readonly ReadOnlyCollection<PersonalReadResponse> rows;
        internal PersonalReadCache(PersonalReadInput input)
        { fingerprint = input.Fingerprint; rows = Array.AsReadOnly(new PersonalReadIndex(input).Read(input)); }
        internal bool Accepts(PersonalReadInput input) => input.Fingerprint == fingerprint;
        internal PersonalReadResponse[] Read(PersonalReadInput input)
        { ExecutionExperimentScenarios.Require(Accepts(input), "낡은 개인권한/세대 불변 캐시"); return rows.ToArray(); }
    }

    private static ReadStorageOutput Output<T>(T value)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
        return new(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), bytes.Length);
    }
}
