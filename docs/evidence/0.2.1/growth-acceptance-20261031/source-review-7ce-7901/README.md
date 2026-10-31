# Z04 성장 소스와 최종 소스의 읽기 감사

성장 실행 `7ce66943b539420c9dc1e3fa8a611daae58020ff` → 최종 기능/단기 실행
`7901f344462ebc1f98d7105efd79ab0aa058b8fe`의 Game/Core 변경18경로를 모두 분류했다.
분류되지 않은 경로는0개다. Core 변경은0개이며 나머지 기존94경로는 바이트가 같다.
법칙·세계 시간/RNG·성장 정책·AI 선택의 추가 소스 변경을 이 정확한 delta에서 찾지 못했다.
실제 수용 판정은 통합 담당자가 한다.

## 18경로 대응

아래 경로의 접두사는 `src/Chronicles.Game/`다. 각 전후 SHA·근거 파일 SHA는 `audit.json`에 있다.

| 실제 경로 | 분류·기존 근거 |
| --- | --- |
| `Cultivation/CultivationEngine.cs` | PR229 수행 partial, 기존 토큰/멤버 보존 |
| `Cultivation/CultivationEngine.Formation.cs` | 같은 보존 검사 |
| `Cultivation/CultivationEngine.Martial.cs` | 같은 보존 검사 |
| `Cultivation/CultivationEngine.Practice.cs` | 같은 보존 검사 |
| `Cultivation/CultivationEngine.Preparation.cs` | 같은 보존 검사 |
| `Cultivation/CultivationEngine.Progression.cs` | 같은 보존 검사 |
| `Cultivation/CultivationEngine.Recovery.cs` | 같은 보존 검사 |
| `World.PeopleSocialBridge.cs` | PR228 사회 partial, 기존 멤버·본문·초기화 순서 보존 |
| `World.PeopleSocialEcology.cs` | 같은 보존 검사 |
| `World.PeopleSocialEvents.cs` | 같은 보존 검사 |
| `World.PeopleSocialOaths.cs` | 같은 보존 검사 |
| `World.PeopleSocialReputation.cs` | 같은 보존 검사 |
| `World.PeopleSocialRequests.cs` | 같은 보존 검사 |
| `World.PeopleSocialTraits.cs` | 같은 보존 검사 |
| `CanonicalHashSink.cs` | PR237 호출 소유 stream SHA-256, canonical Hash의 전체 임시 배열 축소 |
| `GameCodec.cs` | PR237 Hash 본문, PR236 기본128→16MiB 선택 경계, PR244 JSON 쓰기 분기·UTF8 조각 helper |
| `Knowledge/KnowledgeAuthority.cs` | PR240 internal 세계 소유 상태 연결; public Restore/Checkpoint 사본 유지 |
| `World.KnowledgeBridge.cs` | PR240 private runtime 연결·Save의 반복 전체 사본 제거 |

## 분리 증거의 소스 연결

기존 증거의 기준은404767a다. 이번에는 해당 증거를 재실행하지 않고 다음 바이트 연결을 확인했다.

- 성장7ce의 원래 수행 엔진 SHA는 `b1e36a663b66000da97cb5d0f0a613a35a5638f80f54e74815cd7f12fb5ff632`로,
  `docs/tasks/2026-10-31-cultivation-engine-split/member-comparison.json`의 원본 SHA와 같다.
  최종7901의7파일도 보고서의 결과 SHA와 전부 같다. 기존43메서드의 서명/본문 토큰·주석 보존 근거가 연결된다.
- 성장7ce의 원래 사회 브리지 SHA는 `b3f7855d524f3a9fed32a3b184357659a5f0d4b1499b8c89968e260db1deb8ca`로,
  `docs/tasks/people-social-bridge-split/member-comparison.json`의 원본 SHA와 같다.
  최종7파일도 결과 SHA와 전부 같다. 기존43멤버/40메서드·멤버 trivia·필드 초기화 순서 보존 근거가 연결된다.
- GameCodec/KnowledgeAuthority/KnowledgeBridge의 성장 원본은 세 leaf의 b159 기준 파일과 같다.
  최종 네 성능 경로는 통합된 각 구현 커밋의 파일과 같다. 실제 diff에서 Codec 변경도 위 세 범위로 끝난다.

Laws.cs, State.cs, World.cs, World.Actions.cs와 모든 Core 파일은 이 범위에서 같다.
World.CultivationBridge/World.EcologyBridge/World.PeopleBridge도 같다.
World.Planning/IntelligentPlanning/CultivationPlanning/EcologyRecovery의 선택 로직도 같다.
별도 새 성장 계수·난수 호출·월 전이·AI 목표 분기를 변경된 경로에 추가한 소스는 없다.

## 실제 유한 대조와 한계

- PR240의 df DLL 대조는 선언 기능 fixture의 seed42/73, 전수·학습·유료 필사·실험·수행·취소/거부·재전송
  49경계와20저장 사본이다. 월별 Hash/RNG/ID/지식과 저장 bytes가 같지만 기본 자연성장의 모든 분기를 포괄하지 않는다.
- PR237은 기존 canonical buffered oracle, PR244는 기존5f DLL·serializer oracle와 schema2 bytes를 대조했다.
  PR236은16MiB를 넘는 본문을 기존 binary3로 쓰므로 큰 저장의 물리 bytes는 달라질 수 있다.
  canonical 세계 상태·논리 체크섬·순번 및 이전 JSON2/binary3 읽기 계약의 보존과 파일 형식의 동일성은 구분한다.
- 최종7901의 기존 실제323/323 전체 기능 결과는 SHA
  `1cca9aea9ffdef4149282cf8586fbbaf653288a62c31b5beccbe9b1bcd2706ba`로 root가 확인했다.
- `population-crosscheck.json`은 별도의 기존 C6 합성 기준선과 최종7901의12개월 결과를 읽었다.
  인구2/100/1000/10000의 initial/final Hash·법칙·fixture·작업량·인지 bytes가 모두 같고 기존32상한이 통과했다.
  이 비교의 기준선은 C6이며7ce의6000개월 자연성장 궤적과 같은 실행으로 취급하지 않는다.

이 감사에는 새6000개월 성장, 빌드, 기능, 성능, native 실행이 없다. 과거7ce 결과의 실제 source/FP를
최종7901 실행으로 바꾸지 않는다. SDK/runtime/호스트·검사 하네스·플랫폼은18개 Game/Core 소스 범위 밖이며,
최종 장기/플랫폼/릴리스와 Z04 수용은 각각 실제 근거와 root 판정으로 남긴다.
제품·원장·readiness 변경과 PR 생성은0이다.

재현 읽기 명령: `git diff --name-status 7ce66943b539420c9dc1e3fa8a611daae58020ff 7901f344462ebc1f98d7105efd79ab0aa058b8fe -- src/Chronicles.Game src/Chronicles.Core`.
