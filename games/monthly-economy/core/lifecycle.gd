extends RefCounted
## People, employment, corporate office and share ownership are distinct records.
## Every public action is validated identically for player and rule-based AI.
const Ledger = preload("res://core/ledger.gd")
const Domain = preload("res://core/domain.gd")
const CONFIG_PATH: String = "res://data/lifecycle.json"
static var _cache: Dictionary = {}
static var _kind_cache: Array = []

static func _cfg() -> Dictionary:
	if not _cache.is_empty(): return _cache
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(CONFIG_PATH))
	if not parsed is Dictionary or not validate_content(parsed).is_empty():
		push_error("LIFECYCLE_CONTENT_INVALID")
		return {}
	_cache = parsed
	return _cache

static func validate_content(data: Dictionary) -> Array:
	var errors: Array = []
	if data.get("status") != "experimental" or data.get("version") != 1: errors.append("LIFECYCLE_CONTENT_VERSION")
	if not data.get("parameters") is Dictionary: return ["LIFECYCLE_PARAMETERS_MISSING"]
	for key in ["initial_shares","minimum_capital","registration_fee","maximum_name_length","default_hours","default_wage","minimum_wage","maximum_wage","maximum_hours","training_monthly_fee","training_hours","maximum_skill","maximum_training_months","migration_fee","director_monthly_hours","delegation_monthly_hours","maximum_delegation_budget","acquisition_valid_months","liquidation_facility_price","liquidation_stock_discount_percent","retirement_notice_months","ai_reconsider_period","ai_startup_cash","ai_startup_capital","ai_training_cash","ai_wage_improvement","ai_hiring_wage_step","ai_maximum_wage","employees_per_facility","initial_reputation","initial_role_skill","initial_operations_skill","ai_training_months","executive_hourly_wage","minimum_executive_skill","ai_liquidation_reserve","liquidation_notice_months","guaranteed_hour_percent","director_guaranteed_hours"]:
		if not _integer(data.parameters.get(key)) or int(data.parameters[key]) <= 0: errors.append("LIFECYCLE_PARAMETER:" + key)
	if not data.get("payroll_policy") is Dictionary or data.payroll_policy.get("payment_rule") != "pro_rata_by_amount" or data.payroll_policy.get("rounding_rule") != "seeded_remainder" or data.payroll_policy.get("idle_hours_are_output") != false or data.payroll_policy.get("unpaid_wages_create_claims") != true: errors.append("LIFECYCLE_PAYROLL_POLICY")
	if not data.get("default_company_policy") is Dictionary: errors.append("LIFECYCLE_COMPANY_POLICY_MISSING")
	if not data.parameters.get("allow_player_switch") is bool: errors.append("LIFECYCLE_SWITCH_RULE_MISSING")
	for key in ["skills","roles","allowed_kinds","delegable_permissions","ui_actions"]:
		if not data.get(key) is Array or data[key].is_empty(): errors.append("LIFECYCLE_ARRAY:" + key)
	return errors

static func content_hash() -> String:
	return JSON.stringify(_cfg(), "", true, true).sha256_text()

static func ui_actions() -> Array:
	var actions: Array = _cfg().get("ui_actions", []).duplicate(true)
	for action in actions:
		if action.type in ["create_company", "create_subsidiary"]:
			for field in action.fields:
				if field.key == "kind": field.options = allowed_kinds()
	return actions

static func allowed_kinds() -> Array:
	if not _kind_cache.is_empty(): return _kind_cache.duplicate()
	_kind_cache = _cfg().get("allowed_kinds", []).duplicate()
	if ResourceLoader.exists("res://core/industry.gd"):
		var industry: Variant = load("res://core/industry.gd")
		if industry != null and industry.has_method("definitions"):
			for building in industry.definitions().get("buildings", []):
				if not str(building.id) in _kind_cache: _kind_cache.append(str(building.id))
	return _kind_cache.duplicate()

static func command_types() -> Array:
	var result: Array = []
	for action in _cfg().get("ui_actions", []): result.append(str(action.type))
	return result

static func _p() -> Dictionary:
	return _cfg().get("parameters", {})

static func _integer(value: Variant) -> bool:
	return (typeof(value) == TYPE_INT or typeof(value) == TYPE_FLOAT) and is_finite(float(value)) and float(value) == floor(float(value)) and abs(float(value)) <= Ledger.MAX_INTEGER

static func _number(payload: Dictionary, key: String, minimum: int, maximum: int = Ledger.MAX_INTEGER) -> bool:
	return payload.has(key) and _integer(payload[key]) and int(payload[key]) >= minimum and int(payload[key]) <= maximum

static func _text(payload: Dictionary, key: String) -> bool:
	return payload.get(key) is String and not str(payload[key]).strip_edges().is_empty()

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if item is Dictionary and str(item.get("id", "")) == id: return item
	return {}

static func planning_reservations(s: Dictionary, c: Dictionary) -> Array:
	var p: Dictionary = _p()
	var payload: Dictionary = c.get("payload", {})
	var actor: String = str(c.get("actor_id", ""))
	var company: String = str(c.get("acting_company_id", ""))
	var result: Array = []
	var cash_owner: String = actor
	var money: int = 0
	var labor: int = 0
	match str(c.get("type", "")):
		"create_company":
			money = int(payload.get("capital", 0)) + int(p.registration_fee)
			labor = int(p.director_monthly_hours)
		"create_subsidiary":
			cash_owner = company
			money = int(payload.get("capital", 0)) + int(p.registration_fee)
		"start_training":
			money = int(p.training_monthly_fee)
			labor = int(p.training_hours)
		"move_person": money = int(p.migration_fee)
		"post_job", "hire_person":
			cash_owner = company
			# This is the employer's own first-month payroll commitment. The
			# candidate's labor remains competitive until that person accepts.
			money = int(payload.get("wage", 0)) * int(payload.get("hours", 0))
		"apply_job":
			var offer: Dictionary = _find(s.get("lifecycle", {}).get("job_offers", []), str(payload.get("offer_id", "")))
			labor = int(offer.get("hours", 0))
		"apply_director": labor = int(p.director_monthly_hours)
		"accept_acquisition":
			var offer: Dictionary = _find(s.get("lifecycle", {}).get("acquisitions", []), str(payload.get("offer_id", "")))
			if offer.get("seller_id") == actor or offer.get("seller_id") == company:
				result.append({"owner_id": str(offer.seller_id), "resource_type": "shares", "resource_id": str(offer.company_id), "quantity": int(offer.get("shares", 0))})
		"buy_liquidation_asset":
			var asset: Dictionary = _find(s.get("lifecycle", {}).get("liquidation_assets", []), str(payload.get("asset_id", "")))
			cash_owner = company
			money = int(asset.get("price", 0))
	if money > 0: result.append({"owner_id": cash_owner, "resource_type": "cash", "quantity": money})
	if labor > 0: result.append({"owner_id": actor, "resource_type": "labor", "quantity": labor})
	return result

static func _cash(s: Dictionary, owner: String) -> int:
	return Ledger.balance(s, owner + ":cash")

static func _event(s: Dictionary, type: String, reason: String, month: int, extra: Dictionary = {}) -> void:
	var event: Dictionary = {"id": "ev-%d" % s.events.size(), "month": month, "type": type, "reason": reason, "actor_id": "", "city_id": "", "product_id": "", "quantity": 0, "amount": 0}
	event.merge(extra, true)
	s.events.append(event)

static func _new_id(s: Dictionary, prefix: String) -> String:
	var serial: int = int(s.lifecycle.get("next_serial", 0))
	s.lifecycle.next_serial = serial + 1
	return "lc-%s-%d" % [prefix, serial]

static func _role_skill(role: String) -> String:
	return str({"director": "management", "builder": "construction", "developer": "software", "researcher": "research", "service": "operations"}.get(role, "operations"))

static func initialize(s: Dictionary) -> void:
	if s.has("lifecycle"): return
	var p: Dictionary = _p()
	s.lifecycle = {"version": 1, "next_serial": 0, "employment": [], "job_offers": [], "director_applications": [], "training": [], "delegations": [], "acquisitions": [], "liquidations": [], "liquidation_assets": [], "successions": [], "applied_commands": {}, "wage_claims": [], "payroll_accruals": {}, "payroll_months": {}}
	if not s.has("ownership"): s.ownership = []
	if not s.has("households"): s.households = []
	for company in s.companies:
		var has_owner: bool = false
		for record in s.ownership:
			if record.company_id == company.id: has_owner = true
		if not has_owner:
			s.ownership.append({"company_id": company.id, "owner_id": company.shareholder_id, "shares": int(p.initial_shares), "voting_shares": int(p.initial_shares)})
		company["business_kind"] = str(company.get("business_kind", "bakery"))
		company["incorporation_month"] = 0
		company["parent_company_id"] = ""
	for person in s.people:
		person["capacity_hours"] = int(person.hours)
		person["skills"] = {"operations": int(p.initial_operations_skill), "construction": 0, "management": 0, "software": 0, "research": 0}
		person.skills[_role_skill(str(person.role))] = int(p.initial_role_skill)
		person["career_status"] = "active"
		person["reputation"] = int(p.initial_reputation)
		person["successor_id"] = ""
		person["household_id"] = "household-" + str(person.id)
		var city: Dictionary = _find(s.cities, str(person.city_id))
		person["nationality_id"] = city.get("nation_id", "")
		if _find(s.households, person.household_id).is_empty():
			s.households.append({"id": person.household_id, "members": [person.id], "city_id": person.city_id, "joint_account_id": "", "consumption_policy": "essential_first"})
		if not str(person.company_id).is_empty():
			s.lifecycle.employment.append({"id": "employment-" + str(person.id), "person_id": person.id, "company_id": person.company_id, "city_id":person.city_id, "role": person.role, "wage": int(person.wage), "hours": int(person.hours), "guaranteed_hours": _default_guaranteed_hours(str(person.role), int(person.hours)), "start_month": 0, "end_month": 0, "status": "active"})

static func shares_owned(s: Dictionary, owner: String, company: String, voting: bool = false) -> int:
	var result: int = 0
	for record in s.get("ownership", []):
		if record.get("owner_id") == owner and record.get("company_id") == company:
			result += int(record.get("voting_shares" if voting else "shares", 0))
	return result

static func _largest_owner(s: Dictionary, company: String) -> String:
	var totals: Dictionary = {}
	for holding in s.get("ownership", []):
		if holding.company_id == company: totals[str(holding.owner_id)] = int(totals.get(str(holding.owner_id), 0)) + int(holding.shares)
	var owners: Array = totals.keys()
	owners.sort()
	var best: String = ""
	for owner in owners:
		if int(totals[owner]) > int(totals.get(best, 0)): best = str(owner)
	return best

static func _buyer(s: Dictionary, c: Dictionary) -> String:
	return str(c.acting_company_id) if not str(c.get("acting_company_id", "")).is_empty() else str(c.actor_id)

static func _controls(s: Dictionary, owner: String, company: String) -> bool:
	var total: int = 0
	for record in s.get("ownership", []):
		if record.get("company_id") == company: total += int(record.get("voting_shares", 0))
	return total > 0 and shares_owned(s, owner, company, true) * 2 > total

static func _director(s: Dictionary, actor: String, company: String) -> bool:
	var co: Dictionary = _find(s.companies, company)
	return not co.is_empty() and co.get("status", "active") == "active" and co.get("director_id") == actor

static func has_delegated_permission(s: Dictionary, actor: String, company: String, type: String, amount: int = 0) -> bool:
	for delegation in s.get("lifecycle", {}).get("delegations", []):
		if delegation.get("status") != "active" or delegation.get("person_id") != actor or delegation.get("company_id") != company: continue
		if not type in delegation.get("permissions", []): continue
		var manager: Dictionary = _find(s.people, actor)
		if manager.is_empty() or manager.get("company_id") != company or manager.get("career_status", "active") != "active": continue
		var co: Dictionary = _find(s.companies, company)
		if co.get("director_id") != delegation.get("grantor_id"): continue
		if amount <= delegated_budget_remaining(s, delegation): return true
	return false

static func can_act(s: Dictionary, actor: String, company: String, type: String, payload: Dictionary = {}) -> bool:
	if _director(s, actor, company): return true
	var amount: int = 0
	for key in ["budget", "amount", "capital"]:
		if _integer(payload.get(key)): amount = maxi(amount, int(payload[key]))
	if _integer(payload.get("wage")) and _integer(payload.get("hours")):
		amount = maxi(amount, int(payload.wage) * int(payload.hours))
	return has_delegated_permission(s, actor, company, type, amount)

static func delegated_budget_remaining(s: Dictionary, delegation: Dictionary) -> int:
	# A new planning month has a fresh mandate ceiling. Previous spending stays
	# available for reports until the first real claim initializes the new month.
	var used: int = int(delegation.get("spent", 0)) if int(delegation.get("month", -1)) == int(s.planning_month) else 0
	return maxi(0, int(delegation.get("budget", 0)) - used)

static func claim_delegated_budget(s: Dictionary, c: Dictionary, amount: int) -> bool:
	if amount < 0: return false
	if _director(s, str(c.actor_id), str(c.acting_company_id)): return true
	if not has_delegated_permission(s, str(c.actor_id), str(c.acting_company_id), str(c.type), amount): return false
	for delegation in s.get("lifecycle", {}).get("delegations", []):
		if delegation.get("status") == "active" and delegation.get("person_id") == c.actor_id and delegation.get("company_id") == c.acting_company_id and c.type in delegation.get("permissions", []):
			var company: Dictionary = _find(s.companies, str(delegation.company_id))
			if company.get("director_id") != delegation.get("grantor_id"): continue
			if amount > delegated_budget_remaining(s, delegation): continue
			if int(delegation.get("month", -1)) != int(s.planning_month):
				delegation.month = int(s.planning_month)
				delegation.spent = 0
			delegation.spent = int(delegation.spent) + amount
			return true
	return false

static func _corporate(s: Dictionary, c: Dictionary) -> bool:
	return can_act(s, str(c.actor_id), str(c.get("acting_company_id", "")), str(c.type), c.get("payload", {}))

static func _reserved_cash(s: Dictionary, owner: String, except_id: String) -> int:
	var reserved: int = 0
	for queued in s.get("commands", []):
		if str(queued.get("id", "")) == except_id: continue
		var kind: String = str(queued.get("type", ""))
		var payer: String = str(queued.get("actor_id", "")) if kind in ["create_company", "move_person", "start_training", "offer_acquisition"] else str(queued.get("acting_company_id", ""))
		if payer != owner: continue
		var payload: Dictionary = queued.get("payload", {})
		match kind:
			"create_company", "create_subsidiary": reserved += int(payload.get("capital", 0)) + int(_p().registration_fee)
			"move_person": reserved += int(_p().migration_fee)
			"start_training": reserved += int(_p().training_monthly_fee)
	return reserved

static func _fee_recipient(s: Dictionary, city_id: String, payer: String) -> String:
	for government in s.get("public", {}).get("governments", []):
		if government.get("kind") == "city" and government.get("city_id") == city_id and s.accounts.has(str(government.id) + ":cash"): return str(government.id)
	for company in s.companies:
		if company.city_id == city_id and company.get("status", "active") == "active" and company.id != payer: return str(company.id)
	return ""

static func _permissions(payload: Dictionary) -> Array:
	var value: Variant = payload.get("permissions", [])
	if value is Array: return value
	if value is String:
		var result: Array = []
		for text in value.split(","):
			var clean: String = text.strip_edges()
			if not clean.is_empty() and not clean in result: result.append(clean)
		return result
	return []

static func _active_training(s: Dictionary, person: String) -> bool:
	for t in s.lifecycle.training:
		if t.person_id == person and t.status in ["active", "completed_pending"]: return true
	return false

static func _queued_person_action(s: Dictionary, c: Dictionary, types: Array) -> bool:
	for queued in s.get("commands", []):
		if queued.get("id") != c.id and queued.get("actor_id") == c.actor_id and queued.get("type") in types: return true
	return false

static func _teacher(s: Dictionary, learner: Dictionary, skill: String, required_hours: int = 0) -> Dictionary:
	for person in s.people:
		if person.id == learner.id or person.city_id != learner.city_id or person.get("career_status", "active") != "active": continue
		if str(person.get("company_id", "")).is_empty(): continue
		if _role_skill(str(person.role)) != skill: continue
		if int(person.get("skills", {}).get(skill, 0)) < int(learner.get("skills", {}).get(skill, 0)): continue
		var used: int = int(s.get("monthly_labor", {}).get(person.id, 0))
		if int(person.hours) - used >= required_hours: return person
	return {}

static func validate_command(s: Dictionary, c: Dictionary) -> String:
	if not s.has("lifecycle"): return "LIFECYCLE_NOT_INITIALIZED"
	if not c.get("payload") is Dictionary: return "INVALID_PAYLOAD"
	if not str(c.get("type", "")) in command_types(): return "UNSUPPORTED_LIFECYCLE_COMMAND"
	if str(c.get("id", "")).is_empty(): return "EMPTY_COMMAND_ID"
	if s.lifecycle.applied_commands.has(str(c.id)): return "DUPLICATE_MODULE_COMMAND"
	var actor: Dictionary = _find(s.people, str(c.get("actor_id", "")))
	if actor.is_empty(): return "UNKNOWN_ACTOR"
	if actor.get("career_status", "active") != "active" and str(c.type) != "change_player": return "PERSON_RETIRED"
	var payload: Dictionary = c.payload
	var p: Dictionary = _p()
	var type: String = str(c.type)
	if type in ["post_job", "hire_person", "fire_person", "delegate_management", "revoke_delegation", "create_subsidiary", "declare_bankruptcy", "buy_liquidation_asset"] and not _corporate(s, c): return "COMPANY_AUTHORITY_REQUIRED"
	match type:
		"create_company", "create_subsidiary":
			if not _text(payload, "name") or str(payload.name).length() > int(p.maximum_name_length): return "INVALID_COMPANY_NAME"
			for co in s.companies:
				if co.name == str(payload.name).strip_edges(): return "COMPANY_NAME_EXISTS"
			if _find(s.cities, str(payload.get("city_id", ""))).is_empty(): return "UNKNOWN_CITY"
			if not str(payload.get("kind", "")) in allowed_kinds(): return "UNKNOWN_BUSINESS_KIND"
			if not _number(payload, "capital", int(p.minimum_capital)): return "CAPITAL_TOO_SMALL"
			if type == "create_company" and int(actor.skills.get("management", 0)) < int(p.minimum_executive_skill): return "EXECUTIVE_QUALIFICATION_REQUIRED"
			if type == "create_company" and actor.role == "director" and not str(actor.company_id).is_empty(): return "DIRECTOR_MUST_LEAVE_OFFICE_BEFORE_STARTUP"
			var payer: String = str(actor.id) if type == "create_company" else str(c.acting_company_id)
			if _cash(s, payer) - _reserved_cash(s, payer, str(c.id)) < int(payload.capital) + int(p.registration_fee): return "STARTUP_FUNDS_REQUIRED"
			if _fee_recipient(s, str(payload.city_id), payer).is_empty(): return "REGISTRATION_SERVICE_UNAVAILABLE"
			if type == "create_company" and _queued_person_action(s, c, ["create_company", "apply_job", "resign_job", "move_person"]): return "PERSONAL_TIME_RESERVED"
		"post_job", "hire_person":
			var work_city: String=str(payload.get("city_id",_find(s.companies,str(c.acting_company_id)).get("city_id","")))
			var site_exists: bool=_find(s.companies,str(c.acting_company_id)).get("city_id","")==work_city
			for site in s.facilities+s.projects:
				if site.company_id==c.acting_company_id and site.city_id==work_city: site_exists=true
			if not site_exists: return "COMPANY_WORKSITE_REQUIRED"
			if not str(payload.get("role", "")) in _cfg().roles: return "UNKNOWN_JOB_ROLE"
			if not _number(payload, "wage", int(p.minimum_wage), int(p.maximum_wage)) or not _number(payload, "hours", 1, int(p.maximum_hours)): return "INVALID_EMPLOYMENT_TERMS"
			if payload.has("guaranteed_hours") and (not _number(payload,"guaranteed_hours",1,int(payload.hours)) or (str(payload.role)=="director" and int(payload.guaranteed_hours)>int(_p().director_guaranteed_hours))): return "INVALID_GUARANTEED_HOURS"
			if _cash(s, str(c.acting_company_id)) < int(payload.wage) * int(payload.hours): return "PAYROLL_BUDGET_REQUIRED"
			if type == "post_job":
				if not _number(payload, "skill", 0, int(p.maximum_skill)): return "INVALID_SKILL_REQUIREMENT"
				for offer in s.lifecycle.job_offers:
					if offer.company_id == c.acting_company_id and offer.status == "open" and offer.role == payload.role and offer.city_id==work_city: return "VACANCY_ALREADY_OPEN"
			else:
				var person: Dictionary = _find(s.people, str(payload.get("person_id", "")))
				if person.is_empty(): return "UNKNOWN_CANDIDATE"
				if person.get("career_status", "active") != "active" or person.get("role") == "director" or person.company_id == c.acting_company_id: return "CANDIDATE_NOT_AVAILABLE"
				for offer in s.lifecycle.job_offers:
					if offer.status == "open" and offer.company_id == c.acting_company_id and offer.get("candidate_id", "") == person.id: return "CANDIDATE_ALREADY_HAS_OFFER"
				if person.city_id != work_city: return "CANDIDATE_RELOCATION_REQUIRED"
				if int(payload.hours) > int(person.get("capacity_hours", p.default_hours)): return "PERSON_HOURS_EXCEEDED"
		"apply_job":
			var offer: Dictionary = _find(s.lifecycle.job_offers, str(payload.get("offer_id", "")))
			if offer.is_empty() or offer.get("status") != "open": return "JOB_OFFER_CLOSED"
			if str(offer.get("candidate_id", "")) not in ["", str(actor.id)]: return "PRIVATE_JOB_OFFER"
			if offer.company_id == actor.company_id: return "ALREADY_EMPLOYED_HERE"
			if actor.role == "director" and not str(actor.company_id).is_empty(): return "DIRECTOR_MUST_LEAVE_OFFICE_FIRST"
			if offer.city_id != actor.city_id: return "RELOCATION_REQUIRED"
			if int(offer.hours) > int(actor.get("capacity_hours", p.default_hours)): return "PERSON_HOURS_EXCEEDED"
			if int(actor.skills.get(_role_skill(str(offer.role)), 0)) < int(offer.skill): return "QUALIFICATION_REQUIRED"
			if _queued_person_action(s, c, ["create_company", "apply_job", "resign_job", "move_person"]): return "PERSONAL_TIME_RESERVED"
		"resign_job":
			if str(actor.company_id).is_empty(): return "NOT_EMPLOYED"
			if _queued_person_action(s, c, ["create_company", "apply_job", "resign_job", "move_person"]): return "PERSONAL_TIME_RESERVED"
		"fire_person":
			var employee: Dictionary = _find(s.people, str(payload.get("person_id", "")))
			if employee.is_empty() or employee.company_id != c.acting_company_id: return "NOT_COMPANY_EMPLOYEE"
			if employee.role == "director": return "DIRECTOR_DISMISSAL_REQUIRES_VOTE"
		"start_training":
			if not str(payload.get("skill", "")) in _cfg().skills: return "UNKNOWN_SKILL"
			if not _number(payload, "months", 1, int(p.maximum_training_months)) or not _number(payload, "budget", int(p.training_monthly_fee)): return "INVALID_TRAINING_PLAN"
			if int(payload.budget) < int(payload.months) * int(p.training_monthly_fee): return "TRAINING_BUDGET_TOO_SMALL"
			if _cash(s, str(actor.id)) - _reserved_cash(s, str(actor.id), str(c.id)) < int(p.training_monthly_fee): return "TRAINING_FUNDS_REQUIRED"
			if _active_training(s, str(actor.id)): return "TRAINING_ALREADY_ACTIVE"
			if int(actor.skills.get(payload.skill, 0)) >= int(p.maximum_skill): return "SKILL_AT_MAXIMUM"
			if _teacher(s, actor, str(payload.skill)).is_empty(): return "TRAINING_PROVIDER_UNAVAILABLE"
		"move_person":
			var destination: Dictionary = _find(s.cities, str(payload.get("city_id", "")))
			if destination.is_empty() or destination.id == actor.city_id: return "INVALID_DESTINATION"
			if actor.role == "director" and not str(actor.company_id).is_empty(): return "DIRECTOR_MUST_LEAVE_OFFICE_FIRST"
			if _cash(s, str(actor.id)) - _reserved_cash(s, str(actor.id), str(c.id)) < int(p.migration_fee): return "MIGRATION_FUNDS_REQUIRED"
			if _fee_recipient(s, str(destination.id), str(actor.id)).is_empty(): return "RELOCATION_SERVICE_UNAVAILABLE"
			if _queued_person_action(s, c, ["create_company", "apply_job", "resign_job", "move_person"]): return "PERSONAL_TIME_RESERVED"
		"apply_director":
			var target: Dictionary = _find(s.companies, str(payload.get("company_id", "")))
			if target.is_empty() or target.get("status", "active") != "active": return "COMPANY_INACTIVE"
			if actor.role == "director": return "DIRECTOR_MUST_LEAVE_OFFICE_FIRST"
			if actor.city_id != target.city_id: return "NOMINEE_RELOCATION_REQUIRED"
			if int(actor.skills.get("management", 0)) < int(p.minimum_executive_skill): return "EXECUTIVE_QUALIFICATION_REQUIRED"
			for candidate in s.lifecycle.director_applications:
				if candidate.company_id == target.id and candidate.person_id == actor.id and candidate.status == "applied": return "DIRECTOR_APPLICATION_ALREADY_ACTIVE"
		"appoint_director", "dismiss_director":
			var target: Dictionary = _find(s.companies, str(payload.get("company_id", "")))
			if target.is_empty() or target.get("status", "active") != "active": return "COMPANY_INACTIVE"
			if not _controls(s, str(actor.id), str(target.id)) and not (_director(s, str(actor.id), str(c.acting_company_id)) and _controls(s, str(c.acting_company_id), str(target.id))): return "CONTROLLING_VOTE_REQUIRED"
			if type == "dismiss_director" and str(target.director_id).is_empty(): return "NO_DIRECTOR"
			if type == "appoint_director":
				var nominee: Dictionary = _find(s.people, str(payload.get("person_id", "")))
				if nominee.is_empty() or nominee.get("career_status", "active") != "active": return "INVALID_DIRECTOR_NOMINEE"
				if nominee.role == "director" and nominee.company_id != target.id: return "NOMINEE_ALREADY_MANAGES_COMPANY"
				if nominee.city_id != target.city_id: return "NOMINEE_RELOCATION_REQUIRED"
				if int(nominee.skills.get("management", 0)) < int(p.minimum_executive_skill): return "EXECUTIVE_QUALIFICATION_REQUIRED"
				if nominee.id != actor.id:
					var consent: bool = false
					for candidate in s.lifecycle.director_applications:
						if candidate.company_id == target.id and candidate.person_id == nominee.id and candidate.status == "applied": consent = true
					if not consent: return "DIRECTOR_CANDIDATE_CONSENT_REQUIRED"
		"delegate_management":
			var manager: Dictionary = _find(s.people, str(payload.get("person_id", "")))
			if manager.is_empty() or manager.company_id != c.acting_company_id or manager.id == actor.id: return "MANAGER_MUST_BE_COMPANY_EMPLOYEE"
			if not _number(payload, "budget", 1, int(p.maximum_delegation_budget)): return "INVALID_DELEGATION_BUDGET"
			var permissions: Array = _permissions(payload)
			if permissions.is_empty(): return "NO_DELEGATED_PERMISSIONS"
			for permission in permissions:
				if not permission in _cfg().delegable_permissions: return "CANNOT_DELEGATE_PERMISSION"
			if not _director(s, str(actor.id), str(c.acting_company_id)): return "ONLY_DIRECTOR_CAN_DELEGATE"
		"revoke_delegation":
			var delegation: Dictionary = _find(s.lifecycle.delegations, str(payload.get("delegation_id", "")))
			if delegation.is_empty() or delegation.company_id != c.acting_company_id or delegation.status != "active": return "DELEGATION_NOT_ACTIVE"
		"offer_acquisition":
			var target: Dictionary = _find(s.companies, str(payload.get("target_company_id", "")))
			if target.is_empty() or target.get("status", "active") != "active": return "COMPANY_INACTIVE"
			if not _number(payload, "amount", 1): return "INVALID_ACQUISITION_PRICE"
			var seller: String = _largest_owner(s, str(target.id))
			var buyer: String = _buyer(s, c)
			if buyer != actor.id and not _corporate(s, c): return "COMPANY_AUTHORITY_REQUIRED"
			if buyer == target.id: return "CANNOT_ACQUIRE_OWN_EQUITY"
			if seller == buyer or shares_owned(s, seller, str(target.id)) == 0: return "NO_ACQUIRABLE_OWNER"
			if _cash(s, buyer) < int(payload.amount): return "ACQUISITION_FUNDS_REQUIRED"
		"accept_acquisition":
			var offer: Dictionary = _find(s.lifecycle.acquisitions, str(payload.get("offer_id", "")))
			if offer.is_empty() or offer.status != "offered" or int(s.planning_month) > int(offer.expires_month): return "ACQUISITION_OFFER_EXPIRED"
			if not (offer.seller_id == actor.id or _director(s, str(actor.id), str(offer.seller_id))): return "ACQUISITION_SELLER_AUTHORITY_REQUIRED"
			if shares_owned(s, str(offer.seller_id), str(offer.company_id)) < int(offer.shares): return "ACQUISITION_SHARES_CHANGED"
			if _cash(s, str(offer.buyer_id)) < int(offer.amount): return "ACQUISITION_BUYER_FUNDS_REQUIRED"
		"declare_bankruptcy":
			if not _director(s, str(actor.id), str(c.acting_company_id)): return "DIRECTOR_AUTHORITY_REQUIRED"
		"buy_liquidation_asset":
			var asset: Dictionary = _find(s.lifecycle.liquidation_assets, str(payload.get("asset_id", "")))
			if asset.is_empty() or asset.status != "listed": return "LIQUIDATION_ASSET_UNAVAILABLE"
			if asset.estate_id == c.acting_company_id: return "CANNOT_BUY_OWN_ASSET"
			if _cash(s, str(c.acting_company_id)) < int(asset.price): return "ASSET_PURCHASE_FUNDS_REQUIRED"
		"appoint_successor":
			var next: Dictionary = _find(s.people, str(payload.get("person_id", "")))
			if next.is_empty() or next.id == actor.id or next.get("career_status", "active") != "active": return "INVALID_SUCCESSOR"
		"retire_person":
			if str(actor.get("successor_id", "")).is_empty(): return "SUCCESSOR_REQUIRED"
		"change_player":
			if not p.allow_player_switch: return "OBSERVER_SWITCH_DISABLED"
			if c.actor_id != s.player_id: return "ONLY_PLAYER_MAY_SWITCH_CONTROL"
			if _find(s.people, str(payload.get("person_id", ""))).is_empty(): return "UNKNOWN_SUCCESSOR"
	return ""

static func apply(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var error: String = validate_command(s, c)
	if not error.is_empty(): return {"ok": false, "error": error}
	if str(c.type) not in ["create_company", "create_subsidiary", "move_person", "accept_acquisition", "buy_liquidation_asset", "declare_bankruptcy"]:
		# Pure domain commands are already completely validated and cannot fail
		# midway. Avoid copying the growing ledger for every hire or policy.
		var direct: Dictionary = _apply_mutating(s, c, month)
		if direct.get("ok", false): s.lifecycle.applied_commands[str(c.id)] = month
		return direct
	# A financial command may contain several monetary legs. Stage together.
	var candidate: Dictionary = s.duplicate(true)
	var result: Dictionary = _apply_mutating(candidate, c, month)
	if not result.get("ok", false): return result
	candidate.lifecycle.applied_commands[str(c.id)] = month
	s.clear()
	s.merge(candidate, true)
	return {"ok": true, "error": ""}

static func _account(s: Dictionary, owner: String, suffix: String, kind: String) -> String:
	var id: String = owner + ":" + suffix
	var currency: String = str(s.accounts.get(owner + ":cash", {}).get("currency", "CR"))
	Ledger.open_account(s, id, owner, kind, currency)
	return id

static func _pay(s: Dictionary, id: String, payer: String, receiver: String, amount: int, reason: String, month: int) -> bool:
	if amount <= 0: return true
	return Ledger.transfer(s, id, payer, receiver, amount, reason, month).get("ok", false)

static func _end_employment(s: Dictionary, person: Dictionary, month: int, reason: String) -> void:
	var old_company: String = str(person.company_id)
	for contract in s.lifecycle.employment:
		if contract.person_id == person.id and contract.status == "active":
			contract.status = "ended"
			contract.end_month = month
	for delegation in s.lifecycle.delegations:
		if delegation.get("person_id") == person.id and delegation.get("status") == "active": delegation.status = "revoked"
	person.company_id = ""
	person.role = "unemployed"
	person.hours = int(person.capacity_hours)
	person.wage = 0
	person.employment_status = "unemployed"
	_event(s, "employment_end", reason, month, {"actor_id": person.id, "company_id": old_company, "city_id": person.city_id})

static func _employ(s: Dictionary, person: Dictionary, company: Dictionary, role: String, wage: int, hours: int, month: int, guaranteed_hours: int = -1) -> void:
	if not str(person.company_id).is_empty(): _end_employment(s, person, month, "이직으로 기존 근로 계약 종료")
	person.company_id = company.id
	person.role = role
	person.hours = hours
	person.wage = wage
	person.employment_status = "employed"
	s.lifecycle.employment.append({"id": _new_id(s, "employment"), "person_id": person.id, "company_id": company.id, "city_id":person.city_id, "role": role, "wage": wage, "hours": hours, "guaranteed_hours": _default_guaranteed_hours(role, hours) if guaranteed_hours < 0 else guaranteed_hours, "start_month": month, "end_month": 0, "status": "active"})
	_event(s, "employment_start", "실제 인물과 근로 계약 체결", month, {"actor_id": person.id, "company_id": company.id, "city_id": person.city_id, "hours": hours, "amount": wage})

static func _clear_office(s: Dictionary, company: Dictionary, month: int, reason: String) -> void:
	var old: Dictionary = _find(s.people, str(company.director_id))
	if not old.is_empty() and old.company_id == company.id and old.role == "director": _end_employment(s, old, month, reason)
	company.director_id = ""
	for delegation in s.lifecycle.delegations:
		if delegation.company_id == company.id and delegation.status == "active": delegation.status = "revoked"
	if s.get("controlled_company_id", "") == company.id: s.controlled_company_id = ""

static func _appoint(s: Dictionary, company: Dictionary, person: Dictionary, month: int) -> void:
	if not str(company.director_id).is_empty() and company.director_id != person.id: _clear_office(s, company, month, "주주 의결로 대표 해임")
	_employ(s, person, company, "director", int(_p().executive_hourly_wage), int(person.capacity_hours), month)
	for candidate in s.lifecycle.director_applications:
		if candidate.company_id == company.id and candidate.person_id == person.id and candidate.status == "applied": candidate.status = "appointed"
	company.director_id = person.id
	if s.player_id == person.id: s.controlled_company_id = company.id
	_event(s, "director_appointed", "의결권에 따른 대표 선임·소유권은 별도 유지", month, {"actor_id": person.id, "company_id": company.id, "city_id": company.city_id})

static func _make_company(s: Dictionary, c: Dictionary, month: int, parent: bool) -> Dictionary:
	var payload: Dictionary = c.payload
	var investor: String = str(c.acting_company_id) if parent else str(c.actor_id)
	var company_id: String = _new_id(s, "company")
	for kind in ["cash", "equity", "expense", "revenue"]: Ledger.open_account(s, company_id + ":" + kind, company_id, kind)
	var investment_account: String = _account(s, investor, "investment:" + company_id, "asset")
	var amount: int = int(payload.capital)
	var posted: Dictionary = Ledger.post(s, "lifecycle-investment-" + str(c.id), [{"account": investor + ":cash", "debit": 0, "credit": amount}, {"account": investment_account, "debit": amount, "credit": 0}, {"account": company_id + ":cash", "debit": amount, "credit": 0}, {"account": company_id + ":equity", "debit": 0, "credit": amount}], "법인 출자·개인/모회사와 회사 자산 분리", month)
	if not posted.ok: return posted
	var recipient: String = _fee_recipient(s, str(payload.city_id), investor)
	if not _pay(s, "lifecycle-register-" + str(c.id), investor, recipient, int(_p().registration_fee), "법인 등록 서비스", month): return {"ok": false, "error": "REGISTRATION_PAYMENT_FAILED"}
	var co: Dictionary = {"id": company_id, "name": str(payload.name).strip_edges(), "city_id": payload.city_id, "director_id": "", "shareholder_id": investor, "policy": _cfg().default_company_policy.duplicate(true), "opening_cash": 0, "revenue": 0, "expenses": 0, "last_profit": 0, "status": "active", "business_kind": payload.kind, "incorporation_month": month, "parent_company_id": investor if parent else ""}
	s.companies.append(co)
	s.ownership.append({"company_id": company_id, "owner_id": investor, "shares": int(_p().initial_shares), "voting_shares": int(_p().initial_shares)})
	if not parent:
		var founder: Dictionary = _find(s.people, str(c.actor_id))
		_appoint(s, co, founder, month)
	else:
		# Incorporation does not create a second full-time director. A qualified
		# unemployed nominee must be appointed before it can submit operations.
		_event(s, "director_vacancy", "자회사 설립·대표 선임 필요", month, {"actor_id": c.actor_id, "company_id": company_id, "city_id": co.city_id})
	_event(s, "company_incorporated", "실제 출자금과 신주로 법인 설립·시설은 별도 건설", month, {"actor_id": c.actor_id, "company_id": company_id, "city_id": co.city_id, "amount": amount})
	return {"ok": true}

static func _apply_mutating(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var payload: Dictionary = c.payload
	var actor: Dictionary = _find(s.people, str(c.actor_id))
	var co: Dictionary = _find(s.companies, str(c.acting_company_id))
	match str(c.type):
		"create_company", "create_subsidiary": return _make_company(s, c, month, c.type == "create_subsidiary")
		"post_job":
			s.lifecycle.job_offers.append({"id": _new_id(s, "vacancy"), "company_id": co.id, "city_id":str(payload.get("city_id",co.city_id)), "role": payload.role, "wage": int(payload.wage), "hours": int(payload.hours), "guaranteed_hours": int(payload.get("guaranteed_hours",_default_guaranteed_hours(str(payload.role),int(payload.hours)))), "skill": int(payload.skill), "status": "open", "candidate_id": "", "posted_month": month, "expires_month": month + int(_p().acquisition_valid_months)})
			_event(s, "job_offer", "직종·시간·보수와 자금 한도가 있는 공개 채용", month, {"actor_id": actor.id, "company_id": co.id, "city_id": co.city_id})
		"apply_job":
			var offer: Dictionary = _find(s.lifecycle.job_offers, str(payload.offer_id))
			var employer: Dictionary = _find(s.companies, str(offer.company_id))
			if employer.is_empty() or employer.status != "active" or _cash(s, str(employer.id)) < int(offer.wage) * int(offer.hours): return {"ok": false, "error": "EMPLOYER_PAYROLL_UNAVAILABLE"}
			_employ(s, actor, employer, str(offer.role), int(offer.wage), int(offer.hours), month, int(offer.get("guaranteed_hours",_default_guaranteed_hours(str(offer.role),int(offer.hours)))))
			offer.status = "filled"
			offer.person_id = actor.id
		"hire_person":
			s.lifecycle.job_offers.append({"id": _new_id(s, "vacancy"), "company_id": co.id, "city_id":str(payload.get("city_id",co.city_id)), "candidate_id": payload.person_id, "role": payload.role, "wage": int(payload.wage), "hours": int(payload.hours), "guaranteed_hours": int(payload.get("guaranteed_hours",_default_guaranteed_hours(str(payload.role),int(payload.hours)))), "skill": 0, "status": "open", "posted_month": month, "expires_month": month + int(_p().acquisition_valid_months)})
			_event(s, "job_offer", "채용 제안·해당 인물이 동의해 지원해야 근로 계약 체결", month, {"actor_id": actor.id, "company_id": co.id, "city_id": co.city_id, "candidate_id": payload.person_id})
		"resign_job":
			if actor.role == "director": _clear_office(s, _find(s.companies, str(actor.company_id)), month, "대표 자진 사임")
			else: _end_employment(s, actor, month, "근로자 자진 사직")
		"fire_person": _end_employment(s, _find(s.people, str(payload.person_id)), month, "고용주가 근로 계약 종료")
		"start_training":
			s.lifecycle.training.append({"id": _new_id(s, "training"), "person_id": actor.id, "skill": payload.skill, "months_required": int(payload.months), "months_done": 0, "budget": int(payload.budget), "spent": 0, "status": "active", "start_month": month, "effective_month": 0, "reason": "교육 시작 대기"})
		"move_person":
			if not str(actor.company_id).is_empty(): _end_employment(s, actor, month, "도시 이주에 따른 근로 계약 종료")
			var recipient: String = _fee_recipient(s, str(payload.city_id), str(actor.id))
			if not _pay(s, "lifecycle-move-" + str(c.id), str(actor.id), recipient, int(_p().migration_fee), "거주지 변경 등록 서비스", month): return {"ok": false, "error": "MIGRATION_PAYMENT_FAILED"}
			var origin: String = str(actor.city_id)
			actor.city_id = payload.city_id
			_find(s.households, str(actor.household_id)).city_id = payload.city_id
			_event(s, "migration", "실제 이주비 지급·기존 직장 정리", month, {"actor_id": actor.id, "city_id": actor.city_id, "origin_city_id": origin, "amount": int(_p().migration_fee)})
		"apply_director":
			s.lifecycle.director_applications.append({"id": _new_id(s, "executive-application"), "company_id": payload.company_id, "person_id": actor.id, "hourly_wage": int(_p().executive_hourly_wage), "management_hours": int(_p().director_monthly_hours), "status": "applied", "created_month": month})
			_event(s, "director_application", "본인이 공개 직위 조건에 동의해 대표 직위에 지원", month, {"actor_id": actor.id, "company_id": payload.company_id, "city_id": actor.city_id})
		"appoint_director": _appoint(s, _find(s.companies, str(payload.company_id)), _find(s.people, str(payload.person_id)), month)
		"dismiss_director": _clear_office(s, _find(s.companies, str(payload.company_id)), month, "주주 의결로 대표 해임")
		"delegate_management":
			s.lifecycle.delegations.append({"id": _new_id(s, "delegation"), "company_id": co.id, "grantor_id": actor.id, "person_id": payload.person_id, "permissions": _permissions(payload), "budget": int(payload.budget), "spent": 0, "month": month, "status": "active"})
		"revoke_delegation": _find(s.lifecycle.delegations, str(payload.delegation_id)).status = "revoked"
		"offer_acquisition":
			var target: Dictionary = _find(s.companies, str(payload.target_company_id))
			var seller: String = _largest_owner(s, str(target.id))
			s.lifecycle.acquisitions.append({"id": _new_id(s, "acquisition"), "buyer_id": _buyer(s, c), "seller_id": seller, "company_id": target.id, "shares": shares_owned(s, seller, str(target.id)), "amount": int(payload.amount), "status": "offered", "created_month": month, "expires_month": month + int(_p().acquisition_valid_months)})
		"accept_acquisition":
			var offer: Dictionary = _find(s.lifecycle.acquisitions, str(payload.offer_id))
			var investment: String = _account(s, str(offer.buyer_id), "investment:" + str(offer.company_id), "asset")
			var seller_investment: String = _account(s, str(offer.seller_id), "investment:" + str(offer.company_id), "asset")
			var old_value: int = maxi(0, Ledger.balance(s, seller_investment))
			var gain: int = int(offer.amount) - old_value
			var lines: Array = [{"account": str(offer.buyer_id) + ":cash", "debit": 0, "credit": int(offer.amount)}, {"account": investment, "debit": int(offer.amount), "credit": 0}, {"account": str(offer.seller_id) + ":cash", "debit": int(offer.amount), "credit": 0}]
			if old_value > 0: lines.append({"account": seller_investment, "debit": 0, "credit": old_value})
			if gain > 0: lines.append({"account": _account(s, str(offer.seller_id), "capital_gain", "revenue"), "debit": 0, "credit": gain})
			elif gain < 0: lines.append({"account": _account(s, str(offer.seller_id), "capital_loss", "expense"), "debit": -gain, "credit": 0})
			var payment: Dictionary = Ledger.post(s, "lifecycle-acquire-" + str(c.id), lines, "유통 지분 인수·회사 매출과 분리", month)
			if not payment.ok: return payment
			var remaining: int = int(offer.shares)
			for holding in s.ownership:
				if holding.company_id == offer.company_id and holding.owner_id == offer.seller_id:
					var amount: int = mini(remaining, int(holding.shares))
					holding.shares -= amount
					holding.voting_shares = maxi(0, int(holding.voting_shares) - amount)
					remaining -= amount
			s.ownership.append({"company_id": offer.company_id, "owner_id": offer.buyer_id, "shares": int(offer.shares), "voting_shares": int(offer.shares)})
			offer.status = "accepted"
			var acquired: Dictionary = _find(s.companies, str(offer.company_id))
			if _controls(s, str(offer.buyer_id), str(acquired.id)): acquired.shareholder_id = offer.buyer_id
			_event(s, "acquisition", "대금은 매도 주주에게 지급·법인 계약과 자산 소유자 유지", month, {"actor_id": offer.buyer_id, "company_id": acquired.id, "city_id": acquired.city_id, "amount": int(offer.amount)})
		"declare_bankruptcy": _open_liquidation(s, co, month)
		"buy_liquidation_asset":
			var result: Dictionary = _buy_asset(s, co, _find(s.lifecycle.liquidation_assets, str(payload.asset_id)), str(c.id), month)
			if not result.ok: return result
		"appoint_successor":
			actor.successor_id = payload.person_id
			s.lifecycle.successions.append({"id": _new_id(s, "succession"), "person_id": actor.id, "successor_id": payload.person_id, "status": "nominated", "month": month})
		"retire_person":
			if not str(actor.company_id).is_empty():
				if actor.role == "director": _clear_office(s, _find(s.companies, str(actor.company_id)), month, "은퇴로 대표 직위 종료")
				else: _end_employment(s, actor, month, "개인 은퇴")
			actor.career_status = "retired"
			actor.retired_month = month
			# Retirement changes viewpoint, never gifts a second copy of wealth.
			if s.player_id == actor.id: _switch_player(s, str(actor.successor_id))
			_event(s, "retirement", "은퇴·재산과 채무의 소유자는 유지", month, {"actor_id": actor.id, "city_id": actor.city_id})
		"change_player": _switch_player(s, str(payload.person_id))
	_event(s, "lifecycle_command", "인물·법인 행위 처리", month, {"actor_id": c.actor_id, "company_id": c.acting_company_id, "city_id": actor.city_id, "command_id": c.id, "action": c.type})
	return {"ok": true, "error": ""}

static func _switch_player(s: Dictionary, person_id: String) -> void:
	s.player_id = person_id
	s.controlled_company_id = ""
	for co in s.companies:
		if co.get("director_id") == person_id and co.get("status", "active") == "active":
			s.controlled_company_id = co.id
			break

static func _open_liquidation(s: Dictionary, co: Dictionary, month: int) -> void:
	co.status = "liquidating"
	# Release the estate's confirmed sell reservations and return its own buy
	# escrow before determining which actual assets can be sold by the estate.
	if s.has("market"):
		var market: Variant = load("res://core/market.gd")
		for order in s.market.orders:
			if order.owner_id == co.id and order.status == "open": market._close(s, order, "owner_liquidation", month)
	_clear_office(s, co, month, "法人 청산으로 대표 계약 종료")
	for person in s.people:
		if person.company_id == co.id: _end_employment(s, person, month, "청산으로 고용 계약 종료")
	for offer in s.lifecycle.job_offers:
		if offer.company_id == co.id and offer.status == "open": offer.status = "cancelled"
	for project in s.projects:
		if project.company_id == co.id and project.status in ["active", "paused"]:
			project.status = "cancelled"
			project.reason = "발주 법인 청산·사용 자재 환불 없음"
	var estate: Dictionary = {"id": _new_id(s, "estate"), "company_id": co.id, "opened_month": month, "status": "liquidating", "distributed": 0}
	s.lifecycle.liquidations.append(estate)
	for facility in s.facilities:
		if facility.company_id == co.id:
			facility.status = "closed"
			s.lifecycle.liquidation_assets.append({"id": _new_id(s, "asset"), "estate_id": co.id, "kind": "facility", "reference_id": facility.id, "city_id": facility.city_id, "name": str(facility.name), "price": int(_p().liquidation_facility_price), "status": "listed"})
	for lot in s.inventory:
		if lot.owner_id == co.id and int(lot.quantity) > 0 and lot.location != "shipment":
			var product: Dictionary = _find(s.products, str(lot.product_id))
			var price: int = maxi(1, int(int(lot.quantity) * int(product.price) * int(_p().liquidation_stock_discount_percent) / 100))
			s.lifecycle.liquidation_assets.append({"id": _new_id(s, "asset"), "estate_id": co.id, "kind": "inventory", "reference_id": lot.id, "city_id": lot.city_id, "name": str(product.name), "quantity": int(lot.quantity), "price": price, "status": "listed"})
	_refresh_estate_land(s, str(co.id))
	_event(s, "bankruptcy", "법인 자산·채무 보존·직원 계약 정리·유상 청산 매각 개시", month, {"actor_id": co.id, "city_id": co.city_id})

static func _buy_asset(s: Dictionary, buyer: Dictionary, listing: Dictionary, command_id: String, month: int) -> Dictionary:
	var seller: String = str(listing.estate_id)
	if listing.kind == "land":
		var land: Variant = load("res://core/land.gd")
		var result: Dictionary = land.transfer_titles(s, seller, str(buyer.id), str(listing.city_id), listing.rect, int(listing.price), month, "estate-" + command_id)
		if not result.get("ok", false): return result
		listing.status = "sold"
		listing.buyer_id = buyer.id
		listing.sold_month = month
		_event(s, "asset_disposal", "청산 토지의 실제 대금과 권리 이전·임차권 유지", month, {"actor_id":buyer.id,"seller_id":seller,"city_id":listing.city_id,"amount":int(listing.price)})
		return {"ok":true,"error":""}
	var reference: Dictionary = _find(s.facilities, str(listing.reference_id)) if listing.kind == "facility" else _find(s.inventory, str(listing.reference_id))
	var suffix: String = "facility:" + str(reference.get("project_id", listing.reference_id)) if listing.kind == "facility" else "stock:" + str(reference.get("product_id", ""))
	var seller_asset: String = _account(s, seller, suffix, "asset")
	var buyer_asset: String = _account(s, str(buyer.id), suffix, "asset")
	var book: int = maxi(0, Ledger.balance(s, seller_asset))
	if listing.kind == "inventory":
		var held: int = 0
		for owned in s.inventory:
			if owned.owner_id == seller and owned.product_id == reference.product_id: held += int(owned.quantity)
		book = int(book * int(listing.quantity) / held) if held > 0 else 0
	var price: int = int(listing.price)
	var lines: Array = [{"account": str(buyer.id) + ":cash", "debit": 0, "credit": price}, {"account": buyer_asset, "debit": price, "credit": 0}, {"account": seller + ":cash", "debit": price, "credit": 0}]
	if book > 0: lines.append({"account": seller_asset, "debit": 0, "credit": book})
	if price > book: lines.append({"account": _account(s, seller, "capital_gain", "revenue"), "debit": 0, "credit": price - book})
	elif book > price: lines.append({"account": _account(s, seller, "capital_loss", "expense"), "debit": book - price, "credit": 0})
	var payment: Dictionary = Ledger.post(s, "lifecycle-asset-" + command_id, lines, "청산 자산 취득·소유권 이전", month)
	if not payment.ok: return payment
	if listing.kind == "facility":
		var facility: Dictionary = _find(s.facilities, str(listing.reference_id))
		if s.has("land"):
			var land: Variant = load("res://core/land.gd")
			var conveyed: Dictionary = land.convey_facility_titles(s, seller, str(buyer.id), facility, price, month, command_id)
			if not conveyed.get("ok", false): return conveyed
		facility.company_id = buyer.id
		facility.status = "ready"
		facility.operational_month = month + 1
	else:
		var lot: Dictionary = _find(s.inventory, str(listing.reference_id))
		var quantity: int = int(listing.quantity)
		if int(lot.quantity) < quantity: return {"ok": false, "error": "LIQUIDATION_STOCK_CHANGED"}
		lot.quantity = int(lot.quantity) - quantity
		_event(s, "asset_inventory_out", "청산 매각 출고·물리적 보관 장소 유지", month, {"actor_id": seller, "city_id": lot.city_id, "product_id": lot.product_id, "quantity": quantity, "inventory_delta": -quantity, "lot_id": lot.id})
		# Preserve the purchased batch's age, expiry, specification and quality.
		# A distinct lot prevents mixing it with a newer buyer batch.
		var target: Dictionary = lot.duplicate(true)
		target.id = "lot:estate:" + command_id
		target.owner_id = buyer.id
		target.accounting_city_id = lot.city_id
		target.location = "warehouse"
		target.quantity = quantity
		target.reserved = 0
		target.unit_cost = int(price / maxi(1, quantity))
		target.acquisition_cost_total = price
		s.inventory.append(target)
		Domain.register_lot(s, target)
		_event(s, "asset_inventory_in", "청산 자산 인수·새 소유자 창고 재고", month, {"actor_id": buyer.id, "city_id": target.city_id, "product_id": target.product_id, "quantity": quantity, "inventory_delta": quantity, "lot_id": target.id})
	listing.status = "sold"
	listing.buyer_id = buyer.id
	listing.sold_month = month
	_event(s, "asset_disposal", "실제 대금과 시설·재고 소유권 이전", month, {"actor_id": buyer.id, "seller_id": seller, "city_id": listing.city_id, "amount": price})
	return {"ok": true}

static func monthly(s: Dictionary, month: int) -> void:
	if not s.has("lifecycle"): return
	for delegation in s.lifecycle.delegations:
		if int(delegation.get("month", -1)) != month:
			delegation.spent = 0
			delegation.month = month
		var company: Dictionary = _find(s.companies, str(delegation.company_id))
		if company.is_empty() or company.get("director_id") != delegation.grantor_id: delegation.status = "revoked"
		if delegation.status == "active":
			var manager: Dictionary = _find(s.people, str(delegation.person_id))
			if manager.is_empty() or manager.company_id != delegation.company_id:
				delegation.status = "revoked"
			else:
				var used: int = int(s.monthly_labor.get(manager.id, 0))
				var hours: int = int(_p().delegation_monthly_hours)
				var wage: int = hours * int(manager.wage)
				if int(manager.hours) - used >= hours and _cash(s, str(company.id)) >= wage and _pay(s, "lifecycle-delegated-%d-%s" % [month, delegation.id], str(company.id), str(manager.id), wage, "위임 관리 업무의 계약 임금", month):
					s.monthly_labor[manager.id] = used + hours
					manager.last_income = int(manager.get("last_income", 0)) + wage
					_event(s, "delegated_management", "직원의 실제 작업시간으로 위임 관리 수행", month, {"actor_id": manager.id, "company_id": delegation.company_id, "city_id": manager.city_id, "hours": hours,"amount":wage,"wage_paid":true})
	for offer in s.lifecycle.job_offers:
		if offer.status == "open" and month > int(offer.expires_month): offer.status = "expired"
	for offer in s.lifecycle.acquisitions:
		if offer.status == "offered" and month > int(offer.expires_month): offer.status = "expired"
	for training in s.lifecycle.training:
		var person: Dictionary = _find(s.people, str(training.person_id))
		if person.is_empty(): continue
		if training.status == "completed_pending" and month >= int(training.effective_month):
			person.skills[training.skill] = mini(int(_p().maximum_skill), int(person.skills.get(training.skill, 0)) + 1)
			training.status = "completed"
			_event(s, "education_complete", "교육 기간·수강료·수업 시간을 충족해 다음 월 숙련 적용", month, {"actor_id": person.id, "city_id": person.city_id, "skill": training.skill})
		if training.status != "active": continue
		var hours: int = int(_p().training_hours)
		var used: int = int(s.get("monthly_labor", {}).get(person.id, 0))
		var instructor: Dictionary = _teacher(s, person, str(training.skill), hours)
		if instructor.is_empty() or int(person.hours) - used < hours:
			training.reason = "교육 인력 또는 가용 시간 부족"
			continue
		var fee: int = int(_p().training_monthly_fee)
		if int(training.spent) + fee > int(training.budget) or _cash(s, str(person.id)) < fee:
			training.reason = "수강료 예산 또는 현금 부족"
			continue
		var teacher_pay: int = hours * int(instructor.wage)
		if _cash(s, str(instructor.company_id)) + fee < teacher_pay:
			training.reason = "교육 제공자의 강사 보수 지급 능력 부족"
			continue
		if not _pay(s, "lifecycle-tuition-%d-%s" % [month, training.id], str(person.id), str(instructor.company_id), fee, "직무 교육 서비스", month): continue
		if teacher_pay > 0 and not _pay(s, "lifecycle-teacher-%d-%s" % [month, training.id], str(instructor.company_id), str(instructor.id), teacher_pay, "실제 강의 시간의 계약 임금", month): continue
		s.monthly_labor[person.id] = used + hours
		s.monthly_labor[instructor.id] = int(s.monthly_labor.get(instructor.id, 0)) + hours
		instructor.last_income = int(instructor.get("last_income", 0)) + teacher_pay
		_event(s,"labor","실제 유급 강의 노동",month,{"actor_id":instructor.id,"company_id":instructor.company_id,"city_id":instructor.city_id,"hours":hours,"amount":teacher_pay,"wage_paid":true})
		training.spent = int(training.spent) + fee
		training.months_done = int(training.months_done) + 1
		training.reason = "실제 강사·수업 시간과 수강료 투입"
		if int(training.months_done) >= int(training.months_required):
			training.status = "completed_pending"
			training.effective_month = month + 1
		_event(s, "education", training.reason, month, {"actor_id": person.id, "city_id": person.city_id, "hours": hours, "amount": fee})
	for estate in s.lifecycle.liquidations:
		if estate.status != "liquidating": continue
		var owner: String = str(estate.company_id)
		var debt: int = _debt(s, owner)
		if debt > 0 and _cash(s, owner) > 0:
			for priority in _cfg().get("liquidation_priority", []):
				if priority == "wage_claims": estate.distributed = int(estate.distributed) + settle_wage_claims(s,owner,_cash(s,owner),month)
				if priority == "public_claims" and ResourceLoader.exists("res://core/public_sector.gd"):
					var public: Variant = load("res://core/public_sector.gd")
					if public != null and public.has_method("settle_claims"):
						var paid: Dictionary = public.settle_claims(s, owner, _cash(s, owner), month)
						estate.distributed = int(estate.distributed) + int(paid.get("paid", 0))
				if priority == "land_claims" and s.has("land"):
					var land: Variant = load("res://core/land.gd")
					estate.distributed = int(estate.distributed) + int(land.settle_debt(s, owner, _cash(s, owner), month))
				if priority == "financial_contracts" and ResourceLoader.exists("res://core/finance.gd"):
					var finance: Variant = load("res://core/finance.gd")
					if finance != null and finance.has_method("settle_debt"): estate.distributed = int(estate.distributed) + int(finance.settle_debt(s, owner, _cash(s, owner), month))
		_refresh_estate_inventory(s, owner, month)
		_refresh_estate_land(s, owner)
		var listed: bool = false
		if s.has("land"):
			for title_owner in s.land.titles.values():
				if title_owner == owner: listed = true; break
		for asset in s.lifecycle.liquidation_assets:
			if asset.estate_id == owner and asset.status == "listed": listed = true
		if not listed and _debt(s, owner) == 0 and month - int(estate.opened_month) >= int(_p().liquidation_notice_months):
			_distribute_estate(s, owner, month)
			_find(s.companies, owner).status = "closed"
			estate.status = "closed"

static func _refresh_estate_inventory(s: Dictionary, owner: String, month: int) -> void:
	var active: Dictionary = {}
	for asset in s.lifecycle.liquidation_assets:
		if asset.estate_id != owner or asset.kind != "inventory" or asset.status != "listed": continue
		var lot: Dictionary = _find(s.inventory, str(asset.reference_id))
		var available: int = maxi(0, int(lot.get("quantity", 0)) - int(lot.get("reserved", 0))) if lot.get("owner_id") == owner and lot.get("location") != "shipment" else 0
		if available == 0:
			asset.status = "lost"
			_event(s, "estate_stock_unavailable", "실제 재고 손실·소진에 따라 청산 매각 목록 종료·현금 환급 없음", month, {"actor_id":owner,"city_id":asset.city_id,"asset_id":asset.id,"lot_id":asset.reference_id})
			continue
		if available != int(asset.quantity):
			asset.price = maxi(1, int(int(asset.price) * available / maxi(1, int(asset.quantity))))
			asset.quantity = available
			_event(s, "estate_stock_requoted", "청산 재고의 실제 잔량에 맞춰 매각 수량과 대금 갱신", month, {"actor_id":owner,"city_id":asset.city_id,"asset_id":asset.id,"lot_id":asset.reference_id,"quantity":available,"amount":asset.price})
		active[str(asset.reference_id)] = true
	# An already-owned incoming shipment or a remainder after a partial estate
	# sale is a real asset too. Arrival never generates recovery money.
	for lot in s.inventory:
		if lot.owner_id != owner or lot.location == "shipment" or active.has(str(lot.id)): continue
		var quantity: int = maxi(0, int(lot.quantity) - int(lot.get("reserved", 0)))
		if quantity <= 0: continue
		var product: Dictionary = _find(s.products, str(lot.product_id))
		var price: int = maxi(1, int(quantity * int(product.price) * int(_p().liquidation_stock_discount_percent) / 100))
		s.lifecycle.liquidation_assets.append({"id":_new_id(s,"asset"),"estate_id":owner,"kind":"inventory","reference_id":lot.id,"city_id":lot.city_id,"name":str(product.name),"quantity":quantity,"price":price,"status":"listed"})

static func _refresh_estate_land(s: Dictionary, owner: String) -> void:
	if not s.has("land"): return
	var land: Variant = load("res://core/land.gd")
	if land == null or not land.has_method("estate_assets"): return
	for parcel in land.estate_assets(s, owner):
		var overlaps: bool = false
		for listed in s.lifecycle.liquidation_assets:
			if listed.estate_id != owner or listed.kind != "land" or listed.status != "listed" or listed.city_id != parcel.city_id: continue
			var a: Dictionary = listed.rect
			var b: Dictionary = parcel.rect
			if int(a.x) < int(b.x)+int(b.w) and int(b.x) < int(a.x)+int(a.w) and int(a.y) < int(b.y)+int(b.h) and int(b.y) < int(a.y)+int(a.h): overlaps = true; break
		if not overlaps:
			s.lifecycle.liquidation_assets.append({"id":_new_id(s,"asset"),"estate_id":owner,"kind":"land","reference_id":"land:"+str(parcel.city_id)+":"+str(parcel.rect),"city_id":parcel.city_id,"rect":parcel.rect.duplicate(true),"name":parcel.name,"price":int(parcel.price),"status":"listed"})

static func _default_guaranteed_hours(role: String, hours: int) -> int:
	if role == "director": return mini(hours, int(_p().director_guaranteed_hours))
	return mini(hours, int(hours * int(_p().guaranteed_hour_percent) / 100))

static func _paid_labor_index(s: Dictionary, month: int) -> Dictionary:
	var paid: Dictionary = {}
	for event in s.get("events", []):
		if int(event.get("month", -1)) != month or not event.get("wage_paid", false): continue
		if event.get("type") not in ["labor", "delegated_management"]: continue
		var key: String = str(event.get("company_id", "")) + "|" + str(event.get("actor_id", ""))
		paid[key] = int(paid.get(key, 0)) + maxi(0, int(event.get("amount", 0)))
	return paid

static func _floor_share(amount: int, weight: int, total: int) -> int:
	# Exact floor(amount * weight / total) without multiplying two money values.
	# Remainders stay below 2*MAX_INTEGER, well inside signed 64-bit arithmetic.
	if amount <= 0 or weight <= 0 or total <= 0: return 0
	var base: int = int(amount / total) * weight
	amount = amount % total
	var bit: int = 1
	while bit <= weight: bit <<= 1
	bit >>= 1
	var quotient: int = 0
	var remainder: int = 0
	while bit > 0:
		quotient *= 2
		remainder *= 2
		if remainder >= total: remainder -= total; quotient += 1
		if weight & bit:
			remainder += amount
			if remainder >= total: remainder -= total; quotient += 1
		bit >>= 1
	return base + quotient

static func _wage_allocations(s: Dictionary, owner: String, claims: Array, cash_budget: int, month: int) -> Array:
	var pending: Array = claims.filter(func(claim): return claim.employer_id == owner and int(claim.remaining) > 0)
	var total: int = 0
	for claim in pending: total += int(claim.remaining)
	if total <= 0 or cash_budget <= 0: return []
	var budget: int = mini(cash_budget, total)
	pending.sort_custom(func(a,b):return (str(s.seed)+"|payroll|"+str(month)+"|"+str(a.id)).sha256_text() < (str(s.seed)+"|payroll|"+str(month)+"|"+str(b.id)).sha256_text())
	var result: Array = []
	var used: int = 0
	for claim in pending:
		var amount: int = _floor_share(budget, int(claim.remaining), total)
		result.append({"claim":claim,"amount":amount})
		used += amount
	for row in result:
		if used >= budget: break
		if int(row.amount) < int(row.claim.remaining): row.amount = int(row.amount) + 1; used += 1
	return result

static func _wage_lines(ledger: Dictionary, claim: Dictionary, amount: int, accrual: bool) -> Array:
	if amount <= 0: return []
	var owner: String = str(claim.employer_id)
	var employee: String = str(claim.person_id)
	var payable: String = _account(ledger, owner, "wage_payable:" + employee, "liability")
	var receivable: String = _account(ledger, employee, "wage_receivable:" + owner, "asset")
	if accrual:
		return [{"account":_account(ledger,owner,"idle_payroll","expense"),"debit":amount,"credit":0},{"account":payable,"debit":0,"credit":amount},{"account":receivable,"debit":amount,"credit":0},{"account":_account(ledger,employee,"wage_income","revenue"),"debit":0,"credit":amount}]
	return [{"account":owner+":cash","debit":0,"credit":amount},{"account":payable,"debit":amount,"credit":0},{"account":employee+":cash","debit":amount,"credit":0},{"account":receivable,"debit":0,"credit":amount}]

static func _record_wage_payment(s: Dictionary, claim: Dictionary, amount: int, month: int) -> void:
	if amount <= 0: return
	claim.remaining = int(claim.remaining) - amount
	claim.paid = int(claim.get("paid",0)) + amount
	claim.status = "paid" if int(claim.remaining) == 0 else "partial"
	var employee: Dictionary = _find(s.people,str(claim.person_id))
	if not employee.is_empty(): employee.last_income = int(employee.get("last_income",0)) + amount
	_event(s,"wage_payment","실제 회사 자금으로 계약 보장 임금·임금 체불 채권 지급",month,{"actor_id":claim.employer_id,"payer_id":claim.employer_id,"payee_id":claim.person_id,"city_id":claim.city_id,"amount":amount,"claim_id":claim.id,"employment_id":claim.employment_id})

static func pay_contractual_wages(s: Dictionary, month: int) -> Dictionary:
	if not s.has("lifecycle") or not s.lifecycle.get("wage_claims") is Array: return {"ok":false,"error":"PAYROLL_NOT_INITIALIZED"}
	var key: String = str(month)
	if s.lifecycle.payroll_months.has(key): return s.lifecycle.payroll_months[key].duplicate(true)
	var paid_work: Dictionary = _paid_labor_index(s,month)
	var new_claims: Array = []
	var accruals: Dictionary = {}
	var employers: Dictionary = {}
	var accrued: int = 0
	for contract in s.lifecycle.employment:
		if contract.status != "active" or int(contract.start_month) > month: continue
		var employer: Dictionary = _find(s.companies,str(contract.company_id))
		if employer.is_empty() or employer.get("status","active") != "active": continue
		var accrual_key: String = str(contract.id) + "@" + key
		if s.lifecycle.payroll_accruals.has(accrual_key): continue
		var guaranteed: int = mini(int(contract.hours),int(contract.get("guaranteed_hours",_default_guaranteed_hours(str(contract.role),int(contract.hours)))))
		var expected: int = guaranteed * int(contract.wage)
		var actual: int = int(paid_work.get(str(contract.company_id)+"|"+str(contract.person_id),0))
		var due: int = maxi(0,expected-actual)
		var record: Dictionary = {"employment_id":contract.id,"employer_id":contract.company_id,"person_id":contract.person_id,"month":month,"guaranteed_hours":guaranteed,"guaranteed_amount":expected,"actual_paid":actual,"top_up_due":due}
		accruals[accrual_key] = record
		if due <= 0: continue
		var claim: Dictionary = {"id":"wage-"+str(contract.id)+"-"+key,"employment_id":contract.id,"employer_id":contract.company_id,"person_id":contract.person_id,"city_id":contract.get("city_id",employer.city_id),"month":month,"amount":due,"remaining":due,"paid":0,"status":"unpaid"}
		new_claims.append(claim)
		accrued += due
	var all_claims: Array = s.lifecycle.wage_claims.duplicate()
	all_claims.append_array(new_claims)
	for claim in all_claims:
		if int(claim.remaining)>0: employers[str(claim.employer_id)] = true
	# Stage only account balances. Ledger.post checks every accrual and payment
	# before it appends the one shared journal row, so no partial payroll commits.
	var ledger: Dictionary = {"accounts":s.accounts.duplicate(true),"journal":s.journal,"journal_index":s.journal_index}
	var lines: Array = []
	for claim in new_claims: lines.append_array(_wage_lines(ledger,claim,int(claim.amount),true))
	var payments: Array = []
	var owners: Array = employers.keys()
	owners.sort()
	for owner in owners:
		for row in _wage_allocations(s,str(owner),all_claims,_cash(s,str(owner)),month):
			if int(row.amount)>0: payments.append(row); lines.append_array(_wage_lines(ledger,row.claim,int(row.amount),false))
	if not lines.is_empty():
		var posting: Dictionary = Ledger.post(ledger,"contractual-payroll-"+key,lines,"고용 계약의 보장 임금 발생·한정 자금 비례 지급",month)
		if not posting.get("ok",false): return posting
		s.accounts = ledger.accounts
	s.lifecycle.payroll_accruals.merge(accruals,true)
	s.lifecycle.wage_claims.append_array(new_claims)
	for claim in new_claims:
		var employer: Dictionary = _find(s.companies,str(claim.employer_id))
		if not employer.is_empty(): employer.expenses = int(employer.get("expenses",0)) + int(claim.amount)
		_event(s,"wage_accrual","생산 여부와 별개인 고용 계약의 보장 유급시간·실제 지급 임금 차액",month,{"actor_id":claim.employer_id,"person_id":claim.person_id,"city_id":claim.city_id,"amount":claim.amount,"claim_id":claim.id,"employment_id":claim.employment_id})
	var paid: int = 0
	for row in payments: _record_wage_payment(s,row.claim,int(row.amount),month); paid += int(row.amount)
	var remaining: int = 0
	for claim in s.lifecycle.wage_claims:
		remaining += int(claim.remaining)
		if int(claim.remaining)>0:
			_event(s,"wage_arrears","지급 가능한 회사 현금 부족·직원 채권과 회사 채무 유지",month,{"actor_id":claim.employer_id,"person_id":claim.person_id,"city_id":claim.city_id,"amount":claim.remaining,"claim_id":claim.id})
	var result: Dictionary = {"ok":true,"error":"","month":month,"paid":paid,"accrued":accrued,"remaining":remaining}
	s.lifecycle.payroll_months[key] = result.duplicate(true)
	return result

static func settle_wage_claims(s: Dictionary, owner: String, cash_budget: int, month: int) -> int:
	var payments: Array = _wage_allocations(s,owner,s.lifecycle.wage_claims,mini(maxi(0,cash_budget),_cash(s,owner)),month)
	var lines: Array = []
	var ledger: Dictionary = {"accounts":s.accounts.duplicate(true),"journal":s.journal,"journal_index":s.journal_index}
	var paid: int = 0
	for row in payments:
		if int(row.amount)>0: lines.append_array(_wage_lines(ledger,row.claim,int(row.amount),false)); paid += int(row.amount)
	if lines.is_empty(): return 0
	var posting: Dictionary = Ledger.post(ledger,"wage-settlement-%d-%s-%d" % [month,owner,s.journal.size()],lines,"청산·체불 임금 실제 지급·기존 채권 채무 상환",month)
	if not posting.get("ok",false): return 0
	s.accounts = ledger.accounts
	for row in payments: _record_wage_payment(s,row.claim,int(row.amount),month)
	return paid

static func _debt(s: Dictionary, owner: String) -> int:
	# Public procurement advances and taxes are debts too. Inspect the common
	# ledger, rather than treating only bank loans as liquidation obligations.
	var debt: int = 0
	for account in s.accounts.values():
		if account.get("owner") == owner and account.get("kind") == "liability": debt += maxi(0, -int(account.balance))
	return debt

static func _distribute_estate(s: Dictionary, owner: String, month: int) -> void:
	var available: int = _cash(s, owner)
	if available <= 0:
		for holding in s.ownership:
			if holding.company_id != owner or int(holding.shares) <= 0: continue
			var investment: String = _account(s, str(holding.owner_id), "investment:" + owner, "asset")
			var loss: int = maxi(0, Ledger.balance(s, investment))
			if loss > 0:
				Ledger.post(s, "lifecycle-equity-loss-%d-%s-%s" % [month, owner, holding.owner_id], [{"account": investment, "debit": 0, "credit": loss}, {"account": _account(s, str(holding.owner_id), "capital_loss", "expense"), "debit": loss, "credit": 0}], "청산 잔여 자산 없음·소유자의 지분 손실", month)
			holding.shares = 0
			holding.voting_shares = 0
		return
	var total: int = 0
	for holding in s.ownership:
		if holding.company_id == owner: total += int(holding.shares)
	if total <= 0: return
	var distributed: int = 0
	var holdings: Array = s.ownership.filter(func(h): return h.company_id == owner and int(h.shares) > 0)
	holdings.sort_custom(func(a, b): return str(a.owner_id) < str(b.owner_id))
	for index in range(holdings.size()):
		var holding: Dictionary = holdings[index]
		var amount: int = available - distributed if index == holdings.size() - 1 else int(available * int(holding.shares) / total)
		if amount <= 0: continue
		var equity: String = _account(s, owner, "equity", "equity")
		var investment: String = _account(s, str(holding.owner_id), "investment:" + owner, "asset")
		var book: int = maxi(0, Ledger.balance(s, investment))
		var lines: Array = [{"account": owner + ":cash", "debit": 0, "credit": amount}, {"account": equity, "debit": amount, "credit": 0}, {"account": str(holding.owner_id) + ":cash", "debit": amount, "credit": 0}]
		if book > 0: lines.append({"account": investment, "debit": 0, "credit": book})
		if amount > book: lines.append({"account": _account(s, str(holding.owner_id), "capital_gain", "revenue"), "debit": 0, "credit": amount - book})
		elif book > amount: lines.append({"account": _account(s, str(holding.owner_id), "capital_loss", "expense"), "debit": book - amount, "credit": 0})
		var payment: Dictionary = Ledger.post(s, "lifecycle-liquidation-%d-%s-%s" % [month, owner, holding.owner_id], lines, "청산 후 잔여 재산 분배·현금 복제 없음", month)
		if payment.ok:
			distributed += amount
			holding.shares = 0
			holding.voting_shares = 0

static func _command(s: Dictionary, person: String, company: String, type: String, payload: Dictionary) -> Dictionary:
	return {"id": "ai-life-%d-%s-%s" % [int(s.planning_month), person, type], "actor_id": person, "acting_company_id": company, "expected_version": int(s.version), "type": type, "payload": payload}

static func _free_site(s: Dictionary, city_id: String, owner: String = "") -> Dictionary:
	var city: Dictionary = _find(s.cities, city_id)
	for y in range(int(city.get("height", 0)) - 1):
		for x in range(int(city.get("width", 0)) - 1):
			var occupied: bool = false
			for road in city.roads:
				if int(road.x) >= x and int(road.x) < x + 2 and int(road.y) >= y and int(road.y) < y + 2: occupied = true
			for facility in s.facilities:
				if facility.city_id == city_id and x < int(facility.x) + int(facility.w) and x + 2 > int(facility.x) and y < int(facility.y) + int(facility.h) and y + 2 > int(facility.y): occupied = true
			for project in s.projects:
				if project.city_id == city_id and project.status in ["active", "paused"] and x < int(project.x) + 2 and x + 2 > int(project.x) and y < int(project.y) + 2 and y + 2 > int(project.y): occupied = true
			if occupied: continue
			if owner != "" and s.has("land"):
				var land: Variant = load("res://core/land.gd")
				if land != null and not land.can_build(s, owner, city_id, {"x": x, "y": y, "w": 2, "h": 2}): continue
			for road in city.roads:
				if (int(road.x) >= x and int(road.x) < x + 2 and int(road.y) in [y - 1, y + 2]) or (int(road.y) >= y and int(road.y) < y + 2 and int(road.x) in [x - 1, x + 2]): return {"x": x, "y": y}
	return {}

static func ai_commands(s: Dictionary) -> Array:
	var commands: Array = []
	if not s.has("lifecycle"): return commands
	var p: Dictionary = _p()
	var people: Array = s.people.duplicate()
	people.sort_custom(func(a, b): return str(a.id) < str(b.id))
	for person in people:
		if person.id == s.player_id or person.get("career_status", "active") != "active": continue
		var own_cash: int = _cash(s, str(person.id))
		var appointed: bool = false
		for target in s.companies:
			if target.get("status", "active") != "active" or not str(target.get("director_id", "")).is_empty(): continue
			var acting: String = ""
			var controls: bool = _controls(s, str(person.id), str(target.id))
			if not controls and person.role == "director" and _controls(s, str(person.company_id), str(target.id)):
				controls = true
				acting = str(person.company_id)
			if not controls: continue
			for application in s.lifecycle.director_applications:
				if application.company_id != target.id or application.status != "applied": continue
				var command: Dictionary = _command(s, str(person.id), acting, "appoint_director", {"company_id": target.id, "person_id": application.person_id})
				if validate_command(s, command).is_empty():
					commands.append(command)
					appointed = true
					break
			if appointed: break
		if appointed: continue
		if person.role != "director" and int(person.skills.get("management", 0)) >= int(p.minimum_executive_skill):
			for target in s.companies:
				if target.get("status", "active") != "active" or not str(target.get("director_id", "")).is_empty() or target.city_id != person.city_id: continue
				var command: Dictionary = _command(s, str(person.id), "", "apply_director", {"company_id": target.id})
				if validate_command(s, command).is_empty():
					commands.append(command)
					appointed = true
					break
		if appointed: continue
		if person.role == "director":
			var own: Dictionary = _find(s.companies, str(person.company_id))
			if own.is_empty() or own.get("status", "active") != "active": continue
			var facilities: Array = s.facilities.filter(func(f): return f.company_id == own.id and f.status != "closed")
			if _cash(s, str(own.id)) >= int(p.ai_liquidation_reserve) and int(s.planning_month) % int(p.ai_reconsider_period) == 0:
				var acquired_asset: bool = false
				for listing in s.lifecycle.liquidation_assets:
					if listing.status != "listed" or listing.estate_id == own.id or listing.city_id != own.city_id: continue
					var buy: Dictionary = _command(s, str(person.id), str(own.id), "buy_liquidation_asset", {"asset_id": listing.id})
					if validate_command(s, buy).is_empty():
						commands.append(buy)
						acquired_asset = true
						break
				if acquired_asset: continue
			if facilities.is_empty() and _cash(s, str(own.id)) >= int(p.ai_startup_capital) / 2:
				var site: Dictionary = _free_site(s, str(own.city_id), str(own.id))
				if not site.is_empty(): commands.append(_command(s, str(person.id), str(own.id), "build", {"kind": own.get("business_kind", "bakery"), "city_id": own.city_id, "x": site.x, "y": site.y}))
			var employees: int = 0
			for candidate in s.people:
				if candidate.company_id == own.id and candidate.role != "director": employees += 1
			var desired: int = facilities.size() * int(p.employees_per_facility)
			if employees < desired:
				var role: String = "operator"
				for facility in facilities:
					if facility.get("kind") == "builders": role = "builder"
				var wage: int = mini(int(p.ai_maximum_wage), int(p.default_wage) + int(p.ai_hiring_wage_step))
				var command: Dictionary = _command(s, str(person.id), str(own.id), "post_job", {"role": role, "wage": wage, "hours": int(p.default_hours), "skill": 0})
				if validate_command(s, command).is_empty(): commands.append(command)
			if _cash(s, str(own.id)) < int(p.registration_fee) and int(own.get("last_profit", 0)) < 0:
				var command: Dictionary = _command(s, str(person.id), str(own.id), "declare_bankruptcy", {})
				if validate_command(s, command).is_empty(): commands.append(command)
			continue
		# Job comparison reads public vacancy terms, own qualifications and money.
		var best: Dictionary = {}
		for offer in s.lifecycle.job_offers:
			if offer.status != "open" or offer.city_id != person.city_id or offer.company_id == person.company_id: continue
			if str(offer.get("candidate_id", "")) not in ["", str(person.id)]: continue
			if int(offer.hours) > int(person.capacity_hours) or int(person.skills.get(_role_skill(str(offer.role)), 0)) < int(offer.skill): continue
			if int(offer.wage) >= int(person.wage) + int(p.ai_wage_improvement) and (best.is_empty() or int(offer.wage) > int(best.wage)): best = offer
		if not best.is_empty():
			commands.append(_command(s, str(person.id), "", "apply_job", {"offer_id": best.id}))
			continue
		var decision_key: int = str(person.id).sha256_text().substr(0, 6).hex_to_int()
		if (int(s.planning_month) + decision_key) % int(p.ai_reconsider_period) != 0: continue
		if own_cash >= int(p.ai_startup_cash) and not _free_site(s, str(person.city_id)).is_empty():
			var command: Dictionary = _command(s, str(person.id), "", "create_company", {"name": str(person.name) + " 기업 " + str(s.planning_month), "city_id": person.city_id, "capital": int(p.ai_startup_capital), "kind": "bakery"})
			if validate_command(s, command).is_empty():
				commands.append(command)
				continue
		if own_cash >= int(p.ai_training_cash) and not _active_training(s, str(person.id)):
			var skill: String = "management" if own_cash >= int(p.ai_startup_cash) and int(person.skills.get("management", 0)) < int(p.minimum_executive_skill) else _role_skill(str(person.role))
			var command: Dictionary = _command(s, str(person.id), "", "start_training", {"skill": skill, "months": int(p.ai_training_months), "budget": int(p.training_monthly_fee) * int(p.ai_training_months)})
			if validate_command(s, command).is_empty(): commands.append(command)
	return commands

static func validate(s: Dictionary) -> Array:
	var errors: Array = []
	if not s.get("lifecycle") is Dictionary: return ["LIFECYCLE_STATE_MISSING"]
	for key in ["employment", "job_offers", "director_applications", "training", "delegations", "acquisitions", "liquidations", "liquidation_assets", "successions", "wage_claims"]:
		if not s.lifecycle.get(key) is Array: errors.append("LIFECYCLE_STATE_ARRAY:" + key)
	if not s.lifecycle.get("payroll_accruals") is Dictionary or not s.lifecycle.get("payroll_months") is Dictionary: errors.append("PAYROLL_STATE_SCHEMA")
	if not s.lifecycle.get("applied_commands") is Dictionary: errors.append("LIFECYCLE_COMMAND_INDEX")
	if not errors.is_empty(): return errors
	var employed: Dictionary = {}
	for contract in s.lifecycle.employment:
		if not contract is Dictionary or not _integer(contract.get("hours")) or not _integer(contract.get("wage")) or not _integer(contract.get("guaranteed_hours")) or int(contract.get("guaranteed_hours",-1))<0 or int(contract.get("guaranteed_hours",0))>int(contract.get("hours",0)): errors.append("EMPLOYMENT_SCHEMA")
		elif contract.status == "active":
			if employed.has(contract.person_id): errors.append("DUPLICATE_ACTIVE_EMPLOYMENT:" + str(contract.person_id))
			employed[contract.person_id] = contract
	for person in s.people:
		if not person.get("skills") is Dictionary or not _integer(person.get("capacity_hours")) or int(person.capacity_hours) <= 0: errors.append("PERSON_CAPACITY_SCHEMA")
		if person.get("employment_status") == "employed":
			if not employed.has(person.id): errors.append("EMPLOYMENT_CONTRACT_MISSING:" + str(person.id))
			else:
				var contract: Dictionary = employed[person.id]
				if contract.company_id != person.company_id or contract.role != person.role or int(contract.hours) != int(person.hours) or int(contract.wage) != int(person.wage): errors.append("EMPLOYMENT_CONTRACT_MISMATCH:" + str(person.id))
		if int(person.hours) > int(person.get("capacity_hours", 0)): errors.append("PERSON_OVERBOOKED:" + str(person.id))
		for value in person.get("skills", {}).values():
			if not _integer(value) or int(value) < 0 or int(value) > int(_p().maximum_skill): errors.append("PERSON_SKILL_INVALID")
	var ownership_keys: Dictionary = {}
	for holding in s.get("ownership", []):
		if not holding is Dictionary or not _integer(holding.get("shares")) or not _integer(holding.get("voting_shares")) or int(holding.shares) < 0 or int(holding.voting_shares) < 0 or int(holding.voting_shares) > int(holding.shares): errors.append("OWNERSHIP_QUANTITY_INVALID")
		elif _find(s.companies, str(holding.company_id)).is_empty(): errors.append("OWNERSHIP_COMPANY_MISSING")
	for company in s.companies:
		if not str(company.get("director_id", "")).is_empty():
			var director: Dictionary = _find(s.people, str(company.director_id))
			if director.is_empty() or director.company_id != company.id or director.role != "director": errors.append("DIRECTOR_OFFICE_MISMATCH:" + str(company.id))
	for delegation in s.lifecycle.delegations:
		if not _integer(delegation.get("budget")) or not _integer(delegation.get("spent")) or int(delegation.spent) < 0 or int(delegation.spent) > int(delegation.budget): errors.append("DELEGATION_BUDGET_INVALID")
	var wage_pairs: Dictionary = {}
	var wage_ids: Dictionary = {}
	for claim in s.lifecycle.wage_claims:
		if not claim is Dictionary:
			errors.append("WAGE_CLAIM_SCHEMA")
			continue
		var schema_ok: bool = true
		for key in ["id","employment_id","employer_id","person_id","city_id","status"]:
			if not claim.get(key) is String: schema_ok = false
		for key in ["month","amount","remaining","paid"]:
			if not _integer(claim.get(key)) or int(claim.get(key,-1))<0: schema_ok = false
		if not schema_ok:
			errors.append("WAGE_CLAIM_SCHEMA")
			continue
		if wage_ids.has(claim.id): errors.append("DUPLICATE_WAGE_CLAIM")
		wage_ids[claim.id] = true
		if int(claim.amount)!=int(claim.remaining)+int(claim.paid) or claim.status not in ["unpaid","partial","paid"]: errors.append("WAGE_CLAIM_BALANCE")
		if (claim.status=="paid") != (int(claim.remaining)==0): errors.append("WAGE_CLAIM_STATUS")
		if _find(s.companies,str(claim.employer_id)).is_empty() or _find(s.people,str(claim.person_id)).is_empty(): errors.append("WAGE_CLAIM_PARTY")
		var pair: String = str(claim.employer_id)+"|"+str(claim.person_id)
		if not wage_pairs.has(pair): wage_pairs[pair] = {"owner":claim.employer_id,"person":claim.person_id,"remaining":0}
		wage_pairs[pair].remaining = int(wage_pairs[pair].remaining)+int(claim.remaining)
	for pair in wage_pairs.values():
		if Ledger.balance(s,str(pair.owner)+":wage_payable:"+str(pair.person)) != -int(pair.remaining) or Ledger.balance(s,str(pair.person)+":wage_receivable:"+str(pair.owner)) != int(pair.remaining): errors.append("WAGE_CLAIM_LEDGER_MISMATCH")

	return errors

static func summary(s: Dictionary, owner: String) -> Dictionary:
	if not s.has("lifecycle"): return {}
	var person: Dictionary = _find(s.people, owner)
	var company: Dictionary = _find(s.companies, owner)
	var result: Dictionary = {"person": person.duplicate(true), "company": company.duplicate(true), "employment": s.lifecycle.employment.filter(func(e): return e.person_id == owner or e.company_id == owner), "director_applications": s.lifecycle.director_applications.filter(func(a): return a.person_id == owner or a.company_id == owner), "jobs": s.lifecycle.job_offers.filter(func(e): return e.status == "open" and (str(e.get("candidate_id", "")) in ["", owner] or e.company_id == owner)), "training": s.lifecycle.training.filter(func(t): return t.person_id == owner), "delegations": s.lifecycle.delegations.filter(func(d): return d.person_id == owner or d.company_id == owner), "holdings": s.get("ownership", []).filter(func(h): return h.owner_id == owner), "acquisitions": s.lifecycle.acquisitions.filter(func(a): return a.buyer_id == owner or a.seller_id == owner), "wage_claims":s.lifecycle.wage_claims.filter(func(w):return w.person_id==owner or w.employer_id==owner), "payroll":s.lifecycle.payroll_months.duplicate(true), "liquidation_assets": s.lifecycle.liquidation_assets.filter(func(a): return a.status == "listed")}
	return result.duplicate(true)
