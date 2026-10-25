extends RefCounted
## Local checksummed snapshots with an independently verified previous copy.
## Neither loading nor reading a report advances the simulation clock.

const Ledger = preload("res://core/ledger.gd")
const FORMAT_VERSION: int = 2
const SCHEMA_VERSION: int = 3
const RULES_VERSION: String = "experimental-0.2-usd"
const SAVE_DIR: String = "user://saves"
const MAX_SAVE_BYTES: int = 64 * 1024 * 1024
const MAX_INTEGER: int = 9007199254740991
# This released content used virtual currencies and unrelated cargo dimensions.
# Renaming its money while relabeling its inventory would change physical stock
# and transport obligations, so no partial conversion is presented as a success.
const LEGACY_SCHEMA_VERSION: int = 2
const LEGACY_RULES_VERSION: String = "experimental-0.2"
const LEGACY_CONTENT_HASHES: Array = ["8658e3c73aa23b5bb148777b99dde211ab8a52483c9ed7f46304e9839c96e00e"]

static func canonical(value: Variant) -> String:
	return JSON.stringify(_normalize(value),"",true,true)

static func hash_state(state: Dictionary) -> String:
	return canonical(state).sha256_text()

static func _fail(message: String) -> Dictionary:
	return {"ok": false, "error": message, "state": {}, "recovered": false}

static func _valid_slot(slot: String) -> bool:
	if slot.is_empty() or slot.length() > 48:
		return false
	for index in range(slot.length()):
		var code: int = slot.unicode_at(index)
		if not ((code >= 48 and code <= 57) or (code >= 65 and code <= 90) or (code >= 97 and code <= 122) or code == 45 or code == 95):
			return false
	return true

static func _normalize(value: Variant) -> Variant:
	if typeof(value) == TYPE_STRING_NAME:
		return str(value)
	if value is Dictionary:
		var result: Dictionary = {}
		for key in value:
			result[str(key)] = _normalize(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item in value:
			result.append(_normalize(item))
		return result
	if typeof(value) == TYPE_FLOAT and is_finite(value) and value == floor(value) and abs(value) <= MAX_INTEGER:
		return int(value)
	return value

static func _json_safe(value: Variant) -> bool:
	match typeof(value):
		TYPE_NIL, TYPE_BOOL, TYPE_STRING, TYPE_STRING_NAME:
			return true
		TYPE_INT:
			return value >= -MAX_INTEGER and value <= MAX_INTEGER
		TYPE_FLOAT:
			return is_finite(value) and abs(value) <= MAX_INTEGER
		TYPE_ARRAY:
			for item in value:
				if not _json_safe(item):
					return false
			return true
		TYPE_DICTIONARY:
			for key in value:
				if (not key is String and not key is StringName) or not _json_safe(value[key]):
					return false
			return true
	return false

static func _validate_state(state: Dictionary) -> Array:
	if not _json_safe(state):
		return ["NON_JSON_OR_UNSAFE_STATE"]
	if state.get("schema_version") != SCHEMA_VERSION:
		return ["UNSUPPORTED_SCHEMA_VERSION: 지원하지 않는 저장 형식입니다. 원본은 유지됩니다."]
	if state.get("rules_version") != RULES_VERSION:
		return ["UNSUPPORTED_RULES_VERSION: 저장된 게임 규칙이 현재 버전과 다릅니다. 원본은 유지됩니다."]
	if state.get("phase") not in ["Planning", "Report", "Committed"]:
		return ["PARTIAL_TURN_NOT_SAVEABLE"]
	var errors: Array = Ledger.validate(state)
	if not errors.is_empty():
		return errors
	var core: Variant = load("res://core/economy.gd")
	if core == null:
		return ["ECONOMY_VALIDATOR_UNAVAILABLE"]
	errors.append_array(core.validate(state))
	return errors

static func _legacy_compatibility(state: Dictionary) -> Dictionary:
	# Verify the original ledger with the original currency codes allowed. Never
	# feed an old state to the current USD-only economic validator first.
	if not _json_safe(state): return _fail("SAVE_VALIDATION: NON_JSON_OR_UNSAFE_STATE")
	var errors: Array=Ledger.validate(state,true)
	if not errors.is_empty(): return _fail("LEGACY_SAVE_LEDGER_INVALID: "+"; ".join(errors))
	var known: bool=state.get("content_hash","") in LEGACY_CONTENT_HASHES
	var notice: String="이전 저장은 달러·실제 물류 단위·생산식 변경으로 안전하게 자동 변환할 수 없습니다. 원본은 그대로 유지됩니다. 새 게임을 시작해 주세요."
	var code: String="LEGACY_UNITS_REQUIRE_NEW_GAME"
	if not known:
		code="LEGACY_CONTENT_UNRECOGNIZED"
		notice="현재 버전에서 확인할 수 없는 이전 콘텐츠의 저장입니다. 원본은 그대로 유지됩니다. 원래 버전으로 열거나 새 게임을 시작해 주세요."
	var result: Dictionary=_fail(code+": "+notice)
	result.merge({"legacy_save":true,"requires_new_game":true,"original_preserved":true,"compatibility_code":code,"compatibility_note":notice,"source_schema_version":state.schema_version,"source_rules_version":state.rules_version,"source_content_known":known},true)
	return result

static func _read_path(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return _fail("SAVE_NOT_FOUND")
	var file: FileAccess = FileAccess.open(path, FileAccess.READ)
	if file == null:
		return _fail("SAVE_OPEN_FAILED")
	if file.get_length() <= 0 or file.get_length() > MAX_SAVE_BYTES:
		file.close()
		return _fail("SAVE_SIZE_INVALID")
	var raw: String = file.get_as_text()
	file.close()
	var parser: JSON = JSON.new()
	if parser.parse(raw) != OK or not parser.data is Dictionary:
		return _fail("SAVE_JSON_INVALID")
	var envelope: Dictionary = _normalize(parser.data)
	if envelope.get("format_version") != FORMAT_VERSION or not envelope.get("state") is Dictionary:
		return _fail("SAVE_FORMAT_UNSUPPORTED")
	var state: Dictionary = envelope.state
	if envelope.get("checksum") != hash_state(state):
		return _fail("SAVE_CHECKSUM_MISMATCH")
	if envelope.get("schema_version") != state.get("schema_version") or envelope.get("rules_version") != state.get("rules_version"):
		return _fail("SAVE_VERSION_MISMATCH")
	if envelope.get("content_hash", "") != state.get("content_hash", ""):
		return _fail("SAVE_CONTENT_MISMATCH")
	if state.get("schema_version")==LEGACY_SCHEMA_VERSION and state.get("rules_version")==LEGACY_RULES_VERSION:
		var legacy: Dictionary=_legacy_compatibility(state)
		legacy["original_path"]=path
		return legacy
	var errors: Array = _validate_state(state)
	if not errors.is_empty():
		return _fail("SAVE_VALIDATION: " + "; ".join(errors))
	return {"ok": true, "error": "", "state": state, "recovered": false}

static func save_world(state: Dictionary, slot: String = "autosave") -> Dictionary:
	if not _valid_slot(slot):
		return _fail("INVALID_SAVE_SLOT")
	var errors: Array = _validate_state(state)
	if not errors.is_empty():
		return _fail("SAVE_VALIDATION: " + "; ".join(errors))
	var absolute_dir: String = ProjectSettings.globalize_path(SAVE_DIR)
	var mkdir_error: Error = DirAccess.make_dir_recursive_absolute(absolute_dir)
	if mkdir_error != OK:
		return _fail("SAVE_DIRECTORY_FAILED: " + str(mkdir_error))
	var path: String = SAVE_DIR + "/" + slot + ".json"
	var temporary: String = path + ".tmp"
	var backup: String = path + ".bak"
	var backup_temporary: String = backup + ".tmp"
	var envelope: Dictionary = {
		"format_version": FORMAT_VERSION,
		"schema_version": state.schema_version,
		"rules_version": state.rules_version,
		"content_hash": state.get("content_hash", ""),
		"checksum": hash_state(state),
		"state": state
	}
	var contents: String = canonical(envelope)
	if contents.to_utf8_buffer().size() > MAX_SAVE_BYTES:
		return _fail("SAVE_SIZE_INVALID")
	var file: FileAccess = FileAccess.open(temporary, FileAccess.WRITE)
	if file == null:
		return _fail("SAVE_WRITE_FAILED")
	file.store_string(contents)
	file.flush()
	var write_error: Error = file.get_error()
	file.close()
	if write_error != OK:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		return _fail("SAVE_FLUSH_FAILED: " + str(write_error))
	var checked: Dictionary = _read_path(temporary)
	if not checked.ok:
		DirAccess.remove_absolute(ProjectSettings.globalize_path(temporary))
		return _fail("SAVE_TEMP_VERIFY_FAILED: " + str(checked.error))
	# Never replace a known-good backup with a corrupt or partial latest file.
	var current: Dictionary = _read_path(path)
	if not current.ok and FileAccess.file_exists(path):
		var digest: String=FileAccess.get_sha256(path)
		if digest.is_empty(): return _fail("PREVIOUS_FORMAT_UNREADABLE")
		var preserved: String=path+".preserved-"+digest
		if not FileAccess.file_exists(preserved):
			var keep_error: Error=DirAccess.copy_absolute(ProjectSettings.globalize_path(path),ProjectSettings.globalize_path(preserved))
			if keep_error!=OK or FileAccess.get_sha256(preserved)!=digest: return _fail("PREVIOUS_FORMAT_BACKUP_FAILED")
	if current.ok:
		var copied: Error = DirAccess.copy_absolute(ProjectSettings.globalize_path(path), ProjectSettings.globalize_path(backup_temporary))
		if copied != OK or not _read_path(backup_temporary).ok:
			return _fail("SAVE_BACKUP_WRITE_FAILED")
		var backup_error: Error = DirAccess.rename_absolute(ProjectSettings.globalize_path(backup_temporary), ProjectSettings.globalize_path(backup))
		if backup_error != OK:
			return _fail("SAVE_BACKUP_RENAME_FAILED: " + str(backup_error))
	var replace_error: Error = DirAccess.rename_absolute(ProjectSettings.globalize_path(temporary), ProjectSettings.globalize_path(path))
	if replace_error != OK:
		return _fail("SAVE_COMMIT_FAILED: " + str(replace_error))
	return {"ok": true, "error": "", "slot": slot, "checksum": envelope.checksum, "recovered": false}

static func load_world(slot: String = "autosave") -> Dictionary:
	if not _valid_slot(slot):
		return _fail("INVALID_SAVE_SLOT")
	var path: String = SAVE_DIR + "/" + slot + ".json"
	var current: Dictionary = _read_path(path)
	if current.ok:
		return current
	var backup: Dictionary = _read_path(path + ".bak")
	if backup.ok:
		backup.recovered = true
		backup["recovery_reason"] = current.error
		return backup
	var failure: Dictionary=_fail(str(current.error) + " / BACKUP: " + str(backup.error))
	for key in ["legacy_save","requires_new_game","original_preserved","compatibility_code","compatibility_note","source_schema_version","source_rules_version","source_content_known","original_path"]:
		if current.has(key): failure[key]=current[key]
	return failure

static func list_slots() -> Array:
	var slots: Array = []
	var directory: DirAccess = DirAccess.open(SAVE_DIR)
	if directory == null:
		return slots
	var candidates: Dictionary = {}
	for filename in directory.get_files():
		if filename.ends_with(".json"):
			candidates[filename.trim_suffix(".json")] = true
		elif filename.ends_with(".json.bak"):
			candidates[filename.trim_suffix(".json.bak")] = true
	var names: Array = candidates.keys()
	names.sort()
	for slot in names:
		if not _valid_slot(str(slot)):
			continue
		var result: Dictionary = load_world(str(slot))
		var item: Dictionary = {"slot": slot, "ok": result.ok, "error": result.error, "recovered": result.recovered}
		for key in ["legacy_save","requires_new_game","compatibility_code","compatibility_note","source_schema_version","source_rules_version"]:
			if result.has(key): item[key]=result[key]
		if result.ok:
			item["planning_month"] = result.state.get("planning_month", 1)
			item["last_resolved_month"] = result.state.get("last_resolved_month", 0)
			item["player_id"] = result.state.get("player_id", "")
			item["rules_version"] = result.state.get("rules_version", "")
		slots.append(item)
	return slots

static func delete_slot(slot: String) -> Dictionary:
	if not _valid_slot(slot):
		return _fail("INVALID_SAVE_SLOT")
	for suffix in [".json", ".json.bak", ".json.tmp", ".json.bak.tmp"]:
		var path: String = ProjectSettings.globalize_path(SAVE_DIR + "/" + slot + str(suffix))
		if FileAccess.file_exists(path):
			var error: Error = DirAccess.remove_absolute(path)
			if error != OK:
				return _fail("SAVE_DELETE_FAILED: " + str(error))
	return {"ok": true, "error": ""}
