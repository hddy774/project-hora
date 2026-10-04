# 시장 축소 재현 도구

현재 체크아웃의 `AlphaExchange.Core`를 호출하는 .NET 9 콘솔 분석 도구다. 앱이나 시장 정책의 기본 동작을 수정하지 않는다. 고정 시드 5개와 수수료 0 비교 1개를 각각 36시즌 실행하고 시작/월말의 현금·실물 주식 보존을 검사한다.

저장소 루트에서 실행한다.

```bash
dotnet run --project tools/MarketAudit -c Release -- artifacts/market-audit
```

인수를 생략해도 현재 작업 디렉터리 아래 `artifacts/market-audit`에 기록한다. 출력 폴더는 분석 전용으로 사용한다. `monthly.csv`·`summary.json`을 생성하며 같은 이름 파일은 새 실행으로 교체한다. 앱 저장 파일을 읽거나 수정하지 않는다. 기준 v1.2.0 두 번 실행의 시나리오 합산 시간은 이 환경에서 약 80~97초였으며 빌드 시간과 Android 기기 성능 지표는 아니다.

기본 시드는 `20261004, 42, 17, 20260308, 314159`, 각 실행은 25,920시간이다. 수수료 0 비교는 첫 시드에서 초기와 매 월말 주문 취소 뒤 `FeeBasisPoints=0`을 설정한다. 다른 정책은 변경하지 않는다. 저장을 포함한 스트레스 검사는 아니며 완료 시즌 대기 목록은 측정 후 비워 메모리 증가를 제한한다.

기준 자료의 엔진은 main `e11579ac794f043c7b2cec10052746af9e5e6adf` / v1.2.0이다. [원자료](../../docs/analysis/v1.3.0/monthly.csv)와 [요약](../../docs/analysis/v1.3.0/summary.json)은 이 엔진으로 실행한 결과다. 현재 브랜치는 v1.3.0을 구현하여 기준 엔진과 다르다. `baselineReferenceCommit`은 비교 기준이며 도구가 과거 엔진을 자동 체크아웃한다는 뜻이 아니다. 현재 구현 결과는 `docs/analysis/v1.3.0-implementation/`에 별도 보관한다. v1.2.0을 재현하려면 원본 커밋을 별도 체크아웃하고 그 버전의 코어를 사용하는 분석 도구를 실행한다.

월말 검사 222회에 통과하면 종료 코드 0이며, 현금/주식 대조 실패는 예외와 실패 코드로 종료한다. 이는 전체 회계·매칭 테스트나 Android 저장/복구 테스트를 대신하지 않는다. CSV에는 금액·수량을 정수, 수익률을 0.1=10%인 비율로 기록한다. v1.3.0 `capitalization`은 평가 가격 × (발행 주식−자사주)이며 현재 당시의 주식 수를 사용한다. 기준 CSV는 기존 정의를 유지한다.

`reportedDividends`와 `cumulative_declared_dividends`는 기업 보고서에 표시된 금액이다. v1.2.0에서는 투자자에게 지급되지 않는다. `system_cash_difference`는 기존 투자자+은행+정부+수수료 계정만 대조하며 기업 영업 장부를 포함하지 않는다. 같은 시드라도 수수료 개입 뒤 난수 소비 경로가 달라질 수 있으므로 두 실행 차이를 정밀 인과 추정치로 취급하지 않는다.

그래프는 기록된 CSV에서 다음 명령으로 다시 생성한다. 분석용 Python 환경에 Matplotlib가 필요하며 앱에는 추가되지 않는다.

```bash
python3 tools/MarketAudit/plot.py docs/analysis/v1.3.0/monthly.csv artifacts/market-audit/market-decline.svg
```

문제 해석은 [조사 문서](../../docs/MARKET-AUDIT-v1.2.0.md), 구현 순서는 [개발 계획](../../docs/PLAN-v1.3.0-market.md)을 참고한다.

추가 인수: `출력폴더 시즌수(1~120) 선택적쉼표구분시드목록`. 현재 버전은 기업/실물 경제까지 포함한 현금 보존, 창업자/자사주를 포함한 실물 주식 보존을 대조한다. 가격/총수익 지수·순외부 유출입·실제 투자자 수취 배당 열도 기록한다. v1.3.0의 `reportedDividends` 누계는 기업의 실제 지급액이며 지급 계정과 수취 계정을 별도 대조한다.
