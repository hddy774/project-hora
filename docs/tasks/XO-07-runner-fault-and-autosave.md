# [XO-07] 선협 관찰 — 실행기 오류 격리·정지 중 자동 저장·저장 내구성

상태: 승인(사용자 전체 개발 지시, 2026-10-29) · 우선순위 P1 · 선행: XO-03 병합 · 검토 근거: [13 §2 R-12~R-15](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
1. `WorldSimulator.Step` 중 예외가 나면 실행기는 정지만 하고, 반쯤 처리된 해를 다시 진행하거나 저장할 수 있다(장부 불일치 등 영구 손상).
2. 정지 중에는 `Monitor.Wait`로 잠들어, 관심·개입을 바꾼 뒤에도 90초 자동 저장이 돌지 않는다.
3. 저장 파일 쓰기에 flush가 없고 직전 정상본이 남지 않는다.
4. `Stop` 뒤 `Start`가 즉시 끝나는 재사용 함정이 있다.

## 범위
- 포함: `Xianxia.App/Services/SimulationRunner.cs`, `GameSession.cs`, `SaveStore.cs`, `Sheets/SettingsSheet.cs`(문구·버튼 상태),
  `Views/HeaderBar.cs`(진행 버튼 비활성), 테스트 `tests/Xianxia.App.Tests/RunnerFaultTests.cs`(신규).
- 제외: Step 단위 트랜잭션·스냅샷 롤백(메모리 비용 때문), 다중 슬롯, 코어 규칙 변경.

## 인터페이스 (계약)
```csharp
// SimulationRunner
/// <summary>진행 중 오류로 이 세계를 더 진행·저장할 수 없는 상태인가. Replace로 새 세계를 받으면 false가 된다.</summary>
internal bool IsFaulted { get; }

/// <summary>테스트 전용: 다음 Step 직전에 호출된다. null이면 아무것도 하지 않는다.</summary>
internal Action<World>? BeforeStepForTest { get; set; }

// SaveStore
internal string BackupPath { get; }   // world.sav.bak
```

## 동작 규칙
1. Step 예외(OOM 제외) 시 `IsFaulted = true`, `_running = false`, `Error` 설정(기존 문구 유지). 버전은 올린다(화면 갱신).
2. `IsFaulted`인 동안: `SetRunning(true)`는 무시, 자동 저장 콜백을 호출하지 않음, `GameSession.Save()`는 파일을 쓰지 않고
   false와 문구 `"오류가 난 세계는 저장하지 않습니다. 마지막 저장을 불러오거나 새 세계를 시작하세요."`를 남긴다.
   백그라운드·종료 저장도 같은 경로라 쓰지 않는다.
3. `Replace(world)`는 `IsFaulted = false`, `Error = null`로 되돌린다(불러오기·새 세계).
4. 설정 시트: `IsFaulted`이면 상단에 2의 문구, "저장" 버튼 비활성. 헤더 진행 버튼 비활성.
5. 정지 대기: `Monitor.Wait(_gate, TimeSpan.FromSeconds(1))`로 바꾸고, 깨어날 때마다(잠금 밖에서) 자동 저장 조건
   (`경과 ≥ 간격 && Version != savedVersion && !IsFaulted`)을 검사한다. 진행 중 루프의 기존 검사는 그대로.
6. `Start()`: `_stopping`이 true면 `InvalidOperationException("실행기는 한 번만 시작할 수 있습니다.")`. 문서 주석에 일회 수명을 적는다.
7. `SaveStore.Write`: 임시 파일을 `FileStream`으로 쓰고 `Flush(flushToDisk: true)` → 기존 `world.sav`가 있으면 `world.sav.bak`으로
   `File.Copy(overwrite: true)` → `File.Move(tmp, world.sav, overwrite: true)`.
8. `TryLoad`: `world.sav`가 SaveFormatException으로 격리되고 `.bak`이 있으면 `.bak`을 같은 검증으로 로드한다.
   성공하면 문제 문구 끝에 `" 직전 저장으로 복구했습니다."`를 붙이고 그 세계를 돌려준다. `.bak`도 실패하면 기존처럼 새 세계.
   `.bak`은 지우지 않는다.

## 수용 기준 (Acceptance Criteria)
- [x] `BeforeStepForTest`로 5번째 Step에서 예외를 던지면 `IsFaulted`, 이후 `SetRunning(true)` 후 1초 동안 연도 불변, 저장 파일 미변경.
- [x] 오류 후 `LoadSaved`로 정상 세계를 받으면 `IsFaulted == false`이고 진행된다.
- [x] 정지 상태에서 개입 → 자동 저장 간격(테스트는 200ms로 주입) + 1.5초 안에 저장 콜백이 1회 호출된다. 버전이 그대로면 호출되지 않는다.
- [x] `Stop()` 뒤 `Start()`가 InvalidOperationException.
- [x] 저장 2회 후 `.bak`이 첫 저장과 같은 바이트다.
- [x] `world.sav`를 무작위 바이트로 바꾸면 `TryLoad`가 `.bak`에서 복구하고 손상본은 XO-03 규칙으로 격리된다.
- [x] 헤드리스 UI: 오류 상태에서 설정 시트 저장 버튼 비활성, 문구 표시(`CompletedUiTests`에 1건 추가).
- [x] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과.

## 테스트 케이스
- `Fault_StopsAndBlocksResume`
- `Fault_BlocksAllSaves`
- `Replace_ClearsFault`
- `Paused_AutosavesAfterChange`
- `Paused_DoesNotAutosaveWithoutChange`
- `Start_AfterStop_Throws`
- `Write_KeepsPreviousAsBackup`
- `TryLoad_RecoversFromBackup`
- `SettingsSheet_DisablesSaveWhenFaulted`

## 참고
- 코드: `SimulationRunner.cs:74~98·185~275`, `SaveStore.cs:48~100`, `GameSession.cs:40~115`.
- 저장 형식·난수 영향 없음. Android `allowBackup=true`는 `.bak`도 포함하지만 변경하지 않는다.

완료 근거(2026-10-29): 대표 PR #32의 상태 원본과 연결된 서브 PR/CI, 현재 구현 문서20을 따른다. Android 실제 입력·출시는 별도 XO-09/UI-QA/RELEASE-02에서 완료 전까지 추적한다.
