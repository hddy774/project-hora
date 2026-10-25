extends RefCounted
const Public = preload("res://core/public_sector.gd")
const Ledger = preload("res://core/ledger.gd")

static var _serial: int=0
static var _checks: int=0

static func _check(value: bool, message: String, failures: Array) -> void:
	_checks+=1
	if not value: failures.append(message)

static func _find(items: Array,id: String) -> Dictionary:
	for item in items:
		if item.get("id","")==id: return item
	return {}

static func _command(s: Dictionary, actor: String, org: String, type: String, payload: Dictionary) -> Dictionary:
	_serial+=1
	return {"id":"public-test-"+str(_serial),"actor_id":actor,"acting_company_id":org,"type":type,"payload":payload,"expected_version":s.version}

static func _apply(s: Dictionary, actor: String, org: String, type: String, payload: Dictionary, failures: Array) -> Dictionary:
	var c: Dictionary=_command(s,actor,org,type,payload)
	var error: String=Public.validate_command(s,c)
	_check(error.is_empty(),type+" validates: "+error,failures)
	if not error.is_empty(): return {"ok":false,"error":error}
	var result: Dictionary=Public.apply(s,c,int(s.planning_month))
	_check(result.get("ok",false),type+" applies: "+str(result.get("error","")),failures)
	_check(Ledger.validate(s).is_empty(),type+" keeps balanced/reconstructible owner accounts",failures)
	return result

static func _cash_sum(s: Dictionary) -> int:
	var total: int=0
	for account in s.accounts.values():
		if account.kind=="cash": total+=int(account.balance)
	return total

static func _quantity(s: Dictionary, owner: String, product: String) -> int:
	var total: int=0
	for lot in s.inventory:
		if (owner=="" or lot.owner_id==owner) and lot.product_id==product: total+=int(lot.quantity)
	return total

static func _world() -> Dictionary:
	var s: Dictionary={"seed":42,"version":0,"planning_month":1,"last_resolved_month":0,"phase":"Planning","player_id":"p-0","people":[],"companies":[],"cities":[],"nations":[],"continents":[],"facilities":[],"products":[{"id":"food","name":"식품","unit":"상자","industry":"소비재","price":14},{"id":"tools","name":"공구","unit":"세트","industry":"자본재","price":16},{"id":"water","name":"용수","unit":"m3","industry":"서비스","price":1}],"recipes":[{"id":"bake","inputs":{"water":1},"outputs":{"food":5},"labor":6}],"inventory":[],"events":[],"accounts":{},"journal":[],"commands":[],"monthly_labor":{},"shipments":[]}
	for i in range(3):
		var city: String="city-"+str(i)
		var nation: String="nation-"+str(i)
		var company: String="co-"+str(i)
		s.continents.append({"id":"continent-"+str(i),"name":"대륙"+str(i)})
		s.nations.append({"id":nation,"name":"국가"+str(i),"continent_id":"continent-"+str(i),"currency":"CR"})
		s.cities.append({"id":city,"name":"도시"+str(i),"nation_id":nation,"continent_id":"continent-"+str(i)})
		for j in range(5):
			var person: String="p-"+str(i*5+j)
			s.people.append({"id":person,"name":"인물"+str(i*5+j),"city_id":city,"company_id":company,"role":"director" if j==0 else "operator","hours":80,"wage":2,"employment_status":"employed","last_income":100})
			Ledger.opening(s,person,1000,0)
		s.companies.append({"id":company,"name":"회사"+str(i),"city_id":city,"director_id":"p-"+str(i*5),"shareholder_id":"p-"+str(i*5),"policy":{"price_percent":100,"production_percent":100},"status":"active","revenue":0,"expenses":0})
		Ledger.opening(s,company,10000,0)
		s.facilities.append({"id":"facility-"+str(i),"company_id":company,"city_id":city,"recipe_id":"bake","capacity":20,"status":"operational","quality":50})
		var lot: Dictionary={"id":"lot:"+company+":"+city+":food","owner_id":company,"city_id":city,"accounting_city_id":city,"product_id":"food","quantity":80,"reserved":0,"location":"warehouse","unit_cost":7}
		s.inventory.append(lot)
		s.events.append({"id":"ev-"+str(s.events.size()),"month":0,"type":"opening","reason":"개시 재고","actor_id":company,"city_id":city,"product_id":"food","quantity":80,"amount":0,"inventory_delta":80,"lot_id":lot.id})
	Public.initialize(s)
	return s

static func _stock_invariants(s: Dictionary, failures: Array) -> void:
	var totals: Dictionary={}
	for event in s.events:
		if event.has("inventory_delta"): totals[event.lot_id]=int(totals.get(event.lot_id,0))+int(event.inventory_delta)
	for lot in s.inventory:
		_check(int(lot.quantity)>=0 and int(lot.reserved)>=0 and int(lot.reserved)<=int(lot.quantity),"Public stock and reservation are finite nonnegative",failures)
		_check(int(totals.get(lot.id,0))==int(lot.quantity),"Every public stock change has an actual inventory event: "+str(lot.id),failures)

static func run() -> Array:
	_serial=0
	_checks=0
	var failures: Array=[]
	var s: Dictionary=_world()
	_check(Public.validate(s).is_empty(),"Public opening schema and reservations valid",failures)
	_check(Ledger.validate(s).is_empty(),"Public opening cash has balanced counterpart entries",failures)
	_check(s.public.governments.size()==6 and s.public.armies.size()==3,"Only city/nation institutions, no continent/world government",failures)
	var total_cash: int=_cash_sum(s)
	var g: Dictionary=_find(s.public.governments,"gov-nation-nation-0")
	var office: Dictionary=_find(s.public.offices,g.office_id)
	var actor: String=office.person_id
	var before: String=JSON.stringify(s,"",true)
	var forbidden: Dictionary=Public.apply(s,_command(s,"p-0",g.id,"public_set_budget",{"government_id":g.id,"category":"military","amount":20}),1)
	_check(not forbidden.ok,"Corporate ownership is not public budget authority",failures)
	_check(JSON.stringify(s,"",true)==before,"Unauthorized public command preserves every account and state",failures)
	var huge: Dictionary=Public.apply(s,_command(s,actor,g.id,"public_announce_procurement",{"government_id":g.id,"product_id":"food","quantity":100000,"max_unit_price":100,"deadline_month":3,"military":true}),1)
	_check(not huge.ok,"An agency cannot reserve more money than its finite budget",failures)
	var opened: Dictionary=_apply(s,actor,g.id,"public_announce_procurement",{"government_id":g.id,"product_id":"food","quantity":6,"max_unit_price":30,"deadline_month":3,"military":true},failures)
	if not opened.get("ok",false): return failures
	var contract: Dictionary=_find(s.public.procurements,opened.contract_id)
	_check(int(g.reserved)==180 and int(contract.payable)==0,"Announcement reserves funds without sales or payment",failures)
	_apply(s,"p-0","co-0","public_bid",{"contract_id":contract.id,"unit_price":14,"delivery_month":3},failures)
	var bid: Dictionary=s.public.bids[-1]
	_check(Public.summary(s,"").bids.is_empty(),"Public report does not expose sealed submitted bids",failures)
	_check(Public.summary(s,"p-0").bids.size()==1,"Bidder can inspect its own submitted proposal",failures)
	_apply(s,actor,g.id,"public_award",{"government_id":g.id,"contract_id":contract.id,"bid_id":bid.id,"advance_bps":2000},failures)
	_check(int(contract.advance_paid)==16 and int(contract.payable)==0,"Advance is a finite payment and contract liability, not sales",failures)
	_check(int(_find(s.companies,"co-0").revenue)==0,"Award and advance do not create product revenue",failures)
	var unpaid: Dictionary=Public.apply(s,_command(s,actor,g.id,"public_pay",{"government_id":g.id,"contract_id":contract.id}),1)
	_check(not unpaid.ok,"Uninspected goods cannot be paid as completed deliveries",failures)
	var all_food: int=_quantity(s,"","food")
	var delivery_result: Dictionary=_apply(s,"p-0","co-0","public_deliver",{"contract_id":contract.id,"quantity":6},failures)
	var delivery: Dictionary=_find(s.public.deliveries,delivery_result.get("delivery_id",""))
	_check(_quantity(s,"","food")==all_food,"Delivery changes location without duplicating goods",failures)
	var held: Dictionary=_find(s.inventory,delivery.get("lot_id",""))
	_check(held.get("owner_id")=="co-0" and int(held.get("reserved",0))==6,"Before inspection seller retains title and cannot reuse reserved goods",failures)
	_apply(s,actor,g.id,"public_inspect",{"government_id":g.id,"delivery_id":delivery.id},failures)
	_check(int(contract.payable)==68 and int(contract.accepted_quantity)==6,"Inspection recognizes only actual goods and offsets earned advance",failures)
	_check(int(_find(s.companies,"co-0").revenue)==84,"Inspection recognizes contract sales exactly once",failures)
	_apply(s,actor,g.id,"public_pay",{"government_id":g.id,"contract_id":contract.id},failures)
	_check(contract.status=="fulfilled" and int(contract.cash_paid)==84 and int(g.reserved)==0,"Final payment releases only this contract reservation",failures)
	_check(int(contract.payable)==0 and int(contract.advance_remaining)==0,"Contract principal obligations reconcile after fulfillment",failures)
	_check(_cash_sum(s)==total_cash,"Procurement, bids and advances never create world cash",failures)
	var repeat: Dictionary=Public.apply(s,_command(s,actor,g.id,"public_inspect",{"government_id":g.id,"delivery_id":delivery.id}),1)
	_check(not repeat.ok,"Accepted delivery cannot be recognized twice",failures)
	Public.begin_month(s,1)
	var army: Dictionary=s.public.armies[0]
	_check(Public.provided_need(s,army.members[0],"food",1)==2,"Actual military meal offsets the same person's household need",failures)
	_check(_quantity(s,g.id,"food")==2,"Army consumes actual military inventory",failures)
	Public.monthly(s,1)
	_check(_cash_sum(s)==total_cash,"Public wages, supplies, tax and transfers all have cash counterparties",failures)
	_check(Ledger.validate(s).is_empty(),"Taxes and public-service asset consumption reconcile",failures)
	_check(Public.validate(s).is_empty(),"Post-service government reservations and deliveries reconcile",failures)
	var nonmarket_net: int=0
	var actual_public_wages: int=0
	for event in s.events:
		if event.get("sector","")=="public_nonmarket": nonmarket_net+=int(event.amount)*(1 if event.type=="gdp_output" else -1)
		if event.type=="public_payment" and int(event.get("hours",0))>0: actual_public_wages+=int(event.amount)
	_check(nonmarket_net==actual_public_wages and nonmarket_net>0,"Public nonmarket VA equals actual paid work after real intermediate costs, not procurement or transfers",failures)
	_stock_invariants(s,failures)
	# Citizen political activity does not grant arbitrary treasury access.
	var party: Dictionary=s.public.parties[0]
	_apply(s,"p-0","","public_join_party",{"party_id":party.id},failures)
	_apply(s,"p-0","","public_run_office",{"government_id":g.id,"platform":"services"},failures)
	var candidate: Dictionary=s.public.candidates[-1]
	_apply(s,"p-0","","public_vote",{"candidate_id":candidate.id},failures)
	var double_vote: Dictionary=Public.apply(s,_command(s,"p-0","","public_vote",{"candidate_id":candidate.id}),1)
	_check(not double_vote.ok,"Each person has one ballot per jurisdiction/election",failures)
	s.planning_month=3
	s.monthly_labor={}
	Public.monthly(s,3)
	_check(_find(s.public.offices,g.office_id).person_id=="p-0","A citizen can win office using the same rules as AI candidates",failures)
	_check(_find(s.companies,"co-0").director_id=="p-0","Political appointment does not transfer corporate title or assets",failures)
	var proposal_result: Dictionary=_apply(s,"p-0","","public_propose_policy",{"government_id":g.id,"key":"import_ban","value":1,"other_nation_id":"nation-1","product_id":"food"},failures)
	if proposal_result.get("ok",false):
		var proposal: Dictionary=s.public.policy_proposals[-1]
		_apply(s,"p-1","","public_vote_policy",{"proposal_id":proposal.id,"support":true},failures)
		var closed_vote: Dictionary=Public.apply(s,_command(s,"p-2","","public_vote_policy",{"proposal_id":proposal.id,"support":true}),3)
		_check(not closed_vote.ok,"Approved policy closes further votes instead of reapplying effects",failures)
		_check(Public.trade_rule(s,"city-1","city-0","food",3).allowed,"Announced policy does not apply before its recorded effective month",failures)
		_check(not Public.trade_rule(s,"city-1","city-0","food",4).allowed,"Approved jurisdiction/product embargo restricts actual trade when effective",failures)
		_check(Public.trade_rule(s,"city-1","city-0","tools",4).allowed,"Targeted product embargo does not stop unrelated products",failures)
	_check(_cash_sum(s)==total_cash,"Politics and policy do not mint funds",failures)
	_check(Public.validate(s).is_empty(),"Public politics/contract state stays valid",failures)
	_check(Ledger.validate(s).is_empty(),"All public activity keeps owner/currency double entry",failures)
	var choices: Array=Public.ai_commands(s)
	for c in choices:
		_check(Public.validate_command(s,c).is_empty(),"AI public choices use the same validator",failures)
	_test_remote_and_default(failures)
	print("Public sector checks: "+str(_checks)+"; failures: "+str(failures.size()))
	return failures

static func _test_remote_and_default(failures: Array) -> void:
	var s: Dictionary=_world()
	var g: Dictionary=_find(s.public.governments,"gov-nation-nation-1")
	var actor: String=_find(s.public.offices,g.office_id).person_id
	var opened: Dictionary=_apply(s,actor,g.id,"public_announce_procurement",{"government_id":g.id,"product_id":"food","quantity":6,"max_unit_price":30,"deadline_month":4,"military":true},failures)
	if not opened.get("ok",false): return
	var contract: Dictionary=_find(s.public.procurements,opened.contract_id)
	_apply(s,"p-0","co-0","public_bid",{"contract_id":contract.id,"unit_price":14,"delivery_month":4},failures)
	_apply(s,actor,g.id,"public_award",{"government_id":g.id,"contract_id":contract.id,"bid_id":s.public.bids[-1].id,"advance_bps":2000},failures)
	var before_food: int=_quantity(s,"","food")
	var sent: Dictionary=_apply(s,"p-0","co-0","public_deliver",{"contract_id":contract.id,"quantity":6},failures)
	if not sent.get("ok",false): return
	var delivery: Dictionary=_find(s.public.deliveries,sent.delivery_id)
	var lot: Dictionary=_find(s.inventory,delivery.lot_id)
	_check(delivery.status=="transit" and int(delivery.arrive_month)==3,"Foreign public deliveries use finite cross-city/customs lead time",failures)
	_check(lot.accounting_city_id=="city-0" and lot.owner_id=="co-0","In-transit economic attribution follows actual supplier title",failures)
	_check(_quantity(s,"","food")==before_food,"Foreign shipment neither duplicates nor erases goods",failures)
	var premature: Dictionary=Public.apply(s,_command(s,actor,g.id,"public_inspect",{"government_id":g.id,"delivery_id":delivery.id}),1)
	_check(not premature.ok,"Goods in transit cannot be inspected or sold",failures)
	Public._arrive_deliveries(s,2)
	_check(delivery.status=="transit","Opening an earlier month does not teleport a public shipment",failures)
	Public._arrive_deliveries(s,3)
	_check(delivery.status=="awaiting_inspection" and lot.city_id==g.city_id and int(lot.reserved)==6,"Arrival relocates the same reserved supplier-owned lot",failures)
	s.planning_month=3
	_apply(s,actor,g.id,"public_inspect",{"government_id":g.id,"delivery_id":delivery.id},failures)
	_apply(s,actor,g.id,"public_pay",{"government_id":g.id,"contract_id":contract.id},failures)
	_check(_quantity(s,"","food")==before_food,"Foreign acceptance moves ownership exactly once",failures)
	_check(Public.validate(s).is_empty(),"Remote contract, city attribution and public reservations reconcile",failures)
	_stock_invariants(s,failures)
	# A supplier's liquidation preserves the government's actual advance claim.
	var estate: Dictionary=_world()
	g=_find(estate.public.governments,"gov-nation-nation-0")
	actor=_find(estate.public.offices,g.office_id).person_id
	opened=_apply(estate,actor,g.id,"public_announce_procurement",{"government_id":g.id,"product_id":"food","quantity":6,"max_unit_price":30,"deadline_month":3,"military":true},failures)
	if not opened.get("ok",false): return
	contract=_find(estate.public.procurements,opened.contract_id)
	_apply(estate,"p-0","co-0","public_bid",{"contract_id":contract.id,"unit_price":14,"delivery_month":3},failures)
	_apply(estate,actor,g.id,"public_award",{"government_id":g.id,"contract_id":contract.id,"bid_id":estate.public.bids[-1].id,"advance_bps":2000},failures)
	var cash_before_default: int=_cash_sum(estate)
	_find(estate.companies,"co-0").status="liquidating"
	Public.monthly(estate,2)
	_check(contract.status=="defaulted" and int(contract.advance_remaining)==16,"Liquidation retains unearned advance as an estate debt",failures)
	_check(int(g.reserved)==0 and int(contract.reserved_value)==0,"Default releases only unfulfilled outgoing budget",failures)
	_check(Ledger.balance(estate,"co-0:contract_advances")==-16,"Default does not silently cancel the supplier's liability",failures)
	var settlement: Dictionary=Public.settle_claims(estate,"co-0",5,2)
	_check(int(settlement.paid)==5 and int(contract.advance_remaining)==11,"Estate recovery uses only the available actual cash",failures)
	_check(_cash_sum(estate)==cash_before_default,"Default and estate recovery cannot create a refund or money",failures)
	_check(Public.validate(estate).is_empty() and Ledger.validate(estate).is_empty(),"Default claims and partial estate settlements reconcile",failures)
	_stock_invariants(estate,failures)
