# ALPHA EXCHANGE · 알파 익스체인지

완전 오프라인 Android 주식 관찰 시뮬레이터입니다. [게임 안내](../../README.md) · [게임 목록과 개발 절차](../README.md).

- 앱 ID: `com.alphaexchange.offline`
- 엔진: `src/AlphaExchange.Core` (이 게임 전용)
- 앱: `src/AlphaExchange.Android`
- 검사: `tests/AlphaExchange.Checks`
- 빌드: `./build.sh alpha-exchange` → `dist/alpha-exchange/`
- [게임별 릴리스 검색](https://github.com/hddy774/project-hora/releases?q=alpha-exchange): 배포 시 `alpha-exchange/vX.Y.0` 태그와 `AlphaExchange-vX.Y.0.apk`를 확인합니다. 검색 링크는 공개 APK의 존재를 보장하지 않습니다.

현재 v1.11.0은 같은 앱 ID·배포키를 사용합니다. v1.10의 상태 9를 상태 10으로 이관하며 SQLite 6의 현금·주식·완료 시간·원본 이력을 보존합니다. `history-v5.sqlite`와 이전 JSON/ZIP은 별도 보관본으로 유지합니다. 설치·정확한 호환 범위는 [현행 릴리스 안내](../../docs/releases/v1.11.0.md)를 확인합니다.

기능별 계획은 `docs/plans`와 Draft PR에서 시작하고, 검증 후 게임별 태그와 APK를 배포합니다. 다음 변경에도 [개발 규격](../../docs/guides/development-standard.md)을 적용합니다.
