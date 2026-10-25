extends RefCounted
## Progress is an out-of-state observer of real completed work checkpoints.
const Progress=preload("res://core/progress.gd")
const Economy=preload("res://core/economy.gd")
const Generation=preload("res://core/generation.gd")
static var checks: int=0

static func _check(value: bool, reason: String, failures: Array) -> void:
	checks+=1
	if not value:failures.append(reason)

static func _trace_ok(trace: Array, total: int, failures: Array, label: String) -> void:
	var previous: int=-1
	var monotone: bool=not trace.is_empty()
	var exact: bool=true
	for value in trace:
		if int(value.total)!=total or int(value.completed)<previous or int(value.completed)<0 or int(value.completed)>total:monotone=false
		if int(value.percent)!=int(int(value.completed)*100/maxi(1,total)):exact=false
		if value.get("basis")!="completed_work_units":exact=false
		previous=int(value.completed)
	_check(monotone,label+" progress remains monotone within its finite checkpoint total",failures)
	_check(exact,label+" percentages come from completed work rather than elapsed-time estimates",failures)
	_check(not trace.is_empty() and trace[-1].status=="completed" and int(trace[-1].completed)==total and int(trace[-1].percent)==100,label+" reaches100 only after successful completion",failures)

static func run() -> Array:
	return _run(3)

static func run_quick() -> Array:
	return _run(1)

static func _run(months: int) -> Array:
	checks=0
	var failures: Array=[]
	_test_tracker(failures)
	_test_threads(failures)
	_test_core(months,failures)
	print("Thread-safe progress checks: %d; failures: %d"%[checks,failures.size()])
	return failures

static func _test_tracker(failures: Array) -> void:
	var p=Progress.new()
	_check(p.get_snapshot().status=="idle","Fresh observer has no invented completed task",failures)
	_check(p.begin(5,"읽기"),"Known positive checkpoint count begins a task",failures)
	p.advance("1단계")
	p.set_completed(0,"되돌림 요청")
	p.advance("음수 요청",-20)
	_check(int(p.snapshot().completed)==1,"Stale or negative updates never move progress backwards",failures)
	var detached: Dictionary=p.get_snapshot()
	detached.completed=999
	detached.stage="외부 변경"
	_check(int(p.get_snapshot().completed)==1 and p.get_snapshot().stage!="외부 변경","Snapshots never expose mutable observer state",failures)
	p.fail("EXPECTED_FAILURE","작업 실패")
	var failed: Dictionary=p.get_snapshot()
	p.advance("실패 뒤 진행")
	p.finish("거짓 완료")
	_check(p.get_snapshot()==failed and failed.status=="failed" and int(failed.percent)==20,"Failure freezes measured work without inventing100percent success",failures)
	_check(not p.begin(0,"잘못된 총량") and p.get_snapshot().error=="INVALID_PROGRESS_TOTAL","Unknown or zero work totals fail explicitly",failures)
	p.begin(5,"전체")
	var child=Progress.observer(p.mapped(0,3,"하위 · "))
	child.begin(2,"준비")
	child.advance("실행")
	child.finish("완료")
	_check(int(p.get_snapshot().completed)==3 and p.get_snapshot().status=="running","Child completion advances only its parent's measured range",failures)
	p.advance("후속 처리")
	p.finish("전체 완료")
	_check(p.get_snapshot().status=="completed" and int(p.get_snapshot().percent)==100,"Parent completes after all remaining checkpoints",failures)

static func _test_threads(failures: Array) -> void:
	var p=Progress.new()
	p.begin(1000,"동시 작업")
	var threads: Array=[]
	for index in range(4):
		var thread=Thread.new()
		var started: int=thread.start(func() -> void:
			for unit in range(250):p.advance("작업 완료",1)
		)
		_check(started==OK,"Progress producer thread starts",failures)
		threads.append(thread)
	var previous: int=0
	var monotone: bool=true
	var running: bool=true
	while running:
		var value: Dictionary=p.get_snapshot()
		if int(value.completed)<previous or int(value.completed)>1000:monotone=false
		previous=int(value.completed)
		running=false
		for thread in threads:
			if thread.is_alive():running=true
		if running:OS.delay_usec(100)
	for thread in threads:thread.wait_to_finish()
	_check(monotone and int(p.get_snapshot().completed)==1000,"Concurrent producers lose no increments and readers see monotone snapshots",failures)
	p.finish("동시 작업 완료")
	_check(int(p.get_snapshot().percent)==100,"All actual producer increments permit final100percent",failures)

static func _test_core(months: int, failures: Array) -> void:
	var options: Dictionary={"cities_per_nation":1,"expanded_industries":false}
	var world_trace: Array=[]
	var observed: Dictionary=Economy.new_world(42,options,func(value: Dictionary) -> void:world_trace.append(value))
	var plain: Dictionary=Economy.new_world(42,options)
	_check(Economy.state_hash(observed)==Economy.state_hash(plain),"World progress creates no extra assets, people, events, randomness or hash changes",failures)
	_trace_ok(world_trace,Economy.world_progress_steps(options),failures,"World creation")
	_check(world_trace.size()==Economy.world_progress_steps(options)+1,"Each counted generation checkpoint is actually reported",failures)
	var before: String=Economy.state_hash(plain)
	var month_trace: Array=[]
	var with_progress: Dictionary=Economy.resolve_month(plain,func(value: Dictionary) -> void:month_trace.append(value))
	var without_progress: Dictionary=Economy.resolve_month(observed)
	_check(with_progress.ok and without_progress.ok,"Both observed and unobserved monthly workers succeed",failures)
	if not with_progress.ok or not without_progress.ok:return
	_check(Economy.state_hash(with_progress.state)==Economy.state_hash(without_progress.state) and with_progress.state.events==without_progress.state.events,"Month checkpoints never change financial results, event order or state hashes",failures)
	_check(Economy.state_hash(plain)==before,"A progress-enabled monthly worker preserves its input snapshot",failures)
	_trace_ok(month_trace,Economy.turn_progress_steps(),failures,"Month resolution")
	_check(month_trace.size()==Economy.turn_progress_steps()+1,"All28 monthly checkpoint updates correspond to completed stages",failures)
	var preview_trace: Array=[]
	var assessment: Dictionary=Generation.validate(plain,{"preview_months":months},func(value: Dictionary) -> void:preview_trace.append(value))
	_check(assessment.ok and int(assessment.observation.months)==months,"Generation progress measures only actually resolved preview months",failures)
	_check(Economy.state_hash(plain)==before and assessment.fingerprint==before,"Copied preview progress never advances the real start world",failures)
	_trace_ok(preview_trace,Generation.progress_steps(months),failures,"Copied generation preview")
	_check(preview_trace.any(func(v):return str(v.stage).begins_with("예비 관찰 1/")),"Preview relays real nested monthly checkpoints",failures)
	var invalid: Dictionary=plain.duplicate(true)
	invalid.phase="Resolving"
	var invalid_hash: String=Economy.state_hash(invalid)
	var failure_progress=Progress.new()
	var thread=Thread.new()
	_check(thread.start(func() -> Dictionary:return Economy.resolve_month(invalid,failure_progress))==OK,"A failing monthly worker starts with its own observer",failures)
	var failed: Dictionary=thread.wait_to_finish()
	_check(not failed.ok and failure_progress.get_snapshot().status=="failed" and int(failure_progress.get_snapshot().percent)<100,"Worker rejection produces an explicit failed progress state",failures)
	_check(Economy.state_hash(invalid)==invalid_hash and failed.state==invalid,"Worker failure aborts without committing economic changes",failures)
	var bad_generation=Progress.new()
	var rejected: Dictionary=Generation.validate(plain,{"preview_months":-1},bad_generation)
	_check(not rejected.ok and bad_generation.get_snapshot().status=="failed","Invalid generation jobs fail their observer before any fake preview completion",failures)
	_check(Economy.state_hash(plain)==before,"Failed generation progress preserves the original state",failures)
