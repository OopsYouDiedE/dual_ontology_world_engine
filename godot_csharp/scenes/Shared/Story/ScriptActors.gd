extends Node
## 仅显示脚本声明的新人物，直接调用原 StoryActor 与原 SpriteFrames。
const Actor := preload("res://scenes/Story/StoryActor.gd")
var figures := {}

func refresh(parent: Node2D, declarations: Array, anchor: Vector2) -> void:
	var present := {}
	for i in declarations.size():
		var declaration: Dictionary = declarations[i]
		var key := String(declaration.id)
		present[key] = true
		if not figures.has(key):
			var actor := CharacterBody2D.new()
			actor.set_script(Actor)
			actor.standard = load(String(declaration.frames_path))
			actor.name = "Script_" + key
			actor.position = anchor + Vector2(36 + i * 32, 16)
			parent.add_child(actor)
			figures[key] = actor
	for key in figures.keys():
		if not present.has(key):
			figures[key].queue_free()
			figures.erase(key)
