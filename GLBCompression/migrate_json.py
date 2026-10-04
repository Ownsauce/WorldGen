"""Convert legacy .model.json packages to GLB plus small metadata."""
import argparse
import base64
import hashlib
import json
from pathlib import Path


def convert(path: Path) -> None:
    package = json.loads(path.read_text(encoding='utf-8'))
    encoded = package.pop('glbBase64', None)
    if not isinstance(encoded, str):
        raise ValueError(f'{path}: no glbBase64 field')
    data = base64.b64decode(encoded, validate=True)
    actual = hashlib.sha256(data).hexdigest()
    if actual != package.get('sha256'):
        raise ValueError(f'{path}: GLB SHA-256 mismatch')
    output = path.with_name(path.name.removesuffix('.model.json') + '.glb')
    metadata = output.with_suffix('.meta.json')
    if output.exists() or metadata.exists():
        raise FileExistsError(f'{output} or {metadata} already exists')
    package['format'] = 'glb-metadata'
    output.write_bytes(data)
    metadata.write_text(json.dumps(package, indent=2) + '\n', encoding='utf-8')
    print(f'{path.name} -> {output.name}, {metadata.name}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path, help='A legacy .model.json file or directory of them')
    args = parser.parse_args()
    paths = sorted(args.input.glob('*.model.json')) if args.input.is_dir() else [args.input]
    if not paths:
        parser.error('No .model.json files found')
    for item in paths:
        convert(item)
