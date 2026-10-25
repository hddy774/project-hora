extends RefCounted
## Forms are built from executable module command descriptors, never mock actions.
const MODULE_PATHS: Dictionary = {"finance": "res://core/finance.gd", "lifecycle": "res://core/lifecycle.gd", "public": "res://core/public_sector.gd", "industry": "res://core/industry.gd", "market": "res://core/market.gd", "land": "res://core/land.gd"}
const LABELS: Dictionary = {"finance": "금융과 자산", "lifecycle": "인물과 기업", "public": "정치·외교·공공조달", "industry": "산업·연구·디지털", "market": "상품 주문과 계약", "land": "토지·임대·용도"}
const SOURCE_PATHS: Dictionary = {
	"land_offers": ["land", "offers"], "land_leases": ["land", "leases"], "land_claims": ["land", "claims"], "own_orders": ["market", "orders"], "fx_rates": ["finance", "fx_rates"], "banks": ["finance", "banks"], "loans": ["finance", "loans"], "bonds": ["finance", "bonds"], "insurance": ["finance", "policies"], "insurance_policies": ["finance", "policies"], "loss_events": ["finance", "loss_events"], "share_offers": ["finance", "share_offers"], "equity_issues": ["finance", "equity_issues"],
	"employment": ["lifecycle", "employment"], "wage_claims": ["lifecycle", "wage_claims"], "jobs": ["lifecycle", "job_offers"], "job_offers": ["lifecycle", "job_offers"], "training": ["lifecycle", "training"], "acquisitions": ["lifecycle", "acquisitions"], "assets": ["lifecycle", "liquidation_assets"], "delegations": ["lifecycle", "delegations"],
	"governments": ["public", "governments"], "offices": ["public", "offices"], "parties": ["public", "parties"], "candidates": ["public", "candidates"], "policies": ["public", "policy_proposals"], "policy_proposals": ["public", "policy_proposals"], "procurements": ["public", "procurements"], "bids": ["public", "bids"], "deliveries": ["public", "deliveries"], "treaties": ["public", "treaties"], "armies": ["public", "armies"],
	"research": ["industry", "research"], "software": ["industry", "software"], "licenses": ["industry", "licenses"], "subscriptions": ["industry", "subscriptions"], "routes": ["industry", "routes"], "warehouses": ["industry", "warehouses"], "agriculture": ["industry", "agriculture"]
}

static func module_script(name: String) -> Variant:
	var path: String = str(MODULE_PATHS.get(name, ""))
	return load(path) if path != "" and ResourceLoader.exists(path) else null

static func descriptors(name: String) -> Array:
	var script: Variant = module_script(name)
	if script != null and script.has_method("ui_actions"):
		var values: Variant = script.ui_actions()
		if values is Array: return values
	return []

static func page(app: Control, module_name: String, title: String, action_filter: Array = [], records_source: String = "", owner_override: String = "") -> void:
	var card: VBoxContainer = app._card(app.content, title, str(LABELS.get(module_name, title)))
	var script: Variant = module_script(module_name)
	var owner: String = str(app._company().get("id", app.world.get("player_id", "")))
	if owner == "": owner = str(app.world.get("player_id", ""))
	if module_name == "finance": owner = app._finance_owner()
	if owner_override != "": owner = owner_override
	if script != null and script.has_method("summary"):
		var result: Variant = script.summary(app.world, str(app.world.get("player_id", "")) if module_name == "public" else owner)
		if result is Dictionary:
			if module_name == "public": app._label(card, "공공부문 전체 집계 · 각 정부 예산과 기관 장부의 합계", 12, app.MUTED)
			_summary(app, card, result)
	var actions: Array = descriptors(module_name)
	var visible: int = 0
	for action: Dictionary in actions:
		var type: String = str(action.get("type", ""))
		if not action_filter.is_empty() and not _matches(type, action_filter): continue
		visible += 1
		var item: Dictionary = action.duplicate(true)
		if owner_override != "": item["ui_owner_id"] = owner_override
		app._button(card, str(action.get("label", action.get("name", type))) + "  ›", func(): form(app, module_name, item))
	if visible == 0:
		app._label(card, "현재 선택한 항목의 실행 가능한 명령이 없습니다. 다른 역할이나 대상을 선택해 확인하세요.", 13, app.MUTED)
	if records_source != "": records(app, module_name, records_source, "기록과 계약", owner_override)

static func _matches(type: String, filters: Array) -> bool:
	for token: String in filters:
		if type == token or type.contains(token): return true
	return false

static func _summary(app: Control, parent: Node, summary: Dictionary) -> void:
	var shown: int = 0
	var labels: Dictionary = {
		"cash": "현금", "savings": "예금", "debt": "채무 원금", "retained_earnings": "유보 이익", "loan_principal": "대출 원금", "principal": "원금", "arrears": "연체", "loans": "대출", "holdings": "보유 권리", "bonds": "채권", "policies": "보험 계약", "employment": "고용", "employed": "고용 인원", "projects": "프로젝트", "research": "연구", "software": "소프트웨어", "licenses": "사용권", "subscriptions": "구독", "budget": "예산", "treasury": "공공 자금", "procurements": "조달", "contracts": "계약", "office": "공직", "offices": "공직", "ownership": "소유 지분", "training": "교육", "routes": "운송 경로", "service_capacity": "서비스 처리능력", "research_active": "진행 중인 연구", "software_released": "출시 제품", "owned_licenses": "보유 사용권", "active_seats": "이용 좌석", "growing_projects": "생육 프로젝트", "storage_capacity": "창고 수용량", "storage_used": "창고 사용량", "route_count": "세계 운송 경로", "reserved": "예약 예산", "services": "제공 서비스", "tax_revenue": "당월 세수"
	}
	for key in summary:
		if shown >= 9: break
		var value: Variant = summary[key]
		if value is Dictionary or value is Array: continue
		if str(key) in ["owner_id", "person_id", "company_id", "id", "private", "error", "ok"]: continue
		var label: String = str(labels.get(str(key), _humanize(str(key))))
		var text: String = str(value)
		if str(key) in ["cash", "savings", "loan_principal", "principal", "arrears", "budget", "treasury", "debt", "retained_earnings"] and (value is int or value is float): text = app._money(int(value))
		app._row(parent, label, text)
		shown += 1

	for wallet: Dictionary in summary.get("wallets", []):
		app._row(parent, "보유 " + str(wallet.get("currency", "")), app._money(int(wallet.get("amount", 0))).trim_suffix(" CR") + " " + str(wallet.get("currency", "")))

static func all_actions(app: Control) -> void:
	var box: VBoxContainer = app._open_modal("할 수 있는 행동")
	app._label(box, "선택한 인물의 자격·직위·소유권과 실제 예산으로 검증합니다. 명령은 다음 월 계획에 추가됩니다.", 13, app.MUTED)
	for name: String in MODULE_PATHS:
		var actions: Array = descriptors(name)
		if actions.is_empty(): continue
		app._label(box, str(LABELS[name]), 18, app.MINT)
		for action: Dictionary in actions:
			var item: Dictionary = action.duplicate(true)
			var module_name: String = name
			app._button(box, str(item.get("label", item.get("type", "행동"))), func(): form(app, module_name, item))
	app._button(box, "닫기", app._close_modal)

static func form(app: Control, module_name: String, action: Dictionary) -> void:
	if app.resolving: return
	var box: VBoxContainer = app._open_modal(str(action.get("label", action.get("type", "명령"))))
	app._label(box, str(action.get("description", "권한·계약·현금·시간을 검사한 후 다음 월 계획에 추가합니다.")), 13, app.MUTED)
	var context := OptionButton.new()
	context.fit_to_longest_item = false
	context.clip_text = true
	context.custom_minimum_size.y = 44
	var owner_ids: Array = []
	var scope: String = str(action.get("scope", "company"))
	if str(action.get("ui_owner_id", "")) == str(app.world.get("player_id", "")): scope = "personal"
	if scope in ["personal", "owner", "public", "both", "any"] or module_name == "finance":
		context.add_item("개인 · " + str(app._person().get("name", "내 인물")))
		owner_ids.append("")
	if scope != "personal" and scope != "public":
		for company: Dictionary in (app._finance_contexts() if module_name == "finance" else app._represented_companies()):
			context.add_item(("공공기관 · " if company.has("can_control") else "법인 · ") + str(company.get("name", "조직")))
			owner_ids.append(str(company.get("id", "")))
	if owner_ids.is_empty():
		context.add_item("개인 · " + str(app._person().get("name", "내 인물")))
		owner_ids.append("")
	var selected_owner: String = app._finance_owner() if module_name == "finance" else str(app._company().get("id", ""))
	context.select(maxi(owner_ids.find(selected_owner), 0))
	box.add_child(context)
	var editors: Dictionary = {}
	var fields: Array = action.get("fields", [])
	for raw: Variant in fields:
		if not raw is Dictionary: continue
		var field: Dictionary = raw.duplicate(true)
		if action.has("ui_owner_id"): field["ui_owner_id"] = action.ui_owner_id
		var key: String = str(field.get("key", field.get("name", "")))
		if key == "": continue
		app._label(box, str(field.get("label", _humanize(key))) + (" · " + str(field["unit"]) if field.has("unit") else ""), 14)
		var kind: String = str(field.get("kind", field.get("type", "text")))
		if kind in ["int", "integer", "number", "percent"]:
			var edit := SpinBox.new()
			edit.custom_minimum_size = Vector2(0, 44)
			edit.min_value = float(field.get("min", 0))
			edit.max_value = float(field.get("max", 9007199254740991))
			edit.step = 1
			edit.value = float(int(app.world.get("planning_month", 1)) + 12) if key in ["expires_month", "latest_delivery_month"] else float(field.get("default", field.get("min", 0)))
			edit.allow_greater = false
			edit.allow_lesser = false
			box.add_child(edit)
			editors[key] = {"node": edit, "kind": "int"}
		elif kind in ["choice", "select", "id", "enum"] or field.has("source"):
			var edit := OptionButton.new()
			edit.fit_to_longest_item = false
			edit.clip_text = true
			edit.custom_minimum_size.y = 44
			var values: Array = []
			var choice_records: Dictionary = {}
			if field.get("optional", false):
				edit.add_item("선택하지 않음"); values.append("")
			var source: String = str(field.get("source", ""))
			var options: Variant = field.get("options", field.get("choices", []))
			if options is Dictionary:
				for id in options:
					edit.add_item(str(options[id])); values.append(id)
			elif options is Array and not options.is_empty():
				for option in options:
					if option is Dictionary:
						edit.add_item(str(option.get("label", option.get("name", option.get("id", ""))))); values.append(option.get("value", option.get("id", "")))
					else: edit.add_item(_option_label(str(option))); values.append(option)
			else:
				for item: Dictionary in source_records(app.world, source, str(app.world.get("player_id", ""))):
					if not _field_filter(app, field, item): continue
					var id: String = str(item.get(str(field.get("value_key", "id")), ""))
					if id == "": continue
					edit.add_item(_record_name(app, item))
					values.append(id)
					choice_records[id] = item.duplicate(true)
			if values.is_empty():
				edit.add_item("해당 조건의 대상 없음")
				values.append("")
			var default_value: Variant = app._government().get("id", field.get("default", "")) if key == "government_id" else field.get("default", "")
			edit.select(maxi(values.find(default_value), 0))
			box.add_child(edit)
			editors[key] = {"node": edit, "kind": "choice", "values": values, "optional": field.get("optional", false)}
			if source in ["jobs", "job_offers"]:
				var terms: Label = app._label(box, "", 13, app.GOLD)
				var update_terms: Callable = func(index: int):
					var offer: Dictionary = choice_records.get(str(values[index]), {}) if index>=0 and index<values.size() else {}
					if offer.is_empty(): terms.text = "현재 지원할 수 있는 유효한 고용 제안이 없습니다."
					else:
						terms.text = "지원 전 계약 조건\n직무: %s · 시간당 %s\n월 계약: %d h · 보장 유급시간: %d h\n교육 업무: %s" % [app._role(str(offer.get("role",""))), app._money(int(offer.get("wage",0))), int(offer.get("hours",0)), int(offer.get("guaranteed_hours",0)), "동의 조건 포함" if offer.get("teaching_authorized",false) else "동의 조건 없음"]
				edit.item_selected.connect(update_terms)
				update_terms.call(edit.selected)
		elif kind in ["bool", "boolean"]:
			var edit := CheckButton.new()
			edit.text = str(field.get("hint", "사용"))
			edit.button_pressed = bool(field.get("default", false))
			edit.custom_minimum_size.y = 44
			box.add_child(edit)
			editors[key] = {"node": edit, "kind": "bool"}
		else:
			var edit := LineEdit.new()
			edit.text = str(field.get("default", ""))
			edit.placeholder_text = str(field.get("hint", ""))
			edit.custom_minimum_size.y = 44
			box.add_child(edit)
			editors[key] = {"node": edit, "kind": "text", "array": kind in ["array", "csv"]}
	app._label(box, "금액은 최소 화폐 단위입니다. 100최소단위 = 1 CR. 예상치는 체결·납품·지급을 보장하지 않습니다.", 12, app.MUTED)
	app._button(box, "계획에 추가", func():
		var payload: Dictionary = {}
		for key in editors:
			var editor: Dictionary = editors[key]
			match str(editor.kind):
				"int": payload[key] = int(editor.node.value)
				"bool": payload[key] = editor.node.button_pressed
				"choice":
					var value: Variant = editor["values"][editor.node.selected]
					if value == "" and editor.get("optional", false): continue
					payload[key] = value
				"text":
					var value: String = editor.node.text.strip_edges()
					if editor.get("array", false): payload[key] = Array(value.split(",", false))
					else: payload[key] = value
		var result: Dictionary = app._queue_as(str(action.get("type", "")), payload, str(owner_ids[context.selected]))
		if result.get("ok", false):
			app._close_modal()
			app._render()
			app._toast("다음 월 계획에 추가했습니다.")
		else: app._message("명령을 추가할 수 없습니다", app._reason(str(result.get("error", "조건을 확인하세요."))))
	, true)
	app._button(box, "취소", app._close_modal)

static func _field_filter(app: Control, field: Dictionary, item: Dictionary) -> bool:
	if item.has("candidate_id") and str(item.get("candidate_id", "")) not in ["", str(app.world.get("player_id", ""))]: return false
	var filter: String = str(field.get("filter", ""))
	if str(field.get("source", "")) == "products" and str(field.get("filter", "")) == "physical": return item.get("kind", "material") not in ["service", "digital"]
	if str(field.get("source", "")) in ["loans", "insurance_policies", "finance_offers", "subscriptions", "agriculture", "delegations", "land_claims", "land_leases"]: filter = "own"
	var owners: Array = [str(app.world.get("player_id", ""))]
	for entity: Dictionary in app._finance_contexts(): owners.append(str(entity.get("id", "")))
	if str(field.get("ui_owner_id", "")) != "": owners = [str(field.ui_owner_id)]
	if filter == "own_or_released" and str(item.get("status", "")) == "released": return true
	if str(field.get("source", "")) == "land_claims" and int(item.get("remaining", item.get("amount",0))) <= 0: return false
	if filter in ["own", "own_or_released"]:
		var recognized: bool = false
		for key: String in ["owner_id", "company_id", "borrower_id", "person_id", "issuer_id", "seller_id", "buyer_id", "landlord_id", "tenant_id", "employer_id"]:
			if item.has(key):
				recognized = true
				if str(item[key]) in owners: return true
		if recognized: return false
	if field.has("status") and item.get("status", "") != field.status: return false
	return true

static func source_records(world: Dictionary, source: String, observer: String = "") -> Array:
	if source == "": return []
	if source == "market_sides": return [{"id":"buy","name":"매입"},{"id":"sell","name":"매도"}]
	if source in ["tech_topics", "digital_categories"]:
		var industry: Variant = module_script("industry")
		if industry == null: return []
		return industry.topics() if source == "tech_topics" else industry.digital_categories()
	if source == "finance_offers":
		return world.get("finance", {}).get("share_offers", []) + world.get("finance", {}).get("equity_issues", [])
	if source == "loss_events":
		var finance: Variant = module_script("finance")
		if observer == "" or finance == null: return []
		var allowed: Array = finance.definition().get("insurable_events", [])
		return world.get("events", []).filter(func(e): return str(e.get("type", "")) in allowed and (str(e.get("actor_id", "")) == observer or _observer_controls(world, observer, str(e.get("actor_id", "")))))
	if source in ["bids", "deliveries"]:
		var public_module: Variant = module_script("public")
		if observer == "" or public_module == null or not public_module.has_method("summary"): return []
		return public_module.summary(world, observer).get(source, [])
	if source == "services": return world.get("products", []).filter(func(p): return p.get("kind", "") == "service" or p.get("industry", "") == "서비스")
	if source == "currencies":
		var result: Array = []
		var seen: Dictionary = {}
		for currency: Dictionary in world.get("finance", {}).get("currencies", []):
			var id: String = str(currency.get("id", ""))
			if id != "" and not seen.has(id): result.append({"id": id, "name": id}); seen[id] = true
		for nation: Dictionary in world.get("nations", []):
			var id: String = str(nation.get("currency", "CR"))
			if not seen.has(id): result.append({"id": id, "name": id}); seen[id] = true
		for rate: Dictionary in source_records(world, "fx_rates"):
			for key: String in ["from_currency", "to_currency", "from", "to"]:
				var id: String = str(rate.get(key, ""))
				if id != "" and not seen.has(id): result.append({"id": id, "name": id}); seen[id] = true
		return result
	var path: Array = SOURCE_PATHS.get(source, [source])
	var value: Variant = world
	for part: String in path:
		if not value is Dictionary: return []
		value = value.get(part, [])
	if value is Array: return value
	if value is Dictionary:
		var records: Array = []
		for key in value:
			if value[key] is Dictionary:
				var item: Dictionary = value[key].duplicate(true)
				if not item.has("id"): item["id"] = str(key)
				records.append(item)
		return records
	return []

static func records(app: Control, module_name: String, source: String, title: String = "기록과 계약", owner_override: String = "") -> void:
	var box: VBoxContainer = app._card(app.content, "기록과 계약" if title == "기록과 계약" else title)
	var entries: Array = source_records(app.world, source, str(app.world.get("player_id", "")))
	var visible: int = 0
	var player: String = str(app.world.get("player_id", ""))
	var own: String = app._finance_owner() if module_name == "finance" else str(app._company().get("id", player))
	if owner_override != "": own = owner_override
	for item: Dictionary in entries:
		if visible >= 24: break
		var associated: Array = []
		for key: String in ["owner_id", "borrower_id", "company_id", "person_id", "buyer_id", "seller_id", "issuer_id", "holder_id", "investor_id", "landlord_id", "tenant_id", "employer_id"]:
			if item.has(key): associated.append(str(item[key]))
		var public_record: bool = module_name == "public" and source not in ["bids", "deliveries"]
		if source == "land_offers" and item.get("status", "") == "open": public_record = true
		if source in ["jobs", "job_offers"]:
			if str(item.get("candidate_id", "")) not in ["", player]: continue
			public_record = true
		if not public_record and not associated.is_empty() and player not in associated and own not in associated: continue
		visible += 1
		app._label(box, _record_name(app, item), 16, app.TEXT)
		if item.get("rect") is Dictionary:
			var rect: Dictionary = item.rect
			app._row(box, "대상 부지", "(%s, %s) · %s × %s타일" % [rect.get("x",0),rect.get("y",0),rect.get("w",1),rect.get("h",1)])
		for key: String in ["status", "stage", "role", "term_months", "months_remaining", "quantity", "shares", "principal", "arrears", "coverage", "remaining", "progress", "required_work", "deadline_month", "due_month", "effective_month", "budget", "amount", "specification", "side", "remaining_quantity", "limit_price", "expires_month", "fulfilled_quantity", "price", "rent", "duration_months", "guaranteed_hours", "hours", "wage", "month", "paid", "teaching_authorized"]:
			if not item.has(key): continue
			if item[key] is Dictionary or item[key] is Array: continue
			var value: String = str(item[key])
			if key in ["principal", "arrears", "coverage", "budget", "amount", "price", "rent", "limit_price", "paid", "wage"] and (item[key] is int or item[key] is float): value = app._money(int(item[key]))
			if key == "remaining" and source in ["wage_claims", "land_claims"]: value = app._money(int(item[key]))
			if key == "teaching_authorized": value = "동의" if bool(item[key]) else "동의하지 않음"
			if key == "role": value = app._role(value)
			if key == "status": value = app._status(value)
			app._row(box, _humanize(key), value)
	if visible == 0: app._label(box, "아직 해당하는 기록이 없습니다. 위 행동에서 자격과 비용을 확인해 계획을 시작할 수 있습니다.", 13, app.MUTED)

static func _record_name(app: Control, item: Dictionary) -> String:
	for key: String in ["name", "title", "label", "description"]:
		if item.has(key) and str(item[key]) != "": return str(item[key])
	var parts: Array = []
	for key: String in ["product_id", "person_id", "company_id", "government_id", "city_id"]:
		if not item.has(key): continue
		var source: String = {"product_id": "products", "person_id": "people", "company_id": "companies", "government_id": "governments", "city_id": "cities"}[key]
		for candidate: Dictionary in source_records(app.world, source, str(app.world.get("player_id", ""))):
			if candidate.get("id", "") == item[key]: parts.append(str(candidate.get("name", item[key]))); break
	return " · ".join(parts) if not parts.is_empty() else str(item.get("id", "계약"))

static func _humanize(key: String) -> String:
	return str({"status": "상태", "stage": "단계", "role": "직무", "term_months": "계약 기간", "months_remaining": "남은 개월", "quantity": "수량", "shares": "주식 수", "principal": "원금", "arrears": "연체", "coverage": "보장액", "remaining": "남은 의무", "progress": "진척", "required_work": "필요 공수", "deadline_month": "납기 월", "due_month": "지급 월", "effective_month": "발효 월", "budget": "예산", "amount": "금액", "specification": "규격", "side": "주문 구분", "remaining_quantity": "미체결 수량", "limit_price": "제한 단가", "expires_month": "만료 월", "fulfilled_quantity": "체결 물량", "price": "가격", "rent": "월 임대료", "duration_months": "사용 기간", "guaranteed_hours": "월 보장 유급시간", "hours": "계약 시간", "wage": "시간당 임금", "month": "발생 월", "paid": "지급액", "teaching_authorized": "교육 업무 동의"}.get(key, key.replace("_", " ")))


static func _observer_controls(world: Dictionary, observer: String, owner: String) -> bool:
	for company: Dictionary in world.get("companies", []):
		if str(company.get("id", "")) == owner and str(company.get("director_id", "")) == observer: return true
	var public_module: Variant = module_script("public")
	if public_module != null and public_module.has_method("has_authority"):
		return public_module.has_authority(world, observer, owner, "budget")
	return false


static func _option_label(value: String) -> String:
	return str({"agriculture":"농업", "industry":"산업", "residential":"주거", "commercial":"상업", "buy":"매입", "sell":"매도", "equity":"신주 발행", "dividend":"배당", "download":"다운로드", "online":"온라인 서비스", "no":"사용 안 함", "yes":"사용", "neutral":"중립", "cooperate":"협력", "tension":"긴장", "conflict":"분쟁", "free_trade":"자유무역", "transit":"통과 운송", "ceasefire":"휴전", "embargo":"통상 제한", "tax":"세율", "tariff":"관세", "service":"공공서비스", "corporation":"법인", "sole":"개인사업", "operator":"운영 인력", "builder":"건설 인력", "director":"대표"}.get(value, value))
