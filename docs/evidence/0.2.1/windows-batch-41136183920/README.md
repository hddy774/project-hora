# Windows 고정3VM 배치: 성공·실패 모두 보존

루트가 첫 실행93b28 결과 확인 뒤 사전 승인한 고정3VM 배치1회입니다. HEAD `190e016b7d996befc7d94fd095d14e4a75cb94be`, 실행 [41136183920](https://github.com/hddy774/project-hora/actions/runs/41136183920), attempt1을22:09:17UTC owner라벨1회로 시작해 모두 보존했습니다. 추가 배치/재실행/설치/업데이트/이미지 선별은 없습니다. 첫 단일 실행과 합산 성공률을 계산하지 않습니다.

세 VM 모두 실제 제품 checkout `35f33db919f910c9d55c42b61b76459a08688d12`, 기존 함수의 실제 지문 `528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e`, clean=true, SDK10.0.403, .NET10.0.14 설치 목록, 앱 exe `0.2.1+35f33…`를 확인했습니다. workflow 정의HEAD190e016/event merge d961b26과 제품 소스35f33은 별개입니다. 원형190 정의와 첫93b28 원문은 변경하지 않았습니다.

| sample / 작업 | 이미지 / 지역 | 실제 WebView2 exe | 기존45초 gate | native JSON |
| --- | --- | --- | --- | --- |
| 1 / 124339099556 | 20261021.278.1 / eastus | 155.0.4283.62 | success, exit0 | render+bridge true |
| 2 / 124339099067 | 20261030.294.1 / northcentralus | 156.0.4314.50 | success, exit0 | render+bridge true |
| 3 / 124339099399 | 20261030.294.1 / westus3 | 156.0.4314.50 | **failure, timed_out:true, exit:null** | 늦게 true 작성, gate 수용 아님 |

세 native JSON의 SHA256은 모두 `ea6c8db2409358dd1f7269753582643ed81b4c4e39d8165f5607030dde11f975`로 같아도 판정은 **sample1·2 success / sample3 failure**, run 전체failure입니다. sample3은 프로세스 시작22:10:40.9024395Z, native JSON LastWrite22:11:48.5731668Z(약67.67초 후)이고 wrapper_start 기준으로는67.988901초 후입니다. timeout을 넘긴 뒤 남은 실제 render+bridge true/error:null/연결됨 JSON을 finally가 보존한 것이며 이를 성공으로 승격하지 않았습니다. 프로세스 before-kill은 응답true/선로만상록/handle196908, 캡처 시점에는 visible 창 없음으로 PNG0·수집오류1을 보존했습니다. 세 stdout/stderr는 각0바이트, 복사된 startup log는0개입니다. 구간별 상세 진척 시각이 없어서 지연·창 부재의 구체 원인은 불명입니다.

새 이미지의 실제 성공sample2와 같은 이미지 실패sample3을 함께 보존하므로 새 이미지/156의 항상 실패·명백한 버전 비호환·런타임 미설치로 단정하지 않습니다. 실제 설치/실행 exe 버전을 관측했고 Edge 공개 목록의156.0.4314.56을 WebView2 버전으로 대체하지 않았습니다. 기존94/215 timeout이 같은 원인이라고 확정하지 않습니다. 위 시각은 기능 진단 관측이며 성능 기준선이 아닙니다.

| sample | artifact | ZIP bytes | GitHub digest와 대조한 SHA256 |
| --- | --- | --- | --- |
| 1 | 13017061874 | 2784 | cd5a2750a30c1b7c11337be55a7fb2ec5b000bf814938b770a47c5333d7b6d47 |
| 2 | 13016102865 | 2803 | cb792a9d1859ad325b979c62c53baef37740a41760d2b69d2265e0c05c2034c9 |
| 3 | 13016013306 | 2932 | 83b102bbd0e8a95495c614f752934306ef193dfb39bdebc5367e3e80c7393f78 |

24개 artifact 텍스트 원문을 sample별 raw에 바이트 그대로 복사했습니다(CRLF/BOM 유지). ZIP/전체 job decoded log는 scratch에 보존하고 저장소에는 ZIP·바이너리를 추가하지 않았습니다. 필요한 job 줄만 원본SHA/줄 번호로 발췌했습니다. copied-file-manifest와 raw-file-manifests가 파일SHA와 출처를 고정합니다. 비밀·환경 전체·게임 저장 슬롯 수집은0입니다.

이미 종료된 ZIP/24raw/CI메타를 읽은 독립 검사 `verification.json` SHA256은 `9362cd34efb8e34ef2792e3794ff33d8c6336be7e6827025cc2a233a0285dded`입니다. artifact_data_validated는 원문 무결성·source·판정 대조이고 batch_gate_conclusion은failure입니다. 검증기 원문도 보존하지만 저장소 축소 사본에는 ZIP이 없으므로 그대로 재실행했다고 주장하지 않습니다. 새 앱/기능/성능 실행을 하지 않았습니다.

공식 release의 기존60000ms/Windows proof호출/실행횟수는 별도이며 이번 PR은 stdout/stderr·wrapper 관측·finally의 기존 raw/startup 로그만 보존합니다. 공식 경로에는 screenshot helper를 추가하지 않습니다. 전체 기능/성능·보유 인증서·동일 APK API24/35·0.2.0 업데이트·게시 판정은 별도입니다.
