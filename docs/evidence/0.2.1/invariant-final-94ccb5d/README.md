# 새 런타임 설정94ccb의 실제314 기능 원문·24INV 맵

root의 실제 성공 종료 뒤 소스 `94ccb5d16f0c0d7bba0fd14aa8b0451dfe15bdd5`, 제품 지문 `528e62a918ef892ad7cf28c42f7048b7be4a656fddcb0e260f3c586b9c6e891e`의 **전체314/314·실패0·exit0/end-clean**을 보존했다. 실제 worktree는 `/workspace/seonro-stable-runtime`, CPU0, 2026-10-30 19:46:40.755681~19:55:44.080518 UTC다. 다른 CPU의 성장 실행과 공유한 기능 실행이므로 소요 시간을 성능 표본으로 쓰지 않는다.

- [functional.json](functional.json)·[functional.log](functional.log)·[root-run.original.json](root-run.original.json)은 scratch 원문과 바이트 동일하다. 전체 보고서 SHA256은 `afaa049a3841e786ae3cf16aabf79d59eb4ff0784c3d912b990e57839b66d616`이다.
- [실제 runtimeconfig 사본](runtimeconfig.original.json)은 `System.Runtime.TieredPGO=false`를 담는다. root의 실행 기록에서 `DOTNET_TieredPGO`와 `COMPlus_TieredPGO` override는 둘 다 null이다. 이것은 실제 Tests 프로세스 설정이며 기존 모든 앱/플랫폼 실행에 소급 적용한 주장이 아니다.
- [출처 기록](provenance.json)은 root의 명령·프로세스·종료/clean과 실제 실행 폴더의 네 DLL SHA·생성 정보버전·PE/CLI 메타데이터를 대조한다. 원래 모듈 SHA는 root-run 기록을 사용하고 현재 실행 폴더 사본은 한 번씩 읽어 확인했다. 과거 실제 로드의 독립 인증이나 attribute 생성자 연결까지 입증한 것은 아니다.
- [실행 대조](execution-verification.json)는 변경 없는 기존 validator로 **314개 이름·24INV·152개 명명 연결**을 확인했다. 새 case는0개이고 기존 claim/경계/보조 앵커를 유지한다. [맵](../invariant-execution-map.json)은 계속 `not_asserted`다.
- [SourceReview](../invariant-execution-source-review-94ccb5d.json)는 af91 이후의 변경이 `Directory.Build.props` 설명주석과 `TieredPGO=false` 두 줄뿐임을 기록한다. src/tests/scripts·명령/RNG/serializer·검사 본문은 변경하지 않았다. 동적 PGO 설정 출처를 위한 source-review 영역만 추가했다.

[이전 af91 맵 JSON](../invariant-final-af91fb0/map.snapshot.json)·[Markdown](../invariant-final-af91fb0/map.snapshot.md)은 이전 provenance의 SHA와 바이트 동일하다. 당시 [원문/provenance](../invariant-final-af91fb0/README.md)·[SourceReview](../invariant-execution-source-review-af91fb0.json)·수용 감사·원장은 바꾸지 않았다. 원래 Markdown 상대 링크와 전역 map을 포함한 당시 manifest는 [그 commit의 경로](https://github.com/hddy774/project-hora/blob/6fe2eb1a9157f206ff48601194155d8bfd443481/docs/evidence/0.2.1/invariant-execution-map.md)를 따른다.

이번에는 등록 이름/검사 본문 지문이 같으므로 **기존 구조 검증기는 옛 af91의314 보고서도 통과시킬 수 있다**. [출처 대조](source-attribution-checks.json)는 이 구조 일치와 실제 source/제품 지문 불일치를 구분한다. 현재94 원문 SHA·root-run·FP528·네 DLL 정보버전·명시 PGO=false를 함께 묶고, 이전 보고서를 현재 실행으로 재라벨하지 않는다. 검증기 자체를 고치거나 old314가 구조상 거절됐다고 기록하지 않았다.

이 새 기능 결과는 후속 네 규모/장기/성장·행동·native·공식 서명·업그레이드·릴리스 통과가 아니다. 이전 af91 성능 불합격/quiet 진단·c6 기준선·f3/b491 네이티브·926 저장 실패는 각 source 그대로 보존한다. 별도 후속 실측은 새 실제 소스의 원문으로 판정한다.

이 문서 분기에서 root 소유 worktree/원본을 수정하지 않았다. 새 컴파일·게임/성능/성장/collector 실행, gzip·DLL 바이너리 복사·반복 full product 지문 계산, 제품/검사/스크립트/validator·원장/진행도/readiness 변경은0이다. 작은 원문/메타데이터 보존과 기존 helper·validator 대조만 수행했다.
