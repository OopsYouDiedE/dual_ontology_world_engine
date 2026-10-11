import subprocess,pathlib,json,time
p=pathlib.Path('artifacts/detective-agent/chapter2-20261011/verification/loop-remedy-r15')
commands=[('integration',['dotnet','run','--project','godot_csharp/scenes/Shared/check/StrategyIntegrationCheck','-c','Release']),('core-decision',['dotnet','run','--project','csharp/SubjectiveDecisionRegression.csproj','-c','Release']),('debug-build',['dotnet','build','godot_csharp/DualOntologyWorldEngine.sln','-m:1','--disable-build-servers']),('import',[r'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe','--headless','--editor','--path','godot_csharp','--import'])]
for name,cmd in commands:
 start=time.time()
 r=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 (p/(name+'.log')).write_bytes(r.stdout)
 (p/(name+'.json')).write_text(json.dumps({'command':cmd,'exit':r.returncode,'seconds':time.time()-start}),encoding='utf8')
 print(name,r.returncode,r.stdout.decode(errors='replace')[-450:],flush=True)
