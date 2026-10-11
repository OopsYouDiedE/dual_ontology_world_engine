import subprocess,pathlib
p=pathlib.Path('artifacts/detective-agent/chapter2-20261011/verification/loop-remedy-r15').resolve()
for name,cmd in [('integration-baseline-build',['dotnet','build','godot_csharp/scenes/Shared/check/StrategyIntegrationCheck','-c','Release','-t:Rebuild','-p:CustomAfterMicrosoftCommonTargets='+str(p/'Baseline.targets')]),('integration-baseline-confirmed',['dotnet','run','--project','godot_csharp/scenes/Shared/check/StrategyIntegrationCheck','-c','Release','--no-build']),('controller-final-build',['dotnet','build','godot_csharp/scenes/Shared/check/PlayerControllerCheck','-c','Release','-t:Rebuild']),('controller-final',['dotnet','run','--project','godot_csharp/scenes/Shared/check/PlayerControllerCheck','-c','Release','--no-build'])]:
 r=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 (p/(name+'.log')).write_bytes(r.stdout)
 print(name,r.returncode,flush=True)
 print('\n'.join(x for x in r.stdout.decode(errors='replace').splitlines() if x.startswith(('FAIL ','StrategyIntegrationCheck:')) or 'BASELINE HEAD' in x or 'checks passed' in x),flush=True)
 if r.returncode and name.endswith('build'): break
