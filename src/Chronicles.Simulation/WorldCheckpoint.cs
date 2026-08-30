using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed record AgentCheckpoint(
    string Name,
    byte SpiritualRoot,
    byte Bone,
    byte Constitution,
    byte Comprehension,
    RealmStage RealmStage,
    byte RealmLayer,
    int CultivationProgress,
    byte TechniqueGrade,
    ushort TechniqueMastery,
    byte SpellGrade,
    ushort SpellMastery,
    int CombatExperience,
    int Wins,
    int Losses,
    int Health,
    int MaximumHealth,
    int Qi,
    int MaximumQi,
    AgentActivity Activity,
    long NextDecisionTick,
    int TargetRank,
    int Opponent,
    int LastOpponent,
    long LastCombatTick,
    long CombatStartedTick,
    byte CombatActions,
    bool Guarding,
    int ChallengesIssued);

public sealed record WorldCheckpoint(
    int SchemaVersion,
    ulong Seed,
    ulong RandomState,
    long Tick,
    AgentCheckpoint[] Agents,
    WorldEvent[] Events,
    long EventSequence,
    int TotalBreakthroughs,
    int TotalChallenges,
    int TotalCombatsStarted,
    int TotalCombatsCompleted,
    int ActiveCombats)
{
    public const int CurrentSchemaVersion = 1;
}
