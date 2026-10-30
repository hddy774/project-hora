using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Chronicles.Game;
using Chronicles.Game.Tests;
using Chronicles.Game.Cultivation;

var results = new List<object>();
var failed = 0;
var filterPosition = Array.IndexOf(args, "--filter");
var selectedScenario = filterPosition >= 0 && filterPosition + 1 < args.Length ? args[filterPosition + 1] : null;
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
void Test(string name, Action body)
{
    if (selectedScenario is not null && !name.Contains(selectedScenario, StringComparison.OrdinalIgnoreCase)) return;
    var sw = Stopwatch.StartNew();
    try { body(); results.Add(new { name, passed = true, ms = sw.ElapsedMilliseconds }); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failed++; results.Add(new { name, passed = false, error = e.ToString() }); Console.WriteLine($"FAIL {name}: {e}"); }
}
GameCommand Cmd(string kind, long actor = 0, long target = 0, long value = 0, string data = "") =>
    new(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), actor, kind, target, value, data);
void Accepted(GameWorld w, GameCommand c) { var r = w.Execute(c); Check(r.Accepted, r.Message); }
(GameWorld W, long A, long B) Fresh(Dictionary<string, long>? laws = null)
{
    var w = GameWorld.Create(42, laws);
    var s = w.Checkpoint(); return (w, s.People[0].Id, s.People[1].Id);
}
void RoundTrip(GameWorld w)
{
    var restored = GameCodec.Decode(GameCodec.Encode(w));
    Check(w.StateHash() == restored.StateHash(), "왕복 hash");
    w.AdvanceTo(w.Month + 12); restored.AdvanceTo(restored.Month + 12);
    Check(w.StateHash() == restored.StateHash(), "왕복 후 명령 없는 진행");
}

Test("INV-001/002/011 정확한 창세·안정 신원·100지점", () =>
{
    var w = GameWorld.Create(); var s = w.Checkpoint();
    Check(s.People.Count == 2 && s.People.All(p => p.Realm == Realm.HalfImmortal), "두 반선");
    Check(s.Groups.All(g => g.Kind != GroupKind.Sect), "초기 종문0");
    Check(s.Places.Count == 100 && s.Places.GroupBy(p => p.Province).All(g => g.Count() == 10), "10×10");
    Check(s.Items.All(i => i.Source == "창세 초기 자산" || i.Source == "창세 초기 지식 매체"), "초기 자산 출처");
    Check(s.People.All(p => p.Skills.Count == 0), "반선 기예 최대 복제 없음");
    Invariants.Validate(s);
});
Test("INV-003 사용자 세 정책과 0 비활성", () =>
{
    var (w, a, b) = Fresh(); var s = w.Checkpoint();
    Check(s.Laws["birth.kinship"] == 1 && s.Laws["birth.bothParents"] == 1 && s.Laws["birth.minimumRealm"] == 3, "정책 기본값");
    Accepted(w, Cmd("law", data: "birth.enabled", value: 0));
    var before = w.StateHash(); Check(!w.Execute(Cmd("conceive", b, a)).Accepted, "출산 비활성"); Check(w.StateHash() == before, "거절 원자성");
});
Test("INV-004/005/011/012 실제 출생·유전·단일 영혼·재전송", () =>
{
    var (w, a, b) = Fresh(); var command = Cmd("conceive", b, a);
    Accepted(w, command); var hash = w.StateHash();
    Accepted(w, command); Check(w.StateHash() == hash, "중복 명령 무영향");
    Check(!w.Execute(command with { Value = 7 }).Accepted && w.StateHash() == hash, "충돌 ID 거부");
    var pregnancy = w.Checkpoint().Pregnancies.Single();
    Check(w.Checkpoint().Souls.Count == 2, "출생 전 혼 미배정");
    w = GameCodec.Decode(GameCodec.Encode(w)); w.AdvanceTo(9);
    var s = w.Checkpoint(); var child = s.People.Single(p => p.ParentOne != 0);
    Check(s.BirthCount == 1 && s.People.Count == 3 && s.Souls.Count == 3, "단일 결과");
    Check(child.Realm == Realm.Mortal && child.Formations.Count == 0 && child.Skills.Count == 0, "경지·숙련 비복제");
    Check(child.Roots.SequenceEqual(pregnancy.Roots) && child.Aptitudes.Take(5).SequenceEqual(pregnancy.Aptitudes.Take(5)) && pregnancy.Aptitudes[5] == 0 &&
        child.Aptitudes[5] == s.PeopleDomain.Profiles.Single(profile => profile.PersonId == child.Id).Innate.Foundations[5], "유전 재추첨0");
    RoundTrip(w);
});
Test("INV-003 기존 임신 법칙 revision 보존", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("conceive", b, a));
    var pregnancy = w.Checkpoint().Pregnancies.Single();
    Accepted(w, Cmd("law", value: 120, data: "birth.gestation"));
    Accepted(w, Cmd("law", value: 0, data: "birth.enabled"));
    w.AdvanceTo(9); Check(w.Checkpoint().BirthCount == 1, "기존 임신 완료");
    Check(w.Checkpoint().Pregnancies.Single().LawRevision == pregnancy.LawRevision, "기존 revision");
});
Test("INV-011 양친 금단 바로 아래·같음", () =>
{
    var (w, a, b) = Fresh(); var s = w.Checkpoint(); WorldScenarioFixtures.SetCultivation(s, s.People[0], Realm.Foundation);
    w = GameWorld.Restore(s); var before = w.StateHash();
    Check(!w.Execute(Cmd("conceive", b, a)).Accepted && before == w.StateHash(), "한쪽 축기 거절");
    s = w.Checkpoint(); WorldScenarioFixtures.SetCultivation(s, s.People[0], Realm.GoldenCore); w = GameWorld.Restore(s);
    Accepted(w, Cmd("conceive", b, a));
});
Test("INV-012 혈연 허용·제한 프리셋", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("conceive", b, a)); w.AdvanceTo(9);
    var s = w.Checkpoint(); var child = s.People[2]; child.Born = -240;
    WorldScenarioFixtures.SetCultivation(s, child, Realm.GoldenCore);
    var partner = s.People.Single(person => person.ParentOne == 0 && person.Gestates != child.Gestates);
    s.People[0].Partner = 0; s.People[1].Partner = 0; child.Partner = partner.Id;
    partner.Partner = child.Id; child.NextBirth = 0; partner.NextBirth = 0;
    w = GameWorld.Restore(s); Accepted(w, Cmd("conceive", child.Id, partner.Id));
    s = w.Checkpoint(); s.Pregnancies.RemoveAt(1); s.Laws["birth.kinship"] = 0;
    w = GameWorld.Restore(s); Check(!w.Execute(Cmd("conceive", child.Id, partner.Id)).Accepted, "제한 프리셋 혈연 거부");
});
Test("INV-006/007/004 시간 분할·조회/저장 무영향", () =>
{
    var a = GameWorld.Create(997); var b = GameWorld.Create(997);
    a.AdvanceTo(240);
    for (var month = 1; month <= 240; month++)
    {
        var before = b.StateHash(); b.Checkpoint(); Check(before == b.StateHash(), "조회 순수");
        b.AdvanceTo(month); if (month % 13 == 0) b = GameCodec.Decode(GameCodec.Encode(b));
    }
    Check(a.StateHash() == b.StateHash(), "분할/복원 결정론");
});
Test("INV-004 손상 저장·중단 파일·백업 복원", () =>
{
    var path = Path.Combine(Path.GetTempPath(), "seonro-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), "world.save");
    try
    {
        var w = GameWorld.Create(); GameSaveStore.SaveAtomic(w, path); var hash = w.StateHash();
        w.AdvanceTo(1); GameSaveStore.SaveAtomic(w, path);
        File.WriteAllText(path, "손상"); File.WriteAllText(path + ".pending", "중단");
        Check(GameSaveStore.Load(path).StateHash() == hash, "유효 백업 복원");
        var bytes = GameCodec.Encode(w); bytes[bytes.Length / 2] ^= 1;
        var rejected = false; try { GameCodec.Decode(bytes); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "손상 거절");
    }
    finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
});
Test("INV-004/012 불법 부모·신원 중복 저장 거부", () =>
{
    var s = GameWorld.Create().Checkpoint(); s.People[0].ParentOne = s.People[1].Id;
    var rejected = false; try { GameWorld.Restore(s); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "잘못된 계보 거절");
    s = GameWorld.Create().Checkpoint(); s.People[1].BodyId = s.People[0].BodyId;
    rejected = false; try { GameWorld.Restore(s); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "중복 신원 거절");
});
Test("INV-016/017/018 실물 출품·거래·영석 보존", () =>
{
    var (w, a, b) = Fresh(); var item = w.Checkpoint().Items.First(i => i.Owner == a && i.Definition == "tool");
    Accepted(w, Cmd("offer", a, item.Id, 10)); var contract = w.Checkpoint().Contracts.Single();
    var before = w.StateHash(); Check(!w.Execute(Cmd("equip", a, item.Id)).Accepted && before == w.StateHash(), "예약 장착 불가");
    Accepted(w, Cmd("trade", b, contract.Id)); var s = w.Checkpoint();
    Check(s.Items.Single(i => i.Id == item.Id).Owner == b, "실물 이전");
    Check(s.People.Sum(p => p.Money) == s.CreatedMoney, "영석 보존");
    RoundTrip(w);
});
Test("INV-017 경매 예치·낙찰 단일 정산", () =>
{
    var (w, a, b) = Fresh(new() { ["birth.enabled"] = 0 });
    var item = w.Checkpoint().Items.First(i => i.Owner == a && i.Definition == "tool");
    Accepted(w, Cmd("offer", a, item.Id, 10, "auction")); var contract = w.Checkpoint().Contracts.Single();
    Accepted(w, Cmd("bid", b, contract.Id, 20)); var s = w.Checkpoint(); Check(s.Contracts.Single().Escrow == 20, "입찰 예치");
    w = GameCodec.Decode(GameCodec.Encode(w)); w.AdvanceTo(12);
    s = w.Checkpoint(); var settled = s.Contracts.Single(c => c.Id == contract.Id);
    Check(settled.Status == "completed" && settled.Escrow == 0, "낙찰 종결");
    Check(s.Items.Single(i => i.Id == item.Id).Owner == b, "낙찰 실물"); Invariants.Validate(s);
});
Test("INV-017 제작 예약·취소·소비·저장", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("possess", a));
    void FinishAction() => w.AdvanceTo(w.Checkpoint().People.Single(p => p.Id == a).Due);
    Accepted(w, Cmd("gather", a, data: "herb")); FinishAction();
    Accepted(w, Cmd("gather", a, data: "wood")); FinishAction();
    var beforeInputs = w.Checkpoint().Materials["herb"].Consumption;
    Accepted(w, Cmd("craft", a, data: "pill"));
    Check(w.Checkpoint().Inventory.Jobs.Any(job => job.Actor == a && job.Status == "processing") &&
        w.Checkpoint().Materials["herb"].Consumption > beforeInputs, "예약 후 실제 공정 개시에서 재료 소비");
    w = GameCodec.Decode(GameCodec.Encode(w)); Accepted(w, Cmd("cancel", a));
    Check(w.Checkpoint().Items.All(i => i.Reserved == 0) &&
        w.Checkpoint().Materials["herb"].Consumption > beforeInputs, "미사용 예약 회수·이미 소비한 재료 유지");
    for (var attempt = 0; attempt < 3 && !w.Checkpoint().Items.Any(i => i.Owner == a && i.Definition == "pill"); attempt++)
    { Accepted(w, Cmd("craft", a, data: "pill")); FinishAction(); }
    var pill = w.Checkpoint().Items.Single(i => i.Owner == a && i.Definition == "pill"); Accepted(w, Cmd("use", a, pill.Id));
    Check(w.Checkpoint().Items.All(i => i.Id != pill.Id), "실제 복용 소비");
});
Test("INV-020 조직 금고·정책 예약·해산 역사", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("possess", a)); Accepted(w, Cmd("found", a, data: "sect"));
    w.AdvanceTo(1); var g = w.Checkpoint().Groups.Single(g => g.Kind == GroupKind.Sect && g.Leader == a);
    Accepted(w, Cmd("policy", a, g.Id, 2, Catalog.PolicyNames[0]));
    Check(w.Checkpoint().Politics.Preparations.Any(preparation => preparation.Group == g.Id && preparation.Escrow > 0), "정책 예산 예약");
    w = GameCodec.Decode(GameCodec.Encode(w)); w.AdvanceTo(2);
    Check(w.Checkpoint().Groups.Single(x => x.Id == g.Id).Policies[Catalog.PolicyNames[0]] == 2, "정책 시행");
    Accepted(w, Cmd("dissolve", a, g.Id)); Invariants.Validate(w.Checkpoint());
    Check(w.Checkpoint().Groups.Single(x => x.Id == g.Id).Dissolved, "역사 보존");
});
Test("INV-013/014 화신 대원만 경계·실제 사망·대기·출생", () =>
{
    foreach (var layer in Enumerable.Range(8, 2))
    {
        var (w, a, b) = Fresh(new() { ["soul.crossing"] = 0, ["birth.enabled"] = 0 });
        var s = w.Checkpoint(); var p = s.People[0]; WorldScenarioFixtures.SetCultivation(s, p, Realm.Spirit, layer, layer == 9); p.Lifespan = 1201;
        w = GameWorld.Restore(s); w.AdvanceTo(1); s = w.Checkpoint();
        Check(s.People[0].Status == LifeStatus.Dead && s.DeathCount == 1, "实际 수명 종료");
        Check(s.Souls.Single(x => x.Id == p.SoulId).Status == (layer == 9 ? "waiting" : "ended"), "최소 경지 완성 경계");
        RoundTrip(w);
    }
});
Test("INV-023 원영 파단 후 취소 사망·앱 복원은 포기 아님", () =>
{
    var (w, a, b) = Fresh(); var s = w.Checkpoint(); var p = s.People[0];
    WorldScenarioFixtures.SetCultivation(s, p, Realm.GoldenCore, 9, true);
    var metadata = s.Cultivation[p.BodyId];
    s.Cultivation[p.BodyId] = metadata with { Attempt = new CultivationAttempt("fixture-fracture", AttemptKind.RealmBreakthrough,
        AttemptPhase.Fractured, Realm.NascentSoul, "path.4.0", 0, s.LawRevision,
        new KnowledgeEvidence("path.4.0", "explicit-fixture", 1, 3000, 0, 0, true, false, "명시적 파단 직후 저장 경계"),
        [], 5000, 0, 1, 0, new(Realm.GoldenCore, 5000, 10000, 0, metadata.ActivePath))
        { TargetWork = WorldScenarioFixtures.Policy(s).RequiredWork(Realm.NascentSoul),
          TargetRootWork = metadata.GrowthBasis!.RootWork, LifespanAward = WorldScenarioFixtures.Policy(s).Lifespan(Realm.NascentSoul) } };
    p.Irreversible = true; p.Action = ActionKind.Breakthrough; p.Due = 1;
    w = GameWorld.Restore(s); Accepted(w, Cmd("possess", a));
    w = GameCodec.Decode(GameCodec.Encode(w)); Check(w.Checkpoint().People[0].Status == LifeStatus.Alive, "복원은 포기 아님");
    Accepted(w, Cmd("cancel", a)); s = w.Checkpoint();
    Check(s.DeathCount == 1 && s.People[0].Status == LifeStatus.Dead && s.Souls[0].Route == 0, "파단 후 실패·자동 윤회 없음");
});
Test("INV-010/021 빙의·AI 인계에서 기존 행동 보존", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("gather", a, data: "ore")); var due = w.Checkpoint().People[0].Due;
    Accepted(w, Cmd("possess", a)); Check(w.Checkpoint().People[0].Due == due, "빙의 작업 보존");
    var generation = w.ViewGeneration; Accepted(w, Cmd("observe")); Check(w.ViewGeneration == generation + 1, "표시 세대");
    Check(w.Checkpoint().People[0].Due == due, "AI 인계 작업 보존");
});
Test("30경로·24재능·40특성·12장비·정의 조회 순수성", () =>
{
    Check(Catalog.Paths.Count == 30 && Catalog.Paths.GroupBy(x => x.Realm).All(g => g.Count() == 3), "30경로");
    Check(Catalog.Talents.Count == 24 && Catalog.TraitNames.Count == 40 && Catalog.EquipmentSlots.Count == 12, "정의 수");
    var w = GameWorld.Create(); var hash = w.StateHash(); var p = w.Checkpoint().People[0];
    foreach (var i in Enumerable.Range(0, 24)) Check(Catalog.Talent(p, i) is >= 0 and <= 10000, "재능 범위");
    Check(w.StateHash() == hash, "정의 조회 무영향");
});

Test("INV-008/009/010 관찰→플레이·숨은 상태 비간섭·ID 우회", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("possess", a));
    var baseline = JsonSerializer.Serialize(w.View(b)); var s = w.Checkpoint();
    WorldScenarioFixtures.SetCultivation(s, s.People[1], Realm.Mortal); s.People[1].Health = 12; s.People[1].Roots[0] = 0;
    s.PeopleDomain.Profiles.Single(p => p.PersonId == b).Innate.ElementRoots[0] = 0;
    var changed = GameWorld.Restore(s);
    Check(baseline == JsonSerializer.Serialize(changed.View(b)), "동일 인지에서 숨은 변화 비간섭");
    Check(w.View().Laws.Count == 0 && w.View().Census.Count == 0 && w.View().Hash.Length == 0, "전역 정답 미노출");
    Check(w.View(b).Detail!.Aptitudes is null && w.View(b).Detail!.Combat is null, "타인 상세 미상");
    var before = w.StateHash(); w.View(a); w.View(b); w.View(99999999);
    Check(before == w.StateHash(), "모든 조회 순수");
    Check(w.View(99999999).Detail is null, "미발견 ID 미노출");
    Check(!w.Execute(Cmd("law", value: 0, data: "birth.enabled")).Accepted, "플레이 법칙 권한 우회 거부");
    var generation = w.ViewGeneration; Accepted(w, Cmd("observe"));
    Check(w.ViewGeneration > generation && w.View(b).Detail!.Aptitudes is not null, "조회 세대와 관찰 상세");
});

foreach (var route in Enumerable.Range(1, 13))
    Test($"RC-{route:00} 실제 원인 생산자·자동 Gate·여정 왕복", () => ReincarnationScenarios.Run(route));

Test("INV-008 인지 예산 변경·명제/인물/지점 합계", () =>
{
    var (w, a, b) = Fresh(); w.AdvanceTo(24);
    Accepted(w, Cmd("law", value: 1, data: "ai.beliefBudget"));
    Check(w.Checkpoint().People.All(p => p.Beliefs.Count + p.ObservedPeople.Count + p.ObservedPlaces.Count <= 1), "통합 예산");
    RoundTrip(w);
});
Test("INV-013 혼멸·역사 최고 경지로 자격 우회 없음", () =>
{
    var (w, a, b) = Fresh(); var s = w.Checkpoint(); s.Souls[0].Integrity = 0;
    s.People[0].Lifespan = 1201; w = GameWorld.Restore(s); w.AdvanceTo(1);
    Check(w.Checkpoint().Souls[0].Status == "destroyed" && w.Checkpoint().Souls[0].Route == 0, "혼멸 윤회 불가");
    (w,a,b) = Fresh(); s = w.Checkpoint(); WorldScenarioFixtures.SetCultivation(s, s.People[0], Realm.Mortal); s.People[0].Lifespan = 1201;
    w = GameWorld.Restore(s); w.AdvanceTo(1);
    Check(w.Checkpoint().Souls[0].Route == 0, "과거 반선 형성 기록은 현재 자격 아님");
});
Test("INV-018 물품 원장 없는 수량 복제 거부", () =>
{
    var s = GameWorld.Create().Checkpoint(); s.Items[0].Quantity++;
    var rejected = false; try { GameWorld.Restore(s); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "품목 원장과 실물 불일치 거절");
});
Test("INV-006 저장 왕복·호스트 문화권 독립", () =>
{
    var before = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var w = GameWorld.Create();
        foreach (var id in "I,i,İ,a-b,ab".Split(',')) Accepted(w, new(id, 0, "observe"));
        var hash = w.StateHash(); var bytes = GameCodec.Encode(w);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
        Check(GameCodec.Decode(bytes).StateHash() == hash, "문화권별 정렬과 명령 식별자");
    }
    finally { CultureInfo.CurrentCulture = before; }
});
Test("INV-001 슬롯 재사용·낡은 세대/사망 신원 거부", () =>
{
    var (w, a, b) = Fresh(); var s = w.Checkpoint(); var old = s.People[0];
    old.Lifespan = 1201; var slot = old.Slot; var generation = old.SlotGeneration;
    w = GameWorld.Restore(s); w.AdvanceTo(1);
    Accepted(w, Cmd("create", target: 0, data: "명시 편입"));
    var fresh = w.Checkpoint().People[^1];
    Check(fresh.Slot == slot && fresh.SlotGeneration != generation, "실제 슬롯 재사용");
    Check(w.ResolveSlot(slot, generation) == 0 && w.ResolveSlot(slot, fresh.SlotGeneration) == fresh.Id, "세대별 조회");
    Check(!w.Execute(Cmd("possess", a)).Accepted, "사망 신원으로 조작 불가");
    RoundTrip(w);
});
Test("INV-015/024 폐품 점수와 기능 결손 분리", () =>
{
    var (w, a, b) = Fresh(); var s = w.Checkpoint();
    var tool = s.Items.First(i => i.Owner == a && i.Definition == "tool"); tool.Quality = 0;
    var book = s.Items.First(i => i.Owner == a && i.Definition == "path.1.0"); book.Quality = 0;
    w = GameWorld.Restore(s); Accepted(w, Cmd("possess", a)); Accepted(w, Cmd("equip", a, tool.Id));
    var knowledge = w.Checkpoint().People[0].Understanding[book.Definition];
    Accepted(w, Cmd("study", a, book.Id)); w.AdvanceTo(1);
    Check(w.Checkpoint().People[0].Understanding[book.Definition] > knowledge, "결손 없는 폐품 서적 실제 학습");
    s = w.Checkpoint(); s.Items.Single(i => i.Id == book.Id).Defects = ItemDefect.LostCore;
    w = GameWorld.Restore(s); knowledge = w.Checkpoint().People[0].Understanding[book.Definition];
    var damaged = w.StateHash();
    Check(!w.Execute(Cmd("study", a, book.Id)).Accepted && w.StateHash() == damaged,
        "읽을 수 없는 실제 결손 매체는 시작 전에 원자 거부");
    Check(w.Checkpoint().People[0].Understanding[book.Definition] == knowledge, "실제 결손은 학습 차단");
});
Test("INV-008 장소 ID와 인물 ID 충돌에서 사건 누출 없음", () =>
{
    var (w, a, b) = Fresh(); Accepted(w, Cmd("possess", a));
    var before = JsonSerializer.Serialize(w.View()); var s = w.Checkpoint();
    s.Events.Add(new(s.NextId++, s.Month, "재해", 0, a, "미발견 지점의 숨은 재해", "세계 사건", "place"));
    w = GameWorld.Restore(s);
    Check(JsonSerializer.Serialize(w.View()) == before, "인물/지점 ID 네임스페이스 분리");
});

foreach (var feature in FeatureScenarioCatalog.Discover()) Test(feature.Name, feature.Run);

if (args.Contains("--report", StringComparer.Ordinal))
{
    var index = Array.IndexOf(args, "--report");
    File.WriteAllText(args[index + 1], JsonSerializer.Serialize(new { selection = selectedScenario ?? "all", total = results.Count, failed, results }, new JsonSerializerOptions { WriteIndented = true }));
}
if (results.Count == 0) throw new ArgumentException("일치하는 기능 시나리오가 없습니다.");
Console.WriteLine($"RESULT {results.Count - failed}/{results.Count}");
if (failed == 0 && args.Contains("--performance", StringComparer.Ordinal))
    PerformanceScenarios.Run(args[Array.IndexOf(args, "--performance") + 1]);
if (failed == 0 && args.Contains("--growth", StringComparer.Ordinal))
    GrowthScenarios.Run(args[Array.IndexOf(args, "--growth") + 1]);
if (failed == 0 && args.Contains("--soak", StringComparer.Ordinal))
    PerformanceScenarios.Soak(args[Array.IndexOf(args, "--soak") + 1]);
return failed == 0 ? 0 : 1;
