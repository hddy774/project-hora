extends RefCounted
## Display conversions only. The simulation continues to store integer cents.
const KRW_PER_USD: int = 1000
const MAX_CENTS: int = 9007199254740991
const MONEY_FIELDS: Dictionary = {
	"create_company":["capital"], "create_subsidiary":["capital"], "post_job":["wage"], "hire_person":["wage"], "start_training":["budget"], "delegate_management":["budget"], "offer_acquisition":["amount"],
	"finance_borrow":["amount"], "finance_repay":["amount"], "finance_deposit":["amount"], "finance_withdraw":["amount"], "finance_dividend":["amount"], "finance_issue_bond":["amount"], "finance_buy_bond":["amount"], "finance_exchange":["amount"], "finance_insure":["coverage"], "finance_issue_shares":["unit_price"], "finance_trade_shares":["unit_price"], "finance_offer_shares":["unit_price"], "finance_offer_equity":["unit_price"], "finance_authorize_capital":["unit_price","amount"],
	"industry_research":["monthly_budget"], "industry_start_research":["monthly_budget"], "industry_start_software":["price"], "industry_release_software":["price"], "industry_software":["price"], "industry_advertise":["budget"], "market_order":["limit_price"], "land_offer_sale":["price"], "land_offer_lease":["price"], "land_pay_rent":["amount"],
	"public_donate":["amount"], "public_set_budget":["amount"], "public_transfer":["amount"], "public_announce_procurement":["max_unit_price"], "public_bid":["unit_price"]
}

static func grouped(value: int) -> String:
	var raw: String = str(absi(value))
	var result: String = ""
	for i: int in range(raw.length()):
		if i > 0 and (raw.length() - i) % 3 == 0: result += ","
		result += raw[i]
	return ("-" if value < 0 else "") + result

static func decimal_cents(cents: int, grouping: bool = false) -> String:
	var absolute: int = absi(cents)
	@warning_ignore("integer_division")
	var whole: int = absolute / 100
	return ("-" if cents < 0 else "") + (grouped(whole) if grouping else str(whole)) + "." + str(absolute % 100).pad_zeros(2)

static func money_full(cents: int, currency: String = "USD") -> String:
	var symbol: String = str({"USD":"$", "EUR":"€", "GBP":"£"}.get(currency, currency + " "))
	return ("-" if cents < 0 else "") + symbol + decimal_cents(absi(cents), true)

static func _truncated_scale(value: int, scale: int) -> String:
	@warning_ignore("integer_division")
	var whole: int = value / scale
	@warning_ignore("integer_division")
	var fraction: int = (value % scale) / maxi(scale / 100, 1)
	if fraction == 0: return str(whole)
	return str(whole) + "." + str(fraction).pad_zeros(2).trim_suffix("0")

static func money(cents: int, currency: String = "USD", compact: bool = false, view: String = "USD") -> String:
	if view == "KRW" and currency == "USD": return won(cents, compact)
	if not compact or absi(cents) < 100000000: return money_full(cents, currency)
	var scale: int = 100000000000 if absi(cents) >= 100000000000 else 100000000
	var suffix: String = "B" if scale == 100000000000 else "M"
	var symbol: String = str({"USD":"$", "EUR":"€", "GBP":"£"}.get(currency, currency + " "))
	return ("-" if cents < 0 else "") + symbol + _truncated_scale(absi(cents), scale) + suffix

static func won(cents: int, compact: bool = true) -> String:
	# 1 cent = 10 won under the user's fixed display conversion.
	var value: int = absi(cents) * (KRW_PER_USD / 100)
	var sign_value: String = "-" if cents < 0 else ""
	if not compact or value < 10000: return sign_value + "₩" + grouped(value)
	for unit: Dictionary in [{"scale":1000000000000,"label":"조원"},{"scale":100000000,"label":"억원"},{"scale":10000,"label":"만원"}]:
		if value >= int(unit.scale): return sign_value + _truncated_scale(value, int(unit.scale)) + str(unit.label)
	return sign_value + "₩" + grouped(value)

static func money_detail(cents: int, currency: String = "USD") -> String:
	var text: String = money_full(cents, currency)
	if currency == "USD": text += " · " + won(cents, false) + "\n표시용 고정 환산: 1달러 = 1,000원 · 실제 환전 아님"
	return text

static func parse_cents(value: String, allow_negative: bool = false, currency: String = "USD") -> Dictionary:
	var clean: String = value.strip_edges().replace(",", "")
	var negative: bool = clean.begins_with("-")
	if negative: clean = clean.substr(1).strip_edges()
	var allowed: Array = {"USD":["USD","$"],"EUR":["EUR","€"],"GBP":["GBP","£"]}.get(currency,[currency])
	for prefix: String in allowed: clean = clean.trim_prefix(prefix).strip_edges()
	if clean.begins_with("-") and not negative: negative = true;clean=clean.substr(1)
	if negative and not allow_negative: return {"ok":false,"error":"0 이상의 금액을 입력하세요."}
	if clean == "." or clean == "" or clean.count(".") > 1: return {"ok":false,"error":currency + " 금액을 소수점 두 자리까지 입력하세요."}
	var parts: PackedStringArray = clean.split(".", true)
	var whole: String = parts[0]
	var fraction: String = parts[1] if parts.size() == 2 else ""
	if whole == "": whole = "0"
	if not whole.is_valid_int() or whole.contains("+") or whole.contains("-") or (fraction != "" and not fraction.is_valid_int()) or fraction.contains("+") or fraction.contains("-") or fraction.length() > 2:
		return {"ok":false,"error":"선택한 통화의 숫자만 입력하고, 소수는 두 자리까지 사용하세요."}
	whole = whole.lstrip("0")
	if whole == "": whole = "0"
	if whole.length() > 14: return {"ok":false,"error":"입력할 수 있는 금액 범위를 넘었습니다."}
	var cents: int = int(whole) * 100 + int(fraction.rpad(2, "0"))
	if cents > MAX_CENTS: return {"ok":false,"error":"입력할 수 있는 금액 범위를 넘었습니다."}
	return {"ok":true,"cents":-cents if negative else cents}

static func is_money_field(command: String, key: String, descriptor: Dictionary = {}) -> bool:
	return str(descriptor.get("kind", "")) == "money" or key in MONEY_FIELDS.get(command, [])

static func unit(world: Dictionary, product_id: String) -> String:
	for product: Dictionary in world.get("products", []):
		if str(product.get("id", "")) == product_id:
			return str(product.get("unit", "개")) + ("·월" if str(product.get("service_period", "")) == "month" else "")
	return "단위 확인 불가"

static func quantity(world: Dictionary, product_id: String, amount: int) -> String:
	return grouped(amount) + " " + unit(world, product_id)

static func unit_price(world: Dictionary, product_id: String, cents: int, view: String = "USD") -> String:
	return money(cents, "USD", false, view) + " / 1 " + unit(world, product_id)

static func cargo_mass(grams: int) -> String:
	var sign_value: String = "-" if grams < 0 else ""
	if absi(grams) < 1000: return str(grams) + " g"
	if absi(grams) >= 1000000: return sign_value + _truncated_scale(absi(grams), 1000000) + " t"
	return sign_value + _truncated_scale(absi(grams), 1000) + " kg"
