extends RefCounted
## Experimental, deterministic, offline monthly economy. No wall-clock callbacks.
const Ledger = preload("res://core/ledger.gd")
const Progress = preload("res://core/progress.gd")
const RULES_VERSION = "experimental-0.2-usd"
const Domain = preload("res://core/domain.gd")
const Finance = preload("res://core/finance.gd")
const Life = preload("res://core/lifecycle.gd")
const Public = preload("res://core/public_sector.gd")
const Industry = preload("res://core/industry.gd")
const Market = preload("res://core/market.gd")
const Land = preload("res://core/land.gd")
const FIXTURE_PATH: String = "res://data/experimental.json"
static var _fixture_cache: Dictionary = {}
static var _fixture_error: String = ""
static var _building_cache: Dictionary={}
static var _content_hash_cache: String=""
static var _command_module_cache: Dictionary={}
static var _plan_cache_state: Dictionary={}
static var _plan_cache_version: int=-1
static var _plan_cache_count: int=-1
static var _plan_cache_last: String=""
static var _plan_cache_totals: Dictionary={}
static var _plan_cache_definitions: Dictionary={}

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
	if data.status!="experimental" or data.version!=2 or data.rules_version!=RULES_VERSION: errors.append("UNSUPPORTED_EXPERIMENTAL_FIXTURE")
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
		if product.get("unit","") not in ["kg","L","m³","kWh","개"]: errors.append("UNSUPPORTED_BASE_PRODUCT_UNIT")
		if product.get("unit_dimension","") != {"kg":"mass","L":"volume","m³":"volume","kWh":"energy","개":"count"}.get(product.get("unit",""),"invalid"): errors.append("INVALID_PRODUCT_UNIT_DIMENSION")
		if not product.get("quantity_note") is String or str(product.get("quantity_note","")).is_empty(): errors.append("MISSING_PRODUCT_QUANTITY_NOTE")
		var mass: Variant=product.get("cargo_mass_g_per_unit")
		if not _whole_number(mass) or not _whole_number(product.get("cargo_size")):
			errors.append("INVALID_PRODUCT_CARGO_MASS")
		elif int(mass)!=int(product.cargo_size) or int(mass)<0 or (str(product.get("kind","material"))=="service" and int(mass)!=0) or (str(product.get("kind","material"))!="service" and int(mass)<=0):
			errors.append("INVALID_PRODUCT_CARGO_MASS")
		if product.get("unit","")=="kg" and _whole_number(mass) and int(mass)!=1000: errors.append("KILOGRAM_PRODUCT_MASS_MISMATCH")
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
		if recipe.has("resource") and (str(recipe.resource).is_empty() or not _whole_number(recipe.get("resource_per_batch")) or int(recipe.get("resource_per_batch",0))<=0): errors.append("INVALID_RECIPE_RESOURCE")
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
	if not p.get("initial_staff_adjustments") is Array: errors.append("INITIAL_STAFF_ADJUSTMENTS_REQUIRED")
	else:
		for staffing in p.initial_staff_adjustments:
			if not staffing is Dictionary or not staffing.get("kind") is String:
				errors.append("INVALID_INITIAL_STAFFING")
				continue
			for key in ["additional_people","hours","wage"]:
				if not _whole_number(staffing.get(key)) or int(staffing.get(key,0))<=0: errors.append("INVALID_INITIAL_STAFFING:"+key)
	var numeric_keys: Array=["default_seed","city_width","city_height","tile_side_m","road_y","employees_per_company","available_hours","hourly_wage","company_opening_cash","person_opening_cash","initial_input_stock","initial_output_stock","initial_output_city_step","initial_maintenance_tools","contractor_width","contractor_height","forest_opening","forest_city_step","stone_opening","stone_city_step","forest_capacity","forest_regrowth","construction_budget","construction_work_per_month","construction_hour_price","construction_stage_foundation","construction_stage_frame","director_hours","director_pay","freight_unit_cost","freight_lead_months","minimum_price","policy_minimum","policy_maximum","price_policy_minimum","price_policy_maximum","stock_target_default","stock_target_essentials","stock_excess_multiple","stock_slow_batches","ai_high_stock","ai_low_stock","ai_price_step","ai_price_minimum","ai_price_maximum","maintenance_period_months","maintenance_tools_per_facility","dividend_cash_floor","cities_per_nation","initial_catalyst_stock","service_reserve_percent","asset_depreciation_months","construction_equipment_capacity","equipment_hours_per_work","essential_forecast_buffer_percent"]
	for key in numeric_keys:
		if not _whole_number(p.get(key)): errors.append("INVALID_OR_MISSING_PARAMETER:"+key)
		elif int(p[key])<0 or (key not in ["default_seed","road_y","initial_output_city_step","forest_city_step","stone_city_step","policy_minimum"] and int(p[key])==0): errors.append("NONPOSITIVE_PARAMETER:"+key)
	for key in ["company_kinds","facility_positions","continent_names","nation_names","city_names","director_names","employee_titles","essential_products","required_initial_services"]:
		if typeof(p.get(key))!=TYPE_ARRAY or p.get(key,[]).is_empty(): errors.append("INVALID_PARAMETER_ARRAY:"+key)
	for key in ["default_policy","starter_materials","household_demand"]:
		if typeof(p.get(key))!=TYPE_DICTIONARY or p.get(key,{}).is_empty(): errors.append("INVALID_PARAMETER_VECTOR:"+key)
	if typeof(p.get("contractor_name"))!=TYPE_STRING or str(p.get("contractor_name","")).is_empty(): errors.append("INVALID_CONTRACTOR_NAME")
	if not errors.is_empty(): return errors
	if p.continent_names.size()!=3 or p.nation_names.size()!=3 or p.city_names.size()<3: errors.append("FIXTURE_REQUIRES_THREE_GEOGRAPHIC_ENTRIES")
	if p.company_kinds.size()!=p.facility_positions.size() or p.company_kinds.size()!=p.director_names.size(): errors.append("FIXTURE_COMPANY_LAYOUT_MISMATCH")
	if p.employee_titles.size()!=int(p.employees_per_company): errors.append("FIXTURE_EMPLOYEE_LAYOUT_MISMATCH")
	for key in ["continent_names","nation_names","city_names","director_names","employee_titles","company_kinds","essential_products","required_initial_services"]:
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
	if _content_hash_cache=="": _content_hash_cache=_canonical_json({"fixture":_fixture(),"industry":Industry.definitions(),"industry_full":Industry._data(),"industry_parameters":Industry.parameters(),"finance":Finance.definition(),"lifecycle":Life.content_hash(),"public":Public.content_hash(),"market":Market.content_hash(),"land":Land.content_hash()}).sha256_text()
	return _content_hash_cache

static func building_types() -> Array:
	var data: Dictionary = _fixture()
	return data.get("buildings",[]).duplicate(true) + Industry.definitions().get("buildings",[]).duplicate(true)

static func _product_data() -> Array:
	var result: Array = _fixture().get("products",[]).duplicate(true)
	var overrides: Dictionary = Industry.definitions().get("product_overrides",{})
	for item in result:
		item["kind"] = "material"
		if overrides.has(item.id): item.merge(overrides[item.id],true)
	result.append_array(Industry.definitions().get("products",[]).duplicate(true))
	return result

static func _recipe_data() -> Array:
	var result: Array = _fixture().get("recipes",[]).duplicate(true)
	var overrides: Dictionary = Industry.definitions().get("recipe_overrides",{})
	for item in result:
		if overrides.has(item.id): item.merge(overrides[item.id],true)
	result.append_array(Industry.definitions().get("recipes",[]).duplicate(true))
	return result

static func world_progress_steps(options: Dictionary = {}) -> int:
	var fixture: Dictionary=_fixture()
	if fixture.is_empty():return 10
	var per_nation: int=int(options.get("cities_per_nation",fixture.parameters.cities_per_nation))
	var extra: int=Industry.definitions().get("buildings",[]).size() if bool(options.get("expanded_industries",fixture.parameters.extended_industries)) else 0
	return 10+3*per_nation+extra

static func new_world(seed_value: Variant = null, options: Dictionary = {}, progress: Variant = null) -> Dictionary:
	var task: RefCounted=Progress.observer(progress)
	task.begin(world_progress_steps(options),"세계 생성 규칙 읽기")
	var fixture: Dictionary = _fixture()
	if fixture.is_empty(): task.fail("EXPERIMENTAL_CONTENT_INVALID","세계 생성 실패"); return {}
	var p: Dictionary = fixture.parameters
	if seed_value == null: seed_value = int(p.default_seed)
	if not _whole_number(seed_value) or int(seed_value)<0: task.fail("INVALID_SEED","세계 생성 실패"); return {}
	var per_nation: int = int(options.get("cities_per_nation",p.cities_per_nation))
	if per_nation not in [1,2]: task.fail("INVALID_WORLD_SIZE","세계 생성 실패"); return {}
	var s: Dictionary = {"schema_version":3,"rules_version":RULES_VERSION,"base_currency":Ledger.BASE_CURRENCY,"minor_units_per_major":Ledger.MINOR_UNITS_PER_MAJOR,"seed":int(seed_value),"version":0,"planning_month":1,"last_resolved_month":0,"phase":"Planning","report_pending":false,"player_id":"p-0","controlled_company_id":"co-0","continents":[],"nations":[],"cities":[],"people":[],"companies":[],"facilities":[],"inventory":[],"projects":[],"shipments":[],"products":_product_data(),"recipes":_recipe_data(),"commands":[],"journal":[],"accounts":{},"events":[],"reports":[],"last_report":{},"used_command_ids":[],"monthly_labor":{},"experimental":true,"id_counters":{},"generation_options":options.duplicate(true)}
	s.content_hash = _content_hash()
	task.advance("세계 설정 검사 완료")
	for continent in range(3):
		var cid: String = "continent-%d" % continent
		var nid: String = "nation-%d" % continent
		s.continents.append({"id":cid,"name":p.continent_names[continent]})
		s.nations.append({"id":nid,"name":p.nation_names[continent],"continent_id":cid,"currency":"USD"})
		for local in range(per_nation):
			var index: int = continent*per_nation+local
			var city_id: String = "city-%d" % index
			var roads: Array = []
			for y in p.road_rows:
				for x in range(int(p.city_width)): roads.append({"x":x,"y":int(y)})
			var resources: Dictionary = Industry.initial_reserves(index)
			resources["forest"] = int(p.forest_opening)+index*int(p.forest_city_step)
			resources["stone"] = int(p.stone_opening)+index*int(p.stone_city_step)
			s.cities.append({"id":city_id,"name":p.city_names[index],"nation_id":nid,"continent_id":cid,"width":int(p.city_width),"height":int(p.city_height),"tile_side_m":int(p.tile_side_m),"roads":roads,"resources":resources,"resource_capacity":{"forest":int(p.forest_capacity)+index*int(p.forest_city_step)},"forest_regrowth":int(p.forest_regrowth)})
			for ki in range(p.company_kinds.size()):
				_seed_company(s,str(p.company_kinds[ki]),city_id,Vector2i(int(p.facility_positions[ki][0]),int(p.facility_positions[ki][1])),index*int(p.company_kinds.size())+ki)
			task.advance("도시·기초 산업 생성 완료: "+str(index+1)+"/"+str(3*per_nation))
	if bool(options.get("expanded_industries",p.extended_industries)):
		var extras: Array = Industry.definitions().get("buildings",[])
		for i in range(extras.size()):
			var city: Dictionary = s.cities[i%s.cities.size()]
			var position: Vector2i = _seed_site(s,city)
			if position.x>=0: _seed_company(s,str(extras[i].id),str(city.id),position,s.companies.size())
			task.advance("확장 산업 배치 완료: "+str(i+1)+"/"+str(extras.size()))
	_seed_required_services(s)
	task.advance("교육 등 필수 서비스 배치 완료")
	_seed_balancing_staff(s)
	task.advance("초기 고용 배치 완료")
	Public.initialize(s)
	task.advance("공공기관 초기화 완료")
	Life.initialize(s)
	task.advance("인물·법인 초기화 완료")
	Industry.initialize(s)
	task.advance("산업·설비 초기화 완료")
	Land.initialize(s)
	task.advance("토지·권리 초기화 완료")
	Finance.initialize(s)
	task.advance("은행·개시 장부 초기화 완료")
	Market.initialize(s)
	task.advance("유한 시장 초기화 완료")
	s["initial_metrics"] = {"people":s.people.size(),"companies":s.companies.size(),"products":s.products.size(),"cities":s.cities.size()}
	task.finish("세계 생성 완료")
	return s

static func _seed_required_services(s: Dictionary) -> void:
	for city in s.cities:
		for kind in _parameters().required_initial_services:
			var exists: bool=false
			for facility in s.facilities:
				if facility.city_id==city.id and facility.kind==kind: exists=true; break
			if exists: continue
			var position: Vector2i=_seed_site(s,city)
			if position.x<0 or _building(str(kind)).is_empty():
				push_error("INITIAL_SERVICE_SITE_REQUIRED:"+str(city.id)+":"+str(kind))
				continue
			_seed_company(s,str(kind),str(city.id),position,s.companies.size())

static func _seed_balancing_staff(s: Dictionary) -> void:
	# These are explicit opening people/contracts, never a runtime rescue.
	# Append after the baseline roster so existing identities keep their IDs.
	for adjustment in _parameters().initial_staff_adjustments:
		for facility in s.facilities:
			if facility.kind!=adjustment.kind: continue
			var city: Dictionary=_find(s.cities,str(facility.city_id))
			for n in range(int(adjustment.additional_people)):
				var id: String="p-%d" % s.people.size()
				s.people.append({"id":id,"name":str(city.name)+" 추가 운영자 "+str(n+1),"city_id":city.id,"nationality":city.nation_id,"company_id":facility.company_id,"role":"operator","hours":int(adjustment.hours),"wage":int(adjustment.wage),"employment_status":"employed","last_income":0,"last_consumption":0,"opening_assignment_reason":"필수 식품 생산의 실제 노동시간 충족"})
				Ledger.opening(s,id,int(_parameters().person_opening_cash),0)

static func _seed_company(s: Dictionary, kind: String, city_id: String, tile: Vector2i, index: int) -> void:
	var p: Dictionary = _parameters()
	var def: Dictionary = _building(kind)
	var co: String = "co-%d" % index
	var first_person: int = s.people.size()
	var pid: String = "p-%d" % first_person
	var city: Dictionary = _find(s.cities,city_id)
	var label: String = str(p.contractor_name) if kind=="builders" else str(def.get("name",kind))
	s.companies.append({"id":co,"name":str(city.name)+" "+label,"city_id":city_id,"director_id":pid,"shareholder_id":pid,"policy":p.default_policy.duplicate(true),"opening_cash":int(p.company_opening_cash),"revenue":0,"expenses":0,"last_profit":0,"status":"active"})
	Ledger.opening(s,co,int(p.company_opening_cash),0)
	for i in range(int(p.employees_per_company)):
		var id: String = "p-%d" % (first_person+i)
		s.people.append({"id":id,"name":"%s %s%d" % [p.director_names[index%p.director_names.size()],p.employee_titles[i],index+1],"city_id":city_id,"nationality":city.nation_id,"company_id":co,"role":"director" if i==0 else ("builder" if kind=="builders" else "operator"),"hours":int(p.available_hours),"wage":int(Life._p().executive_hourly_wage) if i==0 else int(p.hourly_wage),"employment_status":"employed","last_income":0,"last_consumption":0})
		Ledger.opening(s,id,int(p.person_opening_cash),0)
	s.facilities.append({"id":"f-%d" % index,"name":label,"company_id":co,"city_id":city_id,"kind":kind,"x":tile.x,"y":tile.y,"w":int(p.contractor_width) if kind=="builders" else int(def.w),"h":int(p.contractor_height) if kind=="builders" else int(def.h),"status":"operational","recipe_id":str(def.get("recipe_id","")),"capacity":int(def.get("capacity",0)),"operational_month":1,"condition":100})
	if kind=="builders": _change_inventory(s,co,city_id,"machinery",1,"opening",0)
	if kind!="builders":
		var recipe: Dictionary = _find(s.recipes,str(def.recipe_id))
		for product in _keys(recipe.get("inputs",{})):
			if not Industry.is_service(s,product): _change_inventory(s,co,city_id,product,int(p.initial_input_stock),"opening",0)
		for product in _keys(recipe.get("outputs",{})):
			if not Industry.is_service(s,product) and _find(s.products,product).get("kind")!="digital": _change_inventory(s,co,city_id,product,int(p.initial_output_stock),"opening",0)
		for product in _keys(recipe.get("catalysts",{})): _change_inventory(s,co,city_id,product,int(p.initial_catalyst_stock)*int(recipe.catalysts[product]),"opening",0)
	_change_inventory(s,co,city_id,"tools",int(p.initial_maintenance_tools),"opening",0)
	if kind=="bakery":
		for product in _keys(p.starter_materials): _change_inventory(s,co,city_id,product,int(p.starter_materials[product]),"opening",0)

static func _seed_site(s: Dictionary, city: Dictionary) -> Vector2i:
	var tutorial: Dictionary=Land.definition()
	for y in range(int(city.height)-1):
		for x in range(int(city.width)-1):
			if _rect_overlap({"x":x,"y":y,"w":2,"h":2},{"x":int(tutorial.tutorial_x),"y":int(tutorial.tutorial_y),"w":int(tutorial.plot_width),"h":int(tutorial.plot_height)}): continue
			var payload: Dictionary={"city_id":city.id,"x":x,"y":y,"kind":"bakery"}
			var owner: String=""
			for co in s.companies:
				if co.city_id==city.id: owner=co.id; break
			if _site_error(s,payload,owner,false)=="": return Vector2i(x,y)
	return Vector2i(-1,-1)

static func new_game_options() -> Dictionary:
	return {"fields":[{"key":"cities_per_nation","label":"국가별 도시 수","kind":"choice","default":2,"options":[{"label":"2개 도시 · 국내/국제 교역","value":2},{"label":"1개 도시 · 작은 세계","value":1}]},{"key":"expanded_industries","label":"산업 구성","kind":"choice","default":true,"options":[{"label":"전체 산업 공급망","value":true},{"label":"핵심 산업 연습","value":false}]}]}

static func construction_budget(kind: String) -> int:
	var def: Dictionary=_building(kind)
	var total: int=int(def.get("work",0))*int(_parameters().construction_hour_price)
	for product in ["wood","stone","tools"]:
		var item: Dictionary=_find(_product_data(),product)
		total+=int(def.get(product,0))*(int(item.get("price",0))*2+int(_parameters().freight_unit_cost))
	return maxi(int(_parameters().construction_budget),total)

static func _command_module(type: String) -> Variant:
	if _command_module_cache.is_empty():
		for module in command_modules():
			for command_type in module.command_types(): _command_module_cache[command_type]=module
	return _command_module_cache.get(type)

static func planning_reservations(s: Dictionary, c: Dictionary) -> Array:
	var rows: Array=[]
	var module: Variant=_command_module(str(c.get("type","")))
	if module!=null: rows=module.planning_reservations(s,c)
	elif c.get("type")=="build": rows=[{"owner_id":str(c.acting_company_id),"resource_type":"cash","quantity":construction_budget(str(c.payload.get("kind","")))}]
	var company: Dictionary=_find(s.get("companies",[]),str(c.get("acting_company_id","")))
	if not company.is_empty() and company.get("director_id","")!=c.get("actor_id",""):
		var bound: int=0
		for r in rows:
			if r.owner_id==company.id and r.resource_type=="cash": bound+=int(r.quantity)
		if bound>0:
			var applicable: bool=false
			var chosen: String=""
			for grant in s.get("lifecycle",{}).get("delegations",[]):
				if grant.get("status")!="active" or grant.get("person_id")!=c.actor_id or grant.get("company_id")!=company.id or c.type not in grant.get("permissions",[]): continue
				applicable=true
				if grant.get("grantor_id")==company.director_id and Life.delegated_budget_remaining(s,grant)>=bound:
					chosen=str(grant.id)
					break
			if applicable: rows.append({"owner_id":str(c.actor_id),"resource_type":"delegation","resource_id":chosen,"quantity":bound})
	return rows

static func _reservation_key(r: Dictionary) -> String:
	var kind: String=str(r.resource_type)
	var resource: String=str(r.get("resource_id",r.get("currency","USD" if kind=="cash" else "")))
	if kind=="labor": resource="hours"
	return str(r.owner_id)+"|"+kind+"|"+resource+"|"+str(r.get("city_id",""))

static func _cache_plan_rows(rows: Array) -> void:
	for r in rows:
		if r.get("existing_reservation",false): continue
		var key: String=_reservation_key(r)
		_plan_cache_totals[key]=int(_plan_cache_totals.get(key,0))+maxi(0,int(r.get("quantity",0)))
		_plan_cache_definitions[key]=r

static func _ensure_plan_cache(s: Dictionary) -> void:
	var last: String=str(s.commands[-1].id) if not s.commands.is_empty() else ""
	if is_same(_plan_cache_state,s) and _plan_cache_version==int(s.version) and _plan_cache_count==s.commands.size() and _plan_cache_last==last: return
	# Only a transient planning accelerator. A fresh immutable clone rebuilds it
	# during every turn's full command revalidation; no cache enters a save/hash.
	_plan_cache_state=s
	_plan_cache_version=int(s.version)
	_plan_cache_count=s.commands.size()
	_plan_cache_last=last
	_plan_cache_totals={}
	_plan_cache_definitions={}
	for queued in s.commands: _cache_plan_rows(planning_reservations(s,queued))

static func _reservation_error(s: Dictionary, c: Dictionary) -> String:
	var rows: Array=planning_reservations(s,c)
	if rows.is_empty(): return ""
	_ensure_plan_cache(s)
	var additions: Dictionary={}
	var definitions: Dictionary={}
	for r in rows:
		if r.get("existing_reservation",false): continue
		var key: String=_reservation_key(r)
		additions[key]=int(additions.get(key,0))+maxi(0,int(r.get("quantity",0)))
		definitions[key]=r
	for key in additions:
		var r: Dictionary=definitions[key]
		var owner: String=str(r.owner_id)
		var available: int=0
		match str(r.resource_type):
			"cash":
				var currency: String=str(r.get("resource_id",r.get("currency","USD")))
				available=Ledger.balance(s,owner+":cash" if currency=="USD" else owner+":cash:"+currency)
				if currency=="USD": available-=Public.reserved_cash(s,owner)
			"labor": available=int(_find(s.people,owner).get("hours",0))
			"inventory": available=available_quantity(s,owner,str(r.get("resource_id","")),str(r.get("city_id","")))
			"delegation": available=Life.delegated_budget_remaining(s,_find(s.get("lifecycle",{}).get("delegations",[]),str(r.get("resource_id",""))))
			"shares": available=Finance.available_shares(s,owner,str(r.get("resource_id","")))
		if int(_plan_cache_totals.get(key,0))+int(additions[key])>available: return "SELF_RESERVATION_EXCEEDS_"+str(r.resource_type).to_upper()+":"+owner
	return ""

static func _append_planned_command(s: Dictionary, c: Dictionary) -> void:
	_ensure_plan_cache(s)
	s.commands.append(c.duplicate(true))
	_cache_plan_rows(planning_reservations(s,c))
	_plan_cache_count=s.commands.size()
	_plan_cache_last=str(c.id)

static func preflight_summary(s: Dictionary, actor_id: String, company_id: String) -> Dictionary:
	var owner: String=company_id if company_id!="" else actor_id
	var reserved: int=0
	var commands: Array=[]
	for c in s.get("commands",[]):
		if c.actor_id!=actor_id: continue
		if str(c.get("acting_company_id",""))==company_id: commands.append(c)
		for r in planning_reservations(s,c):
			if r.owner_id==owner and r.resource_type=="cash" and not r.get("existing_reservation",false): reserved+=int(r.quantity)
	var hours: int=0
	var builders: int=0
	for person in s.people:
		if person.company_id==owner or person.id==owner: hours+=int(person.hours)
		if person.city_id==Domain.owner_city(s,owner) and person.role=="builder": builders+=int(person.hours)
	var cash_limit: int=maxi(0,cash(s,owner)-Public.reserved_cash(s,owner))
	var company: Dictionary=_find(s.companies,company_id)
	if not company.is_empty() and company.get("director_id","")!=actor_id:
		var delegated: int=0
		for grant in s.get("lifecycle",{}).get("delegations",[]):
			if grant.get("person_id")==actor_id and grant.get("company_id")==owner and grant.get("status")=="active": delegated=maxi(delegated,Life.delegated_budget_remaining(s,grant))
		cash_limit=mini(cash_limit,delegated)
	return {"available_cash":maxi(0,cash_limit-reserved),"reserved_cash":reserved,"expected_spending":reserved,"confirmed_spending":0,"available_hours":hours,"construction_labor":builders,"issues":[],"commands":commands}

static func command_modules() -> Array:
	return [Life,Finance,Public,Industry,Market,Land]

static func command_descriptors() -> Array:
	var actions: Array=[]
	for module in command_modules(): actions.append_array(module.ui_actions())
	return actions

static func _building(kind: String) -> Dictionary:
	if _building_cache.is_empty():
		for definition in building_types(): _building_cache[str(definition.id)]=definition
	return _building_cache.get(kind,{})

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if str(item.get("id", "")) == id:
			return item
	return {}

static func cash(s: Dictionary, owner_id: String) -> int:
	return Ledger.balance(s, owner_id + ":cash")

static func available_quantity(s: Dictionary, owner_id: String, product_id: String, city_id: String = "") -> int:
	return Domain.available_quantity(s,owner_id,product_id,city_id)

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

static func _change_inventory(s: Dictionary, owner_id: String, city_id: String, product_id: String, delta: int, reason: String, month: int, event_type: String = "inventory") -> Dictionary:
	var result: Dictionary = Domain.change_inventory(s,owner_id,city_id,product_id,delta,reason,month,event_type)
	if not result.get("ok",false): _event(s,"inventory_failed",str(result.error),month,{"actor_id":owner_id,"city_id":city_id,"product_id":product_id,"quantity":absi(delta)})
	return result

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
	var x: int = int(payload.get("x",-1))
	var y: int = int(payload.get("y",-1))
	var definition: Dictionary=_building(str(payload.get("kind","")))
	if definition.is_empty(): return "UNKNOWN_BUILDING"
	var w: int=int(definition.w)
	var h: int=int(definition.h)
	var rect: Dictionary = {"x":x,"y":y,"w":w,"h":h,"kind":str(payload.get("kind",""))}
	if x < 0 or y < 0 or x + w > int(city.width) or y + h > int(city.height):
		return "OUT_OF_BOUNDS"
	if s.has("land") and not Land.can_build(s,company_id,str(city.id),rect): return Land.rights_error(s,company_id,str(city.id),rect)
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
	if typeof(command.payload) != TYPE_DICTIONARY: return {"ok":false,"error":"INVALID_PAYLOAD"}
	if _find(s.people,str(command.actor_id)).is_empty(): return {"ok":false,"error":"UNKNOWN_ACTOR"}
	for module in command_modules():
		if str(command.type) in module.command_types():
			var message: String = module.validate_command(s,command)
			if message!="": return {"ok":false,"error":message}
			var reservation_error: String=_reservation_error(s,command)
			if reservation_error!="": return {"ok":false,"error":reservation_error}
			_append_planned_command(s,command)
			return {"ok":true,"error":""}
	var company: Dictionary = _find(s.companies, str(command.acting_company_id))
	var actor: Dictionary = _find(s.people, str(command.actor_id))
	if company.is_empty() or actor.is_empty():
		return {"ok":false,"error":"UNKNOWN_ACTOR_OR_COMPANY"}
	if not Life.can_act(s,str(actor.id),str(company.id),str(command.type),command.payload):
		return {"ok":false,"error":"AUTHORITY_REQUIRED"}
	if company.get("status","active") != "active":
		return {"ok":false,"error":"COMPANY_INACTIVE"}
	if typeof(command.payload) != TYPE_DICTIONARY:
		return {"ok":false,"error":"INVALID_PAYLOAD"}
	var payload: Dictionary = command.payload
	match str(command.type):
		"set_policy":
			if payload.has("dividend_percent") and not Finance.capital_authority(s,str(actor.id),str(company.id)): return {"ok":false,"error":"CONTROLLING_VOTE_REQUIRED"}
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
					reserved += construction_budget(str(queued.payload.get("kind","")))
			if cash(s,company.id) - reserved < construction_budget(str(payload.kind)):
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
	var reservation_error: String=_reservation_error(s,command)
	if reservation_error!="": return {"ok":false,"error":reservation_error}
	_append_planned_command(s,command)
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
	var sites: Dictionary={}
	if s.has("_runtime"):
		sites=s._runtime.suppliers.get(product_id,{})
	else:
		for facility in s.facilities:
			var recipe: Dictionary=_find(s.recipes,str(facility.recipe_id))
			if recipe.get("outputs",{}).has(product_id): sites[str(facility.company_id)+"|"+str(facility.city_id)]={"company_id":facility.company_id,"city_id":facility.city_id}
	for site in sites.values():
		var company: Dictionary=s._runtime.companies.get(str(site.company_id),{}) if s.has("_runtime") else _find(s.companies,str(site.company_id))
		if company.id==buyer or company.get("status","active")!="active": continue
		if available_quantity(s,str(company.id),product_id,str(site.city_id))>0:
			sellers.append({"id":company.id,"city_id":site.city_id,"price":_effective_price(s,str(company.id),product_id),"remote":site.city_id!=city_id})
	sellers.sort_custom(func(a,b):
		var a_cost: int = int(a.price) + (int(_parameters().freight_unit_cost) if a.remote else 0)
		var b_cost: int = int(b.price) + (int(_parameters().freight_unit_cost) if b.remote else 0)
		if a_cost != b_cost:
			return a_cost < b_cost
		return _stable_key(s,"seller",a.id + buyer + product_id) < _stable_key(s,"seller",b.id + buyer + product_id)
	)
	return sellers

static func _contractor(s: Dictionary, city_id: String) -> Dictionary:
	var candidates: Array=[]
	for facility in s.facilities:
		if facility.city_id!=city_id or facility.kind!="builders" or facility.get("status","") not in ["operational","ready"]: continue
		var company: Dictionary=_find(s.companies,str(facility.company_id))
		if not company.is_empty() and company.get("status","active")=="active" and company not in candidates: candidates.append(company)
	if candidates.is_empty(): return {}
	return _sorted(s,candidates,"contractor")[0]

static func _purchase(s: Dictionary, buyer: String, city_id: String, product_id: String, desired: int, month: int, reason: String, budget_limit: int = -1) -> int:
	if desired<=0: return 0
	if Industry.is_service(s,product_id): return int(Industry.purchase_service(s,buyer,city_id,product_id,desired,month,-1,reason=="생산 투입물",budget_limit).get("quantity",0))
	var acquired: int=0
	var spent: int=0
	_event(s,"demand",reason,month,{"actor_id":buyer,"city_id":city_id,"product_id":product_id,"quantity":desired})
	for seller in _sellers(s,buyer,city_id,product_id):
		var remote: bool=bool(seller.remote)
		var price: int=int(seller.price)
		var quantity: int=mini(desired-acquired,available_quantity(s,seller.id,product_id,seller.city_id))
		var rule: Dictionary=Public.trade_rule(s,str(seller.city_id),city_id,product_id,month)
		if not rule.get("allowed",true): continue
		var quote: Dictionary={"allowed":true,"unit_fee":0,"fee":0,"eta":month,"quantity":quantity}
		if remote: quote=Industry.route_quote(s,str(seller.city_id),city_id,product_id,quantity,month)
		if not quote.get("allowed",false): continue
		quantity=mini(quantity,int(quote.get("quantity",quantity)))
		var tariff_bps: int=int(rule.get("tariff_bps",0))
		var budget: int=cash(s,buyer) if budget_limit<0 else mini(cash(s,buyer),maxi(0,budget_limit-spent))
		var upper: int=mini(quantity,int(budget/maxi(1,price)))
		var lower: int=1
		var affordable: int=0
		# Tonne freight rounds once for the whole shipment. Find the largest
		# affordable actual quantity, re-quoting every candidate and the final
		# reduced amount instead of multiplying an indicative unit fee.
		while lower<=upper:
			var candidate: int=int((lower+upper)/2)
			var candidate_quote: Dictionary=Industry.route_quote(s,str(seller.city_id),city_id,product_id,candidate,month) if remote else {"allowed":true,"fee":0,"quantity":candidate,"eta":month}
			var candidate_tariff: int=int(ceil(float(price*candidate)*tariff_bps/10000.0))
			var candidate_cost: int=price*candidate+int(candidate_quote.get("fee",0))+candidate_tariff
			if candidate_quote.get("allowed",false) and int(candidate_quote.get("quantity",0))==candidate and candidate_cost<=budget:
				affordable=candidate
				lower=candidate+1
			else: upper=candidate-1
		quantity=affordable
		if quantity<=0: continue
		quote=Industry.route_quote(s,str(seller.city_id),city_id,product_id,quantity,month) if remote else {"allowed":true,"fee":0,"quantity":quantity,"eta":month}
		if not quote.get("allowed",false) or int(quote.get("quantity",0))!=quantity: continue
		var freight_total: int=int(quote.get("fee",0))
		var tariff: int=int(ceil(float(price*quantity)*tariff_bps/10000.0))
		var age: Dictionary=Industry.inventory_age(s,str(seller.id),str(seller.city_id),product_id,quantity)
		if remote:
			var booked: Dictionary=Industry.reserve_route(s,buyer,str(seller.city_id),city_id,product_id,quantity,month,quote)
			if not booked.get("ok",false): continue
		if not Finance.settle_trade(s,"trade-%d-%d" % [month,s.journal.size()],buyer,str(seller.id),quantity*price,product_id,quantity,month).get("ok",false): continue
		if tariff>0 and str(rule.get("tariff_receiver",""))!="": _pay(s,buyer,str(rule.tariff_receiver),tariff,"관세 납부",month,{"city_id":city_id})
		_change_inventory(s,str(seller.id),str(seller.city_id),product_id,-quantity,"판매 출고",month,"trade_out")
		if remote:
			var sid: String="ship-%d-%d" % [month,s.shipments.size()]
			var lid: String="lot:"+sid
			var lot: Dictionary={"id":lid,"owner_id":buyer,"city_id":seller.city_id,"accounting_city_id":city_id,"location":"shipment","product_id":product_id,"quantity":quantity,"reserved":0,"shipment_id":sid,"unit_cost":price,"born_month":int(age.born_month)}
			if int(age.get("expires_month",0))>0: lot.expires_month=int(age.expires_month)
			s.inventory.append(lot)
			Domain.register_lot(s,lot)
			_event(s,"shipment","운송 중 소유권 이전",month,{"actor_id":buyer,"city_id":city_id,"product_id":product_id,"quantity":quantity,"inventory_delta":quantity,"lot_id":lid})
			s.shipments.append({"id":sid,"owner_id":buyer,"product_id":product_id,"quantity":quantity,"from_city_id":seller.city_id,"to_city_id":city_id,"start_month":month,"eta":int(quote.get("eta",month+1))+int(rule.get("delay",0)),"status":"transit","lot_id":lid,"route_ids":quote.get("path",[]),"carrier_id":str(quote.get("carrier_id",""))})
		else:
			var incoming: Dictionary=_change_inventory(s,buyer,city_id,product_id,quantity,"매입 입고",month,"trade_in")
			for lot_id in incoming.get("lot_ids",[]):
				var lot: Dictionary=_find(s.inventory,str(lot_id))
				lot.born_month=int(age.born_month)
				if int(age.get("expires_month",0))>0: lot.expires_month=mini(int(lot.get("expires_month",age.expires_month)),int(age.expires_month))
		_event(s,"trade",reason,month,{"actor_id":buyer,"seller_id":seller.id,"city_id":city_id,"seller_city_id":seller.city_id,"product_id":product_id,"quantity":quantity,"amount":quantity*price,"unit_price":price,"remote":remote,"foreign":_find(s.cities,str(seller.city_id)).nation_id!=_find(s.cities,city_id).nation_id})
		spent+=quantity*price+freight_total+tariff
		acquired+=quantity
		if acquired>=desired: break
	if acquired<desired: _event(s,"shortage","예산·규격·가용 재고·운송 제약",month,{"actor_id":buyer,"city_id":city_id,"product_id":product_id,"quantity":desired-acquired})
	return acquired

static func _arrive_shipments(s: Dictionary, month: int) -> void:
	Industry.receive_due_shipments(s,month)

static func _work_available(s: Dictionary, company_id: String, role: String = "operator", city_id: String = "") -> int:
	var total: int = 0
	for person in s.people:
		if person.company_id == company_id and person.role == role and (city_id=="" or person.city_id==city_id):
			total += maxi(0,int(person.hours) - int(s.monthly_labor.get(person.id,0)))
	return total

static func _work(s: Dictionary, company_id: String, hours: int, month: int, role: String = "operator", city_id: String = "", fee_owner: String = "", fee_amount: int = 0) -> bool:
	if hours<=0: return true
	var assignments: Array=[]
	var remaining: int=hours
	var wages: int=0
	for person in _sorted(s,s.people,"workers"):
		if person.company_id!=company_id or person.role!=role or (city_id!="" and person.city_id!=city_id): continue
		var used: int=mini(remaining,maxi(0,int(person.hours)-int(s.monthly_labor.get(person.id,0))))
		if used>0:
			assignments.append({"person":person,"hours":used,"wage":used*int(person.wage)})
			wages+=used*int(person.wage)
			remaining-=used
		if remaining<=0: break
	if remaining>0 or cash(s,company_id)+fee_amount<wages: return false
	if fee_amount>0 and (fee_owner=="" or cash(s,fee_owner)<fee_amount): return false
	var lines: Array=[]
	if fee_amount>0:
		lines.append_array([{"account":fee_owner+":expense","debit":fee_amount,"credit":0},{"account":fee_owner+":cash","debit":0,"credit":fee_amount},{"account":company_id+":cash","debit":fee_amount,"credit":0},{"account":company_id+":revenue","debit":0,"credit":fee_amount}])
	for a in assignments:
		if int(a.wage)>0: lines.append_array([{"account":company_id+":expense","debit":int(a.wage),"credit":0},{"account":company_id+":cash","debit":0,"credit":int(a.wage)},{"account":str(a.person.id)+":cash","debit":int(a.wage),"credit":0},{"account":str(a.person.id)+":revenue","debit":0,"credit":int(a.wage)}])
	if not lines.is_empty():
		var posted: Dictionary=Ledger.post(s,"work-%d-%d" % [month,s.journal.size()],lines,"공정대금·실제 작업 임금 동시 결제" if fee_amount>0 else "실제 작업 임금 동시 결제",month)
		if not posted.get("ok",false): return false
	var employer: Dictionary=_find(s.companies,company_id)
	if not employer.is_empty(): employer.expenses=int(employer.expenses)+wages; employer.revenue=int(employer.revenue)+fee_amount
	if fee_amount>0:
		var owner: Dictionary=_find(s.companies,fee_owner)
		if not owner.is_empty(): owner.expenses=int(owner.expenses)+fee_amount
		_event(s,"payment","공정 도급 대금",month,{"actor_id":fee_owner,"payer_id":fee_owner,"payee_id":company_id,"city_id":city_id,"amount":fee_amount})
	for a in assignments:
		a.person.last_income=int(a.person.last_income)+int(a.wage)
		s.monthly_labor[a.person.id]=int(s.monthly_labor.get(a.person.id,0))+int(a.hours)
		_event(s,"payment","근로 임금",month,{"actor_id":company_id,"payer_id":company_id,"payee_id":a.person.id,"city_id":a.person.city_id,"amount":a.wage,"hours":a.hours})
		_event(s,"labor","건설 작업" if role=="builder" else "생산 작업",month,{"actor_id":a.person.id,"company_id":company_id,"city_id":a.person.city_id,"hours":a.hours,"amount":int(a.wage),"wage_paid":true})
	if role=="operator": Finance.record_labor(s,company_id,wages,month)
	return true

static func _produce(s: Dictionary, facility: Dictionary, month: int) -> void:
	if facility.get("status","") not in ["operational","ready"] or int(facility.get("operational_month",1))>month or str(facility.recipe_id).is_empty(): return
	var company: Dictionary=_find(s.companies,str(facility.company_id))
	if company.is_empty() or company.get("status","active")!="active": return
	if s.has("land") and not Land.can_operate(s,str(company.id),str(facility.city_id),facility):
		_event(s,"production_limit","부지 소유·임대 사용권이 유효하지 않음",month,{"actor_id":company.id,"city_id":facility.city_id,"facility_id":facility.id})
		return
	facility.status="operational"
	var recipe: Dictionary=_find(s.recipes,str(facility.recipe_id))
	var gate: Dictionary=Industry.process_gate(s,facility,recipe,month)
	if not gate.get("allowed",false):
		_event(s,"production_limit",str(gate.get("reason","공정 제약")),month,{"actor_id":company.id,"city_id":facility.city_id,"facility_id":facility.id})
		return
	var target: int=mini(int(gate.get("batch_limit",facility.capacity)),int(int(facility.capacity)*int(company.policy.production_percent)/100))
	if facility.kind=="school": target=mini(target,maxi(0,int(facility.capacity)-Life.training_batches_used(s,str(facility.id),month)))
	var output: String=str(recipe.outputs.keys()[0])
	if not Industry.is_service(s,output):
		var stock_target: int=int(_parameters().stock_target_essentials) if output in _parameters().essential_products else int(_parameters().stock_target_default)
		var held: int=available_quantity(s,str(company.id),output,str(facility.city_id))
		if _parameters().household_demand.has(output):
			var population: int=0
			for person in s.people:
				if person.city_id==facility.city_id: population+=1
			var forecast: int=int(ceil(float(population*int(_parameters().household_demand[output]))*float(100+int(_parameters().essential_forecast_buffer_percent))/100.0))
			var required: int=maxi(0,forecast+stock_target-held)
			target=mini(target,int(ceil(float(required)/int(recipe.outputs[output]))))
		elif held>=stock_target*int(_parameters().stock_excess_multiple): target=0
		elif held>=stock_target: target=mini(target,int(_parameters().stock_slow_batches))
	elif month>1:
		var sold: int=0
		for e in s.get("last_report",{}).get("events",[]):
			if e.get("type")=="service_trade" and e.get("seller_id")==company.id and e.get("product_id")==output: sold+=int(e.quantity)
		var forecast: int=int(ceil(float(sold)*float(100+int(_parameters().service_reserve_percent))/100.0))
		if sold>0: target=mini(target,maxi(1,int(ceil(float(forecast)/int(recipe.outputs[output])))))
	if target<=0: return
	for product in _keys(recipe.inputs):
		var needed: int=Industry.input_quantity(s,facility,recipe,product,target)
		var available: int=Industry.service_available(s,str(company.id),product,str(facility.city_id)) if Industry.is_service(s,product) else available_quantity(s,str(company.id),product,str(facility.city_id))
		if needed>available: _purchase(s,str(company.id),str(facility.city_id),product,needed-available,month,"생산 투입물")
	var labor: int=Industry.labor_per_batch(s,facility,recipe)
	var batches: int=mini(target,int(_work_available(s,str(company.id),"operator",str(facility.city_id))/maxi(1,labor)))
	var constraint: String="운영 인력 시간 부족"
	for product in _keys(recipe.inputs):
		var available: int=Industry.service_available(s,str(company.id),product,str(facility.city_id)) if Industry.is_service(s,product) else available_quantity(s,str(company.id),product,str(facility.city_id))
		while batches>0 and available<Industry.input_quantity(s,facility,recipe,product,batches): batches-=1; constraint="투입 부족: "+str(_find(s.products,product).name)
	if recipe.has("resource"):
		batches=mini(batches,int(Industry.resource_available(s,str(facility.city_id),str(recipe.resource))/int(recipe.resource_per_batch)))
	var wage: int=int(_parameters().hourly_wage)
	batches=mini(batches,int(cash(s,str(company.id))/maxi(1,labor*wage)))
	if batches<target: _event(s,"production_limit",constraint,month,{"actor_id":company.id,"city_id":facility.city_id,"facility_id":facility.id,"product_id":output,"quantity":target-batches})
	if batches<=0 or not _work(s,str(company.id),batches*labor,month,"operator",str(facility.city_id)): return
	for product in _keys(recipe.inputs):
		var used: int=Industry.input_quantity(s,facility,recipe,product,batches)
		if Industry.is_service(s,product):
			Industry.consume_service(s,str(company.id),str(facility.city_id),product,used,month)
			_event(s,"service_input","공정 서비스 투입",month,{"actor_id":company.id,"city_id":facility.city_id,"product_id":product,"quantity":used})
		else: _change_inventory(s,str(company.id),str(facility.city_id),product,-used,"공정 투입",month,"production_input")
	if recipe.has("resource"): Industry.consume_resource(s,str(facility.city_id),str(recipe.resource),batches*int(recipe.resource_per_batch),month,str(company.id))
	Industry.record_output(s,facility,recipe,batches,month)
	_event(s,"production","실제 투입·설비·노동의 공정",month,{"actor_id":company.id,"city_id":facility.city_id,"facility_id":facility.id,"recipe_id":recipe.id,"batches":batches,"hours":batches*labor})

static func _ai_commands(s: Dictionary) -> void:
	var p: Dictionary=_parameters()
	for company in s.companies:
		if company.director_id == s.player_id or str(company.director_id)=="" or company.get("status","active") != "active":
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

static func _charge_grant(s: Dictionary, grant_id: String, amount: int, month: int) -> void:
	var grant: Dictionary=_find(s.get("lifecycle",{}).get("delegations",[]),grant_id)
	if grant.is_empty(): return
	if int(grant.get("month",0))!=month: grant.month=month; grant.spent=0
	grant.spent=int(grant.spent)+amount

static func _execute_commands(s: Dictionary, month: int) -> void:
	for command in _sorted(s,s.commands,"commands"):
		var grant_before: Dictionary={}
		var delegation_applies: bool=false
		var delegated_cost: int=0
		for r in planning_reservations(s,command):
			if r.owner_id==command.acting_company_id and r.resource_type=="cash": delegated_cost+=int(r.quantity)
		for grant in s.get("lifecycle",{}).get("delegations",[]):
			if grant.get("status")=="active" and grant.get("person_id")==command.actor_id and grant.get("company_id")==command.acting_company_id and command.type in grant.get("permissions",[]):
				delegation_applies=true
				if Life.delegated_budget_remaining(s,grant)<delegated_cost: continue
				grant_before={"grant_id":str(grant.id),"spent":int(grant.spent)}
				break
		if delegation_applies and (grant_before.is_empty() or not Life.has_delegated_permission(s,str(command.actor_id),str(command.acting_company_id),str(command.type),delegated_cost)):
			_event(s,"command_failed","DELEGATED_BUDGET_EXCEEDED",month,{"actor_id":command.actor_id,"command_id":command.id})
			s.used_command_ids.append(command.id)
			continue
		var handled: bool=false
		for module in command_modules():
			if str(command.type) in module.command_types():
				var result: Dictionary=module.apply(s,command,month)
				if not result.get("ok",false):
					_event(s,"command_failed",str(result.get("error","수행 조건 변경")),month,{"actor_id":command.actor_id,"city_id":Domain.owner_city(s,str(command.actor_id)),"command_id":command.id})
				elif not grant_before.is_empty(): _charge_grant(s,str(grant_before.grant_id),delegated_cost,month)
				handled=true
				break
		if handled:
			s.used_command_ids.append(command.id)
			continue
		var company: Dictionary = _find(s.companies,command.acting_company_id)
		if company.is_empty() or not Life.can_act(s,str(command.actor_id),str(command.acting_company_id),str(command.type),command.payload):
			_event(s,"command_failed","AUTHORITY_LOST_AT_EXECUTION",month,{"actor_id":command.actor_id,"command_id":command.id})
			s.used_command_ids.append(command.id)
			continue
		match str(command.type):
			"set_policy":
				if command.payload.has("dividend_percent") and not Finance.capital_authority(s,str(command.actor_id),str(company.id)):
					_event(s,"command_failed","CAPITAL_AUTHORITY_LOST_AT_EXECUTION",month,{"actor_id":command.actor_id,"command_id":command.id})
					s.used_command_ids.append(command.id)
					continue
				if not grant_before.is_empty(): _charge_grant(s,str(grant_before.grant_id),delegated_cost,month)
				for key in _keys(command.payload):
					company.policy[key] = int(command.payload[key])
				_event(s,"policy","반복 운영정책 변경",month,{"actor_id":command.actor_id,"company_id":company.id,"city_id":company.city_id,"command_id":command.id})
			"build":
				var site_error: String = _site_error(s,command.payload,company.id,false)
				if not site_error.is_empty():
					_event(s,"command_failed",site_error,month,{"actor_id":command.actor_id,"command_id":command.id,"city_id":company.city_id})
				else:
					var definition: Dictionary = _building(command.payload.kind)
					var project: Dictionary = {"id":"project-" + command.id,"company_id":company.id,"city_id":command.payload.city_id,"kind":definition.id,"name":definition.name,"x":int(command.payload.x),"y":int(command.payload.y),"w":int(definition.w),"h":int(definition.h),"stage":"기초","progress":0,"required_work":int(definition.work),"months_worked":0,"min_months":int(definition.min_months),"status":"active","materials_used":{"wood":0,"stone":0,"tools":0},"materials_required":{"wood":int(definition.wood),"stone":int(definition.stone),"tools":int(definition.tools)},"reason":"착공 준비","start_month":month,"budget":construction_budget(str(definition.id)),"spent":0}
					if not grant_before.is_empty(): _charge_grant(s,str(grant_before.grant_id),delegated_cost,month)
					s.projects.append(project)
					_event(s,"construction_start","부지 확정·월별 자재와 시공 필요",month,{"actor_id":company.id,"city_id":project.city_id,"project_id":project.id,"command_id":command.id})
			"cancel_project", "pause_project", "resume_project":
				var project: Dictionary = _find(s.projects,command.payload.project_id)
				project.status = {"cancel_project":"cancelled","pause_project":"paused","resume_project":"active"}[command.type]
				project.reason = "사용자 공사 정책 변경"
				if command.type=="cancel_project": Finance.cancel_construction(s,str(company.id),str(project.id),month)
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
	target = mini(target,_work_available(s,contractor.id,"builder",str(project.city_id)))
	var equipment_id: String=str(contractor.id)+":"+str(project.city_id)
	var equipment_hours: int=available_quantity(s,str(contractor.id),"machinery",str(project.city_id))*int(p.construction_equipment_capacity)-int(s.get("monthly_equipment",{}).get(equipment_id,0))
	target=mini(target,maxi(0,int(equipment_hours/int(p.equipment_hours_per_work))))
	target=mini(target,maxi(0,int((int(project.budget)-int(project.spent))/int(p.construction_hour_price))))
	target = mini(target,int(cash(s,owner)/int(p.construction_hour_price)))
	for product_id in _keys(project.materials_required):
		var monthly_need: int = int(ceil(float(project.materials_required[product_id]) * float(int(project.progress)+target) / float(project.required_work))) - int(project.materials_used[product_id])
		var shortage: int = monthly_need - available_quantity(s,owner,product_id,project.city_id)
		if shortage > 0:
			var before_cash: int=cash(s,owner)
			var unit_budget: int=maxi(1,int(_find(s.products,product_id).price)*2+int(p.freight_unit_cost))
			shortage=mini(shortage,maxi(0,int((int(project.budget)-int(project.spent))/unit_budget)))
			_purchase(s,owner,project.city_id,product_id,shortage,month,"건설 자재")
			project.spent=int(project.spent)+maxi(0,before_cash-cash(s,owner))
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
		project.reason = "시공 인력·장비시간 또는 예산 부족"
	if feasible <= 0:
		if cash(s,owner)<int(p.construction_hour_price):
			project.reason = "시공대금 지급 예산 부족"
		_event(s,"construction_delay",project.reason,month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id})
		return
	if not _work(s,str(contractor.id),feasible,month,"builder",str(project.city_id),owner,feasible*int(p.construction_hour_price)):
		project.reason = "시공대금·실제 임금의 동시 결제 불가"
		_event(s,"construction_delay",project.reason,month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id})
		return
	if not s.has("monthly_equipment"): s.monthly_equipment={}
	s.monthly_equipment[equipment_id]=int(s.monthly_equipment.get(equipment_id,0))+feasible*int(p.equipment_hours_per_work)
	s["cost_context"]={"project_id":project.id}
	Finance.capitalize_construction_payment(s,owner,feasible*int(p.construction_hour_price),month,str(project.id))
	for product_id in _keys(project.materials_required):
		var need: int = int(ceil(float(project.materials_required[product_id]) * float(int(project.progress)+feasible) / float(project.required_work))) - int(project.materials_used[product_id])
		if need > 0:
			_change_inventory(s,owner,project.city_id,product_id,-need,"건설 공정 투입",month,"construction_input")
			project.materials_used[product_id] = int(project.materials_used[product_id]) + need
	s.erase("cost_context")
	project.progress = int(project.progress) + feasible
	project.months_worked = int(project.months_worked) + 1
	project.spent = int(project.spent) + feasible*int(p.construction_hour_price)
	project.stage = "기초" if int(project.progress)<int(p.construction_stage_foundation) else ("골조" if int(project.progress)<int(p.construction_stage_frame) else "설비·검수")
	_event(s,"construction_work","자재·시공시간·공정대금 투입",month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id,"hours":feasible,"equipment_hours":feasible*int(p.equipment_hours_per_work),"amount":feasible*int(p.construction_hour_price),"quantity":feasible})
	if int(project.progress)>=int(project.required_work) and int(project.months_worked)>=int(project.min_months):
		project.status = "completed"
		project.stage = "준공"
		project.reason = "월말 검수 완료·다음 달부터 가동 가능"
		project.completed_month = month
		Finance.finalize_construction(s,owner,str(project.id),month)
		var definition: Dictionary = _building(project.kind)
		s.facilities.append({"id":"facility-"+project.id,"name":definition.name,"company_id":owner,"city_id":project.city_id,"kind":project.kind,"x":project.x,"y":project.y,"w":project.w,"h":project.h,"status":"ready","recipe_id":definition.recipe_id,"capacity":definition.capacity,"operational_month":month+1,"project_id":project.id})
		_event(s,"construction_complete",project.reason,month,{"actor_id":owner,"city_id":project.city_id,"project_id":project.id})

static func _consume(s: Dictionary, person: Dictionary, month: int) -> void:
	var wants: Dictionary=_parameters().household_demand.duplicate(true)
	wants.merge(Industry.household_needs(s,person,month),true)
	var ordered: Array=[]
	for product in _keys(_parameters().household_demand): ordered.append(product)
	for product in _keys(wants):
		if product not in ordered: ordered.append(product)
	var optional_budget: int=-1
	for product in ordered:
		var essential: bool=_parameters().household_demand.has(product)
		if not essential and optional_budget<0: optional_budget=int(cash(s,str(person.id))*int(_parameters().optional_budget_percent)/100)
		var before_cash: int=cash(s,str(person.id))
		var amount: int=maxi(0,int(wants[product])-Public.provided_need(s,str(person.id),product,month))
		var consumed: int=0
		if Industry.is_service(s,product):
			var held: int=Industry.service_available(s,str(person.id),product,str(person.city_id))
			if held<amount: _purchase(s,str(person.id),str(person.city_id),product,amount-held,month,"가계 필수 수요",-1 if essential else optional_budget)
			var used: int=mini(amount,Industry.service_available(s,str(person.id),product,str(person.city_id)))
			if used>0: Industry.consume_service(s,str(person.id),str(person.city_id),product,used,month)
			consumed=used
			if used<amount and _parameters().household_demand.has(product): _event(s,"unmet_need","가계 서비스 접근·구매력 부족",month,{"actor_id":person.id,"city_id":person.city_id,"product_id":product,"quantity":amount-used})
		else:
			var held: int=available_quantity(s,str(person.id),product,str(person.city_id))
			if held<amount: _purchase(s,str(person.id),str(person.city_id),product,amount-held,month,"가계 구매",-1 if essential else optional_budget)
			var used: int=mini(amount,available_quantity(s,str(person.id),product,str(person.city_id)))
			consumed=used
			if used>0:
				if _parameters().household_demand.has(product): _change_inventory(s,str(person.id),str(person.city_id),product,-used,"가계 사용",month,"consumption")
				else: Industry.household_use(s,person,product,used,month)
			if used<amount and _parameters().household_demand.has(product): _event(s,"unmet_need","현지 재고 또는 가계 구매력 부족",month,{"actor_id":person.id,"city_id":person.city_id,"product_id":product,"quantity":amount-used})
		if not essential: optional_budget=maxi(0,optional_budget-maxi(0,before_cash-cash(s,str(person.id))))
		person.last_consumption=int(person.last_consumption)+consumed

static func turn_progress_steps() -> int:
	return 28

static func resolve_month(s: Dictionary, progress: Variant = null) -> Dictionary:
	var task: RefCounted=Progress.observer(progress)
	task.begin(turn_progress_steps(),"월 정산 입력·원장 검사")
	var errors: Array = validate(s)
	var p: Dictionary=_parameters()
	if not errors.is_empty():
		task.fail("INVALID_INPUT_STATE","입력 상태 검사 실패")
		return {"ok":false,"errors":errors,"error":"INVALID_INPUT_STATE","state":s,"report":{}}
	if s.phase != "Planning":
		task.fail("NOT_PLANNING","계획 단계가 아님")
		return {"ok":false,"errors":["NOT_PLANNING"],"error":"NOT_PLANNING","state":s,"report":{}}
	task.advance("입력·원장 검사 완료")
	var work: Dictionary = s.duplicate(true)
	var month: int = int(work.planning_month)
	var start_event: int = work.events.size()
	work.phase = "Resolving"
	work.monthly_labor = {}
	work["monthly_equipment"]={}
	for company in work.companies:
		company.revenue = 0
		company.expenses = 0
	for person in work.people:
		person.last_income = 0
		person.last_consumption = 0
	task.advance("월 계획 스냅샷 준비 완료")
	# Queue AI through the public validator while the snapshot is still planning.
	work.phase = "Planning"
	_ai_commands(work)
	for module in [Life,Industry,Public,Finance,Market,Land]:
		for candidate in module.ai_commands(work):
			if candidate.get("actor_id","")!=work.player_id: queue_command(work,candidate)
	task.advance("인물·기업·공공 AI 계획 수집 완료")
	# Revalidate every submitted command from the immutable planning snapshot.
	var recheck: Dictionary = work.duplicate(true)
	recheck.commands = []
	for command in work.commands:
		var result: Dictionary = queue_command(recheck,command)
		if not result.ok:
			task.fail(str(result.error),"확정 명령 검사 실패")
			return {"ok":false,"errors":[result.error],"error":"INVALID_COMMAND","state":s,"report":{}}
	recheck={}
	_plan_cache_state={}
	_plan_cache_count=-1
	work.phase = "Resolving"
	task.advance("공통 권한·예약·명령 검사 완료")
	_arrive_shipments(work,month)
	task.advance("예정 화물 도착 처리 완료")
	_execute_commands(work,month)
	task.advance("확정 명령 실행 완료")
	Land.before_turn(work,month)
	Market.before_turn(work,month)
	Industry.before_turn(work,month)
	Industry.register_closure_claims(work,month)
	task.advance("시장·산업 월초 예약 준비 완료")
	Life.monthly(work,month)
	Public.begin_month(work,month)
	task.advance("인물 계약·공공 월초 처리 완료")
	Market.monthly(work,month)
	Industry.monthly(work,month)
	task.advance("시장 체결·산업 프로젝트 처리 완료")
	for facility in work.facilities:
		if int(facility.get("operational_month",1))<=month and facility.status=="ready":
			facility.status="operational"
	# All roles reserve the same person's finite monthly capacity.
	for company in _sorted(work,work.companies,"management"):
		var director: Dictionary=_find(work.people,str(company.director_id))
		if director.is_empty() or company.get("status","active")!="active": continue
		var hours: int=mini(int(p.director_hours),maxi(0,int(director.hours)-int(work.monthly_labor.get(director.id,0))))
		var amount: int=hours*int(director.wage)
		if hours>0 and cash(work,str(company.id))>=amount and _pay(work,str(company.id),str(director.id),amount,"대표 경영 보수",month,{"city_id":company.city_id,"hours":hours}):
			work.monthly_labor[director.id]=int(work.monthly_labor.get(director.id,0))+hours
			_event(work,"labor","대표 경영 업무",month,{"actor_id":director.id,"company_id":company.id,"city_id":company.city_id,"hours":hours,"amount":amount,"wage_paid":true})
	task.advance("시설 가동·대표 경영 처리 완료")
	_prepare_runtime(work)
	var ordered: Array=_sorted(work,work.facilities,"production")
	ordered.sort_custom(func(a,b): return _production_priority(work,a)<_production_priority(work,b))
	task.advance("생산 순서·조회 인덱스 준비 완료")
	for facility in ordered:
		if _production_priority(work,facility)<=1: _produce(work,facility,month)
	task.advance("전력·용수 생산 완료")
	Life.deliver_training(work,month)
	task.advance("승인된 교육 제공 완료")
	for facility in ordered:
		if _production_priority(work,facility)>1: _produce(work,facility,month)
	task.advance("나머지 산업 생산 완료")
	Industry.deliver_digital(work,month)
	task.advance("디지털 납품 처리 완료")
	for project in _sorted(work,work.projects,"construction"):
		_construct(work,project,month)
	task.advance("건설 공정 처리 완료")
	var payroll: Dictionary=Life.pay_contractual_wages(work,month)
	if not payroll.get("ok",false):
		task.fail(str(payroll.get("error","PAYROLL_FAILURE")),"계약 임금 정산 실패")
		return {"ok":false,"errors":[str(payroll.get("error","PAYROLL_FAILURE"))],"error":"PAYROLL_FAILURE","state":s,"report":{}}
	task.advance("계약 임금·체불 처리 완료")
	# Buyers are batch collected, then seeded per-period order changes fairness.
	for person in _sorted(work,work.people,"consumption"):
		_consume(work,person,month)
	task.advance("가계 구매·소비 처리 완료")
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
	task.advance("정비·자원 재생 처리 완료")
	Public.monthly(work,month)
	task.advance("정부·정책·조달 월말 처리 완료")
	Industry.after_turn(work,month)
	task.advance("산업·재고 월말 처리 완료")
	Finance.monthly(work,month)
	task.advance("금융·채무 월말 처리 완료")
	for company in work.companies:
		company.last_profit=Finance.retained_earnings(work,str(company.id))
		var amount: int=maxi(0,int(int(company.last_profit)*int(company.policy.dividend_percent)/100))
		amount=mini(amount,maxi(0,cash(work,str(company.id))-int(p.dividend_cash_floor)))
		if amount>0: Finance.distribute_dividend(work,str(company.id),amount,month)
	task.advance("기업 손익·배당 처리 완료")
	Finance.reconcile_deposits(work,month)
	task.advance("예금·결제 대응 검사 완료")
	work.erase("_runtime")
	work.last_resolved_month=month
	work.planning_month=month+1
	work.version=int(work.version)+1
	work.phase="Planning"
	work.report_pending=true
	task.advance("다음 계획 월 커밋 후보 준비 완료")
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
	task.advance("월간 보고 생성 완료")
	errors=validate(work)
	if not errors.is_empty():
		task.fail("INVARIANT_FAILURE","월말 경제 불변식 검사 실패")
		return {"ok":false,"errors":errors,"error":"INVARIANT_FAILURE","state":s,"report":{}}
	task.advance("월말 경제 불변식 검사 완료")
	report.state_hash=state_hash(work)
	task.finish("월 정산·확정 해시 완료")
	return {"ok":true,"errors":[],"error":"","state":work,"report":report}

static func _scope_cities(s: Dictionary, scope_id: String) -> Array:
	var ids: Array = []
	for city in s.cities:
		if scope_id=="world" or city.id==scope_id or city.nation_id==scope_id or city.continent_id==scope_id:
			ids.append(city.id)
	return ids

static func metrics(s: Dictionary, scope_id: String = "world") -> Dictionary:
	# Query only: physical activity is valued at immutable content base prices.
	# Selling an existing good, issuing credit, or moving money adds no output.
	var month: int=int(s.get("last_resolved_month",0))
	var city_ids: Array=_scope_cities(s,scope_id)
	var selected: Dictionary={}
	var cities: Dictionary={}
	var owners: Dictionary={}
	var people: Dictionary={}
	var products: Dictionary={}
	var rows: Dictionary={}
	var city_products: Dictionary={}
	var household_prices: Dictionary={}
	for id in city_ids: selected[str(id)]=true
	for city in s.get("cities",[]): cities[str(city.id)]=_metric_empty_city(str(city.id))
	for collection in [s.get("people",[]),s.get("companies",[]),s.get("households",[])]:
		for entity in collection:
			if entity.has("city_id"): owners[str(entity.id)]=str(entity.city_id)
	for group_name in ["public","finance"]:
		for collection in s.get(group_name,{}).values():
			if not collection is Array: continue
			for entity in collection:
				if entity is Dictionary and entity.has("id") and cities.has(str(entity.get("city_id",""))): owners[str(entity.id)]=str(entity.city_id)
	for person in s.get("people",[]):
		people[str(person.id)]=true
		if not cities.has(str(person.city_id)): continue
		var local: Dictionary=cities[str(person.city_id)]
		local.population+=1
		if person.get("employment_status","")=="employed": local.employed+=1
	for company in s.get("companies",[]):
		if not cities.has(str(company.city_id)): continue
		cities[str(company.city_id)].revenue+=int(company.get("revenue",0))
		cities[str(company.city_id)].expenses+=int(company.get("expenses",0))
	for product in s.get("products",[]):
		var id: String=str(product.id)
		products[id]=product
		rows[id]={"product_id":id,"name":str(product.get("name",id)),"unit":str(product.get("unit","")),"price":int(product.price),"base_price":int(product.price),"supply":0,"demand":0,"unmet":0,"produced":0,"consumed":0,"trade_quantity":0,"trade_value":0,"priced_quantity":0,"trade_value_by_currency":{},"price_observed":false}
	for account in s.get("accounts",{}).values():
		if account.get("kind","")!="cash": continue
		var city: String=str(owners.get(str(account.get("owner","")),""))
		if not cities.has(city): continue
		_metric_add_currency(cities[city].cash_by_currency,str(account.get("currency","USD")),int(account.get("balance",0)))
	for lot in s.get("inventory",[]):
		var city: String=str(lot.get("accounting_city_id",lot.get("city_id","")))
		var product: String=str(lot.get("product_id",""))
		if not cities.has(city) or not products.has(product): continue
		var quantity: int=int(lot.get("quantity",0))
		cities[city].inventory_value+=quantity*int(products[product].price)
		if selected.has(city): rows[product].supply+=quantity
	# One event scan serves activity, territorial trade, and carried-forward
	# household price observations. No product-by-event nested search.
	var exports_by_currency: Dictionary={}
	var imports_by_currency: Dictionary={}
	var exports: int=0
	var imports: int=0
	for event in s.get("events",[]):
		var type: String=str(event.get("type",""))
		var event_month: int=int(event.get("month",0))
		var product: String=str(event.get("product_id",""))
		var city: String=str(event.get("city_id",owners.get(str(event.get("actor_id","")),"")))
		var quantity: int=maxi(0,int(event.get("quantity",0)))
		var price: int=int(products.get(product,{}).get("price",0))
		var value: int=quantity*price
		var sector: String=str(event.get("sector",""))
		var priced_sector_sale: bool=type=="gdp_output" and sector in ["digital","education_current"] and not str(event.get("buyer_id","")).is_empty()
		var is_trade: bool=type in ["trade","service_trade"] or priced_sector_sale
		if is_trade and event_month<=month and quantity>0 and people.has(str(event.get("buyer_id",event.get("actor_id","")))) and str(event.get("currency","USD"))=="USD":
			var key: String=_metric_product_key(city,product)
			if not household_prices.has(key) or int(household_prices[key].month)<event_month: household_prices[key]={"month":event_month,"quantity":0,"amount":0}
			if int(household_prices[key].month)==event_month:
				household_prices[key].quantity+=quantity
				household_prices[key].amount+=int(event.get("amount",0))
		if event_month!=month: continue
		if is_trade:
			var seller_city: String=str(event.get("seller_city_id",owners.get(str(event.get("seller_id","")),city)))
			var buyer_city: String=str(event.get("buyer_city_id",city))
			var currency: String=str(event.get("currency","USD"))
			var amount: int=int(event.get("amount",0))
			if selected.has(seller_city) and not selected.has(buyer_city):
				exports+=(int(event.get("amount",0)) if currency=="USD" else 0) if priced_sector_sale else value
				_metric_add_currency(exports_by_currency,currency,amount)
			if selected.has(buyer_city) and not selected.has(seller_city):
				imports+=(int(event.get("amount",0)) if currency=="USD" else 0) if priced_sector_sale else value
				_metric_add_currency(imports_by_currency,currency,amount)
			if cities.has(buyer_city): cities[buyer_city].trades+=1
			if selected.has(buyer_city) and rows.has(product):
				rows[product].trade_quantity+=quantity
				_metric_add_currency(rows[product].trade_value_by_currency,currency,amount)
				if currency=="USD":
					rows[product].trade_value+=amount
					rows[product].priced_quantity+=quantity
			# Sold service units are actual output at the provider's location.
			# Unused capacity is not storable production and is not GDP.
			if type=="service_trade" and cities.has(seller_city):
				cities[seller_city].gross_output+=value
				cities[seller_city].production_value+=value
				_metric_product_activity(city_products,seller_city,product,"produced",quantity)
		if not cities.has(city): continue
		match type:
			"gdp_output","gdp_input":
				if sector not in ["public_nonmarket","digital","education_current"]: continue
				var measured: int=int(event.get("amount",0)) if str(event.get("currency","USD"))=="USD" else 0
				var field: String="gross_output" if type=="gdp_output" else "intermediate_consumption"
				var prefix: String="public_" if sector=="public_nonmarket" else ("training_" if sector=="education_current" else "digital_")
				var component: String=prefix+field
				cities[city][field]+=measured
				cities[city][component]+=measured
			"production_output":
				cities[city].gross_output+=value
				cities[city].production_value+=value
				_metric_product_activity(city_products,city,product,"produced",quantity)
			"production_input","service_input": cities[city].intermediate_consumption+=value
			"construction_input":
				# Materials become part of work-in-progress output and are also
				# intermediate inputs. Their producer's VA is counted once.
				cities[city].gross_output+=value
				cities[city].intermediate_consumption+=value
				cities[city].construction_materials+=value
			"construction_work","construction_progress":
				var work_value: int=int(event.get("hours",quantity))*int(_parameters().construction_hour_price)
				cities[city].gross_output+=work_value
				cities[city].construction_work+=work_value
			"demand":
				cities[city].demand_value+=value
				_metric_product_activity(city_products,city,product,"demand",quantity)
			"unmet_need":
				cities[city].unmet_value+=value
				_metric_product_activity(city_products,city,product,"unmet",quantity)
			"consumption":
				cities[city].consumption_value+=value
				_metric_product_activity(city_products,city,product,"consumed",quantity)
	var result: Dictionary=_metric_empty_city(scope_id)
	result.merge({"scope_id":scope_id,"month":month,"per_product":[],"gdp":0,"real_gdp":0,"value_added":0,"price_index":100,"price_index_bps":10000,"price_basket":[],"price_index_missing_weight_percent":0.0,"price_index_reference_weight_percent":0.0,"exports":exports,"imports":imports,"net_exports":exports-imports,"exports_by_currency":exports_by_currency,"imports_by_currency":imports_by_currency,"accounting":"experimental_mixed_basis_covered_sector_value_added","valuation_currency":"USD","price_basis":"goods_services_construction_at_content_base_prices;public_at_actual_cost;digital_at_earned_transaction_prices;training_at_earned_tuition_and_declared_input_prices","nominal_gdp_available":false,"full_real_gdp_available":false,"real_gdp_scope":"fixed_base_price_goods_sold_services_and_construction_only","unit_warning":"통합 생산·소비·미충족·재고는 기준가격으로 환산한 최소 화폐 단위입니다. 품목별 물량은 per_product에서 확인합니다.","definition":"상품·판매 서비스·건설은 기준가격, 공공 비시장 서비스는 실제 비용, 디지털은 이행한 거래가격, 직업교육은 실제 제공한 수강료와 명시된 투입 평가로 계산한 부가가치입니다. 평가 기준별 구성요소를 분리하며 전 부문 명목·실질 GDP로 단정하지 않습니다."},true)
	for city in cities:
		if not selected.has(city): continue
		for field in ["population","employed","revenue","expenses","trades","gross_output","intermediate_consumption","production_value","consumption_value","demand_value","unmet_value","inventory_value","construction_materials","construction_work","public_gross_output","public_intermediate_consumption","digital_gross_output","digital_intermediate_consumption","training_gross_output","training_intermediate_consumption"]: result[field]=int(result[field])+int(cities[city][field])
		for currency in cities[city].cash_by_currency: _metric_add_currency(result.cash_by_currency,currency,int(cities[city].cash_by_currency[currency]))
		for product in products:
			var activity: Dictionary=city_products.get(_metric_product_key(city,product),{})
			for field in ["demand","unmet","produced","consumed"]: rows[product][field]=int(rows[product][field])+int(activity.get(field,0))
	for product in s.get("products",[]):
		var row: Dictionary=rows[str(product.id)]
		if int(row.priced_quantity)>0:
			row.price=int(round(float(row.trade_value)/float(row.priced_quantity)))
			row.price_observed=true
		row.erase("priced_quantity")
		result.per_product.append(row)
	result.gdp=int(result.gross_output)-int(result.intermediate_consumption)
	result.public_nonmarket_value_added=int(result.public_gross_output)-int(result.public_intermediate_consumption)
	result.digital_current_price_value_added=int(result.digital_gross_output)-int(result.digital_intermediate_consumption)
	result.market_training_current_price_value_added=int(result.training_gross_output)-int(result.training_intermediate_consumption)
	result.covered_fixed_price_value_added=int(result.gdp)-int(result.public_nonmarket_value_added)-int(result.digital_current_price_value_added)-int(result.market_training_current_price_value_added)
	result.real_gdp=result.covered_fixed_price_value_added
	result.value_added=result.gdp
	result.cash=int(result.cash_by_currency.get("USD",0))
	# Legacy numeric UI keys retain their shape, with explicit monetary units.
	result.production=result.production_value
	result.consumption=result.consumption_value
	result.unmet=result.unmet_value
	result.inventory_total=result.inventory_value
	result.shortage_rate_percent=100.0*float(result.unmet_value)/float(result.demand_value) if int(result.demand_value)>0 else 0.0
	_metric_price_index(result,s,selected,cities,products,city_products,household_prices,month)
	return result

static func _metric_empty_city(id: String) -> Dictionary:
	return {"city_id":id,"population":0,"employed":0,"revenue":0,"expenses":0,"trades":0,"cash_by_currency":{},"gross_output":0,"intermediate_consumption":0,"production_value":0,"consumption_value":0,"demand_value":0,"unmet_value":0,"inventory_value":0,"construction_materials":0,"construction_work":0,"public_gross_output":0,"public_intermediate_consumption":0,"digital_gross_output":0,"digital_intermediate_consumption":0,"training_gross_output":0,"training_intermediate_consumption":0}

static func _metric_add_currency(amounts: Dictionary, currency: String, amount: int) -> void:
	amounts[currency]=int(amounts.get(currency,0))+amount

static func _metric_product_key(city: String, product: String) -> String:
	return city+"\u001f"+product

static func _metric_product_activity(index: Dictionary, city: String, product: String, field: String, quantity: int) -> void:
	var key: String=_metric_product_key(city,product)
	if not index.has(key): index[key]={}
	index[key][field]=int(index[key].get(field,0))+quantity

static func _metric_price_index(result: Dictionary, s: Dictionary, selected: Dictionary, cities: Dictionary, products: Dictionary, activity: Dictionary, observations: Dictionary, month: int) -> void:
	# Fixed quantities are annualized from the content rules. A missing quote
	# keeps its basket weight and last observed price, or its explicit base
	# reference; missing goods never disappear from a seemingly stable CPI.
	var basket: Dictionary={}
	for product in _parameters().household_demand: basket[str(product)]=float(_parameters().household_demand[product])
	for rule in Industry._data().get("household_needs",[]):
		basket[str(rule.product_id)]=float(rule.quantity)/float(maxi(1,int(rule.period)))
	var base_cost: float=0.0
	var current_cost: float=0.0
	var missing_weight: float=0.0
	var reference_weight: float=0.0
	for product in _keys(basket):
		if not products.has(product): continue
		var row: Dictionary={"product_id":product,"unit":str(products[product].get("unit","")),"monthly_quantity_per_person":float(basket[product]),"base_price":int(products[product].price),"base_cost":0.0,"current_cost":0.0,"missing_observation_cost":0.0,"reference_cost":0.0,"unmet_quantity":0,"price_observation":"actual_household_trade"}
		for city in selected:
			if not cities.has(city): continue
			var count: int=int(cities[city].population)
			var weight: float=float(basket[product])*count
			var reference: float=weight*float(row.base_price)
			var observed: Dictionary=observations.get(_metric_product_key(city,product),{})
			var current_price: float=float(observed.amount)/float(observed.quantity) if int(observed.get("quantity",0))>0 else float(row.base_price)
			row.base_cost+=reference
			row.current_cost+=weight*current_price
			row.unmet_quantity+=int(activity.get(_metric_product_key(city,product),{}).get("unmet",0))
			if int(observed.get("month",-1))!=month: row.missing_observation_cost+=reference
			if observed.is_empty(): row.reference_cost+=reference
		if float(row.missing_observation_cost)>0.0: row.price_observation="partly_carried_forward_or_reference"
		base_cost+=float(row.base_cost)
		current_cost+=float(row.current_cost)
		missing_weight+=float(row.missing_observation_cost)
		reference_weight+=float(row.reference_cost)
		result.price_basket.append(row)
	if base_cost>0.0:
		result.price_index=int(round(current_cost*100.0/base_cost))
		result.price_index_bps=int(round(current_cost*10000.0/base_cost))
		result.price_index_missing_weight_percent=missing_weight*100.0/base_cost
		result.price_index_reference_weight_percent=reference_weight*100.0/base_cost
	result.price_index_definition="고정 월 환산 가계 소비 묶음 · 기준 100 · 미관측 품목은 직전 실거래 또는 기준가격 유지"

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
	# Normalize restored whole-number floats, then use the engine's native
	# recursive key sorting and full precision serializer for large histories.
	return JSON.stringify(_normalize_fixture(value),"",true,true)

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
		"cities":["id","name","nation_id","continent_id","width","height","tile_side_m","roads","resources","resource_capacity","forest_regrowth"],
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
	var number_fields: Array=["x","y","w","h","width","height","tile_side_m","hours","wage","capacity","quantity","reserved","progress","required_work","months_worked","min_months","month","amount","revenue","expenses","forest_regrowth","operational_month","completed_month","start_month","budget","spent","last_profit"]
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
	if int(s.schema_version)!=3 or s.rules_version!=RULES_VERSION: errors.append("UNSUPPORTED_SCHEMA_OR_RULES")
	if s.get("base_currency")!=Ledger.BASE_CURRENCY or s.get("minor_units_per_major")!=Ledger.MINOR_UNITS_PER_MAJOR: errors.append("WORLD_MONEY_UNIT_MISMATCH")
	for nation in s.nations:
		if nation.get("currency")!=Ledger.BASE_CURRENCY: errors.append("NATION_BASE_CURRENCY_MISMATCH")
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
		if int(city.get("width",0))<=0 or int(city.get("height",0))<=0 or int(city.get("tile_side_m",0))<=0: errors.append("INVALID_CITY_SIZE")
		for value in city.get("resources",{}).values():
			if int(value)<0: errors.append("NEGATIVE_NATURAL_RESOURCE")
	for company in s.companies:
		if (str(company.get("director_id",""))!="" and _find(s.people,company.director_id).is_empty()) or _find(s.cities,company.get("city_id","")).is_empty(): errors.append("COMPANY_REFERENCE_INVALID")
		if not s.accounts.has(str(company.id)+":cash"): errors.append("COMPANY_CASH_ACCOUNT_MISSING")
		elif typeof(s.accounts[str(company.id)+":cash"])!=TYPE_DICTIONARY or s.accounts[str(company.id)+":cash"].get("owner")!=company.id: errors.append("COMPANY_CASH_OWNER_MISMATCH")
	for person in s.people:
		if _find(s.cities,person.get("city_id","")).is_empty() or (str(person.get("company_id",""))!="" and _find(s.companies,person.company_id).is_empty()): errors.append("PERSON_REFERENCE_INVALID")
		if int(s.get("monthly_labor",{}).get(person.id,0))>int(person.get("capacity_hours",person.get("hours",0))): errors.append("LABOR_OVERALLOCATED:"+str(person.id))
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
		if Domain.owner_city(s,str(lot.get("owner_id","")))=="": errors.append("UNKNOWN_INVENTORY_OWNER")
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
	for module in command_modules():
		for issue in module.validate(s): errors.append(issue)
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

static func _prepare_runtime(s: Dictionary) -> void:
	var recipes: Dictionary={}
	var companies: Dictionary={}
	var suppliers: Dictionary={}
	for recipe in s.recipes: recipes[str(recipe.id)]=recipe
	for company in s.companies: companies[str(company.id)]=company
	for facility in s.facilities:
		var recipe: Dictionary=recipes.get(str(facility.recipe_id),{})
		for product in recipe.get("outputs",{}):
			if not suppliers.has(product): suppliers[product]={}
			suppliers[product][str(facility.company_id)+"|"+str(facility.city_id)]={"company_id":facility.company_id,"city_id":facility.city_id}
	s["_runtime"]={"companies":companies,"recipes":recipes,"suppliers":suppliers,"stock_groups":{}}
	for lot in s.inventory: Domain.register_lot(s,lot)

static func _production_priority(s: Dictionary, facility: Dictionary) -> int:
	var recipe: Dictionary=_find(s.recipes,str(facility.get("recipe_id","")))
	if recipe.get("outputs",{}).has("energy"): return 0
	if recipe.get("outputs",{}).has("water"): return 1
	if recipe.get("process_kind","")=="extraction": return 2
	if recipe.get("process_kind","") in ["agriculture","livestock"]: return 3
	if recipe.get("process_kind","")=="service": return 6
	return 4
