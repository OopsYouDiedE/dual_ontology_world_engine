# 独立机制示例：继承同一对象和认知，只扩展默认属性及候选。
章节：morning
前置章节：arrival

原生
    sample_room = ref(id="room.sample")
    sample_object = ref(id="object.sample")
    primary = ref(id="character.primary")
    secondary = ref(id="character.secondary")

    extend(target=sample_object, properties={"observable:additional_detail": True})
    extend(target=primary, properties={"state:sample_counter": 0})

    @选项(id="inspect_extension", target=sample_object, applicability=known(sample_object))
    def inspect_extension():
        ```
    示例角色甲：查看新增的可见属性。
    ```
        inspect(target=sample_object)

    @反应(id="detailed_extension", actor=secondary, intent="testimony", response_to=["inspect_extension"],
        applicability=at(secondary, sample_room) and known(sample_object),
        goal=state(secondary, "strategy") >= 1, weight=6)
    def detailed_extension():
        ```
    示例角色乙：继承后的详细回应示例。
    ```

    @反应(id="brief_extension", actor=secondary, intent="testimony", response_to=["inspect_extension"],
        applicability=known(sample_object), goal=state(secondary, "strategy") == 0, weight=1)
    def brief_extension():
        ```
    示例角色乙：继承后的简短回应示例。
    ```
