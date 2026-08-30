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
            └── Chronicles.Desktop

Chronicles.Tests ──> Core + Simulation
```

### Chronicles.Core

안정 Agent ID, 정수 simulation tick, 고정된 정의 ID, 결정론적 난수와 전투력 값
타입을 소유한다. UI, 파일 시스템과 WPF를 참조하지 않는다.

### Chronicles.Simulation

10,000명 권위 상태, AI 결정, 수행·돌파, 공법·법술, 전투 타임라인, 전투력과
랭킹을 소유한다. 렌더 프레임과 벽시계에 의존하지 않는다.

### Chronicles.Desktop

SimulationRunner가 게시한 불변 스냅샷만 읽는다. 일시정지, 배속, 검색과 추적
명령을 전달하고 WPF 화면을 그린다.

### Chronicles.Tests

외부 테스트 프레임워크 없이 실행 가능한 결정론적 계약 러너로 시작한다. 공식,
랭킹, 경쟁 활성도, 저장 왕복과 10,000명 성능 예산을 자동 검증한다.

## 실행 모델

시뮬레이션은 전용 worker에서 고정 tick으로 실행한다. 각 AI는 매 tick 전부 판단하지
않고 `nextDecisionTick` 또는 예약된 전투 행동 시점에만 활성화된다. 화면은 제한된
주기로 생성한 불변 조회 스냅샷을 받는다.

정렬용 전투력 랭킹은 구성 요소 변경이 발생한 인물만 갱신하고, 전체 정렬은 UI 요청과
공식 랭킹 갱신 경계에서 수행한다. 구현 초기에는 정확성을 우선하고 성능 측정 결과에
따라 순위 자료구조를 최적화한다.
