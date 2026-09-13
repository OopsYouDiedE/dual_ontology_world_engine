class_name BookNotebook
extends Control

## 对应 game_resource/dist/app.js
## 原样搬运符文与三章手记浏览逻辑

const SIGILS: Array[String] = [
	"ᛠ", "ᛑ", "ᚹ", "ᛟ", "ᛉ", "ᛞ", "ᚦ", "ᛇ", "ᚨ", "ᛗ", "ᛚ", "ᚱ",
	"ᛃ", "ᛏ", "ᚷ", "ᛖ", "ᚾ", "ᛜ", "ᛒ", "ᚺ", "ᛋ", "ᛁ", "ᚢ", "ᚠ"
]

const NAMES: Array[String] = [
	"潮生", "余响", "行旅", "归藏", "庇护", "昼夜", "棘门", "界木",
	"言灵", "同生", "流转", "归途", "丰年", "指引", "馈赠", "双影",
	"余烬", "种子", "新生", "寒息", "日轮", "静止", "原力", "财富"
]

const DESCRIPTIONS: Array[String] = [
	"万物自潮水而来。", "声音散去，记忆仍在。", "无名者走过无名之地。", "将不愿遗忘之物收起。",
	"为远行者留一盏灯。", "光与影在此交替。", "越过荆棘，方能相见。", "根须连接两个世界。",
	"被说出的名字有了重量。", "彼此成为存在的证据。", "潮水从不沿原路归来。", "每条路都记得足迹。",
	"等待也会结出果实。", "向着最后一颗星。", "给予之物终将回响。", "另一个自己藏在水面。",
	"熄灭之后，尚有温度。", "未曾发生之事在此沉睡。", "破土时请保持安静。", "雾中传来冬天的呼吸。",
	"白昼的碎片。", "让这一瞬停留片刻。", "石头记得山的名字。", "所拥有的，皆为暂借。"
]

const CHAPTERS: Array[Array] = [
	[
		"潮间之物", "潮汐中的旅人", "它们在退潮时现身，循着记忆的纹路行走。", "不要询问姓名，只需为它们留一盏灯。",
		"余响之瓶", "VESSEL OF ECHOES", "封存离去者最后一句话。", "月落以后，瓶中会泛起潮声。",
		"渡鸦行箱", "THE RAVEN’S COFFER", "灰潮渡口", "第七个无月夜"
	],
	[
		"雾港遗存", "守灯人的契约", "雾气吞没航道时，守灯人便会醒来。", "灯火每明灭一次，就有一艘船归港。",
		"雾灯之钥", "KEY OF THE MIST", "钥匙没有齿，只有一道刻痕。", "握紧它，便能听见远方的钟声。",
		"漂流密匣", "DRIFTWOOD RELIQUARY", "雾港旧灯塔", "第三次雾钟响起"
	],
	[
		"无月之境", "昨日的回声", "它们没有影子，脚下却总有两行足迹。", "一行通向昨日，另一行尚未写成。",
		"遗忘之露", "DEW OF OBLIVION", "收集自无人记得的清晨。", "每一滴，都藏着一个未完的梦。",
		"夜航者之箱", "THE NIGHTFARER’S CHEST", "静默海岸", "最后一颗星沉没"
	]
]

var closed: bool = false
var chapter: int = 0
var sound_enabled: bool = false
var selected_rune: int = 0
var rx: float = 9.0
var ry: float = -7.0
var rz: float = -5.0

# 界面节点引用
var rune_name_label: Label
var rune_text_label: Label
var rune_count_label: Label
var chapter_indicator: Label
var chapter_title: Label
var chapter_text: Label
var item_title: Label
var item_subtitle: Label
var item_text: Label
var chest_title: Label
var chest_subtitle: Label
var left_page_number: Label
var right_page_number: Label
var left_footer: Label
var right_footer: Label
var book_container: PanelContainer
var rune_buttons: Array[Button] = []

func _ready() -> void:
	anchor_right = 1.0
	anchor_bottom = 1.0
	setup_ui()
	select_rune(0)
	turn(0)

func setup_ui() -> void:
	var root_vbox = VBoxContainer.new()
	root_vbox.set_anchors_preset(Control.PRESET_FULL_RECT)
	root_vbox.add_theme_constant_override("separation", 10)
	add_child(root_vbox)
	
	# 顶部控制栏
	var top_bar = HBoxContainer.new()
	top_bar.custom_minimum_size = Vector2(0, 45)
	root_vbox.add_child(top_bar)
	
	var toggle_btn = Button.new()
	toggle_btn.text = "✕ 合上手记"
	toggle_btn.pressed.connect(toggle)
	top_bar.add_child(toggle_btn)
	
	var prev_btn = Button.new()
	prev_btn.text = "◀ 上一页"
	prev_btn.pressed.connect(func(): turn(-1))
	top_bar.add_child(prev_btn)
	
	var next_btn = Button.new()
	next_btn.text = "下一页 ▶"
	next_btn.pressed.connect(func(): turn(1))
	top_bar.add_child(next_btn)
	
	chapter_indicator = Label.new()
	chapter_indicator.text = "潮间之物 01 — 03"
	top_bar.add_child(chapter_indicator)
	
	# 主体：左右分栏
	var main_split = HBoxContainer.new()
	main_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_split.add_theme_constant_override("separation", 20)
	root_vbox.add_child(main_split)
	
	# 左侧：24符文面板
	var rune_panel = VBoxContainer.new()
	rune_panel.custom_minimum_size = Vector2(320, 0)
	main_split.add_child(rune_panel)
	
	var rune_grid = GridContainer.new()
	rune_grid.columns = 6
	rune_panel.add_child(rune_grid)
	
	for i in range(SIGILS.size()):
		var b = Button.new()
		b.text = SIGILS[i]
		b.custom_minimum_size = Vector2(48, 48)
		b.pressed.connect(func(): select_rune(i))
		rune_grid.add_child(b)
		rune_buttons.append(b)
	
	rune_name_label = Label.new()
	rune_name_label.text = NAMES[0]
	rune_name_label.add_theme_font_size_override("font_size", 20)
	rune_panel.add_child(rune_name_label)
	
	rune_text_label = Label.new()
	rune_text_label.text = DESCRIPTIONS[0]
	rune_text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	rune_panel.add_child(rune_text_label)
	
	rune_count_label = Label.new()
	rune_count_label.text = "01 / 24"
	rune_panel.add_child(rune_count_label)
	
	# 右侧：书本双页内容
	book_container = PanelContainer.new()
	book_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	main_split.add_child(book_container)
	
	var pages_split = HBoxContainer.new()
	pages_split.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pages_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	pages_split.add_theme_constant_override("separation", 40)
	book_container.add_child(pages_split)
	
	# 左页
	var left_page = VBoxContainer.new()
	left_page.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pages_split.add_child(left_page)
	
	chapter_title = Label.new()
	chapter_title.add_theme_font_size_override("font_size", 22)
	left_page.add_child(chapter_title)
	
	chapter_text = Label.new()
	chapter_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	left_page.add_child(chapter_text)
	
	left_footer = Label.new()
	left_footer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	left_footer.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	left_page.add_child(left_footer)
	
	left_page_number = Label.new()
	left_page.add_child(left_page_number)
	
	# 右页
	var right_page = VBoxContainer.new()
	right_page.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pages_split.add_child(right_page)
	
	item_title = Label.new()
	item_title.add_theme_font_size_override("font_size", 20)
	right_page.add_child(item_title)
	
	item_subtitle = Label.new()
	right_page.add_child(item_subtitle)
	
	item_text = Label.new()
	item_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	right_page.add_child(item_text)
	
	chest_title = Label.new()
	chest_title.add_theme_font_size_override("font_size", 18)
	right_page.add_child(chest_title)
	
	chest_subtitle = Label.new()
	right_page.add_child(chest_subtitle)
	
	right_footer = Label.new()
	right_footer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right_footer.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	right_page.add_child(right_footer)
	
	right_page_number = Label.new()
	right_page.add_child(right_page_number)

func select_rune(i: int) -> void:
	selected_rune = i
	rune_name_label.text = NAMES[i]
	rune_text_label.text = DESCRIPTIONS[i]
	rune_count_label.text = "%02d / 24" % (i + 1)
	for idx in range(rune_buttons.size()):
		rune_buttons[idx].modulate = Color(1.3, 1.2, 0.8) if idx == i else Color(1, 1, 1)

func toggle() -> void:
	closed = not closed
	book_container.visible = not closed

func turn(delta: int) -> void:
	if closed:
		return
	chapter = (chapter + delta + 3) % 3
	var c: Array = CHAPTERS[chapter]
	
	chapter_indicator.text = "%s 0%d — 03" % [c[0], chapter + 1]
	chapter_title.text = c[1]
	chapter_text.text = c[2] + "\n\n" + c[3]
	
	item_title.text = c[4]
	item_subtitle.text = c[5]
	item_text.text = c[6] + "\n" + c[7]
	
	chest_title.text = c[8]
	chest_subtitle.text = c[9]
	
	right_footer.text = "采集地 · " + c[10]
	left_footer.text = "观察记录 · " + c[11]
	
	left_page_number.text = "%02d" % (chapter * 2 + 1)
	right_page_number.text = "%02d" % (chapter * 2 + 2)

func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE:
			toggle()
		elif event.keycode == KEY_RIGHT:
			turn(1)
		elif event.keycode == KEY_LEFT:
			turn(-1)
		elif event.keycode == KEY_ESCAPE and not closed:
			toggle()
