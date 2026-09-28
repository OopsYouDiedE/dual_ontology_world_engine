extends TextureRect
## Generated inspection art; the caption is authored separately by the investigation.
var kind := ""
const ROOT := "res://ShareAsset/Internal/Props/FirstAwakeningEvidence/"

func _ready() -> void:
	if custom_minimum_size == Vector2.ZERO:
		custom_minimum_size = Vector2(340,340)
	expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var filename := "trace" if kind == "traces" else "number" if kind.begins_with("room") else kind
	if not filename.is_empty():
		texture = load(ROOT + filename + ".png")
