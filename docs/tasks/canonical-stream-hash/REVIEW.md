# 검토 안내와 실제 검증

## 바뀐 경로

`GameCodec.Hash`는 같은 `JsonSerializerOptions`로 `JsonSerializer.Serialize(Stream, ...)`를 호출하고,
호출이 소유하는 `CanonicalHashSink`에 canonical UTF8 바이트를 순서대로 전달한다. sink는 그 바이트를
`IncrementalHash(SHA256)`에 추가하고 기존 소문자 hex 문자열을 반환한다. 전체 JSON 배열이나 세계를
보관하지 않으며, `using`은 정상 완료와 serializer 예외 모두에서 자원을 해제한다.

`Clone`, `CommandHash`, serializer 옵션, `Encode/Decode`, `JsonPayloadLimit=128MiB`, 파일 저장 경로는
이 PR에서 변경하지 않았다. [범위 대조](scope-comparison.json)는 Hash 본문만 원래 문장으로 되돌리면
`GameCodec.cs` 전체가 기준 source와 바이트 동일하고, `GameSaveStore.cs`도 동일함을 확인한다.

## 현재 제품 코드의 검사

- 빌드/실행 source: 깨끗한 로컬 `5723b19032c210fe69e457dbf7ffb19000195c6d`.
  이후 증거·문서 추가는 제품 파일을 바꾸지 않는다. GitData 게시 commit의 metadata SHA는 별개이며,
  게시 시 실제 원격 tree/파일 바이트를 독립 대조한다.
- CPU3, SDK10.0.403, runtime10.0.14, `TieredPGO=false`, 공유 호스트.
- Release 빌드: 경고0·오류0. 정확한 명령·실제 네 DLL 해시와 source revision 문자열은
  [검증 요약](verification.json)과 [실행 원문](checks/functional-run.json)에 있다.
- 기능 선택 **10/10**, 새 사례3개와 기존 사례7개다. 전체 기능 실행·새 성능·성장·native 실행은0이다.

| 실제 filter | 통과 | 확인한 계약 |
| --- | ---: | --- |
| `canonical streaming hash:` | 3/3 | 기존 buffered oracle, Unicode/escape/잘못 짝지은 surrogate/null, en-US/tr-TR/ko-KR, 기존 N100 기준 해시, 큰 payload, 직렬화 실패·다음 호출, 상태/RNG/ID |
| `대용량 저장:` | 4/4 | 수정 전 schema2 고정 bytes·기존 양형식·순번·재전송/RNG, 손상 거부, 실제 파일 pending/backup/원문 보존 |
| `host recovery:` | 1/1 | 호스트 복구·기존 파일 보존·실제 재실행 |
| `저장 왕복·호스트 문화권 독립` | 1/1 | 저장 후 문화권 변경과 명령 식별자 |
| `INV-006/007/004 시간 분할·조회/저장 무영향` | 1/1 | 기존 240개월 기능 시나리오의 시간 분할·조회·저장 후 같은 최종 상태 |

[제품 경계](checks/product-boundary.json), [정본 정적 검사](checks/plan.json),
[릴리스 보호 self-test](checks/release-guard.json)와 diff whitespace도 통과했다.
공유 validator나 불변식 실행 맵을 수정하지 않았다. 기존 Name은 그대로이며, 새 Name3개와
`INV-004/006` 맵 반영 필요사항은 `verification.json.mapping_followup`에 적었다. 최종 통합 source와
전체 실행 결과를 연결하는 작업은 통합 담당자가 수행한다. 이번 선택10개를 전체 통과로 쓰지 않는다.

## 선행 측정의 범위

[선행 원문](diagnostic/result.original.json), [요약](diagnostic/summary.original.json),
[실제 보존 파일/SHA](diagnostic/index.json)는 `df7240556a260202a248e57b1f9868e372b9d995`에서 수집했다.
기존 synthetic N100/N1000의 Month0이며 AdvanceTo를 수행하지 않았다. 준비 포함 고정10분 안에 끝났고
실제 자식 실행은19.167초였다. 당시 네 DLL은 루트의 고정 Release 원문 SHA와 일치했다.

| 당시 N1000 진단 | 호출당 current-thread 할당 | 시간 중앙값 |
| --- | ---: | ---: |
| 기존 buffered `Hash` | 15,447,208 B | 45.6181 ms |
| 당시 scratch stream-hash 후보 | 580,464 B | 42.6253 ms |

두 후보 모두 같은 canonical 해시였고 stream 바이트 oracle도 일치했다. 표는 **수정 전 소스의 기존
구현과 당시 scratch 후보**를 고정 순서로 대조한 값이다. 새 제품 sink의 최종 시간 개선률을 측정한
것이 아니다. 공유 호스트·warm/cold 혼합·소수 표본 한계를 유지한다. allocation은 retained heap,
RSS 또는 ArrayPool root의 증거가 아니다. 당시 Encode/Clone/파일 저장 진단도 원문에 보존했지만
그 경로나 기준을 이 PR의 제품 변경으로 확장하지 않았다.

## 자체 검토와 후속

Codex가 본문/예외 경로·source 바이트 대조·선택 기능 결과를 자체 검토했다. 실제 외부 Claude
검토를 새로 수신했다고 표시하지 않는다. 현재 자체 검사에서 발견한 미해결 결함은 없다.
통합 담당자의 독립 리뷰, 새 전체 기능 실행과 최종 성능 검증은 별도이며 기존 FP0cb2f90f 증거를
새 제품 source의 결과로 재표시하지 않는다. [PR84 피드백](../../plans/reviews/v021-claude-pr84-review-2026-10-31.md)과
[버전별 리뷰 안내](../../reviews/v0.2.1-review-guide.md)를 함께 읽을 수 있다.
