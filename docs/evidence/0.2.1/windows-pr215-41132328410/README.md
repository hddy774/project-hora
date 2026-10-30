# PR215 윈도우 실패 원문과 성공 비교

실행 [41132328410](https://github.com/hddy774/project-hora/actions/runs/41132328410)의 Windows 작업 [124326069182](https://github.com/hddy774/project-hora/actions/runs/41132328410/job/124326069182)은 빌드 0오류 뒤 기존 `WaitForExit(45000)`를 넘겨 wrapper가 앱을 강제 종료했습니다. 실제 원인은 **불명**입니다. 화면·연결 진단 JSON, WebView2 설치 버전, 프로세스 출력/상태, 시작 오류 로그가 이 실행의 원문에 없어서 환경과 앱 내부 원인을 구분할 수 없습니다. Windows 전용 artifact도 없습니다. 실패를 성공으로 바꾸거나 재실행하지 않았습니다.

이벤트 head는 `8a7f23f7b53e79c8bbfe583bcbf375d494851cff`, 실제 PR merge checkout은 `360eb097cfade5c29c1a00fea4080e232d5652e9`입니다. 두 커밋의 tree는 `992f288df34e99a0009c69950b221ce512f2396f`로 같고 head 커밋은 릴리스 노트 1파일만 수정합니다. 제품 지문 `528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e`는 루트가 확인한 출처이며 이 읽기 진단에서 지문을 다시 계산하지 않았습니다.

| 비교 | Windows 원문 결과 | 이미지 | 지역 |
| --- | --- | --- | --- |
| PR213 / run41131121103 / job124321984975 | 약8초 뒤 `actualRenderAndBridge:true`, `error:null` | `20261021.278.1` | centralus |
| PR214 / run41131877137 / job124324550162 | 약8초 뒤 같은 실제 화면·연결 성공 | `20261021.278.1` | westus2 |
| PR215 / run41132328410 / job124326069182 | 45초 watchdog timeout, 실제 원인 불명 | `20261030.294.1` | westus |

모두 `windows-2025-vs2026`, Windows Server2025, SDK10.0.403/runtime10.0.14이고 빌드는 0오류입니다. 이미지 차이는 관찰한 단서이며 인과관계로 확정하지 않습니다. 공개 이미지 목록의 Edge 버전은 WebView2 런타임 버전으로 사용할 수 없습니다.

로그 3개는 연결 도구가 반환한 UTF-8 decoded 원문을 바이트 그대로 보존했습니다. 줄 번호 발췌와 원본 SHA는 `excerpt-provenance.json`, 사본 SHA/출처는 `copied-file-manifest.json`에 있습니다. 기존 로그의 마스킹을 유지했으며 비밀·키스토어·환경 전체·게임 저장 슬롯은 읽거나 복사하지 않았습니다. 모든 artifact 전체를 보관했다고 주장하지 않습니다.

최종 run의 Android 작업은 성공했고 portable 원문은 314/314, failed0입니다. 내려받은 `product-evidence` ZIP artifact13014775251은 SHA256 `293d0ec78d533bf5a75e9b243ee7148acea05d1f6cac4c1ad2ea5aaad3c11e0b`, 원문 `product-tests.json`은 SHA256 `ddab0ceb4300d881a20fe2a478ee51f4de690cab06f4a09a9b38dbf984c6b318`입니다. ZIP/제품 report 전체는 scratch에 보존하고 이 폴더에는 복사하지 않았습니다. Android 전체 원문을 이 진단에서 다시 내려받았다고 주장하지 않습니다.

이 실패는 이전94의 무진단 timeout과 같은 원인이라고 확정하지 않습니다. 이전4fe 성공, API24/35 디버그 실행, 성장9조건, 공식 보유 인증서·동일 APK·0.2.0 업데이트·최종 릴리스 판정은 각각 별도 근거입니다. 제품·테스트·기존 CI·timeout·gate 변경과 새 앱 실행/재실행은 0입니다. 새 진단 workflow는 루트가 별도 검토 후 라벨로 실행합니다.
