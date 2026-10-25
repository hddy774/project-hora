extends RefCounted
## Financial contracts and accrual stock accounts. All amounts are minor-unit
## integers. Bank deposits, bank reserves, central-bank liabilities and sovereign
## opening backing are separate positions; an approved loan is not free revenue.
const Ledger = preload("res://core/ledger.gd")
const Domain = preload("res://core/domain.gd")
const PATH = "res://data/finance.json"
static var _definition: Dictionary = {}

static func definition() -> Dictionary:
	if _definition.is_empty():
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(PATH))
		if not parsed is Dictionary or parsed.get("status") != "experimental" or parsed.get("version") != 2:
			push_error("FINANCE_DEFINITION_INVALID")
			return {}
		for key in ["bank_capital","insurer_capital","market_cash","foreign_float","initial_shares","opening_facility_value","loan_limit","credit_cash_multiplier","credit_asset_haircut_bps","credit_income_multiple","loan_interest_bps","savings_interest_bps","capital_ratio_bps","cb_credit_limit","default_after_months","max_term_months","insurance_premium_bps","claim_grace_months","exchange_fee_bps","maximum_transaction","ai_company_cash_buffer","ai_loan_target","ai_person_reserve","ai_investment_budget","ai_share_offer_quantity","ai_share_price","ai_offer_term_months","ai_loan_months"]:
			if not _integer(parsed.get(key)) or int(parsed[key]) <= 0:
				push_error("FINANCE_PARAMETER_INVALID:" + key)
				return {}
		if parsed.get("base_currency")!=Ledger.BASE_CURRENCY or parsed.get("minor_unit")!="cent" or parsed.get("minor_units_per_major")!=Ledger.MINOR_UNITS_PER_MAJOR: return {}
		if not parsed.get("currencies") is Array or parsed.currencies.is_empty(): return {}
		if not parsed.get("insurable_events") is Array or parsed.insurable_events.is_empty(): return {}
		var seen: Dictionary={}
		for currency in parsed.currencies:
			if not currency is Dictionary or not currency.get("id") is String or str(currency.id).is_empty() or seen.has(currency.id):
				push_error("FINANCE_CURRENCY_INVALID")
				return {}
			for key in ["numerator","denominator"]:
				if not _integer(currency.get(key)) or int(currency[key])<=0 or int(currency[key])>10000:
					push_error("FINANCE_CURRENCY_RATE_INVALID")
					return {}
			if not Ledger.supported_currency(currency.id) or currency.get("minor_digits")!=2: return {}
			seen[currency.id]=true
		if not seen.has("USD"): return {}
		_definition = _normalize(parsed)
	return _definition.duplicate(true)

static func content_hash() -> String:
	return JSON.stringify(definition(),"",true,true).sha256_text()

static func _normalize(value: Variant) -> Variant:
	if value is Dictionary:
		var result: Dictionary = {}
		for key in value: result[str(key)] = _normalize(value[key])
		return result
	if value is Array:
		var result: Array = []
		for item in value: result.append(_normalize(item))
		return result
	if typeof(value) == TYPE_FLOAT and _integer(value): return int(value)
	return value

static func _integer(v: Variant) -> bool:
	return (typeof(v) == TYPE_INT or typeof(v) == TYPE_FLOAT) and is_finite(float(v)) and float(v) == floor(float(v)) and abs(float(v)) <= Ledger.MAX_INTEGER

static func _find(items: Array, id: String) -> Dictionary:
	for item in items:
		if item is Dictionary and str(item.get("id", "")) == id: return item
	return {}

static func _keys(value: Dictionary) -> Array:
	var keys: Array = value.keys()
	keys.sort()
	return keys

static func _event(s: Dictionary, type: String, reason: String, month: int, extra: Dictionary = {}) -> void:
	var e: Dictionary = {"id":"ev-%d" % s.events.size(),"month":month,"type":type,"reason":reason,"actor_id":"","city_id":"","product_id":"","quantity":0,"amount":0}
	e.merge(extra, true)
	s.events.append(e)

static func _cash_id(owner: String, currency: String = "USD") -> String:
	return owner + ":cash" + ("" if currency == "USD" else ":" + currency)

static func _account(s: Dictionary, owner: String, suffix: String, kind: String, currency: String = "USD") -> String:
	var id: String = owner + ":" + suffix
	Ledger.open_account(s, id, owner, kind, currency)
	return id

static func _line(account: String, amount: int) -> Dictionary:
	return {"account":account,"debit":maxi(amount,0),"credit":maxi(-amount,0)}

static func _post(s: Dictionary, id: String, lines: Array, reason: String, month: int) -> Dictionary:
	var nonzero: Array = lines.filter(func(l): return int(l.debit) != 0 or int(l.credit) != 0)
	if nonzero.is_empty(): return {"ok":true,"error":""}
	return Ledger.post(s, id, nonzero, reason, month)

static func _id(s: Dictionary, prefix: String) -> String:
	return "fin-%s-%d" % [prefix, s.journal.size()]

static func _currency(s: Dictionary, id: String) -> Dictionary:
	return _find(s.finance.currencies, id)

static func cash(s: Dictionary, owner: String, currency: String = "USD") -> int:
	return Ledger.balance(s, _cash_id(owner,currency))

static func _open_wallet(s: Dictionary, owner: String, currency: String) -> void:
	Ledger.open_account(s, _cash_id(owner,currency), owner, "cash", currency)
	_account(s,owner,"equity:"+currency,"equity",currency)
	_account(s,owner,"expense:"+currency,"expense",currency)
	_account(s,owner,"revenue:"+currency,"revenue",currency)

static func _opening_wallet(s: Dictionary, owner: String, currency: String, amount: int) -> void:
	_open_wallet(s,owner,currency)
	_post(s,_id(s,"opening-wallet"),[_line(_cash_id(owner,currency),amount),_line(owner+":equity:"+currency,-amount)],"개시 외화 자산",0)

static func _owner_city(s: Dictionary, owner: String) -> String:
	for key in ["companies","people"]:
		var entity: Dictionary = _find(s.get(key,[]),owner)
		if not entity.is_empty(): return str(entity.get("city_id",""))
	if s.get("public") is Dictionary:
		for key in ["governments","parties"]:
			var entity: Dictionary = _find(s.public.get(key,[]),owner)
			if not entity.is_empty(): return str(entity.get("city_id",""))
	for key in ["banks","insurers","market_makers"]:
		var entity: Dictionary = _find(s.get("finance",{}).get(key,[]),owner)
		if not entity.is_empty(): return str(entity.get("city_id",""))
	return ""

static func _owner(s: Dictionary, c: Dictionary) -> String:
	return str(c.get("acting_company_id","")) if str(c.get("acting_company_id","")).length() > 0 else str(c.get("actor_id",""))

static func _authorized(s: Dictionary, c: Dictionary, owner: String) -> bool:
	var actor: String = str(c.get("actor_id",""))
	if _find(s.get("people",[]),actor).is_empty(): return false
	if actor == owner: return true
	var company: Dictionary = _find(s.get("companies",[]),owner)
	if not company.is_empty():
		if company.get("status","active") not in ["active","liquidating"]: return false
		if company.get("director_id","") == actor: return true
		if ResourceLoader.exists("res://core/lifecycle.gd"):
			var life: Variant=load("res://core/lifecycle.gd")
			var amount: int=0
			for reservation in planning_reservations(s,c):
				if reservation.resource_type=="cash" and reservation.owner_id==owner and reservation.resource_id=="USD": amount+=int(reservation.quantity)
			if life!=null and life.has_method("has_delegated_permission") and life.has_delegated_permission(s,actor,owner,str(c.type),amount): return true
		return false
	for office in s.get("public",{}).get("offices",[]):
		if office.get("government_id","") == owner and office.get("person_id","") == actor: return true
	return false

static func initialize(s: Dictionary) -> void:
	if s.get("finance") is Dictionary and s.finance.get("initialized",false): return
	var d: Dictionary = definition()
	if d.is_empty(): return
	s["finance"] = {"initialized":true,"version":2,"base_currency":Ledger.BASE_CURRENCY,"minor_units_per_major":Ledger.MINOR_UNITS_PER_MAJOR,"currencies":d.currencies.duplicate(true),"banks":[],"central_banks":[],"insurers":[],"market_makers":[],"owner_bank":{},"loans":[],"bonds":[],"policies":[],"claims":[],"share_offers":[],"equity_issues":[],"capital_mandates":[],"applied_commands":{},"last_serviced_month":0,"notices":[]}
	if not s.has("ownership"): s.ownership = []
	for company in s.companies:
		var exists: bool = false
		for holding in s.ownership:
			if holding.company_id == company.id: exists = true
		if not exists:
			s.ownership.append({"company_id":company.id,"owner_id":company.get("shareholder_id",company.director_id),"shares":int(d.initial_shares),"voting_shares":int(d.initial_shares)})
	for nation in s.nations:
		var city: Dictionary = {}
		for candidate in s.cities:
			if candidate.nation_id == nation.id:
				city = candidate
				break
		if city.is_empty(): continue
		var bank_id: String = "bank-" + str(nation.id)
		var cb_id: String = "central-bank-" + str(nation.id)
		var insurer_id: String = "insurer-" + str(nation.id)
		var maker_id: String = "fx-" + str(nation.id)
		var bank: Dictionary = {"id":bank_id,"name":str(nation.name)+" 은행","nation_id":nation.id,"city_id":city.id,"central_bank_id":cb_id,"currency":"USD","capital":int(d.bank_capital),"cb_borrowing":0,"status":"active"}
		s.finance.banks.append(bank)
		s.finance.central_banks.append({"id":cb_id,"name":str(nation.name)+" 중앙은행","nation_id":nation.id,"city_id":city.id,"issued":{}})
		s.finance.insurers.append({"id":insurer_id,"name":str(nation.name)+" 보험","nation_id":nation.id,"city_id":city.id})
		s.finance.market_makers.append({"id":maker_id,"name":str(nation.name)+" 외환중개","nation_id":nation.id,"city_id":city.id})
		Ledger.opening(s,bank_id,int(d.bank_capital),0)
		Ledger.opening(s,insurer_id,int(d.insurer_capital),0)
		Ledger.opening(s,maker_id,int(d.market_cash),0)
		for currency in s.finance.currencies:
			_account(s,bank_id,"reserve:"+str(currency.id),"asset",currency.id)
			_account(s,cb_id,"issued:"+str(currency.id),"liability",currency.id)
			_account(s,cb_id,"sovereign_backing:"+str(currency.id),"asset",currency.id)
			if currency.id != "USD": _opening_wallet(s,maker_id,str(currency.id),int(d.foreign_float))
	var owners: Array = []
	for key in ["people","companies"]:
		for entity in s.get(key,[]): owners.append(str(entity.id))
	for entity in s.get("public",{}).get("governments",[]): owners.append(str(entity.id))
	for entity in s.get("public",{}).get("parties",[]): owners.append(str(entity.id))
	for key in ["insurers","market_makers"]:
		for entity in s.finance[key]: owners.append(str(entity.id))
	owners.sort()
	for owner in owners:
		var city: String = _owner_city(s,owner)
		var city_data: Dictionary = _find(s.cities,city)
		if city_data.is_empty(): continue
		var bank_id: String = "bank-"+str(city_data.nation_id)
		s.finance.owner_bank[owner] = bank_id
		for currency in s.finance.currencies:
			var amount: int = cash(s,owner,str(currency.id))
			var deposit: String = _account(s,bank_id,"deposit:"+owner+":"+str(currency.id),"liability",currency.id)
			if amount > 0:
				_post(s,_id(s,"opening-deposit"),[_line(bank_id+":reserve:"+str(currency.id),amount),_line(deposit,-amount)],"개시 예금과 결제 준비자산",0)
	# Central-bank reserves have an explicit public-debt counterpart. This
	# records the one-time world opening, never an operating bailout.
	for bank in s.finance.banks:
		var gov: String = "gov-nation-"+str(bank.nation_id)
		for currency in s.finance.currencies:
			var cur: String = str(currency.id)
			var backing: int = Ledger.balance(s,bank.id+":reserve:"+cur) + (cash(s,bank.id) if cur=="USD" else 0)
			if backing <= 0: continue
			var cb: Dictionary = _find(s.finance.central_banks,str(bank.central_bank_id))
			var gov_debt: String = _account(s,gov,"opening_sovereign_debt:"+cur,"liability",cur)
			var distribution: String = _account(s,gov,"opening_distribution:"+cur,"expense",cur)
			_post(s,_id(s,"opening-monetary-backing"),[_line(cb.id+":sovereign_backing:"+cur,backing),_line(cb.id+":issued:"+cur,-backing),_line(distribution,backing),_line(gov_debt,-backing)],"개시 발행·중앙은행 자산·정부 대응채무",0)
			cb.issued[cur] = backing
	# Opening inventories are real corporate assets. Shares are ownership
	# claims recorded by their existing owners; they never duplicate cash.
	var quantities: Dictionary={}
	var opening_values: Dictionary={}
	for company in s.companies: opening_values[company.id]=cash(s,company.id)
	for account_id in s.accounts:
		var account: Dictionary=s.accounts[account_id]
		if account.kind=="asset" and ":land:" in str(account_id) and opening_values.has(account.owner): opening_values[account.owner]=int(opening_values[account.owner])+int(account.balance)
	for lot in s.get("inventory",[]):
		if int(lot.quantity)<=0: continue
		if not quantities.has(lot.owner_id): quantities[lot.owner_id]={}
		quantities[lot.owner_id][lot.product_id]=int(quantities[lot.owner_id].get(lot.product_id,0))+int(lot.quantity)
	for owner in _keys(quantities):
		for product_id in _keys(quantities[owner]):
			var product: Dictionary=_find(s.products,product_id)
			var value: int=int(quantities[owner][product_id])*int(product.price)
			if value<=0: continue
			_post(s,_id(s,"opening-stock"),[_line(_stock_account(s,owner,product_id),value),_line(_account(s,owner,"opening_stock_equity","equity"),-value)],"개시 재고 원가",0)
			if opening_values.has(owner): opening_values[owner]=int(opening_values[owner])+value
	for facility in s.get("facilities",[]):
		var owner: String=str(facility.get("company_id",facility.get("owner_id","")))
		if owner=="": continue
		var value: int=int(facility.get("opening_value",d.opening_facility_value))
		var asset_id: String=str(facility.get("project_id",facility.id))
		if value>0:
			_post(s,_id(s,"opening-facility"),[_line(_account(s,owner,"facility:"+asset_id,"asset"),value),_line(_account(s,owner,"opening_facility_equity","equity"),-value)],"개시 설비·건물 자산",0)
			if opening_values.has(owner): opening_values[owner]=int(opening_values[owner])+value
	for company in s.companies:
		var equity_value: int = int(opening_values.get(company.id,cash(s,company.id)))
		var total: int = shares_outstanding(s,company.id)
		for holding in s.ownership:
			if holding.company_id == company.id and total > 0:
				var value: int = int(equity_value * int(holding.shares) / total)
				if value > 0:
					_post(s,_id(s,"opening-equity-claim"),[_line(_investment(s,holding.owner_id,company.id),value),_line(_account(s,holding.owner_id,"opening_investment_equity","equity"),-value)],"개시 지분 소유권",0)

static func _stage(s: Dictionary) -> Dictionary:
	var work: Dictionary = s.duplicate()
	for key in ["accounts","finance","ownership"]: work[key] = s[key].duplicate(true)
	for key in ["journal","events"]: work[key] = s[key].duplicate()
	work["journal_index"] = s.get("journal_index",{}).duplicate()
	return work

static func _commit(s: Dictionary, work: Dictionary) -> void:
	for key in ["accounts","finance","ownership","journal","journal_index","events"]: s[key] = work[key]

static func command_types() -> Array:
	return ["finance_borrow","finance_repay","finance_deposit","finance_withdraw","finance_issue_shares","finance_offer_shares","finance_offer_equity","finance_subscribe_equity","finance_trade_shares","finance_dividend","finance_authorize_capital","finance_cancel_offer","finance_issue_bond","finance_buy_bond","finance_insure","finance_claim","finance_exchange"]

static func planning_reservations(s: Dictionary, c: Dictionary) -> Array:
	if not c.get("payload") is Dictionary: return []
	var p: Dictionary=c.payload
	var owner: String=_owner(s,c)
	var type: String=str(c.get("type",""))
	var amount: int=int(p.get("amount",0)) if _integer(p.get("amount",0)) else 0
	var shares: int=int(p.get("shares",0)) if _integer(p.get("shares",0)) else 0
	var price: int=int(p.get("unit_price",0)) if _integer(p.get("unit_price",0)) else 0
	var cash_cost: int=0
	var cash_owner: String=owner
	var currency: String="USD"
	var reservations: Array=[]
	match type:
		"finance_repay","finance_deposit","finance_dividend","finance_buy_bond": cash_cost=amount
		"finance_issue_shares":
			if str(p.get("investor_id",""))==str(c.get("actor_id","")):
				cash_owner=str(c.actor_id); cash_cost=shares*price
		"finance_trade_shares": cash_cost=shares*price
		"finance_subscribe_equity":
			var issue: Dictionary=_find(s.get("finance",{}).get("equity_issues",[]),str(p.get("issue_id","")))
			cash_cost=shares*int(issue.get("unit_price",0))
		"finance_insure":
			if _integer(p.get("coverage")) and _integer(p.get("term_months")): cash_cost=_premium(int(p.coverage),int(p.term_months))
		"finance_exchange":
			currency=str(p.get("from_currency","USD"))
			if amount>0: cash_cost=amount+_fx_fee(amount)
		"finance_offer_shares":
			if shares>0: reservations.append({"owner_id":owner,"resource_type":"shares","resource_id":str(p.get("company_id","")),"quantity":shares})
	if cash_cost>0: reservations.append({"owner_id":cash_owner,"resource_type":"cash","resource_id":currency,"quantity":cash_cost})
	return reservations

static func validate_command(s: Dictionary, c: Dictionary) -> String:
	if not s.get("finance") is Dictionary: return "FINANCE_NOT_INITIALIZED"
	if s.finance.get("applied_commands",{}).has(str(c.get("id",""))): return "DUPLICATE_FINANCE_COMMAND"
	var owner: String = _owner(s,c)
	if not c.get("payload") is Dictionary: return "INVALID_FINANCE_PAYLOAD"
	var p: Dictionary = c.payload
	if str(c.get("type",""))=="finance_authorize_capital":
		if not capital_authority(s,str(c.get("actor_id","")),str(p.get("company_id",""))): return "CONTROLLING_VOTE_REQUIRED"
	elif not _authorized(s,c,owner): return "FINANCE_AUTHORITY_REQUIRED"
	var d: Dictionary = definition()
	for key in ["amount","shares","unit_price","coverage","term_months","interest_bps"]:
		if p.has(key) and (not _integer(p[key]) or int(p[key]) <= 0 or int(p[key]) > int(d.maximum_transaction)): return "INVALID_FINANCE_INTEGER:"+key
	if p.has("term_months") and int(p.term_months) > int(d.max_term_months): return "TERM_LIMIT"
	var type: String = str(c.type)
	if type not in command_types(): return "UNKNOWN_FINANCE_COMMAND"
	match type:
		"finance_borrow":
			var bank: Dictionary = _find(s.finance.banks,str(p.get("bank_id","")))
			if bank.is_empty() or not p.has("amount") or not p.has("term_months"): return "BANK_OR_LOAN_TERMS_MISSING"
			if bank.status != "active": return "BANK_LENDING_SUSPENDED"
			var principal: int = debt_total(s,owner)
			if int(p.amount)+principal > _credit_capacity(s,owner): return "BORROWER_CREDIT_LIMIT"
			if int(p.amount)+_bank_loans(s,bank.id) > int(bank.capital)*10000/int(d.capital_ratio_bps): return "BANK_CAPITAL_LIMIT"
			if int(p.amount) > Ledger.balance(s,bank.id+":reserve:USD")+cash(s,bank.id)+int(d.cb_credit_limit)-int(bank.cb_borrowing): return "BANK_LIQUIDITY_LIMIT"
		"finance_repay":
			var loan: Dictionary = _find(s.finance.loans,str(p.get("loan_id","")))
			if loan.is_empty() or loan.borrower_id != owner or int(loan.principal)+int(loan.arrears)<=0: return "LOAN_NOT_OWNED"
			if not p.has("amount") or int(p.amount)>int(loan.principal)+int(loan.arrears): return "REPAYMENT_EXCEEDS_DEBT"
			if cash(s,owner)<int(p.amount): return "INSUFFICIENT_CASH"
		"finance_deposit":
			if _find(s.finance.banks,str(p.get("bank_id",""))).is_empty() or not p.has("amount"): return "BANK_OR_AMOUNT_MISSING"
			if cash(s,owner)<int(p.amount): return "INSUFFICIENT_CASH"
		"finance_withdraw":
			var bank: Dictionary = _find(s.finance.banks,str(p.get("bank_id","")))
			if bank.is_empty() or not p.has("amount"): return "BANK_OR_AMOUNT_MISSING"
			if Ledger.balance(s,_savings_id(owner,bank.id))<int(p.amount): return "INSUFFICIENT_SAVINGS"
			if _liquidity(s,bank.id)<int(p.amount): return "BANK_LIQUIDITY_LIMIT"
		"finance_issue_shares":
			if _find(s.companies,owner).is_empty(): return "CORPORATION_REQUIRED"
			if not _capital_allowed(s,c,owner): return "CAPITAL_RESOLUTION_REQUIRED"
			var investor: String = str(p.get("investor_id",""))
			if investor == owner or _owner_city(s,investor)=="" or not p.has("shares") or not p.has("unit_price"): return "INVALID_SUBSCRIBER"
			# Acceptance is explicit: issuer may invite itself only through its
			# representative's own account, or a pre-approved investor mandate.
			if investor != str(c.actor_id) and not _mandate(s,investor,owner): return "SUBSCRIBER_CONSENT_REQUIRED"
			if cash(s,investor)<int(p.shares)*int(p.unit_price): return "SUBSCRIBER_CASH_REQUIRED"
		"finance_offer_shares":
			var company: String=str(p.get("company_id",""))
			if not p.has("shares") or not p.has("unit_price") or not p.has("term_months"): return "SHARE_OFFER_TERMS_MISSING"
			var reserved: int=0
			for offer in s.finance.share_offers:
				if offer.owner_id==owner and offer.company_id==company and int(offer.expiry_month)>=int(s.planning_month): reserved+=int(offer.remaining)
			if shares_owned(s,owner,company)<int(p.shares)+reserved: return "SHARES_ALREADY_RESERVED_OR_MISSING"
		"finance_offer_equity":
			if _find(s.companies,owner).is_empty() or not p.has("shares") or not p.has("unit_price") or not p.has("term_months"): return "CORPORATE_EQUITY_TERMS_REQUIRED"
			if not _capital_allowed(s,c,owner): return "CAPITAL_RESOLUTION_REQUIRED"
		"finance_subscribe_equity":
			var issue: Dictionary=_find(s.finance.equity_issues,str(p.get("issue_id","")))
			if issue.is_empty() or issue.issuer_id==owner or not p.has("shares"): return "EQUITY_ISSUE_UNAVAILABLE"
			if int(issue.expiry_month)<int(s.planning_month) or int(issue.remaining)<int(p.shares): return "EQUITY_ISSUE_EXPIRED_OR_FILLED"
			if cash(s,owner)<int(p.shares)*int(issue.unit_price): return "INSUFFICIENT_CASH"
		"finance_trade_shares":
			var seller: String = str(p.get("seller_id",""))
			var company: String = str(p.get("company_id",""))
			if seller == owner or not p.has("shares") or not p.has("unit_price"): return "INVALID_SHARE_TRADE"
			if shares_owned(s,seller,company)<int(p.shares): return "SELLER_SHARES_REQUIRED"
			if _share_offer(s,seller,company,int(p.shares),int(p.unit_price)).is_empty(): return "SELLER_CONSENT_REQUIRED"
			if cash(s,owner)<int(p.shares)*int(p.unit_price): return "INSUFFICIENT_CASH"
		"finance_dividend":
			if _find(s.companies,owner).is_empty() or not p.has("amount") or shares_outstanding(s,owner)<=0: return "INVALID_DIVIDEND"
			if not _capital_allowed(s,c,owner): return "CAPITAL_RESOLUTION_REQUIRED"
			if cash(s,owner)<int(p.amount): return "INSUFFICIENT_CASH"
			if int(p.amount)>maxi(0,retained_earnings(s,owner)): return "DISTRIBUTABLE_PROFIT_REQUIRED"
		"finance_authorize_capital":
			var target: Dictionary=_find(s.companies,str(p.get("company_id","")))
			var person: Dictionary=_find(s.people,str(p.get("person_id","")))
			if target.is_empty() or person.is_empty() or str(p.get("mode","")) not in ["equity","dividend"] or not p.has("term_months"): return "INVALID_CAPITAL_MANDATE"
			if p.mode=="equity" and (not p.has("shares") or not p.has("unit_price")): return "SHARE_AUTHORIZATION_TERMS_MISSING"
			if p.mode=="dividend" and not p.has("amount"): return "DIVIDEND_AUTHORIZATION_AMOUNT_REQUIRED"
		"finance_cancel_offer":
			var offer: Dictionary=_find(s.finance.share_offers,str(p.get("offer_id","")))
			if offer.is_empty(): offer=_find(s.finance.equity_issues,str(p.get("offer_id","")))
			if offer.is_empty() or str(offer.get("owner_id",offer.get("issuer_id","")))!=owner: return "OWN_OFFER_REQUIRED"
		"finance_issue_bond":
			if not p.has("amount") or not p.has("term_months") or not p.has("interest_bps"): return "BOND_TERMS_MISSING"
			if int(p.interest_bps)>1000: return "BOND_INTEREST_LIMIT"
		"finance_buy_bond":
			var bond: Dictionary = _find(s.finance.bonds,str(p.get("bond_id","")))
			if bond.is_empty() or bond.issuer_id==owner or not p.has("amount"): return "BOND_NOT_AVAILABLE"
			if int(bond.face)-int(bond.subscribed)<int(p.amount): return "BOND_FULLY_SUBSCRIBED"
			if cash(s,owner)<int(p.amount): return "INSUFFICIENT_CASH"
		"finance_insure":
			var facility: Dictionary = _find(s.facilities,str(p.get("asset_id","")))
			if facility.is_empty() or facility.company_id!=owner or not p.has("coverage") or not p.has("term_months"): return "INSURABLE_OWNED_ASSET_REQUIRED"
			var premium: int = _premium(int(p.coverage),int(p.term_months))
			if cash(s,owner)<premium: return "INSUFFICIENT_PREMIUM_CASH"
		"finance_claim":
			var policy: Dictionary = _find(s.finance.policies,str(p.get("policy_id","")))
			var loss: Dictionary = _find(s.events,str(p.get("loss_event_id","")))
			if policy.is_empty() or policy.owner_id!=owner or policy.status not in ["active","expired"]: return "ACTIVE_OWN_POLICY_REQUIRED"
			if loss.is_empty() or loss.actor_id!=owner or loss.type not in d.insurable_events: return "VERIFIED_LOSS_EVENT_REQUIRED"
			if int(loss.month)<int(policy.start_month) or int(loss.month)>int(policy.end_month): return "LOSS_OUTSIDE_COVER"
			if int(s.planning_month)>int(policy.end_month)+int(d.claim_grace_months): return "CLAIM_WINDOW_EXPIRED"
			if str(loss.get("city_id",""))!=str(policy.city_id): return "LOSS_LOCATION_NOT_COVERED"
			if loss.has("facility_id") and loss.facility_id!=policy.asset_id: return "OTHER_ASSET_LOSS_NOT_COVERED"
			if int(_loss_value(s,loss))<=0: return "LOSS_HAS_NO_INSURED_VALUE"
			for claim in s.finance.claims:
				if claim.loss_event_id==loss.id: return "LOSS_ALREADY_CLAIMED"
		"finance_exchange":
			var from: String = str(p.get("from_currency",""))
			var to: String = str(p.get("to_currency",""))
			if from==to or _currency(s,from).is_empty() or _currency(s,to).is_empty() or not p.has("amount"): return "INVALID_CURRENCY_PAIR"
			if cash(s,owner,from)<int(p.amount)+_fx_fee(int(p.amount)): return "INSUFFICIENT_FX_SOURCE"
			var maker: Dictionary = _maker(s,owner)
			if maker.is_empty() or cash(s,maker.id,to)<_convert(s,int(p.amount),from,to): return "FINITE_FX_LIQUIDITY_REQUIRED"
	return ""

static func apply(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var error: String = validate_command(s,c)
	if error != "": return {"ok":false,"error":error}
	# Announcements reserve contractual choices, never cash or bank deposits.
	# Stage only records these branches can mutate; all settlement branches
	# below retain full ledger staging and counterpart reconciliation.
	if str(c.type) in ["finance_offer_shares","finance_offer_equity","finance_authorize_capital","finance_cancel_offer","finance_issue_bond"]:
		var offer_work: Dictionary=s.duplicate()
		offer_work.finance=s.finance.duplicate(true)
		offer_work.events=s.events.duplicate()
		var offered: Dictionary=_execute(offer_work,c,month)
		if not offered.get("ok",false): return offered
		_consume_capital_mandate(offer_work,c)
		offer_work.finance.applied_commands[str(c.id)]=month
		s.finance=offer_work.finance
		s.events=offer_work.events
		return offered
	var work: Dictionary = _stage(s)
	register_owner(work,_owner(s,c))
	var result: Dictionary = _execute(work,c,month)
	if not result.get("ok",false): return result
	_consume_capital_mandate(work,c)
	reconcile_deposits(work,month)
	work.finance.applied_commands[str(c.id)]=month
	_commit(s,work)
	return result

static func _execute(s: Dictionary, c: Dictionary, month: int) -> Dictionary:
	var owner: String = _owner(s,c)
	var p: Dictionary = c.payload
	var type: String = str(c.type)
	var id: String = "finance-"+str(c.id)
	var result: Dictionary = {"ok":true,"error":""}
	match type:
		"finance_borrow":
			var bank: Dictionary = _find(s.finance.banks,str(p.bank_id))
			var loan_id: String = "loan-"+str(c.id)
			var amount: int = int(p.amount)
			var bank_asset: String = _account(s,bank.id,"loan:"+loan_id,"asset")
			var borrower_debt: String = _account(s,owner,"loan:"+loan_id,"liability")
			var deposit: String = _account(s,bank.id,"deposit:"+owner+":USD","liability")
			result = _post(s,id,[_line(bank_asset,amount),_line(deposit,-amount),_line(owner+":cash",amount),_line(borrower_debt,-amount)],"대출 실행·대응 예금과 부채",month)
			if result.ok:
				s.finance.loans.append({"id":loan_id,"bank_id":bank.id,"borrower_id":owner,"currency":"USD","principal":amount,"original_principal":amount,"interest_bps":int(bank.get("loan_interest_bps",definition().loan_interest_bps)),"term_months":int(p.term_months),"start_month":month,"due_month":month+int(p.term_months)-1,"arrears":0,"missed_payments":0,"status":"active","provision":0})
				_event(s,"money_creation","은행 대출의 대응 예금 발행",month,{"actor_id":owner,"city_id":_owner_city(s,owner),"currency":"USD","amount":amount,"loan_id":loan_id,"journal_id":id})
				# A foreign bank remits the created deposit to the customer's
				# existing settlement bank; it does not duplicate that deposit.
				var primary: String=str(s.finance.owner_bank.get(owner,bank.id))
				if primary!=str(bank.id):
					_post(s,id+"-interbank",[_line(deposit,amount),_line(bank.id+":reserve:USD",-amount),_line(primary+":reserve:USD",amount),_line(_account(s,primary,"deposit:"+owner+":USD","liability"),-amount)],"타행 대출 예금의 은행 간 결제",month)
		"finance_repay": result = _repay(s,_find(s.finance.loans,str(p.loan_id)),int(p.amount),month,id)
		"finance_deposit": result = _savings(s,owner,str(p.bank_id),int(p.amount),false,month,id)
		"finance_withdraw": result = _savings(s,owner,str(p.bank_id),int(p.amount),true,month,id)
		"finance_issue_shares":
			var investor: String = str(p.investor_id)
			var amount: int = int(p.shares)*int(p.unit_price)
			result = _post(s,id,[_line(_investment(s,investor,owner),amount),_line(investor+":cash",-amount),_line(owner+":cash",amount),_line(_account(s,owner,"issued_equity","equity"),-amount)],"新株 인수·출자 (영업매출 아님)",month)
			if result.ok: _add_shares(s,investor,owner,int(p.shares))
		"finance_offer_shares":
			s.finance.share_offers.append({"id":"share-offer-"+str(c.id),"owner_id":owner,"company_id":p.company_id,"remaining":int(p.shares),"unit_price":int(p.unit_price),"expiry_month":month+int(p.term_months)})
		"finance_offer_equity":
			s.finance.equity_issues.append({"id":"equity-issue-"+str(c.id),"issuer_id":owner,"remaining":int(p.shares),"unit_price":int(p.unit_price),"expiry_month":month+int(p.term_months)})
		"finance_authorize_capital":
			s.finance.capital_mandates.append({"id":"capital-"+str(c.id),"company_id":p.company_id,"person_id":p.person_id,"grantor_id":c.actor_id,"mode":p.mode,"remaining_shares":int(p.get("shares",0)),"minimum_price":int(p.get("unit_price",0)),"remaining_amount":int(p.get("amount",0)),"expiry_month":month+int(p.term_months)})
		"finance_cancel_offer":
			var offer: Dictionary=_find(s.finance.share_offers,str(p.offer_id))
			if offer.is_empty(): offer=_find(s.finance.equity_issues,str(p.offer_id))
			offer.remaining=0
		"finance_subscribe_equity":
			var issue: Dictionary=_find(s.finance.equity_issues,str(p.issue_id))
			var amount: int=int(p.shares)*int(issue.unit_price)
			result=_post(s,id,[_line(owner+":cash",-amount),_line(_investment(s,owner,issue.issuer_id),amount),_line(issue.issuer_id+":cash",amount),_line(_account(s,issue.issuer_id,"issued_equity","equity"),-amount)],"투자자 동의 청약·실제 신주 대금 납입",month)
			if result.ok:
				issue.remaining=int(issue.remaining)-int(p.shares)
				_add_shares(s,owner,issue.issuer_id,int(p.shares))
		"finance_trade_shares": result = _share_trade(s,owner,str(p.seller_id),str(p.company_id),int(p.shares),int(p.unit_price),month,id)
		"finance_dividend": result = distribute_dividend(s,owner,int(p.amount),month,id)
		"finance_issue_bond":
			s.finance.bonds.append({"id":"bond-"+str(c.id),"issuer_id":owner,"currency":"USD","face":int(p.amount),"subscribed":0,"outstanding":0,"interest_bps":int(p.interest_bps),"start_month":month,"due_month":month+int(p.term_months),"status":"offered","holders":[],"arrears":0})
		"finance_buy_bond":
			var bond: Dictionary = _find(s.finance.bonds,str(p.bond_id))
			var amount: int = int(p.amount)
			result = _post(s,id,[_line(_account(s,owner,"bond:"+str(bond.id),"asset"),amount),_line(owner+":cash",-amount),_line(bond.issuer_id+":cash",amount),_line(_account(s,bond.issuer_id,"bond:"+str(bond.id),"liability"),-amount)],"채권 1차 인수·대응 발행부채",month)
			if result.ok:
				var holding: Dictionary=_bond_holding(bond,owner)
				holding.principal=int(holding.principal)+amount
				bond.subscribed = int(bond.subscribed)+amount
				bond.outstanding = int(bond.outstanding)+amount
				bond.status = "active"
		"finance_insure": result = _insure(s,owner,p,month,id)
		"finance_claim": result = _claim(s,owner,p,month,id)
		"finance_exchange": result = _exchange(s,owner,p,month,id)
	if result.ok:
		_event(s,"finance",_command_label(type),month,{"actor_id":owner,"city_id":_owner_city(s,owner),"command_id":c.id,"amount":int(p.get("amount",0))})
	return result

static func _bank_loans(s: Dictionary, bank_id: String) -> int:
	var total: int = 0
	for loan in s.finance.loans:
		if loan.bank_id==bank_id: total += int(loan.principal)-int(loan.provision)
	return total

static func _credit_capacity(s: Dictionary, owner: String) -> int:
	var d: Dictionary=definition()
	var assets: int=0
	for account_id in s.accounts:
		var account: Dictionary=s.accounts[account_id]
		if account.owner==owner and account.kind=="asset" and account.currency=="USD": assets+=maxi(0,int(account.balance))
	var person: Dictionary=_find(s.people,owner)
	var income: int=int(person.get("last_income",0))
	return mini(int(d.loan_limit),cash(s,owner)*int(d.credit_cash_multiplier)+int(assets*int(d.credit_asset_haircut_bps)/10000)+income*int(d.credit_income_multiple))

static func _liquidity(s: Dictionary, bank_id: String) -> int:
	return cash(s,bank_id)+Ledger.balance(s,bank_id+":reserve:USD")

static func _savings_id(owner: String, bank: String) -> String:
	return owner+":savings:"+bank

static func _savings(s: Dictionary, owner: String, bank_id: String, amount: int, withdrawing: bool, month: int, id: String) -> Dictionary:
	var primary: String = str(s.finance.owner_bank.get(owner,bank_id))
	var saving: String = _account(s,owner,"savings:"+bank_id,"asset")
	var liability: String = _account(s,bank_id,"savings:"+owner,"liability")
	var primary_deposit: String = _account(s,primary,"deposit:"+owner+":USD","liability")
	var direction: int = -1 if withdrawing else 1
	var lines: Array = [_line(saving,direction*amount),_line(owner+":cash",-direction*amount),_line(liability,-direction*amount),_line(primary_deposit,direction*amount)]
	if bank_id != primary:
		lines.append(_line(bank_id+":reserve:USD",direction*amount))
		lines.append(_line(primary+":reserve:USD",-direction*amount))
	return _post(s,id,lines,"정기예금 인출" if withdrawing else "정기예금 예치",month)

static func _repay(s: Dictionary, loan: Dictionary, amount: int, month: int, id: String) -> Dictionary:
	var owner: String = str(loan.borrower_id)
	var bank: String = str(loan.bank_id)
	var interest: int = mini(amount,int(loan.arrears))
	var principal: int = mini(amount-interest,int(loan.principal))
	var lines: Array = []
	if interest>0:
		lines.append_array([_line(_account(s,owner,"interest_payable:"+str(loan.id),"liability"),interest),_line(owner+":cash",-interest),_line(bank+":cash",interest),_line(_account(s,bank,"interest_receivable:"+str(loan.id),"asset"),-interest)])
	if principal>0:
		var primary: String=str(s.finance.owner_bank.get(owner,bank))
		lines.append_array([_line(owner+":loan:"+str(loan.id),principal),_line(owner+":cash",-principal),_line(bank+":loan:"+str(loan.id),-principal)])
		if primary==bank: lines.append(_line(_account(s,bank,"deposit:"+owner+":USD","liability"),principal))
		else: lines.append_array([_line(bank+":reserve:USD",principal),_line(_account(s,primary,"deposit:"+owner+":USD","liability"),principal),_line(primary+":reserve:USD",-principal)])
	var recovery: int=mini(principal,int(loan.provision))
	if recovery>0: lines.append_array([_line(bank+":provision:"+str(loan.id),recovery),_line(_account(s,bank,"credit_loss_recovery","revenue"),-recovery)])
	var result: Dictionary = _post(s,id,lines,"대출 원금 소멸·연체이자 정산",month)
	if result.ok:
		loan.principal = int(loan.principal)-principal
		loan.arrears = int(loan.arrears)-interest
		loan.provision=int(loan.provision)-recovery
		if principal>0: _event(s,"money_destruction","대출 원금과 대응 예금 소멸",month,{"actor_id":owner,"city_id":_owner_city(s,owner),"currency":"USD","amount":principal,"loan_id":loan.id,"journal_id":id})
		if int(loan.principal)==0 and int(loan.arrears)==0: loan.status="repaid"
	return result

static func shares_outstanding(s: Dictionary, company: String) -> int:
	var total: int = 0
	for holding in s.get("ownership",[]):
		if holding.company_id==company: total+=int(holding.shares)
	return total

static func shares_owned(s: Dictionary, owner: String, company: String) -> int:
	var total: int = 0
	for holding in s.get("ownership",[]):
		if holding.company_id==company and holding.owner_id==owner: total+=int(holding.shares)
	return total

static func available_shares(s: Dictionary, owner: String, company: String) -> int:
	var total: int=shares_owned(s,owner,company)
	for offer in s.get("finance",{}).get("share_offers",[]):
		if offer.owner_id==owner and offer.company_id==company and int(offer.expiry_month)>=int(s.planning_month): total-=int(offer.remaining)
	return maxi(0,total)

static func capital_authority(s: Dictionary, actor: String, company: String) -> bool:
	var votes: int=0
	var all_votes: int=0
	for holding in s.get("ownership",[]):
		if holding.company_id!=company: continue
		all_votes+=int(holding.voting_shares)
		var represented: bool=holding.owner_id==actor
		var parent: Dictionary=_find(s.companies,str(holding.owner_id))
		if not parent.is_empty() and parent.get("director_id","")==actor: represented=true
		for office in s.get("public",{}).get("offices",[]):
			if office.government_id==holding.owner_id and office.person_id==actor: represented=true
		if represented: votes+=int(holding.voting_shares)
	return all_votes>0 and votes*2>all_votes

static func _capital_mandate(s: Dictionary, c: Dictionary, company: String) -> Dictionary:
	var p: Dictionary=c.get("payload",{})
	var mode: String="dividend" if c.get("type")=="finance_dividend" else "equity"
	for mandate in s.finance.get("capital_mandates",[]):
		if mandate.company_id!=company or mandate.person_id!=c.actor_id or mandate.mode!=mode or int(mandate.expiry_month)<int(s.planning_month): continue
		if mode=="dividend" and int(mandate.remaining_amount)>=int(p.get("amount",0)): return mandate
		if mode=="equity" and int(mandate.remaining_shares)>=int(p.get("shares",0)) and int(p.get("unit_price",0))>=int(mandate.minimum_price): return mandate
	return {}

static func _capital_allowed(s: Dictionary, c: Dictionary, company: String) -> bool:
	return capital_authority(s,str(c.actor_id),company) or not _capital_mandate(s,c,company).is_empty()

static func _consume_capital_mandate(s: Dictionary, c: Dictionary) -> void:
	if str(c.type) not in ["finance_issue_shares","finance_offer_equity","finance_dividend"]: return
	var company: String=_owner(s,c)
	if capital_authority(s,str(c.actor_id),company): return
	var mandate: Dictionary=_capital_mandate(s,c,company)
	if mandate.is_empty(): return
	if mandate.mode=="dividend": mandate.remaining_amount=int(mandate.remaining_amount)-int(c.payload.amount)
	else: mandate.remaining_shares=int(mandate.remaining_shares)-int(c.payload.shares)

static func _add_shares(s: Dictionary, owner: String, company: String, amount: int) -> void:
	for holding in s.ownership:
		if holding.company_id==company and holding.owner_id==owner:
			holding.shares=int(holding.shares)+amount
			holding.voting_shares=int(holding.voting_shares)+amount
			return
	s.ownership.append({"company_id":company,"owner_id":owner,"shares":amount,"voting_shares":amount})

static func _investment(s: Dictionary, owner: String, company: String) -> String:
	return _account(s,owner,"investment:"+company,"asset")

static func _mandate(s: Dictionary, owner: String, company: String) -> bool:
	var entity: Dictionary = _find(s.get("people",[]),owner)
	if entity.is_empty(): entity=_find(s.get("companies",[]),owner)
	return bool(entity.get("investment_policy",{}).get("allow_share_trades",false)) or company in entity.get("investment_policy",{}).get("approved_companies",[])

static func _share_offer(s: Dictionary, owner: String, company: String, quantity: int, price: int) -> Dictionary:
	for offer in s.finance.share_offers:
		if offer.owner_id==owner and offer.company_id==company and int(offer.remaining)>=quantity and int(offer.unit_price)==price and int(offer.expiry_month)>=int(s.planning_month): return offer
	return {}

static func _share_trade(s: Dictionary, buyer: String, seller: String, company: String, shares: int, price: int, month: int, id: String) -> Dictionary:
	var amount: int = shares*price
	var holding: int = shares_owned(s,seller,company)
	var cost: int = int(Ledger.balance(s,_investment(s,seller,company))*shares/holding)
	var difference: int = amount-cost
	var lines: Array = [_line(buyer+":cash",-amount),_line(_investment(s,buyer,company),amount),_line(seller+":cash",amount),_line(_investment(s,seller,company),-cost)]
	if difference>0: lines.append(_line(_account(s,seller,"investment_gain","revenue"),-difference))
	elif difference<0: lines.append(_line(_account(s,seller,"investment_loss","expense"),-difference))
	var result: Dictionary = _post(s,id,lines,"기존 주식 매매·매도자에게 지급",month)
	if result.ok:
		var offer: Dictionary=_share_offer(s,seller,company,shares,price)
		offer.remaining=int(offer.remaining)-shares
		_add_shares(s,seller,company,-shares)
		_add_shares(s,buyer,company,shares)
	return result

static func retained_earnings(s: Dictionary, owner: String) -> int:
	var result: int = 0
	for account_id in s.accounts:
		var account: Dictionary = s.accounts[account_id]
		if account.owner==owner and account.currency=="USD" and account.kind in ["expense","revenue"]: result-=int(account.balance)
	result-=Ledger.balance(s,owner+":dividends")
	return result

static func distribute_dividend(s: Dictionary, company: String, amount: int, month: int, id: String = "") -> Dictionary:
	if amount<=0 or cash(s,company)<amount or shares_outstanding(s,company)<=0: return {"ok":false,"error":"INVALID_DIVIDEND"}
	var total: int = shares_outstanding(s,company)
	var distributed: int = 0
	var entries: Array = []
	var holders: Array = s.ownership.filter(func(h): return h.company_id==company and int(h.shares)>0)
	holders.sort_custom(func(a,b): return str(a.owner_id)<str(b.owner_id))
	for index in range(holders.size()):
		var h: Dictionary = holders[index]
		var payout: int = amount-distributed if index==holders.size()-1 else int(amount*int(h.shares)/total)
		if payout<=0: continue
		entries.append_array([_line(company+":cash",-payout),_line(_account(s,company,"dividends","equity"),payout),_line(h.owner_id+":cash",payout),_line(_account(s,h.owner_id,"dividend_income","revenue"),-payout)])
		distributed+=payout
	return _post(s,id if id!="" else _id(s,"dividend"),entries,"지분별 배당·법인 자본 분배",month)

static func _premium(coverage: int, months: int) -> int:
	return maxi(1,int(ceil(float(coverage*months*int(definition().insurance_premium_bps))/10000.0)))

static func _insure(s: Dictionary, owner: String, p: Dictionary, month: int, id: String) -> Dictionary:
	var city: Dictionary = _find(s.cities,_owner_city(s,owner))
	var insurer: Dictionary = _find(s.finance.insurers,"insurer-"+str(city.nation_id))
	var premium: int = _premium(int(p.coverage),int(p.term_months))
	var policy_id: String = "policy-"+id
	var result: Dictionary = _post(s,id,[_line(owner+":cash",-premium),_line(_account(s,owner,"prepaid_insurance:"+policy_id,"asset"),premium),_line(insurer.id+":cash",premium),_line(_account(s,insurer.id,"unearned_premium:"+policy_id,"liability"),-premium)],"보험료 선납·보험사의 미경과 의무",month)
	if result.ok: s.finance.policies.append({"id":policy_id,"owner_id":owner,"insurer_id":insurer.id,"asset_id":p.asset_id,"city_id":city.id,"coverage":int(p.coverage),"remaining_coverage":int(p.coverage),"premium":premium,"earned":0,"start_month":month,"end_month":month+int(p.term_months)-1,"term_months":int(p.term_months),"status":"active"})
	return result

static func _loss_value(s: Dictionary, event: Dictionary) -> int:
	if event.has("loss_value"): return maxi(0,int(event.loss_value))
	var product: Dictionary = _find(s.products,str(event.get("product_id","")))
	return int(event.get("quantity",0))*int(product.get("price",0))

static func _claim(s: Dictionary, owner: String, p: Dictionary, month: int, id: String) -> Dictionary:
	var policy: Dictionary = _find(s.finance.policies,str(p.policy_id))
	var loss: Dictionary = _find(s.events,str(p.loss_event_id))
	var amount: int = mini(int(policy.remaining_coverage),_loss_value(s,loss))
	if amount<=0: return {"ok":false,"error":"COVERAGE_EXHAUSTED"}
	var claim_id: String = "claim-"+id
	var result: Dictionary = _post(s,id,[_line(_account(s,owner,"claim_receivable:"+claim_id,"asset"),amount),_line(_account(s,owner,"insurance_recovery","revenue"),-amount),_line(_account(s,policy.insurer_id,"claims_expense","expense"),amount),_line(_account(s,policy.insurer_id,"claim_payable:"+claim_id,"liability"),-amount)],"확인된 사고의 보험금 지급 의무",month)
	if result.ok:
		policy.remaining_coverage=int(policy.remaining_coverage)-amount
		var claim: Dictionary={"id":claim_id,"policy_id":policy.id,"loss_event_id":loss.id,"owner_id":owner,"insurer_id":policy.insurer_id,"amount":amount,"paid":0,"status":"payable"}
		s.finance.claims.append(claim)
		_pay_claim(s,claim,month)
	return result

static func _pay_claim(s: Dictionary, claim: Dictionary, month: int) -> void:
	var due: int=int(claim.amount)-int(claim.paid)
	var amount: int=mini(due,cash(s,claim.insurer_id))
	if amount<=0: return
	var result: Dictionary=_post(s,_id(s,"claim-settle"),[_line(claim.insurer_id+":cash",-amount),_line(claim.insurer_id+":claim_payable:"+claim.id,amount),_line(claim.owner_id+":cash",amount),_line(claim.owner_id+":claim_receivable:"+claim.id,-amount)],"보험사 실제 자금으로 보험금 지급",month)
	if result.ok:
		claim.paid=int(claim.paid)+amount
		if int(claim.paid)==int(claim.amount): claim.status="paid"

static func _maker(s: Dictionary, owner: String) -> Dictionary:
	var city: Dictionary = _find(s.cities,_owner_city(s,owner))
	return _find(s.finance.market_makers,"fx-"+str(city.get("nation_id","")))

static func _convert(s: Dictionary, amount: int, from: String, to: String) -> int:
	var a: Dictionary = _currency(s,from)
	var b: Dictionary = _currency(s,to)
	return int(amount*int(a.numerator)*int(b.denominator)/(int(a.denominator)*int(b.numerator)))

static func _fx_fee(amount: int) -> int:
	return maxi(1,int(ceil(float(amount*int(definition().exchange_fee_bps))/10000.0)))

static func _exchange(s: Dictionary, owner: String, p: Dictionary, month: int, id: String) -> Dictionary:
	var from: String=str(p.from_currency)
	var to: String=str(p.to_currency)
	var amount: int=int(p.amount)
	var received: int=_convert(s,amount,from,to)
	if received<=0: return {"ok":false,"error":"FX_ROUNDING_ZERO"}
	var fee: int=_fx_fee(amount)
	var maker: Dictionary=_maker(s,owner)
	_open_wallet(s,owner,from)
	_open_wallet(s,owner,to)
	var result: Dictionary=_post(s,id+"-source",[_line(_cash_id(owner,from),-amount-fee),_line(_account(s,owner,"fx_position:"+from,"equity",from),amount),_line(_account(s,owner,"fx_fee:"+from,"expense",from),fee),_line(_cash_id(maker.id,from),amount+fee),_line(_account(s,maker.id,"fx_position:"+from,"equity",from),-amount),_line(_account(s,maker.id,"fx_fee_income:"+from,"revenue",from),-fee)],"실제 상대방 외환 매도 통화 정산",month)
	if not result.ok: return result
	return _post(s,id+"-target",[_line(_cash_id(owner,to),received),_line(_account(s,owner,"fx_position:"+to,"equity",to),-received),_line(_cash_id(maker.id,to),-received),_line(_account(s,maker.id,"fx_position:"+to,"equity",to),received)],"유한 외환 보유 상대방에게서 매수 통화 인도",month)

static func reconcile_deposits(s: Dictionary, month: int) -> void:
	if not s.get("finance") is Dictionary: return
	for key in ["companies","people"]:
		for entity in s.get(key,[]): register_owner(s,str(entity.id))
	for entity in s.get("public",{}).get("governments",[]): register_owner(s,str(entity.id))
	for entity in s.get("public",{}).get("parties",[]): register_owner(s,str(entity.id))
	var restricted: Dictionary=_restricted_deposits(s)
	for owner in _keys(s.finance.owner_bank):
		var bank: String=str(s.finance.owner_bank[owner])
		for currency in s.finance.currencies:
			var cur: String=str(currency.id)
			var deposit: String=_account(s,bank,"deposit:"+owner+":"+cur,"liability",cur)
			var delta: int=cash(s,owner,cur)+int(restricted.get(str(owner)+"|"+cur,0))+Ledger.balance(s,deposit)
			if delta!=0:
				_post(s,_id(s,"deposit-settlement"),[_line(bank+":reserve:"+cur,delta),_line(deposit,-delta)],"예금 소유자 현금흐름과 은행 결제자산 정산",month)
	for bank in s.finance.banks:
		var reserve: int=Ledger.balance(s,bank.id+":reserve:USD")
		if reserve<0:
			var own_cover: int=mini(-reserve,cash(s,bank.id))
			if own_cover>0: _post(s,_id(s,"liquidity-cover"),[_line(bank.id+":reserve:USD",own_cover),_line(bank.id+":cash",-own_cover)],"은행 자기 유동성의 결제 투입",month)
			reserve=Ledger.balance(s,bank.id+":reserve:USD")
			if reserve<0:
				var borrowing: int=mini(-reserve,int(definition().cb_credit_limit)-int(bank.cb_borrowing))
				if borrowing>0:
					var cb: String=str(bank.central_bank_id)
					_post(s,_id(s,"cb-facility"),[_line(bank.id+":reserve:USD",borrowing),_line(_account(s,bank.id,"cb_borrowing","liability"),-borrowing),_line(_account(s,cb,"bank_loan:"+str(bank.id),"asset"),borrowing),_line(cb+":issued:USD",-borrowing)],"유한 담보 한도의 중앙은행 결제 대출",month)
					bank.cb_borrowing=int(bank.cb_borrowing)+borrowing
					var central: Dictionary=_find(s.finance.central_banks,cb)
					central.issued["USD"]=int(central.issued.get("USD",0))+borrowing
					_event(s,"base_money_creation","중앙은행 결제 대출·대응 은행 부채",month,{"actor_id":bank.id,"city_id":bank.city_id,"currency":"USD","amount":borrowing})

static func register_owner(s: Dictionary, owner: String, city_id: String = "") -> void:
	if not s.get("finance") is Dictionary or s.finance.owner_bank.has(owner): return
	if city_id=="": city_id=_owner_city(s,owner)
	var city: Dictionary=_find(s.cities,city_id)
	if city.is_empty(): return
	var bank: String="bank-"+str(city.nation_id)
	if _find(s.finance.banks,bank).is_empty(): return
	s.finance.owner_bank[owner]=bank
	for currency in s.finance.currencies:
		_account(s,bank,"deposit:"+owner+":"+str(currency.id),"liability",currency.id)

static func monthly(s: Dictionary, month: int) -> void:
	if not s.get("finance") is Dictionary: return
	if int(s.finance.get("last_serviced_month",0))>=month: return
	for loan in s.finance.loans:
		if int(loan.principal)<=0: continue
		var interest: int=maxi(1,int(ceil(float(int(loan.principal)*int(loan.interest_bps))/10000.0)))
		_post(s,_id(s,"interest-accrual"),[_line(_account(s,loan.borrower_id,"loan_interest","expense"),interest),_line(_account(s,loan.borrower_id,"interest_payable:"+str(loan.id),"liability"),-interest),_line(_account(s,loan.bank_id,"interest_receivable:"+str(loan.id),"asset"),interest),_line(_account(s,loan.bank_id,"loan_interest_income","revenue"),-interest)],"계약 이자 발생·대응 미수/미지급",month)
		loan.arrears=int(loan.arrears)+interest
		var repay: int=mini(cash(s,loan.borrower_id),int(loan.arrears)+(int(loan.principal) if month>=int(loan.due_month) else 0))
		if repay>0: _repay(s,loan,repay,month,_id(s,"scheduled-loan-payment"))
		if int(loan.arrears)>0 or (month>=int(loan.due_month) and int(loan.principal)>0):
			loan.missed_payments=int(loan.missed_payments)+1
			loan.status="arrears"
			_event(s,"finance_arrears","대출 지급기일에 실제 잔액 부족",month,{"actor_id":loan.borrower_id,"city_id":_owner_city(s,loan.borrower_id),"contract_id":loan.id,"amount":int(loan.arrears)})
			if int(loan.missed_payments)>=int(definition().default_after_months):
				loan.status="default"
				if int(loan.provision)==0 and int(loan.principal)>0:
					_post(s,_id(s,"loan-provision"),[_line(_account(s,loan.bank_id,"credit_loss","expense"),int(loan.principal)),_line(_account(s,loan.bank_id,"provision:"+str(loan.id),"asset"),-int(loan.principal))],"대손충당·자기자본 손실 (차주채무는 유지)",month)
					loan.provision=int(loan.principal)
		else:
			loan.missed_payments=0
			if int(loan.principal)>0: loan.status="active"
	for bond in s.finance.bonds: _service_bond(s,bond,month)
	for policy in s.finance.policies:
		if policy.status!="active": continue
		var elapsed: int=mini(int(policy.term_months),maxi(0,month-int(policy.start_month)+1))
		var earned: int=int(int(policy.premium)*elapsed/int(policy.term_months))-int(policy.earned)
		if earned>0:
			_post(s,_id(s,"premium-earned"),[_line(policy.insurer_id+":unearned_premium:"+policy.id,earned),_line(_account(s,policy.insurer_id,"premium_revenue","revenue"),-earned),_line(policy.owner_id+":prepaid_insurance:"+policy.id,-earned),_line(_account(s,policy.owner_id,"insurance_expense","expense"),earned)],"기간 경과 보험료 수익·비용 인식",month)
			policy.earned=int(policy.earned)+earned
		if month>=int(policy.end_month): policy.status="expired"
	for claim in s.finance.claims:
		if claim.status!="paid": _pay_claim(s,claim,month)
	# Savings interest is a real bank expense; insufficient bank cash produces
	# an unpaid notice instead of generating the promised interest externally.
	for owner in _keys(s.finance.owner_bank):
		for bank in s.finance.banks:
			var balance: int=Ledger.balance(s,_savings_id(str(owner),bank.id))
			var interest: int=int(balance*int(definition().savings_interest_bps)/10000)
			if interest>0 and cash(s,bank.id)>=interest:
				_post(s,_id(s,"savings-interest"),[_line(bank.id+":cash",-interest),_line(_account(s,bank.id,"savings_interest_expense","expense"),interest),_line(str(owner)+":cash",interest),_line(_account(s,str(owner),"savings_interest_income","revenue"),-interest)],"은행 실제 자금의 예금 이자 지급",month)
	reconcile_deposits(s,month)
	for bank in s.finance.banks:
		bank.capital=int(definition().bank_capital)+retained_earnings(s,bank.id)
		if int(bank.capital)<=0: bank.status="resolution"
	s.finance.last_serviced_month=month

static func _bond_holding(bond: Dictionary, owner: String) -> Dictionary:
	for holding in bond.holders:
		if holding.owner_id==owner: return holding
	var holding: Dictionary={"owner_id":owner,"principal":0,"interest_due":0}
	bond.holders.append(holding)
	return holding

static func _service_bond(s: Dictionary, bond: Dictionary, month: int) -> void:
	if (int(bond.outstanding)<=0 and int(bond.get("arrears",0))<=0) or month<=int(bond.start_month): return
	for holding in bond.holders:
		var principal: int=int(holding.principal)
		if principal<=0 and int(holding.get("interest_due",0))<=0: continue
		var interest: int=maxi(1,int(ceil(float(principal*int(bond.interest_bps))/10000.0))) if principal>0 else 0
		var payable: String=_account(s,bond.issuer_id,"bond_interest_payable:"+bond.id+":"+holding.owner_id,"liability")
		var receivable: String=_account(s,holding.owner_id,"bond_interest_receivable:"+bond.id,"asset")
		_post(s,_id(s,"bond-interest-accrual"),[_line(_account(s,bond.issuer_id,"bond_interest","expense"),interest),_line(payable,-interest),_line(receivable,interest),_line(_account(s,holding.owner_id,"bond_interest_income","revenue"),-interest)],"채권 계약 이자의 실제 발생·미지급/미수",month)
		holding.interest_due=int(holding.get("interest_due",0))+interest
		var paid: int=mini(int(holding.interest_due),cash(s,bond.issuer_id))
		if paid>0:
			_post(s,_id(s,"bond-interest"),[_line(bond.issuer_id+":cash",-paid),_line(payable,paid),_line(holding.owner_id+":cash",paid),_line(receivable,-paid)],"채권 발생이자 지급",month)
			holding.interest_due=int(holding.interest_due)-paid
		if int(holding.interest_due)>0:
			bond.status="arrears"
		if month>=int(bond.due_month):
			var repayment: int=mini(principal,cash(s,bond.issuer_id))
			if repayment>0:
				_post(s,_id(s,"bond-principal"),[_line(bond.issuer_id+":cash",-repayment),_line(bond.issuer_id+":bond:"+bond.id,repayment),_line(holding.owner_id+":cash",repayment),_line(holding.owner_id+":bond:"+bond.id,-repayment)],"채권 만기 원금 상환·자산 부채 감소",month)
				holding.principal=principal-repayment
				bond.outstanding=int(bond.outstanding)-repayment
			if int(holding.principal)>0: bond.status="default"
	bond.arrears=0
	for holding in bond.holders: bond.arrears=int(bond.arrears)+int(holding.get("interest_due",0))
	if int(bond.outstanding)==0 and int(bond.arrears)==0: bond.status="repaid"

static func debt_total(s: Dictionary, owner: String) -> int:
	var total: int=0
	for loan in s.get("finance",{}).get("loans",[]):
		if loan.borrower_id==owner: total+=int(loan.principal)+int(loan.arrears)
	for bond in s.get("finance",{}).get("bonds",[]):
		if bond.issuer_id==owner: total+=int(bond.outstanding)+int(bond.arrears)
	return total

static func settle_debt(s: Dictionary, owner: String, cash_budget: int, month: int) -> int:
	var total: int=0
	for loan in s.get("finance",{}).get("loans",[]):
		if loan.borrower_id!=owner: continue
		var amount: int=mini(mini(cash(s,owner),cash_budget-total),int(loan.principal)+int(loan.arrears))
		if amount>0 and _repay(s,loan,amount,month,_id(s,"estate-loan")).ok: total+=amount
	for bond in s.get("finance",{}).get("bonds",[]):
		if bond.issuer_id!=owner: continue
		for holding in bond.holders:
			var amount: int=mini(mini(cash(s,owner),cash_budget-total),int(holding.principal))
			if amount<=0: continue
			var result: Dictionary=_post(s,_id(s,"estate-bond"),[_line(owner+":cash",-amount),_line(owner+":bond:"+bond.id,amount),_line(holding.owner_id+":cash",amount),_line(holding.owner_id+":bond:"+bond.id,-amount)],"청산 재원으로 실제 채권 원금 정산",month)
			if result.ok:
				total+=amount
				holding.principal=int(holding.principal)-amount
				bond.outstanding=int(bond.outstanding)-amount
	return total

static func _quantity(s: Dictionary, owner: String, product: String) -> int:
	return Domain.owned_quantity(s,owner,product)

static func _stock_account(s: Dictionary, owner: String, product: String) -> String:
	return _account(s,owner,"stock:"+product,"asset")

static func inventory_account(s: Dictionary, owner: String, product: String) -> String:
	return _stock_account(s,owner,product)

static func cost_of_sale(s: Dictionary, owner: String, product: String, quantity: int, month: int, id: String = "") -> Dictionary:
	var cost: int=_stock_cost(s,owner,product,quantity)
	var result: Dictionary=_post(s,id if id!="" else _id(s,"cost-of-sale"),[_line(_stock_account(s,owner,product),-cost),_line(_account(s,owner,"cost_of_goods_sold","expense"),cost)],"실제 납품품의 매출원가",month)
	result["cost"]=cost
	return result

static func _stock_cost(s: Dictionary, owner: String, product: String, quantity: int) -> int:
	var held: int=_quantity(s,owner,product)
	if held<=0: return 0
	return mini(Ledger.balance(s,_stock_account(s,owner,product)),int(Ledger.balance(s,_stock_account(s,owner,product))*quantity/held))

static func settle_trade(s: Dictionary, id: String, buyer: String, seller: String, amount: int, product: String, quantity: int, month: int) -> Dictionary:
	if amount<=0 or quantity<=0 or buyer==seller or cash(s,buyer)<amount: return {"ok":false,"error":"INVALID_ACCRUAL_TRADE"}
	var cost: int=_stock_cost(s,seller,product,quantity)
	return _post(s,id,[_line(buyer+":cash",-amount),_line(_stock_account(s,buyer,product),amount),_line(seller+":cash",amount),_line(_account(s,seller,"sales_revenue","revenue"),-amount),_line(_stock_account(s,seller,product),-cost),_line(_account(s,seller,"cost_of_goods_sold","expense"),cost)],"상품 인도·재고 취득·판매/매출원가",month)

static func settle_service(s: Dictionary, id: String, buyer: String, seller: String, amount: int, month: int, purpose: String = "consumption") -> Dictionary:
	if amount<=0 or buyer==seller or cash(s,buyer)<amount: return {"ok":false,"error":"INVALID_SERVICE_SETTLEMENT"}
	var production: bool=purpose=="production"
	return _post(s,id,[_line(buyer+":cash",-amount),_line(_account(s,buyer,"production_wip" if production else "service_expense","asset" if production else "expense"),amount),_line(seller+":cash",amount),_line(_account(s,seller,"service_revenue","revenue"),-amount)],"생산 서비스 원가 조달" if production else "실제 서비스 이용 대금",month)

static func inventory_flow(s: Dictionary, owner: String, product: String, delta: int, reason: String, month: int, project_id: String = "") -> void:
	if not s.get("finance") is Dictionary or delta==0 or reason in ["매입 입고","판매 출고","도착 인수","운송 종료","운송 출고","운송 중 소유권 이전","trade_in","trade_out","accounted_use"]: return
	var stock: String=_stock_account(s,owner,product)
	if delta<0:
		var value: int=_stock_cost(s,owner,product,-delta)
		if value<=0: return
		var target: String="stock_use"
		var kind: String="expense"
		if reason in ["공정 투입","산업 공정 투입","생육 투입물"]: target="production_wip"; kind="asset"
		elif reason=="건설 공정 투입":
			if project_id=="": project_id=str(s.get("cost_context",{}).get("project_id",""))
			target="construction_wip:"+project_id; kind="asset"
		_post(s,_id(s,"inventory-use"),[_line(stock,-value),_line(_account(s,owner,target,kind),value)],"재고 원가 이동: "+reason,month)
	elif reason in ["공정 산출","산업 공정 산출"]:
		# The complete output vector is valued once by allocate_production;
		# assigning all batch cost to whichever byproduct is inserted first
		# would make accounting depend on Dictionary iteration order.
		return
	elif reason=="opening":
		var value: int=delta*int(_find(s.products,product).get("price",0))
		if value>0: _post(s,_id(s,"opening-stock"),[_line(stock,value),_line(_account(s,owner,"opening_stock_equity","equity"),-value)],"개시 재고 원가",month)

static func record_labor(s: Dictionary, owner: String, amount: int, month: int) -> void:
	if amount<=0 or not s.get("finance") is Dictionary: return
	_post(s,_id(s,"labor-capitalize"),[_line(_account(s,owner,"production_wip","asset"),amount),_line(_account(s,owner,"expense","expense"),-amount)],"생산노무비의 재공품 원가 대체",month)

static func capitalize_construction_payment(s: Dictionary, owner: String, amount: int, month: int, project_id: String = "") -> void:
	if amount<=0 or not s.get("finance") is Dictionary: return
	if project_id=="": project_id=str(s.get("cost_context",{}).get("project_id",""))
	_post(s,_id(s,"construction-capitalize"),[_line(_account(s,owner,"construction_wip:"+project_id,"asset"),amount),_line(_account(s,owner,"expense","expense"),-amount)],"도급 공정대금의 건설중자산 대체",month)

static func finalize_construction(s: Dictionary, owner: String, project_id: String, month: int) -> void:
	var wip: String=_account(s,owner,"construction_wip:"+project_id,"asset")
	var value: int=Ledger.balance(s,wip)
	if value>0: _post(s,_id(s,"construction-complete"),[_line(_account(s,owner,"facility:"+project_id,"asset"),value),_line(wip,-value)],"준공 건물 자산 대체",month)

static func allocate_production(s: Dictionary, owner: String, outputs: Dictionary, month: int, project_id: String = "") -> void:
	if not s.get("finance") is Dictionary: return
	var wip: String=_account(s,owner,"production_wip","asset")
	var cost: int=Ledger.balance(s,wip)
	if cost<=0: return
	if project_id!="":
		_post(s,_id(s,"growth-cost"),[_line(_account(s,owner,"growth:"+project_id,"asset"),cost),_line(wip,-cost)],"실제 생육 투입원가의 진행자산 대체",month)
		return
	_allocate_outputs(s,owner,outputs,cost,wip,month)

static func complete_growth(s: Dictionary, owner: String, project_id: String, outputs: Dictionary, month: int) -> void:
	var source: String=_account(s,owner,"growth:"+project_id,"asset")
	var cost: int=Ledger.balance(s,source)
	if cost>0: _allocate_outputs(s,owner,outputs,cost,source,month)

static func abandon_growth(s: Dictionary, owner: String, project_id: String, month: int) -> void:
	_writeoff_project(s,owner,"growth:"+project_id,"failed_growth",month)

static func cancel_construction(s: Dictionary, owner: String, project_id: String, month: int) -> void:
	_writeoff_project(s,owner,"construction_wip:"+project_id,"cancelled_construction",month)

static func _writeoff_project(s: Dictionary, owner: String, source_suffix: String, expense_suffix: String, month: int) -> void:
	var source: String=_account(s,owner,source_suffix,"asset")
	var value: int=Ledger.balance(s,source)
	if value>0: _post(s,_id(s,"project-writeoff"),[_line(source,-value),_line(_account(s,owner,expense_suffix,"expense"),value)],"취소된 프로젝트의 이미 투입한 실제 원가 손실",month)

static func _allocate_outputs(s: Dictionary, owner: String, outputs: Dictionary, cost: int, source: String, month: int) -> void:
	var weights: Dictionary={}
	var total: int=0
	for product_id in _keys(outputs):
		var product: Dictionary=_find(s.products,product_id)
		var weight: int=maxi(0,int(outputs[product_id]))*maxi(1,int(product.get("price",1)))
		if weight>0: weights[product_id]=weight; total+=weight
	if total<=0:
		_post(s,_id(s,"failed-production-cost"),[_line(source,-cost),_line(_account(s,owner,"failed_production","expense"),cost)],"산출물 없는 공정의 실제 손실",month)
		return
	var keys: Array=_keys(weights)
	var allocated: int=0
	var lines: Array=[_line(source,-cost)]
	for index in range(keys.size()):
		var product_id: String=str(keys[index])
		var amount: int=cost-allocated if index==keys.size()-1 else int(cost*int(weights[product_id])/total)
		allocated+=amount
		if amount<=0: continue
		var product: Dictionary=_find(s.products,product_id)
		var account: String=_account(s,owner,"service_production_cost","expense") if product.get("kind","material") in ["service","digital"] else _stock_account(s,owner,product_id)
		lines.append(_line(account,amount))
	_post(s,_id(s,"batch-cost-allocation"),lines,"원가를 다중 산출물에 기준가치 비중으로 배분·미이월 서비스 원가 인식",month)

static func summary(s: Dictionary, owner: String) -> Dictionary:
	var savings: int=0
	for bank in s.get("finance",{}).get("banks",[]): savings+=Ledger.balance(s,_savings_id(owner,bank.id))
	var wallets: Array=[]
	for currency in s.get("finance",{}).get("currencies",[]): wallets.append({"currency":currency.id,"amount":cash(s,owner,currency.id)})
	return {"currency":Ledger.BASE_CURRENCY,"minor_units_per_major":Ledger.MINOR_UNITS_PER_MAJOR,"cash":cash(s,owner),"restricted_deposits":int(_restricted_deposits(s).get(owner+"|USD",0)),"savings":savings,"debt":debt_total(s,owner),"retained_earnings":retained_earnings(s,owner),"wallets":wallets,"loans":s.get("finance",{}).get("loans",[]).filter(func(l): return l.borrower_id==owner),"holdings":s.get("ownership",[]).filter(func(h): return h.owner_id==owner),"bonds":s.get("finance",{}).get("bonds",[]).filter(func(b): return b.issuer_id==owner),"policies":s.get("finance",{}).get("policies",[]).filter(func(p): return p.owner_id==owner),"share_offers":s.get("finance",{}).get("share_offers",[]),"equity_issues":s.get("finance",{}).get("equity_issues",[])}.duplicate(true)

static func _restricted_deposits(s: Dictionary) -> Dictionary:
	var balances: Dictionary={}
	for account_id in s.get("accounts",{}):
		var account: Dictionary=s.accounts[account_id]
		if account.kind=="asset" and ":market-escrow:" in str(account_id):
			var key: String=str(account.owner)+"|"+str(account.currency)
			balances[key]=int(balances.get(key,0))+int(account.balance)
	return balances

static func monetary_summary(s: Dictionary) -> Array:
	var result: Array=[]
	if not s.get("finance") is Dictionary: return result
	var restricted: Dictionary=_restricted_deposits(s)
	for currency in s.finance.currencies:
		var cur: String=str(currency.id)
		var transaction: int=0
		var savings: int=0
		var held: int=0
		var reserves: int=0
		var issued: int=0
		for owner in s.finance.owner_bank:
			transaction+=cash(s,str(owner),cur)
			held+=int(restricted.get(str(owner)+"|"+cur,0))
		if cur=="USD":
			for account_id in s.accounts:
				var account: Dictionary=s.accounts[account_id]
				if account.kind=="asset" and account.currency==cur and ":savings:" in str(account_id): savings+=int(account.balance)
		for bank in s.finance.banks: reserves+=Ledger.balance(s,bank.id+":reserve:"+cur)+cash(s,bank.id,cur)
		for cb in s.finance.central_banks: issued-=Ledger.balance(s,cb.id+":issued:"+cur)
		result.append({"currency":cur,"transaction_deposits":transaction,"restricted_deposits":held,"savings_deposits":savings,"broad_money":transaction+held+savings,"bank_settlement_assets":reserves,"central_bank_issued":issued})
	return result

static func ai_commands(s: Dictionary, month: int = -1) -> Array:
	var result: Array=[]
	if not s.get("finance") is Dictionary: return result
	if month<0: month=int(s.planning_month)
	var d: Dictionary=definition()
	var companies: Array=s.companies.duplicate()
	companies.sort_custom(func(a,b): return str(a.id)<str(b.id))
	for company in companies:
		var actor: String=str(company.get("director_id",""))
		if actor=="" or actor==str(s.get("player_id","")) or company.get("status","active")!="active": continue
		if cash(s,company.id)>=int(d.ai_company_cash_buffer): continue
		var room: int=maxi(0,_credit_capacity(s,company.id)-debt_total(s,company.id))
		var amount: int=mini(room,maxi(0,int(d.ai_loan_target)-cash(s,company.id)))
		var c: Dictionary={}
		if amount>0:
			c=_ai_command(s,actor,company.id,"finance_borrow",{"bank_id":s.finance.owner_bank.get(company.id,""),"amount":amount,"term_months":int(d.ai_loan_months)},month)
			if validate_command(s,c)=="": result.append(c); continue
		var has_issue: bool=false
		for issue in s.finance.equity_issues:
			if issue.issuer_id==company.id and int(issue.remaining)>0 and int(issue.expiry_month)>=month: has_issue=true
		if not has_issue:
			c=_ai_command(s,actor,company.id,"finance_offer_equity",{"shares":int(d.ai_share_offer_quantity),"unit_price":int(d.ai_share_price),"term_months":int(d.ai_offer_term_months)},month)
			if validate_command(s,c)=="": result.append(c)
	var people: Array=s.people.duplicate()
	people.sort_custom(func(a,b): return str(a.id)<str(b.id))
	for person in people:
		var owner: String=str(person.id)
		if owner==str(s.get("player_id","")) or person.get("career_status","active")!="active": continue
		var free: int=maxi(0,cash(s,owner)-int(d.ai_person_reserve))
		var candidate: Dictionary={}
		if free>0:
			var budget: int=mini(free,int(d.ai_investment_budget))
			for issue in s.finance.equity_issues:
				if int(issue.expiry_month)<month or int(issue.remaining)<=0: continue
				var shares: int=mini(int(issue.remaining),int(budget/int(issue.unit_price)))
				if shares<=0: continue
				var c: Dictionary=_ai_command(s,owner,"","finance_subscribe_equity",{"issue_id":issue.id,"shares":shares},month)
				if validate_command(s,c)=="": candidate=c; break
			if candidate.is_empty():
				for offer in s.finance.share_offers:
					if offer.owner_id==owner or int(offer.expiry_month)<month or int(offer.remaining)<=0: continue
					var shares: int=mini(int(offer.remaining),int(budget/int(offer.unit_price)))
					if shares<=0: continue
					var c: Dictionary=_ai_command(s,owner,"","finance_trade_shares",{"company_id":offer.company_id,"seller_id":offer.owner_id,"shares":shares,"unit_price":int(offer.unit_price)},month)
					if validate_command(s,c)=="": candidate=c; break
			if candidate.is_empty():
				for bond in s.finance.bonds:
					var amount: int=mini(budget,int(bond.face)-int(bond.subscribed))
					if amount<=0 or month>int(bond.due_month): continue
					var c: Dictionary=_ai_command(s,owner,"","finance_buy_bond",{"bond_id":bond.id,"amount":amount},month)
					if validate_command(s,c)=="": candidate=c; break
			if candidate.is_empty() and free>=int(d.ai_person_reserve):
				var c: Dictionary=_ai_command(s,owner,"","finance_deposit",{"bank_id":s.finance.owner_bank.get(owner,""),"amount":budget},month)
				if validate_command(s,c)=="": candidate=c
		elif cash(s,owner)<int(d.ai_person_reserve)/2:
			for bank in s.finance.banks:
				var saving: int=Ledger.balance(s,_savings_id(owner,bank.id))
				var amount: int=mini(saving,int(d.ai_person_reserve)-cash(s,owner))
				if amount>0:
					var c: Dictionary=_ai_command(s,owner,"","finance_withdraw",{"bank_id":bank.id,"amount":amount},month)
					if validate_command(s,c)=="": candidate=c; break
			if candidate.is_empty():
				for holding in s.ownership:
					if holding.owner_id!=owner: continue
					var shares: int=mini(int(d.ai_share_offer_quantity),available_shares(s,owner,holding.company_id))
					if shares<=0: continue
					var c: Dictionary=_ai_command(s,owner,"","finance_offer_shares",{"company_id":holding.company_id,"shares":shares,"unit_price":int(d.ai_share_price),"term_months":int(d.ai_offer_term_months)},month)
					if validate_command(s,c)=="": candidate=c; break
		if not candidate.is_empty(): result.append(candidate)
	return result

static func _ai_command(s: Dictionary, actor: String, company: String, type: String, payload: Dictionary, month: int) -> Dictionary:
	return {"id":"finance-ai-%s-%s-%s-%d"%[type,actor,company,month],"actor_id":actor,"acting_company_id":company,"expected_version":int(s.version),"type":type,"payload":payload}

static func validate(s: Dictionary) -> Array:
	if not s.get("finance") is Dictionary: return ["FINANCE_STATE_MISSING"]
	if s.finance.get("base_currency")!=Ledger.BASE_CURRENCY or s.finance.get("minor_units_per_major")!=Ledger.MINOR_UNITS_PER_MAJOR: return ["FINANCE_MONEY_UNIT_MISMATCH"]
	var errors: Array=[]
	for key in ["banks","central_banks","insurers","market_makers","loans","bonds","policies","claims","currencies","share_offers","equity_issues","capital_mandates"]:
		if not s.finance.get(key) is Array: errors.append("FINANCE_ARRAY_MISSING:"+key)
	if not errors.is_empty(): return errors
	var fields: Dictionary={"banks":["id","central_bank_id","city_id","capital","cb_borrowing"],"central_banks":["id","nation_id","city_id","issued"],"loans":["id","bank_id","borrower_id","principal","original_principal","interest_bps","term_months","start_month","due_month","arrears","missed_payments","status","provision"],"bonds":["id","issuer_id","face","subscribed","outstanding","interest_bps","due_month","arrears","holders"],"policies":["id","owner_id","insurer_id","premium","earned","remaining_coverage","coverage"],"claims":["id","owner_id","insurer_id","policy_id","loss_event_id","amount","paid"],"insurers":["id","nation_id","city_id"],"market_makers":["id","nation_id","city_id"],"share_offers":["id","owner_id","company_id","remaining","unit_price","expiry_month"],"equity_issues":["id","issuer_id","remaining","unit_price","expiry_month"],"capital_mandates":["id","company_id","person_id","grantor_id","mode","remaining_shares","minimum_price","remaining_amount","expiry_month"]}
	for key in fields:
		var seen: Dictionary={}
		for row in s.finance[key]:
			if not row is Dictionary:
				errors.append("FINANCE_ROW_SCHEMA:"+key)
				continue
			for field in fields[key]:
				if not row.has(field): errors.append("FINANCE_FIELD_MISSING:"+key+":"+field)
			if not row.get("id") is String or str(row.get("id","")).is_empty() or seen.has(row.get("id")): errors.append("FINANCE_ID_INVALID:"+key)
			seen[row.get("id","")]=true
	# Structural validation finishes before any arithmetic or account-name
	# concatenation; a damaged save returns diagnostics rather than a cast error.
	var numeric: Dictionary={"banks":["capital","cb_borrowing"],"loans":["principal","original_principal","interest_bps","term_months","start_month","due_month","arrears","missed_payments","provision"],"bonds":["face","subscribed","outstanding","interest_bps","due_month","arrears"],"policies":["premium","earned","remaining_coverage","coverage"],"claims":["amount","paid"],"share_offers":["remaining","unit_price","expiry_month"],"equity_issues":["remaining","unit_price","expiry_month"],"capital_mandates":["remaining_shares","minimum_price","remaining_amount","expiry_month"]}
	var strings: Dictionary={"banks":["id","central_bank_id","city_id"],"central_banks":["id","nation_id","city_id"],"loans":["id","bank_id","borrower_id","status"],"bonds":["id","issuer_id"],"policies":["id","owner_id","insurer_id"],"claims":["id","owner_id","insurer_id","policy_id","loss_event_id"],"insurers":["id","nation_id","city_id"],"market_makers":["id","nation_id","city_id"],"share_offers":["id","owner_id","company_id"],"equity_issues":["id","issuer_id"],"capital_mandates":["id","company_id","person_id","grantor_id","mode"]}
	for key in strings:
		for row in s.finance[key]:
			if not row is Dictionary: continue
			for field in strings[key]:
				if not row.get(field) is String or str(row.get(field,"")).is_empty(): errors.append("FINANCE_STRING_INVALID:"+key+":"+field)
			for field in numeric.get(key,[]):
				if not _integer(row.get(field)) or (field!="capital" and int(row[field])<0): errors.append("FINANCE_INTEGER_INVALID:"+key+":"+field)
	for central in s.finance.central_banks:
		if not central is Dictionary or not central.get("issued") is Dictionary: errors.append("CENTRAL_BANK_ISSUED_SCHEMA"); continue
		for currency in central.issued:
			if not currency is String or not _integer(central.issued[currency]) or int(central.issued[currency])<0: errors.append("CENTRAL_BANK_ISSUED_SCHEMA")
	for currency in s.finance.currencies:
		if not currency is Dictionary or not currency.get("id") is String or str(currency.get("id","")).is_empty() or not _integer(currency.get("numerator")) or not _integer(currency.get("denominator")): errors.append("FINANCE_CURRENCY_SCHEMA"); continue
		if int(currency.numerator)<=0 or int(currency.denominator)<=0: errors.append("FINANCE_CURRENCY_RATE_INVALID")
		if not Ledger.supported_currency(currency.id) or currency.get("minor_digits")!=2: errors.append("FINANCE_CURRENCY_UNIT_INVALID")
	for key in ["owner_bank","applied_commands"]:
		if not s.finance.get(key) is Dictionary: errors.append("FINANCE_DICTIONARY_MISSING:"+key)
	if s.finance.get("owner_bank") is Dictionary:
		for owner in s.finance.owner_bank:
			if not owner is String or not s.finance.owner_bank[owner] is String: errors.append("OWNER_BANK_SCHEMA")
	if not s.get("ownership") is Array: errors.append("FINANCE_OWNERSHIP_SCHEMA")
	else:
		for holding in s.ownership:
			if not holding is Dictionary or not holding.get("owner_id") is String or not holding.get("company_id") is String or not _integer(holding.get("shares")) or not _integer(holding.get("voting_shares")): errors.append("FINANCE_OWNERSHIP_SCHEMA")
	if not s.get("accounts") is Dictionary: errors.append("FINANCE_ACCOUNTS_SCHEMA")
	else:
		for account_id in s.accounts:
			var account: Variant=s.accounts[account_id]
			if not account is Dictionary or not account.get("kind") is String or not account.get("owner") is String or not account.get("currency") is String or not _integer(account.get("balance")): errors.append("FINANCE_ACCOUNT_SCHEMA")
	for key in ["banks","loans","bonds"]:
		for row in s.finance[key]:
			if not row is Dictionary or row.get("currency")!=Ledger.BASE_CURRENCY: errors.append("FINANCE_BASE_CURRENCY_MISMATCH:"+key)
	if not errors.is_empty(): return errors
	for loan in s.finance.loans:
		for key in ["principal","original_principal","interest_bps","term_months","start_month","due_month","arrears","missed_payments","provision"]:
			if not _integer(loan.get(key)) or int(loan[key])<0: errors.append("LOAN_NEGATIVE_OR_INVALID:"+key)
		if _find(s.finance.banks,str(loan.bank_id)).is_empty() or not s.accounts.has(str(loan.borrower_id)+":cash"): errors.append("LOAN_PARTY_MISSING")
		if Ledger.balance(s,loan.bank_id+":loan:"+loan.id)!=int(loan.principal): errors.append("LOAN_ASSET_MISMATCH:"+loan.id)
		if Ledger.balance(s,loan.borrower_id+":loan:"+loan.id)!=-int(loan.principal): errors.append("LOAN_DEBT_MISMATCH:"+loan.id)
		if Ledger.balance(s,loan.bank_id+":interest_receivable:"+loan.id)!=int(loan.arrears) or Ledger.balance(s,loan.borrower_id+":interest_payable:"+loan.id)!=-int(loan.arrears): errors.append("LOAN_INTEREST_COUNTERPART_MISMATCH:"+loan.id)
		if int(loan.provision)>int(loan.principal) or Ledger.balance(s,loan.bank_id+":provision:"+loan.id)!=-int(loan.provision): errors.append("LOAN_PROVISION_MISMATCH:"+loan.id)
	for bond in s.finance.bonds:
		if not bond.holders is Array:
			errors.append("BOND_HOLDER_SCHEMA")
			continue
		var total: int=0
		var interest_total: int=0
		for holding in bond.holders:
			if not holding is Dictionary or not holding.get("owner_id") is String or not _integer(holding.get("principal")) or not _integer(holding.get("interest_due",0)):
				errors.append("BOND_HOLDER_SCHEMA")
				continue
			if int(holding.principal)<0: errors.append("BOND_NEGATIVE_HOLDING")
			total+=int(holding.principal)
			var due: int=int(holding.get("interest_due",0))
			interest_total+=due
			if Ledger.balance(s,str(holding.owner_id)+":bond:"+str(bond.id))!=int(holding.principal): errors.append("BOND_ASSET_MISMATCH:"+str(bond.id))
			if Ledger.balance(s,str(holding.owner_id)+":bond_interest_receivable:"+str(bond.id))!=due or Ledger.balance(s,str(bond.issuer_id)+":bond_interest_payable:"+str(bond.id)+":"+str(holding.owner_id))!=-due: errors.append("BOND_INTEREST_COUNTERPART_MISMATCH")
		if total!=int(bond.outstanding): errors.append("BOND_HOLDING_MISMATCH:"+bond.id)
		if interest_total!=int(bond.arrears): errors.append("BOND_ARREARS_MISMATCH:"+str(bond.id))
		if Ledger.balance(s,bond.issuer_id+":bond:"+bond.id)!=-int(bond.outstanding): errors.append("BOND_DEBT_MISMATCH:"+bond.id)
	for holding in s.get("ownership",[]):
		if not _integer(holding.get("shares")) or int(holding.shares)<0 or int(holding.voting_shares)<0: errors.append("INVALID_OWNERSHIP_QUANTITY")
	for bank in s.finance.banks:
		for currency in s.finance.currencies:
			if Ledger.balance(s,bank.id+":reserve:"+str(currency.id))<0: errors.append("BANK_SETTLEMENT_LIQUIDITY_NEGATIVE:"+bank.id)
	var restricted: Dictionary=_restricted_deposits(s)
	for owner in s.finance.get("owner_bank",{}):
		var bank: String=str(s.finance.owner_bank[owner])
		if _find(s.finance.banks,bank).is_empty(): errors.append("OWNER_BANK_UNKNOWN")
		for currency in s.finance.currencies:
			var cur: String=str(currency.id)
			if Ledger.balance(s,bank+":deposit:"+str(owner)+":"+cur)!=-(cash(s,str(owner),cur)+int(restricted.get(str(owner)+"|"+cur,0))): errors.append("DEPOSIT_OWNER_COUNTERPART_MISMATCH:"+str(owner)+":"+cur)
	for policy in s.finance.policies:
		if int(policy.remaining_coverage)<0 or int(policy.earned)>int(policy.premium): errors.append("INSURANCE_COVERAGE_OR_PREMIUM_MISMATCH")
		var unearned: int=int(policy.premium)-int(policy.earned)
		if Ledger.balance(s,policy.owner_id+":prepaid_insurance:"+policy.id)!=unearned or Ledger.balance(s,policy.insurer_id+":unearned_premium:"+policy.id)!=-unearned: errors.append("INSURANCE_PREMIUM_COUNTERPART_MISMATCH")
	for claim in s.finance.claims:
		var due: int=int(claim.amount)-int(claim.paid)
		if due<0 or Ledger.balance(s,claim.owner_id+":claim_receivable:"+claim.id)!=due or Ledger.balance(s,claim.insurer_id+":claim_payable:"+claim.id)!=-due: errors.append("INSURANCE_CLAIM_COUNTERPART_MISMATCH")
	for position in monetary_summary(s):
		if int(position.bank_settlement_assets)!=int(position.central_bank_issued): errors.append("CENTRAL_BANK_RESERVE_COUNTERPART_MISMATCH:"+str(position.currency))
	for account_id in s.accounts:
		if ":stock:" in str(account_id) and int(s.accounts[account_id].balance)<0: errors.append("NEGATIVE_STOCK_VALUE:"+str(account_id))
	return errors

static func _command_label(type: String) -> String:
	return str({"finance_borrow":"차입 실행","finance_repay":"채무 상환","finance_deposit":"예금 가입","finance_withdraw":"예금 인출","finance_issue_shares":"신주 발행","finance_offer_shares":"주식 매도 제안","finance_offer_equity":"신주 청약 공고","finance_subscribe_equity":"신주 청약 납입","finance_trade_shares":"기존 주식 거래","finance_dividend":"지분별 배당","finance_authorize_capital":"주주 의결권에 따른 자본 승인","finance_cancel_offer":"미체결 자본 제안 철회","finance_issue_bond":"채권 발행 공고","finance_buy_bond":"채권 인수","finance_insure":"보험 계약","finance_claim":"보험금 청구","finance_exchange":"유한 외환 거래"}.get(type,type))

static func ui_actions() -> Array:
	var amount: Dictionary={"key":"amount","label":"금액 (최소 화폐 단위)","kind":"int","min":1,"max":int(definition().maximum_transaction),"default":100}
	var term: Dictionary={"key":"term_months","label":"기간 (개월)","kind":"int","min":1,"max":int(definition().max_term_months),"default":12}
	return [
		{"type":"finance_borrow","label":"대출 신청","scope":"both","fields":[{"key":"bank_id","label":"은행","kind":"choice","source":"banks"},amount,term]},
		{"type":"finance_repay","label":"대출 상환","scope":"both","fields":[{"key":"loan_id","label":"내 대출","kind":"choice","source":"loans"},amount]},
		{"type":"finance_deposit","label":"예금 가입","scope":"both","fields":[{"key":"bank_id","label":"은행","kind":"choice","source":"banks"},amount]},
		{"type":"finance_withdraw","label":"예금 인출","scope":"both","fields":[{"key":"bank_id","label":"은행","kind":"choice","source":"banks"},amount]},
		{"type":"finance_issue_shares","label":"신주 발행","scope":"company","fields":[{"key":"investor_id","label":"동의한 투자자","kind":"choice","source":"people"},{"key":"shares","label":"주식 수","kind":"int","min":1,"max":100000,"default":10},{"key":"unit_price","label":"주당 가격","kind":"int","min":1,"max":100000,"default":12}]},
		{"type":"finance_offer_shares","label":"주식 매도 제안","scope":"both","fields":[{"key":"company_id","label":"보유 회사","kind":"choice","source":"companies"},{"key":"shares","label":"매도 주식 수","kind":"int","min":1,"max":100000,"default":10},{"key":"unit_price","label":"매도 주당 가격","kind":"int","min":1,"max":100000,"default":10},term]},
		{"type":"finance_offer_equity","label":"신주 청약 공고","scope":"company","fields":[{"key":"shares","label":"모집할 주식 수","kind":"int","min":1,"max":100000,"default":10},{"key":"unit_price","label":"청약 주당 가격","kind":"int","min":1,"max":100000,"default":10},term]},
		{"type":"finance_subscribe_equity","label":"공개 신주 청약","scope":"both","fields":[{"key":"issue_id","label":"공개 신주 공고","kind":"choice","source":"equity_issues"},{"key":"shares","label":"인수할 주식 수","kind":"int","min":1,"max":100000,"default":10}]},
		{"type":"finance_trade_shares","label":"주식 매수","scope":"both","fields":[{"key":"company_id","label":"회사","kind":"choice","source":"companies"},{"key":"seller_id","label":"매도 동의자","kind":"choice","source":"people"},{"key":"shares","label":"수량","kind":"int","min":1,"max":100000,"default":10},{"key":"unit_price","label":"가격","kind":"int","min":1,"max":100000,"default":12}]},
		{"type":"finance_dividend","label":"배당 결정","scope":"company","fields":[amount]},
		{"type":"finance_authorize_capital","label":"대표에게 자본 승인","scope":"personal","fields":[{"key":"company_id","label":"의결권을 가진 회사","kind":"choice","source":"companies"},{"key":"person_id","label":"승인받을 대표","kind":"choice","source":"people"},{"key":"mode","label":"승인 종류","kind":"choice","options":[{"value":"equity","label":"신주 발행"},{"value":"dividend","label":"배당"}]},{"key":"shares","label":"신주 승인 수량","kind":"int","min":1,"max":100000,"default":100},{"key":"unit_price","label":"신주 최저 가격","kind":"int","min":1,"max":100000,"default":10},amount,term]},
		{"type":"finance_cancel_offer","label":"미체결 주식 제안 철회","scope":"both","fields":[{"key":"offer_id","label":"내 주식·청약 제안","kind":"choice","source":"finance_offers"}]},
		{"type":"finance_issue_bond","label":"채권 발행","scope":"both","fields":[amount,{"key":"interest_bps","label":"월 금리 (bp)","kind":"int","min":1,"max":1000,"default":60},term]},
		{"type":"finance_buy_bond","label":"채권 인수","scope":"both","fields":[{"key":"bond_id","label":"발행 채권","kind":"choice","source":"bonds"},amount]},
		{"type":"finance_insure","label":"시설 보험","scope":"company","fields":[{"key":"asset_id","label":"내 시설","kind":"choice","source":"facilities"},{"key":"coverage","label":"보장 한도","kind":"int","min":1,"max":1000000,"default":1000},term]},
		{"type":"finance_claim","label":"보험금 청구","scope":"company","fields":[{"key":"policy_id","label":"내 보험","kind":"choice","source":"insurance_policies"},{"key":"loss_event_id","label":"실제 사고","kind":"choice","source":"loss_events"}]},
		{"type":"finance_exchange","label":"외환 거래","scope":"both","fields":[{"key":"from_currency","label":"매도 통화","kind":"choice","source":"currencies"},{"key":"to_currency","label":"매수 통화","kind":"choice","source":"currencies"},amount]}
	]
