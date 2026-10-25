extends RefCounted
## Experimental, deterministic, offline monthly economy. No wall-clock callbacks.
const Ledger = preload("res://core/ledger.gd")
const RULES_VERSION = "experimental-0.1"
const FIXTURE_PATH: String = "res://data/experimental.json"
static var _fixture_cache: Dictionary = {}
static var _fixture_error: String = ""

static func _fixture() -> Dictionary:
	if not _fixture_cache.is_empty(): return _fixture_cache
	if not _fixture_error.is_empty(): return {}
	if not FileAccess.file_exists(FIXTURE_PATH):
		_fixture_error="EXPERIMENTAL_FIXTURE_MISSING:"+FIXTURE_PATH
		push_error(_fixture_error)
		return {}
	var parser: JSON=JSON.new()
	var parse_error: Error=parser.parse(FileAccess.get_file_as_string(FIXTURE_PATH))
	if parse_error!=OK or typeof(parser.data)!=TYPE_DICTIONARY:
		_fixture_error="EXPERIMENTAL_FIXTURE_PARSE_ERROR:"+parser.get_error_message()
		push_error(_fixture_error)
		return {}
	var candidate: Dictionary=_normalize_fixture(parser.data)
	var errors: Array=validate_fixture(candidate)
	if not errors.is_empty():
		_fixture_error="EXPERIMENTAL_FIXTURE_INVALID:"+str(errors)
		push_error(_fixture_error)
		return {}
	_fixture_cache=candidate
	return _fixture_cache

static func _normalize_fixture(value: Variant) -> Variant:
	if typeof(value)==TYPE_DICTIONARY:
		var result: Dictionary={}
		for key in value: result[str(key)]=_normalize_fixture(value[key])
		return result
	if typeof(value)==TYPE_ARRAY:
		var result: Array=[]
		for item in value: result.append(_normalize_fixture(item))
		return result
	if typeof(value)==TYPE_FLOAT and _whole_number(value): return int(value)
	return value

static func fixture_status() -> Dictionary:
	var data: Dictionary=_fixture()
	return {"ok":not data.is_empty(),"error":_fixture_error,"status":data.get("status","invalid"),"version":data.get("version",0)}

static func validate_fixture(data: Dictionary) -> Array:
	var errors: Array=[]
	for key in ["status","version","rules_version","products","recipes","buildings","parameters"]:
		if not data.has(key): errors.append("MISSING_FIXTURE_FIELD:"+key)
	if not errors.is_empty(): return errors
	if data.status!="experimental" or data.version!=1 or data.rules_version!=RULES_VERSION: errors.append("UNSUPPORTED_EXPERIMENTAL_FIXTURE")
	for key in ["products","recipes","buildings"]:
		if typeof(data[key])!=TYPE_ARRAY or data[key].is_empty(): errors.append("INVALID_FIXTURE_ARRAY:"+key)
	if typeof(data.parameters)!=TYPE_DICTIONARY: errors.append("INVALID_FIXTURE_PARAMETERS")
	if not errors.is_empty(): return errors
	var product_ids: Dictionary={}
	for product in data.products:
		if typeof(product)!=TYPE_DICTIONARY:
			errors.append("INVALID_PRODUCT_OBJECT")
			continue
		for key in ["id","name","unit","industry"]:
			if typeof(product.get(key))!=TYPE_STRING or str(product.get(key,"")).is_empty(): errors.append("INVALID_PRODUCT_TEXT:"+key)
		if not _whole_number(product.get("price")) or int(product.get("price",0))<=0: errors.append("INVALID_PRODUCT_PRICE")
		var id: String=str(product.get("id",""))
		if product_ids.has(id): errors.append("DUPLICATE_PRODUCT_ID")
		product_ids[id]=true
	var recipe_ids: Dictionary={}
	for recipe in data.recipes:
		if typeof(recipe)!=TYPE_DICTIONARY:
			errors.append("INVALID_RECIPE_OBJECT")
			continue
		if typeof(recipe.get("id"))!=TYPE_STRING or str(recipe.get("id","")).is_empty(): errors.append("INVALID_RECIPE_ID")
		if not _whole_number(recipe.get("labor")) or int(recipe.get("labor",0))<=0: errors.append("INVALID_RECIPE_LABOR")
		for key in ["inputs","outputs"]:
			if typeof(recipe.get(key))!=TYPE_DICTIONARY or recipe.get(key,{}).is_empty():
				errors.append("INVALID_RECIPE_VECTOR:"+key)
				continue
			for product_id in recipe[key]:
				if not product_ids.has(product_id): errors.append("UNKNOWN_RECIPE_PRODUCT")
				if not _whole_number(recipe[key][product_id]) or int(recipe[key][product_id])<=0: errors.append("INVALID_RECIPE_QUANTITY")
		if recipe.has("resource") and (recipe.resource not in ["forest","stone"] or not _whole_number(recipe.get("resource_per_batch")) or int(recipe.get("resource_per_batch",0))<=0): errors.append("INVALID_RECIPE_RESOURCE")
		var id: String=str(recipe.get("id",""))
		if recipe_ids.has(id): errors.append("DUPLICATE_RECIPE_ID")
		recipe_ids[id]=true
	var building_ids: Dictionary={}
	for building in data.buildings:
		if typeof(building)!=TYPE_DICTIONARY:
			errors.append("INVALID_BUILDING_OBJECT")
			continue
		for key in ["id","name","recipe_id"]:
			if typeof(building.get(key))!=TYPE_STRING or str(building.get(key,"")).is_empty(): errors.append("INVALID_BUILDING_TEXT:"+key)
		for key in ["capacity","wood","stone","tools","work","min_months","w","h"]:
			if not _whole_number(building.get(key)) or int(building.get(key,0))<=0: errors.append("INVALID_BUILDING_INTEGER:"+key)
		if not recipe_ids.has(building.get("recipe_id")): errors.append("UNKNOWN_BUILDING_RECIPE")
		var id: String=str(building.get("id",""))
		if building_ids.has(id): errors.append("DUPLICATE_BUILDING_ID")
		building_ids[id]=true
	var p: Dictionary=data.parameters
	var numeric_keys: Array=["default_seed","city_width","city_height","road_y","employees_per_company","available_hours","hourly_wage","company_opening_cash","person_opening_cash","initial_input_stock","initial_output_stock","initial_output_city_step","initial_maintenance_tools","contractor_width","contractor_height","forest_opening","forest_city_step","stone_opening","stone_city_step","forest_capacity","forest_regrowth","construction_budget","construction_work_per_month","construction_hour_price","construction_stage_foundation","construction_stage_frame","director_hours","director_pay","freight_unit_cost","freight_lead_months","minimum_price","policy_minimum","policy_maximum","price_policy_minimum","price_policy_maximum","stock_target_default","stock_target_essentials","stock_excess_multiple","stock_slow_batches","ai_high_stock","ai_low_stock","ai_price_step","ai_price_minimum","ai_price_maximum","maintenance_period_months","maintenance_tools_per_facility","dividend_cash_floor"]
	for key in numeric_keys:
		if not _whole_number(p.get(key)): errors.append("INVALID_OR_MISSING_PARAMETER:"+key)
		elif int(p[key])<0 or (key not in ["default_seed","road_y","initial_output_city_step","forest_city_step","stone_city_step","policy_minimum"] and int(p[key])==0): errors.append("NONPOSITIVE_PARAMETER:"+key)
	for key in ["company_kinds","facility_positions","continent_names","nation_names","city_names","director_names","employee_titles","essential_products"]:
		if typeof(p.get(key))!=TYPE_ARRAY or p.get(key,[]).is_empty(): errors.append("INVALID_PARAMETER_ARRAY:"+key)
	for key in ["default_policy","starter_materials","household_demand"]:
		if typeof(p.get(key))!=TYPE_DICTIONARY or p.get(key,{}).is_empty(): errors.append("INVALID_PARAMETER_VECTOR:"+key)
	if typeof(p.get("contractor_name"))!=TYPE_STRING or str(p.get("contractor_name","")).is_empty(): errors.append("INVALID_CONTRACTOR_NAME")
	if not errors.is_empty(): return errors
	if p.continent_names.size()!=3 or p.nation_names.size()!=3 or p.city_names.size()!=3: errors.append("FIXTURE_REQUIRES_THREE_GEOGRAPHIC_ENTRIES")
	if p.company_kinds.size()!=p.facility_positions.size() or p.company_kinds.size()!=p.director_names.size(): errors.append("FIXTURE_COMPANY_LAYOUT_MISMATCH")
	if p.employee_titles.size()!=int(p.employees_per_company): errors.append("FIXTURE_EMPLOYEE_LAYOUT_MISMATCH")
	for key in ["continent_names","nation_names","city_names","director_names","employee_titles","company_kinds","essential_products"]:
		for item in p[key]:
			if typeof(item)!=TYPE_STRING or str(item).is_empty(): errors.append("INVALID_PARAMETER_TEXT:"+key)
	for position in p.facility_positions:
		if typeof(position)!=TYPE_ARRAY or position.size()!=2:
			errors.append("INVALID_FACILITY_POSITION")
			continue
		if not _whole_number(position[0]) or not _whole_number(position[1]): errors.append("INVALID_FACILITY_COORDINATE")
	for kind in p.company_kinds:
		if kind!="builders" and not building_ids.has(kind): errors.append("UNKNOWN_INITIAL_BUILDING_KIND")
	for key in ["default_policy","starter_materials","household_demand"]:
		for name in p[key]:
			if not _whole_number(p[key][name]) or int(p[key][name])<0: errors.append("INVALID_PARAMETER_VECTOR_QUANTITY:"+key)
	for key in ["production_percent","price_percent","dividend_percent"]:
		if not p.default_policy.has(key): errors.append("MISSING_DEFAULT_POLICY:"+key)
	for key in p.starter_materials:
		if not product_ids.has(key): errors.append("UNKNOWN_STARTER_MATERIAL")
	for key in p.household_demand:
		if not product_ids.has(key): errors.append("UNKNOWN_HOUSEHOLD_PRODUCT")
	if int(p.maintenance_period_months)<=0 or int(p.construction_hour_price)<=0 or int(p.hourly_wage)<=0: errors.append("INVALID_PARAMETER_DIVISOR")
	return errors

static func _parameters() -> Dictionary:
	var data: Dictionary=_fixture()
	return data.parameters if not data.is_empty() else {}

static func policy_limits() -> Dictionary:
	var p: Dictionary = _parameters()
	if p.is_empty(): return {}
	return {"production_min": int(p.policy_minimum), "production_max": int(p.policy_maximum), "price_min": int(p.price_policy_minimum), "price_max": int(p.price_policy_maximum)}

static func _content_hash() -> String:
	# Parameters affect outcomes just as recipes do. A changed balance fixture
	# cannot silently resume a save created under a different economic contract.
	return JSON.stringify({"products": _product_data(), "recipes": _recipe_data(), "buildings": building_types(), "parameters": _parameters()}, "", true).sha256_text()

static func building_types() -> Array:
	var data: Dictionary=_fixture()
	return data.buildings.duplicate(true) if not data.is_empty() else []

static func _product_data() -> Array:
	var data: Dictionary=_fixture()
	return data.products.duplicate(true) if not data.is_empty() else []

static func _recipe_data() -> Array:
	var data: Dictionary=_fixture()
	return data.recipes.duplicate(true) if not data.is_empty() else []

static func new_world(seed_value: Variant = null) -> Dictionary:
	var fixture: Dictionary=_fixture()
	if fixture.is_empty(): return {}
	var p: Dictionary=fixture.parameters
	if seed_value==null: seed_value=int(p.default_seed)
	else: seed_value=int(seed_value)
	var kinds: Array=p.company_kinds
	var products: Array = _product_data()
	var recipes: Array = _recipe_data()
	var s: Dictionary = {"schema_version":1,"rules_version":RULES_VERSION,"seed":seed_value,"version":0,"planning_month":1,"last_resolved_month":0,"phase":"Planning","report_pending":false,"player_id":"p-0","controlled_company_id":"co-0","continents":[],"nations":[],"cities":[],"people":[],"companies":[],"facilities":[],"inventory":[],"projects":[],"shipments":[],"products":products,"recipes":recipes,"commands":[],"journal":[],"accounts":{},"events":[],"reports":[],"last_report":{},"used_command_ids":[],"monthly_labor":{},"experimental":true}
	s.content_hash = _content_hash()
	var positions: Array = p.facility_positions
	var korean_names: Array = p.city_names
	var directors: Array = p.director_names
	for ci in range(3):
		var continent_id: String = "continent-%d" % ci
		var nation_id: String = "nation-%d" % ci
		var city_id: String = "city-%d" % ci
		s.continents.append({"id":continent_id,"name":p.continent_names[ci]})
		s.nations.append({"id":nation_id,"name":p.nation_names[ci],"continent_id":continent_id,"currency":"CR"})
		var roads: Array = []
		for x in range(int(p.city_width)):
			roads.append({"x":x,"y":int(p.road_y)})
		s.cities.append({"id":city_id,"name":korean_names[ci],"nation_id":nation_id,"continent_id":continent_id,"width":int(p.city_width),"height":int(p.city_height),"roads":roads,"resources":{"forest":int(p.forest_opening) + ci * int(p.forest_city_step),"stone":int(p.stone_opening) + ci * int(p.stone_city_step)},"resource_capacity":{"forest":int(p.forest_capacity) + ci * int(p.forest_city_step)},"forest_regrowth":int(p.forest_regrowth)})
		for ki in range(kinds.size()):
			var index: int = ci * kinds.size() + ki
			var company_id: String = "co-%d" % index
			var person_id: String = "p-%d" % (index * int(p.employees_per_company))
			var kind: String = kinds[ki]
			var definition: Dictionary = _building(kind)
			var building_name: String = str(p.contractor_name) if kind=="builders" else str(definition.name)
			s.companies.append({"id":company_id,"name":"%s %s" % [korean_names[ci],building_name],"city_id":city_id,"director_id":person_id,"shareholder_id":person_id,"policy":p.default_policy.duplicate(true),"opening_cash":int(p.company_opening_cash),"revenue":0,"expenses":0,"last_profit":0,"status":"active"})
			Ledger.opening(s, company_id, int(p.company_opening_cash), 0)
			for pi in range(int(p.employees_per_company)):
				var pid: String = "p-%d" % (index * int(p.employees_per_company) + pi)
				s.people.append({"id":pid,"name":"%s %s" % [directors[ki], p.employee_titles[pi]],"city_id":city_id,"company_id":company_id,"role":"director" if pi == 0 else ("builder" if kind == "builders" else "operator"),"hours":int(p.available_hours),"wage":int(p.hourly_wage),"employment_status":"employed","last_income":0,"last_consumption":0})
				Ledger.opening(s, pid, int(p.person_opening_cash), 0)
			var facility: Dictionary = {"id":"f-%d" % index,"name":building_name,"company_id":company_id,"city_id":city_id,"kind":kind,"x":positions[ki][0],"y":positions[ki][1],"w":int(p.contractor_width) if kind=="builders" else int(definition.w),"h":int(p.contractor_height) if kind=="builders" else int(definition.h),"status":"operational","recipe_id":definition.get("recipe_id",""),"capacity":definition.get("capacity",0),"operational_month":1}
			s.facilities.append(facility)
			if kind != "builders":
				var recipe: Dictionary = _find(s.recipes, facility.recipe_id)
				for product_id in _keys(recipe.inputs):
					_change_inventory(s, company_id, city_id, product_id, int(p.initial_input_stock), "opening", 0)
				for product_id in _keys(recipe.outputs):
					_change_inventory(s, company_id, city_id, product_id, int(p.initial_output_stock) + ci * int(p.initial_output_city_step), "opening", 0)
			# Finite opening maintenance stock. These are not regenerated during play.
			_change_inventory(s, company_id, city_id, "tools", int(p.initial_maintenance_tools), "opening", 0)
		# The selected company can begin a genuine construction project immediately.
		var selected_company: String = "co-%d" % (ci * kinds.size())
		for product_id in ["wood","stone"]:
			_change_inventory(s,selected_company,city_id,product_id,int(p.starter_materials[product_id]),"opening",0)
	return s

static func _building(kind: String) -> Dictionary:
	for definition in building_types():
		if definition.id == kind:
			return definition
	return {}

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if str(item.get("id", "")) == id:
			return item
	return {}

static func cash(s: Dictionary, owner_id: String) -> int:
	return Ledger.balance(s, owner_id + ":cash")

static func available_quantity(s: Dictionary, owner_id: String, product_id: String, city_id: String = "") -> int:
	var total: int = 0
	for lot in s.get("inventory", []):
		if lot.owner_id == owner_id and lot.product_id == product_id and str(lot.location) != "shipment" and (city_id == "" or lot.city_id == city_id):
			total += maxi(0, int(lot.quantity) - int(lot.get("reserved",0)))
	return total

static func _available_lot(s: Dictionary, owner_id: String, city_id: String, product_id: String) -> Dictionary:
	var lot_id: String = "lot:%s:%s:%s" % [owner_id, city_id, product_id]
	for lot in s.inventory:
		if lot.id == lot_id:
			return lot
	var lot: Dictionary = {"id":lot_id,"owner_id":owner_id,"city_id":city_id,"accounting_city_id":city_id,"location":"warehouse","product_id":product_id,"quantity":0,"reserved":0,"unit_cost":0}
	s.inventory.append(lot)
	return lot

static func _event(s: Dictionary, event_type: String, reason: String, month: int, extra: Dictionary = {}) -> void:
	var event: Dictionary = {"id":"ev-%d" % s.events.size(),"month":month,"type":event_type,"reason":reason,"actor_id":"","city_id":"","product_id":"","quantity":0,"amount":0}
	event.merge(extra, true)
	s.events.append(event)

static func _change_inventory(s: Dictionary, owner_id: String, city_id: String, product_id: String, delta: int, reason: String, month: int, event_type: String = "inventory") -> void:
	var lot: Dictionary = _available_lot(s, owner_id, city_id, product_id)
	lot.quantity = int(lot.quantity) + delta
	_event(s, event_type, reason, month, {"actor_id":owner_id,"city_id":city_id,"product_id":product_id,"quantity":absi(delta),"inventory_delta":delta,"lot_id":lot.id})

static func adjust_inventory(s: Dictionary, owner_id: String, product_id: String, delta: int, city_id: String, reason: String = "experimental_shock") -> Dictionary:
	if _find(s.products, product_id).is_empty() or _find(s.cities, city_id).is_empty():
		return {"ok":false,"error":"UNKNOWN_PRODUCT_OR_CITY"}
	if delta > 0:
		return {"ok":false,"error":"SHOCK_MAY_ONLY_REMOVE_STOCK"}
	if available_quantity(s, owner_id, product_id, city_id) < -delta:
		return {"ok":false,"error":"INSUFFICIENT_STOCK"}
	_change_inventory(s, owner_id, city_id, product_id, delta, reason, int(s.planning_month), "loss")
	return {"ok":true}

static func _rect_overlap(a: Dictionary, b: Dictionary) -> bool:
	return int(a.x) < int(b.x) + _dimension(b,"w") and int(a.x) + _dimension(a,"w") > int(b.x) and int(a.y) < int(b.y) + _dimension(b,"h") and int(a.y) + _dimension(a,"h") > int(b.y)

static func _dimension(rect: Dictionary, key: String) -> int:
	if rect.has(key): return int(rect[key])
	var definition: Dictionary=_building(str(rect.get("kind","")))
	if definition.is_empty():
		push_error("MISSING_RECTANGLE_DEFINITION")
		return 0
	return int(definition[key])

static func _site_error(s: Dictionary, payload: Dictionary, company_id: String, include_commands: bool = true) -> String:
	var city: Dictionary = _find(s.cities, str(payload.get("city_id","")))
	if city.is_empty():
		return "UNKNOWN_CITY"
	var company: Dictionary = _find(s.companies, company_id)
	if company.city_id != city.id:
		return "REMOTE_CONSTRUCTION_NOT_SUPPORTED"
	var x: int = int(payload.get("x",-1))
	var y: int = int(payload.get("y",-1))
	var definition: Dictionary=_building(str(payload.get("kind","")))
	if definition.is_empty(): return "UNKNOWN_BUILDING"
	var w: int=int(definition.w)
	var h: int=int(definition.h)
	var rect: Dictionary = {"x":x,"y":y,"w":w,"h":h}
	if x < 0 or y < 0 or x + w > int(city.width) or y + h > int(city.height):
		return "OUT_OF_BOUNDS"
	var adjacent: bool = false
	for road in city.roads:
		var rx: int = int(road.x)
		var ry: int = int(road.y)
		if rx >= x and rx < x + w and ry >= y and ry < y + h:
			return "ROAD_OCCUPIED"
		if (rx >= x and rx < x + w and (ry == y - 1 or ry == y + h)) or (ry >= y and ry < y + h and (rx == x - 1 or rx == x + w)):
			adjacent = true
	if not adjacent:
		return "ROAD_ACCESS_REQUIRED"
	for facility in s.facilities:
		if facility.city_id == city.id and _rect_overlap(rect, facility):
			return "TILE_OCCUPIED"
	for project in s.projects:
		if project.city_id == city.id and project.status in ["active","paused"] and _rect_overlap(rect, project):
			return "TILE_OCCUPIED"
	if include_commands:
		for command in s.commands:
			if command.type == "build" and command.acting_company_id == company_id and command.payload.get("city_id", "") == city.id and _rect_overlap(rect, command.payload):
				return "SELF_TILE_RESERVATION_CONFLICT"
	return ""

static func queue_command(s: Dictionary, command: Dictionary) -> Dictionary:
	var p: Dictionary=_parameters()
	if p.is_empty(): return {"ok":false,"error":"EXPERIMENTAL_CONTENT_INVALID"}
	if str(s.get("phase", "")) != "Planning":
		return {"ok":false,"error":"NOT_PLANNING"}
	for field in ["id","actor_id","acting_company_id","expected_version","type","payload"]:
		if not command.has(field):
			return {"ok":false,"error":"MISSING_FIELD:" + field}
	for field in ["id","actor_id","acting_company_id","type"]:
		if typeof(command[field]) not in [TYPE_STRING,TYPE_STRING_NAME]:
			return {"ok":false,"error":"INVALID_COMMAND_TEXT:"+field}
	if typeof(command.expected_version) not in [TYPE_INT,TYPE_FLOAT] or not is_finite(float(command.expected_version)) or float(command.expected_version) != floor(float(command.expected_version)) or int(command.expected_version) != int(s.version):
		return {"ok":false,"error":"STALE_STATE_VERSION"}
	if str(command.id).is_empty():
		return {"ok":false,"error":"EMPTY_COMMAND_ID"}
	for queued in s.commands:
		if queued.id == command.id:
			return {"ok":false,"error":"DUPLICATE_COMMAND_ID"}
	if command.id in s.get("used_command_ids",[]):
		return {"ok":false,"error":"DUPLICATE_COMMAND_ID"}
	var company: Dictionary = _find(s.companies, str(command.acting_company_id))
	var actor: Dictionary = _find(s.people, str(command.actor_id))
	if company.is_empty() or actor.is_empty():
		return {"ok":false,"error":"UNKNOWN_ACTOR_OR_COMPANY"}
	if company.director_id != actor.id:
		return {"ok":false,"error":"AUTHORITY_REQUIRED"}
	if company.get("status","active") != "active":
		return {"ok":false,"error":"COMPANY_INACTIVE"}
	if typeof(command.payload) != TYPE_DICTIONARY:
		return {"ok":false,"error":"INVALID_PAYLOAD"}
	var payload: Dictionary = command.payload
	match str(command.type):
		"set_policy":
			if payload.is_empty():
				return {"ok":false,"error":"EMPTY_POLICY"}
			for key in _keys(payload):
				if key not in ["production_percent","price_percent","dividend_percent"]:
					return {"ok":false,"error":"UNKNOWN_POLICY"}
				if typeof(payload[key]) not in [TYPE_INT, TYPE_FLOAT] or float(payload[key]) != floor(float(payload[key])):
					return {"ok":false,"error":"POLICY_MUST_BE_INTEGER"}
				var minimum: int = int(p.policy_minimum) if key != "price_percent" else int(p.price_policy_minimum)
				var maximum: int = int(p.policy_maximum) if key != "price_percent" else int(p.price_policy_maximum)
				if int(payload[key]) < minimum or int(payload[key]) > maximum:
					return {"ok":false,"error":"POLICY_OUT_OF_RANGE"}
			for queued in s.commands:
				if queued.acting_company_id == company.id and queued.type == "set_policy":
					return {"ok":false,"error":"POLICY_ALREADY_QUEUED"}
		"build":
			if _building(str(payload.get("kind",""))).is_empty():
				return {"ok":false,"error":"UNKNOWN_BUILDING"}
			for coordinate in ["x","y"]:
				if typeof(payload.get(coordinate)) not in [TYPE_INT,TYPE_FLOAT] or float(payload[coordinate]) != floor(float(payload[coordinate])):
					return {"ok":false,"error":"INVALID_TILE"}
			var site_error: String = _site_error(s, payload, company.id)
			if not site_error.is_empty():
				return {"ok":false,"error":site_error}
			var reserved: int = 0
			for queued in s.commands:
				if queued.acting_company_id == company.id and queued.type == "build":
					reserved += int(p.construction_budget)
			if cash(s,company.id) - reserved < int(p.construction_budget):
				return {"ok":false,"error":"CONSTRUCTION_BUDGET_REQUIRED"}
		"cancel_project", "pause_project", "resume_project":
			var project: Dictionary = _find(s.projects, str(payload.get("project_id","")))
			if project.is_empty() or project.company_id != company.id:
				return {"ok":false,"error":"PROJECT_AUTHORITY_REQUIRED"}
			if project.status in ["cancelled","completed"]:
				return {"ok":false,"error":"PROJECT_ALREADY_CLOSED"}
			for queued in s.commands:
				if queued.payload.get("project_id", "") == project.id:
					return {"ok":false,"error":"PROJECT_COMMAND_ALREADY_QUEUED"}
		_:
			return {"ok":false,"error":"UNSUPPORTED_COMMAND"}
	s.commands.append(command.duplicate(true))
	return {"ok":true,"error":""}

static func _stable_key(s: Dictionary, category: String, id: String) -> String:
	return (str(s.seed) + ":" + str(s.planning_month) + ":" + category + ":" + id).sha256_text()

static func _sorted(s: Dictionary, items: Array, category: String) -> Array:
	# Compute seeded priorities once rather than hashing on every comparator call.
	var decorated: Array = []
	for item in items:
		decorated.append({"key":_stable_key(s,category,str(item.id)),"item":item})
	decorated.sort_custom(func(a,b): return str(a.key)<str(b.key))
	var result: Array = []
	for entry in decorated: result.append(entry.item)
	return result

static func _effective_price(s: Dictionary, company_id: String, product_id: String) -> int:
	var definition: Dictionary = _find(s.products, product_id)
	var company: Dictionary = _find(s.companies, company_id)
	var multiplier: int = int(company.policy.price_percent)
	return maxi(int(_parameters().minimum_price), int(round(float(definition.price) * float(multiplier) / 100.0)))

static func _pay(s: Dictionary, payer: String, payee: String, amount: int, reason: String, month: int, extra: Dictionary = {}) -> bool:
	if amount <= 0:
		return true
	var id: String = "payment-%d-%d" % [month, s.journal.size()]
	var result: Dictionary = Ledger.transfer(s, id, payer, payee, amount, reason, month)
	if not result.get("ok",false):
		return false
	var payer_company: Dictionary = _find(s.companies,payer)
	var payee_company: Dictionary = _find(s.companies,payee)
	if not payer_company.is_empty():
		payer_company.expenses = int(payer_company.expenses) + amount
	if not payee_company.is_empty():
		payee_company.revenue = int(payee_company.revenue) + amount
	var person: Dictionary = _find(s.people,payee)
	if not person.is_empty():
		person.last_income = int(person.last_income) + amount
	var details: Dictionary = {"actor_id":payer,"payee_id":payee,"amount":amount}
	details.merge(extra,true)
	_event(s,"payment",reason,month,details)
	return true

static func _sellers(s: Dictionary, buyer: String, city_id: String, product_id: String) -> Array:
	var sellers: Array = []
	var supplier_ids: Dictionary = {}
	for facility in s.facilities:
		if str(facility.recipe_id).is_empty(): continue
		var recipe: Dictionary = _find(s.recipes,facility.recipe_id)
		if recipe.get("outputs",{}).has(product_id): supplier_ids[facility.company_id]=true
	for company_id in supplier_ids:
		var company: Dictionary = _find(s.companies,str(company_id))
		if company.id == buyer or company.get("status","active") != "active":
			continue
		if available_quantity(s, company.id, product_id, company.city_id) > 0:
			sellers.append({"id":company.id,"city_id":company.city_id,"price":_effective_price(s,company.id,product_id),"remote":company.city_id != city_id})
	sellers.sort_custom(func(a,b):
		var a_cost: int = int(a.price) + (int(_parameters().freight_unit_cost) if a.remote else 0)
		var b_cost: int = int(b.price) + (int(_parameters().freight_unit_cost) if b.remote else 0)
		if a_cost != b_cost:
			return a_cost < b_cost
		return _stable_key(s,"seller",a.id + buyer + product_id) < _stable_key(s,"seller",b.id + buyer + product_id)
	)
	return sellers

static func _contractor(s: Dictionary, city_id: String) -> Dictionary:
	for company in s.companies:
		if company.city_id == city_id:
			for facility in s.facilities:
				if facility.company_id == company.id and facility.kind == "builders":
					return company
	return {}

static func _purchase(s: Dictionary, buyer: String, city_id: String, product_id: String, desired: int, month: int, reason: String) -> int:
	var acquired: int = 0
	if desired <= 0:
		return acquired
	_event(s,"demand",reason,month,{"actor_id":buyer,"city_id":city_id,"product_id":product_id,"quantity":desired})
	for seller in _sellers(s,buyer,city_id,product_id):
		var remote: bool = bool(seller.remote)
		var price: int = int(seller.price)
		var unit_cost: int = price + (int(_parameters().freight_unit_cost) if remote else 0)
		var quantity: int = mini(desired - acquired, available_quantity(s,seller.id,product_id,seller.city_id))
		quantity = mini(quantity, int(cash(s,buyer) / unit_cost))
		if quantity <= 0:
			continue
		if not _pay(s,buyer,seller.id,quantity * price,"상품 매입",month,{"city_id":city_id,"product_id":product_id,"quantity":quantity,"seller_city_id":seller.city_id}):
			continue
		_change_inventory(s,seller.id,seller.city_id,product_id,-quantity,"판매 출고",month,"trade_out")
		if remote:
			var contractor: Dictionary = _contractor(s,seller.city_id)
			if not contractor.is_empty():
				_pay(s,buyer,contractor.id,quantity * int(_parameters().freight_unit_cost),"도시 간 운송",month,{"city_id":city_id,"product_id":product_id,"quantity":quantity})
			var shipment_id: String = "ship-%d-%d" % [month,s.shipments.size()]
			var lot_id: String = "lot:" + shipment_id
			s.inventory.append({"id":lot_id,"owner_id":buyer,"city_id":seller.city_id,"accounting_city_id":city_id,"location":"shipment","product_id":product_id,"quantity":quantity,"reserved":0,"shipment_id":shipment_id,"unit_cost":price})
			_event(s,"shipment","운송 중 소유권 이전",month,{"actor_id":buyer,"city_id":city_id,"product_id":product_id,"quantity":quantity,"inventory_delta":quantity,"lot_id":lot_id})
			s.shipments.append({"id":shipment_id,"owner_id":buyer,"product_id":product_id,"quantity":quantity,"from_city_id":seller.city_id,"to_city_id":city_id,"eta":month+int(_parameters().freight_lead_months),"status":"transit","lot_id":lot_id})
		else:
			_change_inventory(s,buyer,city_id,product_id,quantity,"매입 입고",month,"trade_in")
		_event(s,"trade",reason,month,{"actor_id":buyer,"seller_id":seller.id,"city_id":city_id,"seller_city_id":seller.city_id,"product_id":product_id,"quantity":quantity,"amount":quantity*price,"unit_price":price,"remote":remote})
		acquired += quantity
		if acquired >= desired:
			break
	if acquired < desired:
		_event(s,"shortage", "구매 예산 또는 실제 공급 부족",month,{"actor_id":buyer,"city_id":city_id,"product_id":product_id,"quantity":desired-acquired,"requested_reason":reason})
	return acquired

static func _arrive_shipments(s: Dictionary, month: int) -> void:
	for shipment in s.shipments:
		if shipment.status == "transit" and int(shipment.eta) <= month:
			var lot: Dictionary = _find(s.inventory,shipment.lot_id)
			var quantity: int = int(lot.quantity)
			lot.quantity = 0
			_event(s,"shipment_arrival","운송 종료",month,{"actor_id":shipment.owner_id,"city_id":shipment.to_city_id,"product_id":shipment.product_id,"quantity":quantity,"lot_id":lot.id,"inventory_delta":-quantity})
			_change_inventory(s,shipment.owner_id,shipment.to_city_id,shipment.product_id,quantity,"도착 인수",month,"shipment_arrival")
			shipment.status = "delivered"

static func _work_available(s: Dictionary, company_id: String, role: String = "operator") -> int:
	var total: int = 0
	for person in s.people:
		if person.company_id == company_id and person.role == role:
			total += maxi(0,int(person.hours) - int(s.monthly_labor.get(person.id,0)))
	return total

static func _work(s: Dictionary, company_id: String, hours: int, month: int, role: String = "operator") -> bool:
	if hours <= 0:
		return true
	if _work_available(s,company_id,role) < hours or cash(s,company_id) < hours * int(_parameters().hourly_wage):
		return false
	var remaining: int = hours
	for person in _sorted(s,s.people,"workers"):
		if person.company_id != company_id or person.role != role:
			continue
		var free: int = maxi(0,int(person.hours) - int(s.monthly_labor.get(person.id,0)))
		var used: int = mini(free,remaining)
		if used > 0:
			if not _pay(s,company_id,person.id,used * int(person.wage),"근로 임금",month,{"city_id":person.city_id,"hours":used}):
				return false
			s.monthly_labor[person.id] = int(s.monthly_labor.get(person.id,0)) + used
			_event(s,"labor","건설 작업" if role == "builder" else "생산 작업",month,{"actor_id":person.id,"company_id":company_id,"city_id":person.city_id,"hours":used})
			remaining -= used
		if remaining <= 0:
			break
	return remaining == 0

static func _produce(s: Dictionary, facility: Dictionary, month: int) -> void:
	if facility.get("status","") not in ["operational","ready"] or int(facility.get("operational_month",1)) > month or str(facility.recipe_id).is_empty():
		return
	facility.status = "operational"
	var company: Dictionary = _find(s.companies, facility.company_id)
	var recipe: Dictionary = _find(s.recipes, facility.recipe_id)
	var target: int = int(int(facility.capacity) * int(company.policy.production_percent) / 100)
	if target <= 0:
		return
	# Stock policy is actor-visible own inventory, not omniscient future demand.
	var output_id: String = str(recipe.outputs.keys()[0])
	var p: Dictionary=_parameters()
	var target_stock: int = int(p.stock_target_essentials) if output_id in p.essential_products else int(p.stock_target_default)
	var existing: int = available_quantity(s,company.id,output_id,company.city_id)
	if existing >= target_stock * int(p.stock_excess_multiple):
		target = 0
	elif existing >= target_stock:
		target = mini(target,int(p.stock_slow_batches))
	if target <= 0:
		_event(s,"production_limit","목표 재고를 채워 감산",month,{"actor_id":company.id,"city_id":company.city_id,"facility_id":facility.id,"product_id":output_id})
		return
	for product_id in _keys(recipe.inputs):
		var missing: int = target * int(recipe.inputs[product_id]) - available_quantity(s,company.id,product_id,company.city_id)
		if missing > 0:
			_purchase(s,company.id,company.city_id,product_id,missing,month,"생산 투입물")
	var batches: int = target
	var constraint: String = "설비 처리능력"
	for product_id in _keys(recipe.inputs):
		var possible: int = int(available_quantity(s,company.id,product_id,company.city_id) / int(recipe.inputs[product_id]))
		if possible < batches:
			batches = possible
			constraint = "원료 부족: " + str(_find(s.products,product_id).name)
	var work_batches: int = int(_work_available(s,company.id) / int(recipe.labor))
	if work_batches < batches:
		batches = work_batches
		constraint = "운영 인력 시간 부족"
	var money_batches: int = int(cash(s,company.id) / (int(recipe.labor)*int(p.hourly_wage)))
	if money_batches < batches:
		batches = money_batches
		constraint = "임금 지급 예산 부족"
	var city: Dictionary = _find(s.cities,company.city_id)
	if recipe.has("resource"):
		var resource_batches: int = int(int(city.resources[recipe.resource]) / int(recipe.resource_per_batch))
		if resource_batches < batches:
			batches = resource_batches
			constraint = "현지 자연자원 부족"
	if batches < target:
		_event(s,"production_limit",constraint,month,{"actor_id":company.id,"city_id":company.city_id,"facility_id":facility.id,"product_id":output_id,"quantity":target-batches})
	if batches <= 0:
		return
	if not _work(s,company.id,batches*int(recipe.labor),month):
		return
	for product_id in _keys(recipe.inputs):
		_change_inventory(s,company.id,company.city_id,product_id,-batches*int(recipe.inputs[product_id]),"공정 투입",month,"production_input")
	if recipe.has("resource"):
		var amount: int = batches * int(recipe.resource_per_batch)
		city.resources[recipe.resource] = int(city.resources[recipe.resource]) - amount
		_event(s,"resource_extraction","자연자원 채취",month,{"actor_id":company.id,"city_id":company.city_id,"resource":recipe.resource,"quantity":amount})
	for product_id in _keys(recipe.outputs):
		_change_inventory(s,company.id,company.city_id,product_id,batches*int(recipe.outputs[product_id]),"공정 산출",month,"production_output")
	_event(s,"production","실제 투입물과 노동으로 생산",month,{"actor_id":company.id,"city_id":company.city_id,"facility_id":facility.id,"recipe_id":recipe.id,"batches":batches,"hours":batches*int(recipe.labor)})

static func _ai_commands(s: Dictionary) -> void:
	var p: Dictionary=_parameters()
	for company in s.companies:
		if company.director_id == s.player_id or company.get("status","active") != "active":
			continue
		var has_policy: bool = false
		for command in s.commands:
			if command.type == "set_policy" and command.acting_company_id == company.id:
				has_policy = true
		if has_policy:
			continue
		var price_percent: int = int(company.policy.price_percent)
		var own_output: int = 0
		for facility in s.facilities:
			if facility.company_id == company.id and not str(facility.recipe_id).is_empty():
				var recipe: Dictionary = _find(s.recipes,facility.recipe_id)
				for product_id in _keys(recipe.outputs):
					own_output += available_quantity(s,company.id,product_id,company.city_id)
		if own_output > int(p.ai_high_stock):
			price_percent = maxi(int(p.ai_price_minimum),price_percent - int(p.ai_price_step))
		elif own_output < int(p.ai_low_stock):
			price_percent = mini(int(p.ai_price_maximum),price_percent + int(p.ai_price_step))
		queue_command(s,{"id":"ai-policy-%d-%s" % [s.planning_month,company.id],"actor_id":company.director_id,"acting_company_id":company.id,"expected_version":s.version,"type":"set_policy","payload":{"price_percent":price_percent}})

static func _execute_commands(s: Dictionary, month: int) -> void:
	for command in _sorted(s,s.commands,"commands"):
		var company: Dictionary = _find(s.companies,command.acting_company_id)
		match str(command.type):
			"set_policy":
				for key in _keys(command.payload):
					company.policy[key] = int(command.payload[key])
				_event(s,"policy","반복 운영정책 변경",month,{"actor_id":command.actor_id,"company_id":company.id,"city_id":company.city_id,"command_id":command.id})
			"build":
				var site_error: String = _site_error(s,command.payload,company.id,false)
				if not site_error.is_empty():
					_event(s,"command_failed",site_error,month,{"actor_id":command.actor_id,"command_id":command.id,"city_id":company.city_id})
				else:
					var definition: Dictionary = _building(command.payload.kind)
					var project: Dictionary = {"id":"project-" + command.id,"company_id":company.id,"city_id":command.payload.city_id,"kind":definition.id,"name":definition.name,"x":int(command.payload.x),"y":int(command.payload.y),"w":int(definition.w),"h":int(definition.h),"stage":"기초","progress":0,"required_work":int(definition.work),"months_worked":0,"min_months":int(definition.min_months),"status":"active","materials_used":{"wood":0,"stone":0,"tools":0},"materials_required":{"wood":int(definition.wood),"stone":int(definition.stone),"tools":int(definition.tools)},"reason":"착공 준비","start_month":month,"budget":int(_parameters().construction_budget),"spent":0}
					s.projects.append(project)
					_event(s,"construction_start","부지 확정·월별 자재와 시공 필요",month,{"actor_id":company.id,"city_id":project.city_id,"project_id":project.id,"command_id":command.id})
			"cancel_project", "pause_project", "resume_project":
				var project: Dictionary = _find(s.projects,command.payload.project_id)
				project.status = {"cancel_project":"cancelled","pause_project":"paused","resume_project":"active"}[command.type]
				project.reason = "사용자 공사 정책 변경"
				_event(s,"construction_status","미사용 재고 유지·이미 사용한 자재는 환불하지 않음",month,{"actor_id":company.id,"city_id":project.city_id,"project_id":project.id,"status":project.status,"command_id":command.id})
		s.used_command_ids.append(command.id)
	s.commands = []

static func _construct(s: Dictionary, project: Dictionary, month: int) -> void:
	var p: Dictionary=_parameters()
	if project.status != "active":
		return
	var contractor: Dictionary = _contractor(s,project.city_id)
	if contractor.is_empty():
		project.reason = "시공업체 없음"
		return
	var owner: String = str(project.company_id)
	var target: int = mini(int(p.construction_work_per_month),int(project.required_work)-int(project.progress))
	target = mini(target,_work_available(s,contractor.id,"builder"))
	target = mini(target,int(cash(s,owner)/int(p.construction_hour_price)))
	for product_id in _keys(project.materials_required):
		var monthly_need: int = int(ceil(float(project.materials_required[product_id]) * float(int(project.progress)+target) / float(project.required_work))) - int(project.materials_used[product_id])
		var shortage: int = monthly_need - available_quantity(s,owner,product_id,project.city_id)
		if shortage > 0:
			_purchase(s,owner,project.city_id,product_id,shortage,month,"건설 자재")
	var feasible: int = target
	while feasible > 0:
		var has_materials: bool = true
		for product_id in _keys(project.materials_required):
			var need: int = int(ceil(float(project.materials_required[product_id]) * float(int(project.progress)+feasible) / float(project.required_work))) - int(project.materials_used[product_id])
			if available_quantity(s,owner,product_id,project.city_id) < need:
				has_materials = false
		if has_materials:
			break
		feasible -= 1
	project.reason = "진행 중"
	if feasible < target:
		project.reason = "현장 자재 부족 또는 미도착"
	elif target < mini(int(p.construction_work_per_month),int(project.required_work)-int(project.progress)):
		project.reason = "시공 인력 또는 자금 부족"
	if feasible <= 0:
		if cash(s,owner)<int(p.construction_hour_price):
			project.reason = "시공대금 지급 예산 부족"
		_event(s,"construction_delay",project.reason,month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id})
		return
	if not _pay(s,owner,contractor.id,feasible*int(p.construction_hour_price),"공정 도급 대금",month,{"city_id":project.city_id,"project_id":project.id}):
		project.reason = "시공대금 미지급"
		return
	if not _work(s,contractor.id,feasible,month,"builder"):
		project.reason = "시공업체 임금 지급 실패"
		return
	for product_id in _keys(project.materials_required):
		var need: int = int(ceil(float(project.materials_required[product_id]) * float(int(project.progress)+feasible) / float(project.required_work))) - int(project.materials_used[product_id])
		if need > 0:
			_change_inventory(s,owner,project.city_id,product_id,-need,"건설 공정 투입",month,"construction_input")
			project.materials_used[product_id] = int(project.materials_used[product_id]) + need
	project.progress = int(project.progress) + feasible
	project.months_worked = int(project.months_worked) + 1
	project.spent = int(project.spent) + feasible*int(p.construction_hour_price)
	project.stage = "기초" if int(project.progress)<int(p.construction_stage_foundation) else ("골조" if int(project.progress)<int(p.construction_stage_frame) else "설비·검수")
	_event(s,"construction_work","자재·시공시간·공정대금 투입",month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id,"hours":feasible,"quantity":feasible})
	if int(project.progress)>=int(project.required_work) and int(project.months_worked)>=int(project.min_months):
		project.status = "completed"
		project.stage = "준공"
		project.reason = "월말 검수 완료·다음 달부터 가동 가능"
		project.completed_month = month
		var definition: Dictionary = _building(project.kind)
		s.facilities.append({"id":"facility-"+project.id,"name":definition.name,"company_id":owner,"city_id":project.city_id,"kind":project.kind,"x":project.x,"y":project.y,"w":project.w,"h":project.h,"status":"ready","recipe_id":definition.recipe_id,"capacity":definition.capacity,"operational_month":month+1,"project_id":project.id})
		_event(s,"construction_complete",project.reason,month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id})

static func _consume(s: Dictionary, person: Dictionary, month: int) -> void:
	var wants: Dictionary = _parameters().household_demand.duplicate(true)
	for product_id in wants:
		var amount: int = int(wants[product_id])
		var held: int = available_quantity(s,person.id,product_id,person.city_id)
		if held < amount:
			_purchase(s,person.id,person.city_id,product_id,amount-held,month,"가계 필수 수요")
		var consumed: int = mini(amount,available_quantity(s,person.id,product_id,person.city_id))
		if consumed > 0:
			_change_inventory(s,person.id,person.city_id,product_id,-consumed,"가계 사용",month,"consumption")
			person.last_consumption = int(person.last_consumption)+consumed
		if consumed < amount:
			_event(s,"unmet_need","현지 재고 또는 가계 구매력 부족",month,{"actor_id":person.id,"city_id":person.city_id,"product_id":product_id,"quantity":amount-consumed})

static func resolve_month(s: Dictionary) -> Dictionary:
	var errors: Array = validate(s)
	var p: Dictionary=_parameters()
	if not errors.is_empty():
		return {"ok":false,"errors":errors,"error":"INVALID_INPUT_STATE","state":s,"report":{}}
	if s.phase != "Planning":
		return {"ok":false,"errors":["NOT_PLANNING"],"error":"NOT_PLANNING","state":s,"report":{}}
	var work: Dictionary = s.duplicate(true)
	var month: int = int(work.planning_month)
	var start_event: int = work.events.size()
	work.phase = "Resolving"
	work.monthly_labor = {}
	for company in work.companies:
		company.revenue = 0
		company.expenses = 0
	for person in work.people:
		person.last_income = 0
		person.last_consumption = 0
	# Queue AI through the public validator while the snapshot is still planning.
	work.phase = "Planning"
	_ai_commands(work)
	# Revalidate every submitted command from the immutable planning snapshot.
	var recheck: Dictionary = work.duplicate(true)
	recheck.commands = []
	for command in work.commands:
		var result: Dictionary = queue_command(recheck,command)
		if not result.ok:
			return {"ok":false,"errors":[result.error],"error":"INVALID_COMMAND","state":s,"report":{}}
	work.phase = "Resolving"
	_arrive_shipments(work,month)
	_execute_commands(work,month)
	for facility in work.facilities:
		if int(facility.get("operational_month",1))<=month and facility.status=="ready":
			facility.status="operational"
	# Directors use actual compensated time; they do not duplicate operator time.
	for company in _sorted(work,work.companies,"management"):
		if cash(work,company.id)>=int(p.director_pay):
			_pay(work,company.id,company.director_id,int(p.director_pay),"대표 경영 보수",month,{"city_id":company.city_id,"hours":int(p.director_hours)})
			work.monthly_labor[company.director_id]=int(p.director_hours)
			_event(work,"labor","대표 경영 업무",month,{"actor_id":company.director_id,"company_id":company.id,"city_id":company.city_id,"hours":int(p.director_hours)})
	for facility in _sorted(work,work.facilities,"production"):
		_produce(work,facility,month)
	for project in _sorted(work,work.projects,"construction"):
		_construct(work,project,month)
	# Buyers are batch collected, then seeded per-period order changes fairness.
	for person in _sorted(work,work.people,"consumption"):
		_consume(work,person,month)
	# Wear is explicit: one maintenance tool per operating facility each year.
	if month%int(p.maintenance_period_months)==0:
		for facility in work.facilities:
			if facility.status=="operational" and available_quantity(work,facility.company_id,"tools",facility.city_id)>0:
				_change_inventory(work,facility.company_id,facility.city_id,"tools",-int(p.maintenance_tools_per_facility),"시설 정비 공구 사용",month,"maintenance")
	for city in work.cities:
		var growth: int = mini(int(city.forest_regrowth),int(city.resource_capacity.forest)-int(city.resources.forest))
		if growth>0:
			city.resources.forest=int(city.resources.forest)+growth
			_event(work,"resource_regrowth","유한 산림의 자연 재생",month,{"city_id":city.id,"resource":"forest","quantity":growth})
	for company in work.companies:
		company.last_profit = int(company.revenue)-int(company.expenses)
		var dividend: int = maxi(0,int(int(company.last_profit)*int(company.policy.dividend_percent)/100))
		dividend=mini(dividend,maxi(0,cash(work,company.id)-int(p.dividend_cash_floor)))
		if dividend>0:
			_pay(work,company.id,company.shareholder_id,dividend,"주주 배당",month,{"city_id":company.city_id})
	work.last_resolved_month=month
	work.planning_month=month+1
	work.version=int(work.version)+1
	work.phase="Planning"
	work.report_pending=true
	var report: Dictionary = {"month":month,"production":0,"consumption":0,"unmet":0,"unmet_events":0,"revenue":0,"expenses":0,"trades":0,"construction_completed":0,"events":[],"summary":"","state_hash":""}
	for index in range(start_event,work.events.size()):
		var event: Dictionary = work.events[index]
		report.events.append(event.duplicate(true))
		match str(event.type):
			"production_output": report.production+=int(event.quantity)
			"consumption": report.consumption+=int(event.quantity)
			"unmet_need":
				report.unmet+=int(event.quantity)
				report.unmet_events+=1
			"trade":
				report.trades+=1
				report.revenue+=int(event.amount)
				# A goods purchase has the same settlement amount, not new money.
				report.expenses+=int(event.amount)
			"construction_complete": report.construction_completed+=1
	report.summary="%d월 · 실제 체결 %d건 · 준공 %d곳 · 미충족 수요 발생 %d건" % [month,report.trades,report.construction_completed,report.unmet_events]
	work.reports.append(report)
	work.last_report=report
	errors=validate(work)
	if not errors.is_empty():
		return {"ok":false,"errors":errors,"error":"INVARIANT_FAILURE","state":s,"report":{}}
	report.state_hash=state_hash(work)
	return {"ok":true,"errors":[],"error":"","state":work,"report":report}

static func _scope_cities(s: Dictionary, scope_id: String) -> Array:
	var ids: Array = []
	for city in s.cities:
		if scope_id=="world" or city.id==scope_id or city.nation_id==scope_id or city.continent_id==scope_id:
			ids.append(city.id)
	return ids

static func metrics(s: Dictionary, scope_id: String = "world") -> Dictionary:
	var city_ids: Array = _scope_cities(s,scope_id)
	var result: Dictionary={"cash":0,"revenue":0,"expenses":0,"population":0,"employed":0,"production":0,"consumption":0,"unmet":0,"price_index":100,"inventory_total":0,"trades":0,"per_product":[],"scope_id":scope_id,"month":int(s.last_resolved_month),"accounting":"experimental_cash_flow_not_GDP","unit_warning":"서로 다른 상품 단위의 합은 생산·소비 활동량 참고값이며 GDP가 아닙니다."}
	var prices_total: int=0
	var prices_count: int=0
	for person in s.people:
		if person.city_id in city_ids:
			result.population+=1
			result.cash+=cash(s,person.id)
			if person.get("employment_status","")=="employed": result.employed+=1
	for company in s.companies:
		if company.city_id in city_ids:
			result.cash+=cash(s,company.id)
			result.revenue+=int(company.revenue)
			result.expenses+=int(company.expenses)
	for product in s.products:
		var row: Dictionary={"product_id":product.id,"name":product.name,"unit":product.unit,"price":int(product.price),"supply":0,"demand":0,"unmet":0,"produced":0,"consumed":0,"trade_quantity":0,"trade_value":0}
		for lot in s.inventory:
			if lot.accounting_city_id in city_ids and lot.product_id==product.id:
				row.supply+=int(lot.quantity)
		for event in s.events:
			if int(event.month)!=int(s.last_resolved_month) or event.city_id not in city_ids or event.product_id!=product.id:
				continue
			match str(event.type):
				"demand": row.demand+=int(event.quantity)
				"unmet_need": row.unmet+=int(event.quantity)
				"production_output": row.produced+=int(event.quantity)
				"consumption": row.consumed+=int(event.quantity)
				"trade":
					row.trade_quantity+=int(event.quantity)
					row.trade_value+=int(event.amount)
					result.trades+=1
		if row.trade_quantity>0:
			row.price=int(round(float(row.trade_value)/float(row.trade_quantity)))
			prices_total+=int(round(float(row.price)*100.0/float(product.price)))
			prices_count+=1
		result.production+=row.produced
		result.consumption+=row.consumed
		result.unmet+=row.unmet
		result.inventory_total+=row.supply
		result.per_product.append(row)
	if prices_count>0: result.price_index=int(prices_total/prices_count)
	return result

static func state_hash(s: Dictionary) -> String:
	var normalized: Dictionary = s.duplicate(true)
	if normalized.has("commands"):
		normalized.commands.sort_custom(func(a,b): return str(a.get("id",""))<str(b.get("id","")))
	# Reports contain their own checksum as presentation metadata; omit the field.
	for report in normalized.get("reports",[]):
		report.erase("state_hash")
	normalized.erase("last_report")
	return _canonical_json(normalized).sha256_text()

static func _canonical_json(value: Variant) -> String:
	match typeof(value):
		TYPE_DICTIONARY:
			var keys: Array=value.keys()
			keys.sort_custom(func(a,b): return str(a)<str(b))
			var pairs: PackedStringArray=[]
			for key in keys: pairs.append(JSON.stringify(str(key))+":"+_canonical_json(value[key]))
			return "{"+",".join(pairs)+"}"
		TYPE_ARRAY:
			var items: PackedStringArray=[]
			for item in value: items.append(_canonical_json(item))
			return "["+",".join(items)+"]"
		TYPE_FLOAT:
			if is_finite(value) and value==floor(value) and abs(value)<=9007199254740991.0: return str(int(value))
			return JSON.stringify(value,"",true,true)
		TYPE_STRING_NAME:
			return JSON.stringify(str(value))
		_:
			return JSON.stringify(value)

static func validate(s: Dictionary) -> Array:
	if _fixture().is_empty(): return ["EXPERIMENTAL_CONTENT_INVALID"]
	var errors: Array=[]
	for key in ["schema_version","rules_version","seed","version","planning_month","last_resolved_month","phase","continents","nations","cities","people","companies","facilities","inventory","projects","shipments","products","recipes","commands","journal","accounts","events","reports","used_command_ids"]:
		if not s.has(key): errors.append("MISSING_STATE_FIELD:"+key)
	if not errors.is_empty(): return errors
	# Loading untrusted JSON must fail with diagnostics before a typed lookup.
	for key in ["schema_version","seed","version","planning_month","last_resolved_month"]:
		if not _whole_number(s[key]): errors.append("INVALID_INTEGER_FIELD:"+key)
	for key in ["rules_version","phase"]:
		if typeof(s[key]) not in [TYPE_STRING,TYPE_STRING_NAME]: errors.append("INVALID_STRING_FIELD:"+key)
	for key in ["continents","nations","cities","people","companies","facilities","inventory","projects","products","recipes","commands","journal","events","reports"]:
		if typeof(s[key])!=TYPE_ARRAY: errors.append("INVALID_ARRAY_FIELD:"+key)
		else:
			for item in s[key]:
				if typeof(item)!=TYPE_DICTIONARY: errors.append("INVALID_OBJECT_IN:"+key)
	if typeof(s.accounts)!=TYPE_DICTIONARY: errors.append("INVALID_ACCOUNTS_OBJECT")
	if typeof(s.get("monthly_labor",{}))!=TYPE_DICTIONARY: errors.append("INVALID_LABOR_OBJECT")
	if typeof(s.get("shipments",[]))!=TYPE_ARRAY: errors.append("INVALID_SHIPMENTS_ARRAY")
	else:
		for item in s.get("shipments",[]):
			if typeof(item)!=TYPE_DICTIONARY: errors.append("INVALID_SHIPMENT_OBJECT")
	if typeof(s.used_command_ids)!=TYPE_ARRAY: errors.append("INVALID_USED_COMMAND_IDS")
	else:
		for id in s.used_command_ids:
			if typeof(id) not in [TYPE_STRING,TYPE_STRING_NAME]: errors.append("INVALID_USED_COMMAND_ID")
	if not errors.is_empty(): return errors
	var field_contracts: Dictionary={
		"continents":["id","name"],"nations":["id","name","continent_id"],
		"cities":["id","name","nation_id","continent_id","width","height","roads","resources","resource_capacity","forest_regrowth"],
		"people":["id","name","city_id","company_id","role","hours","wage"],
		"companies":["id","name","city_id","director_id","shareholder_id","policy","revenue","expenses"],
		"facilities":["id","name","company_id","city_id","kind","x","y","w","h","status","recipe_id","capacity"],
		"inventory":["id","owner_id","city_id","accounting_city_id","location","product_id","quantity","reserved"],
		"projects":["id","company_id","city_id","kind","x","y","w","h","status","progress","required_work","months_worked","min_months","materials_used","materials_required"],
		"events":["id","month","type","reason","actor_id","city_id","product_id","quantity","amount"]}
	for collection in field_contracts:
		for item in s[collection]:
			for field in field_contracts[collection]:
				if not item.has(field): errors.append("MISSING_ENTITY_FIELD:"+collection+":"+field)
	if not errors.is_empty(): return errors
	var text_fields: Array=["id","name","city_id","nation_id","continent_id","company_id","director_id","shareholder_id","owner_id","accounting_city_id","location","product_id","role","kind","status","recipe_id","type","reason","actor_id"]
	var number_fields: Array=["x","y","w","h","width","height","hours","wage","capacity","quantity","reserved","progress","required_work","months_worked","min_months","month","amount","revenue","expenses","forest_regrowth","operational_month","completed_month","start_month","budget","spent","last_profit"]
	for collection in field_contracts:
		for item in s[collection]:
			for field in item:
				if str(field) in text_fields and typeof(item[field]) not in [TYPE_STRING,TYPE_STRING_NAME]: errors.append("INVALID_ENTITY_TEXT:"+collection+":"+str(field))
				if str(field) in number_fields and not _whole_number(item[field]): errors.append("INVALID_ENTITY_INTEGER:"+collection+":"+str(field))
	for key in s.get("monthly_labor",{}):
		if typeof(key) not in [TYPE_STRING,TYPE_STRING_NAME] or not _whole_number(s.monthly_labor[key]) or int(s.monthly_labor[key])<0: errors.append("INVALID_LABOR_TIME")
	for shipment in s.get("shipments",[]):
		for field in ["id","owner_id","product_id","from_city_id","to_city_id","status","lot_id"]:
			if typeof(shipment.get(field)) not in [TYPE_STRING,TYPE_STRING_NAME]: errors.append("INVALID_SHIPMENT_REFERENCE")
		for field in ["quantity","eta"]:
			if not _whole_number(shipment.get(field)): errors.append("INVALID_SHIPMENT_INTEGER")
	if not errors.is_empty(): return errors
	for city in s.cities:
		if typeof(city.roads)!=TYPE_ARRAY or typeof(city.resources)!=TYPE_DICTIONARY or typeof(city.resource_capacity)!=TYPE_DICTIONARY: errors.append("INVALID_CITY_SCHEMA")
		else:
			for road in city.roads:
				if typeof(road)!=TYPE_DICTIONARY or not _whole_number(road.get("x")) or not _whole_number(road.get("y")): errors.append("INVALID_ROAD_SCHEMA")
			for key in ["forest","stone"]:
				if not _whole_number(city.resources.get(key)): errors.append("INVALID_NATURAL_RESOURCE")
			if not _whole_number(city.resource_capacity.get("forest")): errors.append("INVALID_RESOURCE_CAPACITY")
	for company in s.companies:
		if typeof(company.policy)!=TYPE_DICTIONARY: errors.append("INVALID_POLICY_SCHEMA")
		else:
			for key in ["production_percent","price_percent","dividend_percent"]:
				if not _whole_number(company.policy.get(key)): errors.append("INVALID_POLICY_SCHEMA")
	for project in s.projects:
		if typeof(project.materials_used)!=TYPE_DICTIONARY or typeof(project.materials_required)!=TYPE_DICTIONARY: errors.append("INVALID_PROJECT_SCHEMA")
		else:
			for product_id in _keys(project.materials_required):
				if typeof(product_id) not in [TYPE_STRING,TYPE_STRING_NAME] or not _whole_number(project.materials_required[product_id]) or not _whole_number(project.materials_used.get(product_id)): errors.append("INVALID_CONSTRUCTION_MATERIAL_QUANTITY")
	for event in s.events:
		if event.has("inventory_delta") and (not _whole_number(event.inventory_delta) or typeof(event.get("lot_id")) not in [TYPE_STRING,TYPE_STRING_NAME]): errors.append("INVALID_INVENTORY_EVENT")
	if not errors.is_empty(): return errors
	if int(s.schema_version)!=1 or s.rules_version!=RULES_VERSION: errors.append("UNSUPPORTED_SCHEMA_OR_RULES")
	if s.continents.size()!=3: errors.append("WORLD_REQUIRES_THREE_CONTINENTS")
	if int(s.planning_month)!=int(s.last_resolved_month)+1: errors.append("MONTH_COMMIT_MISMATCH")
	if int(s.version)!=int(s.last_resolved_month): errors.append("VERSION_MONTH_MISMATCH")
	if int(s.version)<0 or int(s.planning_month)<1 or int(s.last_resolved_month)<0: errors.append("NEGATIVE_MONTH_OR_VERSION")
	if s.phase not in ["Planning","Resolving"]: errors.append("INVALID_PHASE")
	var expected_content_hash: String = _content_hash()
	if s.get("content_hash","")!=expected_content_hash: errors.append("CONTENT_HASH_MISMATCH")
	if JSON.stringify(s.products,"",true)!=JSON.stringify(_product_data(),"",true) or JSON.stringify(s.recipes,"",true)!=JSON.stringify(_recipe_data(),"",true): errors.append("CONTENT_DEFINITION_MISMATCH")
	for collection in ["continents","nations","cities","people","companies","facilities","inventory","projects","products","recipes","events","shipments"]:
		var ids: Dictionary={}
		for item in s[collection]:
			if typeof(item)!=TYPE_DICTIONARY or str(item.get("id","")).is_empty():
				errors.append("MISSING_ID:"+collection)
				continue
			if ids.has(item.id): errors.append("DUPLICATE_ID:"+collection+":"+str(item.id))
			ids[item.id]=true
	for nation in s.nations:
		if _find(s.continents,nation.get("continent_id","")).is_empty(): errors.append("UNKNOWN_CONTINENT")
	for city in s.cities:
		var nation: Dictionary=_find(s.nations,city.get("nation_id",""))
		if nation.is_empty() or nation.get("continent_id","")!=city.get("continent_id",""): errors.append("CITY_NATION_MISMATCH")
		if int(city.get("width",0))<=0 or int(city.get("height",0))<=0: errors.append("INVALID_CITY_SIZE")
		for value in city.get("resources",{}).values():
			if int(value)<0: errors.append("NEGATIVE_NATURAL_RESOURCE")
	for company in s.companies:
		if _find(s.people,company.get("director_id","")).is_empty() or _find(s.cities,company.get("city_id","")).is_empty(): errors.append("COMPANY_REFERENCE_INVALID")
		if not s.accounts.has(str(company.id)+":cash"): errors.append("COMPANY_CASH_ACCOUNT_MISSING")
		elif typeof(s.accounts[str(company.id)+":cash"])!=TYPE_DICTIONARY or s.accounts[str(company.id)+":cash"].get("owner")!=company.id: errors.append("COMPANY_CASH_OWNER_MISMATCH")
	for person in s.people:
		if _find(s.cities,person.get("city_id","")).is_empty() or _find(s.companies,person.get("company_id","")).is_empty(): errors.append("PERSON_REFERENCE_INVALID")
		if int(s.get("monthly_labor",{}).get(person.id,0))>int(person.get("hours",0)): errors.append("LABOR_OVERALLOCATED:"+str(person.id))
		if int(person.get("hours",0))<0: errors.append("NEGATIVE_AVAILABLE_LABOR")
		if int(person.get("wage",0))<0: errors.append("NEGATIVE_WAGE")
		if not s.accounts.has(str(person.id)+":cash"): errors.append("PERSON_CASH_ACCOUNT_MISSING")
		elif typeof(s.accounts[str(person.id)+":cash"])!=TYPE_DICTIONARY or s.accounts[str(person.id)+":cash"].get("owner")!=person.id: errors.append("PERSON_CASH_OWNER_MISMATCH")
	var inventory_sums: Dictionary={}
	for event in s.events:
		if event.has("inventory_delta"):
			if not _whole_number(event.inventory_delta): errors.append("INVALID_INVENTORY_EVENT_QUANTITY")
			var lot_id: String=str(event.get("lot_id",""))
			inventory_sums[lot_id]=int(inventory_sums.get(lot_id,0))+int(event.inventory_delta)
	for lot in s.inventory:
		if typeof(lot.get("quantity")) not in [TYPE_INT,TYPE_FLOAT] or float(lot.quantity)!=floor(float(lot.quantity)): errors.append("INVALID_INVENTORY_QUANTITY")
		if int(lot.get("quantity",-1))<0 or int(lot.get("reserved",-1))<0 or int(lot.get("reserved",0))>int(lot.get("quantity",0)): errors.append("NEGATIVE_OR_OVERRESERVED_INVENTORY:"+str(lot.id))
		if _find(s.products,lot.get("product_id","")).is_empty() or _find(s.cities,lot.get("city_id","")).is_empty() or _find(s.cities,lot.get("accounting_city_id","")).is_empty(): errors.append("INVENTORY_REFERENCE_INVALID")
		if _find(s.people,lot.get("owner_id","")).is_empty() and _find(s.companies,lot.get("owner_id","")).is_empty(): errors.append("UNKNOWN_INVENTORY_OWNER")
		if int(inventory_sums.get(lot.id,0))!=int(lot.quantity): errors.append("INVENTORY_FLOW_MISMATCH:"+str(lot.id))
	for lot_id in inventory_sums:
		if _find(s.inventory,str(lot_id)).is_empty(): errors.append("ORPHAN_INVENTORY_EVENT")
	var occupied: Dictionary={}
	for facility in s.facilities:
		if _find(s.companies,facility.get("company_id","")).is_empty() or _find(s.cities,facility.get("city_id","")).is_empty(): errors.append("FACILITY_REFERENCE_INVALID")
		if not str(facility.get("recipe_id","")).is_empty() and _find(s.recipes,facility.recipe_id).is_empty(): errors.append("UNKNOWN_RECIPE")
		_record_tiles(s,facility,occupied,errors)
	for project in s.projects:
		if project.status in ["active","paused"]: _record_tiles(s,project,occupied,errors)
		if int(project.get("progress",0))>int(project.get("required_work",0)) or int(project.get("progress",0))<0: errors.append("INVALID_PROJECT_PROGRESS")
		for product_id in _keys(project.get("materials_used",{})):
			if int(project.materials_used[product_id])>int(project.materials_required.get(product_id,0)): errors.append("EXCESS_CONSTRUCTION_INPUT")
	for shipment in s.get("shipments",[]):
		var lot: Dictionary=_find(s.inventory,shipment.get("lot_id",""))
		if lot.is_empty(): errors.append("SHIPMENT_LOT_MISSING")
		elif shipment.status=="transit" and (int(lot.quantity)!=int(shipment.quantity) or lot.location!="shipment"): errors.append("SHIPMENT_STOCK_MISMATCH")
	var ledger_errors: Array=Ledger.validate(s)
	for error in ledger_errors: errors.append(error)
	return errors

static func _keys(value: Dictionary) -> Array:
	var keys: Array=value.keys()
	keys.sort_custom(func(a,b): return str(a)<str(b))
	return keys

static func _whole_number(value: Variant) -> bool:
	return typeof(value) in [TYPE_INT,TYPE_FLOAT] and is_finite(float(value)) and float(value)==floor(float(value)) and abs(float(value))<=9007199254740991.0

static func _record_tiles(s: Dictionary, item: Dictionary, occupied: Dictionary, errors: Array) -> void:
	var city: Dictionary=_find(s.cities,item.get("city_id",""))
	if city.is_empty(): return
	var x: int=int(item.get("x",-1))
	var y: int=int(item.get("y",-1))
	var w: int=int(item.get("w",0))
	var h: int=int(item.get("h",0))
	if x<0 or y<0 or w<=0 or h<=0 or x+w>int(city.width) or y+h>int(city.height):
		errors.append("INVALID_TILE_RECT:"+str(item.id))
		return
	for yy in range(y,y+h):
		for xx in range(x,x+w):
			var key: String="%s:%d:%d" % [city.id,xx,yy]
			if occupied.has(key): errors.append("TILE_OVERLAP:"+str(item.id))
			occupied[key]=item.id
			for road in city.get("roads",[]):
				if int(road.x)==xx and int(road.y)==yy: errors.append("FACILITY_ON_ROAD:"+str(item.id))
