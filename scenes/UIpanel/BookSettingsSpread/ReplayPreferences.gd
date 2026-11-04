@tool
extends Node
## Settings controls in a replay edit this in-memory presentation model. It has
## no persistence, window, audio, input-map or global translation side effects.
signal setting_changed(section: String, key: String, value: Variant)
signal language_changed(code: String)

var _values: Dictionary = {}
var _actions: Dictionary = {}

static func capture(source: Node) -> Dictionary:
	if source == null: return {}
	var values: Dictionary = source.defaults()
	var actions: Dictionary = {}
	for section: String in values:
		for key: String in values[section]:
			values[section][key] = source.get_value(section, key)
	for action: String in values.get("controls", {}):
		actions[action] = source.action_text(action)
	return {"values": values.duplicate(true), "actions": actions}

func restore_data(data: Dictionary) -> void:
	_values = data.get("values", {}).duplicate(true)
	_actions = data.get("actions", {}).duplicate(true)
	for section: String in _values:
		for key: String in _values[section]:
			setting_changed.emit(section, key, _values[section][key])
	language_changed.emit(current_language())

func capture_data() -> Dictionary:
	return {"values": _values.duplicate(true), "actions": _actions.duplicate(true)}

func get_value(section: String, key: String) -> Variant:
	return _values.get(section, {}).get(key)

func set_value(section: String, key: String, value: Variant) -> bool:
	if not _values.has(section): _values[section] = {}
	_values[section][key] = value
	setting_changed.emit(section, key, value)
	return true

func current_language() -> String:
	return str(_values.get("language", {}).get("locale", "zh_CN"))

func set_language(code: String) -> void:
	set_value("language", "locale", code)
	language_changed.emit(code)

func action_text(action: String) -> String:
	return str(_actions.get(action, ""))
