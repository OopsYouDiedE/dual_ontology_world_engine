# 编译器语法分支只读审查

审查对象：当前 `language_core/compiler/compiler.dv` 新增列表推导与elif分支，以及 `compiler/examples/syntax-probes/` 的样本与说明。不读取设计文件，不修改生产源码，不运行编译器或探针。

## 静态判断

未发现能够从现有分支直接证明的作用域泄漏、重复source求值或elif误接缺陷。此结论只覆盖人工源码审读，编译器自举和生成C#仍待主代理工程验证。

- `compiler.dv:636–642`：source使用外层env重命名，guard/projection使用追加binder后的局部env。`lookup`（114行）采用最后同名映射，因此嵌套同名binder覆盖内层体，而内层source仍能读取外层binder。位置后缀区分嵌套局部；`sf_shift_tokens`（624行）使分段解析的位置按原源码偏移校正。
- `compiler.dv:648–658`：source只在object comp_source初始化处emit一次。guard位于每次迭代if条件，projection只在guard为真时Add求值。结果按索引顺序构造。source列表本身并不复制。
- `compiler.dv:643–647`：checker依次递归检查source、guard、projection，binder只放入后两者的局部环境。既有声明函数参数检查仍会在projection内执行；绑定不会进入后续语句环境。`sf_emit_comprehension`中的add_local和表达式emit继续负责未绑定引用诊断。
- `compiler.dv:385–392`：仅if接受后续分支，且必须是同缩进line。elif降低为otherwise中的嵌套if，递归处理后续elif/else。其他行还原cursor，while不会接收elif。静态上没有把内层分支归给外层的路径。

## 明确限制与后续核对

1. **类型静态边界**：binder固定为Value；source没有List类型，guard没有Bool静态期望（643–647行）。例如 `[for x in 3: x]`、`[for x in [] if 7: x]`可以通过静态遍历，分别在运行期R.Items/R.Bool报错；空source时非法guard不会执行。这与样本说明一致，不能报告为完整集合泛型或完整静态检查。
2. **输入列表可变**：循环每步重新读取source.Count（654行）。如果guard/projection通过别名追加或删改source，遍历会看到变化；source表达式仍只求值一次，但没有快照迭代保证。当前说明仅承诺eager/ordered，未承诺快照。建议公共参考注明调用者在遍历中修改源列表时的动态索引行为，避免误解。
3. **样本覆盖有限**：现有样本覆盖source计数、嵌套遮蔽、外层捕获、空集合、投影惰性、elif链。主代理运行时应额外核对独立if之间、嵌套if和while之后的elif诊断，避免只以单条链样本判断所有缩进接线。
4. **生成器必须实编译**：C#代码通过字符串拼接生成（648–658行），人工审读不能替代C0→C1→C2→C3固定点和样本C#编译。此前未运行的探针不得写成已经验证。

没有修改附件之外的设计判断，也不据此声称游戏行为或性能改善。
