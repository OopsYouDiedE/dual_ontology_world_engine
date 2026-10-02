"""Publish revised spoken copy while retaining the chapter's runtime cues.

The design's scene descriptions remain author notes and inspection records;
they are not speakers in an automatic performance. This is a content writer,
not a Godot import, build or gameplay runner.
"""
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[2]
DESIGN = ROOT / "docs/design-standard.md"
DIALOGUE = ROOT / "godot_csharp/scenes/FirstAwakening/red_token.dialogue"
AUTHOR_ONLY = {"检视", "操作提示", "推演回放"}


def spoken_copy(body: str) -> str:
    lines = []
    for line in body.strip().splitlines():
        match = re.match(r"^(\s*)([^：:]+)：(.*)$", line)
        if match:
            indent, speaker, text = match.groups()
            if speaker.strip() in AUTHOR_ONLY:
                continue
            line = f"{indent}{speaker}: {text.lstrip()}"
        lines.append(line.rstrip())
    return "\n".join(lines)


def publish() -> None:
    design = DESIGN.read_text(encoding="utf-8")
    draft = {
        match[1]: spoken_copy(match[2])
        for match in re.finditer(
            r"<!-- SCENE (\w+) route=\w+ -->\s*(.*?)<!-- SCENE_END -->",
            design, re.S,
        )
    }
    text = DIALOGUE.read_text(encoding="utf-8")

    def rewrite(match: re.Match) -> str:
        title, current = match[1], match[2]
        if title not in draft:
            return match[0]
        if title == "S01":
            # The opening already matches the six spoken lines; retain head
            # turns and its single explicit attention search in their places.
            return match[0]
        if title == "S28":
            # This node owns live exit tracks. Its admission is a separate
            # runtime node, and its individual spoken actions remain cues.
            lines = [line for line in current.splitlines()
                     if not re.match(r"^\s*检视\s*:", line)]
            return "~ S28\n" + "\n".join(lines).rstrip() + "\n\n"
        body = draft[title]
        if title == "S02":
            body = body.replace("里面的朋友醒了吗？", "醒了？")
            body += (
                "\ndo dialogue_confirm_red_companions()\n"
                "if not story_ok\n\t=> STAGE_BLOCKED\n"
                "if not copper_introduction_complete\n\t=> COPPER_INTRO"
            )
        elif title == "COPPER_INTRO":
            body += (
                "\ndo dialogue_complete_copper_introduction()\n"
                "if not story_ok\n\t=> STAGE_BLOCKED"
            )
        elif title == "S04":
            first, rest = body.split("\n", 1)
            # Safety information remains available before actual delivery;
            # its first line must not claim that unexamined money was checked.
            body = (
                "if receipt_checked\n\t" + first + "\nelse\n"
                "\t沈鼎: 先把话说清。我们几个都只记得登船，后面全是空的。这是怎么回事？\n"
                + rest
            )
        return f"~ {title}\n{body}\n=> END\n\n"

    rewritten = re.sub(r"^~ (\w+)\n(.*?)(?=^~ |\Z)", rewrite, text, flags=re.M | re.S)
    DIALOGUE.write_text(rewritten, encoding="utf-8", newline="\n")
    print("Published revised chapter dialogue; opening, exit and observation entries retained.")


if __name__ == "__main__":
    publish()
