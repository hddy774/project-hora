extends SceneTree
## Reproducible, headless experiments; no renderer or external AI API is used.
const Economy = preload("res://core/economy.gd")

func _init() -> void:
	var months: int = 3
	var seed_value: int = 42
	var output: String = "user://last-experiment.json"
	for arg: String in OS.get_cmdline_user_args():
		var parts: PackedStringArray = arg.split("=", true, 1)
		if parts.size() != 2 or parts[0] not in ["--months", "--seed", "--output"]:
			push_error("Use -- --months=3 --seed=42 --output=user://last-experiment.json")
			quit(2)
			return
		if parts[0] == "--output":
			output = parts[1]
		else:
			if not parts[1].is_valid_int():
				push_error("Experimental seed/month inputs must be whole integers")
				quit(2)
				return
			if parts[0] == "--months": months = parts[1].to_int()
			else: seed_value = parts[1].to_int()
	if months < 1 or seed_value < 0 or seed_value > 2147483647 or output.is_empty():
		push_error("Invalid experimental input")
		quit(2)
		return
	var world: Dictionary = Economy.new_world(seed_value)
	var errors: Array = Economy.validate(world)
	if not errors.is_empty():
		push_error(str(errors))
		quit(1)
		return
	var experiment: Dictionary = {"experimental": true, "seed": seed_value, "months_requested": months, "rules_version": world.rules_version, "content_hash": world.content_hash, "godot": Engine.get_version_info().string, "observations": [], "performance_scope": "one headless resolve call on this host; not Android or supported scale"}
	for i in range(months):
		var started: int = Time.get_ticks_msec()
		var resolved: Dictionary = Economy.resolve_month(world)
		var elapsed: int = Time.get_ticks_msec() - started
		if not resolved.get("ok", false):
			push_error("Experiment rejected month: " + str(resolved.get("errors", [])))
			quit(1)
			return
		world = resolved.state
		var observation: Dictionary = {"month": world.last_resolved_month, "resolve_ms": elapsed, "state_hash": Economy.state_hash(world), "world": Economy.metrics(world), "cities": []}
		# Do not export mixed-unit diagnostic totals as economic indicators.
		for key: String in ["production", "consumption", "unmet", "inventory_total", "price_index"]:
			observation.world.erase(key)
		for city: Dictionary in world.cities:
			var metrics: Dictionary = Economy.metrics(world, str(city.id))
			for key: String in ["production", "consumption", "unmet", "inventory_total", "price_index"]:
				metrics.erase(key)
			observation.cities.append(metrics)
		experiment.observations.append(observation)
		print("Experimental month %d: %d ms, %d settled goods trades, hash %s" % [world.last_resolved_month, elapsed, resolved.report.trades, observation.state_hash])
	var file: FileAccess = FileAccess.open(output, FileAccess.WRITE)
	if file == null:
		push_error("Experiment output could not be written")
		quit(1)
		return
	file.store_string(JSON.stringify(experiment, "  ", true, true))
	file.flush()
	var code: Error = file.get_error()
	file.close()
	if code != OK:
		push_error("Experiment output write failed")
		quit(1)
		return
	print("Experimental observations written to " + output)
	quit(0)
