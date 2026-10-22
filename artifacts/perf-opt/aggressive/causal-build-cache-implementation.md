# 安全只读因果程序集复用实现记录

分支 opt/fix-causal-build-cache，基线9d50b226；只修改工具、工程合同及有限工具测试，未读设计文件，未运行正式会话、完整DAG扫描或工程构建。

新增 --opt-causalbuildcache on|off，默认on，接入TOOL_FLAGS、基线全关与独立消融，并写入verification_config及其SHA-256。从--reuse-program兄弟causal-program复用，独立compiled-causal-program.json与causal-check-70入口。无可验证manifest正常构建并原子封存；已有manifest与完整v2来源、生成物、程序集、编译器、入口或插桩合同失配时拒绝复用。复制后再次验证，并保留精确项目文本、Runtime、Bridge、当前源码与编译器核对。每次仍重新生成本轮intake并实际运行完整因果核验，不复用旧报告。

构建和执行时限保持原值；result.causal_stages在构建/执行超时时仍保留已发生耗时与具体timeout_stage，未执行不填造执行耗时。仅工具程序集的生成/编译可省略，不改变会话、正式判定或证据封存要求。

验证：2026-10-23，在此分支运行 `python -B -m unittest test_causal_build_cache test_tool_hardening` 成功。新增fixture使用伪编译产物和mock子进程，不启动DV。覆盖冷构建封存、命中复用仍执行核验、关闭重建、缺manifest正常构建、入口/来源/二进制失配拒绝，以及构建与执行超时的独立记录；既有v2完整文件/复制竞态与矩阵开关工具检查亦通过。git diff --check无问题。临时fixture由TemporaryDirectory作用域清理。

尚未用真实新编译的因果程序集验证运行效果，尚未量化本修改的构建节省或整命令180秒。先前S4和S2-jia的causal manifest没有新封存合同，不能被补造为命中；第一次须正常构建。本次没有恢复或宣布任何游戏验收结论。
