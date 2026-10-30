# 0.2.1 수용 근거 감사

대상은 `codex/v0.2.1`의 `d1f3a6a4ce7018d37df1d4838d473b0794591fe2`다. 개정6 Notion 로컬 원문, 개정7 결정, 111개 작업과 55개 V020 대응을 새 `Chronicles.Game`·공용 Shell·Android/Windows 호스트·실제 시험 본문에 대조했다. 이 문서는 상태 변경 제안이며 원장이나 Notion의 완료 상태를 수정하지 않는다.

정적 소스 감사만 수행했다. 새 게임 시험이나 성능·프로파일링·성장·장기 실행은 실행하지 않았다. 엔진 없는 .NET10 구현이 현재 승인 범위다. Unity 실행이나 네이티브/SIMD 도입을 새 선행 조건으로 만들지 않는다.

## 먼저 해결할 수용 차이

| 근거 | 현재 차이와 필요한 확인 |
| --- | --- |
| G01 | 초기 원천 Definition 누출은 root가 KnownSources로 수정했다. 최신 소스에서 실제 개인 정보 비간섭 결과를 대조한다. |
| G02 | 배원단 복용이 이전 Person.Work만 증가시킨다. 실제 수행 sidecar에 영수증 단위로 한 번 연결하고 복용/저장/시간 경과를 검증해야 한다. 담당자 확인·수정 진행 중이다. |
| G03 | PR109 사회/명성/특성/비인간 자가 생리 소스와 공통 hook은 합쳐졌다. 최신 실제 사회·종족 시험 및 직접 권한 경로 검증은 담당 후속 작업이다. 기존15/15를 새 통과로 합산하지 않는다. |
| G04 | 변이 법칙을 읽는 frozen GeneticModel은 PR109로 합쳐졌다. 확률0/폭0·중간 법칙 변경 시험은 담당자가 보고했으며 최종 소스 전체 실행과 대조한다. |
| G05 | PR97의 풍부한 정본 DTO와 실제 query7개 시험 소스는 합쳐졌다. 담당자7/7과 최신 전체/브라우저/네이티브 통과를 구분한다. |
| G06 | 초기5개를 포함한8개 kernel 시험과 별도로 자동 소비/NPC구매/계절충격/원격손실의 실제 public4개 소스가 추가됐다. 최신 실행과 광범위한 제도·교육단절·병목 연결을 대조한다. |
| G07 | 실물 영석 bridge의 초기화/명령/불변식 hook은 연결됐다. 공개6개 시험 후속 결과로 실소비/법칙 revision/단일 발급/복원을 대조해야 한다. |
| G08 | 최종 schema2의 전체 불변식·중간 상태·조회/저장/명령 재현 감사는 모든 연결 이후 한 SHA의 전체 기능 시험으로 마감해야 한다. |
| G09 | 최신 풍부한 화면의 390px/확대/키보드/터치/모드 전환 및 실제 Windows/Android 패키지 실행 근거가 필요하다. 기존 브라우저 결과/CI 정의 자체는 새 실행 통과가 아니다. |
| G10 | 성능·추론 비용·인구/저장 단계·성장 분포·장기 안정성·EX 비교는 사용자 지시대로 모든 기능 이후에만 수행한다. |
| G11 | 실제 Attack 피해는 8축 중 출력/방호를 읽는다. 제어/지속/이동/감지/협응/비용의 실제 작용·전술과 공개 비교 근거를 정본 범위에 맞게 확인해야 한다. 복잡한 전투 엔진을 새로 요구하는 뜻은 아니다. |
| G12 | 최종111/55 원장·live Notion·문서·VERSION·유지 서명·실제 릴리스 자산 대조는 root 최종 작업이다. |

## 시험 근거의 범위

- 최신 root 전체 기능 보고서: `c94712e`, **171/183**, `/workspace/scratch/wired-tests-v5.json`. planner11개는 창세 화폐 영수증을 남긴 채 CreatedMoney를 줄이는 fixture, query1개는 출처 없는70영석 fixture다. 실제 잔액 이전으로 수정 중이며 엄격 화폐 검증은 유지한다. 이후 Hosting 추출 `d1f3a6a`의 전체 통과를 뜻하지 않는다.
- root 기존 전체 기능 보고서: `363dd32`, **146/146**, `/workspace/scratch/wired-tests-v4.json`. 후속 planner/생태/화폐/사회/조회/Hosting 추출 전체 통과라는 뜻은 아니다.
- planner PR107 `fbc9cb8`: 선택 기능 **11/11**. 당시 전체154/157은 다른 공통 fixture3개 실패를 포함한 역사 결과이며 root가 후속 수정했다. 최신 전체 결과로 재사용하지 않는다.
- root 확장 host recovery: 선택 **1/1**, `/workspace/scratch/host-recovery-v2.json`. 실행 SHA가 제공되지 않아 해당 부분을 미상으로 기록했다.
- People PR109와 query PR97의 후속 결과는 담당자 보고로 별도 표시했다. 감사 기준 소스의 통과로 합산하지 않는다.
- `KnowledgeIntegrationScenarios`의 앞2개는 계약/모듈 시험이다. `EcologyIntegrationScenarios`는8개 kernel과4개 실제 자동 hook public 사례를 별도로 분류했다. 파일 이름만으로 실제 자동 hook 시험으로 분류하지 않았다.
- 이전 `core-tests.json`38개, 브라우저9메뉴, 초기 성능/성장/soak 산출물은 해당 시점·범위의 기록이다. 최종 수용 증거가 아니다.

JSON에는 실제 시험 class/줄/이름·기존 실행 보고서 대응, 코드 경로, 미수용 조건과 담당 진행 상태를 별도로 보존했다. 아래 “실제 경로 근거”도 제품 전체 완료나 최종 릴리스 통과를 뜻하지 않는다.

## 111개 작업별 제안

| 작업 | 제안 | 확인된 범위 / 남은 수용 |
| --- | --- | --- |
| V021-A01 0.2.0 편입·코드 통합 기준선 | 문서 근거 | All 55 legacy IDs and their 111-task targets survive; actual baseline SHAs and isolated old product evidence are recorded. |
| V021-A02 Canon Map·정책 결정·임시값 | 문서 근거 | Revision7 explicitly distinguishes confirmed kin/both-Golden-Core policies, developer-tuned numbers and source proposals. |
| V021-A03 안정 ID·자산·DTO·명령 계약 | 부분 수용 | Person/Body/Soul IDs, slot generations, command idempotence, candidate commits and domain-reference validation are live. **G08** |
| V021-B01 공통 일곱 품계 | 부분 수용 | Shared seven grade boundaries and real appraisals; innate score and physical item effects are separate. **G05** |
| V021-B02 폐품·결손·손상 | 실제 경로 근거 | Low score is separate from physical missing pages, lost core, broken circuits, contamination and damage; defects affect real access/effects. |
| V021-P01 동적 인구·생애·영혼 저장소 | 실제 경로 근거 | Dynamic real birth/death and stable Person/Body/Soul identity survive slot reuse and persistence; stale generation commands reject. |
| V021-P02 초기 두 인물·학습/생계 출발 | 실제 경로 근거 | Exactly two Half Immortal founders, zero sects/hidden people, real initial assets and explicit personal knowledge; subsequent children have no inherited realm/mastery. |
| V021-P03 인구·가계 소비·분업·정착 | 부분 수용 | Actual care uses food/labor; automatic ecology creation/consumption/month settlement and real household/market facility hooks are now wired. **G06** |
| V021-F01 도려·가족·사회 관계 | 부분 수용 | Actual partner independence plus shared timed social assessment/relay/oath dispatch, completion, witnessed event and monthly trait/reputation producers are now wired. **G03** |
| V021-F02 임신·출생·부모·윤회 연결 | 실제 경로 근거 | Both parents qualify at conception; law/genome snapshot is frozen, soul assigned once at actual birth, command replay cannot duplicate a child. |
| V021-F03 유전 원형·혈맥·체질·발현 | 부분 수용 | Actual conception now captures typed GeneticModel using configured mutation chance/magnitude, law revision and frozen allele selections; prenatal/body and birth soul remain separate. **G04** |
| V021-F04 양육·교육·분가·상속 | 부분 수용 | Paid care, real teaching, adult household leadership, death estate and public-office/account succession are separate. A complete actual-command adoption/household-separation and development→education chain is not established by the cited focused cases. |
| V021-R01 오행 영근·선천 구조 | 실제 경로 근거 | Five inherited roots, acquired root growth and current realm have independent values and causal sources. |
| V021-R02 복합 수행 공정·층계 | 실제 경로 근거 | Actual monthly source debit feeds absorption/refinement/circulation/assimilation, loads and structure progress; neutral whole-course formula is checked separately. |
| V021-R03 입문·병목·돌파·안착 | 부분 수용 | Entry, prerequisite bottlenecks, actual timed preparation/support consumption, fracture death and aftercare are connected. **G02** |
| V021-R04 축기·금단·상위 프로필 | 부분 수용 | All realm-specific structures and the real founder profile are defined; actual upper-life/aftercare flow has public coverage. All thirty routes use pure domain transition coverage; do not report every route as a completed public-world vertical scenario. |
| V021-R05 영맥·공급·혼잡·자리 | 실제 경로 근거 | Real finite Qi, element supply, turbidity, crowding/seats, usage rights and paid arrays enter cultivation context once. |
| V021-R06 퇴행·회복·수행 보정 | 부분 수용 | Damage, timed real recovery and marker release have public evidence; route conversion and retreat costs have module evidence. **G10** |
| V021-V01 자질·천교·후보·공석 | 부분 수용 | World/region/species/realm reevaluation is wired annually with dated vacancies/candidates; absolute aptitude is separate. **G05** |
| V021-V02 명성·인지·사회 반응 | 부분 수용 | Actual event witnesses now create dated received reputation claims; social dispatch/independent decisions use received judgments and planner uses cached hostility. **G03** |
| V021-W01 무인 독립 10경지 | 부분 수용 | Root-free ten-stage martial progression, default six-hundred-year cap and independent reproductive qualification are implemented. An actual public full martial progression/combat/lifecycle vertical case is not supplied by the pure ten-stage module scenario. |
| V021-W02 종족·기원·요괴·생태 | 부분 수용 | Typed species/body/title, explicit nonhuman creation, activity capability guards, self physiology/habitat and preference helper source are now merged and monthly hooks call them. **G03** |
| V021-L01 다목표 AI·실패 회복 | 실제 경로 근거 | Nine scored real goals, urgent survival/care preservation, actual recipe knowledge and receipt-driven multi-step recovery execute through shared actions. |
| V021-M01 10주·100지점 실제 지도 | 실제 경로 근거 | Exactly10 provinces×10 stable places, one adjacency graph and separate actor movement; physical facilities do not increase strategic-node count. |
| V021-M02 복수 시설·자원·비경·성립 | 부분 수용 | Automatic real household/market/workshop descriptive facilities, installed arrays and mapped rights are wired; no stock/capacity/node is created by labels. **G06** |
| V021-M03 소유·통치·운영·통행권 | 실제 경로 근거 | Property, operation, administration, passage, sovereignty and inheritance remain separate authority transitions. |
| V021-M04 이동·경로·통로 사건 | 실제 경로 근거 | Actual travel duration and known paths, closed-route failure, cargo/title and return location preserve person/item uniqueness. |
| V021-M05 탐색·기연·비경 | 부분 수용 | Real timed exploration, source observations, finite per-place discovery reward, known neighbors and seal hazard cause producers exist. Explicit simultaneous finite-reward competition and full progression/condition replay acceptance are not demonstrated by the cited focused public cases. |
| V021-K01 기예 작업·예약·공정 | 실제 경로 근거 | Actual time capacity, one physical reservation ledger, irreversible consumed inputs, cancellation and one-time output/mastery settlement are connected. |
| V021-K02 학습·숙련·Teaching·서적 | 실제 경로 근거 | Initial/parent/mentor/book/apprentice paths require explicit sources and paid time; comprehension and actual practice/mastery are independent, never inherited or gained by owning a book. |
| V021-K03 연단 수직 구현 | 부분 수용 | Actual material processing, physical pills, storage/trade/use and healing/breakthrough support exist. **G02** |
| V021-K04 연기 수직 구현 | 부분 수용 | Real smith recipes/tool output, inventory ownership, equipment/binding/repair module paths and auction transfer are available. A public forge→equip→bind→damage→repair continuation case is not established by current craft/auction cases. |
| V021-K05 제부 수직 구현 | 부분 수용 | Actual talisman recipes, finite physical use effects, sale and cause IDs exist in the shared inventory engine. A successful public craft→sale→actual finite talisman use and replay case is not established by the pill-oriented craft scenario. |
| V021-K06 진법 수직 구현 | 실제 경로 근거 | Actual installation rights, map range, paid upkeep, expiry/destruction and real finite Qi/defense contribution are connected. |
| V021-K07 필수 전문 활동 | 부분 수용 | Finite gather/mine, real medicine/appraisal/exploration and transport have actual command evidence; farm.food has a real input recipe. Successful public farming production and full representative cross-profession cycles are not all established by these focused cases. |
| V021-E01 지역 시장·실제 거래 주체 | 실제 경로 근거 | Actual local sellers/buyers/physical stock and funded transfers reserve and settle once; no invisible market-making NPC exists. |
| V021-E02 실수급·가격·생산 | 부분 수용 | Actual funded market receipts, automatic monthly ecology, own typed source snapshots and actual NPC purchase through shared commands are now wired. **G01 G06** |
| V021-E03 교역·화물·운송 계약 | 실제 경로 근거 | Physical in-transit title, labor, fees, real destination and blocked/death failure recovery preserve assets. |
| V021-E04 경매·실물·입찰 예치 | 실제 경로 근거 | Real escrow, funded bids, replay rejection, stable simultaneous close and no-bid/death refunds are covered across world and module scenarios. |
| V021-E05 생태 경제·공적/개인 회계 | 부분 수용 | Real accounts, estate/care/material balances plus actual genesis/stone-origin currency validation and command hooks are now wired. **G06 G07** |
| V021-S01 0개 시작·종합 종문 성립 | 실제 경로 근거 | Zero initial sects, real funded founding/dissolution, concurrent limit and no invisible initial property; actual policy cancellation is preserved. |
| V021-S02 종문 생활·교육·재정 | 부분 수용 | Real office/budget/work reservation, funded educational requests and actual bilateral teaching exist. All detailed office/payroll/loyalty/contribution effects retain module evidence; current social trait hooks are merged but the public full sect-life cycle remains only partly demonstrated. |
| V021-S03 종문 승계·외교·왕조 관할 | 부분 수용 | Public succession/debt and territory changes preserve authority; reincarnation creates no old office automatically. A combined public local-charter/diplomacy/succession and rebirth rights-denial chain is not established by isolated module cases. |
| V021-G01 가문·왕조·봉신 통치 | 실제 경로 근거 | Actual one-province sovereignty, acyclic vassal graph, direct administration and per-layer taxes remain distinct from family/sect membership. |
| V021-G02 정치 승계·무영토 왕조 | 실제 경로 근거 | Territory0 dynasty persists with zero tax, real restoration, successors and one principal/creditor/debtor/enforcement owner; no phantom receivable. |
| V021-G03 10대 정책·세부 선택·변경 | 실제 경로 근거 | All ten detailed policy categories, adopted/prepared/insufficient/implemented states, shared labor/funds, current jurisdiction revalidation and refund/history have world plus module proof. |
| V021-X01 빙의·조작권·명령 기반 | 실제 경로 근거 | Possession shares the same identity/action, yields back to AI without resetting progress and authenticates command IDs/actor/revision. |
| V021-X02 새 인물 생성·세계 편입 | 부분 수용 | Explicit adult external-origin creation preserves initial2, separate identity and zero issued assets; natural birth uses frozen parents. **G03** |
| V021-X03 동등한 인물 행동·권한 | 부분 수용 | AI and controlled actors call shared action validators with actual time, rights, independent teachers and funded counterparties. **G03 G11** |
| V021-X04 플레이 시간·계획·사망 전환 | 부분 수용 | Command submit/replay/cancel, irreversible-stage cancellation, preserved shared actions and host pause/save are distinct. **G08 G09** |
| V021-U01 통일 셸·관찰/플레이 UI | 부분 수용 | One shared Korean shell supplies navigation/search/back, detail, empty state and common commands across local/Windows/Android bridges. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G05 G09** |
| V021-U02 그래프·표·가계/관계 비교 | 부분 수용 | Shared shell has bars, tables, census line/realm distributions, family links and explicit units/unknown labels. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G05 G09** |
| V021-U03 인물·가계·수행·천교 화면 | 부분 수용 | Existing person/family/cultivation/knowledge panels have source-separated permission gates. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G05 G09** |
| V021-U04 지도·인구·이동·권리 화면 | 부분 수용 | Actual100 places, known routes, multiple facility labels and sovereignty/property commands have core support. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G05 G06 G09** |
| V021-U05 종문·정책·왕조·경제 화면 | 부분 수용 | Real policy, territoryless dynasty, inventory/jobs/contracts/auctions and public accounts exist behind common commands. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G05 G06 G07 G09** |
| V021-U06 사건·연대기·관찰 목록 | 부분 수용 | Actual causal events, person/group/place links, actor journals and filtered history lists exist. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G03 G05 G09** |
| V021-D01 인구·통계·가계·이력 투영 | 부분 수용 | Monthly census and bounded ecology history plus filtered people/knowledge projections preserve missing/unknown semantics. **G05 G06 G10** |
| V021-D02 최종 통합 저장 감사 | 최종 검증 대기 | Feature-level saved intermediate states and pending/backup recovery already pass focused/public tests. **G08** |
| V021-T01 최종 불변식 감사 | 최종 검증 대기 | INV-ID functional harness and strict real domain references are present. **G08** |
| V021-T02 전 범위 시나리오·조회 무영향 | 최종 검증 대기 | Real cross-domain births/learning/craft/politics/AI/13routes and pure queries have focused evidence. **G08** |
| V021-T03 시간·명령·결정론 회귀 | 최종 검증 대기 | Split advance, culture, command replay and checkpoint continuation are exercised at real world boundaries. **G08** |
| V021-T04 단계별 성능·보정·장기 안정성 | 최종 측정 대기 | Performance/growth/soak harness source and baseline distinction are preserved; no measurement was run during this audit. **G10** |
| V021-T05 엔진 없는 UI·모드/입력 사용성 | 최종 검증 대기 | Existing browser1280/390 functional evidence and actual adapter check scripts exist; CI defines Windows smoke and Android emulator checks. **G09** |
| V021-T06 통합 릴리스·근거·버전 일치 | 최종 검증 대기 | VERSION0.2.1, project identity, multi-PR history and retained-signing workflow source exist. **G08 G09 G10 G12** |
| V021-Z01 세계 법칙 registry·기본 프리셋 | 실제 경로 근거 | Typed/bounded/default/reason/provisional law registry records fixed two-founder/zero-sect/Golden-Core/Gate rules and explicit0-disable semantics. |
| V021-Z02 법칙 검증·원자 시행·영향 분석 | 부분 수용 | Batch laws validate atomically; active pregnancy, cultivation attempts, writing inputs and political preparation retain adopted snapshots; capacity changes grant no stock. **G04 G08** |
| V021-Z03 법칙 편집·프리셋·차이/이력 UI | 부분 수용 | Observer-only atomic JSON preset import, defaults/history/diff controls and unit validation exist. **G09** |
| V021-N01 관찰/플레이 지식별 조회 계약 | 부분 수용 | Rich canonical observer/self projections are now connected and scoped before disclosure, counts, revision tokens and source histories; ecology now uses only cached typed source rows. **G01 G05** |
| V021-N02 플레이 정보 획득·행동 검증 | 부분 수용 | Unknown IDs/remote private stock reject; actual exploration, independent teaching and receipt outcomes acquire facts through normal commands. **G01 G03** |
| V021-N03 모드 전환·캐시/화면 누출 검수 | 부분 수용 | Core same-information counterfactuals, selected actor/generation caches and serialized host requests exist. **G05 G09** |
| V021-Y01 6대 기반·24개 재능 파생 | 실제 경로 근거 | Six foundations,24 derived talents, ten cognition maps and actual one-source study credits separate innate, learned, comprehension and current expression. |
| V021-Y02 10분류·40특성 예시와 수명주기 | 부분 수용 | Forty trait lifecycles plus witnessed events, role/habit-derived acquisition, real current-damage/recovery markers and source-backed own preference effects are wired. **G03** |
| V021-Y03 유전·혼흔·현재 발현·인지 연결 | 부분 수용 | Typed frozen genetic model uses configured laws; actual body/soul/learning sources are separated and merged social/current-expression hooks consume source-backed effects once. **G03 G04** |
| V021-O01 계절·영맥·서식·자원 생태 | 부분 수용 | Automatic source/Qi/season/disaster/month history hooks and actual native self habitat/resource consumption source are now wired to existing authorities. **G03 G06** |
| V021-O02 생활·보관·운송·정착 순환 | 부분 수용 | Actual household consumption, deleted-stack audit, remote-loss witness filtering, storage/cargo and monthly nonhuman self physiology source are wired. **G03 G06** |
| V021-O03 지식 확산·제도 적응·실패 세계 | 부분 수용 | Actual teaching, witnessed social relay source, policy responses and ecology adaptation now use personal typed snapshots and real funded actions; no corrective hidden supply. **G01 G03 G06** |
| V021-H01 엔진 없는 C# 경계·초기 기준선 | 문서 근거 | Engine-free direction, real pre-extraction SHA/correctness/cost baseline and .NET10/API caveat are documented; Unity product generation is not a prerequisite. |
| V021-H02 플랫폼 독립 저장 계약·어댑터 | 실제 경로 근거 | Game now owns pure byte encode/decode/hash; Chronicles.Hosting owns GameSession/GameSaveStore/paths/atomic current-pending-backup I/O. Old Windows SQLite is physically isolated in legacy/windows-pre-v021. |
| V021-H03 호스트 중단/재개·checkpoint 복구 | 실제 경로 근거 | Authenticated pending/current/backup selection, interrupted/corrupt saves, host suspend/restore and replay sequence preserve actual commands/births and old originals. |
| V021-H04 사건·동적 SoA·지역/관계 인덱스 | 부분 수용 | Stable generations, alive/resident/dependent indexes, integer event boundaries and exact replay contracts exist in the current managed implementation. **G10** |
| V021-H05 기존 구조의 단계적 추출 | 실제 경로 근거 | Current product solution now builds Core/Game/Hosting and native/Shell adapters; Game is independent of host/file/engine types. Legacy products are isolated from default build with source-provenance hashes and manifest checks. |
| V021-H06 확장 성능 실험·지속 R&D 원장 | 최종 측정 대기 | Initial baseline and EX research intent remain source-backed; no external benchmark multiplier is presented as product improvement. **G10** |
| V021-H07 엔진 없는 UI 표시 모델·입력 경계 | 부분 수용 | Shared Korean HTML/input/DTO UI lives outside Game; Hosting owns serial command/session/save I/O and current product adapters reference this boundary. **G09** |
| V021-H08 향후 Unity 연결 설계·호환 근거 | 설계/판단 기록 | Current .NET10/C# latest use is disclosed; future .NETStandard2.1/C#9 candidate, platform/API isolation and source-vs-DLL/serializer constraints are documented. No candidate Unity-compatible compile or Unity runtime pass is claimed. Future selected-package/generated-code compatibility proof remains distinct from current engine-free delivery; Unity execution is not a0.2.1 blocker. |
| V021-H09 C# 배치·SIMD·네이티브 비교 판단 | 설계/판단 기록 | Managed scalar implementation is the declared reference. SIMD/native/IL2CPP/Burst are research candidates; no additional native language/backend is mandatory. **G10** |
| V021-J01 본혼/분혼·현생·생물학 계보 분리 | 실제 경로 근거 | Person/body/current birth and biological family DAG remain distinct from root/parent soul, actual independent splits and source eligibility snapshots. |
| V021-J02 화신 대원만 Gate·13개 자동 조건 | 실제 경로 근거 | All13 actual cause producers evaluate event-preceding current Gate/soul survival, never historical max realm; no manual reincarnation button or future deferral. |
| V021-J03 6영역 흔적·보존/접근/통합 | 실제 경로 근거 | All six preserved/accessed/integrated trace domains, crossing loss and real awakening/taught integration keep new body/mastery independent. |
| V021-J04 사망/비승/분혼·대기/출생 원자 전이 | 실제 경로 근거 | Actual death/ascension/split/waiting/birth assign a single soul and preserve costs/assets; ascension is not death and splits mint neither people labor nor cloned items. |
| V021-C01 게임명·설명·개발 문서 정합화 | 부분 수용 | Product title, VERSION0.2.1, migrated repository/readme and architecture boundary source agree on the current engine-free product. **G12** |
| V021-L02 14축 성격·도심·감정/습성 | 실제 경로 근거 | All fourteen axes enter actual candidate scores; current mood, completed habits and own active source-backed preferences change decisions. |
| V021-L03 AI 다양성·의사결정 비용 검증 | 최종 측정 대기 | Functional choice diversity and bounded actual candidate/expansion evidence exist. **G10** |
| V021-B03 저작/판본·매체·개인 지식 | 실제 경로 근거 | Works, immutable editions, actual media, personal adoption/misreading, actual teaching/copy inputs and corrections preserve dated source histories. |
| V021-B04 10경지 수행·30개 공법 예시 프로필 | 부분 수용 | Ten realm profiles, thirty paths, prepare/start/fracture, minor trials and adopted knowledge revisions are implemented. All thirty formation routes are covered by domain transitions; broad all-route world-command acceptance is not demonstrated by that module test alone. |
| V021-I01 16분류 자원·다양한 물품 정의 | 부분 수용 | All64 resource definitions have executable producer/consumer recipes and actual authoritative shared registry. Exhaustive64-resource executable proof is module-level; public advanced-recipe coverage is representative, not every vertical cycle. |
| V021-I02 인벤토리·용기·저물대 | 실제 경로 근거 | Real owner/custody/container/equipment/reservation identity and recursive capacity, non-merging books and overflow preserve physical stock; known bundles are filtered. |
| V021-I03 12분류 장비·본명법보 계약 | 부분 수용 | All12 categories and multi-slot one-cause effects, atomic replacement, binding and estate/soul contract state are implemented. All12 equipment and life-bound repair/rebirth combinations have domain proof; a complete public lifecycle combination case is not evidenced. |
| V021-K08 공유 정의·희소 지식/물품·지연 상세 | 부분 수용 | Shared definitions, immutable editions, actual item IDs, sparse personal memory and paged pure knowledge queries preserve RNG/state. **G05 G10** |
| V021-L04 인지·믿음·기억·실패 학습 | 부분 수용 | Dated source/people/place observations, confidence/expiry, actual failure contradiction correction and deterministic bounded cognition are saved. **G01 G10** |
| V021-L05 다단계 목표·계획·정보 탐색 | 실제 경로 근거 | Bounded actual dependencies, information search, teacher requests, reserved physical inputs and observed completion receipts drive replanning. |
| V021-L06 협상·가계/조직 협력·전투 의사결정 | 부분 수용 | Real obligations, independent teaching choice, shared labor and known-information retreat are used. **G03 G11** |
| V021-L07 AI 지능·학습·공정성 검증 | 부분 수용 | Public goals→information→acquire/learn→produce/trade, wrong-belief recovery, same-information noninterference and deterministic budgeted continuation pass eleven selected cases. **G10** |
| V021-R07 10대경지·30개 수행 경로와 형성 이력 | 부분 수용 | Thirty route definitions, formation/current separation, conversion costs, compatibility and founder provenance are live. **G10** |
| V021-R08 전투 특성·표시 전투력·조건부 잠재력 | 부분 수용 | Eight canonical axes, source deduplication, conditional potential and next-realm conversion are implemented; real attacks use canonical output/protection. **G05 G11** |
| V021-O04 가계·공방·도시·조직의 수요/행동 순환 | 부분 수용 | Automatic household/facility/demand settlement and actual hungry NPC purchase from another real owner now connect power/labor/knowledge/transport without auto unlock. **G06** |
| V021-O05 생태 충격·회복·사회 변화 시나리오 | 부분 수용 | Actual seasonal renewal, finite disaster loss and route closure/recovery public source now complements depletion, succession, bounded history and failure replanning. **G06 G10** |
| V021-U07 AI 판단·경로/잠재·생태 비교 화면 | 부분 수용 | Saved actual goal/candidate/expansion evidence and canonical path/potential/ecology source models are available. Rich canonical domain/query source and automatic social/ecology/currency hooks are now merged. **G05 G09** |
| V021-R09 창세 반선 구조 프로필 최소 계약 | 실제 경로 근거 | Founders use explicit Half Immortal structure/lifespan/fertility/ascension contracts, source-labeled as genesis, without fabricated past victories or breakthroughs. |
| V021-Z04 창세 성장 곡선 추정·프리셋 타당성 보고 | 최종 측정 대기 | Confirmed kin policy and both-Golden-Core default are explicit; provisional numbers remain developer-tuned rather than user facts. **G10** |
| V021-T07 조기 불변식·저장 왕복 하네스 | 실제 경로 근거 | Actual auto-discovered IFeatureScenario runner and INV-ID/cross-domain save/hash/RNG checks exist; proposed test names in the ledger are not treated as passes. |
| V021-H10 이전 경로 제거·이중 권위 정리 | 부분 수용 | Old Simulation/Queries/Runtime/Persistence/Presentation/Application/WPF workspace and tests are moved under legacy/windows-pre-v021 with original paths/commits/content SHA256. Current default solution and startup use only Core/Game/Hosting and present host adapters. **G08** |

## 구조·연구 항목의 해석

H04의 새 구현은 현재 관리 컬렉션·월 경계·안정 슬롯/지역/부양 인덱스다. 구획 SoA나 사건 힙을 이미 도입했다고 주장하지 않는다. 현재 채택하지 않은 힙 재구축·버퍼 대여 수명 시험을 새 기능 선행 조건으로 만들어서도 안 된다. 같은 사건/해시를 보존하는 비용/자료 배치 비교는 최종 연구 단계에 남는다.

H05는 새 Game→Core와 직렬 세션·플랫폼 어댑터, 도메인별 명시적 상태/전이 경계로 평가했다. 기획의 파일 배치 표를 이유로 프로젝트를 모두 다시 분리하라는 요구를 만들지 않는다. H10은 새 시작 경로와 fallback/mirror의 실제 쓰기 소유자를 감사하며, 이전 코드와 시험은 현재 legacy/windows-pre-v021로 격리되어 기본 제품 빌드에 들어오지 않는다. 별도 저장/회귀 기준선의 보존만으로 이중 권위라 부르지 않는다.

H08은 .NET10/C# 최신과 미래 Unity 후보 제약을 구별한다. Unity 실행이나 IL2CPP 통과 주장은 없다. H09는 관리 스칼라 정답을 선택했으며 네이티브 도입 의무가 없다. 향후 최적화 백엔드를 채택한다면 변환/복사 포함 비용·스칼라 동등성·오류/낡은 출력 폐기·단일 적용 증거가 그때 필요하다. EX01–06의 측정 통과는 현재 주장하지 않는다.

## 55개 V020 대응 보존

| 이전 ID | V021 대상 | 대상의 현재 감사 제안 |
| --- | --- | --- |
| V020-A01 | V021-A01 | 문서 근거 |
| V020-A02 | V021-A02, V021-A03 | 문서 근거, 부분 수용 |
| V020-A03 | V021-D02 | 최종 검증 대기 |
| V020-A04 | V021-A01, V021-T06 | 문서 근거, 최종 검증 대기 |
| V020-B01 | V021-B01 | 부분 수용 |
| V020-B02 | V021-B02 | 실제 경로 근거 |
| V020-C01 | V021-R01, V021-F03 | 실제 경로 근거, 부분 수용 |
| V020-C02 | V021-R02 | 실제 경로 근거 |
| V020-C03 | V021-R02, V021-R06 | 실제 경로 근거, 부분 수용 |
| V020-C04 | V021-R02 | 실제 경로 근거 |
| V020-C05 | V021-R03 | 부분 수용 |
| V020-C06 | V021-R02, V021-R06 | 실제 경로 근거, 부분 수용 |
| V020-C07 | V021-R03 | 부분 수용 |
| V020-D01 | V021-R03 | 부분 수용 |
| V020-E01 | V021-R05 | 실제 경로 근거 |
| V020-E02 | V021-R05 | 실제 경로 근거 |
| V020-E03 | V021-L01, V021-R05, V021-M04 | 실제 경로 근거 |
| V020-B03 | V021-V01 | 부분 수용 |
| V020-B04 | V021-V01 | 부분 수용 |
| V020-B05 | V021-V01 | 부분 수용 |
| V020-B06 | V021-V02 | 부분 수용 |
| V020-Q01 | V021-A03, V021-U03, V021-U04, V021-U05 | 부분 수용 |
| V020-F01 | V021-U03 | 부분 수용 |
| V020-F02 | V021-U03 | 부분 수용 |
| V020-F03 | V021-U02, V021-U03 | 부분 수용 |
| V020-F04 | V021-U04 | 부분 수용 |
| V020-F05 | V021-A02, V021-U01 | 문서 근거, 부분 수용 |
| V020-G01 | V021-D02 | 최종 검증 대기 |
| V020-G02 | V021-T03 | 최종 검증 대기 |
| V020-G03 | V021-T04 | 최종 측정 대기 |
| V020-G04 | V021-R06, V021-T04 | 부분 수용, 최종 측정 대기 |
| V020-G05 | V021-T06 | 최종 검증 대기 |
| V020-H01 | V021-W01 | 부분 수용 |
| V020-H02 | V021-W01 | 부분 수용 |
| V020-H03 | V021-W01 | 부분 수용 |
| V020-H04 | V021-W01, V021-U03 | 부분 수용 |
| V020-I01 | V021-W02 | 부분 수용 |
| V020-I02 | V021-W02, V021-K07 | 부분 수용 |
| V020-I03 | V021-W02, V021-S01 | 부분 수용, 실제 경로 근거 |
| V020-J01 | V021-P01, V021-P03 | 실제 경로 근거, 부분 수용 |
| V020-J02 | V021-F02, V021-P03 | 실제 경로 근거, 부분 수용 |
| V020-J03 | V021-P01, V021-P03 | 실제 경로 근거, 부분 수용 |
| V020-J04 | V021-U03, V021-U04, V021-D01 | 부분 수용 |
| V020-K01 | V021-M01 | 실제 경로 근거 |
| V020-K02 | V021-M02, V021-R05, V021-W02 | 부분 수용, 실제 경로 근거 |
| V020-K03 | V021-M03, V021-M04, V021-E03 | 실제 경로 근거 |
| V020-K04 | V021-U04 | 부분 수용 |
| V020-L01 | V021-L01 | 실제 경로 근거 |
| V020-L02 | V021-L01, V021-R02 | 실제 경로 근거 |
| V020-L03 | V021-L01, V021-V02, V021-U03 | 실제 경로 근거, 부분 수용 |
| V020-M01 | V021-R04, V021-A02 | 부분 수용, 문서 근거 |
| V020-M02 | V021-P01, V021-F02, V021-D02, V021-J02, V021-J04 | 실제 경로 근거, 최종 검증 대기 |
| V020-M03 | V021-F02, V021-F04, V021-Z01, V021-Z02 | 실제 경로 근거, 부분 수용 |
| V020-N01 | V021-T02 | 최종 검증 대기 |
| V020-N02 | V021-A02, V021-T06 | 문서 근거, 최종 검증 대기 |

총111개 ID, 원래107개+신규4개, 이전55개 대응과24개 INV 참조를 보존했다. 감사 JSON의 경로·시험 참조·개수·분류만 정적으로 검증했으며, 게임 시험 통과나 새 성능 측정으로 표시하지 않는다.
