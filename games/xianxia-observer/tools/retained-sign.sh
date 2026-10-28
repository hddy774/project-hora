#!/usr/bin/env bash
# Invoked only by the owner-requested Xianxia GitHub release job. Never downloads
# the retained key to a developer workspace and never prints credential values.
set -euo pipefail
if [[ "$#" != 2 ]]; then
  printf 'Usage: retained_sign.sh verified-unsigned.apk signed-output.apk\n' >&2
  exit 2
fi
export HORA_UNSIGNED_APK="$1" HORA_SIGNED_APK="$2"
python3 - <<'PY'
import base64, hashlib, json, os, pathlib, re, subprocess, sys, tempfile, zipfile
PIN='0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a'
APPLICATION_ID='org.hora.xianxiaobserver'
VERSION='0.1.0'
POLICY='retained_repository_publisher_for_hora_release_request'
phase='trusted release inputs'
def mask(value):
    print('::add-mask::'+value.replace('%','%25').replace('\r','%0D').replace('\n','%0A'),flush=True)
def unique(pairs):
    output={}
    for key,value in pairs:
        if key in output: raise ValueError('duplicate JSON field')
        output[key]=value
    return output
def quiet(args,env):
    result=subprocess.run([str(x) for x in args],env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if result.returncode: raise ValueError('subprocess failed')
    return result.stdout
try:
    source=os.environ['SOURCE_SHA']; expected=os.environ['UNSIGNED_SHA256']
    if not re.fullmatch(r'[a-f0-9]{40}',source) or not re.fullmatch(r'[a-f0-9]{64}',expected): raise ValueError('reviewed source required')
    unsigned=pathlib.Path(os.environ['HORA_UNSIGNED_APK']).resolve()
    signed=pathlib.Path(os.environ['HORA_SIGNED_APK']).resolve()
    if not unsigned.is_file() or unsigned==signed or unsigned.stat().st_size>256*1024*1024: raise ValueError('input')
    if hashlib.file_digest(unsigned.open('rb'),'sha256').hexdigest()!=expected: raise ValueError('checksum')
    phase='retained publisher bundle transport'
    raw=os.environ.get('SIGNING_BUNDLE','')
    if not raw or len(raw.encode())>48000: raise ValueError('missing retained publisher identity')
    mask(raw)
    payload=raw.strip().lstrip('\ufeff').strip()
    fence=re.fullmatch(r'```(?:json)?[ \t]*\r?\n(.*)\r?\n```',payload,re.I|re.S)
    if fence: payload=fence[1].strip()
    if payload.startswith("'{") and payload.endswith("}'"): payload=payload[1:-1]
    raw_keystore=None
    if not payload.startswith(('{','"')):
        decoded=base64.b64decode(''.join(payload.split()),validate=True)
        if decoded.startswith(b'\x30\x82'): raw_keystore=payload
        else: payload=decoded.decode('utf-8-sig').strip()
    if raw_keystore is not None:
        phase='retained raw keystore companion secrets'
        required=('RAW_STORE_PASSWORD','RAW_KEY_ALIAS','RAW_KEY_PASSWORD')
        if any(not os.environ.get(name) for name in required): raise ValueError('missing companion secret')
        bundle={'schemaVersion':1,'game':'alpha-exchange','applicationId':'com.alphaexchange.offline',
                'certificateSha256':PIN,'keystoreBase64':raw_keystore,
                'storePassword':os.environ['RAW_STORE_PASSWORD'],'keyAlias':os.environ['RAW_KEY_ALIAS'],'keyPassword':os.environ['RAW_KEY_PASSWORD']}
    else:
        phase='retained bundle JSON schema'
        bundle=json.loads(payload,object_pairs_hook=unique)
        if isinstance(bundle,str): bundle=json.loads(bundle.strip().lstrip('\ufeff').strip(),object_pairs_hook=unique)
    required={'schemaVersion','game','applicationId','keystoreBase64','storePassword','keyAlias','keyPassword','certificateSha256'}
    if not isinstance(bundle,dict) or set(bundle)!=required or type(bundle['schemaVersion']) is not int or bundle['schemaVersion']!=1: raise ValueError('schema')
    # Preserve and verify the original bundle contract exactly. The existing
    # Alpha release workflow and its application identity are never rewritten.
    phase='original retained bundle identity'
    if bundle['game']!='alpha-exchange' or bundle['applicationId']!='com.alphaexchange.offline' or bundle['certificateSha256']!=PIN: raise ValueError('retained identity')
    for field in ('keystoreBase64','storePassword','keyAlias','keyPassword'):
        value=bundle[field]
        if not isinstance(value,str) or not 1<=len(value)<=44000 or '\x00' in value: raise ValueError('credential field')
        mask(value)
    key_data=base64.b64decode(''.join(bundle['keystoreBase64'].split()),validate=True)
    if not 1<=len(key_data)<=32768: raise ValueError('key size')
    env={k:v for k,v in os.environ.items() if k not in ('SIGNING_BUNDLE','RAW_STORE_PASSWORD','RAW_KEY_ALIAS','RAW_KEY_PASSWORD','GH_TOKEN','GITHUB_TOKEN')}
    env.update(STORE_PASSWORD=bundle['storePassword'],KEY_PASSWORD=bundle['keyPassword'])
    sdk=pathlib.Path(os.environ.get('ANDROID_HOME',os.environ.get('ANDROID_SDK_ROOT','')))/'build-tools/36.0.0'
    if not (sdk/'apksigner').is_file() or not (sdk/'aapt').is_file(): raise ValueError('build tools 36.0.0')
    phase='new Xianxia unsigned APK identity and offline policy'
    badging=quiet([sdk/'aapt','dump','badging',unsigned],env).decode()
    for expected_field in ("package: name='"+APPLICATION_ID+"'","versionName='"+VERSION+"'","versionCode='1'","sdkVersion:'24'"):
        if expected_field not in badging: raise ValueError('Xianxia manifest')
    if 'android.permission.INTERNET' in quiet([sdk/'aapt','dump','permissions',unsigned],env).decode(): raise ValueError('network permission')
    manifest=quiet([sdk/'aapt','dump','xmltree',unsigned,'AndroidManifest.xml'],env).decode()
    if 'android:debuggable(0x0101000f)=(type 0x12)0xffffffff' in manifest: raise ValueError('debuggable release')
    if 'android:screenOrientation(0x0101001e)=(type 0x10)0x1' not in manifest: raise ValueError('portrait')
    with zipfile.ZipFile(unsigned) as z:
        if len(z.namelist())!=len(set(z.namelist())): raise ValueError('duplicate ZIP entries')
        if {n.split('/')[1] for n in z.namelist() if n.startswith('lib/') and n.endswith('.so')}!={'arm64-v8a','x86_64'}: raise ValueError('ABIs')
    unsigned_check=subprocess.run([str(sdk/'apksigner'),'verify',str(unsigned)],env=env,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    if unsigned_check.returncode==0: raise ValueError('expected unsigned source artifact')
    os.umask(0o077)
    phase='temporary retained key access'
    with tempfile.TemporaryDirectory(prefix='hora-publisher-',dir=os.environ['RUNNER_TEMP']) as temporary:
        root=pathlib.Path(temporary); key=root/'publisher.p12'; cert=root/'publisher.der'
        key.write_bytes(key_data)
        quiet(['keytool','-exportcert','-keystore',key,'-alias',bundle['keyAlias'],'-storepass:env','STORE_PASSWORD','-file',cert],env)
        if hashlib.sha256(cert.read_bytes()).hexdigest()!=PIN: raise ValueError('publisher certificate')
        quiet(['keytool','-certreq','-keystore',key,'-alias',bundle['keyAlias'],'-storepass:env','STORE_PASSWORD','-keypass:env','KEY_PASSWORD'],env)
        phase='signing verified new application with retained publisher'
        quiet([sdk/'apksigner','sign','--ks',key,'--ks-key-alias',bundle['keyAlias'],'--ks-pass','env:STORE_PASSWORD','--key-pass','env:KEY_PASSWORD','--out',signed,unsigned],env)
    phase='signed Xianxia certificate and unchanged payload verification'
    verification=quiet([sdk/'apksigner','verify','--verbose','--print-certs',signed],env).decode()
    certs=re.findall(r'(?m)^Signer #1 certificate SHA-256 digest:[ \t]*([a-fA-F0-9]{64})[ \t]*$',verification)
    if certs!=[PIN] or re.findall(r'(?m)^Number of signers:[ \t]*(\d+)[ \t]*$',verification)!=['1'] or 'Verifies' not in verification.splitlines(): raise ValueError('signed publisher mismatch')
    def signature_file(name):
        return name.startswith('META-INF/') and (name.endswith(('.SF','.RSA','.DSA','.EC')) or name=='META-INF/MANIFEST.MF')
    with zipfile.ZipFile(unsigned) as before, zipfile.ZipFile(signed) as after:
        original={n for n in before.namelist() if not signature_file(n)}
        result={n for n in after.namelist() if not signature_file(n)}
        if original!=result: raise ValueError('payload file set')
        for name in original:
            if hashlib.sha256(before.read(name)).digest()!=hashlib.sha256(after.read(name)).digest(): raise ValueError('payload changed')
    report={'application_id':APPLICATION_ID,'version':VERSION,'version_code':1,'source_commit':source,
            'certificate_sha256':PIN,'signing_policy':POLICY,'unsigned_sha256':expected,
            'signed_sha256':hashlib.file_digest(signed.open('rb'),'sha256').hexdigest(),
            'payload_matches_verified_unsigned':True,'original_bundle_application_id':'com.alphaexchange.offline',
            'original_bundle_modified':False,'key_created':False,'key_exported_to_workspace':False,'verification_build_tools':'36.0.0'}
    (signed.parent/'SIGNING-VERIFICATION.json').write_text(json.dumps(report,indent=2)+'\n')
    print('Xianxia release signature verified against retained publisher certificate '+PIN)
except Exception:
    print('::error::Xianxia retained-publisher signing failed at: '+phase+'. No credential values are reported, no new identity was created, and the original Alpha bundle was not changed.',file=sys.stderr)
    sys.exit(1)
PY
