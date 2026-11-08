"""Prop Workshop: parameter-driven lowpoly props from the Blender kit.

Write a recipe, run the workshop, and the GLB lands where Godot prefabs read it.

  python3.11 tools/building/prop_workshop.py --list                  # all generators
  python3.11 tools/building/prop_workshop.py --describe chair         # its parameters + defaults
  python3.11 tools/building/prop_workshop.py tools/building/prop_recipes/study_armchair.json
  python3.11 tools/building/prop_workshop.py --all                   # rebuild every recipe

Recipe (JSON):
  {
    "name": "study_armchair",            # object name and default file stem
    "generator": "chair",                # any kit generator, see --list
    "parameters": {"arms": true, "height": 1.05},
    "palette": {"wood": "#5A3422", "green": "#5B2A2A"},   # optional colour overrides
    "output": "assets/meshes/study_armchair.glb"          # optional, defaults to this path
  }
Unknown parameters are rejected so a typo never silently produces a default prop.
Requires: Python 3.11 with `pip install bpy==4.5.3` (matches mcp/game/environment.py).
"""
import argparse, inspect, json, os, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
KIT_PARENT = Path(__file__).resolve().parent / "lowpoly_chapters_v3"
sys.path.insert(0, str(KIT_PARENT))
MODULES = ["kit.furniture", "kit.decor", "kit.special_props", "kit.public_props",
           "kit.residential_props", "kit.industrial_props", "kit.outdoor_props", "kit.lighting", "kit.openings"]

def generators():
    import importlib
    found = {}
    for name in MODULES:
        try: module = importlib.import_module(name)
        except Exception as error: print("skip", name, error, file=sys.stderr); continue
        for key, fn in vars(module).items():
            if key.startswith("_") or not inspect.isfunction(fn) or fn.__module__ != module.__name__: continue
            params = list(inspect.signature(fn).parameters)
            if len(params) >= 2 and params[0] == "name" and params[1] == "mats":
                found.setdefault(key, fn)
    return found

def describe(fn):
    out = {}
    for p in list(inspect.signature(fn).parameters.values())[2:]:
        if p.name == "parent": continue
        out[p.name] = None if p.default is inspect.Parameter.empty else p.default
    return out

def build(recipe_path):
    import bpy
    from kit import core
    recipe = json.loads(Path(recipe_path).read_text(encoding="utf-8"))
    gens = generators()
    fn = gens.get(recipe["generator"])
    if fn is None: raise SystemExit("unknown generator %r; run --list" % recipe["generator"])
    allowed = describe(fn); params = recipe.get("parameters", {})
    bad = sorted(set(params) - set(allowed))
    if bad: raise SystemExit("unknown parameters for %s: %s (allowed: %s)" % (recipe["generator"], bad, sorted(allowed)))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = core.palette()
    for key, colour in recipe.get("palette", {}).items():
        if key not in mats: raise SystemExit("unknown palette key %r (have %s)" % (key, sorted(mats)))
        mats[key] = core.solid_material("LP_" + key + "_custom", colour, glass=(key == "glass"))
    root = fn(recipe["name"], mats, **params)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    objects = [root] + list(root.children_recursive)
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    out = ROOT / recipe.get("output", "assets/meshes/%s.glb" % recipe["name"])
    out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(out), export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_extras=False, export_cameras=False, export_lights=False)
    import mathutils
    corners = [o.matrix_world @ mathutils.Vector(c) for o in objects if o.type == "MESH" for c in o.bound_box]
    size = [round(max(c[i] for c in corners) - min(c[i] for c in corners), 3) for i in range(3)] if corners else [0, 0, 0]
    tris = sum(len(o.data.polygons) for o in objects if o.type == "MESH")
    print(json.dumps({"recipe": str(recipe_path), "output": str(out.relative_to(ROOT)), "bytes": out.stat().st_size,
                      "size_m_xyz": size, "faces": tris, "parameters": {**allowed, **params}}, ensure_ascii=False))

if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("recipes", nargs="*"); ap.add_argument("--list", action="store_true")
    ap.add_argument("--describe"); ap.add_argument("--all", action="store_true")
    a = ap.parse_args()
    try:
        if a.list: print(json.dumps(sorted(generators()), ensure_ascii=False))
        elif a.describe:
            g = generators().get(a.describe)
            print(json.dumps(describe(g), ensure_ascii=False, indent=1) if g else "unknown generator")
        else:
            paths = sorted((ROOT / "tools/building/prop_recipes").glob("*.json")) if a.all else a.recipes
            if not paths: ap.print_help()
            for p in paths: build(p)
    finally:
        sys.stdout.flush(); os._exit(0)   # bpy can segfault on interpreter teardown
