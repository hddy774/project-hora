# 선협 관찰

수선자·종문·전쟁·윤회를 관찰하는 한국어 오프라인 게임입니다. C# 시뮬레이션과 Avalonia 화면을
Android·데스크톱에서 공유합니다. React 원본은 `legacy/original-app.js`에 보존했습니다.

일곱 탭(천하·종문·인물·사기·업적·현황·기록), 상세 시트, 천기 개입과 기기 내 저장을 제공합니다.
범위와 수용 기준은 [XO-01](../../docs/tasks/XO-01-release-completion.md)을 참고하세요.

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
검사 → APK 빌드 → 보관된 게시자 인증서 서명 → 동일 APK의 Android 에뮬레이터 검사 →
APK 한 개만 초안에 업로드 → 재다운로드 해시 검사 → 최신 안정 릴리스 게시 순서로 처리합니다.
출처가 다른 기존 태그와 게시된 릴리스는 덮어쓰지 않습니다. 새 버전은 프로젝트·워크플로·서명·게시
도구의 버전과 릴리스 태그를 함께 갱신해야 합니다.

키스토어는 CI의 임시 폴더에서만 사용하며 저장소나 개발 환경으로 가져오지 않습니다.
스크린샷·로그·서명 보고서는 Actions 아티팩트에만 남고 릴리스 첨부는 APK 한 개입니다.

게시 단계만 실패한 경우에는 워크플로의 `source_run_id`에 이전 실행 ID를 넣고 `publish=true`로
실행하면 같은 실행의 검사·서명·Android 성공을 확인한 뒤 그 APK만 게시합니다. 재빌드하지 않습니다.
CLI에서는 같은 실행의 검사·서명·Android 성공과 아티팩트 해시를 확인한 뒤,
해당 실행의 APK와 서명 보고서를 작업 폴더의 `release-input/`에 두고
`publish-release.py prepare --source-run <실행 ID>`로 게시만 재시도할 수 있습니다.
`readback --source-run <실행 ID>`로 초안 APK의 다운로드를 검증한 다음
`publish --source-run <실행 ID>`를 실행합니다.
이 경로도 인증된 저장소 소유자와 동일 실행의 출처·검사·APK 해시를 요구합니다.
