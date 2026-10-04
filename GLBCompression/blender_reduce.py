"""Reduce a static or skinned mesh with Blender collapse decimation, preserving UV/material data."""
import sys
from pathlib import Path
import bpy
source, output, ratio_text = sys.argv[sys.argv.index('--')+1:]
ratio = float(ratio_text)
if not .01 <= ratio <= 1:
    raise ValueError('Mesh percentage must be 1–100')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
if not meshes:
    raise ValueError('No mesh objects found')
for obj in meshes:
    if obj.data.shape_keys:
        raise ValueError('Mesh reduction cannot preserve morph targets yet. Use 100% mesh detail for this model.')
    bpy.context.view_layer.objects.active=obj
    triangulate=obj.modifiers.new('Triangle target','TRIANGULATE')
    bpy.ops.object.modifier_move_to_index(modifier=triangulate.name, index=0)
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    modifier=obj.modifiers.new('Selected mesh percentage','DECIMATE')
    modifier.decimate_type='COLLAPSE'
    modifier.ratio=ratio
    modifier.use_collapse_triangulate=True
    # Apply geometry changes before skin deformation; keep the armature modifier live.
    bpy.ops.object.modifier_move_to_index(modifier=modifier.name, index=0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.ops.export_scene.gltf(filepath=output, export_format='GLB', export_animations=True)
if not Path(output).is_file():
    raise RuntimeError('Blender did not produce a reduced GLB')
