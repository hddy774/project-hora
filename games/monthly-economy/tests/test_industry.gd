extends RefCounted
const Industry = preload("res://core/industry.gd")
const Ledger = preload("res://core/ledger.gd")
const Finance = preload("res://core/finance.gd")
const Domain = preload("res://core/domain.gd")
static var checks: int = 0

static func _check(failures: Array, condition: bool, message: String) -> void:
	checks += 1
	if not condition: failures.append(message)

static func _world() -> Dictionary:
	var defs: Dictionary = Industry.definitions()
	var products: Array = [
		{"id":"grain","name":"곡물","unit":"t","industry":"농업","price":3,"kind":"material","cargo_size":3},
		{"id":"flour","name":"밀가루","unit":"t","industry":"가공재","price":6,"kind":"material","cargo_size":3},
		{"id":"food","name":"식품","unit":"상자","industry":"소비재","price":14,"kind":"material","cargo_size":1},
		{"id":"wood","name":"목재","unit":"t","industry":"임업","price":3,"kind":"material","cargo_size":3},
		{"id":"stone","name":"석재","unit":"t","industry":"기초자원","price":2,"kind":"material","cargo_size":3},
		{"id":"tools","name":"공구","unit":"세트","industry":"자본재","price":16,"kind":"material","cargo_size":1},
		{"id":"energy","name":"전력","unit":"MWh","industry":"서비스","price":3,"kind":"service","cargo_size":0},
		{"id":"water","name":"용수","unit":"m³","industry":"서비스","price":1,"kind":"service","cargo_size":0}
	]
	products.append_array(defs.products)
	var s: Dictionary = {"seed":42,"version":0,"planning_month":1,"last_resolved_month":0,"phase":"Resolving","player_id":"p-player","companies":[],"people":[],"facilities":[],"cities":[{"id":"c-a","nation_id":"n-a","continent_id":"k-a","resources":{}},{"id":"c-b","nation_id":"n-b","continent_id":"k-b","resources":{}}],"products":products,"recipes":defs.recipes,"inventory":[],"shipments":[],"events":[],"monthly_labor":{},"commands":[],"accounts":{},"journal":[]}
	for suffix in ["a","b","c","d"]:
		var owner: String = "co-" + suffix
		var person: String = "p-" + suffix
		s.companies.append({"id":owner,"name":owner,"city_id":"c-a","director_id":person,"policy":{"price_percent":100},"status":"active","revenue":0,"expenses":0})
		Ledger.opening(s,owner,12000)
		s.people.append({"id":person,"company_id":owner,"city_id":"c-a","role":"director","hours":80,"wage":2,"employment_status":"employed","skills":{"operations":1,"research":1,"software":1}})
		Ledger.opening(s,person,1000)
		for i in range(2):
			var worker: String = person+"-"+str(i)
			s.people.append({"id":worker,"company_id":owner,"city_id":"c-a","role":"operator","hours":80,"wage":2,"employment_status":"employed","skills":{"operations":1,"research":1,"software":1}})
			Ledger.opening(s,worker,1000)
	var kinds: Array = ["software_studio","research_lab","logistics_hub","vegetable_farm"]
	for i in range(4):
		var definition: Dictionary = Domain.find(defs.buildings,kinds[i])
		s.facilities.append({"id":"f-"+str(i),"company_id":"co-"+["a","b","c","d"][i],"city_id":"c-a","kind":kinds[i],"recipe_id":definition.recipe_id,"capacity":definition.capacity,"status":"operational"})
	s.facilities.append({"id":"f-branch","company_id":"co-a","city_id":"c-b","kind":"warehouse","recipe_id":"i_warehouse","capacity":10,"status":"operational"})
	Industry.initialize(s)
	for owner in ["co-a","co-b","co-c","co-d"]:
		for product in ["computer","tools","steel","machine_parts","grain","fertilizer","wood","recyclable_waste"]: Domain.change_inventory(s,owner,"c-a",product,100,"opening",0,"opening")
	Domain.change_inventory(s,"co-c","c-a","transport_vehicle",2,"opening",0,"opening")
	Domain.change_inventory(s,"co-c","c-a","fuel",100,"opening",0,"opening")
	Domain.change_inventory(s,"co-d","c-a","livestock",4,"opening",0,"opening")
	return s

static func _command(owner: String, type: String, payload: Dictionary, id: String) -> Dictionary:
	return {"id":id,"actor_id":"p-"+owner.trim_prefix("co-"),"acting_company_id":owner,"expected_version":0,"type":type,"payload":payload}

static func _money(s: Dictionary) -> int:
	var value: int = 0
	for account in s.accounts.values():
		if account.kind == "cash": value += int(account.balance)
	return value

static func _stock(s: Dictionary, product: String) -> int:
	var value: int = 0
	for lot in s.inventory:
		if lot.product_id == product: value += int(lot.quantity)
	return value

static func _month(s: Dictionary, month: int) -> void:
	s.planning_month = month
	s.monthly_labor = {}
	Industry.before_turn(s,month)
	Industry.monthly(s,month)

static func run() -> Array:
	checks = 0
	var failures: Array = []
	_test_content(failures)
	_test_services(failures)
	_test_research_software(failures)
	_test_biology(failures)
	_test_logistics(failures)
	_test_recycling_repair_storage(failures)
	_test_closure_budget_quality(failures)
	_test_online_delivery(failures)
	_test_transit_cost_loss(failures)
	print("Industry checks: %d" % checks)
	return failures

static func _test_content(failures: Array) -> void:
	var data: Dictionary = Industry._data().duplicate(true)
	_check(failures,Industry.validate_data(data).is_empty(),"Complete industry definitions validate")
	_check(failures,data.products.size()>=60 and data.recipes.size()>=60,"Industry catalog covers distinct real supply chains")
	data.products[0].erase("unit")
	_check(failures,not Industry.validate_data(data).is_empty(),"Missing product unit fails content validation")
	data=Industry._data().duplicate(true)
	data.recipes[0].inputs["invented_missing_resource"]=1
	_check(failures,not Industry.validate_data(data).is_empty(),"Recipe cannot use an undefined product")
	var defs: Dictionary = Industry.definitions()
	defs.products[0].price=999999
	_check(failures,int(Industry.definitions().products[0].price)!=999999,"Definition query is an independent copy")
	var industries: Dictionary = {}
	for product in Industry.definitions().products: industries[product.industry]=true
	for name in ["농업","축산","수산","에너지 원자원","금속 광물","비금속 광물","기초소재","화학과 정유","부품과 산업용품","생활 소비재","내구 소비재","자본재","디지털 산업","서비스","군수"]:
		_check(failures,industries.has(name),"Industrial class has actual product definitions: "+name)
	_check(failures,Industry.command_types().size()>=14,"Industry user actions have command implementations")

static func _test_services(failures: Array) -> void:
	var s: Dictionary = _world()
	var opening: int = _money(s)
	Industry.before_turn(s,1)
	Industry.publish_service_capacity(s,"co-b","c-a","energy",10,1)
	_check(failures,Industry.service_supply(s,"c-a","energy")==10,"Service output is current-month capacity")
	_check(failures,_stock(s,"energy")==0,"Publishing a service creates no physical inventory")
	var paid: Dictionary = Industry.purchase_service(s,"co-a","c-a","energy",3,1)
	_check(failures,paid.quantity==3 and paid.cost==9,"Service order has a real price and finite provider capacity")
	_check(failures,Industry.service_available(s,"co-a","energy","c-a")==3,"Buyer receives only paid service rights")
	_check(failures,Industry.consume_service(s,"co-a","c-a","energy",2,1),"Service right can be consumed")
	_check(failures,Industry.service_available(s,"co-a","energy","c-a")==1,"Consumed service cannot be reused")
	_check(failures,not Industry.consume_service(s,"co-a","c-a","energy",2,1),"Overspending service capacity is rejected")
	_check(failures,_money(s)==opening and Ledger.validate(s).is_empty(),"Service settlement transfers funds without creating money")
	s.planning_month=2
	Industry.before_turn(s,2)
	_check(failures,Industry.service_available(s,"co-a","energy","c-a")==0 and Industry.service_supply(s,"c-a","energy")==0,"Unused rights and service capacity do not carry into another month")
	_check(failures,Industry.validate(s).is_empty(),"Service state validates after expiration")

static func _test_research_software(failures: Array) -> void:
	var s: Dictionary = _world()
	var opening: int = _money(s)
	var research: Dictionary = _command("co-b","industry_research",{"topic":"process_efficiency","monthly_budget":100},"r1")
	_check(failures,Industry.apply(s,research,1).ok,"Research starts with actual computer, provider and funding")
	_check(failures,not Industry.apply(s,research,1).ok,"A command ID cannot pay twice")
	var software: Dictionary = _command("co-a","industry_software",{"name":"Workbench","platform":"open-platform","category":"business","kind":"download","price":30},"s1")
	_check(failures,Industry.apply(s,software,1).ok,"Software starts as a labor project rather than manufactured inventory")
	for month in range(1,4):
		_month(s,month)
		var journals: int = s.journal.size()
		Industry.monthly(s,month)
		_check(failures,s.journal.size()==journals,"Same-month module processing cannot pay work twice")
	_check(failures,Industry._tech(s,"co-b","process_efficiency")==1,"Research needs actual paid work and its minimum period")
	var developed: Dictionary = s.industry.software[0]
	_check(failures,developed.status=="ready" and int(developed.work)==int(developed.required_work),"Software completes development and testing after the required work")
	_check(failures,_stock(s,"software_license")==0,"Development does not manufacture software license stock")
	var adopt: Dictionary = _command("co-b","industry_adopt",{"topic":"process_efficiency","facility_id":"f-1"},"a1")
	_check(failures,Industry.apply(s,adopt,4).ok,"Technology installation spends real tools and funds")
	_month(s,4)
	_check(failures,not Domain.find(s.facilities,"f-1").get("tech_levels",{}).has("process_efficiency"),"Paid adoption is not instantly installed")
	_month(s,5)
	_check(failures,int(Domain.find(s.facilities,"f-1").tech_levels.get("process_efficiency",0))==1,"Technology activates after installation work and elapsed period")
	var release: Dictionary = _command("co-a","industry_release",{"software_id":developed.id},"release1")
	_check(failures,Industry.apply(s,release,5).ok,"Developer controls release of a finished product")
	var bad: Dictionary = _command("co-b","industry_license",{"software_id":developed.id,"seats":2,"platform":"wrong-platform"},"bad1")
	var buyer_before: int = Ledger.balance(s,"co-b:cash")
	_check(failures,not Industry.apply(s,bad,5).ok and Ledger.balance(s,"co-b:cash")==buyer_before,"Incompatible licenses fail before payment")
	var license: Dictionary = _command("co-b","industry_license",{"software_id":developed.id,"seats":2,"platform":"open-platform"},"l1")
	_check(failures,Industry.apply(s,license,5).ok and Ledger.balance(s,"co-b:cash")==buyer_before-60,"License purchase pays the actual publisher")
	_check(failures,_stock(s,"software_license")==0 and s.industry.licenses.size()==1,"Digital rights are contracts instead of cloned goods")
	var subscribe: Dictionary = _command("co-c","industry_subscribe",{"software_id":developed.id,"seats":1,"platform":"open-platform"},"sub1")
	_check(failures,Industry.apply(s,subscribe,5).ok,"Subscription records one paid period")
	var subscription: Dictionary = s.industry.subscriptions[0]
	var cash_before: int = Ledger.balance(s,"co-c:cash")
	_month(s,6)
	_check(failures,Ledger.balance(s,"co-c:cash")==cash_before-30 and int(subscription.next_due)==7,"Subscription renews from real buyer funds at its due month")
	_check(failures,Industry.apply(s,_command("co-c","industry_cancel_subscription",{"subscription_id":subscription.id},"sub-cancel"),6).ok,"Subscriber controls future renewal")
	cash_before=Ledger.balance(s,"co-c:cash")
	_month(s,7)
	_check(failures,Ledger.balance(s,"co-c:cash")==cash_before and subscription.status=="cancelled","Cancelled subscriptions do not create another charge")
	_check(failures,_money(s)==opening and Ledger.validate(s).is_empty(),"Research, installation, software, licenses and renewals conserve money")
	_check(failures,Industry.validate(s).is_empty(),"Research and digital state validates")

static func _test_biology(failures: Array) -> void:
	var s: Dictionary = _world()
	s.planning_month=2
	Industry.before_turn(s,2)
	Industry.publish_service_capacity(s,"co-d","c-a","water",10,2)
	var before_grain: int = Industry.available_quantity(s,"co-d","grain","c-a")
	var command: Dictionary = _command("co-d","industry_agriculture",{"facility_id":"f-3","batches":1},"crop1")
	_check(failures,Industry.apply(s,command,2).ok,"Seasonal crop starts with real seeds, fertilizer, water and paid work")
	_check(failures,Industry.available_quantity(s,"co-d","grain","c-a")==before_grain-1 and Industry.available_quantity(s,"co-d","vegetables","c-a")==0,"Planting consumes inputs without instant crop inventory")
	s.planning_month=3
	Industry.before_turn(s,3)
	_check(failures,Industry.available_quantity(s,"co-d","vegetables","c-a")==0,"Growing crops cannot be harvested early")
	s.planning_month=4
	Industry.before_turn(s,4)
	_check(failures,Industry.available_quantity(s,"co-d","vegetables","c-a")==5,"Completed biological work generates the specified harvest once")
	Industry.before_turn(s,4)
	_check(failures,Industry.available_quantity(s,"co-d","vegetables","c-a")==5,"Reopening the same turn cannot harvest again")
	Industry.publish_service_capacity(s,"co-d","c-a","water",10,4)
	s.monthly_labor={}
	command=_command("co-d","industry_agriculture",{"facility_id":"f-3","batches":1},"crop2")
	_check(failures,Industry.apply(s,command,4).ok,"A new season can start another real crop")
	var project: Dictionary = s.industry.agriculture[-1]
	var seed_left: int = Industry.available_quantity(s,"co-d","grain","c-a")
	_check(failures,Industry.apply(s,_command("co-d","industry_cancel_agriculture",{"project_id":project.id},"cancelcrop"),4).ok,"Biological work can be cancelled")
	_check(failures,Industry.available_quantity(s,"co-d","grain","c-a")==seed_left,"Cancelling does not recreate consumed seed or labor")
	var reserve: int = Industry.resource_available(s,"c-a","iron_reserve")
	_check(failures,Industry.consume_resource(s,"c-a","iron_reserve",reserve,4,"co-d"),"Extraction reduces a finite deposit")
	_check(failures,not Industry.consume_resource(s,"c-a","iron_reserve",1,4,"co-d") and Industry.resource_available(s,"c-a","iron_reserve")==0,"An exhausted mine cannot create more ore")
	_check(failures,Industry.validate(s).is_empty(),"Biological and deposit state validates")

static func _test_logistics(failures: Array) -> void:
	var s: Dictionary = _world()
	Industry.before_turn(s,1)
	var wood: int = _stock(s,"wood")
	var fuel: int = Industry.available_quantity(s,"co-c","fuel","c-a")
	var company_cash: int = Ledger.balance(s,"co-a:cash")
	var quote: Dictionary = Industry.route_quote(s,"c-a","c-b","wood",20,1)
	_check(failures,quote.get("allowed",false) and int(quote.eta)>1,"Remote delivery needs an actual carrier, finite route capacity and time")
	var result: Dictionary = Industry.apply(s,_command("co-a","industry_ship",{"product_id":"wood","quantity":20,"to_city":"c-b"},"ship1"),1)
	_check(failures,result.ok,"Owned cargo can be dispatched using the quoted route")
	_check(failures,_stock(s,"wood")==wood and s.shipments.size()==1,"Dispatch preserves the one actual cargo quantity")
	_check(failures,Ledger.balance(s,"co-a:cash")<company_cash and Industry.available_quantity(s,"co-c","fuel","c-a")<fuel,"Carrier service spends real buyer funds, fuel and labor")
	var shipment: Dictionary = s.shipments[0]
	var route: Dictionary = Domain.find(s.industry.routes,str(shipment.route_ids[0]))
	route.blocked=true
	var due: int = int(shipment.eta)
	Industry.receive_due_shipments(s,due)
	_check(failures,shipment.status=="transit" and int(shipment.eta)==due+1 and _stock(s,"wood")==wood,"Blocking a route delays cargo without deleting or duplicating it")
	route.blocked=false
	Industry.receive_due_shipments(s,due+1)
	_check(failures,shipment.status=="delivered" and Industry.available_quantity(s,"co-a","wood","c-b")==20,"Cargo arrives only after the route is accessible and its date has passed")
	_check(failures,_stock(s,"wood")==wood,"Arrival changes city location instead of creating a second cargo")
	_check(failures,Industry.validate(s).is_empty() and Ledger.validate(s).is_empty(),"Logistics accounts, route limits and cargo state validate")

static func _test_recycling_repair_storage(failures: Array) -> void:
	var s: Dictionary = _world()
	var waste: int = Industry.available_quantity(s,"co-a","recyclable_waste","c-a")
	var steel: int = Industry.available_quantity(s,"co-a","steel","c-a")
	_check(failures,Industry.apply(s,_command("co-a","industry_recycle",{"batches":2},"recycle1"),1).ok,"Recycling needs actual recovered material and paid work")
	_check(failures,Industry.available_quantity(s,"co-a","recyclable_waste","c-a")==waste-6 and Industry.available_quantity(s,"co-a","steel","c-a")==steel+2,"Recycling uses a bounded recipe instead of returning unlimited inputs")
	var facility: Dictionary = Domain.find(s.facilities,"f-0")
	facility.status="damaged"; facility.damage_percent=20; facility.repair_required=true
	_check(failures,Industry.apply(s,_command("co-a","industry_repair",{"facility_id":"f-0"},"repair1"),1).ok,"Damaged facility repair uses real tools and parts")
	_month(s,1)
	_check(failures,facility.status=="damaged","Repairs cannot restore a damaged plant instantly")
	_month(s,2)
	_check(failures,facility.status=="operational" and not facility.repair_required,"Paid repair work restores actual facility operation after time passes")
	var capacity: int = Industry.storage_capacity(s,"co-a","c-a")
	_check(failures,Industry.apply(s,_command("co-a","industry_warehouse",{"refrigerated":"yes"},"warehouse1"),2).ok,"Warehouse installation consumes material, labor and actual fees")
	_check(failures,Industry.storage_capacity(s,"co-a","c-a")==capacity,"New storage is not operational before its installation month")
	s.planning_month=3
	Industry.before_turn(s,3)
	_check(failures,Industry.storage_capacity(s,"co-a","c-a")>capacity,"Completed warehouse installation adds finite real capacity")
	Domain.change_inventory(s,"co-a","c-a","milk",8,"opening",3,"opening")
	var milk: Dictionary = {}
	for item in s.inventory:
		if item.owner_id=="co-a" and item.product_id=="milk" and int(item.quantity)>0: milk=item
	Industry.publish_service_capacity(s,"co-b","c-a","energy",10,3)
	Industry.after_turn(s,3)
	_check(failures,int(milk.expires_month)>5 and int(milk.cold_original_expiry)==5,"Refrigerated storage uses actual electricity and gives a bounded life extension")
	s.planning_month=4
	Industry.before_turn(s,4)
	Industry.after_turn(s,4)
	_check(failures,int(milk.expires_month)==5,"Power loss removes the refrigeration extension rather than preserving food forever")
	s.planning_month=5
	Industry.before_turn(s,5)
	_check(failures,Industry.available_quantity(s,"co-a","milk","c-a")==0,"Expired perishable stock is removed before production and consumption")
	_check(failures,Industry.available_quantity(s,"co-a","organic_waste","c-a")>0,"Food spoilage produces organic waste, never metal scrap")
	_check(failures,Industry.validate(s).is_empty() and Ledger.validate(s).is_empty(),"Recycling, repairs and cold-storage state reconcile")

static func _test_closure_budget_quality(failures: Array) -> void:
	var s: Dictionary = _world()
	Industry.before_turn(s, 1)
	Industry.publish_service_capacity(s, "co-b", "c-a", "energy", 10, 1)
	var spending: Dictionary = Industry.purchase_service(s, "co-a", "c-a", "energy", 10, 1, -1, false, 8)
	_check(failures, int(spending.quantity) == 2 and int(spending.cost) == 6, "A service budget caps cumulative actual spending")
	var development: Dictionary = _command("co-a", "industry_software", {"name":"Online Workbench","platform":"open-platform","category":"business","kind":"online","price":30}, "online-project")
	_check(failures, Industry.apply(s, development, 1).ok, "Online products use the same paid development project")
	for month in range(1, 4): _month(s, month)
	var online: Dictionary = s.industry.software[0]
	_check(failures, Industry.apply(s, _command("co-a", "industry_release", {"software_id":online.id}, "online-release"), 3).ok, "Online release requires completed work")
	Domain.change_inventory(s, "co-a", "c-a", "server", 1, "opening", 0, "opening")
	var perpetual: Dictionary = _command("co-b", "industry_license", {"software_id":online.id,"seats":1,"platform":"open-platform"}, "online-perpetual")
	_check(failures, Industry.validate_command(s, perpetual) == "ONLINE_SUBSCRIPTION_REQUIRED", "Online operation does not promise unfunded perpetual service")
	var subscribe: Dictionary = _command("co-b", "industry_subscribe", {"software_id":online.id,"seats":2,"platform":"open-platform"}, "online-subscribe")
	_check(failures, Industry.apply(s, subscribe, 3).ok, "An online subscription pays for its defined period")
	var original_money: int = _money(s)
	var remaining_cash: int = Ledger.balance(s, "co-a:cash")
	Domain.pay(s, "estate-cash-spent", "co-a", "co-c", remaining_cash - 20, "actual prior obligations", 3)
	Domain.find(s.companies, "co-a").status = "liquidating"
	Industry.register_closure_claims(s, 3)
	_check(failures, s.industry.refund_claims.size() == 1 and int(s.industry.refund_claims[0].amount) == 60, "Unprovided prepaid period becomes a real estate claim")
	Industry.register_closure_claims(s, 3)
	_check(failures, s.industry.refund_claims.size() == 1, "Repeating closure inspection creates no duplicate claim")
	Industry._pay_refunds(s, 3)
	var claim: Dictionary = s.industry.refund_claims[0]
	_check(failures, int(claim.paid) == 20 and claim.status == "pending", "An insolvent publisher pays only actual available estate cash")
	_check(failures, Ledger.balance(s, str(claim.liability_account)) == -40 and Ledger.balance(s, str(claim.receivable_account)) == 40, "Unpaid refund survives as matching debt and receivable")
	Domain.pay(s, "estate-asset-sale", "co-d", "co-a", 40, "actual estate asset buyer payment", 3)
	Industry._pay_refunds(s, 4)
	_check(failures, int(claim.paid) == 60 and claim.status == "paid", "Later actual estate proceeds can settle the outstanding claim")
	_check(failures, _money(s) == original_money and Ledger.validate(s).is_empty(), "Refund recognition and payment never mint recovery funds")
	_check(failures, Industry.validate(s).is_empty(), "Closed online contracts and refund ledger validate")
	var planning: Dictionary = _world()
	planning.phase = "Planning"
	planning.monthly_labor["p-b-0"] = 80
	var research: Dictionary = _command("co-b", "industry_research", {"topic":"product_quality","monthly_budget":100}, "next-month-research")
	var reservations: Array = Industry.planning_reservations(planning, research)
	var own_labor: int = 0
	for reservation in reservations:
		_check(failures, str(reservation.owner_id) == "co-b" or Domain.find(planning.people, str(reservation.owner_id)).get("company_id") == "co-b", "Industry planning reserves only its own resources")
		if reservation.resource_type == "labor": own_labor += int(reservation.quantity)
	_check(failures, own_labor > 0, "Next planning month ignores already-resolved prior labor totals")
	var facility: Dictionary = Domain.find(planning.facilities, "f-1")
	var ordinary: int = Industry.product_quality(planning, facility)
	planning.industry.adoptions.append({"id":"test-installed-quality","facility_id":facility.id,"owner_id":"co-b","topic":"product_quality","status":"installed","level":2})
	_check(failures, Industry.product_quality(planning, facility) > ordinary, "Installed quality research changes actual output quality metadata")

static func _test_online_delivery(failures: Array) -> void:
	var s: Dictionary = _world()
	var development: Dictionary = _command("co-a", "industry_software", {"name":"Paid Online","platform":"open-platform","kind":"online","price":30}, "earned-development")
	Industry.apply(s, development, 1)
	for month in range(1,4): _month(s, month)
	var online: Dictionary = s.industry.software[0]
	Industry.apply(s, _command("co-a", "industry_release", {"software_id":online.id}, "earned-release"), 3)
	Domain.change_inventory(s, "co-a", "c-a", "server", 1, "opening", 0, "opening")
	var money: int = _money(s)
	var revenue: int = Ledger.balance(s, "co-a:revenue")
	Industry.apply(s, _command("co-c", "industry_subscribe", {"software_id":online.id,"seats":2,"platform":"open-platform"}, "earned-subscription"), 3)
	_check(failures, Ledger.balance(s, "co-a:industry-online-advance") == -60 and Ledger.balance(s, "co-c:industry-online-prepaid") == 60, "Online prepayment is an obligation and customer prepaid asset")
	_check(failures, Ledger.balance(s, "co-a:revenue") == revenue, "Cash received before online delivery is not earned revenue")
	Industry.publish_service_capacity(s, "co-b", "c-a", "energy", 20, 3)
	Industry.publish_service_capacity(s, "co-b", "c-a", "telecom_access", 20, 3)
	var advertising: Dictionary = _command("co-b", "industry_advertise", {"software_id":online.id,"budget":6}, "earned-advertising")
	_check(failures, Industry.apply(s, advertising, 3).ok, "Advertiser budgets are separate actual customer money")
	Industry.deliver_digital(s, 3)
	_check(failures, int(online.active_seats) == 2 and int(s.industry.licenses[0].served_seats) == 2, "Server, energy and network provide actual paid online seats")
	_check(failures, Ledger.balance(s, "co-a:industry-online-advance") == 0 and Ledger.balance(s, "co-c:industry-online-prepaid") == 0, "Delivered online periods release matching prepaid and obligation balances")
	_check(failures, Ledger.balance(s, "co-a:revenue") == revenue - 60 - int(s.industry.advertising[0].spent), "Only delivered subscription and actual impressions earn provider revenue")
	var digital: int = 0
	var inputs: int = 0
	for event in s.events:
		if event.type == "gdp_output" and event.get("sector") == "digital" and int(event.month) == 3: digital += int(event.amount)
		if event.type == "gdp_input" and event.get("sector") == "digital" and int(event.month) == 3: inputs += int(event.amount)
	_check(failures, digital == 60 + int(s.industry.advertising[0].spent) and int(s.industry.advertising[0].spent) > 0 and inputs > 0, "Digital output and its actually consumed operating services enter explainable GDP events")
	var journal_count: int = s.journal.size()
	Industry.deliver_digital(s, 3)
	_check(failures, s.journal.size() == journal_count, "Repeated digital delivery cannot earn or charge twice")
	_month(s, 4)
	var buyer_cash: int = Ledger.balance(s, "co-c:cash")
	Industry.deliver_digital(s, 4)
	_check(failures, int(online.active_seats) == 0 and Ledger.balance(s, "co-c:cash") == buyer_cash + 60, "A real operating outage refunds its unprovided prepaid period from provider funds")
	var outage_output: int = 0
	for event in s.events:
		if event.type == "gdp_output" and int(event.month) == 4 and event.get("sector") == "digital": outage_output += int(event.amount)
	_check(failures, outage_output == 0, "Outage registrations and refunded subscriptions create no phantom GDP")
	_check(failures, _money(s) == money and Ledger.validate(s).is_empty() and Industry.validate(s).is_empty(), "Delivered periods, ads and outage refunds conserve actual money and obligations")

static func _test_transit_cost_loss(failures: Array) -> void:
	var s: Dictionary = _world()
	Domain.change_inventory(s, "co-a", "c-a", "vegetables", 3, "opening", 0, "opening")
	s.nations = [{"id":"n-a","name":"A"},{"id":"n-b","name":"B"}]
	Finance.initialize(s)
	var stock_account: String = Finance.inventory_account(s, "co-a", "vegetables")
	var value: int = Ledger.balance(s, stock_account)
	_check(failures, value > 0, "Perishable cargo has a real opening inventory carrying value")
	var ship: Dictionary = _command("co-a", "industry_ship", {"product_id":"vegetables","quantity":3,"to_city":"c-b"}, "dated-cargo")
	_check(failures, Industry.apply(s, ship, 1).ok, "Dated cargo dispatch uses real paid carrier capacity")
	_check(failures, Ledger.balance(s, stock_account) == value, "Dispatch retains the owner's in-transit stock asset")
	var shipment: Dictionary = s.shipments[0]
	shipment.eta = 8
	Industry.receive_due_shipments(s, 8)
	_check(failures, shipment.status == "spoiled" and _stock(s, "vegetables") == 0, "Expired cargo is explicitly lost instead of delivered or duplicated")
	_check(failures, Ledger.balance(s, stock_account) == 0, "Transit spoilage writes off the real stock asset before physical removal")
	_check(failures, Ledger.validate(s).is_empty(), "Actual cargo loss keeps the carrying-value ledger balanced")
