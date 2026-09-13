#!/usr/bin/env python3
"""
Klei Animation Extractor (krane_extract.py)
A pure-Python decompiler & extractor for Klei Entertainment's anim.bin & build.bin animation format.
Extracts official character animations into JSON and Godot-compatible animation track data.
"""

import sys
import os
import struct
import math
import zipfile
import json
import argparse

class KAnimReader:
    def __init__(self, data):
        self.data = data
        self.offset = 0

    def read_u8(self):
        v = self.data[self.offset]
        self.offset += 1
        return v

    def read_i32(self):
        v = struct.unpack_from('<i', self.data, self.offset)[0]
        self.offset += 4
        return v

    def read_u32(self):
        v = struct.unpack_from('<I', self.data, self.offset)[0]
        self.offset += 4
        return v

    def read_f32(self):
        v = struct.unpack_from('<f', self.data, self.offset)[0]
        self.offset += 4
        return v

    def read_str(self):
        length = self.read_u32()
        s = self.data[self.offset:self.offset + length].decode('utf-8', errors='ignore')
        self.offset += length
        return s

def parse_anim_bin(data):
    if len(data) < 8 or data[:4] != b'ANIM':
        raise ValueError("Invalid magic header, expected 'ANIM'")

    reader = KAnimReader(data)
    reader.offset = 4
    version = reader.read_i32()
    numelements = reader.read_u32()
    numframes = reader.read_u32()
    numevents = reader.read_u32()
    numanims = reader.read_u32()

    anims = []
    for _ in range(numanims):
        name = reader.read_str()
        facing_byte = reader.read_u8()
        bank_hash = reader.read_u32()
        frame_rate = reader.read_f32()
        n_frames = reader.read_u32()

        frames = []
        for _ in range(n_frames):
            bx = reader.read_f32()
            by = reader.read_f32()
            bw = reader.read_f32()
            bh = reader.read_f32()

            n_events = reader.read_u32()
            events = [reader.read_u32() for _ in range(n_events)]

            n_elems = reader.read_u32()
            elements = []
            for _ in range(n_elems):
                sym_hash = reader.read_u32()
                build_frame = reader.read_u32()
                layer_hash = reader.read_u32()
                m_a = reader.read_f32()
                m_b = reader.read_f32()
                m_c = reader.read_f32()
                m_d = reader.read_f32()
                m_tx = reader.read_f32()
                m_ty = reader.read_f32()
                z = reader.read_f32()

                det = m_a * m_d - m_b * m_c
                sign_x = -1.0 if det < 0 else 1.0
                sx = math.sqrt(m_a * m_a + m_b * m_b) * sign_x
                sy = math.sqrt(m_c * m_c + m_d * m_d)
                rot = math.atan2(m_b * sign_x, m_a * sign_x)

                elements.append({
                    'matrix': [m_a, m_b, m_c, m_d, m_tx, m_ty],
                    'symbol_hash': sym_hash,
                    'build_frame': build_frame,
                    'layer_hash': layer_hash,
                    'x': round(m_tx, 3),
                    'y': round(m_ty, 3), # m_ty is already negative-up (Godot coordinate system)
                    'rotation': round(rot, 4),
                    'scale_x': round(sx, 4),
                    'scale_y': round(sy, 4),
                    'z': round(z, 2)
                })

            frames.append({
                'bbox': [round(bx, 2), round(by, 2), round(bw, 2), round(bh, 2)],
                'elements': elements
            })

        anims.append({
            'name': name,
            'facing': facing_byte,
            'bank_hash': bank_hash,
            'frame_rate': frame_rate,
            'frames': frames
        })

    hash_table = {}
    if reader.offset < len(data):
        ht_size = reader.read_u32()
        for _ in range(ht_size):
            h = reader.read_u32()
            s = reader.read_str()
            hash_table[h] = s

    for anim in anims:
        anim['bank'] = hash_table.get(anim['bank_hash'], f"bank_{anim['bank_hash']:08x}")
        for f in anim['frames']:
            for el in f['elements']:
                el['symbol'] = hash_table.get(el['symbol_hash'], f"sym_{el['symbol_hash']:08x}")
                el['layer'] = hash_table.get(el['layer_hash'], f"layer_{el['layer_hash']:08x}")

    return anims

def extract_zip(zip_path):
    with zipfile.ZipFile(zip_path, 'r') as z:
        if 'anim.bin' in z.namelist():
            return parse_anim_bin(z.read('anim.bin'))
    return []

def main():
    parser = argparse.ArgumentParser(description="Extract Klei animations from anim.bin / zip")
    parser.add_argument("inputs", nargs="+", help="Path to anim.bin or zip files")
    parser.add_argument("--output", "-o", default="extracted_animations.json", help="Output JSON path")
    args = parser.parse_args()

    all_anims = []
    for inp in args.inputs:
        if not os.path.exists(inp):
            print(f"[Warning] File not found: {inp}")
            continue

        if inp.endswith('.zip'):
            anims = extract_zip(inp)
        else:
            with open(inp, 'rb') as f:
                anims = parse_anim_bin(f.read())

        print(f"Loaded {len(anims)} animations from {os.path.basename(inp)}")
        all_anims.extend(anims)

    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
    with open(args.output, 'w', encoding='utf-8') as f:
        json.dump(all_anims, f, indent=2, ensure_ascii=False)
    print(f"[Success] Extracted {len(all_anims)} animations to {args.output}")

if __name__ == '__main__':
    main()
