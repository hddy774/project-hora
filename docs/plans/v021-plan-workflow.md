# 0.2.1 기계 원장 작업 방법

기획 정본은 [Notion 통합 페이지](https://app.notion.com/p/408acc9e6175817ab59bd91bb9dc011d)다. JSON은 개정6·111작업의 기계 미러이며 별도의 기획 정본이 아니다. Notion 표/JSON/INV를 같은 변경에서 맞춘다.

- `v021-tasks.json`: 전체 작업 의존성·상태·마일스톤·최소/후속 범위·Q·INV·제안 시험·V020 대응.
- `v021-invariants.md`: 24개 불변 계약과 미구현 게임 시험명.
- `v021-review-resolution.md`: FB-01~25의 판정·이유·미결 정책.
- `reviews/v021-claude-review-2026-10-30.md`: 사용자 제공 수신 원문, UTF-8/LF·종단 개행1개. SHA256은 JSON source에 기록한다.
- `../../scripts/validate-v021-plan.py`: Python 3.10 이상·표준 라이브러리만 사용하는 원장 검사. 게임 언어는 계속 C#이다.

## 실행

저장소 루트에서:

```bash
python3 scripts/validate-v021-plan.py --self-test
```

선택적으로 Notion MCP가 반환한 Markdown 본문 snapshot을 UTF-8 파일로 저장한 뒤:

```bash
python3 scripts/validate-v021-plan.py --self-test --notion-snapshot path/to/notion-export.md
```

Notion 검사는 6열 작업표의 ID·산출물·전체 선행·수용/INV·전체 완료 마일스톤·차단 Q를 비교한다. 접기 영역이 모두 포함된 본문을 사용한다. 스크립트는 live Notion을 읽거나 외부 서비스에 쓰지 않는다. CI는 저장소 JSON/INV/원문 hash와 부정 시험을 검사하며 live Notion 동기화를 보장하지 않는다.

검사 대상은 111개 작업·107개 기존 ID/신규4개·55개 원래 요구·선행 존재/순환·모든 작업의 T06 연결·Q/INV/마일스톤/부분 출력 참조·원문 hash·명시적 상태다. 수가 의도적으로 달라지는 후속 개정에서는 정본·미러·검증 기준을 함께 개정한다.

## 단계·차단·증거

`deps`는 전체 완료 선행이다. M1 `slices`의 선행은 부분 산출물끼리의 별도 DAG이며 `completes_task=false`다. 부분 출력 통과를 전체 작업 완료로 복사하지 않는다. 현재 모든 작업/부분 출력은 planned, evidence는 비어 있다.

`blocked_by_q`는 정책 민감 수용 시점의 직접 결정 게이트다. 독립 계약·문서·가정별 연구는 할 수 있지만 미결 기본값/해당 수용 결과는 확정하지 않는다. 상위 작업의 열린 결정은 전체 선행을 통해 후속 완료에도 영향을 준다. 선택인 엄격 플레이는 기존 기본 모드 작업을 차단하지 않는다.

모든 게임 시험명은 제안/미구현이다. Python 원장 검사와 그 부정 시험이 통과해도 T07 제품 하네스·게임 저장/수행/AI 시험·Android/Unity·성능 통과를 뜻하지 않는다. 새 도메인 상태는 T07 왕복/INV 증거를 기능별로 붙인 후 D02/T01 최종 감사에서 모은다.

현재 작업은 문서·기계 원장·검증기/CI에 한정하며 src·제품 테스트·VERSION을 변경하지 않는다. 기존 AGENTS의 제품 경계와 현재 사용자 요구의 정합화는 A02의 후속 기능 착수 계약이다.
