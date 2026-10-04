import copy
import json
import struct
import sys
import tempfile
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
import catalog
from glb_geometry import triangle_intersects_box


def fixture(path, blocked=False):
    # Thin frame: three box meshes; triangles crossing the opening exercise SAT.
    vertices, indices = [], []
    boxes = [(-1.5,0,-.2,-1,3,.2),(1,0,-.2,1.5,3,.2),(-1.5,2.8,-.2,1.5,3,.2)]
    if blocked: boxes.append((-1.5,1.3,-.2,1.5,1.5,.2))
    for x0,y0,z0,x1,y1,z1 in boxes:
        start = len(vertices)
        vertices.extend([(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)])
        for face in [(0,2,1),(0,3,2),(4,5,6),(4,6,7),(0,1,5),(0,5,4),(3,7,6),(3,6,2),(0,4,7),(0,7,3),(1,2,6),(1,6,5)]:
            indices.extend(start+i for i in face)
    pos=b''.join(struct.pack('<fff',*v) for v in vertices)
    idx=struct.pack('<'+'I'*len(indices),*indices)
    doc={'asset':{'version':'2.0'},'scene':0,'scenes':[{'nodes':[0]}], 'nodes':[{'mesh':0}],
         'buffers':[{'byteLength':len(pos)+len(idx)}], 'bufferViews':[{'buffer':0,'byteLength':len(pos)},{'buffer':0,'byteOffset':len(pos),'byteLength':len(idx)}],
         'accessors':[{'bufferView':0,'componentType':5126,'count':len(vertices),'type':'VEC3'}, {'bufferView':1,'componentType':5125,'count':len(indices),'type':'SCALAR'}],
         'meshes':[{'primitives':[{'attributes':{'POSITION':0},'indices':1}]}]}
    text=json.dumps(doc).encode();text+=b' '*(-len(text)%4)
    binary=pos+idx;binary+=b'\0'*(-len(binary)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,28+len(text)+len(binary))+struct.pack('<II',len(text),0x4e4f534a)+text+struct.pack('<II',len(binary),0x004e4942)+binary)


class CatalogTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.folder=self.root/'temple/architecture/doorways/001';self.folder.mkdir(parents=True)
        fixture(self.folder/'game.glb')
        catalog.dump(self.folder/'asset.json',catalog.doorway_metadata([3,3,.4],[1.9,2.7]))
    def tearDown(self): self.tmp.cleanup()
    def test_identical_filenames_and_deterministic_build(self):
        second=self.root/'subway/architecture/doorways/001';second.mkdir(parents=True)
        fixture(second/'game.glb');catalog.dump(second/'asset.json',catalog.doorway_metadata([3,3,.4],[1.9,2.7]))
        original=self.folder/'original.glb';original.write_bytes(b'not a GLB and deliberately excluded')
        first=catalog.build(self.root,'test-1');second_build=catalog.build(self.root,'test-1')
        self.assertEqual(first,second_build);self.assertEqual(2,len(first['assets']))
        self.assertNotEqual(first['assets'][0]['id'],first['assets'][1]['id']);catalog.validate(first,self.root)
    def test_missing_metadata_is_reported(self):
        (self.folder/'asset.json').unlink()
        with self.assertRaisesRegex(ValueError,'Missing asset.json'): catalog.build(self.root,'test-1')
    def test_hash_tampering_and_duplicate_identity_are_rejected(self):
        doc=catalog.build(self.root,'test-1');bad=copy.deepcopy(doc);bad['assets'].append(copy.deepcopy(bad['assets'][0]))
        with self.assertRaisesRegex(ValueError,'Duplicate'): catalog.validate(bad,self.root)
        with (self.folder/'game.glb').open('ab') as stream:stream.write(b'x')
        with self.assertRaisesRegex(ValueError,'Hash mismatch'):catalog.validate(doc,self.root)
    def test_blocked_mesh_and_collision_are_rejected(self):
        fixture(self.folder/'game.glb',blocked=True)
        with self.assertRaisesRegex(ValueError,'obstruct'):catalog.build(self.root,'test-1')
        settings=json.loads((self.folder/'asset.json').read_text());settings['collision']['boxes'][0]['centerMeters']['x']=0
        catalog.dump(self.folder/'asset.json',settings)
        with self.assertRaisesRegex(ValueError,'blocks'):catalog.build(self.root,'test-1')
    def test_escaping_path_is_rejected(self):
        with self.assertRaisesRegex(ValueError,'Unsafe'):catalog.relative_file(self.root,'../outside.glb')
        with self.assertRaisesRegex(ValueError,'Unsafe'):catalog.relative_file(self.root,'/absolute.glb')
    def test_face_crossing_opening_without_interior_vertices(self):
        self.assertTrue(triangle_intersects_box(((-3,0,0),(3,0,0),(0,4,0)),(0,1,0),(1,1,1)))
        self.assertFalse(triangle_intersects_box(((3,0,0),(4,0,0),(4,4,0)),(0,1,0),(1,1,1)))
    def test_invalid_socket_is_rejected(self):
        doc=catalog.build(self.root,'test-1');doc['assets'][0]['sockets'][0]['forward']['z']=-1
        with self.assertRaisesRegex(ValueError,'face outward'):catalog.validate(doc,self.root)

if __name__=='__main__':unittest.main()
