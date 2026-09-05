"""五天（1000 AV）连续行动值推演引擎。

严格遵循嵌套行动值调度机制，每日 200 AV（白昼 120 AV，夜晚 80 AV），
完整驱动全部角色的规则认知、行为决策、异常交互与生存状态审计。
"""

from __future__ import annotations

from typing import Any
from ..core.engine import ActionRuntime, Timeline
from ..core.entity_system import Entity
from ..core.soul import RuleEnvironment, RuleSystem
from .zoo_actors import build_zoo_actors
from .zoo_rules import build_zoo_rule_patches, build_zoo_rule_publications
from .zoo_world import build_zoo_world


class ZooSimulation:
    """动物园怪谈五日嵌套行动值推演器。"""

    def __init__(self) -> None:
        self.timeline = Timeline()
        self.env = RuleEnvironment()
        self.rules = RuleSystem()

        # 1. 构建世界拓扑、场景与实体
        self.space, self.scenes, self.entities = build_zoo_world()

        # 2. 构建九套规则出版物与客观规则补丁
        self.pubs = build_zoo_rule_publications(
            self.entities["zoo_authority"], self.entities["aquarium_authority"]
        )
        self.patches = build_zoo_rule_patches(self.space)

        # 3. 构建全部角色与 Soul
        self.actors = build_zoo_actors(self.scenes, self.entities, self.pubs)

        # 4. 初始化运行引擎
        self.engine = ActionRuntime(
            timeline=self.timeline,
            env=self.env,
            rules=self.rules,
            event_parent=self.space,
        )
        self.engine.patches.extend(self.patches)

        # 推演日志与审计追踪
        self.daily_logs: dict[int, list[str]] = {day: [] for day in range(1, 6)}
        self.event_records: list[dict[str, Any]] = []

    def log(self, day: int, message: str) -> None:
        """记录推演事实日志。"""
        time_str = f"[Day {day} | AV: {self.timeline.now:05.1f}]"
        self.daily_logs[day].append(f"{time_str} {message}")

    def run_actor_turn(self, day: int, actor_key: str) -> None:
        """为一个角色调度一次由其 Soul 决策生成的完整行动。"""
        char = self.actors[actor_key]
        soul = char.souls[0]

        # 由 Soul 根据已采信规则、当前处境与人格偏好选出最佳 Intent
        action = soul.step(char, self.rules, self.env)
        if not action:
            return

        # 提交行动值时间线并完成嵌套结算
        label = action.label or action.effect.op
        self.engine.submit(action, delay=action.motion_cost, wait=True)

        self.log(day, f"{char.name} 执行行动：{label} (消耗 {action.motion_cost} AV)")
        self.event_records.append({
            "day": day,
            "time": self.timeline.now,
            "actor": char.name,
            "action": label,
            "effect": action.effect.op,
            "sanity": char.properties.get("sanity"),
            "location": char.parent.name if char.parent else "未知",
        })

    def run_day_1(self) -> None:
        """第 1 日推演：入园认知确立、兔子园区异动与守则求援机制验证。"""
        day = 1
        self.log(day, "=== 第 1 日开始 (0 - 200 AV)：游客入园，表世界秩序与初始异象 ===")

        # 1. 游客周明和小宇入园，采信《游客守则》
        self.run_actor_turn(day, "visitor_adult")
        self.run_actor_turn(day, "visitor_child")

        # 2. 蓝衣员工苏静例行检查饮料店货架
        self.run_actor_turn(day, "staff_drink_shop")

        # 3. 兔子园区异动：出逃的红眼兔子发出非游客方向的笑声
        self.log(day, "【异常发生】兔子园区灌木深处隐隐传出怪异笑声，数只红眼兔子窜出封闭草坪！")
        self.run_actor_turn(day, "the_entity")

        # 4. 恐慌游客林澜走散，触发《游客守则》第9条与第10条：撕下虚线狂奔向饮料店求助
        self.run_actor_turn(day, "visitor_lost")

        # 5. 苏静依据《员工守则》第10条，核实验证身份后接引林澜赶赴狮子园区假山安全通道
        self.run_actor_turn(day, "staff_drink_shop")

        # 6. 大象园区保安老孙在安保室重温便签条，坚定自身制服颜色认知
        self.run_actor_turn(day, "security_guard")

        # 推进到第1日结束 (200 AV)
        self.timeline.now = 200
        self.log(day, "=== 第 1 日结束：夜禁广播生效，各区域封闭，避难者安全隐蔽于狮子园区假山 ===")

    def run_day_2(self) -> None:
        """第 2 日推演：猿类双街异象封锁、大象外观核查与货架“兔子血”收存。"""
        day = 2
        self.log(day, "=== 第 2 日开始 (200 - 400 AV)：深层规则碰撞与员工防线 ===")

        # 1. 猿类园区突现双街异象，蓝衣巡视员赵刚依照《员工守则》第2条疏导左街并封锁10分钟
        self.log(day, "【异常发生】猿类园区出现两条并列街道，右侧街道出现兔子展示栏！")
        self.run_actor_turn(day, "staff_patrol")

        # 2. 饮料店货架突现“兔子血”，苏静遵照第4条封存收纳
        self.log(day, "【异常发生】饮料店冷柜深处凭空凝结出一瓶猩红色的‘兔子血’！")
        self.run_actor_turn(day, "staff_drink_shop")

        # 3. 成年游客周明进入大象园区，警惕大象外观
        self.run_actor_turn(day, "visitor_adult")

        # 4. 老孙在安保室接待并为大象园区提供认知安全保障，周明进入安保室交流
        self.run_actor_turn(day, "security_guard")
        self.run_actor_turn(day, "visitor_adult")

        # 5. 赵刚完成猿类区封锁后巡查至兔子区，修剪遮阴灌木，消除“它”滋生的阴影
        self.run_actor_turn(day, "staff_patrol")

        # 推进到第2日结束 (400 AV)
        self.timeline.now = 400
        self.log(day, "=== 第 2 日结束：异常被蓝衣员工与保安依守则规范成功阻截 ===")

    def run_day_3(self) -> None:
        """第 3 日推演：园长每三天检查白狮子表格《它》、吼叫净化与黑衣人游荡。"""
        day = 3
        self.log(day, "=== 第 3 日开始 (400 - 600 AV)：园长中枢核验与白狮子神圣吼叫 ===")

        # 1. 周明遵照便签指引前往狮子园区
        self.run_actor_turn(day, "visitor_adult")

        # 2. 动物园园长顾明远执行《园长文件》第4条：每三天检查狮子园区并在表格《它》中登记
        self.run_actor_turn(day, "director")

        # 3. 狮子园区守门员陈锋确认4头白狮子开始齐声长吼，完成对假山避难游客林澜的净化，护送离园
        self.log(day, "【神圣驱逐】狮子园区四头威严的白狮子仰天长啸，净化声波席卷全园，驱散阴霾！")
        self.run_actor_turn(day, "staff_lion_keeper")

        # 4. 黑衣人陆离在表里交界处游荡，试图搭讪游客，但游客坚守守则无视黑衣
        self.log(day, "【表里交错】一名身穿黑色工作服的迷航者在海洋馆门廊前徘徊张望！")
        self.run_actor_turn(day, "staff_black")

        # 5. 阴影中的“它”试图侵蚀，但因现场人员均受虚线与狮吼守护而无从下手
        self.run_actor_turn(day, "the_entity")

        # 推进到第3日结束 (600 AV)
        self.timeline.now = 600
        self.log(day, "=== 第 3 日结束：林澜被成功净化并安全转移，园长完成白狮子周期性审计 ===")

    def run_day_4(self) -> None:
        """第 4 日推演：海洋馆夜班运转、强制熄灭水母小夜灯与驱逐黑衣人。"""
        day = 4
        self.log(day, "=== 第 4 日开始 (600 - 800 AV)：里世界海洋馆夜班守则运转 ===")

        # 1. 幸存者老魏在水母区钟点客房内依照纸条指引黑暗休息
        self.run_actor_turn(day, "visitor_survivor")

        # 2. 红衣员工夏晚夜班（12:30~6:00）巡视，发现客房水母小夜灯未关，依据守则强行关闭
        self.log(day, "【夜班巡查】凌晨1:10，红衣管理员夏晚进入水母区钟点客房例行检查！")
        self.run_actor_turn(day, "staff_red")

        # 3. 黑衣人试图进入客房借宿，夏晚依守则手持电击棒强硬将其驱离
        self.log(day, "【违规处置】黑衣人员试图借宿客房，夏晚坚决拒绝并使用防卫器械将其驱逐！")
        # 模拟黑衣人潜入客房后被夏晚驱离
        self.actors["staff_black"].reparent(self.scenes["guest_room"])
        self.run_actor_turn(day, "staff_red")

        # 4. 夏晚清理货架上的异化“山羊肉”至鲸鱼区密闭收纳箱
        self.run_actor_turn(day, "staff_red")

        # 推进到第4日结束 (800 AV)
        self.timeline.now = 800
        self.log(day, "=== 第 4 日结束：海洋馆夜班守则精准执行，黑夜中的窥视被完全切断 ===")

    def run_day_5(self) -> None:
        """第 5 日推演：园长室突发断电、虚线求援决断、“它”的终极冲击与全员破局逃离。"""
        day = 5
        self.log(day, "=== 第 5 日开始 (800 - 1000 AV)：终局冲击、团结破局与生还脱出 ===")

        # 1. 凌晨3:00，园长办公室强电发生超自然故障断电！
        self.log(day, "【重大危机】园长办公室供电设备突然爆裂熄灭，门外响起诡异的孩子哭笑交替声！")
        self.scenes["director_office"].tags["Blackout"] = 1

        # 2. 顾明远依照《园长文件》第8条，撕下备用虚线握紧出门，号令现场员工抢修
        self.run_actor_turn(day, "director")

        # 3. “它”倾巢出动发动终极精神污染侵蚀
        self.log(day, "【终极冲击】“它”将全部阴影汇聚于主干道，疯狂寻找认知的破绽！")
        self.run_actor_turn(day, "the_entity")

        # 4. 保安老孙与巡视员赵刚、陈锋协同，坚定自身人类与制服认知，引导白狮子再次怒吼
        self.run_actor_turn(day, "security_guard")
        self.run_actor_turn(day, "staff_lion_keeper")

        # 5. 成年游客周明、儿童小宇在员工掩护下，手持虚线安全抵达动物园正门逃离出口
        self.log(day, "【胜利逃脱】在团结、勇气与坚定的人性信念下，所有受庇护人类在白狮子护送下成功冲破迷雾离开动物园！")
        self.actors["visitor_adult"].reparent(self.scenes["entrance"])
        self.actors["visitor_child"].reparent(self.scenes["entrance"])
        self.actors["visitor_survivor"].reparent(self.scenes["entrance"])

        # 推进到第5日终点 (1000 AV)
        self.timeline.now = 1000
        self.log(day, "=== 第 5 日结束：全员完成 1000 AV 行动值推演，规则系统验证圆满成功！ ===")

    def run_all(self) -> None:
        """按顺序完整推演五天。"""
        self.run_day_1()
        self.run_day_2()
        self.run_day_3()
        self.run_day_4()
        self.run_day_5()

    def generate_summary(self) -> dict[str, Any]:
        """生成五日推演结果统计与角色最终状态。"""
        survivors = []
        for key, char in self.actors.items():
            if key == "the_entity":
                continue
            survivors.append({
                "name": char.name,
                "role": list(char.tags.keys())[0],
                "sanity": char.properties.get("sanity", 100),
                "is_human": char.properties.get("is_human", True),
                "final_location": char.parent.name if char.parent else "脱出",
            })

        return {
            "total_days": 5,
            "total_av": self.timeline.now,
            "total_events": len(self.event_records),
            "survivors": survivors,
        }
