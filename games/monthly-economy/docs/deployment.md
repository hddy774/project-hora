# Hora Economic Lab 배포 계약

대상 버전은 `0.2.0`, Android 애플리케이션 ID는 `org.hora.economiclab`이다. 표시명은 **Hora Economic Lab · 호라경제연구실**이다. 기존 Alpha 앱과 월 단위 실험 앱의 ID·저장 공간·릴리스 기록은 별도로 유지한다. 데이터의 실험용 수치는 이번 릴리스 번호와 관계없이 `experimental` 상태다.

## 소스 검증과 패키징

`bash tools/build.sh all`은 다음 작업을 실패 시 중단하는 순서로 실행한다.

1. Godot 4.6.3의 리소스 가져오기.
2. `tests/run.gd`의 도메인·통합 시험 전체.
3. `tests/test_ui.gd`, `tests/test_ui_forms.gd`, `tests/test_ui_input.gd`, `tests/test_game_flow.gd`를 각각 실행.
4. Linux 실행 파일을 내보낸 뒤, 패키지 안의 실제 코어·정의 데이터·라이선스·생성 그래픽을 불러오고 독립 실행을 확인.
5. 디버깅을 비활성화한 **미서명 Android release 템플릿** APK를 내보내고 ID·버전·세로 화면·오프라인 권한·ABI·데이터·그래픽 바이트를 검사.
6. 실제 Git 커밋·작업 트리 변경 여부와 각 배포 파일의 크기·SHA-256을 기록.

Godot 종료 코드가 0이어도 `SCRIPT ERROR:`, `Parse Error:`, `ERROR:`가 출력되면 통과시키지 않는다. Android 검증 도구는 `build-tools/36.0.0`을 고정 사용한다. SDK 37의 다른 인증서 출력 형식을 추측해서 받아들이지 않는다.

PNG는 Godot가 가져온 `.ctex`로 패키징될 수 있다. 소스 실행에서는 원본 PNG 해시와 실제 가져온 텍스처 바이트를 함께 대조한다. 릴리스 승격에서는 같은 소스 커밋의 검증 보고서와 APK 내부 바이트를 다시 비교한다. 모든 `data/*.json`과 UI·지도 매니페스트 및 라이선스는 소스 바이트와 직접 비교한다.

## 저장된 게시자 서명과 최신 릴리스

새 태그는 `hora-economic-lab/v0.2.0`이다. GitHub Actions의 소스 검증 작업은 저장소 읽기 권한으로 미서명 입력과 Linux 파일을 만든다. 별도의 수동 승격 작업에 `source_run_id`와 정확한 `source_commit`을 전달해야 한다. 작업은 성공한 동일 저장소 main 브랜치 실행, 정확한 커밋, 아티팩트 해시, 준비된 초안의 커밋과 태그를 먼저 확인한다.

사용자의 완성본 GitHub 배포 요청을 근거로 기존 저장소 게시자 인증서를 같은 소유자의 새 앱에 사용하는 구현 결정을 채택했다. 정책 코드는 `retained_repository_publisher_for_hora_release_request`다. 사용자가 인증서 자체를 별도로 지정했다고 주장하지 않는다. 기존 Alpha 서명 번들의 원래 스키마·앱 메타데이터·인증서 핀을 그대로 확인하고, 새 Hora APK의 ID와 버전은 별도로 검사한다. 원래 Alpha 릴리스 워크플로는 변경하지 않는다.

키는 승격 작업의 범위 제한 임시 디렉터리에서만 사용한다. 비밀 값은 로그에서 마스킹하고 하위 도구에는 필요한 값만 환경 변수로 전달한다. 새 키를 만들거나 개발 작업 공간으로 기존 키를 내려받지 않는다. 서명이 실패하면 디버그 키로 대체하지 않는다. 고정 인증서 SHA-256은 다음과 같다.

`0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a`

서명 후 인증서·서명자 수·APK의 모든 실질 페이로드 바이트를 다시 검사한다. 업로드한 첨부 파일을 다시 다운로드해 SHA-256이 같은지 확인한 다음 초안을 `prerelease=false`, `latest=true`로 공개한다. 최종 파일은 다음과 같다.

- `HoraEconomicLab-v0.2.0.apk`
- `HoraEconomicLab-v0.2.0-linux.zip`
- `BUILD-MANIFEST.json`, `SHA256SUMS.txt`
- `APK-VERIFICATION.json`, `SIGNING-VERIFICATION.json`, `SOURCE-WORKFLOW-VERIFICATION.json`

## 로컬에서 실제 확인한 범위 — 2026-10-25

패키징 개발 중 Linux 약 80 MB 실행 파일과 Android 약 62 MB 미서명 release APK를 실제로 내보냈다. Linux 패키지에서 당시 코어와 모든 정의·라이선스, **82개 생성 그래픽 항목**을 불러오는 검사가 통과했다. 워크플로는 actionlint 1.7.7을 통과했고, 셸 구문 및 9개 인라인 Python 블록의 컴파일과 패키징 Python 구문 검사를 통과했다. 인물·회사 모듈의 독립 시험은 당시 70개 검증을 통과했다.

이 로컬 패키지는 통합 개발 중 생성한 점검용이다. 이후 추가된 `generation.json`이 이전 APK에 없음을 바이트 검증기가 실제로 탐지해 이전 패키지를 최신 소스와 일치한다고 판정하지 않았다. 소스 동결 후 전체 시험과 내보내기를 다시 수행한 성공 실행의 산출물만 최종 승격 대상으로 삼는다. 서명 비밀은 이 로컬 확인 과정에서 사용하지 않았다.

Android 실제 기기·에뮬레이터 실행 여부는 별도 증거로 기록한다. 정적 APK 검증 자체는 실행 시험을 뜻하지 않으며, 기본 보고서의 `device_execution_tested`는 `false`다. 최종 CI 실행 번호·커밋·배포 URL과 실행 시험 결과는 확인 후 기록한다.


## v0.2.0 실제 공개 결과 — 2026-10-25

소스 **`84b10bc0869a68f9a9c6e6363819c88336d73772`**, 변경 없는 작업 트리의 [전체 검증/미서명 빌드40162017239](https://github.com/hddy774/project-hora/actions/runs/40162017239)가 성공했다. [승격40162690007](https://github.com/hddy774/project-hora/actions/runs/40162690007)은 이 성공 실행의 아티팩트만 내려받아 기존 게시자 서명과 페이로드 보존을 확인하고, 첨부7개를 업로드·재다운로드한 뒤 공개했다. [Hora Economic Lab v0.2.0](https://github.com/hddy774/project-hora/releases/tag/hora-economic-lab/v0.2.0)은 GitHub API의 `releases/latest`와 일치하며 `draft=false`, `prerelease=false`다. 이전 Alpha/실험 릴리스는 보존했다.

게시 후 별도 작업 공간에서 공개7개 파일을 다시 내려받아 이름·크기·SHA-256·매니페스트·ZIP 내부 실행 파일·정확한 출처 커밋을 독립 검사했다. 공개 APK를 Android build-tools36의 `apksigner`로 다시 검증하고 ID/버전/오프라인/비디버그/세로/ABI/모든 정의·라이선스·생성 그래픽의 바이트 출처를 확인했다. 공개 Linux ZIP에서 꺼낸 실제 실행 파일은 `--headless --quit-after 10 -- --demo`로 종료0, 오류 없이 시작했다. 이 시작 검사는 전체 Linux GUI 플레이 검수를 뜻하지 않는다. [공개 다운로드 검증 JSON](observations/public-release-v0.2.0.json)에 최종 값을 보존한다.

| 파일 | 크기(바이트) | SHA-256 |
|---|---:|---|
| Android APK | 66,395,805 | `1ccc7b3dfe0d177c78bd130a1912b7c679d03426779f302f7dc32d1aa2c903c3` |
| Linux ZIP | 41,423,481 | `b5b15b10184adb5b8d72f42659318f33d00dcd4897eb8e7dc7a09030b45497e4` |
| ZIP 내부 Linux 실행 파일 | 85,746,896 | `7c0d18ddbdc03e9f12129a06d10343a49ea6ad9d6b1b745b6845505abebf3f47` |

처음 승격 시도40161257405는 같은 셸에서 `GITHUB_ENV` 값을 바로 읽는 환경변수 오류로 서명/업로드 전에 중단됐고 초안을 공개하지 않았다. 현재 셸 export를 명시한 최종 소스에서 전체 검사와 패키징을 다시 통과한 뒤 위 승격이 성공했다. 공식 APK의 전체 모바일 플레이·ARM64 실기기·백그라운드/저장 공간 부족은 미검증이다. 최신 릴리스 표시는 전체 기획과 제품 품질 검수가 끝났다는 뜻으로 사용하지 않는다.

저장소 루트 README는 호라경제연구실 소개로 교체했고 이전 Alpha 설명은 보관 문서로 유지했다. GitHub 저장소 About의 description 수정은 연결 토큰의 Administration 권한 부재로 HTTP403이 반환되어 반영하지 못했다. 공개 README/코드/릴리스 변경에는 이 권한이 필요하지 않았다.


최종 소스84b10bc의 별도 디버그 서명 Android 프로브는 API24/x86_64/host Mesa에서 설치·부팅·네이티브 타이틀 렌더까지만 확인했다. 정확한 프로브 해시와 관측 한계는 [검증 기록](validation.md#최종-소스의-android-개발-프로브)에 따르며 공식 배포 APK 실측과 구별한다.
