"""보존 원문만 읽는 AI 측정 요약기. 게임·seed·법칙·원문은 바꾸지 않는다."""
from __future__ import annotations
import collections
import gzip
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent

def read_raw(name):
    with gzip.open(ROOT / name, 'rb') as stream:
        raw = stream.read()
    return json.loads(raw), hashlib.sha256(raw).hexdigest()

def write(name, value):
    (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

def key(row):
    s = row['specification']
    return tuple(s.get(k) for k in ('family', 'seed', 'budget', 'repetition', 'axis'))

def choice_sequence(row):
    return [(d['person'], d['decision']['month'], d['decision']['goal'], d['decision']['action']) for d in row['decisions']]

def hashes_equal(left, right):
    return (left['initialHash'] == right['initialHash'] and left['finalHash'] == right['finalHash'] and
            [(s['month'], s['hash']) for s in left['snapshots']] == [(s['month'], s['hash']) for s in right['snapshots']])

def merge_counts(rows, name):
    values = collections.Counter()
    for row in rows:
        values.update(row[name])
    return dict(sorted(values.items()))

def distribution(values):
    values = sorted(values)
    if not values:
        return None
    def percentile(p):
        return values[max(0, math.ceil(p * len(values)) - 1)]
    return {'samples': len(values), 'minimum': values[0], 'p50': percentile(.50),
            'p95': percentile(.95), 'maximum': values[-1], 'mean': sum(values) / len(values)}

def main():
    data, raw_hash = read_raw('restart1.raw.json.gz')
    old, old_hash = read_raw('interrupted.raw.json.gz')
    plan_bytes = (ROOT / 'plan.json').read_bytes()
    plan = json.loads(plan_bytes)
    rows = data['reports']
    errors = []
    def check(condition, description):
        if not condition:
            errors.append(description)
    archived = {a['path']: a for a in json.loads((ROOT / 'archive-verification.json').read_text())['artifacts']}
    check(raw_hash == archived['restart1.raw.json.gz']['uncompressedSha256'], '완료 원문 SHA 불일치')
    check(old_hash == archived['interrupted.raw.json.gz']['uncompressedSha256'], '중단 원문 SHA 불일치')
    expected = []
    maximum = max(plan['planningBudgets'])
    for seed in plan['seeds']:
        for repetition in range(plan['repetitions']):
            expected.append(('axes', seed, maximum, repetition, None))
            expected.extend(('axes', seed, maximum, repetition, axis) for axis in range(14))
            for budget in plan['planningBudgets']:
                expected.extend((family, seed, budget, repetition, None) for family in ('paper', 'tool', 'ecology'))
            expected.extend((family, seed, maximum, repetition, None) for family in ('correction', 'cooperation', 'fairness'))
    check([key(r) for r in rows] == expected, '계획 순서/사례 집합 불일치')
    check(len({key(r) for r in rows}) == len(rows), '중복 사례')
    check(data['executionStatus'] == 'completed' and data['failedCases'] == 0, '생산자 미완료/오류')
    check(data['sourceCommit'] == plan['sourceCommit'], 'sourceCommit 불일치')
    check(data['planSha256'] == hashlib.sha256(plan_bytes).hexdigest(), '계획 SHA 불일치')
    for row in rows:
        label = str(key(row))
        check(not row.get('collectionFailed'), label + ': 수집 예외')
        horizon = plan['horizonMonths']
        months = list(range(row['initialMonth'] + 1, row['initialMonth'] + horizon + 1))
        check(row['horizonMonths'] == horizon and row['finalMonth'] - row['initialMonth'] == horizon, label + ': 기간')
        check([s['month'] for s in row['snapshots']] == months, label + ': 월 해시 누락')
        check([s['month'] for s in row['rawLatencySamples']] == months, label + ': 지연 표본 누락')
        check(all(e <= row['specification']['budget'] for e in row['decisionExpansions']), label + ': 실제 탐색 예산')
        check(all(s['actualBudget'] == row['specification']['budget'] for s in row['rawLatencySamples']), label + ': 법칙 예산')
        check(row['replay']['finalHash'] == row['finalHash'], label + ': 복제 최종 해시')
        check(row['decisionExpansions'] == [d['decision']['expansions'] for d in row['decisions']], label + ': 탐색 표본 배열')
        check(sum(s['newDecisions'] for s in row['rawLatencySamples']) == len(row['decisions']), label + ': 결정 표본 수')
        check(row['measuredContracts']['budgetBounded'] and row['measuredContracts']['replayEqual'], label + ': 예산/재현 계약')
        check(row['measuredContracts']['hiddenEqual'] is not False, label + ': 정보 동등성')
        if row['goalCompletedWithinHorizon']:
            costs = row['firstTargetCosts']
            check(costs is not None and costs['targetReceipt'] in row['goalReceipts'], label + ': 실제 목표 영수증')
    by_key = {key(row): row for row in rows}
    pairs = []
    for row in rows:
        s = row['specification']
        if s['repetition'] != 0:
            continue
        counterpart = by_key[(s['family'], s['seed'], s['budget'], 1, s.get('axis'))]
        pairs.append({'specificationWithoutRepetition': {k: v for k, v in s.items() if k != 'repetition'},
                      'initialAndEveryMonthlyAndFinalHashEqual': hashes_equal(row, counterpart),
                      'selectedDecisionSequenceEqual': choice_sequence(row) == choice_sequence(counterpart)})
    overlap = [{'specification': row['specification'], 'initialAndEveryMonthlyAndFinalHashEqual': hashes_equal(row, by_key[key(row)])}
               for row in old['reports']]
    check(all(p['initialAndEveryMonthlyAndFinalHashEqual'] for p in pairs), '반복 간 해시 불일치')
    check(all(p['initialAndEveryMonthlyAndFinalHashEqual'] for p in overlap), '중단 원문과 해시 불일치')
    axes = []
    for item in data['axisCausalComparisons']:
        row = by_key[('axes', item['seed'], maximum, item['repetition'], item['axis'])]
        baseline = by_key[('axes', item['seed'], maximum, item['repetition'], None)]
        axes.append({**item,
                     'selectedDecisionSequenceEqualToBaseline': choice_sequence(row) == choice_sequence(baseline),
                     'actionDistributionEqualToBaseline': row['actionDistribution'] == baseline['actionDistribution'],
                     'goalDistributionEqualToBaseline': row['goalDistribution'] == baseline['goalDistribution']})
    frequency = data['runtime']['stopwatchFrequency']
    groups = []
    for family, budget in sorted({(r['specification']['family'], r['specification']['budget']) for r in rows}):
        selected = [r for r in rows if r['specification']['family'] == family and r['specification']['budget'] == budget]
        samples = [s for r in selected for s in r['rawLatencySamples']]
        targets = [r for r in selected if r['expectedGoal'] is not None]
        actions = merge_counts(selected, 'actionDistribution')
        decision_count = sum(actions.values())
        groups.append({'family': family, 'budget': budget, 'cases': len(selected),
            'declaredTargets': len(targets), 'completedTargets': sum(r['goalCompletedWithinHorizon'] is True for r in targets),
            'uncompletedTargets': sum(r['goalCompletedWithinHorizon'] is False for r in targets),
            'stagnationCandidates': sum(r['stagnationCandidate'] for r in selected),
            'firstCompletionMonthsAfterPreparation': [r['firstTargetCosts']['elapsedMonths'] for r in selected if r['firstTargetCosts'] is not None],
            'actionDistribution': actions, 'goalDistribution': merge_counts(selected, 'goalDistribution'),
            'decisionCount': decision_count,
            'mostFrequentAction': max(actions, key=actions.get) if actions else None,
            'mostFrequentActionDecisionShare': max(actions.values()) / decision_count if decision_count else None,
            'occupiedPersonMonthsByAction': merge_counts(selected, 'occupiedPersonMonthsByAction'),
            'consumedByDefinition': merge_counts(selected, 'consumedByDefinition'),
            'completedOutputsByDefinition': merge_counts(selected, 'completedOutputsByDefinition'),
            'failureDistribution': merge_counts(selected, 'failureDistribution'),
            'budgetDeferrals': sum(len(r['budgetDeferrals']) for r in selected),
            'decisionsAtBudgetLimit': sum(r['decisionsAtBudgetLimit'] for r in selected),
            'advanceMilliseconds': distribution([s['advanceTicks'] * 1000 / frequency for s in samples]),
            'currentThreadAllocatedBytes': distribution([s['advanceAllocatedBytes'] for s in samples])})
    cases = []
    for row in rows:
        cases.append({k: row[k] for k in ('specification', 'initialMonth', 'finalMonth', 'initialHash', 'finalHash',
            'expectedGoal', 'goalCompletedWithinHorizon', 'firstTargetCosts', 'goalReceipts', 'failureDistribution',
            'beliefCorrection', 'actionDistribution', 'goalDistribution', 'dominantActionShare', 'distinctSelectedActions',
            'occupiedPersonMonthsByAction', 'nonWaitingPersonMonths', 'completedPaidJobsPerNonWaitingPersonMonth',
            'consumedByDefinition', 'completedOutputsByDefinition', 'actualPayments', 'maxConsecutiveWaitingWithoutPaidProcess',
            'stagnationCandidate', 'measuredContracts')})
    summary = {
        'schemaVersion': 1, 'kind': '보존 AI 원문 독립 요약', 'measurementSource': data['sourceCommit'],
        'mainRawSha256': raw_hash, 'interruptedRawSha256': old_hash, 'planSha256': data['planSha256'],
        'runtime': data['runtime'], 'plan': plan, 'executionStatus': data['executionStatus'],
        'plannedCases': len(expected), 'collectedUniqueCases': len(by_key), 'producerFailedCases': data['failedCases'],
        'independentAuditErrors': errors, 'familyCounts': dict(collections.Counter(r['specification']['family'] for r in rows)),
        'monthlyAdvanceSamples': sum(len(r['rawLatencySamples']) for r in rows),
        'monthlyStateHashes': sum(len(r['snapshots']) for r in rows),
        'allCasesUsedLawCount': sorted({len(r['actualLaws']) for r in rows}),
        'declaredTargets': sum(r['expectedGoal'] is not None for r in rows),
        'completedTargets': sum(r['goalCompletedWithinHorizon'] is True for r in rows),
        'uncompletedTargets': sum(r['goalCompletedWithinHorizon'] is False for r in rows),
        'notApplicableTargets': sum(r['goalCompletedWithinHorizon'] is None for r in rows),
        'stagnationCandidates': sum(r['stagnationCandidate'] for r in rows),
        'axisComparisons': len(axes), 'axisScoreCausalityObserved': sum(a['scoreCausalityObserved'] for a in axes),
        'axisSelectedDecisionSequencesChanged': sum(not a['selectedDecisionSequenceEqualToBaseline'] for a in axes),
        'axisActionDistributionsChanged': sum(not a['actionDistributionEqualToBaseline'] for a in axes),
        'repetitionPairs': len(pairs), 'repetitionHashMismatches': sum(not p['initialAndEveryMonthlyAndFinalHashEqual'] for p in pairs),
        'interruptedOverlapCases': len(overlap), 'interruptedOverlapMismatches': sum(not p['initialAndEveryMonthlyAndFinalHashEqual'] for p in overlap),
        'fairnessCases': sum(r['fairness']['compared'] for r in rows),
        'fairnessMismatches': sum(r['measuredContracts']['hiddenEqual'] is False for r in rows),
        'budgetMismatches': sum(not r['measuredContracts']['budgetBounded'] for r in rows),
        'replayMismatches': sum(not r['measuredContracts']['replayEqual'] for r in rows),
        'groups': groups,
        'uncompletedCases': [r['specification'] for r in rows if r['goalCompletedWithinHorizon'] is False],
        'stagnationCases': [{'specification': r['specification'], 'maximumConsecutiveMonths': r['maxConsecutiveWaitingWithoutPaidProcess']}
                            for r in rows if r['stagnationCandidate']],
        'correctionCases': [{'specification': r['specification'], 'actualPreparationMonths': r['fixture'].get('actualPreparationMonths'),
                             'correction': r['beliefCorrection'], 'firstTargetCosts': r['firstTargetCosts']}
                            for r in rows if r['specification']['family'] == 'correction'],
        'cooperationCases': [{'specification': r['specification'], 'actualPayments': r['actualPayments']}
                             for r in rows if r['specification']['family'] == 'cooperation'],
        'livingCountScope': '원문 snapshots.living은 매월 전체 생존자 수다. 개별 사망 총수나 동월 출생/사망 상쇄를 따로 복원하지 않는다.',
        'latencyScope': '실제 공개 AdvanceTo 월 경계 전체 wall ticks/현재 스레드 할당. 순수 추론 latency·exclusive CPU가 아니다. 준비/예열/조회/복제비교/보고서저장은 이 구간 밖이다.',
        'quantileMethod': '각 family/budget의 원시 월 표본을 정렬한 최근접 상위 순위 ceil(p*N)-1. 두 반복은 결정론 대조이며 독립 seed 네 개로 세지 않는다.',
        'censorScope': '미완수는 고정144개월에서 종료한 결과이며 성공시간으로 대체하지 않는다. 정체 후보는 유한 연속대기 관측이며 영구교착·최적전략을 단정하지 않는다.'
    }
    write('summary.json', summary)
    write('case-metrics.json', cases)
    write('axis-comparisons.json', axes)
    write('repetition-and-overlap.json', {'repetitionPairs': pairs, 'interruptedOverlap': overlap})
    print(json.dumps({k: summary[k] for k in ('collectedUniqueCases', 'independentAuditErrors', 'completedTargets',
          'uncompletedTargets', 'stagnationCandidates', 'axisScoreCausalityObserved', 'axisSelectedDecisionSequencesChanged',
          'repetitionPairs', 'repetitionHashMismatches', 'fairnessCases', 'interruptedOverlapMismatches')}, ensure_ascii=False))
    if errors:
        raise SystemExit(1)

if __name__ == '__main__':
    main()
