# Hora Economic Lab 배포 계약

현재 대상 버전은 `0.2.1`, Android 애플리케이션 ID는 `org.hora.economiclab`이다. 표시명은 **Hora Economic Lab · 호라경제연구실**이다. 사용자의 APK 전용 배포 요청에 따라 **새 GitHub 릴리스의 업로드 첨부는 `HoraEconomicLab-v0.2.1.apk` 한 개**다. Linux ZIP, JSON 검증 보고서, 매니페스트, 체크섬 파일은 릴리스 첨부로 올리지 않는다.

기존 Alpha·실험·v0.2.0 릴리스 기록과 첨부는 삭제하지 않는다. 아래 v0.2.0의7개 첨부 기록은 당시 실제 결과이며 새 배포 정책과 구별한다. 경제 계수의 `experimental` 표시는 배포 파일 종류나 릴리스 번호와 별개다.

## 내부 소스 검증과 빌드

`bash tools/build.sh all`은 다음 순서로 실행하고 실패하면 중단한다.

1. 고정된 Godot4.6.3의 리소스를 가져온다.
2. `tests/run.gd`의 도메인·통합·장기 경제·중립 기본 세계 회귀를 실행한다.
3. `test_ui.gd`, `test_ui_forms.gd`, `test_ui_input.gd`, `test_game_flow.gd`를 각각 실행한다.
4. Linux 실행 파일을 내보내 패키지 안의 실제 코어·정의·라이선스·생성 그래픽과 독립 시작을 검증한다.
5. 비디버그 **미서명 Android release 템플릿 APK**를 내보내 ID·버전·세로 화면·오프라인 권한·ABI·데이터·그래픽 바이트를 확인한다.
6. 실제 소스 커밋·작업 트리 변경 여부와 내부 빌드 입력의 크기/SHA-256을 기록한다.

소스 Actions 아티팩트에는 `HoraEconomicLab-v0.2.1-UNSIGNED.apk`, 내부 Linux ZIP, 검증 보고서와 체크섬이 남는다. `BUILD-MANIFEST.json`의 `artifacts`는 이 **내부 검증 입력 목록**이며 공개 첨부 목록이 아니다. 별도의 `publication_policy: apk_only`, `public_release_assets: [HoraEconomicLab-v0.2.1.apk]`가 공개 범위를 명시한다. 로컬 `linux`/`android` 빌드 대상은 내부 확인을 위해 유지한다.

프로세스 종료 코드가0이어도 `SCRIPT ERROR:`, `Parse Error:`, `ERROR:`가 있으면 실패한다. Android 검증 도구는 build-tools36.0.0을 고정 사용한다. PNG는 가져온 `.ctex`로 패키징될 수 있으므로 원본/가져온 바이트를 정확한 소스와 비교한다. 모든 `data/*.json`, UI·지도 매니페스트, 라이선스도 대조한다.

## 서명·승격·APK 한 개 게시

태그는 `hora-economic-lab/v0.2.1`이다. 별도의 수동 승격에 `source_run_id`와 정확한40자리 `source_commit`을 전달한다. 성공한 동일 저장소 main 실행, 해당 workflow, 정확한 소스 커밋·아티팩트 해시, 준비된 초안/기존 태그의 커밋을 모두 확인한다. 초안에 다른 이름의 첨부가 있으면 삭제하거나 무시하지 않고 승격을 거절한다.

기존 저장소 게시자를 Hora 배포 요청 범위에서 사용하는 정책은 `retained_repository_publisher_for_hora_release_request`다. 사용자가 특정 인증서를 별도로 지정했다고 표현하지 않는다. 기존 Alpha 번들의 스키마·앱 메타데이터·인증서 핀과 새 Hora 앱 ID/버전을 각각 확인하며 Alpha workflow를 변경하지 않는다. 키는 승격의 범위 제한 임시 디렉터리에서만 사용하고 비밀 값은 마스킹한다. 새 키를 만들거나 개발 작업 공간으로 기존 키를 내려받지 않으며 디버그 키 대체도 하지 않는다.

고정 게시자 인증서 SHA-256:

`0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a`

서명 후 서명자 수·고정 인증서·미서명 입력과 동일한 모든 실질 APK 페이로드를 다시 확인한다. 릴리스에는 서명한 APK만 업로드하고, 초안의 첨부 이름/개수/크기를 검사한 뒤 그 APK를 다시 다운로드해 크기와 SHA-256을 대조한다.

원본/서명 빌드 매니페스트, unsigned/signed APK 검증, 서명·소스 workflow·다운로드 보고서, 체크섬은 **게시 전에 Actions 검증 아티팩트**로 보존한다. 릴리스 노트에는 APK 크기/SHA-256, 소스 커밋/성공 실행 링크, 게시자 인증서 지문과 이 검증 아티팩트 링크를 기록한다. 보고서를 릴리스 첨부로 배포하지 않는다. 승격 증명 아티팩트 보관 기간은90일이다.

검증 자료가 보존된 후에만 초안을 `draft=false`, `prerelease=false`, `latest=true`로 공개한다. `releases/latest`에서 태그·ID·소스 커밋·공개 상태와 APK 한 개의 이름/크기를 다시 확인하고 그 결과도 Actions에 보존한다. 정확한 최종 공개 첨부는 다음 한 개다.

- `HoraEconomicLab-v0.2.1.apk`

이 문서는 배포 계약이다. v0.2.1의 실제 성공 실행/공개 다운로드 결과는 확인 후 별도 기록하며, APK의 정적 검증을 전체 Android 기기 플레이 시험으로 확대하지 않는다.

## 이전 로컬 패키징 검증 — 2026-10-25

0.2.0 개발 중 Linux 약80MB 실행 파일과 약62MB 미서명 APK를 내보냈다. 당시 패키지의 코어/정의·라이선스·82개 생성 그래픽 검증과 actionlint1.7.7, 셸/인라인 Python 구문 검사를 통과했다. 이후 `generation.json`이 추가되자 검증기가 이전 APK의 누락을 탐지했다. 따라서 변경된 소스를 다시 검증한 성공 실행의 산출물만 승격한다. 이 과거 로컬 확인에는 서명 비밀을 사용하지 않았다.

Android 기기/에뮬레이터 실행은 별도 증거다. 기본 APK 검증 보고서의 `device_execution_tested`는 `false`이며 전체 모바일 플레이가 검증됐다는 뜻이 아니다.

## v0.2.0 실제 공개 결과 — 2026-10-25

소스 **`84b10bc0869a68f9a9c6e6363819c88336d73772`**, 변경 없는 작업 트리의 [전체 검증/미서명 빌드40162017239](https://github.com/hddy774/project-hora/actions/runs/40162017239)가 성공했다. [승격40162690007](https://github.com/hddy774/project-hora/actions/runs/40162690007)은 이 성공 실행의 아티팩트만 내려받아 기존 게시자 서명과 페이로드 보존을 확인하고, 첨부7개를 업로드·재다운로드한 뒤 공개했다. [Hora Economic Lab v0.2.0](https://github.com/hddy774/project-hora/releases/tag/hora-economic-lab/v0.2.0)은 공개 직후 GitHub API의 `releases/latest`와 일치했고 `draft=false`, `prerelease=false`였다. 이전 Alpha/실험 릴리스는 보존했다.

게시 후 별도 작업 공간에서 공개7개 파일을 다시 내려받아 이름·크기·SHA-256·매니페스트·ZIP 내부 실행 파일·정확한 출처 커밋을 독립 검사했다. 공개 APK를 Android build-tools36의 `apksigner`로 다시 검증하고 ID/버전/오프라인/비디버그/세로/ABI/모든 정의·라이선스·생성 그래픽의 바이트 출처를 확인했다. 공개 Linux ZIP에서 꺼낸 실제 실행 파일은 `--headless --quit-after 10 -- --demo`로 종료0, 오류 없이 시작했다. 이 시작 검사는 전체 Linux GUI 플레이 검수를 뜻하지 않는다. [공개 다운로드 검증 JSON](observations/public-release-v0.2.0.json)에 최종 값을 보존한다.

| 파일 | 크기(바이트) | SHA-256 |
|---|---:|---|
| Android APK | 66,395,805 | `1ccc7b3dfe0d177c78bd130a1912b7c679d03426779f302f7dc32d1aa2c903c3` |
| Linux ZIP | 41,423,481 | `b5b15b10184adb5b8d72f42659318f33d00dcd4897eb8e7dc7a09030b45497e4` |
| ZIP 내부 Linux 실행 파일 | 85,746,896 | `7c0d18ddbdc03e9f12129a06d10343a49ea6ad9d6b1b745b6845505abebf3f47` |

처음 승격 시도40161257405는 같은 셸에서 `GITHUB_ENV` 값을 바로 읽는 환경변수 오류로 서명/업로드 전에 중단됐고 초안을 공개하지 않았다. 현재 셸 export를 명시한 최종 소스에서 전체 검사와 패키징을 다시 통과한 뒤 위 승격이 성공했다. 공식 APK의 전체 모바일 플레이·ARM64 실기기·백그라운드/저장 공간 부족은 미검증이다. 최신 릴리스 표시는 전체 기획과 제품 품질 검수가 끝났다는 뜻으로 사용하지 않는다.

저장소 루트 README는 호라경제연구실 소개로 교체했고 이전 Alpha 설명은 보관 문서로 유지했다. GitHub 저장소 About의 description 수정은 연결 토큰의 Administration 권한 부재로 HTTP403이 반환되어 반영하지 못했다. 공개 README/코드/릴리스 변경에는 이 권한이 필요하지 않았다.


최종 소스84b10bc의 별도 디버그 서명 Android 프로브는 API24/x86_64/host Mesa에서 설치·부팅·네이티브 타이틀 렌더까지만 확인했다. 정확한 프로브 해시와 관측 한계는 [검증 기록](validation.md#최종-소스의-android-개발-프로브)에 따르며 공식 배포 APK 실측과 구별한다.


## v0.2.1 APK 전용 공개 결과 — 2026-10-25

최종 소스 **`86a2f7363ddbe8c8fda35f439083fbb1b1d0ba9d`**의 [전체 소스 CI40165742287](https://github.com/hddy774/project-hora/actions/runs/40165742287)가 성공했다. [게시40166437986](https://github.com/hddy774/project-hora/actions/runs/40166437986)은 같은 성공 실행의 변경 없는 미서명 입력만 서명하고, APK 한 개를 업로드·재다운로드한 뒤 [v0.2.1 최신 릴리스](https://github.com/hddy774/project-hora/releases/tag/hora-economic-lab/v0.2.1)를 공개했다. API의 `releases/latest`는 ID423167967, `draft=false`, `prerelease=false`이며 첨부는 아래 APK 한 개다.

| 공개 파일 | 크기(바이트) | SHA-256 |
|---|---:|---|
| `HoraEconomicLab-v0.2.1.apk` | 66,453,908 | `0d45b3d7e54fbf049cef57379808d1a3bc596c9412c40bbf15ba2a9548cb5b2b` |

게시 후 독립 작업 공간에서도 실제 공개 APK를 다시 내려받아 GitHub 파일 digest·릴리스 노트의 SHA·파일 크기·정확한 소스 태그/성공 main CI를 검사했다. build-tools36 `apksigner`와 APK 정의/라이선스/생성 텍스처 바이트 검사도 통과했다. 공식 앱 ID `org.hora.economiclab`, version0.2.1/code3, SDK24/36, 세로/오프라인/비디버그와 arm64-v8a/x86_64를 확인했다. 보관 게시자 인증서 핀은 이전과 같다.

[Actions 서명·출처 증명 아티팩트](https://github.com/hddy774/project-hora/actions/runs/40166437986/artifacts/12588697142)의 ZIP SHA도 확인해, 원래 미서명 입력·서명 APK·재다운로드 파일·깨끗한 출처 커밋이 일치하고 실질 APK 페이로드가 바뀌지 않았음을 교차 검사했다. 이 자료는 Actions의90일 보관 정책이며 릴리스 첨부가 아니다. 영구적인 공개 다운로드 요약은 [JSON 기록](observations/public-release-v0.2.1.json), 해시·CI·증명 링크는 릴리스 노트에서 확인한다. JSON·체크섬·Linux 파일을 이 공개 릴리스에 첨부하지 않았다.

Android 실제 실행 프로브는 별도 기록을 따른다. APK 정적·서명·바이트 검증을 전체 모바일 플레이나 ARM64 실기기 검수로 확대하지 않는다. 이전 릴리스의 파일과 역사 기록은 그대로 보존했다.


공식 v0.2.1 공개 파일 그대로의 API24/x86_64 프로브는 설치·인증서·네이티브 엔진/GL 초기화·생성 로고 스플래시까지만 확인했다. 소프트웨어 에뮬레이터는 그 이후에 머물러 타이틀/입력/월 정산은 미검증이다. [단계별 기동 기록](observations/android-v0.2.1-official-boot-probe.json)을 전체 Android 플레이 검수와 구별한다.
