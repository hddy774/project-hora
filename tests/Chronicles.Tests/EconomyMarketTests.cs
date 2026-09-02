using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;

internal static class EconomyMarketTests
{
    public static void EconomyLoop()
    {
        Equal(2, EconomyRules.AllMaterials.Count, "간략화 재료 수");
        Equal(1, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Treasure), "법보 재료 수");
        Equal(1, EconomyRules.AllMaterials.Count(item => item.Category == MaterialCategory.Pill), "단약 재료 수");
        Equal(3, EconomyRules.TreasureSlotCount, "본명·방어·공격 법보 슬롯 수");
        Equal(44, EconomyRules.MarketGoodCount, "대경지별 완제품 포함 시장 상품 수");
        True(
            EconomyRules.GetPillGoodId(PillKind.Growth, RealmStage.QiRefining)
                != EconomyRules.GetPillGoodId(PillKind.Growth, RealmStage.FoundationEstablishment),
            "성장 단약은 대경지별 별도 상품");
        var high7999 = EconomyRules.GetQualityPriceMultiplierBasisPoints(new QualityValue(7_999));
        var extreme8000 = EconomyRules.GetQualityPriceMultiplierBasisPoints(new QualityValue(8_000));
        var extreme8100 = EconomyRules.GetQualityPriceMultiplierBasisPoints(new QualityValue(8_100));
        Equal(high7999 * 2, extreme8000, "79.99→80.00 가격 격차는 품질 효과보다 2배 확대");
        Equal(extreme8000 * 2, extreme8100, "80 이상 품질 1.00마다 가격 2배");

        var world = World.Create(707, 2_000);
        var initialMarket = world.CreateSnapshot(1, 0).Market;
        Equal(EconomyRules.MarketGoodCount, initialMarket.Goods.Count, "시장 상품 수");
        True(
            initialMarket.Goods.Where(item => item.AverageQuality is null)
                .All(item => item.CurrentPrice == item.CityBasePrice),
            "품질 없는 초기 상품 가격은 도시 기준가");
        world.AdvanceTo(new SimulationTick(24_000));
        var diagnosticMarket = world.CreateSnapshot(1, 0).Market;
        Console.WriteLine(
            $"ECONOMY PRODUCTION: 단약 {diagnosticMarket.PillsProduced:N0} · 법보 {diagnosticMarket.TreasuresProduced:N0} · " +
            $"공법서 {diagnosticMarket.TechniqueBooksProduced:N0} · 법술서 {diagnosticMarket.SpellBooksProduced:N0} · " +
            $"대기 {diagnosticMarket.ProductionQueue?.Count ?? 0:N0} · 재료 {diagnosticMarket.MaterialStock:N0} · " +
            $"도시 영석 {diagnosticMarket.TreasurySpiritStones:N0} · " +
            $"납품 {diagnosticMarket.MissionDeliveries:N0} · 판매 {diagnosticMarket.SaleTransactions:N0}/" +
            $"{world.TotalMaterialsSold:N0}");
        True(world.TotalMissionsCompleted > 0, "도시 임무가 완료되어야 합니다.");
        True(world.TotalTreasuresCrafted > 0, "법보 제작이 발생해야 합니다.");
        True(world.TotalPillsCrafted > 0, "단약 제작이 발생해야 합니다.");
        True(world.TotalMaterialsSold > 0, "재료 판매가 발생해야 합니다.");
        var market = world.CreateSnapshot(1, 0).Market;
        var techniqueMarket = market.Goods.Single(item => item.Kind == MarketGoodKind.TechniqueBook);
        var spellMarket = market.Goods.Single(item => item.Kind == MarketGoodKind.SpellBook);
        Console.WriteLine(
            $"ECONOMY: 공법서 재고 {techniqueMarket.Stock:N0}·누적 {techniqueMarket.Supply:N0}/{techniqueMarket.Demand:N0}·최근 {techniqueMarket.RecentSupply:N0}/{techniqueMarket.RecentDemand:N0} · "
            + $"법술서 재고 {spellMarket.Stock:N0}·누적 {spellMarket.Supply:N0}/{spellMarket.Demand:N0}·최근 {spellMarket.RecentSupply:N0}/{spellMarket.RecentDemand:N0}");
        True(market.TreasurySpiritStones >= 0, "도시 시장 영석은 음수가 될 수 없습니다.");
        True(market.Goods.All(item => item.Stock >= 0), "시장 재고는 음수가 될 수 없습니다.");
        True(market.Goods.Any(item => item.Supply > 0), "임무 납품·판매·도시 생산이 공급으로 기록되어야 합니다.");
        True(market.Goods.Any(item => item.Demand > 0), "구매·도시 생산 소비가 수요로 기록되어야 합니다.");
        True(
            market.Goods.Any(item => item.CurrentPrice != item.CityBasePrice),
            "공급과 수요에 따라 시장 가격이 변동해야 합니다.");
        True(
            techniqueMarket.Supply > 0,
            "도시는 영석으로 공법서를 생산해야 합니다.");
        True(
            spellMarket.Supply > 0,
            "도시는 영석으로 법술서를 생산해야 합니다.");
        True(market.PurchaseTransactions > 0, "도시 시장 구매 거래 누계");
        True(market.SaleTransactions > 0, "도시 시장 판매 거래 누계");
        Equal(
            (long)world.TotalMissionsCompleted,
            market.MissionDeliveries + market.MiningMissionCount,
            "재료 납품·채굴 임무 누계");
        True(market.MaterialsProcessed > 0, "도시 가공 재료 누계");
        True(market.PillsProduced > 0, "도시 단약 생산 누계");
        True(market.TreasuresProduced > 0, "도시 법보 생산 누계");
        True(market.TechniqueBooksProduced > 0, "도시 공법서 생산 누계");
        True(market.SpellBooksProduced > 0, "도시 법술서 생산 누계");
        True(market.SpiritStonesMined > 0, "도시 유한 광맥 채굴 누계");

        var agentsWithEquipment = 0;
        var agentsWithPills = 0;
        var agentsWithMissionHistory = 0;
        for (var index = 0; index < world.Population; index++)
        {
            var agent = world.GetAgentSnapshot(new AgentId(index));
            Equal(3, agent.EquippedTreasures.Count, "개별 법보 슬롯 수");
            True(agent.TechniqueQuality.Hundredths <= 7_999, "시작 공법 품질 상한 79.99");
            True(agent.SpellQuality.Hundredths <= 7_999, "시작 법술 품질 상한 79.99");
            Equal(agent.InventoryItemCount, agent.Materials.Sum(item => item.Count), "개별 재료 상세 합계");
            if (agent.EquippedTreasures.Any(item => item.Quality is not null))
            {
                agentsWithEquipment++;
                True(agent.CombatPower.Treasure > 0, "장착 법보는 전투력에 반영되어야 합니다.");
            }

            if (agent.GrowthPills > 0 || agent.BreakthroughPills > 0)
            {
                agentsWithPills++;
            }

            if (agent.MissionsCompleted > 0)
            {
                agentsWithMissionHistory++;
            }
        }

        True(agentsWithEquipment > 0, "법보를 장착한 AI가 있어야 합니다.");
        True(agentsWithPills > 0, "단약을 보유한 AI가 있어야 합니다.");
        True(agentsWithMissionHistory > 0, "임무 이력이 있는 AI가 있어야 합니다.");
    }

    public static void ProductionSkipsGoodsWithoutResources()
    {
        var world = World.Create(708, 1);
        var checkpoint = world.CreateCheckpoint();
        var stock = (int[])checkpoint.Market.Stock.Clone();
        var demand = (long[])checkpoint.Market.Demand.Clone();
        var supply = (long[])checkpoint.Market.Supply.Clone();
        var qualities = new long[EconomyRules.MarketGoodCount];
        Array.Clear(stock);
        Array.Clear(demand);
        Array.Clear(supply);

        var blockedTreasure = EconomyRules.GetTreasureGoodId(
            TreasureSlot.AttackTreasure,
            RealmStage.QiRefining);
        demand[blockedTreasure] = 1_000;
        demand[EconomyRules.TechniqueBookGoodId] = 10;
        var recentDemand = new long[EconomyRules.MarketGoodCount];
        recentDemand[blockedTreasure] = 1_000;
        recentDemand[EconomyRules.TechniqueBookGoodId] = 10;
        var demandBuckets = new long[
            EconomyRules.MarketGoodCount * EconomyRules.MarketFlowWindowMonths];
        recentDemand.CopyTo(demandBuckets, 0);
        var prices = new long[EconomyRules.MarketGoodCount];
        for (var goodId = 0; goodId < prices.Length; goodId++)
        {
            prices[goodId] = EconomyRules.GetMarketPrice(
                EconomyRules.GetMarketGood(goodId),
                stock[goodId],
                supply[goodId],
                demand[goodId],
                0,
                recentDemand[goodId]);
        }

        var market = checkpoint.Market with
        {
            TreasurySpiritStones = 10_000,
            Stock = stock,
            Prices = prices,
            Demand = demand,
            Supply = supply,
            QualityHundredths = qualities,
            RecentSupply = new long[EconomyRules.MarketGoodCount],
            RecentDemand = recentDemand,
            SupplyWindowBuckets = new long[
                EconomyRules.MarketGoodCount * EconomyRules.MarketFlowWindowMonths],
            DemandWindowBuckets = demandBuckets,
            CurrentFlowBucket = 0,
            ProductionQueue = [],
            LastIndustryMonth = 0,
        };
        world = World.Restore(checkpoint with { Market = market });
        world.AdvanceTo(GameCalendar.GetMonthStart(1));

        var production = world.CreateSnapshot(1, 0).Market.ProductionQueue ?? [];
        True(production.Count > 0, "재료가 필요 없는 생산은 계속 시작되어야 합니다.");
        True(
            production.Any(item => item.Kind == MarketGoodKind.TechniqueBook),
            "재료 부족 법보 수요가 공법서 생산을 굶기면 안 됩니다.");
        True(
            production.All(item => item.GoodId != blockedTreasure),
            "재료가 없는 상품은 생산 대기열에 들어가면 안 됩니다.");
    }

    public static void MarketSettlementAtomicity()
    {
        var market = new CityMarket(2);
        var initial = market.CreateSnapshot();
        var materialId = (int)MaterialId.TreasureMaterial;
        var initialMaterial = initial.Goods[materialId];

        Throws<ArgumentOutOfRangeException>(
            () => market.SettlePurchase(0, 0, 0, materialId, 1, long.MaxValue),
            "source result id 0 거부");
        var insufficient = market.SettlePurchase(0, 1, 0, materialId, 1, 0);
        True(insufficient.Status == MarketSettlementStatus.InsufficientFunds, "부족 자금 결과");
        var afterInsufficient = market.CreateSnapshot();
        Equal(initial.TreasurySpiritStones, afterInsufficient.TreasurySpiritStones, "실패 시 도시 영석 불변");
        Equal(initialMaterial.Stock, afterInsufficient.Goods[materialId].Stock, "실패 시 재고 불변");
        Equal(initialMaterial.Demand, afterInsufficient.Goods[materialId].Demand, "구매력 없는 시도는 수요가 아님");
        Equal(-1L, market.CreateCheckpoint().LastSettlementSourceByAgent![0], "비변경 실패는 source를 소비하지 않음");

        var settled = market.SettlePurchase(0, 1, 0, materialId, 1, long.MaxValue);
        True(settled.IsSettled, "같은 source의 자금 보충 재시도 정산");
        Equal(initialMaterial.Stock - 1, market.CreateSnapshot().Goods[materialId].Stock, "정상 구매 재고 1 이동");
        var duplicate = market.SettlePurchase(0, 1, 0, materialId, 1, long.MaxValue);
        True(duplicate.Status == MarketSettlementStatus.DuplicateSource, "정산된 동일 source 중복 거부");
        Equal(initialMaterial.Stock - 1, market.CreateSnapshot().Goods[materialId].Stock, "중복 정산 자산 이동 0회");

        var bookId = EconomyRules.TechniqueBookGoodId;
        var beforeBook = market.CreateSnapshot().Goods[bookId];
        var sameQuality = market.SettlePurchase(
            1,
            1,
            0,
            bookId,
            1,
            long.MaxValue,
            new QualityValue(EconomyRules.InitialSkillMaximumHundredths));
        True(sameQuality.Status == MarketSettlementStatus.NotAnImprovement, "동일 품질 서적 구매 거부");
        var afterRejectedBook = market.CreateSnapshot().Goods[bookId];
        Equal(beforeBook.Stock, afterRejectedBook.Stock, "개선 없는 서적은 사라지지 않음");
        Equal(beforeBook.Demand, afterRejectedBook.Demand, "개선 없는 서적은 인공 수요를 만들지 않음");
        Equal(-1L, market.CreateCheckpoint().LastSettlementSourceByAgent![1], "개선 없는 서적은 source를 소비하지 않음");

        var betterBook = market.SettlePurchase(
            1,
            1,
            0,
            bookId,
            1,
            long.MaxValue,
            new QualityValue(7_000));
        True(betterBook.IsSettled, "더 좋은 서적만 구매 정산");
        Equal(2L, market.CreateSnapshot().PurchaseTransactions, "실제 구매만 거래 누계에 포함");
        Equal(2, market.CreateSnapshot().RecentTrades?.Count ?? 0, "정산된 실물 거래 원장");

        var emptyTreasuryMarket = new CityMarket(
            new CityMarket(1).CreateCheckpoint() with { TreasurySpiritStones = 0 },
            WorldCheckpoint.CurrentSchemaVersion,
            1);
        var treasuryDenied = emptyTreasuryMarket.SettleSale(
            0,
            1,
            0,
            materialId,
            1,
            0);
        True(
            treasuryDenied.Status == MarketSettlementStatus.CityTreasuryInsufficient,
            "도시 자금 부족 판매 거부");
        Equal(
            -1L,
            emptyTreasuryMarket.CreateCheckpoint().LastSettlementSourceByAgent![0],
            "도시 자금 부족은 source를 소비하지 않음");
        True(emptyTreasuryMarket.TryCollectCraftingFee(100, 100), "도시 자금 보충");
        True(
            emptyTreasuryMarket.SettleSale(0, 1, 0, materialId, 1, 0).IsSettled,
            "같은 source의 도시 자금 보충 재시도 정산");
    }

    public static void MarketOfferReservationLifecycle()
    {
        var market = new CityMarket(2);
        var goodId = (int)MaterialId.TreasureMaterial;
        var initialStock = market.GetStock(goodId);
        True(initialStock >= 2, "예약 테스트 초기 재고");

        var unavailableChannel = market.ReserveCityOffer(
            0,
            goodId,
            1,
            long.MaxValue,
            currentMonth: 0,
            sourceResultId: 1,
            MarketTradeChannel.SectInternal);
        True(
            unavailableChannel.Status == MarketContractStatus.ChannelUnavailable,
            "조직 권리 없는 내부 시장은 명시적으로 비활성");

        var reserved = market.ReserveCityOffer(
            0,
            goodId,
            1,
            long.MaxValue,
            currentMonth: 0,
            sourceResultId: 1);
        True(reserved.IsReserved, "도시 판매 제안 예약");
        True(reserved.OfferId is not null, "안정 Offer ID");
        True(reserved.ReservationId is not null, "안정 Reservation ID");
        Equal(initialStock, market.GetStock(goodId), "예약은 소유권을 아직 이전하지 않음");

        var reservedSnapshot = market.CreateSnapshot();
        True(
            reservedSnapshot.Offers!.Any(item => item.Id == reserved.OfferId),
            "실제 권위 Offer 조회");
        True(
            reservedSnapshot.Reservations!.Any(item =>
                item.Id == reserved.ReservationId
                && item.Status == MarketReservationStatus.Reserved),
            "실제 권위 Reservation 조회");

        market.AdvanceIndustry(EconomyRules.ReservationLifetimeMonths);
        var expired = market.CreateSnapshot();
        True(
            expired.Reservations!.Any(item =>
                item.Id == reserved.ReservationId
                && item.Status == MarketReservationStatus.Expired),
            "만료 월에 예약 해제");
        Equal(
            0,
            market.CreateCheckpoint().Authority!.ReservedStock[goodId],
            "만료 후 예약 재고 0");

        var second = market.ReserveCityOffer(
            1,
            goodId,
            1,
            long.MaxValue,
            currentMonth: EconomyRules.ReservationLifetimeMonths,
            sourceResultId: 2);
        True(second.IsReserved, "만료 뒤 새 예약");
        var settlementTick = GameCalendar
            .GetMonthStart(EconomyRules.ReservationLifetimeMonths)
            .Value;
        var settled = market.SettleReservedPurchase(
            1,
            second.ReservationId!.Value,
            sourceResultId: 2,
            settlementTick,
            long.MaxValue);
        True(settled.IsSettled, "예약 Preflight 뒤 원자 체결");
        Equal(initialStock - 1, market.GetStock(goodId), "체결 시점 소유권 이전");
        True(
            market.CreateSnapshot().Reservations!.Any(item =>
                item.Id == second.ReservationId
                && item.Status == MarketReservationStatus.Settled),
            "체결 provenance 보존");

        var checkpoint = market.CreateCheckpoint();
        var restored = new CityMarket(checkpoint, WorldCheckpoint.CurrentSchemaVersion, 2);
        True(
            restored.CreateCheckpoint().Authority!.Reservations
                .Any(item => item.ReservationId == second.ReservationId.Value.Value),
            "Offer/Reservation 저장 복원");
    }

    public static void MarketOwnerCapacityGuard()
    {
        var saleMarket = new CityMarket(1);
        var saleBefore = saleMarket.CreateCheckpoint();
        var sale = saleMarket.SettleSale(
            0,
            1,
            0,
            (int)MaterialId.TreasureMaterial,
            1,
            long.MaxValue);
        True(sale.Status == MarketSettlementStatus.CapacityExceeded, "판매 대금 소유자 용량 초과 거부");
        EqualMarketCheckpoint(saleBefore, saleMarket.CreateCheckpoint(), "판매 용량 실패 시장 불변");

        var missionMarket = new CityMarket(2);
        var missionDemand = missionMarket.SettlePurchase(
            0,
            1,
            0,
            (int)MaterialId.PillMaterial,
            3,
            long.MaxValue);
        True(missionDemand.Status == MarketSettlementStatus.OutOfStock, "임무 용량 검사 준비 수요 기록");
        var demandRecorded = missionMarket.CreateCheckpoint();
        Equal(1L, demandRecorded.LastSettlementSourceByAgent![0], "재고 부족 수요 기록은 source 소비");
        True(
            missionMarket.SettlePurchase(
                0,
                1,
                0,
                (int)MaterialId.PillMaterial,
                3,
                long.MaxValue).Status == MarketSettlementStatus.DuplicateSource,
            "재고 부족 수요의 동일 source 중복 거부");
        EqualMarketCheckpoint(demandRecorded, missionMarket.CreateCheckpoint(), "중복 재고 부족 수요 불변");
        var missionBefore = missionMarket.CreateCheckpoint();
        var mission = missionMarket.SettleMissionDelivery(
            1,
            1,
            0,
            MaterialId.PillMaterial,
            EconomyRules.MissionRequiredItems,
            0,
            long.MaxValue);
        True(mission.Status == MarketSettlementStatus.CapacityExceeded, "임무 보상 소유자 용량 초과 거부");
        EqualMarketCheckpoint(missionBefore, missionMarket.CreateCheckpoint(), "임무 용량 실패 시장 불변");

        var miningMarket = new CityMarket(1);
        var miningBefore = miningMarket.CreateCheckpoint();
        True(
            !miningMarket.TryCompleteMining(
                RealmStage.QiRefining,
                long.MaxValue,
                out var miningReward,
                out var extracted),
            "채굴 보상 소유자 용량 초과 거부");
        Equal(0L, miningReward, "용량 실패 채굴 보상 0");
        Equal(0L, extracted, "용량 실패 채굴량 0");
        EqualMarketCheckpoint(miningBefore, miningMarket.CreateCheckpoint(), "채굴 용량 실패 시장 불변");
    }

    public static void MarketFlowWindowAndSnapshotCache()
    {
        var market = new CityMarket(1);
        _ = market.SettlePurchase(
            0,
            1,
            0,
            (int)MaterialId.PillMaterial,
            1,
            long.MaxValue);
        var first = market.CreateSnapshot();
        var second = market.CreateSnapshot();
        True(ReferenceEquals(first, second), "동일 authority revision은 시장 snapshot 재사용");
        True(first.RecentDemand > 0, "최근 수요가 현재 관측 창에 기록됨");

        market.AdvanceIndustry(EconomyRules.MarketFlowWindowMonths);
        var expired = market.CreateSnapshot();
        var material = expired.Goods[(int)MaterialId.PillMaterial];
        Equal(0L, material.RecentDemand, "12개월이 지난 수요는 현재 창에서 만료");
        True(material.Demand > 0, "감사·통계용 누적 수요는 보존");
        True(!ReferenceEquals(first, expired), "authority 변경 시 snapshot 갱신");
    }

    public static void MarketSchema24CodecAndSourceReplay()
    {
        const long purchaseSource = 1;
        const long saleSource = 2;
        var market = new CityMarket(1);
        var purchase = market.SettlePurchase(
            0,
            purchaseSource,
            0,
            (int)MaterialId.PillMaterial,
            1,
            long.MaxValue);
        True(purchase.IsSettled, "codec 준비 구매 정산");
        var sale = market.SettleSale(
            0,
            saleSource,
            0,
            (int)MaterialId.TreasureMaterial,
            1,
            0);
        True(sale.IsSettled, "codec 준비 판매 정산");
        market.AdvanceIndustry(5);

        var baseWorld = World.Create(710, 1);
        baseWorld.AdvanceTo(GameCalendar.GetMonthStart(5));
        var sourceWorld = World.Restore(baseWorld.CreateCheckpoint() with
        {
            Market = market.CreateCheckpoint(),
        });
        var sourceCheckpoint = sourceWorld.CreateCheckpoint();
        var restored = World.Restore(WorldSaveCodec.Decode(
            WorldSaveCodec.Encode(sourceCheckpoint)));
        Equal(sourceWorld.ComputeStateHash(), restored.ComputeStateHash(), "schema 24 시장 codec 상태 hash");

        var restoredMarketCheckpoint = restored.CreateCheckpoint().Market;
        EqualMarketCheckpoint(
            sourceCheckpoint.Market,
            restoredMarketCheckpoint,
            "schema 24 시장 codec 전체 필드");
        Equal(
            saleSource,
            restoredMarketCheckpoint.LastSettlementSourceByAgent![0],
            "마지막 source watermark 왕복");
        Equal(3L, restoredMarketCheckpoint.NextTradeSequence, "다음 거래 sequence 왕복");
        Equal(2, restoredMarketCheckpoint.RecentTrades!.Length, "최근 거래 원장 왕복");
        Equal(
            purchaseSource,
            restoredMarketCheckpoint.RecentTrades[0].SourceResultId,
            "구매 거래 source 원장 왕복");
        Equal(
            saleSource,
            restoredMarketCheckpoint.RecentTrades[1].SourceResultId,
            "판매 거래 source 원장 왕복");
        True(
            restoredMarketCheckpoint.RecentDemand!.Any(value => value > 0)
            && restoredMarketCheckpoint.DemandWindowBuckets!.Any(value => value > 0),
            "최근 수요와 bucket 왕복");
        True(
            restoredMarketCheckpoint.RecentSupply!.Any(value => value > 0)
            && restoredMarketCheckpoint.SupplyWindowBuckets!.Any(value => value > 0),
            "최근 공급과 bucket 왕복");

        var replayMarket = new CityMarket(
            restoredMarketCheckpoint,
            WorldCheckpoint.CurrentSchemaVersion,
            1);
        var replayBefore = replayMarket.CreateCheckpoint();
        var duplicate = replayMarket.SettleSale(
            0,
            saleSource,
            0,
            (int)MaterialId.TreasureMaterial,
            1,
            0);
        True(duplicate.Status == MarketSettlementStatus.DuplicateSource, "복원 뒤 동일 source 재정산 거부");
        EqualMarketCheckpoint(replayBefore, replayMarket.CreateCheckpoint(), "복원 뒤 중복 source 시장 불변");

        replayMarket.AdvanceIndustry(EconomyRules.MarketFlowWindowMonths);
        var expired = replayMarket.CreateCheckpoint();
        Equal(0L, expired.RecentDemand!.Sum(), "복원한 수요 bucket 12개월 경계 만료");
        Equal(0L, expired.RecentSupply!.Sum(), "복원한 공급 bucket 12개월 경계 만료");

        var invalidTrades = restoredMarketCheckpoint.RecentTrades.ToArray();
        invalidTrades[0] = invalidTrades[0] with { SourceResultId = 0 };
        Throws<InvalidDataException>(
            () => World.Restore(sourceCheckpoint with
            {
                Market = restoredMarketCheckpoint with { RecentTrades = invalidTrades },
            }),
            "0 source 거래 원장 거부");

        var invalidWatermarks = restoredMarketCheckpoint.LastSettlementSourceByAgent.ToArray();
        invalidWatermarks[0] = 0;
        Throws<InvalidDataException>(
            () => World.Restore(sourceCheckpoint with
            {
                Market = restoredMarketCheckpoint with
                {
                    LastSettlementSourceByAgent = invalidWatermarks,
                },
            }),
            "0 source watermark 거부");

        var invalidPrices = restoredMarketCheckpoint.Prices.ToArray();
        invalidPrices[(int)MaterialId.PillMaterial]++;
        Throws<InvalidDataException>(
            () => World.Restore(sourceCheckpoint with
            {
                Market = restoredMarketCheckpoint with { Prices = invalidPrices },
            }),
            "현재 수급 공식과 다른 저장 가격 거부");
    }

    public static void MarketCheckpointValidation()
    {
        var world = World.Create(709, 1);
        var checkpoint = world.CreateCheckpoint();
        var depleted = checkpoint with
        {
            Market = checkpoint.Market with { RemainingSpiritStoneOre = 0 },
        };
        var restored = World.Restore(depleted);
        Equal(0L, restored.CreateSnapshot(1, 0).Market.RemainingSpiritStoneOre, "고갈 광맥 0 영구 보존");

        var schema23Market = checkpoint.Market with
        {
            RecentSupply = null,
            RecentDemand = null,
            SupplyWindowBuckets = null,
            DemandWindowBuckets = null,
            CurrentFlowBucket = 0,
            AuthorityRevision = 0,
            LastSettlementSourceByAgent = null,
            RecentTrades = null,
            NextTradeSequence = 0,
        };
        var migrated = World.Restore(checkpoint with
        {
            SchemaVersion = 23,
            Market = schema23Market,
        }).CreateCheckpoint();
        Equal(WorldCheckpoint.CurrentSchemaVersion, migrated.SchemaVersion, "schema 23 시장은 현재 schema로 이전");
        Equal(EconomyRules.MarketGoodCount, migrated.Market.RecentSupply?.Length ?? 0, "이전 후 최근 수급 생성");
        Equal(checkpoint.Agents.Length, migrated.Market.LastSettlementSourceByAgent?.Length ?? 0, "이전 후 중복 정산 방지 슬롯 생성");

        var invalidQuality = (long[])checkpoint.Market.QualityHundredths!.Clone();
        invalidQuality[EconomyRules.TechniqueBookGoodId] = checked(
            ((long)checkpoint.Market.Stock[EconomyRules.TechniqueBookGoodId]
                * QualityValue.MaximumHundredths) + 1);
        Throws<InvalidDataException>(
            () => World.Restore(checkpoint with
            {
                Market = checkpoint.Market with { QualityHundredths = invalidQuality },
            }),
            "재고보다 큰 품질 합계 거부");

        var definition = EconomyRules.GetMarketGood(EconomyRules.TechniqueBookGoodId);
        var invalidOrder = new CityProductionCheckpoint(
            definition.Id,
            checked((ushort)(QualityValue.MaximumHundredths + 1)),
            0,
            EconomyRules.CityProductionMonths,
            EconomyRules.GetProductionMaterialCost(definition.Kind, definition.Realm),
            EconomyRules.GetProductionSpiritStoneCost(definition));
        Throws<InvalidDataException>(
            () => World.Restore(checkpoint with
            {
                Market = checkpoint.Market with { ProductionQueue = [invalidOrder] },
            }),
            "범위 밖 생산 품질 거부");

        Throws<InvalidDataException>(
            () => World.Restore(checkpoint with
            {
                Market = checkpoint.Market with { LastIndustryMonth = 1 },
            }),
            "세계보다 미래인 도시 산업 월 거부");

        Throws<InvalidDataException>(
            () => World.Restore(checkpoint with
            {
                Market = checkpoint.Market with { NextTradeSequence = long.MaxValue },
            }),
            "overflow 직전 거래 sequence 거부");
    }

    private static void EqualMarketCheckpoint(
        CityMarketCheckpoint expected,
        CityMarketCheckpoint actual,
        string message)
    {
        Equal(expected.TreasurySpiritStones, actual.TreasurySpiritStones, $"{message}·도시 영석");
        Equal(expected.PurchaseTransactions, actual.PurchaseTransactions, $"{message}·구매 누계");
        Equal(expected.SaleTransactions, actual.SaleTransactions, $"{message}·판매 누계");
        Equal(expected.MissionDeliveries, actual.MissionDeliveries, $"{message}·임무 누계");
        Equal(expected.MaterialsProcessed, actual.MaterialsProcessed, $"{message}·가공 재료");
        Equal(expected.PillsProduced, actual.PillsProduced, $"{message}·단약 생산");
        Equal(expected.TreasuresProduced, actual.TreasuresProduced, $"{message}·법보 생산");
        Equal(expected.TechniqueBooksProduced, actual.TechniqueBooksProduced, $"{message}·공법서 생산");
        Equal(expected.SpellBooksProduced, actual.SpellBooksProduced, $"{message}·법술서 생산");
        Equal(expected.TreasureQualityHundredths, actual.TreasureQualityHundredths, $"{message}·구형 품질");
        Equal(expected.LastIndustryMonth, actual.LastIndustryMonth, $"{message}·산업 월");
        Equal(expected.SpiritStonesMined, actual.SpiritStonesMined, $"{message}·채굴 영석");
        Equal(expected.RemainingSpiritStoneOre, actual.RemainingSpiritStoneOre, $"{message}·남은 광맥");
        Equal(expected.MiningMissionCount, actual.MiningMissionCount, $"{message}·채굴 임무");
        Equal(expected.SpiritStoneReserveTarget, actual.SpiritStoneReserveTarget, $"{message}·비축 목표");
        Equal(expected.CurrentFlowBucket, actual.CurrentFlowBucket, $"{message}·현재 수급 bucket");
        Equal(expected.AuthorityRevision, actual.AuthorityRevision, $"{message}·권위 revision");
        Equal(expected.NextTradeSequence, actual.NextTradeSequence, $"{message}·다음 거래 sequence");
        EqualSequence(expected.Stock, actual.Stock, $"{message}·재고");
        EqualSequence(expected.Prices, actual.Prices, $"{message}·가격");
        EqualSequence(expected.Supply, actual.Supply, $"{message}·누적 공급");
        EqualSequence(expected.Demand, actual.Demand, $"{message}·누적 수요");
        EqualSequence(expected.QualityHundredths, actual.QualityHundredths, $"{message}·품질 합계");
        EqualSequence(expected.ProductionQueue, actual.ProductionQueue, $"{message}·생산 대기열");
        EqualSequence(expected.RecentSupply, actual.RecentSupply, $"{message}·최근 공급");
        EqualSequence(expected.RecentDemand, actual.RecentDemand, $"{message}·최근 수요");
        EqualSequence(expected.SupplyWindowBuckets, actual.SupplyWindowBuckets, $"{message}·공급 bucket");
        EqualSequence(expected.DemandWindowBuckets, actual.DemandWindowBuckets, $"{message}·수요 bucket");
        EqualSequence(
            expected.LastSettlementSourceByAgent,
            actual.LastSettlementSourceByAgent,
            $"{message}·source watermark");
        EqualSequence(expected.RecentTrades, actual.RecentTrades, $"{message}·최근 거래 원장");
    }

    private static void EqualSequence<T>(T[]? expected, T[]? actual, string message)
    {
        if (expected is null || actual is null)
        {
            True(expected is null && actual is null, message);
            return;
        }

        True(expected.SequenceEqual(actual), message);
    }

    private static void Equal<T>(T expected, T actual, string message) =>
        TestAssert.Equal(expected, actual, message);

    private static void True(bool condition, string message) => TestAssert.True(condition, message);

    private static void Throws<TException>(Action action, string message)
        where TException : Exception => TestAssert.Throws<TException>(action, message);

}
