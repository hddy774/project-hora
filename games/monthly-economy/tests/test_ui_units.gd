extends SceneTree
## Exact money entry, display-only conversion and measured progress UI regression.
const Main = preload("res://ui/main.tscn")
const Economy = preload("res://core/economy.gd")
const Domains = preload("res://ui/domain_views.gd")
const P = preload("res://ui/presentation.gd")
const Widgets = preload("res://ui/progress_widgets.gd")
const Money = preload("res://ui/money_input.gd")
const Policy = preload("res://ui/policy_value_input.gd")
var checks: int = 0
var failures: Array = []
func check(actual: Variant, expected: Variant, label: String) -> void:
	checks+=1
	if actual!=expected: failures.append(label+": "+str(actual)+" != "+str(expected))
func _init() -> void: call_deferred("run")
func run() -> void:
	root.size=Vector2i(480,920)
	var app=Main.instantiate();root.add_child(app);await process_frame;await process_frame
	app.world=Economy.new_world(42,{"cities_per_nation":1,"expanded_industries":false});app.screen="game";app._render();await process_frame
	var initial_hash: String = Economy.state_hash(app.world)
	var preferences_path: String = "user://display-settings.json"
	var had_preferences: bool = FileAccess.file_exists(preferences_path)
	var preferences: PackedByteArray = FileAccess.get_file_as_bytes(preferences_path) if had_preferences else PackedByteArray()
	app.settings["currency_view"]="USD"
	check(app._money(123456),"$1,234.56","Full dollars")
	app._toggle_currency();check(app._money(100000000,true),"10억원","KRW shortcut")
	check(Economy.state_hash(app.world),initial_hash,"View switch cannot alter ledger")
	check(app._load_preferences().get("currency_view"),"KRW","Display preference stored independently")
	app._toggle_currency();check(Economy.state_hash(app.world),initial_hash,"Switch back cannot alter ledger")
	var money_fields: int = 0
	for module_name: String in Domains.MODULE_PATHS:
		for action: Dictionary in Domains.descriptors(module_name):
			Domains.form(app,module_name,action)
			for label: Node in app.modal.find_children("*","Label",true,false):
				check(label.text.contains("최소 화폐 단위") or label.text.contains("최소화폐단위"),false,"No minor-unit input instruction: "+str(action.type))
			for field: Dictionary in action.get("fields",[]):
				var key: String = str(field.get("key",""))
				var editor: Node
				for node: Node in app.modal.find_children("*","Control",true,false):
					if node.get_meta("field_key","")==key: editor=node;break
				if P.is_money_field(str(action.type),key,field):
					money_fields+=1
					check(editor!=null and editor.get_script()==Money,true,str(action.type)+" dollar editor "+key)
					var dollar_label: bool = false
					for label: Node in app.modal.find_children("*","Label",true,false):
						if label.text.contains("달러"): dollar_label=true
					check(dollar_label,true,str(action.type)+" explicitly labels dollars")
					if editor!=null and editor.get_script()==Money:
						check(editor.read_value().get("cents"),int(field.get("default",field.get("min",0))),str(action.type)+" preserves default cents")
				elif key in ["quantity","shares","seats","hours","months","term_months"]: check(editor is SpinBox,true,key+" remains a count")
			app._close_modal()
	check(money_fields>30,true,"All financial domains covered")
	check(Economy.state_hash(app.world),initial_hash,"Inspecting entries leaves world unchanged")
	# Real command submission: dollars are converted exactly once before shared validation.
	for action: Dictionary in Domains.descriptors("finance"):
		if action.type!="finance_deposit": continue
		action=action.duplicate(true);action["ui_owner_id"]=app.world.player_id
		Domains.form(app,"finance",action)
		for node: Node in app.modal.find_children("*","Control",true,false):
			if node.get_meta("field_key","")=="amount": node.edit.text="1.23"
		for button: Node in app.modal.find_children("*","Button",true,false):
			if button.text=="계획에 추가": button.pressed.emit();break
	check(app.world.commands.size(),1,"Real dollar deposit queued")
	if app.world.commands.size()==1:
		check(app.world.commands[0].payload.amount,123,"Payload uses exact cents")
		check(app.world.commands[0].acting_company_id,"","Personal form uses personal account")
	var policy=Policy.new();root.add_child(policy)
	policy.set_policy("tax_bps");policy.amount.edit.text="1.25";check(policy.read_value().cents,125,"Percent to bp")
	policy.set_policy("services_budget");policy.amount.edit.text="1.25";check(policy.read_value().cents,125,"Budget to cents")
	policy.set_policy("import_ban");policy.enabled.button_pressed=true;check(policy.read_value().cents,1,"Flag is neither money nor rate")
	policy.queue_free()
	var box: VBoxContainer = app._open_modal("진행률 검증")
	var view: Dictionary = Widgets.work(app,box,"작업량 확인 중",0,0)
	check(view.bar.indeterminate,true,"Unknown denominator indeterminate")
	check(view.percent.text.contains("%"),false,"Unknown denominator has no invented percentage")
	Widgets.update(view,{"completed":3,"total":7,"stage":"실제 작업"})
	check(view.bar.indeterminate,false,"Measured denominator")
	check(view.percent.text.begins_with("42%"),true,"Measured completed work")
	var planned_hash: String = Economy.state_hash(app.world)
	app._close_modal();app._preflight()
	check(is_instance_valid(app.progress_view.get("bar")),true,"Preflight progress exists before work")
	while app.operation_busy: await process_frame
	check(app.operation_progress.get_snapshot().status,"completed","Preflight completed")
	check(Economy.state_hash(app.world),planned_hash,"Preflight read only")
	app._close_modal()
	var slot: String = "ui-units-progress-"+str(Time.get_ticks_usec())
	app._save_visible(slot,false)
	var saving_modal: Control = app.modal
	app._back()
	check(app.modal==saving_modal,true,"Back cannot close an in-flight IO modal")
	check(app._queue("set_policy",{"production_percent":50}).get("ok"),false,"No plan edits during IO apply")
	check(is_instance_valid(app.progress_view.get("bar")),true,"Save progress appears before IO")
	while app.operation_busy: await process_frame
	await process_frame
	check(app.operation_progress.get_snapshot().percent,100,"Save reaches measured completion")
	check(app.Store.load_world(slot).get("ok"),true,"Saved exact world is valid")
	app._load_visible("ui-units-no-such-file")
	while app.operation_busy: await process_frame
	check(app.operation_progress.get_snapshot().status,"failed","Failed load never claims success")
	check(Economy.state_hash(app.world),planned_hash,"Failed load preserves current world")
	# Unexpected worker return types must release the busy state and preserve input.
	app._show_progress("정산 작업 오류 회귀",2)
	app.resolving=true;app.worker=Thread.new();app.worker.start(func():return null)
	while app.resolving: await process_frame
	check(app.operation_progress.get_snapshot().status,"failed","Null month worker reports failure")
	check(Economy.state_hash(app.world),planned_hash,"Null month worker preserves committed input")
	app._show_progress("생성 작업 오류 회귀",2)
	app.generation_cancelled=false;app.generator=Thread.new();app.generator.start(func():return null)
	while app.generator!=null: await process_frame
	check(app.operation_progress.get_snapshot().status,"failed","Null generation worker reports failure")
	check(Economy.state_hash(app.world),planned_hash,"Null generation worker preserves existing world")
	app.Store.delete_slot(slot)
	var view_file: String = "user://view-"+slot+".json"
	if FileAccess.file_exists(view_file): DirAccess.remove_absolute(ProjectSettings.globalize_path(view_file))
	if had_preferences:
		var file=FileAccess.open(preferences_path,FileAccess.WRITE);file.store_buffer(preferences);file.close()
	else: DirAccess.remove_absolute(ProjectSettings.globalize_path(preferences_path))
	app.queue_free();await process_frame
	for failure in failures: push_error(failure)
	print("UI units/progress: ",checks," checks, ",money_fields," money fields, ",failures.size()," failures")
	quit(0 if failures.is_empty() else 1)
