# 지분/성능 릴리스 측정

```sh
dotnet run --project tools/PerformanceAudit -c Release -- baseline /tmp/baseline.json
dotnet run --project tools/PerformanceAudit -c Release -- optimized /tmp/optimized.json
```

같은 Linux/.NET Release 환경, 시드 20261004, 168시간 진행한 30개 회사/100개 기관/10,000명 개인으로 재무 집계·저장 준비·직렬화·쓰기 시간과 현재 스레드 할당량을 측정한다. 워밍업/강제 수집 후 9회(쓰기/범위/누락 검사는 5회)의 중앙값·최대값과 시간 원본 표본을 JSON으로 기록한다. 캐시 재조회가 아닌 실제 집계 비용을 재기 위해 측정 시 `cachedStats`만 비운다.

720시간 시뮬레이션 시간과 실제 매칭 수를 별도로 기록한다. 장기 조회 데이터는 경제 시뮬레이션과 구분한 합성 5년(360일/년)의 시간별 원기록 43,201행이다. 720행씩 원자적으로 저장하고 500점 이하의 범위 조회·경계·누락 검사와 원본 파일 크기를 확인한다. 실행 종료 시 임시 DB를 지운다.

`prepareSave`는 저장용 상태 분리 비용이며 실제 JSON 인코딩/디스크 완료 시간이 아니다. `writeSnapshot`에는 상태 준비·인코딩·압축·검증·트랜잭션을 포함한다. 현재 스레드 할당량은 프로세스의 최대/상주 RAM이 아니다. 운영체제 스케줄링, GC와 JIT 때문에 시간에 변동이 있으므로 모든 지표와 표본을 함께 기록한다. 물리 Android FPS/메모리 성능으로 해석하지 않는다.
