"""Headless camera-only probe of the unchanged current Corvo campus model."""
import os, sys, json, hashlib
HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.abspath(os.path.join(HERE, '../../remake-chapter2/campus-site'))
src = open(os.path.join(SOURCE, 'build_3d.py'), encoding='utf-8').read()
src = src.split("if '--save' in argv:")[0]
src = src.replace("T = np.load(os.path.join(HERE, 'terrain.npz'))", "T = np.load(" + repr(os.path.join(HERE, 'terrain.npz')) + ")")
ns = {'__file__': os.path.join(SOURCE, 'build_3d.py'), '__name__': 'camera_source'}
old_argv = sys.argv[:]
sys.argv = [sys.argv[0]]
exec(compile(src, ns['__file__'], 'exec'), ns)
sys.argv = old_argv
bpy, Vector = ns['bpy'], ns['Vector']
scn, SC, O, EU, EV, ground = (ns[k] for k in ('scn','SC','O','EU','EV','ground'))
scn.render.engine = 'BLENDER_EEVEE_NEXT'
scn.eevee.taa_render_samples = 16
scn.render.resolution_x, scn.render.resolution_y = 960, 540
scn.render.resolution_percentage = 100
scn.render.image_settings.file_format = 'PNG'
def point(u,v,h):
    p = O + EU*u + EV*v
    return Vector((p.x,p.y,ground(p.x,p.y)+h))
def target(u,v,z):
    p = O + EU*u + EV*v
    return Vector((p.x,p.y,z))
tests = [
 ('01_gate_eye',0,-160,1.7,0,500,100,24),
 ('02_court_eye',0,-104,1.7,0,500,100,35),
 ('03_court_6m',0,-104,6,0,500,100,35),
 ('04_living_east_eye',-16,-56.7,1.7,-16,500,105,24),
 ('05_south_25m',0,-210,25,0,400,60,35),
 ('06_southeast_45m',125,-220,45,-10,80,50,24),
]
records=[]
for name,u,v,h,tu,tv,tz,lens in tests:
    loc, aim = point(u,v,h), target(tu,tv,tz)
    cam = ns['camera'](name,loc,aim,lens)
    scn.camera=cam
    scn.render.filepath=os.path.join(HERE,name+'.png')
    rec=dict(name=name,location=list(loc),target=list(aim),lens_mm=lens,eye_above_ground_m=h,local_uv=[u,v],resolution=[960,540],engine=scn.render.engine,samples=16)
    records.append(rec)
    bpy.ops.render.render(write_still=True)
json.dump(dict(source='artifacts/remake-chapter2/campus-site/build_3d.py',scene_sha256=hashlib.sha256(open(os.path.join(SOURCE,'scene.json'),'rb').read()).hexdigest(),terrain_sha256=hashlib.sha256(open(os.path.join(HERE,'terrain.npz'),'rb').read()).hexdigest(),scale=0.5,cameras=records),open(os.path.join(HERE,'camera_parameters.json'),'w',encoding='utf-8'),ensure_ascii=False,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE,'camera_tests.blend'))
print('Completed six camera-only probes')
