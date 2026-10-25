extends RefCounted
const Market=preload("res://core/market.gd")
const Industry=preload("res://core/industry.gd")
const Domain=preload("res://core/domain.gd")
const Ledger=preload("res://core/ledger.gd")
const Finance=preload("res://core/finance.gd")
const Fixture=preload("res://tests/test_industry.gd")
static var checks:int=0
static func _check(f:Array,condition:bool,message:String)->void:
	checks+=1
	if not condition:f.append(message)
static func _world()->Dictionary:
	var s:Dictionary=Fixture._world()
	s.phase="Planning"
	Market.initialize(s)
	return s
static func _order(owner:String,side:String,quantity:int,price:int,id:String,city:String="c-a",product:String="tools")->Dictionary:
	var c:Dictionary=Fixture._command(owner,"market_order",{"side":side,"product_id":product,"quantity":quantity,"limit_price":price,"city_id":city,"expires_month":12,"latest_delivery_month":12,"specification":"standard"},id)
	return c
static func _money(s:Dictionary)->int:
	var amount:int=0
	for account in s.accounts.values():
		if account.kind=="cash" or (account.kind=="asset" and "market-escrow:" in str(account.get("id",""))):amount+=int(account.balance)
	# Accounts carry their ID in the dictionary key, not inside the record.
	for key in s.accounts:
		if ":market-escrow:" in str(key):amount+=int(s.accounts[key].balance)
	return amount
static func run()->Array:
	checks=0
	var f:Array=[]
	_test_orders(f)
	_test_fair(f)
	_test_logistics(f)
	_test_specs(f)
	_test_expiry_quality_finance(f)
	_test_content_ai(f)
	print("Market checks: %d"%checks)
	return f
static func _test_orders(f:Array)->void:
	var s:Dictionary=_world()
	var money:int=_money(s)
	var cash:int=Ledger.balance(s,"co-b:cash")
	var buy:Dictionary=_order("co-b","buy",10,30,"b1")
	_check(f,Market.validate_command(s,buy)=="","A backed own buy order validates")
	_check(f,Market.apply(s,buy,1).ok,"Buy order opens")
	_check(f,Ledger.balance(s,"co-b:cash")==cash-300,"Confirmed buy escrow cannot be spent by other policies")
	_check(f,_money(s)==money,"Escrow changes ownership of no money")
	_check(f,not Market.apply(s,buy,1).ok,"Identical order command is not applied twice")
	var sell:Dictionary=_order("co-a","sell",6,20,"s1")
	_check(f,Market.apply(s,sell,1).ok,"Seller can reserve own identified stock")
	_check(f,Domain.available_quantity(s,"co-a","tools","c-a")==94,"Reserved offer stock is unavailable to other policies")
	_check(f,Market.validate(s).is_empty(),"Unmatched orders satisfy cash and stock invariants")
	var public_rows:Array=Market.order_book(s)
	_check(f,not public_rows[0].has("escrow_account") and not public_rows[1].has("reservations"),"Public order book omits private cash accounts and lot IDs")
	Market.monthly(s,1)
	_check(f,s.market.fills.size()==1 and int(s.market.fills[0].quantity)==6,"Matched limit order actually fills finite stock")
	_check(f,int(s.market.orders[0].remaining)==4 and s.market.orders[0].status=="open","Partial quantity persists as an order")
	_check(f,Ledger.balance(s,str(s.market.orders[0].escrow_account))==120,"Only outstanding buyer quantity remains escrowed")
	_check(f,Ledger.balance(s,"co-b:cash")==cash-240,"Price improvement refunds the unused limit immediately")
	_check(f,Domain.available_quantity(s,"co-b","tools","c-a")==106,"Real bought stock arrives once")
	_check(f,Domain.available_quantity(s,"co-a","tools","c-a")==94,"Actual sale decreases the seller stock once")
	_check(f,_money(s)==money and Ledger.validate(s).is_empty(),"Trade and escrow preserve cash and double entry")
	var journals:int=s.journal.size()
	Market.monthly(s,1)
	_check(f,s.journal.size()==journals and s.market.fills.size()==1,"Monthly matching is idempotent")
	var cancel:Dictionary=Fixture._command("co-b","market_cancel",{"order_id":s.market.orders[0].id},"cancel")
	_check(f,Market.apply(s,cancel,1).ok,"Buyer can cancel its own unfilled remainder")
	_check(f,Ledger.balance(s,"co-b:cash")==cash-120,"Cancellation returns remaining escrow without revenue")
	_check(f,Market.validate(s).is_empty(),"Filled and cancelled orders keep complete invariants")
	var bad:Dictionary=_order("co-b","buy",10000,1000000,"no-cash")
	_check(f,Market.validate_command(s,bad)=="MARKET_ESCROW_FUNDS_REQUIRED","Unfunded desired order is rejected")
	bad=_order("co-a","sell",1000,1,"no-stock")
	_check(f,Market.validate_command(s,bad)=="MARKET_OWN_STOCK_REQUIRED","Seller cannot offer a rival's or nonexistent stock")
	bad=_order("co-a","sell",1,1,"service","c-a","energy")
	_check(f,Market.validate_command(s,bad)=="MARKET_PHYSICAL_PRODUCT_REQUIRED","Time service capacity is not sold as stored goods")
	var snapshot:String=JSON.stringify(s)
	Market.summary(s,"co-a")
	_check(f,JSON.stringify(s)==snapshot,"Order and fill queries are pure")
static func _fair_world(reverse:bool=false,split:bool=false)->Dictionary:
	var s:Dictionary=_world()
	Market.apply(s,_order("co-a","sell",9,10,"seller"),1)
	var buys:Array=[_order("co-b","buy",10,30,"buyer-b"),_order("co-d","buy",10,30,"buyer-d")]
	if reverse:buys.reverse()
	if split:
		buys=[_order("co-d","buy",10,30,"buyer-d")]
		for i in range(10):buys.append(_order("co-b","buy",1,30,"split-b-"+str(i)))
	for c in buys:Market.apply(s,c,1)
	Market.monthly(s,1)
	return s
static func _owned_fills(s:Dictionary,owner:String)->int:
	var amount:int=0
	for fill in s.market.fills:
		if fill.buyer_id==owner:amount+=int(fill.quantity)
	return amount
static func _test_fair(f:Array)->void:
	var first:Dictionary=_fair_world()
	var reversed:Dictionary=_fair_world(true)
	var split:Dictionary=_fair_world(false,true)
	_check(f,absi(_owned_fills(first,"co-b")-_owned_fills(first,"co-d"))<=1,"Equal buyers share finite supply proportionally")
	_check(f,_owned_fills(first,"co-b")==_owned_fills(reversed,"co-b"),"UI submission order grants no scarce-supply priority")
	_check(f,_owned_fills(first,"co-b")==_owned_fills(split,"co-b"),"Splitting orders cannot gain residual priority")
	_check(f,Market.validate(first).is_empty() and Market.validate(split).is_empty(),"Fair partial allocation preserves every reservation")
	var s:Dictionary=_world()
	Market.apply(s,_order("co-a","sell",10,40,"expensive"),1)
	Market.apply(s,_order("co-d","sell",10,20,"cheap"),1)
	Market.apply(s,_order("co-b","buy",10,30,"buyer"),1)
	Market.monthly(s,1)
	_check(f,s.market.fills.size()==1 and s.market.fills[0].seller_id=="co-d","Compatible lowest ask wins without violating buyer limit")
	_check(f,s.market.orders[0].remaining==10,"Unaccepted overpriced offer remains reserved, not revenue")
	s.planning_month=13
	Market.monthly(s,13)
	_check(f,s.market.orders[0].status=="expired" and Domain.available_quantity(s,"co-a","tools","c-a")==100,"Expiry releases original seller stock")
	_check(f,Market.validate(s).is_empty(),"Expired open orders release all resources")
static func _test_logistics(f:Array)->void:
	var s:Dictionary=_world()
	var original:int=Fixture._stock(s,"tools")
	Market.apply(s,_order("co-a","sell",5,10,"s"),1)
	Market.apply(s,_order("co-b","buy",5,200,"b","c-b"),1)
	s.phase="Resolving"
	Market.monthly(s,1)
	_check(f,s.market.fills.size()==1 and s.shipments.size()==1,"Remote order uses actual bounded logistics")
	_check(f,Fixture._stock(s,"tools")==original,"Shipment dispatch preserves global quantity")
	_check(f,Domain.available_quantity(s,"co-b","tools","c-b")==0,"In-transit order stock cannot be used at destination")
	_check(f,int(s.market.fills[0].delivered_cost)>50,"Delivered limit includes paid freight")
	var shipment:Dictionary=s.shipments[0]
	_check(f,shipment.route_ids.size()>0,"Persistent shipment records actual route constraints")
	for route in s.industry.routes:route.blocked=true
	Industry.receive_due_shipments(s,int(shipment.eta))
	_check(f,shipment.status=="transit" and Fixture._stock(s,"tools")==original,"Blocked delivery preserves the physical cargo")
	for route in s.industry.routes:route.blocked=false
	Industry.receive_due_shipments(s,int(shipment.eta))
	_check(f,shipment.status=="delivered" and Domain.available_quantity(s,"co-b","tools","c-b")==5,"Unblocked cargo arrives once with original owner")
	_check(f,Fixture._stock(s,"tools")==original,"Actual arrival neither duplicates nor loses stock")
	var blocked:Dictionary=_world()
	for route in blocked.industry.routes:route.blocked=true
	Market.apply(blocked,_order("co-a","sell",5,10,"s"),1)
	Market.apply(blocked,_order("co-b","buy",5,200,"b","c-b"),1)
	Market.monthly(blocked,1)
	_check(f,blocked.market.fills.is_empty() and blocked.market.orders[0].remaining==5,"No inaccessible external market absorbs unsellable goods")
	_check(f,Market.validate(blocked).is_empty(),"Blocked contract retains money and goods reservations")
static func _test_specs(f:Array)->void:
	var s:Dictionary=_world()
	var buyer:Dictionary=_order("co-b","buy",2,30,"b")
	buyer.payload.specification="incompatible-spec"
	Market.apply(s,buyer,1)
	Market.apply(s,_order("co-a","sell",2,10,"s"),1)
	Market.monthly(s,1)
	_check(f,s.market.fills.is_empty(),"Buyer-required specification rejects an incompatible offer")
	var unauthorized:Dictionary=Fixture._command("co-b","market_cancel",{"order_id":s.market.orders[1].id},"steal-cancel")
	_check(f,Market.validate_command(s,unauthorized)=="MARKET_ORDER_AUTHORITY","Competitor cannot cancel another actor's contract")
	var deadlines:Dictionary=_world()
	var late:Dictionary=_order("co-b","buy",2,200,"late","c-b")
	late.payload.latest_delivery_month=1
	Market.apply(deadlines,late,1)
	Market.apply(deadlines,_order("co-a","sell",2,10,"source"),1)
	Market.monthly(deadlines,1)
	_check(f,deadlines.market.fills.is_empty(),"Infeasible requested delivery deadline blocks matching")

static func _test_expiry_quality_finance(f:Array)->void:
	var s:Dictionary=_world()
	Domain.change_inventory(s,"co-a","c-a","vegetables",3,"opening",0,"opening")
	var sell:Dictionary=_order("co-a","sell",3,10,"perishable","c-a","vegetables")
	_check(f,Market.apply(s,sell,1).ok,"A fresh perishable offer reserves actual dated stock")
	s.planning_month=5
	Market.before_turn(s,5)
	_check(f,s.market.orders[0].status=="stock_lost","Expiration terminates an unfulfilled perishable offer")
	_check(f,Fixture._stock(s,"vegetables")==0,"Expired offer reservation cannot make spoiled food usable again")
	_check(f,Market.validate(s).is_empty(),"Spoiled offer cleanup releases all backing reservations")
	var quality:Dictionary=_world()
	var demanded:Dictionary=_order("co-b","buy",2,30,"quality-demand")
	demanded.payload.min_quality=80
	Market.apply(quality,demanded,1)
	Market.apply(quality,_order("co-a","sell",2,10,"ordinary-supply"),1)
	Market.monthly(quality,1)
	_check(f,quality.market.fills.is_empty(),"Actual offered quality must satisfy the buyer's explicit specification")
	var financed:Dictionary=_world()
	financed.nations=[{"id":"n-a","name":"A"},{"id":"n-b","name":"B"}]
	Finance.initialize(financed)
	var deposit_id:String="bank-n-a:deposit:co-b:USD"
	var initial_deposit:int=Ledger.balance(financed,deposit_id)
	Market.apply(financed,_order("co-b","buy",10,30,"financed-buy"),1)
	Finance.reconcile_deposits(financed,1)
	_check(f,Ledger.balance(financed,deposit_id)==initial_deposit,"Escrow remains a restricted bank deposit, preserving the bank counterpart")
	_check(f,int(Finance.summary(financed,"co-b").restricted_deposits)==300,"Restricted deposits are visible separately from spendable cash")
	var stock_account:String=Finance.inventory_account(financed,"co-a","tools")
	var stock_before:int=Ledger.balance(financed,stock_account)
	Market.apply(financed,_order("co-a","sell",4,20,"financed-sell"),1)
	Market.monthly(financed,1)
	Finance.reconcile_deposits(financed,1)
	_check(f,financed.market.fills.size()==1,"Accrual finance and actual market stock settle together")
	_check(f,Ledger.validate(financed).is_empty() and Market.validate(financed).is_empty(),"Escrow, revenue, inventory cost and actual cash preserve double entry")
	_check(f,Ledger.balance(financed,stock_account)==stock_before-64,"Real stock cost is released only once on sale")

static func _test_content_ai(f:Array)->void:
	var data:Dictionary=Market._data().duplicate(true)
	_check(f,Market.validate_data(data).is_empty(),"Market versioned experimental data has valid units and bounded tuning")
	data.units.erase("limit_price")
	_check(f,not Market.validate_data(data).is_empty(),"Missing price unit explicitly fails market content validation")
	data=Market._data().duplicate(true)
	data.parameters.ai_order_period=0
	_check(f,not Market.validate_data(data).is_empty(),"Zero AI time period is rejected before division")
	data=Market._data().duplicate(true)
	data.version="unknown"
	_check(f,not Market.validate_data(data).is_empty(),"Unrecognized market data version is not silently accepted")
	var s:Dictionary=_world()
	s.planning_month=6
	s.recipes.append({"id":"test-market-need","inputs":{"tools":2},"outputs":{"medicine":1},"labor":1})
	s.recipes.append({"id":"test-market-output","inputs":{"wood":1},"outputs":{"tools":1},"labor":1})
	Domain.find(s.facilities,"f-1").recipe_id="test-market-need"
	Domain.find(s.facilities,"f-0").recipe_id="test-market-output"
	Domain.change_inventory(s,"co-b","c-a","tools",-100,"fixture actual use",5)
	var offer:Dictionary=_order("co-a","sell",10,16,"public-ask")
	Market.apply(s,offer,6)
	var snapshot:String=JSON.stringify(s)
	var commands:Array=Market.ai_commands(s)
	_check(f,commands.size()>0 and JSON.stringify(s)==snapshot,"AI plans bounded real limit orders without mutating the world")
	var buyer_found:bool=false
	for command in commands:
		_check(f,Market.validate_command(s,command).is_empty(),"AI limit orders pass the exact player command validator")
		if command.acting_company_id=="co-b" and command.payload.side=="buy":buyer_found=true
	_check(f,buyer_found,"Corporate AI can buy from the public order book for its actual input deficit")
	var invalid:Dictionary=_world()
	invalid.market.orders.append({"id":"broken"})
	_check(f,not Market.validate(invalid).is_empty(),"Malformed saved order fields fail explicitly without runtime dereference")
