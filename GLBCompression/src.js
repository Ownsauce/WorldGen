import * as THREE from 'three';
import { extractFaces, splitMesh } from './mesh_split.js';
import { polygonFaces } from './polygon_selection.js';
import { clone as cloneSkeleton } from 'three/addons/utils/SkeletonUtils.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { FBXLoader } from 'three/addons/loaders/FBXLoader.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { GLTFExporter } from 'three/addons/exporters/GLTFExporter.js';
import { SimplifyModifier } from 'three/addons/modifiers/SimplifyModifier.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

const $ = id => document.getElementById(id);
const status = message => { $('status').textContent = message; };
const views = [createView('original'), createView('converted')];
let source = null, packaged = null, metadataText = '', convertedBytes = null, outputName = 'model', sourceFile = null, sourceFiles = [], exportVariant = '';
const optimizedCache = new Map();
const urls = [];
const simplifier = new SimplifyModifier();

function createView(id) {
  const host = $(id);
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x1b2530);
  const camera = new THREE.PerspectiveCamera(45, 1, 0.01, 10000);
  camera.position.set(3, 2, 4);
  const renderer = new THREE.WebGLRenderer({ antialias: true, preserveDrawingBuffer: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  host.appendChild(renderer.domElement);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  scene.add(new THREE.HemisphereLight(0xffffff, 0x667080, 2));
  const light = new THREE.DirectionalLight(0xffffff, 2);
  light.position.set(4, 7, 6);
  scene.add(light);
  const view = { host, scene, camera, renderer, controls, model: null };
  controls.addEventListener('change', () => {
    if (!$('linked').checked) return;
    const other = views.find(item => item !== view);
    if (!other) return;
    other.camera.position.copy(camera.position);
    other.camera.quaternion.copy(camera.quaternion);
    other.camera.zoom = camera.zoom;
    other.camera.updateProjectionMatrix();
    other.controls.target.copy(controls.target);
  });
  return view;
}

function showModel(view, model) {
  // Inspection edits a skeleton-aware preview copy, never the source/export scene.
  model = cloneSkeleton(model);
  for (const helper of view.boneHelpers || []) { view.scene.remove(helper); helper.dispose(); }
  view.boneHelpers = [];
  if (view.model) view.scene.remove(view.model);
  view.model = model;
  view.scene.add(model);
  model.updateMatrixWorld(true);
  const roots = new Set();
  model.traverse(object => { if (object.isSkinnedMesh && object.skeleton?.bones.length) {
    let bone = object.skeleton.bones[0];
    while (bone.parent?.isBone) bone = bone.parent;
    roots.add(bone);
  }});
  for (const root of roots) {
    const helper = new THREE.SkeletonHelper(root);
    helper.material.depthTest = false; helper.material.depthWrite = false;
    helper.renderOrder = 1000; helper.visible = $('show-bones').checked;
    view.scene.add(helper); view.boneHelpers.push(helper);
  }
  frame(view);
  refreshInspection();
  if (view === views[1]) { refreshUV(); resetSplit(); }
}
function frame(view) {
  if (!view.model) return;
  const box = new THREE.Box3().setFromObject(view.model);
  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3());
  const radius = Math.max(size.length() * .5, .1);
  view.controls.target.copy(center);
  view.camera.position.copy(center).add(new THREE.Vector3(radius * 1.8, radius * 1.1, radius * 2.1));
  view.camera.near = Math.max(radius / 1000, .001);
  view.camera.far = Math.max(radius * 100, 100);
  view.camera.updateProjectionMatrix();
  view.controls.update();
}
function statistics(scene) {
  let meshes = 0, triangles = 0, vertices = 0, materials = new Set(), textures = new Set();
  scene.traverse(object => {
    if (!object.isMesh && !object.isSkinnedMesh) return;
    meshes++;
    const geometry = object.geometry;
    vertices += geometry.getAttribute('position')?.count || 0;
    triangles += geometry.index ? geometry.index.count / 3 : (geometry.getAttribute('position')?.count || 0) / 3;
    for (const material of Array.isArray(object.material) ? object.material : [object.material]) {
      if (!material) continue;
      materials.add(material.uuid);
      for (const value of Object.values(material)) if (value?.isTexture) textures.add(value.uuid);
    }
  });
  return { meshes, vertices, triangles: Math.round(triangles), materials: materials.size, textures: textures.size };
}
function formatSize(bytes) { return `${(bytes / 1048576).toFixed(2)} MiB`; }
function info(stats) { return `${stats.meshes} meshes · ${stats.vertices} vertices · ${stats.triangles} triangles · ${stats.materials} materials · ${stats.textures} textures`; }
function basename(name) { return name.split(/[\\/]/).pop(); }
function gltfLoader(fileMap, fbx = false) {
  const manager = new THREE.LoadingManager();
  manager.setURLModifier(url => {
    if (/^(data:|blob:)/i.test(url)) return url;
    const clean = decodeURIComponent(url.split('?')[0]);
    const file = fileMap.get(clean) || fileMap.get(basename(clean));
    if (!file) throw new Error(`Missing linked model file: ${url}. Select it alongside the model.`);
    const objectUrl = URL.createObjectURL(file);
    urls.push(objectUrl);
    return objectUrl;
  });
  return fbx ? new FBXLoader(manager) : new GLTFLoader(manager);
}
function parseGltf(bytes, fileMap) {
  return new Promise((resolve, reject) => gltfLoader(fileMap).parse(bytes, '', resolve, reject));
}
async function sha256(bytes) {
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return [...new Uint8Array(digest)].map(byte => byte.toString(16).padStart(2, '0')).join('');
}
function base64(bytes) {
  const data = new Uint8Array(bytes);
  let output = '';
  for (let i = 0; i < data.length; i += 0x8000) output += String.fromCharCode(...data.subarray(i, i + 0x8000));
  return btoa(output);
}
async function simplifyGeometry(geometry, ratio) {
  const simplify = async input => {
    const vertices = input.getAttribute('position')?.count || 0;
    if (vertices < 6) return input.clone();
    const result = await simplifier.modify(input, Math.round(vertices * (1 - ratio)));
    return result.index?.count >= 3 ? result : input.clone();
  };
  if (geometry.groups.length <= 1) return simplify(geometry);
  const groups = [];
  for (const group of geometry.groups) {
    const part = geometry.clone();
    const indices = geometry.index
      ? geometry.index.array.slice(group.start, group.start + group.count)
      : Uint32Array.from({ length: group.count }, (_, i) => group.start + i);
    part.setIndex(Array.from(indices));
    part.clearGroups();
    groups.push(await simplify(part));
  }
  const merged = mergeGeometries(groups, true);
  if (!merged) throw new Error('Could not retain the geometry material groups during simplification.');
  return merged;
}
async function makeLodScene(original, ratio) {
  const clone = original.clone(true);
  const originalMeshes = [], clonedMeshes = [];
  original.traverse(object => { if (object.isMesh) originalMeshes.push(object); });
  clone.traverse(object => { if (object.isMesh) clonedMeshes.push(object); });
  for (let i = 0; i < clonedMeshes.length; i++) {
    if (originalMeshes[i].isSkinnedMesh || originalMeshes[i].morphTargetInfluences?.length)
      throw new Error('LOD simplification currently supports static meshes only.');
    clonedMeshes[i].geometry = await simplifyGeometry(originalMeshes[i].geometry, ratio);
  }
  return clone;
}
async function packGltf(gltfFile, files) {
  const document = JSON.parse(await gltfFile.text());
  const fileMap = new Map();
  for (const file of files) { fileMap.set(file.name, file); fileMap.set(basename(file.name), file); }
  const mime = filename => ({ png: 'image/png', jpg: 'image/jpeg', jpeg: 'image/jpeg', webp: 'image/webp', ktx2: 'image/ktx2' }[filename.split('.').pop().toLowerCase()] || 'application/octet-stream');
  async function embed(value) {
    if (!value || typeof value !== 'object') return;
    for (const [key, child] of Object.entries(value)) {
      if (key === 'uri' && typeof child === 'string' && !/^(data:|https?:)/i.test(child)) {
        const clean = decodeURIComponent(child.split('?')[0]);
        const file = fileMap.get(clean) || fileMap.get(basename(clean));
        if (!file) throw new Error(`Missing linked model file: ${child}`);
        value[key] = `data:${mime(file.name)};base64,${base64(await file.arrayBuffer())}`;
      } else if (child && typeof child === 'object') await embed(child);
    }
  }
  await embed(document);
  const json = new TextEncoder().encode(JSON.stringify(document));
  const paddedLength = (json.length + 3) & ~3;
  const output = new ArrayBuffer(12 + 8 + paddedLength);
  const header = new DataView(output);
  header.setUint32(0, 0x46546c67, true);
  header.setUint32(4, 2, true);
  header.setUint32(8, output.byteLength, true);
  header.setUint32(12, paddedLength, true);
  header.setUint32(16, 0x4e4f534a, true);
  const payload = new Uint8Array(output, 20);
  payload.fill(0x20);
  payload.set(json);
  return output;
}


function parseGlb(buffer) {
  const view = new DataView(buffer);
  if (view.getUint32(0, true) !== 0x46546c67 || view.getUint32(4, true) !== 2)
    throw new Error('Expected a glTF 2.0 GLB.');
  const length = view.getUint32(8, true);
  if (length !== buffer.byteLength) throw new Error('Invalid GLB length.');
  let offset = 12, json = null, bin = null;
  while (offset + 8 <= length) {
    const size = view.getUint32(offset, true), type = view.getUint32(offset + 4, true);
    offset += 8;
    if (offset + size > length) throw new Error('Invalid GLB chunk.');
    if (type === 0x4e4f534a) json = JSON.parse(new TextDecoder().decode(new Uint8Array(buffer, offset, size)));
    if (type === 0x004e4942) bin = new Uint8Array(buffer, offset, size);
    offset += size;
  }
  if (!json) throw new Error('GLB has no JSON chunk.');
  return { json, bin };
}
function readImage(part, image) {
  if (image.bufferView !== undefined) {
    const view = part.json.bufferViews[image.bufferView];
    if (!part.bin || view.buffer !== 0) return null;
    return { bytes: part.bin.slice(view.byteOffset || 0, (view.byteOffset || 0) + view.byteLength), mime: image.mimeType };
  }
  if (image.uri?.startsWith('data:')) {
    const comma = image.uri.indexOf(',');
    const header = image.uri.slice(5, comma);
    if (!header.includes(';base64')) return null;
    const binary = atob(image.uri.slice(comma + 1));
    const bytes = Uint8Array.from(binary, char => char.charCodeAt(0));
    return { bytes, mime: header.split(';')[0] };
  }
  return null;
}
async function imageFingerprint(image) {
  if (!image || !['image/png','image/jpeg','image/webp'].includes(image.mime)) return null;
  const bitmap = await createImageBitmap(new Blob([image.bytes], { type: image.mime }));
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = 32;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  context.drawImage(bitmap, 0, 0, 32, 32);
  const result = { width: bitmap.width, height: bitmap.height, pixels: context.getImageData(0, 0, 32, 32).data };
  bitmap.close();
  return result;
}
function imageDistance(a, b) {
  if (a.width !== b.width || a.height !== b.height) return Infinity;
  let error = 0;
  for (let i = 0; i < a.pixels.length; i += 4)
    error += Math.abs(a.pixels[i]-b.pixels[i]) + Math.abs(a.pixels[i+1]-b.pixels[i+1]) + Math.abs(a.pixels[i+2]-b.pixels[i+2]) + Math.abs(a.pixels[i+3]-b.pixels[i+3]);
  return error / a.pixels.length;
}
function makeGlb(json, bin = null) {
  const jsonBytes = new TextEncoder().encode(JSON.stringify(json));
  const jsonSize = (jsonBytes.length + 3) & ~3;
  const binSize = bin ? (bin.length + 3) & ~3 : 0;
  const buffer = new ArrayBuffer(12 + 8 + jsonSize + (bin ? 8 + binSize : 0));
  const view = new DataView(buffer), bytes = new Uint8Array(buffer);
  view.setUint32(0, 0x46546c67, true); view.setUint32(4, 2, true); view.setUint32(8, buffer.byteLength, true);
  view.setUint32(12, jsonSize, true); view.setUint32(16, 0x4e4f534a, true);
  bytes.fill(0x20, 20, 20 + jsonSize); bytes.set(jsonBytes, 20);
  if (bin) {
    const binHeader = 20 + jsonSize;
    view.setUint32(binHeader, binSize, true); view.setUint32(binHeader + 4, 0x004e4942, true);
    bytes.set(bin, binHeader + 8);
  }
  return buffer;
}

async function resizeTexture(image, scale, isNormal) {
  if (!image || !['image/png', 'image/jpeg', 'image/webp'].includes(image.mime)) return null;
  const bitmap = await createImageBitmap(new Blob([image.bytes], { type: image.mime }));
  const width = Math.max(1, Math.round(bitmap.width * scale));
  const height = Math.max(1, Math.round(bitmap.height * scale));
  if (width === bitmap.width && height === bitmap.height) { bitmap.close(); return null; }
  const canvas = document.createElement('canvas');
  canvas.width = width; canvas.height = height;
  const context = canvas.getContext('2d', { willReadFrequently: isNormal });
  context.imageSmoothingEnabled = true;
  context.imageSmoothingQuality = 'high';
  context.drawImage(bitmap, 0, 0, width, height);
  bitmap.close();
  if (isNormal) {
    const pixels = context.getImageData(0, 0, width, height);
    const data = pixels.data;
    for (let i = 0; i < data.length; i += 4) {
      let x = data[i] / 127.5 - 1, y = data[i+1] / 127.5 - 1, z = data[i+2] / 127.5 - 1;
      const length = Math.hypot(x, y, z) || 1;
      data[i] = Math.round((x / length + 1) * 127.5);
      data[i+1] = Math.round((y / length + 1) * 127.5);
      data[i+2] = Math.round((z / length + 1) * 127.5);
    }
    context.putImageData(pixels, 0, 0);
  }
  const mime = image.mime;
  const blob = await new Promise(resolve => canvas.toBlob(resolve, mime, mime === 'image/jpeg' ? 0.86 : undefined));
  if (!blob) throw new Error('The browser could not encode a resized texture.');
  const bytes = new Uint8Array(await blob.arrayBuffer());
  return bytes.length < image.bytes.length ? { bytes, mime, width, height } : null;
}
function replaceImages(part, replacement) {
  if (!replacement.size) return null;
  const json = part.json;
  if (!part.bin) {
    for (const [index, image] of replacement) {
      json.images[index].uri = `data:${image.mime};base64,${base64(image.bytes)}`;
      json.images[index].mimeType = image.mime;
    }
    return makeGlb(json);
  }
  const byView = new Map();
  for (const [index, image] of replacement) {
    const viewIndex = json.images[index].bufferView;
    if (viewIndex === undefined) throw new Error('A source image has no buffer view.');
    byView.set(viewIndex, image);
    json.images[index].mimeType = image.mime;
  }
  const parts = []; let cursor = 0;
  for (let i = 0; i < json.bufferViews.length; i++) {
    const view = json.bufferViews[i];
    if (view.buffer !== 0) throw new Error('Model has an unsupported additional buffer.');
    const section = byView.get(i)?.bytes || part.bin.slice(view.byteOffset || 0, (view.byteOffset || 0) + view.byteLength);
    view.byteOffset = cursor; view.byteLength = section.length;
    parts.push({ offset: cursor, section });
    cursor += (section.length + 3) & ~3;
  }
  const bin = new Uint8Array(cursor);
  for (const { offset, section } of parts) bin.set(section, offset);
  json.buffers[0].byteLength = cursor;
  return makeGlb(json, bin);
}
async function resizeModelTextures(modelBytes, scale) {
  const part = parseGlb(modelBytes), images = part.json.images || [];
  const normalSources = new Set();
  for (const material of part.json.materials || []) {
    const textureIndex = material.normalTexture?.index;
    const sourceIndex = (part.json.textures || [])[textureIndex]?.source;
    if (sourceIndex !== undefined) normalSources.add(sourceIndex);
  }
  const replacement = new Map();
  let originalImageBytes = 0, resultImageBytes = 0;
  for (let i = 0; i < images.length; i++) {
    const image = readImage(part, images[i]);
    if (!image) continue;
    originalImageBytes += image.bytes.length;
    const resized = await resizeTexture(image, scale, normalSources.has(i));
    if (resized) replacement.set(i, resized);
    resultImageBytes += resized?.bytes.length || image.bytes.length;
  }
  const bytes = replaceImages(part, replacement) || modelBytes;
  return { bytes, note: `${replacement.size}/${images.length} texture images resized; image bytes ${formatSize(originalImageBytes)} → ${formatSize(resultImageBytes)}.` };
}

async function preserveSourceImages(sourceBytes, outputBytes) {
  const original = parseGlb(sourceBytes), converted = parseGlb(outputBytes);
  const sourceImages = (original.json.images || []).map(image => readImage(original, image));
  const outputImages = (converted.json.images || []).map(image => readImage(converted, image));
  if (!sourceImages.length || sourceImages.length !== outputImages.length || !converted.bin)
    return { bytes: outputBytes, note: 'Source images could not be matched; exported images were retained.' };
  const sourcePrints = await Promise.all(sourceImages.map(imageFingerprint));
  const outputPrints = await Promise.all(outputImages.map(imageFingerprint));
  if (sourcePrints.some(value => !value) || outputPrints.some(value => !value))
    return { bytes: outputBytes, note: 'Some image formats could not be compared; exported images were retained.' };
  const used = new Set(), replacement = new Map();
  for (let i = 0; i < outputImages.length; i++) {
    let best = -1, distance = Infinity;
    for (let j = 0; j < sourceImages.length; j++) {
      if (used.has(j)) continue;
      const score = imageDistance(outputPrints[i], sourcePrints[j]);
      if (score < distance) { best = j; distance = score; }
    }
    // Allow modest JPEG re-encoding differences, but reject uncertain matches.
    if (best < 0 || distance > 4)
      return { bytes: outputBytes, note: 'Image identity was uncertain; exported images were retained.' };
    used.add(best);
    replacement.set(converted.json.images[i].bufferView, sourceImages[best]);
  }
  const parts = []; let cursor = 0;
  for (let i = 0; i < converted.json.bufferViews.length; i++) {
    const view = converted.json.bufferViews[i];
    if (view.buffer !== 0) throw new Error('Exported GLB uses an unexpected buffer.');
    const chosen = replacement.get(i);
    const section = chosen?.bytes || converted.bin.slice(view.byteOffset || 0, (view.byteOffset || 0) + view.byteLength);
    view.byteOffset = cursor;
    view.byteLength = section.length;
    parts.push({ offset: cursor, section });
    cursor += (section.length + 3) & ~3;
  }
  const bin = new Uint8Array(cursor);
  for (const { offset, section } of parts) bin.set(section, offset);
  for (const image of converted.json.images || []) {
    const chosen = replacement.get(image.bufferView);
    if (chosen) image.mimeType = chosen.mime;
  }
  converted.json.buffers[0].byteLength = cursor;
  return { bytes: makeGlb(converted.json, bin), note: 'Original compressed image bytes preserved.' };
}

function download(name, contents, type) {
  const url = URL.createObjectURL(new Blob([contents], { type }));
  const link = document.createElement('a');
  link.href = url; link.download = name; link.click();
  setTimeout(() => URL.revokeObjectURL(url), 30000);
}
$('files').addEventListener('change', async event => {
  $('convert').disabled = true;
  $('download-glb').disabled = true;
  $('download-meta').disabled = true;
  for (const view of views) {
    if (view.model) view.scene.remove(view.model);
    for (const helper of view.boneHelpers || []) { view.scene.remove(helper); helper.dispose(); }
    view.model = null; view.boneHelpers = [];
  }
  source = null; packaged = null; convertedBytes = null; optimizedCache.clear();
  refreshInspection(); refreshUV(); resetSplit();
  $('download-fbx').disabled = true;
  $('unwrap-uv').disabled = true; $('base-color-file').disabled = true;
  $('original-size').textContent = '';
  $('converted-size').textContent = '';
  for (const url of urls.splice(0)) URL.revokeObjectURL(url);
  const files = [...event.target.files];
  sourceFiles = files;
  const model = files.find(file => /\.(glb|gltf|fbx)$/i.test(file.name));
  if (!model) { status('Select a .fbx, .glb or .gltf model.'); return; }
  const fileMap = new Map();
  for (const file of files) { fileMap.set(file.name, file); fileMap.set(basename(file.name), file); }
  try {
    status(`Loading ${model.name}…`);
    const bytes = /\.gltf$/i.test(model.name) ? await model.text() : await model.arrayBuffer();
    if (/\.fbx$/i.test(model.name)) {
      const loader = gltfLoader(fileMap, true);
      let loadingTextures = false, resolveTextures, rejectTextures;
      const texturesReady = new Promise((resolve, reject) => { resolveTextures = resolve; rejectTextures = reject; });
      loader.manager.onStart = () => { loadingTextures = true; };
      loader.manager.onLoad = () => resolveTextures();
      loader.manager.onError = url => rejectTextures(new Error(`Could not load FBX texture: ${url}`));
      const scene = loader.parse(bytes, '');
      if (loadingTextures) await texturesReady;
      source = { scene, animations: scene.animations || [] };
    } else source = await parseGltf(bytes, fileMap);
    sourceFile = model;
    outputName = model.name.replace(/\.(glb|gltf|fbx)$/i, '').replace(/[^a-zA-Z0-9_-]/g, '-');
    showModel(views[0], source.scene);
    $('original-info').textContent = info(statistics(source.scene));
    const totalBytes = files.reduce((sum, file) => sum + file.size, 0);
    $('original-size').textContent = /\.glb$/i.test(model.name)
      ? `· GLB ${formatSize(model.size)}`
      : `· ${/\.fbx$/i.test(model.name) ? 'FBX' : 'glTF'} + linked files ${formatSize(totalBytes)}`;
    $('converted-info').textContent = '';
    $('convert').disabled = false;
    $('reset').disabled = false;
    status('Source loaded. Click Convert. Drag either model to orbit.');
  } catch (error) { status(`Load failed: ${error.message}`); }
});
$('convert').addEventListener('click', async () => {
  if (!source) return;
  convertedBytes = null;
  $('convert').disabled = true;
  $('download-glb').disabled = true; $('download-meta').disabled = true; $('download-fbx').disabled = true;
  for (const id of ['detail-range','detail-number','texture-range','texture-number','reduction-method','unwrap-uv','base-color-file','uv-resolution']) $(id).disabled = true;
  try {
    status('Packaging model data into a self-contained GLB…');
    const detailPercent = Number($('detail-number').value);
    const texturePercent = Number($('texture-number').value);
    const ratio = detailPercent / 100;
    const textureScale = texturePercent / 100;
    let bytes, imageNote = '';
    let prepared = optimizedCache.get(textureScale);
    if (!prepared) {
      const originalBytes = /\.glb$/i.test(sourceFile.name)
        ? await sourceFile.arrayBuffer() : /\.fbx$/i.test(sourceFile.name)
          ? await exportSource()
          : await packGltf(sourceFile, sourceFiles);
      if (textureScale === 1) prepared = { bytes: originalBytes, scene: source.scene, note: '' };
      else {
        const optimized = await resizeModelTextures(originalBytes, textureScale);
        prepared = { bytes: optimized.bytes, scene: (await parseGltf(optimized.bytes, new Map())).scene, note: optimized.note };
      }
      optimizedCache.set(textureScale, prepared);
    }
    imageNote = prepared.note;
    if (ratio === 1) bytes = prepared.bytes;
    else if ($('reduction-method').value === 'blender') {
      status('Reducing mesh with Blender…');
      const response = await fetch('/reduce-glb', {
        method: 'POST', headers: { 'Content-Type': 'model/gltf-binary', 'X-Mesh-Ratio': String(ratio) }, body: prepared.bytes
      });
      if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.error || `Mesh reduction failed (${response.status})`);
      }
      bytes = await response.arrayBuffer();
      const preserved = await preserveSourceImages(prepared.bytes, bytes);
      bytes = preserved.bytes;
      imageNote += ' ' + preserved.note;
      imageNote += ' Blender collapse decimation; inspect UVs, fine detail, and joint deformation on rigged models.';
    } else {
      const lodScene = await makeLodScene(prepared.scene, ratio);
      bytes = await new GLTFExporter().parseAsync(lodScene, {
        binary: true, onlyVisible: false, includeCustomExtensions: true
      });
      const preserved = await preserveSourceImages(prepared.bytes, bytes);
      bytes = preserved.bytes;
      imageNote += ' ' + preserved.note;
    }
    const hash = await sha256(bytes);
    // Reload the exact bytes that will be downloaded as a GLB.
    packaged = await parseGltf(bytes, new Map());
    showModel(views[1], packaged.scene);
    const before = statistics(source.scene), after = statistics(packaged.scene);
    $('converted-info').textContent = info(after);
    const manifest = { schemaVersion: 1, format: 'glb-metadata', sourceName: outputName,
      detailLevel: `${detailPercent}% mesh`, targetTriangleRatio: ratio,
      textureScale, reductionMethod: $('reduction-method').value,
      sha256: hash, byteLength: bytes.byteLength, sourceStats: before, convertedStats: after };
    convertedBytes = bytes;
    exportVariant = `${ratio}-tex${textureScale}`;
    metadataText = JSON.stringify(manifest, null, 2);
    $('converted-size').textContent = `· GLB ${formatSize(bytes.byteLength)}`;
    $('json-preview').textContent = metadataText;
    $('download-glb').disabled = false;
    $('download-meta').disabled = false;
    $('download-fbx').disabled = false;
    $('unwrap-uv').disabled = false; $('base-color-file').disabled = false;
    $('face-count').textContent = `Actual: ${(100 * after.triangles / Math.max(1, before.triangles)).toFixed(1)}% of original triangles · ${after.triangles.toLocaleString()} export triangles · Studio reference: 30,000 faces · ${after.triangles <= 30000 ? 'at or below' : 'above'} 30,000 triangles. Percentage remains your choice.`;
    const sizeChange = ((bytes.byteLength / sourceFiles.reduce((sum, file) => sum + file.size, 0) - 1) * 100).toFixed(1);
    $('summary').textContent = `${detailPercent}% mesh, ${texturePercent}% textures · GLB ${formatSize(bytes.byteLength)} (${sizeChange}% vs source files). ${imageNote} SHA-256 ${hash}. The metadata JSON is optional.`;
    status('Conversion complete. Compare both models, then download GLB or FBX. Metadata JSON is optional.');
  } catch (error) { status(`Conversion failed: ${error.message}`); }
  finally { $('convert').disabled = false; for (const id of ['detail-range','detail-number','texture-range','texture-number','reduction-method','unwrap-uv','base-color-file','uv-resolution']) $(id).disabled = (id === 'unwrap-uv' || id === 'base-color-file') && !convertedBytes; }
});
async function publishEditedOutput(bytes, note, changes) {
  const result = await parseGltf(bytes, new Map());
  const after = statistics(result.scene);
  const manifest = { ...JSON.parse(metadataText), ...changes, sha256: await sha256(bytes), byteLength: bytes.byteLength, convertedStats: after };
  packaged = result;
  convertedBytes = bytes;
  metadataText = JSON.stringify(manifest, null, 2);
  showModel(views[1], result.scene);
  $('converted-info').textContent = info(after);
  $('converted-size').textContent = `· GLB ${formatSize(bytes.byteLength)}`;
  $('json-preview').textContent = metadataText;
  $('summary').textContent = `${note} · GLB ${formatSize(bytes.byteLength)} · ${after.textures} textures. Preview shows the exported file.`;
}
function editBusy(busy) {
  for (const control of $('manual-split').querySelectorAll('input,button,select')) control.disabled = busy;
  if (busy) { cancelPolygon(); painting = false; $('split-brush').checked = false; views[1].controls.enabled = true; }
  else refreshRegions();
  for (const id of ['files','convert','detail-range','detail-number','texture-range','texture-number','reduction-method','uv-resolution']) $(id).disabled = busy;
  for (const id of ['unwrap-uv','base-color-file','download-glb','download-fbx','download-meta']) $(id).disabled = busy || !convertedBytes;
}
$('unwrap-uv').addEventListener('click', async () => {
  if (!convertedBytes) return;
  editBusy(true);
  try {
    const resolution = Number($('uv-resolution').value);
    status('Unwrapping UVs and rebaking material textures with Blender. This can take several minutes…');
    const response = await fetch('/unwrap-glb', { method: 'POST', headers: { 'Content-Type': 'model/gltf-binary', 'X-Bake-Size': String(resolution) }, body: convertedBytes });
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.error || (response.status === 404 ? 'The running server is outdated. Stop it with Ctrl+C, run python3 server.py again in GLBCompression, then refresh this page.' : `UV processing failed (${response.status})`));
    }
    await publishEditedOutput(await response.arrayBuffer(), 'New UV layout with rebaked base color, roughness, metallic, normal, emission and alpha', { uvUnwrapped: true, uvBakeResolution: resolution });
    exportVariant += `-uv${resolution}`;
    setPreviewTab(true);
    status('UV unwrapping complete. Inspect the layout and rebaked textures before exporting.');
  } catch (error) { status(error.message); }
  finally { editBusy(false); }
});
$('base-color-file').addEventListener('change', async event => {
  const file = event.target.files[0];
  if (!file || !packaged) return;
  editBusy(true);
  try {
    const url = URL.createObjectURL(file);
    let texture;
    try { texture = await new THREE.TextureLoader().loadAsync(url); }
    finally { URL.revokeObjectURL(url); }
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.flipY = false;
    // Reload current bytes to avoid altering the preview on a failed operation.
    const scene = (await parseGltf(convertedBytes, new Map())).scene;
    scene.traverse(object => {
      if (!object.isMesh) return;
      if (!object.geometry.getAttribute('uv')) throw new Error('Unwrap UVs before applying an image.');
      const update = material => {
        const copy = material.clone(); copy.map = texture;
        if (copy.color) copy.color.set(0xffffff);
        copy.needsUpdate = true; return copy;
      };
      object.material = Array.isArray(object.material) ? object.material.map(update) : update(object.material);
    });
    const bytes = await new GLTFExporter().parseAsync(scene, { binary: true, onlyVisible: false, animations: packaged.animations });
    await publishEditedOutput(bytes, 'Applied base-color image using the current UV layout', { baseColorImage: file.name });
    exportVariant += '-color';
    status('Base-color image applied and embedded. Inspect its placement before exporting.');
  } catch (error) { status(error.message); }
  finally { editBusy(false); }
});
$('reduction-method').addEventListener('change', selectionChanged);
$('download-glb').addEventListener('click', () => download(`${outputName}-${exportVariant}.glb`, convertedBytes, 'model/gltf-binary'));
$('download-fbx').addEventListener('click', async () => {
  if (!convertedBytes) return;
  const bytes = convertedBytes, name = `${outputName}-${exportVariant}.fbx`;
  $('download-fbx').disabled = true;
  status('Exporting the selected mesh to FBX with Blender…');
  try {
    const response = await fetch('/export-fbx', { method: 'POST', headers: { 'Content-Type': 'model/gltf-binary' }, body: bytes });
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.error || `FBX export failed (${response.status})`);
    }
    download(name, await response.arrayBuffer(), 'application/octet-stream');
    status('FBX exported. The selected percentage was preserved.');
  } catch (error) { status(error.message); }
  finally { $('download-fbx').disabled = !convertedBytes || $('download-glb').disabled; }
});
$('download-meta').addEventListener('click', () => download(`${outputName}-${exportVariant}.meta.json`, metadataText, 'application/json'));
function selectionChanged() {
  $('download-glb').disabled = true; $('download-meta').disabled = true; $('download-fbx').disabled = true;
  if (source && !$('convert').disabled) $('convert').click();
}
for (const prefix of ['detail', 'texture']) {
  const slider = $(`${prefix}-range`), number = $(`${prefix}-number`);
  slider.addEventListener('input', () => { number.value = slider.value; });
  slider.addEventListener('change', selectionChanged);
  number.addEventListener('input', () => {
    const value = Number(number.value);
    if (Number.isFinite(value) && value >= 1 && value <= 100) slider.value = String(Math.round(value));
  });
  number.addEventListener('change', () => {
    const value = Number(number.value);
    number.value = String(Math.min(100, Math.max(1, Number.isFinite(value) ? Math.round(value) : 100)));
    slider.value = number.value;
    selectionChanged();
  });
  number.addEventListener('keydown', event => { if (event.key === 'Enter') number.blur(); });
}
$('reset').addEventListener('click', () => { for (const view of views) frame(view); });
// Inspection never edits downloaded GLB bytes. Restore source visibility during FBX packaging.
async function exportSource() {
  const visibility = [];
  source.scene.traverse(object => { visibility.push([object, object.visible]); object.visible = true; });
  try { return await new GLTFExporter().parseAsync(source.scene, { binary: true, onlyVisible: false, animations: source.animations }); }
  finally { for (const [object, visible] of visibility) object.visible = visible; }
}
function meshParts(model) {
  const parts = []; model?.traverse(object => { if (object.isMesh && !object.userData.splitOverlay) parts.push(object); }); return parts;
}
const inspectionMaterials = new WeakMap();
function partColor(index) { return new THREE.Color().setHSL((index * .61803398875) % 1, .75, .55); }
function styleParts() {
  for (const view of views) meshParts(view.model).forEach((part, index) => {
    if (!inspectionMaterials.has(part)) inspectionMaterials.set(part, part.material);
    if (part.material !== inspectionMaterials.get(part)) {
      for (const mat of Array.isArray(part.material) ? part.material : [part.material]) mat.dispose();
    }
    part.material = inspectionMaterials.get(part);
    if ($('color-parts').checked) {
      const color = partColor(index);
      const make = () => new THREE.MeshStandardMaterial({color, roughness: .7, side: THREE.DoubleSide});
      part.material = Array.isArray(part.material) ? part.material.map(make) : make();
    }
  });
}
function refreshInspection() {
  styleParts();
  const view = views[Number($('inspect-target').value)];
  const parts = meshParts(view.model), bones = new Set();
  for (const mesh of parts) for (const bone of mesh.skeleton?.bones || []) bones.add(bone);
  const clips = (view === views[0] ? source : packaged)?.animations || [];
  $('rig-info').textContent = `${parts.length} mesh parts · ${parts.filter(p => p.isSkinnedMesh).length} skinned meshes · ${bones.size} bones · ${clips.length} animation clips. Skeleton shown in its current pose.`;
  $('component-note').textContent = parts.length === 1 ? 'This file contains one mesh object. No separately named body components were imported. Show all parts restores visibility; it does not split or label the mesh.' : parts.length ? 'Select Highlight or Isolate beside a name to identify its mesh. Colors match the swatches when Color parts is enabled.' : 'Load a model to inspect its components.';
  $('parts-list').replaceChildren();
  parts.forEach((part, index) => {
    const row = document.createElement('div'); row.className = 'part-row';
    const label = document.createElement('label');
    const swatch = document.createElement('span'); swatch.className = 'part-swatch'; swatch.style.background = '#' + partColor(index).getHexString();
    const checkbox = document.createElement('input'); checkbox.type = 'checkbox'; checkbox.checked = part.visible;
    checkbox.addEventListener('change', () => { part.visible = checkbox.checked; });
    label.append(checkbox, swatch, document.createTextNode(` ${part.name || 'Unnamed mesh'} [${index + 1}]${part.isSkinnedMesh ? ' · skinned' : ''}`));
    const highlight = document.createElement('button'); highlight.textContent = 'Highlight';
    highlight.addEventListener('click', () => {
      styleParts(); part.visible = true;
      const make = material => { const copy = material.clone(); if (copy.emissive) { copy.emissive.set(0xffaa22); copy.emissiveIntensity = 1; } return copy; };
      part.material = Array.isArray(part.material) ? part.material.map(make) : make(part.material);
      $('selected-part').textContent = `Highlighted: ${part.name || 'Unnamed mesh'} [${index + 1}]`;
    });
    const isolate = document.createElement('button'); isolate.textContent = 'Isolate';
    isolate.addEventListener('click', () => { for (const other of parts) other.visible = other === part; refreshInspection(); $('selected-part').textContent = `Isolated: ${part.name || 'Unnamed mesh'} [${index + 1}]`; });
    row.append(label, highlight, isolate); $('parts-list').append(row);
  });
}
$('color-parts').addEventListener('change', styleParts);
$('inspect-target').addEventListener('change', refreshInspection);
$('show-bones').addEventListener('change', () => {
  for (const view of views) for (const helper of view.boneHelpers || []) helper.visible = $('show-bones').checked;
});
$('show-parts').addEventListener('click', () => {
  for (const mesh of meshParts(views[Number($('inspect-target').value)].model)) mesh.visible = true;
  refreshInspection(); $('selected-part').textContent = '';
});
function setPreviewTab(uv) {
  $('converted').hidden = uv; $('uv-panel').hidden = !uv;
  $('model-tab').setAttribute('aria-selected', String(!uv)); $('uv-tab').setAttribute('aria-selected', String(uv));
  if (uv) drawUV();
}
$('model-tab').addEventListener('click', () => setPreviewTab(false));
$('uv-tab').addEventListener('click', () => setPreviewTab(true));
function refreshUV() {
  const select = $('uv-mesh'); select.replaceChildren();
  meshParts(views[1].model).forEach((mesh, index) => {
    const option = document.createElement('option'); option.value = String(index);
    option.textContent = `${mesh.name || 'Unnamed mesh'} [${index + 1}]`; select.append(option);
  });
  drawUV();
}
function drawUV() {
  const canvas = $('uv-canvas'), ctx = canvas.getContext('2d');
  const size = canvas.width, margin = 24, span = size - margin * 2;
  ctx.fillStyle = '#111721'; ctx.fillRect(0, 0, size, size);
  const mesh = meshParts(views[1].model)[Number($('uv-mesh').value)];
  const uv = mesh?.geometry.getAttribute('uv');
  if (!uv) { $('uv-info').textContent = mesh ? 'This mesh has no UV coordinates.' : 'Convert a model to inspect its UV layout.'; return; }
  const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
  const maps = materials.map(material => material?.map).filter(Boolean);
  // A multi-material mesh can have different atlases; do not imply one image covers them all.
  if ($('uv-texture').checked && maps.length === 1 && maps[0].image) {
    try {
      ctx.save();
      if (maps[0].flipY) { ctx.translate(0, size); ctx.scale(1, -1); }
      ctx.drawImage(maps[0].image, margin, margin, span, span); ctx.restore();
    } catch { ctx.restore(); }
  }
  ctx.strokeStyle = '#67788a'; ctx.strokeRect(margin, margin, span, span);
  ctx.save(); ctx.beginPath(); ctx.rect(margin, margin, span, span); ctx.clip();
  ctx.strokeStyle = '#77e8ff'; ctx.lineWidth = .7; ctx.beginPath();
  const indices = mesh.geometry.index, count = indices?.count || uv.count;
  let outside = 0;
  for (let i = 0; i < uv.count; i++) if (uv.getX(i) < 0 || uv.getX(i) > 1 || uv.getY(i) < 0 || uv.getY(i) > 1) outside++;
  for (let i = 0; i + 2 < count; i += 3) {
    for (let corner = 0; corner < 3; corner++) {
      const vertex = indices ? indices.getX(i + corner) : i + corner;
      const x = margin + uv.getX(vertex) * span, y = margin + (1 - uv.getY(vertex)) * span;
      if (corner === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.closePath();
  }
  ctx.stroke(); ctx.restore();
  $('uv-info').textContent = `${Math.floor(count / 3).toLocaleString()} UV triangles · 0–1 tile (V points up). ${outside ? `${outside} UV vertices outside this tile; tiled coordinates are clipped.` : ''} ${maps.length > 1 ? 'Multiple material textures: showing wireframe only.' : ''}`;
}
$('uv-mesh').addEventListener('change', drawUV);
$('uv-texture').addEventListener('change', drawUV);
// Manual regions are attached to the exact selected/exported mesh topology.
let splitState = null, splitOverlay = null, painting = false, paintQueued = false;
function resetSplit() {
  cancelPolygon();
  clearSplitOverlay(); splitState = null; painting = false;
  $('split-brush').checked = false; views[1].controls.enabled = true;
  $('split-mesh').replaceChildren();
  meshParts(views[1].model).forEach((mesh,index) => {
    const option = document.createElement('option'); option.value = index;
    option.textContent = `${mesh.name || 'Unnamed mesh'} [${index+1}]`; $('split-mesh').append(option);
  });
  chooseSplitMesh();
}
function clearSplitOverlay() {
  if (!splitOverlay) return;
  splitOverlay.removeFromParent(); splitOverlay.geometry.dispose();
  for (const mat of splitOverlay.material) mat.dispose(); splitOverlay = null;
}
function chooseSplitMesh() {
  cancelPolygon();
  clearSplitOverlay();
  const mesh = meshParts(views[1].model)[Number($('split-mesh').value)];
  splitState = mesh ? {mesh, labels: new Uint32Array((mesh.geometry.index?.count || mesh.geometry.attributes.position.count)/3), regions: [], undo: []} : null;
  refreshRegions();
}
function refreshRegions() {
  const select = $('split-region'), previous = select.value; select.replaceChildren();
  splitState?.regions.forEach((name,index) => { const option = document.createElement('option'); option.value = index+1; option.textContent = name; select.append(option); });
  if ([...select.options].some(o=>o.value===previous)) select.value = previous;
  $('split-apply').disabled = !splitState?.labels.some(x=>x>0);
  $('split-undo').disabled = !splitState?.undo.length;
  $('split-count').textContent = splitState ? `${splitState.labels.reduce((n,x)=>n+(x>0),0).toLocaleString()} / ${splitState.labels.length.toLocaleString()} faces assigned. ${splitState.regions.length} named regions.` : 'Convert a model first to begin splitting.';
}
function updateSplitOverlay() {
  clearSplitOverlay(); if (!splitState) return;
  const {mesh,labels,regions} = splitState;
  const faces=[]; labels.forEach((label,face)=>{if(label)faces.push(face)});
  if (!faces.length) { refreshRegions(); return; }
  const geometry = extractFaces(mesh.geometry, faces); geometry.clearGroups();
  faces.forEach((face,i)=> { const label=labels[face]-1,last=geometry.groups.at(-1); if(last?.materialIndex===label)last.count+=3;else geometry.addGroup(i*3,3,label); });
  const materials = regions.map((_,i)=>new THREE.MeshBasicMaterial({color:partColor(i), transparent:true, opacity:.8, depthWrite:false, polygonOffset:true, polygonOffsetFactor:-2, polygonOffsetUnits:-2, side:THREE.DoubleSide}));
  splitOverlay = mesh.isSkinnedMesh ? new THREE.SkinnedMesh(geometry,materials) : new THREE.Mesh(geometry,materials);
  if (mesh.isSkinnedMesh) { splitOverlay.skeleton=mesh.skeleton; splitOverlay.bindMode=mesh.bindMode; splitOverlay.bindMatrix.copy(mesh.bindMatrix); splitOverlay.bindMatrixInverse.copy(mesh.bindMatrixInverse); }
  splitOverlay.userData.splitOverlay = true; splitOverlay.renderOrder=5;
  mesh.add(splitOverlay); refreshRegions();
}
function paintFaces(event) {
  if (!splitState) return;
  const region=Number($('split-region').value); if (!region && !$('split-erase').checked) { status('Add a named region before painting.'); return; }
  const view=views[1], rect=view.renderer.domElement.getBoundingClientRect(),radius=Number($('split-size').value);
  const ray=new THREE.Raycaster(),point=new THREE.Vector2();
  const candidates=meshParts(view.model).filter(mesh=>mesh.visible);
  const apply=(x,y)=> {
    point.set((x-rect.left)/rect.width*2-1,-(y-rect.top)/rect.height*2+1);
    ray.setFromCamera(point,view.camera);
    const hit=ray.intersectObjects(candidates,false)[0];
    if(hit?.object===splitState.mesh && hit.faceIndex != null) splitState.labels[hit.faceIndex]=$('split-erase').checked ? 0 : region;
  };
  view.model.updateMatrixWorld(true); apply(event.clientX,event.clientY);
  for(let y=-radius;y<=radius;y+=8)for(let x=-radius;x<=radius;x+=8)if(x*x+y*y<=radius*radius)apply(event.clientX+x,event.clientY+y);
  if(!paintQueued) { paintQueued=true;requestAnimationFrame(()=>{paintQueued=false;updateSplitOverlay();}); }
}
$('split-add').addEventListener('click',()=> {
  if(!splitState)return;
  if(polygonPoints.length){status('Finish or cancel the current polygon before adding a region.');return;}
  const name=$('split-name').value.trim();
  if(!name) {status('Enter a component name.');return;}
  const names=new Set(splitState.regions);
  for(const mesh of meshParts(packaged?.scene))names.add(mesh.name);
  if(names.has(name)){status('Choose a unique component name.');return;}
  splitState.regions.push(name);$('split-name').value='';refreshRegions();$('split-region').value=splitState.regions.length;updateSplitOverlay();
});
$('split-mesh').addEventListener('change',chooseSplitMesh);
$('split-brush').addEventListener('change',selectionMode);
$('split-undo').addEventListener('click',()=> {if(splitState?.undo.length){splitState.labels=splitState.undo.pop();updateSplitOverlay();refreshRegions();}});
const paintCanvas=views[1].renderer.domElement;
paintCanvas.addEventListener('pointerdown',event=> {
  if(!$('split-brush').checked || !splitState || event.button!==0)return;
  event.stopImmediatePropagation();event.preventDefault();
  if($('split-method').value==='polygon') {
    if(!Number($('split-region').value)&&!$('split-erase').checked){status('Add a named region first.');return;}
    const rect=paintCanvas.getBoundingClientRect();
    if(!polygonPoints.length) {views[1].camera.updateMatrixWorld();polygonCamera={world:views[1].camera.matrixWorld.clone(),projection:views[1].camera.projectionMatrix.clone()};}
    polygonPoints.push({x:(event.clientX-rect.left)/rect.width,y:(event.clientY-rect.top)/rect.height});drawPolygon();return;
  }
  painting=true;
  splitState.undo.push(splitState.labels.slice());if(splitState.undo.length>20)splitState.undo.shift();
  paintCanvas.setPointerCapture(event.pointerId);paintFaces(event);
},true);
paintCanvas.addEventListener('pointermove',event=>{if(painting){event.stopImmediatePropagation();paintFaces(event);}},true);
for(const type of ['pointerup','pointercancel','lostpointercapture'])paintCanvas.addEventListener(type,()=>{painting=false;refreshRegions();},true);
$('split-apply').addEventListener('click',async()=> {
  if(!splitState || !convertedBytes)return;
  if(polygonPoints.length){status('Finish or cancel the current polygon before splitting.');return;}
  const state=splitState,index=Number($('split-mesh').value);editBusy(true);
  $('split-apply').disabled=true; $('split-brush').checked=false; views[1].controls.enabled=true;
  try {
    const result=await parseGltf(convertedBytes,new Map());const mesh=meshParts(result.scene)[index];
    const group=splitMesh(mesh,state.labels,state.regions);
    const names=group.children.filter(o=>o.isMesh).map(o=>o.name);
    const bytes=await new GLTFExporter().parseAsync(result.scene,{binary:true,onlyVisible:false,animations:result.animations});
    await publishEditedOutput(bytes,`Split into named components: ${names.join(', ')}`,{manualComponentSplitting:true});
    exportVariant+='-split';$('inspect-target').value='1';refreshInspection();
    status('Component splitting complete. Inspect the named parts, then download GLB or FBX.');
  }catch(error){status(`Splitting failed: ${error.message}`);}
  finally{editBusy(false);refreshRegions();}
});

let polygonPoints=[],polygonCamera=null;
function cancelPolygon() {
  polygonPoints=[];polygonCamera=null;drawPolygon();
}
function drawPolygon() {
  const svg=$('polygon-overlay');svg.replaceChildren();svg.hidden=!polygonPoints.length;
  if(polygonPoints.length) {
    const shape=document.createElementNS('http://www.w3.org/2000/svg','polygon');
    shape.setAttribute('points',polygonPoints.map(p=>`${p.x*1000},${p.y*1000}`).join(' '));svg.append(shape);
    for(const p of polygonPoints) {const dot=document.createElementNS('http://www.w3.org/2000/svg','circle');dot.setAttribute('cx',p.x*1000);dot.setAttribute('cy',p.y*1000);dot.setAttribute('r','5');svg.append(dot);}
  }
  $('polygon-finish').disabled=polygonPoints.length<3;
  $('polygon-back').disabled=!polygonPoints.length;$('polygon-cancel').disabled=!polygonPoints.length;
  $('polygon-info').textContent=polygonPoints.length?`${polygonPoints.length} points. Finish polygon closes the last point to the first and assigns enclosed faces.`:'Click points around the area, then Finish polygon. Faces are selected by their centers; the polygon does not cut through triangles.';
}
function selectionMode() {
  cancelPolygon();painting=false;
  const polygon=$('split-method').value==='polygon';
  $('polygon-controls').hidden=!polygon;$('brush-controls').hidden=polygon;
  views[1].controls.enabled=!$('split-brush').checked;
  if($('split-brush').checked)setPreviewTab(false);
}
$('split-method').addEventListener('change',selectionMode);
$('split-region').addEventListener('change',cancelPolygon);
$('polygon-cancel').addEventListener('click',cancelPolygon);
$('polygon-back').addEventListener('click',()=>{polygonPoints.pop();drawPolygon();});
$('polygon-finish').addEventListener('click',()=> {
  if(!splitState || polygonPoints.length<3)return;
  const camera=views[1].camera;
  if(!camera.matrixWorld.equals(polygonCamera.world)||!camera.projectionMatrix.equals(polygonCamera.projection)) {cancelPolygon();status('Camera changed while drawing. Draw the polygon again in the current view.');return;}
  const region=Number($('split-region').value);
  if(!region&&!$('split-erase').checked){status('Add a named region first.');return;}
  views[1].model.updateMatrixWorld(true);
  const faces=polygonFaces(splitState.mesh,camera,polygonPoints,meshParts(views[1].model).filter(m=>m.visible),$('polygon-through').checked);
  if(faces.length) {
    splitState.undo.push(splitState.labels.slice());if(splitState.undo.length>20)splitState.undo.shift();
    for(const face of faces)splitState.labels[face]=$('split-erase').checked?0:region;
    updateSplitOverlay();
  }
  cancelPolygon();status(`${faces.length.toLocaleString()} faces ${$('split-erase').checked?'unassigned':'assigned to '+splitState.regions[region-1]}. Add more polygons or regions, then split all named regions once.`);
});

selectionMode();
function animate() {
  requestAnimationFrame(animate);
  for (const view of views) {
    const width = view.host.clientWidth, height = view.host.clientHeight;
    if (width && height && (view.renderer.domElement.width !== Math.floor(width * view.renderer.getPixelRatio()) || view.renderer.domElement.height !== Math.floor(height * view.renderer.getPixelRatio()))) {
      view.renderer.setSize(width, height, false);
      view.camera.aspect = width / height;
      view.camera.updateProjectionMatrix();
    }
    view.controls.update();
    view.renderer.render(view.scene, view.camera);
  }
}
animate();
