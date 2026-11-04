extends RefCounted
## Renderer visibility is separate from physical activity and perception.

static func apply(root: Node, visible_to_player: bool) -> void:
	if not is_instance_valid(root): return
	var requested:=1 if visible_to_player else 0
	if int(root.get_meta("player_render_state",-1))==requested: return
	root.set_meta("player_render_state",requested)
	_apply(root,visible_to_player)

static func _apply(node: Node, enabled: bool) -> void:
	if node is VisualInstance3D:
		if not node.has_meta("player_render_layers"): node.set_meta("player_render_layers",node.layers)
		node.layers=int(node.get_meta("player_render_layers")) if enabled else 0
	for child: Node in node.get_children(): _apply(child,enabled)
