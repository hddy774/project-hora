# 0.0.15 로컬 검증 증거

## 범위와 커밋

- typed authority command/effect/event·원자 rollback·causal thread: `c223cd1`
- Stable Registry·M01~M14 owner map·revision/change 계보: `e979c1b`
- schema 25 SQLite 슬롯·candidate recovery·safe-boundary authority bundle: `50e35c8`
- GitHub Actions / GitHub Release: 실행하지 않음

## 저장 계약

- 새 저장 루트: `%LocalAppData%\ChroniclesOfMyriadImmortalPaths\saves\<slot-id>\world.db`
- 기본 슬롯: `default`
- 실제 SQLite header와 `PRAGMA user_version=25`
- 슬롯 manifest, `world.db` primary, `world.db.bak`, `world.db.tmp` 후보를 분리한다.
- 후보는 SQLite integrity, schema, metadata, payload SHA-256을 먼저 검사한다. 가장 최신의
  검증 후보 하나만 schema 24 authority checkpoint로 decode하고 detached `World`로 복원해
  state hash까지 일치한 뒤 Application이 live world를 교체한다.
- `%LocalAppData%\ChroniclesOfMyriadImmortalPaths\world.cmip`은 읽기·이동·삭제·수정하지
  않는다. 선택 시 `IncompatibleLegacySave`를 반환한다.
- ranking/query/UI cache는 저장하지 않고 복원 시 다시 만든다.

## 자동 검증

```powershell
dotnet build tests/Chronicles.Tests/Chronicles.Tests.csproj -c Release -m:1 /nr:false
dotnet tests/Chronicles.Tests/bin/Release/net10.0/Chronicles.Tests.dll
```

- Release build: 경고 0, 오류 0
- 전체 검사: 48/48 PASS
- typed command optimistic revision, command replay 거부, causal parent/root 보존 PASS
- effect 적용 실패 시 pause/speed/revision/journal 원자 rollback PASS
- Registry 중복 Stable ID·고아 Ref·Revision 충돌·owner 중복 검증 PASS
- 등록 순서를 뒤집어도 stable 해석과 owner lookup 동일 PASS
- 두 슬롯 분리, 실제 SQLite header, manifest, primary/backup/temp 최신 후보 선택 PASS
- 손상 primary → backup, 손상 primary + 최신 temp → temp 복원 PASS
- legacy 파일 exclusive lock 상태에서도 byte와 timestamp 불변 PASS

## 10,000명 SQLite 5회 중앙값

| 항목 | 기존 Brotli/JSON container | schema 25 SQLite | 판정 |
|---|---:|---:|---|
| 저장 | 372 ms | 392 ms | 1초 예산 PASS |
| 복원 | 318 ms | 415 ms | 1초 예산 PASS |
| 저장 live managed peak | 59.61 MiB | 30.61 MiB | 48.7% 절감 PASS |
| payload | - | 3.05 MiB | 64 MiB 상한 PASS |

SQLite writer는 safe boundary에서 이미 계산된 checkpoint+state hash bundle을 받는다.
따라서 저장 중 검증용 `World`를 중복 복원하지 않으면서 임의 checkpoint/hash 조합을
공개 생성할 수 없게 했다. JSON은 전체 해제 byte 배열을 만들지 않고 Brotli로 직접
streaming한다.

## 통합 성능

- 10,000명 초기화 30ms
- 20,000 tick 623ms
- 관찰 0.3ms
- 합계 654ms
- checkpoint 73ms
- schema 24 detached 복원 144ms

## 직접 실행

0.0.15 버전 완료 커밋 `61ec556`의 Windows x64 WPF를 직접 실행해 다음을 확인했다.

- 창 제목 `선로만상록 PC v0.0.15`, 화면 `VERSION 0.0.15`
- 인구 10,000명과 년·월·일이 1×에서 자연스럽게 실시간 진행
- 게임 UI의 저장 버튼으로 `saves\default\world.db`와 `slot.json` 생성
- SQLite 파일 3,559,424 bytes, header `SQLite format 3\0`
- 게임 UI의 불러오기 버튼으로 저장 시점의 authority world 복원 후 실시간 진행 재개
- 기존 `world.cmip` 35,343,390 bytes의 수정 시각은 2026-08-31 10:56:07로 유지되어
  새 저장 시각 2026-09-01 08:59:35에 변경되지 않음
- 검증 뒤 게임 창을 정상 종료하고 동일 제목 창 0개 확인
