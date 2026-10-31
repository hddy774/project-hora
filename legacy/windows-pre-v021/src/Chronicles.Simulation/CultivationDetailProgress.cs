namespace Chronicles.Simulation;

public enum CultivationDetailStageState : byte
{
    Pending,
    Active,
    Completed,
}

public readonly record struct CultivationDetailStageDefinition(
    string StableId,
    string KoreanName,
    string Description,
    int WeightBasisPoints,
    bool RequiresReview = true);

public readonly record struct CultivationDetailStageProgress(
    string StableId,
    string KoreanName,
    string Description,
    long CompletedWork,
    long RequiredWork,
    int ProgressBasisPoints,
    CultivationDetailStageState State,
    bool RequiresReview);

public sealed record CultivationDetailProgressSnapshot(
    string ScopeStableId,
    string ScopeKoreanName,
    int DefinitionRevision,
    long AccumulatedWork,
    long TotalRequiredWork,
    int OverallProgressBasisPoints,
    int CurrentStageIndex,
    string CurrentStageStableId,
    string CurrentStageKoreanName,
    IReadOnlyList<CultivationDetailStageProgress> Stages,
    bool RequiresReview);

/// <summary>
/// Projects the already-authoritative cultivation Work into observer-facing sub stages.
/// It never consumes RNG, changes Work, creates enlightenment events, or changes skill quality.
/// Stage names and weights are provisional PC review data until the source specification confirms them.
/// </summary>
public static class CultivationDetailProgressRules
{
    public const int CurrentRevision = 1;
    public const int BasisPointMaximum = 10_000;
    public const int StageCount = 5;
    public const string ReviewId = "PC-TEMP-CULT-DETAIL-STAGES-021";

    private static readonly int[] Weights = [1_000, 1_500, 2_000, 2_500, 3_000];

    private static readonly CultivationDetailStageDefinition[] EntryStages = CreateDefinitions(
        "entry",
        ("호흡 안정", "몸과 호흡을 가다듬어 영기를 느낄 준비를 합니다."),
        ("감각 개방", "평범한 감각 너머의 미세한 영기 변화를 구분합니다."),
        ("영기 탐색", "주변 환경에서 영기가 흐르는 방향과 밀도를 찾습니다."),
        ("첫 감응", "영근과 외부 영기가 처음 호응하는 상태를 반복해 안정시킵니다."),
        ("단전 개문", "영기를 몸 안으로 인도해 연기경 입문 기반을 엽니다."));

    private static readonly CultivationDetailStageDefinition[][] RealmStages =
    [
        CreateDefinitions(
            "qi_refining",
            ("영기 감지", "천지에 흩어진 영기의 흐름과 밀도를 분별합니다."),
            ("천지 감응", "감지한 영기와 몸·영근의 호응을 안정시킵니다."),
            ("원리 이해·깨달음", "현재 층의 수행 원리를 이해하고 내적 깨달음을 정리합니다."),
            ("영기 인도", "영기를 경맥을 따라 단전으로 안전하게 인도합니다."),
            ("단전 축적", "단전에 영기를 쌓고 현재 층의 기반을 다집니다.")),
        CreateDefinitions(
            "foundation",
            ("단전 안정", "연기경에서 쌓은 단전과 경맥의 균형을 안정시킵니다."),
            ("삼도 감응", "인도·지도·천도의 축기 가능성과 Source를 감응합니다."),
            ("축기 원리 이해", "선택한 축기 경로와 공법의 결합 원리를 정리합니다."),
            ("진원 응축", "흩어진 영기를 더 밀도 높은 진원으로 응축합니다."),
            ("도기 축성", "용량·품질·안정성을 갖춘 축기 기반을 세웁니다.")),
        CreateDefinitions(
            "golden_core",
            ("진원 수렴", "축기 기반의 진원을 단전 중심으로 수렴합니다."),
            ("단핵 감응", "금단의 핵이 형성될 위치와 회전을 감응합니다."),
            ("금단 원리 이해", "팽창·정련·강화·순환의 균형을 이해합니다."),
            ("금성 정련", "응축한 단핵을 반복 정련해 불순을 줄입니다."),
            ("금단 완성", "안정적인 금단 순환과 현재 층의 기반을 완성합니다.")),
        CreateDefinitions(
            "nascent_soul",
            ("신식 응집", "흩어진 신식을 금단과 연결해 응집합니다."),
            ("영태 감응", "원영이 자라날 영태의 형상과 반응을 감응합니다."),
            ("원영 원리 이해", "신식·영태·본명의 결속 원리를 이해합니다."),
            ("원영 배양", "영태에 영력과 신식을 공급해 원영을 배양합니다."),
            ("본명 결속", "원영의 성숙·안정·독립성과 본명을 결속합니다.")),
        CreateDefinitions(
            "spirit_transformation",
            ("원신 정련", "원영에서 확장된 원신을 정련합니다."),
            ("천지 교감", "원신으로 더 넓은 천지의 흐름을 감응합니다."),
            ("화신 원리 이해", "원신 승화와 화신 현현의 원리를 이해합니다."),
            ("화신 분화", "원신의 일부를 안정적인 화신으로 분화합니다."),
            ("법칙 공명", "화신과 천지 법칙의 공명을 안정시킵니다.")),
        CreateDefinitions(
            "void_refinement",
            ("허공 감지", "물질 너머 허공의 결을 감지합니다."),
            ("허공 감응", "법력과 허공의 반응을 반복해 안정시킵니다."),
            ("연허 원리 이해", "법력의 허화와 공간 교섭 원리를 이해합니다."),
            ("법력 허화", "법력을 허공의 성질에 맞게 정련합니다."),
            ("연허 순환", "허화한 법력의 내외 순환을 완성합니다.")),
        CreateDefinitions(
            "body_integration",
            ("육신 정련", "고경지 법력을 견디도록 육신을 정련합니다."),
            ("원신 감응", "육신과 원신 사이의 미세한 불일치를 감응합니다."),
            ("합체 원리 이해", "육신·원신·법력의 합일 원리를 이해합니다."),
            ("내외 통합", "내부 도체와 외부 천지의 흐름을 통합합니다."),
            ("합체 안정", "육신과 원신의 합일을 안정적으로 유지합니다.")),
        CreateDefinitions(
            "tribulation",
            ("겁운 감지", "다가오는 천겁의 징조와 겁운을 감지합니다."),
            ("천겁 감응", "자신의 도와 천겁이 충돌할 지점을 감응합니다."),
            ("도심 이해", "천겁 동안 지켜야 할 도심과 대응 원리를 정리합니다."),
            ("겁력 대비", "육신·원신·법보에 천겁 대응력을 축적합니다."),
            ("겁후 재구성", "천겁 이후 다시 세울 경지 기반을 준비합니다.")),
        CreateDefinitions(
            "mahayana",
            ("대도 감지", "자신의 수행이 닿을 대도의 방향을 감지합니다."),
            ("법칙 감응", "축적한 법칙 이해 사이의 공명을 감응합니다."),
            ("대승 원리 이해", "경지와 법칙을 하나의 도로 통합해 이해합니다."),
            ("도과 응집", "통합한 이해를 안정적인 도과로 응집합니다."),
            ("승선 기반", "범계를 벗어날 대승의 기반을 완성합니다.")),
        CreateDefinitions(
            "half_immortal",
            ("선기 감지", "범계 영기와 다른 선기의 흔적을 감지합니다."),
            ("선기 감응", "육신·원신과 선기의 호응을 안정시킵니다."),
            ("반선 원리 이해", "범태 탈각과 선원 응축의 원리를 이해합니다."),
            ("선체 정련", "선기를 받아들일 수 있도록 육신과 원신을 정련합니다."),
            ("선원 축적", "선원을 축적해 반선경 기반을 안정시킵니다.")),
    ];

    public static ProvisionalBalanceDefinition ProvisionalBalance { get; } = new(
        ReviewId,
        CurrentRevision,
        "10/15/20/25/30% 순차 세부 단계",
        "기존 대경지 전체 Work를 변경하지 않는 관찰용 파생 투영",
        true);

    public static IReadOnlyList<CultivationDetailStageDefinition> GetDefinitions(
        CultivationLifecyclePhase phase,
        RealmStage realm)
    {
        if (phase is CultivationLifecyclePhase.PreCultivation
            or CultivationLifecyclePhase.EntryTraining)
        {
            return EntryStages;
        }

        _ = RealmRules.GetProfile(realm);
        return RealmStages[(int)realm];
    }

    public static CultivationDetailProgressSnapshot Create(
        CultivationLifecyclePhase phase,
        RealmStage realm,
        int floor,
        int currentWork,
        bool floorNineCompleted,
        bool greatPerfectionAchieved)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentWork);

        var isEntry = phase is CultivationLifecyclePhase.PreCultivation
            or CultivationLifecyclePhase.EntryTraining;
        var definitions = GetDefinitions(phase, realm);
        long accumulatedWork;
        long totalRequiredWork;
        string scopeStableId;
        string scopeKoreanName;
        if (isEntry)
        {
            totalRequiredWork = CultivationRules.RequiredEntryWork;
            accumulatedWork = Math.Min(currentWork, totalRequiredWork);
            scopeStableId = "cultivation.entry";
            scopeKoreanName = "범인 입문 준비";
        }
        else
        {
            if (floor is < 1 or > RealmRules.MinorStageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(floor));
            }

            var currentRequirement = RealmRules.GetMinorStageRequiredWork(realm, floor);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(currentWork, currentRequirement);

            totalRequiredWork = RealmRules.GetTotalRealmRequiredWork(realm);
            accumulatedWork = GetCompletedFloorWork(realm, floor)
                + Math.Min(currentWork, currentRequirement);
            if (floorNineCompleted || greatPerfectionAchieved)
            {
                accumulatedWork = totalRequiredWork;
            }

            scopeStableId = RealmRules.GetProfile(realm).StableId;
            scopeKoreanName = GetRealmKoreanName(realm);
        }

        var stages = ProjectStages(definitions, accumulatedWork, totalRequiredWork);
        var currentStageIndex = Array.FindIndex(
            stages,
            stage => stage.State != CultivationDetailStageState.Completed);
        if (currentStageIndex < 0)
        {
            currentStageIndex = stages.Length - 1;
        }

        var currentStage = stages[currentStageIndex];
        var overallProgress = totalRequiredWork == 0
            ? 0
            : checked((int)Math.Min(
                BasisPointMaximum,
                accumulatedWork * BasisPointMaximum / totalRequiredWork));
        return new CultivationDetailProgressSnapshot(
            scopeStableId,
            scopeKoreanName,
            CurrentRevision,
            accumulatedWork,
            totalRequiredWork,
            overallProgress,
            currentStageIndex,
            currentStage.StableId,
            currentStage.KoreanName,
            stages,
            RequiresReview: true);
    }

    private static CultivationDetailStageProgress[] ProjectStages(
        IReadOnlyList<CultivationDetailStageDefinition> definitions,
        long accumulatedWork,
        long totalRequiredWork)
    {
        var result = new CultivationDetailStageProgress[definitions.Count];
        var cumulativeWeight = 0;
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var startWork = totalRequiredWork * cumulativeWeight / BasisPointMaximum;
            cumulativeWeight += definition.WeightBasisPoints;
            var endWork = index == definitions.Count - 1
                ? totalRequiredWork
                : totalRequiredWork * cumulativeWeight / BasisPointMaximum;
            var requiredWork = Math.Max(1, endWork - startWork);
            var completedWork = Math.Clamp(accumulatedWork - startWork, 0, requiredWork);
            var progress = checked((int)(completedWork * BasisPointMaximum / requiredWork));
            var state = progress switch
            {
                >= BasisPointMaximum => CultivationDetailStageState.Completed,
                > 0 => CultivationDetailStageState.Active,
                _ when accumulatedWork == startWork => CultivationDetailStageState.Active,
                _ => CultivationDetailStageState.Pending,
            };
            result[index] = new CultivationDetailStageProgress(
                definition.StableId,
                definition.KoreanName,
                definition.Description,
                completedWork,
                requiredWork,
                progress,
                state,
                definition.RequiresReview);
        }

        return result;
    }

    private static long GetCompletedFloorWork(RealmStage realm, int currentFloor)
    {
        long completed = 0;
        for (var floor = 1; floor < currentFloor; floor++)
        {
            completed += RealmRules.GetMinorStageRequiredWork(realm, floor);
        }

        return completed;
    }

    private static CultivationDetailStageDefinition[] CreateDefinitions(
        string scope,
        params (string Name, string Description)[] stages)
    {
        if (stages.Length != StageCount)
        {
            throw new ArgumentException($"세부 수행 단계는 정확히 {StageCount}개여야 합니다.", nameof(stages));
        }

        var result = new CultivationDetailStageDefinition[StageCount];
        for (var index = 0; index < StageCount; index++)
        {
            result[index] = new CultivationDetailStageDefinition(
                $"cultivation.detail.{scope}.{index + 1}",
                stages[index].Name,
                stages[index].Description,
                Weights[index]);
        }

        return result;
    }

    private static string GetRealmKoreanName(RealmStage realm) => realm switch
    {
        RealmStage.QiRefining => "연기경",
        RealmStage.FoundationEstablishment => "축기경",
        RealmStage.GoldenCore => "금단경",
        RealmStage.NascentSoul => "원영경",
        RealmStage.SpiritTransformation => "화신경",
        RealmStage.VoidRefinement => "연허경",
        RealmStage.BodyIntegration => "합체경",
        RealmStage.TribulationCrossing => "도겁경",
        RealmStage.Mahayana => "대승경",
        RealmStage.HalfImmortal => "반선경",
        _ => throw new ArgumentOutOfRangeException(nameof(realm)),
    };
}
