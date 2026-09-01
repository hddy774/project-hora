# PC 임시 규칙 검토 원장

> 감사 기준: PC `0.0.21`, GitHub/local `main`
> `7eb4d540b328d4664778b3e494148db0cd36bee2`, 2026-09-02 KST.

이 원장은 코드와 문서에서 검색된 고유 검토 ID 55개를 한 행씩 정리한다. 238개 모바일-PC
이식 요구사항 중 `PC_TEMP_REVIEW`인 31행은 **요구사항 단위**이고, 이 문서의 55개는
**공식·수치·정책 단위**이므로 두 숫자는 서로 대체하지 않는다. 기존
`pc-port-ledger.json`에는 37개 ID가 연결되어 있고, 아래 표에서 `이식 원장 외`로 표시한
18개는 기능 trace 문서와 코드에는 있으나 중앙 요구사항 행에는 직접 연결되지 않았다.

모든 행의 기본 권위 상태는 `PC 임시`, 결정 상태는 `검토 대기`다. 표의 `현재 효과`는
승인 여부가 아니라 현재 코드에서의 작동 범위를 뜻한다. 이 문서는 값을 승인·변경하지
않는다.

| Review ID | 도메인 | 현재 효과 | 현재 규칙 요약 | 주요 영향 | 중앙 연결 | 대표 근거 |
|---|---|---|---|---|---|---|
| `PC-TEMP-AI-UTILITY-113` | AI | 동작 중 | 목표·행동 선택의 임시 효용 가중치 | 게임플레이·결정론 | 연결 | `PersonCausalModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-APPEARANCE-DISTRIBUTION-113` | 인물 | 동작 중 | 외형·미모의 임시 20.00~80.00 분포 | 게임플레이·저장 | 연결 | `PersonLifeAuthority.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-MEMORY-POLICY-113` | 인과·기억 | 동작 중 | 생별 기억 상한과 강화 정책 | 게임플레이·저장·성능 | 연결 | `AgentCausalRuntime.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-TRAIT-DIST-113` | 자질 | 동작 중 | 6영역·60 Component 초기 분포 | 게임플레이·RNG·저장 | 연결 | `AptitudeModel.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-TRAIT-ROLE-113` | 자질 | 동작 중 | Component의 임시 역할 조합 | 게임플레이·전투·수행 | 연결 | `AptitudeModel.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-SPIRIT-ROOT-113` | 자질 | 동작 중 | 영근 세분화와 표시 계약 | 게임플레이·저장·UI | 연결 | `AptitudeModel.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-FLOOR-WEIGHTS-113` | 수행 | 동작 중 | 9층 Work 가중치 80~140 | 게임플레이·저장 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-COMPONENT-TARGETS-113` | 수행 | 동작 중 | 병목·대원만 구성요소 기준 | 게임플레이·저장 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-METHOD-PREFLIGHT-113` | 수행 | 동작 중 | 공법 숙련·이해 5.00 preflight | 게임플레이·결정론 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-STABILIZATION-113` | 수행 | 동작 중 | 구성요소 평균 30.00 안착 기준 | 게임플레이·결정론 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-ENVIRONMENT-113` | 수행 | 동작 중 | 영맥 환경 80~120% 수행 계수 | 게임플레이·지도 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-FOUNDATION-SOURCE-113` | 수행 | 동작 중 | 인도·지도·천도 Source 가용성 매핑 | 게임플레이·저장 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-UPPER-REALM-113` | 수행 | 동작 중 | 상위 경지 구성요소와 대승경 수명 | 게임플레이·저장 | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-LAW-LUCK-BRIDGE-113` | 수행 | 검토 전용 | 법칙 이해·운명/행운 연결 전 `Unavailable` | 게임플레이 gate·UI | 연결 | `RealmRules.cs`, `PC-CULTIVATION-018.md` |
| `PC-TEMP-CULT-ACTION-DURATION-019` | 수행 계획 | 동작 중 | 경지별 계획 기간과 여유·일반·폐관 비율 | 게임플레이·성능 | 이식 원장 외 | `PC-CULTIVATION-ACTION-019.md` |
| `PC-TEMP-CULT-CONDITION-FACTOR-019` | 수행 계획 | 동작 중 | 강도·자원·진법 50~200% 종합 계수 | 게임플레이·자원 | 이식 원장 외 | `PC-CULTIVATION-ACTION-019.md` |
| `PC-TEMP-CULT-FORMATION-INSTALL-019` | 수행 계획 | 동작 중 | 이해 30.00 설치 자격과 성공 판단 | 게임플레이·RNG | 이식 원장 외 | `PC-CULTIVATION-ACTION-019.md` |
| `PC-TEMP-CULT-JOINT-TEACHING-019` | 수행 계획 | 동작 중 | 공동 수행 흡수 상한과 교사 경험 | 게임플레이·관계 | 이식 원장 외 | `PC-CULTIVATION-ACTION-019.md` |
| `PC-TEMP-CULT-DETAIL-STAGES-021` | 수행 관찰 | 동작 중 | 범인·10대경지의 5단계 이름·가중치 | 조회·UI | 이식 원장 외 | `CultivationDetailProgress.cs`, `PC-CULTIVATION-DETAIL-021.md` |
| `PC-TEMP-REINCARNATION-UPPER-DURATION-113` | 윤회 | 동작 중 | 상위 5경지 체류 34·55·89·144·233년 | 게임플레이·저장 | 연결 | `ReincarnationModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-REINCARNATION-STAGES-113` | 윤회 | 동작 중 | 도하 단계 누적 경계 1/12~12/12 | 게임플레이·저장 | 연결 | `ReincarnationModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-REINCARNATION-TRACE-113` | 윤회 | 동작 중 | 6영역 전생 흔적 후보 가중치 | 게임플레이·결정론 | 연결 | `World.Reincarnation.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-REINCARNATION-BIRTH-CANDIDATE-113` | 윤회 | 동작 중 | 부모 후보·가계·후보 부재 처리 | 게임플레이·저장 | 연결 | `ReincarnationModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-REINCARNATION-SOUL-DAMAGE-113` | 윤회 | 동작 중 | 전투 사망의 영혼 손상 구간 | 게임플레이·저장 | 연결 | `ReincarnationModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-RELATION-BALANCE-113` | 관계 | 동작 중 | 관계 metric 변화량과 역할 임계값 | 게임플레이·저장 | 연결 | `RelationModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-TEACHING-BALANCE-113` | Teaching | 동작 중 | 전수 확률·성장량·연속성 균형 | 게임플레이·RNG | 연결 | `TeachingModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-RELATION-EXTERNAL-CONTEXT-113` | 관계 | 동작 중 | 조직·평판 권위 전 외부 문맥 연결 | 조회·UI·게임플레이 | 연결 | `RelationModel.cs`, `PC-PERSON-CAUSAL-016.md` |
| `PC-TEMP-ATLAS-NAMES-113` | 지도 | 동작 중 | Production 1·2층천의 임시 지명 | UI·저장 | 연결 | `PC-ATLAS-ECONOMY-017.md` |
| `PC-TEMP-VEIN-PROFILES-113` | 지도·자원 | 동작 중 | 영맥 품질·월 생산·초기 공급 calibration | 게임플레이·경제 | 연결 | `PC-ATLAS-ECONOMY-017.md` |
| `PC-TEMP-LAYER-ACCESS-113` | 지도 | 동작 중 | 제2층천 축기경 이상 접근 Gate | 게임플레이·UI | 연결 | `PC-ATLAS-ECONOMY-017.md` |
| `PC-TEMP-GEOGRAPHY-DENSITY-020` | 지리 | 동작 중 | 임시 지명·거리·정착지/자원 밀도 | 생성·성능·저장 | 이식 원장 외 | `PC-ORGANIZATION-020.md` |
| `PC-TEMP-ORG-APTITUDE-MAPPING-020` | 조직 | 동작 중 | 정신·오성의 조직 운영·창설 매핑 | 게임플레이·AI | 연결 | `OrganizationModel.cs`, `PC-ORGANIZATION-020.md` |
| `PC-TEMP-ORG-SUCCESSION-020` | 조직 | 동작 중 | 승계 점수와 공석 10년·쇠퇴 50년 | 게임플레이·저장 | 연결 | `OrganizationModel.cs`, `PC-ORGANIZATION-020.md` |
| `PC-TEMP-ORG-STRATEGY-020` | 조직 | 동작 중 | 세 전략축 분류 경계 | 게임플레이·AI | 연결 | `OrganizationModel.cs`, `PC-ORGANIZATION-020.md` |
| `PC-TEMP-ORG-AI-DECISION-020` | 조직 | 동작 중 | 월 8명 분산 평가와 가입·이탈 판단 | 게임플레이·성능 | 연결 | `OrganizationModel.cs`, `PC-ORGANIZATION-020.md` |
| `PC-TEMP-ORG-CONTRIBUTION-020` | 조직 | 동작 중 | 공동 출자와 가입 후 기여식 | 게임플레이·경제 | 연결 | `OrganizationModel.cs`, `PC-ORGANIZATION-020.md` |
| `PC-TEMP-MARKET-WINDOW-113` | 시장 | 동작 중 | 최근 공급·수요 12개월 창 | 경제·저장·조회 | 연결 | `EconomyRules.cs`, `PC-ECON-MARKET-013.md` |
| `PC-TEMP-MARKET-LEDGER-113` | 시장 | 동작 중 | 최근 실물 거래 100건 보존 | 저장·조회·메모리 | 연결 | `EconomyRules.cs`, `PC-ECON-MARKET-013.md` |
| `PC-TEMP-MARKET-BOOK-BASE-113` | 시장 | 동작 중 | 공법서·법술서 기준가 4영석 | 경제·게임플레이 | 연결 | `EconomyRules.cs`, `PC-ECON-MARKET-013.md` |
| `PC-TEMP-MARKET-CONTRACT-LIFETIME-113` | 시장 | 동작 중 | Offer 12개월·Reservation 2개월 | 경제·저장 | 연결 | `PC-ATLAS-ECONOMY-017.md` |
| `PC-TEMP-MARKET-ORGANIZATION-CHANNEL-113` | 시장·조직 | 검토 전용 | 조직 권리 연동 전 명시적 비활성 | 게임플레이 gate·UI | 연결 | `PC-ATLAS-ECONOMY-017.md` |
| `PC-TEMP-MARKET-STARTER-DEMAND-019` | 시장 | 동작 중 | 초기 실물 공급 전 수요와 임무 잉여 | 경제·AI | 이식 원장 외 | `PC-CULTIVATION-ACTION-019.md` |
| `PC-TEMP-DEVELOPER-BALANCE-017` | 개발 도구 | 도구·표시 전용 | 개발자 모드 25~400% 세계 배율 | 진단·저장·UI | 이식 원장 외 | `FormulaRegistry.cs`, `PC-ATLAS-ECONOMY-017.md` |
| `PC-TEMP-CONTENT-PLACEMENT-021` | 콘텐츠 | 동작 중 | 초기 비경·재료·아이템 Atlas 배치 | 생성·게임플레이·저장 | 이식 원장 외 | `ContentAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-CONTENT-PC-DEFINITIONS-021` | 콘텐츠 | 동작 중 | PC 간략 아이템 정의와 승인 수량 매핑 | 게임플레이·저장 | 이식 원장 외 | `ContentAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-CONTENT-DISCOVERY-CHANCE-021` | 콘텐츠 | 동작 중 | 이동 5%·사냥 10% 발견 후보 확률 | 게임플레이·RNG | 이식 원장 외 | `ContentAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-KNOWLEDGE-INITIAL-DISTRIBUTION-021` | 지식 | 동작 중 | 60 내용·90 판본·600 실물 초기 분배 | 생성·저장 | 이식 원장 외 | `KnowledgeAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-KNOWLEDGE-RESTORATION-FORMULA-021` | 지식 | 검토 전용 | 복원·재구성 공식 미승인 상태 | 게임플레이 gate·저장 | 이식 원장 외 | `KnowledgeAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-LAW-CANDIDATE-CATALOG-021` | 법칙 | 검토 전용 | 기초 10·파생 44 ordinal 후보 catalog | 조회·UI·저장 | 연결 | `LawAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-LAW-READINESS-GATE-021` | 법칙 | 검토 전용 | 일곱 증거 전 REVIEW 유지 Gate | 권위 gate·검증 | 연결 | `LawAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-LAW-SCOPE-MAPPING-021` | 법칙 | 검토 전용 | Scope·Relation 미확정 매핑 | 권위 gate·UI | 이식 원장 외 | `LawAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-LAW-STABLE-ID-GATE-021` | 법칙 | 검토 전용 | 증거 전 Stable LawId 발급 금지 | 권위 gate·저장 | 이식 원장 외 | `LawAuthorityModel.cs`, `PC-CONTENT-KNOWLEDGE-LAW-021.md` |
| `PC-TEMP-WORLD-GENERATION-021` | 세계 생성 | 동작 중 | 초기·최대 수량과 요수 모집단 기본값 | 생성·RNG·성능 | 이식 원장 외 | `WorldGenerationModel.cs`, `PC-WORLD-GENERATION-021.md` |
| `PC-TEMP-PROCEDURAL-NAME-EFFECT-021` | 세계 생성 | 동작 중 | 이름 token·품질 분포·효과 종류/강도 | 생성·RNG·UI | 이식 원장 외 | `WorldGenerationModel.cs`, `PC-WORLD-GENERATION-021.md` |
| `PC-TEMP-CONTENT-GROWTH-021` | 세계 생성 | 동작 중 | 10년마다 아이템 4종·서적 2종 추가 | 생성·성능·저장 | 이식 원장 외 | `WorldGenerationModel.cs`, `PC-WORLD-GENERATION-021.md` |

## 검토 순서

1. 게임 결과와 RNG에 직접 영향을 주는 `동작 중` 행을 먼저 검토한다.
2. 저장·migration에 포함된 값은 변경 전 고정 fixture와 migration 정책을 만든다.
3. `검토 전용` Gate는 근거가 생길 때까지 실제 효과를 열지 않는다.
4. `도구·표시 전용` 값은 공식 통계와 사용자 세계에 역전파되지 않는지 확인한다.
5. 승인·반려·대체 결정에는 Notion 결정 페이지와 Git SHA를 함께 기록한다.

## 집계 검증

- 고유 검토 ID: **55**
- `pc-port-ledger.json`에 직접 연결: **37**
- 코드·기능 trace에만 존재: **18**
- 0.0.21 고정 SHA 당시 disposition: Port 128 / PC Adapt 15 /
  PC_TEMP_REVIEW 31 / Platform Excluded 0 / Gap·Deferred 64
- 후속 사용자 결정으로 53-table 물리 저장 형태를 PC Adapt 처리한 현재 생성 작업본:
  Port 128 / PC Adapt 16 / PC_TEMP_REVIEW 31 / Platform Excluded 0 /
  Gap·Deferred 63. 이 집계는 새 고정 SHA의 검증 기준선으로 승격됐다는 뜻이 아니다.
