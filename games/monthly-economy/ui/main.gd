extends Control

const Economy = preload("res://core/economy.gd")
const Ledger = preload("res://core/ledger.gd")
const Store = preload("res://core/save_store.gd")
const CityMap = preload("res://ui/city_map.gd")
const FONT = preload("res://assets/NotoSansKR.otf")
const BG = Color("0b141e")
const PANEL = Color("142432")
const PANEL_LIGHT = Color("1b3040")
const TEXT = Color("e7eef2")
const MUTED = Color("9caebb")
const MINT = Color("8de2bf")
const GOLD = Color("edbf7e")
const RED = Color("f09b98")
const NAV = ["메인", "사업", "시장", "자산", "확장"]
const TREE = [
	{"현황": {"기업": ["요약", "생산", "판매"], "거시경제": ["생산", "고용", "물가"], "산업": ["수급", "경쟁", "병목"]}, "도시": ["지도", "시설", "인구"], "알림": ["작업", "경영", "사건"]},
	{"회사": ["법인", "조직", "전략"], "운영": ["생산", "유통", "정책"], "프로젝트": ["건설", "계획", "이력"]},
	{"상품": ["가격", "수급", "거래"], "인력": ["고용", "시간", "임금"], "결제": ["입출금", "원장", "예약"]},
	{"개인": ["자금", "직위", "이력"], "법인": ["자금", "시설", "수지"], "부동산": ["토지", "건물", "공사"]},
	{"세계": ["대륙", "국가", "도시"], "규칙": ["경제", "공정", "범위"], "게임관리": ["저장", "설정", "정보"]}
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


func _ready() -> void:
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
	if resolving and worker != null and not worker.is_alive():
		var result: Dictionary = worker.wait_to_finish()
		worker = null
		resolving = false
		_close_modal()
		if not result.get("ok", false):
			_message("정산을 확정하지 않았습니다", "이전 월과 계획을 보존했습니다.\n" + str(result.get("errors", [])))
			return
		var candidate: Dictionary = result["state"]
		var save_result: Dictionary = Store.save_world(candidate, "autosave")
		if not save_result.get("ok", false):
			_message("저장 공간을 확인하세요", "월 결과를 저장하지 못해 이전 월과 계획을 유지했습니다.\n" + str(save_result.get("error", "저장 실패")))
			return
		world = candidate
		dirty = false
		saved_hash = Economy.state_hash(world)
		_render()
		_show_report(result.get("report", {}))


func _apply_theme() -> void:
	var t := Theme.new()
	t.default_font = FONT
	t.default_font_size = int(16 * float(settings.get("text_scale", 1.0)))
	t.set_color("font_color", "Label", TEXT)
	t.set_color("font_color", "Button", TEXT)
	t.set_color("font_hover_color", "Button", MINT)
	t.set_color("font_pressed_color", "Button", MINT)
	t.set_stylebox("normal", "Button", _style(PANEL_LIGHT, 10))
	t.set_stylebox("hover", "Button", _style(Color("264454"), 10))
	t.set_stylebox("pressed", "Button", _style(Color("2c554c"), 10, MINT))
	t.set_stylebox("focus", "Button", _style(Color(0, 0, 0, 0), 10, MINT))
	t.set_stylebox("disabled", "Button", _style(PANEL, 10))
	t.set_color("font_disabled_color", "Button", MUTED)
	t.set_stylebox("normal", "LineEdit", _style(BG, 8, MUTED))
	t.set_color("font_color", "LineEdit", TEXT)
	t.set_color("font_color", "OptionButton", TEXT)
	t.set_stylebox("normal", "OptionButton", _style(PANEL_LIGHT, 8))
	t.set_stylebox("panel", "PopupMenu", _style(PANEL, 8))
	t.set_color("font_color", "PopupMenu", TEXT)
	t.set_constant("separation", "VBoxContainer", 12)
	t.set_constant("separation", "HBoxContainer", 10)
	theme = t


func _style(color: Color, radius: int = 14, border: Color = Color.TRANSPARENT) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = color
	s.corner_radius_top_left = radius
	s.corner_radius_top_right = radius
	s.corner_radius_bottom_left = radius
	s.corner_radius_bottom_right = radius
	s.content_margin_left = 14
	s.content_margin_right = 14
	s.content_margin_top = 12
	s.content_margin_bottom = 12
	if border.a > 0:
		s.border_color = border
		s.set_border_width_all(1)
	return s


func _render() -> void:
	if is_instance_valid(scroll) and last_screen_key != "":
		scroll_positions[last_screen_key] = scroll.scroll_vertical
	for c in get_children():
		remove_child(c)
		c.queue_free()
	modal = null
	toast_label = null
	var background := ColorRect.new()
	background.color = Color.BLACK if settings.get("high_contrast", false) else BG
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
	frame.add_theme_constant_override("separation", 10)
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
	content.add_theme_constant_override("separation", 12)
	scroll.add_child(content)
	last_screen_key = str(outer) + str(paths[outer]) + city_id
	var active: Array = _active_path()
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
	var result: Dictionary = {"left": 16, "right": 16, "top": 16, "bottom": 12}
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
	b.custom_minimum_size.y = 46
	b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	b.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	if accent:
		b.add_theme_stylebox_override("normal", _style(Color("80d9b5"), 10))
		b.add_theme_color_override("font_color", BG)
		b.add_theme_color_override("font_hover_color", TEXT)
	b.pressed.connect(action)
	parent.add_child(b)
	return b


func _card(parent: Node, heading: String = "", eyebrow: String = "") -> VBoxContainer:
	var panel := PanelContainer.new()
	panel.add_theme_stylebox_override("panel", _style(PANEL))
	parent.add_child(panel)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 10)
	panel.add_child(box)
	if eyebrow != "":
		_label(box, eyebrow, 12, MINT)
	if heading != "":
		_label(box, heading, 21)
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
	return _find(world.get("companies", []), str(world.get("controlled_company_id", "co-0")))


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
	box.add_theme_constant_override("separation", 18)
	title_scroll.add_child(box)
	_label(box, "H O R A   /   E C O N O M Y", 14, MINT)
	var spacer := Control.new()
	spacer.custom_minimum_size.y = 34
	box.add_child(spacer)
	_label(box, "한 달의 결정,\n서로 연결된 세계.", 35)
	_label(box, "월간 경제 실험실", 19, GOLD)
	_label(box, "사람이 일하고, 기업이 생산하고,\n도시가 거래하는 경제를 관찰하세요.", 16, MUTED)
	var illustration = CityMap.new()
	illustration.custom_minimum_size.y = 230
	illustration.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_child(illustration)
	illustration.update_world(Economy.new_world(42), "city-0")
	var auto_result: Dictionary = Store.load_world("autosave")
	var continue_button: Button = _button(box, "계속하기", func(): _load("autosave"), true)
	continue_button.disabled = not auto_result.get("ok", false)
	if auto_result.get("ok", false):
		var previous: Dictionary = auto_result["state"]
		_label(box, "최근 저장 · 계획 %d개월차 · 자동 저장" % int(previous.get("planning_month", 1)), 12, MUTED)
	else:
		_label(box, "정상 자동 저장이 아직 없습니다.", 12, MUTED)
	_button(box, "새 세계 시작", _new_game_dialog)
	var row := HBoxContainer.new()
	box.add_child(row)
	_button(row, "불러오기", _load_dialog)
	_button(row, "설정", _settings_dialog)
	_button(row, "정보", _info_dialog)
	_button(box, "종료", func(): get_tree().quit())
	_label(box, "EXPERIMENTAL 0.1  ·  오프라인 검증 빌드\n전체 기획의 도시 경제·건설 검증 단계입니다. 은행·정치·군수 등 남은 범위는 정보에서 확인할 수 있습니다.", 12, MUTED)


func _header() -> void:
	var line := HBoxContainer.new()
	frame.add_child(line)
	_label(line, "H O R A", 22, MINT)
	var date: Label = _label(line, _month() + "  ·  계획", 15, GOLD)
	date.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	var context := HBoxContainer.new()
	frame.add_child(context)
	var name_value: String = str(_person().get("name", "내 인물"))
	var company_name: String = str(_company().get("name", "권한 있는 법인"))
	var context_button: Button = _button(context, name_value + "  /  " + company_name, _context_dialog)
	context_button.alignment = HORIZONTAL_ALIGNMENT_LEFT
	context_button.add_theme_font_size_override("font_size", 13)
	var chooser := OptionButton.new()
	chooser.custom_minimum_size = Vector2(138, 42)
	var ids: Array = []
	for city in world.get("cities", []):
		chooser.add_item(str(city["name"]))
		ids.append(str(city["id"]))
	chooser.add_item("세계 집계")
	ids.append("world")
	chooser.select(maxi(ids.find(scope), 0))
	chooser.item_selected.connect(func(index: int):
		scope = ids[index]
		if scope != "world":
			city_id = scope
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
	row.add_theme_constant_override("separation", 6)
	parent.add_child(row)
	for i in labels.size():
		var index: int = i
		var button: Button = _button(row, str(labels[i]), func():
			paths[outer][depth] = index
			for j in range(depth + 1, 3):
				paths[outer][j] = 0
			full_map = false
			_render()
		)
		button.custom_minimum_size.y = 35 if depth > 0 else 40
		button.add_theme_font_size_override("font_size", 13 if depth > 0 else 15)
		if i == selected:
			var selected_style: StyleBoxFlat = _style(Color("21453e") if depth == 0 else PANEL_LIGHT, 8, MINT if depth == 0 else Color.TRANSPARENT)
			selected_style.content_margin_top = 5
			selected_style.content_margin_bottom = 5
			button.add_theme_stylebox_override("normal", selected_style)
			button.add_theme_color_override("font_color", MINT)
		else:
			var normal_style: StyleBoxFlat = _style(Color("101d28"), 8)
			normal_style.content_margin_top = 5
			normal_style.content_margin_bottom = 5
			button.add_theme_stylebox_override("normal", normal_style)
			button.add_theme_color_override("font_color", MUTED)


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
	var month_button: Button = _button(frame, "다음 달 진행    →    " + str(int(world.get("planning_month", 1)) + 1) + "개월차", _preflight, true)
	month_button.custom_minimum_size.y = 50
	month_button.disabled = resolving
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	frame.add_child(row)
	var glyphs: Array = ["◉", "▥", "⇄", "◇", "···"]
	for i in NAV.size():
		var index: int = i
		var b: Button = _button(row, glyphs[i] + "\n" + NAV[i], func():
			outer = index
			full_map = false
			_render()
		)
		b.custom_minimum_size.y = 58
		b.add_theme_font_size_override("font_size", 13)
		b.add_theme_stylebox_override("normal", _style(Color("1b3936") if outer == i else Color("101d28"), 9))
		b.add_theme_color_override("font_color", MINT if outer == i else MUTED)
	_label(frame, "실험 규칙 · 버튼을 눌렀을 때만 1개월 진행", 11, MUTED).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


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
				else: _projects_page()
			elif first == "운영":
				if leaf == "생산": _production_page()
				elif leaf == "유통": _inventory_page()
				else: _policy_page()
			else:
				if leaf == "법인": _company_page()
				elif leaf == "조직": _people_page("조직")
				else: _policy_page()
		2:
			if first == "상품":
				if leaf == "거래": _trade_page()
				else: _market_page(leaf)
			elif first == "인력": _people_page(leaf)
			else:
				if leaf == "예약": _plans_page()
				else: _journal_page(leaf)
		3:
			if first == "개인": _personal_page(leaf)
			elif first == "법인":
				if leaf == "시설": _facility_list(true)
				elif leaf == "수지": _journal_page("입출금")
				else: _company_page()
			else:
				if leaf == "공사": _projects_page()
				elif leaf == "건물": _facility_list(true)
				else: _map_page()
		4:
			if first == "세계": _world_page(leaf)
			elif first == "규칙": _rules_page(leaf)
			elif leaf == "저장": _save_page()
			elif leaf == "설정": _settings_page(content)
			else: _info_page(content)


func _dashboard() -> void:
	var co: Dictionary = _company()
	var report: Dictionary = world.get("last_report", {})
	var hero: VBoxContainer = _card(content, str(co.get("name", "내 회사")), "COMPANY PULSE  /  법인 현금 · 확정 잔액")
	hero.add_theme_constant_override("separation", 5)
	_label(hero, _money(Economy.cash(world, str(co.get("id", "co-0")))), 29, MINT)
	var amounts: HBoxContainer = HBoxContainer.new()
	hero.add_child(amounts)
	var l := VBoxContainer.new()
	var r := VBoxContainer.new()
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	r.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	amounts.add_child(l)
	amounts.add_child(r)
	var own_stats: Dictionary = _own_cash_flow()
	_label(l, "직전 수입  " + _money(own_stats["income"]), 13, MUTED)
	_label(r, "직전 지급  " + _money(own_stats["outflow"]), 13, GOLD)
	_label(hero, "개인 현금  " + _money(Economy.cash(world, str(world.get("player_id", "p-0")))) + "  ·  법인과 별도 소유", 12, MUTED)
	var map_card: VBoxContainer = _card(content, str(_find(world.get("cities", []), city_id).get("name", "도시")) + " · 타일 도시")
	_add_map(map_card, 215)
	var line := HBoxContainer.new()
	map_card.add_child(line)
	_button(line, "도시 살펴보기", func(): _go(0, [1, 0, 0]))
	_button(line, "시설 건설", func(): _go(1, [2, 0, 0]), true)
	var tasks: VBoxContainer = _card(content, "이번 달 살펴볼 일", "NEXT ACTIONS")
	_row(tasks, "내 계획", str(world.get("commands", []).size()) + "개")
	_row(tasks, "진행 중 공사", str(_projects_for_company().size()) + "개")
	if report.is_empty():
		_label(tasks, "첫 월을 진행하면 생산·판매·급여·생활 소비가 실제 거래로 기록됩니다.", 14, MUTED)
	else:
		_label(tasks, str(report.get("summary", "직전 월 정산 완료")), 14, MUTED)
		_button(tasks, "직전 월 보고서", func(): _show_report(report))


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
	map.update_world(world, city_id)
	map.tile_selected.connect(func(x: int, y: int):
		selected_tile = Vector2i(x, y)
		if build_mode:
			_build_confirm(x, y)
	)
	map.facility_selected.connect(func(id: String): _facility_dialog(id))


func _map_page(construction: bool = false) -> void:
	build_mode = construction
	var card: VBoxContainer = _card(content, "도시에 직접 짓는 사업", "BUILD & OPERATE" if construction else "CITY MAP")
	_label(card, "시설을 누르면 운영 상태를 확인합니다. 지도를 끌어 이동하고 확대할 수 있습니다.", 13, MUTED)
	if construction:
		var chooser := OptionButton.new()
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
	var owner: String = str(_company().get("id", ""))
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
	if facility.is_empty(): return
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
	var card: VBoxContainer = _card(content, "도시 상품 시장", "MARKET / " + ("세계" if scope == "world" else str(_find(world["cities"], scope).get("name", scope))))
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
	var owner: String = str(_company().get("id", ""))
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
		if str(p.get("city_id", "")) != city_id: continue
		var own: bool = p.get("company_id", "") == _company().get("id", "")
		if mode == "조직" and not own: continue
		_row(card, str(p.get("name", p.get("id", ""))), _role(str(p.get("role", ""))), MINT if own else TEXT)
		var detail: String = str(_find(world.get("companies", []), str(p.get("company_id", ""))).get("name", "구직 상태"))
		if mode in ["임금", "조직"] and own: detail += " · 시간당 " + _money(int(p.get("wage", 0)))
		if mode == "시간": detail += " · 가용 " + str(p.get("hours", 0)) + "시간"
		_label(card, detail, 12, MUTED)


func _company_page() -> void:
	var card: VBoxContainer = _card(content, str(_company().get("name", "내 법인")), "LEGAL ENTITY")
	_row(card, "대표", str(_person().get("name", "")))
	_row(card, "법인 현금", _money(Economy.cash(world, str(_company().get("id", "")))), MINT)
	_row(card, "개인 현금", _money(Economy.cash(world, str(world.get("player_id", "")))))
	_row(card, "경영 권한", "대표 권한 있음")
	_label(card, "대표의 경영 권한과 회사 소유 재산은 구분합니다. 이 빌드에서는 대표 교체·지분 거래·창업 절차가 아직 구현되지 않았습니다.", 14, MUTED)
	_button(card, "자기 법인의 원장", func(): _go(2, [2, 1, 0]))
	_inventory_page()


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
	var card: VBoxContainer = _card(content, str(_person().get("name", "내 인물")), "PERSON / " + mode)
	if mode == "자금":
		_label(card, _money(Economy.cash(world, str(world.get("player_id", "")))), 30, MINT)
		_label(card, "개인 현금 · 법인 현금 포함 안 함", 13, MUTED)
		_label(card, "임금은 법인의 실제 지급에서, 생활 소비는 개인의 실제 잔액에서 처리합니다.", 14, MUTED)
	elif mode == "직위":
		_row(card, "역할", _role(str(_person().get("role", "대표"))))
		_row(card, "근무 법인", str(_company().get("name", "")))
		_row(card, "거주 도시", str(_find(world["cities"], str(_person().get("city_id", ""))).get("name", "")))
		_label(card, "플레이어와 AI의 명령은 같은 권한 검사기를 사용합니다. 전면적인 구직·창업·이직은 후속 구현 범위입니다.", 14, MUTED)
	else:
		for entry in world.get("journal", []):
			if int(entry.get("month", 0)) != int(world.get("last_resolved_month", 0)): continue
			for line in entry.get("lines", []):
				if str(line.get("account", "")) == str(world["player_id"]) + ":cash":
					_row(card, _reason(str(entry.get("reason", "거래"))), _money(int(line.get("debit", 0)) - int(line.get("credit", 0))))


func _own_cash_flow() -> Dictionary:
	var result: Dictionary = {"income": 0, "outflow": 0}
	var account: String = str(_company().get("id", "")) + ":cash"
	for entry in world.get("journal", []):
		if int(entry.get("month", 0)) != int(world.get("last_resolved_month", 0)) or int(entry.get("month", 0)) == 0: continue
		for line in entry.get("lines", []):
			if line.get("account", "") == account:
				result["income"] += int(line.get("debit", 0))
				result["outflow"] += int(line.get("credit", 0))
	return result


func _journal_page(mode: String) -> void:
	var card: VBoxContainer = _card(content, "현금 흐름과 복식 원장", "LEDGER / 내 법인")
	var stats: Dictionary = _own_cash_flow()
	_row(card, "직전 월 현금 유입", _money(stats["income"]), MINT)
	_row(card, "직전 월 현금 유출", _money(stats["outflow"]), GOLD)
	_row(card, "현재 현금", _money(Economy.cash(world, str(_company().get("id", "")))))
	_label(card, "현재는 현금 거래와 대응 수익·비용의 복식 기록입니다. 발생주의 자산평가·감가상각·GDP는 아직 제공하지 않습니다.", 12, MUTED)
	var count_value: int = 0
	var entries: Array = world.get("journal", [])
	for i in range(entries.size() - 1, -1, -1):
		var entry: Dictionary = entries[i]
		var own_lines: Array = entry.get("lines", []).filter(func(l): return str(l.get("account", "")).begins_with(str(_company().get("id", "")) + ":"))
		if own_lines.is_empty(): continue
		_label(card, "%d월 · %s" % [entry.get("month", 0), _reason(str(entry.get("reason", "거래")))], 14, GOLD)
		for line in own_lines:
			if mode == "입출금" and not str(line["account"]).ends_with(":cash"): continue
			var kind: String = str(line["account"]).split(":")[-1]
			_row(card, str({"cash": "현금", "equity": "자본", "expense": "비용", "revenue": "수입", "asset": "자산", "liability": "부채"}.get(kind, kind)), "차 " + _money(int(line.get("debit", 0))) + " / 대 " + _money(int(line.get("credit", 0))))
		count_value += 1
		if count_value >= 25: break


func _aggregate_page() -> void:
	var stats: Dictionary = Economy.metrics(world, scope)
	var card: VBoxContainer = _card(content, "하위 도시에서 올라온 경제", "ECONOMY / 실제 집계")
	_row(card, "인물", str(stats.get("population", 0)) + "명")
	_row(card, "고용", str(stats.get("employed", 0)) + "명")
	_row(card, "화폐 잔액 합계", _money(int(stats.get("cash", 0))))
	_label(card, "수량 단위가 다른 품목의 생산량은 하나의 GDP로 합산하지 않습니다. 품목별 생산·소비·부족은 시장 → 상품에서 확인하세요.", 14, MUTED)
	_label(card, "대륙·국가 화면은 새로운 소비자나 무한 재고를 만들지 않습니다.", 14, MINT)


func _world_page(level: String) -> void:
	var collection: String = "continents" if level == "대륙" else ("nations" if level == "국가" else "cities")
	var intro: VBoxContainer = _card(content, "세 대륙, 연결될 세계", "WORLD / EXPERIMENTAL FIXTURE")
	_label(intro, "실험 세계: 3대륙 × 1국가 × 1도시. 최종 규모는 미정입니다. 각 도시는 같은 경제 규칙을 사용합니다. 도시 간 단순 발송·다음 월 도착을 지원하며, 국제 물류 경로·관세·외환은 후속 범위입니다.", 14, MUTED)
	for entity in world.get(collection, []):
		var stats: Dictionary = Economy.metrics(world, str(entity["id"]))
		var card: VBoxContainer = _card(content, str(entity.get("name", entity["id"])))
		_row(card, "인물 / 고용", "%s / %s명" % [stats.get("population", 0), stats.get("employed", 0)])
		_row(card, "실제 계정 현금 합계", _money(int(stats.get("cash", 0))))
		if level == "도시":
			var id: String = str(entity["id"])
			_button(card, "이 도시 지도 열기", func():
				city_id = id
				scope = id
				_go(0, [1, 0, 0])
			)


func _events_page(mode: String) -> void:
	var card: VBoxContainer = _card(content, "관찰한 원인과 결과", "EVENTS / " + mode)
	var events: Array = world.get("events", [])
	var count_value: int = 0
	var owner: String = str(_company().get("id", ""))
	for i in range(events.size() - 1, -1, -1):
		var e: Dictionary = events[i]
		if str(e.get("city_id", "")) != city_id: continue
		if mode in ["경영", "작업"] and str(e.get("actor_id", "")) not in [owner, str(world.get("player_id", ""))]: continue
		if mode == "병목" and str(e.get("type", "")) not in ["shortage", "construction_delay", "unpaid", "unmet_need", "production_limit"]: continue
		# Only authorized company events and public aggregate shortage explanations.
		if e.get("actor_id", "") != owner and str(e.get("type", "")) in ["purchase", "sale", "trade", "wage"]: continue
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
		_label(card, "전체 목표를 단계별로 추적합니다. 현재 검증 범위는 도시의 실물 순환·현금 원장·타일 공사·반복 AI·결정적 월 처리·로컬 저장입니다.\n\n전면적인 인물 진로, 주식·신용·은행, 소프트웨어와 전체 산업, 국제 운송·외환, 도시·국가 정치·외교·군수는 남아 있습니다.", 15)
	_label(card, "세계 규모·가격·임금·레시피·공기 등 모든 실험 수치는 출시 밸런스가 아닙니다. 기획서의 미정값을 사용자 확정값으로 바꾸지 않습니다.", 13, GOLD)


func _save_page() -> void:
	var card: VBoxContainer = _card(content, "진행과 복구", "SESSION / 저장 관리")
	_row(card, "현재 계획 월", _month())
	_row(card, "미저장 변경", "있음" if dirty else "없음")
	_button(card, "수동 저장", func(): _save("manual", true), true)
	_button(card, "저장 불러오기", _load_dialog)
	_button(card, "최근 월 보고서", func(): _show_report(world.get("last_report", {})))
	var management: VBoxContainer = _card(content, "세션 관리")
	_button(management, "새 게임", func(): _leave_dialog("new"))
	_button(management, "타이틀로 돌아가기", func(): _leave_dialog("title"))
	_button(management, "앱 종료", func(): _leave_dialog("quit"))
	_label(management, "정산 결과는 검증한 후 저장합니다. 불러오기·보고 확인·앱 복귀로 월을 다시 처리하지 않습니다.", 13, MUTED)


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
	_label(card, "숫자는 통화 CR와 품목 단위를 함께 표시합니다. 움직이는 필수 연출과 시간 제한이 없습니다. 음악·진동·계정·원격 수집 기능은 이 빌드에서 사용하지 않습니다.", 14, MUTED)
	_button(card, "표시 설정 초기화", func():
		settings = {"text_scale": 1.0, "high_contrast": false, "reduce_motion": true}
		_save_preferences()
		_apply_theme()
		_render()
	)


func _info_page(parent: Node) -> void:
	var card: VBoxContainer = _card(parent, "월간 경제 실험실", "HORA / 0.1.0 EXPERIMENTAL")
	_label(card, "사용자 기획서 2026-10-25 · 설계 0.1\nGodot 4.6.3 · 오프라인 싱글플레이 검증 빌드\n저장 형식 1 · 실험 규칙 experimental-0.1", 14, MUTED)
	_label(card, "현재 제공: 유한한 도시 생산·거래·소비, 개인/법인 계정 분리, 반복 정책, 타일 건설과 월말 보고, 로컬 저장·복구.\n\n남은 범위: 모든 인물의 자유 진로, 소유권·대표 교체, 전체 산업·소프트웨어·서비스, 은행·금융, 국제 물류·외환, 정치·외교·국방 조달, 기기별 성능·접근성 검증.", 14)
	_label(card, "원작 UI·명칭·상품 수치를 복제하지 않습니다. 도시는 코드로 그린 자체 도형, 글꼴은 Noto Sans KR (SIL OFL 1.1)입니다. Godot는 MIT 라이선스입니다. 실제 기기 성능과 장기 경제 타당성은 별도 검증 대상입니다.", 12, MUTED)
	_button(card, "소스·요구 추적표", func(): OS.shell_open("https://github.com/hddy774/project-hora"))
	_button(card, "라이선스 보기", func(): _message("오픈소스 고지", "Noto Sans CJK KR — SIL Open Font License 1.1\n저작권 2014–2021 Adobe.\n\n" + FileAccess.get_file_as_string("res://assets/FONT-LICENSE.txt") + "\n\n" + FileAccess.get_file_as_string("res://assets/GODOT-NOTICES.txt")))


func _preflight() -> void:
	if resolving: return
	var problems: Array = Economy.validate(world)
	if not problems.is_empty():
		_message("상태 검증 실패", "월을 진행하지 않았습니다.\n" + "\n".join(problems))
		return
	var box: VBoxContainer = _open_modal("다음 달 전 점검")
	_row(box, "정산할 월", _month())
	_row(box, "자기 명령", str(world.get("commands", []).size()) + "개")
	_row(box, "법인 현금", _money(Economy.cash(world, str(_company().get("id", "")))))
	_row(box, "진행 공사", str(_projects_for_company().size()) + "개")
	var payroll: int = 0
	for p in world.get("people", []):
		if p.get("company_id", "") == _company().get("id", ""):
			payroll += int(p.get("wage", 0)) * (6 if p.get("role", "") == "director" else int(p.get("hours", 0)))
	_row(box, "운영 노동 한도 내 임금", "최대 " + _money(payroll))
	_label(box, "원료 발주·급여·가계 소비와 다른 인물의 계획을 함께 정산합니다. 수입은 보장되지 않으며 자금·자재 부족 시 미이행과 지연이 발생할 수 있습니다.", 14, GOLD)
	_button(box, "1개월 정산 시작", _resolve, true)
	_button(box, "계획으로 돌아가기", _close_modal)


func _resolve() -> void:
	if resolving: return
	var saved: Dictionary = Store.save_world(world, "autosave")
	if not saved.get("ok", false):
		_message("정산 전 저장 실패", str(saved.get("error", "")))
		return
	resolving = true
	var box: VBoxContainer = _open_modal("한 달을 정산하고 있습니다")
	_label(box, "계획 검증 → 생산·거래·소비 → 공사 → 장부 검사 → 저장\n\n계획 변경은 다음 월에 할 수 있습니다. 중단하면 마지막으로 저장한 정상 월에서 복구합니다.", 14, MUTED)
	worker = Thread.new()
	var snapshot: Dictionary = world.duplicate(true)
	var code: Error = worker.start(func(): return Economy.resolve_month(snapshot))
	if code != OK:
		worker = null
		resolving = false
		_message("정산 시작 실패", "작업을 시작하지 못했습니다. 이전 상태를 보존했습니다.")


func _show_report(report: Dictionary) -> void:
	if report.is_empty():
		_message("아직 월간 보고서가 없습니다", "첫 월을 정산하면 실제 생산·소비·지급·공정 결과를 확인할 수 있습니다.")
		return
	var box: VBoxContainer = _open_modal("%d개월차 월간 보고" % int(report.get("month", 0)))
	_label(box, "MONTH CLOSED / 검증된 확정 기록", 12, MINT)
	_label(box, str(report.get("summary", "한 달의 정산을 완료했습니다.")), 19)
	_row(box, "실제 상품 체결", str(report.get("trades", 0)) + "건")
	_row(box, "준공", str(report.get("construction_completed", 0)) + "개")
	var flow: Dictionary = _own_cash_flow()
	_row(box, "내 법인 수입", _money(flow["income"]), MINT)
	_row(box, "내 법인 지급", _money(flow["outflow"]), GOLD)
	_label(box, "상품별 생산·소비·부족은 시장에서 단위별로 확인합니다. 보고를 다시 열거나 저장을 불러와도 이 월을 재정산하지 않습니다.", 13, MUTED)
	_button(box, "원인과 사건 보기", func():
		_close_modal()
		_go(0, [2, 2, 0])
	)
	_button(box, "다음 계획 열기 · " + _month(), func():
		world["report_pending"] = false
		_save("autosave", false)
		_close_modal()
	, true)


func _queue(type: String, payload: Dictionary) -> Dictionary:
	var command: Dictionary = {
		"id": "player-%d-%d" % [int(world.get("planning_month", 1)), _next_command_serial()],
		"actor_id": str(world.get("player_id", "p-0")),
		"acting_company_id": str(world.get("controlled_company_id", "co-0")),
		"expected_version": int(world.get("version", 0)),
		"type": type,
		"payload": payload
	}
	var result: Dictionary = Economy.queue_command(world, command)
	if result.get("ok", false): dirty = true
	return result


func _next_command_serial() -> int:
	var number: int = 0
	var ids: Array = world.get("commands", []).map(func(c): return str(c.get("id", "")))
	while ids.has("player-%d-%d" % [int(world.get("planning_month", 1)), number]): number += 1
	return number


func _go(tab: int, path: Array) -> void:
	outer = tab
	paths[tab] = path
	full_map = false
	_render()


func _context_dialog() -> void:
	var box: VBoxContainer = _open_modal("조작 인물과 대리 법인")
	_row(box, "인물", str(_person().get("name", "")))
	_row(box, "개인 현금", _money(Economy.cash(world, str(world.get("player_id", "")))))
	_row(box, "대리 법인", str(_company().get("name", "")))
	_row(box, "법인 현금", _money(Economy.cash(world, str(_company().get("id", "")))))
	_label(box, "도시와 세계의 조회 범위를 바꿔도 인물의 자격과 계정 소유자는 변하지 않습니다. 이번 실험에서는 시작 법인의 대표를 조작합니다.", 14, MUTED)
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
	panel.add_theme_stylebox_override("panel", _style(PANEL, 18, Color("345061")))
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
	var this_toast: Label = toast_label
	get_tree().create_timer(3.5).timeout.connect(func():
		if is_instance_valid(this_toast): this_toast.queue_free()
	)


func _new_game_dialog() -> void:
	var box: VBoxContainer = _open_modal("새 실험 세계")
	_label(box, "세 대륙 · 도시의 실물 경제 · 월 단위 수동 진행", 17, MINT)
	_label(box, "도시·기업·인물 수와 가격은 검증용 데이터입니다. 같은 시드와 명령은 같은 결과를 만듭니다. 기존 저장을 지우지 않습니다.", 14, MUTED)
	var seed_input := LineEdit.new()
	seed_input.text = "42"
	seed_input.placeholder_text = "세계 시드 · 0~2147483647 정수"
	seed_input.custom_minimum_size.y = 48
	box.add_child(seed_input)
	_button(box, "세계 생성 및 초기 검사", func():
		var text_value: String = seed_input.text.strip_edges()
		if not text_value.is_valid_int() or text_value.to_int() < 0 or text_value.to_int() > 2147483647:
			_message("시드 확인", "0부터 2147483647까지의 정수를 입력하세요.")
			return
		var generated: Dictionary = Economy.new_world(text_value.to_int())
		var errors: Array = Economy.validate(generated)
		if not errors.is_empty():
			_message("초기 세계 검사 실패", "\n".join(errors))
			return
		world = generated
		city_id = str(_person().get("city_id", "city-0"))
		scope = city_id
		outer = 0
		paths = [[0,0,0], [0,0,0], [0,0,0], [0,0,0], [0,0,0]]
		screen = "game"
		dirty = true
		saved_hash = ""
		_render()
		var intro: VBoxContainer = _open_modal("첫 계획 월을 시작합니다")
		_label(intro, "초기 장부·재고·타일 검사를 통과했습니다. 예비 장기 균형을 보장하는 결과는 아닙니다.\n\n당신은 " + str(_person().get("name", "대표")) + ", " + str(_company().get("name", "회사")) + "의 대표입니다. 개인 돈과 법인 돈은 별도 계정입니다.", 15)
		_label(intro, "1. 도시의 시설과 공급망 살펴보기\n2. 사업에서 반복 생산·가격 정책 정하기\n3. 타일에 시설 건설 계획하기\n4. 다음 달을 눌러 결과와 원인 확인하기", 14, MINT)
		_button(intro, "계획 시작", func():
			_close_modal()
			_save("autosave", false)
		, true)
	, true)
	_button(box, "취소", _close_modal)


func _save(slot: String, show_message: bool) -> bool:
	if world.is_empty() or resolving: return false
	var result: Dictionary = Store.save_world(world, slot)
	if result.get("ok", false):
		_save_view(slot)
		if slot != "recovery":
			dirty = false
			saved_hash = Economy.state_hash(world)
		if show_message:
			_render()
			_toast("월과 미실행 계획을 저장했습니다.")
		return true
	if show_message: _message("저장하지 못했습니다", str(result.get("error", "알 수 없는 오류")))
	return false


func _load(slot: String) -> void:
	var result: Dictionary = Store.load_world(slot)
	if not result.get("ok", false):
		_message("불러올 수 없습니다", str(result.get("error", "정상 저장 없음")))
		return
	world = result["state"]
	city_id = str(_person().get("city_id", "city-0"))
	scope = city_id
	_restore_view(slot)
	screen = "game"
	dirty = false
	saved_hash = Economy.state_hash(world)
	_render()
	if result.get("recovered", false):
		_message("이전 정상 저장으로 복구했습니다", "최신 파일의 검사가 실패해 정상 백업을 불러왔습니다. 마지막 정상 월과 미실행 계획을 확인하세요.")
	elif world.get("report_pending", false): _show_report(world.get("last_report", {}))


func _load_dialog() -> void:
	var box: VBoxContainer = _open_modal("저장 불러오기")
	for slot in ["autosave", "manual", "recovery", "before_load", "before_load_b"]:
		var s: String = slot
		var result: Dictionary = Store.load_world(s)
		var name_value: String = {"autosave": "자동 저장", "manual": "수동 저장", "recovery": "중단 복구", "before_load": "불러오기 전 백업", "before_load_b": "불러오기 전 보조 백업"}[s]
		if result.get("ok", false):
			var previous: Dictionary = result["state"]
			var b: Button = _button(box, name_value + " · 계획 %d개월차" % int(previous.get("planning_month", 1)), func():
				if dirty:
					var confirm: VBoxContainer = _open_modal("현재 변경을 어떻게 할까요?")
					_label(confirm, "불러올 저장은 이미 검사를 통과했습니다. 현재의 미저장 계획은 별도로 보존할 수 있습니다.", 14, MUTED)
					_button(confirm, "현재 계획을 별도 백업 후 불러오기", func():
						# Never overwrite the selected slot while preparing to load it.
						var backup_slot: String = "before_load_b" if s == "before_load" else "before_load"
						if _save(backup_slot, false): _load(s)
					)
					_button(confirm, "저장 없이 불러오기", func(): _load(s))
					_button(confirm, "취소", _close_modal)
				else: _load(s)
			)
			b.add_theme_font_size_override("font_size", 15)
		else: _label(box, name_value + " · 정상 저장 없음", 14, MUTED)
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
		dirty = false
		_render()


func _back() -> void:
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
		file.store_string(JSON.stringify({"outer": outer, "paths": paths, "city_id": city_id, "scope": scope, "scroll_positions": scroll_positions}))
		file.close()


func _restore_view(slot: String) -> void:
	var path: String = "user://view-" + slot + ".json"
	if not FileAccess.file_exists(path): return
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not value is Dictionary: return
	outer = clampi(int(value.get("outer", 0)), 0, 4)
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
	if restored_scope == "world" or not _find(world.get("cities", []), restored_scope).is_empty(): scope = restored_scope
	if value.get("scroll_positions", {}) is Dictionary:
		scroll_positions = value.get("scroll_positions", {}).duplicate()
		last_screen_key = ""


func _reason(code: String) -> String:
	var descriptions: Dictionary = {
		"OPENING_EQUITY": "개시 현금과 자본",
		"UNKNOWN_CITY": "선택한 도시를 찾을 수 없습니다.",
		"REMOTE_CONSTRUCTION_NOT_SUPPORTED": "이 실험 빌드에서는 내 법인이 있는 도시에서 건설할 수 있습니다.",
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
		"NOT_PLANNING": "현재 정산 중입니다. 다음 계획 월에 입력하세요."
	}
	return str(descriptions.get(code, code))


func _status(value: String) -> String:
	return str({"operational": "가동", "ready": "준공 · 가동 준비", "active": "진행 중", "paused": "중단", "completed": "준공", "cancelled": "취소"}.get(value, value))


func _role(value: String) -> String:
	return str({"director": "대표", "operator": "운영 인력", "builder": "건설 인력"}.get(value, value))
