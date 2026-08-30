# 기술 아키텍처

## 선택 기술

- 언어: C#
- 런타임: .NET 10
- PC UI: WPF
- 대상: Windows x64
- 배포: self-contained 단일 폴더 패키지

## 모듈

```text
Chronicles.Core
    └── Chronicles.Simulation
            ├── Chronicles.Persistence
            │       └── Chronicles.Desktop
            └──────────── Chronicles.Desktop

Chronicles.Tests ──> Core + Simulation
```

### Chronicles.Core

안정 Agent ID, 정수 simulation tick, 0.01 단위 공통 품질값과 경계 배율, 고정된 정의 ID, 결정론적 난수와 전투력 값
타입을 소유한다. UI, 파일 시스템과 WPF를 참조하지 않는다.

### Chronicles.Simulation

10,000명 권위 상태, AI 결정, 수행·돌파, 공법·법술 품질과 숙련, 전투 타임라인, 전투력과
전투력·경지·자질 랭킹, 전체 인물 탐색, 생애 일대기, 수명 판정과 윤회를 소유한다. 렌더 프레임과 벽시계에 의존하지
않는다. 안정 Agent ID는 영혼 슬롯으로 유지하고 생 번호·이름·탄생 tick으로 현생을
구분한다.

### Chronicles.Desktop

SimulationRunner가 게시한 불변 스냅샷만 읽는다. 일시정지, 배속, 검색과 추적
명령을 전달하고 WPF 화면을 그린다.

### Chronicles.Persistence

Simulation의 명시적 체크포인트를 Brotli 압축 payload와 SHA-256 checksum을 가진
versioned `.cmip` container로 변환한다. 같은 폴더의 임시 파일을 flush한 뒤 원자적으로
교체하며, 손상되거나 지원하지 않는 schema는 세계를 바꾸기 전에 거부한다.

### Chronicles.Tests

외부 테스트 프레임워크 없이 실행 가능한 결정론적 계약 러너로 시작한다. 공식,
랭킹, 경쟁 활성도, 저장 왕복과 10,000명 성능 예산을 자동 검증한다.

## 실행 모델

시뮬레이션은 전용 worker에서 고정 tick으로 실행한다. 각 AI는 매 tick 전부 판단하지
않고 `nextDecisionTick` 또는 예약된 전투 행동 시점에만 활성화된다. 화면은 제한된
주기로 생성한 불변 조회 스냅샷을 받는다.

전투력·경지·자질 순위 배열은 공식 갱신 경계에서 결정론적 동률 규칙과 함께 정렬한다.
UI에는 범주마다 상위 1,000명만 복사하고 전체 탐색은 안정 Agent ID 기반 페이지 조회를
사용한다. 구현 초기에는 정확성을 우선하고 성능 측정 결과에 따라 순위 자료구조를
최적화한다.
