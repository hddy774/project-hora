#!/usr/bin/env python3
"""Packaging evidence only: invoke frozen7ce's unchanged pure validator/aggregator; no world execution."""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import subprocess
import sys

SOURCE = '7ce66943b539420c9dc1e3fa8a611daae58020ff'
FINGERPRINT = '4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304'
SCRIPT_SHA = 'ed7d8a4f2f3ea6eb217b21602150aa1aa166600ac2810156c29dcab01163b32b'
OFFICIAL_SHA = 'bd592d544766dfff2ba39ece2a531513610ccca3b0c24b86061e9d9b0626e0ab'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def build(source_checkout, inputs, official_path, validated_cli_path, output, proof):
    source_checkout = source_checkout.resolve()
    inputs = inputs.resolve()
    output = output.resolve()
    proof = proof.resolve()
    if output.exists() or proof.exists() or output.parent != inputs or output.name != 'baseline.json':
        raise ValueError('새 baseline.json과 별도 증거 파일만 생성합니다. 기존 원문을 덮어쓰지 않습니다.')
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=source_checkout, text=True).strip() != SOURCE:
        raise ValueError('정확한7ce 체크아웃이 아닙니다.')
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=source_checkout, text=True).strip():
        raise ValueError('측정 코드 체크아웃이 dirty입니다.')
    script = source_checkout / 'scripts/aggregate-v021-growth.py'
    if digest(script.read_bytes()) != SCRIPT_SHA:
        raise ValueError('고정된 공식 통합기 소스가 다릅니다.')
    official_bytes = official_path.read_bytes()
    if digest(official_bytes) != OFFICIAL_SHA:
        raise ValueError('공식 집계 원문 SHA가 다릅니다.')
    spec = importlib.util.spec_from_file_location('frozen_growth_7ce', script)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    paths = [pathlib.Path(f'raw/{scenario}-{seed}.json') for scenario in 'abc' for seed in [42, 98, 2026]]
    for relative in paths:
        actual = inputs / relative
        if actual.is_symlink() or not actual.resolve().is_relative_to(inputs):
            raise ValueError('입력 묶음 안의 실제 상대 경로 파일만 사용합니다.')
    payloads = [(inputs / path).read_bytes() for path in paths]
    documents = [module.read(data) for data in payloads]
    fingerprint = module.VERIFY.fingerprint(SOURCE)
    if fingerprint != FINGERPRINT:
        raise ValueError('제품 지문이 고정7ce와 다릅니다.')
    laws = module.law_definitions(subprocess.check_output(
        ['git', 'show', SOURCE + ':src/Chronicles.Game/Laws.cs'], cwd=source_checkout, text=True))
    result = module.aggregate(documents, SOURCE, fingerprint, laws)
    # Same source metadata builder as frozen main302..305, using caller-supplied relative package paths.
    result['inputArtifacts'] = [
        {'path': path.as_posix(), 'sha256': digest(payload), 'environment': document['environment'],
         'cohortKeys': [list(module.key(row)) for row in document['reports']]}
        for path, payload, document in zip(paths, payloads, documents)
    ]
    prior = module.read(validated_cli_path.read_bytes())
    if {k: v for k, v in prior.items() if k != 'inputArtifacts'} != {
            k: v for k, v in result.items() if k != 'inputArtifacts'}:
        raise ValueError('CLI 검증 결과와 비경로 필드가 다릅니다.')
    if len(prior['inputArtifacts']) != 9:
        raise ValueError('CLI 검증 원본9개가 필요합니다.')
    for before, after in zip(prior['inputArtifacts'], result['inputArtifacts']):
        if {k: v for k, v in before.items() if k != 'path'} != {k: v for k, v in after.items() if k != 'path'}:
            raise ValueError('CLI 출처의 비경로 메타데이터가 다릅니다.')
    official = module.read(official_bytes)
    if result['reports'] != official['reports'] or result['distributions'] != official['distributions']:
        raise ValueError('공식9원문 관측행/분포와 다릅니다.')
    official_hashes = {tuple(row['cohortKeys'][0]): row['sha256'] for row in official['inputArtifacts']}
    new_hashes = {tuple(row['cohortKeys'][0]): row['sha256'] for row in result['inputArtifacts']}
    if official_hashes != new_hashes:
        raise ValueError('공식9원본 SHA가 다릅니다.')
    data = (json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode('utf-8')
    proof_data = {
        'scope': 'Unchanged frozen API validation; relative location metadata at NEW artifact creation. No JSON surgery/world/collector.',
        'sourceCommit': SOURCE, 'productSourceFingerprint': fingerprint,
        'aggregatorScriptSha256': SCRIPT_SHA, 'entryPoints': ['read', 'law_definitions', 'aggregate'],
        'officialAggregateSha256': OFFICIAL_SHA, 'validatedCliDerivedSha256': digest(validated_cli_path.read_bytes()),
        'portableBaselineSha256': digest(data), 'cohorts': 9,
        'observations': sum(len(row['timeSeries']) for row in result['reports']),
        'cliComparison': 'Every field identical except inputArtifacts[].path; every other artifact field identical.',
        'officialRowsEqual': True, 'officialDistributionsEqual': True, 'originalRawShaEqual': True,
        'rawBytesUnchanged': all((inputs / path).read_bytes() == payload for path, payload in zip(paths, payloads)),
        'newWorldOrCollectorExecutions': 0,
    }
    if not proof_data['rawBytesUnchanged']:
        raise ValueError('패키징 중 원본 byte가 바뀌었습니다.')
    proof.parent.mkdir(parents=True, exist_ok=True)
    with output.open('xb') as stream:
        stream.write(data)
    with proof.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(proof_data, ensure_ascii=False, indent=2) + '\n')
    return proof_data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-checkout', type=pathlib.Path, required=True)
    parser.add_argument('--inputs', type=pathlib.Path, required=True)
    parser.add_argument('--official-aggregate', type=pathlib.Path, required=True)
    parser.add_argument('--validated-cli', type=pathlib.Path, required=True)
    parser.add_argument('--proof', type=pathlib.Path, required=True)
    args = parser.parse_args()
    result = build(args.source_checkout, args.inputs, args.official_aggregate, args.validated_cli,
                   args.inputs / 'baseline.json', args.proof)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
