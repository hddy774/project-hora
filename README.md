# ALPHA EXCHANGE · 알파 익스체인지

완전 오프라인 C# Android 경제 관찰 게임입니다. 100개 투자사와 1,000→10,000개 개미 집단이 6개 분야의 30개 가상 회사에 투자합니다. 플레이어는 시장·200명의 인물·기업·정부·은행을 관찰합니다.

v1.9.0은 유지보수 구조와 Android 실행 비용을 개선합니다. Core·공통 Client·Android의 책임을 구분하고 차트·순위·통계·이미지 비용을 줄이며 새 시장과 저장 준비를 백그라운드로 옮깁니다. [APK·설치 안내](docs/releases/v1.9.0.md), [성능 측정과 한계](docs/performance/v1.9.0/README.md)를 제공합니다. v1.8.0과 같은 배포키를 사용해 설치 업데이트할 수 있습니다.

## 게임

- 200명의 투자자·경영자·정치인·금융인과 서로 다른 프로필 그림, 직무별 능력과 개인 지갑을 제공합니다.
- 실제 가격/시간 우선 호가 체결과 기관 AI의 기간·가격·현금·위험 계획을 사용합니다. 플레이어가 직접 매매하지 않습니다.
- 기업의 사업·임금·배당·설비·대출·세금과 정부·은행의 예산을 계정 간 현금 이동으로 반영합니다.
- 분할·병합·합병·IPO·증자·자사주·주주 의결·파산·세대 교체와 과거 기록을 보관합니다.
- 시장·자산·랭킹·인물·통계·시즌·뉴스 화면에서 재무제표·지분·선거·정책·사업 과정을 관찰합니다.
- 1시간은 기본 5초이며 최대 100배속을 선택할 수 있습니다. 30일 시즌이 이어집니다. 실제 처리량은 기기 성능에 따라 다릅니다.
- 완료한 시간별 기록은 SQLite에 보관하고 ZIP으로 내보내기/복원합니다. 앱을 벗어나면 저장하고 멈춥니다. 서버·계정·인터넷 권한이 필요하지 않습니다.

Android 8.0 이상 ARM64와 x86_64를 지원합니다. APK 이름은 `AlphaExchange-vX.Y.0.apk`입니다. 배포 개인키와 비밀번호는 사용자가 별도로 보관하며 소스·공개 릴리스에 넣지 않습니다.

## 개발

유지보수·확장성과 Android 실행을 우선하는 [개발 규격](docs/guides/development-standard.md)을 모든 새 작업에 적용합니다. [코드 구조와 실행 경계](docs/guides/architecture.md), [v1.9.0 계획](docs/plans/v1.9.0.md), [문서 안내](docs/README.md)를 먼저 읽으세요.

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
