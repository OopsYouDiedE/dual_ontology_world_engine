"""Read-only Terrarium decoder. Run with system Python, then build_scene.py in Blender."""
from pathlib import Path
import numpy as np
from PIL import Image

HERE=Path(__file__).resolve().parent
SRC=HERE.parent/'terrain-source'
a=np.asarray(Image.open(SRC/'corvo-terrarium.png').convert('RGB'),dtype=float)
z=a[:,:,0]*256+a[:,:,1]+a[:,:,2]/256-32768
np.save(HERE/'terrain_elevation.npy',z)

def sample(x,y):
    lon=-31.1155+x/(.5*111320*np.cos(np.radians(39.67325)))
    lat=39.67325+y/(.5*111320)
    px=(lon+180)/360*8192*256-3387*256
    py=(1-np.arcsinh(np.tan(np.radians(lat)))/np.pi)/2*8192*256-3109*256
    ix,iy=int(px),int(py);u,v=px-ix,py-iy
    return (z[iy,ix]*(1-u)+z[iy,ix+1]*u)*(1-v)+(z[iy+1,ix]*(1-u)+z[iy+1,ix+1]*u)*v

if __name__=='__main__':
    for y in [-200,-150,-100,-50,0,50,75,100,130,160,190,220,250]:
        print(y,[(x,round(sample(x,y),1)) for x in [-180,-120,-60,0,60,120,180]])
