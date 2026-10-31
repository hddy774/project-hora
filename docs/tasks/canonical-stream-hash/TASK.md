# canonical JSON 해시의 임시 전체 바이트 배열 제거

## 배경·승인·출처

- 이슈: [#233](https://github.com/hddy774/project-hora/issues/233), 상위 PR84.
- 구현·명세: Codex. 외부 Claude 검토를 수행했다고 표시하지 않는다.
- 기준 source: `b15992b6150afb8e4f3348501c51051a7f7733d0`.
- 선행 진단: `df7240556a260202a248e57b1f9868e372b9d995`, SDK10.0.403/runtime10.0.14,
  CPU3의 기존 N100/N1000 Month0 synthetic fixture. 이 진단의 완료와 실제 측정값은
  `diagnostic/`의 원문·요약·SHA로 구분한다. 새 제품 코드의 최종 성능 결과로 쓰지 않는다.
- 통합 담당자가 진단 후보1인 `GameCodec.Hash`의 stream hashing만 제품 패치로 승인했다.

## 범위와 소유

- `src/Chronicles.Game/GameCodec.cs`의 `Hash` 본문, 비파일 SHA-256 sink 파일1개,
  새 canonical 호환성 기능 시나리오와 이 작업 문서만 소유한다.
- `Clone`, `CommandHash`, `Encode/Decode`, binary 선택 한계, `GameSaveStore`, 정책/게이트,
  세계 상태·공개 시그니처·프로젝트 설정·원장·기존 증거는 변경하지 않는다.
- 전체 기능 및 최종 성능 검증은 통합 후 루트가 별도로 수행한다.

## 계약

| 항목 | 보존할 계약 |
| --- | --- |
| 입력/출력 | `GameCodec.Hash(GameState value)` → 기존 canonical UTF8 JSON 바이트의 소문자 SHA-256 문자열 |
| serializer | 같은 `Options`, 속성 순서·정수·null·Unicode/escape·문화권·MaxDepth 계약 유지 |
| 자원 | 호출이 소유하는 비파일 write-only stream/IncrementalHash. 완료·실패 모두 Dispose, 공유 캐시/가변 세계 없음 |
| 실패 | 기존 serializer의 예외 종류·메시지를 변환/삼키지 않음. null 입력도 기존 `null` JSON 해시 유지 |
| 시간/RNG | 세계·RNG·ID·전이 순서·명령 서명에 쓰기를 하지 않음 |
| 저장/호환 | 기존 저장 bytes/schema/checksum·재전송·복구 경로 불변. 기존 Hash 호출자만 내부 계산 방식 변경 |

## 수용 기준

1. 기존 buffered serializer oracle와 stream hash가 Unicode·escape·문화권·null·실패 경계에서 같다.
2. 기존 실제 N100 canonical 기준 해시와 같고, serializer 기본 버퍼보다 큰 payload도 같다.
3. 해시 반복/실패 후 새 호출에서 상태와 RNG가 그대로다.
4. 기존 대용량 양형식/고정 bytes·손상/원자 복구, 호스트 복구·문화권·결정론 선택 검사가 통과한다.
5. CPU3 Release 빌드는 경고/오류0. 제품 경계/정본 정적 검사와 diff whitespace가 통과한다.

## 검사·증거·리뷰

실제 선택 Name·명령·결과·소스 및 새 Name의 불변식 맵 후속 필요 여부는 `REVIEW.md`와
`verification.json`에 남긴다. 검사는 `taskset -c 3`, `-m:1`, `UseSharedCompilation=false`로 격리한다.
새 성능·성장·장기·전체 기능 실행은 이 PR에서 하지 않는다. 이전 진단의 allocation은 retained heap,
ArrayPool root 또는 공유 호스트의 최종 시간 개선률을 증명하지 않는다.

리뷰는 [개발 절차](../../standards/development-process.md),
[코드 규칙](../../standards/coding-conventions.md),
[리뷰 체크리스트](../../standards/review-checklist.md)와
[PR84 피드백 수신](../../plans/reviews/v021-claude-pr84-review-2026-10-31.md)을 따른다.
