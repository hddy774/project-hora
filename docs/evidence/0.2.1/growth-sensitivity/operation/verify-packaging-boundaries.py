#!/usr/bin/env python3
"""Pure file/packaging contracts on copies of actual primary data. No world or collector execution."""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import shutil
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-checkout', type=pathlib.Path, required=True)
    parser.add_argument('--inputs', type=pathlib.Path, required=True)
    parser.add_argument('--official-aggregate', type=pathlib.Path, required=True)
    parser.add_argument('--validated-cli', type=pathlib.Path, required=True)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('portable_adapter', pathlib.Path(__file__).with_name('build-portable-baseline.py'))
    adapter = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(adapter)
    digest = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
    original_hashes = {p.name: digest(p) for p in (args.inputs / 'raw').glob('*.json')}
    official_hash = digest(args.official_aggregate)
    with tempfile.TemporaryDirectory(prefix='portable-relocation-') as temporary:
        relocated = pathlib.Path(temporary) / 'different-root' / 'inputs'
        shutil.copytree(args.inputs / 'raw', relocated / 'raw')
        baseline = json.loads((args.inputs / 'baseline.json').read_text())
        assert len(baseline['inputArtifacts']) == 9
        for artifact in baseline['inputArtifacts']:
            path = pathlib.Path(artifact['path'])
            assert not path.is_absolute() and (relocated / path).resolve().is_relative_to(relocated.resolve())
            assert digest(relocated / path) == artifact['sha256']
    mutations = {
        'missing-one-raw': lambda root: (root / 'raw/a-42.json').unlink(),
        'partial-original': lambda root: alter(root / 'raw/a-42.json', lambda r: r.update(requested_complete=False)),
        'different-source': lambda root: alter(root / 'raw/a-42.json', lambda r: r.update(sourceCommit='f' * 40)),
        'duplicate-cohort': lambda root: shutil.copyfile(root / 'raw/a-42.json', root / 'raw/c-42.json'),
        'empty-observations': lambda root: alter(root / 'raw/a-42.json', lambda r: r['reports'][0].update(timeSeries=[])),
        'existing-output-protected': lambda root: (root / 'baseline.json').write_bytes(b'KEEP ORIGINAL\n'),
        'symlink-outside-package': lambda root: symlink(root / 'raw/a-42.json', (args.inputs / 'raw/a-42.json').resolve()),
    }
    rejected = []
    for name, mutation in mutations.items():
        with tempfile.TemporaryDirectory(prefix='portable-negative-') as temporary:
            root = pathlib.Path(temporary) / 'inputs'
            shutil.copytree(args.inputs / 'raw', root / 'raw')
            mutation(root)
            output = root / 'baseline.json'
            before = output.read_bytes() if output.exists() else None
            try:
                adapter.build(args.source_checkout, root, args.official_aggregate, args.validated_cli, output, pathlib.Path(temporary) / 'proof.json')
            except (ValueError, KeyError, TypeError, OSError):
                assert (output.read_bytes() if output.exists() else None) == before
                rejected.append(name)
            else:
                raise AssertionError('잘못된 입력이 통과했습니다: ' + name)
    assert {p.name: digest(p) for p in (args.inputs / 'raw').glob('*.json')} == original_hashes
    assert digest(args.official_aggregate) == official_hash
    print(json.dumps({'scope': 'file packaging only; actual source observations not regenerated', 'relocatedNinePathsResolve': True,
                      'negativeCasesRejected': rejected, 'originalRawAndOfficialBytesUnchanged': True,
                      'newWorldOrCollectorExecutions': 0}, ensure_ascii=False))


def alter(path, change):
    data = json.loads(path.read_text())
    change(data)
    path.write_text(json.dumps(data))


def symlink(path, destination):
    path.unlink()
    path.symlink_to(destination)


if __name__ == '__main__':
    main()
