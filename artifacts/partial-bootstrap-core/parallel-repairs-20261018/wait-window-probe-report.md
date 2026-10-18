# 等待白天边界与静态元数据缓存（第3轮准备）

仅修改chapter1/opening.dv，未改调查剧情、同伴选择、目标、配额、费用目录或实际执行链；未运行正式会话。

本人具有chapter2_profile且显式声明daytime_wait_chunk_limit时，凌晨等待长度另外受480日内起点截短。6:00不再选择跨入白天的180分钟片段，使用既有120分钟动作到8:00；临近起点继续使用既有60/30/15/5/1长度。不声明白天参数的主体保持原选择；本人更小的基础上限继续生效。

c1n_clock_boundaries与c1n_without_clock在原调用位置增加完整condition值的纯备忘包装，原函数体移入_uncached，递归次序保持。没有将boundaries提前到目标构造阶段执行，避免未走到的非法分支提前报错。目标的完整available_when身份检查不变，动态pending、本人time与非时钟真值每次仍核对；memo不缓存动态真值或人物结果。

## 实际中性差分

证据位于 `artifacts/wait-window-probes-01/`。before-opening.dv封存本轮改前源码，baseline-helpers.dv从其提取原算法并仅重命名；manifest.json保存编译源指纹。probe-bundle.dv、ranges.json、Program.cs、Runtime.cs以及生成/构建/执行原始stdout/stderr均保留。编译包含生产声明以解析依赖，main只直接调用中性helper，没有open/prepare/complete或正式人物输入。

normal.retry.stdout.txt实际记录：嵌套合取/否定、重复边界、非时钟条件、析取及常量输入的旧新boundaries/without_clock结果相同；修改返回列表后再次缓存读取不含注入项；完整condition改为600后返回[600]；调用前主体与独立重建值相同。旧6:00候选kind为wait.first_night.180，新为120；边界前一分钟为1；本人基础上限60时为60；不声明白天参数时仍为180。

畸形not缺item时，旧新执行均以Missing field: item拒绝；畸形all缺items时，旧新均以Missing field: items拒绝。错误日志单独保留，不缓存失败。

首个探针execute因入口rt_arg(1)而runner只传一个参数，出现IndexOutOfRangeException；这属于探针调用错误。原日志未覆盖，retry-commands.json明确追加占位首参后执行同一程序集，正常输入exit0，畸形输入按上述错误退出。未把该探针错误作为正式会话结果。

这些证据仅说明给定中性值的结果、边界和别名/错误合同；不证明剧情接触、同伴信息新鲜度或本轮正式进展改善。调查现场结果在侦探离开后才发生的断链未通过此改动宣称解决。
