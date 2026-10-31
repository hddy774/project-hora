# 선로만상록 0.2.1 불변식 카탈로그

정본은 [Notion 통합 기획](https://app.notion.com/p/408acc9e6175817ab59bd91bb9dc011d)이며 이 파일과 `v021-tasks.json`은 개정6의 기계/검증용 동반 자료다. 제품 코드를 구현하거나 게임 시험을 통과한 목록이 아니다. 모든 `proposed_tests`는 아직 미구현이다. 실행한 원장 검증기 자체 시험과 구분한다.

기능은 새 상태의 저장 왕복·관련 INV 등록을 T07 제품 하네스에 연결하고 완료 증거를 남긴다. 미결 Q의 정책 민감 분기를 임의 값으로 통과시키지 않는다. 카탈로그의 기계 필드는 JSON의 `invariants`에 동일하게 보존한다.

## INV-001 · 도메인별 단일 권위와 안정 신원

같은 사실의 쓰기 권위는 하나다. 정의·실물·권리·인지·조회 투영을 구분하고 PersonId·BodyId·SoulId·SpiritFragmentId를 서로 대체하지 않는다. 영구 참조는 안정 ID를 사용하며, 슬롯 재사용·용량 확장·사망 이후에도 이전 명령·관계·계약이 다른 개체를 가리키지 않는다.

관련 작업: V021-A03 · V021-P01 · V021-J01 · V021-D02

제안 게임 시험(미구현):

- `StableIdentity_SlotReuseDoesNotRetargetHistoricalReferences`
- `DomainIdentity_PersonBodySoulAndSpiritFragmentRemainDistinct`
- `Authority_ProjectionsDoNotBecomeIndependentWriters`

## INV-002 · 100지점·명시 통로·단일 실제 위치

기본 지도는 10주×10지점의 중복 없는 실제 활동 노드 100개다. 층천·시설·비경 내부는 하위 공간이며 전략 지점을 추가하지 않는다. 이동은 명시 통로와 접근 조건으로 계산하고 지점 도착·현지 탐색·비경 입장을 구분한다. 인물과 화물은 실제 위치 상태 하나만 가지며 이동 중 양쪽 체류·도착 재고로 중복 집계되지 않는다.

관련 작업: V021-M01 · V021-M02 · V021-M04 · V021-M05

제안 게임 시험(미구현):

- `Geography_NestedSpacesPreserveOneHundredStrategicNodes`
- `Movement_RequiresExplicitPassageAndAccessConditions`
- `Location_TransitIsNotCountedAtBothEndpoints`

## INV-003 · 형식화된 법칙과 시행 경계 보존

법칙은 안정 LawId·자료형·단위·조건·의존성·시행 방식·revision을 가지며 임의 실행 코드를 저장하지 않는다. 0·비활성·무제한·미설정을 구분하고 경지 자격은 경로·완성 상태를 판정한다. 변경은 전체 검증 후 지정 tick에 적용하며 이전·이후 구간을 각각 해당 규칙으로 정산한다. 기존 임신·예약·확정 결과는 명시된 시행 프로필로 처리하고 삭제·소급 재추첨으로 맞추지 않는다.

관련 작업: V021-Z01 · V021-Z02 · V021-Z03 · V021-F02

제안 게임 시험(미구현):

- `LawValues_ZeroDisabledUnlimitedAndUnsetRemainDistinct`
- `LawChange_BoundarySettlementUsesApplicableRevision`
- `LawChange_ExistingCommitmentsFollowDeclaredTransitionProfile`

## INV-004 · 공통 저장 왕복과 중단 복구

0.2.1 저장 왕복은 모든 도메인의 공통 의무다. 신원·임신/출생·유전/양육·영혼/흔적·수행·권리/정책/채무·물품/예약/화물/경매·인지·조작권/명령·RNG·사건 순서와 revision을 보존하여 복원 뒤 같은 전이가 이어져야 한다. 중단·부분 파일·손상된 최신 저장에서 일관된 정상 상태를 복구하며, 이전 버전 파일을 변경하거나 추정 데이터로 자동 변환하지 않는다.

관련 작업: V021-T07 · V021-D02 · V021-H02 · V021-H03

제안 게임 시험(미구현):

- `Persistence_AllDomainCommitmentsSurviveRoundTrip`
- `Persistence_RestoredContinuationMatchesUninterruptedExecution`
- `Persistence_InterruptedWritePreservesRecoverableCheckpoint`
- `Persistence_UnsupportedLegacyFileRemainsUnmodified`

## INV-005 · 원자 전이·멱등 처리·단일 종결

출생·사망·이동·자산 이전·입찰·정책·장비 교체의 관련 상태와 원장은 하나의 권위 전이로 반영한다. 동일 명령 ID·사건 원인 키의 재처리는 효과를 추가하지 않는다. 같은 tick의 완료·취소·낙찰·권한 변경은 공통의 안정 사건 순서로 처리하여 종결 결과 하나만 만든다. 취소 확정 전 예약을 해제하지 않고 실패한 장비 교체는 기존 상태를 유지한다.

관련 작업: V021-A03 · V021-F02 · V021-E01 · V021-E04 · V021-G03 · V021-I03 · V021-X04

제안 게임 시험(미구현):

- `Transition_FailureLeavesNoPartialAuthorityMutation`
- `Idempotency_ReplayedCommandOrCauseDoesNotDuplicateEffects`
- `Cancellation_CompletionRaceProducesSingleTerminalOutcome`
- `EquipmentSwap_RejectionPreservesSlotsAndStorage`

## INV-006 · 엔진 독립 실행과 재현 가능한 진행

Core/Simulation과 권위 실행 계약은 UnityEngine·WPF·Android SDK·플랫폼 UI 객체에 의존하지 않는다. 동일 초기 상태·seed·정의/법칙/AI 정책 revision·논리 예산·tick별 명령열은 단일/분할 advance·배속·checkpoint·화면 상태·스레드 수에 관계없이 같은 권위 결과를 낸다. 난수는 안정 stream과 확정 사건에서 소비한다. CPU 시간은 같은 게임 시각의 양보/재개에만 쓰며 반올림·오버플로·병렬 병합 순서를 명시한다.

관련 작업: V021-H01 · V021-H04 · V021-H08 · V021-T03

제안 게임 시험(미구현):

- `Architecture_CoreAndSimulationHaveNoPlatformDependencies`
- `Determinism_AdvancePartitionCheckpointAndSpeedPreserveResults`
- `Determinism_WorkerCountAndHostYieldPreserveEventSequence`
- `Randomness_CommittedEventDoesNotRerollOnReplay`

## INV-007 · 관찰 순수성과 관찰 여부 독립

조회·검색·정렬·확대·가계도 펼침·그래프·추적 등록·미리보기는 세계·RNG·명령열·인지·계획·생성 상태를 바꾸지 않는다. 필요한 인물·유전형·기연은 화면 선택을 기다려 생성하지 않는다. 세계 통계와 중요한 사건 기록은 관찰 여부와 무관하게 수집한다. 비교 실험은 권위 세계와 분리한 fixture와 난수로 실행하며 미수집 과거를 실제 기록처럼 복원하지 않는다.

관련 작업: V021-D01 · V021-U02 · V021-T02 · V021-N01

제안 게임 시험(미구현):

- `Observation_QueryAndPreviewLeaveAuthorityAndRngUnchanged`
- `Observation_SelectionDoesNotGeneratePeopleTraitsOrOpportunities`
- `History_WorldStatisticsDoNotDependOnWatchlistMembership`

## INV-008 · 개인 인지 경계와 숨은 변화 비간섭

AI는 감각·전수·문헌·소문 등 실제 획득 경로를 통과한 정보만 판단에 사용한다. 믿음은 출처·관측/획득 시각·맥락·확신·반증을 보존하며 모름과 거짓, 순환 소문과 독립 증거를 구분한다. 개인 입력이 같은 두 세계는 새 관측 전까지 숨은 사실 차이만으로 결정·재판단 시점이 달라지지 않는다. 실행기는 실제 조건을 검사하되 실패 결과로 숨은 정답을 전달하지 않는다. 개인 믿음의 예산·만료·요약·망각은 논리 tick/안정 키로 재현하고 보존된 세계 사건/원천 이력과 구분한다.

관련 작업: V021-N01 · V021-L01 · V021-L04 · V021-L05 · V021-N02

제안 게임 시험(미구현):

- `Cognition_HiddenWorldChangesDoNotAlterDecisionOrReviewTiming`
- `Belief_UnknownFalseAndRepeatedRumorSourcesRemainDistinct`
- `Execution_FailureDoesNotRevealUnobservedAuthorityFacts`

## INV-009 · 권한 우선 조회와 표시 비간섭

플레이 조회는 지식·권한 필터를 검색·정렬·집계·행 수·그래프·추천보다 먼저 적용하고 허용 정보만 DTO에 담는다. 같은 관측 이력과 구분 가능한 물리 표식에서는 숨은 속성 차이가 표시 묶음·정렬·추천·미리보기를 바꾸지 않는다. 표시 묶음은 실제 스택을 합치지 않으며 숨은 속성으로 명령 대상을 최적 선택하지 않는다. 직접 입력한 비공개 ID와 거부 이유도 지식·접근권 검사를 우회하지 못한다.

관련 작업: V021-N01 · V021-N02 · V021-N03 · V021-I02 · V021-U07

제안 게임 시험(미구현):

- `Query_AuthorizationPrecedesSearchSortAggregateAndRecommendation`
- `Inventory_HiddenPropertiesDoNotChangeKnownDisplayGroups`
- `Command_HiddenTargetIdsAndErrorsDoNotBypassKnowledgeBoundary`

## INV-010 · 조회 세대·시점·표시 의미 보존

조회 문맥은 세계·모드·ActorId·지식/권한/법칙 revision·표시 세대를 식별하고 응답은 요청 ID·snapshot tick·세대를 가진다. 전환·복원 후 이전 세대와 같은 화면의 오래된 응답을 폐기하며 관찰 캐시·툴팁·내보내기를 새 권한으로 재생성한다. 표와 합계는 같은 snapshot을 사용하고 모집단·단위·시점을 표시한다. 실제 0·미상·미정·기록 없음·미성립·개발 미제공을 구분하며 화면 이동은 접수된 세계 명령을 취소하지 않는다.

관련 작업: V021-N01 · V021-N03 · V021-H07 · V021-T05

제안 게임 시험(미구현):

- `QueryGeneration_LateObserverResponseIsDiscardedAfterPlaySwitch`
- `QueryGeneration_ActorAndWorldRestoreInvalidateOldResponses`
- `Presentation_SnapshotPopulationAndMissingValueMeaningsRemainExplicit`
- `Navigation_ClosingScreenDoesNotCancelAcceptedCommand`

## INV-011 · 실제 창세·생식·생애·인구 원장

기본 창세는 생존 인물 정확히 2명·두 시조 반선경·종문 0이며 사용자 변경은 법칙 revision으로 구분한다. 사회 운영자를 숨겨 생성하지 않는다. 생존 인구는 시작 인구+실제 출생+명시적 편입−사망−명시적 이탈과 일치하고 임신 태아·지역 이동·비승을 출생이나 사망으로 혼동하지 않는다. 출생은 새 PersonId·BodyId와 부모·부담·결과를 기록하며 기본 금단 출산과 선택된 생식/성숙·건강·의사·재검사 규칙을 따른다. 도려·임신·출생·양육은 별도 상태다.

관련 작업: V021-P01 · V021-P02 · V021-F01 · V021-F02 · V021-R09

제안 게임 시험(미구현):

- `Genesis_DefaultWorldHasTwoHalfImmortalFoundersAndNoSect`
- `Population_LedgerBalancesBirthEntryDeathAndExit`
- `Reproduction_RelationshipDoesNotImplyPregnancyOrEligibility`
- `Birth_NewIdentityAndRecordedOutcomeAreCreatedOnlyOnce`

## INV-012 · 유전·가계·교육·상속의 계승 경계

생물학적 부모 그래프는 무순환이며 입양·양육·도려·영혼 근원을 다른 연결로 보존한다. 유전 원형은 잉태 사건·부모 전달 상태 snapshot·규칙 revision·안정 난수로 한 번 확정하고 발달·후천 변화·현재 발현을 분리한다. 후천 변화는 정의된 전달 경로가 있을 때만 유전된다. 부모의 경지·숙련·기억·직책을 복사하지 않으며 교육은 실제 지식·시간·자원, 상속은 실제 재산·채무·제도에 근거한다. 영구 가족 연결은 최근 관계 상한으로 유실하지 않는다.

관련 작업: V021-F03 · V021-F04 · V021-Y03 · V021-K02

제안 게임 시험(미구현):

- `Genealogy_AdoptionAndSoulOriginDoNotRewriteBiologicalParents`
- `Genetics_ConceptionSnapshotAndGenotypeSurviveReplay`
- `Inheritance_RealmSkillsOfficesAndAssetsUseSeparateTransferRules`

## INV-013 · 윤회 자격 증거와 단일 여정

윤회는 실행 버튼이나 독립 성취가 아니라 실제 원인 사건에 대한 시스템 전이다. 일반 경로 자격은 원인 직전 경지/완성·영혼 상태 snapshot과 적용 법칙 revision으로 고정하며 역사상 최고 경지나 사건 후 손상 값으로 대체하지 않는다. 활성 경로의 실제 조건·영혼 존속·현생 종료 증거를 요구한다. 여러 조건이 맞아도 안정 우선순위와 원인 키로 여정 하나만 배정하고 다른 매칭 원인은 설명에 남긴다.

관련 작업: V021-J02 · V021-J04 · V021-Z02

제안 게임 시험(미구현):

- `Reincarnation_EligibilityUsesImmediatelyPreCauseSnapshot`
- `Reincarnation_MinimumQualificationAndSoulSurvivalAreBothRequired`
- `Reincarnation_OverlappingTriggersCreateOneJourney`
- `Reincarnation_SeveranceRequiresTerminationEvidenceBeyondTimerExpiry`

## INV-014 · 분혼 정체성·출생 배정·흔적 단계

분혼은 실제 형성 사건과 제한/비용을 가진 새 SoulId·ParentSoulId·RootSoulId이며 RC-13은 형성 당시 근원 자격과 비승 확정을 검사한다. 수행 분령은 SpiritFragmentId이며 독립 영혼·출생 후보가 아니다. 기본안의 영혼 배정은 실제 적격 출생에서 한 번 이루어지고 이전 살아 있는 현생 연결을 먼저 종결한다. 태내 미배정 영혼 항목은 미결로 두며 배정 후 과거를 다시 쓰지 않는다. 여섯 흔적의 보존·접근·통합을 분리하고 대기 영혼이나 분혼 때문에 부모·육신·재산·숙련을 자동 생성하지 않는다.

관련 작업: V021-J01 · V021-J03 · V021-J04 · V021-F02 · V021-R04

제안 게임 시험(미구현):

- `SplitSoul_UsesFormationEvidenceWithoutCopyingRootCultivationHistory`
- `SoulAssignment_OneActualBirthReceivesOneSoulWithoutConcurrentLives`
- `PrenatalState_UnassignedSoulEffectsRemainUnresolvedWithoutRetroactiveRewrite`
- `SoulTrace_PreservationAccessAndIntegrationDoNotGrantAutomaticOwnershipOrMastery`

## INV-015 · 효과 원천 단회 반영과 형성·현재·잠재 분리

효과는 원천 ID·대상·연산·중첩/상한·revision을 가지며 같은 원인을 유전·혼흔·특성·경로·장비에서 중복 가산하지 않는다. 24재능은 기존 6대 자질과 정의된 의존성에서 파생하고 상태 표식을 원인에 되먹여 증폭하지 않는다. 형성 snapshot·현재 구조·현재 발휘·알려진 평가·조건부 잠재를 분리한다. 경로 전환과 다음 경지 변환은 기여를 한 번 전달하며 배타 효과·과거 보상을 반복 유지하거나 지급하지 않는다.

관련 작업: V021-Y01 · V021-Y02 · V021-Y03 · V021-R07 · V021-R08 · V021-I03

제안 게임 시험(미구현):

- `Effects_SharedCauseIsAppliedOnceAcrossInheritanceTraceAndTrait`
- `DerivedTalent_StateMarkerDoesNotFeedBackIntoItsOwnCause`
- `RouteTransition_TransfersContributionWithoutDuplicateEffectsOrRewards`
- `Potential_ConditionalFutureDoesNotBecomeCurrentAbility`

## INV-016 · 실물·소유·보관·접근·수납 분리

실물은 공통 정의와 별도 InstanceId를 가지며 소유자·보관자·운송자·장착·결속·사용권을 구분한다. 같은 실물을 시장·제작·장비별 권위 객체로 복제하지 않는다. 실제 위치/컨테이너 부모는 하나이고 수납 순환을 금지하며 용량·중첩·허용 유형은 선택된 프로필로 검사한다. 숨은 상태·품질·예약이 다른 실물을 병합해 정보를 잃지 않는다. 용기 해제·파괴·초과에서 재고를 삭제하지 않고 명시 전이하며 소유권만으로 원격 소비를 허용하지 않는다.

관련 작업: V021-I01 · V021-I02 · V021-I03 · V021-E01

제안 게임 시험(미구현):

- `Asset_OwnershipCustodyEquipmentAndBindingRemainIndependent`
- `Container_CyclesMultipleLocationsAndInvalidNestingAreRejected`
- `Inventory_StackOperationsPreserveDistinctInstanceState`
- `Container_RemovalOrOverflowDoesNotSilentlyDeleteContents`

## INV-017 · 공통 예약·예치·시간 용량과 공정 소비

가용·예약·예치 자금과 물품은 공통 원장으로 관리하여 제작·판매·입찰·장착·정책 사이의 중복 사용을 막는다. 개인·가계·집계 노동·교육·돌봄·방어는 같은 인력/시간·시설 용량을 공유한다. 계획의 예상 재고·상대 수락·UI 초안은 실제 예약이나 계약이 아니다. 소비는 공정 경계에서 발생하며 중단·취소는 이미 소비한 양·회수량·폐기량을 구분한다. 정책 묶음과 복수 의무도 전체 용량을 함께 검사한다.

관련 작업: V021-K01 · V021-E01 · V021-E04 · V021-G03 · V021-F04

제안 게임 시험(미구현):

- `Reservation_CraftingSaleAuctionAndEquipmentCannotReuseCommittedAssets`
- `Capacity_CareTeachingProductionAndDefenseShareOneLaborBudget`
- `DraftAndPlan_AssumptionsDoNotReserveAssetsOrAcceptContracts`
- `WorkCancellation_ConsumedInputsAreNotRefundedAsUnusedStock`

## INV-018 · 자원·화폐·보상·경제 집계의 보존

자연 원천과 소유 재고를 구분하고 생성/재생·생산·소비·손실·이전 사건이 잔량과 실물 원장을 설명해야 한다. 유한 보상은 먼저 확정한 정산만 획득하며 재입장·재처리로 복제하지 않는다. 영석 채굴은 정의된 유입이고 급여·세금·판매·경매·수수료는 실제 당사자 간 이전이다. 재생률 0은 기존 잔량을 지우지 않고 용량 증가는 즉시 지급이 아니다. 운송품·생산·노동·집계 수요를 중복 계상하거나 가격 변화·미충족 주문으로 거래나 재산을 만들지 않는다.

관련 작업: V021-O01 · V021-O02 · V021-O04 · V021-E01 · V021-E05 · V021-M05

제안 게임 시험(미구현):

- `ResourceLedger_SourcesProductionConsumptionAndLossExplainBalances`
- `FiniteReward_CompetingOrRepeatedSettlementAwardsOnlyAvailableStock`
- `Currency_TaxesFeesWagesAndAuctionPaymentsRemainTransfers`
- `Economy_AggregationTransitAndPriceUpdatesDoNotCreateDuplicateStockOrDemand`

## INV-019 · 주권·위임·소유·영토 주장의 분리

토지 소유·실효 통제·시설 운영·자원권·최상위 주권·봉신 위임·영토 주장을 다른 권리로 관리한다. 왕조 성립은 한 주 이상의 독립 최상위 통치권을 요구하며 봉신의 수임 영토나 단순 소유로 대체하지 않는다. 동일 관할의 최상위 주권을 중복 배정하지 않고 봉신 사슬은 무순환이며 직할/간접 통치 주는 중복 합산하지 않는다. 무영토 왕조의 역사·자산·주장은 보존하지만 주장만으로 옛 영토에 새 조세·명령권을 행사하지 못한다.

관련 작업: V021-M03 · V021-G01 · V021-G02

제안 게임 시험(미구현):

- `Sovereignty_OwnershipAndDelegationDoNotAutomaticallyCreateDynasty`
- `Vassalage_NoCyclesDuplicateSovereigntyOrDoubleCountedProvinces`
- `LandlessDynasty_HistoryAndAssetsPersistWithoutClaimBasedNewTaxAuthority`

## INV-020 · 정책 시행·관할·승계·채권의 계속 효력

종문은 종합 조직이며 현재 전문가·재고·정책 차이를 영구 전문 배율로 바꾸지 않는다. 정책은 제안·승인·공표·효력·실제 이행을 구분하고 10대 분류 전체의 예산·시간·상위 법·기존 계약을 검사한다. 관할/승계와 시행이 겹치면 공통 순서로 권한을 확정한 뒤 시설·조항별로 재검사한다. 제안자 사망만으로 유효 의결을 취소하지 않으며 채무 원금·채권 귀속·집행권·영토 주장을 분리한다. 복위·승계·윤회로 금고·채권·직책·과거 급여를 복제하지 않는다.

관련 작업: V021-G03 · V021-S01 · V021-S03 · V021-G02

제안 게임 시험(미구현):

- `Policy_AdoptionDoesNotCreateResourcesOrPermanentSectSpecialization`
- `JurisdictionChange_RevalidatesOnlyAffectedFacilityClauses`
- `Succession_ValidDecisionsAndDebtPrincipalAreNotDuplicatedOrImplicitlyErased`
- `RestorationOrReincarnation_DoesNotAutomaticallyRestoreOfficeOrPastPayments`

## INV-021 · 기존 행동 스케줄과 계획 경계의 정합성

인물당 다음 행동 하나와 indexed heap 위치 인덱스의 정합성을 유지하며 갱신은 in-place로 처리한다. 동적 등록·제거·확장·슬롯 재사용으로 이전 인물의 행동을 실행하지 않는다. 재검토 사유는 병합하되 추론 요청·추론 완료·실제 행동을 구분하고 개인에게 유효한 결정만 다음 행동을 갱신한다. 기존 월 pulse·장기계획 경계와 중요사건 재판단을 함께 보존한다. 자료구조만 바꾼 호환 시험과 AI 정책 revision을 바꾼 행동 시험은 서로 다른 기대 기준을 가진다.

관련 작업: V021-L01 · V021-L05 · V021-H04 · V021-P01

제안 게임 시험(미구현):

- `AgentSchedule_InPlaceUpdatesPreserveOneNodeAndHeapIndexPerPerson`
- `AgentSchedule_DynamicRegistrationAndSlotReuseRejectStaleActions`
- `Reconsideration_MergedReasonsDoNotBecomeDuplicateActions`
- `Planning_PulseEventAndPlanBoundariesRespectRevisionSpecificExpectations`

## INV-022 · 미정 정책·차단 판정·검증 근거의 비우회

Q-01 혈연/다양성, Q-03 종문 한도 세부, 미정 계수·배치·기술 채택을 임의 기본값이나 확정 정본으로 승격하지 않는다. 성장 차단·무거래·공석·재원 부족을 외부인·숨은 주민·가짜 입찰자·무료 자원·무한 변이로 보정하지 않는다. 번식 가능 상태는 현재 가능·조건 대기·구조적 차단·미판정을 구분하며 구조적 차단은 명시 후보 범위와 규칙으로 입증된 경우에만 표시한다. 관계 미설정은 무관계가 아니다. 기획안·소스 근거·구현·시험 실행·성능 측정 상태를 구분하고 미구현 테스트나 PC 결과를 실제 통과·Android 검증으로 표시하지 않는다.

관련 작업: V021-A02 · V021-P02 · V021-Z04 · V021-T06 · V021-R09

제안 게임 시험(미구현):

- `UnresolvedPolicy_DoesNotSilentlySelectKinshipOrSectCountingRules`
- `GrowthBlock_NoEligiblePairDoesNotAloneProvePermanentExtinction`
- `WorldStagnation_DoesNotSpawnUnspecifiedPeopleResourcesOrBidders`
- `Evidence_ProposedTestsAndHostBenchmarksAreNotReportedAsProductPasses`

## INV-023 · 경지 필수 전이·돌파/중단의 경계

형성 기록·현재 성숙·안착·손상·조건부 잠재를 분리한다. 원영 파단 후 형성 실패는 사망이며 준비 거부/앱 정지와 혼동하지 않는다. 도겁의 소경지별 실제 겁난을 생략하지 않고 반선·비승 완료를 구분한다. 경로명만으로 높은 품질·자유 잠재를 지급하지 않는다.

관련 작업: V021-R03 · V021-R04 · V021-R07 · V021-R08 · V021-B04

제안 게임 시험(미구현):

- `Cultivation_PreparationIsNotIrreversibleAttempt`
- `NascentSoul_FailureAfterCoreBreakRecordsDeathOnce`
- `Tribulation_EachMinorStageHasActualResolution`
- `HalfImmortal_DoesNotImplyAscension`

## INV-024 · 저작·판본·개인 지식과 실행의 분리

저작/판본·실물 매체·개인 해독/이해/오해·실전 숙련은 별도다. 공정은 채택한 개인 지식 revision과 실제 접근 조건을 사용한다. 정정본 발생이나 책 소실이 개인 지식을 소급 정답화/삭제하거나 미독 장절을 지급하지 않는다. 혼흔 접근도 실물·전수 권리·기예 숙련 자동 획득이 아니다.

관련 작업: V021-B03 · V021-B04 · V021-K02 · V021-J03 · V021-L04

제안 게임 시험(미구현):

- `Knowledge_PhysicalOwnershipDoesNotGrantMastery`
- `Knowledge_NewEditionDoesNotSilentlyRewriteBelief`
- `Process_UsesSelectedKnowledgeRevisionAndAccess`
