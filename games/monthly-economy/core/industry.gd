extends RefCounted
## Versioned industry, biological work, non-storable services and digital rights.
## Economic state is JSON-only. Every payment and stock change has a real owner.
const Ledger = preload("res://core/ledger.gd")
const Domain = preload("res://core/domain.gd")
const DATA_PATH: String = "res://data/industry.json"
static var _cache: Dictionary = {}

static func _normalize(value: Variant) -> Variant:
	if value is Dictionary:
		var result: Dictionary = {}
		for key in value: result[str(key)] = _normalize(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item in value: result.append(_normalize(item))
		return result
	if typeof(value) == TYPE_FLOAT and is_finite(value) and value == floor(value): return int(value)
	return value

static func _data() -> Dictionary:
	if not _cache.is_empty(): return _cache
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(DATA_PATH))
	if not parsed is Dictionary:
		push_error("INDUSTRY_DATA_MISSING_OR_INVALID")
		return {}
	var candidate: Dictionary = _normalize(parsed)
	var errors: Array = validate_data(candidate)
	if not errors.is_empty():
		push_error("INDUSTRY_DATA_INVALID: " + str(errors))
		return {}
	_cache = candidate
	return _cache

static func validate_data(data: Dictionary) -> Array:
	var errors: Array = []
	for key in ["status", "version", "products", "recipes", "buildings", "resources", "parameters", "tech_topics", "household_needs", "digital_categories"]:
		if not data.has(key): errors.append("INDUSTRY_FIELD_MISSING:" + key)
	if not errors.is_empty(): return errors
	if data.status != "experimental" or data.version != 1: errors.append("INDUSTRY_VERSION_UNSUPPORTED")
	for key in ["products", "recipes", "buildings", "resources", "tech_topics", "household_needs", "digital_categories"]:
		if not data[key] is Array or data[key].is_empty(): errors.append("INDUSTRY_ARRAY_INVALID:" + key)
	if not data.parameters is Dictionary: errors.append("INDUSTRY_PARAMETERS_INVALID")
	if not errors.is_empty(): return errors
	var ids: Dictionary = {"grain": true, "flour": true, "food": true, "wood": true, "stone": true, "tools": true, "energy": true, "water": true}
	for product in data.products:
		if not product is Dictionary:
			errors.append("INDUSTRY_PRODUCT_INVALID")
			continue
		for key in ["id", "name", "unit", "industry", "kind"]:
			if not product.get(key) is String or str(product.get(key, "")).is_empty(): errors.append("INDUSTRY_PRODUCT_TEXT:" + key)
		var id: String = str(product.get("id", ""))
		if ids.has(id): errors.append("INDUSTRY_DUPLICATE_PRODUCT:" + id)
		ids[id] = true
		if product.get("kind") not in ["material", "service", "digital"]: errors.append("INDUSTRY_PRODUCT_KIND")
		for key in ["price", "shelf_life_months", "cargo_size", "durable_months"]:
			if not _whole(product.get(key)) or int(product.get(key, -1)) < 0: errors.append("INDUSTRY_PRODUCT_NUMBER:" + key)
		if int(product.get("price", 0)) <= 0: errors.append("INDUSTRY_PRODUCT_PRICE")
	var recipe_ids: Dictionary = {}
	for recipe in data.recipes:
		if not recipe is Dictionary:
			errors.append("INDUSTRY_RECIPE_INVALID")
			continue
		var id: String = str(recipe.get("id", ""))
		if id.is_empty() or recipe_ids.has(id): errors.append("INDUSTRY_RECIPE_ID")
		recipe_ids[id] = true
		for key in ["labor", "lead_months"]:
			if not _whole(recipe.get(key)) or int(recipe.get(key, -1)) < 0: errors.append("INDUSTRY_RECIPE_NUMBER")
		if int(recipe.get("labor", 0)) <= 0: errors.append("INDUSTRY_RECIPE_LABOR")
		for key in ["inputs", "outputs"]:
			if not recipe.get(key) is Dictionary or recipe.get(key, {}).is_empty():
				errors.append("INDUSTRY_RECIPE_VECTOR")
				continue
			for product_id in recipe[key]:
				if not ids.has(product_id) or not _positive(recipe[key][product_id]): errors.append("INDUSTRY_RECIPE_PRODUCT")
		if recipe.has("resource") and (recipe.resource not in data.resources or not _positive(recipe.get("resource_per_batch"))): errors.append("INDUSTRY_RECIPE_RESOURCE")
		for product_id in recipe.get("catalysts", {}):
			if not ids.has(product_id) or not _positive(recipe.catalysts[product_id]): errors.append("INDUSTRY_CATALYST_INVALID")
	var building_ids: Dictionary = {}
	for building in data.buildings:
		if not building is Dictionary:
			errors.append("INDUSTRY_BUILDING_INVALID")
			continue
		var id: String = str(building.get("id", ""))
		if id.is_empty() or building_ids.has(id): errors.append("INDUSTRY_BUILDING_ID")
		building_ids[id] = true
		if not recipe_ids.has(building.get("recipe_id")): errors.append("INDUSTRY_BUILDING_RECIPE")
		for key in ["capacity", "wood", "stone", "tools", "work", "min_months", "w", "h"]:
			if not _positive(building.get(key)): errors.append("INDUSTRY_BUILDING_NUMBER:" + key)
	var required: Array = ["research_work", "research_min_months", "research_monthly_work", "research_setup_fee", "research_monthly_budget", "adoption_cost", "adoption_months", "software_work", "software_min_months", "software_monthly_work", "software_setup_fee", "software_default_price", "software_support_months", "license_max_seats", "server_seats", "network_seats_per_unit", "ads_per_seat", "ad_unit_price", "route_capacity", "domestic_lead", "foreign_lead", "domestic_fee", "foreign_fee", "freight_fuel_per_units", "freight_labor_per_units", "onsite_storage", "warehouse_capacity_increment", "warehouse_work", "warehouse_wood", "warehouse_steel", "warehouse_cost", "warehouse_cold_extra_energy", "recycle_work", "recycle_units_in", "recycle_units_out", "resource_base", "resource_city_step", "fish_capacity", "fish_regrowth", "ai_research_reserve", "ai_software_reserve", "ai_action_period", "software_maintenance_energy", "software_maintenance_telecom", "agriculture_max_projects", "spoilage_waste_percent", "max_budget", "max_quantity", "repair_work", "repair_monthly_work", "repair_tools", "repair_parts", "repair_min_months", "tech_step_percent", "tech_max_reduction", "durable_wear_period", "optional_consumption_period", "apparel_replacement_period", "cold_bonus_months", "digital_food_reserve", "software_base_quality", "software_quality_step", "initial_specialist_skill", "computer_hours", "quality_base", "quality_step"]
	for key in required:
		if not _positive(data.parameters.get(key)): errors.append("INDUSTRY_PARAMETER_MISSING_OR_INVALID:" + key)
	if not data.parameters.get("resource_city_multipliers") is Array or data.parameters.get("resource_city_multipliers", []).size() != 3: errors.append("INDUSTRY_RESOURCE_DISTRIBUTION")
	return errors

static func definitions() -> Dictionary:
	var data: Dictionary = _data()
	if data.is_empty(): return {}
	return {"products": data.products.duplicate(true), "recipes": data.recipes.duplicate(true), "buildings": data.buildings.duplicate(true), "resources": data.resources.duplicate(), "product_overrides": {"energy": {"kind": "service", "cargo_size": 0}, "water": {"kind": "service", "cargo_size": 0}, "food": {"shelf_life_months": 3, "cargo_size": 1}, "grain": {"cargo_size": 3}, "flour": {"cargo_size": 3}, "wood": {"cargo_size": 3}, "stone": {"cargo_size": 3}}}

static func content_hash() -> String:
	return JSON.stringify(_data(), "", true).sha256_text()

static func digital_categories() -> Array:
	return _data().get("digital_categories", []).duplicate(true)

static func parameters() -> Dictionary:
	return _data().get("parameters", {}).duplicate(true)

static func _p() -> Dictionary:
	return _data().get("parameters", {})

static func _whole(value: Variant) -> bool:
	return typeof(value) in [TYPE_INT, TYPE_FLOAT] and is_finite(float(value)) and float(value) == floor(float(value)) and abs(float(value)) <= 9007199254740991.0

static func _positive(value: Variant) -> bool:
	return _whole(value) and int(value) > 0

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if item is Dictionary and str(item.get("id", "")) == id: return item
	return {}

static func _keys(value: Dictionary) -> Array:
	var result: Array = value.keys()
	result.sort()
	return result

static func _id(s: Dictionary, prefix: String) -> String:
	var serial: int = int(s.get("industry", {}).get("serial", 0))
	s.industry.serial = serial + 1
	return prefix + "-" + str(serial)

static func _event(s: Dictionary, type: String, reason: String, month: int, extra: Dictionary = {}) -> void:
	var e: Dictionary = {"id": "ev-%d" % s.get("events", []).size(), "month": month, "type": type, "reason": reason, "actor_id": "", "city_id": "", "product_id": "", "quantity": 0, "amount": 0}
	e.merge(extra, true)
	s.events.append(e)

static func _city(s: Dictionary, owner: String) -> String:
	var entity: Dictionary = _find(s.get("companies", []), owner)
	if entity.is_empty(): entity = _find(s.get("people", []), owner)
	return str(entity.get("city_id", ""))

static func _cash(s: Dictionary, owner: String) -> int:
	return Ledger.balance(s, owner + ":cash")

static func _pay(s: Dictionary, from: String, to: String, amount: int, reason: String, month: int) -> bool:
	if amount <= 0: return true
	var result: Dictionary = Ledger.transfer(s, "industry-payment-%d-%d" % [month, s.journal.size()], from, to, amount, reason, month)
	if not result.get("ok", false): return false
	var payer: Dictionary = _find(s.get("companies", []), from)
	var payee: Dictionary = _find(s.get("companies", []), to)
	if not payer.is_empty(): payer.expenses = int(payer.get("expenses", 0)) + amount
	if not payee.is_empty(): payee.revenue = int(payee.get("revenue", 0)) + amount
	var person: Dictionary = _find(s.get("people", []), to)
	if not person.is_empty(): person.last_income = int(person.get("last_income", 0)) + amount
	_event(s, "payment", reason, month, {"actor_id": from, "payee_id": to, "amount": amount, "city_id": _city(s, from)})
	return true

static func _lot(s: Dictionary, owner: String, city: String, product: String) -> Dictionary:
	var id: String = "lot:%s:%s:%s" % [owner, city, product]
	var existing: Dictionary = _find(s.get("inventory", []), id)
	if not existing.is_empty(): return existing
	var item: Dictionary = {"id": id, "owner_id": owner, "city_id": city, "accounting_city_id": city, "location": "warehouse", "product_id": product, "quantity": 0, "reserved": 0, "unit_cost": 0}
	s.inventory.append(item)
	Domain.register_lot(s, item)
	return item

static func _change(s: Dictionary, owner: String, city: String, product: String, delta: int, reason: String, month: int, type: String = "inventory") -> Dictionary:
	var result: Dictionary = Domain.change_inventory(s, owner, city, product, delta, reason, month, type)
	if not result.get("ok", false): _event(s, "industry_failed", str(result.get("error", "INVENTORY_CHANGE_FAILED")), month, {"actor_id": owner, "city_id": city, "product_id": product})
	return result

static func available_quantity(s: Dictionary, owner: String, product: String, city: String = "") -> int:
	var total: int = 0
	for item in s.get("inventory", []):
		if item.get("owner_id") == owner and item.get("product_id") == product and item.get("location") != "shipment" and (city == "" or item.get("city_id") == city):
			total += maxi(0, int(item.get("quantity", 0)) - int(item.get("reserved", 0)))
	return total

static func is_service(s: Dictionary, product: String) -> bool:
	return str(_find(s.get("products", []), product).get("kind", "service" if product in ["energy", "water"] else "material")) == "service"

static func initialize(s: Dictionary) -> void:
	if s.has("industry"): return
	s.industry = {"serial": 0, "applied_commands": {}, "equipment_hours": {}, "refund_claims": [], "prepared_month": -1, "processed_month": -1, "delivered_month": -1, "closed_month": -1, "research": [], "technology": [], "adoptions": [], "repairs": [], "software": [], "licenses": [], "subscriptions": [], "advertising": [], "agriculture": [], "service_capacity": [], "service_entitlements": [], "routes": [], "warehouses": [], "resource_opening": {}, "software_usage": [], "durables": []}
	var city_ids: Array = []
	for city in s.get("cities", []): city_ids.append(str(city.id))
	city_ids.sort()
	for index in range(city_ids.size()):
		var city: Dictionary = _find(s.cities, city_ids[index])
		var opening: Dictionary = initial_reserves(index)
		if not city.has("resources"): city.resources = {}
		for resource in _keys(opening):
			if not city.resources.has(resource): city.resources[resource] = opening[resource]
		s.industry.resource_opening[city.id] = city.resources.duplicate(true)
	for i in range(city_ids.size()):
		for j in range(i + 1, city_ids.size()):
			var a: Dictionary = _find(s.cities, city_ids[i])
			var b: Dictionary = _find(s.cities, city_ids[j])
			var domestic: bool = a.nation_id == b.nation_id
			s.industry.routes.append({"id": "route:%s:%s" % [a.id, b.id], "from_city": a.id, "to_city": b.id, "mode": "road" if domestic else "sea", "capacity": int(_p().route_capacity), "used": 0, "lead": int(_p().domestic_lead if domestic else _p().foreign_lead), "unit_fee": int(_p().domestic_fee if domestic else _p().foreign_fee), "blocked": false})
	for facility in s.get("facilities", []):
		if facility.get("kind") == "warehouse":
			s.industry.warehouses.append({"id": "warehouse:" + str(facility.id), "owner_id": facility.company_id, "city_id": facility.city_id, "capacity": int(_p().warehouse_capacity_increment), "refrigerated": false, "facility_id": facility.id})
	for person in s.get("people", []):
		if not person.has("skills"): person.skills = {}
		for facility in s.get("facilities", []):
			if facility.company_id != person.get("company_id", "") or person.get("role") == "director": continue
			if facility.kind == "research_lab": person.skills["research"] = int(_p().initial_specialist_skill)
			if facility.kind == "software_studio": person.skills["software"] = int(_p().initial_specialist_skill)
		var categories: Array = _data().digital_categories
		person.digital_preference = categories[int(str(person.id).sha256_text().substr(0, 6).hex_to_int()) % categories.size()].id
	for company in s.get("companies", []):
		for topic in _data().tech_topics:
			s.industry.technology.append({"id": "tech:%s:%s" % [company.id, topic.id], "owner_id": company.id, "topic": topic.id, "level": 0})

static func initial_reserves(city_index: int) -> Dictionary:
	var result: Dictionary = {}
	var p: Dictionary = _p()
	var multipliers: Array = p.resource_city_multipliers
	for resource in _data().get("resources", []):
		result[resource] = int(int(p.resource_base) * int(multipliers[city_index % multipliers.size()]) / 100) + city_index * int(p.resource_city_step)
	result.fish_stock = int(p.fish_capacity)
	return result

static func resource_available(s: Dictionary, city_id: String, resource: String) -> int:
	return maxi(0, int(_find(s.get("cities", []), city_id).get("resources", {}).get(resource, 0)))

static func consume_resource(s: Dictionary, city_id: String, resource: String, quantity: int, month: int, owner: String = "") -> bool:
	if quantity < 0 or resource_available(s, city_id, resource) < quantity: return false
	var city: Dictionary = _find(s.cities, city_id)
	city.resources[resource] = int(city.resources[resource]) - quantity
	_event(s, "resource_extraction", "유한 매장·생물 자원 채취", month, {"actor_id": owner, "city_id": city_id, "resource": resource, "quantity": quantity})
	return true

static func process_gate(s: Dictionary, facility: Dictionary, recipe: Dictionary, month: int) -> Dictionary:
	var limit: int = int(facility.get("capacity", 0))
	if recipe.get("catalysts", {}).has("computer"): limit = mini(limit, int(_computer_hours(s, str(facility.company_id)) / maxi(1, int(recipe.get("labor", 1)))))
	for product in _keys(recipe.get("catalysts", {})):
		limit = mini(limit, int(available_quantity(s, str(facility.company_id), str(product), str(facility.city_id)) / int(recipe.catalysts[product])))
	if recipe.has("sow_months") and (1 + (month - 1) % 12) not in recipe.sow_months:
		return {"allowed": false, "reason": "파종 계절 대기", "batch_limit": 0}
	if str(recipe.get("process_kind", "")) in ["agriculture", "livestock"] and int(recipe.get("lead_months", 0)) > 0:
		var active: int = 0
		for project in s.get("industry", {}).get("agriculture", []):
			if project.facility_id == facility.id and project.status == "growing": active += 1
		if active >= int(_p().agriculture_max_projects): return {"allowed": false, "reason": "생육 현장 수용능력 부족", "batch_limit": 0}
	return {"allowed": limit > 0, "reason": "촉매·작업 설비 부족" if limit <= 0 else "", "batch_limit": limit}

static func product_quality(s: Dictionary, facility: Dictionary) -> int:
	var level: int = 0
	for adoption in s.get("industry", {}).get("adoptions", []):
		if adoption.get("facility_id") == facility.get("id") and adoption.get("topic") == "product_quality" and adoption.get("status") == "installed": level = maxi(level, int(adoption.get("level", 1)))
	return int(_p().quality_base) + level * int(_p().quality_step)

static func record_output(s: Dictionary, facility: Dictionary, recipe: Dictionary, batches: int, month: int) -> void:
	if batches <= 0: return
	if recipe.get("catalysts", {}).has("computer"):
		var key: String = str(facility.company_id) + ":computer"
		s.industry.equipment_hours[key] = int(s.industry.equipment_hours.get(key, 0)) + batches * int(recipe.labor)
	var process: String = str(recipe.get("process_kind", "manufacturing"))
	if process in ["agriculture", "livestock"] and int(recipe.get("lead_months", 0)) > 0:
		var reservations: Array = []
		for catalyst in _keys(recipe.get("catalysts", {})):
			var reserved: int = int(recipe.catalysts[catalyst]) * batches
			reservations.append_array(_reserve_stock(s, str(facility.company_id), str(facility.city_id), catalyst, reserved))
		var project: Dictionary = {"id": _id(s, "growth"), "owner_id": facility.company_id, "city_id": facility.city_id, "facility_id": facility.id, "recipe_id": recipe.id, "start_month": month, "ready_month": month + int(recipe.lead_months), "status": "growing", "reservations": reservations, "batches": batches, "outputs": recipe.outputs.duplicate(true), "quality": product_quality(s, facility), "reason": "투입·파종 완료, 생육 기간 필요"}
		s.industry.agriculture.append(project)
		_allocate_outputs(s, str(facility.company_id), recipe.outputs, batches, month, str(project.id))
		_event(s, "biological_start", project.reason, month, {"actor_id": facility.company_id, "city_id": facility.city_id, "project_id": project.id, "facility_id": facility.id, "recipe_id": recipe.id, "batches": batches})
		return
	_allocate_outputs(s, str(facility.company_id), recipe.get("outputs", {}), batches, month)
	for product in _keys(recipe.get("outputs", {})):
		var quantity: int = int(recipe.outputs[product]) * batches
		if is_service(s, product): publish_service_capacity(s, str(facility.company_id), str(facility.city_id), product, quantity, month)
		else:
			var made: Dictionary = _change(s, str(facility.company_id), str(facility.city_id), product, quantity, "산업 공정 산출", month, "production_output")
			for lot_id in made.get("lot_ids", []):
				var lot: Dictionary = _find(s.inventory, str(lot_id))
				var previous: int = maxi(0, int(lot.quantity) - quantity)
				var quality: int = product_quality(s, facility)
				lot.quality = int((previous * int(lot.get("quality", _p().quality_base)) + quantity * quality) / maxi(1, int(lot.quantity)))

static func _finance(s: Dictionary) -> Variant:
	if not s.get("finance") is Dictionary: return null
	return load("res://core/finance.gd")

static func _allocate_outputs(s: Dictionary, owner: String, recipe_outputs: Dictionary, batches: int, month: int, project_id: String = "") -> void:
	var finance: Variant = _finance(s)
	if finance == null or not finance.has_method("allocate_production"): return
	var total: Dictionary = {}
	for product in _keys(recipe_outputs): total[product] = int(recipe_outputs[product]) * batches
	finance.allocate_production(s, owner, total, month, project_id)

static func publish_service_capacity(s: Dictionary, owner: String, city_id: String, product: String, quantity: int, month: int) -> void:
	if quantity <= 0: return
	var id: String = "service:%s:%s:%s:%d" % [owner, city_id, product, month]
	var lot: Dictionary = _find(s.industry.service_capacity, id)
	if lot.is_empty():
		var company: Dictionary = _find(s.get("companies", []), owner)
		var price: int = int(_find(s.get("products", []), product).get("price", 1))
		price = maxi(1, int(round(float(price) * float(company.get("policy", {}).get("price_percent", 100)) / 100.0)))
		lot = {"id": id, "owner_id": owner, "city_id": city_id, "product_id": product, "month": month, "capacity": 0, "used": 0, "unit_price": price}
		s.industry.service_capacity.append(lot)
	lot.capacity = int(lot.capacity) + quantity
	_event(s, "service_capacity", "당월 제공 능력 생산", month, {"actor_id": owner, "city_id": city_id, "product_id": product, "quantity": quantity})

static func service_available(s: Dictionary, owner: String, product: String, city_id: String = "") -> int:
	var total: int = 0
	var month: int = int(s.get("planning_month", 0))
	for lot in s.get("industry", {}).get("service_entitlements", []):
		if lot.owner_id == owner and lot.product_id == product and int(lot.month) == month and (city_id == "" or lot.city_id == city_id): total += maxi(0, int(lot.quantity) - int(lot.used))
	for lot in s.get("industry", {}).get("service_capacity", []):
		if lot.owner_id == owner and lot.product_id == product and int(lot.month) == month and (city_id == "" or lot.city_id == city_id): total += maxi(0, int(lot.capacity) - int(lot.used))
	return total

static func service_supply(s: Dictionary, city_id: String, product: String) -> int:
	var total: int = 0
	for lot in s.get("industry", {}).get("service_capacity", []):
		if lot.city_id == city_id and lot.product_id == product: total += maxi(0, int(lot.capacity) - int(lot.used))
	return total

static func purchase_service(s: Dictionary, buyer: String, city_id: String, product: String, quantity: int, month: int, max_unit_price: int = -1, production: bool = false, budget_limit: int = -1) -> Dictionary:
	if quantity <= 0: return {"ok": true, "quantity": 0, "cost": 0, "error": ""}
	var candidates: Array = []
	for lot in s.get("industry", {}).get("service_capacity", []):
		if lot.city_id == city_id and lot.product_id == product and lot.owner_id != buyer and int(lot.month) == month and int(lot.capacity) > int(lot.used) and (max_unit_price < 0 or int(lot.unit_price) <= max_unit_price): candidates.append(lot)
	candidates.sort_custom(func(a, b): return int(a.unit_price) < int(b.unit_price) if int(a.unit_price) != int(b.unit_price) else str(a.id) < str(b.id))
	var acquired: int = 0
	var cost: int = 0
	for lot in candidates:
		var take: int = mini(quantity - acquired, int(lot.capacity) - int(lot.used))
		take = mini(take, int(_cash(s, buyer) / int(lot.unit_price)))
		if budget_limit >= 0: take = mini(take, int(maxi(0, budget_limit - cost) / int(lot.unit_price)))
		if take <= 0: continue
		var amount: int = take * int(lot.unit_price)
		var finance: Variant = _finance(s)
		if finance != null and finance.has_method("settle_service"):
			var settled: Dictionary = finance.settle_service(s, "service-payment-%d-%d" % [month, s.journal.size()], buyer, str(lot.owner_id), amount, month, "production" if production else "consumption")
			if not settled.get("ok", false): continue
			var payer: Dictionary = _find(s.get("companies", []), buyer)
			var payee: Dictionary = _find(s.get("companies", []), str(lot.owner_id))
			if not payer.is_empty(): payer.expenses = int(payer.get("expenses", 0)) + amount
			if not payee.is_empty(): payee.revenue = int(payee.get("revenue", 0)) + amount
		else:
			if not _pay(s, buyer, str(lot.owner_id), amount, "시간 기반 서비스 구매", month): continue
		lot.used = int(lot.used) + take
		var id: String = "entitlement:%s:%s:%s:%d" % [buyer, city_id, product, month]
		var right: Dictionary = _find(s.industry.service_entitlements, id)
		if right.is_empty():
			right = {"id": id, "owner_id": buyer, "city_id": city_id, "product_id": product, "month": month, "quantity": 0, "used": 0}
			s.industry.service_entitlements.append(right)
		right.quantity = int(right.quantity) + take
		acquired += take
		cost += take * int(lot.unit_price)
		_event(s, "service_trade", "실제 제공능력과 예산으로 서비스 체결", month, {"actor_id": buyer, "seller_id": lot.owner_id, "city_id": city_id, "product_id": product, "quantity": take, "amount": take * int(lot.unit_price)})
		if acquired == quantity: break
	return {"ok": acquired == quantity, "quantity": acquired, "cost": cost, "error": "SERVICE_CAPACITY_OR_BUDGET" if acquired < quantity else ""}

static func consume_service(s: Dictionary, owner: String, city_id: String, product: String, quantity: int, month: int) -> bool:
	if quantity < 0 or service_available(s, owner, product, city_id) < quantity: return false
	var remaining: int = quantity
	for collection in ["service_entitlements", "service_capacity"]:
		for lot in s.industry[collection]:
			if lot.owner_id != owner or lot.city_id != city_id or lot.product_id != product or int(lot.month) != month: continue
			var capacity: int = int(lot.quantity) if collection == "service_entitlements" else int(lot.capacity)
			var used: int = mini(remaining, capacity - int(lot.used))
			lot.used = int(lot.used) + used
			remaining -= used
			if remaining == 0: break
		if remaining == 0: break
	_event(s, "service_consumption", "당월 서비스 사용", month, {"actor_id": owner, "city_id": city_id, "product_id": product, "quantity": quantity})
	return true

static func command_types() -> Array:
	return ["industry_research", "industry_adopt", "industry_software", "industry_release", "industry_license", "industry_subscribe", "industry_cancel_subscription", "industry_advertise", "industry_agriculture", "industry_cancel_agriculture", "industry_ship", "industry_recycle", "industry_warehouse", "industry_repair"]

static func ui_actions() -> Array:
	return [
		{"type": "industry_repair", "label": "피해 시설 수리", "scope": "company", "fields": [{"key": "facility_id", "label": "수리할 내 사업장", "kind": "choice", "source": "facilities", "filter": "own"}]},
		{"type": "industry_research", "label": "연구 착수", "scope": "company", "fields": [{"key": "topic", "label": "연구 분야", "kind": "choice", "source": "tech_topics"}, {"key": "monthly_budget", "label": "월 연구 한도", "kind": "int", "min": 1, "max": int(_p().max_budget), "default": int(_p().research_monthly_budget)}]},
		{"type": "industry_adopt", "label": "연구 기술 도입", "scope": "company", "fields": [{"key": "topic", "label": "보유 기술", "kind": "choice", "source": "tech_topics"}, {"key": "facility_id", "label": "적용 사업장", "kind": "choice", "source": "facilities", "filter": "own"}]},
		{"type": "industry_software", "label": "소프트웨어 개발", "scope": "company", "fields": [{"key": "name", "label": "제품명", "kind": "text", "default": "업무 소프트웨어"}, {"key": "platform", "label": "호환 플랫폼", "kind": "text", "default": "open-platform"}, {"key": "category", "label": "콘텐츠 분야", "kind": "choice", "source": "digital_categories"}, {"key": "kind", "label": "제품 방식", "kind": "choice", "options": ["download", "online"], "default": "download"}, {"key": "price", "label": "좌석당 가격", "kind": "int", "min": 1, "max": int(_p().max_budget), "default": int(_p().software_default_price)}]},
		{"type": "industry_release", "label": "제품 출시", "scope": "company", "fields": [{"key": "software_id", "label": "개발 제품", "kind": "choice", "source": "software", "filter": "own"}]},
		{"type": "industry_license", "label": "사용권 구매", "scope": "both", "fields": [{"key": "software_id", "label": "출시 소프트웨어", "kind": "choice", "source": "software", "filter": "own_or_released"}, {"key": "seats", "label": "좌석 수", "kind": "int", "min": 1, "max": int(_p().license_max_seats), "default": 1}, {"key": "platform", "label": "이용 플랫폼", "kind": "text", "default": "open-platform"}]},
		{"type": "industry_subscribe", "label": "구독 계약", "scope": "both", "fields": [{"key": "software_id", "label": "출시 서비스", "kind": "choice", "source": "software", "filter": "own_or_released"}, {"key": "seats", "label": "월 좌석 수", "kind": "int", "min": 1, "max": int(_p().license_max_seats), "default": 1}, {"key": "platform", "label": "이용 플랫폼", "kind": "text", "default": "open-platform"}]},
		{"type": "industry_cancel_subscription", "label": "구독 갱신 중단", "scope": "both", "fields": [{"key": "subscription_id", "label": "구독 계약", "kind": "choice", "source": "subscriptions"}]},
		{"type": "industry_advertise", "label": "온라인 광고 계약", "scope": "both", "fields": [{"key": "software_id", "label": "광고 매체", "kind": "choice", "source": "software", "filter": "own_or_released"}, {"key": "budget", "label": "실제 지급 한도", "kind": "int", "min": 1, "max": int(_p().max_budget), "default": 100}]},
		{"type": "industry_agriculture", "label": "생육 작업 계획", "scope": "company", "fields": [{"key": "facility_id", "label": "농축산 사업장", "kind": "choice", "source": "facilities", "filter": "own"}, {"key": "batches", "label": "작업 배치", "kind": "int", "min": 1, "max": int(_p().max_quantity), "default": 1}]},
		{"type": "industry_cancel_agriculture", "label": "생육 중단", "scope": "company", "fields": [{"key": "project_id", "label": "생육 프로젝트", "kind": "choice", "source": "agriculture"}]},
		{"type": "industry_ship", "label": "도시 간 운송", "scope": "company", "fields": [{"key": "product_id", "label": "실물 품목", "kind": "choice", "source": "products"}, {"key": "to_city", "label": "도착 도시", "kind": "choice", "source": "cities"}, {"key": "quantity", "label": "수량", "kind": "int", "min": 1, "max": int(_p().max_quantity), "default": 1}]},
		{"type": "industry_recycle", "label": "재활용 처리", "scope": "company", "fields": [{"key": "batches", "label": "처리 배치", "kind": "int", "min": 1, "max": int(_p().max_quantity), "default": 1}]},
		{"type": "industry_warehouse", "label": "보관 설비 증설", "scope": "company", "fields": [{"key": "refrigerated", "label": "냉장 여부", "kind": "choice", "options": ["no", "yes"], "default": "no"}]}
	]

static func topics() -> Array:
	return _data().get("tech_topics", []).duplicate(true)

static func _owner(s: Dictionary, c: Dictionary) -> String:
	var actor: String = str(c.get("actor_id", ""))
	if _find(s.get("people", []), actor).is_empty(): return ""
	var company: String = str(c.get("acting_company_id", ""))
	if company.is_empty(): return actor
	var entity: Dictionary = _find(s.get("companies", []), company)
	if entity.is_empty() or entity.get("status", "active") != "active": return ""
	if entity.get("director_id", "") == actor: return company
	if ResourceLoader.exists("res://core/lifecycle.gd"):
		var lifecycle: Variant = load("res://core/lifecycle.gd")
		if lifecycle != null and lifecycle.has_method("can_act") and lifecycle.can_act(s, actor, company, str(c.get("type", "")), c.get("payload", {})): return company
	for delegation in s.get("lifecycle", {}).get("delegations", []):
		if delegation.get("company_id") == company and delegation.get("person_id") == actor and delegation.get("status") == "active" and str(c.get("type", "")) in delegation.get("permissions", []) and delegation.get("grantor_id") == entity.get("director_id"):
			return company
	return ""

static func _provider(s: Dictionary, city: String, buyer: String, product: String = "professional_hour") -> String:
	var candidates: Array = []
	for facility in s.get("facilities", []):
		if facility.company_id == buyer or facility.get("status", "") not in ["operational", "ready"]: continue
		var recipe: Dictionary = _find(s.get("recipes", []), str(facility.get("recipe_id", "")))
		if recipe.get("outputs", {}).has(product) or (product == "professional_hour" and recipe.get("outputs", {}).has("development_hour")):
			candidates.append({"id": facility.company_id, "local": facility.city_id == city})
	candidates.sort_custom(func(a, b): return str(a.id) < str(b.id) if bool(a.local) == bool(b.local) else bool(a.local))
	return str(candidates[0].id) if not candidates.is_empty() else ""

static func _work_plan(s: Dictionary, owner: String, requested: int, skill: String = "") -> Dictionary:
	var remaining: int = requested
	var assignments: Array = []
	var people: Array = s.get("people", []).duplicate()
	people.sort_custom(func(a, b): return str(a.id) < str(b.id))
	var wages: int = 0
	for person in people:
		if person.get("company_id", "") != owner or person.get("role") in ["director", "builder"] or person.get("employment_status", "employed") != "employed": continue
		if not skill.is_empty() and int(person.get("skills", {}).get(skill, 0)) < 1: continue
		var used_before: int = 0 if s.get("phase", "Planning") == "Planning" else int(s.get("monthly_labor", {}).get(person.id, 0))
		var free: int = maxi(0, int(person.get("hours", 0)) - used_before)
		var used: int = mini(remaining, free)
		if used > 0:
			assignments.append({"person_id": person.id, "hours": used, "wages": used * int(person.get("wage", 0))})
			wages += used * int(person.get("wage", 0))
			remaining -= used
		if remaining == 0: break
	return {"ok": remaining == 0 and _cash(s, owner) >= wages, "assignments": assignments, "wages": wages, "hours": requested - remaining}

static func _work(s: Dictionary, owner: String, requested: int, month: int, reason: String) -> bool:
	var skill: String = "research" if "연구" in reason else ("software" if "소프트웨어" in reason else "")
	var plan: Dictionary = _work_plan(s, owner, requested, skill)
	if not skill.is_empty() and _computer_hours(s, owner) < requested: return false
	if not plan.ok: return false
	for assignment in plan.assignments:
		if assignment.wages > 0 and not _pay(s, owner, str(assignment.person_id), int(assignment.wages), reason, month): return false
		s.monthly_labor[assignment.person_id] = int(s.monthly_labor.get(assignment.person_id, 0)) + int(assignment.hours)
		_event(s, "labor", reason, month, {"actor_id": assignment.person_id, "company_id": owner, "city_id": _city(s, owner), "hours": assignment.hours, "amount": int(assignment.wages), "wage_paid": true})
	if not skill.is_empty():
		var equipment_key: String = owner + ":computer"
		s.industry.equipment_hours[equipment_key] = int(s.industry.equipment_hours.get(equipment_key, 0)) + requested
	if reason in ["농축산 생육 작업", "재활용 처리 노동"]:
		var finance: Variant = _finance(s)
		if finance != null and finance.has_method("record_labor"): finance.record_labor(s, owner, int(plan.wages), month)
	return true

static func _tech(s: Dictionary, owner: String, topic: String) -> int:
	if topic == "software_automation": return 1 if _business_license(s, owner) and available_quantity(s, owner, "computer", _city(s, owner)) > 0 else 0
	for tech in s.get("industry", {}).get("technology", []):
		if tech.owner_id == owner and tech.topic == topic: return int(tech.level)
	return 0

static func labor_per_batch(s: Dictionary, facility: Dictionary, recipe: Dictionary) -> int:
	var level: int = int(facility.get("tech_levels", {}).get("process_efficiency", 0))
	if int(facility.get("tech_levels", {}).get("software_automation", 0)) > 0 and _tech(s, str(facility.company_id), "software_automation") > 0: level += 1
	return maxi(1, int(floor(float(recipe.get("labor", 1)) * float(100 - mini(int(_p().tech_max_reduction), level * int(_p().tech_step_percent))) / 100.0)))

static func input_quantity(_s: Dictionary, facility: Dictionary, recipe: Dictionary, product: String, batches: int) -> int:
	var total: int = int(recipe.get("inputs", {}).get(product, 0)) * batches
	if product == "energy":
		var level: int = int(facility.get("tech_levels", {}).get("energy_efficiency", 0))
		total = int(ceil(float(total) * float(100 - mini(int(_p().tech_max_reduction), level * int(_p().tech_step_percent))) / 100.0))
	return total

static func _has_inputs(s: Dictionary, owner: String, city: String, recipe: Dictionary, batches: int) -> bool:
	for product in _keys(recipe.get("inputs", {})):
		var required: int = int(recipe.inputs[product]) * batches
		if is_service(s, product):
			if service_available(s, owner, product, city) < required: return false
		elif available_quantity(s, owner, product, city) < required: return false
	return true

static func validate_command(s: Dictionary, c: Dictionary) -> String:
	if str(c.get("type", "")) not in command_types(): return "INDUSTRY_COMMAND_UNKNOWN"
	if not s.get("industry") is Dictionary: return "INDUSTRY_NOT_INITIALIZED"
	if str(c.get("id", "")).is_empty(): return "INDUSTRY_COMMAND_ID_REQUIRED"
	if s.industry.get("applied_commands", {}).has(str(c.id)): return "INDUSTRY_DUPLICATE_COMMAND"
	var owner: String = _owner(s, c)
	if owner.is_empty(): return "INDUSTRY_AUTHORITY_REQUIRED"
	var type: String = str(c.type)
	var payload: Variant = c.get("payload", {})
	if not payload is Dictionary: return "INDUSTRY_PAYLOAD_INVALID"
	var p: Dictionary = payload
	var city: String = _city(s, owner)
	var company: bool = not _find(s.get("companies", []), owner).is_empty()
	if type not in ["industry_license", "industry_subscribe", "industry_cancel_subscription", "industry_advertise"] and not company: return "INDUSTRY_COMPANY_REQUIRED"
	if type == "industry_research":
		var topic: Dictionary = _find(_data().tech_topics, str(p.get("topic", "")))
		if topic.is_empty(): return "RESEARCH_TOPIC_UNKNOWN"
		if topic.get("license_required", false): return "RESEARCH_LICENSE_CANNOT_BE_INVENTED"
		if _tech(s, owner, str(topic.id)) >= int(topic.level_cap): return "RESEARCH_LEVEL_CAP"
		for project in s.industry.research:
			if project.owner_id == owner and project.topic == topic.id and project.status in ["researching", "paused"]: return "RESEARCH_ALREADY_ACTIVE"
		if not _positive(p.get("monthly_budget")) or int(p.monthly_budget) > int(_p().max_budget): return "RESEARCH_BUDGET_INVALID"
		if available_quantity(s, owner, "computer", city) < 1: return "RESEARCH_COMPUTER_REQUIRED"
		if _provider(s, city, owner).is_empty(): return "RESEARCH_SERVICE_SUPPLIER_REQUIRED"
		if _cash(s, owner) < int(_p().research_setup_fee): return "INDUSTRY_FUNDS_REQUIRED"
	elif type == "industry_adopt":
		if _tech(s, owner, str(p.get("topic", ""))) <= 0: return "TECHNOLOGY_NOT_OWNED"
		var facility: Dictionary = _find(s.get("facilities", []), str(p.get("facility_id", "")))
		if facility.is_empty() or facility.get("company_id") != owner: return "FACILITY_AUTHORITY_REQUIRED"
		if int(facility.get("tech_levels", {}).get(str(p.topic), 0)) >= _tech(s, owner, str(p.topic)): return "TECHNOLOGY_ALREADY_ADOPTED"
		for adoption in s.industry.adoptions:
			if adoption.facility_id == facility.id and adoption.topic == p.topic and adoption.status == "installing": return "TECHNOLOGY_INSTALLATION_ACTIVE"
		if _cash(s, owner) < int(_p().adoption_cost) or available_quantity(s, owner, "tools", city) < 1: return "TECHNOLOGY_INSTALLATION_RESOURCES"
		if _provider(s, city, owner).is_empty(): return "RESEARCH_SERVICE_SUPPLIER_REQUIRED"
	elif type == "industry_software":
		if not p.get("name") is String or str(p.name).strip_edges().is_empty() or str(p.name).length() > 48: return "SOFTWARE_NAME_INVALID"
		if not p.get("platform") is String or str(p.platform).strip_edges().is_empty() or str(p.platform).length() > 32: return "SOFTWARE_PLATFORM_INVALID"
		if str(p.get("category", "business")) not in _data().digital_categories.map(func(item): return str(item.id)): return "SOFTWARE_CATEGORY_INVALID"
		if p.get("kind", "download") not in ["download", "online"]: return "SOFTWARE_KIND_INVALID"
		if not _positive(p.get("price")) or int(p.price) > int(_p().max_budget): return "SOFTWARE_PRICE_INVALID"
		if available_quantity(s, owner, "computer", city) < 1: return "SOFTWARE_DEVELOPMENT_EQUIPMENT"
		if _cash(s, owner) < int(_p().software_setup_fee) or _provider(s, city, owner).is_empty(): return "SOFTWARE_START_RESOURCES"
	elif type == "industry_release":
		var software: Dictionary = _find(s.industry.software, str(p.get("software_id", "")))
		if software.is_empty() or software.owner_id != owner: return "SOFTWARE_AUTHORITY_REQUIRED"
		if software.status != "ready": return "SOFTWARE_NOT_READY"
	elif type in ["industry_license", "industry_subscribe"]:
		var software: Dictionary = _find(s.industry.software, str(p.get("software_id", "")))
		if software.is_empty() or software.status != "released" or _find(s.get("companies", []), str(software.owner_id)).get("status", "active") != "active": return "SOFTWARE_NOT_RELEASED"
		if software.owner_id == owner: return "SOFTWARE_SELF_LICENSE_NOT_SALE"
		if type == "industry_license" and software.get("kind", "download") == "online": return "ONLINE_SUBSCRIPTION_REQUIRED"
		if not _positive(p.get("seats")) or int(p.seats) > int(_p().license_max_seats): return "SOFTWARE_SEATS_INVALID"
		if str(p.get("platform", "")) != str(software.platform): return "SOFTWARE_PLATFORM_INCOMPATIBLE"
		if _cash(s, owner) < int(p.seats) * int(software.price): return "INDUSTRY_FUNDS_REQUIRED"
		if software.kind == "online" and int(p.seats) + _licensed_seats(s, software.id) > _online_capacity(s, software): return "ONLINE_SERVER_CAPACITY"
	elif type == "industry_cancel_subscription":
		var subscription: Dictionary = _find(s.industry.subscriptions, str(p.get("subscription_id", "")))
		if subscription.is_empty() or subscription.buyer_id != owner or not subscription.auto_renew: return "SUBSCRIPTION_AUTHORITY_REQUIRED"
	elif type == "industry_advertise":
		var software: Dictionary = _find(s.industry.software, str(p.get("software_id", "")))
		if software.is_empty() or software.status != "released" or software.kind != "online" or software.owner_id == owner: return "ADVERTISING_MEDIA_REQUIRED"
		if not _positive(p.get("budget")) or int(p.budget) > int(_p().max_budget) or _cash(s, owner) < int(p.budget): return "ADVERTISING_BUDGET_INVALID"
	elif type == "industry_agriculture":
		var facility: Dictionary = _find(s.get("facilities", []), str(p.get("facility_id", "")))
		if facility.is_empty() or facility.company_id != owner or facility.status != "operational": return "FACILITY_AUTHORITY_REQUIRED"
		var recipe: Dictionary = _find(s.get("recipes", []), str(facility.recipe_id))
		if recipe.get("process_kind", "") not in ["agriculture", "livestock"] or int(recipe.get("lead_months", 0)) <= 0: return "BIOLOGICAL_PROCESS_REQUIRED"
		if not _positive(p.get("batches")) or int(p.batches) > int(_p().max_quantity): return "INDUSTRY_QUANTITY_INVALID"
		var gate: Dictionary = process_gate(s, facility, recipe, int(s.planning_month))
		if not gate.allowed or int(p.batches) > int(gate.batch_limit): return "BIOLOGICAL_CAPACITY_OR_SEASON"
		if not _has_inputs(s, owner, city, recipe, int(p.batches)): return "BIOLOGICAL_INPUTS_REQUIRED"
		if not _work_plan(s, owner, int(p.batches) * int(recipe.labor)).ok: return "BIOLOGICAL_WORK_OR_WAGES_REQUIRED"
	elif type == "industry_cancel_agriculture":
		var project: Dictionary = _find(s.industry.agriculture, str(p.get("project_id", "")))
		if project.is_empty() or project.owner_id != owner or project.status != "growing": return "BIOLOGICAL_PROJECT_AUTHORITY"
	elif type == "industry_ship":
		if not _positive(p.get("quantity")) or int(p.quantity) > int(_p().max_quantity): return "INDUSTRY_QUANTITY_INVALID"
		var product: String = str(p.get("product_id", ""))
		if is_service(s, product) or str(_find(s.products, product).get("kind", "")) == "digital": return "PHYSICAL_CARGO_REQUIRED"
		var destination: Dictionary = _find(s.get("cities", []), str(p.get("to_city", "")))
		if destination.is_empty() or destination.id == city: return "SHIPPING_DESTINATION_INVALID"
		if available_quantity(s, owner, product, city) < int(p.quantity): return "SHIPPING_STOCK_REQUIRED"
		var quote: Dictionary = route_quote(s, city, destination.id, product, int(p.quantity))
		if not quote.get("allowed", false) or int(quote.get("quantity", 0)) < int(p.quantity): return str(quote.get("error", "SHIPPING_CAPACITY"))
		if _cash(s, owner) < int(quote.fee): return "INDUSTRY_FUNDS_REQUIRED"
	elif type == "industry_recycle":
		if not _positive(p.get("batches")) or int(p.batches) > int(_p().max_quantity): return "INDUSTRY_QUANTITY_INVALID"
		if available_quantity(s, owner, "recyclable_waste", city) < int(p.batches) * int(_p().recycle_units_in): return "RECYCLABLE_INPUT_REQUIRED"
		if not _work_plan(s, owner, int(p.batches) * int(_p().recycle_work)).ok: return "RECYCLING_WORK_REQUIRED"
	elif type == "industry_repair":
		var facility: Dictionary = _find(s.get("facilities", []), str(p.get("facility_id", "")))
		if facility.is_empty() or facility.company_id != owner or not facility.get("repair_required", false): return "DAMAGED_FACILITY_REQUIRED"
		for repair in s.industry.repairs:
			if repair.facility_id == facility.id and repair.status == "repairing": return "REPAIR_ALREADY_ACTIVE"
		if available_quantity(s, owner, "tools", city) < int(_p().repair_tools) or available_quantity(s, owner, "machine_parts", city) < int(_p().repair_parts): return "REPAIR_MATERIALS_REQUIRED"
	elif type == "industry_warehouse":
		if available_quantity(s, owner, "wood", city) < int(_p().warehouse_wood) or available_quantity(s, owner, "steel", city) < int(_p().warehouse_steel): return "WAREHOUSE_MATERIALS_REQUIRED"
		var work: Dictionary = _work_plan(s, owner, int(_p().warehouse_work))
		if not work.ok or _cash(s, owner) < int(work.wages) + int(_p().warehouse_cost) or _provider(s, city, owner).is_empty(): return "WAREHOUSE_WORK_OR_BUDGET"
	return ""

static func apply(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var error: String = validate_command(s, c)
	if not error.is_empty(): return {"ok": false, "error": error}
	var owner: String = _owner(s, c)
	var city: String = _city(s, owner)
	var p: Dictionary = c.payload
	var type: String = str(c.type)
	if type == "industry_research":
		_pay(s, owner, _provider(s, city, owner), int(_p().research_setup_fee), "연구 계약 착수", month)
		var project: Dictionary = {"id": _id(s, "research"), "owner_id": owner, "city_id": city, "topic": str(p.topic), "level": _tech(s, owner, str(p.topic)) + 1, "required_work": int(_p().research_work), "work": 0, "start_month": month, "min_months": int(_p().research_min_months), "budget": int(p.monthly_budget), "spent": int(_p().research_setup_fee), "status": "researching", "reason": "연구 인력 배정 대기"}
		s.industry.research.append(project)
		_event(s, "research_start", "노동·예산과 기간이 필요한 연구 착수", month, {"actor_id": owner, "city_id": city, "project_id": project.id, "topic": p.topic})
	elif type == "industry_adopt":
		_pay(s, owner, _provider(s, city, owner), int(_p().adoption_cost), "기술 도입 계약", month)
		_change(s, owner, city, "tools", -1, "설비 개조 도구 투입", month, "adoption_input")
		s.industry.adoptions.append({"id": _id(s, "adoption"), "owner_id": owner, "city_id": city, "topic": str(p.topic), "facility_id": str(p.facility_id), "level": _tech(s, owner, str(p.topic)), "start_month": month, "ready_month": month + int(_p().adoption_months), "work": 0, "required_work": int(_p().research_monthly_work), "status": "installing"})
	elif type == "industry_software":
		_pay(s, owner, _provider(s, city, owner), int(_p().software_setup_fee), "개발 프로젝트 착수", month)
		var version: int = 1
		for previous in s.industry.software:
			if previous.owner_id == owner and previous.name == p.name: version = maxi(version, int(previous.version) + 1)
		var software: Dictionary = {"id": _id(s, "software"), "owner_id": owner, "city_id": city, "name": str(p.name), "category": str(p.get("category", "business")), "quality": int(_p().software_base_quality) + _tech(s, owner, "software_platform") * int(_p().software_quality_step), "platform": str(p.platform), "kind": str(p.get("kind", "download")), "price": int(p.price), "version": version, "work": 0, "required_work": int(_p().software_work), "start_month": month, "min_months": int(_p().software_min_months), "status": "developing", "support_until": 0, "active_seats": 0, "reason": "개발·시험 작업 대기"}
		s.industry.software.append(software)
		_event(s, "software_start", "인력·기술·장비를 사용하는 제품 개발", month, {"actor_id": owner, "city_id": city, "software_id": software.id, "version": version})
	elif type == "industry_release":
		var software: Dictionary = _find(s.industry.software, str(p.software_id))
		software.status = "released"
		software.released_month = month
		software.support_until = month + int(_p().software_support_months)
		_event(s, "software_release", "개발·시험을 마친 제품 출시", month, {"actor_id": owner, "city_id": city, "software_id": software.id, "version": software.version})
	elif type in ["industry_license", "industry_subscribe"]:
		var software: Dictionary = _find(s.industry.software, str(p.software_id))
		var amount: int = int(p.seats) * int(software.price)
		if not _digital_charge(s, owner, software, amount, month, "구독 사용권" if type == "industry_subscribe" else "소프트웨어 사용권"): return {"ok": false, "error": "SOFTWARE_PAYMENT_FAILED"}
		var license: Dictionary = {"id": _id(s, "license"), "buyer_id": owner, "owner_id": owner, "software_id": software.id, "publisher_id": software.owner_id, "seats": int(p.seats), "platform": software.platform, "version": software.version, "start_month": month, "expires_month": month + 1 if type == "industry_subscribe" else 0, "perpetual": type == "industry_license", "status": "active", "transferable": false, "paid_month": month, "period_amount": amount, "earned_amount": 0 if software.kind == "online" else amount}
		s.industry.licenses.append(license)
		if software.kind != "online": _digital_output(s, software, owner, int(p.seats), amount, month, "다운로드 사용권 인도")
		if type == "industry_subscribe": s.industry.subscriptions.append({"id": _id(s, "subscription"), "buyer_id": owner, "software_id": software.id, "license_id": license.id, "seats": int(p.seats), "monthly_price": amount, "next_due": month + 1, "auto_renew": true, "status": "active"})
		_event(s, "license_sale", "실제 구매자 잔액에서 지급한 디지털 권리", month, {"actor_id": owner, "seller_id": software.owner_id, "city_id": city, "software_id": software.id, "quantity": int(p.seats), "amount": amount, "license_id": license.id})
	elif type == "industry_cancel_subscription":
		var subscription: Dictionary = _find(s.industry.subscriptions, str(p.subscription_id))
		subscription.auto_renew = false
		_event(s, "subscription_cancel", "기지급 이용기간 뒤 갱신 중단", month, {"actor_id": owner, "city_id": city, "subscription_id": subscription.id})
	elif type == "industry_advertise":
		s.industry.advertising.append({"id": _id(s, "advertisement"), "owner_id": owner, "city_id": city, "software_id": str(p.software_id), "budget": int(p.budget), "spent": 0, "impressions": 0, "status": "active", "start_month": month})
	elif type == "industry_agriculture":
		var facility: Dictionary = _find(s.facilities, str(p.facility_id))
		var recipe: Dictionary = _find(s.recipes, str(facility.recipe_id))
		_work(s, owner, int(p.batches) * int(recipe.labor), month, "농축산 생육 작업")
		for product in _keys(recipe.inputs):
			var quantity: int = int(recipe.inputs[product]) * int(p.batches)
			if is_service(s, product): consume_service(s, owner, city, product, quantity, month)
			else: _change(s, owner, city, product, -quantity, "공정 투입", month, "production_input")
		record_output(s, facility, recipe, int(p.batches), month)
	elif type == "industry_cancel_agriculture":
		var project: Dictionary = _find(s.industry.agriculture, str(p.project_id))
		_release_growth_reservations(s, project)
		var finance: Variant = _finance(s)
		if finance != null and finance.has_method("abandon_growth"): finance.abandon_growth(s, owner, str(project.id), month)
		project.status = "cancelled"
		project.reason = "생육 중단, 이미 투입한 자재·노동은 복원하지 않음"
		_event(s, "biological_cancel", project.reason, month, {"actor_id": owner, "city_id": city, "project_id": project.id})
	elif type == "industry_ship":
		var quantity: int = int(p.quantity)
		var product: String = str(p.product_id)
		var quote: Dictionary = route_quote(s, city, str(p.to_city), product, quantity, month)
		var reserved: Dictionary = reserve_route(s, owner, city, str(p.to_city), product, quantity, month, quote)
		if not reserved.ok: return reserved
		var age: Dictionary = inventory_age(s, owner, city, product, quantity)
		_change(s, owner, city, product, -quantity, "운송 출고", month, "shipment_departure")
		var id: String = _id(s, "shipment")
		var lot_id: String = "lot:" + id
		var lot: Dictionary = {"id": lot_id, "owner_id": owner, "city_id": city, "accounting_city_id": city, "location": "shipment", "product_id": product, "quantity": quantity, "reserved": 0, "shipment_id": id, "unit_cost": int(age.get("unit_cost", 0))}
		lot.merge(age, true)
		s.inventory.append(lot)
		Domain.register_lot(s, lot)
		s.shipments.append({"id": id, "owner_id": owner, "product_id": product, "quantity": quantity, "from_city_id": city, "to_city_id": str(p.to_city), "eta": int(quote.eta), "status": "transit", "lot_id": lot_id, "route_ids": quote.path, "carrier_id": quote.carrier_id})
		_event(s, "shipment", "운송 중에도 한 번만 포함하는 물량", month, {"actor_id": owner, "city_id": city, "product_id": product, "quantity": quantity, "inventory_delta": quantity, "lot_id": lot_id, "shipment_id": id})
	elif type == "industry_recycle":
		var batches: int = int(p.batches)
		_work(s, owner, batches * int(_p().recycle_work), month, "재활용 처리 노동")
		_change(s, owner, city, "recyclable_waste", -batches * int(_p().recycle_units_in), "공정 투입", month, "recycling_input")
		_allocate_outputs(s, owner, {"steel": int(_p().recycle_units_out)}, batches, month)
		_change(s, owner, city, "steel", batches * int(_p().recycle_units_out), "정해진 회수율의 재생 강철", month, "recycling_output")
	elif type == "industry_repair":
		_change(s, owner, city, "tools", -int(_p().repair_tools), "시설 수리 도구", month, "repair_input")
		_change(s, owner, city, "machine_parts", -int(_p().repair_parts), "시설 수리 부품", month, "repair_input")
		s.industry.repairs.append({"id": _id(s, "repair"), "owner_id": owner, "city_id": city, "facility_id": str(p.facility_id), "work": 0, "required_work": int(_p().repair_work), "start_month": month, "status": "repairing"})
	elif type == "industry_warehouse":
		_pay(s, owner, _provider(s, city, owner), int(_p().warehouse_cost), "보관 설비 설치", month)
		_work(s, owner, int(_p().warehouse_work), month, "창고 증설 작업")
		_change(s, owner, city, "wood", -int(_p().warehouse_wood), "창고 증설 자재", month, "construction_input")
		_change(s, owner, city, "steel", -int(_p().warehouse_steel), "창고 증설 자재", month, "construction_input")
		s.industry.warehouses.append({"id": _id(s, "warehouse"), "owner_id": owner, "city_id": city, "capacity": int(_p().warehouse_capacity_increment), "refrigerated": str(p.get("refrigerated", "no")) == "yes", "operational_month": month + 1, "facility_id": ""})
	s.industry.applied_commands[str(c.id)] = month
	return {"ok": true, "error": ""}

static func _carrier(s: Dictionary, from_city: String) -> Dictionary:
	var candidates: Array = []
	for facility in s.get("facilities", []):
		if facility.get("kind", "") != "logistics_hub" or facility.get("status", "") not in ["operational", "ready"]: continue
		var owner: String = str(facility.company_id)
		if _find(s.get("companies", []), owner).get("status", "active") != "active": continue
		if available_quantity(s, owner, "transport_vehicle", str(facility.city_id)) < 1: continue
		candidates.append({"id": owner, "city_id": facility.city_id, "local": facility.city_id == from_city})
	candidates.sort_custom(func(a, b): return str(a.id) < str(b.id) if bool(a.local) == bool(b.local) else bool(a.local))
	return candidates[0] if not candidates.is_empty() else {}

static func _path(s: Dictionary, from: String, to: String, units: int) -> Dictionary:
	var distances: Dictionary = {from: 0}
	var previous: Dictionary = {}
	var open: Array = [from]
	while not open.is_empty():
		open.sort_custom(func(a, b): return int(distances[a]) < int(distances[b]) if int(distances[a]) != int(distances[b]) else str(a) < str(b))
		var current: String = str(open.pop_front())
		if current == to: break
		var edges: Array = s.get("industry", {}).get("routes", []).duplicate()
		edges.sort_custom(func(a, b): return str(a.id) < str(b.id))
		for edge in edges:
			if edge.get("blocked", false) or int(edge.capacity) - int(edge.used) < units: continue
			var next: String = ""
			if edge.from_city == current: next = str(edge.to_city)
			elif edge.to_city == current: next = str(edge.from_city)
			if next.is_empty(): continue
			var candidate: int = int(distances[current]) + int(edge.unit_fee) + int(edge.lead)
			if not distances.has(next) or candidate < int(distances[next]):
				distances[next] = candidate
				previous[next] = {"city": current, "edge": edge.id}
				if next not in open: open.append(next)
	if not distances.has(to): return {"ok": false, "path": [], "lead": 0, "unit_fee": 0}
	var ids: Array = []
	var cursor: String = to
	var lead: int = 0
	var fee: int = 0
	while cursor != from:
		var step: Dictionary = previous[cursor]
		ids.push_front(step.edge)
		var edge: Dictionary = _find(s.industry.routes, str(step.edge))
		lead += int(edge.lead)
		fee += int(edge.unit_fee)
		cursor = str(step.city)
	return {"ok": true, "path": ids, "lead": lead, "unit_fee": fee}

static func route_quote(s: Dictionary, from_city: String, to_city: String, product_id: String, quantity: int, month: int = -1) -> Dictionary:
	if month < 0: month = int(s.get("planning_month", 1))
	if _find(s.get("cities", []), from_city).is_empty() or _find(s.get("cities", []), to_city).is_empty(): return {"allowed": false, "error": "SHIPPING_CITY_UNKNOWN", "quantity": 0}
	var product: Dictionary = _find(s.get("products", []), product_id)
	if product.is_empty() or is_service(s, product_id) or product.get("kind", "") == "digital": return {"allowed": false, "error": "PHYSICAL_CARGO_REQUIRED", "quantity": 0}
	if quantity <= 0: return {"allowed": false, "error": "SHIPPING_QUANTITY_INVALID", "quantity": 0}
	if from_city == to_city: return {"allowed": true, "quantity": quantity, "carrier_id": "", "unit_fee": 0, "fee": 0, "eta": month, "path": [], "fuel": 0, "labor": 0, "error": ""}
	var carrier: Dictionary = _carrier(s, from_city)
	if carrier.is_empty(): return {"allowed": false, "error": "SHIPPING_CARRIER_REQUIRED", "quantity": 0}
	var size: int = maxi(1, int(product.get("cargo_size", 1)))
	var possible: int = quantity
	var path: Dictionary = _path(s, from_city, to_city, size)
	if not path.ok: return {"allowed": false, "error": "SHIPPING_ROUTE_BLOCKED_OR_FULL", "quantity": 0}
	var all_edges: Array = path.path.duplicate()
	var pickup_lead: int = 0
	var pickup_fee: int = 0
	if str(carrier.city_id) != from_city:
		var pickup: Dictionary = _path(s, str(carrier.city_id), from_city, size)
		if not pickup.ok: return {"allowed": false, "error": "SHIPPING_PICKUP_UNREACHABLE", "quantity": 0}
		pickup_lead = int(pickup.lead)
		pickup_fee = int(pickup.unit_fee)
		for edge_id in pickup.path:
			if edge_id not in all_edges: all_edges.append(edge_id)
	for edge_id in all_edges:
		var edge: Dictionary = _find(s.industry.routes, str(edge_id))
		possible = mini(possible, int((int(edge.capacity) - int(edge.used)) / size))
	var carrier_fuel: int = available_quantity(s, str(carrier.id), "fuel", str(carrier.city_id))
	possible = mini(possible, int(carrier_fuel * int(_p().freight_fuel_per_units) / size))
	var workforce: Dictionary = _work_plan(s, str(carrier.id), int(_p().max_quantity))
	possible = mini(possible, int(int(workforce.hours) * int(_p().freight_labor_per_units) / size))
	if possible <= 0: return {"allowed": false, "error": "SHIPPING_FUEL_OR_WORKFORCE", "quantity": 0}
	var cargo_units: int = possible * size
	var unit_fee: int = (int(path.unit_fee) + pickup_fee) * size
	return {"allowed": true, "quantity": possible, "carrier_id": carrier.id, "carrier_city": carrier.city_id, "unit_fee": unit_fee, "fee": possible * unit_fee, "eta": month + int(path.lead) + pickup_lead, "path": all_edges, "fuel": int(ceil(float(cargo_units) / float(_p().freight_fuel_per_units))), "labor": int(ceil(float(cargo_units) / float(_p().freight_labor_per_units))), "cargo_units": cargo_units, "error": ""}

static func reserve_route(s: Dictionary, buyer: String, from_city: String, to_city: String, product_id: String, quantity: int, month: int, _quote: Dictionary = {}) -> Dictionary:
	var quote: Dictionary = route_quote(s, from_city, to_city, product_id, quantity, month)
	if not quote.get("allowed", false) or int(quote.quantity) < quantity: return {"ok": false, "error": str(quote.get("error", "SHIPPING_CAPACITY"))}
	if from_city == to_city: return {"ok": true, "error": "", "quote": quote}
	if _cash(s, buyer) < int(quote.fee): return {"ok": false, "error": "SHIPPING_PAYMENT_REQUIRED"}
	var plan: Dictionary = _work_plan(s, str(quote.carrier_id), int(quote.labor))
	if not plan.ok and _cash(s, str(quote.carrier_id)) + int(quote.fee) < int(plan.wages): return {"ok": false, "error": "SHIPPING_WAGES_REQUIRED"}
	if int(plan.hours) < int(quote.labor): return {"ok": false, "error": "SHIPPING_WORK_REQUIRED"}
	if buyer != str(quote.carrier_id) and not _pay(s, buyer, str(quote.carrier_id), int(quote.fee), "실제 운송 서비스", month): return {"ok": false, "error": "SHIPPING_PAYMENT_FAILED"}
	if not _work(s, str(quote.carrier_id), int(quote.labor), month, "화물 운송 노동"): return {"ok": false, "error": "SHIPPING_WORK_FAILED"}
	_change(s, str(quote.carrier_id), str(quote.carrier_city), "fuel", -int(quote.fuel), "운송 연료 투입", month, "logistics_input")
	for edge_id in quote.path:
		var edge: Dictionary = _find(s.industry.routes, str(edge_id))
		edge.used = int(edge.used) + int(quote.cargo_units)
	_event(s, "logistics_service", "경로 용량·연료·인력과 납기 사용", month, {"actor_id": buyer, "city_id": from_city, "destination_city_id": to_city, "product_id": product_id, "quantity": quantity, "amount": quote.fee, "carrier_id": quote.carrier_id, "route_ids": quote.path, "eta": quote.eta})
	return {"ok": true, "error": "", "quote": quote}

static func transfer_age(s: Dictionary, seller: String, buyer: String, product: String, from_city: String, to_city: String, target_lot_id: String = "") -> void:
	var age: Dictionary = inventory_age(s, seller, from_city, product, 0)
	if int(age.get("expires_month", 0)) <= 0: return
	var targets: Array = []
	if not target_lot_id.is_empty():
		var item: Dictionary = _find(s.get("inventory", []), target_lot_id)
		if not item.is_empty(): targets.append(item)
	else:
		for item in s.get("inventory", []):
			if item.owner_id == buyer and item.product_id == product and item.city_id == to_city and item.location != "shipment" and int(item.quantity) > 0: targets.append(item)
	for destination in targets:
		var previous: int = int(destination.get("expires_month", 0))
		destination.expires_month = int(age.expires_month) if previous <= 0 else mini(previous, int(age.expires_month))
		destination.born_month = mini(int(destination.get("born_month", age.born_month)), int(age.born_month))

static func storage_capacity(s: Dictionary, owner: String, city: String) -> int:
	var total: int = 0
	for facility in s.get("facilities", []):
		if facility.get("company_id") == owner and facility.get("city_id") == city and facility.get("status") in ["operational", "ready"]: total += int(_p().onsite_storage)
	for warehouse in s.get("industry", {}).get("warehouses", []):
		if warehouse.owner_id == owner and warehouse.city_id == city and int(warehouse.get("operational_month", 0)) <= int(s.get("planning_month", 1)): total += int(warehouse.capacity)
	return total

static func storage_used(s: Dictionary, owner: String, city: String) -> int:
	var total: int = 0
	for lot in s.get("inventory", []):
		if lot.owner_id == owner and lot.city_id == city and lot.location != "shipment":
			var product: Dictionary = _find(s.get("products", []), str(lot.product_id))
			total += int(lot.quantity) * maxi(1, int(product.get("cargo_size", 1)))
	return total

static func output_capacity(s: Dictionary, facility: Dictionary, recipe: Dictionary) -> int:
	var units: int = 0
	for product in recipe.get("outputs", {}):
		if not is_service(s, product): units += int(recipe.outputs[product]) * maxi(1, int(_find(s.products, product).get("cargo_size", 1)))
	if units <= 0: return int(facility.get("capacity", 0))
	return maxi(0, int((storage_capacity(s, str(facility.company_id), str(facility.city_id)) - storage_used(s, str(facility.company_id), str(facility.city_id))) / units))

static func _licensed_seats(s: Dictionary, software_id: String) -> int:
	var total: int = 0
	for license in s.get("industry", {}).get("licenses", []):
		if license.software_id == software_id and license.status == "active": total += int(license.seats)
	return total

static func _online_capacity(s: Dictionary, software: Dictionary) -> int:
	return available_quantity(s, str(software.owner_id), "server", str(software.city_id)) * int(_p().server_seats)

static func before_turn(s: Dictionary, month: int) -> void:
	if not s.get("industry") is Dictionary: initialize(s)
	if int(s.industry.prepared_month) == month: return
	for capacity in s.industry.service_capacity:
		var unused: int = int(capacity.capacity) - int(capacity.used)
		if unused > 0: _event(s, "service_expiry", "미사용 기간별 제공능력 소멸", month, {"actor_id": capacity.owner_id, "city_id": capacity.city_id, "product_id": capacity.product_id, "quantity": unused})
	for entitlement in s.industry.service_entitlements:
		var unused: int = int(entitlement.quantity) - int(entitlement.used)
		if unused > 0: _event(s, "service_right_expiry", "지난달 미사용 이용권 소멸", month, {"actor_id": entitlement.owner_id, "city_id": entitlement.city_id, "product_id": entitlement.product_id, "quantity": unused})
	s.industry.service_capacity = []
	s.industry.service_entitlements = []
	for route in s.industry.routes: route.used = 0
	s.industry.prepared_month = month
	s.industry.equipment_hours = {}
	_spoil_inventory(s, month)
	for project in s.industry.agriculture:
		if project.status == "growing" and int(project.ready_month) <= month:
			var finance: Variant = _finance(s)
			if finance != null and finance.has_method("complete_growth"):
				var total: Dictionary = {}
				for product in _keys(project.outputs): total[product] = int(project.outputs[product]) * int(project.batches)
				finance.complete_growth(s, str(project.owner_id), str(project.id), total, month)
			for product in _keys(project.outputs):
				var made: Dictionary = _change(s, str(project.owner_id), str(project.city_id), product, int(project.outputs[product]) * int(project.batches), "완료된 생육·수확", month, "production_output")
				for lot_id in made.get("lot_ids", []):
					var lot: Dictionary = _find(s.inventory, str(lot_id))
					lot.quality = mini(int(lot.get("quality", project.get("quality", _p().quality_base))), int(project.get("quality", _p().quality_base)))
			_release_growth_reservations(s, project)
			project.status = "harvested"
			project.reason = "최소 생육기간과 선투입을 충족한 수확"
			_event(s, "biological_harvest", project.reason, month, {"actor_id": project.owner_id, "city_id": project.city_id, "project_id": project.id, "facility_id": project.facility_id})
	for adoption in s.industry.adoptions:
		if adoption.status == "installing" and int(adoption.work) >= int(adoption.required_work) and int(adoption.ready_month) <= month:
			var facility: Dictionary = _find(s.get("facilities", []), str(adoption.facility_id))
			if facility.is_empty() or facility.company_id != adoption.owner_id:
				adoption.status = "cancelled"
				continue
			if not facility.has("tech_levels"): facility.tech_levels = {}
			facility.tech_levels[adoption.topic] = int(adoption.level)
			adoption.status = "installed"
			_event(s, "technology_adopted", "설치·교육 기간과 비용을 거친 기술 적용", month, {"actor_id": adoption.owner_id, "city_id": adoption.city_id, "facility_id": facility.id, "topic": adoption.topic, "level": adoption.level})

static func monthly(s: Dictionary, month: int) -> void:
	if not s.get("industry") is Dictionary: initialize(s)
	if int(s.industry.get("processed_month", -1)) == month: return
	s.industry.processed_month = month
	register_closure_claims(s, month)
	_pay_refunds(s, month)
	for project in s.industry.research:
		if project.status != "researching" or _find(s.get("companies", []), str(project.owner_id)).get("status", "active") != "active": continue
		var owner: String = str(project.owner_id)
		var hours: int = mini(int(_p().research_monthly_work), int(project.required_work) - int(project.work))
		var plan: Dictionary = _work_plan(s, owner, hours, "research")
		if hours > 0 and (not plan.ok or int(plan.wages) > int(project.budget)):
			project.reason = "연구 예산 또는 숙련 인력 시간 부족"
			_event(s, "research_delay", project.reason, month, {"actor_id": owner, "city_id": project.city_id, "project_id": project.id})
			continue
		if hours > 0 and _work(s, owner, hours, month, "연구 수행 임금"):
			project.work = int(project.work) + hours
			project.spent = int(project.spent) + int(plan.wages)
			project.reason = "연구 진행"
		if int(project.work) >= int(project.required_work) and month - int(project.start_month) + 1 >= int(project.min_months):
			var id: String = "tech:%s:%s" % [owner, project.topic]
			var tech: Dictionary = _find(s.industry.technology, id)
			if tech.is_empty():
				tech = {"id": id, "owner_id": owner, "topic": project.topic, "level": 0}
				s.industry.technology.append(tech)
			tech.level = maxi(int(tech.level), int(project.level))
			project.status = "completed"
			project.reason = "실제 연구 작업 완료, 시설 적용은 별도"
			_event(s, "research_complete", project.reason, month, {"actor_id": owner, "city_id": project.city_id, "project_id": project.id, "topic": project.topic, "level": project.level})
	for adoption in s.industry.adoptions:
		if adoption.status != "installing": continue
		var hours: int = maxi(0, int(adoption.required_work) - int(adoption.work))
		if hours > 0 and _work(s, str(adoption.owner_id), hours, month, "설비 개조·교육 임금"): adoption.work = int(adoption.work) + hours
	for software in s.industry.software:
		if software.status != "developing" or _find(s.get("companies", []), str(software.owner_id)).get("status", "active") != "active": continue
		var hours: int = mini(int(_p().software_monthly_work), int(software.required_work) - int(software.work))
		if available_quantity(s, str(software.owner_id), "computer", str(software.city_id)) < 1:
			software.reason = "개발 장비 없음"
			continue
		if hours > 0 and _work(s, str(software.owner_id), hours, month, "소프트웨어 개발·시험 임금"):
			software.work = int(software.work) + hours
			software.reason = "개발" if int(software.work) < int(software.required_work) * 2 / 3 else "호환성 시험"
		else: software.reason = "개발 인력 시간·운전자금 부족"
		if int(software.work) >= int(software.required_work) and month - int(software.start_month) + 1 >= int(software.min_months):
			software.status = "ready"
			software.reason = "개발·시험 완료, 출시 결정 대기"
			_event(s, "software_ready", software.reason, month, {"actor_id": software.owner_id, "city_id": software.city_id, "software_id": software.id})
	for repair in s.industry.repairs:
		if repair.status != "repairing": continue
		var hours: int = mini(int(_p().repair_monthly_work), int(repair.required_work) - int(repair.work))
		if hours > 0 and _work(s, str(repair.owner_id), hours, month, "피해 시설 수리 노동"): repair.work = int(repair.work) + hours
		if int(repair.work) >= int(repair.required_work) and month - int(repair.start_month) >= int(_p().repair_min_months):
			var facility: Dictionary = _find(s.facilities, str(repair.facility_id))
			if not facility.is_empty() and facility.company_id == repair.owner_id:
				facility.status = "operational"
				facility.damage_percent = 0
				facility.repair_required = false
				repair.status = "completed"
				_event(s, "facility_repaired", "실제 부품·노동·기간을 사용한 시설 복구", month, {"actor_id": repair.owner_id, "city_id": repair.city_id, "facility_id": facility.id})
	_process_subscriptions(s, month)

static func _process_subscriptions(s: Dictionary, month: int) -> void:
	for subscription in s.industry.subscriptions:
		if int(subscription.next_due) > month or subscription.status in ["cancelled", "terminated"]: continue
		var license: Dictionary = _find(s.industry.licenses, str(subscription.license_id))
		var software: Dictionary = _find(s.industry.software, str(subscription.software_id))
		if not subscription.auto_renew or software.is_empty() or software.status != "released" or _find(s.get("companies", []), str(software.get("owner_id", ""))).get("status", "active") != "active":
			subscription.status = "cancelled"
			license.status = "expired"
			continue
		if _cash(s, str(subscription.buyer_id)) < int(subscription.monthly_price):
			subscription.status = "past_due"
			license.status = "expired"
			_event(s, "subscription_past_due", "구독 갱신 예산 부족으로 이용권 만료", month, {"actor_id": subscription.buyer_id, "city_id": _city(s, str(subscription.buyer_id)), "subscription_id": subscription.id})
			continue
		if _digital_charge(s, str(subscription.buyer_id), software, int(subscription.monthly_price), month, "월별 구독 갱신"):
			subscription.status = "active"
			subscription.next_due = month + 1
			license.status = "active"
			license.expires_month = month + 1
			license.paid_month = month
			license.period_amount = int(subscription.monthly_price)
			license.earned_amount = 0 if software.kind == "online" else int(subscription.monthly_price)
			if software.kind != "online": _digital_output(s, software, str(subscription.buyer_id), int(subscription.seats), int(subscription.monthly_price), month, "갱신된 다운로드 사용권 인도")

static func _digital_charge(s: Dictionary, buyer: String, software: Dictionary, amount: int, month: int, reason: String) -> bool:
	if software.kind != "online": return _pay(s, buyer, str(software.owner_id), amount, reason, month)
	var seller: String = str(software.owner_id)
	Ledger.open_account(s, buyer + ":industry-online-prepaid", buyer, "asset")
	Ledger.open_account(s, seller + ":industry-online-advance", seller, "liability")
	var result: Dictionary = Ledger.post(s, "online-prepayment-%d-%d" % [month, s.journal.size()], [
		{"account":buyer + ":cash","debit":0,"credit":amount},
		{"account":buyer + ":industry-online-prepaid","debit":amount,"credit":0},
		{"account":seller + ":cash","debit":amount,"credit":0},
		{"account":seller + ":industry-online-advance","debit":0,"credit":amount}
	], "온라인 이용기간 선불금·이행의무", month)
	if result.get("ok", false): _event(s, "payment", reason, month, {"actor_id":buyer,"payer_id":buyer,"payee_id":seller,"city_id":_city(s,buyer),"amount":amount,"software_id":software.id})
	return bool(result.get("ok", false))

static func _digital_output(s: Dictionary, software: Dictionary, buyer: String, quantity: int, amount: int, month: int, reason: String) -> void:
	if amount <= 0 or quantity <= 0: return
	_event(s, "gdp_output", reason, month, {"sector":"digital","valuation":"transaction_price","actor_id":software.owner_id,"seller_id":software.owner_id,"buyer_id":buyer,"city_id":software.city_id,"buyer_city_id":_city(s,buyer),"product_id":"software_license","software_id":software.id,"category":software.get("category","business"),"quantity":quantity,"amount":amount,"currency":"CR"})

static func _digital_input(s: Dictionary, owner: String, city: String, product: String, quantity: int, month: int) -> void:
	if quantity <= 0: return
	var price: int = int(_find(s.products, product).get("price", 0))
	_event(s, "gdp_input", "실제 소모한 디지털 운영 서비스", month, {"sector":"digital","valuation":"content_base_price","actor_id":owner,"city_id":city,"product_id":product,"quantity":quantity,"amount":quantity * price,"currency":"CR"})

static func deliver_digital(s: Dictionary, month: int) -> void:
	if not s.get("industry") is Dictionary or int(s.industry.get("delivered_month", -1)) == month: return
	s.industry.delivered_month = month
	_process_online(s, month)
	for software in s.industry.software:
		if software.kind != "online" or software.status != "released": continue
		var licenses: Array = []
		var demand: int = 0
		for license in s.industry.licenses:
			if license.software_id == software.id and license.status == "active" and int(license.get("expires_month", 0)) > month:
				licenses.append(license)
				demand += int(license.seats)
		var remaining: int = int(software.active_seats)
		licenses.sort_custom(func(a,b): return (str(month)+str(a.buyer_id)).sha256_text()<(str(month)+str(b.buyer_id)).sha256_text())
		for license in licenses:
			var served: int = mini(int(license.seats), remaining)
			remaining -= served
			license.served_month = month
			license.served_seats = served
			if int(license.get("paid_month", -1)) != month: continue
			var earned: int = mini(int(license.get("period_amount", 0)), served * int(software.price))
			if earned > 0:
				var posted: Dictionary = Ledger.post(s, "online-earned-%d-%d" % [month, s.journal.size()], [
					{"account":str(software.owner_id)+":industry-online-advance","debit":earned,"credit":0},
					{"account":str(software.owner_id)+":revenue","debit":0,"credit":earned},
					{"account":str(license.buyer_id)+":expense","debit":earned,"credit":0},
					{"account":str(license.buyer_id)+":industry-online-prepaid","debit":0,"credit":earned}
				], "서버·전력·통신을 확보해 제공한 온라인 이용기간", month)
				if posted.get("ok", false):
					license.earned_amount = earned
					_digital_output(s, software, str(license.buyer_id), served, earned, month, "실제 온라인 이용기간 제공")
			var unprovided: int = int(license.get("period_amount", 0)) - int(license.get("earned_amount", 0))
			if unprovided > 0: _create_online_claim(s, license, "", unprovided, month)
	_process_advertising(s, month)
	_pay_refunds(s, month)

static func _process_online(s: Dictionary, month: int) -> void:
	for software in s.industry.software:
		if software.status != "released" or software.kind != "online": continue
		if _find(s.get("companies", []), str(software.owner_id)).get("status", "active") != "active":
			software.active_seats = 0
			continue
		var owner: String = str(software.owner_id)
		var city: String = str(software.city_id)
		var energy: int = int(_p().software_maintenance_energy)
		var network: int = int(_p().software_maintenance_telecom)
		if service_available(s, owner, "energy", city) < energy: purchase_service(s, owner, city, "energy", energy - service_available(s, owner, "energy", city), month)
		if service_available(s, owner, "telecom_access", city) < network: purchase_service(s, owner, city, "telecom_access", network - service_available(s, owner, "telecom_access", city), month)
		var available: bool = service_available(s, owner, "energy", city) >= energy and service_available(s, owner, "telecom_access", city) >= network
		software.active_seats = 0
		if available:
			consume_service(s, owner, city, "energy", energy, month)
			consume_service(s, owner, city, "telecom_access", network, month)
			_digital_input(s, owner, city, "energy", energy, month)
			_digital_input(s, owner, city, "telecom_access", network, month)
			software.active_seats = mini(_licensed_seats(s, str(software.id)), _online_capacity(s, software))
		else:
			_event(s, "online_outage", "서버·전력·통신 처리능력 또는 운영 예산 부족", month, {"actor_id": owner, "city_id": city, "software_id": software.id})
		s.industry.software_usage.append({"software_id": software.id, "month": month, "paid_seats": _licensed_seats(s, str(software.id)), "active_seats": software.active_seats, "server_capacity": _online_capacity(s, software)})

static func _process_advertising(s: Dictionary, month: int) -> void:
	var slots_used: Dictionary = {}
	for campaign in s.industry.advertising:
		if campaign.status != "active": continue
		var software: Dictionary = _find(s.industry.software, str(campaign.software_id))
		if software.is_empty() or software.status != "released":
			campaign.status = "paused"
			continue
		var available: int = maxi(0, int(software.active_seats) * int(_p().ads_per_seat) - int(slots_used.get(software.id, 0)))
		var remaining: int = int(campaign.budget) - int(campaign.spent)
		var impressions: int = mini(available, int(mini(remaining, _cash(s, str(campaign.owner_id))) / int(_p().ad_unit_price)))
		if impressions > 0 and _pay(s, str(campaign.owner_id), str(software.owner_id), impressions * int(_p().ad_unit_price), "실제 노출된 온라인 광고", month):
			campaign.impressions = int(campaign.impressions) + impressions
			campaign.spent = int(campaign.spent) + impressions * int(_p().ad_unit_price)
			slots_used[software.id] = int(slots_used.get(software.id, 0)) + impressions
			_digital_output(s, software, str(campaign.owner_id), impressions, impressions * int(_p().ad_unit_price), month, "실제 광고 노출 인도")
			_event(s, "advertising_delivery", "실제 이용 좌석·광고주 예산에서 발생한 광고", month, {"actor_id": campaign.owner_id, "city_id": campaign.city_id, "software_id": software.id, "quantity": impressions, "amount": impressions * int(_p().ad_unit_price)})
		if int(campaign.spent) >= int(campaign.budget): campaign.status = "completed"

static func after_turn(s: Dictionary, month: int) -> void:
	deliver_digital(s, month)
	if int(s.get("industry", {}).get("closed_month", -1)) == month: return
	_maintain_cold_storage(s, month)
	_spoil_inventory(s, month)
	_wear_durables(s, month)
	for city in s.get("cities", []):
		var opening: int = int(s.industry.resource_opening.get(city.id, {}).get("fish_stock", 0))
		var current: int = resource_available(s, str(city.id), "fish_stock")
		var growth: int = mini(int(_p().fish_regrowth), maxi(0, opening - current))
		if growth > 0:
			city.resources.fish_stock = current + growth
			_event(s, "resource_regrowth", "수산 자원의 제한된 자연 재생", month, {"city_id": city.id, "resource": "fish_stock", "quantity": growth})
	s.industry.closed_month = month

static func ai_commands(s: Dictionary) -> Array:
	var result: Array = []
	var month: int = int(s.get("planning_month", 1))
	var companies: Array = s.get("companies", []).duplicate()
	companies.sort_custom(func(a, b): return str(a.id) < str(b.id))
	var public_software: Array = []
	for item in s.get("industry", {}).get("software", []):
		if item.status == "released" and _find(s.companies, str(item.owner_id)).get("status", "active") == "active": public_software.append(item)
	public_software.sort_custom(func(a, b):
		var aa: float = float(a.get("quality", _p().software_base_quality)) / maxi(1, int(a.price))
		var bb: float = float(b.get("quality", _p().software_base_quality)) / maxi(1, int(b.price))
		return aa > bb if aa != bb else str(a.id) < str(b.id)
	)
	for company in companies:
		if company.get("status", "active") != "active" or company.director_id == s.get("player_id", "") or str(company.director_id).is_empty(): continue
		var owner: String = str(company.id)
		var payload: Dictionary = {}
		var type: String = ""
		for software in s.get("industry", {}).get("software", []):
			if software.owner_id == owner and software.status == "ready":
				type = "industry_release"
				payload = {"software_id": software.id}
				break
		if type.is_empty() and month % int(_p().optional_consumption_period) == 0 and _cash(s, owner) > int(_p().ai_research_reserve):
			for facility in s.get("facilities", []):
				if facility.company_id != owner: continue
				for topic in _data().tech_topics:
					if _tech(s, owner, str(topic.id)) > int(facility.get("tech_levels", {}).get(topic.id, 0)):
						type = "industry_adopt"
						payload = {"topic": topic.id, "facility_id": facility.id}
						break
				if not type.is_empty(): break
		if type.is_empty() and month % int(_p().optional_consumption_period) == 0 and not _business_license(s, owner) and available_quantity(s, owner, "computer", str(company.city_id)) > 0:
			for software in public_software:
				if software.owner_id != owner and software.get("category", "business") in ["business", "system", "development"] and int(software.price) <= maxi(0, _cash(s, owner) - int(_p().ai_research_reserve)):
					type = "industry_subscribe" if software.kind == "online" else "industry_license"
					payload = {"software_id": software.id, "seats": 1, "platform": software.platform}
					break
		if type.is_empty() and month % int(_p().ai_action_period) == 0 and _cash(s, owner) > int(_p().ai_research_reserve) and available_quantity(s, owner, "computer", str(company.city_id)) > 0:
			if _work_plan(s, owner, int(_p().research_monthly_work), "research").hours > 0:
				type = "industry_research"
				payload = {"topic": "process_efficiency", "monthly_budget": int(_p().research_monthly_budget)}
			if _work_plan(s, owner, int(_p().software_monthly_work), "software").hours > 0 and _cash(s, owner) > int(_p().ai_software_reserve):
				var own_count: int = 0
				var developing: bool = false
				for software in s.industry.software:
					if software.owner_id != owner: continue
					own_count += 1
					if software.status in ["developing", "ready"]: developing = true
				if not developing:
					var category: Dictionary = _data().digital_categories[own_count % _data().digital_categories.size()]
					type = "industry_software"
					payload = {"name": str(company.name) + " " + str(category.name), "category": category.id, "platform": "open-platform", "kind": "online" if available_quantity(s, owner, "server", str(company.city_id)) > 0 and own_count % 2 == 1 else "download", "price": int(_p().software_default_price)}
		if type.is_empty(): continue
		var command: Dictionary = {"id": "ai-industry-%d-%s" % [month, owner], "actor_id": company.director_id, "acting_company_id": owner, "expected_version": int(s.get("version", 0)), "type": type, "payload": payload}
		if validate_command(s, command).is_empty(): result.append(command)
	if month % int(_p().ai_action_period) == 0:
		for person in s.get("people", []):
			if person.id == s.get("player_id", "") or _cash(s, str(person.id)) <= int(_p().digital_food_reserve): continue
			var has_right: bool = false
			for license in s.industry.licenses:
				if license.buyer_id == person.id and license.status == "active": has_right = true
			if has_right: continue
			for software in public_software:
				if software.get("category", "business") != person.get("digital_preference", "business") or int(software.price) > _cash(s, str(person.id)) - int(_p().digital_food_reserve): continue
				var command: Dictionary = {"id": "ai-digital-%d-%s" % [month, person.id], "actor_id": person.id, "acting_company_id": "", "expected_version": int(s.get("version", 0)), "type": "industry_subscribe" if software.kind == "online" else "industry_license", "payload": {"software_id": software.id, "seats": 1, "platform": software.platform}}
				if validate_command(s, command).is_empty(): result.append(command)
				break
	return result

static func summary(s: Dictionary, owner: String) -> Dictionary:
	var result: Dictionary = {"research_active": 0, "software_released": 0, "owned_licenses": 0, "active_seats": 0, "growing_projects": 0, "storage_capacity": storage_capacity(s, owner, _city(s, owner)), "storage_used": storage_used(s, owner, _city(s, owner)), "route_count": s.get("industry", {}).get("routes", []).size(), "technology": [], "services": []}
	for project in s.get("industry", {}).get("research", []):
		if project.owner_id == owner and project.status == "researching": result.research_active += 1
	for software in s.get("industry", {}).get("software", []):
		if software.owner_id == owner and software.status == "released":
			result.software_released += 1
			result.active_seats += int(software.active_seats)
	for license in s.get("industry", {}).get("licenses", []):
		if license.buyer_id == owner and license.status == "active": result.owned_licenses += int(license.seats)
	for project in s.get("industry", {}).get("agriculture", []):
		if project.owner_id == owner and project.status == "growing": result.growing_projects += 1
	for tech in s.get("industry", {}).get("technology", []):
		if tech.owner_id == owner: result.technology.append(tech.duplicate(true))
	for capacity in s.get("industry", {}).get("service_capacity", []):
		if capacity.owner_id == owner: result.services.append(capacity.duplicate(true))
	return result

static func validate(s: Dictionary) -> Array:
	var errors: Array = []
	if not s.get("industry") is Dictionary: return ["INDUSTRY_STATE_MISSING"]
	for name in ["research", "technology", "adoptions", "repairs", "software", "licenses", "subscriptions", "advertising", "agriculture", "service_capacity", "service_entitlements", "routes", "warehouses", "software_usage", "durables", "refund_claims"]:
		if not s.industry.get(name) is Array:
			errors.append("INDUSTRY_STATE_ARRAY:" + name)
			continue
		var ids: Dictionary = {}
		for row in s.industry[name]:
			if not row is Dictionary:
				errors.append("INDUSTRY_STATE_ROW:" + name)
				continue
			if row.has("id"):
				if str(row.id).is_empty() or ids.has(row.id): errors.append("INDUSTRY_DUPLICATE_ID:" + name)
				ids[row.id] = true
	if not errors.is_empty(): return errors
	for capacity in s.industry.service_capacity:
		if not _whole(capacity.get("capacity")) or not _whole(capacity.get("used")) or int(capacity.used) < 0 or int(capacity.used) > int(capacity.capacity): errors.append("SERVICE_CAPACITY_EXCEEDED")
		if not is_service(s, str(capacity.get("product_id", ""))): errors.append("SERVICE_PRODUCT_INVALID")
	for entitlement in s.industry.service_entitlements:
		if not _whole(entitlement.get("quantity")) or not _whole(entitlement.get("used")) or int(entitlement.used) < 0 or int(entitlement.used) > int(entitlement.quantity): errors.append("SERVICE_RIGHT_EXCEEDED")
	for route in s.industry.routes:
		if not _whole(route.get("capacity")) or not _whole(route.get("used")) or int(route.used) < 0 or int(route.used) > int(route.capacity): errors.append("LOGISTICS_CAPACITY_EXCEEDED")
		if _find(s.get("cities", []), str(route.from_city)).is_empty() or _find(s.get("cities", []), str(route.to_city)).is_empty(): errors.append("LOGISTICS_CITY_INVALID")
	for project in s.industry.research:
		if not _whole(project.get("work")) or int(project.work) < 0 or int(project.work) > int(project.required_work): errors.append("RESEARCH_WORK_INVALID")
	for software in s.industry.software:
		if not _whole(software.get("work")) or int(software.work) < 0 or int(software.work) > int(software.required_work): errors.append("SOFTWARE_WORK_INVALID")
		if not _positive(software.get("price")): errors.append("SOFTWARE_PRICE_INVALID")
	for license in s.industry.licenses:
		if not _positive(license.get("seats")) or _find(s.industry.software, str(license.software_id)).is_empty(): errors.append("SOFTWARE_RIGHT_INVALID")
	for campaign in s.industry.advertising:
		if not _whole(campaign.get("spent")) or int(campaign.spent) < 0 or int(campaign.spent) > int(campaign.budget): errors.append("ADVERTISING_BUDGET_EXCEEDED")
	for claim in s.industry.refund_claims:
		var remaining: int = int(claim.get("amount", 0)) - int(claim.get("paid", 0))
		if remaining < 0 or int(claim.get("paid", 0)) < 0: errors.append("REFUND_AMOUNT_INVALID")
		if Ledger.balance(s, str(claim.get("liability_account", ""))) != -remaining or Ledger.balance(s, str(claim.get("receivable_account", ""))) != remaining: errors.append("REFUND_LEDGER_MISMATCH")
	for lot in s.get("inventory", []):
		if int(lot.get("quantity", 0)) > 0 and (is_service(s, str(lot.get("product_id", ""))) or _find(s.get("products", []), str(lot.get("product_id", ""))).get("kind", "") == "digital"): errors.append("NONPHYSICAL_PRODUCT_IN_INVENTORY")
	for city in s.get("cities", []):
		for resource in _data().get("resources", []):
			if not _whole(city.get("resources", {}).get(resource)) or int(city.resources[resource]) < 0: errors.append("INDUSTRY_RESOURCE_INVALID:" + str(resource))
	return errors

static func _release_growth_reservations(s: Dictionary, project: Dictionary) -> void:
	for reservation in project.get("reservations", []):
		var item: Dictionary = _find(s.get("inventory", []), str(reservation.lot_id))
		if not item.is_empty(): item.reserved = maxi(0, int(item.get("reserved", 0)) - int(reservation.quantity))
	project.reservations = []

static func receive_due_shipments(s: Dictionary, month: int) -> void:
	for shipment in s.get("shipments", []):
		if shipment.get("status") != "transit" or int(shipment.get("eta", month + 1)) > month: continue
		var blocked: bool = false
		for route_id in shipment.get("route_ids", []):
			var route: Dictionary = _find(s.get("industry", {}).get("routes", []), str(route_id))
			if not route.is_empty() and route.get("blocked", false): blocked = true
		if blocked:
			shipment.eta = month + 1
			_event(s, "shipment_delay", "운송 중 항로가 차단되어 유한 화물 보존", month, {"actor_id": shipment.owner_id, "city_id": shipment.to_city_id, "shipment_id": shipment.id})
			continue
		var source: Dictionary = _find(s.get("inventory", []), str(shipment.lot_id))
		if source.is_empty(): continue
		var quantity: int = int(source.quantity)
		var expired: bool = int(source.get("expires_month", 0)) > 0 and int(source.expires_month) <= month
		if expired:
			var finance: Variant = _finance(s)
			if finance != null and finance.has_method("inventory_flow"): finance.inventory_flow(s, str(shipment.owner_id), str(shipment.product_id), -quantity, "운송 중 유통기간 경과", month)
		source.quantity = 0
		_event(s, "spoilage" if expired else "shipment_arrival", "운송 중 유통기간 경과" if expired else "실물 화물 도착 인수", month, {"actor_id": shipment.owner_id, "city_id": shipment.to_city_id, "product_id": shipment.product_id, "quantity": quantity, "lot_id": source.id, "inventory_delta": -quantity, "shipment_id": shipment.id})
		shipment.status = "spoiled" if expired else "delivered"
		if expired: continue
		var received: Dictionary = _change(s, str(shipment.owner_id), str(shipment.to_city_id), str(shipment.product_id), quantity, "도시 귀속 이전·화물 도착", month, "shipment_arrival")
		if int(source.get("expires_month", 0)) > 0:
			for lot_id in received.get("lot_ids", []):
				var target: Dictionary = _find(s.inventory, str(lot_id))
				target.expires_month = mini(int(target.get("expires_month", source.expires_month)), int(source.expires_month))
				target.quality = mini(int(target.get("quality", source.get("quality", _p().quality_base))), int(source.get("quality", _p().quality_base)))
				target.born_month = mini(int(target.get("born_month", source.get("born_month", 0))), int(source.get("born_month", 0)))

static func household_needs(s: Dictionary, person: Dictionary, month: int) -> Dictionary:
	var needs: Dictionary = {}
	for rule in _data().get("household_needs", []):
		if month % int(rule.period) != 0: continue
		var product: String = str(rule.product_id)
		if rule.mode == "durable" and available_quantity(s, str(person.id), product, str(person.city_id)) > 0: continue
		needs[product] = int(rule.quantity)
	return needs

static func household_use(s: Dictionary, person: Dictionary, product: String, quantity: int, month: int) -> bool:
	if quantity <= 0: return true
	var definition: Dictionary = _find(s.get("products", []), product)
	if is_service(s, product): return consume_service(s, str(person.id), str(person.city_id), product, quantity, month)
	if available_quantity(s, str(person.id), product, str(person.city_id)) < quantity: return false
	if int(definition.get("durable_months", 0)) > 0:
		var id: String = "durable:%s:%s" % [person.id, product]
		if _find(s.industry.durables, id).is_empty(): s.industry.durables.append({"id": id, "owner_id": person.id, "city_id": person.city_id, "product_id": product, "quantity": quantity, "acquired_month": month, "replacement_month": month + int(definition.durable_months), "status": "in_use"})
		return true
	_change(s, str(person.id), str(person.city_id), product, -quantity, "생활 소모품 사용", month, "consumption")
	return true

static func expire_inventory(s: Dictionary, month: int) -> void:
	_spoil_inventory(s, month)

static func _spoil_inventory(s: Dictionary, month: int) -> void:
	for lot in s.get("inventory", []).duplicate():
		if lot.get("location") == "shipment" or int(lot.get("quantity", 0)) <= 0: continue
		var product: Dictionary = _find(s.get("products", []), str(lot.product_id))
		var life: int = int(product.get("shelf_life_months", 0))
		if life <= 0: continue
		if not lot.has("expires_month"):
			lot.born_month = 0 if int(s.get("last_resolved_month", 0)) == 0 else month
			lot.expires_month = int(lot.born_month) + life
		if int(lot.expires_month) <= month:
			var quantity: int = maxi(0, int(lot.quantity) - int(lot.get("reserved", 0)))
			if quantity > 0:
				_change(s, str(lot.owner_id), str(lot.city_id), str(lot.product_id), -quantity, "유통기간 경과·부패", month, "spoilage")
				var waste: int = int(quantity * int(_p().spoilage_waste_percent) / 100)
				if waste > 0: _change(s, str(lot.owner_id), str(lot.city_id), "organic_waste", waste, "회수 가능한 유기성 폐기물", month, "waste_recovery")

static func _reserve_stock(s: Dictionary, owner: String, city: String, product: String, quantity: int) -> Array:
	if available_quantity(s, owner, product, city) < quantity: return []
	var reservations: Array = []
	var remaining: int = quantity
	for lot in s.get("inventory", []):
		if lot.owner_id != owner or lot.city_id != city or lot.product_id != product or lot.location == "shipment": continue
		var take: int = mini(remaining, maxi(0, int(lot.quantity) - int(lot.get("reserved", 0))))
		if take <= 0: continue
		lot.reserved = int(lot.get("reserved", 0)) + take
		reservations.append({"lot_id": lot.id, "quantity": take})
		remaining -= take
		if remaining == 0: break
	return reservations

static func inventory_age(s: Dictionary, owner: String, city: String, product: String, quantity: int = 0) -> Dictionary:
	var candidates: Array = []
	for item in s.get("inventory", []):
		if item.owner_id == owner and item.city_id == city and item.product_id == product and item.location != "shipment" and int(item.quantity) - int(item.get("reserved", 0)) > 0: candidates.append(item)
	candidates.sort_custom(func(a, b):
		var aa: int = int(a.get("expires_month", 2147483647))
		var bb: int = int(b.get("expires_month", 2147483647))
		return aa < bb if aa != bb else str(a.id) < str(b.id)
	)
	var result: Dictionary = {"born_month": int(s.get("planning_month", 1)), "expires_month": 0, "unit_cost": 0}
	var remaining: int = quantity
	for item in candidates:
		var expires: int = int(item.get("cold_original_expiry", item.get("expires_month", 0)))
		result.born_month = mini(int(result.born_month), int(item.get("born_month", result.born_month)))
		if expires > 0: result.expires_month = expires if int(result.expires_month) <= 0 else mini(int(result.expires_month), expires)
		result.unit_cost = int(item.get("unit_cost", 0))
		if quantity > 0:
			remaining -= mini(remaining, int(item.quantity) - int(item.get("reserved", 0)))
			if remaining <= 0: break
	return result

static func _maintain_cold_storage(s: Dictionary, month: int) -> void:
	var active: Dictionary = {}
	for warehouse in s.industry.warehouses:
		if not warehouse.refrigerated or int(warehouse.get("operational_month", 0)) > month: continue
		var owner: String = str(warehouse.owner_id)
		var city: String = str(warehouse.city_id)
		var required: int = int(_p().warehouse_cold_extra_energy)
		if service_available(s, owner, "energy", city) < required: purchase_service(s, owner, city, "energy", required - service_available(s, owner, "energy", city), month)
		if service_available(s, owner, "energy", city) < required:
			_event(s, "cold_storage_outage", "냉장 보관 전력·운영 예산 부족", month, {"actor_id": owner, "city_id": city, "warehouse_id": warehouse.id})
			continue
		consume_service(s, owner, city, "energy", required, month)
		active[owner + ":" + city] = true
	for lot in s.get("inventory", []):
		if lot.location == "shipment" or int(lot.quantity) <= 0 or int(lot.get("expires_month", 0)) <= month: continue
		if active.has(str(lot.owner_id) + ":" + str(lot.city_id)):
			if not lot.has("cold_original_expiry"):
				lot.cold_original_expiry = int(lot.expires_month)
				lot.expires_month = int(lot.expires_month) + int(_p().cold_bonus_months)
			lot.cold_maintained_month = month
		elif lot.has("cold_original_expiry"):
			lot.expires_month = mini(int(lot.expires_month), int(lot.cold_original_expiry))

static func _wear_durables(s: Dictionary, month: int) -> void:
	for durable in s.industry.durables:
		if durable.status != "in_use" or int(durable.replacement_month) > month: continue
		var quantity: int = mini(int(durable.quantity), available_quantity(s, str(durable.owner_id), str(durable.product_id), str(durable.city_id)))
		if quantity > 0:
			_change(s, str(durable.owner_id), str(durable.city_id), str(durable.product_id), -quantity, "내구재 수명·마모", month, "durable_wear")
			_change(s, str(durable.owner_id), str(durable.city_id), "recyclable_waste", quantity, "폐내구재의 제한된 회수", month, "waste_recovery")
		durable.status = "worn"

static func _business_license(s: Dictionary, owner: String) -> bool:
	for license in s.get("industry", {}).get("licenses", []):
		if license.buyer_id != owner or license.status != "active": continue
		var software: Dictionary = _find(s.industry.software, str(license.software_id))
		if not software.is_empty() and software.get("category", "business") in ["business", "system", "development"]: return true
	return false

static func _computer_hours(s: Dictionary, owner: String) -> int:
	return maxi(0, available_quantity(s, owner, "computer", _city(s, owner)) * int(_p().computer_hours) - int(s.get("industry", {}).get("equipment_hours", {}).get(owner + ":computer", 0)))

static func planning_reservations(s: Dictionary, c: Dictionary) -> Array:
	var owner: String = _owner(s, c)
	if owner.is_empty() or not c.get("payload") is Dictionary: return []
	var p: Dictionary = c.payload
	var type: String = str(c.get("type", ""))
	var money: int = 0
	var hours: int = 0
	var inputs: Dictionary = {}
	match type:
		"industry_research":
			money = int(_p().research_setup_fee) + int(p.get("monthly_budget", 0))
			hours = int(_p().research_monthly_work)
		"industry_software":
			money = int(_p().software_setup_fee)
			hours = int(_p().software_monthly_work)
		"industry_adopt":
			money = int(_p().adoption_cost)
			hours = int(_p().research_monthly_work)
			inputs = {"tools": 1}
		"industry_license", "industry_subscribe":
			var software: Dictionary = _find(s.get("industry", {}).get("software", []), str(p.get("software_id", "")))
			money = int(software.get("price", 0)) * int(p.get("seats", 0))
		"industry_advertise": money = int(p.get("budget", 0))
		"industry_agriculture":
			var facility: Dictionary = _find(s.get("facilities", []), str(p.get("facility_id", "")))
			var recipe: Dictionary = _find(s.get("recipes", []), str(facility.get("recipe_id", "")))
			hours = int(recipe.get("labor", 0)) * int(p.get("batches", 0))
			for product in recipe.get("inputs", {}):
				if not is_service(s, str(product)): inputs[product] = int(recipe.inputs[product]) * int(p.get("batches", 0))
		"industry_ship":
			var quote: Dictionary = route_quote(s, _city(s, owner), str(p.get("to_city", "")), str(p.get("product_id", "")), int(p.get("quantity", 0)))
			money = int(quote.get("fee", 0))
			inputs[str(p.get("product_id", ""))] = int(p.get("quantity", 0))
		"industry_recycle":
			hours = int(_p().recycle_work) * int(p.get("batches", 0))
			inputs = {"recyclable_waste": int(_p().recycle_units_in) * int(p.get("batches", 0))}
		"industry_warehouse":
			money = int(_p().warehouse_cost)
			hours = int(_p().warehouse_work)
			inputs = {"wood": int(_p().warehouse_wood), "steel": int(_p().warehouse_steel)}
		"industry_repair":
			hours = int(_p().repair_monthly_work)
			inputs = {"tools": int(_p().repair_tools), "machine_parts": int(_p().repair_parts)}
	var result: Array = []
	if hours > 0:
		var skill: String = "research" if type == "industry_research" else ("software" if type == "industry_software" else "")
		var plan: Dictionary = _work_plan(s, owner, hours, skill)
		if type != "industry_research": money += int(plan.wages)
		for assignment in plan.assignments: result.append({"owner_id": str(assignment.person_id), "resource_type": "labor", "quantity": int(assignment.hours)})
	if money > 0: result.append({"owner_id": owner, "resource_type": "cash", "quantity": money})
	for product in _keys(inputs):
		if int(inputs[product]) > 0: result.append({"owner_id": owner, "resource_type": "inventory", "resource_id": product, "quantity": int(inputs[product])})
	return result

static func register_closure_claims(s: Dictionary, month: int) -> void:
	# Must precede liquidation distributions. The delivered perpetual download
	# rights survive a publisher's closure; an unprovided prepaid online period
	# becomes an actual estate liability, never a free replacement payment.
	for subscription in s.get("industry", {}).get("subscriptions", []):
		if subscription.status in ["cancelled", "terminated"]: continue
		var software: Dictionary = _find(s.industry.software, str(subscription.software_id))
		var publisher: Dictionary = _find(s.get("companies", []), str(software.get("owner_id", "")))
		if publisher.is_empty() or publisher.get("status", "active") == "active": continue
		var license: Dictionary = _find(s.industry.licenses, str(subscription.license_id))
		subscription.auto_renew = false
		subscription.status = "terminated"
		if license.is_empty(): continue
		if software.get("kind", "download") != "online" or int(license.get("expires_month", 0)) <= month:
			license.status = "expired"
			continue
		license.status = "unavailable"
		var amount: int = maxi(0, int(license.get("period_amount", subscription.monthly_price)) - int(license.get("earned_amount", 0)))
		_create_online_claim(s, license, str(subscription.id), amount, month)

static func _create_online_claim(s: Dictionary, license: Dictionary, subscription_id: String, amount: int, month: int) -> void:
	if amount <= 0 or int(license.get("refund_claimed_month", -1)) == int(license.get("paid_month", month)): return
	var claim_id: String = _id(s, "refund")
	var creditor: String = str(license.buyer_id)
	var debtor: String = str(license.publisher_id)
	var liability: String = debtor + ":industry-refund:" + claim_id
	var receivable: String = creditor + ":industry-refund:" + claim_id
	Ledger.open_account(s, liability, debtor, "liability")
	Ledger.open_account(s, receivable, creditor, "asset")
	var posted: Dictionary = Ledger.post(s, "industry-refund-claim-" + claim_id, [
		{"account": debtor + ":industry-online-advance", "debit": amount, "credit": 0},
		{"account": liability, "debit": 0, "credit": amount},
		{"account": receivable, "debit": amount, "credit": 0},
		{"account": creditor + ":industry-online-prepaid", "debit": 0, "credit": amount}
	], "미제공 선불 온라인 이용기간의 환급 청구권", month)
	if posted.get("ok", false):
		license.refund_claimed_month = int(license.get("paid_month", month))
		s.industry.refund_claims.append({"id": claim_id, "creditor_id": creditor, "debtor_id": debtor, "subscription_id": subscription_id, "license_id": license.id, "amount": amount, "paid": 0, "status": "pending", "month": month, "liability_account": liability, "receivable_account": receivable})
		_event(s, "subscription_refund_claim", "미제공 선불금의 실제 채권·채무 전환", month, {"actor_id": creditor, "debtor_id": debtor, "city_id": _city(s, creditor), "amount": amount, "claim_id": claim_id})

static func _pay_refunds(s: Dictionary, month: int) -> void:
	for claim in s.get("industry", {}).get("refund_claims", []):
		if claim.status != "pending": continue
		var amount: int = mini(int(claim.amount) - int(claim.paid), _cash(s, str(claim.debtor_id)))
		if amount <= 0: continue
		var result: Dictionary = Ledger.post(s, "industry-refund-pay-%s-%d-%d" % [claim.id, month, s.journal.size()], [
			{"account": claim.liability_account, "debit": amount, "credit": 0},
			{"account": str(claim.debtor_id) + ":cash", "debit": 0, "credit": amount},
			{"account": str(claim.creditor_id) + ":cash", "debit": amount, "credit": 0},
			{"account": claim.receivable_account, "debit": 0, "credit": amount}
		], "구독 환급 청구권의 실제 재산 지급", month)
		if result.get("ok", false):
			claim.paid = int(claim.paid) + amount
			if int(claim.paid) == int(claim.amount): claim.status = "paid"
			_event(s, "refund_payment", "환급은 실제 공급자 잔고에서만 지급", month, {"actor_id": claim.debtor_id, "payee_id": claim.creditor_id, "city_id": _city(s, str(claim.creditor_id)), "amount": amount, "claim_id": claim.id})
