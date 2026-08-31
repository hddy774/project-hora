using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private WorldMapSnapshot CreateMapSnapshot(ushort? detailedLocationId)
    {
        if (detailedLocationId is { } selected
            && selected >= WorldMapRules.LocationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(detailedLocationId));
        }

        var populationByLocation = new int[WorldMapRules.LocationCount];
        List<MapAgentSummary>? detailedAgents = detailedLocationId is null
            ? null
            : new List<MapAgentSummary>(WorldMapRules.DetailedAgentLimit);
        for (var index = 0; index < Population; index++)
        {
            var locationId = _agents.LocationIds[index];
            populationByLocation[locationId]++;
            if (locationId == detailedLocationId
                && detailedAgents!.Count < WorldMapRules.DetailedAgentLimit)
            {
                detailedAgents.Add(new MapAgentSummary(
                    new AgentId(index),
                    _agents.Names[index],
                    _agents.RealmStages[index],
                    _agents.RealmLayers[index],
                    _agents.Activities[index],
                    _agents.Power[index].Total));
            }
        }

        var locations = new MapLocationSnapshot[WorldMapRules.LocationCount];
        foreach (var definition in WorldMapRules.AllLocations)
        {
            IReadOnlyList<MonsterHabitatSnapshot> monsters;
            if (definition.Biome == BiomeType.City)
            {
                monsters = Array.Empty<MonsterHabitatSnapshot>();
            }
            else
            {
                var habitats = new MonsterHabitatSnapshot[WorldMapRules.MonstersPerWildBiome];
                for (var localIndex = 0; localIndex < habitats.Length; localIndex++)
                {
                    habitats[localIndex] = new MonsterHabitatSnapshot(
                        WorldMapRules.GetMonster(definition.Biome, localIndex),
                        _monsterPopulations[WorldMapRules.GetMonsterPopulationOffset(
                            definition.Id,
                            localIndex)]);
                }

                monsters = habitats;
            }

            locations[definition.Id] = new MapLocationSnapshot(
                definition.Id,
                definition.KoreanName,
                definition.Biome,
                definition.X,
                definition.Y,
                populationByLocation[definition.Id],
                monsters,
                definition.Id == detailedLocationId
                    ? detailedAgents!.ToArray()
                    : Array.Empty<MapAgentSummary>());
        }

        return new WorldMapSnapshot(
            locations,
            _totalMonsterHunts,
            _totalMonsterVictories,
            detailedLocationId);
    }

    private static int[] CreateInitialMonsterPopulations()
    {
        var populations = new int[
            WorldMapRules.LocationCount * WorldMapRules.MonstersPerWildBiome];
        foreach (var location in WorldMapRules.AllLocations)
        {
            if (location.Biome == BiomeType.City)
            {
                continue;
            }

            for (var localIndex = 0; localIndex < WorldMapRules.MonstersPerWildBiome; localIndex++)
            {
                populations[WorldMapRules.GetMonsterPopulationOffset(location.Id, localIndex)] =
                    WorldMapRules.InitialMonsterPopulation;
            }
        }

        return populations;
    }

    private void RegenerateMonsters(long currentMonth)
    {
        if (currentMonth <= _lastMonsterRegenerationMonth)
        {
            return;
        }

        var monthCount = currentMonth - _lastMonsterRegenerationMonth;
        var regeneration = Math.Min(
            WorldMapRules.MaximumMonsterPopulation,
            checked((int)Math.Min(
                WorldMapRules.MaximumMonsterPopulation,
                monthCount * WorldMapRules.MonthlyMonsterRegeneration)));
        foreach (var location in WorldMapRules.AllLocations)
        {
            if (location.Biome == BiomeType.City)
            {
                continue;
            }

            for (var localIndex = 0; localIndex < WorldMapRules.MonstersPerWildBiome; localIndex++)
            {
                var offset = WorldMapRules.GetMonsterPopulationOffset(location.Id, localIndex);
                _monsterPopulations[offset] = Math.Min(
                    WorldMapRules.MaximumMonsterPopulation,
                    _monsterPopulations[offset] + regeneration);
            }
        }

        _lastMonsterRegenerationMonth = currentMonth;
    }

    private bool TrySelectMapActivity(int index, out AgentActivity activity)
    {
        var location = WorldMapRules.GetLocation(_agents.LocationIds[index]);
        if (location.Biome != BiomeType.City)
        {
            if (_random.NextInt(100) < 68)
            {
                activity = AgentActivity.HuntingMonster;
                return true;
            }

            PrepareTravel(index, preferCity: true);
            activity = AgentActivity.Traveling;
            return true;
        }

        if (_random.NextInt(100) < 8)
        {
            PrepareTravel(index, preferCity: false);
            activity = AgentActivity.Traveling;
            return true;
        }

        activity = default;
        return false;
    }

    private void PrepareTravel(int index, bool preferCity)
    {
        var current = _agents.LocationIds[index];
        ushort destination;
        if (preferCity && _random.NextInt(100) < 70)
        {
            destination = WorldMapRules.CityLocationId;
        }
        else
        {
            do
            {
                destination = checked((ushort)_random.NextInt(WorldMapRules.LocationCount));
            }
            while (destination == current || (preferCity && WorldMapRules.IsCity(destination)));
        }

        _agents.TravelDestinationIds[index] = destination;
    }

    private void ResolveTravel(int index)
    {
        var destination = _agents.TravelDestinationIds[index];
        _agents.LocationIds[index] = destination;
        AddEvent(WorldEventKind.TravelCompleted, index, destination);
    }

    private bool ResolveMonsterHunt(int index)
    {
        var location = WorldMapRules.GetLocation(_agents.LocationIds[index]);
        if (location.Biome == BiomeType.City)
        {
            _agents.Activities[index] = AgentActivity.Cultivating;
            return false;
        }

        var monsterIndex = SelectAvailableMonsterIndex(location.Id);
        if (monsterIndex < 0)
        {
            PrepareTravel(index, preferCity: true);
            _agents.Activities[index] = AgentActivity.Traveling;
            return false;
        }

        var monster = WorldMapRules.GetMonster(location.Biome, monsterIndex);
        _totalMonsterHunts++;
        _agents.MonsterHunts[index]++;

        var stage = (int)_agents.RealmStages[index] + 1;
        var danger = checked((long)monster.BaseDanger * stage * stage * stage);
        var threat = Math.Max(1L, GetActualCombatThreat(index));
        var advantage = checked((threat - danger) * 5_000 / Math.Max(1L, threat + danger));
        var victoryChance = checked((int)Math.Clamp(5_000 + advantage, 800L, 9_500L));
        if (_random.Chance(victoryChance))
        {
            var populationOffset = WorldMapRules.GetMonsterPopulationOffset(
                location.Id,
                monsterIndex);
            _monsterPopulations[populationOffset]--;
            _totalMonsterVictories++;
            _agents.MonsterVictories[index]++;
            var experienceGain = checked(40 + (int)Math.Min(600L, danger / 200));
            ApplyCombatProgress(
                index,
                experienceGain,
                2 + Math.Min(6, monster.BaseDanger / 500));
            _agents.AddMaterial(index, monster.DropMaterial, 1 + _random.NextInt(3));
            if (_random.NextInt(100) < 35)
            {
                var damage = Math.Max(1, _agents.MaximumHealth[index] * (2 + _random.NextInt(7)) / 100);
                _agents.Health[index] = Math.Max(1, _agents.Health[index] - damage);
                ApplyMonsterInjury(index, damage);
            }

            AddEvent(WorldEventKind.MonsterHuntWon, index, monster.Id);
            return false;
        }

        var lossDamage = Math.Max(
            1,
            _agents.MaximumHealth[index] * (15 + _random.NextInt(26)) / 100);
        _agents.Health[index] = Math.Max(0, _agents.Health[index] - lossDamage);
        var fatal = ApplyMonsterInjury(index, lossDamage);
        AddEvent(WorldEventKind.MonsterHuntLost, index, monster.Id);
        if (!fatal)
        {
            return false;
        }

        BeginReincarnation(index, combatDeath: true);
        return true;
    }

    private int SelectAvailableMonsterIndex(ushort locationId)
    {
        var start = _random.NextInt(WorldMapRules.MonstersPerWildBiome);
        for (var offset = 0; offset < WorldMapRules.MonstersPerWildBiome; offset++)
        {
            var localIndex = (start + offset) % WorldMapRules.MonstersPerWildBiome;
            if (_monsterPopulations[WorldMapRules.GetMonsterPopulationOffset(
                    locationId,
                    localIndex)] > 0)
            {
                return localIndex;
            }
        }

        return -1;
    }

    private bool ApplyMonsterInjury(int index, int damage)
    {
        var injuryGain = checked((int)Math.Min(
            CombatInjuryRules.MaximumInjuryPoints,
            (long)damage * CombatInjuryRules.MaximumInjuryPoints
                / Math.Max(1, _agents.MaximumHealth[index])
                / 2));
        var points = _agents.Health[index] <= 0
            ? CombatInjuryRules.KnockoutInjuryPoints
            : Math.Min(
                CombatInjuryRules.MaximumInjuryPoints,
                _agents.InjuryPoints[index] + injuryGain);
        _agents.InjuryPoints[index] = checked((ushort)points);
        var severity = CombatInjuryRules.GetSeverity(points);
        if (severity >= InjurySeverity.Severe)
        {
            AddEvent(WorldEventKind.CombatInjury, index, (int)severity);
        }

        return _random.Chance(CombatInjuryRules.GetFatalChanceBasisPoints(severity));
    }

    private long CalculateTravelDuration(int index)
    {
        var months = WorldMapRules.GetTravelMonths(
            _agents.LocationIds[index],
            _agents.TravelDestinationIds[index]);
        return Math.Max(
            GameCalendar.GetNextMonthStart(Tick).Value - Tick.Value,
            checked((long)months * GameCalendar.TicksPerYear / GameCalendar.MonthsPerYear));
    }

    private long CalculateMonsterHuntDuration(int index)
    {
        var biome = WorldMapRules.GetLocation(_agents.LocationIds[index]).Biome;
        var months = biome switch
        {
            BiomeType.Forest => 1,
            BiomeType.Plains => 1,
            BiomeType.Mountains => 2,
            _ => 1,
        };
        return Math.Max(
            GameCalendar.GetNextMonthStart(Tick).Value - Tick.Value,
            checked((long)months * GameCalendar.TicksPerYear / GameCalendar.MonthsPerYear));
    }
}
