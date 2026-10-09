"""Read-only artifact/code inventory. This script is evidence tooling, not a game test."""
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone, timedelta
import csv
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
BASELINE = '4aca6644449cd00ba5f5521ad086af54b31dd225'
TZ = timezone(timedelta(hours=8))

def git(*args):
    return subprocess.check_output(['git', '-c', 'core.quotepath=false', *args], cwd=ROOT).decode('utf-8')

def csv_out(name, rows, fields):
    with (OUT / name).open('w', encoding='utf-8-sig', newline='') as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)

tracked = set(git('ls-tree', '-r', '--name-only', BASELINE, '--', 'artifacts').splitlines())
history = git('log', BASELINE, '--format=%x1e%H|%cI', '--name-only', '--', 'artifacts')
dates = {}
for block in history.split('\x1e')[1:]:
    header, *names = block.strip().splitlines()
    commit, date = header.split('|')
    date = datetime.fromisoformat(date).astimezone(TZ).isoformat()
    for name in names:
        name = name.strip()
        if name:
            info = dates.setdefault(name, {'last_commit':commit, 'last_commit_time':date})
            info.update(first_visible_commit=commit, first_visible_commit_time=date)

date_re = re.compile(r'20\d{2}-\d\d-\d\d(?:[T ][0-2]\d:[0-5]\d(?::[0-5]\d(?:\.\d+)?)?(?:Z|[+-]\d\d:\d\d)?)?|20\d{6}[-_]\d{6}(?:[-_]\d+)*')
text_exts = {'.txt','.md','.json','.jsonl','.log','.csv','.py','.gd','.cs','.toml'}
rows = []
for p in sorted((ROOT/'artifacts').rglob('*')):
    if not p.is_file() or OUT in p.parents or any(s in p.parts for s in ('bin','obj','__pycache__')):
        continue
    rel = p.relative_to(ROOT).as_posix()
    stat = p.stat()
    candidates = date_re.findall(rel)
    if p.suffix.lower() in text_exts:
        # Bounded timestamp extraction only; not a semantic judgment of test results.
        sample = p.read_bytes()[:150000].decode('utf-8-sig', errors='replace')
        candidates += date_re.findall(sample)
    candidates = list(dict.fromkeys(candidates))[:15]
    rows.append(dict(path=rel, group=p.relative_to(ROOT/'artifacts').parts[0],
                     bytes=stat.st_size, extension=p.suffix.lower(),
                     tracked_at_baseline=rel in tracked,
                     filesystem_mtime_not_generation=datetime.fromtimestamp(stat.st_mtime,TZ).isoformat(),
                     timestamp_candidates_not_verified=' | '.join(candidates),
                     **{key:dates.get(rel,{}).get(key,'') for key in
                        ('last_commit','last_commit_time','first_visible_commit','first_visible_commit_time')}))
csv_out('artifact-files.csv', rows, list(rows[0]))
groups = []
for group in sorted({r['group'] for r in rows}):
    rs = [r for r in rows if r['group']==group]
    commits = [r['last_commit_time'] for r in rs if r['last_commit_time']]
    groups.append(dict(group=group, files=len(rs), bytes=sum(r['bytes'] for r in rs),
                       tracked_at_baseline=sum(r['tracked_at_baseline'] for r in rs),
                       extensions=dict(Counter(r['extension'] for r in rs)),
                       earliest_last_commit=min(commits) if commits else None,
                       latest_last_commit=max(commits) if commits else None))

source_list = subprocess.check_output(['rg','--files','csharp','godot_csharp','tools',
    '-g','*.cs','-g','*.gd','-g','*.py','-g','*.csproj',
    '-g','!**/bin/**','-g','!**/obj/**','-g','!**/.godot/**'],cwd=ROOT).decode().splitlines()
patterns = {
    'chinese_string_candidate':re.compile(r'"[^"\n]*[\u4e00-\u9fff][^"\n]*"'),
    'fixed_story_identifier_candidate':re.compile(r'\b(?:HallCausalityRules|HallReplayProgram|P\.(?:Quer|Margaret|Mora|Ward|Gu|Ruan)|Person\.(?:Huo|Shen|Su)|SuPlace|HuoPlace|WAIT_SMOKE)\b|"E(?:[1-9]|1[0-9])"'),
    'branch_candidate':re.compile(r'\b(?:if|switch|case)\b.*(?:E[0-9]|Huo|Shen|Su|Quer|Mora|Margaret|answer_done|replay_match|reversal_one|wake|investigate)'),
    'fixture_candidate':re.compile(r'set_progress|debug_jump|skip_intro|answer_done|\.Passed|Assert\.|\bCheck\('),
}
hits = []
for rel_native in source_list:
    rel = Path(rel_native).as_posix()
    text = (ROOT/rel).read_text(encoding='utf-8-sig',errors='replace')
    excluded = any(x in rel.split('/') for x in ('check','tools','Examples')) or 'RegressionProgram' in rel
    generated = Path(rel).name.startswith('generated') or Path(rel).name=='Chapter1CompiledDialogue.cs'
    scope = 'generated' if generated else 'evaluation_tool_or_example' if excluded else 'production_candidate'
    for n,line in enumerate(text.splitlines(),1):
        for label, pattern in patterns.items():
            if pattern.search(line):
                hits.append(dict(path=rel,line=n,scope=scope,pattern=label,excerpt=line.strip()[:500]))
csv_out('regex-candidates.csv',hits,['path','line','scope','pattern','excerpt'])
summary = dict(baseline=BASELINE, inventory_time=datetime.now(TZ).isoformat(),
    method='Filesystem inventory plus baseline Git history; timestamp matches are candidates, never generation proof.',
    exclusions='This audit output directory and bin/obj/__pycache__; source scan excludes .godot.',
    artifact_files=len(rows),artifact_groups=groups,
    source_files_scanned=len(source_list),regex_hits=len(hits),
    regex_files=len({r['path'] for r in hits}),
    patterns={k:v.pattern for k,v in patterns.items()})
(OUT/'inventory-summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
evidence_targets = {
 'artifacts/ch1-actors/verification-report.txt':[
  'verify-motion-final-full.log','verify-stage-failure-full.log','verify-build-final-single-process.log',
  'verification-source-manifest.json','actor_motion_regression.gd','StageFailureFixture/Program.cs'],
 'artifacts/ch1-staging/independent-report.md':[
  'independent-protocol-smoke-final.log','protocol-normal.jsonl','independent-build-final-single-node.log','render'],
 'artifacts/ch1-portrait/verification-report.md':[
  'verification/musician-runtime-contact.png','verification/musician-dialogue-widget-contact.png','verification/portrait-metrics.json'],
 'artifacts/ch1-strings/verification-report.md':[
  'post-replay.jsonl','replay-comparison.json','structured-entry-checks.json','display-variant-comparison.json',
  'post-unified-verify.stdout.log','final-compile-check.stdout.log'],
 'artifacts/ch1-locker/delivery-report.txt':[
  'ui-v05/evidence.json','ui-v05/claim-detective-reach.png','final-v05/report.txt'],
 'artifacts/ch2-flowcheck/report.md':[
  'round-05/validation-report.md','round-05/flow2.stdout.log','round-05/run-metadata.json'],
 'artifacts/remake-belief-replay/regression-repair/report.md':[
  'flow-round3.log','independent-story-check-20261008T202100Z.log','restore-build-diagnosis.md'],
 'artifacts/basic-ui-system/literary-and-ui-conclusion.txt':[
  'literary-rounds/round-05-level1-technical.txt','literary-rounds/round-05-engine-full-output.jsonl',
  'literary-rounds/round-05-ui-full-output.jsonl','ui-rounds/round-04/manifest.json'],
 'artifacts/basic-ui-system/level3-6_1-sol/level3-sol61-20261008-01/acceptance-review.txt':[
  'evidence-audit.json','recording.json','session/metadata.json','gameplay.mp4'],
}
gaps=[]
for report,targets in evidence_targets.items():
    for target in targets:
        p=ROOT/Path(report).parent/target
        gaps.append(dict(report=report,evidence=p.relative_to(ROOT).as_posix(),
                         exists_in_workspace=p.exists(),tracked_at_baseline=p.relative_to(ROOT).as_posix() in tracked,
                         selection='Selected references, not exhaustive; directory targets checked for existence only.'))
csv_out('report-evidence-presence.csv',gaps,list(gaps[0]))
print(json.dumps({'artifact_files':len(rows),'groups':groups,'source_files_scanned':len(source_list),
                  'regex_hits':len(hits),'regex_files':len({r['path'] for r in hits})},ensure_ascii=False))
