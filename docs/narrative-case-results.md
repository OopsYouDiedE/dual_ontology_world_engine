# 最后一席渡船：自主案例检查

初始行动槽顺序为阿芙、沈砚；同刻按登记顺序执行。所有组保持这一条件。初始药物费用余额 2 Q、盟约执行源 2 Q，燃料单独计量。合作前瞻只使用公开提议，不保证对方履行。

## 燃料紧张

初始 fuel=1，最终 fuel=0，事件中资源实际消耗=4；女儿安全=True，承诺履行=False，病人获救=False，承诺破裂=True。

- AV 0: **药师阿芙** / 把药送上渡船；药师阿芙.charges: 2→1，阿芙的药.loaded: False→True，把药送上渡船.used: False→True
- AV 0: **船夫沈砚** / 把最后一席留给女儿；最后一席渡船.fuel: 1→0，沈砚的女儿.safe: False→True，把最后一席留给女儿.used: False→True
- AV 0: **盟约执行源** / 约定的后果发生；盟约执行源.charges: 2→0，给阿芙的承诺.broken: False→True，药师阿芙.strain: 0→1
- AV 4: **药师阿芙** / 暂且坐下，整理呼吸；药师阿芙.strain: 1→0

共省略无状态变化的 wait 事件：0 次。

## 燃料充足

初始 fuel=2，最终 fuel=0，事件中资源实际消耗=5；女儿安全=True，承诺履行=True，病人获救=True，承诺破裂=True。

- AV 0: **药师阿芙** / 把药送上渡船；药师阿芙.charges: 2→1，阿芙的药.loaded: False→True，把药送上渡船.used: False→True
- AV 0: **船夫沈砚** / 把最后一席留给女儿；最后一席渡船.fuel: 2→1，沈砚的女儿.safe: False→True，把最后一席留给女儿.used: False→True
- AV 0: **盟约执行源** / 约定的后果发生；盟约执行源.charges: 2→0，给阿芙的承诺.broken: False→True，药师阿芙.strain: 0→1
- AV 4: **药师阿芙** / 暂且坐下，整理呼吸；药师阿芙.strain: 1→0
- AV 4: **船夫沈砚** / 履行给阿芙的承诺；最后一席渡船.fuel: 1→0，给阿芙的承诺.honoured: False→True，岸上的病人.saved: False→True，履行给阿芙的承诺.used: False→True

共省略无状态变化的 wait 事件：0 次。

## 燃料紧张·守诺优先

初始 fuel=1，最终 fuel=0，事件中资源实际消耗=3；女儿安全=False，承诺履行=True，病人获救=True，承诺破裂=False。

- AV 0: **药师阿芙** / 把药送上渡船；药师阿芙.charges: 2→1，阿芙的药.loaded: False→True，把药送上渡船.used: False→True
- AV 0: **船夫沈砚** / 履行给阿芙的承诺；最后一席渡船.fuel: 1→0，给阿芙的承诺.honoured: False→True，岸上的病人.saved: False→True，履行给阿芙的承诺.used: False→True
- AV 4: **药师阿芙** / 航行结束后，把余药留给沈砚的女儿；药师阿芙.charges: 1→0，沈砚的女儿.treated: False→True，航行结束后，把余药留给沈砚的女儿.used: False→True

共省略无状态变化的 wait 事件：0 次。

## 燃料紧张·未公开提议

初始 fuel=1，最终 fuel=0，事件中资源实际消耗=3；女儿安全=True，承诺履行=False，病人获救=False，承诺破裂=True。

- AV 0: **船夫沈砚** / 把最后一席留给女儿；最后一席渡船.fuel: 1→0，沈砚的女儿.safe: False→True，把最后一席留给女儿.used: False→True
- AV 0: **盟约执行源** / 约定的后果发生；盟约执行源.charges: 2→0，给阿芙的承诺.broken: False→True，药师阿芙.strain: 0→1
- AV 10: **药师阿芙** / 暂且坐下，整理呼吸；药师阿芙.strain: 1→0

共省略无状态变化的 wait 事件：0 次。

## 运行分析

燃料反事实：女儿安全=True, 承诺履行=False, 病人获救=False, 承诺破裂=True → 女儿安全=True, 承诺履行=True, 病人获救=True, 承诺破裂=True。同燃料权重反事实：女儿安全=True, 承诺履行=False, 病人获救=False, 承诺破裂=True → 女儿安全=False, 承诺履行=True, 病人获救=True, 承诺破裂=False。药物必须先装载，守诺机会才可救活病人；背弃保护承诺会留下 `broken` 和阿芙的 strain。航行后，阿芙仍有一份药：她是否帮助沈砚的女儿，由一般救助关切与失信后不再援助的条件关切共同决定。即使补足履约，已发生的背弃仍然影响援助。行动由 `Start/Run` 的效用选择产生，程序没有安排固定行动序列。

边界：这些条件关切来自作者设定，并非模型自行生成的人格变化；仍未覆盖跨作品、跨长篇的文学质量。
