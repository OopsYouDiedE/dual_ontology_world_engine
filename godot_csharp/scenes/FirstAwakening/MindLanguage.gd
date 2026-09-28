extends RefCounted
## Public word catalogue and syntax only. Feasibility belongs to replay execution.
const PEOPLE := ["侦探", "工程师", "厨师", "音乐家", "门外的人"]
const VERBS := [
	{"word":"走到", "code":"GoTo", "aliases":["走向", "前往", "到"]},
	{"word":"观察", "code":"Inspect", "aliases":["查看", "检查"]},
	{"word":"等候", "code":"Wait", "aliases":["等待"]},
	{"word":"交谈", "code":"Talk", "aliases":["询问"]},
	{"word":"召集", "code":"Convene", "aliases":[]},
	{"word":"接通", "code":"Connect", "aliases":[]},
	{"word":"断开", "code":"Disconnect", "aliases":[]},
	{"word":"推开", "code":"PushDoor", "aliases":["推"]},
	{"word":"拉开", "code":"PullDoor", "aliases":["拉"]},
	{"word":"用木楔撑开", "code":"BraceDoorWithWedge", "aliases":["用木楔撑住", "用木楔卡住", "撑开", "撑住"]},
	{"word":"取下", "code":"RemoveWedge", "aliases":["撤下", "拔出", "拔下", "撤走", "拿走"]},
	{"word":"用纸包起", "code":"WrapWedge", "aliases":["用纸包住", "包好", "包起"]},
	{"word":"拆开包纸取出", "code":"UnwrapWedge", "aliases":["拆包取出"]},
	{"word":"关闭", "code":"CloseDoor", "aliases":["关上"]},
	{"word":"插入", "code":"SlideInnerBolt", "aliases":["插上"]},
	{"word":"落下", "code":"OuterBar", "aliases":["放下"]},
	{"word":"抬起", "code":"LiftOuterBar", "aliases":["提起"]},
	{"word":"移开", "code":"MoveStop", "aliases":["挪开", "移走", "搬开"]},
	{"word":"放回", "code":"ReturnStop", "aliases":["移回", "搬回"]},
	{"word":"用湿布擦拭", "code":"WipeFloor", "aliases":["用湿布擦", "擦拭", "擦"]},
	{"word":"拧开", "code":"OpenValve", "aliases":[]},
	{"word":"拧紧", "code":"CloseValve", "aliases":["拧上"]},
	{"word":"擦去浮灰查看", "code":"WipeWindow", "aliases":["擦净查看"]},
	{"word":"比对", "code":"Compare", "aliases":["比较"]}
]
## Additional physical parts use distinct targets, even when they share a site position.
const PARTS := [
	{"word":"木楔", "key":"wedge", "aliases":["门底的木楔", "门底木楔"]},
	{"word":"生活翼内销", "key":"inner_bolt", "aliases":["内销"]},
	{"word":"主楼侧横闩", "key":"outer_bar", "aliases":["外闩", "横闩", "主楼外闩"]},
	{"word":"门厅地面", "key":"floor", "aliases":["地面", "地"]},
	{"word":"湿布", "key":"cloth", "aliases":[]},
	{"word":"主楼侧门外", "key":"outside", "aliases":["主楼侧", "门外"]}
]
const CONDITION_FORMS := [
	{"word":"看见热烟后，", "code":"Smoke", "target":"door", "aliases":["见到热烟后，", "看到热烟后，", "看见灰烟后，"]},
	{"word":"看见隔离门打开后，", "code":"DoorOpen", "target":"door", "aliases":[]},
	{"word":"看见隔离门关闭后，", "code":"DoorClosed", "target":"door", "aliases":[]},
	{"word":"看见内销插入后，", "code":"InnerBoltSlid", "target":"inner_bolt", "aliases":[]},
	{"word":"看见内销拔出后，", "code":"InnerBoltWithdrawn", "target":"inner_bolt", "aliases":[]},
	{"word":"看见外闩落下后，", "code":"OuterBarDropped", "target":"outer_bar", "aliases":[]},
	{"word":"看见外闩抬起后，", "code":"OuterBarLifted", "target":"outer_bar", "aliases":[]},
	{"word":"看见木楔撑门后，", "code":"WedgeBraced", "target":"wedge", "aliases":[]},
	{"word":"看见木楔撤下后，", "code":"WedgeRemoved", "target":"wedge", "aliases":[]},
	{"word":"看见木楔包好后，", "code":"WedgeWrapped", "target":"wedge", "aliases":[]},
	{"word":"看见止挡座移开后，", "code":"StopMoved", "target":"stop", "aliases":[]},
	{"word":"看见地面擦拭后，", "code":"FloorWiped", "target":"floor", "aliases":[]},
	{"word":"看见水阀出水后，", "code":"WaterRunning", "target":"water", "aliases":[]}
]
const ORDER_WORDS := ["起初，", "随后，", "接着，"]
## Old predicates remain public conveniences, never actor-specific permissions.
const PHRASES := [
	{"word":"走到门边观察", "code":"GoTo", "target":"door"},
	{"word":"走到门口观察", "code":"GoTo", "target":"door"},
	{"word":"去门边查看", "code":"GoTo", "target":"door"},
	{"word":"到门边观察", "code":"GoTo", "target":"door"},
	{"word":"用木楔撑门", "code":"BraceDoorWithWedge", "target":"door"},
	{"word":"拿木楔撑开门", "code":"BraceDoorWithWedge", "target":"door"},
	{"word":"把木楔塞在门底", "code":"BraceDoorWithWedge", "target":"door"},
	{"word":"将木楔垫在门底", "code":"BraceDoorWithWedge", "target":"door"},
	{"word":"把木楔垫在门底", "code":"BraceDoorWithWedge", "target":"door"},
	{"word":"把木楔包进纸里", "code":"WrapWedge", "target":"wedge"},
	{"word":"将木楔裹进纸中", "code":"WrapWedge", "target":"wedge"},
	{"word":"把木楔裹进纸里", "code":"WrapWedge", "target":"wedge"}
]

const SITE_KEYS := ["door", "paper", "stop", "water", "clock", "notebook", "drawer", "study", "tear", "box_mark", "meeting", "rations", "wash", "cup", "hinge", "weight", "slit", "trace", "floor_wipe", "engineer", "chef", "musician", "transmitter", "floor"]
const DEFAULT_SITE_IDS := {"door":151, "paper":152, "stop":153, "floor_wipe":154, "slit":155}
const VERB_ALIAS_IDS := [[201, 202, 203], [204, 205], [206], [207], [], [], [], [208], [209], [210, 211, 212, 213], [214, 215, 216, 217, 218], [219, 220, 221], [222], [223], [224], [225], [226], [227, 228, 229], [230, 231], [232, 233, 234], [], [235], [236], [237]]
const SITE_ALIAS_IDS := {"door":[251, 252, 253, 254], "paper":[255, 256], "stop":[257, 258], "wash":[259], "clock":[260], "transmitter":[261, 262], "meeting":[263]}
const PART_ALIAS_IDS := [[271, 272], [273], [274, 275, 276], [277, 278], [], [279, 280]]
const CONDITION_ALIAS_IDS := [[291, 292, 293], [], [], [], [], [], [], [], [], [], [], [], []]
const LANGUAGE_RESOURCE := "res://scenes/FirstAwakening/i18n/mind_language.%s.translation"
static var _translations: Dictionary = {}

static func _word(number: int, locale: String) -> String:
	if not _translations.has(locale):
		var language_code := "zh_CN" if locale == "zh" else locale
		var path := LANGUAGE_RESOURCE % language_code
		var translation := load(path) as Translation
		if translation == null:
			push_error("Missing mind language translation resource: " + path)
			return ""
		_translations[locale] = translation
	var key := "FA_LANG_%03d" % number
	var result: String = _translations[locale].get_message(key)
	if result.is_empty():
		push_error("Missing mind language key: " + key)
		return ""
	return result

static func _aliases(ids: Array, locale: String) -> Array[String]:
	var result: Array[String] = []
	for id in ids: result.append(_word(id, locale))
	return result

static func _locale(locale: String = "") -> String:
	var value := locale if not locale.is_empty() else TranslationServer.get_locale()
	if value.begins_with("en"): return "en"
	if value.begins_with("ja"): return "ja"
	return "zh"

static func example(locale: String = "") -> String:
	return _word(122, _locale(locale))

static func display_person(canonical: String, locale: String = "") -> String:
	var index := PEOPLE.find(canonical)
	return _word(1 + index, _locale(locale)) if index >= 0 else canonical

static func canonical_person(display: String, locale: String = "") -> String:
	if PEOPLE.has(display): return display
	for language in [_locale(locale), "zh", "en", "ja"]:
		for i in PEOPLE.size():
			if display == _word(1 + i, language): return PEOPLE[i]
	return display

static func _local_target(key: String, language: String, sites: Dictionary = {}) -> String:
	if key.begins_with("window_"): return _word(141, language) % (int(key.get_slice("_", 1)) + 5)
	if key.begins_with("bed_"): return _word(142, language) % (int(key.get_slice("_", 1)) + 5)
	if key.begins_with("room_"): return _word(143, language) % (int(key.get_slice("_", 1)) + 5)
	var index := SITE_KEYS.find(key)
	if index >= 0: return _word(51 + index, language)
	if sites.has(key):
		var language_code := "zh_CN" if language == "zh" else language
		var translation := load("res://scenes/FirstAwakening/i18n/first_awakening_ui.%s.translation" % language_code) as Translation
		return translation.get_message(sites[key].title_id)
	return key.replace("_", " ")

static func _local_catalog(sites: Dictionary, language: String) -> Dictionary:
	var zh := _get_catalog_zh(sites)
	var people: Array[String] = []
	for i in PEOPLE.size(): people.append(_word(1 + i, language))
	var verbs: Array[Dictionary] = []
	for i in VERBS.size(): verbs.append({"word":_word(11 + i, language), "code":VERBS[i].code, "aliases":[]})
	var targets: Array[Dictionary] = []
	for entry in zh.targets:
		var index := -1
		for i in PARTS.size():
			if PARTS[i].key == entry.key: index = i
		var word: String = _word(41 + index, language) if index >= 0 else _local_target(entry.key, language, sites)
		targets.append({"word":word, "key":entry.key, "aliases":[]})
	var forms: Array[Dictionary] = []
	for i in CONDITION_FORMS.size(): forms.append({"word":_word(81 + i, language), "code":CONDITION_FORMS[i].code, "target":CONDITION_FORMS[i].target, "aliases":[]})
	var conditions: Array[String] = []
	for person in people:
		for form in forms: conditions.append(person + form.word)
	var order_words: Array[String] = []
	for i in ORDER_WORDS.size(): order_words.append(_word(101 + i, language))
	var templates: Array[String] = []
	for i in 4:
		var item := _word(111 + i, language)
		if not item.is_empty(): templates.append(item)
	return {"locale":language, "people":people, "verbs":verbs, "targets":targets, "conditions":conditions,
		"condition_forms":forms, "order_words":order_words, "templates":templates,
		"phrases":[], "help":_word(121, language)}

static func get_catalog(sites: Dictionary = {}, locale: String = "") -> Dictionary:
	var language := _locale(locale)
	return _get_catalog_zh(sites) if language == "zh" else _local_catalog(sites, language)

static func _get_catalog_zh(sites: Dictionary = {}) -> Dictionary:
	var targets: Array[Dictionary] = []
	var current_sites: Dictionary = sites
	if current_sites.is_empty():
		current_sites = {}
		for key in DEFAULT_SITE_IDS:
			current_sites[key] = {"title":_word(DEFAULT_SITE_IDS[key], "zh")}
	var title_counts := {}
	var canonical_titles := {}
	for key in current_sites:
		var title: String = current_sites[key].title
		if not sites.is_empty():
			if str(key).begins_with("window_"): title = _word(144, "zh")
			elif str(key).begins_with("bed_"): title = _word(145, "zh")
			elif str(key).begins_with("room_"): title = _local_target(key, "zh")
			elif SITE_KEYS.has(key): title = _local_target(key, "zh")
		canonical_titles[key] = title
		title_counts[title] = title_counts.get(title, 0) + 1
	for key in current_sites:
		var title: String = canonical_titles[key]
		var aliases: Array = _aliases(SITE_ALIAS_IDS.get(key, []), "zh")
		if title_counts[title] > 1:
			# The four dormitories repeat window and bedding names. Never guess one.
			var room := int(str(key).get_slice("_", 1)) + 5
			title = "R-%02d的%s" % [room, title]
			aliases.append(title.replace("的", ""))
			if str(key).begins_with("window_"):
				aliases.append_array(["R-%02d窗户" % room, "R-%02d的窗户" % room, "R-%02d雾窗" % room])
		elif str(key).begins_with("room_"):
			aliases.append(title.replace(" ", ""))
		targets.append({"word": title, "key": key, "aliases": aliases})
	for i in PARTS.size(): targets.append({"word":_word(41 + i, "zh"), "key":PARTS[i].key, "aliases":_aliases(PART_ALIAS_IDS[i], "zh")})
	var people: Array[String] = []
	for i in PEOPLE.size(): people.append(_word(1 + i, "zh"))
	var verbs: Array[Dictionary] = []
	for i in VERBS.size(): verbs.append({"word":_word(11 + i, "zh"), "code":VERBS[i].code, "aliases":_aliases(VERB_ALIAS_IDS[i], "zh")})
	var forms: Array[Dictionary] = []
	for i in CONDITION_FORMS.size(): forms.append({"word":_word(81 + i, "zh"), "code":CONDITION_FORMS[i].code, "target":CONDITION_FORMS[i].target, "aliases":_aliases(CONDITION_ALIAS_IDS[i], "zh")})
	var conditions: Array[String] = []
	for person in people:
		for form in forms:
			for suffix in [form.word] + form.aliases: conditions.append(person + suffix)
	var order_words: Array[String] = []
	for i in ORDER_WORDS.size(): order_words.append(_word(101 + i, "zh"))
	var templates: Array[String] = []
	for i in 4: templates.append(_word(111 + i, "zh"))
	var phrases: Array[Dictionary] = []
	for i in PHRASES.size(): phrases.append({"word":_word(311 + i, "zh"), "code":PHRASES[i].code, "target":PHRASES[i].target})
	return {"locale":"zh", "people": people, "verbs": verbs, "targets": targets, "conditions": conditions,
		"condition_forms": forms, "order_words": order_words,
		"templates": templates, "phrases": phrases, "help": _word(121, "zh")}

static func _compact(text: String) -> String:
	return text.strip_edges().replace(" ", "").replace("　", "").replace("\t", "").replace(",", "，")

static func _error(line: int, reason: String) -> Dictionary:
	return {"error": _word(134, "zh") % [line + 1, reason], "line": line}

static func _target_keys(catalog: Dictionary) -> Dictionary:
	var result := {}
	for entry in catalog.targets:
		for spelling in [entry.word] + entry.aliases:
			result[_compact(spelling)] = entry.key
	return result

static func _parse_action(text: String, targets: Dictionary, default_person: String = "") -> Dictionary:
	var actor := ""
	for person in PEOPLE:
		if text.begins_with(person):
			actor = person
			text = text.trim_prefix(person)
			break
	if actor.is_empty(): actor = default_person
	if actor.is_empty(): return {"error":_word(131, "zh")}
	for phrase in PHRASES:
		if text == phrase.word: return {"actor":actor, "verb":phrase.code, "target":phrase.target}
	for verb in VERBS:
		for spelling in [verb.word] + verb.aliases:
			if text.begins_with(spelling):
				var noun: String = text.trim_prefix(spelling)
				if targets.has(noun): return {"actor":actor, "verb":verb.code, "target":targets[noun]}
			for marker in ["把", "将"]:
				if text.begins_with(marker) and text.ends_with(spelling):
					var noun: String = text.trim_prefix(marker).trim_suffix(spelling)
					if targets.has(noun): return {"actor":actor, "verb":verb.code, "target":targets[noun]}
	return {"error":_word(132, "zh")}

static func _compile_zh(draft: String, sites: Dictionary = {}, default_person: String = "") -> Dictionary:
	if not default_person.is_empty() and not PEOPLE.has(default_person):
		return _error(0, _word(133, "zh"))
	var catalog := _get_catalog_zh(sites)
	var targets := _target_keys(catalog)
	var sentences: Array[Dictionary] = []
	var codes: Array[String] = []
	var lines := draft.split("\n")
	for line in lines.size():
		var text: String = _compact(lines[line])
		if text.is_empty() or text.begins_with("#") or text.begins_with("＃"): continue
		text = text.replace("然后，", "；").replace("然后", "；")
		for mark in ["。", ".", ";"]: text = text.replace(mark, "；")
		for raw_sentence in text.split("；", false):
			var sentence: String = raw_sentence
			var ordered := false
			for prefix in ORDER_WORDS:
				if sentence.begins_with(prefix):
					ordered = true
					sentence = sentence.trim_prefix(prefix)
					break
			var observer := ""
			var condition := {}
			for person in PEOPLE:
				for form in CONDITION_FORMS:
					for suffix in [form.word] + form.aliases:
						var prefix: String = person + suffix
						if sentence.begins_with(prefix):
							observer = person
							condition = form
							sentence = sentence.trim_prefix(prefix)
							break
					if not observer.is_empty(): break
				if not observer.is_empty(): break
			if not observer.is_empty() and not ordered:
				for prefix in ORDER_WORDS:
					if sentence.begins_with(prefix):
						sentence = sentence.trim_prefix(prefix)
						break
			var parsed := _parse_action(sentence, targets, default_person)
			if parsed.has("error"): return _error(line, parsed.error)
			var code := "ACT|%s|%s|%s" % [parsed.actor, parsed.verb, parsed.target]
			if not observer.is_empty(): codes.append("WHEN|%s|%s|%s" % [observer, condition.code, condition.target])
			codes.append(code)
			sentences.append({"code":code, "actor":parsed.actor, "verb":parsed.verb,
				"target":parsed.target, "observer":observer, "condition":condition.get("code", ""),
				"smoke":condition.get("code", "") == "Smoke", "line":line})
	return {"codes":codes, "sentences":sentences}

static func _words_zh(code: String, sites: Dictionary = {}) -> String:
	var pieces := code.split("|")
	if pieces.size() != 4: return ""
	if pieces[0] == "WHEN":
		for form in CONDITION_FORMS:
			if form.code == pieces[2]: return pieces[1] + form.word.trim_suffix("，")
		return ""
	var catalog := _get_catalog_zh(sites)
	var action_word: String = pieces[2]
	var target_word: String = pieces[3]
	for verb in catalog.verbs:
		if verb.code == pieces[2]: action_word = verb.word
	for target in catalog.targets:
		if target.key == pieces[3]: target_word = target.word
	return pieces[1] + action_word + target_word

static func _local_error(line: int, language: String, reason: String) -> Dictionary:
	return {"error":_word(134, language) % [line + 1, reason], "line":line}

static func _normalize_local(value: String, language: String) -> String:
	value = value.strip_edges().replace("　", " ").replace("\t", " ")
	if language == "en": return " ".join(value.to_lower().split(" ", false))
	return value.replace(" ", "")

static func _local_action(raw: String, catalog: Dictionary, default_person: String, language: String) -> Dictionary:
	var text := _normalize_local(raw, language)
	var actor := ""
	for person in catalog.people:
		var token := _normalize_local(person, language)
		if text.begins_with(token):
			actor = canonical_person(person, language)
			text = text.trim_prefix(token).strip_edges()
			break
	if actor.is_empty(): actor = canonical_person(default_person, language)
	if not PEOPLE.has(actor):
		return {"error":_word(131, language)}
	if language == "en":
		for verb in catalog.verbs:
			var token := _normalize_local(verb.word, language)
			if not text.begins_with(token): continue
			var noun := text.trim_prefix(token).strip_edges()
			for target in catalog.targets:
				if noun == _normalize_local(target.word, language): return {"actor":actor, "verb":verb.code, "target":target.key}
	else:
		for target in catalog.targets:
			var noun := _normalize_local(target.word, language)
			if not text.begins_with(noun): continue
			var suffix := text.trim_prefix(noun)
			for verb in catalog.verbs:
				if suffix == _normalize_local(verb.word, language): return {"actor":actor, "verb":verb.code, "target":target.key}
	return {"error":_word(132, language)}

static func _compile_local(draft: String, sites: Dictionary, default_person: String, language: String) -> Dictionary:
	if not default_person.is_empty() and not PEOPLE.has(canonical_person(default_person, language)):
		return _local_error(0, language, _word(133, language))
	var catalog := _local_catalog(sites, language)
	var codes: Array[String] = []
	var sentences: Array[Dictionary] = []
	var lines := draft.split("\n")
	for line in lines.size():
		var raw: String = lines[line].strip_edges()
		if raw.is_empty() or raw.begins_with("#") or raw.begins_with("＃"): continue
		if language == "en": raw = raw.replace(" then ", ";").replace(" Then ", ";")
		for mark in ["。", ".", "；"]: raw = raw.replace(mark, ";")
		for part in raw.split(";", false):
			var text := _normalize_local(part, language)
			var ordered := false
			for prefix in catalog.order_words:
				var token := _normalize_local(prefix, language)
				if text.begins_with(token):
					text = text.trim_prefix(token).strip_edges()
					ordered = true
					break
			var observer := ""
			var condition := {}
			for person in catalog.people:
				for form in catalog.condition_forms:
					var token := _normalize_local(person + form.word, language)
					if text.begins_with(token):
						observer = canonical_person(person, language)
						condition = form
						text = text.trim_prefix(token).strip_edges()
						break
				if not observer.is_empty(): break
			if not observer.is_empty() and not ordered:
				for prefix in catalog.order_words:
					var token := _normalize_local(prefix, language)
					if text.begins_with(token):
						text = text.trim_prefix(token).strip_edges()
						break
			var parsed := _local_action(text, catalog, default_person, language)
			if parsed.has("error"): return _local_error(line, language, parsed.error)
			var code := "ACT|%s|%s|%s" % [parsed.actor, parsed.verb, parsed.target]
			if not observer.is_empty(): codes.append("WHEN|%s|%s|%s" % [observer, condition.code, condition.target])
			codes.append(code)
			sentences.append({"code":code, "actor":parsed.actor, "verb":parsed.verb,
				"target":parsed.target, "observer":observer, "condition":condition.get("code", ""),
				"smoke":condition.get("code", "") == "Smoke", "line":line})
	return {"codes":codes, "sentences":sentences}

static func compile_text(draft: String, sites: Dictionary = {}, default_person: String = "", locale: String = "") -> Dictionary:
	var language := _locale(locale)
	if language == "zh": return _compile_zh(draft, sites, default_person)
	var local := _compile_local(draft, sites, default_person, language)
	if not local.has("error"): return local
	# Existing Chinese drafts remain executable when the display language changes.
	var chinese := _compile_zh(draft, sites, canonical_person(default_person, language))
	return chinese if not chinese.has("error") else local

static func words(code: String, sites: Dictionary = {}, locale: String = "") -> String:
	var language := _locale(locale)
	if language == "zh": return _words_zh(code, sites)
	var pieces := code.split("|")
	if pieces.size() != 4: return ""
	var actor := display_person(pieces[1], language)
	var catalog := _local_catalog(sites, language)
	if pieces[0] == "WHEN":
		for form in catalog.condition_forms:
			if form.code == pieces[2]: return actor + form.word.trim_suffix(", ").trim_suffix("、")
		return ""
	var verb_word := pieces[2]
	var target_word := pieces[3]
	for verb in catalog.verbs:
		if verb.code == pieces[2]: verb_word = verb.word
	for target in catalog.targets:
		if target.key == pieces[3]: target_word = target.word
	if language == "en": return actor + " " + verb_word + " " + target_word
	return actor + target_word + verb_word
