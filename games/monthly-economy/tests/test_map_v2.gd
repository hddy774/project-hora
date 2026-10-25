extends RefCounted
## Geometry and interaction contracts for the generated-art isometric map.
const CityMap = preload("res://ui/city_map.gd")
const HistoryChart = preload("res://ui/history_chart.gd")
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
	var work: Dictionary = map.construction_progress({"progress":12,"required_work":24,"months_worked":3,"min_months":3})
	_expect(work.known and is_equal_approx(float(work.ratio),0.5), "Map progress is measured physical work, even when minimum elapsed time is complete", failures)
	_expect(work.label.contains("50%") and work.time_label == "최소 공기 3 / 3개월", "Map work percentage and elapsed minimum period remain separate", failures)
	var unknown: Dictionary = map.construction_progress({"progress":12})
	_expect(not unknown.known and unknown.ratio == 0.0 and not str(unknown.label).contains("%"), "Missing construction work denominator displays unknown, never fabricated completion", failures)
	_expect(not map.construction_progress({"progress":12,"required_work":0}).known, "Zero required work cannot create a division or misleading progress", failures)
	_expect(map.construction_progress({"progress":1,"required_work":3,"status":"paused"}).label == "공사 중단 · 작업 33%", "Paused work retains its actual completed fraction without rounding up", failures)
	_expect(not map.plot_area(state.facilities[0]).known, "A city without a configured physical tile side does not invent a plot area", failures)
	state.cities[0]["tile_side_m"] = 10
	var area: Dictionary = map.plot_area(state.facilities[0])
	_expect(area.known and area.area_m2 == 400.0 and area.label == "400 m²", "Physical plot area uses actual footprint and configured city tile side", failures)
	state.cities[0].erase("tile_side_m")
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
	var chart = HistoryChart.new()
	chart.value_key = "cash"
	_expect(chart.display_value(123456) == "$1,234.56", "Chart money uses cents and readable dollars", failures)
	_expect(chart.display_value(100000000) == "$1M", "Chart money supports the requested M scale", failures)
	_expect(chart.display_value(-100000000000) == "-$1B", "Chart preserves negative values and B scale", failures)
	chart.currency_view = "KRW"
	_expect(chart.display_value(100000000) == "10억원", "Chart uses the selected fixed-rate won display", failures)
	chart.value_key = "trades"
	_expect(chart.display_value(1234) == "1,234건", "Transaction counts keep their count unit when currency view changes", failures)
	chart.product_id = "grain"
	chart.world = {"products":[{"id":"grain","unit":"kg"}]}
	_expect(chart.display_value(1250) == "1,250 kg", "Physical chart quantities show the actual product unit", failures)
	_expect(chart.display_value(null) == "자료 없음", "Unavailable chart observations are not shown as zero", failures)
	var source: Array = [{"month":1,"trades":3},{"month":2},{"month":3,"trades":5}]
	chart.set_observations(source)
	_expect(chart._value(1) == null, "An absent committed observation creates a gap, not a synthetic zero", failures)
	chart.observations[0].trades = 99
	_expect(source[0].trades == 3, "Chart selection and formatting retain a copy of committed history", failures)
	chart.free()
	print("Isometric map checks: %d" % checks)
	return failures

static func _expect(condition: bool, reason: String, failures: Array) -> void:
	checks += 1
	if not condition: failures.append(reason)
