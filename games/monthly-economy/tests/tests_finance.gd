extends RefCounted
const Finance=preload("res://core/finance.gd")
const Ledger=preload("res://core/ledger.gd")
static var _serial: int=0
static var _checks: int=0

static func _world() -> Dictionary:
	var s: Dictionary={"schema_version":3,"planning_month":1,"last_resolved_month":0,"version":0,"phase":"Planning","accounts":{},"journal":[],"events":[],"nations":[],"cities":[],"companies":[],"people":[],"products":[{"id":"food","name":"식품","unit":"상자","price":10}],"inventory":[],"facilities":[],"commands":[]}
	for i in range(3):
		var city: String="city-%d"%i
		var nation: String="nation-%d"%i
		var company: String="co-%d"%i
		var person: String="p-%d"%i
		s.nations.append({"id":nation,"name":"국가%d"%i})
		s.cities.append({"id":city,"nation_id":nation})
		s.companies.append({"id":company,"city_id":city,"director_id":person,"shareholder_id":person,"policy":{"dividend_percent":0},"status":"active"})
		s.people.append({"id":person,"city_id":city,"last_income":0})
		s.facilities.append({"id":"facility-%d"%i,"company_id":company,"city_id":city})
		s.inventory.append({"id":"stock-%d"%i,"owner_id":company,"city_id":city,"product_id":"food","quantity":100,"reserved":0})
		Ledger.opening(s,person,500)
		Ledger.opening(s,company,10000)
	Finance.initialize(s)
	return s

static func _command(s: Dictionary, type: String, payload: Dictionary, actor: String="p-0", owner: String="co-0", month: int=1) -> Dictionary:
	_serial+=1
	return Finance.apply(s,{"id":"finance-test-%d"%_serial,"actor_id":actor,"acting_company_id":owner,"expected_version":s.version,"type":type,"payload":payload},month)

static func _check(condition: bool, label: String, failures: Array) -> void:
	_checks+=1
	if not condition: failures.append(label)

static func _valid(s: Dictionary, label: String, failures: Array) -> void:
	_check(Ledger.validate(s).is_empty(),label+" balanced/reconstructed ledger "+str(Ledger.validate(s)),failures)
	_check(Finance.validate(s).is_empty(),label+" financial contracts "+str(Finance.validate(s)),failures)

static func _money(s: Dictionary, currency: String="USD") -> int:
	var total: int=0
	for account in s.accounts.values():
		if account.kind=="cash" and account.currency==currency: total+=int(account.balance)
	return total

static func run() -> Array:
	_checks=0
	var failures: Array=[]
	var s: Dictionary=_world()
	_valid(s,"Opening monetary/stock state",failures)
	_check(s.finance.banks.size()==3 and s.finance.central_banks.size()==3,"Actual banks and central banks initialize",failures)
	var initial: int=_money(s)
	var borrowed: Dictionary=_command(s,"finance_borrow",{"bank_id":"bank-nation-0","amount":1000,"term_months":12})
	_check(borrowed.get("ok",false),"Loan executes "+str(borrowed),failures)
	_check(_money(s)==initial+1000,"Loan creates deposit money with debt instead of transfer revenue",failures)
	_check(Finance.debt_total(s,"co-0")==1000,"Loan borrower obligation exists",failures)
	_valid(s,"Loan origination",failures)
	var loan_id: String=str(s.finance.loans[0].id)
	_check(_command(s,"finance_repay",{"loan_id":loan_id,"amount":1000}).get("ok",false),"Principal repayment executes",failures)
	_check(_money(s)==initial and Finance.debt_total(s,"co-0")==0,"Principal repayment destroys its deposit and liability once",failures)
	_valid(s,"Principal repayment",failures)
	var before: String=JSON.stringify(s,"",true,true)
	var bad: Dictionary=_command(s,"finance_borrow",{"bank_id":"bank-nation-0","amount":1.5,"term_months":12})
	_check(not bad.get("ok",false) and JSON.stringify(s,"",true,true)==before,"Fractional money rejected without mutation",failures)
	bad=_command(s,"finance_borrow",{"bank_id":"bank-nation-0","amount":10,"term_months":12},"p-1","co-0")
	_check(not bad.get("ok",false) and JSON.stringify(s,"",true,true)==before,"Other company's credit cannot be used",failures)
	var primary_reserve: int=Ledger.balance(s,"bank-nation-0:reserve:USD")
	var foreign_reserve: int=Ledger.balance(s,"bank-nation-1:reserve:USD")
	_check(_command(s,"finance_borrow",{"bank_id":"bank-nation-1","amount":400,"term_months":12}).get("ok",false),"Other-bank loan executes",failures)
	_check(s.finance.owner_bank["co-0"]=="bank-nation-0","Loan does not silently move or duplicate existing deposits",failures)
	_check(Ledger.balance(s,"bank-nation-1:reserve:USD")==foreign_reserve-400 and Ledger.balance(s,"bank-nation-0:reserve:USD")==primary_reserve+400,"Interbank loan remits actual reserves",failures)
	loan_id=str(s.finance.loans[-1].id)
	_check(_command(s,"finance_repay",{"loan_id":loan_id,"amount":400}).get("ok",false),"Other-bank loan can be repaid",failures)
	_check(Ledger.balance(s,"bank-nation-1:reserve:USD")==foreign_reserve and Ledger.balance(s,"bank-nation-0:reserve:USD")==primary_reserve,"Cross-bank principal repayment restores actual settlement positions",failures)
	_valid(s,"Cross-bank loan",failures)
	_check(_command(s,"finance_deposit",{"bank_id":"bank-nation-1","amount":200}).get("ok",false),"Savings at another bank executes",failures)
	_check(Ledger.balance(s,"co-0:savings:bank-nation-1")==200,"Savings is distinct owned asset, not new income",failures)
	_check(_command(s,"finance_withdraw",{"bank_id":"bank-nation-1","amount":200}).get("ok",false),"Savings can be withdrawn",failures)
	_valid(s,"Savings transfer",failures)
	# An existing-share trade requires an actual seller offer. Corporate cash
	# is unchanged; only buyer/seller cash and their ownership positions move.
	var co_cash: int=Finance.cash(s,"co-0")
	bad=_command(s,"finance_trade_shares",{"company_id":"co-0","seller_id":"p-0","shares":10,"unit_price":10},"p-1","")
	_check(not bad.get("ok",false),"No compulsory trade of another person's private shares",failures)
	_check(_command(s,"finance_offer_shares",{"company_id":"co-0","shares":10,"unit_price":10,"term_months":2},"p-0","").get("ok",false),"Shareholder can make a priced offer",failures)
	_check(_command(s,"finance_trade_shares",{"company_id":"co-0","seller_id":"p-0","shares":10,"unit_price":10},"p-1","").get("ok",false),"Buyer fills consenting offer",failures)
	_check(Finance.shares_owned(s,"p-1","co-0")==10 and Finance.shares_owned(s,"p-0","co-0")==990,"Shares move once",failures)
	_check(Finance.cash(s,"co-0")==co_cash,"Secondary share consideration never becomes company sales",failures)
	_check(_command(s,"finance_offer_equity",{"shares":20,"unit_price":10,"term_months":2}).get("ok",false),"Company can offer new equity",failures)
	var issue: String=str(s.finance.equity_issues[0].id)
	_check(_command(s,"finance_subscribe_equity",{"issue_id":issue,"shares":10},"p-1","").get("ok",false),"Investor explicitly accepts issue and pays",failures)
	_check(Finance.cash(s,"co-0")==co_cash+100 and Finance.shares_outstanding(s,"co-0")==1010,"New share proceeds and issued ownership match",failures)
	_valid(s,"Equity primary/secondary",failures)
	_check(_command(s,"finance_issue_bond",{"amount":200,"interest_bps":50,"term_months":1}).get("ok",false),"Bond offer has terms",failures)
	var bond: String=str(s.finance.bonds[0].id)
	_check(_command(s,"finance_buy_bond",{"bond_id":bond,"amount":100},"p-2","").get("ok",false),"Finite investor money subscribes bond",failures)
	_check(s.finance.bonds[0].outstanding==100,"Bond promise is not issued money until investor pays",failures)
	Finance.monthly(s,2)
	_check(s.finance.bonds[0].outstanding==0 and s.finance.bonds[0].status=="repaid","Maturity repays actual principal and liability",failures)
	_valid(s,"Bond maturity",failures)
	# A verified physical loss is necessary for a claim. Its payment reduces
	# the insurer's finite cash; neither submitted policy nor claim prints cash.
	_check(_command(s,"finance_insure",{"asset_id":"facility-0","coverage":1000,"term_months":3}).get("ok",false),"Actual facility can be insured",failures)
	var policy: String=str(s.finance.policies[0].id)
	bad=_command(s,"finance_claim",{"policy_id":policy,"loss_event_id":"invented"})
	_check(not bad.get("ok",false),"Fabricated insurance loss is rejected",failures)
	Finance.inventory_flow(s,"co-0","food",-10,"사고 손실",1)
	s.inventory[0].quantity=90
	s.events.append({"id":"actual-loss","type":"loss","reason":"시험 사고","actor_id":"co-0","city_id":"city-0","month":1,"product_id":"food","quantity":10,"amount":0})
	var insurer_cash: int=Finance.cash(s,"insurer-nation-0")
	_check(_command(s,"finance_claim",{"policy_id":policy,"loss_event_id":"actual-loss"}).get("ok",false),"Verified covered loss creates and pays claim",failures)
	_check(Finance.cash(s,"insurer-nation-0")==insurer_cash-100,"Claim uses actual insurer funds",failures)
	_check(not _command(s,"finance_claim",{"policy_id":policy,"loss_event_id":"actual-loss"}).get("ok",false),"One loss cannot be claimed twice",failures)
	_valid(s,"Insurance claim",failures)
	var xr_before: int=_money(s,"EUR")
	var cr_before: int=_money(s,"USD")
	_check(_command(s,"finance_exchange",{"from_currency":"USD","to_currency":"EUR","amount":120}).get("ok",false),"Foreign currency with finite market maker executes",failures)
	_check(Finance.cash(s,"co-0","EUR")==100,"Rational conversion delivers actual foreign wallet units",failures)
	_check(_money(s,"EUR")==xr_before and _money(s,"USD")==cr_before,"FX conserves each currency separately including paid fee",failures)
	_valid(s,"FX trade",failures)
	# Accrual goods settlement and COGS are distinct from cash expenses.
	var purchase: Dictionary=Finance.settle_trade(s,"test-accrual-trade","p-0","co-0",20,"food",2,3)
	_check(purchase.get("ok",false),"Goods settlement uses owned stock and actual cash",failures)
	_check(Ledger.balance(s,"p-0:stock:food")==20,"Buyer capitalizes inventory rather than immediate expense",failures)
	_check(Ledger.balance(s,"co-0:cost_of_goods_sold")==20,"Seller recognizes actual inventory COGS",failures)
	Finance.reconcile_deposits(s,3)
	_valid(s,"Accrual trade",failures)
	_test_malformed_records(failures)
	_test_escrow(failures)
	_test_capital_permissions(failures)
	_test_cost_allocation(failures)
	_test_default_and_reservations(failures)
	print("Finance assertions: %d"%_checks)
	return failures

static func _test_capital_permissions(failures: Array) -> void:
	var s: Dictionary=_world()
	s.companies[0].director_id="p-1"
	var result: Dictionary=_command(s,"finance_offer_equity",{"shares":1000,"unit_price":1,"term_months":2},"p-1","co-0")
	_check(not result.get("ok",false) and result.get("error")=="CAPITAL_RESOLUTION_REQUIRED","Hired CEO cannot dilute the owner's shares",failures)
	result=_command(s,"finance_authorize_capital",{"company_id":"co-0","person_id":"p-1","mode":"equity","shares":20,"unit_price":10,"amount":1000,"term_months":2},"p-0","")
	_check(result.get("ok",false),"Controlling shareholder can grant bounded capital authority",failures)
	result=_command(s,"finance_offer_equity",{"shares":20,"unit_price":5,"term_months":2},"p-1","co-0")
	_check(not result.get("ok",false),"Capital mandate minimum issue price is enforced",failures)
	result=_command(s,"finance_offer_equity",{"shares":20,"unit_price":10,"term_months":2},"p-1","co-0")
	_check(result.get("ok",false),"Hired CEO can use an actual shareholder mandate",failures)
	result=_command(s,"finance_offer_equity",{"shares":1,"unit_price":10,"term_months":2},"p-1","co-0")
	_check(not result.get("ok",false),"A capital mandate cannot be spent twice",failures)
	var sold: Dictionary=Finance.settle_trade(s,"capital-test-real-sale","p-2","co-0",200,"food",10,1)
	_check(sold.get("ok",false),"A real sale can produce distributable profit",failures)
	s.inventory[0].quantity=90
	Finance.reconcile_deposits(s,1)
	_check(Finance.retained_earnings(s,"co-0")==100,"Retained profit subtracts inventory COGS",failures)
	_check(_command(s,"finance_authorize_capital",{"company_id":"co-0","person_id":"p-1","mode":"dividend","amount":50,"term_months":2},"p-0","").get("ok",false),"Owner can approve a bounded dividend",failures)
	var company_cash: int=Finance.cash(s,"co-0")
	_check(_command(s,"finance_dividend",{"amount":50},"p-1","co-0").get("ok",false),"Authorized CEO distributes actual retained profit",failures)
	_check(Finance.cash(s,"co-0")==company_cash-50 and Ledger.balance(s,"co-0:dividends")==50,"Dividend reduces equity and cash rather than operating expenses",failures)
	_check(not _command(s,"finance_dividend",{"amount":1},"p-1","co-0").get("ok",false),"Dividend authority is finite",failures)
	_valid(s,"Bounded capital authority",failures)

static func _test_cost_allocation(failures: Array) -> void:
	var s: Dictionary=_world()
	s.products.append({"id":"byproduct","name":"부산물","unit":"t","price":5,"kind":"material"})
	s.products.append({"id":"power","name":"전력","unit":"MWh","price":5,"kind":"service"})
	Ledger.transfer(s,"test-batch-pay","co-0","p-0",20,"근로 임금",1)
	Finance.record_labor(s,"co-0",20,1)
	Finance.inventory_flow(s,"co-0","food",-10,"공정 투입",1)
	s.inventory[0].quantity=90
	_check(Ledger.balance(s,"co-0:production_wip")==120,"Actual materials and paid production wages accumulate in WIP",failures)
	Finance.allocate_production(s,"co-0",{"food":6,"byproduct":4,"power":4},1)
	_check(Ledger.balance(s,"co-0:production_wip")==0,"Batch allocation exhausts exactly the incurred cost",failures)
	_check(Ledger.balance(s,"co-0:stock:food")==972 and Ledger.balance(s,"co-0:stock:byproduct")==24,"Multiple material outputs receive price-weighted costs",failures)
	_check(Ledger.balance(s,"co-0:service_production_cost")==24,"Service production cost is expensed in its period",failures)
	s.inventory[0].quantity=96
	Finance.inventory_flow(s,"co-0","food",-5,"공정 투입",1)
	s.inventory[0].quantity=91
	var growth_cost: int=Ledger.balance(s,"co-0:production_wip")
	Finance.allocate_production(s,"co-0",{"food":10},1,"growth-A")
	_check(Ledger.balance(s,"co-0:growth:growth-A")==growth_cost,"Delayed crops carry their actual consumed inputs",failures)
	var stock_before: int=Ledger.balance(s,"co-0:stock:food")
	Finance.complete_growth(s,"co-0","growth-A",{"food":10},3)
	_check(Ledger.balance(s,"co-0:growth:growth-A")==0 and Ledger.balance(s,"co-0:stock:food")==stock_before+growth_cost,"Harvest transfers existing biological cost rather than generating income",failures)
	Ledger.transfer(s,"project-A-paid","co-0","co-1",48,"공정 도급 대금",1)
	Finance.capitalize_construction_payment(s,"co-0",48,1,"project-A")
	Ledger.transfer(s,"project-B-paid","co-0","co-1",24,"공정 도급 대금",1)
	Finance.capitalize_construction_payment(s,"co-0",24,1,"project-B")
	Finance.finalize_construction(s,"co-0","project-A",2)
	_check(Ledger.balance(s,"co-0:facility:project-A")==48,"Finished project becomes its own asset",failures)
	_check(Ledger.balance(s,"co-0:construction_wip:project-B")==24,"Completing one project leaves another project's WIP intact",failures)
	Finance.reconcile_deposits(s,2)
	_valid(s,"Multi-output and project cost accounting",failures)

static func _test_default_and_reservations(failures: Array) -> void:
	var s: Dictionary=_world()
	_check(_command(s,"finance_borrow",{"bank_id":"bank-nation-0","amount":100,"term_months":1}).get("ok",false),"Short term debt fixture exists",failures)
	var loan: Dictionary=s.finance.loans[0]
	var drain: int=Finance.cash(s,"co-0")
	Ledger.transfer(s,"actual-business-loss","co-0","co-1",drain,"실제 실패 지출",1)
	Finance.reconcile_deposits(s,1)
	for month in range(1,7): Finance.monthly(s,month)
	_check(loan.status=="default" and int(loan.principal)==100,"Default preserves borrower debt",failures)
	_check(int(loan.provision)==100,"Default recognizes bank credit loss instead of deleting deposits",failures)
	_check(Finance.cash(s,"p-0")==500,"Unrelated depositor money is not deleted on another borrower's default",failures)
	_valid(s,"Default with loan/deposit counterpart",failures)
	Ledger.transfer(s,"genuine-recovery","co-1","co-0",200,"실제 채권 회수 재원",7)
	Finance.reconcile_deposits(s,7)
	var debt: int=Finance.debt_total(s,"co-0")
	_check(_command(s,"finance_repay",{"loan_id":loan.id,"amount":debt},"p-0","co-0",7).get("ok",false),"Recovered funds repay actual debt and overdue interest",failures)
	loan=s.finance.loans[0]
	_check(loan.status=="repaid" and int(loan.provision)==0,"Credit loss provision reverses only on genuine principal recovery",failures)
	_valid(s,"Credit recovery",failures)
	var reservation: Array=Finance.planning_reservations(s,{"type":"finance_exchange","actor_id":"p-0","acting_company_id":"co-0","payload":{"from_currency":"EUR","to_currency":"USD","amount":100}})
	_check(reservation.size()==1 and reservation[0].resource_id=="EUR" and int(reservation[0].quantity)>100,"FX reserves its source currency and actual fee only",failures)
	reservation=Finance.planning_reservations(s,{"type":"finance_borrow","actor_id":"p-0","acting_company_id":"co-0","payload":{"amount":100}})
	_check(reservation.is_empty(),"Unfunded future loan proceeds are not treated as already owned cash",failures)
	var actions: Dictionary={}
	for action in Finance.ui_actions(): actions[action.type]=true
	for type in Finance.command_types(): _check(actions.has(type),"Every finance command has a real UI descriptor: "+type,failures)
	var snapshot: String=JSON.stringify(s,"",true,true)
	Finance.ai_commands(s)
	_check(JSON.stringify(s,"",true,true)==snapshot,"Financial AI planning cannot mutate the world or preempt rival assets",failures)

static func _test_escrow(failures: Array) -> void:
	var s: Dictionary=_world()
	var before: Dictionary=Finance.monetary_summary(s)[0]
	var deposit: int=Ledger.balance(s,"bank-nation-0:deposit:co-0:USD")
	var reserve: int=Ledger.balance(s,"bank-nation-0:reserve:USD")
	Ledger.open_account(s,"co-0:market-escrow:test","co-0","asset")
	var result: Dictionary=Ledger.post(s,"escrow-funded",[{"account":"co-0:cash","debit":0,"credit":100},{"account":"co-0:market-escrow:test","debit":100,"credit":0}],"확정 매입 주문의 제한 예금",1)
	_check(result.ok,"Escrow moves only owned cash into a restricted deposit",failures)
	Finance.reconcile_deposits(s,1)
	_check(Ledger.balance(s,"bank-nation-0:deposit:co-0:USD")==deposit and Ledger.balance(s,"bank-nation-0:reserve:USD")==reserve,"Escrow does not destroy client deposits or transfer bank reserves",failures)
	var after: Dictionary=Finance.monetary_summary(s)[0]
	_check(after.broad_money==before.broad_money and after.restricted_deposits==100,"Broad money includes confirmed market escrow exactly once",failures)
	_valid(s,"Restricted market deposits",failures)

static func _test_malformed_records(failures: Array) -> void:
	var s: Dictionary=_world()
	_check(_command(s,"finance_borrow",{"bank_id":"bank-nation-0","amount":100,"term_months":1}).ok,"Malformed-record test has a real loan",failures)
	s.finance.loans[0].principal={"bad":"type"}
	_check(not Finance.validate(s).is_empty(),"Malformed loan principal returns a diagnostic before arithmetic",failures)
	s=_world()
	s.ownership[0].voting_shares={"bad":"type"}
	_check(not Finance.validate(s).is_empty(),"Malformed ownership returns a diagnostic before voting arithmetic",failures)
	s=_world()
	s.finance.currencies[0].denominator={"bad":"type"}
	_check(not Finance.validate(s).is_empty(),"Malformed currency denominator is rejected before conversion",failures)
