using System.IO;
using System.Windows.Media;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Desktop.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void UpdateWorldRecordsPanel(WorldSnapshot snapshot)
    {
        switch (WorldRecordsPanel.SelectedRecordIndex)
        {
            case 0:
                var recentSequence = snapshot.RecentEvents.Count > 0
                    ? snapshot.RecentEvents[^1].Sequence
                    : 0;
                if (_projectedRecentEventSequence == recentSequence
                    && _projectedRecentEventCount == snapshot.RecentEvents.Count)
                {
                    break;
                }

                WorldRecordsPanel.ApplyRecent(snapshot.RecentEvents
                    .Reverse()
                    .Select(item => FormatEvent(snapshot, item))
                    .ToArray());
                _projectedRecentEventSequence = recentSequence;
                _projectedRecentEventCount = snapshot.RecentEvents.Count;
                break;
            case 1:
                var chronicleSequence = snapshot.PermanentWorldChronicle.Count > 0
                    ? snapshot.PermanentWorldChronicle[^1].Sequence
                    : 0;
                if (_projectedWorldChronicleSequence == chronicleSequence
                    && _projectedWorldChronicleCount == snapshot.PermanentWorldChronicleCount)
                {
                    break;
                }

                WorldRecordsPanel.ApplyChronicle(snapshot.PermanentWorldChronicle
                    .Reverse()
                    .Select(item => FormatEvent(snapshot, item))
                    .ToArray(), snapshot.PermanentWorldChronicleCount);
                _projectedWorldChronicleSequence = chronicleSequence;
                _projectedWorldChronicleCount = snapshot.PermanentWorldChronicleCount;
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
        WorldEventKind.TechniqueEnlightenment => $"{primary}, 공법 깨달음 · 이해도 {worldEvent.Value / 100m:0.00}",
        WorldEventKind.SpellEnlightenment => $"{primary}, 법술 깨달음 · 이해도 {worldEvent.Value / 100m:0.00}",
        WorldEventKind.CultivationEntered => $"{primary}, 입문 3 Gate를 통과해 연기경에 입문",
        WorldEventKind.GreatPerfectionAchieved => $"{primary}, {FormatEncodedRealm(worldEvent.Value, greatPerfection: true)} 완성",
        WorldEventKind.Death => $"{primary}, {worldEvent.Value:N0}세에 수명을 다함",
        WorldEventKind.ReincarnationEntered => $"{primary}, 윤회의 강에 들어감",
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
        if (!_applyingObservation)
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
                .Select(item => FormatWorldEventFileLine(snapshot, item, isPermanent: true))
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
}
