from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import json
import os
import shutil
import struct
import subprocess
import tempfile
import threading
ROOT = Path(__file__).resolve().parent
EXPORT_LOCK = threading.Lock()
class Handler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path == '/api/status':
            contents = json.dumps({'version': 2, 'features': ['fbx-export', 'mesh-reduction', 'uv-unwrapping']}).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Cache-Control', 'no-store')
            self.send_header('Content-Length', str(len(contents)))
            self.end_headers(); self.wfile.write(contents)
            return
        super().do_GET()
    def do_POST(self):
        if self.path not in ('/export-fbx', '/reduce-glb', '/unwrap-glb'):
            self.send_error(404); return
        origin = self.headers.get('Origin')
        if origin and origin not in ('http://127.0.0.1:8765', 'http://localhost:8765'):
            self.send_error(403); return
        try:
            reducing = self.path == '/reduce-glb'
            unwrapping = self.path == '/unwrap-glb'
            bake_size = int(self.headers.get('X-Bake-Size', '1024'))
            if bake_size not in (256, 512, 1024, 2048):
                raise ValueError('Unsupported UV bake resolution')
            ratio = float(self.headers.get('X-Mesh-Ratio', '1'))
            if not .01 <= ratio <= 1:
                raise ValueError('Mesh percentage must be 1–100')
            size = int(self.headers.get('Content-Length', '0'))
            if size < 20 or size > 256 * 1024 * 1024:
                raise ValueError('Selected GLB must be between 20 bytes and 256 MiB')
            data = self.rfile.read(size)
            if len(data) != size or struct.unpack_from('<III', data) != (0x46546c67, 2, size):
                raise ValueError('Expected a complete GLB 2.0 file')
            blender = shutil.which(os.environ.get('WORLDGEN_BLENDER', 'blender'))
            if not blender:
                raise ValueError('This operation requires Blender. Install it or set WORLDGEN_BLENDER to its executable.')
            if not EXPORT_LOCK.acquire(blocking=False):
                self.send_error(409, 'A Blender operation is already running'); return
            try:
                with tempfile.TemporaryDirectory(prefix='worldgen-fbx-') as folder:
                    work = Path(folder)
                    source, output = work/'selected.glb', work/('selected.glb.out.glb' if unwrapping else 'reduced.glb' if reducing else 'selected.fbx')
                    source.write_bytes(data)
                    script = ROOT/('blender_unwrap.py' if unwrapping else 'blender_reduce.py' if reducing else 'blender_export.py')
                    args = [blender, '--background', '--factory-startup', '--python', str(script), '--', str(source), str(output)]
                    if reducing: args.append(str(ratio))
                    if unwrapping: args.append(str(bake_size))
                    result = subprocess.run(args,
                        cwd=work, capture_output=True, text=True, timeout=600 if unwrapping else 180)
                    if result.returncode != 0 or not output.is_file():
                        raise ValueError('Blender export failed: ' + (result.stderr or result.stdout)[-1800:])
                    contents = output.read_bytes()
            finally:
                EXPORT_LOCK.release()
            self.send_response(200)
            self.send_header('Content-Type', 'application/octet-stream')
            self.send_header('Content-Length', str(len(contents)))
            self.end_headers(); self.wfile.write(contents)
        except (ValueError, OSError, subprocess.TimeoutExpired) as error:
            contents = json.dumps({'error': str(error)}).encode()
            self.send_response(400)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', str(len(contents)))
            self.end_headers(); self.wfile.write(contents)
if __name__ == '__main__':
    os.chdir(ROOT)
    print('GLBCompression viewer: http://127.0.0.1:8765', flush=True)
    ThreadingHTTPServer(('127.0.0.1', 8765), Handler).serve_forever()
