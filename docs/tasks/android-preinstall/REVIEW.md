# 리뷰 입구와 검사 범위

먼저 `_prepare_install`의 읽기 probe→기기 관측→외부 Wait→새 회차→설치 허용을 읽는다.
그 다음 install_apk 분기에만 호출되는지, install1회/AM1회와 원래 최종 while이 유지되는지 확인한다.

- `Reader.snapshot`의 기본값True는 원래 앱 PID를 포함한 모든 채널·valid XML·PNG를 요구한다.
  False는 새 기기 단계에만 쓰며 앱이 없다는 이유로 gate를 막지 않는다.
- `_preinstall_probe`는 알려진 offline/boot 미완료만 기다린다. 다른 값·stderr·timeout은 실패다.
- `_normal_device_ui`는 transport 성공이나 inspect_dialog의 None만으로 기기 준비를 합성하지 않는다.
  완전한 캡처 뒤 실제 focus·dialog 없는 XML을 확인한다.
- 설치 오류는 한 번만 기록하며 재시도하지 않는다. Success와 함께 Failure/Error/Exception이 있으면 거부한다.
- `install_apk=None`인 API24/공식은 새 gate를 실행하지 않는다. 해당 workflow·공식 upgrade의 원래 순서도 변경0이다.

기존24개의 원문·수정 후40개의 로그와 AST 대조는 `checks/`에 둔다.
초기 gate 상태에서 기존 예산 fixture가 앱 시작 전 빈 화면만 제공해1개 실패한 로그도 보존한다.
이를 launcher/앱 단계를 나누는 fixture로 바꿨으며 기존 assertion은 모두 유지했다.

새16개는 실제 adb/기기를 호출하지 않는 모의 flow다. 원래 DOM verifier만 subprocess로 호출한다.
실제 offline/XML fixture와 모의 후속 상태의 경계를 [실패 증거](../../evidence/0.2.1/android-preinstall-356c1cf/README.md)에 적는다.
작성·자체 검토는 Codex UI 담당이며 루트 독립 검토·새 native CI·통합 전체 판정은 별도다.
