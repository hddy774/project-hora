# 대용량 저장 봉투 수정과 선택 기능 근거

N10,000 원본은 12개월 진행 뒤 저장 단계에서 실패했다. 세계 JSON을 `Envelope.Payload`의 단일 문자열로 다시 직렬화하면서 길이 398,228,383이 런타임의 문자열 토큰 상한을 넘었다. 원본은 [9263756 실패 보존 자료](https://github.com/hddy774/project-hora/blob/f47aa3f0eef1bc3c45f5a01a9003691f472c5a33/docs/evidence/0.2.1/baseline-9263756-save-failure/README.md)에 그대로 남아 있으며 이번 자료는 그 실행을 성공으로 바꾸지 않는다.

수정 기준은 `9704e95508456ee317a378a9b8dac55985bf1849`, 선택 기능 검사를 실행한 소스는 `c6e470458ecefe055d98f89120f08c40c0c0660d`다. 제품 소스 지문은 `dcc95b5cb52a0d3b8e6b66975b736a685f4a82250508a2434d29563bd7b9cfc0`이며, [provenance.json](provenance.json)에 명령·CPU0 고정·Release DLL 네 개의 버전/크기/SHA·기준 소스와 실패 원본 SHA를 기록했다. 이 폴더에 DLL이나 대용량 저장 바이너리를 첨부하지 않는다.

canonical `GameState` UTF8 본문이 **128MiB 이하**이면 기존 schema2 JSON 봉투를 쓴다. 이를 넘으면 아래 64바이트 헤더와 UTF8 원문을 연속 저장한다. 상태의 `DomainSchema`, 게임 법칙, `VERSION`은 바뀌지 않는다. 기존 schema2 저장을 계속 읽고, 기존 미지원 개발 상태 및 schema1 JSON 봉투를 계속 거부한다. 버전3은 대용량 **봉투 형식**이며, schema3 JSON 봉투를 허용하는 변경은 아니다.

| 오프셋 | 크기 | 버전3 이진 봉투 내용 |
| --- | --- | --- |
| 0 | 12B | ASCII `SEONRO-021B`와 마지막 NUL |
| 12 | 4B | little-endian int32 버전, 정확히 3 |
| 16 | 8B | little-endian int64 순번, 0 이상 |
| 24 | 8B | little-endian int64 본문 바이트 수, 양수이며 `Array.MaxLength - 64` 이하 |
| 32 | 32B | SHA-256: 문화권 독립 십진 순번 + LF + canonical UTF8 본문 |
| 64 | 명시된 크기 | canonical `GameState` UTF8 JSON 원문 |

체크섬은 기존과 같은 논리 입력을 증분 계산한다. 큰 본문은 중간 UTF16 문자열을 만들지 않고 읽는다. 선언 길이는 남은 파일 바이트 수와 정확히 같아야 하며 버전·순번·체크섬·잘림·후행 자료를 검증한 뒤 세계 불변식을 검사한다. 암호학적 발신자 인증이나 스트리밍 저장으로 바꾸는 작업은 아니다.

| 실제 실행 범위 | 결과 | 원문 |
| --- | --- | --- |
| 고정 소스 Release 빌드 | 경고 0, 오류 0 | [빌드 로그](release-build.log) |
| `--filter INV-004` 기존 저장 관련 검사와 신규 4개 | 74/74 통과 | [보고서](storage-functional.json), [로그](storage-functional.log) |
| `--filter 'inventory copy oracle'` | 독립 엄격복사(strict)·JSON15·형식화복사(typed) 전사 1/1 통과 | [보고서](copy-oracle-functional.json), [로그](copy-oracle-functional.log) |

신규 4개 검사는 작은 실제 세계에서 내부 임계값을 전달하여 큰 봉투와 정확한 경계 분기를 실행했다. 이 검증용 값은 공개 명령·UI 설정·저장 상태·전역 변수로 노출되지 않는다. 기존 `GameSaveStore`의 실제 원자파일 경로를 그대로 거친다.

- 수정 전 9704 Release DLL을 먼저 읽어 seed191, 순번0/7의 기존 봉투 길이(각 1,887,246B)와 SHA를 확보했다. [원문](before-9704-golden.log), [별도 검증 소스](before-9704-probe.cs.txt), [프로젝트 참조](before-9704-probe.csproj.txt)를 보존한다. 새 검사에서는 독립 기존 encoder가 이 고정 SHA를 재현하는지 확인한 뒤 새 encoder와 **실제 바이트 배열**을 대조한다.
- 두 봉투의 순번·상태 hash·RNG·실제 공개 명령 재전송·2개월 속행과 그 뒤의 정상 작은 저장 바이트가 같다. 삼자 전체 전사 검사는 기존 두 fixture의 실제 전체 행·명령 응답·저장 배열·저장 후 속행 비교를 통과했다.
- prefix·버전·음수/변조 순번·0/음수/과대/불일치 길이·체크섬·본문 손상·여러 잘림 경계·후행 자료를 거부했다. 올바른 체크섬이 붙은 잘못된 JSON 및 미지원 상태도 거부했다.
- 실제 파일에서 schema2→버전3→schema2 저장, 높은 순번의 pending 선택, 작은 backup 복구, 손상 primary/pending 원문 보존을 확인했다. 순번 상한의 다음 저장 실패가 유효한 primary/pending/backup의 실제 바이트를 덮지 않는지도 확인했다.

이번 작업에서는 전체 기능 수트나 성능·성장·대량 인구 재실측을 실행하지 않았다. 따라서 **실제 N10,000 저장 성공 근거는 아직 아니다**. 공개 `byte[]` API가 전체 스냅샷·canonical 본문·반환 봉투를 메모리에 만드는 점, 메모리 가용량·배열 크기·개별 JSON 값의 런타임 상한은 남는다. 실제 최종 전체 기능 및 후속 실측 판정은 해당 고정 소스의 별도 원본으로 확인해야 한다.

상위 PR: #84. 수정 PR: #192. 관련 이슈: #191.
