extends RefCounted
## Read-only readiness assessment. Preview months run on an isolated world copy.
const Economy=preload("res://core/economy.gd")
const Industry=preload("res://core/industry.gd")
const Public=preload("res://core/public_sector.gd")
const Ledger=preload("res://core/ledger.gd")
const PATH: String="res://data/generation.json"
static var _cached: Dictionary={}

static func configuration() -> Dictionary:
	if _cached.is_empty() and FileAccess.file_exists(PATH):
		var parsed: Variant=JSON.parse_string(FileAccess.get_file_as_string(PATH))
		if parsed is Dictionary: _cached=parsed
	return _cached.duplicate(true)

static func validate_configuration(config: Dictionary) -> Array:
	var errors: Array=[]
	if config.get("status")!="experimental" or int(config.get("version",0))!=1: errors.append("GENERATION_CONFIGURATION_VERSION")
	for key in ["preview_months","maximum_preview_months","buffer_warning_percent"]:
		if not _integer(config.get(key)) or int(config[key])<=0: errors.append("GENERATION_CONFIGURATION_INTEGER:"+key)
	for key in ["essential_products","support_products","coverage"]:
		if not config.get(key) is Array or config.get(key,[]).is_empty(): errors.append("GENERATION_CONFIGURATION_ARRAY:"+key)
	if errors.is_empty() and (int(config.preview_months)>int(config.maximum_preview_months) or int(config.buffer_warning_percent)>100): errors.append("GENERATION_CONFIGURATION_RANGE")
	return errors

static func _integer(value: Variant) -> bool:
	return typeof(value) in [TYPE_INT,TYPE_FLOAT] and is_finite(float(value)) and float(value)==floor(float(value)) and abs(float(value))<=9007199254740991.0

static func _key(city: String, product: String) -> String:
	return city+"\u001f"+product

static func _stocks(s: Dictionary) -> Dictionary:
	var result: Dictionary={}
	for lot in s.get("inventory",[]):
		var product: String=str(lot.product_id)
		result[product]=int(result.get(product,0))+int(lot.quantity)
	return result

static func _index(s: Dictionary) -> Dictionary:
	var result: Dictionary={"cities":{},"products":{},"companies":{},"recipes":{},"employees":{},"producers":{},"owned_stock":{},"local_stock":{},"routes":{},"chain_ok":{},"cycles":[]}
	for key in ["cities","products","companies","recipes"]:
		for row in s.get(key,[]): result[key][str(row.id)]=row
	for person in s.get("people",[]):
		var company: String=str(person.get("company_id",""))
		if person.get("employment_status","")!="employed" or person.get("role","")=="director": continue
		if not result.employees.has(company): result.employees[company]={"hours":0,"wage":0}
		result.employees[company].hours+=int(person.get("hours",0))
		result.employees[company].wage=maxi(int(result.employees[company].wage),int(person.get("wage",0)))
	for lot in s.get("inventory",[]):
		if lot.get("location","") in ["shipment","public_transit"]: continue
		if int(lot.get("expires_month",0))>0 and int(lot.expires_month)<=int(s.planning_month): continue
		var free: int=maxi(0,int(lot.quantity)-int(lot.get("reserved",0)))
		var local_key: String=_key(str(lot.city_id),str(lot.product_id))
		result.local_stock[local_key]=int(result.local_stock.get(local_key,0))+free
		var owner_key: String=_key(str(lot.owner_id),local_key)
		result.owned_stock[owner_key]=int(result.owned_stock.get(owner_key,0))+free
	for facility in s.get("facilities",[]):
		if facility.get("status","") not in ["operational","ready"] or int(facility.get("capacity",0))<=0: continue
		if result.companies.get(str(facility.company_id),{}).get("status","active")!="active": continue
		var recipe: Dictionary=result.recipes.get(str(facility.get("recipe_id","")),{})
		for product in recipe.get("outputs",{}):
			if not result.producers.has(str(product)): result.producers[str(product)]=[]
			result.producers[str(product)].append(facility)
	return result

static func _path(s: Dictionary, index: Dictionary, from: String, to: String, product: String) -> bool:
	if from==to: return true
	if index.products.get(product,{}).get("kind","")=="service": return false
	var key: String=from+"|"+to+"|"+product
	if index.routes.has(key): return bool(index.routes[key])
	var legal: bool=bool(Public.trade_rule(s,from,to,product,int(s.planning_month)).get("allowed",false))
	var quote: Dictionary=Industry.route_quote(s,from,to,product,1,int(s.planning_month)) if legal else {}
	index.routes[key]=legal and quote.get("allowed",false) and int(quote.get("quantity",0))>0
	return bool(index.routes[key])

static func _sources(s: Dictionary, index: Dictionary, product: String, destination: String) -> Array:
	var result: Array=[]
	for facility in index.producers.get(product,[]):
		if _path(s,index,str(facility.city_id),destination,product): result.append(facility)
	return result

static func _readiness(s: Dictionary, index: Dictionary, facility: Dictionary) -> Dictionary:
	var owner: String=str(facility.company_id)
	var city: String=str(facility.city_id)
	var recipe: Dictionary=index.recipes.get(str(facility.recipe_id),{})
	var reasons: Array=[]
	var warnings: Array=[]
	var staff: Dictionary=index.employees.get(owner,{"hours":0,"wage":0})
	var hours: int=maxi(1,int(recipe.get("labor",1)))
	if int(staff.hours)<hours: reasons.append("WORKFORCE_MISSING")
	if Ledger.balance(s,owner+":cash")<hours*maxi(1,int(staff.wage)): reasons.append("WORKING_CAPITAL_MISSING")
	if int(facility.get("operational_month",1))>int(s.planning_month): warnings.append("NOT_OPERATIONAL_UNTIL:"+str(facility.operational_month))
	for product in recipe.get("inputs",{}):
		var quantity: int=int(recipe.inputs[product])
		var stock: int=int(index.owned_stock.get(_key(owner,_key(city,str(product))),0))
		var sources: Array=_sources(s,index,str(product),city)
		if sources.is_empty():
			# Opening buffers cannot certify a renewable supply chain.
			reasons.append("INPUT_REPLENISHMENT_MISSING:"+str(product))
		elif stock<quantity and index.products.get(str(product),{}).get("kind","")!="service": warnings.append("INPUT_REQUIRES_PURCHASE:"+str(product))
	for product in recipe.get("catalysts",{}):
		if int(index.owned_stock.get(_key(owner,_key(city,str(product))),0))<int(recipe.catalysts[product]): reasons.append("EQUIPMENT_MISSING:"+str(product))
	if recipe.has("resource") and int(index.cities.get(city,{}).get("resources",{}).get(str(recipe.resource),0))<int(recipe.get("resource_per_batch",1)): reasons.append("RESOURCE_MISSING:"+str(recipe.resource))
	return {"ready":reasons.is_empty(),"reasons":reasons,"warnings":warnings,"facility_id":facility.id,"company_id":owner,"city_id":city}

static func _chain(s: Dictionary, index: Dictionary, product: String, city: String, visiting: Dictionary = {}) -> bool:
	var key: String=_key(city,product)
	if index.chain_ok.has(key): return true
	if visiting.has(key):
		# Any storable input inside this cycle can bootstrap its first batch;
		# an unrelated downstream finished-good buffer cannot do so.
		var inside: bool=false
		var buffered: bool=false
		for marker in visiting:
			if marker==key: inside=true
			if not inside: continue
			var parts: PackedStringArray=str(marker).split("\u001f")
			var cycle_product: String=parts[parts.size()-1]
			if index.products.get(cycle_product,{}).get("kind","")!="service" and int(index.local_stock.get(marker,0))>0: buffered=true
		if buffered: _unique(index.cycles,"CYCLIC_CHAIN_REQUIRES_OPENING_BUFFER:"+city+":"+product)
		return buffered
	var next: Dictionary=visiting.duplicate()
	next[key]=true
	for facility in _sources(s,index,product,city):
		if not _readiness(s,index,facility).ready: continue
		var possible: bool=true
		var recipe: Dictionary=index.recipes.get(str(facility.recipe_id),{})
		for input in recipe.get("inputs",{}):
			if not _chain(s,index,str(input),str(facility.city_id),next):
				possible=false
				break
		if possible:
			index.chain_ok[key]=true
			return true
	return false

static func _check_graph(s: Dictionary, config: Dictionary, report: Dictionary) -> void:
	var index: Dictionary=_index(s)
	var population: Dictionary={}
	for person in s.people: population[str(person.city_id)]=int(population.get(str(person.city_id),0))+1
	for city in s.cities:
		if int(population.get(str(city.id),0))<=0: continue
		for product in config.essential_products:
			if not index.products.has(str(product)):
				report.errors.append("ESSENTIAL_PRODUCT_UNDEFINED:"+str(product))
				continue
			var sources: Array=_sources(s,index,str(product),str(city.id))
			var usable: Array=[]
			var details: Array=[]
			for facility in sources:
				var ready: Dictionary=_readiness(s,index,facility)
				details.append(ready)
				if ready.ready:
					var complete: bool=true
					var recipe: Dictionary=index.recipes.get(str(facility.recipe_id),{})
					for input in recipe.get("inputs",{}):
						if not _chain(s,index,str(input),str(facility.city_id)):
							complete=false
							ready.reasons.append("UPSTREAM_CHAIN_MISSING:"+str(input))
					if complete: usable.append(str(facility.id))
			var access: Dictionary={"city_id":city.id,"product_id":product,"renewable_producers":usable,"source_checks":details,"initial_local_stock":int(index.local_stock.get(_key(str(city.id),str(product)),0)),"population":int(population[str(city.id)]),"ready":not usable.is_empty()}
			report.observation.essential_access.append(access)
			if usable.is_empty(): report.errors.append("ESSENTIAL_SUPPLY_PATH_MISSING:"+str(city.id)+":"+str(product))
	# Check all intermediate dependencies so an essential factory cannot hide
	# an upstream industry that survives solely on a one-time opening buffer.
	for product in index.producers:
		for facility in index.producers[product]:
			var readiness: Dictionary=_readiness(s,index,facility)
			for reason in readiness.reasons:
				if str(reason).begins_with("INPUT_REPLENISHMENT_MISSING:"): _unique(report.warnings,"UPSTREAM_BUFFER_DEPENDENCY:"+str(facility.id)+":"+str(reason))
	for cycle in index.cycles: _unique(report.warnings,str(cycle))
	for product in config.support_products:
		if not index.products.has(str(product)): continue
		if index.producers.get(str(product),[]).is_empty(): _unique(report.warnings,"SUPPORT_PRODUCTION_MISSING:"+str(product))
	for government in s.get("public",{}).get("governments",[]):
		if Ledger.balance(s,str(government.id)+":cash")<=0: report.warnings.append("PUBLIC_OPENING_CASH_EMPTY:"+str(government.id))
	# Every customer's wallet must have a real bank and matching deposit.
	for account in s.accounts.values():
		if account.get("kind","")!="cash" or int(account.balance)==0: continue
		var owner: String=str(account.owner)
		if owner.begins_with("bank-") or owner.begins_with("central-bank-"): continue
		var bank: String=str(s.get("finance",{}).get("owner_bank",{}).get(owner,""))
		if bank.is_empty(): report.errors.append("OPENING_BANK_RELATION_MISSING:"+owner)
		elif Ledger.balance(s,bank+":deposit:"+owner+":"+str(account.currency))!=-int(account.balance): report.errors.append("OPENING_DEPOSIT_COUNTERPART_MISMATCH:"+owner+":"+str(account.currency))

static func _unique(items: Array, text: String) -> void:
	if text not in items: items.append(text)

static func validate(s: Dictionary, options: Dictionary = {}) -> Dictionary:
	var config: Dictionary=configuration()
	var report: Dictionary={"ok":false,"errors":[],"warnings":[],"observation":{"months":0,"unmet_value":0,"unmet_events":0,"essential_access":[],"turns":[],"opening_stock":{},"final_stock":{},"depleted_buffers":[],"production_by_product":{},"consumption_by_product":{},"coverage":config.get("coverage",[])},"fingerprint":"","assessment_hash":"","configuration_hash":JSON.stringify(config,"",true).sha256_text(),"status":"experimental"}
	report.errors.append_array(validate_configuration(config))
	var months: Variant=options.get("preview_months",config.get("preview_months",0))
	if not _integer(months) or int(months)<0 or int(months)>int(config.get("maximum_preview_months",0)): report.errors.append("GENERATION_PREVIEW_MONTHS_RANGE")
	if not report.errors.is_empty(): return report
	var state_errors: Array=Economy.validate(s)
	if not state_errors.is_empty():
		for error in state_errors: report.errors.append("OPENING_INVARIANT:"+str(error))
		return report
	report.fingerprint=Economy.state_hash(s)
	report.observation.opening_stock=_stocks(s)
	_check_graph(s,config,report)
	if not report.errors.is_empty():
		report.assessment_hash=JSON.stringify(report,"",true).sha256_text()
		return report
	var copy: Dictionary=s.duplicate(true)
	var start_events: int=copy.events.size()
	for step in range(int(months)):
		var resolved: Dictionary=Economy.resolve_month(copy)
		if not resolved.get("ok",false):
			for error in resolved.get("errors",[resolved.get("error","UNKNOWN_PREVIEW_FAILURE")]): report.errors.append("PREVIEW_MONTH:"+str(copy.planning_month)+":"+str(error))
			break
		copy=resolved.state
		var metrics: Dictionary=Economy.metrics(copy)
		report.observation.months+=1
		report.observation.unmet_value+=int(metrics.unmet_value)
		report.observation.turns.append({"month":int(copy.last_resolved_month),"state_hash":Economy.state_hash(copy),"gdp":int(metrics.gdp),"covered_fixed_price_value_added":int(metrics.covered_fixed_price_value_added),"public_nonmarket_value_added":int(metrics.public_nonmarket_value_added),"digital_earned_value_added":int(metrics.digital_current_price_value_added),"accounting":str(metrics.accounting),"unmet_value":int(metrics.unmet_value),"shortage_rate_percent":float(metrics.shortage_rate_percent),"trades":int(metrics.trades),"cash_by_currency":metrics.cash_by_currency.duplicate(true)})
	for i in range(start_events,copy.events.size()):
		var event: Dictionary=copy.events[i]
		if event.type=="unmet_need": report.observation.unmet_events+=1
		if event.type in ["production_output","consumption"]:
			var vector: Dictionary=report.observation.production_by_product if event.type=="production_output" else report.observation.consumption_by_product
			vector[str(event.product_id)]=int(vector.get(str(event.product_id),0))+int(event.quantity)
		if event.type=="opening" and int(event.month)>=int(s.planning_month): report.errors.append("PREVIEW_UNAUTHORIZED_OPENING_ASSET:"+str(event.id))
	report.observation.final_stock=_stocks(copy)
	for product in report.observation.opening_stock:
		var opening: int=int(report.observation.opening_stock[product])
		var final: int=int(report.observation.final_stock.get(product,0))
		if opening>0 and final*100<opening*int(config.buffer_warning_percent):
			var entry: Dictionary={"product_id":product,"opening":opening,"remaining":final,"produced":int(report.observation.production_by_product.get(product,0)),"consumed":int(report.observation.consumption_by_product.get(product,0))}
			report.observation.depleted_buffers.append(entry)
			report.warnings.append("OPENING_BUFFER_DEPLETED:"+str(product))
	if int(report.observation.unmet_events)>0: report.warnings.append("UNMET_HOUSEHOLD_NEEDS:"+str(report.observation.unmet_events))
	if int(report.observation.months)<int(config.maximum_preview_months): report.warnings.append("SHORT_OBSERVATION_NOT_LONG_RUN_BALANCE")
	if int(months)==0: report.warnings.append("STRUCTURAL_CHECK_ONLY_NO_PREVIEW_MONTHS")
	if Economy.state_hash(s)!=report.fingerprint: report.errors.append("GENERATION_CHECK_MUTATED_REAL_START")
	report.ok=report.errors.is_empty() and int(report.observation.months)==int(months)
	report.assessment_hash=JSON.stringify(report,"",true).sha256_text()
	return report

static func experiment(seeds: Array, months: int, world_options: Dictionary = {}) -> Dictionary:
	var result: Dictionary={"status":"experimental","months":months,"runs":[],"ok":true}
	for seed in seeds:
		var world: Dictionary=Economy.new_world(seed,world_options)
		var assessment: Dictionary=validate(world,{"preview_months":months})
		result.runs.append({"seed":seed,"options":world_options.duplicate(true),"assessment":assessment})
		if not assessment.ok: result.ok=false
	return result
