# GLBCompression

A local model preparation tool for FBX, GLB, and glTF: mesh reduction, texture resizing, UV rebaking, component/skeleton inspection, and manual polygon/brush component splitting.

**[Read the user guide](USER_GUIDE.md)** for use cases, all controls, character support, export behavior, and troubleshooting.

## Run

```bash
python3 server.py
```

Open <http://127.0.0.1:8765>. Blender is required for Blender decimation, UV processing, and FBX export.

## Build and verify

```bash
npm ci
npm run build
npm test
```

Keep reference images and original models; save reviewed game exports under the relevant asset folder. WorldGen placement metadata and client integration are separate from compression. See [portable integration](../docs/INTEGRATION.md).
