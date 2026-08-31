using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void UpdateWorldRecordsPanel(WorldSnapshot snapshot)
    {
        switch (WorldRecordsPanel.SelectedRecordIndex)
        {
            case 0:
                var worldLogCategory = WorldRecordsPanel.SelectedWorldLogCategory;
                var recentSequence = snapshot.RecentEvents.Count > 0
                    ? snapshot.RecentEvents[^1].Sequence
                    : 0;
                if (_projectedRecentEventSequence == recentSequence
                    && _projectedRecentEventCount == snapshot.RecentEvents.Count
                    && _projectedWorldLogCategory == worldLogCategory)
                {
                    break;
                }

                WorldRecordsPanel.ApplyRecent(snapshot.RecentEvents
                    .Where(item => MatchesWorldLogCategory(item.Kind, worldLogCategory))
                    .Reverse()
                    .Select(item => FormatEvent(snapshot, item))
                    .ToArray(), snapshot.RecentEvents.Count);
                _projectedRecentEventSequence = recentSequence;
                _projectedRecentEventCount = snapshot.RecentEvents.Count;
                _projectedWorldLogCategory = worldLogCategory;
                break;
            case 1:
                var worldChronicleCategory = WorldRecordsPanel.SelectedWorldChronicleCategory;
                var chronicleSequence = snapshot.PermanentWorldChronicle.Count > 0
                    ? snapshot.PermanentWorldChronicle[^1].Sequence
                    : 0;
                if (_projectedWorldChronicleSequence == chronicleSequence
                    && _projectedWorldChronicleCount == snapshot.PermanentWorldChronicleCount
                    && _projectedWorldChronicleCategory == worldChronicleCategory)
                {
                    break;
                }

                WorldRecordsPanel.ApplyChronicle(snapshot.PermanentWorldChronicle
                    .Reverse()
                    .Select(item => FormatWorldChronicle(item, worldChronicleCategory))
                    .ToArray(), snapshot.PermanentWorldChronicleCount);
                _projectedWorldChronicleSequence = chronicleSequence;
                _projectedWorldChronicleCount = snapshot.PermanentWorldChronicleCount;
                _projectedWorldChronicleCategory = worldChronicleCategory;
                break;
            case 2:
                var debugVersion = _logService.DebugVersion;
                if (_projectedDebugVersion == debugVersion)
                {
                    break;
                }

                WorldRecordsPanel.ApplyDebug(_logService.GetDebugEntries()
                    .Reverse()
                    .Select(FormatDebugEntry)
                    .ToArray());
                _projectedDebugVersion = debugVersion;
                break;
        }
    }

    private EventRow FormatEvent(WorldSnapshot snapshot, WorldEvent worldEvent)
    {
        var primary = worldEvent.PrimaryName ?? GetAgentName(snapshot, worldEvent.PrimaryAgent);
        var secondary = worldEvent.SecondaryName
            ?? (worldEvent.SecondaryAgent is { } secondaryAgent
                ? GetAgentName(snapshot, secondaryAgent)
                : string.Empty);
        var summary = FormatWorldEventSummary(worldEvent, primary, secondary);
        var color = worldEvent.Kind switch
        {
            WorldEventKind.NewLeader
                or WorldEventKind.BreakthroughSucceeded
                or WorldEventKind.MinorStageBreakthrough
                or WorldEventKind.BottleneckBreakthrough
                or WorldEventKind.MajorRealmBreakthrough
                or WorldEventKind.FloorNineCompletion
                or WorldEventKind.ReincarnationCrossingCompleted
                or WorldEventKind.Rebirth
                or WorldEventKind.TreasureCrafted
                or WorldEventKind.PillCrafted
                or WorldEventKind.TechniqueBookPurchased
                or WorldEventKind.SpellBookPurchased => (Brush)FindResource("AccentBrush"),
            WorldEventKind.CombatEnded or WorldEventKind.MonsterHuntWon => (Brush)FindResource("PositiveBrush"),
            WorldEventKind.BreakthroughFailed
                or WorldEventKind.CombatRetreated
                or WorldEventKind.CombatInjury
                or WorldEventKind.CombatDeath
                or WorldEventKind.MonsterHuntLost => (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow(FormatGameDate(GameCalendar.GetDate(worldEvent.Tick)), summary, color);
    }

    private EventRow FormatWorldChronicle(
        WorldCenturyChronicleEntry entry,
        WorldChronicleCategory category)
    {
        var summary = category switch
        {
            WorldChronicleCategory.History => FormatWorldHistorySummary(entry),
            WorldChronicleCategory.Cultivation => FormatWorldCultivationHistory(entry),
            WorldChronicleCategory.Ranking => FormatWorldRankingHistory(entry),
            WorldChronicleCategory.LifeAndReincarnation => FormatWorldLifeHistory(entry),
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
        return new EventRow(
            $"제 {entry.CenturyNumber:N0}권 · 세계력 {entry.StartYear:N0}~{entry.EndYear:N0}년",
            summary,
            (Brush)FindResource("AccentBrush"));
    }

    private static bool MatchesWorldLogCategory(
        WorldEventKind kind,
        WorldLogCategory category) => category switch
    {
        WorldLogCategory.All => true,
        WorldLogCategory.Competition => kind is WorldEventKind.NewLeader,
        WorldLogCategory.Combat => kind is
            WorldEventKind.CombatStarted
            or WorldEventKind.CombatPhysicalAttack
            or WorldEventKind.CombatSpellCast
            or WorldEventKind.CombatGuarded
            or WorldEventKind.CombatRetreated
            or WorldEventKind.CombatEnded
            or WorldEventKind.CombatInjury
            or WorldEventKind.CombatDeath
            or WorldEventKind.MonsterHuntWon
            or WorldEventKind.MonsterHuntLost,
        WorldLogCategory.Cultivation => kind is
            WorldEventKind.BreakthroughSucceeded
            or WorldEventKind.BreakthroughFailed
            or WorldEventKind.MinorStageBreakthrough
            or WorldEventKind.BottleneckBreakthrough
            or WorldEventKind.MajorRealmBreakthrough
            or WorldEventKind.FloorNineCompletion
            or WorldEventKind.CultivationEntered
            or WorldEventKind.GreatPerfectionAchieved
            or WorldEventKind.TechniqueEnlightenment
            or WorldEventKind.SpellEnlightenment
            or WorldEventKind.TechniqueAcquired
            or WorldEventKind.SpellAcquired,
        WorldLogCategory.Economy => kind is
            WorldEventKind.MissionCompleted
            or WorldEventKind.TreasureCrafted
            or WorldEventKind.PillCrafted
            or WorldEventKind.PillPurchased
            or WorldEventKind.MaterialSold
            or WorldEventKind.MaterialPurchased
            or WorldEventKind.TreasurePurchased
            or WorldEventKind.TechniqueBookPurchased
            or WorldEventKind.SpellBookPurchased
            or WorldEventKind.SpiritStoneMined,
        WorldLogCategory.Reincarnation => kind is
            WorldEventKind.Death
            or WorldEventKind.CombatDeath
            or WorldEventKind.ReincarnationEntered
            or WorldEventKind.ReincarnationCrossingCompleted
            or WorldEventKind.Rebirth,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    private static string FormatWorldHistorySummary(WorldCenturyChronicleEntry entry)
    {
        var cultivation = FormatWorldCultivationHistory(entry);
        var ranking = FormatWorldRankingHistory(entry);
        var life = FormatWorldLifeHistory(entry);
        return $"이 백 년의 수행사: {cultivation}  경쟁사: {ranking}  생사록: {life}  " +
            $"세간에서는 전투 {KoreanNumberFormatter.Format(entry.CombatsCompleted)}회, " +
            $"요수 사냥 {KoreanNumberFormatter.Format(entry.MonsterHunts)}회, " +
            $"도시 임무 {KoreanNumberFormatter.Format(entry.MissionsCompleted)}회가 이루어졌다.";
    }

    private static string FormatWorldCultivationHistory(WorldCenturyChronicleEntry entry)
    {
        var realms = new List<string>(RealmRules.All.Count);
        for (var realm = 0; realm < RealmRules.All.Count; realm++)
        {
            var count = entry.RealmBreakthroughCounts[realm];
            if (count == 0)
            {
                continue;
            }

            realms.Add(
                $"{FormatRealmStage((RealmStage)realm)} " +
                $"{KoreanNumberFormatter.Format(entry.RealmBreakthroughUniqueCounts[realm])}명·" +
                $"{KoreanNumberFormatter.Format(count)}회");
        }

        return realms.Count == 0
            ? "대경지 돌파 기록이 없었다."
            : $"{string.Join(", ", realms)}의 대경지 돌파가 기록되었다.";
    }

    private static string FormatWorldRankingHistory(WorldCenturyChronicleEntry entry)
    {
        static string FormatChanges(long[] values) =>
            $"TOP 1 {KoreanNumberFormatter.Format(values[0])}회 · " +
            $"TOP 3 {KoreanNumberFormatter.Format(values[1])}회 · " +
            $"TOP 5 {KoreanNumberFormatter.Format(values[2])}회 · " +
            $"TOP 10 {KoreanNumberFormatter.Format(values[3])}회";

        return $"전투력 [{FormatChanges(entry.CombatPowerPositionChanges)}], " +
            $"경지 [{FormatChanges(entry.RealmPositionChanges)}], " +
            $"자질 [{FormatChanges(entry.AptitudePositionChanges)}], " +
            $"부자 [{FormatChanges(entry.WealthPositionChanges)}]. " +
            $"세기 말 1위는 전투력 {entry.CombatPowerLeader.Name}, " +
            $"경지 {entry.RealmLeader.Name}, 자질 {entry.AptitudeLeader.Name}, " +
            $"부자 {entry.WealthLeader.Name}였다.";
    }

    private static string FormatWorldLifeHistory(WorldCenturyChronicleEntry entry) =>
        $"{KoreanNumberFormatter.Format(entry.UniqueDeathCount)}명이 " +
        $"{KoreanNumberFormatter.Format(entry.DeathCount)}회 죽었고, " +
        $"{KoreanNumberFormatter.Format(entry.UniqueReincarnationEntryCount)}명이 " +
        $"{KoreanNumberFormatter.Format(entry.ReincarnationEntryCount)}회 윤회의 강에 들었다. " +
        $"{KoreanNumberFormatter.Format(entry.UniqueRebirthCount)}명이 " +
        $"{KoreanNumberFormatter.Format(entry.RebirthCount)}회 환생했으며, " +
        $"세기 말 강에 남은 영혼은 " +
        $"{KoreanNumberFormatter.Format(entry.ReincarnationRiverResidentsAtEnd)}명이었다.";

    private static string FormatWorldEventSummary(
        WorldEvent worldEvent,
        string primary,
        string secondary) => worldEvent.Kind switch
    {
        WorldEventKind.BreakthroughSucceeded => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 돌파 성공",
        WorldEventKind.BreakthroughFailed => $"{primary}, 돌파 시도 실패 · 재준비",
        WorldEventKind.MinorStageBreakthrough => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 소경지 돌파",
        WorldEventKind.BottleneckBreakthrough => $"{primary}, {worldEvent.Value:N0}번의 시도 끝에 병목 돌파",
        WorldEventKind.MajorRealmBreakthrough => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 대경지 돌파",
        WorldEventKind.FloorNineCompletion => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 소경지 완성",
        WorldEventKind.TechniqueEnlightenment => $"{primary}, 공법 깨달음 · 이해도 {worldEvent.Value / 100m:0.00}",
        WorldEventKind.SpellEnlightenment => $"{primary}, 법술 깨달음 · 이해도 {worldEvent.Value / 100m:0.00}",
        WorldEventKind.CultivationEntered => $"{primary}, 입문 3 Gate를 통과해 연기경에 입문",
        WorldEventKind.GreatPerfectionAchieved => $"{primary}, {FormatEncodedRealm(worldEvent.Value, greatPerfection: true)} 완성",
        WorldEventKind.Death => $"{primary}, {worldEvent.Value:N0}세에 수명을 다함",
        WorldEventKind.ReincarnationEntered => $"{primary}, 윤회의 강에 들어감",
        WorldEventKind.ReincarnationCrossingCompleted =>
            $"{primary}, 윤회의 강 도하 완료 · 새 생 준비",
        WorldEventKind.Rebirth => $"{primary}, 제 {worldEvent.Value:N0}생으로 환생",
        WorldEventKind.MissionCompleted => $"{primary}, 도시 임무 완료 · 영석 +{KoreanNumberFormatter.Format(worldEvent.Value)}",
        WorldEventKind.TreasureCrafted => $"{primary}, {FormatQuality(worldEvent.Value)} 법보 제작",
        WorldEventKind.PillCrafted => $"{primary}, {FormatPill((PillKind)worldEvent.Value)} 제작",
        WorldEventKind.PillPurchased => $"{primary}, 영석으로 {FormatPill((PillKind)worldEvent.Value)} 구매",
        WorldEventKind.MaterialSold => $"{primary}, {EconomyRules.GetMaterial((MaterialId)worldEvent.Value).KoreanName} 판매",
        WorldEventKind.MaterialPurchased => $"{primary}, 시장에서 {EconomyRules.GetMaterial((MaterialId)worldEvent.Value).KoreanName} 구매",
        WorldEventKind.TreasurePurchased => $"{primary}, 시장에서 {FormatQuality(worldEvent.Value)} 법보 구매",
        WorldEventKind.TechniqueBookPurchased => $"{primary}, 시장에서 {FormatQuality(worldEvent.Value)} 공법서 구매",
        WorldEventKind.SpellBookPurchased => $"{primary}, 시장에서 {FormatQuality(worldEvent.Value)} 법술서 구매",
        WorldEventKind.NewLeader => $"{primary}, {secondary}을 제치고 새로운 1위 등극",
        WorldEventKind.CombatStarted => $"{primary}, 상위권 {secondary}에게 도전",
        WorldEventKind.CombatPhysicalAttack => $"{primary} → {secondary}, 공법 공격 {worldEvent.Value:N0} 피해",
        WorldEventKind.CombatSpellCast => $"{primary} → {secondary}, 법술 {worldEvent.Value:N0} 피해",
        WorldEventKind.CombatGuarded => $"{primary}, 다음 공격에 대비해 방어",
        WorldEventKind.CombatRetreated => $"{primary}, {secondary}과의 전투에서 이탈",
        WorldEventKind.CombatEnded => $"{primary}, {secondary}에게 승리 · 경험 +{worldEvent.Value:N0}",
        WorldEventKind.CombatInjury => $"{primary}, 전투 중 {FormatInjurySeverity((InjurySeverity)worldEvent.Value)} 부상",
        WorldEventKind.CombatDeath => $"{primary}, {worldEvent.Value:N0}세에 전투 부상으로 사망",
        WorldEventKind.TravelCompleted => $"{primary}, {WorldMapRules.GetLocation(checked((ushort)worldEvent.Value)).KoreanName}에 도착",
        WorldEventKind.MonsterHuntWon => $"{primary}, {FormatMonster(worldEvent.Value)} 사냥 승리",
        WorldEventKind.MonsterHuntLost => $"{primary}, {FormatMonster(worldEvent.Value)} 사냥 패배",
        WorldEventKind.SpiritStoneMined => $"{primary}, 도시 광맥에서 영석 {KoreanNumberFormatter.Format(worldEvent.Value)} 채굴",
        _ => worldEvent.Kind.ToString(),
    };

    private static string FormatWorldEventNotification(WorldEventNotification notification) =>
        $"{FormatGameDate(GameCalendar.GetDate(notification.Event.Tick))} " +
        $"[{(notification.IsPermanentChronicle ? "영구 연대기" : "세계 로그")}] " +
        FormatWorldEventSummary(
            notification.Event,
            notification.PrimaryName,
            notification.SecondaryName ?? string.Empty);

    private EventRow FormatDebugEntry(DebugLogEntry entry)
    {
        var color = entry.Level switch
        {
            DebugLogLevel.Error => (Brush)FindResource("DangerBrush"),
            DebugLogLevel.Warning => (Brush)FindResource("AccentBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow(
            $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} · #{entry.Sequence:N0}",
            $"{entry.Level} · {entry.Message}",
            color);
    }

    private void WorldRecordsPanel_ObservationRequested(object? sender, EventArgs e)
    {
        if (!_applyingObservation && !_applyingWorkspaceRoute)
        {
            RefreshVisibleObservation();
        }
    }

    private void WorldRecordsPanel_AutoOutputChanged(object? sender, EventArgs e)
    {
        if (_logService.AutoWorldOutput != WorldRecordsPanel.AutoWorldOutput)
        {
            _logService.SetAutoWorldOutput(WorldRecordsPanel.AutoWorldOutput);
        }
        if (_logService.AutoDebugOutput != WorldRecordsPanel.AutoDebugOutput)
        {
            _logService.SetAutoDebugOutput(WorldRecordsPanel.AutoDebugOutput);
        }

        WorldRecordsPanel.SetOutputStatus(
            WorldRecordsPanel.AutoWorldOutput || WorldRecordsPanel.AutoDebugOutput
                ? "자동 기록 중"
                : "자동 기록 꺼짐");
        if (WorldRecordsPanel.SelectedRecordIndex == 2)
        {
            RefreshVisibleObservation();
        }
    }

    private async void WorldRecordsPanel_WorldExportRequested(object? sender, EventArgs e)
    {
        if (_runner is null)
        {
            return;
        }

        try
        {
            WorldRecordsPanel.SetOutputStatus("세계 기록 출력 중…");
            var snapshot = _runner.GetSnapshot(
                rankingCount: 1,
                eventCount: World.WorldLogCapacity,
                sections: ObservationSections.WorldLog | ObservationSections.WorldChronicle,
                worldChronicleCount: int.MaxValue);
            var recent = snapshot.RecentEvents
                .Select(item => FormatWorldEventFileLine(snapshot, item, isPermanent: false))
                .ToArray();
            var chronicle = snapshot.PermanentWorldChronicle
                .Select(FormatWorldChronicleFileLine)
                .ToArray();
            var path = await _logService.ExportWorldAsync(recent, chronicle);
            _logService.AddDebug(DebugLogLevel.Information, $"세계 기록 수동 출력 완료 · {path}");
            WorldRecordsPanel.SetOutputStatus($"완료 · {Path.GetFileName(path)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logService.AddDebug(DebugLogLevel.Error, $"세계 기록 수동 출력 실패 · {exception.Message}");
            WorldRecordsPanel.SetOutputStatus($"출력 실패 · {exception.Message}", isError: true);
        }
    }

    private async void WorldRecordsPanel_DebugExportRequested(object? sender, EventArgs e)
    {
        try
        {
            _logService.AddDebug(DebugLogLevel.Information, "디버그 로그 수동 출력 요청");
            WorldRecordsPanel.SetOutputStatus("디버그 로그 출력 중…");
            var path = await _logService.ExportDebugAsync();
            _logService.AddDebug(DebugLogLevel.Information, $"디버그 로그 수동 출력 완료 · {path}");
            WorldRecordsPanel.SetOutputStatus($"완료 · {Path.GetFileName(path)}");
            if (WorldRecordsPanel.SelectedRecordIndex == 2)
            {
                RefreshVisibleObservation();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logService.AddDebug(DebugLogLevel.Error, $"디버그 로그 수동 출력 실패 · {exception.Message}");
            WorldRecordsPanel.SetOutputStatus($"출력 실패 · {exception.Message}", isError: true);
        }
    }

    private void WorldRecordsPanel_FolderOpenRequested(object? sender, EventArgs e)
    {
        try
        {
            var directory = GameLogService.EnsureOutputDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
            _logService.AddDebug(DebugLogLevel.Information, $"로그 폴더 열기 · {directory}");
            WorldRecordsPanel.SetOutputStatus("로그 폴더 열림");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or Win32Exception
            or InvalidOperationException)
        {
            _logService.AddDebug(DebugLogLevel.Error, $"로그 폴더 열기 실패 · {exception.Message}");
            WorldRecordsPanel.SetOutputStatus($"폴더 열기 실패 · {exception.Message}", isError: true);
        }
    }

    private static string FormatWorldEventFileLine(
        WorldSnapshot snapshot,
        WorldEvent worldEvent,
        bool isPermanent)
    {
        var primary = worldEvent.PrimaryName ?? GetAgentName(snapshot, worldEvent.PrimaryAgent);
        var secondary = worldEvent.SecondaryName
            ?? (worldEvent.SecondaryAgent is { } secondaryAgent
                ? GetAgentName(snapshot, secondaryAgent)
                : string.Empty);
        return $"{FormatGameDate(GameCalendar.GetDate(worldEvent.Tick))} " +
            $"[{(isPermanent ? "영구 연대기" : "세계 로그")}] " +
            FormatWorldEventSummary(worldEvent, primary, secondary);
    }

    private static string FormatWorldChronicleFileLine(WorldCenturyChronicleEntry entry) =>
        $"[영구 세계 연대기 · 제 {entry.CenturyNumber:N0}권 · " +
        $"세계력 {entry.StartYear:N0}~{entry.EndYear:N0}년] " +
        FormatWorldHistorySummary(entry);
}
