extends Node
## Concrete Godot bindings for chapter semantic outputs. No chapter rules live here.
const Presentation := preload("res://scenes/Chapter2/Game/Chapter2Presentation.gd")
var host: Node
var presenter: Node
var closing := false
var close_option: Dictionary = {}
signal cancelled

func _ready() -> void:
	presenter = Presentation.new()
	add_child(presenter)

func present_dialogue(lines: Array) -> void:
	if closing: return
	presenter.set_lines(lines)
	host.dialogue.panel.show()
	host.dialogue.start_packet(presenter, "semantic", host)
	await host.dialogue.finished
	if closing: return
	host.dialogue.panel.hide()

func show_choices(title: String, options: Array) -> void:
	_remember_close(options)
	var body: VBoxContainer = host.open_modal(title, Vector2(860, mini(800, 200 + 56 * options.size())), "menu")
	var list: Control = host._ui_content("ChoiceList", body)
	for option in options:
		var button: Button = host._ui_content("ChoiceOption", list)
		button.text = str(option.label)
		button.disabled = not bool(option.get("available", true))
		button.tooltip_text = str(option.get("lock_hint", ""))
		button.pressed.connect(host._choose_event.bind(str(option.id), str(option.token)))

func show_card(packet: Dictionary) -> void:
	_remember_close(packet.options)
	var text := str(packet.text)
	var body: VBoxContainer = host.open_modal(str(packet.title), Vector2(860, clampi(300 + 30 * text.length() / 34, 340, 640)), "card")
	var view: Control = host._ui_content("EvidenceCard", body)
	view.get_node("Facts").text = text
	view.get_node("Source").text = "来源：" + str(packet.place)
	var actions := []
	for option in packet.get("options", []):
		actions.append([str(option.label), host._choose_event.bind(str(option.id), str(option.token))])
	host.add_footer(actions)

func begin_shutdown() -> void:
	if closing: return
	closing = true
	if is_instance_valid(host.dialogue): host.dialogue.cancel_presentation()
	if is_instance_valid(presenter): presenter.clear()
	cancelled.emit()

func execute(packet: Dictionary) -> void:
	if closing: return
	match str(packet.kind):
		"lines":
			await present_dialogue(packet.lines)
		"candidates":
			show_choices(str(packet.title), packet.options)
		"card":
			show_card(packet)
		"scene":
			await host.load_semantic_scene(packet)
		"aside":
			host.show_aside(str(packet.text))
		"title":
			host._flash_title(str(packet.text))
		"music":
			host.Sound.music(str(packet.id))
		"sound":
			host.Sound.sfx(str(packet.id), float(packet.get("volume", -4.0)))
		"stance":
			host._group_by_stance(packet.stands)
		"end":
			if packet.result.has("stands"): host._group_by_stance(packet.result.stands)
			host._ending(packet.result)
		"error":
			push_error(str(packet.message))
		_:
			push_error("Unsupported chapter display output: " + str(packet.kind))

func _remember_close(options: Array) -> void:
	close_option = {}
	for option in options:
		if str(option.id) in ["close", "cancel"]:
			close_option = option
			return
