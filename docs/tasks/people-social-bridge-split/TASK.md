# 인물 사회 브리지의 기능별 분리

- 추적: [#225](https://github.com/hddy774/project-hora/issues/225)
- 기준: `404767ad4dc576ea3669cddd42529a50741cbf6f`
- 목표: 57,065바이트·720줄의 한 파일을 실제 책임별로 나눠 리뷰할 부분을 좁힌다.
- 소유: 원래 `src/Chronicles.Game/World.PeopleSocialBridge.cs`와 새 `World.PeopleSocial{Requests,Events,Reputation,Traits,Oaths,Ecology}.cs`, 이 작업 문서만.
- 변경 제외: 공개 API/namespace/서명, 메서드 본문·조건·산술·RNG·전이 순서, 새 기능/추상화/최적화/테스트, 공유 validator·기획 원장·다른 담당 파일.

원래 const와 중첩 record2개, 생성 입력 연결, 공통 Social 상태 갱신, 초기 baseline, 월별 driver, 검증은 원래 파일에 둔다. 새 partial은 기존 메서드만 가지며 필드/초기화자/constructor를 추가·이동하지 않는다. 교육/돌봄/입양 분기는 사건 처리·월별 driver의 기존 본문 안에 있으므로 새 helper로 추출하지 않는다.

완료 조건은43멤버(메서드40개)의 원문·본문·토큰·주석/공백 일치, 관련 기존 기능 검사29/29, Release 빌드 경고/오류0, 제품 경계/기획 원장 정적 검사와 diff whitespace 검사 통과다. 실제 명령·원본 결과·파일SHA는 이 폴더에 보존한다. 빌드/선택 검사는CPU2에 격리하고 `-m:1`/`UseSharedCompilation=false`를 사용했다. `T04 ... saved People policy`라는 기존 기능 사례도 filter에 포함되지만 측정 옵션을 실행하지 않았다.

검사 대상은 기준 커밋에 기록된7개 소스 파일 변경을 적용한 worktree 후보이며 깨끗한404767 기준 자체를 검사했다고 주장하지 않는다. 테스트 후 생산 소스는 바꾸지 않았다. FP528·기존 네이티브/성능 증거는 과거 소스에 그대로 두고 새 통합 소스의 전체 정확성/성능 검증은 루트가 별도로 수행한다. 이번 작업의 성능·성장·장기 실행은0이다.
