import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';

const root = path.resolve(import.meta.dirname, '../..');
const html = fs.readFileSync(path.join(root, 'tools/web_building_editor/index.html'), 'utf8');
const mesh = html.match(/^    const MeshKit = \{[\s\S]*?^    \};/m)?.[0];
assert.ok(mesh, 'MeshKit source was not found');
function source(name) {
  const start = html.indexOf(`    function ${name}(`);
  const end = html.indexOf('\n    }\n', start);
  assert.ok(start !== -1 && end > start, `${name} source was not found`);
  return html.slice(start, end + 6);
}
const values = {
  'wall-h-val': { value: '3.6' },
  'wall-t-val': { value: '0.28' },
  'wall-outset-val': { value: '0.4' },
  'wall-style-select': { value: 'stone' },
  'wall-has-pillars': { checked: true }
};
const messages = [];
const context = vm.createContext({
  document: { getElementById: id => values[id] },
  floorLayers: [{ id: 'test-layer', name: 'Two rooms', y: 2, polygons: [
    [[0, 0], [0, 4], [6, 4], [6, 0]],
    [[7, 0], [7, 3], [10, 3], [10, 0]]
  ] }],
  activeLayerId: 'test-layer', wallPaths: [{ id: 'manual', points: [] }], propsData: [],
  uid: (() => { let id = 0; return tag => `${tag}-${++id}`; })(),
  showToast: message => messages.push(message),
  rebuildAll3D: () => {}, updateInspectorUI: () => {},
  updateSceneOutlineUI: () => {}, saveHistorySnapshot: () => {}
});
vm.runInContext([mesh, source('getActiveLayer'), source('autoBuildWallFromActiveFloor'), source('buildGodotTscn')].join('\n'), context);
values['wall-outset-val'].value = 'invalid';
vm.runInContext('autoBuildWallFromActiveFloor()', context);
assert.equal(context.wallPaths.length, 1, 'invalid input must not alter the drawing');
values['wall-outset-val'].value = '0.4';
vm.runInContext('autoBuildWallFromActiveFloor()', context);
assert.equal(context.wallPaths.length, 3, 'both floor polygons must produce walls');
vm.runInContext('autoBuildWallFromActiveFloor()', context);
assert.equal(context.wallPaths.length, 3, 'repeat must replace only generated walls');
assert.equal(context.wallPaths[0].id, 'manual', 'hand-drawn wall must survive');
vm.runInContext('autoBuildWallFromActiveFloor(true)', context);
const exported = vm.runInContext('buildGodotTscn()', context);
assert.equal((exported.match(/type="CSGPolygon3D"/g) || []).length, 2);
assert.equal((exported.match(/script = ExtResource\("1_wall"\)/g) || []).length, 8);
assert.ok(exported.includes('openings = Array[Dictionary]('), 'doors/windows must survive export');
assert.ok(exported.includes('use_collision = true'), 'floors need physical collision');
const target = path.join(root, '.runtime/verified-building.tscn');
fs.mkdirSync(path.dirname(target), { recursive: true });
fs.writeFileSync(target, exported);
console.log(JSON.stringify({ passed: true, polygons: 2, walls: 8, authored_wall_preserved: true,
  invalid_input_rejected: true, generated_scene: target, notices: messages.slice(0, 2) }));
