"""动物园怪谈全部角色的生成、Soul 绑定、认知规则与行动树。

涵盖游客（成年、儿童、迷路者、幸存者）、蓝衣员工（售货员、巡逻员、狮子园看守）、
大象区保安、红衣夜班员工、黑衣迷失者、园长以及超自然实体“它”。
"""

from __future__ import annotations

from ..core.entity_system import Entity
from ..core.soul import (
    Character,
    CognitiveStage,
    Disposition,
    Effect,
    F,
    Intent,
    ResponseMode,
    Soul,
)


def build_zoo_actors(scenes: dict[str, Entity], entities: dict[str, Entity], pubs: dict[str, Entity]) -> dict[str, Character]:
    """生成怪谈文本涉及的全部角色，并初始化其 Soul、认知态度与行为决策树。"""

    actors: dict[str, Character] = {}

    # -------------------------------------------------------------
    # 1. 游客群体
    # -------------------------------------------------------------
    # (1) 成年游客：周明（理性求生，严格遵守游客守则）
    char_adult = Character("成年游客_周明", scenes["entrance"], {"Visitor": 1, "Adult": 1})
    char_adult.properties.update(sanity=100, is_human=True, has_dotted_line=True, held_items=["zoo_map"])
    soul_adult = Soul("周明_Soul")
    char_adult.bind(soul_adult)
    # 采信《动物园园区游客守则》
    soul_adult.consider(pubs["visitor"].get("rule"), pubs["visitor"].get("issuer"), confidence=5)
    # 人格倾向：谨慎求生
    soul_adult.attach("Disposition", Disposition("谨慎求生", soul_adult, tags={"Cautious": 2}))

    def step_adult(soul: Soul, actor: Entity, env, context):
        current_scene = actor.parent
        # 1. 若在兔子区发现树荫或出逃兔子，优先离开并报告
        if current_scene is scenes["rabbit_enclosure"]:
            def act_leave_rabbit():
                actor.reparent(scenes["drink_shop"])
                actor.properties["sanity"] = max(0, actor.properties["sanity"] - 5)
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("leave_rabbit_zone"),
                motion_cost=15,
                resolve=act_leave_rabbit,
                label="周明发现兔子异样，迅速撤离兔子园区前往饮料店",
                response_mode=ResponseMode.AVOID,
            )
        # 2. 正常游览至大象区，严格核对标示牌
        if current_scene is scenes["drink_shop"]:
            def act_visit_elephant():
                actor.reparent(scenes["elephant_enclosure"])
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("visit_elephant_zone"),
                motion_cost=20,
                resolve=act_visit_elephant,
                label="周明依游览路线前往大象园区",
                response_mode=ResponseMode.VERIFY,
            )
        # 3. 在大象区发现异常，依守则进入安保室寻求庇护与信息核验
        if current_scene is scenes["elephant_enclosure"]:
            def act_seek_security():
                actor.reparent(scenes["security_office"])
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("seek_security_shelter"),
                motion_cost=10,
                resolve=act_seek_security,
                label="周明在大象园区保持高度警惕，进入保安室与老孙核验规则便签",
                response_mode=ResponseMode.PROTECT,
            )
        # 4. 安保室交流后，前往狮子园区寻求白狮子庇护
        if current_scene is scenes["security_office"]:
            def act_go_lion():
                actor.reparent(scenes["lion_enclosure"])
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("flee_to_lion_zone"),
                motion_cost=20,
                resolve=act_go_lion,
                label="周明遵照便签与守则第14条建议，径直前往最安全的狮子园区",
                response_mode=ResponseMode.PROTECT,
            )
        return None

    soul_adult.install_step(step_adult, priority=10)
    actors["visitor_adult"] = char_adult

    # (2) 儿童游客：小宇（10岁，拥有童真，持有兔子玩偶，认知不易被它直接污染）
    char_child = Character("儿童游客_小宇", scenes["entrance"], {"Visitor": 1, "Child": 1})
    char_child.properties.update(sanity=100, is_human=True, age=10, is_child=True, held_items=["rabbit_toy"])
    soul_child = Soul("小宇_Soul")
    char_child.bind(soul_child)
    soul_child.consider(pubs["visitor"].get("rule"), pubs["visitor"].get("issuer"), confidence=3)
    soul_child.attach("Disposition", Disposition("天真好奇", soul_child, tags={"Innocence": 3}))

    def step_child(soul: Soul, actor: Entity, env, context):
        if actor.parent is scenes["entrance"]:
            def act_enter():
                actor.reparent(scenes["drink_shop"])
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("child_explore"),
                motion_cost=15,
                resolve=act_enter,
                label="小宇抱着兔子玩偶兴高采烈地随人群进入饮料店广场",
                response_mode=ResponseMode.COOPERATE,
            )
        return None

    soul_child.install_step(step_child, priority=10)
    actors["visitor_child"] = char_child

    # (3) 迷失恐慌游客：林澜（听见笑声后走散，依守则撕下虚线，寻找蓝衣售货员）
    char_lost = Character("恐慌游客_林澜", scenes["rabbit_enclosure"], {"Visitor": 1, "Lost": 1})
    char_lost.properties.update(sanity=65, is_human=True, has_dotted_line=True, held_items=["torn_dotted_line"])
    soul_lost = Soul("林澜_Soul")
    char_lost.bind(soul_lost)
    soul_lost.consider(pubs["visitor"].get("rule"), pubs["visitor"].get("issuer"), confidence=5)
    soul_lost.attach("Disposition", Disposition("惊慌自救", soul_lost, tags={"Fear": 3}))

    def step_lost(soul: Soul, actor: Entity, env, context):
        # 听到笑声并走散，遵照守则第10条：15分钟内前往离自己最近的饮料店寻找穿蓝色工作服的员工
        if actor.parent is scenes["rabbit_enclosure"]:
            def act_flee_to_drinkshop():
                actor.reparent(scenes["drink_shop"])
                actor.tags["SeekingHelp"] = 1
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("seek_blue_staff_to_lion_rock"),
                motion_cost=15,
                resolve=act_flee_to_drinkshop,
                label="林澜紧握撕下的地图虚线，惊恐中狂奔至饮料店求助蓝衣售货员",
                response_mode=ResponseMode.PROTECT,
            )
        return None

    soul_lost.install_step(step_lost, priority=15)
    actors["visitor_lost"] = char_lost

    # (4) 幸存者：老魏（曾进入海洋馆客房，记录并留下血泪纸条，坚守人类两只眼睛与不换衣认知）
    char_survivor = Character("幸存者_老魏", scenes["guest_room"], {"Visitor": 1, "Survivor": 1})
    char_survivor.properties.update(sanity=55, is_human=True, has_dotted_line=False, held_items=["notes"])
    soul_survivor = Soul("老魏_Soul")
    char_survivor.bind(soul_survivor)
    soul_survivor.consider(pubs["abandoned"].get("rule"), pubs["abandoned"].get("issuer"), confidence=5)
    soul_survivor.attach("Disposition", Disposition("求生意志", soul_survivor, tags={"Stealth": 3}))

    def step_survivor(soul: Soul, actor: Entity, env, context):
        # 幸存者在客房内保持密闭休息，等待白狮子吼叫信号或安全时机
        if actor.parent is scenes["guest_room"]:
            def act_stay_hidden():
                actor.properties["sanity"] = min(100, actor.properties["sanity"] + 5)
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("rest_in_dark_closed_room"),
                motion_cost=25,
                resolve=act_stay_hidden,
                label="老魏在黑暗客房内关闭水母灯，默念人类面部特征，静待生机",
                response_mode=ResponseMode.CONCEAL,
            )
        return None

    soul_survivor.install_step(step_survivor, priority=10)
    actors["visitor_survivor"] = char_survivor

    # -------------------------------------------------------------
    # 2. 动物园蓝衣员工群体
    # -------------------------------------------------------------
    # (1) 饮料店售货员：苏静（守则4查兔子血，守则10引渡迷路游客至狮子假山）
    char_shop = Character("蓝衣售货员_苏静", scenes["drink_shop"], {"Staff": 1, "BlueUniform": 1})
    char_shop.properties.update(sanity=95, is_human=True, uniform_color="blue", uniform_color_anchored=True)
    soul_shop = Soul("苏静_Soul")
    char_shop.bind(soul_shop)
    soul_shop.consider(pubs["blue_staff"].get("rule"), pubs["blue_staff"].get("issuer"), confidence=5)
    soul_shop.attach("Disposition", Disposition("尽职尽责", soul_shop, tags={"Duty": 3}))

    def step_shop(soul: Soul, actor: Entity, env, context):
        # 1. 若现场有求助游客，依守则第10条护送至狮子园区假山后
        lost_visitor = next((c for c in scenes["drink_shop"].children if c.has("SeekingHelp")), None)
        if lost_visitor:
            def act_escort():
                lost_visitor.reparent(scenes["lion_rock"])
                lost_visitor.tags.pop("SeekingHelp", None)
                lost_visitor.tags["ShelteredInRock"] = 1
                lost_visitor.properties["sanity"] = min(100, lost_visitor.properties["sanity"] + 20)
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("escort_lost_visitor_to_lion_zone"),
                motion_cost=20,
                resolve=act_escort,
                label=f"苏静核实工作服为蓝色，立刻带领求助游客{lost_visitor.name}前往狮子园区假山安全通道",
                response_mode=ResponseMode.PROTECT,
            )
        # 2. 每小时例行检查货架，收存凭空出现的“兔子血”
        def act_check_shelf():
            entities["rabbit_blood"].reparent(scenes["director_office"])
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("store_rabbit_blood"),
            motion_cost=15,
            resolve=act_check_shelf,
            label="苏静按时巡查饮料店货架，将异常出现的“兔子血”封存收纳",
            response_mode=ResponseMode.VERIFY,
        )

    soul_shop.install_step(step_shop, priority=12)
    actors["staff_drink_shop"] = char_shop

    # (2) 巡逻员：赵刚（负责修剪兔子区灌木、疏散猿类区双街、处理出逃兔子）
    char_patrol = Character("蓝衣巡视员_赵刚", scenes["ape_enclosure"], {"Staff": 1, "BlueUniform": 1})
    char_patrol.properties.update(sanity=90, is_human=True, uniform_color="blue", uniform_color_anchored=True)
    soul_patrol = Soul("赵刚_Soul")
    char_patrol.bind(soul_patrol)
    soul_patrol.consider(pubs["blue_staff"].get("rule"), pubs["blue_staff"].get("issuer"), confidence=5)
    soul_patrol.attach("Disposition", Disposition("果断勇敢", soul_patrol, tags={"Brave": 2}))

    def step_patrol(soul: Soul, actor: Entity, env, context):
        current_scene = actor.parent
        # 1. 猿类区双街异常时，引导游客往左街并封锁10分钟
        if current_scene is scenes["ape_enclosure"]:
            def act_evacuate_ape():
                actor.reparent(scenes["rabbit_enclosure"])
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("evacuate_ape_left_and_lock"),
                motion_cost=25,
                resolve=act_evacuate_ape,
                label="赵刚确认猿类街区异象，引导游客沿左街撤离并完成园区临时封锁",
                response_mode=ResponseMode.RESTRAIN,
            )
        # 2. 兔子区修剪灌木以防止阴影积聚
        if current_scene is scenes["rabbit_enclosure"]:
            def act_trim():
                scenes["rabbit_enclosure"].tags.pop("Shaded", None)
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("trim_rabbit_bushes"),
                motion_cost=20,
                resolve=act_trim,
                label="赵刚修剪兔子园区灌木，消除可能滋生“它”的阴影遮蔽",
                response_mode=ResponseMode.VERIFY,
            )
        return None

    soul_patrol.install_step(step_patrol, priority=10)
    actors["staff_patrol"] = char_patrol

    # (3) 狮子园区看守：陈锋（负责确认4头白狮子，守护假山通道，听吼叫后送客）
    char_lion_keeper = Character("狮子园守门员_陈锋", scenes["lion_enclosure"], {"Staff": 1, "BlueUniform": 1})
    char_lion_keeper.properties.update(sanity=95, is_human=True, uniform_color="blue", uniform_color_anchored=True)
    soul_lion_keeper = Soul("陈锋_Soul")
    char_lion_keeper.bind(soul_lion_keeper)
    soul_lion_keeper.consider(pubs["blue_staff"].get("rule"), pubs["blue_staff"].get("issuer"), confidence=5)
    soul_lion_keeper.attach("Disposition", Disposition("沉稳忠诚", soul_lion_keeper, tags={"Loyal": 3}))

    def step_lion_keeper(soul: Soul, actor: Entity, env, context):
        # 检查假山内是否有避难游客
        sheltered = next((c for c in scenes["lion_rock"].children if c.has("ShelteredInRock")), None)
        if sheltered:
            def act_protect_and_release():
                # 狮子吼叫净化
                sheltered.tags.pop("ShelteredInRock", None)
                sheltered.reparent(scenes["entrance"])
                sheltered.tags["PurifiedAndSafe"] = 1
                sheltered.properties["sanity"] = 100
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("escort_purified_visitor_to_exit"),
                motion_cost=25,
                resolve=act_protect_and_release,
                label=f"陈锋守卫假山通道，确认4头白狮子吼叫净化完毕，将{sheltered.name}安全送达动物园正门离园",
                response_mode=ResponseMode.PROTECT,
            )
        # 例行友善对待4头白狮子
        def act_care_lions():
            pass
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("treat_white_lions_kindly"),
            motion_cost=15,
            resolve=act_care_lions,
            label="陈锋在狮子园区巡视，友善照看四头白狮子，维持守护气场",
            response_mode=ResponseMode.COOPERATE,
        )

    soul_lion_keeper.install_step(step_lion_keeper, priority=12)
    actors["staff_lion_keeper"] = char_lion_keeper

    # -------------------------------------------------------------
    # 3. 大象园区保安：老孙（坚守制服颜色认知，相信人类，留存便签，1:00交班）
    # -------------------------------------------------------------
    char_security = Character("大象区保安_老孙", scenes["security_office"], {"Security": 1, "UniformAnchored": 1})
    char_security.properties.update(sanity=92, is_human=True, uniform_color="blue", uniform_color_anchored=True)
    soul_security = Soul("老孙_Soul")
    char_security.bind(soul_security)
    soul_security.consider(pubs["security"].get("rule"), pubs["security"].get("issuer"), confidence=5)
    soul_security.attach("Disposition", Disposition("坚定信念", soul_security, tags={"Faith": 3}))

    def step_security(soul: Soul, actor: Entity, env, context):
        # 1. 牢记衣服颜色，进行自我认知强化
        def act_anchor():
            actor.properties["uniform_color_anchored"] = True
            actor.properties["sanity"] = min(100, actor.properties["sanity"] + 5)
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("keep_uniform_color_cognition"),
            motion_cost=15,
            resolve=act_anchor,
            label="老孙在安保室重温便签条，坚定默念‘我的工作服是蓝色的，我相信人类’",
            response_mode=ResponseMode.VERIFY,
        )

    soul_security.install_step(step_security, priority=10)
    actors["security_guard"] = char_security

    # -------------------------------------------------------------
    # 4. 海洋馆红衣员工：夏晚（12:30~6:00夜班，检查水母小夜灯，清理山羊肉，驱赶黑衣人）
    # -------------------------------------------------------------
    char_red = Character("红衣管理员_夏晚", scenes["jellyfish_zone"], {"Staff": 1, "RedUniform": 1})
    char_red.properties.update(sanity=88, is_human=True, uniform_color="red", uniform_color_anchored=True)
    soul_red = Soul("夏晚_Soul")
    char_red.bind(soul_red)
    soul_red.consider(pubs["red_staff"].get("rule"), pubs["red_staff"].get("issuer"), confidence=5)
    soul_red.attach("Disposition", Disposition("严谨戒备", soul_red, tags={"Alert": 3}))

    def step_red(soul: Soul, actor: Entity, env, context):
        # 1. 若客房水母小夜灯亮着且超过指定时间，强制熄灯
        lamp = entities["jellyfish_lamp"]
        if lamp.get("turned_on"):
            def act_turn_off_lamp():
                lamp.set("turned_on", False)
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("force_turn_off_lamp_by_0115"),
                motion_cost=15,
                resolve=act_turn_off_lamp,
                label="夏晚巡查水母区客房，依照守则在凌晨1:15前强行关闭水母小夜灯，阻断‘它’的窥视",
                response_mode=ResponseMode.RESTRAIN,
            )
        # 2. 检查是否有穿黑衣者借宿，若有则使用电击棒驱赶
        black_guest = next((c for c in scenes["guest_room"].children if c.has("BlackUniform")), None)
        if black_guest:
            def act_repel_black():
                black_guest.reparent(scenes["aquarium_gate"])
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("repel_black_coat_guests"),
                motion_cost=20,
                resolve=act_repel_black,
                label=f"夏晚发现黑衣人员{black_guest.name}试图留宿，依守则手持防卫器械强硬将其驱离至门廊",
                response_mode=ResponseMode.RESTRAIN,
            )
        # 3. 例行处理陌生“山羊肉”至鲸鱼区收纳箱
        def act_store_meat():
            entities["goat_meat"].reparent(scenes["whale_zone"])
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("store_goat_meat_in_whale_box"),
            motion_cost=15,
            resolve=act_store_meat,
            label="夏晚将货架出现的陌生‘山羊肉’投入鲸鱼区密闭收纳箱妥善隔离",
            response_mode=ResponseMode.VERIFY,
        )

    soul_red.install_step(step_red, priority=12)
    actors["staff_red"] = char_red

    # -------------------------------------------------------------
    # 5. 黑衣迷失者：陆离（误入表里夹缝穿上黑制服，认知拉扯）
    # -------------------------------------------------------------
    char_black = Character("黑衣迷航者_陆离", scenes["aquarium_gate"], {"BlackUniform": 1, "Confused": 1})
    char_black.properties.update(sanity=50, is_human=True, uniform_color="black")
    soul_black = Soul("陆离_Soul")
    char_black.bind(soul_black)
    # 接收了海洋馆门口告示与残缺纸条
    soul_black.consider(pubs["aquarium_gate"].get("rule"), pubs["aquarium_gate"].get("issuer"), confidence=4)
    soul_black.attach("Disposition", Disposition("彷徨徘徊", soul_black, tags={"Confused": 2}))

    def step_black(soul: Soul, actor: Entity, env, context):
        # 黑衣人在门口徘徊，试图寻求理解或充当导游
        def act_wander():
            actor.properties["sanity"] = max(20, actor.properties["sanity"] - 2)
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("wander_in_aquarium_threshold"),
            motion_cost=20,
            resolve=act_wander,
            label="陆离身着黑衣在海洋馆入口处徘徊，在表里规则矛盾中艰难维系仅存的人性理智",
            response_mode=ResponseMode.NEGOTIATE,
        )

    soul_black.install_step(step_black, priority=8)
    actors["staff_black"] = char_black

    # -------------------------------------------------------------
    # 6. 动物园园长：顾明远（统领全局，常备虚线地图，每三天查狮子，断电撕纸求援）
    # -------------------------------------------------------------
    char_director = Character("动物园园长_顾明远", scenes["director_office"], {"Director": 1, "Leader": 1})
    char_director.properties.update(sanity=98, is_human=True, has_dotted_line=True, held_items=["zoo_maps_x3", "table_it"])
    soul_director = Soul("顾明远_Soul")
    char_director.bind(soul_director)
    soul_director.consider(pubs["director"].get("rule"), pubs["director"].get("issuer"), confidence=5)
    soul_director.attach("Disposition", Disposition("领袖担当", soul_director, tags={"Sacrifice": 3, "Leadership": 3}))

    def step_director(soul: Soul, actor: Entity, env, context):
        # 1. 若办公室断电，立刻撕下右手边虚线出门，要求遇见的第一个员工处理
        if scenes["director_office"].has("Blackout"):
            def act_handle_blackout():
                scenes["director_office"].tags.pop("Blackout", None)
            return Intent(
                actor=actor,
                mind=soul,
                effect=F("office_lights_and_blackout_protocol"),
                motion_cost=25,
                resolve=act_handle_blackout,
                label="顾明远遇办公室突发停电，立即撕下桌面备用虚线握紧出门，号令现场员工抢修恢复供电",
                response_mode=ResponseMode.COOPERATE,
            )
        # 2. 每三天检查白狮子数量并登记至表格《它》
        def act_record_lions():
            table = entities["table_it"]
            table.properties["lion_records"].append(4)
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("record_white_lions_in_it_table"),
            motion_cost=20,
            resolve=act_record_lions,
            label="顾明远例行检视白狮子监控与数量，在加密文件《它》中冷静记录‘白狮子数量：4’",
            response_mode=ResponseMode.VERIFY,
        )

    soul_director.install_step(step_director, priority=15)
    actors["director"] = char_director

    # -------------------------------------------------------------
    # 7. 超自然实体：“它”（不可名状的阴影存在）
    # -------------------------------------------------------------
    char_it = Character("“它”（认知侵蚀者）", scenes["rabbit_enclosure"], {"EntityIT": 1, "Anomaly": 1})
    char_it.properties.update(power=100)
    soul_it = Soul("它_Soul")
    char_it.bind(soul_it)
    soul_it.attach("Disposition", Disposition("阴影侵蚀", soul_it, tags={"Predator": 3}))

    def step_it(soul: Soul, actor: Entity, env, context):
        # “它”搜索处于无保护状态的成年人类实施认知侵蚀；对儿童无认知抓手
        target = None
        for scene in scenes.values():
            for child in scene.children:
                if (
                    child.has("Visitor")
                    and not child.has("Child")
                    and child.get("is_human")
                ):
                    target = child
                    break
            if target:
                break

        if target:
            # 如果目标持有撕下的虚线纸条，客观规则补丁 Patch_DottedLineProtection 将发挥硬约束！
            has_line = target.get("has_dotted_line", False)

            if has_line:
                label = f"“它”企图侵蚀{target.name}，但其握持的地图虚线绽放白光将阴影弹开，“它”受挫发出尖锐哭声！"
                def act_corrupt():
                    # 虚线阻断侵蚀，SAN 保持完好
                    pass
            else:
                label = f"“它”自树荫暗处伸展无形触角，对无防护的{target.name}释放认知干扰！"
                def act_corrupt():
                    target.properties["sanity"] = max(0, target.properties["sanity"] - 30)
                    target.tags["Corrupted"] = 1

            return Intent(
                actor=actor,
                mind=soul,
                effect=F("inflict_cognition_corruption"),
                motion_cost=30,
                resolve=act_corrupt,
                label=label,
                response_mode=ResponseMode.RESTRAIN,
            )

        # 否则在阴影与光影交界处巡弋
        def act_lurk():
            pass
        return Intent(
            actor=actor,
            mind=soul,
            effect=F("lurk_in_shadows"),
            motion_cost=25,
            resolve=act_lurk,
            label="“它”在园区树荫与夜间光影交界处隐秘盘旋，窥伺认知的裂痕",
            response_mode=ResponseMode.AVOID,
        )

    soul_it.install_step(step_it, priority=5)
    actors["the_entity"] = char_it

    return actors
