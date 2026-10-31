# V021-T04/Z04 동결 민감도 원문과 창세 성장 해석

## 배경·승인·출처

- 관련 작업은 기존 `V021-T04`, `V021-Z04`이며111개 목록에 새 항목을 추가하지 않는다. 상위 PR84, 추적 이슈212, 입력 PR213에 연결한다.
- 사용자는 혈연 허용·양친 금단을 고정했고 잠정값 보정을 개발에 위임했다. 통합 담당이 원형9검증 뒤 `cultivation.foundation=900/1500`, 6000개월/120간격/240분을 채택했다.
- 실제 작성자는 Codex다. 외부 Claude 검토를 수행했다고 표시하지 않는다. 현재 [개발 절차](../standards/development-process.md)와 [리뷰 구조](V021-REVIEW-STRUCTURE.md)를 따른다.
- 현재 통합 소스의 구조·SDK/CI 개선과 별개로 측정 대상은 정확한7ce/FP4e다. 이전 소스의 결과를 새 최종 소스 합격으로 승격하지 않는다.

## 범위와 소유

수정 범위는 이 명세와 `docs/evidence/0.2.1/growth-sensitivity-results-41131479924/`의 작은 보고·원문 metadata·SHA색인·읽기 전용 검증이다. Game/Core/하네스/입력/법칙/원장/Notion은 수정하지 않는다. 별도 근거 사전릴리스의 업로드 utility와 게시 여부는 통합 담당·people가 소유한다.

## 계약

| 항목 | 명시적 값 |
|---|---|
| 실제 소스 | `7ce66943b539420c9dc1e3fa8a611daae58020ff`, FP `4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304` |
| 입력 | 고정 계획SHA2533…/portable 기준선SHA02ab…/상한파일SHAacbf…; 정확한 전체SHA는 결과색인 |
| 명령 | 기존 `--filter INV-001/002/011 --sensitivity OUT --sensitivity-plan PLAN --sensitivity-seed SEED`; 실제 절대 경로 포함 argv는 원형 metadata |
| 조건 | 기본a 혈연 허용·양친 금단·유입0, 단일 잠정법칙900/1500, 실제 seed42/98/2026, 각6000개월·120간격 |
| 실행 예산 | 시드별 두 값 순차 관측, CLI240분; 상한을 넘긴 부분 자료는 실패로 유지 |
| 권위·정보 | 실제 World.Create/AdvanceTo 관측의 보정 자료. 보고서가 세계 상태·부모/혈연·권리·경제 장부를 새로 소유하지 않음 |
| 시간·RNG | 세계 월/Person.Born 차이/호스트 stopwatch/UI명목 환산을 분리. 보고 과정은 세계·RNG를 실행하거나 바꾸지 않음 |
| 실패 | 실제 exit124·partial·관측 마지막 월을 보존. 빈 값·미상·미성립·미완료를0이나성공으로 치환하지 않음 |
| 저장·재전송 | 원형ZIP byte/SHA·4DLL PE·입력/출처 보존. 보고 과정에 새 save/replay/복원 실행은 없음 |
| 호환 | 기존 입력·보고 schema와게이트 경로/수용 의미를 유지. 새 JSON은 행정용 색인·파생 요약이며 세계 저장/측정 게이트가 아님 |

## 관찰 가능한 수용 기준

1. 실제 run41131479924의 실패와4완주/2부분을 원문 source·종료·SHA로 구분한다. 6/6완료로 고쳐 쓰지 않는다.
2. 기본9와900/1500의 실제 성장 지표·관측 범위·개별 형성 기록을 비교한다.900무개선/후손Foundation0/1500검열과 시조 초기·상위목표 영향의 한계를 명시한다.
3. 몸나이가 없는 기본c2026/entity8042는 미상이다. 첫 출생인물2105의 월62를 다른 사람의 생일로 사용하지 않는다.
4. 실제 checkpoint 정책과 선언catalog를 분리한다. 혈연/양친 정책·새 인물/무료 재원이나 성공 목표를 만들지 않는다.
5. [보관 규칙](../standards/evidence-retention.md)에 따라 큰 원문ZIP6개는 별도 근거 자산으로 보관하고 실제 URL/ID가 확인되기 전에는 영속 보관 완료로 표시하지 않는다.

## 검사·증거·리뷰

```text
python3 docs/evidence/0.2.1/growth-sensitivity-results-41131479924/verify-report.py
python3 docs/evidence/0.2.1/growth-sensitivity-results-41131479924/verify-report.py --archives /workspace/scratch/sensitivity-41131479924-7ce
```

이 명령은 파일·원형ZIP을 읽으며 새 C# 세계/성능/성장 실행을 하지 않는다. 실제 4DLL custom attribute 검증과 완주/부분 감사는 결과 폴더의 작은 검증 원문에 있다. 큰 ZIP의 실제 영속 업로드·다운로드 검증은 별도 utility의 index에 연결한다.

Codex 자체 검토는 출처/byte·실패·검열·개인Born·권한 및 시간 비간섭·새 성공기준 부재를 확인한다. 보고서의 수용과 기존111개 원장/밸런스/최종소스/릴리스 수용은 통합 담당이 별도로 판정한다. 새 실행·조건 변경과 제품 기본값의 채택은 이 문서가 승인하지 않는다.
