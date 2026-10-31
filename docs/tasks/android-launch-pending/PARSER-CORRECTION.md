# 독립 리뷰의 추가 행 파서 보완

최종 제품 입력은 `2464337ec577b2894eb87468c2c72916b1d6b8bf`, 제품 지문은
`0eaf05f4c00e79ac1f485a50ac4d0e6eec30f82dce49a2012e1aa0cf716bfdc2`다.
앞선 `verification.json`·검사 로그·SHA manifest는 초기1cf/지문72e6의 기록으로 그대로 보존한다.
현재 소스의 결과는 [parser-correction.json](parser-correction.json)을 읽는다.

Cultivation 담당의 독립 리뷰에서 한 단어 형식만 세던 정규식이 정상 timeout 행 뒤의 빈 `Status:`나
`Status: unknown extra`를 무시하는 공백을 발견했다. Activity도 같은 문제가 있었다.
추가4개 입력을 기존 parser 검사에 넣자 [수정 전 로그](checks/parser-correction.initial-failure.log)에
4개 하위 사례 실패가 재현됐다. 이 probe의 shell 종료 코드는 별도로 수집하지 않았고 실패 원문을 보존한다.

모든 `Status:`·`Activity:` 행을 먼저 세고, 유일한 행의 전체 값을 공백 제거 후 검증하도록 보완했다.
실제196바이트 fixture의 결과는 같고, 빈/여러 단어의 추가 행도 중복으로 거부한다.
1cf 대비 helper AST에서 바뀐 정의는 `check_launch`뿐이며 Reader·단일90초·시작1회·관측 반복·
최종 PID/전경/WebView/DOM/bridge/PNG·ANR/충돌 기준은 그대로다.

정확한 원격246 clean checkout에서 CPU3 Python24/24를 확인했다.
[최종 검사 로그](checks/parser-correction.frozen-tests.log), 원격3파일 재조회·소스/tree/파일 SHA와
plan/progress 자체 검사는 최종 JSON에 있다. 기존15개 Name·14개 본문과 명시한 정책 하위 사례 변경은 같다.
새 native/수동 CI/Game/성능·장기 실행0이며, 루트의 새 지문 전체323·32지표·120개월 수용은 별도다.

이 수정은 초기1cf 이후 선택 입력 변경이다. 루트가 진행 중인1cf 전체 기능 자식을 중단하고 이유와
원문을 보존한다고 알렸으며, 그 결과를246에 승격하지 않는다. PR의 자동 CI도 각 실제 source로 구분한다.
