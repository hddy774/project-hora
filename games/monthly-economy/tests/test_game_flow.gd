extends SceneTree
## Actual UI callbacks, asynchronous monthly commit, construction and save/load.
## Run separately from core suites:
## godot --headless --path games/monthly-economy --script tests/test_game_flow.gd
## Existing autosave/test-slot bytes are restored before this process exits.

const Economy = preload("res://core/economy.gd")
const Store = preload("res://core/save_store.gd")
const Main = preload("res://ui/main.tscn")
const TEST_SLOT: String = "ui_flow"
const TIMEOUT_MSEC: int = 20000

var failures: Array = []
var preserved: Dictionary = {}
var app: Control
var checks: int = 0

func _init() -> void:
	Engine.max_fps = 60
	call_deferred("_run")

func _run() -> void:
	_preserve_files()
	root.size = Vector2i(480, 920)
	app = Main.instantiate()
	root.add_child(app)
	await process_frame
	app.world = Economy.new_world(42, {"cities_per_nation": 1, "expanded_industries": false})
	app.screen = "game"
	app._render()
	await process_frame
	await process_frame
	print("UI flow: new world and actual command submission")
	var initial_facilities: int = app.world.facilities.size()
	var queue_result: Dictionary = app._queue("build", {"kind": "bakery", "x": 6, "y": 6, "city_id": "city-0"})
	_expect(queue_result.get("ok", false), "UI can queue a legal building command: " + str(queue_result.get("error", "")))
	_expect(app.world.commands.size() == 1, "Queued build is a pending command")
	_expect(app.world.facilities.size() == initial_facilities, "Queueing construction does not instantly create a facility")
	_expect(app.world.projects.is_empty(), "Planning does not secretly start the construction project")
	var project_id: String = ""
	if not app.world.commands.is_empty():
		project_id = "project-" + str(app.world.commands[0].id)
	var facility_id: String = "facility-" + project_id
	# Keep a real pending-command withdrawal button behind the resolving modal.
	app._go(1, [2, 1, 0])
	await process_frame
	await process_frame
	_expect(_background_button("철회") != null, "Queued-build fixture exposes a real withdrawal callback")
	var month_one_ok: bool = await _advance_using_modal()
	if month_one_ok:
		var project: Dictionary = _find(app.world.projects, project_id)
		_expect(not project.is_empty(), "Monthly UI resolution creates the queued project")
		_expect(project.get("status", "") == "active", "First-month project remains under construction")
		_expect(int(project.get("progress", 0)) > 0 and int(project.get("progress", 0)) < int(project.get("required_work", 0)), "First month consumes actual work without bypassing the construction period")
		_expect(_find(app.world.facilities, facility_id).is_empty(), "Unfinished project cannot produce through an early facility")
		_check_report_without_advancing()
		_check_autosave()
		var month_two_ok: bool = await _advance_using_modal()
		if month_two_ok:
			project = _find(app.world.projects, project_id)
			var facility: Dictionary = _find(app.world.facilities, facility_id)
			_expect(project.get("status", "") == "completed", "Supplied project completes after its minimum construction period")
			_expect(int(project.get("completed_month", 0)) == 2, "Project records its actual completion month")
			_expect(facility.get("status", "") == "ready", "Completed facility awaits its next operating month")
			_expect(int(facility.get("operational_month", 0)) == 3, "Facility eligibility starts in the month after completion")
			_check_no_early_production(facility_id, 3)
			_check_report_without_advancing()
			var month_three_ok: bool = await _advance_using_modal()
			if month_three_ok:
				facility = _find(app.world.facilities, facility_id)
				_expect(facility.get("status", "") == "operational", "Next month makes the completed facility operational; output still requires available inputs and labor")
				_check_no_early_production(facility_id, 3)
				_check_report_without_advancing()
				_check_autosave()
				await _check_pending_plan_save_load()
	_cleanup()
	await process_frame
	for failure: Variant in failures:
		push_error(str(failure))
	if failures.is_empty():
		print("PASS: real UI game flow (%d checks; queued build, locked planning/withdrawal/navigation, three guarded monthly callbacks, report replay, autosave, pending-plan/manual-view restoration)" % checks)
	else:
		print("FAIL: real UI game flow (%d failures in %d checks)" % [failures.size(), checks])
	quit(0 if failures.is_empty() else 1)

func _advance_using_modal() -> bool:
	var previous_month: int = int(app.world.planning_month)
	var previous_version: int = int(app.world.version)
	var immutable_hash: String = Economy.state_hash(app.world)
	var command_count: int = app.world.commands.size()
	var production_policy: int = int(_find(app.world.companies, str(app.world.controlled_company_id)).policy.production_percent)
	app._preflight()
	await process_frame
	var start: Button = _modal_button("1개월 정산 시작")
	_expect(start != null, "Preflight exposes the real monthly-resolution callback")
	if start == null:
		return false
	start.pressed.emit()
	_expect(app.resolving, "The modal callback enters locked monthly resolution")
	if not app.resolving:
		return false
	var first_worker: Thread = app.worker
	var blocked: Dictionary = app._queue("set_policy", {"production_percent": 80})
	_expect(not blocked.get("ok", false) and blocked.get("error", "") == "NOT_PLANNING", "UI rejects new planning commands while a monthly worker is running")
	_expect(app.world.commands.size() == command_count, "Rejected in-flight planning cannot add a command that would be lost at commit")
	var withdrawal: Button = _background_button("철회")
	if withdrawal != null:
		withdrawal.pressed.emit()
		_expect(app.world.commands.size() == command_count, "Even a programmatic background withdrawal signal cannot remove the locked build")
	var previous_outer: int = int(app.outer)
	var previous_paths: Array = app.paths.duplicate(true)
	app._go((previous_outer + 1) % 5, [0, 0, 0])
	_expect(int(app.outer) == previous_outer and app.paths == previous_paths, "Background navigation cannot replace the locked planning view")
	var buttons: Array = app.frame.find_children("*", "BaseButton", true, false)
	_expect(not buttons.is_empty(), "Resolving-lock fixture contains real background controls")
	for button: BaseButton in buttons:
		_expect(button.disabled, "Every background plan button is disabled while resolving")
		_expect(button.focus_mode == Control.FOCUS_NONE, "Disabled plan buttons cannot be reached through keyboard focus")
	app._resolve()
	_expect(app.worker == first_worker, "Repeated next-month activation keeps the same worker")
	_expect(Economy.state_hash(app.world) == immutable_hash, "Background resolution leaves the visible committed input immutable")
	var started: int = Time.get_ticks_msec()
	while app.resolving and Time.get_ticks_msec() - started < TIMEOUT_MSEC:
		await process_frame
	if app.resolving:
		failures.append("Monthly UI callback exceeded 20-second test timeout")
		# Avoid abandoning a worker that still references this scene. A timeout is
		# already a failure; join its finite work so files can still be restored.
		if app.worker != null:
			app.worker.wait_to_finish()
			app.worker = null
		app.resolving = false
		return false
	_expect(int(app.world.planning_month) == previous_month + 1, "A double activation advances exactly one planning month")
	_expect(int(app.world.last_resolved_month) == previous_month, "The visible result records the resolved month")
	_expect(int(app.world.version) == previous_version + 1, "A monthly UI commit increments the world version once")
	_expect(app.worker == null, "UI joins and clears its completed worker")
	_expect(is_instance_valid(app.modal), "Successful monthly commit displays its report")
	_expect(_modal_button("다음 계획 열기", true) != null, "Commit report exposes the next planning action")
	_expect(Economy.validate(app.world).is_empty(), "Visible monthly result passes the economic invariants")
	_expect(int(_find(app.world.companies, str(app.world.controlled_company_id)).policy.production_percent) == production_policy, "Rejected in-flight policy never appears in the committed company")
	print("UI flow: committed month %d through modal callback" % previous_month)
	return int(app.world.planning_month) == previous_month + 1

func _check_report_without_advancing() -> void:
	var month: int = int(app.world.planning_month)
	var version: int = int(app.world.version)
	var journal_count: int = app.world.journal.size()
	var events_count: int = app.world.events.size()
	# Use the actual report action; it acknowledges presentation state and saves
	# without repeating production, construction, wages, or payments.
	var open_plan: Button = _modal_button("다음 계획 열기", true)
	if open_plan != null:
		open_plan.pressed.emit()
	else:
		failures.append("Report acknowledgement button missing")
	var acknowledged_hash: String = Economy.state_hash(app.world)
	app._show_report(app.world.last_report)
	_expect(is_instance_valid(app.modal), "An existing report can be reopened")
	app._close_modal()
	app._show_report(app.world.last_report)
	app._close_modal()
	_expect(Economy.state_hash(app.world) == acknowledged_hash, "Reopening and closing an acknowledged report does not mutate the economy")
	_expect(int(app.world.planning_month) == month and int(app.world.version) == version, "Report replay never advances the month/version")
	_expect(app.world.journal.size() == journal_count and app.world.events.size() == events_count, "Report replay never duplicates payments or economic events")

func _check_autosave() -> void:
	var loaded: Dictionary = Store.load_world("autosave")
	_expect(loaded.get("ok", false), "UI commit produced a valid autosave: " + str(loaded.get("error", "")))
	if loaded.get("ok", false):
		_expect(int(loaded.state.planning_month) == int(app.world.planning_month), "Autosave contains the next planning month")
		_expect(int(loaded.state.last_resolved_month) == int(app.world.last_resolved_month), "Autosave contains its resolved month in the same snapshot")
		_expect(Economy.state_hash(loaded.state) == Economy.state_hash(app.world), "Autosave reload preserves the committed economic state")

func _check_pending_plan_save_load() -> void:
	print("UI flow: pending-plan save, view restore, title return and reload")
	app._go(0, [1, 2, 0])
	await process_frame
	await process_frame
	app.scroll.scroll_vertical = 80
	await process_frame
	await process_frame
	var saved_scroll: int = int(app.scroll.scroll_vertical)
	_expect(saved_scroll > 0, "View restoration fixture actually contains a scrolled body")
	var result: Dictionary = app._queue("set_policy", {"production_percent": 80})
	_expect(result.get("ok", false), "A new policy can be queued in the next planning month")
	var pending_hash: String = Economy.state_hash(app.world)
	var saved_month: int = int(app.world.planning_month)
	var saved_paths: Array = app.paths.duplicate(true)
	var saved_outer: int = int(app.outer)
	_expect(app._save(TEST_SLOT, false), "UI manual save preserves a pending plan")
	var loaded: Dictionary = Store.load_world(TEST_SLOT)
	_expect(loaded.get("ok", false), "Manual slot passes validation")
	if loaded.get("ok", false):
		_expect(Economy.state_hash(loaded.state) == pending_hash, "Saved pending command remains unexecuted and unchanged")
		_expect(loaded.state.commands.size() == app.world.commands.size(), "Pending commands are restored exactly once")
	app._go(4, [2, 2, 0])
	app._load(TEST_SLOT)
	await process_frame
	await process_frame
	_expect(Economy.state_hash(app.world) == pending_hash, "Actual UI load restores the planned state without resolving it")
	_expect(int(app.outer) == saved_outer and app.paths == saved_paths, "Manual load restores the selected outer/inner navigation context")
	_expect(int(app.scroll.scroll_vertical) == saved_scroll, "Manual load restores the selected leaf view scroll position")
	_expect(int(app.world.planning_month) == saved_month, "Manual load cannot advance the planning month")
	app._leave("title")
	await process_frame
	_expect(app.screen == "title", "Session can return to title")
	app._load(TEST_SLOT)
	await process_frame
	_expect(app.screen == "game", "Saved session can be loaded from title")
	_expect(Economy.state_hash(app.world) == pending_hash, "Returning to title does not erase the saved pending plan")
	app._new_game_dialog()
	app._close_modal()
	_expect(Economy.state_hash(app.world) == pending_hash, "Cancelling new-game setup preserves the current session")

func _check_no_early_production(facility_id: String, eligible_month: int) -> void:
	for event: Dictionary in app.world.events:
		if str(event.get("facility_id", "")) == facility_id and str(event.get("type", "")) == "production":
			_expect(int(event.get("month", 0)) >= eligible_month, "A newly completed building has no production before its eligible month")

func _background_button(text: String) -> Button:
	if not is_instance_valid(app.frame):
		return null
	for node: Node in app.frame.find_children("*", "Button", true, false):
		if str(node.text) == text:
			return node as Button
	return null

func _modal_button(text: String, prefix: bool = false) -> Button:
	if not is_instance_valid(app.modal):
		return null
	for node: Node in app.modal.find_children("*", "Button", true, false):
		if (prefix and str(node.text).begins_with(text)) or (not prefix and str(node.text) == text):
			return node as Button
	return null

func _find(items: Array, id: String) -> Dictionary:
	for item: Dictionary in items:
		if str(item.get("id", "")) == id:
			return item
	return {}

func _preserve_files() -> void:
	for slot: String in ["autosave", TEST_SLOT]:
		for suffix: String in [".json", ".json.bak", ".json.tmp", ".json.bak.tmp"]:
			var path: String = "user://saves/" + slot + suffix
			preserved[path] = {"exists": FileAccess.file_exists(path), "bytes": FileAccess.get_file_as_bytes(path) if FileAccess.file_exists(path) else PackedByteArray()}
		var view_path: String = "user://view-" + slot + ".json"
		preserved[view_path] = {"exists": FileAccess.file_exists(view_path), "bytes": FileAccess.get_file_as_bytes(view_path) if FileAccess.file_exists(view_path) else PackedByteArray()}

func _cleanup() -> void:
	if is_instance_valid(app):
		app.queue_free()
	for path: String in preserved:
		var original: Dictionary = preserved[path]
		if original.exists:
			var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
			if file == null:
				failures.append("Could not restore pre-test local file " + path)
			else:
				file.store_buffer(original.bytes)
				file.flush()
				file.close()
				_expect(FileAccess.get_file_as_bytes(path) == original.bytes, "Pre-existing local file restored byte-for-byte: " + path)
		elif FileAccess.file_exists(path):
			var error: Error = DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
			_expect(error == OK, "Test-created temporary slot removed: " + path)

func _expect(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
