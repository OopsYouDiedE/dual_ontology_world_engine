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
var _quitting := false

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	get_tree().auto_accept_quit = false
	get_tree().root.close_requested.connect(quit_cleanly)
	for i in 2:
		var player := AudioStreamPlayer.new()
		player.name = "Music%d" % i
		player.bus = _bus_or_master(&"Music")
		player.volume_db = SILENT_DB
		add_child(player)
		_music.append(player)
	for i in SFX_VOICES:
		var player := AudioStreamPlayer.new()
		player.name = "Sfx%d" % i
		player.bus = _bus_or_master(&"Sfx")
		add_child(player)
		_sfx.append(player)

## 配乐/音效/语音走各自的总线（default_bus_layout.tres；音量由 GameSettings 管理），缺总线时退回 Master。
static func _bus_or_master(bus: StringName) -> StringName:
	return bus if AudioServer.get_bus_index(bus) != -1 else &"Master"

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

## Release playback before the audio server and scene tree are destroyed.
func release_audio() -> void:
	if _fade:
		_fade.kill()
		_fade = null
	for player in _music + _sfx:
		if is_instance_valid(player):
			player.stop()
			player.stream = null
	_cache.clear()
	_current = ""
	_ducked = false

## stop() queues playback deletion; the driver must complete later mix steps first.
func release_audio_and_wait_for_mix() -> bool:
	var stopped_at := Time.get_ticks_usec()
	release_audio()
	var deadline := stopped_at + 500000
	var last_mix_at := stopped_at
	var completed_mixes := 0
	while Time.get_ticks_usec() < deadline:
		await get_tree().process_frame
		# Driver mix time is distinct from render frames, including on the dummy driver.
		var sampled_at := Time.get_ticks_usec()
		var mix_at := sampled_at - int(AudioServer.get_time_since_last_mix() * 1000000.0)
		# A 1ms separation rejects timestamp sampling jitter, rather than counting it as a mix.
		if mix_at > last_mix_at + 1000:
			last_mix_at = mix_at
			completed_mixes += 1
			if completed_mixes == 2:
				print_verbose("GameAudio: two driver mix steps completed after playback release.")
				return true
	push_error("GameAudio: audio driver did not complete two mix steps within the 500ms shutdown bound.")
	return false

func quit_cleanly(exit_code := 0) -> void:
	if _quitting: return
	_quitting = true
	# Start all host drains before awaiting any one of them. World/session files
	# must be sealed before audio teardown is allowed to terminate the process.
	var hosts := get_tree().get_nodes_in_group("lifecycle_hosts")
	for host in hosts:
		if is_instance_valid(host) and host.has_method("begin_shutdown"): host.begin_shutdown()
	for host in hosts:
		if is_instance_valid(host) and host.has_method("wait_for_shutdown"):
			await host.wait_for_shutdown()
	var drained: bool = await release_audio_and_wait_for_mix()
	get_tree().quit(exit_code if drained else (exit_code if exit_code != 0 else 1))

func _exit_tree() -> void:
	release_audio()
