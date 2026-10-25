extends RefCounted
## Geometry and interaction contracts for the generated-art isometric map.
const CityMap = preload("res://ui/city_map.gd")
static var checks: int = 0

static func run(require_generated_art: bool = true) -> Array:
	var failures: Array = []
	checks = 0
	var state: Dictionary = {
		"seed": 42, "player_id": "p-0", "cities": [{"id": "city-0", "width": 12, "height": 12, "roads": []}],
		"facilities": [{"id": "f-0", "city_id": "city-0", "x": 0, "y": 3, "w": 2, "h": 2, "kind": "bakery", "status": "operational"}],
		"projects": [{"id": "project-1", "city_id": "city-0", "x": 2, "y": 6, "w": 2, "h": 2, "kind": "mill", "status": "active", "progress": 12, "required_work": 24}],
		"commands": []
	}
	for x: int in range(12): state.cities[0].roads.append({"x": x, "y": 5})
	var map = CityMap.new()
	map.size = Vector2(448, 340)
	map.custom_minimum_size.y = 340
	map._ready()
	map.update_world(state, "city-0")
	_expect(map.custom_minimum_size.y == 340, "Map readiness preserves the parent's requested height", failures)
	map.focus_company("", 1.8)
	map.focus_tile(Vector2(1, 4), 1.8)
	map.size = Vector2(448, 340)
	map._on_resized()
	_expect(map._point(Vector2(1, 4)).distance_to(map.size * 0.5) < 0.01, "Real facility focus survives container layout resizing", failures)
	map.center_view()
	var original: String = JSON.stringify(state, "", true).sha256_text()
	for viewport: Vector2 in [Vector2(448, 340), Vector2(360, 280), Vector2(640, 420)]:
		map.size = viewport
		for factor: float in [0.7, 1.0, 2.0, 3.6]:
			map.set_zoom(factor)
			for x: int in range(12):
				for y: int in range(12):
					var center: Vector2 = map._point(Vector2(x + 0.5, y + 0.5))
					_expect(map._tile_at(center) == Vector2i(x, y), "Inverse isometric projection selects exact tile center %d,%d zoom%.1f" % [x, y, factor], failures)
	map.center_view()
	_expect(map._tile_at(map._point(Vector2(-0.25, -0.25))) == Vector2i(-1, -1), "Outside isometric land cannot select a city tile", failures)
	_expect(map._tile_at(map._point(Vector2(12.25, 5.0))) == Vector2i(-1, -1), "Far edge remains outside the map", failures)
	_expect(map._preview_valid(Vector2i(6, 6)), "A vacant roadside footprint is available", failures)
	_expect(not map._preview_valid(Vector2i(0, 3)), "Existing facility blocks construction preview", failures)
	_expect(not map._preview_valid(Vector2i(6, 5)), "Road tiles block construction preview", failures)
	_expect(not map._preview_valid(Vector2i(11, 6)), "Footprint cannot extend beyond city bounds", failures)
	_expect(not map._preview_valid(Vector2i(6, 8)), "Isolated plot without road access is not marked valid", failures)
	var selected: Array = []
	map.facility_selected.connect(func(id: String): selected.append(id))
	map._activate(Vector2i(0, 3))
	_expect(selected == ["f-0"], "Facility selection returns its actual simulation ID", failures)
	var copy: Dictionary = map.get_selected_item()
	copy.status = "modified"
	_expect(state.facilities[0].status == "operational", "Selected-item query returns a copy of simulation data", failures)
	map._activate(Vector2i(2, 6))
	_expect(selected[-1] == "project-1", "Construction selection returns its actual project ID", failures)
	var before_drag: int = selected.size()
	map._pointer_down = true
	map._pointer_start = Vector2.ZERO
	map._last_pointer = Vector2.ZERO
	map._moved = false
	map._move_pointer(Vector2(50, 40))
	map._end_pointer(map._point(Vector2(0.5, 3.5)))
	_expect(selected.size() == before_drag, "Camera drag cannot accidentally activate a facility", failures)
	map.center_view()
	var focal_point: Vector2 = map._point(Vector2(6.5, 6.5))
	var focal_grid: Vector2 = map._grid_at(focal_point)
	map.zoom_at(1.5, focal_point)
	_expect(map._grid_at(focal_point).distance_to(focal_grid) < 0.001, "Zoom keeps the selected focal point under the pointer", failures)
	_expect(JSON.stringify(state, "", true).sha256_text() == original, "Rendering queries, selection, pan and zoom never change economic state", failures)
	state.commands.append({"id": "own-build", "type": "build", "actor_id": "p-0", "payload": {"city_id": "city-0", "kind": "bakery", "x": 6, "y": 6, "w": 2, "h": 2}})
	state.commands.append({"id": "private-build", "type": "build", "actor_id": "p-1", "payload": {"city_id": "city-0", "kind": "mill", "x": 8, "y": 6, "w": 2, "h": 2}})
	_expect(not map._preview_valid(Vector2i(6, 6)), "Own pending construction reserves its own footprint", failures)
	_expect(map._preview_valid(Vector2i(8, 6)), "A rival's uncommitted private plan cannot pre-empt the UI plot", failures)
	_expect(map._planned_builds().size() == 1, "The map never exposes rival private pending construction plans", failures)
	if require_generated_art:
		_expect(map.art_status().generated_atlas_loaded, "Generated building/foliage atlas is an actual loaded resource", failures)
		_expect(map.art_status().ground_loaded, "Generated ground is an actual loaded resource", failures)
		_expect(map.art_status().callout_loaded, "Construction callout uses the generated UI skin resource", failures)
	map.free()
	print("Isometric map checks: %d" % checks)
	return failures

static func _expect(condition: bool, reason: String, failures: Array) -> void:
	checks += 1
	if not condition: failures.append(reason)
