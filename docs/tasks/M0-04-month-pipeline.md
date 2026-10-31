# [M0-04] 현재 명령·논리월·후보 원자성 계약

## 실제 인터페이스

`GameWorld.Create(ulong, IReadOnlyDictionary<string,long>?)`, `Execute(GameCommand)`,
`AdvanceTo(long)`, `Checkpoint()`, `StateHash()`가 현재 진입점이다.
메모리 저장은 `GameCodec.Encode/Decode`, 실제 파일은 Hosting의 `GameSaveStore.SaveAtomic/Load`가 맡는다.

## 동작 계약

- 세계는 정수 논리월로 진행하고 현재 완료/취소/월 정산 순서를 보존한다.
  관찰/직접 제어/AI 인계는 같은 세계와 실제 예약/행동을 사용한다.
- 명령은 현재 후보 트랜잭션에서 검증 후 받아들이고 실패 시 원본·RNG·ID·돈/실물·예약을 유지한다.
- 공개 full-copy/strict 계약과 private opaque owned 경로를 구분한다.
  private 후보의 alias/소유 토큰·stale fingerprint·전후 검증을 임의로 생략하지 않는다.
- 세션 직렬화·파일 I/O는 Hosting 경계에서 수행하고 UI는 가변 GameState를 소유하지 않는다.
  시간/원인 기록은 실제 순서와 영수증으로 설명한다.

## 수용 기준과 확인

- [ ] 명세에 변경되는 완료/시작/취소 hook 순서·예약 생애·거부 조건을 적는다.
- [ ] 실제 중간상태 저장/재개·재전송·동일 입력 분할 진행의 결과를 필요한 시나리오로 대조한다.
- [ ] 명령 거부와 후속 hook 실패가 부분 권위 변경을 남기지 않는다.

선행: [M0-02](M0-02-money-quantity.md), [M0-03](M0-03-ids-random.md).
현재 구현을 이름만 다른 WorldState/MonthPipeline으로 재작성하거나 clone/검증을 일괄 제거하는 작업이 아니다.
