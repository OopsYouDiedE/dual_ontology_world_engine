extends RefCounted
## Godot owns every scene, sprite and visual label. Core publishes semantic IDs.

const CATALOG_PATH := "res://assets/data/presentation_catalog.json"
const GEOMETRY := preload("res://scenes/components/maps/GeometryCatalog.gd")
static var _catalog: Dictionary = {}

static func catalog() -> Dictionary:
	if _catalog.is_empty(): _catalog = JSON.parse_string(FileAccess.get_file_as_string(CATALOG_PATH))
	return _catalog

static func snapshot(value: Dictionary) -> Dictionary:
	if value.is_empty(): return {}
	var result := value.duplicate(false)
	var bindings := catalog()
	var map_id := str(value.get("map", ""))
	var viewer := str(value.get("viewer", ""))
	var cast: Dictionary = bindings.actors
	result.resource = str(bindings.views[str(value.get("view_kind", "world"))])
	result.presentation = value.get("presentation", {}).duplicate(false)
	result.presentation.player_actor = str(result.presentation.get("player_actor", viewer))
	result.presentation.player_sprite = str(cast.get(result.presentation.player_actor, ""))
	result.cast = cast
	result.sites = {}
	for site in GEOMETRY.map_data(map_id).get("sites", {}):
		var item: Dictionary = GEOMETRY.map_data(map_id).sites[site]
		result.sites[site] = {"title": str(item.get("label", site))}
	for site: Dictionary in GEOMETRY.additions(map_id).get("sites", []):
		result.sites[str(site.id)] = {"title": str(site.get("label", site.id))}
	var map_bindings: Dictionary = bindings.maps.get(map_id, {})
	result.sites.merge(map_bindings.get("site_titles", {}), true)
	result.sprite_sites = map_bindings.get("sprite_sites", {})
	result.site_aliases = map_bindings.get("site_aliases", {})
	result.people = []
	for source: Dictionary in value.get("people", []):
		var who := str(source.key)
		if not cast.has(who): continue
		var person := source.duplicate(false)
		person.sprite = str(cast[who])
		person.frame = ""
		result.people.append(person)
	var minute := int(floor(float(value.get("game_time", 0))))
	result.hud = {"title": str(bindings.title), "room": str(value.get("location_title", value.get("hud", {}).get("room", ""))), "time": "%d:%02d" % [int(minute / 60) % 24, minute % 60]}
	return result

static func event(value: Dictionary) -> Dictionary:
	var result := value.duplicate(false)
	if value.has("snapshot"):
		result.snapshot = snapshot(value.snapshot)
		result.resource = result.snapshot.get("resource", "")
	if value.has("execution_snapshot"): result.execution_snapshot = snapshot(value.execution_snapshot)
	return result
