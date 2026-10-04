import sys
from pathlib import Path
import bpy
source, output = map(Path, sys.argv[sys.argv.index('--') + 1:])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(source))
for image in bpy.data.images:
    if image.packed_file:
        image.unpack(method='WRITE_LOCAL')
for obj in bpy.context.scene.objects:
    if obj.type == 'MESH':
        modifier = obj.modifiers.new('Export triangles', 'TRIANGULATE')
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=False,
    object_types={'MESH', 'ARMATURE', 'EMPTY'}, path_mode='COPY', embed_textures=True,
    add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y')
if not output.is_file():
    raise RuntimeError('Blender did not produce an FBX')
