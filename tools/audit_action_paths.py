"""Read-only source/reference inventory; never starts a session or certifies gameplay."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
CODE = {'.cs', '.gd', '.dv', '.py', '.js', '.ts', '.tsx', '.jsx', '.ps1', '.sh', '.bat', '.cmd', '.csproj', '.sln'}
RETIRED = re.compile(r'\b(AgentPilot|PilotNavigation|GameSession|WorldSession|WorldCore|DispatchField|submit_token|dvs_session_step)\b')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    if args.out.exists():
        raise ValueError('Use a new output path; never overwrite source audit evidence')
    names = sorted(set(subprocess.check_output(
        ['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=ROOT
    ).decode().split('\0')) - {''})
    files, issues, existing_missing = [], [], []
    removed_path = ROOT/'artifacts/partial-bootstrap-core/single-action-path/removed-files.json'
    removed = json.loads(removed_path.read_text(encoding='utf-8'))
    for name in names:
        path = ROOT / name
        if not path.is_file() or name.startswith(('artifacts/', 'music/', 'tmp/', 'scratch/')):
            continue
        if path.suffix not in CODE | {'.tscn', '.tres'}:
            continue
        data = path.read_bytes()
        source = data.decode('utf-8-sig')
        entry = {'path': name, 'sha256': hashlib.sha256(data).hexdigest(), 'role': role(name), 'action_mentions': []}
        if path.suffix == '.py':
            ast.parse(source, filename=name)
        for number, line in enumerate(source.splitlines(), 1):
            if re.search(r'(session_(prepare|complete)|\b(Prepare|Complete|follow_path|execute_move|dv_execute)\b)', line):
                entry['action_mentions'].append({'line': number, 'source': line.strip()})
            # This auditor necessarily lists retired names as data.
            if name != 'tools/audit_action_paths.py' and RETIRED.search(line):
                issues.append({'path': name, 'line': number, 'reason': 'retired entry name', 'source': line.strip()})
        for ref in re.finditer(r'["\']res://([^"\']+)["\']', source):
            target = ref[1]
            if '%' in target or '..' in target or '{' in target or '$script' in target:
                continue
            if Path(target).suffix in {'.gd', '.cs', '.tscn', '.tres'} and not (ROOT/'godot_csharp'/target).exists():
                finding = {'path': name, 'reason': 'missing literal Godot resource', 'target': target}
                (issues if 'godot_csharp/'+target in removed else existing_missing).append(finding)
        if path.suffix in CODE:
            files.append(entry)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps({'scope': 'active tracked and non-ignored source; historical artifacts and temporary output excluded',
                                   'files': files, 'issues': issues, 'existing_missing_resources': existing_missing}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(args.out)
    if issues:
        print(json.dumps(issues, ensure_ascii=False, indent=2))
        raise SystemExit(1)


def role(name):
    if name.startswith('language_core/verification/scenarios/') or name.startswith('language_core/game/'):
        return 'native content / production composition'
    if name.startswith('language_core/verification/session/') or name.startswith('language_core/core/'):
        return 'compiled DV mechanism; prediction and authority remain separate'
    if name.startswith('language_core/Session/') or name.startswith('godot_csharp/scenes/Host/'):
        return 'single session serialization / dispatch / passive view trace'
    if name.startswith('godot_csharp/scenes/Shared/Field') or name.startswith('godot_csharp/scenes/Story/'):
        return 'physical execution / navigation / rendering'
    return 'compiler, read-only judgment, presentation, editor or development utility'


if __name__ == '__main__':
    main()
