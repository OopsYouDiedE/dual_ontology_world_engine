"""Read-only audio seam measurements; never invokes build_soundtrack.main."""
from pathlib import Path
import sys, subprocess, json, hashlib, zlib
import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'music'))
import cues, interpret, synth, build_soundtrack as build

def db(x):
    return float(20*np.log10(max(float(x), 1e-15)))

def decode(path):
    raw = subprocess.check_output(['ffmpeg','-v','error','-i',str(path),'-f','f32le','-acodec','pcm_f32le','-ac','2','-ar',str(synth.SR),'-'])
    return np.frombuffer(raw, dtype='<f4').reshape(-1,2)

def metrics(x):
    x = np.asarray(x, dtype=np.float64)
    n = len(x); sr = synth.SR
    dif = np.abs(np.diff(x,axis=0))
    jump = np.abs(x[0]-x[-1])
    result = dict(samples=n, sample_rate=sr,duration_s=n/sr,rms_dbfs=db(np.sqrt(np.mean(x*x))),peak_dbfs=db(np.max(np.abs(x))),
                  seam_jump_per_channel=jump.tolist(),seam_jump_max=float(max(jump)),seam_jump_dbfs=db(max(jump)),
                  internal_diff_p999=float(np.quantile(dif,.999)),internal_diff_max=float(np.max(dif)),
                  seam_percentile=float(np.mean(dif<=max(jump))*100))
    result['jump_over_internal_p999'] = float(max(jump)/np.quantile(dif,.999))
    for ms in [10,100]:
        w=round(sr*ms/1000)
        seam=np.concatenate([x[-w:],x[:w]])
        result[f'head_{ms}ms_rms_dbfs']=db(np.sqrt(np.mean(x[:w]**2)))
        result[f'tail_{ms}ms_rms_dbfs']=db(np.sqrt(np.mean(x[-w:]**2)))
        result[f'seam_plusminus_{ms}ms_rms_dbfs']=db(np.sqrt(np.mean(seam**2)))
        result[f'seam_local_{ms}ms_diff_p999']=float(np.quantile(np.abs(np.diff(seam,axis=0)),.999))
        result[f'seam_local_{ms}ms_diff_max']=float(np.max(np.abs(np.diff(seam,axis=0))))
    amp=np.max(np.abs(x),axis=1)
    for threshold in [-60,-80]:
        above=np.flatnonzero(amp>=10**(threshold/20))
        result[f'head_below_{threshold}db_s']=float(above[0]/sr) if len(above) else n/sr
        result[f'tail_below_{threshold}db_s']=float((n-1-above[-1])/sr) if len(above) else n/sr
    return result

def existing():
    results={}
    for make in cues.CUES:
        p=make()
        events,total=interpret.perform(p,interpret.Style(**(p.style or {})),np.random.default_rng(zlib.crc32(p.slug.encode())))
        entry=dict(loop=p.loop, interpreted_duration_s=total,period_samples_exact=total*synth.SR,
                   round_period_samples=round(total*synth.SR),
                   first_events=[dict(staff=e.staff,pitch=e.pitch,start=e.start,length=e.length) for e in events if e.start<.1])
        for kind,path in [('ogg',ROOT/'godot_csharp/ShareAsset/Internal/Props/Audio/Music'/f'{p.slug}.ogg'),
                          ('mp3',ROOT/'music'/f'Blessed Sleepers - {build.SCORE_NAMES[p.slug]}.mp3')]:
            x=decode(path)
            entry[kind]=dict(path=str(path.relative_to(ROOT)),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),**metrics(x))
            if kind=='ogg' and p.slug in ['door_side','day_noon','rite']:
                w=3*synth.SR
                sf.write(str(OUT/f'loop-seam-{p.slug}.wav'),np.concatenate([x[-w:],x[:w],x[-w:],x[:w]]),synth.SR,subtype='PCM_24')
        results[p.slug]=entry
        print(p.slug, 'OGG',entry['ogg']['duration_s'], 'jump/p999',round(entry['ogg']['jump_over_internal_p999'],3),
              'MP3',entry['mp3']['duration_s'],'silence',round(entry['mp3']['head_below_-60db_s'],3),round(entry['mp3']['tail_below_-60db_s'],3),flush=True)
    (OUT/'loop-seam-metrics.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    return results

def rerender():
    captured={}
    original=synth.compress
    def capture(stereo, **kwargs):
        captured['precompress']=stereo
        out=original(stereo,**kwargs)
        captured['compressed']=out
        return out
    synth.compress=capture
    audio,total=build.render(cues.door_side())
    synth.compress=original
    folder=ROOT/'tmp'/'audio-seam-validation';folder.mkdir(parents=True,exist_ok=True)
    build.write_ogg(str(folder/'door_side.ogg'),audio)
    pre=captured['precompress'];post=captured['compressed']
    gains=[]
    for i in [0, -1]:
        c=int(np.argmax(np.abs(pre[:,i])))
        gains.append(db(post[c,i]/pre[c,i]))
    result=dict(total_s=total,pcm_precompress=metrics(pre.T),pcm_compressed=metrics(post.T),pcm_mastered=metrics(audio.T),
                encoded_ogg=metrics(decode(folder/'door_side.ogg')),compress_boundary_gain_db=gains,
                compression_boundary_gain_change_db=gains[0]-gains[1],
                exact_period_samples=total*synth.SR,round_period_samples=round(total*synth.SR),
                third_start_minus_second_start_samples=round(2*total*synth.SR)-round(total*synth.SR))
    (OUT/'loop-seam-door-side-rerender.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2),flush=True)

if __name__=='__main__':
    if '--rerender' in sys.argv: rerender()
    else: existing()
