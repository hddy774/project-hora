extends SceneTree
## Regression for modal/navigation replacement during actual native GUI dispatch.
## Button signals emitted directly do not exercise the viewport's input stack.
const Main = preload("res://ui/main.tscn")
const Economy = preload("res://core/economy.gd")
var failures: Array = []
var clicks: int = 0
var app: Control

class ErrorCapture extends Logger:
	var entries: Array = []
	var guard := Mutex.new()
	func _log_error(function: String, file: String, line: int, code: String, rationale: String, _notify: bool, kind: int, _traces: Array[ScriptBacktrace]) -> void:
		if kind == Logger.ERROR_TYPE_WARNING: return
		guard.lock()
		entries.append("%s:%d %s: %s %s" % [file, line, function, code, rationale])
		guard.unlock()
	func snapshot() -> Array:
		guard.lock()
		var result: Array = entries.duplicate()
		guard.unlock()
		return result

func _init() -> void:
	call_deferred("_run")

func _run() -> void:
	root.size = Vector2i(480, 920)
	app = Main.instantiate()
	root.add_child(app)
	await process_frame
	app.world = Economy.new_world(42, {"cities_per_nation":1,"expanded_industries":false})
	app.screen = "game"
	app._render()
	await process_frame
	await process_frame
	var initial_hash: String = Economy.state_hash(app.world)
	var capture := ErrorCapture.new()
	OS.add_logger(capture)
	var previous_accumulation: bool = Input.use_accumulated_input
	Input.use_accumulated_input = false
	for index in range(6):
		var box: VBoxContainer = app._open_modal("실제 입력 회귀 %d" % index)
		var old_modal: Control = app.modal
		var counter: Dictionary = {"called":false}
		var button: Button = app._button(box, "다음 창 열기", func():
			counter.called = true
			app._message("교체한 창", "이전 버튼의 GUI 입력 처리를 마친 뒤 노드를 정리합니다.")
			_expect(old_modal.is_inside_tree(), "Modal detached while its pressed callback was dispatched")
			_expect(not old_modal.is_visible_in_tree(), "Replaced modal remains visible")
		)
		await process_frame
		await process_frame
		await _click(button)
		_expect(counter.called, "Mouse GUI dispatch did not invoke modal replacement")
		var close: Button = _find_button(app.modal, "확인")
		_expect(close != null, "Replacement modal has a real close action")
		if close != null: await _click(close)
		_expect(app.modal == null, "Native close action did not close modal")
	# Successful forms close the modal and then rebuild the background in one event.
	var box: VBoxContainer = app._open_modal("화면 교체")
	var old_modal: Control = app.modal
	var old_frame: Control = app.frame
	var transition: Dictionary = {"called":false}
	var transition_button: Button = app._button(box, "닫고 시장으로", func():
		transition.called = true
		app._close_modal()
		app._go(2,[0,0,0])
		_expect(old_modal.is_inside_tree(), "Render detached the still-dispatched modal")
		_expect(old_frame.is_inside_tree(), "Render detached its previous UI during GUI dispatch")
	)
	await process_frame
	await process_frame
	await _click(transition_button)
	_expect(transition.called and app.outer == 2, "Modal close + page transition failed")
	for index in range(5):
		var navigation: Button = _navigation_button(index)
		_expect(navigation != null, "External navigation button missing")
		if navigation != null: await _click(navigation)
		_expect(app.outer == index, "Mouse navigation selected the wrong external tab")
	Input.use_accumulated_input = previous_accumulation
	OS.remove_logger(capture)
	for entry in capture.snapshot(): failures.append("Native GUI error: " + str(entry))
	_expect(Economy.state_hash(app.world) == initial_hash, "GUI input changed economic state")
	app.queue_free()
	await process_frame
	for failure in failures: push_error(str(failure))
	print("PASS" if failures.is_empty() else "FAIL", ": native GUI dispatch (", clicks, " actual mouse clicks; modal replacement/close, background rebuild, five tabs, no engine errors)")
	quit(0 if failures.is_empty() else 1)

func _click(button: Button) -> void:
	if button == null: return
	var position: Vector2 = button.get_global_rect().get_center()
	var motion := InputEventMouseMotion.new()
	motion.position = position
	motion.global_position = position
	Input.parse_input_event(motion)
	for pressed in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = MOUSE_BUTTON_LEFT
		event.pressed = pressed
		event.position = position
		event.global_position = position
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame
	clicks += 1
	await process_frame
	await process_frame

func _find_button(node: Node, text: String) -> Button:
	if node == null: return null
	for child in node.find_children("*", "Button", true, false):
		if child.text == text: return child
	return null

func _navigation_button(index: int) -> Button:
	for child in app.frame.get_children():
		if child is HBoxContainer and child.get_child_count() == 5:
			var candidate: Node = child.get_child(index)
			if candidate is Button and str(candidate.text).ends_with(str(app.NAV[index])): return candidate
	return null

func _expect(value: bool, message: String) -> void:
	if not value: failures.append(message)
