#!/usr/bin/env bash
set -euo pipefail

unsigned_apk="${1:?Unsigned APK path required}"
signed_apk="${2:?Signed APK path required}"
signing_info="${3:?Signing information path required}"
: "${ANDROID_HOME:?Android SDK required}"
: "${RUNNER_TEMP:?Temporary directory required}"
keystore="$RUNNER_TEMP/hora-release-$$.keystore"
certificate="$RUNNER_TEMP/hora-release-$$.cer"
original_certificate=9fd5aa86793e26beec38948dfae79d1981581f8e54abe0047025a863cc64f722
trap 'rm -f "$keystore" "$certificate"' EXIT
umask 077

case "${GENERATE_SIGNING_KEY:-false}" in
  true)
    STORE_PASSWORD=$(openssl rand -hex 32)
    KEY_PASSWORD=$STORE_PASSWORD
    KEY_ALIAS=hora-actions-generated
    export STORE_PASSWORD KEY_PASSWORD
    if [[ "${GITHUB_ACTIONS:-false}" == true ]]; then
      echo "::add-mask::$STORE_PASSWORD"
    fi
    keytool -genkeypair -noprompt -storetype PKCS12 \
      -keystore "$keystore" -alias "$KEY_ALIAS" \
      -storepass:env STORE_PASSWORD -keypass:env KEY_PASSWORD \
      -keyalg RSA -keysize 2048 -sigalg SHA256withRSA -validity 10000 \
      -dname 'CN=Alpha Exchange, OU=GitHub Actions generated signature, O=Alpha Exchange, C=KR'
    mode='GitHub Actions generated one-time key'
    notice='New signing identity: cannot update previous releases in place. Uninstalling the old app can delete local saves. This temporary key is deleted and cannot sign another build.'
    ;;
  false)
    for setting in KEYSTORE_BASE64 STORE_PASSWORD KEY_ALIAS KEY_PASSWORD; do
      if [[ -z "${!setting:-}" ]]; then
        echo "::error::Existing Android signing configuration is missing: $setting. Configure the four ANDROID_* signing secrets, or explicitly select generate_signing_key for a new installation identity."
        exit 1
      fi
    done
    printf '%s' "$KEYSTORE_BASE64" | base64 --decode > "$keystore"
    mode='Existing v1.1.0 release key'
    notice='Original signing identity retained; installed v1.1.0 games can update.'
    ;;
  *) echo 'Invalid GENERATE_SIGNING_KEY value' >&2; exit 1 ;;
esac

keytool -exportcert -keystore "$keystore" -alias "$KEY_ALIAS" \
  -storepass:env STORE_PASSWORD -file "$certificate"
expected_certificate=$(sha256sum "$certificate" | cut -d ' ' -f 1)
if [[ "${GENERATE_SIGNING_KEY:-false}" == false && "$expected_certificate" != "$original_certificate" ]]; then
  echo '::error::Configured signing key does not match the original v1.1.0 certificate.'
  exit 1
fi
"$ANDROID_HOME/build-tools/35.0.0/apksigner" sign \
  --ks "$keystore" --ks-key-alias "$KEY_ALIAS" \
  --ks-pass env:STORE_PASSWORD --key-pass env:KEY_PASSWORD \
  --out "$signed_apk" "$unsigned_apk"
verification="$(dirname "$signed_apk")/APK-VERIFICATION.txt"
"$ANDROID_HOME/build-tools/35.0.0/apksigner" verify --verbose --print-certs "$signed_apk" > "$verification"
grep -F "Signer #1 certificate SHA-256 digest: $expected_certificate" "$verification"
{
  printf 'Signing mode: %s\nCertificate SHA-256: %s\n' "$mode" "$expected_certificate"
  printf '%s\n' "$notice"
  printf 'Source commit: %s\n' "$(git rev-parse HEAD)"
  printf 'Workflow commit: %s\n' "${GITHUB_SHA:-local-verification}"
  printf 'Workflow run: %s\n' "${GITHUB_RUN_ID:-local-verification}"
} > "$signing_info"
