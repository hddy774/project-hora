# Notion 진단 원문 보관: CPU trace2개 첨부·heap3개 보류

[0.2.1 성능 진단 원문 보관 — 실제 하위 페이지](https://app.notion.com/p/40aacc9e617581019cdfcfc214c29885)는
사용자 지정 기획 페이지 `408acc9e6175817ab59bd91bb9dc011d` 아래에 생성했다. 프로젝트 부모의 접근 권한을
상속하며 공개 공유/ACL 설정은 변경하지 않았다. 별도 ACL 감사나 공개 GitHub 릴리스 자산 보관을 뜻하지 않는다.

## 실제 보관과 한계

- CPU trace2개는 로컬 원문→gzip 압축 해제의 SHA/크기 일치를 확인하고, 파일별1회 multipart POST에서
  HTTP200을 받았다. 즉시 native file 블록으로 연결했고 실제 페이지 재조회에서도 stable 파일 블록2개를 확인했다.
- 파일별 source는 아래와 같이 서로 다르다. 이 문서를 저장한 개발 HEAD의 실행 결과로 바꾸어 부르지 않는다.
- **서버 바이트 독립 다운로드 SHA 검증은 미실행**이다. `include_file_urls=true`의 읽기 결과가
  다운로드 URL/references를 주지 않았고, 활성 도구에 binary 다운로드 기능이 없었다. 비공식 endpoint나
  자격 증명을 탐색하지 않았다. 로컬 SHA·HTTP200·첨부 블록 확인과 서버 bytes 검증을 구분한다.
- 도구의 단일 파일 상한은20MiB지만 실제 워크스페이스 상한은 **5MiB**였다. heap fresh gzip의1회 요청은
  HTTP400 `validation_error`로 거절됐으며 같은 파일을 반복 요청하지 않았다. heap3개 모두 Notion **미보관**이다.
- 원본 trace/dump와 기존 gzip은 유지했다. 허용된 post-return XZ1회도5MiB를 넘어 업로드하지 않았다.
  분할·다른 heap 재압축·추가 업로드·공유 변경은 하지 않았다. 압축 CPU3 작업은02:30:14.498059UTC에 끝났다.
- 새 Game/기능/성능 실행, 제품/게이트/원장 수정, 큰 binary의 Git/CI 추가, 민감도6개 GitHub ZIP 중복은0이다.

| 첨부 파일 | 실제 실행 source | 압축 bytes | 업로드 완료 UTC |
| --- | --- | ---: | --- |
| `df72405-kb-month30-cpu1.nettrace.gz` | `df7240556a260202a248e57b1f9868e372b9d995` | 4,179,794 | 2026-10-31 02:25:34.005789 |
| `db0feae-kb-month30-cpu1.nettrace.gz` | `db0feae5a5111c6ffc97b180cc37b5f9a232b3e2` | 2,679,873 | 2026-10-31 02:27:41.043049 |

실제 fileUploadId·원문/압축/복원 SHA와 heap3개 보류 내역은
[원형 색인](notion-retention-index.json)에 있다. 두 파일의 첨부를 기능/성능 수용이나 모든 진단 원문 보관 완료로
확대하지 않는다. heap의 남은 영속 보관은 별도 미완료 범위다.

## 이번 Git 사본과 SHA

아래3개 파일만 `/workspace/scratch/notion-raw-retention-preparation-df72405/`에서 바이트 그대로 복사했다.
JSON 재직렬화·개행 정규화·원래 SHA 변경은 하지 않았다.

| 파일 | bytes | SHA-256 |
| --- | ---: | --- |
| [notion-retention-index.json](notion-retention-index.json) | 6033 | `22a6ad984fcc4503e39d5d366da58222db40e6effe5481b6b0ddab540812a7ed` |
| [retention-small-proof-manifest.json](retention-small-proof-manifest.json) | 1502 | `534daadce0a4e8fb59e52a9d1d9609e3d16d35e7588285bc9b491f672d23fc7c` |
| [upload-result-2.json](upload-result-2.json) | 174 | `b9f634dc372043eff5e78649bfd2d2fc4af6b69dccc98e4092a7dec874e8b8c0` |

534daad… manifest는 당시 scratch의 작은 근거9개를 열거한 **기존 원문**이다. 그9개 전부를 이번 Git 폴더에
복사했다는 뜻은 아니다. 이 폴더의 사본 범위는 위3개이며, manifest의 해당 파일 크기/SHA를 대조했다.
보관 대상 binary 자체는 Git에 없다. HTTP400 원문에는 실제5MiB 거절 사유만 있고 token/upload 주소는 없다.

Codex가 원형 크기/SHA·JSON 파싱·허용된 Notion child 링크 외 HTTP URL/인증 header/token 부재·문서 diff를
정적으로 확인했다. 기존 첨부의 재업로드/새 압축/새 Notion 조회 없이 저장한 근거만 보존했다.
추적 이슈는 [#233](https://github.com/hddy774/project-hora/issues/233)이며 실제 trace/heap source의 한계를 유지한다.
