"""Complete the alias probe with the existing compiler's required neutral option label."""
import importlib.util
import json
from pathlib import Path
import sys
sys.dont_write_bytecode = True
base = Path(__file__).resolve().parent
root = base.parents[3]
source = 'chapter(id="probe")\nr = room(id="r", name="空间")\nx = object(id="x", name="件", aliases=["器"], room=r)\n@option(id="a", actor="player", target="器", label="中性选项")\ndef a():\n    inspect(target="器")\n'
path = base / "compiler-inputs/C02b_alias_to_id_complete.py"
spec = importlib.util.spec_from_file_location("d01a_world_script_alias", root / "godot_csharp/tools/world_script.py")
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
try:
    document = module.Compiler([path], texts={path.as_posix(): source}).parse()
    result = {"status": "COMPILED", "document": document}
except module.ScriptError as exc:
    result = {"status": "REJECTED", "error": str(exc)}
(base / "compiler-alias-complete.json").write_text(json.dumps({"id": "C02b_alias_to_id_complete", "neutral_source": source,
    "reason": "The first C02 probe omitted required option label and is preserved unchanged in compiler-results.json.", "observed": result},
    ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result.get("status")))
