# Literal capacity 中性工具（准备后暂停）

仅新增本engineering目录文件；未修改生产、旧工具、候选compiler.dv或00-data.dv，未构建、测试或执行。根任务现已转Core/UI接线，本候选留待后续集中静态审查后才决定执行。

入口：`python <本目录>/run.py --out <全新证据目录> [--runtime <同一最终Runtime.cs>] [--before-source <冻结04/source>]`。

before仅使用冻结04已有缓存C3，按工具链同一指纹算法定位并复制缓存，不存在即停止，不重建冻结源；旧00-data来自候选目录原字节副本。公共core对两版本用同一当前源。after在执行时调用当前compiler()和当前00-data，故需要主代理先集成候选，当前尚不可解释为候选验收。两份生成C#都使用同一--runtime。

fixture为纯中性DV，覆盖literal副作用顺序、空/1/2/3/6/多元素、pair/single/list3引用别名、独立clone、记录更新与重复键、JSON/Rational。StartupObject wrapper调用实际生成函数，构造样本用3独立进程；输入构造与语义SHA在计时外，保留所有根。metrics不参与逐字比较，不外推游戏。异常仅比较type、primary message及此前真实副作用，原ex.ToString堆栈另存，不删除或伪造为相同。

尚未构建或验证工具本身。源hash、stdout/stderr、生成C#、失败日志都保留；不以capacity字符串代替运行语义核对。工具不载入章节、设计或游戏会话。
