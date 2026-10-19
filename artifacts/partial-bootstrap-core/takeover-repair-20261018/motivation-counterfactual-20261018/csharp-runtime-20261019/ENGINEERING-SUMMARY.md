# 结构化批次工程记录

2026-10-19。中性容器比较与实际游戏验收分开；不执行流程回归。正式会话另存frozen-04-integrated。

## 实际修正与比较

structured-engineering/run-01暴露空List接口枚举器的.NET特殊状态：空集合的Current错误与非空List枚举器不同。生产FrozenList已将空集合接口枚举交给私有原生空List；非空结构枚举器保持值类型，越界下标借原生List保留原错误。

run-02核对错误原消息、有序重复键、坏pair首错、严格值域、独立copy、父失效、memo、原生Session Save/WriteSave/Restore、Trace/WriteTrace、HostJSON及DAG原始字节。已保存的对应输出与stage02底本相同。run-03改为从私有数组批量复制独占可变根，run-04保留record_with具体冻结类型并避免重复接口索引；两次因实际代码变化重新比较，原始输入与输出均保留。Session使用中性DVProgram stub，只核对平台边界，不验证剧情或正式宿主执行。

独立代理重新计算源码清单、比较文件SHA及DAG原始payload摘要链，详见STRUCTURED-ENGINEERING-INDEPENDENT-REVIEW.md。run-01的execution-status只记录子进程，最终比较失败从comparison.json及驱动退出可知；工具后续补记录comparison阶段，原记录没有改写。

## 有限分配与耗时样本

run-04每份样本是新进程，固定输入并保留所有返回根，计时不含JSON摘要。下表是该样本的分配观察，不外推整局。

| 中性操作 | stage02分配字节 | 只读数组分配字节 |
| --- | ---: | ---: |
| 唯一小树冻结 | 10,041,696 | 9,121,696 |
| 唯一大树冻结 | 36,191,448 | 32,513,256–32,513,464 |
| 独立copy、读取、少数字段修改 | 11,417,200 | 9,665,200 |

最后一行的三个现实毫秒样本分别为底本47.2066 / 43.3986 / 51.8897，候选44.9445 / 70.0183 / 53.0326。CPU仍有波动，不能宣布稳定提速或由减少分配推算五日完成。结构化只消除冻结二次复制，根更新仍O(n)，没有实现HAMT/持久向量。

## 编译事实与失败

build-integrated/core保存编译器自举固定点与Core生成构建的工程记录，未执行领域程序。其运行时快照在随后CopyRoot/内部索引收尾前，不能代替最终宿主构建。

第一次宿主构建在生成DV时报告dialogue.dv调用不存在的dv_any；修成既有二元c1_any，原失败stdout/stderr保留。第二次`dotnet build godot_csharp/DualOntologyWorldEngine.sln -m:1 --disable-build-servers -p:NuGetAudit=false`退出0，实际耗时36.266秒；该路径按最终Runtime指纹重建C3并核对C2/C3源固定点，再生成Session与Godot宿主。记录见build-integrated/host-02-result.json及原始日志。没有启动Godot，不能据编译评价画面、几何搬运、人物行为或剧情达成。

实际内容变化包括恢复两层规划、真实给药/逐段救援/收闻与临床时序。这些形成新条件；C#中性差分只证明其覆盖的平台边界，不把整体DV行为变化包装成事实等价提速。
