extends Area2D
## A scene-authored proximity trigger for one investigation site.

@export var site_id := ""

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _on_body_entered(body: Node2D) -> void:
	if is_instance_valid(owner) and "player" in owner and body == owner.player:
		owner._site_entered(site_id)

func _on_body_exited(body: Node2D) -> void:
	if is_instance_valid(owner) and "player" in owner and body == owner.player:
		owner._site_exited(site_id)
