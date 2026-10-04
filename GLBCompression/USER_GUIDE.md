# GLBCompression: purpose and user guide

## What this tool is for

GLBCompression is a local model preparation tool. Use it between generating or downloading a model (for example from Hunyuan) and importing that model into a game or another authoring tool.

Typical uses:

- Reduce a large generated model to a triangle count and appearance you choose.
- Resize embedded textures to reduce storage and texture memory.
- Convert an FBX into a self-contained GLB, or export the selected model as FBX.
- Inspect imported mesh components, UV layouts, skeletons, and animation clip counts.
- Mark regions on one mesh and export them as separate named mesh objects.
- Unwrap static meshes and rebake their existing textures onto the new UV layout.

The tool does not generate dungeon layouts or place assets in WorldGen. Those jobs belong to the procedural generator and client integration. The tool also does not generate skeletons, automatically recognize body parts, retopologize into a quad mesh, or automatically paint a model from a reference image.

## Start the tool

```bash
cd /home/cody/Coding/WorldGen/GLBCompression
python3 server.py
```

Open <http://127.0.0.1:8765>. Keep the server terminal running.

The bundled viewer is ready to use. Rebuilding requires Node/npm:

```bash
npm ci
npm run build
npm test
```

Blender is required for Blender decimation, UV processing, and FBX export. The server finds `blender` on PATH; `WORLDGEN_BLENDER` can point to another executable. Browser simplification and GLB packaging do not require Blender.

After a Python server update, stop and restart it. After a viewer update, refresh the page; use a hard refresh if the old controls remain.

## Import a model

Choose an FBX, GLB, or glTF. Select external textures and binary files alongside the model when needed. GLB commonly embeds these resources; FBX and glTF can reference separate files.

The original appears on the left. Click **Convert** to create the selected/exported result on the right. Linked cameras let you compare the same view; Reset cameras frames both models.

An imported mesh object is a component the inspector can display. Colored regions shown in another application's preview are not necessarily exported components. The file must contain separate mesh objects, or you must split it here. This viewer does not decode application-specific segmentation metadata.

## Choose mesh and texture percentages

**Mesh detail** is your requested percentage of the original geometry. The default **Blender decimation** method targets approximately that triangle ratio. Counts may differ slightly, particularly for small parts. The displayed actual triangle count is the result that matters.

There is no automatic 30,000-face cap. The displayed Studio reference is informational; choose the percentage suitable for the next tool or game. Check that tool's interpretation of faces versus triangles yourself.

**Texture size** scales each image's width and height. For example, 50% requests half the width and half the height, approximately one quarter of the pixels. A smaller encoded image replaces the original only when it saves bytes, so file-size reduction is not guaranteed for every image.

The **Browser simplifier** is an alternative for static meshes. UV seams and split vertices can limit its reduction; Blender decimation can handle models that the browser method barely reduces.

Inspect silhouette, faces, fingers, thin railings, door clearance, UV seams, and shading. On characters, inspect joint deformation after reimport into an animation-capable tool: fewer vertices can change bending quality even with weights retained.

Changing percentages or reduction method rebuilds from the original. Finish compression before UV edits, image application, or manual splitting, otherwise those edits are replaced.

## Inspect components and bones

Choose **Original** or **Selected / exported** in the inspection panel.

- Visibility checkboxes hide or show individual meshes.
- **Color parts** gives mesh objects distinct preview colors.
- **Highlight** identifies the mesh next to its name.
- **Isolate** hides other meshes in that preview.
- **Show all parts / clear highlight** restores visibility and clears highlighting.
- **Show bones through model** draws imported skeleton connections in the current pose.

The panel reports mesh objects, skinned meshes, unique bones, and animation clip counts. A single bone has no connecting line. Skeleton editing, animation playback, and pose editing are not provided.

These controls change preview copies only. Hiding or recoloring a part does not remove or recolor it in a download. Multi-material components can appear as multiple mesh primitives under a named component parent.

## Split one mesh into named components

Example: label `torso`, `left_arm`, `right_arm`, and `pants` before preparing interchangeable character parts.

1. Import and Convert at your chosen final detail level.
2. Under **Manual component splitting**, select the mesh to work on.
3. Enter a unique component name and click **Add region**.
4. Choose **Polygon** and enable selection.
5. Click points around the desired area in the selected preview.
6. Click **Finish polygon**. This closes the last point to the first and assigns enclosed triangle centers to the active region.
7. Add more polygons to the same region, or add/select another region and repeat.
8. When all regions on that mesh are assigned, click **Split all named regions once**.
9. Inspect the resulting named components and download GLB or FBX to save them.

Finish polygon assigns a region; it does not immediately split geometry. Split all named regions turns all assigned regions on the selected mesh into mesh objects together. To split another existing mesh, select it after applying the first split and repeat.

### Selection controls

- **Select through** includes front and back faces of the selected mesh inside the screen-space polygon, including hidden faces. Leave it off to select only visible surfaces.
- Disable selection to orbit, then enable it again to work from another angle. Finish or cancel a pending polygon before changing the view; camera changes invalidate its outline.
- **Undo point** removes the latest polygon point; **Cancel polygon** discards the pending outline.
- **Erase assignment** returns selected faces to the unassigned remainder.
- **Undo assignment** restores the previous completed polygon or brush stroke, up to 20 operations.
- **Brush** remains available for local corrections. Its radius controls a sampled screen-space area; rotate to reach hidden surfaces.

Later assignments replace earlier ones on overlapping faces. Polygon selection uses whole triangles based on their centers; it does not create new triangle edges along your outline.

### What a split preserves

Splitting copies vertex attributes, including UV sets, normals, colors, skin indices, and skin weights. It retains material assignments and existing skeleton binding. Unassigned faces become a remainder mesh. No faces are deliberately dropped or added.

Cuts leave open edges: the tool does not cap holes or build hidden body surfaces. Morph targets are not supported by splitting. Component boundaries follow the existing topology.

Splitting prepares modular parts but does not make unrelated replacement parts fit automatically. Replacement torsos/clothing still need compatible skeletons, weights, proportions, and attachment boundaries. Armor may be added separately while hiding body regions beneath it.

Unapplied region selections are temporary. Apply the split and download the model to save the result.

## UV layout and texture workflows

The selected preview has **Model** and **UV layout** tabs. Choose a mesh in UV layout to inspect UV triangle outlines. **Texture background** shows its base-color image when a single applicable map exists. Multi-map meshes show wireframe rather than imply that one image covers all materials.

The viewer displays the first UV set in the 0–1 tile. Tiled coordinates outside that tile are clipped and reported.

### Unwrap UVs + preserve textures

For static meshes, choose a bake size (256, 512, 1024, or 2048) and click **Unwrap UVs + preserve textures**. Blender Smart UV Project makes a new layout, and CPU Cycles rebakes existing base color, alpha, roughness, metallic, normals, and emission onto that layout. The UV tab opens when processing finishes.

Existing maps cannot simply be reused with unrelated UVs, so rebaking transfers their appearance. Materials are consolidated per mesh. Custom shaders and advanced layered materials are not fully supported. Inspect seams and appearance after baking. Processing can take several minutes.

### Apply base-color image

Select a PNG, JPEG, or WebP to embed it as the selected model's base-color map using its existing UVs. This applies the image across the model's meshes; it is not a per-component painting editor. It cannot infer placement from a photograph, create missing UVs, or recover paint from an unrelated UV layout.

If another authoring tool discards textures while changing UVs, keep the original textured model. Reassignment alone will not recover its original appearance when the new UVs differ; a transfer/rebake workflow is needed.

## Character support and limits

| Operation | Static model | Skinned character | Morph targets |
| --- | --- | --- | --- |
| Import and inspect | Supported | Skeleton and skin inspection | Loading supported; no morph editing UI |
| GLB packaging at 100% mesh detail | Supported | Imported skin and clips included | Prefer unchanged GLB; verify other conversions |
| Blender decimation | Supported | Armature and weights retained; inspect bending | Unsupported |
| Browser simplification | Supported | Unsupported | Unsupported |
| UV unwrapping/rebaking | Supported | Unsupported | Unsupported |
| Manual component splitting | Supported | Skin binding retained | Unsupported |
| FBX export | Supported | Armatures and skin binding included | No preservation guarantee; verify before use |

GLB export paths include imported animation clips where applicable. FBX export currently does not export animation clips. A skeleton's presence alone does not establish compatibility with your game's animations. Inspect the final file in the target client or animation tool.

## Export and store the asset

**Download GLB** saves the selected model bytes; the selected preview reloads that same GLB. **Download FBX** asks local Blender to convert those selected bytes into triangulated FBX with embedded textures where supported. Inspect materials after FBX reimport.

**Download metadata JSON** is optional. It records counts, settings, byte length, and SHA-256, not geometry. It is distinct from WorldGen's placement `asset.json` and does not define dimensions, sockets, or collision.

Store approved content together:

```text
Assets/subway/architecture/stairs/my-step/
  reference.png
  original.glb
  game.glb
```

Retain an original FBX if that is your source format. Preserve the original; save or copy the reviewed compressed export as `game.glb`. The tool does not automatically write downloaded files into the asset library or approve them for gameplay.

For procedural use, also review scale in meters, pivot, allowed orientation, connection spacing, traversal clearance, and collision. The current portable WorldGen catalog/importer covers static doorway frames; extending it to stairs, floors, props, or characters requires additional placement/integration work. See [portable integration](../docs/INTEGRATION.md).

## Troubleshooting

- **404 during UV processing:** restart `python3 server.py` from this directory, then refresh the page. An old server can serve a new viewer without the newer routes.
- **Missing texture/file:** select external textures and binaries with the model.
- **Only one component:** the file contains one mesh; source-tool preview segments may not have been applied/exported. Use the source tool's completed split export or split manually here.
- **Few triangles removed:** choose Blender decimation and inspect the actual count.
- **Morph-target error:** keep mesh detail at 100% and avoid manual splitting; morph preservation is not implemented for these operations.
- **UV operation on a rigged model:** UV processing is static-only. Complete UV work before rigging or use a character authoring tool.
- **Edits disappeared after changing percentage:** compression settings rebuild from the source. Repeat edits after choosing the final settings.

Legacy JSON packages can be recovered without overwriting the source:

```bash
python3 migrate_json.py /path/to/model.model.json
```
