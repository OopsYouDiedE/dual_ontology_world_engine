"""PID-directed real-console CLI acceptance bridge; Windows + Pillow required."""
import ctypes as C
from ctypes import wintypes as W
import json, os, pathlib, subprocess, sys, time

k = C.WinDLL('kernel32', use_last_error=True)
u = C.WinDLL('user32', use_last_error=True)
g = C.WinDLL('gdi32', use_last_error=True)
k.GetConsoleWindow.restype = W.HWND
k.AttachConsole.argtypes = [W.DWORD]
k.GetStdHandle.argtypes = [W.DWORD]
k.GetStdHandle.restype = W.HANDLE
k.CreateFileW.argtypes = [W.LPCWSTR, W.DWORD, W.DWORD, C.c_void_p, W.DWORD, W.DWORD, W.HANDLE]
k.CreateFileW.restype = W.HANDLE
k.CloseHandle.argtypes = [W.HANDLE]
u.GetWindowDC.argtypes = [W.HWND]; u.GetWindowDC.restype = W.HDC
u.ReleaseDC.argtypes = [W.HWND, W.HDC]
u.GetWindowRect.argtypes = [W.HWND, C.POINTER(W.RECT)]
u.PrintWindow.argtypes = [W.HWND, W.HDC, W.UINT]
g.CreateCompatibleDC.argtypes = [W.HDC]; g.CreateCompatibleDC.restype = W.HDC
g.CreateCompatibleBitmap.argtypes = [W.HDC, C.c_int, C.c_int]; g.CreateCompatibleBitmap.restype = W.HBITMAP
g.SelectObject.argtypes = [W.HDC, W.HANDLE]; g.SelectObject.restype = W.HANDLE
g.DeleteObject.argtypes = [W.HANDLE]
g.DeleteDC.argtypes = [W.HDC]

class CHAR(C.Union):
    _fields_ = [('UnicodeChar', W.WCHAR), ('AsciiChar', C.c_char)]
class KEY(C.Structure):
    _fields_ = [('bKeyDown', W.BOOL), ('wRepeatCount', W.WORD), ('wVirtualKeyCode', W.WORD), ('wVirtualScanCode', W.WORD), ('uChar', CHAR), ('dwControlKeyState', W.DWORD)]
class EVENT(C.Union):
    _fields_ = [('KeyEvent', KEY), ('padding', C.c_byte * 16)]
class INPUT(C.Structure):
    _fields_ = [('EventType', W.WORD), ('Event', EVENT)]
k.WriteConsoleInputW.argtypes = [W.HANDLE, C.POINTER(INPUT), W.DWORD, C.POINTER(W.DWORD)]

class COORD(C.Structure):
    _fields_ = [('X', W.SHORT), ('Y', W.SHORT)]
class SMALLRECT(C.Structure):
    _fields_ = [('Left', W.SHORT), ('Top', W.SHORT), ('Right', W.SHORT), ('Bottom', W.SHORT)]
class INFO(C.Structure):
    _fields_ = [('dwSize', COORD), ('dwCursorPosition', COORD), ('wAttributes', W.WORD), ('srWindow', SMALLRECT), ('dwMaximumWindowSize', COORD)]
class CELL(C.Structure):
    _fields_ = [('Char', CHAR), ('Attributes', W.WORD)]
k.GetConsoleScreenBufferInfo.argtypes = [W.HANDLE, C.POINTER(INFO)]
k.ReadConsoleOutputW.argtypes = [W.HANDLE, C.POINTER(CELL), COORD, COORD, C.POINTER(SMALLRECT)]

def viewport(pid):
    k.FreeConsole(); checked(k.AttachConsole(pid)); handle = None
    try:
        handle = k.CreateFileW('CONOUT$', 0x80000000, 3, None, 3, 0, None)
        if handle == W.HANDLE(-1).value: raise C.WinError(C.get_last_error())
        info = INFO(); checked(k.GetConsoleScreenBufferInfo(handle,C.byref(info)))
        rect = info.srWindow; width=rect.Right-rect.Left+1; height=rect.Bottom-rect.Top+1
        cells = (CELL*(width*height))()
        checked(k.ReadConsoleOutputW(handle,cells,COORD(width,height),COORD(0,0),C.byref(rect)))
        rows = [''.join(cell.Char.UnicodeChar for cell in cells[row*width:(row+1)*width] if not cell.Attributes & 0x0200).rstrip() for row in range(height)]
        return '\n'.join(rows).rstrip()
    finally:
        if handle is not None and handle != W.HANDLE(-1).value: k.CloseHandle(handle)
        k.FreeConsole()

k.GetNumberOfConsoleInputEvents.argtypes = [W.HANDLE, C.POINTER(W.DWORD)]
k.GetConsoleMode.argtypes = [W.HANDLE, C.POINTER(W.DWORD)]

def snapshot(pid):
    # A redraw can replace/resize the console buffer between API calls. Only
    # retry this read operation; never retry delivery of a player command.
    for attempt in range(3):
        try:
            result = snapshot_once(pid)
            result['read_retries'] = attempt
            return result
        except OSError as error:
            if error.winerror not in (1, 6, 87) or attempt == 2:
                raise
            time.sleep(.02)


def snapshot_once(pid):
    """Read the actual console, including the cursor's current prompt position."""
    k.FreeConsole(); checked(k.AttachConsole(pid)); handles=[]
    try:
        out = k.CreateFileW('CONOUT$',0x80000000,3,None,3,0,None); handles.append(out)
        inp = k.CreateFileW('CONIN$',0x80000000,3,None,3,0,None); handles.append(inp)
        if any(h == W.HANDLE(-1).value for h in handles): raise C.WinError(C.get_last_error())
        info=INFO(); checked(k.GetConsoleScreenBufferInfo(out,C.byref(info)))
        rect=info.srWindow; width=rect.Right-rect.Left+1; height=rect.Bottom-rect.Top+1
        cells=(CELL*(width*height))()
        checked(k.ReadConsoleOutputW(out,cells,COORD(width,height),COORD(0,0),C.byref(rect)))
        rows=[''.join(c.Char.UnicodeChar for c in cells[r*width:(r+1)*width] if not c.Attributes&0x0200).rstrip() for r in range(height)]
        cursor=info.dwCursorPosition; prefix=''
        if cursor.X:
            line=(CELL*cursor.X)(); line_rect=SMALLRECT(0,cursor.Y,cursor.X-1,cursor.Y)
            checked(k.ReadConsoleOutputW(out,line,COORD(cursor.X,1),COORD(0,0),C.byref(line_rect)))
            prefix=''.join(c.Char.UnicodeChar for c in line if not c.Attributes&0x0200)
        pending=W.DWORD(); mode=W.DWORD()
        checked(k.GetNumberOfConsoleInputEvents(inp,C.byref(pending)))
        checked(k.GetConsoleMode(inp,C.byref(mode)))
        after=INFO(); checked(k.GetConsoleScreenBufferInfo(out,C.byref(after)))
        stable = (bytes(info) == bytes(after))
        return {'text':'\n'.join(rows).rstrip(),'cursor':{'x':cursor.X,'y':cursor.Y},
                'viewport':{'left':rect.Left,'top':rect.Top,'width':width,'height':height},
                'cursor_prefix':prefix,'pending_input_events':pending.value,'input_mode':mode.value,
                'read_stable':stable,'ready':stable and prefix.endswith('dv> ') and pending.value==0}
    finally:
        for h in handles:
            if h is not None and h!=W.HANDLE(-1).value: k.CloseHandle(h)
        k.FreeConsole()

def checked(ok):
    if not ok: raise C.WinError(C.get_last_error())
    return ok

def send(pid, text):
    # Attach by PID, never use foreground focus, SendKeys, or global keyboard.
    k.FreeConsole()
    checked(k.AttachConsole(pid))
    h = None
    try:
        # CONIN$ is explicit: redirected parent stdin may survive AttachConsole.
        h = k.CreateFileW('CONIN$', 0xC0000000, 3, None, 3, 0, None)
        if h == W.HANDLE(-1).value: raise C.WinError(C.get_last_error())
        records = []
        for char in text + '\r':
            for down in (True, False):
                r = INPUT(); r.EventType = 1
                r.Event.KeyEvent = KEY(down, 1, 13 if char == '\r' else 0, 0, CHAR(UnicodeChar=char), 0)
                records.append(r)
        data = (INPUT * len(records))(*records); count = W.DWORD()
        checked(k.WriteConsoleInputW(h, data, len(data), C.byref(count)))
        if count.value != len(data): raise RuntimeError('Partial input write')
    finally:
        if h is not None and h != W.HANDLE(-1).value: k.CloseHandle(h)
        k.FreeConsole()

class BMIHEADER(C.Structure):
    _fields_ = [('biSize', W.DWORD), ('biWidth', W.LONG), ('biHeight', W.LONG), ('biPlanes', W.WORD), ('biBitCount', W.WORD), ('biCompression', W.DWORD), ('biSizeImage', W.DWORD), ('biXPelsPerMeter', W.LONG), ('biYPelsPerMeter', W.LONG), ('biClrUsed', W.DWORD), ('biClrImportant', W.DWORD)]
g.GetDIBits.argtypes = [W.HDC, W.HBITMAP, W.UINT, W.UINT, C.c_void_p, C.c_void_p, W.UINT]

def capture(hwnd, path):
    # PrintWindow emits physical pixels; a DPI-virtualized GetWindowRect would
    # allocate a smaller bitmap and crop its right and bottom at desktop scaling.
    u.SetThreadDpiAwarenessContext.argtypes = [C.c_void_p]
    u.SetThreadDpiAwarenessContext.restype = C.c_void_p
    previous = u.SetThreadDpiAwarenessContext(C.c_void_p(-4))
    checked(previous)
    try:
        return capture_physical(hwnd, path)
    finally:
        checked(u.SetThreadDpiAwarenessContext(previous))


def capture_physical(hwnd, path):
    from PIL import Image, ImageStat
    rect = W.RECT(); checked(u.GetWindowRect(hwnd, C.byref(rect)))
    width, height = rect.right-rect.left, rect.bottom-rect.top
    if width < 20 or height < 20: raise RuntimeError('Invalid window extent')
    dc = u.GetWindowDC(hwnd); checked(dc)
    memory = g.CreateCompatibleDC(dc); bitmap = g.CreateCompatibleBitmap(dc, width, height)
    old = g.SelectObject(memory, bitmap)
    try:
        checked(u.PrintWindow(hwnd, memory, 2))
        g.SelectObject(memory, old)
        header = BMIHEADER(); header.biSize = C.sizeof(header); header.biWidth = width; header.biHeight = -height; header.biPlanes = 1; header.biBitCount = 32
        pixels = C.create_string_buffer(width*height*4)
        checked(g.GetDIBits(memory, bitmap, 0, height, pixels, C.byref(header), 0))
        image = Image.frombuffer('RGB', (width,height), pixels.raw, 'raw', 'BGRX', 0, 1)
        image.save(path, 'JPEG', quality=95)
        return {'path':str(path), 'width':width, 'height':height, 'stddev':ImageStat.Stat(image).stddev,
                'coordinate_space':'physical_pixels', 'dpi_awareness':'per_monitor_v2'}
    finally:
        g.SelectObject(memory, old); g.DeleteObject(bitmap); g.DeleteDC(memory); u.ReleaseDC(hwnd,dc)

# One independently-created DVLOG2 per operation: RecordWriter opens with xb.
import argparse, contextlib, datetime, hashlib, re
from binary_evidence import RecordWriter

def stamp():
    return {'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'monotonic_ns':time.monotonic_ns()}

def digest(path):
    result=hashlib.sha256()
    with pathlib.Path(path).open('rb') as source:
        while block:=source.read(1024*1024): result.update(block)
    return result.hexdigest()

def save_json(path,value):
    temporary=path.with_suffix(path.suffix+'.tmp')
    temporary.write_text(json.dumps(value,ensure_ascii=False,indent=2),encoding='utf-8')
    os.replace(temporary,path)

def read_json(path):
    return json.loads(path.read_text(encoding='utf-8'))

@contextlib.contextmanager
def locked(folder):
    path=folder/'bridge.lock'
    try: descriptor=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL)
    except FileExistsError: raise RuntimeError('This session already has an in-flight operation; do not retry its input.')
    try:
        os.write(descriptor,str(os.getpid()).encode()); os.close(descriptor)
        yield
    finally: path.unlink(missing_ok=True)

def runtime(folder):
    path=folder/'runtime.json'
    return read_json(path) if path.exists() else {}

def context(manifest,run):
    return {'source_revision':manifest['source_revision'],'assembly_sha256':manifest['assembly_sha256'],
            'bridge_sha256':manifest['bridge_sha256'],'pid':run.get('wrapper_pid'),'game_pid':run.get('game_pid'),
            'operation_bridge_sha256':digest(__file__),'hwnd':run.get('hwnd'),'stage':manifest['stage']}

def evidence(writer,manifest,run,kind,**fields):
    value={'kind':kind,**stamp(),**context(manifest,run),**fields}
    writer.append(value); writer.stream.flush()
    return value

def frame(folder,manifest,run,writer,name):
    visible=snapshot(run['wrapper_pid'])
    if run.get('game_exit') is not None: visible.update(ready=False,exited=True)
    image=capture(run['hwnd'],folder/(name+'.jpg'))
    image['sha256']=digest(image['path']); image['capture_api']='PrintWindow'
    evidence(writer,manifest,run,'native_console_frame',label=name,image=image,visible=visible)
    return {'image':image,**visible}

def wait_prompt(folder,pid,timeout):
    deadline=time.monotonic()+timeout; seen=0; last=None
    while True:
        last=snapshot(pid)
        if runtime(folder).get('game_exit') is not None: return {**last,'ready':False,'exited':True}
        if last['ready']:
            seen+=1
            if seen>=2: return last
        else: seen=0
        if time.monotonic()>=deadline: raise TimeoutError('No new dv> prompt before deadline; input is NOT retried.')
        time.sleep(.05)

def session_folder(value):
    folder=pathlib.Path(value).resolve()
    if not (folder/'manifest.json').exists(): raise RuntimeError('Not a bridge session: '+str(folder))
    return folder

def configure_console():
    """Set the real native viewport to the acceptance session's 120x42 cells."""
    k.SetConsoleScreenBufferSize.argtypes=[W.HANDLE,COORD]
    k.SetConsoleWindowInfo.argtypes=[W.HANDLE,W.BOOL,C.POINTER(SMALLRECT)]
    handle=k.CreateFileW('CONOUT$',0xC0000000,3,None,3,0,None)
    if handle==W.HANDLE(-1).value: raise C.WinError(C.get_last_error())
    try:
        info=INFO(); checked(k.GetConsoleScreenBufferInfo(handle,C.byref(info)))
        # Grow first, resize the viewport, then settle the exact column width.
        checked(k.SetConsoleScreenBufferSize(handle,COORD(max(120,info.dwSize.X),max(1000,info.dwSize.Y))))
        rect=SMALLRECT(0,0,119,41)
        checked(k.SetConsoleWindowInfo(handle,True,C.byref(rect)))
        checked(k.SetConsoleScreenBufferSize(handle,COORD(120,max(1000,info.dwSize.Y))))
    finally: k.CloseHandle(handle)

def child(folder):
    """Run only the configured real CLI, inheriting native console stdin/stdout."""
    folder=pathlib.Path(folder); manifest=read_json(folder/'manifest.json')
    k.SetConsoleTitleW('DV-CLI '+folder.name)
    if hasattr(sys.stdout,'reconfigure'): sys.stdout.reconfigure(encoding='utf-8')
    run={'wrapper_pid':os.getpid(),'hwnd':int(k.GetConsoleWindow()),'created':stamp()}
    save_json(folder/'runtime.json',run)
    try:
        configure_console()
        process=subprocess.Popen(manifest['argv'],cwd=manifest['repo'])
        run['game_pid']=process.pid; save_json(folder/'runtime.json',run)
        run['game_exit']=process.wait(); run['exited']=stamp()
    except BaseException as error:
        run['game_exit']=-1; run['error']=repr(error); print(repr(error),flush=True)
    save_json(folder/'runtime.json',run)
    # Retain the actual final pixels until the controller captures and releases.
    while not (folder/'release.flag').exists(): time.sleep(.1)

def start(args):
    if args.stage == 'player' and args.controller != 'llm':
        raise ValueError('Blind-player sessions require controller=llm.')
    folder=pathlib.Path(args.session).resolve(); repo=pathlib.Path(args.repo).resolve()
    folder.mkdir(parents=True,exist_ok=True)
    if any(folder.iterdir()): raise RuntimeError('Session folder must be empty; never overwrite an existing game/evidence.')
    assembly=repo/'dv_core/cli/bin/Release/net8.0/dv-cli.dll'
    if not assembly.is_file(): raise FileNotFoundError(assembly)
    argv=['dotnet',str(assembly),'--tui','--seed','1','--width','120','--height','42','--controller',args.controller,'--profile',args.profile,
          '--zone',args.zone,'--viewer',args.viewer,'--planning','approximate',
          '--trace',str(folder/'session.dvdag'),'--save',str(folder/'session.dvsave'),
          '--performance',str(folder/'engine'),'--source-revision',args.source_revision]
    if args.stage == 'player':
        argv += ['--map','subjective','--no-scores']
    manifest={'version':1,'repo':str(repo),'session':str(folder),'stage':args.stage,
              'source_revision':args.source_revision,'assembly_sha256':digest(assembly),
              'bridge_sha256':digest(__file__),'argv':argv,'created':stamp(),'steps':{},'pending':None}
    save_json(folder/'manifest.json',manifest)
    conhost=os.path.join(os.environ['SystemRoot'],'System32','conhost.exe')
    process=subprocess.Popen([conhost,'-ForceNoHandoff','--',sys.executable,str(pathlib.Path(__file__).resolve()),'_child',str(folder)],creationflags=subprocess.CREATE_NEW_CONSOLE)
    manifest['conhost_pid']=process.pid; save_json(folder/'manifest.json',manifest)
    with RecordWriter(folder/'start.dvlog') as writer:
        deadline=time.monotonic()+args.timeout; run={}
        try:
            while not (run:=runtime(folder)).get('game_pid'):
                if run.get('game_exit') is not None: raise RuntimeError(run.get('error','CLI failed before startup'))
                if time.monotonic()>=deadline: raise TimeoutError('Startup timed out; do not start a replacement process.')
                time.sleep(.05)
            evidence(writer,manifest,run,'process_started',argv=argv,conhost_pid=process.pid)
            wait_prompt(folder,run['wrapper_pid'],max(.1,deadline-time.monotonic()))
            result=frame(folder,manifest,run,writer,'start')
            return {'session':str(folder),'runtime':run,'frame':result}
        except BaseException as error:
            evidence(writer,manifest,run,'startup_failed',error=repr(error))
            raise

def validate_player_input(text):
    parts=text.strip().lower().split(maxsplit=1)
    verb=parts[0] if parts else ''
    argument=parts[1].strip() if len(parts)>1 else ''
    if verb in ('view','views','inspect','tp','time'):
        raise ValueError('Blind-player bridge forbids observer changes and developer inspection/mutation.')
    if verb == 'map' and argument != 'subjective':
        raise ValueError('Blind-player map must explicitly remain subjective.')
    if verb == 'scores' and argument != 'off':
        raise ValueError('Blind-player sessions do not expose planner scores.')


def command(args):
    folder=session_folder(args.session)
    if not re.fullmatch(r'[A-Za-z0-9_.:-]{1,100}',args.command_id): raise ValueError('Use a short safe command-id')
    if '\n' in args.text or '\r' in args.text: raise ValueError('Exactly one normal CLI input line per command')
    with locked(folder):
        manifest=read_json(folder/'manifest.json'); run=runtime(folder)
        if manifest['stage'] == 'player': validate_player_input(args.text)
        if args.command_id in manifest['steps']:
            return {'duplicate_not_delivered':True,'step':manifest['steps'][args.command_id]}
        if run.get('game_exit') is not None: raise RuntimeError('CLI has exited; no input delivered to its retained window.')
        if manifest['pending']: raise RuntimeError('Earlier input is unresolved. Use capture to wait; never resend it.')
        before=snapshot(run['wrapper_pid'])
        if not before['ready']: raise RuntimeError('CLI is not at dv> prompt; no input delivered.')
        if before['input_mode']&6 != 6: raise RuntimeError('Normal console line+echo input is required for new-prompt tracking.')
        number=len(manifest['steps'])+1; prefix=f'{number:04d}'
        step={'number':number,'command_id':args.command_id,'input_sha256':hashlib.sha256(args.text.encode('utf-8')).hexdigest(),'state':'preparing','created':stamp(),
              'evidence':str(folder/(prefix+'.dvlog'))}
        manifest['steps'][args.command_id]=step; save_json(folder/'manifest.json',manifest)
        with RecordWriter(step['evidence']) as writer:
            try:
                frame(folder,manifest,run,writer,prefix+'-before')
                step['state']='delivery_unknown'; manifest['pending']=args.command_id
                save_json(folder/'manifest.json',manifest)
                evidence(writer,manifest,run,'stdin_delivery_intent',command_id=args.command_id,text=args.text,transport='WriteConsoleInputW')
                send(run['wrapper_pid'],args.text)
                evidence(writer,manifest,run,'stdin_written',command_id=args.command_id,text=args.text,characters=len(args.text)+1)
                # Echo moves the old prompt cursor before the subsequent prompt.
                wait_prompt(folder,run['wrapper_pid'],args.timeout)
                run=runtime(folder)
                suffix='focus-after' if args.text.startswith('focus ') else 'confirm-after' if args.text=='confirm' else 'next-after' if args.text=='next' else 'after'
                result=frame(folder,manifest,run,writer,prefix+'-'+suffix)
                step['state']='process_exited' if run.get('game_exit') is not None else 'prompt_returned'; manifest['pending']=None
                step['completed']=stamp(); save_json(folder/'manifest.json',manifest)
                return {'step':step,'frame':result}
            except BaseException as error:
                evidence(writer,manifest,run,'command_incomplete',command_id=args.command_id,error=repr(error))
                step['error']=repr(error)
                if step['state']=='preparing': step['state']='not_delivered'
                else: step['state']='unresolved'
                save_json(folder/'manifest.json',manifest)
                try: frame(folder,manifest,run,writer,prefix+'-partial')
                except BaseException as capture_error: evidence(writer,manifest,run,'capture_failed',error=repr(capture_error))
                raise

def capture_session(args):
    folder=session_folder(args.session)
    with locked(folder):
        manifest=read_json(folder/'manifest.json'); run=runtime(folder)
        if args.wait_ready: wait_prompt(folder,run['wrapper_pid'],args.wait_ready)
        prefix='capture-'+str(time.time_ns())
        with RecordWriter(folder/(prefix+'.dvlog')) as writer:
            result=frame(folder,manifest,run,writer,prefix)
            if manifest['pending'] and (result['ready'] or run.get('game_exit') is not None):
                key=manifest['pending']; step=manifest['steps'][key]
                step['state']='resolved_by_capture'; step['resolution']=prefix; step['resolved']=stamp()
                evidence(writer,manifest,run,'prior_input_resolved',command_id=key)
                manifest['pending']=None; save_json(folder/'manifest.json',manifest)
            return {'session':str(folder),'frame':result,'pending':manifest['pending']}

def autonext(args):
    folder=session_folder(args.session); manifest=read_json(folder/'manifest.json')
    if manifest['stage']!='ai': raise RuntimeError('autonext is forbidden in blind-player sessions.')
    results=[]
    for number in range(args.count):
        result=command(argparse.Namespace(session=args.session,command_id=f'{args.run_id}:{number+1}',text='next',timeout=args.timeout))
        results.append({'command_id':f'{args.run_id}:{number+1}','step':result['step']})
        if result['step']['state'] not in ('prompt_returned',): break
    return {'session':args.session,'steps':results}

def stop(args):
    folder=session_folder(args.session); run=runtime(folder)
    result=None
    if run.get('game_exit') is None:
        result=command(argparse.Namespace(session=args.session,command_id=args.command_id,text='quit',timeout=args.timeout))
    deadline=time.monotonic()+args.timeout
    while (run:=runtime(folder)).get('game_exit') is None:
        if time.monotonic()>=deadline: raise TimeoutError('CLI quit is still sealing; not resent or forcibly killed.')
        time.sleep(.1)
    final=capture_session(argparse.Namespace(session=args.session,wait_ready=0))
    (folder/'release.flag').touch()
    return {'quit':result,'final':final,'game_exit':run['game_exit']}

def main():
    parser=argparse.ArgumentParser(description=__doc__); sub=parser.add_subparsers(dest='operation',required=True)
    p=sub.add_parser('start'); p.add_argument('--repo',required=True); p.add_argument('--session',required=True)
    p.add_argument('--source-revision',required=True); p.add_argument('--stage',choices=['ai','player'],required=True)
    p.add_argument('--controller',choices=['ai','llm'],required=True); p.add_argument('--profile',choices=['Standard_Detective','Foolish_Detective'],required=True)
    p.add_argument('--zone',choices=['zone_yi','zone_jia','zone_bing','zone_ding','none'],required=True)
    p.add_argument('--viewer',required=True); p.add_argument('--timeout',type=float,default=60)
    p=sub.add_parser('command'); p.add_argument('--session',required=True); p.add_argument('--command-id',required=True); p.add_argument('--text',required=True); p.add_argument('--timeout',type=float,default=60)
    p=sub.add_parser('capture'); p.add_argument('--session',required=True); p.add_argument('--wait-ready',type=float,default=0)
    p=sub.add_parser('autonext'); p.add_argument('--session',required=True); p.add_argument('--run-id',required=True); p.add_argument('--count',type=int,required=True); p.add_argument('--timeout',type=float,default=60)
    p=sub.add_parser('stop'); p.add_argument('--session',required=True); p.add_argument('--command-id',required=True); p.add_argument('--timeout',type=float,default=60)
    p=sub.add_parser('_child'); p.add_argument('session')
    args=parser.parse_args()
    handlers={'start':start,'command':command,'capture':capture_session,'autonext':autonext,'stop':stop,'_child':lambda a:child(a.session)}
    result=handlers[args.operation](args)
    if result is not None: print(json.dumps(result,ensure_ascii=True),flush=True)

if __name__=='__main__': main()
