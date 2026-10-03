#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
: "${ANDROID_HOME:?Set ANDROID_HOME to your Android SDK directory}"
: "${JAVA_HOME:?Set JAVA_HOME to your JDK 17 directory}"
dotnet run --project tests/AlphaExchange.Checks -c Release
dotnet publish src/AlphaExchange.Android/AlphaExchange.Android.csproj \
  -c Release \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -o dist
