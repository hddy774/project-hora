extends RefCounted
## Thread-safe observation only. Never store this object in economic state.
## Counts completed checkpoints; it does not estimate elapsed/remaining time.
## Listener callbacks run on the worker and must not access the UI scene tree.
var _mutex: Mutex = Mutex.new()
var _state: Dictionary = {"status":"idle","completed":0,"total":0,"percent":0,"stage":"","label":"","error":"","revision":0,"basis":"completed_work_units"}
var _listener: Callable = Callable()

static func observer(target: Variant = null) -> RefCounted:
	if target is RefCounted and target.has_method("begin") and target.has_method("advance") and target.has_method("finish") and target.has_method("fail") and target.has_method("get_snapshot"): return target
	var result: RefCounted = load("res://core/progress.gd").new()
	if target is Callable: result._listener = target
	return result

func _publish(value: Dictionary) -> void:
	if _listener.is_valid(): _listener.call(value)

func begin(total: int, stage: String = "") -> bool:
	_mutex.lock()
	_state = {"status":"running" if total>0 and total<=9007199254740991 else "failed","completed":0,"total":maxi(0,total),"percent":0,"stage":stage,"label":stage,"error":"" if total>0 and total<=9007199254740991 else "INVALID_PROGRESS_TOTAL","revision":int(_state.revision)+1,"basis":"completed_work_units"}
	var value: Dictionary = _state.duplicate(true)
	_mutex.unlock()
	_publish(value)
	return value.status == "running"

func advance(stage: String = "", steps: int = 1) -> void:
	_mutex.lock()
	if _state.status != "running": _mutex.unlock(); return
	_state.completed = mini(int(_state.total),int(_state.completed)+maxi(0,steps))
	_state.percent = int(int(_state.completed)*100/int(_state.total))
	if stage!="": _state.stage=stage; _state.label=stage
	_state.revision=int(_state.revision)+1
	var value: Dictionary=_state.duplicate(true)
	_mutex.unlock()
	_publish(value)

func set_completed(completed: int, stage: String = "") -> void:
	_mutex.lock()
	if _state.status != "running": _mutex.unlock(); return
	_state.completed = maxi(int(_state.completed),mini(int(_state.total),maxi(0,completed)))
	_state.percent = int(int(_state.completed)*100/int(_state.total))
	if stage!="": _state.stage=stage; _state.label=stage
	_state.revision=int(_state.revision)+1
	var value: Dictionary=_state.duplicate(true)
	_mutex.unlock()
	_publish(value)

func finish(stage: String = "") -> void:
	_mutex.lock()
	if _state.status != "running": _mutex.unlock(); return
	_state.completed=_state.total
	_state.percent=100
	_state.status="completed"
	if stage!="": _state.stage=stage; _state.label=stage
	_state.revision=int(_state.revision)+1
	var value: Dictionary=_state.duplicate(true)
	_mutex.unlock()
	_publish(value)

func fail(error: String, stage: String = "") -> void:
	_mutex.lock()
	if _state.status in ["completed","failed"]: _mutex.unlock(); return
	_state.status="failed"
	_state.error=error
	if stage!="": _state.stage=stage; _state.label=stage
	_state.revision=int(_state.revision)+1
	var value: Dictionary=_state.duplicate(true)
	_mutex.unlock()
	_publish(value)

func get_snapshot() -> Dictionary:
	_mutex.lock()
	var value: Dictionary=_state.duplicate(true)
	_mutex.unlock()
	return value

func snapshot() -> Dictionary:
	return get_snapshot()

func mapped(start: int, span: int, prefix: String = "") -> Callable:
	# Map a child task's measured checkpoint count into an already-begun
	# parent range. Child completion never completes the parent implicitly.
	return func(value: Dictionary) -> void:
		var total: int=maxi(1,int(value.get("total",1)))
		var done: int=clampi(int(value.get("completed",0)),0,total)
		set_completed(start+int(done*maxi(0,span)/total),prefix+str(value.get("stage","")))
		if value.get("status")=="failed": fail(str(value.get("error","WORKER_FAILED")),prefix+str(value.get("stage","")))
