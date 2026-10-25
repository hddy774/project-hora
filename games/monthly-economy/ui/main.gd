extends Control

const Economy = preload("res://core/economy.gd")
const Ledger = preload("res://core/ledger.gd")
const Store = preload("res://core/save_store.gd")
const CityMap = preload("res://ui/city_map.gd")
const Art = preload("res://ui/art.gd")
const Domains = preload("res://ui/domain_views.gd")
const HistoryChart = preload("res://ui/history_chart.gd")
const FONT = preload("res://assets/NotoSansKR.otf")
const BG = Color("071724")
const PANEL = Color("0d2233")
const PANEL_LIGHT = Color("132c41")
const TEXT = Color("edf6ff")
const MUTED = Color("96aec5")
const MINT = Color("45baff")
const GOLD = Color("efc37b")
const RED = Color("f09b98")
const NAV = ["메인", "사업", "시장", "자산", "확장"]
const TREE = [
	{"현황": {"기업": ["요약", "생산", "판매"], "거시경제": ["생산", "고용", "물가"], "산업": ["수급", "경쟁", "병목"]}, "도시": ["지도", "시설", "인구"], "알림": ["작업", "경영", "사건"]},
	{"회사": ["법인", "조직", "전략"], "운영": ["생산", "유통", "정책"], "프로젝트": ["건설", "계획", "연구개발"]},
	{"상품": ["가격", "주문", "교역"], "인력": ["구직", "채용", "임금"], "금융": ["은행", "투자", "보험"]},
	{"개인": ["자금", "직위", "채무"], "법인": ["지분", "경영권", "재무"], "부동산": ["토지", "건물", "임대"]},
	{"세계": ["대륙", "국가", "도시"], "정치외교": ["국내정치", "국제관계", "공공조달"], "게임관리": ["저장", "설정", "정보"]}
]
var world: Dictionary = {}
var screen: String = "title"
var outer: int = 0
var paths: Array = [[0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0]]
var city_id: String = "city-0"
var scope: String = "city-0"
var frame: VBoxContainer
var content: VBoxContainer
var scroll: ScrollContainer
var modal: Control
var toast_label: Label
var worker: Thread
var generator: Thread
var generation_cancelled: bool = false
var pending_practice: bool = false
var practice_mode: bool = false
var practice_steps: Dictionary = {}
var goal_mode: String = "none"
var resolving: bool = false
var dirty: bool = false
var build_kind: String = "bakery"
var selected_tile: Vector2i = Vector2i(-1, -1)
var build_mode: bool = false
var full_map: bool = false
var settings: Dictionary = {"text_scale": 1.0, "high_contrast": false, "reduce_motion": true}
var scroll_positions: Dictionary = {}
var last_screen_key: String = ""
var saved_hash: String = ""
var fatal_error: String = ""
var context_company_id: String = "__default__"
var context_government_id: String = ""
var last_selected_id: String = ""
var last_map: Control
var file_dialog: FileDialog


func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	Engine.max_fps = 30
	get_tree().auto_accept_quit = false
	var saved_settings: Dictionary = _load_preferences()
	settings.merge(saved_settings, true)
	_apply_theme()
	var content_status: Dictionary = Economy.fixture_status()
	if not content_status.get("ok", false):
		fatal_error = str(content_status.get("error", "필수 데이터 없음"))
		_render()
		return
	if "--demo" in OS.get_cmdline_user_args():
		world = Economy.new_world(42)
		screen = "game"
	_render()


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST or what == NOTIFICATION_WM_GO_BACK_REQUEST:
		_back()
	if what == NOTIFICATION_APPLICATION_PAUSED and not world.is_empty() and not resolving:
		_save("recovery", false)


func _process(_delta: float) -> void:
	if generator != null and not generator.is_alive():
		var generated: Dictionary = generator.wait_to_finish()
		generator = null
		if not generation_cancelled:
			_generation_result_dialog(generated.get("world", {}), generated.get("assessment", {"ok":false,"errors":["생성 검사 결과가 없습니다."]}))
	if resolving and worker != null and not worker.is_alive():
		var result: Dictionary = worker.wait_to_finish()
		worker = null
		resolving = false
		_close_modal()
		if not result.get("ok", false):
			_render()
			_message("정산을 확정하지 않았습니다", "이전 월과 계획을 보존했습니다.\n" + str(result.get("errors", [])))
			return
		var candidate: Dictionary = result["state"]
		var save_result: Dictionary = Store.save_world(candidate, _storage_slot("autosave"))
		if not save_result.get("ok", false):
			_render()
			_message("저장 공간을 확인하세요", "월 결과를 저장하지 못해 이전 월과 계획을 유지했습니다.\n" + str(save_result.get("error", "저장 실패")))
			return
		world = candidate
		dirty = false
		_save_view(_storage_slot("autosave"))
		saved_hash = Economy.state_hash(world)
		_render()
		_show_report(result.get("report", {}))


func _apply_theme() -> void:
	var t := Theme.new()
	t.default_font = FONT
	t.default_font_size = int(15 * float(settings.get("text_scale", 1.0)))
	t.set_color("font_color", "Label", TEXT)
	for type_name: String in ["Button", "OptionButton", "CheckButton"]:
		t.set_color("font_color", type_name, TEXT)
		t.set_color("font_hover_color", type_name, Color.WHITE)
		t.set_color("font_pressed_color", type_name, Color.WHITE)
		t.set_color("font_disabled_color", type_name, Color("647c94"))
		t.set_stylebox("normal", type_name, Art.style("button", PANEL_LIGHT))
		t.set_stylebox("hover", type_name, Art.style("button_active", Color("1764a3")))
		t.set_stylebox("pressed", type_name, Art.style("button_active", Color("087be1")))
		t.set_stylebox("disabled", type_name, Art.style("button", PANEL))
		t.set_stylebox("focus", type_name, _style(Color.TRANSPARENT, 10, MINT))
	t.set_stylebox("normal", "LineEdit", Art.style("panel", BG, Color("34566f"), 10))
	t.set_color("font_color", "LineEdit", TEXT)
	t.set_stylebox("panel", "PopupMenu", Art.style("panel", PANEL))
	t.set_color("font_color", "PopupMenu", TEXT)
	t.set_constant("separation", "VBoxContainer", 9)
	t.set_constant("separation", "HBoxContainer", 8)
	theme = t


func _style(color: Color, radius: int = 12, border: Color = Color.TRANSPARENT) -> StyleBox:
	if color.a <= 0.01:
		var clear := StyleBoxFlat.new()
		clear.bg_color = color
		clear.set_corner_radius_all(radius)
		clear.set_content_margin_all(8)
		if border.a > 0:
			clear.set_border_width_all(1)
			clear.border_color = border
		return clear
	var id: String = "panel" if color == PANEL else "button"
	if color.b > 0.5 or color.g > 0.55: id = "button_active"
	return Art.style(id, color, border, 10)


func _render() -> void:
	if is_instance_valid(scroll) and last_screen_key != "":
		scroll_positions[last_screen_key] = scroll.scroll_vertical
	for c in get_children():
		remove_child(c)
		c.queue_free()
	modal = null
	toast_label = null
	var background := TextureRect.new()
	background.texture = Art.texture("background")
	background.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	background.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	background.modulate = Color.WHITE if not settings.get("high_contrast", false) else Color("263443")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	var safe := MarginContainer.new()
	safe.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var margins: Dictionary = _display_margins()
	safe.add_theme_constant_override("margin_left", margins.left)
	safe.add_theme_constant_override("margin_right", margins.right)
	safe.add_theme_constant_override("margin_top", margins.top)
	safe.add_theme_constant_override("margin_bottom", margins.bottom)
	add_child(safe)
	frame = VBoxContainer.new()
	frame.add_theme_constant_override("separation", 7)
	safe.add_child(frame)
	if fatal_error != "":
		_label(frame, "필수 게임 데이터를 확인할 수 없습니다", 25, GOLD)
		_label(frame, "이 빌드를 다시 내려받아 설치하세요. 기존 정상 저장 파일은 변경하지 않았습니다.\n\n검사 결과: " + fatal_error, 15, MUTED)
		_button(frame, "종료", func(): get_tree().quit())
		return
	if screen == "title":
		_title()
		return
	_header()
	_navigation()
	scroll = ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	frame.add_child(scroll)
	content = VBoxContainer.new()
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	content.add_theme_constant_override("separation", 9)
	scroll.add_child(content)
	last_screen_key = str(outer) + str(paths[outer]) + city_id
	var active: Array = _active_path()
	if practice_mode and outer == 1 and active[0] == "운영" and active[-1] == "생산": practice_steps["inspect"] = true
	if practice_mode: _practice_panel()
	elif goal_mode != "none": _goal_panel()
	_draw_page(active)
	_footer()
	var previous_scroll: int = int(scroll_positions.get(last_screen_key, 0))
	scroll.set_deferred("scroll_vertical", previous_scroll)


func _label(parent: Node, text: String, size_value: int = 16, color: Color = TEXT) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", int(size_value * float(settings.get("text_scale", 1.0))))
	l.add_theme_color_override("font_color", color)
	l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(l)
	return l


func _display_margins() -> Dictionary:
	var result: Dictionary = {"left": 8, "right": 8, "top": 10, "bottom": 6}
	if OS.get_name() not in ["Android", "iOS"]: return result
	var area: Rect2i = DisplayServer.get_display_safe_area()
	var physical: Vector2i = DisplayServer.screen_get_size()
	var logical: Vector2 = get_viewport_rect().size
	if physical.x <= 0 or physical.y <= 0 or area.size.x <= 0 or area.size.y <= 0: return result
	result.left = maxi(16, int(ceil(float(area.position.x) * logical.x / physical.x)))
	result.right = maxi(16, int(ceil(float(physical.x - area.end.x) * logical.x / physical.x)))
	result.top = maxi(16, int(ceil(float(area.position.y) * logical.y / physical.y)))
	result.bottom = maxi(12, int(ceil(float(physical.y - area.end.y) * logical.y / physical.y)))
	return result


func _button(parent: Node, text: String, action: Callable, accent: bool = false) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size.y = 43
	b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	b.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	if accent:
		b.add_theme_stylebox_override("normal", Art.style("button_primary", Color("087fe4"), MINT, 10))
		b.add_theme_color_override("font_color", Color.WHITE)
		b.add_theme_color_override("font_hover_color", Color.WHITE)
	b.pressed.connect(func():
		if resolving: return
		if settings.get("haptics", false) and OS.get_name() == "Android": Input.vibrate_handheld(12)
		action.call()
	)
	parent.add_child(b)
	return b


func _card(parent: Node, heading: String = "", eyebrow: String = "") -> VBoxContainer:
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", Art.style("panel", PANEL, Color("25445c"), 10))
	parent.add_child(panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	panel.add_child(box)
	if eyebrow != "" and outer != 0: _label(box, eyebrow, 11, MINT)
	if heading != "": _label(box, heading, 19)
	return box


func _row(parent: Node, name_value: String, value: String, color: Color = TEXT) -> void:
	var row := HBoxContainer.new()
	parent.add_child(row)
	_label(row, name_value, 14, MUTED)
	var v: Label = _label(row, value, 15, color)
	v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT


func _money(amount: int) -> String:
	var sign_value: String = "−" if amount < 0 else ""
	var n: int = absi(amount)
	# CR is the fixture currency. Values below are minor units, never a float balance.
	return sign_value + str(n / 100) + "." + ("0" if n % 100 < 10 else "") + str(n % 100) + " CR"


func _month() -> String:
	var month: int = int(world.get("planning_month", 1))
	return "Y%02d · %02d월" % [1 + int((month - 1) / 12.0), 1 + (month - 1) % 12]


func _company() -> Dictionary:
	var id: String = str(world.get("controlled_company_id", "")) if context_company_id == "__default__" else context_company_id
	for company: Dictionary in _represented_companies():
		if str(company.get("id", "")) == id: return company
	return {}


func _person() -> Dictionary:
	return _find(world.get("people", []), str(world.get("player_id", "p-0")))


func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if str(item.get("id", "")) == id:
			return item
	return {}


func _title() -> void:
	var title_scroll := ScrollContainer.new()
	title_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	title_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	frame.add_child(title_scroll)
	var box := VBoxContainer.new()
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_theme_constant_override("separation", 14)
	title_scroll.add_child(box)
	_icon(box, "app_icon", 76)
	_label(box, "Hora Economic Lab", 21, MINT)
	_label(box, "호라경제연구실", 34)
	_label(box, "한 달의 결정, 서로 연결된 세계.", 20, GOLD)
	_label(box, "사람이 일하고 기업이 생산하며 도시가 거래합니다.\n당신의 진로와 경제의 변화를 직접 선택하세요.", 15, MUTED)
	var auto_result: Dictionary = Store.load_world("autosave")
	var continue_button: Button = _button(box, "계속하기", func(): _load("autosave"), true)
	continue_button.icon = Art.texture("load")
	continue_button.expand_icon = true
	continue_button.add_theme_constant_override("icon_max_width", 24)
	continue_button.disabled = not auto_result.get("ok", false)
	if auto_result.get("ok", false):
		var previous: Dictionary = auto_result.state
		_label(box, "자동 저장 · 계획 %d개월차" % int(previous.get("planning_month", 1)), 12, MUTED)
	else: _label(box, "계속할 수 있는 정상 자동 저장이 아직 없습니다.", 12, MUTED)
	_button(box, "새 게임", _new_game_dialog)
	_button(box, "불러오기", _load_dialog)
	_button(box, "학습과 도움말", _tutorial_dialog)
	var row := HBoxContainer.new()
	box.add_child(row)
	_button(row, "설정", _settings_dialog)
	_button(row, "게임 정보", _info_dialog)
	_button(box, "종료", func(): get_tree().quit())
	_label(box, str(ProjectSettings.get_setting("application/config/version", "0.2.0")) + " · 오프라인 싱글플레이 · 월 단위 수동 진행", 11, MUTED)


func _header() -> void:
	var top := HBoxContainer.new()
	top.add_theme_constant_override("separation", 12)
	frame.add_child(top)
	var title: Label = _label(top, NAV[outer], 24, TEXT)
	title.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	title.custom_minimum_size.x = 60 * float(settings.get("text_scale", 1.0))
	var date: Label = _label(top, _month(), 15, MUTED)
	date.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	var status := HBoxContainer.new()
	status.add_theme_constant_override("separation", 5)
	top.add_child(status)
	_icon(status, "calendar", 22)
	_label(status, "정산 중" if resolving else "계획 중", 14, MINT).custom_minimum_size.x = 52
	var context := HBoxContainer.new()
	context.add_theme_constant_override("separation", 7)
	frame.add_child(context)
	var person_name: String = str(_person().get("name", "내 인물"))
	var company_name: String = str(_government().get("name", _company().get("name", "개인 활동")))
	var button: Button = _button(context, person_name + " · " + company_name + "  ⌄", _context_dialog)
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.clip_text = true
	button.add_theme_stylebox_override("normal", Art.style("button", PANEL_LIGHT, Color.TRANSPARENT, 5))
	button.custom_minimum_size = Vector2(0, 31)
	button.add_theme_font_size_override("font_size", 12)
	var chooser := OptionButton.new()
	chooser.fit_to_longest_item = false
	chooser.clip_text = true
	chooser.custom_minimum_size = Vector2(90, 31)
	chooser.add_theme_stylebox_override("normal", Art.style("button", PANEL_LIGHT, Color.TRANSPARENT, 5))
	chooser.add_theme_font_size_override("font_size", 12)
	var ids: Array = ["world"]
	chooser.add_item("세계 집계")
	for group: String in ["continents","nations","cities"]:
		for entity: Dictionary in world.get(group, []):
			var prefix: String = {"continents":"대륙", "nations":"국가", "cities":"도시"}[group]
			chooser.add_item(prefix + " · " + str(entity.name)); ids.append(str(entity.id))
	chooser.select(maxi(ids.find(scope), 0))
	chooser.item_selected.connect(func(index: int):
		if resolving: return
		scope = ids[index]
		if not _find(world.get("cities",[]),scope).is_empty(): city_id = scope
		_render()
	)
	context.add_child(chooser)


func _navigation() -> void:
	var node: Variant = TREE[outer]
	for depth in range(3):
		if node is Dictionary:
			var keys: Array = node.keys()
			var selected: int = clampi(int(paths[outer][depth]), 0, keys.size() - 1)
			paths[outer][depth] = selected
			_tabs(frame, keys, selected, depth)
			node = node[keys[selected]]
		elif node is Array:
			var selected: int = clampi(int(paths[outer][depth]), 0, node.size() - 1)
			paths[outer][depth] = selected
			_tabs(frame, node, selected, depth)
			break


func _tabs(parent: Node, labels: Array, selected: int, depth: int) -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 5 if depth > 0 else 3)
	parent.add_child(row)
	for i in labels.size():
		var index: int = i
		var button: Button = _button(row, str(labels[i]), func():
			paths[outer][depth] = index
			for j in range(depth + 1, 3): paths[outer][j] = 0
			full_map = false
			_render()
		)
		button.custom_minimum_size.y = 29 if depth > 0 else 38
		button.add_theme_font_size_override("font_size", 13 if depth > 0 else 17)
		var box: StyleBox
		if depth == 0:
			box = _style(Color.TRANSPARENT, 0)
		else:
			box = Art.style("button_active" if i == selected else "button", Color("0876ce") if i == selected else PANEL_LIGHT, Color.TRANSPARENT, 6)
		box.content_margin_top = 4
		box.content_margin_bottom = 4
		button.add_theme_stylebox_override("normal", box)
		button.add_theme_color_override("font_color", Color.WHITE if i == selected else MUTED)
		if depth == 0 and i == selected:
			var stripe := TextureRect.new()
			stripe.texture = Art.texture("underline")
			stripe.mouse_filter = Control.MOUSE_FILTER_IGNORE
			stripe.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
			stripe.offset_top = -4
			stripe.offset_bottom = 0
			button.add_child(stripe)
			if stripe.texture == null:
				var line := ColorRect.new()
				line.color = MINT
				line.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
				line.mouse_filter = Control.MOUSE_FILTER_IGNORE
				stripe.add_child(line)


func _active_path() -> Array:
	var path: Array = []
	var node: Variant = TREE[outer]
	for depth in range(3):
		if node is Dictionary:
			var key: String = str(node.keys()[int(paths[outer][depth])])
			path.append(key)
			node = node[key]
		elif node is Array:
			path.append(str(node[int(paths[outer][depth])]))
			break
	return path


func _footer() -> void:
	var budget_row := HBoxContainer.new()
	budget_row.add_theme_constant_override("separation", 7)
	frame.add_child(budget_row)
	var estimate: Dictionary = _planning_summary()
	var expense := VBoxContainer.new()
	expense.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	budget_row.add_child(expense)
	_label(expense, "계획에 예약한 지급", 11, MUTED)
	_label(expense, _money(int(estimate.get("expected_spending", 0))), 16, GOLD)
	var month_button: Button = _button(budget_row, "다음 달  ›", _preflight, true)
	month_button.name = "NextMonthAction"
	month_button.custom_minimum_size = Vector2(205, 48)
	month_button.size_flags_horizontal = Control.SIZE_SHRINK_END
	month_button.icon = Art.texture("calendar")
	month_button.expand_icon = true
	month_button.add_theme_constant_override("icon_max_width", 24)
	month_button.disabled = resolving
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 3)
	frame.add_child(row)
	var names: Array = ["home", "factory", "chart", "coins", "grid"]
	for i in NAV.size():
		var index: int = i
		var b: Button = _button(row, "\n" + NAV[i], func():
			outer = index
			full_map = false
			_render()
		)
		b.custom_minimum_size.y = 63
		b.add_theme_font_size_override("font_size", 12)
		b.add_theme_stylebox_override("normal", _style(Color.TRANSPARENT, 7))
		b.add_theme_color_override("font_color", MINT if outer == i else MUTED)
		var texture := TextureRect.new()
		texture.texture = Art.texture(names[i])
		texture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		texture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		texture.custom_minimum_size = Vector2(28, 28)
		texture.mouse_filter = Control.MOUSE_FILTER_IGNORE
		texture.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
		texture.offset_left = -14
		texture.offset_right = 14
		texture.offset_top = 4
		texture.offset_bottom = 32
		texture.modulate = MINT if outer == i else Color("8da2bc")
		b.add_child(texture)
	_label(frame, "한 번의 정산은 1개월 · 계획 중에는 시간이 흐르지 않습니다", 10, MUTED).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


func _draw_page(path: Array) -> void:
	var first: String = path[0]
	var leaf: String = path[-1]
	match outer:
		0:
			if first == "도시":
				if leaf == "지도": _map_page()
				elif leaf == "시설": _facility_list(false)
				else: _people_page("인구")
			elif first == "알림": _events_page(leaf)
			elif path[1] == "기업":
				if leaf == "요약": _dashboard()
				elif leaf == "생산": _production_page()
				else: _trade_page()
			elif path[1] == "거시경제":
				if leaf == "고용": _people_page("고용")
				elif leaf == "물가": _market_page("가격")
				else: _aggregate_page()
			else:
				if leaf == "경쟁": _company_list()
				elif leaf == "병목": _events_page("병목")
				else: _market_page("수급")
		1:
			if first == "프로젝트":
				if leaf == "건설": _map_page(true)
				elif leaf == "계획": _plans_page()
				else:
					Domains.page(self, "industry", "연구·소프트웨어·개발", [], "research")
					Domains.records(self, "industry", "software", "소프트웨어 프로젝트")
			elif first == "운영":
				if leaf == "생산": _production_page()
				elif leaf == "유통":
					_inventory_page()
					Domains.page(self, "industry", "물류·창고·재활용", ["ship", "warehouse", "recycle"], "routes")
				else: _policy_page()
			else:
				if leaf == "법인": _company_page()
				elif leaf == "조직":
					_people_page("조직")
					Domains.page(self, "lifecycle", "인사·대표·위임", ["hire", "fire", "appoint_director", "dismiss_director", "delegate", "revoke"], "delegations")
				else:
					Domains.page(self, "lifecycle", "기업 전략·인수·정리", ["subsidiary", "acquisition", "bankruptcy", "liquidation"], "acquisitions")
					_policy_page()
		2:
			if first == "상품":
				if leaf == "주문":
					Domains.page(self, "market", "주문·예약·체결", [], "own_orders")
					_market_order_book()
				elif leaf == "교역": _trade_page()
				else: _market_page(leaf)
			elif first == "인력":
				if leaf == "구직": Domains.page(self, "lifecycle", "구직·교육·이주", ["apply_job", "resign_job", "training", "move_person"], "jobs")
				elif leaf == "채용": Domains.page(self, "lifecycle", "채용·배치", ["post_job", "hire_person", "fire_person"], "jobs")
				else:
					_people_page("임금")
					Domains.records(self,"lifecycle","wage_claims","계약 임금·미지급 의무")
			elif leaf == "은행": Domains.page(self, "finance", "은행·예금·대출·환전", ["borrow", "repay", "deposit", "withdraw", "exchange"], "loans")
			elif leaf == "투자": Domains.page(self, "finance", "주식·채권·배당", ["shares", "equity", "capital", "cancel_offer", "bond", "dividend"], "bonds")
			else: Domains.page(self, "finance", "보험 계약과 청구", ["insure", "claim"], "insurance_policies")
		3:
			if first == "개인": _personal_page(leaf)
			elif first == "법인":
				if leaf == "지분": _ownership_page()
				elif leaf == "경영권": Domains.page(self, "lifecycle", "대표·의결권·위임", ["appoint_director", "dismiss_director", "delegate", "revoke", "subsidiary", "successor"], "delegations")
				else: _journal_page("원장")
			else:
				if leaf == "건물": _facility_list(true)
				else:
					_map_page()
					Domains.page(self, "land", "토지·임대·용도", ["lease", "rent", "claim"] if leaf == "임대" else ["sale", "buy", "change_use", "cancel"], "land_offers")
					Domains.records(self, "land", "land_leases", "사용권과 임대 계약")
		4:
			if first == "세계": _world_page(leaf)
			elif first == "정치외교":
				if leaf == "국내정치": Domains.page(self, "public", "국내 정치·공공 예산", ["party", "office", "vote", "policy", "budget", "transfer"], "offices")
				elif leaf == "국제관계": Domains.page(self, "public", "국제 관계·협정·안보", ["treaty", "relation", "readiness", "conflict"], "treaties")
				else:
					Domains.page(self, "public", "공공조달·군수 납품", ["procurement", "bid", "award", "deliver", "inspect", "pay"], "procurements")
					Domains.records(self, "public", "armies", "군과 보급")
			elif leaf == "저장": _save_page()
			elif leaf == "설정": _settings_page(content)
			else: _info_page(content)


func _dashboard() -> void:
	build_mode = false
	var company: Dictionary = _government() if not _government().is_empty() else _company()
	var estimate: Dictionary = _planning_summary()
	var hero: VBoxContainer = _card(content)
	var heading := HBoxContainer.new()
	hero.add_child(heading)
	_label(heading, "공공기관 요약" if not _government().is_empty() else ("기업 요약" if not company.is_empty() else "개인 요약"), 22)
	var detail: Button = _button(heading, "상세 보기 ›", func(): _go(4, [1, 0, 0]) if not _government().is_empty() else (_go(1, [0, 0, 0]) if not _company().is_empty() else _go(3, [0, 0, 0])))
	detail.size_flags_horizontal = Control.SIZE_SHRINK_END
	detail.custom_minimum_size.y = 31
	detail.add_theme_font_size_override("font_size", 12)
	detail.add_theme_stylebox_override("normal", _style(Color.TRANSPARENT))
	detail.add_theme_color_override("font_color", MINT)
	var metrics_row := HBoxContainer.new()
	metrics_row.add_theme_constant_override("separation", 5)
	hero.add_child(metrics_row)
	_metric(metrics_row, "coins", "가용 자금", _money(int(estimate.get("available_cash", 0))))
	_metric(metrics_row, "chart", "예약 지급", _money(int(estimate.get("expected_spending", 0))))
	_metric(metrics_row, "people", "가용 시간", str(estimate.get("available_hours", 0)) + " h")
	var map_card: VBoxContainer = _card(content)
	var map_heading := HBoxContainer.new()
	map_card.add_child(map_heading)
	_label(map_heading, str(_find(world.get("cities", []), city_id).get("name", "도시")), 18)
	var construction: Button = _button(map_heading, "건설 계획", func(): _go(1, [2, 0, 0]))
	construction.custom_minimum_size.y = 31
	construction.add_theme_stylebox_override("normal", Art.style("button", PANEL_LIGHT, Color.TRANSPARENT, 5))
	construction.size_flags_horizontal = Control.SIZE_SHRINK_END
	construction.add_theme_font_size_override("font_size", 13)
	construction.icon = Art.texture("factory")
	construction.expand_icon = true
	construction.add_theme_constant_override("icon_max_width", 19)
	_add_map(map_card, 340)
	var zoom := HBoxContainer.new()
	map_card.add_child(zoom)
	_label(zoom, "시설을 눌러 상세 확인 · 드래그 이동", 11, MUTED)
	var zoom_in: Button = _button(zoom, "+", func():
		if is_instance_valid(last_map) and last_map.has_method("zoom_in"): last_map.zoom_in()
	)
	zoom_in.custom_minimum_size = Vector2(35, 32)
	zoom_in.size_flags_horizontal = Control.SIZE_SHRINK_END
	var zoom_out: Button = _button(zoom, "−", func():
		if is_instance_valid(last_map) and last_map.has_method("zoom_out"): last_map.zoom_out()
	)
	zoom_out.custom_minimum_size = Vector2(35, 32)
	zoom_out.size_flags_horizontal = Control.SIZE_SHRINK_END
	_dashboard_construction()
	var checks: VBoxContainer = _card(content, "월말 점검")
	_dashboard_action(checks, "resources", "자재·재고 확인", str(_own_inventory_count()) + "품목", func(): _go(1, [1, 1, 0]))
	_dashboard_action(checks, "people", "인력·업무시간", str(estimate.get("available_hours", 0)) + " h", func(): _go(1, [0, 1, 0]))
	_dashboard_action(checks, "clipboard", "미실행 계획", str(world.get("commands", []).size()) + "개", func(): _go(1, [2, 1, 0]))
	var report: Dictionary = world.get("last_report", {})
	if not report.is_empty():
		var pulse: VBoxContainer = _card(content, "최근 월간 기록")
		_label(pulse, str(report.get("summary", "월 정산 완료")), 13, MUTED)
		_button(pulse, "월간 보고 열기", func(): _show_report(report))


func _add_map(parent: Node, height: int) -> void:
	var map = CityMap.new()
	map.custom_minimum_size.y = height
	map.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	map.build_mode = build_mode
	map.selected_kind = build_kind
	map.selected_tile = selected_tile
	var definition: Dictionary = _find(Economy.building_types(), build_kind)
	map.preview_size = Vector2i(int(definition.get("w", 2)), int(definition.get("h", 2)))
	parent.add_child(map)
	last_map = map
	map.update_world(world, city_id)
	if not build_mode and not full_map and map.has_method("focus_company") and not _company().is_empty(): map.focus_company(str(_company().get("id", "")), 1.8)
	map.tile_selected.connect(func(x: int, y: int):
		selected_tile = Vector2i(x, y)
		if build_mode and not resolving: _build_confirm(x, y)
	)
	map.facility_selected.connect(func(id: String):
		last_selected_id = id
		_facility_dialog(id)
	)


func _map_page(construction: bool = false) -> void:
	build_mode = construction
	var card: VBoxContainer = _card(content, "도시에 직접 짓는 사업", "BUILD & OPERATE" if construction else "CITY MAP")
	_label(card, "시설을 누르면 운영 상태를 확인합니다. 지도를 끌어 이동하고 확대할 수 있습니다.", 13, MUTED)
	if construction:
		var chooser := OptionButton.new()
		chooser.fit_to_longest_item = false
		chooser.clip_text = true
		chooser.custom_minimum_size.y = 46
		var types: Array = Economy.building_types()
		for item in types:
			chooser.add_item(str(item["name"]))
		chooser.select(maxi(types.map(func(x): return str(x["id"])).find(build_kind), 0))
		chooser.item_selected.connect(func(index: int):
			build_kind = str(types[index]["id"])
			selected_tile = Vector2i(-1, -1)
			_render()
		)
		card.add_child(chooser)
		var spec: Dictionary = _find(types, build_kind)
		_row(card, "자재", "목재 %d · 석재 %d · 도구 %d" % [spec.get("wood", 0), spec.get("stone", 0), spec.get("tools", 0)])
		_row(card, "시공", "%d공수 · 최소 %d개월" % [spec.get("work", 0), spec.get("min_months", 0)])
		_label(card, "빈 타일을 눌러 위치 선택 → 조건 확인 → 계획에 추가", 13, MINT)
	_add_map(card, 400 if full_map else 290)
	_button(card, "요약 크기로" if full_map else "지도 넓게 보기", func():
		full_map = not full_map
		_render()
	)
	_label(card, "시설  ■  ·  공사  ▧  ·  도로  ═\n색과 함께 형태·상태 문구로 구분합니다.", 12, MUTED)
	if construction:
		_button(card, "건설 계획 보기", func(): _go(1, [2, 1, 0]))
	_projects_page(false)


func _build_confirm(x: int, y: int) -> void:
	var spec: Dictionary = _find(Economy.building_types(), build_kind)
	var box: VBoxContainer = _open_modal("건설 계획 확인")
	_row(box, "시설", str(spec.get("name", build_kind)))
	_row(box, "위치", "%s · (%d, %d)" % [str(_find(world["cities"], city_id).get("name", city_id)), x, y])
	_row(box, "부지", "%d × %d 타일" % [spec.get("w", 2), spec.get("h", 2)])
	_row(box, "시공", "%d공수 / 최소 %d개월" % [spec.get("work", 0), spec.get("min_months", 0)])
	_row(box, "예약 시공 예산", _money(Economy.construction_budget(build_kind)))
	_label(box, "실제 자재와 건설 인력으로 공정이 진행됩니다. 당월 준공 시설은 다음 달부터 가동합니다. 부족하면 지연되며 원인이 기록됩니다.", 14, MUTED)
	_button(box, "이 위치에 건설 예약", func():
		var result: Dictionary = _queue("build", {"kind": build_kind, "x": x, "y": y, "city_id": city_id})
		if result.get("ok", false):
			_close_modal()
			_render()
			_toast("건설 계획을 추가했습니다. 아직 착공하지 않았습니다.")
		else:
			_message("건설할 수 없습니다", _reason(str(result.get("error", "조건 불충족"))))
	, true)
	_button(box, "위치 다시 선택", _close_modal)


func _production_page() -> void:
	if practice_mode: practice_steps["inspect"] = true
	var card: VBoxContainer = _card(content, "가동과 투입물", "PRODUCTION")
	_label(card, "생산은 도착한 원료, 배정 노동, 설비와 실제 자금의 제약을 받습니다.", 14, MUTED)
	for facility in world.get("facilities", []):
		if str(facility.get("company_id", "")) != str(_company().get("id", "")):
			continue
		_row(card, str(facility.get("name", "시설")), _status(str(facility.get("status", ""))))
		var recipe: Dictionary = _find(world.get("recipes", []), str(facility.get("recipe_id", "")))
		_label(card, "투입  " + _product_vector(recipe.get("inputs", {})) + "\n산출  " + _product_vector(recipe.get("outputs", {})), 13, MUTED)
	_button(card, "생산·가격 정책 조정", func(): _go(1, [1, 2, 0]), true)
	_inventory_page()


func _product_vector(values: Dictionary) -> String:
	var parts: Array = []
	for id in values:
		var p: Dictionary = _find(world.get("products", []), str(id))
		parts.append(str(p.get("name", id)) + " " + str(values[id]) + " " + str(p.get("unit", "")))
	return " · ".join(parts) if not parts.is_empty() else "원천 자원·노동 공정"


func _policy_page() -> void:
	var card: VBoxContainer = _card(content, "반복 운영 정책", "PLAN THIS MONTH")
	var company: Dictionary = _company()
	var policy: Dictionary = company.get("policy", {})
	var limits: Dictionary = Economy.policy_limits()
	_label(card, "매달 같은 명령을 입력할 필요 없이 반복합니다. 변경은 다음 정산부터 적용됩니다.", 14, MUTED)
	var production := HSlider.new()
	production.min_value = int(limits["production_min"])
	production.max_value = int(limits["production_max"])
	production.step = 10
	production.value = int(policy.get("production_percent", 100))
	production.custom_minimum_size.y = 40
	var production_text: Label = _label(card, "생산 목표  %d%%" % int(production.value), 17, MINT)
	card.add_child(production)
	production.value_changed.connect(func(v: float): production_text.text = "생산 목표  %d%%" % int(v))
	var price := HSlider.new()
	price.min_value = int(limits["price_min"])
	price.max_value = int(limits["price_max"])
	price.step = 5
	price.value = int(policy.get("price_percent", 100))
	price.custom_minimum_size.y = 40
	var price_text: Label = _label(card, "기준 가격 대비  %d%%" % int(price.value), 17, GOLD)
	card.add_child(price)
	price.value_changed.connect(func(v: float): price_text.text = "기준 가격 대비  %d%%" % int(v))
	_button(card, "정책 변경을 계획에 추가", func():
		var result: Dictionary = _queue("set_policy", {"production_percent": int(production.value), "price_percent": int(price.value)})
		if result.get("ok", false):
			_render()
			_toast("정책 변경을 추가했습니다. 다음 정산에서 적용합니다.")
		else: _message("명령 확인", _reason(str(result.get("error", "실행 불가"))))
	, true)
	_row(card, "현재 생산 정책", "%d%%" % int(policy.get("production_percent", 100)))
	_row(card, "현재 가격 정책", "%d%%" % int(policy.get("price_percent", 100)))
	_label(card, "높은 목표가 생산능력을 만들지는 않습니다. 원료·노동·설비·지급 가능 자금 안에서 실행합니다.", 13, MUTED)
	_plans_page(false)


func _inventory_page() -> void:
	var card: VBoxContainer = _card(content, "실제 보유 재고", "INVENTORY / 내 법인")
	var owner: String = str(_company().get("id", world.get("player_id", "")))
	var totals: Dictionary = {}
	for lot in world.get("inventory", []):
		if str(lot.get("owner_id", "")) == owner:
			var id: String = str(lot.get("product_id", ""))
			totals[id] = int(totals.get(id, 0)) + int(lot.get("quantity", 0))
	for id in totals:
		var product: Dictionary = _find(world.get("products", []), id)
		_row(card, str(product.get("name", id)), str(totals[id]) + " " + str(product.get("unit", "")))
	if totals.is_empty(): _label(card, "보유한 재고가 없습니다.", 14, MUTED)
	_label(card, "소유자·실제 위치·도시 귀속을 가진 유한 재고입니다. 주문과 보유량은 구분합니다.", 12, MUTED)


func _plans_page(heading: bool = true) -> void:
	var card: VBoxContainer = _card(content, "미실행 명령" if heading else "변경 대기", "PLANNING")
	var commands: Array = world.get("commands", [])
	if commands.is_empty(): _label(card, "추가한 명령이 없습니다. 반복 정책과 AI는 다음 월에도 활동합니다.", 14, MUTED)
	for command in commands:
		if str(command.get("actor_id", "")) != str(world.get("player_id", "")): continue
		var caption: String = "건설 예약" if command.get("type", "") == "build" else "정책 변경"
		if command.get("type", "") == "cancel_project": caption = "공사 취소"
		var line := HBoxContainer.new()
		card.add_child(line)
		var payload: Dictionary = command.get("payload", {})
		var description: String = ""
		if command.get("type", "") == "build":
			description = str(_find(Economy.building_types(), str(payload.get("kind", ""))).get("name", "시설")) + " · (%s, %s)" % [payload.get("x", 0), payload.get("y", 0)]
		elif command.get("type", "") == "set_policy":
			if payload.has("production_percent"): description += "생산 목표 %d%%  " % int(payload.production_percent)
			if payload.has("price_percent"): description += "기준 가격 %d%%" % int(payload.price_percent)
		else: description = str(_find(world.get("projects", []), str(payload.get("project_id", ""))).get("name", "내 공사"))
		_label(line, caption + "\n" + description, 13, MUTED)
		var id: String = str(command.get("id", ""))
		var cancel: Button = _button(line, "철회", func():
			if resolving: return
			var kept: Array = []
			for c in world["commands"]:
				if str(c.get("id", "")) != id: kept.append(c)
			world["commands"] = kept
			dirty = true
			_render()
		)
		cancel.size_flags_horizontal = Control.SIZE_SHRINK_END
	_label(card, "철회는 아직 실행하지 않은 자기 명령만 되돌립니다. 정산 후 공사 취소에는 별도 비용·자재 처리가 적용됩니다.", 12, MUTED)


func _projects_for_company() -> Array:
	return world.get("projects", []).filter(func(p): return str(p.get("company_id", "")) == str(_company().get("id", "")) and p.get("status", "") != "cancelled")


func _projects_page(heading: bool = true) -> void:
	var card: VBoxContainer = _card(content, "공사 진행" if heading else "내 건설 현장", "CONSTRUCTION")
	var found: bool = false
	for project in _projects_for_company():
		found = true
		var id: String = str(project.get("id", ""))
		_label(card, str(project.get("name", project.get("kind", "시설"))), 18)
		var bar := ProgressBar.new()
		bar.max_value = maxi(int(project.get("required_work", 1)), 1)
		bar.value = int(project.get("progress", 0))
		bar.custom_minimum_size.y = 20
		card.add_child(bar)
		_row(card, "실제 공수", "%d / %d" % [project.get("progress", 0), project.get("required_work", 1)])
		_row(card, "상태", _status(str(project.get("status", "진행 중"))))
		_row(card, "경과 / 최소 공기", "%d / %d개월" % [project.get("months_worked", 0), project.get("min_months", 0)])
		if project.has("reason"): _label(card, _reason(str(project["reason"])), 13, GOLD)
		if str(project.get("status", "")) not in ["completed", "cancelled"]:
			_button(card, "공사 취소 계획", func():
				var b: VBoxContainer = _open_modal("진행 중 공사 취소")
				_label(b, "이미 사용한 자재와 지급한 비용은 반환되지 않습니다. 미사용 자재는 법인 소유로 유지됩니다.", 14, GOLD)
				_button(b, "취소 명령 추가", func():
					var result: Dictionary = _queue("cancel_project", {"project_id": id})
					_close_modal()
					if result.get("ok", false):
						_render()
						_toast("공사 취소 명령을 추가했습니다.")
					else: _message("취소할 수 없습니다", _reason(str(result.get("error", ""))))
				)
				_button(b, "계속 시공", _close_modal)
			)
	if not found: _label(card, "진행 중인 건설이 없습니다. 사업 → 프로젝트 → 건설에서 빈 타일에 시설을 계획하세요.", 14, MUTED)


func _facility_list(own_only: bool) -> void:
	var card: VBoxContainer = _card(content, "내 사업장" if own_only else "도시 시설", "FACILITIES")
	for facility in world.get("facilities", []):
		if own_only and facility.get("company_id", "") != _company().get("id", ""): continue
		if not own_only and str(facility.get("city_id", "")) != city_id: continue
		var id: String = str(facility.get("id", ""))
		_button(card, str(facility.get("name", "시설")) + "  ·  " + _status(str(facility.get("status", ""))), func(): _facility_dialog(id))


func _facility_dialog(id: String) -> void:
	var facility: Dictionary = _find(world.get("facilities", []), id)
	if facility.is_empty():
		_project_dialog(id)
		return
	var co: Dictionary = _find(world.get("companies", []), str(facility.get("company_id", "")))
	var box: VBoxContainer = _open_modal(str(facility.get("name", "시설 상세")))
	_row(box, "소속 법인", str(co.get("name", "")))
	_row(box, "위치", "%s · %s, %s" % [facility.get("city_id", ""), facility.get("x", 0), facility.get("y", 0)])
	_row(box, "점유", "%s × %s 타일" % [facility.get("w", 1), facility.get("h", 1)])
	_row(box, "상태", _status(str(facility.get("status", ""))))
	if facility.get("company_id", "") == _company().get("id", ""):
		var recipe: Dictionary = _find(world.get("recipes", []), str(facility.get("recipe_id", "")))
		_label(box, "투입  " + _product_vector(recipe.get("inputs", {})) + "\n산출  " + _product_vector(recipe.get("outputs", {})), 14, MUTED)
		_button(box, "운영 정책 열기", func():
			_close_modal()
			_go(1, [1, 2, 0])
		)
	else:
		_label(box, "공개 시설 정보입니다. 다른 법인의 내부 원가·현금·계획은 조회 권한이 없습니다.", 14, MUTED)
	_button(box, "닫기", _close_modal)


func _market_page(mode: String) -> void:
	var card: VBoxContainer = _card(content, "도시 상품 시장", "MARKET / " + (_scope_label(scope)))
	_label(card, "품목별 단위를 따로 표시합니다. 재고·구매 요청·소비는 실제 상태와 직전 정산 기록에서 집계합니다.", 13, MUTED)
	var statistics: Dictionary = Economy.metrics(world, scope)
	for p in statistics.get("per_product", []):
		var id: String = str(p.get("product_id", ""))
		var box: VBoxContainer = _card(content, str(p.get("name", id)))
		_row(box, "실거래 평균" if int(p.get("trade_quantity", 0)) > 0 else "실험 기준 단가", _money(int(p.get("price", 0))) + " / " + str(p.get("unit", "")))
		if mode == "수급":
			_row(box, "도시 귀속 재고", str(p.get("supply", 0)) + " " + str(p.get("unit", "")))
			_row(box, "구매 요청 / 필수 미충족", "%s / %s" % [p.get("demand", 0), p.get("unmet", 0)], GOLD)
		else:
			_row(box, "직전 생산 / 소비", "%s / %s %s" % [p.get("produced", 0), p.get("consumed", 0), p.get("unit", "")])
		_button(box, "상품과 생산 연결", func(): _product_dialog(id))
	if statistics.get("per_product", []).is_empty():
		_label(card, "첫 정산 전입니다. 등록 상품의 기준 단가는 생산 계획용 실험값입니다.", 14, MUTED)


func _product_dialog(id: String) -> void:
	var p: Dictionary = _find(world.get("products", []), id)
	var box: VBoxContainer = _open_modal(str(p.get("name", id)))
	_row(box, "산업", str(p.get("industry", "")))
	_row(box, "단위", str(p.get("unit", "")))
	_row(box, "실험 기준가격", _money(int(p.get("price", 0))))
	for recipe in world.get("recipes", []):
		if recipe.get("outputs", {}).has(id):
			_label(box, "공정 " + str(recipe.get("id", "")), 17, MINT)
			_label(box, "투입  " + _product_vector(recipe.get("inputs", {})) + "\n산출  " + _product_vector(recipe.get("outputs", {})), 14, MUTED)
	_button(box, "닫기", _close_modal)


func _trade_page() -> void:
	var card: VBoxContainer = _card(content, "내 회사의 체결 기록", "ACTUAL TRANSACTIONS")
	var owner: String = str(_company().get("id", world.get("player_id", "")))
	var events: Array = world.get("events", [])
	var count_value: int = 0
	for i in range(events.size() - 1, -1, -1):
		var e: Dictionary = events[i]
		if str(e.get("type", "")) not in ["trade", "purchase", "sale"]: continue
		if e.get("actor_id", "") != owner and e.get("buyer_id", "") != owner and e.get("seller_id", "") != owner: continue
		_event_row(card, e)
		count_value += 1
		if count_value >= 30: break
	if count_value == 0: _label(card, "체결한 상품 거래가 없습니다. 다음 월에 실제 주문·예산·공급이 맞으면 기록됩니다.", 14, MUTED)


func _people_page(mode: String) -> void:
	var card: VBoxContainer = _card(content, "사람과 노동", "PEOPLE / " + mode)
	_label(card, "개인별 계정과 가용 시간이 있습니다. 건설과 운영의 노동시간을 중복 사용하지 않습니다.", 14, MUTED)
	for p in world.get("people", []):
		if mode != "조직" and str(p.get("city_id", "")) not in _scope_cities(): continue
		var own: bool = p.get("company_id", "") == _company().get("id", "")
		if mode == "조직" and not own: continue
		_row(card, str(p.get("name", p.get("id", ""))), _role(str(p.get("role", ""))), MINT if own else TEXT)
		var detail: String = str(_find(world.get("companies", []), str(p.get("company_id", ""))).get("name", "구직 상태"))
		if mode in ["임금", "조직"] and own:
			detail += " · 시간당 " + _money(int(p.get("wage", 0)))
			for contract: Dictionary in world.get("lifecycle",{}).get("employment",[]):
				if contract.person_id == p.id and contract.get("status","") == "active":
					detail += " · 월 보장 " + str(contract.get("guaranteed_hours",0)) + " h"
					break
		if mode == "시간": detail += " · 가용 " + str(p.get("hours", 0)) + "시간"
		_label(card, detail, 12, MUTED)


func _company_page() -> void:
	var company: Dictionary = _company()
	var card: VBoxContainer = _card(content, str(company.get("name", "개인 활동")), "법인과 경영 권한")
	if company.is_empty():
		_label(card, "현재 대리하는 법인이 없습니다. 취업·창업·투자 또는 대표 취임 자격을 확인하세요.", 14, MUTED)
	else:
		_row(card, "대표", str(_find(world.get("people", []), str(company.get("director_id", ""))).get("name", "공석")))
		_row(card, "법인 현금", _money(_visible_cash(str(company.id))), MINT)
		_row(card, "개인 현금", _money(Economy.cash(world, str(world.get("player_id", "")))))
		_label(card, "법인 자산과 대표의 개인 소유 재산은 별도입니다. 취임·해임은 권한을 바꾸며 지분을 자동 이전하지 않습니다.", 13, MUTED)
		_button(card, "원장·재무 보고", func(): _go(3, [1, 2, 0]))
	Domains.page(self, "lifecycle", "창업과 조직", ["create_company", "subsidiary", "post_job"], "contracts")
	if not company.is_empty(): _inventory_page()


func _company_list() -> void:
	var card: VBoxContainer = _card(content, "지역의 경쟁과 분업", "PUBLIC COMPANY DIRECTORY")
	for co in world.get("companies", []):
		if str(co.get("city_id", "")) != city_id: continue
		var total: int = 0
		for f in world.get("facilities", []):
			if f.get("company_id", "") == co["id"]: total += 1
		_row(card, str(co.get("name", "")), "사업장 %d" % total)
	_label(card, "공개 사업장 명부입니다. 기업별 내부 현금·원가·확정 전 계획은 공개하지 않습니다.", 13, MUTED)


func _personal_page(mode: String) -> void:
	var person: Dictionary = _person()
	var card: VBoxContainer = _card(content, str(person.get("name", "내 인물")), "개인 자산과 진로")
	if mode == "자금":
		_label(card, _money(Economy.cash(world, str(world.get("player_id", "")))), 29, MINT)
		_label(card, "개인 소유 현금 · 회사 자금과 별도", 12, MUTED)
		Domains.page(self, "finance", "개인의 금융 거래", ["deposit", "withdraw", "borrow", "repay", "exchange"], "loans", str(world.get("player_id","")))
	elif mode == "직위":
		_row(card, "직무", _role(str(person.get("role", ""))))
		_row(card, "근무 법인", str(_find(world.get("companies", []), str(person.get("company_id", ""))).get("name", "구직 중")))
		_row(card, "거주 도시", str(_find(world.get("cities", []), str(person.get("city_id", ""))).get("name", "")))
		Domains.records(self,"lifecycle","employment","실제 고용 계약",str(world.get("player_id","")))
		Domains.records(self,"lifecycle","wage_claims","미지급 임금 채권",str(world.get("player_id","")))
		Domains.page(self, "lifecycle", "진로·창업·후계", ["create_company", "apply_job", "apply_director", "resign", "training", "move", "retire", "successor", "change_player"], "training", str(world.get("player_id","")))
	else:
		Domains.page(self, "finance", "채무·상환·보험", ["borrow", "repay", "insure", "claim"], "loans", str(world.get("player_id","")))
		Domains.records(self, "finance", "insurance_policies", "보험 계약", str(world.get("player_id","")))


func _own_cash_flow() -> Dictionary:
	var result: Dictionary = {"income": 0, "outflow": 0}
	var account: String = _finance_owner() + ":cash"
	for entry in world.get("journal", []):
		if int(entry.get("month", 0)) != int(world.get("last_resolved_month", 0)) or int(entry.get("month", 0)) == 0: continue
		for line in entry.get("lines", []):
			if line.get("account", "") == account:
				result["income"] += int(line.get("debit", 0))
				result["outflow"] += int(line.get("credit", 0))
	return result


func _journal_page(mode: String) -> void:
	var represented: Dictionary = _company()
	if not represented.is_empty() and represented.get("director_id", "") != world.get("player_id", ""):
		var restricted: VBoxContainer = _card(content, "위임한 범위의 경영")
		_label(restricted, "법인 전체 재무 조회에는 대표 권한이 필요합니다. 현재 위임한 업무와 예산 한도에서 생산·공사를 계획할 수 있습니다.", 14, MUTED)
		_row(restricted, "가용 위임 한도", _money(_delegated_available(str(represented.id))))
		_button(restricted, "개인 금융 보기", func(): context_company_id = ""; _go(2, [2, 0, 0]))
		return
	var card: VBoxContainer = _card(content, "현금 흐름과 복식 원장", "LEDGER / 선택한 주체")
	var stats: Dictionary = _own_cash_flow()
	_row(card, "직전 월 현금 유입", _money(stats["income"]), MINT)
	_row(card, "직전 월 현금 유출", _money(stats["outflow"]), GOLD)
	_row(card, "현재 현금", _money(Economy.cash(world, _finance_owner())))
	_label(card, "현금 유입·유출과 수익·비용·자산·부채를 구분합니다. 자산 평가액과 실제 현금 지급은 다른 항목입니다.", 12, MUTED)
	var count_value: int = 0
	var entries: Array = world.get("journal", [])
	for i in range(entries.size() - 1, -1, -1):
		var entry: Dictionary = entries[i]
		var own_lines: Array = entry.get("lines", []).filter(func(l): return str(l.get("account", "")).begins_with(_finance_owner() + ":"))
		if own_lines.is_empty(): continue
		_label(card, "%d월 · %s" % [entry.get("month", 0), _reason(str(entry.get("reason", "거래")))], 14, GOLD)
		for line in own_lines:
			if mode == "입출금" and not str(line["account"]).ends_with(":cash"): continue
			var kind: String = str(line["account"]).split(":")[-1]
			_row(card, str({"cash": "현금", "equity": "자본", "expense": "비용", "revenue": "수입", "asset": "자산", "liability": "부채"}.get(kind, kind)), "차 " + _money(int(line.get("debit", 0))) + " / 대 " + _money(int(line.get("credit", 0))))
		count_value += 1
		if count_value >= 25: break


func _aggregate_page() -> void:
	var statistics: Dictionary = Economy.metrics(world, scope)
	var card: VBoxContainer = _card(content, "도시에서 연결되는 경제", "실제 집계")
	_row(card, "인물", str(statistics.get("population", 0)) + "명")
	_row(card, "고용", str(statistics.get("employed", 0)) + "명")
	_row(card, "현금 잔액 합계", _money(int(statistics.get("cash", 0))))
	if statistics.has("value_added"): _row(card, "실험 부가가치", _money(int(statistics.value_added)))
	for item: Dictionary in [{"key":"covered_fixed_price_value_added","label":"기준가격 상품·서비스·건설"},{"key":"public_nonmarket_value_added","label":"공공 비시장 서비스 · 실제 비용"},{"key":"digital_current_price_value_added","label":"디지털 · 이행 거래가격"}]:
		if statistics.has(item.key): _row(card,str(item.label),_money(int(statistics[item.key])))
	if statistics.has("definition"): _label(card,str(statistics.definition),12,MUTED)
	_label(card, "품목별 생산·소비는 해당 단위를 유지합니다. 서로 다른 단위의 물량을 GDP로 합산하지 않습니다.", 12, MUTED)
	var chart_card: VBoxContainer = _card(content, "세계 · 월별 실제 상품 체결", "확정 기록 · 세계 기준")
	var chart = HistoryChart.new()
	chart_card.add_child(chart)
	chart.set_observations(world.get("reports", []), "trades")


func _world_page(level: String) -> void:
	var collection: String = "continents" if level == "대륙" else ("nations" if level == "국가" else "cities")
	var intro: VBoxContainer = _card(content, "세 대륙의 경제", "세계 · 국가 · 도시")
	_label(intro, "상위 지표는 실제 도시의 주체·계정·거래를 집계합니다. 지역 화면을 열어도 별도의 수요·재고를 만들지 않습니다.", 13, MUTED)
	for entity: Dictionary in world.get(collection, []):
		var statistics: Dictionary = Economy.metrics(world, str(entity.id))
		var card: VBoxContainer = _card(content, str(entity.get("name", entity.id)))
		_row(card, "인물 / 고용", "%s / %s명" % [statistics.get("population", 0), statistics.get("employed", 0)])
		_row(card, "현금 계정 합계", _money(int(statistics.get("cash", 0))))
		if statistics.has("value_added"): _row(card, "실험 부가가치", _money(int(statistics.value_added)))
		var id: String = str(entity.id)
		_button(card, "이 범위의 상품 수급", func(): scope=id; _go(2,[0,0,0]))
		if level == "도시":
			_button(card, "도시 지도 열기", func():
				city_id = id
				scope = id
				_go(0, [1, 0, 0])
			)


func _events_page(mode: String) -> void:
	var card: VBoxContainer = _card(content, "관찰한 원인과 결과", "EVENTS / " + mode)
	var events: Array = world.get("events", [])
	var count_value: int = 0
	var owner: String = str(_company().get("id", world.get("player_id", "")))
	for i in range(events.size() - 1, -1, -1):
		var e: Dictionary = events[i]
		if str(e.get("city_id", "")) not in _scope_cities(): continue
		if mode in ["경영", "작업"] and str(e.get("actor_id", "")) not in [owner, str(world.get("player_id", ""))]: continue
		if mode == "병목" and str(e.get("type", "")) not in ["shortage", "construction_delay", "unpaid", "unmet_need", "production_limit"]: continue
		# Internal causes are visible only to the subject or authorized representative.
		var event_actor: String = str(e.get("actor_id", ""))
		var public_event: bool = bool(e.get("public", false)) or str(e.get("type", "")) in ["policy_effective", "treaty_effective", "election_result", "conflict_started", "conflict_resolved"]
		if event_actor not in [owner, str(world.get("player_id", "")), str(_government().get("id", ""))] and not public_event: continue
		_event_row(card, e)
		count_value += 1
		if count_value >= 24: break
	if count_value == 0: _label(card, "해당 조건의 사건이 없습니다. 정산 후 실제 이벤트의 이유 코드를 표시합니다.", 14, MUTED)


func _event_row(parent: Node, event: Dictionary) -> void:
	var reason: String = _reason(str(event.get("reason", event.get("type", "사건"))))
	_label(parent, "%d월  ·  %s" % [event.get("month", 0), reason], 14, GOLD if str(event.get("type", "")) in ["shortage", "delay", "unmet"] else TEXT)
	var detail: Array = []
	if event.has("product_id") and str(event["product_id"]) != "": detail.append(str(_find(world["products"], str(event["product_id"])).get("name", event["product_id"])))
	if int(event.get("quantity", 0)) != 0: detail.append("수량 " + str(event["quantity"]))
	if int(event.get("amount", 0)) != 0 and event.get("actor_id", "") == _company().get("id", ""): detail.append(_money(int(event["amount"])))
	if not detail.is_empty(): _label(parent, " · ".join(detail), 12, MUTED)


func _rules_page(leaf: String) -> void:
	var card: VBoxContainer = _card(content, "이 빌드가 적용하는 규칙", "RULES / 확정·제안·실험값 구분")
	if leaf == "경제":
		_label(card, "• 1턴은 1개월이며 계획·대기 중 시간이 흐르지 않습니다.\n• 생산·급여·매매·소비에 실제 소유자와 자금·재고가 있습니다.\n• 플레이어와 AI는 같은 명령 검증기를 사용합니다.\n• 무료 외부 구매자·자동 자금 보충은 없습니다.\n• 화면을 열지 않은 도시도 같은 규칙으로 정산합니다.", 15)
	elif leaf == "공정":
		_label(card, "• 타일 배치는 건설 계획이며 즉시 준공이 아닙니다.\n• 실제 목재·석재·도구와 시공 공수, 최소 기간을 사용합니다.\n• 부족하면 가능한 공정까지만 진행하고 이유를 남깁니다.\n• 공사 취소가 이미 사용한 자재를 복원하지 않습니다.\n• 월말 준공한 건물은 다음 월부터 가동합니다.", 15)
	else:
		_label(card, "인물의 직업·창업·대표 취임, 기업 지분과 금융, 산업·소프트웨어, 도시 간 무역·운송, 정치·외교·공공조달이 실제 명령과 장부에 연결됩니다.\n\n운영 수치와 세계 규모는 실험 설정이며 각 행동은 자격·권한·예산·납기로 검증합니다.", 15)
	_label(card, "세계 규모·가격·임금·레시피·공기 등 모든 실험 수치는 출시 밸런스가 아닙니다. 기획서의 미정값을 사용자 확정값으로 바꾸지 않습니다.", 13, GOLD)


func _save_page() -> void:
	var card: VBoxContainer = _card(content, "저장과 복구")
	_row(card, "현재 계획 월", _month())
	_row(card, "미저장 변경", "있음" if dirty else "없음")
	_button(card, "수동 저장", func(): _save("manual", true), true)
	_button(card, "다른 슬롯에 저장", _save_as_dialog)
	_button(card, "불러오기·삭제", _load_dialog)
	_button(card, "백업 파일 내보내기", _export_dialog)
	_button(card, "백업 파일 가져오기", _import_dialog)
	_button(card, "최근 월간 보고", func(): _show_report(world.get("last_report", {})))
	var session: VBoxContainer = _card(content, "세션 관리")
	_button(session, "새 게임", func(): _leave_dialog("new"))
	_button(session, "타이틀로", func(): _leave_dialog("title"))
	_button(session, "앱 종료", func(): _leave_dialog("quit"))
	_label(session, "저장 결과를 검사한 뒤 정상 파일을 교체합니다. 보고 확인·불러오기·앱 복귀는 월을 다시 정산하지 않습니다.", 12, MUTED)


func _settings_page(parent: Node) -> void:
	var card: VBoxContainer = _card(parent, "읽기와 조작", "SETTINGS / 기기 설정")
	var text_size := OptionButton.new()
	text_size.custom_minimum_size.y = 48
	for text_value in ["글자 크기 · 기본", "글자 크기 · 크게", "글자 크기 · 더 크게"]: text_size.add_item(text_value)
	var scales: Array = [1.0, 1.15, 1.3]
	text_size.select(maxi(scales.find(float(settings.get("text_scale", 1.0))), 0))
	text_size.item_selected.connect(func(index: int):
		settings["text_scale"] = scales[index]
		_save_preferences()
		_apply_theme()
		_render()
	)
	card.add_child(text_size)
	var contrast := CheckButton.new()
	contrast.text = "고대비 배경"
	contrast.custom_minimum_size.y = 48
	contrast.button_pressed = settings.get("high_contrast", false)
	contrast.toggled.connect(func(enabled: bool):
		settings["high_contrast"] = enabled
		_save_preferences()
		_render()
	)
	card.add_child(contrast)
	var haptics := CheckButton.new()
	haptics.text = "터치 진동 · 지원하는 Android 기기"
	haptics.custom_minimum_size.y = 48
	haptics.button_pressed = settings.get("haptics", false)
	haptics.toggled.connect(func(enabled: bool): settings["haptics"] = enabled; _save_preferences())
	card.add_child(haptics)
	_label(card, "금액은 통화와 품목 단위를 함께 표시합니다. 움직이는 필수 연출과 시간 제한이 없으며, 중요한 상태는 문구로도 안내합니다. 계정·원격 수집 없이 기기에 저장합니다.", 14, MUTED)
	_button(card, "표시 설정 초기화", func():
		settings = {"text_scale": 1.0, "high_contrast": false, "reduce_motion": true}
		_save_preferences()
		_apply_theme()
		_render()
	)


func _info_page(parent: Node) -> void:
	var card: VBoxContainer = _card(parent, "호라경제연구실", "Hora Economic Lab")
	_label(card, "Hora Economic Lab", 22, MINT)
	_row(card, "게임 버전", str(ProjectSettings.get_setting("application/config/version", "0.2.0")))
	_row(card, "세계 규칙", str(world.get("rules_version", "새 게임에서 확인")))
	_row(card, "저장 형식", str(world.get("schema_version", "2")))
	_label(card, "1턴은 1개월입니다. 계획을 정하고 다음 달을 눌러 실제 생산·노동·거래·정치·금융의 결과를 확인하세요. 기다리거나 앱을 닫아 둬도 경제 시간이 진행되지 않습니다.", 14, MUTED)
	_button(card, "조작과 경제 규칙", _tutorial_dialog)
	if not world.is_empty(): _button(card, "개인 목표 설정", _goal_dialog)
	_button(card, "현재 인물의 전체 행동", func(): Domains.all_actions(self))
	_button(card, "프로젝트·검증 기록", func(): OS.shell_open("https://github.com/hddy774/project-hora"))
	_label(card, "UI·아이콘·도시 이미지는 이 게임용으로 생성한 리소스입니다. 날짜·금액·공정과 조작 글자는 현재 게임 데이터에서 표시합니다. 저장·로그는 로컬에 보관하며 외부로 자동 전송하지 않습니다.", 12, MUTED)
	_button(card, "자산·오픈소스 고지", func(): _message("고지와 라이선스", "게임용 생성 UI·아이콘·도시 이미지 리소스\n\n" + FileAccess.get_file_as_string("res://assets/FONT-LICENSE.txt") + "\n\n" + FileAccess.get_file_as_string("res://assets/GODOT-NOTICES.txt")))


func _preflight() -> void:
	if generator != null: _message("초기 검사 정리 중", "취소한 세계의 사전 계산을 안전하게 정리하고 있습니다. 현재 세계와 저장은 유지하며 완료 후 명령을 실행할 수 있습니다."); return
	if resolving: return
	var problems: Array = Economy.validate(world)
	if not problems.is_empty():
		_message("상태 검증 실패", "월을 진행하지 않았습니다.\n" + "\n".join(problems))
		return
	var box: VBoxContainer = _open_modal("다음 달 전 점검")
	_row(box, "정산할 월", _month())
	_row(box, "자기 명령", str(world.get("commands", []).size()) + "개")
	_row(box, "선택 주체 가용 자금", _money(int(_planning_summary().get("available_cash", 0))))
	_row(box, "진행 공사", str(_projects_for_company().size()) + "개")
	var payroll: int = 0
	for p in world.get("people", []):
		if p.get("company_id", "") == _company().get("id", ""):
			payroll += int(p.get("wage", 0)) * (6 if p.get("role", "") == "director" else int(p.get("hours", 0)))
	if not _company().is_empty() and _government().is_empty(): _row(box, "운영 노동 한도 내 임금", "최대 " + _money(payroll))
	_label(box, "원료 발주·급여·가계 소비와 다른 인물의 계획을 함께 정산합니다. 수입은 보장되지 않으며 자금·자재 부족 시 미이행과 지연이 발생할 수 있습니다.", 14, GOLD)
	_button(box, "1개월 정산 시작", _resolve, true)
	_button(box, "계획으로 돌아가기", _close_modal)


func _resolve() -> void:
	if generator != null: return
	if resolving: return
	var saved: Dictionary = Store.save_world(world, _storage_slot("autosave"))
	if not saved.get("ok", false):
		_message("정산 전 저장 실패", str(saved.get("error", "")))
		return
	resolving = true
	var box: VBoxContainer = _open_modal("한 달을 정산하고 있습니다")
	_label(box, "계획 검증 → 생산·거래·소비 → 공사 → 장부 검사 → 저장\n\n계획 변경은 다음 월에 할 수 있습니다. 중단하면 마지막으로 저장한 정상 월에서 복구합니다.", 14, MUTED)
	worker = Thread.new()
	var snapshot: Dictionary = world.duplicate(true)
	_lock_plan_controls(frame)
	var code: Error = worker.start(func(): return Economy.resolve_month(snapshot))
	if code != OK:
		worker = null
		resolving = false
		_render()
		_message("정산 시작 실패", "작업을 시작하지 못했습니다. 이전 상태를 보존했습니다.")


func _lock_plan_controls(node: Node) -> void:
	for child in node.get_children():
		if child is BaseButton: child.disabled = true
		if child is Slider: child.editable = false
		if child is LineEdit: child.editable = false
		if child is Control: child.focus_mode = Control.FOCUS_NONE
		_lock_plan_controls(child)


func _show_report(report: Dictionary) -> void:
	if practice_mode and not report.is_empty(): practice_steps["report"] = true
	if report.is_empty():
		_message("아직 월간 보고서가 없습니다", "첫 월을 정산하면 실제 생산·소비·지급·공정 결과를 확인할 수 있습니다.")
		return
	var box: VBoxContainer = _open_modal("%d개월차 월간 보고" % int(report.get("month", 0)))
	_label(box, "MONTH CLOSED / 검증된 확정 기록", 12, MINT)
	_label(box, str(report.get("summary", "한 달의 정산을 완료했습니다.")), 19)
	_row(box, "실제 상품 체결", str(report.get("trades", 0)) + "건")
	_row(box, "준공", str(report.get("construction_completed", 0)) + "개")
	var flow: Dictionary = _own_cash_flow()
	_row(box, "선택 주체 현금 유입", _money(flow["income"]), MINT)
	_row(box, "선택 주체 현금 지급", _money(flow["outflow"]), GOLD)
	_label(box, "상품별 생산·소비·부족은 시장에서 단위별로 확인합니다. 보고를 다시 열거나 저장을 불러와도 이 월을 재정산하지 않습니다.", 13, MUTED)
	_button(box, "원인과 사건 보기", func():
		_close_modal()
		_go(0, [2, 2, 0])
	)
	_button(box, "다음 계획 열기 · " + _month(), func():
		world["report_pending"] = false
		_save("autosave", false)
		_close_modal()
		_render()
	, true)


func _queue(type: String, payload: Dictionary) -> Dictionary:
	return _queue_as(type, payload, str(_company().get("id", "")))


func _next_command_serial() -> int:
	var number: int = 0
	var ids: Array = world.get("commands", []).map(func(c): return str(c.get("id", "")))
	while ids.has("player-%d-%d" % [int(world.get("planning_month", 1)), number]): number += 1
	return number


func _go(tab: int, path: Array) -> void:
	if resolving: return
	outer = tab
	paths[tab] = path
	full_map = false
	_render()


func _context_dialog() -> void:
	var box: VBoxContainer = _open_modal("조작 인물과 대리 조직")
	_row(box, "인물", str(_person().get("name", "")))
	_row(box, "개인 현금", _money(Economy.cash(world, str(world.get("player_id", "")))))
	_label(box, "조회 지역은 자격이나 소유권을 바꾸지 않습니다. 법인 명령은 대표·위임 권한을 다시 검사합니다.", 13, MUTED)
	_button(box, "개인 활동으로 전환", func():
		context_company_id = ""
		context_government_id = ""
		_close_modal()
		_render()
	)
	for company: Dictionary in _represented_companies():
		var id: String = str(company.id)
		_button(box, str(company.get("name", "법인")) + " · " + _money(_visible_cash(id)), func():
			context_company_id = id
			context_government_id = ""
			_close_modal()
			_render()
		)
	for government: Dictionary in _public_contexts():
		var id: String = str(government.id)
		_button(box, "공직 · " + str(government.get("name", "기관")), func():
			context_government_id = id
			context_company_id = ""
			_close_modal()
			_render()
		)
	_button(box, "모든 행동 보기", func(): Domains.all_actions(self))
	_button(box, "개인 자산", func(): _go(3, [0, 0, 0]))
	_button(box, "닫기", _close_modal)


func _open_modal(title: String) -> VBoxContainer:
	_close_modal()
	modal = Control.new()
	modal.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(modal)
	var shade := ColorRect.new()
	shade.color = Color(0.01, 0.025, 0.04, 0.94)
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	modal.add_child(shade)
	var margins := MarginContainer.new()
	margins.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margins.add_theme_constant_override("margin_left", 22)
	margins.add_theme_constant_override("margin_right", 22)
	margins.add_theme_constant_override("margin_top", 50)
	margins.add_theme_constant_override("margin_bottom", 30)
	modal.add_child(margins)
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", Art.style("modal", PANEL, Color("345061"), 16))
	margins.add_child(panel)
	var modal_scroll := ScrollContainer.new()
	modal_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	panel.add_child(modal_scroll)
	var box := VBoxContainer.new()
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_theme_constant_override("separation", 16)
	modal_scroll.add_child(box)
	_label(box, title, 24)
	return box


func _close_modal() -> void:
	if is_instance_valid(modal):
		remove_child(modal)
		modal.queue_free()
	modal = null


func _message(title: String, message: String) -> void:
	var box: VBoxContainer = _open_modal(title)
	_label(box, message, 14, MUTED)
	_button(box, "확인", _close_modal, true)


func _toast(message: String) -> void:
	if is_instance_valid(toast_label): toast_label.queue_free()
	toast_label = Label.new()
	toast_label.text = message
	toast_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	toast_label.add_theme_color_override("font_color", BG)
	toast_label.add_theme_font_size_override("font_size", 14)
	toast_label.add_theme_stylebox_override("normal", _style(MINT, 10))
	toast_label.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	toast_label.offset_top = 90
	toast_label.offset_bottom = 150
	toast_label.offset_left = 20
	toast_label.offset_right = -20
	add_child(toast_label)
	var toast_ref: WeakRef = weakref(toast_label)
	get_tree().create_timer(3.5).timeout.connect(func():
		var current: Variant = toast_ref.get_ref()
		if is_instance_valid(current): current.queue_free()
	)


func _new_game_dialog() -> void:
	if resolving: return
	if generator != null:
		_message("세계 생성 중", "이전 생성 작업을 정리하고 있습니다. 현재 세계와 저장은 보존합니다.")
		return
	var box: VBoxContainer = _open_modal("새 게임 설정")
	_label(box, "세계는 정확히 세 대륙입니다. 숫자와 생성 규모는 선택한 규칙 데이터에 따라 초기 검사합니다.", 14, MUTED)
	_label(box, "세계 시드", 14)
	var seed_input := LineEdit.new()
	seed_input.text = "42"
	seed_input.placeholder_text = "0~2147483647 정수"
	seed_input.custom_minimum_size.y = 44
	box.add_child(seed_input)
	var engine: Variant = Economy
	var options: Dictionary = engine.new_game_options() if engine.has_method("new_game_options") else {}
	var world_options: Dictionary = {}
	var editors: Dictionary = {}
	var descriptors: Array = options.get("fields", [])
	for field: Dictionary in descriptors:
		var key: String = str(field.get("key", ""))
		if key == "": continue
		_label(box, str(field.get("label", key)), 14)
		if field.get("kind", "int") == "choice":
			var choice := OptionButton.new()
			choice.custom_minimum_size.y = 44
			var values: Array = []
			for item: Dictionary in field.get("options", []):
				choice.add_item(str(item.get("label", item.get("value", "")))); values.append(item.get("value"))
			choice.select(maxi(values.find(field.get("default")), 0))
			box.add_child(choice)
			editors[key] = {"node": choice, "values": values}
		else:
			var number := SpinBox.new()
			number.min_value = float(field.get("min", 1)); number.max_value = float(field.get("max", 100))
			number.step = 1; number.value = float(field.get("default", 1)); number.custom_minimum_size.y = 44
			box.add_child(number)
			editors[key] = {"node": number}
	_label(box, "세계 생성과 취소는 기존 저장을 덮어쓰지 않습니다. 새 세계의 계획을 시작할 때 이전 자동 저장은 별도 보관합니다.", 12, MUTED)
	_button(box, "세계 생성 및 초기 검사", func():
		var text_value: String = seed_input.text.strip_edges()
		if not text_value.is_valid_int() or text_value.to_int() < 0 or text_value.to_int() > 2147483647:
			_message("시드 확인", "0부터 2147483647까지의 정수를 입력하세요."); return
		for key in editors:
			var editor: Dictionary = editors[key]
			world_options[key] = editor["values"][editor.node.selected] if editor.has("values") else int(editor.node.value)
		_begin_generation(text_value.to_int(), world_options)
	, true)
	_button(box, "취소", _close_modal)


func _save(slot: String, show_message: bool) -> bool:
	if world.is_empty() or resolving: return false
	slot = _storage_slot(slot)
	var result: Dictionary = Store.save_world(world, slot)
	if result.get("ok", false):
		_save_view(slot)
		if slot not in ["recovery", "practice-recovery"]:
			dirty = false
			saved_hash = Economy.state_hash(world)
		if show_message:
			_render()
			_toast("월과 미실행 계획을 저장했습니다.")
		return true
	if show_message: _message("저장하지 못했습니다", str(result.get("error", "알 수 없는 오류")))
	return false


func _load(slot: String) -> void:
	if resolving: return
	var result: Dictionary = Store.load_world(slot)
	if not result.get("ok", false):
		_message("불러올 수 없습니다", str(result.get("error", "정상 저장 없음")))
		return
	world = result["state"]
	practice_mode = world.get("session_kind", "") == "practice" or slot.begins_with("practice-")
	practice_steps = {}
	goal_mode = str(world.get("scenario_goal", "none"))
	city_id = str(_person().get("city_id", "city-0"))
	scope = city_id
	context_company_id = "__default__"
	context_government_id = ""
	_restore_view(slot)
	screen = "game"
	dirty = false
	saved_hash = Economy.state_hash(world)
	_render()
	if result.get("recovered", false):
		_message("이전 정상 저장으로 복구했습니다", "최신 파일의 검사가 실패해 정상 백업을 불러왔습니다. 마지막 정상 월과 미실행 계획을 확인하세요.")
	elif world.get("report_pending", false): _show_report(world.get("last_report", {}))


func _load_dialog() -> void:
	if resolving: return
	var box: VBoxContainer = _open_modal("저장 불러오기")
	var slots: Array = Store.list_slots()
	if slots.is_empty(): _label(box, "저장된 세계가 없습니다. 새 게임에서 세계를 생성하세요.", 14, MUTED)
	for item: Dictionary in slots:
		var slot: String = str(item.get("slot", ""))
		var row := HBoxContainer.new()
		box.add_child(row)
		if item.get("ok", false):
			var name_value: String = _slot_label(slot)
			var caption: String = "%s · %d개월차" % [name_value, int(item.get("planning_month", 1))]
			var load_button: Button = _button(row, caption, func(): _confirm_load_slot(slot))
			load_button.add_theme_font_size_override("font_size", 13)
		else:
			_label(row, _slot_label(slot) + "\n" + _reason(str(item.get("error", "호환되지 않거나 손상된 파일"))), 12, GOLD)
		var remove: Button = _button(row, "삭제", func(): _delete_save_dialog(slot))
		remove.size_flags_horizontal = Control.SIZE_SHRINK_END
		remove.custom_minimum_size.x = 58
	_button(box, "닫기", _close_modal)


func _leave_dialog(destination: String) -> void:
	if resolving: return
	if not dirty:
		_leave(destination)
		return
	var box: VBoxContainer = _open_modal("미저장 계획이 있습니다")
	_label(box, "마지막 저장 이후 바뀐 계획을 저장할 수 있습니다.", 14, MUTED)
	_button(box, "저장 후 나가기", func():
		if _save("autosave", false): _leave(destination)
		else: _message("저장 실패", "저장 공간을 확인하세요. 현재 세션은 유지했습니다.")
	, true)
	_button(box, "저장 없이 나가기", func(): _leave(destination))
	_button(box, "취소", _close_modal)


func _leave(destination: String) -> void:
	_close_modal()
	if destination == "quit": get_tree().quit()
	elif destination == "new": _new_game_dialog()
	else:
		screen = "title"
		world = {}
		practice_mode = false
		practice_steps = {}
		goal_mode = "none"
		dirty = false
		_render()


func _back() -> void:
	if generator != null and not generation_cancelled:
		_cancel_generation()
		return
	if resolving: return
	if is_instance_valid(modal):
		_close_modal()
	elif full_map:
		full_map = false
		_render()
	elif screen == "title": get_tree().quit()
	elif outer != 0:
		_go(0, [0, 0, 0])
	else: _leave_dialog("title")


func _settings_dialog() -> void:
	var box: VBoxContainer = _open_modal("표시 설정")
	_settings_page(box)
	_button(box, "닫기", _close_modal)


func _info_dialog() -> void:
	var box: VBoxContainer = _open_modal("게임 정보")
	_info_page(box)
	_button(box, "닫기", _close_modal)


func _save_preferences() -> void:
	var file: FileAccess = FileAccess.open("user://display-settings.json", FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify(settings))
		file.close()


func _load_preferences() -> Dictionary:
	if not FileAccess.file_exists("user://display-settings.json"): return {}
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string("user://display-settings.json"))
	if value is Dictionary:
		var scale: float = float(value.get("text_scale", 1.0))
		if scale not in [1.0, 1.15, 1.3]: value["text_scale"] = 1.0
		return value
	return {}


func _save_view(slot: String) -> void:
	if is_instance_valid(scroll) and last_screen_key != "":
		scroll_positions[last_screen_key] = scroll.scroll_vertical
	var file: FileAccess = FileAccess.open("user://view-" + slot + ".json", FileAccess.WRITE)
	if file != null:
		file.store_string(JSON.stringify({"outer": outer, "paths": paths, "city_id": city_id, "scope": scope, "scroll_positions": scroll_positions, "context_company_id": context_company_id, "context_government_id": context_government_id, "practice_steps": practice_steps, "goal_mode": goal_mode}))
		file.close()


func _restore_view(slot: String) -> void:
	var path: String = "user://view-" + slot + ".json"
	if not FileAccess.file_exists(path): return
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not value is Dictionary: return
	outer = clampi(int(value.get("outer", 0)), 0, 4)
	context_company_id = str(value.get("context_company_id", "__default__"))
	context_government_id = str(value.get("context_government_id", ""))
	practice_steps = value.get("practice_steps", {}).duplicate() if value.get("practice_steps", {}) is Dictionary else {}
	goal_mode = str(value.get("goal_mode", world.get("scenario_goal", "none")))
	var restored_paths: Variant = value.get("paths", [])
	if restored_paths is Array and restored_paths.size() == 5:
		var valid: bool = true
		for item in restored_paths:
			if not item is Array or item.size() != 3:
				valid = false
				break
			for index in item:
				if not (index is int or index is float) or int(index) < 0 or int(index) > 2: valid = false
		if valid:
			var normalized_paths: Array = []
			for item in restored_paths:
				var normalized_item: Array = []
				for index in item: normalized_item.append(int(index))
				normalized_paths.append(normalized_item)
			paths = normalized_paths
	var restored_city: String = str(value.get("city_id", city_id))
	if not _find(world.get("cities", []), restored_city).is_empty(): city_id = restored_city
	var restored_scope: String = str(value.get("scope", city_id))
	if restored_scope == "world" or not _find(world.get("cities", []), restored_scope).is_empty() or not _find(world.get("nations", []), restored_scope).is_empty() or not _find(world.get("continents", []), restored_scope).is_empty(): scope = restored_scope
	if value.get("scroll_positions", {}) is Dictionary:
		scroll_positions = value.get("scroll_positions", {}).duplicate()
		last_screen_key = ""


func _reason(code: String) -> String:
	var descriptions: Dictionary = {
		"OPENING_EQUITY": "개시 현금과 자본",
		"UNKNOWN_CITY": "선택한 도시를 찾을 수 없습니다.",
		"REMOTE_CONSTRUCTION_NOT_SUPPORTED": "현재 대리 법인의 사업 도시에서 부지를 선택하세요.",
		"OUT_OF_BOUNDS": "건물의 일부가 도시 경계 밖입니다.",
		"ROAD_OCCUPIED": "도로 타일 위에는 건물을 지을 수 없습니다.",
		"ROAD_ACCESS_REQUIRED": "도로에 접한 빈 부지를 선택하세요.",
		"TILE_OCCUPIED": "이미 시설이나 공사가 점유한 타일입니다.",
		"SELF_TILE_RESERVATION_CONFLICT": "내가 계획한 다른 건물과 위치가 겹칩니다.",
		"AUTHORITY_REQUIRED": "이 법인의 대표 권한이 필요합니다.",
		"INSUFFICIENT_CASH": "사용 가능한 현금이 부족합니다.",
		"STALE_STATE_VERSION": "월 상태가 바뀌었습니다. 현재 계획에서 다시 입력하세요.",
		"PROJECT_NOT_ACTIVE": "현재 취소하거나 변경할 수 있는 공사가 아닙니다.",
		"COMPANY_INACTIVE": "활동 중인 법인만 이 명령을 실행할 수 있습니다.",
		"GENERATION_CLEANUP":"취소한 세계의 사전 계산을 정리하고 있습니다. 기존 세계는 유지하며 정리 완료 후 명령을 추가할 수 있습니다.",
		"NOT_PLANNING": "현재 정산 중입니다. 다음 계획 월에 입력하세요."
	}
	descriptions.merge({
		"MARKET_AUTHORITY_REQUIRED":"선택한 계정으로 주문할 권한이 없습니다.",
		"MARKET_CITY_INVALID":"상품을 출고하거나 받을 실제 도시를 선택하세요.",
		"MARKET_DATE_INVALID":"주문 만료·최종 인도 월이 현재 계획 월과 맞지 않습니다.",
		"MARKET_ESCROW_FUNDS_REQUIRED":"도착단가와 주문 수량에 필요한 자기 예산이 부족합니다.",
		"MARKET_ORDER_AUTHORITY":"자기 소유의 유효한 주문만 철회할 수 있습니다.",
		"MARKET_ORDER_LIMIT":"현재 주체의 유효 주문 수 한도에 도달했습니다.",
		"MARKET_OWN_STOCK_REQUIRED":"매도할 자기 소유의 미예약 재고가 부족합니다.",
		"MARKET_PHYSICAL_PRODUCT_REQUIRED":"실물 품목을 선택하세요. 서비스·디지털은 해당 계약 화면에서 거래합니다.",
		"MARKET_PRICE_INVALID":"거래 가능한 범위의 정수 단가를 입력하세요.",
		"MARKET_QUALITY_INVALID":"유효한 품질·규격을 선택하세요.",
		"MARKET_QUANTITY_INVALID":"거래 한도 안의 양수 수량을 입력하세요.",
		"MARKET_SIDE_INVALID":"매입 또는 매도 구분을 선택하세요.",
		"MARKET_SPECIFICATION_REQUIRED":"품목의 유효한 거래 규격을 입력하세요.",
		"LAND_ALREADY_LEASED":"이미 임차인이 사용 중인 부지입니다.",
		"LAND_AUTHORITY_REQUIRED":"소유자·대표 또는 공공기관의 토지 권한이 필요합니다.",
		"LAND_OFFER_KIND_MISMATCH":"매각 제안은 매입으로, 임대 제안은 임차로 수락하세요.",
		"LAND_OFFER_TERMS_INVALID":"대금·제안 유효기간·임대기간을 확인하세요.",
		"LAND_OFFER_UNAVAILABLE":"이미 체결·철회·만료된 제안입니다. 유효한 공개 제안을 선택하세요.",
		"LAND_OUT_OF_BOUNDS":"선택한 부지의 일부가 도시 경계 밖입니다.",
		"LAND_OWNERSHIP_OR_LEASE_REQUIRED":"해당 부지의 소유권 또는 유효한 사용권이 필요합니다.",
		"LAND_RECT_INVALID":"도시 안의 유효한 위치와 양수 너비·높이를 입력하세요.",
		"LAND_SELF_TRADE":"같은 소유자가 자기 토지를 사고팔 수 없습니다.",
		"LAND_TITLE_REQUIRED":"선택한 부지 전체에 대한 소유권이 필요합니다.",
		"LAND_USE_INVALID":"지원하는 토지 용도를 선택하세요.",
		"LAND_USE_NOT_PERMITTED":"현재 토지의 허용 용도와 맞지 않습니다. 소유권·용도 변경을 확인하세요.",
		"LEASE_DURATION_INVALID":"허용 범위의 임대기간을 입력하세요.",
		"LEASE_RIGHTS_MUST_BE_PRESERVED":"토지를 이전해도 기존 임차인의 사용권을 보존해야 합니다.",
		"LEASE_TITLE_CANNOT_BE_SPLIT":"임대 중인 부지의 소유권을 일부만 나눌 수 없습니다.",
		"PUBLIC_ROAD_RESERVED":"공공 도로로 예약된 타일은 거래·건설할 수 없습니다.",
		"RENT_AMOUNT_INVALID":"실제 미지급 임대료 안의 지급액을 입력하세요.",
		"RENT_CLAIM_NOT_OWNED":"현재 주체의 미지급 임대료 채무가 아닙니다.",
		"TENANT_POSSESSION_RESERVED":"기존 임차인의 점유·사용권이 적용 중입니다.",
		"FACILITY_CONVERSION_REQUIRED":"기존 시설을 전환하거나 정리한 뒤 토지 용도를 변경하세요.",
		"STALE_STATE":"상태가 변경되었습니다. 현재 계획에서 다시 입력하세요.",
		"INVALID_GUARANTEED_HOURS":"월 보장 유급시간은 계약 시간 안에서 지정하세요. 대표 계약은 대표직의 유급시간 한도를 따릅니다.",
		"CONTROLLING_VOTE_REQUIRED":"이 자본 결정에는 필요한 의결권이나 유효한 주주 승인이 필요합니다."
	})
	if code.begins_with("SELF_RESERVATION_EXCEEDS_"):
		var kind: String = code.trim_prefix("SELF_RESERVATION_EXCEEDS_").split(":")[0]
		return "자기 명령 사이에서 같은 " + str({"CASH":"현금","INVENTORY":"재고","LABOR":"업무시간","SHARES":"지분","DELEGATION":"위임 예산"}.get(kind,"자원")) + "을 중복 예약할 수 없습니다. 기존 계획을 철회하거나 수량을 줄이세요."
	if descriptions.has(code): return str(descriptions[code])
	if code == "": return "조건을 확인하세요."
	if code.to_upper() != code or code.contains(" "): return code
	if code.contains("AUTHORITY") or code.contains("DIRECTOR_REQUIRED"): return "선택한 주체에게 필요한 직위·소유권 또는 위임 권한이 없습니다. (" + code + ")"
	if code.contains("FUNDS") or code.contains("CASH") or code.contains("BUDGET"): return "현금·예산·예약 지급액을 확인하세요. (" + code + ")"
	if code.contains("CONSENT") or code.contains("OFFER"): return "상대방의 유효한 제안 또는 동의가 필요합니다. (" + code + ")"
	if code.contains("REQUIRED"): return "이 행동의 자격·투입물·선행 조건이 충족되지 않았습니다. (" + code + ")"
	if code.contains("INVALID"): return "입력한 값이나 대상의 조건을 확인하세요. (" + code + ")"
	if code.contains("ALREADY") or code.contains("DUPLICATE"): return "이미 처리되었거나 진행 중인 요청과 중복됩니다. (" + code + ")"
	return "요청 조건을 충족하지 못했습니다. 상세 코드: " + code


func _status(value: String) -> String:
	return str({"operational": "가동", "ready": "준공 · 가동 준비", "active": "진행 중", "paused": "중단", "completed": "준공", "cancelled": "취소", "researching": "연구 중", "released": "출시", "growing": "생육 중", "installing": "설치 중", "repairing": "수리 중", "open": "공개 모집", "submitted": "제출", "accepted": "승인·인수", "rejected": "거절", "transit": "운송 중", "awaiting_inspection": "검수 대기", "paid": "지급 완료", "awarded": "계약 체결", "defaulted": "채무 불이행", "bankrupt": "파산", "retired": "은퇴", "employed": "취업", "unemployed": "구직", "pending": "대기", "unpaid": "미지급", "partially_paid": "일부 지급", "expired": "만료", "settled": "정산", "proposed": "제안", "enacted": "시행", "elected": "선출", "matured": "만기", "estate_owned": "청산 재고"}.get(value, value))


func _role(value: String) -> String:
	return str({"director": "대표", "operator": "운영 인력", "builder": "건설 인력", "researcher": "연구자", "developer": "개발자", "public_worker": "공공 인력", "service": "서비스", "military": "군인", "mayor": "도시 책임자", "national_leader": "국가 책임자"}.get(value, value))

func _represented_companies() -> Array:
	var result: Array = []
	var person: String = str(world.get("player_id", ""))
	for company: Dictionary in world.get("companies", []):
		if company.get("director_id", "") == person:
			result.append(company)
			continue
		for delegation: Dictionary in world.get("lifecycle", {}).get("delegations", []):
			if str(delegation.get("person_id", "")) == person and str(delegation.get("company_id", "")) == str(company.id) and str(delegation.get("status", "active")) == "active":
				result.append(company)
				break
	return result


func _queue_as(type: String, payload: Dictionary, company_id: String) -> Dictionary:
	if generator != null: return {"ok":false,"error":"GENERATION_CLEANUP"}
	if resolving: return {"ok": false, "error": "NOT_PLANNING"}
	var command: Dictionary = {"id": "player-%d-%d" % [int(world.get("planning_month", 1)), _next_command_serial()], "actor_id": str(world.get("player_id", "")), "acting_company_id": company_id, "expected_version": int(world.get("version", 0)), "type": type, "payload": payload}
	var result: Dictionary = Economy.queue_command(world, command)
	if result.get("ok", false):
		dirty = true
		if practice_mode:
			practice_steps["command"] = true
			if type == "build": practice_steps["build"] = true
	return result


func _icon(parent: Node, name_value: String, width: int = 28) -> TextureRect:
	var icon := TextureRect.new()
	icon.texture = Art.texture(name_value)
	icon.custom_minimum_size = Vector2(width, width)
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(icon)
	return icon


func _metric(parent: Node, icon_name: String, name_value: String, value: String) -> void:
	var panel := PanelContainer.new()
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_theme_stylebox_override("panel", Art.style("panel", PANEL_LIGHT, Color.TRANSPARENT, 7))
	parent.add_child(panel)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	panel.add_child(row)
	_icon(row, icon_name, 25)
	var box := VBoxContainer.new()
	box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	box.add_theme_constant_override("separation", 1)
	row.add_child(box)
	_label(box, name_value, 11, MUTED)
	_label(box, value, 14, TEXT)


func _planning_summary() -> Dictionary:
	var owner: String = str(_government().get("id", _company().get("id", world.get("player_id", ""))))
	if owner == "": owner = str(world.get("player_id", ""))
	var engine: Variant = Economy
	if engine.has_method("preflight_summary"):
		var values: Variant = engine.preflight_summary(world, str(world.get("player_id", "")), str(_government().get("id", _company().get("id", ""))))
		if values is Dictionary: return values
	var available: int = 0
	for person: Dictionary in world.get("people", []):
		if str(person.get("company_id", "")) == owner:
			available += maxi(0, int(person.get("hours", 0)) - int(world.get("monthly_labor", {}).get(person.get("id", ""), 0)))
	return {"available_cash": _visible_cash(owner), "reserved_cash": 0, "expected_spending": 0, "available_hours": available}


func _own_inventory_count() -> int:
	var owner: String = str(_company().get("id", world.get("player_id", "")))
	var seen: Dictionary = {}
	for lot: Dictionary in world.get("inventory", []):
		if str(lot.get("owner_id", "")) == owner and int(lot.get("quantity", 0)) > 0: seen[str(lot.product_id)] = true
	return seen.size()


func _dashboard_action(parent: Node, image: String, title: String, value: String, action: Callable) -> void:
	var row := HBoxContainer.new()
	parent.add_child(row)
	_icon(row, image, 24)
	var button: Button = _button(row, title, action)
	button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	button.custom_minimum_size.y = 36
	button.add_theme_font_size_override("font_size", 14)
	var number: Label = _label(row, value + "  ›", 13, MUTED)
	number.size_flags_horizontal = Control.SIZE_SHRINK_END


func _dashboard_construction() -> void:
	var card: VBoxContainer = _card(content, "건설 프로젝트")
	if not _government().is_empty():
		_label(card, "공공사업과 납품은 해당 기관의 승인된 예산·공고·계약에서 진행합니다.", 13, MUTED)
		_button(card, "공공예산·조달 관리", func(): _go(4, [1, 2, 0]))
		return
	var projects: Array = _projects_for_company()
	var project: Dictionary = {}
	for item: Dictionary in projects:
		if str(item.get("id", "")) == last_selected_id: project = item; break
	if project.is_empty() and not projects.is_empty(): project = projects[-1]
	if project.is_empty():
		_label(card, "진행 중인 공사가 없습니다. 부지·자재·시공 인력을 확인해 시설을 계획하세요.", 13, MUTED)
		_button(card, "건설 계획", func(): _go(1, [2, 0, 0]))
		return
	var heading := HBoxContainer.new()
	card.add_child(heading)
	_icon(heading, "construction", 30)
	_label(heading, str(project.get("name", project.get("kind", "시설"))), 18)
	var detail: Button = _button(heading, "공정 관리 ›", func(): _project_dialog(str(project.get("id", ""))))
	detail.size_flags_horizontal = Control.SIZE_SHRINK_END
	detail.custom_minimum_size.y = 31
	detail.add_theme_font_size_override("font_size", 12)
	var progress: int = int(project.get("progress", 0))
	var total: int = maxi(int(project.get("required_work", 1)), 1)
	var stage_row := HBoxContainer.new()
	stage_row.add_theme_constant_override("separation", 4)
	card.add_child(stage_row)
	var stages: Array = ["기초", "골조", "설비", "준공"]
	for i in range(4):
		var complete: bool = progress * 4 >= total * (i + 1) or str(project.get("status", "")) == "completed"
		var stage := VBoxContainer.new()
		stage.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		stage_row.add_child(stage)
		var icon := _icon(stage, "check" if complete else "circle", 19)
		icon.modulate = MINT if complete or progress * 4 >= total * i else MUTED
		var label: Label = _label(stage, stages[i], 12, MINT if complete else MUTED)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var counts := HBoxContainer.new()
	card.add_child(counts)
	var required: Dictionary = project.get("materials_required", {})
	var used: Dictionary = project.get("materials_used", {})
	var material_parts: Array = []
	for id in required:
		var product: Dictionary = _find(world.get("products", []), str(id))
		material_parts.append("%s %d/%d" % [product.get("name", id), used.get(id, 0), required[id]])
	var details: VBoxContainer = VBoxContainer.new()
	counts.add_child(details)
	_label(details, "자재 투입 · " + " · ".join(material_parts), 12, GOLD)
	_row(card, "건설 공수", "%d / %d h" % [progress, total])
	var remaining_work: int = maxi(total - progress, 0)
	var elapsed: int = int(project.get("months_worked", 0))
	var minimum: int = maxi(int(project.get("min_months", 0)) - elapsed, 1 if remaining_work > 0 else 0)
	_row(card, "잔여 기간", "최소 %d개월 · 자재·인력에 따라 변동" % minimum if remaining_work > 0 else "월말 검수 완료")
	_label(card, _reason(str(project.get("reason", "자재와 시공 일정을 확인하세요."))), 12, MUTED)
	_label(card, "준공 다음 달부터 운영 인력·원료를 확보해 가동", 11, MUTED)


func _ownership_page() -> void:
	var card: VBoxContainer = _card(content, "소유 지분과 의결권")
	var owner: String = str(_company().get("id", world.get("player_id", "")))
	var count_value: int = 0
	for holding: Dictionary in world.get("ownership", []):
		if str(holding.get("owner_id", "")) not in [owner, str(world.get("player_id", ""))]: continue
		var company: Dictionary = _find(world.get("companies", []), str(holding.get("company_id", "")))
		_row(card, str(company.get("name", "회사")), "%s주 · 의결권 %s" % [holding.get("shares", 0), holding.get("voting_shares", holding.get("shares", 0))])
		count_value += 1
	if count_value == 0: _label(card, "보유 지분이 없습니다. 발행·거래 조건과 투자 가능 자금을 확인하세요.", 13, MUTED)
	Domains.page(self, "finance", "주식과 배당", ["shares", "dividend"], "equity_issues")

func _project_dialog(id: String) -> void:
	var project: Dictionary = _find(world.get("projects", []), id)
	if project.is_empty(): return
	var box: VBoxContainer = _open_modal(str(project.get("name", "건설 프로젝트")))
	_row(box, "시공 단계", str(project.get("stage", "기초")))
	_row(box, "상태", _status(str(project.get("status", "active"))))
	_row(box, "진척", "%s / %s h" % [project.get("progress", 0), project.get("required_work", 0)])
	_row(box, "최소 공기", str(project.get("min_months", 0)) + "개월")
	var owner: String = str(project.get("company_id", ""))
	var own: bool = false
	for company: Dictionary in _represented_companies():
		if str(company.get("id", "")) == owner: own = true
	if own:
		_row(box, "지급한 공정대금", _money(int(project.get("spent", 0))))
		var required: Dictionary = project.get("materials_required", {})
		var used: Dictionary = project.get("materials_used", {})
		for product_id in required:
			var product: Dictionary = _find(world.get("products", []), str(product_id))
			_row(box, str(product.get("name", product_id)), "%s / %s %s" % [used.get(product_id, 0), required[product_id], product.get("unit", "")])
		_label(box, _reason(str(project.get("reason", ""))), 13, GOLD)
		if project.get("status", "") in ["active", "paused"]:
			var action: String = "resume_project" if project.get("status", "") == "paused" else "pause_project"
			_button(box, "시공 재개" if action == "resume_project" else "시공 중단", func():
				var result: Dictionary = _queue_as(action, {"project_id": id}, owner)
				if result.get("ok", false): _close_modal(); _render(); _toast("공사 변경 명령을 추가했습니다.")
				else: _message("공정 변경 불가", _reason(str(result.get("error", ""))))
			)
			_button(box, "공사 취소 계획", func():
				var confirmation: VBoxContainer = _open_modal("공사 취소")
				_label(confirmation, "이미 사용한 자재와 지급한 비용은 되돌려 생성하지 않습니다. 미사용 물자는 소유자 재고로 남습니다.", 14, GOLD)
				_button(confirmation, "취소 명령 추가", func():
					var result: Dictionary = _queue_as("cancel_project", {"project_id": id}, owner)
					if result.get("ok", false): _close_modal(); _render()
					else: _message("공사 취소 불가", _reason(str(result.get("error", ""))))
				)
				_button(confirmation, "계속 시공", _close_modal)
			)
	else: _label(box, "공개된 현장 상태입니다. 다른 법인의 비용과 자재 계약은 조회하지 않습니다.", 13, MUTED)
	_button(box, "닫기", _close_modal)

func _start_character_dialog(candidate: Dictionary) -> void:
	var box: VBoxContainer = _open_modal("시작 인물 선택")
	_label(box, "같은 세계의 기존 인물을 선택합니다. 선택은 조작 주체만 바꾸며 현금·지분·시설을 추가하지 않습니다.", 13, MUTED)
	var chooser := OptionButton.new()
	chooser.fit_to_longest_item = false
	chooser.clip_text = true
	chooser.custom_minimum_size.y = 44
	var people: Array = candidate.get("people", [])
	for person: Dictionary in people:
		var company: Dictionary = _find(candidate.get("companies", []), str(person.get("company_id", "")))
		chooser.add_item(str(person.get("name", "인물")) + " · " + str(company.get("name", "구직 중")))
	box.add_child(chooser)
	var details: Label = _label(box, "", 14, MUTED)
	var update_details: Callable = func(index: int):
		var person: Dictionary = people[index]
		var company: Dictionary = _find(candidate.get("companies", []), str(person.get("company_id", "")))
		details.text = "직무: %s\n개인 현금: %s\n거주: %s\n기업 자금과 개인 돈은 별도입니다." % [_role(str(person.get("role", ""))), _money(Economy.cash(candidate, str(person.id))), _find(candidate.get("cities", []), str(person.get("city_id", ""))).get("name", "")]
	chooser.item_selected.connect(update_details)
	if not people.is_empty(): update_details.call(0)
	_label(box, "초기 장부·재고·권한·공간 검사를 통과했습니다. 선택된 규모의 장기 경제 결과와 실제 기기 성능은 플레이 조건에 따라 달라집니다.", 12, MUTED)
	_button(box, "이 인물로 계획 시작", func():
		if people.is_empty(): return
		var selected: Dictionary = people[chooser.selected]
		candidate.player_id = str(selected.id)
		candidate.controlled_company_id = ""
		for company: Dictionary in candidate.get("companies", []):
			if company.get("director_id", "") == selected.id:
				candidate.controlled_company_id = str(company.id); break
		var errors: Array = Economy.validate(candidate)
		if not errors.is_empty(): _message("인물 선택 검사 실패", "\n".join(errors)); return
		if not _archive_before_new_game(): _message("이전 진행 보관 실패", "저장 공간을 확인하세요. 기존 세계와 파일을 유지했습니다."); return
		world = candidate
		practice_mode = false
		practice_steps = {}
		goal_mode = "none"
		context_company_id = "__default__"
		city_id = str(selected.get("city_id", "city-0")); scope = city_id
		outer = 0; paths = [[0,0,0], [0,0,0], [0,0,0], [0,0,0], [0,0,0]]
		screen = "game"; dirty = true; saved_hash = ""; scroll_positions = {}; last_screen_key = ""
		_render()
		if not _save("autosave", false): _message("첫 계획 저장 실패", "세계는 열렸지만 자동 저장하지 못했습니다. 수동 저장과 저장 공간을 확인하세요.")
	, true)
	_button(box, "설정으로 돌아가기", _new_game_dialog)
	_button(box, "취소", _close_modal)


func _archive_before_new_game() -> bool:
	var directory: String = ProjectSettings.globalize_path("user://saves")
	if DirAccess.make_dir_recursive_absolute(directory) != OK: return false
	if not world.is_empty():
		var slot: String = _unique_slot("archive")
		var saved: Dictionary = Store.save_world(world, _storage_slot(slot))
		if not saved.get("ok", false): return false
		_save_view(slot)
	if FileAccess.file_exists("user://saves/autosave.json"):
		var slot: String = _unique_slot("archive")
		for suffix: String in [".json", ".json.bak"]:
			var source: String = "user://saves/autosave" + suffix
			if FileAccess.file_exists(source):
				var error: Error = DirAccess.copy_absolute(ProjectSettings.globalize_path(source), ProjectSettings.globalize_path("user://saves/" + slot + suffix))
				if error != OK: return false
	return true


func _unique_slot(prefix: String) -> String:
	var serial: int = 1
	while FileAccess.file_exists("user://saves/" + prefix + "-" + str(serial) + ".json") or FileAccess.file_exists("user://saves/" + prefix + "-" + str(serial) + ".json.bak"): serial += 1
	return prefix + "-" + str(serial)


func _slot_label(slot: String) -> String:
	if slot.begins_with("practice-"): return "연습 · " + _slot_label(slot.trim_prefix("practice-"))
	return str({"autosave": "자동 저장", "manual": "수동 저장", "recovery": "중단 복구", "before_load": "불러오기 전 백업", "before_load_b": "불러오기 전 보조 백업"}.get(slot, "이전 세계 " + slot.trim_prefix("archive-") if slot.begins_with("archive-") else slot))


func _confirm_load_slot(slot: String) -> void:
	var valid: Dictionary = Store.load_world(slot)
	if not valid.get("ok", false): _message("불러올 수 없습니다", _reason(str(valid.get("error", "파일 검사 실패")))); return
	if not dirty: _load(slot); return
	var box: VBoxContainer = _open_modal("미저장 계획을 보관할까요?")
	_label(box, "불러올 파일은 검사를 통과했습니다. 현재 계획은 별도 백업으로 보관할 수 있습니다.", 14, MUTED)
	_button(box, "별도 백업 후 불러오기", func():
		if _save(_unique_slot("before-load"), false): _load(slot)
		else: _message("백업 실패", "현재 계획을 보존했습니다. 저장 공간을 확인하세요.")
	, true)
	_button(box, "저장 없이 불러오기", func(): _load(slot))
	_button(box, "취소", _close_modal)


func _save_as_dialog() -> void:
	var box: VBoxContainer = _open_modal("다른 슬롯에 저장")
	var name_input := LineEdit.new()
	name_input.text = _unique_slot("manual")
	name_input.placeholder_text = "영문·숫자·하이픈·밑줄 1~48자"
	name_input.custom_minimum_size.y = 44
	box.add_child(name_input)
	_button(box, "저장", func():
		var slot: String = name_input.text.strip_edges()
		if FileAccess.file_exists("user://saves/" + slot + ".json"):
			_message("기존 슬롯이 있습니다", "새 이름을 사용하세요. 기존 저장은 덮어쓰지 않았습니다."); return
		if _save(slot, false): _close_modal(); _render(); _toast("새 슬롯에 저장했습니다.")
		else: _message("저장 실패", "슬롯 이름과 저장 공간을 확인하세요.")
	, true)
	_button(box, "취소", _close_modal)


func _delete_save_dialog(slot: String) -> void:
	var box: VBoxContainer = _open_modal("저장 삭제")
	_label(box, _slot_label(slot) + " 슬롯과 해당 복구본을 삭제합니다. 현재 실행 중인 세계는 삭제하지 않습니다.", 14, GOLD)
	_button(box, "이 슬롯 삭제", func():
		var result: Dictionary = Store.delete_slot(slot)
		if result.get("ok", false):
			var view_path: String = "user://view-" + slot + ".json"
			if FileAccess.file_exists(view_path): DirAccess.remove_absolute(ProjectSettings.globalize_path(view_path))
			_load_dialog()
		else: _message("삭제 실패", _reason(str(result.get("error", "파일 접근 실패"))))
	)
	_button(box, "취소", _close_modal)


func _export_dialog() -> void:
	if world.is_empty() or resolving: return
	if not _save("manual", false): _message("내보내기 준비 실패", "현재 세계를 저장하지 못했습니다."); return
	file_dialog = FileDialog.new()
	file_dialog.file_mode = FileDialog.FILE_MODE_SAVE_FILE
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.use_native_dialog = true
	file_dialog.title = "게임 백업 내보내기"
	file_dialog.current_file = "hora-economic-lab-backup.json"
	file_dialog.add_filter("*.json", "Hora Economic Lab 저장")
	add_child(file_dialog)
	var current_dialog: FileDialog = file_dialog
	current_dialog.canceled.connect(func(): current_dialog.queue_free())
	file_dialog.file_selected.connect(func(path: String):
		current_dialog.queue_free()
		var bytes: PackedByteArray = FileAccess.get_file_as_bytes("user://saves/" + _storage_slot("manual") + ".json")
		var target: FileAccess = FileAccess.open(path, FileAccess.WRITE)
		if target == null: _message("내보내기 실패", "선택한 위치에 쓸 수 없습니다."); return
		target.store_buffer(bytes); target.flush()
		var error: Error = target.get_error(); target.close()
		if error != OK: _message("내보내기 실패", "파일 쓰기를 완료하지 못했습니다.")
		else: _message("백업 내보내기 완료", "현재 월과 미실행 명령을 파일에 보관했습니다.")
	)
	file_dialog.popup_centered_ratio(0.85)


func _import_dialog() -> void:
	if resolving: return
	file_dialog = FileDialog.new()
	file_dialog.file_mode = FileDialog.FILE_MODE_OPEN_FILE
	file_dialog.access = FileDialog.ACCESS_FILESYSTEM
	file_dialog.use_native_dialog = true
	file_dialog.title = "게임 백업 가져오기"
	file_dialog.add_filter("*.json", "Hora Economic Lab 저장")
	add_child(file_dialog)
	var current_dialog: FileDialog = file_dialog
	current_dialog.canceled.connect(func(): current_dialog.queue_free())
	file_dialog.file_selected.connect(func(path: String):
		current_dialog.queue_free()
		var source: FileAccess = FileAccess.open(path, FileAccess.READ)
		if source == null: _message("가져오기 실패", "파일을 읽을 수 없습니다."); return
		if source.get_length() <= 0 or source.get_length() > Store.MAX_SAVE_BYTES: source.close(); _message("가져오기 실패", "지원하지 않는 파일 크기입니다."); return
		var bytes: PackedByteArray = source.get_buffer(source.get_length()); source.close()
		var slot: String = _unique_slot("import")
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path("user://saves"))
		var target: FileAccess = FileAccess.open("user://saves/" + slot + ".json", FileAccess.WRITE)
		if target == null: _message("가져오기 실패", "로컬 저장 위치에 쓸 수 없습니다."); return
		target.store_buffer(bytes); target.flush(); target.close()
		var checked: Dictionary = Store.load_world(slot)
		if not checked.get("ok", false):
			Store.delete_slot(slot)
			_message("파일 검사 실패", _reason(str(checked.get("error", "형식·내용·체크섬을 확인하세요."))))
			return
		_confirm_load_slot(slot)
	)
	file_dialog.popup_centered_ratio(0.85)


func _tutorial_dialog() -> void:
	var box: VBoxContainer = _open_modal("학습과 도움말")
	_button(box, "별도 연습 세계 시작", _start_practice_dialog, true)
	_label(box, "연습 세계는 같은 생성·시장·장부 규칙을 사용하며 별도 저장 슬롯에 보관합니다. 기존 샌드박스는 덮어쓰지 않습니다.", 13, MUTED)
	_label(box, "시간 제한 없이 계획을 읽고 수정할 수 있습니다. 아래 설명을 확인한 뒤 실제 화면으로 이동하세요.", 14, MUTED)
	_label(box, "① 주체와 자금", 18, MINT)
	_label(box, "상단 인물·조직에서 개인과 권한 있는 법인을 구분합니다. 대표로 취임해도 회사 돈은 개인 현금이 아닙니다.", 14, MUTED)
	_label(box, "② 생산과 시장", 18, MINT)
	_label(box, "시장 수급에서 상품별 단위와 구매 요청·실제 체결을 구분합니다. 생산은 원료·노동·설비·자금 안에서 진행됩니다.", 14, MUTED)
	_label(box, "③ 첫 건설", 18, MINT)
	_label(box, "사업 → 프로젝트 → 건설에서 시설을 고르고 도로에 접한 빈 타일을 선택하세요. 자재와 공수·기간을 사용하며 월말 준공 뒤 다음 월부터 가동합니다.", 14, MUTED)
	_label(box, "④ 한 달과 원인", 18, MINT)
	_label(box, "다음 달 점검 후 1개월을 정산합니다. 월간 보고와 사건에서 실제 부족·지급·계약 이행 원인을 확인하세요. 보고 재열기와 앱 복귀로 월이 흐르지 않습니다.", 14, MUTED)
	_label(box, "⑤ 자유로운 활동", 18, MINT)
	_label(box, "구직·창업·투자·정치·조달 등은 같은 인물·권한·자금 검사를 사용합니다. 실패해도 남은 자산·부채·자격에서 다음 선택을 할 수 있습니다.", 14, MUTED)
	if not world.is_empty():
		_button(box, "현재 인물의 행동 보기", func(): Domains.all_actions(self))
		_button(box, "건설 화면 열기", func(): _go(1, [2, 0, 0]))
	else: _button(box, "새 세계에서 시작", _new_game_dialog, true)
	_button(box, "닫기", _close_modal)

func _public_contexts() -> Array:
	var script: Variant = Domains.module_script("public")
	if script == null or not script.has_method("summary"): return []
	var summary: Dictionary = script.summary(world, str(world.get("player_id", "")))
	return summary.get("governments", []).filter(func(g): return g.get("can_control", false))


func _government() -> Dictionary:
	if context_government_id == "": return {}
	for government: Dictionary in _public_contexts():
		if str(government.get("id", "")) == context_government_id: return government
	return {}


func _finance_contexts() -> Array:
	var result: Array = []
	for company: Dictionary in _represented_companies():
		if str(company.get("director_id", "")) == str(world.get("player_id", "")): result.append(company)
	result.append_array(_public_contexts())
	return result


func _finance_owner() -> String:
	if not _government().is_empty(): return str(_government().id)
	var company: Dictionary = _company()
	if company.get("director_id", "") == world.get("player_id", ""): return str(company.id)
	return str(world.get("player_id", ""))

func _delegated_available(company_id: String) -> int:
	var cap: int = 0
	for delegation: Dictionary in world.get("lifecycle", {}).get("delegations", []):
		if str(delegation.get("person_id", "")) == str(world.get("player_id", "")) and str(delegation.get("company_id", "")) == company_id and delegation.get("status", "") == "active":
			cap = maxi(cap, maxi(0, int(delegation.get("budget", 0)) - int(delegation.get("spent", 0))))
	return cap


func _visible_cash(owner: String) -> int:
	if owner == str(world.get("player_id", "")): return Economy.cash(world, owner)
	for company: Dictionary in _represented_companies():
		if str(company.id) == owner:
			if company.get("director_id", "") == world.get("player_id", ""): return Economy.cash(world, owner)
			return mini(_delegated_available(owner), Economy.cash(world, owner))
	for government: Dictionary in _public_contexts():
		if str(government.id) == owner: return Economy.cash(world, owner)
	return 0


func _begin_generation(seed_value: int, options: Dictionary, for_practice: bool = false) -> void:
	if resolving or generator != null: return
	generation_cancelled = false
	pending_practice = for_practice
	var box: VBoxContainer = _open_modal("세계와 초기 경제를 준비하고 있습니다")
	_label(box, "세 대륙·도시·인물·공급망·개시 장부를 생성합니다. 기존 세계와 저장은 그대로 보존합니다.", 14, MUTED)
	_label(box, "작업을 취소해도 생성 중인 임시 세계는 현재 게임에 적용되지 않습니다.", 13, MUTED)
	_button(box, "생성 취소", _cancel_generation)
	generator = Thread.new()
	var copied: Dictionary = options.duplicate(true)
	var status: Error = generator.start(func():
		var candidate: Dictionary = Economy.new_world(seed_value, copied)
		var assessment: Dictionary = {"ok":false,"errors":["초기 공급망 검증기를 읽을 수 없습니다."],"warnings":[]}
		if ResourceLoader.exists("res://core/generation.gd"):
			var module: Variant = load("res://core/generation.gd")
			if module != null and module.has_method("validate"): assessment = module.validate(candidate)
		return {"world":candidate,"assessment":assessment}
	)
	if status != OK:
		generator = null
		_message("세계 생성 시작 실패", "작업을 시작하지 못했습니다. 기존 세계와 저장을 유지했습니다.")


func _cancel_generation() -> void:
	generation_cancelled = true
	_close_modal()
	_toast("세계 생성을 취소했습니다. 기존 세계와 저장은 유지합니다.")


func _exit_tree() -> void:
	if generator != null:
		generation_cancelled = true
		generator.wait_to_finish()
		generator = null
	if worker != null:
		worker.wait_to_finish()
		worker = null


func _market_order_book() -> void:
	var module: Variant = Domains.module_script("market")
	var card: VBoxContainer = _card(content, "공개 주문장")
	if module == null or not module.has_method("order_book"):
		_label(card, "주문 데이터를 읽을 수 없습니다.", 13, MUTED)
		return
	var rows: Array = module.order_book(world, "", city_id)
	if rows.is_empty(): _label(card, "현재 도시의 유효한 공개 주문이 없습니다. 실제 재고·예산을 예약하는 지정가 주문을 제출할 수 있습니다.", 13, MUTED)
	for index in range(mini(rows.size(), 24)):
		var order: Dictionary = rows[index]
		var product: Dictionary = _find(world.products, str(order.get("product_id", "")))
		_row(card, ("매입 · " if order.get("side", "") == "buy" else "매도 · ") + str(product.get("name", "상품")), "%s %s · %s" % [order.get("remaining", order.get("quantity", 0)), product.get("unit", ""), _money(int(order.get("limit_price", 0)))])
	_label(card, "매입 제한가는 운임·관세를 포함한 최대 도착단가입니다. 주문은 매출이나 도착 재고가 아니며, 월 정산에서 조건이 맞는 상대와 체결합니다.", 12, MUTED)


func _storage_slot(slot: String) -> String:
	return "practice-" + slot if practice_mode and not slot.begins_with("practice-") else slot


func _generation_result_dialog(candidate: Dictionary, assessment: Dictionary) -> void:
	if assessment.get("ok", false) and str(assessment.get("fingerprint", "")) != Economy.state_hash(candidate):
		assessment = {"ok":false,"errors":["사전 검사 후 원본 상태가 바뀌어 시작을 차단했습니다."],"warnings":[]}
	var box: VBoxContainer = _open_modal("초기 세계 검사 결과")
	_row(box, "세계 규모", "%d대륙 · %d국가 · %d도시" % [candidate.get("continents", []).size(), candidate.get("nations", []).size(), candidate.get("cities", []).size()])
	var errors: Array = assessment.get("errors", [])
	var warnings: Array = assessment.get("warnings", [])
	_row(box, "생성 검사", "통과" if assessment.get("ok", false) else "재생성 필요")
	for error in errors: _label(box, _generation_message(str(error), candidate), 13, RED)
	for warning in warnings: _label(box, _generation_message(str(warning), candidate), 13, GOLD)
	var observation: Dictionary = assessment.get("observation", {})
	_row(box, "별도 사전 관찰", str(observation.get("months", 0)) + "개월")
	for turn: Dictionary in observation.get("turns", []):
		_row(box, "%d개월차 실제 관찰" % int(turn.get("month",0)), "부족률 %.2f%% · 체결 %d건" % [float(turn.get("shortage_rate_percent",0)),int(turn.get("trades",0))])
	_label(box, "사전 관찰은 복사한 세계에서 수행했습니다. 시작할 세계의 월·재고·장부·자산은 원본을 유지합니다.", 13, MUTED)
	if assessment.get("ok", false):
		_button(box, "연습 시작" if pending_practice else "시작 인물 선택", func():
			if pending_practice: _enter_practice(candidate)
			else: _start_character_dialog(candidate)
		, true)
	_button(box, "다시 생성", _start_practice_dialog if pending_practice else _new_game_dialog)
	_button(box, "취소", _close_modal)


func _start_practice_dialog() -> void:
	if resolving or generator != null: return
	var box: VBoxContainer = _open_modal("독립 연습 세계")
	_label(box, "생산 확인 → 유효한 명령 → 건설 예약 → 한 달 정산 → 실제 보고서 확인을 직접 수행합니다. 연습에도 정상 비용·시간·AI 경쟁이 적용됩니다.", 14, MUTED)
	_label(box, "현재 세계의 미저장 계획은 별도 보관하고, 연습 저장은 practice- 슬롯만 사용합니다.", 13, GOLD)
	_button(box, "연습 세계 생성", func():
		if not world.is_empty() and not _archive_before_new_game(): _message("진행 보관 실패", "현재 세계를 보관하지 못해 연습으로 이동하지 않았습니다."); return
		_begin_generation(42, {"cities_per_nation":1,"expanded_industries":false}, true)
	, true)
	_button(box, "취소", _close_modal)


func _enter_practice(candidate: Dictionary, save_slot: String = "autosave") -> void:
	world = candidate
	world["session_kind"] = "practice"
	practice_mode = true
	practice_steps = {}
	goal_mode = "none"
	context_company_id = "__default__"
	context_government_id = ""
	city_id = str(_person().get("city_id", "city-0")); scope = city_id
	outer = 0; paths = [[0,0,0],[0,0,0],[0,0,0],[0,0,0],[0,0,0]]
	screen = "game"; dirty = true; scroll_positions = {}; last_screen_key = ""
	_close_modal(); _render()
	if not _save(save_slot, false): _message("연습 저장 실패", "연습 세계는 열렸으나 별도 저장에 실패했습니다. 기존 샌드박스 저장은 유지합니다.")


func _practice_panel() -> void:
	for id in world.get("used_command_ids", []):
		if str(id).begins_with("player-"): practice_steps["command"] = true
	for project in _projects_for_company():
		if project.get("status", "") != "cancelled": practice_steps["build"] = true
	var done: Array = [bool(practice_steps.get("inspect", false)), bool(practice_steps.get("command", false)), bool(practice_steps.get("build", false)), not world.get("last_report", {}).is_empty(), bool(practice_steps.get("report", false))]
	var completed: int = done.count(true)
	var card: VBoxContainer = _card(content, "연습 · %d / 5" % completed)
	var labels: Array = ["생산과 원료·노동 상태를 확인하세요.", "권한과 비용을 통과한 명령을 추가하세요.", "빈 부지에 건설을 예약하세요.", "다음 달을 눌러 실제 한 달을 정산하세요.", "확정된 월간 보고서를 확인하세요."]
	if completed >= 5:
		_label(card, "학습을 마쳤습니다. 같은 규칙으로 계속 연습할 수 있습니다.", 13, MINT)
	else:
		var next: int = done.find(false)
		_label(card, labels[next], 13, GOLD)
		match next:
			0: _button(card, "생산 확인", func(): _go(1,[1,0,0]))
			1: _button(card, "반복 정책 설정", func(): _go(1,[1,2,0]))
			2: _button(card, "건설 계획", func(): _go(1,[2,0,0]))
			3: _button(card, "월말 점검", _preflight)
			4: _button(card, "월간 보고", func(): _show_report(world.get("last_report",{})))
	_button(card, "연습 종료 · 타이틀", func(): _leave_dialog("title"))


func _goal_dialog() -> void:
	var box: VBoxContainer = _open_modal("선택 목표")
	_label(box, "목표는 실제 시설·이익·계약 상태에서 판정합니다. 달성 후에도 계속 플레이할 수 있습니다.", 13, MUTED)
	for definition: Dictionary in [{"id":"construction","label":"내 시설 첫 준공"},{"id":"profit","label":"누적 이익 흑자"},{"id":"procurement","label":"공공 납품 이행"},{"id":"none","label":"목표 없이 샌드박스"}]:
		var id: String = definition.id
		_button(box, str(definition.label), func(): goal_mode=id; world["scenario_goal"]=id; dirty=true; _close_modal(); _render(); _save_view(_storage_slot("autosave")))
	_button(box, "취소", _close_modal)


func _goal_panel() -> void:
	var owner: String = str(_company().get("id", ""))
	var achieved: bool = false
	var label: String = ""
	match goal_mode:
		"construction":
			label = "내 시설 첫 준공"
			for project in world.get("projects", []):
				if project.company_id == owner and project.status == "completed": achieved=true
		"profit":
			label = "누적 이익 흑자"
			var module: Variant = Domains.module_script("finance")
			if module != null and not owner.is_empty(): achieved = module.retained_earnings(world,owner)>0 and not world.get("last_report",{}).is_empty()
		"procurement":
			label = "공공 납품 이행"
			for contract in world.get("public",{}).get("procurements",[]):
				if contract.company_id == owner and int(contract.get("quantity",0))>0 and int(contract.get("accepted_quantity",0))>=int(contract.quantity): achieved=true
	var card: VBoxContainer = _card(content, "목표 · " + label)
	_label(card, "달성했습니다. 계속 플레이하거나 새 목표를 선택하세요." if achieved else "진행 중 · 선택한 법인의 실제 월말 기록으로 판정합니다.", 13, MINT if achieved else MUTED)
	_button(card, "목표 변경", _goal_dialog)


func _generation_message(code: String, candidate: Dictionary) -> String:
	var parts: PackedStringArray = code.split(":")
	var product: String = str(_find(candidate.get("products", []), str(parts[-1])).get("name", parts[-1]))
	match str(parts[0]):
		"OPENING_BUFFER_DEPLETED": return "초기 완충 재고가 관찰 중 소진되었습니다: " + product
		"UNMET_HOUSEHOLD_NEEDS": return "사전 관찰의 미충족 생활 수요: " + str(parts[-1]) + "건"
		"SHORT_OBSERVATION_NOT_LONG_RUN_BALANCE": return "짧은 사전 관찰은 장기 균형을 보장하지 않습니다."
		"STRUCTURAL_CHECK_ONLY_NO_PREVIEW_MONTHS": return "공급 경로·장부의 구조만 검사했으며 월별 작동은 관찰하지 않았습니다."
		"SUPPORT_PRODUCTION_MISSING": return "정비·지원 품목의 재생산 시설을 확인하세요: " + product
		"UPSTREAM_BUFFER_DEPENDENCY": return "일부 상위 공정이 초기 투입재 재고에 의존합니다: " + product
		"ESSENTIAL_SUPPLY_PATH_MISSING":
			var city: String = str(_find(candidate.get("cities", []), parts[1] if parts.size()>1 else "").get("name", "도시"))
			return city + "의 필수재 재생산·운송 경로가 부족합니다: " + product
		"ESSENTIAL_PRODUCT_UNDEFINED": return "필수 품목의 정의가 누락되었습니다: " + product
		"PUBLIC_OPENING_CASH_EMPTY": return "일부 공공기관의 개시 지급 자금이 없습니다. " + str(parts[-1])
		"PREVIEW_MONTH": return "사전 정산이 완료되지 않았습니다: " + _reason(str(parts[-1]))
		"GENERATION_CHECK_MUTATED_REAL_START": return "사전 관찰이 원본 세계를 변경하여 시작을 차단했습니다."
	return _reason(code)


func _scope_label(id: String) -> String:
	if id == "world": return "세계"
	for group: String in ["cities","nations","continents"]:
		var entity: Dictionary = _find(world.get(group, []),id)
		if not entity.is_empty(): return str(entity.get("name",id))
	return "선택 범위"


func _scope_cities() -> Array:
	var result: Array = []
	for city: Dictionary in world.get("cities", []):
		if scope == "world" or str(city.id)==scope or str(city.nation_id)==scope or str(city.continent_id)==scope: result.append(str(city.id))
	return result
