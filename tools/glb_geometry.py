"""Strict static, self-contained GLB reader used for bounds and clearance checks."""
import itertools
import json
import math
import struct


def multiply(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def transform(m, p):
    return tuple(sum(m[i][j] * p[j] for j in range(3)) + m[i][3] for i in range(3))


def node_matrix(node):
    if 'matrix' in node:
        values = node['matrix']
        return [[values[j * 4 + i] for j in range(4)] for i in range(4)]
    x, y, z, w = node.get('rotation', [0, 0, 0, 1])
    scale = node.get('scale', [1, 1, 1])
    translation = node.get('translation', [0, 0, 0])
    rows = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
            [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
            [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
    return [[rows[i][j]*scale[j] for j in range(3)] + [translation[i]] for i in range(3)] + [[0, 0, 0, 1]]


def read_glb(path):
    blob = path.read_bytes()
    if len(blob) < 20 or struct.unpack_from('<III', blob) != (0x46546c67, 2, len(blob)):
        raise ValueError(f'{path}: invalid GLB 2 header')
    chunks = []
    offset = 12
    while offset < len(blob):
        if offset + 8 > len(blob):
            raise ValueError('Truncated GLB chunk')
        length, kind = struct.unpack_from('<II', blob, offset)
        offset += 8
        if length % 4 or offset + length > len(blob):
            raise ValueError('Invalid GLB chunk size')
        chunks.append((kind, blob[offset:offset+length]))
        offset += length
    if not chunks or chunks[0][0] != 0x4e4f534a:
        raise ValueError('GLB JSON must be the first chunk')
    doc = json.loads(chunks[0][1])
    binary = next((data for kind, data in chunks if kind == 0x004e4942), b'')
    if doc.get('asset', {}).get('version') != '2.0':
        raise ValueError('Only glTF 2.0 is supported')
    if doc.get('extensionsRequired'):
        raise ValueError('Required GLB extensions are not supported by the portable validator')
    if doc.get('animations') or doc.get('skins'):
        raise ValueError('This catalog version supports static assets only')
    buffers = doc.get('buffers', [])
    if len(buffers) != 1 or buffers[0].get('uri') or buffers[0]['byteLength'] > len(binary):
        raise ValueError('GLB must embed a single binary buffer')
    for image in doc.get('images', []):
        if 'uri' in image:
            raise ValueError('GLB images must be embedded buffer views')
    views = doc.get('bufferViews', [])
    for view in views:
        if view.get('buffer', 0) != 0 or view.get('byteOffset', 0) + view['byteLength'] > len(binary):
            raise ValueError('Invalid buffer view')

    def accessor(index):
        a = doc['accessors'][index]
        if 'sparse' in a or 'bufferView' not in a or a.get('normalized'):
            raise ValueError('Sparse or normalized geometry accessors are unsupported')
        fmt, size = {5121: ('B', 1), 5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}[a['componentType']]
        width = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        view = views[a['bufferView']]
        stride = view.get('byteStride', size * width)
        local = a.get('byteOffset', 0)
        if stride < size * width or local + max(0, a['count']-1)*stride + size*width > view['byteLength']:
            raise ValueError('Accessor exceeds its buffer view')
        start = view.get('byteOffset', 0) + local
        return [struct.unpack_from('<' + fmt * width, binary, start+i*stride) for i in range(a['count'])]

    triangles = []
    identity = [[int(i == j) for j in range(4)] for i in range(4)]
    nodes = doc.get('nodes', [])
    scene = doc.get('scene', 0)
    if not doc.get('scenes'):
        raise ValueError('GLB has no scene')

    def visit(index, parent, ancestors):
        if index in ancestors:
            raise ValueError('Cyclic node hierarchy')
        node = nodes[index]
        if 'skin' in node:
            raise ValueError('Skinned node is unsupported')
        matrix = multiply(parent, node_matrix(node))
        if 'mesh' in node:
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                if primitive.get('mode', 4) != 4 or primitive.get('targets') or primitive.get('extensions'):
                    raise ValueError('Only uncompressed static triangle primitives are supported')
                position = doc['accessors'][primitive['attributes']['POSITION']]
                if position['componentType'] != 5126 or position['type'] != 'VEC3':
                    raise ValueError('Positions must be float VEC3')
                points = [transform(matrix, point) for point in accessor(primitive['attributes']['POSITION'])]
                if not all(math.isfinite(value) for point in points for value in point):
                    raise ValueError('Nonfinite mesh position')
                if 'indices' in primitive:
                    index_accessor = doc['accessors'][primitive['indices']]
                    if index_accessor['componentType'] not in (5121, 5123, 5125) or index_accessor['type'] != 'SCALAR':
                        raise ValueError('Indices must be unsigned SCALAR')
                indices = [item[0] for item in accessor(primitive['indices'])] if 'indices' in primitive else list(range(len(points)))
                if len(indices) % 3:
                    raise ValueError('Incomplete triangle')
                for i in range(0, len(indices), 3):
                    triangles.append(tuple(points[j] for j in indices[i:i+3]))
        for child in node.get('children', []):
            visit(child, matrix, ancestors | {index})
    for index in doc['scenes'][scene].get('nodes', []):
        visit(index, identity, set())
    if not triangles:
        raise ValueError('GLB has no triangles in its default scene')
    minimum = [min(p[i] for triangle in triangles for p in triangle) for i in range(3)]
    maximum = [max(p[i] for triangle in triangles for p in triangle) for i in range(3)]
    if any(maximum[i] <= minimum[i] for i in range(3)):
        raise ValueError('GLB must have positive dimensions in all three axes')
    return triangles, minimum, maximum


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def triangle_intersects_box(triangle, center, size):
    """Separating-axis test; catches faces spanning the opening without interior vertices."""
    points = [tuple(p[i]-center[i] for i in range(3)) for p in triangle]
    edges = [tuple(points[(j+1) % 3][i]-points[j][i] for i in range(3)) for j in range(3)]
    basis = [(1, 0, 0), (0, 1, 0), (0, 0, 1)]
    axes = basis + [cross(edges[0], edges[1])] + [cross(edge, axis) for edge, axis in itertools.product(edges, basis)]
    for axis in axes:
        projected = [sum(p[i]*axis[i] for i in range(3)) for p in points]
        radius = sum(size[i]*0.5*abs(axis[i]) for i in range(3))
        if min(projected) > radius + 1e-9 or max(projected) < -radius - 1e-9:
            return False
    return True


def normalized_triangles(triangles, minimum, maximum, dimensions):
    scale = [dimensions[i]/(maximum[i]-minimum[i]) for i in range(3)]
    pivot = [(minimum[0]+maximum[0])*0.5, minimum[1], (minimum[2]+maximum[2])*0.5]
    return [tuple(tuple((p[i]-pivot[i])*scale[i] for i in range(3)) for p in triangle) for triangle in triangles]
