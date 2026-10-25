extends RefCounted
## Public authority is a role held by a person, never ownership of treasury money.
## Every obligation, inventory delivery and payment is finite and city-attributed.
const Ledger = preload("res://core/ledger.gd")
const Domain = preload("res://core/domain.gd")
const DATA_PATH = "res://data/public_sector.json"
static var _data: Dictionary = {}
static var _data_error: String = ""

static func _normalize(value: Variant) -> Variant:
	if value is Dictionary:
		var out: Dictionary = {}
		for key in value: out[str(key)] = _normalize(value[key])
		return out
	if value is Array:
		var out: Array = []
		for item in value: out.append(_normalize(item))
		return out
	if typeof(value) == TYPE_FLOAT and is_finite(value) and value == floor(value): return int(value)
	return value

static func _config() -> Dictionary:
	if not _data.is_empty(): return _data
	if not _data_error.is_empty(): return {}
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
	if not parsed is Dictionary:
		_data_error = "PUBLIC_DATA_MISSING_OR_INVALID"
		return {}
	var d: Dictionary = _normalize(parsed)
	var errors: Array = validate_config(d)
	if not errors.is_empty():
		_data_error = ";".join(errors)
		return {}
	_data = d
	return _data

static func validate_config(d: Dictionary) -> Array:
	var errors: Array = []
	if d.get("status") != "experimental" or d.get("version") != 1: errors.append("PUBLIC_DATA_VERSION")
	if not d.get("parameters") is Dictionary: return ["PUBLIC_PARAMETERS_MISSING"]
	var required: Array = ["national_opening_cash","city_opening_cash","national_tax_bps","city_tax_bps","base_tariff_bps","office_term_months","campaign_months","campaign_fee","party_join_fee","office_hours","hourly_wage","public_worker_hours","public_workers_per_city","policy_delay_months","policy_fee","maximum_tax_bps","maximum_tariff_bps","service_food_per_worker","service_water_per_worker","public_service_target","army_members","army_hours","army_food_per_member","army_food_stock_months","army_tools_target","army_tools_period_months","army_tools_use","procurement_lead_months","procurement_min_quality","base_quality","maximum_advance_bps","bid_fee","permit_fee","permit_delay_months","price_weight","quality_weight","delivery_weight","cross_city_lead_months","customs_delay_months","tension_extra_months","late_penalty_bps","conflict_damage_period_months","conflict_damage_percent","mobilization_hours","minimum_welfare_cash","welfare_transfer","election_preference_bonus","minimum_office_support","political_action_hours","service_notice_months","procurement_price_bps","maximum_interest_bps","late_penalty_cap_bps","party_found_fee","campaign_awareness_cap","initial_office_term_months"]
	for key in required:
		if not _integer(d.parameters.get(key)) or int(d.parameters.get(key, -1)) < 0: errors.append("PUBLIC_PARAMETER:" + key)
	for key in ["national_budget","city_budget"]:
		if not d.parameters.get(key) is Dictionary:
			errors.append("PUBLIC_BUDGET:" + key)
			continue
		for category in ["services","procurement","military","transfers"]:
			if not _integer(d.parameters[key].get(category)) or int(d.parameters[key].get(category, -1)) < 0: errors.append("PUBLIC_BUDGET_CATEGORY:" + category)
	for key in ["platforms","policy_keys","party_names"]:
		if not d.get(key) is Array or d.get(key, []).size() < 3: errors.append("PUBLIC_CHOICES:" + key)
	if int(d.parameters.get("office_term_months",0)) <= 0 or int(d.parameters.get("army_tools_period_months",0)) <= 0 or int(d.parameters.get("conflict_damage_period_months",0)) <= 0: errors.append("PUBLIC_PERIOD_MUST_BE_POSITIVE")
	return errors

static func content_hash() -> String:
	return JSON.stringify(_config(), "", true).sha256_text()

static func _p() -> Dictionary:
	return _config().get("parameters", {})

static func _integer(value: Variant) -> bool:
	return (typeof(value) == TYPE_INT or typeof(value) == TYPE_FLOAT) and is_finite(float(value)) and float(value) == floor(float(value)) and abs(float(value)) <= 9007199254740991.0

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if item is Dictionary and str(item.get("id", "")) == id: return item
	return {}

static func _keys(d: Dictionary) -> Array:
	var keys: Array = d.keys()
	keys.sort()
	return keys

static func _id(s: Dictionary, prefix: String) -> String:
	s.public.sequence = int(s.public.sequence) + 1
	return prefix + "-" + str(s.public.sequence)

static func _event(s: Dictionary, type: String, reason: String, month: int, extra: Dictionary = {}) -> void:
	var e: Dictionary = {"id":"ev-" + str(s.events.size()),"month":month,"type":type,"reason":reason,"actor_id":"","city_id":"","product_id":"","quantity":0,"amount":0}
	e.merge(extra, true)
	s.events.append(e)

static func _accounts(s: Dictionary, owner: String) -> void:
	for kind in ["cash","equity","expense","revenue"]: Ledger.open_account(s, owner + ":" + kind, owner, kind, "USD")

static func _account(s: Dictionary, owner: String, suffix: String, kind: String) -> String:
	var id: String = owner + ":" + suffix
	Ledger.open_account(s, id, owner, kind, "USD")
	return id

static func _line(account: String, amount: int) -> Dictionary:
	return {"account":account,"debit":maxi(amount,0),"credit":maxi(-amount,0)}

static func _cash(s: Dictionary, owner: String) -> int:
	return Ledger.balance(s, owner + ":cash")

static func _transfer(s: Dictionary, from: String, to: String, amount: int, reason: String, month: int, extra: Dictionary = {}) -> bool:
	if amount <= 0: return true
	_accounts(s, from)
	_accounts(s, to)
	var result: Dictionary = Ledger.transfer(s, _id(s,"public-pay"), from, to, amount, reason, month)
	if not result.get("ok", false): return false
	var payer: Dictionary = _find(s.get("companies", []), from)
	var receiver: Dictionary = _find(s.get("companies", []), to)
	if not payer.is_empty(): payer.expenses = int(payer.get("expenses",0)) + amount
	if not receiver.is_empty(): receiver.revenue = int(receiver.get("revenue",0)) + amount
	var person: Dictionary = _find(s.get("people", []), to)
	if not person.is_empty() and reason in ["공직 근무 보수","공공 서비스 근로","군 근무 보수"]: person.last_income = int(person.get("last_income",0)) + amount
	var e: Dictionary = {"actor_id":from,"counterparty_id":to,"amount":amount,"city_id":owner_city(s, from)}
	e.merge(extra,true)
	_event(s,"public_payment",reason,month,e)
	return true

static func owner_city(s: Dictionary, owner: String) -> String:
	var g: Dictionary = _find(s.get("public", {}).get("governments", []), owner)
	if not g.is_empty(): return str(g.city_id)
	var party: Dictionary = _find(s.get("public", {}).get("parties", []), owner)
	if not party.is_empty(): return str(party.city_id)
	var co: Dictionary = _find(s.get("companies", []), owner)
	if not co.is_empty(): return str(co.city_id)
	return str(_find(s.get("people", []), owner).get("city_id", ""))

static func _city_people(s: Dictionary, city: String) -> Array:
	var people: Array = s.get("people", []).filter(func(x): return str(x.get("city_id", "")) == city)
	people.sort_custom(func(a,b): return str(a.id) < str(b.id))
	return people

static func _nation_for_city(s: Dictionary, city: String) -> String:
	return str(_find(s.get("cities", []), city).get("nation_id", ""))

static func _government_for_nation(s: Dictionary, nation: String) -> Dictionary:
	for g in s.get("public", {}).get("governments", []):
		if g.kind == "nation" and g.nation_id == nation: return g
	return {}

static func initialize(s: Dictionary) -> void:
	if s.has("public"): return
	if _config().is_empty(): return
	var p: Dictionary = _p()
	s.public = {"version":1,"sequence":0,"governments":[],"offices":[],"parties":[],"candidates":[],"ballots":[],"policy_proposals":[],"policies":[],"treaties":[],"relations":[],"tax_assessments":[],"procurements":[],"bids":[],"deliveries":[],"military_lots":[],"armies":[],"conflicts":[],"license_applications":[],"mobilized":[],"provided_needs":{},"last_prepared_month":0,"applied_commands":[],"service_changes":[],"default_claims":[]}
	for city in s.cities:
		_make_government(s, "city", city.id, city.nation_id, city.id, str(city.name) + " 시정부", int(p.city_opening_cash), int(p.city_tax_bps), p.city_budget)
	for nation in s.nations:
		var cities: Array = s.cities.filter(func(x): return x.nation_id == nation.id)
		if cities.is_empty(): continue
		var city: Dictionary = cities[0]
		_make_government(s,"nation",nation.id,nation.id,city.id,str(nation.name) + " 정부",int(p.national_opening_cash),int(p.national_tax_bps),p.national_budget)
		var members: Array = []
		var residents: Array = _city_people(s,city.id)
		for person in residents:
			if members.size() >= int(p.army_members): break
			if person.get("role","")=="director" or s.public.offices.any(func(o): return o.person_id == person.id): continue
			members.append(person.id)
		s.public.armies.append({"id":"army-" + str(nation.id),"nation_id":nation.id,"government_id":"gov-nation-" + str(nation.id),"city_id":city.id,"members":members,"readiness":1,"effective_readiness":0,"paid_hours":0,"last_supply_ratio":0,"mobilization":false})
	for nation in s.nations:
		var city: String = str(_government_for_nation(s,nation.id).get("city_id",""))
		for i in range(_config().party_names.size()):
			var id: String = "party-" + str(nation.id) + "-" + str(i)
			_accounts(s,id)
			s.public.parties.append({"id":id,"name":_config().party_names[i],"nation_id":nation.id,"city_id":city,"platform":_config().platforms[i],"members":[],"donations":0})
	for i in range(s.nations.size()):
		for j in range(i+1,s.nations.size()):
			s.public.relations.append({"id":_pair(s.nations[i].id,s.nations[j].id),"nation_a":s.nations[i].id,"nation_b":s.nations[j].id,"state":"neutral","since_month":0,"reason":"개시 외교 관계"})

static func _make_government(s: Dictionary, kind: String, jurisdiction: String, nation: String, city: String, name: String, opening: int, tax_bps: int, budget: Dictionary) -> void:
	var id: String = "gov-" + kind + "-" + jurisdiction
	var residents: Array = _city_people(s,city)
	var holder: String = ""
	if not residents.is_empty(): holder = str(residents[-1 if kind == "city" else maxi(0,residents.size()-2)].id)
	var council: Array = []
	for person in residents:
		if council.size() >= 3: break
		council.append(person.id)
	var g: Dictionary = {"id":id,"kind":kind,"jurisdiction_id":jurisdiction,"nation_id":nation,"city_id":city,"name":name,"currency":"USD","office_id":"office-"+id,"tax_bps":tax_bps,"tariff_bps":int(_p().base_tariff_bps) if kind=="nation" else 0,"budget":budget.duplicate(true),"budget_used":{"services":0,"procurement":0,"military":0,"transfers":0},"budget_month":0,"reserved":0,"services":0,"arrears":0,"council":council,"last_tax_revenue":0,"last_service_cost":0,"last_election_month":0,"mobilization":false}
	s.public.governments.append(g)
	s.public.offices.append({"id":g.office_id,"government_id":id,"person_id":holder,"term_start":0,"term_end":int(_p().initial_office_term_months),"authority":["budget","policy","procurement","services","military","diplomacy"] if kind=="nation" else ["budget","policy","procurement","services"]})
	if not s.accounts.has(id + ":cash"): Ledger.opening(s,id,opening,0)
	_event(s,"government_opening","개시 공공 재정과 직위 배치",0,{"actor_id":id,"city_id":city,"amount":opening,"person_id":holder})

static func has_authority(s: Dictionary, actor: String, government: String, capability: String = "budget") -> bool:
	for office in s.get("public", {}).get("offices", []):
		if office.get("government_id") == government and office.get("person_id") == actor and capability in office.get("authority", []): return true
	return false

static func _eligible(s: Dictionary, actor: String, g: Dictionary) -> bool:
	var person: Dictionary = _find(s.people,actor)
	if person.is_empty(): return false
	return person.city_id == g.city_id if g.kind == "city" else _nation_for_city(s,person.city_id) == g.nation_id

static func _company(s: Dictionary, c: Dictionary) -> Dictionary:
	return _find(s.get("companies", []),str(c.get("acting_company_id", c.get("payload", {}).get("company_id",""))))

static func _spendable(s: Dictionary, g: Dictionary) -> int:
	return maxi(0,_cash(s,g.id)-int(g.reserved))

static func _budget_left(g: Dictionary, category: String, month: int) -> int:
	return maxi(0,int(g.budget.get(category,0))-(int(g.budget_used.get(category,0)) if int(g.budget_month)==month else 0))

static func _budget_use(g: Dictionary, category: String, amount: int, month: int) -> void:
	if int(g.budget_month) != month:
		g.budget_month=month
		g.budget_used={"services":0,"procurement":0,"military":0,"transfers":0}
	g.budget_used[category]=int(g.budget_used.get(category,0))+amount

static func _pair(a: String,b: String) -> String:
	return a + "|" + b if a < b else b + "|" + a

static func command_types() -> Array:
	return ["public_create_party","public_campaign","public_resign_office","public_join_service","public_leave_service","public_join_party","public_donate","public_run_office","public_vote","public_propose_policy","public_vote_policy","public_set_budget","public_transfer","public_announce_procurement","public_bid","public_award","public_deliver","public_inspect","public_pay","public_cancel_contract","public_apply_license","public_propose_treaty","public_accept_treaty","public_set_relation","public_set_readiness"]

static func _require_int(payload: Dictionary, key: String, low: int, high: int = 9007199254740991) -> bool:
	return _integer(payload.get(key)) and int(payload[key]) >= low and int(payload[key]) <= high

static func validate_command(s: Dictionary, c: Dictionary) -> String:
	if not s.has("public") or _config().is_empty(): return "PUBLIC_NOT_INITIALIZED"
	if not c.get("payload") is Dictionary or str(c.get("actor_id","")).is_empty() or str(c.get("id","")).is_empty(): return "PUBLIC_COMMAND_SCHEMA"
	if c.id in s.public.applied_commands: return "PUBLIC_COMMAND_ALREADY_APPLIED"
	if _find(s.get("people",[]),str(c.actor_id)).is_empty(): return "PUBLIC_UNKNOWN_ACTOR"
	if not _integer(c.get("expected_version")) or int(c.expected_version) != int(s.version): return "STALE_STATE_VERSION"
	var p: Dictionary = c.payload
	var type: String = str(c.get("type",""))
	if type not in command_types(): return "PUBLIC_UNKNOWN_COMMAND"
	var actor: String = str(c.actor_id)
	var g: Dictionary = _find(s.public.governments,str(p.get("government_id",c.get("acting_company_id",""))))
	var co: Dictionary = _company(s,c)
	var month: int = int(s.planning_month)
	var person: Dictionary=_find(s.people,actor)
	var used: int=int(s.monthly_labor.get(actor,0)) if int(s.public.last_prepared_month)==month else 0
	var planned: int=0
	if s.get("phase","")=="Planning":
		for queued in s.get("commands",[]):
			if queued.get("actor_id")==actor and str(queued.get("type","")) in command_types() and queued.get("id")!=c.id: planned+=int(_p().political_action_hours)
	if used+planned+int(_p().political_action_hours)>int(person.hours): return "PUBLIC_PERSON_TIME_EXHAUSTED"
	match type:
		"public_create_party":
			if not p.get("name") is String or p.name.strip_edges().length()<2 or p.name.length()>40: return "PUBLIC_PARTY_NAME"
			if p.get("platform","") not in _config().platforms: return "PUBLIC_UNKNOWN_PLATFORM"
			if _cash(s,actor)<int(_p().party_found_fee): return "INSUFFICIENT_CASH"
			if s.public.parties.any(func(x): return x.name==p.name): return "PUBLIC_PARTY_NAME_TAKEN"
		"public_campaign":
			var party: Dictionary=_find(s.public.parties,str(p.get("party_id","")))
			var candidate: Dictionary=_find(s.public.candidates,str(p.get("candidate_id","")))
			if party.is_empty() or party.get("leader_id","")!=actor: return "PUBLIC_PARTY_LEADER_REQUIRED"
			if candidate.is_empty() or candidate.status!="campaigning" or candidate.person_id not in party.members: return "PUBLIC_CANDIDATE_NOT_AVAILABLE"
			if not _require_int(p,"quantity",1): return "PUBLIC_CAMPAIGN_VALUE"
			if _find(s.products,"paper").is_empty(): return "PUBLIC_CAMPAIGN_MATERIAL_NOT_AVAILABLE"
			var suppliers: Array=s.companies.filter(func(x): return x.city_id==party.city_id and _can_produce(s,x.id,"paper") and _available(s,x.id,x.city_id,"paper")>=int(p.quantity))
			if suppliers.is_empty(): return "PUBLIC_CAMPAIGN_MATERIAL_NOT_AVAILABLE"
			suppliers.sort_custom(func(a,b): return _offer_price(s,a,"paper")<_offer_price(s,b,"paper"))
			if _cash(s,party.id)<int(p.quantity)*_offer_price(s,suppliers[0],"paper"): return "INSUFFICIENT_CASH"
		"public_resign_office":
			if g.is_empty() or not has_authority(s,actor,g.id,"policy"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
		"public_join_service","public_leave_service":
			var army: Dictionary=_find(s.public.armies,str(p.get("army_id","")))
			if army.is_empty() or _nation_for_city(s,person.city_id)!=army.nation_id: return "PUBLIC_POLITICAL_JURISDICTION"
			if (type=="public_join_service" and actor in army.members) or (type=="public_leave_service" and actor not in army.members): return "PUBLIC_SERVICE_MEMBERSHIP"
			if s.public.service_changes.any(func(x): return x.person_id==actor and x.status=="pending"): return "PUBLIC_SERVICE_CHANGE_PENDING"
		"public_join_party","public_donate":
			var party: Dictionary = _find(s.public.parties,str(p.get("party_id","")))
			if party.is_empty(): return "PUBLIC_PARTY_NOT_FOUND"
			if _nation_for_city(s,_find(s.people,actor).city_id) != party.nation_id: return "PUBLIC_POLITICAL_JURISDICTION"
			if type=="public_join_party" and actor in party.members: return "PUBLIC_ALREADY_PARTY_MEMBER"
			var amount: int = int(_p().party_join_fee) if type=="public_join_party" else int(p.get("amount",0))
			if type=="public_donate" and not co.is_empty() and co.director_id!=actor: return "AUTHORITY_REQUIRED"
			if type=="public_donate" and not _require_int(p,"amount",1): return "PUBLIC_INVALID_AMOUNT"
			if _cash(s,co.id if type=="public_donate" and not co.is_empty() else actor)<amount: return "INSUFFICIENT_CASH"
		"public_run_office":
			if g.is_empty() or not _eligible(s,actor,g): return "PUBLIC_POLITICAL_JURISDICTION"
			var election_office: Dictionary=_find(s.public.offices,g.office_id)
			if month<int(election_office.term_end)-int(_p().campaign_months): return "PUBLIC_ELECTION_REGISTRATION_NOT_OPEN"
			if s.get("phase","")=="Planning" and s.get("commands",[]).any(func(x): return x.get("type","")=="public_run_office" and x.get("actor_id","")==actor and x.get("payload",{}).get("government_id","")==g.id and x.get("id","")!=c.id): return "PUBLIC_ALREADY_CANDIDATE"
			if str(p.get("platform","services")) not in _config().platforms: return "PUBLIC_UNKNOWN_PLATFORM"
			if s.public.candidates.any(func(x): return x.government_id==g.id and x.person_id==actor and x.status=="campaigning"): return "PUBLIC_ALREADY_CANDIDATE"
			if _cash(s,actor)<int(_p().campaign_fee): return "INSUFFICIENT_CASH"
		"public_vote":
			var candidate: Dictionary = _find(s.public.candidates,str(p.get("candidate_id","")))
			if candidate.is_empty() or candidate.status!="campaigning": return "PUBLIC_CANDIDATE_NOT_AVAILABLE"
			g=_find(s.public.governments,candidate.government_id)
			if not _eligible(s,actor,g): return "PUBLIC_POLITICAL_JURISDICTION"
			if int(candidate.election_month)<month: return "PUBLIC_ELECTION_CLOSED"
			if s.public.ballots.any(func(x): return x.government_id==g.id and x.person_id==actor and int(x.election_month)==int(candidate.election_month)): return "PUBLIC_ALREADY_VOTED"
			if s.get("phase","")=="Planning":
				for queued in s.get("commands",[]):
					if queued.get("type","")!="public_vote" or queued.get("actor_id","")!=actor or queued.get("id","")==c.id: continue
					var choice: Dictionary=_find(s.public.candidates,str(queued.get("payload",{}).get("candidate_id","")))
					if choice.get("government_id","")==g.id and int(choice.get("election_month",0))==int(candidate.election_month): return "PUBLIC_ALREADY_VOTED"
		"public_propose_policy":
			if g.is_empty() or not _eligible(s,actor,g): return "PUBLIC_POLITICAL_JURISDICTION"
			var key: String = str(p.get("key",""))
			if key not in _config().policy_keys: return "PUBLIC_UNKNOWN_POLICY"
			if key in ["tariff_bps","import_ban","export_ban","mobilization","interest_bps"] and g.kind!="nation": return "PUBLIC_NATIONAL_AUTHORITY_REQUIRED"
			if not _require_int(p,"value",0): return "PUBLIC_POLICY_VALUE"
			if key=="tax_bps" and int(p.value)>int(_p().maximum_tax_bps): return "PUBLIC_POLICY_VALUE"
			if key=="tariff_bps" and int(p.value)>int(_p().maximum_tariff_bps): return "PUBLIC_POLICY_VALUE"
			if key=="interest_bps" and int(p.value)>int(_p().maximum_interest_bps): return "PUBLIC_POLICY_VALUE"
			if key in ["import_ban","export_ban","mobilization"] and int(p.value)>1: return "PUBLIC_POLICY_VALUE"
			if key in ["import_ban","export_ban"] and _find(s.nations,str(p.get("other_nation_id",""))).is_empty(): return "PUBLIC_UNKNOWN_NATION"
			if _cash(s,actor)<int(_p().policy_fee): return "INSUFFICIENT_CASH"
		"public_vote_policy":
			var proposal: Dictionary = _find(s.public.policy_proposals,str(p.get("proposal_id","")))
			if proposal.is_empty() or proposal.status!="voting": return "PUBLIC_POLICY_NOT_VOTING"
			g=_find(s.public.governments,proposal.government_id)
			if actor not in g.council and not has_authority(s,actor,g.id,"policy"): return "PUBLIC_COUNCIL_AUTHORITY_REQUIRED"
			if proposal.votes.has(actor): return "PUBLIC_ALREADY_VOTED"
			if typeof(p.get("support"))!=TYPE_BOOL: return "PUBLIC_VOTE_VALUE"
		"public_set_budget":
			if g.is_empty() or not has_authority(s,actor,g.id,"budget"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
			if str(p.get("category","")) not in g.budget or not _require_int(p,"amount",0): return "PUBLIC_BUDGET_VALUE"
			if int(p.amount)>_cash(s,g.id): return "PUBLIC_UNFUNDED_BUDGET"
		"public_transfer":
			if g.is_empty() or not has_authority(s,actor,g.id,"budget"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
			var to: String = str(p.get("recipient_id",""))
			if _find(s.people,to).is_empty() and _find(s.public.governments,to).is_empty(): return "PUBLIC_TRANSFER_RECIPIENT"
			if not _require_int(p,"amount",1) or int(p.amount)>_spendable(s,g) or int(p.amount)>_budget_left(g,"transfers",month): return "PUBLIC_TRANSFER_BUDGET"
			if to==actor: return "PUBLIC_SELF_DEALING"
		"public_announce_procurement":
			if g.is_empty() or not has_authority(s,actor,g.id,"procurement"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
			if _find(s.products,str(p.get("product_id",""))).is_empty(): return "PUBLIC_UNKNOWN_PRODUCT"
			if not _require_int(p,"quantity",1) or not _require_int(p,"max_unit_price",1): return "PUBLIC_PROCUREMENT_VALUE"
			if not _require_int(p,"deadline_month",month+1): return "PUBLIC_PROCUREMENT_DEADLINE"
			if int(p.quantity)>9007199254740991/int(p.max_unit_price): return "PUBLIC_PROCUREMENT_OVERFLOW"
			var cost: int=int(p.quantity)*int(p.max_unit_price)
			if cost>_spendable(s,g) or cost>_budget_left(g,"procurement",month): return "PUBLIC_PROCUREMENT_BUDGET"
			if p.get("military",false) and g.kind!="nation": return "PUBLIC_NATIONAL_AUTHORITY_REQUIRED"
		"public_bid":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			if contract.is_empty() or contract.status!="open" or int(contract.deadline_month)<=month: return "PUBLIC_PROCUREMENT_NOT_OPEN"
			if co.is_empty() or co.director_id!=actor or co.get("status","active")!="active": return "AUTHORITY_REQUIRED"
			g=_find(s.public.governments,contract.government_id)
			if _interest(s,actor,co.id,g.id): return "PUBLIC_SELF_DEALING"
			if not _require_int(p,"unit_price",1,int(contract.max_unit_price)) or not _require_int(p,"delivery_month",month+1,int(contract.deadline_month)): return "PUBLIC_BID_TERMS"
			if not _can_produce(s,co.id,contract.product_id): return "PUBLIC_SUPPLIER_CAPABILITY_REQUIRED"
			if _quality(s,co.id,contract.product_id)<int(contract.min_quality): return "PUBLIC_QUALITY_CAPABILITY_REQUIRED"
			if contract.get("required_license","")!="" and contract.required_license not in co.get("licenses",[]): return "PUBLIC_LICENSE_REQUIRED"
			if not _capacity_available(s,co.id,contract.product_id,int(contract.quantity),int(p.delivery_month),month): return "PUBLIC_CAPACITY_ALREADY_COMMITTED"
			if s.public.bids.any(func(x): return x.contract_id==contract.id and x.company_id==co.id and x.status=="submitted"): return "PUBLIC_DUPLICATE_BID"
			if s.get("phase","")=="Planning" and s.get("commands",[]).any(func(x): return x.get("type","")=="public_bid" and x.get("acting_company_id","")==co.id and x.get("payload",{}).get("contract_id","")==contract.id and x.get("id","")!=c.id): return "PUBLIC_DUPLICATE_BID"
			if _cash(s,co.id)<int(_p().bid_fee): return "INSUFFICIENT_CASH"
		"public_award":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			if contract.is_empty() or contract.status!="open": return "PUBLIC_PROCUREMENT_NOT_OPEN"
			g=_find(s.public.governments,contract.government_id)
			if not has_authority(s,actor,g.id,"procurement"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
			var bid: Dictionary=_find(s.public.bids,str(p.get("bid_id","")))
			if bid.is_empty() or bid.contract_id!=contract.id or bid.status!="submitted": return "PUBLIC_BID_NOT_FOUND"
			var best: Dictionary=_best_bid(s,contract)
			if best.is_empty() or best.id!=bid.id: return "PUBLIC_PUBLISHED_EVALUATION_REQUIRED"
			if _interest(s,_find(s.companies,bid.company_id).director_id,bid.company_id,g.id): return "PUBLIC_SELF_DEALING"
			if not _require_int(p,"advance_bps",0,int(_p().maximum_advance_bps)): return "PUBLIC_ADVANCE_LIMIT"
		"public_deliver":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			if contract.is_empty() or contract.status not in ["awarded","partially_delivered","late"]: return "PUBLIC_CONTRACT_NOT_DELIVERABLE"
			if co.is_empty() or co.id!=contract.company_id or co.director_id!=actor: return "AUTHORITY_REQUIRED"
			if not _require_int(p,"quantity",1,int(contract.quantity)-int(contract.delivered_quantity)): return "PUBLIC_DELIVERY_QUANTITY"
			if _available(s,co.id,co.city_id,contract.product_id)<int(p.quantity): return "PUBLIC_DELIVERY_STOCK_MISSING"
			g=_find(s.public.governments,contract.government_id)
			var rule: Dictionary=_contract_trade_rule(s,contract,co.city_id,g.city_id,month)
			if not rule.allowed: return "PUBLIC_DELIVERY_TRADE_RESTRICTED"
		"public_inspect":
			var delivery: Dictionary=_find(s.public.deliveries,str(p.get("delivery_id","")))
			if delivery.is_empty() or delivery.status!="awaiting_inspection": return "PUBLIC_DELIVERY_NOT_ARRIVED"
			g=_find(s.public.governments,_find(s.public.procurements,delivery.contract_id).government_id)
			if not has_authority(s,actor,g.id,"procurement"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
		"public_pay":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			if contract.is_empty(): return "PUBLIC_CONTRACT_NOT_FOUND"
			g=_find(s.public.governments,contract.government_id)
			if not has_authority(s,actor,g.id,"procurement"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
			if int(contract.payable)<=0: return "PUBLIC_NOTHING_INSPECTED_PAYABLE"
			if int(contract.payable)>_cash(s,g.id)-maxi(0,int(g.reserved)-int(contract.reserved_value)): return "PUBLIC_PAYMENT_BUDGET"
		"public_cancel_contract":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			if contract.is_empty() or contract.status in ["fulfilled","cancelled"]: return "PUBLIC_CONTRACT_NOT_CANCELLABLE"
			if not has_authority(s,actor,contract.government_id,"procurement"): return "PUBLIC_OFFICE_AUTHORITY_REQUIRED"
		"public_apply_license":
			if co.is_empty() or co.director_id!=actor: return "AUTHORITY_REQUIRED"
			if "defense" in co.get("licenses",[]): return "PUBLIC_LICENSE_ALREADY_HELD"
			if _cash(s,co.id)<int(_p().permit_fee): return "INSUFFICIENT_CASH"
			if s.public.license_applications.any(func(x): return x.company_id==co.id and x.status=="pending"): return "PUBLIC_LICENSE_ALREADY_PENDING"
		"public_propose_treaty":
			if g.is_empty() or g.kind!="nation" or not has_authority(s,actor,g.id,"diplomacy"): return "PUBLIC_FOREIGN_AUTHORITY_REQUIRED"
			var other: Dictionary=_find(s.nations,str(p.get("other_nation_id","")))
			if other.is_empty() or other.id==g.nation_id: return "PUBLIC_UNKNOWN_NATION"
			if str(p.get("kind","")) not in ["free_trade","transit","ceasefire","embargo"]: return "PUBLIC_TREATY_KIND"
		"public_accept_treaty":
			var treaty: Dictionary=_find(s.public.treaties,str(p.get("treaty_id","")))
			if treaty.is_empty() or treaty.status!="proposed": return "PUBLIC_TREATY_NOT_PROPOSED"
			g=_government_for_nation(s,treaty.nation_b)
			if not has_authority(s,actor,g.id,"diplomacy"): return "PUBLIC_FOREIGN_AUTHORITY_REQUIRED"
		"public_set_relation":
			if g.is_empty() or g.kind!="nation" or not has_authority(s,actor,g.id,"diplomacy"): return "PUBLIC_FOREIGN_AUTHORITY_REQUIRED"
			if _find(s.nations,str(p.get("other_nation_id",""))).is_empty() or p.get("other_nation_id")==g.nation_id: return "PUBLIC_UNKNOWN_NATION"
			if str(p.get("state","")) not in ["neutral","cooperate","tension","conflict"]: return "PUBLIC_RELATION_STATE"
		"public_set_readiness":
			var army: Dictionary=_find(s.public.armies,str(p.get("army_id","")))
			if army.is_empty() or not has_authority(s,actor,army.government_id,"military"): return "PUBLIC_MILITARY_AUTHORITY_REQUIRED"
			if not _require_int(p,"readiness",0,3): return "PUBLIC_READINESS_VALUE"
	return ""

static func _interest(s: Dictionary, person: String, company: String, gov: String) -> bool:
	if has_authority(s,person,gov,"procurement"): return true
	var office: Dictionary={}
	for item in s.public.offices:
		if item.government_id==gov: office=item
	if office.is_empty(): return false
	var co: Dictionary=_find(s.companies,company)
	return co.get("director_id")==office.person_id or co.get("shareholder_id")==office.person_id

static func _can_produce(s: Dictionary, company: String, product: String) -> bool:
	for facility in s.facilities:
		if facility.company_id!=company or facility.get("status","") not in ["operational","ready"]: continue
		var recipe: Dictionary=_find(s.recipes,str(facility.get("recipe_id","")))
		if recipe.get("outputs",{}).has(product): return true
	return false

static func _quality(s: Dictionary, company: String, product: String) -> int:
	var value: int=0
	for facility in s.facilities:
		if facility.company_id!=company: continue
		var recipe: Dictionary=_find(s.recipes,str(facility.get("recipe_id","")))
		if recipe.get("outputs",{}).has(product): value=maxi(value,int(facility.get("quality",_p().base_quality)))
	return value

static func _capacity_available(s: Dictionary, company: String, product: String, quantity: int, due: int, month: int) -> bool:
	var capacity: int=0
	for facility in s.facilities:
		if facility.company_id!=company or facility.get("status","") not in ["operational","ready"]: continue
		var recipe: Dictionary=_find(s.recipes,str(facility.get("recipe_id","")))
		capacity += int(facility.get("capacity",0))*int(recipe.get("outputs",{}).get(product,0))
	var committed: int=0
	for bid in s.public.bids:
		if bid.company_id==company and bid.status=="submitted" and int(bid.delivery_month)<=due:
			var existing: Dictionary=_find(s.public.procurements,bid.contract_id)
			if existing.get("product_id","")==product: committed+=int(existing.quantity)
	if s.get("phase","")=="Planning":
		for command in s.get("commands",[]):
			if command.get("type","")!="public_bid" or command.get("acting_company_id","")!=company: continue
			var queued_contract: Dictionary=_find(s.public.procurements,str(command.get("payload",{}).get("contract_id","")))
			if queued_contract.get("product_id","")==product and int(command.payload.get("delivery_month",due))<=due: committed+=int(queued_contract.quantity)
	for contract in s.public.procurements:
		if contract.get("company_id","")==company and contract.product_id==product and contract.status in ["awarded","partially_delivered","late"] and int(contract.deadline_month)<=due:
			committed+=maxi(0,int(contract.quantity)-int(contract.accepted_quantity))
	return quantity+committed<=capacity*maxi(1,due-month)

static func _bid_score(bid: Dictionary, contract: Dictionary) -> int:
	var p: Dictionary=_p()
	return int(int(bid.unit_price)*int(p.price_weight)*10000/maxi(1,int(contract.max_unit_price))) + int(maxi(0,int(bid.delivery_month)-int(contract.opened_month))*int(p.delivery_weight)*10000/maxi(1,int(contract.deadline_month)-int(contract.opened_month))) - int(bid.quality)*int(p.quality_weight)*100

static func _best_bid(s: Dictionary, contract: Dictionary) -> Dictionary:
	var bids: Array=s.public.bids.filter(func(x): return x.contract_id==contract.id and x.status=="submitted")
	bids.sort_custom(func(a,b):
		var aa: int=_bid_score(a,contract)
		var bb: int=_bid_score(b,contract)
		return str(a.id).sha256_text()<str(b.id).sha256_text() if aa==bb else aa<bb
	)
	return bids[0] if not bids.is_empty() else {}

static func apply(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var error: String=validate_command(s,c)
	if not error.is_empty(): return {"ok":false,"error":error}
	var p: Dictionary=c.payload
	var actor: String=str(c.actor_id)
	var g: Dictionary=_find(s.public.governments,str(p.get("government_id",c.get("acting_company_id",""))))
	var co: Dictionary=_company(s,c)
	var result: Dictionary={"ok":true,"error":""}
	match str(c.type):
		"public_create_party":
			var person: Dictionary=_find(s.people,actor)
			g=_government_for_nation(s,_nation_for_city(s,person.city_id))
			if not _transfer(s,actor,g.id,int(_p().party_found_fee),"정당 설립 등록 비용",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			var party: Dictionary={"id":_id(s,"party"),"name":str(p.name).strip_edges(),"nation_id":g.nation_id,"city_id":person.city_id,"platform":p.platform,"members":[actor],"leader_id":actor,"donations":0}
			_accounts(s,party.id)
			s.public.parties.append(party)
			var finance: Variant=_finance(s)
			if finance!=null and finance.has_method("register_owner"): finance.register_owner(s,party.id,party.city_id)
		"public_campaign":
			var party: Dictionary=_find(s.public.parties,p.party_id)
			var candidate: Dictionary=_find(s.public.candidates,p.candidate_id)
			var suppliers: Array=s.companies.filter(func(x): return x.city_id==party.city_id and _can_produce(s,x.id,"paper") and _available(s,x.id,x.city_id,"paper")>=int(p.quantity))
			suppliers.sort_custom(func(a,b): return str(a.id)<str(b.id) if _offer_price(s,a,"paper")==_offer_price(s,b,"paper") else _offer_price(s,a,"paper")<_offer_price(s,b,"paper"))
			var seller: Dictionary=suppliers[0]
			var amount: int=int(p.quantity)*_offer_price(s,seller,"paper")
			if not _transfer(s,party.id,seller.id,amount,"정당의 실제 선거 홍보물 구매",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			var cost: int=_sale_cost(s,seller.id,"paper",int(p.quantity),month)
			seller.expenses=int(seller.get("expenses",0))+cost
			_remove(s,seller.id,seller.city_id,"paper",int(p.quantity),"선거 활동 홍보물 실제 사용",month)
			candidate.awareness=mini(int(_p().campaign_awareness_cap),int(candidate.get("awareness",0))+int(p.quantity))
			_event(s,"campaign_outreach","실제 후원 재원·홍보물·업무시간을 사용한 정치 활동",month,{"actor_id":party.id,"city_id":party.city_id,"candidate_id":candidate.id,"quantity":int(p.quantity),"product_id":"paper","amount":amount})
		"public_resign_office":
			var office: Dictionary=_find(s.public.offices,g.office_id)
			var successors: Array=g.council.filter(func(x): return x!=actor and not _find(s.people,str(x)).is_empty())
			if successors.is_empty(): return {"ok":false,"error":"PUBLIC_NO_CARETAKER"}
			office.person_id=successors[0]
			office.term_start=month
			office.term_end=month+int(_p().campaign_months)
			_event(s,"office_resignation","사임 후 지정 절차로 직무대행에게 권한만 이전",month,{"actor_id":actor,"city_id":g.city_id,"government_id":g.id,"successor_id":office.person_id})
		"public_join_service","public_leave_service":
			s.public.service_changes.append({"id":_id(s,"service-change"),"person_id":actor,"army_id":p.army_id,"action":"join" if c.type=="public_join_service" else "leave","effective_month":month+int(_p().service_notice_months),"status":"pending"})
		"public_join_party","public_donate":
			var party: Dictionary=_find(s.public.parties,p.party_id)
			var amount: int=int(_p().party_join_fee) if c.type=="public_join_party" else int(p.amount)
			var donor: String=co.id if c.type=="public_donate" and not co.is_empty() else actor
			if not _transfer(s,donor,party.id,amount,"정치 활동의 공개 자금 이전",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			party.donations=int(party.donations)+amount
			if c.type=="public_join_party":
				party.members.append(actor)
				if not party.has("leader_id"): party.leader_id=actor
		"public_run_office":
			if not _transfer(s,actor,g.id,int(_p().campaign_fee),"공직 후보 등록 비용",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			var office: Dictionary=_find(s.public.offices,g.office_id)
			var election: int=maxi(month+1,int(office.term_end))
			for candidate in s.public.candidates:
				if candidate.government_id==g.id and candidate.status=="campaigning": election=int(candidate.election_month)
			s.public.candidates.append({"id":_id(s,"candidate"),"government_id":g.id,"person_id":actor,"name":_find(s.people,actor).name,"platform":str(p.get("platform","services")),"election_month":election,"status":"campaigning","votes":0,"registered_month":month})
		"public_vote":
			var candidate: Dictionary=_find(s.public.candidates,p.candidate_id)
			s.public.ballots.append({"id":_id(s,"ballot"),"government_id":candidate.government_id,"person_id":actor,"candidate_id":candidate.id,"election_month":candidate.election_month})
		"public_propose_policy":
			if not _transfer(s,actor,g.id,int(_p().policy_fee),"정책 제안 접수 비용",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			var proposal: Dictionary={"id":_id(s,"proposal"),"government_id":g.id,"proposer_id":actor,"key":p.key,"value":int(p.value),"other_nation_id":str(p.get("other_nation_id","")),"product_id":str(p.get("product_id","")),"proposed_month":month,"effective_month":month+int(_p().policy_delay_months),"votes":{},"status":"voting"}
			if actor in g.council or has_authority(s,actor,g.id,"policy"): proposal.votes[actor]=true
			s.public.policy_proposals.append(proposal)
			_approve_policy(s,proposal,month)
		"public_vote_policy":
			var proposal: Dictionary=_find(s.public.policy_proposals,p.proposal_id)
			proposal.votes[actor]=bool(p.support)
			_approve_policy(s,proposal,month)
		"public_set_budget":
			var proposal: Dictionary={"id":_id(s,"budget"),"government_id":g.id,"key":str(p.category)+"_budget","value":int(p.amount),"effective_month":month+int(_p().policy_delay_months),"status":"scheduled","proposer_id":actor,"other_nation_id":"","product_id":""}
			s.public.policies.append(proposal)
		"public_transfer":
			if not _transfer(s,g.id,p.recipient_id,int(p.amount),"승인된 공공 이전지출",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			_budget_use(g,"transfers",int(p.amount),month)
		"public_announce_procurement":
			result=_announce(s,g,str(p.product_id),int(p.quantity),int(p.max_unit_price),int(p.deadline_month),bool(p.get("military",false)),month)
		"public_bid":
			var contract: Dictionary=_find(s.public.procurements,p.contract_id)
			g=_find(s.public.governments,contract.government_id)
			if not _transfer(s,co.id,g.id,int(_p().bid_fee),"공공 입찰 참가 비용",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			s.public.bids.append({"id":_id(s,"bid"),"contract_id":contract.id,"company_id":co.id,"actor_id":actor,"unit_price":int(p.unit_price),"delivery_month":int(p.delivery_month),"quality":_quality(s,co.id,contract.product_id),"status":"submitted","submitted_month":month})
		"public_award":
			result=_award(s,_find(s.public.procurements,p.contract_id),_find(s.public.bids,p.bid_id),int(p.advance_bps),month)
		"public_deliver":
			result=_deliver(s,_find(s.public.procurements,p.contract_id),co,int(p.quantity),month)
		"public_inspect":
			result=_inspect(s,_find(s.public.deliveries,p.delivery_id),month)
		"public_pay":
			result=_pay_contract(s,_find(s.public.procurements,p.contract_id),month)
		"public_cancel_contract":
			result=_cancel_contract(s,_find(s.public.procurements,p.contract_id),month)
		"public_apply_license":
			g=_government_for_nation(s,_nation_for_city(s,co.city_id))
			if not _transfer(s,co.id,g.id,int(_p().permit_fee),"국방 납품 허가 심사 비용",month): return {"ok":false,"error":"PUBLIC_PAYMENT_FAILED"}
			s.public.license_applications.append({"id":_id(s,"license"),"company_id":co.id,"government_id":g.id,"license":"defense","status":"pending","effective_month":month+int(_p().permit_delay_months)})
		"public_propose_treaty":
			s.public.treaties.append({"id":_id(s,"treaty"),"nation_a":g.nation_id,"nation_b":p.other_nation_id,"kind":p.kind,"status":"proposed","proposer_id":actor,"proposed_month":month,"effective_month":month+int(_p().policy_delay_months),"end_month":0,"grandfather_before_month":month})
		"public_accept_treaty":
			var treaty: Dictionary=_find(s.public.treaties,p.treaty_id)
			treaty.status="scheduled"
			treaty.accepted_by=actor
			treaty.effective_month=maxi(month+int(_p().policy_delay_months),int(treaty.effective_month))
		"public_set_relation":
			_set_relation(s,g.nation_id,p.other_nation_id,p.state,month,actor)
		"public_set_readiness":
			var army: Dictionary=_find(s.public.armies,p.army_id)
			army.readiness=int(p.readiness)
	if not result.get("ok",false): return result
	s.monthly_labor[actor]=int(s.monthly_labor.get(actor,0))+int(_p().political_action_hours)
	_event(s,"labor","공공·정치 활동에 사용한 개인 업무시간",month,{"actor_id":actor,"city_id":_find(s.people,actor).city_id,"hours":int(_p().political_action_hours),"command_id":c.id})
	s.public.applied_commands.append(c.id)
	_event(s,"public_command","권한과 예산을 확인한 공공 명령",month,{"actor_id":actor,"city_id":str(g.get("city_id",co.get("city_id",""))),"command_id":c.id,"command_type":c.type})
	return result

static func _approve_policy(s: Dictionary, proposal: Dictionary, month: int) -> void:
	var g: Dictionary=_find(s.public.governments,proposal.government_id)
	var yes: int=0
	for vote in proposal.votes.values():
		if vote: yes+=1
	var voters: Array=g.council.duplicate()
	var office: Dictionary=_find(s.public.offices,g.office_id)
	if not office.is_empty() and office.person_id not in voters: voters.append(office.person_id)
	if yes>=int(voters.size()/2)+1:
		proposal.status="approved"
		var policy: Dictionary=proposal.duplicate(true)
		policy.id=_id(s,"policy")
		policy.status="scheduled"
		policy.effective_month=maxi(month+int(_p().policy_delay_months),int(proposal.effective_month))
		s.public.policies.append(policy)
		_event(s,"policy_approved","절차를 통과한 정책이 다음 적용 월을 기다림",month,{"actor_id":g.id,"city_id":g.city_id,"policy_id":policy.id,"effective_month":policy.effective_month})

static func _announce(s: Dictionary, g: Dictionary, product: String, quantity: int, max_price: int, deadline: int, military: bool, month: int) -> Dictionary:
	var amount: int=quantity*max_price
	if amount>_spendable(s,g) or amount>_budget_left(g,"procurement",month): return {"ok":false,"error":"PUBLIC_PROCUREMENT_BUDGET"}
	var definition: Dictionary=_find(s.products,product)
	var contract: Dictionary={"id":_id(s,"procurement"),"government_id":g.id,"city_id":g.city_id,"product_id":product,"quantity":quantity,"max_unit_price":max_price,"unit_price":0,"deadline_month":deadline,"opened_month":month,"military":military,"delivery_terms":"delivered_duty_paid","min_quality":int(_p().procurement_min_quality),"required_license":"defense" if str(definition.get("industry",""))=="군수" else "","evaluation":{"price_weight":int(_p().price_weight),"quality_weight":int(_p().quality_weight),"delivery_weight":int(_p().delivery_weight)},"company_id":"","bid_id":"","status":"open","delivered_quantity":0,"accepted_quantity":0,"cash_paid":0,"advance_paid":0,"advance_remaining":0,"payable":0,"recognized_value":0,"reserved_value":amount,"penalties":0,"penalties_paid":0,"penalty_bps":int(_p().late_penalty_bps),"penalty_cap_bps":int(_p().late_penalty_cap_bps),"last_penalty_month":0}
	g.reserved=int(g.reserved)+amount
	_budget_use(g,"procurement",amount,month)
	s.public.procurements.append(contract)
	_event(s,"procurement_open","유한한 예산을 예약하고 공개 규격·평가 기준으로 조달 공고",month,{"actor_id":g.id,"city_id":g.city_id,"contract_id":contract.id,"product_id":product,"quantity":quantity,"amount":amount})
	return {"ok":true,"error":"","contract_id":contract.id}

static func _award(s: Dictionary, contract: Dictionary, bid: Dictionary, advance_bps: int, month: int) -> Dictionary:
	var g: Dictionary=_find(s.public.governments,contract.government_id)
	var gross: int=int(contract.quantity)*int(bid.unit_price)
	var advance: int=int(gross*advance_bps/10000)
	var old_reserve: int=int(contract.reserved_value)
	if advance>_cash(s,g.id): return {"ok":false,"error":"PUBLIC_ADVANCE_BUDGET"}
	if advance>0:
		var lines: Array=[_line(_account(s,g.id,"procurement_advances","asset"),advance),_line(g.id+":cash",-advance),_line(bid.company_id+":cash",advance),_line(_account(s,bid.company_id,"contract_advances","liability"),-advance)]
		var posted: Dictionary=Ledger.post(s,_id(s,"public-advance"),lines,"공공 계약 선급금과 이행 의무",month)
		if not posted.ok: return posted
	contract.company_id=bid.company_id
	contract.bid_id=bid.id
	contract.unit_price=int(bid.unit_price)
	contract.deadline_month=int(bid.delivery_month)
	contract.advance_paid=advance
	contract.advance_remaining=advance
	contract.cash_paid=advance
	contract.reserved_value=gross-advance
	g.reserved=int(g.reserved)-old_reserve+int(contract.reserved_value)
	contract.status="awarded"
	for item in s.public.bids:
		if item.contract_id==contract.id: item.status="awarded" if item.id==bid.id else "lost"
	_event(s,"procurement_award","공개 평가로 계약 체결·납품 전 전액 매출 인식 없음",month,{"actor_id":g.id,"city_id":g.city_id,"contract_id":contract.id,"company_id":bid.company_id,"amount":gross,"advance":advance})
	return {"ok":true,"error":""}

static func _available(s: Dictionary, owner: String, city: String, product: String) -> int:
	var value: int=0
	for lot in s.inventory:
		if lot.owner_id==owner and lot.city_id==city and lot.product_id==product and lot.get("location","") not in ["shipment","public_transit"]:
			value+=maxi(0,int(lot.quantity)-int(lot.get("reserved",0)))
	return value

static func _remove(s: Dictionary, owner: String, city: String, product: String, quantity: int, reason: String, month: int) -> bool:
	if quantity<0 or _available(s,owner,city,product)<quantity: return false
	var lots: Array=s.inventory.filter(func(x): return x.owner_id==owner and x.city_id==city and x.product_id==product and int(x.quantity)>int(x.get("reserved",0)) and x.get("location","") not in ["shipment","public_transit"])
	lots.sort_custom(func(a,b): return str(a.id)<str(b.id) if int(a.get("born_month",0))==int(b.get("born_month",0)) else int(a.get("born_month",0))<int(b.get("born_month",0)))
	var left: int=quantity
	for lot in lots:
		var take: int=mini(left,int(lot.quantity)-int(lot.get("reserved",0)))
		if take<=0: continue
		lot.quantity=int(lot.quantity)-take
		_event(s,"public_inventory",reason,month,{"actor_id":owner,"city_id":city,"product_id":product,"quantity":take,"inventory_delta":-take,"lot_id":lot.id})
		left-=take
		if left==0: break
	return left==0

static func _new_lot(s: Dictionary, owner: String, city: String, product: String, quantity: int, cost: int, month: int, reason: String, location: String = "warehouse", reserved: int = 0) -> Dictionary:
	var lot: Dictionary={"id":_id(s,"public-lot"),"owner_id":owner,"city_id":city,"accounting_city_id":city,"location":location,"product_id":product,"quantity":quantity,"reserved":reserved,"unit_cost":cost,"born_month":month}
	var definition: Dictionary=_find(s.products,product)
	if int(definition.get("shelf_life_months",0))>0: lot.expires_month=month+int(definition.shelf_life_months)
	s.inventory.append(lot)
	Domain.register_lot(s,lot)
	_event(s,"public_inventory",reason,month,{"actor_id":owner,"city_id":city,"product_id":product,"quantity":quantity,"inventory_delta":quantity,"lot_id":lot.id})
	return lot

static func _deliver(s: Dictionary, contract: Dictionary, co: Dictionary, quantity: int, month: int) -> Dictionary:
	var g: Dictionary=_find(s.public.governments,contract.government_id)
	var rule: Dictionary=_contract_trade_rule(s,contract,co.city_id,g.city_id,month)
	if not rule.allowed: return {"ok":false,"error":"PUBLIC_DELIVERY_TRADE_RESTRICTED"}
	var age: Dictionary=_stock_age(s,co.id,co.city_id,contract.product_id,quantity)
	var eta: int=month+int(rule.delay)
	var path: Array=[]
	var industry: Variant=load("res://core/industry.gd") if s.has("industry") else null
	var tariff: int=int(quantity*int(contract.unit_price)*int(rule.tariff_bps)/10000)
	if industry!=null and industry.has_method("route_quote"):
		var quote: Dictionary=industry.route_quote(s,co.city_id,g.city_id,contract.product_id,quantity,month)
		if not quote.get("allowed",false) or int(quote.get("quantity",0))<quantity: return {"ok":false,"error":str(quote.get("error","PUBLIC_DELIVERY_ROUTE_CAPACITY"))}
		if _cash(s,co.id)<int(quote.get("fee",0))+tariff: return {"ok":false,"error":"PUBLIC_DELIVERY_FREIGHT_BUDGET"}
		if quote.get("carrier_id","")==co.id and contract.product_id=="fuel" and quote.get("carrier_city","")==co.city_id and _available(s,co.id,co.city_id,"fuel")<quantity+int(quote.get("fuel",0)): return {"ok":false,"error":"PUBLIC_DELIVERY_FUEL_STOCK"}
		var reserved: Dictionary=industry.reserve_route(s,co.id,co.city_id,g.city_id,contract.product_id,quantity,month,quote)
		if not reserved.get("ok",false): return reserved
		path=quote.get("path",[]).duplicate()
		eta=maxi(eta,int(quote.get("eta",month)))
	if tariff>0 and not _transfer(s,co.id,str(rule.tariff_receiver),tariff,"납품 인도 조건에 따른 실제 관세 납부",month,{"contract_id":contract.id,"product_id":contract.product_id}): return {"ok":false,"error":"PUBLIC_DELIVERY_TARIFF_BUDGET"}
	if not _remove(s,co.id,co.city_id,contract.product_id,quantity,"납품 출고·소유권은 검수까지 공급자 유지",month): return {"ok":false,"error":"PUBLIC_DELIVERY_STOCK_MISSING"}
	var lot: Dictionary=_new_lot(s,co.id,co.city_id,contract.product_id,quantity,int(contract.unit_price),month,"검수 조건부 납품 물량", "inspection" if eta==month else "public_transit",quantity)
	_apply_age(lot,age)
	lot.accounting_city_id=co.city_id
	if eta==month: lot.city_id=g.city_id
	var delivery: Dictionary={"id":_id(s,"delivery"),"contract_id":contract.id,"company_id":co.id,"government_id":g.id,"product_id":contract.product_id,"quantity":quantity,"quality":_quality(s,co.id,contract.product_id),"lot_id":lot.id,"from_city_id":co.city_id,"to_city_id":g.city_id,"shipped_month":month,"arrive_month":eta,"route_ids":path,"status":"awaiting_inspection" if eta==month else "transit","accepted_month":0}
	s.public.deliveries.append(delivery)
	contract.delivered_quantity=int(contract.delivered_quantity)+quantity
	contract.status="partially_delivered"
	_event(s,"procurement_delivery","공급자 재고를 출고하고 납기·검수 의무를 연결",month,{"actor_id":co.id,"city_id":g.city_id,"contract_id":contract.id,"delivery_id":delivery.id,"product_id":contract.product_id,"quantity":quantity,"arrival_month":eta})
	return {"ok":true,"error":"","delivery_id":delivery.id}

static func _inventory_account(s: Dictionary, owner: String, product: String) -> String:
	# Public lots carry acquisition unit cost; this account is never cash income.
	var finance: Variant=_finance(s)
	if finance!=null and finance.has_method("inventory_account"): return str(finance.inventory_account(s,owner,product))
	return _account(s,owner,"public_inventory","asset")

static func _inspect(s: Dictionary, delivery: Dictionary, month: int) -> Dictionary:
	var contract: Dictionary=_find(s.public.procurements,delivery.contract_id)
	var g: Dictionary=_find(s.public.governments,contract.government_id)
	var lot: Dictionary=_find(s.inventory,delivery.lot_id)
	if delivery.status!="awaiting_inspection" or lot.is_empty() or int(lot.quantity)!=int(delivery.quantity) or int(lot.reserved)!=int(delivery.quantity): return {"ok":false,"error":"PUBLIC_INSPECTION_STOCK_MISMATCH"}
	if int(delivery.quality)<int(contract.min_quality):
		delivery.status="rejected"
		lot.reserved=0
		lot.location="warehouse"
		contract.delivered_quantity=int(contract.delivered_quantity)-int(delivery.quantity)
		_event(s,"procurement_rejected","실제 납품 규격 미달·매출/대금 지급 없음",month,{"actor_id":g.id,"city_id":g.city_id,"delivery_id":delivery.id,"contract_id":contract.id,"product_id":contract.product_id,"quantity":delivery.quantity})
		return {"ok":true,"error":"","accepted":false}
	var gross: int=int(delivery.quantity)*int(contract.unit_price)
	var advance_offset: int=mini(int(contract.advance_remaining),int(int(contract.advance_paid)*int(delivery.quantity)/maxi(1,int(contract.quantity))))
	if int(contract.accepted_quantity)+int(delivery.quantity)==int(contract.quantity): advance_offset=int(contract.advance_remaining)
	var seller: String=str(contract.company_id)
	var lines: Array=[_line(_inventory_account(s,g.id,contract.product_id),gross),_line(_account(s,g.id,"procurement_payable","liability"),-gross),_line(_account(s,seller,"procurement_receivable","asset"),gross),_line(_account(s,seller,"procurement_revenue","revenue"),-gross)]
	if advance_offset>0:
		lines.append(_line(_account(s,g.id,"procurement_payable","liability"),advance_offset))
		lines.append(_line(_account(s,g.id,"procurement_advances","asset"),-advance_offset))
		lines.append(_line(_account(s,seller,"contract_advances","liability"),advance_offset))
		lines.append(_line(_account(s,seller,"procurement_receivable","asset"),-advance_offset))
	var posted: Dictionary=Ledger.post(s,_id(s,"public-inspection"),lines,"검수 완료에 따른 재고·채무·매출·선급금 정산",month)
	if not posted.ok: return posted
	var sale_cost: int=_sale_cost(s,seller,contract.product_id,int(delivery.quantity),month)
	var supplier: Dictionary=_find(s.companies,seller)
	if not supplier.is_empty(): supplier.expenses=int(supplier.get("expenses",0))+sale_cost
	lot.quantity=0
	lot.reserved=0
	_event(s,"public_inventory","검수 후 납품 소유권 이전",month,{"actor_id":seller,"city_id":g.city_id,"product_id":contract.product_id,"quantity":delivery.quantity,"inventory_delta":-int(delivery.quantity),"lot_id":lot.id})
	var accepted: Dictionary=_new_lot(s,g.id,g.city_id,contract.product_id,int(delivery.quantity),int(contract.unit_price),month,"공공 검수 인수", "military" if contract.military else "warehouse")
	_apply_age(accepted,{"born_month":lot.get("born_month",month),"expires_month":lot.get("expires_month",0)})
	if contract.military:
		var army: Dictionary={}
		for a in s.public.armies:
			if a.government_id==g.id: army=a
		if not army.is_empty(): s.public.military_lots.append({"id":_id(s,"military-lot-ref"),"army_id":army.id,"lot_id":accepted.id,"contract_id":contract.id})
	contract.accepted_quantity=int(contract.accepted_quantity)+int(delivery.quantity)
	contract.recognized_value=int(contract.recognized_value)+gross
	contract.advance_remaining=int(contract.advance_remaining)-advance_offset
	contract.payable=int(contract.payable)+gross-advance_offset
	delivery.status="accepted"
	delivery.accepted_month=month
	var co: Dictionary=_find(s.companies,seller)
	if not co.is_empty(): co.revenue=int(co.get("revenue",0))+gross
	_event(s,"trade","검수로 확정된 공공 납품 거래",month,{"actor_id":g.id,"buyer_id":g.id,"seller_id":seller,"city_id":g.city_id,"seller_city_id":delivery.from_city_id,"product_id":contract.product_id,"quantity":delivery.quantity,"amount":gross,"contract_id":contract.id,"delivery_id":delivery.id,"government_purchase":true})
	_event(s,"procurement_inspected","검수 통과 물량만 인수·매출과 지급 의무 발생",month,{"actor_id":g.id,"city_id":g.city_id,"company_id":seller,"contract_id":contract.id,"delivery_id":delivery.id,"product_id":contract.product_id,"quantity":delivery.quantity,"amount":gross})
	return {"ok":true,"error":"","accepted":true}

static func _pay_contract(s: Dictionary, contract: Dictionary, month: int) -> Dictionary:
	var g: Dictionary=_find(s.public.governments,contract.government_id)
	var amount: int=int(contract.payable)
	if amount<=0: return {"ok":false,"error":"PUBLIC_NOTHING_INSPECTED_PAYABLE"}
	if amount>_cash(s,g.id)-maxi(0,int(g.reserved)-int(contract.reserved_value)): return {"ok":false,"error":"PUBLIC_PAYMENT_BUDGET"}
	var lines: Array=[_line(_account(s,g.id,"procurement_payable","liability"),amount),_line(g.id+":cash",-amount),_line(contract.company_id+":cash",amount),_line(_account(s,contract.company_id,"procurement_receivable","asset"),-amount)]
	var posted: Dictionary=Ledger.post(s,_id(s,"public-settlement"),lines,"검수 채무의 실제 대금 지급",month)
	if not posted.ok: return posted
	contract.cash_paid=int(contract.cash_paid)+amount
	contract.payable=0
	contract.reserved_value=maxi(0,int(contract.reserved_value)-amount)
	g.reserved=maxi(0,int(g.reserved)-amount)
	if int(contract.accepted_quantity)==int(contract.quantity) and int(contract.payable)==0:
		contract.status="fulfilled"
	_event(s,"procurement_payment","검수 완료분의 예산·현금·채권·채무 동시 정산",month,{"actor_id":g.id,"city_id":g.city_id,"contract_id":contract.id,"company_id":contract.company_id,"amount":amount})
	return {"ok":true,"error":""}

static func _cancel_contract(s: Dictionary, contract: Dictionary, month: int) -> Dictionary:
	var g: Dictionary=_find(s.public.governments,contract.government_id)
	var release: int=maxi(0,int(contract.reserved_value)-int(contract.payable))
	g.reserved=maxi(0,int(g.reserved)-release)
	contract.reserved_value=int(contract.payable)
	contract.status="cancelled"
	for bid in s.public.bids:
		if bid.contract_id==contract.id and bid.status=="submitted": bid.status="cancelled"
	for delivery in s.public.deliveries:
		if delivery.contract_id==contract.id and delivery.status in ["transit","awaiting_inspection"]:
			var lot: Dictionary=_find(s.inventory,delivery.lot_id)
			if not lot.is_empty():
				lot.reserved=0
				lot.location="warehouse"
			delivery.status="returned_to_supplier_title"
	var advance: int=int(contract.advance_remaining)
	if advance>0 and not str(contract.company_id).is_empty():
		var recovered: int=mini(advance,_cash(s,contract.company_id))
		if recovered>0:
			var lines: Array=[_line(_account(s,contract.company_id,"contract_advances","liability"),recovered),_line(contract.company_id+":cash",-recovered),_line(g.id+":cash",recovered),_line(_account(s,g.id,"procurement_advances","asset"),-recovered)]
			var posted: Dictionary=Ledger.post(s,_id(s,"public-advance-refund"),lines,"미이행 선급금의 실제 반환",month)
			if posted.ok: contract.advance_remaining=advance-recovered
	_event(s,"procurement_cancelled","미사용 예산 해제·검수 채무와 반환되지 않은 선급금 청구권 유지",month,{"actor_id":g.id,"city_id":g.city_id,"contract_id":contract.id,"amount":release,"advance_receivable":contract.advance_remaining})
	return {"ok":true,"error":""}

static func _finance(s: Dictionary) -> Variant:
	return load("res://core/finance.gd") if s.has("finance") and ResourceLoader.exists("res://core/finance.gd") else null

static func _sale_cost(s: Dictionary, owner: String, product: String, quantity: int, month: int) -> int:
	var finance: Variant=_finance(s)
	if finance!=null and finance.has_method("cost_of_sale"):
		var result: Dictionary=finance.cost_of_sale(s,owner,product,quantity,month,_id(s,"public-cogs"))
		return int(result.get("cost",0)) if result.get("ok",false) else 0
	return 0

static func _consume_lot(s: Dictionary, lot: Dictionary, quantity: int, reason: String, month: int, event_type: String = "government_consumption") -> int:
	if int(lot.get("expires_month",0))>0 and int(lot.expires_month)<=month: return 0
	var used: int=mini(quantity,maxi(0,int(lot.quantity)-int(lot.get("reserved",0))))
	if used<=0: return 0
	var cost: int=used*int(lot.get("unit_cost",0))
	if cost>0:
		var account: String=_inventory_account(s,lot.owner_id,lot.product_id)
		cost=mini(cost,maxi(0,Ledger.balance(s,account)))
		if cost>0:
			var posted: Dictionary=Ledger.post(s,_id(s,"public-use"),[_line(_account(s,lot.owner_id,"public_service_expense","expense"),cost),_line(account,-cost)],reason,month)
			if not posted.ok: return 0
	lot.quantity=int(lot.quantity)-used
	_event(s,event_type,reason,month,{"actor_id":lot.owner_id,"city_id":lot.city_id,"product_id":lot.product_id,"quantity":used,"inventory_delta":-used,"lot_id":lot.id,"amount":cost})
	_nonmarket_output(s,lot.owner_id,lot.city_id,cost,month,"공공·군 서비스에 실제 사용한 재고 원가",true,lot.product_id,used)
	return used

static func _public_buy(s: Dictionary, g: Dictionary, product: String, wanted: int, month: int, category: String) -> int:
	if wanted<=0: return 0
	var sellers: Array=[]
	for company in s.companies:
		if company.city_id!=g.city_id or company.get("status","active")!="active" or not _can_produce(s,company.id,product): continue
		if _available(s,company.id,company.city_id,product)>0: sellers.append(company)
	sellers.sort_custom(func(a,b):
		var pa: int=_offer_price(s,a,product)
		var pb: int=_offer_price(s,b,product)
		return str(a.id).sha256_text()<str(b.id).sha256_text() if pa==pb else pa<pb
	)
	var acquired: int=0
	for seller in sellers:
		var price: int=_offer_price(s,seller,product)
		var possible: int=mini(wanted-acquired,_available(s,seller.id,seller.city_id,product))
		possible=mini(possible,int(mini(_spendable(s,g),_budget_left(g,category,month))/price))
		if possible<=0: continue
		var amount: int=possible*price
		var age: Dictionary=_stock_age(s,seller.id,seller.city_id,product,possible)
		var lines: Array=[_line(_inventory_account(s,g.id,product),amount),_line(g.id+":cash",-amount),_line(seller.id+":cash",amount),_line(_account(s,seller.id,"public_sales","revenue"),-amount)]
		var posted: Dictionary=Ledger.post(s,_id(s,"public-purchase"),lines,"현지 공공 서비스용 실제 자재 구매",month)
		if not posted.ok: continue
		var cost: int=_sale_cost(s,seller.id,product,possible,month)
		_remove(s,seller.id,seller.city_id,product,possible,"공공 서비스 자재 판매 출고",month)
		var received: Dictionary=_new_lot(s,g.id,g.city_id,product,possible,price,month,"공공 서비스 자재 매입 입고")
		_apply_age(received,age)
		seller.revenue=int(seller.get("revenue",0))+amount
		seller.expenses=int(seller.get("expenses",0))+cost
		_budget_use(g,category,amount,month)
		acquired+=possible
		_event(s,"trade","공공 서비스의 유효 예산과 현지 실제 재고 체결",month,{"actor_id":g.id,"buyer_id":g.id,"seller_id":seller.id,"city_id":g.city_id,"seller_city_id":seller.city_id,"product_id":product,"quantity":possible,"amount":amount})
		if acquired>=wanted: break
	return acquired

static func _offer_price(s: Dictionary, company: Dictionary, product: String) -> int:
	var price: int=int(_find(s.products,product).get("price",1))
	return maxi(1,int(round(float(price)*float(company.get("policy",{}).get("price_percent",100))/100.0)))

static func _pay_hours(s: Dictionary, g: Dictionary, person: Dictionary, requested: int, category: String, reason: String, month: int) -> int:
	var free: int=maxi(0,int(person.hours)-int(s.monthly_labor.get(person.id,0)))
	var hourly: int=int(_p().hourly_wage)
	var hours: int=mini(requested,free)
	hours=mini(hours,int(mini(_spendable(s,g),_budget_left(g,category,month))/hourly))
	if hours<=0: return 0
	if not _transfer(s,g.id,person.id,hours*hourly,reason,month,{"hours":hours,"city_id":g.city_id}): return 0
	s.monthly_labor[person.id]=int(s.monthly_labor.get(person.id,0))+hours
	_budget_use(g,category,hours*hourly,month)
	_event(s,"labor",reason,month,{"actor_id":person.id,"company_id":g.id,"city_id":g.city_id,"hours":hours})
	_nonmarket_output(s,g.id,g.city_id,hours*hourly,month,"실제 공직·공공·군 근무의 비용 평가",false,"",hours)
	return hours

static func _activate_policies(s: Dictionary, month: int) -> void:
	for policy in s.public.policies:
		if policy.status!="scheduled" or int(policy.effective_month)>month: continue
		var g: Dictionary=_find(s.public.governments,policy.government_id)
		if g.is_empty(): continue
		var key: String=str(policy.key)
		if key in ["tax_bps","tariff_bps"]: g[key]=int(policy.value)
		elif key.ends_with("_budget"):
			var category: String=key.trim_suffix("_budget")
			if g.budget.has(category): g.budget[category]=int(policy.value)
		elif key=="mobilization": g.mobilization=int(policy.value)>0
		elif key=="interest_bps":
			g.interest_bps=int(policy.value)
			for bank in s.get("finance",{}).get("banks",[]):
				if bank.get("nation_id","")==g.nation_id: bank.loan_interest_bps=int(policy.value)
			for central in s.get("finance",{}).get("central_banks",[]):
				if central.get("nation_id","")==g.nation_id: central.policy_rate_bps=int(policy.value)
		policy.status="active"
		_event(s,"policy_effective","승인된 정책의 발효 월 도래",month,{"actor_id":g.id,"city_id":g.city_id,"policy_id":policy.id,"policy_key":key,"value":int(policy.value)})
	for treaty in s.public.treaties:
		if treaty.status=="scheduled" and int(treaty.effective_month)<=month:
			treaty.status="active"
			if treaty.kind=="ceasefire":
				var relation: Dictionary=_find(s.public.relations,_pair(treaty.nation_a,treaty.nation_b))
				if not relation.is_empty(): relation.state="neutral"
				for conflict in s.public.conflicts:
					if conflict.pair_id==_pair(treaty.nation_a,treaty.nation_b) and conflict.status=="active":
						conflict.status="ceasefire"
						conflict.ended_month=month
			_event(s,"treaty_effective","쌍방이 승인한 협정 발효",month,{"actor_id":_government_for_nation(s,treaty.nation_a).get("id",""),"city_id":_government_for_nation(s,treaty.nation_a).get("city_id",""),"treaty_id":treaty.id})

static func begin_month(s: Dictionary, month: int) -> void:
	if not s.has("public") or int(s.public.last_prepared_month)>=month: return
	s.public.last_prepared_month=month
	s.public.provided_needs={}
	s.public.mobilized=[]
	_activate_policies(s,month)
	for change in s.public.service_changes:
		if change.status!="pending" or int(change.effective_month)>month: continue
		var army: Dictionary=_find(s.public.armies,change.army_id)
		if army.is_empty(): continue
		if change.action=="join" and change.person_id not in army.members: army.members.append(change.person_id)
		if change.action=="leave": army.members.erase(change.person_id)
		change.status="applied"
		_event(s,"military_employment","실제 통지기간을 거친 군 근무 계약 변경",month,{"actor_id":change.person_id,"city_id":army.city_id,"army_id":army.id,"action":change.action})
	for g in s.public.governments:
		if int(g.budget_month)!=month:
			g.budget_month=month
			g.budget_used={"services":0,"procurement":0,"military":0,"transfers":0}
		g.last_tax_revenue=0
		g.last_service_cost=0
	for office in s.public.offices:
		var g: Dictionary=_find(s.public.governments,office.government_id)
		var person: Dictionary=_find(s.people,office.person_id)
		if not person.is_empty(): _pay_hours(s,g,person,int(_p().office_hours),"services","공직 근무 보수",month)
	for army in s.public.armies:
		var g: Dictionary=_find(s.public.governments,army.government_id)
		army.paid_hours=0
		var needed: int=army.members.size()*int(_p().army_food_per_member)
		var food: int=0
		for ref in s.public.military_lots:
			if ref.army_id!=army.id: continue
			var lot: Dictionary=_find(s.inventory,ref.lot_id)
			if lot.is_empty() or lot.product_id!="food": continue
			food+=_consume_lot(s,lot,needed-food,"군의 실제 급식 사용",month,"military_consumption")
			if food>=needed: break
		var food_left: int=food
		for member in army.members:
			var person: Dictionary=_find(s.people,member)
			if person.is_empty(): continue
			var hours: int=_pay_hours(s,g,person,int(_p().army_hours)*int(army.readiness),"military","군 근무 보수",month)
			army.paid_hours=int(army.paid_hours)+hours
			if food_left>0:
				var meal: int=mini(food_left,int(_p().army_food_per_member))
				s.public.provided_needs[str(person.id)+":food"]={"quantity":meal,"month":month,"source":army.id}
				food_left-=meal
			if g.mobilization:
				var mobilized: int=mini(int(_p().mobilization_hours),maxi(0,int(person.hours)-int(s.monthly_labor.get(person.id,0))))
				s.monthly_labor[person.id]=int(s.monthly_labor.get(person.id,0))+mobilized
				s.public.mobilized.append({"person_id":person.id,"army_id":army.id,"hours":mobilized,"month":month})
		army.last_supply_ratio=100 if needed==0 else int(food*100/needed)
		army.effective_readiness=mini(int(army.readiness),int(army.paid_hours/maxi(1,army.members.size()*int(_p().army_hours))))
		if food<needed: army.effective_readiness=0
		if month%int(_p().army_tools_period_months)==0:
			var tools: int=0
			for ref in s.public.military_lots:
				if ref.army_id!=army.id: continue
				var lot: Dictionary=_find(s.inventory,ref.lot_id)
				if lot.is_empty() or lot.product_id!="tools": continue
				tools+=_consume_lot(s,lot,int(_p().army_tools_use)-tools,"군 장비의 실제 정비 공구 사용",month,"military_consumption")
				if tools>=int(_p().army_tools_use): break
			if tools<int(_p().army_tools_use): army.effective_readiness=0

static func provided_need(s: Dictionary, person: String, product: String, month: int) -> int:
	var record: Dictionary=s.get("public",{}).get("provided_needs",{}).get(person+":"+product,{})
	return int(record.get("quantity",0)) if int(record.get("month",-1))==month else 0

static func _taxes(s: Dictionary, month: int) -> void:
	for g in s.public.governments:
		for owner in s.people+s.companies:
			var city: String=str(owner.get("city_id",""))
			if (g.kind=="city" and city!=g.city_id) or (g.kind=="nation" and _nation_for_city(s,city)!=g.nation_id): continue
			var base: int=maxi(0,int(owner.get("last_income",0))) if owner.has("hours") else maxi(0,int(owner.get("revenue",0))-int(owner.get("expenses",0)))
			var due: int=int(base*int(g.tax_bps)/10000)
			if due<=0: continue
			var tax_id: String="tax:"+g.id+":"+str(owner.id)+":"+str(month)
			if _find(s.public.tax_assessments,tax_id).is_empty():
				var accrued: Dictionary=Ledger.post(s,tax_id,[_line(_account(s,owner.id,"tax_expense","expense"),due),_line(_account(s,owner.id,"tax_payable","liability"),-due),_line(_account(s,g.id,"tax_receivable","asset"),due),_line(_account(s,g.id,"tax_revenue","revenue"),-due)],"공개 세율과 과세표준에 따른 세액 발생",month)
				if accrued.ok: s.public.tax_assessments.append({"id":tax_id,"government_id":g.id,"payer_id":owner.id,"city_id":city,"assessed":due,"paid":0,"outstanding":due,"month":month,"status":"due"})
	for tax in s.public.tax_assessments:
		if int(tax.outstanding)<=0: continue
		var estate: Dictionary=_find(s.companies,tax.payer_id)
		if not estate.is_empty() and estate.get("status","active")!="active": continue
		var g: Dictionary=_find(s.public.governments,tax.government_id)
		var amount: int=mini(int(tax.outstanding),_cash(s,tax.payer_id))
		if amount>0:
			var posted: Dictionary=Ledger.post(s,_id(s,"public-tax-settle"),[_line(_account(s,tax.payer_id,"tax_payable","liability"),amount),_line(tax.payer_id+":cash",-amount),_line(g.id+":cash",amount),_line(_account(s,g.id,"tax_receivable","asset"),-amount)],"발생 세금의 실제 납부",month)
			if posted.ok:
				tax.paid=int(tax.paid)+amount
				tax.outstanding=int(tax.outstanding)-amount
				g.last_tax_revenue=int(g.last_tax_revenue)+amount
				_event(s,"tax_payment","관할별 세금 납부·미납 잔액은 채무로 유지",month,{"actor_id":tax.payer_id,"government_id":g.id,"city_id":tax.city_id,"amount":amount,"tax_id":tax.id})
		tax.status="paid" if int(tax.outstanding)==0 else "arrears"
	for g in s.public.governments:
		g.arrears=0
		for tax in s.public.tax_assessments:
			if tax.government_id==g.id: g.arrears=int(g.arrears)+int(tax.outstanding)

static func _services(s: Dictionary, month: int) -> void:
	for g in s.public.governments:
		var worked: int=0
		for person in _city_people(s,g.city_id):
			if worked>=int(_p().public_workers_per_city): break
			if _pay_hours(s,g,person,int(_p().public_worker_hours),"services","공공 서비스 근로",month)>0: worked+=1
		var need_food: int=worked*int(_p().service_food_per_worker)
		var need_water: int=worked*int(_p().service_water_per_worker)
		_public_buy(s,g,"food",maxi(0,need_food-_civil_stock(s,g.id,g.city_id,"food")),month,"services")
		var utility_water: int=_utility_service(s,g,"water",need_water,month)
		if utility_water<0: _public_buy(s,g,"water",maxi(0,need_water-_civil_stock(s,g.id,g.city_id,"water")),month,"services")
		var food: int=0
		var water: int=maxi(0,utility_water)
		for lot in s.inventory:
			if lot.owner_id!=g.id or lot.city_id!=g.city_id or lot.get("location","")=="military": continue
			if lot.product_id=="food" and food<need_food: food+=_consume_lot(s,lot,need_food-food,"공공 서비스 급식·돌봄 투입",month)
			if lot.product_id=="water" and water<need_water: water+=_consume_lot(s,lot,need_water-water,"공공 위생·돌봄 용수 투입",month)
		g.services=mini(worked,mini(int(food/maxi(1,int(_p().service_food_per_worker))),int(water/maxi(1,int(_p().service_water_per_worker)))))
		g.last_service_cost=int(g.budget_used.services)
		_event(s,"public_service","실제 직원·급식·용수를 사용한 공공 서비스 제공",month,{"actor_id":g.id,"city_id":g.city_id,"quantity":int(g.services),"amount":int(g.last_service_cost),"unit":"service_slots"})
		var welfare: int=_budget_left(g,"transfers",month)
		for person in _city_people(s,g.city_id):
			if welfare<=0: break
			if _cash(s,person.id)>=int(_p().minimum_welfare_cash): continue
			var amount: int=mini(int(_p().welfare_transfer),mini(welfare,_spendable(s,g)))
			if amount>0 and _transfer(s,g.id,person.id,amount,"예산 범위의 생활 이전지출",month):
				_budget_use(g,"transfers",amount,month)
				welfare-=amount

static func _arrive_deliveries(s: Dictionary, month: int) -> void:
	for delivery in s.public.deliveries:
		if delivery.status not in ["transit","awaiting_inspection"]: continue
		var lot: Dictionary=_find(s.inventory,delivery.lot_id)
		if not lot.is_empty() and int(lot.get("expires_month",0))>0 and int(lot.expires_month)<=month:
			_spoil_locked_delivery(s,delivery,lot,month)
			continue
		if not lot.is_empty() and int(lot.quantity)<int(delivery.quantity):
			var lost: int=int(delivery.quantity)-int(lot.quantity)
			var contract: Dictionary=_find(s.public.procurements,delivery.contract_id)
			contract.delivered_quantity=maxi(0,int(contract.delivered_quantity)-lost)
			delivery.quantity=int(lot.quantity)
			lot.reserved=mini(int(lot.reserved),int(lot.quantity))
			_event(s,"procurement_loss","실제 납품 재고 손실을 계약의 미이행 물량에 반영",month,{"actor_id":delivery.company_id,"city_id":lot.city_id,"contract_id":contract.id,"delivery_id":delivery.id,"product_id":delivery.product_id,"quantity":lost})
			if int(delivery.quantity)==0: delivery.status="lost"
		if delivery.status!="transit" or int(delivery.arrive_month)>month: continue
		if lot.is_empty(): continue
		var contract: Dictionary=_find(s.public.procurements,delivery.contract_id)
		var rule: Dictionary=_contract_trade_rule(s,contract,delivery.from_city_id,delivery.to_city_id,month)
		var closed: bool=not rule.allowed
		for route_id in delivery.get("route_ids",[]):
			if _find(s.get("industry",{}).get("routes",[]),str(route_id)).get("blocked",false): closed=true
		if closed:
			delivery.arrive_month=month+1
			_event(s,"procurement_route_delay","납품 중 항로·법적 접근 제한으로 화물 보존 대기",month,{"actor_id":delivery.company_id,"city_id":delivery.to_city_id,"delivery_id":delivery.id,"contract_id":delivery.contract_id,"quantity":delivery.quantity})
			continue
		_move_lot_city(s,lot,str(delivery.to_city_id))
		lot.location="inspection"
		delivery.status="awaiting_inspection"
		_event(s,"procurement_arrival","예약된 납품 화물 도착·검수 전 사용 금지",month,{"actor_id":delivery.company_id,"city_id":delivery.to_city_id,"delivery_id":delivery.id,"contract_id":delivery.contract_id,"product_id":delivery.product_id,"quantity":delivery.quantity,"lot_id":lot.id})

static func _elections(s: Dictionary, month: int) -> void:
	for g in s.public.governments:
		var due: Array=s.public.candidates.filter(func(c): return c.government_id==g.id and c.status=="campaigning" and int(c.election_month)<=month)
		if due.is_empty(): continue
		for candidate in due:
			candidate.votes=0
			for ballot in s.public.ballots:
				if ballot.candidate_id==candidate.id and int(ballot.election_month)==int(candidate.election_month): candidate.votes=int(candidate.votes)+1
		var residents: Array=[]
		for person in s.people:
			if _eligible(s,person.id,g): residents.append(person)
		for person in residents:
			if s.public.ballots.any(func(b): return b.person_id==person.id and b.government_id==g.id and int(b.election_month)==int(due[0].election_month)): continue
			var candidates: Array=due.duplicate()
			candidates.sort_custom(func(a,b): return _candidate_score(s,person,a,g)>_candidate_score(s,person,b,g))
			if not candidates.is_empty(): candidates[0].votes=int(candidates[0].votes)+1
		due.sort_custom(func(a,b): return str(a.id).sha256_text()<str(b.id).sha256_text() if int(a.votes)==int(b.votes) else int(a.votes)>int(b.votes))
		var winner: Dictionary=due[0]
		var office: Dictionary=_find(s.public.offices,g.office_id)
		var previous: String=str(office.person_id)
		office.person_id=winner.person_id
		office.term_start=month
		office.term_end=month+int(_p().office_term_months)
		g.last_election_month=month
		for candidate in due: candidate.status="elected" if candidate.id==winner.id else "lost"
		_event(s,"election_result","관할 인물의 표와 생활·서비스 평가에 따른 공직 교체",month,{"actor_id":g.id,"city_id":g.city_id,"person_id":winner.person_id,"previous_person_id":previous,"votes":winner.votes,"election_month":month})

static func _candidate_score(s: Dictionary, person: Dictionary, candidate: Dictionary, g: Dictionary) -> int:
	var preferred: String=_config().platforms[int(abs(str(person.id).hash()))%_config().platforms.size()]
	var score: int=int(_p().election_preference_bonus) if candidate.platform==preferred else 0
	if candidate.platform=="services": score+=maxi(0,int(_p().public_service_target)-int(g.services))
	if candidate.platform=="growth": score+=1 if person.get("employment_status","")=="employed" else int(_p().election_preference_bonus)
	if candidate.platform=="security":
		var relation_count: int=0
		for relation in s.public.relations:
			if g.nation_id in [relation.nation_a,relation.nation_b] and relation.state in ["tension","conflict"]: relation_count+=1
		score+=relation_count
	score+=mini(int(_p().campaign_awareness_cap),int(candidate.get("awareness",0)))
	if candidate.person_id==person.id: score+=1
	return score*10000+int((str(person.id)+str(candidate.id)).sha256_text().left(3).hex_to_int())

static func _set_relation(s: Dictionary, nation_a: String, nation_b: String, state: String, month: int, actor: String) -> void:
	var relation: Dictionary=_find(s.public.relations,_pair(nation_a,nation_b))
	if relation.is_empty():
		relation={"id":_pair(nation_a,nation_b),"nation_a":nation_a,"nation_b":nation_b,"state":state,"since_month":month,"reason":"권한 있는 외교 명령"}
		s.public.relations.append(relation)
	else:
		if relation.state=="conflict" and state!="conflict": return
		relation.state=state
		relation.since_month=month
		relation.reason="권한 있는 외교 명령"
	if state=="conflict" and not s.public.conflicts.any(func(x): return x.pair_id==relation.id and x.status=="active"):
		s.public.conflicts.append({"id":_id(s,"conflict"),"pair_id":relation.id,"nation_a":nation_a,"nation_b":nation_b,"started_month":month,"ended_month":0,"status":"active","last_damage_month":0,"initiator_id":actor})
	_event(s,"diplomacy","국가 간 외교 상태 변경의 경제 경로 기록",month,{"actor_id":actor,"city_id":_government_for_nation(s,nation_a).get("city_id",""),"nation_a":nation_a,"nation_b":nation_b,"state":state})

static func _conflicts(s: Dictionary, month: int) -> void:
	for conflict in s.public.conflicts:
		if conflict.status!="active" or month<=int(conflict.started_month) or month%int(_p().conflict_damage_period_months)!=0 or int(conflict.last_damage_month)==month: continue
		var capable: bool=false
		for army in s.public.armies:
			if army.nation_id in [conflict.nation_a,conflict.nation_b] and int(army.effective_readiness)>0: capable=true
		if not capable: continue
		var targets: Array=[]
		for facility in s.facilities:
			if _nation_for_city(s,facility.city_id) in [conflict.nation_a,conflict.nation_b] and facility.get("status","") in ["operational","ready"]: targets.append(facility)
		targets.sort_custom(func(a,b): return (str(s.seed)+str(month)+str(a.id)).sha256_text()<(str(s.seed)+str(month)+str(b.id)).sha256_text())
		if targets.is_empty(): continue
		var facility: Dictionary=targets[0]
		facility.damage_percent=mini(100,int(facility.get("damage_percent",0))+int(_p().conflict_damage_percent))
		facility.status="damaged"
		facility.repair_required=true
		conflict.last_damage_month=month
		_event(s,"conflict_damage","실제 분쟁과 유효 군 대비 상태에 따른 시설 피해·복구 수요",month,{"actor_id":facility.company_id,"city_id":facility.city_id,"facility_id":facility.id,"conflict_id":conflict.id,"damage_percent":facility.damage_percent})

static func _arrived_quantity(s: Dictionary, contract: Dictionary) -> int:
	var amount: int=0
	for delivery in s.public.deliveries:
		if delivery.contract_id==contract.id and delivery.status in ["awaiting_inspection","accepted"]: amount+=int(delivery.quantity)
	return amount

static func monthly(s: Dictionary, month: int) -> void:
	if not s.has("public"): return
	begin_month(s,month)
	_arrive_deliveries(s,month)
	_services(s,month)
	_taxes(s,month)
	_elections(s,month)
	_conflicts(s,month)
	for application in s.public.license_applications:
		if application.status!="pending" or int(application.effective_month)>month: continue
		var company: Dictionary=_find(s.companies,application.company_id)
		if company.is_empty() or company.get("status","active")!="active":
			application.status="rejected"
			continue
		if not company.has("licenses"): company.licenses=[]
		if application.license not in company.licenses: company.licenses.append(application.license)
		application.status="approved"
		_event(s,"license_approved","이미 납부한 심사와 경과 기간을 거친 국방 납품 허가",month,{"actor_id":company.id,"city_id":company.city_id,"license":application.license})
	for contract in s.public.procurements:
		var supplier: Dictionary=_find(s.companies,str(contract.get("company_id","")))
		if contract.status in ["awarded","partially_delivered","late"] and not str(contract.get("company_id","")).is_empty() and (supplier.is_empty() or supplier.get("status","active")!="active"):
			_default_contract(s,contract,month)
			continue
		if contract.status=="open" and int(contract.deadline_month)<month:
			var government: Dictionary=_find(s.public.governments,contract.government_id)
			government.reserved=maxi(0,int(government.reserved)-int(contract.reserved_value))
			contract.reserved_value=0
			contract.status="expired"
			for bid in s.public.bids:
				if bid.contract_id==contract.id and bid.status=="submitted": bid.status="expired"
			_event(s,"procurement_expired","기한 내 계약이 없어 미사용 예산 예약 해제",month,{"actor_id":government.id,"city_id":government.city_id,"contract_id":contract.id})
			continue
		if contract.status in ["awarded","partially_delivered","late"] and int(contract.deadline_month)<month and _arrived_quantity(s,contract)<int(contract.quantity):
			contract.status="late"
			if int(contract.last_penalty_month)!=month and not str(contract.company_id).is_empty():
				var base: int=maxi(0,int(contract.quantity)-int(contract.accepted_quantity))*int(contract.unit_price)
				var cap: int=int(int(contract.quantity)*int(contract.unit_price)*int(_p().late_penalty_cap_bps)/10000)
				var penalty: int=mini(maxi(0,cap-int(contract.penalties)),int(base*int(_p().late_penalty_bps)/10000))
				if penalty>0:
					var g: Dictionary=_find(s.public.governments,contract.government_id)
					var posted: Dictionary=Ledger.post(s,_id(s,"public-penalty"),[_line(_account(s,contract.company_id,"contract_penalty","expense"),penalty),_line(_account(s,contract.company_id,"penalty_payable","liability"),-penalty),_line(_account(s,g.id,"penalty_receivable","asset"),penalty),_line(_account(s,g.id,"penalty_revenue","revenue"),-penalty)],"공공 계약 지체 책임과 대응 청구권",month)
					if posted.ok: contract.penalties=int(contract.penalties)+penalty
				contract.last_penalty_month=month
			_event(s,"procurement_late","납기 경과·자동 전액 지급이나 재고 보충 없음",month,{"actor_id":contract.government_id,"city_id":contract.city_id,"contract_id":contract.id,"product_id":contract.product_id,"quantity":int(contract.quantity)-int(contract.accepted_quantity)})

static func _effective_policy(s: Dictionary, g: Dictionary, key: String, month: int, fallback: int) -> int:
	var value: int=int(g.get(key,fallback))
	for policy in s.public.policies:
		if policy.government_id==g.id and policy.key==key and policy.status in ["scheduled","active"] and int(policy.effective_month)<=month: value=int(policy.value)
	return value

static func trade_rule(s: Dictionary, seller_city: String, buyer_city: String, product: String, month: int) -> Dictionary:
	var out: Dictionary={"allowed":true,"tariff_bps":0,"tariff_percent":0.0,"tariff_receiver":"","delay":0,"capacity_percent":100,"reasons":[],"grandfather_before_month":0,"hard_block":false}
	if not s.has("public"): return out
	var source: Dictionary=_find(s.cities,seller_city)
	var destination: Dictionary=_find(s.cities,buyer_city)
	if source.is_empty() or destination.is_empty():
		out.allowed=false
		out.reasons.append("거래 도시가 존재하지 않음")
		return out
	if seller_city==buyer_city: return out
	out.delay=int(_p().cross_city_lead_months)
	if source.nation_id==destination.nation_id: return out
	var receiver: Dictionary=_government_for_nation(s,destination.nation_id)
	out.tariff_bps=_effective_policy(s,receiver,"tariff_bps",month,int(_p().base_tariff_bps))
	out.tariff_receiver=receiver.get("id","")
	out.delay=int(out.delay)+int(_p().customs_delay_months)
	var pair: String=_pair(source.nation_id,destination.nation_id)
	var relation: Dictionary=_find(s.public.relations,pair)
	if relation.get("state","")=="tension":
		out.delay=int(out.delay)+int(_p().tension_extra_months)
		out.capacity_percent=75
		out.reasons.append("외교 긴장으로 통관·운송 처리능력 감소")
	if relation.get("state","")=="conflict":
		out.allowed=false
		out.capacity_percent=0
		out.hard_block=true
		out.reasons.append("무력 분쟁에 따른 해당 국가 간 항로 접근 제한")
	for treaty in s.public.treaties:
		if treaty.status not in ["active","scheduled"] or int(treaty.effective_month)>month or _pair(treaty.nation_a,treaty.nation_b)!=pair: continue
		if int(treaty.get("end_month",0))>0 and int(treaty.end_month)<month: continue
		if treaty.kind=="free_trade":
			out.tariff_bps=0
			out.reasons.append("발효된 자유무역협정")
		elif treaty.kind=="transit": out.delay=maxi(int(_p().cross_city_lead_months),int(out.delay)-int(_p().customs_delay_months))
		elif treaty.kind=="ceasefire":
			out.allowed=true
			out.capacity_percent=100
		elif treaty.kind=="embargo":
			out.allowed=false
			out.hard_block=true
			out.reasons.append("쌍방의 시장 접근 제한 협정")
	for policy in s.public.policies:
		if policy.status not in ["active","scheduled"] or int(policy.effective_month)>month or int(policy.value)==0: continue
		if policy.key not in ["import_ban","export_ban"]: continue
		var government: Dictionary=_find(s.public.governments,policy.government_id)
		if government.is_empty() or government.kind!="nation": continue
		if str(policy.get("product_id",""))!="" and policy.product_id!=product: continue
		var prohibited: bool=(policy.key=="import_ban" and government.nation_id==destination.nation_id and policy.other_nation_id==source.nation_id) or (policy.key=="export_ban" and government.nation_id==source.nation_id and policy.other_nation_id==destination.nation_id)
		if prohibited:
			out.allowed=false
			out.grandfather_before_month=maxi(int(out.grandfather_before_month),int(policy.effective_month))
			out.reasons.append("관할·원산지·품목에 적용된 공개 무역 제한")
	out.tariff_percent=float(out.tariff_bps)/100.0
	return out

static func _contract_trade_rule(s: Dictionary, contract: Dictionary, source: String, destination: String, month: int) -> Dictionary:
	var rule: Dictionary=trade_rule(s,source,destination,str(contract.product_id),month)
	if not rule.allowed and not rule.get("hard_block",false) and int(rule.get("grandfather_before_month",0))>0 and int(contract.opened_month)<int(rule.grandfather_before_month):
		rule.allowed=true
		rule.reasons.append("시행 전 확정 계약의 명시된 경과 규정")
	return rule

static func _army_stock(s: Dictionary, army: Dictionary, product: String) -> int:
	var total: int=0
	for ref in s.public.military_lots:
		if ref.army_id!=army.id: continue
		var lot: Dictionary=_find(s.inventory,ref.lot_id)
		if not lot.is_empty() and lot.product_id==product: total+=int(lot.quantity)
	return total

static func _outstanding_procurement(s: Dictionary, government: String, product: String) -> int:
	var total: int=0
	for contract in s.public.procurements:
		if contract.government_id==government and contract.product_id==product and contract.status not in ["fulfilled","cancelled","defaulted","expired"]: total+=maxi(0,int(contract.quantity)-int(contract.accepted_quantity))
	return total

static func _ai_command(s: Dictionary, actor: String, institution: String, type: String, payload: Dictionary, suffix: String) -> Dictionary:
	return {"id":"public-ai:"+str(s.planning_month)+":"+actor+":"+type+":"+suffix,"actor_id":actor,"acting_company_id":institution,"expected_version":int(s.version),"type":type,"payload":payload}

static func ai_commands(s: Dictionary) -> Array:
	var commands: Array=[]
	if not s.has("public"): return commands
	var month: int=int(s.planning_month)
	for g in s.public.governments:
		var office: Dictionary=_find(s.public.offices,g.office_id)
		var actor: String=str(office.get("person_id",""))
		if actor.is_empty() or actor==s.player_id: continue
		for contract in s.public.procurements:
			if contract.government_id!=g.id: continue
			if contract.status=="open":
				var bid: Dictionary=_best_bid(s,contract)
				if not bid.is_empty(): commands.append(_ai_command(s,actor,g.id,"public_award",{"government_id":g.id,"contract_id":contract.id,"bid_id":bid.id,"advance_bps":0},contract.id))
			if int(contract.payable)>0: commands.append(_ai_command(s,actor,g.id,"public_pay",{"government_id":g.id,"contract_id":contract.id},contract.id))
		for delivery in s.public.deliveries:
			if delivery.government_id==g.id and delivery.status=="awaiting_inspection": commands.append(_ai_command(s,actor,g.id,"public_inspect",{"government_id":g.id,"delivery_id":delivery.id},delivery.id))
		for treaty in s.public.treaties:
			if treaty.nation_b==g.nation_id and g.kind=="nation" and treaty.status=="proposed" and treaty.kind in ["free_trade","transit","ceasefire"]: commands.append(_ai_command(s,actor,g.id,"public_accept_treaty",{"government_id":g.id,"treaty_id":treaty.id},treaty.id))
		if g.kind=="nation":
			var army: Dictionary={}
			for a in s.public.armies:
				if a.government_id==g.id: army=a
			if not army.is_empty():
				var targets: Dictionary={"food":army.members.size()*int(_p().army_food_per_member)*int(_p().army_food_stock_months),"tools":int(_p().army_tools_target)}
				if not _find(s.products,"defense_equipment").is_empty(): targets["defense_equipment"]=army.members.size()
				for product in _keys(targets):
					var needed: int=maxi(0,int(targets[product])-_army_stock(s,army,product)-_outstanding_procurement(s,g.id,product))
					if needed<=0: continue
					var price: int=maxi(1,int(int(_find(s.products,product).get("price",1))*int(_p().procurement_price_bps)/10000))
					commands.append(_ai_command(s,actor,g.id,"public_announce_procurement",{"government_id":g.id,"product_id":product,"quantity":needed,"max_unit_price":price,"deadline_month":month+int(_p().procurement_lead_months),"military":true},product))
		if int(office.term_end)<=month+int(_p().campaign_months) and not s.public.candidates.any(func(x): return x.government_id==g.id and x.person_id==actor and x.status=="campaigning"):
			commands.append(_ai_command(s,actor,"","public_run_office",{"government_id":g.id,"platform":"services"},g.id))
	for company in s.companies:
		var actor: String=str(company.director_id)
		if actor==s.player_id or company.get("status","active")!="active": continue
		for contract in s.public.procurements:
			if contract.status=="open" and _can_produce(s,company.id,contract.product_id):
				var price: int=_offer_price(s,company,contract.product_id)
				commands.append(_ai_command(s,actor,company.id,"public_bid",{"contract_id":contract.id,"unit_price":mini(price,int(contract.max_unit_price)),"delivery_month":maxi(month+1,int(contract.deadline_month))},contract.id))
			if contract.get("company_id","")==company.id and contract.status in ["awarded","partially_delivered","late"]:
				var amount: int=mini(maxi(0,int(contract.quantity)-int(contract.delivered_quantity)),_available(s,company.id,company.city_id,contract.product_id))
				if amount>0: commands.append(_ai_command(s,actor,company.id,"public_deliver",{"contract_id":contract.id,"quantity":amount},contract.id))
	for person in s.people:
		if person.id==s.player_id: continue
		for proposal in s.public.policy_proposals:
			if proposal.status!="voting" or proposal.votes.has(person.id): continue
			var g: Dictionary=_find(s.public.governments,proposal.government_id)
			if person.id in g.council or has_authority(s,person.id,g.id,"policy"):
				var support: bool=not (proposal.key=="tax_bps" and int(proposal.value)>int(_p().national_tax_bps)*2)
				commands.append(_ai_command(s,person.id,"","public_vote_policy",{"proposal_id":proposal.id,"support":support},proposal.id))
		for g in s.public.governments:
			if not _eligible(s,person.id,g): continue
			var candidates: Array=s.public.candidates.filter(func(x): return x.government_id==g.id and x.status=="campaigning")
			if not candidates.is_empty():
				candidates.sort_custom(func(a,b): return _candidate_score(s,person,a,g)>_candidate_score(s,person,b,g))
				commands.append(_ai_command(s,person.id,"","public_vote",{"candidate_id":candidates[0].id},g.id+":"+str(candidates[0].election_month)))
			if int(abs((str(s.seed)+str(person.id)+str(g.id)).hash()))%int(_p().office_term_months)==month%int(_p().office_term_months) and _cash(s,person.id)>=int(_p().campaign_fee):
				var platform: String=_config().platforms[int(abs(str(person.id).hash()))%_config().platforms.size()]
				commands.append(_ai_command(s,person.id,"","public_run_office",{"government_id":g.id,"platform":platform},g.id))
	var valid: Array=[]
	for command in commands:
		if validate_command(s,command).is_empty(): valid.append(command)
	valid.sort_custom(func(a,b): return str(a.id)<str(b.id))
	return valid

static func summary(s: Dictionary, owner: String = "") -> Dictionary:
	var out: Dictionary={"governments":[],"offices":[],"parties":[],"candidates":[],"policies":[],"treaties":[],"relations":[],"procurements":[],"bids":[],"deliveries":[],"armies":[],"conflicts":[],"tax_assessments":[],"cash":0,"reserved":0,"services":0,"tax_revenue":0}
	if not s.has("public"): return out
	for g in s.public.governments:
		var copy: Dictionary=g.duplicate(true)
		copy.cash=_cash(s,g.id)
		copy.can_control=owner==g.id or has_authority(s,owner,g.id,"budget")
		out.governments.append(copy)
		out.cash+=int(copy.cash)
		out.reserved+=int(g.reserved)
		out.services+=int(g.services)
		out.tax_revenue+=int(g.last_tax_revenue)
	for key in ["offices","parties","candidates","policies","treaties","relations","procurements","armies","conflicts"]:
		out[key]=s.public[key].duplicate(true)
	for bid in s.public.bids:
		var contract: Dictionary=_find(s.public.procurements,bid.contract_id)
		var co: Dictionary=_find(s.companies,bid.company_id)
		if bid.status!="submitted" or co.get("director_id")==owner or owner==co.get("id") or has_authority(s,owner,contract.get("government_id",""),"procurement"): out.bids.append(bid.duplicate(true))
	for delivery in s.public.deliveries:
		var co: Dictionary=_find(s.companies,delivery.company_id)
		if owner==co.get("id") or owner==co.get("director_id") or has_authority(s,owner,delivery.government_id,"procurement"): out.deliveries.append(delivery.duplicate(true))
	for tax in s.public.tax_assessments:
		if owner==tax.payer_id or has_authority(s,owner,tax.government_id,"budget"): out.tax_assessments.append(tax.duplicate(true))
	return out

static func validate(s: Dictionary) -> Array:
	var errors: Array=[]
	if not _data_error.is_empty(): errors.append(_data_error)
	if not s.has("public"): return ["PUBLIC_NOT_INITIALIZED"]
	for key in ["governments","offices","parties","candidates","ballots","policy_proposals","policies","treaties","relations","tax_assessments","procurements","bids","deliveries","military_lots","armies","conflicts","license_applications","applied_commands","service_changes","default_claims"]:
		if not s.public.get(key) is Array: errors.append("PUBLIC_COLLECTION:"+key)
	if not errors.is_empty(): return errors
	var ids: Dictionary={}
	for key in ["governments","offices","parties","candidates","ballots","policy_proposals","policies","treaties","relations","tax_assessments","procurements","bids","deliveries","military_lots","armies","conflicts","license_applications","service_changes","default_claims"]:
		for item in s.public[key]:
			if not item is Dictionary or str(item.get("id","")).is_empty():
				errors.append("PUBLIC_RECORD_SCHEMA:"+key)
				continue
			if ids.has(str(item.id)): errors.append("PUBLIC_DUPLICATE_ID:"+str(item.id))
			ids[str(item.id)]=true
	for g in s.public.governments:
		if g.get("kind") not in ["city","nation"] or _find(s.cities,str(g.get("city_id",""))).is_empty() or _find(s.nations,str(g.get("nation_id",""))).is_empty(): errors.append("PUBLIC_JURISDICTION:"+str(g.id))
		if not s.accounts.has(str(g.id)+":cash") or s.accounts[str(g.id)+":cash"].get("owner")!=g.id: errors.append("PUBLIC_CASH_OWNER:"+str(g.id))
		var expected: int=0
		for contract in s.public.procurements:
			if contract.government_id==g.id: expected+=int(contract.reserved_value)
		if not _integer(g.get("reserved")) or int(g.reserved)<0 or int(g.reserved)!=expected or int(g.reserved)>_cash(s,g.id): errors.append("PUBLIC_BUDGET_RESERVATION:"+str(g.id))
		for key in ["services","procurement","military","transfers"]:
			if not _integer(g.get("budget",{}).get(key)) or int(g.budget.get(key,-1))<0: errors.append("PUBLIC_BUDGET_SCHEMA:"+key)
	for office in s.public.offices:
		if _find(s.public.governments,str(office.get("government_id",""))).is_empty() or _find(s.people,str(office.get("person_id",""))).is_empty(): errors.append("PUBLIC_OFFICE_REFERENCE:"+str(office.id))
	for contract in s.public.procurements:
		if _find(s.public.governments,str(contract.get("government_id",""))).is_empty() or _find(s.products,str(contract.get("product_id",""))).is_empty(): errors.append("PUBLIC_CONTRACT_REFERENCE:"+str(contract.id))
		for key in ["quantity","delivered_quantity","accepted_quantity","cash_paid","advance_paid","advance_remaining","payable","reserved_value"]:
			if not _integer(contract.get(key)) or int(contract.get(key,-1))<0: errors.append("PUBLIC_CONTRACT_INTEGER:"+key)
		if int(contract.accepted_quantity)>int(contract.delivered_quantity) or int(contract.delivered_quantity)>int(contract.quantity): errors.append("PUBLIC_DELIVERY_OVERCOMMITTED:"+str(contract.id))
		if contract.status=="fulfilled" and (int(contract.accepted_quantity)!=int(contract.quantity) or int(contract.payable)!=0 or int(contract.reserved_value)!=0): errors.append("PUBLIC_FALSE_FULFILLMENT:"+str(contract.id))
	for delivery in s.public.deliveries:
		var contract: Dictionary=_find(s.public.procurements,str(delivery.get("contract_id","")))
		var lot: Dictionary=_find(s.inventory,str(delivery.get("lot_id","")))
		if contract.is_empty() or lot.is_empty():
			errors.append("PUBLIC_DELIVERY_REFERENCE:"+str(delivery.id))
			continue
		if delivery.status in ["transit","awaiting_inspection"] and (lot.owner_id!=delivery.company_id or int(lot.quantity)!=int(delivery.quantity) or int(lot.reserved)!=int(delivery.quantity)): errors.append("PUBLIC_DELIVERY_RESERVATION:"+str(delivery.id))
	for tax in s.public.tax_assessments:
		if int(tax.get("assessed",-1))!=int(tax.get("paid",0))+int(tax.get("outstanding",0)) or int(tax.get("outstanding",-1))<0: errors.append("PUBLIC_TAX_RECONCILIATION:"+str(tax.id))
	for ref in s.public.military_lots:
		var lot: Dictionary=_find(s.inventory,str(ref.get("lot_id","")))
		if _find(s.public.armies,str(ref.get("army_id",""))).is_empty() or lot.is_empty() or lot.get("location","")!="military": errors.append("PUBLIC_MILITARY_INVENTORY_REFERENCE:"+str(ref.id))
	return errors

static func ui_actions() -> Array:
	return [
		{"type":"public_create_party","label":"정당 설립","scope":"personal","fields":[{"key":"name","label":"정당 이름","kind":"text","default":"새로운 시민연합"},{"key":"platform","label":"정책 방향","kind":"choice","options":["services","growth","security"]}]},
		{"type":"public_campaign","label":"실제 홍보물로 선거 활동","scope":"personal","fields":[{"key":"party_id","label":"운영 정당","kind":"choice","source":"parties"},{"key":"candidate_id","label":"소속 후보","kind":"choice","source":"candidates"},{"key":"quantity","label":"홍보물 수량","kind":"int","min":1,"max":100,"default":2}]},
		{"type":"public_resign_office","label":"공직 사임","scope":"personal","fields":[{"key":"government_id","label":"관할 정부","kind":"choice","source":"governments"}]},
		{"type":"public_join_service","label":"군 근무 지원","scope":"personal","fields":[{"key":"army_id","label":"국가 군","kind":"choice","source":"armies"}]},
		{"type":"public_leave_service","label":"군 근무 사직 통지","scope":"personal","fields":[{"key":"army_id","label":"국가 군","kind":"choice","source":"armies"}]},
		{"type":"public_join_party","label":"정당 가입","scope":"personal","fields":[{"key":"party_id","label":"정당","kind":"choice","source":"parties"}]},
		{"type":"public_donate","label":"공개 정치 후원","scope":"personal","fields":[{"key":"party_id","label":"정당","kind":"choice","source":"parties"},{"key":"amount","label":"후원액(최소단위)","kind":"int","min":1,"max":10000,"default":5}]},
		{"type":"public_run_office","label":"공직 출마","scope":"personal","fields":[{"key":"government_id","label":"관할 정부","kind":"choice","source":"governments"},{"key":"platform","label":"정책 방향","kind":"choice","options":["services","growth","security"],"default":"services"}]},
		{"type":"public_vote","label":"후보 투표","scope":"personal","fields":[{"key":"candidate_id","label":"후보","kind":"choice","source":"candidates"}]},
		{"type":"public_propose_policy","label":"정책 제안","scope":"personal","fields":[{"key":"government_id","label":"관할 정부","kind":"choice","source":"governments"},{"key":"key","label":"정책 종류","kind":"choice","options":["tax_bps","tariff_bps","services_budget","procurement_budget","import_ban","export_ban","mobilization","interest_bps"]},{"key":"value","label":"값(세율은 10000분율)","kind":"int","min":0,"max":5000,"default":500},{"key":"other_nation_id","label":"무역 제한 대상국","kind":"choice","source":"nations"}]},
		{"type":"public_vote_policy","label":"정책 표결","scope":"personal","fields":[{"key":"proposal_id","label":"심의 안건","kind":"choice","source":"policy_proposals"},{"key":"support","label":"찬성","kind":"bool","default":true}]},
		{"type":"public_set_budget","label":"공공 예산 편성","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"category","label":"예산 부문","kind":"choice","options":["services","procurement","military","transfers"]},{"key":"amount","label":"월 예산 한도(최소단위)","kind":"int","min":0,"max":50000,"default":400}]},
		{"type":"public_transfer","label":"공공 이전지출","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"recipient_id","label":"수령 인물","kind":"choice","source":"people"},{"key":"amount","label":"지급액(최소단위)","kind":"int","min":1,"max":10000,"default":5}]},
		{"type":"public_announce_procurement","label":"조달 공고","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"product_id","label":"납품 품목","kind":"choice","source":"products"},{"key":"quantity","label":"수량(품목단위)","kind":"int","min":1,"max":10000,"default":4},{"key":"max_unit_price","label":"상한 단가(최소단위)","kind":"int","min":1,"max":10000,"default":28},{"key":"deadline_month","label":"납품 기한(계획월 번호)","kind":"int","min":2,"max":1200,"default":4},{"key":"military","label":"국방 창고 납품","kind":"bool","default":true}]},
		{"type":"public_bid","label":"공공·군수 입찰","scope":"company","fields":[{"key":"contract_id","label":"공고","kind":"choice","source":"procurements"},{"key":"unit_price","label":"제안 단가(최소단위)","kind":"int","min":1,"max":10000,"default":14},{"key":"delivery_month","label":"납품 약속월","kind":"int","min":2,"max":1200,"default":4}]},
		{"type":"public_award","label":"공개 기준으로 계약 선정","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"contract_id","label":"공고","kind":"choice","source":"procurements"},{"key":"bid_id","label":"입찰 제안","kind":"choice","source":"bids"},{"key":"advance_bps","label":"선급금 비율(10000분율)","kind":"int","min":0,"max":2000,"default":0}]},
		{"type":"public_deliver","label":"실제 재고 납품","scope":"company","fields":[{"key":"contract_id","label":"계약","kind":"choice","source":"procurements"},{"key":"quantity","label":"분할 납품 수량","kind":"int","min":1,"max":10000,"default":2}]},
		{"type":"public_inspect","label":"납품 검수","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"delivery_id","label":"도착한 납품","kind":"choice","source":"deliveries"}]},
		{"type":"public_pay","label":"검수 대금 지급","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"contract_id","label":"검수 채무 계약","kind":"choice","source":"procurements"}]},
		{"type":"public_cancel_contract","label":"조달 계약 해지","scope":"public","fields":[{"key":"government_id","label":"정부","kind":"choice","source":"governments"},{"key":"contract_id","label":"계약","kind":"choice","source":"procurements"}]},
		{"type":"public_apply_license","label":"국방 납품 허가 신청","scope":"company","fields":[]},
		{"type":"public_propose_treaty","label":"국가 간 협정 제안","scope":"public","fields":[{"key":"government_id","label":"국가 정부","kind":"choice","source":"governments"},{"key":"other_nation_id","label":"상대국","kind":"choice","source":"nations"},{"key":"kind","label":"협정","kind":"choice","options":["free_trade","transit","ceasefire","embargo"]}]},
		{"type":"public_accept_treaty","label":"협정 비준","scope":"public","fields":[{"key":"government_id","label":"국가 정부","kind":"choice","source":"governments"},{"key":"treaty_id","label":"제안 협정","kind":"choice","source":"treaties"}]},
		{"type":"public_set_relation","label":"외교·안보 상태 결정","scope":"public","fields":[{"key":"government_id","label":"국가 정부","kind":"choice","source":"governments"},{"key":"other_nation_id","label":"상대국","kind":"choice","source":"nations"},{"key":"state","label":"외교 상태","kind":"choice","options":["neutral","cooperate","tension","conflict"]}]},
		{"type":"public_set_readiness","label":"군 대비 목표 설정","scope":"public","fields":[{"key":"army_id","label":"국가 군","kind":"choice","source":"armies"},{"key":"readiness","label":"대비 목표(0~3)","kind":"int","min":0,"max":3,"default":1}]}
	]

static func _default_contract(s: Dictionary, contract: Dictionary, month: int) -> void:
	var g: Dictionary=_find(s.public.governments,contract.government_id)
	var released: int=maxi(0,int(contract.reserved_value)-int(contract.payable))
	g.reserved=maxi(0,int(g.reserved)-released)
	contract.reserved_value=int(contract.payable)
	contract.status="defaulted"
	for delivery in s.public.deliveries:
		if delivery.contract_id==contract.id and delivery.status in ["transit","awaiting_inspection"]:
			var lot: Dictionary=_find(s.inventory,delivery.lot_id)
			if not lot.is_empty():
				lot.reserved=0
				lot.location="warehouse"
			delivery.status="estate_owned"
	s.public.default_claims.append({"id":_id(s,"public-default-claim"),"government_id":g.id,"company_id":contract.company_id,"contract_id":contract.id,"advance_claim":int(contract.advance_remaining),"penalty_claim":maxi(0,int(contract.penalties)-int(contract.get("penalties_paid",0))),"month":month,"status":"unpaid"})
	_event(s,"procurement_default","공급자 지급불능·미이행 예산만 해제하고 선급금·세금·검수 채무는 보존",month,{"actor_id":g.id,"city_id":g.city_id,"company_id":contract.company_id,"contract_id":contract.id,"amount":released,"advance_claim":int(contract.advance_remaining)})

static func settle_claims(s: Dictionary, owner: String, available: int, month: int) -> Dictionary:
	var left: int=mini(maxi(0,available),_cash(s,owner))
	var paid: int=0
	for tax in s.get("public",{}).get("tax_assessments",[]):
		if tax.payer_id!=owner or int(tax.outstanding)<=0 or left<=0: continue
		var g: Dictionary=_find(s.public.governments,tax.government_id)
		var amount: int=mini(left,int(tax.outstanding))
		var result: Dictionary=Ledger.post(s,_id(s,"estate-tax"),[_line(_account(s,owner,"tax_payable","liability"),amount),_line(owner+":cash",-amount),_line(g.id+":cash",amount),_line(_account(s,g.id,"tax_receivable","asset"),-amount)],"청산 재원의 실제 미납 세금 변제",month)
		if result.ok:
			tax.outstanding=int(tax.outstanding)-amount
			tax.paid=int(tax.paid)+amount
			tax.status="paid" if int(tax.outstanding)==0 else "arrears"
			left-=amount
			paid+=amount
	for contract in s.get("public",{}).get("procurements",[]):
		if contract.get("company_id","")!=owner or contract.status not in ["cancelled","defaulted"] or left<=0: continue
		var g: Dictionary=_find(s.public.governments,contract.government_id)
		var advance: int=mini(left,int(contract.advance_remaining))
		if advance>0:
			var result: Dictionary=Ledger.post(s,_id(s,"estate-advance"),[_line(_account(s,owner,"contract_advances","liability"),advance),_line(owner+":cash",-advance),_line(g.id+":cash",advance),_line(_account(s,g.id,"procurement_advances","asset"),-advance)],"청산 재원의 실제 미이행 선급금 변제",month)
			if result.ok:
				contract.advance_remaining=int(contract.advance_remaining)-advance
				left-=advance
				paid+=advance
		var penalty: int=mini(left,maxi(0,int(contract.penalties)-int(contract.get("penalties_paid",0))))
		if penalty>0:
			var result: Dictionary=Ledger.post(s,_id(s,"estate-penalty"),[_line(_account(s,owner,"penalty_payable","liability"),penalty),_line(owner+":cash",-penalty),_line(g.id+":cash",penalty),_line(_account(s,g.id,"penalty_receivable","asset"),-penalty)],"청산 재원의 실제 계약 지체채무 변제",month)
			if result.ok:
				contract.penalties_paid=int(contract.get("penalties_paid",0))+penalty
				left-=penalty
				paid+=penalty
	for claim in s.get("public",{}).get("default_claims",[]):
		if claim.company_id!=owner: continue
		var contract: Dictionary=_find(s.public.procurements,claim.contract_id)
		claim.advance_claim=int(contract.advance_remaining)
		claim.penalty_claim=maxi(0,int(contract.penalties)-int(contract.get("penalties_paid",0)))
		claim.status="paid" if int(claim.advance_claim)+int(claim.penalty_claim)==0 else "unpaid"
	if paid>0: _event(s,"estate_public_settlement","청산 가용 현금에서 공공 채권을 실제 변제",month,{"actor_id":owner,"city_id":owner_city(s,owner),"amount":paid})
	return {"ok":true,"paid":paid,"remaining_available":left}

static func reserved_cash(s: Dictionary, owner: String) -> int:
	var government: Dictionary=_find(s.get("public",{}).get("governments",[]),owner)
	return int(government.get("reserved",0))

static func planning_reservations(s: Dictionary, c: Dictionary) -> Array:
	if not s.has("public") or not c.get("payload") is Dictionary: return []
	var p: Dictionary=c.payload
	var actor: String=str(c.get("actor_id",""))
	var type: String=str(c.get("type",""))
	if type not in command_types(): return []
	var result: Array=[{"owner_id":actor,"resource_type":"labor","resource_id":"hours","quantity":int(_p().political_action_hours)}]
	var owner: String=actor
	var amount: int=0
	var co: Dictionary=_company(s,c)
	var government: String=str(p.get("government_id",c.get("acting_company_id","")))
	match type:
		"public_create_party": amount=int(_p().party_found_fee)
		"public_join_party": amount=int(_p().party_join_fee)
		"public_run_office": amount=int(_p().campaign_fee)
		"public_propose_policy": amount=int(_p().policy_fee)
		"public_donate":
			owner=str(co.get("id",actor))
			amount=int(p.get("amount",0))
		"public_bid":
			owner=str(co.get("id",actor))
			amount=int(_p().bid_fee)
		"public_apply_license":
			owner=str(co.get("id",actor))
			amount=int(_p().permit_fee)
		"public_campaign":
			var party: Dictionary=_find(s.public.parties,str(p.get("party_id","")))
			owner=str(party.get("id",actor))
			var min_price: int=0
			for company in s.companies:
				if company.city_id==party.get("city_id","") and _can_produce(s,company.id,"paper"):
					var price: int=_offer_price(s,company,"paper")
					if min_price==0 or price<min_price: min_price=price
			amount=min_price*int(p.get("quantity",0))
		"public_transfer":
			owner=government
			amount=int(p.get("amount",0))
		"public_announce_procurement":
			owner=government
			amount=int(p.get("quantity",0))*int(p.get("max_unit_price",0))
		"public_award":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			var bid: Dictionary=_find(s.public.bids,str(p.get("bid_id","")))
			var advance: int=int(int(contract.get("quantity",0))*int(bid.get("unit_price",0))*int(p.get("advance_bps",0))/10000)
			if advance>0: result.append({"owner_id":contract.get("government_id",government),"resource_type":"cash","quantity":advance,"currency":"USD","from_reservation":contract.get("id",""),"existing_reservation":true})
		"public_pay":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			if int(contract.get("payable",0))>0: result.append({"owner_id":contract.get("government_id",government),"resource_type":"cash","quantity":int(contract.payable),"currency":"USD","from_reservation":contract.get("id",""),"existing_reservation":true})
		"public_deliver":
			var contract: Dictionary=_find(s.public.procurements,str(p.get("contract_id","")))
			owner=str(co.get("id",actor))
			var destination: Dictionary=_find(s.public.governments,str(contract.get("government_id","")))
			if not contract.is_empty() and not co.is_empty() and not destination.is_empty():
				var rule: Dictionary=_contract_trade_rule(s,contract,co.city_id,destination.city_id,int(s.planning_month))
				amount=int(int(p.get("quantity",0))*int(contract.get("unit_price",0))*int(rule.get("tariff_bps",0))/10000)
				if s.has("industry"):
					var quote: Dictionary=load("res://core/industry.gd").route_quote(s,co.city_id,destination.city_id,contract.product_id,int(p.get("quantity",0)),int(s.planning_month))
					amount+=int(quote.get("fee",0))
			result.append({"owner_id":co.get("id",""),"resource_type":"inventory","resource_id":contract.get("product_id",""),"city_id":co.get("city_id",""),"quantity":int(p.get("quantity",0))})
	if amount>0: result.append({"owner_id":owner,"resource_type":"cash","quantity":amount,"currency":"USD"})
	return result

static func _civil_stock(s: Dictionary, owner: String, city: String, product: String) -> int:
	var quantity: int=0
	for lot in s.inventory:
		if lot.owner_id==owner and lot.city_id==city and lot.product_id==product and lot.get("location","") not in ["shipment","public_transit","inspection","military"]: quantity+=maxi(0,int(lot.quantity)-int(lot.get("reserved",0)))
	return quantity

static func _utility_service(s: Dictionary, g: Dictionary, product: String, quantity: int, month: int) -> int:
	var industry: Variant=load("res://core/industry.gd") if s.has("industry") else null
	if industry==null or not industry.is_service(s,product): return -1
	var budget: int=mini(_spendable(s,g),_budget_left(g,"services",month))
	var cheapest: int=0
	for capacity in s.industry.get("service_capacity",[]):
		if capacity.city_id==g.city_id and capacity.product_id==product and int(capacity.month)==month and int(capacity.capacity)>int(capacity.used):
			var price: int=int(capacity.unit_price)
			if cheapest==0 or price<cheapest: cheapest=price
	if cheapest<=0 or budget<cheapest: return 0
	var wanted: int=mini(quantity,int(budget/cheapest))
	if wanted<=0: return 0
	var result: Dictionary=industry.purchase_service(s,g.id,g.city_id,product,wanted,month,int(budget/wanted),false)
	var acquired: int=int(result.get("quantity",0))
	_budget_use(g,"services",int(result.get("cost",0)),month)
	if acquired>0:
		industry.consume_service(s,g.id,g.city_id,product,acquired,month)
		_nonmarket_output(s,g.id,g.city_id,int(result.get("cost",0)),month,"공공 서비스에 실제 제공된 용수·전력 투입",true,product,acquired)
	return acquired

static func _stock_age(s: Dictionary, owner: String, city: String, product: String, quantity: int) -> Dictionary:
	var age: Dictionary={"born_month":int(s.planning_month),"expires_month":0}
	var lots: Array=s.inventory.filter(func(x): return x.owner_id==owner and x.city_id==city and x.product_id==product and int(x.quantity)>int(x.get("reserved",0)))
	lots.sort_custom(func(a,b): return str(a.id)<str(b.id) if int(a.get("born_month",0))==int(b.get("born_month",0)) else int(a.get("born_month",0))<int(b.get("born_month",0)))
	var left: int=quantity
	for lot in lots:
		var used: int=mini(left,int(lot.quantity)-int(lot.get("reserved",0)))
		if used<=0: continue
		age.born_month=mini(int(age.born_month),int(lot.get("born_month",0)))
		if int(lot.get("expires_month",0))>0 and (int(age.expires_month)==0 or int(lot.expires_month)<int(age.expires_month)): age.expires_month=int(lot.expires_month)
		left-=used
		if left<=0: break
	return age

static func _apply_age(lot: Dictionary, age: Dictionary) -> void:
	lot.born_month=int(age.born_month)
	if int(age.get("expires_month",0))>0: lot.expires_month=int(age.expires_month)

static func _spoil_locked_delivery(s: Dictionary, delivery: Dictionary, lot: Dictionary, month: int) -> void:
	var quantity: int=int(lot.quantity)
	if quantity<=0: return
	var finance: Variant=_finance(s)
	if finance!=null and finance.has_method("inventory_flow"): finance.inventory_flow(s,lot.owner_id,lot.product_id,-quantity,"납품 중 유통기간 경과",month)
	lot.quantity=0
	lot.reserved=0
	_event(s,"spoilage","검수 전 실제 납품 화물의 유통기간 경과·공급자 손실",month,{"actor_id":lot.owner_id,"city_id":lot.city_id,"product_id":lot.product_id,"quantity":quantity,"inventory_delta":-quantity,"lot_id":lot.id,"delivery_id":delivery.id})
	var contract: Dictionary=_find(s.public.procurements,delivery.contract_id)
	contract.delivered_quantity=maxi(0,int(contract.delivered_quantity)-int(delivery.quantity))
	delivery.quantity=0
	delivery.status="spoiled"

static func _nonmarket_output(s: Dictionary, government: String, city: String, cost: int, month: int, reason: String, intermediate: bool, product: String, quantity: int) -> void:
	if cost<=0: return
	var data: Dictionary={"actor_id":government,"government_id":government,"city_id":city,"sector":"public_nonmarket","valuation":"actual_cost","currency":"USD","amount":cost,"product_id":product,"quantity":quantity,"nonmarket_input_component":intermediate}
	if intermediate: _event(s,"gdp_input",reason,month,data)
	# Nonmarket output is valued at its actually incurred production costs.
	# Its intermediate part also appears above, leaving public labor as VA.
	_event(s,"gdp_output",reason,month,data)

static func _move_lot_city(s: Dictionary, lot: Dictionary, destination: String) -> void:
	var previous: String=str(lot.city_id)
	if previous==destination: return
	if s.has("_runtime") and s._runtime.has("stock_groups"):
		var groups: Dictionary=s._runtime.stock_groups
		var prefix: String=str(lot.owner_id)+"|"+str(lot.product_id)+"|"
		if groups.has(prefix+previous): groups[prefix+previous].erase(lot)
		if not groups.has(prefix+destination): groups[prefix+destination]=[]
		groups[prefix+destination].append(lot)
	lot.city_id=destination
