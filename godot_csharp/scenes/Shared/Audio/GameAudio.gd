extends Node
## 全局配乐与音效（自动加载为 GameAudio）。配乐两条轨交叉淡化、无缝循环；音效走小型播放器池。
## 场景脚本经 Sound.gd 调用，自动加载缺席时（无头检查脚本）静默跳过。
## 曲目与音效由 music/build_soundtrack.py 生成，曲目用途见 docs/design-standard.md 第五部分。

const MUSIC_PATH := "res://ShareAsset/Internal/Props/Audio/Music/%s.ogg"
const SFX_PATH := "res://ShareAsset/Internal/Props/Audio/Sfx/%s.ogg"
const MUSIC_DB := -9.0
const DUCK_DB := -9.0
const SILENT_DB := -60.0
const SFX_VOICES := 8

var _music: Array[AudioStreamPlayer] = []
var _active := 0
var _current := ""
var _ducked := false
var _fade: Tween
var _sfx: Array[AudioStreamPlayer] = []
var _next_sfx := 0
var _cache := {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	for i in 2:
		var player := AudioStreamPlayer.new()
		player.name = "Music%d" % i
		player.volume_db = SILENT_DB
		add_child(player)
		_music.append(player)
	for i in SFX_VOICES:
		var player := AudioStreamPlayer.new()
		player.name = "Sfx%d" % i
		add_child(player)
		_sfx.append(player)

func current_music() -> String:
	return _current

func play_music(id: String, fade := 1.6) -> void:
	if id == _current:
		return
	var stream := _load(MUSIC_PATH % id)
	if stream == null:
		return
	_current = id
	var looped: AudioStream = stream.duplicate()
	if looped is AudioStreamOggVorbis:
		looped.loop = true
	var old := _music[_active]
	_active = 1 - _active
	var new := _music[_active]
	new.stream = looped
	new.volume_db = SILENT_DB
	new.play()
	_fade_to(new, old, fade)

func stop_music(fade := 1.6) -> void:
	_current = ""
	_fade_to(null, _music[_active], fade)

## 重演回放时压低配乐，结束后恢复。
func duck(on: bool) -> void:
	if _ducked == on:
		return
	_ducked = on
	var player := _music[_active]
	if player.playing and _current != "":
		if _fade:
			_fade.kill()
		_fade = create_tween()
		_fade.tween_property(player, "volume_db", _target_db(), 0.6)

func play_sfx(id: String, volume_db := 0.0, pitch := 1.0) -> void:
	var stream := _load(SFX_PATH % id)
	if stream == null:
		return
	var player := _sfx[_next_sfx]
	_next_sfx = (_next_sfx + 1) % _sfx.size()
	player.stream = stream
	player.volume_db = volume_db
	player.pitch_scale = pitch
	player.play()

func _target_db() -> float:
	return MUSIC_DB + (DUCK_DB if _ducked else 0.0)

func _fade_to(incoming: AudioStreamPlayer, outgoing: AudioStreamPlayer, seconds: float) -> void:
	if _fade:
		_fade.kill()
	_fade = create_tween().set_parallel(true)
	if incoming:
		_fade.tween_property(incoming, "volume_db", _target_db(), seconds)
	if outgoing and outgoing.playing:
		_fade.tween_property(outgoing, "volume_db", SILENT_DB, seconds)
		_fade.chain().tween_callback(outgoing.stop)

func _load(path: String) -> AudioStream:
	if not _cache.has(path):
		_cache[path] = load(path) if ResourceLoader.exists(path) else null
		if _cache[path] == null:
			push_warning("GameAudio: 缺少音频 " + path)
	return _cache[path]
