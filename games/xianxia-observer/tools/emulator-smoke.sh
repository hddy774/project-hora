#!/usr/bin/env bash
# Android 에뮬레이터에 APK를 설치·실행하고 화면과 logcat을 남긴다.
# 앱 프로세스가 사라지거나 치명적 예외가 있으면 실패한다. CI(emulator 작업)에서만 실행한다.
# 사용법: emulator-smoke.sh <apk> <출력 디렉터리>
set -euo pipefail

apk="$1"
out="$2"
package="org.hora.xianxiaobserver"
mkdir -p "$out"

adb wait-for-device
adb install -r "$apk"
adb logcat -c
adb shell am start -W -n "$package/.MainActivity"

# 첫 화면이 그려지고 시뮬레이션이 돌기 시작할 시간을 준다.
sleep 25
adb exec-out screencap -p > "$out/01-launch.png"

# 하단 탭과 화면 가운데를 차례로 눌러 입력 처리 중 예외가 없는지 본다.
size=$(adb shell wm size | awk -F': ' '/Physical/ {print $2}' | tr -d '\r')
width=${size%x*}
height=${size#*x}
for fraction in 25 42 58 75 92; do
    adb shell input tap $((width * fraction / 100)) $((height * 97 / 100))
    sleep 2
done
adb exec-out screencap -p > "$out/02-after-taps.png"
adb shell input tap $((width / 2)) $((height / 2))
sleep 3
adb exec-out screencap -p > "$out/03-center-tap.png"
pid=$(adb shell pidof "$package" | tr -d '\r' || true)
adb shell input keyevent KEYCODE_BACK
sleep 3
adb exec-out screencap -p > "$out/04-after-back.png"

adb logcat -d > "$out/logcat.txt"
grep -nE "FATAL EXCEPTION|AndroidRuntime|$package|mono-rt|DOTNET|Avalonia" "$out/logcat.txt" | tail -80 || true


# 아티팩트를 내려받기 어려운 환경에서도 화면을 확인할 수 있게 축소 화면을 로그 맨 끝에 base64로 남긴다.
if command -v convert > /dev/null; then
    for name in 01-launch 02-after-taps 03-center-tap; do
        convert "$out/$name.png" -resize 300x -strip -quality 60 "$out/$name-small.jpg"
        echo "----- BEGIN $name-small.jpg (base64) -----"
        base64 -w 150 "$out/$name-small.jpg"
        echo "----- END $name-small.jpg -----"
    done
fi
if grep -q "FATAL EXCEPTION" "$out/logcat.txt"; then
    echo "::error::앱에서 치명적 예외가 발생했습니다."
    exit 1
fi
if [ -z "$pid" ]; then
    echo "::error::앱 프로세스가 실행 중이 아닙니다."
    exit 1
fi
echo "앱이 실행 중입니다(pid $pid)."
