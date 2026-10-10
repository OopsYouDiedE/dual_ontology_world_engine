from pathlib import Path
import hashlib
import json
import re

directory = Path(__file__).resolve().parent
repository = directory.parents[4]
final = ['host-build', 'neutral-navigation', 'neutral-physics-input-drained',
         'controller-build-formal-candidates', 'controller-runtime-formal-candidates']
records = []
for path in sorted(directory.glob('*.json')):
    if path.name == 'manifest.json':
        continue
    record = json.loads(path.read_text(encoding='utf-8'))
    if 'log' not in record:
        continue
    raw = (directory / record['log']).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == record['sha256'], path
    if path.stem in final:
        assert record['exit_code'] == 0, path
        assert not re.search(rb'SCRIPT ERROR|^ERROR:|^FAIL |Unhandled exception', raw, re.MULTILINE), path
    records.append(record)
source_paths = [
    'godot_csharp/scenes/Host/PilotNavigation.gd', 'godot_csharp/scenes/Host/AgentPilot.gd',
    'godot_csharp/scenes/Host/GameEngine.cs', 'godot_csharp/scenes/Shared/FieldNavigation.gd',
    'godot_csharp/scenes/Shared/Story/PlayerController.cs',
    'godot_csharp/scenes/Shared/check/PlayerControllerCheck/Program.cs',
    'godot_csharp/tools/check/check_virtual_navigation.gd', 'godot_csharp/tools/check/check_campus_access.gd']
sources = {path: hashlib.sha256((repository / path).read_bytes()).hexdigest() for path in source_paths}
manifest = {'final_runs': final, 'runs': records, 'source_sha256_at_archive': sources,
            'raw_logs_preserved': True, 'formal_story_read': False, 'design_read': False,
            'full_test_run': False, 'ui_acceptance': False, 'blind_play': False}
(directory / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS evidence hashes and final log diagnostics')
