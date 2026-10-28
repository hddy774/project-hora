# 작업 명세 목록

Claude가 작성하고 Codex가 구현하는 작업 명세. 형식과 흐름은 [개발 규칙 §2~3](../standards/development-process.md).
**상태가 `승인`인 작업만 구현한다.** (GitHub 이슈의 `status:ready` 라벨과 같은 의미)

## M0. 기반 (엔진과 무관한 순수 C# 코어)

| ID | 작업 | 선행 | 상태 |
|---|---|---|---|
| [M0-01](M0-01-solution-scaffold.md) | .NET 솔루션·프로젝트 구조 생성 | — | 승인 대기 |
| [M0-02](M0-02-money-quantity.md) | `Money`, `Quantity` 값 타입 | M0-01 | 승인 대기 |
| [M0-03](M0-03-ids-random.md) | 엔티티 ID 타입과 결정론적 난수기 | M0-01 | 승인 대기 |
| [M0-04](M0-04-month-pipeline.md) | 월 정산 파이프라인 골격 | M0-02, M0-03 | 승인 대기 |
| [M0-05](M0-05-ci.md) | CI 워크플로 (format·build·test) | M0-01 | 승인 대기 |

M0-02, M0-03, M0-05는 M0-01 이후 **병렬 진행 가능**하다.
엔진(Unity/Godot) 결정 전에도 M0는 모두 진행할 수 있다.
