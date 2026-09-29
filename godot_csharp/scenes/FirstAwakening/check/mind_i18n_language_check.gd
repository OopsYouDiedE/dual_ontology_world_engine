extends SceneTree

const Language = preload("res://scenes/FirstAwakening/MindLanguage.gd")
const DraftEditor = preload("res://scenes/FirstAwakening/MindDraftEditor.gd")
const SITES := {
	"door":{"title":"隔离门与内销"}, "water":{"title":"水阀"},
	"window_0":{"title":"铁格雾窗"}, "window_1":{"title":"铁格雾窗"}
}
var failures: Array[String] = []

func require(ok: bool, message: String) -> void:
	if not ok: failures.append(message)

func _initialize() -> void:
	for locale in ["en", "ja"]:
		var catalog := Language.get_catalog(SITES, locale)
		require(catalog.people.size() == Language.PEOPLE.size(), locale + " people")
		for person in Language.PEOPLE:
			var displayed := Language.display_person(person, locale)
			require(Language.canonical_person(displayed, locale) == person, locale + " person round trip")
		for verb in catalog.verbs:
			for target in catalog.targets:
				var action: String = catalog.people[0] + " " + verb.word + " " + target.word if locale == "en" else catalog.people[0] + target.word + verb.word
				var parsed := Language.compile_text(action, SITES, "", locale)
				require(parsed.get("codes", []) == ["ACT|侦探|%s|%s" % [verb.code, target.key]], locale + " action " + action + " => " + str(parsed))
		for form in catalog.condition_forms:
			var action: String = catalog.people[1] + " inspect water valve" if locale == "en" else catalog.people[1] + "水栓を調べる"
			# A condition is observed by the same person who chooses the action.
			var draft: String = catalog.people[1] + form.word + action
			var parsed := Language.compile_text(draft, SITES, "", locale)
			require(parsed.get("codes", []) == ["WHEN|工程师|%s|%s" % [form.code, form.target], "ACT|工程师|Inspect|water"], locale + " condition " + draft + " => " + str(parsed))
		var implicit: String = "inspect water valve" if locale == "en" else "水栓を調べる"
		var compiled := Language.compile_text(implicit, SITES, "工程师", locale)
		require(compiled.get("codes", []) == ["ACT|工程师|Inspect|water"], locale + " canonical heading")
		var legacy := Language.compile_text("工程师观察水阀。", SITES, "侦探", locale)
		require(legacy.get("codes", []) == ["ACT|工程师|Inspect|water"], locale + " legacy Chinese draft")
		var local_word := Language.words("ACT|侦探|Inspect|water", SITES, locale)
		require(local_word == ("Detective inspect water valve" if locale == "en" else "探偵水栓を調べる"), locale + " displayed action")
		require(Language.compile_text("unknown magic", SITES, "侦探", locale).has("error"), locale + " rejects prose")
	call_deferred("_check_editors")

func _check_editors() -> void:
	var root_control := Control.new()
	root.add_child(root_control)
	for locale in ["en", "ja"]:
		var editor := DraftEditor.new()
		root_control.add_child(editor)
		editor.configure(Language.get_catalog(SITES, locale), "侦探")
		require(editor.get_completion_suffix() == ("go to isolation door." if locale == "en" else "隔離扉へ行く。"), locale + " example")
		if locale == "en":
			editor.text = "Detective ins"
			editor.set_caret_column(editor.text.length())
			require(editor.get_completion_suffix() == "pect", "English completion after actor")
			editor.text = "go t"
			editor.set_caret_column(editor.text.length())
			require(editor.get_completion_suffix() == "o", "English multiword verb completion")
		else:
			editor.text = "探偵水"
			editor.set_caret_column(editor.text.length())
			require(editor.get_completion_suffix() == "栓", "Japanese completion after actor")
		editor.queue_free()
	root_control.queue_free()
	for failure in failures: push_error(failure)
	print("MIND I18N LANGUAGE: %d failures" % failures.size())
	quit(0 if failures.is_empty() else 1)
