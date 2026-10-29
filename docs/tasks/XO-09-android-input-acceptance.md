# [XO-09] 선협 관찰 — Android 실입력 수용 검사

상태: 승인 대기 · 우선순위 P1(검증) · 선행 결정: D-07(지원 범위) · 검토 근거: [13 §2 R-19·R-18](../../games/xianxia-observer/docs/13-claude-review.md)

## 배경 (왜)
v0.1.0 에뮬레이터 검사에서 Pixel Launcher ANR 창이 탭 입력을 가렸다. 지금 스크립트는 좌표를 누르고 PNG를 남길 뿐,
**탭이 실제로 바뀌었는지 판정하지 않는다.** ARM64 실기기와 최소 API 24도 실행하지 않았다.

## 범위
- 포함
  - 앱이 탭·시트 전환을 한 줄 로그로 남기는 기능(Release 포함, 개인 정보 없음).
  - `games/xianxia-observer/tools/emulator-smoke.sh`: 로그로 판정.
  - `.github/workflows/xianxia-observer.yml`: 에뮬레이터 job을 API 34·API 24 두 개로(행렬).
  - 새 문서 `games/xianxia-observer/docs/device-checklist.md`: 오너가 ARM64 실기기에서 수행할 수동 점검표.
- 제외: 접근성·글자 배율 품질 기준(별도 결정), 성능·발열 측정, 스토어 배포.

## 인터페이스 (계약)
```csharp
// Xianxia.App/Services/UiTrace.cs (신규)
/// <summary>탭·시트 전환을 플랫폼 로그에 한 줄로 남긴다. 기본 구현은 Console.WriteLine.</summary>
internal static class UiTrace
{
    /// <summary>Android 호스트가 교체할 수 있는 출력. null이면 Console.WriteLine.</summary>
    internal static Action<string>? Sink { get; set; }
    internal static void Tab(int index, string name);      // "[xo-ui] tab {index} {name}"
    internal static void Sheet(string kind, int id);       // "[xo-ui] sheet {kind} {id}"
    internal static void SheetClosed(string kind);         // "[xo-ui] close {kind}"
}
```
- Android 호스트(`MainActivity.cs`)는 `UiTrace.Sink = line => Android.Util.Log.Info("XianxiaUi", line)`로 설정한다.
- 호출 위치: 탭이 실제로 바뀐 직후(`MainView`), 시트를 연 직후·닫은 직후(`SheetHost`).

## 동작 규칙
1. 로그는 탭·시트 전환 때만 남긴다. 연간 진행·렌더 루프에서는 남기지 않는다.
2. 스모크 스크립트 순서: 설치 → 실행 → 런처 강제 종료 → **5초 대기 후 `dumpsys window`에서 포커스 창이 이 패키지인지 확인**(아니면 실패) →
   7개 탭을 차례로 탭 → 각 탭 직후 2초 안에 logcat에 `"[xo-ui] tab {i}"`가 있어야 함 → 인물 탭에서 첫 행 탭 → `sheet Cultivator` 확인 →
   뒤로 가기 → `close Cultivator` 확인 → 백그라운드·복귀 → 재실행.
3. 판정 실패 시 스크립트는 종료 코드 1과 실패 단계 이름을 출력한다. 기존 FATAL/ANR 검사는 유지한다.
4. 탭 좌표는 기존 식(`width × (2i+1)/14`, `height × 90%`)을 유지하되, 실패 시 스크린샷 파일명을 로그에 남긴다.
5. API 24 job은 `google_apis` x86_64 이미지. 이미지 부팅 실패는 job 실패로 둔다(건너뛰지 않는다).
6. `device-checklist.md` 항목(오너 수행, 각 항목 통과/실패/메모 칸): 설치·업데이트(v0.1.0 위에 설치해 저장 유지), 7탭 전환, 인물 검색 중 키보드가 입력창을 가리지 않음,
   시스템 글자 크기 최대에서 헤더·탭 글자 잘림, 제스처 내비게이션에서 하단 탭 터치, 노치·상단 여백, 뒤로 가기 순서(시트→천하→종료),
   천기 개입 1회, 수동 저장 후 강제 종료·재실행, 30분 연속 진행 후 발열·크래시 여부, 기기 모델·Android 버전·ABI 기록.
7. 선택(R-18): 같은 시드 7·인구 300·1,000년 세계를 ARM64 기기와 x86_64 에뮬레이터에서 저장해 해시를 비교하는 방법을 checklist 끝에 적는다(앱 설정 화면에 저장 SHA-256 표시는 **추가하지 않는다**; adb로 파일을 꺼내 비교).

## 수용 기준 (Acceptance Criteria)
- [ ] API 34·API 24 두 job 모두 7개 탭 로그·시트 열기/닫기 로그를 확인하고 성공.
- [ ] 런처 창이 포커스를 가진 상황(스크립트에서 런처를 다시 띄워 재현)에서는 스크립트가 **실패**한다(음성 대조, PR에 로그 첨부).
- [ ] UI 로그가 연간 진행 중 1초에 1줄 넘게 찍히지 않는다(60초 진행 logcat에서 `[xo-ui]` 0줄).
- [ ] `device-checklist.md`가 존재하고 6번 항목을 모두 포함한다. 실기기 결과는 오너가 채우며 이 PR의 완료 조건이 아니다.
- [ ] 포맷·코드 규칙·Release 빌드 경고 0·전체 테스트 통과, actionlint 통과.

## 테스트 케이스
- `UiTrace_WritesTabLine` (Sink 주입, App 테스트)
- `UiTrace_WritesSheetOpenAndClose`
- CI: `emulator (api 34)`, `emulator (api 24)`

## 참고
- 현재 스크립트: `games/xianxia-observer/tools/emulator-smoke.sh`(75줄), 워크플로 237~250줄 부근.
- 공개 릴리스·서명 단계는 바꾸지 않는다.
