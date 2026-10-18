# 规划ref结构键：静态审读与未执行探针

只修改core/00-data.dv与verification/session/15-action-planning.dv，保留已采纳inspection私有根改动。未读设计规范，未构建或运行。修改前原字节及SHA-256在before-*与ref-key-original-fingerprints.json。主代理负责rt_is_text ABI、编译器及文档。

标准ref仅限按type/object/facet/field/(可选subject)原顺序的4或5个二元素字段对，type必须ref，全部值为文本。键R+字段数+冒号，再依次拼接每个完整值的UTF-16长度+冒号+原文本。长度分帧不依赖任何文本分隔符，因此a:b/c/d与a/b:c/d不碰撞；空字符串、显式空subject与省略subject各自独立。全部字段值原样保留，不做本地主体补全或语义归一。

非标准字段、顺序变化、重复字段、扩展字段、缺字段、非文本值、非ref类型或非列表一律J+原rt_json_stringify，不增加原不存在的字段读取。特殊UTF-16代理项及U+FFFD文本也走J：JSON编码器可能把孤立代理项替换为U+FFFD，若替换字符仍走R便会破坏原JSON键相等关系。有效非BMP文本保守走J，BMP Unicode不改变。R/J域互斥；满足标准JSON值相同的ref同走R，否则同走J，保留原序列化区分及原始错误路径。

查全四个相关生产/消费点：dependency_add写图，dependencies按ref取图，inspection_freshness写桶，inspection输入签名读取桶。dependency_topology和inspection_freshness内部memo名附ref-key-v1，避免旧结构键缓存误复用。依赖cache的最终result.refs仍是原完整ref值，budget、遍历/重复消除及错误次序不变。25-simulation的dvs_sim_ref_key有默认subject语义，属于另一合同，本次未改；跨文件未发现这里图/桶的其他读取。

DV的dv_same与Runtime精确相等没有改变，新helper只用于私有lookup字符串。subject在标准路径作为第五文本完整编码，fallback保持原JSON；不绕过dv_read的Cross-subject检查。缺字段仍先按旧JSON生成键，不在helper强读缺失字段；后续原有需要该字段的读取仍在原位置抛错。

## 未执行中性探针

inspection-probe.dv与old-ref-functions.dv沿同一生产依赖声明，旧函数从本次before-15-action-planning.dv提取仅改名；old-source.json记录来源指纹。实际调用只构造中性mind、方法、ref和目标，不打开正式会话。

keys模式逐对比较原JSON-key相等关系与新结构-key相等关系，覆盖分隔符碰撞、重复、空/不同subject、顺序、扩展/重复/缺失字段、非文本与非ref、Unicode/代理项/替换字符。dependencies模式比较按每个原完整ref查到的图邻接，以及原/新依赖闭包、expanded与stop。其他模式承接inspection重复桶/availability/refresh/错误首错及双向别名检查。inspection结果仅为比较把根内部键恢复为桶第一条完整ref的原JSON，桶值本身不克隆，保留别名检查能力；字段顺序、条目顺序和精确数直接比较。

运行脚本run-inspection-engineering.py仅待主代理授权统一工程执行，保存原输出、异常首行和源码一致性，不用检查数量或构建结果评价游戏。当前未生成任何探针运行证据，也不声称提速。
