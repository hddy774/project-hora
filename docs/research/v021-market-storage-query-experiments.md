# EX-03·05·06 비교 소스 준비

이 PR은 test-only 비교 소스와 작은 고정 정확성 검사3개를 추가한다.
현재 단계에서는 **Release 빌드만 수행**하며 기능 검사·예열·성능 표본 수집은 실행하지 않았다.
측정 결과·채택 판단·Android 통과를 작성하지 않는다. 루트의 현재 기준선 측정과 동시 부하를 만들지 않는다.
공용 `ExperimentDefinition/ExperimentMeasurement/BackendSample`은 PR147의 준비 schema를 재사용한다.

## 입력과 실행 경계

`PublicReadFixture.Create(listings)`는 실제 `GameWorld.Create(42, laws)`와 고정 ID의 공개 split/offer 명령으로 출품을 만든다.
초기 식량은 등록된 창세 법칙과 실제 Opening 원천으로 선언하며 숨은 인물·상점·자산을 생성하지 않는다.
공유 도구는 현재 공개 도메인 `GrantUse` 명령을 `InventoryEngine.Apply/ApplyTo/Restore`로 적용한다.
이는 실제 단일 실물 권위의 명령이며 앱 UI가 GrantUse를 완주했다고 부르지 않는다.

시장 입력은 `LocalOffers`의 현지 공개 광고와 실제 허가된 사용권의 ID·종류·소유자·날짜만 가진다.
타인의 품질·재고·돈·지식이나 원격 실제 원천을 AI 후보 입력으로 넣지 않는다.
개인 조회 입력은 실제 `World.View(viewer)`가 권한 필터를 끝낸 자기 실물/현재 첫 지식 페이지를 복사한다.
후보 인덱스와 불변 캐시는 그 동일한 입력 이후에만 만들어진다.

예열/측정 세계는 각각 같은 생성·명령열로 만들고 시작 hash/명령 hash가 같은지 검사한다.
두 준비 과정의 생성·공개 명령·권한 DTO 캡처·검증 비용도 별도 원시 표본으로 기록한다.
읽기 입력은 분리된 불변 값이며 정산은 매 작업마다 동일 GameCodec clone, 저장은 매 작업마다 고유 폴더를 사용한다.
예열 결과를 측정 결과로 합치거나 가변 세계를 반복 진행하여 입력을 바꾸지 않는다.

## 비교하는 실제 후보

| ID | 기준/후보 | 정확성 경계 | 비용 범위 |
| --- | --- | --- | --- |
| EX-03 | 허가 광고 전체 scan / 명시 ContractId·종류 색인 / 색인 재사용 | 같은 후보 규칙·누락/선택·고정 요청 분포, 실제 사용권 철회, 숨은 실제 split 비간섭, 같은 공개 sale 정산·멱등·자산 보존 | 읽기 커널, cold index 구축 포함, 같은 GameCodec 복원·실제 정산 전체를 별도 측정 |
| EX-05 | 현재 JSON GameCodec/GameSaveStore / 같은 envelope를 BCL Brotli로 압축한 원자파일 후보 | 동일 sequence/schema의 바이트, 내용/길이/기존 GameCodec 검증, 완전/부분 pending·backup·손상·이전 원본 보존 | codec/압축·복사·검증 roundtrip과 고유 폴더 생성/정리·Flush(true)·rename·읽기/복원 전체를 분리 |
| EX-06 | 같은 개인 DTO scan / read index / 입력 지문에 묶인 불변 cache | 같은 행/전체수/page·장비/용기·닫힘, 실제 비공개 split 후 같은 결과, 실제 자기 장비 변경의 cache 무효화 | 커널·cold index/cache 구축·warm reopen·실제 World.View/입력복사/지문부터 응답 직렬화 전체, 닫힌 요청은 조회 호출0·행0 |

EX-03의 분포는 고정 sale/auction/미관측 종류 요청의 선택 분포다. 제품 AI의 전체 목표·장기 행동 분포로 확대하지 않는다.
실제 즉시 sale은 양 당사자 돈과 기존 실물만 이전한다. auction 요청 비교를 경매 낙찰 완주로 부르지 않는다.
EX-06은 서버 읽기 비용이며 브라우저 렌더·터치/입력 지연·전체 도감 가상화 측정이 아니다.

EX-05 후보는 기존 JSON GameCodec를 바꾸지 않는 압축 저장 wrapper다.
SQLite/MessagePack/MemoryPack를 실행하거나 도입하지 않았다. 추가 패키지·제품 저장 경로도 없다.
사용한 `.NET BCL/System.IO.Compression`의 실제 assembly/runtime 버전을 보고서에 넣고 기존 runtime MIT 의존으로 기록한다.
입력 파일·해제 크기는 상한을 검사한다. 후보는 단일 writer의 같은 호스트 파일 주입 복구만 확인하며
실제 전원 손실·동시 writer·Android 프로세스 중단·WAL/checkpoint 호환을 입증하지 않는다.
현재 `GameSaveStore`의 원본 보존·현재 앱 경로는 이 실험 때문에 변경되지 않는다.

## 명시적 측정 진입점

같은 최종 소스/Release 빌드에서 루트 측정 분담 지시를 받은 뒤에만 호출한다.
제품 소스 fingerprint와 기기 설명은 실제 실행 호스트가 확인한 값을 전달하고 별도 provenance도 보존한다.
현재 API는 호출자의 출처 선언을 받으며 자체적으로 Git/실제 장치 출처를 인증하지 않는다.

```csharp
Chronicles.Game.Tests.Experiments.ExecutionExperimentScenarios.Run(
    reportPath, productSourceFingerprint, device,
    listings: 32, repetitions: 25, warmups: 2,
    workspace: isolatedDirectory, selectedExperiment: "EX-03");
```

`selectedExperiment`는 `EX-03`, `EX-05`, `EX-06` 또는 전체(null)다.
자동 `IFeatureScenario`는 `VerifyMarket/Verify/VerifyQueries`만 부르며 sampler/Stopwatch를 호출하지 않는다.
새 정확성 검사 이름은 각각 `EX03 준비 기능`, `EX05 준비 기능`, `EX06 준비 기능`을 포함한다.
공통 Program의 측정 flag 추가는 루트가 소스 병합 후 연결한다.

보고서는 각 작업의 raw ns·현재 thread의 관리 할당·정답 hash·실제 출력/파일 바이트와 p50/p95/p99(nearest rank)를 보존한다.
할당은 native Brotli/OS/I/O 버퍼·프로세스 peak RSS가 아니다. 열/전원은 이 하네스에서 미수집이라고 기록하며 호스트 별도 근거를 사용한다.
같은 입력의 결과가 다르면 후보는 reject, 같은 결과면 측정 자료와 한계를 검토할 revisit로 남긴다.
실측 값 없이 adopt를 작성하지 않으며 이 test-only 후보의 속도만으로 앱 경로를 채택하지 않는다.
