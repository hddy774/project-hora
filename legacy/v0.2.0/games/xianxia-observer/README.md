# 선로만상록 프로젝트

수선자·종문·전쟁·윤회를 관찰하는 한국어 오프라인 게임입니다. C# 시뮬레이션과 Avalonia 화면을
Android·데스크톱에서 공유합니다. React 원본은 `legacy/original-app.js`에 보존했습니다.

고서 목판 테마의 다섯 주 탭(천하·종문·인물·사기·더보기)과 더보기의 업적·현황·기록,
네 분류 인물 상세·개입 확인·기기 내 저장을 제공합니다. 시스템 글자100/130/200%를 지원합니다.
범위와 수용 기준은 [XO-01](../../docs/tasks/XO-01-release-completion.md)을 참고하세요.

전체 기획·규칙·밸런스·화면·아키텍처·저장·Android·검증·후속 제안은
[기획·기술 문서 목차](docs/README.md)에 정리했습니다.
클로드 피드백을 요청할 때는 [검토 요청문](docs/10-claude-review-request.md)을 함께 전달하세요.

## 실행과 검사

이 디렉터리에서 .NET 10 SDK로 실행합니다.

```sh
dotnet restore XianxiaObserver.slnx --locked-mode
dotnet format XianxiaObserver.slnx --verify-no-changes --no-restore
bash ../../tools/check-code-rules.sh .
dotnet build XianxiaObserver.slnx -c Release --no-restore -warnaserror
dotnet test XianxiaObserver.slnx -c Release --no-build
dotnet run --project src/Xianxia.Desktop
```

헤드리스 UI 검사는 실제 입력 흐름을 실행하고 `tests/Xianxia.App.Tests/bin/Release/net10.0/screens/`에
화면을 남깁니다. 시뮬레이션 검사는 결정론·영석 보존·저장·장기 진행 회귀를 검사합니다.

Android 프로젝트는 별도 `android` 워크로드, Java 17, Android SDK 36이 필요합니다.
`XianxiaObserver.slnx`에는 워크로드 없이 실행 가능한 프로젝트만 포함합니다.

```sh
dotnet workload install android
dotnet publish src/Xianxia.Android -c Release -f net10.0-android
```

## APK 배포

GitHub Actions **Xianxia Observer**에서 소스 브랜치를 선택하고 `publish=true`로 실행합니다.
검사 → APK 빌드 → 보관된 게시자 인증서 서명 → 동일 APK의 Android API24/34 실입력·저장 업데이트 검사 →
APK 한 개만 초안에 업로드 → 재다운로드 해시 검사 → 최신 안정 릴리스 게시 순서로 처리합니다.
출처가 다른 기존 태그와 게시된 릴리스는 덮어쓰지 않습니다. 새 버전은 프로젝트·워크플로·서명·게시
도구의 버전과 릴리스 태그를 함께 갱신해야 합니다.

키스토어는 CI의 임시 폴더에서만 사용하며 저장소나 개발 환경으로 가져오지 않습니다.
전체 스크린샷·로그·서명 보고서는 Actions 아티팩트에 보관하고 대표 PNG는 문서에도 포함합니다. 릴리스 첨부는 APK 한 개입니다.

게시 단계만 실패한 경우에는 워크플로의 `source_run_id`에 이전 실행 ID를 넣고 `publish=true`로
실행하면 같은 실행의 검사·서명·Android 성공을 확인한 뒤 그 APK만 게시합니다. 재빌드하지 않습니다.
CLI에서는 같은 실행의 검사·서명·Android 성공과 아티팩트 해시를 확인한 뒤,
해당 실행의 APK와 서명 보고서를 작업 폴더의 `release-input/`에 두고
`publish-release.py prepare --source-run <실행 ID>`로 게시만 재시도할 수 있습니다.
`readback --source-run <실행 ID>`로 초안 APK의 다운로드를 검증한 다음
`publish --source-run <실행 ID>`를 실행합니다.
이 경로도 인증된 저장소 소유자와 동일 실행의 출처·검사·APK 해시를 요구합니다.

현재 공식 버전은 **[0.2.0 / versionCode2](https://github.com/hddy774/project-hora/releases/tag/xianxia-observer/v0.2.0)**입니다. [APK 다운로드](https://github.com/hddy774/project-hora/releases/download/xianxia-observer/v0.2.0/XianxiaObserver-v0.2.0.apk). 변경점과 구 저장 안내는 [0.2.0 버전 노트](docs/release-notes-v0.2.0.md), [밸런스 실측](docs/19-balance-v0.2.0.md)을 봅니다.

공식 APK 소스는 `09e8c693e3ebb52d2495bffb1a48364eb9396dce`, SHA256은 `b381a400514ea311117bb2b28d15c68baffc00d4ea71a689c9140a69867bf2df`입니다. API24·34 공식 수용과 공개 다운로드·기존 인증서·ARM64/x86_64 확인을 마쳤습니다. 자세한 출처는 [Android 수용 증거](docs/21-android-v0.2.0-verification.md)에 있습니다.
