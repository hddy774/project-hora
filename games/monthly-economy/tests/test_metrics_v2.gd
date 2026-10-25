extends RefCounted
## Synthetic event fixtures isolate accounting definitions from balance tuning.
const Economy=preload("res://core/economy.gd")
const Ledger=preload("res://core/ledger.gd")
static var checks: int=0

static func _check(condition: bool, message: String, failures: Array) -> void:
	checks+=1
	if not condition: failures.append(message)

static func _event(s: Dictionary, type: String, city: String, product: String, quantity: int, extra: Dictionary = {}) -> void:
	var row: Dictionary={"id":"metric-event-"+str(s.events.size()),"month":1,"type":type,"city_id":city,"product_id":product,"quantity":quantity,"amount":0,"actor_id":""}
	row.merge(extra,true)
	s.events.append(row)

static func _world() -> Dictionary:
	var s: Dictionary={"last_resolved_month":1,"planning_month":2,"products":[],"cities":[],"people":[],"companies":[],"accounts":{},"journal":[],"events":[],"inventory":[],"public":{"governments":[]},"finance":{"market_makers":[],"banks":[]}}
	for i in range(3):
		var suffix: String=["a","b","c"][i]
		var city: String="city-"+suffix
		var owner: String="co-"+suffix
		var person: String="p-"+suffix
		s.cities.append({"id":city,"nation_id":"nation-ab" if i<2 else "nation-c","continent_id":"continent-ab" if i<2 else "continent-c"})
		s.people.append({"id":person,"city_id":city,"employment_status":"employed"})
		s.companies.append({"id":owner,"city_id":city,"revenue":0,"expenses":0})
		Ledger.opening(s,owner,1000)
		Ledger.opening(s,person,100)
	for row in [["food","상자",14],["water","m³",1],["energy","MWh",3],["wood","t",3],["tools","세트",16]]:
		s.products.append({"id":row[0],"name":row[0],"unit":row[1],"price":row[2],"kind":"service" if row[0] in ["water","energy"] else "material"})
	s.public.governments.append({"id":"gov-a","city_id":"city-a"})
	Ledger.opening(s,"gov-a",50)
	s.finance.market_makers.append({"id":"fx-c","city_id":"city-c"})
	Ledger.open_account(s,"fx-c:USD","fx-c","cash","USD")
	Ledger.open_account(s,"fx-c:capital:USD","fx-c","equity","USD")
	Ledger.post(s,"foreign-open",[{"account":"fx-c:USD","debit":500,"credit":0},{"account":"fx-c:capital:USD","debit":0,"credit":500}],"실험용 외화 개시",0)
	s.inventory.append({"id":"transit-lot","owner_id":"co-a","city_id":"","accounting_city_id":"city-a","product_id":"food","quantity":7,"location":"shipment"})
	_event(s,"production_output","city-a","food",10)
	_event(s,"production_input","city-a","wood",2)
	_event(s,"service_input","city-a","energy",1)
	_event(s,"production_output","city-b","food",2)
	_event(s,"production_output","city-c","tools",1)
	_event(s,"construction_input","city-a","wood",5)
	_event(s,"construction_work","city-a","",3,{"hours":3})
	_event(s,"trade","city-b","food",5,{"actor_id":"p-b","seller_id":"co-a","seller_city_id":"city-a","amount":140})
	_event(s,"service_trade","city-b","water",4,{"actor_id":"p-b","seller_id":"co-a","seller_city_id":"city-a","amount":4})
	_event(s,"trade","city-c","food",2,{"actor_id":"p-c","seller_id":"co-b","seller_city_id":"city-b","amount":28})
	_event(s,"service_trade","city-a","water",2,{"actor_id":"p-a","seller_id":"co-c","seller_city_id":"city-c","amount":4})
	_event(s,"demand","city-a","food",2,{"actor_id":"p-a"})
	_event(s,"unmet_need","city-a","food",1,{"actor_id":"p-a"})
	_event(s,"consumption","city-a","food",1,{"actor_id":"p-a"})
	return s

static func run() -> Array:
	checks=0
	var failures: Array=[]
	var s: Dictionary=_world()
	var before: String=Economy.state_hash(s)
	var world: Dictionary=Economy.metrics(s)
	_check(int(world.gdp)==193,"GDP values actual output less intermediates at fixed base prices",failures)
	_check(int(world.gross_output)==217 and int(world.intermediate_consumption)==24,"Construction materials enter output and intermediates exactly once",failures)
	_check(int(world.gdp)==int(world.real_gdp) and not world.nominal_gdp_available,"Fixed-price output is not misrepresented as nominal GDP",failures)
	_check(world.exports==0 and world.imports==0,"Closed-world internal trade is not world imports or exports",failures)
	_check(int(world.cash)==3350 and int(world.cash_by_currency.USD)==500,"Cash aggregation includes public wallets and preserves currencies separately",failures)
	_check(int(world.inventory_total)==98,"Legacy inventory total is base-price money, not a sum of unlike units",failures)
	_check(float(world.shortage_rate_percent)==50.0,"Observed unmet demand remains visible separately from CPI",failures)
	var nation: Dictionary=Economy.metrics(s,"nation-ab")
	_check(int(nation.exports)==28 and int(nation.imports)==2 and int(nation.net_exports)==26,"National boundary excludes domestic goods/services and includes foreign services",failures)
	_check(int(nation.exports_by_currency.CR)==28 and int(nation.imports_by_currency.CR)==4,"Border cash values retain actual prices while real quantities use fixed base prices",failures)
	var total: Dictionary={"gdp":0,"gross_output":0,"intermediate_consumption":0,"cash":0,"population":0,"trades":0}
	for city in s.cities:
		var local: Dictionary=Economy.metrics(s,city.id)
		for field in total: total[field]+=int(local[field])
	for field in total: _check(int(total[field])==int(world[field]),"World "+field+" is the exact city sum",failures)
	var energy_basket: Dictionary={}
	for row in world.price_basket:
		if row.product_id=="energy": energy_basket=row
	_check(not energy_basket.is_empty() and float(energy_basket.base_cost)==18.0,"Unavailable household energy retains its fixed basket weight",failures)
	_check(float(energy_basket.reference_cost)==float(energy_basket.base_cost),"Unobserved price uses an explicit reference rather than disappearing",failures)
	_check(float(world.price_index_missing_weight_percent)>0.0 and int(world.price_index)>100,"Price inflation and missing quotes are both reported",failures)
	_check(Economy.state_hash(s)==before,"All geographic metric queries preserve the simulation and RNG state",failures)
	# A real transfer and balanced loan creation change wallets, never output.
	Ledger.transfer(s,"metric-transfer","p-a","p-b",10,"가계 간 자금 이전",1)
	Ledger.open_account(s,"p-a:loan","p-a","liability")
	Ledger.open_account(s,"bank-a:loan","bank-a","asset")
	Ledger.open_account(s,"bank-a:deposit","bank-a","liability")
	s.finance.banks.append({"id":"bank-a","city_id":"city-a"})
	Ledger.post(s,"metric-credit",[{"account":"p-a:cash","debit":25,"credit":0},{"account":"p-a:loan","debit":0,"credit":25},{"account":"bank-a:loan","debit":25,"credit":0},{"account":"bank-a:deposit","debit":0,"credit":25}],"대출과 대응 예금",1)
	_event(s,"money_creation","city-a","",0,{"amount":25})
	_event(s,"public_transfer","city-a","",0,{"amount":100000})
	_event(s,"dividend","city-b","",0,{"amount":100000})
	var financial: Dictionary=Economy.metrics(s)
	_check(int(financial.gdp)==int(world.gdp),"Transfers, dividends and loan creation add no goods or GDP",failures)
	_check(int(financial.cash)==int(world.cash)+25,"Credit is recorded in cash independently of production",failures)
	_check(Ledger.validate(s).is_empty(),"Financial exclusion fixture keeps balanced owner/currency postings",failures)
	# Future observations cannot rewrite this month's price index.
	_event(s,"trade","city-a","food",1,{"actor_id":"p-a","seller_id":"co-c","seller_city_id":"city-c","amount":9999,"month":2})
	_check(Economy.metrics(s).price_index==financial.price_index,"Metrics ignore future price observations",failures)
	_event(s,"gdp_output","city-a","",10,{"actor_id":"gov-a","sector":"public_nonmarket","valuation":"actual_cost","amount":20})
	_event(s,"gdp_input","city-a","food",1,{"actor_id":"gov-a","sector":"public_nonmarket","valuation":"actual_cost","amount":14})
	_event(s,"gdp_output","city-a","food",1,{"actor_id":"gov-a","sector":"public_nonmarket","valuation":"actual_cost","amount":14})
	var public_activity: Dictionary=Economy.metrics(s)
	_check(int(public_activity.gdp)==int(world.gdp)+20,"Actual public labor adds at-cost nonmarket output",failures)
	_check(int(public_activity.public_gross_output)==34 and int(public_activity.public_intermediate_consumption)==14,"Actual public inputs enter gross output and intermediate use once",failures)
	_check(int(public_activity.real_gdp)==int(world.real_gdp) and int(public_activity.public_nonmarket_value_added)==20,"Public actual-cost component stays separate from fixed-price covered real output",failures)
	_check(not public_activity.full_real_gdp_available,"Mixed public/digital valuation never masquerades as full deflated national GDP",failures)
	_event(s,"gdp_output","city-c","",2,{"actor_id":"co-c","seller_id":"co-c","buyer_id":"co-a","buyer_city_id":"city-a","sector":"digital","valuation":"transaction_price","amount":100})
	_event(s,"gdp_input","city-c","energy",1,{"actor_id":"co-c","sector":"digital","valuation":"transaction_price","amount":3})
	var digital: Dictionary=Economy.metrics(s)
	_check(int(digital.gdp)==int(public_activity.gdp)+97 and int(digital.digital_current_price_value_added)==97,"Only earned digital output less its actual operating inputs adds digital value",failures)
	_check(int(digital.real_gdp)==int(world.real_gdp),"Current-price license output is not silently deflated using an invented price",failures)
	_check(int(Economy.metrics(s,"nation-ab").imports)==102,"Actual foreign digital license belongs to the correct trade boundary",failures)
	var city_total: int=0
	for city in s.cities: city_total+=int(Economy.metrics(s,city.id).gdp)
	_check(city_total==int(digital.gdp),"Public and digital output stays assigned to exactly one actual city",failures)
	print("Fixed-price metrics checks: "+str(checks)+"; failures: "+str(failures.size()))
	return failures
