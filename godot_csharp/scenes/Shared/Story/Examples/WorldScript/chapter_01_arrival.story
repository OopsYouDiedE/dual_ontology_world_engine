# 独立机制示例：声明、主观候选与策略评分。不是正式章节。
章节：arrival

地点 示例地点
    编号：room.sample
    代号：sample_room

物件 示例物件
    编号：object.sample
    代号：sample_object
    位置：示例地点
    属性：enabled 1
    可见：detail

# 通用角色不需要章节画像；角色、候选条件与动作沿用正式原生块，不执行 Python。
原生
    primary = character(id="character.primary", name="示例角色甲", room=sample_room,
        scope="world", state={"focus": 1}, observable={"present": True})
    secondary = character(id="character.secondary", name="示例角色乙", room=sample_room,
        scope="world", belief={"strategy": 1}, observable={"present": True})

    @选项(id="inspect_sample", target=sample_object, applicability=known(sample_object))
    def inspect_sample():
        ```
    示例角色甲：查看示例物件的可见属性。
    ```
        inspect(target=sample_object)

    @反应(id="detailed_reply", actor=secondary, intent="testimony", response_to=["inspect_sample"],
        applicability=known(sample_object), goal=state(secondary, "strategy") == 1, weight=5)
    def detailed_reply():
        ```
    示例角色乙：这是一条详细回应示例。
    ```

    @反应(id="brief_reply", actor=secondary, intent="testimony", response_to=["inspect_sample"],
        applicability=known(sample_object), goal=state(secondary, "strategy") == 0, weight=1)
    def brief_reply():
        ```
    示例角色乙：这是一条简短回应示例。
    ```
