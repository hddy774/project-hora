using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public ulong ComputeStateHash() => ComputeStateHashCore(
        includeContent: true,
        includeKnowledge: true,
        includeLaws: true,
        includeGeneration: true,
        includeDecisionRankings: true,
        includeDetailedChronicles: false);

    /// <summary>
    /// 구 슬롯의 저장 당시 hash 계약을 검증할 때만 사용합니다. 복원된 세계의 현재
    /// canonical hash는 항상 <see cref="ComputeStateHash"/>를 사용합니다.
    /// </summary>
    public ulong ComputeStateHashForCheckpointSchema(int checkpointSchema)
    {
        if (checkpointSchema is < 7 or > WorldCheckpoint.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpointSchema));
        }
        return ComputeStateHashCore(
            includeContent: checkpointSchema >= 27,
            includeKnowledge: checkpointSchema >= 28,
            includeLaws: checkpointSchema >= 29,
            includeGeneration: checkpointSchema >= 30,
            includeDecisionRankings: checkpointSchema >= 31,
            includeDetailedChronicles: checkpointSchema < 32);
    }

    private ulong ComputeStateHashCore(
        bool includeContent,
        bool includeKnowledge,
        bool includeLaws,
        bool includeGeneration,
        bool includeDecisionRankings,
        bool includeDetailedChronicles)
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, Seed);
        Add(ref hash, (ulong)Tick.Value);
        Add(ref hash, _random.State);
        Add(ref hash, _balance.DeveloperModeEnabled ? 1UL : 0UL);
        Add(ref hash, checked((ulong)_balance.CultivationWorkPercent));
        Add(ref hash, checked((ulong)_balance.CombatAggressionPercent));
        Add(ref hash, checked((ulong)_balance.EconomyActivityPercent));
        Add(ref hash, checked((ulong)_balance.MonsterDangerPercent));
        Add(ref hash, (ulong)_eventSequence);
        Add(ref hash, (ulong)_chronicleSequence);
        Add(ref hash, (ulong)_personalLogSequence);
        Add(ref hash, (ulong)_totalBreakthroughs);
        Add(ref hash, (ulong)_totalChallenges);
        Add(ref hash, (ulong)_totalCombatsStarted);
        Add(ref hash, (ulong)_totalCombatsCompleted);
        Add(ref hash, (ulong)_activeCombats);
        Add(ref hash, (ulong)_totalDeaths);
        Add(ref hash, (ulong)_totalReincarnations);
        Add(ref hash, _reincarnation.ComputeStateHash());
        Add(ref hash, _causal.ComputeStateHash());
        Add(ref hash, (ulong)_totalMissionsCompleted);
        Add(ref hash, (ulong)_totalTreasuresCrafted);
        Add(ref hash, (ulong)_totalPillsCrafted);
        Add(ref hash, (ulong)_totalMaterialsSold);
        Add(ref hash, (ulong)_totalMonsterHunts);
        Add(ref hash, (ulong)_totalMonsterVictories);
        if (includeDecisionRankings)
        {
            Add(ref hash, unchecked((ulong)(_lastMonthlyBoundaryMonth + 1)));
            Add(ref hash, _combatPowerRankingCache.IsDirty ? 1UL : 0UL);
            Add(ref hash, _realmRankingCache.IsDirty ? 1UL : 0UL);
            Add(ref hash, _aptitudeRankingCache.IsDirty ? 1UL : 0UL);
            Add(ref hash, _wealthRankingCache.IsDirty ? 1UL : 0UL);
        }
        _atlas.AddStateHash(ref hash);
        _organizations.AddStateHash(ref hash);
        if (includeContent)
        {
            Add(ref hash, _content.ComputeStateHash());
        }
        if (includeKnowledge)
        {
            Add(ref hash, _knowledge.ComputeStateHash());
        }
        if (includeLaws)
        {
            Add(ref hash, _laws.ComputeStateHash());
        }
        if (includeGeneration)
        {
            Add(ref hash, _generation.ComputeStateHash());
        }
        Add(ref hash, (ulong)_lastRankingRefreshTick);
        Add(ref hash, (ulong)_rankingMovementFromTick);
        Add(ref hash, (ulong)_rankingRisenCount);
        Add(ref hash, (ulong)_rankingFallenCount);
        Add(ref hash, (ulong)_rankingUnchangedCount);
        Add(ref hash, (ulong)_rankingTotalAbsoluteMovement);
        for (var tier = 0; tier < _eliteCombatChangedCounts.Length; tier++)
        {
            Add(ref hash, (ulong)_eliteCombatChangedCounts[tier]);
            Add(ref hash, (ulong)_eliteRealmChangedCounts[tier]);
        }
        foreach (var frame in _rankingHistory.Frames)
        {
            Add(ref hash, (ulong)frame.Tick.Value);
            for (var index = 0; index < frame.CombatPowerTop.Length; index++)
            {
                Add(ref hash, (ulong)frame.CombatPowerTop[index]);
                Add(ref hash, (ulong)frame.RealmTop[index]);
                Add(ref hash, (ulong)frame.WealthTop[index]);
            }
            for (var tier = 0; tier < frame.CombatPowerPositionChangeTotals.Length; tier++)
            {
                Add(ref hash, (ulong)frame.CombatPowerPositionChangeTotals[tier]);
                Add(ref hash, (ulong)frame.RealmPositionChangeTotals[tier]);
                Add(ref hash, (ulong)frame.WealthPositionChangeTotals[tier]);
            }
        }
        var market = _market.CreateCheckpoint();
        Add(ref hash, (ulong)market.TreasurySpiritStones);
        Add(ref hash, (ulong)market.PurchaseTransactions);
        Add(ref hash, (ulong)market.SaleTransactions);
        Add(ref hash, (ulong)market.MissionDeliveries);
        Add(ref hash, (ulong)market.MaterialsProcessed);
        Add(ref hash, (ulong)market.PillsProduced);
        Add(ref hash, (ulong)market.TreasuresProduced);
        Add(ref hash, (ulong)market.TechniqueBooksProduced);
        Add(ref hash, (ulong)market.SpellBooksProduced);
        Add(ref hash, (ulong)market.TreasureQualityHundredths);
        Add(ref hash, (ulong)market.LastIndustryMonth);
        Add(ref hash, (ulong)market.SpiritStonesMined);
        Add(ref hash, (ulong)market.RemainingSpiritStoneOre);
        Add(ref hash, (ulong)market.MiningMissionCount);
        Add(ref hash, (ulong)market.SpiritStoneReserveTarget);
        Add(ref hash, (ulong)market.CurrentFlowBucket);
        Add(ref hash, (ulong)market.AuthorityRevision);
        Add(ref hash, (ulong)market.NextTradeSequence);
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            Add(ref hash, (ulong)market.Stock[goodId]);
            Add(ref hash, (ulong)market.Prices[goodId]);
            Add(ref hash, (ulong)market.Supply[goodId]);
            Add(ref hash, (ulong)market.Demand[goodId]);
            Add(ref hash, (ulong)(market.QualityHundredths?[goodId] ?? 0));
            Add(ref hash, (ulong)(market.RecentSupply?[goodId] ?? 0));
            Add(ref hash, (ulong)(market.RecentDemand?[goodId] ?? 0));
        }
        foreach (var value in market.SupplyWindowBuckets ?? Array.Empty<long>())
        {
            Add(ref hash, (ulong)value);
        }
        foreach (var value in market.DemandWindowBuckets ?? Array.Empty<long>())
        {
            Add(ref hash, (ulong)value);
        }
        foreach (var source in market.LastSettlementSourceByAgent ?? Array.Empty<long>())
        {
            Add(ref hash, unchecked((ulong)source));
        }
        foreach (var trade in market.RecentTrades ?? Array.Empty<MarketTradeCheckpoint>())
        {
            Add(ref hash, (ulong)trade.Sequence);
            Add(ref hash, (ulong)trade.SourceResultId);
            Add(ref hash, (ulong)trade.Tick);
            Add(ref hash, (ulong)trade.AgentId);
            Add(ref hash, (byte)trade.Kind);
            Add(ref hash, (ulong)trade.GoodId);
            Add(ref hash, (ulong)trade.Count);
            Add(ref hash, (ulong)trade.UnitPrice);
            Add(ref hash, (ulong)trade.TotalSpiritStones);
            Add(ref hash, trade.QualityHundredths);
        }
        foreach (var order in market.ProductionQueue ?? Array.Empty<CityProductionCheckpoint>())
        {
            Add(ref hash, (ulong)order.GoodId);
            Add(ref hash, order.QualityHundredths);
            Add(ref hash, (ulong)order.CompletedMonths);
            Add(ref hash, (ulong)order.RequiredMonths);
            Add(ref hash, (ulong)order.MaterialCost);
            Add(ref hash, (ulong)order.SpiritStoneCost);
        }
        if (market.Authority is { } marketAuthority)
        {
            Add(ref hash, (ulong)marketAuthority.NextOfferId);
            Add(ref hash, (ulong)marketAuthority.NextReservationId);
            Add(ref hash, (ulong)marketAuthority.LastExpiryMonth);
            foreach (var reserved in marketAuthority.ReservedStock)
            {
                Add(ref hash, (ulong)reserved);
            }
            foreach (var offer in marketAuthority.Offers)
            {
                Add(ref hash, (ulong)offer.OfferId);
                Add(ref hash, (ulong)offer.Revision);
                Add(ref hash, (ulong)offer.GoodId);
                Add(ref hash, (byte)offer.Channel);
                Add(ref hash, (byte)offer.Side);
                Add(ref hash, (ulong)offer.CreatedMonth);
                Add(ref hash, (ulong)offer.ExpiresMonth);
                Add(ref hash, (byte)offer.Status);
            }
            foreach (var reservation in marketAuthority.Reservations)
            {
                Add(ref hash, (ulong)reservation.ReservationId);
                Add(ref hash, (ulong)reservation.OfferId);
                Add(ref hash, (ulong)reservation.AgentIndex);
                Add(ref hash, (ulong)reservation.GoodId);
                Add(ref hash, (ulong)reservation.Count);
                Add(ref hash, (ulong)reservation.UnitPrice);
                Add(ref hash, (ulong)reservation.CreatedMonth);
                Add(ref hash, (ulong)reservation.ExpiresMonth);
                Add(ref hash, (byte)reservation.Status);
                Add(ref hash, (ulong)reservation.SourceResultId);
            }
        }
        Add(ref hash, (ulong)_lastMonsterRegenerationMonth);
        foreach (var population in _monsterPopulations)
        {
            Add(ref hash, (ulong)population);
        }
        var relations = _relations.CreateCheckpoint();
        Add(ref hash, (ulong)relations.HistorySequence);
        Add(ref hash, (ulong)relations.ResultSequence);
        Add(ref hash, (ulong)relations.Relations.Length);
        foreach (var relation in relations.Relations)
        {
            Add(ref hash, relation.Id.Value);
            Add(ref hash, (ulong)relation.LeftAgent);
            Add(ref hash, (ulong)relation.RightAgent);
            Add(ref hash, relation.LeftPersonId.Value);
            Add(ref hash, relation.RightPersonId.Value);
            AddRelationMetricsHash(ref hash, relation.LeftTowardRight);
            AddRelationMetricsHash(ref hash, relation.RightTowardLeft);
            Add(ref hash, (byte)relation.Roles);
            Add(ref hash, (ulong)(relation.MasterAgent + 1));
            Add(ref hash, relation.CooperativeInteractions);
            Add(ref hash, relation.JourneyInteractions);
            Add(ref hash, relation.CombatInteractions);
            Add(ref hash, (ulong)relation.TechniqueTeachingSessions);
            Add(ref hash, (ulong)relation.SpellTeachingSessions);
            Add(ref hash, relation.TechniqueTransmissionEstablished ? 1UL : 0UL);
            Add(ref hash, relation.SpellTransmissionEstablished ? 1UL : 0UL);
            Add(ref hash, relation.TechniqueKnowledgeRef.Value);
            Add(ref hash, relation.SpellKnowledgeRef.Value);
            var relationResult = relation.LatestInteractionResult;
            Add(ref hash, relationResult.Id.Value);
            Add(ref hash, (ulong)relationResult.Sequence);
            Add(ref hash, (ulong)relationResult.Tick);
            Add(ref hash, relationResult.RelationId.Value);
            Add(ref hash, (byte)relationResult.Status);
            Add(ref hash, relationResult.InteractionKind is { } interactionKind
                ? checked((ulong)(byte)interactionKind + 1UL)
                : 0UL);
            Add(ref hash, relationResult.TeachingResult is { } teachingResult
                ? checked((ulong)(byte)teachingResult + 1UL)
                : 0UL);
            Add(ref hash, relationResult.LeftPersonId.Value);
            Add(ref hash, relationResult.RightPersonId.Value);
            Add(ref hash, relationResult.SourceEventRecordId.Value);
            foreach (var history in relation.History)
            {
                Add(ref hash, (ulong)history.Sequence);
                Add(ref hash, (ulong)history.Tick);
                Add(ref hash, (byte)history.Kind);
                Add(ref hash, (byte)history.ChangedRoles);
                Add(ref hash, history.LeftPersonId.Value);
                Add(ref hash, history.RightPersonId.Value);
                Add(ref hash, history.TeachingTarget is { } target
                    ? checked((ulong)(byte)target + 1UL)
                    : 0UL);
                Add(ref hash, history.KnowledgeRef.Value);
                Add(ref hash, history.ProvenanceId.Value);
            }
        }
        for (var index = 0; index < Population; index++)
        {
            Add(ref hash, (ulong)index);
            AddString(ref hash, _agents.Names[index]);
            Add(ref hash, _agents.SpiritualRoots[index]);
            Add(ref hash, _agents.Bones[index]);
            Add(ref hash, _agents.Constitutions[index]);
            Add(ref hash, _agents.Comprehensions[index]);
            Add(ref hash, _agents.Minds[index]);
            Add(ref hash, _agents.Souls[index]);
            for (var component = 0; component < AptitudeRules.TotalComponentCount; component++)
            {
                Add(
                    ref hash,
                    _agents.AptitudeComponents[
                        (index * AptitudeRules.TotalComponentCount) + component]);
            }
            Add(ref hash, (ulong)_agents.BirthTicks[index]);
            Add(ref hash, (ulong)_agents.LifeNumbers[index]);
            Add(ref hash, _agents.SoulIdentityIds[index].Value);
            Add(ref hash, _agents.BodyIdentityIds[index].Value);
            Add(ref hash, _agents.PersonIdentityIds[index].Value);
            Add(ref hash, (byte)_agents.LifeStates[index]);
            Add(ref hash, (byte)_agents.Genders[index]);
            Add(ref hash, (byte)_agents.SoulEmbodimentStates[index]);
            Add(ref hash, (byte)_agents.BodyLifeStates[index]);
            Add(ref hash, (byte)_agents.CurrentLifePhases[index]);
            Add(ref hash, (ulong)_agents.LifeAuthorityRevisions[index]);
            var appearance = _agents.AppearanceStates[index];
            Add(ref hash, appearance.DefinitionId.Value);
            Add(ref hash, appearance.InnateBeautyHundredths);
            Add(ref hash, appearance.HeightHundredths);
            Add(ref hash, appearance.BuildHundredths);
            Add(ref hash, appearance.FacialFeatureHundredths);
            Add(ref hash, appearance.ComplexionHundredths);
            Add(ref hash, appearance.HairHundredths);
            Add(ref hash, appearance.BearingHundredths);
            Add(ref hash, unchecked((ulong)(long)appearance.PermanentModifierHundredths));
            Add(ref hash, appearance.ScarSeverityHundredths);
            Add(ref hash, (ulong)appearance.AuthorityRevision);
            var lifeTransition = _agents.LatestLifeTransitions[index];
            Add(ref hash, (byte)lifeTransition.Status);
            Add(ref hash, (byte)lifeTransition.Reason);
            Add(ref hash, lifeTransition.PersonId.Value);
            Add(ref hash, lifeTransition.BodyId.Value);
            Add(ref hash, (byte)lifeTransition.PreviousPhase);
            Add(ref hash, (byte)lifeTransition.CurrentPhase);
            Add(ref hash, (byte)lifeTransition.PreviousBodyState);
            Add(ref hash, (byte)lifeTransition.CurrentBodyState);
            Add(ref hash, (byte)lifeTransition.PreviousSoulState);
            Add(ref hash, (byte)lifeTransition.CurrentSoulState);
            Add(ref hash, (ulong)lifeTransition.Tick);
            Add(ref hash, (ulong)lifeTransition.AuthorityRevision);
            var decision = _agents.DecisionTraces[index];
            Add(ref hash, (ulong)_agents.DecisionAuthorityRevisions[index]);
            Add(ref hash, (ulong)decision.Tick);
            Add(ref hash, (ulong)decision.AuthorityRevision);
            Add(ref hash, (byte)decision.PrimaryGoal);
            Add(ref hash, (byte)decision.SupportingGoal);
            Add(ref hash, (byte)decision.SelectedActivity);
            Add(ref hash, (byte)decision.SelectedReason);
            Add(ref hash, (ulong)((int?)decision.AlternativeActivity + 1 ?? 0));
            Add(ref hash, (byte)decision.Resolution);
            Add(ref hash, (ulong)_agents.TeachingAuthorityRevisions[index]);
            AddTeachingTraceHash(ref hash, _agents.TeachingTraces[index]);
            Add(ref hash, (ulong)_agents.LearningAuthorityRevisions[index]);
            AddTeachingTraceHash(ref hash, _agents.LearningTraces[index]);
            Add(ref hash, _agents.TechniqueMisunderstandingPoints[index]);
            Add(ref hash, _agents.SpellMisunderstandingPoints[index]);
            Add(ref hash, _agents.BottleneckFailures[index]);
            Add(ref hash, (byte)_agents.RealmStages[index]);
            Add(ref hash, _agents.RealmLayers[index]);
            Add(ref hash, (ulong)_agents.CultivationProgress[index]);
            Add(ref hash, (ulong)_agents.PendingCultivationWork[index]);
            Add(ref hash, (ulong)_agents.LastCultivationWorkBefore[index]);
            Add(ref hash, (ulong)_agents.LastCultivationWorkGain[index]);
            Add(ref hash, (byte)_agents.CultivationStates[index]);
            Add(ref hash, (byte)_agents.CultivationBottleneckCauses[index]);
            Add(ref hash, _agents.FloorNineCompleted[index] ? 1UL : 0UL);
            Add(ref hash, _agents.GreatPerfectionAchieved[index] ? 1UL : 0UL);
            Add(ref hash, _agents.EntryGatePassMasks[index]);
            for (var gate = 0; gate < CultivationRules.EntryGateSlotCount; gate++)
            {
                Add(ref hash, _agents.EntryGateTemperings[
                    (index * CultivationRules.EntryGateSlotCount) + gate]);
            }
            Add(ref hash, (byte)_agents.FoundationRoutes[index]);
            for (var component = 0; component < CultivationRules.RealmComponentSlotCount; component++)
            {
                var cultivationOffset = (index * CultivationRules.RealmComponentSlotCount) + component;
                Add(ref hash, _agents.CultivationComponentValues[cultivationOffset]);
                Add(ref hash, _agents.CultivationComponentLimits[cultivationOffset]);
            }

            for (var formation = 0; formation < CultivationRules.FormationSlotCount; formation++)
            {
                Add(ref hash, _agents.FormationValues[(index * CultivationRules.FormationSlotCount) + formation]);
            }

            if (_agents.FoundationFormations.CreateCheckpoint(index) is { } foundationFormation)
            {
                Add(ref hash, 1UL);
                Add(ref hash, (ulong)foundationFormation.MethodRevision);
                Add(ref hash, (byte)foundationFormation.CompletedSteps);
                Add(ref hash, (byte)foundationFormation.Source.Route);
                Add(ref hash, (byte)foundationFormation.Source.Kind);
                Add(ref hash, foundationFormation.Source.LocationId);
                Add(ref hash, (ulong)foundationFormation.Source.SourceRevision);
                Add(ref hash, (ulong)foundationFormation.Source.FrozenTick);
                Add(ref hash, (ulong)foundationFormation.Source.SourceCount);
                Add(ref hash, (ulong)foundationFormation.Source.RemainingSupply);
                Add(ref hash, (byte)foundationFormation.Source.Availability);
            }
            else
            {
                Add(ref hash, 0UL);
            }

            var trace = _agents.CultivationTraces[index];
            Add(ref hash, (ulong)trace.Tick);
            Add(ref hash, (ulong)trace.FormulaRevision);
            Add(ref hash, (byte)trace.AttemptClass);
            Add(ref hash, (byte)trace.Outcome);
            Add(ref hash, (byte)trace.Realm);
            Add(ref hash, trace.SourceFloor);
            Add(ref hash, trace.DestinationFloor);
            Add(ref hash, (ulong)trace.WorkBefore);
            Add(ref hash, (ulong)trace.WorkGain);
            Add(ref hash, (ulong)trace.WorkAfter);
            Add(ref hash, (ulong)trace.RequiredWork);
            Add(ref hash, (ulong)trace.CarryoverWork);
            Add(ref hash, (ulong)trace.BaseChanceBasisPoints);
            Add(ref hash, unchecked((ulong)(long)trace.ModifierBasisPoints));
            Add(ref hash, (ulong)trace.FinalChanceBasisPoints);
            Add(ref hash, unchecked((ulong)(long)trace.RollBasisPoints));
            Add(ref hash, (byte)trace.Blocker);
            Add(ref hash, (byte)trace.FormulaKind);
            Add(ref hash, (ulong)trace.AuthorityRevisionBefore);
            Add(ref hash, (ulong)trace.AuthorityRevisionAfter);
            Add(ref hash, (ulong)trace.AttemptEffectRegistryRevision);
            Add(ref hash, (ulong)trace.CandidateEffectCount);
            Add(ref hash, (ulong)trace.SelectedEffectCount);
            Add(ref hash, (byte)trace.AttemptEffectAvailability);
            Add(ref hash, (byte)trace.FailureEffect);
            Add(ref hash, (byte)trace.FailureEffectAvailability);
            Add(ref hash, (byte)trace.RngDomain);
            Add(ref hash, trace.AttemptSnapshotFrozen ? 1UL : 0UL);
            Add(ref hash, (byte)trace.FoundationProcedureSteps);
            Add(ref hash, (ulong)trace.EnvironmentFactorBasisPoints);
            Add(ref hash, (byte)trace.EnvironmentAvailability);
            Add(ref hash, (byte)trace.LawUnderstandingAvailability);
            Add(ref hash, (byte)trace.LuckFateAvailability);
            Add(ref hash, (ulong)_agents.CultivationAuthorityRevisions[index]);
            var cultivationPlan = _agents.CultivationPlans[index];
            Add(ref hash, (ulong)cultivationPlan.AuthorityRevision);
            Add(ref hash, (ulong)cultivationPlan.StartedTick);
            Add(ref hash, (ulong)cultivationPlan.PlannedEndTick);
            Add(ref hash, (ulong)cultivationPlan.PlannedMonths);
            Add(ref hash, (ulong)cultivationPlan.CompletedMonths);
            Add(ref hash, cultivationPlan.LocationId);
            Add(ref hash, (byte)cultivationPlan.Intensity);
            Add(ref hash, (byte)cultivationPlan.ResourcePolicy);
            Add(ref hash, (ulong)cultivationPlan.SpiritStoneBudget);
            Add(ref hash, (ulong)cultivationPlan.SpiritStonesConsumed);
            Add(ref hash, (ulong)cultivationPlan.GrowthPillBudget);
            Add(ref hash, (ulong)cultivationPlan.GrowthPillsConsumed);
            Add(ref hash, (byte)cultivationPlan.FormationSupport);
            Add(ref hash, cultivationPlan.FormationKnowledgeHundredths);
            Add(ref hash, cultivationPlan.FormationInstallationExperienceHundredths);
            Add(ref hash, cultivationPlan.FormationInstallationSucceeded ? 1UL : 0UL);
            Add(ref hash, (byte)cultivationPlan.TeachingRole);
            Add(ref hash, unchecked((ulong)(long)cultivationPlan.TeachingPartnerAgent));
            Add(ref hash, (ulong)cultivationPlan.FrozenTeachingBonusBasisPoints);
            Add(ref hash, (ulong)cultivationPlan.FrozenConditionFactorBasisPoints);
            Add(ref hash, cultivationPlan.IsActive ? 1UL : 0UL);
            Add(ref hash, (ulong)_agents.CultivationExperience[index]);
            Add(ref hash, _agents.FormationInstallationExperience[index]);
            Add(ref hash, _agents.TechniqueQualities[index]);
            Add(ref hash, _agents.TechniqueMasteries[index]);
            Add(ref hash, (ulong)_agents.TechniqueTrainingExperience[index]);
            Add(ref hash, _agents.TechniqueUnderstanding[index]);
            Add(ref hash, _agents.SpellQualities[index]);
            Add(ref hash, _agents.SpellMasteries[index]);
            Add(ref hash, (ulong)_agents.SpellTrainingExperience[index]);
            Add(ref hash, _agents.SpellUnderstanding[index]);
            Add(ref hash, (ulong)_agents.CombatExperience[index]);
            Add(ref hash, _agents.CombatUnderstanding[index]);
            Add(ref hash, (ulong)_agents.Wins[index]);
            Add(ref hash, (ulong)_agents.Losses[index]);
            Add(ref hash, (ulong)_agents.Health[index]);
            Add(ref hash, (ulong)_agents.MaximumHealth[index]);
            Add(ref hash, (ulong)_agents.Qi[index]);
            Add(ref hash, (ulong)_agents.MaximumQi[index]);
            Add(ref hash, (byte)_agents.Activities[index]);
            Add(ref hash, (ulong)_agents.NextDecisionTicks[index]);
            Add(ref hash, (ulong)(_agents.LastActionMonths[index] + 1));
            Add(ref hash, (ulong)_agents.LastActionTicks[index]);
            Add(ref hash, (ulong)_agents.ActionsTaken[index]);
            Add(ref hash, (ulong)(_agents.Opponents[index] + 1));
            Add(ref hash, (ulong)(_agents.LastOpponents[index] + 1));
            Add(ref hash, (ulong)_agents.LastCombatTicks[index]);
            Add(ref hash, (ulong)_agents.CombatStartedTicks[index]);
            Add(ref hash, (ulong)_agents.CombatActions[index]);
            Add(ref hash, (ulong)(_agents.CombatTurnOwners[index] + 1));
            Add(ref hash, _agents.InjuryPoints[index]);
            Add(ref hash, _agents.Guarding[index] ? 1UL : 0UL);
            Add(ref hash, (ulong)_agents.ChallengesIssued[index]);
            Add(ref hash, (ulong)_agents.SpiritStones[index]);
            Add(ref hash, (ulong)_agents.GrowthPills[index]);
            Add(ref hash, (ulong)_agents.BreakthroughPills[index]);
            Add(ref hash, (ulong)_agents.MissionsCompleted[index]);
            Add(ref hash, _agents.ActiveMissionMaterials[index]);
            Add(ref hash, _agents.LocationIds[index]);
            Add(ref hash, _agents.TravelDestinationIds[index]);
            Add(ref hash, (ulong)_agents.MonsterHunts[index]);
            Add(ref hash, (ulong)_agents.MonsterVictories[index]);
            for (var material = 0; material < EconomyRules.MaterialCount; material++)
            {
                Add(ref hash, (ulong)_agents.MaterialInventory[(index * EconomyRules.MaterialCount) + material]);
            }

            for (var slot = 0; slot < EconomyRules.TreasureSlotCount; slot++)
            {
                Add(ref hash, _agents.EquippedTreasureQualities[(index * EconomyRules.TreasureSlotCount) + slot]);
            }
            Add(ref hash, (ulong)_rankByAgent[index]);
            Add(ref hash, (ulong)_realmRankByAgent[index]);
            Add(ref hash, (ulong)_aptitudeRankByAgent[index]);
            if (includeDecisionRankings)
            {
                Add(ref hash, (ulong)_wealthRankByAgent[index]);
            }
            foreach (var entry in _agents.PersonalLogs[index])
            {
                Add(ref hash, (ulong)entry.Sequence);
                Add(ref hash, (ulong)entry.Tick.Value);
                Add(ref hash, (ulong)entry.LifeNumber);
                Add(ref hash, (byte)entry.Kind);
                Add(ref hash, (ulong)((int?)entry.Activity + 1 ?? 0));
                Add(ref hash, (ulong)((int?)entry.WorldEventKind + 1 ?? 0));
                Add(ref hash, (ulong)((int?)entry.ChronicleEventKind + 1 ?? 0));
                Add(ref hash, entry.IsPrimaryActor ? 1UL : 0UL);
                Add(ref hash, (ulong)(entry.RelatedAgent?.Value + 1 ?? 0));
                Add(ref hash, unchecked((ulong)entry.Value));
            }

            if (includeDetailedChronicles)
            {
                foreach (var entry in _agents.Chronicles[index])
                {
                    Add(ref hash, (ulong)entry.Sequence);
                    Add(ref hash, (ulong)entry.Tick.Value);
                    Add(ref hash, (ulong)entry.LifeNumber);
                    Add(ref hash, (ulong)entry.AgeYears);
                    AddString(ref hash, entry.PersonName);
                    Add(ref hash, (byte)entry.Kind);
                    Add(ref hash, (byte)entry.RealmStage);
                    Add(ref hash, (byte)entry.MinorStage);
                    Add(ref hash, unchecked((ulong)(long)entry.Value));
                }
            }
        }

        foreach (var worldEvent in _events)
        {
            Add(ref hash, (ulong)worldEvent.Sequence);
            Add(ref hash, (ulong)worldEvent.Tick.Value);
            Add(ref hash, (byte)worldEvent.Kind);
            Add(ref hash, (ulong)worldEvent.PrimaryAgent.Value);
            Add(ref hash, (ulong)(worldEvent.SecondaryAgent?.Value + 1 ?? 0));
            Add(ref hash, unchecked((ulong)(long)worldEvent.Value));
            AddString(ref hash, worldEvent.PrimaryName ?? string.Empty);
            AddString(ref hash, worldEvent.SecondaryName ?? string.Empty);
        }


        AddWorldCenturyChronicleHash(ref hash, _worldChronicle.CreateCheckpoint());

        return hash;
    }

    private static void Add(ref ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= 1099511628211UL;
        }
    }

    private static void AddRelationMetricsHash(ref ulong hash, RelationMetrics metrics)
    {
        Add(ref hash, unchecked((ulong)(long)metrics.FavorDisfavor));
        Add(ref hash, unchecked((ulong)(long)metrics.TrustDistrust));
        Add(ref hash, unchecked((ulong)(long)metrics.RespectContempt));
        Add(ref hash, (ulong)metrics.Gratitude);
        Add(ref hash, (ulong)metrics.Grievance);
        Add(ref hash, (ulong)metrics.Vigilance);
    }

    private static void AddTeachingTraceHash(ref ulong hash, TeachingTraceState teaching)
    {
        Add(ref hash, (ulong)teaching.AuthorityRevision);
        Add(ref hash, teaching.IsActive ? 1UL : 0UL);
        Add(ref hash, (ulong)(teaching.LearnerAgent + 1));
        Add(ref hash, teaching.TeacherPersonId.Value);
        Add(ref hash, teaching.LearnerPersonId.Value);
        Add(ref hash, teaching.RelationId.Value);
        Add(ref hash, (byte)teaching.TargetKind);
        Add(ref hash, teaching.SourceQuality);
        Add(ref hash, teaching.TargetKnowledgeRef.Value);
        Add(ref hash, teaching.ProvenanceId.Value);
        Add(ref hash, (ulong)teaching.StartedTick);
        Add(ref hash, (ulong)teaching.LastResultTick);
        Add(ref hash, (byte)teaching.LastResult);
        Add(ref hash, (ulong)teaching.LastGain);
        Add(ref hash, (ulong)teaching.SessionsCompleted);
        Add(ref hash, teaching.MisunderstandingPoints);
        Add(ref hash, teaching.TransmissionEstablished ? 1UL : 0UL);
    }

    private static void AddString(ref ulong hash, string value)
    {
        foreach (var character in value)
        {
            Add(ref hash, character);
        }

        Add(ref hash, 0xFFFFUL);
    }

    private static void AddWorldCenturyChronicleHash(
        ref ulong hash,
        WorldCenturyChronicleCheckpoint checkpoint)
    {
        Add(ref hash, (ulong)checkpoint.Sequence);
        foreach (var entry in checkpoint.Entries)
        {
            Add(ref hash, (ulong)entry.Sequence);
            Add(ref hash, (ulong)entry.CenturyNumber);
            Add(ref hash, (ulong)entry.StartYear);
            Add(ref hash, (ulong)entry.EndYear);
            AddArray(ref hash, entry.RealmBreakthroughCounts);
            AddArray(ref hash, entry.RealmBreakthroughUniqueCounts);
            AddArray(ref hash, entry.CombatPowerPositionChanges);
            AddArray(ref hash, entry.RealmPositionChanges);
            AddArray(ref hash, entry.AptitudePositionChanges);
            AddArray(ref hash, entry.WealthPositionChanges);
            AddLeader(ref hash, entry.CombatPowerLeader);
            AddLeader(ref hash, entry.RealmLeader);
            AddLeader(ref hash, entry.AptitudeLeader);
            AddLeader(ref hash, entry.WealthLeader);
            Add(ref hash, (ulong)entry.DeathCount);
            Add(ref hash, (ulong)entry.UniqueDeathCount);
            Add(ref hash, (ulong)entry.ReincarnationEntryCount);
            Add(ref hash, (ulong)entry.UniqueReincarnationEntryCount);
            Add(ref hash, (ulong)entry.RebirthCount);
            Add(ref hash, (ulong)entry.UniqueRebirthCount);
            Add(ref hash, (ulong)entry.ReincarnationRiverResidentsAtEnd);
            Add(ref hash, (ulong)entry.CombatsCompleted);
            Add(ref hash, (ulong)entry.MonsterHunts);
            Add(ref hash, (ulong)entry.MissionsCompleted);
            Add(ref hash, (ulong)entry.SpiritStonesMined);
            Add(ref hash, (ulong)entry.TreasuresCrafted);
            Add(ref hash, (ulong)entry.PillsCrafted);
            Add(ref hash, (ulong)entry.SecretRealmsDiscovered);
            Add(ref hash, (ulong)entry.UniqueSecretRealmDiscoverers);
        }

        var current = checkpoint.Current;
        Add(ref hash, (ulong)current.CenturyIndex);
        AddArray(ref hash, current.RealmBreakthroughCounts);
        AddArray(ref hash, current.RealmBreakthroughUniqueCounts);
        foreach (var bits in current.RealmBreakthroughAgentBits)
        {
            AddArray(ref hash, bits);
        }
        AddArray(ref hash, current.CombatPowerPositionChanges);
        AddArray(ref hash, current.RealmPositionChanges);
        AddArray(ref hash, current.AptitudePositionChanges);
        AddArray(ref hash, current.WealthPositionChanges);
        AddArray(ref hash, current.PreviousCombatPowerTop);
        AddArray(ref hash, current.PreviousRealmTop);
        AddArray(ref hash, current.PreviousAptitudeTop);
        AddArray(ref hash, current.PreviousWealthTop);
        AddLeader(ref hash, current.CombatPowerLeader);
        AddLeader(ref hash, current.RealmLeader);
        AddLeader(ref hash, current.AptitudeLeader);
        AddLeader(ref hash, current.WealthLeader);
        Add(ref hash, unchecked((ulong)current.LastRankingSampleYear));
        Add(ref hash, (ulong)current.DeathCount);
        Add(ref hash, (ulong)current.UniqueDeathCount);
        AddArray(ref hash, current.DeathAgentBits);
        Add(ref hash, (ulong)current.ReincarnationEntryCount);
        Add(ref hash, (ulong)current.UniqueReincarnationEntryCount);
        AddArray(ref hash, current.ReincarnationEntryAgentBits);
        Add(ref hash, (ulong)current.RebirthCount);
        Add(ref hash, (ulong)current.UniqueRebirthCount);
        AddArray(ref hash, current.RebirthAgentBits);
        Add(ref hash, (ulong)current.ReincarnationRiverResidents);
        Add(ref hash, (ulong)current.CombatsCompleted);
        Add(ref hash, (ulong)current.MonsterHunts);
        Add(ref hash, (ulong)current.MissionsCompleted);
        Add(ref hash, (ulong)current.SpiritStonesMined);
        Add(ref hash, (ulong)current.TreasuresCrafted);
        Add(ref hash, (ulong)current.PillsCrafted);
        Add(ref hash, (ulong)current.SecretRealmsDiscovered);
        Add(ref hash, (ulong)current.UniqueSecretRealmDiscoverers);
        AddArray(ref hash, current.SecretRealmDiscovererBits ?? []);
    }

    private static void AddLeader(ref ulong hash, WorldChronicleLeader leader)
    {
        Add(ref hash, (ulong)(leader.AgentId.Value + 1));
        AddString(ref hash, leader.Name ?? string.Empty);
    }

    private static void AddArray(ref ulong hash, IEnumerable<int> values)
    {
        foreach (var value in values)
        {
            Add(ref hash, unchecked((ulong)(long)value));
        }
    }

    private static void AddArray(ref ulong hash, IEnumerable<long> values)
    {
        foreach (var value in values)
        {
            Add(ref hash, unchecked((ulong)value));
        }
    }

    private static void AddArray(ref ulong hash, IEnumerable<ulong> values)
    {
        foreach (var value in values)
        {
            Add(ref hash, value);
        }
    }
}
