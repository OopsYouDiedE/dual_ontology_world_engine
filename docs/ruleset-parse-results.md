# Ruleset 完整解析结果

本报告由当前组合式 `RuleParser` 的实际输出整理。解析器源码不包含作品专名或完整语句匹配。

## 文学语料

### 《浮士德》

中文：如果浮士德完全满足，并要求当前这一刻停留，随后其人世生命结束，并归梅菲斯特支配。

```text
WORD SPIRIT:
ACTIVATION RULE:
WHEN ALL:
- Faust is completely_satisfied
REQUIRE:
- Faust requests present_moment_remain
END ACTIVATION RULE
ACTUAL EFFECTS IN ORDER:
1. Faust ends_life earthly
2. Faust binds_to Mephistopheles
```

```text
WordSpirit
├─ Rule: state(Faust, completely_satisfied)
│  └─ REQUIRE request(Faust, present_moment_remain)
└─ sequence
   ├─ end_life(Faust, earthly)
   └─ bind(Faust, Mephistopheles)
```

### 《威尼斯商人》

中文：安东尼奥逾期未还债时，夏洛克可取得恰好一磅肉，但不得使其流血，也不得取得不精确的数量。

```text
WHEN ALL:
- Antonio has_not completed repay_debt before deadline
PERMIT:
- Shylock takes flesh_from_Antonio exactly_1_pound
FORBID:
- Shylock sheds Antonio_blood
- Shylock takes_inexact_quantity flesh 1 pound
```

```text
Rule
├─ not(completed_before(completed, repay_debt, deadline))
├─ PERMIT take(Shylock, flesh_from_Antonio, exactly_1_pound)
└─ FORBID sequence
   ├─ shed(Shylock, Antonio_blood)
   └─ take_inexact_quantity(Shylock, flesh, 1, pound)
```

### 《格萝娅的咒歌》

中文：禁止束缚物捆住斯维普达格；发生束缚时，护咒破坏锁具并解除束缚。

```text
WORD SPIRIT:
ACTIVATION RULE:
WHEN ALWAYS:
FORBID:
- restraints bind Svipdag limbs
END ACTIVATION RULE
ACTUAL EFFECTS IN ORDER:
1. restraint_locks break Svipdag
2. restraints remove Svipdag limbs
```

```text
WordSpirit
├─ Rule: true → FORBID bind(restraints, Svipdag, limbs)
└─ sequence
   ├─ break(restraint_locks, Svipdag)
   └─ remove(restraints, Svipdag, limbs)
```

### 《蓝胡子》

中文：法蒂玛可进入蓝色房间以外的房间；不得进入蓝色房间或把小钥匙插入其锁孔。

```text
WHEN ALWAYS:
PERMIT:
- Fatima enters any_room except blue_chamber
FORBID:
- Fatima enters blue_chamber
- Fatima inserts small_key blue_chamber_lock
```

```text
Rule(true)
├─ PERMIT enter_except(Fatima, any_room, blue_chamber)
└─ FORBID sequence
   ├─ enter(Fatima, blue_chamber)
   └─ insert(Fatima, small_key, blue_chamber_lock)
```

### 狄多亡语

中文：狄多临死时要求永久敌视埃涅阿斯及其后裔，禁止和平与盟约，并呼唤未来复仇者。

```text
WORD SPIRIT:
ACTIVATION RULE:
WHEN ALL:
- Dido is dying
REQUIRE:
- Dido declare_hostility Aeneas_and_descendants permanent
END ACTIVATION RULE
ACTUAL EFFECTS IN ORDER:
1. hostility persists two_peoples
2. peace_and_alliance remain_forbidden two_peoples
3. future_avenger arises_from Dido_people
```

```text
WordSpirit
├─ Rule: state(Dido, dying)
│  └─ REQUIRE declare_hostility(Dido, Aeneas_and_descendants, permanent)
└─ sequence
   ├─ persist(hostility, two_peoples)
   ├─ remain_forbidden(peace_and_alliance, two_peoples)
   └─ arises_from(future_avenger, Dido_people)
```

### 《麦克白》仪式

中文：女巫依次加入材料、共同重复吟唱，再用狒狒血冷却混合物，完成后召唤幻影。

```text
WORD SPIRIT:
ACTIVATION RULE:
WHEN ALL:
- ritual_cauldron is boiling
REQUIRE IN ORDER:
1. witches add_in_order required_ingredients
2. all_witches repeat_after_each chant ingredient_group
3. second_witch cool_with mixture baboon_blood
END ACTIVATION RULE
ACTUAL EFFECTS IN ORDER:
1. completed_ritual summons apparitions
```

```text
WordSpirit
├─ Rule: state(ritual_cauldron, boiling)
│  └─ REQUIRE sequence
│     ├─ add_in_order(witches, required_ingredients)
│     ├─ repeat_after_each(all_witches, chant, ingredient_group)
│     └─ cool_with(second_witch, mixture, baboon_blood)
└─ summon(completed_ritual, apparitions)
```

### `geasa`

中文：主体不得执行行为 A，也不得拒绝义务 B；违反后保护崩溃。

```text
WORD SPIRIT:
ACTIVATION RULE:
WHEN ALL:
- subject is bound_by geasa
FORBID:
- subject performs forbidden_action_A
END ACTIVATION RULE
ACTIVATION RULE:
WHEN ALL:
- subject is bound_by geasa
FORBID:
- subject refuses obligation_B
END ACTIVATION RULE
ACTUAL EFFECTS IN ORDER:
1. subject_protection collapses
```

```text
WordSpirit
├─ bound_by(subject, geasa) → FORBID perform(subject, forbidden_action_A)
├─ bound_by(subject, geasa) → FORBID refuse(subject, obligation_B)
└─ collapse(subject_protection)
```

## 动物园语料

### 违规后的离开流程

```text
WHEN ALL:
- visitor has_violated visitor_rules
- visitor is separated_from companions
REQUIRE IN ORDER:
1. within 15 minutes, visitor goes_to nearest_drink_shop
2. visitor tells blue_uniform_seller situation
3. visitor leaves zoo
```

```text
all(violate(visitor, visitor_rules), separated_from(visitor, companions))
→ REQUIRE sequence(within(minutes_15, go_to(visitor, nearest_drink_shop)), tell(visitor, blue_uniform_seller, situation), leave(visitor, zoo))
```

### 白狮子数量异常

```text
WHEN ALL:
- visitor perceives white_lions_over_4_roaring
FORBID:
- visitor leaves lion_zone
REQUIRE IN ORDER:
1. visitor tells staff white_lion_count
2. wait until white_lion_count is 4
3. visitor leaves lion_zone
```

```text
perceives(visitor, white_lions_over_4_roaring)
├─ FORBID leave(visitor, lion_zone)
└─ REQUIRE sequence(tell(...), wait_until(state(white_lion_count, 4)), leave(...))
```

### 白狮子袭击后的流程

```text
WHEN ALL:
- staff is attacked_by normal_white_lion
- staff is_not died_immediately
FORBID:
- staff goes_to hospital immediately
REQUIRE IN ORDER:
1. staff leaves lion_zone
2. staff goes_to aquarium
```

```text
all(attacked_by(staff, normal_white_lion), not(state(staff, died_immediately)))
├─ FORBID go_to(staff, hospital, immediately)
└─ REQUIRE sequence(leave(staff, lion_zone), go_to(staff, aquarium))
```

## 验证

当前测试共 11 项，全部通过。报告中的 AST 使用紧凑树形表示；节点内容来自 `format_ast` 的实际解析结果。
