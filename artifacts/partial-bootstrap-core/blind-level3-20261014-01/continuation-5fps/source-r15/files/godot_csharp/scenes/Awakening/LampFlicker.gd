extends Node2D
## Flame flicker for the scene's lamps (lights carry a base_energy meta).

var flames: Array[PointLight2D] = []
var clock := 0.0

func _ready() -> void:
	for child in get_parent().get_node("Lighting").get_children():
		if child is PointLight2D and child.has_meta("base_energy"):
			flames.append(child)

func _process(delta: float) -> void:
	clock += delta
	for index in flames.size():
		var lamp := flames[index]
		var base: float = lamp.get_meta("base_energy")
		lamp.energy = base * (1.0 + 0.035 * sin(clock * 5.3 + index * 1.7) + 0.02 * sin(clock * 13.1 + index * 0.6))
