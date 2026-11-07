extends RefCounted
## Capture the real viewport through the renderer's asynchronous readback queue.
## GPU access stays on the render thread; CPU encoding and disk IO use one worker.
var _mutex := Mutex.new()
var _done := false
var _task := -1
var _started_us := 0
var _save_path := ""
var _save := false
var _logical_size := Vector2i.ZERO
var _result := {"png": PackedByteArray(), "encode_us": 0, "save_us": 0, "readback_us": 0, "request_us": 0, "error": OK, "message": "", "texture_format": -1}

func start(texture: RID, logical_size: Vector2i, save_path: String, save: bool) -> void:
	_started_us = Time.get_ticks_usec()
	_save_path = save_path
	_save = save
	_logical_size = logical_size
	RenderingServer.call_on_render_thread(_request.bind(texture))

func _request(texture: RID) -> void:
	var started := Time.get_ticks_usec()
	var rd := RenderingServer.get_rendering_device()
	if rd == null:
		_fail("Asynchronous viewport capture requires the project's RenderingDevice renderer.")
		return
	var target := RenderingServer.texture_get_rd_texture(texture)
	var format := rd.texture_get_format(target)
	var image_format := Image.FORMAT_RGBA8
	match format.format:
		RenderingDevice.DATA_FORMAT_R8G8B8A8_UNORM, RenderingDevice.DATA_FORMAT_R8G8B8A8_SRGB: image_format = Image.FORMAT_RGBA8
		RenderingDevice.DATA_FORMAT_R8G8B8_UNORM, RenderingDevice.DATA_FORMAT_R8G8B8_SRGB: image_format = Image.FORMAT_RGB8
		RenderingDevice.DATA_FORMAT_R16G16B16A16_SFLOAT: image_format = Image.FORMAT_RGBAH
		_:
			_fail("Unsupported viewport readback format: " + str(format.format))
			return
	_mutex.lock()
	_result.texture_format = format.format
	_mutex.unlock()
	var error := rd.texture_get_data_async(target, 0, _downloaded.bind(format.width, format.height, image_format))
	_mutex.lock()
	_result.request_us = Time.get_ticks_usec()-started
	_mutex.unlock()
	if error != OK: _fail("Viewport asynchronous readback failed: " + error_string(error))

func _downloaded(data: PackedByteArray, width: int, height: int, image_format: int) -> void:
	_mutex.lock()
	_result.readback_us = Time.get_ticks_usec()-_started_us
	_mutex.unlock()
	var task := WorkerThreadPool.add_task(_encode.bind(data,width,height,image_format), false, "MCP PNG receipt")
	_mutex.lock()
	_task = task
	_mutex.unlock()

func _encode(data: PackedByteArray, width: int, height: int, image_format: int) -> void:
	var started := Time.get_ticks_usec()
	var image := Image.create_from_data(width,height,false,image_format,data)
	if image_format == Image.FORMAT_RGBAH: image.convert(Image.FORMAT_RGBA8)
	if image.get_size() != _logical_size: image.resize(_logical_size.x,_logical_size.y)
	if image.detect_alpha() != Image.ALPHA_NONE:
		var bg := Image.create(_logical_size.x, _logical_size.y, false, Image.FORMAT_RGBA8)
		bg.fill(Color(0.0901961, 0.12549, 0.14902, 1.0))
		bg.blend_rect(image, Rect2i(Vector2i.ZERO, _logical_size), Vector2i.ZERO)
		image = bg
	var png := image.save_png_to_buffer()
	var encode_us := Time.get_ticks_usec()-started
	started = Time.get_ticks_usec()
	var error := OK
	if _save:
		var file := FileAccess.open(_save_path,FileAccess.WRITE)
		if file != null: file.store_buffer(png)
		else: error = FileAccess.get_open_error()
	_mutex.lock()
	_result.png = png
	_result.encode_us = encode_us
	_result.save_us = Time.get_ticks_usec()-started
	_result.error = error
	_done = true
	_mutex.unlock()

func _fail(message: String) -> void:
	_mutex.lock()
	_result.error = ERR_UNAVAILABLE
	_result.message = message
	_done = true
	_mutex.unlock()

func complete() -> bool:
	_mutex.lock()
	var ready: bool = _done and (_task >= 0 or _result.message != "")
	_mutex.unlock()
	return ready

func result() -> Dictionary:
	if _task >= 0: WorkerThreadPool.wait_for_task_completion(_task)
	return _result
