extends HBoxContainer
## Exact decimal entry; floating-point values never become command money.
const Presentation = preload("res://ui/presentation.gd")
var edit: LineEdit
var prefix: Label
var minimum_cents: int = 0
var maximum_cents: int = Presentation.MAX_CENTS
var allow_negative: bool = false
var currency: String = "USD"

func _init() -> void:
	custom_minimum_size.y = 44
	size_flags_horizontal = Control.SIZE_EXPAND_FILL
	prefix = Label.new()
	prefix.text = "$"
	add_child(prefix)
	edit = LineEdit.new()
	edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	edit.custom_minimum_size.y = 44
	edit.virtual_keyboard_type = LineEdit.KEYBOARD_TYPE_NUMBER_DECIMAL
	edit.placeholder_text = "0.00"
	edit.text = "0.00"
	add_child(edit)

func set_currency(value: String) -> void:
	currency = value
	prefix.text = str({"USD":"$", "EUR":"€", "GBP":"£"}.get(value, value))
	edit.tooltip_text = "금액 입력: " + value + " · 소수점 두 자리까지"

func set_cents(value: int) -> void:
	edit.text = Presentation.decimal_cents(value)

func read_value() -> Dictionary:
	var result: Dictionary = Presentation.parse_cents(edit.text, allow_negative, currency)
	if not result.get("ok", false): return result
	if int(result.cents) < minimum_cents or int(result.cents) > maximum_cents:
		return {"ok":false,"error":"금액 범위: " + Presentation.money_full(minimum_cents, currency) + " ~ " + Presentation.money_full(maximum_cents, currency)}
	return result
