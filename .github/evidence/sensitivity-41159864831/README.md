# 보충 run41159864831 고정6ZIP 보관 입력

보충 실행의 원형6개를 기존 run41131479924의 6ZIP/태그/색인과 별도로 고정한다. 최초 준비의5개는3seed+inputs+prepare였으며, 실제 종료 때 게시된 collection ZIP에도 실패 검증의 고유 `provenance/request.json`이 있어 root가 이 배치만 **정확히6개**로 확장했다. 임의 원형을 추가하는 기능은 없다. collection90개 중89개가 direct seed ZIP과 byte같고 나머지1개가 위 요청 원문이라는 독립 검증을 연결한다. `verified-six.txt`는 없고 collector는 첫2026 `requested_complete=false` 검사에서 AssertionError·exit1이었다.

## 실제 실패·부분 결과 보존

원래 보충 run은 `completed/failure`다. 1500×42/98/2026와900×98는6000개월·51표본을 완료했고, 두 번째900×42/2026는240분 상한으로 CLI exit124·부분 종료했다. 원문 complete는세seed 모두false이고 requested_complete는42/2026false·98true다. 값 순서를 반전한 보충 계획과 원래 계획을 교차 색인하여 고유6조건의 관찰창이 모여도, 이 실행/계획 전체 합격이나 보정 효과로 바꾸지 않는다.

시드42의900은 progress3550·표본3480, 시드2026의900은 progress5052·표본5040에서 끝났다. 이 부분 결과를6000개월의 최종 상태로 비교하지 않는다. 시드98 두 조건은 기존98의 중복 관측이며 추가 고유조건 수는0이다. 기존 실패/부분 원문과 별도 계획의 SHA를 유지한다.

## 고정 출처와 원형

실제 측정 source는 `7ce66943b539420c9dc1e3fa8a611daae58020ff`, 제품 지문은 `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`다. 이벤트 head는 `19c1faa3a1e6b8a921fd3cbe1642ac3824c955b6`, workflow source는 `836ff97e930e82af36a6f5fae6dae575c109f7d9`로 구분한다. 최신 제품의 기능/성능 합격으로 승격하지 않는다.

| 원형 | artifact ID | ZIP 바이트 |
| --- | ---: | ---: |
| seed42 | 13033147298 | 1,617,563 |
| seed98 | 13030660873 | 1,625,359 |
| seed2026 | 13032846688 | 1,625,931 |
| inputs | 13025494772 | 204,978 |
| prepare | 13026322503 | 871 |
| collection | 13032956600 | 4,877,539 |

합계는 **9,952,241B**다. 세seed ZIP 합계는4,868,853B다. 각 원형의 실제 크기·SHA는 로컬 ZIP과 서버 digest에 맞춰 확인했고, 독립 수집 담당도6개 전체 bytes/SHA/ZIP CRC를 확인했다. `manifest.json`은 원래 ID·이름·크기·SHA·만료 시각·run/attempt를 고정한다. ZIP을 재압축하거나 JSON/완료 플래그를 수정하지 않았다. ZIP/DLL은 Git에 넣지 않는다.

`run-metadata.json`은 root가 저장한 최종 API decoded UTF8 원문을 재직렬화 없이 복사한12,673B 사본이며 SHA256은 `d7dd05a843bf2d6dc73389e7ad807ba1e76606ac158eb7a6e9a7610d4b98c701`이다. manifest의 실제 SHA를 `SUPPLEMENTAL_BATCH.manifest_sha256`에 고정했고 실제6ID도 불변 tuple에 고정했다. 작업 당시 소형 검증은 `/workspace/scratch/sensitivity-41159864831-7ce/final-artifact-verification.json`, 각 `seed-*-outcome-verification.json`/`seed-98-duplicate-verification.json`, 별도 원형 확인은 `/workspace/scratch/supplemental-archive-final-six-original-verification.json`이다.

## 분리된 실행·보존 계약

보관기는 기본으로 항상 기존6개를 선택한다. `--supplemental-41159864831`을 명시해야 이 고정6개를 선택하며 임의 manifest/URL/tag/run CLI는 없다. 소유자 라벨·같은 저장소·실제 PR head/run/attempt·새 workflow 경로를 검사하고, 원문 및 draft/공개 재다운로드의 전체 bytes/SHA와 제품 latest 보호를 유지한다. 누락/변조/미완료 snapshot의 거절은 명시적 mock fixture로 계속 검사하므로 실제 manifest 확정 뒤에도 유효하다.

새 대상은 `evidence/v0.2.1-sensitivity-41159864831`, 색인은 `sensitivity-artifact-index-41159864831.json`, 라벨은 `evidence-archive:v021-sensitivity-41159864831`다. prerelease=true·make_latest=false이며 기존6ZIP/색인/기존 workflow/제품 APK를 수정하지 않는다. 실제 archive·라벨·업로드·다운로드는 root 검토 뒤 별도 실행 대상이다. 이 커밋의 mock/입력 검증 통과를 실제 보관 성공으로 표시하지 않는다.
