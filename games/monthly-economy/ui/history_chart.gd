extends Control
## Only committed observations are drawn, with their actual measurement unit.
const Presentation = preload("res://ui/presentation.gd")
const INK: Color = Color("5fc5ff")
const MUTED: Color = Color("9bb3c7")
const MONEY_KEYS: Array[String] = ["cash", "revenue", "expenses", "profit", "value_added", "gdp", "real_gdp", "gross_output", "intermediate_consumption", "exports", "imports", "net_exports", "covered_fixed_price_value_added", "public_nonmarket_value_added", "digital_current_price_value_added"]

var observations: Array = []
var value_key: String = "trades"
var value_kind: String = "auto"
var product_id: String = ""
var world: Dictionary = {}
var currency: String = "USD"
var currency_view: String = "USD":
	set(value):
		currency_view = value
		queue_redraw()
var selected: int = -1
var _chart_area: Rect2 = Rect2(36, 16, 20, 20)

func _ready() -> void:
	custom_minimum_size.y = maxf(custom_minimum_size.y, 158.0)
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL
	tooltip_text = "확정된 월별 기록입니다. 그래프를 누르거나 좌우 키로 해당 월의 실제 값을 확인하세요."

func set_observations(values: Array, key: String = "trades") -> void:
	observations = values.duplicate(true)
	value_key = key
	selected = -1
	queue_redraw()

func display_value(value: Variant, compact: bool = true) -> String:
	if not value is int and not value is float: return "자료 없음"
	var kind: String = value_kind
	if kind == "auto": kind = "money" if value_key in MONEY_KEYS else ("quantity" if not product_id.is_empty() else "count")
	match kind:
		"money": return Presentation.money(int(value), currency, compact, currency_view)
		"quantity": return Presentation.quantity(world, product_id, int(value))
		"percent": return "%.1f%%" % float(value)
		"people": return Presentation.grouped(int(value)) + "명"
		_: return Presentation.grouped(int(value)) + "건"

func _value(index: int) -> Variant:
	if index < 0 or index >= observations.size() or not observations[index] is Dictionary: return null
	var value: Variant = observations[index].get(value_key)
	return value if value is int or value is float else null

func _draw() -> void:
	var font: Font = get_theme_default_font()
	var minimum: float = 0.0
	var maximum: float = 0.0
	var count: int = 0
	for index: int in observations.size():
		var value: Variant = _value(index)
		if value == null: continue
		minimum = minf(minimum, float(value))
		maximum = maxf(maximum, float(value))
		count += 1
	if count == 0:
		draw_string(font, Vector2(12, 60), "첫 월을 정산하면 실제 기록을 표시합니다." if observations.is_empty() else "이 지표의 확정 기록이 없습니다.", HORIZONTAL_ALIGNMENT_LEFT, maxf(size.x - 24, 20), 12, MUTED)
		return
	var top_label: String = display_value(maximum)
	var bottom_label: String = display_value(minimum)
	var axis_width: float = clampf(maxf(font.get_string_size(top_label, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x, font.get_string_size(bottom_label, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x) + 8.0, 36.0, minf(118.0, size.x * 0.35))
	_chart_area = Rect2(axis_width, 17, maxf(size.x - axis_width - 12, 20), maxf(size.y - 65, 20))
	var area: Rect2 = _chart_area
	var span: float = maxf(maximum - minimum, 1.0)
	var zero_y: float = area.end.y - area.size.y * (0.0 - minimum) / span
	draw_line(area.position, area.position + Vector2(0, area.size.y), Color("38566d"), 1)
	draw_line(Vector2(area.position.x, zero_y), Vector2(area.end.x, zero_y), Color("38566d"), 1)
	var previous: Vector2 = Vector2.INF
	for index: int in observations.size():
		var value: Variant = _value(index)
		if value == null:
			previous = Vector2.INF
			continue
		var x: float = area.position.x + area.size.x * float(index) / maxf(observations.size() - 1, 1)
		var y: float = area.end.y - area.size.y * (float(value) - minimum) / span
		var point: Vector2 = Vector2(x, y)
		if previous.is_finite(): draw_line(previous, point, INK, 2.5, true)
		draw_circle(point, 4.0 if index == selected else 2.5, Color.WHITE if index == selected else INK)
		previous = point
	draw_string(font, Vector2(0, 22), top_label, HORIZONTAL_ALIGNMENT_RIGHT, axis_width - 7, 10, MUTED)
	draw_string(font, Vector2(0, area.end.y + 3), bottom_label, HORIZONTAL_ALIGNMENT_RIGHT, axis_width - 7, 10, MUTED)
	var first: Dictionary = observations[0] if observations[0] is Dictionary else {}
	draw_string(font, Vector2(area.position.x, size.y - 30), "%s월" % first.get("month", "—"), HORIZONTAL_ALIGNMENT_LEFT, area.size.x, 11, MUTED)
	var index: int = selected if selected >= 0 and selected < observations.size() else observations.size() - 1
	var item: Dictionary = observations[index] if observations[index] is Dictionary else {}
	var caption: String = "%s월 · %s" % [item.get("month", "—"), display_value(_value(index))]
	draw_string(font, Vector2(0, size.y - 8), caption, HORIZONTAL_ALIGNMENT_RIGHT, maxf(size.x - 12, 20), 12, Color("d8ecff"))

func _gui_input(event: InputEvent) -> void:
	if observations.is_empty(): return
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		grab_focus()
		selected = clampi(int(round((event.position.x - _chart_area.position.x) / maxf(_chart_area.size.x, 1) * (observations.size() - 1))), 0, observations.size() - 1)
		queue_redraw()
		accept_event()
	elif event is InputEventKey and event.pressed and event.keycode in [KEY_LEFT, KEY_RIGHT]:
		selected = clampi((observations.size() - 1 if selected < 0 else selected) + (-1 if event.keycode == KEY_LEFT else 1), 0, observations.size() - 1)
		queue_redraw()
		accept_event()
