using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 인물 SoA의 수행 구성요소, 입문 단련, 재료·법보 접근 작업집합이다.
/// 배열 소유권은 AgentStore에 유지하고 전이 코드는 좁은 메서드만 사용한다.
/// </summary>
internal sealed partial class AgentStore
{
    public int GetMaterialCount(int index, MaterialId material) =>
        MaterialInventory[GetMaterialOffset(index, material)];

    public int GetCultivationComponentValue(int index, int component) =>
        CultivationComponentValues[GetCultivationComponentOffset(index, component)];

    public int GetCultivationComponentLimit(int index, int component) =>
        CultivationComponentLimits[GetCultivationComponentOffset(index, component)];

    public void SetCultivationComponent(int index, int component, int value, int limit)
    {
        if (value is < 0 or > QualityValue.MaximumHundredths
            || limit is < 0 or > QualityValue.MaximumHundredths
            || value > limit)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var offset = GetCultivationComponentOffset(index, component);
        CultivationComponentValues[offset] = checked((ushort)value);
        CultivationComponentLimits[offset] = checked((ushort)limit);
    }

    public int GetCultivationComponentAverage(int index, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var start = index * CultivationRules.RealmComponentSlotCount;
        var total = 0;
        for (var component = 0; component < count; component++)
        {
            total += CultivationComponentValues[start + component];
        }

        return total / count;
    }

    public ushort[] CopyCultivationComponentValues(int index) =>
        CopyCultivationArray(
            CultivationComponentValues,
            index,
            CultivationRules.RealmComponentSlotCount);

    public ushort[] CopyCultivationComponentLimits(int index) =>
        CopyCultivationArray(
            CultivationComponentLimits,
            index,
            CultivationRules.RealmComponentSlotCount);

    public ushort[] CopyFormationValues(int index) =>
        CopyCultivationArray(FormationValues, index, CultivationRules.FormationSlotCount);

    public int GetEntryGateTempering(int index, int gateIndex) =>
        EntryGateTemperings[GetEntryGateTemperingOffset(index, gateIndex)];

    public ReadOnlySpan<ushort> GetEntryGateTemperings(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return EntryGateTemperings.AsSpan(
            index * CultivationRules.EntryGateSlotCount,
            CultivationRules.EntryGateSlotCount);
    }

    public void AddFailedEntryGateTemperings(int index, byte passedMask, int gain)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gain);
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var start = index * CultivationRules.EntryGateSlotCount;
        for (var gate = 0; gate < CultivationRules.EntryGateSlotCount; gate++)
        {
            if ((passedMask & (1 << gate)) != 0)
            {
                continue;
            }
            EntryGateTemperings[start + gate] = checked((ushort)Math.Min(
                QualityValue.MaximumHundredths,
                EntryGateTemperings[start + gate] + gain));
        }
    }

    public void AddEntryGateTempering(int index, int gateIndex, int gain)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gain);
        var offset = GetEntryGateTemperingOffset(index, gateIndex);
        EntryGateTemperings[offset] = checked((ushort)Math.Min(
            QualityValue.MaximumHundredths,
            EntryGateTemperings[offset] + gain));
    }

    public ushort[] CopyEntryGateTemperings(int index) =>
        CopyCultivationArray(EntryGateTemperings, index, CultivationRules.EntryGateSlotCount);

    public void RestoreCultivationArrays(
        int index,
        ushort[] componentValues,
        ushort[] componentLimits,
        ushort[] formationValues,
        ushort[]? entryGateTemperings = null)
    {
        Array.Copy(
            componentValues,
            0,
            CultivationComponentValues,
            index * CultivationRules.RealmComponentSlotCount,
            CultivationRules.RealmComponentSlotCount);
        Array.Copy(
            componentLimits,
            0,
            CultivationComponentLimits,
            index * CultivationRules.RealmComponentSlotCount,
            CultivationRules.RealmComponentSlotCount);
        Array.Copy(
            formationValues,
            0,
            FormationValues,
            index * CultivationRules.FormationSlotCount,
            CultivationRules.FormationSlotCount);
        if (entryGateTemperings is not null)
        {
            Array.Copy(
                entryGateTemperings,
                0,
                EntryGateTemperings,
                index * CultivationRules.EntryGateSlotCount,
                CultivationRules.EntryGateSlotCount);
        }
    }

    public void AddMaterial(int index, MaterialId material, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var offset = GetMaterialOffset(index, material);
        MaterialInventory[offset] = checked(MaterialInventory[offset] + count);
    }

    public void ConsumeMaterial(int index, MaterialId material, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var offset = GetMaterialOffset(index, material);
        if (MaterialInventory[offset] < count)
        {
            throw new InvalidOperationException("보유하지 않은 재료를 소비할 수 없습니다.");
        }

        MaterialInventory[offset] -= count;
    }

    public int GetInventoryItemCount(int index)
    {
        var total = 0;
        var start = index * EconomyRules.MaterialCount;
        for (var material = 0; material < EconomyRules.MaterialCount; material++)
        {
            total = checked(total + MaterialInventory[start + material]);
        }

        return total;
    }

    public int[] CopyMaterialInventory(int index)
    {
        var copy = new int[EconomyRules.MaterialCount];
        Array.Copy(
            MaterialInventory,
            index * EconomyRules.MaterialCount,
            copy,
            0,
            copy.Length);
        return copy;
    }

    public ushort[] CopyEquippedTreasureQualities(int index)
    {
        var copy = new ushort[EconomyRules.TreasureSlotCount];
        Array.Copy(
            EquippedTreasureQualities,
            index * EconomyRules.TreasureSlotCount,
            copy,
            0,
            copy.Length);
        return copy;
    }

    public void RestoreEconomyState(int index, int[] materials, ushort[] treasures)
    {
        var materialStart = index * EconomyRules.MaterialCount;
        if (materials.Length == EconomyRules.MaterialCount)
        {
            Array.Copy(materials, 0, MaterialInventory, materialStart, EconomyRules.MaterialCount);
        }
        else if (materials.Length == EconomyRules.LegacyMaterialCount)
        {
            MaterialInventory[materialStart + (int)MaterialId.TreasureMaterial] = checked(
                Sum(materials.AsSpan(0, 10))
                + Sum(materials.AsSpan(20, 5)));
            MaterialInventory[materialStart + (int)MaterialId.PillMaterial] = checked(
                Sum(materials.AsSpan(10, 10))
                + Sum(materials.AsSpan(25, 5)));
        }
        else
        {
            throw new InvalidDataException("지원하지 않는 재료 인벤토리 형식입니다.");
        }

        var treasureStart = index * EconomyRules.TreasureSlotCount;
        if (treasures.Length == EconomyRules.TreasureSlotCount)
        {
            Array.Copy(treasures, 0, EquippedTreasureQualities, treasureStart, EconomyRules.TreasureSlotCount);
        }
        else if (treasures.Length == EconomyRules.LegacyTreasureSlotCount)
        {
            EquippedTreasureQualities[treasureStart + (int)TreasureSlot.NatalTreasure] = treasures[0];
            EquippedTreasureQualities[treasureStart + (int)TreasureSlot.DefenseTreasure] =
                GetBestLegacyTreasure(treasures.AsSpan(1, 3));
            EquippedTreasureQualities[treasureStart + (int)TreasureSlot.AttackTreasure] =
                GetBestLegacyTreasure(treasures.AsSpan(4, 3));
        }
        else
        {
            throw new InvalidDataException("지원하지 않는 법보 장착 형식입니다.");
        }
    }

    private static int Sum(ReadOnlySpan<int> values)
    {
        var total = 0;
        foreach (var value in values)
        {
            total = checked(total + value);
        }

        return total;
    }

    public ushort GetTreasureQuality(int index, TreasureSlot slot) =>
        EquippedTreasureQualities[GetTreasureOffset(index, slot)];

    public void SetTreasureQuality(int index, TreasureSlot slot, ushort quality) =>
        EquippedTreasureQualities[GetTreasureOffset(index, slot)] = quality;

    public long GetTreasurePower(int index)
    {
        long effectiveHundredths = 0;
        for (var slot = 0; slot < EconomyRules.TreasureSlotCount; slot++)
        {
            var raw = EquippedTreasureQualities[(index * EconomyRules.TreasureSlotCount) + slot];
            if (raw != EconomyRules.EmptyTreasureQuality)
            {
                effectiveHundredths = checked(
                    effectiveHundredths + QualityRules.GetEffectiveHundredths(new QualityValue(raw)));
            }
        }

        return SkillCombatScaling.GetTreasureDisplayContribution(effectiveHundredths);
    }

    public int GetTreasureCombatEffect(int index, bool attack)
    {
        long total = 0;
        for (var slotIndex = 0; slotIndex < EconomyRules.TreasureSlotCount; slotIndex++)
        {
            var slot = (TreasureSlot)slotIndex;
            if (slot != TreasureSlot.NatalTreasure
                && (attack ? !EconomyRules.IsWeapon(slot) : !EconomyRules.IsArmor(slot)))
            {
                continue;
            }

            var raw = GetTreasureQuality(index, slot);
            if (raw != EconomyRules.EmptyTreasureQuality)
            {
                total = checked(
                    total + QualityRules.GetEffectiveHundredths(new QualityValue(raw)));
            }
        }

        return SkillCombatScaling.GetTreasureAttributeContribution(total);
    }

}
