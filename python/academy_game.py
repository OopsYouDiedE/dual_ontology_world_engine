"""《异常感知学园》群像叙事运行时（兼容导出入口）。

游戏主运行时已拆分至 `python.game` 子包。
本模块保持向下兼容导出。
"""

from .content.academy_data import (
    OBJECTIVE_SPECS,
    RELATION_NEGATIVE,
    RELATION_PROTECTIVE,
    STORY_ROLES,
)
from .game.academy_game import AcademyGame

__all__ = ["AcademyGame", "OBJECTIVE_SPECS", "RELATION_NEGATIVE", "RELATION_PROTECTIVE", "STORY_ROLES"]

import sys as _sys

if _academy_module := _sys.modules.get(f"{__package__}.academy"):
    _academy_module.AcademyGame = AcademyGame
