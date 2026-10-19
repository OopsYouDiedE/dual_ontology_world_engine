# 两种源码指纹

source-hashes.json与after-source-hashes.json均用hashlib.sha256(Path(path).read_bytes())，保留CRLF/原始字节；freeze-result记录该口径。

generated-source-consistency.json使用language_core/tools/dv_sources.py的sources([game/entry.dv])返回的source，再sha256(source.encode('utf-8'))，逐项对照Session/obj/dv/source-manifest.json。这与build_session.py的实际构建代码相同：相对include展开时保留模块边界，include行置空、文本换行规范化及模块尾换行，原字节SHA与该模块SHA可能不同。其module_hashes不是raw源字节hash；differences为空只说明生成DV库输入未过期，不作为游戏验收。
