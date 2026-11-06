extends SubViewportContainer
## Integer nearest-neighbour presentation. HUD remains in the parent viewport.
@export var base_resolution := Vector2i(320, 248)
@onready var world_view: SubViewport = $Viewport
var pixel_scale := 1

func _ready() -> void:
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	stretch = false
	get_viewport().size_changed.connect(_fit)
	_fit()

func _fit() -> void:
	var available := Vector2i(get_viewport_rect().size)
	if available.x <= 0 or available.y <= 0: return
	var target := Vector2i(maxi(1,base_resolution.x),maxi(1,base_resolution.y))
	# Small windows use a smaller source area instead of fractional pixel interpolation.
	target = Vector2i(mini(target.x,available.x),mini(target.y,available.y))
	pixel_scale = maxi(1,mini(available.x/target.x,available.y/target.y))
	world_view.size = target
	size = Vector2(target)
	scale = Vector2.ONE*pixel_scale
	position = Vector2((available-target*pixel_scale)/2)

func set_resolution(value: Vector2i) -> void:
	base_resolution = value
	if is_node_ready(): _fit()
