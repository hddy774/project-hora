extends RefCounted

const Economy = preload("res://core/economy.gd")
const Ledger = preload("res://core/ledger.gd")
const Store = preload("res://core/save_store.gd")
const Finance = preload("res://core/finance.gd")

static var checks: int = 0

static func run() -> Array:
	return _run(true)

static func run_quick() -> Array:
	return _run(false)

static func _run(include_long_observation: bool) -> Array:
	var failures: Array = []
	checks = 0
	_test_content_contract(failures)
	_test_numeric_hash_contract(failures)
	_test_initial_world(failures)
	_test_corrupted_state_rejected(failures)
	_test_command_authority(failures)
	_test_month_is_atomic_and_deterministic(failures)
	_test_queries_read_only(failures)
	_test_queue_order(failures)
	_test_inventory_shock(failures)
	_test_international_shipment(failures)
	if include_long_observation:
		_test_long_observation(failures)
	_test_construction(failures)
	_test_construction_shortage(failures)
	_test_cancellation_after_progress(failures)
	_test_construction_equipment(failures)
	_test_round_trip(failures)
	print("Economy assertions: %d (long observation: %s)" % [checks, "included" if include_long_observation else "skipped"])
	return failures

static func _check(condition: bool, label: String, failures: Array) -> void:
	checks += 1
	if not condition:
		failures.append(label)

static func _world(seed_value: int) -> Dictionary:
	return Economy.new_world(seed_value, {"cities_per_nation": 1, "expanded_industries": false})

static func _money_by_currency(s: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for id in s.accounts:
		var account: Dictionary = s.accounts[id]
		if account.kind == "cash" or (account.kind == "asset" and (":savings:" in str(id) or ":market-escrow:" in str(id))):
			var currency: String = str(account.currency)
			result[currency] = int(result.get(currency, 0)) + int(account.balance)
	return result

static func _check_money_changes(before: Dictionary, after: Dictionary, label: String, failures: Array) -> void:
	var expected: Dictionary = _money_by_currency(before)
	for index in range(before.events.size(), after.events.size()):
		var event: Dictionary = after.events[index]
		if event.type in ["money_creation", "money_destruction"]:
			var currency: String = str(event.get("currency", "USD"))
			expected[currency] = int(expected.get(currency, 0)) + int(event.get("amount", 0)) * (1 if event.type == "money_creation" else -1)
	_check(expected == _money_by_currency(after), label + ": deposits change only through recorded currency-specific credit issue/repayment", failures)
	_check(Finance.validate(after).is_empty(), label + ": banking assets, deposits, liabilities, and settlement counterparts match", failures)

static func _test_content_contract(failures: Array) -> void:
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string("res://data/experimental.json"))
	_check(parsed is Dictionary, "Experimental fixture is present and valid JSON", failures)
	if not parsed is Dictionary:
		return
	var fixture: Dictionary = parsed
	_check(Economy.validate_fixture(fixture).is_empty(), "Versioned experimental fixture satisfies its unit and definition contracts", failures)
	_check(Economy.fixture_status().get("ok", false), "Runtime loads the validated fixture", failures)
	var broken: Dictionary = fixture.duplicate(true)
	broken.parameters.erase("hourly_wage")
	_check(not Economy.validate_fixture(broken).is_empty(), "Missing economic parameters fail instead of using a silent default", failures)
	broken = fixture.duplicate(true)
	broken.products[0].erase("unit")
	_check(not Economy.validate_fixture(broken).is_empty(), "A product without a measurement unit is rejected", failures)
	broken = fixture.duplicate(true)
	broken.recipes[0].inputs["unknown_product"] = 1
	_check(not Economy.validate_fixture(broken).is_empty(), "Recipes cannot create references to undefined goods", failures)
	broken = fixture.duplicate(true)
	broken.products[0].price = 1.5
	_check(not Economy.validate_fixture(broken).is_empty(), "Prices cannot silently truncate fractional minimum currency units", failures)
	broken = fixture.duplicate(true)
	broken.buildings[0].erase("min_months")
	_check(not Economy.validate_fixture(broken).is_empty(), "Building definitions must specify a minimum construction duration", failures)
	var products: Array = Economy._product_data()
	products[0].price = 999999
	_check(Economy._product_data()[0].price != 999999, "Product queries cannot mutate the shared fixture", failures)
	var recipes: Array = Economy._recipe_data()
	recipes[0].inputs["flour"] = 999999
	_check(Economy._recipe_data()[0].inputs.get("flour", 0) != 999999, "Recipe queries cannot mutate shared input vectors", failures)
	var buildings: Array = Economy.building_types()
	buildings[0].wood = 999999
	_check(Economy.building_types()[0].wood != 999999, "Building queries cannot mutate shared construction definitions", failures)
	var world: Dictionary = _world(42)
	_check(world.content_hash == Economy._content_hash(), "Saved contract includes all domain definitions and economic parameters", failures)
	_check(str(world.content_hash).length() == 64, "Saved content contract has a SHA256 identity", failures)
	world.content_hash = (str(world.content_hash) + "changed balance contract").sha256_text()
	_check(not Economy.validate(world).is_empty(), "A world from another balance contract cannot silently resume", failures)

static func _test_numeric_hash_contract(failures: Array) -> void:
	var ordered: Dictionary = {"a": [2, 0.25], "z": 1, "nested": {"q": 3}}
	var reordered: Dictionary = {"nested": {"q": 3.0}, "z": 1.0, "a": [2.0, 0.25]}
	_check(Economy._canonical_json(ordered) == Economy._canonical_json(reordered), "Canonical economic serialization normalizes whole numbers and key order", failures)
	_check(Store.canonical(ordered) == Store.canonical(reordered), "Canonical save serialization normalizes whole numbers and key order", failures)
	_check(Economy._canonical_json(ordered) == Store.canonical(ordered), "Save and economic canonical serialization use the same numeric contract", failures)
	var precise: Dictionary = {"value": 0.12345678912345678, "limit": 9007199254740991}
	var encoded: String = Store.canonical(precise)
	var restored: Variant = JSON.parse_string(encoded)
	_check(restored is Dictionary and restored.value == precise.value and int(restored.limit) == int(precise.limit), "Canonical save round trip preserves fractional precision and safe integer limit", failures)
	var s: Dictionary = _world(42)
	var equivalent: Dictionary = s.duplicate(true)
	equivalent.schema_version = float(s.schema_version)
	equivalent.version = float(s.version)
	equivalent.planning_month = float(s.planning_month)
	_check(Economy.state_hash(s) == Economy.state_hash(equivalent), "JSON whole-number restoration cannot change a world's deterministic hash", failures)

static func _assert_world(s: Dictionary, label: String, failures: Array) -> void:
	var errors: Array = Economy.validate(s)
	_check(errors.is_empty(), "%s: invariant errors %s" % [label, str(errors)], failures)
	for lot in s.inventory:
		_check(int(lot.quantity) >= 0, "%s: negative stock %s" % [label, lot.id], failures)
		_check(int(lot.reserved) >= 0 and int(lot.reserved) <= int(lot.quantity), "%s: stock over-reserved %s" % [label, lot.id], failures)
	for account in s.accounts.values():
		if account.kind == "cash":
			_check(int(account.balance) >= 0, "%s: negative cash %s" % [label, account.owner], failures)

static func _test_initial_world(failures: Array) -> void:
	var first: Dictionary = _world(42)
	var same: Dictionary = _world(42)
	_check(first.continents.size() == 3, "World must have exactly three continents", failures)
	_check(first.planning_month == 1 and first.last_resolved_month == 0, "New world begins before its first monthly resolution", failures)
	_check(Economy.state_hash(first) == Economy.state_hash(same), "Same seed regenerates identical initial world", failures)
	_assert_world(first, "Initial world", failures)
	var owner_ids: Dictionary = {}
	for person in first.people:
		owner_ids[person.id] = true
	for company in first.companies:
		_check(not owner_ids.has(company.id), "Person and company IDs must remain distinct", failures)
		owner_ids[company.id] = true
	_check(first.people.size() >= first.companies.size(), "Every company has an actual modeled director", failures)
	for company in first.companies:
		var director_found: bool = false
		for person in first.people:
			if person.id == company.director_id:
				director_found = true
		_check(director_found, "Company references a real director: " + str(company.id), failures)
	for city in first.cities:
		var nation_found: bool = false
		for nation in first.nations:
			if nation.id == city.nation_id:
				nation_found = nation.continent_id == city.continent_id
		_check(nation_found, "City's nation and continent match: " + str(city.id), failures)

static func _test_corrupted_state_rejected(failures: Array) -> void:
	var pristine: Dictionary = _world(42)
	var corrupted: Dictionary = pristine.duplicate(true)
	corrupted.people = null
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects a malformed top-level collection before lookup", failures)
	corrupted = pristine.duplicate(true)
	corrupted.people[0] = null
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects a malformed collection item before lookup", failures)
	corrupted = pristine.duplicate(true)
	corrupted.version = 0.5
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects fractional versions without silently truncating", failures)
	corrupted = pristine.duplicate(true)
	corrupted.people[0].hours = {}
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects malformed labor hours before arithmetic", failures)
	corrupted = pristine.duplicate(true)
	corrupted.inventory[0].quantity = {}
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects malformed stock quantity before arithmetic", failures)
	corrupted = pristine.duplicate(true)
	corrupted.facilities[0].x = 0.5
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects fractional occupied tile coordinates", failures)
	corrupted = pristine.duplicate(true)
	corrupted.continents.pop_back()
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects a world without exactly three continents", failures)
	corrupted = pristine.duplicate(true)
	corrupted.people[1].id = corrupted.people[0].id
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects duplicate person IDs", failures)
	corrupted = pristine.duplicate(true)
	corrupted.inventory[0].quantity += 1
	_check(not Economy.validate(corrupted).is_empty(), "Validator detects stock that has no recorded source", failures)
	corrupted = pristine.duplicate(true)
	corrupted.inventory[0].accounting_city_id = "missing-city"
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects stock with an unknown accounting city", failures)
	corrupted = pristine.duplicate(true)
	corrupted.inventory[0].reserved = int(corrupted.inventory[0].quantity) + 1
	_check(not Economy.validate(corrupted).is_empty(), "Validator rejects a reservation exceeding its lot", failures)
	var before: String = Economy.state_hash(corrupted)
	var result: Dictionary = Economy.resolve_month(corrupted)
	_check(not result.get("ok", false), "An invalid monthly input is never committed", failures)
	_check(Economy.state_hash(corrupted) == before, "A failed month retains input state and clock", failures)

static func _policy(s: Dictionary, id_value: String, actor_id: String = "p-0", company_id: String = "co-0") -> Dictionary:
	return {"id": id_value, "actor_id": actor_id, "acting_company_id": company_id,
		"expected_version": s.version, "type": "set_policy", "payload": {"production_percent": 75, "price_percent": 110}}

static func _reject_unchanged(s: Dictionary, command: Dictionary, label: String, failures: Array) -> void:
	var before: String = Economy.state_hash(s)
	var result: Dictionary = Economy.queue_command(s, command)
	_check(not bool(result.get("ok", false)), label + " must be rejected", failures)
	_check(not str(result.get("error", "")).is_empty(), label + " must explain its rejection", failures)
	_check(Economy.state_hash(s) == before, label + " must not mutate world or reservations", failures)

static func _test_command_authority(failures: Array) -> void:
	var s: Dictionary = _world(42)
	_reject_unchanged(s, _policy(s, "unknown-actor", "missing-person"), "Unknown actor", failures)
	var stale: Dictionary = _policy(s, "stale-version")
	stale.expected_version = int(s.version) - 1
	_reject_unchanged(s, stale, "Stale state version", failures)
	var fractional_version: Dictionary = _policy(s, "fractional-version")
	fractional_version.expected_version = float(s.version) + 0.5
	_reject_unchanged(s, fractional_version, "Fractional state version", failures)
	var other_company: String = str(s.companies[1].id)
	_reject_unchanged(s, _policy(s, "wrong-authority", "p-0", other_company), "Other company without authority", failures)
	var unknown: Dictionary = _policy(s, "unknown-kind")
	unknown.type = "grant_free_money"
	_reject_unchanged(s, unknown, "Unknown action", failures)
	var fractional: Dictionary = _policy(s, "fractional-policy")
	fractional.payload.production_percent = 75.5
	_reject_unchanged(s, fractional, "Fractional value in integer policy field", failures)
	var fractional_tile: Dictionary = {"id": "fractional-tile", "actor_id": "p-0", "acting_company_id": "co-0",
		"expected_version": s.version, "type": "build", "payload": {"kind": "workshop", "x": 6.5, "y": 6, "city_id": "city-0"}}
	_reject_unchanged(s, fractional_tile, "Fractional tile coordinate", failures)
	var unknown_city: Dictionary = fractional_tile.duplicate(true)
	unknown_city.id = "unknown-city"
	unknown_city.payload.x = 6
	unknown_city.payload.city_id = "missing-city"
	_reject_unchanged(s, unknown_city, "Unknown construction city", failures)
	var wrong_phase: Dictionary = s.duplicate(true)
	wrong_phase.phase = "Resolving"
	_reject_unchanged(wrong_phase, _policy(wrong_phase, "mid-resolution"), "Command during locked monthly resolution", failures)
	var success: Dictionary = Economy.queue_command(s, _policy(s, "valid-policy"))
	_check(bool(success.get("ok", false)), "Director can submit own company policy", failures)
	_reject_unchanged(s, _policy(s, "valid-policy"), "Duplicate command ID", failures)

static func _test_month_is_atomic_and_deterministic(failures: Array) -> void:
	var s: Dictionary = _world(42)
	Economy.queue_command(s, _policy(s, "month-policy"))
	var before: String = Economy.state_hash(s)
	var money_snapshot: Dictionary = s.duplicate(true)
	var a: Dictionary = Economy.resolve_month(s)
	var b: Dictionary = Economy.resolve_month(s)
	_check(bool(a.get("ok", false)), "First month resolves: " + str(a.get("errors", [])), failures)
	_check(bool(b.get("ok", false)), "Repeated resolution of same immutable input succeeds", failures)
	_check(Economy.state_hash(s) == before, "Resolution leaves input snapshot and commands unchanged", failures)
	if not bool(a.get("ok", false)) or not bool(b.get("ok", false)):
		return
	_check(Economy.state_hash(a.state) == Economy.state_hash(b.state), "Identical input produces identical monthly state hash", failures)
	_check(a.state.last_resolved_month == 1 and a.state.planning_month == 2, "Monthly commit records resolved and next planning month together", failures)
	_check(a.state.version == int(s.version) + 1, "One resolution advances the state version exactly once", failures)
	_check_money_changes(money_snapshot, a.state, "Production and transfers", failures)
	_assert_world(a.state, "After first month", failures)
	_check(a.state.journal.size() > s.journal.size(), "Monthly economy generates real payment journal entries", failures)
	_check(a.state.events.size() > s.events.size(), "Monthly economy explains its changes with events", failures)

static func _test_queue_order(failures: Array) -> void:
	var a: Dictionary = _world(84)
	var b: Dictionary = _world(84)
	var first: Dictionary = _policy(a, "order-a")
	var second: Dictionary = _policy(a, "order-b", str(a.companies[1].director_id), str(a.companies[1].id))
	var aa: Dictionary = Economy.queue_command(a, first)
	var ab: Dictionary = Economy.queue_command(a, second)
	var bb: Dictionary = Economy.queue_command(b, second)
	var ba: Dictionary = Economy.queue_command(b, first)
	_check(aa.get("ok", false) and ab.get("ok", false) and bb.get("ok", false) and ba.get("ok", false), "Player and AI directors use same valid command path", failures)
	var ra: Dictionary = Economy.resolve_month(a)
	var rb: Dictionary = Economy.resolve_month(b)
	_check(ra.get("ok", false) and rb.get("ok", false), "Both independent command orders resolve", failures)
	if ra.get("ok", false) and rb.get("ok", false):
		_check(Economy.state_hash(ra.state) == Economy.state_hash(rb.state), "Independent plan edit order does not alter monthly allocation", failures)

static func _test_queries_read_only(failures: Array) -> void:
	var s: Dictionary = _world(42)
	var result: Dictionary = Economy.resolve_month(s)
	_check(result.get("ok", false), "Aggregation fixture resolves", failures)
	if not result.get("ok", false):
		return
	s = result.state
	var before: String = Economy.state_hash(s)
	var world: Dictionary = Economy.metrics(s)
	var city_totals: Dictionary = {"cash": 0, "population": 0, "employed": 0, "revenue": 0, "expenses": 0, "trades": 0, "gdp": 0, "real_gdp": 0, "gross_output": 0, "intermediate_consumption": 0}
	var product_totals: Dictionary = {}
	for city in s.cities:
		var actual: Dictionary = Economy.metrics(s, str(city.id))
		for field in city_totals:
			city_totals[field] = int(city_totals[field]) + int(actual[field])
		for product in actual.per_product:
			product_totals[product.product_id] = int(product_totals.get(product.product_id, 0)) + int(product.supply)
	for field in city_totals:
		_check(int(world[field]) == int(city_totals[field]), "World %s is exactly the sum of city accounts" % field, failures)
	for product in world.per_product:
		_check(int(product.supply) == int(product_totals[product.product_id]), "World stock is attributed once across cities: " + str(product.product_id), failures)
		_check(int(product.supply) == _stock_total(s, str(product.product_id)), "World stock includes warehouse and in-transit lots once: " + str(product.product_id), failures)
	for nation in s.nations:
		var local_gdp: int = 0
		for city in s.cities:
			if city.nation_id == nation.id: local_gdp += int(Economy.metrics(s, str(city.id)).gdp)
		_check(int(Economy.metrics(s, str(nation.id)).gdp) == local_gdp, "National value added is the sum of its real cities: " + str(nation.id), failures)
	_check(int(world.gdp) == int(world.gross_output) - int(world.intermediate_consumption), "World output less real intermediate inputs equals fixed-base-price value added", failures)
	_check(int(world.exports) == 0 and int(world.imports) == 0, "A closed complete world has no fictitious external exports or imports", failures)
	for continent in s.continents:
		Economy.metrics(s, str(continent.id))
	_check(Economy.state_hash(s) == before, "Opening world, continent, nation, or city views does not advance or mutate the economy", failures)

static func _stock_total(s: Dictionary, product_id: String = "") -> int:
	var total: int = 0
	for lot in s.inventory:
		if product_id.is_empty() or lot.product_id == product_id:
			total += int(lot.quantity)
	return total

static func _test_inventory_shock(failures: Array) -> void:
	var s: Dictionary = _world(42)
	var lot: Dictionary = {}
	for candidate in s.inventory:
		if int(candidate.quantity) > 0 and candidate.location != "shipment":
			lot = candidate
			break
	_check(not lot.is_empty(), "Opening world has actual inventory", failures)
	if lot.is_empty():
		return
	var initial_stock: int = _stock_total(s, str(lot.product_id))
	var money_snapshot: Dictionary = s.duplicate(true)
	var loss: Dictionary = Economy.adjust_inventory(s, str(lot.owner_id), str(lot.product_id), -1, str(lot.city_id), "verification_damage")
	_check(loss.get("ok", false), "Explicit inventory loss is accepted as an event", failures)
	_check(_stock_total(s, str(lot.product_id)) == initial_stock - 1, "A loss destroys exactly its actual quantity", failures)
	_check_money_changes(money_snapshot, s, "Inventory damage never creates compensation cash", failures)
	_assert_world(s, "After explicit inventory damage", failures)
	var before_reject: String = Economy.state_hash(s)
	var excessive_loss: Dictionary = Economy.adjust_inventory(s, str(lot.owner_id), str(lot.product_id), -1000000000, str(lot.city_id), "verification_impossible_damage")
	_check(not excessive_loss.get("ok", false), "Cannot destroy unavailable inventory", failures)
	_check(Economy.state_hash(s) == before_reject, "Rejected inventory loss has no partial source/sink event", failures)

static func _test_international_shipment(failures: Array) -> void:
	# Expanded supply chains include finite carriers, fuel and route facilities.
	var s: Dictionary = Economy.new_world(42)
	var money_snapshot: Dictionary = s.duplicate(true)
	# Remove local flour and pause local milling; a solvent bakery must use a
	# real overseas seller, shipment, and next-month delivery rather than an
	# immediate imaginary external market.
	for lot in s.inventory.duplicate(true):
		if lot.city_id == "city-0" and lot.product_id == "flour" and int(lot.quantity) > 0:
			var loss: Dictionary = Economy.adjust_inventory(s, str(lot.owner_id), "flour", -int(lot.quantity), "city-0", "verification_local_flour_loss")
			_check(loss.get("ok", false), "Local flour shock is recorded", failures)
	for facility in s.facilities:
		if facility.city_id != "city-0" or facility.kind != "mill":
			continue
		for company in s.companies:
			if company.id == facility.company_id:
				var pause: Dictionary = _policy(s, "pause-local-mill", str(company.director_id), str(company.id))
				pause.payload = {"production_percent": 0}
				_check(Economy.queue_command(s, pause).get("ok", false), "Local milling is paused through authorized command", failures)
	var result: Dictionary = Economy.resolve_month(s)
	_check(result.get("ok", false), "A local supply shortage can resolve through finite international trade", failures)
	if not result.get("ok", false):
		return
	s = result.state
	var tested_shipments: Array = []
	for shipment in s.shipments:
		if shipment.owner_id != "co-0" or shipment.product_id != "flour":
			continue
		tested_shipments.append(str(shipment.id))
		_check(shipment.status == "transit", "Purchased overseas flour remains in transit during departure month", failures)
		_check(shipment.from_city_id != shipment.to_city_id and shipment.to_city_id == "city-0", "Shipment has real distinct origin and destination cities", failures)
		_check(int(shipment.eta) > int(s.last_resolved_month), "Transport has an actual later arrival month", failures)
		for lot in s.inventory:
			if lot.id == shipment.lot_id:
				_check(lot.location == "shipment" and int(lot.quantity) == int(shipment.quantity), "Transit inventory is exactly the shipment's existing quantity", failures)
				_check(lot.owner_id == "co-0" and lot.accounting_city_id == "city-0", "Transit ownership and economic city attribution remain explicit", failures)
	_check(not tested_shipments.is_empty(), "Flour shortage uses an actual overseas supplier and finite shipment", failures)
	_check(Economy.available_quantity(s, "co-0", "flour", "city-0") == 0, "Undelivered flour cannot feed current-month production", failures)
	_check_money_changes(money_snapshot, s, "Goods and freight payments", failures)
	_assert_world(s, "Overseas shipment in transit", failures)
	result = Economy.resolve_month(s)
	_check(result.get("ok", false), "Following month can continue actual transport", failures)
	if not result.get("ok", false):
		return
	s = result.state
	var last_eta: int = int(s.last_resolved_month)
	for shipment in s.shipments:
		if str(shipment.id) in tested_shipments: last_eta = maxi(last_eta, int(shipment.eta))
	while int(s.last_resolved_month) < last_eta:
		result = Economy.resolve_month(s)
		_check(result.get("ok", false), "Finite carrier progresses toward its contractual ETA", failures)
		if not result.get("ok", false): return
		s = result.state
	for shipment in s.shipments:
		if str(shipment.id) in tested_shipments:
			_check(shipment.status == "delivered", "Original shipment arrives once at its contractual month", failures)
			for lot in s.inventory:
				if lot.id == shipment.lot_id:
					_check(int(lot.quantity) == 0, "Arrival removes the transit lot instead of duplicating its goods", failures)
	_assert_world(s, "Overseas shipment after arrival", failures)
	_check_money_changes(money_snapshot, s, "Arrival does not duplicate payment", failures)

static func _test_long_observation(failures: Array) -> void:
	for seed_value in [7, 42, 99]:
		var started_ms: int = Time.get_ticks_msec()
		var s: Dictionary = _world(seed_value)
		var money_snapshot: Dictionary = s.duplicate(true)
		var saw_production: bool = false
		var saw_consumption: bool = false
		var late_production_months: int = 0
		var late_consumption_months: int = 0
		for month_index in range(24):
			money_snapshot = s.duplicate(true)
			var result: Dictionary = Economy.resolve_month(s)
			_check(bool(result.get("ok", false)), "No-intervention seed %d month %d resolves: %s" % [seed_value, month_index + 1, str(result.get("errors", []))], failures)
			if not bool(result.get("ok", false)):
				break
			s = result.state
			_assert_world(s, "Seed %d month %d" % [seed_value, month_index + 1], failures)
			_check_money_changes(money_snapshot, s, "No-intervention month %d seed %d" % [month_index + 1, seed_value], failures)
			var actual: Dictionary = Economy.metrics(s)
			saw_production = saw_production or int(actual.get("production", 0)) > 0
			saw_consumption = saw_consumption or int(actual.get("consumption", 0)) > 0
			if month_index >= 18:
				late_production_months += 1 if int(actual.get("production", 0)) > 0 else 0
				late_consumption_months += 1 if int(actual.get("consumption", 0)) > 0 else 0
		_check(saw_production, "Autonomous economy produces goods for seed %d" % seed_value, failures)
		_check(saw_consumption, "Autonomous households consume actual goods for seed %d" % seed_value, failures)
		_check(late_production_months > 0, "Production continues beyond opening buffers for seed %d" % seed_value, failures)
		_check(late_consumption_months > 0, "Household consumption continues in months 19-24 for seed %d" % seed_value, failures)
		print("No intervention seed=%d months=%d elapsed_ms=%d late_production_months=%d late_consumption_months=%d" % [seed_value, s.last_resolved_month, Time.get_ticks_msec() - started_ms, late_production_months, late_consumption_months])

static func _test_construction(failures: Array) -> void:
	var s: Dictionary = _world(42)
	var initial_facilities: int = s.facilities.size()
	var command: Dictionary = {"id": "build-workshop", "actor_id": "p-0", "acting_company_id": "co-0",
		"expected_version": s.version, "type": "build", "payload": {"kind": "workshop", "x": 6, "y": 6, "city_id": "city-0"}}
	var queued: Dictionary = Economy.queue_command(s, command)
	_check(queued.get("ok", false), "Build on a clear road-connected tile can be planned: " + str(queued.get("error", "")), failures)
	_check(s.facilities.size() == initial_facilities and s.projects.is_empty(), "Planning a build never creates a completed facility or spends materials", failures)
	if not queued.get("ok", false):
		return
	var overlap: Dictionary = command.duplicate(true)
	overlap.id = "overlap-workshop"
	_reject_unchanged(s, overlap, "Overlapping planned construction", failures)
	var first: Dictionary = Economy.resolve_month(s)
	_check(first.get("ok", false), "First construction month resolves: " + str(first.get("errors", [])), failures)
	if not first.get("ok", false):
		return
	s = first.state
	_check(s.projects.size() == 1, "A construction command creates one persistent project", failures)
	if s.projects.is_empty():
		return
	var project_id: String = str(s.projects[0].id)
	var min_months: int = int(s.projects[0].min_months)
	_check(min_months > 1, "Experimental facility enforces an actual minimum construction duration", failures)
	_check(s.facilities.size() == initial_facilities, "Construction cannot become an operating facility in its first month", failures)
	_check(int(s.projects[0].progress) < int(s.projects[0].required_work), "First-month work is below total required work", failures)
	var finished: bool = false
	for later_month in range(12):
		var result: Dictionary = Economy.resolve_month(s)
		_check(result.get("ok", false), "Construction follow-up month resolves", failures)
		if not result.get("ok", false):
			break
		s = result.state
		_assert_world(s, "Construction month %d" % (later_month + 2), failures)
		var project: Dictionary = {}
		for candidate in s.projects:
			if candidate.id == project_id:
				project = candidate
		if project.get("status", "") in ["complete", "completed", "ready"]:
			finished = true
			_check(int(project.months_worked) >= min_months, "Completion respects minimum elapsed construction months", failures)
			_check(int(project.progress) >= int(project.required_work), "Completion requires all actual construction work", failures)
			var completed_count: int = 0
			for item in s.facilities:
				if item.get("project_id", "") == project_id: completed_count += 1
			_check(completed_count == 1, "Completion creates exactly one project-linked facility", failures)
			for facility in s.facilities:
				if facility.get("project_id", "") == project_id:
					_check(facility.status != "operational", "Month-end completion cannot already be operating", failures)
					_check(int(facility.operational_month) > int(s.last_resolved_month), "New facility becomes eligible in a later planning month", failures)
					for event in s.events:
						if int(event.month) == int(s.last_resolved_month) and event.type == "production":
							_check(event.get("facility_id", "") != facility.id, "New facility produces no output during its completion month", failures)
			break
	_check(finished, "Resource-funded experimental project can finish through normal work", failures)

static func _test_construction_shortage(failures: Array) -> void:
	var s: Dictionary = _world(42)
	# A supply shock is recorded through the same inventory source/sink path.
	# It does not edit stock behind the validator's back.
	for lot in s.inventory.duplicate(true):
		if lot.product_id == "wood" and int(lot.quantity) > 0 and lot.location != "shipment":
			var loss: Dictionary = Economy.adjust_inventory(s, str(lot.owner_id), "wood", -int(lot.quantity), str(lot.city_id), "verification_wood_supply_loss")
			_check(loss.get("ok", false), "Wood shock removes only existing supply", failures)
	for facility in s.facilities:
		if facility.kind != "forestry":
			continue
		var company: Dictionary = {}
		for candidate in s.companies:
			if candidate.id == facility.company_id:
				company = candidate
		if company.is_empty():
			continue
		var stop: Dictionary = _policy(s, "pause-" + str(company.id), str(company.director_id), str(company.id))
		stop.payload = {"production_percent": 0, "price_percent": 100}
		var paused: Dictionary = Economy.queue_command(s, stop)
		_check(paused.get("ok", false), "Wood producer can legally pause production", failures)
	var build: Dictionary = {"id": "starved-build", "actor_id": "p-0", "acting_company_id": "co-0", "expected_version": s.version,
		"type": "build", "payload": {"kind": "workshop", "x": 6, "y": 6, "city_id": "city-0"}}
	var queued: Dictionary = Economy.queue_command(s, build)
	_check(queued.get("ok", false), "A legal construction plan can carry a future material shortage warning", failures)
	if not queued.get("ok", false):
		return
	var facility_count: int = s.facilities.size()
	for month_index in range(3):
		var result: Dictionary = Economy.resolve_month(s)
		_check(result.get("ok", false), "A shortage is an economic result, not a failed simulation", failures)
		if not result.get("ok", false):
			return
		s = result.state
		_check(s.facilities.size() == facility_count, "No construction completes without its required wood", failures)
		_check(_stock_total(s, "wood") == 0, "No hidden wood top-up during a shortage", failures)
		_assert_world(s, "Construction wood shortage %d" % (month_index + 1), failures)
	_check(not s.projects.is_empty(), "Delayed project remains in the world", failures)
	if not s.projects.is_empty():
		var project: Dictionary = s.projects[0]
		_check(int(project.progress) < int(project.required_work), "Material shortage constrains actual construction work", failures)
		var cancel: Dictionary = {"id": "cancel-starved-build", "actor_id": "p-0", "acting_company_id": "co-0", "expected_version": s.version,
			"type": "cancel_project", "payload": {"project_id": project.id}}
		var canceled: Dictionary = Economy.queue_command(s, cancel)
		_check(canceled.get("ok", false), "Director can request cancellation of own delayed project", failures)
		var final_result: Dictionary = Economy.resolve_month(s)
		_check(final_result.get("ok", false), "Cancellation resolves without inventory or labor duplication", failures)
		if final_result.get("ok", false):
			_assert_world(final_result.state, "Canceled delayed project", failures)

static func _test_cancellation_after_progress(failures: Array) -> void:
	var s: Dictionary = _world(42)
	var command: Dictionary = {"id": "cancel-after-work", "actor_id": "p-0", "acting_company_id": "co-0", "expected_version": s.version, "type": "build", "payload": {"kind": "workshop", "x": 6, "y": 6, "city_id": "city-0"}}
	_check(Economy.queue_command(s, command).get("ok", false), "Funded cancellation fixture queues through the ordinary build validator", failures)
	var result: Dictionary = Economy.resolve_month(s)
	_check(result.get("ok", false), "Cancellation fixture performs a real first month of construction", failures)
	if not result.get("ok", false): return
	s = result.state
	var project: Dictionary = Economy._find(s.projects, "project-cancel-after-work")
	_check(int(project.get("progress", 0)) > 0 and int(project.get("materials_used", {}).get("wood", 0)) > 0, "Cancellation follows actual labor and consumed construction materials", failures)
	var wood_before: int = Economy.available_quantity(s, "co-0", "wood", "city-0")
	var stone_before: int = Economy.available_quantity(s, "co-0", "stone", "city-0")
	var spent_before: int = int(project.get("spent", 0))
	var before_work: int = int(project.get("progress", 0))
	command = {"id": "cancel-consumed-materials", "actor_id": "p-0", "acting_company_id": "co-0", "expected_version": s.version, "type": "cancel_project", "payload": {"project_id": project.id}}
	_check(Economy.queue_command(s, command).get("ok", false), "Authorized cancellation of an active project queues", failures)
	result = Economy.resolve_month(s)
	_check(result.get("ok", false), "Already-paid work can be canceled without breaking asset accounts", failures)
	if not result.get("ok", false): return
	s = result.state
	project = Economy._find(s.projects, "project-cancel-after-work")
	_check(project.status == "cancelled" and int(project.progress) == before_work and int(project.spent) == spent_before, "Canceled project preserves actual sunk work and payments", failures)
	_check(Economy.available_quantity(s, "co-0", "wood", "city-0") == wood_before and Economy.available_quantity(s, "co-0", "stone", "city-0") == stone_before, "Cancellation never recreates consumed wood or stone as a false refund", failures)
	_check(Economy._find(s.facilities, "facility-project-cancel-after-work").is_empty(), "Canceled unfinished construction never leaves an operating building", failures)
	_assert_world(s, "Cancellation after consumed work", failures)

static func _test_construction_equipment(failures: Array) -> void:
	var s: Dictionary = _world(42)
	for lot in s.inventory.duplicate(true):
		if lot.product_id == "machinery" and int(lot.quantity) > 0:
			_check(Economy.adjust_inventory(s, str(lot.owner_id), "machinery", -int(lot.quantity), str(lot.city_id), "verification_equipment_loss").get("ok", false), "Actual construction equipment loss has a recorded source/sink", failures)
	var command: Dictionary = {"id": "equipment-starved-project", "actor_id": "p-0", "acting_company_id": "co-0", "expected_version": s.version, "type": "build", "payload": {"kind": "workshop", "city_id": "city-0", "x": 6, "y": 6}}
	_check(Economy.queue_command(s, command).get("ok", false), "A legal project may be planned despite future equipment scarcity", failures)
	for month in range(3):
		var result: Dictionary = Economy.resolve_month(s)
		_check(result.get("ok", false), "Equipment scarcity remains an explainable economic delay", failures)
		if not result.get("ok", false): return
		s = result.state
		_check(_stock_total(s, "machinery") == 0, "No hidden construction machinery is replenished", failures)
		var project: Dictionary = Economy._find(s.projects, "project-equipment-starved-project")
		_check(not project.is_empty() and int(project.progress) == 0 and project.status == "active", "Without actual equipment no construction work or completion is fabricated", failures)
		_check(not s.events.any(func(event): return event.type == "construction_work" and event.get("project_id", "") == "project-equipment-starved-project"), "No equipment-free construction wages or labor are recorded", failures)
		_assert_world(s, "Equipment shortage month %d" % (month + 1), failures)

static func _test_round_trip(failures: Array) -> void:
	var s: Dictionary = _world(42)
	var queued: Dictionary = Economy.queue_command(s, _policy(s, "saved-policy"))
	_check(queued.get("ok", false), "Save fixture contains a valid unexecuted command", failures)
	var build: Dictionary = {"id": "saved-build", "actor_id": "p-0", "acting_company_id": "co-0", "expected_version": s.version,
		"type": "build", "payload": {"kind": "workshop", "x": 6, "y": 6, "city_id": "city-0"}}
	_check(Economy.queue_command(s, build).get("ok", false), "Save fixture includes pending construction with material requirements", failures)
	var before: String = Economy.state_hash(s)
	var save_result: Dictionary = Store.save_world(s, "verification_economy")
	_check(save_result.get("ok", false), "World can be saved with pending commands: " + str(save_result.get("error", "")), failures)
	var loaded: Dictionary = Store.load_world("verification_economy")
	_check(loaded.get("ok", false), "Saved world can be loaded: " + str(loaded.get("error", "")), failures)
	if loaded.get("ok", false):
		_check(Economy.state_hash(loaded.state) == before, "Saving and loading preserves complete world and pending commands", failures)
		_check(loaded.state.planning_month == 1 and loaded.state.last_resolved_month == 0, "Loading never advances simulation time", failures)
		var a: Dictionary = Economy.resolve_month(s)
		var b: Dictionary = Economy.resolve_month(loaded.state)
		if a.get("ok", false) and b.get("ok", false):
			_check(Economy.state_hash(a.state) == Economy.state_hash(b.state), "Save round trip preserves next month results", failures)
			var committed_save: Dictionary = Store.save_world(a.state, "verification_economy")
			var committed_load: Dictionary = Store.load_world("verification_economy")
			_check(committed_save.get("ok", false) and committed_load.get("ok", false), "Committed monthly result and report can be reloaded", failures)
			if committed_load.get("ok", false):
				_check(Economy.state_hash(committed_load.state) == Economy.state_hash(a.state), "Report reload does not replay its month's payments or production", failures)
				_check(committed_load.state.last_resolved_month == 1 and committed_load.state.planning_month == 2, "Reload preserves the next month and last resolved month atomically", failures)
				var next_original: Dictionary = Economy.resolve_month(a.state)
				var next_restored: Dictionary = Economy.resolve_month(committed_load.state)
				_check(next_original.get("ok", false) and next_restored.get("ok", false), "Ongoing project can resolve after committed save reload", failures)
				if next_original.get("ok", false) and next_restored.get("ok", false):
					_check(Economy.state_hash(next_original.state) == Economy.state_hash(next_restored.state), "Restored project material-key order does not alter subsequent construction or economics", failures)
		else:
			_check(false, "Both original and reloaded world must resolve", failures)
	Store.delete_slot("verification_economy")
