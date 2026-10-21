"""Bake title-desk microstructure with Blender 4.2 / CPU Cycles.

Run from the repository root:
  blender --factory-startup --background --python mcp/asset_pipeline/build_desk_surface_maps.py

Only stdlib + Blender are required. This never changes the albedo or desk geometry.
The four authored node graphs produce actual tangent normals and roughness data;
the reference albedo contributes a tightly limited, high-pass registration detail.
"""

from array import array
import binascii
import hashlib
import json
import math
from pathlib import Path
import struct
import time
import zlib

import bpy


REPO = Path(__file__).resolve().parents[2]
ROOT = REPO / "godot_csharp"
SOURCE = ROOT.parent / "mcp/asset_pipeline/sources/Internal/Props/UIDesk/Painterly3D"
RUNTIME = ROOT / "ShareAsset/Internal/Props/UIDesk/Painterly3D"
ALBEDO_PATH = RUNTIME / "reference-material-atlas.webp"
SOURCE_BLEND = SOURCE / "desk_surface_maps.blend"
RESOLUTION = 1024
SAMPLES = 8
SEED = 73417

# Image coordinates use a top-left origin; Blender UVs use a bottom-left origin.
# span_m is the physical width of one authored surface sample, not a prop scale.
TILES = (
    dict(name="wood", u=0.0, v=0.5, crop=(0, 0), depth_m=.001,
         span_m=1.0, base_roughness=.49, metallic=0.0),
    dict(name="cloth", u=0.5, v=0.5, crop=(512, 0), depth_m=.0005,
         span_m=.45, base_roughness=.83, metallic=0.0),
    dict(name="paper", u=0.0, v=0.0, crop=(0, 512), depth_m=.00015,
         span_m=.45, base_roughness=.90, metallic=0.0),
    dict(name="brass", u=0.5, v=0.0, crop=(512, 512), depth_m=.0002,
         span_m=.35, base_roughness=.40, metallic=1.0),
)


class SurfaceGraph:
    """Small node helpers; all height/roughness authoring remains in the .blend."""

    def __init__(self, tile, reference):
        self.tile = tile
        self.material = bpy.data.materials.new("DeskMicro_" + tile["name"])
        self.material.use_nodes = True
        self.nodes = self.material.node_tree.nodes
        self.links = self.material.node_tree.links
        self.nodes.clear()
        self.count = 0
        uv = self.node("ShaderNodeUVMap", "Atlas UV")
        uv.uv_map = "BakeUV"
        self.atlas_uv = uv.outputs["UV"]
        shifted = self.vector("SUBTRACT", self.atlas_uv, (tile["u"], tile["v"], 0.0))
        self.local_uv = self.vector("MULTIPLY", shifted, (2.0, 2.0, 1.0))
        separate = self.node("ShaderNodeSeparateXYZ", "Tile coordinates")
        self.links.new(self.local_uv, separate.inputs[0])
        self.u, self.v = separate.outputs["X"], separate.outputs["Y"]
        self.reference = reference

    def node(self, node_type, label):
        n = self.nodes.new(node_type)
        n.label = label
        n.name = label
        n.location = ((self.count % 9) * 210, -(self.count // 9) * 220)
        self.count += 1
        return n

    def assign(self, socket, value):
        if isinstance(value, (int, float, tuple, list)):
            socket.default_value = value
        else:
            self.links.new(value, socket)

    def scalar(self, operation, a, b=None, label=None):
        n = self.node("ShaderNodeMath", label or operation)
        n.operation = operation
        self.assign(n.inputs[0], a)
        if b is not None:
            self.assign(n.inputs[1], b)
        return n.outputs[0]

    def vector(self, operation, a, b):
        n = self.node("ShaderNodeVectorMath", operation)
        n.operation = operation
        self.assign(n.inputs[0], a)
        self.assign(n.inputs[1], b)
        return n.outputs[0]

    def clamp(self, value, low, high):
        return self.scalar("MINIMUM", self.scalar("MAXIMUM", value, low), high)

    def weighted(self, values, label):
        result = 0.0
        for socket, weight in values:
            part = self.scalar("MULTIPLY", socket, weight)
            result = self.scalar("ADD", result, part, label=label)
        return result

    def noise(self, scale, label, detail=2.0):
        coord = self.vector("MULTIPLY", self.local_uv, scale)
        n = self.node("ShaderNodeTexNoise", label)
        n.noise_dimensions = "3D"
        self.links.new(coord, n.inputs["Vector"])
        n.inputs["Scale"].default_value = 1.0
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = .58
        return n.outputs["Fac"]

    def ridge(self, coordinate, repeats, phase=0.0, exponent=1.0):
        angle = self.scalar("ADD", self.scalar("MULTIPLY", coordinate, math.tau * repeats), phase)
        wave = self.scalar("SINE", angle)
        wave = self.scalar("MULTIPLY", self.scalar("ADD", wave, 1.0), .5)
        return self.scalar("POWER", wave, exponent)

    def image_sample(self, du, dv, label):
        coord = self.vector("ADD", self.atlas_uv, (du, dv, 0.0))
        # Clamp every sample inside its material quadrant, including filter support.
        inset = 1.5 / self.reference.size[0]
        coord = self.vector("MAXIMUM", coord, (self.tile["u"] + inset,
                                               self.tile["v"] + inset, 0.0))
        coord = self.vector("MINIMUM", coord, (self.tile["u"] + .5 - inset,
                                               self.tile["v"] + .5 - inset, 0.0))
        n = self.node("ShaderNodeTexImage", label)
        n.image = self.reference
        n.interpolation = "Linear"
        n.extension = "EXTEND"
        self.links.new(coord, n.inputs["Vector"])
        return n.outputs["Color"]

    def registration_detail(self):
        center_color = self.image_sample(0.0, 0.0, "Reference albedo - preview")
        center = self.node("ShaderNodeRGBToBW", "Reference luminance")
        self.links.new(center_color, center.inputs[0])
        neighbors = []
        radius = 3.0 / self.reference.size[0]
        for i, (du, dv) in enumerate(((radius, 0), (-radius, 0), (0, radius), (0, -radius))):
            color = self.image_sample(du, dv, "Reference neighbor %d" % i)
            gray = self.node("ShaderNodeRGBToBW", "Neighbor luminance %d" % i)
            self.links.new(color, gray.inputs[0])
            neighbors.append((gray.outputs[0], .25))
        blur = self.weighted(neighbors, "Four-neighbor local mean")
        residual = self.scalar("SUBTRACT", center.outputs[0], blur,
                               label="Reference high-pass ONLY")
        # At most +/- 3% of normalized height. Broad color/light is not geometry.
        residual = self.scalar("MULTIPLY", self.clamp(residual, -.12, .12), .25,
                               label="Registration height bounded to +/-0.03")
        return residual, center_color

    def build(self):
        registration, albedo = self.registration_detail()
        kind = self.tile["name"]
        if kind == "wood":
            wander = self.noise((4.0, 1.5, 1.0), "Slow grain wander")
            grain = self.ridge(self.u, 76, self.scalar("MULTIPLY", wander, 3.2), 7.0)
            pores = self.noise((95.0, 12.0, 1.0), "Fine longitudinal pores")
            wear = self.noise((8.0, 5.0, 1.0), "Uneven handled wear")
            height = self.weighted(((grain, -.65), (pores, .25), (wear, .10)), "Wood groove height")
            height = self.scalar("ADD", height, .5)
            rough = self.weighted(((grain, .11), (pores, .16), (wear, -.10)), "Wood micro roughness")
            rough = self.scalar("ADD", rough, .43)
        elif kind == "cloth":
            warp = self.ridge(self.u, 64, exponent=.7)
            weft = self.ridge(self.v, 64, exponent=.7)
            fibers = self.noise((125.0, 125.0, 1.0), "Fine woven fibers", 1.0)
            crossing = self.scalar("MULTIPLY", self.ridge(self.u, 32), self.ridge(self.v, 32))
            height = self.weighted(((warp, .40), (weft, .40), (crossing, .08), (fibers, .12)),
                                   "Woven warp and weft height")
            rough = self.scalar("ADD", self.scalar("MULTIPLY", fibers, .18), .73)
        elif kind == "paper":
            along = self.noise((115.0, 18.0, 1.0), "Paper longitudinal fibers")
            cross = self.noise((24.0, 108.0, 1.0), "Paper cross fibers")
            tooth = self.noise((155.0, 155.0, 1.0), "Fine paper tooth", 1.0)
            height = self.weighted(((along, .54), (cross, .32), (tooth, .14)), "Paper fiber height")
            rough = self.scalar("ADD", self.weighted(((tooth, .10), (along, .05)), "Paper roughness"), .82)
        else:
            scratch_noise = self.noise((8.0, 120.0, 1.0), "Long fine brass scratches")
            scratches = self.scalar("POWER", self.scalar("SUBTRACT", 1.0, scratch_noise), 5.0)
            patina = self.noise((6.0, 7.0, 1.0), "Authored oxidation patches")
            tooth = self.noise((92.0, 92.0, 1.0), "Metal polishing tooth", 1.0)
            height = self.weighted(((scratches, -1.8), (tooth, .12), (patina, .06)), "Shallow brass scratches")
            height = self.scalar("ADD", height, .5)
            rough = self.scalar("ADD", self.weighted(((patina, .40), (scratches, .18), (tooth, .06)),
                                                      "Patina and scratch roughness"), .20)

        height = self.scalar("ADD", height, registration, label="Authored height + limited registration")
        # Meet the same flat height at tile edges. This also prevents atlas seams
        # becoming raised material borders when a quadrant repeats in Godot.
        edge_u = self.scalar("MINIMUM", self.u, self.scalar("SUBTRACT", 1.0, self.u))
        edge_v = self.scalar("MINIMUM", self.v, self.scalar("SUBTRACT", 1.0, self.v))
        edge = self.clamp(self.scalar("MULTIPLY", self.scalar("MINIMUM", edge_u, edge_v), 128.0), 0, 1)
        height = self.scalar("ADD", self.scalar("MULTIPLY", self.scalar("SUBTRACT", height, .5), edge), .5)
        base = self.tile["base_roughness"]
        rough = self.scalar("ADD", self.scalar("MULTIPLY", self.scalar("SUBTRACT", rough, base), edge), base)
        rough = self.clamp(rough, .18, .97)

        bump = self.node("ShaderNodeBump", "Authored depth %.6f m" % self.tile["depth_m"])
        bump.inputs["Strength"].default_value = 1.0
        bump.inputs["Distance"].default_value = self.tile["depth_m"]
        self.links.new(height, bump.inputs["Height"])
        bsdf = self.node("ShaderNodeBsdfPrincipled", "Physically authored surface")
        self.links.new(albedo, bsdf.inputs["Base Color"])
        self.links.new(rough, bsdf.inputs["Roughness"])
        self.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
        bsdf.inputs["Metallic"].default_value = self.tile["metallic"]
        output = self.node("ShaderNodeOutputMaterial", "Material output")
        self.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
        self.target = self.node("ShaderNodeTexImage", "ACTIVE BAKE TARGET - not connected")
        self.material["authored_depth_m"] = self.tile["depth_m"]
        self.material["sample_span_m"] = self.tile["span_m"]
        return self.material

    def target_image(self, image):
        for node in self.nodes:
            node.select = False
        self.target.image = image
        self.target.select = True
        self.nodes.active = self.target


def create_bake_mesh(graphs):
    vertices, faces, uv_values = [], [], []
    for i, tile in enumerate(TILES):
        x, y, size = (i % 2) * 1.4, (i // 2) * 1.4, tile["span_m"]
        start = len(vertices)
        vertices.extend(((x, y, 0), (x + size, y, 0), (x + size, y + size, 0), (x, y + size, 0)))
        faces.append((start, start + 1, start + 2, start + 3))
        u, v = tile["u"], tile["v"]
        uv_values.extend(((u, v), (u + .5, v), (u + .5, v + .5), (u, v + .5)))
    mesh = bpy.data.meshes.new("DeskSurfaceBakeQuads")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name="BakeUV")
    for i, value in enumerate(uv_values):
        uv.data[i].uv = value
    obj = bpy.data.objects.new("DeskSurfaceBakeSamples", mesh)
    bpy.context.collection.objects.link(obj)
    for graph in graphs:
        mesh.materials.append(graph.material)
    for i, polygon in enumerate(mesh.polygons):
        polygon.material_index = i
        polygon.use_smooth = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    return obj


def data_image(name):
    image = bpy.data.images.new(name, width=RESOLUTION, height=RESOLUTION,
                                alpha=True, float_buffer=True, is_data=True)
    image.colorspace_settings.name = "Non-Color"
    return image


def image_buffer(image):
    pixels = array("f", [0.0]) * (RESOLUTION * RESOLUTION * 4)
    image.pixels.foreach_get(pixels)
    return pixels


def png_chunk(kind, content):
    return struct.pack(">I", len(content)) + kind + content + struct.pack(">I", binascii.crc32(kind + content) & 0xffffffff)


def write_data_png(path, pixels, width, height, left=0, top=0, normal=False):
    """Encode Cycles' data values directly; no display/gamma/AgX transformation.

    PNG rows run top to bottom. Blender image buffers run bottom to top. The
    same conversion is used for the atlas and its exact quadrant crops.
    """
    channels = 3 if normal else 1
    raw = bytearray()
    for row in range(height):
        source_y = RESOLUTION - 1 - (top + row)
        raw.append(0)  # PNG filter: none; deterministic byte representation.
        for x in range(left, left + width):
            start = (source_y * RESOLUTION + x) * 4
            for c in range(channels):
                value = pixels[start + c]
                if not math.isfinite(value):
                    raise RuntimeError("Non-finite bake sample in " + path.name)
                raw.append(max(0, min(255, int(value * 255.0 + .5))))
    color_type = 2 if normal else 0
    header = struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0)
    encoded = b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header)
    encoded += png_chunk(b"IDAT", zlib.compress(raw, 9)) + png_chunk(b"IEND", b"")
    path.write_bytes(encoded)
    return dict(name=path.name, width=width, height=height, size_bytes=len(encoded),
                sha256=hashlib.sha256(encoded).hexdigest())


def validate_buffers(normals, roughness):
    summary = {}
    for tile in TILES:
        left, top = tile["crop"]
        count, rg_sum, rg_sq = 0, 0.0, 0.0
        rough_min, rough_max, min_z = 1.0, 0.0, 1.0
        for row in range(top + 8, top + 504, 4):
            y = RESOLUTION - 1 - row
            for x in range(left + 8, left + 504, 4):
                idx = (y * RESOLUTION + x) * 4
                nx, ny, nz = (normals[idx + c] * 2.0 - 1.0 for c in range(3))
                length = math.sqrt(nx * nx + ny * ny + nz * nz)
                r = roughness[idx]
                if not (.80 < length < 1.20 and -.01 < nz <= 1.01 and .17 < r < .98):
                    raise RuntimeError("Invalid normal/roughness bake in " + tile["name"])
                count += 2
                rg_sum += nx + ny
                rg_sq += nx * nx + ny * ny
                min_z = min(min_z, nz)
                rough_min, rough_max = min(rough_min, r), max(rough_max, r)
        variation = math.sqrt(max(0.0, rg_sq / count - (rg_sum / count) ** 2))
        if variation < .0001 or rough_max - rough_min < .001:
            raise RuntimeError("Flat or missing surface data in " + tile["name"])
        summary[tile["name"]] = dict(normal_xy_std=round(variation, 6), min_normal_z=round(min_z, 6),
                                     roughness_min=round(rough_min, 6), roughness_max=round(rough_max, 6))
    return summary


def main():
    started = time.monotonic()
    if not ALBEDO_PATH.is_file():
        raise FileNotFoundError("Required reference albedo: " + str(ALBEDO_PATH))
    SOURCE.mkdir(parents=True, exist_ok=True)
    RUNTIME.mkdir(parents=True, exist_ok=True)
    (ROOT.parent / "mcp/asset_pipeline/sources/.gdignore").touch(exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = SAMPLES
    scene.cycles.seed = SEED
    scene.cycles.use_animated_seed = False
    scene.cycles.use_denoising = False
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.bake.use_selected_to_active = False
    scene.render.bake.margin = 0
    reference = bpy.data.images.load(str(ALBEDO_PATH), check_existing=False)
    reference.colorspace_settings.name = "sRGB"
    if tuple(reference.size) != (1254, 1254):
        raise ValueError("Reference atlas must be 1254 x 1254, got " + str(tuple(reference.size)))
    reference.pack()
    graphs = [SurfaceGraph(tile, reference) for tile in TILES]
    for graph in graphs:
        graph.build()
    create_bake_mesh(graphs)
    normal_image = data_image("reference-normal (linear OpenGL +Y)")
    rough_image = data_image("reference-roughness (linear scalar)")
    source_text = bpy.data.texts.new("build_desk_surface_maps.py")
    source_text.write(Path(__file__).read_text(encoding="utf-8"))
    scene["surface_bake_seed"] = SEED
    scene["normal_encoding"] = "Tangent OpenGL +X +Y +Z; linear data; 8-bit RGB PNG"
    scene["roughness_encoding"] = "Linear scalar; 8-bit grayscale PNG; no gamma transform"

    for graph in graphs:
        graph.target_image(normal_image)
    # Preserve the complete authoring graphs even if a later bake is interrupted.
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))
    print("DESK_SURFACE_BAKE normal 1024 CPU", flush=True)
    bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT", normal_r="POS_X",
                        normal_g="POS_Y", normal_b="POS_Z", target="IMAGE_TEXTURES",
                        save_mode="INTERNAL", use_clear=True, margin=0,
                        use_selected_to_active=False, uv_layer="BakeUV")
    for graph in graphs:
        graph.target_image(rough_image)
    print("DESK_SURFACE_BAKE roughness 1024 CPU", flush=True)
    bpy.ops.object.bake(type="ROUGHNESS", target="IMAGE_TEXTURES", save_mode="INTERNAL",
                        use_clear=True, margin=0, use_selected_to_active=False, uv_layer="BakeUV")
    normal_pixels, rough_pixels = image_buffer(normal_image), image_buffer(rough_image)
    statistics = validate_buffers(normal_pixels, rough_pixels)
    files = [write_data_png(RUNTIME / "reference-normal.png", normal_pixels, 1024, 1024, normal=True),
             write_data_png(RUNTIME / "reference-roughness.png", rough_pixels, 1024, 1024)]
    for tile in TILES:
        if tile["name"] not in ("cloth", "paper"):
            continue
        x, y = tile["crop"]
        prefix = "reference-" + tile["name"]
        files.append(write_data_png(RUNTIME / (prefix + "-normal.png"), normal_pixels,
                                    512, 512, x, y, normal=True))
        files.append(write_data_png(RUNTIME / (prefix + "-roughness.png"), rough_pixels,
                                    512, 512, x, y))
    manifest = dict(blender_version=bpy.app.version_string, generator=Path(__file__).name,
                    seed=SEED, samples=SAMPLES, device="CPU", atlas_layout="TL wood / TR cloth / BL paper / BR brass",
                    albedo_sha256=hashlib.sha256(ALBEDO_PATH.read_bytes()).hexdigest(),
                    normal="OpenGL tangent +Y, linear RGB", roughness="linear scalar",
                    parameters=list(TILES), statistics=statistics, files=files)
    (SOURCE / "reference-surface-maps-manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    # Pack the two float bake buffers and keep the normal target active for review.
    normal_image.pack()
    rough_image.pack()
    for graph in graphs:
        graph.target_image(normal_image)
    scene["surface_bake_statistics"] = json.dumps(statistics)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND))
    print("DESK_SURFACE_BAKE_OK " + json.dumps(dict(seconds=round(time.monotonic() - started, 2),
                                                   statistics=statistics, files=files)), flush=True)


if __name__ == "__main__":
    main()
