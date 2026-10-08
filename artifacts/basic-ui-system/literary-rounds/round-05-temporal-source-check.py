"""Implementation self-check only; independent regression and literary review remain separate."""
from pathlib import Path

root = Path(__file__).resolve().parents[3]
code = root / "godot_csharp/scenes/Chapter2/Prototype"
story = (root / "godot_csharp/scenes/Awakening/Script/story/续篇/07_家具与文本.story").read_text(encoding="utf-8")
emergence = (code / "Emergence.cs").read_text(encoding="utf-8")
loop = (code / "GameLoop.cs").read_text(encoding="utf-8")
social = (code / "Social.cs").read_text(encoding="utf-8")
retell = (code / "Retell.cs").read_text(encoding="utf-8")
player = (code / "Player.cs").read_text(encoding="utf-8")
assert 'Job.PostRules => K("贴规"' not in emergence
posts = loop[loop.index("private void WardPosts"):loop.index("public RuleSpec? WardCommittee")]
assert posts.count('NewsFrom(w, NewsKind.Rumor, K("贴规", w.Id)') == 2
assert posts.index("Post(WardNight, Place.Hall)") < posts.index('NewsFrom(w, NewsKind.Rumor, K("贴规", w.Id)')
assert 'var note = K("补记", p.Id, night: Day - 1);' in loop
assert 'public Slot Slot { get; init; }' in social
assert 'Day = Day, Slot = Slot' in social
assert 'private static Claim PresentedClaim' in retell
assert '.With("记日", Chapter2Design.DayName(item.Day)).With("记时", Names.Of(item.Slot))' in retell
assert 'NightLedgerNames.Count' in player and 'n.Claim.Night == CaseNight' in player
assert '台账补记记录' in player
for key in ('病粮未公开', '病粮未记', '病粮部分已记', '病粮已记', '台账补记记录'):
    assert key + '：' in story
assert '知识有时：' in story
print('PASS implementation source invariants; no runtime/UI/literary acceptance claimed.')
