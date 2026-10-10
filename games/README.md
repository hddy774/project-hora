# Project Hora Android 게임 목록

한 저장소에서 **게임마다 독립된 Android 앱/APK**를 관리합니다. 게임 선택용 통합 앱은 만들지 않습니다.

| 게임 | 앱 ID | 배포 확인 | 소스 |
|---|---|---|---|
| [ALPHA EXCHANGE · 알파 익스체인지](alpha-exchange/README.md) | `com.alphaexchange.offline` | [게임별 릴리스 검색](https://github.com/hddy774/project-hora/releases?q=alpha-exchange) | `src/AlphaExchange.Core`, `src/AlphaExchange.Android` |

릴리스 검색은 배포가 있을 때 해당 게임의 태그/자산을 확인하는 경로이며, 현재 다운로드 가능한 APK가 있다는 보장이 아닙니다. 기존 배포 정리와 서명 체계 재정비는 별도 작업입니다. 저장소 전체의 `releases/latest`는 다른 게임을 가리킬 수 있어 게임별 다운로드에 사용하지 않습니다. 아직 새 게임은 추가하지 않았습니다.

## 개발 명령

Python 3.10 이상과 저장소의 .NET SDK/Android 요구 사항을 사용합니다.

```sh
python3 scripts/games.py list
python3 scripts/games.py validate
python3 scripts/test_games.py
python3 scripts/games.py check alpha-exchange
python3 scripts/games.py check alpha-exchange -- --storage-load
python3 scripts/games.py build alpha-exchange
./build.sh alpha-exchange
```

`./build.sh`의 무인자 실행은 기존처럼 ALPHA EXCHANGE를 선택합니다. 빌드 결과는 게임별 `dist/<slug>/`에 생깁니다. 빌드/CI의 개발용 서명 APK는 공개 배포 APK가 아닙니다.

## 다른 게임 추가

1. `games/<slug>/game.json`에 schemaVersion 1, 고유 id/name/applicationId/apkPrefix, androidProject/checksProject, releaseNotes 경로를 등록합니다. 그림 검사가 있으면 artworkCheck를 지정합니다.
2. 새 게임의 엔진·앱·테스트·아트를 `games/<slug>/` 아래에 독립적으로 둡니다. 기존 ALPHA EXCHANGE 경로는 호환성을 위해 이동하지 않습니다. 금융 엔진을 모든 게임의 공통 코어로 만들지 않습니다.
3. Android 프로젝트의 ApplicationId와 등록값을 맞춥니다. ApplicationDisplayVersion/ApplicationVersion은 프로젝트가 유일한 원본이며 각 게임이 독립적으로 올립니다. 기능 배포는 중간 번호를 올린 `X.Y.0`을 사용합니다. 두 게임의 앱 ID/프로젝트/APK 파일 접두사가 겹치면 검사가 실패합니다.
4. `games.py validate`와 해당 게임 검사/빌드를 통과시킵니다. CI는 등록된 모든 게임의 CPU 검사와 Android 빌드를 matrix로 실행합니다. ALPHA EXCHANGE의 과거 기록/회계/에뮬레이터 검사는 해당 게임만 실행합니다. 새 게임의 네이티브 시나리오는 별도로 추가합니다.
5. 공개 배포가 필요할 때만 해당 게임의 signing.script와 고유 signing.secretPrefix, 승인된 공개 인증서 지문 signing.certificateSha256를 구성합니다. 스크립트는 기존 서명 인터페이스와 `SIGNING-INFO.txt`/인증서 검증 형식을 따라야 합니다. 서명 키 생성·보관·접근 권한은 별도 승인 대상입니다. 등록 파일에 비밀값을 넣지 않습니다.

공유되는 것은 목록/빌드/검사/릴리스 라우팅뿐입니다. 게임 간 런타임 의존성을 강제하지 않습니다.

## 게임별 릴리스

- 새로운 태그는 `<slug>/vX.Y.0`입니다. ALPHA EXCHANGE만 과거 `vX.Y.0` 경로도 해석합니다. 기존 태그와 APK 이름 규칙을 보존합니다. 공개 릴리스/자산의 삭제 여부는 별도 배포 관리 작업입니다.
- APK 이름은 `<apkPrefix>-v<version>.apk`입니다. ALPHA EXCHANGE는 `AlphaExchange-v1.7.0.apk` 같은 기존 이름을 유지합니다.
- main 병합, 브랜치 push, 태그 push는 공개 배포를 시작하지 않습니다. 권한 있는 사용자가 **Manually publish one Android game**에서 game과 이미 main에 포함된 버전 태그를 선택해야 합니다. 워크플로는 태그를 만들거나 이동하지 않습니다.
- 선택한 태그에 이 registry/build 기반과 해당 버전의 릴리스 문서가 있어야 합니다. 기반 추가 전의 과거 태그를 재빌드하는 도구가 아닙니다. 과거 태그는 소스 이력이며 공개 APK의 존재를 뜻하지 않습니다.
- 영구 서명 설정은 `<secretPrefix>_SIGNING_BUNDLE`에 한 번에 보관할 수 있습니다. 기존 네 개 비밀값과 번들이 함께 있으면 모호한 설정으로 중단합니다. 공개 인증서 지문은 게임 등록값과 일치해야 합니다. 자세한 소유자 직접 실행 절차는 [서명 설정 안내](../docs/ANDROID-SIGNING-SETUP.md)를 따릅니다.
- 기존 저장된 서명 비밀 이름은 `<secretPrefix>_KEYSTORE_BASE64`, `_KEYSTORE_PASSWORD`, `_KEY_ALIAS`, `_KEY_PASSWORD`입니다. ALPHA EXCHANGE만 기존 `ANDROID_*`와 역사적 fallback 이름을 유지합니다. 다른 게임에 그 키를 자동 적용하지 않습니다.
- 릴리스에서 일회용 키를 생성하는 선택지는 제거했습니다. 키 생성은 별도 소유자 직접 실행 bootstrap에만 있으며, 이 변경은 생성·보관을 실행하거나 기존 인증서 지문을 바꾸지 않습니다. 새 지문은 따로 승인해야 합니다.
- 서명 완료 아티팩트로 복구할 때도 같은 소스 커밋, 앱 ID/버전/인증서/체크섬을 검증합니다. 이미 공개된 릴리스를 덮어쓰지 않습니다. 게임별 릴리스에 저장소 전체의 latest 표시를 강제하지 않습니다.

앱 ID와 저장 형식 보존만으로 설치 업데이트가 보장되지는 않습니다. 기존 v1.7.0은 버린 일회용 서명키를 사용했으므로 동일 키 재서명이 불가능합니다. 향후 안정된 서명키와 백업/재설치 전환은 별도 결정이 필요합니다.
