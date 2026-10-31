# 확장 실험 실행 진입점

게임 기능 마감·전체 기능 검증 뒤의 최종 측정에서만 명시적으로 실행한다.
기본 기능 검사는 샘플러를 실행하지 않는다. 한 프로세스에 측정 모드는 한 개만 지정한다.
각 실험·규모·재측정은 별도의 결과 파일과 깨끗한 소스 체크아웃을 사용한다.

```text
dotnet run --project tests/Chronicles.Game.Tests -c Release --no-build -- --filter INV-001/002/011 --execution-experiment EX-01 --execution-populations 2,100 --execution-repetitions 3 --execution-output <결과.json>
dotnet run --project tests/Chronicles.Game.Tests -c Release --no-build -- --filter INV-001/002/011 --execution-experiment EX-03 --execution-output <결과.json> --execution-source-fingerprint <실제제품SHA256> --execution-listings 32 --execution-repetitions 25 --execution-warmups 2
```

EX-01·02·04는 `ExecutionMeasurementRequest`에 인구 배열·반복·선택 ID를 전달한다.
EX-03·05·06은 실제 제품 지문과 호스트 설명, 출품 수·예열·반복을 명시한다.
생략한 호스트 이름은 현재 머신 이름이며 실기기 인증이 아니다. 결과의 선언 범위와 별도 출처 기록을 확인한다.
EX-05의 작업 폴더는 `--execution-workspace`로 지정할 수 있으며 기본값은 결과 폴더 아래 고유 하위 폴더다.

`--filter`를 쓰면 해당 기능 선택 결과만 기록된다. 이를 전체 기능 통과로 부르지 않는다.
부분 인구·선택 실험의 완료와 여섯 실험 전체·네 규모의 완료를 구분한다.
원시 표본·정답·hash·할당·환경·한계가 없는 준비 코드나 숫자를 수용 완료로 바꾸지 않는다.
