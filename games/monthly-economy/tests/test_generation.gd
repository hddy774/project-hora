extends RefCounted
const Generation=preload("res://core/generation.gd")
const Economy=preload("res://core/economy.gd")
static var checks: int=0

static func _check(condition: bool, message: String, failures: Array) -> void:
	checks+=1
	if not condition: failures.append(message)

static func run() -> Array:
	return _run(3)

static func run_quick() -> Array:
	return _run(1)

static func _run(preview: int) -> Array:
	checks=0
	var failures: Array=[]
	var config: Dictionary=Generation.configuration()
	_check(Generation.validate_configuration(config).is_empty(),"Generation parameters are explicit valid experimental data",failures)
	var missing: Dictionary=config.duplicate(true)
	missing.erase("preview_months")
	_check(not Generation.validate_configuration(missing).is_empty(),"Missing preview tuning cannot silently become an invented default",failures)
	var s: Dictionary=Economy.new_world(42,{"cities_per_nation":1,"expanded_industries":false})
	var before: String=Economy.state_hash(s)
	var structural: Dictionary=Generation.validate(s,{"preview_months":0})
	_check(structural.ok and structural.observation.months==0,"Readiness can be tested separately from preview execution",failures)
	_check(structural.observation.essential_access.size()==s.cities.size()*config.essential_products.size(),"Every populated city's actual essential path is checked",failures)
	_check("STRUCTURAL_CHECK_ONLY_NO_PREVIEW_MONTHS" in structural.warnings,"Structural-only results cannot claim observed economic stability",failures)
	var observed: Dictionary=Generation.validate(s,{"preview_months":preview})
	_check(observed.ok,"Finite copied monthly preview completes: "+str(observed.errors),failures)
	_check(int(observed.observation.months)==preview and observed.observation.turns.size()==preview,"Readiness evidence records exactly the months actually resolved",failures)
	_check(observed.fingerprint==before and Economy.state_hash(s)==before,"Preview preserves the untouched month-one world, plans and accounts",failures)
	_check(s.planning_month==1 and s.last_resolved_month==0 and s.events.size()==Economy.new_world(42,{"cities_per_nation":1,"expanded_industries":false}).events.size(),"Loading a generated world never commits preview transactions",failures)
	_check("SHORT_OBSERVATION_NOT_LONG_RUN_BALANCE" in observed.warnings,"A successful short preview is explicitly distinguished from long-run balance",failures)
	var again: Dictionary=Generation.validate(s,{"preview_months":preview})
	_check(observed.assessment_hash==again.assessment_hash,"Same seed/world/preview has reproducible evidence and outcome hashes",failures)
	# Existing opening stock cannot mask the absence of replenishing food.
	var broken: Dictionary=s.duplicate(true)
	for facility in broken.facilities:
		if facility.recipe_id=="bake": facility.status="closed"
	var rejected: Dictionary=Generation.validate(broken,{"preview_months":0})
	_check(not rejected.ok and rejected.errors.any(func(e): return str(e).begins_with("ESSENTIAL_SUPPLY_PATH_MISSING:")),"Finite food buffers do not certify a world with no renewable food producer",failures)
	_check(int(rejected.observation.months)==0,"Structural failure stops preview and reports no invented successful months",failures)
	var upstream: Dictionary=s.duplicate(true)
	for facility in upstream.facilities:
		if facility.recipe_id=="grow": facility.status="closed"
	var dependency: Dictionary=Generation.validate(upstream,{"preview_months":0})
	_check(not dependency.ok,"Missing upstream grain replenishment propagates through flour to essential food readiness",failures)
	var bad_bank: Dictionary=s.duplicate(true)
	bad_bank.finance.owner_bank.erase("p-0")
	var mismatched: Dictionary=Generation.validate(bad_bank,{"preview_months":0})
	_check(not mismatched.ok,"Customer money must retain its actual bank/deposit relationship",failures)
	var unreasonable: Dictionary=Generation.validate(s,{"preview_months":int(config.maximum_preview_months)+1})
	_check(not unreasonable.ok and unreasonable.observation.months==0,"Invalid preview horizon fails before world mutation or execution",failures)
	print("Generation checks: "+str(checks)+"; failures: "+str(failures.size()))
	return failures
