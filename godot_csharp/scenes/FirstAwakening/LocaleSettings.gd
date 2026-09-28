extends Node
## Shared locale choice for Godot UI and DialogueManager translations.
signal language_changed(code: String)

const LANGUAGES := ["zh_CN", "en", "ja"]
const SAVE_PATH := "user://language.cfg"

func _ready() -> void:
	var saved := ConfigFile.new()
	var system_locale := TranslationServer.get_locale()
	var code := "ja" if system_locale.begins_with("ja") else "en" if system_locale.begins_with("en") else "zh_CN"
	if saved.load(SAVE_PATH) == OK:
		code = str(saved.get_value("language", "locale", code))
	_apply(code)

func current_language() -> String:
	var locale := TranslationServer.get_locale()
	return "ja" if locale.begins_with("ja") else "en" if locale.begins_with("en") else "zh_CN"

func set_language(code: String) -> void:
	if not LANGUAGES.has(code): return
	if current_language() == code: return
	_apply(code)
	var saved := ConfigFile.new()
	saved.set_value("language", "locale", code)
	saved.save(SAVE_PATH)
	language_changed.emit(code)

func _apply(code: String) -> void:
	TranslationServer.set_locale(code if LANGUAGES.has(code) else "zh_CN")
