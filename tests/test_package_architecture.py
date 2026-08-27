import ast,subprocess,sys
from pathlib import Path
PACKAGE=Path(__file__).parents[1]/"dual_ontology_engine"/"core"
def test_core_has_no_support_or_legacy_star_imports():
    for source in PACKAGE.rglob("*.py"):
        tree=ast.parse(source.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if isinstance(node,ast.ImportFrom):
                assert "supports" not in (node.module or "")
                assert not ((node.module or "").endswith("legacy") and any(alias.name=="*" for alias in node.names))
def test_core_import_isolated():
    subprocess.run([sys.executable,"-c","import sys; import dual_ontology_engine.core; assert not any(n.startswith('dual_ontology_engine.supports') for n in sys.modules)"],check=True)
