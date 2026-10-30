# 원형 성장 9조건의 원문과 독립 검증

실측 소스는 `7ce66943b539420c9dc1e3fa8a611daae58020ff`, 제품 지문은
`4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304`다.
[실제 실행 41111524034](https://github.com/hddy774/project-hora/actions/runs/41111524034)의
9개 관측 작업과 공식 통합 작업이 모두 성공했다. 각 조건은 6000개월을 120개월 간격으로
관찰한 51표본이며 총 459표본이다. a/b/c × 시드 42/98/2026의 원형을 바꾸지 않았다.

9개 원본의 ZIP 지문, 각 21파일 원본 manifest, 실제 네 DLL의 PE 소스·Release·SHA256,
SDK 10.0.403/.NET 10.0.14, 실행 환경, 창세 선택검사 1/1, 종료 코드 0을 확인했다.
`final-verification.json`은 공식 inputArtifacts 9개 지문과 모든 원본 행의 일치도 기록한다.
기존 926 원본은 이 9개 결과에 합산하지 않았다. 94/4fe 제품 지문 528의 실행도 별도다.

- 각 `<조건>-<시드>/out/growth.json`은 내려받은 원문과 바이트가 같다.
- `official-aggregate/out/growth-aggregate.json`은 공식 절대 inputArtifacts 경로까지 그대로다.
  원문 SHA256은 `bd592d544766dfff2ba39ece2a531513610ccca3b0c24b86061e9d9b0626e0ab`다.
- `official-aggregate/provenance/aggregate.json`의 SHA256은
  `3750b5196220d793de701d11f8615bed8c7bb1d09893f475ece783c3f205a766`이다.
- `final-verification.json`의 SHA256은
  `c74f85bd5c1aab95d403498039c1c400cab658618478c834608903a0d59196a0`이다.
- 검증기 세 파일은 실제 scratch 실행본이다. v1을 보존했고 v2는 이미 확인한 원본의
  반복 해시를 피하며 새 원본만 검증했다. 원본 검증 때의 ZIP·DLL은 저장소에 넣지 않았다.
  아카이브 검증기를 이 축약 사본에서 실행했다고 주장하지 않는다.
- `pe-inspector-source`는 각 CI가 사용한 같은 PE 읽기 보조 코드의 한 사본이다.
  네 DLL 자체는 제외했고 각 실행의 `provenance/assemblies.json`을 보존했다.
- 원본 artifact의 21파일 manifest에는 제외된 DLL과 보조 파일도 기재돼 있다.
  이 디렉터리를 ZIP 전체 복사본으로 해석하면 안 된다. 실제 복사 범위와 원래 경로는
  `copied-file-manifest.json`, ZIP ID/지문은 `.artifact.json`과 `final-ci-metadata.json`에 있다.

9개의 서로 다른 GitHub 러너 이름, 실제 논리 CPU 4개, 가용 메모리와 쿼터·디스크를 기록했다.
물리 호스트의 열·전원은 측정하지 않았다. 서로 다른 VM의 elapsed 합계는 병렬 전체
wall 시간이나 같은 물리 CPU에서의 성능 비교가 아니다.

완료는 유한 관찰이 끝났다는 뜻이다. 부재·사망·목표 미달·잠정 법칙을 원문 그대로
유지했으며 균형, 무한 생존, 확률 추정, 장치 성능 또는 전체 릴리스 성공으로 바꾸지 않는다.
기본 a의 세 관찰창에서 후손 금단 부재와 c2026의 실제 후손 금단 기록도 원문 그대로다.
휴대 가능한 상대경로 파생 묶음은 별도 담당 작업이며 이 공식 집계를 고치지 않는다.
