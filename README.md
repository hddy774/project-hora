# ALPHA EXCHANGE · 알파 익스체인지

기관 100곳과 개인 투자자 10,000명이 5업종·15종목에서 경쟁하는 완전 오프라인 Android 경제 관찰 게임입니다.

[최신 정식 APK 다운로드](https://github.com/hddy774/project-hora/releases/latest)

Android 8.0 이상, ARM64/x86_64를 지원합니다. `AlphaExchange-vX.Y.0.apk`를 기존 앱 위에 설치하면 저장된 시장을 이어갈 수 있습니다. 업데이트 전에 앱의 기록 내보내기로 백업할 수 있습니다. 서버·계정·인터넷 권한이 필요하지 않습니다.

이 배포 브랜치는 APK 배포와 설치·변경 안내, 배포 서명 자동화만 보관합니다. 게임 소스·개발 리소스·전체 기획·설계·검증과 이전 Git 이력은 소유자의 비공개 Notion 보관본에서 관리합니다. 이 브랜치의 릴리스 태그로 GitHub가 자동 생성하는 Source code ZIP/TAR에는 게임 소스가 포함되지 않습니다.

앱 ID: `com.alphaexchange.offline`

업데이트 인증서 SHA-256: `0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a`

개인키와 비밀번호는 공개하지 않습니다. 배포 자동화는 기존 `ANDROID_SIGNING_BUNDLE` Secret을 사용하고 인증서를 대조합니다. 테스트 서명 APK와 서명되지 않은 APK는 정식 릴리스로 배포하지 않습니다.

배포 관리자는 `ANDROID_SIGNING_BUNDLE`에 기존 `ANDROID_SIGNING_BUNDLE.json`의 전체 내용을 등록할 수 있습니다. 이 Secret에 PKCS12 키 파일의 Base64 값을 등록한 경우에는 기존 키의 `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` Secret도 함께 필요합니다. 두 입력 방식 모두 실제 인증서와 최종 APK 서명자를 위 지문과 대조합니다. 비밀번호는 릴리스·문서·채팅에 기재하지 않습니다.

릴리스별 변경 내용·파일 SHA-256은 [Releases](https://github.com/hddy774/project-hora/releases)에서 확인하세요. 사용된 오픈소스의 고지는 APK에 포함됩니다.
