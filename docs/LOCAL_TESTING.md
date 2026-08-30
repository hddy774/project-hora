# PC 로컬 실행 및 테스트

## 가장 쉬운 실행 방법

저장소 루트의 `로컬_빌드_및_실행.cmd`를 두 번 클릭합니다. 스크립트가 다음 작업을
순서대로 수행합니다.

1. Release 빌드
2. 전체 자동 테스트
3. .NET 설치가 필요 없는 Windows x64 독립 실행본 생성
4. 바탕화면에 `선로만상록 PC` 바로가기 생성
5. 게임 실행

완성된 실행 파일은
`artifacts/Chronicles-<VERSION>-win-x64/Chronicles.exe`에 있습니다. 이후에는 바탕화면
바로가기를 사용하면 다시 빌드하지 않고 바로 실행할 수 있습니다.

## 수동 명령

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-local.ps1 -CreateDesktopShortcut -Launch
```

코드 변경 없이 기존 실행본만 시험할 때는 바탕화면 바로가기 또는 `Chronicles.exe`를
직접 실행합니다.

## 기본 점검표

- 초기 인구가 10,000명인지 확인
- tick, 수행, 도전, 전투와 사건 기록이 실시간으로 증가하는지 확인
- 1x, 10x, 25x, 50x, 100x와 일시정지가 동작하는지 확인
- 전투력·경지·자질 랭킹이 각각 TOP 1,000까지 표시되는지 확인
- 전체 탐색에서 10,000명을 페이지로 이동하고 이름 또는 `A00001` 검색이 동작하는지 확인
- 인물을 선택했을 때 6대 자질·경지·공법·법술·법보·단약·임무·영석·일대기가 표시되는지 확인
- 전체 경지·전투력 분포, 랭킹 변동률, 승리·패배·사망 통계가 갱신되는지 확인
- 도시 시장의 35개 상품 재고·공급·수요·현재가가 거래에 따라 바뀌는지 확인
- 저장 후 `%LocalAppData%\ChroniclesOfMyriadImmortalPaths\world.cmip`이 생성되는지 확인
- 진행한 뒤 불러오면 저장한 tick과 상태로 돌아가는지 확인

로컬 산출물과 저장 파일은 GitHub Release에 업로드되지 않습니다.
