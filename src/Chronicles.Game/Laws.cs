using System.Collections.ObjectModel;

namespace Chronicles.Game;

public sealed record LawDefinition(string Id, string Name, string Unit, long Minimum,
    long Maximum, long DefaultValue, bool Provisional, string Rationale);

/// <summary>Every adjustable gameplay coefficient has one definition and a saved revision.</summary>
public static class Laws
{
    private static LawDefinition D(string id, string name, string unit, long min, long max,
        long value, string reason, bool provisional = true) =>
        new(id, name, unit, min, max, value, provisional, reason);

    public static IReadOnlyList<LawDefinition> All { get; } = Array.AsReadOnly<LawDefinition>(
    [
        D("birth.enabled", "출산 허용", "켜짐/꺼짐", 0, 1, 1, "세계 생식 허용", false),
        D("birth.kinship", "혈연 후손 간 허용", "켜짐/꺼짐", 0, 1, 1, "사용자 확정 2026-10-30", false),
        D("birth.bothParents", "양친 모두 경지 자격", "켜짐/꺼짐", 0, 1, 1, "사용자 확정 2026-10-30", false),
        D("birth.minimumRealm", "출산 최소 경지", "경지", 0, 10, 3, "금단 기본·사용자 확정", false),
        D("birth.maturity", "성년·생식 시작", "개월", 0, 1200, 216, "인간형 기본 18년"),
        D("birth.gestation", "임신 기간", "개월", 1, 120, 9, "인간형 기본"),
        D("birth.interval", "출산 후 회복·간격", "개월", 0, 12000, 120, "양육/생계 여유를 포함한 초기 보정"),
        D("birth.fertility", "생식 가능 수명 비율", "%", 0, 100, 80, "수명과 가임 수명 분리"),
        D("birth.chance", "적격 도려 잉태 확률", "만분율/월", 0, 10000, 300, "성장곡선 보정 대상"),
        D("birth.healthCost", "출산 부담", "건강점", 0, 90, 12, "무상 출산 방지"),
        D("birth.martialEquivalent", "무인 정점 출산 환산", "켜짐/꺼짐", 0, 1, 0, "금단 전투력만으로 생식 자격 자동 환산하지 않음", false),
        D("care.interval", "어린 자녀 돌봄 간격", "개월", 1, 120, 4, "한 번의 돌봄은 실제 양육자 작업 한 달을 사용"),
        D("care.age", "집중 양육 종료 나이", "개월", 1, 216, 60, "초기 인간형 다섯 살까지 돌봄 노동"),
        D("care.food", "돌봄 작업 식량", "개/작업", 0, 100, 1, "생활 소비와 구분한 양육 활동의 실제 투입"),
        D("genetics.mutationMagnitude", "잉태 유전 변이 폭", "자질점", 0, 10000, 200, "현재 잉태 원형에 고정·진행 중 임신 재추첨 없음"),
        D("genetics.mutationChance", "잉태 전달 단위 변이 확률", "만분율", 0, 10000, 10000, "선천 원형만 대상·학습/기예 숙련 비유전"),
        D("founder.lifespan", "반선 시조 수명", "개월", 1200, 12000000, 1200000, "사용자 위임 잠정 100000년"),
        D("founder.age", "시조 시작 나이", "개월", 216, 120000, 1200, "경지 구조는 초기 조건·가짜 과거 없음"),
        D("founder.ascensionProtection", "시조 비승 보호", "개월", 0, 120000, 1200, "후대 독립까지의 기본 보호·0이면 없음"),
        D("founder.initialFood", "시조 초기 식량", "개/인", 0, 100000, 360, "창세 자산 출처에 기록"),
        D("founder.initialMoney", "시조 초기 영석", "개/인", 0, 1000000, 1000, "창세 자산 출처에 기록"),
        D("cultivation.entry", "새 육신의 균형 자질 입문 기간", "개월", 1, 12000, 36, "새 육신에서 채택·기존 목표와 진척 보존"),
        D("cultivation.qi", "새 육신의 연기 전체 수행 기간", "개월", 1, 120000, 600, "균형 50.00 총 50년·기존 육신의 채택 목표 보존"),
        D("cultivation.foundation", "새 형성의 축기 전체 수행 기간", "개월", 1, 120000, 1200, "새 형성에서 채택·준비 중 돌파의 기간/수명 보존"),
        D("cultivation.upper", "새 상위 경지 형성 기간 증가", "%", 100, 500, 150, "새 형성의 잠정 비율·기존 형성 기록/진척 보존"),
        D("cultivation.breakthroughRisk", "기본 돌파 실패", "만분율", 0, 10000, 300, "자질·준비·경로 영향 별도"),
        D("cultivation.sourceCost", "수행 영기 소비", "개/월", 0, 100, 2, "지역 실제 공급·혼잡"),
        D("cultivation.recovery", "자연 회복", "건강점/월", 0, 100, 2, "회복은 논리 시간 소비"),
        D("martial.period", "무예 경지 기본 기간", "개월", 1, 12000, 120, "무예 독립 10경지"),
        D("life.mortal", "범인 기본수명", "개월", 12, 2400, 960, "잠정 인간형 80년"),
        D("life.qi", "연기 기본수명", "개월", 12, 120000, 1800, "정본 연기150년", false),
        D("life.foundation", "축기 기본수명", "개월", 12, 120000, 3600, "정본 축기300년", false),
        D("life.core", "금단 기본수명", "개월", 12, 120000, 7200, "정본 금단600년", false),
        D("life.upperMultiplier", "상위 수명 증가", "%", 100, 1000, 200, "원영 이상 잠정값"),
        D("soul.minimumRealm", "윤회 최소 경지", "경지", 0, 10, 5, "화신 대원만 기본", false),
        D("soul.crossing", "윤회 도하 기간", "개월", 0, 12000, 120, "출생 부모 없으면 계속 대기"),
        D("soul.waitingFirst", "적격 대기혼 우선", "켜짐/꺼짐", 0, 1, 1, "개발 기본안·안정 SoulId 순서"),
        D("soul.splitLimit", "비승 분혼 한도", "개/근원", 0, 100, 1, "무한 분혼 방지"),
        D("soul.splitCost", "분혼 보존량 비용", "%", 0, 100, 30, "독립혼 생성 비용"),
        D("soul.enabledRoutes", "윤회 활성 경로", "비트마스크", 0, 8191, 8191, "13개 조건 유지", false),
        D("world.capacityRealm", "계역 수용 경지", "경지", 0, 10, 10, "수용 초과의 실제 배척/전이"),
        D("hazard.sealChance", "고대 금제 발동", "만분율/탐색", 0, 10000, 100, "8번 지점 계열의 고대 금제·실제 탐색만 판정"),
        D("hazard.sealDuration", "영구 봉인 판정 기간", "개월", 1, 12000, 120, "복귀 수단과 실제 봉인 지속 확인"),
        D("hazard.turbidity", "구조 부식 탁도 경계", "점", 0, 100, 95, "위험 환경에서 실제 수행할 때 손상"),
        D("hazard.structureDamage", "고탁도 구조 손상", "점/수행월", 0, 100, 2, "회복 불가 100점 경계"),
        D("hazard.bondDamage", "형신 결속 손상", "점/수행월", 0, 100, 1, "합체 결속과 오행 상충·건강 부하"),
        D("hazard.portalChance", "계역 통로 사고", "만분율/이동", 0, 10000, 10, "9주↔10주 경계 이동의 실제 통로 사고"),
        D("hazard.trialDamage", "겁후 육신 부담", "건강점", 0, 100, 10, "겁을 통과해도 실제 육신 손상"),
        D("soul.pactDuration", "양혼부 보호 기한", "개월", 1, 12000, 120, "실물 사용으로 성립한 혼약의 자동 판정"),
        D("ecology.foodUse", "생활 식량 소비", "개/인월", 0, 100, 1, "실제 재고에서 소비"),
        D("ecology.regrowth", "자연 원천 재생", "개/월", 0, 10000, 20, "0이면 고갈 원천 재생 없음"),
        D("ecology.sourceCapacity", "지점 원천 상한", "개", 1, 1000000, 1000, "유한한 채집·광물·영기"),
        D("ecology.spoilage", "식량 부패", "%/년", 0, 100, 10, "기존 저장량에서 소실 기록"),
        D("ecology.disasterChance", "자연 재해 확률", "만분율/주년", 0, 10000, 20, "연도 경계에서만 판정"),
        D("economy.gatherYield", "채집 작업 수확", "개/작업", 1, 1000, 12, "원천 잔량에서 차감"),
        D("economy.craftDuration", "제작 작업 기간", "개월", 1, 120, 1, "단일 노동 용량"),
        D("economy.travelDuration", "통로 이동 시간", "개월/통로", 1, 120, 1, "원격 재고 접근 불가"),
        D("inventory.carryCapacity", "기본 휴대 용량", "무게점", 0, 1000000, 500, "용기 자체 용량과 구분"),
        D("inventory.productionWork", "제작 작업량", "노동점", 1, 100000, 100, "월 100 노동점 기준의 개발 기본값"),
        D("inventory.professionalWork", "전문 활동 작업량", "노동점", 1, 100000, 100, "치료·감정 등의 실제 작업"),
        D("inventory.extractionWork", "단위 채집 작업량", "노동점/개", 1, 100000, 25, "원천별 실제 채집량과 기간 계산"),
        D("currency.stoneValue", "실물 영석 환전 단위", "영석 잔액/실물 1개", 0, 1000000, 1, "0이면 환전 비활성·실물 소비와 출처 원장으로 잔액 전환"),
        D("knowledge.copyMonths", "필사 기본 시간", "개월", 1, 120, 1, "실제 매체·장절·노동 예약"),
        D("knowledge.copyPaper", "필사 장절당 종이", "개", 1, 1000, 1, "빈 매체와 추가 필사 재료의 실소비"),
        D("knowledge.copyInk", "필사 장절당 먹", "개", 1, 1000, 1, "형식별 실제 필사 재료"),
        D("knowledge.teachMinimum", "전수 최소 이해", "%", 1, 100, 1, "개인 지식의 전수 자격·시간은 별도 실제 예약"),
        D("knowledge.practiceMinimum", "실습 최소 이해", "%", 1, 100, 1, "실습 자격이며 책 소유나 읽기로 실전 숙련 지급하지 않음"),
        D("knowledge.maximumActionMonths", "지식 작업 최대 기간", "개월", 1, 1200, 120, "단일 예약의 한계·장기 작업은 분할"),
        D("knowledge.misreadChance", "첫 독해 오해 확률", "만분율", 0, 10000, 100, "실제로 읽은 개인 주장만 변화·판본 정답 변경 없음"),
        D("politics.sectLimit", "동시 종문 한도", "개", 0, 1000, 10, "역사상 창종 수와 구분"),
        D("politics.foundCost", "조직 창설 재원", "영석", 0, 1000000, 100, "개인→조직 금고 이전"),
        D("politics.founderRealm", "가문 가주 최소 경지", "경지", 0, 10, 0, "성년·실제 가계 자격과 출산 금단 자격을 분리"),
        D("politics.policyCost", "정책 변경 준비금", "영석", 0, 100000, 10, "예산 예약·시행 후 비용"),
        D("politics.salary", "월 조직 급여", "영석/인", 0, 100000, 1, "부족하면 미지급으로 기록"),
        D("politics.preparationMonths", "정책 준비 기간", "개월", 1, 1200, 1, "준비 중 법칙 revision과 실제 인력을 고정"),
        D("politics.preparationWorkers", "정책 준비 인력", "명", 1, 1000, 1, "같은 기간의 다른 노동과 중복 예약하지 않음"),
        D("politics.taxBasisPoints", "왕조 직접세", "만분율", 0, 10000, 100, "실거주 납세자의 실제 가용 영석에서 징수"),
        D("politics.tributeBasisPoints", "봉신 공납", "만분율", 0, 10000, 1000, "직접 세수에서 계층마다 한 번 전달"),
        D("politics.activityGrant", "정책 활동 지원금", "영석", 0, 1000000, 5, "조직의 실제 예산으로 지급"),
        D("politics.reliefThreshold", "구휼 지원 기준", "영석", 0, 1000000, 10, "정책 대상 선정의 개발 기본값"),
        D("ai.beliefBudget", "개인 믿음 보관 예산", "건", 1, 10000, 48, "논리 시각·안정 순서로 만료"),
        D("ai.beliefLifetime", "관측 유효기간", "개월", 1, 1200, 120, "관찰 정답 소급 없음"),
        D("ai.planBudget", "계획 탐색 예산", "노드/결정", 1, 1000, 16, "동일 정보·동일 예산"),
        D("ai.observationBudget", "월간 현장 주의 예산", "인물/관측", 1, 100, 4, "관측 가능한 인물 중 제한된 주의·전체 정답 복사 없음"),
        D("ai.familyWeight", "가계 유지 목표 가중치", "점", 0, 1000, 60, "양육·생계 우선순위 보정"),
        D("ai.studyWeight", "구도 목표 가중치", "점", 0, 1000, 50, "성격·자기 지식과 결합"),
    ]);

    public static IReadOnlyDictionary<string, LawDefinition> ById { get; } =
        new ReadOnlyDictionary<string, LawDefinition>(All.ToDictionary(x => x.Id, StringComparer.Ordinal));

    public static SortedDictionary<string, long> CreateDefaults() =>
        new(All.ToDictionary(x => x.Id, x => x.DefaultValue, StringComparer.Ordinal), StringComparer.Ordinal);

    public static string? Validate(IReadOnlyDictionary<string, long> values)
    {
        if (values.Count != All.Count) return "법칙 정의 수가 다릅니다.";
        foreach (var d in All)
            if (!values.TryGetValue(d.Id, out var v) || v < d.Minimum || v > d.Maximum)
                return $"법칙 범위 오류: {d.Id}";
        if (values["founder.age"] >= values["founder.lifespan"])
            return "시조 나이는 수명보다 작아야 합니다.";
        if (values["knowledge.copyMonths"] > values["knowledge.maximumActionMonths"])
            return "필사 기본 시간은 단일 지식 작업 최대 기간을 넘을 수 없습니다.";
        return null;
    }
}
