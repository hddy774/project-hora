extends SceneTree
## Run against the exported pack, never resolve resources from developmentsource.
func _init() -> void:
	var args: PackedStringArray = OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Pass the source directory after -- for packaged byte comparisons")
		quit(1)
		return
	var source: String = args[0]
	var expected: Array = ["assets/generated/ui_assets.json", "assets/generated/provenance.json", "assets/generated/app_icon.provenance.json", "assets/map/atlas.json", "assets/GODOT-NOTICES.txt", "assets/FONT-LICENSE.txt"]
	var directory: DirAccess = DirAccess.open(source.path_join("data"))
	if directory == null:
		push_error("Source definitions are unavailable")
		quit(1)
		return
	for name: String in directory.get_files():
		if name.ends_with(".json"): expected.append("data/" + name)
	for path: String in expected:
		if not FileAccess.file_exists("res://" + path) or FileAccess.get_file_as_bytes("res://" + path) != FileAccess.get_file_as_bytes(source.path_join(path)):
			push_error("Exported definition/license differs: " + path)
			quit(1)
			return
	var art: Variant = load("res://ui/art.gd")
	if art == null:
		push_error("Generated graphics loader is missing")
		quit(1)
		return
	var manifest: Dictionary = art.manifest()
	var image_count: int = 0
	for group: String in ["textures", "icons"]:
		for id: String in manifest.get(group, {}):
			var graphic: Texture2D = art.texture(id)
			if graphic == null or graphic.get_width() <= 0 or graphic.get_height() <= 0:
				push_error("Generated graphic cannot load: " + id)
				quit(1)
				return
			image_count += 1
	if image_count <= 0:
		push_error("Generated UI graphic registry is empty")
		quit(1)
		return
	var economy: Variant = load("res://core/economy.gd")
	if economy == null:
		push_error("Exported simulation core cannot load")
		quit(1)
		return
	var world: Dictionary = economy.new_world(42, {"cities_per_nation": 1, "expanded_industries": false})
	var errors: Array = economy.validate(world)
	if not errors.is_empty():
		push_error("Exported world failed validation: " + str(errors))
		quit(1)
		return
	print("PASS: exported core, all domain definitions/licenses and %d generated graphics load from the packaged resources." % image_count)
	quit(0)
