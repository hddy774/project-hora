from pathlib import Path
import datetime, hashlib, json, os, selectors, signal, subprocess, time

work = Path('/workspace/scratch/t04-retained-heap-plan-5398700')
probe = work / 'probe/bin/Release/net10.0/HeapProbe.dll'
tests = Path('/workspace/seonro-integration-df72405/tests/Chronicles.Game.Tests/bin/Release/net10.0/Chronicles.Game.Tests.dll')
dump = work / 'tools/dotnet-dump'
env = {key: os.environ[key] for key in ['PATH', 'LANG', 'LC_ALL', 'TMPDIR', 'TZ'] if key in os.environ}
env.update(DOTNET_ROOT='/workspace/.dotnet', DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
started = time.monotonic()
deadline = started + 540
records = []

def remaining():
    value = deadline - time.monotonic()
    if value <= 0:
        raise TimeoutError('fixed diagnostic deadline')
    return value

for mode in ['fresh', 'sequential']:
    output = work / ('actual-' + mode)
    output.mkdir(exist_ok=False)
    command = ['taskset', '-c', '2', '/workspace/.dotnet/dotnet', str(probe), mode, str(output), str(tests)]
    child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, stdin=subprocess.PIPE, text=True, env=env, start_new_session=True)
    selector = selectors.DefaultSelector()
    selector.register(child.stdout, selectors.EVENT_READ)
    log = []
    try:
        while child.poll() is None:
            if not selector.select(timeout=min(1, remaining())):
                continue
            line = child.stdout.readline()
            if not line:
                continue
            log.append(line)
            (output / 'child.log').write_text(''.join(log))
            if not line.startswith('MARKER '):
                continue
            marker = json.loads(line[7:])
            records.append(marker)
            print(json.dumps(marker), flush=True)
            (work / 'markers.partial.json').write_text(json.dumps(records, indent=2) + '\n')
            if marker['phase'] == 'pre-snapshot-opening':
                target = output / 'opening.dmp'
                with (output / 'dump-collect.log').open('w') as handle:
                    result = subprocess.run(['taskset', '-c', '2', str(dump), 'collect', '--process-id', str(child.pid), '--type', 'Heap', '--output', str(target)], env=env, stdout=handle, stderr=subprocess.STDOUT, timeout=min(100, remaining()))
                marker['dumpExit'] = result.returncode
                marker['dumpBytes'] = target.stat().st_size if target.exists() else None
                print(mode + ' dump exit=' + str(result.returncode), flush=True)
            child.stdin.write('continue\n')
            child.stdin.flush()
        log.extend(child.stdout.readlines())
        (output / 'child.log').write_text(''.join(log))
        records.append({'mode': mode, 'exitCode': child.wait()})
        print(mode + ' child exit=' + str(child.returncode), flush=True)
    except BaseException as error:
        os.killpg(child.pid, signal.SIGKILL)
        child.wait()
        records.append({'mode': mode, 'error': type(error).__name__})
        raise
    finally:
        (work / 'markers.partial.json').write_text(json.dumps(records, indent=2) + '\n')
(work / 'markers.json').write_text(json.dumps(records, indent=2) + '\n')
print('DIAGNOSTIC COLLECTION COMPLETE; no acceptance claim', flush=True)
