from pathlib import Path
import json
p=Path('artifacts/remake-chapter2/refactor-v5/auto-05')
def norm(text, cli=False):
 return [s.strip() for s in text.splitlines() if s.strip() and not (cli and s.startswith('> '))]
a=norm((p/'godot-visible-output.txt').read_text(encoding='utf-8-sig'))
b=norm((p/'cli-output.txt').read_text(encoding='utf-8-sig'), True)
diff=[(i+1,x,y) for i,(x,y) in enumerate(zip(a,b)) if x!=y]
r={'godot_nonempty_lines':len(a),'cli_nonempty_lines':len(b),'equal':a==b,'differences':diff[:10]}
(p/'comparison.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(r,ensure_ascii=False))
rp=json.loads((p/'replay.json').read_text(encoding='utf-8-sig'))
print('replay keys',rp.keys())
print('frame count',len(rp.get('frames',[])))
print('frame sample',rp.get('frames',[])[:2])
