"""《异常感知学园》第一章：禁闭后的二十天（兼容导出入口）。

初期篇逻辑已拆分至 `python_cli.prologue` 与 `python_cli.content` 子包。
本模块保持向下兼容导出。
"""

from .content.prologue_data import (
    APPROACH,
    DAY_SPECS,
    LEISURE,
    PROJECT_SPECS,
    REPAIR_SCENES,
)
from .prologue.academy_prologue import AcademyPrologue

__all__ = [
    "AcademyPrologue",
    "DAY_SPECS",
    "PROJECT_SPECS",
    "APPROACH",
    "LEISURE",
    "REPAIR_SCENES",
]
