extends SceneTree

# Invoke Godot with --headless --main-pack <exported Linux executable> and
# --script <absolute path> to verify the embedded pack rather than source files.
func _init() -> void:
	var args: PackedStringArray = OS.get_cmdline_user_args()
	if args.size() != 1:
		push_error("Pass the source project directory after -- for byte comparisons.")
		quit(1)
		return
	var source_dir: String = args[0]
	for asset_path: String in ["data/experimental.json", "assets/GODOT-NOTICES.txt", "assets/FONT-LICENSE.txt"]:
		if not FileAccess.file_exists("res://" + asset_path):
			push_error("Missing packaged asset: " + asset_path)
			quit(1)
			return
		if FileAccess.get_file_as_bytes("res://" + asset_path) != FileAccess.get_file_as_bytes(source_dir.path_join(asset_path)):
			push_error("Packaged asset differs from source: " + asset_path)
			quit(1)
			return
	var economy = load("res://core/economy.gd")
	if economy == null:
		push_error("Could not load the packaged economy core.")
		quit(1)
		return
	var world: Dictionary = economy.new_world(42)
	var errors: Array = economy.validate(world)
	if not errors.is_empty():
		push_error("Packaged fixture failed validation: " + str(errors))
		quit(1)
		return
	print("PASS: exported Linux core initializes from its packaged JSON; fixture and license notices match source bytes.")
	quit(0)
