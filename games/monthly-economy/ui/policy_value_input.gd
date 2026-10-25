extends VBoxContainer
const MoneyInput = preload("res://ui/money_input.gd")
var amount: HBoxContainer
var enabled: CheckButton
var hint: Label
var policy: String = "tax_bps"
func _init() -> void:
	amount = MoneyInput.new()
	amount.maximum_cents = 5000
	amount.set_cents(500)
	add_child(amount)
	enabled = CheckButton.new();enabled.text = "시행";enabled.custom_minimum_size.y=44;add_child(enabled)
	hint = Label.new();hint.autowrap_mode=TextServer.AUTOWRAP_WORD_SMART;add_child(hint)
	set_policy(policy)
func set_policy(value: String) -> void:
	policy = value
	var flag: bool = policy in ["import_ban","export_ban","mobilization"]
	amount.visible = not flag;enabled.visible = flag
	if flag: hint.text = "켜면 시행, 끄면 해제합니다."
	elif policy.ends_with("_bps"):
		amount.prefix.text = "%"
		amount.edit.tooltip_text = "비율(%) · 0.01% = 1bp(베이시스포인트)"
		hint.text = "퍼센트(%)로 입력 · 0.01% = 1bp"
	else:
		amount.set_currency("USD")
		hint.text = "월 예산을 달러로 입력합니다. 원화 보기는 입력액에 영향을 주지 않습니다."
func read_value() -> Dictionary:
	if policy in ["import_ban","export_ban","mobilization"]: return {"ok":true,"cents":1 if enabled.button_pressed else 0}
	return amount.read_value()
