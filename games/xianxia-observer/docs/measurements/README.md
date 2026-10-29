# 측정 조건과 재현

이 디렉터리는 측정 원문을 보관한다. 측정치가 게임의 목표값이나 출시 보증을 뜻하지 않는다.

## C# 장기 실행

- 기준 소스: `17b48ad1bfcb58a9d32a8def0e74caa2027cb7f0`.
- 실행일: 2026-10-28 UTC, 문서 작성 중 실행.
- 환경: Linux 클라우드 작업 컨테이너, .NET SDK 10.0.403 / net10.0 Release 도구.
- 입력: 시드 1·2·3, 각 인구 1,000, 30,000년, 별도 개입·관심 변경 없음.
- 원문: [csharp-30k-seeds-1-2-3.txt](csharp-30k-seeds-1-2-3.txt).

게임 폴더 `games/xianxia-observer/`에서:

```sh
dotnet build tools/Xianxia.Tools/Xianxia.Tools.csproj -c Release
XIANXIA_GROWTH=1 dotnet tools/Xianxia.Tools/bin/Release/net10.0/Xianxia.Tools.dll report --seeds 1,2,3 --years 30000 --population 1000
```

`XIANXIA_GROWTH=1`은 성장 요인 진단 누계를 켠다. 원문 속 경과 시간·평균은 도구 실행의 값이며
Android UI 포함 프레임·잠금 지연·저장 지연·전력·발열 벤치마크가 아니다. 하드웨어 격리·워밍업·
반복 통계·신뢰구간을 통제하지 않았으므로 다른 기기의 성능 보장이나 정밀 회귀 임계값으로 쓰지 않는다.
보고서의 소수 표시에는 반올림이 있고 원문 마지막 성장 요인 누계는 세 실행의 누적 진단이다.

각 시드의 경지 인구 합 1,000, 장부 예상=실제, 전쟁·대회·성장·좌화 발생을 확인했다.
전체 요약과 해석은 [검증 기록](../08-validation.md)에 있다. 같은 명령의 재실행은 시간 값이 달라질 수 있다.

## 격리 반례 실험

[review-observations.json](review-observations.json)은 문서용 임시 콘솔 프로그램에서 얻은 다섯 관측이다.
생성·공개 API·필요한 private 메서드의 반사 호출을 사용했으며 게임 소스·사용자 저장 파일을 변경하지 않았다.
private 호출은 한 연산의 경계 조건을 재현할 뿐 자연 세계에서 그 상태가 나타날 빈도를 증명하지 않는다.

재현 프로그램·실행법은 [추출 안내](../reference/extraction.md), 결과별 분석은
[문제·미결정 사항](../12-open-questions.md)을 본다. 모든 변경 제안은 미승인 상태다.

## 기존 React 측정

[legacy-30k-seeds-1-2-3.json](legacy-30k-seeds-1-2-3.json)은 이전 작업에 보존된 React 원본 측정이다.
[analyze-legacy.mjs](../../tools/analyze-legacy.mjs)가 원본을 헤드리스 실행한다.
원본과 C#은 RNG·규칙·상태·실행 환경이 다르므로 같은 시드 번호가 같은 세계를 뜻하지 않는다.
현재 C# 측정과 짝을 맞춘 동일 장비 반복 실험이 아니므로 수치상의 속도 개선 배수를 주장하지 않는다.
원본의 발견 사항과 현재 구현의 변경은 [11](../11-legacy-and-decisions.md)에서 구분한다.
