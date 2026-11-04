class_name BuildingProjectResource
extends Resource
## Generated authoring package. YAML/JSON is the editable source of truth.
## Data stays engine-neutral until BuildingScene materializes the compiled geometry.
@export var schema := "dowe-building-project-v2"
@export var source_hash := ""
@export var document: Dictionary = {}
@export var compiled: Dictionary = {}

func is_valid_package() -> bool:
	return schema == "dowe-building-project-v2" and compiled.get("schema", "") == "dowe-building-compiled-v2" and compiled.get("units", "") == "metre" and compiled.get("coordinates", "") == "godot-y-up" and not compiled.get("buildings", []).is_empty()

func editing_source() -> Dictionary:
	return document.duplicate(true)
