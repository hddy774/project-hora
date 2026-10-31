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

검증 출처와 실제 결과는 이 폴더의 후속 원문·검증 기록에 연결한다. 원장·readiness는 수정하지 않는다.
