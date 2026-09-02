"""学园主游戏逻辑包 (Game Runtime)。

包含世界构建、社交情绪影响、NPC 决策与效用、动作与意图执行以及 AcademyGame 主运行时。
"""

from .academy_game import AcademyGame
from .action_system import (
    advance_final_seal,
    begin_final_seal,
    collect,
    establish_rule,
    invoke_word,
    move,
    profession_action,
    recover_memory,
)

__all__ = [
    "AcademyGame",
    "move",
    "recover_memory",
    "profession_action",
    "collect",
    "invoke_word",
    "establish_rule",
    "begin_final_seal",
    "advance_final_seal",
]
