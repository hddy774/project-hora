extends RefCounted
const Land=preload("res://core/land.gd")
const Finance=preload("res://core/finance.gd")
const Ledger=preload("res://core/ledger.gd")
static var _serial: int=0
static var _checks: int=0

static func _world() -> Dictionary:
	var s: Dictionary={"schema_version":2,"version":0,"planning_month":1,"phase":"Planning","accounts":{},"journal":[],"events":[],"nations":[{"id":"nation-0","name":"국가"}],"cities":[{"id":"city-0","name":"도시","nation_id":"nation-0","width":12,"height":12,"roads":[]}],"companies":[],"people":[],"facilities":[],"inventory":[],"products":[{"id":"food","price":10}],"public":{"governments":[{"id":"gov-city-city-0","city_id":"city-0"},{"id":"gov-nation-nation-0","city_id":"city-0"}],"offices":[{"id":"city-office","government_id":"gov-city-city-0","person_id":"p-2"}]},"commands":[]}
	for x in range(12):
		s.cities[0].roads.append({"x":x,"y":5})
		s.cities[0].roads.append({"x":x,"y":11})
	for i in range(3):
		var owner: String="co-%d"%i
		var person: String="p-%d"%i
		s.people.append({"id":person,"city_id":"city-0","last_income":0})
		s.companies.append({"id":owner,"name":owner,"city_id":"city-0","director_id":person,"shareholder_id":person,"status":"active","policy":{"dividend_percent":0}})
		s.facilities.append({"id":"facility-%d"%i,"city_id":"city-0","company_id":owner,"x":i*2,"y":2,"w":2,"h":2})
		Ledger.opening(s,person,500)
		Ledger.opening(s,owner,10000)
	for owner in ["gov-city-city-0","gov-nation-nation-0"]: Ledger.opening(s,owner,10000)
	Land.initialize(s)
	Finance.initialize(s)
	return s

static func _c(s: Dictionary,type: String,payload: Dictionary,actor: String="p-0",owner: String="co-0") -> Dictionary:
	_serial+=1
	return {"id":"land-test-%d"%_serial,"actor_id":actor,"acting_company_id":owner,"expected_version":s.version,"type":type,"payload":payload}

static func _apply(s: Dictionary,type: String,payload: Dictionary,actor: String="p-0",owner: String="co-0",month: int=1) -> Dictionary:
	return Land.apply(s,_c(s,type,payload,actor,owner),month)

static func _check(value: bool,label: String,failures: Array) -> void:
	_checks+=1
	if not value: failures.append(label)

static func _valid(s: Dictionary,label: String,failures: Array) -> void:
	Finance.reconcile_deposits(s,int(s.planning_month))
	var errors: Array=Land.validate(s)
	_check(errors.is_empty(),label+" land registry "+str(errors),failures)
	errors=Ledger.validate(s)
	_check(errors.is_empty(),label+" ledger "+str(errors),failures)
	errors=Finance.validate(s)
	_check(errors.is_empty(),label+" money counterpart "+str(errors),failures)

static func run() -> Array:
	_checks=0
	var failures: Array=[]
	var s: Dictionary=_world()
	_valid(s,"Opening",failures)
	_check(s.land.titles.size()==144,"Every finite tile has exactly one title",failures)
	_check(s.land.titles["city-0:0:5"]=="gov-city-city-0","Road title belongs to local government",failures)
	_check(not Land.can_build(s,"co-0","city-0",{"x":0,"y":5,"w":1,"h":1}),"Road cannot be sold or occupied",failures)
	_check(Land.can_build(s,"co-0","city-0",{"x":6,"y":6,"w":2,"h":2}),"Tutorial construction plot is a recorded opening title",failures)
	_check(not Land.can_build(s,"co-1","city-0",{"x":6,"y":6,"w":2,"h":2}),"A rival cannot build on an owned plot",failures)
	var offer: Dictionary=s.land.offers[0].duplicate(true)
	var candidate: Dictionary=_c(s,"land_buy",{"offer_id":offer.id})
	var rival: Dictionary=_c(s,"land_buy",{"offer_id":offer.id},"p-1","co-1")
	var before: String=JSON.stringify(s,"",true,true)
	_check(Land.validate_command(s,candidate)=="" and Land.validate_command(s,rival)=="","Competitive bids share the same valid public offer",failures)
	_check(JSON.stringify(s,"",true,true)==before,"Planning does not preempt public land or debit either bidder",failures)
	var buyer_cash: int=Finance.cash(s,"co-0")
	var seller_cash: int=Finance.cash(s,offer.owner_id)
	var old_asset: int=Ledger.balance(s,str(offer.owner_id)+":land:city-0")
	var result: Dictionary=Land.apply(s,candidate,1)
	_check(result.ok,"Funded bid acquires an actual offered plot "+str(result),failures)
	_check(Finance.cash(s,"co-0")==buyer_cash-int(offer.price) and Finance.cash(s,offer.owner_id)==seller_cash+int(offer.price),"Sale cash moves once between the actual buyer and seller",failures)
	_check(Ledger.balance(s,"co-0:land:city-0")>0 and Ledger.balance(s,str(offer.owner_id)+":land:city-0")<old_asset,"Sale transfers book asset and separately records gain",failures)
	_check(Land.can_build(s,"co-0","city-0",offer.rect),"Buyer receives possession",failures)
	before=JSON.stringify(s,"",true,true)
	_check(not Land.apply(s,rival,1).ok and JSON.stringify(s,"",true,true)==before,"Competing bid loses without cash loss or title duplication",failures)
	_valid(s,"Title sale",failures)
	var terms: Dictionary=offer.rect.duplicate()
	terms.merge({"city_id":"city-0","price":12,"term_months":12,"duration_months":8})
	_check(_apply(s,"land_offer_lease",terms).ok,"Title owner may consent to a priced lease",failures)
	var lease_offer: Dictionary=s.land.offers[-1]
	var tenant_cash: int=Finance.cash(s,"co-1")
	_check(_apply(s,"land_rent",{"offer_id":lease_offer.id},"p-1","co-1").ok,"Tenant explicitly accepts the lease and first rent",failures)
	var lease: Dictionary=s.land.leases[0]
	_check(Finance.cash(s,"co-1")==tenant_cash-12 and Land.debt_total(s,"co-1")==0,"First rent is funded, not free tenancy or new money",failures)
	_check(Land.can_build(s,"co-1","city-0",offer.rect) and not Land.can_build(s,"co-0","city-0",offer.rect),"Exclusive tenant possession also constrains the landlord",failures)
	_check(not _apply(s,"land_offer_lease",terms).ok,"An active possession cannot be leased twice",failures)
	var use_terms: Dictionary=terms.duplicate()
	use_terms["use"]="industry"
	_check(not _apply(s,"land_change_use",use_terms).ok,"Landlord cannot override an existing tenant's contracted use",failures)
	# Future rent remains a real claim during insolvency.
	Ledger.transfer(s,"land-real-business-loss","co-1","co-2",Finance.cash(s,"co-1"),"실제 사업 지출",1)
	s.planning_month=2
	Land.before_turn(s,2)
	_check(Land.debt_total(s,"co-1")==12 and Finance.cash(s,"co-1")==0,"Unfunded rent accrues debt without minting payment",failures)
	var old_claim: Dictionary=s.land.claims[-1]
	_check(Ledger.balance(s,"co-0:rent_receivable:"+str(old_claim.id))==12,"Landlord has a corresponding unpaid receivable",failures)
	# Buying an occupied title preserves the lease and past creditor.
	terms.price=100
	_check(_apply(s,"land_offer_sale",terms,"p-0","co-0",2).ok,"Occupied title can be offered while preserving the entire lease",failures)
	var sale_offer: Dictionary=s.land.offers[-1]
	_check(_apply(s,"land_buy",{"offer_id":sale_offer.id},"p-2","co-2",2).ok,"Third party can buy the occupied title",failures)
	lease=s.land.leases[0]
	_check(lease.landlord_id=="co-2" and lease.tenant_id=="co-1","Title sale redirects future rent and preserves tenant",failures)
	_check(s.land.claims[-1].landlord_id=="co-0","An existing receivable stays with the original creditor",failures)
	_check(Land.can_build(s,"co-1","city-0",offer.rect) and not Land.can_build(s,"co-2","city-0",offer.rect),"Buyer cannot displace a valid lease by changing title",failures)
	_valid(s,"Lease and title transfer",failures)
	Land.monthly(s,2)
	_check(Land.debt_total(s,"co-1")==12,"Repeated month processing cannot accrue rent twice",failures)
	s.planning_month=3
	Land.monthly(s,3)
	_check(s.land.claims[-1].landlord_id=="co-2" and Land.debt_total(s,"co-1")==24,"Next rent is owed to the new title owner",failures)
	s.planning_month=4
	Land.monthly(s,4)
	_check(s.land.leases[0].status=="default" and Land.debt_total(s,"co-1")==36,"Repeated arrears revoke use rights while preserving all claims",failures)
	_check(not Land.can_operate(s,"co-1","city-0",offer.rect),"Defaulted tenant cannot operate without renewed rights",failures)
	_check(s.facilities.size()==3,"Lease default never deletes physical corporate assets",failures)
	_valid(s,"Rental default",failures)
	Ledger.transfer(s,"land-actual-recovery","co-2","co-1",40,"실제 채무 상환 재원",4)
	_check(Land.settle_debt(s,"co-1",40,4)==36 and Land.debt_total(s,"co-1")==0,"Finite recovered cash pays original and successor creditors",failures)
	_check(s.land.leases[0].status=="default","Paying old debt does not invent a new lease",failures)
	_valid(s,"Rental debt recovery",failures)
	_test_use_and_package(failures)
	# Each action is available through a real form, with own-cash reservation.
	var actions: Dictionary={}
	for action in Land.ui_actions(): actions[action.type]=true
	for type in Land.command_types(): _check(actions.has(type),"Land UI command exists: "+type,failures)
	var reservations: Array=Land.planning_reservations(s,_c(s,"land_buy",{"offer_id":offer.id}))
	_check(reservations.size()==1 and reservations[0].owner_id=="co-0" and reservations[0].quantity==offer.price,"Planning reserves only bidder cash, never foreign title",failures)
	before=JSON.stringify(s,"",true,true)
	var forbidden: Dictionary=terms.duplicate()
	forbidden.x=0; forbidden.y=5; forbidden.w=1; forbidden.h=1
	_check(not _apply(s,"land_offer_sale",forbidden,"p-2","gov-city-city-0",4).ok,"Public office cannot sell reserved road tiles",failures)
	_check(JSON.stringify(s,"",true,true)==before,"Rejected public sale has no ledger or title side effects",failures)
	print("Land assertions: %d"%_checks)
	return failures

static func _test_use_and_package(failures: Array) -> void:
	var s: Dictionary=_world()
	var parcels: Array=Land.estate_assets(s,"co-0")
	_check(parcels.size()==1 and int(parcels[0].rect.w)*int(parcels[0].rect.h)==4,"Estate lists the vacant tutorial parcel without duplicating facility footprint",failures)
	var use: Dictionary={"city_id":"city-0","x":6,"y":6,"w":2,"h":2,"use":"agriculture"}
	_check(_apply(s,"land_change_use",use).ok,"Vacant owned title can change its permitted use",failures)
	var rect: Dictionary={"x":6,"y":6,"w":2,"h":2,"kind":"bakery"}
	_check(not Land.can_build(s,"co-0","city-0",rect),"A factory cannot bypass agriculture-only land rights",failures)
	rect.kind="farm"
	_check(Land.can_build(s,"co-0","city-0",rect),"An agricultural facility fits the actual recorded use",failures)
	var facility: Dictionary=s.facilities[0]
	var book: int=Ledger.balance(s,"co-0:facility:facility-0")
	var cash_before: int=Finance.cash(s,"co-1")
	Ledger.open_account(s,"co-1:facility:facility-0","co-1","asset")
	Ledger.open_account(s,"co-0:package_gain","co-0","revenue")
	var rows: Array=[{"account":"co-1:cash","debit":0,"credit":200},{"account":"co-1:facility:facility-0","debit":200,"credit":0},{"account":"co-0:cash","debit":200,"credit":0},{"account":"co-0:facility:facility-0","debit":0,"credit":book},{"account":"co-0:package_gain","debit":0,"credit":200-book}]
	_check(Ledger.post(s,"paid-facility-package",rows,"실제 시설 패키지 취득",1).ok,"Facility package has an actual funded purchase",failures)
	var titles_before: int=Ledger.balance(s,"co-1:land:city-0")
	var result: Dictionary=Land.convey_facility_titles(s,"co-0","co-1",facility,200,1,"package-test")
	_check(result.ok and result.title_transferred,"Paid facility package conveys owned footprint titles",failures)
	_check(Finance.cash(s,"co-1")==cash_before-200,"Package title reclassification cannot charge consideration twice",failures)
	_check(Ledger.balance(s,"co-1:facility:facility-0")+Ledger.balance(s,"co-1:land:city-0")-titles_before==200,"Building plus land equals the actual package acquisition price",failures)
	_check(s.land.titles["city-0:0:2"]=="co-1","Physical footprint follows the purchased title",failures)
	_valid(s,"Facility package land allocation",failures)
	var before: String=JSON.stringify(s,"",true,true)
	Land.ai_commands(s)
	_check(JSON.stringify(s,"",true,true)==before,"Land AI planning cannot preempt titles or debit buyers",failures)
