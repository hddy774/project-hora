extends RefCounted
## A tile has one title owner. Tenant rights and landlord receivables survive
## title transfer; no sale, lease or unpaid rent creates cash.
const Ledger = preload("res://core/ledger.gd")
const PATH = "res://data/land.json"
static var _cache: Dictionary = {}

static func definition() -> Dictionary:
	if not _cache.is_empty(): return _cache.duplicate(true)
	var parsed: Variant=JSON.parse_string(FileAccess.get_file_as_string(PATH))
	if not parsed is Dictionary or parsed.get("status")!="experimental" or parsed.get("version")!=1: return {}
	for key in ["opening_tile_value","sale_price_per_tile","monthly_rent_per_tile","offer_months","default_lease_months","maximum_term_months","default_after_months","maximum_transaction","plot_width","plot_height","tutorial_x","tutorial_y","ai_cash_reserve"]:
		if not _integer(parsed.get(key)) or int(parsed[key])<=0: return {}
		parsed[key]=int(parsed[key])
	if not parsed.get("allowed_uses") is Array or parsed.allowed_uses.size()<3 or not parsed.get("building_uses") is Dictionary: return {}
	for use in parsed.building_uses.values():
		if use not in parsed.allowed_uses: return {}
	_cache=parsed
	return _cache.duplicate(true)

static func content_hash() -> String:
	return JSON.stringify(definition(),"",true,true).sha256_text()

static func _integer(value: Variant) -> bool:
	return (typeof(value)==TYPE_INT or typeof(value)==TYPE_FLOAT) and is_finite(float(value)) and float(value)==floor(float(value)) and abs(float(value))<=Ledger.MAX_INTEGER

static func _find(items: Array,id: String) -> Dictionary:
	for item in items:
		if item is Dictionary and str(item.get("id",""))==id: return item
	return {}

static func _key(city: String,x: int,y: int) -> String:
	return "%s:%d:%d"%[city,x,y]

static func _rect(p: Dictionary) -> Dictionary:
	return {"x":int(p.get("x",-1)),"y":int(p.get("y",-1)),"w":int(p.get("w",0)),"h":int(p.get("h",0))}

static func _contains(a: Dictionary,b: Dictionary) -> bool:
	return int(a.x)<=int(b.x) and int(a.y)<=int(b.y) and int(a.x)+int(a.w)>=int(b.x)+int(b.w) and int(a.y)+int(a.h)>=int(b.y)+int(b.h)

static func _overlap(a: Dictionary,b: Dictionary) -> bool:
	return int(a.x)<int(b.x)+int(b.w) and int(b.x)<int(a.x)+int(a.w) and int(a.y)<int(b.y)+int(b.h) and int(b.y)<int(a.y)+int(a.h)

static func _tiles(city: String,rect: Dictionary) -> Array:
	var result: Array=[]
	for y in range(int(rect.y),int(rect.y)+int(rect.h)):
		for x in range(int(rect.x),int(rect.x)+int(rect.w)): result.append(_key(city,x,y))
	return result

static func _area(rect: Dictionary) -> int:
	return int(rect.w)*int(rect.h)

static func _event(s: Dictionary,type: String,reason: String,month: int,extra: Dictionary={}) -> void:
	var e: Dictionary={"id":"ev-%d"%s.events.size(),"type":type,"reason":reason,"month":month,"actor_id":"","city_id":"","product_id":"","quantity":0,"amount":0}
	e.merge(extra,true)
	s.events.append(e)

static func _account(s: Dictionary,owner: String,suffix: String,kind: String) -> String:
	var id: String=owner+":"+suffix
	Ledger.open_account(s,id,owner,kind)
	return id

static func _line(account: String,amount: int) -> Dictionary:
	return {"account":account,"debit":maxi(0,amount),"credit":maxi(0,-amount)}

static func _post(s: Dictionary,id: String,lines: Array,reason: String,month: int) -> Dictionary:
	var actual: Array=lines.filter(func(l):return int(l.debit)>0 or int(l.credit)>0)
	if actual.is_empty(): return {"ok":true,"error":""}
	return Ledger.post(s,id,actual,reason,month)

static func _owner(c: Dictionary) -> String:
	return str(c.acting_company_id) if not str(c.get("acting_company_id","")).is_empty() else str(c.get("actor_id",""))

static func _authorized(s: Dictionary,c: Dictionary) -> bool:
	var actor: String=str(c.get("actor_id",""))
	var owner: String=_owner(c)
	if _find(s.get("people",[]),actor).is_empty(): return false
	if actor==owner: return true
	var company: Dictionary=_find(s.get("companies",[]),owner)
	if not company.is_empty():
		if company.get("status","active") not in ["active","liquidating"]: return false
		if company.get("director_id")==actor: return true
		if ResourceLoader.exists("res://core/lifecycle.gd"):
			var life: Variant=load("res://core/lifecycle.gd")
			var bound: int=0
			for reservation in planning_reservations(s,c): bound+=int(reservation.quantity)
			if life!=null and life.has_method("has_delegated_permission"): return life.has_delegated_permission(s,actor,owner,str(c.type),bound)
	for office in s.get("public",{}).get("offices",[]):
		if office.get("person_id")==actor and office.get("government_id")==owner: return true
	return false

static func _known_owner(s: Dictionary,owner: String) -> bool:
	return s.accounts.has(owner+":cash")

static func _initial_assign(s: Dictionary,owner: String,city: String,rect: Dictionary) -> void:
	for tile in _tiles(city,rect):
		if s.land.titles.has(tile) and not s.land.roads.has(tile): s.land.titles[tile]=owner

static func initialize(s: Dictionary) -> void:
	if s.get("land") is Dictionary and s.land.get("initialized",false): return
	var d: Dictionary=definition()
	if d.is_empty(): return
	s.land={"initialized":true,"version":1,"titles":{},"roads":{},"uses":{},"offers":[],"leases":[],"claims":[],"applied_commands":{},"last_serviced_month":0,"next_id":0}
	for city in s.cities:
		var gov: String="gov-city-"+str(city.id)
		# Standalone worlds still identify a finite public title owner.
		if not s.accounts.has(gov+":cash"): Ledger.open_account(s,gov+":cash",gov,"cash")
		for y in range(int(city.height)):
			for x in range(int(city.width)):
				var tile: String=_key(city.id,x,y)
				s.land.titles[tile]=gov
				s.land.uses[tile]="mixed"
		for road in city.get("roads",[]): s.land.roads[_key(city.id,int(road.x),int(road.y))]=true
	for facility in s.get("facilities",[]):
		_initial_assign(s,str(facility.get("company_id",facility.get("owner_id",""))),str(facility.city_id),_rect(facility))
	for city in s.cities:
		var tutorial: Dictionary={"x":int(d.tutorial_x),"y":int(d.tutorial_y),"w":int(d.plot_width),"h":int(d.plot_height)}
		var vacant: bool=int(tutorial.x)+int(tutorial.w)<=int(city.width) and int(tutorial.y)+int(tutorial.h)<=int(city.height)
		for tile in _tiles(city.id,tutorial):
			if s.land.roads.has(tile): vacant=false
		for facility in s.get("facilities",[]):
			if facility.city_id==city.id and _overlap(tutorial,_rect(facility)): vacant=false
		if vacant:
			for company in s.companies:
				if company.city_id==city.id:
					_initial_assign(s,company.id,city.id,tutorial)
					break
	# Opening titles are actual assets, issued only once with explicit opening equity.
	var totals: Dictionary={}
	for tile in s.land.titles:
		var owner: String=s.land.titles[tile]
		var city: String=str(tile).rsplit(":",true,2)[0]
		var group: String=owner+"|"+city
		totals[group]=int(totals.get(group,0))+int(d.opening_tile_value)
	for group in totals:
		var parts: PackedStringArray=str(group).split("|")
		var value: int=int(totals[group])
		_post(s,"land-opening-"+group,[_line(_account(s,parts[0],"land:"+parts[1],"asset"),value),_line(_account(s,parts[0],"opening_land_equity","equity"),-value)],"개시 타일 토지 소유권",0)
	for city in s.cities:
		var gov: String="gov-city-"+str(city.id)
		for y in range(0,int(city.height)-int(d.plot_height)+1,int(d.plot_height)):
			for x in range(0,int(city.width)-int(d.plot_width)+1,int(d.plot_width)):
				var rect: Dictionary={"x":x,"y":y,"w":int(d.plot_width),"h":int(d.plot_height)}
				if _title_error(s,gov,city.id,rect)!="" or not _road_adjacent(s,city.id,rect): continue
				_new_offer(s,gov,city.id,rect,"sale",_area(rect)*int(d.sale_price_per_tile),int(d.offer_months),int(d.default_lease_months),0)

static func _road_adjacent(s: Dictionary,city: String,rect: Dictionary) -> bool:
	for x in range(int(rect.x),int(rect.x)+int(rect.w)):
		if s.land.roads.has(_key(city,x,int(rect.y)-1)) or s.land.roads.has(_key(city,x,int(rect.y)+int(rect.h))): return true
	for y in range(int(rect.y),int(rect.y)+int(rect.h)):
		if s.land.roads.has(_key(city,int(rect.x)-1,y)) or s.land.roads.has(_key(city,int(rect.x)+int(rect.w),y)): return true
	return false

static func _title_error(s: Dictionary,owner: String,city: String,rect: Dictionary) -> String:
	var city_data: Dictionary=_find(s.get("cities",[]),city)
	if city_data.is_empty(): return "UNKNOWN_CITY"
	if int(rect.x)<0 or int(rect.y)<0 or int(rect.w)<=0 or int(rect.h)<=0 or int(rect.x)+int(rect.w)>int(city_data.width) or int(rect.y)+int(rect.h)>int(city_data.height): return "LAND_OUT_OF_BOUNDS"
	for tile in _tiles(city,rect):
		if s.land.roads.has(tile): return "PUBLIC_ROAD_RESERVED"
		if s.land.titles.get(tile,"")!=owner: return "LAND_TITLE_REQUIRED"
	return ""

static func _live(s: Dictionary,lease: Dictionary) -> bool:
	return lease.get("status","")=="active" and int(s.planning_month)<=int(lease.get("end_month",0))

static func _active_lease(s: Dictionary,city: String,tile: String,month: int) -> Dictionary:
	for lease in s.land.leases:
		if lease.city_id==city and lease.status=="active" and month>=int(lease.start_month) and month<=int(lease.end_month) and tile in _tiles(city,lease.rect): return lease
	return {}

static func rights_error(s: Dictionary,owner: String,city: String,rect: Dictionary) -> String:
	if not s.get("land",{}).get("initialized",false): return ""
	var city_data: Dictionary=_find(s.get("cities",[]),city)
	if city_data.is_empty(): return "UNKNOWN_CITY"
	if int(rect.x)<0 or int(rect.y)<0 or int(rect.w)<=0 or int(rect.h)<=0 or int(rect.x)+int(rect.w)>int(city_data.width) or int(rect.y)+int(rect.h)>int(city_data.height): return "LAND_OUT_OF_BOUNDS"
	for tile in _tiles(city,rect):
		if s.land.roads.has(tile): return "PUBLIC_ROAD_RESERVED"
		var lease: Dictionary=_active_lease(s,city,tile,int(s.planning_month))
		if not lease.is_empty():
			if lease.tenant_id!=owner: return "TENANT_POSSESSION_RESERVED"
		elif s.land.titles.get(tile,"")!=owner: return "LAND_OWNERSHIP_OR_LEASE_REQUIRED"
		var use: String=str(rect.get("use",""))
		if use=="" and rect.has("kind"): use=building_use(str(rect.kind))
		if use!="" and s.land.uses.get(tile,"mixed") not in ["mixed",use]: return "LAND_USE_NOT_PERMITTED"
	return ""

static func building_use(kind: String) -> String:
	return str(definition().building_uses.get(kind,"industry"))

static func can_build(s: Dictionary,owner: String,city: String,rect: Dictionary) -> bool:
	return rights_error(s,owner,city,rect)==""

static func can_operate(s: Dictionary,owner: String,city: String,rect: Dictionary) -> bool:
	return can_build(s,owner,city,rect)

static func _new_offer(s: Dictionary,owner: String,city: String,rect: Dictionary,kind: String,price: int,term: int,duration: int,month: int) -> Dictionary:
	var offer: Dictionary={"id":"land-offer-%d"%int(s.land.next_id),"owner_id":owner,"city_id":city,"rect":rect.duplicate(),"kind":kind,"price":price,"duration_months":duration,"start_month":month,"expires_month":month+term,"status":"open"}
	s.land.next_id=int(s.land.next_id)+1
	s.land.offers.append(offer)
	return offer

static func command_types() -> Array:
	return ["land_offer_sale","land_offer_lease","land_buy","land_rent","land_cancel_offer","land_change_use","land_pay_rent"]

static func validate_command(s: Dictionary,c: Dictionary) -> String:
	if not s.get("land",{}).get("initialized",false): return "LAND_NOT_INITIALIZED"
	if not c.get("id") is String or str(c.id).is_empty() or s.land.applied_commands.has(c.id): return "LAND_COMMAND_ID_INVALID"
	if c.get("expected_version",-1)!=s.get("version",0): return "STALE_STATE"
	if not c.get("payload") is Dictionary or c.get("type") not in command_types(): return "LAND_COMMAND_INVALID"
	if not _authorized(s,c): return "LAND_AUTHORITY_REQUIRED"
	var p: Dictionary=c.payload
	var type: String=c.type
	var owner: String=_owner(c)
	var d: Dictionary=definition()
	if type in ["land_offer_sale","land_offer_lease","land_change_use"]:
		for key in ["x","y","w","h"]:
			if not _integer(p.get(key)): return "LAND_RECT_INVALID"
		var rect: Dictionary=_rect(p)
		var error: String=_title_error(s,owner,str(p.get("city_id","")),rect)
		if error!="": return error
		for lease in s.land.leases:
			if lease.city_id==p.city_id and _live(s,lease) and _overlap(rect,lease.rect):
				if type!="land_offer_sale": return "LEASE_RIGHTS_MUST_BE_PRESERVED"
				if not _contains(rect,lease.rect): return "LEASE_TITLE_CANNOT_BE_SPLIT"
		if type=="land_change_use":
			if p.get("use") not in d.allowed_uses: return "LAND_USE_INVALID"
			for facility in s.get("facilities",[]):
				if facility.city_id==p.city_id and _overlap(rect,_rect(facility)) and p.use not in ["mixed",building_use(str(facility.get("kind","")))]: return "FACILITY_CONVERSION_REQUIRED"
		else:
			for key in ["price","term_months"]:
				if not _integer(p.get(key)) or int(p[key])<=0: return "LAND_OFFER_TERMS_INVALID"
			if int(p.price)>int(d.maximum_transaction) or int(p.term_months)>int(d.maximum_term_months): return "LAND_OFFER_TERMS_INVALID"
			if type=="land_offer_lease" and (not _integer(p.get("duration_months")) or int(p.duration_months)<=0 or int(p.duration_months)>int(d.maximum_term_months)): return "LEASE_DURATION_INVALID"
		return ""
	if type=="land_cancel_offer":
		var offer: Dictionary=_find(s.land.offers,str(p.get("offer_id","")))
		return "" if not offer.is_empty() and offer.owner_id==owner and offer.status=="open" else "LAND_OFFER_NOT_OWNED"
	if type=="land_pay_rent":
		var claim: Dictionary=_find(s.land.claims,str(p.get("claim_id","")))
		if claim.is_empty() or claim.tenant_id!=owner or int(claim.remaining)<=0: return "RENT_CLAIM_NOT_OWNED"
		if not _integer(p.get("amount")) or int(p.amount)<=0 or int(p.amount)>int(claim.remaining): return "RENT_AMOUNT_INVALID"
		return "" if Ledger.balance(s,owner+":cash")>=int(p.amount) else "INSUFFICIENT_CASH"
	var offer: Dictionary=_find(s.land.offers,str(p.get("offer_id","")))
	if offer.is_empty() or offer.status!="open" or int(s.planning_month)>int(offer.expires_month): return "LAND_OFFER_UNAVAILABLE"
	if offer.owner_id==owner: return "LAND_SELF_TRADE"
	if (type=="land_buy" and offer.kind!="sale") or (type=="land_rent" and offer.kind!="lease"): return "LAND_OFFER_KIND_MISMATCH"
	var error: String=_title_error(s,offer.owner_id,offer.city_id,offer.rect)
	if error!="": return error
	for lease in s.land.leases:
		if lease.city_id==offer.city_id and _live(s,lease) and _overlap(offer.rect,lease.rect) and type=="land_rent": return "LAND_ALREADY_LEASED"
	return "" if Ledger.balance(s,owner+":cash")>=int(offer.price) else "INSUFFICIENT_CASH"

static func planning_reservations(s: Dictionary,c: Dictionary) -> Array:
	if not c.get("payload") is Dictionary: return []
	var amount: int=0
	if c.get("type") in ["land_buy","land_rent"]:
		var offer: Dictionary=_find(s.get("land",{}).get("offers",[]),str(c.payload.get("offer_id","")))
		amount=int(offer.get("price",0))
	elif c.get("type")=="land_pay_rent" and _integer(c.payload.get("amount")): amount=int(c.payload.amount)
	return [{"owner_id":_owner(c),"resource_type":"cash","resource_id":"CR","quantity":amount}] if amount>0 else []

static func _stage(s: Dictionary) -> Dictionary:
	var work: Dictionary=s.duplicate()
	for key in ["land","accounts"]: work[key]=s[key].duplicate(true)
	for key in ["journal","events"]: work[key]=s[key].duplicate()
	work.journal_index=s.get("journal_index",{}).duplicate()
	return work

static func apply(s: Dictionary,c: Dictionary,month: int) -> Dictionary:
	var error: String=validate_command(s,c)
	if error!="": return {"ok":false,"error":error}
	var work: Dictionary=_stage(s)
	var p: Dictionary=c.payload
	var owner: String=_owner(c)
	var result: Dictionary={"ok":true,"error":""}
	match str(c.type):
		"land_offer_sale","land_offer_lease":
			_new_offer(work,owner,str(p.city_id),_rect(p),"sale" if c.type=="land_offer_sale" else "lease",int(p.price),int(p.term_months),int(p.get("duration_months",definition().default_lease_months)),month)
		"land_cancel_offer": _find(work.land.offers,str(p.offer_id)).status="cancelled"
		"land_change_use":
			for tile in _tiles(p.city_id,_rect(p)): work.land.uses[tile]=str(p.use)
		"land_buy":
			var offer: Dictionary=_find(work.land.offers,str(p.offer_id))
			result=transfer_titles(work,offer.owner_id,owner,offer.city_id,offer.rect,int(offer.price),month,str(c.id))
			if result.ok: offer.status="filled"
		"land_rent":
			var offer: Dictionary=_find(work.land.offers,str(p.offer_id))
			var lease: Dictionary={"id":"lease-%d"%int(work.land.next_id),"landlord_id":offer.owner_id,"tenant_id":owner,"city_id":offer.city_id,"rect":offer.rect.duplicate(),"monthly_rent":int(offer.price),"start_month":month,"end_month":month+int(offer.duration_months)-1,"next_due_month":month+1,"status":"active","missed_months":0}
			work.land.next_id=int(work.land.next_id)+1
			work.land.leases.append(lease)
			var claim: Dictionary=_accrue_rent(work,lease,month)
			result=_pay_claim(work,claim,int(claim.remaining),month,str(c.id))
			if result.ok: offer.status="filled"
		"land_pay_rent": result=_pay_claim(work,_find(work.land.claims,str(p.claim_id)),int(p.amount),month,str(c.id))
	if not result.ok: return result
	work.land.applied_commands[c.id]=month
	_event(work,"land",str(c.type),month,{"actor_id":owner,"city_id":str(p.get("city_id","")),"command_id":c.id})
	for key in ["land","accounts","journal","journal_index","events"]: s[key]=work[key]
	return {"ok":true,"error":""}

static func _owned_area(s: Dictionary,owner: String,city: String) -> int:
	var total: int=0
	for tile in s.land.titles:
		if str(tile).begins_with(city+":") and s.land.titles[tile]==owner: total+=1
	return total

static func transfer_titles(s: Dictionary,seller: String,buyer: String,city: String,rect: Dictionary,price: int,month: int,id: String) -> Dictionary:
	var error: String=_title_error(s,seller,city,rect)
	if error!="": return {"ok":false,"error":error}
	if price<0 or not _known_owner(s,buyer): return {"ok":false,"error":"LAND_TRANSFER_INVALID"}
	var seller_asset: String=_account(s,seller,"land:"+city,"asset")
	var buyer_asset: String=_account(s,buyer,"land:"+city,"asset")
	var cost: int=int(Ledger.balance(s,seller_asset)*_area(rect)/maxi(1,_owned_area(s,seller,city)))
	var lines: Array=[_line(buyer+":cash",-price),_line(buyer_asset,price),_line(seller+":cash",price),_line(seller_asset,-cost)]
	if price==0 and cost>0:
		lines.append(_line(buyer_asset,cost))
		lines.append(_line(_account(s,buyer,"land_transfer_capital","equity"),-cost))
	if price>cost: lines.append(_line(_account(s,seller,"land_sale_gain","revenue"),cost-price))
	elif cost>price: lines.append(_line(_account(s,seller,"land_sale_loss","expense"),cost-price))
	var result: Dictionary=_post(s,"land-sale-"+id,lines,"유한 토지 매각 대금·장부원가·처분손익",month)
	if not result.ok: return result
	for tile in _tiles(city,rect): s.land.titles[tile]=buyer
	for lease in s.land.leases:
		if lease.city_id==city and lease.status=="active" and _contains(rect,lease.rect): lease.landlord_id=buyer
	for offer in s.land.offers:
		if offer.city_id==city and offer.owner_id==seller and offer.status=="open" and _overlap(rect,offer.rect): offer.status="title_transferred"
	_event(s,"land_transfer","토지 소유권 이전·기존 임차권 유지",month,{"actor_id":buyer,"seller_id":seller,"city_id":city,"amount":price,"quantity":_area(rect),"journal_id":"land-sale-"+id})
	return {"ok":true,"error":"","cost":cost}

static func convey_facility_titles(s: Dictionary,seller: String,buyer: String,facility: Dictionary,total_price: int,month: int,id: String) -> Dictionary:
	# Called after a paid facility package has been capitalized. Reclassify a
	# portion of that existing price into land; never charge the buyer twice.
	if not s.get("land",{}).get("initialized",false): return {"ok":true,"error":""}
	var city: String=str(facility.city_id)
	var rect: Dictionary=_rect(facility)
	if _title_error(s,seller,city,rect)!="":
		return {"ok":true,"error":"","title_transferred":false,"reason":"FACILITY_ONLY_EXISTING_THIRD_PARTY_TITLE"}
	for lease in s.land.leases:
		if lease.city_id==city and lease.status=="active" and _overlap(rect,lease.rect) and not _contains(rect,lease.rect): return {"ok":true,"error":"","title_transferred":false,"reason":"WHOLE_LEASE_TITLE_RETAINED_BY_ESTATE"}
	var seller_asset: String=_account(s,seller,"land:"+city,"asset")
	var buyer_asset: String=_account(s,buyer,"land:"+city,"asset")
	var basis: int=int(Ledger.balance(s,seller_asset)*_area(rect)/maxi(1,_owned_area(s,seller,city)))
	var allocation: int=mini(maxi(0,total_price),basis)
	var building: String=_account(s,buyer,"facility:"+str(facility.get("project_id",facility.id)),"asset")
	if Ledger.balance(s,building)<allocation: return {"ok":false,"error":"FACILITY_PACKAGE_BOOK_MISSING"}
	var lines: Array=[_line(seller_asset,-basis),_line(_account(s,seller,"land_package_cost","expense"),basis),_line(buyer_asset,allocation),_line(building,-allocation)]
	var result: Dictionary=_post(s,"land-package-"+id,lines,"시설 패키지 대금 내 토지 원가와 건물 취득원가 분리",month)
	if not result.ok: return result
	for tile in _tiles(city,rect): s.land.titles[tile]=buyer
	for lease in s.land.leases:
		if lease.city_id==city and lease.status=="active" and _contains(rect,lease.rect): lease.landlord_id=buyer
	for offer in s.land.offers:
		if offer.city_id==city and offer.owner_id==seller and offer.status=="open" and _overlap(rect,offer.rect): offer.status="title_transferred"
	_event(s,"land_transfer","실제 시설 매각의 토지 대금 배분·임차권 유지",month,{"actor_id":buyer,"seller_id":seller,"city_id":city,"amount":allocation,"quantity":_area(rect)})
	return {"ok":true,"error":"","title_transferred":true,"land_cost":basis,"land_price":allocation}

static func estate_assets(s: Dictionary,owner: String) -> Array:
	var result: Array=[]
	if not s.get("land",{}).get("initialized",false): return result
	var available: Dictionary={}
	for tile in s.land.titles:
		if s.land.titles[tile]==owner and not s.land.roads.has(tile): available[tile]=true
	# Facilities are priced by their own estate listing; their title is conveyed
	# within that package. Never list the same tile twice as a separate asset.
	for facility in s.get("facilities",[]):
		if facility.get("company_id",facility.get("owner_id",""))==owner:
			for tile in _tiles(str(facility.city_id),_rect(facility)): available.erase(tile)
	var value: int=int(definition().sale_price_per_tile)
	for lease in s.land.leases:
		if lease.landlord_id!=owner or lease.status!="active": continue
		var whole: bool=true
		for tile in _tiles(lease.city_id,lease.rect):
			if not available.has(tile): whole=false
		if whole:
			result.append({"city_id":lease.city_id,"rect":lease.rect.duplicate(),"name":"임차권이 유지되는 토지 "+lease.id,"price":_area(lease.rect)*value})
			for tile in _tiles(lease.city_id,lease.rect): available.erase(tile)
		else:
			# A partial active lease is retained until the whole footprint is free
			# of estate-owned facilities and can be listed in a later refresh.
			for tile in _tiles(lease.city_id,lease.rect): available.erase(tile)
	for city in s.cities:
		for y in range(int(city.height)):
			for x in range(int(city.width)):
				if not available.has(_key(city.id,x,y)): continue
				var width: int=1
				while width<int(definition().plot_width) and available.has(_key(city.id,x+width,y)): width+=1
				var height: int=1
				while height<int(definition().plot_height):
					var row: bool=true
					for dx in range(width):
						if not available.has(_key(city.id,x+dx,y+height)): row=false
					if not row: break
					height+=1
				var rect: Dictionary={"x":x,"y":y,"w":width,"h":height}
				result.append({"city_id":city.id,"rect":rect,"name":"토지 (%d,%d) %d×%d"%[x,y,width,height],"price":width*height*value})
				for tile in _tiles(city.id,rect): available.erase(tile)
	return result

static func _accrue_rent(s: Dictionary,lease: Dictionary,month: int) -> Dictionary:
	var claim: Dictionary={"id":"rent-%s-%d"%[lease.id,month],"lease_id":lease.id,"landlord_id":lease.landlord_id,"tenant_id":lease.tenant_id,"city_id":lease.city_id,"month":month,"amount":int(lease.monthly_rent),"remaining":int(lease.monthly_rent)}
	var payable: String=_account(s,lease.tenant_id,"rent_payable:"+claim.id,"liability")
	var receivable: String=_account(s,lease.landlord_id,"rent_receivable:"+claim.id,"asset")
	var result: Dictionary=_post(s,"land-accrue-"+claim.id,[_line(_account(s,lease.tenant_id,"land_rent","expense"),claim.amount),_line(payable,-int(claim.amount)),_line(receivable,claim.amount),_line(_account(s,lease.landlord_id,"land_rent_income","revenue"),-int(claim.amount))],"토지 임대료 발생·미지급과 미수",month)
	if not result.ok: return {}
	s.land.claims.append(claim)
	return claim

static func _pay_claim(s: Dictionary,claim: Dictionary,amount: int,month: int,id: String) -> Dictionary:
	if amount<=0 or amount>int(claim.get("remaining",0)): return {"ok":false,"error":"RENT_AMOUNT_INVALID"}
	var result: Dictionary=_post(s,"land-rent-pay-"+id,[_line(claim.tenant_id+":cash",-amount),_line(claim.tenant_id+":rent_payable:"+claim.id,amount),_line(claim.landlord_id+":cash",amount),_line(claim.landlord_id+":rent_receivable:"+claim.id,-amount)],"발생 임대료 실제 지급·채권 채무 정산",month)
	if result.ok:
		claim.remaining=int(claim.remaining)-amount
		_event(s,"rent_payment","실제 자금으로 임대료 지급",month,{"actor_id":claim.tenant_id,"payee_id":claim.landlord_id,"city_id":claim.city_id,"amount":amount,"claim_id":claim.id})
	return result

static func debt_total(s: Dictionary,owner: String) -> int:
	var amount: int=0
	for claim in s.get("land",{}).get("claims",[]):
		if claim.tenant_id==owner: amount+=int(claim.remaining)
	return amount

static func settle_debt(s: Dictionary,owner: String,cash_budget: int,month: int) -> int:
	var paid: int=0
	for claim in s.get("land",{}).get("claims",[]):
		if claim.tenant_id!=owner: continue
		var amount: int=mini(int(claim.remaining),mini(cash_budget-paid,Ledger.balance(s,owner+":cash")))
		if amount>0:
			var result: Dictionary=_pay_claim(s,claim,amount,month,"estate-%d"%s.journal.size())
			if result.ok: paid+=amount
	return paid

static func before_turn(s: Dictionary,month: int) -> void:
	monthly(s,month)

static func monthly(s: Dictionary,month: int) -> void:
	if not s.get("land",{}).get("initialized",false) or month<=int(s.land.last_serviced_month): return
	for offer in s.land.offers:
		if offer.status=="open" and month>int(offer.expires_month): offer.status="expired"
	for lease in s.land.leases:
		if lease.status!="active": continue
		if month>int(lease.end_month):
			lease.status="expired"
			_event(s,"lease_expired","임대 사용권 만료·시설과 미지급 의무 보존",month,{"actor_id":lease.tenant_id,"city_id":lease.city_id,"lease_id":lease.id})
			continue
		if month>=int(lease.next_due_month):
			_accrue_rent(s,lease,month)
			lease.next_due_month=month+1
		settle_debt(s,lease.tenant_id,Ledger.balance(s,lease.tenant_id+":cash"),month)
		var overdue: int=0
		for claim in s.land.claims:
			if claim.lease_id==lease.id and int(claim.remaining)>0: overdue+=1
		lease.missed_months=overdue
		if overdue>=int(definition().default_after_months):
			lease.status="default"
			_event(s,"lease_default","미지급 임대료 누적으로 사용권 종료·채무 유지",month,{"actor_id":lease.tenant_id,"city_id":lease.city_id,"lease_id":lease.id,"amount":debt_total(s,lease.tenant_id)})
	s.land.last_serviced_month=month

static func summary(s: Dictionary,owner: String) -> Dictionary:
	var titles: Array=[]
	for city in s.cities:
		var count: int=_owned_area(s,owner,city.id)
		if count>0: titles.append({"city_id":city.id,"tiles":count,"book_value":Ledger.balance(s,owner+":land:"+str(city.id))})
	return {"titles":titles,"offers":s.land.offers.filter(func(o):return o.status=="open"),"leases":s.land.leases.filter(func(l):return l.tenant_id==owner or l.landlord_id==owner),"claims":s.land.claims.filter(func(c):return c.tenant_id==owner or c.landlord_id==owner),"rent_debt":debt_total(s,owner)}.duplicate(true)

static func _record_rect_valid(s: Dictionary,city: String,value: Variant) -> bool:
	if not value is Dictionary: return false
	for key in ["x","y","w","h"]:
		if not _integer(value.get(key)): return false
	var c: Dictionary=_find(s.get("cities",[]),city)
	return not c.is_empty() and int(value.x)>=0 and int(value.y)>=0 and int(value.w)>0 and int(value.h)>0 and int(value.x)+int(value.w)<=int(c.width) and int(value.y)+int(value.h)<=int(c.height)

static func _strings(record: Dictionary,keys: Array) -> bool:
	for key in keys:
		if not record.get(key) is String or str(record[key]).is_empty(): return false
	return true

static func validate(s: Dictionary) -> Array:
	var errors: Array=[]
	if not s.get("land") is Dictionary or not s.land.get("initialized",false): return ["LAND_STATE_MISSING"]
	for key in ["titles","roads","uses","applied_commands"]:
		if not s.land.get(key) is Dictionary: return ["LAND_REGISTRY_INVALID:"+key]
	for key in ["offers","leases","claims"]:
		if not s.land.get(key) is Array: return ["LAND_REGISTRY_INVALID:"+key]
	for key in ["version","next_id","last_serviced_month"]:
		if not _integer(s.land.get(key)) or int(s.land[key])<0: errors.append("LAND_STATE_INTEGER_INVALID:"+key)
	var count: int=0
	var uses: Array=definition().allowed_uses
	var roads: Dictionary={}
	for city in s.cities:
		for y in range(int(city.height)):
			for x in range(int(city.width)):
				var tile: String=_key(city.id,x,y)
				count+=1
				if not s.land.titles.get(tile) is String or not _known_owner(s,str(s.land.titles.get(tile,""))): errors.append("LAND_OWNER_INVALID:"+tile)
				if s.land.uses.get(tile) not in uses: errors.append("LAND_USE_INVALID:"+tile)
		for road in city.get("roads",[]):
			var tile: String=_key(city.id,int(road.x),int(road.y))
			roads[tile]=true
			if s.land.titles.get(tile)!="gov-city-"+str(city.id) or s.land.roads.get(tile)!=true: errors.append("PUBLIC_ROAD_TITLE_INVALID:"+tile)
	if s.land.titles.size()!=count or s.land.uses.size()!=count: errors.append("LAND_TILE_COUNT_MISMATCH")
	if s.land.roads.size()!=roads.size(): errors.append("LAND_ROAD_COUNT_MISMATCH")
	var ids: Dictionary={}
	for offer in s.land.offers:
		if not offer is Dictionary or not _strings(offer,["id","owner_id","city_id","kind","status"]) or not _record_rect_valid(s,str(offer.get("city_id","")),offer.get("rect")): errors.append("LAND_OFFER_INVALID"); continue
		var valid: bool=offer.kind in ["sale","lease"] and offer.status in ["open","filled","expired","cancelled","title_transferred"] and _known_owner(s,offer.owner_id)
		for key in ["price","duration_months","start_month","expires_month"]:
			if not _integer(offer.get(key)) or int(offer[key])<0: valid=false
		if not valid: errors.append("LAND_OFFER_TERMS_INVALID:"+offer.id); continue
		if int(offer.price)<=0 or int(offer.duration_months)<=0 or int(offer.expires_month)<int(offer.start_month): errors.append("LAND_OFFER_TERMS_INVALID:"+offer.id)
		if ids.has(offer.id): errors.append("LAND_DUPLICATE_ID:"+offer.id)
		ids[offer.id]=true
		if offer.status=="open" and _title_error(s,offer.owner_id,offer.city_id,offer.rect)!="": errors.append("LAND_OPEN_OFFER_TITLE_MISMATCH:"+offer.id)
	var occupied: Dictionary={}
	var lease_ids: Dictionary={}
	for lease in s.land.leases:
		if not lease is Dictionary or not _strings(lease,["id","landlord_id","tenant_id","city_id","status"]) or not _record_rect_valid(s,str(lease.get("city_id","")),lease.get("rect")): errors.append("LEASE_INVALID"); continue
		var valid: bool=lease.status in ["active","expired","default"] and _known_owner(s,lease.landlord_id) and _known_owner(s,lease.tenant_id)
		for key in ["monthly_rent","start_month","end_month","next_due_month","missed_months"]:
			if not _integer(lease.get(key)) or int(lease[key])<0: valid=false
		if not valid: errors.append("LEASE_TERMS_INVALID:"+lease.id); continue
		if int(lease.monthly_rent)<=0 or int(lease.end_month)<int(lease.start_month) or lease.landlord_id==lease.tenant_id: errors.append("LEASE_TERMS_INVALID:"+lease.id)
		if ids.has(lease.id): errors.append("LAND_DUPLICATE_ID:"+lease.id)
		ids[lease.id]=true
		lease_ids[lease.id]=true
		if lease.status=="active":
			for tile in _tiles(lease.city_id,lease.rect):
				if occupied.has(tile): errors.append("LEASE_DOUBLE_POSSESSION:"+tile)
				occupied[tile]=true
				if s.land.titles.get(tile)!=lease.landlord_id: errors.append("LEASE_LANDLORD_TITLE_MISMATCH:"+tile)
	for claim in s.land.claims:
		if not claim is Dictionary or not _strings(claim,["id","lease_id","landlord_id","tenant_id","city_id"]): errors.append("RENT_CLAIM_INVALID"); continue
		var valid: bool=lease_ids.has(claim.lease_id) and _known_owner(s,claim.landlord_id) and _known_owner(s,claim.tenant_id)
		for key in ["month","amount","remaining"]:
			if not _integer(claim.get(key)) or int(claim[key])<0: valid=false
		if not valid: errors.append("RENT_CLAIM_INVALID:"+claim.id); continue
		if int(claim.remaining)>int(claim.amount): errors.append("RENT_CLAIM_INVALID:"+claim.id)
		if ids.has(claim.id): errors.append("LAND_DUPLICATE_ID:"+claim.id)
		ids[claim.id]=true
		if Ledger.balance(s,str(claim.tenant_id)+":rent_payable:"+str(claim.id))!=-int(claim.remaining) or Ledger.balance(s,str(claim.landlord_id)+":rent_receivable:"+str(claim.id))!=int(claim.remaining): errors.append("RENT_COUNTERPART_MISMATCH:"+str(claim.id))
	for command_id in s.land.applied_commands:
		if not command_id is String or not _integer(s.land.applied_commands[command_id]) or int(s.land.applied_commands[command_id])<0: errors.append("LAND_APPLIED_COMMAND_INVALID")
	return errors

static func ui_actions() -> Array:
	var d: Dictionary=definition()
	var rect_fields: Array=[{"key":"city_id","label":"도시","kind":"choice","source":"cities"},{"key":"x","label":"타일 X","kind":"int","min":0,"default":0},{"key":"y","label":"타일 Y","kind":"int","min":0,"default":0},{"key":"w","label":"너비","kind":"int","min":1,"default":int(d.plot_width)},{"key":"h","label":"높이","kind":"int","min":1,"default":int(d.plot_height)}]
	var price_fields: Array=[{"key":"price","label":"대금 / 월 임대료","kind":"int","min":1,"max":int(d.maximum_transaction),"default":int(d.sale_price_per_tile)*int(d.plot_width)*int(d.plot_height)},{"key":"term_months","label":"제안 유효 개월","kind":"int","min":1,"max":int(d.maximum_term_months),"default":int(d.offer_months)},{"key":"duration_months","label":"임대 개월","kind":"int","min":1,"max":int(d.maximum_term_months),"default":int(d.default_lease_months)}]
	var actions: Array=[]
	for type in ["land_offer_sale","land_offer_lease"]: actions.append({"type":type,"label":"토지 매각 제안" if type=="land_offer_sale" else "토지 임대 제안","scope":"both","fields":rect_fields.duplicate(true)+price_fields.duplicate(true)})
	for type in ["land_buy","land_rent","land_cancel_offer"]: actions.append({"type":type,"label":{"land_buy":"토지 매입","land_rent":"토지 임차","land_cancel_offer":"토지 제안 철회"}[type],"scope":"both","fields":[{"key":"offer_id","label":"소유자의 공개 제안","kind":"choice","source":"land_offers"}]})
	actions.append({"type":"land_change_use","label":"소유 토지 용도 변경","scope":"both","fields":rect_fields.duplicate(true)+[{"key":"use","label":"허용 용도","kind":"choice","options":d.allowed_uses}]})
	actions.append({"type":"land_pay_rent","label":"미지급 임대료 상환","scope":"both","fields":[{"key":"claim_id","label":"실제 미지급 임대료","kind":"choice","source":"land_claims"},{"key":"amount","label":"지급액","kind":"int","min":1,"default":10}]})
	return actions

static func ai_commands(s: Dictionary,month: int=-1) -> Array:
	var commands: Array=[]
	if not s.get("land",{}).get("initialized",false): return commands
	if month<0: month=int(s.planning_month)
	var companies: Array=s.get("companies",[]).duplicate()
	companies.sort_custom(func(a,b):return str(a.id)<str(b.id))
	var offers: Array=s.land.offers.filter(func(o):return o.status=="open" and o.kind=="sale")
	offers.sort_custom(func(a,b):return int(a.price)<int(b.price) if int(a.price)!=int(b.price) else str(a.id)<str(b.id))
	for company in companies:
		var actor: String=str(company.get("director_id",""))
		if actor==str(s.get("player_id","")) or actor=="" or company.get("status","active")!="active": continue
		var needs_plot: bool=true
		for facility in s.get("facilities",[]):
			if facility.company_id==company.id and facility.get("status","")!="closed": needs_plot=false; break
		for project in s.get("projects",[]):
			if project.get("company_id",project.get("owner_id",""))==company.id and project.get("status","") in ["active","paused"]: needs_plot=false; break
		if not needs_plot: continue
		for offer in offers:
			if offer.city_id!=company.city_id or Ledger.balance(s,str(company.id)+":cash")<int(offer.price)+int(definition().ai_cash_reserve): continue
			# Existing rights are used before purchasing another parcel.
			if can_build(s,company.id,company.city_id,offer.rect): needs_plot=false; break
		if not needs_plot: continue
		# A tutorial/opening plot may not be among public sale offers.
		for tile in s.land.titles:
			if s.land.titles[tile]==company.id and str(tile).begins_with(str(company.city_id)+":"): needs_plot=false; break
		if not needs_plot: continue
		for offer in offers:
			if offer.city_id!=company.city_id or Ledger.balance(s,str(company.id)+":cash")<int(offer.price)+int(definition().ai_cash_reserve): continue
			var c: Dictionary={"id":"ai-land-%d-%s"%[month,company.id],"actor_id":actor,"acting_company_id":company.id,"expected_version":s.version,"type":"land_buy","payload":{"offer_id":offer.id}}
			if validate_command(s,c)=="": commands.append(c); break
	return commands
