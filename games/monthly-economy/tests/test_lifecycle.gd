extends RefCounted
const Economy = preload("res://core/economy.gd")
const Life = preload("res://core/lifecycle.gd")
const Industry = preload("res://core/industry.gd")
const Ledger = preload("res://core/ledger.gd")
static var checks: int = 0
static var _baseline: Dictionary = {}

static func _check(condition: bool, message: String, errors: Array) -> void:
	checks += 1
	if not condition: errors.append(message)

static func _world() -> Dictionary:
	if _baseline.is_empty():
		_baseline = Economy.new_world(42, {"cities_per_nation": 1, "expanded_industries": false})
		Life.initialize(_baseline)
	return _baseline.duplicate(true)

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if item.get("id") == id: return item
	return {}

static func _cmd(s: Dictionary, id: String, actor: String, type: String, payload: Dictionary, company: String = "") -> Dictionary:
	return {"id": id, "actor_id": actor, "acting_company_id": company, "type": type, "payload": payload, "expected_version": s.version}

static func _cash(s: Dictionary) -> Dictionary:
	var totals: Dictionary = {}
	for account in s.accounts.values():
		if account.kind == "cash": totals[account.currency] = int(totals.get(account.currency, 0)) + int(account.balance)
	return totals

static func _hash(s: Dictionary) -> String:
	return JSON.stringify(s, "", true, true).sha256_text()

static func run() -> Array:
	var errors: Array = []
	checks = 0
	_test_initial(errors)
	_test_startup(errors)
	_test_employment(errors)
	_test_office(errors)
	_test_training(errors)
	_test_delegation(errors)
	_test_acquisition(errors)
	_test_liquidation(errors)
	_test_viewpoint(errors)
	print("Lifecycle assertions: %d" % checks)
	return errors

static func _test_initial(errors: Array) -> void:
	var s: Dictionary = _world()
	_check(Life.validate(s).is_empty(), "Generated lifecycle state validates", errors)
	_check(Ledger.validate(s).is_empty(), "Generated financial ownership ledger validates", errors)
	var before: String = _hash(s)
	Life.initialize(s)
	_check(_hash(s) == before, "Lifecycle initialization never grants duplicate wealth or employment", errors)
	_check(Life.shares_owned(s, "p-0", "co-0") > 0, "Initial shareholder has real recorded shares", errors)
	var bad: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://data/lifecycle.json"))
	bad.parameters.erase("registration_fee")
	_check(not Life.validate_content(bad).is_empty(), "Missing lifecycle tuning fails content validation", errors)

static func _utilities(s: Dictionary, month: int) -> void:
	Industry.before_turn(s,month)
	for priority in [0,1]:
		for facility in s.facilities:
			if Economy._production_priority(s,facility)==priority: Economy._produce(s,facility,month)

static func _qualify(s: Dictionary, person: String, skill: String, errors: Array) -> void:
	# Earn actual contractual wages in a real month before paying tuition and
	# startup capital; the higher experimental tuition is never free funding.
	var earned: Dictionary=Economy.resolve_month(s)
	_check(bool(earned.ok),"Founder earns existing employer wages before tuition and startup",errors)
	if not earned.ok:return
	s.clear()
	s.merge(earned.state,true)
	var fee: int=int(Life._p().training_monthly_fee)
	_check(Life.apply(s, _cmd(s, "lc-qualify-" + person + skill, person, "start_training", {"skill": skill, "months": 1, "budget": fee}), int(s.planning_month)).get("ok", false), "Founder can obtain management qualification through paid training", errors)
	var month: int = int(s.planning_month)
	s.monthly_labor = {}
	_utilities(s,month)
	Life.monthly(s, month)
	Life.deliver_training(s,month)
	s.monthly_labor = {}
	Life.monthly(s, month + 1)
	s.planning_month = month + 2
	s.last_resolved_month = month + 1
	s.version = month + 1

static func _test_startup(errors: Array) -> void:
	var s: Dictionary = _world()
	_qualify(s, "p-1", "management", errors)
	var totals: Dictionary = _cash(s)
	var old_companies: int = s.companies.size()
	var original_wealth: int = Ledger.balance(s, "p-1:cash")
	var c: Dictionary = _cmd(s, "lc-startup-test", "p-1", "create_company", {"name": "검증 창업 법인", "capital": 100, "city_id": "city-0", "kind": "bakery"})
	_check(Life.apply(s, c, int(s.planning_month)).get("ok", false), "Worker may leave employment and create a company with own funds", errors)
	_check(s.companies.size() == old_companies + 1, "Incorporation creates exactly one legal entity", errors)
	var co: Dictionary = s.companies[-1]
	_check(Ledger.balance(s, str(co.id) + ":cash") == 100, "Company receives paid capital exactly once", errors)
	_check(Ledger.balance(s, "p-1:cash") < original_wealth - 99, "Founder paid own capital and registration fee", errors)
	_check(_cash(s) == totals, "Company incorporation does not mint currency", errors)
	_check(_find(s.people, "p-1").company_id == co.id and _find(s.people, "p-1").role == "director", "Founder changes office without keeping duplicated previous labor", errors)
	_check(Life.shares_owned(s, "p-1", str(co.id)) > 0, "Paid contribution issues actual ownership", errors)
	_check(s.facilities.filter(func(f): return f.company_id == co.id).is_empty(), "Startup does not obtain a free facility", errors)
	_check(Life.validate(s).is_empty() and Ledger.validate(s).is_empty(), "Startup preserves office and ledger invariants", errors)
	var hash: String = _hash(s)
	_check(not Life.apply(s, c, int(s.planning_month)).get("ok", false) and _hash(s) == hash, "Repeated startup command is rejected atomically", errors)
	var bad: Dictionary = _cmd(s, "lc-no-money", "p-2", "create_company", {"name": "실패 기업", "capital": 999999999, "city_id": "city-0", "kind": "bakery"})
	_check(not Life.apply(s, bad, 1).get("ok", false) and _hash(s) == hash, "Unaffordable startup cannot create accounts or companies", errors)

static func _test_employment(errors: Array) -> void:
	var s: Dictionary = _world()
	_check(Life.apply(s, _cmd(s, "lc-resign", "p-2", "resign_job", {}), 1).get("ok", false), "An employee may resign", errors)
	_check(_find(s.people, "p-2").employment_status == "unemployed", "Resigning releases the previous workplace", errors)
	_check(Life.apply(s, _cmd(s, "lc-post", "p-3", "post_job", {"role": "operator", "wage": 3, "hours": 40, "skill": 0}, "co-1"), 1).get("ok", false), "Director can publish a funded vacancy", errors)
	var offer: Dictionary = s.lifecycle.job_offers[-1]
	_check(Life.apply(s, _cmd(s, "lc-apply", "p-2", "apply_job", {"offer_id": offer.id}), 1).get("ok", false), "Qualified person may accept a public job", errors)
	var person: Dictionary = _find(s.people, "p-2")
	_check(person.company_id == "co-1" and person.hours == 40 and person.wage == 3, "New employer, actual hours and wage replace previous terms", errors)
	var active: Array = s.lifecycle.employment.filter(func(e): return e.person_id == "p-2" and e.status == "active")
	_check(active.size() == 1, "Job changes cannot duplicate employment capacity", errors)
	var before: String = _hash(s)
	_check(not Life.apply(s, _cmd(s, "lc-offer-taken", "p-1", "apply_job", {"offer_id": offer.id}), 1).get("ok", false) and _hash(s) == before, "A filled vacancy cannot hire another person", errors)
	_check(Life.validate(s).is_empty(), "Employment transitions preserve person/contract agreement", errors)
	_check(Life.apply(s, _cmd(s, "lc-headhunt", "p-0", "hire_person", {"person_id": "p-2", "role": "operator", "wage": 4, "hours": 40}, "co-0"), 1).get("ok", false), "A director may propose a paid hiring contract", errors)
	_check(_find(s.people, "p-2").company_id == "co-1", "A hiring proposal cannot forcibly transfer rival labor", errors)
	var private_offer: Dictionary = s.lifecycle.job_offers[-1]
	_check(not Life.apply(s, _cmd(s, "lc-private-job", "p-1", "apply_job", {"offer_id": private_offer.id}), 1).get("ok", false), "Only the intended candidate can accept a private offer", errors)
	_check(Life.apply(s, _cmd(s, "lc-hire-consent", "p-2", "apply_job", {"offer_id": private_offer.id}), 1).get("ok", false), "Candidate can voluntarily accept the new employer's offer", errors)
	_check(_find(s.people, "p-2").company_id == "co-0" and Life.validate(s).is_empty(), "Consensual headhunting moves labor exactly once", errors)

static func _test_office(errors: Array) -> void:
	var s: Dictionary = _world()
	var company_cash: int = Ledger.balance(s, "co-0:cash")
	var personal_cash: int = Ledger.balance(s, "p-0:cash")
	var shares: int = Life.shares_owned(s, "p-0", "co-0")
	_check(Life.apply(s, _cmd(s, "lc-dismiss", "p-0", "dismiss_director", {"company_id": "co-0"}), 1).get("ok", false), "Controlling shareholder may dismiss a director", errors)
	_check(_find(s.companies, "co-0").director_id == "", "Dismissal leaves an explicit office vacancy", errors)
	_check(Ledger.balance(s, "co-0:cash") == company_cash and Ledger.balance(s, "p-0:cash") == personal_cash and Life.shares_owned(s, "p-0", "co-0") == shares, "Office change moves neither company cash nor personal shares", errors)
	_check(not Life.can_act(s, "p-0", "co-0", "post_job", {}), "Former director loses operating authority", errors)
	_check(Life.apply(s, _cmd(s, "lc-ceo-resign", "p-3", "resign_job", {}), 1).get("ok", false), "An executive may leave an existing office before accepting another", errors)
	_check(not Life.apply(s, _cmd(s, "lc-forced-appoint", "p-0", "appoint_director", {"company_id": "co-0", "person_id": "p-3"}), 1).get("ok", false), "A shareholder cannot force an unconsenting nominee into employment", errors)
	_check(Life.apply(s, _cmd(s, "lc-executive-apply", "p-3", "apply_director", {"company_id": "co-0"}), 1).get("ok", false), "Qualified executive personally consents by applying", errors)
	_check(Life.apply(s, _cmd(s, "lc-appoint", "p-0", "appoint_director", {"company_id": "co-0", "person_id": "p-3"}), 1).get("ok", false), "Shareholder may appoint a qualified consenting individual", errors)
	_check(_find(s.companies, "co-0").director_id == "p-3" and Life.shares_owned(s, "p-3", "co-0") == 0, "An employed director receives office without ownership", errors)
	_check(Life.validate(s).is_empty(), "Director appointment preserves office/employment invariants", errors)
	var before: String = _hash(s)
	_check(not Life.apply(s, _cmd(s, "lc-unauthorized-vote", "p-2", "dismiss_director", {"company_id": "co-0"}), 1).get("ok", false) and _hash(s) == before, "A non-shareholder cannot dismiss management", errors)

static func _test_training(errors: Array) -> void:
	var s: Dictionary = _world()
	var totals: Dictionary = _cash(s)
	var original_skill: int = _find(s.people, "p-1").skills.operations
	_check(Life.apply(s, _cmd(s, "lc-training", "p-1", "start_training", {"skill": "operations", "months": 1, "budget": int(Life._p().training_monthly_fee)}), 1).get("ok", false), "A person can fund real training", errors)
	s.monthly_labor = {}
	_utilities(s,1)
	Life.monthly(s, 1)
	Life.deliver_training(s,1)
	var t: Dictionary = s.lifecycle.training[-1]
	_check(t.months_done == 1 and t.spent == int(Life._p().training_monthly_fee) and t.status == "completed_pending", "Training consumes actual tuition, time and a full period", errors)
	_check(int(s.monthly_labor.get("p-1", 0)) > 0, "Learner time is reserved from workplace capacity", errors)
	_check(_find(s.people, "p-1").skills.operations == original_skill, "Skill is not granted before its effective next month", errors)
	s.monthly_labor = {}
	Life.monthly(s, 2)
	_check(_find(s.people, "p-1").skills.operations == original_skill + 1 and t.status == "completed", "Completed training becomes effective next month", errors)
	_check(_cash(s) == totals and Ledger.validate(s).is_empty(), "Training fees transfer existing funds to an actual provider", errors)

static func _test_delegation(errors: Array) -> void:
	var s: Dictionary = _world()
	_check(Life.apply(s, _cmd(s, "lc-delegate", "p-0", "delegate_management", {"person_id": "p-1", "permissions": "post_job,build", "budget": 100}, "co-0"), 1).get("ok", false), "Director can grant a bounded mandate", errors)
	_check(Life.can_act(s, "p-1", "co-0", "post_job", {"wage": 2, "hours": 40}), "Delegate can act inside its budget", errors)
	_check(not Life.can_act(s, "p-1", "co-0", "post_job", {"wage": 3, "hours": 40}), "Delegated budget is a real authorization constraint", errors)
	_check(not Life.can_act(s, "p-1", "co-0", "create_subsidiary", {"capital": 50}), "A delegate cannot invent a new permission", errors)
	_check(Life.claim_delegated_budget(s, _cmd(s, "lc-claim", "p-1", "post_job", {}, "co-0"), 80), "Approved delegated commitment consumes the mandate", errors)
	_check(not Life.can_act(s, "p-1", "co-0", "post_job", {"wage": 2, "hours": 40}), "A budget cannot be committed twice", errors)

static func _test_acquisition(errors: Array) -> void:
	var s: Dictionary = _world()
	var totals: Dictionary = _cash(s)
	var company_cash: int = Ledger.balance(s, "co-0:cash")
	var seller_cash: int = Ledger.balance(s, "p-0:cash")
	_check(Life.apply(s, _cmd(s, "lc-acquisition-offer", "p-3", "offer_acquisition", {"target_company_id": "co-0", "amount": 50}), 1).get("ok", false), "Investor may offer a funded share acquisition", errors)
	var offer: Dictionary = s.lifecycle.acquisitions[-1]
	_check(Life.shares_owned(s, "p-0", "co-0") > 0, "An offer alone does not transfer shares", errors)
	_check(Life.apply(s, _cmd(s, "lc-acquisition-accept", "p-0", "accept_acquisition", {"offer_id": offer.id}), 1).get("ok", false), "Actual selling shareholder must accept the offer", errors)
	_check(Life.shares_owned(s, "p-0", "co-0") == 0 and Life.shares_owned(s, "p-3", "co-0") > 0, "Accepted acquisition moves existing shares exactly once", errors)
	_check(Ledger.balance(s, "co-0:cash") == company_cash and Ledger.balance(s, "p-0:cash") == seller_cash + 50, "Secondary acquisition pays the seller and is not company revenue", errors)
	_check(_cash(s) == totals and Ledger.validate(s).is_empty(), "Acquisition preserves money and balanced journal entries", errors)

static func _test_liquidation(errors: Array) -> void:
	var s: Dictionary = _world()
	var totals: Dictionary = _cash(s)
	_check(Life.apply(s, _cmd(s, "lc-bankruptcy", "p-0", "declare_bankruptcy", {}, "co-0"), 1).get("ok", false), "Authorized company may enter real liquidation", errors)
	_check(_find(s.companies, "co-0").status == "liquidating", "Liquidation is an explicit estate state", errors)
	_check(_find(s.people, "p-1").employment_status == "unemployed", "Failed company releases its staff without deleting people", errors)
	_check(not s.lifecycle.liquidation_assets.is_empty(), "Facilities and inventory become finite sale listings", errors)
	var listing: Dictionary = s.lifecycle.liquidation_assets.filter(func(a): return a.kind == "facility")[0]
	var old_facility: Dictionary = _find(s.facilities, str(listing.reference_id))
	_check(old_facility.company_id == "co-0", "Asset ownership is retained until someone pays", errors)
	_check(Life.apply(s, _cmd(s, "lc-buy-estate", "p-3", "buy_liquidation_asset", {"asset_id": listing.id}, "co-1"), 1).get("ok", false), "Another company may pay for a listed asset", errors)
	var sold: Dictionary = _find(s.facilities, str(listing.reference_id))
	_check(sold.company_id == "co-1" and sold.operational_month == 2, "Paid facility transfer preserves location and requires next-month readiness", errors)
	_check(_cash(s) == totals and Life.validate(s).is_empty() and Ledger.validate(s).is_empty(), "Liquidation does not mint money, delete debts or duplicate labor", errors)

static func _test_viewpoint(errors: Array) -> void:
	var s: Dictionary = _world()
	var balances: Dictionary = _cash(s)
	var owner_cash: int = Ledger.balance(s, "p-0:cash")
	_check(Life.apply(s, _cmd(s, "lc-switch", "p-0", "change_player", {"person_id": "p-1"}), 1).get("ok", false), "Observer may change the controlled individual under the world rule", errors)
	_check(s.player_id == "p-1" and s.controlled_company_id == "", "An ordinary worker does not inherit a company's operating rights", errors)
	_check(_cash(s) == balances and Ledger.balance(s, "p-0:cash") == owner_cash, "Viewpoint changes cannot move any wealth", errors)
