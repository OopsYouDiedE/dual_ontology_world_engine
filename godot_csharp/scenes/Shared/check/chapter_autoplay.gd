extends RefCounted
## 自动检查用的“代按键”：推进对白（选第一个可用回应）、填表单、合上卡片。不是游玩验证。

var host: Node
var lines: Array[String] = []
var choose: Callable = func(_options: Array) -> String: return ""

func _init(target: Node) -> void:
	host = target

## 推进一帧里能推进的界面；返回界面是否还占着。
func step() -> bool:
	var ui: Node = host.ui
	var dialogue: CanvasLayer = ui.dialogue
	if dialogue.active and not dialogue.busy and dialogue.line != null:
		if dialogue.body.visible_characters >= 0:
			dialogue.reveal()
			var text := "%s：%s" % [dialogue.speaker.text, dialogue.body.text]
			if lines.is_empty() or lines[-1] != text: lines.append(text)
		elif not dialogue.line.responses.is_empty():
			var picked: String = ""
			var options: Array = dialogue.line.responses.map(func(r): return {"id": r.next_id, "label": r.text, "available": r.is_allowed})
			picked = str(choose.call(options))
			if picked == "":
				for response in dialogue.line.responses:
					if response.is_allowed:
						picked = response.next_id
						break
			dialogue.choose(picked)
		else:
			dialogue.show_line(dialogue.line.next_id)
		return true
	if ui.modal_kind == "form":
		var actions: Array = ui._modal_event.get("actions", [])
		var picked: String = choose.call(actions)
		for action in actions:
			if picked != "" and str(action.id) != picked: continue
			if picked == "" and (action.has("tab") or str(action.id) == "cancel"): continue
			if ui._form.is_complete(action.get("requires", [])):
				ui._form_action(ui._modal_event, action)
				return true
		# 必填项未选：给每个选项组选第一项。
		for field in ui._modal_event.get("fields", []):
			if str(field.kind) == "choice" and not field.get("options", []).is_empty():
				ui._form.select(str(field.id), str(field.options[0].id))
		return true
	return dialogue.active

func open_modal() -> String:
	return host.ui.modal_kind

## 选章节弹窗里的一项（按 id 或 id 结尾）。
func select(id: String) -> bool:
	var ui: Node = host.ui
	for option in ui._modal_event.get("options", []):
		if str(option.id) == id or str(option.id).ends_with(":" + id):
			ui._select(ui._modal_event, str(option.id))
			return true
	return false
