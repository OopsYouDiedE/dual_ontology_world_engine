@tool
extends Resource
## Generated data resource. The editable source is tools/furniture YAML/JSON.
@export var definition: Dictionary = {}

func valid() -> bool:
	return definition.has_all(["id", "parts", "states", "surfaces", "interactions", "default_state"]) and definition.states.has("normal") and definition.states.has(definition.default_state)
