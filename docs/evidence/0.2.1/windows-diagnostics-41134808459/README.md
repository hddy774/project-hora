# 첫 Windows 진단 실행: 이전 이미지의 실제 성공

루트가 PR217 원형 HEAD `93b28c20b50cfae58de6e2992b1320d295ef06a3`을 검토한 뒤 라벨을 한 번 적용한 실제 실행 [41134808459](https://github.com/hddy774/project-hora/actions/runs/41134808459), 작업 [124334521684](https://github.com/hddy774/project-hora/actions/runs/41134808459/job/124334521684)의 원문입니다. 추가 재실행은 없습니다.

실제 제품 checkout은 `35f33db919f910c9d55c42b61b76459a08688d12`, 기존 함수가 계산한 지문은 `528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e`, clean=true였습니다. workflow 이벤트 merge `b159cd2c5052151bdb3f5c8618236229cedbf70e` 및 정의 head93b28과 제품 소스를 구분합니다. SDK10.0.403 및 .NET10.0.14 설치 목록, 실제 앱 exe의 `0.2.1+35f33…`도 원문에서 확인했습니다.

이번 VM은 **이전 이미지20261021.278.1/eastus**입니다. 실제 WebView2 설치 실행파일과 살아 있는 프로세스의 버전이 모두 **155.0.4283.62**였고 Edge 공개 목록에서 추정하지 않았습니다. 기존 `WaitForExit(45000)`에서 timeout=false/exit0, wrapper 시작~종료 약7.792초, 실제 `passed:true`/`actualRenderAndBridge:true`/`hasView:true`/`error:null`/연결됨/만상의 흐름을 확인했습니다. 이는 기능 진단 시간이며 성능 기준선이 아닙니다.

**새 이미지20261030.294.1의 실패 재현은 아닙니다.** PR94/215의 실제 timeout 원인은 여전히 불명이며 이 성공으로 앞선 실패를 지우지 않습니다. 스크린샷 경로는 이 첫 실행에 없었고 stdout/stderr는 각0바이트, 복사된 startup log는0개, 수집 오류는0개였습니다.

artifact13014867456 ZIP은2754바이트, GitHub digest와 실제 SHA256 `9fdbb6584918d2c0e99c895e7e3cb2b5e5d35fff4fa23fb6f14dc40a16661622`가 일치했습니다. ZIP 자체는 scratch에 보존하고 그 안의8개 텍스트 원문은 `raw/`에 바이트 그대로 복사했습니다(CRLF/BOM 유지). `copied-file-manifest.json`이 원본 경로/파일별 SHA를 고정합니다. 전체 job decoded log SHA는 `b0cf43ef91e82486b18d8c979fefe7405876779be45f1e386562a3dd3d19c955`이며 전체는 scratch, 필요한 줄은 발췌/줄 번호로 보존했습니다. DLL/ZIP/APK/비밀/저장 슬롯은 커밋하지 않았습니다.

`verify-artifact.py`는 scratch의 이미 끝난 ZIP·8raw·메타데이터만 읽어 수행한 독립 검사 원문입니다. 검증 결과 `verification.json` SHA256은 `fa79f5d71cf2317a0f92d21a3def1325312eb95196441c7fbb2189f6fb6c7118`입니다. 재실행에는 원본 ZIP이 필요한데 이 저장소 사본에는 ZIP이 없으므로 축소 사본만으로 그대로 실행했다고 주장하지 않습니다.

루트는 이 결과를 확인한 뒤 같은 소스/지문/SDK/45초로 새 VM3개 고정 배치1회를 별도로 사전 승인했습니다. 어느 이미지든3개 결과를 모두 보존하고 이미지 선별·설치/업데이트·자동 재시도·성공까지 반복을 하지 않습니다. 다음 batch와 이 첫 실행의 결과를 합쳐 성공률을 주장하지 않습니다. 원형93b28 및 이 첫 결과는 변경하지 않고 후속 workflow revision만 제안합니다. 전체 기능/성능과 공식 서명·동일 APK API24/35·0.2.0 업데이트·게시 판정은 별도입니다.
