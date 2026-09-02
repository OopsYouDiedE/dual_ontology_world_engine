from core.python.entity_system import *

# 测试定义场景
roomA = Entity("房间A", tags={"Scene": 1})
roomB = Entity("房间B", tags={"Scene": 1})

passage = Passage("连接A和B的通道", roomA, roomB)
