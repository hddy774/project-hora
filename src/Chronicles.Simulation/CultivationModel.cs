namespace Chronicles.Simulation;

public enum RealmStage : byte
{
    QiRefining,
    FoundationEstablishment,
    GoldenCore,
    NascentSoul,
    SpiritTransformation,
    VoidRefinement,
    BodyIntegration,
    TribulationCrossing,
    Mahayana,
    HalfImmortal,
}

public enum MinorStage : byte
{
    Early = 1,
    Middle,
    Late,
    GreatCompletion,
}

public enum CultivationState : byte
{
    Progressing,
    PreparingBreakthrough,
    PreflightReady,
    AttemptingBreakthrough,
    Bottleneck,
    Stabilizing,
    Completed,
}

public enum CultivationBottleneckCause : byte
{
    None,
    Knowledge,
    Body,
    SpiritualRoot,
    Resources,
    Stability,
}
