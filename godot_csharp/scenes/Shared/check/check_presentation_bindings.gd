extends SceneTree
## 中性演员绑定合同检查：替换身份键不改变节点引用；缺绑定须明确报错。
const Bindings := preload("res://scenes/Shared/Field/PresentationBindings.gd")
var checks := 0
var failures := 0

func _initialize() -> void:
	call_deferred("_run")

func _run() -> void:
	var root := Node.new()
	get_root().add_child(root)
	var first := CharacterBody2D.new()
	first.name = "ActorA"
	root.add_child(first)
	var second := CharacterBody2D.new()
	second.name = "ActorB"
	root.add_child(second)
	var bindings := {"player_v2": "ActorA", "visitor_v2": "ActorB"}
	_expect(Bindings.actor_error(root, bindings, "player_v2") == "", "renamed stable IDs resolve")
	_expect(root.get_node(bindings.player_v2) == first, "player node reference is unchanged")
	_expect(root.get_node(bindings.visitor_v2) == second, "visitor node reference is unchanged")
	bindings.visitor_v2 = "Absent"
	var missing := Bindings.actor_error(root, bindings, "player_v2")
	_expect(missing.contains("visitor_v2 -> Absent"), "missing node reports identity and path")
	var no_player := Bindings.actor_error(root, {"visitor_v2": "ActorB"}, "player_v2")
	_expect(no_player.contains("Missing player actor binding"), "missing player binding reports explicitly")
	_expect(Bindings.actor_error(root, {"p": "ActorA", "q": "ActorA"}, "p").contains("duplicate"), "duplicate node binding is rejected")
	print("Presentation bindings: %d checks, %d failures" % [checks, failures])
	if "--missing-node" in OS.get_cmdline_user_args():
		Bindings.fail(root, missing)
		return
	if "--missing-player" in OS.get_cmdline_user_args():
		Bindings.fail(root, no_player)
		return
	quit(1 if failures else 0)

func _expect(ok: bool, label: String) -> void:
	checks += 1
	if not ok:
		failures += 1
		push_error(label)
