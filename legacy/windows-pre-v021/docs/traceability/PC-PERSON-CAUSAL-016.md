# PC 0.0.16 인물·인과·윤회·관계 수직 계약

## 범위

Alpha 1.1.0의 M03, M04, M06, M07 총 68개 요구사항을 PC 의미 단위로 이식한다.
이 문서의 완료는 코드 존재만이 아니라 권위 상태, 전이, checkpoint/hash, 지연 query,
WPF 표시, 결정론·손상 거부·관찰 무영향 검사를 모두 연결한 상태를 뜻한다.

## 권위와 전이

- `PersonLifeAuthority`: 성별, 영혼/육신/현생 상태, 외형 정의·6개 특징, 선천/후천 미모,
  흉터와 typed 생애 전이를 인물별 평탄 배열로 보관한다.
- `AgentCausalRuntime`: Goal→Plan→Task→Action, 인지 사실, Event Definition/Instance/Record,
  Result, root/parent Causal Thread와 현생 기억을 보관한다.
- 기억은 인물당 8개 고정 ring이며 같은 의미의 경험은 행을 늘리지 않고 강도·횟수·최근
  시각을 강화한다. 새 생이 시작되면 현생 기억과 Knowledge Claim을 새 경계로 연다.
- 윤회는 동일 SoulId를 유지하며 새 BodyId/PersonId를 발급한다. 전투 사망의 영혼 손상,
  부모 후보, FamilyId, 후보 부재 장기 대기, 1인 세계 독립 현현을 명시 상태로 처리한다.
- 사망·윤회 진입·도하 완료·환생은 안정 Event Record ID로 한 계보를 이룬다.
- 관계는 일반 대화, 공동 행동, 전투, 사제 Teaching을 typed Result로 남기며 관계 기억과
  Teaching Knowledge Claim이 source result/event/provenance를 참조한다.
- 후속 조직·평판 authority가 아직 없을 때는 0이나 가짜 중립값을 만들지 않고 관계 외부
  맥락을 `DataUnavailable`로 반환한다.

## 저장과 관찰

- authority checkpoint schema는 25다. 생명/외형 SoA, causal store, 관계 최신 result,
  윤회 부모·가계·손상·출생 후보·causal ref를 checksum과 state hash에 포함한다.
- schema 7~24 codec 회귀는 결정론적 생명/인과 기본값을 생성하되 기록에 없던 전생 근거를
  만들어내지 않는다. 새 SQLite 슬롯 loader는 이전 schema를 계속 거부한다.
- 관계·Teaching·윤회·인과 DTO는 해당 인물 상세 Expander/내부 탭이 열렸을 때만 생성한다.
  조회는 RNG, 권위 revision, state hash를 바꾸지 않는다.

## 임시 검토 ID

- `PC-TEMP-AI-UTILITY-113`
- `PC-TEMP-MEMORY-POLICY-113`
- `PC-TEMP-APPEARANCE-DISTRIBUTION-113`
- `PC-TEMP-REINCARNATION-UPPER-DURATION-113`
- `PC-TEMP-REINCARNATION-STAGES-113`
- `PC-TEMP-REINCARNATION-TRACE-113`
- `PC-TEMP-REINCARNATION-BIRTH-CANDIDATE-113`
- `PC-TEMP-REINCARNATION-SOUL-DAMAGE-113`
- `PC-TEMP-RELATION-BALANCE-113`
- `PC-TEMP-TEACHING-BALANCE-113`
- `PC-TEMP-RELATION-EXTERNAL-CONTEXT-113`

모든 임시값은 기능 부재가 아니라 동작하는 PC 계약의 수치·정책 검토 항목이다. 승인된
정본으로 위장하지 않으며 다음 기획 검토에서 revision과 함께 교체한다.

## 자동 검증 진입점

- `Goal→Plan→Task→Action 인과·인지·생별 기억·외형 권위`
- `희소 방향 관계 6축·초기 Role·생애 경계`
- `사제 성립·Teaching 결과·전승 provenance·직접 수행 0%`
- `단계형 윤회·계보·6영역 흔적·schema 22 이전`
- `관찰 조회 무영향`

생성 원장에서는 M03/M04/M06/M07의 `Gap/Deferred`가 각각 0이어야 한다.
