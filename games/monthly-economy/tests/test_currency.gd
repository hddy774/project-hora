extends RefCounted
## Currency is part of the simulation contract, not only a UI suffix.
const Ledger=preload("res://core/ledger.gd")
const Finance=preload("res://core/finance.gd")
const Economy=preload("res://core/economy.gd")
const Store=preload("res://core/save_store.gd")
static var _checks: int=0

static func _check(value: bool,label: String,failures: Array) -> void:
	_checks+=1
	if not value: failures.append(label)

static func _command(s: Dictionary,id: String,type: String,payload: Dictionary) -> Dictionary:
	return {"id":id,"actor_id":"p-0","acting_company_id":"co-0","expected_version":s.version,"type":type,"payload":payload}

static func _legacy_state() -> Dictionary:
	# A genuine old-format balanced ledger, built explicitly without permitting
	# new gameplay to create virtual-currency accounts.
	return {"schema_version":2,"rules_version":"experimental-0.2","content_hash":Store.LEGACY_CONTENT_HASHES[0],"phase":"Planning","planning_month":8,"last_resolved_month":7,
		"accounts":{"legacy:cash":{"owner":"legacy","kind":"cash","currency":"CR","balance":12345},"legacy:equity":{"owner":"legacy","kind":"equity","currency":"CR","balance":-12345}},
		"journal":[{"id":"opening-legacy","month":0,"reason":"OPENING_EQUITY","lines":[{"account":"legacy:cash","debit":12345,"credit":0},{"account":"legacy:equity","debit":0,"credit":12345}]}],"journal_index":{"opening-legacy":0}}

static func _write_snapshot(path: String,state: Dictionary,format: int=Store.FORMAT_VERSION,checksum: String="") -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(Store.SAVE_DIR))
	var envelope: Dictionary={"format_version":format,"schema_version":state.schema_version,"rules_version":state.rules_version,"content_hash":state.get("content_hash",""),"checksum":Store.hash_state(state) if checksum=="" else checksum,"state":state}
	var file: FileAccess=FileAccess.open(path,FileAccess.WRITE)
	file.store_string(Store.canonical(envelope))
	file.close()

static func _check_slot_compatibility(world: Dictionary,failures: Array) -> void:
	var slot: String="__test_currency_compatibility"
	Store.delete_slot(slot)
	var path: String=Store.SAVE_DIR+"/"+slot+".json"
	var old: Dictionary=_legacy_state()
	_check(Ledger.validate(old,true).is_empty(),"Legacy diagnostic verifies the old ledger under its original currency rules",failures)
	_write_snapshot(path,old)
	var original: String=FileAccess.get_sha256(path)
	var loaded: Dictionary=Store.load_world(slot)
	_check(not loaded.ok and loaded.get("requires_new_game",false) and loaded.get("compatibility_code")=="LEGACY_UNITS_REQUIRE_NEW_GAME","Breaking currency/physical-unit change explicitly refuses a partial old-save conversion",failures)
	_check(loaded.get("original_preserved",false) and FileAccess.get_sha256(path)==original,"Legacy load leaves every original file byte unchanged",failures)
	_check(loaded.state.is_empty() and not loaded.error.contains("UNSUPPORTED_ACCOUNT_CURRENCY"),"Legacy incompatibility is distinct from current-dollar ledger corruption",failures)
	var listed: Array=Store.list_slots()
	var found: bool=false
	for item in listed:
		if item.slot==slot: found=item.get("requires_new_game",false) and item.get("compatibility_note","")!=""
	_check(found,"Save picker receives a clear old-save compatibility explanation",failures)
	_check(Store.save_world(world,slot).ok,"A new dollar world may replace an incompatible slot only with original preservation",failures)
	var preserved: String=path+".preserved-"+original
	_check(FileAccess.file_exists(preserved) and FileAccess.get_sha256(preserved)==original,"Replacing the slot keeps an independently named byte-exact original",failures)
	_check(Store.save_world(world,slot).ok and FileAccess.get_sha256(preserved)==original,"Further saves cannot overwrite the permanently preserved original",failures)
	loaded=Store.load_world(slot)
	_check(loaded.ok and Store.hash_state(loaded.state)==Store.hash_state(world),"Current USD world saves and loads without conversion or amount changes",failures)
	Store.delete_slot(slot)
	if FileAccess.file_exists(preserved): DirAccess.remove_absolute(ProjectSettings.globalize_path(preserved))
	# Unknown content, unknown formats and bad bytes must not be mistaken for
	# a successful currency migration or overwrite the current game session.
	old.content_hash="unrecognized-content"
	_write_snapshot(path,old)
	loaded=Store.load_world(slot)
	_check(not loaded.ok and loaded.get("compatibility_code")=="LEGACY_CONTENT_UNRECOGNIZED","Unknown old content has its own explicit compatibility result",failures)
	_write_snapshot(path,world,999)
	loaded=Store.load_world(slot)
	_check(not loaded.ok and loaded.error.contains("SAVE_FORMAT_UNSUPPORTED"),"Unknown save encoding is refused distinctly",failures)
	var future: Dictionary=world.duplicate(true)
	future.schema_version=999
	_write_snapshot(path,future)
	loaded=Store.load_world(slot)
	_check(not loaded.ok and loaded.error.contains("UNSUPPORTED_SCHEMA_VERSION"),"Unknown world schema is refused distinctly",failures)
	_write_snapshot(path,world,Store.FORMAT_VERSION,"bad-checksum")
	loaded=Store.load_world(slot)
	_check(not loaded.ok and loaded.error.contains("SAVE_CHECKSUM_MISMATCH"),"Damaged bytes are refused before currency compatibility is considered",failures)
	Store.delete_slot(slot)

static func run() -> Array:
	_checks=0
	var failures: Array=[]
	_check(Ledger.BASE_CURRENCY=="USD" and Ledger.MINOR_UNITS_PER_MAJOR==100,"Base contract is US dollars with 100 integer cents",failures)
	var s: Dictionary={"accounts":{},"journal":[]}
	_check(Ledger.opening(s,"alice",12345).ok and s.accounts["alice:cash"].currency=="USD","New ledger accounts use real USD",failures)
	_check(Ledger.balance(s,"alice:cash")==12345,"Changing the currency label never multiplies existing cent amounts by 100",failures)
	var snapshot: String=Store.hash_state(s)
	_check(not Ledger.open_account(s,"fake:cash","fake","cash","CR").ok and Store.hash_state(s)==snapshot,"New gameplay cannot create virtual CR accounts",failures)
	var blocked_legacy: Dictionary=_legacy_state()
	var legacy_hash: String=Store.hash_state(blocked_legacy)
	_check(not Ledger.post(blocked_legacy,"new-virtual-payment",[{"account":"legacy:cash","debit":1,"credit":0},{"account":"legacy:equity","debit":0,"credit":1}],"신규 가상통화 지급",1).ok and Store.hash_state(blocked_legacy)==legacy_hash,"New postings reject legacy virtual currencies without changing the ledger",failures)
	_check(not Ledger.opening(s,"fraction",0.1).ok,"A fraction of one cent cannot enter the ledger",failures)
	_check(Ledger.opening(s,"bob",1).ok and Ledger.transfer(s,"one-cent","alice","bob",1,"한 센트 지급",1).ok and Ledger.balance(s,"bob:cash")==2,"The smallest payment remains exactly one cent",failures)
	_check(Ledger.validate(s).is_empty(),"Dollar ledger balances and reconstructs",failures)
	var world: Dictionary=Economy.new_world(9123,{"cities_per_nation":1,"expanded_industries":false})
	if world.is_empty(): return failures+["Currency test world creation failed"]
	_check(world.schema_version==3 and world.rules_version=="experimental-0.2-usd","New dollar/unit rules have a separate saved-world schema",failures)
	_check(world.base_currency=="USD" and world.minor_units_per_major==100,"World explicitly records the dollar-cent money contract",failures)
	var default_dollars: bool=true
	for nation in world.nations:
		if nation.currency!="USD": default_dollars=false
	for company in world.companies:
		if world.accounts[str(company.id)+":cash"].currency!="USD": default_dollars=false
	for bank in world.finance.banks:
		if bank.currency!="USD" or not world.accounts.has(str(bank.id)+":reserve:USD"): default_dollars=false
	_check(default_dollars,"All default countries, operating wallets and bank settlement accounts use USD",failures)
	var invalid_bank: Dictionary=world.duplicate(true)
	invalid_bank.finance.banks[0].currency="CR"
	_check(not Finance.validate(invalid_bank).is_empty(),"Bank contract metadata cannot silently retain a legacy base currency",failures)
	var codes: Array=[]
	for currency in Finance.definition().currencies:
		codes.append(currency.id)
		_check(currency.minor_digits==2 and currency.iso_numeric in ["840","978","826"],"Foreign-currency metadata uses verified ISO4217 minor units: "+currency.id,failures)
	_check(codes==["USD","EUR","GBP"],"Optional FX uses real ISO currencies, separate from the dollar default world",failures)
	var cash_before: int=Finance.cash(world,"co-0")
	var bank: String=str(world.finance.owner_bank["co-0"])
	var money_before: Array=Finance.monetary_summary(world)
	var result: Dictionary=Finance.apply(world,_command(world,"currency-loan","finance_borrow",{"bank_id":bank,"amount":137,"term_months":12}),1)
	_check(result.ok and Finance.cash(world,"co-0")==cash_before+137 and world.finance.loans[-1].currency=="USD","A 137-cent loan creates exactly 137 USD cents and corresponding debt",failures)
	_check(Finance.apply(world,_command(world,"currency-repay","finance_repay",{"loan_id":world.finance.loans[-1].id,"amount":137}),1).ok and Finance.cash(world,"co-0")==cash_before,"Principal repayment destroys only its actual dollar cents",failures)
	_check(Finance.monetary_summary(world)==money_before,"Borrowing and repaying restore every currency's original positions",failures)
	var fx: Dictionary=_command(world,"currency-fx","finance_exchange",{"from_currency":"USD","to_currency":"EUR","amount":120})
	var reserve: Array=Finance.planning_reservations(world,fx)
	_check(reserve.size()==1 and reserve[0].resource_id=="USD" and int(reserve[0].quantity)>120,"FX reserves its real source currency plus fee in cents",failures)
	var before_fx: Array=Finance.monetary_summary(world)
	_check(Finance.apply(world,fx,1).ok and Finance.cash(world,"co-0","EUR")==100,"Finite EUR inventory is exchanged using the declared experimental rate",failures)
	var after_fx: Array=Finance.monetary_summary(world)
	for i in range(before_fx.size()):
		_check(before_fx[i].currency==after_fx[i].currency and before_fx[i].broad_money==after_fx[i].broad_money,"FX conserves each currency separately: "+str(before_fx[i].currency),failures)
	var planned: Dictionary=Economy.new_world(9124,{"cities_per_nation":1,"expanded_industries":false})
	var budget: int=int(Finance.cash(planned,"co-0")*3/4)
	bank=str(planned.finance.owner_bank["co-0"])
	var first: Dictionary=Economy.queue_command(planned,_command(planned,"dollar-reservation-one","finance_deposit",{"bank_id":bank,"amount":budget}))
	var second: Dictionary=Economy.queue_command(planned,_command(planned,"dollar-reservation-two","finance_deposit",{"bank_id":bank,"amount":budget}))
	_check(first.ok and not second.ok and str(second.error).contains("SELF_RESERVATION_EXCEEDS_CASH"),"Independent commands share one actual USD cash reservation limit",failures)
	var metrics: Dictionary=Economy.metrics(world)
	_check(metrics.valuation_currency=="USD" and metrics.cash_by_currency.has("USD") and not metrics.cash_by_currency.has("CR"),"Economic totals use USD keys and disclose their valuation currency",failures)
	_check(Economy.validate(world).is_empty(),"Dollar world financial and physical invariants pass",failures)
	_check_slot_compatibility(world,failures)
	print("Currency assertions: %d"%_checks)
	return failures
