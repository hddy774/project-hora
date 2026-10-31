# 엔진 없는 기준선과 호환 경계

출발 코드 main b5c46a3(제품0.0.22), 원장 PR7 5455c60, UI/성능 재사용 후보8031bb3를 고정했다.
0.2.0 원본 55개 ID는 v021-tasks.json의 v020_mapping에서 111개 작업에 대응한다.
0.2.0을 먼저 별도 출시해야 하는 의존은 없다.

기존0.0.22 Linux Release 검사: 64/66. SQLite는 Windows 전용, 로그 시험은 Directory.GetFiles 순서 가정을 포함한다.
동일 기존 검사와 WPF 빌드는 Windows 원격 CI에서 통과했다:
https://github.com/hddy774/Chronicles-of-Myriad-Immortal-Paths/actions/runs/41002208799

추출 전 기존10,000명 기준선: 초기화37ms, 20,000tick380ms, 관찰0.8ms, checkpoint70ms, 복원152ms.
이 수치는 새 두 시조 세계의 비용이나 Android 실측으로 치환하지 않는다.

현재 공용 Game은 Core만 참조하고 UI/Unity/Windows SQLite를 참조하지 않는다.
새 각 호스트는 직렬화된 GameSession을 통한다. 이전 World는 별도 저장/별도 신원 공간의 회귀 기준선으로 남으며 새 앱 시작 경로가 아니다.

.NET10·C# 최신 문법을 현재 호스트에서 사용한다. 향후 Unity 연결에는 netstandard2.1/API/언어 하위 집합에 대한 별도 어댑터/포팅 검증이 필요하다.
Span/SIMD/Burst/네이티브를 도입했다는 주장은 하지 않는다. 현재 선택은 관리 C# 스칼라 경로이며 실제 비용/정답 비교가 우선이다.

확정 계약과 개발 기본안, 의도된 기준선 차이는 v021-canon-map.md에 기록했다.
