extends Node2D
## Visual acceptance only; no Core or case logic.
const PATHS := ["res://ShareAsset/Images/characters/animation_v2/detective.tres", "res://ShareAsset/Images/characters/animation_v2/placeholder.tres"]
const EXPECTED := {"idle":1,"walk":8,"talk":3,"inspect":4,"interact":4,"sit":4,"break":6,"cast":7}
const DIRS := ["front", "back", "left", "right"]
const NAMES := ["侦探", "占位人物"]
const CHAIR := Vector2(234,159)
const CRATE := Vector2(282,179)
const NOTICE := Vector2(70,75)
var actors: Array[CharacterBody2D] = []
var sprites: Array[AnimatedSprite2D] = []
var directions := ["front","front"]
var actions := ["idle","idle"]
var seated := [false,false]
var rising := [false,false]
var active := 0
var paused := false
var zoom_level := 3
var show_guides := true
var crate_broken := false
var touched := false
var spell_time := 0.0
var manual_walk := false
var message := "动作验收室：物件反馈只验证表现，不代表案件规则。"
var ui: Label
var world: Node2D
var guides: Node2D
var crate_body: StaticBody2D
var crate_visual: Node2D
var chair_visual: Node2D
var notice_visual: Node2D
var table_visual: Node2D

func _ready() -> void:
	get_window().content_scale_size = Vector2i.ZERO
	get_window().min_size = Vector2i(1040,800)
	get_window().size = Vector2i(1280,900)
	world = Node2D.new()
	world.name = "World"
	world.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(world)
	world.draw.connect(_draw_floor)
	_build_room()
	_build_actors()
	guides = Node2D.new()
	guides.z_index = 1000
	guides.draw.connect(_draw_guides)
	world.add_child(guides)
	_build_ui()
	_apply_scale()
	_update_ui()

func _physics_process(delta: float) -> void:
	if paused: return
	spell_time = maxf(0,spell_time-delta)
	var movement := Input.get_vector("ui_left","ui_right","ui_up","ui_down")
	if not seated[active] and not rising[active]:
		if movement != Vector2.ZERO:
			manual_walk = false
			actors[active].velocity = movement*48.0
			actors[active].move_and_slide()
			directions[active] = ("right" if movement.x>0 else "left") if absf(movement.x)>absf(movement.y) else ("front" if movement.y>0 else "back")
			_set_action("walk")
		elif actions[active] == "walk" and not manual_walk: _set_action("idle")
	for actor in actors: actor.z_index = roundi(actor.position.y)
	guides.queue_redraw()
	_update_ui()

func _unhandled_key_input(event: InputEvent) -> void:
	if not event is InputEventKey or not event.pressed or event.echo: return
	match event.keycode:
		KEY_TAB: select_actor((active+1)%2)
		KEY_SPACE: toggle_pause()
		KEY_ENTER: step_frame()
		KEY_G: show_guides = not show_guides; guides.queue_redraw(); world.queue_redraw()
		KEY_1,KEY_2,KEY_3,KEY_4: set_zoom(int(event.keycode-KEY_0))
		KEY_I: play_action("idle")
		KEY_W: play_action("walk")
		KEY_T: play_action("talk")
		KEY_R: play_action("inspect")
		KEY_E: play_action("interact")
		KEY_S: play_action("sit")
		KEY_B: play_action("break")
		KEY_C: play_action("cast")

func select_actor(index: int) -> void:
	active = index
	manual_walk = false
	_update_ui()

func turn_to(value: String) -> void:
	if seated[active] or rising[active]: return
	directions[active] = value
	_set_action("idle",true)

func play_action(value: String) -> void:
	if value == "sit" and seated[active]:
		rising[active] = true
		seated[active] = false
		sprites[active].play_backwards(sprites[active].animation)
		if paused: sprites[active].pause()
		return
	if seated[active] or rising[active]:
		message = "先按坐/起身离开座位。"
		return
	if value == "sit" and actors[active].position.distance_to(CHAIR)<=30:
		var seat_x: float = -8.0 if directions[active]=="left" else (8.0 if directions[active]=="right" else 0.0)
		actors[active].position = CHAIR+Vector2(seat_x,0)
	manual_walk = value == "walk"
	_set_action(value,true)

func _set_action(value: String,restart := false) -> void:
	var sprite := sprites[active]
	var dir: String = directions[active]
	var animation := value+"_"+("left" if dir=="right" else dir)
	sprite.flip_h = dir == "right"
	if sprite.animation != animation or restart:
		actions[active] = value
		sprite.speed_scale = 1.0
		sprite.play(animation)
		sprite.set_frame_and_progress(0,0)
		if paused: sprite.pause()
	_update_ui()

func toggle_pause() -> void:
	paused = not paused
	for i in sprites.size():
		if paused: sprites[i].pause()
		elif not seated[i]: sprites[i].play(sprites[i].animation,-1.0 if rising[i] else 1.0)
	_update_ui()

func step_frame() -> void:
	paused = true
	for sprite in sprites: sprite.pause()
	var current := sprites[active]
	var count := current.sprite_frames.get_frame_count(current.animation)
	current.frame = (current.frame+1)%count
	_update_ui()

func _on_frame(index: int) -> void:
	var sprite := sprites[index]
	var kind: String = actions[index]
	if kind == "interact" and sprite.frame == 2 and _can_reach(index,NOTICE):
		touched = true
		notice_visual.modulate = Color("a4d6a5")
		message = "接触帧：面前的面板已响应。"
	if kind == "break" and sprite.frame == 3 and not crate_broken and _can_reach(index,CRATE):
		crate_broken = true
		crate_visual.modulate = Color("5c4935")
		crate_visual.scale.y = 0.25
		crate_body.get_child(0).set_deferred("disabled",true)
		message = "挥击接触帧：箱子破损并解除阻挡。"
	if kind == "cast" and sprite.frame == 4:
		spell_time = 0.35
		guides.queue_redraw()

func _can_reach(index: int,target: Vector2) -> bool:
	var toward := target-actors[index].position
	return toward.length()<=30 and toward.normalized().dot(facing_vector(directions[index]))>0.35

func facing_vector(dir: String) -> Vector2:
	return {"front":Vector2.DOWN,"back":Vector2.UP,"left":Vector2.LEFT,"right":Vector2.RIGHT}[dir]

func _on_finished(index: int) -> void:
	if rising[index]:
		rising[index] = false
		actions[index] = "idle"
		sprites[index].play("idle_"+("left" if directions[index]=="right" else directions[index]))
	elif actions[index] == "sit":
		seated[index] = true
		sprites[index].pause()
		message = "保持坐姿；再按坐/起身逆序播放。椅子为独立物件。"

func _build_actors() -> void:
	for index in 2:
		var actor := CharacterBody2D.new()
		actor.name = NAMES[index]
		actor.position = Vector2(96+index*48,176)
		var collision := CollisionShape2D.new()
		var box := RectangleShape2D.new()
		box.size = Vector2(16,12)
		collision.shape = box
		collision.position = Vector2(0,-6)
		actor.add_child(collision)
		var sprite := AnimatedSprite2D.new()
		sprite.name = "AnimatedSprite2D"
		sprite.centered = true
		sprite.position = Vector2(0,-23)
		sprite.sprite_frames = load(PATHS[index])
		actor.add_child(sprite)
		world.add_child(actor)
		actors.append(actor)
		sprites.append(sprite)
		sprite.frame_changed.connect(_on_frame.bind(index))
		sprite.animation_finished.connect(_on_finished.bind(index))
		sprite.play("idle_front")

func _build_room() -> void:
	for rect in [Rect2(16,32,288,8),Rect2(16,32,8,176),Rect2(16,200,288,8),Rect2(296,32,8,64),Rect2(296,128,8,80)]:
		_obstacle("Wall",rect)
		_prop(rect.position+rect.size/2,Rect2(-rect.size/2,rect.size),Color("626e71"),1)
	_obstacle("Table",Rect2(192,91,56,13))
	table_visual = _prop(Vector2(220,104),Rect2(-32,-34,64,30),Color("996b47"),104)
	_add_rect(table_visual,Rect2(-30,-8,5,8),Color("5a3f32"))
	_add_rect(table_visual,Rect2(25,-8,5,8),Color("5a3f32"))
	_add_rect(table_visual,Rect2(-29,-32,58,3),Color("c69562"))
	chair_visual = _prop(CHAIR,Rect2(-10,-17,20,4),Color("9c744f"),roundi(CHAIR.y)-1)
	_add_rect(chair_visual,Rect2(-9,-13,3,13),Color("76573c"))
	_add_rect(chair_visual,Rect2(6,-13,3,13),Color("76573c"))
	crate_body = _obstacle("Crate",Rect2(CRATE-Vector2(10,10),Vector2(20,10)))
	crate_visual = _prop(CRATE,Rect2(-10,-23,20,23),Color("b38150"),roundi(CRATE.y))
	_add_rect(crate_visual,Rect2(-8,-21,16,2),Color("64472f"))
	_add_rect(crate_visual,Rect2(-1,-21,2,19),Color("64472f"))
	notice_visual = _prop(NOTICE,Rect2(-10,-25,20,22),Color("d6c7a4"),roundi(NOTICE.y))
	_add_rect(notice_visual,Rect2(-7,-22,14,2),Color("76664d"))
	_add_rect(notice_visual,Rect2(-7,-16,12,2),Color("76664d"))

func _obstacle(label: String,rect: Rect2) -> StaticBody2D:
	var body := StaticBody2D.new()
	body.name = label
	body.position = rect.position+rect.size/2
	var shape := CollisionShape2D.new()
	var box := RectangleShape2D.new()
	box.size = rect.size
	shape.shape = box
	body.add_child(shape)
	world.add_child(body)
	return body

func _prop(at: Vector2,rect: Rect2,color: Color,order: int) -> Node2D:
	var prop := Node2D.new()
	prop.position = at
	prop.z_index = order
	world.add_child(prop)
	_add_rect(prop,rect,color)
	return prop

func _add_rect(parent: Node2D,rect: Rect2,color: Color) -> void:
	var polygon := Polygon2D.new()
	polygon.polygon = PackedVector2Array([rect.position,rect.position+Vector2(rect.size.x,0),rect.end,rect.position+Vector2(0,rect.size.y)])
	polygon.color = color
	parent.add_child(polygon)

func set_zoom(value: int) -> void:
	zoom_level = value
	_apply_scale()

func _apply_scale() -> void:
	get_window().size = Vector2i(maxi(1040,352*zoom_level+40),maxi(800,224*zoom_level+180))
	world.scale = Vector2.ONE*zoom_level
	world.position = Vector2(20,160)

func _build_ui() -> void:
	var panel := VBoxContainer.new()
	panel.position = Vector2(20,10)
	add_child(panel)
	ui = Label.new()
	ui.add_theme_font_size_override("font_size",16)
	panel.add_child(ui)
	var actions_row := HBoxContainer.new()
	panel.add_child(actions_row)
	var captions := {"idle":"待机 I","walk":"步态 W","talk":"交谈 T","inspect":"检查 R","interact":"交互 E","sit":"坐/起身 S","break":"破坏 B","cast":"施法 C"}
	for key in captions:
		var button := Button.new()
		button.text = captions[key]
		button.focus_mode = Control.FOCUS_NONE
		button.pressed.connect(play_action.bind(key))
		actions_row.add_child(button)
	var settings := HBoxContainer.new()
	panel.add_child(settings)
	for dir in DIRS:
		var button := Button.new()
		button.text = {"front":"转前","back":"转后","left":"转左","right":"转右·镜像"}[dir]
		button.focus_mode = Control.FOCUS_NONE
		button.pressed.connect(turn_to.bind(dir))
		settings.add_child(button)
	for spec in [["切换人物",func(): select_actor(1-active)],["暂停/续播",toggle_pause],["单帧",step_frame]]:
		var button := Button.new()
		button.text = spec[0]
		button.focus_mode = Control.FOCUS_NONE
		button.pressed.connect(spec[1])
		settings.add_child(button)

func _update_ui() -> void:
	if ui == null: return
	var sprite := sprites[active]
	ui.text = "%s · %s/%s · 帧%d/%d · %dx · %s\n方向键移动；按钮转向；1—4缩放；Space暂停；Enter单帧；Tab切换；G辅助线\n%s" % [NAMES[active],actions[active],directions[active],sprite.frame+1,sprite.sprite_frames.get_frame_count(sprite.animation),zoom_level,"暂停" if paused else "播放",message]

func _draw_floor() -> void:
	world.draw_rect(Rect2(0,0,352,224),Color("172224"))
	world.draw_rect(Rect2(24,40,272,160),Color("394346"))
	world.draw_rect(Rect2(296,96,56,32),Color("394346"))
	if show_guides:
		for x in range(32,352,32): world.draw_line(Vector2(x,40),Vector2(x,200),Color("505857"))
		for y in range(40,201,32): world.draw_line(Vector2(24,y),Vector2(296,y),Color("505857"))

func _draw_guides() -> void:
	if show_guides:
		for actor in actors:
			guides.draw_rect(Rect2(actor.position-Vector2(8,12),Vector2(16,12)),Color("e6a26b"),false)
			guides.draw_line(actor.position-Vector2(3,0),actor.position+Vector2(3,0),Color("e6d481"))
	if spell_time>0:
		var at := actors[active].position+facing_vector(directions[active])*20+Vector2(0,-18)
		guides.draw_rect(Rect2(at-Vector2(2,6),Vector2(4,12)),Color("a1ced2"))
		guides.draw_rect(Rect2(at-Vector2(6,2),Vector2(12,4)),Color("a1ced2"))
