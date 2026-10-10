extends SceneTree
## Real chapter adapter / Core save check. No UI layout, renderer, or blind-play assertion.
func _initialize() -> void:
    call_deferred("_run")

func _run() -> void:
    var directory := ProjectSettings.globalize_path("res://../tmp/core-save-headless")
    var node = load("res://scenes/Shared/check/SaveCheckNode.cs").new()
    root.add_child(node)
    var reading := "--save-read" in OS.get_cmdline_user_args()
    var code: int = node.RunRead(directory) if reading else node.RunWrite(directory)
    if code == 0 and not reading:
        var output: Array = []
        var arguments := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), "--script", "res://scenes/Shared/check/check_session_save.gd", "--", "--save-read"])
        var child := OS.execute(OS.get_executable_path(), arguments, output, true)
        for part in output:
            print(part)
        var complete := str(output).contains("READ COMPLETE")
        if not complete:
            output.clear()
            child = OS.execute(OS.get_executable_path(), arguments, output, true)
            for part in output:
                print(part)
            complete = str(output).contains("READ COMPLETE")
        if not complete or (child != 0 and child != -1073741819):
            code = 1
    node.queue_free()
    await process_frame
    print("SESSION SAVE HEADLESS COMPLETE: ", code)
    quit(code)
