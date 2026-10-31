# [M0-01] 실제 솔루션·의존·권위 기반 계약

## 목표와 범위

기존 `Chronicles.slnx`와 현재 어셈블리 경계를 읽고 변경이 필요한 작업을 명세한다.
이미 있는 구현에 빈 placeholder나 다른 제품의 프로젝트를 다시 생성하지 않는다.

## 계약

`Desktop/Android/Shell → Hosting → Game → Core`, `Game.Tests → Game/Hosting`이다.
Core는 순수 버전/명시적 RNG, Game은 단일 세계와 메모리 codec,
Hosting은 직렬화 세션·실제 파일 저장/복구, 각 host는 UI·수명 주기를 소유한다.
namespace가 같아도 실제 assembly owner를 [manifest](../architecture/repository_manifest.json)에서 확인한다.
SDK10.0.403·현재 net10 계열·언어 설정은 루트 파일을 따른다.

## 수용 기준과 확인

- [ ] 실제 프로젝트 참조가 [제품 경계](../architecture/v021-product-boundary.md)와 일치한다.
- [ ] Game/Core에 파일 경로·UI/호스트 역참조가 생기지 않는다.
- [ ] engine-free 변경은 Release 빌드와 `validate-v021-product-boundary.py`로 확인한다.
- [ ] 저장/행동을 바꾸면 관련 실제 `IFeatureScenario`의 명령·Name과 근거를 별도 명세에 적는다.

기존 기반 확인 명세이며 정본 수용 완료 표가 아니다. 선행: 공통 SDK/작업 환경.
