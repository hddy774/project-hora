extends SceneTree
## Real descriptor forms, command submission and isolated tutorial sessions.
## Run: godot --headless --path games/monthly-economy --script tests/test_ui_forms.gd
const Main = preload('res://ui/main.tscn')
const Economy = preload('res://core/economy.gd')
const Domains = preload('res://ui/domain_views.gd')
var errors: Array = []
var count_value: int = 0
func _init(): call_deferred('_run')
func _run():
	root.size=Vector2i(480,920)
	var app=Main.instantiate();root.add_child(app);await process_frame
	app.world=Economy.new_world(42,{"cities_per_nation":1,"expanded_industries":false});app.screen='game';app._render();await process_frame
	var before=Economy.state_hash(app.world)
	for module_name: String in Domains.MODULE_PATHS:
		for descriptor: Dictionary in Domains.descriptors(module_name):
			Domains.form(app,module_name,descriptor)
			await process_frame
			await process_frame
			if app.modal==null: errors.append('Form missing '+str(descriptor.type));continue
			for item: Node in app.modal.find_children('*','OptionButton',true,false):
				if item.item_count<=0: errors.append('Empty selector '+str(descriptor.type))
			for raw:Node in app.modal.find_children('*','Control',true,false):
				var control:Control=raw
				if control.is_visible_in_tree() and control.get_global_rect().end.x > root.size.x + 1: errors.append('Form overflow '+str(descriptor.type)+': '+str(control.get_class()))
			count_value+=1
			app._close_modal()
		print('Forms rendered: ',module_name)
	if before!=Economy.state_hash(app.world): errors.append('Opening forms mutated economic state')
	# Use a real form to queue an actual personal deposit and resolve it through the shared handler.
	app.context_company_id='__default__'
	app._go(3,[0,0,0]);await process_frame
	var opened_personal: bool = false
	for button in app.content.find_children('*','Button',true,false):
		if button.text.begins_with('예금 가입'):
			button.pressed.emit(); opened_personal=true; break
	if not opened_personal: errors.append('Personal asset page did not expose real deposit form')
	await process_frame
	var options=app.modal.find_children('*','OptionButton',true,false)
	if options.size()>0: options[0].select(0)
	var amount=app.modal.find_children('*','LineEdit',true,false)
	if amount.size()>0: amount[0].text='1.00'
	for button in app.modal.find_children('*','Button',true,false):
		if button.text=='계획에 추가': button.pressed.emit();break
	if app.world.commands.size()!=1: errors.append('Actual deposit form failed to queue')
	else:
		if app.world.commands[0].acting_company_id!='': errors.append('Personal form used company money')
		var resolved=Economy.resolve_month(app.world)
		if not resolved.get('ok',false): errors.append('Queued form did not resolve: '+str(resolved.get('errors',[])))
	# Cancellation must preserve the existing session while copied generation finishes.
	var planned_hash: String = Economy.state_hash(app.world)
	app._begin_generation(42,{"cities_per_nation":1,"expanded_industries":false})
	app._cancel_generation()
	while app.generator != null: await process_frame
	if Economy.state_hash(app.world) != planned_hash: errors.append('Cancelling generation changed current session')
	# A practice session uses the same economy and a distinct save namespace.
	var normal_before: PackedByteArray = FileAccess.get_file_as_bytes("user://saves/autosave.json") if FileAccess.file_exists("user://saves/autosave.json") else PackedByteArray()
	var practice_slot: String = "ui-practice-test-" + str(Time.get_ticks_usec())
	app._enter_practice(Economy.new_world(42,{"cities_per_nation":1,"expanded_industries":false}), practice_slot)
	if not app.practice_mode or app._storage_slot("autosave") != "practice-autosave": errors.append('Tutorial session did not use isolated namespace')
	if not Economy.validate(app.world).is_empty(): errors.append('Tutorial world violated core invariants')
	if not app.Store.load_world("practice-" + practice_slot).get("ok",false): errors.append('Tutorial save failed')
	var normal_after: PackedByteArray = FileAccess.get_file_as_bytes("user://saves/autosave.json") if FileAccess.file_exists("user://saves/autosave.json") else PackedByteArray()
	if normal_before != normal_after: errors.append('Tutorial overwrote normal sandbox autosave')
	app._go(1,[1,0,0]);await process_frame
	if not app.practice_steps.get("inspect",false): errors.append('Tutorial did not observe real production view')
	app.Store.delete_slot("practice-" + practice_slot)
	var view_path: String = "user://view-practice-"+practice_slot+".json"
	if FileAccess.file_exists(view_path): DirAccess.remove_absolute(ProjectSettings.globalize_path(view_path))
	for error in errors: push_error(error)
	print('PASS' if errors.is_empty() else 'FAIL',': ',count_value,' module forms, personal deposit command + resolution, state privacy/navigation')
	app.queue_free();await process_frame;quit(0 if errors.is_empty() else 1)
