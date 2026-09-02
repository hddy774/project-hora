# 개발 규칙

## 제품 경계

- 플레이어는 세계에 개입하지 않는 관찰자다.
- 10,000명의 AI는 모두 최종적으로 전투력 랭킹 1위를 목표로 한다.
- 게임의 핵심 권위 시스템은 수행과 전투이며, 랭킹·도시 경제는 두 시스템의 성장과
  경쟁을 연결한다.
- 문파·관계망처럼 승인되지 않은 대형 시스템은 추가하지 않는다.

## 의존 방향

```text
Core -> Simulation -> Queries -> Runtime
                  \-> Persistence
Queries -> Presentation
Application = Runtime + Persistence + Queries 조정
Desktop = Application + Presentation
```

- `Core`: 안정 ID, 정수 시간, 결정론적 난수, 값 타입과 정의 ID.
- `Simulation`: AI 상태, 수행, 경지, 공법, 법술, 전투, 전투력, 랭킹, 일대기·윤회와
  도시 시장 경제.
- `Queries`: simulation을 읽는 불변 관찰 DTO와 revision 기반 투영.
- `Runtime`: worker, 배속, 관찰 요청과 변경 section 통지.
- `Persistence`: versioned save container, checksum과 원자 저장.
- `Presentation`: 플랫폼 중립 표시 모델과 한국어 포맷.
- `Application`: runtime·query·persistence 조정과 세션 생명주기.
- `Desktop`: Application과 Presentation만 직접 참조하는 WPF 관찰 셸.
- `Tests`: 공식, 결정론, 경쟁 활성도, 성능과 저장 회귀 검증.

UI는 시뮬레이션의 가변 상태를 직접 소유하거나 수정하지 않는다. 관찰·검색·정렬은
세계 상태와 난수 소비를 바꾸지 않아야 한다. 현재 파일 owner와 허용 의존은
`docs/architecture/repository_manifest.json`을 정본으로 사용한다.

## 코드 구조와 간결성

- 같은 책임·변경 생명주기·검색 작업집합은 한 대표 파일에 모으되, 서로 다른 도메인이나
  저장·query·UI 책임을 한 거대 파일에 누적하지 않는다.
- 수동 작성 파일은 1,000줄 안팎을 권장한다. 초과 자체를 실패로 만들지는 않지만 새 변경이
  기존 대형 파일을 키운다면 먼저 응집된 기능 단위 추출을 검토한다.
- 한두 메서드만 담는 미세 파일, 범용 `Helper`/`Manager`, 중복 wrapper로 줄 수만 줄이지
  않는다. 분리 뒤 대표 진입점과 owner가 더 선명해야 한다.
- 반복 순회·임시 배열·불필요한 LINQ materialization·매 pulse 재계산을 피하고, hot path는
  연속 저장소·revision/cache·명시적 bounded buffer를 우선한다.
- 조건식과 메서드는 짧고 검색 가능하게 유지하되, 의미를 숨기는 축약·중복 계산·복잡한
  한 줄 표현보다 검증 가능한 이름과 단일 책임을 우선한다.
- 리팩터링은 게임 규칙·RNG 순서·저장 wire 변경과 분리하며, 전후 결정론·전체 테스트와
  해당 성능 수치를 비교한다.

## 시뮬레이션 규칙

- 세계 시간은 정수 tick만 사용하고 벽시계 시간을 권위 상태에 저장하지 않는다.
- AI는 매 렌더 프레임이 아니라 행동 완료·중단 시점에 다음 결정을 내린다.
- 난수는 명시적 seed와 안정된 stream으로만 소비한다.
- 전투력은 경지·자질·공법·법술·전투 경험·법보의 기여도를 각각 계산하고 조회에 공개한다.
- 동일 seed와 동일 명령은 동일 상태 해시와 동일 랭킹을 만들어야 한다.

## 변경과 검증

- `VERSION`이 애플리케이션 버전의 유일한 원본이다.
- 사용자 기능 업데이트를 시작할 때 `scripts/bump-patch-version.ps1`을 한 번 실행해 오른쪽
  patch 숫자만 자동 증가시킨다. major와 minor는 사용자 결정 없이 바꾸지 않는다.
- 기능 단위로 테스트한 뒤 작은 커밋을 만들고 원격 개발 브랜치에 푸시한다.
- 생성물과 `bin/obj/artifacts`는 커밋하지 않는다.
- 릴리스 전에는 Release 빌드, 전체 테스트, 10,000명 성능 검증과 패키지 실행 확인을 수행한다.
