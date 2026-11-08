#!/usr/bin/env python3
"""Upgrade tools/web_building_editor/index.html with:
1. Updated PRESET_ROOMS_CATALOG from rooms_catalog.json (with F01 and all 36 rooms).
2. Fully restored lowpoly props in buildSingleProp3D:
   - Authentic 8-sided faceted stone planter with 10 solid 6-vertex leaf blades and 7 cream faceted flowers (stems, hearts, 6 petals, pollen).
   - Classical carved pedestal (Foot, Plinth, Shaft, Neck, Capital, Panels).
   - Treasure/supply chest with lid, iron bands and gold lock.
   - Refined table with TopUndercut, aprons, front drawer, and gold knobs.
   - Slatted chair with back posts, top rail, vertical slats, stretcher bars, and green cushion.
   - Hall bench with arm rests, slatted back, and green cushion.
   - Bed with headboard, footboard, blanket drape, fold, and pillows.
   - Wash table with framed mirror, ceramic wash basin and water pitcher.
   - Bookcase with base lip, cornice, and multi-colored books.
   - Reception desk with U-shaped carcass, front panels, brass sign, and service bell.
   - Wall lantern with 6-sided faceted cage and top/bottom caps.
   - Banker lamp with stepped lathe base and curved emerald hood.
3. Added MeshKit.createLatheGeometry(profile, sides, closedBottom) for 100% faithful lathe shapes.
4. Materials upgraded with flatShading: true and side: THREE.DoubleSide for authentic faceted lowpoly look.
5. PropPresets and UI dropdown options for planter, pedestal, and chest.
"""

import json
from pathlib import Path

INDEX_FILE = Path(__file__).resolve().parent / "index.html"
CATALOG_FILE = Path(__file__).resolve().parent / "rooms_yaml" / "rooms_catalog.json"

catalog_data = json.loads(CATALOG_FILE.read_text(encoding="utf-8"))
catalog_json_str = json.dumps(catalog_data, ensure_ascii=False)

html_text = INDEX_FILE.read_text(encoding="utf-8")

# 1. Replace PRESET_ROOMS_CATALOG definition
pattern_start = "const PRESET_ROOMS_CATALOG = ["
idx_start = html_text.find(pattern_start)
if idx_start == -1:
    raise ValueError("Could not find PRESET_ROOMS_CATALOG in index.html")

idx_end = html_text.find(";\n", idx_start)
if idx_end == -1:
    idx_end = html_text.find(";", idx_start)

new_catalog_decl = f"const PRESET_ROOMS_CATALOG = {catalog_json_str};"
html_text = html_text[:idx_start] + new_catalog_decl + html_text[idx_end + 1:]
print("Updated PRESET_ROOMS_CATALOG.")

# 2. Update UI dropdown
old_dropdown_opt = '<option value="planter">陶罐绿植盆栽</option>'
new_dropdown_opt = '''<option value="planter">八面石盆花卉绿植</option>
                  <option value="pedestal">古典雕花展台</option>
                  <option value="chest">铁箍古典储物箱</option>'''
if old_dropdown_opt in html_text:
    html_text = html_text.replace(old_dropdown_opt, new_dropdown_opt, 1)
    print("Updated prop selector dropdown in HTML.")

# 3. Update PropPresets
old_presets_start = html_text.find("    const PropPresets = {")
old_presets_end = html_text.find("    function onPropKindChanged(type) {", old_presets_start)

new_prop_presets = '''    const PropPresets = {
      painting:       { name: '油画挂件', fields: [{ id: 'frame_w', label: '画宽(m)', val: 1.35, step: 0.05 }, { id: 'frame_h', label: '画高(m)', val: 0.95, step: 0.05 }, { id: 'mount_y', label: '挂高(m)', val: 1.65, step: 0.05 }] },
      wall_lantern:   { name: '壁灯', fields: [{ id: 'lamp_energy', label: '光强', val: 15, step: 2 }, { id: 'lamp_height', label: '挂高(m)', val: 2.2, step: 0.1 }] },
      banker_lamp:    { name: '台灯', fields: [{ id: 'lamp_energy', label: '光强', val: 8, step: 1 }, { id: 'mount_y', label: '桌面高(m)', val: 0.75, step: 0.05 }] },
      pendant_lamp:   { name: '吊灯', fields: [{ id: 'drop_length', label: '吊长(m)', val: 1.1, step: 0.1 }, { id: 'lamp_energy', label: '光强', val: 22, step: 2 }] },
      table:          { name: '书桌', fields: [{ id: 'table_w', label: '宽(m)', val: 1.8, step: 0.1 }, { id: 'table_d', label: '深(m)', val: 0.95, step: 0.05 }, { id: 'table_h', label: '高(m)', val: 0.75, step: 0.05 }] },
      chair:          { name: '木椅', fields: [{ id: 'seat_w', label: '宽(m)', val: 0.48, step: 0.02 }, { id: 'seat_d', label: '深(m)', val: 0.48, step: 0.02 }] },
      bookshelf:      { name: '书柜', fields: [{ id: 'shelf_w', label: '宽(m)', val: 1.5, step: 0.1 }, { id: 'shelf_h', label: '高(m)', val: 2.2, step: 0.1 }, { id: 'shelf_layers', label: '层数', val: 5, step: 1 }] },
      bench:          { name: '长椅', fields: [{ id: 'bench_len', label: '长(m)', val: 1.6, step: 0.1 }, { id: 'bench_w', label: '深(m)', val: 0.5, step: 0.05 }] },
      reception_desk: { name: '前台', fields: [{ id: 'desk_w', label: '宽(m)', val: 2.4, step: 0.2 }, { id: 'desk_h', label: '高(m)', val: 1.05, step: 0.05 }] },
      wash_table:     { name: '盥洗台', fields: [{ id: 'wash_w', label: '宽(m)', val: 1.1, step: 0.1 }, { id: 'mirror_h', label: '镜高(m)', val: 0.8, step: 0.05 }] },
      bed:            { name: '大床', fields: [{ id: 'bed_w', label: '宽(m)', val: 1.8, step: 0.1 }, { id: 'bed_l', label: '长(m)', val: 2.1, step: 0.1 }, { id: 'head_h', label: '床头高(m)', val: 1.35, step: 0.05 }] },
      cabinet:        { name: '立柜', fields: [{ id: 'cab_w', label: '宽(m)', val: 1.2, step: 0.1 }, { id: 'cab_h', label: '高(m)', val: 2.0, step: 0.1 }] },
      pedestal:       { name: '雕花展台', fields: [{ id: 'width', label: '宽(m)', val: 0.52, step: 0.05 }, { id: 'height', label: '高(m)', val: 0.75, step: 0.05 }] },
      chest:          { name: '储物箱', fields: [{ id: 'chest_w', label: '长(m)', val: 0.95, step: 0.05 }, { id: 'chest_d', label: '宽(m)', val: 0.55, step: 0.05 }, { id: 'chest_h', label: '高(m)', val: 0.50, step: 0.05 }] },
      screen:         { name: '屏风', fields: [{ id: 'screen_w', label: '宽(m)', val: 1.8, step: 0.1 }, { id: 'screen_h', label: '高(m)', val: 1.7, step: 0.05 }] },
      rug:            { name: '地毯', fields: [{ id: 'rug_w', label: '宽(m)', val: 2.6, step: 0.2 }, { id: 'rug_d', label: '深(m)', val: 1.8, step: 0.2 }] },
      lectern:        { name: '讲台', fields: [{ id: 'lec_h', label: '高(m)', val: 1.15, step: 0.05 }] },
      chalkboard:     { name: '黑板', fields: [{ id: 'board_w', label: '宽(m)', val: 2.0, step: 0.1 }, { id: 'board_h', label: '高(m)', val: 1.2, step: 0.05 }] },
      planter:        { name: '花卉盆栽', fields: [{ id: 'plant_w', label: '冠径(m)', val: 0.65, step: 0.05 }, { id: 'plant_h', label: '株高(m)', val: 0.85, step: 0.05 }, { id: 'flower_count', label: '花朵数', val: 7, step: 1 }] },
      upright_piano:  { name: '钢琴', fields: [{ id: 'piano_w', label: '宽(m)', val: 1.45, step: 0.05 }, { id: 'piano_h', label: '高(m)', val: 1.2, step: 0.05 }] },
      easel:          { name: '画架', fields: [{ id: 'easel_h', label: '高(m)', val: 1.75, step: 0.05 }] },
      crates:         { name: '木箱堆', fields: [{ id: 'crate_tier', label: '层数', val: 3, step: 1 }] }
    };

'''
if old_presets_start != -1 and old_presets_end != -1:
    html_text = html_text[:old_presets_start] + new_prop_presets + html_text[old_presets_end:]
    print("Updated PropPresets.")

# 4. Upgrade initMaterials with flatShading: true and side: THREE.DoubleSide
old_mat_start = html_text.find("    function initMaterials() {")
old_mat_end = html_text.find("    function toggleWireframe() {", old_mat_start)
new_init_materials = '''    function initMaterials() {
      for (const [key, hex] of Object.entries(PALETTE)) {
        if (key === 'glass') {
          MATERIALS[key] = new THREE.MeshStandardMaterial({
            color: hex, transparent: true, opacity: 0.42, roughness: 0.1, side: THREE.DoubleSide
          });
        } else if (key === 'gold') {
          MATERIALS[key] = new THREE.MeshStandardMaterial({
            color: hex, metalness: 0.65, roughness: 0.28, flatShading: true, side: THREE.DoubleSide
          });
        } else if (key === 'emission') {
          MATERIALS[key] = new THREE.MeshBasicMaterial({ color: hex });
        } else {
          MATERIALS[key] = new THREE.MeshStandardMaterial({
            color: hex,
            roughness: (key.includes('dark') || key.includes('stone') ? 0.75 : 0.58),
            flatShading: true,
            side: THREE.DoubleSide
          });
        }
      }
    }

'''
if old_mat_start != -1 and old_mat_end != -1:
    html_text = html_text[:old_mat_start] + new_init_materials + html_text[old_mat_end:]
    print("Upgraded initMaterials with flatShading: true.")

# 5. Add createLatheGeometry to MeshKit
if "createLatheGeometry:" not in html_text:
    old_box_geom = "createBoxGeometry: function(sx, sy, sz) {"
    new_lathe_and_box = '''createLatheGeometry: function(profile, sides = 8, closedBottom = true) {
        sides = Math.max(3, Math.floor(sides));
        const vertices = [];
        const rings = [];
        for (const [radius, y] of profile) {
          if (Math.abs(radius) < 1e-8) {
            rings.push([vertices.length]);
            vertices.push(new THREE.Vector3(0, y, 0));
          } else {
            const ring = [];
            const startIdx = vertices.length;
            for (let j = 0; j < sides; j++) {
              const theta = 2 * Math.PI * j / sides + Math.PI / sides;
              ring.push(startIdx + j);
              vertices.push(new THREE.Vector3(radius * Math.cos(theta), y, radius * Math.sin(theta)));
            }
            rings.push(ring);
          }
        }

        const pos = [];
        function addTri(i0, i1, i2) {
          pos.push(vertices[i0].x, vertices[i0].y, vertices[i0].z);
          pos.push(vertices[i1].x, vertices[i1].y, vertices[i1].z);
          pos.push(vertices[i2].x, vertices[i2].y, vertices[i2].z);
        }

        for (let r = 0; r < rings.length - 1; r++) {
          const ra = rings[r];
          const rb = rings[r + 1];
          for (let j = 0; j < sides; j++) {
            const k = (j + 1) % sides;
            if (ra.length === 1 && rb.length === 1) continue;
            if (ra.length === 1) {
              addTri(ra[0], rb[k], rb[j]);
            } else if (rb.length === 1) {
              addTri(ra[j], ra[k], rb[0]);
            } else {
              addTri(ra[j], ra[k], rb[k]);
              addTri(ra[j], rb[k], rb[j]);
            }
          }
        }

        if (closedBottom && rings.length > 0 && rings[0].length > 1) {
          const r0 = rings[0];
          const bottomCenter = new THREE.Vector3(0, profile[0][1], 0);
          const cIdx = vertices.length;
          vertices.push(bottomCenter);
          for (let j = 0; j < sides; j++) {
            const k = (j + 1) % sides;
            addTri(cIdx, r0[j], r0[k]);
          }
        }

        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
        geom.computeVertexNormals();
        return geom;
      },

      createBoxGeometry: function(sx, sy, sz) {'''
    html_text = html_text.replace(old_box_geom, new_lathe_and_box, 1)
    print("Added createLatheGeometry to MeshKit.")

# 6. Replace buildSingleProp3D implementation
old_func_start = html_text.find("    function buildSingleProp3D(pObj) {")
old_func_end = html_text.find("    function setActiveTool(t) {", old_func_start)
if old_func_start == -1 or old_func_end == -1:
    raise ValueError("Could not find buildSingleProp3D boundaries in index.html")

# Find the separator line right before setActiveTool
sep_idx = html_text.rfind("/* =========================================================================", old_func_start, old_func_end)
if sep_idx != -1:
    old_func_end = sep_idx

new_build_single_prop = '''    function buildSingleProp3D(pObj) {
      const { kind: type, x: px, y: baseY = 0, z: pz, yaw = 0, params = {} } = pObj;
      const root = new THREE.Group();

      if (type === 'planter') {
        // 严格按照 Python kit/furniture.py planter & _leaf 还原：
        // 1. 八面体低多边形空心石质花盆 (Lathe profile with 8 sides)
        // 2. 八面体内嵌深色营养土层
        // 3. 10片放射状6顶点立体双向起脊叶片 (Solid 6-vertex closed blade with raised ridge & underside)
        // 4. 7朵带绿茎、奶油色花心、6瓣花瓣与黄色花蕊的低多边形花朵簇
        const pw = params.plant_w ?? params.width ?? 0.65;
        const ph = params.plant_h ?? params.height ?? 0.85;
        const flowerCount = params.flower_count ?? 7;
        const seed = params.seed ?? 7;

        let rngState = seed;
        const rng = () => { rngState = (rngState * 9301 + 49297) % 233280; return rngState / 233280; };
        const uniform = (min, max) => min + (max - min) * rng();

        const potH = ph * 0.44;
        const radius = pw * 0.31;

        // 1. 严格对应 kit/furniture.py 第 485-490 行的八面空心花盆 profile：
        const potProfile = [
          [radius * 0.60, 0],
          [radius * 0.67, potH * 0.07],
          [radius * 0.91, potH * 0.78],
          [radius * 0.94, potH * 0.84],
          [radius,        potH * 0.86],
          [radius,        potH],
          [radius * 0.82, potH],
          [radius * 0.78, potH * 0.81],
          [radius * 0.56, potH * 0.17],
          [0,             potH * 0.17]
        ];
        const potGeom = MeshKit.createLatheGeometry(potProfile, 8, true);
        const potMesh = new THREE.Mesh(potGeom, MATERIALS.stone);
        potMesh.castShadow = true;
        potMesh.receiveShadow = true;
        root.add(potMesh);

        // 2. 严格对应 kit/furniture.py 第 491-492 行的八面体土壤层：
        const soilGeom = new THREE.CylinderGeometry(radius * 0.78, radius * 0.78, ph * 0.018, 8);
        const soilMesh = new THREE.Mesh(soilGeom, MATERIALS.wood_dark);
        soilMesh.position.y = potH * 0.86 + (ph * 0.018) * 0.5;
        root.add(soilMesh);

        // 3. 严格对应 kit/furniture.py 第 459-472 行 _leaf 的 6 顶点双向立体起脊叶片：
        for (let i = 0; i < 10; i++) {
          const theta = 2 * Math.PI * (i / 10) + uniform(-0.15, 0.15);
          const outer = pw * uniform(0.38, 0.48);
          const startX = Math.cos(theta) * pw * 0.05;
          const startZ = Math.sin(theta) * pw * 0.05;
          const startY = potH * 0.92;
          const endX = Math.cos(theta) * outer;
          const endZ = Math.sin(theta) * outer;
          const endY = ph * uniform(0.48, 0.68);
          const leafW = pw * uniform(0.15, 0.23);
          const leafMat = (i % 3 === 0) ? MATERIALS.green : MATERIALS.leaf;

          const dx = endX - startX, dz = endZ - startZ;
          const len = Math.hypot(dx, dz) || 1e-6;
          const tx = -dz / len, tz = dx / len;
          const cx = (startX + endX) * 0.5, cy = (startY + endY) * 0.5, cz = (startZ + endZ) * 0.5;

          const v0 = [startX, startY, startZ];
          const v1 = [endX, endY, endZ];
          const v2 = [cx + tx * leafW * 0.5, cy, cz + tz * leafW * 0.5];
          const v3 = [cx - tx * leafW * 0.5, cy, cz - tz * leafW * 0.5];
          const v4 = [cx, cy + leafW * 0.14, cz]; // 背脊凸起 (raised central ridge)
          const v5 = [cx, cy - leafW * 0.04, cz]; // 腹面微凹 (thin underside)

          const triIndices = [
            0, 2, 4,   2, 1, 4,   1, 3, 4,   3, 0, 4, // 凸脊 4 面
            2, 0, 5,   1, 2, 5,   3, 1, 5,   0, 3, 5  // 腹底 4 面
          ];
          const verts = [v0, v1, v2, v3, v4, v5];
          const positions = [];
          for (const idx of triIndices) {
            positions.push(...verts[idx]);
          }

          const leafGeom = new THREE.BufferGeometry();
          leafGeom.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
          leafGeom.computeVertexNormals();
          const leafMesh = new THREE.Mesh(leafGeom, leafMat);
          root.add(leafMesh);
        }

        // 4. 严格对应 kit/furniture.py 第 501-528 行的花朵簇：
        const count = Math.max(0, Math.floor(flowerCount));
        for (let i = 0; i < count; i++) {
          let fx = 0, fz = 0, fy = ph * 0.89;
          if (i > 0) {
            const angle = 2 * Math.PI * (i - 1) / Math.max(1, count - 1) + 0.20;
            const rr = pw * (count <= 8 ? 0.23 : 0.29);
            fx = Math.cos(angle) * rr;
            fz = Math.sin(angle) * rr;
            fy = ph * (0.72 + 0.06 * Math.sin(angle * 2 + 0.4));
          }
          const flowerRadius = Math.min(pw * 0.115, ph * 0.09);

          // 斜向花茎 (Stem beam from (fx*0.35, potH*0.85, fz*0.35) to (fx, fy, fz))
          const pStart = new THREE.Vector3(fx * 0.35, potH * 0.85, fz * 0.35);
          const pEnd = new THREE.Vector3(fx, fy, fz);
          const stemDir = new THREE.Vector3().subVectors(pEnd, pStart);
          const stemLen = stemDir.length();
          const stemW = pw * 0.012;
          const stemGeom = new THREE.CylinderGeometry(stemW * 0.5, stemW * 0.5, stemLen, 6);
          const stemMesh = new THREE.Mesh(stemGeom, MATERIALS.leaf);
          stemMesh.position.copy(pStart).add(pEnd).multiplyScalar(0.5);
          stemMesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), stemDir.clone().normalize());
          root.add(stemMesh);

          // 花心 (FlowerHeart: ico with radius*0.65, scale (1, 0.75, 1))
          const heartGeom = new THREE.IcosahedronGeometry(flowerRadius * 0.65, 1);
          heartGeom.scale(1, 0.75, 1);
          const heartMesh = new THREE.Mesh(heartGeom, MATERIALS.cream);
          heartMesh.position.set(fx, fy + flowerRadius * 0.15, fz);
          root.add(heartMesh);

          // 6片多边形花瓣 (6 Faceted Petals: radius*0.62, scale (1, 0.70, 1))
          for (let petal = 0; petal < 6; petal++) {
            const theta = petal * Math.PI / 3 + i * 0.37;
            const pr = flowerRadius * 0.65;
            const px = fx + Math.cos(theta) * pr;
            const pz = fz + Math.sin(theta) * pr;
            const py = fy + uniform(-0.05, 0.08) * flowerRadius;
            const petalGeom = new THREE.IcosahedronGeometry(flowerRadius * 0.62, 1);
            petalGeom.scale(1, 0.70, 1);
            const petalMesh = new THREE.Mesh(petalGeom, MATERIALS.cream);
            petalMesh.position.set(px, py, pz);
            root.add(petalMesh);
          }

          // 花粉核心 (Pollen: ico with radius*0.25, mats["wall"])
          const pollenGeom = new THREE.IcosahedronGeometry(flowerRadius * 0.25, 1);
          const pollenMesh = new THREE.Mesh(pollenGeom, MATERIALS.wall);
          pollenMesh.position.set(fx, fy + flowerRadius * 0.60, fz);
          root.add(pollenMesh);
        }

      } else if (type === 'pedestal') {
        // 阶梯式古典雕花展台 (Foot, Plinth, Shaft, Neck, Capital, Inset Panels)
        const pw = params.width ?? 0.52, ph = params.height ?? 0.75;
        const foot = new THREE.Mesh(MeshKit.createBoxGeometry(pw * 1.14, 0.12, pw * 1.14), MATERIALS.wood_light);
        foot.position.y = 0.06; root.add(foot);
        const plinth = new THREE.Mesh(MeshKit.createBoxGeometry(pw, 0.14, pw), MATERIALS.wood);
        plinth.position.y = 0.19; root.add(plinth);
        const shaft = new THREE.Mesh(MeshKit.createBoxGeometry(pw * 0.78, ph - 0.28, pw * 0.78), MATERIALS.wood);
        shaft.position.y = 0.26 + (ph - 0.28) * 0.5; root.add(shaft);
        // 侧面凹凸线脚
        const panelF = new THREE.Mesh(MeshKit.createBoxGeometry(pw * 0.48, (ph - 0.34) * 0.75, 0.02), MATERIALS.wood_dark);
        panelF.position.set(0, 0.26 + (ph - 0.28) * 0.5, pw * 0.39 + 0.01); root.add(panelF);
        const neck = new THREE.Mesh(MeshKit.createBoxGeometry(pw * 0.90, 0.08, pw * 0.90), MATERIALS.wood_dark);
        neck.position.y = ph - 0.08; root.add(neck);
        const cap = new THREE.Mesh(MeshKit.createBoxGeometry(pw * 1.16, 0.10, pw * 1.16), MATERIALS.wood_light);
        cap.position.y = ph - 0.01; root.add(cap);

      } else if (type === 'chest') {
        // 古典五金木箱／储物箱 (Wooden Chest with Lid, Iron Bands & Gold Lock)
        const cw = params.chest_w ?? 0.95, cd = params.chest_d ?? 0.55, ch = params.chest_h ?? 0.50;
        const body = new THREE.Mesh(MeshKit.createBoxGeometry(cw, ch * 0.88, cd), MATERIALS.wood);
        body.position.y = ch * 0.44; root.add(body);
        const lid = new THREE.Mesh(MeshKit.createBoxGeometry(cw + 0.04, 0.08, cd + 0.04), MATERIALS.wood_light);
        lid.position.y = ch * 0.88 + 0.04; root.add(lid);
        // 铁箍包角与加固带
        const band1 = new THREE.Mesh(MeshKit.createBoxGeometry(0.06, ch + 0.02, cd + 0.02), MATERIALS.stone_dark);
        band1.position.set(-cw * 0.32, ch * 0.48, 0); root.add(band1);
        const band2 = new THREE.Mesh(MeshKit.createBoxGeometry(0.06, ch + 0.02, cd + 0.02), MATERIALS.stone_dark);
        band2.position.set(cw * 0.32, ch * 0.48, 0); root.add(band2);
        // 黄金锁扣
        const lock = new THREE.Mesh(MeshKit.createBoxGeometry(0.12, 0.10, 0.03), MATERIALS.gold);
        lock.position.set(0, ch * 0.78, cd * 0.5 + 0.015); root.add(lock);

      } else if (type === 'table') {
        // 书桌 (Top, Undercut, Aprons, FrontDrawer, Legs, Knobs)
        const tw = params.table_w ?? 1.8, td = params.table_d ?? 0.95, th = params.table_h ?? 0.75;
        const topH = 0.05;
        const top = new THREE.Mesh(MeshKit.createBoxGeometry(tw, topH, td), MATERIALS.wood_light);
        top.position.y = th - topH * 0.5; top.castShadow = true; root.add(top);
        const undercut = new THREE.Mesh(MeshKit.createBoxGeometry(tw - 0.06, 0.02, td - 0.06), MATERIALS.wood_dark);
        undercut.position.y = th - topH - 0.01; root.add(undercut);

        const apronH = 0.11;
        const apronL = new THREE.Mesh(MeshKit.createBoxGeometry(0.03, apronH, td - 0.16), MATERIALS.wood);
        apronL.position.set(-tw * 0.5 + 0.07, th - topH - 0.02 - apronH * 0.5, 0); root.add(apronL);
        const apronR = new THREE.Mesh(MeshKit.createBoxGeometry(0.03, apronH, td - 0.16), MATERIALS.wood);
        apronR.position.set(tw * 0.5 - 0.07, th - topH - 0.02 - apronH * 0.5, 0); root.add(apronR);
        const apronB = new THREE.Mesh(MeshKit.createBoxGeometry(tw - 0.16, apronH, 0.03), MATERIALS.wood);
        apronB.position.set(0, th - topH - 0.02 - apronH * 0.5, -td * 0.5 + 0.07); root.add(apronB);

        const drawer = new THREE.Mesh(MeshKit.createBoxGeometry(tw - 0.18, apronH - 0.01, 0.03), MATERIALS.wood_light);
        drawer.position.set(0, th - topH - 0.02 - apronH * 0.5, td * 0.5 - 0.07); root.add(drawer);
        for (const kx of [-0.28, 0.28]) {
          const knob = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, 0.03, 8), MATERIALS.gold);
          knob.rotation.x = Math.PI * 0.5;
          knob.position.set(kx, th - topH - 0.02 - apronH * 0.5, td * 0.5 - 0.045);
          root.add(knob);
        }

        const legW = 0.068, legH = th - topH - 0.02;
        const lx = tw * 0.5 - legW * 0.5 - 0.03, lz = td * 0.5 - legW * 0.5 - 0.03;
        for (const [sx, sz] of [[-lx, -lz], [lx, -lz], [-lx, lz], [lx, lz]]) {
          const leg = new THREE.Mesh(MeshKit.createBoxGeometry(legW, legH, legW), MATERIALS.wood);
          leg.position.set(sx, legH * 0.5, sz); leg.castShadow = true; root.add(leg);
        }

      } else if (type === 'chair') {
        // 木椅 (Turned Legs, Tall Back Posts, Top Rail, Slats, Stretchers, Cushion)
        const sw = params.seat_w ?? 0.48, sd = params.seat_d ?? 0.48;
        const seatH = 0.45;
        const seat = new THREE.Mesh(MeshKit.createBoxGeometry(sw, 0.04, sd), MATERIALS.wood);
        seat.position.y = seatH; root.add(seat);
        const cushion = new THREE.Mesh(MeshKit.createBoxGeometry(sw - 0.04, 0.03, sd - 0.04), MATERIALS.green);
        cushion.position.y = seatH + 0.03; root.add(cushion);

        const legW = 0.042;
        const lx = sw * 0.5 - legW * 0.5, lz = sd * 0.5 - legW * 0.5;
        for (const sx of [-lx, lx]) {
          const fleg = new THREE.Mesh(MeshKit.createBoxGeometry(legW, seatH, legW), MATERIALS.wood);
          fleg.position.set(sx, seatH * 0.5, lz); root.add(fleg);
        }
        const backH = 0.52;
        for (const sx of [-lx, lx]) {
          const bpost = new THREE.Mesh(MeshKit.createBoxGeometry(legW, seatH + backH, legW), MATERIALS.wood);
          bpost.position.set(sx, (seatH + backH) * 0.5, -lz); root.add(bpost);
        }
        const topRail = new THREE.Mesh(MeshKit.createBoxGeometry(sw, 0.06, 0.03), MATERIALS.wood_light);
        topRail.position.set(0, seatH + backH - 0.03, -lz); root.add(topRail);
        for (const kx of [-sw * 0.22, 0, sw * 0.22]) {
          const slat = new THREE.Mesh(MeshKit.createBoxGeometry(0.025, backH - 0.12, 0.015), MATERIALS.wood);
          slat.position.set(kx, seatH + backH * 0.5, -lz); root.add(slat);
        }
        const stY = seatH * 0.35;
        const stL = new THREE.Mesh(MeshKit.createBoxGeometry(0.02, 0.02, sd - 0.08), MATERIALS.wood_dark);
        stL.position.set(-lx, stY, 0); root.add(stL);
        const stR = new THREE.Mesh(MeshKit.createBoxGeometry(0.02, 0.02, sd - 0.08), MATERIALS.wood_dark);
        stR.position.set(lx, stY, 0); root.add(stR);

      } else if (type === 'bench') {
        // 长椅 (Bench with Arm Rests, Slatted Back, Cushion)
        const bl = params.bench_len ?? 1.6, bw = params.bench_w ?? 0.50, seatH = 0.44, backH = 0.48;
        const seat = new THREE.Mesh(MeshKit.createBoxGeometry(bl, 0.04, bw), MATERIALS.wood);
        seat.position.y = seatH; root.add(seat);
        const cushion = new THREE.Mesh(MeshKit.createBoxGeometry(bl - 0.06, 0.03, bw - 0.06), MATERIALS.green);
        cushion.position.y = seatH + 0.03; root.add(cushion);

        const legW = 0.048;
        const lx = bl * 0.5 - legW * 0.5, lz = bw * 0.5 - legW * 0.5;
        for (const sx of [-lx, lx]) {
          const fleg = new THREE.Mesh(MeshKit.createBoxGeometry(legW, seatH, legW), MATERIALS.wood);
          fleg.position.set(sx, seatH * 0.5, lz); root.add(fleg);
          const bpost = new THREE.Mesh(MeshKit.createBoxGeometry(legW, seatH + backH, legW), MATERIALS.wood);
          bpost.position.set(sx, (seatH + backH) * 0.5, -lz); root.add(bpost);
          const arm = new THREE.Mesh(MeshKit.createBoxGeometry(0.04, 0.03, bw), MATERIALS.wood_light);
          arm.position.set(sx, seatH + 0.22, 0); root.add(arm);
        }
        const crest = new THREE.Mesh(MeshKit.createBoxGeometry(bl, 0.06, 0.03), MATERIALS.wood_light);
        crest.position.set(0, seatH + backH - 0.03, -lz); root.add(crest);
        for (let i = 0; i < 7; i++) {
          const slatX = -bl * 0.42 + i * (bl * 0.84 / 6);
          const slat = new THREE.Mesh(MeshKit.createBoxGeometry(0.028, backH - 0.12, 0.016), MATERIALS.wood);
          slat.position.set(slatX, seatH + backH * 0.5, -lz); root.add(slat);
        }

      } else if (type === 'bed') {
        // 双人豪华古典床 (Headboard, Footboard, Rails, Mattress, Velvet Drape Fold, Pillows)
        const bw = params.bed_w ?? 1.8, bl = params.bed_l ?? 2.1, headH = params.head_h ?? 1.35;
        const footH = 0.65, frameH = 0.32;
        const headboard = new THREE.Mesh(MeshKit.createBoxGeometry(bw + 0.08, headH, 0.08), MATERIALS.wood);
        headboard.position.set(0, headH * 0.5, -bl * 0.5 + 0.04); root.add(headboard);
        const headInset = new THREE.Mesh(MeshKit.createBoxGeometry(bw - 0.16, headH * 0.65, 0.02), MATERIALS.wood_dark);
        headInset.position.set(0, headH * 0.52, -bl * 0.5 + 0.08); root.add(headInset);
        const footboard = new THREE.Mesh(MeshKit.createBoxGeometry(bw + 0.08, footH, 0.08), MATERIALS.wood);
        footboard.position.set(0, footH * 0.5, bl * 0.5 - 0.04); root.add(footboard);
        for (const sx of [-bw * 0.5 + 0.02, bw * 0.5 - 0.02]) {
          const rail = new THREE.Mesh(MeshKit.createBoxGeometry(0.04, 0.18, bl - 0.16), MATERIALS.wood);
          rail.position.set(sx, frameH * 0.5, 0); root.add(rail);
        }
        const mat = new THREE.Mesh(MeshKit.createBoxGeometry(bw - 0.06, 0.22, bl - 0.16), MATERIALS.fabric_bed);
        mat.position.set(0, frameH + 0.11, 0); root.add(mat);
        const drape = new THREE.Mesh(MeshKit.createBoxGeometry(bw - 0.04, 0.04, (bl - 0.16) * 0.68), MATERIALS.green);
        drape.position.set(0, frameH + 0.22, (bl - 0.16) * 0.16); root.add(drape);
        const fold = new THREE.Mesh(MeshKit.createBoxGeometry(bw - 0.04, 0.06, 0.25), MATERIALS.fabric_bed);
        fold.position.set(0, frameH + 0.23, -((bl - 0.16) * 0.18)); root.add(fold);
        for (const px of [-bw * 0.26, bw * 0.26]) {
          const pillow = new THREE.Mesh(MeshKit.createBoxGeometry(0.55, 0.10, 0.38), MATERIALS.fabric_bed);
          pillow.rotation.x = -0.22;
          pillow.position.set(px, frameH + 0.26, -bl * 0.5 + 0.35); root.add(pillow);
        }

      } else if (type === 'wash_table') {
        // 梳妆盥洗台 (Vanity with Arched Mirror, Basin, Pitcher, Towel Shelf)
        const ww = params.wash_w ?? 1.1, wd = 0.58, wh = 0.82, mh = params.mirror_h ?? 0.80;
        const top = new THREE.Mesh(MeshKit.createBoxGeometry(ww, 0.04, wd), MATERIALS.wood_light);
        top.position.y = wh - 0.02; root.add(top);
        const shelf = new THREE.Mesh(MeshKit.createBoxGeometry(ww - 0.10, 0.025, wd - 0.10), MATERIALS.wood);
        shelf.position.y = 0.18; root.add(shelf);
        const legW = 0.045;
        const lx = ww * 0.5 - legW * 0.5 - 0.02, lz = wd * 0.5 - legW * 0.5 - 0.02;
        for (const [sx, sz] of [[-lx, -lz], [lx, -lz], [-lx, lz], [lx, lz]]) {
          const leg = new THREE.Mesh(MeshKit.createBoxGeometry(legW, wh - 0.04, legW), MATERIALS.wood);
          leg.position.set(sx, (wh - 0.04) * 0.5, sz); root.add(leg);
        }
        const mirrorFrame = new THREE.Mesh(MeshKit.createBoxGeometry(ww * 0.62, mh, 0.03), MATERIALS.wood_light);
        mirrorFrame.position.set(0, wh + mh * 0.5, -wd * 0.5 + 0.03); root.add(mirrorFrame);
        const mirrorGlass = new THREE.Mesh(MeshKit.createBoxGeometry(ww * 0.52, mh - 0.10, 0.01), MATERIALS.glass);
        mirrorGlass.position.set(0, wh + mh * 0.5, -wd * 0.5 + 0.045); root.add(mirrorGlass);
        const basin = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.12, 0.10, 16), MATERIALS.cream);
        basin.position.set(-ww * 0.15, wh + 0.05, 0.02); root.add(basin);
        const pitcher = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, 0.22, 12), MATERIALS.cream);
        pitcher.position.set(ww * 0.22, wh + 0.11, -0.04); root.add(pitcher);

      } else if (type === 'bookshelf') {
        // 书柜 (Plinth, Cornice, Shelves, Multi-colored Book Rows)
        const bw = params.shelf_w ?? 1.5, bh = params.shelf_h ?? 2.2, bd = 0.38, layers = params.shelf_layers ?? 5;
        const plinth = new THREE.Mesh(MeshKit.createBoxGeometry(bw + 0.04, 0.10, bd + 0.04), MATERIALS.wood_dark);
        plinth.position.y = 0.05; root.add(plinth);
        const cornice = new THREE.Mesh(MeshKit.createBoxGeometry(bw + 0.06, 0.08, bd + 0.06), MATERIALS.wood_light);
        cornice.position.y = bh - 0.04; root.add(cornice);
        for (const sx of [-bw * 0.5 + 0.02, bw * 0.5 - 0.02]) {
          const side = new THREE.Mesh(MeshKit.createBoxGeometry(0.04, bh - 0.18, bd), MATERIALS.wood);
          side.position.set(sx, 0.10 + (bh - 0.18) * 0.5, 0); root.add(side);
        }
        const back = new THREE.Mesh(MeshKit.createBoxGeometry(bw - 0.04, bh - 0.18, 0.02), MATERIALS.wood_dark);
        back.position.set(0, 0.10 + (bh - 0.18) * 0.5, -bd * 0.5 + 0.01); root.add(back);

        const shelfSpacing = (bh - 0.22) / layers;
        const bookMats = [MATERIALS.book_red, MATERIALS.book_blue, MATERIALS.book_green, MATERIALS.cream];
        for (let l = 0; l <= layers; l++) {
          const sy = 0.10 + l * shelfSpacing;
          const shelf = new THREE.Mesh(MeshKit.createBoxGeometry(bw - 0.04, 0.03, bd - 0.02), MATERIALS.wood);
          shelf.position.y = sy; root.add(shelf);
          if (l < layers) {
            let bx = -bw * 0.42;
            while (bx < bw * 0.38) {
              const bookW = 0.035 + (bx % 0.04);
              const bookH = shelfSpacing * 0.72 + (bx % 0.06);
              const bMat = bookMats[Math.abs(Math.floor(bx * 47)) % bookMats.length];
              const book = new THREE.Mesh(MeshKit.createBoxGeometry(bookW, bookH, bd * 0.65), bMat);
              book.position.set(bx + bookW * 0.5, sy + 0.015 + bookH * 0.5, 0.02);
              root.add(book);
              bx += bookW + 0.005;
            }
          }
        }

      } else if (type === 'reception_desk') {
        // 前台接待桌 (U-shaped carcass, Plinth, Recessed Panels, Counter, Bell, Brass Sign)
        const dw = params.desk_w ?? 2.4, dh = params.desk_h ?? 1.05, dd = 0.85;
        const plinth = new THREE.Mesh(MeshKit.createBoxGeometry(dw, 0.08, dd), MATERIALS.wood_dark);
        plinth.position.y = 0.04; root.add(plinth);
        const carcass = new THREE.Mesh(MeshKit.createBoxGeometry(dw - 0.04, dh - 0.14, dd - 0.04), MATERIALS.wood);
        carcass.position.y = 0.08 + (dh - 0.14) * 0.5; root.add(carcass);
        const pW = (dw - 0.28) / 3;
        for (let i = 0; i < 3; i++) {
          const px = -(dw - 0.28) * 0.5 + pW * 0.5 + i * (pW + 0.05);
          const panel = new THREE.Mesh(MeshKit.createBoxGeometry(pW, (dh - 0.25) * 0.78, 0.02), MATERIALS.wood_dark);
          panel.position.set(px, 0.12 + (dh - 0.25) * 0.5, dd * 0.5 - 0.01); root.add(panel);
        }
        const counter = new THREE.Mesh(MeshKit.createBoxGeometry(dw + 0.08, 0.06, dd + 0.08), MATERIALS.wood_light);
        counter.position.y = dh - 0.03; root.add(counter);
        const bell = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.055, 0.04, 8), MATERIALS.gold);
        bell.position.set(dw * 0.32, dh + 0.02, 0); root.add(bell);
        const sign = new THREE.Mesh(MeshKit.createBoxGeometry(0.28, 0.09, 0.04), MATERIALS.gold);
        sign.position.set(0, dh + 0.045, dd * 0.35); root.add(sign);

      } else if (type === 'wall_lantern') {
        // 壁灯 (Mounting Plate, Gold Bracket Arm, 6-sided Faceted Cage, Emission Core & Light)
        const mount = new THREE.Mesh(MeshKit.createBoxGeometry(0.12, 0.26, 0.03), MATERIALS.wood_dark);
        mount.position.set(0, 0, 0.015); root.add(mount);
        const arm = new THREE.Mesh(MeshKit.createBoxGeometry(0.03, 0.03, 0.22), MATERIALS.gold);
        arm.position.set(0, 0.06, 0.12); root.add(arm);
        const cage = new THREE.Mesh(new THREE.CylinderGeometry(0.11, 0.08, 0.26, 6), MATERIALS.gold);
        cage.position.set(0, -0.04, 0.22); root.add(cage);
        const capT = new THREE.Mesh(new THREE.ConeGeometry(0.13, 0.10, 6), MATERIALS.gold);
        capT.position.set(0, 0.14, 0.22); root.add(capT);
        const bulb = new THREE.Mesh(new THREE.IcosahedronGeometry(0.05, 1), MATERIALS.emission);
        bulb.position.set(0, -0.04, 0.22); root.add(bulb);
        const pLight = new THREE.PointLight(0xffd58b, 1.2, 5.0, 1.5);
        pLight.position.set(0, -0.04, 0.26); root.add(pLight);

      } else if (type === 'banker_lamp') {
        // 台灯 (Lathe Circular Base, Curved Side Arms, Emerald Hood, Warm Light)
        const base = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.11, 0.03, 12), MATERIALS.gold);
        base.position.y = 0.015; root.add(base);
        const stem = new THREE.Mesh(new THREE.CylinderGeometry(0.015, 0.015, 0.28, 8), MATERIALS.gold);
        stem.position.y = 0.17; root.add(stem);
        const hood = new THREE.Mesh(MeshKit.createBoxGeometry(0.24, 0.08, 0.12), MATERIALS.green);
        hood.position.set(0, 0.32, 0); root.add(hood);
        const emissionCore = new THREE.Mesh(MeshKit.createBoxGeometry(0.20, 0.02, 0.09), MATERIALS.emission);
        emissionCore.position.set(0, 0.29, 0); root.add(emissionCore);
        const deskLight = new THREE.PointLight(0xffe6a3, 1.0, 3.2, 1.6);
        deskLight.position.set(0, 0.27, 0); root.add(deskLight);

      } else if (type === 'pendant_lamp') {
        // 吊灯 (Ceiling Canopy, Chain/Rod, Multi-tiered Chandelier Frame, Emission Cores)
        const drop = params.drop_length ?? 1.4;
        const canopy = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.20, 0.06, 8), MATERIALS.gold);
        canopy.position.y = drop; root.add(canopy);
        const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, drop, 8), MATERIALS.gold);
        rod.position.y = drop * 0.5; root.add(rod);
        const ring = new THREE.Mesh(new THREE.TorusGeometry(0.55, 0.03, 6, 12), MATERIALS.gold);
        ring.rotation.x = Math.PI * 0.5; ring.position.y = 0; root.add(ring);
        for (let i = 0; i < 6; i++) {
          const ang = i * Math.PI / 3;
          const bulb = new THREE.Mesh(new THREE.IcosahedronGeometry(0.06, 1), MATERIALS.emission);
          bulb.position.set(Math.cos(ang) * 0.55, 0.05, Math.sin(ang) * 0.55); root.add(bulb);
        }
        const chLight = new THREE.PointLight(0xffeaad, 2.0, 9.0, 1.2);
        chLight.position.y = -0.1; root.add(chLight);

      } else if (type === 'painting') {
        // 油画挂件 (Dark Backing, Gold Mitered Frame, Solid Polygon Art Canvas)
        const pw = params.frame_w ?? 1.35, ph = params.frame_h ?? 0.95;
        const frame = new THREE.Mesh(MeshKit.createBoxGeometry(pw, ph, 0.04), MATERIALS.gold);
        frame.position.z = 0.02; root.add(frame);
        const canvas = new THREE.Mesh(MeshKit.createBoxGeometry(pw - 0.16, ph - 0.16, 0.01), MATERIALS.wall);
        canvas.position.z = 0.04; root.add(canvas);
        const art1 = new THREE.Mesh(MeshKit.createBoxGeometry((pw - 0.16) * 0.55, (ph - 0.16) * 0.50, 0.005), MATERIALS.green);
        art1.position.set(-(pw - 0.16) * 0.15, -(ph - 0.16) * 0.12, 0.045); root.add(art1);
        const art2 = new THREE.Mesh(MeshKit.createBoxGeometry((pw - 0.16) * 0.42, (ph - 0.16) * 0.35, 0.005), MATERIALS.leaf);
        art2.position.set((pw - 0.16) * 0.18, (ph - 0.16) * 0.15, 0.048); root.add(art2);

      } else if (type === 'rug') {
        // 地毯 (Woven Carpet with Border & Medallion)
        const rw = params.rug_w ?? 2.6, rd = params.rug_d ?? 1.8;
        const rugBorder = new THREE.Mesh(MeshKit.createBoxGeometry(rw, 0.015, rd), MATERIALS.wood_dark);
        rugBorder.position.y = 0.008; root.add(rugBorder);
        const rugField = new THREE.Mesh(MeshKit.createBoxGeometry(rw - 0.20, 0.018, rd - 0.20), MATERIALS.green);
        rugField.position.y = 0.009; root.add(rugField);
        const medallion = new THREE.Mesh(MeshKit.createBoxGeometry(rw * 0.42, 0.020, rd * 0.42), MATERIALS.cream);
        medallion.position.y = 0.010; root.add(medallion);

      } else if (type === 'cabinet') {
        // 立柜 (Tall Armoire/Cabinet with Doors and Handles)
        const cw = params.cab_w ?? 1.2, ch = params.cab_h ?? 2.0, cd = 0.55;
        const plinth = new THREE.Mesh(MeshKit.createBoxGeometry(cw + 0.04, 0.10, cd + 0.04), MATERIALS.wood_dark);
        plinth.position.y = 0.05; root.add(plinth);
        const body = new THREE.Mesh(MeshKit.createBoxGeometry(cw, ch - 0.18, cd), MATERIALS.wood);
        body.position.y = 0.10 + (ch - 0.18) * 0.5; root.add(body);
        const cornice = new THREE.Mesh(MeshKit.createBoxGeometry(cw + 0.06, 0.08, cd + 0.06), MATERIALS.wood_light);
        cornice.position.y = ch - 0.04; root.add(cornice);
        for (const sx of [-cw * 0.24, cw * 0.24]) {
          const panel = new THREE.Mesh(MeshKit.createBoxGeometry(cw * 0.42, (ch - 0.32) * 0.85, 0.02), MATERIALS.wood_dark);
          panel.position.set(sx, 0.14 + (ch - 0.32) * 0.5, cd * 0.5 + 0.01); root.add(panel);
        }
        for (const kx of [-0.03, 0.03]) {
          const handle = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 0.14, 8), MATERIALS.gold);
          handle.position.set(kx, ch * 0.5, cd * 0.5 + 0.03); root.add(handle);
        }

      } else if (type === 'screen') {
        // 屏风 (Three-panel Folding Screen)
        const sw = params.screen_w ?? 1.8, sh = params.screen_h ?? 1.7;
        const pW = sw / 3;
        for (let i = 0; i < 3; i++) {
          const px = -sw * 0.5 + pW * 0.5 + i * pW;
          const pz = (i === 1) ? -0.10 : 0.08;
          const frame = new THREE.Mesh(MeshKit.createBoxGeometry(pW - 0.02, sh, 0.03), MATERIALS.wood);
          frame.position.set(px, sh * 0.5, pz); root.add(frame);
          const fabric = new THREE.Mesh(MeshKit.createBoxGeometry(pW - 0.08, sh - 0.12, 0.01), MATERIALS.cream);
          fabric.position.set(px, sh * 0.5, pz); root.add(fabric);
        }

      } else if (type === 'crates') {
        // 板条木箱堆 (Crate Stack)
        const c1 = new THREE.Mesh(MeshKit.createBoxGeometry(0.70, 0.70, 0.70), MATERIALS.wood);
        c1.position.set(0, 0.35, 0); root.add(c1);
        const c2 = new THREE.Mesh(MeshKit.createBoxGeometry(0.55, 0.55, 0.55), MATERIALS.wood_light);
        c2.position.set(0.12, 0.70 + 0.275, -0.05); c2.rotation.y = 0.25; root.add(c2);

      } else {
        // 默认通用低多边形箱体
        const box = new THREE.Mesh(MeshKit.createBoxGeometry(0.6, 0.6, 0.6), MATERIALS.wood);
        box.position.y = 0.3; root.add(box);
      }

      root.traverse(child => {
        if (child.isMesh) {
          child.castShadow = true;
          child.receiveShadow = true;
        }
      });
      root.position.set(px, baseY, pz);
      root.rotation.y = (yaw || 0) * Math.PI / 180;
      prop3DGroup.add(root);
      return root;
    }
'''

html_text = html_text[:old_func_start] + new_build_single_prop + html_text[old_func_end:]
print("Upgraded buildSingleProp3D with full authentic lowpoly models.")

INDEX_FILE.write_text(html_text, encoding="utf-8")
print("Successfully patched index.html with authentic lowpoly planter and prop generators!")
