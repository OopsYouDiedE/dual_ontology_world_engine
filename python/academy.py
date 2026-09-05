"""《异常感知学园》的兼容导出入口。

静态内容位于 :mod:`academy_content`，正式运行时位于 :mod:`academy_game`。
新代码应直接从对应模块导入；本模块只维护既有导入路径。
"""

from .academy_content import *
from .academy_content import __all__ as _content_exports
from .academy_game import AcademyGame

__all__ = [*_content_exports, "AcademyGame"]
