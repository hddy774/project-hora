extends RefCounted
## End-to-end module contracts through the same versioned command/turn API.
const Economy = preload("res://core/economy.gd")
const Finance = preload("res://core/finance.gd")
const Life = preload("res://core/lifecycle.gd")
const Public = preload("res://core/public_sector.gd")
const Industry = preload("res://core/industry.gd")
const Ledger = preload("res://core/ledger.gd")
static var checks: int = 0
static var serial: int = 0

static func _check(value: bool, message: String, failures: Array) -> void:
	checks += 1
	if not value: failures.append(message)

static func _compact() -> Dictionary:
	return Economy.new_world(42, {"cities_per_nation": 1, "expanded_industries": false})

static func _find(rows: Array, id: String) -> Dictionary:
	for row in rows:
		if row.get("id", "") == id: return row
	return {}

static func _queue(s: Dictionary, type: String, payload: Dictionary, failures: Array, actor: String = "p-0", owner: String = "co-0") -> bool:
	serial += 1
	var c: Dictionary = {"id": "integration-%d" % serial, "actor_id": actor, "acting_company_id": owner, "expected_version": s.version, "type": type, "payload": payload}
	var before: String = Economy.state_hash(s)
	var result: Dictionary = Economy.queue_command(s, c)
	_check(result.get("ok", false), "Shared command API accepts %s: %s" % [type, result.get("error", "")], failures)
	if not result.get("ok", false):
		_check(Economy.state_hash(s) == before, "Rejected %s has no partial effect" % type, failures)
	return bool(result.get("ok", false))

static func _advance(s: Dictionary, failures: Array, label: String) -> Dictionary:
	var before: String = Economy.state_hash(s)
	var result: Dictionary = Economy.resolve_month(s)
	_check(result.get("ok", false), label + " resolves: " + str(result.get("errors", [])), failures)
	_check(Economy.state_hash(s) == before, label + " preserves immutable input", failures)
	if not result.get("ok", false): return s
	var next: Dictionary = result.state
	_check(Economy.validate(next).is_empty(), label + " validates every linked module", failures)
	_check(int(next.last_resolved_month) == int(s.planning_month) and int(next.planning_month) == int(s.planning_month) + 1, label + " commits one calendar month", failures)
	return next

static func run() -> Array:
	return _run(false)

static func run_quick() -> Array:
	return _run(true)

static func _run(quick: bool) -> Array:
	checks = 0
	serial = 0
	var failures: Array = []
	_test_complete_world_contract(failures)
	_test_registered_ai_commands(failures)
	_test_own_budget_reservations(failures)
	_test_reservation_cache_isolation(failures)
	_test_contractual_payroll(failures)
	_test_credit_and_owner_separation(failures)
	_test_career_and_startup(failures)
	_test_public_contract_and_privacy(failures)
	_test_neutral_benchmark(failures, quick)
	print("Integrated v2 assertions: %d" % checks)
	return failures

static func _test_complete_world_contract(failures: Array) -> void:
	var s: Dictionary = Economy.new_world(81)
	_check(Economy.validate(s).is_empty(), "Default full-industry opening satisfies all invariants", failures)
	_check(s.continents.size() == 3, "Full world retains exactly three continents", failures)
	for nation in s.nations:
		_check(s.cities.filter(func(city): return city.nation_id == nation.id).size() >= 2, "Nation has actual domestic cities: " + str(nation.id), failures)
	var kinds: Dictionary = {}
	for facility in s.facilities: kinds[facility.kind] = true
	for building in Economy.building_types():
		_check(kinds.has(building.id), "Catalog industry has an actual initial facility: " + str(building.id), failures)
	for product in s.products:
		_check(not str(product.unit).is_empty(), "Every product keeps its own measurement unit: " + str(product.id), failures)
		if Industry.is_service(s, str(product.id)):
			_check(s.inventory.filter(func(lot): return lot.product_id == product.id and int(lot.quantity) > 0).is_empty(), "Service is supplied as time capacity without stored goods: " + str(product.id), failures)
	for continent in s.continents:
		_check(not continent.has("inventory") and not continent.has("cash"), "Continent has no duplicated goods or buying wallet", failures)
	var before: String = Economy.state_hash(s)
	Public.summary(s, "p-0")
	Finance.summary(s, "co-0")
	Industry.summary(s, "co-0")
	Life.summary(s, "p-0")
	_check(Economy.state_hash(s) == before, "Cross-module summaries do not mutate economic state", failures)

static func _test_registered_ai_commands(failures: Array) -> void:
	var s: Dictionary = _compact()
	var registry: Dictionary = {}
	for descriptor in Economy.command_descriptors():
		_check(not registry.has(descriptor.type), "Action registry has no conflicting command types: " + str(descriptor.type), failures)
		registry[descriptor.type] = true
	for module in Economy.command_modules():
		for type in module.command_types(): _check(registry.has(type), "Shared action has a UI descriptor: " + str(type), failures)
		if not module.has_method("ai_commands"): continue
		var before: String = Economy.state_hash(s)
		var candidates: Array = module.ai_commands(s)
		_check(Economy.state_hash(s) == before, "AI candidate planning does not mutate real accounts or orders", failures)
		for candidate in candidates:
			var input: Dictionary = s.duplicate(true)
			_check(Economy.queue_command(input, candidate).get("ok", false), "AI action passes the ordinary command API: " + str(candidate.type), failures)

static func _test_own_budget_reservations(failures: Array) -> void:
	var s: Dictionary = _compact()
	var opening_cash: int = Economy.cash(s, "p-0")
	var amount: int = int(opening_cash * 2 / 3)
	if not _queue(s, "finance_deposit", {"bank_id": "bank-nation-0", "amount": amount}, failures, "p-0", ""): return
	var before: String = Economy.state_hash(s)
	var rejected: Dictionary = Economy.queue_command(s, {"id": "self-budget-duplicate", "actor_id": "p-0", "acting_company_id": "", "expected_version": s.version, "type": "finance_deposit", "payload": {"bank_id": "bank-nation-1", "amount": amount}})
	_check(not rejected.get("ok", false), "Two individually affordable plans cannot promise the same personal cash twice", failures)
	_check(Economy.state_hash(s) == before, "Budget overcommit rejection leaves accepted plans and balances unchanged", failures)
	_check(Economy.cash(s, "p-0") == opening_cash, "Pending reservations are distinct from executed deposit transfers", failures)

static func _test_reservation_cache_isolation(failures: Array) -> void:
	var a: Dictionary = _compact()
	var b: Dictionary = _compact()
	var first: Dictionary = {"id": "same-snapshot-same-id", "actor_id": "p-0", "acting_company_id": "", "expected_version": 0, "type": "finance_deposit", "payload": {"bank_id": "bank-nation-0", "amount": 50}}
	_check(Economy.queue_command(a, first).get("ok", false), "First world's finite reservation queues", failures)
	first.payload.amount = 130
	_check(Economy.queue_command(b, first).get("ok", false), "Equivalent-version world keeps its own different reservation", failures)
	var second: Dictionary = {"id": "second-cache-probe", "actor_id": "p-0", "acting_company_id": "", "expected_version": 0, "type": "finance_deposit", "payload": {"bank_id": "bank-nation-1", "amount": 60}}
	_check(Economy.queue_command(a, second).get("ok", false), "One world's remaining personal cash stays independently available", failures)
	var before: String = Economy.state_hash(b)
	_check(not Economy.queue_command(b, second).get("ok", false), "Reservation cache never transfers another snapshot's larger cash capacity", failures)
	_check(Economy.state_hash(b) == before, "Independent reservation rejection leaves the economic state unchanged", failures)
	# A malformed loaded/modified plan must be rechecked from the immutable input;
	# trusting a prior derived-cache entry may never bypass financial constraints.
	a.commands[0].payload.amount = 999999
	before = Economy.state_hash(a)
	var result: Dictionary = Economy.resolve_month(a)
	_check(not result.get("ok", false), "Turn revalidation catches a tampered accepted plan despite a previously valid reservation cache", failures)
	_check(Economy.state_hash(a) == before and int(a.planning_month) == 1, "Invalid cached plan cannot commit partial payments or a calendar advance", failures)

static func _test_contractual_payroll(failures: Array) -> void:
	var s: Dictionary = _compact()
	var idle: Dictionary = {}
	for contract in s.lifecycle.employment:
		if contract.role == "builder" and contract.status == "active":
			idle = contract.duplicate(true)
			break
	_check(not idle.is_empty(), "Payroll fixture has an actual idle construction employment contract", failures)
	if idle.is_empty(): return
	var cash_before: int = Economy.cash(s, str(idle.employer_id if idle.has("employer_id") else idle.company_id))
	var owner: String = str(idle.company_id)
	s = _advance(s, failures, "Guaranteed contract pay supports a household without fabricated construction work")
	var accrued: Dictionary = s.lifecycle.get("payroll_accruals", {}).get(str(idle.id) + "@1", {})
	_check(not accrued.is_empty(), "Monthly API accrues the same actual employment contract", failures)
	if accrued.is_empty(): return
	var expected: int = int(idle.guaranteed_hours) * int(idle.wage)
	_check(int(accrued.guaranteed_amount) == expected, "Guaranteed pay derives from the recorded contract's hours and wage", failures)
	_check(int(accrued.actual_paid) + int(accrued.top_up_due) == maxi(expected, int(accrued.actual_paid)), "Productive wages are credited before the contractual topup, never duplicated", failures)
	var claims: Array = s.lifecycle.wage_claims.filter(func(claim): return claim.employment_id == idle.id and int(claim.month) == 1)
	_check(claims.size() == 1 and int(claims[0].remaining) == 0 and int(claims[0].paid) == int(accrued.top_up_due), "Solvent idle employer settles exactly its actual recorded topup obligation", failures)
	_check(Economy.cash(s, owner) < cash_before, "Guaranteed wages use the employer's finite money", failures)
	var snapshot: String = Economy.state_hash(s)
	var again: Dictionary = Life.pay_contractual_wages(s, 1)
	_check(again.get("ok", false) and Economy.state_hash(s) == snapshot, "Repeated payroll/report access cannot pay the same salary twice", failures)

static func _test_credit_and_owner_separation(failures: Array) -> void:
	var s: Dictionary = _compact()
	var personal_before: int = Economy.cash(s, "p-0")
	var corporate_before: int = Economy.cash(s, "co-0")
	if not _queue(s, "finance_borrow", {"bank_id": "bank-nation-0", "amount": 100, "term_months": 12}, failures): return
	_check(Economy.cash(s, "co-0") == corporate_before and Economy.cash(s, "p-0") == personal_before, "Planning a loan creates neither deposit nor personal funds", failures)
	s = _advance(s, failures, "Bank credit with production and public finance")
	var loans: Array = s.finance.loans.filter(func(loan): return loan.borrower_id == "co-0" and int(loan.original_principal) == 100)
	_check(loans.size() == 1, "Corporate credit executes exactly once", failures)
	if loans.is_empty(): return
	var loan: Dictionary = loans[0]
	_check(Ledger.balance(s, "co-0:loan:" + str(loan.id)) == -int(loan.principal), "Corporate borrower carries the loan principal liability", failures)
	_check(s.events.any(func(event): return event.type == "money_creation" and event.get("loan_id", "") == loan.id and int(event.amount) == 100), "Deposit creation has an explicit monetary event", failures)
	if not _queue(s, "finance_repay", {"loan_id": loan.id, "amount": int(loan.principal)}, failures): return
	s = _advance(s, failures, "Principal repayment with the same shared turn")
	loan = _find(s.finance.loans, str(loan.id))
	_check(int(loan.principal) == 0, "Repayment extinguishes the real remaining principal", failures)
	_check(s.events.any(func(event): return event.type == "money_destruction" and event.get("loan_id", "") == loan.id and int(event.amount) == 100), "Repayment destroys only its matching deposit principal", failures)

static func _test_career_and_startup(failures: Array) -> void:
	var s: Dictionary = _compact()
	var company_cash: int = Economy.cash(s, "co-0")
	var shares: int = Life.shares_owned(s, "p-0", "co-0")
	if not _queue(s, "resign_job", {}, failures, "p-0", ""): return
	s = _advance(s, failures, "Person resigns while the independent company continues")
	_check(Life.shares_owned(s, "p-0", "co-0") == shares, "Leaving employment preserves separately owned shares", failures)
	_check(not _find(s.companies, "co-0").is_empty() and company_cash > 0, "Leaving a role preserves the company and its actual accounts", failures)
	var personal_before: int = Economy.cash(s, "p-0")
	if not _queue(s, "create_company", {"name": "통합 검증기업", "city_id": "city-0", "capital": 100, "kind": "bakery"}, failures, "p-0", ""): return
	_check(Economy.cash(s, "p-0") == personal_before, "Startup planning reserves intent before actual capital transfer", failures)
	s = _advance(s, failures, "Founder transfers real personal capital to a new legal entity")
	var created: Array = s.companies.filter(func(company): return company.name == "통합 검증기업")
	_check(created.size() == 1, "One startup command incorporates exactly one legal entity", failures)
	if not created.is_empty():
		_check(Life.shares_owned(s, "p-0", str(created[0].id)) > 0, "Founder receives actual recorded equity", failures)
		_check(created[0].id != "p-0", "Person and new company keep distinct ownership identities", failures)

static func _test_public_contract_and_privacy(failures: Array) -> void:
	var s: Dictionary = _compact()
	var g: Dictionary = _find(s.public.governments, "gov-nation-nation-0")
	var officer: String = str(_find(s.public.offices, str(g.office_id)).person_id)
	# Act as an existing official; viewpoint changes do not move property.
	s.player_id = officer
	if not _queue(s, "public_announce_procurement", {"government_id": g.id, "product_id": "food", "quantity": 4, "max_unit_price": 28, "deadline_month": 12, "military": true}, failures, officer, str(g.id)): return
	s = _advance(s, failures, "Funded military food procurement announcement")
	var contracts: Array = s.public.procurements.filter(func(contract): return contract.government_id == g.id and int(contract.quantity) == 4 and contract.product_id == "food")
	_check(not contracts.is_empty(), "Procurement announcement creates a finite funded contract", failures)
	if contracts.is_empty(): return
	var id: String = str(contracts[0].id)
	var contract: Dictionary = _find(s.public.procurements, id)
	_check(int(contract.cash_paid) == 0 and int(contract.accepted_quantity) == 0, "Announcement creates an obligation without premature payment or goods", failures)
	if not _queue(s, "public_bid", {"contract_id": id, "unit_price": 1, "delivery_month": 8}, failures): return
	s = _advance(s, failures, "Supplier bid competes under ordinary budget and qualification rules")
	var bids: Array = s.public.bids.filter(func(bid): return bid.contract_id == id and bid.status == "submitted")
	_check(not bids.is_empty(), "Real supplier bid remains available for announced evaluation", failures)
	if bids.is_empty(): return
	var hidden: Dictionary = Public.summary(s, "p-1")
	_check(hidden.bids.filter(func(bid): return bid.contract_id == id).is_empty(), "Unrelated observer cannot read sealed rival bids", failures)
	_check(not Public.summary(s, officer).bids.filter(func(bid): return bid.contract_id == id).is_empty(), "Authorized official may inspect the actual submitted bids", failures)
	var winning_bid: Dictionary = Public._best_bid(s, _find(s.public.procurements, id))
	officer = str(_find(s.public.offices, str(g.office_id)).person_id)
	s.player_id = officer
	if not _queue(s, "public_award", {"government_id": g.id, "contract_id": id, "bid_id": winning_bid.id, "advance_bps": 0}, failures, officer, str(g.id)): return
	s = _advance(s, failures, "Public contract awarded without advance payment")
	contract = _find(s.public.procurements, id)
	_check(int(contract.cash_paid) == 0 and int(contract.payable) == 0, "Award alone does not recognize inspected payable or full sales", failures)
	var supplier: Dictionary = _find(s.companies, str(contract.company_id))
	if not _queue(s, "public_deliver", {"contract_id": id, "quantity": 4}, failures, str(supplier.director_id), str(supplier.id)): return
	s = _advance(s, failures, "Real supplier goods leave inventory for contract delivery")
	var deliveries: Array = s.public.deliveries.filter(func(delivery): return delivery.contract_id == id)
	_check(not deliveries.is_empty(), "Delivery creates tracked goods with a real destination", failures)
	if deliveries.is_empty(): return
	var delivery: Dictionary = deliveries[0]
	for _month in range(3):
		if delivery.status == "awaiting_inspection": break
		s = _advance(s, failures, "Contract transport reaches its actual destination")
		delivery = _find(s.public.deliveries, str(delivery.id))
	officer = str(_find(s.public.offices, str(g.office_id)).person_id)
	s.player_id = officer
	if not _queue(s, "public_inspect", {"government_id": g.id, "delivery_id": delivery.id}, failures, officer, str(g.id)): return
	s = _advance(s, failures, "Officer verifies delivered specification and actual quantity")
	contract = _find(s.public.procurements, id)
	_check(int(contract.accepted_quantity) == 4, "Inspection accepts the supplied quantity once", failures)
	officer = str(_find(s.public.offices, str(g.office_id)).person_id)
	s.player_id = officer
	if not _queue(s, "public_pay", {"government_id": g.id, "contract_id": id}, failures, officer, str(g.id)): return
	s = _advance(s, failures, "Government settles inspected military procurement")
	contract = _find(s.public.procurements, id)
	_check(int(contract.cash_paid) == 4 * int(contract.unit_price) and int(contract.payable) == 0, "Government budget and supplier receivable settle the same contract amount", failures)
	_check(contract.status == "fulfilled", "Paid, accepted contract reaches its terminal fulfilled state", failures)

static func _test_neutral_benchmark(failures: Array, quick: bool) -> void:
	var config: Variant = JSON.parse_string(FileAccess.get_file_as_string("res://data/generation.json"))
	_check(config is Dictionary and config.get("neutral_benchmark") is Dictionary, "Neutral economic benchmark is an explicit versioned experimental data contract", failures)
	if not config is Dictionary or not config.get("neutral_benchmark") is Dictionary: return
	var benchmark: Dictionary = config.neutral_benchmark
	var valid: bool = benchmark.get("status", "") == "experimental" and benchmark.get("world_options") is Dictionary
	for key: String in ["seed", "months", "quick_months", "maximum_essential_unmet_value"]:
		valid = valid and Economy._whole_number(benchmark.get(key)) and float(benchmark.get(key, -1)) >= 0.0
	_check(valid, "Neutral benchmark declares bounded whole seed/horizon/essential-value threshold and world options", failures)
	if not valid: return
	var months: int = int(benchmark.quick_months if quick else benchmark.months)
	_check(months > 0 and months <= int(benchmark.months) and months <= int(config.maximum_preview_months), "Neutral benchmark horizon stays within the declared simulation limit", failures)
	if months <= 0 or months > int(benchmark.months) or months > int(config.maximum_preview_months): return
	var s: Dictionary = Economy.new_world(int(benchmark.seed), benchmark.world_options)
	var threshold: int = int(benchmark.maximum_essential_unmet_value)
	for step: int in range(months):
		var event_start: int = s.events.size()
		var result: Dictionary = Economy.resolve_month(s)
		_check(result.get("ok", false), "Neutral seed %d month %d resolves under actual finite accounts/resources: %s" % [int(benchmark.seed), step + 1, str(result.get("errors", []))], failures)
		if not result.get("ok", false): return
		s = result.state
		_check(Economy.validate(s).is_empty(), "Neutral benchmark month %d satisfies all physical, financial, employment and authority invariants" % (step + 1), failures)
		var actual: Dictionary = Economy.metrics(s)
		_check(int(actual.unmet_value) <= threshold, "Neutral benchmark month %d meets the experimental essential-demand bound (%d <= %d CR-minor)" % [step + 1, int(actual.unmet_value), threshold], failures)
		var new_openings: Array = []
		for index: int in range(event_start, s.events.size()):
			if s.events[index].type == "opening" and int(s.events[index].month) >= 1: new_openings.append(s.events[index].id)
		_check(new_openings.is_empty(), "Neutral benchmark has no runtime opening-asset rescue: " + str(new_openings), failures)
		print("Neutral benchmark seed=%d month=%d people=%d companies=%d essential_unmet_value=%d" % [int(benchmark.seed), step + 1, s.people.size(), s.companies.size(), int(actual.unmet_value)])
