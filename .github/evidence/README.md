# R03 민감도 원형의 영속 보관

## 승인·출처·담당

사용자 PR84 피드백 R03과 기존 전체 게시 승인을 구현한다. 명세·구현·자체 검토는 Codex가 수행했다. 외부 Claude 검토를 새로 받았다고 표시하지 않는다. politics의 실제6artifact handoff를 사용하며 결과 설명은 `docs/evidence/0.2.1/growth-sensitivity-results-41131479924/evidence-index.json`에서 이 manifest와 실제 게시 index를 참조한다.

기준 source는 `b15992b6150afb8e4f3348501c51051a7f7733d0`다. 사용자 지시의 소유 범위에 따라 작업 템플릿의 계약을 이 README에 기록하며 제품 `docs/tasks`·정본·readiness·기존 workflow·제품 scripts/src/tests는 수정하지 않는다.

## 계약과 권위

| 항목 | 값 |
|---|---|
| 요청 | 같은 저장소의 PR에 소유자 hddy774가 정확한 `evidence-archive:v021-sensitivity-20261031` 라벨을 붙인 실제 `pull_request.labeled` 이벤트 |
| 인증 | 현재 Actions `github.token`을 실제 보관 step의 `GH_TOKEN` 환경으로만 받는다. 로컬 gh·키스토어·제품 서명 secret은 사용하지 않는다 |
| 가드 | repo/actor/triggering_actor/sender/head repo/base repo/label, 실제 PR head·현재 label, 실제 run/event/attempt/path/PR/actor를 재확인하고 정확한 요청 head를 checkout한다 |
| 입력 | SHA로 고정한 manifest의 원형6ZIP, 총9,937,563B. 원래 run41131479924/attempt1의 failure를 그대로 확인한다 |
| source 역할 | 측정7ce66943…, metadata head42ebf359…, as-run workflow49383817…를 구분한다. 제품FP4e228…은 원래 측정 source의 값이다 |
| 보관 | tag `evidence/v0.2.1-20261031`, target commit은 측정 source7ce66943…. 별도 prerelease·make_latest=false |
| 출력 | 원형6ZIP와 `sensitivity-artifact-index-20261031.json` 한 개. 실제 index가 영속 보관 완료의 권위이며 게시 전에는 pending이다 |
| 실패 | 원문/기존 자산을 삭제·덮어쓰지 않는다. 보관 요청의 status.json은 보관 중/완료/실패와 중단 단계를 기록한다. 미완료 release만 실패 단계를 표시한다 |
| 시간/재전송 | run/attempt와 원문 ID는 고정한다. 인위적 최근성/age 조건은 없다. 같은 이름은 크기/전체 byte SHA가 같을 때만 재사용한다. 완료 사본은 Actions 만료 뒤에도 자체 public byte 검증으로 확인한다 |
| 제품 경계 | 공식 v0.2.1/latest/APK·기능/성능/서명/업그레이드 게이트를 변경하거나 실행하지 않는다. fingerprint 선택 범위도 바꾸지 않는다 |

## 실제 동작

1. `manifest.json`의 고정 SHA와 `run-metadata.json` 원문 SHA를 확인한다. 후자는 전달된 원래 API 응답을 개행/내용 변경 없이 보존했다.
2. 실제 소유자 요청/현재 PR/현재 run을 확인한다. stale head, 다른 actor/attempt/event/label을 거부한다.
3. 원래 run의 ID/attempt/event/source/failure와 artifact6개의 name/bytes/server digest/run을 대조한다. 원문 ZIP을 내려받아 전체 SHA256을 확인한 뒤에만 보관 release를 생성한다.
4. 별도 draft에6ZIP을 올리고 인증된 draft 다운로드의 전체 byte SHA를 재검증한다. 기존 이름이 다르면 중단하며 기존 자산 삭제·교체 경로는 없다.
5. prerelease로 게시하며 `make_latest=false`를 명시한다. 공개 URL을 **인증 없이** 내려받아 ZIP6개의 bytes/SHA를 다시 확인한다. redirect가 다른 호스트로 이동하면 Authorization을 제거하고 HTTPS만 허용한다.
6. 이 단계가 모두 통과한 뒤 작은 `state=complete` index를 만들고 공개 다운로드까지 검증한다. index에는 원래 manifest/run 원문, 실패/부분 결과, 영속 asset ID/URL/bytes/SHA, 최초 검증 시각/실행이 들어간다.
7. 정상 소유자 재실행은 같은 index의 최초 trace와 bytes를 보존한다. Actions가 이미 만료됐어도 기존6ZIP이 있으면 보관 사본을 검증해 마무리한다. 공개 사본에서 ZIP이 누락됐으면 draft 검증을 건너뛰어 보수하지 않고 실패한다.

출처의 결과는 workflow failure,4완주·2CLI-timeout partial이며 보고서의 complete=false/requested 값도 원문 그대로다. 보관 완료는 게임·보정·목표 달성의 합격으로 바꾸지 않는다. 로컬9.6MB tar는 복구용이며 여기에 추가 업로드하지 않는다.

## 실행과 상태 확인

구현 PR을 검토한 뒤 root가 전용 라벨로 실제 실행을 조율한다. 이 PR을 준비하면서 라벨을 붙이거나 실제 release/API 쓰기를 실행하지 않는다. 별도 사용자 확인이나 외부 Claude 응답을 추가 실행 조건으로 만들지 않는다.

- 자체 검사: `python3 .github/evidence/archive_sensitivity.py --self-test`
- workflow 실행 명령: `python3 .github/evidence/archive_sensitivity.py --execute --output "$ARCHIVE_OUTPUT"`
- 원형/manifest: [고정 manifest](sensitivity-20261031/manifest.json), [원래 run metadata](sensitivity-20261031/run-metadata.json)
- 실제 요청의 작은 결과는 Actions `evidence-archive-status-<run>-<attempt>`에 보존한다. 원형6ZIP은 별도 prerelease에만 올린다.
- 성공 시 status.json의 `release_url/index_url/index_asset_id/index_sha256`를 politics의 작은 결과 색인에 연결한다. URL 확보 전에는 영속 보관을 완료로 표시하지 않는다.
- 네트워크/권한/충돌/공개 readback 실패는 해당 단계와 함께 남긴다. 자동 재시도·새 게임 실행·기존 release 자산 삭제로 해결하지 않는다.

새 workflow/utility는 기존 `.github/workflows/v021-product.yml`을 포함한 제품 입력을 수정하지 않는다. 일반 PR 생성에 따라 기존 자동 CI가 별도로 실행될 수 있으나 이 보관 workflow/자체 검사에는 Game·측정·성장 실행이 없다. 실제 서명이나 공식 APK 게시 증거로 사용하지 않는다.

## 수용·검사·리뷰

- owner/event/repo/PR/head/run/attempt/label 거부는 외부 쓰기 전에 발생한다.
- 원형 metadata/bytes/SHA 불일치는 draft 생성 전에 거부한다.
- 기존 tag/asset/index 충돌, 공개 ZIP 손상은 삭제·덮어쓰기나 complete index 생성을 허용하지 않는다.
- draft→공개ZIP 전체검증→최종index 순서와 기존 latest/APK 불변을 mock 흐름으로 검사한다.
- 교차 호스트 redirect의 auth 제거와 오류 메시지의 token 미노출을 검사한다.
- 정상 재실행/만료된 Actions 원형/부분 게시에서 immutable 사본을 재사용하는 경계를 확인한다.

mock 검사는 실제 게시/권한/공개 다운로드 성공의 증거가 아니다. Python/manifest/YAML/actionlint·범위/FP 대조 결과는 [checks.json](checks.json)에 기록한다. 실제 영속 URL과 검증 index는 root가 라벨로 실행한 뒤에만 확정한다.
