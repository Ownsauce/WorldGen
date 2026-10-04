#!/usr/bin/env python3
"""Build, validate, and package portable WorldGen GLB catalogs. Standard library only."""
import argparse
import hashlib
import json
import math
import re
import sys
import zipfile
from pathlib import Path, PurePosixPath
from glb_geometry import read_glb, normalized_triangles, triangle_intersects_box

SCHEMA = 1


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def vector(values):
    return dict(zip(('x', 'y', 'z'), values))


def xyz(value):
    if not isinstance(value, dict) or set(value) != {'x', 'y', 'z'}:
        raise ValueError('Expected x/y/z vector')
    result = [value[k] for k in ('x', 'y', 'z')]
    if any(isinstance(v, bool) or not isinstance(v, (int, float)) or not math.isfinite(v) for v in result):
        raise ValueError('Vector must contain finite numbers')
    return result


def relative_file(root, name):
    path = PurePosixPath(name)
    if not name or '\\' in name or ':' in name or path.is_absolute() or any(part in ('.', '..') for part in name.split('/')):
        raise ValueError(f'Unsafe relative path: {name}')
    result = (root / name).resolve()
    if not result.is_relative_to(root.resolve()):
        raise ValueError(f'Path leaves asset root: {name}')
    if not result.is_file():
        raise ValueError(f'Missing file: {name}')
    return result


def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n')


def doorway_metadata(dimensions, opening):
    w, h, d = dimensions
    ow, oh = opening
    if not (0 < ow < w and 0 < oh < h and d > 0):
        raise ValueError('Outer dimensions must exceed doorway opening')
    boxes = []
    for sign in (-1, 1):
        boxes.append({'centerMeters': vector([sign*(w+ow)/4, oh/2, 0]),
                      'sizeMeters': vector([(w-ow)/2, oh, d])})
    boxes.append({'centerMeters': vector([0, (h+oh)/2, 0]), 'sizeMeters': vector([w, h-oh, d])})
    return {
        'schemaVersion': SCHEMA, 'purpose': 'doorway-frame', 'dimensionsMeters': vector(dimensions),
        'pivot': 'bottom-center', 'allowedRotations': [0, 90, 180, 270],
        'placement': {'slot': 'doorway-frame', 'openingWidthMeters': ow, 'openingHeightMeters': oh},
        'clearance': {'centerMeters': vector([0, oh/2, 0]), 'sizeMeters': vector([ow, oh, d+0.4])},
        'collision': {'mode': 'boxes', 'boxes': boxes},
        'sockets': [
            {'id': 'front', 'positionMeters': vector([0, 0, d/2]), 'forward': vector([0, 0, 1]), 'widthMeters': ow, 'heightMeters': oh},
            {'id': 'back', 'positionMeters': vector([0, 0, -d/2]), 'forward': vector([0, 0, -1]), 'widthMeters': ow, 'heightMeters': oh}],
        'notes': 'Measured static mesh clearance; appearance and in-game traversal still require client review.'}


def validate_settings(entry):
    if not re.fullmatch(r'[a-z0-9][a-z0-9/-]*', entry['id']) or any(not p for p in entry['id'].split('/')):
        raise ValueError('Asset ID must be a lowercase folder path')
    if entry['purpose'] != 'doorway-frame' or entry['placement']['slot'] != 'doorway-frame':
        raise ValueError('Catalog v1 placement supports doorway frames; add explicit contracts before other purposes')
    dimensions = xyz(entry['dimensionsMeters'])
    if any(v <= 0 for v in dimensions) or entry['pivot'] != 'bottom-center':
        raise ValueError('Positive dimensions and bottom-center pivot are required')
    rotations = entry['allowedRotations']
    if not rotations or len(set(rotations)) != len(rotations) or any(type(v) is not int or v not in (0, 90, 180, 270) for v in rotations):
        raise ValueError('Allowed rotations must be unique quarter turns')
    w, h, d = dimensions
    ow, oh = entry['placement']['openingWidthMeters'], entry['placement']['openingHeightMeters']
    if not (0 < ow < w and 0 < oh < h):
        raise ValueError('Door opening exceeds outer bounds')
    clearance_center = xyz(entry['clearance']['centerMeters'])
    clearance_size = xyz(entry['clearance']['sizeMeters'])
    if clearance_center != [0, oh/2, 0] or clearance_size[0] != ow or clearance_size[1] != oh or clearance_size[2] < d:
        raise ValueError('Clearance must describe the centered doorway opening through its full depth')
    if entry['collision']['mode'] != 'boxes' or not entry['collision']['boxes']:
        raise ValueError('Explicit box collision proxies are required')
    for box in entry['collision']['boxes']:
        c, s = xyz(box['centerMeters']), xyz(box['sizeMeters'])
        if any(v <= 0 for v in s):
            raise ValueError('Collision box dimensions must be positive')
        for i, (lo, hi) in enumerate([(-w/2, w/2), (0, h), (-d/2, d/2)]):
            if c[i]-s[i]/2 < lo-1e-6 or c[i]+s[i]/2 > hi+1e-6:
                raise ValueError('Collision proxy exceeds normalized bounds')
        if all(abs(c[i]-clearance_center[i]) < (s[i]+clearance_size[i])/2-1e-6 for i in range(3)):
            raise ValueError('Collision proxy blocks doorway clearance')
    socket_ids = set()
    if len(entry['sockets']) != 2:
        raise ValueError('Doorway frame requires front and back sockets')
    for socket in entry['sockets']:
        if socket['id'] in socket_ids:
            raise ValueError('Duplicate socket ID')
        socket_ids.add(socket['id'])
        p, f = xyz(socket['positionMeters']), xyz(socket['forward'])
        if socket['id'] not in ('front', 'back'):
            raise ValueError('Doorway sockets must be front/back')
        sign = 1 if socket['id'] == 'front' else -1
        if any(abs(a-b) > 1e-6 for a,b in zip(p,[0,0,sign*d/2])) or f != [0,0,sign]:
            raise ValueError('Doorway sockets must sit at the front/back bottom center and face outward')
        if socket['widthMeters'] != ow or socket['heightMeters'] != oh:
            raise ValueError('Socket opening differs from placement clearance')


def inspect_asset(path, entry):
    triangles, minimum, maximum = read_glb(path)
    dims = xyz(entry['dimensionsMeters'])
    normalized = normalized_triangles(triangles, minimum, maximum, dims)
    center, size = xyz(entry['clearance']['centerMeters']), xyz(entry['clearance']['sizeMeters'])
    # Exclude only shared boundaries, not an interior tolerance band.
    size = [max(1e-9, v-2e-6) for v in size]
    blocked = sum(triangle_intersects_box(t, center, size) for t in normalized)
    if blocked:
        raise ValueError(f'{blocked} mesh triangles obstruct the declared opening; resize or repair the model')
    return {'minimum': vector(minimum), 'maximum': vector(maximum)}, len(triangles)


def build(root, version, only=None):
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]*', version):
        raise ValueError('Content version must be a nonempty filename-safe identifier')
    entries, errors = [], []
    found = set()
    for model in sorted(root.rglob('game.glb')):
        folder = model.parent.relative_to(root)
        if only and folder.as_posix() not in only:
            continue
        found.add(folder.as_posix())
        try:
            folder = model.parent.relative_to(root)
            if len(folder.parts) < 3:
                raise ValueError('Use theme/category/asset folders')
            metadata_path = model.with_name('asset.json')
            if not metadata_path.is_file():
                raise ValueError('Missing asset.json: dimensions, clearance, sockets, and collision must be explicit')
            settings = json.loads(metadata_path.read_text())
            if settings.pop('schemaVersion', None) != SCHEMA:
                raise ValueError('Unsupported asset metadata schema')
            reserved = {'id', 'theme', 'category', 'path', 'sha256', 'sourceBounds', 'triangles'}
            if reserved.intersection(settings):
                raise ValueError('Identity and measured fields are derived by the builder')
            entry = {'id': folder.as_posix(), 'theme': folder.parts[0], 'category': folder.parts[1],
                     'path': model.relative_to(root).as_posix(), 'sha256': sha256(model), **settings}
            validate_settings(entry)
            relative_file(root, entry['path'])
            entry['sourceBounds'], entry['triangles'] = inspect_asset(model, entry)
            entries.append(entry)
        except (ValueError, KeyError, TypeError, IndexError, OSError) as error:
            errors.append(f'{model.relative_to(root)}: {error}')
    if only and set(only)-found:
        errors.append('Selected asset IDs have no game.glb: '+', '.join(sorted(set(only)-found)))
    if errors:
        raise ValueError('\n'.join(errors))
    if not entries:
        raise ValueError('No game.glb assets found; originals and other filenames are intentionally excluded')
    return {'schemaVersion': SCHEMA, 'contentVersion': version, 'coordinateSystem': 'right-handed-y-up', 'assets': entries}


def validate(catalog, root):
    if catalog.get('schemaVersion') != SCHEMA or catalog.get('coordinateSystem') != 'right-handed-y-up' or not catalog.get('contentVersion'):
        raise ValueError('Unsupported catalog schema, coordinate system, or missing content version')
    if not catalog.get('assets'):
        raise ValueError('Catalog has no assets')
    seen, paths = set(), set()
    for entry in catalog['assets']:
        validate_settings(entry)
        if entry['id'] in seen or entry['path'] in paths:
            raise ValueError('Duplicate asset ID or GLB path')
        seen.add(entry['id']); paths.add(entry['path'])
        if entry['path'] != entry['id'] + '/game.glb' or entry['theme'] != entry['id'].split('/')[0] or entry['category'] != entry['id'].split('/')[1]:
            raise ValueError('Catalog identity does not match folder path')
        if not re.fullmatch(r'[0-9a-f]{64}', entry['sha256']):
            raise ValueError('Invalid SHA-256')
        model = relative_file(root, entry['path'])
        if sha256(model) != entry['sha256']:
            raise ValueError(f'Hash mismatch: {entry["id"]}')
        bounds, triangles = inspect_asset(model, entry)
        if bounds != entry['sourceBounds'] or triangles != entry['triangles']:
            raise ValueError(f'Measured bounds/triangle count changed: {entry["id"]}')
    return len(seen)


def package(root, catalog_path, output):
    catalog = json.loads(catalog_path.read_text())
    validate(catalog, root/'Assets')
    if output.exists():
        raise ValueError('Package output already exists; use a new version or filename')
    for name in ('WorldGen.Core', 'WorldGen.Geometry'):
        binary = root/'tmp/dotnet-build'/name/'bin/Release/netstandard2.0'/(name+'.dll')
        if not binary.is_file():
            raise ValueError('Build WorldGen.slnx in Release before packaging')
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix+'.tmp')
    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED) as archive:
        archive.writestr('catalog.json', json.dumps(catalog, indent=2)+'\n')
        archive.writestr('catalog.sha256', hashlib.sha256((json.dumps(catalog, indent=2)+'\n').encode()).hexdigest()+'\n')
        for entry in catalog['assets']:
            archive.write(relative_file(root/'Assets', entry['path']), 'Assets/'+entry['path'])
        for folder in ('WorldGen.Core', 'WorldGen.Geometry'):
            for path in sorted((root/folder).rglob('*')):
                if path.is_file() and not any(p in ('bin','obj','node_modules','__pycache__') for p in path.relative_to(root/folder).parts):
                    archive.write(path, path.relative_to(root).as_posix())
        for folder in ('tools', 'examples', 'Integrations'):
            for path in sorted((root/folder).rglob('*')):
                if path.is_file() and not any(p in ('bin','obj','node_modules','__pycache__') for p in path.relative_to(root/folder).parts):
                    archive.write(path, path.relative_to(root).as_posix())
        for name in ('WorldGen.slnx','Directory.Build.props','docs/INTEGRATION.md','docs/integration-verification.json','THIRD_PARTY.md'):
            archive.write(root/name,name)
        for name in ('WorldGen.Core.dll','WorldGen.Geometry.dll'):
            folder=name[:-4]
            binary=root/'tmp/dotnet-build'/folder/'bin/Release/netstandard2.0'/name
            if not binary.is_file():
                raise ValueError('Build WorldGen.slnx in Release before packaging')
            archive.write(binary,'lib/netstandard2.0/'+name)
        archive.writestr('README.md', '# WorldGen integration package\n\nContent version: '+catalog['contentVersion']+'\n\nSee docs/INTEGRATION.md. References, original GLBs, and developer compression tools are excluded. Libraries target netstandard2.0. Unity adapters and a .NET example are included.\n')
    temporary.replace(output)
    print(f'Packaged {len(catalog["assets"])} assets: {output}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    init = commands.add_parser('init-doorway', help='Write explicit doorway metadata without guessing its dimensions')
    init.add_argument('folder', type=Path); init.add_argument('--dimensions', nargs=3, type=float, required=True)
    init.add_argument('--opening', nargs=2, type=float, required=True)
    b = commands.add_parser('build'); b.add_argument('assets', type=Path); b.add_argument('output', type=Path); b.add_argument('--version', required=True); b.add_argument('--only', action='append', help='Include this asset ID; repeat for an explicit subset')
    v = commands.add_parser('validate'); v.add_argument('catalog', type=Path); v.add_argument('--assets', type=Path, required=True)
    p = commands.add_parser('package'); p.add_argument('catalog', type=Path); p.add_argument('output', type=Path); p.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent)
    args = parser.parse_args()
    try:
        if args.command == 'init-doorway':
            path = args.folder/'asset.json'
            if path.exists(): raise ValueError('asset.json already exists; edit it rather than overwriting')
            dump(path, doorway_metadata(args.dimensions, args.opening)); print(f'Created {path}')
        elif args.command == 'build':
            catalog = build(args.assets.resolve(), args.version, args.only)
            validate(catalog, args.assets.resolve()); dump(args.output, catalog)
            print(f'Built {len(catalog["assets"])} assets: {args.output}')
        elif args.command == 'validate':
            print(f'Validated {validate(json.loads(args.catalog.read_text()), args.assets.resolve())} assets')
        elif args.command == 'package': package(args.root.resolve(), args.catalog.resolve(), args.output.resolve())
    except (ValueError, KeyError, TypeError, IndexError, OSError) as error:
        print(f'ERROR: {error}', file=sys.stderr); return 1
    return 0

if __name__ == '__main__':
    sys.exit(main())
