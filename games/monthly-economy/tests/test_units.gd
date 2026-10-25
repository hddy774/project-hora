extends RefCounted
## Physical dimensions and total-rounded freight, independent of economic tuning.
const Economy=preload("res://core/economy.gd")
const Industry=preload("res://core/industry.gd")
const Market=preload("res://core/market.gd")
const Domain=preload("res://core/domain.gd")
const Ledger=preload("res://core/ledger.gd")
const Fixture=preload("res://tests/test_industry.gd")
static var checks: int=0

static func _check(value: bool, reason: String, failures: Array) -> void:
	checks+=1
	if not value: failures.append(reason)

static func _world() -> Dictionary:
	var s: Dictionary=Fixture._world()
	for route in s.industry.routes: route.unit_fee=1001
	return s

static func run() -> Array:
	checks=0
	var failures: Array=[]
	_test_definitions(failures)
	_test_mass_and_rounding(failures)
	_test_market_and_purchase(failures)
	_test_waste(failures)
	_test_durable_mass(failures)
	print("Physical unit checks: "+str(checks)+"; failures: "+str(failures.size()))
	return failures

static func _test_definitions(failures: Array) -> void:
	var products: Array=Economy._product_data()
	_check(products.size()==82,"Catalog keeps all 82 physical/service/digital definitions",failures)
	for product in products:
		var mass: int=int(product.get("cargo_mass_g_per_unit",-1))
		_check(mass==int(product.get("cargo_size",-2)),"Cargo alias is grams per declared unit: "+str(product.id),failures)
		_check(mass>0 if product.kind=="material" else mass==0,"Only physical goods have carried mass: "+str(product.id),failures)
		if product.unit=="kg" and product.kind=="material": _check(mass==1000,"A kilogram product is exactly 1000g: "+str(product.id),failures)
		_check(not str(product.get("quantity_note","")).is_empty(),"Declared product unit has a concrete meaning: "+str(product.id),failures)
	var base: Dictionary=Economy._fixture().duplicate(true)
	base.products[0].erase("cargo_mass_g_per_unit")
	_check(not Economy.validate_fixture(base).is_empty(),"Missing base product mass is rejected",failures)
	base=Economy._fixture().duplicate(true)
	base.products[0].cargo_size+=1
	_check(not Economy.validate_fixture(base).is_empty(),"Mismatched mass alias is rejected",failures)
	base=Economy._fixture().duplicate(true)
	for product in base.products:
		if product.kind=="service":
			product.cargo_size=1000
			product.cargo_mass_g_per_unit=1000
			break
	_check(not Economy.validate_fixture(base).is_empty(),"Time-based utility service cannot occupy physical warehouse mass",failures)
	base=Economy._fixture().duplicate(true)
	base.products[0].cargo_size=999
	base.products[0].cargo_mass_g_per_unit=999
	_check(not Economy.validate_fixture(base).is_empty(),"A base kg unit with the wrong gram conversion is rejected",failures)
	var extra: Dictionary=Industry._data().duplicate(true)
	extra.products[0].erase("cargo_mass_g_per_unit")
	_check(not Industry.validate_data(extra).is_empty(),"Missing expanded product mass is rejected",failures)
	var mill: Dictionary=Domain.find(Economy._recipe_data(),"mill")
	_check(int(mill.inputs.grain)==4 and int(mill.outputs.flour)==3,"Milling keeps three kg output without inventing grain mass",failures)
	for recipe in Industry._data().recipes:
		if not recipe.has("resource"): continue
		var metadata: Dictionary=Industry._data().resource_units.get(str(recipe.resource),{})
		var product: String=str(recipe.get("resource_product_id",metadata.get("product_id","")))
		_check(not metadata.is_empty() and metadata.unit==Domain.find(products,product).unit,"Resource depletion uses its declared physical output unit: "+str(recipe.id),failures)
		_check(int(recipe.resource_per_batch)==int(recipe.outputs.get(product,0)),"Finite extraction removes its actual declared raw quantity: "+str(recipe.id),failures)

static func _test_mass_and_rounding(failures: Array) -> void:
	var s: Dictionary=_world()
	_check(Industry.cargo_mass_g(s,"grain")==1000 and Industry.cargo_mass_g(s,"milk")==1030 and Industry.cargo_mass_g(s,"vehicle")==1200000,"kg, litre milk and a representative car convert through physical gram metadata",failures)
	_check(Industry.parameters().route_capacity==120000000,"Basic route handles 120 tonnes per month expressed as grams",failures)
	_check(Industry.parameters().onsite_storage==2000000000,"Onsite store holds 2000 tonnes expressed as grams",failures)
	var empty: Dictionary=_world()
	empty.inventory=[]
	Domain.change_inventory(empty,"co-a","c-a","grain",3,"opening",0,"opening")
	Domain.change_inventory(empty,"co-a","c-a","milk",2,"opening",0,"opening")
	Domain.change_inventory(empty,"co-a","c-a","vehicle",1,"opening",0,"opening")
	_check(Industry.storage_used(empty,"co-a","c-a")==1205060,"Mixed kg/L/car quantities sum only after conversion to the same gram dimension",failures)
	_check(Industry.freight_fee_for_mass(1000,1001)==2,"A 1kg shipment rounds its fractional cent once",failures)
	_check(Industry.freight_fee_for_mass(999000,1001)==1000,"999kg uses one total rounding, not 999 rounded per-unit charges",failures)
	_check(Industry.freight_fee_for_mass(1000000,1001)==1001,"One tonne pays the exact published cents-per-tonne rate",failures)
	_check(Industry.freight_fee_for_mass(9007199254740991,9007199254740991)==-1,"Unrepresentable freight arithmetic is rejected rather than overflowing",failures)
	var quote: Dictionary=Industry.route_quote(s,"c-a","c-b","wood",999,1)
	_check(quote.get("allowed",false) and int(quote.cargo_mass_g)==999000 and int(quote.fee)==1000,"Route quote reports actual mass and rounded shipment fee",failures)
	_check(int(quote.fee)!=int(quote.unit_fee)*int(quote.quantity),"Indicative rounded unit fee cannot be mistaken for the actual total",failures)
	var small: Dictionary=Industry.route_quote(s,"c-a","c-b","wood",10,1)
	_check(int(small.fee)==11 and int(small.cargo_mass_g)==10000,"Reduced quantities are re-quoted at their own total rounding",failures)
	var grams_before: int=int(s.industry.routes[0].used)
	var paid: Dictionary=Industry.reserve_route(s,"co-a","c-a","c-b","wood",10,1)
	_check(paid.get("ok",false) and int(s.industry.routes[0].used)-grams_before==10000,"A real booking consumes grams of monthly route capacity",failures)
	_check(Industry.cargo_mass_g(s,"energy")==0,"A kWh utility service is not physical cargo",failures)

static func _test_market_and_purchase(failures: Array) -> void:
	var s: Dictionary=_world()
	var sell: Dictionary={"owner_id":"co-b","city_id":"c-a","product_id":"wood","specification":"standard","offered_quality":50,"limit_price":3}
	var buy: Dictionary={"owner_id":"co-a","city_id":"c-b","product_id":"wood","specification":"standard","min_quality":0,"limit_price":5,"latest_delivery_month":12}
	var quote: Dictionary=Market._quote(s,sell,buy,999,1)
	_check(quote.get("allowed",false) and int(quote.cost)==999*3+1000,"Market limit compares the actual goods plus total-rounded freight",failures)
	# Add a genuine finite supplier to the compact fixture's production graph.
	s.recipes.append({"id":"unit-test-timber","inputs":{"energy":1},"outputs":{"wood":1},"labor":1})
	s.facilities.append({"id":"unit-test-timber-site","company_id":"co-b","city_id":"c-a","recipe_id":"unit-test-timber","kind":"forestry","status":"operational","capacity":10})
	var before: int=Ledger.balance(s,"co-a:cash")
	var bought: int=Economy._purchase(s,"co-a","c-b","wood",10,1,"운임 정수 회귀",41)
	_check(bought==10 and Ledger.balance(s,"co-a:cash")==before-41,"Capped purchase buys all ten affordable kg using freight total11, not unit fee2 times ten",failures)
	var tight: Dictionary=_world()
	tight.recipes=s.recipes.duplicate(true)
	tight.facilities.append(s.facilities[-1].duplicate(true))
	before=Ledger.balance(tight,"co-a:cash")
	bought=Economy._purchase(tight,"co-a","c-b","wood",10,1,"운임 예산 축소 회귀",40)
	_check(bought==9 and Ledger.balance(tight,"co-a:cash")==before-37,"Budget reduction re-quotes nine kg to freight10 and spends the exact total37",failures)
	_check(Ledger.validate(s).is_empty() and Ledger.validate(tight).is_empty(),"Freight rounding changes no cash without a balanced counterparty",failures)

static func _test_waste(failures: Array) -> void:
	var s: Dictionary=_world()
	Domain.change_inventory(s,"co-a","c-a","eggs",20,"opening",0,"opening")
	var eggs: Dictionary={}
	for lot in s.inventory:
		if lot.owner_id=="co-a" and lot.product_id=="eggs": eggs=lot
	eggs.expires_month=1
	Industry.expire_inventory(s,1)
	var mass_event: Dictionary={}
	for event in s.events:
		if event.type=="waste_mass_balance" and event.product_id=="eggs": mass_event=event
	_check(not mass_event.is_empty(),"Perishable count units produce an explicit physical disposal record",failures)
	if mass_event.is_empty(): return
	var expected_g: int=20*Industry.cargo_mass_g(s,"eggs")
	_check(int(mass_event.source_mass_g)==expected_g,"Egg counts convert to grams before organic-waste kg recovery",failures)
	_check(int(mass_event.recovered_mass_g)+int(mass_event.unrecovered_mass_g)==expected_g,"Recovered and unrecovered grams exactly reconcile",failures)
	_check(int(mass_event.waste_quantity)==int(expected_g*int(Industry.parameters().spoilage_waste_percent)/100000),"Recovery floors only the actual batch's kg quantity",failures)
	_check(int(mass_event.recovery_rounding_loss_g)>=0 and int(mass_event.recovery_rounding_loss_g)<1000,"Sub-kilogram recovery remainder is recorded instead of creating a whole kg",failures)
	_check(Industry.available_quantity(s,"co-a","organic_waste","c-a")==int(mass_event.waste_quantity),"Only recovered physical kg enter waste inventory",failures)

static func _test_durable_mass(failures: Array) -> void:
	var s: Dictionary=_world()
	Domain.change_inventory(s,"co-a","c-a","apparel",1,"opening",0,"opening")
	Domain.change_inventory(s,"co-a","c-a","vehicle",1,"opening",0,"opening")
	var before: int=Industry.available_quantity(s,"co-a","recyclable_waste","c-a")
	s.industry.durables.append({"id":"unit-apparel","owner_id":"co-a","city_id":"c-a","product_id":"apparel","quantity":1,"replacement_month":1,"status":"in_use"})
	s.industry.durables.append({"id":"unit-car","owner_id":"co-a","city_id":"c-a","product_id":"vehicle","quantity":1,"replacement_month":1,"status":"in_use"})
	Industry._wear_durables(s,1)
	var apparel: Dictionary={}
	var car: Dictionary={}
	for event in s.events:
		if event.type!="durable_mass_balance": continue
		if event.product_id=="apparel": apparel=event
		if event.product_id=="vehicle": car=event
	_check(not apparel.is_empty() and int(apparel.source_mass_g)==500 and int(apparel.waste_quantity)==0 and int(apparel.unrecovered_mass_g)==500,"A 500g garment never creates a whole kilogram of recyclable waste",failures)
	_check(not car.is_empty() and int(car.source_mass_g)==1200000 and int(car.recovered_mass_g)<=1200000,"A representative 1200kg car cannot produce more recovered mass than it contained",failures)
	_check(int(car.get("recovered_mass_g",0))+int(car.get("unrecovered_mass_g",0))==1200000,"Car recovery and residual loss reconcile in grams",failures)
	_check(Industry.available_quantity(s,"co-a","recyclable_waste","c-a")-before==1200,"Actual recovered car grams convert to kg rather than copying the count of cars",failures)
	before=Industry.available_quantity(s,"co-a","recyclable_waste","c-a")
	Industry._wear_durables(s,1)
	_check(Industry.available_quantity(s,"co-a","recyclable_waste","c-a")==before,"A worn durable cannot create recovery material twice",failures)
