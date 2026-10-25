extends RefCounted
const Art = preload("res://ui/art.gd")
## Measured work and processing stages. Never estimates time from elapsed frames.
static func create(app: Control, parent: Node, title: String = "처리 진행") -> Dictionary:
	var box := VBoxContainer.new();box.add_theme_constant_override("separation",4);parent.add_child(box)
	var stage: Label = app._label(box,title,13,app.MINT)
	var bar := ProgressBar.new();bar.custom_minimum_size=Vector2(0,12);bar.size_flags_horizontal=Control.SIZE_EXPAND_FILL;bar.show_percentage=false;bar.min_value=0;bar.max_value=100;box.add_child(bar)
	bar.add_theme_stylebox_override("background",Art.style("button",app.PANEL_LIGHT))
	bar.add_theme_stylebox_override("fill",Art.style("button_active",app.MINT))
	var percent: Label = app._label(box,"단계 확인 중",12,app.MUTED)
	var result: Dictionary = {"node":box,"stage":stage,"bar":bar,"percent":percent}
	update(result,{"total":0,"stage":title})
	return result

static func update(view: Dictionary, snapshot: Dictionary) -> void:
	if view.is_empty() or not is_instance_valid(view.get("node")) or view.node.is_queued_for_deletion(): return
	var total: int = int(snapshot.get("total",0))
	var done: int = clampi(int(snapshot.get("completed",0)),0,maxi(0,total))
	view.stage.text = str(snapshot.get("stage",snapshot.get("label","처리 중")))
	view.bar.indeterminate = total <= 0
	if total > 0:
		view.bar.value = float(done)*100.0/total
		view.percent.text = "%d%% · %d / %d 완료" % [done*100/total,done,total]
	else: view.percent.text = "진행 중 · 완료율을 아직 계산할 수 없습니다."
	view.bar.tooltip_text = "완료한 처리 단계 또는 작업량의 비율입니다. 예상 소요시간이 아닙니다."
	if snapshot.get("status","") == "failed": view.percent.text += " · 중단"

static func work(app: Control, parent: Node, title: String, completed: int, total: int) -> Dictionary:
	var view: Dictionary = create(app,parent,title)
	update(view,{"stage":title,"completed":completed,"total":total})
	return view
