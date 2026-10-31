# 리뷰 안내

생산 변경은 한 파일의 멤버를6개 partial로 옮긴 것이다. 로직 수정과 formatter 적용은 없다. 동일한 using/namespace/public sealed partial wrapper를 복사했고43개 멤버를 원래 상대 순서로 정확히 한 번 배치했다. `member-comparison.json`의 각 행은 원래/새 파일의 행 번호와 전체 멤버·본문·토큰SHA를 제공한다. 전체 멤버 원문까지 같아서 주석·공백·문자열도 그대로다.

| 파일 | 줄 | 멤버 |
| --- | ---: | ---: |
| World.PeopleSocialBridge.cs | 93 | 8 |
| World.PeopleSocialRequests.cs | 217 | 11 |
| World.PeopleSocialEvents.cs | 118 | 2 |
| World.PeopleSocialReputation.cs | 66 | 7 |
| World.PeopleSocialTraits.cs | 154 | 8 |
| World.PeopleSocialOaths.cs | 50 | 3 |
| World.PeopleSocialEcology.cs | 76 | 4 |

1. 원래 Bridge의 필드/record와 `SettlePeopleSocial`/`ValidatePeopleSocial`을 먼저 확인한다. 월별 호출·반복·전이 순서는 본문SHA가 같다.
2. Requests에서 동의 계획·명령·완료·취소·요청 관측/조회 수명을 본다. Events는 기존 사건 처리 전체와 출처 생성2개 멤버다. 전수·돌봄·입양 등 내부 분기를 임의로 나누지 않았다.
3. Reputation의 받은 진술/기억 예산, Traits의 경험·칭호·실제 권위, Oaths의 관측 영수증/종료, Ecology의 육신 참여 조건/서식지 비용을 각각 좁게 본다.
4. `verification.json`, 기존 기능 `people-functional.json`, Release 기록을 확인한다. 공개 API는 같고 새 메서드·필드·프로젝트 의존성은 없다.

기존 scripts에서 원래 파일명이나 이동 메서드를 단일 파일 텍스트로 읽는 의존은 찾지 못했다. 제품 경계 검사는 cs파일을 재귀 수집해 새 파일을 인식했고 기존 검사 자체를 수정하지 않았다. 생성 bin/obj·DLL·새 측정 데이터는 커밋하지 않았다.

## 멤버 대조 재현

SDK의 Roslyn을 직접 참조하는 일회성 검사기 원문을 `inspector/`에 보존했다. 제품 프로젝트/솔루션에는 참조하지 않는다. 이를 checkout 밖 scratch로 복사해 실행하면 공통 빌드 설정과 분리하여 같은 검사를 재현할 수 있다. baseline SHA를 고정 검사하고 C# 구문 오류, 멤버 추가/누락/중복, namespace/partial wrapper, 필드/constructor 이동, 원문/본문/토큰 차이는 실패한다.

```bash
mkdir -p /tmp/people-social-review
cp -R docs/tasks/people-social-bridge-split/inspector /tmp/people-social-review/
git show 404767ad4dc576ea3669cddd42529a50741cbf6f:src/Chronicles.Game/World.PeopleSocialBridge.cs > /tmp/people-social-review/baseline.cs
dotnet build /tmp/people-social-review/inspector/Inspector.csproj -c Release -m:1 -p:UseSharedCompilation=false
dotnet /tmp/people-social-review/inspector/bin/Release/net10.0/Inspector.dll verify /tmp/people-social-review/baseline.cs "$PWD" /tmp/people-social-review/comparison.json
```

추가 게임 실행 없이 AST/원문만 읽는 검사다. 기본 Git 리뷰에는 `git diff --color-moved=blocks 404767ad4dc576ea3669cddd42529a50741cbf6f...HEAD -- src/Chronicles.Game/World.PeopleSocial*.cs`도 사용할 수 있다. 이번 필터 검사는 기존 인물 domain/public·가족·전수·서약/요청 정보경계와 관련 캐시/정책 계약29개를 실제 실행했으며 전체 기능·플랫폼·성능 검증으로 확대하지 않는다.
