# PC 0.0.22 실행 복구·월 경계·지도 통합 수직 계약

## 범위

- 게시 실행 파일이 WPF 리소스 오류로 조용히 종료되지 않도록 시작 경로를 복구한다.
- `AdvanceTo` 호출을 한 번에 하거나 여러 runtime pulse로 나눠도 같은 월별 권위 전이와
  AI 의사결정 랭킹을 사용하도록 시간 경계를 고정한다.
- 별도 최상위 화면이던 `지역 활동`을 제거하고
  `하천대륙 → 주(권역) → 지점` 선택 아래의 지점 상세로 통합한다.
- 이번 변경은 새 도시·종문·가문·촌이나 99개 주의 권위 상태를 임의로 만들지 않는다.

## 시작 복구와 XAML 리소스 계약

- 콘텐츠의 법칙 검토 화면이 존재하지 않는 `NegativeBrush`를 참조하던 두 곳을 실제
  Application 리소스인 `DangerBrush`로 교정한다.
- `validate-xaml-resources.ps1`은 Desktop의 모든 최상위 XAML 파일을 읽고
  `StaticResource`·`DynamicResource` key가 해당 파일 또는 `App.xaml`에 정의됐는지
  검사한다. 이 검사는 architecture 검사와 로컬 게시 절차의 첫 단계에 연결된다.
- `StartupUri`에 맡기던 창 생성을 `App.OnStartup`의 명시적 `MainWindow` 생성으로
  바꾼다. 시작 예외는 사용자에게 오류 창을 보여 주고
  `logs/debug/automatic`의 5MiB 회전 writer로 상세를 남긴다.
- 로컬 게시 절차는 게시된 `Chronicles.exe`를 실행한 뒤 20초 안에 정확한 실행 파일
  경로와 `선로만상록 PC v<version>` 창 제목을 확인한다. 바로가기를 만들었으면 같은
  검사를 `.lnk`에도 수행하고, 검사 프로세스는 정상 종료한다.
- 바탕화면 바로가기는 사용자 프로필의 실제 게시 실행 파일과 작업 폴더를 가리켜야
  한다. `CodexSandboxOffline` 경로, 존재하지 않는 대상 또는 바탕화면 밖 경로는
  성공으로 처리하지 않는다.

## 월 경계 결정론과 checkpoint schema 31

- 도시 생산, 요수 재생, 조직 전이, 지속 콘텐츠 생성은 호출별 임시 cache가 아니라
  `_lastMonthlyBoundaryMonth`를 기준으로 각 게임 월에 정확히 한 번 실행한다.
- AI가 의사결정에 사용하는 전투력·경지·자질·부자 랭킹은 새 달 경계에서 직전 달의
  dirty 결과를 확정한다. 같은 달 안에서는 runtime pulse 크기나 관찰 호출에 따라 다시
  정렬하지 않는다.
- 인과 사실에 기록하는 전투력 순위는 이미 1-based인 inverse rank를 다시 `+1`하지
  않는다.
- checkpoint schema 31은 마지막 처리 월, 네 랭킹의 agent별 1-based 순위와 dirty
  상태를 `DecisionRankingCheckpoint`로 보존한다. 상태 hash와 wire에 포함하며 잘못된
  순열·길이·미래 월을 복원 전에 거부한다.
- schema 30 이하는 기존 계약으로 랭킹을 한 번 재구성한 뒤 현재 월부터 schema 31의
  월 경계 계약을 적용한다.

## 대륙 → 주(권역) → 지점 지도 통합

- 지도 작업공간의 가시 선택 경로는 `하천대륙 10×10 주 → 오천주 → 층천별 10×10
  지점` 하나로 고정한다. 제1층천과 제2층천은 같은 물리 지점 ID를 공유하되 서로 다른
  view node·자연환경·정착지 profile을 표시한다.
- 기존 Production 3×3 지역 18개는 오천주 지점 10×10의 중앙 3×3에 층천별로
  일대일 대응한다. 이 대응은 기존 지역 활동 권위를 새 지점에서 관찰하기 위한
  compatibility mapping이며 새 지리 권위를 복제하지 않는다.
- 지점 내부 탭은 `지점 개요 / 지역 활동 / 수선자 / 요수·생령 / 도시·거점 /
  자원·영맥`으로 나눈다. 지역 활동, 최고 인물, 현재 요수, 실제 영맥 공급처럼 기존
  Production 권위가 있는 정보만 대응 지점에서 표시한다.
- 중앙 3×3 밖의 지점과 아직 Production이 아닌 99개 주는 생성 청사진만 보여 준다.
  현재 활동·개별 정착지·조직·영맥 Source가 없으면 0이나 추정 권위로 꾸미지 않고
  `DataUnavailable`로 표시한다.
- `수선자` 내부 탭을 실제로 선택했고 Production 지점이 연결된 경우에만 기존
  location ID를 runtime 상세 요청으로 변환한다. 다른 내부 탭과 미구현 지점은 상세
  인물 page를 요청하거나 행을 materialize하지 않는다.

## 자동 검증 계약

- 모든 Desktop XAML의 리소스 참조가 정의되어 있어야 한다.
- 단일 호출, 월 단위 호출, 불규칙 chunk, 모든 지원 배속의 80ms pulse, 중간
  checkpoint 복원은 같은 state hash·RNG·사건·연대기·네 랭킹·저장 wire를 만든다.
- 기존 18개 Production 지역은 층천별 중앙 3×3 지점에 중복 없이 대응하고, 중앙
  3×3 밖 지점은 활동 권위를 임의로 연결하지 않는다.
- 게시 실행 파일과 갱신한 바탕화면 바로가기는 실제 창 제목까지 확인하는 시작
  스모크 검사를 통과해야 한다.

## 코드 고정 전 검증 상태

- 구현 커밋: `5279d5d`, `85fa54c`, `ff9b26e`, `d17628b`.
- Release build: 경고 0·오류 0.
- 정적 계약: XAML 9개·참조 705개·누락 0, architecture module 8개·Desktop 권위
  우회 0건, 238개 이식 원장 PASS.
- 전체 자동 검사: `RESULT: 66/66 passed`.
- 10,000명·20,000 tick 별도 프로세스 5회 합계: 626 / 504 / 564 / 599 /
  559ms, 중앙값 564ms. checkpoint 중앙값 73ms, schema 31 복원 163ms.
- SQLite 5-process 중앙값: 저장 973ms·복원 951ms. 범위는 저장 904~1,106ms,
  복원 875~1,114ms이며 권고 1초 초과 회차를 포함한다.
- 게시 실행 파일·바로가기 시작 스모크와 화면 자동 조작은 VERSION 완료 SHA에서
  재게시한 뒤 `docs/evidence/0.0.22/verification.md`에 고정한다.

마지막 항목까지 고정 SHA에서 채워지기 전에는 `Packaged`로 표시하지 않는다.
GitHub Actions와 GitHub Release는 실행하지 않으므로 이 PC 버전을 `Released`로
표현하지 않는다.
