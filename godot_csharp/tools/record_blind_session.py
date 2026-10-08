"""Record a live rendered blind session; no story or gameplay automation."""
import argparse, ctypes, json, pathlib, subprocess, time
from ctypes import wintypes

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT = ROOT / 'artifacts/basic-ui-system/level3-6_1-sol'

def save(path, data):
    tmp = path.with_suffix('.tmp')
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    tmp.replace(path)

def window(pid):
    found = []
    user = ctypes.windll.user32
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def visit(hwnd, _):
        owner = wintypes.DWORD()
        user.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user.IsWindowVisible(hwnd):
            title = ctypes.create_unicode_buffer(1024)
            user.GetWindowTextW(hwnd, title, 1024)
            if title.value: found.append((hwnd, title.value))
        return True
    user.EnumWindows(callback_type(visit), 0)
    return found[0] if found else None

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('operation', choices=['run', 'stop', 'status'])
    p.add_argument('--session', required=True)
    p.add_argument('--agent', default='recording-preflight')
    p.add_argument('--output', type=pathlib.Path, default=DEFAULT)
    p.add_argument('--godot', default=r'C:\Users\iii\Desktop\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe')
    p.add_argument('--ffmpeg', default=r'C:\Users\iii\Documents\ffmpeg\bin\ffmpeg.exe')
    a = p.parse_args()
    import re
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{0,63}', a.session): p.error('invalid session')
    out = a.output.resolve() / a.session
    state = out / 'recording.json'
    if a.operation == 'status':
        print(state.read_text(encoding='utf-8')); return
    if a.operation == 'stop':
        (out / 'stop.request').touch()
        for _ in range(120):
            data = json.loads(state.read_text(encoding='utf-8'))
            if data['state'] in ['complete', 'failed']:
                print(json.dumps(data, ensure_ascii=False)); return
            time.sleep(0.5)
        raise RuntimeError('recorder has not finalized; inspect recording.json and logs')
    out.mkdir(parents=True, exist_ok=False)
    game = None; recorder = None
    data = {'state':'starting', 'session':a.session, 'started_epoch':time.time(), 'audio':'unavailable: no game loopback capture device; microphone excluded', 'output':str(out / 'gameplay.mp4'), 'session_directory':str(ROOT / 'artifacts/remake-belief-replay' / ('blind-' + a.session))}
    save(state, data)
    try:
        with (out / 'godot.log').open('w', encoding='utf-8') as glog, (out / 'ffmpeg.log').open('w', encoding='utf-8') as flog:
            godot_path = pathlib.Path(a.godot)
            gui_path = godot_path.with_name(godot_path.name.replace('_console.exe', '.exe'))
            if gui_path.exists(): godot_path = gui_path
            game = subprocess.Popen([str(godot_path), '--path', str(ROOT / 'godot_csharp'), '--rendering-method', 'gl_compatibility', '--windowed', '--resolution', '1280x992', '--script', 'res://scenes/Awakening/check/playtest_session.gd', '--', '--session='+a.session, '--agent='+a.agent], stdout=glog, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
            target = None
            for _ in range(240):
                target = window(game.pid)
                if target: break
                if game.poll() is not None: raise RuntimeError('game exited before opening window')
                time.sleep(.25)
            if not target: raise RuntimeError('no rendered game window')
            for _ in range(240):
                if (pathlib.Path(data['session_directory']) / 'status.json').exists(): break
                if game.poll() is not None: raise RuntimeError('game exited during initialization')
                time.sleep(.25)
            else: raise RuntimeError('game initial snapshot unavailable')
            hwnd, title = target
            title = 'Blind recording ' + a.session
            ctypes.windll.user32.SetWindowTextW(hwnd, title)
            command = [a.ffmpeg, '-hide_banner', '-y', '-f','gdigrab','-framerate','15','-draw_mouse','0','-i','title='+title, '-an','-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-preset','veryfast','-crf','29','-pix_fmt','yuv420p','-movflags','+faststart','-progress',str(out / 'progress.txt'), str(out / 'gameplay.mp4')]
            recorder = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=flog, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
            data.update(game_pid=game.pid, recorder_pid=recorder.pid, hwnd=hwnd, window_title=title, command=command)
            for _ in range(120):
                if recorder.poll() is not None: raise RuntimeError('recorder exited; see ffmpeg.log')
                progress = out / 'progress.txt'
                if progress.exists() and any(line.startswith('frame=') and int(line.split('=')[1]) > 0 for line in progress.read_text().splitlines()): break
                time.sleep(.25)
            else: raise RuntimeError('recording has no frame progress')
            data.update(state='recording', recording_ready=True, recording_epoch=time.time()); save(state, data)
            while not (out / 'stop.request').exists() and game.poll() is None:
                if recorder.poll() is not None: raise RuntimeError('recorder exited during session')
                time.sleep(.5)
            recorder.communicate(b'q\n', timeout=45)
            if recorder.returncode: raise RuntimeError('ffmpeg finalization failed')
            # Close the exact game window only after recording has been finalized.
            if game.poll() is None:
                ctypes.windll.user32.PostMessageW(hwnd, 0x0010, 0, 0)
            probe = pathlib.Path(a.ffmpeg).with_name('ffprobe.exe')
            result = subprocess.run([str(probe), '-v','error','-show_streams','-show_format','-of','json',str(out / 'gameplay.mp4')], capture_output=True, text=True, check=True)
            (out / 'probe.json').write_text(result.stdout, encoding='utf-8')
            decode = subprocess.run([a.ffmpeg,'-v','error','-i',str(out / 'gameplay.mp4'),'-f','null','-'], capture_output=True, text=True)
            (out / 'decode.log').write_text(decode.stderr, encoding='utf-8')
            if decode.returncode: raise RuntimeError('video decode verification failed')
            import shutil
            shutil.copytree(data['session_directory'], out / 'session', dirs_exist_ok=False)
            data.update(state='complete', finished_epoch=time.time(), decode_verified=True, archived_session=str(out / 'session')); save(state, data)
    except Exception as error:
        if recorder and recorder.poll() is None:
            try: recorder.communicate(b'q\n',timeout=45)
            except Exception: recorder.kill()
        if game and game.poll() is None: game.terminate()
        data.update(state='failed', error=str(error), finished_epoch=time.time()); save(state, data)
        raise

if __name__ == '__main__': main()




