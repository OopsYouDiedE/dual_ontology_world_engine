import subprocess,time,pathlib,json,re,shutil
started=time.time(); root=pathlib.Path.cwd(); out=root/'artifacts/python-story-refactor/auto-02'; results=[]
report=root/'artifacts/python-story-refactor/auto-validation-02.txt'
def save():
 report.write_text('独立冻结构建自动验收 02\n自动状态注入，不是盲玩。未读取 docs、README、auto-01 或历史报告；未修改生产实现。\n新增检查仅 StoryPacketCheck/Program.cs 的真实 Chapter1Interaction 回归。每项仅运行一次。\n\n'+ '\n'.join(f"{r['status']} | {r['name']} | exit={r.get('exit','N/A')} | {r.get('seconds',0):.3f}s | {r.get('note','')}" for r in results)+'\n',encoding='utf-8')
def skip(name,note):
 results.append(dict(name=name,status='未执行',note=note));save()
def run(name,args,timeout=180,godot=False):
 start=time.monotonic(); log=out/(name+'.log'); timed=False
 with log.open('wb') as f:
  p=subprocess.Popen(args,stdout=f,stderr=subprocess.STDOUT,cwd=root)
  try: code=p.wait(timeout=timeout)
  except subprocess.TimeoutExpired:
   timed=True;p.kill();code=p.wait()
 data=log.read_text(encoding='utf-8',errors='replace')
 errors=bool(re.search(r'(?m)(?:SCRIPT ERROR:|ERROR:|Parse Error|FAIL )',data))
 ok=code==0 and not timed and (not godot or not errors)
 results.append(dict(name=name,status='通过' if ok else '失败',exit=code,seconds=time.monotonic()-start,note=('180秒超时，仅终止本项进程；' if timed else '')+('日志检测到 ERROR/FAIL；' if godot and errors else '')+'log=auto-02/'+log.name))
 save(); print(json.dumps(results[-1],ensure_ascii=False),flush=True);return ok
run('compile-story',['python','godot_csharp/tools/compile_story.py','--check'])
run('independent-story-compare',['python','godot_csharp/scenes/Awakening/check/independent_story_compare.py'])
run('independent-compiler-edge',['python','godot_csharp/scenes/Awakening/check/independent_compiler_edge_check.py'])
built=run('solution-build',['dotnet','build','godot_csharp/DualOntologyWorldEngine.sln'],timeout=180)
checks=['StoryPacketCheck','FormalScriptCheck','ReplayCheck']
for check in checks:
 if built: run(check,['dotnet','run','--project',f'godot_csharp/scenes/Awakening/check/{check}/{check}.csproj'])
 else: skip(check,'solution build failed; stale assembly prohibited')
examples=[]
for name,directory in [('ExampleWorld','world'),('ExampleAwakening','awakening')]:
 if built:
  dest=root/'tmp/python-story-refactor/examples'/directory
  ok=run('compile-'+name,['powershell','-NoProfile','-File','godot_csharp/tools/compile_world_script.ps1','-Source',f'godot_csharp/scenes/Shared/Story/Examples/{name}.cs','-Output',str(dest)])
  examples.append(ok)
 else: skip('compile-'+name,'solution build failed')
if built and all(examples):
 run('SharedRuntimeCheck',['dotnet','run','--project','godot_csharp/scenes/Awakening/check/SharedRuntimeCheck/SharedRuntimeCheck.csproj','--',str(root/'tmp/python-story-refactor/examples/world/WorldScript.ExampleWorld.dll'),str(root/'tmp/python-story-refactor/examples/awakening/WorldScript.ExampleAwakening.dll')])
else: skip('SharedRuntimeCheck','build dependency failed')
godot=r'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe'
imported=run('godot-import',[godot,'--headless','--editor','--path','godot_csharp','--import','--quit'],godot=True) if built else False
if not built: skip('godot-import','solution build failed')
flow=False
if built and imported: flow=run('flow-check',[godot,'--headless','--path','godot_csharp','--script','res://scenes/Awakening/check/flow_check.gd'],godot=True)
else: skip('flow-check','build/import dependency failed')
for name,script,window in [('final-content','scenes/Awakening/check/final_content_check.gd',False),('final-shared-runtime','scenes/Awakening/check/final_shared_runtime_check.gd',False),('hand-outline','scenes/Awakening/check/hand_outline_check.gd',True),('cabinet-fog-current','scenes/Awakening/check/cabinet_fog_current_check.gd',True),('formal-chapter-host','scenes/Chapter2/Game/check/formal_chapter_host_check.gd',True)]:
 if flow:
  args=[godot,'--path','godot_csharp','--rendering-method','gl_compatibility','--script','res://'+script]
  if not window:args.insert(1,'--headless')
  run(name,args,godot=True)
 else: skip(name,'flow/import failed; dependent scene checks stopped')
# Preserve screenshot evidence produced by scripts, without reading existing reports.
for path in [root/'artifacts/python-story-refactor',root/'artifacts/remake-chapter2/shared-runtime']:
 for image in path.glob('*.png'):
  if image.stat().st_mtime>=started:shutil.copy2(image,out/image.name)
(out/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')

