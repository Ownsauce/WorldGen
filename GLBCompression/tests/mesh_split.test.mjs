import * as THREE from 'three';
import {GLTFExporter} from 'three/addons/exporters/GLTFExporter.js';
import {GLTFLoader} from 'three/addons/loaders/GLTFLoader.js';
import {FBXLoader} from 'three/addons/loaders/FBXLoader.js';
import {extractFaces,splitMesh} from '../mesh_split.js';
import fs from 'node:fs';import assert from 'node:assert/strict';
globalThis.FileReader=class {readAsArrayBuffer(blob){blob.arrayBuffer().then(value=>{this.result=value;this.onloadend?.()})}readAsDataURL(blob){blob.arrayBuffer().then(value=>{this.result=`data:${blob.type};base64,${Buffer.from(value).toString('base64')}`;this.onloadend?.()})}};
globalThis.ProgressEvent=class {constructor(type,props){Object.assign(this,props)}};
const geo=new THREE.BoxGeometry();const count=geo.index.count/3;
const weights=new Float32Array(geo.attributes.position.count*4),joints=new Uint16Array(weights.length);for(let i=0;i<weights.length;i+=4)weights[i]=1;
geo.setAttribute('skinWeight',new THREE.BufferAttribute(weights,4));geo.setAttribute('skinIndex',new THREE.BufferAttribute(joints,4));
const scene=new THREE.Scene(),bone=new THREE.Bone();bone.name='Root';scene.add(bone);
const mesh=new THREE.SkinnedMesh(geo,Array.from({length:6},()=>new THREE.MeshStandardMaterial()));mesh.name='Body';mesh.position.set(2,3,4);scene.add(mesh);scene.updateMatrixWorld(true);mesh.bind(new THREE.Skeleton([bone]));
const labels=new Uint32Array(count);labels.fill(1,0,4);labels.fill(2,4,8);
const group=splitMesh(mesh,labels,['torso','legs']);assert.equal(group.children.length,3);
assert.deepEqual(group.children.map(p=>p.name),['torso','legs','Body_remainder']);
assert.equal(group.children.reduce((sum,p)=>sum+p.geometry.index.count,0),geo.index.count);
for(const part of group.children){assert.equal(part.skeleton,mesh.skeleton);assert(part.bindMatrix.equals(mesh.bindMatrix));assert(part.geometry.attributes.uv);assert(part.geometry.attributes.skinWeight);assert(part.material.every((m,i)=>m===mesh.material[i]));assert.equal(part.position.length(),0)}
assert.deepEqual(group.position.toArray(),[2,3,4]);
const bytes=await new GLTFExporter().parseAsync(scene,{binary:true,onlyVisible:false});
const parsed=await new GLTFLoader().parseAsync(bytes,'');const parts=[];parsed.scene.traverse(o=>{if(o.isMesh)parts.push(o)});
assert(parts.length>=3);
const names=[];parsed.scene.traverse(o=>names.push(o.name));assert(names.includes('torso') && names.includes('legs') && names.includes('Body_remainder'));assert(parts.every(p=>p.isSkinnedMesh && p.skeleton.bones.length===1 && p.geometry.attributes.uv && p.geometry.attributes.skinWeight));
assert.equal(parts.reduce((sum,p)=>sum+p.geometry.index.count,0),geo.index.count);
// Bone motion still affects each split mesh after round trip.
parsed.scene.updateMatrixWorld(true);const p=parts[0],before=p.getVertexPosition(0,new THREE.Vector3());p.skeleton.bones[0].position.x+=1;parsed.scene.updateMatrixWorld(true);p.skeleton.update();const after=p.getVertexPosition(0,new THREE.Vector3());assert(after.distanceTo(before)>.9);
const raw=new THREE.BufferGeometry();const data=new Uint8Array([1,2,3,4,5,6,7,8,9,10,11,12]);const inter=new THREE.InterleavedBuffer(data,4);raw.setAttribute('position',new THREE.Float32BufferAttribute([0,0,0,1,0,0,0,1,0],3));raw.setAttribute('color',new THREE.InterleavedBufferAttribute(inter,3,0,true));assert.deepEqual([...extractFaces(raw,[0]).attributes.color.array],[1,2,3,5,6,7,9,10,11]);
const obj=new THREE.Group(),actual=new THREE.Mesh(new THREE.BoxGeometry(),new THREE.MeshStandardMaterial());obj.add(actual);const n=actual.geometry.index.count/3;const assigned=new Uint32Array(n);assigned.fill(1,0,Math.floor(n/2));const actualParts=splitMesh(actual,assigned,['test_region']);assert.equal(actualParts.children.length,2);assert.equal(actualParts.children.reduce((sum,p)=>sum+p.geometry.index.count/3,0),n);
console.log('PASS static mesh split, triangle conservation, material groups, raw interleaved attributes, UVs, skeleton/weights, GLB round trip and bone deformation');
