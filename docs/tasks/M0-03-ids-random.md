# [M0-03] 실제 신원·저장 난수·재전송 기반 계약

## 목표와 실제 모델

사람/육신/혼·물품·계약·명령 ID는 각 도메인의 실제 의미를 유지한다.
배열 슬롯/세대와 영속 ID를 혼동하지 않고 기존 NextId 발급·영수증 검증 순서를 보존한다.
Core의 `DeterministicRandom`은 SplitMix64이며 Game은 저장된 RandomState와 현재 helper를 사용한다.
예전 main의 PCG32 명세나 다른 ID 구조체를 새 계약으로 가져오지 않는다.

## 동작 계약

- 같은 seed·같은 실제 명령 순서·같은 법칙은 같은 권위 상태/RNG 결과를 만든다.
- 조회/정렬/필터/개인 관심 표시·저장만으로 세계 난수를 소비하지 않는다.
- 명령 ID 재전송은 같은 결과로 처리하고, 같은 ID/다른 payload는 실제 충돌로 거부한다.
- 게임 전이에 벽시계·임의GUID·프로세스 해시/사전 순서를 섞지 않는다.
  테스트 입력 ID나 호스트 진단은 권위 RNG와 구분한다.

## 수용 기준과 확인

- [ ] ID 종류·발급/재사용·실패 시 NextId/RNG 불변을 명시한다.
- [ ] 같은 입력 대조와 저장/복원 뒤 실제 다음 명령의 hash/RNG·receipt를 확인한다.
- [ ] 조회 무영향·payload 충돌 거부를 관련 INV-004/006/008 시나리오에 연결한다.

경로: `src/Chronicles.Core/DeterministicRandom.cs`, `src/Chronicles.Game/World.cs`,
`src/Chronicles.Game/State*.cs`, `src/Chronicles.Game/GameCodec.cs`.
선행: [M0-01](M0-01-solution-scaffold.md). 난수 알고리즘 교체/분포 측정은 별도 명세다.
