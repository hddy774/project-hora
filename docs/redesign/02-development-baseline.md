# 선로만상록 개발 기준과 계약 탐색

## 제품과 실제 구현

관찰과 플레이는 같은 `GameWorld`를 쓰고 AI도 같은 명령을 실행한다.
정확히 두 반선 시조, 종문0 시작, 실제 생계/실물/화폐·수행·전투·관계·윤회가 현재 정본 범위다.
사용자 결정·잠정값 보정 위임은 [AGENTS.md](../../AGENTS.md)와 정본을 따른다.

| 확인할 계약 | 실제 진입점 |
| --- | --- |
| 버전·순수 기본형 | `Chronicles.Core/ProductInfo.cs`, `DeterministicRandom.cs` |
| 단일 세계/명령/시간 | `Chronicles.Game/World.cs`의 Create/Execute/AdvanceTo, 관련 partial |
| 상태·법칙/단위 | `Chronicles.Game/State*.cs`, `Laws.cs`, 각 도메인 모델 |
| 저장 본문/봉투 | `Chronicles.Game/GameCodec.cs` |
| 실제 파일·세션 직렬화 | `Chronicles.Hosting/GameSaveStore.cs`, `GameSession.cs` |
| 개인 관측·화면 DTO | Game 조회와 공용 화면 자산·각 host bridge |

경로는 `src/` 기준이다. 상세 owner/허용 참조는
[제품 경계](../architecture/v021-product-boundary.md)와 [파일 manifest](../architecture/repository_manifest.json)를 따른다.
호스트가 private 세계를 소유하고 Game 밖에서 별도 시뮬레이션/실물 장부를 실행하지 않는다.

## 명세를 작성할 때 고정할 것

1. 실제 actor·몸/혼·물품·장소의 식별자와 권한/개인 관측 범위
2. 입력 단위·0/최소/최대·부족/경합/중복 명령과 거부 원자성
3. 노동/기간·예약 시작/소비/해제·RNG/영수증·저장 중간상태
4. 실제 성공/실패 관측값과 등록 검사·소스/SDK·원문 경로

현재 계약에 없는 새 값 타입·엔진·시스템 파이프라인을 이름만 바꾸어 도입하지 않는다.
대체 설계가 필요하면 문제/대안/호환 조건을 별도 명세에 적고 검증한다.

## 문서의 권위

`docs/tasks/M0-*`는 이미 있는 기반 계약을 읽고 필요한 변경을 구체화하는 명세다.
실행 상태는 정본111개 원장과 실제 근거를 사용하며 M0 목록으로 진행률을 다시 계산하지 않는다.
공통 서식/도구 복원과 게임 기능·최종 성능/서명·릴리스 수용은 구분한다.
