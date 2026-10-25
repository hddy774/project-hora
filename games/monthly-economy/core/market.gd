extends RefCounted
## Finite call market. Each active offer is backed by its owner's resources.
const Domain = preload("res://core/domain.gd")
const Ledger = preload("res://core/ledger.gd")
const Industry = preload("res://core/industry.gd")

static var _cache: Dictionary = {}

static func validate_data(data: Dictionary) -> Array:
	var errors: Array = []
	for key in ["status", "version", "parameters", "units", "decisions"]:
		if not data.has(key): errors.append("MARKET_CONTENT_FIELD:" + key)
	if not errors.is_empty(): return errors
	if data.status != "experimental" or data.version != "0.2.0": errors.append("MARKET_CONTENT_VERSION")
	if not data.parameters is Dictionary: errors.append("MARKET_CONTENT_PARAMETERS")
	if not data.units is Dictionary: errors.append("MARKET_CONTENT_UNITS")
	if not data.decisions is Array or data.decisions.is_empty(): errors.append("MARKET_CONTENT_DECISIONS")
	if not errors.is_empty(): return errors
	for key in ["max_order_quantity", "max_unit_price", "max_open_orders_per_owner", "default_order_months", "max_delivery_months", "max_quality_requirement", "ai_order_period", "ai_order_months", "ai_cash_reserve", "ai_stock_months", "ai_surplus_percent", "ai_price_ceiling_percent"]:
		if not _whole(data.parameters.get(key)) or int(data.parameters.get(key, 0)) <= 0: errors.append("MARKET_CONTENT_PARAMETER:" + key)
	for key in ["quantity", "limit_price", "time", "quality"]:
		if not data.units.get(key) is String or str(data.units.get(key, "")).is_empty(): errors.append("MARKET_CONTENT_UNIT:" + key)
	if int(data.parameters.get("ai_surplus_percent", 0)) > 100 or int(data.parameters.get("default_order_months", 0)) > int(data.parameters.get("max_delivery_months", 0)): errors.append("MARKET_CONTENT_RANGE")
	return errors

static func _data() -> Dictionary:
	if not _cache.is_empty(): return _cache
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string("res://data/market.json"))
	if not parsed is Dictionary:
		push_error("MARKET_CONTENT_MISSING_OR_INVALID")
		return {}
	var errors: Array = validate_data(parsed)
	if not errors.is_empty():
		push_error("MARKET_CONTENT_INVALID: " + str(errors))
		return {}
	_cache = parsed
	return _cache

static func content_hash() -> String:
	return FileAccess.get_file_as_string("res://data/market.json").sha256_text()

static func initialize(s: Dictionary) -> void:
	if s.has("market"): return
	if _data().is_empty(): return
	s.market = {"orders": [], "fills": [], "applied_commands": {}, "resolved_month": -1, "serial": 0}

static func command_types() -> Array:
	return ["market_order", "market_cancel"]

static func ui_actions() -> Array:
	if _data().is_empty(): return []
	var p: Dictionary = _data().parameters
	return [
		{"type":"market_order","label":"지정가 주문","scope":"both","fields":[{"key":"side","label":"매입·매도","kind":"choice","source":"market_sides","default":"buy"},{"key":"product_id","label":"실물 품목","kind":"choice","source":"products","filter":"physical"},{"key":"quantity","label":"주문 수량","kind":"int","min":1,"max":int(p.max_order_quantity),"default":1},{"key":"limit_price","label":"최대 도착단가 / 매도단가","kind":"int","min":1,"max":int(p.max_unit_price),"default":20},{"key":"city_id","label":"납품 / 출고 도시","kind":"choice","source":"cities"},{"key":"specification","label":"제품 규격","kind":"text","default":"standard"},{"key":"min_quality","label":"최소 품질 규격","kind":"int","min":0,"max":int(p.max_quality_requirement),"default":0},{"key":"expires_month","label":"주문 만료 월 번호","kind":"int","min":1,"max":100000,"default":12},{"key":"latest_delivery_month","label":"최종 인도 월 번호","kind":"int","min":1,"max":100000,"default":12}]},
		{"type":"market_cancel","label":"미체결 주문 철회","scope":"both","fields":[{"key":"order_id","label":"내 주문","kind":"choice","source":"own_orders","filter":"own"}]}
	]

static func _id(s: Dictionary, prefix: String) -> String:
	var value: int = int(s.market.serial)
	s.market.serial = value + 1
	return prefix + "-" + str(value)

static func _whole(value: Variant) -> bool:
	return typeof(value) in [TYPE_INT, TYPE_FLOAT] and is_finite(float(value)) and float(value) == floor(float(value))

static func _owner(s: Dictionary, c: Dictionary) -> String:
	var actor: String = str(c.get("actor_id", ""))
	if Domain.find(s.get("people", []), actor).is_empty(): return ""
	var company: String = str(c.get("acting_company_id", ""))
	if company.is_empty(): return actor
	var co: Dictionary = Domain.find(s.get("companies", []), company)
	if co.is_empty() or co.get("status", "active") != "active": return ""
	if str(co.get("director_id", "")) == actor: return company
	if ResourceLoader.exists("res://core/lifecycle.gd"):
		var life: Variant = load("res://core/lifecycle.gd")
		if life != null and life.has_method("can_act") and life.can_act(s, actor, company, str(c.get("type", "")), c.get("payload", {})): return company
	return ""

static func _cash(s: Dictionary, owner: String) -> int:
	return Ledger.balance(s, owner + ":cash")

static func _lots(s: Dictionary, owner: String, city: String, product: String, specification: String) -> Array:
	var out: Array = []
	for lot in s.get("inventory", []):
		if lot.get("owner_id") == owner and lot.get("city_id") == city and lot.get("product_id") == product and lot.get("location") != "shipment" and str(lot.get("variant_id", "standard")) == specification and int(lot.get("quantity", 0)) > int(lot.get("reserved", 0)) and int(lot.get("expires_month", 2147483647)) > int(s.get("planning_month", 1)): out.append(lot)
	out.sort_custom(func(a,b): return int(a.get("expires_month",2147483647)) < int(b.get("expires_month",2147483647)) if int(a.get("expires_month",2147483647)) != int(b.get("expires_month",2147483647)) else str(a.id) < str(b.id))
	return out

static func validate_command(s: Dictionary, c: Dictionary) -> String:
	if _data().is_empty(): return "MARKET_CONTENT_INVALID"
	if not s.get("market") is Dictionary: return "MARKET_UNINITIALIZED"
	if str(c.get("type", "")) not in command_types(): return "MARKET_COMMAND_UNKNOWN"
	var owner: String = _owner(s, c)
	if owner.is_empty(): return "MARKET_AUTHORITY_REQUIRED"
	if s.market.applied_commands.has(str(c.get("id", ""))): return "MARKET_DUPLICATE_COMMAND"
	if str(c.get("id", "")).is_empty(): return "MARKET_COMMAND_ID_REQUIRED"
	var p: Dictionary = c.get("payload", {})
	if c.type == "market_cancel":
		var order: Dictionary = Domain.find(s.market.orders, str(p.get("order_id", "")))
		if order.is_empty() or order.owner_id != owner or order.status != "open": return "MARKET_ORDER_AUTHORITY"
		return ""
	var params: Dictionary = _data().parameters
	if p.get("side") not in ["buy", "sell"]: return "MARKET_SIDE_INVALID"
	var product: Dictionary = Domain.find(s.get("products", []), str(p.get("product_id", "")))
	if product.is_empty() or product.get("kind", "material") in ["service", "digital"]: return "MARKET_PHYSICAL_PRODUCT_REQUIRED"
	if not _whole(p.get("quantity")) or int(p.quantity) <= 0 or int(p.quantity) > int(params.max_order_quantity): return "MARKET_QUANTITY_INVALID"
	if not _whole(p.get("limit_price")) or int(p.limit_price) <= 0 or int(p.limit_price) > int(params.max_unit_price): return "MARKET_PRICE_INVALID"
	if Domain.find(s.get("cities", []), str(p.get("city_id", ""))).is_empty(): return "MARKET_CITY_INVALID"
	var month: int = int(s.get("planning_month", 1))
	for key in ["expires_month", "latest_delivery_month"]:
		if not _whole(p.get(key, month + int(params.default_order_months))) or int(p.get(key, month + int(params.default_order_months))) < month or int(p.get(key, month)) > month + int(params.max_delivery_months): return "MARKET_DATE_INVALID"
	if str(p.get("specification", "standard")).strip_edges().is_empty(): return "MARKET_SPECIFICATION_REQUIRED"
	if not _whole(p.get("min_quality",0)) or int(p.get("min_quality",0))<0 or int(p.get("min_quality",0))>int(params.max_quality_requirement): return "MARKET_QUALITY_INVALID"
	var count: int = 0
	for order in s.market.orders:
		if order.owner_id == owner and order.status == "open": count += 1
	if count >= int(params.max_open_orders_per_owner): return "MARKET_ORDER_LIMIT"
	if p.side == "buy":
		if _cash(s, owner) < int(p.quantity) * int(p.limit_price): return "MARKET_ESCROW_FUNDS_REQUIRED"
	else:
		var held: int = 0
		for lot in _lots(s, owner, str(p.city_id), str(p.product_id), str(p.get("specification", "standard"))):
			if int(lot.get("quality",Industry.parameters().quality_base)) >= int(p.get("min_quality",0)): held += int(lot.quantity) - int(lot.get("reserved", 0))
		if held < int(p.quantity): return "MARKET_OWN_STOCK_REQUIRED"
	return ""

static func planning_reservations(s: Dictionary, c: Dictionary) -> Array:
	var p: Dictionary = c.get("payload", {})
	var owner: String = _owner(s, c)
	if owner.is_empty() or c.get("type") != "market_order": return []
	if p.get("side") == "buy": return [{"owner_id":owner,"resource_type":"cash","quantity":int(p.get("quantity",0))*int(p.get("limit_price",0))}]
	return [{"owner_id":owner,"resource_type":"inventory","resource_id":str(p.get("product_id","")),"city_id":str(p.get("city_id","")),"quantity":int(p.get("quantity",0))}]

static func _escrow(s: Dictionary, order: Dictionary, amount: int, month: int, release: bool) -> bool:
	if amount <= 0: return true
	var cash_account: String = str(order.owner_id) + ":cash"
	var hold_account: String = str(order.escrow_account)
	var result: Dictionary = Ledger.post(s, "market-cash-%s-%d" % [order.id,s.journal.size()], [
		{"account":cash_account,"debit":amount if release else 0,"credit":0 if release else amount},
		{"account":hold_account,"debit":0 if release else amount,"credit":amount if release else 0}
	], "미체결 주문의 자기자금 반환" if release else "확정 매입 주문의 자기자금 예치", month)
	return bool(result.get("ok",false))

static func apply(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var error: String = validate_command(s,c)
	if not error.is_empty(): return {"ok":false,"error":error}
	var p: Dictionary = c.payload
	var owner: String = _owner(s,c)
	if c.type == "market_cancel":
		_close(s, Domain.find(s.market.orders,str(p.order_id)), "cancelled",month)
	else:
		var order_id: String = _id(s,"order")
		var order: Dictionary = {"id":order_id,"owner_id":owner,"actor_id":c.actor_id,"side":p.side,"product_id":p.product_id,"city_id":p.city_id,"quantity":int(p.quantity),"remaining":int(p.quantity),"filled":0,"limit_price":int(p.limit_price),"specification":str(p.get("specification","standard")),"created_month":month,"expires_month":int(p.get("expires_month",month+int(_data().parameters.default_order_months))),"latest_delivery_month":int(p.get("latest_delivery_month",month+int(_data().parameters.default_order_months))),"status":"open","reservations":[],"escrow_account":"","min_quality":int(p.get("min_quality",0)),"offered_quality":int(_data().parameters.max_quality_requirement)}
		if p.side == "buy":
			order.escrow_account=owner+":market-escrow:"+order_id
			Ledger.open_account(s,order.escrow_account,owner,"asset")
			if not _escrow(s,order,int(p.quantity)*int(p.limit_price),month,false): return {"ok":false,"error":"MARKET_ESCROW_FAILED"}
		else:
			var remaining: int = int(p.quantity)
			for lot in _lots(s,owner,str(p.city_id),str(p.product_id),str(order.specification)):
				if int(lot.get("quality",Industry.parameters().quality_base)) < int(order.min_quality): continue
				order.offered_quality = mini(int(order.offered_quality),int(lot.get("quality",Industry.parameters().quality_base)))
				var q: int = mini(remaining,int(lot.quantity)-int(lot.get("reserved",0)))
				lot.reserved=int(lot.get("reserved",0))+q
				order.reservations.append({"lot_id":lot.id,"quantity":q})
				remaining-=q
				if remaining==0: break
		s.market.orders.append(order)
		Domain.event(s,"market_order","자기 자금·재고로 뒷받침한 유한 지정가 주문",month,{"actor_id":owner,"city_id":order.city_id,"product_id":order.product_id,"quantity":order.quantity,"order_id":order.id,"side":order.side})
	s.market.applied_commands[str(c.id)]=month
	return {"ok":true,"error":""}

static func _close(s: Dictionary, order: Dictionary, status: String, month: int) -> void:
	if order.is_empty() or order.status != "open": return
	if order.side == "buy": _escrow(s,order,Ledger.balance(s,str(order.escrow_account)),month,true)
	else:
		for reservation in order.reservations:
			var lot: Dictionary = Domain.find(s.inventory,str(reservation.lot_id))
			if not lot.is_empty(): lot.reserved=maxi(0,int(lot.get("reserved",0))-int(reservation.quantity))
		order.reservations=[]
	order.status=status
	Domain.event(s,"market_order_closed","미체결 주문 예약 해제: "+status,month,{"actor_id":order.owner_id,"order_id":order.id,"city_id":order.city_id,"quantity":order.remaining})

static func _key(s: Dictionary, text_value: String, month: int) -> String:
	return (str(s.get("seed",0))+"|market|"+str(month)+"|"+text_value).sha256_text()

static func _trade_rule(s: Dictionary, from_city: String, to_city: String, product: String, month: int) -> Dictionary:
	if s.has("public") and ResourceLoader.exists("res://core/public_sector.gd"):
		var module: Variant=load("res://core/public_sector.gd")
		if module != null and module.has_method("trade_rule"): return module.trade_rule(s,from_city,to_city,product,month)
	return {"allowed":true,"tariff_bps":0,"tariff_receiver":"","delay":0,"capacity_percent":100}

static func _quote(s: Dictionary, sell: Dictionary, buy: Dictionary, quantity: int, month: int) -> Dictionary:
	if buy.owner_id==sell.owner_id or buy.product_id!=sell.product_id or buy.specification!=sell.specification or int(buy.get("min_quality",0))>int(sell.get("offered_quality",0)): return {"allowed":false}
	var rule: Dictionary=_trade_rule(s,str(sell.city_id),str(buy.city_id),str(sell.product_id),month)
	if not rule.get("allowed",true): return {"allowed":false}
	var quote: Dictionary=Industry.route_quote(s,str(sell.city_id),str(buy.city_id),str(sell.product_id),quantity,month)
	if not quote.get("allowed",false): return {"allowed":false}
	var q: int=mini(quantity,int(quote.get("quantity",0)))
	q=mini(q,maxi(0,int(quantity*int(rule.get("capacity_percent",100))/100)))
	if q<=0: return {"allowed":false}
	if int(quote.get("quantity",0))!=q:
		quote=Industry.route_quote(s,str(sell.city_id),str(buy.city_id),str(sell.product_id),q,month)
		if not quote.get("allowed",false) or int(quote.get("quantity",0))!=q: return {"allowed":false}
	var tariff: int=int(ceil(float(int(sell.limit_price)*q)*int(rule.get("tariff_bps",0))/10000.0))
	var delivered: int=int(sell.limit_price)*q+int(quote.get("fee",0))+tariff
	if q<=0 or delivered>q*int(buy.limit_price) or int(quote.get("eta",month))+int(rule.get("delay",0))>int(buy.latest_delivery_month): return {"allowed":false}
	return {"allowed":true,"quantity":q,"cost":delivered,"tariff":tariff,"tariff_receiver":str(rule.get("tariff_receiver","")),"delay":int(rule.get("delay",0)),"route":quote}

static func _take_sell(s: Dictionary, order: Dictionary, quantity: int, month: int) -> Dictionary:
	var remaining: int=quantity
	var age: Dictionary={"born_month":month,"expires_month":0,"unit_cost":0}
	for reservation in order.reservations:
		if remaining<=0: break
		var lot: Dictionary=Domain.find(s.inventory,str(reservation.lot_id))
		if lot.is_empty(): continue
		var q: int=mini(remaining,mini(int(reservation.quantity),int(lot.quantity)))
		if q<=0: continue
		age.born_month=mini(int(age.born_month),int(lot.get("born_month",month)))
		var expiry: int=int(lot.get("cold_original_expiry",lot.get("expires_month",0)))
		if expiry>0: age.expires_month=expiry if int(age.expires_month)==0 else mini(expiry,int(age.expires_month))
		lot.quantity=int(lot.quantity)-q
		lot.reserved=maxi(0,int(lot.get("reserved",0))-q)
		reservation.quantity=int(reservation.quantity)-q
		Domain.event(s,"trade_out","판매 출고",month,{"actor_id":order.owner_id,"city_id":order.city_id,"product_id":order.product_id,"quantity":q,"inventory_delta":-q,"lot_id":lot.id,"order_id":order.id})
		remaining-=q
	return age

static func _execute_fill(s: Dictionary, sell: Dictionary, buy: Dictionary, requested: int, month: int) -> int:
	var quote: Dictionary=_quote(s,sell,buy,mini(requested,mini(int(sell.remaining),int(buy.remaining))),month)
	if not quote.get("allowed",false): return 0
	var q: int=int(quote.quantity)
	var cost: int=int(quote.cost)
	if Ledger.balance(s,str(buy.escrow_account))<cost: return 0
	# All checks precede mutation. The public route quote checks capacity, fuel,
	# work and legal access again at this exact settlement point.
	if not _escrow(s,buy,cost,month,true): return 0
	var remote: bool=sell.city_id!=buy.city_id
	if remote and not Industry.reserve_route(s,str(buy.owner_id),str(sell.city_id),str(buy.city_id),str(sell.product_id),q,month,quote.route).get("ok",false):
		_escrow(s,buy,cost,month,false)
		return 0
	var id: String=_id(s,"market-fill")
	var paid: Dictionary={}
	if s.has("finance") and ResourceLoader.exists("res://core/finance.gd"):
		var finance: Variant=load("res://core/finance.gd")
		paid=finance.settle_trade(s,id,str(buy.owner_id),str(sell.owner_id),q*int(sell.limit_price),str(sell.product_id),q,month)
	else: paid=Domain.pay(s,id,str(buy.owner_id),str(sell.owner_id),q*int(sell.limit_price),"지정가 상품 거래",month)
	if not paid.get("ok",false): return 0
	if int(quote.tariff)>0 and not str(quote.tariff_receiver).is_empty(): Domain.pay(s,id+"-tariff",str(buy.owner_id),str(quote.tariff_receiver),int(quote.tariff),"관세 납부",month)
	var age: Dictionary=_take_sell(s,sell,q,month)
	var shipment_id: String=""
	if remote:
		shipment_id="shipment-"+id
		var lot_id: String="lot:"+shipment_id
		var lot: Dictionary={"id":lot_id,"owner_id":buy.owner_id,"city_id":sell.city_id,"accounting_city_id":buy.city_id,"location":"shipment","product_id":sell.product_id,"variant_id":sell.specification,"quantity":q,"reserved":0,"shipment_id":shipment_id,"born_month":int(age.born_month),"unit_cost":int(sell.limit_price),"quality":int(sell.offered_quality)}
		if int(age.expires_month)>0: lot.expires_month=int(age.expires_month)
		s.inventory.append(lot)
		Domain.register_lot(s, lot)
		s.shipments.append({"id":shipment_id,"owner_id":buy.owner_id,"product_id":sell.product_id,"quantity":q,"from_city_id":sell.city_id,"to_city_id":buy.city_id,"start_month":month,"eta":int(quote.route.eta)+int(quote.delay),"status":"transit","lot_id":lot_id,"route_ids":quote.route.get("path",[]),"carrier_id":str(quote.route.get("carrier_id",""))})
		Domain.event(s,"shipment","운송 중 소유권 이전",month,{"actor_id":buy.owner_id,"city_id":buy.city_id,"product_id":sell.product_id,"quantity":q,"inventory_delta":q,"lot_id":lot_id,"shipment_id":shipment_id})
	else:
		var incoming: Dictionary=Domain.change_inventory(s,str(buy.owner_id),str(buy.city_id),str(sell.product_id),q,"매입 입고",month,"trade_in")
		for lot_id in incoming.get("lot_ids",[]):
			var lot: Dictionary=Domain.find(s.inventory,str(lot_id))
			lot.born_month=mini(int(lot.get("born_month",month)),int(age.born_month))
			lot.variant_id=sell.specification
			lot.quality=mini(int(lot.get("quality",sell.offered_quality)),int(sell.offered_quality))
			if int(age.expires_month)>0: lot.expires_month=int(age.expires_month) if int(lot.get("expires_month",0))<=0 else mini(int(lot.expires_month),int(age.expires_month))
	buy.remaining=int(buy.remaining)-q
	buy.filled=int(buy.filled)+q
	sell.remaining=int(sell.remaining)-q
	sell.filled=int(sell.filled)+q
	# Price improvement belongs to the buyer immediately, while the rest of
	# their outstanding quantity remains escrow-backed at the original limit.
	var excess: int=Ledger.balance(s,str(buy.escrow_account))-int(buy.remaining)*int(buy.limit_price)
	if excess>0: _escrow(s,buy,excess,month,true)
	var fill: Dictionary={"id":id,"buy_order_id":buy.id,"sell_order_id":sell.id,"buyer_id":buy.owner_id,"seller_id":sell.owner_id,"product_id":sell.product_id,"specification":sell.specification,"quantity":q,"price":sell.limit_price,"amount":q*int(sell.limit_price),"delivered_cost":cost,"city_id":buy.city_id,"seller_city_id":sell.city_id,"month":month,"shipment_id":shipment_id}
	s.market.fills.append(fill)
	Domain.event(s,"trade","동시 지정가 시장 체결",month,{"actor_id":buy.owner_id,"seller_id":sell.owner_id,"city_id":buy.city_id,"seller_city_id":sell.city_id,"product_id":sell.product_id,"quantity":q,"amount":q*int(sell.limit_price),"unit_price":sell.limit_price,"fill_id":id,"remote":remote,"foreign":Domain.find(s.cities,str(sell.city_id)).get("nation_id")!=Domain.find(s.cities,str(buy.city_id)).get("nation_id")})
	if int(buy.remaining)==0: _close(s,buy,"filled",month)
	if int(sell.remaining)==0: _close(s,sell,"filled",month)
	return q

static func before_turn(s: Dictionary, month: int) -> void:
	if not s.get("market") is Dictionary: return
	for order in s.market.orders:
		if order.status != "open": continue
		var company: Dictionary = Domain.find(s.get("companies", []), str(order.owner_id))
		if int(order.expires_month) < month or int(order.latest_delivery_month) < month or (not company.is_empty() and company.get("status", "active") != "active"):
			_close(s, order, "owner_closed" if not company.is_empty() and company.get("status", "active") != "active" else "expired", month)
			continue
		if order.side != "sell": continue
		var backed: int = 0
		for reservation in order.reservations:
			var lot: Dictionary = Domain.find(s.inventory, str(reservation.lot_id))
			if not lot.is_empty() and int(lot.get("expires_month", 2147483647)) > month: backed += mini(int(lot.quantity), int(reservation.quantity))
		if backed < int(order.remaining): _close(s, order, "stock_lost", month)
	# Releasing an expired reservation never makes spoiled food edible again.
	Industry.expire_inventory(s, month)

static func monthly(s: Dictionary, month: int) -> void:
	if not s.get("market") is Dictionary or int(s.market.resolved_month)==month: return
	s.market.resolved_month=month
	before_turn(s, month)
	var sells: Array=[]
	for order in s.market.orders:
		if order.status == "open" and order.side == "sell": sells.append(order)
	sells.sort_custom(func(a,b): return int(a.limit_price)<int(b.limit_price) if int(a.limit_price)!=int(b.limit_price) else _key(s,str(a.owner_id)+str(a.product_id)+str(a.city_id),month)<_key(s,str(b.owner_id)+str(b.product_id)+str(b.city_id),month))
	for sell in sells:
		var candidates: Array=[]
		var demand: int=0
		for buy in s.market.orders:
			if buy.status!="open" or buy.side!="buy": continue
			var quote: Dictionary=_quote(s,sell,buy,int(buy.remaining),month)
			if quote.get("allowed",false):
				var quantity: int=mini(int(buy.remaining),int(quote.quantity))
				candidates.append({"order":buy,"demand":quantity,"allocation":0})
				demand+=quantity
		if demand<=0: continue
		var supply: int=mini(int(sell.remaining),demand)
		# Allocate once per buyer, so splitting one instruction into many
		# otherwise identical orders cannot acquire extra rounding priority.
		var owners: Dictionary={}
		for candidate in candidates:
			var owner: String=str(candidate.order.owner_id)
			if not owners.has(owner): owners[owner]={"owner":owner,"demand":0,"allocation":0,"orders":[]}
			owners[owner].demand=int(owners[owner].demand)+int(candidate.demand)
			owners[owner].orders.append(candidate)
		var groups: Array=owners.values()
		var assigned: int=0
		for group in groups:
			group.allocation=int(supply*int(group.demand)/demand)
			assigned+=int(group.allocation)
		groups.sort_custom(func(a,b): return _key(s,str(a.owner),month)<_key(s,str(b.owner),month))
		for group in groups:
			if assigned>=supply: break
			if int(group.allocation)<int(group.demand): group.allocation=int(group.allocation)+1; assigned+=1
		for group in groups:
			var remaining: int=int(group.allocation)
			group.orders.sort_custom(func(a,b): return int(a.order.limit_price)>int(b.order.limit_price) if int(a.order.limit_price)!=int(b.order.limit_price) else str(a.order.id)<str(b.order.id))
			for candidate in group.orders:
				if remaining<=0: break
				var filled: int=_execute_fill(s,sell,candidate.order,mini(remaining,int(candidate.demand)),month)
				remaining-=filled

static func order_book(s: Dictionary, product: String="", city: String="") -> Array:
	var rows: Array=[]
	for order in s.get("market",{}).get("orders",[]):
		if order.status!="open" or (not product.is_empty() and order.product_id!=product) or (not city.is_empty() and order.city_id!=city): continue
		var row: Dictionary={}
		for key in ["id","owner_id","side","product_id","city_id","remaining","limit_price","specification","created_month","expires_month","latest_delivery_month","status","min_quality","offered_quality"]: row[key]=order[key]
		rows.append(row)
	return rows

static func summary(s: Dictionary, owner: String) -> Dictionary:
	var orders: Array=[]
	var fills: Array=[]
	for order in s.get("market",{}).get("orders",[]):
		if order.owner_id==owner: orders.append(order.duplicate(true))
	for fill in s.get("market",{}).get("fills",[]):
		if fill.buyer_id==owner or fill.seller_id==owner: fills.append(fill.duplicate(true))
	return {"orders":orders,"fills":fills,"public_orders":order_book(s)}

static func _has_order(s: Dictionary, owner: String, side: String, product: String) -> bool:
	for order in s.get("market", {}).get("orders", []):
		if order.owner_id == owner and order.side == side and order.product_id == product and order.status == "open": return true
	return false

static func ai_commands(s: Dictionary) -> Array:
	var out: Array = []
	if not s.get("market") is Dictionary or _data().is_empty(): return out
	var p: Dictionary = _data().parameters
	var month: int = int(s.get("planning_month", 1))
	if month % int(p.ai_order_period) != 0: return out
	var companies: Array = s.get("companies", []).duplicate()
	companies.sort_custom(func(a,b): return str(a.id)<str(b.id))
	for company in companies:
		if company.get("status", "active") != "active" or str(company.get("director_id", "")) == str(s.get("player_id", "")): continue
		var owner: String = str(company.id)
		var needs: Dictionary = {}
		var outputs: Dictionary = {}
		for facility in s.get("facilities", []):
			if facility.company_id != owner or facility.get("status", "") != "operational": continue
			var recipe: Dictionary = Domain.find(s.get("recipes", []), str(facility.get("recipe_id", "")))
			for product in recipe.get("inputs", {}): needs[product] = int(needs.get(product, 0)) + int(recipe.inputs[product]) * int(p.ai_stock_months)
			for product in recipe.get("outputs", {}): outputs[product] = true
		var buy_command: Dictionary = {}
		for offer in order_book(s):
			var product: String = str(offer.product_id)
			if offer.side != "sell" or offer.owner_id == owner or not needs.has(product) or _has_order(s, owner, "buy", product): continue
			var missing: int = int(needs[product]) - Domain.available_quantity(s, owner, product, str(company.city_id))
			if missing <= 0: continue
			var quantity: int = mini(missing, int(offer.remaining))
			var route: Dictionary = Industry.route_quote(s, str(offer.city_id), str(company.city_id), product, quantity, month)
			var rule: Dictionary = _trade_rule(s, str(offer.city_id), str(company.city_id), product, month)
			if not route.get("allowed", false) or not rule.get("allowed", true): continue
			quantity=mini(quantity,int(route.quantity))
			var price: int=0
			var budget: int=maxi(0,_cash(s,owner)-int(p.ai_cash_reserve))
			while quantity>0:
				route=Industry.route_quote(s,str(offer.city_id),str(company.city_id),product,quantity,month)
				if not route.get("allowed",false) or int(route.get("quantity",0))!=quantity:
					quantity=0
					break
				var tariff: int=int(ceil(float(int(offer.limit_price)*quantity)*int(rule.get("tariff_bps",0))/10000.0))
				var delivered_total: int=int(offer.limit_price)*quantity+int(route.get("fee",0))+tariff
				price=int(delivered_total/quantity)+(1 if delivered_total%quantity>0 else 0)
				var covered: int=int(budget/maxi(1,price))
				if covered>=quantity: break
				quantity=mini(quantity-1,covered)
			if quantity<=0: continue
			var reference: Dictionary=Domain.find(s.get("products",[]),product)
			if price>int(reference.get("price",1))*int(p.ai_price_ceiling_percent)/100: continue
			buy_command = {"id":"ai-market-buy-%d-%s-%s" % [month, owner, product],"actor_id":company.director_id,"acting_company_id":owner,"expected_version":int(s.get("version", 0)),"type":"market_order","payload":{"side":"buy","product_id":product,"quantity":quantity,"limit_price":price,"city_id":company.city_id,"specification":offer.specification,"expires_month":month+int(p.ai_order_months),"latest_delivery_month":month+int(p.ai_order_months)}}
			if validate_command(s, buy_command).is_empty(): out.append(buy_command)
			break
		var products: Array = outputs.keys()
		products.sort()
		for product in products:
			if _has_order(s, owner, "sell", str(product)): continue
			var definition: Dictionary = Domain.find(s.get("products", []), str(product))
			if definition.get("kind", "material") in ["service", "digital"]: continue
			var stock: int = Domain.available_quantity(s, owner, str(product), str(company.city_id))
			var surplus: int = stock - int(needs.get(product, 0))
			if surplus <= 0: continue
			var quantity: int = mini(int(p.max_order_quantity), maxi(1, int(surplus * int(p.ai_surplus_percent) / 100)))
			var price: int = maxi(1, int(int(definition.price) * int(company.get("policy", {}).get("price_percent", 100)) / 100))
			var command: Dictionary = {"id":"ai-market-sell-%d-%s-%s" % [month, owner, product],"actor_id":company.director_id,"acting_company_id":owner,"expected_version":int(s.get("version", 0)),"type":"market_order","payload":{"side":"sell","product_id":product,"quantity":quantity,"limit_price":price,"city_id":company.city_id,"specification":"standard","expires_month":month+int(p.ai_order_months),"latest_delivery_month":month+int(p.ai_order_months)}}
			if validate_command(s, command).is_empty(): out.append(command)
			break
	return out

static func validate(s: Dictionary) -> Array:
	var errors: Array=[]
	if not s.get("market") is Dictionary: return ["MARKET_STATE_MISSING"]
	for key in ["orders", "fills"]:
		if not s.market.get(key) is Array: errors.append("MARKET_STATE_ARRAY:" + key)
	if not s.market.get("applied_commands") is Dictionary: errors.append("MARKET_STATE_COMMAND_IDS")
	if not errors.is_empty(): return errors
	for order in s.market.orders:
		if not order is Dictionary: errors.append("MARKET_STATE_ORDER"); continue
		for key in ["id", "owner_id", "side", "product_id", "city_id", "specification", "status", "escrow_account"]:
			if not order.get(key) is String: errors.append("MARKET_ORDER_FIELD:" + key)
		for key in ["remaining", "filled", "quantity", "limit_price", "created_month", "expires_month", "latest_delivery_month"]:
			if not _whole(order.get(key)): errors.append("MARKET_ORDER_NUMBER:" + key)
		if not order.get("reservations") is Array: errors.append("MARKET_ORDER_RESERVATIONS")
	for fill in s.market.fills:
		if not fill is Dictionary: errors.append("MARKET_STATE_FILL"); continue
		for key in ["id", "buy_order_id", "sell_order_id"]:
			if not fill.get(key) is String: errors.append("MARKET_FILL_FIELD:" + key)
		for key in ["quantity", "amount", "price"]:
			if not _whole(fill.get(key)): errors.append("MARKET_FILL_NUMBER:" + key)
	if not errors.is_empty(): return errors
	var ids: Dictionary={}
	var reservations: Dictionary={}
	for order in s.market.get("orders",[]):
		if ids.has(order.id): errors.append("MARKET_DUPLICATE_ORDER")
		ids[order.id]=true
		if int(order.remaining)<0 or int(order.filled)<0 or int(order.remaining)+int(order.filled)!=int(order.quantity): errors.append("MARKET_QUANTITY_MISMATCH")
		if order.side=="buy":
			var expected: int=int(order.remaining)*int(order.limit_price) if order.status=="open" else 0
			if Ledger.balance(s,str(order.escrow_account))!=expected: errors.append("MARKET_ESCROW_MISMATCH")
		elif order.status=="open":
			var total: int=0
			for reservation in order.reservations:
				total+=int(reservation.quantity)
				reservations[reservation.lot_id]=int(reservations.get(reservation.lot_id,0))+int(reservation.quantity)
			if total!=int(order.remaining): errors.append("MARKET_SELL_RESERVATION_MISMATCH")
	for lot_id in reservations:
		var lot: Dictionary=Domain.find(s.get("inventory",[]),str(lot_id))
		if lot.is_empty() or int(lot.get("reserved",0))<int(reservations[lot_id]) or int(lot.get("quantity",0))<int(reservations[lot_id]): errors.append("MARKET_UNBACKED_STOCK")
	for fill in s.market.get("fills",[]):
		if int(fill.quantity)<=0 or int(fill.amount)!=int(fill.quantity)*int(fill.price) or not ids.has(fill.buy_order_id) or not ids.has(fill.sell_order_id): errors.append("MARKET_FILL_INVALID")
	return errors
