# 과거 c91 장기 대조: 정본 상한 불통과

**원래 44개 상한 중41개 통과·보유 메모리3개 초과이므로 정본 판정은 false다.** GHA 성공·프로세스 종료0·원형 `complete:true`/`passed:true`는 사실 그대로 보존하며 이를 수정하지 않는다. 시간4개·저장20개·실제 I/O/불변식/조회 무영향/공개명령 성공으로 전체 실패를 가리지 않는다. 최신0eaf 장기/T04 합격이 아니다.

| 초기 인구·개월 | 보유 메모리 실제 B | 원래 상한 B | 초과 B |
| --- | ---: | ---: | ---: |
| 2명·90개월 | 47,817,272 | 47,725,032 | 92,240 |
| 2명·120개월 | 48,053,472 | 47,725,032 | 328,440 |
| 10,000명·30개월 | 2,594,900,560 | 2,192,400,000 | 402,500,560 |

[원형 result](raw/result.json)의 SHA256은 `2bef619b754572cf7cd72d46b9acae008162786df7eddfe5eecc9248bca2e9bb`다. [독립 파생 canonical-review](canonical-review.json)는 원래 검증함수의 `retainedManagedBytes` 거절과 전체44행을 보존한다. 두 파일의 판정 차이를 감추거나 어느 원문도 다시 쓰지 않았다. 전체20경계·시간·환경 표는 [검토 원문](review.md), 작은 판정 분리는 [summary](summary.json)에 있다.

## 실제 실행과 입력

- [run41163711116](https://github.com/hddy774/project-hora/actions/runs/41163711116) attempt1, 실제 소스 `6607234b405921cc39e9527cbea42a7667a709be`, 제품 지문 `c91c3d7b7cf0441f207825d0490d38501072b63b0364234fbd7cf973b1f620f1`의 과거 대조다.
- 한 프로세스에서2/100/1,000/10,000명 각각120개월,0/30/60/90/120의20경계를 완료했다. [실제 명령·종료](raw/measurement-process.json)는03:17:57.697969→07:16:02.173163 UTC, 프로세스·tee 종료0·외부 cap/kill=false를 기록한다. [API run/jobs](github-run.original.json)와 [job log 원문](job.original.log)을 보존한다.
- plan `ec853c715eafc4341fa47802e4de445f0caf165833470d15beb69550905b6c46`, baseline `1842a4dbffb66fa9c8489ad31343d94fed933f022c8d63be430b774c37998484`, budget `7365b80e3de602dfe331a5348f4008808f6620744577582d07bc7f33f11dc272`를 변경하지 않았다. 기존7901/c91 단기32개 성공도 별도 원문이며 장기 전체 성공으로 합성하지 않는다.
- 기존 전체 기능323 원문과 SourceReview를 같은 지문으로 사전 대조한 job이다. 이번 프로세스 앞선 실행은 선택 창세1개이며 전체323개 재실행이 아니다. 같은 SHA의 입력6개는 중복 복사 대신 [repository references](references.json)의 기존 정본 경로로 연결했다.
- SDK10.0.403/runtime10.0.14·PGO=false, Ubuntu24.04.5·AMD EPYC7763·실제 CPU/affinity4를 기록한다. Release0경고/0오류·source clean 및4DLL의 실제 PE 버전/정보버전/source660와 전체 SHA는 [assembly-evidence](raw/assembly-evidence.json), [원본 manifest](raw/artifact-manifest.json), [소스·환경](raw/source-environment.json)에 있다. DLL을 Git에 추가하거나 새로 로드/실행하지 않았다.

## 보존 범위와 한계

원본 ZIP은1,317,632B, SHA256 `a16bbe0148b4d074ef192692ed779e76fdfdccd758d196b0c53d6d7c4aff677f`다. [artifact metadata](artifact-metadata.original.json)·[수집 검증](raw/collection-verification.json)·[scratch 보존 manifest 원문](preservation-manifest.json)을 그대로 복사했다. 그 manifest는 ZIP/DLL/검토 helper 등을 포함한 원래 scratch 자료 목록이며 모두 Git에 포함했다는 뜻이 아니다. 이 폴더에 실제 포함한 파일의 SHA/출처는 별도 [manifest](manifest.json)로 구분한다. 큰 ZIP·DLL·pyc·중복 입력/검증기 소스는 제외했다.

실제 저장 슬롯8개(primary/backup)의 bytes/SHA는 [runner slot manifest](raw/slot-manifest.json)의 기록이며120/90개월 저장 크기와 일치한다. **slot 본문은 원래 artifact에 없으므로 독립 본문 해시·복원 검증을 했다고 주장하지 않는다.** actualruntime/명령/deps·PE 기록은 있지만 프로세스가 로드한 모든 DLL의 별도 SHA 인증은 없다. 서로 다른 물리CPU/열/전원·공유 환경을 동일 조건으로 단정하지 않는다.

메모리 초과의 코드 원인은 현재 Knowledge/People 진단 중으로 미확정이다. 누수·공유풀·JIT·환경 중 어느 하나로 단정하지 않으며 이슈233의 진단과 필요한 최소 변경 뒤 새 소스를 동결해 전체 기능·단기32개·원래120개월을 확인하는 다음 작업과 구분한다. 예산/기간/원장/readiness는 변경하지 않았다.

## 현재0eaf·플랫폼과 구분

현재0eaf의 [run41170302565](https://github.com/hddy774/project-hora/actions/runs/41170302565)는 실제 소스2db5a6d의 별도 진행 중 실행이며 결과 대기다. 이 c91 자료로 T04를 닫지 않는다. 완료109/111(98.2%)·기능87개, T04/T06 진행 중·readiness=false·메인PR84 DRAFT를 유지한다.

최신356c1cf 제품 [run41178765537](https://github.com/hddy774/project-hora/actions/runs/41178765537)은 portable/Windows 성공이나 API35 job124471716168은 설치 return1·정확한 `adb: device offline`으로 실패했다. 실제 checkout은merge `c5f6220e51eb0e9ac16df5db836f153b4093fe48`이고 head356과 whole tree가 같다. [분류 원문](current-platform/classification.json)과 [소스 대조](current-platform/source-comparison.json)는 UI 담당의 읽기 검증을 복사했다. 앱 시작/DOM 검사는0회이고 Quickstep ANR도 관측됐으나 앱 원인이나 offline과의 인과는 미확정이다. 별도 API24 [run41178765593](https://github.com/hddy774/project-hora/actions/runs/41178765593) 성공은 root 확인 범위로 기록하며 이 문서 작성자가 새 원형을 내려 독립 검증했다고 하지 않는다. 이슈266의 별도 최소 수정과 실제 재검증이 남아 있다.

이 작업은 문서 보존·작은 정수/해시/링크 대조만 수행했다. 새 Game·빌드·native·성능·성장·장기 측정은0회다. 이전 실패 원문과 현재 별도 실행의 판정을 승격하지 않는다.
