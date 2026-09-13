extends SceneTree

func _initialize():
    call_deferred("check_room")

func check_room():
    var room = load("res://scenes/ResearchHallShowcase/ResearchHallShowcase.tscn").instantiate()
    root.add_child(room)
    await physics_frame
    var body = room.get_node("Player")
    var actor = body.get_node("Portrait/CharacterShowcase/PlayerCharacter")
    actor.set_physics_process(false)
    var start = body.position
    actor.velocity = Vector2(170, 0)
    for i in range(30):
        await physics_frame
    assert(body.position.x > start.x + 0.5, "Camera-relative movement did not move in world X")
    print("[3D Check] movement X: ", body.position - start)
    room.get_node("CameraRig").rotation.y = PI / 2
    start = body.position
    for i in range(30):
        await physics_frame
    assert(body.position.z < start.z - 0.5, "Orbit did not rotate movement into world Z")
    print("[3D Check] rotated movement Z: ", body.position - start)
    room.get_node("CameraRig").rotation.y = 0
    actor.velocity = Vector2(10000, 0)
    for i in range(30):
        await physics_frame
    assert(body.position.x < 7, "Character crossed 3D wall collider")
    print("[3D Check] wall collision X: ", body.position.x)
    actor.velocity = Vector2.ZERO
    assert(body.get_node("Portrait").get_texture() != null)
    print("[3D Check] PASS: native 3D movement, camera-relative direction, wall collision, viewport texture")
    quit()
