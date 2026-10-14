# ALPHA EXCHANGE · 알파 익스체인지

완전 오프라인 C# Android 경제 관찰 게임입니다. 100개 기관과 개미 10,000명이 5개 업종의 15개 가상 종목에서 경쟁합니다. 플레이어는 투자자·시장·회계를 관찰합니다.

v1.11.0은 일일 ±30% 가격 제한, 기관 초기 자산 100억, 주식 50% 이상 배분, 매 시간 분산 호가 경쟁과 회사별 뉴스를 추가합니다. [변경·설치 안내](docs/releases/v1.11.0.md), [작업 계획](docs/plans/v1.11.0.md)을 제공합니다. 기존 앱과 같은 서명키를 사용하며 v1.10 시장의 현금·주식·시간·이력을 유지합니다. 이전 30종목 시장은 원본 그대로 보관·내보낼 수 있습니다.

## 게임

- 기관은 15종목의 목표 비중·현금·손절·익절·비용·업종 한도를 함께 계획합니다. 개미는 시장·가격·공개 뉴스의 방향을 따릅니다.
- 기관은 매집·추격 유도·분산 매도 전략과 경쟁 호가로 수익을 노립니다. 실제 자금·주식으로 가격/시간 우선 체결하며, 손실과 미체결도 발생합니다.
- 100명의 투자자 프로필을 제공합니다. 경영자·정치인·금융인·직원, 선거·투표·고용·사업 프로젝트와 은행 자동 호가는 없습니다.
- 기업 실적·금리·정책은 무작위 사건으로 변합니다. 배당·대출·세금·기업행동·파산은 실제 계정 간 자금 이동과 장부로 기록합니다.
- 만·억·조/정확한 숫자를 전환하고 금액 상세를 확인합니다. 원형·막대·추이·표를 갖춘 8종 통계 탭, 7종 기업 재무·분석 탭, 기관·개미의 6종 재무 보고를 제공합니다.
- 1시간은 기본 5초이며 최대 100배속을 선택할 수 있습니다. 30일 시즌이 이어지고 실제 처리량은 기기 성능에 따라 다릅니다.
- 완료한 시간별 기록은 SQLite에 보관하고 ZIP으로 내보내기/복원합니다. 앱을 벗어나면 저장하고 멈춥니다. 서버·계정·인터넷 권한이 필요하지 않습니다.

Android 8.0 이상 ARM64와 x86_64를 지원합니다. APK 이름은 `AlphaExchange-vX.Y.0.apk`입니다. 배포 개인키와 비밀번호는 사용자가 별도로 보관하며 소스·공개 릴리스에 넣지 않습니다.

## 개발

유지보수·확장성과 Android 실행을 우선하는 [개발 규격](docs/guides/development-standard.md)을 모든 새 작업에 적용합니다. [코드 구조와 실행 경계](docs/guides/architecture.md), [v1.11.0 계획](docs/plans/v1.11.0.md), [문서 안내](docs/README.md)를 먼저 읽으세요.

| 위치 | 책임 |
|---|---|
| `src/AlphaExchange.Core` | 도메인·시뮬레이션·보고·저장·규칙 |
| `src/AlphaExchange.Client` | 공통 생명주기·저장 큐·표시 버퍼·캐시 |
| `src/AlphaExchange.Android` | Activity·입력·화면 이동·Canvas·기능별 화면 |
| `tests/AlphaExchange.Checks` | 회계·시장·저장·모바일 회귀 검사 |
| `tools`, `scripts` | 측정·배포·자동 구조 검사 |
| `docs` | 현행 가이드·계획·릴리스, 과거 이력과 원자료 |

.NET SDK 9.0.318, Python 3.10 이상, JDK 17과 Android SDK 35를 사용합니다.

```sh
python3 scripts/check-architecture.py
python3 scripts/test_architecture.py
python3 scripts/games.py validate
python3 scripts/games.py check alpha-exchange
python3 scripts/games.py check alpha-exchange -- --client-only
python3 scripts/games.py check alpha-exchange -- --storage-load
./build.sh alpha-exchange
```

Android 빌드는 `ANDROID_HOME`과 `JAVA_HOME`을 설정합니다. 결과는 `dist/alpha-exchange/`에 생성되며 빌드 파일은 커밋하지 않습니다. [서명 설정](docs/guides/android-signing.md)을 사용해 같은 배포키로 서명합니다. [게임 목록과 독립 배포](games/README.md)는 여러 게임의 등록·검사·배포 규칙을 설명합니다.

[경제 규칙](src/AlphaExchange.Core/Rules/README.md), [이전 설계·조사](docs/history), [분석 원자료](docs/analysis), [성능 원자료](docs/performance), [Android 검증 원자료](docs/verification)를 보관합니다.
