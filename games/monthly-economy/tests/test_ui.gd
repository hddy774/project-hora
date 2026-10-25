extends SceneTree
## Independent UI navigation/layout smoke test. Does not click economic commands.
## Run: godot --headless --path games/monthly-economy --script tests/test_ui.gd

const Economy = preload("res://core/economy.gd")
const Main = preload("res://ui/main.tscn")
var failures: Array = []
var visited: int = 0
var app: Control

func _init() -> void:
	call_deferred("_run")

func _run() -> void:
	print("UI smoke: starting")
	root.size = Vector2i(480, 920)
	app = Main.instantiate()
	print("UI smoke: scene instantiated")
	root.add_child(app)
	await process_frame
	print("UI smoke: first frame")
	app.world = Economy.new_world(42)
	app.screen = "game"
	print("UI smoke: world ready")
	var initial_hash: String = Economy.state_hash(app.world)
	_check_tree(app.NAV, app.TREE)
	for scale_value: float in [1.0, 1.3]:
		app.settings["text_scale"] = scale_value
		app._apply_theme()
		await _visit_all("initial, text scale %.2f" % scale_value)
		_expect(Economy.state_hash(app.world) == initial_hash, "UI navigation changed world state at text scale " + str(scale_value))
	var result: Dictionary = Economy.resolve_month(app.world)
	if result.get("ok", false):
		app.world = result["state"]
		var resolved_hash: String = Economy.state_hash(app.world)
		await _visit_all("resolved month, text scale 1.3")
		_expect(Economy.state_hash(app.world) == resolved_hash, "UI navigation changed resolved world state")
	else:
		failures.append("Cannot test populated UI: month resolution failed " + str(result.get("errors", [])))
	app.queue_free()
	await process_frame
	for failure: Variant in failures:
		push_error(str(failure))
	if failures.is_empty():
		print("PASS: UI navigation smoke test (%d leaf views at 480x920, default/1.3 text, no economic mutation)" % visited)
	else:
		print("FAIL: UI navigation smoke test: %d failures across %d leaf visits" % [failures.size(), visited])
	quit(0 if failures.is_empty() else 1)

func _check_tree(nav: Array, tree: Array) -> void:
	_expect(nav.size() == 5, "External navigation must contain exactly five tabs")
	_expect(str(nav[-1]) == "확장", "Rightmost external tab must be 확장")
	_expect(tree.size() == 5, "Each external tab must register its navigation tree")
	for branch: Variant in tree:
		_check_branch(branch, 1)

func _check_branch(branch: Variant, depth: int) -> void:
	_expect(depth <= 3, "Internal navigation exceeds maximum depth 3")
	if branch is Dictionary:
		_expect(branch.size() >= 3, "An internal tab group has fewer than three choices")
		for child: Variant in branch.values():
			_check_branch(child, depth + 1)
	elif branch is Array:
		_expect(branch.size() >= 3, "A leaf tab group has fewer than three choices")
		for leaf: Variant in branch:
			_expect(leaf is String, "Leaf tab unexpectedly contains another navigation layer")
	else:
		failures.append("Invalid navigation branch type")

func _leaf_paths(branch: Variant, prefix: Array = []) -> Array:
	var result: Array = []
	if branch is Dictionary:
		var keys: Array = branch.keys()
		for index: int in range(keys.size()):
			var next: Array = prefix.duplicate()
			next.append(index)
			result.append_array(_leaf_paths(branch[keys[index]], next))
	elif branch is Array:
		for index: int in range(branch.size()):
			var path: Array = prefix.duplicate()
			path.append(index)
			while path.size() < 3:
				path.append(0)
			result.append(path)
	return result

func _visit_all(context: String) -> void:
	for tab_index: int in range(app.NAV.size()):
		var leaf_paths: Array = _leaf_paths(app.TREE[tab_index])
		for path: Array in leaf_paths:
			app._go(tab_index, path)
			await process_frame
			await process_frame
			visited += 1
			if visited % 15 == 0: print("UI smoke: %d leaf visits" % visited)
			var location: String = "%s / %s / %s" % [context, str(app.NAV[tab_index]), str(app._active_path())]
			_check_layout(location)
			_check_footer(location)

func _check_layout(context: String) -> void:
	var available_width: float = float(root.size.x)
	_expect(app.frame.size.x <= available_width + 1.0, context + ": outer frame overflows viewport (%.1f > %.1f)" % [app.frame.size.x, available_width])
	_expect(app.frame.get_global_rect().end.y <= float(root.size.y) + 1.0, context + ": fixed bottom controls overflow viewport height")
	_expect(app.scroll.size.y >= 120.0, context + ": content viewport is too short (%.1f)" % app.scroll.size.y)
	_expect(app.content.size.x <= app.scroll.size.x + 1.0, context + ": content overflows disabled horizontal scroll (%.1f > %.1f)" % [app.content.size.x, app.scroll.size.x])
	for raw_node: Node in app.content.find_children("*", "Control", true, false):
		var control: Control = raw_node as Control
		if not control.is_visible_in_tree():
			continue
		# Vertical body overflow is expected and accessible through its scroll view.
		var relative_end: float = control.global_position.x + control.size.x - app.content.global_position.x
		_expect(relative_end <= app.content.size.x + 2.0, context + ": body control extends past content edge: " + str(control.get_path()))

func _check_footer(context: String) -> void:
	var external_row: HBoxContainer = null
	var next_month: Button = null
	for child: Node in app.frame.get_children():
		if child is HBoxContainer and child.get_child_count() == 5:
			var all_buttons: bool = true
			for item: Node in child.get_children():
				if not item is Button:
					all_buttons = false
			if all_buttons:
				external_row = child
		elif child is Button and str(child.text).begins_with("다음 달"):
			next_month = child
	_expect(external_row != null, context + ": five-button external navigation row missing")
	_expect(next_month != null, context + ": next-month button must be a separate action")
	if external_row != null:
		for index: int in range(5):
			_expect(str(external_row.get_child(index).text).ends_with(str(app.NAV[index])), context + ": external navigation order differs")
	if external_row != null and next_month != null:
		_expect(next_month.get_parent() != external_row, context + ": next-month action became a sixth external tab")
		_expect(next_month.global_position.y + next_month.size.y <= external_row.global_position.y + 1.0, context + ": next-month action is not above external tabs")

func _expect(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
