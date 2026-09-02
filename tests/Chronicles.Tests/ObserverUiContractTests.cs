using Chronicles.Application;
using Chronicles.Core;
using Chronicles.Queries;
using Chronicles.Persistence;
using Chronicles.Simulation;
using System.Text.Json.Nodes;

internal static class ObserverUiContractTests
{
    private static readonly int[] NormalizedWatchlist = [8, 37];
    private static readonly int[] FirstWatchlistPage = [3, 12];
    private static readonly int[] SecondWatchlistPage = [63];
    private static readonly int[] SearchedWatchlistPage = [12];

    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "chronicles-observer-ui-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            VerifyPreferenceRoundTrip(root);
            VerifyWatchlistProjection();
            VerifyAdaptiveScalePolicy();
            VerifyResponsiveCardLayoutPolicy();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyPreferenceRoundTrip(string root)
    {
        var worldPath = Path.Combine(root, "world.db");
        var worldBytes = new byte[] { 0x43, 0x4d, 0x49, 0x50, 0x19 };
        File.WriteAllBytes(worldPath, worldBytes);
        var preferencePath = Path.Combine(root, "ui", "observer-ui.json");
        var preferences = new ObserverUiPreferences(
            ObserverUiPreferences.CurrentSchemaVersion,
            "People",
            1.4d,
            37,
            "Watchlist",
            500,
            "A00038",
            "Cultivation",
            123.5d,
            true,
            "slot-04",
            1,
            new[] { 37, 8, 37, -1 },
            "FullScreen");

        ObserverUiPreferencesStore.Save(preferences, preferencePath);
        var loaded = ObserverUiPreferencesStore.Load(preferencePath);
        Equal(ObserverUiPreferencesLoadStatus.Loaded, loaded.Status, "UI 상태 로드");
        Equal("People", loaded.Preferences.ActiveWorkspace, "작업공간 복원");
        Equal(1.4d, loaded.Preferences.UiScale, "배율 복원");
        Equal(37, loaded.Preferences.SelectedAgentId, "선택 인물 복원");
        Equal("Watchlist", loaded.Preferences.PeopleListMode, "관심 목록 모드 복원");
        Equal("Cultivation", loaded.Preferences.AgentDetailSection, "인물 내부 탭 복원");
        Equal(123.5d, loaded.Preferences.AgentDetailScrollOffset, "인물 스크롤 복원");
        Equal(true, loaded.Preferences.CompactPersonShowsDetail, "좁은 화면 인물 내부 탭 복원");
        Equal("slot-04", loaded.Preferences.SelectedSaveSlotId, "선택 세이브 슬롯 복원");
        Equal("FullScreen", loaded.Preferences.WindowDisplayMode, "전체화면 모드 복원");
        SequenceEqual(NormalizedWatchlist, loaded.Preferences.WatchlistedAgentIds, "관심 ID 정규화");
        True(!File.Exists(preferencePath + ".tmp"), "원자 임시 파일 정리");
        SequenceEqual(worldBytes, File.ReadAllBytes(worldPath), "세계 DB 비변경");

        var legacyJson = JsonNode.Parse(File.ReadAllText(preferencePath))?.AsObject()
            ?? throw new InvalidOperationException("관찰 UI JSON 파싱 실패");
        legacyJson.Remove(nameof(ObserverUiPreferences.WindowDisplayMode));
        File.WriteAllText(preferencePath, legacyJson.ToJsonString());
        var legacy = ObserverUiPreferencesStore.Load(preferencePath);
        Equal(ObserverUiPreferencesLoadStatus.Loaded, legacy.Status, "기존 UI 상태 호환");
        Equal("Windowed", legacy.Preferences.WindowDisplayMode, "기존 상태 창 모드 기본값");

        File.WriteAllText(preferencePath, "{ invalid json");
        var invalid = ObserverUiPreferencesStore.Load(preferencePath);
        Equal(ObserverUiPreferencesLoadStatus.Invalid, invalid.Status, "손상 UI 상태 기본값 복구");
        Equal(ObserverUiPreferences.Default, invalid.Preferences, "손상 UI 기본값");

        File.WriteAllText(preferencePath, "{\"SchemaVersion\":999}");
        var incompatible = ObserverUiPreferencesStore.Load(preferencePath);
        Equal(
            ObserverUiPreferencesLoadStatus.Incompatible,
            incompatible.Status,
            "미지원 UI schema 기본값 복구");
        SequenceEqual(worldBytes, File.ReadAllBytes(worldPath), "오류 UI 로드 중 세계 DB 비변경");
    }

    private static void VerifyWatchlistProjection()
    {
        var world = World.Create(0x0023_0B5E_0001UL, 64);
        var queries = new WorldObservationQueries(world);
        var hashBefore = world.ComputeStateHash();
        var ids = new[]
        {
            new AgentId(12),
            new AgentId(3),
            new AgentId(12),
            new AgentId(63),
            new AgentId(1000),
        };
        var firstPage = queries.BrowseAgentsById(ids, null, offset: 0, limit: 2);
        Equal(3, firstPage.TotalCount, "유효 관심 인물 수");
        SequenceEqual(FirstWatchlistPage, firstPage.Agents.Select(item => item.Id.Value), "안정 ID 첫 페이지");
        var secondPage = queries.BrowseAgentsById(ids, null, offset: 2, limit: 2);
        SequenceEqual(SecondWatchlistPage, secondPage.Agents.Select(item => item.Id.Value), "안정 ID 둘째 페이지");
        var exactSearch = queries.BrowseAgentsById(ids, new AgentId(12).ToString(), 0, 10);
        SequenceEqual(SearchedWatchlistPage, exactSearch.Agents.Select(item => item.Id.Value), "관심 ID 검색");
        Equal(hashBefore, world.ComputeStateHash(), "관심 목록 관찰 무영향");

        world.SetDetailedChronicleProtection(new AgentId(12), isProtected: true);
        Equal(hashBefore, world.ComputeStateHash(), "관심 기록 보존 정책은 게임 상태 hash에 무영향");
        var checkpoint = world.CreateCheckpoint();
        True(checkpoint.Agents[12].PreserveDetailedChronicle,
            "관심 인물 세부 연대기 보존 상태 checkpoint");
        var restored = World.Restore(
            WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
        SequenceEqual(
            [12],
            restored.GetDetailedChronicleProtectionList().Select(item => item.Value),
            "관심 인물 보존 상태 저장 왕복");
        Equal(hashBefore, restored.ComputeStateHash(), "관심 보존 저장 왕복 hash 무영향");
        restored.SetDetailedChronicleProtection(new AgentId(12), isProtected: false);
        Equal(hashBefore, restored.ComputeStateHash(), "관심 해제 hash 무영향");
    }

    private static void VerifyAdaptiveScalePolicy()
    {
        Equal(1.4d, AdaptiveUiScalePolicy.CalculateEffectiveScale(1.4d, 1920d, 1040d),
            "1920×1080 작업 영역 140% 유지");
        Equal(0.975d, AdaptiveUiScalePolicy.CalculateEffectiveScale(1.4d, 1280d, 780d),
            "좁은 화면 자동 안전 배율");
        Equal(0.9d, AdaptiveUiScalePolicy.CalculateEffectiveScale(0.9d, 1280d, 780d),
            "요청 배율보다 확대하지 않음");
        Equal(1.25d, AdaptiveUiScalePolicy.CalculateEffectiveScale(1.25d, 0d, 0d),
            "레이아웃 전 요청 배율 유지");
    }

    private static void VerifyResponsiveCardLayoutPolicy()
    {
        Equal(6, ResponsiveCardLayoutPolicy.CalculateColumns(1_000d, 150d, 8d, 6, 6),
            "넓은 화면 카드 6열");
        Equal(3, ResponsiveCardLayoutPolicy.CalculateColumns(520d, 150d, 8d, 6, 6),
            "좁은 화면 카드 줄바꿈");
        Equal(1, ResponsiveCardLayoutPolicy.CalculateColumns(120d, 150d, 8d, 6, 6),
            "최소 폭 미만 단일 열");
        Equal(0, ResponsiveCardLayoutPolicy.CalculateColumns(520d, 150d, 8d, 0, 6),
            "빈 카드 집합");
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
        }
    }

    private static void SequenceEqual<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string label)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"{label}: sequence mismatch");
        }
    }

    private static void True(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException(label);
        }
    }
}
