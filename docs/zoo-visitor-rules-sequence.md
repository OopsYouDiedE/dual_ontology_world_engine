# 动物园游客守则：受控英文与实际 AST 序列

原文位置：[《动物园规则怪谈》“动物园园区游客守则”第 1—15 条](https://rule-wiki.wikidot.com/zoo-rule)。为避免在仓库中复制整篇外部作品，下面逐条保留编号并给出中文语义转述。受控英文来自 [`examples/zoo_visitor_rules_en.txt`](../examples/zoo_visitor_rules_en.txt)；AST 是 `RuleParser.parse_rules` 的实际解析结果，省略每个 `Rule.source` 中重复保存的输入文本。

记号：`E` 表示 `Expression`，`F` 表示 `Effect`；`REQUIRE`、`FORBID`、`PERMIT` 表示 `Directive.mode`。

## 第 1 条

**原文语义：** 看见逃出园区的兔子时，应远离并通知工作人员，不得靠近或触摸。

**受控英文：**

```text
WHEN ALL:
- visitor perceives escaped_rabbit
REQUIRE IN ORDER:
- visitor moves_away_from escaped_rabbit
- visitor reports_to staff escaped_rabbit
FORBID:
- visitor approaches escaped_rabbit
- visitor touches escaped_rabbit
```

**AST：**

```text
Rule(E(perceives, visitor, escaped_rabbit), [
  REQUIRE F(sequence, F(move_away_from, visitor, escaped_rabbit), F(report_to, visitor, staff, escaped_rabbit)),
  FORBID F(sequence, F(approach, visitor, escaped_rabbit), F(touch, visitor, escaped_rabbit))
])
```

## 第 2 条

**原文语义：** 猿类园区出现两条路且其中出现兔子时，选择左路并尽快离开。

**受控英文：**

```text
WHEN ALL:
- visitor is inside_monkey_zone
- monkey_zone_path_count is 2
- monkey_zone exhibits rabbit
REQUIRE IN ORDER:
- visitor chooses left_path
- visitor leaves monkey_zone quickly
```

**AST：**

```text
Rule(E(all, E(state, visitor, inside_monkey_zone), E(state, monkey_zone_path_count, 2), E(exhibit, monkey_zone, rabbit)), [
  REQUIRE F(sequence, F(choose, visitor, left_path), F(leave, visitor, monkey_zone, quickly))
])
```

## 第 3 条

**原文语义：** 身处大象园区时，应确认自己看到的只有大象。

**受控英文：**

```text
WHEN ALL:
- visitor is inside_elephant_zone
REQUIRE:
- visitor verifies only_elephants_are_visible
```

**AST：**

```text
Rule(E(state, visitor, inside_elephant_zone), [REQUIRE F(verify, visitor, only_elephants_are_visible)])
```

## 第 4 条

**原文语义：** 看见标为兔子血的商品时，不得购买。

**受控英文：**

```text
WHEN ALL:
- visitor perceives rabbit_blood_product
FORBID:
- visitor purchases rabbit_blood_product
```

**AST：**

```text
Rule(E(perceives, visitor, rabbit_blood_product), [FORBID F(purchase, visitor, rabbit_blood_product)])
```

## 第 5 条

**原文语义：** 不得独自在兔子园区的树荫下停留。

**受控英文：**

```text
WHEN ALWAYS:
FORBID:
- visitor stays rabbit_zone_tree_shade alone
```

**AST：**

```text
Rule(E(true), [FORBID F(stay, visitor, rabbit_zone_tree_shade, alone)])
```

## 第 6 条

**原文语义：** 工作人员向游客出售海洋馆门票时，游客不得接受。

**受控英文：**

```text
WHEN ALL:
- staff sells aquarium_ticket visitor
FORBID:
- visitor accepts aquarium_ticket
```

**AST：**

```text
Rule(E(sell, staff, aquarium_ticket, visitor), [FORBID F(accept, visitor, aquarium_ticket)])
```

## 第 7 条

**原文语义：** 看见海洋馆时，应离开该区域并拨打地图所示号码。

**受控英文：**

```text
WHEN ALL:
- visitor perceives aquarium
REQUIRE IN ORDER:
- visitor leaves aquarium_area
- visitor calls map_phone_number
```

**AST：**

```text
Rule(E(perceives, visitor, aquarium), [
  REQUIRE F(sequence, F(leave, visitor, aquarium_area), F(call, visitor, map_phone_number))
])
```

## 第 8 条

**原文语义：** 不得投喂兔子；可以投喂其他动物。

**受控英文：**

```text
WHEN ALWAYS:
FORBID:
- visitor feeds rabbit
PERMIT:
- visitor feeds other_animals
```

**AST：**

```text
Rule(E(true), [FORBID F(feed, visitor, rabbit), PERMIT F(feed, visitor, other_animals)])
```

## 第 9 条

**原文语义：** 在兔子园区听见并非来自游客的笑声时，撕下地图虚线部分，并持续握住碎片直到离开动物园。

**受控英文：**

```text
WHEN ALL:
- visitor is inside_rabbit_zone
- visitor perceives laughter
- laughter is_not from_visitors
REQUIRE IN ORDER:
- visitor tears map_dotted_section
- visitor holds map_fragment until visitor is outside_zoo
```

**AST：**

```text
Rule(E(all,
  E(state, visitor, inside_rabbit_zone),
  E(perceives, visitor, laughter),
  E(not, E(state, laughter, from_visitors))
), [REQUIRE F(sequence,
  F(tear, visitor, map_dotted_section),
  F(until, F(hold, visitor, map_fragment), E(state, visitor, outside_zoo))
)])
```

## 第 10 条

**原文语义：** 违规、迷路且与同伴走散时，在十五分钟内前往最近饮料店，向蓝衣售货员说明情况；若遇黑衣售货员则忽略其话语；随后跟随蓝衣员工经狮子园区员工通道，在假山后等待白狮吼叫，确认护送者仍穿蓝衣后离园。

**受控英文：**

```text
WHEN ALL:
- visitor has_violated visitor_rules
- visitor is lost
- visitor is separated_from_companions
REQUIRE IN ORDER:
- within 15 minutes, visitor goes_to nearest_drink_shop
- visitor tells blue_uniform_seller situation
- if visitor encounters black_uniform_seller, then visitor ignores black_uniform_seller_speech
- visitor follows blue_uniform_staff_to_lion_zone_service_passage
- visitor hides_behind artificial_hill
- wait until white_lions is roaring
- verify that escort is blue_uniform
- visitor leaves zoo
```

**AST：**

```text
Rule(E(all, E(violate, visitor, visitor_rules), E(state, visitor, lost), E(state, visitor, separated_from_companions)), [
  REQUIRE F(sequence,
    F(within, minutes_15, F(go_to, visitor, nearest_drink_shop)),
    F(tell, visitor, blue_uniform_seller, situation),
    F(when, E(encounter, visitor, black_uniform_seller), F(ignore, visitor, black_uniform_seller_speech)),
    F(follow, visitor, blue_uniform_staff_to_lion_zone_service_passage),
    F(hide_behind, visitor, artificial_hill),
    F(wait_until, E(state, white_lions, roaring)),
    F(verify, E(state, escort, blue_uniform)),
    F(leave, visitor, zoo)
  )
])
```

## 第 11 条

**原文语义：** 看见超过四头且正在吼叫的白狮时，在数量恢复到四头前不得离开；应报告数量，等待数量为四后再离开。

**受控英文：**

```text
WHEN ALL:
- visitor perceives white_lions
- white_lion count_is_greater_than 4
- white_lions is roaring
FORBID:
- visitor leaves lion_zone before white_lion_count is 4
REQUIRE IN ORDER:
- visitor reports_to staff white_lion_count
- wait until white_lion_count is 4
- visitor leaves lion_zone
```

**AST：**

```text
Rule(E(all, E(perceives, visitor, white_lions), E(count_gt, white_lion, 4), E(state, white_lions, roaring)), [
  FORBID F(before, F(leave, visitor, lion_zone), E(state, white_lion_count, 4)),
  REQUIRE F(sequence,
    F(report_to, visitor, staff, white_lion_count),
    F(wait_until, E(state, white_lion_count, 4)),
    F(leave, visitor, lion_zone)
  )
])
```

## 第 12 条

**原文语义：** 十二岁以下儿童可购买任意动物玩具；十三至十七岁者取得兔子玩具后须在一个月内丢弃；十八岁及以上者不得购买兔子玩具。

**受控英文：**

```text
WHEN ALWAYS:
PERMIT:
- if child is age_12_or_younger, then child purchases any_animal_toy
REQUIRE:
- if child is age_13_to_17, then within 1 month, child discards rabbit_toy
FORBID:
- if person is age_18_or_older, then person purchases rabbit_toy
```

**AST：**

```text
Rule(E(true), [
  PERMIT F(when, E(state, child, age_12_or_younger), F(purchase, child, any_animal_toy)),
  REQUIRE F(when, E(state, child, age_13_to_17), F(within, month_1, F(discard, child, rabbit_toy))),
  FORBID F(when, E(state, person, age_18_or_older), F(purchase, person, rabbit_toy))
])
```

## 第 13 条

**原文语义：** 看见戴兔耳饰的人进入大象园区时，应放弃参观大象园区。

**受控英文：**

```text
WHEN ALL:
- visitor perceives rabbit_eared_person
- rabbit_eared_person enters elephant_zone
REQUIRE:
- visitor abandons elephant_zone_visit
```

**AST：**

```text
Rule(E(all, E(perceives, visitor, rabbit_eared_person), E(enter, rabbit_eared_person, elephant_zone)), [
  REQUIRE F(abandon, visitor, elephant_zone_visit)
])
```

## 第 14 条

**原文语义：** 陷入无法解决且无法获得帮助的危险时，应不择手段前往狮子园区。

**受控英文：**

```text
WHEN ALL:
- visitor is in_unsolvable_danger
- visitor is unable_to_get_help
REQUIRE:
- visitor goes_to lion_zone by_any_means
```

**AST：**

```text
Rule(E(all, E(state, visitor, in_unsolvable_danger), E(state, visitor, unable_to_get_help)), [
  REQUIRE F(go_to, visitor, lion_zone, by_any_means)
])
```

## 第 15 条

**原文语义：** 同伴多次走散后又反复劝说游客前往海洋馆或大象园区时，不得信任该同伴。

**受控英文：**

```text
WHEN ALL:
- companion has_separated_from visitor
- separation_count count_is_greater_than 1
- companion urges visitor_to_visit_aquarium_or_elephant_zone repeatedly
FORBID:
- visitor trusts companion
```

**AST：**

```text
Rule(E(all,
  E(separated_from, companion, visitor),
  E(count_gt, separation_count, 1),
  E(urge, companion, visitor_to_visit_aquarium_or_elephant_zone, repeatedly)
), [FORBID F(trust, visitor, companion)])
```

## 测试结论

`RuleParser.parse_rules` 返回长度为 15 的 `tuple[Rule, ...]`。第 9—12 条的持续条件、期限、条件动作、先后约束、等待条件和嵌套期限均由独立递归节点表示，没有压入整句专用词。
