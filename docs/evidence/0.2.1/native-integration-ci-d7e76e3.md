# d7e76e3 제품 CI 실행 근거

[제품 CI 41056755620](https://github.com/hddy774/project-hora/actions/runs/41056755620)의 세 작업이 성공했다. 실행 메타데이터의 소스는 `d7e76e35a8b077e57f44aac0b30dbf5fafa4c005`, 실제 체크아웃은 `3c663df8633a058a7497a6fb1ed4bec8793931d5`다. GitHub Git Commit API에서 두 커밋의 트리는 모두 `e7dbad0f979fa1365f7cd3cdd2083ac229ac8842`로 확인됐다.

| 확인 범위 | 실제 근거 |
| --- | --- |
| 이식형 기능 | 전체 선택 `all`, 261/261 통과·실패 0. Shell Release 빌드 경고·오류 0. |
| Windows | WPF WebView2 실제 앱 보고서의 `actualRenderAndBridge: true`, `hasView: true`, 오류 없음, “만상의 흐름”·“연결됨”. |
| Android API35 | x86_64 Pixel 6 에뮬레이터에서 APK `adb install -r` 성공·Activity 실행. 실제 DOM 렌더와 코어 연결 검사 통과. |

[독립 JSON 기록](native-integration-ci-d7e76e3.json)에 작업 링크, 시각, 디코딩한 네이티브 보고서, 오프라인 자산 SHA256, 산출물·구성 파일 SHA256을 보존했다. 내려받은 두 산출물 ZIP의 SHA256은 GitHub 메타데이터의 digest와 일치한다. 산출물 보존 기한은 2026-11-02이며, 기록은 실행 당시의 소스 범위를 따른다. [이전 PR121 근거](native-integration-ci.json)는 별도 역사 자료로 유지한다.

이 기록은 d7e76e3 이후 소스·개인 투영 실험, API24, 기존 게시 인증서·최종 릴리스 서명, v0.2.0 덮어쓰기 업데이트, 기기 성능·최종 릴리스 수용을 증명하지 않는다. Android 전체 접근성 텍스트 내보내기는 확인되지 않았고, 전경 WebView와 네이티브 DOM 보고서를 확인했다. 이번 작업에서는 CI 재실행·네이티브 실행·벤치마크를 시작하지 않았다.
