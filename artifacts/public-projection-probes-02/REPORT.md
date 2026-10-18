# 公共授权投影重复修复工程记录

原失败证据保留在 `artifacts/partial-bootstrap-core/parallel-repairs-20261018/run-180s/`，未覆盖或重跑正式会话。

## 来源定位

gu.c2.document.apology来自同一内容动作，同时有本人grants、public_observers/public_grants和speech/listeners。Complete先本人观察、再现场旁观投影、再话语公共授权投影；所有路径曾共用execution_id。dv_perceive的观察ID为[execution_id, subject, local_ref]，故听众与本人的重复授权均可能碰撞。该动作的原声明grants/public_grants均单字段，未发现该行列表内重复。Core的Repeated observation拒绝保持不改。

## 修改

仅修改session/30-runner.dv的公共授权传播及其辅助函数。公共传播在当前执行事件尾段（逆向到最近action_execution）一次建立本人观察ID索引及来源记录。相同execution_id已实际发布的观察不再作为第二份感知进入mind；只补本次尚未投影的授权。同主体绑定先经dv_perceive真实映射，比较完整观察ID，不假设physical和local名字相同。索引不查询通用mind.seen，不把重复异常普遍吞掉。

新增public_grants_projection回执保存execution_id、action_id和已投影event/subject/local ref/observation_id；同一execution重复调用公共入口明确报Repeated public grant projection。不同execution及`-accepted`执行ID保持独立。原话语聆听和listener_parse路径不变。新观察event.grants是实际新投影的physical授权子集，declared_grants保留原完整授权，便于只读重算。

## 实际中性探针

02在最终尾段索引及实际grants子集修改后重新生成、编译并执行，正常分支退出0。第一版工程输出保留于01，不覆盖。

实际输出：部分交集仅剩一个观察，其local对象名是`different-listener-local`；speaker与listener各持两个真实观察ID（原字段及新补字段）；全覆盖剩余为0；全新execution首次传播正常产生两个观察并读到第二字段值2。重复公共调用明确报Repeated public grant projection；直接把同一Core观察再提交仍报Repeated observation。这些异常为有意负面工程探针，其原stderr均保留。

没有prepare/complete调用、没有真实人物状态、没有剧情答案，也未运行正式会话或流程回归。尚未验证新正式会话能否跨越原停滞及后续剧情；只读检查对新事件grants子集的实际重算尚未执行。建议公共工程参考同步新增投影回执及helper，不能将此记录写成游戏验收。
