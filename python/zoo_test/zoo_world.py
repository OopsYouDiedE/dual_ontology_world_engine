"""动物园与海洋馆空间拓扑、场景设施、动物与道具实体。

遵循 WorldSpace 拓扑网络与 Entity 属性规范，不引入多余数据类。
"""

from __future__ import annotations

from ..core.engine import Passage, WorldSpace
from ..core.entity_system import Entity


def build_zoo_world() -> tuple[WorldSpace, dict[str, Entity], dict[str, Entity]]:
    """构建动物园与海洋馆的场景拓扑、动物群与核心道具。"""
    space = WorldSpace("某市动物园总拓扑")

    # 1. 机构权威实体（作为规则发布者）
    zoo_authority = Entity("动物园管理处", space, {"Authority": 3, "Guarantee": 2})
    aquarium_authority = Entity("海洋馆管理处", space, {"Authority": 2, "Guarantee": 2})

    # 2. 表世界动物园场景
    entrance = Entity("动物园正门与售票处", space, {"Scene": 1, "Public": 1})
    drink_shop = Entity("园区饮料店", space, {"Scene": 1, "Public": 1, "Shop": 1})
    ape_enclosure = Entity("猿类园区", space, {"Scene": 1, "Animals": 1})
    rabbit_enclosure = Entity("兔子园区", space, {"Scene": 1, "Animals": 1, "Shaded": 1})
    elephant_enclosure = Entity("大象园区", space, {"Scene": 1, "Animals": 1})
    security_office = Entity("大象园区安保室", space, {"Scene": 1, "Shelter": 1, "Office": 1})
    lion_enclosure = Entity("狮子园区", space, {"Scene": 1, "Animals": 1, "Sanctuary": 1})
    lion_rock = Entity("狮子园区假山与工作通道", lion_enclosure, {"Scene": 1, "Shelter": 1})
    director_office = Entity("动物园园长办公室", space, {"Scene": 1, "Office": 1, "Shelter": 1})

    # 3. 里世界海洋馆场景
    aquarium_gate = Entity("海洋馆大门与前廊", space, {"Scene": 1, "Hidden": 1})
    jellyfish_zone = Entity("海洋馆水母区", space, {"Scene": 1, "SafeLighting": 1})
    guest_room = Entity("水母区钟点客房", jellyfish_zone, {"Scene": 1, "RestArea": 1})
    whale_zone = Entity("海洋馆鲸鱼区", space, {"Scene": 1, "Projection": 1})
    monitor_room = Entity("海洋馆监控室", space, {"Scene": 1, "Office": 1})

    # 4. 空间连通拓扑
    space.connect(entrance, drink_shop, cost=10)
    space.connect(drink_shop, ape_enclosure, cost=15)
    space.connect(drink_shop, rabbit_enclosure, cost=15)
    space.connect(rabbit_enclosure, elephant_enclosure, cost=20)
    space.connect(elephant_enclosure, security_office, cost=5)
    space.connect(rabbit_enclosure, lion_enclosure, cost=25)
    space.connect(drink_shop, lion_rock, cost=20)
    space.connect(lion_enclosure, lion_rock, cost=5)
    space.connect(lion_enclosure, director_office, cost=20)

    # 海洋馆内部连通
    space.connect(aquarium_gate, jellyfish_zone, cost=10)
    space.connect(jellyfish_zone, guest_room, cost=5)
    space.connect(jellyfish_zone, whale_zone, cost=15)
    space.connect(jellyfish_zone, monitor_room, cost=10)

    # 表里世界交错通道（Passage）
    # 当角色处于迷路、恐慌或被引导时，可从兔子区或狮子区外围进入海洋馆
    def allow_aquarium_passage(node, actor, env):
        return bool(actor and (actor.has("Lost") or actor.has("Corrupted") or actor.has("SearchingOcean")))

    anomaly_passage = Passage(
        "表里交错通道",
        rabbit_enclosure,
        aquarium_gate,
        cost=30,
        condition=allow_aquarium_passage,
    )
    space.add_passage(anomaly_passage)

    # 5. 动物实体
    # 正常白狮子初始4头
    white_lions = [
        Entity(f"白狮子_{i+1}", lion_enclosure, {"WhiteLion": 1, "Animal": 1, "Sanctuary": 1})
        for i in range(4)
    ]
    # 出逃的兔子与笑声发生体
    escaped_rabbits = Entity("出逃的红眼兔子群", rabbit_enclosure, {"Rabbit": 1, "Anomaly": 1, "Hostile": 1})
    # 大象园普通大象
    regular_elephants = Entity("成年大象群", elephant_enclosure, {"Elephant": 1, "Animal": 1})
    # 猿猴群
    apes = Entity("猿猴群", ape_enclosure, {"Ape": 1, "Animal": 1, "Perceptive": 1})
    # 水母群
    jellyfish = Entity("发光水母生态缸", jellyfish_zone, {"Jellyfish": 1, "Calming": 1})
    # “它”（邪神触角与阴影聚合体）
    the_entity = Entity("“它”（不可名状的阴影存在）", space, {"EntityIT": 1, "Anomaly": 1, "Stealth": 1})

    # 6. 核心道具与设施
    # 带有虚线的地图
    zoo_maps = [
        Entity(
            f"园区导览地图_{i+1}",
            entrance,
            {"Item": 1, "Map": 1},
            properties={"has_dotted_line": True, "torn": False},
        )
        for i in range(5)
    ]
    # 备用虚线纸条（撕下后具备护身符效果）
    torn_dotted_lines = [
        Entity("撕下的地图虚线纸条", entrance, {"Item": 1, "DottedLine": 1, "Talisman": 1})
        for _ in range(3)
    ]
    # 兔子血（饮料店冷柜偶发出现）
    rabbit_blood = Entity("货架上的“兔子血”", drink_shop, {"Item": 1, "RabbitBlood": 1})
    # 山羊肉（生肉试探物）
    goat_meat = Entity("货架上的“山羊肉”包装", drink_shop, {"Item": 1, "GoatMeat": 1})
    # 水母小夜灯
    jellyfish_lamp = Entity(
        "客房水母小夜灯",
        guest_room,
        {"Item": 1, "Lamp": 1},
        properties={"turned_on": True, "charged": True},
    )
    # 园长办公室表格“它”
    table_it = Entity(
        "统计记录表《它》",
        director_office,
        {"Document": 1},
        properties={"lion_records": [4, 4, 4]},
    )
    # 鲸鱼区大象收纳箱
    whale_storage = Entity("鲸鱼区大型密闭收纳箱", whale_zone, {"Container": 1})

    scenes = {
        "entrance": entrance,
        "drink_shop": drink_shop,
        "ape_enclosure": ape_enclosure,
        "rabbit_enclosure": rabbit_enclosure,
        "elephant_enclosure": elephant_enclosure,
        "security_office": security_office,
        "lion_enclosure": lion_enclosure,
        "lion_rock": lion_rock,
        "director_office": director_office,
        "aquarium_gate": aquarium_gate,
        "jellyfish_zone": jellyfish_zone,
        "guest_room": guest_room,
        "whale_zone": whale_zone,
        "monitor_room": monitor_room,
    }

    entities = {
        "zoo_authority": zoo_authority,
        "aquarium_authority": aquarium_authority,
        "white_lions": white_lions,
        "escaped_rabbits": escaped_rabbits,
        "regular_elephants": regular_elephants,
        "apes": apes,
        "jellyfish": jellyfish,
        "the_entity": the_entity,
        "zoo_maps": zoo_maps,
        "torn_dotted_lines": torn_dotted_lines,
        "rabbit_blood": rabbit_blood,
        "goat_meat": goat_meat,
        "jellyfish_lamp": jellyfish_lamp,
        "table_it": table_it,
        "whale_storage": whale_storage,
    }

    return space, scenes, entities
