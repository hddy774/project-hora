# PC 0.0.21 공통 권위 Registry 수직 계약

## 범위

- 요구사항: `A110-M01-V01`~`A110-M01-V17`
- 구현 방식: Alpha 1.1.0 M01의 안정 식별·권위 owner·revision·mapping 의미를 PC Core와 Application 진단 표면으로 이식한다.
- 물리 DB column 확장은 M01의 명시적 후속 범위이므로 새 저장 wire를 만들지 않는다. 외부 `WorldSchema 25`와 checkpoint schema 26은 기존 Persistence 계약을 유지한다.

## 권위·진단 계약

- `AuthorityRegistry`는 표시명과 분리된 FNV 기반 `RegistryStableId`를 생성한다.
- Domain, Target, Semantic State, Unit, Operation, Owner, Schema, Mapping, Formula 종류를 구분한다.
- 항목은 active/deprecated/retired, 도입·폐기 revision, replacement를 가진다.
- M01~M14 각 domain은 한 개의 primary owner, 활성 schema·mapping, 단일 mutation operation에 연결된다.
- 등록 순서와 표시 순서는 ID 해석에 영향을 주지 않는다.
- 미등록 ID는 `Undefined`, 손상 Registry는 `Blocked`이며 기본값이나 READY로 보충하지 않는다.
- 중복 ID·key·owner, 고아 연결, 잘못된 revision·replacement·ChangeSet sequence는 정적 생성 시 거부한다.

## 읽기·UI 계약

- `AuthorityRegistryDiagnosticsProjection`은 Core Registry와 Formula Registry에서 불변 DTO를 프로세스당 한 번만 만든다.
- Desktop은 Application DTO만 받아 설정의 `권위 진단` 내부 탭에 contract, revision, READY/BLOCKED, 활성 schema·mapping을 표시한다.
- M01~M14 owner와 공식 Stable Ref는 기본 접힘 상태의 토글에서만 상세 표시한다.
- 세계 pulse, RNG, 권위 상태, 저장 hash는 진단 조회로 변경되지 않는다.

## 검증

- `공식 Registry·typed command·원자 transaction·causal event`
  - M01~M14 owner 14개와 owner 중복 0
  - 고아 schema·mapping·owner 0
  - 등록 순서 역전 후 동일 해석
  - 미등록 ID `Undefined`
  - 진단 DTO 참조 재사용 및 공식 Stable Ref 전수 연결
- WPF Release build: 경고 0, 오류 0

## 이식 판정

- V01~V11, V13~V17: `Port`
- V12: `Port` disposition + Windows WPF 개발자/진단 UI `PC Adapt`
- `Gap / Deferred`: 0
