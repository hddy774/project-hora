extends Control
## Read-only isometric city renderer. Economic state and simulation RNG are never mutated.
## Generated atlases are referenced through assets/map/atlas.json, never baked into the UI.

signal tile_selected(x: int, y: int)
signal facility_selected(id: String)

const ART_MANIFEST: String = "res://assets/map/atlas.json"
const BACKGROUND: Color = Color("0a1c2b")
const ACCENT: Color = Color("37c8ff")
const MUTED: Color = Color("a2c4d2")
const DANGER: Color = Color("ef9084")
const MARGIN: float = 16.0
const ROOF_CLEARANCE: float = 45.0

var world: Dictionary = {}
var city_id: String = "city-0"
var show_header: bool = false:
	set(value):
		show_header = value
		queue_redraw()
var build_mode: bool = false:
	set(value):
		build_mode = value
		queue_redraw()
var selected_tile: Vector2i = Vector2i(-1, -1):
	set(value):
		selected_tile = value
		queue_redraw()
var selected_kind: String = "farm":
	set(value):
		selected_kind = value
		queue_redraw()
var preview_size: Vector2i = Vector2i(2, 2):
	set(value):
		preview_size = Vector2i(maxi(1, value.x), maxi(1, value.y))
		queue_redraw()

var _zoom: float = 1.0
var _pan: Vector2 = Vector2.ZERO
var _focus_target: Vector2 = Vector2.INF
var _pointer_down: bool = false
var _pointer_start: Vector2 = Vector2.ZERO
var _last_pointer: Vector2 = Vector2.ZERO
var _moved: bool = false
var _hover: Vector2i = Vector2i(-1, -1)
var _touch_points: Dictionary = {}
var _pinch_distance: float = 0.0
var _pinch_midpoint: Vector2 = Vector2.ZERO
var _art: Dictionary = {}
var _atlas: Texture2D
var _ground: Texture2D
var _callout: Texture2D
var _sprite_cache: Dictionary = {}

func _ready() -> void:
	custom_minimum_size = Vector2(maxf(custom_minimum_size.x, 240.0), maxf(custom_minimum_size.y, 240.0))
	clip_contents = true
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS
	mouse_filter = Control.MOUSE_FILTER_STOP
	focus_mode = Control.FOCUS_ALL
	tooltip_text = "시설을 선택하거나 지도를 끌어 이동하세요. 휠·두 손가락 또는 확대 버튼으로 타일을 크게 볼 수 있습니다."
	_load_art()
	resized.connect(_on_resized)
	mouse_exited.connect(_clear_hover)

func _load_art() -> void:
	if not FileAccess.file_exists(ART_MANIFEST):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(ART_MANIFEST))
	if parsed is Dictionary:
		configure_art(parsed)

func configure_art(manifest: Dictionary) -> void:
	_art = manifest.duplicate(true)
	_sprite_cache.clear()
	_atlas = _load_texture(str(_art.get("atlas", "")))
	_ground = _load_texture(str(_art.get("ground", "")))
	_callout = _load_texture(str(_art.get("callout", "")))
	if _callout == null: _callout = _generated_callout()
	queue_redraw()

func _load_texture(path: String) -> Texture2D:
	if path.is_empty() or not ResourceLoader.exists(path):
		return null
	var resource: Resource = load(path)
	return resource as Texture2D

func _generated_callout() -> Texture2D:
	var path: String = "res://assets/generated/ui_assets.json"
	if not FileAccess.file_exists(path): return null
	var value: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if not value is Dictionary: return null
	var record: Variant = value.get("textures", {}).get("panel", {})
	if not record is Dictionary: return null
	var texture: Texture2D = _load_texture(str(record.get("path", "")))
	var region: Variant = record.get("rect", [])
	if texture == null or not region is Array or region.size() != 4: return null
	var cut := AtlasTexture.new()
	cut.atlas = texture
	cut.region = Rect2(float(region[0]), float(region[1]), float(region[2]), float(region[3]))
	cut.filter_clip = true
	return cut

func art_status() -> Dictionary:
	return {"generated_atlas_loaded": _atlas != null, "ground_loaded": _ground != null, "callout_loaded": _callout != null, "source": _art.get("provenance", "")}

func update_world(state: Dictionary, city: String) -> void:
	var changed_city: bool = city_id != city
	world = state
	city_id = city
	if changed_city:
		center_view()
	queue_redraw()

func set_zoom(factor: float) -> void:
	_focus_target = Vector2.INF
	_zoom = clampf(factor, 0.7, 3.6)
	_limit_pan()
	queue_redraw()

func zoom_in() -> void:
	zoom_at(_zoom * 1.2, size * 0.5)

func zoom_out() -> void:
	zoom_at(_zoom / 1.2, size * 0.5)

func get_zoom() -> float:
	return _zoom

func zoom_at(factor: float, focal_point: Vector2) -> void:
	_focus_target = Vector2.INF
	var grid_point: Vector2 = _grid_at(focal_point)
	_zoom = clampf(factor, 0.7, 3.6)
	_pan += focal_point - _point(grid_point)
	_limit_pan()
	queue_redraw()

func focus_company(company_id: String, zoom_level: float = 1.8) -> void:
	for facility: Variant in world.get("facilities", []):
		if not facility is Dictionary or str(facility.get("city_id", "")) != city_id or str(facility.get("company_id", "")) != company_id: continue
		var target: Vector2 = Vector2(float(facility.get("x", 0)) + float(facility.get("w", 2)) * 0.5, float(facility.get("y", 0)) + float(facility.get("h", 2)) * 0.5)
		# Frame the real nearby business district, including the represented site.
		# A border facility alone would place half the viewport outside the city.
		var low: Vector2 = target
		var high: Vector2 = target
		for nearby: Variant in world.get("facilities", []):
			if not nearby is Dictionary or str(nearby.get("city_id", "")) != city_id: continue
			var center: Vector2 = Vector2(float(nearby.get("x", 0)) + float(nearby.get("w", 2)) * 0.5, float(nearby.get("y", 0)) + float(nearby.get("h", 2)) * 0.5)
			if center.distance_to(target) <= 12.0:
				low = low.min(center)
				high = high.max(center)
		focus_tile((low + high) * 0.5, zoom_level * 1.35)
		return
	center_view()

func focus_tile(tile: Vector2, zoom_level: float = 1.8) -> void:
	_focus_target = tile
	_zoom = clampf(zoom_level, 0.7, 3.6)
	_pan = Vector2.ZERO
	_pan = size * 0.5 - _point(tile)
	_limit_pan()
	queue_redraw()

func _on_resized() -> void:
	# Containers settle size after add_child; retain the requested real target.
	if _focus_target.is_finite():
		_pan = Vector2.ZERO
		_pan = size * 0.5 - _point(_focus_target)
		_limit_pan()
	queue_redraw()

func center_view() -> void:
	_focus_target = Vector2.INF
	_zoom = 1.0
	_pan = Vector2.ZERO
	queue_redraw()

func _city() -> Dictionary:
	for item: Variant in world.get("cities", []):
		if item is Dictionary and str(item.get("id", "")) == city_id:
			return item
	return {}

func _dimensions() -> Vector2i:
	var city: Dictionary = _city()
	return Vector2i(maxi(1, int(city.get("width", 12))), maxi(1, int(city.get("height", 12))))

func _projection_ratio() -> float:
	return clampf(float(_art.get("projection_ratio", 0.5)), 0.45, 0.8)

func _half_width() -> float:
	var dimensions: Vector2i = _dimensions()
	var edge_count: float = float(dimensions.x + dimensions.y)
	var available: Vector2 = Vector2(maxf(16.0, size.x - MARGIN * 2.0), maxf(16.0, size.y - MARGIN * 2.0 - ROOF_CLEARANCE))
	return maxf(1.0, minf(available.x / edge_count, available.y / (edge_count * _projection_ratio()))) * _zoom

func _cell_size() -> float:
	# Legacy query kept for callers; this is an isometric tile's full width.
	return _half_width() * 2.0

func _origin() -> Vector2:
	var dimensions: Vector2i = _dimensions()
	var half: float = _half_width()
	var ground_height: float = float(dimensions.x + dimensions.y) * half * _projection_ratio()
	return Vector2(size.x * 0.5 + float(dimensions.y - dimensions.x) * half * 0.5, (size.y - ground_height) * 0.5 + ROOF_CLEARANCE * 0.3) + _pan

func _point(grid: Vector2) -> Vector2:
	var half: float = _half_width()
	return _origin() + Vector2((grid.x - grid.y) * half, (grid.x + grid.y) * half * _projection_ratio())

func _grid_at(local_position: Vector2) -> Vector2:
	var half: float = maxf(_half_width(), 0.001)
	var delta: Vector2 = local_position - _origin()
	var summed: float = delta.y / (half * _projection_ratio())
	var difference: float = delta.x / half
	return Vector2((summed + difference) * 0.5, (summed - difference) * 0.5)

func _tile_at(local_position: Vector2) -> Vector2i:
	var grid: Vector2 = _grid_at(local_position)
	var tile: Vector2i = Vector2i(floori(grid.x + 0.000001), floori(grid.y + 0.000001))
	var dimensions: Vector2i = _dimensions()
	if tile.x < 0 or tile.y < 0 or tile.x >= dimensions.x or tile.y >= dimensions.y:
		return Vector2i(-1, -1)
	return tile

func _diamond(tile: Vector2, footprint: Vector2 = Vector2.ONE) -> PackedVector2Array:
	return PackedVector2Array([_point(tile), _point(tile + Vector2(footprint.x, 0.0)), _point(tile + footprint), _point(tile + Vector2(0.0, footprint.y))])

func _tile_rect(tile: Vector2i, footprint: Vector2i = Vector2i.ONE) -> Rect2:
	var points: PackedVector2Array = _diamond(Vector2(tile), Vector2(footprint))
	return Rect2(Vector2(points[3].x, points[0].y), Vector2(points[1].x - points[3].x, points[2].y - points[0].y))

func _limit_pan() -> void:
	var dimensions: Vector2i = _dimensions()
	var half: float = _half_width()
	var content: Vector2 = Vector2(float(dimensions.x + dimensions.y) * half, float(dimensions.x + dimensions.y) * half * _projection_ratio() + ROOF_CLEARANCE * _zoom)
	# Edge facilities must be focusable at the viewport center too.
	var extent: Vector2 = Vector2(maxf(16.0, content.x * 0.5 + MARGIN), maxf(16.0, content.y * 0.5 + MARGIN))
	_pan.x = clampf(_pan.x, -extent.x, extent.x)
	_pan.y = clampf(_pan.y, -extent.y, extent.y)

func _clear_hover() -> void:
	_hover = Vector2i(-1, -1)
	queue_redraw()

func _begin_pointer(position: Vector2) -> void:
	_focus_target = Vector2.INF
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
		var item: Dictionary = get_selected_item()
		if not item.is_empty():
			facility_selected.emit(str(item.get("id", "")))

func get_selected_item() -> Dictionary:
	for collection: String in ["facilities", "projects"]:
		for item: Variant in world.get(collection, []):
			if item is Dictionary and str(item.get("city_id", "")) == city_id and str(item.get("status", "")) not in ["cancelled", "demolished", "completed"] and _contains(item, selected_tile):
				return item.duplicate(true)
	for command: Dictionary in _planned_builds():
		if _contains(command.payload, selected_tile):
			return {"id": command.id, "kind": command.payload.kind, "status": "planned", "x": command.payload.x, "y": command.payload.y, "w": command.payload.get("w", preview_size.x), "h": command.payload.get("h", preview_size.y), "city_id": city_id}
	return {}

func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and _touch_points.is_empty():
		if event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
			zoom_at(_zoom * (1.16 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.0 / 1.16), event.position)
			accept_event()
		elif event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed: _begin_pointer(event.position)
			else: _end_pointer(event.position)
			accept_event()
	elif event is InputEventMouseMotion and _touch_points.is_empty():
		_hover = _tile_at(event.position)
		_move_pointer(event.position)
		queue_redraw()
		if _pointer_down: accept_event()
	elif event is InputEventScreenTouch:
		if event.pressed:
			_touch_points[event.index] = event.position
			if _touch_points.size() == 1: _begin_pointer(event.position)
			else:
				_pointer_down = false
				_moved = true
				_reset_pinch()
		else:
			if _touch_points.size() == 1: _end_pointer(event.position)
			_touch_points.erase(event.index)
			if not _touch_points.is_empty():
				_pointer_down = false
				_moved = true
			_reset_pinch()
		accept_event()
	elif event is InputEventScreenDrag:
		if _touch_points.has(event.index):
			_touch_points[event.index] = event.position
			if _touch_points.size() >= 2: _update_pinch()
			else: _move_pointer(event.position)
			accept_event()
	elif event is InputEventMagnifyGesture:
		_moved = true
		zoom_at(_zoom * event.factor, event.position)
		accept_event()
	elif event is InputEventPanGesture:
		_pan -= event.delta * 12.0
		_limit_pan()
		queue_redraw()
		accept_event()
	elif event is InputEventKey and event.pressed:
		if event.keycode in [KEY_PLUS, KEY_EQUAL, KEY_KP_ADD]:
			zoom_in()
			accept_event()
		elif event.keycode in [KEY_MINUS, KEY_KP_SUBTRACT]:
			zoom_out()
			accept_event()
		elif event.keycode == KEY_HOME:
			center_view()
			accept_event()

func _touch_pair() -> Array:
	var keys: Array = _touch_points.keys()
	keys.sort()
	return [_touch_points[keys[0]], _touch_points[keys[1]]] if keys.size() >= 2 else []

func _reset_pinch() -> void:
	var pair: Array = _touch_pair()
	_pinch_distance = pair[0].distance_to(pair[1]) if pair.size() == 2 else 0.0
	_pinch_midpoint = (pair[0] + pair[1]) * 0.5 if pair.size() == 2 else Vector2.ZERO

func _update_pinch() -> void:
	var pair: Array = _touch_pair()
	if pair.size() != 2: return
	var distance: float = pair[0].distance_to(pair[1])
	var midpoint: Vector2 = (pair[0] + pair[1]) * 0.5
	if _pinch_distance > 1.0:
		zoom_at(_zoom * distance / _pinch_distance, midpoint)
	_pan += midpoint - _pinch_midpoint
	_limit_pan()
	_pinch_distance = distance
	_pinch_midpoint = midpoint
	_moved = true
	queue_redraw()

func _contains(item: Dictionary, tile: Vector2i) -> bool:
	return Rect2i(int(item.get("x", -100)), int(item.get("y", -100)), maxi(1, int(item.get("w", 2))), maxi(1, int(item.get("h", 2)))).has_point(tile)

func _roads() -> Array:
	var values: Variant = _city().get("roads", [])
	return values if values is Array else []

func _road_at(tile: Vector2i) -> bool:
	for road: Variant in _roads():
		if road is Dictionary and int(road.get("x", -1)) == tile.x and int(road.get("y", -1)) == tile.y: return true
		if road is Array and road.size() >= 2 and int(road[0]) == tile.x and int(road[1]) == tile.y: return true
	return false

func _planned_builds() -> Array:
	var plans: Array = []
	for command: Variant in world.get("commands", []):
		if command is Dictionary and str(command.get("type", "")) == "build" and str(command.get("actor_id", "")) == str(world.get("player_id", "")) and command.get("payload", {}) is Dictionary and str(command.payload.get("city_id", "")) == city_id:
			var plan: Dictionary = command.duplicate(true)
			var footprint: Vector2i = _definition_footprint(str(plan.payload.get("kind", "")))
			plan.payload["w"] = int(plan.payload.get("w", footprint.x))
			plan.payload["h"] = int(plan.payload.get("h", footprint.y))
			plans.append(plan)
	return plans

func _definition_footprint(kind: String) -> Vector2i:
	for item: Variant in world.get("building_definitions", world.get("buildings", [])):
		if item is Dictionary and str(item.get("id", "")) == kind:
			return Vector2i(maxi(1, int(item.get("w", 2))), maxi(1, int(item.get("h", 2))))
	for item: Variant in world.get("facilities", []):
		if item is Dictionary and str(item.get("kind", "")) == kind:
			return Vector2i(maxi(1, int(item.get("w", 2))), maxi(1, int(item.get("h", 2))))
	return Vector2i(2, 2)

func _occupied(tile: Vector2i) -> bool:
	if _road_at(tile): return true
	for collection: String in ["facilities", "projects"]:
		for item: Variant in world.get(collection, []):
			if item is Dictionary and str(item.get("city_id", "")) == city_id and str(item.get("status", "")) not in ["cancelled", "demolished", "completed"] and _contains(item, tile): return true
	for command: Dictionary in _planned_builds():
		if _contains(command.payload, tile): return true
	return false

func _preview_valid(tile: Vector2i) -> bool:
	var dimensions: Vector2i = _dimensions()
	if tile.x < 0 or tile.y < 0 or tile.x + preview_size.x > dimensions.x or tile.y + preview_size.y > dimensions.y: return false
	var road_access: bool = false
	for x: int in range(tile.x, tile.x + preview_size.x):
		for y: int in range(tile.y, tile.y + preview_size.y):
			if _occupied(Vector2i(x, y)): return false
			for adjacent: Vector2i in [Vector2i(x - 1, y), Vector2i(x + 1, y), Vector2i(x, y - 1), Vector2i(x, y + 1)]:
				if _road_at(adjacent): road_access = true
	return road_access

func _visual_hash(x: int, y: int) -> int:
	return ((x * 73856093) ^ (y * 19349663) ^ (int(world.get("seed", 0)) * 83492791)) & 0x7fffffff

func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), BACKGROUND)
	if _city().is_empty():
		_draw_text("도시를 선택하세요", size * 0.5 - Vector2(60, 0), 15, MUTED)
		return
	_draw_terrain()
	var objects: Array = []
	var dimensions: Vector2i = _dimensions()
	for y: int in range(dimensions.y):
		for x: int in range(dimensions.x):
			var tile: Vector2i = Vector2i(x, y)
			if not _occupied(tile) and (_visual_hash(x, y) % (3 if x == 0 or y == 0 or x == dimensions.x - 1 or y == dimensions.y - 1 else 17)) == 0:
				objects.append({"_decor": true, "x": x, "y": y, "w": 1, "h": 1})
	for facility: Variant in world.get("facilities", []):
		if facility is Dictionary and str(facility.get("city_id", "")) == city_id:
			objects.append(facility)
	for project: Variant in world.get("projects", []):
		if project is Dictionary and str(project.get("city_id", "")) == city_id and str(project.get("status", "")) not in ["completed", "cancelled"]:
			var item: Dictionary = project.duplicate()
			item["_construction"] = true
			objects.append(item)
	objects.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var depth_a: int = int(a.get("x", 0)) + int(a.get("y", 0)) + int(a.get("w", 1)) + int(a.get("h", 1))
		var depth_b: int = int(b.get("x", 0)) + int(b.get("y", 0)) + int(b.get("w", 1)) + int(b.get("h", 1))
		return depth_a < depth_b or (depth_a == depth_b and str(a.get("id", "")) < str(b.get("id", "")))
	)
	for item: Dictionary in objects:
		if item.get("_decor", false): _draw_tree(Vector2i(int(item.x), int(item.y)))
		elif item.get("_construction", false): _draw_construction(item)
		else: _draw_facility(item)
	for command: Dictionary in _planned_builds():
		_draw_plan(command.payload)
	var selected: Dictionary = get_selected_item()
	if not selected.is_empty():
		_outline(_diamond(Vector2(int(selected.x), int(selected.y)), Vector2(int(selected.get("w", 2)), int(selected.get("h", 2)))), ACCENT, 2.0)
	var preview: Vector2i = _hover if _hover.x >= 0 else selected_tile
	if build_mode and preview.x >= 0:
		_draw_preview(preview)
	elif selected_tile.x >= 0 and selected.is_empty():
		_outline(_diamond(Vector2(selected_tile)), Color(0.65, 0.91, 1.0, 0.8), 1.5)
	if show_header:
		_draw_header()

func _draw_terrain() -> void:
	var dimensions: Vector2i = _dimensions()
	var points: PackedVector2Array = _diamond(Vector2.ZERO, Vector2(dimensions))
	var shadow: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in points: shadow.append(point + Vector2(3.0, 8.0))
	draw_colored_polygon(shadow, Color(0.0, 0.02, 0.03, 0.45))
	draw_colored_polygon(PackedVector2Array([points[3], points[2], points[2] + Vector2(0, 7), points[3] + Vector2(0, 7)]), Color("375b3f"))
	draw_colored_polygon(PackedVector2Array([points[2], points[1], points[1] + Vector2(0, 7), points[2] + Vector2(0, 7)]), Color("284c36"))
	for y: int in range(dimensions.y):
		for x: int in range(dimensions.x):
			var tile: Vector2i = Vector2i(x, y)
			var diamond: PackedVector2Array = _diamond(Vector2(tile))
			var variation: float = float(_visual_hash(x, y) % 9) / 100.0
			var ground_color: Color = Color(0.43 + variation, 0.56 + variation, 0.29 + variation * 0.6, 1.0)
			draw_colored_polygon(diamond, ground_color)
			var ground_texture: Texture2D = _ground_sprite(_terrain_kind(tile))
			if ground_texture != null:
				draw_texture_rect(ground_texture, _tile_rect(tile), false, Color(1.0, 1.0, 1.0, clampf(float(_art.get("ground_strength", 0.76)), 0.0, 1.0)))
			if _road_at(tile): _draw_road(tile)
			else: _outline(diamond, Color(0.9, 0.96, 0.77, 0.24), maxf(0.5, _zoom * 0.5))
	_outline(points, Color("4a7561"), 1.0)

func _draw_road(tile: Vector2i) -> void:
	var has_x: bool = _road_at(tile + Vector2i(1, 0)) or _road_at(tile + Vector2i(-1, 0))
	var has_y: bool = _road_at(tile + Vector2i(0, 1)) or _road_at(tile + Vector2i(0, -1))
	var texture: Texture2D = _ground_sprite("crossroad" if has_x and has_y else ("road_y" if has_y else "road_x"))
	if texture != null:
		draw_texture_rect(texture, _tile_rect(tile), false, Color.WHITE)
		return
	var base: PackedVector2Array = _diamond(Vector2(tile))
	draw_colored_polygon(base, Color("a3ae9b"))
	var center: Vector2 = _point(Vector2(tile) + Vector2(0.5, 0.5))
	var inner: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in base: inner.append(center + (point - center) * 0.83)
	draw_colored_polygon(inner, Color("66747b"))
	for neighbor: Vector2i in [Vector2i(-1, 0), Vector2i(1, 0), Vector2i(0, -1), Vector2i(0, 1)]:
		if _road_at(tile + neighbor):
			var direction: Vector2 = _point(Vector2(neighbor)) - _point(Vector2.ZERO)
			draw_line(center + direction * 0.11, center + direction * 0.37, Color("e5e5cf"), maxf(0.7, _zoom), true)
	_outline(base, Color("8d9f96"), 0.7)

func _terrain_kind(tile: Vector2i) -> String:
	for item: Variant in _city().get("terrain", []):
		if item is Dictionary and int(item.get("x", -1)) == tile.x and int(item.get("y", -1)) == tile.y:
			return str(item.get("kind", "grass"))
	return "grass"

func _ground_sprite(key: String) -> Texture2D:
	if _ground == null: return null
	var cache_key: String = "ground/" + key
	if _sprite_cache.has(cache_key): return _sprite_cache[cache_key]
	var cells: Dictionary = _art.get("ground_cells", {})
	var region: Variant = cells.get(key, cells.get("grass", []))
	if not region is Array or region.size() != 4: return null
	var texture := AtlasTexture.new()
	texture.atlas = _ground
	texture.region = Rect2(float(region[0]), float(region[1]), float(region[2]), float(region[3]))
	texture.filter_clip = true
	_sprite_cache[cache_key] = texture
	return texture

func _sprite(key: String) -> Texture2D:
	if _atlas == null: return null
	if _sprite_cache.has(key): return _sprite_cache[key]
	var definitions: Dictionary = _art.get("cells", {})
	var definition: Variant = definitions.get(key, definitions.get("factory", {}))
	if definition is int or definition is float: definition = {"index": int(definition)}
	if not definition is Dictionary: return null
	var index: int = int(definition.get("index", 0))
	var columns: int = maxi(1, int(_art.get("columns", 4)))
	var rows: int = maxi(1, int(_art.get("rows", 4)))
	var cell: Vector2 = Vector2(float(_atlas.get_width()) / columns, float(_atlas.get_height()) / rows)
	var inset: float = float(definition.get("inset", _art.get("inset", 0.02)))
	var texture := AtlasTexture.new()
	texture.atlas = _atlas
	var explicit_region: Variant = definition.get("region", [])
	if explicit_region is Array and explicit_region.size() == 4:
		texture.region = Rect2(float(explicit_region[0]), float(explicit_region[1]), float(explicit_region[2]), float(explicit_region[3]))
	else:
		texture.region = Rect2(Vector2(index % columns, int(index / columns)) * cell + cell * inset, cell * (1.0 - inset * 2.0))
	texture.filter_clip = true
	_sprite_cache[key] = texture
	return texture

func _sprite_key(kind: String) -> String:
	var definitions: Dictionary = _art.get("cells", {})
	if definitions.has(kind): return kind
	if kind.contains("farm") or kind.contains("livestock") or kind.contains("fish") or kind.contains("agric"): return "farm"
	if kind.contains("forest") or kind.contains("timber"): return "forestry"
	if kind.contains("mine") or kind.contains("quarry") or kind.contains("extract"): return "quarry"
	if kind.contains("power") or kind.contains("energy"): return "power"
	if kind.contains("water"): return "water"
	if kind.contains("warehouse") or kind.contains("logistic"): return "warehouse"
	if kind.contains("hospital") or kind.contains("health") or kind.contains("medical"): return "hospital"
	if kind.contains("software") or kind.contains("digital") or kind.contains("research"): return "software"
	if kind.contains("port") or kind.contains("transport"): return "port"
	if kind.contains("office") or kind.contains("bank") or kind.contains("service"): return "office"
	if kind.contains("housing") or kind.contains("residen"): return "housing"
	return "factory"

func _draw_facility(item: Dictionary) -> void:
	var position: Vector2 = Vector2(int(item.get("x", 0)), int(item.get("y", 0)))
	var footprint: Vector2 = Vector2(maxi(1, int(item.get("w", 2))), maxi(1, int(item.get("h", 2))))
	var diamond: PackedVector2Array = _diamond(position, footprint)
	var shadow: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in diamond: shadow.append(point + Vector2(4, 5) * _zoom)
	draw_colored_polygon(shadow, Color(0.06, 0.12, 0.10, 0.28))
	draw_colored_polygon(diamond, Color("a8b9a4"))
	var foundation: Texture2D = _ground_sprite("plaza")
	if foundation != null: draw_texture_rect(foundation, _tile_rect(Vector2i(position), Vector2i(footprint)), false)
	_outline(diamond, Color("c4d0b6"), 0.7)
	var key: String = _sprite_key(str(item.get("kind", "factory")))
	var texture: Texture2D = _sprite(key)
	var tint: Color = Color.WHITE
	if str(item.get("status", "operational")) not in ["operational", "ready", "active"]:
		tint = Color(0.65, 0.71, 0.73, 0.85)
	if texture != null:
		_draw_building_texture(texture, key, position, footprint, tint)
	else:
		_draw_fallback_building(item, position, footprint, tint)
	if _contains(item, selected_tile):
		_outline(diamond, ACCENT, 2.0)

func _draw_building_texture(texture: Texture2D, key: String, position: Vector2, footprint: Vector2, tint: Color) -> void:
	var definitions: Dictionary = _art.get("cells", {})
	var definition: Variant = definitions.get(key, {})
	if not definition is Dictionary: definition = {}
	var anchor_data: Array = definition.get("anchor", [0.5, 0.91])
	var anchor: Vector2 = Vector2(float(anchor_data[0]), float(anchor_data[1]))
	var width: float = (footprint.x + footprint.y) * _half_width() * float(definition.get("scale", _art.get("building_scale", 1.2)))
	var height: float = width * float(texture.get_height()) / maxf(1.0, float(texture.get_width()))
	var bottom: Vector2 = _point(position + footprint)
	var rect: Rect2 = Rect2(bottom - Vector2(width * anchor.x, height * anchor.y), Vector2(width, height))
	draw_texture_rect(texture, rect, false, tint)

func _draw_fallback_building(item: Dictionary, position: Vector2, footprint: Vector2, tint: Color) -> void:
	# Only used before generated resources are installed or for damaged content.
	var kind: String = str(item.get("kind", "factory"))
	var inset: Vector2 = footprint * 0.14
	var base: PackedVector2Array = _diamond(position + inset, footprint * 0.72)
	var height: float = _half_width() * (2.1 if kind in ["power", "office", "software"] else 1.15)
	var top: PackedVector2Array = PackedVector2Array()
	for point: Vector2 in base: top.append(point - Vector2(0, height))
	var wall: Color = Color("c2cdd0") * tint
	var roof: Color = Color("537b90") * tint
	if kind in ["farm", "forestry"]: roof = Color("a2684f") * tint
	if kind == "quarry": roof = Color("98988a") * tint
	draw_colored_polygon(PackedVector2Array([base[3], base[2], top[2], top[3]]), wall.darkened(0.16))
	draw_colored_polygon(PackedVector2Array([base[2], base[1], top[1], top[2]]), wall.darkened(0.32))
	draw_colored_polygon(top, roof)
	_outline(top, Color(0.85, 0.94, 0.96, 0.65), 0.6)
	for i: int in range(3):
		var a: Vector2 = top[3].lerp(top[2], 0.2 + i * 0.23) + Vector2(0, height * 0.36)
		draw_line(a, a + Vector2(_half_width() * 0.2, _half_width() * 0.1), Color("89bdd7"), maxf(1.0, _zoom * 1.5), true)

func _draw_construction(item: Dictionary) -> void:
	var position: Vector2 = Vector2(int(item.get("x", 0)), int(item.get("y", 0)))
	var footprint: Vector2 = Vector2(maxi(1, int(item.get("w", 2))), maxi(1, int(item.get("h", 2))))
	var points: PackedVector2Array = _diamond(position, footprint)
	draw_colored_polygon(points, Color(0.24, 0.47, 0.50, 0.8))
	_outline(points, Color(0.0, 0.75, 1.0, 0.18), 7.0)
	_outline(points, ACCENT, 2.0)
	var texture: Texture2D = _sprite("construction")
	if texture != null:
		_draw_building_texture(texture, "construction", position, footprint, Color.WHITE)
	else:
		var height: float = _half_width() * 1.8
		for x: int in range(3):
			for y: int in range(3):
				var base: Vector2 = _point(position + footprint * Vector2(x / 2.0, y / 2.0))
				draw_line(base, base - Vector2(0, height), Color("ad7961"), maxf(1.0, _zoom * 1.4), true)
		for index: int in range(points.size()):
			draw_line(points[index] - Vector2(0, height), points[(index + 1) % points.size()] - Vector2(0, height), Color("cead8d"), maxf(1.0, _zoom), true)
	var total: int = maxi(1, int(item.get("required_work", 1)))
	var ratio: float = clampf(float(item.get("progress", 0)) / total, 0.0, 1.0)
	var center: Vector2 = _point(position + footprint * 0.5)
	var front: Vector2 = _point(position + footprint)
	draw_line(front - Vector2(18, 2), front + Vector2(18, -2), Color("12334d"), 5, true)
	draw_line(front - Vector2(18, 2), (front - Vector2(18, 2)).lerp(front + Vector2(18, -2), ratio), ACCENT, 3, true)
	if _contains(item, selected_tile) or get_selected_item().is_empty():
		_draw_callout(("중단됨  %d%%" if str(item.get("status", "")) == "paused" else "공사 중  %d%%") % int(round(ratio * 100.0)), center - Vector2(0, _half_width() * 2.0))

func _draw_plan(payload: Dictionary) -> void:
	var points: PackedVector2Array = _diamond(Vector2(int(payload.get("x", 0)), int(payload.get("y", 0))), Vector2(int(payload.get("w", preview_size.x)), int(payload.get("h", preview_size.y))))
	draw_colored_polygon(points, Color(0.0, 0.52, 0.93, 0.20))
	_outline(points, Color(0.3, 0.8, 1.0, 0.8), 1.5)

func _draw_preview(tile: Vector2i) -> void:
	var valid: bool = _preview_valid(tile)
	var color: Color = ACCENT if valid else DANGER
	var points: PackedVector2Array = _diamond(Vector2(tile), Vector2(preview_size))
	draw_colored_polygon(points, Color(color, 0.23))
	_outline(points, Color(color, 0.15), 7.0)
	_outline(points, color, 2.0)
	for point: Vector2 in points: draw_circle(point, maxf(2.0, _zoom * 1.5), color)

func _draw_tree(tile: Vector2i) -> void:
	var position: Vector2 = Vector2(tile)
	var texture: Texture2D = _sprite("trees")
	if texture != null:
		_draw_building_texture(texture, "trees", position, Vector2.ONE, Color.WHITE)
		return
	var center: Vector2 = _point(position + Vector2(0.5, 0.65))
	var half: float = _half_width()
	draw_line(center, center - Vector2(0, half * 0.6), Color("6c7654"), maxf(1.0, _zoom), true)
	draw_colored_polygon(PackedVector2Array([center - Vector2(0, half * 1.5), center + Vector2(half * 0.35, -half * 0.45), center + Vector2(-half * 0.38, -half * 0.45)]), Color("2c6944"))
	draw_colored_polygon(PackedVector2Array([center - Vector2(0, half * 1.08), center + Vector2(half * 0.43, -half * 0.15), center + Vector2(-half * 0.43, -half * 0.15)]), Color("3c7b49"))

func _draw_callout(text: String, position: Vector2) -> void:
	var font: Font = get_theme_default_font()
	var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x + 18.0
	var rect: Rect2 = Rect2(position - Vector2(width * 0.5, 26.0), Vector2(width, 25.0))
	if _callout != null:
		var style := StyleBoxTexture.new()
		style.texture = _callout
		style.texture_margin_left = 8
		style.texture_margin_right = 8
		style.texture_margin_top = 8
		style.texture_margin_bottom = 8
		draw_style_box(style, rect)
	else:
		draw_style_box(_bubble_style(), rect)
	draw_colored_polygon(PackedVector2Array([position + Vector2(-4, -1), position + Vector2(4, -1), position + Vector2(0, 5)]), Color("0b344f"))
	draw_string(font, rect.position + Vector2(9, 17), text, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("a7eaff"))

func _bubble_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color("0b344f")
	style.border_color = ACCENT
	style.set_border_width_all(1)
	style.set_corner_radius_all(5)
	return style

func _draw_header() -> void:
	_draw_text("도시 지도", Vector2(12, 20), 12, MUTED)
	var font: Font = get_theme_default_font()
	var value: String = "%d%%" % int(round(_zoom * 100.0))
	draw_string(font, Vector2(size.x - 48, 20), value, HORIZONTAL_ALIGNMENT_LEFT, -1, 11, MUTED)

func _draw_text(value: String, position: Vector2, font_size: int, color: Color) -> void:
	draw_string(get_theme_default_font(), position, value, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size, color)

func _outline(points: PackedVector2Array, color: Color, width: float) -> void:
	if points.is_empty(): return
	var closed: PackedVector2Array = points.duplicate()
	closed.append(points[0])
	draw_polyline(closed, color, width, true)
