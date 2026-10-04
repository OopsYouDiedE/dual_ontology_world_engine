# 真实高程：Copernicus DEM GLO-30（30 m，海洋与水体已掩膜为 0），AWS 公开数据
#   https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_{N39_00_W032_00, N38_00_W029_00}_DEM/...
#   署名：© DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018,
#         provided under COPERNICUS by the European Union and ESA; all rights reserved.
# 投影：以岛中心为原点的等距近似（米），x 向东，y 向北；岛屿尺度内误差可忽略。
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ISLANDS = {
    # 名称: (瓦片, 纬度范围, 经度范围)
    'corvo': ('N39_00_W032_00', (39.660, 39.738), (-31.135, -31.065)),
    'faial': ('N38_00_W029_00', (38.510, 38.660), (-28.850, -28.590)),
}
ATTRIBUTION = 'Copernicus DEM GLO-30 © DLR e.V. 2010-2014, © Airbus Defence and Space GmbH 2014-2018, provided under COPERNICUS by the EU and ESA'

def load_tile(tile):
    a = np.array(Image.open(os.path.join(HERE, 'dem', f'cop_{tile}.tif')), dtype=np.float32)
    lat_top, lon_left = int(tile[1:3]) + 1, -int(tile[8:11])
    return a, lat_top, lon_left

def blur(Z, passes=1):
    """3×3 二项式平滑（只用于制图，消去 30 m 格网的台阶感）。"""
    k = np.array([1, 2, 1], float) / 4
    for _ in range(passes):
        P = np.pad(Z, 1, mode='edge')
        Z = k[0] * P[:-2, 1:-1] + k[1] * P[1:-1, 1:-1] + k[2] * P[2:, 1:-1]
        P = np.pad(Z, 1, mode='edge')
        Z = k[0] * P[1:-1, :-2] + k[1] * P[1:-1, 1:-1] + k[2] * P[1:-1, 2:]
    return Z

def island(key, scale=1.0):
    """scale：三向等比缩放（水平与高程同乘），坡度不变。"""
    tile, (la0, la1), (lo0, lo1) = ISLANDS[key]
    a, lat_top, lon_left = load_tile(tile)
    r0, r1 = int((lat_top - la1) * 3600), int((lat_top - la0) * 3600) + 1
    c0, c1 = int((lo0 - lon_left) * 3600), int((lo1 - lon_left) * 3600) + 1
    Z = a[r0:r1, c0:c1]
    lats = lat_top - (np.arange(r0, r1) + 0.5) / 3600.0      # 像元中心
    lons = lon_left + (np.arange(c0, c1) + 0.5) / 3600.0
    lat0, lon0 = (la0 + la1) / 2, (lo0 + lo1) / 2
    xs = (lons - lon0) * 111320 * np.cos(np.radians(lat0))
    ys = (lats - lat0) * 110574
    return dict(key=key, xs=xs * scale, ys=ys * scale, Z=Z * scale, land=Z != 0, lat0=lat0, lon0=lon0, lats=lats, lons=lons, scale=scale)

def to_xy(info, lat, lon):
    k = info.get('scale', 1.0)
    return ((lon - info['lon0']) * 111320 * np.cos(np.radians(info['lat0'])) * k, (lat - info['lat0']) * 110574 * k)

def sample(info, x, y):
    """双线性插值取高程（米）。"""
    xs, ys, Z = info['xs'], info['ys'], info['Z']
    fx = np.interp(x, xs, np.arange(len(xs)))
    fy = np.interp(-np.asarray(y, float), -ys, np.arange(len(ys)))
    i0, j0 = np.floor(fy).astype(int), np.floor(fx).astype(int)
    i1, j1 = np.minimum(i0 + 1, len(ys) - 1), np.minimum(j0 + 1, len(xs) - 1)
    ty, tx = fy - i0, fx - j0
    return (Z[i0, j0] * (1 - tx) + Z[i0, j1] * tx) * (1 - ty) + (Z[i1, j0] * (1 - tx) + Z[i1, j1] * tx) * ty
