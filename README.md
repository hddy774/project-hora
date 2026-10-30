# 선로만상록

두 반선 시조에서 시작해 출생·수행·생업·가족·종문·왕조와 윤회가 이어지는 선협 생태계 시뮬레이션입니다. 관찰과 플레이는 같은 세계와 명령 규칙을 사용합니다.

현재 **0.2.1 전체 기능을 개발 중**입니다. 게임 엔진 없이 C#으로 세계를 구현하며, Android와 Windows가 같은 세계 코어와 화면을 사용합니다. 기능 구현·통합 검증을 마친 뒤 성능 시험과 최종 APK 배포를 진행합니다.

## 개발과 기획

- [통합 기획 — Notion](https://app.notion.com/p/408acc9e6175817ab59bd91bb9dc011d)
- [111개 작업·24개 불변식 원장](docs/plans/v021-tasks.json) · [정책과 개발 기본값](docs/plans/v021-canon-map.md)
- [GitHub 이슈](https://github.com/hddy774/project-hora/issues) · [메인·서브 PR](https://github.com/hddy774/project-hora/pulls)
- [개발 증거](docs/evidence/0.2.1) · [작업 분야와 통합 경계](docs/plans/v021-workstreams.md)

2026-10-30 사용자 결정: 혈연 후손 간 허용, 출산은 양친 모두 금단 이상, 잠정 수치는 개발 보정에 위임합니다. 기본 창세는 반선 시조 2명·종문 0개이며 윤회 13개 목표를 유지합니다.

## 소스 구성

| 경로 | 역할 |
| --- | --- |
| `src/Chronicles.Game` | 플랫폼 독립 세계·명령·지식·저장·조회 |
| `src/Chronicles.Shell` | 공용 화면과 로컬 개발 호스트 |
| `src/Chronicles.Android` | Android 오프라인 앱 |
| `src/Chronicles.Desktop` | Windows 앱 |
| `tests/Chronicles.Game.Tests` | 기능·저장·정보 경계 시나리오 |
| `docs/plans` | 정본 대조·결정·작업 원장 |
| `legacy/v0.2.0` | 기존 선로만상록 출시 소스·설계·검증 자료 |

새 세계 구현은 `Chronicles.Game`에서 진행합니다. 이전 Windows 시뮬레이션은 회귀 비교 자료로 보존합니다. 기존 0.2.0 저장의 연속 플레이는 지원 범위에 포함하지 않습니다.

## 로컬 실행

.NET 10 SDK를 사용합니다.

```bash
dotnet run --project tests/Chronicles.Game.Tests -c Release
python3 scripts/validate-v021-plan.py --self-test
dotnet run --project src/Chronicles.Shell
```

Android는 Android workload·SDK 36·JDK 21 설치 후 빌드합니다. 앱 ID는 기존 배포의 `org.hora.xianxiaobserver`를 유지합니다. 배포 서명은 GitHub CI의 기존 인증서로 수행하며 저장소에 키를 저장하지 않습니다.

```bash
dotnet build src/Chronicles.Android -c Release
dotnet run --project src/Chronicles.Desktop -c Release
```

[저장소 이전 기록](docs/evidence/0.2.1/repository-migration.json)에 출처와 보존 범위를 남겼습니다. 선로만상록과 관계없는 경제 실험 프로젝트는 현재 소스에서 제거했습니다.
