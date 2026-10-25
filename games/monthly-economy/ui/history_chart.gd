extends Control
## Only committed report observations are drawn. No interpolated or mock values.
var observations: Array = []
var value_key: String = "trades"
var selected: int = -1
const INK = Color("5fc5ff")

func _ready() -> void:
	custom_minimum_size.y = 145
	mouse_filter = Control.MOUSE_FILTER_STOP

func set_observations(values: Array, key: String = "trades") -> void:
	observations = values.duplicate(true)
	value_key = key
	queue_redraw()

func _draw() -> void:
	var font: Font = get_theme_default_font()
	var area := Rect2(36, 16, maxf(size.x - 48, 20), maxf(size.y - 51, 20))
	draw_line(area.position, area.position + Vector2(0, area.size.y), Color("38566d"), 1)
	draw_line(area.position + Vector2(0, area.size.y), area.end, Color("38566d"), 1)
	if observations.is_empty():
		draw_string(font, Vector2(45, 60), "첫 월을 정산하면 실제 기록을 표시합니다.", HORIZONTAL_ALIGNMENT_LEFT, maxf(size.x - 55, 20), 12, Color("9bb3c7"))
		return
	var maximum: float = 1.0
	for item in observations: maximum = maxf(maximum, float(item.get(value_key, 0)))
	var points := PackedVector2Array()
	for i in observations.size():
		var x: float = area.position.x + area.size.x * float(i) / maxf(observations.size() - 1, 1)
		var y: float = area.end.y - area.size.y * float(observations[i].get(value_key, 0)) / maximum
		points.append(Vector2(x, y))
	if points.size() > 1: draw_polyline(points, INK, 2.5, true)
	for i in points.size(): draw_circle(points[i], 4.0 if i == selected else 2.5, Color.WHITE if i == selected else INK)
	draw_string(font, Vector2(0, 22), str(int(maximum)), HORIZONTAL_ALIGNMENT_LEFT, 32, 11, Color("9bb3c7"))
	draw_string(font, Vector2(14, area.end.y + 4), "0", HORIZONTAL_ALIGNMENT_LEFT, 20, 11, Color("9bb3c7"))
	draw_string(font, Vector2(area.position.x, size.y - 8), "%s월" % observations[0].get("month", 0), HORIZONTAL_ALIGNMENT_LEFT, 100, 11, Color("9bb3c7"))
	var last: Dictionary = observations[-1]
	var caption: String = "%s월 · %s건" % [last.get("month", 0), last.get(value_key, 0)]
	if selected >= 0 and selected < observations.size(): caption = "%s월 · %s건" % [observations[selected].get("month", 0), observations[selected].get(value_key, 0)]
	draw_string(font, Vector2(maxf(size.x - 155, 36), size.y - 8), caption, HORIZONTAL_ALIGNMENT_RIGHT, 143, 11, Color("d8ecff"))

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and not observations.is_empty():
		selected = clampi(int(round((event.position.x - 36.0) / maxf(size.x - 48.0, 1) * (observations.size() - 1))), 0, observations.size() - 1)
		queue_redraw()
