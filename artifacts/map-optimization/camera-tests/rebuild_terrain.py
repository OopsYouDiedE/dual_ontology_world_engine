"""Reproduce export_scene.py's terrain-only export without modifying its scene.json."""
import os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.abspath(os.path.join(HERE, '../../remake-chapter2/campus-site'))
sys.path.insert(0, SOURCE)
import campus as S
xs = np.arange(S.I['xs'][0] + 60, S.I['xs'][-1] - 60, 10.0)
ys = np.arange(S.I['ys'][-1] + 60, S.I['ys'][0] - 60, 10.0)
GX, GY = np.meshgrid(xs, ys)
Z = S.ground(GX, GY).astype(np.float32)
LAND = S.sample(S.LANDF, GX, GY) > 0.5
Z = np.where(LAND, Z, np.minimum(Z, -0.5)).astype(np.float32)
np.savez_compressed(os.path.join(HERE, 'terrain.npz'), xs=xs, ys=ys, z=Z)
print('terrain', Z.shape, 'scale', S.SCALE, 'height', float(Z.max()))
