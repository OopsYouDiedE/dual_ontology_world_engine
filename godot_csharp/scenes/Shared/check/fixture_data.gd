extends RefCounted
## Automatic checks only. Frozen expectations are independent of runtime content.

static var _cache: Dictionary = {}

static func data(fixture: String) -> Dictionary:
	if not _cache.has(fixture):
		var path := "res://scenes/Shared/check/Fixtures/%s.json" % fixture
		var json := JSON.new()
		var error := json.parse(FileAccess.get_file_as_string(path))
		assert(error == OK and json.data is Dictionary, "Invalid fixture: " + path)
		_cache[fixture] = json.data
	return _cache[fixture]

static func text(fixture: String, key: String) -> String:
	var value = data(fixture).texts[key]
	assert(value is String, "Invalid fixture text: " + fixture + "/" + key)
	return value

static func array(fixture: String, key: String) -> Array:
	var value = data(fixture)[key]
	assert(value is Array, "Invalid fixture array: " + fixture + "/" + key)
	return value.duplicate(true)

static func dictionary(fixture: String, key: String) -> Dictionary:
	var value = data(fixture)[key]
	assert(value is Dictionary, "Invalid fixture dictionary: " + fixture + "/" + key)
	return value.duplicate(true)
