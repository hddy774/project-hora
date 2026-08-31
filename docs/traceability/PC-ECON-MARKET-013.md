# PC-ECON-MARKET-013 · 도시 시장 원자 정산과 현재 수급

## 범위와 정본

- PC 버전 0.0.13의 도시·시장 개선 작업이다.
- Notion M10/P10-05의 `Offer → preflight → transfer → record`, source result 중복
  방지, 실제 소유권 이전, UI 읽기 전용 관찰 계약을 PC `CityMarket` 경계에 적용한다.
- 모바일 UI·JNI 계약은 적용하지 않고 C# Simulation/Persistence/WPF 계약으로 옮긴다.

## 권위 owner와 대표 검색 진입점

| 책임 | owner | 대표 파일 |
|---|---|---|
| 상품·가격·최근/누적 수급·생산 | Simulation | `CityMarket.cs`, `EconomyRules.cs` |
| 구매·판매·임무 원자 정산 | Simulation | `CityMarket.Settlement.cs` |
| 수선자 자산 반영 | Simulation | `World.Economy.cs` |
| schema 24·검증·hash | Simulation/Persistence | `WorldCheckpoint.cs`, `World.Persistence.cs`, `World.cs` |
| 읽기 전용 시장 표시 | Desktop | `MarketObservationView.xaml(.cs)` |
| 결정론·경계·성능 회귀 | Tests | `tests/Chronicles.Tests/Program.cs` |

## 구현 계약

1. 누적 공급·수요는 감사값이고 현재 가격·임무·생산은 최근 12개월 수급을 사용한다.
2. 구매력이 없는 시도는 수요가 아니다. 지불 가능하지만 재고가 부족한 시도는 현재
   미충족 수요이며 동일 source로 두 번 기록할 수 없다.
3. 구매·판매·임무 납품은 모든 도시/수선자 용량을 먼저 검증한다. 성공이면 두 자산과
   원장이 모두 바뀌고, 실패면 모두 바뀌지 않는다.
4. 품질 상품은 현재 보유품보다 좋아야 구매되며 실패 시 실물·품질 합계가 사라지지 않는다.
5. 시장 UI는 불변 snapshot만 읽고, 닫힌 최근 거래 탭의 행·문자열을 만들지 않는다.
6. schema 24는 최근 수급/bucket, authority revision, source watermark, 최근 거래와 다음
   sequence를 저장·hash·검증한다. schema 7~23은 결정론적으로 이전한다.

## 임시값과 다음 버전 재검토

- `PC-TEMP-MARKET-WINDOW-113`: 최근 수급 12개월.
- `PC-TEMP-MARKET-LEDGER-113`: 최근 실물 거래 100건.
- `PC-TEMP-MARKET-BOOK-BASE-113`: 공법서·법술서 도시 기준가 4영석.

세 값은 정식 Formula Registry와 장기 경제 soak 표본을 검토한 뒤 유지·조정한다.

## 검증 증거

- 구현 커밋: `507f582`, `93eef94`, `67a3833`.
- Windows x64 Release 전체 빌드: 경고 0, 오류 0.
- 자동 검사: `RESULT: 45/45 passed`.
- 10,000명: 초기화 32ms + 20,000 tick 859ms + 관찰 0.4ms = 891ms.
- checkpoint 116ms, schema 24 복원 349ms.
- 활성 UI 조회: 기준선 12.7ms → 9.8ms, 22.8% 감소.

로컬 패키지·바탕화면 바로가기·직접 실행 증거는 최종 고정 SHA 빌드 뒤 이 문서와
`CHANGELOG.md`에 추가한다. GitHub Release는 만들지 않는다.
