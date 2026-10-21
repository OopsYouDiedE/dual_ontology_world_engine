# 首批集中工程排错

候选隔离根：`tmp/detective-rpg-candidate-20261021`。首次构建和 `check_dv.py` 均返回 1；未启动游戏。编译器将 `40-chapter1.dv:2254:12` 定位为 `unknown function or primitive`：新增门条件误用了不存在的 `dv_any`。

修复为现役三值命题已有的 `not(all(not(...)))`，没有增加命题操作或改编译器。随后重新构建，具体输出见 `candidate-build-02.log`；语法结果见 `candidate-syntax-02.json`；Godot 重新导入见 `candidate-import-01.log`。这些只说明工程结果，不代替游戏验收。

第一次候选复制后补入了 `InvestigationUI.gd` 的实时三语占位和按钮文本绑定，保持已选值；第一次复制命令的工作目录错误使目标路径重复而未执行复制，已改为两个绝对路径后实际复制。该文件在运行前完成更新。最终运行来源以各轮 `source-before.json` / `source-after.json` 为准，初始候选复制指纹不冒充最终构建指纹。
