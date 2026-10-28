# 문서용 데이터 추출과 격리 실험 재현

기준 소스는 [문서 홈](../README.md)의 커밋이다. 추출은 게임 코드를 수정하지 않고 별도의 임시 .NET 10
콘솔에서 시뮬레이션 어셈블리의 공개 API와 XML 주석을 읽었다.

- [catalog.json](catalog.json): 상수·9경지·20공법·5시설·7직위·13지역·18호칭·성격 가중치·28모델·공개 enum.
- [save-example.json](save-example.json): 시드7·인구100·0년 세계의 실제 gzip JSON에서 뽑은 발췌.
- [격리 관측](../measurements/review-observations.json): 고등급 기연, 잘못된 지역 ID 저장, 관심 표시 대조군,
  겹친 사건 종료, 초기 원로 기록의 국소 재현.
- [추출기](evidence-repro.cs.txt), [프로젝트 설정](evidence-repro.csproj.txt): 재현용 소스 원문. 게임 빌드에 포함하지 않는다.

게임 디렉터리에서 다음처럼 별도 폴더에 복사한다. `XianxiaSourceDirectory`는 시뮬레이션 프로젝트 폴더다.

~~~sh
xianxia_export_dir=$(mktemp -d)
cp docs/reference/evidence-repro.cs.txt "$xianxia_export_dir/Program.cs"
cp docs/reference/evidence-repro.csproj.txt "$xianxia_export_dir/Export.csproj"
dotnet run --project "$xianxia_export_dir/Export.csproj" \
  -p:XianxiaSourceDirectory="$PWD/src/Xianxia.Simulation" -- \
  "$xianxia_export_dir/catalog.json" "$xianxia_export_dir/save-example.json" \
  "$xianxia_export_dir/review-observations.json"
~~~

이 실험은 생성한 임시 World만 다루며 플레이어의 world.sav를 열거나 바꾸지 않는다.
기연·사건 종료의 국소 반례는 private 메서드를 reflection으로 직접 호출한다.
이는 해당 연산의 동작을 확인하는 실험이며 정상 플레이에서의 발생 빈도를 측정한 것은 아니다.
관심 표시 실험은 같은 시드7·인구100의 세계 두 개를 600년 진행해 난수 상태와 핵심 인물 상태를 비교한다.
대조군은 관심0명, 실험군은 초기100명 모두 관심이다.

JSON 카탈로그는 문서용 스냅샷이며 런타임 설정이나 저장 마이그레이션 파일이 아니다.
공개 모델 설명은 원문 XML 주석에서 추출해, 주석 자체의 오류가 있을 수 있다. 계산식과 대조가 필요한
항목은 [검토 사항](../12-open-questions.md)에 별도로 적었다.
