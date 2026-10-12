# v1.9.0 검증 원자료

앱 소스 커밋은 `5ff7cb19a2668fdc563f79e62dd13c331bbad0f7`이다. [최종 소스 CI](https://github.com/hddy774/project-hora/actions/runs/38242845751)에서 카탈로그·전체 검사·Android 빌드와 실제 API26 x86_64 실행이 모두 성공했다. 이후 변경은 검증·성능·설치 문서와 원자료이며 앱 소스에는 변경이 없다.

- [전체 검사](ci-full-checks.txt): 41,252,590개 주장,16시즌,5,058,975체결. 현금/주식/담보/부채/배당/세금·세대·순위·원본 이력·복원 검증.
- [공통 모바일·가격평가·순위 검사](client-checks.txt): 5,467개 주장. 차트0/1/상수/긴 입력/NaN/버퍼 재사용, 화면 캐시, 비동기 쓰기 합치기·실패 재시도, 소수점 평가·모든 순위 지표/기간/세대.
- [합성5년 저장](storage-load.txt):43,201행, 보관·복구·조회·파일 내보내기. 경제 시뮬레이션 성능과 구분한다.
- [네이티브 결과](native-smoke.json), [생명주기](lifecycle.json), [480/600/800 모달](modal-viewports.json), [새 시장 전환](new-market-transition.json): 구버전177행 원본 체크섬·기존Run 보존, 일시정지·Activity재생성·확대·모달스크롤·100배속 대기0, 기존 체크포인트 보존 뒤 신규Run 생성.
- [Android PSS 샘플](memory-samples.json)과 `*-meminfo.txt`: 단일 에뮬레이터 관측이며 물리 기기나 기준 APK와의 전후 메모리 비교가 아니다. [화면 샘플](screenshots)을 포함한다.
- [서명·패키지](release-apk.json), [APK 서명 검사](apk-signature.txt), [공개 인증서](public-signing-certificate.pem): v1.8과 같은 인증서, 앱 ID·코드10·버전1.9.0·ARM64/x86_64·최소26/대상34·디버그/인터넷 권한 없음. 개인키는 포함하지 않는다.

APK는 위 소스에서 직접 Release 빌드하고 기존 사용자 배포키로 서명했다. [공개 릴리스 검증](public-release.json)과 [업로드 실행](https://github.com/hddy774/project-hora/actions/runs/38244842226)에서 공개 APK 1개·실제 재다운로드 해시 일치를 확인했다. 임시 업로드 브랜치는 삭제했다. 물리 ARM64 기기·배터리·FPS 검증과 일부 첫 이력 조회의 비동기 전환은 남은 한계다. 기존 targetSdk34와 Android의 status/navigation bar·back API 관련 경고는 이전 버전에서 이어진 것으로 이번 작업에서 목표 SDK를 올리지 않았다.
