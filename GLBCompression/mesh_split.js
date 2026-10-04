import * as THREE from 'three';
// Copy all vertex attributes verbatim, including UV sets, normals, colors and skin data.
export function extractFaces(geometry, faces) {
  const result = new THREE.BufferGeometry(), oldToNew = new Map(), vertices = [], indices = [];
  const groups = geometry.groups;
  for (const face of faces) {
    const start = indices.length;
    for (let corner = 0; corner < 3; corner++) {
      const old = geometry.index ? geometry.index.getX(face * 3 + corner) : face * 3 + corner;
      if (!oldToNew.has(old)) { oldToNew.set(old, vertices.length); vertices.push(old); }
      indices.push(oldToNew.get(old));
    }
    const materialIndex = groups.find(g => face * 3 >= g.start && face * 3 < g.start + g.count)?.materialIndex || 0;
    const last = result.groups.at(-1);
    if (last?.materialIndex === materialIndex) last.count += 3; else result.addGroup(start, 3, materialIndex);
  }
  for (const [name, attr] of Object.entries(geometry.attributes)) {
    const data = new (attr.isInterleavedBufferAttribute ? attr.data.array : attr.array).constructor(vertices.length * attr.itemSize);
    for (let i = 0; i < vertices.length; i++) for (let c = 0; c < attr.itemSize; c++) {
      // Read raw storage to retain normalized integer skin weights and colors exactly.
      const offset = attr.isInterleavedBufferAttribute ? vertices[i] * attr.data.stride + attr.offset + c : vertices[i] * attr.itemSize + c;
      data[i * attr.itemSize + c] = (attr.isInterleavedBufferAttribute ? attr.data.array : attr.array)[offset];
    }
    result.setAttribute(name, new THREE.BufferAttribute(data, attr.itemSize, attr.normalized));
  }
  result.setIndex(indices); result.computeBoundingBox(); result.computeBoundingSphere(); return result;
}
export function splitMesh(mesh, labels, regions) {
  if (Object.keys(mesh.geometry.morphAttributes).length) throw new Error('Manual splitting does not support morph targets yet.');
  const buckets = new Map();
  const count = (mesh.geometry.index?.count || mesh.geometry.attributes.position.count) / 3;
  if (labels.length !== count) throw new Error('Selection no longer matches this mesh.');
  labels.forEach((label, face) => { if (!buckets.has(label)) buckets.set(label, []); buckets.get(label).push(face); });
  const group = new THREE.Group(); group.name = mesh.name;
  group.position.copy(mesh.position); group.quaternion.copy(mesh.quaternion); group.scale.copy(mesh.scale);
  group.userData = {...mesh.userData};
  for (const [label, faces] of buckets) {
    const part = mesh.clone(false); part.geometry = extractFaces(mesh.geometry, faces);
    part.position.set(0,0,0); part.quaternion.identity(); part.scale.set(1,1,1); part.updateMatrix();
    part.name = label ? regions[label - 1] : `${mesh.name || 'Mesh'}_remainder`;
    part.visible = true;
    // clone(false) retains skeleton/bind matrices and all material references.
    group.add(part);
  }
  for (const child of [...mesh.children]) group.add(child);
  const parent = mesh.parent; if (!parent) throw new Error('Mesh has no parent.');
  const index = parent.children.indexOf(mesh); parent.remove(mesh); parent.add(group);
  parent.children.splice(parent.children.indexOf(group), 1); parent.children.splice(index, 0, group);
  return group;
}
