namespace Chronicles.Simulation;

public enum GenderIdentity : byte
{
    Undefined,
    Female,
    Male,
    NonBinary,
}

public enum SoulEmbodimentState : byte
{
    Embodied,
    AwaitingReincarnation,
    Crossing,
    Dissolved,
}

public enum BodyLifeState : byte
{
    Living,
    Dead,
    Destroyed,
}

public enum CurrentLifePhase : byte
{
    Living,
    DeathConfirmed,
    ReincarnationHandoff,
    Archived,
}

public enum LifeTransitionReason : byte
{
    Birth,
    LifespanDeath,
    CombatDeath,
    BodyDestroyed,
    ReincarnationEntered,
    Rebirth,
}

public enum LifeTransitionStatus : byte
{
    Applied,
    Rejected,
    InvalidTransition,
    NoCurrentLife,
}

public readonly record struct AppearanceDefinitionId(ulong Value)
{
    public override string ToString() => $"AP{Value:X16}";
}

public readonly record struct BodyAppearanceState(
    AppearanceDefinitionId DefinitionId,
    ushort InnateBeautyHundredths,
    ushort HeightHundredths,
    ushort BuildHundredths,
    ushort FacialFeatureHundredths,
    ushort ComplexionHundredths,
    ushort HairHundredths,
    ushort BearingHundredths,
    short PermanentModifierHundredths,
    ushort ScarSeverityHundredths,
    long AuthorityRevision)
{
    public int DerivedBeautyHundredths => Math.Clamp(
        InnateBeautyHundredths + PermanentModifierHundredths - (ScarSeverityHundredths / 4),
        0,
        10_000);
}

public readonly record struct LifeTransitionResult(
    LifeTransitionStatus Status,
    LifeTransitionReason Reason,
    PersonIdentityId PersonId,
    BodyIdentityId BodyId,
    CurrentLifePhase PreviousPhase,
    CurrentLifePhase CurrentPhase,
    BodyLifeState PreviousBodyState,
    BodyLifeState CurrentBodyState,
    SoulEmbodimentState PreviousSoulState,
    SoulEmbodimentState CurrentSoulState,
    long Tick,
    long AuthorityRevision);

public readonly record struct PersonLifeAuthorityCheckpoint(
    GenderIdentity Gender,
    SoulEmbodimentState SoulState,
    BodyLifeState BodyState,
    CurrentLifePhase LifePhase,
    BodyAppearanceState Appearance,
    long AuthorityRevision,
    LifeTransitionResult LatestTransition);

public readonly record struct PersonLifeAuthoritySnapshot(
    GenderIdentity Gender,
    SoulEmbodimentState SoulState,
    BodyLifeState BodyState,
    CurrentLifePhase LifePhase,
    BodyAppearanceState Appearance,
    long AuthorityRevision,
    LifeTransitionResult LatestTransition);

public static class PersonLifeAuthorityRules
{
    public const string ContractId = "PC-LIFE-AUTHORITY-001";
    public const int ContractRevision = 1;
    public const string AppearanceDistributionReviewId = "PC-TEMP-APPEARANCE-DISTRIBUTION-113";

    public static GenderIdentity GetGender(PersonIdentityId personId)
    {
        var roll = StableValue(personId.Value, 0x4745_4E44_4552UL) % 10_000UL;
        return roll < 4_950UL
            ? GenderIdentity.Female
            : roll < 9_900UL
                ? GenderIdentity.Male
                : GenderIdentity.NonBinary;
    }

    public static BodyAppearanceState CreateAppearance(BodyIdentityId bodyId)
    {
        var definition = new AppearanceDefinitionId(StableValue(bodyId.Value, 0x4150_5045_4152UL));
        return new BodyAppearanceState(
            definition,
            GetFeature(bodyId.Value, 0),
            GetFeature(bodyId.Value, 1),
            GetFeature(bodyId.Value, 2),
            GetFeature(bodyId.Value, 3),
            GetFeature(bodyId.Value, 4),
            GetFeature(bodyId.Value, 5),
            GetFeature(bodyId.Value, 6),
            PermanentModifierHundredths: 0,
            ScarSeverityHundredths: 0,
            AuthorityRevision: 1);
    }

    public static bool IsValid(PersonLifeAuthorityCheckpoint value) =>
        Enum.IsDefined(value.Gender)
        && value.Gender != GenderIdentity.Undefined
        && Enum.IsDefined(value.SoulState)
        && Enum.IsDefined(value.BodyState)
        && Enum.IsDefined(value.LifePhase)
        && value.AuthorityRevision > 0
        && value.Appearance.DefinitionId.Value != 0
        && value.Appearance.AuthorityRevision > 0
        && value.Appearance.InnateBeautyHundredths <= 10_000
        && value.Appearance.HeightHundredths <= 10_000
        && value.Appearance.BuildHundredths <= 10_000
        && value.Appearance.FacialFeatureHundredths <= 10_000
        && value.Appearance.ComplexionHundredths <= 10_000
        && value.Appearance.HairHundredths <= 10_000
        && value.Appearance.BearingHundredths <= 10_000
        && value.Appearance.ScarSeverityHundredths <= 10_000
        && value.LatestTransition.AuthorityRevision == value.AuthorityRevision;

    private static ushort GetFeature(ulong bodyId, int feature)
    {
        // PC-TEMP-APPEARANCE-DISTRIBUTION-113: 정본 확정 전 중립적인 20.00~80.00 분포.
        var value = StableValue(bodyId, checked((ulong)feature + 1UL));
        return checked((ushort)(2_000UL + (value % 6_001UL)));
    }

    private static ulong StableValue(ulong left, ulong right)
    {
        var value = left ^ right ^ 0xD6E8_FEB8_6659_FD93UL;
        value ^= value >> 32;
        value *= 0xD6E8_FEB8_6659_FD93UL;
        value ^= value >> 32;
        value *= 0xD6E8_FEB8_6659_FD93UL;
        value ^= value >> 32;
        return value == 0 ? 1UL : value;
    }
}

internal sealed partial class AgentStore
{
    public PersonLifeAuthoritySnapshot GetLifeAuthoritySnapshot(int index) => new(
        Genders[index],
        SoulEmbodimentStates[index],
        BodyLifeStates[index],
        CurrentLifePhases[index],
        AppearanceStates[index],
        LifeAuthorityRevisions[index],
        LatestLifeTransitions[index]);

    public PersonLifeAuthorityCheckpoint GetLifeAuthorityCheckpoint(int index) => new(
        Genders[index],
        SoulEmbodimentStates[index],
        BodyLifeStates[index],
        CurrentLifePhases[index],
        AppearanceStates[index],
        LifeAuthorityRevisions[index],
        LatestLifeTransitions[index]);

    public void RestoreLifeAuthority(int index, PersonLifeAuthorityCheckpoint checkpoint)
    {
        if (!PersonLifeAuthorityRules.IsValid(checkpoint))
        {
            throw new InvalidDataException("인물 생애 권위 checkpoint가 손상되었습니다.");
        }

        Genders[index] = checkpoint.Gender;
        SoulEmbodimentStates[index] = checkpoint.SoulState;
        BodyLifeStates[index] = checkpoint.BodyState;
        CurrentLifePhases[index] = checkpoint.LifePhase;
        AppearanceStates[index] = checkpoint.Appearance;
        LifeAuthorityRevisions[index] = checkpoint.AuthorityRevision;
        LatestLifeTransitions[index] = checkpoint.LatestTransition;
    }

    public void RestoreLegacyLifeAuthority(int index, long tick)
    {
        var inRiver = LifeStates[index] == PersonLifeState.ReincarnationRiver;
        InitializeLifeAuthority(index, BirthTicks[index]);
        if (!inRiver)
        {
            return;
        }

        ConfirmDeath(index, tick, combatDeath: false);
        EnterReincarnationRiver(index, tick);
    }

    public LifeTransitionResult ConfirmDeath(int index, long tick, bool combatDeath)
    {
        if (CurrentLifePhases[index] != CurrentLifePhase.Living
            || BodyLifeStates[index] != BodyLifeState.Living)
        {
            return CreateRejectedLifeTransition(index, tick, LifeTransitionStatus.InvalidTransition);
        }

        return ApplyLifeTransition(
            index,
            tick,
            combatDeath ? LifeTransitionReason.CombatDeath : LifeTransitionReason.LifespanDeath,
            CurrentLifePhase.DeathConfirmed,
            BodyLifeState.Dead,
            SoulEmbodimentState.AwaitingReincarnation);
    }

    public LifeTransitionResult EnterReincarnationRiver(int index, long tick)
    {
        if (CurrentLifePhases[index] != CurrentLifePhase.DeathConfirmed)
        {
            return CreateRejectedLifeTransition(index, tick, LifeTransitionStatus.InvalidTransition);
        }

        var result = ApplyLifeTransition(
            index,
            tick,
            LifeTransitionReason.ReincarnationEntered,
            CurrentLifePhase.ReincarnationHandoff,
            BodyLifeStates[index],
            SoulEmbodimentState.Crossing);
        LifeStates[index] = PersonLifeState.ReincarnationRiver;
        return result;
    }

    public void ApplyPermanentScar(int index, long tick, int injuryPoints)
    {
        if (BodyLifeStates[index] != BodyLifeState.Living || injuryPoints < 2_500)
        {
            return;
        }

        var appearance = AppearanceStates[index];
        var scarGain = Math.Clamp(injuryPoints / 20, 1, 500);
        AppearanceStates[index] = appearance with
        {
            ScarSeverityHundredths = checked((ushort)Math.Min(
                10_000,
                appearance.ScarSeverityHundredths + scarGain)),
            AuthorityRevision = checked(appearance.AuthorityRevision + 1),
        };
        LifeAuthorityRevisions[index] = checked(LifeAuthorityRevisions[index] + 1);
        LatestLifeTransitions[index] = LatestLifeTransitions[index] with
        {
            Tick = tick,
            AuthorityRevision = LifeAuthorityRevisions[index],
        };
    }

    private void InitializeLifeAuthority(int index, long birthTick)
    {
        var previousPhase = CurrentLifePhases[index];
        var previousBodyState = BodyLifeStates[index];
        var previousSoulState = SoulEmbodimentStates[index];
        var isFirstLife = LifeAuthorityRevisions[index] == 0;
        Genders[index] = PersonLifeAuthorityRules.GetGender(PersonIdentityIds[index]);
        SoulEmbodimentStates[index] = SoulEmbodimentState.Embodied;
        BodyLifeStates[index] = BodyLifeState.Living;
        CurrentLifePhases[index] = CurrentLifePhase.Living;
        AppearanceStates[index] = PersonLifeAuthorityRules.CreateAppearance(BodyIdentityIds[index]);
        var revision = checked(LifeAuthorityRevisions[index] + 1);
        LifeAuthorityRevisions[index] = revision;
        LatestLifeTransitions[index] = new LifeTransitionResult(
            LifeTransitionStatus.Applied,
            isFirstLife ? LifeTransitionReason.Birth : LifeTransitionReason.Rebirth,
            PersonIdentityIds[index],
            BodyIdentityIds[index],
            previousPhase,
            CurrentLifePhase.Living,
            previousBodyState,
            BodyLifeState.Living,
            previousSoulState,
            SoulEmbodimentState.Embodied,
            birthTick,
            revision);
    }

    private LifeTransitionResult ApplyLifeTransition(
        int index,
        long tick,
        LifeTransitionReason reason,
        CurrentLifePhase lifePhase,
        BodyLifeState bodyState,
        SoulEmbodimentState soulState)
    {
        var revision = checked(LifeAuthorityRevisions[index] + 1);
        var result = new LifeTransitionResult(
            LifeTransitionStatus.Applied,
            reason,
            PersonIdentityIds[index],
            BodyIdentityIds[index],
            CurrentLifePhases[index],
            lifePhase,
            BodyLifeStates[index],
            bodyState,
            SoulEmbodimentStates[index],
            soulState,
            tick,
            revision);
        CurrentLifePhases[index] = lifePhase;
        BodyLifeStates[index] = bodyState;
        SoulEmbodimentStates[index] = soulState;
        LifeAuthorityRevisions[index] = revision;
        LatestLifeTransitions[index] = result;
        return result;
    }

    private LifeTransitionResult CreateRejectedLifeTransition(
        int index,
        long tick,
        LifeTransitionStatus status) => new(
            status,
            LifeTransitionReason.BodyDestroyed,
            PersonIdentityIds[index],
            BodyIdentityIds[index],
            CurrentLifePhases[index],
            CurrentLifePhases[index],
            BodyLifeStates[index],
            BodyLifeStates[index],
            SoulEmbodimentStates[index],
            SoulEmbodimentStates[index],
            tick,
            LifeAuthorityRevisions[index]);
}
