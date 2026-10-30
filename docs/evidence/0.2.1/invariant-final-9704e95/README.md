# 9704 기준 309개 기능 원본과 24INV 실행 매핑

실행 소스는 `9704e95508456ee317a378a9b8dac55985bf1849`, 제품 지문은
`f48068bca453202a3826b870e61e9be9972aae4ce8b68e0f8ff4e0b4e0963d28`다.
루트가 `/workspace/seonro-final-9704e95`에서 CPU4로 실행한 전체 C# 기능 **309/309, 실패0, exit0, source clean**의 원문을 보존한다.
루트 고지 실행 시각은 **17:48:59~17:56:54 UTC**, 종료 관찰은17:57:15며 외부 CPU 부하가 있었다.
기능 소요 시간 합산361.946초는 성능 표본이 아니다. 매핑 작업은 원문/빌드 파일을 읽었으며 기능 runner·계측기를 실행하지 않았다.

- [functional.json](functional.json)과 [functional.log](functional.log): 루트 scratch 원문과 byte 동일하다. JSON SHA256은 `863e0f0face78815a9b97efd28260d399515426c20c4551fa39706fe47db6aba`다.
- [provenance.json](provenance.json): 실행 고지, 소스/제품/검사 지문, 실제4DLL의 생성/내장 정보 버전·해시와 실행 폴더 복사본, runtimeconfig/deps 정적 출처를 담는다. 실제 프로세스의 독립 attestation은 아니다.
- [execution-verification.json](execution-verification.json): 현재 등록309개 이름 전부와 24INV·증인144개 연결을 원본 보고서에 대조했다. 매핑 JSON 자체는 `not_asserted`를 유지한다.
- [negative-input-checks.json](negative-input-checks.json): 과거926의308개(새 검사1개 누락), 실패305/308, 선택 지식33개 원문을 현재 전체 통과 근거로 거절했다. 과거 보고서는 수정하지 않았다.
- [validator-self-test.json](validator-self-test.json)과 [refresh-self-test.json](refresh-self-test.json): Python 합성/정적 계약 검증이다. 제품 기능 검사 수에 더하지 않는다.

## 새 검사와 변경 소스의 범위

[현재 맵](../invariant-execution-map.json)은 feature271 + Program38이며, [새 소스 검토](../invariant-execution-source-review-9704e95.json)는
926 이후 C# 변경4경로(제품3개·검사1개)와 byte 동일한 HTML2경로를 구분한다.
새 실제 이름은 `INV-007/024 knowledge physical snapshot: 전체 사본의 매체·접근권과 순서·동기화 시점·중첩 배열 격리가 같다`다.

새 검사는 **부분 KnowledgeAuthority 모듈**에서 공개 full Checkpoint의 Media/Access와 내부 PhysicalCheckpoint의 값·순서·동기화 전후·destroyed/만료/철회·record/중첩 배열 격리·변조 뒤 권한/재조회·도메인 JSON 복원을 대조한다.
공통 GameCodec의 전체 세계 저장이나 공개 GameWorld 명령 검사로 세지 않는다.
실제 두 caller는 첫 Media만, 기존 물리 동기화 뒤 둘째 Media+Access를 읽고 공개 full Clone/즉시 Save·Source/RNG는 유지한다.
노동 가용 판정은 성년 사전을 읽지 않는 한 caller의 context 구성만 줄였으며 일반 정치 성년 context 기본true를 유지한다.

## 이전 기록과 후속 범위

이전 [926 원본/출처/검증](../invariant-final-9263756/README.md)·[당시 JSON 맵 사본](../invariant-final-9263756/map.snapshot.json)·
[Markdown 맵 사본](../invariant-final-9263756/map.snapshot.md)·[SourceReview](../invariant-execution-source-review-9263756.json)는 보존한다.
맵 사본 두 파일은 갱신 전 bytes 그대로다. 이전 provenance의 맵 hash는 이제 `map.snapshot.json`으로 대조할 수 있다.
이전 Markdown의 원래 상대 링크가 필요하면 [당시 커밋의 원래 경로](https://github.com/hddy774/project-hora/blob/92ba0d1f1e182e686b4e702d1dc4b7c0e06db6a8/docs/evidence/0.2.1/invariant-execution-map.md)를 따른다.

이 문서는9704의309개 기준 시점만 마감한다. 루트가 별도926의10,000명 기준선에서 보고한 큰 string 저장 예외(#191), 중단된 AI/성장 자료의 복구와 이후 코덱 수정·새 검사는 별도다.
기존926의 성장/AI/민감도 자료를9704 실행으로 승격하지 않는다. 제안 카탈로그의 `test_status=not_implemented`·원장·Notion·제품/CLI/scripts/C#는 변경하지 않았다.

전체309개와 명명된144개 증인 연결 통과가 24개 불변식의 모든 조합·정본111개 작업 전체·성능/성장/장기 안정성·전체T05·native·Windows/Android/API24/35·APK 릴리스 완료를 뜻하지 않는다.
API24 probe workflow 추가나 별도 UI 클릭 증거를 C# 검사 수에 더하지 않는다. 검증기의 `--executed-source`는 호출자 선언이므로 원문 hash·실행 고지·4DLL·제품 지문을 함께 확인한다.
