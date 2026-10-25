extends Control
## Read-only, procedurally drawn city view. Drawing never consumes simulation RNG.

signal tile_selected(x: int, y: int)
signal facility_selected(id: String)

var world: Dictionary = {}
var city_id: String = "city-0"
var build_mode: bool = false:
	set(value):
		build_mode = value
		queue_redraw()
var selected_tile: Vector2i = Vector2i(-1, -1):
	set(value):
		selected_tile = value
		queue_redraw()
var selected_kind: String = "farm"
var preview_size: Vector2i = Vector2i(2, 2):
	set(value):
		preview_size = value
		queue_redraw()

var _zoom: float = 1.0
var _pan: Vector2 = Vector2.ZERO
var _pointer_down: bool = false
var _pointer_start: Vector2 = Vector2.ZERO
var _last_pointer: Vector2 = Vector2.ZERO
var _moved: bool = false
var _hover: Vector2i = Vector2i(-1, -1)
var _touch_active: bool = false

const BACKGROUND: Color = Color("101f29")
const SOIL_A: Color = Color("17332f")
const SOIL_B: Color = Color("193731")
const GRID: Color = Color("295047")
const ACCENT: Color = Color("75e6cc")
const MUTED: Color = Color("7caaa5")
const DANGER: Color = Color("ec8c83")
const MARGIN: float = 20.0

func _ready() -> void:
	custom_minimum_size = Vector2(maxf(custom_minimum_size.x, 280.0), maxf(custom_minimum_size.y, 240.0))
	clip_contents = true
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL
	tooltip_text = "타일을 선택하거나 지도를 드래그하세요. 휠과 두 손가락으로 확대할 수 있습니다."
	resized.connect(queue_redraw)
	mouse_exited.connect(_clear_hover)

func update_world(state: Dictionary, city: String) -> void:
	var changed_city: bool = city_id != city
	world = state
	city_id = city
	if changed_city:
		center_view()
	queue_redraw()

func set_zoom(factor: float) -> void:
	_zoom = clampf(factor, 0.75, 3.0)
	_limit_pan()
	queue_redraw()

func center_view() -> void:
	_zoom = 1.0
	_pan = Vector2.ZERO
	queue_redraw()

func _city() -> Dictionary:
	for raw_city: Variant in world.get("cities", []):
		if raw_city is Dictionary and str(raw_city.get("id", "")) == city_id:
			return raw_city
	return {}

func _dimensions() -> Vector2i:
	var city: Dictionary = _city()
	return Vector2i(maxi(1, int(city.get("width", 12))), maxi(1, int(city.get("height", 10))))

func _cell_size() -> float:
	var dimensions: Vector2i = _dimensions()
	var available: Vector2 = Vector2(maxf(1.0, size.x - MARGIN * 2.0), maxf(1.0, size.y - MARGIN * 2.0 - 20.0))
	return minf(available.x / float(dimensions.x), available.y / float(dimensions.y)) * _zoom

func _origin() -> Vector2:
	var dimensions: Vector2i = _dimensions()
	var cells: float = _cell_size()
	return (size - Vector2(dimensions) * cells) * 0.5 + _pan + Vector2(0.0, 6.0)

func _tile_at(local_position: Vector2) -> Vector2i:
	var relative: Vector2 = (local_position - _origin()) / _cell_size()
	var tile: Vector2i = Vector2i(floori(relative.x), floori(relative.y))
	var dimensions: Vector2i = _dimensions()
	if tile.x < 0 or tile.y < 0 or tile.x >= dimensions.x or tile.y >= dimensions.y:
		return Vector2i(-1, -1)
	return tile

func _tile_rect(tile: Vector2i, footprint: Vector2i = Vector2i.ONE) -> Rect2:
	var cells: float = _cell_size()
	return Rect2(_origin() + Vector2(tile) * cells, Vector2(footprint) * cells)

func _limit_pan() -> void:
	var dimensions: Vector2i = _dimensions()
	var content: Vector2 = Vector2(dimensions) * _cell_size()
	var max_offset: Vector2 = Vector2(maxf(0.0, (content.x - size.x) * 0.5 + MARGIN), maxf(0.0, (content.y - size.y) * 0.5 + MARGIN))
	_pan = Vector2(clampf(_pan.x, -max_offset.x, max_offset.x), clampf(_pan.y, -max_offset.y, max_offset.y))

func _clear_hover() -> void:
	_hover = Vector2i(-1, -1)
	queue_redraw()

func _begin_pointer(position: Vector2) -> void:
	grab_focus()
	_pointer_down = true
	_pointer_start = position
	_last_pointer = position
	_moved = false

func _move_pointer(position: Vector2) -> void:
	if not _pointer_down:
		return
	if position.distance_to(_pointer_start) > 7.0:
		_moved = true
	if _moved:
		_pan += position - _last_pointer
		_limit_pan()
	_last_pointer = position
	queue_redraw()

func _end_pointer(position: Vector2) -> void:
	if _pointer_down and not _moved:
		_activate(_tile_at(position))
	_pointer_down = false

func _activate(tile: Vector2i) -> void:
	if tile.x < 0:
		return
	selected_tile = tile
	tile_selected.emit(tile.x, tile.y)
	if not build_mode:
		for raw_facility: Variant in world.get("facilities", []):
			if raw_facility is Dictionary and str(raw_facility.get("city_id", "")) == city_id and _contains(raw_facility, tile):
				facility_selected.emit(str(raw_facility.get("id", "")))
				return
		for raw_project: Variant in world.get("projects", []):
			if raw_project is Dictionary and str(raw_project.get("city_id", "")) == city_id and str(raw_project.get("status", "")) != "cancelled" and _contains(raw_project, tile):
				facility_selected.emit(str(raw_project.get("id", "")))
				return

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and not _touch_active:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP and event.pressed:
			set_zoom(_zoom * 1.15)
			accept_event()
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN and event.pressed:
			set_zoom(_zoom / 1.15)
			accept_event()
		elif event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_begin_pointer(event.position)
			else:
				_end_pointer(event.position)
			accept_event()
	elif event is InputEventMouseMotion and not _touch_active:
		_hover = _tile_at(event.position)
		_move_pointer(event.position)
		queue_redraw()
		if _pointer_down:
			accept_event()
	elif event is InputEventScreenTouch:
		if event.index == 0:
			_touch_active = event.pressed
			if event.pressed:
				_begin_pointer(event.position)
			else:
				_end_pointer(event.position)
			accept_event()
		elif event.pressed:
			_moved = true
	elif event is InputEventScreenDrag and event.index == 0:
		_move_pointer(event.position)
		accept_event()
	elif event is InputEventMagnifyGesture:
		_moved = true
		set_zoom(_zoom * event.factor)
		accept_event()
	elif event is InputEventPanGesture:
		_pan -= event.delta * 12.0
		_limit_pan()
		queue_redraw()
		accept_event()
	elif event is InputEventKey and event.pressed:
		if event.keycode in [KEY_PLUS, KEY_EQUAL, KEY_KP_ADD]:
			set_zoom(_zoom * 1.15)
			accept_event()
		elif event.keycode in [KEY_MINUS, KEY_KP_SUBTRACT]:
			set_zoom(_zoom / 1.15)
			accept_event()
		elif event.keycode == KEY_HOME:
			center_view()
			accept_event()

func _contains(item: Dictionary, tile: Vector2i) -> bool:
	var footprint: Rect2i = Rect2i(int(item.get("x", -100)), int(item.get("y", -100)), int(item.get("w", 1)), int(item.get("h", 1)))
	return footprint.has_point(tile)

func _roads() -> Array:
	var raw_roads: Variant = _city().get("roads", [])
	return raw_roads if raw_roads is Array else []

func _road_at(tile: Vector2i) -> bool:
	for raw_road: Variant in _roads():
		if raw_road is Dictionary and int(raw_road.get("x", -1)) == tile.x and int(raw_road.get("y", -1)) == tile.y:
			return true
		if raw_road is Array and raw_road.size() >= 2 and int(raw_road[0]) == tile.x and int(raw_road[1]) == tile.y:
			return true
	return false

func _occupied(tile: Vector2i) -> bool:
	if _road_at(tile):
		return true
	for list_name: String in ["facilities", "projects"]:
		for raw_item: Variant in world.get(list_name, []):
			if raw_item is Dictionary and str(raw_item.get("city_id", "")) == city_id and str(raw_item.get("status", "")) not in ["cancelled", "demolished"] and _contains(raw_item, tile):
				return true
	return false

func _preview_valid(tile: Vector2i) -> bool:
	var dimensions: Vector2i = _dimensions()
	if tile.x < 0 or tile.y < 0 or tile.x + preview_size.x > dimensions.x or tile.y + preview_size.y > dimensions.y:
		return false
	for x: int in range(tile.x, tile.x + preview_size.x):
		for y: int in range(tile.y, tile.y + preview_size.y):
			if _occupied(Vector2i(x, y)):
				return false
	return true

func _draw() -> void:
	var panel: StyleBoxFlat = StyleBoxFlat.new()
	panel.bg_color = BACKGROUND
	panel.border_color = Color("2a454e")
	panel.set_border_width_all(1)
	panel.set_corner_radius_all(14)
	draw_style_box(panel, Rect2(Vector2.ZERO, size))
	var dimensions: Vector2i = _dimensions()
	var cells: float = _cell_size()
	var map_rect: Rect2 = _tile_rect(Vector2i.ZERO, dimensions)
	draw_rect(map_rect.grow(5.0), Color("0b1921"))
	for y: int in range(dimensions.y):
		for x: int in range(dimensions.x):
			var tile: Vector2i = Vector2i(x, y)
			var rect: Rect2 = _tile_rect(tile)
			draw_rect(rect, SOIL_A if (x + y) % 2 == 0 else SOIL_B)
			draw_rect(rect, GRID, false, 0.6)
			if _road_at(tile):
				_draw_road(tile, rect)
			elif not _occupied(tile):
				_draw_land(tile, rect)
	for raw_facility: Variant in world.get("facilities", []):
		if raw_facility is Dictionary and str(raw_facility.get("city_id", "")) == city_id:
			_draw_facility(raw_facility)
	for raw_project: Variant in world.get("projects", []):
		if raw_project is Dictionary and str(raw_project.get("city_id", "")) == city_id and str(raw_project.get("status", "")) not in ["cancelled", "completed", "operating"]:
			_draw_project(raw_project)
	if _hover.x >= 0 and not build_mode and _hover != selected_tile:
		draw_rect(_tile_rect(_hover).grow(-1.0), Color(0.5, 0.9, 0.82, 0.08))
		draw_rect(_tile_rect(_hover).grow(-1.0), Color(0.5, 0.9, 0.82, 0.5), false, 1.0)
	if selected_tile.x >= 0:
		var selection: Rect2 = _tile_rect(selected_tile, preview_size if build_mode else Vector2i.ONE).grow(-1.5)
		var selection_color: Color = ACCENT
		if build_mode and not _preview_valid(selected_tile):
			selection_color = DANGER
		draw_rect(selection, Color(selection_color, 0.16))
		draw_rect(selection, selection_color, false, 2.0)
		_draw_corners(selection, selection_color)
	_draw_coordinates(dimensions, cells)
	var font: Font = get_theme_default_font()
	var hint: String = "부지 선택 중" if build_mode else "도시 지도"
	draw_string(font, Vector2(14.0, 20.0), hint, HORIZONTAL_ALIGNMENT_LEFT, -1.0, 11, MUTED)
	var zoom_text: String = "%d%%" % int(round(_zoom * 100.0))
	draw_string(font, Vector2(size.x - 51.0, 20.0), zoom_text, HORIZONTAL_ALIGNMENT_LEFT, 40.0, 11, MUTED)

func _draw_coordinates(dimensions: Vector2i, cells: float) -> void:
	var font: Font = get_theme_default_font()
	var origin: Vector2 = _origin()
	if cells < 13.0:
		return
	for x: int in range(dimensions.x):
		draw_string(font, origin + Vector2((float(x) + 0.5) * cells - 3.0, -6.0), str(x + 1), HORIZONTAL_ALIGNMENT_LEFT, cells, 9, Color("53736f"))
	for y: int in range(dimensions.y):
		draw_string(font, origin + Vector2(-13.0, (float(y) + 0.5) * cells + 3.0), str(y + 1), HORIZONTAL_ALIGNMENT_LEFT, 12.0, 9, Color("53736f"))

func _draw_land(tile: Vector2i, rect: Rect2) -> void:
	var value: int = posmod(tile.x * 73856093 ^ tile.y * 19349663, 100)
	var cells: float = rect.size.x
	if value < 15:
		var center: Vector2 = rect.get_center() + Vector2(cells * 0.09, -cells * 0.03)
		draw_line(center + Vector2(0.0, cells * 0.02), center + Vector2(0.0, cells * 0.2), Color("244b3c"), maxf(1.0, cells * 0.05))
		draw_circle(center + Vector2(cells * 0.04, cells * 0.04), cells * 0.17, Color("102b24"))
		draw_circle(center, cells * 0.16, Color("2c5c42"))
		draw_circle(center - Vector2(cells * 0.04, cells * 0.04), cells * 0.1, Color("397051"))
	elif value < 40:
		var center: Vector2 = rect.get_center()
		draw_line(center - Vector2(cells * 0.09, 0.0), center - Vector2(cells * 0.04, cells * 0.07), Color("34604a"), 1.0)
		draw_line(center + Vector2(cells * 0.04, 0.0), center + Vector2(cells * 0.06, -cells * 0.08), Color("34604a"), 1.0)

func _draw_road(tile: Vector2i, rect: Rect2) -> void:
	draw_rect(rect, Color("263c43"))
	var center: Vector2 = rect.get_center()
	var quarter: float = rect.size.x * 0.25
	var road_left: bool = _road_at(tile + Vector2i.LEFT)
	var road_right: bool = _road_at(tile + Vector2i.RIGHT)
	var road_up: bool = _road_at(tile + Vector2i.UP)
	var road_down: bool = _road_at(tile + Vector2i.DOWN)
	if road_left or road_right:
		draw_line(center - Vector2(quarter, 0.0), center + Vector2(quarter, 0.0), Color("668278"), maxf(1.0, rect.size.x * 0.035))
	if road_up or road_down:
		draw_line(center - Vector2(0.0, quarter), center + Vector2(0.0, quarter), Color("668278"), maxf(1.0, rect.size.x * 0.035))

func _draw_facility(item: Dictionary) -> void:
	var tile: Vector2i = Vector2i(int(item.get("x", 0)), int(item.get("y", 0)))
	var footprint: Vector2i = Vector2i(int(item.get("w", 1)), int(item.get("h", 1)))
	var rect: Rect2 = _tile_rect(tile, footprint).grow(-3.0)
	var kind: String = str(item.get("kind", "factory"))
	var status: String = str(item.get("status", "operating"))
	var primary: Color = Color("91a8a9")
	var edge: Color = Color("527176")
	if kind in ["farm", "agriculture", "ranch"]:
		primary = Color("a9bb72")
		edge = Color("566d3d")
	elif kind in ["mill", "bakery", "food", "food_factory"]:
		primary = Color("dab379")
		edge = Color("9a7347")
	elif kind in ["warehouse", "retail", "store", "builders"]:
		primary = Color("7fadc0")
		edge = Color("3c6f83")
	elif kind in ["energy", "power", "power_plant", "solar"]:
		primary = Color("8cb8b6")
		edge = Color("4a7e83")
	elif kind in ["wood", "lumber", "forestry", "sawmill"]:
		primary = Color("ba9670")
		edge = Color("795e42")
	elif kind in ["mine", "quarry", "stone"]:
		primary = Color("a7a7b1")
		edge = Color("6a697a")
	elif kind in ["house", "residential", "office"]:
		primary = Color("8bbfac")
		edge = Color("477f72")
	if status in ["construction", "building"]:
		_draw_project(item)
		return
	var shadow: Rect2 = Rect2(rect.position + Vector2(2.0, 3.0), rect.size)
	draw_rect(shadow, Color(0.02, 0.06, 0.07, 0.6))
	draw_rect(rect, edge)
	var body: Rect2 = Rect2(rect.position, Vector2(rect.size.x, maxf(1.0, rect.size.y - 4.0)))
	draw_rect(body, primary)
	draw_rect(body.grow(-2.0), primary.lightened(0.07), false, 1.0)
	if kind in ["farm", "agriculture", "ranch"]:
		_draw_farm(body)
	elif kind in ["energy", "power", "power_plant", "solar"]:
		_draw_power(body)
	elif kind in ["wood", "lumber", "forestry", "sawmill"]:
		_draw_lumber(body)
	elif kind in ["mine", "quarry", "stone"]:
		_draw_quarry(body)
	elif kind in ["house", "residential", "office"]:
		_draw_home(body, edge)
	else:
		_draw_factory(body, kind, edge)
	if status in ["ready", "ready_next_month", "completed", "idle", "closed", "paused"]:
		draw_rect(rect, Color(0.04, 0.1, 0.14, 0.38))
		draw_circle(rect.end - Vector2(5.0, 6.0), 3.0, Color("edc37c"))
	elif status in ["operating", "operational", "active"]:
		draw_circle(rect.end - Vector2(5.0, 6.0), 2.5, Color("85e6b4"))

func _draw_farm(rect: Rect2) -> void:
	var inner: Rect2 = rect.grow(-4.0)
	draw_rect(inner, Color("6f8144"))
	for i: int in range(5):
		var y: float = inner.position.y + (float(i) + 0.5) * inner.size.y / 5.0
		draw_line(Vector2(inner.position.x + 2.0, y), Vector2(inner.end.x - 2.0, y), Color("bdcc77"), maxf(1.0, inner.size.y * 0.06))
	var roof: Rect2 = Rect2(rect.position + Vector2(3.0, 3.0), Vector2(rect.size.x * 0.32, rect.size.y * 0.3))
	draw_rect(Rect2(roof.position + Vector2(1.0, 1.5), roof.size), Color("51603a"))
	draw_rect(roof, Color("d4d6b1"))
	draw_line(roof.position + Vector2(roof.size.x * 0.5, 0.0), roof.position + Vector2(roof.size.x * 0.5, roof.size.y), Color("98aa87"), 1.0)

func _draw_power(rect: Rect2) -> void:
	var inner: Rect2 = rect.grow(-4.0)
	for y: int in range(2):
		for x: int in range(2):
			var cell: Rect2 = Rect2(inner.position + Vector2(float(x) * inner.size.x * 0.5, float(y) * inner.size.y * 0.5), inner.size * 0.5 - Vector2(2.0, 2.0))
			draw_rect(cell, Color("315e79"))
			draw_line(cell.position + Vector2(cell.size.x * 0.5, 0.0), cell.end - Vector2(cell.size.x * 0.5, 0.0), Color("73a0b2"), 1.0)
			draw_line(cell.position + Vector2(0.0, cell.size.y * 0.5), cell.end - Vector2(0.0, cell.size.y * 0.5), Color("73a0b2"), 1.0)

func _draw_lumber(rect: Rect2) -> void:
	var inner: Rect2 = rect.grow(-5.0)
	for i: int in range(4):
		var y: float = inner.position.y + (float(i) + 0.5) * inner.size.y * 0.25
		draw_line(Vector2(inner.position.x, y), Vector2(inner.end.x, y), Color("e0c490"), maxf(2.0, inner.size.y * 0.15))
		draw_circle(Vector2(inner.end.x - 1.0, y), maxf(1.0, inner.size.y * 0.075), Color("aa774b"))

func _draw_quarry(rect: Rect2) -> void:
	var center: Vector2 = rect.get_center()
	var radius: float = minf(rect.size.x, rect.size.y) * 0.34
	for i: int in range(3):
		var offset: Vector2 = Vector2(float(i - 1) * radius * 0.7, float(i % 2) * radius * 0.6)
		var vertices: PackedVector2Array = PackedVector2Array([center + offset + Vector2(-radius * 0.65, radius * 0.25), center + offset + Vector2(-radius * 0.2, -radius * 0.55), center + offset + Vector2(radius * 0.5, -radius * 0.2), center + offset + Vector2(radius * 0.7, radius * 0.45)])
		draw_colored_polygon(vertices, Color("d0d0d4") if i % 2 == 0 else Color("898d9a"))

func _draw_home(rect: Rect2, edge: Color) -> void:
	var roof: Rect2 = Rect2(rect.position + Vector2(3.0, 3.0), Vector2(rect.size.x - 6.0, rect.size.y * 0.37))
	draw_rect(roof, edge)
	for i: int in range(3):
		var window: Rect2 = Rect2(rect.position + Vector2(rect.size.x * (0.18 + float(i) * 0.25), rect.size.y * 0.62), Vector2(rect.size.x * 0.12, rect.size.y * 0.16))
		draw_rect(window, Color("e0dbae"))

func _draw_factory(rect: Rect2, kind: String, edge: Color) -> void:
	var roof: Rect2 = rect.grow(-4.0)
	draw_rect(roof, edge.lightened(0.1))
	for i: int in range(3):
		var y: float = roof.position.y + (float(i) + 0.5) * roof.size.y / 3.0
		draw_line(Vector2(roof.position.x + 2.0, y), Vector2(roof.end.x - 2.0, y), Color(0.83, 0.9, 0.88, 0.25), 1.0)
	if kind in ["warehouse", "retail", "store", "builders"]:
		var door: Rect2 = Rect2(rect.position + Vector2(rect.size.x * 0.3, rect.size.y * 0.72), Vector2(rect.size.x * 0.4, rect.size.y * 0.25))
		draw_rect(door, Color("325463"))
	else:
		var stack: Rect2 = Rect2(rect.position + Vector2(rect.size.x * 0.7, -2.0), Vector2(maxf(3.0, rect.size.x * 0.12), rect.size.y * 0.33))
		draw_rect(Rect2(stack.position + Vector2(1.0, 1.0), stack.size), Color("34494b"))
		draw_rect(stack, Color("e0c6a0"))
		draw_rect(Rect2(stack.position, Vector2(stack.size.x, 2.0)), Color("596c70"))

func _draw_project(item: Dictionary) -> void:
	var tile: Vector2i = Vector2i(int(item.get("x", 0)), int(item.get("y", 0)))
	var footprint: Vector2i = Vector2i(int(item.get("w", 2)), int(item.get("h", 2)))
	var rect: Rect2 = _tile_rect(tile, footprint).grow(-3.0)
	draw_rect(rect, Color("3d4338"))
	draw_rect(rect, Color("baad76"), false, 1.3)
	var spacing: float = maxf(5.0, _cell_size() * 0.22)
	var cursor: float = rect.position.x - rect.size.y
	while cursor < rect.end.x:
		var a: Vector2 = Vector2(maxf(rect.position.x, cursor), rect.end.y - maxf(0.0, rect.position.x - cursor))
		var b: Vector2 = Vector2(minf(rect.end.x, cursor + rect.size.y), rect.position.y + maxf(0.0, cursor + rect.size.y - rect.end.x))
		draw_line(a, b, Color("5e6046"), 2.0)
		cursor += spacing
	var tower: Vector2 = rect.position + rect.size * Vector2(0.3, 0.65)
	var arm_end: Vector2 = tower + Vector2(rect.size.x * 0.55, -rect.size.y * 0.42)
	draw_line(tower + Vector2(0.0, rect.size.y * 0.13), tower - Vector2(0.0, rect.size.y * 0.42), Color("edc784"), 2.0)
	draw_line(tower - Vector2(rect.size.x * 0.15, rect.size.y * 0.42), arm_end, Color("edc784"), 2.0)
	draw_line(arm_end, arm_end + Vector2(0.0, rect.size.y * 0.25), Color("a5a984"), 1.0)
	var progress: float = float(item.get("progress", 0))
	var required: float = maxf(1.0, float(item.get("required_work", 100)))
	var ratio: float = clampf(progress / required, 0.0, 1.0)
	var bar: Rect2 = Rect2(rect.position + Vector2(3.0, rect.size.y - 7.0), Vector2(maxf(1.0, rect.size.x - 6.0), 3.0))
	draw_rect(bar, Color("1d302d"))
	draw_rect(Rect2(bar.position, Vector2(bar.size.x * ratio, bar.size.y)), Color("e6c58a"))

func _draw_corners(rect: Rect2, color: Color) -> void:
	var length: float = minf(7.0, minf(rect.size.x, rect.size.y) * 0.25)
	for corner: Vector2 in [rect.position, Vector2(rect.end.x, rect.position.y), rect.end, Vector2(rect.position.x, rect.end.y)]:
		var direction: Vector2 = (rect.get_center() - corner).sign()
		draw_line(corner, corner + Vector2(direction.x * length, 0.0), color.lightened(0.15), 3.0)
		draw_line(corner, corner + Vector2(0.0, direction.y * length), color.lightened(0.15), 3.0)
