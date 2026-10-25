extends RefCounted
## Independent regressions for cross-module budgets, rights and city labor.
const Economy = preload("res://core/economy.gd")
const Life = preload("res://core/lifecycle.gd")
const Ledger = preload("res://core/ledger.gd")
const Domain = preload("res://core/domain.gd")
static var checks: int = 0
static var serial: int = 0
static var baseline: Dictionary = {}

static func _check(value: bool, reason: String, failures: Array) -> void:
	checks += 1
	if not value: failures.append(reason)

static func _world() -> Dictionary:
	if baseline.is_empty(): baseline = Economy.new_world(774, {"cities_per_nation": 1, "expanded_industries": false})
	return baseline.duplicate(true)

static func _command(s: Dictionary, type: String, payload: Dictionary, actor: String = "p-0", owner: String = "co-0") -> Dictionary:
	serial += 1
	return {"id":"review-%d" % serial,"actor_id":actor,"acting_company_id":owner,"type":type,"payload":payload,"expected_version":s.version}

static func _submit(s: Dictionary, c: Dictionary, failures: Array) -> bool:
	var result: Dictionary = Economy.queue_command(s, c)
	_check(bool(result.ok), "Review command %s accepted: %s" % [c.type, result.get("error", "")], failures)
	return bool(result.ok)

static func _execute(s: Dictionary) -> void:
	Economy._execute_commands(s, int(s.planning_month))

static func run() -> Array:
	checks = 0
	var failures: Array = []
	_test_shared_reservations(failures)
	_test_delegation(failures)
	_test_runtime_office_rights(failures)
	_test_multicity_contract(failures)
	_test_contractor_atomicity(failures)
	_test_estate_batch_stockloss(failures)
	_test_contractual_payroll(failures)
	_test_payroll_arrears(failures)
	_test_authorized_training(failures)
	_test_observed_workload_staffing(failures)
	print("Independent core integration checks: %d" % checks)
	return failures

static func _test_shared_reservations(failures: Array) -> void:
	var s: Dictionary = _world()
	var trainee: Dictionary = Domain.find(s.people, "p-1")
	var tuition: int = int(Life._p().training_monthly_fee)
	var training: Dictionary = _command(s, "start_training", {"skill":"management","months":1,"budget":tuition}, str(trainee.id), "")
	if not _submit(s, training, failures): return
	var bank: String = str(s.finance.banks[0].id)
	var deposit: Dictionary = _command(s, "finance_deposit", {"bank_id":bank,"amount":Economy.cash(s,str(trainee.id))-tuition+1}, str(trainee.id), "")
	var before: String = Economy.state_hash(s)
	var result: Dictionary = Economy.queue_command(s, deposit)
	_check(not result.ok and str(result.error).begins_with("SELF_RESERVATION_EXCEEDS_CASH"), "Training and deposit share one personal cash ceiling", failures)
	_check(Economy.state_hash(s)==before, "Rejected cross-module plan changes no saved state", failures)
	var clone: Dictionary = s.duplicate(true)
	clone.commands.clear()
	_check(_submit(clone, deposit, failures), "A cloned independent plan rebuilds the transient reservation cache", failures)
	var stock: Dictionary = {}
	for lot in s.inventory:
		if lot.owner_id=="co-0" and lot.location!="shipment" and int(lot.quantity)-int(lot.reserved)>3:
			stock=lot
			break
	if stock.is_empty(): _check(false,"Fixture has finite seller stock",failures);return
	s.commands.clear()
	var available: int = Economy.available_quantity(s,"co-0",str(stock.product_id),str(stock.city_id))
	var payload: Dictionary = {"side":"sell","product_id":stock.product_id,"city_id":stock.city_id,"quantity":available-1,"limit_price":20,"expires_month":4,"latest_delivery_month":4}
	if not _submit(s,_command(s,"market_order",payload),failures):return
	payload.quantity=2
	result=Economy.queue_command(s,_command(s,"market_order",payload))
	_check(not result.ok and str(result.error).begins_with("SELF_RESERVATION_EXCEEDS_INVENTORY"),"Two pending sell orders cannot duplicate their own stock",failures)
	s.commands.clear()
	payload.quantity=available
	_check(_submit(s,_command(s,"market_order",payload),failures),"Removing pending commands releases only their own reservations",failures)

static func _test_delegation(failures: Array) -> void:
	var s: Dictionary = _world()
	var manager: Dictionary = Domain.find(s.people,"p-1")
	var grant: Dictionary = _command(s,"delegate_management",{"person_id":manager.id,"permissions":["post_job","build","set_policy"],"budget":100})
	if not _submit(s,grant,failures):return
	_execute(s)
	var mandate: Dictionary = s.lifecycle.delegations[-1]
	var job: Dictionary = _command(s,"post_job",{"role":"operator","wage":2,"hours":20,"skill":0},str(manager.id))
	if not _submit(s,job,failures):return
	_execute(s)
	mandate=Domain.find(s.lifecycle.delegations,str(mandate.id))
	_check(int(mandate.spent)==40,"A successful delegated command charges its actual bound exactly once",failures)
	var initial_cash: int=Economy.cash(s,str(manager.id))
	Life.monthly(s,1)
	_check(int(mandate.spent)==40,"Monthly work does not erase already charged delegated spending",failures)
	_check(Economy.cash(s,str(manager.id))==initial_cash+int(Life._p().delegation_monthly_hours)*int(manager.wage),"Delegated management uses paid actual employee hours",failures)
	_check(not Life.claim_delegated_budget(s,job,61),"Mandate rejects an aggregate spend above its monthly ceiling",failures)
	_check(int(mandate.spent)==40,"A failed budget claim leaves spent unchanged",failures)
	s.planning_month=2
	_check(Life.delegated_budget_remaining(s,mandate)==100,"The next planning month receives a fresh finite ceiling",failures)
	_check(Life.claim_delegated_budget(s,job,90),"First actual next-month claim initializes the mandate month",failures)
	Life.monthly(s,2)
	_check(int(mandate.month)==2 and int(mandate.spent)==90,"Next-month settlement preserves that month's charged spend",failures)
	# Insert a previously validated plan whose grant was reduced before execution.
	var site: Dictionary = Life._free_site(s,str(manager.city_id),"co-0")
	_check(not site.is_empty(),"The finite owned tutorial plot remains available",failures)
	if site.is_empty():return
	var build: Dictionary=_command(s,"build",{"city_id":manager.city_id,"kind":"bakery","x":site.x,"y":site.y},str(manager.id))
	var old_count: int=s.projects.size()
	mandate.budget=Economy.construction_budget("bakery")+100
	if not _submit(s,build,failures):return
	mandate.budget=100
	_execute(s)
	_check(s.projects.size()==old_count,"Execution rechecks a build's entire delegated bound even when no funded grant matches",failures)
	_check(s.events.any(func(e):return e.type=="command_failed" and e.get("command_id")==build.id),"Delegated-budget failure has an actual command failure event",failures)

static func _test_runtime_office_rights(failures: Array) -> void:
	var s: Dictionary=_world()
	var company: Dictionary=Domain.find(s.companies,"co-0")
	var old_policy: int=int(company.policy.production_percent)
	var policy: Dictionary=_command(s,"set_policy",{"production_percent":50})
	var dismissal: Dictionary=_command(s,"dismiss_director",{"company_id":"co-0"},"p-0","")
	while Economy._stable_key(s,"commands",str(dismissal.id))>=Economy._stable_key(s,"commands",str(policy.id)):
		dismissal=_command(s,"dismiss_director",{"company_id":"co-0"},"p-0","")
	if not _submit(s,policy,failures) or not _submit(s,dismissal,failures):return
	_execute(s)
	company=Domain.find(s.companies,"co-0")
	_check(str(company.director_id)=="","The controlling shareholder may dismiss their own executive office",failures)
	_check(int(company.policy.production_percent)==old_policy,"A later frozen legacy command loses authority after an earlier dismissal",failures)
	_check(Life.shares_owned(s,"p-0","co-0")>0,"Dismissal preserves the former director's shares",failures)
	_check(s.events.any(func(e):return e.type=="command_failed" and e.get("command_id")==policy.id and e.reason=="AUTHORITY_LOST_AT_EXECUTION"),"Loss of runtime authority has an explicit explanation",failures)

static func _test_multicity_contract(failures: Array) -> void:
	var s: Dictionary=_world()
	var buyer: Dictionary=Domain.find(s.companies,"co-0")
	var remote_company: Dictionary={}
	for co in s.companies:
		if co.city_id!=buyer.city_id:remote_company=co;break
	if remote_company.is_empty():_check(false,"Fixture has another actual city",failures);return
	var remote_city: String=str(remote_company.city_id)
	var former_director: String=str(remote_company.director_id)
	if not _submit(s,_command(s,"declare_bankruptcy",{},former_director,str(remote_company.id)),failures):return
	_execute(s)
	var listing: Dictionary={}
	for item in s.lifecycle.liquidation_assets:
		if item.estate_id==remote_company.id and item.kind=="facility":listing=item;break
	if listing.is_empty():_check(false,"Liquidation offers an actual remote facility",failures);return
	if not _submit(s,_command(s,"buy_liquidation_asset",{"asset_id":listing.id}),failures):return
	_execute(s)
	var candidate: Dictionary={}
	for person in s.people:
		if person.city_id==remote_city and person.role!="director":candidate=person;break
	if candidate.is_empty():_check(false,"Remote city has a consenting local candidate",failures);return
	var post: Dictionary=_command(s,"post_job",{"city_id":remote_city,"role":"operator","wage":3,"hours":24,"skill":0})
	if not _submit(s,post,failures):return
	_execute(s)
	var offer: Dictionary=s.lifecycle.job_offers[-1]
	_check(offer.city_id==remote_city,"The branch vacancy is attributed to its actual city",failures)
	if not _submit(s,_command(s,"apply_job",{"offer_id":offer.id},str(candidate.id),""),failures):return
	_execute(s)
	candidate=Domain.find(s.people,str(candidate.id))
	_check(candidate.company_id==buyer.id and candidate.city_id==remote_city,"Branch employment changes employer while keeping consented work location",failures)
	var contracts: Array=s.lifecycle.employment.filter(func(e):return e.person_id==candidate.id and e.status=="active")
	_check(contracts.size()==1 and contracts[0].city_id==remote_city,"The active work contract stores the branch city once",failures)
	s.monthly_labor={}
	var cash_before: int=Economy.cash(s,str(candidate.id))
	_check(Economy._work(s,"co-0",1,1,"operator",str(buyer.city_id)),"Headquarters can use its own local employee",failures)
	_check(int(s.monthly_labor.get(candidate.id,0))==0 and Economy.cash(s,str(candidate.id))==cash_before,"Headquarters work cannot consume or pay a distant branch worker",failures)
	_check(Economy._work(s,"co-0",1,1,"operator",remote_city),"The branch can use its own local employee",failures)
	_check(int(s.monthly_labor.get(candidate.id,0))==1 and Economy.cash(s,str(candidate.id))==cash_before+3,"Actual branch labor and contract wage agree",failures)
	_check(Ledger.validate(s).is_empty(),"Cross-city acquisition, hiring and wages retain balanced journals",failures)

static func _test_contractor_atomicity(failures: Array) -> void:
	var s: Dictionary=_world()
	var owner: Dictionary=Domain.find(s.companies,"co-0")
	var contractor: Dictionary=Economy._contractor(s,str(owner.city_id))
	_check(not contractor.is_empty(),"Construction has a real local contractor",failures)
	if contractor.is_empty():return
	# A valid finite shock: the contractor pays its existing cash out, then
	# its builder contracts are amended to a wage above the fixed service fee.
	var emptied: Dictionary=Ledger.transfer(s,"review-contracting-cash-shock",str(contractor.id),str(contractor.director_id),Economy.cash(s,str(contractor.id)),"시험 시공업체의 기존 현금 지급",1)
	_check(bool(emptied.ok),"Contractor cash shock moves existing money to an actual recipient",failures)
	for person in s.people:
		if person.company_id==contractor.id and person.role=="builder":
			person.wage=20
			for contract in s.lifecycle.employment:
				if contract.person_id==person.id and contract.status=="active":contract.wage=20
	var site: Dictionary=Life._free_site(s,str(owner.city_id),str(owner.id))
	_check(not site.is_empty(),"Atomic construction check has an owned unoccupied site",failures)
	if site.is_empty():return
	if not _submit(s,_command(s,"build",{"kind":"bakery","city_id":owner.city_id,"x":site.x,"y":site.y}),failures):return
	_execute(s)
	var project: Dictionary=s.projects[-1]
	var cash_before: int=Economy.cash(s,str(owner.id))
	var builder_cash: int=Economy.cash(s,str(contractor.id))
	Economy._construct(s,project,1)
	if int(project.progress)==0:
		var fees: int=0
		for event in s.events:
			if event.type=="payment" and event.get("payee_id")==contractor.id and event.reason=="공정 도급 대금":fees+=int(event.amount)
		_check(fees==0,"Failed contractor payroll cannot collect unearned construction progress fees",failures)
		_check(Economy.cash(s,str(contractor.id))==builder_cash,"A failed work leg does not enrich the contractor",failures)
	else:
		_check(int(project.spent)>=cash_before-Economy.cash(s,str(owner.id)),"Any successful construction fee and materials are tracked in project spending",failures)
	_check(Ledger.validate(s).is_empty(),"Construction failure keeps all remaining postings balanced",failures)

static func _test_estate_batch_stockloss(failures: Array) -> void:
	var s: Dictionary=_world()
	var stock: Dictionary={}
	for lot in s.inventory:
		if lot.owner_id=="co-0" and lot.location!="shipment" and int(lot.quantity)>2:stock=lot;break
	_check(not stock.is_empty(),"Estate regression has an actual inventory batch",failures)
	if stock.is_empty():return
	stock.born_month=0
	stock.expires_month=6
	stock.variant_id="estate-quality"
	stock.quality=83
	var original: Dictionary=stock.duplicate(true)
	var sell: Dictionary=_command(s,"market_order",{"side":"sell","product_id":stock.product_id,"city_id":stock.city_id,"quantity":1,"limit_price":5,"specification":"estate-quality","expires_month":4,"latest_delivery_month":4})
	if not _submit(s,sell,failures):return
	_execute(s)
	_check(int(Domain.find(s.inventory,str(original.id)).reserved)==1,"Confirmed sell order reserves the actual estate batch",failures)
	if not _submit(s,_command(s,"declare_bankruptcy",{}),failures):return
	_execute(s)
	_check(int(Domain.find(s.inventory,str(original.id)).reserved)==0,"Bankruptcy releases confirmed sell reservation before listing the estate",failures)
	_check(s.market.orders[0].status=="owner_liquidation","Confirmed owner order is closed with an explicit estate reason",failures)
	var listing: Dictionary={}
	for item in s.lifecycle.liquidation_assets:
		if item.kind=="inventory" and item.reference_id==original.id:listing=item;break
	if listing.is_empty():_check(false,"Physical estate batch has a sale listing",failures);return
	var buyer: Dictionary=Domain.find(s.companies,"co-1")
	var purchase: Dictionary=_command(s,"buy_liquidation_asset",{"asset_id":listing.id},str(buyer.director_id),str(buyer.id))
	if not _submit(s,purchase,failures):return
	_execute(s)
	var acquired: Dictionary=Domain.find(s.inventory,"lot:estate:"+str(purchase.id))
	_check(not acquired.is_empty(),"Paid estate inventory creates one distinct acquired batch",failures)
	if acquired.is_empty():return
	for key in ["born_month","expires_month","variant_id","quality"]:
		_check(acquired.get(key)==original.get(key),"Estate inventory preserves original "+key,failures)
	_check(acquired.city_id==original.city_id and acquired.owner_id==buyer.id,"Batch title changes without teleporting its actual city",failures)
	var doomed: Dictionary={}
	for item in s.lifecycle.liquidation_assets:
		if item.estate_id=="co-0" and item.kind=="inventory" and item.status=="listed":doomed=item;break
	if doomed.is_empty():_check(false,"Estate loss regression has another finite batch",failures);return
	var lot: Dictionary=Domain.find(s.inventory,str(doomed.reference_id))
	var loss: Dictionary=Domain.change_inventory(s,"co-0",str(lot.city_id),str(lot.product_id),-int(lot.quantity),"청산 자산의 실제 부패 손실",1,"spoilage")
	_check(bool(loss.ok),"Declared spoilage consumes only real estate stock",failures)
	var cash_before: int=Economy.cash(s,"co-0")
	Life.monthly(s,1)
	doomed=Domain.find(s.lifecycle.liquidation_assets,str(doomed.id))
	_check(doomed.status=="lost","A spoiled estate batch is removed from available sale listings",failures)
	_check(Economy.cash(s,"co-0")<=cash_before,"A lost estate batch never creates recovery cash",failures)
	_check(s.events.any(func(e):return e.type=="estate_stock_unavailable" and e.get("asset_id")==doomed.id),"Estate listing loss links to the actual referenced asset",failures)
	_check(Ledger.validate(s).is_empty(),"Estate reservation release, purchase and stock loss keep balanced journals",failures)

static func _cash_total(s: Dictionary) -> int:
	var total: int=0
	for account in s.accounts.values():
		if account.kind=="cash" and account.currency=="CR":total+=int(account.balance)
	return total

static func _test_contractual_payroll(failures: Array) -> void:
	var s: Dictionary=_world()
	var company: Dictionary=Domain.find(s.companies,"co-0")
	_check(Economy._work(s,"co-0",5,1,"operator",str(company.city_id)),"Payroll regression pays actual productive work first",failures)
	var labor: Array=s.events.filter(func(e):return e.type=="labor" and e.get("company_id")=="co-0" and e.get("wage_paid",false))
	if labor.is_empty():_check(false,"Actual labor exposes its paid wage amount",failures);return
	var person: Dictionary=Domain.find(s.people,str(labor[0].actor_id))
	var contract: Dictionary={}
	for item in s.lifecycle.employment:
		if item.person_id==person.id and item.status=="active":contract=item;break
	var actual: int=int(labor[0].amount)
	var promised: int=int(contract.guaranteed_hours)*int(contract.wage)
	var gov: String=str(s.public.governments[0].id)
	_check(bool(Domain.pay(s,"review-unrelated-public-income",gov,str(person.id),7,"외부 공공 근로 임금",1).ok),"Unrelated public income has an actual payer",failures)
	Domain.event(s,"labor","외부 공공 근로",1,{"actor_id":person.id,"company_id":gov,"amount":7,"hours":1,"wage_paid":true})
	var initial: int=Economy.cash(s,str(person.id))
	var money: int=_cash_total(s)
	var work_cost: int=Ledger.balance(s,"co-0:production_wip")
	var result: Dictionary=Life.pay_contractual_wages(s,1)
	_check(bool(result.ok),"Contractual guaranteed payroll posts successfully",failures)
	var record: Dictionary=s.lifecycle.payroll_accruals[str(contract.id)+"@1"]
	_check(int(record.actual_paid)==actual,"Only this employer's paid actual labor offsets its guarantee",failures)
	_check(int(record.top_up_due)==promised-actual,"Idle-hour top-up equals contractual promise less actual paid wage",failures)
	_check(Economy.cash(s,str(person.id))==initial+promised-actual,"Actual work and contractual top-up pay the monthly promise exactly once",failures)
	_check(_cash_total(s)==money,"Guaranteed wages transfer finite company cash without issuing money",failures)
	_check(Ledger.balance(s,"co-0:production_wip")==work_cost,"Paid idle capacity creates no productive work-in-progress asset",failures)
	var director: Dictionary=Domain.find(s.people,str(company.director_id))
	var director_contract: Dictionary={}
	for item in s.lifecycle.employment:
		if item.person_id==director.id and item.status=="active":director_contract=item;break
	_check(int(director_contract.guaranteed_hours)==6,"Executive employment guarantees only the explicit six management hours",failures)
	var before: String=Economy.state_hash(s)
	var repeated: Dictionary=Life.pay_contractual_wages(s,1)
	_check(repeated==result and Economy.state_hash(s)==before,"Repeated payroll for the same month changes no claims, cash, journal or events",failures)
	_check(Ledger.validate(s).is_empty() and Life.validate(s).is_empty(),"Guaranteed pay and wage receivable/payable accounts validate",failures)
	var account_count: int=s.accounts.size()
	s.planning_month=2
	_check(bool(Life.pay_contractual_wages(s,2).ok) and s.accounts.size()==account_count,"A second paid month reuses wage accounts instead of creating monthly claim accounts",failures)
	_check(Life.validate(s).is_empty(),"Two-month wage claims reconcile to aggregated employer/employee accounts",failures)
	_check(Life._floor_share(101,2,3)==67 and Life._floor_share(Ledger.MAX_INTEGER,4503599627370495,Ledger.MAX_INTEGER)==4503599627370495,"Pro-rata money allocation uses exact integer division at safe money bounds",failures)

static func _test_payroll_arrears(failures: Array) -> void:
	var s: Dictionary=_world()
	var company: Dictionary=Domain.find(s.companies,"co-0")
	var owner: String=str(company.director_id)
	var drain: int=Economy.cash(s,"co-0")-101
	_check(bool(Ledger.transfer(s,"review-payroll-cash-shortage","co-0",owner,drain,"유한 회사 자금의 사전 지급 충격",1).ok),"Payroll shortage moves existing company funds rather than deleting cash",failures)
	var money: int=_cash_total(s)
	var result: Dictionary=Life.pay_contractual_wages(s,1)
	_check(bool(result.ok),"Cash-short payroll records contractual arrears",failures)
	var claims: Array=s.lifecycle.wage_claims.filter(func(w):return w.employer_id=="co-0")
	var amount: int=0
	var paid: int=0
	var remaining: int=0
	for claim in claims:amount+=int(claim.amount);paid+=int(claim.paid);remaining+=int(claim.remaining)
	_check(paid==101 and Economy.cash(s,"co-0")==0,"Short employer pays only its available 101 minor currency units",failures)
	_check(remaining==amount-paid and remaining>0,"All unpaid promised wages remain explicit employee claims",failures)
	_check(_cash_total(s)==money,"Unpaid wages create claims and debts while cash stays conserved",failures)
	for claim in claims:
		var floor_paid: int=Life._floor_share(101,int(claim.amount),amount)
		_check(int(claim.paid)>=floor_paid and int(claim.paid)<=floor_paid+1,"Finite payroll is allocated proportionally with deterministic one-unit remainders",failures)
		_check(Ledger.balance(s,"co-0:wage_payable:"+str(claim.person_id))==-int(claim.remaining) and Ledger.balance(s,str(claim.person_id)+":wage_receivable:co-0")==int(claim.remaining),"Employee wage asset and employer liability are the same unpaid amount",failures)
	_check(Life._debt(s,"co-0")>=remaining,"Estate debt totals include all wage arrears",failures)
	var continuing: Dictionary=s.duplicate(true)
	var continuing_accounts: int=continuing.accounts.size()
	_check(bool(Ledger.transfer(continuing,"review-next-period-wage-funding","co-1","co-0",50,"차기 고용 임금의 유한 지급 재원",2).ok),"Next-period wage funding comes from an existing finite payer",failures)
	continuing.planning_month=2
	_check(bool(Life.pay_contractual_wages(continuing,2).ok),"An active employer handles prior arrears and a new contractual month together",failures)
	var continuing_claims: Array=continuing.lifecycle.wage_claims.filter(func(w):return w.employer_id=="co-0")
	var continued_paid: int=0
	for claim in continuing_claims:continued_paid+=int(claim.paid)
	_check(continued_paid==paid+50 and Economy.cash(continuing,"co-0")==0,"Old and new wage claims share only the actual next-period funds",failures)
	_check(continuing.accounts.size()==continuing_accounts,"Multiple unpaid months retain aggregate pair accounts without monthly account growth",failures)
	_check(Life.validate(continuing).is_empty() and Ledger.validate(continuing).is_empty(),"Multi-period wage claims reconcile to aggregate balances exactly",failures)
	if not _submit(s,_command(s,"declare_bankruptcy",{}),failures):return
	_execute(s)
	_check(s.lifecycle.wage_claims.filter(func(w):return w.employer_id=="co-0" and int(w.remaining)>0).size()==claims.size(),"Bankruptcy and dismissal preserve employee wage claims",failures)
	_check(bool(Ledger.transfer(s,"review-estate-wage-funding","co-1","co-0",50,"청산 자산 대금의 유한 지급 시험",2).ok),"Estate wage recovery uses an actual counterparty's finite cash",failures)
	Life.monthly(s,2)
	var after: int=0
	for claim in s.lifecycle.wage_claims:
		if claim.employer_id=="co-0":after+=int(claim.remaining)
	_check(after==remaining-50,"Next-period estate cash pays the retained employee claims before other debt",failures)
	_check(Life.settle_wage_claims(s,"co-0",50,2)==0,"A depleted estate cannot pay its employees a second time",failures)
	_check(Ledger.validate(s).is_empty() and Life.validate(s).is_empty(),"Partial pay, retained arrears and later estate repayment all validate",failures)

static func _test_authorized_training(failures: Array) -> void:
	var s: Dictionary=_world()
	var school: Dictionary={}
	for facility in s.facilities:
		if facility.kind=="school" and facility.city_id=="city-0":school=facility;break
	_check(not school.is_empty(),"Opening compact world contains a real finite local school",failures)
	if school.is_empty():return
	var provider: Dictionary=Domain.find(s.companies,str(school.company_id))
	var learner: Dictionary=Domain.find(s.people,"p-1")
	var policy: Dictionary=_command(s,"set_teaching_policy",{"enabled":false,"max_hours":16},str(provider.director_id),str(provider.id))
	_check(not Life.apply(s,_command(s,"set_teaching_policy",{"enabled":true,"max_hours":16},"p-0",str(provider.id)),1).ok,"A rival cannot authorize another school's instructors",failures)
	_check(bool(Life.apply(s,policy,1).ok),"The real school director may suspend instructional service",failures)
	var fee: int=int(Life._p().training_monthly_fee)
	var blocked: Dictionary=Life.apply(s,_command(s,"start_training",{"skill":"management","months":1,"budget":fee},str(learner.id),""),1)
	_check(not blocked.ok and blocked.error=="TRAINING_PROVIDER_UNAVAILABLE","Suspended school never falls back to bakery staff or player directors",failures)
	_check(bool(Life.apply(s,_command(s,"set_teaching_policy",{"enabled":true,"max_hours":16},str(provider.director_id),str(provider.id)),1).ok),"School director explicitly authorizes bounded instruction",failures)
	var enrolled: int=0
	for person in s.people:
		if person.city_id!="city-0" or person.id=="p-0" or person.company_id==provider.id or person.role=="director":continue
		if enrolled>=8:break
		var command: Dictionary=_command(s,"start_training",{"skill":"management","months":1,"budget":fee},str(person.id),"")
		if Life.apply(s,command,1).ok:enrolled+=1
	_check(enrolled==8,"Learners submit their own funded course requests",failures)
	var industry: Variant=load("res://core/industry.gd")
	industry.before_turn(s,1)
	s.monthly_labor={}
	for priority in [0,1]:
		for facility in s.facilities:
			if Economy._production_priority(s,facility)==priority:Economy._produce(s,facility,1)
	var paper_before: int=Domain.available_quantity(s,str(provider.id),"paper","city-0")
	var money_before: int=_cash_total(s)
	Life.monthly(s,1)
	var delivered: Dictionary=Life.deliver_training(s,1)
	_check(int(delivered.lessons)==4,"Two consenting instructors provide only their actual16-hour monthly limits",failures)
	_check(Life.training_batches_used(s,str(school.id),1)==4 and int(delivered.lessons)<=int(school.capacity),"Actual lessons reserve shared classroom batch capacity",failures)
	_check(Domain.available_quantity(s,str(provider.id),"paper","city-0")==paper_before-int(delivered.lessons),"Each delivered course consumes actual paper once",failures)
	_check(_cash_total(s)==money_before,"Tuition, power purchase and instructor wages conserve actual cash",failures)
	_check(int(s.monthly_labor.get("p-0",0))==0,"Other people's training never commandeers the player director",failures)
	_check(int(s.monthly_labor.get("p-1",0))<=int(Life._p().training_hours),"A bakery worker can spend only their own voluntary learner time",failures)
	for person_id in s.lifecycle.teaching_usage.teachers:
		var instructor: Dictionary=Domain.find(s.people,str(person_id))
		_check(instructor.company_id==provider.id and int(s.lifecycle.teaching_usage.teachers[person_id])<=16,"Instruction is bounded to staff of the authorized actual school",failures)
	var power_used: int=0
	for right in s.industry.service_entitlements:
		if right.owner_id==provider.id and right.product_id=="energy" and int(right.month)==1:power_used+=int(right.used)
	_check(power_used==int(delivered.lessons),"Training uses real purchased current-month power service",failures)
	var before: String=Economy.state_hash(s)
	Life.deliver_training(s,1)
	_check(Economy.state_hash(s)==before,"Repeating course delivery in a month cannot charge or teach twice",failures)
	_check(Life.validate(s).is_empty() and Ledger.validate(s).is_empty(),"Authorized teaching preserves contracts, finite time and balanced accounts",failures)
	var corrupt: Dictionary=s.duplicate(true)
	Domain.find(corrupt.companies,str(provider.id)).teaching_policy.enabled="true"
	_check(not Life.validate(corrupt).is_empty(),"Malformed teaching authorization is rejected before it can command a worker",failures)
	corrupt=s.duplicate(true)
	corrupt.lifecycle.teaching_usage.erase("teachers")
	_check(not Life.validate(corrupt).is_empty(),"Malformed teaching capacity fails validation before lesson execution",failures)
	var limited: Dictionary=_world()
	var limited_school: Dictionary=Domain.find(limited.facilities,str(school.id))
	limited_school.capacity=1
	for actor in ["p-1","p-2"]:
		_check(bool(Life.apply(limited,_command(limited,"start_training",{"skill":"management","months":1,"budget":fee},actor,""),1).ok),"Classroom-limited requests remain consented and funded",failures)
	industry.before_turn(limited,1)
	for priority in [0,1]:
		for facility in limited.facilities:
			if Economy._production_priority(limited,facility)==priority:Economy._produce(limited,facility,1)
	Life.monthly(limited,1)
	_check(int(Life.deliver_training(limited,1).lessons)==1,"Physical classroom capacity limits delivery even with another available instructor",failures)

static func _test_observed_workload_staffing(failures: Array) -> void:
	var s: Dictionary=_world()
	s.player_id="p-3"
	s.controlled_company_id="co-1"
	var crew: Array=s.people.filter(func(p):return p.company_id=="co-0" and p.role=="operator")
	for index in range(2,crew.size()):Life._end_employment(s,crew[index],1,"관측된 노동 부족 회귀 검사: 기존 직원 이직")
	var targets: Array=Life.staffing_targets(s,"co-0")
	_check(targets.size()==1 and int(targets[0].required_heads)==2,"No observed demand constraint keeps the existing minimum crew",failures)
	var idle: Array=Life.ai_commands(s).filter(func(c):return c.actor_id=="p-0" and c.acting_company_id=="co-0" and c.type=="post_job")
	_check(idle.is_empty(),"An idle two-person factory does not hire for theoretical capacity alone",failures)
	var industry: Variant=load("res://core/industry.gd")
	industry.before_turn(s,1)
	for priority in [0,1]:
		for facility in s.facilities:
			if Economy._production_priority(s,facility)==priority:Economy._produce(s,facility,1)
	var bakery: Dictionary={}
	for facility in s.facilities:
		if facility.company_id=="co-0" and facility.kind=="bakery":bakery=facility;break
	var opening_stock: int=Economy.available_quantity(s,"co-0","food","city-0")
	var bought: int=Economy._purchase(s,"co-1","city-0","food",opening_stock,1,"회귀 검사의 실제 유한 구매자 식량 비축")
	_check(bought==opening_stock and Economy.available_quantity(s,"co-0","food","city-0")==0,"A real funded buyer purchases the opening food buffer before staffing demand is tested",failures)
	Economy._produce(s,bakery,1)
	var observed: Array=s.events.filter(func(e):return e.type=="production_limit" and e.get("actor_id")=="co-0" and e.reason=="운영 인력 시간 부족")
	_check(not observed.is_empty(),"Two80-hour workers cause a real observed192-hour bakery workload limit",failures)
	s.last_report={"events":observed.duplicate(true)}
	targets=Life.staffing_targets(s,"co-0")
	_check(int(targets[0].workload_hours)==192 and int(targets[0].required_heads)==3,"Observed bakery labor shortage raises desired staffing from two to three",failures)
	var candidates: Array=Life.ai_commands(s).filter(func(c):return c.actor_id=="p-0" and c.acting_company_id=="co-0" and c.type=="post_job")
	_check(candidates.size()==1,"The actual short employer proposes one funded legal vacancy",failures)
	if candidates.is_empty():return
	_check(candidates[0].payload.city_id==bakery.city_id and candidates[0].payload.role=="operator","Vacancy targets its actual city and operational role",failures)
	if not _submit(s,candidates[0],failures):return
	_execute(s)
	var offer: Dictionary=s.lifecycle.job_offers[-1]
	if not _submit(s,_command(s,"apply_job",{"offer_id":offer.id},"p-4",""),failures):return
	_execute(s)
	targets=Life.staffing_targets(s,"co-0")
	_check(int(targets[0].employee_heads)==3,"A consenting third worker satisfies the observed labor requirement",failures)
	_check(Life.ai_commands(s).filter(func(c):return c.actor_id=="p-0" and c.acting_company_id=="co-0" and c.type=="post_job").is_empty(),"A fulfilled workload crew does not keep posting redundant vacancies",failures)
	var builder: Dictionary=Economy._contractor(s,"city-0")
	var builder_targets: Array=Life.staffing_targets(s,str(builder.id))
	_check(builder_targets.size()>0 and builder_targets[0].role=="builder" and int(builder_targets[0].required_heads)==2,"A builder facility without a production recipe preserves its two-person builder minimum",failures)
	var school: Dictionary={}
	for facility in s.facilities:
		if facility.kind=="school" and facility.city_id=="city-0":school=facility;break
	var school_targets: Array=Life.staffing_targets(s,str(school.company_id))
	_check(school_targets.size()==1 and int(school_targets[0].required_heads)==2,"An operating school retains its bounded two-person minimum",failures)
