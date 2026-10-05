"""Actual Android app/legacy SQLite/UI/minute-clock smoke test on a rooted CI emulator."""
import json, os, pathlib, re, sqlite3, subprocess, sys, time
import brotli
import xml.etree.ElementTree as ET

apk, fixture, output = map(pathlib.Path, sys.argv[1:4])
output.mkdir(parents=True, exist_ok=True)
package = "com.alphaexchange.offline"
device = f"/data/user/0/{package}/files"

def adb(*args, binary=False):
    result = subprocess.run(["adb", *args], check=True, capture_output=True, timeout=120)
    return result.stdout if binary else result.stdout.decode(errors="replace")

adb("root"); adb("wait-for-device")
adb("install", "-r", str(apk))
uid = re.search(r"userId=(\d+)", adb("shell", "dumpsys", "package", package)).group(1)
adb("shell", "mkdir", "-p", device)
for name in ("history-v5.sqlite", "history-v5.sqlite.bak", "market-v4.json", "market-v4.json.bak"):
    path=fixture/name
    if path.exists(): adb("push", str(path), device+"/"+name)
adb("shell", "chown", "-R", f"{uid}:{uid}", device)
adb("shell", "restorecon", "-RF", device)
adb("shell", "svc", "wifi", "disable"); adb("shell", "svc", "data", "disable")
adb("shell", "monkey", "-p", package, "-c", "android.intent.category.LAUNCHER", "1")

def tap(x,y):
    adb("shell", "input", "tap", str(round(left+x*scale)), str(round(top+y*scale))); time.sleep(1)

def screenshot(name):
    (output/(name+".png")).write_bytes(adb("exec-out", "screencap", "-p",binary=True))

def window_tree():
    adb("shell","rm","-f","/sdcard/hora-window.xml")
    dump=adb("shell","uiautomator","dump","/sdcard/hora-window.xml")
    (output/"hierarchy-dump.txt").write_text(dump)
    raw=adb("exec-out","cat","/sdcard/hora-window.xml")
    (output/"window.xml").write_text(raw)
    try: return ET.fromstring(raw)
    except ET.ParseError:
        # During activity startup, Android's dumper can report a null root and
        # exit successfully without writing XML. The bounded screen wait retries.
        return ET.Element("hierarchy")

def screen_description(tree):
    return " ".join(n.attrib.get("content-desc","") for n in tree.iter("node"))

def description():
    return screen_description(window_tree())

def require_screen(text,timeout=30):
    deadline=time.monotonic()+timeout
    while True:
        tree=window_tree(); actual=screen_description(tree)
        if text in actual: return tree
        if time.monotonic()>deadline:
            screenshot("failure-screen")
            (output/"failure-logcat.txt").write_text(adb("logcat","-d","-t","250"))
            raise RuntimeError(f"Expected native screen: {text}; actual: {actual}")
        time.sleep(1)

def swipe():
    adb("shell", "input", "swipe", str(round(left+200*scale)),str(round(top+430*scale)),str(round(left+200*scale)),str(round(top+210*scale)),"450")
    time.sleep(1)

def pull_state(name):
    folder=output/name; folder.mkdir(exist_ok=True)
    adb("pull",device+"/history-v5.sqlite",str(folder/"history-v5.sqlite"))
    for suffix in ("-wal","-shm"):
        try: adb("pull",device+"/history-v5.sqlite"+suffix,str(folder/("history-v5.sqlite"+suffix)))
        except subprocess.CalledProcessError: pass
    with sqlite3.connect(folder/"history-v5.sqlite") as db:
        state=db.execute("select state from runs where id=(select value from meta where key='current')").fetchone()[0]
        return json.loads(brotli.decompress(state)),folder

# Startup is asynchronous; wait for the first source8 checkpoint after resume.
lobby_tree=require_screen("시뮬레이션 시작 화면",60); screenshot("01-lobby")
# Android can expose hardware keys instead of a navigation bar. Use the actual
# GameView bounds, including status/navigation insets, rather than guessing them.
view=next(n for n in lobby_tree.iter("node") if n.attrib.get("content-desc","").startswith("알파 익스체인지"))
left,top,right,bottom=map(int,re.findall(r"\d+",view.attrib["bounds"]))
scale=(right-left)/400; h=(bottom-top)/scale
(output/"viewport.json").write_text(json.dumps({"bounds":[left,top,right,bottom],"scale":scale,"canvasHeight":h},indent=2))
tap(200,h-140)
require_screen("시장 화면")
deadline=time.monotonic()+60
while True:
    time.sleep(2)
    try:
        state,_=pull_state("running")
        if state["Version"]==8 and state["CompletedHours"]>=170: break
    except (sqlite3.Error,subprocess.CalledProcessError,IndexError): pass
    if time.monotonic()>deadline: raise RuntimeError("App failed to load/advance/save the true v7 fixture")
tap(278,h-108); require_screen("일시정지"); time.sleep(2); screenshot("02-paused-market")
paused_at=time.monotonic()

# All new role lists, actual additional portraits and uncropped profile zoom.
tap(200,h-40); require_screen("종합 인물 목록"); screenshot("03-people")
tap(200,194); require_screen("경영자 인물 목록"); screenshot("04-executives")
tap(200,325); require_screen("인물 프로필"); screenshot("05-new-executive-profile")
tap(85,240); require_screen("일러스트 확대"); screenshot("06-new-portrait-zoom")
adb("shell","input","keyevent","4"); time.sleep(1)
adb("shell","input","keyevent","4"); time.sleep(1)
require_screen("경영자 인물 목록")
tap(273,194); require_screen("정치인 인물 목록"); screenshot("07-politicians")
tap(346,194); require_screen("금융인 인물 목록"); screenshot("08-financiers")
tap(200,246); require_screen("경제 정부와 선거"); screenshot("09-economic-votes")
tap(400/7*4.5,h-40); require_screen("통계 화면"); screenshot("10-statistics")
tap(273,194); screenshot("10b-institution-financials")
tap(400/7*0.5,h-40); require_screen("시장 화면"); swipe(); screenshot("11-companies")
tap(200,345)
if "실시간 호가" not in description():
    tap(400/7*0.5,h-40); swipe(); tap(200,420)
require_screen("실시간 호가"); screenshot("12-order-book")
adb("shell","input","keyevent","4"); time.sleep(1)
require_screen("시장 화면")

#100x on actual native worker. Allow one second for input/checkpoint boundaries.
before,_=pull_state("before100x")
for _ in range(5): tap(146,h-108)
require_screen("100배속")
screenshot("13-speed100")
idle=time.monotonic()-paused_at
if idle<16: time.sleep(16-idle)
idle=time.monotonic()-paused_at
started=time.monotonic(); tap(278,h-108); require_screen("진행 중"); time.sleep(15); tap(278,h-108)
elapsed=time.monotonic()-started; require_screen("일시정지"); time.sleep(3); screenshot("14-after100x")
adb("shell","input","keyevent","3"); time.sleep(3)
adb("shell","am","force-stop",package)
after,folder=pull_state("final")
delta=after["CompletedMinutes"]-before["CompletedMinutes"]
pending=after["PendingClockMinutes"]
if delta+pending < (elapsed-2)*1200 or pending>1200:
    screenshot("failure-100x")
    (output/"failure-logcat.txt").write_text(adb("logcat","-d","-t","250"))
    (output/"native-clock-failure.json").write_text(json.dumps({"elapsedSeconds":elapsed,"completedMinutes":delta,"pendingMinutes":pending},indent=2))
    raise RuntimeError(f"Native100x failed: {delta} completed,{pending} pending over{elapsed:.3f}s")
baseline=json.loads((fixture/"baseline.json").read_text())
with sqlite3.connect(folder/"history-v5.sqlite") as db:
    actual={(hour,resolution):sha for hour,resolution,sha in db.execute("select hour,resolution,hash from hours where run=?",(baseline["run"],))}
    if any(actual.get((row["hour"],row["resolution"]))!=row["hash"] for row in baseline["rows"]):
        raise RuntimeError("Native migration changed or removed legacy raw-hour hashes")
if after["RunId"]!=baseline["run"] or after["Version"]!=8 or len(after["World"]["People"])!=200:
    raise RuntimeError("Native run/person identity changed")
summary={"legacyVersion":7,"nativeVersion":8,"preservedRows":len(baseline["rows"]),"newHour":after["CompletedHours"],
    "native100x":{"elapsedSeconds":elapsed,"completedMinutes":delta,"pendingMinutes":pending,"boundaryAllowanceSeconds":2,"idleBeforeResumeSeconds":idle},
    "screenshots":len(list(output.glob("*.png"))),"roles":200,"retail":10000}
(output/"summary.json").write_text(json.dumps(summary,indent=2))
print("PASS native legacy migration, immutable hours,200 profiles/zoom and100x:",json.dumps(summary),flush=True)
subprocess.run(["dotnet","run","--project","tools/NativeSmokeFixture","-c","Release","--","inspect",str(folder)],check=True)
