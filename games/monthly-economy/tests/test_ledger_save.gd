extends RefCounted

const Ledger = preload("res://core/ledger.gd")
const Store = preload("res://core/save_store.gd")

static func _expect(failures: Array, condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)

static func run() -> Array:
	var failures: Array = []
	var s: Dictionary = {"accounts": {}, "journal": []}
	_expect(failures, Ledger.opening(s, "alice", 1200).ok, "opening Alice")
	_expect(failures, Ledger.opening(s, "bob", 800).ok, "opening Bob")
	_expect(failures, Ledger.transfer(s, "sale-1", "alice", "bob", 300, "SALE", 1).ok, "transfer posts")
	_expect(failures, Ledger.balance(s, "alice:cash") == 900 and Ledger.balance(s, "bob:cash") == 1100, "transfer changes both owners once")
	_expect(failures, Ledger.balance(s, "alice:expense") == 300 and Ledger.balance(s, "bob:revenue") == -300, "cash transfer has opposite expense and revenue")
	_expect(failures, Ledger.validate(s).is_empty(), "balanced ledger reconstructs")
	_expect(failures, Ledger.opening(s, "new-owner", 100).ok and Ledger.validate(s).is_empty(), "opening after payments preserves journal index")
	var before: String = Store.hash_state(s)
	_expect(failures, not Ledger.transfer(s, "sale-1", "alice", "bob", 300, "SALE", 1).ok, "duplicate journal rejected")
	_expect(failures, Store.hash_state(s) == before, "duplicate journal leaves state unchanged")
	_expect(failures, not Ledger.transfer(s, "no-cash", "alice", "bob", 901, "SALE", 1).ok, "insufficient cash rejected")
	_expect(failures, Store.hash_state(s) == before, "rejected cash transfer atomic")
	_expect(failures, not Ledger.transfer(s, "fraction", "alice", "bob", 1.5, "SALE", 1).ok, "fractional currency rejected")
	_expect(failures, not Ledger.opening(s, "new-owner", 1.5).ok, "fractional opening rejected")
	_expect(failures, Store.hash_state(s) == before, "fractional inputs leave ledger unchanged")
	_expect(failures, not Ledger.post(s, "unbalanced", [
		{"account": "alice:cash", "debit": 1, "credit": 0},
		{"account": "bob:cash", "debit": 0, "credit": 1}
	], "INVALID", 1).ok, "cross-owner balanced cash alone rejected")
	_expect(failures, Store.hash_state(s) == before, "invalid owner posting atomic")
	var tampered: Dictionary = s.duplicate(true)
	tampered.accounts["alice:cash"].balance += 1
	_expect(failures, not Ledger.validate(tampered).is_empty(), "cached balance tamper detected")
	tampered = s.duplicate(true)
	tampered.journal.append(tampered.journal[0].duplicate(true))
	_expect(failures, not Ledger.validate(tampered).is_empty(), "duplicate history detected")
	var foreign: Dictionary = s.duplicate(true)
	Ledger.open_account(foreign, "carol:cash", "carol", "cash", "EUR")
	Ledger.open_account(foreign, "carol:equity", "carol", "equity", "EUR")
	_expect(failures, Ledger.post(foreign, "eur-opening", [
		{"account": "carol:cash", "debit": 100, "credit": 0},
		{"account": "carol:equity", "debit": 0, "credit": 100}
	], "OPENING", 0).ok, "separate currency opening")
	_expect(failures, not Ledger.transfer(foreign, "no-fx", "alice", "carol", 10, "INVALID_FX", 1).ok, "unfunded currency conversion rejected")
	_expect(failures, Ledger.validate(foreign).is_empty(), "currency ledgers independently reconstruct")
	var partial: Dictionary = {"accounts": {}, "journal": []}
	Ledger.opening(partial, "funded", 200)
	Ledger.open_account(partial, "recipient:cash", "recipient", "cash")
	Ledger.open_account(partial, "recipient:equity", "recipient", "equity")
	_expect(failures, Ledger.transfer(partial, "new-accounts", "funded", "recipient", 10, "SALE", 1).ok and Ledger.validate(partial).is_empty(), "transfer initializes missing income accounts without stale journal index")
	_expect(failures, Store.canonical({"z": 1, "a": [2, 1]}) == Store.canonical({"a": [2.0, 1.0], "z": 1.0}), "canonical key order and numeric restoration")
	var precise: float = 0.12345678901234567
	var precise_restored: Dictionary = Store._normalize(JSON.parse_string(Store.canonical({"fraction": precise})))
	_expect(failures, precise_restored.fraction == precise, "non-currency floating-point state retains full precision")
	_expect(failures, not Store.load_world("../unsafe").ok, "save slot traversal rejected")

	var core: Variant = load("res://core/economy.gd")
	if core == null:
		failures.append("economy unavailable for save integration")
		return failures
	var world: Dictionary = core.new_world(9283)
	var slot: String = "__test_ledger_save"
	Store.delete_slot(slot)
	var saved: Dictionary = Store.save_world(world, slot)
	_expect(failures, saved.ok, "save initial valid world: " + str(saved.error))
	if not saved.ok:
		return failures
	var loaded: Dictionary = Store.load_world(slot)
	_expect(failures, loaded.ok and not loaded.recovered, "load latest verified snapshot")
	_expect(failures, loaded.ok and Store.hash_state(loaded.state) == Store.hash_state(world), "save/load preserves full state and command version")
	var resolved: Dictionary = core.resolve_month(world)
	_expect(failures, resolved.ok, "save test world resolves")
	if resolved.ok:
		var next: Dictionary = resolved.state
		saved = Store.save_world(next, slot)
		_expect(failures, saved.ok, "save next month with previous snapshot backup")
		loaded = Store.load_world(slot)
		_expect(failures, loaded.ok and Store.hash_state(loaded.state) == Store.hash_state(next), "reload resolved state without double settlement")
		var invalid: Dictionary = next.duplicate(true)
		invalid.accounts[invalid.accounts.keys()[0]].balance += 1
		_expect(failures, not Store.save_world(invalid, slot).ok, "reject invalid state before overwriting valid save")
		loaded = Store.load_world(slot)
		_expect(failures, loaded.ok and Store.hash_state(loaded.state) == Store.hash_state(next), "invalid save attempt preserves latest good file")
		var path: String = "user://saves/" + slot + ".json"
		var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
		file.store_string("{\"truncated\":")
		file.close()
		loaded = Store.load_world(slot)
		_expect(failures, loaded.ok and loaded.recovered, "corrupt latest recovers previous verified snapshot")
		_expect(failures, loaded.ok and Store.hash_state(loaded.state) == Store.hash_state(world), "backup recovery exact previous month")
		# A corrupt latest must not overwrite the only good recovery copy.
		_expect(failures, Store.save_world(next, slot).ok, "save after corrupt latest")
		file = FileAccess.open(path, FileAccess.WRITE)
		file.store_string("broken again")
		file.close()
		loaded = Store.load_world(slot)
		_expect(failures, loaded.ok and loaded.recovered and Store.hash_state(loaded.state) == Store.hash_state(world), "corrupt latest never replaces valid backup")
	Store.delete_slot(slot)
	return failures
