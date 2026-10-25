extends SceneTree

# Build-time notice extraction from the exact engine used for the previews.
# These notices cover Godot and its dependencies, not this game's source code.
func _init() -> void:
	var engine_version: Dictionary = Engine.get_version_info()
	var version: String = str(engine_version.get("string", ""))
	if engine_version.get("major", 0) != 4 or engine_version.get("minor", 0) != 6 or engine_version.get("patch", 0) != 3 or engine_version.get("status", "") != "stable":
		push_error("Generate engine notices with Godot 4.6.3 stable.")
		quit(1)
		return
	var output := FileAccess.open("res://assets/GODOT-NOTICES.txt", FileAccess.WRITE)
	if output == null:
		push_error("Could not write engine license notices.")
		quit(1)
		return
	output.store_string("GODOT ENGINE AND THIRD-PARTY NOTICES\n")
	output.store_string("Engine version: " + version + "\n")
	output.store_string("Generated from Engine.get_license_text(), Engine.get_copyright_info(), and Engine.get_license_info().\n\n")
	output.store_string("GODOT ENGINE LICENSE\n\n" + Engine.get_license_text() + "\n\n")
	output.store_string("THIRD-PARTY COPYRIGHT INFORMATION\n\n")
	for component in Engine.get_copyright_info():
		output.store_string(str(component.get("name", "")) + "\n")
		for part in component.get("parts", []):
			for copyright in part.get("copyright", []):
				output.store_string(str(copyright) + "\n")
			output.store_string("License: " + str(part.get("license", "")) + "\n")
			output.store_string("Files:\n")
			for source_file in part.get("files", []):
				output.store_string("  " + str(source_file) + "\n")
			output.store_string("\n")
		output.store_string("\n")
	output.store_string("THIRD-PARTY LICENSE TEXTS\n\n")
	var licenses: Dictionary = Engine.get_license_info()
	var license_names: Array = licenses.keys()
	license_names.sort()
	for license_name in license_names:
		output.store_string(str(license_name) + "\n\n" + str(licenses[license_name]) + "\n\n")
	output.close()
	print("Wrote exact Godot 4.6.3 engine and dependency notices.")
	quit(0)
