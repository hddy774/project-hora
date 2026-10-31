# 2464337 최종 단기 네 규모·32개 지표 원문

실제 실행 소스는 `2464337ec577b2894eb87468c2c72916b1d6b8bf`, 제품 지문은 `0eaf05f4c00e79ac1f485a50ac4d0e6eec30f82dce49a2012e1aa0cf716bfdc2`다.
프로세스 종료0·네 규모 **2/100/1,000/10,000명, 각12개월 완료**, 원래 절대/회귀 상한의
**32/32 통과**를 보존한다. 원문SHA256은 `acebfee1068992a73e9ceb590f68c518909cb31c581fca59edfb929f92542696`다.

## 원문과 출처

- [측정 원문](population.json)·[로그](population.log)·[원래 실행 기록](run.json)·[원래32행 판정](assessment.json)은 scratch와 byte 동일하다.
- [선행 선택검사](preceding-functional.json)는 같은 프로세스의 창세1/1이며 전체검사가 아니다.
  실제 전체323/323은 [별도 기능 원문](../invariant-final-2464337/README.md)과
  [SourceReview](../invariant-execution-source-review-2464337.json)에 연결한다.
- [실행 wrapper](run-v021-final-population.original.py)·[출처](provenance.json)·[요약](summary.json)·[SHA 색인](manifest.json)을 함께 보존한다.

실행 시각은 2026-10-31T04:41:47.337614+00:00~2026-10-31T04:55:30.658153+00:00다. 각 규모는 별도 fixture의2개월 예열과
12개월 측정1회다. CPU0 한 개 affinity·공유4CPU quota·Linux 호스트이고 SDK10.0.403,
명시적 `--fx-version 10.0.14`와 실제 runtime map을 기록했다. PGO=false·4개 환경 override null,
source 전후clean·4DLL source/SHA는 원래 run에 있다. 열·전원 상태는 미측정이며 trace는 부착하지 않았다.

## 실제32지표

| 초기 인구 | 월p95 ms | 조회 ms | 저장 ms | 읽기 ms | 월진행 할당 GiB | 저장 MiB | 수집 보유힙 MiB | 인지투영 MiB |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 2 | 242.43 | 33.71 | 158.62 | 173.32 | 0.134 | 2.781 | 14.983 | 0.169 |
| 100 | 675.76 | 32.67 | 471.12 | 562.93 | 0.945 | 14.514 | 34.024 | 3.317 |
| 1,000 | 2862.99 | 162.33 | 1617.11 | 2055.91 | 9.299 | 50.906 | 117.160 | 22.395 |
| 10,000 | 62948.85 | 1223.15 | 16936.96 | 21710.84 | 301.420 | 379.780 | 412.197 | 213.807 |

표만 읽기용으로 반올림했다. 원문/판정의 수치와 상한은 그대로다. 할당은 실제 AdvanceTo 전후의
프로세스 전체 할당 차이 합계로 진행 JSON/콘솔 기록을 제외한다. 보유힙은 full GC 뒤 실제 rooted world·
런타임 캐시·이전 작은 결과를 포함하는 관리힙이며 RSS/native나 인물별 합계가 아니다.
저장/읽기는 공통 GameCodec의 메모리 Encode/Decode이며 실제 파일 원자저장은120개월 장기 하네스의 별도 범위다.

## 원래 입력과 판정 보존

[기준선](../final-performance/baseline.json)의 SHA는
`1842a4dbffb66fa9c8489ad31343d94fed933f022c8d63be430b774c37998484`,
[고정 예산](../final-performance/budget.json)의 SHA는
`7365b80e3de602dfe331a5348f4008808f6620744577582d07bc7f33f11dc272`다.
각8지표를 원래 절대 상한과 기준선+25% 회귀 상한 모두에 대조했다. 기존 verifier의 complete/네 규모
검사를 읽기로 사용하고 wrapper32행의 수치/상한/판정을 한 번 독립 대조했다. 예산·기간·정책을 바꾸지 않았다.
초기/최종 상태hash·법칙·fixture·실제 작업량의 기준선 대조는 요약에 개별 bool로 기록한다.
저장된 byte array/SHA는 하네스 원문에 없으므로 저장 bytes 자체 동등을 주장하지 않는다.

이 폴더는 새 원문을 추가할 뿐 기존 final-performance/population.json·기준선·예산·계획·실패 원형을
수정하지 않는다. 문서 작성의 새 Game/빌드/성능/성장 실행은0이다. 단기32지표 판정은120개월 장기·
민감도 기본값·native·서명/업그레이드·T04 전체/릴리스 수용을 대신하지 않으며 원장/readiness는 변경하지 않는다.
