"""Smart-project static meshes and rebake PBR appearance to the new UV layout."""
import math
import sys
from pathlib import Path
import bpy

source, output, resolution = sys.argv[sys.argv.index('--')+1:]
resolution = int(resolution)
if resolution not in (256, 512, 1024, 2048):
    raise ValueError('Unsupported bake resolution')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 1
scene.cycles.use_denoising = False
scene.render.bake.margin = 8
scene.render.bake.use_clear = True
scene.render.bake.use_pass_direct = False
scene.render.bake.use_pass_indirect = False
scene.render.bake.use_pass_color = True
meshes = [obj for obj in scene.objects if obj.type == 'MESH']
if not meshes:
    raise ValueError('No mesh objects')

for index, obj in enumerate(meshes):
    if obj.data.shape_keys or any(mod.type == 'ARMATURE' for mod in obj.modifiers):
        raise ValueError('UV tool supports static models before rigging')
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # Isolate shared data/materials so another object's bake cannot replace them.
    obj.data = obj.data.copy()
    if not obj.data.materials:
        material = bpy.data.materials.new('Default')
        material.use_nodes = True
        obj.data.materials.append(material)
    materials = []
    for i, slot in enumerate(obj.material_slots):
        material = slot.material.copy() if slot.material else bpy.data.materials.new('Default')
        material.use_nodes = True
        slot.material = material
        materials.append(material)
    old_uv = obj.data.uv_layers.active
    old_name = old_uv.name if old_uv else None
    for material in materials:
        tree = material.node_tree
        if old_name:
            mapping = tree.nodes.new('ShaderNodeUVMap')
            mapping.uv_map = old_name
            for node in list(tree.nodes):
                if node.type == 'TEX_IMAGE' and not node.inputs['Vector'].is_linked:
                    tree.links.new(mapping.outputs['UV'], node.inputs['Vector'])
                if node.type == 'NORMAL_MAP' and not node.uv_map:
                    node.uv_map = old_name
                if node.type == 'TEX_COORD':
                    for link in list(node.outputs['UV'].links):
                        tree.links.new(mapping.outputs['UV'], link.to_socket)
        elif any(node.type == 'TEX_IMAGE' and node.image for node in tree.nodes):
            raise ValueError('Textured model has no source UVs; cannot recover original texture placement automatically')
    uv = obj.data.uv_layers.new(name='WorldGenUV')
    obj.data.uv_layers.active = uv
    uv.active_render = True
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    baked = {}
    channels = [('color','Base Color',(0.8,0.8,0.8,1)), ('roughness','Roughness',.5),
                ('metallic','Metallic',0), ('alpha','Alpha',1), ('emission','Emission Color',(0,0,0,1))]
    for name, socket_name, default in channels + [('normal',None,None)]:
        image = bpy.data.images.new(f'WorldGen_{index}_{name}', width=resolution, height=resolution, alpha=True)
        image.colorspace_settings.name = 'sRGB' if name in ('color','emission') else 'Non-Color'
        image.generated_color = (0.5,0.5,1,1) if name=='normal' else (0,0,0,1)
        restore = []
        for material in materials:
            tree = material.node_tree
            target = tree.nodes.new('ShaderNodeTexImage')
            target.image = image
            tree.nodes.active = target
            output_node = next(node for node in tree.nodes if node.type=='OUTPUT_MATERIAL' and node.is_active_output)
            if name != 'normal':
                original = output_node.inputs['Surface'].links[0].from_socket if output_node.inputs['Surface'].is_linked else None
                principled = next((node for node in tree.nodes if node.type=='BSDF_PRINCIPLED'),None)
                if principled is None:
                    raise ValueError('Material cannot be rebaked: no Principled shader')
                value = principled.inputs.get(socket_name)
                emitter = tree.nodes.new('ShaderNodeEmission')
                if value and value.is_linked:
                    tree.links.new(value.links[0].from_socket, emitter.inputs['Color'])
                else:
                    color = value.default_value if value else default
                    if isinstance(color,(int,float)):
                        color = (color,color,color,1)
                    emitter.inputs['Color'].default_value = color
                if name == 'emission':
                    strength = principled.inputs.get('Emission Strength')
                    if strength and strength.is_linked:
                        tree.links.new(strength.links[0].from_socket,emitter.inputs['Strength'])
                    elif strength:emitter.inputs['Strength'].default_value=strength.default_value
                tree.links.new(emitter.outputs[0],output_node.inputs['Surface'])
                restore.append((tree,output_node,original,emitter,target))
            else:restore.append((tree,output_node,None,None,target))
        for tree, output_node, original, emitter, target in restore:
            tree.nodes.active = target
        bpy.context.view_layer.update()
        bpy.ops.object.bake(type='NORMAL' if name=='normal' else 'EMIT')
        for tree, output_node, original, emitter, target in restore:
            if emitter:
                tree.nodes.remove(emitter)
                if original:tree.links.new(original,output_node.inputs['Surface'])
            tree.nodes.remove(target)
        baked[name] = image
    # Pack baked alpha into the base-color texture for glTF/FBX export.
    color_pixels = list(baked['color'].pixels)
    alpha_pixels = list(baked['alpha'].pixels)
    for i in range(3,len(color_pixels),4):color_pixels[i]=alpha_pixels[i-3]
    baked['color'].pixels[:] = color_pixels
    result = bpy.data.materials.new(f'WorldGen_{index}_Baked')
    result.use_nodes = True
    tree = result.node_tree
    shader = next(node for node in tree.nodes if node.type=='BSDF_PRINCIPLED')
    for name, socket_name, default in channels:
        if name=='alpha':continue
        node=tree.nodes.new('ShaderNodeTexImage');node.image=baked[name]
        tree.links.new(node.outputs['Color'],shader.inputs[socket_name])
        if name=='color':tree.links.new(node.outputs['Alpha'],shader.inputs['Alpha'])
    if any(m.blend_method != 'OPAQUE' for m in materials):result.blend_method='BLEND'
    shader.inputs['Emission Strength'].default_value = 1
    normal=tree.nodes.new('ShaderNodeTexImage');normal.image=baked['normal']
    normal_map=tree.nodes.new('ShaderNodeNormalMap')
    tree.links.new(normal.outputs['Color'],normal_map.inputs['Color'])
    tree.links.new(normal_map.outputs['Normal'],shader.inputs['Normal'])
    obj.data.materials.clear();obj.data.materials.append(result)
    for polygon in obj.data.polygons:polygon.material_index=0
    # Remove old UV layers so the new atlas is always exported as TEXCOORD_0.
    for layer in list(obj.data.uv_layers):
        if layer != uv:obj.data.uv_layers.remove(layer)
    for image in baked.values():image.pack()
bpy.ops.export_scene.gltf(filepath=output,export_format='GLB',export_animations=False)
if not Path(output).is_file():raise RuntimeError('UV export failed')
