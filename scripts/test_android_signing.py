"""Offline signing-contract tests. All keytool, randomness, HTTP, and signing are mocked."""
import argparse
import base64
from contextlib import redirect_stdout
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import types
import unittest
from unittest.mock import patch
import android_signing as signer

ROOT = Path(__file__).resolve().parents[1]
CERT = b'PUBLIC-CERTIFICATE-TEST-FIXTURE'
PIN = hashlib.sha256(CERT).hexdigest()
FAKE_KEY = b'NOT-A-REAL-KEY'
ENCODED = base64.b64encode(FAKE_KEY).decode()
FAKE_PASSWORD = 'not-a-real-password-fixture'


def bundle(**overrides):
    value = dict(schemaVersion=1, game='alpha-exchange', applicationId='com.alphaexchange.offline',
                 keystoreBase64=ENCODED, storePassword=FAKE_PASSWORD, keyAlias='fixture-alias',
                 keyPassword=FAKE_PASSWORD, certificateSha256=PIN)
    value.update(overrides)
    return json.dumps(value)


def environment(**overrides):
    value = dict(GAME='alpha-exchange', APP_ID='com.alphaexchange.offline', EXPECTED_SIGNING_CERTIFICATE=PIN, SIGNING_BUNDLE=bundle())
    value.update(overrides)
    return value


def bootstrap_module():
    workflow = (ROOT / '.github/workflows/bootstrap-android-signing.yml').read_text()
    source = workflow.split("          python3 - <<'PY_BOOTSTRAP'\n", 1)[1].split('          PY_BOOTSTRAP\n', 1)[0]
    source = '\n'.join(line[10:] for line in source.splitlines())
    module = types.ModuleType('bootstrap_draft_test')
    exec(compile(source, '<bootstrap-workflow>', 'exec'), module.__dict__)
    return module


class IdentityTests(unittest.TestCase):
    def test_bundle_and_legacy_load(self):
        data, _, pin, _ = signer.load_identity(environment())
        self.assertEqual((data, pin), (FAKE_KEY, PIN))
        data, _, pin, _ = signer.load_identity(environment(SIGNING_BUNDLE='', KEYSTORE_BASE64=ENCODED, STORE_PASSWORD=FAKE_PASSWORD, KEY_PASSWORD=FAKE_PASSWORD, KEY_ALIAS='fixture'))
        self.assertEqual((data, pin), (FAKE_KEY, PIN))

    def test_invalid_and_ambiguous_identities_fail(self):
        cases = [environment(GENERATE_SIGNING_KEY='true'), environment(EXPECTED_SIGNING_CERTIFICATE=''),
                 environment(KEY_ALIAS='existing'), environment(SIGNING_BUNDLE=bundle(game='other-game')),
                 environment(SIGNING_BUNDLE=bundle(applicationId='other.app')), environment(SIGNING_BUNDLE=bundle(certificateSha256='f'*64)),
                 environment(SIGNING_BUNDLE=bundle(keystoreBase64='invalid!')), environment(SIGNING_BUNDLE=bundle(schemaVersion=True)),
                 environment(SIGNING_BUNDLE=bundle(storePassword='')), environment(SIGNING_BUNDLE=bundle(extra='unexpected')),
                 environment(SIGNING_BUNDLE='{"schemaVersion":1,"schemaVersion":1}'), environment(SIGNING_BUNDLE='x'*48001)]
        for env in cases:
            with self.subTest(env=list(env)):
                with self.assertRaises((ValueError, TypeError)):
                    signer.load_identity(env)

    def test_exactly_one_pinned_apk_signer(self):
        good = ('Signer #1 certificate SHA-256 digest: '+PIN+'\n').encode()
        self.assertTrue(signer.verified_signer(good, PIN))
        self.assertFalse(signer.verified_signer(good+good, PIN))
        self.assertFalse(signer.verified_signer(good+b'Signer #2 certificate SHA-256 digest: other\n', PIN))
        self.assertFalse(signer.verified_signer(good, 'f'*64))

    def test_verify_only_cleanup_and_token_isolation(self):
        with tempfile.TemporaryDirectory() as tmp:
            calls=[]
            def fake(command, env):
                calls.append(command)
                self.assertNotIn('GH_TOKEN', env)
                self.assertNotIn('SIGNING_BUNDLE', env)
                if '-file' in command:
                    Path(command[command.index('-file')+1]).write_bytes(CERT)
                return b''
            with patch.object(signer, 'quiet', side_effect=fake), patch.object(subprocess, 'run', side_effect=AssertionError('No real process permitted')), redirect_stdout(io.StringIO()) as output:
                signer.operate(argparse.Namespace(verify_only=True), environment(RUNNER_TEMP=tmp, GH_TOKEN='fake-token'))
            self.assertEqual(len(calls), 2)
            self.assertNotIn('-genkeypair', calls[0])
            self.assertIn('-certreq', calls[1])
            self.assertIn(PIN, output.getvalue())
            self.assertEqual(list(Path(tmp).iterdir()), [])

    def test_verify_only_requires_usable_private_key(self):
        with tempfile.TemporaryDirectory() as tmp:
            def fake(command, env):
                if '-exportcert' in command:
                    Path(command[command.index('-file')+1]).write_bytes(CERT)
                    return b''
                if '-certreq' in command:
                    raise ValueError('Wrong key password or certificate-only entry')
                raise AssertionError(command)
            with patch.object(signer, 'quiet', side_effect=fake), redirect_stdout(io.StringIO()) as output:
                with self.assertRaises(ValueError):
                    signer.operate(argparse.Namespace(verify_only=True), environment(RUNNER_TEMP=tmp))
            self.assertNotIn('verified', output.getvalue())
            self.assertEqual(list(Path(tmp).iterdir()), [])

    def test_certificate_mismatch_cannot_sign(self):
        with tempfile.TemporaryDirectory() as tmp:
            def fake(command, env):
                Path(command[command.index('-file')+1]).write_bytes(b'WRONG-PUBLIC-CERT')
                return b''
            with patch.object(signer, 'quiet', side_effect=fake) as command:
                with self.assertRaisesRegex(ValueError, 'Actual signing certificate'):
                    signer.operate(argparse.Namespace(verify_only=False), environment(RUNNER_TEMP=tmp))
            self.assertEqual(command.call_count, 1)
            self.assertEqual(list(Path(tmp).iterdir()), [])

    def test_signing_writes_only_verified_public_outputs(self):
        with tempfile.TemporaryDirectory() as tmp:
            output_path=Path(tmp)/'release.apk'
            info_path=Path(tmp)/'SIGNING-INFO.txt'
            def fake(command, env):
                self.assertNotIn('GH_TOKEN',env)
                if '-exportcert' in command:
                    Path(command[command.index('-file')+1]).write_bytes(CERT)
                    return b''
                if 'sign' in command:
                    Path(command[command.index('--out')+1]).write_bytes(b'FAKE-APK-NOT-INSTALLABLE')
                    return b''
                if 'verify' in command:
                    return ('Signer #1 certificate SHA-256 digest: '+PIN+'\n').encode()
                raise AssertionError(command)
            args=argparse.Namespace(verify_only=False,unsigned_apk='fixture.apk',signed_apk=str(output_path),signing_info=str(info_path))
            with patch.object(signer,'quiet',side_effect=fake), patch.object(subprocess,'run',return_value=subprocess.CompletedProcess([],0,stdout='a'*40+'\n')) as git, redirect_stdout(io.StringIO()):
                signer.operate(args,environment(RUNNER_TEMP=tmp,ANDROID_HOME='/mock-sdk'))
            self.assertEqual(git.call_count,1)
            self.assertEqual(git.call_args.args[0],['git','rev-parse','HEAD'])
            self.assertEqual(output_path.read_bytes(),b'FAKE-APK-NOT-INSTALLABLE')
            for path in (info_path,Path(tmp)/'APK-VERIFICATION.txt'):
                self.assertIn(PIN,path.read_text())
                self.assertNotIn(FAKE_PASSWORD,path.read_text())
                self.assertNotIn(ENCODED,path.read_text())
            self.assertFalse(any(p.is_dir() for p in Path(tmp).iterdir()))

    def test_mask_escapes_workflow_command_characters(self):
        with patch.dict(os.environ, {'GITHUB_ACTIONS':'true'}), redirect_stdout(io.StringIO()) as output:
            signer.mask('a%\nb\r')
        self.assertEqual(output.getvalue(), '::add-mask::a%25%0Ab%0D\n')


class BootstrapTests(unittest.TestCase):
    def setUp(self):
        self.module=bootstrap_module()
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.env=dict(GITHUB_ACTIONS='true', GITHUB_REPOSITORY='hddy774/project-hora', GITHUB_EVENT_NAME='workflow_dispatch',
                      GITHUB_REF='refs/heads/main', GITHUB_ACTOR='hddy774', GITHUB_TRIGGERING_ACTOR='hddy774', GITHUB_RUN_ATTEMPT='1',
                      BOOTSTRAP_CONFIRMATION='CREATE_ALPHA_EXCHANGE_KEY', GH_TOKEN='FAKE-TOKEN-NOT-A-CREDENTIAL', RUNNER_TEMP=self.temp.name)
        self.calls=[]
        self.names=b''
        self.status=b'201'
        self.write_error=False

    def fake_command(self, command, *, data=None, token=False, extra_env=None):
        self.calls.append(command)
        if command[0]=='keytool':
            self.assertFalse(token)
            if '-genkeypair' in command:
                Path(command[command.index('-keystore')+1]).write_bytes(FAKE_KEY)
            else:
                Path(command[command.index('-file')+1]).write_bytes(CERT)
            return b''
        if command[:3]==['gh','secret','set']:
            self.assertIn('--no-store',command)
            self.assertNotIn(FAKE_PASSWORD,command)
            self.assertEqual(json.loads(data)['certificateSha256'],PIN)
            return base64.b64encode(b'FAKE-CIPHERTEXT')+b'\n'
        if '--paginate' in command: return self.names
        if command[-1].endswith('/public-key'): return b'{"key":"FAKE-PUBLIC-ENCRYPTION-KEY","key_id":"fixture-key-id"}'
        if 'PUT' in command:
            if self.write_error: raise RuntimeError('ambiguous network error')
            self.assertEqual(set(json.loads(data)),{'encrypted_value','key_id'})
            return b'HTTP/2.0 '+self.status+b' Result\r\n\r\n'
        if command[-1].endswith('/ANDROID_SIGNING_BUNDLE'): return b'{"name":"ANDROID_SIGNING_BUNDLE"}'
        raise AssertionError(command)

    def execute(self):
        with patch.dict(os.environ,self.env,clear=True), patch.object(self.module,'command',side_effect=self.fake_command), patch.object(self.module.secrets,'token_hex',return_value=FAKE_PASSWORD), patch.object(subprocess,'run',side_effect=AssertionError('No real process permitted')), redirect_stdout(io.StringIO()) as output:
            self.module.bootstrap()
        return output.getvalue()

    def test_success_single_write_public_summary_and_cleanup(self):
        output=self.execute()
        public='\n'.join(line for line in output.splitlines() if not line.startswith('::add-mask::'))
        self.assertIn(PIN,public)
        self.assertNotIn(FAKE_PASSWORD,public)
        self.assertNotIn(ENCODED,public)
        self.assertEqual(sum('PUT' in command for command in self.calls),1)
        self.assertEqual(list(Path(self.temp.name).iterdir()),[])

    def test_existing_legacy_or_bundle_aborts_before_generation(self):
        for name in self.module.EXISTING_NAMES:
            self.calls=[]
            self.names=name.encode()+b'\n'
            with self.assertRaises(RuntimeError): self.execute()
            self.assertFalse(any(c[0]=='keytool' or 'PUT' in c for c in self.calls))

    def test_missing_grant_wrong_owner_branch_or_rerun_aborts(self):
        for key,value in [('GH_TOKEN',''),('GITHUB_ACTOR','someone'),('GITHUB_TRIGGERING_ACTOR','someone'),('GITHUB_REF','refs/heads/feature'),('GITHUB_RUN_ATTEMPT','2'),('BOOTSTRAP_CONFIRMATION','yes'),('RUNNER_DEBUG','1')]:
            with self.subTest(key=key):
                env=dict(self.env); env[key]=value
                with patch.dict(os.environ,env,clear=True):
                    with self.assertRaises(RuntimeError): self.module.guard()

    def test_ambiguous_write_never_retries_or_removes_remote_secret(self):
        self.write_error=True
        with self.assertRaises(RuntimeError): self.execute()
        self.assertEqual(sum('PUT' in c for c in self.calls),1)
        self.assertFalse(any('DELETE' in c for c in self.calls))
        self.assertEqual(list(Path(self.temp.name).iterdir()),[])

    def test_upsert_status_is_not_reported_as_creation(self):
        self.status=b'204'
        with self.assertRaisesRegex(RuntimeError,'new creation'): self.execute()
        self.assertEqual(sum('PUT' in c for c in self.calls),1)

    def test_api_read_error_fails_before_randomness(self):
        with patch.dict(os.environ,self.env,clear=True), patch.object(self.module,'command',side_effect=RuntimeError('access denied')), patch.object(self.module.secrets,'token_hex') as random:
            with self.assertRaises(RuntimeError): self.module.bootstrap()
            random.assert_not_called()

    def test_identity_appearing_at_second_check_prevents_write(self):
        original=self.fake_command
        count=0
        def raced(command, **kwargs):
            nonlocal count
            if '--paginate' in command:
                count+=1
                if count==2:
                    self.names=b'ANDROID_SIGNING_BUNDLE\n'
            return original(command,**kwargs)
        self.fake_command=raced
        with self.assertRaisesRegex(RuntimeError,'existing or partial'):
            self.execute()
        self.assertFalse(any('PUT' in c for c in self.calls))
        self.assertEqual(list(Path(self.temp.name).iterdir()),[])

    def test_repository_encryption_key_change_prevents_write(self):
        original=self.fake_command
        count=0
        def changed(command, **kwargs):
            nonlocal count
            response=original(command,**kwargs)
            if command[-1].endswith('/public-key'):
                count+=1
                if count==2:
                    return b'{"key":"CHANGED-PUBLIC-KEY","key_id":"different"}'
            return response
        self.fake_command=changed
        with self.assertRaisesRegex(RuntimeError,'encryption key changed'):
            self.execute()
        self.assertFalse(any('PUT' in c for c in self.calls))

    def test_workflow_has_no_automatic_trigger_or_secret_artifact(self):
        workflow=(ROOT/'.github/workflows/bootstrap-android-signing.yml').read_text()
        for forbidden in ('uses:', 'upload-artifact', 'actions/cache', 'pull_request:', 'push:', 'schedule:', 'workflow_run:'):
            self.assertNotIn(forbidden,workflow)
        self.assertIn('permissions: {}',workflow)
        self.assertIn('cancel-in-progress: false',workflow)


if __name__=='__main__':
    unittest.main()
