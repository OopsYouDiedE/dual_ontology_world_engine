extends RefCounted
## 表现声明的校验不依赖任何章节身份；失败消息可用于 headless 检查。

static func actor_error(root: Node, bindings: Dictionary, player_key: String) -> String:
	if bindings.is_empty() or not bindings.has(player_key): return "Missing player actor binding: " + player_key
	var nodes := {}
	for key in bindings:
		var path := str(bindings[key])
		if path == "" or nodes.has(path): return "Invalid or duplicate actor node binding: " + str(key)
		nodes[path] = true
		if root.get_node_or_null(path) is not CharacterBody2D: return "Missing actor node binding: " + str(key) + " -> " + path
	return ""

static func fail(node: Node, message: String) -> void:
	push_error(message)
	if node.is_inside_tree(): node.get_tree().quit(1)
