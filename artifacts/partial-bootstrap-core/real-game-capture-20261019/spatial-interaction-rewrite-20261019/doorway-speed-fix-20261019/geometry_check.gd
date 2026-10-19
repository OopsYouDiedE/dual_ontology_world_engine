extends SceneTree
func _initialize() -> void:
    run.call_deferred()
func run() -> void:
    var script = load("res://scenes/Shared/Field/SpatialInput.gd")
    var adapter = load("res://scenes/Chapter2/Field/CampusMapView.gd").new()
    adapter.key = "zone_yi"
    adapter.map = load("res://scenes/Chapter2/Maps/zone_yi.tscn").instantiate()
    root.add_child(adapter)
    adapter.add_child(adapter.map)
    adapter._build_grid()
    var rows: Array = []
    for point in [Vector2(811.743591308594,287), Vector2(811.743591308594,360), Vector2(704,280)]:
        rows.append({"point": [point.x,point.y], "departure_room": adapter.departure_room(point), "entered_room": adapter.entered_room(point), "doorway_rooms": adapter.doorway_rooms(point), "walkable": adapter.walkable(point)})
    print(JSON.stringify({"scope":"static-authored-geometry-only; no session, state injection or input", "spatial_script_loadable":script != null and script.can_instantiate(), "points":rows}))
    quit()
