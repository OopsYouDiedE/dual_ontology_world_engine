@tool
extends Control
## 在检视栏切换预览页；进入各原始预制件修改真实节点及资源。

@export_enum("标题", "对白", "共享HUD", "纸页弹窗", "重演手稿", "学园图", "回放操作", "心声气泡", "现场提示", "房间遮罩", "地图浏览HUD", "文字模板", "按钮模板", "分隔线模板", "对白选项模板", "一章AnswerHeader", "一章AnswerList", "一章Board", "一章ChapterHUD", "一章Comparisons", "一章Ending", "一章EvidenceCard", "一章Footer", "一章NameEntry", "一章Notebook", "一章NotebookSection", "一章SoleChoice", "一章SoundExperiment", "一章SoundResults", "一章TraceCard", "一章TubeMenu", "二章AnswerHeader", "二章AnswerList", "二章ChapterHUD", "二章ChoiceList", "二章ChoiceOption", "二章Confirm", "二章Ending", "二章EvidenceCard", "二章FlashTitle", "二章Footer", "二章Notebook")
var preview_page: int = 0:
	set(value):
		preview_page = value
		if is_node_ready():
			$Pages.current_tab = value

func _ready() -> void:
	$Pages.current_tab = preview_page
