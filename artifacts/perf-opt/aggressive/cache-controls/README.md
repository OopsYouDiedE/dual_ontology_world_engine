# 核心缓存开关与首夜深度修复

基线：5a530d6331be161c0cb6853a069ec5d4785fee15；分支opt/fix-cache-controls。未读设计文件，未运行正式会话。

## 变更

30新增plan/declaration显式memo包装器，保持原owner/key/value合同。plan组由CLI opt-plancache映射到DV_OPT_PLANCACHE，关闭完整规划结果、预测、主题、行动范围、检视新鲜度与重演候选可用性缓存；actor.dependency_cache在off时也不读取/保存，而重新执行相同依赖闭包。declaration组由opt-declcache映射到DV_OPT_DECLCACHE，关闭静态动作目录、方法/拓扑、推理输入引用、模型编译计划及空闲声明索引。没有全局关闭rt_memo；dirty、付费重演结果共享、证据索引保持独立。

Runtime增加一参数rt_memo_group_enabled ABI，编译器arity与工程文档同步；每个ValueContext采样一次CLI环境。未设置环境变量的其他宿主保持原来的缓存开启行为。本轮只完善CLI开关，不宣称未设置环境的其他宿主会由policy自动关闭全部组。

40仅修改c1n_wait_profile：直接保留actor，移除首日/房间/角色ID深度压低。等待目标和等待动作未修改；个人策略深度仍受统一prune_depth cap约束。

## 验证

- dotnet build dv_core/LanguageCore.sln -c Release -m:1 --disable-build-servers -p:NuGetAudit=false 成功，0警告、0错误，54.27秒，原始日志release-build.txt。
- python mcp/verification/check_cache_controls.py 成功，原始日志mechanism.txt。有限合成夹具直接调用生成机制，不开启会话：四种plan/declaration组合分别核对关闭不读旧条目、不写缓存、原生其他memo仍能读写；实际预测返回值与依赖refs/stop在四种组合一致；第二次预测和actor依赖结果命中确实随plan开关改变，方法索引随declaration独立改变。
- 直接调用生成c1n_wait_profile，验证八个旧白名单角色、demon及未知fixture身份均保留同一个actor对象及其深度/等待策略。
- git diff --check无差异错误；测试临时工程由TemporaryDirectory清理，.scratch无本轮遗留。

implementation.patch保留源码/文档/检查器完整差异；source-sha256.txt保留对应字节指纹。检查器源码位于mcp/verification，临时工程不入库。

## 边界

没有执行Core AI、formal-model正式会话、Godot或性能比较；没有宣称章末达成、速度提升、全局行为等价或真实封存/正文差分。完整规划缓存由共同包装器接线和Release编译覆盖，机制夹具没有构造完整剧情规划。新增环境开关只在ValueContext创建时采样，运行中改变环境不会改写现有上下文。
