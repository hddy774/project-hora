# 이전 Windows 구현 보관

이 폴더는 현재 0.2.1 제품에서 분리한 관련 Windows 엔진·UI·SQLite 저장·테스트와
추적 자료의 출처 보관본이다. 현재 제품은 루트 `Chronicles.slnx`의 Core/Game/Hosting과
네이티브 호스트를 사용한다. 이 폴더는 기본 빌드·APK·현재 기능 합격 수에 포함하지 않는다.

`source-provenance.json`은 각 원본 경로·commit·보관 경로·SHA-256을 기록한다.
이전 엔진 소스의 이동 기준점은 `91e5d32fc4a9c034901f61a5bfd3ba922ce1f98b`다.
자체 실행 가능한 이전 WPF wiring, 아키텍처 manifest/규칙과 VERSION은 마지막 이전
entry 기준점 `5279d5dab568a48d8b5ab9600bda29600c41767d`에서 보존했다.
그 보관 VERSION은 과거 Windows 빌드의 값이며 현재 제품 버전의 원본이 아니다.
현재 제품은 루트 VERSION만 사용한다.

`Chronicles.slnx`의 모든 프로젝트 참조는 이 폴더 안에서 끝난다. 이전 Windows 실행은
`App.OnStartup -> MainWindow -> GameApplicationSession -> SimulationRunner`다.
현재 제품의 `EcologyWindow`, `GameSession`, `GameWorld`를 참조하지 않는다.
Windows 전용 `winsqlite3.dll` 어댑터도 이 보관본에 남는다.

이전 Core의 사용 중인 두 순수 파일과 bounded log writer는 현재 제품에도 재사용한다.
이것은 두 세계 권위를 연결하는 참조가 아니며 프로젝트 그래프는 서로 분리되어 있다.

보관 소스와 프로젝트 구조는 검사할 수 있다. 이전 suite에는 측정이 포함되므로 모든 기능
완료 전에는 실행하지 않는다. 일부 역사적 추적 생성기는 당시 외부 Alpha 정본 manifest를
입력으로 요구했다. 그 외부 프로젝트나 데이터를 새로 들여오지 않았으며, 보관 manifest와
기존 추적 자료가 생성기 입력 전체를 대체한다고 주장하지 않는다.

현재 경계·저장 어댑터·Unity 후속 한계는
[0.2.1 제품 경계](../../docs/architecture/v021-product-boundary.md)를 따른다.
