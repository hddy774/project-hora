"""검토 후에만 실행하는 재시도 없는 private 재개 진단 supervisor."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

PROJECT = Path(__file__).resolve().parent
SOURCE = Path('/workspace/scratch/v021-three-case-diagnostic-7901f34/result-saves')
ASSEMBLIES = PROJECT / 'bin/Release/net10.0'
CAP_SECONDS = 1575  # 원래 N1000 사례의 상한을 외부 중단 한도로만 사용. 정식 시간 판정은 하지 않는다.
EXPECTED = {
    'Chronicles.Core.dll': '19470b906faf107d42e9b728ed757f95112be70d14e1e19ea20c8b9351229a24',
    'Chronicles.Game.dll': 'fcec1a70b238a22b7aa3ef53902f0916ae0b2ccb38744be0a31bb7174e927364',
    'Chronicles.Hosting.dll': 'cbc7b6f3f74e254adf3d1558182be595f7bec218e7bc723ae2834c7a1004ce79',
}


def stamp():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def save():
    temporary = directory / 'supervisor.json.pending'
    temporary.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + '\n')
    temporary.replace(directory / 'supervisor.json')


if len(sys.argv) != 2:
    raise SystemExit('존재하지 않는 새 supervisor 출력 폴더 하나를 지정하세요.')
directory = Path(sys.argv[1]).resolve()
if directory.exists() or directory == SOURCE or SOURCE in directory.parents or directory in SOURCE.parents:
    raise SystemExit('원본과 분리된 존재하지 않는 출력 폴더만 허용합니다.')
if os.getsid(0) != os.getpid():
    raise SystemExit('supervisor 자체도 setsid로 분리해서 실행하세요.')
for name, expected in EXPECTED.items():
    with (ASSEMBLIES / name).open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual != expected:
        raise SystemExit('기존7901 DLL 사본과 다릅니다: ' + name)
runtime = json.loads((ASSEMBLIES / 'SaveGrowthResumeProbe.runtimeconfig.json').read_text())
if runtime['runtimeOptions']['configProperties'].get('System.Runtime.TieredPGO') is not False:
    raise SystemExit('helper의 TieredPGO=false 설정을 확인하세요.')
overrides = {k: os.environ.get(k) for k in
             ['DOTNET_TieredPGO', 'COMPlus_TieredPGO', 'DOTNET_TieredCompilation', 'COMPlus_TieredCompilation']}
if any(v is not None for v in overrides.values()):
    raise SystemExit('기존과 다른 tiering 환경 override를 제거한 뒤 검토하세요.')
command = ['taskset', '-c', '0', '/workspace/.dotnet/dotnet', 'exec', '--fx-version', '10.0.14',
           str(ASSEMBLIES / 'SaveGrowthResumeProbe.dll'), str(SOURCE), str(directory / 'resume-world')]
directory.mkdir(parents=True, exist_ok=False)
meta = {'schema': 'seonro-resume-supervisor-control.v1', 'status': 'starting',
        'started_utc': stamp(), 'supervisor_pid': os.getpid(), 'supervisor_sid': os.getsid(0),
        'command': command, 'cap_seconds': CAP_SECONDS, 'maximum_attempts': 1,
        'formal_t04_eligible': False, 'source_assemblies_sha256': EXPECTED,
        'tiered_environment': overrides,
        'scope': '1회30월 재개 파일 증가 진단의 프로세스 제어만 기록한다. 시간/힙/전체120개월/원격T04 합격 근거가 아니다.'}
save()
started = time.monotonic()
with (directory / 'resume.log').open('wb') as log:
    child = subprocess.Popen(command, cwd=PROJECT, stdin=subprocess.DEVNULL,
                             stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    meta.update(status='running', child_pid=child.pid, child_pgid=child.pid, child_sid=child.pid)
    save()
    reached_cap = False
    while child.poll() is None:
        if 'actual_runtime_mapped_files' not in meta:
            try:
                paths = sorted({line.split()[-1] for line in Path(f'/proc/{child.pid}/maps').read_text().splitlines()
                                if '/Microsoft.NETCore.App/' in line})
                if any('/10.0.14/libcoreclr.so' in p for p in paths):
                    meta['actual_runtime_mapped_files'] = paths
                    save()
            except FileNotFoundError:
                pass
        if time.monotonic() - started >= CAP_SECONDS:
            reached_cap = True
            meta['status'] = 'external_cap_stopping_private_group'
            save()
            try:
                os.killpg(child.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                try:
                    os.killpg(child.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            break
        time.sleep(0.25)
    try:
        code = child.wait(timeout=5 if reached_cap else None)
    except subprocess.TimeoutExpired:
        code = None
        meta['private_group_exit_not_observed_after_kill'] = True
meta.update(status='external_cap' if reached_cap else 'process_exited', child_exit_code=code,
            ended_utc=stamp(), external_cap_reached=reached_cap,
            supervisor_control_elapsed_seconds=time.monotonic() - started)
save()
raise SystemExit(124 if reached_cap or code is None else code if code >= 0 else 128 - code)
