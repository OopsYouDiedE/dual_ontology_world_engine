import re

from rich.console import Console, Group
from rich.panel import Panel
from rich.table import Table
from rich.text import Text


def list_view(title, entries, empty="（空）"):
    table = Table.grid(padding=(0, 1))
    table.add_column(justify="right", style="cyan")
    table.add_column()
    for index, entry in enumerate(entries, 1):
        table.add_row(f"{index}.", entry)
    if not entries:
        table.add_row("", f"[dim]{empty}[/]")
    return Group(Text(title, style="bold yellow"), table)


def status_bar(values):
    table = Table.grid(expand=True)
    for _ in values:
        table.add_column()
    table.add_row(*(f"[dim]{name}[/] [cyan]{value}[/]" for name, value in values))
    return Panel(table, padding=(0, 1), border_style="yellow")


def parse_indexes(value, maximum, minimum_count=1, maximum_count=None):
    maximum_count = maximum if maximum_count is None else maximum_count
    try:
        indexes = list(dict.fromkeys(int(part) for part in re.split(r"[ ,，]+", value.strip()) if part))
    except ValueError:
        return []
    return indexes if minimum_count <= len(indexes) <= maximum_count and all(1 <= item <= maximum for item in indexes) else []


class TerminalUI:
    def __init__(self, color=True, clear=True):
        self.console = Console(color_system="auto" if color else None, highlight=False)
        self.clear_enabled = clear

    @property
    def width(self):
        return self.console.width

    def clear(self):
        if self.clear_enabled and self.console.is_terminal:
            self.console.clear()

    def heading(self, title, subtitle=""):
        self.console.rule(f"[bold yellow]✦ {title} ✦[/]")
        if subtitle:
            self.console.print(subtitle, style="dim", justify="center")

    def status(self, values):
        self.console.print(status_bar(values))

    def panel(self, title, content, style="yellow"):
        self.console.print(Panel(content, title=title, border_style=style))

    def list(self, title, entries, empty="（空）"):
        self.console.print(list_view(title, entries, empty))

    def log(self, entries, limit=8):
        content = "\n".join(f"• {entry}" for entry in entries[-limit:]) or "尚无记录"
        self.panel("因果记录", content)

    def menu(self, title, options):
        self.list(title, [f"{label}  [dim][{key}][/]" for key, label in options])
        aliases = {str(index): key for index, (key, _) in enumerate(options, 1)} | {key: key for key, _ in options}
        while (answer := self.prompt("› ").casefold()) not in aliases:
            self.error("请输入有效编号或命令。")
        return aliases[answer]

    def prompt(self, message):
        return self.console.input(f"[bold yellow]{message}[/]").strip()

    def notice(self, message):
        self.console.print(message, style="dim")

    def error(self, message):
        self.console.print(message, style="red")
