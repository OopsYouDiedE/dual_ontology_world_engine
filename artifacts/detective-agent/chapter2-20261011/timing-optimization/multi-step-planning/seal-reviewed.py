"""Create an explicitly reviewed new seal; never called by the game runner."""
import argparse, hashlib, json, subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--root', required=True)
p.add_argument('--output', required=True)
p.add_argument('--review', required=True)
args = p.parse_args()
root = Path(args.root).resolve()
names = subprocess.check_output(['git','ls-files','-z','--cached','--others','--exclude-standard'],cwd=root).decode().split('\0')
paths = [root/n for n in names if n and (n.startswith(('csharp/','godot_csharp/','tools/','docs/')) or n=='AGENTS.md') and (root/n).is_file()]
paths += list((root/'godot_csharp/.godot/mono/temp/bin/Debug').glob('*.dll'))
out = root/args.output
out.parent.mkdir(parents=True,exist_ok=True)
if out.exists(): raise RuntimeError('Do not overwrite an existing seal')
files = {f.relative_to(root).as_posix():hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(set(paths))}
out.write_text(json.dumps({'label':'Reviewed multi-step planning comparison: separate source baseline',
 'runtime_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root).decode().strip(),
 'review':args.review,'authorized_change':'User explicitly authorized generic Core planning and isolated variants; no relaxed story, judgement, input, collision, time or measurement rules.',
 'files':files},ensure_ascii=False,indent=2),encoding='utf-8')
print(out)
