# ALPHA EXCHANGE · 알파 익스체인지

완전 오프라인 Android 주식 관찰 시뮬레이터입니다. [게임 안내](../../README.md) · [게임 목록과 개발 절차](../README.md).

- 앱 ID: `com.alphaexchange.offline`
- 엔진: `src/AlphaExchange.Core` (이 게임 전용)
- 앱: `src/AlphaExchange.Android`
- 검사: `tests/AlphaExchange.Checks`
- 빌드: `./build.sh alpha-exchange` → `dist/alpha-exchange/`
- [게임별 릴리스 검색](https://github.com/hddy774/project-hora/releases?q=alpha-exchange): 배포 시 `alpha-exchange/vX.Y.0` 태그와 `AlphaExchange-vX.Y.0.apk`를 확인합니다. 검색 링크는 공개 APK의 존재를 보장하지 않습니다.

소스 폴더와 네임스페이스를 옮기지 않습니다. `history-v5.sqlite`, 기존 JSON/ZIP 이전 경로와 앱 저장 공간을 유지합니다. 앱 ID 보존은 저장 파일 호환성에 필요하지만, 이전의 버린 일회용 서명키 때문에 기존 APK에 새 APK를 덮어쓸 수 있다는 보장은 아닙니다. 삭제 전 전체 기록 ZIP 백업이 필요합니다.

새 기능 버전/릴리스 노트는 별도 배포 준비에서 작성합니다. 이 기반 PR에서는 앱 버전을 올리거나 새 태그/APK를 배포하지 않습니다. 기존 배포 정리와 서명키 준비는 별도 작업입니다.
