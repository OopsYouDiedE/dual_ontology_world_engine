extends RefCounted
## Runtime content comes from C#; spatial data comes from the generated layout.

static var PLACES: Array
static var EVIDENCE: Dictionary
static var PERSON_NAMES: Dictionary
static var ACTOR_NODES: Dictionary
static var ROOM_NAMES: Dictionary
static var VARIABLES: Array
static var STATEMENTS: Dictionary
static var SITES: Dictionary = {}
static var EXTRA_SITES: Dictionary = {}
static var LIFE_TRACES: Dictionary = {}
static var POSTS: Dictionary = {}
static var OWN_OBJECTS: Dictionary = {}
static var OBJECT_SPRITES: Dictionary = {}
static var FURNITURE_ASIDES: Dictionary = {}
static var SITE_BOXES: Dictionary = {}
static var PROPS: Array = []
static var DIALOGUE_CAST: Dictionary = {}
## 以下均来自 chapter_01.story 的同名块；宿主只读，不再自备文字。
static var TEXT: Dictionary = {}
static var SOUND_SPOTS: Dictionary = {}
static var SOLE_TARGETS: Dictionary = {}
static var ROOM_REGIONS: Array = []
static var STAGES: Dictionary = {}
static var STAGE_EVENTS: Dictionary = {}
static var VERDICTS: Dictionary = {}
static var STAGING: Dictionary = {}

## 文案表里的一条文字；{名称} 由 args 替换。
static func text(key: String, args: Dictionary = {}) -> String:
	assert(TEXT.has(key), "chapter text key missing from chapter_01.story: " + key)
	var value: String = TEXT.get(key, key)
	for name in args:
		value = value.replace("{%s}" % name, str(args[name]))
	return value

static func apply_content(data: Dictionary) -> void:
	PLACES = data["PLACES"]
	EVIDENCE = data["EVIDENCE"]
	PERSON_NAMES = data["PERSON_NAMES"]
	ACTOR_NODES = data["ACTOR_NODES"]
	ROOM_NAMES = data["ROOM_NAMES"]
	VARIABLES = data["VARIABLES"]
	STATEMENTS = data["STATEMENTS"]
	TEXT = data["TEXT"]
	SOUND_SPOTS = data["SOUND_SPOTS"]
	SOLE_TARGETS = data["SOLE_TARGETS"]
	ROOM_REGIONS = data["ROOM_REGIONS"]
	STAGES = data["STAGES"]
	STAGE_EVENTS = data["STAGE_EVENTS"]
	VERDICTS = data["VERDICTS"]
	var spatial: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://scenes/Awakening/Script/generated_awakening_layout.json"))
	SITES.clear()
	EXTRA_SITES.clear()
	LIFE_TRACES.clear()
	POSTS.clear()
	OWN_OBJECTS.clear()
	OBJECT_SPRITES.clear()
	SITE_BOXES.clear()
	for source in spatial.sites:
		var site: Dictionary = source.duplicate()
		site.point = Vector2(float(source.x), float(source.y))
		SITES[str(source.id)] = site
	for id in spatial.extra_sites:
		var site: Dictionary = spatial.extra_sites[id].duplicate()
		# Generated kinds use target IDs; adapt them to the host's spatial representation.
		match str(site.kind):
			"person":
				assert(ACTOR_NODES.has(str(site.target)), "Unknown extra-site actor: " + str(site.target))
				site.person = str(site.target)
			"board":
				assert(site.has("x") and site.has("y"), "Board site requires authored x/y coordinates: " + str(id))
				site.board = true
				site.point = Vector2(float(site.x), float(site.y))
			_:
				assert(false, "Unsupported extra-site kind: " + str(site.kind))
		EXTRA_SITES[id] = site
	for source in spatial.life_traces:
		var trace: Dictionary = source.duplicate()
		trace.point = Vector2(float(source.x), float(source.y))
		LIFE_TRACES[str(source.id)] = trace
	for who in spatial.posts:
		POSTS[who] = Vector2(float(spatial.posts[who][0]), float(spatial.posts[who][1]))
	for id in spatial.objects:
		var object: Dictionary = spatial.objects[id]
		OWN_OBJECTS[str(object.person)] = id
		OBJECT_SPRITES[id] = str(object.sprite)
	for id in spatial.site_boxes:
		var box: Array = spatial.site_boxes[id]
		SITE_BOXES[id] = Rect2(float(box[0]), float(box[1]), float(box[2]), float(box[3]))
	STAGING.clear()
	for key in spatial.staging:
		STAGING[key] = Vector2(float(spatial.staging[key][0]), float(spatial.staging[key][1]))
	FURNITURE_ASIDES = spatial.furniture_asides
	DIALOGUE_CAST = spatial.dialogue_cast
	PROPS = spatial.props
