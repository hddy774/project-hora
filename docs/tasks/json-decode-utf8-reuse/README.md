# JSON 저장 복원에서 체크섬 본문 재사용

상위 PR: #84. 관련 이슈: #233. 작성·구현·자체 검토: Codex knowledge agent.
기준 소스는 `356c1cf`이며, 기존 c91 장기 원문의 retained 상한 초과는 그대로 보존한다.

## 범위와 계약

`GameCodec.DecodeEnvelope`의 JSON2 분기에서 체크섬에 이미 사용한 UTF8 배열을 로컬로 보존하고
같은 span을 본문 Deserialize에 사용한다. 기존 string Deserialize의 별도 3×char 변환 버퍼를
피하기 위한 후보이며, 풀 root나 T04 합격을 사전 확정하지 않는다.

- 봉투 Format·Schema·Sequence → 체크섬 → 본문 JSON → 공개 Restore의 순서를 유지한다.
- 공개 Restore/Checkpoint의 깊은 사본, 예외 정규화, 상태·RNG·바이트 계약을 유지한다.
- Clone·Encode·Hash·이진 Decode·파일 슬롯·하네스·기간·GC·상한은 변경하지 않는다.
- 새 IFeatureScenario 두 개는 자동 discovery로 등록한다. Program 수정은 없다.

## 한정 수용·검증

1. Release 경고·오류 0 및 새 공개 codec 시나리오·기존 관련 저장 시나리오 선택 검사.
2. 원래 `6607234b405921cc39e9527cbea42a7667a709be` DLL과 후보를 동일한 초기 별도 프로세스에서
   같은 정상·오류 입력으로 비교한다. 오류 종류·메시지·순서, 성공 hash·RNG·순번·재저장 바이트를 대조한다.
3. 실제 N2 M90/M120 원문은 c91 최종 slot manifest와 SHA가 일치해야 한다.
   각 프로세스에서 M90→M120 Decode를 한 번씩 수행하며 ArrayPool 사건과 full-GC retained를 관측한다.
4. SDK10.0.403/runtime10.0.14/PGO false/CPU1, 프로세스당 120초 상한이다.
   새 월 진행·전체 규모 측정·장기 재실행은 없다. 선택 기능·한정 진단을 T04로 확대하지 않는다.

## 실제 결과와 리뷰 입구

실행 소스는 `7a7d82b1bc8cdc2e06825014b181d0e5fc5ddb2e`다.
원격 구현 `5b0c1d5c730f16e061d7696d83847adcfe1d5f75`와 전체 구현 tree
`b049bfa3d09f601c681fbd954b3c6e4bb20128d8`가 정확히 같다. 연결 도구의 commit 메타데이터 차이이며
실제 실행 PE 소스를 원격 SHA로 바꾸어 적지 않았다.
[검증 기록](verification.json)과 [파일 색인](manifest.json)에 원문·SHA·한계를 연결했다.
기존660과 기준356의 `src/Chronicles.Game`/`src/Chronicles.Core` 차이는 없었다.

- Release 빌드 경고0/오류0, 선택 기능 **10/10**: 신규 codec2·기존 대용량 저장4·segment2·기본 경계1·운송 저장1.
- [별도 프로세스 oracle](checks/oracle-comparison.json)은 **36/36 동일**이다.
  정상 결과의 세계 hash/RNG/순번/재저장 SHA와 거부의 exception type·message·inner chain을 비교했다.
- [기존 관측](checks/old660-memory.json)과 [후보 관측](checks/candidate-memory.json)은 각 슬롯을 한 번만 읽었다.
  원래 입력과 새 프로세스 설정은 같으며 관측 과정의 월 진행은0이다.

| 실제 입력 | 기존 GC 후 retained B | 후보 GC 후 retained B | 16MiB BufferRented 기존 → 후보 |
| --- | ---: | ---: | ---: |
| N2 M90 | 50,837,416 | 34,114,576 | 1 → 0 |
| N2 M120 | 51,459,072 | 34,727,632 | 1 → 0 |

이는 입력 byte[]와 복원 세계를 함께 살려 둔 **codec-only 진단**이다.
CPU1 고정·별도 초기 프로세스·EventListener·full-GC 조건이며, 원래 장기 관측점의 liveness와 다르다.
공통8MiB rent는 유지됐다. 현재 장기 실행의 SOS root·전체 할당 예산·N10k 개선·T04 합격은 증명하지 않는다.
최초 M90 thread allocation은94,940,136→78,169,992B였고 풀 재사용 M120은49,466,256→49,465,608B였다.

[probe 소스](probe/Program.cs)와 [실행 wrapper](probe/run-comparison.py)를 보존했다.
정상·오류 입력은 같은 old DLL에서 한 번 생성했고 실제 두 슬롯은 c91 manifest의 SHA와 일치하는
로컬 원문을 읽었다. 큰 슬롯 본문·DLL은 이 작은 PR에 복제하지 않는다.
로그 부모 폴더 누락으로 Game 시작 전에 끝난 최초 wrapper 오류도 별도 원문으로 남겼다.

자체 리뷰는 제품 diff가 JSON Decode의3개 변경 줄에 한정되고 검증 순서·예외 catch·public Restore가
그대로임을 대조했다. 독립 리뷰나 현재 전체 기능/최종 성능·native 실행은 여기서 주장하지 않는다.
원장·readiness·기존 원문·공유 실행 증인표는 수정하지 않았다. 통합 담당자가 새 동결 소스에서 전체를 검증한다.
