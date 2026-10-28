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

# Google APIs 이미지의 Pixel Launcher ANR 창이 게임의 입력을 가리는 경우를 없앤다.
# 게임 프로세스는 유지하고 에뮬레이터 런처만 정리한 뒤 다시 앞으로 가져온다.
adb shell am force-stop com.google.android.apps.nexuslauncher
adb shell am start -W -n "$package/.MainActivity"

# 모든 하단 탭을 눌러 실제 화면과 입력 처리 결과를 남긴다.
size=$(adb shell wm size | awk -F': ' '/Physical/ {print $2}' | tr -d '\r')
width=${size%x*}
height=${size#*x}
for tab in 0 1 2 3 4 5 6; do
    # Pixel 6의 제스처 안전 영역 위, 탭 글리프 중앙을 누른다. 하단 여백을 누르면 탭이 바뀌지 않는다.
    adb shell input tap $((width * (2 * tab + 1) / 14)) $((height * 90 / 100))
    sleep 2
    adb exec-out screencap -p > "$out/tab-$tab.png"
done
adb exec-out screencap -p > "$out/02-after-taps.png"
adb shell input tap $((width / 2)) $((height / 2))
sleep 3
adb exec-out screencap -p > "$out/03-center-tap.png"
pid=$(adb shell pidof "$package" | tr -d '\r' || true)
adb shell input keyevent KEYCODE_BACK
sleep 3
adb exec-out screencap -p > "$out/04-after-back.png"

# Android 수명 주기의 백그라운드 저장과 복귀, 종료 후 저장 불러오기를 실제 배포 APK로 확인한다.
adb shell input keyevent KEYCODE_HOME
sleep 3
adb shell am start -W -n "$package/.MainActivity"
sleep 4
adb exec-out screencap -p > "$out/05-foreground.png"
adb shell am force-stop "$package"
adb shell am start -W -n "$package/.MainActivity"
sleep 6
adb exec-out screencap -p > "$out/06-relaunch.png"
pid=$(adb shell pidof "$package" | tr -d '\r' || true)
sha256sum "$apk" > "$out/tested-apk-sha256.txt"

adb logcat -d > "$out/logcat.txt"
grep -nE "FATAL EXCEPTION|AndroidRuntime|$package|mono-rt|DOTNET|Avalonia" "$out/logcat.txt" | tail -80 || true


# 화면 자체는 아티팩트에 보관한다. 빈 화면을 성공으로 처리하지 않는다.
if command -v convert > /dev/null; then
    variation=$(convert "$out/06-relaunch.png" -format '%[fx:standard_deviation]' info:)
    awk -v value="$variation" 'BEGIN { exit !(value > 0.01) }'
fi
if grep -q "FATAL EXCEPTION" "$out/logcat.txt" || grep -q "ANR in $package" "$out/logcat.txt"; then
    echo "::error::앱에서 치명적 예외가 발생했습니다."
    exit 1
fi
if [ -z "$pid" ]; then
    echo "::error::앱 프로세스가 실행 중이 아닙니다."
    exit 1
fi
echo "앱이 실행 중입니다(pid $pid)."
