#!/usr/bin/env python3
"""Execute the actual workflow shell block with explicit fake host/device commands."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import yaml

ROOT = Path(__file__).resolve().parents[4]
workflow = yaml.load((ROOT / '.github/workflows/v021-api24-probe.yml').read_text(), Loader=yaml.BaseLoader)
block = next(s['run'] for s in workflow['jobs']['api24']['steps'] if s['name'] == 'GitHub 러너의 실제 하드웨어 가속 확인(KVM)')
STUBS = r'''
record() {
  printf '%s\n' "$1" >> "$TRACE"
  if [ "$FAIL_STAGE" = "$1" ]; then return "$FAIL_CODE"; fi
}
test() {
  case "$*" in
    '-c /dev/kvm') record character-device ;;
    '-r /dev/kvm -a -w /dev/kvm') record access ;;
    *) builtin test "$@" ;;
  esac
}
sudo() {
  case "$*" in
    'tee /etc/udev/rules.d/99-kvm4all.rules') cat > /dev/null; record rule ;;
    'udevadm control --reload-rules') record reload ;;
    'udevadm trigger --name-match=kvm') record trigger ;;
    'udevadm settle --timeout=10') record settle ;;
    *) printf 'Unexpected sudo command: %s\n' "$*"; return 99 ;;
  esac
}
id() { printf 'fixture id\n'; printf 'id\n' >> "$TRACE"; }
stat() { printf 'fixture stat\n'; printf 'stat\n' >> "$TRACE"; }
'''
CASES = [('success', '', 0, 'true', 'github-hosted'),
         ('github-actions', '', 1, 'false', 'github-hosted'),
         ('hosted', '', 1, 'true', 'self-hosted'),
         ('character-device', 'character-device', 1, 'true', 'github-hosted'),
         ('reload', 'reload', 7, 'true', 'github-hosted'),
         ('trigger', 'trigger', 8, 'true', 'github-hosted'),
         ('settle', 'settle', 9, 'true', 'github-hosted'),
         ('access', 'access', 1, 'true', 'github-hosted')]
rows = []
for name, failed, expected, gha, hosted in CASES:
    with tempfile.TemporaryDirectory() as temporary:
        trace = Path(temporary) / 'trace'
        env = dict(os.environ, TRACE=str(trace), FAIL_STAGE=failed, FAIL_CODE=str(expected),
                   GITHUB_ACTIONS=gha, RUNNER_ENVIRONMENT=hosted)
        result = subprocess.run(['bash', '-c', STUBS + '\n' + block], capture_output=True, text=True, env=env, timeout=3)
        assert result.returncode == expected, (name, result.stdout, result.stderr)
        assert f'KVM final exit={expected}\n' in result.stdout
        observed = trace.read_text().splitlines()
        assert observed[-2:] == ['id', 'stat']
        expected_sequence = ['character-device', 'rule', 'reload', 'trigger', 'settle', 'access']
        if name in ('github-actions', 'hosted'):
            expected_sequence = []
        elif failed:
            expected_sequence = expected_sequence[:expected_sequence.index(failed) + 1]
        assert observed[:-2] == expected_sequence, (name, observed)
        assert f'KVM stage={name} exit={expected}\n' in result.stdout if expected else 'KVM stage=access exit=0\n' in result.stdout
        rows.append({'case': name, 'expectedExit': expected, 'actualExit': result.returncode,
                     'observedCommands': observed, 'stdout': result.stdout, 'stderr': result.stderr})
print(json.dumps({'passed': len(rows), 'cases': rows, 'realUdevOrKvmOperations': 0,
                  'scope': '선언한 가짜 host/device 명령으로 실제 workflow shell의 가드·실패 전파·순서만 검증'}, ensure_ascii=False, indent=2))
