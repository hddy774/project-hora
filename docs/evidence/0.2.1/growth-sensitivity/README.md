# 고정7ce 단일 법칙 민감도 입력안

이 입력은 루트가 검토 초안의 수치·창·운영 상한을 채택한 뒤 준비했다. 실제 민감도6조건은 아직 실행하지 않았다. 루트가 입력 PR을 검토한 뒤 명시적 소유자 라벨로 실행한다. **제품 기본값1200을900/1500으로 자동 변경하는 자료가 아니다.**

## 같은 세계 코드와 실제 기준선

- 측정 체크아웃: `7ce66943b539420c9dc1e3fa8a611daae58020ff`.
- 제품 지문: `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`.
- SDK10.0.403/실제.NET10.0.14, 기본a·혈연허용·양친금단·외부인0. 다른 법칙·AI·자산·생계·교전·잠정 모델은 그대로다.
- 입력 문서/워크플로 소스와 측정 소스는 별도다. 입력의 개발 기준은ff829이며, 빌드는 입력을 RUNNER_TEMP에 보존한 뒤 정확한7ce를 체크아웃해 수행한다. 후속94/FP528·PGO 설정을 가져오지 않는다.

[공식 원문 보존 PR211](https://github.com/hddy774/project-hora/pull/211)은 원격 실행41111524034의 실제9raw·공식 집계·출처·검증을 보존한다. 공식 집계 SHA는 `bd592d544766dfff2ba39ece2a531513610ccca3b0c24b86061e9d9b0626e0ab`, 독립 전수검증 SHA는 `c74f85bd5c1aab95d403498039c1c400cab658618478c834608903a0d59196a0`이다.9개×51표본=459관찰, 실제 입력/행/법칙/소스/원본 플래그를 확인했다. 옛926 관측은 여기에 합산하지 않는다.

## 채택한 한 키와 적용식

기존 계획 schema1의 `cultivation.foundation`만900/1500으로 바꾸고 시드42/98/2026을 각각두 값에 실제 실행한다. 기본1200·단위개월·범위1..120000·provisional=true이며, 이 키의 추가 교차의존 검사는 없다. 다른 법칙은 기본값 그대로다.

고정7ce 코드 근거는 [Laws.cs40](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/Laws.cs#L40), [정책 매핑355](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/World.CultivationBridge.cs#L355), [RequiredWork213](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/Cultivation/CultivationContracts.cs#L213)이다. 축기 목표는 `FoundationMonths × FlowPerMonth(5000)`이므로600만→450만/750만 작업량(±25%)이다. 상위는150%를 정수 곱으로 누적한다.

[준비 target201](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/Cultivation/CultivationEngine.cs#L201)을 [새 형성276](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/Cultivation/CultivationEngine.cs#L276)에 저장하고, 이후 [GrowthBasis/성숙도429](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/Cultivation/CultivationEngine.cs#L429)를 쓴다. 실제 달력 진행은 공법·제어·건강·공급·오행 처리·실제 월 노동에 달려 명목개월과 같지 않다. 법칙 변경으로 이미 채택한 목표를 소급 단축하지 않는다.

범위 한계가 있다. [시조 생성138](https://github.com/hddy774/project-hora/blob/7ce66943b539420c9dc1e3fa8a611daae58020ff/src/Chronicles.Game/Cultivation/RealmStructures.cs#L138)도 같은 RequiredWork를 써서 상위 목표 및 시조60% 초기구조의 **절대량**이 함께 재계산된다. 시조 두 반선·60%비율·초기자산·수명은 유지되지만 이 실험을 후손 축기 한 단계만 격리한 효과로 주장하지 않는다.

## 관측으로 확인된 병목과 불확실성

| 기본a 시드 | 출생/사망 |6000개월 현존후손|후손금단|최대세대|
|---|---:|---:|---:|---:|
|42|17/16|1|0|1|
|98|0/0|0|0|0|
|2026|6/6|0|0|1|

비Mortal 후손은42에서최대4명,2026에서최대3명까지 관찰됐다. 첫세대에서 금단·다음 세대로 이어지지 않은 현상은 실제지만, 원형 집계는 Qi/Foundation을 분리하지 않아 축기가 유일하거나 지배적인 원인이라고 진단할 수 없다. 이 비교는 수행량 계수가 그 병목에 얼마나 민감한지 보는 조건부6관찰이다. 수명/출산간격까지 동시에 움직이지 않는다.

c2026에서는 실제 후손금단이 정확4326개월(entity8042, 실제 `Formations[GoldenCore].Month`)에 성립했다. 이는 기본a의 성공률이나 정책 교체 근거로 확대하지 않는다. 관측창은 원형과 같은6000개월/120간격을 유지한다.3600은 이미 관측된4326을 검열하므로 채택하지 않았다.6000이후도 오른쪽 검열이며3시드는 보편적 확률·불가능성·장기 생존 증명이 아니다. 미도달/사망/부재와 성립 시각은 원문 그대로 기록한다. 인구·금단 마감·모두 생존 등의 새 성공 목표는 없다.

## 운영 상한240분의 실제 시간 근거

아래는 같은7ce 기본a의 실제 CLI 시간이며 창세 선택검사·로그·보고서 IO를 포함한다. 서로 다른 VM이므로 순수 기기 성능의 대조나 병렬 전체 시간으로 합산하지 않는다.

|시드|실제 case 분|실제 CLI 분|두 값 단순 기준 분|
|---|---:|---:|---:|
|42|53.8158|53.8343|107.6685|
|98|28.2489|28.2724|56.5449|
|2026|57.3403|57.3713|114.7426|

최대57.3713196×두 값×운영상 잠정여유2=229.4853분을30분 단위로 올려 **각 시드 작업 CLI240분**으로 채택했다. 상한 파일은 순수 ASCII `240`과 LF다. 이 여유는 공학적 운영 경계이며 보장 시간·게임 성능 수용 조건이 아니다. 법칙 변화로 인구/행동량이 늘어 초과할 수 있다. timeout/partial은 실패로 보존하며 창·시드·법칙·완료 플래그를 바꿔 통과시키지 않는다. 작업360분 서비스 경계 안에서 원문 업로드 시간을 남긴다.

## 원문 불변과 상대 경로 패키징

정식7ce CLI는 입력을항상 `Path.resolve()`하므로 상대 인수도 절대 출처로 저장한다. 최초 비portable 파생본을 `operation/nonportable-cli-baseline.json.gz`에 원문 byte를 복원할 수 있게 보존했다. 압축 해제 SHA는898a4c435a36c76efdc2d019a747843899686b393236a6814dcbdb9989fc9fee이며 JSON 내용은 사후 수정하지 않았다.

운영 adapter `operation/build-portable-baseline.py`는 **정확7ce의 미수정 `read/law_definitions/aggregate` 순수API**로9원본을 검증하고 새 결과 생성 시 기존main과 같은 출처 메타데이터에 상대 `raw/...` 경로를 기록한다. 제품 scripts/World/기존 검증기/schema는 변경하지 않는다. 공식 통합기 소스 SHA는 `ed7d8a4f2f3ea6eb217b21602150aa1aa166600ac2810156c29dcab01163b32b`다.

새 파생본은 최초 CLI 결과와 path 외 **모든** 필드/출처 메타데이터가 같고, 공식 집계와9cohort/분포/9SHA가 같다. 기본 입력12파일(9raw+baseline+plan+cap)의 baseline/plan/cap SHA는 `input-hashes.json`에 기록했다. 순서는9raw 검증→정식 API 집계/이동·음성경계 확인→baseline SHA→채택 계획→plan SHA→상한 파일이다.

`operation/first-candidate-adapter-as-run.py`와 proof는 최초 운영 시도를 보존하며 경로는 당시 작업공간을 나타낸다. 현재 재현 가능한 adapter 호출은 다음과 같다. 입력 폴더에는9raw만 먼저 배치하고 출력 baseline은 존재하지 않아야 한다. source-checkout은 깨끗한 정확7ce, 공식 집계와 최초 CLI 원문 경로는 별도로 제공한다.

```text
PYTHONDONTWRITEBYTECODE=1 python3 operation/build-portable-baseline.py \
  --source-checkout <정확7ce체크아웃> --inputs <새9raw묶음> \
  --official-aggregate <공식aggregate원문> \
  --validated-cli <압축해제한 최초CLI원문> \
  --proof <별도출처파일>
```

실제9원문 복사본을 쓰는 `operation/verify-packaging-boundaries.py`는 다른 루트로 옮긴9상대경로 해석과 누락/부분/다른소스/중복/빈관찰/기존출력보호/외부symlink 거절7개를 확인했다. 원문/공식 집계 byte는 그대로이며 **새 World/수집기 실행0**이다. 검사 결과는 `operation/packaging-boundary-results.txt`, 출처는 `portable-provenance.json`에 보존한다. 첫 부분hash를 다른6000개월hash와 비교한 보조 종료 관찰기의 잘못된 검사는 별도 보존하고 같은 월 비교로 정정했으며, 보조926을 최종7ce9관찰에 합산하지 않았다.
