extends SceneTree
const P = preload("res://ui/presentation.gd")
const Money = preload("res://ui/money_input.gd")
var failures: Array = []
var checks: int = 0
func check(actual: Variant, expected: Variant, label: String) -> void:
	checks += 1
	if actual != expected: failures.append(label+": "+str(actual)+" != "+str(expected))
func _init() -> void: call_deferred("run")
func run() -> void:
	check(P.money_full(123456),"$1,234.56","Exact cents")
	check(P.money_full(-1),"-$0.01","Negative cent")
	check(P.money(99999999,"USD",true),"$999,999.99","Below M")
	check(P.money(100000000,"USD",true),"$1M","Exact M")
	check(P.money(99999999999,"USD",true),"$999.99M","No false B rounding")
	check(P.money(100000000000,"USD",true),"$1B","Exact B")
	check(P.money(-123456789,"USD",true),"-$1.23M","Negative M")
	check(P.won(100000000),"10억원","M to KRW")
	check(P.won(100000000000),"1조원","B to KRW")
	check(P.won(1,false),"₩10","Fixed cents to won")
	check(P.won(-123,false),"-₩1,230","Negative won")
	for pair: Array in [["0.01",1],["1,234.56",123456],["$12.30",1230],[".50",50],["90071992547409.91",P.MAX_CENTS]]:
		var parsed: Dictionary = P.parse_cents(pair[0]); check(parsed.get("ok"),true,"Parse "+pair[0]); check(parsed.get("cents"),pair[1],"Cent precision "+pair[0])
	for text: String in ["1.234","NaN","1e6","--1","-1","90071992547409.92","1.2.3","+1",".","€10","GBP10"]: check(P.parse_cents(text).get("ok"),false,"Reject "+text)
	check(P.parse_cents("-1.23",true).cents,-123,"Signed converter")
	check(P.parse_cents("-$1.23",true).cents,-123,"Signed formatted copy")
	check(P.parse_cents("€10",false,"EUR").cents,1000,"Selected EUR input")
	check(P.cargo_mass(1),"1 g","Sub-kg mass")
	check(P.cargo_mass(-1001),"-1 kg","Negative mass")
	check(P.is_money_field("market_order","limit_price"),true,"Unit price is money")
	check(P.is_money_field("market_order","quantity"),false,"Quantity is not money")
	check(P.is_money_field("finance_issue_bond","interest_bps"),false,"Rate is not money")
	var input = Money.new(); root.add_child(input); input.set_cents(123456)
	check(input.edit.text,"1234.56","Input dollars")
	input.edit.text="12.34";check(input.read_value().cents,1234,"Input payload cents")
	input.edit.text="12.345";check(input.read_value().ok,false,"Input excessive precision rejected")
	input.maximum_cents=100;input.edit.text="1.01";check(input.read_value().ok,false,"Input range in cents")
	input.queue_free(); await process_frame
	for failure in failures: push_error(failure)
	print("Presentation: ",checks," checks, ",failures.size()," failures")
	quit(0 if failures.is_empty() else 1)
