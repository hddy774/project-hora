"""Actual Android app/legacy SQLite/UI/minute-clock smoke test on a rooted CI emulator."""
import contextlib, io, json, os, pathlib, re, sqlite3, subprocess, sys, time
from PIL import Image
from native_capture import display_projection, logical_crop_bounds, logical_display_size
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
    logical_size=logical_display_size(adb("shell","wm","size"))
    raw=adb("exec-out", "screencap", "-p",binary=True)
    (output/(name+".png")).write_bytes(raw)
    image=Image.open(io.BytesIO(raw)).convert("RGB")
    image.info["logical_display_size"]=logical_size
    projection=display_projection(logical_size,image.size)
    view_bounds=[left,top,right,bottom] if "left" in globals() else None
    (output/(name+".capture.json")).write_text(json.dumps({"logicalDisplaySize":logical_size,
        "screenshotSize":image.size,"gameViewBounds":view_bounds,"projection":{"scale":projection[0],"offsetX":projection[1],"offsetY":projection[2]}},indent=2))
    return image

def crop(image,x1,y1,x2,y2):
    # Touch/UIAutomator coordinates are logical, while screencap can include
    # physical letterboxing or downscale a WM override larger than the display.
    logical=(left+x1*scale,top+y1*scale,left+x2*scale,top+y2*scale)
    bounds=logical_crop_bounds(logical,image.info["logical_display_size"],image.size)
    return image.crop(bounds).tobytes()

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

def swipe_between(start,end):
    adb("shell", "input", "swipe", str(round(left+200*scale)),str(round(top+start*scale)),str(round(left+200*scale)),str(round(top+end*scale)),"450")
    time.sleep(1)

def swipe():
    swipe_between(430,210)

def measure_viewport(tree):
    global left,top,right,bottom,scale,h
    view=next(n for n in tree.iter("node") if n.attrib.get("content-desc","").startswith("알파 익스체인지"))
    left,top,right,bottom=map(int,re.findall(r"\d+",view.attrib["bounds"]))
    scale=(right-left)/400; h=(bottom-top)/scale

def pull_state(name):
    folder=output/name; folder.mkdir(exist_ok=True)
    try: pid=adb("shell","pidof",package).strip()
    except subprocess.CalledProcessError: pid=""  # Final capture follows force-stop.
    if pid: adb("shell","kill","-STOP",pid)
    try:
        # Freeze only during evidence capture so a native WAL checkpoint cannot
        # alter the main DB between the two pulls. Rebuild local SHM and never
        # retain a stale sidecar from an earlier polling attempt.
        for suffix in ("-wal","-shm"):
            (folder/("history-v5.sqlite"+suffix)).unlink(missing_ok=True)
        adb("pull",device+"/history-v5.sqlite",str(folder/"history-v5.sqlite"))
        try: adb("pull",device+"/history-v5.sqlite-wal",str(folder/"history-v5.sqlite-wal"))
        except subprocess.CalledProcessError: pass
    finally:
        if pid: adb("shell","kill","-CONT",pid)
    with contextlib.closing(sqlite3.connect(folder/"history-v5.sqlite")) as db:
        state=db.execute("select state from runs where id=(select value from meta where key='current')").fetchone()[0]
        return json.loads(brotli.decompress(state)),folder

# Startup is asynchronous; wait for the first source8 checkpoint after resume.
lobby_tree=require_screen("시뮬레이션 시작 화면",60)
# Android can expose hardware keys instead of a navigation bar. Use the actual
# GameView bounds, including status/navigation insets, rather than guessing them.
measure_viewport(lobby_tree); screenshot("01-lobby")
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
    if time.monotonic()>deadline:
        screenshot("failure-startup")
        (output/"failure-logcat.txt").write_text(adb("logcat","-d","-t","250"))
        raise RuntimeError("App failed to load/advance/save the true v7 fixture")
# Lifecycle pause must preserve accepted foreground work, remain stopped while
# backgrounded, and return paused without adding the elapsed wall-clock time.
def clock_state(state):
    return {key:state[key] for key in ("RunId","CompletedMinutes","PendingClockMinutes","MinuteProgress","NextTransactionId")}

def settled_checkpoint(name,timeout=60):
    deadline=time.monotonic()+timeout; previous=None; stable=0
    while time.monotonic()<deadline:
        state,_=pull_state(name)
        current=clock_state(state)
        stable=stable+1 if current==previous else 0
        if stable>=3: return state
        previous=current
        time.sleep(1)
    raise RuntimeError("Lifecycle checkpoint did not settle while backgrounded")

before_home,_=pull_state("before-home")
adb("shell","input","keyevent","3")
background=settled_checkpoint("background-settled")
if background["RunId"]!=before_home["RunId"] or background["CompletedMinutes"]<before_home["CompletedMinutes"] or \
    background["CompletedMinutes"]+background["PendingClockMinutes"]<before_home["CompletedMinutes"]+before_home["PendingClockMinutes"]:
    raise RuntimeError("Lifecycle pause discarded completed minutes or accepted foreground work")
background_wait=8
time.sleep(background_wait)
background_later,_=pull_state("background-later")
if clock_state(background_later)!=clock_state(background):
    raise RuntimeError("Simulation clock changed after the background checkpoint settled")
adb("shell","monkey","-p",package,"-c","android.intent.category.LAUNCHER","1")
require_screen("시장 화면"); require_screen("일시정지")
time.sleep(2)
returned,_=pull_state("returned-paused")
if clock_state(returned)!=clock_state(background):
    raise RuntimeError("Returning to the Activity changed the paused simulation clock")
screenshot("02-paused-market")
paused_at=time.monotonic()
lifecycle_evidence={"backgroundWaitSeconds":background_wait,"beforeHome":clock_state(before_home),
    "settled":clock_state(background),"afterBackground":clock_state(background_later),"afterReturn":clock_state(returned)}
(output/"lifecycle.json").write_text(json.dumps(lifecycle_evidence,indent=2))

def paused_market_viewport(expected_height,name,expected_run,timeout=60):
    # wm size can recreate this Activity rather than just resize its existing
    # View. Wait for the new bounds AND async load, then use the existing-run
    # Continue action if recreation restored the normal lobby screen.
    deadline=time.monotonic()+timeout
    while True:
        tree=require_screen("알파 익스체인지",max(1,deadline-time.monotonic()))
        measure_viewport(tree)
        actual=screen_description(tree)
        if abs(h-expected_height)<=2 and any(screen in actual for screen in ("시장 화면","시뮬레이션 시작 화면","시뮬레이션 안내")): break
        if time.monotonic()>deadline:
            raise RuntimeError(f"Expected loaded canvas {expected_height}, got {h}: {actual}")
        time.sleep(1)
    recreated="시뮬레이션 시작 화면" in actual
    if recreated:
        saved,_=pull_state(name+"-before-continue")
        if saved["RunId"]!=expected_run:
            raise RuntimeError("Viewport recreation changed the saved run before Continue")
        tap(200,h-140)
        tree=require_screen("시장 화면")
        actual=screen_description(tree)
    elif "시뮬레이션 안내" in actual:
        # The finally path must also restore a usable market after a modal test
        # failed before dismissal. Do not change runtime configuration to do so.
        adb("shell","input","keyevent","4")
        tree=require_screen("시장 화면")
        actual=screen_description(tree)
    if "시장 화면" not in actual:
        raise RuntimeError(f"Unexpected screen after viewport change: {actual}")
    if "진행 중" in actual:
        tap(278,h-108)
    tree=require_screen("시장 화면"); require_screen("일시정지")
    measure_viewport(tree)
    if abs(h-expected_height)>2:
        raise RuntimeError(f"Canvas changed during test setup: expected {expected_height}, got {h}")
    state=settled_checkpoint(name+"-paused")
    if state["RunId"]!=expected_run:
        raise RuntimeError("Viewport recreation or Continue changed the existing run")
    return recreated

# Exercise real Canvas/touch handling at short and tall logical viewports. Resize
# only this disposable emulator and always restore its previous display setting.
size_text=adb("shell","wm","size")
original_override=re.search(r"Override size: (\d+x\d+)",size_text)
display_width,display_height=map(int,re.findall(r"(?:Physical|Override) size: (\d+)x(\d+)",size_text)[-1])
original_canvas=h
insets=display_height-(bottom-top)
modal_evidence=[]
try:
    for requested_height in (480,600,800):
        pixel_height=round(requested_height*scale+insets)
        adb("shell","wm","size",f"{display_width}x{pixel_height}")
        recreated=paused_market_viewport(requested_height,f"modal-{requested_height}",returned["RunId"])
        page_before=screenshot(f"modal-{requested_height}-page-before")
        tap(360,32); require_screen("시뮬레이션 안내")
        modal_top=max(12,h-654)
        body_top=modal_top+58; body_bottom=h-84
        before_scroll=screenshot(f"modal-{requested_height}-help-top")
        swipe_between(body_bottom-24,body_top+24)
        after_scroll=screenshot(f"modal-{requested_height}-help-scrolled")
        if crop(before_scroll,18,body_top+4,382,body_bottom-4)==crop(after_scroll,18,body_top+4,382,body_bottom-4):
            raise RuntimeError(f"Help body did not scroll at canvas {h}")
        if crop(before_scroll,336,modal_top+15,382,modal_top+57)!=crop(after_scroll,336,modal_top+15,382,modal_top+57):
            raise RuntimeError("Help close control moved while scrolling")
        if crop(before_scroll,27,h-72,373,h-22)!=crop(after_scroll,27,h-72,373,h-22):
            raise RuntimeError("Help CTA moved while scrolling")
        tap(200,h-47); require_screen("시장 화면")
        page_after=screenshot(f"modal-{requested_height}-page-after")
        if crop(page_before,18,102,382,h-146)!=crop(page_after,18,102,382,h-146):
            raise RuntimeError("Help gesture moved or activated the covered market page")
        # Reopen the same modal, verify reset, and exercise the separate close hit.
        tap(360,32); require_screen("시뮬레이션 안내")
        reopened=screenshot(f"modal-{requested_height}-help-reopened")
        if crop(before_scroll,18,body_top+4,382,body_bottom-4)!=crop(reopened,18,body_top+4,382,body_bottom-4):
            raise RuntimeError("Reopened help retained a dismissed modal's scroll offset")
        tap(360,modal_top+34); require_screen("시장 화면")
        modal_evidence.append({"requestedCanvasHeight":requested_height,"actualCanvasHeight":h,
            "continuedAfterRecreation":recreated,"preservedRunId":returned["RunId"],"bodyScrolled":True,"closePinned":True,"ctaPinned":True,"ctaDismissed":True,"closeDismissed":True,
            "underlyingPageUnchanged":True,"reopenedAtTop":True})
finally:
    adb("shell","wm","size",original_override.group(1) if original_override else "reset")
    paused_market_viewport(original_canvas,"restored-viewport",returned["RunId"])
paused_at=time.monotonic()
(output/"modal-viewports.json").write_text(json.dumps(modal_evidence,indent=2))

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
with contextlib.closing(sqlite3.connect(folder/"history-v5.sqlite")) as db:
    actual={(hour,resolution):sha for hour,resolution,sha in db.execute("select hour,resolution,hash from hours where run=?",(baseline["run"],))}
    if any(actual.get((row["hour"],row["resolution"]))!=row["hash"] for row in baseline["rows"]):
        raise RuntimeError("Native migration changed or removed legacy raw-hour hashes")
if after["RunId"]!=baseline["run"] or after["Version"]!=8 or len(after["World"]["People"])!=200:
    raise RuntimeError("Native run/person identity changed")
summary={"legacyVersion":7,"nativeVersion":8,"preservedRows":len(baseline["rows"]),"newHour":after["CompletedHours"],
    "native100x":{"elapsedSeconds":elapsed,"completedMinutes":delta,"pendingMinutes":pending,"boundaryAllowanceSeconds":2,"idleBeforeResumeSeconds":idle},
    "lifecycle":lifecycle_evidence,"modalViewports":modal_evidence,
    "screenshots":len(list(output.glob("*.png"))),"roles":200,"retail":10000}
(output/"summary.json").write_text(json.dumps(summary,indent=2))
print("PASS native lifecycle, modal viewports, legacy migration, immutable hours,200 profiles/zoom and100x:",json.dumps(summary),flush=True)
subprocess.run(["dotnet","run","--project","tools/NativeSmokeFixture","-c","Release","--","inspect",str(folder)],check=True)
