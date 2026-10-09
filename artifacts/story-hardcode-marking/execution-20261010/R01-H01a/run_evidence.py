from pathlib import Path
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[3]
CLONE = REPO / 'tmp/solution-review-workers/H01a'
BASE = '7489db56a714724eb7cb735958dd879dc662d238'
TIP = 'dc63af04213ac0d418b1b598bcd9e025d657eafe'

def run(name, args, cwd=CLONE):
    completed = subprocess.run(args, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (ROOT / (name + '.log')).write_bytes(completed.stdout)
    metadata = {'name': name, 'args': args, 'cwd': str(cwd), 'exit_code': completed.returncode, 'output_bytes': len(completed.stdout)}
    (ROOT / (name + '.result.json')).write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print(json.dumps(metadata), flush=True)
    return completed.returncode

if __name__ == '__main__':
    action = sys.argv[1]
    if action == 'diff':
        run('raw-diff', ['git', 'diff', '--no-ext-diff', '--no-textconv', BASE, TIP, '--', 'godot_csharp/scenes/Shared/Story/WorldBehavior.cs'])
    elif action == 'host':
        run('host-build', ['dotnet', 'build', 'godot_csharp/DualOntologyWorldEngine.sln', '--nologo'])
    elif action in ('formal', 'baseline-formal'):
        build_path = CLONE / ('csharp/bin/h01a-' + action)
        args = ['dotnet', 'build', 'godot_csharp/scenes/Shared/check/WorldScriptCheck/WorldScriptCheck.csproj', '-p:UseArtifactsOutput=true', '-p:ArtifactsPath=' + str(build_path)]
        if action == 'baseline-formal':
            args += ['-p:CustomAfterMicrosoftCommonTargets=' + str(ROOT / 'baseline-overlay.targets')]
        if run(action + '-build', args) == 0:
            run(action + '-run', ['dotnet', str(build_path / 'bin/WorldScriptCheck/debug/WorldScriptCheck.dll')])
    elif action == 'baseline-source':
        (ROOT / 'baseline').mkdir(exist_ok=True)
        source = subprocess.run(['git', 'show', BASE + ':godot_csharp/scenes/Shared/Story/WorldBehavior.cs'], cwd=CLONE, check=True, stdout=subprocess.PIPE).stdout
        (ROOT / 'baseline/WorldBehavior.cs').write_bytes(source)
        source_path = (ROOT / 'baseline/WorldBehavior.cs').as_posix()
        overlay = '<Project><Target Name="H01aBaselineSourceOverlay" BeforeTargets="CoreCompile" Condition="\'$(MSBuildProjectName)\' == \'ChapterRuntime\'"><ItemGroup><Compile Remove="WorldBehavior.cs" /><Compile Include="' + source_path + '" /></ItemGroup></Target></Project>\n'
        (ROOT / 'baseline-overlay.targets').write_text(overlay, encoding='utf-8')
        print('Saved exact baseline WorldBehavior.cs and external build overlay; clone content unchanged.')
    elif action == 'count':
        path = CLONE / 'godot_csharp/scenes/Awakening/Script/generated_chapter_01_world.json'
        data = json.loads(path.read_bytes())
        chapters = data['chapters']
        result = {'path': str(path), 'chapter_count': len(chapters), 'candidate_count': sum(len(ch.get('candidates', [])) for ch in chapters)}
        (ROOT / 'formal-candidate-count.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(json.dumps(result))
    elif action in ('formal-with-argument', 'baseline-formal-with-argument'):
        prior = 'baseline-formal' if action.startswith('baseline-') else 'formal'
        build_path = CLONE / ('csharp/bin/h01a-' + prior)
        run(action, ['dotnet', str(build_path / 'bin/WorldScriptCheck/debug/WorldScriptCheck.dll'), 'godot_csharp/scenes/Shared/Story/Examples/WorldScript/generated_world.json'])
    elif action in ('neutral', 'baseline-neutral'):
        build_path = ROOT / ('build-' + action)
        args = ['dotnet', 'build', str(ROOT / 'neutral/NeutralProvenanceCheck.csproj'), '-p:UseArtifactsOutput=true', '-p:ArtifactsPath=' + str(build_path)]
        if action == 'baseline-neutral':
            args += ['-p:CustomAfterMicrosoftCommonTargets=' + str(ROOT / 'baseline-overlay.targets')]
        if run(action + '-build', args) == 0:
            run(action + '-run', ['dotnet', str(build_path / 'bin/NeutralProvenanceCheck/debug/NeutralProvenanceCheck.dll')])
    elif action == 'guard':
        run('final-guard', [sys.executable, str(ROOT.parent / 'F02/hardened_scope_guard.py'), '--repo', str(CLONE), '--lock', str(ROOT.parent / 'control/H01a.scope.lock.json'), '--head', TIP, '--json'], REPO)
    else:
        raise SystemExit('Unknown action: ' + action)
