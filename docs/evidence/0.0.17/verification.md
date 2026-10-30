# 0.0.17 로컬 검증 증거

## 범위와 커밋

- Atlas 연결 후보 캐시: `dc68021`
- 개발자 세계 밸런스·저장 권위: `29f3917`
- 선택형 런타임 성능 계측: `b7bb953`
- 설정 작업공간·UI 배율·세계 생성: `dd9a859`
- 추적 문서와 아키텍처 manifest: `7a0053c`
- 버전 완료 고정 SHA: `de00f46941a672f454d61a0902ea5bb1fea4d989`
- GitHub Actions / GitHub Release: 실행하지 않음

## 자동 검증

고정 SHA에서 `scripts/build-local.ps1 -CreateDesktopShortcut -Launch`를 실행했다.

- Windows Release 전체 솔루션: 경고 0, 오류 0
- 전체 자동 검사: `RESULT: 52/52 passed`
- 아키텍처 계약: 8개 production module, Desktop authority 직접 접근 0
- 이식 원장: 238개 고유 행과 원본 SHA 대조 PASS
- 180년 SOAK: 사냥 참여 1,000명, 전투 경험 973명, 사냥 65,290회,
  전투 29,023회, 사망 1,493회
- 설정 권위: AI 수 100~50,000 검증, 개발자 배율 25~400% 검증, checkpoint/hash/복원 PASS

## 성능

- UI 선택 query 80회: 이전 이중 전체 조회 133.7ms → 선택 조회 12.1ms, 90.9% 감소
- 요약 query p95: 0.180ms / 16ms 예산 PASS
- 1,000행 materialization p95: 0.103ms / 50ms 예산 PASS
- 지도 닫힌 탭 allocation: 37.2% 감소
- 10,000명: 초기화 47ms + 20,000 tick 1,071ms + 관찰 0.4ms = 1,119ms
- 0.0.13 고정 기준 2,212ms 대비 49.4% 감소 / 30% 목표 PASS
- checkpoint 73ms, authority schema 25 복원 218ms

SQLite 5회 중앙값은 payload 5.82MiB, 저장 916ms, 복원 859ms로 각각 1초 예산을
통과했다. 저장 live managed peak는 기존 127.92MiB에서 SQLite 52.04MiB로 59.3%
줄어 30% 절감 예산을 통과했다.

## 직접 실행·화면 자동 관찰

- 정확히 한 개의 `선로만상록 PC v0.0.17` 창과 0.0.17 산출물 경로를 선택했다.
- 화면 `VERSION 0.0.17`, 10,000명, 년·월·일, 실시간 전투/랭킹 통계 갱신을 확인했다.
- 좌측 세계·인물·관계·지도·경제·조직·콘텐츠·기록·진단·설정 경로와
  `열린 경로만 갱신` 안내가 접근성 트리에 존재함을 확인했다.
- 100×에서 세계 시간이 수백 년까지 진행되고 년·월·일이 함께 바뀌는 것을 확인했다.
- 테스트 창은 산출물 전체 경로와 PID를 대조한 뒤 종료했으며 동일 실행본 프로세스가
  남지 않았음을 확인했다.

설정 내부 컨트롤의 사용자 입력 자동화는 Windows 전경 입력 보호가 반복되어 이 실행에서
완료 증거로 사용하지 않았다. AI 수·배율·저장 왕복은 자동 회귀로 검증했고, 다음 UI 구조
개편에서 내부 탭 재배치 뒤 화면 입력 검증을 다시 수행한다.

## 산출물

- 실행 파일: `artifacts/Chronicles-0.0.17-win-x64/Chronicles.exe`
- 크기: 140,914,015 bytes
- SHA-256: `89DB5AEA775C629C8C09BC198ED001F56917FA32F22B5D565C7922DC3892EBA2`
- 파일 버전: `0.0.17.0`
- 제품 버전: `0.0.17+de00f46941a672f454d61a0902ea5bb1fea4d989`
- 바탕화면 바로가기: `선로만상록 PC.lnk`
