"""双重本体世界引擎的 Python 核心实现。

导入顺序体现依赖方向：实体模型位于底层，Soul 与社交模块表达认知和选择，
言灵模块负责受约束的状态改变，engine 最后组合时间线与行动结算。
"""

from .base_actions import *
from .engine import *
from .entity_system import *
from .incident import *
from .kotodama import *
from .social import *
from .soul import *
