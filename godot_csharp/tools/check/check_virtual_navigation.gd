extends SceneTree
const PlayerNavigation := preload("res://tools/virtual_player_navigation.gd")
const Navigation := preload("res://scenes/Shared/FieldNavigation.gd")

class Walker extends RefCounted:
	var position := Vector2(128, 128)

class Map extends RefCounted:
	var calls := 0
	func walkable(_point, _walker) -> bool:
		calls += 1
		return true
	func nav_grid(_walker) -> AStarGrid2D:
		calls += 1
		var floors := {}
		for y in 9:
			for x in 9:
				floors[Vector2i(x, y)] = true
		return Navigation.build_route_grid(floors, [], Vector2i(9, 9))

class Field extends RefCounted:
	var player := Walker.new()
	var adapter := Map.new()
	var visible := false
	func in_sight(_point) -> bool: return visible

var passed := 0
var failed := 0
func check(condition: bool, label: String) -> void:
	if condition:
		passed += 1
		print("PASS " + label)
	else:
		failed += 1
		push_error("FAIL " + label)

func _initialize() -> void:
	var field := Field.new()
	var attempts := PlayerNavigation.offers(field)
	check(attempts.size() == 8, "unknown boundary retains ordinary directional attempts")
	check(field.adapter.calls == 0, "unknown attempts do not inspect hidden collision or routes")
	for offer in attempts:
		var point := Vector2(offer.location[0], offer.location[1])
		check(is_equal_approx(field.player.position.distance_to(point), 32.0), "short ordinary step")
		check(offer.input.point == offer.location and offer.input.action == "move", "public point matches ordinary input")
	field.visible = true
	var known := PlayerNavigation.offers(field)
	check(known.size() == 16 and field.adapter.calls > 0, "longer visible routes use formal navigation")
	print("TOTAL %d checks, %d passed, %d failed" % [passed + failed, passed, failed])
	quit(1 if failed else 0)
