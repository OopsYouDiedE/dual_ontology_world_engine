import subprocess,pathlib,json
p=pathlib.Path('artifacts/detective-agent/chapter2-20261011/verification/loop-remedy-r15').resolve()
r=subprocess.run(['git','show','HEAD:godot_csharp/scenes/Shared/Story/PlayerController.cs'],stdout=subprocess.PIPE,check=True)
(p/'baseline-PlayerController.cs.txt').write_bytes(r.stdout)
f=(p/'baseline-PlayerController.cs.txt').as_posix()
(p/'Baseline.targets').write_text('<Project><Target Name="GroundBaselineController" BeforeTargets="CoreCompile" Condition="\'$(MSBuildProjectName)\' == \'ChapterRuntime\'"><ItemGroup><Compile Remove="PlayerController.cs"/><Compile Include="'+f+'"/></ItemGroup><Message Importance="high" Text="BASELINE HEAD PlayerController replacement active"/></Target></Project>',encoding='utf8')
cmd=['dotnet','run','--project','godot_csharp/scenes/Shared/check/StrategyIntegrationCheck','-c','Release','-p:CustomAfterMicrosoftCommonTargets='+str(p/'Baseline.targets')]
r=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
(p/'integration-baseline.log').write_bytes(r.stdout)
print(r.returncode)
print('\n'.join(x for x in r.stdout.decode(errors='replace').splitlines() if x.startswith(('FAIL ','StrategyIntegrationCheck:')) or 'BASELINE HEAD' in x))
