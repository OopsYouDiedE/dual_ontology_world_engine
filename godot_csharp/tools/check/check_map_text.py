"""Isolated regression for compiled resource text and batch validation (no Godot)."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import shutil
import sys
import unittest
from unittest.mock import patch
from uuid import uuid4

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import build_awakening_map as ch1
import build_chapter2_scenes as ch2


def write_json(path, data):
    path.write_bytes((json.dumps(data, ensure_ascii=False, indent=4) + "\n").replace("\n", "\r\n").encode())


class MapTextCheck(unittest.TestCase):
    def setUp(self):
        scratch = TOOLS.parents[1] / "tmp"
        scratch.mkdir(parents=True, exist_ok=True)
        # mkdtemp uses mode 0700, whose Windows ACL excludes the restricted test token.
        self.root = scratch / f"map-text-{uuid4().hex}"
        self.root.mkdir()
        if self.root.resolve().parent != scratch.resolve():
            raise ValueError("Fixture directory escaped the workspace scratch directory")
        self.addCleanup(shutil.rmtree, self.root)
        self.layout_json = self.root / "generated_awakening_layout.json"
        self.content_json = self.root / "generated_awakening_content.json"
        self.layout = self.root / "map_layout.tres"
        self.asides = {key: f"new:{key}" for key in ch1.ASIDE_KEYS}
        self.labels = {r[3]: f"new:{r[3]}" for r in ch1.ROOMS.values()}
        write_json(self.layout_json, {"furniture_asides": self.asides})
        write_json(self.content_json, {"ROOM_NAMES": self.labels})
        data = {"asides": [[rect, f"old:{key}"] for rect, key in zip(ch1.ASIDE_RECTS, ch1.ASIDE_KEYS)],
                "rooms": {key: dict(left=left, north=north, owner=owner, label=f"old:{label}")
                          for key, (left, north, owner, label) in ch1.ROOMS.items()}, "unchanged": [12, 34]}
        self.layout.write_bytes(('[gd_resource type="Resource" format=3]\r\n\r\n[resource]\r\nmetadata/data = '
                                 + json.dumps(data, ensure_ascii=False, indent=3) + "\r\n").encode())
        self.keys = list(ch2.rooms.TEXT_SITES)
        self.text = {k: f"new:{k}" for key in self.keys
                     for k in (ch2.title_key(key), *(ch2.label_key(key, s) for s in ch2.rooms.TEXT_SITES[key]))}
        self.compiled = self.root / "compiled.json"
        write_json(self.compiled, {"chapters": [{"id": "chapter2", "design": {"text": self.text}}]})
        self.scenes = self.root / "scenes"
        self.scenes.mkdir()
        self.manifest = self.root / "manifest.json"
        entries = {}
        for key, sites in ch2.rooms.TEXT_SITES.items():
            lines = ['[gd_scene format=3]', f'[node name="{key}" type="Node2D"]',
                     f'metadata/title = "old:{ch2.title_key(key)}"', 'metadata/map_size = Vector2(77, 99)',
                     '[node name="Sites" type="Node2D" parent="."]']
            entries[key] = {"title": f"old:{ch2.title_key(key)}", "geometry": [77, 99], "sites": []}
            for site in reversed(sites):
                binding = ch2.label_key(key, site)
                lines += [f'[node name="{site}" type="Marker2D" parent="Sites"]', 'position = Vector2(23, 45)',
                          f'metadata/label = "old:{binding}"']
                entries[key]["sites"].append({"id": site, "point": [23, 45], "label": f"old:{binding}"})
            lines += ['[node name="Tail" type="Node2D" parent="."]', 'position = Vector2(56, 78)', '']
            (self.scenes / f"{key}.tscn").write_bytes("\r\n".join(lines).encode())
        entries["unselected"] = {"title": "untouched", "sites": []}
        write_json(self.manifest, {"rooms": entries, "untouched": "\\u1234"})
        self.outputs = [self.layout, self.manifest, *self.scenes.glob("*.tscn")]

    def run_ch1(self):
        return ch1.update_text_only(self.root, self.layout)

    def run_ch2(self, keys=None):
        return ch2.update_text_only(self.keys if keys is None else keys, self.compiled, self.scenes, self.manifest)

    def reject(self, action):
        before = {path: path.read_bytes() for path in self.outputs}
        with self.assertRaises(ValueError):
            action()
        self.assertEqual(before, {path: path.read_bytes() for path in self.outputs})

    def mutate_json(self, path, action):
        data = json.loads(path.read_bytes())
        action(data)
        write_json(path, data)

    def test_lossless_success_and_repeat(self):
        before = {path: path.read_bytes() for path in self.outputs}
        special = 'new:quote" slash\\ newline\n中文'
        self.asides[ch1.ASIDE_KEYS[0]] = special
        self.text[ch2.title_key(self.keys[-1])] = special
        write_json(self.layout_json, {"furniture_asides": self.asides})
        write_json(self.compiled, {"chapters": [{"id": "chapter2", "design": {"text": self.text}}]})
        with patch.object(ch1, "plan", side_effect=AssertionError("text-only built geometry")), \
                patch.object(ch2.rooms, "all_rooms", side_effect=AssertionError("text-only built rooms")):
            self.assertEqual(1, self.run_ch1())
            self.assertEqual(15, self.run_ch2())
            self.assertEqual(0, self.run_ch1())
            self.assertEqual(0, self.run_ch2())
        for path, raw in before.items():
            expected = raw
            for key, value in {**self.asides, **self.labels, **self.text}.items():
                expected = expected.replace(json.dumps(f"old:{key}", ensure_ascii=False).encode(),
                                            json.dumps(value, ensure_ascii=False).encode())
            self.assertEqual(expected, path.read_bytes(), path.name)

    def test_ch1_missing_wrong_type_unknown_and_duplicate_text(self):
        for kind in ("missing", "type", "unknown", "duplicate"):
            with self.subTest(kind=kind):
                table = dict(self.asides)
                if kind == "missing":
                    del table[ch1.ASIDE_KEYS[-1]]
                elif kind == "type":
                    table[ch1.ASIDE_KEYS[-1]] = []
                elif kind == "unknown":
                    table["map.awakening_yi.unknown"] = "neutral"
                write_json(self.layout_json, {"furniture_asides": table})
                if kind == "duplicate":
                    raw = self.layout_json.read_bytes()
                    token = json.dumps(ch1.ASIDE_KEYS[-1]).encode()
                    self.layout_json.write_bytes(raw.replace(b'"furniture_asides": {', b'"furniture_asides": {' + token + b': "duplicate",', 1))
                self.reject(self.run_ch1)

    def test_ch1_room_and_slot_validation(self):
        original = self.layout.read_bytes()
        for kind in ("unknown_room", "missing_slot", "wrong_type", "geometry", "duplicate_room"):
            with self.subTest(kind=kind):
                self.layout.write_bytes(original)
                prefix, body = original.decode().split("metadata/data = ", 1)
                data = json.loads(body)
                if kind == "unknown_room":
                    data["rooms"]["unknown"] = data["rooms"].pop("R06")
                elif kind == "missing_slot":
                    data["asides"].pop()
                elif kind == "wrong_type":
                    data["rooms"]["R06"]["label"] = 9
                elif kind == "geometry":
                    data["asides"][-1][0][0] += 32
                raw = json.dumps(data)
                if kind == "duplicate_room":
                    raw = raw.replace('"rooms": {', '"rooms": {"R06": {},', 1)
                self.layout.write_bytes((prefix + "metadata/data = " + raw).encode())
                self.reject(self.run_ch1)

    def test_ch2_invalid_map_batches(self):
        for keys in ([], [self.keys[0], "unknown"], [self.keys[0], "campus_n"], [self.keys[0], self.keys[0]]):
            with self.subTest(keys=keys):
                self.reject(lambda: self.run_ch2(keys))

    def test_ch2_text_failures(self):
        for kind in ("missing", "type", "unknown_map", "unknown_site", "duplicate", "duplicate_chapter"):
            with self.subTest(kind=kind):
                table = dict(self.text)
                key = ch2.title_key(self.keys[-1])
                if kind == "missing":
                    del table[key]
                elif kind == "type":
                    table[key] = {}
                elif kind in ("unknown_map", "unknown_site"):
                    table["资源.地图.unknown.标题" if kind == "unknown_map" else "资源.场点.greenhouse.unknown.标签"] = "neutral"
                chapter = {"id": "chapter2", "design": {"text": table}}
                write_json(self.compiled, {"chapters": [chapter, chapter] if kind == "duplicate_chapter" else [chapter]})
                if kind == "duplicate":
                    token = json.dumps(key, ensure_ascii=False).encode()
                    self.compiled.write_bytes(self.compiled.read_bytes().replace(b'"text": {', b'"text": {' + token + b': "duplicate",', 1))
                self.reject(self.run_ch2)

    def test_ch2_late_scene_failure_does_not_write_earlier_map(self):
        path = self.scenes / f"{self.keys[-1]}.tscn"
        original = path.read_bytes()
        for old, new in ((b'name="gauge"', b'name="unknown"'), (b'name="gauge"', b'name="valve"'),
                         (b'metadata/label = "old:', b'metadata/other = "old:'),
                         (b'metadata/label = "old:', b'metadata/label = 17\r\nmetadata/other = "old:'),
                         (b'metadata/title = ', b'metadata/title = "duplicate"\r\nmetadata/title = ')):
            with self.subTest(new=new):
                path.write_bytes(original.replace(old, new, 1))
                self.reject(self.run_ch2)

    def test_ch2_late_manifest_failure_does_not_write_scenes(self):
        original = self.manifest.read_bytes()
        for kind in ("missing", "unknown_site", "duplicate_site", "type"):
            with self.subTest(kind=kind):
                self.manifest.write_bytes(original)
                def mutate(data):
                    entry = data["rooms"][self.keys[-1]]
                    if kind == "missing":
                        del data["rooms"][self.keys[-1]]
                    elif kind == "unknown_site":
                        entry["sites"][-1]["id"] = "unknown"
                    elif kind == "duplicate_site":
                        entry["sites"].append(copy.deepcopy(entry["sites"][0]))
                    else:
                        entry["sites"][-1]["label"] = None
                self.mutate_json(self.manifest, mutate)
                self.reject(self.run_ch2)

    def test_geometry_does_not_select_text_and_keys_do_not_leak(self):
        room = ch1.plan()[0]
        rows = ["...."]
        text = (self.asides, self.labels)
        first = ch1.layout_data(room, rows, {"sconces": []}, text)
        moved = copy.deepcopy(room)
        moved["decor"] = [(d[0], d[1] + 2048, *d[2:]) if d[0] == "window" else d for d in moved["decor"]]
        moved["floor_work"] = [(d[0], d[1] - 2048, *d[2:]) for d in moved["floor_work"]]
        second = ch1.layout_data(moved, rows, {"sconces": []}, text)
        self.assertEqual([e[1] for e in first["asides"]], [e[1] for e in second["asides"]])
        for recipe in ch2.rooms.all_rooms():
            projected = ch2.resolve_room_text(recipe, self.text)
            self.assertNotIn("title_key", projected)
            self.assertTrue(all(set(s) == {"id", "point", "label"} for s in projected["sites"]))

    def test_sites_only_uses_compiled_labels(self):
        key = self.keys[-1]
        self.assertEqual(2, ch2.update_sites_only([key], self.compiled, self.scenes, self.manifest))
        manifest = json.loads(self.manifest.read_bytes())
        for site in manifest["rooms"][key]["sites"]:
            self.assertEqual(self.text[ch2.label_key(key, site["id"])], site["label"])
        self.assertEqual(f"old:{ch2.title_key(key)}", manifest["rooms"][key]["title"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
