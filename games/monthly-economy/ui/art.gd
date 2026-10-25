extends RefCounted
## Generated UI graphics stay in resources; live text is rendered separately.
static var _manifest: Dictionary = {}
static var _textures: Dictionary = {}
const MANIFEST: String = "res://assets/generated/ui_assets.json"

static func manifest() -> Dictionary:
	if _manifest.is_empty() and FileAccess.file_exists(MANIFEST):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
		if parsed is Dictionary: _manifest = parsed
	return _manifest

static func texture(id: String) -> Texture2D:
	if _textures.has(id): return _textures[id]
	var data: Dictionary = manifest()
	var record: Variant = data.get("textures", {}).get(id, data.get("icons", {}).get(id, null))
	if record == null:
		var conventional: String = "res://assets/generated/" + id + ".png"
		if ResourceLoader.exists(conventional):
			var loaded: Texture2D = load(conventional)
			_textures[id] = loaded
			return loaded
		return null
	var path: String = str(record) if record is String else str(record.get("path", data.get("atlas", "")))
	if not path.begins_with("res://"): path = "res://assets/generated/" + path
	if not ResourceLoader.exists(path): return null
	var base: Texture2D = load(path)
	if record is Dictionary and record.get("rect", []) is Array and record.get("rect", []).size() == 4:
		var rect: Array = record.rect
		var atlas := AtlasTexture.new()
		atlas.atlas = base
		atlas.region = Rect2(float(rect[0]), float(rect[1]), float(rect[2]), float(rect[3]))
		_textures[id] = atlas
		return atlas
	_textures[id] = base
	return base

static func style(id: String, fallback: Color, border: Color = Color.TRANSPARENT, padding: int = 12) -> StyleBox:
	var graphic: Texture2D = texture(id)
	if graphic != null:
		var box := StyleBoxTexture.new()
		box.texture = graphic
		var margin: int = 14
		box.texture_margin_left = margin
		box.texture_margin_top = margin
		box.texture_margin_right = margin
		box.texture_margin_bottom = margin
		box.content_margin_left = padding
		box.content_margin_right = padding
		box.content_margin_top = padding
		box.content_margin_bottom = padding
		return box
	var box := StyleBoxFlat.new()
	box.bg_color = fallback
	box.set_corner_radius_all(10)
	box.set_content_margin_all(padding)
	if border.a > 0:
		box.set_border_width_all(1)
		box.border_color = border
	return box
