# R03 민감도 원형의 실제 영속 보관 완료

기존 민감도 run41131479924의 원형 ZIP 6개를 [별도 근거 사전 릴리스](https://github.com/hddy774/project-hora/releases/tag/evidence/v0.2.1-20261031)에 보관했다. 공개 파일 7개를 다시 무인증으로 내려받아 ZIP 6개는 고정 manifest의 전체 바이트·SHA256과, 색인은 실제 Actions 사본·서버 digest와 일치함을 확인했다. **보관 완료이며 원래 민감도 실행의 합격을 뜻하지 않는다.**

## 실제 실행과 권위

- [보관 run41163158886/attempt1](https://github.com/hddy774/project-hora/actions/runs/41163158886)은 `89e1046fd6b8f9ee8ce787a4186f0a2677eb07e9`에서 실제 `pull_request.labeled` 요청을 처리했다. actor/triggering_actor는 모두 hddy774이며 전체 보관 작업은 성공이다. API의 완료 상태 갱신 시각은 `2026-10-31T03:10:44Z`다.
- 공개 release ID는 `429687967`, tag는 `evidence/v0.2.1-20261031`이다. `draft=false`, `prerelease=true`이며 실제 tag ref는 측정 소스 `7ce66943b539420c9dc1e3fa8a611daae58020ff`를 가리킨다. `make_latest=false` 요청은 source/index에 보존됐고 실제 latest는 기존 0.2.0으로 유지된다.
- 보관 완료의 권위는 [공개 최종 색인](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/sensitivity-artifact-index-20261031.json)이다. asset ID `694202127`, 22,726B, SHA256은 `0a16fedf7185e2223befe8f69626380cfc16cfc32068a44c4cdd94d229fcccea`다. 이 폴더의 [색인 사본](public-index.snapshot.json)은 같은 바이트의 고정 시점 증거다.
- [실제 상태](status.json)와 [검증 요약](verification.json), [독립 공개 바이트 대조](public-byte-verification.json)를 함께 보존한다. 원형 6개는 총 9,937,563B이며 ZIP 자체를 Git에 중복 추가하지 않았다.

| 원형 구분 | 원래 artifact ID | 영속 asset ID | 바이트 |
|---|---:|---:|---:|
| [집계](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/v021-sensitivity-collection-41131479924-1.zip) | 13024674339 | 694201587 | 4,870,195 |
| [시드 2026](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/v021-sensitivity-41131479924-1-seed-2026.zip) | 13024223579 | 694201637 | 1,629,500 |
| [시드 42](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/v021-sensitivity-41131479924-1-seed-42.zip) | 13023894081 | 694201731 | 1,606,840 |
| [시드 98](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/v021-sensitivity-41131479924-1-seed-98.zip) | 13020234556 | 694201829 | 1,625,169 |
| [입력](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/v021-sensitivity-inputs-41131479924-1.zip) | 13013860275 | 694201906 | 204,982 |
| [준비](https://github.com/hddy774/project-hora/releases/download/evidence/v0.2.1-20261031/v021-sensitivity-prepare-41131479924-1.zip) | 13012967859 | 694201962 | 877 |

각 파일의 전체 64자리 SHA256·무인증 HTTP200·재검증 시각은 독립 대조 JSON에 들어 있다. status artifact13027898251의 ZIP은4,419B·SHA256 `d9c3c67be96c66552e99138b9d4fc56d7c9db03e24f0ba36374fac622f2d0cde`였으며 포함된 index도 공개 색인과 같은 바이트다.

## 기존 결과와 릴리스 보존

원래 run41131479924는 전체 실패(failure), 4개 조건의 측정 창 완주와 2개 조건의 명령 실행 시간 상한(CLI timeout) 부분 종료다. 보고서의 `complete=false`와 시드별 `requested` 값(98=true, 42/2026=false)을 유지한다. 목표 달성·보정 성공으로 바꾸지 않으며 새 분리 run41159864831의 결과를 섞지 않았다.

기존 latest는 `xianxia-observer/v0.2.0`·release428209007이다. 원래 APK689402166·43,505,222B·서버 SHA256 `b381a400514ea311117bb2b28d15c68baffc00d4ea71a689c9140a69867bf2df`와 release 본문·태그·게시 시각·자산 보호필드가 이전 스냅샷과 일치했다. [이전 tag ref](previous-release-tag.json)도 `09e8c693e3ebb52d2495bffb1a48364eb9396dce`를 유지한다. 이번 대조에서 이전 APK를 다시 내려받았다고 주장하지 않는다.

첫 보관 run41161342049의 `original-download` HTTP415 실패는 [기존 실패 원문](https://github.com/hddy774/project-hora/blob/89e1046fd6b8f9ee8ce787a4186f0a2677eb07e9/.github/evidence/checks/archive-41161342049/README.md)에 그대로 남아 있다. 고정 manifest와 원형 ZIP, 실패·24개 검사 원문을 수정하지 않았다. 이번 성공은 그 실패 이력을 지우거나 성공으로 덮어쓰지 않는다.

## 출처와 문서 범위

| 역할 | source·제품 지문 |
|---|---|
| 실제 민감도 측정 | 7ce66943… / FP4e228e7b…; metadata head42ebf359…·당시 workflow49383817…와 구분 |
| 이번 보관 helper 실행 | 89e1046f… / FP0cb2f90f…; Game·측정을 실행하지 않는 보관 흐름 |
| 이 문서의 통합 기준 | 661700ca… / FPc91c3d7b…; 기존 제품 입력을 수정하지 않는 문서 PR |

root의 사용자 R03 위임에 따라 Codex가 읽기 관측·독립 SHA 대조·문서를 수행했다. root는 실행 전 정확 source의 가드·27개 자체 검사를 독립 검토했으며 새 Claude 검토로 표시하지 않는다. 이 PR은 이 폴더의 Markdown/JSON만 추가한다. [run](run.json)·[job metadata](jobs.json)·[release](release.json)·[latest](latest-after.json)·[tag](tag.json)와 [작업 원문 UTF8 래퍼](job-log.json)를 보존한다. 래퍼의 `originalUtf8`을 UTF8로 복원하면 BOM·공백·개행을 포함한 원래 byte 수/SHA와 일치한다.

로컬 검증은 JSON·참조·전체 SHA·index/status/출처·보호필드의 일치와 문서 전후 제품 지문 동일성만 확인했다. PR242의 HEAD, label, 기존 실패·manifest·정본·readiness·게이트는 이 관측/문서 작업에서 변경하지 않았다. 추가 Game/기능/성능/성장/공식 서명·릴리스 실행은0회다. 현재 통합의 전체323 검사나 최종 성능·정식 동일 APK/API24·35·업그레이드 통과를 이 자료로 대신하지 않는다.
