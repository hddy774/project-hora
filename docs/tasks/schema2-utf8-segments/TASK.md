# schema2 JSON 봉투 문자열의 전체 UTF16 임시 사본 제거

## 승인·출처·목표

- 관련 이슈 [#233](https://github.com/hddy774/project-hora/issues/233), 상위 PR84.
- 구현·명세: Codex. 실제 외부 Claude 검토를 새로 수신했다고 표시하지 않는다.
- 기준 source: `5f789c817eb9519b696acd72410643169ff11ef2`.
- 원래 계획 SHA `ec853c715eafc4341fa47802e4de445f0caf165833470d15beb69550905b6c46`의
  N2/120개월 선행 진단은 완주했지만 retained가 월90에164,093,608B, 월120에147,548,184B였다.
  원래47,725,032B 상한을 넘었다. whole report는 뒤 case 전에 외부중단한 partial이며 최종T04 합격이 아니다.
- SDK403/refpack10.0.14의 UTF8 segment API를 읽기 확인한 뒤, 통합 담당자가 JSON 쓰기 분기만 개선하도록 승인했다.

## 소유·제외 범위

`GameCodec.cs`의 JSON 분기와 private helper, 의미 있는 새 호환성 시나리오, 이 작업 문서·작은 증거만 소유한다.
`Format→Schema→Hash→Payload→Sequence` 순서와 같은 Encoder/indent/MaxDepth64, 각16KiB byte segment,
final=true, 호출 소유 ArrayBufferWriter→ToArray를 사용한다. 전체 출력 byte 배열·최종 복사는 남는다.

Hash/Clone/CommandHash/PayloadHash/Decode/Envelope record, 명시 limit와16MiB default·binary bytes,
Hosting, 세계 권위·시간/RNG·게임 값·원래 예산/GC/측정 하네스·원장은 변경하지 않는다.

## 수용 기준과 실행 범위

1. 독립 기존 serializer oracle, 실제5f DLL 원형 및 기존 고정 schema2 bytes와 전체 바이트가 같다.
2. Unicode/문화권·sequence0/7/상한, 빈/단일/16KiB±1 조각과 분할 UTF8·escape의 순서/내용이 같다.
3. 기존 양형식·16MiB 기본 경계·순번·재전송/RNG·손상/원자 파일 복구가 보존된다.
4. CPU3 Release 경고/오류0과 필요한 선택 기능·정적 검사가 통과한다. 정확한 Name/명령/출처를 기록한다.
5. 제품 source/실제4DLL을 동결한 뒤 같은 원래 plan의 N2/120개월 보고가 쓰이면 외부중단하는 진단을
   CPU3 단일200초 cap으로1회만 실행한다. 뒤 case/전체T04 통과를 주장하지 않고 원문을 수정하지 않는다.
6. 전후 같은 월의 Hash/saveBytes/모든 flags를 비교하고, 실제 retained·case 시간은 원래 상한과 그대로 비교한다.
   성공까지 반복하지 않으며 선행164MB 원문을 덮어쓰지 않는다.

검증·원문·SHA·읽기/실행 구분은 REVIEW.md와 verification.json에 보존한다. 새 전체/성장/추가 원격 실행은0이다.
이번 net10 API가 실제 애플리케이션 target/API24에서 작동하는 네이티브 검사는 통합 뒤 별도 필요하다.
쓰기 UTF16/char scratch를 피하는 후보이며 Decode의 Payload string·반복 검증/byte 배열 비용까지 해결했다고
주장하지 않는다. 기존 root 파일 예산·기능/성능 수용 기준은 그대로 유지한다.
