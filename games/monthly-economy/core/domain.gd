extends RefCounted
## Shared domain operations. All mutations run on the resolving work state.
const Ledger = preload("res://core/ledger.gd")

static func find(items: Array, id: String) -> Dictionary:
	for item in items:
		if item is Dictionary and str(item.get("id", "")) == id: return item
	return {}

static func next_id(s: Dictionary, prefix: String) -> String:
	if not s.has("id_counters"): s.id_counters = {}
	var counter: int = int(s.id_counters.get(prefix, 0))
	s.id_counters[prefix] = counter + 1
	return "%s-%d" % [prefix, counter]

static func event(s: Dictionary, event_type: String, reason: String, month: int, extra: Dictionary = {}) -> void:
	if not s.has("events"): s.events = []
	var record: Dictionary = {"id": "ev-%d" % s.events.size(), "month": month, "type": event_type, "reason": reason, "actor_id": "", "city_id": "", "product_id": "", "quantity": 0, "amount": 0}
	record.merge(extra, true)
	s.events.append(record)

static func pay(s: Dictionary, id: String, from_owner: String, to_owner: String, amount: int, reason: String, month: int = 0) -> Dictionary:
	var result: Dictionary = Ledger.transfer(s, id, from_owner, to_owner, amount, reason, month)
	if result.get("ok", false):
		event(s, "payment", reason, month, {"actor_id": from_owner, "payer_id": from_owner, "payee_id": to_owner, "city_id": owner_city(s, from_owner), "amount": amount, "journal_id": id})
	return result

static func register_lot(s: Dictionary, lot: Dictionary) -> void:
	if not s.has("_runtime") or not s._runtime.has("stock_groups"): return
	for key in [str(lot.owner_id)+"|"+str(lot.product_id)+"|"+str(lot.city_id),str(lot.owner_id)+"|"+str(lot.product_id)+"|"]:
		if not s._runtime.stock_groups.has(key): s._runtime.stock_groups[key]=[]
		s._runtime.stock_groups[key].append(lot)

static func stock_lots(s: Dictionary, owner: String, product: String, city: String = "") -> Array:
	if s.has("_runtime") and s._runtime.has("stock_groups"): return s._runtime.stock_groups.get(owner+"|"+product+"|"+city,[])
	var result: Array=[]
	for lot in s.get("inventory",[]):
		if lot.get("owner_id")==owner and lot.get("product_id")==product and (city=="" or lot.get("city_id")==city): result.append(lot)
	return result

static func owned_quantity(s: Dictionary, owner: String, product: String) -> int:
	var amount: int=0
	for lot in stock_lots(s,owner,product): amount+=int(lot.quantity)
	return amount

static func available_quantity(s: Dictionary, owner: String, product: String, city: String = "") -> int:
	var amount: int = 0
	for lot in stock_lots(s,owner,product,city):
		if lot.get("location") != "shipment": amount += maxi(0,int(lot.quantity)-int(lot.get("reserved",0)))
	return amount

static func change_inventory(s: Dictionary, owner: String, city: String, product: String, delta: int, reason: String, month: int, event_type: String = "inventory") -> Dictionary:
	if delta == 0: return {"ok": true, "error": "", "quantity": 0}
	if delta < 0 and available_quantity(s, owner, product, city) < -delta: return {"ok": false, "error": "INSUFFICIENT_STOCK"}
	var affected: Array = []
	if delta < 0:
		for candidate in stock_lots(s,owner,product,city):
			if candidate.owner_id==owner and candidate.city_id==city and candidate.product_id==product and candidate.location!="shipment" and int(candidate.quantity)>int(candidate.get("reserved",0)): affected.append(candidate)
		affected.sort_custom(func(x,y): return int(x.get("expires_month",2147483647))<int(y.get("expires_month",2147483647)) if int(x.get("expires_month",2147483647))!=int(y.get("expires_month",2147483647)) else str(x.id)<str(y.id))
	else:
		var definition: Dictionary=find(s.get("products",[]),product)
		var life: int=int(definition.get("shelf_life_months",0))
		var id: String="lot:%s:%s:%s" % [owner,city,product]
		if life>0: id += ":batch-%d" % month
		var lot: Dictionary=find(s.get("inventory",[]),id)
		if lot.is_empty():
			lot={"id":id,"owner_id":owner,"city_id":city,"accounting_city_id":city,"location":"warehouse","product_id":product,"quantity":0,"reserved":0,"unit_cost":0,"born_month":month}
			if life>0: lot["expires_month"]=month+life
			s.inventory.append(lot)
			register_lot(s,lot)
		affected.append(lot)
	if s.has("finance") and ResourceLoader.exists("res://core/finance.gd"):
		var finance: Variant=load("res://core/finance.gd")
		if finance!=null and finance.has_method("inventory_flow"): finance.inventory_flow(s,owner,product,delta,reason,month)
	var remaining: int=absi(delta)
	var ids: Array=[]
	for lot in affected:
		var quantity: int=remaining if delta>0 else mini(remaining,int(lot.quantity)-int(lot.get("reserved",0)))
		var change: int=quantity if delta>0 else -quantity
		lot.quantity=int(lot.quantity)+change
		event(s,event_type,reason,month,{"actor_id":owner,"city_id":city,"product_id":product,"quantity":quantity,"inventory_delta":change,"lot_id":lot.id})
		ids.append(lot.id)
		remaining-=quantity
		if remaining==0: break
	return {"ok":remaining==0,"error":"" if remaining==0 else "INSUFFICIENT_STOCK","quantity":absi(delta)-remaining,"lot_ids":ids}

static func owner_city(s: Dictionary, owner: String) -> String:
	for collection in [s.get("people", []), s.get("companies", [])]:
		var item: Dictionary = find(collection, owner)
		if not item.is_empty(): return str(item.get("city_id", ""))
	for group_name in ["public", "finance"]:
		var group: Dictionary = s.get(group_name, {})
		for key in group:
			if group[key] is Array:
				var item: Dictionary = find(group[key], owner)
				if not item.is_empty() and item.has("city_id"): return str(item.city_id)
	return ""

static func person(s: Dictionary, id: String) -> Dictionary:
	return find(s.get("people", []), id)

static func company(s: Dictionary, id: String) -> Dictionary:
	return find(s.get("companies", []), id)
