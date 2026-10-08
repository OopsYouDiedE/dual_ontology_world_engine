@tool
extends Control
## 在检视栏切换预览页；进入各模块预制件修改真实节点及资源。每页都是共享组件，预览字段在组件自己的 Editor Preview 分组里。

@export_enum("标题", "对白", "现场HUD", "纸页弹窗", "选择菜单", "证物卡", "表单堆叠", "表单分页", "笔记本", "重演手稿", "学园图", "结局卡", "回放操作", "心声气泡", "现场提示", "房间遮罩", "文字模板", "按钮模板", "分隔线模板", "对白选项模板")
var preview_page: int = 0:
	set(value):
		preview_page = value
		if is_node_ready():
			$Pages.current_tab = value

func _ready() -> void:
	$Pages.current_tab = preview_page
